# JHP 인계 문서 v8 (Sonnet 5.5용)

> 새 대화 시작 시 `CLAUDE.md`를 먼저 읽고, 이 문서의 0장과 11장(현재 상태/남은 작업/변경 이력)을 읽으세요. v5·v6·v7 가이드와 `tree.md`, `메모.md`는 이 문서로 대체되어 삭제되었습니다 (git 기록에는 남아 있음).
> 3~5장은 작업 초기의 **이력**이며 현재 상태는 11장이 기준입니다. 최신 커밋은 `git log`로 확인하세요.

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
10. **[상시 규칙] 사용자의 컴퓨터 사용을 방해하지 않습니다.** 실제 마우스 커서 이동·클릭, 키 입력, 포커스 뺏기, 최대화로 사용자 화면을 덮는 동작을 하지 않습니다(`SetCursorPos`, `mouse_event`, `SetForegroundWindow`, `taskkill /IM JHP.exe` 금지). 사용자가 쓰는 JHP 인스턴스는 건드리지 않습니다.
11. **[상시 규칙] UI 검증은 `tools/ui-test.ps1`로 합니다.** 창 메시지(SendMessage)와 UI Automation으로 조작하고 PrintWindow로 캡처하며, 별도 폴더의 인스턴스를 실행하고 자기 PID만 종료합니다. 실행: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test.ps1 -OutDir <캡처폴더>` (빌드 생략 `-SkipBuild`).

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

## 3. 현재 상태 (작업 트리 기준, 2026-10-12 확인) — 작업 초기 기록
> 이 장과 4~5장은 초기 시점의 기록입니다. 아래 ①②③은 이후 모두 커밋·검증되었습니다. 현재 상태는 11장을 보세요.

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

---

## 10. 실제 실행 테스트 결과 (2026-10-12, 화면 배율 200% 환경에서 앱을 직접 실행·캡처)

테스트 방법: `dotnet build` 후 `JHP.exe`를 실행하고, PowerShell로 마우스 이동/클릭 + 화면 캡처 + UI Automation 컨트롤 좌표 덤프. (스크립트는 임시 폴더에 있었고 저장소에는 없음. 사용자가 동시에 마우스를 쓰면 결과가 흔들림.)

### 발견·수정한 문제 (이전 "정적 점검"에서 놓친 것들)
| 문제 | 원인 | 수정 |
|------|------|------|
| 최소화/최대화/닫기 버튼이 **화면에 안 보임**(호버 효과가 없어 보이던 진짜 원인) | `Anchor=Right`가 폭 200짜리 기본 패널 기준으로 계산되어 버튼이 창 밖(x≈2430)으로 밀림 | 앵커 제거, `LayoutTitleBar()`에서 오른쪽 끝부터 직접 배치 |
| 타이틀바가 작고 글자가 잘림, 슬라이더/라벨 어긋남 | 배율 200%에서 폰트는 커지는데 좌표/크기는 96dpi 값 그대로 | `AutoScaleDimensions=(96,96)` + `AutoScaleMode.Dpi`, 하드코딩 px를 `DeviceDpi/96f`로 환산(타이틀바, 메뉴, 리사이즈 두께, 커스텀 컨트롤 글리프) |
| 알람 설정창/사이트 추가창이 배율에서 깨짐 | `AutoScaleMode`를 컨트롤 추가 **전에** 지정 → 빈 폼에만 적용됨 | 자식 컨트롤을 모두 추가한 뒤(`ResumeLayout` 직전)에 지정 |
| 호버 영역/아이콘 배경이 검정 사각형으로 보임 | 더블버퍼 + 투명 브러시 | 부모 배경색으로 불투명하게 칠함 |
| V버튼 드롭다운 글자가 잘림 | `AutoSize=false` 항목의 높이를 지정하지 않음 | `menu.Font.Height + 배율 14px`로 높이 지정 |
| 저장된 창 크기가 배율이 바뀌면 어긋남 | 물리 px로 저장 | 96dpi 기준(논리 px)으로 저장/복원, 작업 영역으로 제한 |

### 원본 UI와의 차이를 메운 부분 (인벤 34059 글의 이미지 3장 확인)
- 사이트 메뉴 순서: 항상 위에 표시 / 다른 창 선택 시 테두리 안보이기 → 사이트 목록 → 사이트 추가
- 기본 사이트(첫 실행, 설정 파일 없을 때): 넷플릭스, 라프텔, 웨이브, 티빙, 쿠팡플레이, 디즈니플러스, 유튜브
- 알람 8개 항목명: 재획비(2시간), 경쿠(1시간), 경뿌/경쿠/유니온(30분), 유니온 버프(20분), 경쿠(15분), 유니온 버프(10분), 메소 회수(100초), 파운틴(55초) — TTS도 이 이름을 읽음
- 알람 소리: `alarm/*.mp3` 드롭다운 + ▷ 미리듣기, "TTS 출력 (선택 시 소리재생 X)" + "TTS 샘플" 버튼, TTS 속도
- 타이머 우클릭 = 설정창 열기/닫기 토글(비모달)

### 아직 확인되지 않은 것
- 실제 mp3/TTS 소리 재생(캡처로는 확인 불가), OTT 다음화 스킵(9장 참고)
- 125%/150% 배율(200%만 캡처 확인), 최대화 상태 레이아웃
- 사이트 추가 대화상자 화면(배율 수정 후 캡처 못 함)


---

## 11. 세션 인계 요약 (2026-10-12 늦은 시각, 토큰 한도 대비)

### 현재 상태 한눈에
- 브랜치 `test`. 최신 커밋은 `git log`로 확인(아래 "변경 이력"과 "추가 진행" 표가 항목별 기록).
- 원격 `origin/test`에 푸시됨(사용자가 "문제 없으면 푸시까지 해도 된다"고 허용: `test` 브랜치만, 빌드 0 오류 + UI 테스트 전체 통과일 때). `main` 병합/Release/태그 푸시는 사용자 요청 시에만.
- 빌드 0 오류. `tools/ui-test.ps1` 기본 옵션 **45/45 통과**(hover, menu/site는 SKIP: 실제 마우스/팝업이 필요).
- 아래 "이번 세션에서 한 일"·"알아 둘 함정"은 초기 세션 기록이고, 이후 진행은 11장 뒤쪽 표(추가 진행/회귀 점검/추가 진행 2)를 보세요.

### 이번 세션에서 한 일
1. 기능 점검 → 알람 겹침 시 하나만 울리게, 버전 비교, 설정/알람 파일 경로 수정.
2. 실제 실행 캡처로 진짜 원인 발견·수정: 화면 배율(200%) 미대응, 창 버튼이 창 밖으로 밀림(Anchor), 투명 브러시 검정 배경, 메뉴 항목 높이, AutoScale를 컨트롤 추가 전에 지정한 문제.
3. 인벤 34059 글 이미지 3장으로 원본 UI 확인 → 사이트 메뉴 순서/기본 사이트 7개/알람 항목명(재획비, 경쿠, 경뿌/경쿠/유니온, 유니온 버프, 메소 회수, 파운틴)/알람 소리 드롭다운+▷/TTS 샘플 반영.
4. 마우스 없이 테스트하도록 `tools/ui-test.ps1` 작성(규칙 10, 11).

### 알아 둘 함정 (다음 세션에서 같은 시간 낭비 방지)
- WinForms는 합성 `WM_MOUSEMOVE` 직후 2ms 안에 `WM_MOUSELEAVE`가 와서(실제 커서가 위에 없으므로) **호버 색은 마우스 없이 자동 검증이 안 됨**. 호버 코드는 `_hovered` 필드(MouseEnter/Move/Leave 이벤트)로 동작하며, 실제 마우스로는 과거에 빨강(196,43,28)/회색(60,60,60) 확인함. 최종 육안 확인은 사용자에게 요청.
- `AutoScaleMode.Dpi`는 **자식 컨트롤을 모두 추가한 뒤** 지정해야 함(앞에서 하면 빈 폼에만 적용). 하드코딩 px는 `DeviceDpi / 96f`로 환산.
- `Anchor=Right`는 부모 패널의 기본 폭(200) 기준으로 계산돼 어긋남 → 직접 배치(`LayoutTitleBar`).
- UIA `BoundingRectangle`이 무한대일 수 있음(`ToInt`로 변환). WebView2 내부 요소는 HWND가 없어 `NativeWindowHandle=0`으로 거름.
- 메뉴 드롭다운 검사 시 `ContextMenuStrip`이 창이 화면 밖(-20000)이어도 **사용자 화면 좌상단에 잠깐 표시**됨(이를 피하려면 메뉴 테스트를 빼야 함).
- 모달 대화상자를 여는 항목은 UIA `Invoke`가 막히므로 별도 runspace에서 실행(`Invoke-ElAsync`). 대화상자 열린 동안 `FromHandle`이 가끔 시간 초과됨.
- Git Bash의 `/tmp`와 Windows Python의 `/tmp`는 다른 경로. 임시 파일은 스크래치 폴더 사용. 한글이 들어간 파이썬은 heredoc 대신 파일로 저장해 실행.
- PowerShell 5.1: 단일 PSCustomObject의 `.Count`가 null일 수 있어 `@(...)`로 감싸야 함. 스크립트 파일은 UTF-8 **BOM** 필요.

### 남은 작업 (우선순위)
| 순위 | 작업 | 비고 |
|------|------|------|
| 1 | **OTT 다음화 스킵 개선** | 지금은 페이지 로드 시 1회만 클릭. 사용자가 라프텔/넷플릭스에서 "다음화" 버튼 요소(HTML)를 알려주면 `setInterval`/`MutationObserver`로 교체 (`JHP.Asset/UserScripts.cs`) |
| 2 | 사이트 추가 화면/최대화/125%·150% 육안 확인 | 사용자 확인 필요 |
| 3 | ~~원본의 "사이트 관리" 창 필요 여부~~ | 완료(대체): ▼ 메뉴 항목 우클릭으로 위/아래 이동, 이름/주소 수정, 시작 사이트 지정. 별도 관리 창은 만들지 않음 |
| 4 | 알람 소리/TTS 실제 재생 확인 | `alarm/`에 mp3를 넣고 사용자가 들어봐야 함 |
| 5 | v1.1.0 Release 생성 | `UpdateChecker`는 Release 기준. 사용자 요청 시에만 (`test` 푸시는 허용됨) |
| 6 | ~~알람 설정창 하단 여백, 커스텀 알람 기본값(원본은 1초) 다듬기~~ | 완료(하단 여백 `y+46`, `CustomAlarm.Tick` 기본 1). 하단 여백은 사용자 육안 확인 권장 |

### 파일 지도 (이번 세션 변경분)
- `JHP/Form1.cs`(레이아웃/메뉴/알람 토글/Timer_Tick), `JHP/Form1.Designer.cs`(AutoScale, 리사이즈 패널), `JHP/AlarmForm.cs`, `JHP/SiteForm.cs`
- `JHP.Controls/ControlButton.cs`, `Timerbutton.cs`, `NSlider.cs`(DPI, 호버), `JHP.Api/Config.cs`(기본 사이트, 경로), `Synth.cs`(경로), `UpdateChecker.cs`, `ReSize.cs`
- `tools/ui-test.ps1` (UI 자동 테스트), `CHANGELOG.md`(v1.1.0)

### 추가 진행 (2026-10-12, 사용자 지적 사항 처리)
| 발견 | 조치 |
|------|------|
| 타이틀바에 슬라이더/버튼이 많아 창을 잡을 빈 영역이 좁음 | 슬라이더 최대 폭 170→110(논리 px). 1000px 폭 기준 빈 영역 약 +120px. (더블클릭 최대화는 합성 입력으로 검증 불가해 보류) |
| 볼륨/투명도 숫자 입력이 다 입력해야 반영됨 | `TextChanged`로 입력 즉시 반영(`ApplyInlineEditLive`), 숫자만 입력, 범위 밖/미완성 값은 확정 시 보정. 투명도 최소 30 미만은 입력 중 무시(창이 갑자기 어두워지는 것 방지) |
| 프로그램 아이콘이 창/작업 표시줄에 없음 | `Program.AppIcon`(exe에서 `ExtractAssociatedIcon`)을 3개 폼의 `Icon`에 지정. `Resources\app.ico`는 원래 csproj에 있었음 |
| 슬라이더를 끌 때마다 config.json 쓰기 | 400ms 디바운스 저장(`ScheduleSave`), 종료 시 저장은 그대로 |
| 설정 파일 손상 시 다음 저장에서 사용자 설정이 사라짐 / 저장 중 종료 시 반쯤 써진 파일 | 임시 파일 교체 저장, 파싱 실패 시 `.corrupt` 사본 보존 |
- 검증: 빌드 0 오류, `tools/ui-test.ps1` 27/27 통과(입력 즉시 반영·아이콘은 자동 검증 항목 없음 → 사용자 육안 확인 권장).
- 아직 안 한 후보: 단축키(키 조합 사용자 결정 필요), 새 창(window.open) 처리 정책(OAuth 팝업 위험, 사용자 결정 필요), 설정 내보내기/가져오기, 알람 미리 알림. (단일 인스턴스 안내는 완료)

### 변경 이력 (되돌리기용)
기능이 사라지거나 동작이 달라졌을 때 아래 항목 단위로 되돌립니다. 커밋 해시는 `git log --oneline`에서 해당 요약으로 찾고, `git revert <해시>`(이력 보존) 또는 문제 파일만 `git checkout <이전해시> -- <파일>`로 복구합니다.
작업은 항목당 커밋 하나로 하고, 위험한 변경 전에는 `git tag jhp-checkpoint-<번호>`를 만듭니다.

| 구분 | 변경 | 주로 건드린 파일 | 되돌릴 때 주의 |
|------|------|------------------|----------------|
| 사용자 지적 3건 | 타이틀바 슬라이더 폭 축소, 입력 즉시 반영, 폼 아이콘 | `Form1.cs`, `Program.cs`, `AlarmForm.cs`, `SiteForm.cs` | 아이콘은 `Program.AppIcon`을 3개 폼이 참조 |
| 설정 안정화 | `Config.Normalize`, 임시 파일 교체 저장, `.corrupt` 보존 | `JHP.Api/Config.cs` | 되돌리면 손상된 config로 시작 시 예외 가능 |
| 사이트 주소 검증 | `Site.TryNormalizeUrl/SameUrl`, 추가창 검증 | `JHP.Api/Site.cs`, `SiteForm.cs`, `Form1.cs` | `Form`에서 `Site`가 `Component.Site`와 겹치므로 `JHP.Api.Site.`로 호출 |
| 오디오/TTS | 장치 없음 허용, 파일 없음 시 시스템 소리, 재생 후 해제 | `JHP.Api/Synth.cs` | |
| 앱 수명 | 단일 인스턴스(폴더별 Mutex), 전역 예외 로그 | `JHP/Program.cs` | 다른 폴더의 사본은 서로 막지 않음 |
| 알람 타이머 | 경과 시간 기준 계산(타이머 250ms), 바뀐 항목만 리셋 | `Form1.cs`, `Form1.Designer.cs` | `Timer.Interval`과 `Timer_Tick`의 `steps` 계산은 짝 |
| 창 상태 | 최대화 영역 제한, 복원 글리프, RestoreBounds 저장 | `Form1.cs`, `ControlButton.cs` | `ButtonType.Restore` 추가됨 |
| 메뉴/툴팁 | 체크 표시 다크화, ✕ 배율, 툴팁 | `DarkMenuRenderer.cs`, `Form1.cs` | |
| 테스트 | 캡처 전 1.5초 대기(검게 캡처되는 문제) | `tools/ui-test.ps1` | |
| 사이트 관리 | ▼ 메뉴 사이트 항목 우클릭 → 이동/수정/시작 사이트 지정 (`SiteListOps` 순수 함수, `SiteForm(Site?)` 오버로드, 비활성 항목 색) | `JHP.Api/Site.cs`, `JHP/Form1.cs`, `JHP/SiteForm.cs`, `JHP.Controls/DarkMenuRenderer.cs` | 시작 사이트는 `LatestUrl`이 없을 때만 쓰임(마지막 주소 우선 규칙 불변). 팝업이라 UI 자동 테스트 미검증 → 육안 확인 필요 |
| 알람 음소거 | `Config.MuteVideoOnAlarm`, ▼ 메뉴 체크 항목, `Synth.PlaybackFinished` 이벤트, `FireAlarm`에서 `CoreWebView2.IsMuted` 저장/설정, 종료·15초 안전 타이머로 복원 | `JHP.Api/Synth.cs`, `JHP.Api/Config.cs`, `Form1.cs` | Synth는 교체/Stop 시 이벤트를 내지 않음(취소된 TTS 무시). 콜백은 `BeginInvoke`로 UI 스레드에서 복원 |
| 테스트 확장 | 타이머 버튼 활성/비활성 색(PrintWindow 픽셀), 슬라이더 양 끝(투명도 30%/100%, 볼륨 0/100), 숫자 라벨 인라인 입력(WM_CHAR 즉시 반영·Esc 복원), 알람 설정창 확인/취소의 config.json 반영, 포커스 아웃 옵션(WM_ACTIVATE 합성). 각 검사는 `Run-Safe`로 UIA 오류 시 SKIP | `tools/ui-test.ps1` | 앱 코드는 바꾸지 않음. 통과 개수는 스크립트 출력으로 확인 |

현재 검증: 빌드 0 오류, `tools/ui-test.ps1` 전체 통과(메뉴 열림 검사가 드물게 타이밍 때문에 실패 → 재실행 시 통과, 앱 문제 아님).

### 화면 정리 규칙 (사용자 요청, 2026-10-12 / 같은 날 정정)
- **정정**: "작업 중 창이 절대 보이면 안 된다"가 아니라 **"작업/확인이 끝난 뒤에는 최소화하거나 화면에서 보이지 않게 처리한다"**가 사용자의 의도입니다(사용자 PC를 다른 사람이 볼 수 있음). 작업 중 창이 잠깐 뜨는 것은 괜찮습니다.
- `tools/ui-test.ps1`은 편의상 `JHP_OFFSCREEN=1`로 창을 화면 밖에 띄우고, ▼ 메뉴 팝업 검사는 `-IncludeMenu`일 때만 합니다(팝업은 화면 가장자리에 보임). 필요하면 `-IncludeMenu`나 화면 위 확인을 해도 되며, 끝나면 반드시 정리합니다.
- 예약 작업은 `conhost.exe --headless powershell ...`로 실행해 콘솔 창도 뜨지 않습니다.
- 화면에 띄워 확인한 뒤에는 최소화하거나 화면 밖으로 내리고, 내가 띄운 프로세스는 종료합니다. 사용자 화면을 덮는 최대화는 하지 않습니다.

### 회귀 점검 반영 (서브에이전트 리뷰, 체크포인트 1 이후)
| 지적 | 조치 |
|------|------|
| 알람 파일이 없을 때 볼륨 0이어도 시스템 소리가 남 | 볼륨 0이면 무음 |
| 이전 플레이어의 지연된 PlaybackStopped가 새 재생을 끊을 수 있음 | 현재 플레이어일 때만 정리 |
| 반복 예외 시 오류 대화상자가 쌓임 | 5초에 한 번만 표시(로그는 모두 기록) |
| 사이트 추가창 취소 시 폼 미해제 | `using`으로 항상 해제 |
| 절전 복귀 직후 알람이 한꺼번에/즉시 울림 | 경과가 120초를 넘으면 재동기화 |
| 같은 크기 최대화/모니터 이동 후 최대화에서 상태 표시·영역 불일치 | `Resize`/`LocationChanged`에서도 갱신 |
| 투명도 57% → 56% 절삭 | 반올림 |
| 설정창 확인 시 메인 슬라이더로 바꾼 볼륨이 되돌아감 | 설정창에서 바꾼 경우에만 반영 |
| 사이트 Name null / `SiteMenuItem.Site`가 `Component.Site`를 숨김(CS0114) | `Name ??= Url`, 속성명 `SiteInfo` |

### 추가 진행 2 (2026-10-12 낮)
| 구분 | 변경 | 파일 | 되돌릴 때 |
|------|------|------|-----------|
| 단일 인스턴스 안내 | 같은 폴더에서 JHP를 다시 실행하면 새로 띄우지 않고 기존 창을 복원·활성화 (`EventWaitHandle` `<mutex>_show`) | `JHP/Program.cs` | `git revert` 해당 커밋 (이전 동작: 조용히 종료) |

- 예약 작업 `JHP-Claude-Resume`을 오늘 15:29부터 1시간 간격(최대 19:00 무렵까지)으로 다시 설정함. 프롬프트의 19:00 종료 규칙은 그대로.
- [다크 입력 칸] 알람 설정창의 ComboBox/TextBox/NumericUpDown 배경·글자색을 다크로 (`AlarmForm.InputBack`, `JHP/AlarmForm.cs`). 되돌리기: `git revert` 해당 커밋 (이전: 흰 입력 칸)
- [시작 사이트 삭제 정리] ▼ 메뉴에서 시작 사이트를 ✕로 지우면 `DefaultSite`를 비움 (`Form1.cs` DeleteRequested). 되돌리기: `git revert` 해당 커밋
- [테스트] `tools/ui-test.ps1`에 같은 폴더 두 번째 실행 검사(7b) 추가 → 전체 45/45
- Form1.cs 전체 코드 리뷰 완료(2026-10-12): 위 항목 외 결함 없음
- [메뉴 검사 실행] 규칙 정정 후 -IncludeMenu로 전체 실행: **58/58 통과**(▼ 메뉴 항목 전부 + '알람 울릴 때 영상 음소거' 항목 존재, 사이트 추가창 열림). 사이트 추가창 내부 검사는 UIA 시간 초과로 SKIP(기존과 동일). 실행 후 남은 JHP 프로세스 없음.
- [설정 내보내기/가져오기] ▼ 메뉴 항목 2개 추가 (`Form1.cs` ExportSettings/ImportSettings, `Config.ExportTo/ReadFrom`). 되돌리기: `git revert` 해당 커밋. 파일 대화상자는 실제 입력이 필요해 자동 검증은 메뉴 항목 존재까지만 함 → 사용자 육안 확인 권장
- [리뷰 반영 2] 서브에이전트 리뷰(checkpoint-4 이후 diff) 5건 반영: 가져오기 시 알람창 닫기, `DefaultSite` 보정(Normalize), 시작 직후 두 번째 실행 신호 보존(`pendingShow`), 최소화 복원 시 직전 상태 사용(`BringToFrontFromAnotherInstance`), 메시지 상자를 `WithDialogState`로 감쌈. 되돌리기: `git revert` 해당 커밋
- [AlarmForm 직접 배치] `Anchor=Right/Bottom` 제거 → `LayoutRightAligned()`(Resize/Shown에서 호출). 되돌리기: `git revert` 해당 커밋 (`JHP/AlarmForm.cs`)
- [타이틀바 더블클릭 최대화] `pnlTitleBar`/`lblTitle` `MouseDoubleClick` → `btnMaximize.PerformClick()` (`JHP/Form1.cs`). 자동 검증 불가 → 사용자 육안 확인 필요. 되돌리기: `git revert` 해당 커밋
- [전역 단축키] `RegisterHotKey`(Ctrl+Alt+J), `Config.HotkeyEnabled`(기본 false), ▼ 메뉴 체크. `Form1.WndProc`의 WM_HOTKEY → `ToggleTimer`. 되돌리기: `git revert` 해당 커밋. 실제 키 입력은 사용자 PC를 건드리므로 자동 검증하지 않음 → 사용자 확인 필요(조합 변경 희망 시 `VK_J`)

### 게임 안전 원칙 (사용자 요구, 2026-10-12)
- 목적: 메이플 사냥 중 같이 쓰는 프로그램이므로 **게임 제재 사유가 되면 안 됨**. 현재 코드에는 `SendInput`/후킹/메모리 접근/프로세스·창 열거/화면 캡처/주입이 **없음**(2026-10-12 grep 확인). 외부 통신은 WebView2(사용자가 연 사이트)와 업데이트 확인(GitHub)뿐.
- 전역 단축키(Ctrl+Alt+J)는 `RegisterHotKey`만 사용(키 후킹 아님). 기본 꺼짐, 켠 사람만 동작. 이 조합은 시스템 전체에서 다른 프로그램(게임 포함)에 전달되지 않으므로, 게임에서 쓰는 키와 겹치면 안 됨. 조합을 자유 입력으로 바꾸게 하지 않고 고정 조합 또는 소수의 프리셋만 허용할 것(기능 요청이 오면 이 원칙을 먼저 설명).
- 새 기능을 넣을 때마다 위 목록(게임 프로세스를 읽거나 건드리는 기능)에 해당하지 않는지 확인합니다. 제재 여부는 넥슨의 정책에 달려 있어 코드로 보장할 수는 없으며, 보수적으로 "게임을 전혀 건드리지 않는다"를 유지합니다.
- [새 창 정책] `NewWindowRequested`: 사용자 조작 + 크기/위치 미지정 + http(s)면 같은 창에서 Navigate. OAuth 등 팝업은 건드리지 않음. 실제 사이트에서의 동작은 자동 검증 불가 → 사용자 확인 필요(문제 시 `Form1.InitWV`의 핸들러만 제거). 되돌리기: `git revert` 해당 커밋
