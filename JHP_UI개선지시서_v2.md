# JHP UI 개선 작업 지시서 v2

> **이 파일은 JHP 프로젝트의 Form1.cs / Form1.Designer.cs 구현을 위한 상세 명세서입니다.**
> 반드시 `JHP_완전통합가이드_v2.md`와 함께 Claude에게 첨부하세요.
> 이 지시서를 먼저 읽고 GitHub 파일 확인 없이 바로 구현을 시작할 수 있도록 작성되었습니다.

---

## 📋 현재 파일 상태 요약 (GitHub 직접 확인 완료)

| 파일 | 현재 상태 | 문제 |
|------|-----------|------|
| `Form1.cs` | ❌ 빈 껍데기 | `InitializeComponent()` 한 줄만 있음 |
| `Form1.Designer.cs` | ❌ 빈 껍데기 | `ClientSize = 800x450`, `Text = "Form1"` 만 있음 |
| `AlarmForm.cs` | ⚠️ 구현됨 but TrackBar 사용 | NSlider 미사용, 팝업 형태 — UI 개선 시 역할 변경 |
| `Synth.cs` | ✅ 정상 | `Ring(string alarmName, int volume)` 시그니처 확인됨 |
| `ReSize.cs` | ✅ 정상 | **static class** — 인스턴스 생성 불가, 정적 메서드로 호출 |
| `ControlButton.cs` | ✅ 정상 | Minimize/Maximize/Close 구현 완료 |
| `NSlider.cs` | ✅ 정상 | `Value`, `Minimum`, `Maximum`, `ValueChanged` 이벤트 |
| `Config.cs` | ✅ 정상 | 싱글톤, JSON 읽기/쓰기 완료 |

### ⚠️ 필수 확인 사항 (가이드와 실제 코드 불일치)

```csharp
// Synth.cs 실제 메서드 시그니처
Synth.Instance.Ring(string alarmName, int volume);
Synth.Instance.TTS(string text, int volume, int rate);

// 호출 예시 (Form1에서 사용할 형태)
Synth.Instance.Ring(Config.Instance.AlarmName, Config.Instance.Volume);
Synth.Instance.TTS(labelText, Config.Instance.Volume, Config.Instance.Rate);

// ReSize.cs — static class이므로 인스턴스 생성 없이 직접 호출
ReSize.GetMousePosition(form, cursor);  // Form1에서: ReSize.GetMousePosition(this, pt)
ReSize.SetThick(mousePosition);
```

---

## 🎯 구현 목표 — UI 구조

```
┌──────────────────────────────────────────────────────────┐
│ [사이트 ▼]  [항상위 □] [테두리숨기기 □]    [─][□][×] │  ← 타이틀바 (드래그 이동)
├──────────────────────────────────────────────────────┬───┤
│                                                      │알 │
│                                                      │람 │
│              WebView2 브라우저                       │패 │
│              (좌클릭: 타이머 시작/재시작)            │널 │
│                                                      │   │
│                                                      │   │
└──────────────────────────────────────────────────────┴───┘
```

### 알람 패널 (우측 고정, 약 240px) 상세

```
┌──────────────────────────────┐
│ □ 재획비 (2시간)             │
│ □ 경쿠 (1시간)               │
│ □ 열변/경쿠/오니오 (30분)    │  ← CheckBox 8개
│ □ 오니오 버프 (20분)         │
│ □ 경쿠 (15분)                │
│ □ 오니오 버프 (10분)         │
│ □ 메소 히수 (100초)          │
│ □ 파우티 (55초)              │
├──────────────────────────────┤
│ [이름___] [  0  ▲] 초 □    │
│ [이름___] [  0  ▲] 초 □    │  ← 커스텀 알람 3슬롯
│ [이름___] [  0  ▲] 초 □    │
├──────────────────────────────┤
│ 알람 파일 [경험치업.mp3 ▼]  │  ← alarm 폴더 mp3 목록
│ 볼륨      [────●────]   50  │  ← NSlider + Label
│ TTS 출력                □   │  ← CheckBox
│ TTS 속도  [────●────]       │  ← NSlider
├──────────────────────────────┤
│      [ 알람 설정 열기 ]      │  ← AlarmForm 팝업 (선택사항)
└──────────────────────────────┘
```

