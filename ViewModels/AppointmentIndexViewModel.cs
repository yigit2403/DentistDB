using DentistDB.Models;

namespace DentistDB.ViewModels;

public enum AppointmentCalendarView
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public class AppointmentIndexViewModel
{
    public string? StatusFilter { get; set; }
    public int? PatientId { get; set; }
    public DateTime ReferenceDate { get; set; } = DateTime.Today;
    public AppointmentCalendarView CalendarView { get; set; } = AppointmentCalendarView.Daily;
    public IList<AppointmentBucketViewModel> Buckets { get; set; } = new List<AppointmentBucketViewModel>();
    public AppointmentStatus[] Statuses { get; set; } = Array.Empty<AppointmentStatus>();
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PreviousDate { get; set; }
    public DateTime NextDate { get; set; }
}

public class AppointmentBucketViewModel
{
    public DateTime Start { get; set; }
    public string Label { get; set; } = string.Empty;
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();
}
