using DentistDB.Models;

namespace DentistDB.ViewModels;

public enum AppointmentCalendarView
{
    Daily,
    Weekly,
    Monthly,
    List
}

public class AppointmentIndexViewModel
{
    public AppointmentStatus? StatusFilter { get; set; }
    public int? PatientId { get; set; }
    public string? PatientName { get; set; }
    public DateTime ReferenceDate { get; set; } = DateTime.Today;
    public AppointmentCalendarView CalendarView { get; set; } = AppointmentCalendarView.Daily;

    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PreviousDate { get; set; }
    public DateTime NextDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>All appointments in the period, ordered by start time.</summary>
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();

    /// <summary>Calendar cells: one per day in the period (Monthly includes leading/trailing days).</summary>
    public IList<CalendarDayViewModel> Days { get; set; } = new List<CalendarDayViewModel>();

    public TimeOnly DayStart { get; set; } = new(9, 0);
    public TimeOnly DayEnd { get; set; } = new(18, 0);

    public int TotalCount => Appointments.Count;
    public int ScheduledCount => Appointments.Count(a => a.Status == AppointmentStatus.Scheduled);
    public int CompletedCount => Appointments.Count(a => a.Status == AppointmentStatus.Completed);
}

public class CalendarDayViewModel
{
    public DateTime Date { get; set; }
    public bool IsCurrentPeriod { get; set; } = true;
    public bool IsToday => Date.Date == DateTime.Today;
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();
}
