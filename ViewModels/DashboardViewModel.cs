using DentistDB.Models;

namespace DentistDB.ViewModels;

public class DashboardViewModel
{
    public IList<Appointment> TodaysAppointments { get; set; } = new List<Appointment>();
    public IList<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
    public IList<Payment> UpcomingPaymentPlanItems { get; set; } = new List<Payment>();
    public IList<PreviousOperation> RecentOperations { get; set; } = new List<PreviousOperation>();
    public IList<Patient> RecentPatients { get; set; } = new List<Patient>();
    public int TotalPatients { get; set; }
    public int AppointmentsThisMonth { get; set; }
}
