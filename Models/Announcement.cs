using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniBoard.Models;

public class Announcement
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tytuł jest wymagany")]
    [StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Opis jest wymagany")]
    [StringLength(2000)]
    public string Description { get; set; } = "";

    [Range(0, 100000, ErrorMessage = "Cena musi być między 0 a 100000")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal? Price { get; set; }          

    [Required(ErrorMessage = "Kontakt jest wymagany")]
    [EmailAddress]
    [StringLength(254)]
    public string Contact { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Kategoria")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string AuthorId { get; set; } = "";
    public ApplicationUser? Author { get; set; }
}