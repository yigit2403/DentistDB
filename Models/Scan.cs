using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum ScanType
{
    [Display(Name = "Panoramik")]
    Panoramic,
    [Display(Name = "Tomografi (BT)")]
    CT,
    [Display(Name = "Periapikal")]
    Periapical,
    [Display(Name = "Bitewing")]
    Bitewing,
    [Display(Name = "Ağız İçi Fotoğraf")]
    IntraoralPhoto,
    [Display(Name = "Belge")]
    Document,
    [Display(Name = "Diğer")]
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
    public string StoredPath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Display(Name = "Dosya Boyutu")]
    public long FileSize { get; set; }

    [Display(Name = "Görüntü Türü")]
    public ScanType ScanType { get; set; } = ScanType.Other;

    [Display(Name = "Çekim Tarihi")]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    [NotMapped]
    public bool IsPdf => string.Equals(ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    [NotMapped]
    public string FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F0} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
    };
}
