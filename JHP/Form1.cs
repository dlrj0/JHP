using JHP.Api;
using JHP.Asset;
using JHP.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JHP;

public partial class Form1 : Form
{
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly int[] AlarmSeconds =
        [7200, 3600, 1800, 1200, 900, 600, 100, 55];

    private static readonly string[] AlarmLabels =
    [
        "재획비 (2시간)", "경쿠 (1시간)", "열변/경쿠/오니오 (30분)",
        "오니오 버프 (20분)", "경쿠 (15분)", "오니오 버프 (10분)",
        "메소 히수 (100초)", "파우티 (55초)"
    ];

    private const string CurrentVersion = "v1.0.0";
    private const int    TITLE_HEIGHT   = 32;

    private readonly Config _config          = Config.Instance;
    private readonly int[]  _remaining       = new int[8];
    private readonly int[]  _customRemaining = new int[3];
    private bool _timerRunning;

    // ─────────────────────────────────────────────
    //  생성자
    // ─────────────────────────────────────────────
    public Form1()
    {
        InitializeComponent();
        Load         += Form1_Load;
        FormClosing  += Form1_FormClosing;
        Activated    += Form1_Activated;
        Deactivate   += Form1_Deactivate;
    }

    // ─────────────────────────────────────────────
    //  LOAD
    // ─────────────────────────────────────────────
    private async void Form1_Load(object? sender, EventArgs e)
    {
        InitControls();          // 컨트롤 생성 및 배치
        LoadConfigToControls();  // Config → 컨트롤 값 반영
        LoadSites();             // 사이트 목록 로드
        LoadAlarmFiles();        // alarm/ 폴더 mp3 목록

        timer          = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick    += Timer_Tick;
        timer.Start();           // 타이머는 항상 구동, _timerRunning 플래그로 카운트 제어
        ResetTimerState();

        // SplitContainer 크기는 폼 레이아웃 완료 후에 알 수 있으므로 BeginInvoke 사용
        BeginInvoke(() =>
        {
            if (splitMain.Width > 260)
                splitMain.SplitterDistance = splitMain.Width - 240;
        });

        await InitWV();
        _ = CheckUpdateAsync();
    }

    // ─────────────────────────────────────────────
    //  UI 구성 — InitControls
    // ─────────────────────────────────────────────
    private void InitControls()
    {
        SuspendLayout();

        // ══════════════════════════════════════
        //  타이틀바
        // ══════════════════════════════════════
        pnlTitleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = TITLE_HEIGHT,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        pnlTitleBar.MouseDown += TitleBar_MouseDown;

        // 창 컨트롤 버튼 — Dock=Right 사용 (Location 고정 금지: 창 크기 변경 시 잘림)
        btnClose    = new ControlButton { Type = ControlButton.ButtonType.Close,    Dock = DockStyle.Right };
        btnMaximize = new ControlButton { Type = ControlButton.ButtonType.Maximize, Dock = DockStyle.Right };
        btnMinimize = new ControlButton { Type = ControlButton.ButtonType.Minimize, Dock = DockStyle.Right };

        btnClose.Click    += (_, _) => Close();
        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        SizeChanged += (_, _) => btnMaximize?.Invalidate();

        // 사이트 선택 드롭다운
        cmbSite = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.FromArgb(40, 40, 40),
            ForeColor     = Color.White,
            Width         = 160,
            Left          = 8,
            Top           = (TITLE_HEIGHT - 22) / 2
        };
        cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;

        // 항상 위 체크박스
        cbTopMost = new CheckBox
        {
            Text      = "항상 위",
            ForeColor = Color.LightGray,
            AutoSize  = true,
            Left      = 176,
            Top       = (TITLE_HEIGHT - 17) / 2
        };
        cbTopMost.CheckedChanged += (_, _) =>
        {
            TopMost          = cbTopMost.Checked;
            _config.TopMost  = cbTopMost.Checked;
            _config.Save();
        };

        // 테두리 숨기기 체크박스
        cbHideBorder = new CheckBox
        {
            Text      = "테두리 숨기기",
            ForeColor = Color.LightGray,
            AutoSize  = true,
            Left      = 258,
            Top       = (TITLE_HEIGHT - 17) / 2
        };
        cbHideBorder.CheckedChanged += (_, _) =>
        {
            _config.IsHideWindowBorderOnFocusOut = cbHideBorder.Checked;
            _config.Save();
        };

