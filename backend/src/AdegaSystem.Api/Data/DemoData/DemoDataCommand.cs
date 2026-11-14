using AdegaSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Data.DemoData;

/// <summary>
/// O comando "seed-demo", chamado pelo Program.cs:
///   dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- seed-demo
///
/// 1. confere de novo que a conexão aberta é o banco adega_demo;
/// 2. aplica as migrations que já existem no projeto (cria as tabelas, as categorias e o admin);
/// 3. define a senha do admin (Admin:InitialPassword), como a API faz ao subir;
/// 4. roda o DemoDataSeeder e mostra um resumo. Devolve o código de saída do processo (0 = ok).
/// </summary>
public static class DemoDataCommand
{
    public static async Task<int> RunAsync(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoData");

        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();

                // Última barreira antes de criar tabelas: o banco desta conexão precisa ser o adega_demo.
                var database = context.Database.GetDbConnection().Database;
                if (!string.Equals(database, DemoDatabase.DatabaseName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"A carga só roda no banco '{DemoDatabase.DatabaseName}', não em '{database}'.");
                }

                logger.LogInformation("Aplicando as migrations no banco {Database}...", database);
                await context.Database.MigrateAsync();
            }

            await AdminPasswordInitializer.RunAsync(app.Services, app.Configuration, logger);

            var seeder = new DemoDataSeeder(app.Services, app.Environment, app.Configuration);
            var result = await seeder.RunAsync();
            if (result == DemoSeedResult.AlreadySeeded)
            {
                logger.LogInformation("Os dados de demonstração já estavam carregados. Nada foi alterado.");
                return 0;
            }

            logger.LogInformation(
                "Dados de demonstração carregados: {Products} produtos, {Suppliers} fornecedores, {Customers} clientes, " +
                "{Purchases} compras, {Sales} vendas e {Combos} combos. Logins demo: {Logins} (senha: a de {PasswordKey}).",
                DemoCatalog.Products.Length, DemoCatalog.Suppliers.Length, DemoCatalog.Customers.Length,
                DemoTimeline.Purchases.Length, DemoTimeline.Sales.Length, DemoCatalog.Combos.Length,
                string.Join(", ", DemoCatalog.Users.Select(u => u.Email)), DemoDataSeeder.PasswordKey);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A carga de demonstração não foi feita: {Message}", ex.Message);
            return 1;
        }
    }
}
