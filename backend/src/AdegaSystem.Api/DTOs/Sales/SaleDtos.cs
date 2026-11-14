using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Sales;

/// <summary>
/// Corpo do POST /api/sales. O preço não é enviado: a API usa o preço de venda do cadastro
/// (e o preço do combo, para os combos). Discount é o desconto na venda inteira, em reais.
/// A venda precisa ter pelo menos um produto avulso (Items) ou um combo (Combos).
/// </summary>
public record CreateSaleDto(
    int? CustomerId,
    PaymentMethod PaymentMethod,
    decimal Discount,
    IReadOnlyList<CreateSaleItemDto> Items,
    IReadOnlyList<CreateSaleComboDto>? Combos = null);

/// <summary>Um item: produto, quantidade e, se houver, desconto nesta linha (em reais).</summary>
public record CreateSaleItemDto(int ProductId, int Quantity, decimal Discount = 0);

/// <summary>Um combo vendido: qual e quantos. A API abre o combo em um item por produto.</summary>
public record CreateSaleComboDto(int ComboId, int Quantity);

/// <summary>Corpo do POST /api/sales/{id}/cancel.</summary>
public record CancelSaleDto(string Reason);

/// <summary>Uma linha da listagem de vendas (sem os itens).</summary>
public record SaleDto(
    int Id,
    DateTime SaleDate,
    int? CustomerId,
    string? CustomerName,
    string UserName,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    PaymentMethod PaymentMethod,
    SaleStatus Status);

/// <summary>
/// A venda completa. Resposta do GET /api/sales/{id}, do POST e do cancelamento.
/// LoyaltyPoints = pontos de fidelidade que a venda deu ao cliente (0 se nenhum).
/// </summary>
public record SaleDetailsDto(
    int Id,
    DateTime SaleDate,
    int? CustomerId,
    string? CustomerName,
    string UserName,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    decimal TotalCost,
    decimal Profit,
    PaymentMethod PaymentMethod,
    SaleStatus Status,
    DateTime? CanceledAt,
    string? CanceledByUserName,
    string? CancelReason,
    int LoyaltyPoints,
    IReadOnlyList<SaleItemDto> Items);

/// <summary>ComboId e ComboName vêm preenchidos quando o item faz parte de um combo.</summary>
public record SaleItemDto(
    int Id,
    int ProductId,
    string ProductName,
    int? ComboId,
    string? ComboName,
    int Quantity,
    decimal UnitPrice,
    decimal CostPrice,
    decimal Discount,
    decimal Total,
    IReadOnlyList<SaleItemBatchDto> Batches);

/// <summary>Quanto saiu de cada lote. É o que permite devolver ao lote certo no cancelamento.</summary>
public record SaleItemBatchDto(int BatchId, string BatchNumber, DateOnly? ExpirationDate, int Quantity, decimal UnitCost);

/// <summary>Filtros do GET /api/sales. Datas no horário de Brasília (ex.: 2026-10-01).</summary>
public class SaleQuery : PagedQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public SaleStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
}
