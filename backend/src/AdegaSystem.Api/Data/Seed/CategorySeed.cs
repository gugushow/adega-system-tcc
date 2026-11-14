using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Data.Seed;

/// <summary>
/// Categorias iniciais. Elas entram no banco pela própria migration (HasData),
/// então todo ambiente novo já nasce com elas.
/// </summary>
public static class CategorySeed
{
    // Data fixa: com DateTime.UtcNow, cada nova migration acharia que o seed mudou.
    private static readonly DateTime SeedDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Category[] Categories =
    [
        new() { Id = 1, Name = "Cerveja", CreatedAt = SeedDate },
        new() { Id = 2, Name = "Vinho", CreatedAt = SeedDate },
        new() { Id = 3, Name = "Whisky", CreatedAt = SeedDate },
        new() { Id = 4, Name = "Gin", CreatedAt = SeedDate },
        new() { Id = 5, Name = "Vodka", CreatedAt = SeedDate },
        new() { Id = 6, Name = "Energético", CreatedAt = SeedDate },
        new() { Id = 7, Name = "Refrigerante", CreatedAt = SeedDate },
        new() { Id = 8, Name = "Água", CreatedAt = SeedDate },
        new() { Id = 9, Name = "Gelo", CreatedAt = SeedDate },
        new() { Id = 10, Name = "Outros", CreatedAt = SeedDate },
    ];
}
