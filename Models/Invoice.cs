using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentistDB.Models;

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Cancelled
}

public class Invoice
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Display(Name = "Invoice Date")]
    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Due Date")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Total Amount")]
    [Range(0, 999999.99)]
    public decimal TotalAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [NotMapped]
    public decimal TotalPaid => Payments.Sum(p => p.Amount);

    [NotMapped]
    public decimal Balance => TotalAmount - TotalPaid;
}
