using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Paints the Mastering light show while a made-up song plays, so the bangs can be looked at.   csc /main:VS ... VizShot.cs     run: VS.exe <output folder>
static class VS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string path)
    {
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        bmp.Save(path, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + path);
    }
    static void Tap(StudioEngine eng, double kick, double low, double mid, double hi)
    {
        var tp = eng.MasterTap; var tpp = eng.MasterTapPre; for (int i = 0; i < tp.Length; i++) { double s = kick * Math.Sin(i * 2 * Math.PI * 70 / 44100) + low * Math.Sin(i * 2 * Math.PI * 130 / 44100) + mid * Math.Sin(i * 2 * Math.PI * 350 / 44100) + hi * 0.25 * (Math.Sin(i * 2 * Math.PI * 5000 / 44100) + Math.Sin(i * 2 * Math.PI * 6300 / 44100) + Math.Sin(i * 2 * Math.PI * 7700 / 44100) + Math.Sin(i * 2 * Math.PI * 9100 / 44100)); tp[i] = (float)s; tpp[i] = tp[i] * 0.5f; }
        eng.MasterTapPos = 4096;
    }
    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_viz_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        var xs = new float[44100 * 60]; eng.Tracks.Add(StudioEngine.NewTrack("viz", "audio", AudioClip.FromArrays(xs, (float[])xs.Clone(), "viz")));
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(300); SetF("tab", args.Length > 1 ? int.Parse(args[1]) : 1); SetF("visStageOn", true);
        int ovv = Prefs.OutVol; Prefs.OutVol = 0; eng.Playing = true;
        string[] names = { "quiet", "kick", "left", "right", "top", "all" };
        double[][] hits = { new[] { 0.0, 0, 0, 0 }, new[] { 0.9, 0, 0, 0 }, new[] { 0.1, 0.9, 0, 0 }, new[] { 0.1, 0, 0.9, 0 }, new[] { 0.1, 0, 0, 0.9 }, new[] { 0.8, 0.7, 0.7, 0.7 } };
        for (int n = 0; n < names.Length; n++)
        {
            var h = hits[n];
            for (int f = 0; f < 14; f++) { double k = f < 2 ? 1.0 : 0.05; Tap(eng, h[0] * k + 0.02, h[1] * k, h[2] * k, h[3] * k); sf.Invalidate(); sf.Update(); Application.DoEvents(); Pump(30); if (f == 3) Console.WriteLine(names[n] + " jetE " + string.Join(",", System.Linq.Enumerable.Select((float[])typeof(StudioForm).GetField("jetE", BF).GetValue(sf), x => x.ToString("0.00")))); if (f == 3) Shot(Path.Combine(outDir, "viz_" + names[n] + ".png")); }
        }
        Prefs.OutVol = ovv; eng.Playing = false; sf.Close();
    }
}
