namespace AdegaSystem.Api.Entities;

public class PurchaseItem
{
    public int Id { get; set; }

    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Quantidade em unidades (12 latas, não "1 caixa").</summary>
    public int Quantity { get; set; }

    /// <summary>Custo de uma unidade, com 4 casas (ex.: R$ 3,8975).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Quantity x UnitCost, arredondado em 2 casas.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Lote criado por este item. O número do lote e as datas ficam no próprio lote.
    /// Nulo só em compras feitas antes da Fase 3.
    /// </summary>
    public int? BatchId { get; set; }
    public ProductBatch? Batch { get; set; }
}
