using JHP.Api;
using Microsoft.Web.WebView2.Core;

namespace JHP;

// 브라우저 데이터 삭제 대화상자: 크롬의 '인터넷 사용 기록 삭제'처럼 지울 항목을 골라서 지운다.
// 확인하면 Result에 고른 항목(CoreWebView2BrowsingDataKinds 조합)이 들어간다. 아무것도 고르지 않으면 확인할 수 없다.
public class BrowserDataForm : Form
{
    private readonly (string Text, string Hint, CoreWebView2BrowsingDataKinds Kinds, bool Default)[] _items =
    [
        ("방문 기록", "", CoreWebView2BrowsingDataKinds.BrowsingHistory, true),
        ("다운로드 기록", "", CoreWebView2BrowsingDataKinds.DownloadHistory, true),
        ("캐시", "저장된 이미지·파일", CoreWebView2BrowsingDataKinds.DiskCache, true),
        ("자동완성(입력 기록)", "", CoreWebView2BrowsingDataKinds.GeneralAutofill, true),
        ("쿠키 및 사이트 데이터", "로그인이 모두 해제됩니다", CoreWebView2BrowsingDataKinds.AllSite, false),
        ("저장된 비밀번호", "", CoreWebView2BrowsingDataKinds.PasswordAutosave, false),
        ("사이트 설정", "권한 등", CoreWebView2BrowsingDataKinds.Settings, false),
    ];

    private readonly List<CheckBox> _checks = [];
    private Button _btnOk = null!;

    public CoreWebView2BrowsingDataKinds Result { get; private set; }

    protected override bool ShowWithoutActivation => Program.TestOffscreen;

    protected override CreateParams CreateParams   // 자동 테스트에서는 활성화되지 않는 창으로 만든다(WS_EX_NOACTIVATE)
    {
        get
        {
            var cp = base.CreateParams;
            if (Program.TestOffscreen) cp.ExStyle |= 0x08000000;
            return cp;
        }
    }

    public BrowserDataForm()
    {
        Text = "브라우저 기록 삭제";
        Icon = Program.AppIcon ?? Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        Font = new Font("Malgun Gothic", 9F);
        Width = 380;

        int y = 14;
        var lblTop = new Label { Text = "삭제할 항목을 고르세요.", Left = 16, Top = y, Width = 330, ForeColor = Color.LightGray };
        Controls.Add(lblTop);
        y += 28;

        foreach (var item in _items)
        {
            var cb = new CheckBox
            {
                Text = item.Hint.Length == 0 ? item.Text : $"{item.Text}  ({item.Hint})",
                AccessibleName = item.Text,
                Left = 20, Top = y, Width = 330, Height = 22,
                Checked = item.Default,
                ForeColor = Color.White,
                Tag = item.Kinds
            };
            cb.CheckedChanged += (_, _) => UpdateOkEnabled();
            _checks.Add(cb);
            Controls.Add(cb);
            y += 26;
        }

        y += 4;
        var lblNote = new Label
        {
            Text = "JHP 설정(사이트 목록·알람 등)은 지워지지 않습니다. 유튜브 등 사이트 계정에 저장된 시청/검색 기록은 해당 사이트에서 따로 지워야 합니다.",
            Left = 16, Top = y, Width = 340, Height = 54, ForeColor = Color.Gray
        };
        Controls.Add(lblNote);
        y += 62;

        _btnOk = new Button
        {
            Text = "삭제", Left = 188, Top = y, Width = 80, DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55, 90, 145), ForeColor = Color.White
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += (_, _) =>
        {
            var kinds = (CoreWebView2BrowsingDataKinds)0;
            foreach (var cb in _checks)
                if (cb.Checked) kinds |= (CoreWebView2BrowsingDataKinds)cb.Tag!;
            Result = kinds;
        };
        var btnCancel = new Button
        {
            Text = "취소", Left = 278, Top = y, Width = 80, DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        Controls.AddRange([_btnOk, btnCancel]);
        AcceptButton = _btnOk;
        CancelButton = btnCancel;
        ClientSize = new Size(380, y + 46);

        // 배율 적용은 자식 컨트롤을 모두 추가한 뒤에 해야 한다. 좌표는 96dpi 기준.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
    }

    private void UpdateOkEnabled() => _btnOk.Enabled = _checks.Any(c => c.Checked);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Apply(Handle);   // 시스템 타이틀바를 다크로
    }
}
