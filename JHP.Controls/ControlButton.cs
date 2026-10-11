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
                 ControlStyles.DoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(46, 30);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool hovered = _hovered;

        Color bg = hovered
            ? (Type == ButtonType.Close ? Color.FromArgb(196, 43, 28) : Color.FromArgb(60, 60, 60))
            : (Parent?.BackColor ?? Color.FromArgb(28, 28, 28)); // 투명 브러시는 더블버퍼(검정)가 그대로 보이므로 부모 배경색으로 칠한다

        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        float s = DeviceDpi / 96f;
        int cx = Width / 2, cy = Height / 2, r = (int)Math.Round(5 * s);
        using var pen = new Pen(Color.White, 1.2f * s);

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

    // 호버 상태는 실제 커서 위치(Cursor.Position)가 아니라 MouseEnter/Leave 이벤트로 추적한다.
    // (자동 UI 테스트가 메시지로 마우스 이벤트를 보내도 동작하고, 사용자의 실제 마우스를 건드리지 않아도 된다)
    private bool _hovered;

    // MouseEnter가 오지 않는 경우(메시지로 합성한 마우스 이동 등)에도 첫 MouseMove에서 호버로 전환한다.
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_hovered) { _hovered = true; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; base.OnMouseLeave(e); Invalidate(); }

    // 초기 렌더링 버그 수정: 핸들 생성 직후 강제로 한 번 더 그려줌 (Timerbutton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
