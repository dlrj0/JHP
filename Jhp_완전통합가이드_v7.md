# JHP — 완전 통합 가이드 v7

> **새 Claude 대화를 시작할 때 이 파일 하나만 붙여넣으세요.**
> `Jhp_ui개선진행상황_v5.md`, `Jhp_완전통합가이드_v6.md`는 이 문서로 통합·대체되었습니다 — 삭제하거나 무시하세요.
> 이 문서는 **2026-06-19, 사용자가 채팅에 직접 첨부한 파일의 실제 코드를 기준**으로 작성되었습니다.
> .NET 10.0 WinForms + WebView2 / 모든 작업 Visual Studio GUI만 사용 (터미널/CLI 없음)

---

## 📝 작업 원칙 (모든 세션 공통, 우선순위 높음)

1. **신뢰 기준은 "채팅에 직접 첨부된 파일"만.** GitHub raw URL fetch는 구버전이 캐시되어 보이는 경우가 있어 신뢰하지 않습니다 (v5에서 발견된 문제).
2. **첨부되지 않은 파일은 추측해서 작성 금지.** 필요하면 사용자에게 해당 파일을 요청하고, 받은 뒤에 코드 작성.
3. **이 문서엔 전체 코드를 통째로 넣지 않습니다.** 코드는 매 세션 채팅에서 직접 작성/전달하고, 이 문서엔 상태 표와 계획만 기록합니다. (v5에서 토큰 소진으로 코드 블록이 일부 전달되지 않았던 사고 재발 방지 — 문서가 비대해질수록 같은 문제가 재발할 위험이 커짐)
4. **필요한 부분만 수정**, 관련 없는 기존 기능은 그대로 유지.
5. **난이도 높은 작업(여러 파일 동시 수정 등)은 작성 전 "지금 진행해도 되는지" 먼저 확인.**
6. 이 어시스턴트는 .NET 빌드/실행 환경에 접근할 수 없습니다 → 모든 코드는 정적 분석만으로 작성됩니다. **빌드(Ctrl+Shift+B) 또는 실행 후 발견된 문제는 다음 세션에 그대로(에러 메시지 전문, 가능하면 스크린샷) 붙여넣어 주세요.**

---

## 🗺️ 프로젝트 기본 정보

| 항목 | 값 |
|------|-----|
| GitHub | https://github.com/dlrj0/JHP (branch: `test`) |
| 로컬 경로 | `C:\Users\wjgus\source\repos\dlrj0\JHP` |
| 솔루션 | `JHP.slnx` |
| 목적 | 메이플스토리 재획비 타이머 + OTT 시청 보조 도구 (배포 중단된 메이플인벤 사이트 제작자 프로그램 직접 재구현) |
| NuGet | Microsoft.Web.WebView2, NAudio, Octokit, System.Speech |

```
[Claude에게 붙여넣는 컨텍스트]
- 이름: JHP
- 종류: .NET 10.0 WinForms + WebView2 내장 브라우저
- GitHub: https://github.com/dlrj0/JHP (branch: test)
- 모든 작업: Visual Studio GUI만 (터미널 사용 안 함)
```

### 프로젝트 구조 (2026-06-19 사용자 확인 기준)

```
C:.
|   .gitignore
|   CHANGELOG.md
|   JHP.slnx
|   Jhp_완전통합가이드_v7.md   ← 이 문서 (v5/v6 대체)
|   README.md
|   tree.md
|
+---JHP
|   |   AlarmForm.cs
|   |   Form1.cs
|   |   Form1.Designer.cs
|   |   Form1.resx
|   |   JHP.csproj
|   |   Program.cs
|   |   SiteForm.cs
|   |
|   \---Resources
|           app.ico
|           app_icon.svg
|           app_icon_512.png
|
+---JHP.Api
|       Config.cs
|       CustomAlarm.cs
|       JHP.Api.csproj
|       Prompt.cs
|       ReSize.cs
|       Site.cs
|       Synth.cs
|       ToolStripCommand.cs
|       UpdateChecker.cs
|
+---JHP.Asset
|       JHP.Asset.csproj
|       UserScripts.cs
|
\---JHP.Controls
        ControlButton.cs
        CustomCheckBox.cs
        DarkMenuRenderer.cs
        JHP.Controls.csproj
        NSlider.cs
        SiteListViewControl.cs
        Timerbutton.cs
```

