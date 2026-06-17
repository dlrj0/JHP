# JHP — 시작부터 배포까지 완전 통합 가이드 v2 (Visual Studio GUI 전용)

> **이 파일 하나로 Visual Studio 설치 → 환경 설정 → 개발 → 배포까지 전 과정을 진행할 수 있습니다.**
> Claude와 새 대화를 시작할 때 이 파일만 붙여넣으면 전체 맥락이 유지됩니다.
>
> **.NET 10.0 LTS 기준 (지원 종료: 2030년 11월)**
>
> **이 버전은 터미널(명령 프롬프트, PowerShell, dotnet CLI)을 전혀 사용하지 않습니다.**
> 솔루션/프로젝트 생성, 참조 설정, NuGet 설치, 빌드, 배포까지 전부 Visual Studio의
> **솔루션 탐색기 우클릭 메뉴 + 메뉴/대화상자 GUI**로만 진행합니다.

---

## 📌 원본 파일 재첨부가 필요 없는 이유

| 파일 | 분석 상태 | 이 가이드에 포함된 정보 |
|------|-----------|----------------------|
| `JHP.dll` | ✅ 역어셈블 분석 완료 | 전체 클래스 구조, 메서드 목록, 동작 흐름 |
| `config.json` | ✅ 구조 파악 완료 | 모든 필드 및 기본값 |
| `JHP.deps.json` | ✅ 의존성 파악 완료 | NuGet 패키지 목록 |

---

## 🗺️ 이 프로젝트의 위치 / 이름 정보 (중요)

| 항목 | 값 |
|------|-----|
| GitHub 저장소 이름 | **JHP** |
| 로컬 솔루션 폴더 | `C:\Users\wjgus\source\repos\dlrj0\JHP` |
| 솔루션 파일 | `JHP.slnx` |
| 프로젝트 4개 | `JHP`(UI), `JHP.Api`(로직), `JHP.Controls`(커스텀 UI), `JHP.Asset`(JS) |

> ⚠️ **이름 혼동 주의:** 솔루션 이름과 UI 프로젝트 이름이 둘 다 "JHP"입니다.
> `...\JHP\JHP\JHP.csproj` 처럼 JHP가 두 번 겹치는 경로가 생기는 것은 정상입니다.

---

## 📊 시작 전 확인사항 — 용량 · 기간 예상

### 디스크 용량

| 항목 | 필요 용량 | 비고 |
|------|----------|------|
| Visual Studio 2022 Community | 6 ~ 9GB | `.NET 데스크톱 개발` 워크로드 포함 |
| .NET 10.0 SDK | 포함 | VS 설치 시 자동 포함 |
| 프로젝트 소스 + 빌드 캐시 | 약 1 ~ 2GB | bin/obj/cache 합산 |
| WebView2 런타임 | 약 100MB | Windows 11은 대부분 이미 설치됨 |
| **합계** | **약 8 ~ 12GB** | |

### 배포 파일 크기

| 배포 방식 | 배포 폴더 크기 | zip 압축 후 | 조건 |
|-----------|--------------|------------|------|
| 프레임워크 의존 | 약 5MB | 약 3MB | 사용자 PC에 .NET 10 Runtime 설치 필요 |
| **자체 포함 (권장)** | **약 130 ~ 160MB** | **약 50 ~ 80MB** | .NET Runtime 포함, 별도 설치 불필요 |

---

## 🗂️ 프로젝트 개요 (새 대화 시 복사해서 붙여넣기)

```
[프로젝트 개요]
- 이름: JHP
- 종류: .NET 10.0 WinForms + WebView2 내장 브라우저
- 목적: 메이플스토리 재획비 타이머 + OTT 시청 보조 도구
- 솔루션: JHP.slnx (로컬 경로: C:\Users\wjgus\source\repos\dlrj0\JHP)
- 프로젝트 4개: JHP(UI), JHP.Api(로직), JHP.Controls(커스텀 UI), JHP.Asset(JS)
- NuGet 패키지: Microsoft.Web.WebView2, NAudio, Octokit, System.Speech
- 모든 작업은 Visual Studio GUI(솔루션 탐색기 우클릭)로 진행, 터미널/CLI 사용 안 함
- 현재 [X단계] 진행 중, [파일명] 작성 요청
```

---

## ⚠️ 메이플스토리 이용 규정 준수 기준

