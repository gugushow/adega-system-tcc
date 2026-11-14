using MySql.Data.MySqlClient;

namespace AdegaSystem.Api.Data.DemoData;

/// <summary>Como a API foi chamada: normal, carregando a demo ou usando o banco da demo.</summary>
public enum DemoMode
{
    /// <summary>Uso normal: banco da "DefaultConnection".</summary>
    None,

    /// <summary>"seed-demo": aplica as migrations no adega_demo, carrega os dados e encerra.</summary>
    Seed,

    /// <summary>"use-demo": sobe a API normalmente, mas apontando para o adega_demo.</summary>
    Use,
}

/// <summary>
/// Escolhe e confere a connection string do banco de demonstração.
///
/// Os dados demo moram num banco separado (adega_demo), com a própria connection string
/// ("ConnectionStrings:DemoConnection" nos User Secrets). Este arquivo garante que os comandos
/// da demo nunca encostem no banco de desenvolvimento normal nem rodem fora de Development.
/// </summary>
public static class DemoDatabase
{
    public const string SeedArgument = "seed-demo";
    public const string UseArgument = "use-demo";
    public const string ConnectionName = "DemoConnection";
    public const string DatabaseName = "adega_demo";

    public static DemoMode ParseMode(string[] args)
    {
        if (args.Contains(SeedArgument, StringComparer.OrdinalIgnoreCase))
        {
            return DemoMode.Seed;
        }

        return args.Contains(UseArgument, StringComparer.OrdinalIgnoreCase) ? DemoMode.Use : DemoMode.None;
    }

    /// <summary>
    /// Devolve a connection string do adega_demo, depois de conferir tudo o que protege os outros bancos.
    /// Qualquer problema vira InvalidOperationException, e a API nem chega a subir.
    /// </summary>
    public static string GetConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        // 1) Só no ambiente de desenvolvimento. O Docker de produção usa "Production" e cai aqui.
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Os comandos da demo ({SeedArgument}/{UseArgument}) só funcionam no ambiente Development. " +
                $"Ambiente atual: {environment.EnvironmentName}.");
        }

        // 2) Connection string própria: nunca reaproveita a DefaultConnection.
        var demo = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(demo))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionName}' não configurada. Rode na raiz do projeto: " +
                $"dotnet user-secrets set \"ConnectionStrings:{ConnectionName}\" " +
                $"\"Server=localhost;Port=3307;Database={DatabaseName};User=adega;Password=SUA_SENHA;\" --project backend/src/AdegaSystem.Api");
        }

        // 3) O banco precisa se chamar exatamente adega_demo.
        var demoDatabase = DatabaseOf(demo);
        if (!string.Equals(demoDatabase, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A '{ConnectionName}' precisa apontar para o banco '{DatabaseName}', mas aponta para '{demoDatabase}'.");
        }

        // 4) E a connection string normal não pode ser o mesmo banco: assim um erro de digitação
        //    nos User Secrets nunca faz a demo cair no banco de desenvolvimento (nem o inverso).
        var normal = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(normal)
            && string.Equals(DatabaseOf(normal), DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A 'DefaultConnection' também aponta para '{DatabaseName}'. O banco da demo precisa ser separado do banco normal.");
        }

        return demo;
    }

    private static string DatabaseOf(string connectionString)
    {
        try
        {
            return new MySqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Connection string em formato inválido.", ex);
        }
    }
}