---

## 🛠️ Form1.cs 전체 구현 명세

### 네임스페이스 및 using

```csharp
using JHP.Api;
using JHP.Controls;
using JHP.Asset;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
```

### Form 기본 설정

```csharp
// Form1 생성자에서 설정
FormBorderStyle = FormBorderStyle.None;   // 커스텀 창 테두리
BackColor = Color.FromArgb(20, 20, 20);
ForeColor = Color.White;
MinimumSize = new Size(700, 450);
Text = "JHP";
```

### 필드 전체 목록

```csharp
// 레이아웃
private Panel _titleBar = null!;          // 드래그 이동용 타이틀바
private Panel _alarmPanel = null!;        // 우측 알람 패널
private WebView2 _webView = null!;        // 내장 브라우저

// 타이틀바 컨트롤
private ControlButton _btnClose = null!;
private ControlButton _btnMax = null!;
private ControlButton _btnMin = null!;
private ComboBox _cmbSite = null!;        // 사이트 선택 드롭다운
private CheckBox _cbTopMost = null!;
private CheckBox _cbHideBorder = null!;

// 알람 패널 컨트롤
private CheckBox[] _alarmChecks = new CheckBox[8];
private TextBox[] _customNames = new TextBox[3];
private NumericUpDown[] _customTicks = new NumericUpDown[3];
private CheckBox[] _customEnabled = new CheckBox[3];
private ComboBox _cmbAlarmFile = null!;
private NSlider _sliderVolume = null!;
private Label _lblVolume = null!;
private CheckBox _cbTts = null!;
private NSlider _sliderRate = null!;

// 타이머
private System.Windows.Forms.Timer _timer = null!;
private DateTime _timerStartTime = DateTime.MinValue;
private bool _timerRunning = false;

// 창 크기조절 상태
private const int TITLE_HEIGHT = 32;     // 타이틀바 높이(px)
```

### 알람 슬롯 상수

```csharp
// [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
private static readonly int[] AlarmTicks = { 7200, 3600, 1800, 1200, 900, 600, 100, 55 };
private static readonly string[] AlarmLabels =
{
    "재획비 (2시간)", "경쿠 (1시간)", "열변/경쿠/오니오 (30분)",
    "오니오 버프 (20분)", "경쿠 (15분)", "오니오 버프 (10분)",
    "메소 히수 (100초)", "파우티 (55초)"
};
```

---

## 🔧 창 이동 및 크기조절 구현 (FormBorderStyle.None 핵심)

> **이 섹션이 창 닫기/최소화/최대화/이동/크기조절의 핵심입니다.**
> FormBorderStyle.None 사용 시 Windows의 기본 창 동작이 모두 비활성화되므로
> WndProc에서 직접 처리해야 합니다.

### WndProc 재정의 (드래그 이동 + 엣지 리사이즈)

```csharp
protected override void WndProc(ref Message m)
{
    const int WM_NCHITTEST = 0x0084;
    const int HTCLIENT = 1;
    const int HTCAPTION = 2;
    // 리사이즈 방향 상수 (Windows API 표준값)
    const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12;
    const int HTTOPLEFT = 13, HTTOPRIGHT = 14;
    const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    if (m.Msg == WM_NCHITTEST)
    {
        // 최대화 상태에서는 리사이즈 불필요
        if (WindowState != FormWindowState.Normal)
        {
            base.WndProc(ref m);
            return;
        }

        var pt = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF,
                                         (m.LParam.ToInt32() >> 16) & 0xFFFF));

        // ReSize.cs (static)으로 엣지 감지
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
                _                               => (IntPtr)HTCLIENT
            };
            return;
        }

        // 타이틀바 영역: 상단 TITLE_HEIGHT 높이, ControlButton 3개(138px) 제외
        if (pt.Y >= 0 && pt.Y < TITLE_HEIGHT && pt.X < Width - 138)
        {
            m.Result = (IntPtr)HTCAPTION;  // Windows가 드래그 이동 처리
            return;
        }
    }

    base.WndProc(ref m);
}
```

### 커서 업데이트