### ✅ 사용 가능한 기능

이 프로그램은 **게임 클라이언트와 직접 상호작용하지 않는 별도 도구**입니다.

| 기능 | 이유 |
|------|------|
| 재획비 타이머 / 알람 | 단순 시간 측정, 게임 개입 없음 |
| 내장 브라우저 (WebView2) | 별도 Edge 창, 게임 메모리 미접근 |
| mp3 알람음 재생 | 시스템 오디오, 게임과 무관 |
| TTS 음성 안내 | System.Speech 사용, 게임 개입 없음 |
| 항상 위 / 투명도 조절 | 일반 윈도우 속성, 게임 미개입 |
| 사이트 즐겨찾기 | 단순 URL 저장 |
| OTT JS 인젝션 (Laftel, Netflix 스킵) | 브라우저 내부에만 적용, 게임과 무관 |
| 자동 업데이트 확인 (GitHub API) | 알림만, 자동 실행 없음 |

### ❌ 절대 추가하면 안 되는 기능

| 기능 | 위험 이유 |
|------|-----------|
| 게임 화면 오버레이 / 픽셀 감지 | 화면 캡처 = 비인가 프로그램으로 탐지 가능 |
| 게임 클라이언트 메모리 읽기/쓰기 | 명백한 핵 행위 |
| 마우스/키보드 자동 입력 | 매크로 = 이용 정지 |
| 게임 프로세스 탐지 / 연동 | 비인가 접근 |

---

## 🔍 원본 프로그램 분석 결과

### 모듈 구성

**JHP (폼 레이어)**
- `Form1` — 메인 윈도우. WebView2 포함, 타이머, 메뉴바, 리사이즈 처리 전부
- `AlarmForm` — 알람 설정 팝업 (현재 GitHub 코드에 구현됨 — TrackBar 사용 중)
- `SiteForm` — 사이트 추가 팝업
- `Program` — 진입점

**JHP.Api (비즈니스 로직)**
- `Config` — 싱글톤. config.json 읽기/쓰기 (Load, Save, Replace)
- `Site` — `{Name, Url}` 단순 모델
- `CustomAlarm` — `{Name, Tick, Enabled}` 모델
- `Synth` — 싱글톤. NAudio로 mp3 재생 `Ring(string alarmName, int volume)`, TTS `TTS(string text, int volume, int rate)`
- `UpdateChecker` — Octokit(GitHub API)로 최신 버전 확인
- `Prompt` — 다이얼로그 (ShowDialog 2종)
- `ReSize` — **static class**. 마우스 드래그 리사이즈 (`GetMousePosition`, `SetThick`)
- `ToolStripCommand` — 메뉴 명령 enum

**JHP.Controls (커스텀 UI)**
- `NSlider` — 커스텀 슬라이더 (볼륨/투명도용)
- `SiteListViewControl` — 사이트 목록 컨트롤
- `ControlButton` — 최소화/최대화/닫기 버튼
- `CustomCheckBox` — 커스텀 체크박스

**JHP.Asset**
- `UserScripts` — JS 코드 상수 2개: LaftelSkipNext, NetflixSkipNext

### ⚠️ 현재 GitHub 코드와 가이드의 불일치 사항 (중요)

| 파일 | 불일치 내용 |
|------|------------|
| `Synth.cs` | `Ring(string alarmName, int volume)` — volume 파라미터가 있음. Form1에서 `Synth.Instance.Ring(cfg.AlarmName, cfg.Volume)` 형태로 호출해야 함 |
| `Synth.cs` | `TTS(string text, int volume, int rate)` — volume, rate 파라미터가 있음. Form1에서 `Synth.Instance.TTS(text, cfg.Volume, cfg.Rate)` 형태로 호출 |
| `ReSize.cs` | **static class**로 구현됨. `new ReSize()` 인스턴스 생성 불가. `ReSize.GetMousePosition(form, cursor)` 와 `ReSize.SetThick(pos)` 로 호출 |
| `AlarmForm.cs` | TrackBar 사용 중 (NSlider 아님). UI 개선 지시서에 따라 Form1 우측 패널로 통합 시 NSlider로 전환 필요 |

### Config 구조 (config.json 전체 필드)

