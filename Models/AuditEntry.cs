using System.ComponentModel.DataAnnotations;

namespace DentistDB.Models;

/// <summary>Who did what, when. Written automatically on every SaveChanges plus sign-in events.</summary>
public class AuditEntry
{
    public long Id { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Account { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(60)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public int? PatientId { get; set; }

    [MaxLength(300)]
    public string Summary { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Details { get; set; }
}
