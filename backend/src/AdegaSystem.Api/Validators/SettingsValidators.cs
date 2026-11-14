using AdegaSystem.Api.DTOs.Settings;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class MarginSettingsDtoValidator : AbstractValidator<MarginSettingsDto>
{
    public MarginSettingsDtoValidator()
    {
        // Margem de 100% significaria custo zero, então o limite é "menor que 100".
        RuleFor(x => x.MinimumMarginPercent)
            .GreaterThanOrEqualTo(0).WithMessage("A margem mínima não pode ser negativa.")
            .LessThan(100).WithMessage("A margem mínima deve ser menor que 100%.");
    }
}

public class LoyaltySettingsDtoValidator : AbstractValidator<LoyaltySettingsDto>
{
    public LoyaltySettingsDtoValidator()
    {
        RuleFor(x => x.AmountPerPoint)
            .GreaterThan(0).WithMessage("O valor por ponto deve ser maior que zero.")
            .LessThanOrEqualTo(10_000).WithMessage("O valor por ponto deve ser de no máximo R$ 10.000,00.")
            .PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("O valor por ponto aceita no máximo 2 casas decimais.");
    }
}
