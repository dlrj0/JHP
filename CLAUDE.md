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
- 단위 테스트: `dotnet test JHP.Tests/JHP.Tests.csproj` (57개; 순수 로직: AlarmScheduler/Config/Site·SiteListOps/UpdateChecker/DarkMessageBox)
- 사용자가 쓰는 JHP가 실행 중이면 `JHP/bin`의 DLL이 잠겨 빌드가 실패합니다. 이때는 `-p:OutDir=<다른 폴더>\`로 빌드하세요.

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
