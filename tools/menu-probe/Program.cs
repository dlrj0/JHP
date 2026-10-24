using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 임시 프로브: 실제 팝업(ContextMenuStrip, 투명도 0이라 보이지 않음)에서 항목의 우클릭/좌클릭 이벤트와 사이트 이동(Click 핸들러) 여부를 기록한다.
var log = new List<string>();
var form = new QuietForm { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, 0), ShowInTaskbar = false, Size = new Size(400, 200) };
form.Show();
var menu = new ContextMenuStrip { Opacity = 0 };
var item = new LogItem("site item with a long text", log);
item.Click += (_, _) => log.Add(">>> NAVIGATE (Click handler ran)");
item.DeleteRequested += () => { log.Add(">>> DELETE requested"); menu.Close(); };
item.ManageRequested += () => { log.Add(">>> MANAGE MENU requested"); menu.Close(); };
menu.Items.Add(item);
menu.Items.Add(new ToolStripMenuItem("other"));
void Pump() { for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(30); } }
menu.Show(form, new Point(10, 10));
Pump();
var b = item.Bounds;
Console.WriteLine($"menu visible={menu.Visible} opacity={menu.Opacity} bounds={menu.Bounds} item={b}");
IntPtr lp = (IntPtr)((b.Y + 5) << 16 | (b.X + 10));
Native.SendMessage(menu.Handle, 0x0200, IntPtr.Zero, lp); Pump();
Console.WriteLine("--- right click");
log.Clear();
Native.SendMessage(menu.Handle, 0x0204, (IntPtr)2, lp); Pump();
Native.SendMessage(menu.Handle, 0x0205, IntPtr.Zero, lp); Pump();
foreach (var l in log) Console.WriteLine(l);
Console.WriteLine($"menu visible after right click = {menu.Visible}");

if (!menu.Visible) { menu.Show(form, new Point(10, 10)); Pump(); }
b = item.Bounds; lp = (IntPtr)((b.Y + 5) << 16 | (b.X + 10));
Native.SendMessage(menu.Handle, 0x0200, IntPtr.Zero, lp); Pump();
Console.WriteLine("--- left click");
log.Clear();
Native.SendMessage(menu.Handle, 0x0201, (IntPtr)1, lp); Pump();
Native.SendMessage(menu.Handle, 0x0202, IntPtr.Zero, lp); Pump();
foreach (var l in log) Console.WriteLine(l);
if (!menu.Visible) { menu.Show(form, new Point(10, 10)); Pump(); }
b = item.Bounds; lp = (IntPtr)((b.Y + 5) << 16 | (b.X + b.Width - 8));
Native.SendMessage(menu.Handle, 0x0200, IntPtr.Zero, lp); Pump();
Console.WriteLine("--- left click on X zone");
log.Clear();
Native.SendMessage(menu.Handle, 0x0201, (IntPtr)1, lp); Pump();
Native.SendMessage(menu.Handle, 0x0202, IntPtr.Zero, lp); Pump();
foreach (var l in log) Console.WriteLine(l);
Console.WriteLine("--- keyboard activation (PerformClick) after a right click");
log.Clear();
if (!menu.Visible) { menu.Show(form, new Point(10, 10)); Pump(); }
b = item.Bounds; lp = (IntPtr)((b.Y + 5) << 16 | (b.X + 10));
Native.SendMessage(menu.Handle, 0x0200, IntPtr.Zero, lp); Pump();
Native.SendMessage(menu.Handle, 0x0204, (IntPtr)2, lp); Pump();
Native.SendMessage(menu.Handle, 0x0205, IntPtr.Zero, lp); Pump();
log.Clear();
item.PerformClick();
foreach (var l in log) Console.WriteLine(l);
form.Close();

class LogItem : ToolStripMenuItem
{
    // JHP/Form1.cs SiteMenuItem과 같은 로직
    public static int DeleteZoneWidth = 28;
    private readonly List<string> _log;
    private MouseButtons _downButton = MouseButtons.None;
    private bool _downInDeleteZone;
    public event Action? ManageRequested;
    public event Action? DeleteRequested;
    public LogItem(string t, List<string> log) : base(t) { _log = log; }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        _downButton = e.Button;
        _downInDeleteZone = e.Button == MouseButtons.Left && e.X >= Width - DeleteZoneWidth;
        base.OnMouseDown(e);
    }
    protected override void OnClick(EventArgs e)
    {
        bool ignore = _downButton == MouseButtons.Right || _downInDeleteZone;
        _downButton = MouseButtons.None; _downInDeleteZone = false;
        if (ignore) { _log.Add("(click ignored)"); return; }
        base.OnClick(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { ManageRequested?.Invoke(); return; }
        if (e.Button == MouseButtons.Left && e.X >= Width - DeleteZoneWidth) { DeleteRequested?.Invoke(); return; }
        base.OnMouseUp(e);
    }
}

class QuietForm : Form
{
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; } }
}

static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
}