```json
{
  "width": 800,
  "height": 600,
  "x": 100,
  "y": 100,
  "opacity": 1.0,
  "volume": 50,
  "rate": 0,
  "isMaximize": false,
  "topMost": false,
  "isHideWindowBorderOnFocusOut": false,
  "sites": [{"Name": "...", "Url": "..."}],
  "defaultSite": "",
  "alarmEnabled": [false, false, false, false, false, false, false, false],
  "alarmName": "경험치업.mp3",
  "tts": false,
  "customAlarms": [
    {"Name": "", "Tick": 0, "Enabled": false},
    {"Name": "", "Tick": 0, "Enabled": false},
    {"Name": "", "Tick": 0, "Enabled": false}
  ],
  "latestUrl": ""
}
```

### 알람 슬롯 8개 (고정)

`[0]=2h(7200s)` → `[1]=1h(3600s)` → `[2]=30m(1800s)` → `[3]=20m(1200s)` → `[4]=15m(900s)` → `[5]=10m(600s)` → `[6]=100s` → `[7]=55s`

### NuGet 패키지

| 패키지 | 용도 | 설치 대상 프로젝트 |
|--------|------|-------------------|
| `Microsoft.Web.WebView2` | Edge 내장 브라우저 | JHP |
| `NAudio` | mp3 알람 재생 | JHP.Api |
| `Octokit` | GitHub API (버전 체크) | JHP.Api |
| `System.Speech` | TTS 음성 안내 | JHP.Api |

---

## 📁 최종 프로젝트 폴더 구조

```
C:\Users\wjgus\source\repos\dlrj0\JHP\
├── .gitignore
├── README.md
├── CHANGELOG.md
├── JHP.slnx
├── alarm\
│   └── (mp3 파일 직접 넣기 — git 제외)
├── JHP\
│   ├── JHP.csproj
│   ├── Program.cs
│   ├── Form1.cs
│   ├── Form1.Designer.cs
│   ├── AlarmForm.cs
│   └── SiteForm.cs
├── JHP.Api\
│   ├── JHP.Api.csproj
│   ├── Config.cs
│   ├── Site.cs
│   ├── CustomAlarm.cs
│   ├── Synth.cs
│   ├── UpdateChecker.cs
│   ├── Prompt.cs
│   ├── ReSize.cs
│   └── ToolStripCommand.cs
├── JHP.Controls\
│   ├── JHP.Controls.csproj
│   ├── NSlider.cs
│   ├── SiteListViewControl.cs
│   ├── ControlButton.cs
│   └── CustomCheckBox.cs
└── JHP.Asset\
    ├── JHP.Asset.csproj
    └── UserScripts.cs
```

---

## 🖥️ PART 1 — Visual Studio 설치

### 1-1. 설치 파일 다운로드

```
https://visualstudio.microsoft.com/ko/vs/community/
```

### 1-2. 워크로드 선택

| 체크 여부 | 워크로드 이름 |
|-----------|--------------|
| ✅ 반드시 | **.NET 데스크톱 개발** |
| ❌ 선택 안 함 | ASP.NET, Azure, C++, 게임 개발 등 |

### 1-3. 설치 후 설정

- 로그인: Microsoft 계정
- 개발 설정: "Visual C#"
- 색 테마: "어둡게(Dark)" 권장
- 줄 번호 표시: 도구 → 옵션 → 텍스트 편집기 → 모든 언어 → ✅ "줄 번호" 체크

---

## 🐙 PART 2 — GitHub 계정 연동 및 저장소 설정

### 2-1. VS에서 GitHub 계정 연동

```
VS 우측 상단 계정 아이콘 → "계정 추가" → "GitHub" 선택
→ 브라우저에서 GitHub 로그인 → 권한 허용
```

```
도구 → 옵션 → 소스 제어 → Git 전역 설정
→ 사용자 이름 / 이메일 입력 → 확인
```

### 2-2. 저장소 복제

```
VS 시작 화면 → "저장소 복제"
→ URL: https://github.com/dlrj0/JHP
→ 로컬 경로: C:\Users\wjgus\source\repos\dlrj0\JHP
→ "복제" 클릭
```

### 2-3. .gitignore 추가 항목

```gitignore
# 배포 결과물
output/
bin/
obj/

# 개인 설정
config.json

# WebView2 캐시
cache/

# mp3 파일 (저작권)
alarm/*.mp3

# VS 개인 설정
*.user
*.suo
.vs/
```

