using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

/// <summary>A treatment / procedure record in the patient's history.</summary>
public class PreviousOperation
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Display(Name = "Tarih")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, MaxLength(200)]
    [Display(Name = "İşlem Başlığı")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Tanı")]
    public string? Diagnosis { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Uygulanan İşlemler")]
    public string? Procedures { get; set; }

    [MaxLength(500)]
    [Display(Name = "Reçete")]
    public string? Prescriptions { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(200)]
    [Display(Name = "Seçilen Dişler")]
    public string? SelectedTeethData { get; set; }

    /// <summary>
    /// Lower-cased, diacritic-folded copy of the text fields so SQLite LIKE matches
    /// "çekim" against "Çekim" (LIKE is only case-insensitive for ASCII). Maintained by
    /// <see cref="Services.SearchNormalizer.BuildOperationIndex"/>.
    /// </summary>
    [MaxLength(600)]
    public string SearchIndex { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
