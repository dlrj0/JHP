using JHP.Api;
using JHP.Controls;
using System.Runtime.InteropServices;

namespace JHP;

public class AlarmForm : Form
{
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    private readonly CheckBox[] _alarmChecks = new CheckBox[8];
    private readonly TextBox[] _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CheckBox[] _customEnabled = new CheckBox[3];

    private Panel _pnlTitle = null!;
    private TextBox _tbAlarmName = null!;
    private CheckBox _cbTts = null!;
    private TrackBar _tbVolume = null!;
    private TrackBar _tbRate = null!;
    private Label _lblVolume = null!;
    private Label _lblRate = null!;

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        Text = "알람 설정";
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
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 46, 1),
            Type = ControlButton.ButtonType.Close
        };
        btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _pnlTitle.Controls.Add(lblCaption);
        _pnlTitle.Controls.Add(btnClose);
        _pnlTitle.MouseDown += TitleBar_MouseDown;
        lblCaption.MouseDown += TitleBar_MouseDown;

        Controls.Add(_pnlTitle);

        // ===== 본문 (타이틀바 아래에서 시작) =====
        int y = 44;

        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 4) * 135,
                Top = y + (i / 4) * 28,
                Width = 128,
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        AddLabel("알람 파일명", 12, y + 3, 90);
        _tbAlarmName = new TextBox
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_tbAlarmName);
        y += 32;

        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        Controls.Add(_cbTts);
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

        _lblRate = AddLabel("속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar
        {
            Left = 108, Top = y, Width = ClientSize.Width - 120,
            Minimum = -10, Maximum = 10, TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"속도: {_tbRate.Value}";
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

            _customTicks[i] = new NumericUpDown { Left = 234, Top = y, Width = 80, Minimum = 0, Maximum = 99999 };
            Controls.Add(_customTicks[i]);

            AddLabel("초", 320, y + 3, 30);
            y += 32;
        }

        y += 8;

        var btnOk = new Button
        {
            Text = "확인",
            Left = ClientSize.Width - 180, Top = y, Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "취소",
            Left = ClientSize.Width - 92, Top = y, Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        Controls.Add(btnCancel);

        Height = y + 72;
        AcceptButton = btnOk;
        CancelButton = btnCancel;
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
