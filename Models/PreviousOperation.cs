using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public class PreviousOperation
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Ücret")]
    [Range(0, 999999.99)]
    public decimal PriceAmount { get; set; }

    [Display(Name = "Fatura")]
    public int? InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    [MaxLength(200)]
    [Display(Name = "İşlem Başlığı")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Tanı")]
    public string? Diagnosis { get; set; }

    [MaxLength(500)]
    [Display(Name = "Uygulanan İşlemler")]
    public string? Procedures { get; set; }

    [MaxLength(500)]
    [Display(Name = "Reçeteler")]
    public string? Prescriptions { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(200)]
    [Display(Name = "Seçilen Dişler")]
    public string? SelectedTeethData { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
