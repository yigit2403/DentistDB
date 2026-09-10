using DentistDB.Models;
using DentistDB.Services;
using Xunit;

namespace DentistDB.Tests.Unit;

public class ConsentTemplateTests
{
    [Fact]
    public void Render_ReplacesAllPlaceholders()
    {
        var patient = new Patient { FullName = "Ayşe Yılmaz", Tckn = "10000000146", BirthDate = new DateOnly(1985, 4, 12) };
        var clinic = new ClinicSettings("Gülüş Diş", new TimeOnly(9, 0), new TimeOnly(18, 0));
        var identity = new ClinicIdentity("Ali Veli", "Dt.", "12345", "Konya", "0332 000 00 00", "");

        var text = ConsentTemplates.Render("{HastaAdi} {TCKN} {DogumTarihi} {Tarih} {KlinikAdi} {HekimAdi} {Adres} {Telefon}", patient, clinic, identity, new DateOnly(2026, 9, 10));

        Assert.Equal("Ayşe Yılmaz 10000000146 12.04.1985 10.09.2026 Gülüş Diş Dt. Ali Veli Konya 0332 000 00 00", text);
    }

    [Fact]
    public void Defaults_ContainNoUnknownPlaceholders()
    {
        var patient = new Patient { FullName = "X", Tckn = "1" };
        var clinic = new ClinicSettings("K", new TimeOnly(9, 0), new TimeOnly(18, 0));
        var identity = new ClinicIdentity("", "Dt.", "", "", "", "");

        foreach (var template in new[] { ConsentTemplates.TreatmentDefault, ConsentTemplates.KvkkDefault })
        {
            var rendered = ConsentTemplates.Render(template, patient, clinic, identity, DateOnly.FromDateTime(DateTime.Today));
            Assert.DoesNotContain("{", rendered);
        }
    }
}

public class PatientRiskTests
{
    [Fact]
    public void RiskFlags_ListOnlyRelevantConditions()
    {
        var patient = new Patient { Allergies = "Penisilin", UsesAnticoagulant = true, IsSmoker = true };

        Assert.True(patient.HasMedicalRisk);
        Assert.Equal(new[] { "Alerji: Penisilin", "Kan sulandırıcı" }, patient.RiskFlags);
    }

    [Fact]
    public void HasMedicalRisk_FalseWhenNothingSet()
    {
        Assert.False(new Patient().HasMedicalRisk);
        Assert.True(new Patient { MedicalAlerts = "Bayılma öyküsü" }.HasMedicalRisk);
    }
}

public class BackupSettingsTests
{
    [Fact]
    public void IsStale_WhenOlderThan36Hours()
    {
        Assert.True(new BackupSettings("x", new TimeOnly(2, 0), 30, null, null).IsStale);
        Assert.True(new BackupSettings("x", new TimeOnly(2, 0), 30, DateTime.UtcNow.AddHours(-40), "ok").IsStale);
        Assert.False(new BackupSettings("x", new TimeOnly(2, 0), 30, DateTime.UtcNow.AddHours(-10), "ok").IsStale);
        Assert.True(new BackupSettings("x", new TimeOnly(2, 0), 30, DateTime.UtcNow, "HATA: disk dolu").LastFailed);
    }
}
