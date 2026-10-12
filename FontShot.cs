using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Pictures of the Fonts page and of the studio in some wild fonts.   csc /main:FS ... FontShot.cs     run: FontShot.exe <folder for pictures> [folder with the font files]
static class FS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void Shot(string path, int w, int h) { var bmp = new Bitmap(w, h); var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, w, h)); for (int i = 0; i < 3; i++) pm.Invoke(sf, new object[] { pe }); } bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose(); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir); string src = args.Length > 1 ? args[1] : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\purphex-web\public\fonts"));
        string work = Path.Combine(Path.GetTempPath(), "fs_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work; string realSet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "settings.txt"); byte[] realBytes = File.Exists(realSet) ? File.ReadAllBytes(realSet) : null;   // choosing fonts saves the choice in your real settings: put them back exactly as they were
        try
        {
            Directory.CreateDirectory(FontKit.Dir); foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(FontKit.Dir, Path.GetFileName(f)), true);
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true; eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(400); FontKit.Build();
            SetF("tab", 8); Pump(100); Shot(Path.Combine(outDir, "page-default.png"), 1500, 900);
            foreach (var id in new[] { "nosifer", "pirataone", "orbitron", "pacifico", "bangers", "silkscreen", "rubikwetpaint", "permanentmarker" })
            {
                var e = FontKit.Find(id); if (e == null) { Console.WriteLine("missing " + id); continue; } if (!FontKit.Apply(e)) { Console.WriteLine("could not use " + id); continue; }
                Console.WriteLine(id + " scale " + FontKit.Scale.ToString("0.00")); SetF("tab", 0); SetF("selTrack", 0); Pump(60); Shot(Path.Combine(outDir, "mix-" + id + ".png"), 1500, 900);
                if (id == "nosifer" || id == "rubikwetpaint") { SetF("tab", 8); Pump(60); Shot(Path.Combine(outDir, "page-" + id + ".png"), 1500, 900); SetF("tab", 1); Pump(60); Shot(Path.Combine(outDir, "mastering-" + id + ".png"), 1500, 900); }
            }
            Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } try { if (realBytes != null) File.WriteAllBytes(realSet, realBytes); } catch { } }
        Environment.Exit(0);
    }
}
