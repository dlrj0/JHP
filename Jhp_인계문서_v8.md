# JHP 인계 문서 v8 (Sonnet 5.5용)

> 새 대화 시작 시 이 파일 하나만 붙여넣으세요. v5·v6·v7 가이드와 `tree.md`, `메모.md`는 이 문서로 대체되어 삭제되었습니다 (git 기록에는 남아 있음).
> 기준: 2026-10-12, 작업 트리 기준 (git HEAD `13ba229` + 미커밋 변경 5개 파일).

---

## 0. 작업 원칙 (모든 세션 공통, 우선순위 높음)

1. **신뢰 기준은 사용자가 채팅에 첨부한 파일 또는 로컬 작업 트리 파일.** GitHub raw fetch는 구버전이 캐시되어 보일 수 있으므로 쓰지 않습니다.
2. **첨부/확인되지 않은 파일은 추측해서 수정 금지.** 필요한 파일이 있으면 먼저 요청합니다.
3. **이 문서에는 전체 코드를 넣지 않습니다.** 상태와 계획만 기록합니다.
4. **필요한 부분만 수정**, 관련 없는 기존 기능은 유지합니다.
5. **여러 파일을 동시에 크게 고치는 작업은 착수 전에 "지금 진행해도 되는지" 먼저 확인합니다.**
6. **[상시 규칙] 코드를 작성·수정한 같은 턴에 이 문서(상태 표·이슈·다음 작업)를 함께 갱신합니다.** 사용자가 매번 요청하지 않아도 됩니다.
7. **[상시 규칙] 파일이 없으면 코드를 쓰지 않고, 필요한 파일·정보를 먼저 요청합니다.**
8. 빌드 확인은 가능합니다: `dotnet build JHP/JHP.csproj` (이 환경에서 0 오류 확인됨). 실행 동작(호버, 리사이즈, 레이아웃)은 사용자가 VS에서 직접 확인해야 합니다.
9. 커밋·푸시는 사용자가 요청할 때만 합니다.

---

## 1. 프로젝트 기본 정보

| 항목 | 값 |
|------|-----|
| 목적 | 메이플스토리 재획비 타이머 + OTT/사이트 시청 보조 도구 |
| 종류 | .NET 10.0 (net10.0-windows) WinForms + WebView2 |
| 솔루션 | `JHP.slnx` (프로젝트 4개: `JHP`, `JHP.Api`, `JHP.Asset`, `JHP.Controls`) |
| 로컬 경로 | `C:\Users\wjgus\source\repos\dlrj0\JHP` |
| GitHub | https://github.com/dlrj0/JHP (branch: `test`, 기본 브랜치: `main`) |
| NuGet | Microsoft.Web.WebView2 1.0.4022.49, NAudio, Octokit, System.Speech |
| 현재 버전 상수 | `JHP/Form1.cs` `CurrentVersion = "v1.1.0"` (CHANGELOG 반영, 릴리스 미생성) |

### 프로젝트 구조 (정리 후)

```
JHP.slnx
Jhp_인계문서_v8.md   ← 이 문서
CHANGELOG.md / README.md / .gitignore
alarm/                 ← mp3 알람 파일 (비어 있음, 사용자가 채움)
JHP/                   ← 메인 WinForms (Form1, AlarmForm, SiteForm, Program, Resources)
JHP.Api/               ← Config, CustomAlarm, Site, Synth, ReSize, Prompt, UpdateChecker
JHP.Asset/             ← UserScripts (OTT 다음화 스킵 스크립트)
JHP.Controls/          ← ControlButton, Timerbutton, DarkMenuRenderer, NSlider, CustomCheckBox
```

> 솔루션 이름과 UI 프로젝트 이름이 둘 다 "JHP"입니다. 경로는 `JHP\JHP\JHP.csproj`입니다.

---

## 2. 원본 플레이어 (참고 기준)

이 프로그램은 메이플인벤 게시글로 배포되던 **d3vdev 님의 "재획플레이어"**를 재구현한 것입니다 (가이드 원 출처).

| 버전 | 날짜 | 출처 | 주요 내용 |
|------|------|------|----------|
| v1.0 | 2023-05-20 | 인벤 팁과노하우 게시글 34038 (작성자 d3vdev) | 커스텀 알람 3개, 사이트 삭제, TTS 속도 조절 추가 |
| v2 | 2023-05-22 | 인벤 게시글 34059 (글 기준 작성자 0x0b) | "테두리 가리기" 기능, "항상 위" 시 사이트 관리창이 안 보이던 문제 수정, 메뉴 구분선 클릭 에러 수정 |

