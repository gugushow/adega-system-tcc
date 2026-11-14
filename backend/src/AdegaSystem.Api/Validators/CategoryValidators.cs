using AdegaSystem.Api.DTOs.Categories;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}

public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}
