namespace AdegaSystem.Api.Entities;

public class Supplier
{
    public int Id { get; set; }

    /// <summary>Nome fantasia, o nome usado no dia a dia.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Razão social.</summary>
    public string? CorporateName { get; set; }

    /// <summary>Gravado sem máscara e em maiúsculas (14 caracteres). Único quando preenchido.</summary>
    public string? Cnpj { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Purchase> Purchases { get; set; } = [];
}
