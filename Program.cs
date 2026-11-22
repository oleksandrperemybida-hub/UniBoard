using Microsoft.EntityFrameworkCore;
using UniBoard.Data;
using Microsoft.AspNetCore.Identity;
using UniBoard.Models;

namespace UniBoard
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Brak connection string 'DefaultConnection'.");

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

            // Настройка пути при отказе в доступе (403)
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath = "/Error";
            });

            builder.Services.AddRazorPages();

            var app = builder.Build();

            await DbSeeder.SeedAsync(app.Services);

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Перехват статус-кодов (403, 404 и др.) и отображение кастомной страницы Error
            app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }
    }
}