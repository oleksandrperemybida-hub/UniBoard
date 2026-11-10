using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniBoard.Models;

namespace UniBoard.Pages.Admin;

[Authorize(Roles = AppRoles.Administrator)]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
