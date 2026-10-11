namespace JHP.Controls;

// 메뉴바(menuStrip) 전용 다크테마 렌더러
// 기본 ToolStripProfessionalRenderer는 호버/선택 시 밝은 파란 그라데이션을 그려서
// 앱의 어두운 배경(28,28,28 / 40,40,40)과 어울리지 않으므로, 색상을 직접 지정한다.
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Color bg = e.Item.Enabled && (e.Item.Selected || e.Item.Pressed)
            ? Color.FromArgb(55, 90, 145)   // 버튼(강조) 색상과 통일
            : Color.FromArgb(40, 40, 40);   // 메뉴바 기본 배경

        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.White : Color.FromArgb(110, 110, 110);
        base.OnRenderItemText(e);
    }

    // 기본 렌더러는 체크 상자를 밝은 색(노랑/주황)으로 그려 어두운 메뉴에서 튄다 → 흰색 ✓만 직접 그린다.
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        float s = e.Item.Owner is { } owner ? owner.DeviceDpi / 96f : 1f;
        using var bg = new SolidBrush(Color.FromArgb(60, 60, 60));
        e.Graphics.FillRectangle(bg, r);
        using var pen = new Pen(Color.White, 1.6f * s);
        var old = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, u = r.Height / 6f;
        e.Graphics.DrawLines(pen, [new PointF(cx - 2.2f * u, cy), new PointF(cx - 0.6f * u, cy + 1.6f * u), new PointF(cx + 2.4f * u, cy - 1.8f * u)]);
        e.Graphics.SmoothingMode = old;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // 메뉴바 기본 테두리 제거 (다크테마와 어울리지 않는 밝은 외곽선 숨김)
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(55, 55, 55));
        int midY = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 4, midY, e.Item.Width - 4, midY);
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(55, 90, 145);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 90, 145);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(55, 90, 145);
        public override Color MenuItemBorder => Color.FromArgb(55, 90, 145);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(55, 90, 145);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(55, 90, 145);
        public override Color MenuBorder => Color.FromArgb(40, 40, 40);
        public override Color CheckBackground => Color.FromArgb(60, 60, 60);
        public override Color CheckSelectedBackground => Color.FromArgb(60, 60, 60);
        public override Color CheckPressedBackground => Color.FromArgb(60, 60, 60);
        public override Color SeparatorDark => Color.FromArgb(55, 55, 55);
        public override Color SeparatorLight => Color.FromArgb(55, 55, 55);
        public override Color MenuStripGradientBegin => Color.FromArgb(40, 40, 40);
        public override Color MenuStripGradientEnd => Color.FromArgb(40, 40, 40);
        public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientBegin => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientEnd => Color.FromArgb(40, 40, 40);
    }
}
