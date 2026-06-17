using JHP.Api;
using JHP.Controls;

namespace JHP;

public class AlarmForm : Form
{
    // [0]=2h [1]=1h [2]=30m [3]=20m [4]=15m [5]=10m [6]=100s [7]=55s
    private static readonly string[] AlarmLabels = ["2시간", "1시간", "30분", "20분", "15분", "10분", "100초", "55초"];

    private readonly CustomCheckBox[] _alarmChecks = new CustomCheckBox[8];
    private readonly TextBox[]  _customNames = new TextBox[3];
    private readonly NumericUpDown[] _customTicks = new NumericUpDown[3];
    private readonly CustomCheckBox[] _customEnabled = new CustomCheckBox[3];

    // ⑥ TextBox → ComboBox: alarm/ 폴더 mp3 목록 자동 로드
    private ComboBox _cmbAlarmFile = null!;
    private CustomCheckBox _cbTts  = null!;

    // 메인 화면(Form1)과 동일한 NSlider 사용 — TrackBar는 시스템 기본 테마로 그려져 다크 UI와 어울리지 않음
    private NSlider _sliderVolume = null!;
    private NSlider _sliderRate   = null!;
    private Label    _lblVolume   = null!;
    private Label    _lblRate     = null!;

    public AlarmForm()
    {
        InitializeComponent();
        LoadAlarmFiles();   // ⑥ 파일 목록 먼저 로드
        LoadFromConfig();   // 그 다음 설정값 적용
    }

