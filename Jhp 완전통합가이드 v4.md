# JHP 완전통합가이드 v4

> **새 Claude 대화 시작 시 이 파일을 붙여넣으세요.**
> 이전 가이드(`v3`)와 진행상황 문서(`v5`)는 실제 GitHub 코드와 일치하지 않습니다 — 이 문서(v4)가 현재 기준입니다.
> 작업 전 반드시 `raw.githubusercontent.com`에서 직접 fetch하여 실제 상태를 재확인하세요.

---

## 🗂️ 프로젝트 구조 (test 브랜치 기준)

```
JHP/ (솔루션 루트)
├── JHP/                 # 메인 WinForms 앱
│   ├── Form1.cs
│   ├── Form1.Designer.cs
│   ├── AlarmForm.cs
│   ├── SiteForm.cs
│   └── Program.cs
├── JHP.Api/             # 공용 유틸
│   ├── Config.cs
│   ├── ReSize.cs        ← 리사이즈 헬퍼 (static class)
│   ├── Synth.cs
│   ├── Site.cs
│   ├── CustomAlarm.cs
│   ├── ToolStripCommand.cs
│   └── UpdateChecker.cs
├── JHP.Controls/        # 커스텀 컨트롤
│   ├── ControlButton.cs
│   ├── TimerButton.cs
│   ├── NSlider.cs
│   ├── SiteListViewControl.cs
│   ├── CustomCheckBox.cs
│   └── DarkMenuRenderer.cs
└── JHP.Asset/
    └── UserScripts.cs
```

---

## 📎 실제 API 시그니처 (변경 없음)

```csharp
Synth.Instance.Ring(string alarmName, int volume);
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();

// ReSize — static class, Form1.cs에서 WM_NCHITTEST 처리에 사용
ReSize.GetMousePosition(Form form, Point cursor);  // → ReSize.MousePosition enum
ReSize.SetThick(ReSize.MousePosition pos);         // → Cursor

// Config (싱글턴)
Config.Instance.Volume / Rate / Tts / AlarmName
Config.Instance.AlarmEnabled[8]   // bool[]
Config.Instance.CustomAlarms       // CustomAlarm[3] — .Name, .Tick, .Enabled
Config.Instance.TopMost / IsHideWindowBorderOnFocusOut / Opacity
Config.Instance.Sites              // List<Site>
Config.Instance.DefaultSite
Config.Instance.Save()
```

---

## ✅ 현재 GitHub(test 브랜치) 실제 상태

### 완성된 파일
| 파일 | 상태 |
|------|------|
| `JHP.Controls/TimerButton.cs` | ✅ 완성 — `OnHandleCreated` 포함, 좌클릭/우클릭 분리, `Active` 프로퍼티 |
| `JHP.Api/ReSize.cs` | ✅ 완성 — 8방향 리사이즈, `RESIZE_THICK = 8` |
| `JHP/Form1.cs` | ✅ 현재 작동 중 (단, 아래 미완료 작업 적용 전 상태) |
| `JHP/SiteForm.cs` | ✅ 건드리지 않음 (이후 V버튼 작업 때 재사용 예정) |

### 미완료 — 다음 세션에서 적용 필요한 파일 (4개)

아래는 v5 문서에서 "완료"라고 했지만 **실제 GitHub에 반영되지 않은** 코드들입니다.
전체 파일을 통째로 교체하면 됩니다.

---

#### 1. `JHP.Controls/ControlButton.cs`
**변경 내용:** `OnHandleCreated` 오버라이드 추가 (초기 렌더링 투명 배경 버그 수정, TimerButton과 동일 패턴)

```csharp
namespace JHP.Controls;

using System.ComponentModel;

public class ControlButton : Button
{
    public enum ButtonType { Minimize, Maximize, Close }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonType Type { get; set; } = ButtonType.Close;

    public ControlButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        Size = new Size(46, 30);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));

        Color bg = hovered
            ? (Type == ButtonType.Close ? Color.FromArgb(196, 43, 28) : Color.FromArgb(60, 60, 60))
            : Color.Transparent;

        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        int cx = Width / 2, cy = Height / 2, r = 5;
        using var pen = new Pen(Color.White, 1.2f);

        switch (Type)
        {
            case ButtonType.Close:
                g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                break;
            case ButtonType.Minimize:
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
                break;
            case ButtonType.Maximize:
                g.DrawRectangle(pen, cx - r, cy - r, r * 2, r * 2);
                break;
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

    // 초기 렌더링 버그 수정: 핸들 생성 직후 강제로 한 번 더 그려줌 (TimerButton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
```

