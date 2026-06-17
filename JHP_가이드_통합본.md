# JHP — 완전 통합 가이드 (단일 파일)

> **새 대화 시작 시 이 파일 하나만 붙여넣으면 됩니다.**
> .NET 10.0 WinForms + WebView2 / 모든 작업 Visual Studio GUI만 사용 (터미널/CLI 없음)

---

## 🗺️ 프로젝트 기본 정보

| 항목 | 값 |
|------|-----|
| GitHub | https://github.com/dlrj0/JHP |
| 로컬 경로 | `C:\Users\wjgus\source\repos\dlrj0\JHP` |
| 솔루션 | `JHP.slnx` |
| 프로젝트 4개 | `JHP`(UI), `JHP.Api`(로직), `JHP.Controls`(커스텀 UI), `JHP.Asset`(JS) |

> ⚠️ 솔루션 이름과 UI 프로젝트 이름이 둘 다 "JHP"입니다.
> `...\JHP\JHP\JHP.csproj` 경로가 정상입니다.

```
[Claude에게 붙여넣는 컨텍스트]
- 이름: JHP
- 종류: .NET 10.0 WinForms + WebView2 내장 브라우저
- 목적: 메이플스토리 재획비 타이머 + OTT 시청 보조 도구
- NuGet: Microsoft.Web.WebView2, NAudio, Octokit, System.Speech
- 현재 작업: Form1.cs / Form1.Designer.cs 작성
- GitHub: https://github.com/dlrj0/JHP
- 모든 작업: Visual Studio GUI만 (터미널 사용 안 함)
```

---

## 📋 진행 상태

### ✅ 완료된 파일 (16개 — GitHub에 모두 올라가 있음)

| 프로젝트 | 파일 |
|---------|------|
| JHP.Api | `Config.cs`, `Site.cs`, `CustomAlarm.cs`, `Synth.cs`, `UpdateChecker.cs`, `Prompt.cs`, `ReSize.cs`, `ToolStripCommand.cs` |
| JHP.Controls | `NSlider.cs`, `SiteListViewControl.cs`, `ControlButton.cs`, `CustomCheckBox.cs` |
| JHP.Asset | `UserScripts.cs` |
| JHP | `AlarmForm.cs`, `SiteForm.cs`, `Program.cs` |

### ⏳ 남은 작업 (순서대로)

| 순서 | 파일 | 비고 |
|------|------|------|
| **1** | **`Form1.cs`** | **현재 작업 — 이 가이드의 핵심** |
| **2** | **`Form1.Designer.cs`** | **Form1.cs와 함께 작성** |
| 3 | `README.md` | 이 가이드 하단 템플릿 참조 |
| 4 | `CHANGELOG.md` | 이 가이드 하단 템플릿 참조 |

> Form1.cs GitHub 현재 상태: `public Form1() { InitializeComponent(); }` 한 줄만 있음.
> 구현이 전혀 없으므로 전면 작성이 필요합니다.

---

## ⚠️ 실제 코드와 가이드 불일치 사항 (필독)

GitHub 코드를 직접 확인해서 발견한 불일치입니다. Form1.cs 작성 시 반드시 적용하세요.

```csharp
// ✅ 실제 Synth.cs 메서드 시그니처 (volume/rate 파라미터 있음)
Synth.Instance.Ring(string alarmName, int volume);
Synth.Instance.TTS(string text, int volume, int rate);

// Form1에서 호출할 때:
Synth.Instance.Ring(Config.Instance.AlarmName, Config.Instance.Volume);
Synth.Instance.TTS(labelText, Config.Instance.Volume, Config.Instance.Rate);

// ✅ ReSize.cs는 static class — new ReSize() 불가
ReSize.GetMousePosition(this, point);   // 인스턴스 없이 직접 호출
ReSize.SetThick(mousePosition);
```

---

## 🛠️ Form1.cs 구현 명세

### 목표 레이아웃

