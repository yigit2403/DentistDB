using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

/// <summary>A billable dental procedure on the clinic price list.</summary>
public class Procedure
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    [Display(Name = "İşlem Adı")]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Liste Fiyatı")]
    [Range(0, 9999999.99)]
    public decimal DefaultPrice { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
