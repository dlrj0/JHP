# JHP — 완전 통합 가이드 v6

> **새 Claude 대화를 시작할 때 이 파일 하나만 붙여넣으면 됩니다.**
> 이 문서는 `v5`에서 4개 파일(ControlButton.cs / Form1.Designer.cs / Form1.cs / AlarmForm.cs)을
> **2026-06-18에 GitHub raw URL을 직접 fetch해서 재검증하고 코드를 작성한 후** 상태를 반영한 최신 버전입니다.
> .NET 10.0 WinForms + WebView2 / 모든 작업 Visual Studio GUI만 사용 (터미널/CLI 없음)

---

## ✅ 직접 검증 결과 (2026-06-18)

이 세션에서 raw.githubusercontent.com에서 직접 fetch해서 확인 후 **4개 파일 모두 코드 작성 완료**했습니다.

| 파일 | 이전 GitHub `test` 브랜치 상태 | 이 세션 처리 결과 |
|------|------|------|
| `JHP.Controls/Timerbutton.cs` | ✅ 이미 새 코드 반영됨 | 변경 없음 |
| `JHP.Controls/ControlButton.cs` | ❌ 구버전 (`OnHandleCreated` 없음) | ✅ **코드 작성 완료 — GitHub 적용 필요** |
| `JHP/Form1.Designer.cs` | ❌ 구버전 (`lblNextAlarm`/`btnAlarmSettings`, 슬라이더 90px, `tbInlineEdit`/`toolTip` 없음, Controls.Add 순서 오류) | ✅ **코드 작성 완료 — GitHub 적용 필요** |
| `JHP/Form1.cs` | ❌ 구버전 (`timerButton`/`ToggleTimer` 없음, 앱 시작 시 타이머 자동 시작, `InjectJS` null 체크 없음) | ✅ **코드 작성 완료 — GitHub 적용 필요** |
| `JHP/AlarmForm.cs` | ❌ 구버전 (`FormBorderStyle.FixedDialog`, 폭 420px, 체크박스 컬럼 94px, 자체 타이틀바 없음) | ✅ **코드 작성 완료 — GitHub 적용 필요** |

> 📌 **다음 세션 시작 시 안내:** 4개 파일을 Visual Studio에서 직접 열어 내용을 교체하고 커밋/푸시해야 합니다.
> 작업 전 raw URL을 다시 fetch해서 실제로 반영됐는지 먼저 확인만 하세요. "작성했다"≠"적용됐다"입니다. fetch해도 구버전이 출력될 수 있으니 미구현된 것 같다면 파일을 직접 요구하면 됩니다. =fetch결과와 깃허브 실제파일 버전이 다를 수 있음.

---

## 📝 작업 원칙 (모든 세션 공통)

> 코드 수정 시 필요한 부분만 수정하고, 수정이 필요 없는 기존 기능은 그대로 유지해야 합니다.
> (A 기능을 수정하려다 B 기능이 사라지는 일이 없어야 함.)

---

## 🗺️ 프로젝트 기본 정보

| 항목 | 값 |
|------|-----|
| GitHub | https://github.com/dlrj0/JHP (branch: `test`) |
| 로컬 경로 | `C:\Users\wjgus\source\repos\dlrj0\JHP` |
| 솔루션 | `JHP.slnx` |
| 프로젝트 4개 | `JHP`(UI), `JHP.Api`(로직), `JHP.Controls`(커스텀 UI), `JHP.Asset`(JS) |

> ⚠️ 솔루션 이름과 UI 프로젝트 이름이 둘 다 "JHP"입니다. `...\JHP\JHP\JHP.csproj` 경로가 정상입니다.

```
[Claude에게 붙여넣는 컨텍스트]
- 이름: JHP
- 종류: .NET 10.0 WinForms + WebView2 내장 브라우저
- 목적: 메이플스토리 재획비 타이머 + OTT 시청 보조 도구
- NuGet: Microsoft.Web.WebView2, NAudio, Octokit, System.Speech
- GitHub: https://github.com/dlrj0/JHP (branch: test)
- 모든 작업: Visual Studio GUI만 (터미널 사용 안 함)
```