```
┌──────────────────────────────────────────────────────────┐
│ [사이트▼] [항상위□] [테두리숨기기□]          [─][□][×] │ ← 타이틀바(드래그 이동)
├──────────────────────────────────────────────────────┬───┤
│                                                      │알 │
│              WebView2 브라우저                       │람 │
│         (좌클릭 → 타이머 시작/재시작)                │패 │
│                                                      │널 │
└──────────────────────────────────────────────────────┴───┘
```

### 알람 패널 (우측 240px 고정)

```
□ 재획비 (2시간)        ← CheckBox 8개
□ 경쿠 (1시간)            [0]=7200s [1]=3600s [2]=1800s [3]=1200s
□ 열변/경쿠/오니오 (30분) [4]=900s  [5]=600s  [6]=100s  [7]=55s
□ 오니오 버프 (20분)
□ 경쿠 (15분)
□ 오니오 버프 (10분)
□ 메소 히수 (100초)
□ 파우티 (55초)
──────────────────────
[이름__] [0▲] 초 □   ← 커스텀 알람 3슬롯
[이름__] [0▲] 초 □
[이름__] [0▲] 초 □
──────────────────────
알람 파일 [경험치업.mp3▼]  ← alarm 폴더 mp3 목록
볼륨      [────●────]  50  ← NSlider + Label
TTS 출력               □
TTS 속도  [────●────]
──────────────────────
[ 알람 설정 열기 ]          ← AlarmForm 팝업
```

### Form 기본 설정

```csharp
FormBorderStyle = FormBorderStyle.None;  // 커스텀 창 테두리
BackColor = Color.FromArgb(20, 20, 20);
ForeColor = Color.White;
MinimumSize = new Size(700, 450);
Text = "JHP";
```

### 필드 목록

```csharp
private Panel _titleBar = null!;
private Panel _alarmPanel = null!;
private WebView2 _webView = null!;

private ControlButton _btnClose = null!, _btnMax = null!, _btnMin = null!;
private ComboBox _cmbSite = null!;
private CheckBox _cbTopMost = null!, _cbHideBorder = null!;

private CheckBox[] _alarmChecks = new CheckBox[8];
private TextBox[] _customNames = new TextBox[3];
private NumericUpDown[] _customTicks = new NumericUpDown[3];
private CheckBox[] _customEnabled = new CheckBox[3];
private ComboBox _cmbAlarmFile = null!;
private NSlider _sliderVolume = null!, _sliderRate = null!;
private Label _lblVolume = null!;
private CheckBox _cbTts = null!;

private System.Windows.Forms.Timer _timer = null!;
private DateTime _timerStartTime;
private bool _timerRunning = false;

private const int TITLE_HEIGHT = 32;

// [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
private static readonly int[] AlarmTicks = { 7200, 3600, 1800, 1200, 900, 600, 100, 55 };
private static readonly string[] AlarmLabels =
{
    "재획비 (2시간)", "경쿠 (1시간)", "열변/경쿠/오니오 (30분)",
    "오니오 버프 (20분)", "경쿠 (15분)", "오니오 버프 (10분)",
    "메소 히수 (100초)", "파우티 (55초)"
};
```

### WndProc — 창 이동 + 엣지 리사이즈 (FormBorderStyle.None 핵심)

```csharp
protected override void WndProc(ref Message m)
{
    const int WM_NCHITTEST = 0x0084;
    const int HTCAPTION = 2;
    const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12;
    const int HTTOPLEFT = 13, HTTOPRIGHT = 14;
    const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
    {
        var pt = PointToClient(new Point(
            m.LParam.ToInt32() & 0xFFFF,
            (m.LParam.ToInt32() >> 16) & 0xFFFF));

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
                _                               => IntPtr.Zero
            };
            return;
        }

        // 타이틀바 드래그 이동 (버튼 3개 = 138px 제외)
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
```

### InitControls

