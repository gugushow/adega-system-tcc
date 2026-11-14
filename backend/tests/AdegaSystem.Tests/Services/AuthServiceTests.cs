using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AdegaSystem.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public static readonly JwtOptions Jwt = new()
    {
        Issuer = "AdegaSystem",
        Audience = "AdegaSystem.Frontend",
        Secret = "chave-de-teste-com-mais-de-32-caracteres!",
        ExpirationMinutes = 60,
    };

    private AuthService CreateService(int currentUserId = TestData.AdminUserId)
    {
        var context = _database.CreateContext();
        return new(context, new PasswordHasher<User>(), new TokenService(Options.Create(Jwt)),
            new TestData.FakeCurrentUser(currentUserId), TestData.CreateAuditService(context, currentUserId),
            new LoginDtoValidator(), new ChangePasswordDtoValidator());
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokenWithUserAndRole()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR, name: "Ana Lima");

        var result = await CreateService().LoginAsync(new LoginDto("ana@adega.com", TestData.DefaultPassword));

        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(UserRole.VENDEDOR, result.User.Role);
        Assert.InRange(result.ExpiresAt, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));

        // O conteúdo do token pode ser lido por qualquer um (não é criptografado, é assinado).
        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);
        Assert.Equal(user.Id.ToString(), token.GetClaim(AppClaims.UserId).Value);
        Assert.Equal("VENDEDOR", token.GetClaim(AppClaims.Role).Value);
        Assert.Equal("Ana Lima", token.GetClaim(AppClaims.Name).Value);
        Assert.Equal("AdegaSystem", token.Issuer);
    }

    [Fact]
    public async Task Login_EmailIgnoresCaseAndSpaces()
    {
        await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var result = await CreateService().LoginAsync(new LoginDto("  ANA@Adega.com ", TestData.DefaultPassword));

        Assert.Equal("ana@adega.com", result.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsUnauthorizedWithGenericMessage()
    {
        await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginDto("ana@adega.com", "SenhaErrada1")));

        Assert.Equal("E-mail ou senha inválidos.", exception.Message);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsTheSameMessage()
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginDto("ninguem@adega.com", TestData.DefaultPassword)));

        Assert.Equal("E-mail ou senha inválidos.", exception.Message);
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsUnauthorized()
    {
        await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR, active: false);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginDto("ana@adega.com", TestData.DefaultPassword)));
    }

    [Fact]
    public async Task Login_SeededAdminWithoutPassword_ThrowsUnauthorized()
    {
        // O admin da migration começa sem senha até "Admin:InitialPassword" ser configurada.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginDto("admin@adega.local", "qualquer1")));
    }

    [Fact]
    public async Task Login_WithEmptyFields_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateService().LoginAsync(new LoginDto("", "")));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "Informe o e-mail.");
        Assert.Contains(exception.Errors, e => e.ErrorMessage == "Informe a senha.");
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ThrowsBusinessException()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService(user.Id).ChangePasswordAsync(new ChangePasswordDto("SenhaErrada1", "NovaSenha9")));

        Assert.Equal("A senha atual está incorreta.", exception.Message);
    }

    [Fact]
    public async Task ChangePassword_Success_OnlyTheNewPasswordWorks()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        await CreateService(user.Id).ChangePasswordAsync(new ChangePasswordDto(TestData.DefaultPassword, "NovaSenha9"));

        await CreateService().LoginAsync(new LoginDto("ana@adega.com", "NovaSenha9"));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginDto("ana@adega.com", TestData.DefaultPassword)));
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_ThrowsValidationException()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(user.Id).ChangePasswordAsync(new ChangePasswordDto(TestData.DefaultPassword, TestData.DefaultPassword)));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "A nova senha precisa ser diferente da atual.");
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsTheLoggedUser()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.ESTOQUISTA, name: "Ana Lima");

        var current = await CreateService(user.Id).GetCurrentUserAsync();

        Assert.Equal("Ana Lima", current.Name);
        Assert.Equal(UserRole.ESTOQUISTA, current.Role);
    }

    public void Dispose() => _database.Dispose();
}
