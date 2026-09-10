using System.Security.Cryptography;
using System.Text;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

/// <summary>
/// iCalendar feed so the dentist's phone calendar can subscribe to the clinic schedule.
/// Calendar apps cannot sign in with a PIN, so the feed is protected by a long random token
/// that the administrator can rotate from the connection page.
/// </summary>
public class CalendarController : Controller
{
    public const string TokenSettingKey = "Calendar:Token";

    private readonly ApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public CalendarController(ApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Feed(string? token)
    {
        var expected = await _settings.GetAsync(TokenSettingKey);
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(token)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token)))
        {
            return NotFound();
        }

        var clinic = await _settings.GetClinicSettingsAsync();
        var from = DateTime.Today.AddDays(-30);
        var to = DateTime.Today.AddDays(180);

        var appointments = await _db.Appointments.AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.AppointmentDate >= from && a.AppointmentDate < to && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        var ics = BuildCalendar(clinic.ClinicName, appointments, DateTime.UtcNow);
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "randevular.ics");
    }

    public static string BuildCalendar(string clinicName, IEnumerable<Appointment> appointments, DateTime nowUtc)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//DentistDB//Randevular//TR\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        sb.Append($"X-WR-CALNAME:{Escape(clinicName)} Randevular\r\n");
        sb.Append("X-WR-TIMEZONE:Europe/Istanbul\r\n");
        sb.Append("REFRESH-INTERVAL;VALUE=DURATION:PT30M\r\n");
        sb.Append("X-PUBLISHED-TTL:PT30M\r\n");

        foreach (var a in appointments)
        {
            var start = a.AppointmentDate;
            var end = a.EndDate;
            var teeth = string.IsNullOrWhiteSpace(a.SelectedTeethData) ? "" : $" · Diş {a.SelectedTeethData}";
            var status = a.Status switch
            {
                AppointmentStatus.Completed => " (tamamlandı)",
                AppointmentStatus.NoShow => " (gelmedi)",
                _ => ""
            };

            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:dentistdb-appointment-{a.Id}@{Escape(clinicName)}\r\n");
            sb.Append($"DTSTAMP:{nowUtc:yyyyMMdd'T'HHmmss'Z'}\r\n");
            sb.Append($"DTSTART;TZID=Europe/Istanbul:{start:yyyyMMdd'T'HHmmss}\r\n");
            sb.Append($"DTEND;TZID=Europe/Istanbul:{end:yyyyMMdd'T'HHmmss}\r\n");
            sb.Append($"SUMMARY:{Escape($"{a.Patient?.FullName} – {a.Purpose}{status}")}\r\n");
            var description = $"{a.Purpose}{teeth}" +
                (string.IsNullOrWhiteSpace(a.Patient?.Phone) ? "" : $"\nTel: {a.Patient.Phone}") +
                (a.Patient?.HasMedicalRisk == true ? "\n⚠ Tıbbi uyarı" : "") +
                (string.IsNullOrWhiteSpace(a.Notes) ? "" : $"\n{a.Notes}");
            sb.Append($"DESCRIPTION:{Escape(description)}\r\n");
            sb.Append($"LOCATION:{Escape(clinicName)}\r\n");
            sb.Append($"STATUS:{(a.Status == AppointmentStatus.Scheduled ? "CONFIRMED" : "TENTATIVE")}\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    public static string NewToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
    }

    private static string Escape(string text)
    {
        return text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");
    }
}
