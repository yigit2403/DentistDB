using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class InvoiceFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Display(Name = "Fatura Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Required]
    [Display(Name = "Toplam Tutar")]
    [Range(0.01, 999999.99, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Durum")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Ödeme Planı Aktif")]
    public bool EnablePaymentPlan { get; set; }

    [Display(Name = "İlk Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? FirstPaymentDate { get; set; }

    [Display(Name = "Taksit Sayısı")]
    [Range(1, 48, ErrorMessage = "Taksit sayısı 1 ile 48 arasında olmalıdır.")]
    public int InstallmentCount { get; set; } = 1;

    [Display(Name = "Periyot (Ay)")]
    [Range(1, 12, ErrorMessage = "Periyot 1 ile 12 ay arasında olmalıdır.")]
    public int InstallmentIntervalMonths { get; set; } = 1;

    public IList<PlannedPaymentViewModel> ExistingPlannedPayments { get; set; } = new List<PlannedPaymentViewModel>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
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

    [Required]
    [Display(Name = "Tutar")]
    [Range(0.01, 999999.99, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal Amount { get; set; }

    [Display(Name = "Ödeme Yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(300)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Planlı Taksit")]
    public int? PlannedPaymentId { get; set; }

    public IList<SelectListItem> PlannedPayments { get; set; } = new List<SelectListItem>();
}

public class PlannedPaymentViewModel
{
    public int Id { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsSettled { get; set; }
    public int? InstallmentNumber { get; set; }
}
