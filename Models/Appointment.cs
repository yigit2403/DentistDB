using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum AppointmentStatus
{
    [Display(Name = "Planlandı")]
    Scheduled,
    [Display(Name = "Tamamlandı")]
    Completed,
    [Display(Name = "İptal Edildi")]
    Cancelled,
    [Display(Name = "Gelmedi")]
    NoShow
}

public class Appointment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Required]
    [Display(Name = "Tarih ve Saat")]
    public DateTime AppointmentDate { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "Randevu Nedeni")]
    public string Purpose { get; set; } = string.Empty;

    [Display(Name = "Durum")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
