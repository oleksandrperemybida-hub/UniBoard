using System.ComponentModel.DataAnnotations;
using UniBoard.Models;
using CreateModel = UniBoard.Pages.Announcements.CreateModel;

namespace UniBoard.Tests.Unit;

/// <summary>
/// Testy reguł walidacji (DataAnnotations) - Task 3 (model danych) i Task 7 (formularz).
/// Wartości graniczne: długości tekstu, zakres ceny, e-mail, kategoria.
/// </summary>
public class ModelValidationTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    private static bool HasError(IEnumerable<ValidationResult> results, string member)
        => results.Any(r => r.MemberNames.Contains(member));

    private static CreateModel.AnnouncementInput ValidInput() => new()
    {
        Title = "Sprzedam rower",
        Description = "Opis ogloszenia",
        Price = 100m,
        Contact = "kontakt@test.pl",
        CategoryId = 1
    };

    private static Announcement ValidAnnouncement() => new()
    {
        Title = "Sprzedam rower",
        Description = "Opis ogloszenia",
        Price = 100m,
        Contact = "kontakt@test.pl",
        CategoryId = 1,
        AuthorId = "user-1"
    };

    // ---------- formularz (AnnouncementInput) ----------

    [Fact]
    public void Input_Valid_HasNoErrors()
        => Assert.Empty(Validate(ValidInput()));

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Input_Title_LengthBoundaries(int length, bool valid)
    {
        var input = ValidInput();
        input.Title = new string('a', length);
        Assert.Equal(valid, !HasError(Validate(input), nameof(input.Title)));
    }

    [Fact]
    public void Input_Title_WhitespaceOnly_IsInvalid()
    {
        var input = ValidInput();
        input.Title = "     ";
        Assert.True(HasError(Validate(input), nameof(input.Title)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Input_Description_LengthBoundaries(int length, bool valid)
    {
        var input = ValidInput();
        input.Description = new string('a', length);
        Assert.Equal(valid, !HasError(Validate(input), nameof(input.Description)));
    }

    [Theory]
    [InlineData(-1.0, false)]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(100000.0, true)]
    [InlineData(100001.0, false)]
    [InlineData(999999.0, false)]
    public void Input_Price_RangeBoundaries(double price, bool valid)
    {
        var input = ValidInput();
        input.Price = (decimal)price;
        Assert.Equal(valid, !HasError(Validate(input), nameof(input.Price)));
    }

    // Range(0, 100000) ma int-owe granice, a Price to decimal - wartość jest zaokrąglana do int
    // przed porównaniem, więc ułamkowe ceny tuż poza zakresem mogą przechodzić walidację.
    [Theory]
    [Trait("Category", "KnownBug")]
    [InlineData(-0.40)]
    [InlineData(100000.40)]
    public void BUG06_Input_Price_FractionOutsideRange_ShouldBeInvalid(double price)
    {
        var input = ValidInput();
        input.Price = (decimal)price;
        Assert.True(HasError(Validate(input), nameof(input.Price)));
    }

    [Fact]
    public void Input_Price_Null_IsAllowed_MeansFree()
    {
        var input = ValidInput();
        input.Price = null;
        Assert.Empty(Validate(input));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("not-an-email", false)]
    [InlineData("@b.pl", false)]
    [InlineData("a@", false)]
    [InlineData("a@@b.pl", false)]
    [InlineData("a@b.pl", true)]
    [InlineData("jan.kowalski@student.uni.edu.pl", true)]
    public void Input_Contact_EmailFormat(string contact, bool valid)
    {
        var input = ValidInput();
        input.Contact = contact;
        Assert.Equal(valid, !HasError(Validate(input), nameof(input.Contact)));
    }

    [Fact]
    public void Input_Contact_254Chars_IsValid_255Chars_IsInvalid()
    {
        var ok = ValidInput();
        ok.Contact = new string('x', 249) + "@a.pl";      // 254 znaki
        Assert.False(HasError(Validate(ok), nameof(ok.Contact)));

        var tooLong = ValidInput();
        tooLong.Contact = new string('x', 250) + "@a.pl"; // 255 znaków
        Assert.True(HasError(Validate(tooLong), nameof(tooLong.Contact)));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]   // "-- wybierz kategorię --"
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void Input_CategoryId_MustBePositive(int categoryId, bool valid)
    {
        var input = ValidInput();
        input.CategoryId = categoryId;
        Assert.Equal(valid, !HasError(Validate(input), nameof(input.CategoryId)));
    }

    [Fact]
    public void Input_DoesNotExposeAuthorOrDate_ProtectionAgainstOverposting()
    {
        var names = typeof(CreateModel.AnnouncementInput).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "CategoryId", "Contact", "Description", "Price", "Title" }, names);
    }

    // ---------- encja Announcement ----------

    [Fact]
    public void Announcement_Valid_HasNoErrors()
        => Assert.Empty(Validate(ValidAnnouncement()));

    [Fact]
    public void Announcement_Default_FailsRequiredFields()
    {
        var errors = Validate(new Announcement());
        Assert.True(HasError(errors, nameof(Announcement.Title)));
        Assert.True(HasError(errors, nameof(Announcement.Description)));
        Assert.True(HasError(errors, nameof(Announcement.Contact)));
    }

    [Fact]
    public void Announcement_CreatedAt_DefaultsToNowUtc()
    {
        var a = new Announcement();
        Assert.InRange(a.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Announcement_Title_LengthBoundaries(int length, bool valid)
    {
        var a = ValidAnnouncement();
        a.Title = new string('a', length);
        Assert.Equal(valid, !HasError(Validate(a), nameof(a.Title)));
    }

    // ---------- Category / ApplicationUser ----------

    [Theory]
    [InlineData("", false)]
    [InlineData("A", true)]
    [InlineData("Elektronika", true)]
    public void Category_Name_Required(string name, bool valid)
    {
        var c = new Category { Name = name };
        Assert.Equal(valid, !HasError(Validate(c), nameof(c.Name)));
    }

    [Fact]
    public void Category_Name_Max50()
    {
        Assert.False(HasError(Validate(new Category { Name = new string('a', 50) }), nameof(Category.Name)));
        Assert.True(HasError(Validate(new Category { Name = new string('a', 51) }), nameof(Category.Name)));
    }

    [Fact]
    public void ApplicationUser_FirstAndLastName_AreRequired_AndMax50()
    {
        var empty = Validate(new ApplicationUser());
        Assert.True(HasError(empty, nameof(ApplicationUser.FirstName)));
        Assert.True(HasError(empty, nameof(ApplicationUser.LastName)));

        var tooLong = Validate(new ApplicationUser { FirstName = new string('a', 51), LastName = new string('b', 51) });
        Assert.True(HasError(tooLong, nameof(ApplicationUser.FirstName)));
        Assert.True(HasError(tooLong, nameof(ApplicationUser.LastName)));

        var ok = Validate(new ApplicationUser { FirstName = "Jan", LastName = "Kowalski" });
        Assert.Empty(ok);
    }
}
