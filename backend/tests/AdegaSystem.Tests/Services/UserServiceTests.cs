using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>Por padrão quem está logado é o admin semeado (Id 1).</summary>
    private UserService CreateService(int currentUserId = TestData.AdminUserId)
    {
        var context = _database.CreateContext();
        return new(context, _hasher, new TestData.FakeCurrentUser(currentUserId), TestData.CreateAuditService(context, currentUserId),
            new CreateUserDtoValidator(), new UpdateUserDtoValidator());
    }

    private async Task<User> GetUserAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await context.Users.SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken);
    }

    private bool PasswordMatches(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    [Fact]
    public async Task Create_StoresOnlyThePasswordHash_AndNormalizesEmail()
    {
        var created = await CreateService().CreateAsync(new CreateUserDto("Ana Lima", "  Ana@Adega.com ", "Senha123", UserRole.VENDEDOR));

        Assert.Equal("ana@adega.com", created.Email);
        Assert.Equal(UserRole.VENDEDOR, created.Role);
        Assert.True(created.Active);

        var saved = await GetUserAsync(created.Id);
        Assert.NotEqual("Senha123", saved.PasswordHash);
        Assert.True(PasswordMatches(saved, "Senha123"));
    }

    [Fact]
    public async Task Create_WithEmailAlreadyUsed_IgnoringCase_ThrowsBusinessException()
    {
        await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().CreateAsync(new CreateUserDto("Outra Ana", "ANA@adega.com", "Senha123", UserRole.ESTOQUISTA)));

        Assert.Equal("Já existe um usuário com esse e-mail.", exception.Message);
    }

    [Theory]
    [InlineData("curta1", "A senha deve ter pelo menos 8 caracteres.")]
    [InlineData("somenteletras", "A senha precisa ter pelo menos uma letra e um número.")]
    [InlineData("12345678", "A senha precisa ter pelo menos uma letra e um número.")]
    public async Task Create_WithWeakPassword_ThrowsValidationException(string password, string message)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(new CreateUserDto("Ana Lima", "ana@adega.com", password, UserRole.VENDEDOR)));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Password" && e.ErrorMessage == message);
    }

    [Fact]
    public async Task Update_WithoutNewPassword_KeepsTheCurrentPassword()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var updated = await CreateService().UpdateAsync(user.Id,
            new UpdateUserDto("Ana Lima Souza", "ana@adega.com", UserRole.ESTOQUISTA, Active: true));

        Assert.Equal("Ana Lima Souza", updated.Name);
        Assert.Equal(UserRole.ESTOQUISTA, updated.Role);
        Assert.True(PasswordMatches(await GetUserAsync(user.Id), TestData.DefaultPassword));
    }

    [Fact]
    public async Task Update_WithNewPassword_ReplacesThePassword()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        await CreateService().UpdateAsync(user.Id,
            new UpdateUserDto("Ana Lima", "ana@adega.com", UserRole.VENDEDOR, Active: true, NewPassword: "NovaSenha9"));

        var saved = await GetUserAsync(user.Id);
        Assert.True(PasswordMatches(saved, "NovaSenha9"));
        Assert.False(PasswordMatches(saved, TestData.DefaultPassword));
    }

    [Fact]
    public async Task Deactivate_OwnUser_ThrowsBusinessException()
    {
        var admin = await TestData.AddUserAsync(_database, "chefe@adega.com", UserRole.ADMIN);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService(admin.Id).DeactivateAsync(admin.Id));

        Assert.Equal("Você não pode desativar o seu próprio usuário.", exception.Message);
    }

    [Fact]
    public async Task Update_DeactivatingOwnUser_ThrowsBusinessException()
    {
        // Há outro admin ativo (o do seed), então quem barra é a regra do próprio usuário.
        var admin = await TestData.AddUserAsync(_database, "chefe@adega.com", UserRole.ADMIN);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService(admin.Id).UpdateAsync(admin.Id,
            new UpdateUserDto("Usuário de Teste", "chefe@adega.com", UserRole.ADMIN, Active: false)));

        Assert.Equal("Você não pode desativar o seu próprio usuário.", exception.Message);
        Assert.True((await GetUserAsync(admin.Id)).Active);
    }

    [Fact]
    public async Task Deactivate_LastActiveAdmin_ThrowsBusinessException()
    {
        // Um estoquista "logado" só serve para não cair na regra do próprio usuário.
        var estoquista = await TestData.AddUserAsync(_database, "estoque@adega.com", UserRole.ESTOQUISTA);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService(estoquista.Id).DeactivateAsync(TestData.AdminUserId));

        Assert.Equal("O sistema precisa de pelo menos um administrador ativo.", exception.Message);
        Assert.True((await GetUserAsync(TestData.AdminUserId)).Active);
    }

    [Fact]
    public async Task Update_RemovingAdminRoleFromTheLastAdmin_ThrowsBusinessException()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().UpdateAsync(TestData.AdminUserId,
                new UpdateUserDto("Administrador", "admin@adega.local", UserRole.VENDEDOR, Active: true)));

        Assert.Equal("O sistema precisa de pelo menos um administrador ativo.", exception.Message);
    }

    [Fact]
    public async Task Deactivate_AnotherAdmin_WhenOneAdminRemains_Works()
    {
        var secondAdmin = await TestData.AddUserAsync(_database, "chefe@adega.com", UserRole.ADMIN);

        await CreateService().DeactivateAsync(secondAdmin.Id);

        Assert.False((await GetUserAsync(secondAdmin.Id)).Active);
    }

    [Fact]
    public async Task GetAll_FiltersByRoleAndSearch()
    {
        await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR, name: "Ana Lima");
        await TestData.AddUserAsync(_database, "bruno@adega.com", UserRole.VENDEDOR, name: "Bruno Costa");
        await TestData.AddUserAsync(_database, "carla@adega.com", UserRole.ESTOQUISTA, name: "Carla Dias");

        var vendedores = await CreateService().GetAllAsync(new UserQuery { Role = UserRole.VENDEDOR });
        var search = await CreateService().GetAllAsync(new UserQuery { Search = "carla" });

        Assert.Equal(["Ana Lima", "Bruno Costa"], vendedores.Items.Select(u => u.Name));
        Assert.Equal("carla@adega.com", Assert.Single(search.Items).Email);
    }

    [Fact]
    public async Task GetById_UnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetByIdAsync(999));
    }

    public void Dispose() => _database.Dispose();
}
