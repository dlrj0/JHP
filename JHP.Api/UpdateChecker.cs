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
            bool hasUpdate = string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;

            return (hasUpdate, release.TagName, release.HtmlUrl);
        }
        catch
        {
            return (false, "", "");
        }
    }
}