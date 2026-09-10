using System.ComponentModel.DataAnnotations;
using DentistDB.Models;

namespace DentistDB.ViewModels;

public class ScanUploadViewModel
{
    [Required]
    public int PatientId { get; set; }

    [Display(Name = "Dosya")]
    public IFormFile? File { get; set; }

    [Display(Name = "Görüntü Türü")]
    public ScanType ScanType { get; set; } = ScanType.Panoramic;

    [Display(Name = "Çekim Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500, ErrorMessage = "Notlar en fazla 500 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public string? PatientName { get; set; }
}

public class ScanEditViewModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }

    [Display(Name = "Görüntü Türü")]
    public ScanType ScanType { get; set; }

    [Display(Name = "Çekim Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; }

    [MaxLength(500, ErrorMessage = "Notlar en fazla 500 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string? PatientName { get; set; }
}
