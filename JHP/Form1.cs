using JHP.Api;
using JHP.Asset;
using JHP.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JHP;

public partial class Form1 : Form
{
    private static readonly int[] AlarmSeconds = [7200, 3600, 1800, 1200, 900, 600, 100, 55];
    // 알람이 울릴 때 TTS로 읽는 이름 (원본 재획플레이어의 항목명)
    private static readonly string[] AlarmNames = ["재획비", "경쿠", "경뿌/경쿠/유니온", "유니온 버프", "경쿠", "유니온 버프", "메소 회수", "파운틴"];

    private const string CurrentVersion = "v1.1.0";

    private readonly Config _config = Config.Instance;
    private readonly int[] _remaining = new int[8];
    private readonly int[] _customRemaining = new int[3];
    private bool _timerRunning = false;

    // 인라인 편집 대상 추적 ("volume" | "opacity")
    private string? _inlineEditTarget;
    private int _inlineEditOriginal; // Esc로 취소할 때 되돌릴 값

    private long _lastTickMs;                                  // 마지막으로 반영한 시각(Environment.TickCount64)
    private FormWindowState _lastNonMinimizedState = FormWindowState.Normal;
    private int _modalDepth;                                   // 모달 대화상자가 떠 있는 동안 타이틀바 숨김을 막는다
    private string? _lastTip;
    private bool[]? _alarmEnabledSnapshot;                     // 알람 설정창을 열 때의 값 (확인 시 바뀐 항목만 리셋)
    private (bool Enabled, int Tick)[]? _customSnapshot;

    public Form1()
    {
        InitializeComponent();
        Icon = Program.AppIcon ?? Icon; // 작업 표시줄/Alt+Tab 아이콘 (Form 기본 아이콘 대신 exe에 포함된 앱 아이콘)
        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
        Synth.Instance.PlaybackFinished += Synth_PlaybackFinished;
        Activated += Form1_Activated;
        Deactivate += Form1_Deactivate;
        SizeChanged += (_, _) => OnWindowStateChanged();
        Resize += (_, _) => OnWindowStateChanged();   // 같은 크기로 최대화되는 경우에도 상태 표시를 갱신
        LocationChanged += (_, _) =>
        {
            // 다른 모니터로 옮긴 뒤 Win+↑ 등으로 최대화해도 해당 모니터의 작업 영역을 쓰도록 갱신
            if (WindowState == FormWindowState.Normal) UpdateMaximizedBounds();
        };
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        InitControls();

        // 저장된 크기는 96dpi 기준(논리 px) 값 → 현재 배율로 환산하고, 화면 작업 영역을 넘지 않게 제한
        if (_config.Width > 0 && _config.Height > 0)
        {
            float s = DeviceDpi / 96f;
            var area = Screen.FromPoint(new Point(_config.X, _config.Y)).WorkingArea;
            int w = Math.Min(Math.Max((int)(_config.Width * s), MinimumSize.Width), area.Width);
            int h = Math.Min(Math.Max((int)(_config.Height * s), MinimumSize.Height), area.Height);
            Size = new Size(w, h);
        }

        if (IsOnScreen(new Rectangle(_config.X, _config.Y, Width, Height)))
            Location = new Point(_config.X, _config.Y);

        // 자동 테스트용: 환경 변수 JHP_OFFSCREEN=1이면 창을 처음부터 화면 밖에 띄워 사용자의 화면에 보이지 않게 한다.
        if (Environment.GetEnvironmentVariable("JHP_OFFSCREEN") == "1")
            Location = new Point(-20000, 0);

        LayoutTitleBar();

        // 최대화 복원은 웹뷰 초기화 전에 해서, 일반 크기로 떴다가 커지는 깜빡임을 줄인다.
        if (_config.IsMaximize)
        {
            UpdateMaximizedBounds();
            WindowState = FormWindowState.Maximized;
        }

        // 타이머는 자동 시작 안 함 — 사용자가 timerButton 클릭 시 시작 (ToggleTimer에서만 timer.Start())

        await InitWV();
        CheckUpdateAsync();
    }

