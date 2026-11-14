using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Combos;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Combos. O vendedor consulta (para vender no PDV); cadastrar, editar e desativar é do ADMIN.</summary>
[ApiController]
[Route("api/combos")]
[Authorize(Roles = Roles.AdminOrVendedor)]
public class CombosController(IComboService comboService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search (nome) e active.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ComboDto>>>> GetAll([FromQuery] ComboQuery query) =>
        Ok(ApiResponse<PagedResult<ComboDto>>.Ok(await comboService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ComboDto>>> GetById(int id) =>
        Ok(ApiResponse<ComboDto>.Ok(await comboService.GetByIdAsync(id)));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ComboDto>>> Create(CreateComboDto dto)
    {
        var combo = await comboService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = combo.Id }, ApiResponse<ComboDto>.Ok(combo, "Combo cadastrado com sucesso."));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ComboDto>>> Update(int id, UpdateComboDto dto) =>
        Ok(ApiResponse<ComboDto>.Ok(await comboService.UpdateAsync(id, dto), "Combo atualizado com sucesso."));

    /// <summary>Não apaga do banco: desativa o combo (soft delete).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await comboService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Combo desativado com sucesso."));
    }
}
