using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Combos;

/// <summary>
/// Um combo com os produtos e as contas que interessam ao vendedor.
/// NormalPrice = quanto custariam os produtos separados (preços de venda atuais);
/// Savings = NormalPrice − SalePrice.
/// AvailableQuantity = quantos combos dá para montar com o estoque atual (o produto mais escasso manda).
/// </summary>
public record ComboDto(
    int Id,
    string Name,
    string? Description,
    decimal SalePrice,
    decimal NormalPrice,
    decimal Savings,
    decimal SavingsPercent,
    int AvailableQuantity,
    bool Active,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ComboItemDto> Items);

/// <param name="UnitPrice">Preço de venda atual do produto (o avulso).</param>
public record ComboItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice, int CurrentStock, bool ProductActive);

/// <summary>Campos comuns ao cadastro e à edição.</summary>
public interface IComboInput
{
    string Name { get; }
    string? Description { get; }
    decimal SalePrice { get; }
    IReadOnlyList<ComboItemInputDto> Items { get; }
}

public record ComboItemInputDto(int ProductId, int Quantity);

/// <summary>Corpo do POST /api/combos.</summary>
public record CreateComboDto(string Name, string? Description, decimal SalePrice, IReadOnlyList<ComboItemInputDto> Items) : IComboInput;

/// <summary>Corpo do PUT /api/combos/{id}. Os itens enviados substituem os atuais.</summary>
public record UpdateComboDto(string Name, string? Description, decimal SalePrice, bool Active, IReadOnlyList<ComboItemInputDto> Items)
    : IComboInput;

/// <summary>Filtros do GET /api/combos.</summary>
public class ComboQuery : PagedQuery
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
}
