namespace AdegaSystem.Api.Entities;

/// <summary>
/// Compra de um fornecedor. É um documento: depois de gravada, nunca é editada nem apagada.
/// Gravar a compra também dá entrada no estoque (ver PurchaseService).
/// </summary>
public class Purchase
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    /// <summary>Número da nota fiscal. Opcional, mas único por fornecedor quando informado.</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Dia da compra (data da nota). Sem hora, por isso DateOnly.</summary>
    public DateOnly PurchaseDate { get; set; }

    /// <summary>Soma dos totais dos itens.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Quem registrou a compra.</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseItem> Items { get; set; } = [];
}