```csharp
// WndProc WM_NCHITTEST가 처리하므로 OnMouseMove에서는 커서만 업데이트
protected override void OnMouseMove(MouseEventArgs e)
{
    base.OnMouseMove(e);
    if (WindowState == FormWindowState.Normal)
        Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
}
```

### ControlButton 이벤트 연결

```csharp
_btnClose.Type = ControlButton.ButtonType.Close;
_btnClose.Click += (s, e) => Close();

_btnMin.Type = ControlButton.ButtonType.Minimize;
_btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

_btnMax.Type = ControlButton.ButtonType.Maximize;
_btnMax.Click += (s, e) =>
{
    WindowState = WindowState == FormWindowState.Maximized
        ? FormWindowState.Normal
        : FormWindowState.Maximized;
};

// 최대화 상태 변경 시 버튼 아이콘 업데이트
SizeChanged += (s, e) => _btnMax.Invalidate();
```

---

## 🗂️ 레이아웃 구성 (InitControls)

```csharp
private void InitControls()
{
    SuspendLayout();

    // ── 타이틀바 패널 ──
    _titleBar = new Panel
    {
        Dock = DockStyle.Top,
        Height = TITLE_HEIGHT,
        BackColor = Color.FromArgb(28, 28, 28)
    };

    // ControlButton 3개 (우측 정렬)
    _btnClose = new ControlButton { Type = ControlButton.ButtonType.Close, Dock = DockStyle.Right };
    _btnMax   = new ControlButton { Type = ControlButton.ButtonType.Maximize, Dock = DockStyle.Right };
    _btnMin   = new ControlButton { Type = ControlButton.ButtonType.Minimize, Dock = DockStyle.Right };

    // 사이트 드롭다운
    _cmbSite = new ComboBox
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 40, 40),
        ForeColor = Color.White,
        Width = 180,
        Left = 8,
        Top = (TITLE_HEIGHT - 22) / 2
    };

    // 항상 위 체크박스
    _cbTopMost = new CheckBox
    {
        Text = "항상 위",
        ForeColor = Color.LightGray,
        AutoSize = true,
        Left = 200,
        Top = (TITLE_HEIGHT - 17) / 2
    };

    // 테두리 숨기기 체크박스
    _cbHideBorder = new CheckBox
    {
        Text = "테두리 숨기기",
        ForeColor = Color.LightGray,
        AutoSize = true,
        Left = 280,
        Top = (TITLE_HEIGHT - 17) / 2
    };

    _titleBar.Controls.AddRange(new Control[] { _btnClose, _btnMax, _btnMin, _cmbSite, _cbTopMost, _cbHideBorder });

    // ── 본문 SplitContainer ──
    var split = new SplitContainer
    {
        Dock = DockStyle.Fill,
        FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 1,
        SplitterDistance = 500,   // 초기값, Form_Load에서 재계산
        IsSplitterFixed = true,
        BackColor = Color.FromArgb(20, 20, 20)
    };

    // ── 좌측: WebView2 ──
    _webView = new WebView2 { Dock = DockStyle.Fill };
    // 좌클릭으로 타이머 시작/재시작
    _webView.MouseClick += (s, e) =>
    {
        if (e.Button == MouseButtons.Left) StartTimer();
    };
    split.Panel1.Controls.Add(_webView);

    // ── 우측: 알람 패널 ──
    _alarmPanel = BuildAlarmPanel();
    split.Panel2.Controls.Add(_alarmPanel);
    split.Panel2MinSize = 240;

    Controls.AddRange(new Control[] { split, _titleBar });

    // 이벤트 연결
    ConnectEvents();

    ResumeLayout(true);
}
```

---

## 🔔 알람 패널 구성 (BuildAlarmPanel)

