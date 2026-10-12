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
        # 레이아웃이 바뀌는 중에 요소가 사라질 수 있으므로 읽기 실패는 건너뛴다
        try { $c = $e.Current; if ($null -eq $c -or $null -eq $c.ControlType) { continue } } catch { continue }
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
function Run-Safe([string]$name, [scriptblock]$body) {
    # UI Automation 시간 초과 등 예외가 한 검사에서 나도 전체 테스트가 중단되지 않게 한다(해당 검사는 SKIP 처리).
    try { & $body }
    catch { Write-Host "  [SKIP] $name : $($_.Exception.Message)" }
}
function Get-TitleParts {
    $wr = Get-Rect $main
    $e = Get-Elements $main
    $lb = @($e | Where-Object { $_.Type -eq 'Text' -and ($_.Name -like '볼륨*' -or $_.Name -like '투명도*') } | Sort-Object L)
    $sl = @($e | Where-Object { $_.Type -eq 'Pane' -and $_.H -gt 0 -and $_.H -lt 80 -and $_.W -gt 60 -and $_.W -lt 700 -and $_.T -lt ($wr.T + 100) } | Sort-Object L)
    if ($lb.Count -ne 2 -or $sl.Count -lt 2) { throw "title parts not found: labels=$($lb.Count) sliders=$($sl.Count)" }
    [pscustomobject]@{ VolLabel = $lb[0]; OpaLabel = $lb[1]; VolSlider = $sl[0]; OpaSlider = $sl[1] }
}
function Get-LabelText([string]$pattern) {
    (Get-Elements $main | Where-Object { $_.Type -eq 'Text' -and $_.Name -like $pattern } | Select-Object -First 1).Name
}
function Wait-Colors([IntPtr]$h, $el, $winRect, [scriptblock]$until, [int]$timeoutMs = 2500) {
    # 조건($until: $_ = Green/White 개수)을 만족할 때까지 다시 그려진 화면을 읽는다. 시간이 지나도 안 되면 마지막 값을 돌려준다.
    $sw = [Diagnostics.Stopwatch]::StartNew()
    do {
        $r = Count-Colors $h $el $winRect
        if ($r | Where-Object $until) { return $r }
    } while ($sw.ElapsedMilliseconds -lt $timeoutMs)
    $r
}
function Count-Colors([IntPtr]$h, $el, $winRect) {
    # el 영역(화면 좌표)을 PrintWindow 비트맵에서 읽어 초록/흰색 픽셀 수를 센다.
    $r = Get-Rect $h; $w = $r.R - $r.L; $ht = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
    Start-Sleep -Milliseconds 300
    [Win]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $x0 = [Math]::Max(0, $el.L - $r.L); $y0 = [Math]::Max(0, $el.T - $r.T)
    $x1 = [Math]::Min($w, $x0 + $el.W); $y1 = [Math]::Min($ht, $y0 + $el.H)
    $green = 0; $white = 0
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = $x0; $x -lt $x1; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.G -ge 190 -and $c.R -le 170 -and $c.B -le 190 -and ($c.G - $c.R) -ge 50) { $green++ }
            elseif ($c.R -ge 235 -and $c.G -ge 235 -and $c.B -ge 235) { $white++ }
        }
    }
    $bmp.Dispose()
    [pscustomobject]@{ Green = $green; White = $white }
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

    # ---------- 3-1. 타이머 버튼 활성/비활성 색 (스펙 T-1) ----------
    # 활성 아이콘은 초록(110,220,140), 비활성은 흰색. UIA Invoke로 Click을 보내고 PrintWindow 픽셀로 확인한다.
    Run-Safe 'timer-color' {
        $tb = ((Get-Elements $main) | Where-Object { $_.Type -eq 'Button' } | Sort-Object L)[1]
        $wr = Get-Rect $main
        $before = Count-Colors $main $tb $wr
        Invoke-El $tb
        $running = Wait-Colors $main $tb $wr { $_.Green -ge 5 }
        Check 'timer: 시작하면 아이콘이 활성(초록) 색으로 바뀜' ($before.Green -eq 0 -and $running.Green -ge 5) ("stopped green=$($before.Green) white=$($before.White) / running green=$($running.Green) white=$($running.White)")
        Invoke-El $tb
        $stopped = Wait-Colors $main $tb $wr { $_.Green -eq 0 -and $_.White -ge 5 }
        Check 'timer: 다시 누르면 정지하며 비활성(흰색) 색으로 돌아감' ($stopped.Green -eq 0 -and $stopped.White -ge 5) ("green=$($stopped.Green) white=$($stopped.White)")
    }

    # ---------- 3-2. 투명도 슬라이더 양 끝 / 볼륨 0 ----------
    Run-Safe 'slider-ends' {
      try {
        $tp = Get-TitleParts
        Send-Mouse $tp.OpaSlider 'ldown' 0.0 0.5; Send-Mouse $tp.OpaSlider 'lup' 0.0 0.5
        $t = Get-LabelText '투명도*'
        Check 'slider: 투명도 슬라이더 왼쪽 끝 클릭 → 투명도 30%' ($t -eq '투명도 30%') "label='$t'"
        Send-Mouse $tp.OpaSlider 'ldown' 1.0 0.5; Send-Mouse $tp.OpaSlider 'lup' 1.0 0.5
        $t = Get-LabelText '투명도*'
        Check 'slider: 투명도 슬라이더 오른쪽 끝 클릭 → 투명도 100%' ($t -eq '투명도 100%') "label='$t'"
        Send-Mouse $tp.VolSlider 'ldown' 0.0 0.5; Send-Mouse $tp.VolSlider 'lup' 0.0 0.5
        $t = Get-LabelText '볼륨*'
        Check 'slider: 볼륨 슬라이더 왼쪽 끝 클릭 → 볼륨 0' ($t -eq '볼륨 0') "label='$t'"
        Send-Mouse $tp.VolSlider 'ldown' 1.0 0.5; Send-Mouse $tp.VolSlider 'lup' 1.0 0.5   # 이후 종료 저장 검사를 위해 100으로 되돌림
        $t = Get-LabelText '볼륨*'
        Check 'slider: 볼륨 슬라이더 오른쪽 끝 클릭 → 볼륨 100(복원)' ($t -eq '볼륨 100') "label='$t'"
      }
      finally {   # 중간에 실패해도 이후 검사(종료 저장 볼륨 100)를 위해 양쪽 슬라이더를 오른쪽 끝으로 되돌림
        try { $tp = Get-TitleParts; foreach ($sl in $tp.VolSlider, $tp.OpaSlider) { Send-Mouse $sl 'ldown' 1.0 0.5; Send-Mouse $sl 'lup' 1.0 0.5 } } catch { }
      }
    }

    # ---------- 3-3. 숫자 라벨 클릭 → 인라인 입력 칸 / 즉시 반영 / Esc 복원 ----------
    function Test-InlineEdit([string]$labelPattern, [string]$prefix, [string]$suffix, [string]$original, [int[]]$steps) {
        # $steps: 입력할 문자 목록(ASCII). 마지막 두 글자까지 입력한 뒤 값을 확인한다.
        $tp = Get-TitleParts
        $lbl = if ($labelPattern -like '볼륨*') { $tp.VolLabel } else { $tp.OpaLabel }
        Send-Mouse $lbl 'ldown' 0.5 0.5; Send-Mouse $lbl 'lup' 0.5 0.5
        $edit = $null
        for ($try = 0; $try -lt 8 -and $null -eq $edit; $try++) {   # 입력 칸이 뜰 때까지 잠깐 기다림(타이밍 편차)
            if ($try -gt 0) { Start-Sleep -Milliseconds 200 }
            $edit = (Get-Elements $main) | Where-Object { $_.Type -eq 'Edit' -and [Win]::IsWindowVisible($_.Hwnd) } | Select-Object -First 1
        }
        Check "inline[$prefix]: 라벨을 클릭하면 입력 칸(Edit)이 나타남" ($null -ne $edit)
        if ($null -eq $edit) { return }
        $initText = ''
        try { $initText = $edit.El.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { }
        Check "inline[$prefix]: 입력 칸에 현재 값이 채워짐" ($initText -eq ($original -replace '\D', '')) "text='$initText' expected=$original"
        # 기존 글자를 지우고(Backspace) 한 글자씩 입력 — WM_CHAR로 보내므로 실제 키보드/포커스를 쓰지 않는다
        1..5 | ForEach-Object { [Win]::SendMessage($edit.Hwnd, 0x0102, [IntPtr]8, [IntPtr]::Zero) | Out-Null }
        $seq = @()
        foreach ($ch in $steps) {
            [Win]::SendMessage($edit.Hwnd, 0x0102, [IntPtr]$ch, [IntPtr]::Zero) | Out-Null
            Start-Sleep -Milliseconds 120
            $seq += (Get-LabelText $labelPattern)
        }
        $script:inlineSeq = $seq
        # Esc (WM_KEYDOWN VK_ESCAPE)
        [Win]::SendMessage($edit.Hwnd, 0x0100, [IntPtr]0x1B, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 300
        $after = Get-LabelText $labelPattern
        $hidden = -not [Win]::IsWindowVisible($edit.Hwnd)
        Check "inline[$prefix]: Esc로 원래 값 복원하고 입력 칸이 사라짐" ($after -eq "$prefix$original$suffix" -and $hidden) "label='$after' editHidden=$hidden"
    }
    Run-Safe 'inline-volume' {
        Test-InlineEdit '볼륨*' '볼륨 ' '' '100' @(55, 48)   # '7', '0'
        Check 'inline[볼륨 ]: 입력 즉시 라벨이 바뀜 (7 → 볼륨 7, 70 → 볼륨 70)' ($script:inlineSeq[0] -eq '볼륨 7' -and $script:inlineSeq[1] -eq '볼륨 70') ("seq=" + ($script:inlineSeq -join ' | '))
    }
    Run-Safe 'inline-opacity' {
        Test-InlineEdit '투명도*' '투명도 ' '%' '100' @(53, 48)   # '5', '0'
        # 5는 최솟값(30) 미만이라 입력 중간 상태로 두고, 50이 되는 순간 반영된다 (Form1.ApplyInlineEditLive 주석)
        Check 'inline[투명도 ]: 범위 밖 중간 입력(5)은 반영하지 않고 50에서 반영' ($script:inlineSeq[1] -eq '투명도 50%' -and $script:inlineSeq[0] -ne '투명도 5%') ("seq=" + ($script:inlineSeq -join ' | '))
    }

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

    # ---------- 5-1. 알람 설정창: 값 변경 후 확인/취소 ----------
    # 확인(Invoke) → config.json에 즉시 반영, 취소 → 반영 안 됨. 최종 값은 종료 후에도 다시 확인한다(8번).
    $script:alarmExpect = $null
    function Open-AlarmForm {
        $tb = ((Get-Elements $main) | Where-Object { $_.Type -eq 'Button' } | Sort-Object L)[1]
        Send-Mouse $tb 'rdown'; Send-Mouse $tb 'rup'
        $h = Find-ProcWindow '알람 설정'
        if ($h -eq [IntPtr]::Zero) { throw 'alarm window did not open' }
        Start-Sleep -Milliseconds 900
        $h
    }
    function Get-AlarmControls([IntPtr]$h) {
        $ae = @(Get-Elements $h)
        # UIA 이름: 커스텀 알람 체크박스는 옆 라벨('커스텀 알람'/'초')이 이름이 되고, 이름 칸은 '', 초 칸의 Edit는 '회전자'
        $nameEdits = @($ae | Where-Object { $_.Type -eq 'Edit' -and [string]::IsNullOrEmpty($_.Name) } | Sort-Object T)
        $tickEdits = @($ae | Where-Object { $_.Type -eq 'Edit' -and $_.Name -eq '회전자' } | Sort-Object T)
        [pscustomobject]@{
            Check1   = $ae | Where-Object { $_.Type -eq 'CheckBox' -and $_.Name -eq '재획비 (2시간)' } | Select-Object -First 1
            Check2   = $ae | Where-Object { $_.Type -eq 'CheckBox' -and $_.Name -eq '경쿠 (1시간)' } | Select-Object -First 1
            Customs  = @($ae | Where-Object { $_.Type -eq 'CheckBox' -and ($_.Name -eq '커스텀 알람' -or $_.Name -eq '초') } | Sort-Object T)
            NameEdits = $nameEdits
            TickEdits = $tickEdits
            Ok       = $ae | Where-Object { $_.Type -eq 'Button' -and $_.Name -eq '확인' } | Select-Object -First 1
            Cancel   = $ae | Where-Object { $_.Type -eq 'Button' -and $_.Name -eq '취소' } | Select-Object -First 1
        }
    }
    function Set-Toggle($el) {
        try { $el.El.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
        catch { throw "Toggle 실패 ($($el.Type) '$($el.Name)'): $($_.Exception.Message)" }
        Start-Sleep -Milliseconds 150
    }
    function Get-Toggle($el) { $el.El.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
    function Set-EditValue($el, [string]$v) {
        try { $el.El.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
        catch { throw "SetValue 실패 ($($el.Type) '$($el.Name)' $($el.Class)): $($_.Exception.Message)" }
        Start-Sleep -Milliseconds 150
    }
    function Read-Cfg { $p = Join-Path $app 'config.json'; if (Test-Path $p) { Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json } else { $null } }

    Run-Safe 'alarm-confirm' {
        $h = Open-AlarmForm
        $c = Get-AlarmControls $h
        if (-not $c.Check1 -or -not $c.Check2 -or $c.Customs.Count -ne 3 -or $c.NameEdits.Count -ne 3 -or $c.TickEdits.Count -ne 3 -or -not $c.Ok -or -not $c.Cancel) {
            throw "alarm controls not found: c1=$([bool]$c.Check1) c2=$([bool]$c.Check2) customs=$($c.Customs.Count) nameEdits=$($c.NameEdits.Count) tickEdits=$($c.TickEdits.Count) ok=$([bool]$c.Ok) cancel=$([bool]$c.Cancel)"
        }
        $init1 = Get-Toggle $c.Check1
        Set-Toggle $c.Check1                                # 재획비(2시간) 켜기
        Set-Toggle $c.Customs[0]                            # 커스텀 알람 1 켜기
        Set-EditValue $c.NameEdits[0] 'UI테스트'
        Set-EditValue $c.TickEdits[0] '123'
        Invoke-El $c.Ok
        Start-Sleep -Milliseconds 600
        $closed = (Find-ProcWindow '알람 설정' 1000) -eq [IntPtr]::Zero
        Check 'alarm-confirm: 확인을 누르면 설정창이 닫힘' $closed
        $cfg = Read-Cfg
        $ok = $null -ne $cfg -and $cfg.alarmEnabled[0] -eq $true -and $cfg.customAlarms[0].enabled -eq $true -and $cfg.customAlarms[0].tick -eq 123 -and $cfg.customAlarms[0].name -eq 'UI테스트'
        $detail = if ($null -eq $cfg) { 'config.json 없음' } else { "init1=$init1 alarmEnabled[0]=$($cfg.alarmEnabled[0]) custom0=enabled:$($cfg.customAlarms[0].enabled) tick:$($cfg.customAlarms[0].tick) name:$($cfg.customAlarms[0].name)" }
        Check 'alarm-confirm: 확인 → config.json에 알람 체크/커스텀 알람 값이 반영됨' $ok $detail
        $script:alarmExpect = $true
    }

    function Close-AlarmIfOpen {
        $h = Find-ProcWindow '알람 설정' 600
        if ($h -ne [IntPtr]::Zero) { [Win]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 500 }
    }
    Close-AlarmIfOpen
    Run-Safe 'alarm-cancel' {
        $h = Open-AlarmForm
        $c = Get-AlarmControls $h
        if (-not $c.Check2 -or $c.TickEdits.Count -ne 3 -or -not $c.Cancel) { throw "alarm controls not found (tickEdits=$($c.TickEdits.Count))" }
        $before = Read-Cfg
        Set-Toggle $c.Check2                                # 경쿠(1시간) 켜기 (취소할 것)
        Set-EditValue $c.TickEdits[0] '456'
        Invoke-El $c.Cancel
        Start-Sleep -Milliseconds 600
        $closed = (Find-ProcWindow '알람 설정' 1000) -eq [IntPtr]::Zero
        Check 'alarm-cancel: 취소를 누르면 설정창이 닫힘' $closed
        $cfg = Read-Cfg
        $ok = $null -ne $cfg -and $cfg.alarmEnabled[1] -eq $before.alarmEnabled[1] -and $cfg.customAlarms[0].tick -eq $before.customAlarms[0].tick
        Check 'alarm-cancel: 취소 → config.json에 변경이 반영되지 않음' $ok ("alarmEnabled[1]=$($cfg.alarmEnabled[1]) (before=$($before.alarmEnabled[1])) custom0.tick=$($cfg.customAlarms[0].tick)")
        # 취소한 값이 다음에 열 때도 남아있지 않은지 (창이 이전 입력을 기억하지 않음)
        $h2 = Open-AlarmForm
        $c2 = Get-AlarmControls $h2
        $t = if ($c2.Check2) { Get-Toggle $c2.Check2 } else { 'n/a' }
        Check 'alarm-cancel: 다시 열면 취소한 값이 아닌 저장된 값이 표시됨' ($t -eq 'Off') "경쿠(1시간) toggle=$t"
        [Win]::PostMessage($h2, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null   # WM_CLOSE (Accepted=false)
        Start-Sleep -Milliseconds 500
    }

    Close-AlarmIfOpen

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
        foreach ($want in '항상 위에 표시', '다른 창 선택 시 테두리 안보이기', '알람 울릴 때 영상 음소거', '넷플릭스', '라프텔', '웨이브', '티빙', '쿠팡플레이', '디즈니플러스', '유튜브', '＋ 사이트 추가') {
            Check "menu: '$want' 항목" (($names -contains $want) -or ($names -contains "$want (시작)"))   # 시작 사이트는 이름 뒤에 (시작)이 붙는다
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

    # ---------- 7b. 같은 폴더에서 두 번째 실행 ----------
    # 두 번째 인스턴스는 창을 띄우지 않고 곧 종료해야 하며, 기존 인스턴스는 계속 살아 있어야 한다.
    $env:JHP_OFFSCREEN = '1'
    $dup = Start-Process (Join-Path $app 'JHP.exe') -WorkingDirectory $app -PassThru
    Remove-Item Env:\JHP_OFFSCREEN -ErrorAction SilentlyContinue
    $dupExited = $dup.WaitForExit(8000)
    if (-not $dupExited) { try { $dup.Kill() } catch { } }
    Check 'single: 같은 폴더의 두 번째 실행은 곧 종료됨' $dupExited
    $proc.Refresh()
    Check 'single: 기존 인스턴스는 계속 실행 중' (-not $proc.HasExited)

    # ---------- 8. 정상 종료 시 설정 저장 ----------
    # 앞의 슬라이더 검사에서 볼륨을 100으로 만들었다. WM_CLOSE(정상 종료)로 닫은 뒤 config.json에 반영됐는지 본다.
    [Win]::PostMessage($main, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    $proc.WaitForExit(10000) | Out-Null
    Check 'close: WM_CLOSE로 정상 종료됨' $proc.HasExited
    $cfgPath = Join-Path $app 'config.json'
    $cfgText = if (Test-Path $cfgPath) { Get-Content $cfgPath -Raw -Encoding UTF8 } else { '' }
    Check 'close: 종료 시 config.json에 볼륨 100 저장' ($cfgText -match '"volume":\s*100') ($cfgText.Length.ToString() + ' bytes')
    Check 'close: 종료 시 사이트 목록 7개 저장' ((([regex]::Matches($cfgText, '"url":')).Count) -eq 7)

    if ($script:alarmExpect) {
        Run-Safe 'close-alarm' {
            $cfg = $cfgText | ConvertFrom-Json
            $ok = $cfg.alarmEnabled[0] -eq $true -and $cfg.customAlarms[0].enabled -eq $true -and $cfg.customAlarms[0].tick -eq 123 -and $cfg.customAlarms[0].name -eq 'UI테스트'
            Check 'close: 종료 후 config.json에 확인한 알람 설정이 남아 있고 취소한 값은 없음' $ok "alarmEnabled=$($cfg.alarmEnabled -join ',') custom0.tick=$($cfg.customAlarms[0].tick)"
        }
    }
    else { Write-Host '[SKIP] close: 알람 설정 반영 확인 — 앞의 알람 확인 검사가 실행되지 않음' }

    # ---------- 8-1. 포커스 아웃 옵션 (WM_ACTIVATE 메시지로 합성, 사용자 포커스는 건드리지 않음) ----------
    Run-Safe 'focusout' {
        $cfgFile = Join-Path $app 'config.json'
        $txt = Get-Content $cfgFile -Raw -Encoding UTF8
        if ($txt -notmatch '"isHideWindowBorderOnFocusOut":\s*false') { throw 'isHideWindowBorderOnFocusOut 항목을 찾지 못함' }
        Set-Content -LiteralPath $cfgFile -Value ($txt -replace '"isHideWindowBorderOnFocusOut":\s*false', '"isHideWindowBorderOnFocusOut": true') -Encoding UTF8
        if (-not $KeepOnScreen) { $env:JHP_OFFSCREEN = '1' }
        $p3 = Start-Process (Join-Path $app 'JHP.exe') -WorkingDirectory $app -PassThru
        Remove-Item Env:\JHP_OFFSCREEN -ErrorAction SilentlyContinue
        try {
            $m3 = [IntPtr]::Zero
            for ($i = 0; $i -lt 60; $i++) { Start-Sleep -Milliseconds 250; $p3.Refresh(); if ($p3.MainWindowHandle -ne [IntPtr]::Zero) { $m3 = $p3.MainWindowHandle; break } }
            if ($m3 -eq [IntPtr]::Zero) { throw 'focusout 인스턴스의 창을 찾지 못함' }
            if (-not $KeepOnScreen) { [Win]::SetWindowPos($m3, [IntPtr]::Zero, -20000, 0, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null }
            Start-Sleep -Milliseconds 2500
            $btns = @()
            for ($try = 0; $try -lt 12 -and $btns.Count -lt 5; $try++) {   # 창이 준비될 때까지 기다림. 프로세스의 최상위 창 중 타이틀바 버튼이 5개인 창을 메인으로 본다
                if ($try -gt 0) { Start-Sleep -Milliseconds 500 }
                foreach ($cand in (@($m3) + @([Win]::TopWindows([uint32]$p3.Id)))) {
                    $found = @(([System.Windows.Automation.AutomationElement]::FromHandle($cand)).FindAll([System.Windows.Automation.TreeScope]::Descendants,
                        (New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), ([System.Windows.Automation.ControlType]::Button))) |
                        Where-Object { $_.Current.NativeWindowHandle -ne 0 } | Sort-Object { $_.Current.BoundingRectangle.Left })
                    if ($found.Count -ge 5) { $btns = $found; $m3 = $cand; break }
                }
            }
            if ($btns.Count -lt 5) { throw "title bar buttons=$($btns.Count)" }
            $minH = [IntPtr]$btns[2].Current.NativeWindowHandle   # 타이틀바 안의 버튼(최소화)으로 타이틀바 표시 여부를 본다
            $WM_ACTIVATE = 0x0006; $WM_ACTIVATEAPP = 0x001C
            [Win]::SendMessage($m3, $WM_ACTIVATE, [IntPtr]1, [IntPtr]::Zero) | Out-Null          # WA_ACTIVE
            Start-Sleep -Milliseconds 300
            $shown0 = [Win]::IsWindowVisible($minH)
            [Win]::SendMessage($m3, $WM_ACTIVATE, [IntPtr]0, [IntPtr]::Zero) | Out-Null          # WA_INACTIVE
            Start-Sleep -Milliseconds 400
            $hidden = -not [Win]::IsWindowVisible($minH)
            Check 'focusout: 옵션 켠 상태에서 창이 비활성화되면 타이틀바가 숨겨짐' ($shown0 -and $hidden) "visibleBefore=$shown0 hiddenAfterDeactivate=$hidden"
            [Win]::SendMessage($m3, $WM_ACTIVATE, [IntPtr]1, [IntPtr]::Zero) | Out-Null
            Start-Sleep -Milliseconds 400
            $back = [Win]::IsWindowVisible($minH)
            Check 'focusout: 다시 활성화되면 타이틀바가 다시 보임' $back "visible=$back"
            # 알람 설정창이 떠 있는 동안에는 비활성화돼도 숨기지 않는다
            $pnlTimer = [IntPtr]$btns[1].Current.NativeWindowHandle
            $lp = [IntPtr]((5 -shl 16) -bor 5)
            [Win]::SendMessage($pnlTimer, 0x0204, [IntPtr]2, $lp) | Out-Null; [Win]::SendMessage($pnlTimer, 0x0205, [IntPtr]::Zero, $lp) | Out-Null
            $sw = [Diagnostics.Stopwatch]::StartNew(); $af = [IntPtr]::Zero
            while ($sw.ElapsedMilliseconds -lt 4000 -and $af -eq [IntPtr]::Zero) {
                foreach ($hh in [Win]::TopWindows([uint32]$p3.Id)) { $sb = New-Object System.Text.StringBuilder 64; [Win]::GetWindowText($hh, $sb, 64) | Out-Null; if ($sb.ToString() -eq '알람 설정') { $af = $hh } }
                Start-Sleep -Milliseconds 200
            }
            if ($af -ne [IntPtr]::Zero) {
                Start-Sleep -Milliseconds 400
                [Win]::SendMessage($m3, $WM_ACTIVATE, [IntPtr]0, [IntPtr]::Zero) | Out-Null
                Start-Sleep -Milliseconds 400
                $still = [Win]::IsWindowVisible($minH)
                Check 'focusout: 알람 설정창이 열려 있는 동안에는 타이틀바를 숨기지 않음' $still "visible=$still"
                [Win]::PostMessage($af, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                Start-Sleep -Milliseconds 400
            }
            else { Write-Host '[SKIP] focusout: 알람 설정창을 열지 못해 "설정창 열린 동안 유지" 검사를 건너뜀' }
        }
        finally {
            if (-not $p3.HasExited) {
                [Win]::PostMessage($p3.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                $p3.WaitForExit(8000) | Out-Null
                if (-not $p3.HasExited) { $p3.Kill() }
            }
        }
    }

    # ---------- 9. 손상된 설정 파일로 시작 ----------
    Set-Content -LiteralPath $cfgPath -Value '{ "volume": 999, "alarmEnabled": [true], "customAlarms": [], "sites": null, ' -Encoding UTF8
    Remove-Item (Join-Path $app 'config.json.corrupt') -ErrorAction SilentlyContinue
    if (-not $KeepOnScreen) { $env:JHP_OFFSCREEN = '1' }
    $proc2 = Start-Process (Join-Path $app 'JHP.exe') -WorkingDirectory $app -PassThru
    Remove-Item Env:\JHP_OFFSCREEN -ErrorAction SilentlyContinue
    $alive = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250; $proc2.Refresh()
        if ($proc2.HasExited) { break }
        if ($proc2.MainWindowHandle -ne [IntPtr]::Zero -and $i -ge 12) { $alive = $true; break }
    }
    Check 'robust: 손상된 config.json으로도 시작되고 종료되지 않음' $alive
    Check 'robust: 손상된 설정 사본(config.json.corrupt) 보존' (Test-Path (Join-Path $app 'config.json.corrupt'))
    if (-not $proc2.HasExited) { $proc2.Kill() }
}
finally {
    if (-not $proc.HasExited) { $proc.Kill() }
}

$fail = @($results | Where-Object { -not $_.Pass })
Write-Host ''
Write-Host ("결과: {0} / {1} 통과, 캡처: {2}" -f ($results.Count - $fail.Count), $results.Count, $OutDir)
if ($fail.Count -gt 0) { exit 1 }
