using JHP.Api;
using JHP.Asset;
using JHP.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

namespace JHP;

public partial class Form1 : Form
{
    // ─── 알람 상수 ──────────────────────────────────────────────────────────
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly int[] AlarmTicks = { 7200, 3600, 1800, 1200, 900, 600, 100, 55 };
    private static readonly string[] AlarmLabels =
    {
        "재획비 (2시간)", "경쿠 (1시간)", "열변/경쿠/오니오 (30분)",
        "오니오 버프 (20분)", "경쿠 (15분)", "오니오 버프 (10분)",
        "메소 히수 (100초)", "파우티 (55초)"
    };

    private const string CurrentVersion = "v1.0.0";
    private const int TITLE_HEIGHT = 32;

    // ─── 레이아웃 컨트롤 ────────────────────────────────────────────────────
    private Panel _titleBar = null!;
    private SplitContainer _split = null!;
    private Panel _alarmPanel = null!;
    private WebView2 _webView = null!;

    // ─── 타이틀바 컨트롤 ────────────────────────────────────────────────────
    private ControlButton _btnClose = null!, _btnMax = null!, _btnMin = null!;
    private ComboBox _cmbSite = null!;
    private CheckBox _cbTopMost = null!, _cbHideBorder = null!;

    // ─── 알람 패널 컨트롤 ───────────────────────────────────────────────────
    private readonly CheckBox[] _alarmChecks   = new CheckBox[8];
    private readonly TextBox[]  _customNames   = new TextBox[3];
    private readonly NumericUpDown[] _customTicks   = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];
    private ComboBox _cmbAlarmFile = null!;
    private NSlider  _sliderVolume = null!, _sliderRate = null!;
    private Label    _lblVolume    = null!;
    private CheckBox _cbTts        = null!;

    // ─── 타이머 ─────────────────────────────────────────────────────────────
    private System.Windows.Forms.Timer _timer = null!;
    private DateTime _timerStartTime;
    private bool _timerRunning = false;

    // ────────────────────────────────────────────────────────────────────────
    public Form1()
    {
        InitializeComponent();
    }

    // ===== Form1_Load ========================================================
    private async void Form1_Load(object? sender, EventArgs e)
    {
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += Timer_Tick;

        InitControls();
        LoadConfigToControls();
        LoadSites();
        LoadAlarmFiles();

        // SplitContainer가 실제로 표시된 뒤 패널 크기 확정
        Shown += (_, _) =>
            _split.SplitterDistance = Math.Max(_split.Width - 241, 100);

        _ = InitWV();
        _ = CheckUpdateAsync();
    }

    // ===== UI 생성 ===========================================================

    private void InitControls()
    {
        SuspendLayout();

        // ── 타이틀바 ─────────────────────────────────────────────────────────
        _titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = TITLE_HEIGHT,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        _btnClose = new ControlButton { Type = ControlButton.ButtonType.Close,    Dock = DockStyle.Right };
        _btnMax   = new ControlButton { Type = ControlButton.ButtonType.Maximize, Dock = DockStyle.Right };
        _btnMin   = new ControlButton { Type = ControlButton.ButtonType.Minimize, Dock = DockStyle.Right };

        _cmbSite = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.FromArgb(40, 40, 40),
            ForeColor     = Color.White,
            Width = 180,
            Left  = 8,
            Top   = (TITLE_HEIGHT - 22) / 2
        };

        _cbTopMost = new CheckBox
        {
            Text      = "항상 위",
            ForeColor = Color.LightGray,
            AutoSize  = true,
            Left = 200,
            Top  = (TITLE_HEIGHT - 17) / 2
        };

        _cbHideBorder = new CheckBox
        {
            Text      = "테두리 숨기기",
            ForeColor = Color.LightGray,
            AutoSize  = true,
            Left = 278,
            Top  = (TITLE_HEIGHT - 17) / 2
        };

        _titleBar.Controls.AddRange(new Control[]
            { _btnClose, _btnMax, _btnMin, _cmbSite, _cbTopMost, _cbHideBorder });

        // ── 본문 SplitContainer ──────────────────────────────────────────────
        _split = new SplitContainer
        {
            Dock            = DockStyle.Fill,
            FixedPanel      = FixedPanel.Panel2,
            SplitterWidth   = 1,
            IsSplitterFixed = true,
            BackColor       = Color.FromArgb(20, 20, 20)
        };

        // Panel1: WebView2
        _webView = new WebView2 { Dock = DockStyle.Fill };
        _split.Panel1.Controls.Add(_webView);

        // Panel2: 알람 패널 (240px 고정)
        _alarmPanel = BuildAlarmPanel();
        _split.Panel2.Controls.Add(_alarmPanel);
        _split.Panel2MinSize = 240;

        Controls.AddRange(new Control[] { _split, _titleBar });

        ConnectEvents();
        ResumeLayout(true);
    }

    // ── 알람 패널 빌드 ────────────────────────────────────────────────────────
    private Panel BuildAlarmPanel()
    {
        var panel = new Panel
        {
            Dock       = DockStyle.Fill,
            BackColor  = Color.FromArgb(28, 28, 28),
            AutoScroll = true,
            Padding    = new Padding(8)
        };
        int y = 8;

        // 고정 알람 8개
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text      = AlarmLabels[i],
                ForeColor = Color.White,
                Left = 8, Top = y, Width = 220, Height = 22
            };
            panel.Controls.Add(_alarmChecks[i]);
            y += 24;
        }

        y += 4; AddSep(panel, y); y += 10;

        // 커스텀 알람 3슬롯
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 8, Top = y + 2, Width = 18 };
            _customNames[i]   = new TextBox
            {
                Left = 30, Top = y, Width = 90,
                BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White,
                PlaceholderText = "이름"
            };
            _customTicks[i] = new NumericUpDown
            {
                Left = 124, Top = y, Width = 64,
                Minimum = 0, Maximum = 99999,
                BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White
            };
            panel.Controls.Add(new Label
                { Text = "초", Left = 192, Top = y + 3, Width = 20, ForeColor = Color.LightGray });
            panel.Controls.AddRange(new Control[]
                { _customEnabled[i], _customNames[i], _customTicks[i] });
            y += 30;
        }

        y += 4; AddSep(panel, y); y += 10;

        // 알람 파일
        panel.Controls.Add(new Label
            { Text = "알람 파일", Left = 8, Top = y, Width = 60, ForeColor = Color.LightGray });
        _cmbAlarmFile = new ComboBox
        {
            Left = 72, Top = y - 2, Width = 152,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.FromArgb(40, 40, 40), ForeColor = Color.White
        };
        panel.Controls.Add(_cmbAlarmFile);
        y += 30;

        // 볼륨
        panel.Controls.Add(new Label
            { Text = "볼륨", Left = 8, Top = y + 4, Width = 30, ForeColor = Color.LightGray });
        _sliderVolume = new NSlider { Left = 42, Top = y, Width = 150, Height = 20, Minimum = 0, Maximum = 100 };
        _lblVolume    = new Label   { Left = 196, Top = y + 3, Width = 30, ForeColor = Color.White, Text = "50" };
        panel.Controls.AddRange(new Control[] { _sliderVolume, _lblVolume });
        y += 28;

        // TTS 출력
        _cbTts = new CheckBox { Text = "TTS 출력", Left = 8, Top = y, Width = 100, ForeColor = Color.White };
        panel.Controls.Add(_cbTts);
        y += 26;

        // TTS 속도
        panel.Controls.Add(new Label
            { Text = "TTS 속도", Left = 8, Top = y + 4, Width = 55, ForeColor = Color.LightGray });
        _sliderRate = new NSlider { Left = 68, Top = y, Width = 124, Height = 20, Minimum = -10, Maximum = 10 };
        panel.Controls.Add(_sliderRate);
        y += 28;

        AddSep(panel, y); y += 10;

        // 알람 설정 버튼
        var btnAlarm = new Button
        {
            Text      = "알람 설정 열기",
            Left = 8, Top = y, Width = 218, Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145), ForeColor = Color.White,
            Cursor    = Cursors.Hand
        };
        btnAlarm.FlatAppearance.BorderSize = 0;
        btnAlarm.Click += BtnAlarmSettings_Click;
        panel.Controls.Add(btnAlarm);

        return panel;
    }

    private static void AddSep(Panel p, int y) =>
        p.Controls.Add(new Panel
            { Left = 8, Top = y, Width = 224, Height = 1, BackColor = Color.FromArgb(55, 55, 55) });

    // ── 이벤트 연결 ───────────────────────────────────────────────────────────
    private void ConnectEvents()
    {
        _btnClose.Click += (_, _) => Close();
        _btnMin.Click   += (_, _) => WindowState = FormWindowState.Minimized;
        _btnMax.Click   += (_, _) => WindowState =
            WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        SizeChanged     += (_, _) => _btnMax?.Invalidate();

        _cbTopMost.CheckedChanged    += (_, _) =>
        {
            TopMost = _cbTopMost.Checked;
            Config.Instance.TopMost = _cbTopMost.Checked;
        };
        _cbHideBorder.CheckedChanged += (_, _) =>
            Config.Instance.IsHideWindowBorderOnFocusOut = _cbHideBorder.Checked;

        _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;

        _sliderVolume.ValueChanged += (_, _) =>
        {
            Config.Instance.Volume = _sliderVolume.Value;
            Synth.Instance.SetVolume(_sliderVolume.Value);
            _lblVolume.Text = _sliderVolume.Value.ToString();
        };
        _sliderRate.ValueChanged += (_, _) =>
        {
            Config.Instance.Rate = _sliderRate.Value;
            Synth.Instance.SetRate(_sliderRate.Value);
        };
        _cbTts.CheckedChanged += (_, _) => Config.Instance.Tts = _cbTts.Checked;
        _cmbAlarmFile.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbAlarmFile.SelectedItem is string name)
                Config.Instance.AlarmName = name;
        };

        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            _alarmChecks[idx].CheckedChanged += (_, _) =>
                Config.Instance.AlarmEnabled[idx] = _alarmChecks[idx].Checked;
        }
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            _customNames[idx].TextChanged     += (_, _) =>
                Config.Instance.CustomAlarms[idx].Name = _customNames[idx].Text;
            _customTicks[idx].ValueChanged    += (_, _) =>
                Config.Instance.CustomAlarms[idx].Tick = (int)_customTicks[idx].Value;
            _customEnabled[idx].CheckedChanged += (_, _) =>
                Config.Instance.CustomAlarms[idx].Enabled = _customEnabled[idx].Checked;
        }

        // 포커스 아웃 시 타이틀바 숨김 (테두리 숨기기 옵션)
        Activated  += (_, _) => { if (_titleBar != null) _titleBar.Visible = true; };
        Deactivate += (_, _) =>
        {
            if (Config.Instance.IsHideWindowBorderOnFocusOut)
                _titleBar.Visible = false;
        };
    }

    // ===== Config 동기화 =====================================================

    private void LoadConfigToControls()
    {
        var cfg = Config.Instance;

        if (cfg.IsMaximize)
        {
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            Width  = Math.Max(cfg.Width,  MinimumSize.Width);
            Height = Math.Max(cfg.Height, MinimumSize.Height);
            // 다중 모니터 해상도 변경 등 화면 밖 이탈 방지
            var screen = Screen.FromPoint(new Point(cfg.X, cfg.Y));
            Left = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.X : 100;
            Top  = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.Y : 100;
        }
        Opacity = Math.Clamp(cfg.Opacity, 0.2, 1.0);
        TopMost = cfg.TopMost;
        _cbTopMost.Checked    = cfg.TopMost;
        _cbHideBorder.Checked = cfg.IsHideWindowBorderOnFocusOut;

        for (int i = 0; i < 8; i++)
            _alarmChecks[i].Checked = cfg.AlarmEnabled[i];
        for (int i = 0; i < 3; i++)
        {
            _customNames[i].Text      = cfg.CustomAlarms[i].Name;
            _customTicks[i].Value     = Math.Clamp(cfg.CustomAlarms[i].Tick, 0, 99999);
            _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }

        _sliderVolume.Value = cfg.Volume;
        _lblVolume.Text     = cfg.Volume.ToString();
        _sliderRate.Value   = cfg.Rate;
        _cbTts.Checked      = cfg.Tts;

        Synth.Instance.SetVolume(cfg.Volume);
        Synth.Instance.SetRate(cfg.Rate);
    }

    private void SaveConfigFromControls()
    {
        var cfg = Config.Instance;
        if (WindowState == FormWindowState.Normal)
        {
            cfg.Width  = Width;
            cfg.Height = Height;
            cfg.X      = Left;
            cfg.Y      = Top;
        }
        cfg.IsMaximize = WindowState == FormWindowState.Maximized;
        cfg.TopMost    = TopMost;
        cfg.Opacity    = Opacity;

        for (int i = 0; i < 8; i++)
            cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name    = _customNames[i].Text;
            cfg.CustomAlarms[i].Tick    = (int)_customTicks[i].Value;
            cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
        }
        cfg.AlarmName = _cmbAlarmFile.SelectedItem?.ToString() ?? cfg.AlarmName;
        cfg.Tts    = _cbTts.Checked;
        cfg.Volume = _sliderVolume.Value;
        cfg.Rate   = _sliderRate.Value;
        try { cfg.LatestUrl = _webView.CoreWebView2?.Source ?? ""; } catch { }
        cfg.Save();
    }

    // ===== Form 이벤트 =======================================================

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer?.Stop();
        Synth.Instance.Stop();    // NAudio 리소스 해제
        SaveConfigFromControls();
        _webView?.Dispose();      // WebView2 리소스 해제
        base.OnFormClosing(e);
    }

    // ===== WebView2 초기화 ===================================================

    private async Task InitWV()
    {
        try
        {
            // 캐시를 디스크에 저장 — 장시간 사용 시 메모리 누적 방지
            var env = await CoreWebView2Environment.CreateAsync(
                null,
                Path.Combine(AppContext.BaseDirectory, "cache"));
            await _webView.EnsureCoreWebView2Async(env);

            // 좌클릭 → 타이머 시작/재시작
            // WebView2는 자체 HWND를 쓰므로 WinForms MouseClick이 아닌
            // JS postMessage + WebMessageReceived 방식으로 클릭 감지
            _webView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.WebMessageAsString == "lclick")
                    BeginInvoke(() => StartTimer());
            };

            // NavigationCompleted에서 사이트별 JS 주입
            _webView.CoreWebView2.NavigationCompleted += (_, _) => InjectJS();

            string url = Config.Instance.LatestUrl;
            if (string.IsNullOrEmpty(url) && Config.Instance.Sites.Count > 0)
                url = Config.Instance.Sites[0].Url;
            if (!string.IsNullOrEmpty(url))
                _webView.CoreWebView2.Navigate(url);
        }
        catch
        {
            MessageBox.Show(
                "WebView2 런타임이 설치되지 않았습니다.\n" +
                "https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/\n" +
                "에서 설치 후 다시 실행해주세요.",
                "JHP — 런타임 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void InjectJS()
    {
        if (_webView.CoreWebView2 is null) return;
        string host;
        try { host = new Uri(_webView.CoreWebView2.Source).Host; }
        catch { return; }

        try
        {
            // 페이지 클릭 → 타이머 메시지 (중복 등록 방지)
            await _webView.CoreWebView2.ExecuteScriptAsync(
                "if (!window.__jhpClickRegistered__) {" +
                "  window.__jhpClickRegistered__ = true;" +
                "  document.addEventListener('click', function() {" +
                "    window.chrome.webview.postMessage('lclick');" +
                "  });" +
                "}");

            if (host.Contains("laftel.net"))
                await _webView.CoreWebView2.ExecuteScriptAsync(UserScripts.LaftelSkipNext);
            else if (host.Contains("netflix.com"))
                await _webView.CoreWebView2.ExecuteScriptAsync(UserScripts.NetflixSkipNext);
        }
        catch { }
    }

    // ===== 업데이트 체크 =====================================================

    private async Task CheckUpdateAsync()
    {
        try
        {
            var (hasUpdate, latestTag, url) = await UpdateChecker.Check(CurrentVersion);
            if (hasUpdate)
            {
                var ans = MessageBox.Show(
                    $"새 버전({latestTag})이 있습니다.\n다운로드 페이지로 이동할까요?",
                    "JHP 업데이트", MessageBoxButtons.YesNo);
                if (ans == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
        }
        catch { }
    }

    // ===== 타이머 ============================================================

    private void StartTimer()
    {
        _timerStartTime = DateTime.Now;
        _timerRunning   = true;
        _timer.Start(); // 이미 실행 중이면 무시됨 — 시작 시각 갱신이 핵심
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_timerRunning) return;
        // DateTime 기반 — UI 블로킹으로 인한 tick 누락 오차 없음
        int elapsed = (int)(DateTime.Now - _timerStartTime).TotalSeconds;

        for (int i = 0; i < 8; i++)
            if (_alarmChecks[i].Checked && elapsed == AlarmTicks[i])
                FireAlarm(AlarmLabels[i]);

        for (int i = 0; i < 3; i++)
        {
            var ca = Config.Instance.CustomAlarms[i];
            if (ca.Enabled && ca.Tick > 0 && elapsed == ca.Tick)
                FireAlarm(string.IsNullOrEmpty(ca.Name) ? "알람" : ca.Name);
        }
    }

    private void FireAlarm(string label)
    {
        if (Config.Instance.Tts)
            Synth.Instance.TTS(label, Config.Instance.Volume, Config.Instance.Rate);
        else
            Synth.Instance.Ring(Config.Instance.AlarmName, Config.Instance.Volume);
    }

    // ===== 알람 설정 버튼 ====================================================

    private void BtnAlarmSettings_Click(object? sender, EventArgs e)
    {
        bool was = TopMost;
        TopMost = false;     // TopMost=true 상태에서 자식 창 안 보이는 버그 방지
        using var af = new AlarmForm();
        af.ShowDialog(this); // BtnOk_Click 내부에서 Config.Save() 직접 호출
        TopMost = was;
        LoadAlarmFiles();
        LoadConfigToControls();
    }

    // ===== 사이트 드롭다운 ===================================================

    private void LoadSites()
    {
        _cmbSite.SelectedIndexChanged -= CmbSite_SelectedIndexChanged;
        _cmbSite.Items.Clear();
        foreach (var site in Config.Instance.Sites)
            _cmbSite.Items.Add(site.Name);
        _cmbSite.Items.Add("＋ 사이트 추가");

        if (!string.IsNullOrEmpty(Config.Instance.DefaultSite))
            _cmbSite.SelectedItem = Config.Instance.DefaultSite;
        else if (Config.Instance.Sites.Count > 0)
            _cmbSite.SelectedIndex = 0;

        _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;
    }

    private void CmbSite_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = _cmbSite.SelectedItem?.ToString();
        if (selected == "＋ 사이트 추가")
        {
            bool was = TopMost;
            TopMost = false;
            using var sf = new SiteForm();
            if (sf.ShowDialog(this) == DialogResult.OK && sf.Result != null)
            {
                Config.Instance.Sites.Add(sf.Result);
                Config.Instance.Save();
            }
            TopMost = was;
            LoadSites();
            return;
        }
        var site = Config.Instance.Sites.FirstOrDefault(s => s.Name == selected);
        if (site != null && _webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.Navigate(site.Url);
            Config.Instance.DefaultSite = site.Name;
        }
    }

    // ===== 알람 파일 목록 ====================================================

    private void LoadAlarmFiles()
    {
        _cmbAlarmFile.Items.Clear();
        string dir = Path.Combine(AppContext.BaseDirectory, "alarm");
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.mp3"))
                _cmbAlarmFile.Items.Add(Path.GetFileName(f));

        if (_cmbAlarmFile.Items.Count == 0)
            _cmbAlarmFile.Items.Add("(파일 없음)");

        if (!string.IsNullOrEmpty(Config.Instance.AlarmName) &&
            _cmbAlarmFile.Items.Contains(Config.Instance.AlarmName))
            _cmbAlarmFile.SelectedItem = Config.Instance.AlarmName;
        else if (_cmbAlarmFile.Items.Count > 0)
            _cmbAlarmFile.SelectedIndex = 0;
    }

    // ===== 창 이동 + 엣지 리사이즈 (FormBorderStyle.None) ====================

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCAPTION    = 2;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12;
        const int HTTOPLEFT = 13, HTTOPRIGHT = 14;
        const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            var pt = PointToClient(new Point(
                unchecked((short)(m.LParam.ToInt32() & 0xFFFF)),
                unchecked((short)((m.LParam.ToInt32() >> 16) & 0xFFFF))));

            // 엣지 리사이즈 (ReSize static class)
            var pos = ReSize.GetMousePosition(this, pt);
            if (pos != ReSize.MousePosition.None)
            {
                m.Result = pos switch
                {
                    ReSize.MousePosition.Left        => (IntPtr)HTLEFT,
                    ReSize.MousePosition.Right       => (IntPtr)HTRIGHT,
                    ReSize.MousePosition.Top         => (IntPtr)HTTOP,
                    ReSize.MousePosition.Bottom      => (IntPtr)HTBOTTOM,
                    ReSize.MousePosition.TopLeft     => (IntPtr)HTTOPLEFT,
                    ReSize.MousePosition.TopRight    => (IntPtr)HTTOPRIGHT,
                    ReSize.MousePosition.BottomLeft  => (IntPtr)HTBOTTOMLEFT,
                    ReSize.MousePosition.BottomRight => (IntPtr)HTBOTTOMRIGHT,
                    _                                => IntPtr.Zero
                };
                return;
            }

            // 타이틀바 드래그 이동 (ControlButton 3개 = 138px 우측 제외)
            if (pt.Y >= 0 && pt.Y < TITLE_HEIGHT && pt.X < Width - 138)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