```csharp
private void InitControls()
{
    SuspendLayout();

    // 타이틀바
    _titleBar = new Panel { Dock = DockStyle.Top, Height = TITLE_HEIGHT,
                            BackColor = Color.FromArgb(28, 28, 28) };

    _btnClose = new ControlButton { Type = ControlButton.ButtonType.Close,  Dock = DockStyle.Right };
    _btnMax   = new ControlButton { Type = ControlButton.ButtonType.Maximize, Dock = DockStyle.Right };
    _btnMin   = new ControlButton { Type = ControlButton.ButtonType.Minimize, Dock = DockStyle.Right };

    _cmbSite = new ComboBox
    {
        DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White,
        Width = 180, Left = 8, Top = (TITLE_HEIGHT - 22) / 2
    };

    _cbTopMost   = new CheckBox { Text = "항상 위",     ForeColor = Color.LightGray,
                                  AutoSize = true, Left = 200, Top = (TITLE_HEIGHT - 17) / 2 };
    _cbHideBorder = new CheckBox { Text = "테두리 숨기기", ForeColor = Color.LightGray,
                                   AutoSize = true, Left = 278, Top = (TITLE_HEIGHT - 17) / 2 };

    _titleBar.Controls.AddRange(new Control[]
        { _btnClose, _btnMax, _btnMin, _cmbSite, _cbTopMost, _cbHideBorder });

    // 본문 SplitContainer
    var split = new SplitContainer
    {
        Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 1, IsSplitterFixed = true,
        BackColor = Color.FromArgb(20, 20, 20)
    };

    // 좌측: WebView2
    _webView = new WebView2 { Dock = DockStyle.Fill };
    _webView.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) StartTimer(); };
    split.Panel1.Controls.Add(_webView);

    // 우측: 알람 패널
    _alarmPanel = BuildAlarmPanel();
    split.Panel2.Controls.Add(_alarmPanel);
    split.Panel2MinSize = 240;

    Controls.AddRange(new Control[] { split, _titleBar });
    ConnectEvents();
    ResumeLayout(true);
}
```

### BuildAlarmPanel

```csharp
private Panel BuildAlarmPanel()
{
    var panel = new Panel { Dock = DockStyle.Fill,
                            BackColor = Color.FromArgb(28, 28, 28),
                            AutoScroll = true, Padding = new Padding(8) };
    int y = 8;

    for (int i = 0; i < 8; i++)
    {
        _alarmChecks[i] = new CheckBox { Text = AlarmLabels[i], ForeColor = Color.White,
                                         Left = 8, Top = y, Width = 220, Height = 22 };
        panel.Controls.Add(_alarmChecks[i]);
        y += 24;
    }

    y += 4; AddSep(panel, y); y += 10;

    for (int i = 0; i < 3; i++)
    {
        _customEnabled[i] = new CheckBox { Left = 8, Top = y + 2, Width = 18 };
        _customNames[i]   = new TextBox  { Left = 30, Top = y, Width = 90,
            BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White, PlaceholderText = "이름" };
        _customTicks[i]   = new NumericUpDown { Left = 124, Top = y, Width = 64,
            Minimum = 0, Maximum = 99999, BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White };
        panel.Controls.Add(new Label { Text = "초", Left = 192, Top = y+3, Width = 20, ForeColor = Color.LightGray });
        panel.Controls.AddRange(new Control[] { _customEnabled[i], _customNames[i], _customTicks[i] });
        y += 30;
    }

    y += 4; AddSep(panel, y); y += 10;

    panel.Controls.Add(new Label { Text = "알람 파일", Left = 8, Top = y, Width = 60, ForeColor = Color.LightGray });
    _cmbAlarmFile = new ComboBox { Left = 72, Top = y-2, Width = 152,
        DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White };
    panel.Controls.Add(_cmbAlarmFile);
    y += 30;

    panel.Controls.Add(new Label { Text = "볼륨", Left = 8, Top = y+4, Width = 30, ForeColor = Color.LightGray });
    _sliderVolume = new NSlider { Left = 42, Top = y, Width = 150, Height = 20, Minimum = 0, Maximum = 100 };
    _lblVolume    = new Label   { Left = 196, Top = y+3, Width = 30, ForeColor = Color.White, Text = "50" };
    panel.Controls.AddRange(new Control[] { _sliderVolume, _lblVolume });
    y += 28;

    _cbTts = new CheckBox { Text = "TTS 출력", Left = 8, Top = y, Width = 100, ForeColor = Color.White };
    panel.Controls.Add(_cbTts);
    y += 26;

    panel.Controls.Add(new Label { Text = "TTS 속도", Left = 8, Top = y+4, Width = 55, ForeColor = Color.LightGray });
    _sliderRate = new NSlider { Left = 68, Top = y, Width = 124, Height = 20, Minimum = -10, Maximum = 10 };
    panel.Controls.Add(_sliderRate);
    y += 28;

    AddSep(panel, y); y += 10;

    var btnAlarm = new Button { Text = "알람 설정 열기", Left = 8, Top = y, Width = 218, Height = 28,
        FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55,90,145),
        ForeColor = Color.White, Cursor = Cursors.Hand };
    btnAlarm.FlatAppearance.BorderSize = 0;
    btnAlarm.Click += (s, e) =>
    {
        bool was = TopMost;
        TopMost = false;     // TopMost=true 상태에서 자식창 안보이는 원본 버그 방지
        using var af = new AlarmForm();
        af.ShowDialog(this);
        TopMost = was;
        LoadAlarmFiles();
        LoadConfigToControls();
    };
    panel.Controls.Add(btnAlarm);
    return panel;
}

private static void AddSep(Panel p, int y) =>
    p.Controls.Add(new Panel { Left = 8, Top = y, Width = 224, Height = 1,
                                BackColor = Color.FromArgb(55,55,55) });
```

