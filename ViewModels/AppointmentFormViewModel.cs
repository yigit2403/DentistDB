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

    public IReadOnlyList<string> SelectedTeeth { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PurposeSuggestions { get; set; } = Array.Empty<string>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}
