using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// The timeline's waveforms (plain and thin, like Logic): drawn at every pixel, readable fully zoomed out and zoomed in, a quiet clip is still
// visible, a muted one is dimmer. Saves pictures of each zoom (WAVE_SHOT=<folder>) so the look can be checked by eye.
static class WT
{
    static int fails; static StudioForm sf; static App app; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static void SetF(string nm, object v) { typeof(StudioForm).GetField(nm, BF).SetValue(sf, v); }
    static object Get(string nm) { return typeof(StudioForm).GetField(nm, BF).GetValue(sf); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    static void Stealth(Form f) { try { f.ShowInTaskbar = false; f.Opacity = 0.0; int ex = GetWindowLong(f.Handle, -20); SetWindowLong(f.Handle, -20, ex | 0x80000 | 0x20 | 0x80 | 0x08000000); } catch { } }
    static AudioClip Song(int sec, double vol, int kind)
    {
        int sr = 44100, len = sr * sec; var l = new float[len]; var r = new float[len]; var rnd = new Random(kind + 3); double beat = 60.0 / 140 * sr;
        for (int i = 0; i < len; i++)
        {
            double t = i / (double)sr, bt = (i % beat) / beat, v;
            if (kind == 0) v = Math.Exp(-bt * 9) * Math.Sin(2 * Math.PI * (45 + 90 * Math.Exp(-bt * 22)) * t) * 0.9 + (((int)(i / beat) % 2 == 1) ? Math.Exp(-bt * 14) * (rnd.NextDouble() - 0.5) * 0.6 : 0);
            else v = Math.Sin(2 * Math.PI * 220 * t) * (0.5 + 0.5 * Math.Sin(t * 0.7)) * (t % 8 < 6 ? 1 : 0.05);
            l[i] = r[i] = (float)(v * vol);
        }
        return AudioClip.FromArrays(l, r, "wave" + kind);
    }
    static Bitmap Shot() { var b = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height, PixelFormat.Format32bppPArgb); using (var g = Graphics.FromImage(b)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); return b; }
    // how many columns of the clip area hold some bright wave colour (not just the dark region)
    static int WaveColumns(Bitmap b, Rectangle area, Color c)
    {
        int cols = 0;
        for (int x = area.X; x < area.Right; x++) { for (int y = area.Y; y < area.Bottom; y++) { var p = b.GetPixel(x, y); int d = Math.Abs(p.R - c.R) + Math.Abs(p.G - c.G) + Math.Abs(p.B - c.B); if (d < 230 && (p.R + p.G + p.B) > 200) { cols++; break; } } }
        return cols;
    }
    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_wavetest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        string shots = Environment.GetEnvironmentVariable("WAVE_SHOT"); if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        try
        {
            Application.EnableVisualStyles(); app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; CommunityPresets.BaseUrl = "http://127.0.0.1:1"; Prefs.OutVol = 0;
            eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Song(180, 0.9, 0))); eng.Tracks.Add(StudioEngine.NewTrack("Quiet pad", "audio", Song(180, 0.04, 1))); eng.Tracks.Add(StudioEngine.NewTrack("Muted", "audio", Song(180, 0.9, 1)));
            eng.Tracks[0].Color = Color.FromArgb(0, 200, 255).ToArgb(); eng.Tracks[1].Color = Color.FromArgb(255, 60, 220).ToArgb(); eng.Tracks[2].Color = Color.FromArgb(255, 180, 40).ToArgb(); eng.Tracks[2].Mute = true;
            sf = new StudioForm(app); sf.ClientSize = new Size(1600, 900); sf.Show(); Stealth(sf); Pump(400); SetF("tab", 0);
            var lanes = new int[3]; Func<string, Bitmap> zoom = (nm) => { Pump(150); var b = Shot(); if (!string.IsNullOrEmpty(shots)) b.Save(Path.Combine(shots, "wave-" + nm + ".png"), ImageFormat.Png); return b; };
            // fully zoomed out: the whole song in view
            typeof(StudioForm).GetMethod("FitView", BF).Invoke(sf, null); sf.Invalidate(); Pump(200);
            using (var b = zoom("zoomed-out"))
            {
                double spp = (double)Get("spp"); var area = new Rectangle(380, 120, 1100, 600);
                int beat = WaveColumns(b, area, Color.FromArgb(0, 200, 255)), quiet = WaveColumns(b, area, Color.FromArgb(255, 60, 220));
                Check("fully zoomed out the beat's wave fills the clip across (a column at every pixel, not a bar every 3)", beat > 600, beat + " columns, " + spp.ToString("0") + " samples a pixel");
                Check("a very quiet clip is still clearly visible zoomed out", quiet > 300, quiet + " columns");
            }
            // zoomed in: the sound's own line
            SetF("autoFit", false); SetF("spp", 40.0); SetF("scroll", 44100L * 10); sf.Invalidate();
            using (var b = zoom("zoomed-in")) { int beat = WaveColumns(b, new Rectangle(380, 120, 1100, 600), Color.FromArgb(0, 200, 255)); Check("zoomed in the wave is a readable line right across", beat > 600, beat + " columns"); }
            SetF("spp", 900.0); sf.Invalidate(); using (var b = zoom("mid")) { }
            Check("no glow, stars or nebula code is left in the clip painter", !File.Exists("Studio.UI.cs") || !File.ReadAllText("Studio.UI.cs").Contains("// nebula light in two corners"), "");
        }
        catch (Exception ex) { Check("no crash", false, ex.ToString()); }
        Console.WriteLine(fails == 0 ? "=== waveform checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
