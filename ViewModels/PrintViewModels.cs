using DentistDB.Models;
using DentistDB.Services;

namespace DentistDB.ViewModels;

public class PrescriptionPrintViewModel
{
    public PreviousOperation Operation { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public string ClinicName { get; set; } = string.Empty;
    public ClinicIdentity Identity { get; set; } = null!;
}

public class DaySheetViewModel
{
    public DateTime Date { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class DailyReportViewModel
{
    public DateOnly Date { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public IList<Payment> Payments { get; set; } = new List<Payment>();
    public IList<Invoice> InvoicesIssued { get; set; } = new List<Invoice>();
    public IList<Payment> DueInstallments { get; set; } = new List<Payment>();
    public int AppointmentsCompleted { get; set; }
    public int AppointmentsNoShow { get; set; }
    public int AppointmentsTotal { get; set; }

    public decimal TotalCollected => Payments.Sum(p => p.Amount);
    public decimal TotalIssued => InvoicesIssued.Sum(i => i.TotalAmount);
    public IEnumerable<(PaymentMethod Method, decimal Total, int Count)> ByMethod => Payments
        .GroupBy(p => p.PaymentMethod)
        .Select(g => (g.Key, g.Sum(p => p.Amount), g.Count()))
        .OrderByDescending(x => x.Item2);
}

public class AuditLogViewModel
{
    public string? Search { get; set; }
    public string? Account { get; set; }
    public string? EntityType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PaginatedList<AuditEntry> Entries { get; set; } = new(Enumerable.Empty<AuditEntry>(), 0, 1, 50);
    public IReadOnlyList<string> EntityTypes { get; set; } = Array.Empty<string>();
}
