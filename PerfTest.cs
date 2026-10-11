using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// How long one frame takes to draw on each page (the lower the smoother).   csc /main:PerfT ... PerfTest.cs     run: PerfT.exe [width height]
static class PerfT
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static Bitmap bmp; static MethodInfo paint;
    static double Time(string name, int n)
    {
        if (bmp == null || bmp.Width != sf.ClientSize.Width || bmp.Height != sf.ClientSize.Height) { if (bmp != null) bmp.Dispose(); bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); }
        using (var g = Graphics.FromImage(bmp)) { var ea = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); for (int i = 0; i < 5; i++) paint.Invoke(sf, new object[] { ea }); var sw = Stopwatch.StartNew(); for (int i = 0; i < n; i++) paint.Invoke(sf, new object[] { ea }); double ms = sw.Elapsed.TotalMilliseconds / n; Prof.On = true; Prof.Totals.Clear(); Prof.Acc.Clear(); Prof.Cnt.Clear(); Prof.Notes.Clear(); for (int i = 0; i < 10; i++) paint.Invoke(sf, new object[] { ea }); Prof.On = false; var sb = new System.Text.StringBuilder(); foreach (var kv in Prof.Totals) if (kv.Value / 10 >= 0.3) sb.Append("   " + kv.Key + " " + (kv.Value / 10).ToString("0.0")); sb.Append("  ||"); if (Prof.Notes.Count > 0 && name.StartsWith("mastering (stopped")) Console.WriteLine("MISS: " + string.Join(" ; ", Prof.Notes.GetRange(0, Math.Min(40, Prof.Notes.Count)).ToArray()) + " cache=" + Sprites.Count); foreach (var kv in Prof.Acc) if (kv.Value / 10 >= 0.3) sb.Append("  " + kv.Key + " " + (kv.Value / 10).ToString("0.0") + "ms/" + (Prof.Cnt[kv.Key] / 10) + "x");  Console.WriteLine(string.Format("{0,-34} {1,6:0.0} ms |{2}", name, ms, sb)); return ms; }
    }
    [STAThread]
    static void Main(string[] args)
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_perf_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; paint = typeof(StudioForm).GetMethod("PaintAll", BF);
        int W = args.Length > 1 ? int.Parse(args[0]) : 1920, H = args.Length > 1 ? int.Parse(args[1]) : 1080;
        for (int i = 0; i < 8; i++) eng.Tracks.Add(StudioEngine.NewTrack("Track " + i, i == 0 ? "vocal" : "audio", Clip(120)));
        sf = new StudioForm(app); sf.ClientSize = new Size(W, H); sf.Show(); Pump(500); int ov = Prefs.OutVol; Prefs.OutVol = 0;
        var tp = eng.MasterTap; var tpp = eng.MasterTapPre; var rnd = new Random(5); for (int i = 0; i < tp.Length; i++) { float s0 = (float)(0.3 * Math.Sin(i * 0.02) + 0.25 * Math.Sin(i * 0.11) + 0.1 * (rnd.NextDouble() - 0.5)); tp[i] = s0; tpp[i] = s0 * 0.7f; }
        Console.WriteLine("size " + W + " x " + H + " theme " + Themes.Current.Name + " rainbow " + Themes.Current.Rainbow); Prof.On = false;
        SetF("tab", 0); { var bmp0 = new Bitmap(W, H); using (var g0 = Graphics.FromImage(bmp0)) { var ea0 = new PaintEventArgs(g0, new Rectangle(Point.Empty, sf.ClientSize)); for (int i = 0; i < 4; i++) { Prof.On = true; Prof.Acc.Clear(); Prof.Cnt.Clear(); Prof.Notes.Clear(); paint.Invoke(sf, new object[] { ea0 }); Prof.On = false; int mc; Prof.Cnt.TryGetValue("sprite.miss", out mc); Console.WriteLine("frame " + i + " misses " + mc + " cache " + Sprites.Count + " " + (Prof.Notes.Count > 0 ? Prof.Notes[0] : "")); } } }
        SetF("tab", 0); Time("mix (8 tracks, stopped)", 20);
        eng.Playing = true; Time("mix (8 tracks, playing)", 20); eng.Playing = false;
        SetF("tab", 1); Time("mastering (stopped)", 20); eng.Playing = true; Time("mastering (playing)", 20); eng.Playing = false;
        SetF("tab", 5); Time("visualiser (stopped)", 20); eng.Playing = true; Time("visualiser (playing)", 30); eng.Playing = false;
        SetF("tab", 2); Time("lyrics", 20); SetF("tab", 3); Time("projects", 20); SetF("tab", 4); Time("community", 20);
        SetF("tab", 0); SetF("themeOpen", true); Time("mix + theme sheet", 20); SetF("themeOpen", false);
        Prefs.OutVol = ov; Console.WriteLine("=== perf done ===");
        Environment.Exit(0);
    }
}
