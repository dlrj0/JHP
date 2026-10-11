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

    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];

    private Panel _pnlTitle = null!;
    private ComboBox _tbAlarmName = null!;
    private CheckBox _cbTts = null!;
    private TrackBar _tbVolume = null!;
    private TrackBar _tbRate = null!;
    private Label _lblVolume = null!;
    private Label _lblRate = null!;

    // 확인 버튼으로 닫혔는지 (비모달 폼이라 DialogResult 대신 사용)
    public bool Accepted { get; private set; }

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Text = "알람 설정";
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
            Type = ControlButton.ButtonType.Close
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
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnPlay.FlatAppearance.BorderSize = 0;
        btnPlay.Click += (_, _) => Synth.Instance.Ring(_tbAlarmName.Text, _tbVolume.Value);
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
            ForeColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnSample.FlatAppearance.BorderSize = 0;
        btnSample.Click += (_, _) => Synth.Instance.TTS("재획비", _tbVolume.Value, _tbRate.Value);
        Controls.Add(btnSample);
        y += 30;

        _lblVolume = AddLabel("볼륨: 0", 12, y + 8, 90);
        _tbVolume = new TrackBar
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Minimum = 0, Maximum = 100, TickFrequency = 10,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 42;

        _lblRate = AddLabel("TTS 속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Minimum = -10, Maximum = 10, TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"TTS 속도: {_tbRate.Value}";
        Controls.Add(_tbRate);
        y += 42;

        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20 };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36, Top = y, Width = 190, PlaceholderText = "알람 이름",
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 234, Top = y, Width = 80, Minimum = 0, Maximum = 99999
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
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
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
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
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
            cfg.CustomAlarms[i].Tick = (int)_customTicks[i].Value;
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
