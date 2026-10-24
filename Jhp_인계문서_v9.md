# JHP 인계 문서 v9 (압축본, 2026-10-24)

> 새 세션은 `CLAUDE.md` → 이 문서의 0장(재개 지점) → (필요할 때만) `Jhp_스펙문서.md`, `CHANGELOG.md` 순으로 읽으세요.
> v8까지의 상세 작업 이력은 이 문서로 압축했고 원문은 git 기록(`git show <커밋>:Jhp_인계문서_v8.md`)에 남아 있습니다.
> 되돌리기 지점: 태그 `jhp-checkpoint-1`~`6`, 커밋 메시지의 `[Q1]`~`[Q7]`, `[A1]`~`[A12]`.

---

## 0. 재개 지점 (2026-10-24 세션 종료 시점)

**저장소**: 브랜치 `test`, 최신 푸시 `801e8c2`(+ 이 세션 마지막의 우클릭 이동 수정 커밋). 빌드 0 오류·경고 0, 단위 57/57, UI 68/68(연속 통과).
**실행**: 저장소 루트 `run-jhp.bat`(게시 후 실행, `fast`는 게시 생략) → `app/JHP.exe`. 사용자 JHP가 같은 폴더에서 실행 중이면 게시가 실패하고 마지막 게시본을 실행한다.

**이번 세션에서 한 일 (요약, 자세한 내용은 CHANGELOG)**
- 게시를 framework-dependent로 변경(약 122MB → 3.7MB, .NET 10 Desktop Runtime 필요), 게시 폴더는 루트 `app/`(gitignore). 무해한 MSB3277 경고는 csproj `NoWarn`으로 숨김.
- **테스트가 포커스를 뺏고 화면에 뜨던 문제 해결**: `tools/ui-test.ps1`의 `FocusWatch`가 포그라운드 이동·화면 안 노출을 측정(`focus:` 검사, 인스턴스 이름·시작+ms·사용자 입력 후 ms 기록). 원인(UIA `Toggle/SetValue`가 창 활성화, 창 생성 순간 활성화, 설정창 Clamp·툴팁·최대화 복원)과 해결 규칙은 `CLAUDE.md`의 '포커스·화면 노출 측정' 항목.
- UI: 타이틀바 'JHP' 문구 제거, 볼륨 이름 클릭=0(음소거)↔이전 값(투명도는 100↔이전 값), 숫자는 제자리 편집(라벨은 이름만 그리고 같은 글꼴의 테두리 없는 입력 칸이 숫자 자리를 차지).
- ▼ 메뉴: **시크릿 모드**(InPrivate, 전용 폴더 `cache-private`, 전환 시 웹 화면 컨트롤 재생성, 설정 `privateMode`, 타이틀바 보라색 '시크릿'), **브라우저 기록 삭제 항목 선택 대화상자**(`BrowserDataForm`, 쿠키·사이트 데이터=로그인 해제 포함), 우클릭 시 사이트 이동하던 문제 수정(실제 팝업에서 `MouseDown→Click→MouseUp` 순서임을 `tools/menu-probe`로 실측), 우클릭 관리 메뉴에 '삭제…' 추가(우클릭 시 ▼ 목록은 닫히고 관리 메뉴가 같은 자리에 뜸; 동작 후/Esc로 닫으면 목록 복귀, 메뉴 밖 클릭은 복귀 안 함).
- 다른 클로드(AniShelf 세션)에게 물어 포커스 안 뺏는 테스트 방법을 참고했고, 서브에이전트(읽기 전용 점검)로 시크릿 모드 코드를 검토해 경합·폴백·종료 정리 문제를 고침.

**사용자가 직접 확인해야 하는 것(자동 테스트가 못 여는 ▼ 메뉴 팝업/모달 흐름)**
1. ▼ 메뉴 '시크릿 모드' 켜기/끄기: 확인창 → 웹 화면이 새로 시작(시작 사이트), 타이머·알람 유지, 끄면 일반 모드 로그인으로 복귀, 프로그램 껐다 켜도 시크릿 유지.
2. '브라우저 기록 삭제…' 대화상자: 항목 선택 → 삭제 → "삭제했습니다", 쿠키·사이트 데이터를 고르면 로그인이 풀리는지.
3. 사이트 항목 우클릭: 이동하지 않고 관리 메뉴만 뜨는지, '삭제…'와 ✕ 삭제(확인창), 시작 사이트 지정.
4. 넷플릭스 건너뛰기/다음 화가 JHP 안에서 실제로 동작하는지(5장).
5. 런타임이 없는 PC에서 .NET 설치 안내 창이 뜨는지(미확인).

