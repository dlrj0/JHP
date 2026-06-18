namespace JHP.Controls;

using System.ComponentModel;

public class ControlButton : Button
{
    public enum ButtonType { Minimize, Maximize, Close }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonType Type { get; set; } = ButtonType.Close;

    public ControlButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        Size = new Size(46, 30);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));

        Color bg = hovered
            ? (Type == ButtonType.Close ? Color.FromArgb(196, 43, 28) : Color.FromArgb(60, 60, 60))
            : Color.Transparent;

        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        int cx = Width / 2, cy = Height / 2, r = 5;
        using var pen = new Pen(Color.White, 1.2f);

        switch (Type)
        {
            case ButtonType.Close:
                g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                break;
            case ButtonType.Minimize:
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
                break;
            case ButtonType.Maximize:
                g.DrawRectangle(pen, cx - r, cy - r, r * 2, r * 2);
                break;
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

    // 초기 렌더링 버그 수정: 핸들 생성 직후 강제로 한 번 더 그려줌 (TimerButton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
