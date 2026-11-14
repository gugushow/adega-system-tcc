using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class CategoryService(
    AdegaDbContext context,
    IValidator<CreateCategoryDto> createValidator,
    IValidator<UpdateCategoryDto> updateValidator) : ICategoryService
{
    public async Task<PagedResult<CategoryDto>> GetAllAsync(CategoryQuery query)
    {
        var categories = context.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            categories = categories.Where(c => c.Name.ToLower().Contains(query.Search.Trim().ToLower()));
        }

        if (query.Active.HasValue)
        {
            categories = categories.Where(c => c.Active == query.Active.Value);
        }

        var totalItems = await categories.CountAsync();

        var page = await categories
            .OrderBy(c => c.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new { Category = c, ProductCount = c.Products.Count(p => p.Active) })
            .ToListAsync();

        var items = page.Select(x => x.Category.ToDto(x.ProductCount)).ToList();
        return new PagedResult<CategoryDto>(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<CategoryDto> GetByIdAsync(int id)
    {
        var result = await context.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { Category = c, ProductCount = c.Products.Count(p => p.Active) })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("Categoria não encontrada.");

        return result.Category.ToDto(result.ProductCount);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        // 1. Formato dos campos (FluentValidation). Se falhar, lança ValidationException → HTTP 400.
        await createValidator.ValidateAndThrowAsync(dto);

        // 2. Regras que dependem do banco.
        var name = dto.Name.Trim();
        await EnsureNameIsAvailableAsync(name, ignoreId: null);

        var category = new Category { Name = name, Description = Text.EmptyToNull(dto.Description) };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        return category.ToDto(productCount: 0);
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var category = await FindAsync(id);
        var name = dto.Name.Trim();
        await EnsureNameIsAvailableAsync(name, ignoreId: id);

        if (category.Active && !dto.Active)
        {
            await EnsureHasNoActiveProductsAsync(id);
        }

        category.Name = name;
        category.Description = Text.EmptyToNull(dto.Description);
        category.Active = dto.Active;
        await context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeactivateAsync(int id)
    {
        var category = await FindAsync(id);
        await EnsureHasNoActiveProductsAsync(id);

        // Soft delete: só marcamos como inativa. O registro continua no banco
        // para não quebrar o histórico de produtos, vendas e relatórios.
        category.Active = false;
        await context.SaveChangesAsync();
    }

    private async Task<Category> FindAsync(int id) =>
        await context.Categories.FindAsync(id)
        ?? throw new NotFoundException("Categoria não encontrada.");

    private async Task EnsureNameIsAvailableAsync(string name, int? ignoreId)
    {
        // ToLower dos dois lados: "cerveja" e "Cerveja" são a mesma categoria.
        var exists = await context.Categories.AnyAsync(c =>
            c.Name.ToLower() == name.ToLower() && (ignoreId == null || c.Id != ignoreId));

        if (exists)
        {
            throw new BusinessException("Já existe uma categoria com esse nome.");
        }
    }

    private async Task EnsureHasNoActiveProductsAsync(int categoryId)
    {
        if (await context.Products.AnyAsync(p => p.CategoryId == categoryId && p.Active))
        {
            throw new BusinessException("Não é possível desativar uma categoria que possui produtos ativos.");
        }
    }
}
