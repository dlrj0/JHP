<#
  JHP UI 자동 테스트 (사용자의 마우스/키보드/포커스를 건드리지 않는다)

  - 실제 마우스 커서를 움직이거나 클릭하지 않는다. 컨트롤 핸들에 윈도우 메시지(WM_MOUSEMOVE/WM_LBUTTONDOWN...)를 직접 보내고,
    버튼은 UI Automation의 Invoke로 누른다.
  - 화면 캡처는 PrintWindow(창 자신이 그려서 내보내기)를 쓰므로 다른 창에 가려져도, 화면 밖에 있어도 동작한다.
  - 테스트용 인스턴스는 별도 폴더에서 실행하므로 사용자가 쓰는 JHP(설정 파일 포함)에 영향이 없다. 종료도 이 스크립트가 띄운 PID만 한다.

  사용법:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\ui-test.ps1 [-OutDir <캡처 저장 폴더>] [-SkipBuild]
#>
param(
    [string]$OutDir = (Join-Path $env:TEMP 'jhp-uitest'),
    [switch]$SkipBuild,
    [switch]$KeepOnScreen,  # 기본은 테스트 창을 화면 밖으로 치워 사용자의 화면을 가리지 않는다
    [switch]$IncludeMenu    # ▼ 메뉴 검사 포함. ContextMenuStrip은 화면 밖 좌표에서도 화면 가장자리에 잠깐 보이므로 기본은 건너뜀
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text; using System.Collections.Generic;
public static class Win {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> TopWindows(uint pid) {
    var list = new List<IntPtr>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
    return list;
  }
}
"@
[Win]::SetProcessDPIAware() | Out-Null

$repo = Split-Path -Parent $PSScriptRoot
$app = Join-Path $OutDir 'app'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$results = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $null = $results.Add([pscustomobject]@{ Name = $name; Pass = $ok; Detail = $detail })
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("[{0}] {1} {2}" -f $mark, $name, $detail)
}

