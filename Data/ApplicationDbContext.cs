using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniBoard.Models;


namespace UniBoard.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); 

        builder.Entity<Announcement>(e =>
        {
            e.HasOne(a => a.Category)
             .WithMany(c => c.Announcements)
             .HasForeignKey(a => a.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Author)
             .WithMany(u => u.Announcements)
             .HasForeignKey(a => a.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(a => a.CreatedAt);
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique(); 
        });
    }
}