### ConnectEvents

```csharp
private void ConnectEvents()
{
    _btnClose.Click += (s, e) => Close();
    _btnMin.Click   += (s, e) => WindowState = FormWindowState.Minimized;
    _btnMax.Click   += (s, e) => WindowState =
        WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    SizeChanged += (s, e) => _btnMax?.Invalidate();

    _cbTopMost.CheckedChanged += (s, e) =>
    {
        TopMost = _cbTopMost.Checked;
        Config.Instance.TopMost = _cbTopMost.Checked;
    };
    _cbHideBorder.CheckedChanged += (s, e) =>
        Config.Instance.IsHideWindowBorderOnFocusOut = _cbHideBorder.Checked;

    _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;

    _sliderVolume.ValueChanged += (s, e) =>
    {
        Config.Instance.Volume = _sliderVolume.Value;
        Synth.Instance.SetVolume(_sliderVolume.Value);
        _lblVolume.Text = _sliderVolume.Value.ToString();
    };
    _sliderRate.ValueChanged += (s, e) =>
    {
        Config.Instance.Rate = _sliderRate.Value;
        Synth.Instance.SetRate(_sliderRate.Value);
    };
    _cbTts.CheckedChanged    += (s, e) => Config.Instance.Tts = _cbTts.Checked;
    _cmbAlarmFile.SelectedIndexChanged += (s, e) =>
    {
        if (_cmbAlarmFile.SelectedItem is string name) Config.Instance.AlarmName = name;
    };

    for (int i = 0; i < 8; i++)
    {
        int idx = i;
        _alarmChecks[i].CheckedChanged += (s, e) =>
            Config.Instance.AlarmEnabled[idx] = _alarmChecks[idx].Checked;
    }
    for (int i = 0; i < 3; i++)
    {
        int idx = i;
        _customNames[i].TextChanged    += (s, e) => Config.Instance.CustomAlarms[idx].Name = _customNames[idx].Text;
        _customTicks[i].ValueChanged   += (s, e) => Config.Instance.CustomAlarms[idx].Tick = (int)_customTicks[idx].Value;
        _customEnabled[i].CheckedChanged += (s, e) => Config.Instance.CustomAlarms[idx].Enabled = _customEnabled[idx].Checked;
    }

    Activated  += (s, e) => { if (_titleBar != null) _titleBar.Visible = true; };
    Deactivate += (s, e) =>
    {
        if (Config.Instance.IsHideWindowBorderOnFocusOut) _titleBar.Visible = false;
    };
}
```