```csharp
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

    // ── 고정 알람 체크박스 8개 ──
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

    // ── 커스텀 알람 3슬롯 ──
    for (int i = 0; i < 3; i++)
    {
        _customEnabled[i] = new CheckBox { Left = 8, Top = y + 2, Width = 18 };
        panel.Controls.Add(_customEnabled[i]);

        _customNames[i] = new TextBox
        {
            Left = 30, Top = y, Width = 95,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            PlaceholderText = "이름"
        };
        panel.Controls.Add(_customNames[i]);

        _customTicks[i] = new NumericUpDown
        {
            Left = 130, Top = y, Width = 64,
            Minimum = 0, Maximum = 99999,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        panel.Controls.Add(_customTicks[i]);

        var lblSec = new Label { Text = "초", Left = 198, Top = y + 3, Width = 20, ForeColor = Color.LightGray };
        panel.Controls.Add(lblSec);

        y += 30;
    }

    y += 4;
    AddSeparator(panel, y); y += 10;

    // ── 알람 파일 선택 ──
    var lblAlarm = new Label { Text = "알람 파일", Left = 8, Top = y, Width = 65, ForeColor = Color.LightGray };
    panel.Controls.Add(lblAlarm);

    _cmbAlarmFile = new ComboBox
    {
        Left = 76, Top = y - 2, Width = 148,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 40, 40),
        ForeColor = Color.White
    };
    panel.Controls.Add(_cmbAlarmFile);
    y += 30;

    // ── 볼륨 슬라이더 ──
    var lblVolTitle = new Label { Text = "볼륨", Left = 8, Top = y + 4, Width = 35, ForeColor = Color.LightGray };
    panel.Controls.Add(lblVolTitle);

    _sliderVolume = new NSlider { Left = 46, Top = y, Width = 145, Height = 20, Minimum = 0, Maximum = 100 };
    panel.Controls.Add(_sliderVolume);

    _lblVolume = new Label { Left = 196, Top = y + 3, Width = 30, ForeColor = Color.White, Text = "50" };
    panel.Controls.Add(_lblVolume);
    y += 28;

    // ── TTS 체크박스 ──
    _cbTts = new CheckBox { Text = "TTS 출력", Left = 8, Top = y, Width = 100, ForeColor = Color.White };
    panel.Controls.Add(_cbTts);
    y += 26;

    // ── TTS 속도 슬라이더 ──
    var lblRate = new Label { Text = "TTS 속도", Left = 8, Top = y + 4, Width = 60, ForeColor = Color.LightGray };
    panel.Controls.Add(lblRate);

    _sliderRate = new NSlider { Left = 72, Top = y, Width = 120, Height = 20, Minimum = -10, Maximum = 10 };
    panel.Controls.Add(_sliderRate);
    y += 28;

    AddSeparator(panel, y); y += 10;

    // ── 알람 설정 버튼 ──
    var btnAlarm = new Button
    {
        Text = "알람 설정 열기",
        Left = 8, Top = y, Width = 218, Height = 28,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(55, 90, 145),
        ForeColor = Color.White,
        Cursor = Cursors.Hand
    };
    btnAlarm.FlatAppearance.BorderSize = 0;
    btnAlarm.Click += (s, e) =>
    {
        bool was = TopMost;
        TopMost = false;            // ⚠️ 원본 버그: TopMost시 자식창 안보임 → 해제 후 열기
        using var af = new AlarmForm();
        af.ShowDialog(this);
        TopMost = was;
        LoadAlarmFiles();           // 파일 목록 갱신
        LoadConfigToControls();     // 변경사항 반영
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
```

---

## 🔗 이벤트 연결 (ConnectEvents)

