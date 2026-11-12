using System.Net;
using System.Text.RegularExpressions;
using UniBoard.Models;
using UniBoard.Tests.Helpers;

namespace UniBoard.Tests.Integration;

/// <summary>Lista ogłoszeń (Task 8) - stronicowanie i karty, na prawdziwej stronie HTML.</summary>
public class AnnouncementListPageTests : IClassFixture<UniBoardFactory>
{
    private readonly UniBoardFactory _factory;

    public AnnouncementListPageTests(UniBoardFactory factory) => _factory = factory;

    private static int CountCards(string html) => Regex.Matches(html, "class=\"card h-100").Count;

    private async Task SeedTenAnnouncementsAsync()
    {
        var alreadySeeded = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Any()));
        if (alreadySeeded) return;

        await _factory.SeedAsync(async db =>
        {
            var (user, cat) = await TestData.SeedBasicsAsync(db);
            await TestData.SeedAnnouncementsAsync(db, user.Id, cat.Id, 10);
        });
    }

    [Fact]
    public async Task FirstPage_ShowsNineCards_AndPagination()
    {
        await SeedTenAnnouncementsAsync();

        var html = await _factory.CreateClientNoRedirect().GetStringAsync("/");

        Assert.Equal(9, CountCards(html));
        Assert.Contains("Liczba ogłoszeń: 10", html);
        Assert.Contains("pagination", html);
        Assert.Contains("Ogloszenie 10", html);        // najnowsze
        Assert.DoesNotContain("Ogloszenie 1<", html);  // najstarsze jest dopiero na stronie 2
    }

    [Fact]
    public async Task SecondPage_ShowsRemainingCard()
    {
        await SeedTenAnnouncementsAsync();

        var html = await _factory.CreateClientNoRedirect().GetStringAsync("/?strona=2");

        Assert.Equal(1, CountCards(html));
        Assert.Contains("Ogloszenie 1<", html);
    }

    [Theory]
    [InlineData("/?strona=0")]
    [InlineData("/?strona=-5")]
    [InlineData("/?strona=999")]
    [InlineData("/?strona=abc")]
    public async Task InvalidPageParameter_NeverBreaksThePage(string url)
    {
        await SeedTenAnnouncementsAsync();

        var response = await _factory.CreateClientNoRedirect().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cards_LinkToDetailsPage()
    {
        await SeedTenAnnouncementsAsync();

        var html = await _factory.CreateClientNoRedirect().GetStringAsync("/");

        Assert.Matches("href=\"/Announcements/Details/\\d+\"", html);
    }
}

/// <summary>Strona szczegółów (Task 9) - renderowanie widoku, w tym znane błędy.</summary>
public class AnnouncementDetailsPageTests : IClassFixture<UniBoardFactory>
{
    private readonly UniBoardFactory _factory;

    public AnnouncementDetailsPageTests(UniBoardFactory factory) => _factory = factory;

    private async Task<int> SeedAnnouncementAsync(
        string firstName = "Jan", string lastName = "Kowalski", string title = "Sprzedam rower",
        decimal? price = 100m, string contact = "kontakt@test.pl", string authorEmail = "autor@test.pl")
    {
        var id = 0;
        await _factory.SeedAsync(async db =>
        {
            var user = TestData.MakeUser(first: firstName, last: lastName, email: authorEmail);
            var cat = TestData.MakeCategory($"Kat-{Guid.NewGuid():N}");
            db.Users.Add(user);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            var announcement = TestData.MakeAnnouncement(user.Id, cat.Id, title, price: price, contact: contact);
            db.Announcements.Add(announcement);
            await db.SaveChangesAsync();
            id = announcement.Id;
        });
        return id;
    }

    // ---------- działa poprawnie ----------

