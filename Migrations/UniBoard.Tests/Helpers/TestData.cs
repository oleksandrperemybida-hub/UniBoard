using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Tests.Helpers;

public static class TestData
{
    public static ApplicationUser MakeUser(
        string? id = null, string first = "Jan", string last = "Kowalski", string? email = null)
    {
        id ??= Guid.NewGuid().ToString();
        email ??= $"{id[..8]}@test.pl";
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = first,
            LastName = last
        };
    }

    public static Category MakeCategory(string name = "Elektronika") => new() { Name = name };

    public static Announcement MakeAnnouncement(
        string authorId, int categoryId, string title = "Sprzedam rower",
        DateTime? createdAt = null, decimal? price = 100m, string contact = "kontakt@test.pl")
        => new()
        {
            Title = title,
            Description = "Opis ogloszenia",
            Price = price,
            Contact = contact,
            CategoryId = categoryId,
            AuthorId = authorId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

    /// <summary>Zapisuje jednego użytkownika i jedną kategorię.</summary>
    public static async Task<(ApplicationUser User, Category Category)> SeedBasicsAsync(ApplicationDbContext ctx)
    {
        var user = MakeUser();
        var category = MakeCategory();
        ctx.Users.Add(user);
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();
        return (user, category);
    }

    /// <summary>Zapisuje <paramref name="count"/> ogłoszeń o rosnących datach ("Ogloszenie 1" = najstarsze).</summary>
    public static async Task SeedAnnouncementsAsync(
        ApplicationDbContext ctx, string authorId, int categoryId, int count)
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= count; i++)
        {
            ctx.Announcements.Add(MakeAnnouncement(
                authorId, categoryId, title: $"Ogloszenie {i}", createdAt: start.AddMinutes(i)));
        }
        await ctx.SaveChangesAsync();
    }
}
