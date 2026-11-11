using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Pages.Announcements;

// Task 7: Formularz dodawania ogłoszenia - tylko dla zalogowanych użytkowników
[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Osobny model formularza: autor i data NIE przychodzą z formularza (ochrona przed overpostingiem)
    [BindProperty]
    public AnnouncementInput Input { get; set; } = new();

    public SelectList Categories { get; set; } = default!;

    public class AnnouncementInput
    {
        [Required(ErrorMessage = "Tytuł jest wymagany")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Tytuł musi mieć od 3 do 100 znaków")]
        [Display(Name = "Tytuł")]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Opis jest wymagany")]
        [StringLength(2000, ErrorMessage = "Opis może mieć maksymalnie 2000 znaków")]
        [Display(Name = "Opis")]
        public string Description { get; set; } = "";

        [Range(0, 100000, ErrorMessage = "Cena musi być między 0 a 100000")]
        [Display(Name = "Cena (zł)")]
        public decimal? Price { get; set; }

        [Required(ErrorMessage = "Kontakt jest wymagany")]
        [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail")]
        [StringLength(254)]
        [Display(Name = "E-mail kontaktowy")]
        public string Contact { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Wybierz kategorię")]
        [Display(Name = "Kategoria")]
        public int CategoryId { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();

        // Domyślnie podpowiadamy e-mail zalogowanego użytkownika jako kontakt
        var user = await _userManager.GetUserAsync(User);
        Input.Contact = user?.Email ?? "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == Input.CategoryId))
        {
            ModelState.AddModelError("Input.CategoryId", "Wybierz kategorię z listy");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var announcement = new Announcement
        {
            Title = Input.Title.Trim(),
            Description = Input.Description.Trim(),
            Price = Input.Price,
            Contact = Input.Contact.Trim(),
            CategoryId = Input.CategoryId,
            AuthorId = user.Id,            // autor przypisywany automatycznie
            CreatedAt = DateTime.UtcNow     // data przypisywana automatycznie
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Ogłoszenie zostało dodane.";
        return RedirectToPage("/Index");
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
