using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Pages.Announcements;

// Task 11: Edycja własnego ogłoszenia - edytować może tylko autor ogłoszenia
[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Ten sam model formularza co przy dodawaniu (Task 7) - te same pola i reguły walidacji.
    // Autor i data dodania nie są w formularzu, więc nie da się ich zmienić.
    [BindProperty]
    public CreateModel.AnnouncementInput Input { get; set; } = new();

    public int Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public SelectList Categories { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var announcement = await _context.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (announcement is null)
        {
            return NotFound();
        }

        if (!IsAuthor(announcement))
        {
            return Forbid();   // cudze ogłoszenie -> 403 / strona AccessDenied
        }

        Id = announcement.Id;
        CreatedAt = announcement.CreatedAt;

        // Formularz wypełniony aktualnymi danymi ogłoszenia
        Input = new CreateModel.AnnouncementInput
        {
            Title = announcement.Title,
            Description = announcement.Description,
            Price = announcement.Price,
            Contact = announcement.Contact,
            CategoryId = announcement.CategoryId
        };

        await LoadCategoriesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id);

        if (announcement is null)
        {
            return NotFound();
        }

        // Sprawdzamy autora także przy zapisie - nie wystarczy ukryć przycisk "Edytuj"
        if (!IsAuthor(announcement))
        {
            return Forbid();
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == Input.CategoryId))
        {
            ModelState.AddModelError("Input.CategoryId", "Wybierz kategorię z listy");
        }

        if (!ModelState.IsValid)
        {
            Id = announcement.Id;
            CreatedAt = announcement.CreatedAt;
            await LoadCategoriesAsync();
            return Page();
        }

        // Zmieniamy tylko pola z formularza; AuthorId i CreatedAt zostają bez zmian
        announcement.Title = Input.Title.Trim();
        announcement.Description = Input.Description.Trim();
        announcement.Price = Input.Price;
        announcement.Contact = Input.Contact.Trim();
        announcement.CategoryId = Input.CategoryId;

        await _context.SaveChangesAsync();

        TempData["Message"] = "Zmiany w ogłoszeniu zostały zapisane.";
        return RedirectToPage("./Details", new { id = announcement.Id });
    }

    private bool IsAuthor(Announcement announcement)
    {
        var userId = _userManager.GetUserId(User);
        return userId is not null && announcement.AuthorId == userId;
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        Categories = new SelectList(categories, nameof(Category.Id), nameof(Category.Name));
    }
}
