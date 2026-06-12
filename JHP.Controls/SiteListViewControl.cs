using JHP.Api;
using System.ComponentModel;


namespace JHP.Controls;

public class SiteListViewControl : Control
{
    private readonly List<Site> _sites = [];
    private int _selectedIndex = -1;
    private const int ItemHeight = 28; // 항목 높이(px)

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? ItemDoubleClick;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set { _selectedIndex = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
    }

    public Site? SelectedSite =>
        _selectedIndex >= 0 && _selectedIndex < _sites.Count ? _sites[_selectedIndex] : null;

    public SiteListViewControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        BackColor = Color.FromArgb(30, 30, 30);
    }

    public void SetSites(IEnumerable<Site> sites)
    {
        _sites.Clear();
        _sites.AddRange(sites);
        _selectedIndex = -1;
        Invalidate();
    }

    public void AddSite(Site site) { _sites.Add(site); Invalidate(); }

    public void RemoveSite(int index)
    {
        if (index < 0 || index >= _sites.Count) return;
        _sites.RemoveAt(index);
        _selectedIndex = Math.Min(_selectedIndex, _sites.Count - 1);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using var bgBrush = new SolidBrush(BackColor);
        g.FillRectangle(bgBrush, ClientRectangle);

        using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        for (int i = 0; i < _sites.Count; i++)
        {
            var rect = new Rectangle(0, i * ItemHeight, Width, ItemHeight);
            bool selected = i == _selectedIndex;

            if (selected)
            {
                using var selBrush = new SolidBrush(Color.FromArgb(55, 90, 145));
                g.FillRectangle(selBrush, rect);
            }

            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(_sites[i].Name, Font, textBrush,
                new Rectangle(rect.X + 8, rect.Y, rect.Width - 8, rect.Height), sf);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = e.Y / ItemHeight;
        if (i >= 0 && i < _sites.Count) SelectedIndex = i;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        int i = e.Y / ItemHeight;
        if (i >= 0 && i < _sites.Count) { SelectedIndex = i; ItemDoubleClick?.Invoke(this, EventArgs.Empty); }
    }
}