using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Makes the slides of the website's guide video (screens of the real app with a caption on each).   csc /main:TourT ... TourShots.cs    run: TourT.exe <output folder>
static class TourT
{
    static StudioForm sf; static App app; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static string outDir; static int n = 0;
    static void SetF(string nm, object v) { typeof(StudioForm).GetField(nm, BF).SetValue(sf, v); }
    static object M(string nm, params object[] a) { return typeof(StudioForm).GetMethod(nm, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    static void Stealth(Form f) { try { f.ShowInTaskbar = false; f.Opacity = 0.0; int ex = GetWindowLong(f.Handle, -20); SetWindowLong(f.Handle, -20, ex | 0x80000 | 0x20 | 0x80 | 0x08000000); } catch { } }
    // music with a beat, a bass line and a tune, so the waveforms and the meters look like a real song
    static AudioClip Song(int sec, int kind)
    {
        int sr = 44100, len = sr * sec; var l = new float[len]; var r = new float[len]; var rnd = new Random(kind * 7 + 1); double bpm = 140, beat = 60.0 / bpm * sr;
        for (int i = 0; i < len; i++)
        {
            double t = i / (double)sr, bt = (i % beat) / beat, bar = (i / (beat * 4)); double v = 0;
            if (kind == 0) { v += Math.Exp(-bt * 9) * Math.Sin(2 * Math.PI * (45 + 90 * Math.Exp(-bt * 22)) * t) * 0.8; double sn = ((int)(i / beat) % 2 == 1) ? Math.Exp(-bt * 14) * (rnd.NextDouble() * 2 - 1) * 0.35 : 0; v += sn; if ((int)(i / (beat / 2)) % 2 == 1) v += Math.Exp(-((i % (beat / 2)) / (beat / 2)) * 40) * (rnd.NextDouble() * 2 - 1) * 0.15; }
            else if (kind == 1) { double f = 55 * Math.Pow(2, ((int)bar % 4 == 2 ? 3 : ((int)bar % 4 == 3 ? 5 : 0)) / 12.0); v += Math.Sin(2 * Math.PI * f * t) * 0.5 * (0.6 + 0.4 * Math.Sin(t * 5)) + Math.Sin(2 * Math.PI * f * 2 * t) * 0.15; }
            else { double[] sc = { 0, 3, 7, 10, 12, 10, 7, 3 }; double f = 220 * Math.Pow(2, sc[(int)(i / (beat / 2)) % 8] / 12.0); double env = Math.Exp(-((i % (beat / 2)) / (beat / 2)) * 3); v += (Math.Sin(2 * Math.PI * f * t) + 0.4 * Math.Sin(4 * Math.PI * f * t)) * 0.28 * env; }
            l[i] = (float)v; r[i] = (float)v * (kind == 2 ? 0.8f : 1f);
        }
        return AudioClip.FromArrays(l, r, "song");
    }
    static Bitmap Shot()
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents(); var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); return bmp;
    }
    // the slide: the screen filled into 1280 x 720 with a dark band at the bottom holding the title and a line of words
    static void Slide(Bitmap shot, string title, string line, int tickNo = -1)
    {
        n++; using (var s = new Bitmap(1280, 720, PixelFormat.Format32bppPArgb)) using (var g = Graphics.FromImage(s))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.Clear(Color.Black);
            if (shot != null) { double sc = Math.Max(1280.0 / shot.Width, 720.0 / shot.Height); float w = (float)(shot.Width * sc), h = (float)(shot.Height * sc); g.DrawImage(shot, (1280 - w) / 2f, (720 - h) / 2f, w, h); }
            using (var br = new LinearGradientBrush(new Rectangle(0, 470, 1280, 250), Color.FromArgb(0, 4, 2, 12), Color.FromArgb(235, 4, 2, 12), 90f)) g.FillRectangle(br, 0, 470, 1280, 250);
            using (var f1 = new Font("Bahnschrift", 44, FontStyle.Bold, GraphicsUnit.Pixel)) using (var f2 = new Font("Bahnschrift SemiLight", 24, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 245, 238, 255))) g.DrawString(title.ToUpper(), f1, b, 56, 560);
                using (var b = new SolidBrush(Color.FromArgb(235, 190, 170, 240))) g.DrawString(line, f2, b, new RectangleF(58, 622, 1160, 80));
                using (var pen = new Pen(Color.FromArgb(255, 255, 60, 220), 4f)) g.DrawLine(pen, 58, 552, 190, 552); using (var pen = new Pen(Color.FromArgb(255, 0, 229, 255), 4f)) g.DrawLine(pen, 190, 552, 250, 552);
            }
            s.Save(Path.Combine(outDir, "slide" + n.ToString("00") + ".png"), ImageFormat.Png); Console.WriteLine("slide " + n + ": " + title);
        }
        if (shot != null) shot.Dispose();
    }
    static void TitleCard(string big, string small)
    {
        n++; using (var s = new Bitmap(1280, 720, PixelFormat.Format32bppPArgb)) using (var g = Graphics.FromImage(s))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1280, 720), Color.FromArgb(255, 18, 6, 46), Color.FromArgb(255, 3, 1, 10), 70f)) g.FillRectangle(br, 0, 0, 1280, 720);
            var hex = new[] { new PointF(640 - 120, 250), new PointF(640 - 60, 150), new PointF(640 + 60, 150), new PointF(640 + 120, 250), new PointF(640 + 60, 350), new PointF(640 - 60, 350) };
            for (int i = 4; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(36, 255, 43, 214), i * 8f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex);
            using (var pen = new Pen(Color.FromArgb(255, 0, 229, 255), 5f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex);
            using (var f1 = new Font("Bahnschrift", 76, FontStyle.Bold, GraphicsUnit.Pixel)) using (var f2 = new Font("Bahnschrift SemiLight", 30, FontStyle.Regular, GraphicsUnit.Pixel)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center })
            { using (var b = new SolidBrush(Color.White)) g.DrawString(big, f1, b, new RectangleF(0, 400, 1280, 100), sfm); using (var b = new SolidBrush(Color.FromArgb(255, 190, 170, 240))) g.DrawString(small, f2, b, new RectangleF(0, 510, 1280, 120), sfm); }
            s.Save(Path.Combine(outDir, "slide" + n.ToString("00") + ".png"), ImageFormat.Png); Console.WriteLine("slide " + n + ": " + big);
        }
    }
    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "tour"); Directory.CreateDirectory(outDir); StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_tour_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; CommunityPresets.BaseUrl = "http://127.0.0.1:1"; Prefs.OutVol = 0;
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Song(60, 2))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Song(60, 0))); eng.Tracks.Add(StudioEngine.NewTrack("Bass", "audio", Song(60, 1))); eng.Tracks[0].Color = Color.FromArgb(255, 60, 220).ToArgb(); eng.Tracks[1].Color = Color.FromArgb(0, 200, 255).ToArgb(); eng.Tracks[2].Color = Color.FromArgb(150, 110, 255).ToArgb();
        var fx = FxRegistry.Create("eq"); fx.Prepare(44100); eng.Tracks[0].Fx.Add(fx); var fx2 = FxRegistry.Create("reverb"); fx2.Prepare(44100); eng.Tracks[0].Fx.Add(fx2); var fx3 = FxRegistry.Create("comp"); fx3.Prepare(44100); eng.Tracks[1].Fx.Add(fx3);
        sf = new StudioForm(app); sf.ClientSize = new Size(1600, 900); sf.Show(); Stealth(sf); Pump(500); eng.Seek(44100L * 12); SetF("selTrack", 0);
        var tp = eng.MasterTap; var tpp = eng.MasterTapPre; var rnd = new Random(5); for (int i = 0; i < tp.Length; i++) { double bd = Math.Exp(-(i % 5512) / 1500.0); float s0 = (float)(0.6 * bd * Math.Sin(i * 0.012) + 0.25 * Math.Sin(i * 0.11) + 0.15 * Math.Sin(i * 0.43) + 0.08 * (rnd.NextDouble() - 0.5)); tp[i] = s0; tpp[i] = s0 * 0.6f; } eng.MasterTapPos = 4096;
        TitleCard("PURPHEX MUSIC", "Create. Convert. Master.\nA studio, a converter and a sound library for Windows.");
        app.Show(); Stealth(app); app.ClientSize = new Size(560, 660); Pump(600); { var b = new Bitmap(app.Width, app.Height); app.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); var canvas = new Bitmap(1600, 900); using (var g = Graphics.FromImage(canvas)) { using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1600, 900), Color.FromArgb(255, 20, 8, 50), Color.FromArgb(255, 4, 2, 12), 60f)) g.FillRectangle(br, 0, 0, 1600, 900); g.InterpolationMode = InterpolationMode.HighQualityBicubic; float sc = 860f / b.Height; g.DrawImage(b, (1600 - b.Width * sc) / 2f, 10, b.Width * sc, b.Height * sc); } Slide(canvas, "Free: the converter", "Paste a link, press Convert: MP3 or MP4. Split any song into vocals and instrumental with the vocal remover."); b.Dispose(); }
        SetF("tab", 0); Slide(Shot(), "The full studio", "A multitrack timeline, mixer, Auto Mix, 8D audio, Flex Pitch and lyrics, all in one window.");
        SetF("selFx", 0); Slide(Shot(), "250+ plugins", "EQ, compressors, reverbs, pitch and voice tools, bass and creative effects, all built in.");
        SetF("selFx", -1); M("OpenSounds"); Pump(300); Slide(Shot(), "Generated sound packs", "Drums, basses, synths and loops made on your own PC, ready to drag onto the timeline."); M("CloseSounds");
        eng.Playing = true; SetF("tab", 1); sf.ClientSize = new Size(1700, 1000); Pump(300); for (int i = 0; i < 30; i++) { sf.Invalidate(); sf.Update(); Application.DoEvents(); Pump(15); } Slide(Shot(), "Master while you watch", "AUTO MASTERING, 60+ presets and a live visualiser: the EQ, the effects and the light show on one page."); sf.ClientSize = new Size(1600, 900);
        SetF("tab", 5); for (int i = 0; i < 30; i++) { sf.Invalidate(); sf.Update(); Application.DoEvents(); Pump(15); } Slide(Shot(), "The visualiser", "Rings, shockwaves and sparks that move to your music. Switch between your mix and the master."); eng.Playing = false;
        SetF("tab", 0); M("OpenThemeSheet", false); Slide(Shot(), "Themes that change everything", "16 themes, your own photo as a background, and a Rainbow that never stops moving."); SetF("themeOpen", false);
        ProfileStore.Bio = "Dark dnb and bassline from the north. New tracks every month."; ProfileStore.Tags = "dnb bassline dark"; ProfileStore.Links.Clear(); ProfileStore.Links.Add("https://soundcloud.com/neonwolf"); ProfileStore.Followers = 128; ProfileStore.Published = true; ProfileStore.Accent = 160; SetF("profLoaded", true); SetF("tab", 6); Slide(Shot(), "Your own profile", "A banner, a picture, your bio and links. Publish it and other producers can find and follow you.");
        var al = new List<ArtistInfo>(); string[] an = { "DJ Neon Wolf", "Mia Waves", "Sub Zero Ghost", "Kali", "Bassline Bob", "Lunar Echo" }; for (int i = 0; i < an.Length; i++) al.Add(new ArtistInfo { Id = "a" + i.ToString("x12"), Name = an[i], Bio = "Dark dnb and bassline from the north.", Tags = new[] { "dnb", "bassline" }, Followers = 120 - i * 15, Tracks = 3 + i, Accent = i * 55 }); SetF("artList", al); SetF("artFetched", true); SetF("artSel", null); SetF("tab", 7); Slide(Shot(), "Find other producers", "Search artists, open their profiles and follow them.");
        TitleCard("START FREE", "purphexmusic.com\nFree: converter and vocal remover  -  Supporter: the full studio  -  Fantasy: lifetime");
        Console.WriteLine("=== slides done: " + n); Environment.Exit(0);
    }
}
