using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum TreatmentPlanStatus
{
    [Display(Name = "Planlandı")]
    Planned,
    [Display(Name = "Randevulu")]
    Scheduled,
    [Display(Name = "Tamamlandı")]
    Done,
    [Display(Name = "İptal")]
    Cancelled
}

/// <summary>A planned piece of work for a patient that later becomes an appointment, a treatment record and an invoice line.</summary>
public class TreatmentPlanItem
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    public int? ProcedureId { get; set; }

    [ForeignKey(nameof(ProcedureId))]
    public Procedure? Procedure { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "İşlem")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Dişler")]
    public string? ToothNumbers { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Tahmini Ücret")]
    public decimal EstimatedPrice { get; set; }

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public TreatmentPlanStatus Status { get; set; } = TreatmentPlanStatus.Planned;

    public int SortOrder { get; set; }

    public int? AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public Appointment? Appointment { get; set; }

    public int? PreviousOperationId { get; set; }

    [ForeignKey(nameof(PreviousOperationId))]
    public PreviousOperation? PreviousOperation { get; set; }

    public int? InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [NotMapped]
    public bool IsBilled => InvoiceId.HasValue;
}
