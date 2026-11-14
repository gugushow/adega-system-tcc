using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Products;

namespace AdegaSystem.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query);
    Task<ProductDto> GetByIdAsync(int id);
    Task<ProductDto> GetByBarcodeAsync(string barcode);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto);
    Task DeactivateAsync(int id);
}
