using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Http;

namespace DentistDB.ViewModels;

public class ScanUploadViewModel
{
    [Required]
    [Display(Name = "Hasta")]
    public int PatientId { get; set; }

    [Required]
    [Display(Name = "Tarama Dosyası")]
    public IFormFile? File { get; set; }

    [Display(Name = "Tarama Türü")]
    public ScanType ScanType { get; set; } = ScanType.Other;

    [Display(Name = "Tarama Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }
}
