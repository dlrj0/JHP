# JHP — 완전 통합 가이드 v3

> **새 대화 시작 시 이 파일 하나만 붙여넣으면 됩니다.**
> .NET 10.0 WinForms + WebView2 / 모든 작업 Visual Studio GUI만 사용 (터미널/CLI 없음)

---

## 🗺️ 프로젝트 기본 정보

| 항목 | 값 |
|------|-----|
| GitHub | https://github.com/dlrj0/JHP (branch: test) |
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
- GitHub: https://github.com/dlrj0/JHP (branch: test)
- 모든 작업: Visual Studio GUI만 (터미널 사용 안 함)
```

---

## 📋 현재 파일 완료 상태 (20개 — GitHub test 브랜치 기준)

| 프로젝트 | 파일 | 상태 |
|---------|------|------|
| JHP.Api | `Config.cs`, `Site.cs`, `CustomAlarm.cs`, `Synth.cs`, `UpdateChecker.cs`, `Prompt.cs`, `ReSize.cs`, `ToolStripCommand.cs` | ✅ 완료 |
| JHP.Controls | `NSlider.cs`, `SiteListViewControl.cs`, `ControlButton.cs`, `CustomCheckBox.cs` | ✅ 완료 |
| JHP.Asset | `UserScripts.cs` | ✅ 완료 |
| JHP | `AlarmForm.cs`, `SiteForm.cs`, `Program.cs` | ✅ 완료 |
| JHP | **`Form1.cs`** | ✅ 완료 (버그 수정됨) |
| JHP | **`Form1.Designer.cs`** | ✅ 완료 (버그 수정됨) |
| 루트 | `README.md`, `CHANGELOG.md` | ✅ 완료 |

### ⏳ 남은 작업

없음. 모든 파일 작성 완료. 필요 시 README/CHANGELOG 내용 보강 가능.

---

## 🖥️ 실제 UI 레이아웃

```
┌──────────────────────────────────────────────────────────────────┐
│ JHP  ⏱클릭하여시작  볼륨 50[────●────]  투명도 100%[────●────]  [⏰][─][□][×] │
├──────────────────────────────────────────────────────────────────┤
│ [사이트 추가]  [항상 위]  [테두리 숨김 (포커스 아웃)]              │
├──────────────┬───────────────────────────────────────────────────┤
│ 사이트 목록  │                                                    │
│              │                                                    │
│ (사이드바    │              WebView2 브라우저                     │
│  Left 180px) │                                                    │
│              │                                                    │
│ [추가] [삭제]│                                                    │
└──────────────┴───────────────────────────────────────────────────┘
```

### 타이머 동작
- **타이틀바의 `⏱` 라벨 좌클릭** → 타이머 시작 / 재시작 (모든 카운트다운 리셋)
- **`⏱` 라벨 우클릭** → 컨텍스트 메뉴 팝업
  - `▶ 시작 / 재시작`
  - `⏹ 정지`
  - `⏰ 알람 설정 열기`
- 앱 실행 시 자동 시작 **없음** — 사용자가 클릭해야 시작

### AlarmForm (⏰ 버튼 → 팝업 다이얼로그)
- 고정 알람 8개 CheckBox
- 알람 파일 **ComboBox** (alarm/ 폴더 mp3 자동 목록)
- TTS 체크박스 + 볼륨/속도 TrackBar
- 커스텀 알람 3슬롯

---

## ✅ 적용된 버그 수정 내역

### ① Form1.Designer.cs — Controls.Add 순서 오류 [🔴 수정됨]

WinForms에서 `Dock=Top` 컨트롤은 역 z-order로 처리됩니다 (나중에 추가 = 최상단).

```csharp
// ❌ 이전 (잘못됨) — pnlMenuBar가 최상단 차지
Controls.Add(pnlTitleBar);   // 먼저 추가 → 낮은 z-order → 아래에 위치
Controls.Add(pnlMenuBar);    // 나중 추가 → 높은 z-order → 최상단 차지 ← 버그

// ✅ 수정됨 — pnlTitleBar가 최상단
Controls.Add(webView);        // Fill — 나머지 공간 채움
Controls.Add(pnlSidebar);     // Left
Controls.Add(pnlMenuBar);     // Top — pnlTitleBar 아래
Controls.Add(pnlTitleBar);    // Top — 마지막 추가 = 최상단
```

### ② Form1.cs — InjectJS() Uri 예외 [🔴 수정됨]

```csharp
// ❌ 이전 — about:blank 또는 null 이면 예외 발생
string host = new Uri(webView.CoreWebView2.Source).Host;

