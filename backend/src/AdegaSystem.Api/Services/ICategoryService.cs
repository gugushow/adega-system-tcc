using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Categories;

namespace AdegaSystem.Api.Services;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(CategoryQuery query);
    Task<CategoryDto> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryDto dto);
    Task DeactivateAsync(int id);
}
