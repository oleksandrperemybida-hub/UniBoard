using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UniBoard.Models;

namespace UniBoard.Pages.Announcements
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Announcement Announcement { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var announcement = await _context.Announcements
                .Include(a => a.Category)
                .Include(a => a.Author)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (announcement == null)
            {
                return NotFound();
            }

            Announcement = announcement;
            return Page();
        }
    }
}