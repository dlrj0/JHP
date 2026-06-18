using JHP.Api;
using JHP.Asset;
using JHP.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JHP;

public partial class Form1 : Form
{
    private static readonly int[] AlarmSeconds = [7200, 3600, 1800, 1200, 900, 600, 100, 55];
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    private const string CurrentVersion = "v1.0.0";

    private readonly Config _config = Config.Instance;
    private readonly int[] _remaining = new int[8];
    private readonly int[] _customRemaining = new int[3];
    private bool _timerRunning = false;

    // 인라인 편집 대상 추적 ("volume" | "opacity")
    private string? _inlineEditTarget;

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

        // 타이머는 자동 시작 안 함 — 사용자가 timerButton 클릭 시 시작 (ToggleTimer에서만 timer.Start())

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
        // 타이틀바 드래그 이동 (timerButton 제외 — 버튼 클릭과 충돌)
        pnlTitleBar.MouseDown += TitleBar_MouseDown;
        lblTitle.MouseDown += TitleBar_MouseDown;

        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        btnClose.Click += (_, _) => Close();

        // timerButton: 좌클릭=시작/정지, 우클릭=알람설정
        timerButton.Click += (_, _) => ToggleTimer();
        timerButton.RightClick += (_, _) => OpenAlarmSettings();
        toolTip.SetToolTip(timerButton, "클릭하여 타이머 시작");

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
        lblVolumeValue.Click += (_, _) => BeginInlineEdit("volume");

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
        lblOpacityValue.Click += (_, _) => BeginInlineEdit("opacity");

        // 인라인 편집 TextBox
        tbInlineEdit.KeyDown += InlineEdit_KeyDown;
        tbInlineEdit.LostFocus += (_, _) => CommitInlineEdit();

        // V버튼: 사이트 목록 + 사이트 추가 + 항상위/테두리숨김 드롭다운
        // (구버전의 menuStrip/siteList를 대체 — 매 클릭마다 최신 _config.Sites 기준으로 새로 빌드)
        TopMost = _config.TopMost;
        btnMenu.Click += (_, _) => OpenSiteMenu();

        // 알람 타이머 초기화 (시작은 안 함)
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
        UpdateNextAlarmLabel();

