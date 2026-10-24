using Microsoft.Web.WebView2.WinForms;
using JHP.Api;
using JHP.Asset;
using JHP.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JHP;

public partial class Form1 : Form
{
    // 버전의 단일 출처는 JHP.csproj의 <Version>
    private static readonly string CurrentVersion =
        "v" + (typeof(Form1).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0");

    private readonly Config _config = Config.Instance;
    private readonly AlarmScheduler _alarms = new();           // 알람 남은 시간 계산(순수 로직, JHP.Api)
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

    protected override bool ShowWithoutActivation => Program.TestOffscreen;

    protected override CreateParams CreateParams   // 자동 테스트에서는 활성화되지 않는 창으로 만든다(WS_EX_NOACTIVATE): 창이 만들어질 때 포그라운드가 되는 것을 막는다
    {
        get
        {
            var cp = base.CreateParams;
            if (Program.TestOffscreen) cp.ExStyle |= 0x08000000;
            return cp;
        }
    }


    public Form1()
    {
        InitializeComponent();
        if (Program.TestOffscreen)   // 창이 만들어지는 순간부터 화면 밖에 있게 한다(기본 위치에 잠깐 뜨는 것 방지)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-20000, 0);
            toolTip.Active = false;   // 호버 툴팁은 화면 왼쪽 위로 밀려 올라와 사용자 화면에 보인다
        }
        CreateNextAlarmLabel();
        CreatePrivateLabel();
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

    // 시크릿 모드일 때만 보이는 표시. 타이틀바 왼쪽(남은 시간 라벨 오른쪽)에 둔다.
    private Label lblPrivate = null!;

    private void CreatePrivateLabel()
    {
        lblPrivate = new Label
        {
            AutoSize = true,
            Text = "시크릿",
            AccessibleName = "시크릿 모드",
            Font = lblVolumeValue.Font,
            ForeColor = Color.FromArgb(190, 150, 255),
            Cursor = Cursors.Default,
            Visible = false
        };
        pnlTitleBar.Controls.Add(lblPrivate);
        lblPrivate.MouseDown += TitleBar_MouseDown;
        toolTip.SetToolTip(lblPrivate, "시크릿 모드: 웹 화면의 기록·로그인·캐시가 남지 않습니다");
    }

    private void UpdatePrivateIndicator()
    {
        lblPrivate.Visible = _config.PrivateMode;
        LayoutTitleBar();
    }

    // 타이틀바에 항상 보이는 "다음 알람까지 남은 시간" (호버해야만 보이던 툴팁을 보완). 정지 중에는 --:--.
    private Label lblNext = null!;

    private void CreateNextAlarmLabel()
    {
        lblNext = new Label
        {
            AutoSize = true,
            Text = "--:--",
            Font = lblVolumeValue.Font,
            ForeColor = Color.Gray,
            Cursor = Cursors.Default
        };
        pnlTitleBar.Controls.Add(lblNext);
        lblNext.MouseDown += TitleBar_MouseDown;
        toolTip.SetToolTip(lblNext, "다음 알람까지 남은 시간");
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        InitControls();

        // 저장된 크기는 96dpi 기준(논리 px) 값 → 현재 배율로 환산하고, 화면 작업 영역을 넘지 않게 제한
        if (_config.Width > 0 && _config.Height > 0)
        {
            float s = DeviceDpi / 96f;
            var area = Screen.FromPoint(new Point(_config.X, _config.Y)).WorkingArea;
            int w = Math.Min(Math.Max((int)Math.Round(_config.Width * s), MinimumSize.Width), area.Width);
            int h = Math.Min(Math.Max((int)Math.Round(_config.Height * s), MinimumSize.Height), area.Height);
            Size = new Size(w, h);
        }

        if (IsOnScreen(new Rectangle(_config.X, _config.Y, Width, Height)))
            Location = new Point(_config.X, _config.Y);

        // 자동 테스트용: 환경 변수 JHP_OFFSCREEN=1이면 창을 처음부터 화면 밖에 띄워 사용자의 화면에 보이지 않게 한다.
        if (Environment.GetEnvironmentVariable("JHP_OFFSCREEN") == "1")
        {
            StartPosition = FormStartPosition.Manual;   // 기본 위치(화면 안)로 한 번 뜨지 않게
            Location = new Point(-20000, 0);
        }

        UpdatePrivateIndicator();   // 시크릿 모드 표시(+ LayoutTitleBar)
        ApplyHotkey();

        // 최대화 복원은 웹뷰 초기화 전에 해서, 일반 크기로 떴다가 커지는 깜빡임을 줄인다.
        if (_config.IsMaximize && !Program.TestOffscreen)   // 자동 테스트(화면 밖)에서는 최대화하지 않는다: 최대화하면 모니터 위로 올라온다
        {
            UpdateMaximizedBounds();
            WindowState = FormWindowState.Maximized;
        }

        // 타이머는 자동 시작 안 함 — 사용자가 timerButton 클릭 시 시작 (ToggleTimer에서만 timer.Start())

        Config.SaveFailed += ShowNotice;
        await InitWV();
        CheckUpdateAsync();
    }

    // 웹 화면으로 키보드 포커스를 돌려 준다. SetFocus는 창을 활성화(포그라운드 이동)할 수 있어 자동 테스트에서는 건너뛴다.
    private void FocusWeb()
    {
        if (!Program.TestOffscreen) webView.Focus();
    }

    // 설정 저장 실패는 첫 번째 한 번만 알린다. 모달 대화상자 대신 타이틀바 아래 툴팁으로 잠깐 보여 준다(사냥 중 방해 최소화).
    private void ShowNotice(string message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (Program.TestOffscreen) return;   // 자동 테스트: 툴팁은 화면 가장자리로 밀려 올라와 사용자 화면에 보이므로 띄우지 않는다
        BeginInvoke(() =>
        {
            try { float s = DeviceDpi / 96f; toolTip.Show(message, this, (int)(12 * s), (int)(44 * s), 10000); } catch { }
        });
    }

    // 창의 타이틀바 쪽(위 40px)이 어느 화면의 작업 영역과 충분히(가로 100px 이상) 겹치는지 확인한다.
    // 모니터를 분리했거나 해상도가 바뀌어 창이 화면 밖에 있으면 기본 위치를 쓰게 한다.
    private bool IsOnScreen(Rectangle bounds)
    {
        float s = DeviceDpi / 96f;   // 40/100/20px는 96dpi 기준 값 → 현재 배율로 환산
        var strip = new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width, 1), (int)(40 * s));
        foreach (Screen screen in Screen.AllScreens)
        {
            var hit = Rectangle.Intersect(screen.WorkingArea, strip);
            if (hit.Width >= (int)(100 * s) && hit.Height >= (int)(20 * s)) return true;
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

    private bool _htmlFullscreen;           // 웹 페이지가 HTML 전체화면 상태
    private bool _titleHiddenByFocusOut;    // '다른 창 선택 시 테두리 안보이기'로 타이틀바를 숨긴 상태

    // 타이틀바/리사이즈 핸들 표시: 전체화면 중에는 모두 숨기고, 아니면 창 상태(일반일 때만 핸들)에 따른다
    private void ApplyChromeVisibility()
    {
        bool normal = WindowState == FormWindowState.Normal;
        pnlResizeLeft.Visible = pnlResizeRight.Visible = pnlResizeBottom.Visible = normal && !_htmlFullscreen;
        pnlTitleBar.Visible = !_htmlFullscreen && !_titleHiddenByFocusOut;
    }

    private void OnWindowStateChanged()
    {
        if (WindowState != FormWindowState.Minimized) _lastNonMinimizedState = WindowState;

        bool normal = WindowState == FormWindowState.Normal;
        ApplyChromeVisibility();   // 최대화 중엔 리사이즈 핸들 없음
        btnMaximize.Type = WindowState == FormWindowState.Maximized
            ? ControlButton.ButtonType.Restore
            : ControlButton.ButtonType.Maximize;
    }

    private void InitControls()
    {
        // 타이틀바 드래그 이동 (timerButton 제외 — 버튼 클릭과 충돌)
        pnlTitleBar.MouseDown += TitleBar_MouseDown;
        pnlTitleBar.MouseMove += TitleBar_MouseMove;
        // 타이틀바 빈 영역 더블클릭: 최대화 / 이전 크기 (가장자리 리사이즈 영역은 제외)
        pnlTitleBar.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && TitleBarEdgeHit(e.Location) == HTCLIENT) btnMaximize.PerformClick();
        };

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
        timerButton.Click += (_, _) =>
        {
            ToggleTimer();
            FocusWeb();   // 버튼에 포커스가 남아 영상의 Space 등 키를 가로채지 않게 한다
        };
        timerButton.RightClick += (_, _) => OpenAlarmSettings();
        SetTimerTip("클릭하여 타이머 시작\n우클릭: 알람 설정");
        // 스크린리더/UI Automation에 보이는 이름
        btnMenu.AccessibleName = "사이트 목록 및 설정 메뉴";
        timerButton.AccessibleName = "재획비 타이머";
        btnMinimize.AccessibleName = "최소화";
        btnMaximize.AccessibleName = "최대화 또는 이전 크기";
        btnClose.AccessibleName = "닫기";
        sliderVolume.AccessibleName = "볼륨";
        sliderOpacity.AccessibleName = "투명도";
        toolTip.SetToolTip(btnMenu, "사이트 목록 / 설정");
        toolTip.SetToolTip(btnMinimize, "최소화");
        toolTip.SetToolTip(btnMaximize, "최대화 / 이전 크기");
        toolTip.SetToolTip(btnClose, "닫기");
        toolTip.SetToolTip(sliderVolume, "볼륨");
        toolTip.SetToolTip(sliderOpacity, "투명도");
        toolTip.SetToolTip(lblVolumeValue, "숫자: 클릭하여 직접 입력 / 이름: 클릭하면 0(음소거), 다시 클릭하면 이전 값");
        toolTip.SetToolTip(lblOpacityValue, "숫자: 클릭하여 직접 입력 / 이름: 클릭하면 100, 다시 클릭하면 이전 값");

