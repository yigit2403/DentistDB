using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum PaymentMethod
{
    [Display(Name = "Nakit")]
    Cash,
    [Display(Name = "Kredi Kartı")]
    CreditCard,
    [Display(Name = "Banka Kartı")]
    DebitCard,
    [Display(Name = "Havale / EFT")]
    BankTransfer,
    [Display(Name = "Sigorta")]
    Insurance,
    [Display(Name = "Diğer")]
    Other
}

public class Payment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Fatura")]
    public int InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    /// <summary>For settled payments: the payment date. For planned installments: the due date.</summary>
    [Display(Name = "Ödeme Tarihi")]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Tutar")]
    public decimal Amount { get; set; }

    [Display(Name = "Ödeme Yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(300)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Planlı Ödeme")]
    public bool IsPlanned { get; set; }

    [Display(Name = "Tahsil Edildi")]
    public bool IsSettled { get; set; }

    [Display(Name = "Tahsil Tarihi")]
    public DateOnly? SettledDate { get; set; }

    [Display(Name = "Taksit No")]
    public int? InstallmentNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsOverdue => IsPlanned && !IsSettled && PaymentDate < DateOnly.FromDateTime(DateTime.Today);
}
