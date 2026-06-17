using JHP.Api;
using JHP.Controls;
using System.Runtime.InteropServices;

namespace JHP;

public class AlarmForm : Form
{
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

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

    private Panel _pnlTitle = null!;

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        const int contentWidth = 600;

        Text = "알람 설정";
        Width = contentWidth;
        MinimumSize = new Size(520, 440);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;

        // ===== 커스텀 타이틀바 (Form1과 동일한 다크 테마 + 드래그 이동 + 모서리 리사이즈) =====
        _pnlTitle = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        var lblFormTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(12, 8),
            Text = "알람 설정"
        };

        var btnTitleClose = new ControlButton
        {
            Type = ControlButton.ButtonType.Close,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(contentWidth - 46, 1)
        };
        btnTitleClose.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _pnlTitle.MouseDown += TitleBar_MouseDown;
        lblFormTitle.MouseDown += TitleBar_MouseDown;

        _pnlTitle.Controls.Add(lblFormTitle);
        _pnlTitle.Controls.Add(btnTitleClose);

        // ===== 본문 (타이틀바 아래 나머지 영역을 채움) =====
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor
        };

        int y = 12;

        // 고정 알람 8개
        AddLabel(body, "재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 4) * 135,
                Top = y + (i / 4) * 28,
                Width = 125,
                ForeColor = Color.White
            };
            body.Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        // 알람 파일명
        AddLabel(body, "알람 파일명", 12, y + 3, 90);
        _tbAlarmName = new TextBox
        {
            Left = 108,
            Top = y,
            Width = contentWidth - 108 - 32,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        body.Controls.Add(_tbAlarmName);
        y += 32;

        // TTS
        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        body.Controls.Add(_cbTts);
        y += 30;

        // 볼륨
        _lblVolume = AddLabel(body, "볼륨: 0", 12, y + 8, 90);
        _tbVolume = new TrackBar
        {
            Left = 108,
            Top = y,
            Width = contentWidth - 108 - 32,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        body.Controls.Add(_tbVolume);
        y += 42;

        // TTS 속도
        _lblRate = AddLabel(body, "속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar
        {
            Left = 108,
            Top = y,
            Width = contentWidth - 108 - 32,
            Minimum = -10,
            Maximum = 10,
            TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"속도: {_tbRate.Value}";
        body.Controls.Add(_tbRate);
        y += 42;

        // 커스텀 알람 3개
        AddLabel(body, "커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20 };
            body.Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36,
                Top = y,
                Width = 300,
                PlaceholderText = "알람 이름",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            body.Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 346,
                Top = y,
                Width = 90,
                Minimum = 0,
                Maximum = 99999,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            body.Controls.Add(_customTicks[i]);

            var lblSec = AddLabel(body, "초", 442, y + 3, 30);
            lblSec.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            y += 32;
        }

        y += 8;

        var btnOk = new Button
        {
            Text = "확인",
            Left = contentWidth - 32 - 80 - 12 - 80,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        body.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "취소",
            Left = contentWidth - 32 - 80,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        body.Controls.Add(btnCancel);

        Height = y + 72 + _pnlTitle.Height;
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(_pnlTitle);
        Controls.Add(body);
    }

    private static Label AddLabel(Control parent, string text, int left, int top, int width)
    {
        var lbl = new Label { Text = text, Left = left, Top = top, Width = width, ForeColor = Color.LightGray };
        parent.Controls.Add(lbl);
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

    // ===== 커스텀 타이틀바 드래그 이동 =====
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    // ===== 테두리 없는 창 모서리 리사이즈 (Form1.cs와 동일한 패턴) =====
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

        if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT)
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = ReSize.SetThick(ReSize.GetMousePosition(this, e.Location));
    }
}
