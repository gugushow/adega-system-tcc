using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;

namespace AdegaSystem.Api.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto);

    /// <summary>O usuário logado, lido do banco (dados sempre atuais).</summary>
    Task<UserDto> GetCurrentUserAsync();

    Task ChangePasswordAsync(ChangePasswordDto dto);
}