**다음에 할 수 있는 일(우선순위 순)**
- 위 확인에서 나온 문제 수정. 시크릿 모드는 코드로만 점검했고 전환 동작은 실행 확인 전이다.
- 시크릿 모드 중 알람 음소거(`_alarmMuteActive`)와 웹 화면 재생성의 상호작용(실제 오동작은 없을 것으로 점검됨, 필요하면 재생성 직후 재적용).
- 배포: `app/`에서 `cache/`·`config.json`을 빼고 zip(Release/태그는 사용자가 요청할 때만).
- 원본 프로그램(`C:\Users\wjgus\Desktop\JHP (2)\JHP.exe`)과 비교해 사용자가 찾는 개선점.
- 창 가장자리 두께(현재 2)를 더 줄일지(제안으로만 남아 있음), `Form1.cs`(약 1500줄) 분리(이득 작음, 회귀 위험).

**알려진 불안정**: UI 테스트의 `focus:` 검사가 가끔 '화면 안에 보임'(pid 구분: `pp` 인스턴스, 100,100 위치)을 한 번 잡은 적이 있으나 재현되지 않았다(연속 7회 통과). 다시 나오면 기록의 인스턴스 이름·시작+ms로 원인을 가린다. 그 외 인라인 입력(투명도) 검사가 드물게 실패하는 타이밍 문제는 이전과 같다.

