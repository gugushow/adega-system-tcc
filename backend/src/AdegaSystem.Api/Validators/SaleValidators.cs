using AdegaSystem.Api.DTOs.Sales;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>
/// Regras que dá para conferir só olhando o pedido. As que dependem do banco
/// (estoque, preço, desconto maior que o total) ficam no SaleService.
/// </summary>
public class CreateSaleDtoValidator : AbstractValidator<CreateSaleDto>
{
    public CreateSaleDtoValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId.HasValue).WithMessage("Cliente inválido.");
        RuleFor(x => x.PaymentMethod).IsInEnum().WithMessage("Selecione a forma de pagamento.");
        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.")
            .PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("O desconto aceita no máximo 2 casas decimais.");

        RuleFor(x => x.Items)
            .Must((sale, items) => items is { Count: > 0 } || sale.Combos is { Count: > 0 })
            .WithMessage("Adicione pelo menos um produto à venda.");

        // No PDV, adicionar o mesmo produto de novo aumenta a quantidade da linha.
        // A API exige uma linha por produto, o que deixa a escolha dos lotes (FEFO) simples.
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .When(x => x.Items is { Count: > 0 })
            .WithMessage("O mesmo produto aparece mais de uma vez. Junte as quantidades numa linha só.");

        RuleForEach(x => x.Items).SetValidator(new CreateSaleItemDtoValidator());

        // Mesmo raciocínio: um combo repetido vira uma linha com quantidade maior.
        RuleFor(x => x.Combos)
            .Must(combos => combos!.Select(c => c.ComboId).Distinct().Count() == combos!.Count)
            .When(x => x.Combos is { Count: > 0 })
            .WithMessage("O mesmo combo aparece mais de uma vez. Junte as quantidades numa linha só.");

        RuleForEach(x => x.Combos).ChildRules(combo =>
        {
            combo.RuleFor(c => c.ComboId).GreaterThan(0).WithMessage("Selecione o combo.");
            combo.RuleFor(c => c.Quantity).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.").WithinLimit();
        });
    }
}

public class CreateSaleItemDtoValidator : AbstractValidator<CreateSaleItemDto>
{
    public CreateSaleItemDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Selecione o produto.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.").WithinLimit();
        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.")
            .PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("O desconto aceita no máximo 2 casas decimais.");
    }
}

public class CancelSaleDtoValidator : AbstractValidator<CancelSaleDto>
{
    public CancelSaleDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(255).WithMessage("O motivo deve ter no máximo 255 caracteres.");
    }
}
