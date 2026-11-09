using System.ComponentModel.DataAnnotations;

namespace UniBoard.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa kategorii jest wymagana")]
    [StringLength(50)]
    public string Name { get; set; } = "";

    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
}