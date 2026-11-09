using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace UniBoard.Models;

public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "Imię jest wymagane")]
    [StringLength(50)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Nazwisko jest wymagane")]
    [StringLength(50)]
    public string LastName { get; set; } = "";

    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
}

