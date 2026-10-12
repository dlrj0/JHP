using JHP.Api;

namespace JHP;

public class SiteForm : Form
{
    private TextBox _tbName = null!;
    private TextBox _tbUrl = null!;
    private Button _btnOk = null!;

    public Site? Result { get; private set; }

    // 새 사이트 추가
    public SiteForm() : this(null) { }

    // site가 있으면 "사이트 수정" 모드: 이름/주소를 채우고 제목/확인 버튼 문구를 바꾼다.
    public SiteForm(Site? site)
    {
        InitializeComponent();
        if (site is null) return;
        Text = "사이트 수정";
        _tbName.Text = site.Name;
        _tbUrl.Text = site.Url;
        _btnOk.Text = "저장";
    }

    private void InitializeComponent()
    {
        Text = "사이트 추가";
        Icon = Program.AppIcon ?? Icon;
        Width = 360;
        Height = 176;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;

        var lblName = new Label { Text = "이름", Left = 12, Top = 16, Width = 60, ForeColor = Color.LightGray };
        _tbName = new TextBox
        {
            Left = 78, Top = 13, Width = 252,
            MaxLength = 40,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblUrl = new Label { Text = "URL", Left = 12, Top = 48, Width = 60, ForeColor = Color.LightGray };
        _tbUrl = new TextBox
        {
            Left = 78, Top = 45, Width = 252,
            PlaceholderText = "https://",
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnOk = new Button
        {
            Text = "추가",
            Left = 158,
            Top = 82,
            Width = 80,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 90, 145),
            ForeColor = Color.White
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;
        _btnOk = btnOk;

        var btnCancel = new Button
        {
            Text = "취소",
            Left = 248,
            Top = 82,
            Width = 80,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        Controls.AddRange([lblName, _tbName, lblUrl, _tbUrl, btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        // 배율 적용은 자식 컨트롤을 모두 추가한 뒤에 해야 한다. 좌표는 96dpi 기준.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Apply(Handle);   // 시스템 타이틀바를 다크로
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_tbName.Text) || string.IsNullOrWhiteSpace(_tbUrl.Text))
        {
            MessageBox.Show(this, "이름과 URL을 모두 입력해주세요.", "입력 오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            (string.IsNullOrWhiteSpace(_tbName.Text) ? _tbName : _tbUrl).Focus();
            DialogResult = DialogResult.None;
            return;
        }
        if (!JHP.Api.Site.TryNormalizeUrl(_tbUrl.Text, out string url))
        {
            MessageBox.Show(this, "올바른 웹 주소가 아닙니다. http:// 또는 https://로 시작하는 주소를 입력해주세요.", "입력 오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tbUrl.Focus();
            _tbUrl.SelectAll();
            DialogResult = DialogResult.None;
            return;
        }
        Result = new Site { Name = _tbName.Text.Trim(), Url = url };
    }
}