**작업 팁**
- 사용자 JHP가 실행 중이면 `bin`/`app`이 잠긴다 → 빌드는 `-p:OutDir=<스크래치 폴더>\`, 테스트는 `ui-test.ps1`이 자체 폴더에 빌드한다.
- 파이썬으로 한글이 든 파일을 고칠 때: Bash heredoc(`python - <<EOF`)은 한글이 깨지거나 인코딩 오류가 난다 → 스크립트를 Write 도구로 `.py` 파일에 저장해 실행하고, 경로/C# 이스케이프는 raw 문자열(r 접두사)로 쓴다(`\a`가 BEL이 된 적 있음). CRLF/BOM은 유지.
- WebView2 안쪽 DOM 확인은 Playwright MCP(별도 브라우저) 또는 `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`로 띄운 JHP에 CDP. 확인 후 자기가 띄운 PID만 종료.
- 캡처(`PrintWindow`)는 막 열린 창에서 라벨이 검게 나올 수 있다 → 몇 초 기다린 뒤 두 번 찍는다.
- 서브에이전트는 사용자가 허용했다(읽기 전용 점검 위주, 동시 1~2개).

## 1. 프로젝트

| 항목 | 값 |
|---|---|
| 목적 | 메이플스토리 재획비 타이머 + OTT/사이트 시청 보조 창 (원본: 인벤 d3vdev "재획플레이어" 재구현) |
| 기술 | .NET 10 (net10.0-windows) WinForms + WebView2, NAudio, System.Speech |
| 솔루션 | `JHP.slnx` — `JHP`(UI) / `JHP.Api`(로직) / `JHP.Asset`(OTT 스크립트) / `JHP.Controls`(커스텀 컨트롤) / `JHP.Tests`(xunit) |
| 경로 / 원격 | `C:\Users\wjgus\source\repos\dlrj0\JHP`, https://github.com/dlrj0/JHP — 작업 브랜치 `test`, 배포 브랜치 `main` |
| 버전 | `JHP/JHP.csproj`의 `<Version>1.1.0</Version>`이 단일 출처. Release 미생성 |
| 게시 | `JHP/Properties/PublishProfiles/win-x64.pubxml` (framework-dependent, .NET 10 Desktop Runtime x64 필요). 결과물은 저장소에 넣지 않음 |

주요 파일: `Form1.cs`(타이머·메뉴·WebView·창 동작), `AlarmForm.cs`(알람 설정), `SiteForm.cs`, `Program.cs`(단일 인스턴스·오류 로그),
`Api/Config.cs`(설정·정규화·내보내기), `Api/AlarmScheduler.cs`(알람 계산, 단위 테스트됨), `Api/Synth.cs`(mp3/TTS), `Api/UpdateChecker.cs`, `Api/DarkMessageBox.cs`, `Asset/UserScripts.cs`.

## 2. 작업 원칙 (사용자 요구, 항상 유지)

1. **PC 사용 방해 금지**: 실제 마우스 이동/클릭, 키 입력, 포커스 이동, `taskkill /IM JHP.exe`, 화면을 덮는 최대화 금지. 종료는 자기가 띄운 PID만. 작업 중 창이 잠깐 보이는 것은 허용, **끝나면 최소화/화면 밖/종료**. 자동 테스트 창은 활성화되지 않도록 `JHP_OFFSCREEN=1`(`ShowWithoutActivation`).
2. **게임 안전**: 메이플 사냥 중 같이 쓰는 도구. 게임 프로세스/창 접근(메모리, 프로세스·창 열거, 키 입력 합성, 후킹, 화면 캡처, 주입) 기능 추가 금지. 전역 단축키는 `RegisterHotKey`만(옵트인, 기본 꺼짐, Ctrl+Alt+Shift+F10).
3. **추측 금지**: 확인되지 않은 파일/셀렉터는 바꾸지 않고 사용자에게 요청. 큰 구조 변경은 착수 전 확인.
4. **푸시 정책**: 빌드 0 오류 + 단위 테스트 + UI 테스트 통과일 때만 `test` 푸시. `main` 병합·Release·태그·강제 푸시는 사용자가 요청할 때만(이번 세션에 `test`→`main` 병합 요청을 받아 수행).
5. 코드를 바꾸면 같은 턴에 이 문서와 CHANGELOG 갱신. 커밋 메시지에 attribution 줄 금지.
6. 서브에이전트는 읽기 전용 점검/리뷰만, 동시에 1~2개(최대 3). 수정은 한 주체만.
7. 토큰 절약: 테스트 출력은 `grep -E "FAIL|결과"`로 줄이고, 사용자가 요청한 범위만 실행(불필요한 전체 반복 금지).

## 3. 빌드 / 테스트

```
dotnet build JHP/JHP.csproj                                         # 0 오류가 기준 (경고 0건: WebView2 참조 충돌 MSB3277은 csproj `NoWarn`으로 숨김)
dotnet test JHP.Tests/JHP.Tests.csproj                              # 단위 57개
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test.ps1 -OutDir <캡처폴더> [-SkipBuild] [-IncludeMenu]
```
- 기준선(2026-10-24): 빌드 0 오류, 단위 57/57, UI 기본 68항목(시크릿 6항목과 마지막 `focus:` 포함, 연속 통과) / `-IncludeMenu`는 사이트·메뉴 항목이 더해짐.
- **포커스 안 뺏는 테스트(2026-10-24)**: `FocusWatch`가 포그라운드 이동·화면 안 노출을 측정해 `focus:` 검사로 보고한다. 알람 설정창 조작은 UIA 대신 `BM_CLICK`/`WM_SETTEXT`/마우스 메시지, 앱은 테스트 모드(`JHP_OFFSCREEN=1`)에서 화면 밖 생성·`WS_EX_NOACTIVATE`·`FocusWeb()`·Clamp/툴팁/최대화 생략. 자세한 원인과 규칙은 `CLAUDE.md`의 '포커스·화면 노출 측정' 항목.
- `-IncludeMenu`는 메뉴 팝업이 사용자 화면에 잠깐 보이고 포커스를 건드릴 수 있어, 사용자가 요청할 때만 실행.
- **알려진 불안정**: UI 테스트가 실행마다 다른 항목(숫자 입력, 읽기 전용/잠김 설정 종료, 타이머 색)에서 간헐적으로 실패하고 재실행하면 통과함. 사용자가 PC를 조작하지 않아도 발생 → 코드 회귀가 아니라 타이밍 문제로 추정, **원인 미확정**. 해결안: 스크립트에 구간 선택 옵션(`-Only`) 추가 후 실패 구간만 반복, 불안정한 검사에 대기/재시도 추가.
- `tools/ui-test.ps1`: "alarm controls not found"는 FAIL로 기록, UIA 시간 초과는 SKIP(건수 표시). 컨트롤 이름/구조를 바꾸면 SKIP/FAIL을 확인할 것.
- 사용자의 JHP가 실행 중이면 `bin` DLL이 잠겨 빌드 실패 → `-p:OutDir=<다른 폴더>\`.

## 4. 현재 구현 상태 (요약)

- **UI**: 타이틀바 1줄(▼ 메뉴, 타이머 버튼, 남은 시간 라벨, 볼륨/투명도 NSlider + 숫자 제자리 편집(라벨은 이름만, 같은 글꼴의 입력 칸이 숫자 자리를 차지, 즉시 반영, 이름 클릭 시 볼륨 0·투명도 100 ↔ 이전 값), 창 버튼), 가장자리 리사이즈(2px), 더블클릭 최대화, 다크 대화상자/타이틀바.
- **타이머**: 8슬롯 + 커스텀 3슬롯, 좌클릭 시작/정지(정지=초기화), 우클릭 알람창 토글, 실제 경과 시간 기준 계산(절전 복귀 120초 초과 시 재동기화), 겹치면 위쪽 하나만 TTS, 설정창 확인 시 바뀐 항목만 리셋.
- **▼ 메뉴**: 사이트 이동/추가/삭제(✕ 또는 우클릭 관리의 '삭제…', 확인창), 우클릭 관리(이동·수정·시작 사이트 지정·삭제; 우클릭은 이동하지 않음), 시크릿 모드(InPrivate, `Config.PrivateMode`, 웹 화면 컨트롤을 새로 만들어 전환: `RecreateWebViewAsync`/`TogglePrivateMode`/`InitWV`, 전용 폴더 `cache-private`는 시작 때마다 정리), 항상 위, 테두리 숨김, 알람 중 영상 음소거, 전역 단축키, 알람 설정, 설정 내보내기/가져오기(쿼리 제거), 브라우저 기록 삭제(맨 아래, 항목 선택 대화상자 `BrowserDataForm`).
- **견고성**: 단일 인스턴스(재실행 시 기존 창 복원), 설정 손상/잠김/읽기 전용 대응(`.corrupt` 보관, `%LOCALAPPDATA%\JHP` 대체), 저장 실패 알림, 설정 주소 http(s) 검증, `error.log` 1MB 제한, WebView2 렌더러 복구, TTS 장치 재연결, 업데이트 알림(건너뛰기, 활성 창일 때만 표시).
- **웹**: 새 창 정책(같은 창 이동, 자동 팝업 차단), HTML 전체화면 시 타이틀바 숨김, 이동마다 마지막 주소 저장, 넷플릭스 건너뛰기 감시 스크립트.

## 5. 남은 일

| 항목 | 상태/필요한 것 |
|---|---|
| **넷플릭스 건너뛰기 검증** | 요소 확인됨: `button[data-uia="player-skip-intro"]`(부모 `.watch-video--skip-content`), 크레딧 구간 `next-episode-seamless-button-draining`/`watch-credits-seamless-button`. 감시 스크립트(`MutationObserver`)로 변경했으나 **JHP 안에서 실제로 넘어가는지 미확인** → 사용자 확인 필요. 다음 화 버튼의 클릭 동작 상세도 미확인 |
| 시크릿 모드·기록 삭제 대화상자·우클릭 관리 확인 | 구현·빌드·자동 테스트(시작/표시/폴더 정리) 통과, 메뉴 팝업을 거치는 전환·삭제 흐름은 사용자 확인 필요(0장) |
| UI 테스트 안정화 | 인라인 입력(투명도) 검사의 드문 타이밍 실패, `focus:` 검사의 드문 화면 노출 기록(0장). 구간 선택 옵션(`-Only`)과 대기/재시도는 아직 없음 |
| 육안·청취 확인 | 알람 소리/TTS, 125%/150% 배율, 호버 색, 우클릭 관리 메뉴, 더블클릭 최대화, 단축키, 전체화면/팝업 차단, 새 다크 대화상자 |
| 설계 결정 | PerMonitorV2(혼합 배율에서 흐려짐), 테두리 숨김 시 웹 36px 이동, 키보드만으로 ▼ 메뉴 접근, 절전 복귀 정책, 알람 미리 알림, 기본 알람 mp3 동봉(저작권) |
| BrowserProcessExited | 현재는 재실행 안내 툴팁만(컨트롤 재생성 미구현) |
| Release | v1.1.0 Release 생성/zip 배포는 사용자 요청 시 |

## 6. 코드 함정 (같은 시간 낭비 방지)

- DPI: `AutoScaleDimensions=(96,96)`+`AutoScaleMode.Dpi`는 **자식 컨트롤을 모두 추가한 뒤**. px 상수는 `DeviceDpi/96f`. 창 크기 저장은 96dpi 기준 `Math.Round`.
- `Anchor=Right` 금지(부모 기본 폭 기준) → `LayoutTitleBar()`/`LayoutRightAligned()`로 직접 배치. `MaximumSize` 폭 0은 "제한 없음"이 아님.
- 커스텀 그리기 컨트롤은 불투명하게 칠함. 호버는 `_hovered`(MouseEnter/Move/Leave)로 추적 — 합성 마우스 이벤트로는 호버 자동 검증 불가.
- 메인 창 리사이즈는 가장자리 패널 + `WM_NCLBUTTONDOWN`(WebView가 `WM_NCHITTEST`를 가림).
- 설정/캐시/`error.log`는 `Config.DataDirectory`(exe 폴더, 쓰기 불가+기존 설정 없음 → `%LOCALAPPDATA%\JHP`), `alarm/`은 `AppContext.BaseDirectory`.
- 대화상자를 띄울 때 `WithDialogState`(TopMost 해제 + `_modalDepth`) 사용. 모달 항목은 UIA `Invoke`가 막히므로 테스트에서 `Invoke-ElAsync`.
- WinForms 한글 편집: Windows Python heredoc은 한글이 깨짐 → 스크래치 폴더에 파일로 저장해 `python -I`. 한글이 든 PowerShell 스크립트는 UTF-8 BOM. `Form` 안에서 `Site`는 `JHP.Api.Site`로 한정.
- 넷플릭스는 SPA(페이지 이동 없이 화면 전환) + 컨트롤은 실제(신뢰된) 입력이 있어야 표시됨. 건너뛰기 버튼은 재생 중 특정 시점에만 존재.
- **테스트가 포커스를 뺏지 않게**: 알람 설정창 등 WinForms 컨트롤은 UIA `Toggle/SetValue/Invoke` 대신 `BM_CLICK`/`WM_SETTEXT`/마우스 메시지로 조작(UIA 패턴은 창을 활성화한다). 앱은 테스트 모드(`JHP_OFFSCREEN=1`)에서 창을 처음부터 화면 밖에 만들고 `WS_EX_NOACTIVATE`를 쓴다. 새 테스트/창 경로를 추가하면 `focus:` 검사 출력을 확인.
- **ToolStrip 팝업 메뉴 항목**: 우클릭에도 `Click`이 오고 순서는 `MouseDown → Click → MouseUp`(`MouseUp`에서 막으면 늦음). `tools/menu-probe`로 실측 가능(투명도 0 팝업, 화면에 안 보임).
- **WebView2 시크릿 전환**: 프로필을 바꾸려면 컨트롤을 새로 만들어야 한다(`RecreateWebViewAsync`). `InitWV`는 시작 시 컨트롤을 지역 변수로 잡고(전환 경합 방지) `_webViewInitializing`이 0일 때만 전환한다. `cache-private`는 `InitWV`마다 지우고 종료 때도 지운다.
- 불필요 산출물: Playwright MCP가 만드는 `.playwright-mcp/`는 `.gitignore`에 있음. MCP 브라우저는 JHP의 WebView2와 별개(로그인/DOM 다를 수 있음). MCP 탭 전환은 창 포커스를 가져올 수 있음.

## 7. 원본 대비 메모

- 원본 기능 계승: 사이트 이동/추가/삭제, 좌클릭 시작·우클릭 설정창 토글, TTS는 위쪽 하나만, 항상 위, 테두리 숨김. 원본 소스(`d3vdev/JHP`)는 404라 추적 불가.
- 알람 TTS 이름(원본): 재획비, 경쿠, 경뿌/경쿠/유니온, 유니온 버프, 경쿠, 유니온 버프, 메소 회수, 파운틴. 기본 사이트 7개: 넷플릭스, 라프텔, 웨이브, 티빙, 쿠팡플레이, 디즈니플러스, 유튜브.
- 요구 문서(`Jhp_ui개선요구사항_v4.md` 등)의 항목은 모두 반영됨(타이틀바 1줄·V버튼·타이머 버튼·슬라이더 직접 입력·알람창 리사이즈). SiteForm 리사이즈(선택사항)와 타이머 우클릭 컨텍스트 메뉴(구버전 설계)는 의도적으로 미적용.