---

#### 2. `JHP/Form1.Designer.cs`
**변경 내용:**
- `lblNextAlarm`, `btnAlarmSettings` 제거 → `timerButton`(TimerButton 컨트롤)으로 교체
- `sliderVolume`, `sliderOpacity` 90px → 170px 확장 + 좌표 재배치
- 인라인 숫자 편집용 `tbInlineEdit`(TextBox, 기본 Hidden) 추가
- `toolTip` 컴포넌트 추가

```csharp
using JHP.Api;
using JHP.Controls;
using Microsoft.Web.WebView2.WinForms;

namespace JHP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webView?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.ToolTip toolTip;

        private Panel pnlTitleBar;
        private Label lblTitle;
        private TimerButton timerButton;
        private Label lblVolumeValue;
        private Label lblOpacityValue;
        private NSlider sliderVolume;
        private NSlider sliderOpacity;
        private TextBox tbInlineEdit;
        private ControlButton btnMinimize;
        private ControlButton btnMaximize;
        private ControlButton btnClose;

        private Panel pnlMenuBar;
        private MenuStrip menuStrip;
        private ToolStripMenuItem menuAddSite;
        private ToolStripMenuItem menuTopmost;
        private ToolStripMenuItem menuHideBorder;

        private Panel pnlSidebar;
        private SiteListViewControl siteList;
        private Panel pnlSidebarBottom;
        private Button btnAddSite;
        private Button btnRemoveSite;

        private WebView2 webView;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            timer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            toolTip = new System.Windows.Forms.ToolTip(components);

            pnlTitleBar = new Panel();
            lblTitle = new Label();
            timerButton = new TimerButton();
            lblVolumeValue = new Label();
            lblOpacityValue = new Label();
            sliderVolume = new NSlider();
            sliderOpacity = new NSlider();
            tbInlineEdit = new TextBox();
            btnMinimize = new ControlButton();
            btnMaximize = new ControlButton();
            btnClose = new ControlButton();

            pnlMenuBar = new Panel();
            menuStrip = new MenuStrip();
            menuAddSite = new ToolStripMenuItem();
            menuTopmost = new ToolStripMenuItem();
            menuHideBorder = new ToolStripMenuItem();

            pnlSidebar = new Panel();
            siteList = new SiteListViewControl();
            pnlSidebarBottom = new Panel();
            btnAddSite = new Button();
            btnRemoveSite = new Button();

            webView = new WebView2();

            SuspendLayout();

            // ===== 타이틀바 =====
            pnlTitleBar.Dock = DockStyle.Top;
            pnlTitleBar.Height = 36;
            pnlTitleBar.BackColor = Color.FromArgb(28, 28, 28);

            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(12, 9);
            lblTitle.Text = "JHP";

            // timerButton: lblNextAlarm + btnAlarmSettings 대체
            // 좌클릭=타이머 시작/정지, 우클릭=알람설정
            timerButton.Location = new Point(54, 3);
            timerButton.Size = new Size(30, 30);

            lblVolumeValue.AutoSize = true;
            lblVolumeValue.ForeColor = Color.LightGray;
            lblVolumeValue.Location = new Point(100, 10);
            lblVolumeValue.Text = "볼륨 50";
            lblVolumeValue.Cursor = Cursors.IBeam;

            sliderVolume.Location = new Point(158, 11);
            sliderVolume.Size = new Size(170, 20);
            sliderVolume.Minimum = 0;
            sliderVolume.Maximum = 100;

            lblOpacityValue.AutoSize = true;
            lblOpacityValue.ForeColor = Color.LightGray;
            lblOpacityValue.Location = new Point(342, 10);
            lblOpacityValue.Text = "투명도 100%";
            lblOpacityValue.Cursor = Cursors.IBeam;

            sliderOpacity.Location = new Point(430, 11);
            sliderOpacity.Size = new Size(170, 20);
            sliderOpacity.Minimum = 30;
            sliderOpacity.Maximum = 100;

            // 인라인 직접입력 TextBox (기본 숨김, 볼륨/투명도 숫자 클릭 시 표시)
            tbInlineEdit.Visible = false;
            tbInlineEdit.Size = new Size(60, 20);
            tbInlineEdit.BackColor = Color.FromArgb(50, 50, 50);
            tbInlineEdit.ForeColor = Color.White;
            tbInlineEdit.BorderStyle = BorderStyle.FixedSingle;

            btnMinimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMinimize.Location = new Point(862, 3);
            btnMinimize.Type = ControlButton.ButtonType.Minimize;

            btnMaximize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMaximize.Location = new Point(908, 3);
            btnMaximize.Type = ControlButton.ButtonType.Maximize;

            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(954, 3);
            btnClose.Type = ControlButton.ButtonType.Close;

            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(timerButton);
            pnlTitleBar.Controls.Add(lblVolumeValue);
            pnlTitleBar.Controls.Add(sliderVolume);
            pnlTitleBar.Controls.Add(lblOpacityValue);
            pnlTitleBar.Controls.Add(sliderOpacity);
            pnlTitleBar.Controls.Add(tbInlineEdit);
            pnlTitleBar.Controls.Add(btnMinimize);
            pnlTitleBar.Controls.Add(btnMaximize);
            pnlTitleBar.Controls.Add(btnClose);

            // ===== 메뉴바 =====
            pnlMenuBar.Dock = DockStyle.Top;
            pnlMenuBar.Height = 28;
            pnlMenuBar.BackColor = Color.FromArgb(40, 40, 40);

            menuAddSite.Text = "사이트 추가";
            menuAddSite.Tag = ToolStripCommand.ADD_SITE;

            menuTopmost.Text = "항상 위";
            menuTopmost.CheckOnClick = true;
            menuTopmost.Tag = ToolStripCommand.TOPMOST;

            menuHideBorder.Text = "테두리 숨김 (포커스 아웃)";
            menuHideBorder.CheckOnClick = true;
            menuHideBorder.Tag = ToolStripCommand.TOGGLE_HIDE_WINDOW_BORDER;

            menuStrip.BackColor = Color.FromArgb(40, 40, 40);
            menuStrip.ForeColor = Color.White;
            menuStrip.Dock = DockStyle.Fill;
            menuStrip.GripStyle = ToolStripGripStyle.Hidden;
            menuStrip.Items.Add(menuAddSite);
            menuStrip.Items.Add(menuTopmost);
            menuStrip.Items.Add(menuHideBorder);

            pnlMenuBar.Controls.Add(menuStrip);

            // ===== 사이드바 =====
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.Width = 180;
            pnlSidebar.BackColor = Color.FromArgb(30, 30, 30);

            siteList.Dock = DockStyle.Fill;

            pnlSidebarBottom.Dock = DockStyle.Bottom;
            pnlSidebarBottom.Height = 34;
            pnlSidebarBottom.BackColor = Color.FromArgb(40, 40, 40);

            btnAddSite.Dock = DockStyle.Left;
            btnAddSite.Width = 90;
            btnAddSite.FlatStyle = FlatStyle.Flat;
            btnAddSite.FlatAppearance.BorderSize = 0;
            btnAddSite.BackColor = Color.FromArgb(55, 90, 145);
            btnAddSite.ForeColor = Color.White;
            btnAddSite.Text = "추가";
            btnAddSite.UseVisualStyleBackColor = false;

            btnRemoveSite.Dock = DockStyle.Right;
            btnRemoveSite.Width = 90;
            btnRemoveSite.FlatStyle = FlatStyle.Flat;
            btnRemoveSite.FlatAppearance.BorderSize = 0;
            btnRemoveSite.BackColor = Color.FromArgb(60, 60, 60);
            btnRemoveSite.ForeColor = Color.White;
            btnRemoveSite.Text = "삭제";
            btnRemoveSite.UseVisualStyleBackColor = false;

            pnlSidebarBottom.Controls.Add(btnAddSite);
            pnlSidebarBottom.Controls.Add(btnRemoveSite);

            pnlSidebar.Controls.Add(siteList);
            pnlSidebar.Controls.Add(pnlSidebarBottom);

            // ===== 웹뷰 =====
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(24, 24, 24);

            // ===== Form1 =====
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(24, 24, 24);
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(760, 420);
            FormBorderStyle = FormBorderStyle.None;
            Text = "JHP";

            Controls.Add(pnlTitleBar);
            Controls.Add(pnlMenuBar);
            Controls.Add(pnlSidebar);
            Controls.Add(webView);

            ResumeLayout(false);
        }
    }
}
```

