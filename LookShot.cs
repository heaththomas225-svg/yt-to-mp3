using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The profile page with a wide banner and a tall picture, to look at the zoom / move controls.   csc /main:LK ... LookShot.cs     (uses its own empty data folder)
static class LK
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static string MakePic(string path, int w, int h, string label)
    {
        using (var b = new Bitmap(w, h)) using (var g = Graphics.FromImage(b))
        {
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color.OrangeRed, Color.DeepSkyBlue, 45f)) g.FillRectangle(br, 0, 0, w, h);
            using (var pen = new Pen(Color.White, Math.Max(4, w / 200))) { g.DrawRectangle(pen, pen.Width / 2, pen.Width / 2, w - pen.Width, h - pen.Width); g.DrawLine(pen, 0, 0, w, h); g.DrawLine(pen, w, 0, 0, h); }
            using (var f = new Font("Arial", Math.Max(20, Math.Min(w, h) / 6f), FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString(label, f, Brushes.White, new PointF(w / 10f, h / 2.6f));
            b.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return path;
    }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "look"); Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "look_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true);
            sf = new StudioForm(app); sf.ClientSize = new Size(1500, 1000); sf.Show(); Pump(400);
            string wide = MakePic(Path.Combine(work, "wide.png"), 3000, 400, "WIDE BANNER 3000x400"), tall = MakePic(Path.Combine(work, "tall.png"), 600, 1000, "TALL 600x1000");
            ProjectLibrary.ProfileName = "Look Test"; ProfileStore.Bio = "Checking the picture controls."; SetF("tab", 6); Pump(200);
            typeof(StudioForm).GetMethod("SetBanner", BF).Invoke(sf, new object[] { wide }); string err; Console.WriteLine("avatar ok=" + ProjectLibrary.SetAvatar(tall, out err) + " " + err);
            Console.WriteLine("banner zoom/x/y " + ProfileStore.BannerZoom + "/" + ProfileStore.BannerX + "/" + ProfileStore.BannerY + "  avatar " + ProfileStore.AvZoom + "/" + ProfileStore.AvX + "/" + ProfileStore.AvY);
            Action<string, float> shot = (name, scroll) =>
            {
                SetF("profScroll", scroll); var bmp = new Bitmap(1500, 1000); var pm = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1500, 1000)); for (int i = 0; i < 3; i++) pm.Invoke(sf, new object[] { pe }); }
                bmp.Save(Path.Combine(outDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            };
            shot("a-fill-top", 0); shot("a-fill-controls", 380);
            ProfileStore.BannerZoom = ProfileStore.ContainZoom(3000, 400, 1000, 200); ProfileStore.AvZoom = ProfileStore.ContainZoom(600, 1000, 120, 120); typeof(StudioForm).GetMethod("ForgetFramed", BF).Invoke(sf, null);
            Console.WriteLine("contain zoom banner " + ProfileStore.BannerZoom + " avatar " + ProfileStore.AvZoom);
            shot("b-fit-top", 0);
            ProfileStore.BannerZoom = 250; ProfileStore.BannerX = 20; ProfileStore.AvZoom = 220; ProfileStore.AvY = 10; typeof(StudioForm).GetMethod("ForgetFramed", BF).Invoke(sf, null); shot("c-zoomed-top", 0);
            Console.WriteLine("saved to " + outDir);
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