### 프로젝트 구조

```
JHP/ (솔루션 루트)
├── JHP/                 # 메인 WinForms 앱
│   ├── Form1.cs
│   ├── Form1.Designer.cs
│   ├── Form1.resx
│   ├── AlarmForm.cs
│   ├── SiteForm.cs
│   └── Program.cs
├── JHP.Api/             # 공용 유틸
│   ├── Config.cs
│   ├── Site.cs
│   ├── CustomAlarm.cs
│   ├── ReSize.cs        ← 리사이즈 헬퍼 (static class)
│   ├── Synth.cs
│   ├── ToolStripCommand.cs
│   ├── Prompt.cs
│   └── UpdateChecker.cs
├── JHP.Controls/        # 커스텀 컨트롤
│   ├── ControlButton.cs
│   ├── Timerbutton.cs   ← 파일명 소문자 b 주의
│   ├── NSlider.cs
│   ├── SiteListViewControl.cs
│   ├── CustomCheckBox.cs
│   └── DarkMenuRenderer.cs
└── JHP.Asset/
    └── UserScripts.cs
```

---

## 📋 파일별 상태 (직접 검증, 2026-06-18 기준)

| 프로젝트 | 파일 | 상태 |
|---------|------|------|
| JHP.Api | `Config.cs`, `Site.cs`, `CustomAlarm.cs`, `Synth.cs`, `UpdateChecker.cs`, `Prompt.cs`, `ReSize.cs`, `ToolStripCommand.cs` | ✅ 완료, 변경 없음 |
| JHP.Controls | `NSlider.cs`, `SiteListViewControl.cs`, `CustomCheckBox.cs`, `DarkMenuRenderer.cs` | ✅ 완료, 변경 없음 |
| JHP.Controls | `Timerbutton.cs` | ✅ 새 코드 반영됨 |
| JHP.Asset | `UserScripts.cs` | ✅ 완료, 변경 없음 |
| JHP | `SiteForm.cs`, `Program.cs` | ✅ 완료, 변경 없음 |
| JHP.Controls | **`ControlButton.cs`** | 🟡 **코드 작성 완료 — GitHub 커밋/푸시 필요** |
| JHP | **`Form1.Designer.cs`** | 🟡 **코드 작성 완료 — GitHub 커밋/푸시 필요** |
| JHP | **`Form1.cs`** | 🟡 **코드 작성 완료 — GitHub 커밋/푸시 필요** |
| JHP | **`AlarmForm.cs`** | 🟡 **코드 작성 완료 — GitHub 커밋/푸시 필요** |

---

## 🖥️ 목표 UI 레이아웃 (4개 파일 적용 시)

```
┌──────────────────────────────────────────────────────────────────┐
│ JHP  ⏱  볼륨 50[──────●──────]  투명도 100%[──────●──────]  [─][□][×] │
├──────────────────────────────────────────────────────────────────┤
│ [사이트 추가]  [항상 위]  [테두리 숨김 (포커스 아웃)]              │
├──────────────┬───────────────────────────────────────────────────┤
│ 사이트 목록  │                                                    │
│ (Left 180px) │              WebView2 브라우저                     │
│ [추가] [삭제]│                                                    │
└──────────────┴───────────────────────────────────────────────────┘
```

### 타이머 동작 (`timerButton` 도입 후)
- **타이틀바 시계 아이콘(`timerButton`) 좌클릭** → `ToggleTimer()` — 시작 시 모든 카운트다운 리셋, 정지 시 그대로 멈춤
- **`timerButton` 우클릭** → `OpenAlarmSettings()` 바로 호출 (컨텍스트 메뉴 없이 직결)
- `Active` 상태일 때 아이콘이 초록색으로 표시됨 (`Timerbutton.cs`)
- 다음 알람 시각은 라벨이 아니라 **`toolTip.SetToolTip(timerButton, ...)`** 툴팁으로 표시
- 앱 실행 시 자동 시작 **없음**

