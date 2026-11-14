using AdegaSystem.Api.Data;
using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdegaSystem.Tests.Services;

/// <summary>
/// O AdminPasswordInitializer roda na subida da API e recebe o IServiceProvider.
/// Aqui montamos um container pequeno, só com o banco (SQLite em memória) e o hasher de senha.
/// </summary>
public class AdminPasswordInitializerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly PasswordHasher<User> _hasher = new();

    public AdminPasswordInitializerTests()
    {
        // Uma conexão aberta e compartilhada: o banco em memória vive enquanto ela estiver aberta.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _services = new ServiceCollection()
            .AddDbContext<AdegaDbContext>(options => options.UseSqlite(_connection))
            .AddScoped<IPasswordHasher<User>, PasswordHasher<User>>()
            .BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdegaDbContext>().Database.EnsureCreated();
    }

    /// <summary>Configuração em memória, no lugar do User Secrets ou da variável de ambiente.</summary>
    private static IConfiguration Configuration(string? initialPassword)
    {
        var values = new Dictionary<string, string?>();
        if (initialPassword is not null)
        {
            values[AdminPasswordInitializer.ConfigurationKey] = initialPassword;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private Task RunAsync(string? initialPassword) =>
        AdminPasswordInitializer.RunAsync(_services, Configuration(initialPassword), NullLogger.Instance);

    private async Task<User> GetAdminAsync()
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();
        return await context.Users.AsNoTracking().SingleAsync(u => u.Id == UserSeed.AdminId, TestContext.Current.CancellationToken);
    }

    private async Task SetAdminHashAsync(string password)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();
        var admin = await context.Users.SingleAsync(u => u.Id == UserSeed.AdminId, TestContext.Current.CancellationToken);
        admin.PasswordHash = _hasher.HashPassword(admin, password);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private bool PasswordMatches(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    [Fact]
    public async Task Run_AdminWithoutPassword_SetsTheHashFromConfiguration()
    {
        // O admin do seed nasce sem senha.
        Assert.Equal(string.Empty, (await GetAdminAsync()).PasswordHash);

        await RunAsync("SenhaInicial1");

        var admin = await GetAdminAsync();
        Assert.NotEqual("SenhaInicial1", admin.PasswordHash); // só o hash é gravado
        Assert.True(PasswordMatches(admin, "SenhaInicial1"));
    }

    [Fact]
    public async Task Run_AdminWithPassword_NeverOverwritesTheHash()
    {
        // Depois da primeira senha, quem troca é a tela de usuários, não a configuração.
        await SetAdminHashAsync("SenhaAtual9");
        var hashBefore = (await GetAdminAsync()).PasswordHash;

        await RunAsync("OutraSenha1");

        var admin = await GetAdminAsync();
        Assert.Equal(hashBefore, admin.PasswordHash);
        Assert.True(PasswordMatches(admin, "SenhaAtual9"));
    }

    [Theory]
    [InlineData(null)] // configuração ausente
    [InlineData("   ")] // configuração em branco
    public async Task Run_WithoutConfiguration_DoesNothing(string? initialPassword)
    {
        await RunAsync(initialPassword);

        Assert.Equal(string.Empty, (await GetAdminAsync()).PasswordHash);
    }

    [Theory]
    [InlineData("abc")]           // curta demais
    [InlineData("somenteletras")] // sem número
    [InlineData("12345678")]      // sem letra
    public async Task Run_WithWeakPassword_LeavesAdminWithoutPassword(string initialPassword)
    {
        await RunAsync(initialPassword);

        Assert.Equal(string.Empty, (await GetAdminAsync()).PasswordHash);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
