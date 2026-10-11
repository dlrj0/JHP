namespace JHP.Api;

public static class Prompt
{
    // 텍스트 입력 다이얼로그
    public static string? ShowDialog(string title, string label, string defaultValue = "")
    {
        using Form form = new()
        {
            Text = title,
            Width = 360,
            Height = 148,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        Label lbl = new() { Left = 12, Top = 14, Width = 320, Text = label };
        TextBox tb = new() { Left = 12, Top = 36, Width = 320, Text = defaultValue };
        Button ok = new() { Text = "확인", Left = 172, Top = 70, Width = 80, DialogResult = DialogResult.OK };
        Button cancel = new() { Text = "취소", Left = 260, Top = 70, Width = 80, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange([lbl, tb, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : null;
    }

    // 확인/취소 다이얼로그
    // owner를 지정하면 항상 위 상태의 메인 창 뒤에 가려지지 않는다.
    public static bool ShowDialog(string title, string message, IWin32Window? owner = null)
    {
        return MessageBox.Show(owner, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
               == DialogResult.OK;
    }
}