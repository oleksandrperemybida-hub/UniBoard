using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniBoard.Data;
using UniBoard.Models;
using UniBoard.Tests.Helpers;

namespace UniBoard.Tests.Unit;

/// <summary>Task 5 i Task 7: role Administrator/User, konto administratora z user-secrets, startowe kategorie.</summary>
public class DbSeederTests
{
    private const string AdminEmail = "admin@uniboard.local";
    private const string AdminPassword = "TwojeHaslo1!";

    private static Dictionary<string, string?> AdminConfig(string? email = AdminEmail, string? password = AdminPassword)
        => new() { ["Admin:Email"] = email, ["Admin:Password"] = password };

    [Fact]
    public async Task WithoutSecrets_CreatesRolesAndCategories_ButNoAdminAndNoException()
    {
        using var host = new TestHost();

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await roles.RoleExistsAsync(AppRoles.Administrator));
        Assert.True(await roles.RoleExistsAsync(AppRoles.User));
        Assert.Equal(6, await db.Categories.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DefaultCategories_HaveExpectedNames()
    {
        using var host = new TestHost();

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var names = await db.Categories.Select(c => c.Name).ToListAsync();

        Assert.Equal(
            new[] { "Książki i notatki", "Elektronika", "Mieszkania i pokoje", "Korepetycje", "Wydarzenia", "Inne" }
                .OrderBy(n => n),
            names.OrderBy(n => n));
    }

    [Fact]
    public async Task WithSecrets_CreatesAdmin_WithConfirmedEmail_AdministratorRole_AndWorkingPassword()
    {
        using var host = new TestHost(AdminConfig());

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(AdminEmail);

        Assert.NotNull(admin);
        Assert.True(admin!.EmailConfirmed);
        Assert.Equal("Admin", admin.FirstName);
        Assert.Equal("UniBoard", admin.LastName);
        Assert.True(await users.IsInRoleAsync(admin, AppRoles.Administrator));
        Assert.False(await users.IsInRoleAsync(admin, AppRoles.User));
        Assert.True(await users.CheckPasswordAsync(admin, AdminPassword));
        Assert.False(await users.CheckPasswordAsync(admin, "ZleHaslo1!"));
    }

    [Fact]
    public async Task WithSecrets_CustomFirstAndLastName_AreUsed()
    {
        var config = AdminConfig();
        config["Admin:FirstName"] = "Anna";
        config["Admin:LastName"] = "Nowak";
        using var host = new TestHost(config);

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(AdminEmail);
        Assert.Equal("Anna", admin!.FirstName);
        Assert.Equal("Nowak", admin.LastName);
    }

    [Fact]
    public async Task RunningTwice_IsIdempotent()
    {
        using var host = new TestHost(AdminConfig());

        await DbSeeder.SeedAsync(host.Provider);
        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(6, await db.Categories.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.UserRoles.CountAsync());
    }

    [Fact]
    public async Task ExistingCategories_AreNotOverwritten()
    {
        using var host = new TestHost();
        using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Categories.Add(TestData.MakeCategory("Moja kategoria"));
            await db.SaveChangesAsync();
        }

        await DbSeeder.SeedAsync(host.Provider);

        using var check = host.CreateScope();
        var names = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Categories.Select(c => c.Name).ToListAsync();
        Assert.Equal(new[] { "Moja kategoria" }, names);
    }

    [Fact]
    public async Task WeakPassword_DoesNotCreateAdmin_AndDoesNotThrow()
    {
        using var host = new TestHost(AdminConfig(password: "abc"));

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    [Theory]
    [InlineData(null, AdminPassword)]
    [InlineData("", AdminPassword)]
    [InlineData("   ", AdminPassword)]
    [InlineData(AdminEmail, null)]
    [InlineData(AdminEmail, "")]
    [InlineData(AdminEmail, "   ")]
    public async Task MissingOrBlankSecret_DoesNotCreateAdmin(string? email, string? password)
    {
        using var host = new TestHost(AdminConfig(email, password));

        await DbSeeder.SeedAsync(host.Provider);

        using var scope = host.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task ExistingUserWithAdminEmail_GetsAdministratorRole()
    {
        using var host = new TestHost(AdminConfig());
        using (var scope = host.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = new ApplicationUser { UserName = AdminEmail, Email = AdminEmail, FirstName = "Stary", LastName = "Uzytkownik" };
            Assert.True((await users.CreateAsync(existing, AdminPassword)).Succeeded);
        }

        await DbSeeder.SeedAsync(host.Provider);

        using var check = host.CreateScope();
        var um = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await um.FindByEmailAsync(AdminEmail);
        Assert.Equal("Stary", admin!.FirstName);                         // istniejące konto nie jest nadpisywane
        Assert.True(await um.IsInRoleAsync(admin, AppRoles.Administrator));
    }
}
