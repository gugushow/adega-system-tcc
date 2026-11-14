using AdegaSystem.Api.Data;
using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Define a senha do admin semeado na primeira vez que a API sobe com a configuração
/// "Admin:InitialPassword" (User Secrets ou variável de ambiente Admin__InitialPassword).
/// Assim nenhuma senha vai para o repositório nem para a migration.
/// Só age enquanto o admin não tem senha; depois disso, a senha é trocada pela tela.
/// </summary>
public static class AdminPasswordInitializer
{
    public const string ConfigurationKey = "Admin:InitialPassword";

    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Id == UserSeed.AdminId);
        if (admin is null || !string.IsNullOrEmpty(admin.PasswordHash))
        {
            return;
        }

        var password = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "O admin ({Email}) ainda não tem senha e não consegue entrar. Defina a senha inicial com: " +
                "dotnet user-secrets set \"{Key}\" \"SUA_SENHA\" --project backend/src/AdegaSystem.Api e reinicie a API.",
                admin.Email, ConfigurationKey);
            return;
        }

        // A senha inicial segue a mesma regra das outras senhas. Se for fraca, não grava:
        // o admin continua sem senha até a configuração ser corrigida.
        if (!PasswordRules.IsStrong(password))
        {
            logger.LogWarning(
                "A senha inicial do admin em \"{Key}\" é fraca e não foi gravada. Use pelo menos {Min} caracteres, " +
                "com letras e números, e reinicie a API.",
                ConfigurationKey, PasswordRules.MinimumLength);
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        admin.PasswordHash = hasher.HashPassword(admin, password);
        await context.SaveChangesAsync();
        logger.LogInformation("Senha inicial do admin ({Email}) definida a partir da configuração.", admin.Email);
    }
}