### 2-4. 커밋 & 푸시 방법

```
보기 메뉴 → "Git 변경 내용" 탭
→ 변경된 파일 목록 확인
→ 커밋 메시지 작성
→ "모두 커밋 및 푸시" 클릭
```

---

## 🛠️ PART 3 — 솔루션 및 프로젝트 구조 생성

### 3-1. 첫 프로젝트(JHP) 생성

```
Visual Studio 시작 화면 → "새 프로젝트 만들기"
→ "Windows Forms 앱" (C#, Windows, Desktop 태그) 선택 → 다음
→ 프로젝트 이름: JHP
→ 위치: C:\Users\wjgus\source\repos\dlrj0
→ 솔루션 이름: JHP
→ "솔루션 및 프로젝트를 같은 디렉터리에 배치" 체크 해제
→ 다음
→ 프레임워크: .NET 10.0 선택
→ 만들기
```

### 3-2. 나머지 3개 프로젝트 추가 (클래스 라이브러리)

```
솔루션 탐색기 → 솔루션 'JHP' 우클릭 → 추가 → 새 프로젝트
→ "클래스 라이브러리" (C#) 선택 → 다음
→ 프로젝트 이름: JHP.Api / JHP.Controls / JHP.Asset (각각)
→ 프레임워크: .NET 10.0 → 만들기
```

### 3-3. TargetFramework 수정

**JHP.Api / JHP.Controls** — 프로젝트 더블클릭 후 수정:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**JHP.Asset**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### 3-4. 프로젝트 간 참조 설정

```
JHP 프로젝트 우클릭 → 추가 → 프로젝트 참조
→ JHP.Api, JHP.Controls, JHP.Asset 모두 체크 → 확인

JHP.Controls 프로젝트 우클릭 → 추가 → 프로젝트 참조
→ JHP.Api 체크 → 확인
```

### 3-5. NuGet 패키지 설치

```
솔루션 탐색기 → 대상 프로젝트 우클릭
→ NuGet 패키지 관리 → 찾아보기 탭 → 검색 → 설치
```

| 패키지명 | 설치 대상 프로젝트 |
|----------|-------------------|
| `Microsoft.Web.WebView2` | JHP |
| `NAudio` | JHP.Api |
| `Octokit` | JHP.Api |
| `System.Speech` | JHP.Api |

### 3-6. 새 코드 파일 추가 방법

```
솔루션 탐색기 → 파일을 추가할 프로젝트 우클릭
→ 추가 → 클래스
→ 이름 입력 → 추가
→ 자동 생성된 내용 전체 선택(Ctrl+A) → 삭제
→ Claude가 준 코드 붙여넣기(Ctrl+V)
→ 저장(Ctrl+S)
```

---

## 💬 PART 4 — Claude에게 코드 요청 방법

### 4-1. 새 대화 시작 시

새 대화를 시작할 때 아래를 붙여넣고 요청합니다:
1. **이 md 파일 전체**
2. **JHP UI 개선 작업 지시서 v2.md 전체** (Form1 작업 시 반드시 포함)
3. (필요 시) 이미 작성된 코드 파일 내용

> **모든 작업은 Visual Studio GUI로만 진행하므로,
> Claude가 dotnet, cmd, 터미널 명령을 제시하면 GUI로 바꿔서 설명해달라고 요청하세요.**

### 4-2. 코드 요청 순서 및 완료 현황

| 순서 | 파일 | 상태 |
|------|------|------|
| 1 | `Site.cs` | ✅ 완료 |
| 2 | `CustomAlarm.cs` | ✅ 완료 |
| 3 | `Config.cs` | ✅ 완료 |
| 4 | `Synth.cs` | ✅ 완료 |
| 5 | `UpdateChecker.cs` | ✅ 완료 |
| 6 | `Prompt.cs` | ✅ 완료 |
| 7 | `ReSize.cs` | ✅ 완료 |
| 8 | `ToolStripCommand.cs` | ✅ 완료 |
| 9 | `UserScripts.cs` | ✅ 완료 |
| 10 | `NSlider.cs` | ✅ 완료 |
| 11 | `SiteListViewControl.cs` | ✅ 완료 |
| 12 | `ControlButton.cs` | ✅ 완료 |
| 13 | `CustomCheckBox.cs` | ✅ 완료 |
| 14 | `AlarmForm.cs` | ✅ 완료 (TrackBar 사용 중 — UI 개선 시 NSlider로 교체 예정) |
| 15 | `SiteForm.cs` | ✅ 완료 |
| 16 | `Program.cs` | ✅ 완료 |
| **17** | **`Form1.cs`** | **⏳ 다음 작업** |
| **18** | **`Form1.Designer.cs`** | **⏳ 다음 작업** |
| 19 | `README.md` | ⏳ 대기 |
| 20 | `CHANGELOG.md` | ⏳ 대기 |

