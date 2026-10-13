using System; using System.Collections.Generic; using System.Diagnostics; using System.Drawing; using System.IO; using System.Linq; using System.Reflection; using System.Windows.Forms;
// Where the time of one Mix / Mastering paint goes, with PERFORMANCE MODE off and on.   csc /main:PPf ... PerfProf.cs
static class PPf
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    [STAThread] static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string work = Path.Combine(Path.GetTempPath(), "pp_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            for (int i = 0; i < 6; i++) eng.Tracks.Add(StudioEngine.NewTrack("Track " + i, i == 0 ? "vocal" : "beat", Clip(30)));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.ClientSize = new Size(1920, 1080); sf.Show(); Pump(400);
            foreach (bool perf in new[] { false, true }) foreach (int t in new[] { 0, 1 })
            {
                Prefs.PerfMode = perf; typeof(StudioForm).GetField("tab", BF).SetValue(sf, t); Pump(200); var pm = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var bmp = new Bitmap(1920, 1080)) using (var g = Graphics.FromImage(bmp))
                {
                    var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1920, 1080)); for (int i = 0; i < 6; i++) pm.Invoke(sf, new object[] { pe });
                    Prof.On = true; Prof.Acc.Clear(); Prof.Cnt.Clear(); Prof.Totals.Clear(); var sw = Stopwatch.StartNew(); for (int i = 0; i < 30; i++) pm.Invoke(sf, new object[] { pe }); sw.Stop(); Prof.On = false;
                    Console.WriteLine((perf ? "PERF ON  " : "perf off ") + (t == 0 ? "Mix" : "Mastering") + "  " + (sw.Elapsed.TotalMilliseconds / 30).ToString("0.00") + " ms");
                    foreach (var kv in Prof.Totals.OrderByDescending(k => k.Value).Take(5)) Console.WriteLine("      " + kv.Key.PadRight(18) + (kv.Value / 30).ToString("0.00") + " ms");
                    foreach (var kv in Prof.Acc.OrderByDescending(k => k.Value).Take(4)) Console.WriteLine("      [acc] " + kv.Key.PadRight(18) + (kv.Value / 30).ToString("0.00") + " ms");
                }
            }
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
