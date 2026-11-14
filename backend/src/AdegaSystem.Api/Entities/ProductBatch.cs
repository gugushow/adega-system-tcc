namespace AdegaSystem.Api.Entities;

/// <summary>
/// Um lote de um produto: as unidades que chegaram juntas, com a mesma validade e o mesmo custo.
/// Ex.: Heineken 350ml, lote A, 30 unidades, validade 10/11/2026.
///
/// Regra central: Product.CurrentStock é sempre a soma de QuantityAvailable dos lotes do produto.
/// Por isso QuantityAvailable também tem setter privado e só o StockService o altera,
/// junto com o estoque do produto e a movimentação.
/// </summary>
public class ProductBatch
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Compra que trouxe o lote. Nulo no lote de saldo inicial criado pela migration da Fase 3.</summary>
    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    /// <summary>Código do lote impresso na embalagem. Quando a nota não informa, é gerado ("COMPRA-15").</summary>
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Quantas unidades entraram. Não muda depois.</summary>
    public int QuantityReceived { get; set; }

    /// <summary>Quantas unidades ainda restam neste lote.</summary>
    public int QuantityAvailable { get; private set; }

    public DateOnly? ManufacturingDate { get; set; }

    /// <summary>Nulo para produtos sem validade (gelo, por exemplo). Esses lotes nunca aparecem como vencidos.</summary>
    public DateOnly? ExpirationDate { get; set; }

    /// <summary>Custo unitário pago por este lote. É o custo usado no lucro das vendas que saem dele.</summary>
    public decimal UnitCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Única forma de mudar o saldo do lote. Chamado só pelo StockService.</summary>
    internal void ApplyQuantityChange(int quantity)
    {
        var newQuantity = QuantityAvailable + quantity;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException($"O saldo do lote '{BatchNumber}' ficaria negativo ({newQuantity}).");
        }

        QuantityAvailable = newQuantity;
    }
}