> **Form1.cs 작업 시 반드시 "JHP UI 개선 작업 지시서 v2.md"를 함께 첨부하세요.**

### 4-3. 기존 파일 교체 방법

1. VS에서 해당 파일 열기
2. 전체 선택 (`Ctrl+A`) → 삭제
3. Claude 코드 붙여넣기 (`Ctrl+V`)
4. 저장 (`Ctrl+S`)
5. 빌드 → 솔루션 빌드 (`Ctrl+Shift+B`)로 오류 확인

---

## ✅ PART 5 — 개발 중 체크포인트

### 각 단계 완료 후 확인

```
□ 파일 저장됨
□ 빌드 오류 없음 (출력 창에 "0개 오류")
□ Git 변경 내용 탭 → 커밋 메시지 입력 → 모두 커밋 및 푸시 완료
```

### 빌드 오류 발생 시

```
[프로젝트 개요]
JHP, .NET 10.0 WinForms + WebView2, [현재 X단계]

[오류 메시지]
(오류 내용 붙여넣기)

[문제 파일]
(해당 파일 코드 붙여넣기)
```

### Git 커밋 메시지 컨벤션

| 접두사 | 예시 |
|--------|------|
| `feat:` | `feat: Form1.cs 작성 — 창 컨트롤 및 알람패널 통합` |
| `fix:` | `fix: 알람 타이머 오류 수정` |
| `refactor:` | `refactor: Form1 레이아웃 정리` |
| `chore:` | `chore: gitignore 업데이트` |
| `docs:` | `docs: README 작성` |

### Visual Studio 핵심 단축키

| 단축키 | 기능 |
|--------|------|
| `Ctrl + Shift + B` | 솔루션 빌드 |
| `F5` | 디버그 실행 |
| `Ctrl + F5` | 디버그 없이 실행 |
| `Ctrl + K, Ctrl + D` | 현재 파일 코드 자동 정렬 |
| `Ctrl + ,` | 파일/클래스 빠른 검색 |
| `F12` | 정의로 이동 |
| `Ctrl + \, E` | 오류 목록 창 열기 |

---

## 🖥️ PART 6 — Windows 호환성 요구사항

### 운영체제별 주의사항 (원본 개발자 공지 반영)

| OS | 상태 | 조치 |
|----|------|------|
| Windows 10 (1903+) | ✅ 완전 지원 | WebView2 런타임 별도 설치 필요 (Win11은 내장) |
| Windows 11 | ✅ 완전 지원 | SmartScreen: 최초 실행 시 "추가 정보 → 실행" 클릭 필요 (코드 서명 없는 EXE의 일반 현상) |
| Windows 7 | ⚠️ TTS 미작동 | System.Speech TTS 기능이 기본 미설치. Synth.cs의 try-catch로 이미 처리됨. README에 안내 문구 추가 필요 |
| Windows 8/8.1 | ⚠️ 미검증 | 지원 불필요 (EOL) |

### WebView2 런타임 처리

