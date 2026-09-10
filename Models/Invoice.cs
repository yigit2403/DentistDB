using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum InvoiceStatus
{
    [Display(Name = "Taslak")]
    Draft,
    [Display(Name = "Açık")]
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
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    public DateOnly? DueDate { get; set; }

    /// <summary>Sum of all line items. Stored so lists and reports can query it directly.</summary>
    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Toplam Tutar")]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Durum")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [NotMapped]
    public decimal TotalPaid => Payments.Where(p => !p.IsPlanned || p.IsSettled).Sum(p => p.Amount);

    [NotMapped]
    public decimal Balance => TotalAmount - TotalPaid;

    [NotMapped]
    public bool IsOverdue => DueDate.HasValue
        && DueDate.Value < DateOnly.FromDateTime(DateTime.Today)
        && Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid;

    [NotMapped]
    public IEnumerable<Payment> PlannedPayments => Payments
        .Where(p => p.IsPlanned)
        .OrderBy(p => p.InstallmentNumber)
        .ThenBy(p => p.PaymentDate);

    [NotMapped]
    public IEnumerable<Payment> SettledPayments => Payments
        .Where(p => !p.IsPlanned || p.IsSettled)
        .OrderByDescending(p => p.SettledDate ?? p.PaymentDate)
        .ThenByDescending(p => p.Id);
}

public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    public int? ProcedureId { get; set; }

    [ForeignKey(nameof(ProcedureId))]
    public Procedure? Procedure { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "İşlem")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Diş")]
    public string? ToothNumbers { get; set; }

    [Display(Name = "Adet")]
    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Birim Fiyat")]
    public decimal UnitPrice { get; set; }

    public int SortOrder { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;
}
