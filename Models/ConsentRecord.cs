using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum ConsentType
{
    [Display(Name = "Tedavi Onam Formu")]
    Treatment,
    [Display(Name = "KVKK Aydınlatma ve Açık Rıza")]
    Kvkk
}

/// <summary>Records that a printed consent form was signed by the patient.</summary>
public class ConsentRecord
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    public ConsentType Type { get; set; }

    public DateOnly SignedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
