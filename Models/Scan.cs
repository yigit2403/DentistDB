using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum ScanType
{
    Panoramic,
    CT,
    Periapical,
    Bitewing,
    Other
}

public class Scan
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Required, MaxLength(300)]
    [Display(Name = "File Name")]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    [Display(Name = "Stored Path")]
    public string StoredPath { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "Content Type")]
    public string ContentType { get; set; } = string.Empty;

    [Display(Name = "File Size (bytes)")]
    public long FileSize { get; set; }

    [Display(Name = "Scan Type")]
    public ScanType ScanType { get; set; } = ScanType.Other;

    [Display(Name = "Scan Date")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
