# JHP 인계 문서 v9 (압축본, 2026-10-12)

> 새 세션은 `CLAUDE.md` → 이 문서 → (필요할 때만) `Jhp_스펙문서.md`, `CHANGELOG.md` 순으로 읽으세요.
> v8까지의 상세 작업 이력은 이 문서로 압축했고 원문은 git 기록(`git show <커밋>:Jhp_인계문서_v8.md`)에 남아 있습니다.
> 되돌리기 지점: 태그 `jhp-checkpoint-1`~`6`, 커밋 메시지의 `[Q1]`~`[Q7]`, `[A1]`~`[A12]`.

---

## 1. 프로젝트

| 항목 | 값 |
|---|---|
| 목적 | 메이플스토리 재획비 타이머 + OTT/사이트 시청 보조 창 (원본: 인벤 d3vdev "재획플레이어" 재구현) |
| 기술 | .NET 10 (net10.0-windows) WinForms + WebView2, NAudio, System.Speech |
| 솔루션 | `JHP.slnx` — `JHP`(UI) / `JHP.Api`(로직) / `JHP.Asset`(OTT 스크립트) / `JHP.Controls`(커스텀 컨트롤) / `JHP.Tests`(xunit) |
| 경로 / 원격 | `C:\Users\wjgus\source\repos\dlrj0\JHP`, https://github.com/dlrj0/JHP — 작업 브랜치 `test`, 배포 브랜치 `main` |
| 버전 | `JHP/JHP.csproj`의 `<Version>1.1.0</Version>`이 단일 출처. Release 미생성 |
| 게시 | `JHP/Properties/PublishProfiles/win-x64.pubxml` (self-contained). 결과물은 저장소에 넣지 않음 |

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
dotnet build JHP/JHP.csproj                                         # 0 오류가 기준 (경고는 WebView2 참조 충돌 MSB3277 1건)
dotnet test JHP.Tests/JHP.Tests.csproj                              # 단위 57개
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test.ps1 -OutDir <캡처폴더> [-SkipBuild] [-IncludeMenu]
```
- 기준선(2026-10-12): 빌드 0 오류, 단위 57/57, UI 기본 약 57항목 / `-IncludeMenu` 74항목.
- `-IncludeMenu`는 메뉴 팝업이 사용자 화면에 잠깐 보이고 포커스를 건드릴 수 있어, 사용자가 요청할 때만 실행.
- **알려진 불안정**: UI 테스트가 실행마다 다른 항목(숫자 입력, 읽기 전용/잠김 설정 종료, 타이머 색)에서 간헐적으로 실패하고 재실행하면 통과함. 사용자가 PC를 조작하지 않아도 발생 → 코드 회귀가 아니라 타이밍 문제로 추정, **원인 미확정**. 해결안: 스크립트에 구간 선택 옵션(`-Only`) 추가 후 실패 구간만 반복, 불안정한 검사에 대기/재시도 추가.
- `tools/ui-test.ps1`: "alarm controls not found"는 FAIL로 기록, UIA 시간 초과는 SKIP(건수 표시). 컨트롤 이름/구조를 바꾸면 SKIP/FAIL을 확인할 것.
- 사용자의 JHP가 실행 중이면 `bin` DLL이 잠겨 빌드 실패 → `-p:OutDir=<다른 폴더>\`.

## 4. 현재 구현 상태 (요약)

- **UI**: 타이틀바 1줄(▼ 메뉴, 타이머 버튼, 남은 시간 라벨, 볼륨/투명도 NSlider + 숫자 직접 입력, 창 버튼), 가장자리 리사이즈, 더블클릭 최대화, 다크 대화상자/타이틀바.
- **타이머**: 8슬롯 + 커스텀 3슬롯, 좌클릭 시작/정지(정지=초기화), 우클릭 알람창 토글, 실제 경과 시간 기준 계산(절전 복귀 120초 초과 시 재동기화), 겹치면 위쪽 하나만 TTS, 설정창 확인 시 바뀐 항목만 리셋.
- **▼ 메뉴**: 사이트 이동/추가/삭제(✕ 확인), 우클릭 관리(이동·수정·시작 사이트 지정), 항상 위, 테두리 숨김, 알람 중 영상 음소거, 전역 단축키, 알람 설정, 설정 내보내기/가져오기(쿼리 제거).
- **견고성**: 단일 인스턴스(재실행 시 기존 창 복원), 설정 손상/잠김/읽기 전용 대응(`.corrupt` 보관, `%LOCALAPPDATA%\JHP` 대체), 저장 실패 알림, 설정 주소 http(s) 검증, `error.log` 1MB 제한, WebView2 렌더러 복구, TTS 장치 재연결, 업데이트 알림(건너뛰기, 활성 창일 때만 표시).
- **웹**: 새 창 정책(같은 창 이동, 자동 팝업 차단), HTML 전체화면 시 타이틀바 숨김, 이동마다 마지막 주소 저장, 넷플릭스 건너뛰기 감시 스크립트.

## 5. 남은 일

| 항목 | 상태/필요한 것 |
|---|---|
| **넷플릭스 건너뛰기 검증** | 요소 확인됨: `button[data-uia="player-skip-intro"]`(부모 `.watch-video--skip-content`), 크레딧 구간 `next-episode-seamless-button-draining`/`watch-credits-seamless-button`. 감시 스크립트(`MutationObserver`)로 변경했으나 **JHP 안에서 실제로 넘어가는지 미확인** → 사용자 확인 필요. 다음 화 버튼의 클릭 동작 상세도 미확인 |
| 리애니메 | `UserScripts.ReanimeAutoOptions`: 사이트 자체 토글(aria-label Auto Play/Auto Next/Auto Skip Intro & Outro, 켜짐=`text-primary`, 설정은 localStorage `project_r_prefs`)을 최초 1회 켬(`jhp_reanime_opts_done`). 이 사이트는 로드가 `interactive`에서 끝나지 않아 `NavigationCompleted`가 오지 않으므로 `DOMContentLoaded`에서도 주입하고(`InjectJS(domReadyOnly)`), 서버 렌더 버튼은 준비 전엔 클릭이 무시되어 켜진 것을 확인할 때까지 1초 간격으로 재시도한다. JHP(WebView2, CDP로 확인)에서 토글 3개 켜짐·`project_r_prefs` 저장 확인, **영상이 실제로 다음 화로 넘어가는지는 사용자 확인 필요**. 영상은 iframe 안이라 직접 클릭하지 않음 |
| 라프텔 다음화 | `[data-testid="next-episode-button"]`는 검증된 적 없음(1회 클릭 방식 그대로). 로그인/재생 화면 DOM 필요 |
| UI 테스트 안정화 | 3장 참고(구간 선택 옵션 + 대기/재시도) |
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
- 불필요 산출물: Playwright MCP가 만드는 `.playwright-mcp/`는 `.gitignore`에 있음. MCP 브라우저는 JHP의 WebView2와 별개(로그인/DOM 다를 수 있음). MCP 탭 전환은 창 포커스를 가져올 수 있음.

## 7. 원본 대비 메모

- 원본 기능 계승: 사이트 이동/추가/삭제, 좌클릭 시작·우클릭 설정창 토글, TTS는 위쪽 하나만, 항상 위, 테두리 숨김. 원본 소스(`d3vdev/JHP`)는 404라 추적 불가.
- 알람 TTS 이름(원본): 재획비, 경쿠, 경뿌/경쿠/유니온, 유니온 버프, 경쿠, 유니온 버프, 메소 회수, 파운틴. 기본 사이트 7개: 넷플릭스, 라프텔, 웨이브, 티빙, 쿠팡플레이, 디즈니플러스, 유튜브.
- 요구 문서(`Jhp_ui개선요구사항_v4.md` 등)의 항목은 모두 반영됨(타이틀바 1줄·V버튼·타이머 버튼·슬라이더 직접 입력·알람창 리사이즈). SiteForm 리사이즈(선택사항)와 타이머 우클릭 컨텍스트 메뉴(구버전 설계)는 의도적으로 미적용.
