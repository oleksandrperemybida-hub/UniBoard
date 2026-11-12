using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniBoard.Data;

namespace UniBoard.Tests.Helpers;

/// <summary>
/// Baza SQLite w pamięci (zamiast LocalDB) - szybka, nie wymaga instalacji i
/// w odróżnieniu od providera InMemory egzekwuje klucze obce oraz unikalne indeksy.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbContextOptions<ApplicationDbContext> Options { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        EnableForeignKeys(_connection);

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext() => new(Options);

    public static void EnableForeignKeys(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