    [Theory]
    [InlineData("/Announcements/Details/999999")]   // nieistniejące
    [InlineData("/Announcements/Details/abc")]      // zły typ id
    [InlineData("/Announcements/Details")]          // brak id
    public async Task UnknownOrInvalidId_Returns404(string url)
    {
        var response = await _factory.CreateClientNoRedirect().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingAnnouncement_IsAvailableForAnonymous_AndShowsMainData()
    {
        var id = await SeedAnnouncementAsync(title: "Sprzedam laptopa", price: 1500m, authorEmail: "a1@test.pl");

        var response = await _factory.CreateClientNoRedirect().GetAsync($"/Announcements/Details/{id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sprzedam laptopa", html);
        Assert.Contains("Jan K.", html);     // imię i inicjał nazwiska
        Assert.Contains("zł", html);         // cena wyświetlona (sam format liczby zależy od kultury serwera)
    }

    [Fact]
    public async Task UserInput_IsHtmlEncoded_NoXss()
    {
        var id = await SeedAnnouncementAsync(title: "<b>x</b>", authorEmail: "xss@test.pl");

        var html = await _factory.CreateClientNoRedirect().GetStringAsync($"/Announcements/Details/{id}");

        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }

    // ---------- ZNANE BŁĘDY (testy celowo czerwone, dopóki błąd nie zostanie naprawiony) ----------

    // Konta z rejestracji mają puste FirstName/LastName (formularz rejestracji ich nie zbiera),
    // a widok robi LastName?.Substring(0, 1) - dla "" rzuca ArgumentOutOfRangeException => błąd 500.
    [Fact]
    [Trait("Category", "KnownBug")]
    public async Task BUG01_Details_AuthorWithEmptyLastName_ShouldNotCrash()
    {
        var id = await SeedAnnouncementAsync(firstName: "", lastName: "", authorEmail: "nowy@test.pl");

        var response = await _factory.CreateClientNoRedirect().GetAsync($"/Announcements/Details/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Widok pokazuje e-mail konta autora (Author.Email) zamiast kontaktu podanego w ogłoszeniu (Announcement.Contact).
    [Fact]
    [Trait("Category", "KnownBug")]
    public async Task BUG02_Details_ShouldShowAnnouncementContact_NotAuthorAccountEmail()
    {
        var id = await SeedAnnouncementAsync(contact: "kontakt@test.pl", authorEmail: "konto-autora@test.pl");

        var html = await _factory.CreateClientNoRedirect().GetStringAsync($"/Announcements/Details/{id}");

        Assert.Contains("kontakt@test.pl", html);
        Assert.DoesNotContain("konto-autora@test.pl", html);
    }

    // Lista pokazuje cenę 0 jako "Za darmo / Oddam", a szczegóły jako "0 zł".
    [Fact]
    [Trait("Category", "KnownBug")]
    public async Task BUG03_Details_PriceZero_ShouldBeShownAsFree_LikeOnTheList()
    {
        var id = await SeedAnnouncementAsync(price: 0m, authorEmail: "free@test.pl");

        var html = await _factory.CreateClientNoRedirect().GetStringAsync($"/Announcements/Details/{id}");

        Assert.Contains("Za darmo", html);
    }
}

/// <summary>Formularz dodawania ogłoszenia (Task 7) - pełny przepływ GET -> POST -> przekierowanie -> lista.</summary>
public class CreateAnnouncementFlowTests : IClassFixture<UniBoardFactory>
{
    private readonly UniBoardFactory _factory;
    private readonly ApplicationUser _user = TestData.MakeUser(email: "autor@test.pl");
    private int _categoryId;

    public CreateAnnouncementFlowTests(UniBoardFactory factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeedAsync()
    {
        var exists = await _factory.QueryAsync(db => Task.FromResult(db.Categories.Any()));
        if (exists)
        {
            _categoryId = await _factory.QueryAsync(db => Task.FromResult(db.Categories.Select(c => c.Id).First()));
            var userId = await _factory.QueryAsync(db => Task.FromResult(db.Users.Select(u => u.Id).First()));
            _userId = userId;
            return;
        }

        await _factory.SeedAsync(async db =>
        {
            db.Users.Add(_user);
            var cat = TestData.MakeCategory("Elektronika");
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            _categoryId = cat.Id;
        });
        _userId = _user.Id;
    }

    private string _userId = "";

    private async Task<HttpResponseMessage> PostFormAsync(HttpClient client, Dictionary<string, string> fields)
    {
        var token = await UniBoardFactory.GetAntiforgeryTokenAsync(client, "/Announcements/Create");
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync("/Announcements/Create", new FormUrlEncodedContent(fields));
    }

    private Dictionary<string, string> ValidFields(string title) => new()
    {
        ["Input.Title"] = title,
        ["Input.Description"] = "Opis ogloszenia",
        ["Input.Price"] = "250",
        ["Input.Contact"] = "kontakt@test.pl",
        ["Input.CategoryId"] = _categoryId.ToString()
    };

    [Fact]
    public async Task ValidPost_SavesAnnouncement_RedirectsToList_AndShowsItThere()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");

        var response = await PostFormAsync(client, ValidFields("Moje nowe ogloszenie"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);

        var saved = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Single(a => a.Title == "Moje nowe ogloszenie")));
        Assert.Equal(_userId, saved.AuthorId);
        Assert.Equal(250m, saved.Price);

        var listHtml = await client.GetStringAsync("/");
        Assert.Contains("Moje nowe ogloszenie", listHtml);
        Assert.Contains("alert-success", listHtml);   // komunikat o dodaniu (TempData)
    }

    [Fact]
    public async Task EmptyTitle_ShowsValidationError_AndSavesNothing()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");
        var before = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Count()));

