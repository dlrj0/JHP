using JHP.Api;
using Xunit;

namespace JHP.Tests;

// 대화상자는 화면 밖(-20000,0)으로 옮긴 뒤 코드에서 버튼/닫기를 눌러 결과만 확인한다(사용자 입력/포커스를 건드리지 않음).
public class DarkMessageBoxTests
{
    private static (DialogResult Result, Form Form) Run(string text, MessageBoxButtons buttons, Action<Form> act)
    {
        DialogResult result = DialogResult.None;
        Form? shown = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var form = Application.OpenForms.Cast<Form>().FirstOrDefault();
                    if (form is null) return;
                    shown = form;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, 0);
                    act(form);
                };
                timer.Start();
                result = DarkMessageBox.Show(null, text, "테스트", buttons);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "대화상자가 닫히지 않음");
        if (error is not null) throw error;
        return (result, shown!);
    }

    [Fact]
    public void Ok_Enter_ReturnsOk()
    {
        var (result, form) = Run("안내 문구", MessageBoxButtons.OK, f => f.AcceptButton!.PerformClick());
        Assert.Equal(DialogResult.OK, result);
        Assert.Equal("테스트", form.Text);
    }

    [Fact]
    public void YesNo_Enter_ReturnsYes()
    {
        var (result, _) = Run("삭제할까요?", MessageBoxButtons.YesNo, f => f.AcceptButton!.PerformClick());
        Assert.Equal(DialogResult.Yes, result);
    }

    [Fact]
    public void YesNo_Esc_ReturnsNo()
    {
        var (result, _) = Run("삭제할까요?", MessageBoxButtons.YesNo, f => f.CancelButton!.PerformClick());
        Assert.Equal(DialogResult.No, result);
    }

    [Fact]
    public void YesNoCancel_CloseButton_ReturnsCancel()
    {
        var (result, _) = Run("업데이트", MessageBoxButtons.YesNoCancel, f => f.Close());
        Assert.Equal(DialogResult.Cancel, result);
    }

    [Fact]
    public void YesNo_CloseButton_ReturnsNo()
    {
        var (result, _) = Run("삭제할까요?", MessageBoxButtons.YesNo, f => f.Close());
        Assert.Equal(DialogResult.No, result);
    }

    [Fact]
    public void LongText_WrapsWithinReasonableWidth()
    {
        string text = string.Join(" ", Enumerable.Repeat("아주 긴 문장입니다.", 40));
        int width = 0, height = 0;
        var (result, _) = Run(text, MessageBoxButtons.OK, f =>
        {
            width = f.ClientSize.Width;
            height = f.ClientSize.Height;
            f.AcceptButton!.PerformClick();
        });
        Assert.Equal(DialogResult.OK, result);
        Assert.InRange(width, 240, 700);
        Assert.True(height > 100);
    }
}