### 볼륨/투명도 인라인 직접입력
- `lblVolumeValue`/`lblOpacityValue` 숫자 클릭 → `tbInlineEdit`(숨김 TextBox)가 그 위치에 나타나 직접 숫자 입력 가능
- Enter = 적용, Esc = 취소

### AlarmForm (`timerButton` 우클릭 → 팝업 다이얼로그)
- 자체 다크 타이틀바 (`FormBorderStyle.None` + 드래그 이동 + `ControlButton` 닫기)
- 8방향 리사이즈 지원 (`WM_NCHITTEST` + `ReSize`)
- 폭 600px, 고정 알람 8개 CheckBox (컬럼 폭 135px)
- 알람 파일 **TextBox** (v3 가이드의 ComboBox 전환은 보류 — 아래 "선택사항 C" 참고)
- TTS 체크박스 + 볼륨/속도 TrackBar, 커스텀 알람 3슬롯
- 창 크기 변경 시 입력칸/버튼이 `Anchor`로 따라 늘어남

---

## ✅ 적용해야 할 4개 파일 — 전체 코드 (통째로 교체)

> 아래 코드는 v4가 작성한 베이스에 **v3의 버그 수정 3건(TopMost 자식창 버그, InjectJS null 체크, OnMouseMove 리사이즈 커서)과 Controls.Add 순서 수정**을 실제로 병합한 최종본입니다. v6 문서는 이 수정들이 "포함되었다"고만 설명했을 뿐 전체 코드를 남기지 않았어서, 이번에 직접 병합해서 작성했습니다.

### 1. `JHP.Controls/ControlButton.cs`

`OnHandleCreated` 오버라이드 추가 — 초기 렌더링(투명 배경) 버그 수정, `Timerbutton.cs`와 동일 패턴.

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

    // 초기 렌더링 버그 수정: 핸들 생성 직후 강제로 한 번 더 그려줌 (Timerbutton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
```

---

### 2. `JHP/Form1.Designer.cs`

**변경 내용:**
- `lblNextAlarm`/`btnAlarmSettings` 제거 → `TimerButton timerButton`으로 교체
- `sliderVolume`/`sliderOpacity` 90px → 170px 확장 + 좌표 재배치
- 인라인 숫자 편집용 `tbInlineEdit`(TextBox, 기본 숨김), `toolTip` 컴포넌트 추가
- **`Controls.Add` 순서 수정** (v4가 작성한 코드는 이 부분이 여전히 버그 있는 순서였음 — 아래는 수정된 순서)

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

            // ⚠️ Dock=Top 컨트롤은 역 z-order (나중에 추가 = 최상단).
            // webView(Fill) → pnlSidebar(Left) → pnlMenuBar(Top, 2번째) → pnlTitleBar(Top, 최상단) 순서로
            // 반드시 마지막에 pnlTitleBar를 추가해야 메뉴바가 타이틀바를 가리지 않음.
            Controls.Add(webView);
            Controls.Add(pnlSidebar);
            Controls.Add(pnlMenuBar);
            Controls.Add(pnlTitleBar);

            ResumeLayout(false);
        }
    }
}
```

---

### 3. `JHP/Form1.cs`