        var response = await PostFormAsync(client, ValidFields(""));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);       // formularz wyświetlony ponownie
        Assert.Contains("field-validation-error", html);
        Assert.Equal(before, await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Count())));
    }

    [Fact]
    public async Task NoCategorySelected_ShowsValidationError_AndSavesNothing()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");
        var fields = ValidFields("Bez kategorii");
        fields["Input.CategoryId"] = "0";
        var before = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Count()));

        var response = await PostFormAsync(client, fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Count())));
    }

    [Fact]
    public async Task EmptyPrice_IsSavedAsNull_MeansFree()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");
        var fields = ValidFields("Oddam za darmo");
        fields["Input.Price"] = "";

        var response = await PostFormAsync(client, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Single(a => a.Title == "Oddam za darmo")));
        Assert.Null(saved.Price);
    }

    [Fact]
    public async Task Overposting_AuthorAndDateFromForm_AreIgnored()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");
        var fields = ValidFields("Proba overpostingu");
        fields["Input.AuthorId"] = "ktos-inny";
        fields["Input.CreatedAt"] = "2000-01-01T00:00:00";
        fields["AuthorId"] = "ktos-inny";
        fields["CreatedAt"] = "2000-01-01T00:00:00";

        var response = await PostFormAsync(client, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await _factory.QueryAsync(db => Task.FromResult(db.Announcements.Single(a => a.Title == "Proba overpostingu")));
        Assert.Equal(_userId, saved.AuthorId);
        Assert.True(saved.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        await EnsureSeedAsync();
        var client = _factory.CreateClientFor(_userId, "User");

        var response = await client.PostAsync("/Announcements/Create", new FormUrlEncodedContent(ValidFields("Bez tokenu")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>Nawigacja - czy linki z layoutu (Task 6) prowadzą do istniejących stron.</summary>
public class NavigationTests : IClassFixture<UniBoardFactory>
{
    private readonly UniBoardFactory _factory;

    public NavigationTests(UniBoardFactory factory) => _factory = factory;

    // Menu i stopka zawierają linki do /MyAnnouncements, /Favorites i /Contact, a tych stron jeszcze nie ma
    // (prawdopodobnie planowane na Sprint 2). Kliknięcie = 404 albo pusty href.
    [Fact]
    [Trait("Category", "KnownBug")]
    public async Task BUG04_AllInternalLinksInLayout_ShouldLeadToExistingPages()
    {
        var client = _factory.CreateClientNoRedirect();
        var html = await client.GetStringAsync("/");

        var anchors = Regex.Matches(html, "<a\\b[^>]*>").Select(m => m.Value).ToList();
        var emptyHref = anchors.Where(a => Regex.IsMatch(a, "href=\"\"")).ToList();
        Assert.True(emptyHref.Count == 0, "Linki z pustym href: " + string.Join(" | ", emptyHref));

        var internalLinks = anchors
            .Select(a => Regex.Match(a, "href=\"(/[^\"#]*)\""))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.NotEmpty(internalLinks);

        var broken = new List<string>();
        foreach (var link in internalLinks)
        {
            var response = await client.GetAsync(link);
            if (response.StatusCode == HttpStatusCode.NotFound) broken.Add(link);
        }

        Assert.True(broken.Count == 0, "Linki prowadzące do 404: " + string.Join(", ", broken));
    }
}
