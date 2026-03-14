using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum InvoiceStatus
{
    [Display(Name = "Taslak")]
    Draft,
    [Display(Name = "Kesildi")]
    Issued,
    [Display(Name = "Kısmen Ödendi")]
    PartiallyPaid,
    [Display(Name = "Ödendi")]
    Paid,
    [Display(Name = "İptal Edildi")]
    Cancelled
}

public class Invoice
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Display(Name = "Fatura Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Toplam Tutar")]
    [Range(0, 999999.99)]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Durum")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [NotMapped]
    public decimal TotalPaid => Payments.Where(p => !p.IsPlanned || p.IsSettled).Sum(p => p.Amount);

    [NotMapped]
    public decimal Balance => TotalAmount - TotalPaid;

    [NotMapped]
    public IEnumerable<Payment> PlannedPayments => Payments
        .Where(p => p.IsPlanned)
        .OrderBy(p => p.PaymentDate)
        .ThenBy(p => p.InstallmentNumber);
}