```csharp
// Program.cs 또는 Form1_Load에서 WebView2 런타임 존재 확인
// WebView2 초기화 실패 시 사용자에게 안내 메시지 표시
private async Task InitWV()
{
    try
    {
        var env = await CoreWebView2Environment.CreateAsync(
            null,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache")
        );
        await _webView.EnsureCoreWebView2Async(env);
        // ... JS 주입, URL 로드
    }
    catch (Exception ex)
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

### DPI 스케일링 (고해상도 모니터)

```csharp
// Program.cs — ApplicationConfiguration.Initialize() 이미 처리됨
// JHP.csproj에 아래 항목이 있는지 확인 (자동 생성되지 않을 수 있음):
<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
```

### 원본 개발자 보고 버그 목록 (인벤 게시글 기반)

| 버그 | 원인 | Form1.cs에서의 처리 방법 |
|------|------|------------------------|
| 항상 위 체크 시 사이트 관리창 안 보임 | `TopMost = true`일 때 자식 폼이 가려짐 | SiteForm/AlarmForm 등 ShowDialog 호출 전 `this.TopMost = false`로 잠시 해제, 닫힌 후 복원 |
| 메뉴 구분선 클릭 시 에러 | ToolStripSeparator.Click 이벤트 미처리 | ToolStripCommand 처리 시 null/구분선 여부 체크 필요 |
| 테두리 숨기기 | 포커스 이탈 시 타이틀바 숨김 | `OnActivated`/`OnDeactivate` 이벤트에서 처리 |

---

## ⚡ PART 7 — 장시간 사용 최적화 (2시간+ 세션 대응)

> **이 프로그램은 사냥 중 2시간 이상 켜두는 것이 기본입니다.
> 아래 최적화 항목을 반드시 Form1.cs 구현 시 적용하세요.**

### 7-1. WebView2 메모리 관리

```csharp
// ✅ 캐시를 디스크에 저장 (메모리 캐시 누적 방지)
var env = await CoreWebView2Environment.CreateAsync(
    null,
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache")
);

// ❌ 아래처럼 null 경로 사용 시 임시 메모리 캐시 → 누적 문제 발생
// await _webView.EnsureCoreWebView2Async();

// ✅ Form 닫힐 때 WebView2 명시적 Dispose
protected override void OnFormClosing(FormClosingEventArgs e)
{
    _webView?.Dispose();
    base.OnFormClosing(e);
}
```

### 7-2. NAudio 메모리 누수 방지

```csharp
// Synth.cs의 Ring()이 호출될 때마다 Stop()으로 기존 리소스 해제 후 새로 생성
// 이미 Synth.cs에서 Stop() → 새 인스턴스 생성 패턴으로 구현됨 → 정상
// Form 닫힐 때 Synth.Instance.Stop() 호출 필요
protected override void OnFormClosing(FormClosingEventArgs e)
{
    Synth.Instance.Stop();  // NAudio 리소스 해제
    _webView?.Dispose();
    base.OnFormClosing(e);
}
```

### 7-3. Timer 설정

```csharp
// ✅ 1000ms 간격 (1초마다) — 알람 정밀도로 충분
_timer = new System.Windows.Forms.Timer { Interval = 1000 };
_timer.Tick += Timer_Tick;

// ❌ 100ms 이하 간격은 불필요한 CPU 사용
```

### 7-4. 불필요한 Invalidate 방지

```csharp
// NSlider, ControlButton 등 커스텀 컨트롤은 이미 더블 버퍼링 적용됨
// Form1 자체도 더블 버퍼링 설정 권장
protected override void OnLoad(EventArgs e)
{
    base.OnLoad(e);
    SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
}
```

### 7-5. 타이머 경과 시간 추적 방식

```csharp
// ✅ DateTime 기반 — tick 누적 오차 없음
private DateTime _timerStartTime;
private void Timer_Tick(object? sender, EventArgs e)
{
    int elapsed = (int)(DateTime.Now - _timerStartTime).TotalSeconds;
    // elapsed로 알람 체크
}

// ❌ 카운터 누적 방식 — UI 블로킹 시 tick 누락으로 오차 발생
// private int _elapsedSeconds = 0;
// _elapsedSeconds++;
```

### 7-6. System.Speech 메모리

```csharp
// SpeechSynthesizer는 싱글톤으로 유지 (Synth.cs에서 이미 처리됨)
// 매번 new SpeechSynthesizer() 생성 금지 — COM 객체 누수 발생
// TTS() 호출마다 SpeakAsyncCancelAll() → SpeakAsync() 순서로 이미 처리됨
```

---

## ✍️ PART 8 — 주석 작성 기준

### 배포 전 정리 요청 방법

```
이 파일을 배포 전 정리해줘.
- 자명한 동작 설명 주석 제거
- 코드 안 이모지 제거
- 단계 나열식 주석 제거
- 비직관적인 수치, 순서 의존성, 수정 가능성 있는 값에만 주석 유지
- 코드 동작 자체는 변경하지 말 것
```

### 유지 대상

| 유형 | 예시 |
|------|------|
| 비직관적인 수치 | `private const int RESIZE_THICK = 8; // 마우스 드래그 감지 영역(px)` |
| 알람 슬롯 인덱스 | `// [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s` |
| WebView2 초기화 순서 | `// EnsureCoreWebView2Async 완료 전에 Source 설정 시 예외` |
| NAudio 주의 | `// NAudio: WaveStream은 재생 후 반드시 Dispose 필요` |
| WndProc 상수 | `// HTCAPTION = 2: 윈도우 타이틀바 영역으로 처리 (드래그 이동 허용)` |