**변경 내용 (v4 베이스 + v3 버그 수정 병합):**
- `_timerRunning` 필드 + `ToggleTimer()` — 좌클릭으로 시작/정지, 시작 시 항상 리셋
- `timerButton.RightClick` → `OpenAlarmSettings()` 직접 호출 (컨텍스트 메뉴 제거)
- `Timer_Tick`에 `if (!_timerRunning) return;` 가드 — 앱 시작 시 자동 시작 없음
- `UpdateNextAlarmLabel()` → `toolTip.SetToolTip(timerButton, ...)`으로 변경
- `BeginInlineEdit`/`CommitInlineEdit`/`InlineEdit_KeyDown` — 볼륨/투명도 숫자 직접입력
- **`OpenAlarmSettings()`/`AddSite()` — `TopMost` 자식창 버그 수정** (병합)
- **`InjectJS()` — null/`about:blank` 체크 + try/catch 추가** (병합)
- **`OnMouseMove()` — 리사이즈 커서(↔↕) 시각 피드백 추가** (병합)

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

    // 🟢 추가: 창 가장자리에서 리사이즈 커서(↔↕) 시각적 피드백
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (WindowState == FormWindowState.Normal)
            Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
```

---

### 4. `JHP/AlarmForm.cs`

**변경 내용:**
- `FormBorderStyle.FixedDialog` → `FormBorderStyle.None` + 자체 타이틀바(`_pnlTitle`, 드래그 이동 + `ControlButton` 닫기)
- `WM_NCHITTEST` 8방향 리사이즈
- 폭 420 → 600, 체크박스 컬럼 폭 94px → 135px
- 주요 컨트롤에 `Anchor` 설정 (창 늘리면 입력칸/버튼이 따라 늘어남)

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

- **4개 파일 모두 전체 내용을 통째로 교체** (부분 수정 아님), 교체 후 **반드시 커밋/푸시까지 완료**
- `Form1.Designer.cs` 좌표값은 1000px 기준 계산. `sliderOpacity` 끝(x≈600)과 `btnMinimize`(anchor right, 1000px 기준 x=862) 사이 여유는 약 260px. 최소 폭(760px)으로 줄여도 겹치지 않을 것으로 예상되나 빌드 후 실제 확인 필요
- `AlarmForm.cs`의 `btnClose.Location`은 `new Point(_pnlTitle.Width - 46, 1)`로 계산되는데, 이 시점에 `_pnlTitle`이 아직 Form에 추가되지 않아 `Width`가 기본값(0 또는 200)일 수 있음 → `Anchor.Right` 덕분에 런타임에 보정되긴 하지만, 빌드 후 닫기 버튼이 우측 끝에 정확히 붙는지 확인 권장
- `tbInlineEdit`은 `pnlTitleBar` 위에 겹쳐 표시되는 구조이므로 Controls.Add 순서가 중요 (위 코드에서는 이미 올바른 순서로 추가됨)
- `AlarmForm.cs`의 `using JHP.Controls;` — `Form1.Designer.cs`에서도 이미 `JHP.Controls`를 참조하므로 프로젝트 참조 설정은 문제 없음 (확인됨)
- `ReSize.GetMousePosition(Form form, Point cursor)`의 `cursor`는 **client 좌표**여야 함 — 위 `AlarmForm.cs`/`Form1.cs`의 `WndProc`에서는 `PointToClient(new Point(x, y))`로 이미 올바르게 변환됨 (GitHub `JHP.Api/ReSize.cs`와 직접 대조해서 검증함)
- 빌드 오류 시 다음 세션 Claude에게 오류 메시지 전체를 붙여넣어 주세요

---

## 📌 실제 API 시그니처 (GitHub 코드 직접 fetch로 검증함 — 변경 없음)

```csharp
// Synth.cs (싱글톤)
Synth.Instance.Ring(string alarmName, int volume);   // 내부적으로 Path.Combine("alarm", alarmName) 상대경로 사용
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();   // Stop + Dispose (NAudio WavePlayer 포함)

// ReSize.cs (static class — new 불가, RESIZE_THICK = 8)
ReSize.GetMousePosition(Form form, Point cursor);   // cursor는 client 좌표 → ReSize.MousePosition enum
ReSize.SetThick(ReSize.MousePosition pos);          // → Cursor

