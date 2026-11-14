using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Data.Seed;

/// <summary>Configurações iniciais, gravadas pela migration (HasData).</summary>
public static class SystemSettingSeed
{
    public const string MinimumMarginPercentKey = "MinimumMarginPercent";
    public const string LoyaltyEnabledKey = "LoyaltyEnabled";
    public const string LoyaltyAmountPerPointKey = "LoyaltyAmountPerPoint";

    public static readonly SystemSetting[] Settings =
    [
        // Margem mínima de 15%: o exemplo do enunciado. O ADMIN muda na tela de Configurações.
        new() { Id = 1, Key = MinimumMarginPercentKey, Value = "15" },

        // Fidelidade (Fase 7): ligada, com 1 ponto a cada R$ 10,00 gastos, o exemplo do enunciado.
        new() { Id = 2, Key = LoyaltyEnabledKey, Value = "true" },
        new() { Id = 3, Key = LoyaltyAmountPerPointKey, Value = "10" },
    ];
}
