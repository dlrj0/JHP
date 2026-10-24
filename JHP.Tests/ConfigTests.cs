using System.Text.Json;
using JHP.Api;
using Xunit;

namespace JHP.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jhp-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Write(string json)
    {
        string path = Path.Combine(_dir, "c.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void ReadFrom_ClampsOutOfRangeValues()
    {
        var c = Config.ReadFrom(Write("""{ "volume": 999, "opacity": 0.01, "rate": 50, "width": -1, "height": 0 }"""));
        Assert.NotNull(c);
        Assert.Equal(100, c!.Volume);
        Assert.Equal(0.3, c.Opacity);
        Assert.Equal(10, c.Rate);
        Assert.Equal(800, c.Width);
        Assert.Equal(600, c.Height);
    }

    [Fact]
    public void ReadFrom_DropsSitesWithNonHttpUrlsAndFixesMissingScheme()
    {
        var c = Config.ReadFrom(Write("""
            { "sites": [
                { "name": "ok", "url": "https://example.com/a" },
                { "name": "file", "url": "file:///C:/Windows/win.ini" },
                { "name": "js", "url": "javascript:alert(1)" },
                { "name": "noscheme", "url": "youtube.com" },
                { "name": "ftp", "url": "ftp://example.com" }
              ],
              "defaultSite": "file", "latestUrl": "file:///C:/x" }
            """));
        Assert.NotNull(c);
        Assert.Equal(["ok", "noscheme"], c!.Sites.Select(s => s.Name));
        Assert.StartsWith("https://youtube.com", c.Sites[1].Url);
        Assert.Equal("", c.DefaultSite);       // 제외된 사이트를 가리키는 시작 사이트는 비운다
        Assert.Equal("", c.LatestUrl);         // http/https가 아닌 마지막 주소는 버린다
    }

    [Fact]
    public void ReadFrom_FixesArrayLengthsAndNulls()
    {
        var c = Config.ReadFrom(Write("""{ "alarmEnabled": [true], "customAlarms": [], "sites": null, "defaultSite": "없는사이트" }"""));
        Assert.NotNull(c);
        Assert.Equal(8, c!.AlarmEnabled.Length);
        Assert.True(c.AlarmEnabled[0]);
        Assert.False(c.AlarmEnabled[7]);
        Assert.Equal(3, c.CustomAlarms.Count);
        Assert.Empty(c.Sites);
        Assert.Equal("", c.DefaultSite);
    }

    [Fact]
    public void ReadFrom_DropsSitesWithoutUrl_AndKeepsKnownDefault()
    {
        var c = Config.ReadFrom(Write("""{ "sites": [ {"name":"a","url":"https://a.com"}, {"name":"b","url":""}, null ], "defaultSite": "a" }"""));
        Assert.NotNull(c);
        Assert.Single(c!.Sites);
        Assert.Equal("a", c.DefaultSite);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("")]
    public void ReadFrom_InvalidFile_ReturnsNull(string content)
    {
        Assert.Null(Config.ReadFrom(Write(content)));
    }

    [Fact]
    public void ReadFrom_MissingFile_ReturnsNull()
    {
        Assert.Null(Config.ReadFrom(Path.Combine(_dir, "nope.json")));
    }

    [Fact]
    public void ExportTo_ThenReadFrom_RoundTrips()
    {
        var src = Config.ReadFrom(Write("""{ "volume": 33, "tts": true, "sites": [ {"name":"x","url":"https://x.com"} ], "skippedVersion": "v9.9.9" }"""))!;
        string path = Path.Combine(_dir, "export.json");
        Assert.True(src.ExportTo(path));
        var back = Config.ReadFrom(path)!;
        Assert.Equal(33, back.Volume);
        Assert.True(back.Tts);
        Assert.Equal("https://x.com", back.Sites[0].Url);
        Assert.Equal("v9.9.9", back.SkippedVersion);
    }

    [Fact]
    public void ExportTo_UnwritablePath_ReturnsFalse()
    {
        var c = new Config();
        Assert.False(c.ExportTo(Path.Combine(_dir, "no-such-dir", "x.json")));
    }

    [Fact]
    public void Replace_CopiesEveryPersistedProperty()
    {
        // 새 설정 항목을 추가하고 Replace에 넣지 않으면 설정 가져오기에서 조용히 빠진다. 모든 공개 속성이 복사되는지 확인한다.
        var other = new Config
        {
            Width = 1, Height = 2, X = 3, Y = 4, Opacity = 0.5, Volume = 7, Rate = 3, IsMaximize = true, TopMost = true,
            IsHideWindowBorderOnFocusOut = true, MuteVideoOnAlarm = true, HotkeyEnabled = true, PrivateMode = true,
            Sites = [new Site { Name = "n", Url = "https://n.com" }], DefaultSite = "n",
            AlarmEnabled = [true, true, false, false, false, false, false, false], AlarmName = "a.mp3", Tts = true,
            LatestUrl = "https://l.com", SkippedVersion = "v2"
        };
        var target = new Config();
        target.Replace(other);

        foreach (var prop in typeof(Config).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;
            Assert.True(
                JsonSerializer.Serialize(prop.GetValue(other)) == JsonSerializer.Serialize(prop.GetValue(target)),
                $"Replace가 {prop.Name}을(를) 복사하지 않음");
        }
    }
}