---

#### 3. `JHP/Form1.cs`
**변경 내용:**
- `_timerRunning` 필드 추가, `ToggleTimer()` 구현 (정지=초기화, 재시작 시 항상 리셋)
- `timerButton.Click` → `ToggleTimer()`, `timerButton.RightClick` → `OpenAlarmSettings()`
- `Timer_Tick`에 `if (!_timerRunning) return;` 가드 (앱 시작 시 자동 시작 안 함)
- `UpdateNextAlarmLabel()` → `toolTip.SetToolTip(timerButton, ...)` 으로 변경
- `BeginInlineEdit` / `CommitInlineEdit` / `InlineEdit_KeyDown` 추가 (볼륨/투명도 숫자 클릭 시 직접입력)
- 타이틀바 드래그에서 `lblNextAlarm` 제거 → `timerButton` 위치 제외한 나머지 패널 영역으로 처리

```csharp
using JHP.Api;
using JHP.Asset;
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

        // 타이머는 자동 시작 안 함 — 사용자가 timerButton 클릭 시 시작
        // (timer.Start()는 ToggleTimer() 에서만 호출)

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
        toolTip.SetToolTip(timerButton, "다음 알람: --:--:--");

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
        // 타이머 정지 상태면 Tick 무시 (이중 안전장치)
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
            case ToolStripCommand.ADD_SITE: AddSite(); break;
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

    // ===== 포커스 아웃 시 테두리 숨김 =====
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
```

