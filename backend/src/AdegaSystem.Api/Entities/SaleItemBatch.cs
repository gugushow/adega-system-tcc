namespace AdegaSystem.Api.Entities;

/// <summary>
/// Quanto de cada lote foi usado num item de venda. Ex.: venda de 5 Heineken, 3 do lote A
/// e 2 do lote B, gera duas linhas aqui. O enunciado não pede esta entidade, mas sem ela
/// o cancelamento não saberia para quais lotes devolver as unidades.
/// </summary>
public class SaleItemBatch
{
    public int Id { get; set; }

    public int SaleItemId { get; set; }
    public SaleItem SaleItem { get; set; } = null!;

    public int BatchId { get; set; }
    public ProductBatch Batch { get; set; } = null!;

    /// <summary>Unidades tiradas deste lote (sempre positivo).</summary>
    public int Quantity { get; set; }

    /// <summary>Custo unitário do lote, copiado no momento da venda.</summary>
    public decimal UnitCost { get; set; }
}
