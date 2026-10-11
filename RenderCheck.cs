using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Paints the screens into picture files so they can be looked at (text running out of boxes, overlaps...).   csc /main:RenderT ... RenderCheck.cs     run: RenderT.exe <output folder>
static class RenderT
{
    static StudioForm sf; static string outDir; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string name)
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents();
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        string p = Path.Combine(outDir, name + ".png"); bmp.Save(p, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + p);
    }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "render-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60)));
            sf = new StudioForm(app); int[][] sizes = { new[] { 1500, 800 }, new[] { 1000, 700 }, new[] { 640, 480 } };
            foreach (var sz in sizes)
            {
                sf.ClientSize = new Size(sz[0], sz[1]); sf.Show(); Pump(400); string tag = sz[0] + "x" + sz[1];
                var tr = eng.Tracks[0]; tr.Fx.Clear(); foreach (var id in new[] { "lib_worldwide_echo", "lib_melody_tune", "lib_rap_ad_lib_echo" }) { var f = FxRegistry.Create(id); f.Prepare(44100); tr.Fx.Add(f); }
                SetF("selTrack", 0); SetF("selFx", 0); Shot(tag + "-panel-worldwide-echo");
                SetF("selFx", 1); Shot(tag + "-panel-melody-tune");
                // the busy box
                SetF("busy", true); SetF("busyText", "Separating vocals and beat with AI...\n(under a minute with a good graphics card)"); SetF("busyPct", 0.3f); SetF("busyCancelable", true); SetF("busyStart", DateTime.Now.AddSeconds(-20)); Shot(tag + "-busy-separating");
                SetF("busyText", "Setting up the separation engine (one time)..."); Shot(tag + "-busy-setup"); SetF("busy", false); SetF("busyPct", -1f);
                // the vocal styles sheet
                typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); Shot(tag + "-styles"); SetF("stylesScroll", 640f); Shot(tag + "-styles-scroll1"); SetF("stylesScroll", 1500f); Shot(tag + "-styles-scroll2"); SetF("stylesScroll", 0f); SetF("stylesGenre", "Cloud & Lo-Fi"); Shot(tag + "-styles-cloud"); SetF("stylesGenre", ""); SetF("stylesQuery", "zzz"); Shot(tag + "-styles-none"); SetF("stylesQuery", ""); SetF("stylesFav", true); Shot(tag + "-styles-favourites-empty"); SetF("stylesOpen", false);
                // the automation sheet
                var wfx = tr.Fx[0]; typeof(StudioForm).GetMethod("OpenAuto", BF).Invoke(sf, new object[] { tr, wfx, wfx.Find("fb") }); Shot(tag + "-automation");
                var cfx = tr.Fx[1]; typeof(StudioForm).GetMethod("OpenAuto", BF).Invoke(sf, new object[] { tr, cfx, cfx.Find("harm") }); Shot(tag + "-automation-choice"); SetF("autoOpen", false);
                tr.Auto.Clear(); tr.Fx.Clear();
            }
            sf.Close();
        }
        finally { foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(bak, f))) File.Copy(Path.Combine(bak, f), Path.Combine(data, f), true); } catch { } } }
        Environment.Exit(0);
    }
}
