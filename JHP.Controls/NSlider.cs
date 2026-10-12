namespace JHP.Controls;

using System.ComponentModel; // 맨 위에 추가
public class NSlider : Control
{
    private int _value = 0;
    private int _min = 0;
    private int _max = 100;
    private bool _dragging = false;

    public event EventHandler? ValueChanged;

    /// <summary>마우스로 끌기/클릭을 마쳤을 때 발생한다(키보드 조작은 해당 없음).</summary>
    public event EventHandler? DragCompleted;

    // Value, Minimum, Maximum 각각 바로 위에 추가
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, _min, _max);
            if (_value == clamped) return;
            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get => _min; set { _min = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get => _max; set { _max = value; Invalidate(); } }

    public NSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Height = 20;
    }

    // 키보드: ←/→ ±1, PgUp/PgDn ±10, Home/End 처음/끝. 마우스 휠: ±5.
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int step = e.KeyCode switch
        {
            Keys.Left or Keys.Down => -1,
            Keys.Right or Keys.Up => 1,
            Keys.PageDown => -10,
            Keys.PageUp => 10,
            _ => 0
        };
        if (step != 0) { Value += step; e.Handled = true; }
        else if (e.KeyCode == Keys.Home) { Value = _min; e.Handled = true; }
        else if (e.KeyCode == Keys.End) { Value = _max; e.Handled = true; }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += e.Delta > 0 ? 5 : -5;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        int cy = Height / 2, half = (int)Math.Round(2 * s), tr = (int)Math.Round(7 * s);

        using var trackBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
        g.FillRectangle(trackBrush, 0, cy - half, Width, half * 2);

        int fillW = _max == _min ? 0 : (int)((double)(_value - _min) / (_max - _min) * Width);
        using var fillBrush = new SolidBrush(Color.FromArgb(100, 160, 255));
        g.FillRectangle(fillBrush, 0, cy - half, fillW, half * 2);

        int tx = Math.Clamp(fillW, tr, Width - tr);
        using var thumbBrush = new SolidBrush(Color.White);
        g.FillEllipse(thumbBrush, tx - tr, cy - tr, tr * 2, tr * 2);

        // 키보드 포커스 표시 (마우스로만 쓸 때는 보이지 않음)
        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Color.FromArgb(100, 160, 255), Math.Max(1f, s));
            g.DrawEllipse(ring, tx - tr - 2, cy - tr - 2, tr * 2 + 4, tr * 2 + 4);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); _dragging = true; UpdateValue(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_dragging) UpdateValue(e.X); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        bool was = _dragging;
        _dragging = false;
        if (was) DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateValue(int x)
    {
        double ratio = Math.Clamp((double)x / Width, 0.0, 1.0);
        Value = (int)(ratio * (_max - _min) + _min);
    }
}