using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
static class GuideDbgT
{
    static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    [STAThread] static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_gd_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); app.StudioEng.Tracks.Add(StudioEngine.NewTrack("Lead", "vocal", Clip(30)));
        var sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(400);
        Func<int> regs = () => ((IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Count;
        sf.Invalidate(); sf.Update(); Application.DoEvents(); Console.WriteLine("normal: regs " + regs() + " client " + sf.ClientSize);
        Win.ToggleFullscreen(sf); Pump(600); sf.Invalidate(); sf.Update(); Application.DoEvents(); Console.WriteLine("fullscreen: regs " + regs() + " client " + sf.ClientSize + " visible " + sf.Visible + " state " + sf.WindowState);
        sf.Invalidate(); sf.Update(); Application.DoEvents(); Console.WriteLine("again: regs " + regs());
        Environment.Exit(0);
    }
}
