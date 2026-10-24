using JHP.Api;
using JHP.Controls;
using System.Runtime.InteropServices;

namespace JHP;

public class AlarmForm : Form
{
    // 원본 재획플레이어의 항목명 (시간별 용도). TTS로 읽는 이름과 같다.
    private static readonly string[] AlarmLabels =
    [
        "재획비 (2시간)", "경쿠 (1시간)", "경뿌/경쿠/유니온 (30분)", "유니온 버프 (20분)",
        "경쿠 (15분)", "유니온 버프 (10분)", "메소 회수 (100초)", "파운틴 (55초)"
    ];

    // 입력 칸 배경 (사이트 추가창과 같은 값)
    private static readonly Color InputBack = Color.FromArgb(50, 50, 50);

    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];

    private Panel _pnlTitle = null!;
    private ComboBox _tbAlarmName = null!;
    private CheckBox _cbTts = null!;
    private NSlider _tbVolume = null!;
    private NSlider _tbRate = null!;
    private Label _lblVolume = null!;
    private Label _lblRate = null!;
    private Button _btnPlay = null!;
    private Button _btnSample = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    // 확인 버튼으로 닫혔는지 (비모달 폼이라 DialogResult 대신 사용)
    public bool Accepted { get; private set; }

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


    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Text = "알람 설정";
        ShowInTaskbar = false;   // 메인 창의 보조 창이므로 작업 표시줄에 별도 항목을 만들지 않는다
        Icon = Program.AppIcon ?? Icon;
        Width = 600;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        MinimumSize = new Size(480, 360);

        // ===== 자체 타이틀바 =====
        _pnlTitle = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        var lblCaption = new Label
        {
            Text = "알람 설정",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 7)
        };

        var btnClose = new ControlButton
        {
            // 위치는 _pnlTitle.Resize에서 계산 (Anchor는 패널 기본 폭 기준이라 어긋날 수 있음)
            Location = new Point(_pnlTitle.Width - 46, 1),
            Type = ControlButton.ButtonType.Close,
            AccessibleName = "닫기"
        };
        btnClose.Click += (_, _) => Close();
        _pnlTitle.Resize += (_, _) => btnClose.Left = _pnlTitle.Width - btnClose.Width;

        _pnlTitle.Controls.Add(lblCaption);
        _pnlTitle.Controls.Add(btnClose);
        _pnlTitle.MouseDown += TitleBar_MouseDown;
        lblCaption.MouseDown += TitleBar_MouseDown;

        Controls.Add(_pnlTitle);

        // ===== 본문 =====
        int y = 44; // 타이틀바 아래에서 시작

        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 2) * 290,
                Top = y + (i / 2) * 28,
                Width = 280,
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 4 * 28 + 8;

        AddLabel("알람 소리", 12, y + 3, 90);
        // alarm 폴더의 mp3 목록을 드롭다운으로 보여주고, ▷ 버튼으로 미리 들어볼 수 있다 (직접 입력도 가능).
        _tbAlarmName = new ComboBox
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120 - 46,
            DropDownStyle = ComboBoxStyle.DropDown,
            FlatStyle = FlatStyle.Flat,
            BackColor = InputBack,
            ForeColor = Color.White
        };
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "alarm");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.mp3").OrderBy(f => f))
                    _tbAlarmName.Items.Add(Path.GetFileName(f));
        }
        catch { }
        Controls.Add(_tbAlarmName);

        var btnPlay = new Button
        {
            Text = "▷",
            Left = ClientSize.Width - 12 - 40, Top = y - 1, Width = 40, Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            AccessibleName = "알람 소리 미리 듣기"
        };
        _btnPlay = btnPlay;
        btnPlay.FlatAppearance.BorderSize = 0;
        btnPlay.Click += (_, _) => Synth.Instance.Ring(_tbAlarmName.Text, _tbVolume.Value);
        new ToolTip().SetToolTip(btnPlay, "알람 소리 미리 듣기");
        Controls.Add(btnPlay);
        y += 34;

        _cbTts = new CheckBox { Text = "TTS 출력 (선택 시 소리재생 X)", Left = 12, Top = y, Width = 330, ForeColor = Color.White };
        Controls.Add(_cbTts);

        var btnSample = new Button
        {
            Text = "TTS 샘플",
            Left = ClientSize.Width - 12 - 110, Top = y - 2, Width = 110, Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
        };
        _btnSample = btnSample;
        btnSample.FlatAppearance.BorderSize = 0;
        btnSample.Click += (_, _) => Synth.Instance.TTS("재획비", _tbVolume.Value, _tbRate.Value);
        Controls.Add(btnSample);
        y += 30;

        _lblVolume = AddLabel("볼륨: 0", 12, y + 4, 90);
        _tbVolume = new NSlider
        {
            Left = 108, Top = y + 4, Width = ClientSize.Width - 120,
            Minimum = 0, Maximum = 100,
            AccessibleName = "볼륨"
        };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 34;

        _lblRate = AddLabel("TTS 속도: 0", 12, y + 4, 90);
        _tbRate = new NSlider
        {
            Left = 108, Top = y + 4, Width = ClientSize.Width - 120,
            Minimum = -10, Maximum = 10,
            AccessibleName = "TTS 속도"
        };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"TTS 속도: {_tbRate.Value}";
        Controls.Add(_tbRate);
        y += 34;

        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20, AccessibleName = $"커스텀 알람 {i + 1} 사용" };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36, Top = y, Width = 190, PlaceholderText = "알람 이름", AccessibleName = $"커스텀 알람 {i + 1} 이름",
                BackColor = InputBack,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 234, Top = y, Width = 80, Minimum = 0, Maximum = 99999, AccessibleName = $"커스텀 알람 {i + 1} 주기(초)",
                BackColor = InputBack,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_customTicks[i]);

            AddLabel("초", 320, y + 3, 30);
            y += 32;
        }

        y += 8;

        var btnOk = new Button
        {
            Text = "확인",
            Left = ClientSize.Width - 180,
            Top = y,
            Width = 80,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White
        };
        _btnOk = btnOk;
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        btnOk.Click += (_, _) => Close();
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "취소",
            Left = ClientSize.Width - 92,
            Top = y,
            Width = 80,

            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
        };
        _btnCancel = btnCancel;
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) => Close();
        Controls.Add(btnCancel);

        Height = y + 46; // 버튼(약 27px) 아래 여백 ~19px
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        // 배율 적용은 자식 컨트롤을 모두 추가한 뒤에 해야 한다(앞에서 하면 빈 폼에만 적용됨). 좌표는 96dpi 기준.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(false);

        // 오른쪽/아래 정렬 컨트롤은 Anchor 대신 직접 배치한다 (Anchor는 부모 기본 크기 기준이라 배율에서 어긋남)
        Resize += (_, _) => LayoutRightAligned();
        Shown += (_, _) =>
        {
            LayoutRightAligned();
            // 내용 높이보다 작게 줄이면 컨트롤이 겹치므로 세로 크기는 고정한다(가로만 조절)
            MinimumSize = new Size(MinimumSize.Width, Height);
            MaximumSize = new Size(16000, Height);   // 너비 0은 "제한 없음"이 아니라 폭 0으로 적용되는 경우가 있어 충분히 큰 값을 쓴다
        };
    }

    // 어두운 사이트 위에서도 창 경계가 보이도록 1px 테두리
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(80, 80, 80));
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    // 폼 크기가 바뀌거나 배율이 적용된 뒤: 오른쪽 끝 정렬 컨트롤의 위치/폭과 하단 버튼 위치를 다시 계산한다 (좌표는 96dpi 기준 → s 배)
    private void LayoutRightAligned()
    {
        if (_btnOk is null) return;
        float s = DeviceDpi / 96f;
        int Px(int v) => (int)Math.Round(v * s);
        int right = ClientSize.Width - Px(12);

        _btnPlay.Left = right - _btnPlay.Width;
        _tbAlarmName.Width = Math.Max(Px(60), _btnPlay.Left - Px(6) - _tbAlarmName.Left);
        _btnSample.Left = right - _btnSample.Width;
        _tbVolume.Width = Math.Max(Px(60), right - _tbVolume.Left);
        _tbRate.Width = Math.Max(Px(60), right - _tbRate.Left);

        _btnCancel.Left = right - _btnCancel.Width;
        _btnOk.Left = _btnCancel.Left - Px(8) - _btnOk.Width;
        int top = ClientSize.Height - Px(46);
        _btnOk.Top = _btnCancel.Top = top;
    }

    private Label AddLabel(string text, int left, int top, int width)
    {
        // AutoSize: 폰트/DPI 배율이 달라도 글자가 잘리지 않도록 텍스트 길이에 맞춰 폭이 정해진다.
        // width 인자는 기존 호출부 호환용(최소 폭)으로만 사용.
        var lbl = new Label { Text = text, Left = left, Top = top, MinimumSize = new Size(width, 0), AutoSize = true, ForeColor = Color.LightGray };
        Controls.Add(lbl);
        return lbl;
    }

    private int _initialVolume;

    private void LoadFromConfig()
    {
        var cfg = Config.Instance;
        _initialVolume = cfg.Volume;
        for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];
        _tbAlarmName.Text = cfg.AlarmName;
        _cbTts.Checked = cfg.Tts;
        _tbVolume.Value = Math.Clamp(cfg.Volume, _tbVolume.Minimum, _tbVolume.Maximum);
        _tbRate.Value = Math.Clamp(cfg.Rate, _tbRate.Minimum, _tbRate.Maximum);
        _lblVolume.Text = $"볼륨: {cfg.Volume}";
        _lblRate.Text = $"TTS 속도: {cfg.Rate}";
        for (int i = 0; i < 3; i++)
        {
            _customNames[i].Text = cfg.CustomAlarms[i].Name;
            _customTicks[i].Value = Math.Clamp(cfg.CustomAlarms[i].Tick, (int)_customTicks[i].Minimum, (int)_customTicks[i].Maximum);
            _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }
    }

    // 숫자를 직접 입력한 뒤 Enter(=확인)를 누르면 NumericUpDown.Value가 아직 갱신되지 않은 채일 수 있다.
    // 입력칸에 보이는 텍스트를 직접 해석하고, 해석할 수 없으면(빈 칸 등) 마지막 유효 값을 쓴다.
    private static int ReadTick(NumericUpDown nud)
    {
        if (decimal.TryParse(nud.Text, out decimal typed))
            return (int)Math.Clamp(typed, nud.Minimum, nud.Maximum);
        return (int)nud.Value;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var cfg = Config.Instance;
        for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
        cfg.AlarmName = _tbAlarmName.Text;
        cfg.Tts = _cbTts.Checked;
        // 설정창을 열어 둔 동안 메인 창 슬라이더로 바꾼 볼륨을 되돌리지 않도록, 이 창에서 바꾼 경우에만 반영한다
        if (_tbVolume.Value != Math.Clamp(_initialVolume, _tbVolume.Minimum, _tbVolume.Maximum))
            cfg.Volume = _tbVolume.Value;
        cfg.Rate = _tbRate.Value;
        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name = _customNames[i].Text;
            cfg.CustomAlarms[i].Tick = ReadTick(_customTicks[i]);
            cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
        }
        cfg.Save();
        Accepted = true;
        Synth.Instance.SetVolume(cfg.Volume);
        Synth.Instance.SetRate(cfg.Rate);
    }

    // ===== 타이틀바 드래그 =====
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ===== 8방향 리사이즈 (Form1.cs와 동일 패턴) =====
    private const int WM_NCHITTEST = 0x0084;
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

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST || WindowState != FormWindowState.Normal || (int)m.Result != HTCLIENT)
            return;

        int x = unchecked((short)(m.LParam.ToInt32() & 0xFFFF));
        int y = unchecked((short)((m.LParam.ToInt32() >> 16) & 0xFFFF));
        var pos = ReSize.GetMousePosition(this, PointToClient(new Point(x, y)));

        m.Result = (IntPtr)(pos switch
        {
            ReSize.MousePosition.Left => HTLEFT,
            ReSize.MousePosition.Right => HTRIGHT,
            ReSize.MousePosition.Top => HTTOP,
            ReSize.MousePosition.Bottom => HTBOTTOM,
            ReSize.MousePosition.TopLeft => HTTOPLEFT,
            ReSize.MousePosition.TopRight => HTTOPRIGHT,
            ReSize.MousePosition.BottomLeft => HTBOTTOMLEFT,
            ReSize.MousePosition.BottomRight => HTBOTTOMRIGHT,
            _ => HTCLIENT
        });
    }
}
