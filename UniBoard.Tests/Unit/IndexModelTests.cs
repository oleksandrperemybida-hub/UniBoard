using UniBoard.Models;
using UniBoard.Tests.Helpers;
using IndexModel = UniBoard.Pages.IndexModel;

namespace UniBoard.Tests.Unit;

/// <summary>Task 8: lista ogłoszeń - stronicowanie (9 na stronę), sortowanie, skracanie opisu, nazwa autora.</summary>
public class IndexModelTests
{
    // ---------- stronicowanie / sortowanie ----------

    [Fact]
    public async Task EmptyBoard_HasOnePage_AndNoItems()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();

        var model = new IndexModel(ctx);
        await model.OnGetAsync();

        Assert.Empty(model.Announcements);
        Assert.Equal(0, model.TotalCount);
        Assert.Equal(1, model.TotalPages);
        Assert.Equal(1, model.CurrentPage);
    }

    [Fact]
    public void PageSize_Is9()
        => Assert.Equal(9, IndexModel.PageSize);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(18, 2)]
    [InlineData(19, 3)]
    public async Task TotalPages_IsCeilingOfCountDividedByPageSize(int count, int expectedPages)
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, count);

        var model = new IndexModel(ctx);
        await model.OnGetAsync();

        Assert.Equal(count, model.TotalCount);
        Assert.Equal(expectedPages, model.TotalPages);
    }

    [Fact]
    public async Task FirstPage_Has9Items_SecondPage_HasRemaining()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 10);

        var page1 = new IndexModel(ctx);
        await page1.OnGetAsync(1);
        Assert.Equal(9, page1.Announcements.Count);
        Assert.Equal(1, page1.CurrentPage);

        var page2 = new IndexModel(ctx);
        await page2.OnGetAsync(2);
        Assert.Single(page2.Announcements);
        Assert.Equal(2, page2.CurrentPage);
        Assert.Equal("Ogloszenie 1", page2.Announcements[0].Title); // najstarsze trafia na ostatnią stronę
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task PageBelowOne_IsClampedToFirstPage(int requested)
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 10);

        var model = new IndexModel(ctx);
        await model.OnGetAsync(requested);

        Assert.Equal(1, model.CurrentPage);
        Assert.Equal(9, model.Announcements.Count);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(999)]
    [InlineData(int.MaxValue)]
    public async Task PageAboveLast_IsClampedToLastPage(int requested)
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 10);

        var model = new IndexModel(ctx);
        await model.OnGetAsync(requested);

        Assert.Equal(2, model.CurrentPage);
        Assert.Single(model.Announcements);
    }

    [Fact]
    public async Task Announcements_AreSortedNewestFirst()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 5);

        var model = new IndexModel(ctx);
        await model.OnGetAsync();

        Assert.Equal(
            new[] { "Ogloszenie 5", "Ogloszenie 4", "Ogloszenie 3", "Ogloszenie 2", "Ogloszenie 1" },
            model.Announcements.Select(a => a.Title));
    }

    [Fact]
    public async Task SameCreatedAt_HigherIdComesFirst()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        var same = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc);
        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, cat.Id, "Pierwsze", same));
        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, cat.Id, "Drugie", same));
        await ctx.SaveChangesAsync();

        var model = new IndexModel(ctx);
        await model.OnGetAsync();

        Assert.Equal(new[] { "Drugie", "Pierwsze" }, model.Announcements.Select(a => a.Title));
    }

    [Fact]
    public async Task Announcements_IncludeCategoryAndAuthor()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 1);

        var model = new IndexModel(ctx);
        await model.OnGetAsync();

        var item = Assert.Single(model.Announcements);
        Assert.NotNull(item.Category);
        Assert.Equal(cat.Name, item.Category!.Name);
        Assert.NotNull(item.Author);
        Assert.Equal(user.Id, item.Author!.Id);
    }

    // ---------- Shorten ----------

    [Fact]
    public void Shorten_Null_ReturnsNull()
        => Assert.Null(IndexModel.Shorten(null!));

    [Fact]
    public void Shorten_Empty_ReturnsEmpty()
        => Assert.Equal("", IndexModel.Shorten(""));

    [Fact]
    public void Shorten_TextUpToLimit_IsUnchanged()
    {
        var text = new string('a', 120);
        Assert.Equal(text, IndexModel.Shorten(text));
    }

    [Fact]
    public void Shorten_TextOverLimit_IsCutAndEndsWithEllipsis()
    {
        var result = IndexModel.Shorten(new string('a', 121));
        Assert.Equal(new string('a', 120) + "…", result);
    }

    [Fact]
    public void Shorten_TrimsTrailingWhitespaceBeforeEllipsis()
    {
        var text = new string('a', 119) + " " + "bbbbb";
        Assert.Equal(new string('a', 119) + "…", IndexModel.Shorten(text));
    }

    [Fact]
    public void Shorten_CustomMax()
        => Assert.Equal("abc…", IndexModel.Shorten("abcdef", 3));

    // ---------- AuthorName ----------

    [Fact]
    public void AuthorName_Null_ReturnsDefault()
        => Assert.Equal("Użytkownik", IndexModel.AuthorName(null));

    [Fact]
    public void AuthorName_UsesFirstName_WhenPresent()
        => Assert.Equal("Jan", IndexModel.AuthorName(new ApplicationUser { FirstName = "Jan", UserName = "x@y.pl" }));

    [Theory]
    [InlineData("", "jan@uni.pl", null, "jan")]            // użytkownik z rejestracji (brak imienia) -> część przed @
    [InlineData("   ", "anna@uni.pl", null, "anna")]
    [InlineData("", null, "ola@uni.pl", "ola")]            // brak UserName -> Email
    [InlineData("", "plainlogin", null, "plainlogin")]     // login bez @
    [InlineData("", null, null, "Użytkownik")]             // nic nie ma
    public void AuthorName_FallsBackToLoginOrEmail(string firstName, string? userName, string? email, string expected)
    {
        var user = new ApplicationUser { FirstName = firstName, UserName = userName, Email = email };
        Assert.Equal(expected, IndexModel.AuthorName(user));
    }
}