// UpdateChecker.cs
await UpdateChecker.Check(string currentVersion);
// → (bool HasUpdate, string LatestVersion, string Url)

// Config.cs (싱글톤, config.json에 저장)
Config.Instance.Width / Height / X / Y / Opacity   // int, int, int, int, double
Config.Instance.Volume      // int 0~100
Config.Instance.Rate        // int -10~10
Config.Instance.Tts         // bool
Config.Instance.AlarmName   // string (mp3 파일명, 기본값 "경험치업.mp3")
Config.Instance.AlarmEnabled // bool[8]
Config.Instance.CustomAlarms // List<CustomAlarm> (3개)
Config.Instance.TopMost / IsHideWindowBorderOnFocusOut / IsMaximize
Config.Instance.Sites        // List<Site>
Config.Instance.DefaultSite  // string
Config.Instance.LatestUrl    // string
Config.Instance.Save();
Config.Instance.Replace(Config other);

// Site.cs
site.Name  // string
site.Url   // string

// CustomAlarm.cs
alarm.Name    // string
alarm.Tick    // int (초)
alarm.Enabled // bool

// TimerButton.cs (파일명 Timerbutton.cs, 소문자 b)
timerButton.Active     // bool — true면 초록색 아이콘
timerButton.Click      // 좌클릭 (Button 기본 이벤트)
timerButton.RightClick // 우클릭 전용 이벤트 (MouseEventHandler)
```

---

## ⚡ 최적화 사항

| 항목 | 적용 내용 |
|------|----------|
| WebView2 캐시 | `AppContext.BaseDirectory/cache` 디스크 경로 지정 (메모리 캐시 누적 방지) |
| NAudio | `Synth.Stop()`에서 `Dispose()` — 매 Ring 호출마다 리소스 해제 |
| SpeechSynthesizer | 싱글톤 유지, `SpeakAsyncCancelAll()` 후 재사용 |
| 타이머 간격 | 1000ms — `_timerRunning` 플래그로 동작 제어 (오버헤드 최소) |
| 경과 시간 추적 | 카운트다운 방식: `_remaining[i]--` (tick 1회 = 1초 감소) |
| Form 종료 시 | `Synth.Stop()` → `_config.Save()` → `webView.Dispose()` (Dispose에서) |

---

## 🖥️ Windows 호환성

| OS | 상태 | 비고 |
|----|------|------|
| Windows 10 | ✅ | WebView2 런타임 별도 설치 필요 |
| Windows 11 | ✅ | SmartScreen 경고: "추가 정보 → 실행" |
| Windows 7 | ⚠️ | TTS 미작동 가능 — `Synth.cs`의 try-catch로 처리됨 |

---

## ⚠️ 알려진 버그 및 처리 현황

| 버그 | 처리 상태 |
|------|----------|
| `TopMost=true` 상태에서 자식 창 안 보임 | 🟡 수정 코드 작성 완료 (Form1.cs) — **GitHub 적용 전** |
| 창이 화면 밖으로 나가 복귀 불가 | ✅ `Form1_Load`의 `IsOnScreen()` 체크, 이미 GitHub 반영됨 |
| `about:blank`에서 `InjectJS` Uri 예외 | 🟡 수정 코드 작성 완료 (Form1.cs) — **GitHub 적용 전** |
| `Controls.Add` 순서로 레이아웃 깨짐 (메뉴바가 타이틀바 가림) | 🟡 수정 코드 작성 완료 (Form1.Designer.cs) — **GitHub 적용 전** |
| 리사이즈 커서 미변경 | 🟡 `OnMouseMove` 오버라이드 코드 작성 완료 (Form1.cs) — **GitHub 적용 전** |
| 초기 렌더링 시 버튼 아이콘 안 보임(호버 전까지) | 🟡 `OnHandleCreated` 코드 작성 완료 (ControlButton.cs) — **GitHub 적용 전** (Timerbutton.cs는 이미 적용됨) |
| 앱 시작 시 타이머 자동 시작 | 🟡 `ToggleTimer()` 코드 작성 완료 (Form1.cs) — **GitHub 적용 전** |
| AlarmForm 작은 폭(420px)·고정 다이얼로그 | 🟡 600px·리사이즈 가능 코드 작성 완료 (AlarmForm.cs) — **GitHub 적용 전** |

---

## 🔜 남은 작업 — 4개 파일 적용 + 빌드 확인 후 진행 (사용자 승인 필요)

**이 작업들은 코드 작성 전에 반드시 사용자에게 "지금 진행해도 되는지" 먼저 확인하세요.**

### A. 사이트 목록 → V버튼 드롭다운 전환
- `pnlSidebar`(180px 고정), `siteList`, `btnAddSite`, `btnRemoveSite` 제거
- 타이틀바 좌측에 V자 드롭다운 버튼 추가 (`ContextMenuStrip` 권장 — 커스텀 팝업보다 리스크 낮음)
- 드롭다운 내용: `Config.Sites` 목록 + 구분선 + "주소 추가" 항목. 현재 사이트 체크 표시. × 아이콘으로 삭제
- 기존 `SiteForm` 재사용

### B. 메뉴바(`pnlMenuBar`) 제거 → V버튼 드롭다운에 흡수
- `menuAddSite`는 A의 "주소 추가"와 통합
- `menuTopmost`, `menuHideBorder`는 V버튼 드롭다운 내 체크 항목으로 이전
- `pnlMenuBar`, `menuStrip` Designer.cs에서 제거
- `MenuStrip_ItemClicked` 패턴 → `ContextMenuStrip ItemClicked`로 재사용 가능

### C. (선택사항) AlarmForm 알람 파일명 TextBox → ComboBox
- alarm/ 폴더 mp3 자동 목록 드롭다운으로 전환
- 현재는 TextBox 유지. 필요 시 별도 세션에서 처리

```csharp
// 참고: ComboBox 전환 시 패턴
private ComboBox _cmbAlarmFile = null!;

