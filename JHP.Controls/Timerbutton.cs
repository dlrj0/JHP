namespace JHP.Controls;

using System.ComponentModel;

// 재획비 타이머 아이콘 버튼.
// 좌클릭(Click) = 시작/정지 토글, 우클릭(RightClick) = 알람 설정 팝업 직접 오픈.
// Active = true면 "동작 중"임을 색으로 표시한다.
public class TimerButton : Button
{
    private bool _active;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            Invalidate();
        }
    }

    // Button 기본 Click 이벤트는 좌클릭에만 반응하므로, 우클릭은 별도 이벤트로 노출한다.
    public event MouseEventHandler? RightClick;

    public TimerButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(30, 30);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));

        Color bg = hovered ? Color.FromArgb(60, 60, 60) : Color.Transparent;
        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        // 동작 중이면 초록색, 정지 상태면 흰색 — 한눈에 "돌고 있다"는 걸 알 수 있게
        Color iconColor = _active ? Color.FromArgb(110, 220, 140) : Color.White;

        int cx = Width / 2, cy = Height / 2, r = 8;
        using var pen = new Pen(iconColor, 1.6f);

        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);        // 시계 원
        g.DrawLine(pen, cx, cy, cx, cy - r + 2);                  // 12시 방향 바늘
        g.DrawLine(pen, cx, cy, cx + r - 4, cy);                  // 3시 방향 바늘
        g.DrawLine(pen, cx - 2, cy - r - 2, cx + 2, cy - r - 2);  // 상단 손잡이
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
            RightClick?.Invoke(this, e);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

    // 초기 표시 시 아이콘이 마우스 호버 전까지 보이지 않는 문제 방지:
    // 핸들 생성 직후 메시지 루프가 비는 시점에 강제로 한 번 더 그려준다. (ControlButton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
