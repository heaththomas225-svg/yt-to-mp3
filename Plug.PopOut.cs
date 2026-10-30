using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#if !PURPHEX_CORE
// POP OUT: any plugin in a window of its own (its knobs and choices), so it can sit on another monitor, out of the way of the studio.
// It has the Purphex header like every plugin window: presets, SAVE, on / off, undo and redo, and right-click on a knob for MIDI Learn.
class GenericFxWindow : PxWindow
{
    readonly List<Param> nums, chs; const int CellW = 124, CellH = 118;
    public GenericFxWindow(Fx f) : base(f, f.Title.Replace("Purphex ", ""), SizeFor(f))
    {
        Accent = Cyan; var hid = new HashSet<string>((f.Hidden ?? "").Split(','));
        nums = f.P.Where(p => !p.IsChoice && !hid.Contains(p.Id)).ToList(); chs = f.P.Where(p => p.IsChoice && !hid.Contains(p.Id)).ToList();
    }
    static Size SizeFor(Fx f)
    {
        var hid = new HashSet<string>((f.Hidden ?? "").Split(',')); int n = f.P.Count(p => !p.IsChoice && !hid.Contains(p.Id)), c = f.P.Count(p => p.IsChoice && !hid.Contains(p.Id));
        int cols = Math.Max(4, Math.Min(7, n)), rows = Math.Max(1, (n + cols - 1) / cols);
        return new Size(Math.Max(760, 32 + cols * CellW + 24), Math.Min(980, 76 + 30 + rows * CellH + (c > 0 ? 24 + ((c + 2) / 3) * 44 : 0) + 40));
    }
    protected override string SubTitle { get { return "popped out of the studio"; } }   // (what it does is in the hint of its title in the studio: the long text did not fit here)
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        int cols = Math.Max(1, (int)((a.Width - 24) / CellW)); float knobsH = Math.Max(1, (nums.Count + cols - 1) / cols) * CellH + 30;
        var kp = new RectangleF(a.X, a.Y, a.Width, Math.Min(knobsH, a.Height - (chs.Count > 0 ? 70 : 0))); Panel(g, kp, "KNOBS", Cyan); Color[] cs = { Cyan, Pink, Gold, Lime, Violet, Orange };
        float x0 = kp.X + 12 + (kp.Width - 24 - cols * CellW) / 2;
        for (int i = 0; i < nums.Count; i++) { float cx = x0 + (i % cols) * CellW + CellW / 2f, cy = kp.Y + 34 + (i / cols) * CellH + 30; if (cy + 40 > kp.Bottom) break; Knob(g, cx, cy, 24, nums[i].Id, Win.Up(nums[i].Name), cs[i % cs.Length]); }
        if (chs.Count == 0) return;
        var cp = new RectangleF(a.X, kp.Bottom + 10, a.Width, a.Bottom - kp.Bottom - 10); Panel(g, cp, "CHOICES", Gold); float cw = (cp.Width - 28 - 20) / 3;
        for (int i = 0; i < chs.Count; i++) { float x = cp.X + 14 + (i % 3) * (cw + 10), y = cp.Y + 28 + (i / 3) * 44; if (y + 36 > cp.Bottom) break; Txt(g, Win.Up(chs[i].Name), 9.5f, Dim, new RectangleF(x, y, cw, 13), StringAlignment.Near, true); Choice(g, new RectangleF(x, y + 14, cw, 26), chs[i].Id, Gold, 10.5f); }
    }
}
#endif

// Where each plugin window was left (by the kind of plugin): it opens there again, on whichever monitor it was on
static class WindowPlaces
{
    static Dictionary<string, Point> places; static string FileName { get { return Path.Combine(StudioEnv.DataDir, "windowplaces.txt"); } }
    static void Ensure()
    {
        if (places != null) return; places = new Dictionary<string, Point>();
        try { if (File.Exists(FileName)) foreach (var line in File.ReadAllLines(FileName)) { var p = line.Split('\t'); int x, y; if (p.Length == 3 && int.TryParse(p[1], out x) && int.TryParse(p[2], out y)) places[p[0]] = new Point(x, y); } } catch { }
    }
    public static bool Get(string key, Size size, out Point at)
    {
        Ensure(); at = Point.Empty; Point p; if (!places.TryGetValue(key, out p)) return false;
        var r = new Rectangle(p, size); foreach (var s in Screen.AllScreens) { var vis = Rectangle.Intersect(s.WorkingArea, r); if (vis.Width >= 120 && vis.Height >= 60) { at = p; return true; } }   // (a monitor that was unplugged: back to the middle)
        return false;
    }
    public static void Set(string key, Point p)
    {
        Ensure(); places[key] = p;
        try { File.WriteAllLines(FileName, places.Select(kv => kv.Key + "\t" + kv.Value.X + "\t" + kv.Value.Y)); } catch { }
    }
}
