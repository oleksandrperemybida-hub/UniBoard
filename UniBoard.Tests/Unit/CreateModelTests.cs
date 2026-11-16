using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniBoard.Data;
using UniBoard.Models;
using UniBoard.Tests.Helpers;
using CreateModel = UniBoard.Pages.Announcements.CreateModel;

namespace UniBoard.Tests.Unit;

/// <summary>Task 7: formularz dodawania ogłoszenia (logika PageModel).</summary>
public class CreateModelTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModelTests()
    {
        _scope = _host.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _host.Dispose();
    }

    // ---------- pomocnicze ----------

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private CreateModel BuildModel(string? userId)
    {
        var httpContext = new DefaultHttpContext();
        if (userId is not null)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        return new CreateModel(_db, _userManager)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider())
        };
    }

    /// <summary>Symuluje to, co robi ASP.NET Core przed wywołaniem handlera: walidację [BindProperty].</summary>
    private static void RunModelValidation(CreateModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model.Input, new ValidationContext(model.Input), results, true);
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                model.ModelState.AddModelError($"Input.{member}", result.ErrorMessage ?? "error");
            }
        }
    }

    private async Task<(ApplicationUser User, Category Category)> SeedAsync()
    {
        var (user, category) = await TestData.SeedBasicsAsync(_db);
        return (user, category);
    }

    private static void FillValidForm(CreateModel model, int categoryId)
    {
        model.Input.Title = "  Sprzedam rower  ";
        model.Input.Description = "  Rower górski, stan bardzo dobry.  ";
        model.Input.Price = 450m;
        model.Input.Contact = "  kontakt@test.pl  ";
        model.Input.CategoryId = categoryId;
    }

    // ---------- OnGet ----------

    [Fact]
    public async Task OnGet_PrefillsContactWithLoggedUserEmail_AndLoadsCategories()
    {
        var (user, _) = await SeedAsync();
        _db.Categories.Add(TestData.MakeCategory("Inne"));
        await _db.SaveChangesAsync();

        var model = BuildModel(user.Id);
        await model.OnGetAsync();

        Assert.Equal(user.Email, model.Input.Contact);
        Assert.Equal(2, model.Categories.Count());
    }

    [Fact]
    public async Task OnGet_CategoriesAreSortedByName()
    {
        var (user, _) = await SeedAsync(); // "Elektronika"
        _db.Categories.AddRange(TestData.MakeCategory("Wydarzenia"), TestData.MakeCategory("Korepetycje"));
        await _db.SaveChangesAsync();

        var model = BuildModel(user.Id);
        await model.OnGetAsync();

        Assert.Equal(new[] { "Elektronika", "Korepetycje", "Wydarzenia" }, model.Categories.Select(c => c.Text));
    }

    // ---------- OnPost: scenariusz poprawny ----------

    [Fact]
    public async Task OnPost_ValidForm_SavesAnnouncement_WithAuthorAndDate_AndRedirects()
    {
        var (user, category) = await SeedAsync();
        var model = BuildModel(user.Id);
        FillValidForm(model, category.Id);
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);

        var saved = await _db.Announcements.SingleAsync();
        Assert.Equal("Sprzedam rower", saved.Title);                       // przycięte spacje
        Assert.Equal("Rower górski, stan bardzo dobry.", saved.Description);
        Assert.Equal("kontakt@test.pl", saved.Contact);
        Assert.Equal(450m, saved.Price);
        Assert.Equal(category.Id, saved.CategoryId);
        Assert.Equal(user.Id, saved.AuthorId);                             // autor z sesji, nie z formularza
        Assert.InRange(saved.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal("Ogłoszenie zostało dodane.", model.TempData["Message"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(100000)]
    public async Task OnPost_PriceNullZeroAndMax_AreAccepted(int? price)
    {
        var (user, category) = await SeedAsync();
        var model = BuildModel(user.Id);
        FillValidForm(model, category.Id);
        model.Input.Price = price;
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal((decimal?)price, (await _db.Announcements.SingleAsync()).Price);
    }

    // ---------- OnPost: scenariusze błędne ----------

    [Fact]
    public async Task OnPost_UnknownCategory_ReturnsPageWithError_AndSavesNothing()
    {
        var (user, _) = await SeedAsync();
        var model = BuildModel(user.Id);
        FillValidForm(model, categoryId: 9999);
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("Input.CategoryId"));
        Assert.NotNull(model.Categories);                 // lista kategorii przeładowana do ponownego wyświetlenia
        Assert.Empty(_db.Announcements);
    }

    [Fact]
    public async Task OnPost_InvalidModel_ReturnsPage_AndSavesNothing()
    {
        var (user, category) = await SeedAsync();
        var model = BuildModel(user.Id);
        FillValidForm(model, category.Id);
        model.Input.Title = "";                           // brak tytułu
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.NotNull(model.Categories);
        Assert.Empty(_db.Announcements);
    }

    [Fact]
    public async Task OnPost_UserMissingInDatabase_ReturnsChallenge_AndSavesNothing()
    {
        var (_, category) = await SeedAsync();
        var model = BuildModel(userId: "nie-ma-takiego-uzytkownika");
        FillValidForm(model, category.Id);
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<ChallengeResult>(result);
        Assert.Empty(_db.Announcements);
    }

    [Fact]
    public async Task OnPost_AnonymousPrincipal_ReturnsChallenge()
    {
        var (_, category) = await SeedAsync();
        var model = BuildModel(userId: null);
        FillValidForm(model, category.Id);
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<ChallengeResult>(result);
    }

    // ---------- znany błąd ----------

    // Walidacja [StringLength(MinimumLength = 3)] sprawdza tekst PRZED przycięciem spacji (Trim),
    // więc " ab " przechodzi walidację, a w bazie ląduje tytuł "ab" (2 znaki) - łamie regułę min. 3 znaki.
    [Fact]
    [Trait("Category", "KnownBug")]
    public async Task BUG05_OnPost_TitleShorterThanMinimumAfterTrim_ShouldBeRejected()
    {
        var (user, category) = await SeedAsync();
        var model = BuildModel(user.Id);
        FillValidForm(model, category.Id);
        model.Input.Title = " ab ";
        RunModelValidation(model);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(_db.Announcements);
    }
}
