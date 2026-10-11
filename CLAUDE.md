# JHP — Claude 작업 지침

메이플스토리 재획비 타이머 + OTT/사이트 영상 보조 창. .NET 10 WinForms + WebView2. 원본은 인벤 "재획플레이어"(d3vdev).
세션을 시작하면 먼저 아래 문서를 읽으세요.

| 문서 | 용도 |
|------|------|
| `Jhp_인계문서_v8.md` | 작업 원칙(0장), 현재 상태, 함정, **남은 작업(11장)** |
| `Jhp_스펙문서.md` | 원본 기준 기능/UI/설정 명세와 구현 상태 |
| `CHANGELOG.md` | 버전별 변경 (현재 v1.1.0, Release 미생성) |

## 빌드 / 테스트
```
dotnet build JHP/JHP.csproj                                   # 0 오류가 기준
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ui-test.ps1 -OutDir <캡처 폴더>   # UI 자동 테스트 (-SkipBuild 가능)
```
- UI 테스트는 별도 폴더의 인스턴스를 띄우고 창 메시지/UI Automation으로 조작합니다. 캡처는 PrintWindow.
- 테스트 창은 환경 변수 `JHP_OFFSCREEN=1`로 **처음부터 화면 밖(-20000,0)** 에 뜹니다. ▼ 메뉴 팝업은 화면 가장자리에 보이므로 기본은 건너뛰고 `-IncludeMenu`일 때만 검사합니다. 사용자 PC를 다른 사람이 볼 수 있으니, 화면에 창을 띄워야 할 때는 확인 직후 최소화하거나 화면 밖으로 내립니다.
- 사용자가 쓰는 JHP가 실행 중이면 `JHP/bin`의 DLL이 잠겨 빌드가 실패합니다. 이때는 `-p:OutDir=<다른 폴더>\`로 빌드하세요.

## 반드시 지킬 것
1. **사용자의 PC 사용을 방해하지 않고, 화면에 작업 내용이 보이지 않게 합니다.** 실제 마우스 이동/클릭, 키 입력, 포커스 이동, `taskkill /IM JHP.exe`, 사용자 화면을 덮는 최대화 금지. 종료는 자기가 띄운 PID만.
2. **커밋·푸시·Release는 사용자가 요청할 때만** 합니다. (자동 예약 작업은 로컬 커밋까지만)
3. 코드를 바꾸면 **같은 턴에** 인계 문서 11장과 CHANGELOG를 갱신합니다.
4. 첨부/확인되지 않은 파일이나 사이트 요소(셀렉터)는 추측해서 바꾸지 않습니다. 필요한 정보는 사용자에게 요청합니다.
5. 필요한 부분만 수정하고, 파일 삭제 같은 되돌리기 어려운 작업은 먼저 확인합니다.

## 코드 규칙 (실제로 겪은 문제)
- **DPI**: 폼은 `AutoScaleDimensions=(96,96)` + `AutoScaleMode.Dpi`를 **자식 컨트롤을 모두 추가한 뒤** 지정. 코드의 px 상수는 `DeviceDpi / 96f`로 환산(좌표/크기는 96dpi 기준 값).
- **Anchor=Right 금지**(부모 기본 폭 기준으로 계산돼 어긋남). 창 버튼 등은 `LayoutTitleBar()`처럼 직접 배치.
- 커스텀 그리기 컨트롤은 **불투명하게** 칠함(투명 브러시 + 더블버퍼는 검정으로 보임). 호버는 `_hovered`(MouseEnter/Move/Leave)로 추적.
- 메인 창의 리사이즈는 가장자리 패널 + `WM_NCLBUTTONDOWN` 방식(WebView가 가장자리를 덮어 `WM_NCHITTEST`가 폼에 도달하지 않음).
- 설정/알람 파일 경로는 `AppContext.BaseDirectory` 기준(작업 디렉터리 기준 금지).
- 알람 TTS 이름은 원본 항목명(재획비, 경쿠, 경뿌/경쿠/유니온, 유니온 버프, 메소 회수, 파운틴). 겹치면 목록 위쪽 하나만 출력.

## 환경 메모
- Windows + Git Bash/PowerShell. 한글이 든 PowerShell 스크립트는 UTF-8 **BOM** 필요. Git Bash의 `/tmp`와 Windows Python의 `/tmp`는 다른 경로.
- 한글이 들어간 파이썬/셸 한 줄 편집은 이스케이프가 깨지기 쉬우니 Edit 도구 또는 파일로 저장해 실행.
- 원격: `origin` = https://github.com/dlrj0/JHP, 작업 브랜치 `test`(기본 `main`).
