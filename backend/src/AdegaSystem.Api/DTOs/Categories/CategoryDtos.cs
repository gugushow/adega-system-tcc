using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Categories;

/// <summary>O que a API devolve sobre uma categoria.</summary>
public record CategoryDto(int Id, string Name, string? Description, bool Active, DateTime CreatedAt, int ProductCount);

/// <summary>Corpo do POST /api/categories.</summary>
public record CreateCategoryDto(string Name, string? Description);

/// <summary>Corpo do PUT /api/categories/{id}. Active permite reativar uma categoria.</summary>
public record UpdateCategoryDto(string Name, string? Description, bool Active);

/// <summary>Filtros do GET /api/categories.</summary>
public class CategoryQuery : PagedQuery
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
}
