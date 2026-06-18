using JHP.Api;
using JHP.Controls;
using System.Runtime.InteropServices;

namespace JHP;

public class AlarmForm : Form
{
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    // 자체 타이틀바
    private Panel _pnlTitle = null!;
    private Label _lblFormTitle = null!;
    private ControlButton _btnClose = null!;

    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];

    private TextBox _tbAlarmName = null!;
    private CheckBox _cbTts = null!;
    private TrackBar _tbVolume = null!;
    private TrackBar _tbRate = null!;
    private Label _lblVolume = null!;
    private Label _lblRate = null!;

    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    private const int TitleH = 32;  // 자체 타이틀바 높이
    private const int Pad = 12;     // 상단 패딩
    private const int ContentW = 600;

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        Width = ContentW;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        MinimumSize = new Size(420, 400);

        // ===== 자체 타이틀바 =====
        _pnlTitle = new Panel
        {
            Dock = DockStyle.Top,
            Height = TitleH,
            BackColor = Color.FromArgb(22, 22, 22)
        };
        _lblFormTitle = new Label
        {
            Text = "알람 설정",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(12, 8)
        };
        _btnClose = new ControlButton
        {
            Type = ControlButton.ButtonType.Close,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ContentW - 46, 1)
        };
        _btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _pnlTitle.MouseDown += TitleBar_MouseDown;
        _lblFormTitle.MouseDown += TitleBar_MouseDown;
        _pnlTitle.Controls.Add(_lblFormTitle);
        _pnlTitle.Controls.Add(_btnClose);
        Controls.Add(_pnlTitle);

        // ===== 컨텐츠 (y 좌표는 TitleH 아래부터) =====
        int y = TitleH + Pad;
        int labelW = 90;
        int ctrlX = 108;
        int ctrlW = ContentW - ctrlX - Pad;

        // 고정 알람 8개 (4열, 135px 간격)
        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 4) * 135,
                Top = y + (i / 4) * 28,
                Width = 129,
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        // 알람 파일명
        AddLabel("알람 파일명", 12, y + 3, labelW);
        _tbAlarmName = new TextBox
        {
            Left = ctrlX,
            Top = y,
            Width = ctrlW,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_tbAlarmName);
        y += 32;

        // TTS
        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        Controls.Add(_cbTts);
        y += 30;

        // 볼륨
        _lblVolume = AddLabel("볼륨: 0", 12, y + 8, labelW);
        _tbVolume = new TrackBar
        {
            Left = ctrlX,
            Top = y,
            Width = ctrlW,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbVolume.ValueChanged += (_, _) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 42;

        // TTS 속도
        _lblRate = AddLabel("속도: 0", 12, y + 8, labelW);
        _tbRate = new TrackBar
        {
            Left = ctrlX,
            Top = y,
            Width = ctrlW,
            Minimum = -10,
            Maximum = 10,
            TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbRate.ValueChanged += (_, _) => _lblRate.Text = $"속도: {_tbRate.Value}";
        Controls.Add(_tbRate);
        y += 42;

        // 커스텀 알람 3개
        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20 };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36,
                Top = y,
                Width = 240,
                PlaceholderText = "알람 이름",
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 284,
                Top = y,
                Width = 80,
                Minimum = 0,
                Maximum = 99999,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_customTicks[i]);

            AddLabel("초", 370, y + 3, 30);
            y += 32;
        }

        y += 8;

        _btnOk = new Button
        {
            Text = "확인",
            Left = ContentW - 180,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;
        Controls.Add(_btnOk);

        _btnCancel = new Button
        {
            Text = "취소",
            Left = ContentW - 92,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        Controls.Add(_btnCancel);

        Height = y + 60;
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private Label AddLabel(string text, int left, int top, int width)
    {
        var lbl = new Label { Text = text, Left = left, Top = top, Width = width, ForeColor = Color.LightGray };
        Controls.Add(lbl);
        return lbl;
    }

    private void LoadFromConfig()
    {
        var cfg = Config.Instance;
        for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];
        _tbAlarmName.Text = cfg.AlarmName;
        _cbTts.Checked = cfg.Tts;
        _tbVolume.Value = cfg.Volume;
        _tbRate.Value = cfg.Rate;
        _lblVolume.Text = $"볼륨: {cfg.Volume}";
        _lblRate.Text = $"속도: {cfg.Rate}";
        for (int i = 0; i < 3; i++)
        {
            _customNames[i].Text = cfg.CustomAlarms[i].Name;
            _customTicks[i].Value = cfg.CustomAlarms[i].Tick;
            _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var cfg = Config.Instance;
        for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;
        cfg.AlarmName = _tbAlarmName.Text;
        cfg.Tts = _cbTts.Checked;
        cfg.Volume = _tbVolume.Value;
        cfg.Rate = _tbRate.Value;
        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name = _customNames[i].Text;
            cfg.CustomAlarms[i].Tick = (int)_customTicks[i].Value;
            cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
        }
        cfg.Save();
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

    // ===== 8방향 리사이즈 (WM_NCHITTEST) =====
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

        if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT) return;

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

    // 리사이즈 커서 시각적 피드백
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
