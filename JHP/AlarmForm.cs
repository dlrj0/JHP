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
        Width = 460; // 기존 420 → 460, 체크박스 텍스트가 답답해 보이던 문제 완화
        // [수정] 기존 FixedDialog는 리사이즈 자체가 불가능했음.
        // 일반 윈도우창처럼 모서리/테두리를 드래그해서 크기 조절 가능하도록 변경.
        // (Form1처럼 FormBorderStyle.None + WM_NCHITTEST를 직접 구현하는 대신,
        //  네이티브 Sizable 테두리를 사용 — 코드량/리스크가 훨씬 적고 동일한 체감 효과를 줌.
        //  네이티브 테두리이므로 모서리 화살표 커서 변경도 Windows가 자동으로 처리해줌)
        FormBorderStyle = FormBorderStyle.Sizable;
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
                Left = 12 + (i % 4) * 109,   // 94 → 109
                Top = y + (i / 4) * 28,
                Width = 100,                 // 88 → 100
                ForeColor = Color.White
            };
            Controls.Add(_alarmChecks[i]);
        }
        y += 64;

        // 알람 파일명
        AddLabel("알람 파일명", 12, y + 3, 90);
        _tbAlarmName = new TextBox
        {
            Left = 108, Top = y, Width = 318, // 278 → 318
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_tbAlarmName);
        y += 32;

        // TTS
        _cbTts = new CheckBox { Text = "TTS 사용", Left = 12, Top = y, Width = 100, ForeColor = Color.White };
        Controls.Add(_cbTts);
        y += 30;

        // 볼륨
        _lblVolume = AddLabel("볼륨: 0", 12, y + 8, 90);
        _tbVolume = new TrackBar
        {
            Left = 108, Top = y, Width = 318, Minimum = 0, Maximum = 100, TickFrequency = 10,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _tbVolume.ValueChanged += (s, e) => _lblVolume.Text = $"볼륨: {_tbVolume.Value}";
        Controls.Add(_tbVolume);
        y += 42;

        // TTS 속도
        _lblRate = AddLabel("속도: 0", 12, y + 8, 90);
        _tbRate = new TrackBar
        {
            Left = 108, Top = y, Width = 318, Minimum = -10, Maximum = 10, TickFrequency = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
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

            _customNames[i] = new TextBox
            {
                Left = 36, Top = y, Width = 220, PlaceholderText = "알람 이름", // 190 → 220
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 264, Top = y, Width = 80, Minimum = 0, Maximum = 99999,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(_customTicks[i]);

            var lblSec = AddLabel("초", 350, y + 3, 30);
            lblSec.Anchor = AnchorStyles.Top | AnchorStyles.Right;

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
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
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
            ForeColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        Controls.Add(btnCancel);

        Height = y + 72;
        // [추가] 리사이즈는 가능하지만, 컨트롤이 잘릴 정도로 작아지지는 않게 최소 크기 고정
        MinimumSize = new Size(460, Height);

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
