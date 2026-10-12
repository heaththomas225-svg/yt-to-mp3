using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// How long one paint of each page takes (milliseconds, average of 40).   csc /main:PQ ... PerfQ.cs
static class PQ
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy;
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "pq-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1920, 1080); sf.Show(); Pump(400);
            string[] names = { "Mix", "Mastering", "Lyrics", "My Projects", "Community", "Visualiser", "Profile" };
            foreach (int t in new[] { 0, 1, 5, 4, 6 })
            {
                SetF("tab", t); Pump(150); var bmp = new Bitmap(1920, 1080); var pm = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var g = Graphics.FromImage(bmp))
                {
                    var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1920, 1080)); for (int i = 0; i < 5; i++) pm.Invoke(sf, new object[] { pe });
                    var sw = Stopwatch.StartNew(); for (int i = 0; i < 40; i++) pm.Invoke(sf, new object[] { pe }); sw.Stop(); Console.WriteLine(names[t].PadRight(12) + (sw.Elapsed.TotalMilliseconds / 40).ToString("0.00") + " ms");
                }
                bmp.Dispose();
            }
        }
        finally { foreach (var f in Directory.GetFiles(bak)) { try { File.Copy(f, Path.Combine(data, Path.GetFileName(f)), true); } catch { } } }
        Environment.Exit(0);
    }
}