        // 볼륨
        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        UpdateValueLabels();
        sliderVolume.ValueChanged += (_, _) =>
        {
            UpdateValueLabels();
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            ScheduleSave();
        };
        lblVolumeValue.MouseClick += (_, e) => ValueLabel_MouseClick("volume", lblVolumeValue, e);
        lblVolumeValue.MouseMove += (_, e) => lblVolumeValue.Cursor = e.X >= NumberPartStart(lblVolumeValue) ? Cursors.IBeam : Cursors.Hand;
        sliderVolume.DragCompleted += (_, _) => FocusWeb();   // 마우스로 조절한 뒤 화살표 키가 영상 탐색으로 가게

        // 투명도 (30~100%)
        sliderOpacity.Value = Math.Clamp((int)Math.Round(_config.Opacity * 100), sliderOpacity.Minimum, sliderOpacity.Maximum);
        Opacity = sliderOpacity.Value / 100.0;
        UpdateValueLabels();
        sliderOpacity.ValueChanged += (_, _) =>
        {
            UpdateValueLabels();
            Opacity = sliderOpacity.Value / 100.0;
            _config.Opacity = Opacity;
            ScheduleSave();
        };
        lblOpacityValue.MouseClick += (_, e) => ValueLabel_MouseClick("opacity", lblOpacityValue, e);
        lblOpacityValue.MouseMove += (_, e) => lblOpacityValue.Cursor = e.X >= NumberPartStart(lblOpacityValue) ? Cursors.IBeam : Cursors.Hand;
        sliderOpacity.DragCompleted += (_, _) => FocusWeb();

