namespace AdegaSystem.Api.Entities;

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Preenchido quando o item veio de um combo: o combo é aberto em um item por produto.
    /// Nulo nas vendas de produto avulso.
    /// </summary>
    public int? ComboId { get; set; }
    public Combo? Combo { get; set; }

    public int Quantity { get; set; }

    /// <summary>Preço de venda do produto no momento da venda (copiado do cadastro).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Custo médio das unidades vendidas, ponderado pelos lotes usados.
    /// Ex.: 2 unidades de um lote a R$ 4,00 e 1 de um lote a R$ 5,00 → (8 + 5) / 3 = R$ 4,3333.
    /// Fica gravado: se o custo do produto mudar amanhã, o lucro desta venda não muda.
    /// </summary>
    public decimal CostPrice { get; set; }

    /// <summary>
    /// Desconto nesta linha, em reais. Nos itens de combo, é a economia do combo que coube a este produto
    /// (preço normal − parte do preço do combo).
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>Quantity × UnitPrice − Discount.</summary>
    public decimal Total { get; set; }

    /// <summary>De quais lotes saíram as unidades deste item (pode ser mais de um).</summary>
    public ICollection<SaleItemBatch> Batches { get; set; } = [];
}