// ✅ 수정됨
string? source = webView.CoreWebView2.Source;
if (string.IsNullOrWhiteSpace(source)) return;
string host;
try { host = new Uri(source).Host; }
catch { return; }
```

### ③ Form1.cs — TopMost 자식창 버그 [🔴 수정됨]

```csharp
// ✅ OpenAlarmSettings() — TopMost 버그 수정
private void OpenAlarmSettings()
{
    bool was = TopMost;
    TopMost = false;           // ShowDialog 전에 false로
    using var form = new AlarmForm();
    bool ok = form.ShowDialog(this) == DialogResult.OK;
    TopMost = was;             // 닫힌 후 복원
    if (!ok) return;
    // ...
}

// ✅ AddSite() — 동일하게 적용
private void AddSite()
{
    bool was = TopMost;
    TopMost = false;
    using var form = new SiteForm();
    bool ok = form.ShowDialog(this) == DialogResult.OK && form.Result is not null;
    TopMost = was;
    if (!ok) return;
    // ...
}
```

### ④ 타이머 로직 변경 [🟡 기능 변경]

```csharp
// ❌ 이전 — 앱 시작 시 자동으로 카운트다운 시작, 리셋 방법 없음
timer.Start(); // Form1_Load에서 무조건 시작

// ✅ 수정됨 — 클릭으로 시작/재시작, _timerRunning 플래그로 제어
private bool _timerRunning = false;

// WinForms Timer는 항상 Running 상태 유지 (tick 오버헤드 최소)
// _timerRunning = false 이면 Timer_Tick에서 즉시 return

private void StartTimer()           // lblNextAlarm 좌클릭 or 메뉴
{
    for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
    for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
    _timerRunning = true;
    UpdateNextAlarmLabel();
}

private void StopTimer()            // 메뉴에서 정지
{
    _timerRunning = false;
    UpdateNextAlarmLabel();
}

private void Timer_Tick(...)
{
    if (!_timerRunning) return;     // 정지 상태면 아무 동작 없음
    // ...
}
```

### ⑤ OnMouseMove 리사이즈 커서 추가 [🟢 수정됨]

```csharp
// ✅ 추가됨 — 창 가장자리에서 리사이즈 커서(↔↕) 시각적 피드백
protected override void OnMouseMove(MouseEventArgs e)
{
    base.OnMouseMove(e);
    if (WindowState == FormWindowState.Normal)
        Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
}
```

### ⑥ AlarmForm 알람 파일 TextBox → ComboBox [🟢 수정됨]

```csharp
// ❌ 이전 — 파일명 직접 입력
private TextBox _tbAlarmName = null!;

// ✅ 수정됨 — alarm/ 폴더 mp3 목록 자동 드롭다운
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

---

## 📌 실제 API 시그니처 (GitHub 코드 기준)

```csharp
// ✅ Synth.cs (싱글톤)
Synth.Instance.Ring(string alarmName, int volume);
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();   // Stop + Dispose (NAudio WavePlayer 포함)

// ✅ ReSize.cs (static class — new 불가)
ReSize.GetMousePosition(Form form, Point cursor);   // → ReSize.MousePosition enum
ReSize.SetThick(ReSize.MousePosition pos);          // → Cursor

// ✅ UpdateChecker.cs
await UpdateChecker.Check(string currentVersion);
// → (bool HasUpdate, string LatestVersion, string Url)

// ✅ Config.cs (싱글톤)
Config.Instance.Volume      // int 0~100
Config.Instance.Rate        // int -10~10
Config.Instance.Tts         // bool
Config.Instance.AlarmName   // string (mp3 파일명)
Config.Instance.AlarmEnabled // bool[8]
Config.Instance.CustomAlarms // List<CustomAlarm> (3개)
Config.Instance.TopMost, .IsHideWindowBorderOnFocusOut, .Opacity
Config.Instance.Save();

// ✅ CustomAlarm.cs
alarm.Name    // string
alarm.Tick    // int (초)
alarm.Enabled // bool
```

---

## 📄 Form1.cs 전체 구조

