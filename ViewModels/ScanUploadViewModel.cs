using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using Microsoft.AspNetCore.Http;

namespace DentistDB.ViewModels;

public class ScanUploadViewModel
{
    [Required]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [Required]
    [Display(Name = "Scan File")]
    public IFormFile? File { get; set; }

    [Display(Name = "Scan Type")]
    public ScanType ScanType { get; set; } = ScanType.Other;

    [Display(Name = "Scan Date")]
    [DataType(DataType.Date)]
    public DateOnly ScanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(500)]
    public string? Notes { get; set; }
}
