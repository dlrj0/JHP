namespace JHP.Controls;

// 메뉴바(menuStrip) 전용 다크테마 렌더러
// 기본 ToolStripProfessionalRenderer는 호버/선택 시 밝은 파란 그라데이션을 그려서
// 앱의 어두운 배경(28,28,28 / 40,40,40)과 어울리지 않으므로, 색상을 직접 지정한다.
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Color bg = e.Item.Selected || e.Item.Pressed
            ? Color.FromArgb(55, 90, 145)   // 버튼(강조) 색상과 통일
            : Color.FromArgb(40, 40, 40);   // 메뉴바 기본 배경

        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.White;
        base.OnRenderItemText(e);
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
        public override Color MenuStripGradientBegin => Color.FromArgb(40, 40, 40);
        public override Color MenuStripGradientEnd => Color.FromArgb(40, 40, 40);
        public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientBegin => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(40, 40, 40);
        public override Color ImageMarginGradientEnd => Color.FromArgb(40, 40, 40);
    }
}
