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
    // ─────────────────────────────────────────────────────────────
    // 알람 상수
    // ─────────────────────────────────────────────────────────────
    private static readonly int[] AlarmSeconds =
        [7200, 3600, 1800, 1200, 900, 600, 100, 55];

    private static readonly string[] AlarmLabels =
    [
        "재획비 (2시간)",       "경쿠 (1시간)",
        "열변/경쿠/오니오 (30분)", "오니오 버프 (20분)",
        "경쿠 (15분)",          "오니오 버프 (10분)",
        "메소 히수 (100초)",    "파우티 (55초)"
    ];

    private const string CurrentVersion = "v1.0.0";
    private const int TitleHeight = 32;
    private const int AlarmPanelWidth = 240;

    // ─────────────────────────────────────────────────────────────
    // 레이아웃 컨트롤
    // ─────────────────────────────────────────────────────────────
    private Panel _titleBar = null!;
    private WebView2 _webView = null!;
    private Panel _alarmPanel = null!;
    private SplitContainer _split = null!;

    // 타이틀바 컨트롤
    private ControlButton _btnClose = null!;
    private ControlButton _btnMax = null!;
    private ControlButton _btnMin = null!;
    private ComboBox _cmbSite = null!;
    private CheckBox _cbTopMost = null!;
    private CheckBox _cbHideBorder = null!;

    // 알람 패널 컨트롤
    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];
    private ComboBox _cmbAlarmFile = null!;
    private NSlider _sliderVolume = null!;
    private Label _lblVolume = null!;
    private CheckBox _cbTts = null!;
    private NSlider _sliderRate = null!;

    // 타이머
    private System.Windows.Forms.Timer _timer = null!;
    private readonly int[] _remaining = new int[8];
    private readonly int[] _customRemaining = new int[3];
    private bool _timerRunning = false;

    // ─────────────────────────────────────────────────────────────
    // 생성자
    // ─────────────────────────────────────────────────────────────
    public Form1()
    {
        InitializeComponent();
    }

    // ─────────────────────────────────────────────────────────────
    // Form Load
    // ─────────────────────────────────────────────────────────────
    private async void Form1_Load(object? sender, EventArgs e)
    {
        // 1. 타이머 먼저 생성 (InitControls에서 이벤트 연결)
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };

        // 2. 컨트롤 구성
        InitControls();

        // 3. Config 불러와서 UI 반영
        LoadConfigToControls();
        LoadSites();
        LoadAlarmFiles();

        // 4. 창 크기/위치 복원 (LoadConfigToControls 이후)
        var cfg = Config.Instance;
        if (cfg.Width > 0 && cfg.Height > 0)
            Size = new Size(
                Math.Max(cfg.Width, MinimumSize.Width),
                Math.Max(cfg.Height, MinimumSize.Height));

        if (IsOnScreen(cfg.X, cfg.Y))
            Location = new Point(cfg.X, cfg.Y);

        if (cfg.IsMaximize)
            WindowState = FormWindowState.Maximized;

        // 5. WebView2 초기화 (비동기)
        await InitWV();

        // 6. 업데이트 확인 (fire and forget)
        _ = CheckUpdateAsync();
    }

    private static bool IsOnScreen(int x, int y)
    {
        foreach (Screen s in Screen.AllScreens)
            if (s.WorkingArea.Contains(x, y)) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // 컨트롤 구성 (InitControls)
    // ─────────────────────────────────────────────────────────────
    private void InitControls()
    {
        SuspendLayout();

        // ── 타이틀바 ──────────────────────────────────────────────
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = TitleHeight,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        // ControlButton 3개: Dock=Right → AddRange 시 Close 먼저
        _btnClose = new ControlButton
        {
            Type = ControlButton.ButtonType.Close,
            Dock = DockStyle.Right
        };
        _btnMax = new ControlButton
        {
            Type = ControlButton.ButtonType.Maximize,
            Dock = DockStyle.Right
        };
        _btnMin = new ControlButton
        {
            Type = ControlButton.ButtonType.Minimize,
            Dock = DockStyle.Right
        };

        // 사이트 드롭다운
        _cmbSite = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Width = 180,
            Left = 8,
            Top = (TitleHeight - 22) / 2
        };

        // 항상 위
        _cbTopMost = new CheckBox
        {
            Text = "항상 위",
            ForeColor = Color.LightGray,
            AutoSize = true,
            Left = 200,
            Top = (TitleHeight - 17) / 2
        };

        // 테두리 숨기기
        _cbHideBorder = new CheckBox
        {
            Text = "테두리 숨기기",
            ForeColor = Color.LightGray,
            AutoSize = true,
            Left = 280,
            Top = (TitleHeight - 17) / 2
        };

        // 타이틀바에 추가 (Dock.Right는 역순 배치이므로 Close 먼저)
        _titleBar.Controls.AddRange(new Control[]
        {
            _btnClose, _btnMax, _btnMin,
            _cbHideBorder, _cbTopMost, _cmbSite
        });

        // ── SplitContainer (WebView + 알람패널) ─────────────────
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 1,
            IsSplitterFixed = true,
            BackColor = Color.FromArgb(20, 20, 20)
        };

        // WebView2
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(24, 24, 24)
        };
        _split.Panel1.Controls.Add(_webView);

        // 알람 패널
        _alarmPanel = BuildAlarmPanel();
        _split.Panel2.Controls.Add(_alarmPanel);

        // !! Panel2MinSize는 SplitContainer가 Handle 생성 후 설정해야 함
        // Form_Load 이후 시점에 설정하므로 여기서는 생략하고 아래 HandleCreated에서 처리
        _split.HandleCreated += (s, e) =>
        {
            _split.Panel2MinSize = AlarmPanelWidth;
            if (_split.Width > AlarmPanelWidth + _split.Panel1MinSize + _split.SplitterWidth)
                _split.SplitterDistance = _split.Width - AlarmPanelWidth - _split.SplitterWidth;
        };

        // Form Controls.Add 순서: Fill 먼저, Top을 나중에 (Dock=Top은 나중 추가 = 상단)
        Controls.Add(_split);       // Fill
        Controls.Add(_titleBar);    // Top → 마지막 추가 = 최상단

        // ── 이벤트 연결 ──────────────────────────────────────────
        ConnectEvents();

        ResumeLayout(true);
    }

    // ─────────────────────────────────────────────────────────────
    // 알람 패널 빌드
    // ─────────────────────────────────────────────────────────────
    private Panel BuildAlarmPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 28),
            AutoScroll = true,
            Padding = new Padding(8)
        };

        int y = 8;

        // 고정 알람 체크박스 8개
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                ForeColor = Color.White,
                Left = 8,
                Top = y,
                Width = 220,
                Height = 22
            };
            panel.Controls.Add(_alarmChecks[i]);
            y += 24;
        }

        y += 4;
        AddSeparator(panel, y); y += 10;

        // 커스텀 알람 3슬롯
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 8, Top = y + 2, Width = 18, Height = 18 };
            panel.Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 30, Top = y, Width = 90,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                PlaceholderText = "이름",
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 124, Top = y, Width = 65,
                Minimum = 0, Maximum = 99999,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            panel.Controls.Add(_customTicks[i]);

            panel.Controls.Add(new Label
            {
                Text = "초",
                Left = 193, Top = y + 3,
                Width = 18,
                ForeColor = Color.LightGray,
                AutoSize = true
            });

            y += 30;
        }

        y += 4;
        AddSeparator(panel, y); y += 10;

        // 알람 파일
        panel.Controls.Add(new Label
        {
            Text = "알람 파일",
            Left = 8, Top = y + 3,
            Width = 55, ForeColor = Color.LightGray,
            AutoSize = false
        });

        _cmbAlarmFile = new ComboBox
        {
            Left = 66, Top = y, Width = 158,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        panel.Controls.Add(_cmbAlarmFile);
        y += 30;

        // 볼륨 슬라이더
        panel.Controls.Add(new Label
        {
            Text = "볼륨",
            Left = 8, Top = y + 4,
            Width = 30, ForeColor = Color.LightGray,
            AutoSize = true
        });

        _sliderVolume = new NSlider
        {
            Left = 42, Top = y, Width = 148, Height = 20,
            Minimum = 0, Maximum = 100
        };
        panel.Controls.Add(_sliderVolume);

        _lblVolume = new Label
        {
            Left = 196, Top = y + 3,
            Width = 28, ForeColor = Color.White,
            Text = "50", AutoSize = false
        };
        panel.Controls.Add(_lblVolume);
        y += 28;

        // TTS 출력 체크박스
        _cbTts = new CheckBox
        {
            Text = "TTS 출력",
            Left = 8, Top = y,
            Width = 100, ForeColor = Color.White
        };
        panel.Controls.Add(_cbTts);
        y += 26;

        // TTS 속도
        panel.Controls.Add(new Label
        {
            Text = "TTS 속도",
            Left = 8, Top = y + 4,
            Width = 55, ForeColor = Color.LightGray,
            AutoSize = false
        });

        _sliderRate = new NSlider
        {
            Left = 66, Top = y, Width = 120, Height = 20,
            Minimum = -10, Maximum = 10
        };
        panel.Controls.Add(_sliderRate);
        y += 28;

        AddSeparator(panel, y); y += 10;

        // 알람 설정 열기 버튼
        var btnAlarm = new Button
        {
            Text = "알람 설정 열기",
            Left = 8, Top = y, Width = 218, Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btnAlarm.FlatAppearance.BorderSize = 0;
        btnAlarm.Click += (s, e) =>
        {
            bool was = TopMost;
            TopMost = false;
            using var af = new AlarmForm();
            af.ShowDialog(this);
            TopMost = was;
            LoadAlarmFiles();
            LoadConfigToControls();
        };
        panel.Controls.Add(btnAlarm);

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

    // ─────────────────────────────────────────────────────────────
    // 이벤트 연결
    // ─────────────────────────────────────────────────────────────
    private void ConnectEvents()
    {
        // 타이틀바 드래그
        _titleBar.MouseDown += TitleBar_MouseDown;

        // 창 버튼
        _btnClose.Click += (_, _) => Close();
        _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _btnMax.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        SizeChanged += (_, _) => _btnMax?.Invalidate();

        // 항상 위
        _cbTopMost.CheckedChanged += (_, _) =>
        {
            TopMost = _cbTopMost.Checked;
            Config.Instance.TopMost = _cbTopMost.Checked;
            Config.Instance.Save();
        };

        // 테두리 숨기기
        _cbHideBorder.CheckedChanged += (_, _) =>
        {
            Config.Instance.IsHideWindowBorderOnFocusOut = _cbHideBorder.Checked;
            Config.Instance.Save();
        };

        // 사이트 선택
        _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;

        // 볼륨
        _sliderVolume.ValueChanged += (_, _) =>
        {
            Config.Instance.Volume = _sliderVolume.Value;
            Synth.Instance.SetVolume(_sliderVolume.Value);
            _lblVolume.Text = _sliderVolume.Value.ToString();
            Config.Instance.Save();
        };

        // TTS 속도
        _sliderRate.ValueChanged += (_, _) =>
        {
            Config.Instance.Rate = _sliderRate.Value;
            Synth.Instance.SetRate(_sliderRate.Value);
            Config.Instance.Save();
        };

        // TTS 체크박스
        _cbTts.CheckedChanged += (_, _) =>
        {
            Config.Instance.Tts = _cbTts.Checked;
            Config.Instance.Save();
        };

        // 알람 파일 선택
        _cmbAlarmFile.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbAlarmFile.SelectedItem is string name &&
                name != "(mp3 파일 없음)")
            {
                Config.Instance.AlarmName = name;
                Config.Instance.Save();
            }
        };

        // 고정 알람 체크박스 8개
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            _alarmChecks[i].CheckedChanged += (_, _) =>
            {
                Config.Instance.AlarmEnabled[idx] = _alarmChecks[idx].Checked;
                if (_alarmChecks[idx].Checked)
                    _remaining[idx] = AlarmSeconds[idx];
                Config.Instance.Save();
            };
        }

        // 커스텀 알람
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            _customNames[i].TextChanged += (_, _) =>
                Config.Instance.CustomAlarms[idx].Name = _customNames[idx].Text;
            _customTicks[i].ValueChanged += (_, _) =>
            {
                Config.Instance.CustomAlarms[idx].Tick = (int)_customTicks[idx].Value;
                if (Config.Instance.CustomAlarms[idx].Enabled)
                    _customRemaining[idx] = (int)_customTicks[idx].Value;
            };
            _customEnabled[i].CheckedChanged += (_, _) =>
            {
                Config.Instance.CustomAlarms[idx].Enabled = _customEnabled[idx].Checked;
                if (_customEnabled[idx].Checked)
                    _customRemaining[idx] = Config.Instance.CustomAlarms[idx].Tick;
            };
        }

        // 타이머
        _timer.Tick += Timer_Tick;
    }

    // ─────────────────────────────────────────────────────────────
    // Config 로드/저장
    // ─────────────────────────────────────────────────────────────
    private void LoadConfigToControls()
    {
        var cfg = Config.Instance;

        // 투명도
        Opacity = Math.Clamp(cfg.Opacity, 0.2, 1.0);
        TopMost = cfg.TopMost;

        // null 체크 후 설정 (InitControls 이전 호출 방지)
        if (_cbTopMost != null) _cbTopMost.Checked = cfg.TopMost;
        if (_cbHideBorder != null) _cbHideBorder.Checked = cfg.IsHideWindowBorderOnFocusOut;

        // 알람 체크박스
        for (int i = 0; i < 8; i++)
            if (_alarmChecks[i] != null)
                _alarmChecks[i].Checked = cfg.AlarmEnabled[i];

        // remaining 초기화
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++)
            _customRemaining[i] = cfg.CustomAlarms[i].Tick;

        // 커스텀 알람
        for (int i = 0; i < 3; i++)
        {
            if (_customNames[i] != null)
                _customNames[i].Text = cfg.CustomAlarms[i].Name;
            if (_customTicks[i] != null)
                _customTicks[i].Value = Math.Max(0, cfg.CustomAlarms[i].Tick);
            if (_customEnabled[i] != null)
                _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }

        // 슬라이더
        if (_sliderVolume != null)
        {
            _sliderVolume.Value = Math.Clamp(cfg.Volume, _sliderVolume.Minimum, _sliderVolume.Maximum);
            if (_lblVolume != null) _lblVolume.Text = _sliderVolume.Value.ToString();
        }
        if (_sliderRate != null)
            _sliderRate.Value = Math.Clamp(cfg.Rate, _sliderRate.Minimum, _sliderRate.Maximum);
        if (_cbTts != null) _cbTts.Checked = cfg.Tts;

        Synth.Instance.SetVolume(cfg.Volume);
        Synth.Instance.SetRate(cfg.Rate);
    }

    private void SaveConfig()
    {
        var cfg = Config.Instance;

        if (WindowState == FormWindowState.Normal)
        {
            cfg.Width = Width;
            cfg.Height = Height;
            cfg.X = Left;
            cfg.Y = Top;
        }
        cfg.IsMaximize = WindowState == FormWindowState.Maximized;
        cfg.TopMost = TopMost;
        cfg.Opacity = Opacity;

        for (int i = 0; i < 8; i++)
            cfg.AlarmEnabled[i] = _alarmChecks[i]?.Checked ?? false;
        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name = _customNames[i]?.Text ?? "";
            cfg.CustomAlarms[i].Tick = (int)(_customTicks[i]?.Value ?? 0);
            cfg.CustomAlarms[i].Enabled = _customEnabled[i]?.Checked ?? false;
        }

        cfg.AlarmName = _cmbAlarmFile?.SelectedItem?.ToString() ?? cfg.AlarmName;
        cfg.Tts = _cbTts?.Checked ?? false;
        cfg.Volume = _sliderVolume?.Value ?? cfg.Volume;
        cfg.Rate = _sliderRate?.Value ?? cfg.Rate;

        try
        {
            if (_webView?.CoreWebView2 != null)
                cfg.LatestUrl = _webView.CoreWebView2.Source;
        }
        catch { }

        cfg.Save();
    }

    // ─────────────────────────────────────────────────────────────
    // 사이트 드롭다운
    // ─────────────────────────────────────────────────────────────
    private void LoadSites()
    {
        _cmbSite.SelectedIndexChanged -= CmbSite_SelectedIndexChanged;
        _cmbSite.Items.Clear();
        foreach (var site in Config.Instance.Sites)
            _cmbSite.Items.Add(site.Name);
        _cmbSite.Items.Add("＋ 사이트 추가");

        var cfg = Config.Instance;
        if (!string.IsNullOrEmpty(cfg.DefaultSite) &&
            _cmbSite.Items.Contains(cfg.DefaultSite))
            _cmbSite.SelectedItem = cfg.DefaultSite;
        else if (cfg.Sites.Count > 0)
            _cmbSite.SelectedIndex = 0;

        _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;
    }

    private void CmbSite_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = _cmbSite.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) return;

        if (selected == "＋ 사이트 추가")
        {
            bool was = TopMost;
            TopMost = false;
            using var sf = new SiteForm();
            if (sf.ShowDialog(this) == DialogResult.OK && sf.Result is { } site)
            {
                Config.Instance.Sites.Add(site);
                Config.Instance.Save();
                LoadSites();
                _cmbSite.SelectedItem = site.Name;
            }
            else
            {
                LoadSites();
            }
            TopMost = was;
            return;
        }

        var found = Config.Instance.Sites.FirstOrDefault(s => s.Name == selected);
        if (found != null)
        {
            Config.Instance.DefaultSite = found.Name;
            if (_webView?.CoreWebView2 != null)
                _webView.CoreWebView2.Navigate(found.Url);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 알람 파일 로드
    // ─────────────────────────────────────────────────────────────
    private void LoadAlarmFiles()
    {
        _cmbAlarmFile.Items.Clear();
        string dir = Path.Combine(AppContext.BaseDirectory, "alarm");
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.mp3"))
                _cmbAlarmFile.Items.Add(Path.GetFileName(f));

        if (_cmbAlarmFile.Items.Count == 0)
            _cmbAlarmFile.Items.Add("(mp3 파일 없음)");

        string saved = Config.Instance.AlarmName;
        if (!string.IsNullOrEmpty(saved) && _cmbAlarmFile.Items.Contains(saved))
            _cmbAlarmFile.SelectedItem = saved;
        else if (_cmbAlarmFile.Items.Count > 0)
            _cmbAlarmFile.SelectedIndex = 0;
    }

    // ─────────────────────────────────────────────────────────────
    // WebView2 초기화
    // ─────────────────────────────────────────────────────────────
    private async Task InitWV()
    {
        try
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: cacheDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.NavigationCompleted += (_, _) => InjectJS();

            string url = Config.Instance.LatestUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                var def = Config.Instance.Sites.FirstOrDefault(
                    s => s.Name == Config.Instance.DefaultSite)
                    ?? Config.Instance.Sites.FirstOrDefault();
                url = def?.Url ?? "https://www.naver.com";
            }
            _webView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 초기화 실패: {ex.Message}\n" +
                "WebView2 런타임이 설치되어 있는지 확인해주세요.\n" +
                "https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/",
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void InjectJS()
    {
        if (_webView?.CoreWebView2 is null) return;

        string? source = _webView.CoreWebView2.Source;
        if (string.IsNullOrWhiteSpace(source)) return;

        string host;
        try { host = new Uri(source).Host; }
        catch { return; }

        try
        {
            if (host.Contains("laftel.net"))
                await _webView.CoreWebView2.ExecuteScriptAsync(UserScripts.LaftelSkipNext);
            else if (host.Contains("netflix.com"))
                await _webView.CoreWebView2.ExecuteScriptAsync(UserScripts.NetflixSkipNext);
        }
        catch { }
    }

    // ─────────────────────────────────────────────────────────────
    // 업데이트 확인
    // ─────────────────────────────────────────────────────────────
    private async Task CheckUpdateAsync()
    {
        try
        {
            var (hasUpdate, latest, url) = await UpdateChecker.Check(CurrentVersion);
            if (!hasUpdate) return;

            if (MessageBox.Show(
                $"새 버전 {latest}이 있습니다.\n다운로드 페이지로 이동할까요?",
                "JHP 업데이트", MessageBoxButtons.YesNo) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    // ─────────────────────────────────────────────────────────────
    // 타이머
    // ─────────────────────────────────────────────────────────────
    private void StartTimer()
    {
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++)
            _customRemaining[i] = Config.Instance.CustomAlarms[i].Tick;
        _timerRunning = true;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_timerRunning) return;

        for (int i = 0; i < 8; i++)
        {
            if (!Config.Instance.AlarmEnabled[i]) continue;
            if (--_remaining[i] > 0) continue;

            _remaining[i] = AlarmSeconds[i];
            FireAlarm(AlarmLabels[i]);
        }

        for (int i = 0; i < 3; i++)
        {
            var ca = Config.Instance.CustomAlarms[i];
            if (!ca.Enabled || ca.Tick <= 0) continue;
            if (--_customRemaining[i] > 0) continue;

            _customRemaining[i] = ca.Tick;
            FireAlarm(string.IsNullOrWhiteSpace(ca.Name) ? "커스텀 알람" : ca.Name);
        }
    }

    private void FireAlarm(string label)
    {
        if (Config.Instance.Tts)
            Synth.Instance.TTS(label, Config.Instance.Volume, Config.Instance.Rate);
        else
            Synth.Instance.Ring(Config.Instance.AlarmName, Config.Instance.Volume);
    }

    // ─────────────────────────────────────────────────────────────
    // 포커스 이벤트 (테두리 숨기기)
    // ─────────────────────────────────────────────────────────────
    private void Form1_Activated(object? sender, EventArgs e)
    {
        if (_titleBar != null) _titleBar.Visible = true;
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (Config.Instance.IsHideWindowBorderOnFocusOut && _titleBar != null)
            _titleBar.Visible = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 종료
    // ─────────────────────────────────────────────────────────────
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _timer?.Stop();
        Synth.Instance.Stop();
        SaveConfig();
    }

    // ─────────────────────────────────────────────────────────────
    // 타이틀바 드래그
    // ─────────────────────────────────────────────────────────────
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ─────────────────────────────────────────────────────────────
    // 창 리사이즈 (FormBorderStyle.None)
    // ─────────────────────────────────────────────────────────────
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12;
    private const int HTTOPLEFT = 13, HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != WM_NCHITTEST || WindowState != FormWindowState.Normal
            || (int)m.Result != HTCLIENT)
            return;

        int lp = m.LParam.ToInt32();
        int x = unchecked((short)(lp & 0xFFFF));
        int y = unchecked((short)((lp >> 16) & 0xFFFF));
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
