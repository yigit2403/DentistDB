using System.Net;
using DentistDB.Extensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentistDB.Tests.Integration;

/// <summary>Treatment records ("Tedavi Kayıtları"): search folding, validation and tooth parsing.</summary>
public class TreatmentRecordTests : IClassFixture<ClinicAppFactory>
{
    private readonly ClinicAppFactory _factory;

    public TreatmentRecordTests(ClinicAppFactory factory)
    {
        _factory = factory;
    }

    private int AnyPatientId() => _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).Select(p => p.Id).First());

    [Fact]
    public async Task Search_IsTurkishCaseInsensitive()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        var patientId = AnyPatientId();

        var created = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create",
            ("PatientId", patientId.ToString()),
            ("Date", "2026-09-10"),
            ("Title", "Çekim Testi Özel"),
            ("Procedures", "Üst sağ yirmilik çekildi"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        foreach (var term in new[] { "çekim", "Çekim", "cekim", "üst sağ", "UST SAG", "yirmilik" })
        {
            var html = await (await client.GetAsync($"/PreviousOperations?search={Uri.EscapeDataString(term)}")).ReadAsync();
            Assert.Contains("Çekim Testi Özel", html);
        }

        var miss = await (await client.GetAsync("/PreviousOperations?search=implant")).ReadAsync();
        Assert.DoesNotContain("Çekim Testi Özel", miss);
    }

    [Fact]
    public async Task Create_RejectsUnknownPatient_WithValidationMessage_Not500()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create",
            ("PatientId", "999999"),
            ("Date", "2026-09-10"),
            ("Title", "Yok hasta"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hasta bulunamadı", await response.ReadAsync());
    }

    [Fact]
    public async Task Create_RejectsFutureDate()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create",
            ("PatientId", AnyPatientId().ToString()),
            ("Date", DateTime.Today.AddDays(3).ToString("yyyy-MM-dd")),
            ("Title", "Gelecek tarih"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("gelecekte olamaz", await response.ReadAsync());
        Assert.False(_factory.WithDb(db => db.PreviousOperations.Any(o => o.Title == "Gelecek tarih")));
    }

    [Fact]
    public async Task Create_KeepsOnlyValidFdiTeeth()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create",
            ("PatientId", AnyPatientId().ToString()),
            ("Date", "2026-09-10"),
            ("Title", "Garip diş girdisi"),
            ("SelectedTeeth", "18, abc, 99, 16, 16, 75"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var stored = _factory.WithDb(db => db.PreviousOperations.Single(o => o.Title == "Garip diş girdisi"));
        Assert.Equal("16,18,75", stored.SelectedTeethData);
        Assert.Contains("16 18 75", stored.SearchIndex);
    }

    [Theory]
    [InlineData("17, 16,16", "16,17")]
    [InlineData("abc,99,00,16", "16")]
    [InlineData("55;85 11", "11,55,85")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TeethNormalize_FiltersAndSorts(string? input, string? expected)
    {
        Assert.Equal(expected, TeethSelectionSerializer.Normalize(input));
    }
}
