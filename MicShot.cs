using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The Mix page transport bar with the MIC mode box, and the menu it opens.   csc /main:MS ... MicShot.cs     run: MicShot.exe <folder>
static class MS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "ms_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true; eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(10)));
            sf = new StudioForm(app); sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(60, 40); sf.ClientSize = new Size(1366, 700); sf.Show(); sf.TopMost = true; sf.Activate(); Pump(500);
            var bmp = new Bitmap(1366, 700); var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 700)); for (int i = 0; i < 2; i++) pm.Invoke(sf, new object[] { pe }); }
            using (var crop = bmp.Clone(new Rectangle(0, 60, 900, 70), bmp.PixelFormat)) crop.Save(Path.Combine(outDir, "mic-bar.png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            // the menu: find the little box (24 wide, in the transport row) and open the menu under it
            var regs = (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf); RectangleF box = RectangleF.Empty; foreach (object o in regs) { var r = (HitReg)o; if (Math.Abs(r.R.Width - 24) < 0.5f && r.R.Y > 70 && r.R.Y < 110 && r.R.X > 380 && r.R.X < 900) box = r.R; }
            Console.WriteLine("box " + box); typeof(StudioForm).GetMethod("MicMenu", BF).Invoke(sf, new object[] { box }); Pump(500);
            var pt = sf.PointToScreen(new Point((int)box.X - 10, (int)box.Bottom - 20)); using (var shot = new Bitmap(360, 240)) { using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(pt, Point.Empty, new Size(360, 240)); shot.Save(Path.Combine(outDir, "mic-menu.png"), System.Drawing.Imaging.ImageFormat.Png); }
            SendKeys.SendWait("{ESC}"); Pump(200); Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
