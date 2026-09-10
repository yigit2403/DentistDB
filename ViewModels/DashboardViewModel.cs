using DentistDB.Models;

namespace DentistDB.ViewModels;

public class DashboardViewModel
{
    public DateTime Today { get; set; } = DateTime.Today;
    public IList<Appointment> TodaysAppointments { get; set; } = new List<Appointment>();
    public IList<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
    public IList<Payment> DuePaymentPlanItems { get; set; } = new List<Payment>();
    public IList<Invoice> OverdueInvoices { get; set; } = new List<Invoice>();
    public IList<PreviousOperation> RecentOperations { get; set; } = new List<PreviousOperation>();
    public IList<Patient> RecentPatients { get; set; } = new List<Patient>();

    public int TotalPatients { get; set; }
    public int AppointmentsThisWeek { get; set; }
    public int AppointmentsThisMonth { get; set; }
    public int NewPatientsThisMonth { get; set; }

    public decimal OutstandingBalance { get; set; }
    public decimal CollectedThisMonth { get; set; }
    public bool ShowFinancials { get; set; }

    public int TodayCompleted => TodaysAppointments.Count(a => a.Status == AppointmentStatus.Completed);
    public int TodayRemaining => TodaysAppointments.Count(a => a.Status == AppointmentStatus.Scheduled);
}
