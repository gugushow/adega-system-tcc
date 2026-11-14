using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>
/// Controller "magro": só traduz HTTP ↔ service. Não tem regra de negócio nem try/catch;
/// erros viram exceções tratadas pelo GlobalExceptionHandler.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CategoryDto>>>> GetAll([FromQuery] CategoryQuery query) =>
        Ok(ApiResponse<PagedResult<CategoryDto>>.Ok(await categoryService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetById(int id) =>
        Ok(ApiResponse<CategoryDto>.Ok(await categoryService.GetByIdAsync(id)));

    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(CreateCategoryDto dto)
    {
        var category = await categoryService.CreateAsync(dto);

        // 201 Created + cabeçalho Location apontando para GET /api/categories/{id}.
        return CreatedAtAction(nameof(GetById), new { id = category.Id },
            ApiResponse<CategoryDto>.Ok(category, "Categoria cadastrada com sucesso."));
    }

    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(int id, UpdateCategoryDto dto) =>
        Ok(ApiResponse<CategoryDto>.Ok(await categoryService.UpdateAsync(id, dto), "Categoria atualizada com sucesso."));

    /// <summary>Não apaga do banco: desativa a categoria (soft delete).</summary>
    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await categoryService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Categoria desativada com sucesso."));
    }
}