---

## 📦 PART 9 — 빌드 및 배포용 exe 생성

### 9-1. Release 빌드

```
VS 상단 드롭다운: Debug → Release 변경
빌드 → 솔루션 빌드 (Ctrl+Shift+B)
```

### 9-2. 게시 프로필 만들기 (최초 1회)

```
솔루션 탐색기 → JHP 프로젝트 우클릭 → 게시
→ 대상: 폴더 → 다음
→ 경로: C:\Users\wjgus\source\repos\dlrj0\JHP\output
→ 마침
```

### 9-3. 자체 포함(Self-contained) 배포 설정 — 권장

| 항목 | 값 |
|------|-----|
| 구성 | Release |
| 대상 런타임 | win-x64 |
| 배포 모드 | **자체 포함** |

### 9-4. 최종 배포 폴더 구조

```
output\
├── JHP.exe
├── JHP.dll
├── config.json           ← 첫 실행 시 자동 생성
├── alarm\
│   └── 경험치업.mp3
└── cache\                ← 첫 실행 시 자동 생성
```

---

## 🚀 PART 10 — GitHub Release 업로드

```
https://github.com/dlrj0/JHP/releases/new
```

### Release Notes 양식

```markdown
## 설치 방법
1. `JHP_v1.0.0.zip` 압축 해제
2. `alarm` 폴더에 알람용 mp3 파일 추가
3. `JHP.exe` 실행

> 자체 포함 빌드 — 별도 런타임 설치 불필요

---

## 이번 버전 내용
- 재획비 타이머 8슬롯 (2h / 1h / 30m / 20m / 15m / 10m / 100s / 55s)
- 커스텀 알람 3슬롯
- 내장 브라우저 (WebView2)
- mp3 알람 / TTS 음성 안내
- 사이트 즐겨찾기
- OTT 다음화 자동 스킵 (라프텔, 넷플릭스)
- 항상 위 / 투명도 조절
- 자동 업데이트 확인 (GitHub Releases)

---

## 주의사항
본 프로그램은 넥슨의 공식 허가를 받은 프로그램이 아닙니다.
사용으로 발생하는 모든 불이익에 대한 책임은 사용자 본인에게 있습니다.
```

---

## 📝 PART 11 — README.md 작성 가이드

```markdown
# JHP

메이플스토리 재획비 타이머 + OTT 시청 보조 도구

## 다운로드
→ [Releases 페이지](https://github.com/dlrj0/JHP/releases)에서 최신 버전 다운로드

## 설치 방법
1. zip 압축 해제
2. alarm 폴더에 알람용 mp3 파일 추가
3. JHP.exe 실행

## 주요 기능
- 재획비 타이머 8슬롯 (좌클릭으로 시작/재시작)
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
- Windows 11: 최초 실행 시 SmartScreen 경고가 표시될 수 있습니다.
  "추가 정보" → "실행" 클릭 후 정상 사용 가능합니다.
- Windows 7: TTS 기능이 기본 설치되지 않아 음성 안내가 작동하지 않을 수 있습니다.

## 주의사항
본 프로그램은 넥슨의 공식 허가를 받은 프로그램이 아닙니다.
사용으로 발생하는 모든 불이익에 대한 책임은 사용자 본인에게 있습니다.

## 라이선스
MIT
```

---

## ❓ PART 12 — 자주 발생하는 문제 및 해결

**Q. 빌드 시 "System.Speech를 찾을 수 없음" 오류**

```xml
<!-- JHP.Api.csproj에서 변경 -->
<TargetFramework>net10.0-windows</TargetFramework>
```

**Q. WebView2가 초기화되지 않음**
- 원인: WebView2 런타임 미설치
- 해결: https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/ 에서 런타임 설치

