using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The Mastering tab with the BEFORE / AFTER loudness lines and the REVERT bar.   csc /main:MaS ... MasShot.cs     run: MasShot.exe <folder>
static class MaS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "mas_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(10)));
            sf = new StudioForm(app); int[][] sizes = { new[] { 1366, 768 }, new[] { 1240, 680 }, new[] { 1700, 1000 } };
            foreach (var sz in sizes)
            {
                sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(40, 30); sf.ClientSize = new Size(sz[0], sz[1]); sf.Show(); Pump(400);
                SetF("tab", 1); SetF("mpTab", 0); SetF("selMaster", 0);
                SetF("abOrig", new LoudnessResult { I = -16.9, LRA = 6 }); SetF("abMast", new LoudnessResult { I = -13.2, LRA = 5 });
                typeof(StudioForm).GetMethod("OfferRevert", BF).Invoke(sf, new object[] { "Vocal Forward is on your master", (Action)(() => { }) });
                var bmp = new Bitmap(sz[0], sz[1]); var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, sz[0], sz[1])); for (int i = 0; i < 2; i++) pm.Invoke(sf, new object[] { pe }); }
                bmp.Save(Path.Combine(outDir, "mas-" + sz[0] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                using (var crop = bmp.Clone(new Rectangle(0, Math.Max(0, sz[1] - 330), 420, 330), bmp.PixelFormat)) crop.Save(Path.Combine(outDir, "mas-corner-" + sz[0] + ".png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            }
            { // the picture sheet: a wide picture with black bars is cut and fitted by itself
                string pp = Path.Combine(work, "demo.ytmp"); File.WriteAllText(pp, "PURPHEX\nname|Demo\n"); ProjectLibrary.Touch(pp, "Demo", 3, 120, 100, true, null);
                string img = Path.Combine(work, "wide.png"); using (var b0 = new Bitmap(1200, 600)) { using (var g0 = Graphics.FromImage(b0)) { g0.Clear(Color.Black); using (var br = new SolidBrush(Color.FromArgb(230, 255, 40, 60))) g0.FillEllipse(br, 480, 220, 240, 160); } b0.Save(img, System.Drawing.Imaging.ImageFormat.Png); }
                string err; bool ok = ProjectLibrary.SetCover(pp, img, out err); int z, x, y; ProjectLibrary.LoadAdj(pp, out z, out x, out y); Console.WriteLine("setcover " + ok + " " + err + " adj " + z + "/" + x + "/" + y);
                using (var im = Image.FromFile(ProjectLibrary.CoverSrc(pp))) Console.WriteLine("kept picture " + im.Width + "x" + im.Height);
                sf.ClientSize = new Size(1366, 768); Pump(300); SetF("tab", 0); typeof(StudioForm).GetMethod("OpenCoverAdjust", BF).Invoke(sf, new object[] { pp });
                var bmp2 = new Bitmap(1366, 768); var pm2 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp2)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm2.Invoke(sf, new object[] { pe }); }
                bmp2.Save(Path.Combine(outDir, "cover-sheet.png"), System.Drawing.Imaging.ImageFormat.Png); bmp2.Dispose();
            }
            Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
