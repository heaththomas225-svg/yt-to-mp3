using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The Mix page with the first track turned off, at four moments.   csc /main:OFS ... OffShot.cs     run: OffShot.exe <folder>
static class OFS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "ofs_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); sf.ClientSize = new Size(1366, 768); sf.Show(); Pump(400); SetF("tab", 0); Pump(100);
            eng.Tracks[0].Off = true;
            for (int k = 0; k < 4; k++)
            {
                Pump(130 + k * 37); var bmp = new Bitmap(1366, 768); var pm = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); pm.Invoke(sf, new object[] { pe }); }
                bmp.Save(Path.Combine(outDir, "off-" + k + ".png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            }
            Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
