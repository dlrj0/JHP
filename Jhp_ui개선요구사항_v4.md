# JHP UI 개선 요구사항 v4 (실제 코드 기준)

> 이 문서를 새 Claude 대화에 그대로 붙여넣으세요.
> ⚠️ 저장소의 `JHP_UI개선지시서_v2.md`, `Jhp_완전통합가이드_v3.md`는 **구버전 설계 문서이며 실제 코드와 다릅니다.**
> (예: v3 문서는 "AlarmForm이 ComboBox로 알람파일 선택"이라 적혀있지만 실제 코드는 아직 TextBox입니다.)
> 아래 내용은 GitHub `test` 브랜치의 실제 코드를 직접 읽고 정리한 **현재 상태**이며, 이 문서를 기준으로 작업해주세요.

---

## 0. 프로젝트 개요

- 이름: JHP / 종류: .NET WinForms + WebView2 내장 브라우저
- 목적: 메이플스토리 재획비 타이머 + OTT 시청 보조 도구 (원작자 인벤 게시글: https://www.inven.co.kr/board/maple/2304/34038 , https://www.inven.co.kr/board/maple/2304/34059)
- 원작자 의도(인벤 게시글 원문 확인): **"재획비타이머는 좌클릭 시 타이머가 시작, 우클릭 시 설정창을 켜고 끌 수 있음"** — 지금 요구사항 중 타이머 동작 방식은 이 원본 의도를 복원하는 것임
- 프로젝트 구성: `JHP`(UI), `JHP.Api`(로직), `JHP.Controls`(커스텀 컨트롤), `JHP.Asset`(JS)
- 모든 작업은 Visual Studio GUI로만 진행 (터미널 사용 안 함)

---

## 1. 현재 실제 구현 상태 (코드 직접 확인)

### 1-1. 타이틀바 (Form1.Designer.cs, 36px, `pnlTitleBar`)
한 줄에 다음이 가로로 나열되어 있음:
`lblTitle("JHP")` → `lblNextAlarm("다음 알람: --:--:--")` → `lblVolumeValue` + `sliderVolume`(90px) → `lblOpacityValue` + `sliderOpacity`(90px) → `btnAlarmSettings`("⏰" 텍스트 버튼) → `btnMinimize` → `btnMaximize` → `btnClose`

### 1-2. 메뉴바 (`pnlMenuBar`, 28px, 타이틀바 **아래 별도 줄**)
`MenuStrip`에 `menuAddSite("사이트 추가")`, `menuTopmost("항상 위")`, `menuHideBorder("테두리 숨김")` 3개 항목이 가로로 펼쳐져 있음 → **이게 사용자가 말한 "상단탭이 두 줄"의 원인**

### 1-3. 사이트 목록 — 현재 사이드바(Dock=Left, 180px)
`pnlSidebar` 안에 `SiteListViewControl`(커스텀 리스트, 클릭=선택/더블클릭=이동) + 하단에 `btnAddSite("추가")`/`btnRemoveSite("삭제")` 버튼. **이 사이드바 구조를 드롭다운 팝업 구조로 바꿔야 함** (4장 참고)

### 1-4. 타이머 동작 — 현재 코드 (`Form1.cs`)
- `lblNextAlarm`의 `MouseDown`은 **타이틀바 드래그(`TitleBar_MouseDown`)에만 연결**되어 있음. 좌클릭 시작/우클릭 메뉴 같은 전용 동작이 **전혀 없음**
- `timer`는 `Form1_Load`에서 무조건 `timer.Start()` 되어 앱 실행 즉시 카운트다운 시작됨 (멈춤/재시작 기능 없음)
- 알람 설정 진입은 `btnAlarmSettings`("⏰" 버튼) 클릭으로만 가능 → `OpenAlarmSettings()` → `AlarmForm` 모달

### 1-5. 슬라이더 (`NSlider.cs`)
- 마우스 드래그로만 값 변경 가능, **숫자 클릭 직접입력 기능 없음**
- 너비 90px로 고정 (Designer.cs에 하드코딩)

### 1-6. 창 컨트롤 버튼 (`ControlButton.cs`)
`OnPaint`에서 `Type`(Minimize/Maximize/Close)에 따라 **항상** 아이콘(─ / □ / ×)을 그리고, `hovered` 여부로 **배경색만** 바뀜. 코드만 보면 처음부터 아이콘이 보여야 하는데, 실제로는 호버해야 보인다는 증상이 있음 → `BackColor = Color.Transparent` 사용 + 부모 패널 더블버퍼링 미설정 조합에서 초기 렌더링이 누락되는 WinForms 투명 컨트롤 버그일 가능성이 높음. **원인 조사 + 수정 필요** (5-6 참고)

### 1-7. 팝업창 — `SiteForm.cs`, `AlarmForm.cs`
- 둘 다 `FormBorderStyle = FormBorderStyle.FixedDialog` → **리사이즈 자체가 불가능**한 상태 (모서리 드래그 기능 없음)
- `SiteForm`: 이름/URL 입력 + 추가/취소 버튼만 있는 단순 팝업 (사이트 1개 추가용, 현재도 이 형태로 존재함)
- `AlarmForm`: Width=420 고정. 알람 8개 체크박스(가로 4개씩, 각 88px) + 알람 파일명은 **TextBox**(ComboBox 아님, 직접 타이핑) + TTS 체크박스 + 볼륨/속도는 **TrackBar**(NSlider 아님) + 커스텀 알람 3슬롯. 폭이 좁아 체크박스 텍스트가 답답하게 보임
- `Form1`은 `WM_NCHITTEST` + `ReSize.GetMousePosition`으로 모서리 8방향 리사이즈가 **구현되어 있음**. 다만 `ReSize.SetThick()`(커서 모양 변경 메서드)는 정의는 되어 있지만 **Form1.cs 어디서도 호출되지 않음** (OS 기본 동작에 의존 중)

### 1-8. ToolStripCommand / Site / Config
- `ToolStripCommand` enum: `ADD_SITE`, `TOPMOST`, `TOGGLE_HIDE_WINDOW_BORDER` 3개 값만 존재
- `Site`(Name, Url), `Config.Sites`(List<Site>)는 그대로 사용 가능 — 데이터 구조 변경 불필요, **UI 표현 방식만 변경**

---

## 2. 요구사항 요약 (한 줄)

> "사이트 목록"과 "타이머"를 **같은 패턴(아이콘 버튼 → 클릭 시 팝업/목록)**으로 통일하고, 상단탭을 1줄로 압축하고, 팝업창들을 일반 윈도우처럼 리사이즈 가능하게 만들고, 슬라이더와 창 버튼의 디테일 버그를 고친다.

---

## 3. 참고 동작 패턴 — "타이머 버튼 우클릭 메뉴"

지금 앱에 이미 있는 이 패턴을 그대로 다른 곳에도 적용해달라는 것:
> 버튼을 누르면 그 자리에 작은 팝업/목록이 뜨고, 그 안의 항목을 누르면 추가 설정 다이얼로그가 뜨는 방식. Windows 환경변수 설정창(목록 + 추가/편집/삭제)과 비슷한 구조.

---

## 4. 사이트 목록 UI 변경

**현재**: 좌측 사이드바(180px 고정폭, `pnlSidebar`)로 항상 펼쳐져 있음.
**변경**: 사이드바를 없애고, 타이틀바 좌측에 **V자 모양 드롭다운 버튼**을 하나 둔다.

- V버튼 클릭 → 그 아래에 드롭다운 목록 표시 (지금 알람설정 버튼 우클릭 같은 팝업 방식. ContextMenuStrip 또는 커스텀 팝업 패널로 구현)
- 목록 내용: 등록된 사이트들 (`Config.Sites`) + 구분선 + **"주소 추가"** 항목
- 목록에서 사이트 항목 클릭 → 해당 URL로 `webView` 이동, 드롭다운 닫힘
- **"주소 추가"** 클릭 → 지금의 `SiteForm` 팝업(이름/URL 입력)이 그대로 뜨도록 연결 (이미 구현되어 있는 `SiteForm` 재사용, 별도 신규 폼 안 만들어도 됨)
- 목록 안에서 **삭제 기능**도 제공: 각 사이트 항목 옆에 작은 ×(삭제) 아이콘을 두거나, 항목 우클릭 시 "삭제" 메뉴 제공
- **현재 보고 있는 사이트** 옆에는 체크 표시(✓)를 표시해서 무엇이 선택된 상태인지 보여줄 것 (Windows 환경변수 편집창의 목록과 비슷한 느낌)
- 기존 `pnlSidebar`, `siteList`(SiteListViewControl), `btnAddSite`, `btnRemoveSite`는 제거하고 드롭다운 안으로 기능을 이전. `SiteListViewControl.cs`는 드롭다운 내부 목록 렌더링에 재활용 가능(체크 표시 추가 등 일부 수정 필요)

---

## 5. 상단탭(타이틀바) 재구성

### 5-1. 좌측 V버튼에 기능 통합
타이틀바 좌측에 V버튼(사이트) 하나만 두고, 그 드롭다운 안에 다음을 함께 넣어서 **메뉴바(`pnlMenuBar`) 한 줄을 통째로 제거**:
- 사이트 목록 + 주소 추가 (4장)
- "항상 위" 체크 항목
- "테두리 숨김" 체크 항목

→ 결과적으로 타이틀바가 1줄로 줄어듦. `menuStrip`, `menuAddSite`, `menuTopmost`, `menuHideBorder`, `pnlMenuBar` 통째로 V버튼 드롭다운 안으로 흡수.

### 5-2. 타이머 버튼 — 아이콘만, 클릭으로 시작/정지
**현재**: `lblNextAlarm`은 텍스트("다음 알람: --:--:--")만 있고 클릭 동작이 없음. 앱 실행하면 자동으로 카운트다운 시작.
**변경**:
- 텍스트 라벨 대신 타이머 아이콘 버튼 하나로 교체 (이미지/아이콘 디자인은 자유)
- **좌클릭** → 동작 토글: 정지 상태에서 누르면 "시작"(카운트다운 처음부터), 동작 중에 다시 누르면 "정지"(=초기화. 재시작 X, 멈추는 게 곧 초기화)
- **동작 중에는 버튼 색이 바뀌어서** 시각적으로 동작 중임을 표시
- **우클릭** → 항목이 "타이머 설정" 하나뿐이므로 메뉴를 띄우지 말고 **곧바로 `AlarmForm` 팝업**을 띄움 (`OpenAlarmSettings()` 그대로 호출)
- 기존 `btnAlarmSettings`("⏰" 텍스트 버튼)는 제거하고 이 타이머 아이콘 버튼의 우클릭 동작으로 대체

### 5-3. 볼륨 / 투명도 슬라이더
**현재**: `[볼륨 50][슬라이더 90px][투명도 100%][슬라이더 90px]` — 슬라이더가 짧고, 숫자는 텍스트일 뿐 클릭 불가.
**변경**: `볼륨 [0] ─────── 투명도[100%] ───────` 형태로
- 슬라이더(`NSlider`) 폭을 더 넓게 (예: 90px → 150~180px 이상)
- 숫자 라벨(`lblVolumeValue`, `lblOpacityValue`)을 클릭하면 그 자리에서 직접 숫자 입력이 가능하도록 (TextBox로 전환 또는 NumericUpDown 오버레이 등)
- `NSlider.cs` 자체에 폭 가변 대응 + 클릭-편집 모드 추가 필요

### 5-4. 최소화 / 최대화 / 닫기 버튼 초기 렌더링 버그
- 증상: 앱 실행 직후에는 아이콘이 안 보이고(투명하게 보임), 마우스가 그 위로 지나가야(호버) `─`/`□`/`×` 아이콘이 나타남
- 원인 추정: `ControlButton.cs`가 `BackColor = Color.Transparent`로 설정되어 있고 부모 패널(`pnlTitleBar`)이 더블버퍼링 안 된 상태에서, Form 최초 표시 시 OnPaint가 정상적으로 한 번에 호출되지 않는 WinForms의 투명 컨트롤 초기 페인트 문제로 보임
- 요청: 정확한 원인 조사 후 수정 (가능한 해법: 부모 패널에 더블버퍼링 활성화, Form Load 시점에 각 버튼에 `Invalidate()` 강제 호출, 또는 `Color.Transparent` 대신 부모와 동일한 실제 색을 칠해서 투명 렌더링 경로 자체를 피하는 방법 등 검토)

---

## 6. 팝업창(타이머 설정, 사이트 추가) 일반 창처럼 리사이즈 가능하게

**현재**: `AlarmForm`, `SiteForm` 모두 `FormBorderStyle.FixedDialog`로 리사이즈 자체가 막혀 있음.
**변경**:
- `AlarmForm`(타이머 설정 팝업)을 `FormBorderStyle.None` + `Form1`에 이미 구현된 것과 동일한 방식(`WM_NCHITTEST` 오버라이드 + `ReSize.GetMousePosition`)으로 모서리/테두리 드래그 리사이즈 가능하게 변경
- 창 모서리에 마우스가 가면 방향에 맞는 화살표 커서로 바뀌고, 드래그하면 창 크기가 늘어나거나 줄어들도록 (`ReSize.SetThick()`이 이미 정의되어 있으니 `OnMouseMove`에서 호출하도록 연결)
- 현재 `AlarmForm` 내부 컨트롤들이 고정 Left/Top/Width로 절대 좌표 배치되어 있어서, 창 크기를 바꿔도 내부 레이아웃이 따라 늘어나지 않음 → 리사이즈를 의미 있게 만들려면 내부 컨트롤도 Anchor 또는 동적 레이아웃으로 같이 손봐야 함
- `AlarmForm`의 텍스트가 찌그러져 보이는 것은 Width=420 고정 + 체크박스 88px 폭이 좁아서 생기는 문제 → 리사이즈 지원 + 기본 폭을 좀 더 넓게 잡으면 함께 해결됨
- `SiteForm`도 동일한 리사이즈 방식 적용 여부는 선택사항(입력 필드 2개뿐이라 필수는 아니지만, 일관성을 위해 같이 적용해도 됨) — 사용자 확인 필요

---

## 7. 명확화 — 하지 않을 것

- 사이트 목록을 사이드 탭(패널)으로 항상 펼쳐두는 구조는 **사용하지 않음**. 타이머 버튼 우클릭 메뉴처럼 V버튼을 눌렀을 때만 나타나는 드롭다운/팝업 구조로 바꾸는 것이 목적.
- 타이머는 "재시작" 개념이 아니라 좌클릭 한 번 = 시작, 다시 한 번 = 정지(=초기화)인 단순 토글.

---

## 8. 작업 시 참고할 실제 파일/필드명

```
Form1.cs / Form1.Designer.cs
  pnlTitleBar, lblTitle, lblNextAlarm, lblVolumeValue, lblOpacityValue,
  sliderVolume, sliderOpacity, btnAlarmSettings, btnMinimize, btnMaximize, btnClose
  pnlMenuBar, menuStrip, menuAddSite, menuTopmost, menuHideBorder   ← 제거 대상, V버튼 드롭다운으로 흡수
  pnlSidebar, siteList(SiteListViewControl), btnAddSite, btnRemoveSite ← 제거 대상, V버튼 드롭다운으로 흡수
  webView, timer, _remaining[8], _customRemaining[3]
  OpenAlarmSettings(), AddSite(), RemoveSelectedSite(), NavigateToSelectedSite()
  WndProc() — WM_NCHITTEST 8방향 리사이즈 (이미 구현됨, AlarmForm에도 동일 패턴 적용)

JHP.Controls/NSlider.cs        — 폭 가변 + 클릭 직접입력 추가 필요
JHP.Controls/ControlButton.cs  — 초기 렌더링(투명) 버그 조사
JHP.Controls/SiteListViewControl.cs — 드롭다운 내부 목록에 재활용, 체크표시(✓) 추가
JHP.Api/ReSize.cs              — GetMousePosition/SetThick, AlarmForm에도 적용
JHP/SiteForm.cs                — 그대로 재사용 (V버튼 드롭다운 "주소 추가"에서 호출)
JHP/AlarmForm.cs                — FixedDialog → None + 리사이즈, 내부 레이아웃 Anchor 보강
```

---

## 9. Claude에게 요청할 때 함께 보낼 것

- 이 문서 전체
- 실제로 수정할 파일들의 최신 GitHub raw 링크 (Form1.cs, Form1.Designer.cs, AlarmForm.cs, SiteForm.cs, NSlider.cs, ControlButton.cs, SiteListViewControl.cs, ReSize.cs, ToolStripCommand.cs)
- "한 번에 전체 코드 다시 짜지 말고, 이 문서의 섹션(4~6) 단위로 하나씩 진행해줘" 라고 명시하면 이전에 겪었던 "말 안 한 부분이 같이 망가지는" 문제를 줄일 수 있습니다.