### WebView2 초기화

```csharp
private async Task InitWV()
{
    try
    {
        // 캐시를 디스크에 저장 — 장시간 사용 시 메모리 누적 방지
        var env = await CoreWebView2Environment.CreateAsync(
            null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache"));
        await _webView.EnsureCoreWebView2Async(env);

        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(UserScripts.LaftelSkipNext);
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(UserScripts.NetflixSkipNext);

        string url = Config.Instance.LatestUrl;
        if (string.IsNullOrEmpty(url) && Config.Instance.Sites.Count > 0)
            url = Config.Instance.Sites[0].Url;
        if (!string.IsNullOrEmpty(url))
            _webView.Source = new Uri(url);
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
```

### 타이머 로직

```csharp
private void StartTimer()
{
    _timerStartTime = DateTime.Now;
    _timerRunning = true;
    _timer.Start();
}

private void Timer_Tick(object? sender, EventArgs e)
{
    if (!_timerRunning) return;
    // DateTime 기반 — UI 블로킹으로 인한 tick 누락 없음
    int elapsed = (int)(DateTime.Now - _timerStartTime).TotalSeconds;

    for (int i = 0; i < 8; i++)
        if (_alarmChecks[i].Checked && elapsed == AlarmTicks[i]) FireAlarm(AlarmLabels[i]);

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
```

### 사이트 드롭다운

```csharp
private void LoadSites()
{
    _cmbSite.SelectedIndexChanged -= CmbSite_SelectedIndexChanged;
    _cmbSite.Items.Clear();
    foreach (var site in Config.Instance.Sites) _cmbSite.Items.Add(site.Name);
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
        TopMost = false;    // TopMost 버그 방지
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
        _webView.Source = new Uri(site.Url);
        Config.Instance.DefaultSite = site.Name;
    }
}
```

### 알람 파일 목록 로드

```csharp
private void LoadAlarmFiles()
{
    _cmbAlarmFile.Items.Clear();
    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alarm");
    if (Directory.Exists(dir))
        foreach (var f in Directory.GetFiles(dir, "*.mp3"))
            _cmbAlarmFile.Items.Add(Path.GetFileName(f));

    if (_cmbAlarmFile.Items.Count == 0) _cmbAlarmFile.Items.Add("(파일 없음)");

    if (!string.IsNullOrEmpty(Config.Instance.AlarmName) &&
        _cmbAlarmFile.Items.Contains(Config.Instance.AlarmName))
        _cmbAlarmFile.SelectedItem = Config.Instance.AlarmName;
    else if (_cmbAlarmFile.Items.Count > 0)
        _cmbAlarmFile.SelectedIndex = 0;
}
```

### Config 로드/저장

```csharp
private void LoadConfigToControls()
{
    var cfg = Config.Instance;
    if (cfg.IsMaximize) WindowState = FormWindowState.Maximized;
    else
    {
        Width  = Math.Max(cfg.Width, MinimumSize.Width);
        Height = Math.Max(cfg.Height, MinimumSize.Height);
        // 화면 범위 이탈 방지 (다중 모니터 해상도 변경 시)
        var screen = Screen.FromPoint(new Point(cfg.X, cfg.Y));
        Left = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.X : 100;
        Top  = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.Y : 100;
    }
    Opacity = Math.Clamp(cfg.Opacity, 0.2, 1.0);
    TopMost = cfg.TopMost;
    _cbTopMost.Checked    = cfg.TopMost;
    _cbHideBorder.Checked = cfg.IsHideWindowBorderOnFocusOut;

    for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];
    for (int i = 0; i < 3; i++)
    {
        _customNames[i].Text      = cfg.CustomAlarms[i].Name;
        _customTicks[i].Value     = cfg.CustomAlarms[i].Tick;
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
    { cfg.Width = Width; cfg.Height = Height; cfg.X = Left; cfg.Y = Top; }
    cfg.IsMaximize = WindowState == FormWindowState.Maximized;
    cfg.TopMost    = TopMost;
    cfg.Opacity    = Opacity;
    for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
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
    try { cfg.LatestUrl = _webView.Source?.ToString() ?? ""; } catch { }
    cfg.Save();
}
```

