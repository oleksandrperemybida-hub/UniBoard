using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Pages.Admin
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Announcement> Announcements { get; set; } = new List<Announcement>();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            Announcements = await _context.Announcements
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            StatusMessage = "Ogłoszenie zostało usunięte przez administratora.";
            return RedirectToPage();
        }
    }
}