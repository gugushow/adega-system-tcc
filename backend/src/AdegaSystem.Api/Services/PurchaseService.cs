using System.Linq.Expressions;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class PurchaseService(
    AdegaDbContext context,
    ICurrentUserService currentUser,
    IStockService stockService,
    IValidator<CreatePurchaseDto> validator) : IPurchaseService
{
    public async Task<PagedResult<PurchaseDto>> GetAllAsync(PurchaseQuery query)
    {
        var purchases = context.Purchases.AsNoTracking();

        if (query.SupplierId.HasValue)
        {
            purchases = purchases.Where(p => p.SupplierId == query.SupplierId.Value);
        }

        if (query.From.HasValue)
        {
            purchases = purchases.Where(p => p.PurchaseDate >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            purchases = purchases.Where(p => p.PurchaseDate <= query.To.Value);
        }

        var totalItems = await purchases.CountAsync();

        var page = await purchases
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new PagedResult<PurchaseDto>(page, query.Page, query.PageSize, totalItems);
    }

    public async Task<PurchaseDetailsDto> GetByIdAsync(int id)
    {
        var purchase = await context.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.User)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Items).ThenInclude(i => i.Batch)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Compra não encontrada.");

        return new PurchaseDetailsDto(
            purchase.Id,
            purchase.SupplierId,
            purchase.Supplier.Name,
            purchase.InvoiceNumber,
            purchase.PurchaseDate,
            purchase.TotalAmount,
            purchase.User.Name,
            purchase.CreatedAt,
            purchase.Items
                .OrderBy(i => i.Id)
                .Select(i => new PurchaseItemDto(
                    i.Id, i.ProductId, i.Product.Name, i.Quantity, i.UnitCost, i.Total,
                    i.BatchId, i.Batch?.BatchNumber, i.Batch?.ManufacturingDate, i.Batch?.ExpirationDate))
                .ToList());
    }

    public async Task<PurchaseDetailsDto> CreateAsync(CreatePurchaseDto dto)
    {
        await validator.ValidateAndThrowAsync(dto);

        // 1) Conferências que dependem do banco, antes de gravar qualquer coisa.
        await EnsureSupplierIsActiveAsync(dto.SupplierId);

        var invoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? null : dto.InvoiceNumber.Trim();
        await EnsureInvoiceIsNewAsync(dto.SupplierId, invoiceNumber);

        // Carrega os produtos rastreados (sem AsNoTracking), porque o custo deles vai mudar.
        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        var products = await context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new BusinessException($"O produto de código {item.ProductId} não existe.");
            }

            if (!product.Active)
            {
                throw new BusinessException($"O produto \"{product.Name}\" está inativo e não pode ser comprado.");
            }
        }

        // 2) Monta a compra. Os totais são sempre calculados aqui, nunca recebidos do frontend.
        var purchase = new Purchase
        {
            SupplierId = dto.SupplierId,
            InvoiceNumber = invoiceNumber,
            PurchaseDate = dto.PurchaseDate,
            UserId = currentUser.UserId,
            Items = dto.Items.Select(i => new PurchaseItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                // Arredondamento comercial (0,005 sobe), o mesmo de uma calculadora.
                Total = Math.Round(i.Quantity * i.UnitCost, 2, MidpointRounding.AwayFromZero),
            }).ToList(),
        };
        purchase.TotalAmount = purchase.Items.Sum(i => i.Total);

        // 3) Grava tudo numa transação: compra, itens, lotes, estoque, movimentações, custo e histórico.
        // Se qualquer passo falhar, o "await using" desfaz tudo (rollback) ao sair do método sem Commit.
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Purchases.Add(purchase);
        await context.SaveChangesAsync(); // gera o Id da compra, usado no motivo da movimentação

        // Cada item vira um lote. Items foi montado na mesma ordem de dto.Items, então o índice casa os dois.
        var items = purchase.Items.ToList();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var input = dto.Items[index];

            await UpdateCostAsync(products[item.ProductId], item, purchase);

            // O StockService cria o lote, soma o estoque do produto e grava a movimentação PURCHASE.
            var batch = await stockService.ReceiveBatchAsync(new ReceiveBatchRequest(
                item.ProductId,
                item.Quantity,
                item.UnitCost,
                string.IsNullOrWhiteSpace(input.BatchNumber) ? $"COMPRA-{purchase.Id}" : input.BatchNumber.Trim(),
                input.ManufacturingDate,
                input.ExpirationDate,
                purchase.Id,
                $"Compra #{purchase.Id}",
                ReferenceType: nameof(Purchase),
                ReferenceId: purchase.Id));
            item.BatchId = batch.Id;
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetByIdAsync(purchase.Id);
    }

    /// <summary>
    /// Registra o custo pago no histórico e atualiza o custo do produto.
    /// Regra: o custo do produto é o da compra mais recente. Se alguém lançar hoje uma nota
    /// antiga, ela entra no histórico, mas não substitui um custo mais novo.
    /// </summary>
    private async Task UpdateCostAsync(Product product, PurchaseItem item, Purchase purchase)
    {
        var hasNewerCost = await context.ProductPriceHistories
            .AnyAsync(h => h.ProductId == product.Id && h.Date > purchase.PurchaseDate);

        context.ProductPriceHistories.Add(new ProductPriceHistory
        {
            ProductId = product.Id,
            SupplierId = purchase.SupplierId,
            PurchaseId = purchase.Id,
            CostPrice = item.UnitCost,
            Date = purchase.PurchaseDate,
        });

        if (!hasNewerCost)
        {
            product.CostPrice = item.UnitCost;
            product.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task EnsureSupplierIsActiveAsync(int supplierId)
    {
        var supplier = await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId)
            ?? throw new BusinessException("O fornecedor informado não existe.");

        if (!supplier.Active)
        {
            throw new BusinessException("O fornecedor informado está inativo.");
        }
    }

    private async Task EnsureInvoiceIsNewAsync(int supplierId, string? invoiceNumber)
    {
        if (invoiceNumber is null)
        {
            return;
        }

        var exists = await context.Purchases.AnyAsync(p => p.SupplierId == supplierId && p.InvoiceNumber == invoiceNumber);
        if (exists)
        {
            throw new BusinessException("Já existe uma compra com essa nota fiscal para este fornecedor.");
        }
    }

    /// <summary>Conversão para a linha da listagem, traduzida em SQL pelo EF (JOIN com fornecedor e usuário).</summary>
    private static readonly Expression<Func<Purchase, PurchaseDto>> ToDto = p => new PurchaseDto(
        p.Id,
        p.SupplierId,
        p.Supplier.Name,
        p.InvoiceNumber,
        p.PurchaseDate,
        p.TotalAmount,
        p.Items.Count,
        p.User.Name,
        p.CreatedAt);
}