**원본 기능 (현재 JHP가 계승해야 할 것):**
- 사이트 이동: 자주 쓰는 OTT/사이트 등록 후 바로 이동, 사이트 추가·삭제
- 재획비 타이머: 좌클릭 시작, 우클릭 설정창 열기/닫기
- TTS 알림: 버프 목록을 위에서부터 확인해 조건을 먼저 만족한 버프 하나만 음성 출력
- 항상 위, 테두리 숨김(포커스 아웃)
- 실행 시 WebView2 런타임 필요 (HRESULT 0x80070002 → WebView2 런타임 설치)
- 원본 안내: 실행 파일 보안 경고(SmartScreen)가 뜰 수 있음, Windows 7은 TTS 미지원

**확인 불가 사항:** 원본 GitHub `d3vdev/JHP` 페이지를 이번 세션에서 열었을 때 404가 반환되었습니다. 원본 소스는 현재 로컬 git 이력(첫 커밋 `c1f8b1d`, 2026-06-13)으로만 추적 가능합니다.

---

## 3. 현재 상태 (작업 트리 기준, 2026-10-12 확인)

### 3-1. 커밋되지 않은 변경 (5개 파일, 빌드 0 오류 확인)

| 항목 | 파일 | 내용 | 실행 검증 |
|------|------|------|----------|
| ① 컨트롤 버튼 호버 | `JHP.Controls/ControlButton.cs` | `SetStyle`에 `SupportsTransparentBackColor` 추가. `OnHandleCreated` 오버라이드는 HEAD에 이미 있음 | ❌ 실행 확인 결과 미보고 |
| ② 텍스트 겹침 | `JHP/Form1.cs` `LayoutTitleBar()` | 볼륨/투명도 라벨 폭을 `TextRenderer.MeasureText`로 "최대 글자수" 기준 측정, 남은 폭에 맞춰 슬라이더 폭 재계산(60~170px). `pnlTitleBar.Resize`와 라벨 `TextChanged`에서 호출 | ❌ 실행 확인 결과 미보고 |
| ② 텍스트 겹침 | `JHP/AlarmForm.cs` `AddLabel` | `AutoSize=true` + `MinimumSize=(width,0)`으로 글자 잘림 방지 | ❌ 실행 확인 결과 미보고 |
| ③ 메인 창 리사이즈 | `JHP/Form1.Designer.cs` | 가장자리 패널 3개(`pnlResizeLeft/Right/Bottom`, 두께 `ResizeEdge=5`) 추가. 컨트롤 추가 순서 `webView` → 좌 → 우 → 하 → `pnlTitleBar` | ✅ 사용자 확인 완료 (2026-10-12, "리사이즈 문제 없음") |
| ③ 메인 창 리사이즈 | `JHP/Form1.cs` | 타이틀바 상단/좌우 가장자리 판정(`TitleBarEdgeHit`), 하단 패널 모서리 판정(`BottomEdgeHit`), `BeginResize`(`WM_NCLBUTTONDOWN` + `HTxxx`), 커서 변경 `HitToCursor` | ✅ 사용자 확인 완료 (2026-10-12, "리사이즈 문제 없음") |

**③ 설계 메모:** 원인은 `webView`(Dock=Fill)가 클라이언트 영역을 덮어 `WM_NCHITTEST`가 폼에 도달하지 못하는 것입니다. 그래서 가장자리에 얇은 패널을 두고 `MouseDown`에서 직접 리사이즈를 시작합니다. 최대화 상태에서는 리사이즈하지 않습니다.
- ✅ (2026-10-12) `Form1.cs`의 기존 `WndProc`(`WM_NCHITTEST`)와 `OnMouseMove` 오버라이드는 자식 컨트롤이 영역을 전부 덮어 도달하지 않는 죽은 코드라 제거했습니다. `ReSize.cs`는 `AlarmForm`이 계속 사용합니다. 빌드 0 오류.
- 알려진 한계: 타이틀바 우측 5px는 `btnClose`가 덮고 있어 그 구간(y=3~33)에서는 우측 가장자리 리사이즈가 되지 않습니다(상단 3px 및 y≥36 구간은 가능). 불편하면 버튼 3개 위치를 왼쪽으로 5px 이동하세요.

