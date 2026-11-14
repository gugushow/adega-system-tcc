using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Cadastro de usuários (só ADMIN). Duas proteções contra "trancar" o sistema:
/// ninguém desativa o próprio usuário, e sempre sobra pelo menos um ADMIN ativo.
/// </summary>
public class UserService(
    AdegaDbContext context,
    IPasswordHasher<User> passwordHasher,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IValidator<CreateUserDto> createValidator,
    IValidator<UpdateUserDto> updateValidator) : IUserService
{
    public async Task<PagedResult<UserDto>> GetAllAsync(UserQuery query)
    {
        var users = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            users = users.Where(u => u.Name.ToLower().Contains(search) || u.Email.Contains(search));
        }

        if (query.Role.HasValue)
        {
            users = users.Where(u => u.Role == query.Role.Value);
        }

        if (query.Active.HasValue)
        {
            users = users.Where(u => u.Active == query.Active.Value);
        }

        var totalItems = await users.CountAsync();
        var page = await users
            .OrderBy(u => u.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<UserDto>(page.Select(u => u.ToDto()).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<UserDto> GetByIdAsync(int id) => (await FindAsync(id)).ToDto();

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);

        var email = NormalizeEmail(dto.Email);
        await EnsureEmailIsAvailableAsync(email, ignoreId: null);

        var user = new User { Name = dto.Name.Trim(), Email = email, Role = dto.Role };
        // A senha nunca é gravada: só o hash (PBKDF2 com salt aleatório, do ASP.NET Identity).
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        // Dois SaveChanges (a auditoria precisa do Id gerado), mas na mesma transação:
        // ou o usuário e o registro de auditoria são gravados juntos, ou nenhum dos dois.
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        auditService.Add(AuditAction.USER_CREATED, nameof(User), user.Id, user.Name, newValue: Snapshot(user));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto dto)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var user = await FindAsync(id);
        var email = NormalizeEmail(dto.Email);
        await EnsureEmailIsAvailableAsync(email, ignoreId: id);

        if (!dto.Active && user.Active)
        {
            EnsureIsNotCurrentUser(user);
        }

        // Deixar de ser ADMIN ativo (mudando o perfil ou desativando) não pode zerar os administradores.
        var losesAdmin = user.Role == UserRole.ADMIN && user.Active && (dto.Role != UserRole.ADMIN || !dto.Active);
        if (losesAdmin)
        {
            await EnsureAnotherActiveAdminExistsAsync(user.Id);
        }

        var before = Snapshot(user);

        user.Name = dto.Name.Trim();
        user.Email = email;
        user.Role = dto.Role;
        user.Active = dto.Active;
        var passwordReset = !string.IsNullOrEmpty(dto.NewPassword);
        if (passwordReset)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword!);
        }

        // A senha nova nunca vai para a auditoria: só a informação de que foi redefinida.
        // Salvar sem mudar nada não gera registro (tipos anônimos comparam campo a campo no Equals).
        var after = Snapshot(user);
        if (passwordReset)
        {
            auditService.Add(AuditAction.USER_UPDATED, nameof(User), user.Id, user.Name, before,
                new { user.Name, user.Email, Role = user.Role.ToString(), user.Active, passwordReset = true });
        }
        else if (!after.Equals(before))
        {
            auditService.Add(AuditAction.USER_UPDATED, nameof(User), user.Id, user.Name, before, after);
        }

        await context.SaveChangesAsync();
        return user.ToDto();
    }

    public async Task DeactivateAsync(int id)
    {
        var user = await FindAsync(id);
        if (!user.Active)
        {
            return;
        }

        EnsureIsNotCurrentUser(user);
        if (user.Role == UserRole.ADMIN)
        {
            await EnsureAnotherActiveAdminExistsAsync(user.Id);
        }

        // Soft delete: compras, vendas e movimentações continuam apontando para ele.
        user.Active = false;
        auditService.Add(AuditAction.USER_DEACTIVATED, nameof(User), user.Id, user.Name, new { active = true }, new { active = false });
        await context.SaveChangesAsync();
    }

    /// <summary>Os campos do usuário que interessam à auditoria (nunca a senha nem o hash).</summary>
    private static object Snapshot(User user) => new { user.Name, user.Email, Role = user.Role.ToString(), user.Active };

    /// <summary>E-mail sempre em minúsculas: "Ana@Adega.com" e "ana@adega.com" são o mesmo login.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<User> FindAsync(int id) =>
        await context.Users.FindAsync(id)
        ?? throw new NotFoundException("Usuário não encontrado.");

    private void EnsureIsNotCurrentUser(User user)
    {
        if (user.Id == currentUser.UserId)
        {
            throw new BusinessException("Você não pode desativar o seu próprio usuário.");
        }
    }

    private async Task EnsureAnotherActiveAdminExistsAsync(int userId)
    {
        var hasAnother = await context.Users.AnyAsync(u => u.Id != userId && u.Role == UserRole.ADMIN && u.Active);
        if (!hasAnother)
        {
            throw new BusinessException("O sistema precisa de pelo menos um administrador ativo.");
        }
    }

    private async Task EnsureEmailIsAvailableAsync(string email, int? ignoreId)
    {
        var exists = await context.Users.AnyAsync(u => u.Email == email && (ignoreId == null || u.Id != ignoreId));
        if (exists)
        {
            throw new BusinessException("Já existe um usuário com esse e-mail.");
        }
    }
}
