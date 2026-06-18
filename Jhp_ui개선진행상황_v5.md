# JHP UI 개선 — 진행 상황 v5 (이번 세션 인계 문서)

> 새 Claude 대화를 시작할 때 이 파일을 붙여넣으세요.
> `JHP_UI개선지시서_v2.md`, `Jhp_완전통합가이드_v3.md`는 구버전 설계 문서로 실제 코드와 다릅니다 (무시).
> `JHP_UI개선요구사항_v4.md`는 "남은 작업"의 4장(사이트 V버튼), 5-1장(메뉴바 흡수) 설명이 여전히 유효합니다 — 이 부분만 참고하세요. 5-2~5-4장, 6장은 이번 세션에서 작업 완료되어 더 이상 유효하지 않습니다.

---

## ⚠️ 이번 세션 시작 시 발견한 사실

이전 세션에서 "완료했다"고 보고했던 `ControlButton.cs` 렌더링 버그 수정과 `AlarmForm.cs` 리사이즈 작업이, 실제 GitHub `test` 브랜치에는 반영되어 있지 않았습니다 (새 파일 `TimerButton.cs`만 들어가 있었음). 토큰 소진으로 대화가 끊기면서 일부 코드 블록이 사용자에게 전달되지 못했거나 복사 과정에서 누락된 것으로 보입니다.

**따라서 새 세션을 시작하기 전에 반드시 GitHub 코드를 직접 열어서 실제 상태를 재확인하세요.** 사용자가 "적용했다"고 말해도 raw.githubusercontent.com에서 직접 fetch해서 비교하는 것을 권장합니다 (이번에 그렇게 해서 누락을 발견했습니다).

---

## ✅ 이번 세션에서 완료한 작업 (파일 5개, 적용 대기 중)

아래 파일들은 **전체 내용을 통째로 교체**하면 됩니다 (일부 수정 아님).

| 파일 | 변경 내용 |
|------|----------|
| `JHP.Controls/ControlButton.cs` | `OnHandleCreated` 오버라이드 추가 → 초기 렌더링(투명 배경) 버그 수정. `TimerButton.cs`와 동일 패턴 |
| `JHP.Controls/Timerbutton.cs` | (변경 없음, 이전 세션에서 이미 작성된 그대로 사용) |
| `JHP/Form1.Designer.cs` | `lblNextAlarm`, `btnAlarmSettings` 제거 → `timerButton`(TimerButton) 필드로 교체. `sliderVolume`/`sliderOpacity` 90px→170px 확장 + 좌표 재배치. 인라인 숫자 편집용 `tbInlineEdit`(TextBox, 기본 숨김) 추가. `toolTip` 컴포넌트 추가 |
| `JHP/Form1.cs` | `_timerRunning` 필드 추가, `ToggleTimer()`로 좌클릭 시작/정지 토글 구현 (정지 시 다음 시작에서 항상 리셋되므로 "정지=초기화"). `timerButton.RightClick`으로 `OpenAlarmSettings()` 바로 호출. `Timer_Tick`에 `if (!_timerRunning) return;` 가드 추가 (앱 실행 시 자동 시작 안 함). `UpdateNextAlarmLabel()`을 텍스트 라벨 대신 `toolTip.SetToolTip(timerButton, ...)`으로 변경. `BeginInlineEdit`/`CommitInlineEdit`/`InlineEdit_KeyDown` 추가 — 볼륨/투명도 숫자 라벨 클릭 시 직접입력 |
| `JHP/AlarmForm.cs` | `FormBorderStyle.FixedDialog` → `FormBorderStyle.None` + `Form1.cs`와 동일한 `WM_NCHITTEST`/`ReSize` 패턴으로 모서리 8방향 리사이즈 가능. 자체 다크 타이틀바(`_pnlTitle`, 드래그 이동 + `ControlButton` 닫기 버튼) 추가. 기본 폭 420→600. 본문 컨트롤(`_tbAlarmName`, `_tbVolume`, `_tbRate`, 커스텀 알람 입력칸, 확인/취소 버튼)에 `Anchor` 설정 → 창을 늘리면 입력칸/버튼이 따라서 늘어나거나 우측/하단에 붙음. 체크박스 8개 컬럼 폭 94px→135px로 넓혀 텍스트 답답함 완화 |

### 일부러 손대지 않은 것
- `AlarmForm.cs` 내부의 알람 파일명은 여전히 `TextBox`(ComboBox 아님), 볼륨/속도는 여전히 `TrackBar`(NSlider 아님) — v4 문서 6장에서 요구한 범위가 아니었음 (리사이즈만 요청됨)
- `SiteForm.cs`는 그대로 둠 (v4 문서 6장에서 "선택사항"이라 명시됨, 사용자 확인 필요)
- `pnlMenuBar`, `pnlSidebar`, `menuStrip`, `siteList` 등은 전혀 건드리지 않음 — 아래 "남은 작업" 참고

