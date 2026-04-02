using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Required(ErrorMessage = "Randevu tarihi zorunludur.")]
    [Display(Name = "Tarih ve Saat")]
    public DateTime AppointmentDate { get; set; } = DateTime.Today.AddHours(9);

    [MaxLength(200, ErrorMessage = "Randevu nedeni en fazla 200 karakter olabilir.")]
    [Display(Name = "Randevu Nedeni")]
    public string? Purpose { get; set; }

    [Display(Name = "Durum")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    [MaxLength(1000, ErrorMessage = "Notlar en fazla 1000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Mevcut Fatura")]
    public int? InvoiceId { get; set; }

    [Display(Name = "Yeni Fatura Oluştur")]
    public bool CreateInvoice { get; set; }

    [Display(Name = "Fatura Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Display(Name = "Toplam Tutar")]
    [Range(0.01, 999999.99, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal? InvoiceTotalAmount { get; set; }

    [Display(Name = "Fatura Notları")]
    [MaxLength(500, ErrorMessage = "Fatura notları en fazla 500 karakter olabilir.")]
    public string? InvoiceNotes { get; set; }

    [Display(Name = "Taksitli Ödeme")]
    public bool EnableInstallments { get; set; }

    [Display(Name = "İlk Taksit Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? FirstInstallmentDate { get; set; }

    [Display(Name = "Taksit Sayısı")]
    [Range(1, 48, ErrorMessage = "Taksit sayısı 1 ile 48 arasında olmalıdır.")]
    public int InstallmentCount { get; set; } = 1;

    [Display(Name = "Periyot (Ay)")]
    [Range(1, 12, ErrorMessage = "Periyot 1 ile 12 ay arasında olmalıdır.")]
    public int InstallmentIntervalMonths { get; set; } = 1;

    public IReadOnlyList<string> SelectedTeeth { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PurposeSuggestions { get; set; } = Array.Empty<string>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
    public IList<AppointmentInvoiceOptionViewModel> Invoices { get; set; } = new List<AppointmentInvoiceOptionViewModel>();
}

public class AppointmentInvoiceOptionViewModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string Label { get; set; } = string.Empty;
}
