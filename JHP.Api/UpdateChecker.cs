using System.Net.Http;
using System.Text.Json;

namespace JHP.Api;

public static class UpdateChecker
{
    private const string LatestApi = "https://api.github.com/repos/dlrj0/JHP/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        // 네트워크가 막혀 있어도 오래 매달리지 않도록 5초로 제한
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JHP-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<(bool HasUpdate, string LatestVersion, string Url)> Check(string currentVersion)
    {
        try
        {
            using var response = await Http.GetAsync(LatestApi);
            if (!response.IsSuccessStatusCode) return (false, "", "");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            string url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            bool draft = root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True;
            bool pre = root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True;
            if (draft || pre || string.IsNullOrWhiteSpace(tag)) return (false, "", "");

            return (IsNewer(tag, currentVersion), tag, url);
        }
        catch
        {
            return (false, "", "");
        }
    }

    // "v1.2.0" 같은 숫자 버전만 비교한다. "1.2.0-beta" 같은 프리릴리스 태그나 해석할 수 없는 태그는 업데이트로 보지 않는다.
    // (문자열 비교는 "1.10.0" < "1.9.0"으로 판정되므로 Version으로 비교)
    public static bool IsNewer(string latestTag, string currentVersion)
    {
        if (!TryParseVersion(latestTag, out var latest) || !TryParseVersion(currentVersion, out var current))
            return false;
        return latest > current;
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        version = new Version(0, 0);
        string s = text.Trim().TrimStart('v', 'V');
        if (s.Contains('-') || s.Contains('+')) return false;
        if (!Version.TryParse(s, out var parsed)) return false;
        version = parsed;
        return true;
    }
}
