using JHP.Api;

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

    public AlarmForm()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    private void InitializeComponent()
    {
        Text = "알람 설정";
        Width = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;

        int y = 12;

        // 고정 알람 8개
        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            _alarmChecks[i] = new CheckBox
            {
                Text = AlarmLabels[i],
                Left = 12 + (i % 4) * 94,
                Top = y + (i / 4) * 28,
                Width = 88,
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        // 알람 파일명
        AddLabel("알람 파일명", 12, y + 3, 90);
        _tbAlarmName = new TextBox { Left = 108, Top = y, Width = 278 };
        Controls.Add(_tbAlarmName);
        y += 32;

        // TTS
        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        Controls.Add(_cbTts);
        y += 30;

        // 볼륨
        _lblVolume = AddLabel("볼륨: 0", 12, y + 8, 90);
        _tbVolume = new TrackBar { Left = 108, Top = y, Width = 278, Minimum = 0, Maximum = 100, TickFrequency = 10 };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 42;

        // TTS 속도
        _lblRate = AddLabel("속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar { Left = 108, Top = y, Width = 278, Minimum = -10, Maximum = 10, TickFrequency = 2 };
        _tbRate.ValueChanged += (s, e) => _lblRate.Text = $"속도: {_tbRate.Value}";
        Controls.Add(_tbRate);
        y += 42;

        // 커스텀 알람 3개
        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CheckBox { Left = 12, Top = y + 2, Width = 20 };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox { Left = 36, Top = y, Width = 190, PlaceholderText = "알람 이름" };
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
            Left = 218,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "취소",
            Left = 308,
            Top = y,
            Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
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
}