using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using UniBoard.Data;
using UniBoard.Models;
using UniBoard.Tests.Helpers;

namespace UniBoard.Tests.Integration;

/// <summary>
/// Uruchamia prawdziwą aplikację UniBoard (Program.cs, Razor Pages, Identity) w pamięci,
/// ale z bazą SQLite w pamięci zamiast LocalDB i z "udawanym" logowaniem przez nagłówki.
/// Każda klasa testowa (IClassFixture) dostaje własną, czystą bazę.
/// </summary>
public class UniBoardFactory : WebApplicationFactory<UniBoard.Program>
{
    private readonly SqliteConnection _connection;

    public UniBoardFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        TestDb.EnableForeignKeys(_connection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));

            services.AddAuthentication(o => o.DefaultAuthenticateScheme = TestAuthHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        return host;
    }

    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        await seed(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>Klient HTTP bez automatycznych przekierowań (żeby móc sprawdzać 302 i Location).</summary>
    public HttpClient CreateClientNoRedirect()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Klient "zalogowany" jako użytkownik o podanym Id (opcjonalnie z rolami, np. "Administrator").</summary>
    public HttpClient CreateClientFor(string userId, string? roles = null)
    {
        var client = CreateClientNoRedirect();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        }
        return client;
    }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Na stronie formularza nie znaleziono tokenu antiforgery.");
        return match.Groups[1].Value;
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
