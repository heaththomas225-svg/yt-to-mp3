using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Pictures of the preset windows (vocal presets, bus presets, the mastering presets) in two themes, to check that they keep the Purphex look.
//   csc /main:PresetShots ... PresetShots.cs     run: PresetShots.exe <output folder>
static class PresetShots
{
    static StudioForm sf; static string outDir; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Call(string n, params object[] a) { typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string name)
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents();
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        string p = Path.Combine(outDir, name + ".png"); bmp.Save(p, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + p);
    }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "clip"); }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "presetshots"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_pshot_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 860); sf.Show(); Pump(500); SetF("selTrack", 0);
        string only = args.Length > 1 ? args[1] : "";
        for (int pass = 0; pass < 2; pass++)
        {
            string tag = pass == 0 ? "default" : "theme";
            if (pass == 1) { int pick = -1; for (int i = 0; i < Themes.All.Count; i++) if (i != Themes.Index && Themes.All[i].Name.IndexOf("Purphex", StringComparison.OrdinalIgnoreCase) < 0) { pick = i; break; } if (pick >= 0) { Themes.Apply(pick); Console.WriteLine("theme " + Themes.All[pick].Name); } Pump(200); }
            Call("OpenStyles"); Pump(100); Shot(tag + "-vocal-presets"); SetF("stylesOpen", false);
            Call("OpenBusPresets"); Pump(100); Shot(tag + "-bus-presets"); SetF("stylesOpen", false);
            SetF("tab", 1); SetF("mpTab", 1); Pump(100); Shot(tag + "-mastering"); SetF("mpTab", 0); SetF("tab", 0);
            { var comp = FxRegistry.Create("comp"); comp.Prepare(44100); eng.Tracks[0].Fx.Add(comp); Call("OpenPresetSheet", comp); Pump(100); Shot(tag + "-plugin-presets"); SetF("psOpen", false); eng.Tracks[0].Fx.Remove(comp); }
        }
        // the Free Edition studio
        Edition.Override = Tier.Free; Themes.Apply(0); Pump(200);
        SetF("tab", 1); SetF("mpTab", 0); Pump(100); Shot("free-mastering"); SetF("mpTab", 1); Pump(100); Shot("free-mastering-presets"); SetF("mpTab", 0); SetF("tab", 0);
        SetF("menuOpen", true); Pump(100); Shot("free-menu"); SetF("menuOpen", false);
        StudioDialogs.ExportOptions(sf, "My song", 60, new[] { "Lead vocal", "Beat" }, r => { }, fm => { Pump(300); var b = new Bitmap(fm.Width, fm.Height); fm.DrawToBitmap(b, new Rectangle(0, 0, fm.Width, fm.Height)); b.Save(Path.Combine(outDir, "free-export.png"), ImageFormat.Png); Console.WriteLine("saved free-export"); fm.Close(); });
        Pump(500);
        Environment.Exit(0);
    }
}
