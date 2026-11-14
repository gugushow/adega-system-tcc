using System.Text;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdegaSystem.Api.Auth;

/// <summary>
/// Toda a configuração de login (JWT) e permissões num lugar só, para o Program.cs continuar curto.
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Lê a seção "Jwt" e confere o Secret logo que a API sobe (ValidateOnStart).
        // Sem isso, a API subiria normalmente e só falharia no primeiro login.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(jwt => jwt.Secret.Length >= JwtOptions.MinimumSecretLength,
                $"Jwt:Secret não configurado ou com menos de {JwtOptions.MinimumSecretLength} caracteres. " +
                "Em desenvolvimento, rode na raiz do projeto: dotnet user-secrets set \"Jwt:Secret\" " +
                "\"UMA_CHAVE_ALEATORIA_COM_PELO_MENOS_32_CARACTERES\" --project backend/src/AdegaSystem.Api")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // As opções do JwtBearer são montadas a partir do JwtOptions já validado.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Mantém os nomes curtos das claims ("sub", "role") em vez de convertê-los
                // para os nomes longos antigos da Microsoft.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    // Por padrão o .NET aceita o token até 5 minutos depois de vencido. Aqui vence na hora.
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = CheckUserIsStillValidAsync,
                    // Sem token, token inválido ou vencido: 401 no formato padrão da API.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(
                            ApiResponse.Fail("Sua sessão expirou ou você não fez login. Entre novamente."));
                    },
                    // Logado, mas o perfil não tem permissão: 403.
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(
                            ApiResponse.Fail("Você não tem permissão para acessar este recurso."));
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            // "Fechado por padrão": todo endpoint exige login, a não ser que tenha [AllowAnonymous].
            // Assim um controller novo nunca fica aberto por esquecimento.
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        return services;
    }

    /// <summary>
    /// O token vale por horas. Se nesse meio tempo o ADMIN desativar o usuário ou mudar o perfil dele,
    /// o token antigo não pode continuar valendo. Por isso, a cada requisição, conferimos no banco
    /// se o usuário ainda está ativo e com o mesmo perfil que está no token.
    /// </summary>
    private static async Task CheckUserIsStillValidAsync(TokenValidatedContext context)
    {
        var userIdClaim = context.Principal?.FindFirst(AppClaims.UserId)?.Value;
        var roleClaim = context.Principal?.FindFirst(AppClaims.Role)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            context.Fail("Token sem usuário.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AdegaDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Active, u.Role })
            .FirstOrDefaultAsync();

        if (user is null || !user.Active || user.Role.ToString() != roleClaim)
        {
            context.Fail("Usuário desativado ou com perfil alterado.");
        }
    }
}
