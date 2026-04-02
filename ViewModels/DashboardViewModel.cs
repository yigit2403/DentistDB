using DentistDB.Models;

namespace DentistDB.ViewModels;

public class DashboardViewModel
{
    public IList<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
    public IList<Payment> UpcomingInstallments { get; set; } = new List<Payment>();
    public IList<PreviousOperation> RecentOperations { get; set; } = new List<PreviousOperation>();
    public IList<Patient> RecentPatients { get; set; } = new List<Patient>();
    public IList<Invoice> UnpaidInvoices { get; set; } = new List<Invoice>();
}
