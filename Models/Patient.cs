using System.ComponentModel.DataAnnotations;

namespace DentistDB.Models;

public class Patient
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

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

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<TreatmentRecord> TreatmentRecords { get; set; } = new List<TreatmentRecord>();
    public ICollection<Scan> Scans { get; set; } = new List<Scan>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
