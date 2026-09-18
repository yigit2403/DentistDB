using DentistDB.Services;
using Xunit;

namespace DentistDB.Tests.Unit;

public class UpdateCheckerTests
{
    private const string Release = """
        {
          "tag_name": "v0.5.0",
          "html_url": "https://github.com/yigit2403/DentistDB/releases/tag/v0.5.0",
          "assets": [
            { "name": "DentistDB-0.5.0.zip", "browser_download_url": "https://example.test/DentistDB-0.5.0.zip" },
            { "name": "DentistDB-Setup-0.5.0.exe", "browser_download_url": "https://example.test/DentistDB-Setup-0.5.0.exe" }
          ]
        }
        """;

    [Fact]
    public void ParseRelease_FindsInstallerAsset_AndDetectsNewerVersion()
    {
        var info = UpdateChecker.ParseRelease(Release, "0.4.0");

        Assert.True(info.IsNewer);
        Assert.Equal("0.5.0", info.LatestVersion);
        Assert.Equal("https://example.test/DentistDB-Setup-0.5.0.exe", info.DownloadUrl);
        Assert.Equal("https://github.com/yigit2403/DentistDB/releases/tag/v0.5.0", info.ReleaseUrl);
        Assert.Null(info.Error);
        Assert.NotNull(info.CheckedAt);
    }

    [Theory]
    [InlineData("0.5.0", false)]
    [InlineData("0.5.1", false)]
    [InlineData("1.0.0", false)]
    [InlineData("0.4.9", true)]
    public void ParseRelease_IsNewerOnlyWhenLatestIsGreater(string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.ParseRelease(Release, current).IsNewer);
    }

    [Fact]
    public void ParseRelease_WithoutExeAsset_HasNoDownloadButStillReportsVersion()
    {
        const string json = """{ "tag_name": "v0.6.0", "html_url": "https://x/r", "assets": [ { "name": "a.zip", "browser_download_url": "https://x/a.zip" } ] }""";

        var info = UpdateChecker.ParseRelease(json, "0.4.0");

        Assert.True(info.IsNewer);
        Assert.Null(info.DownloadUrl);
    }

    [Fact]
    public void ParseRelease_UnparsableTag_IsNotNewer_AndCarriesError()
    {
        var info = UpdateChecker.ParseRelease("""{ "tag_name": "latest", "assets": [] }""", "0.4.0");

        Assert.False(info.IsNewer);
        Assert.Null(info.LatestVersion);
        Assert.NotNull(info.Error);
    }

    [Theory]
    [InlineData("v0.5.0", "0.5.0")]
    [InlineData("0.5.0", "0.5.0")]
    [InlineData("V1.2.3.4", "1.2.3.4")]
    [InlineData("release-1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeVersion_StripsPrefix_AndRejectsGarbage(string? tag, string? expected)
    {
        Assert.Equal(expected, UpdateChecker.NormalizeVersion(tag));
    }
}
