using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniBoard.Models;

namespace UniBoard.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        foreach (var role in new[] { AppRoles.Administrator, AppRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    logger.LogError("Nie udało się utworzyć roli {Role}: {Errors}", role,
                        string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // Startowe kategorie ogłoszeń - potrzebne w formularzu dodawania ogłoszenia (Task 7).
        // Zarządzanie kategoriami przez administratora będzie w Sprincie 2.
        var db = sp.GetRequiredService<ApplicationDbContext>();
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Książki i notatki" },
                new Category { Name = "Elektronika" },
                new Category { Name = "Mieszkania i pokoje" },
                new Category { Name = "Korepetycje" },
                new Category { Name = "Wydarzenia" },
                new Category { Name = "Inne" });
            await db.SaveChangesAsync();
        }

        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Brak Admin:Email / Admin:Password w konfiguracji (user-secrets) - konto administratora nie zostało utworzone.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = config["Admin:FirstName"] ?? "Admin",
                LastName = config["Admin:LastName"] ?? "UniBoard"
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                logger.LogError("Nie udało się utworzyć konta administratora: {Errors}",
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Administrator))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Administrator);
        }
    }
}
