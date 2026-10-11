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

    [STAThread]
    static void Main()
    {
        // 같은 폴더의 JHP가 이미 실행 중이면 새로 띄우지 않는다 (같은 config.json/cache를 동시에 쓰면 설정이 덮어써짐).
        // 폴더별로 이름을 달리해, 다른 폴더의 사본(테스트용 인스턴스 등)은 서로 막지 않는다.
        string name = "JHP_" + Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToLowerInvariant())))[..16];
        using var mutex = new Mutex(true, name, out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        // 처리되지 않은 예외로 앱이 사라지지 않도록 로그를 남기고 알린다.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportError(e.ExceptionObject as Exception);

        Application.Run(new Form1());
    }

    private static void ReportError(Exception? ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }

        try
        {
            MessageBox.Show("예상하지 못한 오류가 발생했습니다.\n" + (ex?.Message ?? "알 수 없는 오류") +
                "\n\n자세한 내용은 실행 폴더의 error.log에 기록했습니다.", "JHP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch { }
    }
}
