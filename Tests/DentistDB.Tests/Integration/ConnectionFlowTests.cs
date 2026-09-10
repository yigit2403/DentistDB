using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace DentistDB.Tests.Integration;

public class ConnectionFlowTests : IClassFixture<ClinicAppFactory>
{
    private readonly ClinicAppFactory _factory;

    public ConnectionFlowTests(ClinicAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConnectPage_ShowsLanUrlAndCalendarFeed_ThatServesIcs()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var page = await client.GetAsync("/Settings/Connect");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.ReadAsync();
        Assert.Contains("Cihaz Bağlantısı", html);
        Assert.Contains("Yerel ağ", html);

        var match = Regex.Match(html, @"/Calendar/Feed\?token=([0-9a-f]{40})");
        Assert.True(match.Success, "calendar feed link missing");

        // The feed needs no PIN session but does need the token.
        var anonymous = _factory.CreateBrowser();
        var ics = await anonymous.GetAsync(match.Value);
        Assert.Equal(HttpStatusCode.OK, ics.StatusCode);
        Assert.Equal("text/calendar", ics.Content.Headers.ContentType!.MediaType);
        var body = await ics.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VEVENT", body);
        Assert.Contains("Ayşe Yılmaz", body);

        var wrongToken = await anonymous.GetAsync("/Calendar/Feed?token=" + new string('0', 40));
        Assert.Equal(HttpStatusCode.NotFound, wrongToken.StatusCode);

        // Rotating the token invalidates the old link.
        var rotate = await client.PostFormAsync("/Settings/RotateCalendarToken", "/Settings/Connect");
        Assert.Equal(HttpStatusCode.Redirect, rotate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(match.Value)).StatusCode);
    }

    [Fact]
    public async Task Manifest_AndIcons_AreServed()
    {
        var client = _factory.CreateBrowser();
        var manifest = await client.GetAsync("/manifest.webmanifest");
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Contains("\"name\": \"DentistDB\"", await manifest.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/images/icon-192.png")).StatusCode);
    }
}
