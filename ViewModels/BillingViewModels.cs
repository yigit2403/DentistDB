using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class InvoiceFormViewModel
{
    public int Id { get; set; }

    [Required]
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
}
