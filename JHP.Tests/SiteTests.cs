using JHP.Api;
using Xunit;

namespace JHP.Tests;

public class SiteTests
{
    [Theory]
    [InlineData("youtube.com", "https://youtube.com/")]
    [InlineData("  https://laftel.net  ", "https://laftel.net/")]
    [InlineData("http://example.com/a?b=1", "http://example.com/a?b=1")]
    public void TryNormalizeUrl_Accepts(string input, string expected)
    {
        Assert.True(Site.TryNormalizeUrl(input, out string url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com")]
    [InlineData("https://")]
    public void TryNormalizeUrl_Rejects(string? input)
    {
        Assert.False(Site.TryNormalizeUrl(input, out string url));
        Assert.Equal("", url);
    }

    [Fact]
    public void SameUrl_IgnoresTrailingSlashAndCase()
    {
        Assert.True(Site.SameUrl("https://Laftel.net/", "https://laftel.net"));
        Assert.False(Site.SameUrl("https://a.com", "https://b.com"));
        Assert.True(Site.SameUrl(null, ""));
    }
}

public class SiteListOpsTests
{
    private static List<Site> Sample() =>
    [
        new Site { Name = "A", Url = "https://a.com" },
        new Site { Name = "B", Url = "https://b.com" },
        new Site { Name = "C", Url = "https://c.com" },
    ];

    [Fact]
    public void TryMove_SwapsNeighbors_AndRejectsOutOfRange()
    {
        var s = Sample();
        Assert.True(SiteListOps.TryMove(s, 1, -1));
        Assert.Equal(new[] { "B", "A", "C" }, s.Select(x => x.Name));
        Assert.False(SiteListOps.TryMove(s, 0, -1));
        Assert.False(SiteListOps.TryMove(s, 2, 1));
        Assert.False(SiteListOps.TryMove(s, 5, 1));
        Assert.Equal(new[] { "B", "A", "C" }, s.Select(x => x.Name));
    }

    [Fact]
    public void TryReplace_DefaultSiteFollowsRename()
    {
        var s = Sample();
        string def = "B";
        Assert.True(SiteListOps.TryReplace(s, 1, new Site { Name = "B2", Url = "https://b2.com" }, ref def));
        Assert.Equal("B2", def);

        string def2 = "A";
        Assert.True(SiteListOps.TryReplace(s, 2, new Site { Name = "C2", Url = "https://c2.com" }, ref def2));
        Assert.Equal("A", def2);
        Assert.False(SiteListOps.TryReplace(s, 9, new Site(), ref def2));
    }

    [Fact]
    public void DefaultSite_WithDuplicateNames_OnlyFirstIsDefault()
    {
        var s = Sample();
        s.Add(new Site { Name = "A", Url = "https://a2.com" });
        Assert.True(SiteListOps.IsDefaultAt(s, 0, "A"));
        Assert.False(SiteListOps.IsDefaultAt(s, 3, "A"));
        Assert.False(SiteListOps.CanSetDefault(s, 3, "B"));   // 앞에 같은 이름이 있어 지정해도 첫 항목이 가리켜짐
        Assert.True(SiteListOps.CanSetDefault(s, 2, "A"));
    }

    [Fact]
    public void TrySetDefault_AndDisplayName()
    {
        var s = Sample();
        string def = "";
        Assert.True(SiteListOps.TrySetDefault(s, 2, ref def));
        Assert.Equal("C", def);
        Assert.False(SiteListOps.TrySetDefault(s, 2, ref def));   // 이미 시작 사이트
        Assert.Equal("C" + SiteListOps.DefaultMark, SiteListOps.DisplayName(s, 2, def));
        Assert.Equal("A", SiteListOps.DisplayName(s, 0, def));
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.0", "v1.1.0", true)]
    [InlineData("v1.10.0", "v1.9.0", true)]       // 문자열 비교라면 틀리는 경우
    [InlineData("v1.1.0", "v1.1.0", false)]
    [InlineData("v1.0.9", "v1.1.0", false)]
    [InlineData("v1.2.0-beta", "v1.1.0", false)]  // 프리릴리스 태그
    [InlineData("release-2024", "v1.1.0", false)] // 해석할 수 없는 태그
    [InlineData("", "v1.1.0", false)]
    public void IsNewer(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
    }
}
