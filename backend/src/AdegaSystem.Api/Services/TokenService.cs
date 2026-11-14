using System.Security.Claims;
using System.Text;
using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Cria o token JWT. Um JWT tem três partes separadas por ponto: cabeçalho, conteúdo (claims)
/// e assinatura. O conteúdo é só Base64 (qualquer um consegue ler), por isso nunca colocamos
/// senha nem dado sensível nele. O que impede a falsificação é a assinatura, feita com o Secret.
/// </summary>
public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(AppClaims.Name, user.Name),
                new Claim(AppClaims.Email, user.Email),
                new Claim(AppClaims.Role, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
