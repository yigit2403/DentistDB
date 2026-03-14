using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [Required]
    [Display(Name = "Date & Time")]
    public DateTime AppointmentDate { get; set; } = DateTime.Today.AddHours(9);

    [Required, MaxLength(200)]
    public string Purpose { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}
