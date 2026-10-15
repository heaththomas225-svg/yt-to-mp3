using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Records the website's guide video: the real program, moving, driven by a script (a pointer, clicks, captions), written straight into ffmpeg. Everything shown is made up for the video
// (artists, songs, messages): nothing of this PC (names, folders) is in it. The community, artists and messages scenes run against a real purphex-web started on a free port.
//   csc /main:GR ... GuideRec.cs      run: GuideRec.exe <output folder> [sceneNumber to record alone]
static class GR
{
    const int W = 1280, H = 720, FPS = 30; // (the program is drawn at the size of the video: nothing is scaled down, and a picture is quick enough for 30 a second)
     static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static StudioForm sf; static App app; static StudioEngine eng; static Process ff; static Stream ffIn; static Stopwatch clock = Stopwatch.StartNew(); static long written; static Bitmap canvas = new Bitmap(W, H, PixelFormat.Format32bppArgb); static byte[] raw = new byte[W * H * 4];
    static string capTitle = "", capLine = ""; static double capAt = -10, capOff = -10; static PointF cur = new PointF(800, 450); static bool curShow; static double rippleAt = -10; static PointF ripplePt;
    static string tmp, ffm, ffp; static HashSet<int> only = new HashSet<int>(); static int sceneNo;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static double Now { get { return clock.Elapsed.TotalSeconds; } }
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    static void Stealth(Form f) { try { f.ShowInTaskbar = false; f.Opacity = 0.0; int ex = GetWindowLong(f.Handle, -20); SetWindowLong(f.Handle, -20, ex | 0x80000 | 0x20 | 0x80 | 0x08000000); } catch { } }
    static string FindTool(string n) { try { string root = Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"); if (Directory.Exists(root)) foreach (var f in Directory.GetFiles(root, n, SearchOption.AllDirectories)) return f; } catch { } return null; }

    // ---------------------------------------------------------------- the recording
    static Bitmap shotBmp;
    static Bitmap Shot()
    {
        if (shotBmp == null || shotBmp.Width != sf.ClientSize.Width || shotBmp.Height != sf.ClientSize.Height) { if (shotBmp != null) shotBmp.Dispose(); shotBmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height, PixelFormat.Format32bppPArgb); } var bmp = shotBmp;
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); return bmp;
    }
    // what is on the screen at this moment, in the shape of the video; the pointer, the ripple of a click and the caption are drawn over it
    static void Compose(Bitmap shot)
    {
        using (var g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; if (shot == null) g.Clear(Color.Black);
            if (shot != null) { if (shot.Width == W && shot.Height == H) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(shot, 0, 0); g.CompositingMode = CompositingMode.SourceOver; } else { double sc = Math.Min((double)W / shot.Width, (double)H / shot.Height); float w = (float)(shot.Width * sc), h = (float)(shot.Height * sc); g.DrawImage(shot, (W - w) / 2f, (H - h) / 2f, w, h); } }
            double t = Now;
            if (t - rippleAt < 0.5) { double k = (t - rippleAt) / 0.5; using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - k)), 0, 229, 255), 3f)) g.DrawEllipse(pen, ripplePt.X - 10 - (float)(30 * k), ripplePt.Y - 10 - (float)(30 * k), 20 + (float)(60 * k), 20 + (float)(60 * k)); }
            if (curShow) { var pts = new[] { new PointF(cur.X, cur.Y), new PointF(cur.X, cur.Y + 22), new PointF(cur.X + 6, cur.Y + 17), new PointF(cur.X + 11, cur.Y + 27), new PointF(cur.X + 15, cur.Y + 25), new PointF(cur.X + 10, cur.Y + 16), new PointF(cur.X + 17, cur.Y + 16) }; using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, pts); using (var pen = new Pen(Color.Black, 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pts); }
            float al = capTitle.Length == 0 ? 0 : (float)Math.Max(0, Math.Min(1, Math.Min((t - capAt) / 0.4, 1 - (t - capOff) / 0.35)));
            if (al > 0.01f)
            {
                bool vid = false; try { vid = sf != null && F<string>("vidKey").Length > 0; } catch { } var box = new RectangleF(vid ? W - 800 : 40, H - 150, 760, 112); using (var p = new GraphicsPath()) { float d = 28; p.AddArc(box.X, box.Y, d, d, 180, 90); p.AddArc(box.Right - d, box.Y, d, d, 270, 90); p.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90); p.AddArc(box.X, box.Bottom - d, d, d, 90, 90); p.CloseFigure(); using (var b = new SolidBrush(Color.FromArgb((int)(225 * al), 6, 3, 18))) g.FillPath(b, p); using (var pen = new Pen(Color.FromArgb((int)(200 * al), 0, 229, 255), 1.6f)) g.DrawPath(pen, p); }
                using (var f1 = new Font("Bahnschrift", 30, FontStyle.Bold, GraphicsUnit.Pixel)) using (var f2 = new Font("Bahnschrift SemiLight", 20, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * al), 245, 238, 255))) g.DrawString(capTitle.ToUpper(), f1, b, box.X + 26, box.Y + 14);
                    using (var b = new SolidBrush(Color.FromArgb((int)(235 * al), 190, 170, 240))) g.DrawString(capLine, f2, b, new RectangleF(box.X + 26, box.Y + 54, box.Width - 50, 54));
                    using (var pen = new Pen(Color.FromArgb((int)(255 * al), 255, 60, 220), 3f)) g.DrawLine(pen, box.X + 26, box.Y + 10, box.X + 90, box.Y + 10); using (var pen = new Pen(Color.FromArgb((int)(255 * al), 0, 229, 255), 3f)) g.DrawLine(pen, box.X + 90, box.Y + 10, box.X + 120, box.Y + 10);
                }
            }
        }
    }
    // one frame now; when the picture took longer than a frame, the same one is written for the frames that were missed (so the speed is always real)
    static Bitmap reuseShot; static bool shotErr; static bool keepToast; static long rendered; static double renderMs, composeMs, writeMs;
    static void Grab(Func<Bitmap> src)
    {
        if (!keepToast && sf != null) { try { SetF("toast", ""); } catch { } }   // the notes of the program about the sound card of this PC are not part of the video
        long idx = (long)(Now * FPS); if (idx < written) return; double q0 = Now; Bitmap shot = null; try { shot = src(); } catch (Exception ex) { if (!shotErr) { shotErr = true; var ie = ex.InnerException ?? ex; Console.WriteLine("  (a picture failed: " + ie.GetType().Name + ": " + ie.Message + " | " + (ie.StackTrace ?? "").Replace("\r", "").Replace("\n", " | ") + ")"); } } double q1 = Now; Compose(shot); if (shot != null && shot != shotBmp && shot != reuseShot) shot.Dispose(); double q2 = Now; rendered++; renderMs += q1 - q0; composeMs += q2 - q1;
        var bd = canvas.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); try { Marshal.Copy(bd.Scan0, raw, 0, raw.Length); } finally { canvas.UnlockBits(bd); }
        double w0 = Now; for (; written <= idx; written++) { try { ffIn.Write(raw, 0, raw.Length); } catch { } } writeMs += Now - w0;
    }
    static Func<Bitmap> source;
    static void Run(double sec, Action perFrame = null) { double until = Now + sec; while (Now < until) { if (perFrame != null) perFrame(); Application.DoEvents(); Grab(source); Thread.Sleep(1); } }
    static void Say(string title, string line) { capTitle = title; capLine = line; capAt = Now; capOff = 1e9; }
    static void Hush() { capOff = Now; }
    static double Ease(double k) { return k < 0.5 ? 2 * k * k : 1 - Math.Pow(-2 * k + 2, 2) / 2; }
    // the pointer glides to a place (and the program sees the mouse moving, so buttons light up and hints show)
    static void Move(PointF to, double sec, Action<int, int> inject = null)
    {
        var from = cur; double t0 = Now; curShow = true; inject = inject ?? ((x, y) => Ev("OnMouseMove", MouseButtons.None, 0, x, y));
        while (Now - t0 < sec) { double k = Ease(Math.Min(1, (Now - t0) / sec)); cur = new PointF((float)(from.X + (to.X - from.X) * k), (float)(from.Y + (to.Y - from.Y) * k)); inject((int)cur.X, (int)cur.Y); Application.DoEvents(); Grab(source); Thread.Sleep(1); }
        cur = to; inject((int)cur.X, (int)cur.Y);
    }
    static PointF Centre(Rectangle r) { return new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f); }
    static Rectangle Target(string label) { Application.DoEvents(); return (Rectangle)M("TargetOf", label); }
    // glide to a button of the program by its label and press it
    static bool Click(string label, double glide = 0.7, double after = 0.5)
    {
        var r = Target(label); if (r.IsEmpty) { Console.WriteLine("  (no button: " + label + ")"); return false; }
        var p = Centre(r); Move(p, glide); var r2 = Target(label); if (!r2.IsEmpty && Centre(r2) != p) { p = Centre(r2); Move(p, 0.2); } rippleAt = Now; ripplePt = p; Ev("OnMouseDown", MouseButtons.Left, 1, (int)p.X, (int)p.Y); Run(0.08); Ev("OnMouseUp", MouseButtons.Left, 1, (int)p.X, (int)p.Y); Run(after); return true;
    }
    static long lastR, lastW;
    static void Scene(int n, string name) { if (written > lastW) Console.WriteLine("   (the scene before: " + (rendered - lastR) + " different pictures in " + (written - lastW) + " frames = " + ((rendered - lastR) * 100 / Math.Max(1, written - lastW)) + "%)"); lastR = rendered; lastW = written; sceneNo = n; Console.WriteLine("scene " + n + ": " + name + "  @" + Now.ToString("0.0") + "s"); }
    static bool On(int n) { return only.Count == 0 || only.Contains(n); }
    static void Log(string m) { Console.WriteLine("    " + m + "  @" + Now.ToString("0.0") + "s"); }
    static void Do(int n, Action a) { if (!On(n)) return; try { a(); } catch (Exception ex) { Console.WriteLine("SCENE " + n + " FAILED: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); Hush(); curShow = false; } }

    // ---------------------------------------------------------------- made-up music, so the waveforms and meters look like a song
    static AudioClip Song(int sec, int kind)
    {
        int sr = 44100, len = sr * sec; var l = new float[len]; var r = new float[len]; var rnd = new Random(kind * 7 + 1); double bpm = 140, beat = 60.0 / bpm * sr;
        for (int i = 0; i < len; i++)
        {
            double t = i / (double)sr, bt = (i % beat) / beat, bar = (i / (beat * 4)); double v = 0;
            if (kind == 0) { v += Math.Exp(-bt * 9) * Math.Sin(2 * Math.PI * (45 + 90 * Math.Exp(-bt * 22)) * t) * 0.8; double sn = ((int)(i / beat) % 2 == 1) ? Math.Exp(-bt * 14) * (rnd.NextDouble() * 2 - 1) * 0.35 : 0; double hh = Math.Exp(-((i % (beat / 2)) / (beat / 2)) * 40) * (rnd.NextDouble() * 2 - 1) * 0.12; v += sn + hh; }
            else if (kind == 1) { double f = 55 * Math.Pow(2, ((int)bar % 4 == 2 ? 3 : ((int)bar % 4 == 3 ? 5 : 0)) / 12.0); v += Math.Sin(2 * Math.PI * f * t) * 0.5 * (0.6 + 0.4 * Math.Sin(t * 5)) + Math.Sin(2 * Math.PI * f * 2 * t) * 0.12; }
            else { double[] sc = { 0, 3, 7, 10, 12, 10, 7, 3 }; double f = 220 * Math.Pow(2, sc[(int)(i / (beat / 2)) % 8] / 12.0); double env = Math.Exp(-((i % (beat / 2)) / (beat / 2)) * 3); v += (Math.Sin(2 * Math.PI * f * t) + 0.4 * Math.Sin(2 * Math.PI * f * 2 * t)) * 0.22 * env; }
            l[i] = (float)v; r[i] = (float)v * (kind == 2 ? 0.8f : 1f);
        }
        return AudioClip.FromArrays(l, r, "song");
    }
    static void WriteWav(string path, AudioClip c, float gain) { int n = c.Length; using (var ws = new WavStream(path, 44100, 16)) { var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) { l[i] = c.L[i] * gain; r[i] = c.R[i] * gain; } ws.Write(l, r, 0, n); } }
    static string Ff(string args, int ms = 120000) { var p = Process.Start(new ProcessStartInfo(ffm, "-hide_banner -loglevel error -y " + args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }); string e = p.StandardError.ReadToEnd(); p.WaitForExit(ms); return e; }

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "guide"); Directory.CreateDirectory(outDir); if (args.Length > 1) foreach (var x in args[1].Split(',')) only.Add(int.Parse(x));
        ffm = FindTool("ffmpeg.exe"); ffp = FindTool("ffprobe.exe"); if (ffm == null) { Console.WriteLine("ffmpeg is needed"); return; } StudioEnv.Ffmpeg = () => ffm; StudioEnv.Ffprobe = () => ffp;
        tmp = Path.Combine(Path.GetTempPath(), "purphex_guide_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp); StudioEnv.DataDir = Path.Combine(tmp, "appdata"); Directory.CreateDirectory(StudioEnv.DataDir);
        string videoOut = Path.Combine(outDir, "guide-silent.mp4");
        ff = Process.Start(new ProcessStartInfo(ffm, "-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s " + W + "x" + H + " -r " + FPS + " -i - -c:v libx264 -preset veryfast -crf 12 -pix_fmt yuv420p \"" + videoOut + "\"") { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true });
        try { ff.PriorityClass = ProcessPriorityClass.BelowNormal; Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency; } catch { }
        ffIn = ff.StandardInput.BaseStream;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); Application.ThreadException += (se, ee) => Console.WriteLine("  thread exception: " + ee.Exception.GetType().Name + ": " + ee.Exception.Message + " @ " + (ee.Exception.StackTrace ?? "").Split('\n')[0]);
        Application.EnableVisualStyles(); Edition.Override = Tier.Fantasy; EmojiSprites.Disabled = false; StudioForm.VideoNoOsFull = true; StudioForm.NoWindowPaint = true; Prefs.OutVol = 0;
        app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; ProjectLibrary.ProfileName = "Neo Wolf"; ProfileStore.Bio = ""; CommunityPresets.BaseUrl = "http://127.0.0.1:1";
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Song(70, 2))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Song(70, 0))); eng.Tracks.Add(StudioEngine.NewTrack("Bass", "audio", Song(70, 1)));
        foreach (var id in new[] { "eq", "reverb", "comp" }) { var fx = FxRegistry.Create(id); fx.Prepare(44100); eng.Tracks[0].Fx.Add(fx); }
        sf = new StudioForm(app); sf.ClientSize = new Size(W, H); sf.Show(); Stealth(sf); Pump(600); Themes.Apply(0); sf.msgAskHook = p => "Love this one! Want to make a track together? \U0001F525"; SetF("askHook", (Func<string, bool>)(t => true)); sf.songNoteHook = x => { };
        foreach (var em in new[] { "\U0001F525", "\U0001F3A7", "\u2764\uFE0F", "\U0001F64C" }) EmojiSprites.Get(em); { var eu = DateTime.Now.AddSeconds(25); while (DateTime.Now < eu && !EmojiSprites.Known("\U0001F525")) Pump(100); Console.WriteLine("colour emoji ready: " + EmojiSprites.Known("\U0001F525")); }
        source = Shot; clock.Restart(); written = 0; Scenes();
        try { if (web != null && !web.HasExited) web.Kill(); } catch { }
        try { ffIn.Close(); ff.WaitForExit(120000); } catch { }
        Console.WriteLine("render stats: " + rendered + " pictures for " + written + " frames; per picture: paint " + (renderMs / Math.Max(1, rendered) * 1000).ToString("0") + " ms, compose " + (composeMs / Math.Max(1, rendered) * 1000).ToString("0") + " ms, write " + (writeMs / Math.Max(1, rendered) * 1000).ToString("0") + " ms");
        double total = written / (double)FPS; string bed = Path.Combine(tmp, "bed.wav"); { var a0 = Song(70, 0); var a1 = Song(70, 1); var a2 = Song(70, 2); int n = a0.Length; var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) { float v = (a0.L[i] * 0.8f + a1.L[i] * 0.75f + a2.L[i] * 0.7f) * 0.55f; l[i] = v; r[i] = (a0.R[i] * 0.8f + a1.R[i] * 0.75f + a2.R[i] * 0.7f) * 0.55f; } using (var ws = new WavStream(bed, 44100, 16)) ws.Write(l, r, 0, n); }
        string finalOut = Path.Combine(outDir, "guide.mp4"); string me2 = Ff("-i \"" + videoOut + "\" -stream_loop 6 -i \"" + bed + "\" -t " + total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " -c:v libx264 -preset slow -crf 19 -tune animation -pix_fmt yuv420p -c:a aac -b:a 160k -af \"afade=t=in:d=2,afade=t=out:st=" + Math.Max(0, total - 3).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ":d=3\" -shortest -movflags +faststart \"" + finalOut + "\"", 600000); if (me2.Length > 0) Console.WriteLine(me2);
        Console.WriteLine("=== recorded " + written + " frames (" + total.ToString("0.0") + " s) to " + finalOut); Environment.Exit(0);
    }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(10); } }

    // ---------------------------------------------------------------- the story
    static void TitleCard(string big, string small, double sec)
    {
        Func<Bitmap> card = () => { const int W = 1600, H = 900; var s = new Bitmap(W, H, PixelFormat.Format32bppPArgb); using (var g = Graphics.FromImage(s)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; using (var br = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(255, 20, 8, 50), Color.FromArgb(255, 4, 2, 12), 60f)) g.FillRectangle(br, 0, 0, W, H);
                double ph = Now * 1.2; float cx = W / 2f, cy = 300; var hex = new PointF[6]; for (int i = 0; i < 6; i++) hex[i] = new PointF(cx + (float)Math.Cos(i * Math.PI / 3) * 150, cy + (float)Math.Sin(i * Math.PI / 3) * 150);
                for (int i = 4; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(34, 255, 43, 214), i * 9f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex); using (var pen = new Pen(Color.FromArgb(255, 0, 229, 255), 6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex);
                var wave = new List<PointF>(); for (int x = -100; x <= 100; x += 4) wave.Add(new PointF(cx + x, cy + (float)(Math.Sin(x * 0.12 + ph * 4) * 28 * Math.Exp(-x * x / 5200.0))));
                using (var pen = new Pen(Color.FromArgb(255, 255, 60, 220), 4f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, wave.ToArray());
                using (var f1 = new Font("Bahnschrift", 100, FontStyle.Bold, GraphicsUnit.Pixel)) using (var f2 = new Font("Bahnschrift SemiLight", 36, FontStyle.Regular, GraphicsUnit.Pixel)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center })
                { using (var b = new SolidBrush(Color.White)) g.DrawString(big, f1, b, new RectangleF(0, 500, W, 130), sfm); using (var b = new SolidBrush(Color.FromArgb(255, 190, 170, 240))) g.DrawString(small, f2, b, new RectangleF(0, 650, W, 150), sfm); } } return s; };
        var keep = source; curShow = false; source = card; Run(sec); source = keep;
    }
    static void Scenes()
    {
        if (On(1)) { Scene(1, "title"); TitleCard("PURPHEX MUSIC", "Create. Convert. Master. Share.\nA studio, a converter and a community for Windows.", 4.0); }
        Do(2, ConverterScene); Do(3, ProjectsScene); Do(4, StudioScene); Do(5, PresetScene); Do(6, MasterScene); Do(7, BassLabScene); Do(8, CommunityScene); Do(9, ArtistScene); Do(10, ProjectsScene2); Do(11, ThemesScene);
        if (On(99)) { Hush(); TitleCard("START FREE", "purphexmusic.com\nFree: converter and vocal remover  -  Supporter: the full studio  -  Fantasy: lifetime", 4.5); }
    }
    static void ConverterScene()
    {
        Scene(2, "converter"); app.Show(); Stealth(app); app.ClientSize = new Size(560, 660); try { ((TextBox)typeof(App).GetField("outDir", BF).GetValue(app)).Text = @"D:\Music\Purphex"; ((TextBox)typeof(App).GetField("links", BF).GetValue(app)).Text = "lofi beats to study to"; } catch { } Pump(600);
        var keep = source; source = () => { var b = new Bitmap(app.Width, app.Height); app.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); var c = new Bitmap(1600, 900); using (var g = Graphics.FromImage(c)) { using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1600, 900), Color.FromArgb(255, 20, 8, 50), Color.FromArgb(255, 4, 2, 12), 60f)) g.FillRectangle(br, 0, 0, 1600, 900); g.InterpolationMode = InterpolationMode.HighQualityBicubic; float sc = 860f / b.Height; g.DrawImage(b, (1600 - b.Width * sc) / 2f, 20, b.Width * sc, b.Height * sc); } b.Dispose(); return c; };
        try { var an = (CheckBox)typeof(App).GetField("animate", BF).GetValue(app); an.Checked = false; } catch { } Pump(300); curShow = false; Say("Free: the converter", "Paste a link, press Convert: MP3 or MP4. Split any song into vocals and instrumental with the vocal remover."); Run(6.0); Hush(); Run(0.5); source = keep; app.Hide();
    }
    static void ProjectsScene()
    {
        Scene(3, "my projects"); string d0 = Path.Combine(tmp, "projects"); Directory.CreateDirectory(d0); ProjectLibrary.Entries.Clear(); string[] names = { "Night Drive", "Skyline", "Late Hours", "Bass In The Park", "Sunday Loop" };
        for (int i = 0; i < names.Length; i++) { string p = Path.Combine(d0, names[i] + ".ytmproj"); File.WriteAllText(p, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = p, Name = names[i], Tracks = 4 + i, Bpm = 140 - i * 4, Seconds = 150 + i * 20, OnProfile = i < 2, Tags = i == 0 ? "dnb dark" : (i == 1 ? "trap" : ""), Updated = DateTime.Now.AddHours(-i * 5), Opened = DateTime.Now.AddHours(-i * 5) }); }
        eng.Tracks.Clear(); SetF("projectsLoaded", true); sf.ShowWelcome(); curShow = true; cur = new PointF(800, 500); Run(0.8);
        Say("A welcome box first", "Purphex Music opens on the mix with a box on top: open one of your projects or start a new one."); Run(2.6); Click("ALL MY PROJECTS"); Run(0.8);
        Say("Your projects", "Every project has a card with its own running border."); Run(1.6);
        Move(new PointF(560, 520), 1.0); Run(1.2); Move(new PointF(1180, 520), 1.2); Run(0.8); Say("Share, EP / ALBUM, music video", "SHARE the community, put projects together as an EP or an album, add a music video, publish a song."); Run(1.2);
        var ep = Target("EP / ALBUM"); if (!ep.IsEmpty) Move(Centre(ep), 1.0); Run(1.2); var pub = Target("PUBLISH ALL"); if (!pub.IsEmpty) Move(Centre(pub), 0.9); Run(1.2);
        var vb = Target("UPLOAD MUSIC VIDEO"); if (!vb.IsEmpty) Move(Centre(vb), 1.0); Run(1.8); Hush(); Run(0.4); Say("Start a new project", "The first card starts a new project, and every one of your projects opens from its card."); Move(new PointF(140, 380), 1.0); Run(2.2); Hush(); curShow = false;
    }
    static void LoadTracks()
    {
        eng.Tracks.Clear(); eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Song(70, 2))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Song(70, 0))); eng.Tracks.Add(StudioEngine.NewTrack("Bass", "audio", Song(70, 1)));
        foreach (var id in new[] { "eq", "reverb", "comp" }) { var fx = FxRegistry.Create(id); fx.Prepare(44100); eng.Tracks[0].Fx.Add(fx); } SetF("selTrack", 0); SetF("selFx", -1); SetF("tab", 0); SetF("welcomeOn", false); eng.Seek(0); M("FitView");
    }
    static void StudioScene()
    {
        Scene(4, "studio"); LoadTracks(); curShow = true; cur = new PointF(900, 400); Run(0.6); Say("The full studio", "A multitrack timeline, mixer, Auto Mix, 8D audio, Flex Pitch and lyrics, all in one window."); var play = Target("PLAY"); if (!play.IsEmpty) Move(Centre(play), 1.0);
        Click("PLAY", 0.4, 0.3); Run(5.0); var am = Target("AUTO MIX"); if (!am.IsEmpty) Move(Centre(am), 1.2); Run(2.2); Hush(); Run(0.3);
        Say("250+ plugins", "EQ, compressors, reverbs, pitch and voice tools, bass and creative effects, all built in."); SetF("selFx", 0); Run(3.4); SetF("selFx", 1); Run(2.4); SetF("selFx", -1); Hush(); Run(0.4);
        Say("Sound packs", "Drums, basses, synths and loops made on your own PC, ready to drag onto the timeline."); Click("SOUND PACKS", 1.0, 0.8); Run(3.0); try { M("CloseSounds"); } catch { } Hush(); Run(0.3);
    }
    static void PresetScene()
    {
        Scene(5, "presets and instruments"); SetF("tab", 0); SetF("selFx", -1); var tr = eng.Tracks[0]; var bl = FxRegistry.Create("synth"); bl.Prepare(44100); ((InstrumentFx)bl).Owner = tr; tr.Fx.Add(bl);
        Log("preset"); Say("Presets keep your plugins", "Every preset is a box of its own, as many as you like. Your plugins, and Bass Lab, stay. REMOVE takes away only that preset."); M("PutPreset", tr, "Melodic Rap", Presets.BuildChain("Melodic Rap")); Log("preset put"); SetF("selFx", -1); Run(1.0);
        var pr = Target("Presets..."); if (!pr.IsEmpty) Move(Centre(pr), 1.0); Run(3.0); var rm = Target("REMOVE"); if (!rm.IsEmpty) { Move(Centre(rm), 1.2); Run(1.2); } Run(2.0); Hush(); Run(0.3);
    }
    static void MasterScene()
    {
        Scene(6, "mastering"); if (!eng.Playing) eng.Play(); SetF("tab", 1); Say("Master while you watch", "Auto Mastering, loudness, an EQ picture before and after, and a visualiser that moves with the music."); Run(6.0); Hush();
        SetF("tab", 5); Say("The visualiser", "Rings, shockwaves and sparks that move to your music."); Run(5.0); Hush(); SetF("tab", 0); Run(0.3);
    }
    static void WaitUntil(Func<bool> c, double max) { double until = Now + max; while (Now < until && !c()) { Application.DoEvents(); Grab(source); Thread.Sleep(1); } }
    // ---- Bass Lab (its own window: its pointer events go to its picture) ----
    static void BassLabScene()
    {
        Scene(7, "bass lab"); var sfx = new SynthFx(); sfx.Prepare(44100); foreach (var nm in new[] { "My Wobble", "Late Night Reese", "My Neon Lead" }) { sfx.PresetName = nm; SynthPresets.SaveUser(sfx, nm); } SynthPresets.Apply(sfx, "Wobble Bass");
        var sw = new SynthWindow(sfx, null); sw.StartPosition = FormStartPosition.Manual; sw.Show(); Stealth(sw); sw.ClientSize = new Size(W, H); Pump(700); var cv = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(sw); var T = typeof(SynthCanvas);
        Action<string, MouseButtons, int, int, int> cev = (name, b, clicks, x, y) => { var p = cv.Location; T.GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(cv, new object[] { new MouseEventArgs(b, clicks, x - p.X, y - p.Y, 0) }); };
        Func<string, Rectangle> tip = t => { foreach (var r in (System.Collections.IList)T.GetField("regs", BF).GetValue(cv)) { var tf = r.GetType().GetField("Tip"); if (tf != null && (string)tf.GetValue(r) == t) return (Rectangle)T.GetMethod("ToClient", BF).Invoke(cv, new object[] { (RectangleF)r.GetType().GetField("R").GetValue(r) }); } return Rectangle.Empty; };
        Bitmap pianoPic = null; Control pianoCtl = null; foreach (Control ch in sw.Controls) if (ch != cv && ch.Visible && ch.Top > cv.Top) pianoCtl = ch; if (pianoCtl != null) { pianoPic = new Bitmap(pianoCtl.Width, pianoCtl.Height); pianoCtl.DrawToBitmap(pianoPic, new Rectangle(0, 0, pianoCtl.Width, pianoCtl.Height)); }
        Console.WriteLine("  canvas " + cv.Left + "," + cv.Top + " " + cv.Width + "x" + cv.Height + "; window client " + sw.ClientSize + "; piano " + (pianoCtl == null ? "none" : pianoCtl.GetType().Name + " " + pianoCtl.Left + "," + pianoCtl.Top + " " + pianoCtl.Width + "x" + pianoCtl.Height)); Bitmap cvBmp = null; Bitmap bassBmp = new Bitmap(sw.ClientSize.Width, sw.ClientSize.Height, PixelFormat.Format32bppPArgb); reuseShot = bassBmp; var keep = source; source = () => { if (cvBmp == null || cvBmp.Width != cv.Width || cvBmp.Height != cv.Height) { if (cvBmp != null) cvBmp.Dispose(); cvBmp = new Bitmap(cv.Width, cv.Height, PixelFormat.Format32bppArgb); } cv.DrawToBitmap(cvBmp, new Rectangle(0, 0, cv.Width, cv.Height)); using (var gg = Graphics.FromImage(bassBmp)) { gg.Clear(Color.FromArgb(255, 13, 7, 26)); gg.DrawImageUnscaled(cvBmp, cv.Left, cv.Top); if (pianoPic != null) gg.DrawImageUnscaled(pianoPic, pianoCtl.Left, pianoCtl.Top); } return bassBmp; };   // the window without its frame, drawn by its own painting
        Action<int, int> mv = (x, y) => cev("OnMouseMove", MouseButtons.None, 0, x, y); cur = new PointF(1200, 300); curShow = true; Run(0.8);
        Say("Bass Lab", "A wavetable synthesizer for bass and leads: oscillators, warp, filters, wobble LFOs and a matrix. Edit it inside Bass Lab, like a real plugin."); Run(3.2);
        Func<string, double, bool> press = (t, after) => { var r = tip(t); if (r.IsEmpty) { Console.WriteLine("  (no control: " + t + ")"); return false; } var p = Centre(r); Move(p, 0.8, mv); rippleAt = Now; ripplePt = p; cev("OnMouseDown", MouseButtons.Left, 1, (int)p.X, (int)p.Y); Run(0.08); cev("OnMouseUp", MouseButtons.Left, 1, (int)p.X, (int)p.Y); Run(after); return true; };
        Hush(); Run(0.3); Say("YOUR SOUNDS (drag n drop)", "The menu button opens the sounds that come with the DAW, the sounds you saved (until you delete them) and the community's."); press("YOUR SOUNDS (drag n drop)", 1.8);
        press("Click to load Slow Wub", 1.6); press("The sounds you saved: they stay until you delete them", 2.0); press("Sounds other people published", 1.6); press("The sounds that come with the DAW", 1.2);
        Hush(); Run(0.3); Say("Save, share, drag and drop", "SAVE CURRENT SOUND, PUBLISH SOUNDS TO COMMUNITY, drag a sound out as a file, drop a file in to add it."); var pub = tip("Publish a sound for everyone to use (you pick which)."); if (!pub.IsEmpty) Move(Centre(pub), 1.0, mv); Run(3.2);
        try { T.GetMethod("CloseSounds", BF).Invoke(cv, null); } catch { } Run(1.2); Hush(); Run(0.3); source = keep; reuseShot = null; bassBmp.Dispose(); try { sw.Close(); } catch { } curShow = false;
    }

    // ---- a real website with made-up artists, songs, covers, a music video, friends, messages and a collab ----
    static int port; static Process web; static string kMe, idMe, idMia, idSub, idKali, idLuna; static bool webUp;
    static string Key() { var b = new byte[16]; new Random(Guid.NewGuid().GetHashCode()).NextBytes(b); return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant(); }
    static void Cover(string path, int hue)
    {
        using (var b = new Bitmap(480, 480)) using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; Color c1 = Color.FromArgb(255, 20, 8, 50), c2 = HsvColor(hue, 0.85, 0.95), c3 = HsvColor(hue + 70, 0.9, 1.0); using (var br = new LinearGradientBrush(new Rectangle(0, 0, 480, 480), c1, Color.FromArgb(255, 4, 2, 12), 55f)) g.FillRectangle(br, 0, 0, 480, 480);
            var hex = new PointF[6]; for (int i = 0; i < 6; i++) hex[i] = new PointF(240 + (float)Math.Cos(i * Math.PI / 3) * 150, 240 + (float)Math.Sin(i * Math.PI / 3) * 150);
            for (int k = 5; k >= 1; k--) using (var pen = new Pen(Color.FromArgb(40, c2), k * 9f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex); using (var pen = new Pen(c2, 7f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hex);
            var wave = new List<PointF>(); var rnd = new Random(hue); double ph = rnd.NextDouble() * 6; for (int x = -130; x <= 130; x += 4) wave.Add(new PointF(240 + x, 240 + (float)(Math.Sin(x * 0.1 + ph) * 55 * Math.Exp(-x * x / 9000.0)))); using (var pen = new Pen(c3, 6f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, wave.ToArray());
            b.Save(path, ImageFormat.Png);
        }
    }
    static Color HsvColor(double h, double s, double v) { h = ((h % 360) + 360) % 360; double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r = 0, g = 0, b = 0; if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; } return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255)); }
    static void EnsureWeb()
    {
        if (webUp) return; webUp = true; string webDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "purphex-web"); port = 7600 + new Random().Next(1200);
        var psi = new ProcessStartInfo("node", "server.js") { WorkingDirectory = webDir, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; psi.EnvironmentVariables["PORT"] = port.ToString(); psi.EnvironmentVariables["DATA_DIR"] = Path.Combine(tmp, "web"); psi.EnvironmentVariables["PUBLIC_URL"] = "";
        web = Process.Start(psi); web.OutputDataReceived += (a, b) => { }; web.ErrorDataReceived += (a, b) => { }; web.BeginOutputReadLine(); web.BeginErrorReadLine(); CommunityPresets.BaseUrl = "http://127.0.0.1:" + port;
        for (int i = 0; i < 100; i++) { try { using (var wc = new System.Net.WebClient()) { wc.DownloadString(CommunityPresets.BaseUrl + "/healthz"); break; } } catch { Thread.Sleep(150); } }
        // the music: three little songs as MP3, and a music video
        string[] mp3 = new string[3]; for (int i = 0; i < 3; i++) { string wv = Path.Combine(tmp, "s" + i + ".wav"); var a0 = Song(14, 0); var a1 = Song(14, 1); var a2 = Song(14, 2); int n = a0.Length; var l = new float[n]; var r = new float[n]; for (int k = 0; k < n; k++) { float v = a0.L[k] * (i == 1 ? 0.4f : 1f) + a1.L[k] * (i == 0 ? 0.5f : 1f) + a2.L[k] * (i == 2 ? 1f : 0.7f); l[k] = v * 0.6f; r[k] = v * 0.6f; } using (var ws = new WavStream(wv, 44100, 16)) ws.Write(l, r, 0, n); mp3[i] = Path.Combine(tmp, "s" + i + ".mp3"); Ff("-i \"" + wv + "\" -b:a 128k \"" + mp3[i] + "\""); }
        string vid = Path.Combine(tmp, "mv.mp4"); string ve = Ff("-f lavfi -i \"gradients=size=640x360:rate=24:speed=0.03:nb_colors=5\" -t 30 -c:v libx264 -pix_fmt yuv420p \"" + vid + "\""); if (!File.Exists(vid)) Ff("-f lavfi -i \"mandelbrot=size=640x360:rate=24\" -t 30 -c:v libx264 -pix_fmt yuv420p \"" + vid + "\"");
        Func<string, string, string, string, string, string> mkTracks = (title, pid, tags, v, extra) => "{\"t\":\"" + title + "\",\"p\":\"" + pid + "\",\"b\":" + (140 + Math.Abs(title.GetHashCode()) % 40) + ",\"s\":" + (150 + Math.Abs(title.GetHashCode()) % 90) + ",\"g\":[\"" + tags.Replace(" ", "\",\"") + "\"]}";
        kMe = Key(); string kMia = Key(), kSub = Key(), kKali = Key(), kLuna = Key(); ArtistInfo me;
        Func<string, string, string, string, string, string, string> pub = (key, name, bio, tags, tracks, colls) => "{\"key\":\"" + key + "\",\"name\":\"" + name + "\",\"bio\":\"" + bio + "\",\"tags\":[\"" + tags.Replace(" ", "\",\"") + "\"],\"timeMade\":" + (40000 + Math.Abs(name.GetHashCode()) % 90000) + ",\"tracks\":[" + tracks + "],\"collections\":[" + colls + "]}";
        string[] miaT = { "Late Hours", "Blue Smoke", "Afterglow" }, subT = { "Neon Streets", "Skyline", "Rooftop", "Last Train" };
        string miaTracks = string.Join(",", miaT.Select((t, i) => mkTracks(t, "mia" + i, "garage lofi", "", ""))), subTracks = string.Join(",", subT.Select((t, i) => mkTracks(t, "sub" + i, "trap dark", "", "")));
        ProfApi.Publish(pub(kMia, "Mia Waves", "Garage and lofi from the coast. New EP out now.", "garage lofi chill", miaTracks, "{\"id\":\"e1\",\"t\":\"Late Hours EP\",\"k\":\"ep\",\"p\":[\"mia0\",\"mia1\",\"mia2\"]}"), out me); idMia = me.Id;
        ProfApi.Publish(pub(kSub, "Sub Zero Ghost", "Dark trap and sub bass. Album: City Lights.", "trap dark bass", subTracks, "{\"id\":\"a1\",\"t\":\"City Lights\",\"k\":\"album\",\"p\":[\"sub0\",\"sub1\",\"sub2\",\"sub3\"]}"), out me); idSub = me.Id;
        ProfApi.Publish(pub(kKali, "Kali", "Dubstep, wobble and growl. Collabs welcome.", "dubstep bass wobble", mkTracks("Wobble Therapy", "kali0", "dubstep bass", "", ""), ""), out me); idKali = me.Id;
        ProfApi.Publish(pub(kLuna, "Lunar Echo", "Late night lofi and ambient.", "lofi ambient chill", mkTracks("Moon Tape", "luna0", "lofi ambient", "", ""), ""), out me); idLuna = me.Id;
        ProfApi.Publish(pub(kMe, "Neo Wolf", "Dark dnb and bassline from the north. New tracks every month.", "dnb bassline dark", mkTracks("Night Drive", "me0", "dnb dark", "", "") + "," + mkTracks("Skyline Run", "me1", "dnb bassline", "", ""), ""), out me); idMe = me.Id;
        string sid; int hue = 20; Action<string, string, string, string, string, int> up = (key, pid, title, tags, file, h) => { string cv = Path.Combine(tmp, "cv" + (hue += 37) + ".png"); Cover(cv, hue); CommunitySongs.Upload(key, pid, title, tags, "", file, cv, out sid); };
        for (int i = 0; i < 3; i++) up(kMia, "mia" + i, miaT[i], "garage lofi", mp3[i % 3], 0); for (int i = 0; i < 4; i++) up(kSub, "sub" + i, subT[i], "trap dark", mp3[(i + 1) % 3], 0); up(kKali, "kali0", "Wobble Therapy", "dubstep bass", mp3[1], 0); up(kLuna, "luna0", "Moon Tape", "lofi ambient", mp3[2], 0);
        up(kMe, "me1", "Skyline Run", "dnb bassline", mp3[0], 0); up(kMe, "me0", "Night Drive", "dnb dark", mp3[1], 0); if (File.Exists(vid)) ProfApi.VideoUpload(kMe, "me0", vid);
        // friends, messages and a collab (all with the made-up artists)
        string st; ProfApi.Friend(kMia, idMe, true, out st); ProfApi.Friend(kKali, idMe, true, out st); ProfApi.Friend(kMe, idKali, true, out st);
        ProfApi.Send(kMia, idMe, "Love your drop on Night Drive! \U0001F525 The bassline is mad.", "Night Drive"); ProfApi.Send(kKali, idMe, "Want to make a track together? I have a 174 bpm idea.", ""); ProfApi.Send(kMe, idKali, "Yes! Send me the invite \U0001F680", "");
        ProfApi.CollabInvite(kKali, idMe, "Late Night Rider"); var cl = ProfApi.Collabs(kMe); if (cl != null && cl.Count > 0) { ProfApi.CollabAnswer(kMe, cl[0].Id, true); string any = mp3[0]; ProfApi.CollabUpload(kKali, cl[0].Id, any); ProfApi.CollabUpload(kMe, cl[0].Id, mp3[2]); }
        ProfileStore.Key = kMe; ProfileStore.Published = true; ProfileStore.Save(); ProfileStore.Bio = "Dark dnb and bassline from the north."; ProfileStore.Tags = "dnb bassline dark";
    }
    static bool Played() { var pe = F<StudioEngine>("previewEng"); return pe != null && pe.Playing; }
    static void CommunityScene()
    {
        Scene(8, "community"); EnsureWeb(); SetF("tab", 4); SetF("commFilter", 0); M("FeedRefetch"); curShow = true; cur = new PointF(900, 500); Run(0.5); WaitUntil(() => F<System.Collections.IList>("feed").Count > 0, 8.0); Run(0.6);
        Say("The Community tab", "Every song published from Purphex Music, from every artist. Filter by genre, new releases, albums and EPs."); Run(2.4);
        Click("#DNB", 0.9, 1.4); Click("ALL GENRES", 0.7, 0.8);
        Func<int, string, bool> tabClick = (k, nm) => { var p = new PointF(18 + 4 + 140 * k + 67, 209); Move(p, 0.8); rippleAt = Now; ripplePt = p; SetF("commFilter", k); M("FeedRefetch"); Run(1.8); return true; };
        Hush(); Run(0.3); Say("Albums and EPs", "Put your projects together as an EP or an album, and they show up here."); tabClick(2, "ALBUMS"); Run(1.4); tabClick(3, "EPS"); Run(1.4); tabClick(0, "ALL"); Hush(); Run(0.3);
        Say("Play, save, share", "Play a song, SAVE SONG keeps it, and the link under it opens it on the website."); Click("SAVE SONG", 1.0, 1.0); Run(0.6); Hush(); Run(0.2);
        Say("Music videos", "A song with a music video asks if you want to watch it. The video plays above the strip, in time with the song."); Click("PLAY", 1.0, 0.3);
        WaitUntil(() => F<Bitmap>("vidBmp") != null, 12.0); Run(3.6); Hush(); Run(0.2); Say("Full screen at any size", "FULL SCREEN fits the picture to your screen without stretching it, whatever the shape of the video."); Click("FULL SCREEN", 0.9, 0.4); Run(5.0);
        Click("EXIT FULL SCREEN", 0.9, 0.6); Run(1.2); Hush(); M("StopPreview"); Run(0.3); curShow = false;
    }
    static void ScrollTo(string field, float to, double sec) { float from = F<float>(field); double t0 = Now; while (Now - t0 < sec) { double k = Ease(Math.Min(1, (Now - t0) / sec)); SetF(field, (float)(from + (to - from) * k)); Application.DoEvents(); Grab(source); Thread.Sleep(1); } SetF(field, to); }
    static void ArtistScene()
    {
        Scene(9, "artists"); EnsureWeb(); M("ProfileRefresh"); SetF("tab", 7); M("OpenArtists"); curShow = true; cur = new PointF(900, 500); Run(1.2); Say("Artists: talk to artists", "Add friends or accept their request, write to them (also about a song), block, report, and see their time made."); M("OpenArtist", idMia);
        WaitUntil(() => !Target("ACCEPT FRIEND REQUEST").IsEmpty, 8.0); Run(2.2); Click("ACCEPT FRIEND REQUEST", 1.0, 2.0);
        Hush(); Run(0.2); Say("Tracks you can play", "Every track has a play button, its own progress, AUDIO ONLY or MUSIC VIDEO, and a button to reply to the song."); ScrollTo("artScroll", 330f, 1.6); Run(0.6);
        try { var art = F<ArtistInfo>("artSel"); if (art != null && art.TrackList.Count > 0) M("ArtistTrackClick", art, art.TrackList[0]); } catch (Exception ex) { Console.WriteLine("  (play: " + ex.Message + ")"); }
        WaitUntil(Played, 8.0); Run(3.0); Hush(); Run(0.2); Say("Message an artist about a song", "Write to the artist about the song you just heard."); Click("MESSAGE ABOUT IT", 1.0, 1.6); WaitUntil(() => F<int>("artView") == 1, 5.0); M("StopPreview"); Run(2.4);
        Hush(); Run(0.2); Say("Messages", "Your conversations and friend requests. The number on the ARTISTS button is what you have not read."); Run(4.4);
        Hush(); Run(0.2); Say("Friends", "A Friends page in the same layout: your friends, the requests to answer, and a button for everything you can do with them."); Click("FRIENDS", 1.0, 0.8); Run(4.2);
        Hush(); Run(0.2); Say("Chat while you make music", "The chat window drags anywhere over the DAW."); SetF("artView", 0); LoadTracksKeep(); SetF("selTrack", 0); M("OpenChat", idMia); Run(2.4);
        var hp = new PointF(W - 400 - 24 + 150, 86 + 16); Move(hp, 1.0); rippleAt = Now; ripplePt = hp; Ev("OnMouseDown", MouseButtons.Left, 1, (int)hp.X, (int)hp.Y); for (int i = 1; i <= 22; i++) { Ev("OnMouseMove", MouseButtons.Left, 0, (int)hp.X - i * 26, (int)hp.Y + i * 11); cur = new PointF(hp.X - i * 26, hp.Y + i * 11); Run(0.035); } Ev("OnMouseUp", MouseButtons.Left, 1, (int)cur.X, (int)cur.Y); Run(2.4); SetF("chatOpen", false); Run(0.4);
        Hush(); Run(0.2); Say("Collabs", "Invite a friend to make one track together: each of you sends a version and gets the other one."); SetF("tab", 7); M("OpenArtist", idKali); WaitUntil(() => !Target("INVITE TO COLLAB").IsEmpty, 8.0); Run(1.4); var ic = Target("INVITE TO COLLAB"); if (!ic.IsEmpty) Move(Centre(ic), 1.0); Run(2.4);
        SetF("artSel", null); SetF("artView", 2); M("FetchCollabs", true); Run(5.0); Hush(); Run(0.3); SetF("artView", 0); curShow = false;
    }
    static void LoadTracksKeep() { if (eng.Tracks.Count == 0) LoadTracks(); SetF("tab", 0); }
    static void ProjectsScene2() { }
    static void ThemesScene()
    {
        Scene(11, "themes"); SetF("tab", 0); Say("Themes that change everything", "16 themes, your own colours from the colour mixer, and a Rainbow that never stops moving."); M("OpenThemeSheet", false); Run(3.0);
        for (int i = 1; i <= 3; i++) { Themes.Apply(i * 2); Run(1.6); } Themes.Apply(0); SetF("themeOpen", false); Run(0.5); Hush();
    }

}
