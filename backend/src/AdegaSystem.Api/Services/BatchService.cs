using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Batches;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>Consultas de lotes e validade, e a baixa de lotes vencidos.</summary>
public class BatchService(AdegaDbContext context, IStockService stockService, IAuditService auditService) : IBatchService
{
    public async Task<PagedResult<BatchDto>> GetAllAsync(BatchQuery query)
    {
        // "Hoje" no horário de Brasília (ver BusinessClock).
        var today = BusinessClock.Today;
        var batches = context.ProductBatches.AsNoTracking();

        if (query.ProductId.HasValue)
        {
            batches = batches.Where(b => b.ProductId == query.ProductId.Value);
        }

        if (query.OnlyAvailable)
        {
            batches = batches.Where(b => b.QuantityAvailable > 0);
        }

        // Lotes sem validade (ExpirationDate nulo) nunca entram nestes dois filtros.
        if (query.Status == BatchStatusFilter.Expired)
        {
            batches = batches.Where(b => b.ExpirationDate < today);
        }
        else if (query.Status == BatchStatusFilter.Expiring)
        {
            var limit = today.AddDays(query.Days);
            batches = batches.Where(b => b.ExpirationDate >= today && b.ExpirationDate <= limit);
        }

        var totalItems = await batches.CountAsync();

        // Vence primeiro, aparece primeiro (a mesma ordem que o FEFO vai usar nas vendas).
        // "ExpirationDate == null" vale false para quem tem data, então os lotes sem validade vão para o fim.
        var page = await batches
            .OrderBy(b => b.ExpirationDate == null)
            .ThenBy(b => b.ExpirationDate)
            .ThenBy(b => b.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Include(b => b.Product)
            .Include(b => b.Purchase).ThenInclude(p => p!.Supplier)
            .ToListAsync();

        return new PagedResult<BatchDto>(page.Select(b => ToDto(b, today)).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<BatchDto> GetByIdAsync(int id)
    {
        var batch = await context.ProductBatches.AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Purchase).ThenInclude(p => p!.Supplier)
            .FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Lote não encontrado.");

        return ToDto(batch, BusinessClock.Today);
    }

    public async Task<BatchDto> WriteOffAsync(int id, WriteOffBatchDto dto)
    {
        var batch = await context.ProductBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Lote não encontrado.");

        var today = BusinessClock.Today;
        if (batch.ExpirationDate is null || batch.ExpirationDate >= today)
        {
            throw new BusinessException("Só é possível dar baixa em lotes vencidos. Para outras saídas, use o ajuste de estoque.");
        }

        if (batch.QuantityAvailable == 0)
        {
            throw new BusinessException("Este lote não tem mais unidades para dar baixa.");
        }

        var reason = string.IsNullOrWhiteSpace(dto.Reason)
            ? $"Lote {batch.BatchNumber} vencido em {batch.ExpirationDate:dd/MM/yyyy}"
            : dto.Reason.Trim();
        if (reason.Length > 255)
        {
            throw new BusinessException("O motivo deve ter no máximo 255 caracteres.");
        }

        // Retira todo o saldo do lote, pelo mesmo caminho central de qualquer saída.
        await using var transaction = await context.Database.BeginTransactionAsync();
        var movement = await stockService.RegisterMovementAsync(new StockMovementRequest(
            batch.Id, MovementType.EXPIRED, -batch.QuantityAvailable, reason, nameof(ProductBatch), batch.Id));

        var productName = await context.Products.Where(p => p.Id == batch.ProductId).Select(p => p.Name).FirstAsync();
        auditService.Add(AuditAction.BATCH_WRITTEN_OFF, nameof(ProductBatch), batch.Id, $"{productName} · lote {batch.BatchNumber}",
            new { batchQuantity = batch.QuantityAvailable, stock = movement.PreviousStock },
            new { batchQuantity = 0, stock = movement.NewStock, reason });
        await context.SaveChangesAsync();

        await transaction.CommitAsync();

        return await GetByIdAsync(id);
    }

    private static BatchDto ToDto(ProductBatch batch, DateOnly today) => new(
        batch.Id,
        batch.ProductId,
        batch.Product.Name,
        batch.BatchNumber,
        batch.PurchaseId,
        batch.Purchase?.Supplier.Name,
        batch.QuantityReceived,
        batch.QuantityAvailable,
        batch.ManufacturingDate,
        batch.ExpirationDate,
        batch.ExpirationDate is { } expiration ? expiration.DayNumber - today.DayNumber : null,
        batch.ExpirationDate < today,
        batch.UnitCost,
        batch.CreatedAt);
}
