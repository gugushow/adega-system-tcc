using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>
/// Limite de quantidade por item (compra, venda, ajuste e pontos). É bem acima de qualquer uso real
/// de uma adega e existe só para barrar valores absurdos (ex.: 3 bilhões), que estourariam as contas
/// e virariam erro 500 em vez de uma mensagem clara.
/// </summary>
public static class QuantityRules
{
    public const int Max = 100_000;
    public const string MaxText = "100.000";

    public static IRuleBuilderOptions<T, int> WithinLimit<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(-Max, Max).WithMessage($"A quantidade deve ser de no máximo {MaxText}.");
}
