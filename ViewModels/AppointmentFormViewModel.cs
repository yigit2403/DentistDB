using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public enum AppointmentPatientMode
{
    Existing,
    New
}

public enum AppointmentFinanceMode
{
    None,
    PayInFull,
    Installments
}

public enum InstallmentMergeMode
{
    SpreadOverCurrentDates,
    AppendAfterCurrentSchedule
}

public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Display(Name = "Hasta Kayıt Tipi")]
    public AppointmentPatientMode PatientMode { get; set; } = AppointmentPatientMode.Existing;

    [ValidateNever]
    public PatientInputViewModel NewPatient { get; set; } = new();

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

    [Display(Name = "Finans İşlemi")]
    public AppointmentFinanceMode FinanceMode { get; set; }

    [Display(Name = "İşlem Tutarı")]
    [Range(0.01, 999999.99, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal? ChargeAmount { get; set; }

    [Display(Name = "Hesap Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Son Ödeme Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Display(Name = "Finans Notları")]
    [MaxLength(500, ErrorMessage = "Finans notları en fazla 500 karakter olabilir.")]
    public string? InvoiceNotes { get; set; }

    [Display(Name = "Peşin Ödeme Yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Peşin Ödeme Notu")]
    [MaxLength(300, ErrorMessage = "Ödeme notu en fazla 300 karakter olabilir.")]
    public string? PaymentNotes { get; set; }

    [Display(Name = "İlk Taksit Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? FirstInstallmentDate { get; set; }

    [Display(Name = "Taksit Sayısı")]
    [Range(1, 48, ErrorMessage = "Taksit sayısı 1 ile 48 arasında olmalıdır.")]
    public int InstallmentCount { get; set; } = 1;

    [Display(Name = "Periyot (Ay)")]
    [Range(1, 12, ErrorMessage = "Periyot 1 ile 12 ay arasında olmalıdır.")]
    public int InstallmentIntervalMonths { get; set; } = 1;

    [Display(Name = "Taksit Birleştirme Yöntemi")]
    public InstallmentMergeMode? InstallmentMergeMode { get; set; }

    [ValidateNever]
    public IReadOnlyList<string> SelectedTeeth { get; set; } = Array.Empty<string>();
    [ValidateNever]
    public IReadOnlyList<string> PurposeSuggestions { get; set; } = Array.Empty<string>();
    [ValidateNever]
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
    [ValidateNever]
    public IList<AppointmentPatientFinanceSummaryViewModel> PatientFinances { get; set; } = new List<AppointmentPatientFinanceSummaryViewModel>();
}

public class AppointmentPatientFinanceSummaryViewModel
{
    public int PatientId { get; set; }
    public int InvoiceId { get; set; }
    public decimal Balance { get; set; }
    public int UnpaidInstallmentCount { get; set; }
}
