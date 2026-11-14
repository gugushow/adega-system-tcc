using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Mappings;

/// <summary>Conversão entidade → DTO, escrita à mão para ficar explícito o que sai na API.</summary>
public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category, int productCount) =>
        new(category.Id, category.Name, category.Description, category.Active, category.CreatedAt, productCount);
}