```csharp
private void ConnectEvents()
{
    // ControlButton
    _btnClose.Click += (s, e) => Close();
    _btnMin.Click   += (s, e) => WindowState = FormWindowState.Minimized;
    _btnMax.Click   += (s, e) =>
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal : FormWindowState.Maximized;

    // 최대화 버튼 아이콘 업데이트
    SizeChanged += (s, e) => _btnMax?.Invalidate();

    // 항상 위
    _cbTopMost.CheckedChanged += (s, e) =>
    {
        TopMost = _cbTopMost.Checked;
        Config.Instance.TopMost = _cbTopMost.Checked;
    };

    // 테두리 숨기기 (포커스 이탈 시)
    _cbHideBorder.CheckedChanged += (s, e) =>
        Config.Instance.IsHideWindowBorderOnFocusOut = _cbHideBorder.Checked;

    // 사이트 선택
    _cmbSite.SelectedIndexChanged += CmbSite_SelectedIndexChanged;

    // 볼륨 슬라이더
    _sliderVolume.ValueChanged += (s, e) =>
    {
        Config.Instance.Volume = _sliderVolume.Value;
        Synth.Instance.SetVolume(_sliderVolume.Value);
        _lblVolume.Text = _sliderVolume.Value.ToString();
    };

    // TTS 속도
    _sliderRate.ValueChanged += (s, e) =>
    {
        Config.Instance.Rate = _sliderRate.Value;
        Synth.Instance.SetRate(_sliderRate.Value);
    };

    // TTS 체크박스
    _cbTts.CheckedChanged += (s, e) => Config.Instance.Tts = _cbTts.Checked;

    // 알람 파일 선택
    _cmbAlarmFile.SelectedIndexChanged += (s, e) =>
    {
        if (_cmbAlarmFile.SelectedItem is string name)
            Config.Instance.AlarmName = name;
    };

    // 알람 체크박스 8개 → Config 즉시 반영
    for (int i = 0; i < 8; i++)
    {
        int idx = i;
        _alarmChecks[i].CheckedChanged += (s, e) =>
            Config.Instance.AlarmEnabled[idx] = _alarmChecks[idx].Checked;
    }

    // 커스텀 알람 즉시 반영
    for (int i = 0; i < 3; i++)
    {
        int idx = i;
        _customNames[i].TextChanged += (s, e) =>
            Config.Instance.CustomAlarms[idx].Name = _customNames[idx].Text;
        _customTicks[i].ValueChanged += (s, e) =>
            Config.Instance.CustomAlarms[idx].Tick = (int)_customTicks[idx].Value;
        _customEnabled[i].CheckedChanged += (s, e) =>
            Config.Instance.CustomAlarms[idx].Enabled = _customEnabled[idx].Checked;
    }

    // 포커스 이탈 시 테두리 숨기기
    Activated  += (s, e) => { if (_titleBar != null) _titleBar.Visible = true; };
    Deactivate += (s, e) =>
    {
        if (Config.Instance.IsHideWindowBorderOnFocusOut)
            _titleBar.Visible = false;
    };
}
```

---

## 🌐 WebView2 초기화 (InitWV)

```csharp
private async Task InitWV()
{
    try
    {
        var env = await CoreWebView2Environment.CreateAsync(
            null,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache")
            // WebView2 캐시를 디스크에 저장 → 장시간 사용 시 메모리 누적 방지
        );
        await _webView.EnsureCoreWebView2Async(env);

        // JS 주입 (OTT 다음화 스킵)
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            UserScripts.LaftelSkipNext);
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            UserScripts.NetflixSkipNext);

        // 기본 URL 로드
        string url = Config.Instance.LatestUrl;
        if (string.IsNullOrEmpty(url) && Config.Instance.Sites.Count > 0)
            url = Config.Instance.Sites[0].Url;
        if (!string.IsNullOrEmpty(url))
            _webView.Source = new Uri(url);
    }
    catch
    {
        // WebView2 런타임 미설치 시 안내
        MessageBox.Show(
            "WebView2 런타임이 설치되지 않았습니다.\n" +
            "https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/\n" +
            "에서 설치 후 다시 실행해주세요.",
            "JHP — 런타임 오류",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning
        );
    }
}
```

---

## ⏱️ 타이머 로직

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

    // DateTime 기반 경과 시간 (tick 누락 없음)
    int elapsed = (int)(DateTime.Now - _timerStartTime).TotalSeconds;

    // 고정 알람 8개 체크
    for (int i = 0; i < 8; i++)
    {
        if (_alarmChecks[i].Checked && elapsed == AlarmTicks[i])
            FireAlarm(AlarmLabels[i]);
    }

    // 커스텀 알람 3개 체크
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

---

## 🌍 사이트 드롭다운 처리

