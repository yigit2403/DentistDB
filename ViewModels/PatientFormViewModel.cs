using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DentistDB.ViewModels;

public class PatientFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "Ad soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
    [MaxLength(20, ErrorMessage = "Telefon en fazla 20 karakter olabilir.")]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "TCKN zorunludur.")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "TCKN 11 haneli olmalıdır.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "TCKN 11 haneli olmalıdır.")]
    [Display(Name = "TCKN")]
    public string Tckn { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [MaxLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "Geliş Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? ArrivalDate { get; set; }

    [MaxLength(300, ErrorMessage = "Adres en fazla 300 karakter olabilir.")]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(1000, ErrorMessage = "Notlar en fazla 1000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(500, ErrorMessage = "Tıbbi uyarılar en fazla 500 karakter olabilir.")]
    [Display(Name = "Tıbbi Uyarılar")]
    public string? MedicalAlerts { get; set; }

    [Display(Name = "Hasta Fotoğrafı")]
    public IFormFile? PhotoFile { get; set; }

    public string? ExistingPhotoBase64 { get; set; }
    public string? ExistingPhotoContentType { get; set; }

    [Display(Name = "Arşivlendi")]
    public bool IsArchived { get; set; }
}
