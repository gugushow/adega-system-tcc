namespace AdegaSystem.Api.Entities;

/// <summary>
/// Uma venda do PDV. É um documento: depois de gravada, nunca é editada nem apagada.
/// Para desfazer, cancela-se a venda, o que devolve as unidades aos lotes de origem.
///
/// Contas (todas feitas pela API, nunca recebidas do frontend):
///   Subtotal  = soma dos totais dos itens
///   Total     = Subtotal − Discount
///   TotalCost = soma de (quantidade × custo do lote) de cada lote usado
///   Profit    = Total − TotalCost
/// </summary>
public class Sale
{
    public int Id { get; set; }

    /// <summary>Cliente é opcional.</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Quem registrou a venda.</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Data e hora da venda, em UTC (como todas as datas com hora do sistema).</summary>
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }

    /// <summary>Desconto na venda inteira, em reais.</summary>
    public decimal Discount { get; set; }

    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.COMPLETED;

    /// <summary>Custo das mercadorias vendidas, pelo custo real de cada lote usado.</summary>
    public decimal TotalCost { get; set; }

    public decimal Profit { get; set; }

    // Preenchidos só no cancelamento.
    public DateTime? CanceledAt { get; set; }
    public int? CanceledByUserId { get; set; }
    public User? CanceledByUser { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];
}
