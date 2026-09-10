using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Required(ErrorMessage = "Tarih zorunludur.")]
    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Saat zorunludur.")]
    [Display(Name = "Saat")]
    [DataType(DataType.Time)]
    public TimeOnly Time { get; set; } = new(9, 0);

    [Range(5, 600, ErrorMessage = "Süre 5 ile 600 dakika arasında olmalıdır.")]
    [Display(Name = "Süre")]
    public int DurationMinutes { get; set; } = Appointment.DefaultDurationMinutes;

    [Required(ErrorMessage = "Randevu nedeni zorunludur.")]
    [MaxLength(200, ErrorMessage = "Randevu nedeni en fazla 200 karakter olabilir.")]
    [Display(Name = "Randevu Nedeni")]
    public string? Purpose { get; set; }

    [Display(Name = "Durum")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    [MaxLength(1000, ErrorMessage = "Notlar en fazla 1000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Çakışmaya rağmen kaydet")]
    public bool IgnoreConflicts { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>Treatment plan item this appointment is scheduled for (optional).</summary>
    public int? PlanItemId { get; set; }

    [Range(0, 24, ErrorMessage = "En fazla 24 tekrar.")]
    [Display(Name = "Tekrar Sayısı")]
    public int RepeatCount { get; set; }

    [Display(Name = "Tekrar Aralığı")]
    public int RepeatIntervalDays { get; set; } = 7;

    /// <summary>Comma-separated FDI tooth numbers posted by the tooth selector.</summary>
    public string? SelectedTeeth { get; set; }
    public IReadOnlyList<string> PurposeSuggestions { get; set; } = Array.Empty<string>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
    public IReadOnlyList<Appointment> Conflicts { get; set; } = Array.Empty<Appointment>();
    public string? PatientName { get; set; }

    public DateTime StartDateTime => Date.ToDateTime(Time);
}
