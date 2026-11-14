namespace AdegaSystem.Api.Entities;

/// <summary>
/// Combo (promoção): vários produtos vendidos juntos por um preço fechado.
/// Ex.: "Combo Whisky" = 1 Jack Daniel's + 4 Red Bull + 1 Gelo por R$ 199,00 (separados custariam R$ 220,00).
///
/// O combo não tem estoque próprio: ao ser vendido, ele é "aberto" em um item de venda por produto
/// (com ComboId preenchido), e cada produto sai dos próprios lotes (FEFO), como numa venda comum.
/// </summary>
public class Combo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Preço do combo inteiro.</summary>
    public decimal SalePrice { get; set; }

    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ComboItem> Items { get; set; } = [];
}
