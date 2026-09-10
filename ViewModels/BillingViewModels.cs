using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class InvoiceFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Display(Name = "Fatura Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Display(Name = "Durum")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500, ErrorMessage = "Notlar en fazla 500 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public List<InvoiceItemInputModel> Items { get; set; } = new();

    [Display(Name = "Taksitli ödeme planı oluştur")]
    public bool EnablePaymentPlan { get; set; }

    [Display(Name = "İlk Taksit Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? FirstPaymentDate { get; set; }

    [Display(Name = "Taksit Sayısı")]
    [Range(1, 48, ErrorMessage = "Taksit sayısı 1 ile 48 arasında olmalıdır.")]
    public int InstallmentCount { get; set; } = 1;

    [Display(Name = "Taksit Aralığı (Ay)")]
    [Range(1, 12, ErrorMessage = "Aralık 1 ile 12 ay arasında olmalıdır.")]
    public int InstallmentIntervalMonths { get; set; } = 1;

    public bool PlanLocked { get; set; }

    /// <summary>Comma-separated treatment plan item ids this invoice bills (linked after save).</summary>
    public string? PlanItemIds { get; set; }
    public IList<Payment> ExistingPlannedPayments { get; set; } = new List<Payment>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
    public IReadOnlyList<Procedure> Procedures { get; set; } = Array.Empty<Procedure>();
    public string? PatientName { get; set; }

    public decimal Total => Items.Where(i => !i.IsEmpty).Sum(i => i.Quantity * i.UnitPrice);
}

public class InvoiceItemInputModel
{
    public int Id { get; set; }

    public int? ProcedureId { get; set; }

    [MaxLength(200, ErrorMessage = "İşlem adı en fazla 200 karakter olabilir.")]
    [Display(Name = "İşlem")]
    public string? Description { get; set; }

    [MaxLength(100, ErrorMessage = "Diş bilgisi en fazla 100 karakter olabilir.")]
    [Display(Name = "Diş")]
    public string? ToothNumbers { get; set; }

    [Range(1, 999, ErrorMessage = "Adet 1 ile 999 arasında olmalıdır.")]
    [Display(Name = "Adet")]
    public int Quantity { get; set; } = 1;

    [Range(0, 9999999.99, ErrorMessage = "Birim fiyat negatif olamaz.")]
    [Display(Name = "Birim Fiyat")]
    public decimal UnitPrice { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Description) && UnitPrice == 0 && ProcedureId is null;
}

public class PaymentFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Fatura")]
    public int InvoiceId { get; set; }

    [Display(Name = "Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.01, 9999999.99, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    [Display(Name = "Tutar")]
    public decimal Amount { get; set; }

    [Display(Name = "Ödeme Yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(300, ErrorMessage = "Notlar en fazla 300 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Taksit")]
    public int? PlannedPaymentId { get; set; }

    public IList<SelectListItem> PlannedPayments { get; set; } = new List<SelectListItem>();
    public Invoice? Invoice { get; set; }
}

public class BillingIndexViewModel
{
    public InvoiceStatus? StatusFilter { get; set; }
    public bool OnlyOverdue { get; set; }
    public string? Search { get; set; }
    public int? PatientId { get; set; }
    public PaginatedList<Invoice> Invoices { get; set; } = new(Enumerable.Empty<Invoice>(), 0, 1, 25);
    public bool ShowFinancials { get; set; }

    public decimal OutstandingTotal { get; set; }
    public decimal OverdueTotal { get; set; }
    public decimal CollectedThisMonth { get; set; }
    public int OpenInvoiceCount { get; set; }
}
