using System.Linq.Expressions;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Lógica centralizada de estoque. É o único lugar que altera Product.CurrentStock e
/// ProductBatch.QuantityAvailable, sempre os dois juntos e sempre gravando uma StockMovement.
/// Assim, o estoque do produto é sempre a soma dos saldos dos seus lotes.
///
/// - ReceiveBatchAsync: entrada que cria um lote novo (compras).
/// - RegisterMovementAsync: entrada ou saída num lote existente (vendas, cancelamentos, ajustes, perdas, vencidos).
/// </summary>
public class StockService(
    AdegaDbContext context,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IValidator<StockAdjustmentDto> adjustmentValidator) : IStockService
{
    public async Task<ProductBatch> ReceiveBatchAsync(ReceiveBatchRequest request)
    {
        EnsureTransaction();
        if (request.Quantity <= 0)
        {
            throw new ArgumentException($"A entrada de um lote precisa de quantidade positiva ({request.Quantity}).", nameof(request));
        }

        var product = await context.Products.FindAsync(request.ProductId)
            ?? throw new NotFoundException("Produto não encontrado.");

        var batch = new ProductBatch
        {
            ProductId = product.Id,
            PurchaseId = request.PurchaseId,
            BatchNumber = request.BatchNumber,
            QuantityReceived = request.Quantity,
            ManufacturingDate = request.ManufacturingDate,
            ExpirationDate = request.ExpirationDate,
            UnitCost = request.UnitCost,
        };
        context.ProductBatches.Add(batch);

        await ApplyAsync(product, batch, MovementType.PURCHASE, request.Quantity, request.Reason, request.ReferenceType, request.ReferenceId);
        return batch;
    }

    public async Task<StockMovement> RegisterMovementAsync(StockMovementRequest request)
    {
        EnsureTransaction();
        EnsureQuantitySign(request.Type, request.Quantity);

        // FindAsync devolve a mesma instância que o service chamador já tenha carregado,
        // então os dois enxergam o mesmo saldo.
        var batch = await context.ProductBatches.FindAsync(request.BatchId)
            ?? throw new NotFoundException("Lote não encontrado.");
        var product = await context.Products.FindAsync(batch.ProductId)
            ?? throw new NotFoundException("Produto não encontrado.");

        // A saída é sempre de um lote específico: não adianta o produto ter estoque em outros lotes.
        if (batch.QuantityAvailable + request.Quantity < 0)
        {
            throw new BusinessException(
                $"Saldo insuficiente no lote {batch.BatchNumber} de \"{product.Name}\". " +
                $"Disponível: {batch.QuantityAvailable}, solicitado: {-request.Quantity}.");
        }

        return await ApplyAsync(product, batch, request.Type, request.Quantity, request.Reason, request.ReferenceType, request.ReferenceId);
    }

    public async Task<StockMovementDto> AdjustAsync(StockAdjustmentDto dto)
    {
        await adjustmentValidator.ValidateAndThrowAsync(dto);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var movement = await RegisterMovementAsync(new StockMovementRequest(dto.BatchId, dto.Type, dto.Quantity, dto.Reason.Trim()));

        // Ajuste manual é uma das ações que a auditoria precisa registrar (vendas e compras já têm o próprio documento).
        var product = await context.Products.FindAsync(movement.ProductId);
        auditService.Add(AuditAction.STOCK_ADJUSTED, nameof(Product), movement.ProductId, product!.Name,
            new { stock = movement.PreviousStock },
            new { stock = movement.NewStock, type = dto.Type.ToString(), batch = movement.Batch!.BatchNumber, reason = movement.Reason });
        await context.SaveChangesAsync();

        await transaction.CommitAsync();

        return await context.StockMovements.AsNoTracking()
            .Where(m => m.Id == movement.Id)
            .Select(ToDto)
            .FirstAsync();
    }

    /// <summary>
    /// O coração da regra: muda o lote e o produto pela mesma quantidade e grava a movimentação.
    /// Os três são salvos no mesmo SaveChanges, dentro da transação de quem chamou.
    /// </summary>
    private async Task<StockMovement> ApplyAsync(
        Product product, ProductBatch batch, MovementType type, int quantity, string reason, string? referenceType, int? referenceId)
    {
        var previousStock = product.CurrentStock;
        batch.ApplyQuantityChange(quantity);
        product.ApplyStockChange(quantity);

        var movement = new StockMovement
        {
            ProductId = product.Id,
            // Pela navegação, o EF preenche o BatchId mesmo quando o lote acabou de ser criado e ainda não tem Id.
            Batch = batch,
            MovementType = type,
            Quantity = quantity,
            PreviousStock = previousStock,
            NewStock = product.CurrentStock,
            Reason = reason,
            UserId = currentUser.UserId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
        };
        context.StockMovements.Add(movement);

        // Nada fica definitivo até quem chamou fazer o Commit da transação.
        await context.SaveChangesAsync();
        return movement;
    }

    /// <summary>
    /// Sem transação, uma compra com 3 itens poderia gravar o estoque dos 2 primeiros e falhar no 3º.
    /// Exigir a transação aqui impede que alguém esqueça dela.
    /// </summary>
    private void EnsureTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Movimentações de estoque precisam acontecer dentro de uma transação (context.Database.BeginTransactionAsync).");
        }
    }

    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQuery query)
    {
        var movements = context.StockMovements.AsNoTracking();

        if (query.ProductId.HasValue)
        {
            movements = movements.Where(m => m.ProductId == query.ProductId.Value);
        }

        if (query.Type.HasValue)
        {
            movements = movements.Where(m => m.MovementType == query.Type.Value);
        }

        // O dia é o de Brasília (ver BusinessClock). "To" inclui o dia inteiro: vai até o começo do dia seguinte.
        if (query.From.HasValue)
        {
            var start = BusinessClock.StartOfDayUtc(query.From.Value);
            movements = movements.Where(m => m.CreatedAt >= start);
        }

        if (query.To.HasValue)
        {
            var end = BusinessClock.StartOfDayUtc(query.To.Value.AddDays(1));
            movements = movements.Where(m => m.CreatedAt < end);
        }

        var totalItems = await movements.CountAsync();

        var page = await movements
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new PagedResult<StockMovementDto>(page, query.Page, query.PageSize, totalItems);
    }

    /// <summary>
    /// Conversão para DTO escrita como Expression, para o EF conseguir traduzir em SQL:
    /// vira um SELECT com JOIN em Products e Users, trazendo só as colunas usadas.
    /// </summary>
    private static readonly Expression<Func<StockMovement, StockMovementDto>> ToDto = m => new StockMovementDto(
        m.Id,
        m.ProductId,
        m.Product.Name,
        m.MovementType,
        m.BatchId,
        m.Batch == null ? null : m.Batch.BatchNumber,
        m.Quantity,
        m.PreviousStock,
        m.NewStock,
        m.Reason,
        m.ReferenceType,
        m.ReferenceId,
        m.User.Name,
        m.CreatedAt);

    /// <summary>
    /// Cada tipo tem um sinal fixo. Um sinal errado aqui é erro de programação
    /// (o usuário nunca chega até este ponto sem passar por um validator), por isso ArgumentException.
    /// PURCHASE não entra aqui: toda compra cria um lote novo, pelo ReceiveBatchAsync.
    /// </summary>
    private static void EnsureQuantitySign(MovementType type, int quantity)
    {
        var valid = type switch
        {
            MovementType.RETURN => quantity > 0,
            MovementType.SALE or MovementType.LOSS or MovementType.EXPIRED => quantity < 0,
            MovementType.ADJUSTMENT => quantity != 0,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException($"Quantidade {quantity} inválida para movimentação do tipo {type}.", nameof(quantity));
        }
    }
}
