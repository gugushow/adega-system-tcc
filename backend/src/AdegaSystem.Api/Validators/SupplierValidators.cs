using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Suppliers;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class SupplierInputValidator : AbstractValidator<ISupplierInput>
{
    public SupplierInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");
        RuleFor(x => x.CorporateName).MaximumLength(200).WithMessage("A razão social deve ter no máximo 200 caracteres.");

        // CNPJ é opcional; quando preenchido, precisa ter dígitos verificadores corretos.
        RuleFor(x => x.Cnpj).Must(Cnpj.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Cnpj))
            .WithMessage("CNPJ inválido.");

        RuleFor(x => x.Phone).MaximumLength(20).WithMessage("O telefone deve ter no máximo 20 caracteres.");
        RuleFor(x => x.Email).MaximumLength(150).WithMessage("O e-mail deve ter no máximo 150 caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.");
        RuleFor(x => x.Address).MaximumLength(300).WithMessage("O endereço deve ter no máximo 300 caracteres.");
    }
}

public class CreateSupplierDtoValidator : AbstractValidator<CreateSupplierDto>
{
    public CreateSupplierDtoValidator() => Include(new SupplierInputValidator());
}

public class UpdateSupplierDtoValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierDtoValidator() => Include(new SupplierInputValidator());
}
