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
            ProjectLibrary.ProfileName = "Aheath 🎵🔥"; ProfileStore.Bio = "Making beats ❤️ 🎧 and 😀 vibes"; ProfileStore.Location = "London 🇬🇧"; ProfileStore.Tags = "trap drill rap " + string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(1, 70), i => "genre" + i)); ProfileStore.Accent = 0x914232; ProfileStore.NameRgb = 0xFFB07A; ProfileStore.NameRgb2 = 0x6A9BFF;
            for (int i = 1; i <= 10; i++) ProfileStore.Links.Add(new[] { "https://open.spotify.com/artist/1", "https://soundcloud.com/me", "https://www.youtube.com/@me", "https://www.instagram.com/me", "https://www.tiktok.com/@me", "https://music.apple.com/gb/artist/1", "https://bandcamp.com/me", "https://x.com/me", "https://www.facebook.com/me", "https://twitch.tv/me" }[i - 1]);
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
