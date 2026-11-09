using Microsoft.AspNetCore.Identity;
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

        // 1. Role: Administrator i User (tworzone tylko, jeśli ich jeszcze nie ma)
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

        // 2. Konto administratora - dane z user-secrets, nie z repozytorium
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
