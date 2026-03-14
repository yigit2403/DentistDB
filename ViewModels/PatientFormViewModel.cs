using System.ComponentModel.DataAnnotations;

namespace DentistDB.ViewModels;

public class PatientFormViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(150)]
    public string? Email { get; set; }

    [Display(Name = "Date of Birth")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    [Display(Name = "Medical Alerts")]
    public string? MedicalAlerts { get; set; }

    public bool IsArchived { get; set; }
}
