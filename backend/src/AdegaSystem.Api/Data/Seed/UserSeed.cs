using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Data.Seed;

/// <summary>
/// Usuário administrador criado pela migration: o primeiro login do sistema.
///
/// O PasswordHash fica vazio de propósito: nenhuma senha vai para o repositório.
/// A senha inicial vem da configuração "Admin:InitialPassword" (User Secrets ou variável
/// de ambiente) e é gravada como hash pelo AdminPasswordInitializer quando a API sobe.
/// </summary>
public static class UserSeed
{
    public const int AdminId = 1;

    private static readonly DateTime SeedDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly User Admin = new()
    {
        Id = AdminId,
        Name = "Administrador",
        Email = "admin@adega.local",
        PasswordHash = string.Empty,
        Role = UserRole.ADMIN,
        CreatedAt = SeedDate,
    };
}
