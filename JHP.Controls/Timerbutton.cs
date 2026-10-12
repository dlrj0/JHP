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
        bool hovered = _hovered;

        // 투명 브러시는 더블버퍼(검정)가 그대로 보이므로 부모 배경색으로 칠한다
        Color bg = hovered ? Color.FromArgb(60, 60, 60) : (Parent?.BackColor ?? Color.FromArgb(28, 28, 28));
        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        // 동작 중이면 초록색, 정지 상태면 흰색 — 한눈에 "돌고 있다"는 걸 알 수 있게
        Color iconColor = _active ? Color.FromArgb(110, 220, 140) : Color.White;

        float s = DeviceDpi / 96f;
        int cx = Width / 2, cy = Height / 2, r = (int)Math.Round(8 * s);
        int hand = (int)Math.Round(2 * s);
        using var pen = new Pen(iconColor, 1.6f * s);

        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);        // 시계 원
        g.DrawLine(pen, cx, cy, cx, cy - r + hand);                 // 12시 방향 바늘
        g.DrawLine(pen, cx, cy, cx + r - 2 * hand, cy);                 // 3시 방향 바늘
        g.DrawLine(pen, cx - hand, cy - r - hand, cx + hand, cy - r - hand);  // 상단 손잡이

        // 키보드(Tab)로 포커스가 왔을 때만 보이는 표시 (NSlider와 같은 색)
        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Color.FromArgb(100, 160, 255), Math.Max(1f, s));
            g.DrawRectangle(ring, 1, 1, Width - 3, Height - 3);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
            RightClick?.Invoke(this, e);
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

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; base.OnMouseLeave(e); Invalidate(); }

    // 초기 표시 시 아이콘이 마우스 호버 전까지 보이지 않는 문제 방지:
    // 핸들 생성 직후 메시지 루프가 비는 시점에 강제로 한 번 더 그려준다. (ControlButton.cs와 동일 패턴)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(new Action(Invalidate));
    }
}
