using System.Threading.RateLimiting;
using AdegaSystem.Api.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace AdegaSystem.Api.Auth;

/// <summary>
/// Limite de tentativas de login por endereço IP: no máximo N por minuto (padrão 10, configurável em
/// "RateLimiting:LoginPermitLimit"). Quem passa disso recebe 429 e espera o minuto acabar.
/// Atrás de um proxy (nginx), o IP real vem do cabeçalho X-Forwarded-For; para isso a API precisa
/// de ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (já configurado no docker-compose.prod.yml).
/// </summary>
public static class LoginRateLimit
{
    public const string PolicyName = "login";
    public const string Message = "Muitas tentativas de login. Aguarde um minuto e tente novamente.";

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 10);

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Resposta no mesmo formato do resto da API.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
                new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(Message), cancellationToken));
        });
    }
}
