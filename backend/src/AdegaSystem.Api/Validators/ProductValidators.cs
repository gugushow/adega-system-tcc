using AdegaSystem.Api.DTOs.Products;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>
/// Regras de formato dos campos de produto, escritas uma única vez e reaproveitadas
/// pelo cadastro e pela edição (as duas DTOs implementam IProductInput).
/// Regras que dependem do banco (código de barras repetido, categoria inativa)
/// ficam no ProductService, porque precisam consultar dados.
/// </summary>
public class ProductInputValidator : AbstractValidator<IProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
        RuleFor(x => x.Barcode).MaximumLength(50).WithMessage("O código de barras deve ter no máximo 50 caracteres.");
        RuleFor(x => x.Brand).MaximumLength(100).WithMessage("A marca deve ter no máximo 100 caracteres.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Selecione uma categoria.");
        RuleFor(x => x.Volume).MaximumLength(30).WithMessage("O volume deve ter no máximo 30 caracteres.");
        RuleFor(x => x.AlcoholContent).InclusiveBetween(0, 100)
            .When(x => x.AlcoholContent.HasValue)
            .WithMessage("O teor alcoólico deve estar entre 0 e 100%.");
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).WithMessage("O preço de custo não pode ser negativo.");
        RuleFor(x => x.SalePrice).GreaterThan(0).WithMessage("O preço de venda deve ser maior que zero.");
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
    }
}

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator() => Include(new ProductInputValidator());
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator() => Include(new ProductInputValidator());
}
