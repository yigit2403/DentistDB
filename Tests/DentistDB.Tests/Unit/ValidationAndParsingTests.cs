using DentistDB.Infrastructure;
using DentistDB.Models;
using DentistDB.Services;
using Xunit;

namespace DentistDB.Tests.Unit;

public class TcknValidatorTests
{
    [Theory]
    [InlineData("10000000146")]
    [InlineData("12345678950")]
    public void Accepts_ValidNumbers(string tckn) => Assert.True(TcknValidator.IsValid(tckn));

    [Theory]
    [InlineData("12345678901")]
    [InlineData("11111111111")]
    [InlineData("01234567890")]
    [InlineData("1234567895")]
    [InlineData("1234567895a")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_InvalidNumbers(string? tckn) => Assert.False(TcknValidator.IsValid(tckn));
}

public class InvariantDecimalParsingTests
{
    [Theory]
    [InlineData("1250,50", 1250.50)]
    [InlineData("1.250,50", 1250.50)]
    [InlineData("1250.50", 1250.50)]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("1.250", 1250)]
    [InlineData("1.250.000", 1250000)]
    [InlineData("12.5", 12.5)]
    [InlineData("250", 250)]
    [InlineData(" 2 500 ₺", 2500)]
    [InlineData("-15,25", -15.25)]
    public void Parses_TurkishAndInvariantFormats(string input, decimal expected)
    {
        Assert.True(InvariantDecimalModelBinder.TryParse(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Parses_MultipleCommasAsGrouping()
    {
        Assert.True(InvariantDecimalModelBinder.TryParse("12,345,678", out var value));
        Assert.Equal(12345678m, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.34.56,7,8")]
    [InlineData("")]
    public void Rejects_Garbage(string input)
    {
        Assert.False(InvariantDecimalModelBinder.TryParse(input, out _));
    }
}

public class SearchNormalizerTests
{
    [Theory]
    [InlineData("Ömer ÇELİK", "omer celik")]
    [InlineData("  Ayşe   Yılmaz ", "ayse yilmaz")]
    [InlineData("İbrahim Şığ", "ibrahim sig")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_FoldsTurkishCharacters(string? input, string expected)
    {
        Assert.Equal(expected, SearchNormalizer.Normalize(input));
    }

    [Fact]
    public void BuildPatientIndex_IncludesPhoneDigits()
    {
        var patient = new Patient { FullName = "Zeynep Kaya", Tckn = "10000000300", Phone = "0534 333 44 55", Email = "Z@Example.com" };
        var index = SearchNormalizer.BuildPatientIndex(patient);

        Assert.Contains("zeynep kaya", index);
        Assert.Contains("05343334455", index);
        Assert.Contains("z@example.com", index);
    }
}

public class AppointmentRulesTests
{
    [Fact]
    public void Overlaps_DetectsIntersectingIntervals()
    {
        var start = new DateTime(2026, 9, 10, 9, 0, 0);

        Assert.True(AppointmentRules.Overlaps(start, 30, start.AddMinutes(15), 30));
        Assert.True(AppointmentRules.Overlaps(start, 60, start.AddMinutes(15), 15));
        Assert.False(AppointmentRules.Overlaps(start, 30, start.AddMinutes(30), 30));
        Assert.False(AppointmentRules.Overlaps(start, 30, start.AddMinutes(-30), 30));
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, true)]
    [InlineData(AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.NoShow, false)]
    public void BlocksCalendar_OnlyForLiveAppointments(AppointmentStatus status, bool expected)
    {
        Assert.Equal(expected, AppointmentRules.BlocksCalendar(status));
    }

    [Fact]
    public void RoundToSlot_RoundsToNearestFiveMinutes()
    {
        var value = new DateTime(2026, 9, 10, 9, 12, 40);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 15, 0), AppointmentRules.RoundToSlot(value));
    }
}

public class AccessPinOptionsTests
{
    [Theory]
    [InlineData("1234", false)]
    [InlineData("CHANGE_ME_ADMIN_PIN", false)]
    [InlineData("", false)]
    [InlineData("492817", true)]
    public void IsSecurePin_RejectsWeakAndPlaceholderPins(string pin, bool expected)
    {
        Assert.Equal(expected, AccessPinOptions.IsSecurePin(pin));
    }
}
