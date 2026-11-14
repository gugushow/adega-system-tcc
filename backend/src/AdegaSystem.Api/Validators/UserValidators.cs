using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>Regra de senha usada no cadastro, na edição e na troca de senha.</summary>
public static class PasswordRules
{
    public const int MinimumLength = 8;

    /// <summary>Mesma regra fora do FluentValidation (usada na senha inicial do admin).</summary>
    public static bool IsStrong(string? password) =>
        password is not null
        && password.Length is >= MinimumLength and <= 100
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);

    public static IRuleBuilderOptions<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MinimumLength(MinimumLength).WithMessage($"A senha deve ter pelo menos {MinimumLength} caracteres.")
            .MaximumLength(100).WithMessage("A senha deve ter no máximo 100 caracteres.")
            .Must(p => p is not null && p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("A senha precisa ter pelo menos uma letra e um número.");
}

public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(150).WithMessage("O e-mail deve ter no máximo 150 caracteres.")
            .EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("A senha é obrigatória.").StrongPassword();
        RuleFor(x => x.Role).IsInEnum().WithMessage("Selecione o perfil.");
    }
}

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(150).WithMessage("O e-mail deve ter no máximo 150 caracteres.")
            .EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Role).IsInEnum().WithMessage("Selecione o perfil.");
        // Em branco = mantém a senha atual.
        RuleFor(x => x.NewPassword).StrongPassword().When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha.");
    }
}

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("Informe a nova senha.").StrongPassword();
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("A nova senha precisa ser diferente da atual.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