### ⚠️ 빌드/실행 테스트 미완료
이 어시스턴트는 WinForms+WebView2를 빌드할 수 있는 .NET 환경에 접근할 수 없어 (nuget.org 접근 불가) **컴파일 테스트를 하지 못했습니다.** Visual Studio에서 빌드(Ctrl+Shift+B) 후 오류가 나면 다음 세션 Claude에게 오류 메시지를 그대로 붙여넣어 주세요. 특히 확인이 필요한 부분:
- `Form1.Designer.cs`의 좌표값들이 실제 폰트 렌더링에서 컨트롤끼리 겹치지 않는지 (titlebar 폭 1000px 기준으로 계산함, 창을 더 줄이면 `sliderOpacity`와 `btnMinimize` 사이 여유 공간이 줄어들 수 있음 — 필요시 `MinimumSize.Width`를 760에서 더 늘리는 것도 고려)
- `AlarmForm.cs`에서 `ReSize.SetThick()`/`ReSize.GetMousePosition()` 시그니처가 실제 `ReSize.cs`와 일치하는지 (Form1.cs에서 쓰는 것과 동일하게 맞춰서 작성했지만 `ReSize.cs` 원본을 다시 한번 대조해보면 좋음)

---

## 🔜 남은 작업 — 다음 세션에서 진행 (높음 난이도, 사용자 승인 필요)

사용자가 "더 큰 작업"으로 명시한 부분이며, Form1의 구조(타이틀바/메뉴바/사이드바)를 크게 바꾸는 작업입니다. **한 번에 다 하지 말고 아래 두 덩어리로 나눠서 진행하세요.**

### A. 사이트 목록 → V버튼 드롭다운 전환
- `pnlSidebar`(180px 고정 사이드바), `siteList`(SiteListViewControl), `btnAddSite`, `btnRemoveSite` 제거
- 타이틀바 좌측에 V자 드롭다운 버튼 추가 (ContextMenuStrip 추천 — 새 커스텀 팝업 컨트롤보다 구현 리스크 낮음)
- 드롭다운 내용: `Config.Sites` 목록 + 구분선 + "주소 추가" 항목. 사이트 클릭 시 해당 URL로 이동, 현재 보고 있는 사이트엔 체크 표시. 항목 우클릭 또는 × 아이콘으로 삭제. "주소 추가"는 기존 `SiteForm` 재사용
- `SiteListViewControl.cs`를 드롭다운 내부 렌더링에 재활용할지, 아니면 ContextMenuStrip의 `ToolStripMenuItem.Checked`로 단순화할지는 구현 시점에 판단 (후자가 더 단순하고 안전함)

### B. 메뉴바(`pnlMenuBar`) 통째로 제거 → V버튼 드롭다운에 흡수
- `menuAddSite`("사이트 추가")는 A의 "주소 추가"와 중복되므로 통합
- `menuTopmost`("항상 위"), `menuHideBorder`("테두리 숨김")는 V버튼 드롭다운 안에 체크 가능한 항목으로 그대로 이전
- `pnlMenuBar`, `menuStrip` 자체를 Designer.cs에서 제거 → 타이틀바가 한 줄로 줄어듦
- `MenuStrip_ItemClicked`, `ToolStripCommand` enum은 그대로 재사용 가능 (드롭다운이 ContextMenuStrip이면 동일한 `ItemClicked` 패턴 적용 가능)

### 작업 시 주의
- A, B를 합치면 `Form1.Designer.cs`와 `Form1.cs`를 동시에 크게 수정하게 되므로, **반드시 작업 전 사용자에게 "지금 진행해도 되는지" 확인**하고 진행하세요 (사용자가 이전에 "높음 난이도면 코드 작성 전에 먼저 요구해달라"고 명시함)
- 작업 후 `pnlTitleBar`의 V버튼 위치는 기존 `timerButton`(x=54) 앞쪽, 즉 `lblTitle` 우측에 배치하거나 `lblTitle` 자체를 V버튼으로 대체하는 것도 고려 가능 — 사용자에게 배치 선호를 먼저 물어봐도 좋음
- 이 작업이 끝나면 `CHANGELOG.md`에 버전 항목 추가 권장

---

## 📎 참고 — 실제 API 시그니처 (변경 없음, v3 문서에서 재확인됨)

```csharp
Synth.Instance.Ring(string alarmName, int volume);
Synth.Instance.TTS(string text, int volume, int rate);
Synth.Instance.SetVolume(int volume);
Synth.Instance.SetRate(int rate);
Synth.Instance.Stop();

ReSize.GetMousePosition(Form form, Point cursor);   // static class
ReSize.SetThick(ReSize.MousePosition pos);          // → Cursor

Config.Instance.Volume / Rate / Tts / AlarmName / AlarmEnabled[8] / CustomAlarms / TopMost / IsHideWindowBorderOnFocusOut / Opacity
Config.Instance.Sites (List<Site>), Config.Instance.DefaultSite, Config.Instance.Save()
```
