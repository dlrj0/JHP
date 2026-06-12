namespace JHP.Asset;

public static class UserScripts
{
    public const string LaftelSkipNext = """
        (function() {
            const btn = document.querySelector('[data-testid="next-episode-button"]');
            if (btn) btn.click();
        })();
        """;

    public const string NetflixSkipNext = """
        (function() {
            const btn = document.querySelector('.watch-video--skip-content button');
            if (btn) btn.click();
        })();
        """;
}