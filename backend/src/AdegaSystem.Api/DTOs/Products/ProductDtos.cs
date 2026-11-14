using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Products;

/// <summary>
/// O que a API devolve sobre um produto. Inclui campos calculados
/// (nome da categoria, margem, estoque baixo) para a tela não precisar calcular.
/// </summary>
public record ProductDto(
    int Id,
    string Name,
    string? Description,
    string? Barcode,
    string? Brand,
    int CategoryId,
    string CategoryName,
    string? Volume,
    decimal? AlcoholContent,
    decimal CostPrice,
    decimal SalePrice,
    decimal MarginPercent,
    int MinimumStock,
    int CurrentStock,
    bool IsLowStock,
    bool IsOutOfStock,
    bool Active,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Campos que o usuário preenche no cadastro e na edição. Repare que não existe CurrentStock:
/// o estoque só muda por compra, venda ou ajuste, nunca por edição do cadastro.
/// </summary>
public interface IProductInput
{
    string Name { get; }
    string? Description { get; }
    string? Barcode { get; }
    string? Brand { get; }
    int CategoryId { get; }
    string? Volume { get; }
    decimal? AlcoholContent { get; }
    decimal CostPrice { get; }
    decimal SalePrice { get; }
    int MinimumStock { get; }
}

/// <summary>Corpo do POST /api/products.</summary>
public record CreateProductDto(
    string Name,
    string? Description,
    string? Barcode,
    string? Brand,
    int CategoryId,
    string? Volume,
    decimal? AlcoholContent,
    decimal CostPrice,
    decimal SalePrice,
    int MinimumStock) : IProductInput;

/// <summary>Corpo do PUT /api/products/{id}. Active permite reativar um produto.</summary>
public record UpdateProductDto(
    string Name,
    string? Description,
    string? Barcode,
    string? Brand,
    int CategoryId,
    string? Volume,
    decimal? AlcoholContent,
    decimal CostPrice,
    decimal SalePrice,
    int MinimumStock,
    bool Active) : IProductInput;

public enum StockFilter
{
    /// <summary>CurrentStock &lt;= MinimumStock (inclui os zerados).</summary>
    Low,

    /// <summary>CurrentStock = 0.</summary>
    Out,
}

/// <summary>Filtros do GET /api/products.</summary>
public class ProductQuery : PagedQuery
{
    /// <summary>Busca por nome, marca ou código de barras.</summary>
    public string? Search { get; set; }

    public int? CategoryId { get; set; }
    public bool? Active { get; set; }
    public StockFilter? Stock { get; set; }
}