```csharp
private void LoadSites()
{
    _cmbSite.SelectedIndexChanged -= CmbSite_SelectedIndexChanged;
    _cmbSite.Items.Clear();
    foreach (var site in Config.Instance.Sites)
        _cmbSite.Items.Add(site.Name);
    _cmbSite.Items.Add("＋ 사이트 추가");
    _cmbSite.Items.Add("━ 사이트 관리");

    // 기본 사이트 선택
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
        TopMost = false;            // ⚠️ TopMost 버그 방지
        using var sf = new SiteForm();
        if (sf.ShowDialog(this) == DialogResult.OK && sf.Result != null)
        {
            Config.Instance.Sites.Add(sf.Result);
            Config.Instance.Save();
            LoadSites();
            _cmbSite.SelectedItem = sf.Result.Name;
        }
        else
        {
            LoadSites();
        }
        TopMost = was;
        return;
    }

    if (selected == "━ 사이트 관리")
    {
        // 사이트 관리 팝업 (Prompt.cs 또는 별도 구현)
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

---

## 📂 Config 로드/저장

```csharp
private void LoadConfigToControls()
{
    var cfg = Config.Instance;

    // 창 위치/크기
    if (cfg.IsMaximize)
        WindowState = FormWindowState.Maximized;
    else
    {
        Width  = Math.Max(cfg.Width, MinimumSize.Width);
        Height = Math.Max(cfg.Height, MinimumSize.Height);

        // 화면 범위 벗어남 방지
        var screen = Screen.FromPoint(new Point(cfg.X, cfg.Y));
        Left = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.X : 100;
        Top  = screen.WorkingArea.Contains(cfg.X, cfg.Y) ? cfg.Y : 100;
    }

    Opacity = Math.Clamp(cfg.Opacity, 0.2, 1.0);
    TopMost = cfg.TopMost;
    _cbTopMost.Checked    = cfg.TopMost;
    _cbHideBorder.Checked = cfg.IsHideWindowBorderOnFocusOut;

    // 알람 체크박스
    for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];

    // 커스텀 알람
    for (int i = 0; i < 3; i++)
    {
        _customNames[i].Text    = cfg.CustomAlarms[i].Name;
        _customTicks[i].Value   = cfg.CustomAlarms[i].Tick;
        _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
    }

    // 슬라이더
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

    for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
    for (int i = 0; i < 3; i++)
    {
        cfg.CustomAlarms[i].Name    = _customNames[i].Text;
        cfg.CustomAlarms[i].Tick    = (int)_customTicks[i].Value;
        cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
    }

    cfg.AlarmName = _cmbAlarmFile.SelectedItem?.ToString() ?? cfg.AlarmName;
    cfg.Tts       = _cbTts.Checked;
    cfg.Volume    = _sliderVolume.Value;
    cfg.Rate      = _sliderRate.Value;

    try { cfg.LatestUrl = _webView.Source?.ToString() ?? ""; } catch { }

    cfg.Save();
}
```

---

## 📁 알람 파일 목록 로드

```csharp
private void LoadAlarmFiles()
{
    _cmbAlarmFile.Items.Clear();
    string alarmDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alarm");
    if (Directory.Exists(alarmDir))
    {
        foreach (var f in Directory.GetFiles(alarmDir, "*.mp3"))
            _cmbAlarmFile.Items.Add(Path.GetFileName(f));
    }

    if (_cmbAlarmFile.Items.Count == 0)
        _cmbAlarmFile.Items.Add("(mp3 파일 없음)");

    if (!string.IsNullOrEmpty(Config.Instance.AlarmName) &&
        _cmbAlarmFile.Items.Contains(Config.Instance.AlarmName))
        _cmbAlarmFile.SelectedItem = Config.Instance.AlarmName;
    else if (_cmbAlarmFile.Items.Count > 0)
        _cmbAlarmFile.SelectedIndex = 0;
}
```

---

## 🚀 Form1_Load 및 Form1_Closing

```csharp
private async void Form1_Load(object? sender, EventArgs e)
{
    LoadConfigToControls();
    LoadSites();
    LoadAlarmFiles();
    _ = InitWV();
    _ = CheckUpdateAsync();

    _timer = new System.Windows.Forms.Timer { Interval = 1000 };
    _timer.Tick += Timer_Tick;
}

