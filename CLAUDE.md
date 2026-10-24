# JHP — Claude 작업 지침

메이플스토리 재획비 타이머 + OTT/사이트 영상 보조 창. .NET 10 WinForms + WebView2. 원본은 인벤 "재획플레이어"(d3vdev).
세션을 시작하면 먼저 아래 문서를 읽으세요.

| 문서 | 용도 |
|------|------|
| `Jhp_인계문서_v9.md` | 작업 원칙(2장), 빌드/테스트 기준선, 현재 상태, 코드 함정, **남은 일(5장)** — 압축본 |
| `Jhp_스펙문서.md` | 원본 기준 기능/UI/설정 명세와 구현 상태 |
| `CHANGELOG.md` | 버전별 변경 (현재 v1.1.0, Release 미생성) |

## 빌드 / 테스트
```
dotnet build JHP/JHP.csproj                                   # 0 오류가 기준
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ui-test.ps1 -OutDir <캡처 폴더>   # UI 자동 테스트 (-SkipBuild 가능)
```
- UI 테스트는 별도 폴더의 인스턴스를 띄우고 창 메시지/UI Automation으로 조작합니다. 캡처는 PrintWindow.
- 테스트 창은 기본적으로 환경 변수 `JHP_OFFSCREEN=1`로 화면 밖(-20000,0)에 뜹니다(편의 설정). ▼ 메뉴 팝업은 화면 가장자리에 잠깐 보이므로 기본은 건너뛰고 `-IncludeMenu`일 때만 검사합니다. 작업 중 창이 잠깐 뜨는 것은 괜찮지만, **작업/확인이 끝나면 반드시 최소화하거나 화면에서 치웁니다**(다른 사람이 PC 화면을 볼 수 있음).
- **포커스·화면 노출 측정(필수 확인)**: `tools/ui-test.ps1` 맨 끝의 `focus:` 검사가 `FocusWatch`(40ms 폴링, 읽기 전용)로 "테스트가 띄운 JHP pid가 포그라운드가 되었는지 / 가상 화면 안에 보이는 창이 있었는지"를 시각과 함께 기록한다. 사용자가 다른 창을 쓰는 것은 기록하지 않는다(포그라운드가 테스트 pid일 때만). 테스트나 테스트용 코드를 바꾼 뒤에는 이 검사를 꼭 본다. 다른 프로세스의 창 제목은 읽지 않는다.
- **창은 만들어지는 순간부터 화면 밖**: `JHP_OFFSCREEN=1`이면 `Program.TestOffscreen`. 앱이 직접 처리한다 — `Form1` 생성자에서 `StartPosition=Manual`+(-20000,0), 알람 설정창은 `Show` 전에 (-20000,0)에 두고 화면 안 보정(Clamp)을 건너뜀, 최대화 복원·저장 실패 툴팁·`webView.Focus()`(`FocusWeb()`)·다른 인스턴스 `Activate()`는 건너뜀. 화면 밖 판정은 `GetWindowRect`(Windows가 -20000을 약 -10000으로 보정해 보고하므로 L<-10000 기준)로, `window.screenX`나 브라우저가 알려주는 창 경계는 믿지 않는다. 최소화는 WebView2 타이머·영상이 멈추므로 쓰지 않는다. 새 창/대화상자/툴팁/자동 최대화·전체화면 경로를 추가하면 테스트 모드에서 화면 안으로 올라오지 않는지 `FocusWatch`로 확인한다.
- **포커스를 뺏은 실제 원인과 해결(2026-10-24 측정, 이후 `focus:` 검사 0건 유지)**: ① UIA `Toggle`/`SetValue`는 WinForms가 컨트롤을 선택하면서 알람 설정창을 활성화한다 → 테스트는 `BM_CLICK`(체크박스/확인/취소), `WM_SETTEXT`(입력 칸), `WM_LBUTTONDOWN/UP`(직접 그린 컨트롤)로 조작한다(`Invoke-El`/`Set-Toggle`/`Set-EditValue`). UIA는 컨트롤을 찾고 읽는 데만 쓴다. ② 창 생성 순간에 포그라운드가 되는 것은 `ShowWithoutActivation`만으로 막히지 않아 `WS_EX_NOACTIVATE`(`CreateParams`)가 필요하다(Form1/AlarmForm/SiteForm/DarkMessageBox, 테스트 모드만). ③ 알람 설정창이 화면 안으로 보정(Clamp)되던 것, 기본 위치에 잠깐 뜨던 것, 호버/저장 실패 툴팁(화면 가장자리로 밀려 올라옴), 최대화 복원은 테스트 모드에서 건너뜀. 새 테스트를 추가할 때 UIA `Invoke`/`Toggle`/`SetValue`를 쓰지 말고, 쓰고 나면 `focus:` 검사 출력(시각+창 제목)으로 어떤 동작이 원인인지 가린다.
- **WebView2 안쪽을 조작해야 할 때**(다른 프로젝트 AniShelf에서 쓰는 방법, JHP에서는 아직 안 씀): OS 입력 대신 CDP(`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=<포트> --mute-audio`, 별도 `WEBVIEW2_USER_DATA_FOLDER`)로 `Runtime.evaluate`/`Input.dispatchMouseEvent`/`Input.dispatchKeyEvent`를 보낸다. 포커스가 필요 없어 사용자가 키보드·마우스를 써도 영향이 없다. 텍스트 입력은 `HTMLInputElement.prototype`의 value 세터 + `input` 이벤트.
- 금지: `SendKeys`/`SendInput`/`keybd_event`/`mouse_event`/`SetForegroundWindow`/`AppActivate`/`AttachThreadInput`, 이름으로 프로세스 종료(자기가 띄운 PID만).
- 단위 테스트: `dotnet test JHP.Tests/JHP.Tests.csproj` (57개; 순수 로직: AlarmScheduler/Config/Site·SiteListOps/UpdateChecker/DarkMessageBox)
- 사용자가 쓰는 JHP가 실행 중이면 `JHP/bin`의 DLL이 잠겨 빌드가 실패합니다. 이때는 `-p:OutDir=<다른 폴더>\`로 빌드하세요.

- 사용자가 직접 실행할 때: 저장소 루트의 `run-jhp.bat`(게시 후 실행, `fast` 인자는 게시 생략). 게시 폴더는 루트의 `app/`(gitignore, 실행하면 cache·config.json이 생기므로 배포 때는 이 둘을 빼고 묶는다). `.bat`은 ASCII만 쓴다.

## 반드시 지킬 것
1. **사용자의 PC 사용을 방해하지 않고, 작업이 끝나면 창을 화면에서 치웁니다**(최소화/화면 밖/내가 띄운 프로세스 종료). 작업 중 창이 잠깐 보이는 것은 허용. 실제 마우스 이동/클릭, 키 입력, 포커스 이동, `taskkill /IM JHP.exe`, 사용자 화면을 덮는 최대화 금지. 종료는 자기가 띄운 PID만.
2. **커밋은 변경 단위로, 푸시는 조건부로만** 합니다. 사용자가 "진짜 문제가 없다면 푸시까지 해도 된다"고 허용했으므로(2026-10-12), 빌드 0 오류 + `tools/ui-test.ps1` 전체 통과 + 단위 테스트 통과 + 알려진 회귀 없음일 때만 `git push origin test`(test 브랜치만). `main` 병합, Release/태그 생성·푸시, 강제 푸시는 사용자가 요청할 때만 합니다.
3. 코드를 바꾸면 **같은 턴에** 인계 문서(v9)와 CHANGELOG를 갱신합니다.
4. 첨부/확인되지 않은 파일이나 사이트 요소(셀렉터)는 추측해서 바꾸지 않습니다. 필요한 정보는 사용자에게 요청합니다.
5. 필요한 부분만 수정하고, 파일 삭제 같은 되돌리기 어려운 작업은 먼저 확인합니다.
6. **게임과 함께 써도 제재 사유가 되지 않아야 합니다(사용자 요구).** 이 앱은 메이플 사냥 중 옆에 띄우는 보조 창입니다. 게임 프로세스/창을 읽거나 건드리는 기능(메모리 읽기·쓰기, 프로세스/창 열거, 키 입력 합성, 키보드·마우스 후킹, 화면 캡처, DLL 주입, 게임 창 조작)은 추가하지 않습니다. 전역 단축키는 `RegisterHotKey`(옵트인, 기본 꺼짐)만 사용하고, 사용자가 게임 키와 겹치는 조합을 고르지 않도록 주의합니다.

## 코드 규칙 (실제로 겪은 문제)
- **DPI**: 폼은 `AutoScaleDimensions=(96,96)` + `AutoScaleMode.Dpi`를 **자식 컨트롤을 모두 추가한 뒤** 지정. 코드의 px 상수는 `DeviceDpi / 96f`로 환산(좌표/크기는 96dpi 기준 값).
- **Anchor=Right 금지**(부모 기본 폭 기준으로 계산돼 어긋남). 창 버튼 등은 `LayoutTitleBar()`처럼 직접 배치.
- 커스텀 그리기 컨트롤은 **불투명하게** 칠함(투명 브러시 + 더블버퍼는 검정으로 보임). 호버는 `_hovered`(MouseEnter/Move/Leave)로 추적.
- 메인 창의 리사이즈는 가장자리 패널 + `WM_NCLBUTTONDOWN` 방식(WebView가 가장자리를 덮어 `WM_NCHITTEST`가 폼에 도달하지 않음).
- 설정·캐시·error.log는 `Config.DataDirectory`(exe 폴더, 쓰기 불가 시 `%LOCALAPPDATA%\JHP`), `alarm/`은 `AppContext.BaseDirectory` 기준(작업 디렉터리 기준 금지).
- 알람 TTS 이름은 원본 항목명(재획비, 경쿠, 경뿌/경쿠/유니온, 유니온 버프, 메소 회수, 파운틴). 겹치면 목록 위쪽 하나만 출력.

## 환경 메모
- Windows + Git Bash/PowerShell. 한글이 든 PowerShell 스크립트는 UTF-8 **BOM** 필요. Git Bash의 `/tmp`와 Windows Python의 `/tmp`는 다른 경로.
- 한글이 들어간 파이썬/셸 한 줄 편집은 이스케이프가 깨지기 쉬우니 Edit 도구 또는 파일로 저장해 실행.
- 원격: `origin` = https://github.com/dlrj0/JHP, 작업 브랜치 `test`(기본 `main`).
