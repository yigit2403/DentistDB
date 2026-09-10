using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DentistDB.ViewModels;

public class PreviousOperationFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hasta seçimi zorunludur.")]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "İşlem başlığı zorunludur.")]
    [MaxLength(200, ErrorMessage = "İşlem başlığı en fazla 200 karakter olabilir.")]
    [Display(Name = "İşlem Başlığı")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Tanı en fazla 500 karakter olabilir.")]
    [Display(Name = "Tanı")]
    public string? Diagnosis { get; set; }

    [MaxLength(1000, ErrorMessage = "Uygulanan işlemler en fazla 1000 karakter olabilir.")]
    [Display(Name = "Uygulanan İşlemler")]
    public string? Procedures { get; set; }

    [MaxLength(500, ErrorMessage = "Reçete en fazla 500 karakter olabilir.")]
    [Display(Name = "Reçete")]
    public string? Prescriptions { get; set; }

    [MaxLength(2000, ErrorMessage = "Notlar en fazla 2000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public string? ReturnUrl { get; set; }
    public string? PatientName { get; set; }

    /// <summary>Comma-separated FDI tooth numbers posted by the tooth selector.</summary>
    public string? SelectedTeeth { get; set; }
    public IReadOnlyList<string> TitleSuggestions { get; set; } = Array.Empty<string>();
    public IEnumerable<SelectListItem> Patients { get; set; } = Enumerable.Empty<SelectListItem>();
}
