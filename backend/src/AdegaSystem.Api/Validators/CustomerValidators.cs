using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Customers;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class CustomerInputValidator : AbstractValidator<ICustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");

        // CPF é opcional; quando preenchido, precisa ter dígitos verificadores corretos.
        RuleFor(x => x.Cpf).Must(Cpf.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Cpf))
            .WithMessage("CPF inválido.");

        RuleFor(x => x.Phone).MaximumLength(20).WithMessage("O telefone deve ter no máximo 20 caracteres.");
        RuleFor(x => x.Email).MaximumLength(150).WithMessage("O e-mail deve ter no máximo 150 caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.");

        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(_ => BusinessClock.Today).WithMessage("A data de nascimento não pode ser no futuro.")
            .GreaterThan(new DateOnly(1900, 1, 1)).WithMessage("Data de nascimento inválida.")
            .When(x => x.BirthDate.HasValue);
    }
}

public class CreateCustomerDtoValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerDtoValidator() => Include(new CustomerInputValidator());
}

public class UpdateCustomerDtoValidator : AbstractValidator<UpdateCustomerDto>
{
    public UpdateCustomerDtoValidator() => Include(new CustomerInputValidator());
}