    private void InitializeComponent()
    {
        Text            = "알람 설정";
        Width           = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.FromArgb(32, 32, 32);
        ForeColor       = Color.White;

        int y = 12;

        // 고정 알람 8개 (CustomCheckBox + Label 조합 — 체크박스 자체는 텍스트를 그리지 않음)
        AddLabel("재획비 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 8; i++)
        {
            int left = 12 + (i % 4) * 94;
            int top  = y + (i / 4) * 28;

            _alarmChecks[i] = new CustomCheckBox { Left = left, Top = top + 2 };
            Controls.Add(_alarmChecks[i]);

            var lbl = new Label
            {
                Text      = AlarmLabels[i],
                Left      = left + 24,
                Top       = top + 4,
                Width     = 66,
                ForeColor = Color.White
            };
            Controls.Add(lbl);
        }
        y += 64;

        // ⑥ 알람 파일 ComboBox (alarm/ 폴더 mp3 자동 목록)
        AddLabel("알람 파일", 12, y + 3, 70);
        _cmbAlarmFile = new ComboBox
        {
            Left          = 88,
            Top           = y,
            Width         = 298,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.FromArgb(50, 50, 50),
            ForeColor     = Color.White
        };
        Controls.Add(_cmbAlarmFile);
        y += 32;

        // TTS
        _cbTts = new CustomCheckBox { Left = 12, Top = y + 1 };
        Controls.Add(_cbTts);
        var lblTts = new Label { Text = "TTS 사용", Left = 36, Top = y + 3, Width = 100, ForeColor = Color.White };
        Controls.Add(lblTts);
        y += 30;

        // 볼륨 (NSlider — 메인 화면 슬라이더와 동일한 컨트롤/색상)
        _lblVolume = AddLabel("볼륨: 0", 12, y + 3, 90);
        _sliderVolume = new NSlider { Left = 108, Top = y + 3, Width = 278 };
        _sliderVolume.ValueChanged += (_, _) => _lblVolume.Text = $"볼륨: {_sliderVolume.Value}";
        Controls.Add(_sliderVolume);
        y += 30;

        // TTS 속도 (NSlider)
        _lblRate = AddLabel("속도: 0", 12, y + 3, 90);
        _sliderRate = new NSlider { Left = 108, Top = y + 3, Width = 278, Minimum = -10, Maximum = 10 };
        _sliderRate.ValueChanged += (_, _) => _lblRate.Text = $"속도: {_sliderRate.Value}";
        Controls.Add(_sliderRate);
        y += 30;

        // 커스텀 알람 3개
        AddLabel("커스텀 알람", 12, y, 200);
        y += 22;
        for (int i = 0; i < 3; i++)
        {
            _customEnabled[i] = new CustomCheckBox { Left = 12, Top = y + 4 };
            Controls.Add(_customEnabled[i]);

            _customNames[i] = new TextBox
            {
                Left = 36, Top = y, Width = 190,
                PlaceholderText = "알람 이름",
                BackColor   = Color.FromArgb(50, 50, 50),
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_customNames[i]);

            _customTicks[i] = new NumericUpDown
            {
                Left = 234, Top = y, Width = 80,
                Minimum = 0, Maximum = 99999,
                BackColor   = Color.FromArgb(50, 50, 50),
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_customTicks[i]);

            AddLabel("초", 320, y + 3, 30);
            y += 32;
        }

        y += 8;

        var btnOk = new Button
        {
            Text         = "확인",
            Left         = 218, Top = y,
            Width        = 80,
            DialogResult = DialogResult.OK,
            FlatStyle    = FlatStyle.Flat,
            BackColor    = Color.FromArgb(55, 90, 145),
            ForeColor    = Color.White
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text         = "취소",
            Left         = 308, Top = y,
            Width        = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle    = FlatStyle.Flat,
            BackColor    = Color.FromArgb(60, 60, 60),
            ForeColor    = Color.White
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        Controls.Add(btnCancel);

        Height        = y + 72;
        AcceptButton  = btnOk;
        CancelButton  = btnCancel;
    }

    private Label AddLabel(string text, int left, int top, int width)
    {
        var lbl = new Label { Text = text, Left = left, Top = top, Width = width, ForeColor = Color.LightGray };
        Controls.Add(lbl);
        return lbl;
    }

    // ⑥ alarm/ 폴더에서 mp3 파일 목록 로드
    private void LoadAlarmFiles()
    {
        _cmbAlarmFile.Items.Clear();
        string dir = Path.Combine(AppContext.BaseDirectory, "alarm");
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.mp3"))
                _cmbAlarmFile.Items.Add(Path.GetFileName(f));

        if (_cmbAlarmFile.Items.Count == 0)
            _cmbAlarmFile.Items.Add("(파일 없음)");
    }

    private void LoadFromConfig()
    {
        var cfg = Config.Instance;

        for (int i = 0; i < 8; i++) _alarmChecks[i].Checked = cfg.AlarmEnabled[i];

        // ⑥ AlarmName을 ComboBox에서 선택
        if (!string.IsNullOrEmpty(cfg.AlarmName) && _cmbAlarmFile.Items.Contains(cfg.AlarmName))
            _cmbAlarmFile.SelectedItem = cfg.AlarmName;
        else if (_cmbAlarmFile.Items.Count > 0)
            _cmbAlarmFile.SelectedIndex = 0;

        _cbTts.Checked      = cfg.Tts;
        _sliderVolume.Value = Math.Clamp(cfg.Volume, _sliderVolume.Minimum, _sliderVolume.Maximum);
        _sliderRate.Value   = Math.Clamp(cfg.Rate, _sliderRate.Minimum, _sliderRate.Maximum);
        _lblVolume.Text     = $"볼륨: {cfg.Volume}";
        _lblRate.Text       = $"속도: {cfg.Rate}";

        for (int i = 0; i < 3; i++)
        {
            _customNames[i].Text      = cfg.CustomAlarms[i].Name;
            _customTicks[i].Value     = cfg.CustomAlarms[i].Tick;
            _customEnabled[i].Checked = cfg.CustomAlarms[i].Enabled;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var cfg = Config.Instance;

        for (int i = 0; i < 8; i++) cfg.AlarmEnabled[i] = _alarmChecks[i].Checked;

        // ⑥ TextBox 대신 ComboBox에서 읽기
        if (_cmbAlarmFile.SelectedItem is string selected && selected != "(파일 없음)")
            cfg.AlarmName = selected;

        cfg.Tts    = _cbTts.Checked;
        cfg.Volume = _sliderVolume.Value;
        cfg.Rate   = _sliderRate.Value;

        for (int i = 0; i < 3; i++)
        {
            cfg.CustomAlarms[i].Name    = _customNames[i].Text;
            cfg.CustomAlarms[i].Tick    = (int)_customTicks[i].Value;
            cfg.CustomAlarms[i].Enabled = _customEnabled[i].Checked;
        }

        cfg.Save();
        Synth.Instance.SetVolume(cfg.Volume);
        Synth.Instance.SetRate(cfg.Rate);
    }
}
