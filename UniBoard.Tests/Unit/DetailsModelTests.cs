using Microsoft.AspNetCore.Mvc;
using UniBoard.Tests.Helpers;
using DetailsModel = UniBoard.Pages.Announcements.DetailsModel;

namespace UniBoard.Tests.Unit;

/// <summary>Task 9: strona szczegółów ogłoszenia (logika PageModel - bez renderowania widoku).</summary>
public class DetailsModelTests
{
    [Fact]
    public async Task NullId_ReturnsNotFound()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();

        var result = await new DetailsModel(ctx).OnGetAsync(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99999)]
    public async Task UnknownId_ReturnsNotFound(int id)
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();

        var result = await new DetailsModel(ctx).OnGetAsync(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExistingId_ReturnsPage_WithCategoryAndAuthorLoaded()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        var announcement = TestData.MakeAnnouncement(user.Id, cat.Id, "Sprzedam laptopa", price: 1500m);
        ctx.Announcements.Add(announcement);
        await ctx.SaveChangesAsync();

        var model = new DetailsModel(ctx);
        var result = await model.OnGetAsync(announcement.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Equal("Sprzedam laptopa", model.Announcement.Title);
        Assert.Equal(1500m, model.Announcement.Price);
        Assert.Equal(cat.Name, model.Announcement.Category!.Name);
        Assert.Equal(user.Id, model.Announcement.Author!.Id);
        Assert.Equal("Jan", model.Announcement.Author.FirstName);
    }

    [Fact]
    public async Task ReturnsTheRequestedAnnouncement_NotAnotherOne()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        var a1 = TestData.MakeAnnouncement(user.Id, cat.Id, "Pierwsze");
        var a2 = TestData.MakeAnnouncement(user.Id, cat.Id, "Drugie");
        ctx.Announcements.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        var model = new DetailsModel(ctx);
        await model.OnGetAsync(a2.Id);

        Assert.Equal("Drugie", model.Announcement.Title);
    }
}