    // 창의 타이틀바 쪽(위 40px)이 어느 화면의 작업 영역과 충분히(가로 100px 이상) 겹치는지 확인한다.
    // 모니터를 분리했거나 해상도가 바뀌어 창이 화면 밖에 있으면 기본 위치를 쓰게 한다.
    private static bool IsOnScreen(Rectangle bounds)
    {
        var strip = new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width, 1), 40);
        foreach (Screen screen in Screen.AllScreens)
        {
            var hit = Rectangle.Intersect(screen.WorkingArea, strip);
            if (hit.Width >= 100 && hit.Height >= 20) return true;
        }
        return false;
    }

    // FormBorderStyle.None 창은 최대화하면 작업 표시줄까지 덮으므로, 최대화 영역을 작업 영역으로 제한한다.
    private void UpdateMaximizedBounds()
    {
        var screen = Screen.FromControl(this);
        var wa = screen.WorkingArea;
        MaximizedBounds = new Rectangle(wa.X - screen.Bounds.X, wa.Y - screen.Bounds.Y, wa.Width, wa.Height);
    }

    private void OnWindowStateChanged()
    {
        if (WindowState != FormWindowState.Minimized) _lastNonMinimizedState = WindowState;

        bool normal = WindowState == FormWindowState.Normal;
        pnlResizeLeft.Visible = pnlResizeRight.Visible = pnlResizeBottom.Visible = normal; // 최대화 중엔 리사이즈 핸들 없음
        btnMaximize.Type = WindowState == FormWindowState.Maximized
            ? ControlButton.ButtonType.Restore
            : ControlButton.ButtonType.Maximize;
    }

    private void InitControls()
    {
        // 타이틀바 드래그 이동 (timerButton 제외 — 버튼 클릭과 충돌)
        pnlTitleBar.MouseDown += TitleBar_MouseDown;
        lblTitle.MouseDown += TitleBar_MouseDown;
        pnlTitleBar.MouseMove += TitleBar_MouseMove;

        // 가장자리 리사이즈 핸들 (webView가 가장자리를 덮고 있어 WM_NCHITTEST 방식이 동작하지 않음)
        pnlResizeLeft.MouseDown += (_, e) => BeginResize(e, HTLEFT);
        pnlResizeRight.MouseDown += (_, e) => BeginResize(e, HTRIGHT);
        pnlResizeBottom.MouseDown += ResizeBottom_MouseDown;
        pnlResizeBottom.MouseMove += ResizeBottom_MouseMove;

        // 창 폭에 맞춰 볼륨/투명도 영역을 재배치 (라벨-슬라이더 겹침 방지)
        pnlTitleBar.Resize += (_, _) => LayoutTitleBar();
        lblVolumeValue.TextChanged += (_, _) => LayoutTitleBar();
        lblOpacityValue.TextChanged += (_, _) => LayoutTitleBar();

        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (_, _) =>
        {
            UpdateMaximizedBounds();
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        };
        btnClose.Click += (_, _) => Close();

        // timerButton: 좌클릭=시작/정지, 우클릭=알람설정
        timerButton.Click += (_, _) => ToggleTimer();
        timerButton.RightClick += (_, _) => OpenAlarmSettings();
        SetTimerTip("클릭하여 타이머 시작\n우클릭: 알람 설정");
        toolTip.SetToolTip(btnMenu, "사이트 목록 / 설정");
        toolTip.SetToolTip(btnMinimize, "최소화");
        toolTip.SetToolTip(btnMaximize, "최대화 / 이전 크기");
        toolTip.SetToolTip(btnClose, "닫기");
        toolTip.SetToolTip(sliderVolume, "볼륨");
        toolTip.SetToolTip(sliderOpacity, "투명도");
        toolTip.SetToolTip(lblVolumeValue, "클릭하여 숫자로 입력");
        toolTip.SetToolTip(lblOpacityValue, "클릭하여 숫자로 입력");

        // 볼륨
        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        sliderVolume.ValueChanged += (_, _) =>
        {
            lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            ScheduleSave();
        };
        lblVolumeValue.Click += (_, _) => BeginInlineEdit("volume");

        // 투명도 (30~100%)
        sliderOpacity.Value = Math.Clamp((int)Math.Round(_config.Opacity * 100), sliderOpacity.Minimum, sliderOpacity.Maximum);
        Opacity = sliderOpacity.Value / 100.0;
        lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
        sliderOpacity.ValueChanged += (_, _) =>
        {
            lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
            Opacity = sliderOpacity.Value / 100.0;
            _config.Opacity = Opacity;
            ScheduleSave();
        };
        lblOpacityValue.Click += (_, _) => BeginInlineEdit("opacity");

        // 인라인 편집 TextBox
        tbInlineEdit.KeyDown += InlineEdit_KeyDown;
        tbInlineEdit.MaxLength = 3;
        tbInlineEdit.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar); // 숫자만 입력
        tbInlineEdit.TextChanged += (_, _) => ApplyInlineEditLive();
        tbInlineEdit.LostFocus += (_, _) => CommitInlineEdit();

        // V버튼: 사이트 목록 + 사이트 추가 + 항상위/테두리숨김 드롭다운
        // (구버전의 menuStrip/siteList를 대체 — 매 클릭마다 최신 _config.Sites 기준으로 새로 빌드)
        TopMost = _config.TopMost;
        btnMenu.Click += (_, _) => OpenSiteMenu();

        // 알람 타이머 초기화 (시작은 안 함)
        for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
        UpdateNextAlarmLabel();

        timer.Tick += Timer_Tick;
    }

    // 슬라이더를 끄는 동안 매 값마다 파일을 쓰지 않도록 마지막 변경 후 0.4초 뒤 한 번만 저장한다 (종료 시에는 별도로 저장).
    private System.Windows.Forms.Timer? _saveDebounce;

    private void ScheduleSave()
    {
        if (_saveDebounce is null)
        {
            _saveDebounce = new System.Windows.Forms.Timer { Interval = 400 };
            _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); _config.Save(); };
        }
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    // ===== 타이머 시작/정지 토글 =====
    private void ToggleTimer()
    {
        _timerRunning = !_timerRunning;

        if (_timerRunning)
        {
            // 시작 시 항상 리셋
            for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
            for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
            _lastTickMs = Environment.TickCount64;
            timer.Start();
        }
        else
        {
            timer.Stop();
        }

        timerButton.Active = _timerRunning;
        UpdateNextAlarmLabel();
    }

    // ===== 인라인 숫자 편집 =====
    private void BeginInlineEdit(string target)
    {
        _inlineEditTarget = target;
        int value = target == "volume" ? sliderVolume.Value : sliderOpacity.Value;
        Label lbl = target == "volume" ? lblVolumeValue : lblOpacityValue;

        _inlineEditOriginal = value;
        tbInlineEdit.Text = value.ToString();
        tbInlineEdit.Location = new Point(lbl.Left, (pnlTitleBar.Height - tbInlineEdit.Height) / 2);
        tbInlineEdit.Visible = true;
        tbInlineEdit.Focus();
        tbInlineEdit.SelectAll();
    }

    // 입력 도중에도 유효 범위의 값이면 즉시 반영한다(범위를 벗어나거나 입력 중간 상태는 확정 시 보정).
    // 예: 투명도 최소 30이므로 "5"를 치는 순간 30%로 튀지 않고, "50"까지 입력해야 반영된다.
    private void ApplyInlineEditLive()
    {
        if (_inlineEditTarget is null || !tbInlineEdit.Visible) return;
        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        NSlider slider = _inlineEditTarget == "volume" ? sliderVolume : sliderOpacity;
        if (val < slider.Minimum || val > slider.Maximum) return;
        slider.Value = val;
    }

    private void CommitInlineEdit()
    {
        if (_inlineEditTarget is null) { tbInlineEdit.Visible = false; return; }

        string target = _inlineEditTarget;
        _inlineEditTarget = null; // Visible=false로 LostFocus가 다시 들어와도 중복 처리되지 않게 먼저 비운다
        tbInlineEdit.Visible = false;

        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        if (target == "volume")
            sliderVolume.Value = Math.Clamp(val, sliderVolume.Minimum, sliderVolume.Maximum);
        else
            sliderOpacity.Value = Math.Clamp(val, sliderOpacity.Minimum, sliderOpacity.Maximum);
    }

    private void InlineEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { CommitInlineEdit(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape)
        {
            // 입력 중 이미 슬라이더에 반영됐으므로 원래 값으로 되돌린다
            string? target = _inlineEditTarget;
            _inlineEditTarget = null;
            tbInlineEdit.Visible = false;
            if (target == "volume") sliderVolume.Value = _inlineEditOriginal;
            else if (target == "opacity") sliderOpacity.Value = _inlineEditOriginal;
            e.SuppressKeyPress = true;
        }
    }

    // ===== WebView2 =====
    private async Task InitWV()
    {
        try
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: cacheDir);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.NavigationCompleted += (_, e) => { if (e.IsSuccess) InjectJS(); };
            // 렌더러 프로세스가 죽으면 빈 화면으로 남으므로 새로고침한다
            webView.CoreWebView2.ProcessFailed += (_, e) =>
            {
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
                    BeginInvoke(() => webView.CoreWebView2?.Reload());
            };
            webView.CoreWebView2.Navigate(ResolveStartUrl());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 초기화 실패: {ex.Message}\nWebView2 런타임이 설치되어 있는지 확인해주세요.",
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string ResolveStartUrl()
    {
        // 손상/수동 편집된 주소로 시작하다 예외가 나지 않도록 http(s) 주소만 사용한다
        if (JHP.Api.Site.TryNormalizeUrl(_config.LatestUrl, out string latest)) return latest;
        var defaultSite = _config.Sites.FirstOrDefault(s => s.Name == _config.DefaultSite);
        if (defaultSite is not null && JHP.Api.Site.TryNormalizeUrl(defaultSite.Url, out string def)) return def;
        foreach (var site in _config.Sites)
            if (JHP.Api.Site.TryNormalizeUrl(site.Url, out string first)) return first;
        return "https://www.naver.com";
    }

    // 🔴 버그 수정: about:blank 또는 null Source일 때 Uri 생성 예외가 나던 부분 → null 체크 + try/catch
    private async void InjectJS()
    {
        if (webView.CoreWebView2 is null) return;

        string? source = webView.CoreWebView2.Source;
        if (string.IsNullOrWhiteSpace(source)) return;

        string host;
        try { host = new Uri(source).Host; }
        catch { return; }

        try
        {
            if (HostIs(host, "laftel.net"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.LaftelSkipNext);
            else if (HostIs(host, "netflix.com"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.NetflixSkipNext);
        }
        catch { }
    }

    // "evil-netflix.com", "netflix.com.example" 같은 주소와 구분하기 위해 정확히 같거나 하위 도메인인 경우만 일치시킨다
    private static bool HostIs(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    private async void CheckUpdateAsync()
    {
        try
        {
            var (hasUpdate, latest, url) = await UpdateChecker.Check(CurrentVersion);
            if (!hasUpdate || string.IsNullOrWhiteSpace(url)) return;
            if (Prompt.ShowDialog("업데이트 알림", $"새 버전 {latest}이 있습니다. 다운로드 페이지를 여시겠습니까?", this))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { } // 업데이트 확인 실패가 앱 동작에 영향을 주지 않도록 한다
    }

    // ===== 알람 타이머 =====
    private void Timer_Tick(object? sender, EventArgs e)
    {
        // 정지 상태면 Tick 무시 (이중 안전장치)
        if (!_timerRunning) return;

        // WinForms 타이머는 UI가 바쁠 때(창 드래그, 모달 대화상자) Tick이 밀리거나 합쳐지므로,
        // Tick 횟수가 아니라 실제 경과 시간(초)만큼 남은 시간을 줄인다. 타이머는 250ms 주기로 돌며 1초 미만 나머지는 이월한다.
        long now = Environment.TickCount64;
        int steps = (int)((now - _lastTickMs) / 1000);
        if (steps < 1) return;
        if (steps > 120)
        {
            // 절전/최대 부하 등으로 2분 넘게 멈췄다 돌아온 경우: 밀린 시간을 한꺼번에 반영하지 않고 지금부터 다시 센다
            _lastTickMs = now;
            return;
        }
        _lastTickMs += steps * 1000L;

        // 같은 순간에 여러 알람이 겹치면(예: 2시간 시점에 1시간/30분/... 동시 도달) 목록 위쪽 하나만 울린다.
        // 모든 알람의 남은 시간은 각자 정상적으로 갱신/리셋한다.
        string? toFire = null;

        for (int step = 0; step < steps; step++)
        {
            for (int i = 0; i < 8; i++)
            {
                if (!_config.AlarmEnabled[i]) continue;
                if (--_remaining[i] > 0) continue;
                _remaining[i] = AlarmSeconds[i];
                toFire ??= AlarmNames[i];
            }

            for (int i = 0; i < 3; i++)
            {
                var alarm = _config.CustomAlarms[i];
                if (!alarm.Enabled || alarm.Tick <= 0) continue;
                if (--_customRemaining[i] > 0) continue;
                _customRemaining[i] = alarm.Tick;
                toFire ??= string.IsNullOrWhiteSpace(alarm.Name) ? "커스텀 알람" : alarm.Name;
            }
        }

        if (toFire is not null) FireAlarm(toFire);

        UpdateNextAlarmLabel();
    }

    // ===== 알람 울리는 동안 영상 음소거 =====
    // 알람이 연속으로 울려도 처음 저장한 값만 복원 대상으로 유지해, 사용자가 직접 음소거한 상태를 해제하지 않는다.
    private bool _alarmMuteActive;
    private bool _alarmMutePrevious;
    private System.Windows.Forms.Timer? _alarmMuteSafetyTimer;

    private void MuteVideoForAlarm()
    {
        if (!_config.MuteVideoOnAlarm || webView.CoreWebView2 is null) return;
        try
        {
            if (!_alarmMuteActive)
            {
                _alarmMutePrevious = webView.CoreWebView2.IsMuted;
                _alarmMuteActive = true;
            }
            webView.CoreWebView2.IsMuted = true;

            // 재생 완료 이벤트가 오지 않는 경우를 대비한 안전 복원 (15초)
            if (_alarmMuteSafetyTimer is null)
            {
                _alarmMuteSafetyTimer = new System.Windows.Forms.Timer { Interval = 15000 };
                _alarmMuteSafetyTimer.Tick += (_, _) => RestoreVideoMute();
            }
            _alarmMuteSafetyTimer.Stop();
            _alarmMuteSafetyTimer.Start();
        }
        catch { }
    }

    private void RestoreVideoMute()
    {
        _alarmMuteSafetyTimer?.Stop();
        if (!_alarmMuteActive) return;
        _alarmMuteActive = false;
        try
        {
            if (webView.CoreWebView2 is not null)
                webView.CoreWebView2.IsMuted = _alarmMutePrevious;
        }
        catch { }
    }

    // Synth 이벤트는 UI 스레드가 아닐 수 있으므로 UI 스레드로 넘겨 복원한다
    private void Synth_PlaybackFinished()
    {
        try
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(RestoreVideoMute);
        }
        catch { }
    }

    private void FireAlarm(string name)
    {
        MuteVideoForAlarm();
        if (_config.Tts)
            Synth.Instance.TTS(name.Replace("/", ", "), _config.Volume, _config.Rate);
        else
            Synth.Instance.Ring(_config.AlarmName, _config.Volume);
    }

    private void UpdateNextAlarmLabel()
    {
        if (!_timerRunning)
        {
            SetTimerTip("클릭하여 타이머 시작\n우클릭: 알람 설정");
            return;
        }

        int? min = null;
        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i])
                min = min is null ? _remaining[i] : Math.Min(min.Value, _remaining[i]);
        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled && _config.CustomAlarms[i].Tick > 0)
                min = min is null ? _customRemaining[i] : Math.Min(min.Value, _customRemaining[i]);

        string tip = min is null
            ? "다음 알람: --:--:--"
            : $"다음 알람: {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
        SetTimerTip(tip + "\n클릭: 정지 / 우클릭: 알람 설정");
    }

    // 내용이 바뀔 때만 툴팁을 갱신한다 (매초 재설정하면 표시 중인 툴팁이 깜빡일 수 있음)
    private void SetTimerTip(string text)
    {
        if (_lastTip == text) return;
        _lastTip = text;
        toolTip.SetToolTip(timerButton, text);
    }

    // 🔴 버그 수정: TopMost=true 상태에서 ShowDialog 호출 시 자식 창이 부모 뒤에 가려지는 문제 → 임시로 false 설정
    // 우클릭 = 설정창 켜기/끄기 (원본 재획플레이어 동작). 비모달이라 메인 창을 계속 조작할 수 있고,
    // 이미 열려 있으면 닫는다. 소유자(this)로 띄우므로 항상 위 상태에서도 가려지지 않는다.
    private AlarmForm? _alarmForm;

    private void OpenAlarmSettings()
    {
        if (_alarmForm is { IsDisposed: false })
        {
            _alarmForm.Close();
            return;
        }

        _alarmEnabledSnapshot = (bool[])_config.AlarmEnabled.Clone();
        _customSnapshot = _config.CustomAlarms.Select(c => (c.Enabled, c.Tick)).ToArray();

        var form = _alarmForm = new AlarmForm();
        form.FormClosed += (_, _) =>
        {
            _alarmForm = null;
            if (form.Accepted) ApplyAlarmSettings();
            form.Dispose();
        };
        form.StartPosition = FormStartPosition.Manual;
        form.Show(this);
        // 배율이 적용된 뒤 크기를 기준으로 메인 창 가운데에 배치 (CenterParent는 배율 적용 전 크기로 계산돼 어긋남)
        form.Location = new Point(Left + (Width - form.Width) / 2, Top + (Height - form.Height) / 2);
    }

    private void ApplyAlarmSettings()
    {
        // 확인만 눌러도 진행 중인 알람이 처음부터 다시 세어지지 않도록, 설정창을 열 때와 달라진 항목만 리셋한다.
        for (int i = 0; i < 8; i++)
        {
            bool changed = _alarmEnabledSnapshot is null || _alarmEnabledSnapshot[i] != _config.AlarmEnabled[i];
            if (_config.AlarmEnabled[i] && changed) _remaining[i] = AlarmSeconds[i];
        }
        for (int i = 0; i < 3; i++)
        {
            var c = _config.CustomAlarms[i];
            bool changed = _customSnapshot is null || _customSnapshot[i] != (c.Enabled, c.Tick);
            if (c.Enabled && changed) _customRemaining[i] = c.Tick;
        }

        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        UpdateNextAlarmLabel();
    }

    // ===== V버튼 드롭다운 (사이트 목록 + 사이트 추가 + 항상위/테두리숨김) =====
    // 매 클릭마다 _config.Sites 기준으로 새로 빌드하므로, 사이트 추가/삭제 후 별도 갱신 로직이 필요 없음.
    private void OpenSiteMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new SiteMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = Color.FromArgb(40, 40, 40)
        };

        SiteMenuItem.DeleteZoneWidth = (int)(28 * DeviceDpi / 96f);
        string currentUrl = webView.CoreWebView2?.Source ?? _config.LatestUrl;

        // 모든 항목의 폭을 가장 긴 텍스트 기준으로 통일 → ✕ 아이콘이 항상 같은 위치(우측 끝)에 정렬됨
        int textWidth = TextRenderer.MeasureText("＋ 사이트 추가", menu.Font).Width;
        for (int i = 0; i < _config.Sites.Count; i++)
            textWidth = Math.Max(textWidth, TextRenderer.MeasureText(
                SiteListOps.DisplayName(_config.Sites, i, _config.DefaultSite), menu.Font).Width);
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("다른 창 선택 시 테두리 안보이기", menu.Font).Width);
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("알람 울릴 때 영상 음소거", menu.Font).Width);
        int itemWidth = textWidth + (int)(70 * DeviceDpi / 96f);
        int itemHeight = menu.Font.Height + (int)(14 * DeviceDpi / 96f); // AutoSize=false 항목은 높이를 직접 지정해야 배율에서 글자가 잘리지 않는다

        // 원본 재획플레이어와 같은 순서: 항상 위 / 테두리 숨김 → 사이트 목록 → 사이트 추가
        var topmostItem = new ToolStripMenuItem("항상 위에 표시")
        {
            CheckOnClick = true,
            Checked = _config.TopMost,
            AutoSize = false,
            Size = new Size(itemWidth, itemHeight)
        };
        topmostItem.CheckedChanged += (_, _) =>
        {
            TopMost = topmostItem.Checked;
            _config.TopMost = TopMost;
            _config.Save();
        };
        menu.Items.Add(topmostItem);

        var hideBorderItem = new ToolStripMenuItem("다른 창 선택 시 테두리 안보이기")
        {
            CheckOnClick = true,
            Checked = _config.IsHideWindowBorderOnFocusOut,
            AutoSize = false,
            Size = new Size(itemWidth, itemHeight)
        };
        hideBorderItem.CheckedChanged += (_, _) =>
        {
            _config.IsHideWindowBorderOnFocusOut = hideBorderItem.Checked;
            _config.Save();
        };
        menu.Items.Add(hideBorderItem);

        var muteItem = new ToolStripMenuItem("알람 울릴 때 영상 음소거")
        {
            CheckOnClick = true,
            Checked = _config.MuteVideoOnAlarm,
            AutoSize = false,
            Size = new Size(itemWidth, itemHeight)
        };
        muteItem.CheckedChanged += (_, _) =>
        {
            _config.MuteVideoOnAlarm = muteItem.Checked;
            _config.Save();
        };
        menu.Items.Add(muteItem);

        if (_config.Sites.Count > 0)
            menu.Items.Add(new ToolStripSeparator());

        foreach (var site in _config.Sites.ToList())
        {
            int siteIndex = _config.Sites.IndexOf(site);
            var item = new SiteMenuItem(site, SiteListOps.DisplayName(_config.Sites, siteIndex, _config.DefaultSite))
            {
                AutoSize = false,
                Size = new Size(itemWidth, itemHeight),
                Checked = JHP.Api.Site.SameUrl(site.Url, currentUrl)
            };
            item.Click += (_, _) => NavigateToSite(site);
            // 우클릭: 열린 메뉴를 닫고 관리 메뉴를 같은 위치에 띄운다 (관리 후 ▼ 메뉴를 다시 열어 목록을 갱신)
            item.ManageRequested += (_, _) =>
            {
                var pos = Cursor.Position;
                menu.Close();
                BeginInvoke(() => OpenSiteManageMenu(site, pos));
            };
            item.DeleteRequested += (_, _) =>
            {
                _config.Sites.Remove(site);
                // 시작 사이트를 지웠으면(같은 이름이 남아 있지 않으면) 지정을 해제해 이름만 남지 않게 한다
                if (site.Name == _config.DefaultSite && !_config.Sites.Any(s => s.Name == site.Name))
                    _config.DefaultSite = "";
                _config.Save();
                menu.Items.Remove(item);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var addItem = new ToolStripMenuItem("＋ 사이트 추가") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        addItem.Click += (_, _) => AddSite();
        menu.Items.Add(addItem);

        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(btnMenu, new Point(0, btnMenu.Height));
    }

    // ===== 사이트 목록 =====
    // 🔴 버그 수정: OpenAlarmSettings와 동일한 TopMost 자식창 버그 수정 적용
    private void AddSite()
    {
        var added = ShowSiteDialog(null);
        if (added is null) return;

        _config.Sites.Add(added);
        _config.Save();
    }

    // 사이트 추가/수정 대화상자. initial이 null이면 추가, 있으면 수정. 취소 시 null.
    private JHP.Api.Site? ShowSiteDialog(JHP.Api.Site? initial)
    {
        bool wasTopMost = TopMost;
        TopMost = false;
        _modalDepth++;
        try
        {
            using var form = new SiteForm(initial);
            return form.ShowDialog(this) == DialogResult.OK ? form.Result : null;
        }
        finally
        {
            _modalDepth--;
            TopMost = wasTopMost;
        }
    }

    // 사이트 항목 우클릭 관리 메뉴: 위로/아래로 이동, 이름/주소 수정, 시작 사이트 지정.
    // 순서/지정 계산은 SiteListOps(순수 함수)가 하고, 여기서는 반영·저장·메뉴 재오픈만 한다.
    private void OpenSiteManageMenu(JHP.Api.Site site, Point screenPos)
    {
        int index = _config.Sites.IndexOf(site);
        if (index < 0) { OpenSiteMenu(); return; } // 그 사이 목록에서 사라짐

        float s = DeviceDpi / 96f;
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = Color.FromArgb(40, 40, 40)
        };
        int w = (int)(Math.Max(TextRenderer.MeasureText("이름/주소 수정…", menu.Font).Width,
                               TextRenderer.MeasureText("시작 사이트로 지정", menu.Font).Width) + 24 * s);
        int h = menu.Font.Height + (int)(12 * s);

        bool acted = false;
        ToolStripMenuItem Add(string text, bool enabled, Action action)
        {
            var mi = new ToolStripMenuItem(text) { AutoSize = false, Size = new Size(w, h), Enabled = enabled };
            mi.Click += (_, _) => { acted = true; action(); };
            menu.Items.Add(mi);
            return mi;
        }

        Add("위로 이동", index > 0, () => ApplySiteChange(() => SiteListOps.TryMove(_config.Sites, index, -1)));
        Add("아래로 이동", index < _config.Sites.Count - 1, () => ApplySiteChange(() => SiteListOps.TryMove(_config.Sites, index, +1)));
        menu.Items.Add(new ToolStripSeparator());
        Add("이름/주소 수정…", true, () =>
        {
            var edited = ShowSiteDialog(site);
            if (edited is null) return;
            ApplySiteChange(() =>
            {
                string def = _config.DefaultSite;
                bool ok = SiteListOps.TryReplace(_config.Sites, index, edited, ref def);
                _config.DefaultSite = def;
                return ok;
            });
        });
        Add("시작 사이트로 지정", SiteListOps.CanSetDefault(_config.Sites, index, _config.DefaultSite), () =>
            ApplySiteChange(() =>
            {
                string def = _config.DefaultSite;
                bool ok = SiteListOps.TrySetDefault(_config.Sites, index, ref def);
                _config.DefaultSite = def;
                return ok;
            }));

        // 이동/지정/수정을 하면(취소 포함) ▼ 메뉴를 다시 열어 갱신된 목록을 보여 준다. 메뉴 밖을 눌러 닫은 경우는 열지 않음.
        menu.Closed += (_, _) => BeginInvoke(() =>
        {
            menu.Dispose();
            if (acted) OpenSiteMenu();
        });
        menu.Show(screenPos);
    }

    // 목록 변경 함수를 실행하고, 실제로 바뀌었으면 저장한다.
    private void ApplySiteChange(Func<bool> change)
    {
        if (change()) _config.Save();
    }

    private void NavigateToSite(Site site)
    {
        if (webView.CoreWebView2 is null) return;
        if (!JHP.Api.Site.TryNormalizeUrl(site.Url, out string url))
        {
            MessageBox.Show(this, $"'{site.Name}'의 주소가 올바르지 않습니다.\n{site.Url}", "JHP",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { webView.CoreWebView2.Navigate(url); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "JHP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // V버튼 드롭다운 전용 사이트 항목: 일반 ToolStripMenuItem이지만 우측 끝(✕ 영역) 클릭 시
    // 이동 대신 삭제를 수행하도록 OnMouseUp을 가로챈다. SiteMenuRenderer가 그리는 ✕ 아이콘과 짝을 이룸.
    private sealed class SiteMenuItem : ToolStripMenuItem
    {
        public static int DeleteZoneWidth = 28; // OpenSiteMenu()에서 DPI 배율로 갱신

        public JHP.Api.Site SiteInfo { get; }
        public event EventHandler? DeleteRequested;
        public event EventHandler? ManageRequested;

        public SiteMenuItem(Site site, string displayText) : base(displayText)
        {
            SiteInfo = site;
        }

        protected override void OnMouseUp(MouseEventArgs mea)
        {
            if (mea.Button == MouseButtons.Right)
            {
                ManageRequested?.Invoke(this, EventArgs.Empty);
                return; // 우클릭은 이동(Click) 없이 관리 메뉴만 연다
            }
            if (mea.Button == MouseButtons.Left && mea.X >= Width - DeleteZoneWidth)
            {
                DeleteRequested?.Invoke(this, EventArgs.Empty);
                return; // ✕ 영역 클릭 시 기본 Click(사이트 이동) 동작은 발생시키지 않음
            }
            base.OnMouseUp(mea);
        }
    }

    // DarkMenuRenderer를 확장해 SiteMenuItem 우측에 ✕(삭제) 아이콘만 추가로 그려주는 렌더러.
    // 다크 테마 배경/체크 표시는 DarkMenuRenderer 로직을 그대로 사용한다.
    private sealed class SiteMenuRenderer : DarkMenuRenderer
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            base.OnRenderItemText(e);

            if (e.Item is not SiteMenuItem item) return;

            var g = e.Graphics;
            int cx = item.Width - SiteMenuItem.DeleteZoneWidth / 2;
            int cy = item.Height / 2;
            float scale = (item.Owner?.DeviceDpi ?? 96) / 96f;
            int r = (int)Math.Round(4 * scale);

            Color color = item.Selected ? Color.FromArgb(220, 90, 80) : Color.FromArgb(150, 150, 150);
            using var pen = new Pen(color, 1.4f * scale);
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
        }
    }

    // ===== 타이틀바 드래그 =====
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        // 타이틀바 상단/좌우 가장자리는 리사이즈, 그 외는 창 이동
        if (sender is Panel && TitleBarEdgeHit(e.Location) is int hit and not HTCLIENT)
        {
            BeginResize(e, hit);
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
    {
        pnlTitleBar.Cursor = HitToCursor(TitleBarEdgeHit(e.Location));
    }

    // 타이틀바 가장자리 판정 (상단 + 좌우). 최대화 상태에서는 리사이즈 없음.
    private int TitleBarEdgeHit(Point p)
    {
        if (WindowState != FormWindowState.Normal) return HTCLIENT;
        int edge = (int)(ResizeEdge * DeviceDpi / 96f);
        bool top = p.Y < edge;
        bool left = p.X < edge;
        bool right = p.X >= pnlTitleBar.Width - edge;
        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (top) return HTTOP;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        return HTCLIENT;
    }

    private void ResizeBottom_MouseDown(object? sender, MouseEventArgs e) =>
        BeginResize(e, BottomEdgeHit(e.Location));

    private void ResizeBottom_MouseMove(object? sender, MouseEventArgs e) =>
        pnlResizeBottom.Cursor = HitToCursor(BottomEdgeHit(e.Location));

    // 하단 핸들의 좌우 끝은 모서리(대각선) 리사이즈
    private int BottomEdgeHit(Point p)
    {
        int corner = (int)(12 * DeviceDpi / 96f);
        if (p.X < corner) return HTBOTTOMLEFT;
        if (p.X >= pnlResizeBottom.Width - corner) return HTBOTTOMRIGHT;
        return HTBOTTOM;
    }

    private static Cursor HitToCursor(int hit) => hit switch
    {
        HTLEFT or HTRIGHT => Cursors.SizeWE,
        HTTOP or HTBOTTOM => Cursors.SizeNS,
        HTTOPLEFT or HTBOTTOMRIGHT => Cursors.SizeNWSE,
        HTTOPRIGHT or HTBOTTOMLEFT => Cursors.SizeNESW,
        _ => Cursors.Default
    };

    private void BeginResize(MouseEventArgs e, int hit)
    {
        if (e.Button != MouseButtons.Left || WindowState != FormWindowState.Normal || hit == HTCLIENT) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, hit, IntPtr.Zero);
    }

    // ===== 타이틀바 반응형 배치 =====
    // [볼륨 라벨][슬라이더] [투명도 라벨][슬라이더] 를 최소화 버튼 왼쪽까지 남은 폭에 맞춰 배치한다.
    // 라벨 폭은 "최대 글자수" 기준으로 예약해서 값이 바뀌어도(볼륨 5 → 100) 슬라이더와 겹치지 않는다.
    private void LayoutTitleBar()
    {
        if (btnMinimize is null || sliderOpacity is null) return;

        // 모든 px 값은 96dpi 기준 → 현재 DPI 배율(s)을 곱해 사용
        float s = DeviceDpi / 96f;
        int Px(int v) => (int)Math.Round(v * s);
        int gap = Px(8), groupGap = Px(16), rightPad = Px(12), minSlider = Px(60), maxSlider = Px(110); // 슬라이더를 너무 길게 두지 않아 타이틀바에 창을 잡고 끌 수 있는 빈 영역을 확보
        int h = pnlTitleBar.Height;

        // 우측 창 버튼: 오른쪽 끝에서부터 닫기 → 최대화 → 최소화 순으로 배치 (세로 가운데)
        int right = pnlTitleBar.Width;
        foreach (var b in new[] { btnClose, btnMaximize, btnMinimize })
        {
            b.Left = right - b.Width;
            b.Top = (h - b.Height) / 2;
            right = b.Left;
        }

        // 좌측 요소 세로 가운데 정렬
        btnMenu.Top = (h - btnMenu.Height) / 2;
        timerButton.Top = (h - timerButton.Height) / 2;
        lblTitle.Top = (h - lblTitle.Height) / 2;

        int startX = timerButton.Right + Px(16);
        int volLabelW = TextRenderer.MeasureText("볼륨 100", lblVolumeValue.Font).Width + Px(4);
        int opaLabelW = TextRenderer.MeasureText("투명도 100%", lblOpacityValue.Font).Width + Px(4);

        int limit = btnMinimize.Left - rightPad;
        int available = limit - startX - volLabelW - opaLabelW - gap * 2 - groupGap;
        int sliderW = Math.Clamp(available / 2, minSlider, maxSlider);

        int x = startX;
        lblVolumeValue.Left = x;
        lblVolumeValue.Top = (h - lblVolumeValue.Height) / 2;
        x += volLabelW + gap;
        sliderVolume.Left = x;
        sliderVolume.Width = sliderW;
        sliderVolume.Top = (h - sliderVolume.Height) / 2;
        x += sliderW + groupGap;
        lblOpacityValue.Left = x;
        lblOpacityValue.Top = (h - lblOpacityValue.Height) / 2;
        x += opaLabelW + gap;
        sliderOpacity.Left = x;
        sliderOpacity.Width = sliderW;
        sliderOpacity.Top = (h - sliderOpacity.Height) / 2;
    }

    // ===== 포커스 아웃 시 테두리 숨김 =====
    private void Form1_Activated(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        pnlTitleBar.Visible = true;
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        // 알람 설정창/사이트 추가창을 쓰는 동안(메인 창이 비활성화됨)에는 타이틀바를 숨기지 않는다
        if (_modalDepth > 0 || _alarmForm is { IsDisposed: false }) return;
        pnlTitleBar.Visible = false;
    }

    // ===== 종료 시 설정 저장 =====
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        Synth.Instance.PlaybackFinished -= Synth_PlaybackFinished;
        Synth.Instance.Stop();
        RestoreVideoMute(); // 종료 시 음소거 상태를 남기지 않는다
        _alarmMuteSafetyTimer?.Dispose();
        _alarmMuteSafetyTimer = null;

        // 최대화/최소화 상태에서도 일반 크기의 위치/크기(RestoreBounds)를 저장한다
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _config.X = bounds.X;
            _config.Y = bounds.Y;
            float s = DeviceDpi / 96f;
            _config.Width = (int)(bounds.Width / s);
            _config.Height = (int)(bounds.Height / s);
        }

        // 최소화한 채 종료해도 직전 상태(최대화 여부)를 유지한다
        _config.IsMaximize = (WindowState == FormWindowState.Minimized ? _lastNonMinimizedState : WindowState) == FormWindowState.Maximized;
        _config.TopMost = TopMost;
        _config.Opacity = Opacity;

        // 오류 페이지(chrome-error://)나 about:blank가 저장돼 다음 실행이 빈 화면이 되지 않도록 http(s)만 저장한다
        if (webView.CoreWebView2 is not null && JHP.Api.Site.TryNormalizeUrl(webView.CoreWebView2.Source, out string src))
            _config.LatestUrl = src;

        _config.Save();
    }

    // ===== 테두리 없는 창 리사이즈 =====
    // webView/타이틀바가 클라이언트 영역을 전부 덮어 WM_NCHITTEST가 폼에 도달하지 않으므로,
    // 가장자리 패널/타이틀바 MouseDown에서 SendMessage(WM_NCLBUTTONDOWN, HTxxx)로 직접 리사이즈를 시작한다.
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int WM_NCLBUTTONDOWN = 0xA1;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
}
