using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Mappings;

public static class ProductMappings
{
    /// <summary>Exige que product.Category tenha sido carregada (Include).</summary>
    public static ProductDto ToDto(this Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Barcode,
        product.Brand,
        product.CategoryId,
        product.Category.Name,
        product.Volume,
        product.AlcoholContent,
        product.CostPrice,
        product.SalePrice,
        CalculateMarginPercent(product.CostPrice, product.SalePrice),
        product.MinimumStock,
        product.CurrentStock,
        product.IsLowStock,
        product.IsOutOfStock,
        product.Active,
        product.CreatedAt,
        product.UpdatedAt);

    /// <summary>Margem (%) = ((SalePrice - CostPrice) / SalePrice) * 100, com 2 casas.</summary>
    public static decimal CalculateMarginPercent(decimal costPrice, decimal salePrice) =>
        salePrice == 0 ? 0 : Math.Round((salePrice - costPrice) / salePrice * 100, 2);
}