protected override void OnFormClosing(FormClosingEventArgs e)
{
    _timer?.Stop();
    Synth.Instance.Stop();      // NAudio 리소스 해제
    SaveConfigFromControls();
    _webView?.Dispose();        // WebView2 리소스 해제
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

## 📄 Form1.Designer.cs 최소 구성

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
            // 모든 컨트롤 초기화는 Form1.cs의 InitControls()에서 진행
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

## 🎨 UI 컬러 팔레트

| 역할 | Color |
|------|-------|
| 폼/창 배경 | `Color.FromArgb(20, 20, 20)` |
| 패널 배경 | `Color.FromArgb(28, 28, 28)` |
| 입력 필드 배경 | `Color.FromArgb(40, 40, 40)` |
| 기본 텍스트 | `Color.White` |
| 보조 텍스트 | `Color.LightGray` |
| 구분선 | `Color.FromArgb(55, 55, 55)` |
| 버튼 (일반) | `Color.FromArgb(60, 60, 60)` |
| 버튼 (강조) | `Color.FromArgb(55, 90, 145)` |
| 닫기 버튼 호버 | `Color.FromArgb(196, 43, 28)` |
| 슬라이더 채움 | `Color.FromArgb(100, 160, 255)` |

---

## ⚠️ 알려진 버그 및 원본 개발자 공지 사항

### 반드시 처리해야 하는 버그

| 버그 | 발생 조건 | 처리 방법 |
|------|----------|-----------|
| 사이트 추가창 안 보임 | `TopMost = true` 상태에서 ShowDialog | Dialog 열기 전 `TopMost = false`, 닫힌 후 복원 |
| 알람 설정창 안 보임 | 동일 | 동일 |
| 메뉴 구분선 클릭 에러 | ToolStripSeparator 클릭 이벤트 | ToolStripCommand 처리 시 null 체크 |
| 창 화면 밖 이탈 | Config X/Y가 다른 모니터 설정값 | `LoadConfigToControls`에서 `Screen.WorkingArea` 범위 체크 |

### 호환성 공지 (원본 인벤 게시글 기반)

- **Windows 7**: TTS 미설치 → Synth.cs try-catch로 이미 처리됨. 추가 조치 불필요
- **Windows 11**: SmartScreen 경고 → 코드 내 처리 불가, README에 안내 문구만 추가

---

## ✅ 완료 체크리스트

```
□ Form1.cs 작성 (InitControls, InitWV, Timer, 이벤트 연결 모두 포함)
□ Form1.Designer.cs 최소 껍데기 작성
□ FormBorderStyle.None + WndProc 드래그/리사이즈 구현
□ ControlButton 3개 (Close/Max/Min) 정상 동작
□ WebView2 좌측 배치 및 초기화
□ 알람 패널 우측 배치 (NSlider 사용, TrackBar 금지)
□ 볼륨 슬라이더 옆 수치 Label 표시
□ alarm 폴더 mp3 목록이 ComboBox에 로드됨
□ 좌클릭으로 타이머 시작/재시작
□ Config 저장/불러오기 정상 (창 크기, 위치, 알람 상태 포함)
□ TopMost 상태에서 자식 다이얼로그 열릴 때 TopMost 임시 해제 처리
□ Form 닫힐 때 Synth.Stop() + _webView.Dispose() 호출
□ 빌드 오류 없음 (Ctrl+Shift+B)
□ JHP.exe 실행 후 WebView2 로드, 창 이동/리사이즈 동작 확인
□ 커밋 & 푸시: "feat: Form1.cs 구현 — 창 컨트롤 및 알람패널 통합"
```

---

## 📎 Claude에게 요청 시 복사할 프로젝트 개요

```
[프로젝트 개요]
- 이름: JHP
- 종류: .NET 10.0 WinForms + WebView2 내장 브라우저
- 목적: 메이플스토리 재획비 타이머 + OTT 시청 보조 도구
- 솔루션: JHP.slnx (로컬: C:\Users\wjgus\source\repos\dlrj0\JHP)
- 프로젝트 4개: JHP(UI), JHP.Api(로직), JHP.Controls(커스텀 UI), JHP.Asset(JS)
- NuGet: Microsoft.Web.WebView2, NAudio, Octokit, System.Speech
- 현재 작업: Form1.cs, Form1.Designer.cs 작성
- GitHub: https://github.com/dlrj0/JHP
- 모든 작업: Visual Studio GUI만 사용 (터미널/CLI 없음)
- 핵심 불일치: Synth.Ring(name, volume), Synth.TTS(text, volume, rate), ReSize는 static class
```
