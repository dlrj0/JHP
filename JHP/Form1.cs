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

    public Form1()
    {
        InitializeComponent();
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

        timer.Start();

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
        lblTitle.MouseDown += TitleBar_MouseDown;
        lblNextAlarm.MouseDown += TitleBar_MouseDown;

        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        btnClose.Click += (_, _) => Close();
        btnAlarmSettings.Click += (_, _) => OpenAlarmSettings();

        // 볼륨
        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        sliderVolume.ValueChanged += (_, _) =>
        {
            lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            _config.Save();
        };

        // 투명도 (30~100%)
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

        // 메뉴 (사이트 추가 / 항상 위 / 테두리 숨김)
        menuTopmost.Checked = _config.TopMost;
        menuHideBorder.Checked = _config.IsHideWindowBorderOnFocusOut;
        TopMost = _config.TopMost;
        menuStrip.ItemClicked += MenuStrip_ItemClicked;

        // 사이트 목록
        siteList.SetSites(_config.Sites);
        siteList.ItemDoubleClick += (_, _) => NavigateToSelectedSite();
        btnAddSite.Click += (_, _) => AddSite();
        btnRemoveSite.Click += (_, _) => RemoveSelectedSite();

        // 알람 타이머 초기화
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
        UpdateNextAlarmLabel();

        timer.Tick += Timer_Tick;
    }

    // ===== WebView2 =====
    private async Task InitWV()
    {
        try
        {
            // CoreWebView2 초기화 전에 호출하면 예외 발생
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: cacheDir);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.NavigationCompleted += (_, _) => InjectJS();
            webView.CoreWebView2.Navigate(ResolveStartUrl());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 초기화 실패: {ex.Message}\nWebView2 런타임이 설치되어 있는지 확인해주세요.",
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

    private async void InjectJS()
    {
        if (webView.CoreWebView2 is null) return;

        string host = new Uri(webView.CoreWebView2.Source).Host;
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

    // ===== 알람 타이머 =====
    private void Timer_Tick(object? sender, EventArgs e)
    {
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
        int? min = null;

        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i])
                min = min is null ? _remaining[i] : Math.Min(min.Value, _remaining[i]);

        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled && _config.CustomAlarms[i].Tick > 0)
                min = min is null ? _customRemaining[i] : Math.Min(min.Value, _customRemaining[i]);

        lblNextAlarm.Text = min is null
            ? "다음 알람: --:--:--"
            : $"다음 알람: {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
    }

    private void OpenAlarmSettings()
    {
        using var form = new AlarmForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;

        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i]) _remaining[i] = AlarmSeconds[i];

        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled) _customRemaining[i] = _config.CustomAlarms[i].Tick;

        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
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
    private void AddSite()
    {
        using var form = new SiteForm();
        if (form.ShowDialog(this) != DialogResult.OK || form.Result is not { } site) return;

        _config.Sites.Add(site);
        siteList.AddSite(site);
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

    // ===== 포커스 아웃 시 테두리(타이틀바/메뉴바) 숨김 =====
    private void Form1_Activated(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = true;
        pnlMenuBar.Visible = true;
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = false;
        pnlMenuBar.Visible = false;
    }

    // ===== 종료 시 설정 저장 =====
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        Synth.Instance.Stop();

        if (WindowState == FormWindowState.Normal)
        {
            _config.X = Location.X;
            _config.Y = Location.Y;
            _config.Width = Size.Width;
            _config.Height = Size.Height;
        }

        _config.IsMaximize = WindowState == FormWindowState.Maximized;
        _config.TopMost = TopMost;
        _config.Opacity = Opacity;

        if (webView.CoreWebView2 is not null)
            _config.LatestUrl = webView.CoreWebView2.Source;

        _config.Save();
    }

    // ===== 테두리 없는 창 리사이즈 (WM_NCHITTEST) =====
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
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

        // 8방향 리사이즈 힌트 (테두리 두께는 ReSize.RESIZE_THICK)
        m.Result = (IntPtr)(pos switch
        {
            ReSize.MousePosition.Left => HTLEFT,
            ReSize.MousePosition.Right => HTRIGHT,
            ReSize.MousePosition.Top => HTTOP,
            ReSize.MousePosition.Bottom => HTBOTTOM,
            ReSize.MousePosition.TopLeft => HTTOPLEFT,
            ReSize.MousePosition.TopRight => HTTOPRIGHT,
            ReSize.MousePosition.BottomLeft => HTBOTTOMLEFT,
            ReSize.MousePosition.BottomRight => HTBOTTOMRIGHT,
            _ => HTCLIENT
        });
    }
}