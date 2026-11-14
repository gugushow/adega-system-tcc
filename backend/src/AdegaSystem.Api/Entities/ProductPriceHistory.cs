namespace AdegaSystem.Api.Entities;

/// <summary>
/// Histórico do custo de compra: uma linha por produto em cada compra.
/// Permite ver a evolução do custo e comparar fornecedores (tela completa na Fase 7).
/// </summary>
public class ProductPriceHistory
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    /// <summary>Custo unitário pago nesta compra.</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Data da compra (não a data em que foi digitada).</summary>
    public DateOnly Date { get; set; }
}