---

#### 4. `JHP/AlarmForm.cs`
**변경 내용:**
- `FormBorderStyle.FixedDialog` → `FormBorderStyle.None`
- 자체 다크 타이틀바(`_pnlTitle`) + 드래그 이동 + `ControlButton` 닫기 버튼 추가
- `WM_NCHITTEST` + `ReSize` 패턴으로 8방향 리사이즈 지원
- 기본 폭 420 → 600
- 본문 컨트롤에 `Anchor` 설정 (창 늘리면 입력칸/버튼이 따라 늘어남)
- 체크박스 컬럼 폭 94px → 135px

```csharp
using JHP.Api;
using JHP.Controls;
using System.Runtime.InteropServices;

namespace JHP;

public class AlarmForm : Form
{
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];

    private Panel _pnlTitle = null!;
    private TextBox _tbAlarmName = null!;
    private CheckBox _cbTts = null!;
    private TrackBar _tbVolume = null!;
    private TrackBar _tbRate = null!;
    private Label _lblVolume = null!;
    private Label _lblRate = null!;

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        Text = "알람 설정";
        Width = 600;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        MinimumSize = new Size(480, 360);

        // ===== 자체 타이틀바 =====
        _pnlTitle = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        var lblCaption = new Label
        {
            Text = "알람 설정",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 7)
        };

        var btnClose = new ControlButton
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(_pnlTitle.Width - 46, 1),
            Type = ControlButton.ButtonType.Close
        };
        btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _pnlTitle.Controls.Add(lblCaption);
        _pnlTitle.Controls.Add(btnClose);
        _pnlTitle.MouseDown += TitleBar_MouseDown;
        lblCaption.MouseDown += TitleBar_MouseDown;

        Controls.Add(_pnlTitle);

        // ===== 본문 =====
        int y = 44; // 타이틀바 아래에서 시작

        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 4) * 135,
                Top = y + (i / 4) * 28,
                Width = 128,
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        AddLabel("알람 파일명", 12, y + 3, 90);
        _tbAlarmName = new TextBox
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_tbAlarmName);
        y += 32;

        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        Controls.Add(_cbTts);
        y += 30;

        _lblVolume = AddLabel("볼륨: 0", 12, y + 8, 90);
        _tbVolume = new TrackBar
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Minimum = 0, Maximum = 100, TickFrequency = 10,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 42;

        _lblRate = AddLabel("속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Minimum = -10, Maximum = 10, TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"속도: {_tbRate.Value}";
        Controls.Add(_tbRate);
        y += 42;

        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20 };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36, Top = y, Width = 190, PlaceholderText = "알람 이름",
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 234, Top = y, Width = 80, Minimum = 0, Maximum = 99999
            };
            Controls.Add(_customTicks[i]);

            AddLabel("초", 320, y + 3, 30);
            y += 32;
        }

        y += 8;

        var btnOk = new Button
        {
            Text = "확인",
            Left = ClientSize.Width - 180,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "취소",
            Left = ClientSize.Width - 92,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        Controls.Add(btnCancel);

        Height = y + 72;
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private Label AddLabel(string text, int left, int top, int width)
    {
        var lbl = new Label { Text = text, Left = left, Top = top, Width = width, ForeColor = Color.LightGray };
        Controls.Add(lbl);
        return lbl;
    }

    private void LoadFromConfig()
    {
        var cfg = Config.Instance;
        for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];
        _tbAlarmName.Text = cfg.AlarmName;
        _cbTts.Checked = cfg.Tts;
        _tbVolume.Value = cfg.Volume;
        _tbRate.Value = cfg.Rate;
        _lblVolume.Text = $"볼륨: {cfg.Volume}";
        _lblRate.Text = $"속도: {cfg.Rate}";
        for (int i = 0; i < 3; i++)
        {
            _customNames[i].Text = cfg.CustomAlarms[i].Name;
            _customTicks[i].Value = cfg.CustomAlarms[i].Tick;
            _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var cfg = Config.Instance;
        for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
        cfg.AlarmName = _tbAlarmName.Text;
        cfg.Tts = _cbTts.Checked;
        cfg.Volume = _tbVolume.Value;
        cfg.Rate = _tbRate.Value;
        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name = _customNames[i].Text;
            cfg.CustomAlarms[i].Tick = (int)_customTicks[i].Value;
            cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
        }
        cfg.Save();
        Synth.Instance.SetVolume(cfg.Volume);
        Synth.Instance.SetRate(cfg.Rate);
    }

    // ===== 타이틀바 드래그 =====
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ===== 8방향 리사이즈 (Form1.cs와 동일 패턴) =====
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
}
```