        // 인라인 편집 TextBox
        tbInlineEdit.KeyDown += InlineEdit_KeyDown;
        tbInlineEdit.MaxLength = 3;
        tbInlineEdit.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar); // 숫자만 입력
        tbInlineEdit.TextChanged += (_, _) =>
        {
            // 붙여넣기(Ctrl+V)로 들어온 숫자 아닌 글자는 KeyPress 필터를 지나가므로 여기서 걸러 낸다
            string digits = new string(tbInlineEdit.Text.Where(char.IsAsciiDigit).ToArray());
            if (digits != tbInlineEdit.Text)
            {
                tbInlineEdit.Text = digits;           // TextChanged가 다시 들어와 아래 반영을 수행한다
                tbInlineEdit.SelectionStart = digits.Length;
                return;
            }
            ApplyInlineEditLive();
        };
        tbInlineEdit.LostFocus += (_, _) => CommitInlineEdit();

        // V버튼: 사이트 목록 + 사이트 추가 + 항상위/테두리숨김 드롭다운
        // (구버전의 menuStrip/siteList를 대체 — 매 클릭마다 최신 _config.Sites 기준으로 새로 빌드)
        TopMost = _config.TopMost;
        btnMenu.Click += (_, _) => OpenSiteMenu();

        // 알람 타이머 초기화 (시작은 안 함)
        _alarms.ResetAll(_config.CustomAlarms);
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
            _alarms.ResetAll(_config.CustomAlarms);
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

    // ===== 볼륨/투명도 라벨 클릭 =====
    // 라벨은 "볼륨 100"처럼 이름 + 숫자다. 숫자 부분을 누르면 직접 입력, 이름 부분을 누르면 볼륨은 0, 투명도는 100 ↔ 이전 값으로 전환한다.
    private int? _volumeRestore;    // 이름을 눌러 100으로 올리기 전의 값
    private int? _opacityRestore;

    // 볼륨/투명도 라벨 글자. 숫자를 편집하는 동안에는 이름만 그려서(입력 칸이 숫자 자리를 차지) 숫자가 겹쳐 보이지 않게 한다.
    // 접근성 이름(AccessibleName, UI Automation의 Name)은 항상 "볼륨 50"처럼 전체 값을 유지한다.
    private void UpdateValueLabels()
    {
        string vol = $"볼륨 {sliderVolume.Value}", opa = $"투명도 {sliderOpacity.Value}%";
        lblVolumeValue.AccessibleName = vol;
        lblOpacityValue.AccessibleName = opa;
        lblVolumeValue.Text = _inlineEditTarget == "volume" ? "볼륨 " : vol;
        lblOpacityValue.Text = _inlineEditTarget == "opacity" ? "투명도 " : opa;
    }

    // 라벨 안에서 숫자가 시작하는 x 좌표(이름 + 공백의 폭)
    private static int NumberPartStart(Label lbl)
    {
        int sp = lbl.Text.IndexOf(' ');
        if (sp < 0) return 0;
        return TextRenderer.MeasureText(lbl.Text[..(sp + 1)], lbl.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
    }

    private void ValueLabel_MouseClick(string target, Label lbl, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (e.X >= NumberPartStart(lbl)) { BeginInlineEdit(target); return; }

        if (target == "volume") ToggleTo(sliderVolume, sliderVolume.Minimum, ref _volumeRestore);   // 볼륨: 이름을 누르면 0(음소거)
        else ToggleTo(sliderOpacity, sliderOpacity.Maximum, ref _opacityRestore);                    // 투명도: 이름을 누르면 100
        FocusWeb();   // 슬라이더 조작과 같이 화살표 키가 영상 탐색으로 가게
    }

    // 목표 값(볼륨 0, 투명도 100)이 아니면 현재 값을 기억하고 목표 값으로, 이미 목표 값이고 기억해 둔 값이 있으면 원래 값으로 되돌린다.
    private static void ToggleTo(NSlider slider, int target, ref int? restore)
    {
        if (slider.Value != target)
        {
            restore = slider.Value;
            slider.Value = target;
        }
        else if (restore is int previous)
        {
            slider.Value = previous;
            restore = null;
        }
    }

    // ===== 인라인 숫자 편집 =====
    private void BeginInlineEdit(string target)
    {
        _inlineEditTarget = target;
        int value = target == "volume" ? sliderVolume.Value : sliderOpacity.Value;
        Label lbl = target == "volume" ? lblVolumeValue : lblOpacityValue;
        float s = DeviceDpi / 96f;
        int Px(int v) => (int)Math.Round(v * s);

        _inlineEditOriginal = value;
        tbInlineEdit.Text = value.ToString();
        // 이름("볼륨", "투명도")은 라벨에 남기고, 숫자 자리에는 같은 글꼴의 입력 칸을 둔다(라벨은 이름만 그려서 겹치지 않는다).
        tbInlineEdit.Font = lbl.Font;
        string widest = target == "volume" ? "100" : "100%";
        int numberLeft = NumberPartStart(lbl);
        tbInlineEdit.Width = TextRenderer.MeasureText(widest, tbInlineEdit.Font, Size.Empty, TextFormatFlags.NoPadding).Width + Px(14);   // 테두리 없는 TextBox도 안쪽 여백이 있어 글자가 잘리지 않게 넉넉히
        tbInlineEdit.Location = new Point(lbl.Left + numberLeft + Px(3), lbl.Top + (lbl.Height - tbInlineEdit.Height) / 2);
        tbInlineEdit.Visible = true;
        tbInlineEdit.BringToFront();   // 라벨의 남은 폭이 입력 칸의 앞쪽을 가리지 않게
        UpdateValueLabels();   // 라벨에서 숫자를 뺀다(AccessibleName은 유지)
        if (!Program.TestOffscreen) tbInlineEdit.Focus();   // 자동 테스트: SetFocus는 창을 활성화할 수 있어 건너뜀(입력은 메시지로 전달)
        tbInlineEdit.SelectAll();
    }

    // 입력하는 즉시 슬라이더에 반영한다. 범위를 벗어난 값은 가장 가까운 한계로 보정한다
    // (예: 투명도 최소 30이므로 "5"를 치면 30%, 이어서 "50"이 되면 50%). 빈 칸이면 그대로 둔다.
    private void ApplyInlineEditLive()
    {
        if (_inlineEditTarget is null || !tbInlineEdit.Visible) return;
        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        NSlider slider = _inlineEditTarget == "volume" ? sliderVolume : sliderOpacity;
        slider.Value = Math.Clamp(val, slider.Minimum, slider.Maximum);
    }

    private void CommitInlineEdit()
    {
        if (_inlineEditTarget is null) { EndInlineEditUi(); return; }

        string target = _inlineEditTarget;
        _inlineEditTarget = null; // Visible=false로 LostFocus가 다시 들어와도 중복 처리되지 않게 먼저 비운다
        EndInlineEditUi();

        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        if (target == "volume")
            sliderVolume.Value = Math.Clamp(val, sliderVolume.Minimum, sliderVolume.Maximum);
        else
            sliderOpacity.Value = Math.Clamp(val, sliderOpacity.Minimum, sliderOpacity.Maximum);
    }

    // 입력 칸을 닫고 숨겨 둔 라벨을 다시 보이게 한다
    private void EndInlineEditUi()
    {
        tbInlineEdit.Visible = false;
        lblVolumeValue.Visible = true;
        lblOpacityValue.Visible = true;
        UpdateValueLabels();
    }

    private void InlineEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { CommitInlineEdit(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape)
        {
            // 입력 중 이미 슬라이더에 반영됐으므로 원래 값으로 되돌린다
            string? target = _inlineEditTarget;
            _inlineEditTarget = null;
            EndInlineEditUi();
            if (target == "volume") sliderVolume.Value = _inlineEditOriginal;
            else if (target == "opacity") sliderOpacity.Value = _inlineEditOriginal;
            e.SuppressKeyPress = true;
        }
    }

    // ===== WebView2 =====
    private int _webViewInitializing;   // InitWV가 진행 중인 횟수(중첩 포함). 0보다 크면 시크릿 전환을 막는다

    private async Task InitWV()
    {
        _webViewInitializing++;
        try
        {
            // 시크릿 모드는 일반 모드와 폴더를 분리하고 InPrivate(디스크에 기록·로그인을 남기지 않는 프로필)로 연다.
            // 이전 시크릿 세션이 남긴 폴더는 시작할 때마다 지운다(비정상 종료 대비).
            bool priv = _config.PrivateMode;
            var wv = webView;
            DeleteDirectoryQuiet(PrivateCacheDir);
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: priv ? PrivateCacheDir : CacheDir);
                if (priv)
                {
                    var options = env.CreateCoreWebView2ControllerOptions();
                    options.IsInPrivateModeEnabled = true;
                    await wv.EnsureCoreWebView2Async(env, options);
                }
                else
                {
                    await wv.EnsureCoreWebView2Async(env);
                }
            }
            catch (Exception ex) when (priv && ReferenceEquals(wv, webView))
            {
                // 오래된 WebView2 런타임 등으로 시크릿 모드를 열지 못하면 일반 모드로 되돌린다(시크릿 시작 단계의 실패만)
                _config.PrivateMode = false;
                _config.Save();
                UpdatePrivateIndicator();
                ShowMessage($"시크릿 모드를 시작하지 못해 일반 모드로 돌아갑니다.\n{ex.GetType().Name}: {ex.Message}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await RecreateWebViewAsync();
                return;
            }
            // 초기화를 기다리는 동안 컨트롤이 바뀌었거나(전환) 닫혔다면 이 초기화는 버린다
            if (!ReferenceEquals(wv, webView) || wv.CoreWebView2 is null) return;

            webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess) return;
                InjectJS();
                if (_config.PrivateMode) return;   // 시크릿 모드는 방문한 주소를 설정 파일에도 남기지 않는다
                // 강제 종료/전원 차단에도 마지막으로 본 주소가 남도록 이동이 끝날 때마다 저장해 둔다(http/https만)
                string? current = webView.CoreWebView2?.Source;
                if (JHP.Api.Site.TryNormalizeUrl(current, out string normalized) && normalized != _config.LatestUrl)
                {
                    _config.LatestUrl = normalized;
                    ScheduleSave();
                }
            };
            // 링크 클릭으로 열리는 새 창(target=_blank)은 별도 창 대신 이 창에서 연다.
            // 크기/위치를 지정한 팝업(window.open 기능 지정: 로그인 등 opener가 필요한 창)은 기본 동작을 유지한다.
            webView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                if (e.WindowFeatures.HasSize || e.WindowFeatures.HasPosition) return;
                if (e.IsUserInitiated is false) { e.Handled = true; return; }   // 클릭 없이 스스로 뜨는 팝업(광고 등)은 차단
                if (!JHP.Api.Site.TryNormalizeUrl(e.Uri, out string target)) return;
                e.Handled = true;
                try { webView.CoreWebView2.Navigate(target); } catch { }
            };
            // 영상 사이트의 전체화면 버튼(HTML 전체화면): 영상이 창 전체를 쓰도록 타이틀바/리사이즈 핸들을 숨기고, 해제되면 되돌린다
            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
            {
                _htmlFullscreen = webView.CoreWebView2?.ContainsFullScreenElement ?? false;
                ApplyChromeVisibility();
            };
            // 렌더러 프로세스가 죽으면 빈 화면으로 남으므로 새로고침한다
            webView.CoreWebView2.ProcessFailed += (_, e) =>
            {
                // 종료 중이 아니고, 짧은 시간에 반복 실패해도 무한 새로고침되지 않도록 횟수를 제한한다.
                // 오래 켜 둔 상태에서 가끔 죽는 경우를 위해, 마지막 복구로부터 5분이 지나면 횟수를 다시 센다.
                if (IsDisposed || !IsHandleCreated) return;
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    // 브라우저 프로세스(런타임 업데이트/비정상 종료)는 컨트롤을 새로 만들어야 하므로 앱 재시작을 안내한다
                    ShowNotice("웹 화면이 종료되었습니다. JHP를 다시 실행해 주세요.");
                    return;
                }
                if (e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessExited) return;
                long now = Environment.TickCount64;
                if (now - _lastRecoveryMs > 5 * 60_000) _renderRecoveries = 0;
                _lastRecoveryMs = now;
                if (_renderRecoveries++ < 5)
                    BeginInvoke(() => webView.CoreWebView2?.Reload());
            };
            webView.CoreWebView2.Navigate(ResolveStartUrl());
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            DarkMessageBox.Show(this, $"WebView2 초기화 실패: {ex.GetType().Name}: {ex.Message}\n\n" +
                $"WebView2 런타임이 설치되어 있는지, 캐시 폴더에 쓸 수 있는지 확인해주세요.\n캐시 폴더: {CacheDir}",
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _webViewInitializing--;
        }
    }

    private static string CacheDir => Path.Combine(Config.DataDirectory, "cache");
    private static string PrivateCacheDir => Path.Combine(Config.DataDirectory, "cache-private");

    private static void DeleteDirectoryQuiet(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    // 브라우저 프로세스가 아직 파일을 잡고 있을 수 있어 몇 번 다시 시도한다. 끝내 못 지워도 다음 시작 때 지운다.
    private static async Task DeleteDirectoryWithRetryAsync(string dir)
    {
        for (int i = 0; i < 10; i++)
        {
            try { if (!Directory.Exists(dir)) return; Directory.Delete(dir, true); return; }
            catch { await Task.Delay(1000); }
        }
    }

    // WebView2는 프로필(사용자 데이터 폴더/InPrivate)을 바꾸려면 컨트롤을 새로 만들어야 한다. 타이머와 알람은 그대로 유지된다.
    private async Task RecreateWebViewAsync()
    {
        var old = webView;
        int index = Controls.GetChildIndex(old);
        var fresh = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(24, 24, 24) };
        SuspendLayout();
        Controls.Remove(old);
        Controls.Add(fresh);
        Controls.SetChildIndex(fresh, index);
        webView = fresh;
        ResumeLayout(true);
        try { old.Dispose(); } catch { }
        _alarmMuteActive = false;           // 이전 웹 화면에 걸었던 음소거 상태는 함께 사라졌다
        _htmlFullscreen = false;
        ApplyChromeVisibility();
        await InitWV();
    }

    // ▼ 메뉴 '시크릿 모드': 켜고 끌 때마다 웹 화면이 새로 시작되고 시크릿 세션의 데이터는 사라진다.
    private async void TogglePrivateMode()
    {
        if (_webViewInitializing > 0)
        {
            ShowMessage("웹 화면을 준비하는 중입니다. 잠시 후 다시 시도해 주세요.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool turnOn = !_config.PrivateMode;
        string text = turnOn
            ? "시크릿 모드를 켭니다.\n\n" +
              "• 웹 화면이 새로 시작됩니다(로그인되지 않은 상태로 시작 사이트로 이동). 타이머와 알람은 그대로 유지됩니다.\n" +
              "• 방문 기록, 로그인, 쿠키, 캐시가 남지 않고, 프로그램을 끄거나 시크릿 모드를 끄면 모두 사라집니다.\n" +
              "• 프로그램을 다시 켜도 시크릿 모드는 유지됩니다.\n\n켤까요?"
            : "시크릿 모드를 끕니다.\n\n시크릿 모드에서 쓴 로그인·기록·캐시는 모두 삭제되고, 일반 모드의 웹 화면(기존 로그인 유지)으로 돌아갑니다.\n\n끌까요?";
        if (ShowMessage(text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _config.PrivateMode = turnOn;
        _config.Save();
        UpdatePrivateIndicator();
        await RecreateWebViewAsync();
        if (!turnOn) _ = DeleteDirectoryWithRetryAsync(PrivateCacheDir);
    }

    private int _renderRecoveries;
    private long _lastRecoveryMs;

    private string ResolveStartUrl()
    {
        // 손상/수동 편집된 주소로 시작하다 예외가 나지 않도록 http(s) 주소만 사용한다
        // 시크릿 모드는 마지막 주소(일반 모드에서 본 곳)로 가지 않고 시작 사이트로 시작한다
        if (!_config.PrivateMode && JHP.Api.Site.TryNormalizeUrl(_config.LatestUrl, out string latest)) return latest;
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
            if (HostIs(host, "netflix.com"))
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
            if (string.Equals(_config.SkippedVersion, latest, StringComparison.OrdinalIgnoreCase)) return;
            // 예상한 저장소 주소만 연다 (응답이 비정상이어도 임의의 주소/경로를 실행하지 않음)
            if (!url.StartsWith("https://github.com/dlrj0/JHP", StringComparison.OrdinalIgnoreCase)) return;

            // 게임 등 다른 창을 쓰는 중에 대화상자가 포커스를 가져가 키 입력이 들어가지 않도록,
            // 이 창이 활성화되어 있을 때만 바로 띄우고 아니면 다음에 활성화될 때 띄운다.
            _pendingUpdate = (latest, url);
            if (ReferenceEquals(Form.ActiveForm, this)) ShowPendingUpdatePrompt();
        }
        catch { } // 업데이트 확인 실패가 앱 동작에 영향을 주지 않도록 한다
    }

    private (string Latest, string Url)? _pendingUpdate;

    private void ShowPendingUpdatePrompt()
    {
        if (_pendingUpdate is not { } update) return;
        _pendingUpdate = null;
        try
        {
            var answer = WithDialogState(() => DarkMessageBox.Show(this,
                $"새 버전 {update.Latest}이 있습니다.\n\n예: 다운로드 페이지 열기\n아니오: 이 버전은 다시 묻지 않음\n취소(Esc): 나중에",
                "업데이트 알림", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question));
            if (answer == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(update.Url) { UseShellExecute = true });
            else if (answer == DialogResult.No)
            {
                _config.SkippedVersion = update.Latest;
                ScheduleSave();
            }
        }
        catch { }
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
        // 모든 알람의 남은 시간은 각자 정상적으로 갱신/리셋한다. (계산은 AlarmScheduler)
        string? toFire = _alarms.Advance(steps, _config.AlarmEnabled, _config.CustomAlarms);

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
            SetNextText("--:--", Color.Gray);
            return;
        }

        int? min = _alarms.MinRemaining(_config.AlarmEnabled, _config.CustomAlarms);

        SetNextText(min is null ? "--:--" : AlarmScheduler.FormatRemaining(min.Value), Color.FromArgb(110, 220, 140));
        string tip = min is null
            ? "다음 알람: --:--:--"
            : $"다음 알람: {TimeSpan.FromSeconds(min.Value):hh\\:mm\\:ss}";
        SetTimerTip(tip + "\n클릭: 정지 / 우클릭: 알람 설정");
    }

    private void SetNextText(string text, Color color)
    {
        if (lblNext.Text != text) lblNext.Text = text;
        if (lblNext.ForeColor != color) lblNext.ForeColor = color;
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
        if (Program.TestOffscreen) form.Location = new Point(-20000, 0);   // Show 직후 (0,0)에 잠깐 뜨지 않게
        form.Show(this);
        // 배율이 적용된 뒤 크기를 기준으로 메인 창 가운데에 배치 (CenterParent는 배율 적용 전 크기로 계산돼 어긋남)
        var wa = Screen.FromControl(this).WorkingArea;
        int fx = Left + (Width - form.Width) / 2, fy = Top + (Height - form.Height) / 2;
        if (Program.TestOffscreen) form.Location = new Point(fx, fy);   // 자동 테스트: 메인 창을 따라 화면 밖에 두어 사용자 화면에 보이지 않게 한다
        else form.Location = new Point(                                 // 작은 화면/가장자리에서 버튼이 화면 밖으로 나가지 않게
            Math.Clamp(fx, wa.Left, Math.Max(wa.Left, wa.Right - form.Width)),
            Math.Clamp(fy, wa.Top, Math.Max(wa.Top, wa.Bottom - form.Height)));
    }

    private void ApplyAlarmSettings()
    {
        // 확인만 눌러도 진행 중인 알람이 처음부터 다시 세어지지 않도록, 설정창을 열 때와 달라진 항목만 리셋한다.
        _alarms.ApplyChanges(_alarmEnabledSnapshot, _customSnapshot, _config.AlarmEnabled, _config.CustomAlarms);

        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        UpdateValueLabels();
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
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("시크릿 모드 (기록·로그인 안 남김)", menu.Font).Width);
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("전역 단축키 (Ctrl+Alt+Shift+F10)", menu.Font).Width);
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

        var hotkeyItem = new ToolStripMenuItem("전역 단축키 (Ctrl+Alt+Shift+F10)")
        {
            ToolTipText = "다른 프로그램을 쓰는 중에도 Ctrl+Alt+Shift+F10으로 타이머를 시작/정지합니다(음성 안내)",
            CheckOnClick = true,
            Checked = _config.HotkeyEnabled,
            AutoSize = false,
            Size = new Size(itemWidth, itemHeight)
        };
        hotkeyItem.CheckedChanged += (_, _) =>
        {
            _config.HotkeyEnabled = hotkeyItem.Checked;
            ApplyHotkey();
            _config.Save();
        };
        menu.Items.Add(hotkeyItem);

        // 체크 표시는 현재 상태를 보여 주기만 한다. 실제 전환은 확인 후(웹 화면을 새로 만든다) TogglePrivateMode가 한다.
        var privateItem = new ToolStripMenuItem("시크릿 모드 (기록·로그인 안 남김)")
        {
            Checked = _config.PrivateMode,
            AutoSize = false,
            Size = new Size(itemWidth, itemHeight)
        };
        privateItem.Click += (_, _) => BeginInvoke(TogglePrivateMode);
        menu.Items.Add(privateItem);
        menu.Items.Add(new ToolStripSeparator());   // 토글 옵션 | 설정 백업/복원 | 사이트 목록

        var alarmItem = new ToolStripMenuItem("알람 설정…") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        alarmItem.Click += (_, _) => BeginInvoke(OpenAlarmSettings);   // 타이머 버튼 우클릭과 같은 동작(키보드/스크린리더용)
        menu.Items.Add(alarmItem);

        var exportItem = new ToolStripMenuItem("설정 내보내기…") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        exportItem.Click += (_, _) => BeginInvoke(ExportSettings);
        menu.Items.Add(exportItem);
        var importItem = new ToolStripMenuItem("설정 가져오기…") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        importItem.Click += (_, _) => BeginInvoke(ImportSettings);
        menu.Items.Add(importItem);

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
                // 우측 끝을 잘못 눌러 바로 지워지지 않도록 확인한다(메뉴를 닫고 확인창을 띄운 뒤 목록을 다시 연다)
                menu.Close();
                BeginInvoke(() =>
                {
                    ConfirmAndDeleteSite(site);
                    OpenSiteMenu();
                });
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var addItem = new ToolStripMenuItem("＋ 사이트 추가") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        addItem.Click += (_, _) => AddSite();
        menu.Items.Add(addItem);

        menu.Items.Add(new ToolStripSeparator());

        // JHP 설정(사이트 목록, 알람 등)은 그대로 두고 웹 화면(브라우저)의 기록만 지운다
        var clearItem = new ToolStripMenuItem("브라우저 기록 삭제…") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        clearItem.Click += (_, _) => BeginInvoke(ClearBrowserData);
        menu.Items.Add(clearItem);

        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(btnMenu, new Point(0, btnMenu.Height));
    }

    // 크롬처럼 지울 항목(방문/다운로드 기록, 캐시, 자동완성, 쿠키·사이트 데이터=로그인, 비밀번호, 사이트 설정)을 골라 지운다.
    // JHP 설정(config.json: 사이트 목록·알람 등)은 어떤 항목을 골라도 건드리지 않는다.
    private async void ClearBrowserData()
    {
        var profile = webView.CoreWebView2?.Profile;
        if (profile is null) return;

        CoreWebView2BrowsingDataKinds kinds;
        bool wasTopMost = TopMost;
        TopMost = false;
        _modalDepth++;
        try
        {
            using var dlg = new BrowserDataForm();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            kinds = dlg.Result;
        }
        finally
        {
            _modalDepth--;
            TopMost = wasTopMost;
        }

        try
        {
            await profile.ClearBrowsingDataAsync(kinds);
            ShowMessage("선택한 항목을 삭제했습니다.", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowMessage($"브라우저 기록을 삭제하지 못했습니다.\n{ex.Message}",MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // 같은 폴더에서 JHP를 다시 실행했을 때 호출된다: 최소화돼 있으면 직전 상태(일반/최대화)로 복원하고 앞으로 가져온다.
    internal void BringToFrontFromAnotherInstance()
    {
        if (IsDisposed) return;
        if (Program.TestOffscreen) return;   // 자동 테스트 중에는 화면 밖 창을 앞으로 가져오지 않는다(포커스 이동 방지)
        if (WindowState == FormWindowState.Minimized) WindowState = _lastNonMinimizedState;
        Activate();
    }

    // ===== 전역 단축키 (옵트인) =====
    // 다른 프로그램을 쓰는 중에도 Ctrl+Alt+Shift+F10으로 타이머를 시작/정지한다. 기본은 꺼짐이며 ▼ 메뉴에서 켠다.
    // 게임 조작(Ctrl 공격, Alt 점프, 스킬 키)과 겹쳐 입력이 가로채이지 않도록 수식키 3개 + 기능키 조합을 쓴다.
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x4A48;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    private const uint VK_HOTKEY = 0x79;   // VK_F10

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void ApplyHotkey()
    {
        if (!IsHandleCreated) return;
        UnregisterHotKey(Handle, HotkeyId);
        if (!_config.HotkeyEnabled) return;

        if (!RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_HOTKEY))
        {
            // 다른 프로그램이 이미 쓰는 조합이면 켜지지 않는다 — 설정도 꺼 둔다
            _config.HotkeyEnabled = false;
            _config.Save();
            ShowMessage("전역 단축키 Ctrl+Alt+Shift+F10을 등록하지 못했습니다. 다른 프로그램이 이미 사용 중일 수 있습니다.",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            BeginInvoke(() =>
            {
                ToggleTimer();
                // 화면을 보지 않고 눌러도 알 수 있게 짧은 음성으로 알린다
                Synth.Instance.TTS(_timerRunning ? "타이머 시작" : "타이머 정지", _config.Volume, _config.Rate);
            });
            return;
        }
        // 최대화된 채 다른 모니터로 옮기거나 작업 표시줄이 바뀌어도 현재 모니터의 작업 영역에 맞추도록, 최대화 크기를 묻는 시점에 갱신한다.
        if (m.Msg == WM_GETMINMAXINFO && IsHandleCreated)
        {
            try { UpdateMaximizedBounds(); } catch { }
        }
        base.WndProc(ref m);
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    // ===== 설정 내보내기/가져오기 =====
    // 파일 대화상자를 띄우는 동안 항상 위 상태면 대화상자가 가려질 수 있어 임시로 해제한다 (ShowSiteDialog와 같은 방식).
    private T WithDialogState<T>(Func<T> show)
    {
        bool wasTopMost = TopMost;
        TopMost = false;
        _modalDepth++;
        try { return show(); }
        finally
        {
            _modalDepth--;
            TopMost = wasTopMost;
        }
    }

    private void ExportSettings()
    {
        string? path = WithDialogState(() =>
        {
            using var dlg = new SaveFileDialog
            {
                Title = "설정 내보내기",
                Filter = "JHP 설정 (*.json)|*.json",
                FileName = "jhp-config.json",
                OverwritePrompt = true
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        });
        if (path is null) return;

        // 현재 창 상태를 반영해서 내보낸다. 주소의 쿼리/프래그먼트에는 토큰 등이 들어 있을 수 있어 공유 파일에는 남기지 않는다.
        _config.Opacity = Opacity;
        _config.TopMost = TopMost;
        string keptUrl = _config.LatestUrl;
        _config.LatestUrl = Uri.TryCreate(keptUrl, UriKind.Absolute, out var u) ? u.GetLeftPart(UriPartial.Path) : "";
        bool ok;
        try { ok = _config.ExportTo(path); }
        finally { _config.LatestUrl = keptUrl; }
        ShowMessage(ok ? "설정을 내보냈습니다." : "설정을 내보내지 못했습니다. 저장 위치를 확인해주세요.",
            MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private DialogResult ShowMessage(string text, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        WithDialogState(() => DarkMessageBox.Show(this, text, "JHP", buttons, icon));

    private void ImportSettings()
    {
        string? path = WithDialogState(() =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "설정 가져오기",
                Filter = "JHP 설정 (*.json)|*.json",
                CheckFileExists = true
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        });
        if (path is null) return;

        var imported = Config.ReadFrom(path);
        if (imported is null || imported.Sites.Count == 0)
        {
            ShowMessage("올바른 JHP 설정 파일이 아닙니다.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (ShowMessage("현재 설정(사이트 목록, 알람, 볼륨 등)을 가져온 설정으로 바꿉니다. 계속할까요?",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        // 알람 설정창이 열려 있으면 먼저 닫는다(확인되지 않은 채로 닫히므로 오래된 값이 다시 저장되지 않음)
        _alarmForm?.Close();
        _alarmEnabledSnapshot = null;
        _customSnapshot = null;

        // 창 위치/크기와 현재 주소는 지금 상태를 유지하고, 나머지를 교체한다
        imported.X = _config.X; imported.Y = _config.Y;
        imported.Width = _config.Width; imported.Height = _config.Height;
        imported.IsMaximize = _config.IsMaximize;
        imported.LatestUrl = _config.LatestUrl;
        imported.PrivateMode = _config.PrivateMode;   // 가져온 파일이 시크릿 모드를 켜거나 끄지 않는다(웹 화면을 다시 만들어야 하므로)
        _config.Replace(imported);

        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        sliderOpacity.Value = Math.Clamp((int)Math.Round(_config.Opacity * 100), sliderOpacity.Minimum, sliderOpacity.Maximum);
        TopMost = _config.TopMost;
        ApplyHotkey();
        Synth.Instance.SetRate(_config.Rate);
        _alarms.ResetAll(_config.CustomAlarms);
        UpdateNextAlarmLabel();
        _config.Save();
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
        menu.Items.Add(new ToolStripSeparator());
        Add("삭제…", true, () => ConfirmAndDeleteSite(site));

        // 이동/지정/수정을 하면(취소 포함) ▼ 메뉴를 다시 열어 갱신된 목록을 보여 준다. 메뉴 밖을 눌러 닫은 경우는 열지 않음.
        menu.Closed += (_, _) => BeginInvoke(() =>
        {
            menu.Dispose();
            if (acted) OpenSiteMenu();
        });
        menu.Show(screenPos);
    }

    // 확인 후 사이트를 목록에서 지운다. 지웠으면 true.
    private bool ConfirmAndDeleteSite(JHP.Api.Site site)
    {
        if (ShowMessage($"'{site.Name}' 사이트를 목록에서 삭제할까요?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;
        _config.Sites.Remove(site);
        // 시작 사이트를 지웠으면(같은 이름이 남아 있지 않으면) 지정을 해제해 이름만 남지 않게 한다
        if (site.Name == _config.DefaultSite && !_config.Sites.Any(s => s.Name == site.Name))
            _config.DefaultSite = "";
        _config.Save();
        return true;
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
            DarkMessageBox.Show(this, $"'{site.Name}'의 주소가 올바르지 않습니다.\n{site.Url}", "JHP",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            webView.CoreWebView2.Navigate(url);
            FocusWeb();   // ▼ 버튼에 포커스가 남아 Space/방향키가 메뉴로 가지 않게 웹 화면으로 돌려 준다
        }
        catch (Exception ex) { DarkMessageBox.Show(this, ex.Message, "JHP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // V버튼 드롭다운 전용 사이트 항목: 일반 ToolStripMenuItem이지만 우측 끝(✕ 영역) 클릭 시
    // 이동 대신 삭제를 수행하도록 OnMouseUp을 가로챈다. SiteMenuRenderer가 그리는 ✕ 아이콘과 짝을 이룸.
    private sealed class SiteMenuItem : ToolStripMenuItem
    {
        public static int DeleteZoneWidth = 28; // OpenSiteMenu()에서 DPI 배율로 갱신

        public JHP.Api.Site SiteInfo { get; }
        public event EventHandler? DeleteRequested;
        public event EventHandler? ManageRequested;

        public SiteMenuItem(Site site, string displayText) : base(displayText.Replace("&", "&&"))   // 이름의 &가 밑줄 단축키로 처리되지 않게
        {
            SiteInfo = site;
        }

        // WinForms는 메뉴 항목의 우클릭에도 Click 이벤트를 발생시킨다(OnMouseUp에서 return해도 막지 못함).
        // 그래서 우클릭/✕ 클릭이면 이어서 오는 Click을 직접 버린다 → 사이트 이동이 일어나지 않는다.
        private bool _suppressClick;

        protected override void OnMouseDown(MouseEventArgs mea)
        {
            _suppressClick = false;
            base.OnMouseDown(mea);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_suppressClick) { _suppressClick = false; return; }
            base.OnClick(e);
        }

        protected override void OnMouseUp(MouseEventArgs mea)
        {
            if (mea.Button == MouseButtons.Right)
            {
                _suppressClick = true;
                ManageRequested?.Invoke(this, EventArgs.Empty);
                return; // 우클릭은 이동(Click) 없이 관리 메뉴만 연다
            }
            if (mea.Button == MouseButtons.Left && mea.X >= Width - DeleteZoneWidth)
            {
                _suppressClick = true;
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
        if (btnMinimize is null || sliderOpacity is null || lblNext is null) return;

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

        // 타이머 버튼 오른쪽: 다음 알람 남은 시간(h:mm:ss까지 들어가는 고정 폭)
        int nextW = TextRenderer.MeasureText("0:00:00", lblNext.Font).Width + Px(4);
        lblNext.Left = timerButton.Right + Px(8);
        lblNext.Top = (h - lblNext.Height) / 2;
        int startX = lblNext.Left + nextW + Px(12);
        if (lblPrivate is not null && lblPrivate.Visible)
        {
            lblPrivate.Left = startX;
            lblPrivate.Top = (h - lblPrivate.Height) / 2;
            startX += lblPrivate.Width + Px(12);
        }
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
        if (_config.IsHideWindowBorderOnFocusOut || _titleHiddenByFocusOut)
        {
            _titleHiddenByFocusOut = false;
            ApplyChromeVisibility();
        }
        if (_pendingUpdate is not null) BeginInvoke(ShowPendingUpdatePrompt);   // 활성화 처리가 끝난 뒤에 띄운다
    }

    private void Form1_Deactivate(object? sender, EventArgs e)
    {
        if (!_config.IsHideWindowBorderOnFocusOut) return;
        // 알람 설정창/사이트 추가창을 쓰는 동안(메인 창이 비활성화됨)에는 타이틀바를 숨기지 않는다
        if (_modalDepth > 0 || _alarmForm is { IsDisposed: false }) return;
        _titleHiddenByFocusOut = true;
        ApplyChromeVisibility();
    }

    // ===== 종료 시 설정 저장 =====
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        if (IsHandleCreated) UnregisterHotKey(Handle, HotkeyId);
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
            _config.Width = (int)Math.Round(bounds.Width / s);   // 절삭하면 배율 환산을 거치며 1px씩 줄어든다
            _config.Height = (int)Math.Round(bounds.Height / s);
        }

        // 최소화한 채 종료해도 직전 상태(최대화 여부)를 유지한다
        _config.IsMaximize = (WindowState == FormWindowState.Minimized ? _lastNonMinimizedState : WindowState) == FormWindowState.Maximized;
        _config.TopMost = TopMost;
        _config.Opacity = Opacity;

        // 오류 페이지(chrome-error://)나 about:blank가 저장돼 다음 실행이 빈 화면이 되지 않도록 http(s)만 저장한다
        if (!_config.PrivateMode && webView.CoreWebView2 is not null && JHP.Api.Site.TryNormalizeUrl(webView.CoreWebView2.Source, out string src))
            _config.LatestUrl = src;

        _config.Save();

        // 시크릿 모드로 쓰던 웹 화면을 닫고 전용 폴더를 지운다(브라우저 프로세스가 아직 잡고 있으면 다음 시작 때 지워진다)
        if (_config.PrivateMode)
        {
            try { webView.Dispose(); } catch { }
            DeleteDirectoryQuiet(PrivateCacheDir);
        }
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
