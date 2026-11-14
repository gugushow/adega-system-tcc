using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Purchases;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class CreatePurchaseDtoValidator : AbstractValidator<CreatePurchaseDto>
{
    public CreatePurchaseDtoValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("Selecione um fornecedor.");
        RuleFor(x => x.InvoiceNumber).MaximumLength(50).WithMessage("A nota fiscal deve ter no máximo 50 caracteres.");
        RuleFor(x => x.PurchaseDate)
            .NotEmpty().WithMessage("Informe a data da compra.")
            .LessThanOrEqualTo(_ => BusinessClock.Today).WithMessage("A data da compra não pode ser no futuro.");

        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione pelo menos um produto à compra.");

        // O mesmo produto pode vir em vários lotes, mas o mesmo lote não pode aparecer em duas linhas.
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => (i.ProductId, BatchKey(i.BatchNumber))).Distinct().Count() == items.Count)
            .When(x => x.Items is { Count: > 0 })
            .WithMessage("O mesmo lote do mesmo produto aparece mais de uma vez. Junte as quantidades numa linha só.");

        // Valida cada item. Os erros saem com o campo "Items[0].Quantity", e o frontend sabe qual linha marcar.
        RuleForEach(x => x.Items).SetValidator(new CreatePurchaseItemDtoValidator());
    }

    /// <summary>" a1 " e "A1" são o mesmo lote. Linhas sem número contam como o mesmo lote "em branco".</summary>
    private static string BatchKey(string? batchNumber) => batchNumber?.Trim().ToUpperInvariant() ?? string.Empty;
}

public class CreatePurchaseItemDtoValidator : AbstractValidator<CreatePurchaseItemDto>
{
    public CreatePurchaseItemDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Selecione o produto.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.").WithinLimit();
        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("O custo unitário não pode ser negativo.")
            // Mesma precisão da coluna (12,4): evita que o banco arredonde o valor sem avisar.
            .PrecisionScale(12, 4, ignoreTrailingZeros: true).WithMessage("O custo unitário aceita no máximo 4 casas decimais.");

        RuleFor(x => x.BatchNumber).MaximumLength(50).WithMessage("O número do lote deve ter no máximo 50 caracteres.");
        RuleFor(x => x.ExpirationDate)
            .GreaterThan(x => x.ManufacturingDate)
            .When(x => x.ExpirationDate.HasValue && x.ManufacturingDate.HasValue)
            .WithMessage("A validade precisa ser depois da fabricação.");
    }
}
