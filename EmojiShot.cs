using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Draws the Profile page with emoji in the texts so they can be looked at.   csc /main:EM ... EmojiShot.cs
static class EM
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "emoji"); Directory.CreateDirectory(outDir);
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "em-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt", "profile-page.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true);
            sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(400);
            ProjectLibrary.ProfileName = "Aheath 🎵🔥"; ProfileStore.Bio = "Making beats ❤️ 🎧 and 😀 vibes"; ProfileStore.Location = "London 🇬🇧"; ProfileStore.Tags = "trap drill rap";
            for (int i = 1; i <= 10; i++) ProfileStore.Links.Add("https://example" + i + ".com/me");
            SetF("tab", 6); Pump(200);
            foreach (var scroll in new[] { 0f, 520f })
            {
                SetF("profScroll", scroll); var bmp = new Bitmap(1500, 900); var pm = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1500, 900)); for (int i = 0; i < 3; i++) pm.Invoke(sf, new object[] { pe }); }
                bmp.Save(Path.Combine(outDir, "profile-" + (int)scroll + ".png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            }
            Console.WriteLine("saved to " + outDir);
        }
        finally { foreach (var f in Directory.GetFiles(bak)) { try { File.Copy(f, Path.Combine(data, Path.GetFileName(f)), true); } catch { } } }
        Environment.Exit(0);
    }
}
