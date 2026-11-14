using AdegaSystem.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Infrastructure;

/// <summary>
/// Banco SQLite em memória, recriado para cada classe de teste.
/// Usamos SQLite (e não o provider InMemory do EF) porque ele respeita
/// chaves estrangeiras, índices únicos e transações, como o MySQL.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        // O banco em memória existe enquanto a conexão estiver aberta.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AdegaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AdegaDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AdegaDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
