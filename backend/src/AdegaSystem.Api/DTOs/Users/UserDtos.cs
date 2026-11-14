using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Users;

/// <summary>Dados públicos do usuário. O PasswordHash nunca sai da API.</summary>
public record UserDto(int Id, string Name, string Email, UserRole Role, bool Active, DateTime CreatedAt);

/// <summary>Corpo do POST /api/users (e do POST /api/auth/register). A senha chega em texto e é guardada só como hash.</summary>
public record CreateUserDto(string Name, string Email, string Password, UserRole Role);

/// <summary>Corpo do PUT /api/users/{id}. NewPassword em branco mantém a senha atual.</summary>
public record UpdateUserDto(string Name, string Email, UserRole Role, bool Active, string? NewPassword = null);

/// <summary>Filtros do GET /api/users.</summary>
public class UserQuery : PagedQuery
{
    /// <summary>Busca por nome ou e-mail.</summary>
    public string? Search { get; set; }

    public UserRole? Role { get; set; }
    public bool? Active { get; set; }
}
