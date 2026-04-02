using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DentistDB.ViewModels;

public class PatientInputViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required, StringLength(11, MinimumLength = 11)]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "TCKN 11 haneli olmalıdır.")]
    [Display(Name = "TCKN")]
    public string Tckn { get; set; } = string.Empty;

    [EmailAddress, MaxLength(150)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [MaxLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(500)]
    [Display(Name = "Tıbbi Uyarılar")]
    public string? MedicalAlerts { get; set; }
}

public class PatientFormViewModel : PatientInputViewModel
{
    public int Id { get; set; }

    [Display(Name = "Hasta Fotoğrafı")]
    public IFormFile? PhotoFile { get; set; }

    public string? ExistingPhotoBase64 { get; set; }
    public string? ExistingPhotoContentType { get; set; }

    [Display(Name = "Arşivlendi")]
    public bool IsArchived { get; set; }
}
