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

    public Form1()
    {
        InitializeComponent();
        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
        Activated += Form1_Activated;
        Deactivate += Form1_Deactivate;
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

        if (IsOnScreen(_config.X, _config.Y))
            Location = new Point(_config.X, _config.Y);

        LayoutTitleBar();

        // 타이머는 자동 시작 안 함 — 사용자가 timerButton 클릭 시 시작 (ToggleTimer에서만 timer.Start())

        await InitWV();
        CheckUpdateAsync();

        if (_config.IsMaximize)
            WindowState = FormWindowState.Maximized;
    }

    private static bool IsOnScreen(int x, int y)
    {
        foreach (Screen screen in Screen.AllScreens)
            if (screen.WorkingArea.Contains(x, y)) return true;
        return false;
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
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        btnClose.Click += (_, _) => Close();

        // timerButton: 좌클릭=시작/정지, 우클릭=알람설정
        timerButton.Click += (_, _) => ToggleTimer();
        timerButton.RightClick += (_, _) => OpenAlarmSettings();
        toolTip.SetToolTip(timerButton, "클릭하여 타이머 시작");

        // 볼륨
        sliderVolume.Value = Math.Clamp(_config.Volume, sliderVolume.Minimum, sliderVolume.Maximum);
        lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
        sliderVolume.ValueChanged += (_, _) =>
        {
            lblVolumeValue.Text = $"볼륨 {sliderVolume.Value}";
            _config.Volume = sliderVolume.Value;
            Synth.Instance.SetVolume(sliderVolume.Value);
            _config.Save();
        };
        lblVolumeValue.Click += (_, _) => BeginInlineEdit("volume");

        // 투명도 (30~100%)
        sliderOpacity.Value = Math.Clamp((int)(_config.Opacity * 100), sliderOpacity.Minimum, sliderOpacity.Maximum);
        Opacity = sliderOpacity.Value / 100.0;
        lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
        sliderOpacity.ValueChanged += (_, _) =>
        {
            lblOpacityValue.Text = $"투명도 {sliderOpacity.Value}%";
            Opacity = sliderOpacity.Value / 100.0;
            _config.Opacity = Opacity;
            _config.Save();
        };
        lblOpacityValue.Click += (_, _) => BeginInlineEdit("opacity");

        // 인라인 편집 TextBox
        tbInlineEdit.KeyDown += InlineEdit_KeyDown;
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

    // ===== 타이머 시작/정지 토글 =====
    private void ToggleTimer()
    {
        _timerRunning = !_timerRunning;

        if (_timerRunning)
        {
            // 시작 시 항상 리셋
            for (int i = 0; i < 8; i++) _remaining[i] = AlarmSeconds[i];
            for (int i = 0; i < 3; i++) _customRemaining[i] = _config.CustomAlarms[i].Tick;
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

        tbInlineEdit.Text = value.ToString();
        tbInlineEdit.Location = new Point(lbl.Left, lbl.Top - 1);
        tbInlineEdit.Visible = true;
        tbInlineEdit.Focus();
        tbInlineEdit.SelectAll();
    }

    private void CommitInlineEdit()
    {
        tbInlineEdit.Visible = false;
        if (_inlineEditTarget is null) return;

        if (!int.TryParse(tbInlineEdit.Text, out int val)) return;

        if (_inlineEditTarget == "volume")
            sliderVolume.Value = Math.Clamp(val, sliderVolume.Minimum, sliderVolume.Maximum);
        else
            sliderOpacity.Value = Math.Clamp(val, sliderOpacity.Minimum, sliderOpacity.Maximum);

        _inlineEditTarget = null;
    }

    private void InlineEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { CommitInlineEdit(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape) { tbInlineEdit.Visible = false; _inlineEditTarget = null; }
    }

    // ===== WebView2 =====
    private async Task InitWV()
    {
        try
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: cacheDir);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.NavigationCompleted += (_, _) => InjectJS();
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
        if (!string.IsNullOrWhiteSpace(_config.LatestUrl)) return _config.LatestUrl;
        var defaultSite = _config.Sites.FirstOrDefault(s => s.Name == _config.DefaultSite);
        if (defaultSite is not null) return defaultSite.Url;
        return _config.Sites.Count > 0 ? _config.Sites[0].Url : "https://www.naver.com";
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
            if (host.Contains("laftel.net"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.LaftelSkipNext);
            else if (host.Contains("netflix.com"))
                await webView.CoreWebView2.ExecuteScriptAsync(UserScripts.NetflixSkipNext);
        }
        catch { }
    }

    private async void CheckUpdateAsync()
    {
        var (hasUpdate, latest, url) = await UpdateChecker.Check(CurrentVersion);
        if (!hasUpdate) return;
        if (Prompt.ShowDialog("업데이트 알림", $"새 버전 {latest}이 있습니다. 다운로드 페이지를 여시겠습니까?"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ===== 알람 타이머 =====
    private void Timer_Tick(object? sender, EventArgs e)
    {
        // 정지 상태면 Tick 무시 (이중 안전장치)
        if (!_timerRunning) return;

        // 같은 순간에 여러 알람이 겹치면(예: 2시간 시점에 1시간/30분/... 동시 도달) 목록 위쪽 하나만 울린다.
        // 모든 알람의 남은 시간은 각자 정상적으로 갱신/리셋한다.
        string? toFire = null;

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

        if (toFire is not null) FireAlarm(toFire);

        UpdateNextAlarmLabel();
    }

    private void FireAlarm(string name)
    {
        if (_config.Tts)
            Synth.Instance.TTS(name.Replace("/", ", "), _config.Volume, _config.Rate);
        else
            Synth.Instance.Ring(_config.AlarmName, _config.Volume);
    }

    private void UpdateNextAlarmLabel()
    {
        if (!_timerRunning)
        {
            toolTip.SetToolTip(timerButton, "클릭하여 타이머 시작");
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
        toolTip.SetToolTip(timerButton, tip);
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
        for (int i = 0; i < 8; i++)
            if (_config.AlarmEnabled[i]) _remaining[i] = AlarmSeconds[i];
        for (int i = 0; i < 3; i++)
            if (_config.CustomAlarms[i].Enabled) _customRemaining[i] = _config.CustomAlarms[i].Tick;

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
        foreach (var site in _config.Sites)
            textWidth = Math.Max(textWidth, TextRenderer.MeasureText(site.Name, menu.Font).Width);
        textWidth = Math.Max(textWidth, TextRenderer.MeasureText("다른 창 선택 시 테두리 안보이기", menu.Font).Width);
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

        if (_config.Sites.Count > 0)
            menu.Items.Add(new ToolStripSeparator());

        foreach (var site in _config.Sites)
        {
            var item = new SiteMenuItem(site)
            {
                AutoSize = false,
                Size = new Size(itemWidth, itemHeight),
                Checked = string.Equals(site.Url, currentUrl, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => NavigateToSite(site);
            item.DeleteRequested += (_, _) =>
            {
                _config.Sites.Remove(site);
                _config.Save();
                menu.Items.Remove(item);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var addItem = new ToolStripMenuItem("＋ 사이트 추가") { AutoSize = false, Size = new Size(itemWidth, itemHeight) };
        addItem.Click += (_, _) => AddSite();
        menu.Items.Add(addItem);

        menu.Show(btnMenu, new Point(0, btnMenu.Height));
    }

    // ===== 사이트 목록 =====
    // 🔴 버그 수정: OpenAlarmSettings와 동일한 TopMost 자식창 버그 수정 적용
    private void AddSite()
    {
        bool wasTopMost = TopMost;
        TopMost = false;
        using var form = new SiteForm();
        bool ok = form.ShowDialog(this) == DialogResult.OK && form.Result is not null;
        TopMost = wasTopMost;
        if (!ok) return;

        _config.Sites.Add(form.Result!);
        _config.Save();
    }

    private void NavigateToSite(Site site)
    {
        if (webView.CoreWebView2 is not null)
            webView.CoreWebView2.Navigate(site.Url);
    }

    // V버튼 드롭다운 전용 사이트 항목: 일반 ToolStripMenuItem이지만 우측 끝(✕ 영역) 클릭 시
    // 이동 대신 삭제를 수행하도록 OnMouseUp을 가로챈다. SiteMenuRenderer가 그리는 ✕ 아이콘과 짝을 이룸.
    private sealed class SiteMenuItem : ToolStripMenuItem
    {
        public static int DeleteZoneWidth = 28; // OpenSiteMenu()에서 DPI 배율로 갱신

        public Site Site { get; }
        public event EventHandler? DeleteRequested;

        public SiteMenuItem(Site site) : base(site.Name)
        {
            Site = site;
        }

        protected override void OnMouseUp(MouseEventArgs mea)
        {
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
            const int r = 4;

            Color color = item.Selected ? Color.FromArgb(220, 90, 80) : Color.FromArgb(150, 150, 150);
            using var pen = new Pen(color, 1.4f);
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
        int gap = Px(8), groupGap = Px(16), rightPad = Px(12), minSlider = Px(60), maxSlider = Px(170);
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
        pnlTitleBar.Visible = false;
    }

    // ===== 종료 시 설정 저장 =====
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        Synth.Instance.Stop();

        if (WindowState == FormWindowState.Normal)
        {
            _config.X = Location.X;
            _config.Y = Location.Y;
            float s = DeviceDpi / 96f;
            _config.Width = (int)(Size.Width / s);
            _config.Height = (int)(Size.Height / s);
        }

        _config.IsMaximize = WindowState == FormWindowState.Maximized;
        _config.TopMost = TopMost;
        _config.Opacity = Opacity;

        if (webView.CoreWebView2 is not null)
            _config.LatestUrl = webView.CoreWebView2.Source;

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