private void LoadAlarmFiles()
{
    _cmbAlarmFile.Items.Clear();
    string dir = Path.Combine(AppContext.BaseDirectory, "alarm");
    if (Directory.Exists(dir))
        foreach (var f in Directory.GetFiles(dir, "*.mp3"))
            _cmbAlarmFile.Items.Add(Path.GetFileName(f));
    if (_cmbAlarmFile.Items.Count == 0)
        _cmbAlarmFile.Items.Add("(파일 없음)");
}
```

### 작업 시 주의
- A+B를 합치면 `Form1.Designer.cs`와 `Form1.cs`를 동시에 크게 수정
- V버튼 위치: `lblTitle` 우측(`timerButton` 앞) 또는 `lblTitle` 자체를 V버튼으로 대체 — 사용자에게 배치 선호 먼저 물어볼 것
- 완료 후 `CHANGELOG.md` 버전 항목 추가 권장 (현재 GitHub `CHANGELOG.md`는 `v1.0.0` 초기 릴리즈 항목만 있음)

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

### 배포 후 폴더 구조

```
output\
├── JHP.exe
├── alarm\         ← mp3 파일 여기에 복사 (Synth.Ring()이 상대경로 "alarm/{name}"로 읾)
└── cache\         ← 첫 실행 시 자동 생성 (WebView2)
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
→ `alarm\` 폴더가 `JHP.exe`와 같은 폴더(작업 디렉터리)에 있는지 확인 — `Synth.Ring()`은 상대경로를 사용함

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

---

*작성 기준: 2026-06-18 / GitHub `test` 브랜치 raw URL 직접 fetch로 4개 파일 + Config.cs/ReSize.cs/Synth.cs/Site.cs/CustomAlarm.cs/CHANGELOG.md 재검증 후 코드 작성 완료 — Visual Studio에서 교체 및 커밋/푸시 대기 중*
