using DentistDB.Models;
using DentistDB.ViewModels;
using Xunit;

namespace DentistDB.Tests.Unit;

public class TimeGridLayoutTests
{
    private static readonly DateTime Day = new(2026, 9, 17);

    private static Appointment At(int hour, int minute, int duration, int id = 0, AppointmentStatus status = AppointmentStatus.Scheduled) => new()
    {
        Id = id,
        AppointmentDate = Day.AddHours(hour).AddMinutes(minute),
        DurationMinutes = duration,
        Status = status
    };

    [Fact]
    public void Create_CoversWorkingHoursInWholeSlots()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.Equal(TimeSpan.FromHours(9), grid.Start);
        Assert.Equal(TimeSpan.FromHours(18), grid.End);
        Assert.Equal(36, grid.RowCount);
        Assert.Equal(9, grid.Hours.Count());
        Assert.True(grid.IsWorkingRow(1));
        Assert.True(grid.IsHourRow(5));
        Assert.False(grid.IsHourRow(2));
    }

    [Fact]
    public void Create_WidensToWholeHoursAroundOutOfHoursAppointments()
    {
        var grid = TimeGridLayout.Create(new[] { At(7, 45, 30), At(18, 30, 45) }, new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.Equal(TimeSpan.FromHours(7), grid.Start);
        Assert.Equal(TimeSpan.FromHours(20), grid.End);
        Assert.False(grid.IsWorkingRow(1));
        Assert.True(grid.IsWorkingRow(grid.RowOf(TimeSpan.FromHours(9))));
        Assert.False(grid.IsWorkingRow(grid.RowOf(TimeSpan.FromHours(18))));
    }

    [Fact]
    public void Create_NeverRunsPastMidnight()
    {
        var grid = TimeGridLayout.Create(new[] { At(23, 30, 90) }, new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.Equal(TimeSpan.FromHours(24), grid.End);
    }

    [Fact]
    public void Place_MapsStartAndDurationToRows()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        var placement = Assert.Single(grid.Place(new[] { At(10, 30, 45) }));

        Assert.Equal(7, placement.RowStart);   // 10:30 is 90 minutes in → row 7
        Assert.Equal(3, placement.RowSpan);    // 45 minutes → 3 slots
        Assert.Equal(0, placement.Lane);
        Assert.Equal(1, placement.LaneCount);
        Assert.False(placement.IsShort);
    }

    [Fact]
    public void Place_RoundsOddDurationsUpAndKeepsAtLeastOneRow()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        var placements = grid.Place(new[] { At(9, 5, 5, 1), At(9, 20, 20, 2) });

        Assert.Equal(1, placements[0].RowSpan);
        Assert.True(placements[0].IsShort);
        Assert.Equal(2, placements[1].RowStart);
        Assert.Equal(2, placements[1].RowSpan);   // 09:20–09:40 touches rows 2 and 3
    }

    [Fact]
    public void Place_PutsOverlappingAppointmentsInSeparateLanes()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        var placements = grid.Place(new[]
        {
            At(9, 0, 60, 1),
            At(9, 30, 30, 2),
            At(9, 30, 30, 3),
            At(11, 0, 30, 4)
        }).ToDictionary(p => p.Appointment.Id);

        Assert.Equal(0, placements[1].Lane);
        Assert.Equal(1, placements[2].Lane);
        Assert.Equal(2, placements[3].Lane);
        Assert.All(new[] { 1, 2, 3 }, id => Assert.Equal(3, placements[id].LaneCount));

        // The 11:00 appointment does not overlap the cluster, so it gets the full width again.
        Assert.Equal(0, placements[4].Lane);
        Assert.Equal(1, placements[4].LaneCount);
    }

    [Fact]
    public void Place_ReusesAFreedLane()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        var placements = grid.Place(new[]
        {
            At(9, 0, 30, 1),
            At(9, 0, 90, 2),
            At(9, 30, 30, 3)
        }).ToDictionary(p => p.Appointment.Id);

        // Longer appointments are placed first (leftmost); #1 ends at 09:30 so #3 takes its lane
        // instead of opening a third one.
        Assert.Equal(0, placements[2].Lane);
        Assert.Equal(1, placements[1].Lane);
        Assert.Equal(placements[1].Lane, placements[3].Lane);
        Assert.All(placements.Values, p => Assert.Equal(2, p.LaneCount));
    }

    [Fact]
    public void Place_ClampsAppointmentsToTheGrid()
    {
        var grid = TimeGridLayout.Create(new[] { At(23, 30, 60) }, new TimeOnly(9, 0), new TimeOnly(18, 0));

        var placement = Assert.Single(grid.Place(new[] { At(23, 30, 60) }));

        Assert.Equal(grid.RowCount - 1, placement.RowStart);
        Assert.Equal(2, placement.RowSpan);
        Assert.Equal(grid.RowCount + 1, placement.RowStart + placement.RowSpan);
    }

    [Fact]
    public void SlotStart_ReturnsClickTargetForARow()
    {
        var grid = TimeGridLayout.Create(Array.Empty<Appointment>(), new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.Equal(Day.AddHours(9), grid.SlotStart(Day, 1));
        Assert.Equal(Day.AddHours(13).AddMinutes(45), grid.SlotStart(Day, 20));
    }
}
