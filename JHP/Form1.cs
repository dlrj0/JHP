using JHP.Api;
using JHP.Asset;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JHP;

public partial class Form1 : Form
{
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly int[] AlarmSeconds = [7200, 3600, 1800, 1200, 900, 600, 100, 55];
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    private const string CurrentVersion = "v1.0.0";

    private readonly Config _config = Config.Instance;
    private readonly int[] _remaining = new int[8];
    private readonly int[] _customRemaining = new int[3];

    // ④ 타이머 실행 상태 플래그 — WinForms Timer는 항상 돌지만 이 값이 false면 아무 동작 안 함
    private bool _timerRunning = false;

    public Form1()
    {
        InitializeComponent();

        // 작업표시줄/Alt-Tab 아이콘 (Resources/app.ico — csproj에서 출력 폴더로 복사됨)
        // InitializeComponent()가 아닌 여기서 처리: 디자이너의 CodeDom 파서는
        // try/catch나 if문이 섞인 코드를 이해하지 못해 디자이너 오류를 일으킴
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
                Icon = new Icon(iconPath);
        }
        catch { /* 아이콘 파일이 없어도 앱 동작에는 영향 없음 */ }

        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
        Activated += Form1_Activated;
        Deactivate += Form1_Deactivate;
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        InitControls();

        if (_config.Width > 0 && _config.Height > 0)
            Size = new Size(Math.Max(_config.Width, MinimumSize.Width), Math.Max(_config.Height, MinimumSize.Height));

        if (IsOnScreen(_config.X, _config.Y))
            Location = new Point(_config.X, _config.Y);

        // WinForms Timer는 시작하되, _timerRunning=false 로 실제 동작은 정지 상태
        timer.Start();
        ResetTimerState();

        await InitWV();
        CheckUpdateAsync();

