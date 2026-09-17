using DentistDB.Models;

namespace DentistDB.ViewModels;

/// <summary>Where one appointment sits inside a <see cref="TimeGridLayout"/>: which rows and which side-by-side lane.</summary>
public sealed class TimeGridPlacement
{
    public required Appointment Appointment { get; init; }

    /// <summary>1-based CSS grid row of the first slot the appointment touches.</summary>
    public int RowStart { get; init; }

    /// <summary>Number of slot rows the appointment covers (always at least 1).</summary>
    public int RowSpan { get; init; }

    /// <summary>0-based lane inside its overlap cluster.</summary>
    public int Lane { get; init; }

    /// <summary>Total lanes in the overlap cluster; 1 when nothing overlaps.</summary>
    public int LaneCount { get; init; }

    public bool IsShort => RowSpan <= 1;
}

public sealed record TimeGridHour(TimeSpan Time, int RowStart, int RowSpan)
{
    public string Label => $"{(int)Time.TotalHours:00}:{Time.Minutes:00}";
}

/// <summary>
/// Row geometry for the day/week time grid. The grid always covers the clinic's working hours and
/// widens (in whole hours) when an appointment falls outside them, so nothing is ever hidden.
/// </summary>
public sealed class TimeGridLayout
{
    public const int DefaultSlotMinutes = 15;

    /// <summary>Number of CSS grid tracks per day column; divisible by 1–4 so up to four lanes split evenly.</summary>
    public const int LaneTracks = 12;

    private TimeGridLayout(TimeSpan start, TimeSpan end, TimeSpan workStart, TimeSpan workEnd, int slotMinutes)
    {
        Start = start;
        End = end;
        WorkStart = workStart;
        WorkEnd = workEnd;
        SlotMinutes = slotMinutes;
    }

    public TimeSpan Start { get; }
    public TimeSpan End { get; }
    public TimeSpan WorkStart { get; }
    public TimeSpan WorkEnd { get; }
    public int SlotMinutes { get; }

    public int RowCount => (int)Math.Ceiling((End - Start).TotalMinutes / SlotMinutes);
    public int SlotsPerHour => 60 / SlotMinutes;

    public IEnumerable<TimeGridHour> Hours
    {
        get
        {
            for (var t = Start; t < End; t += TimeSpan.FromHours(1))
            {
                var rowStart = RowOf(t);
                var rowEnd = Math.Min(RowCount + 1, RowOf(t + TimeSpan.FromHours(1)));
                yield return new TimeGridHour(t, rowStart, Math.Max(1, rowEnd - rowStart));
            }
        }
    }

    public static TimeGridLayout Create(IEnumerable<Appointment> appointments, TimeOnly dayStart, TimeOnly dayEnd, int slotMinutes = DefaultSlotMinutes)
    {
        if (slotMinutes <= 0 || 60 % slotMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotMinutes), "Slot length must divide an hour.");
        }

        var workStart = dayStart.ToTimeSpan();
        var workEnd = dayEnd.ToTimeSpan();
        if (workEnd <= workStart)
        {
            workEnd = workStart + TimeSpan.FromHours(1);
        }

        var start = workStart;
        var end = workEnd;
        foreach (var a in appointments)
        {
            var s = a.AppointmentDate.TimeOfDay;
            var e = s + TimeSpan.FromMinutes(Math.Max(1, a.DurationMinutes));
            if (s < start) start = s;
            if (e > end) end = e;
        }

        start = TimeSpan.FromHours(Math.Floor(start.TotalHours));
        end = TimeSpan.FromHours(Math.Min(24, Math.Ceiling(end.TotalHours)));
        if (end <= start)
        {
            end = start + TimeSpan.FromHours(1);
        }

        return new TimeGridLayout(start, end, workStart, workEnd, slotMinutes);
    }

    /// <summary>1-based row whose top edge is at <paramref name="time"/> (rounded down to the slot).</summary>
    public int RowOf(TimeSpan time)
    {
        var minutes = (time - Start).TotalMinutes;
        return (int)Math.Floor(minutes / SlotMinutes) + 1;
    }

    public TimeSpan TimeOfRow(int row) => Start + TimeSpan.FromMinutes((row - 1) * SlotMinutes);

    public DateTime SlotStart(DateTime date, int row) => date.Date + TimeOfRow(row);

    public bool IsWorkingRow(int row)
    {
        var t = TimeOfRow(row);
        return t >= WorkStart && t < WorkEnd;
    }

    public bool IsHourRow(int row) => TimeOfRow(row).Minutes == 0;

    /// <summary>
    /// Assigns rows and lanes to one day's appointments. Appointments whose rows overlap are
    /// placed in separate lanes; each overlap cluster reports how many lanes it needs so the
    /// view can split the column evenly.
    /// </summary>
    public IReadOnlyList<TimeGridPlacement> Place(IEnumerable<Appointment> dayAppointments)
    {
        var ordered = dayAppointments
            .OrderBy(a => a.AppointmentDate)
            .ThenByDescending(a => a.DurationMinutes)
            .ThenBy(a => a.Id)
            .ToList();

        var result = new List<TimeGridPlacement>(ordered.Count);
        var cluster = new List<(Appointment Appt, int RowStart, int RowSpan, int Lane)>();
        var laneEnds = new List<int>(); // exclusive end row per lane in the current cluster
        var clusterEnd = int.MinValue;

        void Flush()
        {
            var laneCount = Math.Max(1, laneEnds.Count);
            foreach (var item in cluster)
            {
                result.Add(new TimeGridPlacement
                {
                    Appointment = item.Appt,
                    RowStart = item.RowStart,
                    RowSpan = item.RowSpan,
                    Lane = item.Lane,
                    LaneCount = laneCount
                });
            }
            cluster.Clear();
            laneEnds.Clear();
        }

        foreach (var a in ordered)
        {
            var rowStart = Math.Clamp(RowOf(a.AppointmentDate.TimeOfDay), 1, RowCount);
            var endMinutes = (a.AppointmentDate.TimeOfDay - Start).TotalMinutes + Math.Max(1, a.DurationMinutes);
            var rowEndExclusive = Math.Clamp((int)Math.Ceiling(endMinutes / SlotMinutes) + 1, rowStart + 1, RowCount + 1);
            var rowSpan = rowEndExclusive - rowStart;

            if (cluster.Count > 0 && rowStart >= clusterEnd)
            {
                Flush();
            }

            var lane = laneEnds.FindIndex(end => end <= rowStart);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(rowEndExclusive);
            }
            else
            {
                laneEnds[lane] = rowEndExclusive;
            }

            cluster.Add((a, rowStart, rowSpan, lane));
            clusterEnd = cluster.Count == 1 ? rowEndExclusive : Math.Max(clusterEnd, rowEndExclusive);
        }

        Flush();
        return result;
    }
}
