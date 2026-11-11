using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UniBoard.Data;
using UniBoard.Models;

namespace UniBoard.Pages
{
    // Task 8: Lista ogłoszeń w formie kart - od najnowszych, ze stronicowaniem
    public class IndexModel : PageModel
    {
        public const int PageSize = 9;

        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Announcement> Announcements { get; private set; } = new List<Announcement>();
        public int CurrentPage { get; private set; } = 1;
        public int TotalPages { get; private set; } = 1;
        public int TotalCount { get; private set; }

        public async Task OnGetAsync([FromQuery] int strona = 1)
        {
            TotalCount = await _context.Announcements.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            CurrentPage = Math.Clamp(strona, 1, TotalPages);

            Announcements = await _context.Announcements
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public static string Shorten(string text, int max = 120)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text[..max].TrimEnd() + "…";
        }

        public static string AuthorName(ApplicationUser? author)
        {
            if (author is null) return "Użytkownik";
            if (!string.IsNullOrWhiteSpace(author.FirstName)) return author.FirstName;
            var login = author.UserName ?? author.Email ?? "Użytkownik";
            var at = login.IndexOf('@');
            return at > 0 ? login[..at] : login;
        }
    }
}
