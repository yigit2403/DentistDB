using DentistDB.Models;

namespace DentistDB.Services;

public static class AppointmentRules
{
    /// <summary>Two appointments conflict when their [start, end) intervals intersect.</summary>
    public static bool Overlaps(DateTime startA, int durationA, DateTime startB, int durationB)
    {
        var endA = startA.AddMinutes(durationA);
        var endB = startB.AddMinutes(durationB);
        return startA < endB && startB < endA;
    }

    /// <summary>Only live appointments block the calendar; cancelled and no-show slots are free again.</summary>
    public static bool BlocksCalendar(AppointmentStatus status)
    {
        return status is AppointmentStatus.Scheduled or AppointmentStatus.Completed;
    }

    public static DateTime RoundToSlot(DateTime value, int slotMinutes = 5)
    {
        var ticks = TimeSpan.FromMinutes(slotMinutes).Ticks;
        return new DateTime((value.Ticks + ticks / 2) / ticks * ticks, value.Kind);
    }
}
