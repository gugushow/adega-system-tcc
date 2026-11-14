namespace AdegaSystem.Api.Entities;

/// <summary>
/// Situação da venda. Uma venda nasce COMPLETED e só muda para CANCELED pelo cancelamento,
/// que devolve as unidades aos lotes. Vendas canceladas não entram no faturamento.
/// </summary>
public enum SaleStatus
{
    COMPLETED,
    CANCELED,
}