### Form1_Load / OnFormClosing

```csharp
private async void Form1_Load(object? sender, EventArgs e)
{
    _timer = new System.Windows.Forms.Timer { Interval = 1000 };
    _timer.Tick += Timer_Tick;
    LoadConfigToControls();
    LoadSites();
    LoadAlarmFiles();
    _ = InitWV();
    _ = CheckUpdateAsync();
}

protected override void OnFormClosing(FormClosingEventArgs e)
{
    _timer?.Stop();
    Synth.Instance.Stop();   // NAudio 리소스 해제
    SaveConfigFromControls();
    _webView?.Dispose();     // WebView2 리소스 해제
    base.OnFormClosing(e);
}
```

### 업데이트 체크

```csharp
private async Task CheckUpdateAsync()
{
    try
    {
        var result = await UpdateChecker.Check();
        if (result.IsNewVersion)
        {
            var ans = MessageBox.Show(
                $"새 버전({result.LatestTag})이 있습니다.\n다운로드 페이지로 이동할까요?",
                "JHP 업데이트", MessageBoxButtons.YesNo);
            if (ans == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = result.Url, UseShellExecute = true });
        }
    }
    catch { }
}
```

---

## 📄 Form1.Designer.cs

```csharp
namespace JHP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // 컨트롤 초기화는 Form1.cs의 InitControls()에서 진행
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 700);
            Text = "JHP";
            Load += Form1_Load;
            ResumeLayout(false);
        }
    }
}
```

---

## 📝 README.md 및 CHANGELOG.md 템플릿

### README.md

```markdown
# JHP

메이플스토리 재획비 타이머 + OTT 시청 보조 도구

## 다운로드
[Releases 페이지](https://github.com/dlrj0/JHP/releases)에서 최신 버전 다운로드

## 설치 방법
1. zip 압축 해제
2. alarm 폴더에 알람용 mp3 파일 추가
3. JHP.exe 실행

## 주요 기능
- 재획비 타이머 8슬롯 — 좌클릭으로 시작/재시작
- 커스텀 알람 3슬롯
- 내장 브라우저 (WebView2)
- mp3 알람 / TTS 음성 안내
- 사이트 즐겨찾기
- OTT 다음화 자동 스킵 (라프텔, 넷플릭스)
- 항상 위 / 투명도 조절
- 자동 업데이트 확인

## 사용 환경
- Windows 10 / 11 64bit
- Microsoft Edge 설치 필요 (대부분 이미 설치됨)

## 참고사항
- Windows 11: 최초 실행 시 SmartScreen 경고가 표시될 수 있습니다. "추가 정보" → "실행" 클릭 후 정상 사용 가능합니다.
- Windows 7: TTS 음성 안내 기능이 작동하지 않을 수 있습니다.

## 주의사항
본 프로그램은 넥슨의 공식 허가를 받은 프로그램이 아닙니다.
사용으로 발생하는 모든 불이익에 대한 책임은 사용자 본인에게 있습니다.

## 라이선스
MIT
```

### CHANGELOG.md

```markdown
# CHANGELOG

## [v1.0.0]
### 기능
- 재획비 타이머 8슬롯 (2h/1h/30m/20m/15m/10m/100s/55s)
- 커스텀 알람 3슬롯
- 내장 브라우저 (WebView2)
- mp3 알람 / TTS 안내
- 사이트 즐겨찾기
- OTT 다음화 자동 스킵 (라프텔, 넷플릭스)
- 항상 위 / 투명도 조절
- 자동 업데이트 확인 (GitHub Releases)
```

