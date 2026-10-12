using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Pictures of the preset screens (master presets page, plugin presets sheet, community presets sheet).   csc /main:PS ... PresetShot.cs     run: PresetShot.exe <folder for pictures> [theme index]
static class PS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void Shot(string path, int w, int h) { var bmp = new Bitmap(w, h); var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, w, h)); for (int i = 0; i < 3; i++) pm.Invoke(sf, new object[] { pe }); } bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose(); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir); int theme = args.Length > 1 ? int.Parse(args[1]) : 0;
        string work = Path.Combine(Path.GetTempPath(), "ps_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work; string realSet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "settings.txt"); byte[] realBytes = File.Exists(realSet) ? File.ReadAllBytes(realSet) : null;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true; eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(300); if (theme > 0) { Themes.Apply(theme); Pump(500); }
            SetF("tab", 1); SetF("mpTab", 1); Pump(100); Shot(Path.Combine(outDir, "master-presets.png"), 1500, 900);
            SetF("mpTab", 0); SetF("tab", 0); SetF("selTrack", 0); Pump(100); sf.OpenPresetSheet(eng.MasterFx[0]); Pump(100); Shot(Path.Combine(outDir, "fx-presets.png"), 1500, 900);
            SetF("psOpen", false); Pump(50); sf.OpenCommunityPresets("fx:" + eng.MasterFx[0].Type, eng.MasterFx[0].Title, cp => { }, () => eng.MasterFx[0].Serialize(), "x"); Pump(200); Shot(Path.Combine(outDir, "community-presets.png"), 1500, 900);
            Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } try { if (realBytes != null) File.WriteAllBytes(realSet, realBytes); } catch { } }
        Environment.Exit(0);
    }
}
