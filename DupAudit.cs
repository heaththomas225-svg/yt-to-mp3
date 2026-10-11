using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Lists, for every studio page, the short labels (buttons, menu items) that are drawn more than once on the same screen.   csc /main:DA ... DupAudit.cs
static class DA
{
    static string OutDir = Path.Combine(Path.GetTempPath(), "dupshots"); static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }

    static void Dump(string name)
    {
        Bitmap bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); var args = new object[] { null };
        for (int pass = 0; pass < 2; pass++)
        {
            StudioForm.TxtAudit = new List<StudioForm.AuditText>();
            using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); }
        }
        var list = StudioForm.TxtAudit; StudioForm.TxtAudit = null; try { Directory.CreateDirectory(OutDir); bmp.Save(Path.Combine(OutDir, sf.ClientSize.Width + "-" + name.Replace(" ", "_") + ".png"), System.Drawing.Imaging.ImageFormat.Png); } catch { } bmp.Dispose();
        var groups = list.Where(t => t.S != null && t.S.Trim().Length > 1 && t.S.Trim().Length <= 26).GroupBy(t => t.S.Trim().ToUpperInvariant()).Where(gp => gp.Count() > 1).ToList();
        Console.WriteLine("== " + name + "  (" + list.Count + " strings)");
        foreach (var gp in groups) Console.WriteLine("   x" + gp.Count() + "  \"" + gp.Key + "\"  at " + string.Join("  ", gp.Select(t => (int)t.Box.X + "," + (int)t.Box.Y)));
    }

    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy;
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "dup-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1";
            foreach (var sz in new[] { new Size(1366, 768), new Size(1920, 1080) })
            {
                sf.ClientSize = sz; sf.Show(); Pump(350); Console.WriteLine("---- " + sz.Width + "x" + sz.Height);
                string[] names = { "Mix", "Mastering", "Lyrics", "My Projects", "Community", "Visualiser", "Profile", "Artists" };
                for (int t = 0; t < 8; t++) { SetF("tab", t); Pump(150); Dump(names[t]); }
                SetF("tab", 0); M("OpenMenu"); Dump("main menu"); M("CloseMenu");
            }
        }
        finally { foreach (var f in Directory.GetFiles(bak)) { try { File.Copy(f, Path.Combine(data, Path.GetFileName(f)), true); } catch { } } }
        Environment.Exit(0);
    }
}
