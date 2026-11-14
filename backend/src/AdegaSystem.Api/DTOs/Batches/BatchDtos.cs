using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Batches;

/// <summary>Um lote, com os campos calculados de validade para a tela não precisar calcular.</summary>
/// <param name="DaysToExpire">Dias até vencer (negativo se já venceu). Nulo para lotes sem validade.</param>
/// <param name="IsExpired">Validade antes de hoje. Um lote que vence hoje ainda pode ser vendido hoje.</param>
public record BatchDto(
    int Id,
    int ProductId,
    string ProductName,
    string BatchNumber,
    int? PurchaseId,
    string? SupplierName,
    int QuantityReceived,
    int QuantityAvailable,
    DateOnly? ManufacturingDate,
    DateOnly? ExpirationDate,
    int? DaysToExpire,
    bool IsExpired,
    decimal UnitCost,
    DateTime CreatedAt);

public enum BatchStatusFilter
{
    /// <summary>Validade antes de hoje.</summary>
    Expired,

    /// <summary>Validade entre hoje e hoje + Days.</summary>
    Expiring,
}

/// <summary>Filtros do GET /api/batches.</summary>
public class BatchQuery : PagedQuery
{
    private int _days = 30;

    public int? ProductId { get; set; }

    /// <summary>Por padrão só lotes com saldo. false inclui os lotes já esgotados.</summary>
    public bool OnlyAvailable { get; set; } = true;

    public BatchStatusFilter? Status { get; set; }

    /// <summary>Janela do filtro Expiring (7, 30, 60...). Fica entre 1 e 365.</summary>
    public int Days
    {
        get => _days;
        set => _days = Math.Clamp(value, 1, 365);
    }
}

/// <summary>Corpo do POST /api/batches/{id}/write-off. O motivo é opcional; sem ele, um texto padrão é usado.</summary>
public record WriteOffBatchDto(string? Reason);
