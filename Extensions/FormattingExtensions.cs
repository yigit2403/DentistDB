using System.Globalization;
using DentistDB.Models;

namespace DentistDB.Extensions;

public static class FormattingExtensions
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string ToMoney(this decimal value) => value.ToString("C2", Turkish);

    public static string ToShortDate(this DateOnly value) => value.ToString("dd.MM.yyyy", Turkish);
    public static string ToShortDate(this DateOnly? value) => value.HasValue ? value.Value.ToShortDate() : "-";
    public static string ToLongDate(this DateOnly value) => value.ToString("d MMMM yyyy, dddd", Turkish);
    public static string ToShortDate(this DateTime value) => value.ToString("dd.MM.yyyy", Turkish);
    public static string ToDateTimeText(this DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Turkish);
    public static string ToTimeText(this DateTime value) => value.ToString("HH:mm", Turkish);
    public static string ToDayHeading(this DateTime value) => value.ToString("d MMMM yyyy, dddd", Turkish);
    public static string ToMonthHeading(this DateTime value) => value.ToString("MMMM yyyy", Turkish);

    public static string ToDurationText(this int minutes)
    {
        if (minutes < 60) return $"{minutes} dk";
        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0 ? $"{hours} sa" : $"{hours} sa {rest} dk";
    }

    public static string ToBadgeClass(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Completed => "success",
        AppointmentStatus.Cancelled => "neutral",
        AppointmentStatus.NoShow => "warning",
        _ => "info"
    };

    public static string ToBadgeClass(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Paid => "success",
        InvoiceStatus.Cancelled => "neutral",
        InvoiceStatus.PartiallyPaid => "warning",
        InvoiceStatus.Draft => "neutral",
        _ => "info"
    };

    public static string Initials(this string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = string.Concat(parts.Take(2).Select(p => char.ToUpper(p[0], Turkish)));
        return initials;
    }

    public static string? FormatPhone(this string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '0')
        {
            return $"{digits[..4]} {digits[4..7]} {digits[7..9]} {digits[9..]}";
        }
        if (digits.Length == 10)
        {
            return $"0{digits[..3]} {digits[3..6]} {digits[6..8]} {digits[8..]}";
        }
        return phone;
    }
}
