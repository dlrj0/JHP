using Octokit;

namespace JHP.Api;

public static class UpdateChecker
{
    private const string Owner = "dlrj0";
    private const string Repo = "JHP";

    public static async Task<(bool HasUpdate, string LatestVersion, string Url)> Check(string currentVersion)
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("JHP-Updater"));
            var release = await client.Repository.Release.GetLatest(Owner, Repo);
            if (release is null) return (false, "", "");

            string latest = release.TagName.TrimStart('v');
            string current = currentVersion.TrimStart('v');
            // 문자열 비교는 "1.10.0" < "1.9.0"으로 판정되므로 Version으로 비교하고, 파싱 실패 시에만 문자열 비교로 대체
            bool hasUpdate = Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer)
                ? latestVer > currentVer
                : string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;

            return (hasUpdate, release.TagName, release.HtmlUrl);
        }
        catch
        {
            return (false, "", "");
        }
    }
}