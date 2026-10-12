using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Every font of the Fonts page: has every letter and digit the studio writes, is resized so its lines are as wide and as tall as the original's (so boxes stay tidy),
// and when the whole studio is set in it no writing is cut off or runs off a page.   csc /main:FT ... FontTest.cs     run: FontTest.exe [folder with the font files, default ..\purphex-web\public\fonts] [list file to write]
static class FT
{
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern uint GetGlyphIndices(IntPtr hdc, string lpstr, int c, ushort[] pgi, uint fl);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h); [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h); [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc); [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }

    const string Must = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:,-+/%()'!?=";
    static string Missing(Font font, string chars)
    {
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero), hf = font.ToHfont(), old = SelectObject(dc, hf); var gi = new ushort[chars.Length]; GetGlyphIndices(dc, chars, chars.Length, gi, 1);
        SelectObject(dc, old); DeleteObject(hf); DeleteDC(dc); var m = new StringBuilder(); for (int i = 0; i < chars.Length; i++) if (gi[i] == 0xFFFF) m.Append(chars[i]); return m.ToString();
    }
    static readonly string[] Sample = { "PURPHEX MUSIC", "SPLIT VOCALS + BEAT", "Lead vocal", "0:00 / 3:25", "-14.0 LUFS", "ADD EFFECT", "Parametric EQ" };
    static readonly float[] Sizes = { 10, 12, 14, 16, 22, 30 };
    static double W(Graphics g, Font f, string s) { return g.MeasureString(s, f, 1000000, StringFormat.GenericTypographic).Width; }

    [STAThread]
    static int Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string src = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\purphex-web\public\fonts"));
        string work = Path.Combine(Path.GetTempPath(), "ft_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work; string realSet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "settings.txt"); byte[] realBytes = File.Exists(realSet) ? File.ReadAllBytes(realSet) : null;   /* choosing fonts saves the choice in your real settings: put them back exactly as they were */ int fails = 0; var failed = new List<string>(); var report = new StringBuilder();
        try
        {
            if (!File.Exists(Path.Combine(src, "fonts.txt"))) { Console.WriteLine("no font files at " + src + ": nothing to test"); Console.WriteLine("=== font checks passed (no font files here) ==="); return 0; }
            Directory.CreateDirectory(FontKit.Dir); foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(FontKit.Dir, Path.GetFileName(f)), true);
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1366, 768); sf.Show(); Pump(400);
            FontKit.Build(); Console.WriteLine(FontKit.All.Count + " fonts to test (" + FontKit.All.Count(e => e.Installed) + " on this PC)");
            var bmp0 = new Bitmap(1, 1); var g0 = Graphics.FromImage(bmp0); FontKit.Apply(FontKit.Find(FontKit.DefaultId)); var baseW = new double[Sample.Length, Sizes.Length]; double baseLine; double baseCap;
            for (int i = 0; i < Sample.Length; i++) for (int j = 0; j < Sizes.Length; j++) using (var f = FontKit.Make(Sizes[j], true)) baseW[i, j] = W(g0, f, Sample[i]); using (var f = FontKit.Make(100, true)) baseLine = f.GetHeight(g0);
            using (var f = FontKit.Make(100, true)) using (var p = new System.Drawing.Drawing2D.GraphicsPath()) { p.AddString("HEXZ", f.FontFamily, (int)f.Style, 100f, PointF.Empty, StringFormat.GenericTypographic); baseCap = p.GetBounds().Height / (f.Size / 100.0); }
            var names = new[] { "Mix", "Mastering", "Lyrics", "My Projects", "Community", "Visualiser", "Profile", "Artists", "Fonts" }; int audited = 0, perfect = 0; double worstUp = 0, worstLine = 0;
            foreach (var e in FontKit.All.ToArray())
            {
                var problems = new List<string>(); var fam = FontKit.Load(e); if (fam == null) { problems.Add("could not be loaded"); failed.Add(e.Id); Console.WriteLine("FAIL  " + e.Name + ": " + string.Join("; ", problems)); fails++; continue; }
                FontKit.Apply(e);
                using (var f = FontKit.Make(30, true))
                {
                    string miss = Missing(f, Must); if (miss.Length > 0) problems.Add("no glyphs for [" + miss.Replace(" ", "space") + "]");
                    double up = 0, down = 99; for (int i = 0; i < Sample.Length; i++) for (int j = 0; j < Sizes.Length; j++) { using (var ff = FontKit.Make(Sizes[j], true)) { double r = W(g0, ff, Sample[i]) / baseW[i, j]; up = Math.Max(up, r); down = Math.Min(down, r); } }
                    double line = f.GetHeight(g0) / (30 * FontKit.Scale == 0 ? 1 : 1.0) / (baseLine * 30 / 100.0); double cap; using (var p = new System.Drawing.Drawing2D.GraphicsPath()) { p.AddString("HEXZ", f.FontFamily, (int)f.Style, 100f, PointF.Empty, StringFormat.GenericTypographic); cap = p.GetBounds().Height * (f.Size / 100.0) / (30.0 * baseCap / 100.0 * 1.0); }
                    worstUp = Math.Max(worstUp, up); worstLine = Math.Max(worstLine, line);
                    if (up > 1.15) problems.Add("lines up to " + up.ToString("0.00") + "x as wide as the original"); if (down < 0.45) problems.Add("lines as little as " + down.ToString("0.00") + "x of the original width (too small to read well)");
                    if (FontKit.Scale < 0.575f) problems.Add("has to be shrunk to " + FontKit.Scale.ToString("0.00") + " of the normal size to fit, which is too small to read"); if (line > 1.30) problems.Add("lines " + line.ToString("0.00") + "x as tall as the original"); if (cap > 1.35) problems.Add("capitals " + cap.ToString("0.00") + "x as tall as the original");
                    report.AppendLine(e.Id + "\t" + e.Name + "\tscale " + FontKit.Scale.ToString("0.00") + "\twidth " + down.ToString("0.00") + "-" + up.ToString("0.00") + "\tline " + line.ToString("0.00") + "\tcaps " + cap.ToString("0.00"));
                }
                // the whole studio in this font: nothing cut off, nothing off the page (two window sizes)
                int cut = 0, edge = 0; var examples = new List<string>();
                foreach (var size in new[] { new Size(1366, 768), new Size(1240, 680) })
                {
                    sf.ClientSize = size; Pump(30);
                    for (int t = 0; t < names.Length; t++)
                    {
                        SetF("tab", t); SetF("selTrack", 0); if (t == 1) SetF("mpTab", 0); var bmp = new Bitmap(size.Width, size.Height); StudioForm.TxtAudit = new List<StudioForm.AuditText>();
                        try { using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, size)); for (int pass = 0; pass < 2; pass++) { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); } } }
                        catch (Exception ex) { problems.Add("painting " + names[t] + " failed: " + (ex.InnerException ?? ex).Message); }
                        var list = StudioForm.TxtAudit ?? new List<StudioForm.AuditText>(); StudioForm.TxtAudit = null; bmp.Dispose();
                        foreach (var a in list) { if (a.Cut) { cut++; if (examples.Count < 3) examples.Add(names[t] + ": \"" + (a.S.Length > 24 ? a.S.Substring(0, 24) : a.S) + "\" cut off"); } if (a.Ink.Right > size.Width + 3 || a.Ink.X < -3) { edge++; if (examples.Count < 3) examples.Add(names[t] + ": \"" + (a.S.Length > 24 ? a.S.Substring(0, 24) : a.S) + "\" off the page"); } }
                    }
                }
                if (cut + edge > 0) problems.Add(cut + " pieces of writing cut off and " + edge + " off the page (" + string.Join("; ", examples.ToArray()) + ")");
                audited++; if (problems.Count == 0) perfect++; else { fails++; failed.Add(e.Id); Console.WriteLine("FAIL  " + e.Name + " (" + e.Cat + "): " + string.Join("; ", problems.ToArray())); }
            }
            FontKit.Apply(FontKit.Find(FontKit.DefaultId));
            Console.WriteLine(perfect + " of " + audited + " fonts fit perfectly (widest line seen " + worstUp.ToString("0.00") + "x the original, tallest " + worstLine.ToString("0.00") + "x)");
            if (args.Length > 1) { File.WriteAllLines(args[1], failed.ToArray()); File.WriteAllText(args[1] + ".report.txt", report.ToString()); }
        }
        finally { try { Directory.Delete(work, true); } catch { } try { if (realBytes != null) File.WriteAllBytes(realSet, realBytes); } catch { } }
        Console.WriteLine(fails == 0 ? "=== font checks passed ===" : "=== " + fails + " FAILED ===");
        return fails == 0 ? 0 : 1;
    }
}
