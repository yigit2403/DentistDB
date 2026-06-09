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

    [Display(Name = "Geliş Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? ArrivalDate { get; set; }

    [MaxLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(500)]
    [Display(Name = "Tıbbi Uyarılar")]
    public string? MedicalAlerts { get; set; }

    [Display(Name = "Hasta Fotoğrafı")]
    public string? PhotoBase64 { get; set; }

    [MaxLength(100)]
    public string? PhotoContentType { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<PreviousOperation> PreviousOperations { get; set; } = new List<PreviousOperation>();
    public ICollection<Scan> Scans { get; set; } = new List<Scan>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
