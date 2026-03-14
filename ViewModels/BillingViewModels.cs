using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class InvoiceFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [Display(Name = "Invoice Date")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Due Date")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Required]
    [Display(Name = "Total Amount")]
    [Range(0.01, 999999.99, ErrorMessage = "Amount must be greater than zero.")]
    public decimal TotalAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class PaymentFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Invoice")]
    public int InvoiceId { get; set; }

    [Display(Name = "Payment Date")]
    [DataType(DataType.Date)]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    [Range(0.01, 999999.99, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment Method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(300)]
    public string? Notes { get; set; }
}
