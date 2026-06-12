namespace JHP.Api;

public static class ReSize
{
    private const int RESIZE_THICK = 8; // 마우스 드래그 감지 영역(px)

    public enum MousePosition
    {
        None, Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    public static MousePosition GetMousePosition(Form form, Point cursor)
    {
        bool left = cursor.X <= RESIZE_THICK;
        bool right = cursor.X >= form.Width - RESIZE_THICK;
        bool top = cursor.Y <= RESIZE_THICK;
        bool bottom = cursor.Y >= form.Height - RESIZE_THICK;

        if (top && left) return MousePosition.TopLeft;
        if (top && right) return MousePosition.TopRight;
        if (bottom && left) return MousePosition.BottomLeft;
        if (bottom && right) return MousePosition.BottomRight;
        if (left) return MousePosition.Left;
        if (right) return MousePosition.Right;
        if (top) return MousePosition.Top;
        if (bottom) return MousePosition.Bottom;
        return MousePosition.None;
    }

    public static Cursor SetThick(MousePosition pos) => pos switch
    {
        MousePosition.Left or MousePosition.Right => Cursors.SizeWE,
        MousePosition.Top or MousePosition.Bottom => Cursors.SizeNS,
        MousePosition.TopLeft or MousePosition.BottomRight => Cursors.SizeNWSE,
        MousePosition.TopRight or MousePosition.BottomLeft => Cursors.SizeNESW,
        _ => Cursors.Default
    };
}