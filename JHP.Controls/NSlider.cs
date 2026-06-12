namespace JHP.Controls;

using System.ComponentModel; // 맨 위에 추가
public class NSlider : Control
{
    private int _value = 0;
    private int _min = 0;
    private int _max = 100;
    private bool _dragging = false;

    public event EventHandler? ValueChanged;

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
                 ControlStyles.DoubleBuffer, true);
        Height = 20;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int cy = Height / 2;

        using var trackBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
        g.FillRectangle(trackBrush, 0, cy - 2, Width, 4);

        int fillW = _max == _min ? 0 : (int)((double)(_value - _min) / (_max - _min) * Width);
        using var fillBrush = new SolidBrush(Color.FromArgb(100, 160, 255));
        g.FillRectangle(fillBrush, 0, cy - 2, fillW, 4);

        int tx = Math.Clamp(fillW, 7, Width - 7);
        using var thumbBrush = new SolidBrush(Color.White);
        g.FillEllipse(thumbBrush, tx - 7, cy - 7, 14, 14);
    }

    protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; UpdateValue(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_dragging) UpdateValue(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; }

    private void UpdateValue(int x)
    {
        double ratio = Math.Clamp((double)x / Width, 0.0, 1.0);
        Value = (int)(ratio * (_max - _min) + _min);
    }
}