### 3-2. 이미 HEAD에 반영되어 있는 작업 (완료)
- V버튼 드롭다운 전환 완료: `pnlSidebar`, `siteList`, `btnAddSite`, `btnRemoveSite`, `pnlMenuBar`, `menuStrip`이 `Form1.Designer.cs`에서 제거됨. `OpenSiteMenu()`가 `ContextMenuStrip`을 매번 새로 만듦.
- 타이머: 좌클릭 `ToggleTimer()`, 우클릭 `OpenAlarmSettings()`, 다음 알람 시각은 툴팁(`toolTip`)으로 표시, 앱 실행 시 자동 시작 없음.
- 볼륨/투명도 인라인 직접 입력(`tbInlineEdit`, Enter 적용, Esc 취소).
- `AlarmForm`: `FormBorderStyle.None`, 자체 다크 타이틀바, 8방향 리사이즈(`WM_NCHITTEST` + `ReSize`), `Anchor` 적용, 폭 600px.

### 3-3. 이번 세션에서 정리한 파일
- 삭제: `JHP (2)/`(2024년 빌드 복사본, untracked), `tree.md`, `Jhp_ui개선진행상황_v5.md`, `Jhp_완전통합가이드_v7.md`, `메모.md`, 스크린샷 PNG 2개, 미사용 코드 `JHP.Controls/SiteListViewControl.cs`, `JHP.Controls/CustomCheckBox.cs`, `JHP.Api/ToolStripCommand.cs`, 각 프로젝트의 `bin/`·`obj/` 빌드 산출물
- 삭제한 파일은 대부분 git에 남아 있어 `git show HEAD:<경로>`로 복원 가능합니다. `JHP (2)/`는 untracked라 복원할 수 없습니다.
- 유지: `.vs/`, `*.csproj.user`, `alarm/`, `JHP/Resources/`, `NSlider.cs`, `DarkMenuRenderer.cs`, `Timerbutton.cs`, `SiteForm.cs`, `Prompt.cs`, `UserScripts.cs`

---

## 4. 다음 작업 (우선순위 순)

| 순위 | 작업 | 필요한 것 | 난이도 |
|------|------|----------|--------|
| 1 | ①②③ 실행 검증 | 사용자가 VS에서 실행 후 결과 보고 | 사용자 작업 |
| 2 | ~~③ 중복 경로 정리~~ ✅ 완료 (죽은 `WndProc`/`OnMouseMove` 제거) | — | 완료 |
| 3 | ~~② 좁은 창 한계 처리~~ ✅ 계산상 문제 없음: `MinimumSize.Width=760`에서 남은 폭 약 314px → 슬라이더 약 157px(최소 60px 이상). 실제 폰트/배율은 1번에서 확인 | 1번 결과 | 완료(검증 대기) |
| 4 | 변경 5개 파일 커밋 | 사용자 승인 | 낮음 |
| 5 | `CHANGELOG.md`에 새 버전 항목 추가, `CurrentVersion` 갱신 | 버전 번호 결정 | 낮음 |
| 6 | 원본 대비 미구현 항목 검토 (아래 5장) | 사용자 우선순위 결정 | 사용자 확인 필요 |

### 실행 검증 체크리스트 (사용자용)
- [ ] 최소화/최대화/닫기 버튼에 마우스를 올리면 배경 hover 색이 보이는가 (메인, AlarmForm 닫기 포함)
- [ ] 볼륨을 100, 투명도를 100%로 설정해도 라벨과 슬라이더가 겹치지 않는가 (100%, 125%, 150% 배율)
- [ ] 창을 최대한 좁혔을 때 겹치는가 (겹치면 스크린샷 전달)
- [ ] 메인 창: 좌/우/상단/하단/네 모서리에서 리사이즈 커서가 바뀌고 크기가 조절되는가
- [ ] 최대화 상태에서 가장자리 리사이즈가 막히는가
- [ ] AlarmForm 리사이즈, 입력칸 Anchor 동작
- [ ] 오류가 나면 메시지 전문 또는 스크린샷을 다음 대화에 붙여넣기

---

## 5. 원본과 비교해 남아 있는 항목 (사용자 확인 필요)

- `AlarmForm`: 알람 파일명은 여전히 `TextBox`, 볼륨/속도는 여전히 `TrackBar` (ComboBox/NSlider 전환은 보류 상태)
- `SiteForm.cs`: 변경 없음 (이전 문서에서 "선택사항"으로 분류)
- 원본의 "테두리 가리기" 기능은 메뉴 항목 "테두리 숨김 (포커스 아웃)"으로 구현되어 있음 (`Config.IsHideWindowBorderOnFocusOut`)
- 원본 v2의 "항상 위 시 사이트 관리창 안 보임" 버그는 V버튼 드롭다운 구조로 바뀌어 해당 창 자체가 없음 → 재현 대상 아님

---

## 6. 참고 — 주요 API 시그니처

