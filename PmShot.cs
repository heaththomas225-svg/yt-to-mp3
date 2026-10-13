using System; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
// The Plugins and extensions window: does it open?   csc /main:PmS ... PmShot.cs   run: PmShot.exe <folder>
static class PmS
{
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    [STAThread] static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir); string work = Path.Combine(Path.GetTempPath(), "pm_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var sf = new StudioForm(app); sf.ClientSize = new Size(1366, 768); sf.Show(); Pump(400);
            try { var f = new PluginManagerForm(() => "", s => { }); f.Show(sf); Pump(800); Console.WriteLine("opened visible=" + f.Visible + " bounds=" + f.Bounds + " studio=" + sf.Bounds + " topmost studio=" + sf.TopMost); using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(Path.Combine(outDir, "pm.png"), System.Drawing.Imaging.ImageFormat.Png); } }
            catch (Exception ex) { Console.WriteLine("FAILED " + ex); }
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
