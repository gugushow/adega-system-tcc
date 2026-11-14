using AdegaSystem.Api.DTOs.Combos;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>Regras comuns ao cadastro e à edição. As que dependem do banco (produtos ativos, preço) ficam no ComboService.</summary>
public static class ComboRules
{
    public static void AddComboRules<T>(this AbstractValidator<T> validator) where T : IComboInput
    {
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do combo.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");
        validator.RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
        validator.RuleFor(x => x.SalePrice)
            .GreaterThan(0).WithMessage("O preço do combo deve ser maior que zero.")
            .PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("O preço aceita no máximo 2 casas decimais.");

        validator.RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione pelo menos um produto ao combo.");
        validator.RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .When(x => x.Items is { Count: > 0 })
            .WithMessage("O mesmo produto aparece mais de uma vez. Junte as quantidades numa linha só.");
        validator.RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Selecione o produto.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.")
                .LessThanOrEqualTo(1000).WithMessage("A quantidade deve ser de no máximo 1000.");
        });
    }
}

public class CreateComboDtoValidator : AbstractValidator<CreateComboDto>
{
    public CreateComboDtoValidator() => this.AddComboRules();
}

public class UpdateComboDtoValidator : AbstractValidator<UpdateComboDto>
{
    public UpdateComboDtoValidator() => this.AddComboRules();
}