        timer.Tick += Timer_Tick;
    }

    // ===== 타이머 시작/정지 토글 =====
    private void ToggleTimer()
    {
        _timerRunning = !_timerRunning;

        if (_timerRunning)
        {
            // 시작 시 항상 리셋
            for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
            for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
            timer.Start();
        }
        else
        {
            timer.Stop();
        }

        timerButton.Active = _timerRunning;
        UpdateNextAlarmLabel();
    }

    // ===== 인라인 숫자 편집 =====
    private void BeginInlineEdit(string target)
    {
        _inlineEditTarget = target;
        int value = target == "volume" ? sliderVolume.Value : sliderOpacity.Value;
        Label lbl = target == "volume" ? lblVolumeValue : lblOpacityValue;

        tbInlineEdit.Text = value.ToString();
        tbInlineEdit.Location = new Point(lbl.Left, lbl.Top - 1);
        tbInlineEdit.Visible = true;
        tbInlineEdit.Focus();
        tbInlineEdit.SelectAll();
    }

    private void CommitInlineEdit()
    {
        tbInlineEdit.Visible = false;
        if (_inlineEditTarget is null) return;

        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        if (_inlineEditTarget == "volume")
            sliderVolume.Value = Math.Clamp(val, sliderVolume.Minimum, sliderVolume.Maximum);
        else
            sliderOpacity.Value = Math.Clamp(val, sliderOpacity.Minimum, sliderOpacity.Maximum);

        _inlineEditTarget = null;
    }

    private void InlineEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { CommitInlineEdit(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape) { tbInlineEdit.Visible = false; _inlineEditTarget = null; }
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

    // 🔴 버그 수정: about:blank 또는 null Source일 때 Uri 생성 예외가 나던 부분 → null 체크 + try/catch
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
        // 정지 상태면 Tick 무시 (이중 안전장치)
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
            toolTip.SetToolTip(timerButton, "클릭하여 타이머 시작");
            return;
        }

        int? min = null;
        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i])
                min = min is null ? _remaining[i] : Math.Min(min.Value, _remaining[i]);
        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled && _config.CustomAlarms[i].Tick > 0)
                min = min is null ? _customRemaining[i] : Math.Min(min.Value, _customRemaining[i]);

        string tip = min is null
            ? "다음 알람: --:--:--"
            : $"다음 알람: {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
        toolTip.SetToolTip(timerButton, tip);
    }

    // 🔴 버그 수정: TopMost=true 상태에서 ShowDialog 호출 시 자식 창이 부모 뒤에 가려지는 문제 → 임시로 false 설정
    private void OpenAlarmSettings()
    {
        bool wasTopMost = TopMost;
        TopMost = false;
        using var form = new AlarmForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK;
        TopMost = wasTopMost;
        if (!ok) return;

        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i]) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled) _customRemaining[i] = _config.CustomAlarms[i].Tick;

        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        UpdateNextAlarmLabel();
    }

    // ===== V버튼 드롭다운 (사이트 목록 + 사이트 추가 + 항상위/테두리숨김) =====
    // 매 클릭마다 _config.Sites 기준으로 새로 빌드하므로, 사이트 추가/삭제 후 별도 갱신 로직이 필요 없음.
    private void OpenSiteMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new SiteMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = Color.FromArgb(40, 40, 40)
        };

        string currentUrl = webView.CoreWebView2?.Source ?? _config.LatestUrl;

        // 모든 항목의 폭을 가장 긴 텍스트 기준으로 통일 → ✕ 아이콘이 항상 같은 위치(우측 끝)에 정렬됨
        int textWidth = TextRenderer.MeasureText("＋ 사이트 추가", menu.Font).Width;
        foreach (var site in _config.Sites)
            textWidth = Math.Max(textWidth, TextRenderer.MeasureText(site.Name, menu.Font).Width);
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("테두리 숨김 (포커스 아웃)", menu.Font).Width);
        int itemWidth = textWidth + 70;

        foreach (var site in _config.Sites)
        {
            var item = new SiteMenuItem(site)
            {
                AutoSize = false,
                Width = itemWidth,
                Checked = string.Equals(site.Url, currentUrl, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => NavigateToSite(site);
            item.DeleteRequested += (_, _) =>
            {
                _config.Sites.Remove(site);
                _config.Save();
                menu.Items.Remove(item);
            };
            menu.Items.Add(item);
        }

        if (_config.Sites.Count > 0)
            menu.Items.Add(new ToolStripSeparator());

        var addItem = new ToolStripMenuItem("＋ 사이트 추가") { AutoSize = false, Width = itemWidth };
        addItem.Click += (_, _) => AddSite();
        menu.Items.Add(addItem);

        menu.Items.Add(new ToolStripSeparator());

        var topmostItem = new ToolStripMenuItem("항상 위")
        {
            CheckOnClick = true,
            Checked = _config.TopMost,
            AutoSize = false,
            Width = itemWidth
        };
        topmostItem.CheckedChanged += (_, _) =>
        {
            TopMost = topmostItem.Checked;
            _config.TopMost = TopMost;
            _config.Save();
        };
        menu.Items.Add(topmostItem);

        var hideBorderItem = new ToolStripMenuItem("테두리 숨김 (포커스 아웃)")
        {
            CheckOnClick = true,
            Checked = _config.IsHideWindowBorderOnFocusOut,
            AutoSize = false,
            Width = itemWidth
        };
        hideBorderItem.CheckedChanged += (_, _) =>
        {
            _config.IsHideWindowBorderOnFocusOut = hideBorderItem.Checked;
            _config.Save();
        };
        menu.Items.Add(hideBorderItem);

        menu.Show(btnMenu, new Point(0, btnMenu.Height));
    }

    // ===== 사이트 목록 =====
    // 🔴 버그 수정: OpenAlarmSettings와 동일한 TopMost 자식창 버그 수정 적용
    private void AddSite()
    {
        bool wasTopMost = TopMost;
        TopMost = false;
        using var form = new SiteForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK && form.Result is not null;
        TopMost = wasTopMost;
        if (!ok) return;

        _config.Sites.Add(form.Result!);
        _config.Save();
    }

    private void NavigateToSite(Site site)
    {
        if (webView.CoreWebView2 is not null)
            webView.CoreWebView2.Navigate(site.Url);
    }

    // V버튼 드롭다운 전용 사이트 항목: 일반 ToolStripMenuItem이지만 우측 끝(✕ 영역) 클릭 시
    // 이동 대신 삭제를 수행하도록 OnMouseUp을 가로챈다. SiteMenuRenderer가 그리는 ✕ 아이콘과 짝을 이룸.
    private sealed class SiteMenuItem : ToolStripMenuItem
    {
        public const int DeleteZoneWidth = 28;

        public Site Site { get; }
        public event EventHandler? DeleteRequested;

        public SiteMenuItem(Site site) : base(site.Name)
        {
            Site = site;
        }

        protected override void OnMouseUp(MouseEventArgs mea)
        {
            if (mea.Button == MouseButtons.Left && mea.X >= Width - DeleteZoneWidth)
            {
                DeleteRequested?.Invoke(this, EventArgs.Empty);
                return; // ✕ 영역 클릭 시 기본 Click(사이트 이동) 동작은 발생시키지 않음
            }
            base.OnMouseUp(mea);
        }
    }

    // DarkMenuRenderer를 확장해 SiteMenuItem 우측에 ✕(삭제) 아이콘만 추가로 그려주는 렌더러.
    // 다크 테마 배경/체크 표시는 DarkMenuRenderer 로직을 그대로 사용한다.
    private sealed class SiteMenuRenderer : DarkMenuRenderer
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            base.OnRenderItemText(e);

            if (e.Item is not SiteMenuItem item) return;

            var g = e.Graphics;
            int cx = item.Width - SiteMenuItem.DeleteZoneWidth / 2;
            int cy = item.Height / 2;
            const int r = 4;

            Color color = item.Selected ? Color.FromArgb(220, 90, 80) : Color.FromArgb(150, 150, 150);
            using var pen = new Pen(color, 1.4f);
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
        }
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
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = false;
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

    // 🟢 추가: 창 가장자리에서 리사이즈 커서(↔↕) 시각적 피드백
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
