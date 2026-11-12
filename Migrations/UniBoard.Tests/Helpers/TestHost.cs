using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Tests.Helpers;

/// <summary>
/// Minimalny kontener DI z DbContextem (SQLite w pamięci) i ASP.NET Core Identity -
/// to samo co w Program.cs, ale bez serwera WWW. Używany do testów DbSeeder i CreateModel.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly SqliteConnection _connection;

    public ServiceProvider Provider { get; }

    public TestHost(Dictionary<string, string?>? config = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        TestDb.EnableForeignKeys(_connection);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        Provider = services.BuildServiceProvider();

        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    public IServiceScope CreateScope() => Provider.CreateScope();

    public void Dispose()
    {
        Provider.Dispose();
        _connection.Dispose();
    }
}
