using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum ScanType
{
    [Display(Name = "Panoramik")]
    Panoramic,
    [Display(Name = "BT")]
    CT,
    [Display(Name = "Periapikal")]
    Periapical,
    [Display(Name = "Bitewing")]
    Bitewing,
    [Display(Name = "Diger")]
    Other
}

public class Scan
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Required, MaxLength(300)]
    [Display(Name = "Dosya Adı")]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    [Display(Name = "Kayıt Yolu")]
    public string StoredPath { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "İçerik Türü")]
    public string ContentType { get; set; } = string.Empty;

    [Display(Name = "Dosya Boyutu (bayt)")]
    public long FileSize { get; set; }

    [Display(Name = "Tarama Türü")]
    public ScanType ScanType { get; set; } = ScanType.Other;

    [Display(Name = "Tarama Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
