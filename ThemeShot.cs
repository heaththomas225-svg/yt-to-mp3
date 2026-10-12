using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Makes a theme from a colourful picture and paints the studio with it, to look at.   csc /main:TS ... ThemeShot.cs     run: TS.exe <output folder>
static class TS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string path) { sf.Invalidate(); sf.Update(); Application.DoEvents(); var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); bmp.Save(path, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + path); }
    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_themeshot_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        string pic = Path.Combine(StudioEnv.DataDir, "sunset city.png");
        using (var b = new Bitmap(1600, 900)) { using (var g = Graphics.FromImage(b)) { using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1600, 900), Color.FromArgb(255, 255, 120, 40), Color.FromArgb(255, 20, 90, 150), 90f)) g.FillRectangle(br, 0, 0, 1600, 900); using (var br = new SolidBrush(Color.FromArgb(255, 230, 40, 130))) g.FillEllipse(br, 1000, 160, 340, 340); using (var br = new SolidBrush(Color.FromArgb(255, 15, 25, 50))) g.FillRectangle(br, 0, 620, 1600, 280); } b.Save(pic, ImageFormat.Png); }
        var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; Edition.Override = Tier.Free;
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(300);
        string err = (string)typeof(StudioForm).GetMethod("SetThemePhotoPath", BF | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sf, new object[] { pic }); Console.WriteLine("err: " + err + " theme: " + Themes.Current.Name); Pump(600);
        SetF("tab", 1); Pump(300); Shot(Path.Combine(outDir, "photo_theme_mastering.png"));
        SetF("tab", 0); SetF("themeOpen", true); Pump(300); Shot(Path.Combine(outDir, "photo_theme_sheet.png"));
        sf.Close();
    }
}