---

## ⚠️ 적용 시 주의사항

- **4개 파일 모두 전체 내용을 통째로 교체** (부분 수정 아님)
- `Form1.Designer.cs` 교체 후 Visual Studio가 자동으로 재파싱함 — Designer 뷰에서 이상하게 보여도 빌드 후 확인
- `AlarmForm.cs`의 `btnClose.Location`은 `_pnlTitle`이 Resize될 때 Anchor로 우측 고정됨 — 초기 `_pnlTitle.Width`가 0이면 위치가 이상할 수 있으니 빌드 후 확인
- `tbInlineEdit`은 `pnlTitleBar` 위에 겹쳐 표시되는 구조이므로, 다른 컨트롤과 z-order 충돌이 없는지 확인 (Controls.Add 순서가 중요)
- 빌드 오류 시 다음 세션 Claude에게 오류 메시지 전체를 붙여넣어 주세요

---

## 🔜 다음 단계 작업 (아직 시작 안 함 — 사용자 승인 필요)

위 4개 파일 적용 및 빌드 확인 후 진행하세요.

### A. 사이트 목록 → V버튼 드롭다운 전환 (난이도: 높음)
- `pnlSidebar`, `siteList`, `btnAddSite`, `btnRemoveSite` 제거
- 타이틀바 좌측에 ContextMenuStrip 기반 V버튼 추가
- 드롭다운 내용: `Config.Sites` 목록 + 구분선 + "주소 추가". 현재 사이트에 체크 표시. 항목 우클릭/× 아이콘으로 삭제.
- 기존 `SiteForm` 재사용

### B. 메뉴바(`pnlMenuBar`) 제거 → V버튼 드롭다운에 흡수 (난이도: 높음)
- `menuAddSite`는 A의 "주소 추가"와 통합
- `menuTopmost`, `menuHideBorder`는 V버튼 드롭다운에 체크 가능 항목으로 이전
- `pnlMenuBar`, `menuStrip` 자체를 Designer.cs에서 제거

### 작업 시 주의
- A+B를 합치면 `Form1.Designer.cs`와 `Form1.cs` 동시 대규모 수정 — **코드 작성 전 사용자에게 먼저 확인 요청**
- 작업 완료 후 `CHANGELOG.md`에 버전 항목 추가 권장
- V버튼 위치(lblTitle 우측 or timerButton 우측)는 사용자에게 배치 선호 먼저 확인

---

*작성 기준: 2026-06-18 / GitHub test 브랜치 직접 fetch 확인*