> ⚠️ 솔루션 이름과 UI 프로젝트 이름이 둘 다 "JHP"입니다. `...\JHP\JHP\JHP.csproj` 경로가 정상입니다.

---

## ✅ 이번 세션 확인 완료 — A/B 작업 (V버튼 드롭다운 전환)

이전 세션에서 계획했던 아래 작업이 **실제 코드에 반영되어 있음을 직접 확인**했습니다 (더 이상 "남은 작업" 아님):

- `pnlSidebar`(180px 사이드바), `siteList`, `btnAddSite`, `btnRemoveSite`, `pnlMenuBar`, `menuStrip` — `Form1.Designer.cs`에서 완전히 제거됨. `Controls.Add`는 `webView`, `pnlTitleBar` 둘뿐.
- 타이틀바 맨 왼쪽(x=3)에 `btnMenu`("▼") 추가, 클릭 시 `Form1.cs`의 `OpenSiteMenu()`가 `ContextMenuStrip`을 매번 새로 빌드:
  - `Config.Sites` 목록 → 클릭 시 `NavigateToSite()`, 현재 URL과 일치하면 `Checked = true`
  - 각 항목 우측에 ✕ 삭제 영역 (`SiteMenuItem.DeleteZoneWidth = 28`, `OnMouseUp`에서 가로채서 삭제 처리)
  - "＋ 사이트 추가" → 기존 `SiteForm` 재사용 (`AddSite()`)
  - 구분선 → "항상 위"(`CheckOnClick`, `_config.TopMost` 연동) → "테두리 숨김 (포커스 아웃)"(`_config.IsHideWindowBorderOnFocusOut` 연동)
  - 모든 항목 폭을 최장 텍스트 기준으로 통일 (`itemWidth = textWidth + 70`)
- `SiteMenuRenderer`(`DarkMenuRenderer` 상속, nested private class) — ✕ 아이콘만 추가로 그림, 다크 테마는 부모 클래스 그대로 사용.
- 나머지 타이틀바 구성(V버튼 +36px 시프트 반영): `lblTitle`(48) → `timerButton`(90) → `lblVolumeValue`(136)+`sliderVolume`(194) → `lblOpacityValue`(378)+`sliderOpacity`(466) → `btnMinimize/Maximize/Close`(Anchor Right).

**결론: A, B 작업 모두 완료.** 아래 "🐛 새로 발견된 문제"가 다음 작업의 중심입니다.

---

## 📋 파일별 현재 상태

| 파일 | 상태 | 비고 |
|------|------|------|
| `JHP.Controls/Timerbutton.cs` | ✅ 정상 (비교 기준) | `SupportsTransparentBackColor` 포함, 호버 정상 작동 보고됨 |
| `JHP.Controls/DarkMenuRenderer.cs` | ✅ 변경 없음 | |
| `JHP/Form1.Designer.cs` | ✅ A/B 반영됨 | ⚠️ 볼륨/투명도 라벨-슬라이더 간격 타이트 (아래 ②) |
| `JHP/Form1.cs` | ✅ A/B 반영됨 (V버튼 드롭다운, ToggleTimer, 인라인편집) | ⚠️ 리사이즈 구조적 문제 의심 (아래 ③) |
| `JHP/AlarmForm.cs` | ✅ FormBorderStyle.None, 자체 타이틀바, 8방향 리사이즈, Anchor 적용 | ⚠️ 라벨 폭 타이트 가능성 (아래 ②) |
| `JHP.Controls/ControlButton.cs` | 🟡 **호버 버그 의심** | 아래 ① 참고 — 1줄 수정 후보 |
| `JHP.Api/Config.cs`, `Site.cs`, `CustomAlarm.cs`, `Synth.cs`, `UpdateChecker.cs`, `Prompt.cs`, `ToolStripCommand.cs` | 미검토 (이번 세션 미첨부) | 이전 세션 보고 기준 "변경 없음" |
| `JHP.Api/ReSize.cs` | **미첨부 — 다음 세션에 필요** | ③ 진단에 필요 |
| `JHP.Controls/NSlider.cs` | 미검토 (이번 세션 미첨부) | |
| `JHP.Controls/SiteListViewControl.cs`, `CustomCheckBox.cs` | 미검토 | 아래 "정리 후보" 참고 |
| `JHP/SiteForm.cs`, `Program.cs` | 미검토 | |

