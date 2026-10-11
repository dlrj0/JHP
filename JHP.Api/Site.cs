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

// 사이트 목록 조작(순서 변경/수정/시작 사이트 지정)을 UI와 분리한 순수 함수 모음.
// 목록과 DefaultSite 문자열만 다루므로 코드를 읽는 것만으로 검증할 수 있다.
public static class SiteListOps
{
    // 메뉴에서 시작 사이트 표시에 붙이는 문구
    public const string DefaultMark = " (시작)";

    // index 항목을 delta(-1 위 / +1 아래)만큼 옮긴다. 범위를 벗어나면 아무것도 바꾸지 않고 false.
    public static bool TryMove(IList<Site> sites, int index, int delta)
    {
        int target = index + delta;
        if (index < 0 || index >= sites.Count || target < 0 || target >= sites.Count) return false;
        (sites[index], sites[target]) = (sites[target], sites[index]);
        return true;
    }

    // index 항목을 edited로 바꾼다. 그 항목이 시작 사이트였다면 DefaultSite도 새 이름으로 따라간다.
    public static bool TryReplace(IList<Site> sites, int index, Site edited, ref string defaultSite)
    {
        if (index < 0 || index >= sites.Count) return false;
        if (IsDefaultAt(sites, index, defaultSite)) defaultSite = edited.Name;
        sites[index] = edited;
        return true;
    }

    // DefaultSite는 이름으로 저장되고 조회는 같은 이름 중 첫 항목(FirstOrDefault)이므로, 표시도 첫 항목만 한다.
    public static bool IsDefaultAt(IList<Site> sites, int index, string? defaultSite)
    {
        if (index < 0 || index >= sites.Count || string.IsNullOrEmpty(defaultSite)) return false;
        for (int i = 0; i < sites.Count; i++)
            if (sites[i].Name == defaultSite) return i == index;
        return false;
    }

    // 이미 시작 사이트이거나, 같은 이름의 앞선 항목이 있어 지정해도 그 항목이 가리켜지면 지정할 수 없다.
    public static bool CanSetDefault(IList<Site> sites, int index, string? defaultSite)
    {
        if (index < 0 || index >= sites.Count) return false;
        if (IsDefaultAt(sites, index, defaultSite)) return false;
        for (int i = 0; i < index; i++)
            if (sites[i].Name == sites[index].Name) return false;
        return true;
    }

    public static bool TrySetDefault(IList<Site> sites, int index, ref string defaultSite)
    {
        if (!CanSetDefault(sites, index, defaultSite)) return false;
        defaultSite = sites[index].Name;
        return true;
    }

    // 메뉴에 보이는 이름(시작 사이트면 문구를 덧붙임)
    public static string DisplayName(IList<Site> sites, int index, string? defaultSite) =>
        IsDefaultAt(sites, index, defaultSite) ? sites[index].Name + DefaultMark : sites[index].Name;
}
