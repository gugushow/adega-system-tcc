namespace AdegaSystem.Api.Entities;

/// <summary>Cliente da adega. É opcional na venda: a maioria das vendas de balcão não tem cliente.</summary>
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Gravado só com os 11 dígitos, sem máscara. Único quando preenchido.</summary>
    public string? Cpf { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Data de nascimento (sem hora). Útil no futuro para promoções de aniversário.</summary>
    public DateOnly? BirthDate { get; set; }

    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Sale> Sales { get; set; } = [];
}
