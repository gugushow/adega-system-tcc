using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AdegaSystem.Api.Controllers;

/// <summary>
/// Login e dados do usuário logado. Por padrão toda a API exige login (FallbackPolicy no Program.cs);
/// só o login é liberado com [AllowAnonymous].
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IUserService userService) : ControllerBase
{
    /// <summary>Confere e-mail e senha e devolve o token JWT que o frontend envia nas próximas requisições.</summary>
    [AllowAnonymous]
    // Limite de tentativas por endereço (ver "login" no Program.cs): dificulta adivinhar senhas.
    [EnableRateLimiting(LoginRateLimit.PolicyName)]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login(LoginDto dto) =>
        Ok(ApiResponse<LoginResponseDto>.Ok(await authService.LoginAsync(dto)));

    /// <summary>Quem está logado (lido do banco, então reflete mudanças feitas pelo ADMIN).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me() =>
        Ok(ApiResponse<UserDto>.Ok(await authService.GetCurrentUserAsync()));

    /// <summary>O próprio usuário troca a senha, informando a senha atual.</summary>
    [HttpPut("me/password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordDto dto)
    {
        await authService.ChangePasswordAsync(dto);
        return Ok(ApiResponse.Ok("Senha alterada com sucesso."));
    }

    /// <summary>
    /// Cadastro de usuário. Não existe cadastro aberto: num sistema interno, só o ADMIN cria contas.
    /// Faz o mesmo que POST /api/users.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Register(CreateUserDto dto)
    {
        var user = await userService.CreateAsync(dto);
        return CreatedAtAction(nameof(UsersController.GetById), "Users", new { id = user.Id },
            ApiResponse<UserDto>.Ok(user, "Usuário cadastrado com sucesso."));
    }
}
