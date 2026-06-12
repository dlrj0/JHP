namespace JHP.Controls;

public class CustomCheckBox : CheckBox
{
    public CustomCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        AutoSize = false;
        Size = new Size(20, 20);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using var borderPen = new Pen(Color.FromArgb(100, 160, 255), 1.5f);
        g.DrawRectangle(borderPen, 1, 1, Width - 3, Height - 3);

        if (Checked)
        {
            int m = 4;
            using var checkPen = new Pen(Color.FromArgb(100, 160, 255), 2f);
            g.DrawLine(checkPen, m, Height / 2, Width / 2 - 1, Height - m - 1);
            g.DrawLine(checkPen, Width / 2 - 1, Height - m - 1, Width - m, m);
        }
    }
}