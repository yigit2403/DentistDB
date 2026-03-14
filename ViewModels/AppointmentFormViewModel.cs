using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Required]
    [Display(Name = "Tarih ve Saat")]
    public DateTime AppointmentDate { get; set; } = DateTime.Today.AddHours(9);

    [Required, MaxLength(200)]
    [Display(Name = "Randevu Nedeni")]
    public string Purpose { get; set; } = string.Empty;

    [Display(Name = "Durum")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}
