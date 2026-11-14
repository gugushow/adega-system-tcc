namespace AdegaSystem.Api.Auth;

/// <summary>
/// Configuração do token JWT (seção "Jwt"). Issuer, Audience e a duração ficam no appsettings.json;
/// o Secret NUNCA fica no repositório: vem dos User Secrets em desenvolvimento
/// (dotnet user-secrets set "Jwt:Secret" "...") ou da variável de ambiente Jwt__Secret em produção.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Tamanho mínimo da chave: o HMAC-SHA256 exige pelo menos 256 bits (32 bytes).</summary>
    public const int MinimumSecretLength = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>Validade do token. 480 minutos = um turno de 8 horas.</summary>
    public int ExpirationMinutes { get; set; } = 480;
}
