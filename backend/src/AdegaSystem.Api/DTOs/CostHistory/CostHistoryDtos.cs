namespace AdegaSystem.Api.DTOs.CostHistory;

/// <summary>Resposta do GET /api/products/{id}/cost-history.</summary>
/// <param name="TotalChangePercent">Variação (%) entre o primeiro e o último custo. Nulo com menos de 2 registros.</param>
public record CostHistoryDto(
    int ProductId,
    string ProductName,
    decimal CurrentCostPrice,
    decimal? TotalChangePercent,
    IReadOnlyList<CostHistoryEntryDto> Entries);

/// <summary>Um custo pago numa compra.</summary>
/// <param name="ChangePercent">Variação (%) em relação ao registro anterior. Nulo no primeiro.</param>
public record CostHistoryEntryDto(
    int Id,
    DateOnly Date,
    decimal CostPrice,
    decimal? ChangePercent,
    int SupplierId,
    string SupplierName,
    int PurchaseId);
