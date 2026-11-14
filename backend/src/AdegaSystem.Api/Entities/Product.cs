namespace AdegaSystem.Api.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Opcional (ex.: gelo vendido a granel). Quando informado, é único.</summary>
    public string? Barcode { get; set; }

    public string? Brand { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>Texto livre, como "350ml", "1L" ou "5kg".</summary>
    public string? Volume { get; set; }

    /// <summary>Teor alcoólico em %. Nulo para bebidas sem álcool.</summary>
    public decimal? AlcoholContent { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int MinimumStock { get; set; }

    /// <summary>
    /// Estoque atual. O setter é privado de propósito: nenhuma parte do sistema pode
    /// alterar o estoque diretamente. Toda entrada e saída passa pelo StockService,
    /// que atualiza produto, lote e movimentação na mesma transação.
    /// O EF Core consegue preencher o valor vindo do banco mesmo assim.
    /// </summary>
    public int CurrentStock { get; private set; }

    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Única forma de mudar o estoque. É internal (visível só dentro da API) e deve ser
    /// chamado apenas pelo StockService, que grava a movimentação junto.
    /// Mesmo assim, a entidade se protege: o estoque nunca fica negativo.
    /// </summary>
    internal void ApplyStockChange(int quantity)
    {
        var newStock = CurrentStock + quantity;
        if (newStock < 0)
        {
            throw new InvalidOperationException($"O estoque de '{Name}' ficaria negativo ({newStock}).");
        }

        CurrentStock = newStock;
    }

    // Propriedades calculadas: não são colunas no banco (só têm get).
    public bool IsLowStock => CurrentStock <= MinimumStock;
    public bool IsOutOfStock => CurrentStock == 0;
}
