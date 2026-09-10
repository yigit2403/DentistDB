using DentistDB.Controllers;
using DentistDB.Models;
using DentistDB.Services;
using Xunit;

namespace DentistDB.Tests.Unit;

public class TailscaleStatusParsingTests
{
    [Fact]
    public void ParseStatus_ReadsRunningNode()
    {
        const string json = """
            {"BackendState":"Running","Self":{"DNSName":"klinik.tail1234.ts.net.","TailscaleIPs":["100.101.102.103","fd7a::1"]}}
            """;

        var (installed, online, url, host, ips, error) = ConnectionInfoService.ParseStatus(json);

        Assert.True(installed);
        Assert.True(online);
        Assert.Equal("https://klinik.tail1234.ts.net", url);
        Assert.Equal("klinik.tail1234.ts.net", host);
        Assert.Equal("100.101.102.103", ips[0]);
        Assert.Null(error);
    }

    [Fact]
    public void ParseStatus_ExplainsNeedsLogin()
    {
        var (installed, online, url, _, _, error) = ConnectionInfoService.ParseStatus("""{"BackendState":"NeedsLogin","Self":{"DNSName":""}}""");

        Assert.True(installed);
        Assert.False(online);
        Assert.Null(url);
        Assert.Contains("giriş", error!);
    }

    [Fact]
    public void ParseStatus_ToleratesGarbage()
    {
        var (installed, online, _, _, _, error) = ConnectionInfoService.ParseStatus("not json");
        Assert.True(installed);
        Assert.False(online);
        Assert.NotNull(error);
    }
}

public class CalendarFeedTests
{
    [Fact]
    public void BuildCalendar_ProducesValidEventsWithEscaping()
    {
        var appointment = new Appointment
        {
            Id = 7,
            AppointmentDate = new DateTime(2026, 9, 12, 15, 30, 0),
            DurationMinutes = 60,
            Purpose = "Dolgu, kontrol",
            SelectedTeethData = "16,17",
            Notes = "Anestezi; dikkat",
            Patient = new Patient { FullName = "Ayşe Yılmaz", Phone = "0532 111 22 33", Allergies = "Penisilin" }
        };

        var ics = CalendarController.BuildCalendar("Gülüş Diş", new[] { appointment }, new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.Contains("X-WR-CALNAME:Gülüş Diş Randevular", ics);
        Assert.Contains("UID:dentistdb-appointment-7@", ics);
        Assert.Contains("DTSTART;TZID=Europe/Istanbul:20260912T153000", ics);
        Assert.Contains("DTEND;TZID=Europe/Istanbul:20260912T163000", ics);
        Assert.Contains("SUMMARY:Ayşe Yılmaz – Dolgu\\, kontrol", ics);
        Assert.Contains("Diş 16\\,17", ics);
        Assert.Contains("Anestezi\\; dikkat", ics);
        Assert.Contains("⚠ Tıbbi uyarı", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }

    [Fact]
    public void NewToken_IsLongAndRandom()
    {
        var a = CalendarController.NewToken();
        var b = CalendarController.NewToken();
        Assert.Equal(40, a.Length);
        Assert.NotEqual(a, b);
    }
}