        // 다음 알람 라벨 — 좌클릭: 타이머 시작/재시작 / 우클릭: 컨텍스트 메뉴
        lblNextAlarm = new Label
        {
            Text      = "⏱ 클릭하여 시작",
            ForeColor = Color.FromArgb(150, 200, 255),
            AutoSize  = true,
            Left      = 390,
            Top       = (TITLE_HEIGHT - 15) / 2,
            Cursor    = Cursors.Hand
        };
        lblNextAlarm.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) StartTimer();
        };
        SetupTimerContextMenu(lblNextAlarm);

        // 타이틀바에 컨트롤 추가
        // ⚠️ Dock=Right 버튼은 먼저 추가할수록 오른쪽에 위치 (Close → Max → Min 순서)
        pnlTitleBar.Controls.Add(btnClose);
        pnlTitleBar.Controls.Add(btnMaximize);
        pnlTitleBar.Controls.Add(btnMinimize);
        pnlTitleBar.Controls.Add(cmbSite);
        pnlTitleBar.Controls.Add(cbTopMost);
        pnlTitleBar.Controls.Add(cbHideBorder);
        pnlTitleBar.Controls.Add(lblNextAlarm);

        // ══════════════════════════════════════
        //  메인 영역: WebView2 (좌) + 알람패널 (우)
        // ══════════════════════════════════════
        splitMain = new SplitContainer
        {
            Dock            = DockStyle.Fill,
            FixedPanel      = FixedPanel.Panel2,   // 알람패널 고정 240px
            SplitterWidth   = 2,
            IsSplitterFixed = true,
            BackColor       = Color.FromArgb(50, 50, 50)  // 구분선 색
        };
        splitMain.Panel2MinSize = 240;

        // WebView2
        webView = new WebView2
        {
            Dock                   = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(20, 20, 20)
        };
        // 좌클릭 → 타이머 시작/재시작
        webView.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) StartTimer();
        };
        splitMain.Panel1.Controls.Add(webView);

        // 알람 패널
        pnlAlarm = BuildAlarmPanel();
        splitMain.Panel2.Controls.Add(pnlAlarm);

        // ══════════════════════════════════════
        //  Form에 추가 — 순서가 매우 중요!
        //  Dock=Top 컨트롤은 나중에 추가될수록 위에 표시됨
        //  Fill 먼저, Top 마지막
        // ══════════════════════════════════════
        Controls.Add(splitMain);    // Fill — 먼저 추가
        Controls.Add(pnlTitleBar);  // Top  — 마지막 추가 = 최상단 표시

        ResumeLayout(true);
    }

    // ─────────────────────────────────────────────
    //  알람 패널 구성 (우측 240px 고정)
    // ─────────────────────────────────────────────
    private Panel BuildAlarmPanel()
    {
        var panel = new Panel
        {
            Dock       = DockStyle.Fill,
            BackColor  = Color.FromArgb(28, 28, 28),
            AutoScroll = true
        };

        const int x = 8;
        int y = 8;

        // ── 고정 알람 체크박스 8개 ──
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            alarmChecks[i] = new CheckBox
            {
                Text      = AlarmLabels[i],
                ForeColor = Color.White,
                Left = x, Top = y,
                Width = 224, Height = 22,
                Cursor = Cursors.Hand
            };
            alarmChecks[i].CheckedChanged += (_, _) =>
                _config.AlarmEnabled[idx] = alarmChecks[idx].Checked;
            panel.Controls.Add(alarmChecks[i]);
            y += 24;
        }

        y += 4;
        AddSeparator(panel, y); y += 10;

        // ── 커스텀 알람 3슬롯 ──
        for (int i = 0; i < 3; i++)
        {
            int idx = i;

            customEnabled[i] = new CheckBox
            {
                Left = x, Top = y + 2, Width = 18, Height = 18
            };
            customEnabled[i].CheckedChanged += (_, _) =>
                _config.CustomAlarms[idx].Enabled = customEnabled[idx].Checked;

            customNames[i] = new TextBox
            {
                Left            = x + 22, Top = y,
                Width           = 88,
                BackColor       = Color.FromArgb(40, 40, 40),
                ForeColor       = Color.White,
                BorderStyle     = BorderStyle.FixedSingle,
                PlaceholderText = "이름"
            };
            customNames[i].TextChanged += (_, _) =>
                _config.CustomAlarms[idx].Name = customNames[idx].Text;

            customTicks[i] = new NumericUpDown
            {
                Left = x + 116, Top = y, Width = 66,
                Minimum = 0, Maximum = 99999,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            customTicks[i].ValueChanged += (_, _) =>
                _config.CustomAlarms[idx].Tick = (int)customTicks[idx].Value;

            var lblSec = new Label
            {
                Text = "초", Left = x + 186, Top = y + 3,
                Width = 18, Height = 17, ForeColor = Color.LightGray
            };

            panel.Controls.AddRange([customEnabled[i], customNames[i], customTicks[i], lblSec]);
            y += 30;
        }

        y += 4;
        AddSeparator(panel, y); y += 10;

        // ── 알람 파일 선택 ──
        var lblFile = new Label
        {
            Text = "알람 파일", Left = x, Top = y + 3,
            Width = 56, Height = 17, ForeColor = Color.LightGray
        };
        cmbAlarmFile = new ComboBox
        {
            Left = x + 60, Top = y, Width = 162,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.FromArgb(40, 40, 40),
            ForeColor     = Color.White
        };
        cmbAlarmFile.SelectedIndexChanged += (_, _) =>
        {
            if (cmbAlarmFile.SelectedItem is string name)
                _config.AlarmName = name;
        };
        panel.Controls.Add(lblFile);
        panel.Controls.Add(cmbAlarmFile);
        y += 30;

        // ── 볼륨 슬라이더 ──
        var lblVolTitle = new Label
        {
            Text = "볼륨", Left = x, Top = y + 5,
            Width = 30, Height = 15, ForeColor = Color.LightGray
        };
        sliderVolume = new NSlider
        {
            Left = x + 36, Top = y, Width = 148, Height = 20,
            Minimum = 0, Maximum = 100
        };
        sliderVolume.ValueChanged += (_, _) =>
        {
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            lblVolumeValue.Text = sliderVolume.Value.ToString();
            _config.Save();
        };
        lblVolumeValue = new Label
        {
            Text = "50", Left = x + 190, Top = y + 3,
            Width = 28, Height = 17, ForeColor = Color.White
        };
        panel.Controls.Add(lblVolTitle);
        panel.Controls.Add(sliderVolume);
        panel.Controls.Add(lblVolumeValue);
        y += 28;

        // ── TTS 출력 체크박스 ──
        cbTts = new CheckBox
        {
            Text = "TTS 출력", Left = x, Top = y,
            Width = 90, Height = 22,
            ForeColor = Color.White, Cursor = Cursors.Hand
        };
        cbTts.CheckedChanged += (_, _) =>
        {
            _config.Tts = cbTts.Checked;
            _config.Save();
        };
        panel.Controls.Add(cbTts);
        y += 26;

        // ── TTS 속도 슬라이더 ──
        var lblRate = new Label
        {
            Text = "TTS 속도", Left = x, Top = y + 5,
            Width = 52, Height = 15, ForeColor = Color.LightGray
        };
        sliderRate = new NSlider
        {
            Left = x + 58, Top = y, Width = 126, Height = 20,
            Minimum = -10, Maximum = 10
        };
        sliderRate.ValueChanged += (_, _) =>
        {
            _config.Rate = sliderRate.Value;
            Synth.Instance.SetRate(sliderRate.Value);
            _config.Save();
        };
        panel.Controls.Add(lblRate);
        panel.Controls.Add(sliderRate);
        y += 30;

        AddSeparator(panel, y); y += 10;

        // ── 알람 설정 열기 버튼 ──
        btnAlarmSettings = new Button
        {
            Text      = "알람 설정 열기",
            Left = x, Top = y, Width = 220, Height = 28,
            FlatStyle               = FlatStyle.Flat,
            BackColor               = Color.FromArgb(55, 90, 145),
            ForeColor               = Color.White,
            Cursor                  = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btnAlarmSettings.FlatAppearance.BorderSize = 0;
        btnAlarmSettings.Click += (_, _) => OpenAlarmSettings();
        panel.Controls.Add(btnAlarmSettings);

        return panel;
    }

    private static void AddSeparator(Panel parent, int y)
    {
        parent.Controls.Add(new Panel
        {
            Left = 8, Top = y, Width = 224, Height = 1,
            BackColor = Color.FromArgb(55, 55, 55)
        });
    }

    private void SetupTimerContextMenu(Control target)
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };

        var itemStart = new ToolStripMenuItem("▶ 시작 / 재시작");
        var itemStop  = new ToolStripMenuItem("⏹ 정지");
        var itemAlarm = new ToolStripMenuItem("⏰ 알람 설정 열기");

        itemStart.Click += (_, _) => StartTimer();
        itemStop.Click  += (_, _) => StopTimer();
        itemAlarm.Click += (_, _) => OpenAlarmSettings();

        menu.Items.AddRange([itemStart, itemStop, new ToolStripSeparator(), itemAlarm]);
        target.ContextMenuStrip = menu;
    }

    // ─────────────────────────────────────────────
    //  Config 로드 / 저장
    // ─────────────────────────────────────────────
    private void LoadConfigToControls()
    {
        if (_config.IsMaximize)
        {
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            Width  = Math.Max(_config.Width  > 0 ? _config.Width  : 1100, MinimumSize.Width);
            Height = Math.Max(_config.Height > 0 ? _config.Height : 700,  MinimumSize.Height);

            if (IsOnScreen(_config.X, _config.Y))
                Location = new Point(_config.X, _config.Y);
        }

        Opacity = Math.Clamp(_config.Opacity > 0 ? _config.Opacity : 1.0, 0.2, 1.0);
        TopMost = _config.TopMost;

        cbTopMost.Checked    = _config.TopMost;
        cbHideBorder.Checked = _config.IsHideWindowBorderOnFocusOut;

        for (int i = 0; i < 8; i++)
            alarmChecks[i].Checked = _config.AlarmEnabled[i];

        for (int i = 0; i < 3; i++)
        {
            customNames[i].Text      = _config.CustomAlarms[i].Name;
            customTicks[i].Value     = (decimal)Math.Max(0, _config.CustomAlarms[i].Tick);
            customEnabled[i].Checked = _config.CustomAlarms[i].Enabled;
        }

        sliderVolume.Value  = Math.Clamp(_config.Volume, 0, 100);
        lblVolumeValue.Text = _config.Volume.ToString();
        sliderRate.Value    = Math.Clamp(_config.Rate, -10, 10);
        cbTts.Checked       = _config.Tts;

        Synth.Instance.SetVolume(_config.Volume);
        Synth.Instance.SetRate(_config.Rate);
    }

    private static bool IsOnScreen(int x, int y)
    {
        foreach (var scr in Screen.AllScreens)
            if (scr.WorkingArea.Contains(x, y)) return true;
        return false;
    }

    // ─────────────────────────────────────────────
    //  사이트 목록
    // ─────────────────────────────────────────────
    private void LoadSites()
    {
        cmbSite.SelectedIndexChanged -= CmbSite_SelectedIndexChanged;
        cmbSite.Items.Clear();

        foreach (var site in _config.Sites)
            cmbSite.Items.Add(site.Name);

        cmbSite.Items.Add("＋ 사이트 추가");
        cmbSite.Items.Add("━ 사이트 관리");

        if (!string.IsNullOrEmpty(_config.DefaultSite) &&
            cmbSite.Items.Contains(_config.DefaultSite))
            cmbSite.SelectedItem = _config.DefaultSite;
        else if (_config.Sites.Count > 0)
            cmbSite.SelectedIndex = 0;

        cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;
    }

    private void CmbSite_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = cmbSite.SelectedItem?.ToString();
        if (selected is null) return;

        if (selected == "＋ 사이트 추가")
        {
            bool was = TopMost;
            TopMost = false;                    // ⚠️ TopMost=true 이면 자식창이 안 보임 → 임시 해제
            using var form = new SiteForm();
            bool ok = form.ShowDialog(this) == DialogResult.OK && form.Result is not null;
            TopMost = was;

            if (ok)
            {
                _config.Sites.Add(form.Result!);
                _config.Save();
            }
            LoadSites();
            return;
        }

        if (selected == "━ 사이트 관리")
        {
            // TODO: 사이트 관리 팝업 (추후 구현)
            LoadSites();
            return;
        }

        var site = _config.Sites.FirstOrDefault(s => s.Name == selected);
        if (site is not null)
        {
            _config.DefaultSite = site.Name;
            _config.Save();

            if (webView?.CoreWebView2 is not null)
                webView.CoreWebView2.Navigate(site.Url);
        }
    }

    // ─────────────────────────────────────────────
    //  알람 파일 목록 (alarm/ 폴더 mp3)
    // ─────────────────────────────────────────────
    private void LoadAlarmFiles()
    {
        cmbAlarmFile.Items.Clear();
        string dir = Path.Combine(AppContext.BaseDirectory, "alarm");

        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.mp3"))
                cmbAlarmFile.Items.Add(Path.GetFileName(f));

        if (cmbAlarmFile.Items.Count == 0)
            cmbAlarmFile.Items.Add("(mp3 파일 없음)");

        if (!string.IsNullOrEmpty(_config.AlarmName) &&
            cmbAlarmFile.Items.Contains(_config.AlarmName))
            cmbAlarmFile.SelectedItem = _config.AlarmName;
        else if (cmbAlarmFile.Items.Count > 0)
            cmbAlarmFile.SelectedIndex = 0;
    }

    // ─────────────────────────────────────────────
    //  WebView2 초기화
    // ─────────────────────────────────────────────
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
                $"WebView2 초기화 실패: {ex.Message}\n\nWebView2 런타임이 설치되어 있는지 확인해주세요.",
                "JHP — 런타임 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string ResolveStartUrl()
    {
        if (!string.IsNullOrWhiteSpace(_config.LatestUrl)) return _config.LatestUrl;
        var def = _config.Sites.FirstOrDefault(s => s.Name == _config.DefaultSite);
        if (def is not null) return def.Url;
        return _config.Sites.Count > 0 ? _config.Sites[0].Url : "https://www.naver.com";
    }

    private async void InjectJS()
    {
        if (webView?.CoreWebView2 is null) return;
        string? source = webView.CoreWebView2.Source;
        if (string.IsNullOrWhiteSpace(source)) return;
        string host;
        try   { host = new Uri(source).Host; }
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

    // ─────────────────────────────────────────────
    //  업데이트 확인
    // ─────────────────────────────────────────────
    private async Task CheckUpdateAsync()
    {
        var (hasUpdate, latest, url) = await UpdateChecker.Check(CurrentVersion);
        if (!hasUpdate) return;

        if (Prompt.ShowDialog("업데이트 알림",
                $"새 버전 {latest}이 있습니다. 다운로드 페이지를 여시겠습니까?"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ─────────────────────────────────────────────
    //  타이머
    // ─────────────────────────────────────────────
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

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_timerRunning) return;

        for (int i = 0; i < 8; i++)
        {
            if (!_config.AlarmEnabled[i]) continue;
            if (--_remaining[i] > 0) continue;
            _remaining[i] = AlarmSeconds[i];
            FireAlarm(AlarmLabels[i]);
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
            ? "⏱ (알람 없음)"
            : $"⏱ {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
    }

    private void OpenAlarmSettings()
    {
        bool was = TopMost;
        TopMost = false;                        // ⚠️ TopMost 버그 방지
        using var form = new AlarmForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK;
        TopMost = was;
        if (!ok) return;

        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i]) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled) _customRemaining[i] = _config.CustomAlarms[i].Tick;

        sliderVolume.Value  = Math.Clamp(_config.Volume, 0, 100);
        lblVolumeValue.Text = _config.Volume.ToString();
        sliderRate.Value    = Math.Clamp(_config.Rate, -10, 10);
        cbTts.Checked       = _config.Tts;
        LoadAlarmFiles();
        UpdateNextAlarmLabel();
    }

    // ─────────────────────────────────────────────
    //  타이틀바 드래그
    // ─────────────────────────────────────────────
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ─────────────────────────────────────────────
    //  포커스 아웃 시 타이틀바 숨김
    // ─────────────────────────────────────────────
    private void Form1_Activated(object? sender, EventArgs e)
    {
        if (_config.IsHideWindowBorderOnFocusOut)
            pnlTitleBar.Visible = true;
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (_config.IsHideWindowBorderOnFocusOut)
            pnlTitleBar.Visible = false;
    }

    // ─────────────────────────────────────────────
    //  종료 시 설정 저장
    // ─────────────────────────────────────────────
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer?.Stop();
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

        if (webView?.CoreWebView2 is not null)
            _config.LatestUrl = webView.CoreWebView2.Source;

        _config.Save();
    }

    // ─────────────────────────────────────────────
    //  WndProc — FormBorderStyle.None 창 크기 조절
    // ─────────────────────────────────────────────
    private const int WM_NCHITTEST     = 0x0084;
    private const int HTCLIENT         = 1;
    private const int HTCAPTION        = 2;
    private const int HTLEFT           = 10;
    private const int HTRIGHT          = 11;
    private const int HTTOP            = 12;
    private const int HTTOPLEFT        = 13;
    private const int HTTOPRIGHT       = 14;
    private const int HTBOTTOM         = 15;
    private const int HTBOTTOMLEFT     = 16;
    private const int HTBOTTOMRIGHT    = 17;
    private const int WM_NCLBUTTONDOWN = 0xA1;

    [DllImport("user32.dll")] private static extern bool   ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != WM_NCHITTEST ||
            WindowState != FormWindowState.Normal ||
            (int)m.Result != HTCLIENT)
            return;

        int x   = unchecked((short)(m.LParam.ToInt32() & 0xFFFF));
        int y   = unchecked((short)((m.LParam.ToInt32() >> 16) & 0xFFFF));
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

    // 창 가장자리에서 리사이즈 커서 시각적 피드백
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
