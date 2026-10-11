namespace JHP.Api;

public class Site
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    // 사용자가 입력한 주소를 http/https 절대 주소로 보정한다. "youtube.com" → "https://youtube.com/".
    // javascript:, file: 등 다른 스킴이나 해석할 수 없는 값은 false.
    public static bool TryNormalizeUrl(string? input, out string url)
    {
        url = "";
        string text = (input ?? "").Trim();
        if (text.Length == 0) return false;
        if (!text.Contains("://")) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (string.IsNullOrEmpty(uri.Host)) return false;
        url = uri.AbsoluteUri;
        return true;
    }

    // 끝의 슬래시/대소문자 차이를 무시하고 같은 주소인지 비교 (WebView2의 Source는 끝에 /가 붙는다)
    public static bool SameUrl(string? a, string? b) =>
        string.Equals((a ?? "").TrimEnd('/'), (b ?? "").TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}