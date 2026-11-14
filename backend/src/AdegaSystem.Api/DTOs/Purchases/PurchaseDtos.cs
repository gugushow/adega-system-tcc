using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Purchases;

/// <summary>Corpo do POST /api/purchases. Totais não são enviados: a API calcula.</summary>
public record CreatePurchaseDto(
    int SupplierId,
    string? InvoiceNumber,
    DateOnly PurchaseDate,
    IReadOnlyList<CreatePurchaseItemDto> Items);

/// <summary>
/// Um item da compra: quantidade em unidades, custo de cada unidade e os dados do lote.
/// Cada item vira um lote. O mesmo produto pode vir em várias linhas, uma por lote.
/// </summary>
/// <param name="BatchNumber">Código do lote na embalagem. Em branco, a API gera "COMPRA-{id}".</param>
/// <param name="ExpirationDate">Validade. Em branco para produtos que não vencem (gelo, por exemplo).</param>
public record CreatePurchaseItemDto(
    int ProductId,
    int Quantity,
    decimal UnitCost,
    string? BatchNumber = null,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpirationDate = null);

/// <summary>Uma linha da listagem de compras (sem os itens).</summary>
public record PurchaseDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string? InvoiceNumber,
    DateOnly PurchaseDate,
    decimal TotalAmount,
    int ItemCount,
    string UserName,
    DateTime CreatedAt);

/// <summary>A compra completa, com os itens. Resposta do GET /api/purchases/{id} e do POST.</summary>
public record PurchaseDetailsDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string? InvoiceNumber,
    DateOnly PurchaseDate,
    decimal TotalAmount,
    string UserName,
    DateTime CreatedAt,
    IReadOnlyList<PurchaseItemDto> Items);

public record PurchaseItemDto(
    int Id,
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    decimal Total,
    int? BatchId,
    string? BatchNumber,
    DateOnly? ManufacturingDate,
    DateOnly? ExpirationDate);

/// <summary>Filtros do GET /api/purchases. Datas no formato 2026-10-01.</summary>
public class PurchaseQuery : PagedQuery
{
    public int? SupplierId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
