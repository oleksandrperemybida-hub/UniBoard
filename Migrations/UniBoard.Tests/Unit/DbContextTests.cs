using Microsoft.EntityFrameworkCore;
using UniBoard.Models;
using UniBoard.Tests.Helpers;

namespace UniBoard.Tests.Unit;

/// <summary>Task 2 i 3: konfiguracja EF Core - relacje, ograniczenia i indeksy (na SQLite w pamięci).</summary>
public class DbContextTests
{
    [Fact]
    public async Task Announcement_RoundTrip_PreservesAllFields()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        var created = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var announcement = TestData.MakeAnnouncement(user.Id, cat.Id, "Tytul", created, 123.45m, "a@b.pl");
        ctx.Announcements.Add(announcement);
        await ctx.SaveChangesAsync();

        await using var fresh = db.CreateContext();
        var loaded = await fresh.Announcements.Include(a => a.Category).Include(a => a.Author).SingleAsync();

        Assert.Equal("Tytul", loaded.Title);
        Assert.Equal(123.45m, loaded.Price);
        Assert.Equal("a@b.pl", loaded.Contact);
        Assert.Equal(created, loaded.CreatedAt);
        Assert.Equal(cat.Name, loaded.Category!.Name);
        Assert.Equal(user.Id, loaded.Author!.Id);
    }

    [Fact]
    public async Task Announcement_Price_CanBeNull()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, cat.Id, price: null));
        await ctx.SaveChangesAsync();

        await using var fresh = db.CreateContext();
        Assert.Null((await fresh.Announcements.SingleAsync()).Price);
    }

    [Fact]
    public async Task Category_Name_MustBeUnique()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Categories.Add(TestData.MakeCategory("Elektronika"));
        await ctx.SaveChangesAsync();

        ctx.Categories.Add(TestData.MakeCategory("Elektronika"));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Announcement_WithUnknownCategory_IsRejected()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var user = TestData.MakeUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, categoryId: 9999));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Announcement_WithUnknownAuthor_IsRejected()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var category = TestData.MakeCategory();
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.Announcements.Add(TestData.MakeAnnouncement("nie-ma-takiego-autora", category.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingCategory_WithAnnouncements_IsRestricted()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, cat.Id));
        await ctx.SaveChangesAsync();

        await using var other = db.CreateContext();
        var toDelete = await other.Categories.SingleAsync();
        other.Categories.Remove(toDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingUser_WithAnnouncements_IsRestricted()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        ctx.Announcements.Add(TestData.MakeAnnouncement(user.Id, cat.Id));
        await ctx.SaveChangesAsync();

        await using var other = db.CreateContext();
        var toDelete = await other.Users.SingleAsync();
        other.Users.Remove(toDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingCategory_WithoutAnnouncements_Works()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (_, cat) = await TestData.SeedBasicsAsync(ctx);

        ctx.Categories.Remove(cat);
        await ctx.SaveChangesAsync();

        Assert.Empty(ctx.Categories);
    }

    [Fact]
    public void Model_HasIndexOnCreatedAt_AndUniqueIndexOnCategoryName()
    {
        using var db = new TestDb();
        using var ctx = db.CreateContext();

        var announcement = ctx.Model.FindEntityType(typeof(Announcement))!;
        Assert.Contains(announcement.GetIndexes(), i => i.Properties.Any(p => p.Name == nameof(Announcement.CreatedAt)));

        var category = ctx.Model.FindEntityType(typeof(Category))!;
        Assert.Contains(category.GetIndexes(), i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Category.Name)));
    }

    [Fact]
    public void Model_PriceColumnType_IsDecimal10_2()
    {
        using var db = new TestDb();
        using var ctx = db.CreateContext();

        var price = ctx.Model.FindEntityType(typeof(Announcement))!.FindProperty(nameof(Announcement.Price))!;
        Assert.Equal("decimal(10,2)", price.GetColumnType());
    }

    [Fact]
    public async Task User_CanHaveManyAnnouncements()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (user, cat) = await TestData.SeedBasicsAsync(ctx);
        await TestData.SeedAnnouncementsAsync(ctx, user.Id, cat.Id, 3);

        await using var fresh = db.CreateContext();
        var loaded = await fresh.Users.Include(u => u.Announcements).SingleAsync();
        Assert.Equal(3, loaded.Announcements.Count);
    }
}
