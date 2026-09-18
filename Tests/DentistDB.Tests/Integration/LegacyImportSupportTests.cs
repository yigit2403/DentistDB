using System.Net;
using DentistDB.Models;
using DentistDB.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentistDB.Tests.Integration;

/// <summary>
/// The Access-import feature: patients can be stored without a national ID (the old
/// database has one for only ~40% of records), many such patients coexist despite the
/// unique TCKN index, they are flagged for follow-up, and the interactive create form
/// still demands a valid TCKN.
/// </summary>
public class LegacyImportSupportTests : IClassFixture<ClinicAppFactory>
{
    private readonly ClinicAppFactory _factory;

    public LegacyImportSupportTests(ClinicAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ImportedPatients_WithoutTckn_Persist_Coexist_AndAreFlagged()
    {
        var ids = _factory.WithDb(db =>
        {
            Patient Make(string name, int legacyKey)
            {
                var p = new Patient
                {
                    FullName = name,
                    IsImported = true,
                    LegacyKey = legacyKey,
                    Tckn = null,
                    ArrivalDate = new DateOnly(2010, 5, 1)
                };
                p.SearchIndex = SearchNormalizer.BuildPatientIndex(p);
                return p;
            }

            var a = Make("İmport Hasta Bir", 90001);
            var b = Make("İmport Hasta İki", 90002); // second NULL Tckn must not trip the unique index
            db.Patients.AddRange(a, b);
            db.SaveChanges();
            return new[] { a.Id, b.Id };
        });

        var loaded = _factory.WithDb(db => db.Patients.Where(p => ids.Contains(p.Id)).ToList());
        Assert.Equal(2, loaded.Count);
        Assert.All(loaded, p => Assert.Null(p.Tckn));
        Assert.All(loaded, p => Assert.True(p.NeedsTckn));
    }

    [Fact]
    public async Task ImportedPatient_ShowsMissingTcknBadge_AndIsSearchable()
    {
        var id = _factory.WithDb(db =>
        {
            var p = new Patient
            {
                FullName = "Şükran Yıldız",
                IsImported = true,
                LegacyKey = 90050,
                Tckn = null,
                Phone = "05551112233"
            };
            p.SearchIndex = SearchNormalizer.BuildPatientIndex(p);
            db.Patients.Add(p);
            db.SaveChanges();
            return p.Id;
        });

        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        // Turkish-folded search finds it ("sukran" matches "Şükran").
        var list = await (await client.GetAsync("/Patients?search=sukran")).ReadAsync();
        Assert.Contains("Şükran Yıldız", list);

        var details = await (await client.GetAsync($"/Patients/Details/{id}")).ReadAsync();
        Assert.Contains("eski kayıttan aktarıldı", details);
    }

    [Fact]
    public async Task CreatePatient_WithoutTckn_IsRejected()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Zorunlu TCKN Testi"),
            ("Tckn", ""),
            ("ArrivalDate", "2026-09-18"));

        Assert.Contains("TCKN zorunludur", await response.ReadAsync());
        Assert.False(_factory.WithDb(db => db.Patients.Any(p => p.FullName == "Zorunlu TCKN Testi")));
    }
}
