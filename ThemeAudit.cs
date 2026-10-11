using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Finds colours that did not follow the theme: it draws every page in themes that have no purple at all and marks every purple-ish pixel.   csc /main:ThemeAuditT ... ThemeAudit.cs    run: ThemeAuditT.exe <output folder>
static class ThemeAuditT
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static string outDir;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void M(string n, params object[] a) { typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static bool Purple(Color c)
    {
        float h = c.GetHue(), s = c.GetSaturation(), v = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f; if (s < 0.28f || v < 0.22f) return false; return h >= 262 && h <= 335;
    }
    static int total = 0;
    static void WinShot(string theme, string name, Form f)
    {
        Pump(200); var bmp = new Bitmap(Math.Max(1, f.Width), Math.Max(1, f.Height)); f.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); int bad = 0;
        for (int y = 0; y < bmp.Height; y += 2) for (int x = 0; x < bmp.Width; x += 2) if (Purple(bmp.GetPixel(x, y))) bad++;
        total += bad; Console.WriteLine((bad > 400 ? "PURPLE " : "ok     ") + theme + " / " + name + ": " + bad * 4 + " px"); if (bad > 400) bmp.Save(Path.Combine(outDir, theme + "-" + name.Replace(' ', '_') + ".png"), ImageFormat.Png); bmp.Dispose();
    }
    static void Shot(string theme, string name)
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents();
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height, PixelFormat.Format32bppPArgb); using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        int w = bmp.Width, h = bmp.Height, bad = 0; var cnt = new int[(w / 40 + 1) * (h / 40 + 1)];
        for (int y = 0; y < h; y += 2) for (int x = 0; x < w; x += 2) { if (Purple(bmp.GetPixel(x, y))) { bad++; cnt[(y / 40) * (w / 40 + 1) + x / 40]++; } }
        total += bad; string worst = ""; int bi = 0; for (int i = 0; i < cnt.Length; i++) if (cnt[i] > cnt[bi]) bi = i; if (cnt[bi] > 0) worst = "worst block at (" + (bi % (w / 40 + 1)) * 40 + "," + (bi / (w / 40 + 1)) * 40 + ") " + cnt[bi] * 4 + " px";
        Console.WriteLine((bad > 400 ? "PURPLE " : "ok     ") + theme + " / " + name + ": " + bad * 4 + " px   " + worst);
        if (bad > 400) { using (var g = Graphics.FromImage(bmp)) using (var br = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) { for (int y = 0; y < h; y += 2) for (int x = 0; x < w; x += 2) if (Purple(bmp.GetPixel(x, y))) { } } bmp.Save(Path.Combine(outDir, theme + "-" + name.Replace(' ', '_') + ".png"), ImageFormat.Png); }
        bmp.Dispose();
    }
    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "themeaudit"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_themeaudit_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60))); foreach (var tk in eng.Tracks) tk.Color = Color.FromArgb(255, 200, 150, 40).ToArgb();
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 1000); sf.Show(); Pump(400); int ov = Prefs.OutVol; Prefs.OutVol = 0;
        var tp = eng.MasterTap; var tpp = eng.MasterTapPre; var rnd = new Random(5); for (int i = 0; i < tp.Length; i++) { float s0 = (float)(0.3 * Math.Sin(i * 0.02) + 0.25 * Math.Sin(i * 0.11) + 0.1 * (rnd.NextDouble() - 0.5)); tp[i] = s0; tpp[i] = s0 * 0.7f; }
        string[] themes = args.Length > 1 ? args.Skip(1).ToArray() : new[] { "Green", "Orange", "Yellow" };
        foreach (var th in themes)
        {
            Themes.Apply(Themes.IndexOf(th)); Pump(100); SetF("tab", 0); Console.WriteLine("=== " + th);
            Shot(th, "mix"); var fx = FxRegistry.Create("eq"); fx.Prepare(44100); eng.Tracks[0].Fx.Add(fx); SetF("selTrack", 0); SetF("selFx", 0); Shot(th, "mix eq panel"); eng.Tracks[0].Fx.Clear(); SetF("selFx", -1);
            SetF("tab", 1); SetF("mpTab", 0); Shot(th, "mastering"); SetF("mpTab", 1); Shot(th, "mastering presets"); SetF("mpTab", 0);
            eng.Playing = true; SetF("tab", 5); for (int i = 0; i < 20; i++) { sf.Invalidate(); sf.Update(); Application.DoEvents(); Pump(10); } Shot(th, "visualiser"); eng.Playing = false;
            SetF("tab", 2); Shot(th, "lyrics"); SetF("tab", 3); Shot(th, "projects"); SetF("tab", 4); Shot(th, "community"); SetF("tab", 0);
            SetF("themeOpen", true); Shot(th, "theme sheet"); SetF("themeOpen", false); M("OpenNewTrackSheet", 0); Shot(th, "new track"); SetF("ntOpen", false);
            { var st = new SettingsForm(app); st.Show(); for (int pi = 0; pi < 7; pi++) { typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(st, new object[] { pi }); WinShot(th, "settings page " + pi, st); } st.Close(); }
            app.Show(); Win.Retheme(app); WinShot(th, "main window", app); app.ShowHelp(null); Pump(300); foreach (Form fm in Application.OpenForms) if (fm is HelpForm) { WinShot(th, "help", fm); fm.Close(); break; }
            { var sfx = new SynthFx(); sfx.Prepare(44100); var sw = new SynthWindow(sfx, null); sw.Show(); Pump(300); WinShot(th, "bass lab", sw); sw.Close(); }
            M("OpenStyles"); Shot(th, "styles"); SetF("stylesOpen", false); M("OpenMenu"); SetF("menuSec", 3); Shot(th, "menu"); M("CloseMenu");
        }
        Prefs.OutVol = ov; Themes.Apply(0); Console.WriteLine("=== theme audit done: " + total * 1 + " purple px in total"); Environment.Exit(0);
    }
}
