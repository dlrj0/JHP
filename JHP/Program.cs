namespace JHP;

static class Program
{
    // exe에 포함된 앱 아이콘(Resources\app.ico). 폼 아이콘으로 지정하지 않으면 작업 표시줄에 기본 아이콘이 보인다.
    internal static readonly System.Drawing.Icon? AppIcon = LoadAppIcon();

    private static System.Drawing.Icon? LoadAppIcon()
    {
        try { return System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { return null; }
    }

    // 자동 테스트(JHP_OFFSCREEN=1)로 띄운 창은 활성화(포커스 이동)하지 않는다. 사용자가 작업 중인 창의 포커스를 빼앗지 않기 위함.
    internal static readonly bool TestOffscreen = Environment.GetEnvironmentVariable("JHP_OFFSCREEN") == "1";

    [STAThread]
    static void Main()
    {
        // 같은 폴더의 JHP가 이미 실행 중이면 새로 띄우지 않는다 (같은 config.json/cache를 동시에 쓰면 설정이 덮어써짐).
        // 폴더별로 이름을 달리해, 다른 폴더의 사본(테스트용 인스턴스 등)은 서로 막지 않는다.
        string name = "JHP_" + Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToLowerInvariant())))[..16];
        using var mutex = new Mutex(true, name, out bool createdNew);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name + "_show");
        if (!createdNew)
        {
            // 이미 실행 중인 창에 "앞으로 나와라" 신호를 보내고 종료
            try { showEvent.Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();

        // 처리되지 않은 예외로 앱이 사라지지 않도록 로그를 남기고 알린다.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportError(e.ExceptionObject as Exception);

        var form = new Form1();
        bool pendingShow = false;   // 창이 만들어지기 전에 온 요청은 표시 직후 처리한다
        form.Shown += (_, _) =>
        {
            if (pendingShow) { pendingShow = false; form.BringToFrontFromAnotherInstance(); }
        };
        var registered = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) =>
        {
            try
            {
                if (form.IsDisposed) return;
                if (!form.IsHandleCreated) { pendingShow = true; return; }
                form.BeginInvoke(form.BringToFrontFromAnotherInstance);
            }
            catch { }
        }, null, Timeout.Infinite, false);

        try { Application.Run(form); }
        finally { registered.Unregister(null); }
    }

    private static DateTime _lastErrorShown = DateTime.MinValue;

    private static void ReportError(Exception? ex)
    {
        try
        {
            // 설정/캐시와 같은 폴더에 기록한다(exe 폴더에 쓸 수 없으면 %LOCALAPPDATA%\JHP).
            // 같은 오류가 반복돼도 커지지 않게 1MB를 넘으면 이전 로그를 한 단계만 보관한다.
            string logPath = Path.Combine(JHP.Api.Config.DataDirectory, "error.log");
            try
            {
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1_000_000)
                    File.Move(logPath, logPath + ".old", overwrite: true);
            }
            catch { }
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }

        // 같은 오류가 타이머 등에서 반복돼도 대화상자가 쌓이지 않도록 5초에 한 번만 알린다 (로그는 모두 기록)
        if ((DateTime.Now - _lastErrorShown).TotalSeconds < 5) return;
        _lastErrorShown = DateTime.Now;

        try
        {
            MessageBox.Show("예상하지 못한 오류가 발생했습니다.\n" + (ex?.Message ?? "알 수 없는 오류") +
                $"\n\n자세한 내용은 설정 폴더의 error.log에 기록했습니다.\n{JHP.Api.Config.DataDirectory}", "JHP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch { }
    }
}
