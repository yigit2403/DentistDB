using DentistDB.Models;

namespace DentistDB.ViewModels;

public class DashboardViewModel
{
    public IList<Appointment> TodaysAppointments { get; set; } = new List<Appointment>();
    public IList<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
    public IList<Patient> RecentPatients { get; set; } = new List<Patient>();
    public IList<Invoice> UnpaidInvoices { get; set; } = new List<Invoice>();
    public int TotalPatients { get; set; }
    public int AppointmentsThisMonth { get; set; }
}
