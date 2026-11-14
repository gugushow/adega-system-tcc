using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class AuthService(
    AdegaDbContext context,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IValidator<LoginDto> loginValidator,
    IValidator<ChangePasswordDto> changePasswordValidator) : IAuthService
{
    // A mesma mensagem para e-mail inexistente, senha errada e usuário inativo:
    // assim ninguém descobre quais e-mails estão cadastrados tentando logar.
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        await loginValidator.ValidateAndThrowAsync(dto);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        // PasswordHash vazio: o admin semeado antes de ter a senha inicial configurada.
        if (user is null || !user.Active || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        // O hash guarda a versão do algoritmo. Se o .NET passar a recomendar um algoritmo mais forte,
        // aproveitamos o momento em que temos a senha em mãos para gravar o hash novo.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
            await context.SaveChangesAsync();
        }

        var (token, expiresAt) = tokenService.CreateToken(user);
        return new LoginResponseDto(token, expiresAt, user.ToDto());
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId)
            ?? throw new NotFoundException("Usuário não encontrado.");
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(ChangePasswordDto dto)
    {
        await changePasswordValidator.ValidateAndThrowAsync(dto);

        var user = await context.Users.FindAsync(currentUser.UserId)
            ?? throw new NotFoundException("Usuário não encontrado.");

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            throw new BusinessException("A senha atual está incorreta.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
        // Só o fato fica registrado: senha (nem o hash) nunca vai para a auditoria.
        auditService.Add(AuditAction.USER_PASSWORD_CHANGED, nameof(User), user.Id, user.Name);
        await context.SaveChangesAsync();
    }
}
