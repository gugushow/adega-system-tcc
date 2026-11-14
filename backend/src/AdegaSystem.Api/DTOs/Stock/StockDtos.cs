using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Stock;

/// <summary>
/// Pedido interno de entrada: cria um lote novo (usado pelas compras).
/// Toda compra gera um lote próprio, mesmo que o número coincida com o de outra compra.
/// </summary>
public record ReceiveBatchRequest(
    int ProductId,
    int Quantity,
    decimal UnitCost,
    string BatchNumber,
    DateOnly? ManufacturingDate,
    DateOnly? ExpirationDate,
    int? PurchaseId,
    string Reason,
    string? ReferenceType = null,
    int? ReferenceId = null);

/// <summary>
/// Pedido interno de movimentação num lote que já existe (vendas, cancelamentos de venda,
/// ajustes, perdas e vencidos). O produto vem do próprio lote.
/// </summary>
public record StockMovementRequest(
    int BatchId,
    MovementType Type,
    int Quantity,
    string Reason,
    string? ReferenceType = null,
    int? ReferenceId = null);

/// <summary>
/// Corpo do POST /api/stock/adjustments. Type é ADJUSTMENT (correção, + ou −)
/// ou LOSS (perda, sempre negativa). Quantity tem sinal: -2 = saíram 2 unidades do lote.
/// </summary>
public record StockAdjustmentDto(int BatchId, MovementType Type, int Quantity, string Reason);

public record StockMovementDto(
    int Id,
    int ProductId,
    string ProductName,
    MovementType MovementType,
    int? BatchId,
    string? BatchNumber,
    int Quantity,
    int PreviousStock,
    int NewStock,
    string Reason,
    string? ReferenceType,
    int? ReferenceId,
    string UserName,
    DateTime CreatedAt);

/// <summary>Filtros do GET /api/stock-movements. Datas no horário de Brasília.</summary>
public class StockMovementQuery : PagedQuery
{
    public int? ProductId { get; set; }
    public MovementType? Type { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