---

## 🐛 새로 발견된 문제 3건 (다음 작업 후보, 사용자 확인 필요)

### ① 컨트롤 버튼 호버 효과 없음 (최소화/최대화/닫기, AlarmForm 닫기 포함)

**원인 추정 (확실도 높음):** `ControlButton.cs` 생성자에 `ControlStyles.SupportsTransparentBackColor` 플래그가 빠져 있습니다. 동일 패턴으로 만들어진 `Timerbutton.cs`(호버 정상 작동)에는 이 플래그가 있습니다.

```csharp
// ControlButton.cs — 현재
SetStyle(ControlStyles.AllPaintingInWmPaint |
         ControlStyles.UserPaint |
         ControlStyles.DoubleBuffer, true);   // ← SupportsTransparentBackColor 없음
BackColor = Color.Transparent;

// Timerbutton.cs — 호버 정상 작동, 비교 기준
SetStyle(ControlStyles.AllPaintingInWmPaint |
         ControlStyles.UserPaint |
         ControlStyles.DoubleBuffer |
         ControlStyles.SupportsTransparentBackColor, true);   // ← 이 줄 차이
BackColor = Color.Transparent;
```

`BackColor = Color.Transparent`를 쓰는 Button 계열 컨트롤은 이 플래그 없이는 투명 배경 처리가 제대로 안 돼, `OnPaint`의 호버 `FillRectangle`이 의도대로 보이지 않을 수 있습니다.

**제안:** `ControlButton.cs`의 `SetStyle` 호출에 `ControlStyles.SupportsTransparentBackColor` 한 줄만 추가 (다른 동작 변경 없음, 영향 범위: `btnMinimize`/`btnMaximize`/`btnClose`/`AlarmForm`의 닫기 버튼).

→ **진행 확인 필요.**

---

### ② 텍스트가 잘리거나 겹쳐 보임 (타이틀바, AlarmForm, V버튼 드롭다운)

**공통 원인:** 모든 좌표가 빌드 환경 없이 추정으로 작성되어, 실제 폰트 렌더링으로 검증된 적이 없습니다 (v5 문서에 이미 명시: "컴파일 테스트를 하지 못했습니다"). 디스플레이 배율이 100%가 아니라면(125%/150% 등) 증상이 더 두드러질 수 있습니다.

**가장 확실한 사례 — `Form1.Designer.cs` 타이틀바:**
- `lblVolumeValue`(x=136, AutoSize) ~ `sliderVolume`(x=194) 간격 = **58px**. 디자이너 placeholder 텍스트("볼륨 50")는 2자리 숫자 기준인데, 실제 런타임 값은 0~100까지 들어갑니다(`lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}"`). **볼륨을 100으로 올리면 슬라이더와 겹치는지 바로 확인 가능합니다 — 직접 테스트해 보시면 정확한 검증이 됩니다.**
- `lblOpacityValue`(x=378) ~ `sliderOpacity`(x=466) 간격 = 88px. "투명도 100%" 기준으로도 여유가 크지 않음.

**가능성 있는 사례 — `AlarmForm.cs`:**
- `AddLabel` 헬퍼가 만드는 `Label`은 `AutoSize=false`(기본값) + 고정 `Width` → 텍스트가 Width보다 길면 잘림(클리핑, 말줄임표 없음). 예: "알람 파일명" 라벨 `Width=90`은 한글 6글자 분량이라 폰트/DPI에 따라 잘릴 수 있음.

