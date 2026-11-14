using AdegaSystem.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdegaSystem.Tests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória (sem porta de rede) para testar o caminho HTTP completo:
/// token, [Authorize], roles, controllers e services. O MySQL é trocado por SQLite em memória
/// e o Secret do JWT é um valor de teste, então nada depende da máquina de quem roda os testes.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@adega.local";
    public const string AdminPassword = "AdminTeste1";
    public const string JwtSecret = "chave-de-teste-com-mais-de-32-caracteres!";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>
    /// Tentativas de login por minuto. Os testes fazem dezenas de logins seguidos do mesmo "endereço",
    /// então o padrão aqui é alto; o teste do limite cria uma fábrica própria com um valor baixo.
    /// </summary>
    public int LoginPermitLimit { get; init; } = 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ambiente "Testing": não carrega os User Secrets de quem está rodando os testes.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=nao-usado;Database=nao-usado;");
        builder.UseSetting("Jwt:Secret", JwtSecret);
        // A API define a senha do admin semeado na subida, como em produção.
        builder.UseSetting("Admin:InitialPassword", AdminPassword);
        builder.UseSetting("RateLimiting:LoginPermitLimit", LoginPermitLimit.ToString());
        // Sem logs no console dos testes (cada requisição imprimiria os comandos SQL).
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureTestServices(services =>
        {
            // Remove o DbContext configurado para MySQL e registra de novo com SQLite.
            services.RemoveAll<DbContextOptions<AdegaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AdegaDbContext>>();

            _connection.Open();
            var options = new DbContextOptionsBuilder<AdegaDbContext>().UseSqlite(_connection).Options;
            using (var context = new AdegaDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            services.AddDbContext<AdegaDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
