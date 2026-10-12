using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Pictures of the ordinary windows (the converter, Settings pages, Help).   csc /main:WS ... WinShot.cs     run: WinShot.exe <folder for pictures>
static class WS
{
    static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static void Snap(Form f, string path) { Pump(250); using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); } }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "ws_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work; string realSet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "settings.txt"); byte[] realBytes = File.Exists(realSet) ? File.ReadAllBytes(realSet) : null;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true);
            app.StartPosition = FormStartPosition.Manual; app.Location = new Point(20, 20); app.Show(); Snap(app, Path.Combine(outDir, "main.png")); app.Hide();
            var st = new SettingsForm(app); st.StartPosition = FormStartPosition.Manual; st.Location = new Point(40, 40); st.Show();
            var showPg = typeof(SettingsForm).GetMethod("ShowPage", BF); st.ClientSize = new Size(args.Length > 1 ? int.Parse(args[1]) : st.ClientSize.Width, args.Length > 2 ? int.Parse(args[2]) : st.ClientSize.Height); Pump(200); for (int p = 0; p < 8; p++) { try { showPg.Invoke(st, new object[] { p }); } catch { break; } Snap(st, Path.Combine(outDir, "settings-" + p + ".png")); }
            var hf = new HelpForm(id => true, id => { }, "editions"); hf.StartPosition = FormStartPosition.Manual; hf.Location = new Point(30, 30); hf.Show(); Snap(hf, Path.Combine(outDir, "help.png")); hf.Close();
            st.Close(); Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } try { if (realBytes != null) File.WriteAllBytes(realSet, realBytes); } catch { } }
        Environment.Exit(0);
    }
}
