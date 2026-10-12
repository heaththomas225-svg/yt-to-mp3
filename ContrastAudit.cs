using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

// For every theme and every studio page: how readable is each piece of writing against what is really behind it (WCAG contrast ratio).   csc /main:CA ... ContrastAudit.cs
// run: ContrastAudit.exe [minimum ratio for small writing, default 4.5]   (uses its own empty data folder)
static class CA
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static double Lin(int v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
    static double Lum(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
    public static double Ratio(Color a, Color b) { double l1 = Lum(a), l2 = Lum(b); if (l1 < l2) { var t = l1; l1 = l2; l2 = t; } return (l1 + 0.05) / (l2 + 0.05); }
    static Color Over(Color fg, Color bg) { double a = fg.A / 255.0; return Color.FromArgb(255, (int)Math.Round(fg.R * a + bg.R * (1 - a)), (int)Math.Round(fg.G * a + bg.G * (1 - a)), (int)Math.Round(fg.B * a + bg.B * (1 - a))); }
    static Color Median(List<Color> l) { Func<Func<Color, int>, int> m = f => { var v = l.Select(f).OrderBy(x => x).ToList(); return v[v.Count / 2]; }; return Color.FromArgb(255, m(c => c.R), m(c => c.G), m(c => c.B)); }

    public class Item { public string Theme, Page, S; public float Px; public double Ratio; public Color Text, Back; }
    static double minSmall = 4.5, minLarge = 3.0;

    static void Page(string theme, string page, List<Item> bad, ref int total)
    {
        Bitmap bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        StudioForm.NoTextDraw = true; List<StudioForm.AuditText> list = null;
        try
        {
            for (int pass = 0; pass < 2; pass++) { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); } }
            list = StudioForm.TxtAudit;
        }
        finally { StudioForm.TxtAudit = null; StudioForm.NoTextDraw = false; }
        foreach (var t in list)
        {
            if (string.IsNullOrWhiteSpace(t.S) || t.Ink.Width < 3 || t.Ink.Height < 3 || t.C.A < 20) continue;
            var samples = new List<Color>(); var r = Rectangle.Intersect(Rectangle.Round(t.Ink), new Rectangle(0, 0, bmp.Width, bmp.Height)); if (r.Width < 2 || r.Height < 2) continue;
            for (int iy = 0; iy < 3; iy++) for (int ix = 0; ix < 7; ix++) samples.Add(bmp.GetPixel(Math.Min(bmp.Width - 1, r.X + (int)((ix + 0.5) * r.Width / 7)), Math.Min(bmp.Height - 1, r.Y + (int)((iy + 0.5) * r.Height / 3))));
            var back = Median(samples); var text = Over(t.C, back); double ratio = Ratio(text, back); total++;
            bool large = t.Px >= 18 || (t.Px >= 14 && t.Bold); double need = large ? minLarge : minSmall;
            if (ratio < need) bad.Add(new Item { Theme = theme, Page = page, S = t.S.Length > 28 ? t.S.Substring(0, 28) : t.S, Px = t.Px, Ratio = ratio, Text = text, Back = back });
        }
        bmp.Dispose();
    }

    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; if (args.Length > 0) double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out minSmall);
        string work = Path.Combine(Path.GetTempPath(), "ca_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(500);
            var names = new[] { "Mix", "Mastering", "Lyrics", "My Projects", "Community", "Visualiser", "Profile", "Artists" };
            var bad = new List<Item>(); var perTheme = new List<KeyValuePair<string, int>>(); int grand = 0;
            for (int ti = 0; ti < Themes.All.Count; ti++)
            {
                string tn = Themes.All[ti].Name; Themes.Apply(ti); Pump(450); int before = bad.Count, total = 0;
                for (int t = 0; t < names.Length; t++) { SetF("tab", t); SetF("selTrack", 0); Pump(80); Page(tn, names[t], bad, ref total); }
                SetF("tab", 1); SetF("mpTab", 1); Pump(80); Page(tn, "Mastering presets", bad, ref total); SetF("mpTab", 0); SetF("tab", 0); M("OpenMenu"); Pump(80); Page(tn, "Side menu", bad, ref total); M("CloseMenu");
                perTheme.Add(new KeyValuePair<string, int>(tn, bad.Count - before)); grand += total;
            }
            Console.WriteLine("minimum contrast: small writing " + minSmall.ToString("0.0") + ", large writing " + minLarge.ToString("0.0") + "  (" + grand + " pieces of writing looked at in " + Themes.All.Count + " themes)");
            foreach (var kv in perTheme.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key.PadRight(18) + kv.Value + " hard to read");
            Console.WriteLine();
            foreach (var g in bad.GroupBy(b => b.S + "|" + b.Page).OrderByDescending(g => g.Count()).Take(40)) { var w = g.OrderBy(x => x.Ratio).First(); Console.WriteLine("  x" + g.Count() + "  \"" + w.S + "\"  on " + w.Page + "  worst " + w.Ratio.ToString("0.0") + ":1 in " + w.Theme + "  text " + w.Text.R + "," + w.Text.G + "," + w.Text.B + " on " + w.Back.R + "," + w.Back.G + "," + w.Back.B + "  " + w.Px + "px"); }
            Console.WriteLine(bad.Count <= 2 ? "=== contrast audit passed ===" : "=== " + bad.Count + " hard to read FAILED ==="); if (args.Length > 1) File.WriteAllLines(args[1], bad.Select(b => b.Theme + "\t" + b.Page + "\t" + b.S + "\t" + b.Px + "\t" + b.Ratio.ToString("0.00")).ToArray());
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
