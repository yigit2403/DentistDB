using System.ComponentModel.DataAnnotations;

namespace DentistDB.ViewModels;

public class PatientFormViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(150)]
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

    [Display(Name = "Arşivlendi")]
    public bool IsArchived { get; set; }
}