**가능성 낮음 — V버튼 드롭다운(`OpenSiteMenu`):**
- `itemWidth = textWidth + 70`으로 체크마진+✕삭제영역(28px)+패딩을 커버하도록 계산되어 있어 수치상으로는 대체로 충분해 보이지만, 사이트 이름이 매우 길 경우 등 엣지 케이스는 확인이 필요합니다.

**확인 부탁드릴 것:**
- 디스플레이 배율 설정 (100% / 125% / 150% 등)
- 가능하면 겹쳐 보이는 부분 스크린샷 — 가장 정확한 진단 방법입니다.

---

### ③ 메인 창(JHP) 리사이즈가 동작하지 않음

**원인 추정 (구조적 문제로 보임):** `Form1.Designer.cs`에서 `webView`(`Dock=Fill`)와 `pnlTitleBar`(`Dock=Top, Height=36`) 두 컨트롤이 합쳐서 클라이언트 영역을 **빈틈없이 100% 덮고 있습니다** (여백 0px).

`WM_NCHITTEST`는 커서 아래의 가장 안쪽 자식 컨트롤(이 경우 Panel 또는 WebView2)에 먼저 처리되는 메시지입니다. 자식 컨트롤이 가장자리까지 완전히 덮고 있으면 `Form1`의 `WndProc` 오버라이드 자체에 이 메시지가 도달하지 못합니다 — 현재 구조에서는 어떤 가장자리에서도 리사이즈 판정 로직이 실행될 기회가 사실상 없어 보입니다.

참고로 타이틀바 드래그(`TitleBar_MouseDown`)가 정상 작동하는 건 다른 메커니즘이기 때문입니다 — `MouseDown` 이벤트는 자식 컨트롤에서도 정상적으로 발생하므로, `pnlTitleBar.MouseDown`에서 `ReleaseCapture()` + `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)`을 직접 호출해 "타이틀바를 클릭한 것처럼" 강제 처리하는 방식입니다. 리사이즈도 같은 방식(자식 컨트롤 가장자리에 얇은 핸들 영역을 두고 `MouseDown`에서 동일하게 `SendMessage`)으로 우회 구현하면 동작할 가능성이 높아 보입니다.

`AlarmForm.cs`는 컨트롤들이 여백(`Left=12` 등)을 두고 배치되어 있어 폼 가장자리 일부가 비어 있을 수 있고, 메인 창보다는 리사이즈가 더 잘 동작할 가능성이 있습니다 (단, `_pnlTitle`이 덮는 상단 가장자리는 동일한 문제가 있을 수 있음 — 확인 필요).

**다음 세션에서 필요한 것:**
- `JHP.Api/ReSize.cs` 파일 (아직 미첨부) — `GetMousePosition`/`SetThick`의 실제 임계값(`RESIZE_THICK`) 로직 확인용
- 제안 방향(자식 컨트롤 가장자리에 얇은 리사이즈 핸들 추가 + `MouseDown`에서 `SendMessage` 방식)으로 진행해도 될지 확인

---

## 🔜 다음 작업 우선순위

| 순위 | 작업 | 난이도 | 필요한 것 |
|------|------|--------|----------|
| 1 | `ControlButton.cs` 호버 버그 수정 (① ) | 낮음 (1줄) | 진행 확인만 |
| 2 | 타이틀바/AlarmForm/드롭다운 레이아웃 여백 재계산 (②) | 중간 | 디스플레이 배율, 스크린샷 |
| 3 | 메인 창 리사이즈 재구현 (③) | 높음, 구조 변경 가능성 | `ReSize.cs`, 진행 확인 |

> 1~3 모두 착수 전 "지금 진행해도 되는지" 먼저 여쭤보겠습니다 (특히 3번은 `Form1.Designer.cs`/`Form1.cs` 동시 수정 가능성 있음).

---

## 🗑️ 정리 후보 (사용 여부 확인 필요)

A/B 작업으로 사이드바·메뉴바가 제거되면서 아래 파일들이 더 이상 참조되지 않을 가능성이 있습니다. Visual Studio에서 `Ctrl+Shift+F`(전체 검색)로 실제 참조 여부를 확인한 후 삭제 여부를 결정하세요 (제가 직접 본 적 없는 파일이라 추측만으로 삭제를 권하진 않습니다):