```csharp
// Synth (싱글톤) — 알람 음성/소리
Synth.Instance.Ring(string alarmName, int volume);     // "alarm/{name}" 상대경로 사용
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();

// ReSize (static) — AlarmForm/Form1의 WM_NCHITTEST 경로
ReSize.GetMousePosition(Form form, Point clientCursor); // → ReSize.MousePosition
ReSize.SetThick(ReSize.MousePosition pos);              // → Cursor

// UpdateChecker
await UpdateChecker.Check(string currentVersion);       // → (bool HasUpdate, string LatestVersion, string Url)

// Config (싱글톤, config.json 저장)
Config.Instance.Width / Height / X / Y / Opacity / Volume / Rate / Tts / AlarmName
Config.Instance.AlarmEnabled (bool[8]), CustomAlarms (List<CustomAlarm>, 3개)
Config.Instance.TopMost / IsHideWindowBorderOnFocusOut / IsMaximize
Config.Instance.Sites (List<Site>), DefaultSite, LatestUrl
Config.Instance.Save();

// Timerbutton (파일명 Timerbutton.cs)
timerButton.Active      // true면 초록색
timerButton.RightClick  // 우클릭 전용 이벤트
```

---

## 7. 배포 (Visual Studio 기준)

1. 구성: Release, 솔루션 빌드 (Ctrl+Shift+B)
2. 솔루션 탐색기 → JHP 우클릭 → 게시 → 폴더 `.../JHP/output`, 구성 Release, 대상 런타임 win-x64, 배포 모드 **자체 포함**
3. 결과 폴더: `JHP.exe`, `alarm\` (mp3를 복사), `cache\` (첫 실행 시 생성)
4. zip 압축 후 GitHub Releases 업로드

## 8. 자주 발생하는 오류
- `System.Speech`를 찾을 수 없음 → `JHP.Api.csproj`의 `TargetFramework`가 `net10.0-windows`인지 확인
- WebView2 초기화 실패 → WebView2 런타임 설치
- mp3 알람이 재생되지 않음 → `alarm\` 폴더가 `JHP.exe`와 같은 폴더에 있는지 확인
- 게시 후 exe가 바로 종료 → 게시 프로필의 배포 모드가 **자체 포함**인지 확인

---

## 9. 기능 점검 결과 (2026-10-12, 정적 코드 점검 — 실행 테스트 아님)

| 원본 기능 | 결과 | 비고 |
|-----------|------|------|
| 재획비 타이머 8슬롯, 좌클릭 시작/우클릭 설정 | ✅ 구현됨 | 시작 시 리셋, 정지 시 멈춤 |
| TTS "위쪽 하나만 출력" | 🔧 **수정함** | 기존엔 같은 순간에 여러 알람이 겹치면(예: 2시간 시점) 모두 울려 TTS는 마지막 항목("100초 알람")만 들렸음 → 목록 위쪽 하나만 울리도록 `Timer_Tick` 수정 |
| 커스텀 알람 3슬롯, TTS 속도 | ✅ 구현됨 | `AlarmForm`에서 저장 |
| 사이트 이동/추가/삭제 | ✅ 구현됨 | V버튼 드롭다운, ✕ 영역 클릭 삭제 |
| 항상 위 / 투명도 / 테두리 숨김 | ✅ 구현됨 | 포커스 아웃 시 타이틀바 숨김 |
| 자동 업데이트 확인 | 🔧 **수정함** | 문자열 비교(`"1.10.0" < "1.9.0"`) → `Version` 비교. Release가 없으면 조용히 무시됨 |
| 설정/알람 파일 경로 | 🔧 **수정함** | 작업 디렉터리 기준 → `AppContext.BaseDirectory` 기준 (바로가기 실행 시 config/alarm을 못 찾을 수 있었음) |
| OTT 다음화 스킵 (라프텔/넷플릭스) | ⚠️ **미해결 위험** | `UserScripts`가 페이지 로드 완료 시 **1회만** 버튼을 찾아 클릭함. 다음화 스킵 버튼은 재생 중에 나타나므로 동작하지 않을 가능성이 높음. 셀렉터(`data-testid="next-episode-button"`, `.watch-video--skip-content button`)도 실제 사이트에서 검증된 적 없음 → 사이트에서 실제 DOM 확인 후 주기적 검사(`setInterval`/`MutationObserver`)로 바꾸는 것을 권장 |
| 알람 mp3 | ✅ 코드상 구현됨 | `alarm/` 폴더에 mp3가 없어(현재 빈 폴더) 실제 소리는 확인 불가 |
