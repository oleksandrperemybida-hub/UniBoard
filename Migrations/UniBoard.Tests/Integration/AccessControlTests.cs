using System.Net;
using UniBoard.Tests.Helpers;

namespace UniBoard.Tests.Integration;

/// <summary>
/// Task 4 i 5: dostęp anonimowy / zalogowany / administrator.
/// Testy przez prawdziwy HTTP do uruchomionej w pamięci aplikacji.
/// </summary>
public class AccessControlTests : IClassFixture<UniBoardFactory>
{
    private readonly UniBoardFactory _factory;

    public AccessControlTests(UniBoardFactory factory) => _factory = factory;

    // ---------- strony publiczne ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/Privacy")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    public async Task PublicPages_AreAvailableForAnonymous(string url)
    {
        var response = await _factory.CreateClientNoRedirect().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_EmptyBoard_ShowsEmptyStateAndLoginHint_ForAnonymous()
    {
        var html = await _factory.CreateClientNoRedirect().GetStringAsync("/");

        Assert.Contains("Nie ma jeszcze żadnych ogłoszeń.", html);
        Assert.Contains("Zaloguj się", html);
        Assert.DoesNotContain("Panel Administratora", html);
    }

    [Fact]
    public async Task UnknownUrl_Returns404()
    {
        var response = await _factory.CreateClientNoRedirect().GetAsync("/to-nie-istnieje");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- strony tylko dla zalogowanych ----------

    [Fact]
    public async Task Create_Anonymous_IsRedirectedToLogin()
    {
        var response = await _factory.CreateClientNoRedirect().GetAsync("/Announcements/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
        Assert.Contains("ReturnUrl", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Create_LoggedInUser_ReceivesForm()
    {
        var user = TestData.MakeUser(email: "jan@test.pl");
        await _factory.SeedAsync(async db =>
        {
            db.Users.Add(user);
            db.Categories.Add(TestData.MakeCategory("Elektronika"));
            await db.SaveChangesAsync();
        });

        var response = await _factory.CreateClientFor(user.Id, "User").GetAsync("/Announcements/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dodaj ogłoszenie", html);
        Assert.Contains("Elektronika", html);          // kategoria na liście
        Assert.Contains("value=\"jan@test.pl\"", html); // domyślny kontakt = e-mail użytkownika
    }

    // ---------- panel administratora (/Admin) ----------

    [Fact]
    public async Task Admin_Anonymous_IsRedirectedToLogin()
    {
        var response = await _factory.CreateClientNoRedirect().GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_RegularUser_IsDenied()
    {
        var response = await _factory.CreateClientFor("jakis-user", "User").GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_Administrator_HasAccess()
    {
        var response = await _factory.CreateClientFor("admin-id", "Administrator").GetAsync("/Admin");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Panel administratora", html);
    }

    [Fact]
    public async Task AdminMenuLink_IsVisibleOnlyForAdministrator()
    {
        var adminHtml = await _factory.CreateClientFor("admin-id", "Administrator").GetStringAsync("/");
        var userHtml = await _factory.CreateClientFor("user-id", "User").GetStringAsync("/");

        Assert.Contains("Panel Administratora", adminHtml);
        Assert.DoesNotContain("Panel Administratora", userHtml);
    }

    [Fact]
    public async Task LoggedInUser_SeesLogoutAndNoLoginButtons()
    {
        var html = await _factory.CreateClientFor("user-id", "User").GetStringAsync("/");

        Assert.Contains("Wyloguj", html);
        Assert.DoesNotContain("Zarejestruj się", html);
    }
}