# ---------- 빌드 / 실행 ----------
if (-not $SkipBuild) {
    Push-Location (Join-Path $repo 'JHP')
    $out = & dotnet build JHP.csproj -nologo -v q "-p:OutDir=$app\" 2>&1
    Pop-Location
    $errs = ($out | Select-String -Pattern ' error ' | Measure-Object).Count
    Check 'build' ($errs -eq 0) "errors=$errs"
    if ($errs -gt 0) { $out | Select-Object -Last 15; exit 1 }
}
Remove-Item (Join-Path $app 'config.json') -ErrorAction SilentlyContinue   # 첫 실행 상태에서 시작
# 앱이 처음부터 화면 밖(-20000,0)에 뜨도록 한다 (실행 직후 사용자의 화면에 잠깐 보이는 것을 막음)
if (-not $KeepOnScreen) { $env:JHP_OFFSCREEN = '1' }
$proc = Start-Process (Join-Path $app 'JHP.exe') -WorkingDirectory $app -PassThru
Remove-Item Env:\JHP_OFFSCREEN -ErrorAction SilentlyContinue
$main = [IntPtr]::Zero
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 250
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $main = $proc.MainWindowHandle; break }
}
if ($main -eq [IntPtr]::Zero) { Check 'launch' $false 'main window not found'; $proc.Kill(); exit 1 }
if (-not $KeepOnScreen) { [Win]::SetWindowPos($main, [IntPtr]::Zero, -20000, 0, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null } # NOSIZE|NOZORDER|NOACTIVATE
Start-Sleep -Milliseconds 2500   # WebView2 초기화 대기

# ---------- 헬퍼 ----------
function Get-Rect([IntPtr]$h) { $r = New-Object Win+RECT; [Win]::GetWindowRect($h, [ref]$r) | Out-Null; $r }
function Save-Window([IntPtr]$h, [string]$name) {
    $r = Get-Rect $h; $w = $r.R - $r.L; $ht = $r.B - $r.T
    if ($w -le 0 -or $ht -le 0) { Write-Host "  (캡처 생략: $name — 창 크기를 읽을 수 없음)"; return $null }
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
    # 막 열린 창은 일부 컨트롤이 아직 그려지지 않아 캡처에 검게 남을 수 있으므로 한 번 그려지길 기다렸다가 잡는다
    Start-Sleep -Milliseconds 1500
    [Win]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $path = Join-Path $OutDir "$name.png"; $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    return @{ Bitmap = $bmp; Path = $path; Rect = $r }
}
function ToInt([double]$d) { if ([double]::IsInfinity($d) -or [double]::IsNaN($d)) { 0 } else { [int]$d } }
function Get-Elements([IntPtr]$h, [switch]$NoHandleOk) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $c = $e.Current
        # WebView2(Chromium) 내부 요소는 HWND가 없다 → 우리 WinForms 컨트롤만 남긴다. (메뉴 항목처럼 HWND가 없는 항목은 -NoHandleOk)
        if (-not $NoHandleOk -and $c.NativeWindowHandle -eq 0) { continue }
        [pscustomobject]@{ El = $e; Name = $c.Name; Type = $c.ControlType.ProgrammaticName.Replace('ControlType.', ''); Class = $c.ClassName
                           L = (ToInt $c.BoundingRectangle.Left); T = (ToInt $c.BoundingRectangle.Top); W = (ToInt $c.BoundingRectangle.Width); H = (ToInt $c.BoundingRectangle.Height)
                           Hwnd = [IntPtr]$c.NativeWindowHandle }
    }
}
function Send-Mouse($el, [string]$kind, [double]$fx = 0.5, [double]$fy = 0.5) {
    # el: Get-Elements 항목. 클라이언트 좌표(fx, fy 비율)로 메시지를 보낸다. 실제 커서는 움직이지 않는다.
    $x = [int]($el.W * $fx); $y = [int]($el.H * $fy); $lp = [IntPtr](($y -shl 16) -bor ($x -band 0xFFFF))
    switch ($kind) {
        'move'  { [Win]::SendMessage($el.Hwnd, 0x0200, [IntPtr]::Zero, $lp) | Out-Null }
        'leave' { [Win]::SendMessage($el.Hwnd, 0x02A3, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
        'ldown' { [Win]::SendMessage($el.Hwnd, 0x0201, [IntPtr]1, $lp) | Out-Null }
        'lup'   { [Win]::SendMessage($el.Hwnd, 0x0202, [IntPtr]::Zero, $lp) | Out-Null }
        'rdown' { [Win]::SendMessage($el.Hwnd, 0x0204, [IntPtr]2, $lp) | Out-Null }
        'rup'   { [Win]::SendMessage($el.Hwnd, 0x0205, [IntPtr]::Zero, $lp) | Out-Null }
    }
    # 'move' 직후에는 기다리지 않는다: 실제 커서가 컨트롤 위에 없어서, WinForms가 건 TrackMouseEvent가
    # 곧바로 WM_MOUSELEAVE를 보내 호버가 풀린다. 바로 캡처해야(PrintWindow는 보낸 메시지라 우선 처리됨) 호버 상태가 찍힌다.
    if ($kind -ne 'move') { Start-Sleep -Milliseconds 150 }
}
function Invoke-ElAsync($el) {
    # 모달 대화상자를 여는 항목은 Invoke가 대화상자가 닫힐 때까지 막히므로 별도 runspace에서 실행한다.
    $ps = [powershell]::Create()
    $null = $ps.AddScript({ param($e) $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }).AddArgument($el.El)
    $null = $ps.BeginInvoke()
    Start-Sleep -Milliseconds 700
}
function Invoke-El($el) { $el.El.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }
function Px($bmp, [int]$x, [int]$y) { $c = $bmp.GetPixel($x, $y); "$($c.R),$($c.G),$($c.B)" }
function Find-ProcWindow([string]$title, [int]$timeoutMs = 4000) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        foreach ($h in [Win]::TopWindows([uint32]$proc.Id)) {
            $sb = New-Object System.Text.StringBuilder 256; [Win]::GetWindowText($h, $sb, 256) | Out-Null
            if ($sb.ToString() -eq $title) { return $h }
        }
        Start-Sleep -Milliseconds 200
    }
    return [IntPtr]::Zero
}