---

## ⚡ 최적화 — 2시간+ 장시간 사용 대비

| 항목 | 적용 내용 |
|------|----------|
| WebView2 캐시 | `./cache` 디스크 경로 지정 (메모리 캐시 누적 방지) |
| NAudio | `Stop()`에서 `Dispose()` — 매 Ring 호출마다 리소스 해제 (이미 Synth.cs에 구현됨) |
| SpeechSynthesizer | 싱글톤 유지, `SpeakAsyncCancelAll()` 후 재사용 (이미 Synth.cs에 구현됨) |
| 타이머 간격 | 1000ms — 더 짧게 하면 CPU 낭비 |
| 경과 시간 추적 | `DateTime.Now - _timerStartTime` 방식 — tick 누락 오차 없음 |
| Form 종료 시 | `Synth.Stop()` + `_webView.Dispose()` 순서로 호출 |

---

## 🖥️ Windows 호환성

| OS | 상태 | 비고 |
|----|------|------|
| Windows 10 | ✅ | WebView2 런타임 별도 설치 필요 (Win11은 내장) |
| Windows 11 | ✅ | SmartScreen 경고: "추가 정보 → 실행" (코드 서명 없는 EXE 일반 현상) |
| Windows 7 | ⚠️ | TTS 미작동 가능 — `Synth.cs`의 try-catch로 이미 처리됨 |

---

## ⚠️ 알려진 버그 및 처리 방법

| 버그 | 처리 방법 |
|------|----------|
| `TopMost=true` 상태에서 자식 창 안 보임 | Dialog 열기 전 `TopMost=false`, 닫힌 후 복원 (ConnectEvents/CmbSite 참조) |
| 창이 화면 밖으로 나가 복귀 불가 | `LoadConfigToControls`에서 `Screen.WorkingArea` 범위 체크 |
| 메뉴 구분선 클릭 시 에러 | ToolStripCommand 처리 시 null/separator 체크 필요 |

---

## 📦 배포

### Release 빌드 + 게시

```
VS 상단: Debug → Release
빌드 → 솔루션 빌드 (Ctrl+Shift+B)

솔루션 탐색기 → JHP 프로젝트 우클릭 → 게시
→ 폴더 → 경로: .../JHP/output
→ 구성: Release / 대상 런타임: win-x64 / 배포 모드: 자체 포함
→ 게시
```

### 배포 후 폴더 정리

```
output\
├── JHP.exe
├── alarm\         ← mp3 파일 여기에 복사
└── cache\         ← 첫 실행 시 자동 생성
```

zip 압축 → GitHub Releases 업로드

---

## ❓ 자주 발생하는 오류

**"System.Speech를 찾을 수 없음"**
```xml
<!-- JHP.Api.csproj -->
<TargetFramework>net10.0-windows</TargetFramework>
```

**WebView2 초기화 실패**
→ https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/ 런타임 설치

**mp3 알람 재생 안 됨**
→ `alarm\` 폴더가 `JHP.exe`와 같은 폴더에 있는지 확인
→ `config.json`의 `alarmName` 값이 실제 mp3 파일명과 일치하는지 확인

**GitHub 푸시 인증 오류**
→ 파일 → 계정 설정 → GitHub → 로그아웃 후 재로그인

**게시 후 exe 즉시 종료**
→ 게시 프로필에서 배포 모드 = **자체 포함** 확인

---

## 🔗 참고 링크

| 항목 | URL |
|------|-----|
| WebView2 런타임 | https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/ |
| .NET 10 Runtime | https://dotnet.microsoft.com/download/dotnet/10.0 |
| 원본 GitHub | https://github.com/d3vdev/JHP |
| 원본 인벤 v1 | https://www.inven.co.kr/board/maple/2304/34038 |
| 원본 인벤 v2 | https://www.inven.co.kr/board/maple/2304/34059 |
