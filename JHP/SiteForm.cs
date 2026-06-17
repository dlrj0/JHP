using JHP.Api;

namespace JHP;

public class SiteForm : Form
{
    private TextBox _tbName = null!;
    private TextBox _tbUrl = null!;

    public Site? Result { get; private set; }

    public SiteForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "사이트 추가";
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
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_tbName.Text) || string.IsNullOrWhiteSpace(_tbUrl.Text))
        {
            MessageBox.Show("이름과 URL을 모두 입력해주세요.", "입력 오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        Result = new Site { Name = _tbName.Text.Trim(), Url = _tbUrl.Text.Trim() };
    }
}