**Q. mp3 알람이 재생되지 않음**
- `alarm` 폴더가 `JHP.exe`와 같은 폴더에 있는지 확인
- `config.json`의 `alarmName` 값이 실제 mp3 파일명과 일치하는지 확인

**Q. 창이 화면 밖으로 나가서 돌아오지 않음**

```csharp
// Form1_Load에서 화면 범위 벗어남 체크
var screen = Screen.FromPoint(new Point(cfg.X, cfg.Y));
if (!screen.WorkingArea.Contains(cfg.X, cfg.Y))
{
    Left = 100;
    Top = 100;
}
```

**Q. 항상 위 설정 시 사이트 추가 창이 안 보임**

```csharp
// SiteForm 열기 전 TopMost 해제, 닫힌 후 복원
bool wasTopMost = TopMost;
TopMost = false;
using var sf = new SiteForm();
sf.ShowDialog(this);
TopMost = wasTopMost;
```

**Q. 장시간 사용 후 메모리 증가**
- WebView2 캐시가 디스크가 아닌 임시 경로로 설정된 경우 발생
- `CoreWebView2Environment.CreateAsync(null, "./cache")` 경로를 확인하세요

**Q. GitHub 푸시 시 인증 오류**
- 파일 → 계정 설정 → GitHub → 로그아웃 후 재로그인

**Q. publish(게시) 후 exe 실행 시 즉시 종료됨**
- 게시 프로필에서 **배포 모드 = 자체 포함**으로 지정 후 다시 게시

---

## 📋 PART 13 — 진행 상태 체크리스트

### 환경 설정
```
□ Visual Studio Community 설치
□ .NET 데스크톱 개발 워크로드 설치
□ GitHub 계정 생성
□ VS에 GitHub 계정 연동
□ Git 사용자 정보 설정
□ JHP 저장소 생성 (GitHub 웹)
□ VS에서 저장소 복제
□ .gitignore 업데이트 후 커밋 & 푸시
```

### 프로젝트 구조
```
□ alarm 폴더 생성
□ JHP 프로젝트 생성 (Windows Forms 앱, .NET 10.0)
□ JHP.Api, JHP.Controls, JHP.Asset 프로젝트 추가
□ TargetFramework → net10.0-windows 수정
□ Class1.cs 삭제
□ 프로젝트 간 참조 설정
□ NuGet 패키지 4개 설치
□ 첫 빌드 테스트 성공
□ 초기 구조 커밋 & 푸시
```

### 코드 작성
```
✅ Site.cs
✅ CustomAlarm.cs
✅ Config.cs
✅ Synth.cs
✅ UpdateChecker.cs
✅ Prompt.cs
✅ ReSize.cs
✅ ToolStripCommand.cs
✅ UserScripts.cs
✅ NSlider.cs
✅ SiteListViewControl.cs
✅ ControlButton.cs
✅ CustomCheckBox.cs
✅ AlarmForm.cs
✅ SiteForm.cs
✅ Program.cs
⏳ Form1.cs          ← 다음 작업 (UI 개선 지시서 v2 참조)
⏳ Form1.Designer.cs ← 다음 작업
```

### 문서
```
⏳ README.md
⏳ CHANGELOG.md
```

### 배포
```
□ Release 빌드 성공
□ 게시(Publish) 프로필 생성 및 게시 실행
□ alarm 폴더 mp3 복사
□ 실행 테스트 (output\JHP.exe 직접 실행)
□ output 폴더 zip 압축
□ GitHub Releases v1.0.0 업로드
```

---

## 🔗 참고 링크

| 항목 | URL |
|------|-----|
| Visual Studio Community | https://visualstudio.microsoft.com/ko/vs/community/ |
| .NET 10.0 Desktop Runtime | https://dotnet.microsoft.com/download/dotnet/10.0 |
| WebView2 런타임 | https://developer.microsoft.com/ko-kr/microsoft-edge/webview2/ |
| NAudio 문서 | https://github.com/naudio/NAudio |
| Octokit 문서 | https://github.com/octokit/octokit.net |
| 원본 프로그램 GitHub | https://github.com/d3vdev/JHP |
| 원본 인벤 글 v1 | https://www.inven.co.kr/board/maple/2304/34038 |
| 원본 인벤 글 v2 | https://www.inven.co.kr/board/maple/2304/34059 |