        if (_config.IsMaximize)
            WindowState = FormWindowState.Maximized;
    }

    private static bool IsOnScreen(int x, int y)
    {
        foreach (Screen screen in Screen.AllScreens)
            if (screen.WorkingArea.Contains(x, y)) return true;
        return false;
    }

    private void InitControls()
    {
        // 타이틀바 드래그 이동
        pnlTitleBar.MouseDown += TitleBar_MouseDown;
        lblTitle.MouseDown    += TitleBar_MouseDown;

        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        btnClose.Click        += (_, _) => Close();
        btnAlarmSettings.Click += (_, _) => OpenAlarmSettings();

        // 볼륨 슬라이더
        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        sliderVolume.ValueChanged += (_, _) =>
        {
            lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            _config.Save();
        };

        // 투명도 슬라이더 (30~100%)
        sliderOpacity.Value = Math.Clamp((int)(_config.Opacity * 100), sliderOpacity.Minimum, sliderOpacity.Maximum);
        Opacity = sliderOpacity.Value / 100.0;
        lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
        sliderOpacity.ValueChanged += (_, _) =>
        {
            lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
            Opacity = sliderOpacity.Value / 100.0;
            _config.Opacity = Opacity;
            _config.Save();
        };

        // 메뉴바 (사이트 추가 / 항상 위 / 테두리 숨김)
        menuTopmost.Checked   = _config.TopMost;
        menuHideBorder.Checked = _config.IsHideWindowBorderOnFocusOut;
        TopMost = _config.TopMost;
        menuStrip.ItemClicked += MenuStrip_ItemClicked;

        // 사이드바 사이트 목록
        siteList.SetSites(_config.Sites);
        siteList.ItemDoubleClick += (_, _) => NavigateToSelectedSite();
        btnAddSite.Click    += (_, _) => AddSite();
        btnRemoveSite.Click += (_, _) => RemoveSelectedSite();

        // ④ 타이머 라벨: 좌클릭 = 시작/재시작, 우클릭 = 컨텍스트 메뉴
        lblNextAlarm.Cursor    = Cursors.Hand;
        lblNextAlarm.MouseDown += LblNextAlarm_MouseDown;
        SetupTimerContextMenu();

        timer.Tick += Timer_Tick;
    }

    // ===== ④ 타이머 컨텍스트 메뉴 =====
    private void SetupTimerContextMenu()
    {
        var menu = new ContextMenuStrip();

        var itemStart = new ToolStripMenuItem("▶  시작 / 재시작");
        itemStart.Click += (_, _) => StartTimer();

        var itemStop = new ToolStripMenuItem("⏹  정지");
        itemStop.Click += (_, _) => StopTimer();

        menu.Items.Add(itemStart);
        menu.Items.Add(itemStop);
        menu.Items.Add(new ToolStripSeparator());

        var itemSettings = new ToolStripMenuItem("⏰  알람 설정 열기");
        itemSettings.Click += (_, _) => OpenAlarmSettings();
        menu.Items.Add(itemSettings);

        lblNextAlarm.ContextMenuStrip = menu;
    }

    // 좌클릭 = 시작/재시작
    private void LblNextAlarm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) StartTimer();
    }

    // ===== ④ 타이머 제어 =====
    private void StartTimer()
    {
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
        _timerRunning = true;
        UpdateNextAlarmLabel();
    }

    private void StopTimer()
    {
        _timerRunning = false;
        UpdateNextAlarmLabel();
    }

    private void ResetTimerState()
    {
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
        _timerRunning = false;
        UpdateNextAlarmLabel();
    }

    // ===== WebView2 =====
    private async Task InitWV()
    {
        try
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: cacheDir);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.NavigationCompleted += (_, _) => InjectJS();
            webView.CoreWebView2.Navigate(ResolveStartUrl());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 초기화 실패: {ex.Message}\nWebView2 런타임이 설치되어 있는지 확인해주세요.",
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string ResolveStartUrl()
    {
        if (!string.IsNullOrWhiteSpace(_config.LatestUrl)) return _config.LatestUrl;

        var defaultSite = _config.Sites.FirstOrDefault(s => s.Name == _config.DefaultSite);
        if (defaultSite is not null) return defaultSite.Url;

        return _config.Sites.Count > 0 ? _config.Sites[0].Url : "https://www.naver.com";
    }

    // ② about:blank 또는 잘못된 URI 에서 예외 발생 방지
    private async void InjectJS()
    {
        if (webView.CoreWebView2 is null) return;

        string? source = webView.CoreWebView2.Source;
        if (string.IsNullOrWhiteSpace(source)) return;

        string host;
        try { host = new Uri(source).Host; }
        catch { return; }

        try
        {
            if (host.Contains("laftel.net"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.LaftelSkipNext);
            else if (host.Contains("netflix.com"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.NetflixSkipNext);
        }
        catch { }
    }

    // ===== 업데이트 확인 (GitHub Releases) =====
    private async void CheckUpdateAsync()
    {
        var (hasUpdate, latest, url) = await UpdateChecker.Check(CurrentVersion);
        if (!hasUpdate) return;

        if (Prompt.ShowDialog("업데이트 알림", $"새 버전 {latest}이 있습니다. 다운로드 페이지를 여시겠습니까?"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ===== 타이머 Tick =====
    private void Timer_Tick(object? sender, EventArgs e)
    {
        // ④ _timerRunning = false 이면 아무 동작 없음
        if (!_timerRunning) return;

        for (int i = 0; i < 8; i++)
        {
            if (!_config.AlarmEnabled[i]) continue;
            if (--_remaining[i] > 0) continue;

            _remaining[i] = AlarmSeconds[i];
            FireAlarm($"{AlarmLabels[i]} 알람");
        }

        for (int i = 0; i < 3; i++)
        {
            var alarm = _config.CustomAlarms[i];
            if (!alarm.Enabled || alarm.Tick <= 0) continue;
            if (--_customRemaining[i] > 0) continue;

            _customRemaining[i] = alarm.Tick;
            FireAlarm(string.IsNullOrWhiteSpace(alarm.Name) ? "커스텀 알람" : alarm.Name);
        }

        UpdateNextAlarmLabel();
    }

    private void FireAlarm(string name)
    {
        if (_config.Tts)
            Synth.Instance.TTS(name, _config.Volume, _config.Rate);
        else
            Synth.Instance.Ring(_config.AlarmName, _config.Volume);
    }

    private void UpdateNextAlarmLabel()
    {
        if (!_timerRunning)
        {
            lblNextAlarm.Text = "⏱ 클릭하여 시작";
            return;
        }

        int? min = null;

        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i])
                min = min is null ? _remaining[i] : Math.Min(min.Value, _remaining[i]);

        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled && _config.CustomAlarms[i].Tick > 0)
                min = min is null ? _customRemaining[i] : Math.Min(min.Value, _customRemaining[i]);

        lblNextAlarm.Text = min is null
            ? "⏱ (알람 비활성)"
            : $"다음: {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
    }

    // ③ TopMost=true 상태에서 자식창 안 보이는 버그 방지
    private void OpenAlarmSettings()
    {
        bool was = TopMost;
        TopMost = false;
        using var form = new AlarmForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK;
        TopMost = was;

        if (!ok) return;

        // 설정 변경 후 남은 시간 재초기화
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;

        sliderVolume.Value  = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        UpdateNextAlarmLabel();
    }

    // ===== 메뉴 명령 분기 =====
    private void MenuStrip_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem?.Tag is not ToolStripCommand cmd) return;

        switch (cmd)
        {
            case ToolStripCommand.ADD_SITE:
                AddSite();
                break;

            case ToolStripCommand.TOPMOST:
                TopMost = menuTopmost.Checked;
                _config.TopMost = TopMost;
                _config.Save();
                break;

            case ToolStripCommand.TOGGLE_HIDE_WINDOW_BORDER:
                _config.IsHideWindowBorderOnFocusOut = menuHideBorder.Checked;
                _config.Save();
                break;
        }
    }

    // ===== 사이트 목록 =====
    // ③ TopMost 자식창 버그 방지
    private void AddSite()
    {
        bool was = TopMost;
        TopMost = false;
        using var form = new SiteForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK && form.Result is not null;
        TopMost = was;

        if (!ok) return;

        _config.Sites.Add(form.Result!);
        siteList.AddSite(form.Result!);
        _config.Save();
    }

    private void RemoveSelectedSite()
    {
        int index = siteList.SelectedIndex;
        if (index < 0) return;

        _config.Sites.RemoveAt(index);
        siteList.RemoveSite(index);
        _config.Save();
    }

    private void NavigateToSelectedSite()
    {
        if (siteList.SelectedSite is { } site && webView.CoreWebView2 is not null)
            webView.CoreWebView2.Navigate(site.Url);
    }

    // ===== 타이틀바 드래그 =====
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ===== 포커스 아웃 시 테두리 숨김 =====
    private void Form1_Activated(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = true;
        pnlMenuBar.Visible  = true;
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = false;
        pnlMenuBar.Visible  = false;
    }

    // ===== 종료 시 설정 저장 =====
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        Synth.Instance.Stop();

        if (WindowState == FormWindowState.Normal)
        {
            _config.X      = Location.X;
            _config.Y      = Location.Y;
            _config.Width  = Size.Width;
            _config.Height = Size.Height;
        }

        _config.IsMaximize = WindowState == FormWindowState.Maximized;
        _config.TopMost    = TopMost;
        _config.Opacity    = Opacity;

        if (webView.CoreWebView2 is not null)
            _config.LatestUrl = webView.CoreWebView2.Source;

        _config.Save();
    }

    // ===== 테두리 없는 창: 리사이즈 (WM_NCHITTEST) =====
    private const int WM_NCHITTEST   = 0x0084;
    private const int HTCLIENT       = 1;
    private const int HTCAPTION      = 2;
    private const int HTLEFT         = 10;
    private const int HTRIGHT        = 11;
    private const int HTTOP          = 12;
    private const int HTTOPLEFT      = 13;
    private const int HTTOPRIGHT     = 14;
    private const int HTBOTTOM       = 15;
    private const int HTBOTTOMLEFT   = 16;
    private const int HTBOTTOMRIGHT  = 17;
    private const int WM_NCLBUTTONDOWN = 0xA1;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != WM_NCHITTEST || WindowState != FormWindowState.Normal || (int)m.Result != HTCLIENT)
            return;

        int x = unchecked((short)(m.LParam.ToInt32() & 0xFFFF));
        int y = unchecked((short)((m.LParam.ToInt32() >> 16) & 0xFFFF));
        var pos = ReSize.GetMousePosition(this, PointToClient(new Point(x, y)));

        m.Result = (IntPtr)(pos switch
        {
            ReSize.MousePosition.Left        => HTLEFT,
            ReSize.MousePosition.Right       => HTRIGHT,
            ReSize.MousePosition.Top         => HTTOP,
            ReSize.MousePosition.Bottom      => HTBOTTOM,
            ReSize.MousePosition.TopLeft     => HTTOPLEFT,
            ReSize.MousePosition.TopRight    => HTTOPRIGHT,
            ReSize.MousePosition.BottomLeft  => HTBOTTOMLEFT,
            ReSize.MousePosition.BottomRight => HTBOTTOMRIGHT,
            _                               => HTCLIENT
        });
    }

    // ⑤ 창 가장자리 리사이즈 커서 시각적 피드백
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
