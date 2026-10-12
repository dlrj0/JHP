namespace JHP.Asset;

public static class UserScripts
{
    public const string LaftelSkipNext = """
        (function() {
            const btn = document.querySelector('[data-testid="next-episode-button"]');
            if (btn) btn.click();
        })();
        """;

    // 리애니메(reanime.to)는 자체 설정 토글이 있다(확인 2026-10-12, 재생 페이지 DOM):
    //   - 버튼 aria-label "Auto Play" / "Auto Next" / "Auto Skip Intro & Outro", 켜짐 상태는 class "text-primary"
    //   - 설정은 localStorage "project_r_prefs"(autoPlay/autoNext/skipIntro/skipOutro)에 저장됨
    // 영상은 iframe 안이라 직접 누르지 않고, 꺼진 토글만 한 번 켠다. 이후 사용자가 직접 끈 설정은 건드리지 않는다.
    public const string ReanimeAutoOptions = """
        (function() {
            if (window.__jhpReanimeOpts) return;
            window.__jhpReanimeOpts = true;
            const KEY = 'jhp_reanime_opts_done';
            try { if (localStorage.getItem(KEY)) return; } catch (e) { return; }
            const labels = ['Auto Play', 'Auto Next', 'Auto Skip Intro & Outro'];
            const isOn = b => /(^|\s)text-primary(\s|$)/.test(b.className);
            let tries = 0;
            const apply = () => {
                const btns = labels.map(l => document.querySelector('button[aria-label="' + l + '"]'));
                if (btns.some(b => !b)) return false;
                btns.forEach(b => { if (!isOn(b)) b.click(); });
                try { localStorage.setItem(KEY, '1'); } catch (e) { }
                return true;
            };
            if (apply()) return;
            const obs = new MutationObserver(() => {
                if (apply() || ++tries > 200) obs.disconnect();
            });
            obs.observe(document.documentElement, { childList: true, subtree: true });
        })();
        """;

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