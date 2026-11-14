using AdegaSystem.Api.DTOs.Loyalty;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class RedeemPointsDtoValidator : AbstractValidator<RedeemPointsDto>
{
    public RedeemPointsDtoValidator()
    {
        RuleFor(x => x.Points).GreaterThan(0).WithMessage("Informe quantos pontos serão resgatados.")
            .LessThanOrEqualTo(QuantityRules.Max).WithMessage($"O resgate deve ser de no máximo {QuantityRules.MaxText} pontos.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Informe pelo que os pontos foram trocados.")
            .MaximumLength(255).WithMessage("A descrição deve ter no máximo 255 caracteres.");
    }
}

public class AdjustPointsDtoValidator : AbstractValidator<AdjustPointsDto>
{
    public AdjustPointsDtoValidator()
    {
        RuleFor(x => x.Points).NotEqual(0).WithMessage("Informe os pontos do ajuste (positivo soma, negativo tira).")
            .InclusiveBetween(-QuantityRules.Max, QuantityRules.Max).WithMessage($"O ajuste deve ser de no máximo {QuantityRules.MaxText} pontos.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Informe o motivo do ajuste.")
            .MaximumLength(255).WithMessage("O motivo deve ter no máximo 255 caracteres.");
    }
}
