namespace AdegaSystem.Api.Entities;

/// <summary>
/// Uma linha do extrato de pontos do cliente. O saldo nunca fica guardado numa coluna:
/// ele é sempre a soma de Points de todas as linhas (como um extrato bancário).
/// Ganhos são positivos e resgates são negativos; ajustes podem ser dos dois sinais.
/// </summary>
public class LoyaltyTransaction
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Positivo = entrou no saldo; negativo = saiu.</summary>
    public int Points { get; set; }

    public LoyaltyType Type { get; set; }

    /// <summary>Venda que gerou os pontos (ou cujo cancelamento os estornou).</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    /// <summary>Explicação para o extrato: "Venda #12", "Troca por brinde", "Correção de cadastro".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Quem registrou (o vendedor da venda, quem fez o resgate ou o ajuste).</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
