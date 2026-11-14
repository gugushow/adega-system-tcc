using AdegaSystem.Api.DTOs.Users;

namespace AdegaSystem.Api.DTOs.Auth;

/// <summary>Corpo do POST /api/auth/login.</summary>
public record LoginDto(string Email, string Password);

/// <summary>
/// Resposta do login: o token, quando ele expira (UTC) e quem entrou.
/// O frontend guarda o token e o envia em toda requisição no cabeçalho "Authorization: Bearer ...".
/// </summary>
public record LoginResponseDto(string Token, DateTime ExpiresAt, UserDto User);

/// <summary>Corpo do PUT /api/auth/me/password: o próprio usuário troca a senha.</summary>
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