- `JHP.Controls/SiteListViewControl.cs` — 옛 사이드바(`siteList`)에서 쓰이던 컨트롤
- `JHP.Api/ToolStripCommand.cs` — 옛 `menuStrip`(`MenuStrip_ItemClicked`)에서 쓰이던 enum/패턴

---

## 🖥️ 현재 UI 레이아웃 (실제 코드 기준)

```
┌──────────────────────────────────────────────────────────────────┐
│ [▼] JHP  ⏱  볼륨 50[──────●──────]  투명도 100%[──────●──────]  [─][□][×] │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│                      WebView2 (전체 폭, 사이드바 없음)               │
│                                                                    │
└──────────────────────────────────────────────────────────────────┘
```

### 타이머 동작
- `timerButton` 좌클릭 → `ToggleTimer()` — 시작 시 모든 카운트다운 리셋, 정지 시 그대로 멈춤. 앱 실행 시 자동 시작 없음.
- `timerButton` 우클릭 → `OpenAlarmSettings()` 직결 (컨텍스트 메뉴 없음)
- `Active=true`(동작 중)일 때 아이콘 초록색
- 다음 알람 시각은 라벨이 아니라 `toolTip.SetToolTip(timerButton, ...)` 툴팁으로 표시

### 볼륨/투명도 인라인 직접입력
- `lblVolumeValue`/`lblOpacityValue` 숫자 클릭 → `tbInlineEdit`(숨김 TextBox)가 그 위치에 나타나 직접 입력 가능. Enter=적용, Esc=취소.

### AlarmForm (`timerButton` 우클릭 → 팝업)
- 자체 다크 타이틀바(`FormBorderStyle.None` + 드래그 이동 + `ControlButton` 닫기), 8방향 리사이즈(`WM_NCHITTEST`+`ReSize`)
- 폭 600px, 고정 알람 8개 CheckBox(컬럼 폭 135px), 알람 파일명은 TextBox(ComboBox 전환 보류), TTS+볼륨/속도 TrackBar, 커스텀 알람 3슬롯
- 창 크기 변경 시 입력칸/버튼이 `Anchor`로 따라 늘어남

---

## 📎 참고 — 실제 API 시그니처 (이전 세션 검증 기준, 이번 세션 재검증 안 함)

```csharp
// Synth.cs (싱글톤)
Synth.Instance.Ring(string alarmName, int volume);   // 내부적으로 Path.Combine("alarm", alarmName) 상대경로 사용
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();   // Stop + Dispose (NAudio WavePlayer 포함)

// ReSize.cs (static class — new 불가, RESIZE_THICK = 8로 보고됨, 다음 세션에서 재검증 필요)
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

## 📦 배포

```
VS 상단: Debug → Release
빌드 → 솔루션 빌드 (Ctrl+Shift+B)

솔루션 탐색기 → JHP 프로젝트 우클릭 → 게시
→ 폴더 → 경로: .../JHP/output
→ 구성: Release / 대상 런타임: win-x64 / 배포 모드: 자체 포함
→ 게시
```

```
output\
├── JHP.exe
├── alarm\         ← mp3 파일 여기에 복사 (Synth.Ring()이 상대경로 "alarm/{name}"로 읽음)
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

**WebView2 초기화 실패** → https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/ 런타임 설치

**mp3 알람 재생 안 됨** → `alarm\` 폴더가 `JHP.exe`와 같은 폴더(작업 디렉터리)에 있는지 확인

**GitHub 푸시 인증 오류** → 파일 → 계정 설정 → GitHub → 로그아웃 후 재로그인

**게시 후 exe 즉시 종료** → 게시 프로필에서 배포 모드 = **자체 포함** 확인

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

*작성 기준: 2026-06-19 / 사용자가 채팅에 직접 첨부한 8개 파일(ControlButton.cs, DarkMenuRenderer.cs, Timerbutton.cs, AlarmForm.cs, Form1.cs, Form1.Designer.cs, 및 v5/v6 구문서)을 직접 읽고 작성 — GitHub raw fetch 미사용*