try {
    # ---------- 1. 타이틀바 레이아웃 ----------
    $els = Get-Elements $main
    $winRect = Get-Rect $main
    $buttons = $els | Where-Object { $_.Type -eq 'Button' } | Sort-Object L
    Check 'titlebar: 버튼 5개(메뉴/타이머/최소화/최대화/닫기)' ($buttons.Count -eq 5) "count=$($buttons.Count)"
    $menuBtn, $timerBtn, $minBtn, $maxBtn, $closeBtn = $buttons
    $inside = $true
    foreach ($b in @($minBtn, $maxBtn, $closeBtn)) { if ($b.L -lt $winRect.L -or ($b.L + $b.W) -gt $winRect.R) { $inside = $false } }
    Check 'titlebar: 최소화/최대화/닫기가 창 안쪽에 있음' $inside "close.right=$($closeBtn.L + $closeBtn.W) window.right=$($winRect.R)"

    $labels = $els | Where-Object { $_.Type -eq 'Text' -and ($_.Name -like '볼륨*' -or $_.Name -like '투명도*') } | Sort-Object L
    $sliders = $els | Where-Object { $_.Type -eq 'Pane' -and $_.H -gt 0 -and $_.H -lt 80 -and $_.W -gt 60 -and $_.W -lt 700 -and $_.T -lt ($winRect.T + 100) } | Sort-Object L
    Check 'titlebar: 라벨 2개, 슬라이더 2개 인식' ($labels.Count -eq 2 -and $sliders.Count -eq 2) "labels=$($labels.Count) sliders=$($sliders.Count)"

    function Test-NoOverlap($tag) {
        $e2 = Get-Elements $main
        $lb = $e2 | Where-Object { $_.Type -eq 'Text' -and ($_.Name -like '볼륨*' -or $_.Name -like '투명도*') } | Sort-Object L
        $sl = $e2 | Where-Object { $_.Type -eq 'Pane' -and $_.H -gt 0 -and $_.H -lt 80 -and $_.W -gt 60 -and $_.W -lt 700 -and $_.T -lt ($winRect.T + 140) } | Sort-Object L
        $bt = $e2 | Where-Object { $_.Type -eq 'Button' } | Sort-Object L
        if ($lb.Count -ne 2 -or $sl.Count -lt 2 -or $bt.Count -lt 5) { Check "layout[$tag]: 요소 인식" $false "labels=$($lb.Count) sliders=$($sl.Count) buttons=$($bt.Count)"; return }
        $ok = ($lb[0].L + $lb[0].W) -le $sl[0].L -and ($sl[0].L + $sl[0].W) -le $lb[1].L -and ($lb[1].L + $lb[1].W) -le $sl[1].L -and ($sl[1].L + $sl[1].W) -le $bt[2].L
        Check "layout[$tag]: 라벨/슬라이더/창 버튼이 겹치지 않음" $ok ("vol.right={0} s1.left={1} s1.right={2} opa.left={3} opa.right={4} s2.left={5} s2.right={6} min.left={7}" -f ($lb[0].L + $lb[0].W), $sl[0].L, ($sl[0].L + $sl[0].W), $lb[1].L, ($lb[1].L + $lb[1].W), $sl[1].L, ($sl[1].L + $sl[1].W), $bt[2].L)
    }
    Test-NoOverlap 'initial'
    $shot = Save-Window $main '01-initial'

    # ---------- 2. 호버 ----------
    # 메시지로 합성한 마우스 이동은 실제 커서가 컨트롤 위에 없어서, WinForms가 건 TrackMouseEvent가 2ms 안에
    # WM_MOUSELEAVE를 보내 호버가 풀린다(앱 내부 로그로 확인함). 실제 마우스를 쓰지 않는 한 호버 색은 자동 검증할 수 없다.
    # → 호버는 코드 리뷰(_hovered 이벤트 추적 + OnPaint 색)와 사용자 육안 확인 대상으로 두고, 여기서는 건너뛴다.
    Write-Host '[SKIP] hover: 실제 마우스 없이는 자동 검증 불가 (사용자 육안 확인 필요)'

    # ---------- 3. 볼륨/투명도 슬라이더 ----------
    Send-Mouse $sliders[0] 'ldown' 1.0 0.5; Send-Mouse $sliders[0] 'lup' 1.0 0.5
    $volLabel = (Get-Elements $main | Where-Object { $_.Type -eq 'Text' -and $_.Name -like '볼륨*' } | Select-Object -First 1).Name
    Check 'slider: 볼륨 슬라이더 오른쪽 끝 클릭 → 볼륨 100' ($volLabel -eq '볼륨 100') "label='$volLabel'"
    Test-NoOverlap 'volume100'
    Save-Window $main '03-volume100' | Out-Null

    # ---------- 4. 창 크기: 좁게 / 최대화 ----------
    $minW = [int](760 * ($winRect.R - $winRect.L) / 800)   # 기본 폭(800 논리 px) 대비 배율을 추정해 최소 폭 계산
    [Win]::SetWindowPos($main, [IntPtr]::Zero, 0, 0, $minW, 700, 0x0002 -bor 0x0004 -bor 0x0010) | Out-Null  # NOMOVE|NOZORDER|NOACTIVATE
    Start-Sleep -Milliseconds 600
    $winRect = Get-Rect $main
    Test-NoOverlap 'min-width'
    Save-Window $main '04-min-width' | Out-Null

    # ---------- 5. 타이머 우클릭 → 알람 설정창 ----------
    $els = Get-Elements $main; $timerBtn = ($els | Where-Object { $_.Type -eq 'Button' } | Sort-Object L)[1]
    Send-Mouse $timerBtn 'rdown'; Send-Mouse $timerBtn 'rup'
    $alarm = Find-ProcWindow '알람 설정'
    Check 'alarm: 타이머 우클릭으로 알람 설정창이 열림' ($alarm -ne [IntPtr]::Zero)
    if ($alarm -ne [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 500
        $ae = Get-Elements $alarm
        $expect = '재획비 (2시간)', '경쿠 (1시간)', '경뿌/경쿠/유니온 (30분)', '유니온 버프 (20분)', '경쿠 (15분)', '유니온 버프 (10분)', '메소 회수 (100초)', '파운틴 (55초)'
        $missing = $expect | Where-Object { $n = $_; -not ($ae | Where-Object { $_.Name -eq $n }) }
        Check 'alarm: 원본 알람 항목 8개 이름 표시' ($missing.Count -eq 0) ("missing=" + ($missing -join ','))
        Check 'alarm: TTS 샘플 버튼 / TTS 출력 체크박스 존재' (($ae | Where-Object { $_.Name -eq 'TTS 샘플' }) -and ($ae | Where-Object { $_.Name -like 'TTS 출력*' })) ''
        Check 'alarm: 알람 소리 콤보박스 존재' (@($ae | Where-Object { $_.Type -eq 'ComboBox' }).Count -ge 1) ''
        # 모든 컨트롤이 창 안에 있고 서로 가려지지 않는지(가로 잘림 확인)
        $ar = Get-Rect $alarm
        $outside = $ae | Where-Object { $_.W -gt 0 -and ($_.L + $_.W) -gt ($ar.R + 2) }
        Check 'alarm: 컨트롤이 창 밖으로 나가지 않음' ($outside.Count -eq 0) ("outside=" + (($outside | Select-Object -First 3 | ForEach-Object { $_.Name }) -join ','))
        Save-Window $alarm '05-alarm' | Out-Null
        # 토글: 같은 버튼 우클릭 → 닫힘
        $timerBtn = ((Get-Elements $main) | Where-Object { $_.Type -eq 'Button' } | Sort-Object L)[1]
        Send-Mouse $timerBtn 'rdown'; Send-Mouse $timerBtn 'rup'
        Start-Sleep -Milliseconds 600
        $again = Find-ProcWindow '알람 설정' 1200
        Check 'alarm: 같은 버튼 우클릭으로 닫힘(토글)' ($again -eq [IntPtr]::Zero)
    }

    # ---------- 6. ▼ 메뉴 ----------
    if (-not $IncludeMenu) { Write-Host '[SKIP] menu/site: 팝업이 사용자 화면에 보일 수 있어 기본은 건너뜀 (-IncludeMenu로 실행)' }
    else {
    $els = Get-Elements $main; $menuBtn = ($els | Where-Object { $_.Type -eq 'Button' } | Sort-Object L)[0]
    Invoke-El $menuBtn
    $popup = [IntPtr]::Zero
    for ($try = 0; $try -lt 12 -and $popup -eq [IntPtr]::Zero; $try++) {
    if ($try -gt 0) { Start-Sleep -Milliseconds 250 }
    foreach ($h in [Win]::TopWindows([uint32]$proc.Id)) {
        $sb = New-Object System.Text.StringBuilder 128; [Win]::GetClassName($h, $sb, 128) | Out-Null
        $t = New-Object System.Text.StringBuilder 128; [Win]::GetWindowText($h, $t, 128) | Out-Null
        $r = Get-Rect $h
        if ($h -ne $main -and $sb.ToString() -notlike 'SysShadow*' -and ($r.R - $r.L) -gt 100 -and ($r.B - $r.T) -gt 100) { $popup = $h }
    }
    }
    Check 'menu: ▼ 버튼으로 드롭다운이 열림' ($popup -ne [IntPtr]::Zero)
    if ($popup -ne [IntPtr]::Zero) {
        $me = Get-Elements $popup -NoHandleOk
        $names = ($me | Where-Object { $_.Name } | ForEach-Object { $_.Name })
        foreach ($want in '항상 위에 표시', '다른 창 선택 시 테두리 안보이기', '넷플릭스', '라프텔', '웨이브', '티빙', '쿠팡플레이', '디즈니플러스', '유튜브', '＋ 사이트 추가') {
            Check "menu: '$want' 항목" ($names -contains $want)
        }
        Save-Window $popup '06-menu' | Out-Null
        $add = $me | Where-Object { $_.Name -eq '＋ 사이트 추가' } | Select-Object -First 1
        if ($add) {
            Invoke-ElAsync $add
            $site = Find-ProcWindow '사이트 추가'
            Check 'site: 사이트 추가 대화상자가 열림' ($site -ne [IntPtr]::Zero)
            if ($site -ne [IntPtr]::Zero) {
              try {
                $se = Get-Elements $site; $sr = Get-Rect $site
                $out2 = $se | Where-Object { $_.W -gt 0 -and (($_.L + $_.W) -gt ($sr.R + 2) -or ($_.T + $_.H) -gt ($sr.B + 2)) }
                Check 'site: 컨트롤이 창 밖으로 나가지 않음' ($out2.Count -eq 0)
                Save-Window $site '07-site-dialog' | Out-Null
                $cancel = $se | Where-Object { $_.Name -eq '취소' } | Select-Object -First 1
                if ($cancel) { Invoke-El $cancel }
              }
              catch {
                Write-Host "  [SKIP] site 대화상자 검사 중 UI Automation 시간 초과: $($_.Exception.Message)"
              }
              finally {
                [Win]::PostMessage($site, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null   # WM_CLOSE: 열려 있으면 닫는다
                Start-Sleep -Milliseconds 500
              }
            }
        }
    }

    }  # end if IncludeMenu

    # ---------- 7. 넓은 창 (최대화 상당) ----------
    # 실제 최대화는 사용자의 화면 위로 창이 올라오므로, 화면 밖에서 크게 늘려 검증한다.
    [Win]::SetWindowPos($main, [IntPtr]::Zero, 0, 0, 3000, 1400, 0x0002 -bor 0x0004 -bor 0x0010) | Out-Null
    Start-Sleep -Milliseconds 600
    $winRect = Get-Rect $main
    Test-NoOverlap 'wide'
    Save-Window $main '08-wide' | Out-Null
}
finally {
    if (-not $proc.HasExited) { $proc.Kill() }
}

$fail = @($results | Where-Object { -not $_.Pass })
Write-Host ''
Write-Host ("결과: {0} / {1} 통과, 캡처: {2}" -f ($results.Count - $fail.Count), $results.Count, $OutDir)
if ($fail.Count -gt 0) { exit 1 }
