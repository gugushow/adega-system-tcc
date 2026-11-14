using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Services;

public interface ITokenService
{
    /// <summary>Gera o JWT do usuário e diz quando ele expira (UTC).</summary>
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}
