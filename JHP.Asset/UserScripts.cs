namespace JHP.Asset;

public static class UserScripts
{
    // 넷플릭스는 페이지 이동 없이 화면만 바뀌는 SPA이고, 건너뛰기 버튼은 재생 중 특정 시점에 나타났다 사라진다.
    // 로드 시 한 번만 찾으면 대부분 버튼이 아직 없으므로, 한 번 설치해 두고 DOM 변화를 계속 감시한다.
    // 확인된 요소(2026-10-12, 넷플릭스 재생 화면 DOM):
    //   - 오프닝 건너뛰기: <button data-uia="player-skip-intro"> (부모 .watch-video--skip-content)
    //   - 다음 화: data-uia가 "next-episode-seamless-button"으로 시작하는 요소(카운트다운 중에는 "-draining"이 붙음)
    public const string NetflixSkipNext = """
        (function() {
            if (window.__jhpNetflixSkip) return;
            window.__jhpNetflixSkip = true;
            let busy = false;
            const click = () => {
                if (busy) return;
                const btn = document.querySelector('[data-uia="player-skip-intro"]')
                    || document.querySelector('.watch-video--skip-content button')
                    || document.querySelector('[data-uia^="next-episode-seamless-button"]');
                if (!btn) return;
                busy = true;
                btn.click();
                setTimeout(() => { busy = false; }, 1500);   // 같은 버튼을 연속으로 누르지 않게
            };
            new MutationObserver(click).observe(document.documentElement, { childList: true, subtree: true });
            click();
        })();
        """;
}