```csharp
// 필드
private static readonly int[] AlarmSeconds = [7200, 3600, 1800, 1200, 900, 600, 100, 55];
private static readonly string[] AlarmLabels = [...];
private readonly Config _config = Config.Instance;
private readonly int[] _remaining = new int[8];
private readonly int[] _customRemaining = new int[3];
private bool _timerRunning = false;   // ④ 타이머 실행 플래그

// 메서드 목록
Form1_Load()             // InitControls → timer.Start → ResetTimerState → InitWV → CheckUpdateAsync
InitControls()           // 이벤트 바인딩, 슬라이더 초기화, 타이머 컨텍스트 메뉴
SetupTimerContextMenu()  // lblNextAlarm 우클릭 메뉴 (시작/재시작, 정지, 알람설정)
LblNextAlarm_MouseDown() // 좌클릭 → StartTimer()
StartTimer()             // _remaining 리셋 + _timerRunning = true
StopTimer()              // _timerRunning = false
ResetTimerState()        // 초기화 + 정지 상태
InitWV()                 // WebView2 초기화, NavigationCompleted → InjectJS
ResolveStartUrl()        // LatestUrl → DefaultSite → Sites[0] → naver.com
InjectJS()               // ② null/about:blank 체크, laftel/netflix 스크립트 주입
CheckUpdateAsync()       // Octokit 업데이트 확인
Timer_Tick()             // ④ _timerRunning 체크, 8+3 알람 카운트다운
FireAlarm()              // TTS or mp3
UpdateNextAlarmLabel()   // "⏱ 클릭하여 시작" / "⏱ (알람 비활성)" / "다음: hh:mm:ss"
OpenAlarmSettings()      // ③ TopMost 버그 수정, AlarmForm ShowDialog
MenuStrip_ItemClicked()  // ADD_SITE / TOPMOST / TOGGLE_HIDE_WINDOW_BORDER
AddSite()                // ③ TopMost 버그 수정, SiteForm ShowDialog
RemoveSelectedSite()
NavigateToSelectedSite()
TitleBar_MouseDown()     // ReleaseCapture + WM_NCLBUTTONDOWN
Form1_Activated/Deactivate() // 테두리 숨김/표시
Form1_FormClosing()      // timer.Stop, Synth.Stop, 설정 저장
WndProc()                // WM_NCHITTEST 리사이즈 8방향
OnMouseMove()            // ⑤ 리사이즈 커서 시각적 피드백
```

---

## 📄 Form1.Designer.cs 핵심 규칙

```csharp
// ① Controls.Add 순서 — Dock=Top은 역 z-order (나중에 추가 = 최상단)
Controls.Add(webView);        // Fill
Controls.Add(pnlSidebar);     // Left
Controls.Add(pnlMenuBar);     // Top (2번째)
Controls.Add(pnlTitleBar);    // Top (최상단) ← 반드시 마지막

// 타이머: components에 귀속시켜 Form Dispose 시 자동 해제
timer = new System.Windows.Forms.Timer(components) { Interval = 1000 };

// webView는 Form1.cs의 Dispose에서 명시적 해제
// protected override void Dispose(bool disposing) { webView?.Dispose(); ... }
```

---

## 📄 AlarmForm.cs 핵심 변경사항

```csharp
// ⑥ 알람 파일: TextBox → ComboBox
// 생성자 순서: InitializeComponent() → LoadAlarmFiles() → LoadFromConfig()
// LoadAlarmFiles(): AppContext.BaseDirectory/alarm/*.mp3 목록
// BtnOk_Click(): _cmbAlarmFile.SelectedItem으로 읽기 (파일 없음이면 기존값 유지)
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
| Windows 7 | ⚠️ | TTS 미작동 가능 — Synth.cs의 try-catch로 처리됨 |

---

## ⚠️ 알려진 버그 및 처리 현황

| 버그 | 처리 상태 |
|------|----------|
| `TopMost=true` 상태에서 자식 창 안 보임 | ✅ OpenAlarmSettings / AddSite에서 수정됨 |
| 창이 화면 밖으로 나가 복귀 불가 | ✅ Form1_Load의 IsOnScreen() 체크 |
| about:blank 에서 InjectJS Uri 예외 | ✅ null 체크 및 try-catch 추가됨 |
| Controls.Add 순서로 레이아웃 깨짐 | ✅ Form1.Designer.cs 순서 수정됨 |
| 리사이즈 커서 미변경 | ✅ OnMouseMove 오버라이드 추가됨 |
| AlarmForm 알람파일 직접 입력 | ✅ ComboBox로 변경, mp3 자동 목록 |
| 앱 시작 시 타이머 자동 시작 | ✅ 클릭으로 시작/재시작하도록 변경 |

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
├── alarm\         ← mp3 파일 여기에 복사 (AlarmForm에서 자동 목록 로드)
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
→ `alarm\` 폴더가 `JHP.exe`와 같은 폴더에 있는지 확인
→ AlarmForm에서 mp3 파일명이 드롭다운에 표시되는지 확인

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
