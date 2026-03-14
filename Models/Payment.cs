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
    [Display(Name = "Banka Havalesi")]
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

    [Display(Name = "Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Tutar")]
    [Range(0.01, 999999.99)]
    public decimal Amount { get; set; }

    [Display(Name = "Ödeme Yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(300)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
