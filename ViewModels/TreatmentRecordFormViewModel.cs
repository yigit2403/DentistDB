using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class TreatmentRecordFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    [Display(Name = "Tanı")]
    public string? Diagnosis { get; set; }

    [MaxLength(500)]
    [Display(Name = "Uygulanan İşlemler")]
    public string? Procedures { get; set; }

    [MaxLength(500)]
    [Display(Name = "Reçeteler")]
    public string? Prescriptions { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}
