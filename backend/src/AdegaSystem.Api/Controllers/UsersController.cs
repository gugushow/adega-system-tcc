using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Gestão de usuários: só o ADMIN.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search (nome ou e-mail), role e active.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetAll([FromQuery] UserQuery query) =>
        Ok(ApiResponse<PagedResult<UserDto>>.Ok(await userService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(int id) =>
        Ok(ApiResponse<UserDto>.Ok(await userService.GetByIdAsync(id)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserDto dto)
    {
        var user = await userService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            ApiResponse<UserDto>.Ok(user, "Usuário cadastrado com sucesso."));
    }

    /// <summary>Atualiza dados, perfil e status. newPassword é opcional: vazio mantém a senha atual.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(int id, UpdateUserDto dto) =>
        Ok(ApiResponse<UserDto>.Ok(await userService.UpdateAsync(id, dto), "Usuário atualizado com sucesso."));

    /// <summary>Não apaga do banco: desativa o usuário, que perde o acesso na hora.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await userService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Usuário desativado com sucesso."));
    }
}
