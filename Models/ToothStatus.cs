using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum ToothCondition
{
    [Display(Name = "Sağlam")]
    Healthy,
    [Display(Name = "Çürük")]
    Caries,
    [Display(Name = "Dolgu")]
    Filled,
    [Display(Name = "Kanal Tedavili")]
    RootCanal,
    [Display(Name = "Kaplama")]
    Crown,
    [Display(Name = "Köprü")]
    Bridge,
    [Display(Name = "İmplant")]
    Implant,
    [Display(Name = "Eksik / Çekilmiş")]
    Missing,
    [Display(Name = "Protez")]
    Denture,
    [Display(Name = "Takip")]
    Watch
}

/// <summary>Current state of one tooth on the patient's odontogram.</summary>
public class ToothStatus
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    /// <summary>FDI number, 11–48.</summary>
    [Range(11, 85)]
    public int ToothNumber { get; set; }

    public ToothCondition Condition { get; set; } = ToothCondition.Healthy;

    [MaxLength(300)]
    public string? Note { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
