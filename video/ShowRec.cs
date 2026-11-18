using System; using System.Collections.Generic; using System.Diagnostics; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging; using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices; using System.Windows.Forms;
// THE PURPHEX MUSIC VIDEO (the one at the top of the website): a showcase of the DAW, then HOW TO DOWNLOAD in a box over the blurred studio, then a full
// tour and tutorial of every part, with a subtitle saying what is being shown. It drives the real Purphex Music studio and records each part, frame by frame,
// at 1920x1080 / 30 fps (straight into ffmpeg); video/assemble.js joins the parts with transitions and the lo-fi soundtrack (video/LofiComposer.cs).
// To make the video again (with new features): add or change a Part below, then run video/make.sh. Nothing of this PC (names, folders) is ever shown.
//   csc /main:SR ... video/ShowRec.cs      run: ShowRec.exe <out folder> [part names...]
static class SR
{
    const int W = 1440, H = 810, FPS = 30;   // the studio is drawn at 1440x810 and scaled to 1920x1080 (everything 1.33x bigger, easy to read in a video)
    static StudioForm sf; static App app; static StudioEngine eng; const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;
    static string outDir, ffmpeg, webShots; static Process enc; static Stream encIn; static int frames; static string partName;
    static PointF cur = new PointF(1100, 700); static bool hover; static float clickGlow; static PointF clickAt; static bool cursorOn = true;
    static int OW = 1920, OH = 1080; static float SC = 1920f / W; static Bitmap frame;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    delegate bool EnumProc(IntPtr h, IntPtr l); [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l); [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

    static object F(string n) { var f = typeof(StudioForm).GetField(n, BF); if (f != null) return f.GetValue(sf); return typeof(StudioForm).GetProperty(n, BF).GetValue(sf, null); }
    static void SetF(string n, object v) { var f = typeof(StudioForm).GetField(n, BF); if (f != null) f.SetValue(sf, v); else typeof(StudioForm).GetProperty(n, BF).SetValue(sf, v, null); }
    static object M(string n, params object[] a) { var ms = typeof(StudioForm).GetMethods(BF).Where(m => m.Name == n && m.GetParameters().Length == a.Length).ToArray(); if (ms.Length == 0) { Console.WriteLine("  (no method " + n + ")"); return null; } return ms[0].Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }

    // ---------- recording ----------
    static void Begin(string name)
    {
        partName = name; frames = 0; string path = Path.Combine(outDir, name + ".mp4"); subText = ""; subAt = -99;
        var psi = new ProcessStartInfo(ffmpeg, "-v error -y -f rawvideo -pix_fmt bgra -s " + OW + "x" + OH + " -r " + FPS + " -i - -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -r " + FPS + " \"" + path + "\"") { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
        enc = Process.Start(psi); try { enc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { } encIn = enc.StandardInput.BaseStream; Console.WriteLine("part " + name);
    }
    static void End() { encIn.Close(); enc.WaitForExit(); Console.WriteLine("  " + partName + ": " + frames + " frames (" + (frames / (double)FPS).ToString("0.0") + " s)"); overlay = null; perFrameHook = null; Sub(""); }
    static readonly MethodInfo onPaint = typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
    static byte[] buf;
    static Action<Graphics> overlay;          // drawn after the studio (in studio coordinates) and before the pointer: a window shown on top
    static Func<Bitmap> scene;                // when set, the whole frame comes from here instead of the studio (cards, the download demo)
    static Action perFrameHook;
    // ---------- the subtitle: what is being shown, at the bottom of the picture ----------
    static string subText = ""; static int subAt = -99;
    static void Sub(string t) { if (t == subText) return; subText = t ?? ""; subAt = frames; }
    static void DrawSub(Graphics g)
    {
        if (subText.Length == 0) return; float k = Math.Min(1f, (frames - subAt) / 7f); if (k <= 0) return;
        using (var f = new Font("Bahnschrift", 27, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            var sz = g.MeasureString(subText, f, 1500); float w = Math.Min(1560, sz.Width + 56), h = sz.Height + 20, x = (OW - w) / 2, y = OH - h - 22 + (1 - k) * 10;
            using (var p = new GraphicsPath()) { float d = 22; p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); using (var b = new SolidBrush(Color.FromArgb((int)(205 * k), 8, 6, 16))) g.FillPath(b, p); using (var pen = new Pen(Color.FromArgb((int)(90 * k), 170, 140, 255), 1.4f)) g.DrawPath(pen, p); }
            using (var b = new SolidBrush(Color.FromArgb((int)(255 * k), 245, 242, 255))) using (var sfm = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(subText, f, b, new RectangleF(x + 8, y + 10, w - 16, h - 12), sfm);
        }
    }
    static void Snap()
    {
        if (perFrameHook != null) perFrameHook(); Application.DoEvents();
        if (scene != null) { var b = scene(); using (var g = Graphics.FromImage(frame)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(b, 0, 0); } }
        else
        {
            SetF("toast", "");   // the program's own notes (about the sound card of this PC) are not part of the video
            using (var g = Graphics.FromImage(frame)) { g.Clear(Color.Black); g.ScaleTransform(SC, SC); onPaint.Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, W, H)) }); }
            using (var g = Graphics.FromImage(frame)) { g.ScaleTransform(SC, SC); g.SmoothingMode = SmoothingMode.AntiAlias; if (overlay != null) overlay(g); if (cursorOn) DrawCursor(g); }
        }
        using (var g = Graphics.FromImage(frame)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; DrawSub(g);   /* (a scene draws its own pointer, in its own place) */ }
        var bd = frame.LockBits(new Rectangle(0, 0, OW, OH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); Marshal.Copy(bd.Scan0, buf, 0, buf.Length); frame.UnlockBits(bd);
        encIn.Write(buf, 0, buf.Length); frames++; clickGlow = Math.Max(0, clickGlow - 1f / 9);
    }
    static void Hold(double sec, Action perFrame = null) { int n = (int)Math.Round(sec * FPS); for (int i = 0; i < n; i++) { if (perFrame != null) perFrame(); WaitFrame(); Snap(); } }
    static float[] tbL = new float[4096], tbR = new float[4096];
    static readonly FieldInfo playerF = typeof(StudioEngine).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
    // virtual time: the song moves on exactly one frame of sound per picture (the sound card is never used)
    static void WaitFrame() { var pl = playerF.GetValue(eng) as IDisposable; if (pl != null) { try { pl.Dispose(); } catch { } playerF.SetValue(eng, null); } if (eng.Playing) eng.RenderRealtime(tbL, tbR, StudioEngine.SR / FPS); }

    // ---------- the pointer ----------
    static double Ease(double t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; }
    static void MoveTo(float x, float y, double sec)
    {
        var a = cur; int n = Math.Max(1, (int)(sec * FPS)); float bend = (float)((y - a.Y) * 0.08);
        for (int i = 1; i <= n; i++) { double t = Ease(i / (double)n); cur = new PointF((float)(a.X + (x - a.X) * t), (float)(a.Y + (y - a.Y) * t - Math.Sin(t * Math.PI) * bend)); if (scene == null && hover) Mouse("OnMouseMove", MouseButtons.None); WaitFrame(); Snap(); }
    }
    static void MoveTo(RectangleF r, double sec) { MoveTo(r.X + r.Width / 2, r.Y + r.Height / 2, sec); }
    static void Mouse(string what, MouseButtons b, int clicks = 1) { var mi = typeof(StudioForm).GetMethod(what, BindingFlags.NonPublic | BindingFlags.Instance); mi.Invoke(sf, new object[] { new MouseEventArgs(b, b == MouseButtons.None ? 0 : clicks, (int)cur.X, (int)cur.Y, 0) }); }
    static void Click(double after = 0.5) { if (scene == null) Mouse("OnMouseDown", MouseButtons.Left); clickAt = cur; clickGlow = 1; Snap(); if (scene == null) Mouse("OnMouseUp", MouseButtons.Left); Hold(after); }
    static void Glow() { clickAt = cur; clickGlow = 1; }
    static void Wheel(int delta, int steps, double each) { for (int i = 0; i < steps; i++) { var mi = typeof(StudioForm).GetMethod("OnMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance); mi.Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, (int)cur.X, (int)cur.Y, delta) }); Hold(each); } }
    static void Drag(float x, float y, double sec)
    {
        Mouse("OnMouseDown", MouseButtons.Left); clickAt = cur; clickGlow = 1; var a = cur; int n = Math.Max(1, (int)(sec * FPS));
        for (int i = 1; i <= n; i++) { double t = Ease(i / (double)n); cur = new PointF((float)(a.X + (x - a.X) * t), (float)(a.Y + (y - a.Y) * t)); Mouse("OnMouseMove", MouseButtons.Left); WaitFrame(); Snap(); }
        Mouse("OnMouseUp", MouseButtons.Left); Hold(0.4);
    }
    // the studio draws itself once (not recorded) so the places of its buttons are those of what is on screen now
    static void Repaint() { if (scene != null) return; using (var g = Graphics.FromImage(frame)) { g.Clear(Color.Black); g.ScaleTransform(SC, SC); onPaint.Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, W, H)) }); } }
    static RectangleF Btn(string label) { Repaint(); var m = (Dictionary<string, RectangleF>)F("btnMap"); RectangleF r; bool exact = label.EndsWith("="); if (exact) label = label.Substring(0, label.Length - 1); if (m.TryGetValue(label, out r)) return r; if (exact) return RectangleF.Empty; foreach (var kv in m) if (kv.Key.StartsWith(label, StringComparison.OrdinalIgnoreCase)) return kv.Value; return RectangleF.Empty; }
    static bool ClickBtn(string label, double travel = 0.8, double after = 0.6) { Snap(); var r = Btn(label); if (r.IsEmpty) { Console.WriteLine("  (no button " + label + ")"); return false; } MoveTo(r, travel); Hold(0.12); Click(after); return true; }
    static bool HoverBtn(string label, double travel, double stay) { var r = Btn(label); if (r.IsEmpty) { Console.WriteLine("  (no button " + label + ")"); return false; } MoveTo(r, travel); Hold(stay); return true; }
    static void DrawCursor(Graphics g)
    {
        if (clickGlow > 0) { float rr = 10 + (1 - clickGlow) * 26; using (var p = new Pen(Color.FromArgb((int)(170 * clickGlow), 200, 150, 255), 3f)) g.DrawEllipse(p, clickAt.X - rr, clickAt.Y - rr, rr * 2, rr * 2); }
        var pts = new[] { new PointF(0, 0), new PointF(0, 26), new PointF(6.5f, 20), new PointF(11, 30), new PointF(15, 28), new PointF(10.5f, 18.5f), new PointF(19, 18.5f) };
        var sh = pts.Select(p => new PointF(cur.X + p.X + 2, cur.Y + p.Y + 3)).ToArray(); var pp = pts.Select(p => new PointF(cur.X + p.X, cur.Y + p.Y)).ToArray();
        using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPolygon(b, sh); using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, pp); using (var p = new Pen(Color.FromArgb(255, 20, 12, 30), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, pp);
    }

    // ---------- the session every studio part starts from: a lo-fi song made of Purphex's own sounds ----------
    static float[][] Tile(string path, int bars, double bpm) { var c = AudioClip.FromFile(path); int sr = StudioEngine.SR; int want = (int)(bars * 4 * 60.0 / bpm * sr); var l = new float[want]; var r = new float[want]; int n = c.L.Length; for (int i = 0; i < want; i++) { l[i] = c.L[i % n]; r[i] = c.R[i % n]; } return new[] { l, r }; }
    static Track Loop(string name, string role, string file, int bars, int fromBar, int color) { double bpm = 75; var a = Tile(file, bars, bpm); var t = StudioEngine.NewTrack(name, role, AudioClip.FromArrays(a[0], a[1], name)); t.Offset = (long)(fromBar * 4 * 60.0 / bpm * StudioEngine.SR); t.Color = color; t.BpmChecked = true; t.Bpm = 75; return t; }
    static Fx AddFx(Track t, string type) { var f = FxRegistry.Create(type); if (f == null) return null; f.Prepare(StudioEngine.SR); var ins = f as InstrumentFx; if (ins != null) ins.Owner = t; lock (eng.Sync) t.Fx.Add(f); return f; }
    static Track InstTrack(string name) { var t = StudioEngine.NewTrack(name, "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFFFF9A3D); lock (eng.Sync) eng.Tracks.Add(t); SetF("selTrack", eng.Tracks.Count - 1); return t; }
    static string LofiDir { get { return Path.Combine(StudioEnv.DataDir, "sounds", "jazzy-lofi"); } }
    static void Session()
    {
        try { eng.Stop(); } catch { } eng.Playing = false; lock (eng.Sync) eng.Tracks.Clear(); Fx.ProjectBpm = 75; SetF("projName", "Late Night Session"); SetF("bpmUserSet", true); SetF("toast", "");
        string p = LofiDir;
        var drums = Loop("Drums", "beat", Path.Combine(p, "Drum Loops", "Drum Loop 002 (busy hats) 75 BPM.wav"), 24, 0, unchecked((int)0xFF8E5CFF));
        var keys = Loop("Piano Chords", "audio", Path.Combine(p, "Music Loops", "Grand Piano Chords 002 75 BPM A min.wav"), 24, 0, unchecked((int)0xFFFF5CC8));
        var bass = Loop("Sub Bass", "audio", Path.Combine(p, "Bass Loops", "Sub Bass Loop 013 75 BPM A min.wav"), 20, 4, unchecked((int)0xFF3DD6FF));
        var chops = Loop("Vocal Chops", "vocal", Path.Combine(p, "Music Loops", "Vocal Chop Loop 002 75 BPM A min.wav"), 16, 8, unchecked((int)0xFFFFB13D));
        var top = Loop("Top Loop", "beat", Path.Combine(p, "Drum Loops", "Top Loop 001 75 BPM.wav"), 16, 8, unchecked((int)0xFF5CFFA8));
        foreach (var t in new[] { drums, keys, bass, chops, top }) lock (eng.Sync) eng.Tracks.Add(t);
        AddFx(drums, "comp"); AddFx(drums, "sat"); AddFx(keys, "eq"); AddFx(keys, "reverb"); AddFx(bass, "comp"); AddFx(chops, "eq"); AddFx(chops, "delay"); AddFx(chops, "reverb"); AddFx(top, "eq");
        SetF("selTrack", 1); SetF("tab", 0); try { sf.CloseInspector(); } catch { } foreach (var nm in new[] { "soundsOpen", "browserOpen", "stylesOpen", "fontsWin", "menuOpen", "themeOpen", "welcomeOn" }) { try { SetF(nm, false); } catch { } }
        try { ((System.Collections.IList)F("fxWins")).Clear(); } catch { } try { ((System.Collections.IList)F("toolWins")).Clear(); } catch { } CloseExtraForms();
        foreach (var nm in new[] { "prOpen", "blOn", "flexOpen", "blPlaying", "pickOpen", "mmOpen" }) { try { SetF(nm, false); } catch { } } try { SetF("hintNow", ""); SetF("bubble", ""); } catch { } hover = false; cur = new PointF(1100, 700); Mouse("OnMouseMove", MouseButtons.None);
        Prefs.PanelHidden = false; SetF("autoFit", true); SetF("toast", ""); DefaultLook(); sf.Invalidate(); Pump(100);
    }
    static void DefaultLook() { try { Themes.Apply(Themes.IndexOf("Purphex")); Prefs.ProfileTheme = false; ProfileStore.Accent = -1; M("ApplyProfileLook"); SetF("revertUntil", DateTime.MinValue); } catch { } }
    static void Play(long fromFrame) { lock (eng.Sync) eng.Pos = fromFrame; eng.Playing = true; }
    static void Stop() { eng.Playing = false; }
    static long BarF(double bar) { return (long)(bar * 4 * 60.0 / 75 * StudioEngine.SR); }

    // ---------- windows of their own (export, settings, the Purphex VSTs, Bass Lab...): drawn over the studio as they look on screen ----------
    static Bitmap FormShot(Form f) { var b = new Bitmap(f.Width, f.Height); f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height)); return b; }
    static void HidePaths(Control root) { foreach (Control c in root.Controls) { if (c.Text != null && c.Text.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) >= 0) c.Text = @"C:\Music\Purphex"; HidePaths(c); } }
    static void ShowWindow(Bitmap b, float scale) { overlay = g => { float w = b.Width * scale, h = b.Height * scale, x = (W - w) / 2, y = (H - h) / 2; using (var sh = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(sh, x + 8, y + 12, w, h); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(b, x, y, w, h); }; }
    static RectangleF WindowRect(Bitmap b, float scale) { float w = b.Width * scale, h = b.Height * scale; return new RectangleF((W - w) / 2, (H - h) / 2, w, h); }
    static List<Form> ExtraForms() { var l = new List<Form>(); foreach (Form f in Application.OpenForms) if (f != sf && f != app && f.Visible && f.Width > 50) l.Add(f); return l; }
    static void CloseExtraForms() { foreach (var f in ExtraForms()) { try { f.Close(); } catch { } } }
    static float FitScale(Bitmap b) { return Math.Min(1.0f, Math.Min((W - 80f) / b.Width, (H - 80f) / b.Height)); }
    // the newest window the program opened, live: redrawn every few frames so its meters and animations move
    static void LiveWindow(Form f, double sec, Action<int> each = null)
    {
        f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5200, -5200); f.ShowInTaskbar = false; HidePaths(f); Bitmap b = FormShot(f); float s = FitScale(b); ShowWindow(b, s);
        int n = (int)(sec * FPS); for (int i = 0; i < n; i++) { if (each != null) each(i); if (i % 3 == 0) { var nb = FormShot(f); var old = b; b = nb; ShowWindow(b, s); old.Dispose(); } WaitFrame(); Snap(); }
        overlay = null;
    }
    // the menu Windows shows on a right-click (a pop-up of its own): found, pictured and closed
    static Bitmap GrabPopupMenu()
    {
        uint me = (uint)Process.GetCurrentProcess().Id; ToolStripDropDown found = null;
        EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == me && IsWindowVisible(h)) { var c = Control.FromHandle(h) as ToolStripDropDown; if (c != null) found = c; } return true; }, IntPtr.Zero);
        if (found == null) return null; var b = new Bitmap(found.Width, found.Height); found.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); found.Close(); return b;
    }

    // ---------- cards (the title, a section title, the end) ----------
    static Bitmap logo; static Bitmap Logo() { if (logo == null) { try { using (var s = typeof(SR).Assembly.GetManifestResourceStream("Purphex.logo.png")) logo = new Bitmap(s); } catch { logo = new Bitmap(10, 10); } } return logo; }
    static Bitmap cardBmp;
    static Bitmap Card(string big, string small, double t)
    {
        if (cardBmp == null) cardBmp = new Bitmap(OW, OH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cardBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, OW, OH), Color.FromArgb(255, 14, 6, 34), Color.FromArgb(255, 3, 2, 9), 70f)) g.FillRectangle(br, 0, 0, OW, OH);
            // a slow, thin line of sound across the card
            var pts = new List<PointF>(); for (int x = 0; x <= OW; x += 6) pts.Add(new PointF(x, OH * 0.70f + (float)(Math.Sin(x * 0.012 + t * 1.6) * 18 * Math.Sin(x * 0.0021 + t * 0.4))));
            using (var pen = new Pen(Color.FromArgb(70, 160, 120, 255), 1.4f)) g.DrawLines(pen, pts.ToArray());
            float k = (float)Math.Min(1, t / 0.7); var lg = Logo(); float lh = 250 * (0.94f + 0.06f * k), lw = lh * lg.Width / Math.Max(1, lg.Height);
            var cm = new ColorMatrix { Matrix33 = k }; using (var ia = new ImageAttributes()) { ia.SetColorMatrix(cm); g.DrawImage(lg, new Rectangle((int)((OW - lw) / 2), (int)(OH * 0.22f), (int)lw, (int)lh), 0, 0, lg.Width, lg.Height, GraphicsUnit.Pixel, ia); }
            using (var f1 = new Font("Bahnschrift", 76, FontStyle.Bold, GraphicsUnit.Pixel)) using (var f2 = new Font("Bahnschrift SemiLight", 34, FontStyle.Regular, GraphicsUnit.Pixel)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center })
            { using (var b = new SolidBrush(Color.FromArgb((int)(255 * k), 255, 255, 255))) g.DrawString(big, f1, b, new RectangleF(0, OH * 0.52f, OW, 110), sfm); using (var b = new SolidBrush(Color.FromArgb((int)(230 * k), 190, 175, 240))) g.DrawString(small, f2, b, new RectangleF(0, OH * 0.52f + 104, OW, 120), sfm); }
        }
        return cardBmp;
    }
    static void CardPart(string name, string big, string small, double sec) { Begin(name); bool keepC = cursorOn; cursorOn = false; int f0 = frames; scene = () => Card(big, small, (frames - f0) / (double)FPS); Hold(sec); scene = null; cursorOn = keepC; End(); }

    [STAThread] static int Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : @"D:\PurphexTests\video\parts"; Directory.CreateDirectory(outDir); var only = new HashSet<string>(args.Skip(1));
        webShots = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outDir)), "web");
        frame = new Bitmap(OW, OH, PixelFormat.Format32bppArgb); buf = new byte[OW * OH * 4];
        ffmpeg = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "YtToMp3", "tools", "ffmpeg", "ffmpeg-master-latest-win64-gpl", "bin", "ffmpeg.exe");
        // the sound packs of this PC are used for the song (read only); everything the program writes goes to a folder of the video's own
        string tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outDir)), "appdata"); Directory.CreateDirectory(tmp);   /* (next to the parts: the video's own data folder, never the real one) */
        StudioEnv.DataDir = tmp; if (!Directory.Exists(Path.Combine(tmp, "sounds", "jazzy-lofi"))) { Console.WriteLine("the jazzy-lofi pack is needed in " + tmp + "\\sounds (copy it from the app's sounds folder)"); return 1; }
        Application.EnableVisualStyles(); Edition.Override = Tier.Fantasy; Prefs.OutVol = 0; CommunityPresets.BaseUrl = "http://127.0.0.1:1";
        app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; ProjectLibrary.ProfileName = "Neo Wolf";
        sf = new StudioForm(app); sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(-4000, -4000); sf.ShowInTaskbar = false; sf.Show(); sf.ClientSize = new Size(W, H); Pump(600);
        foreach (var p in Parts) { if (only.Count > 0 && !only.Contains(p.Key)) continue; try { p.Value(); } catch (Exception e) { var ie = e.InnerException ?? e; Console.WriteLine("  FAILED " + p.Key + ": " + ie.GetType().Name + ": " + ie.Message + " @ " + (ie.StackTrace ?? "").Split('\n')[0]); try { End(); } catch { } scene = null; overlay = null; } }
        try { eng.Stop(); } catch { } sf.Close(); Console.WriteLine("=== parts done"); return 0;
    }
    static readonly List<KeyValuePair<string, Action>> Parts = new List<KeyValuePair<string, Action>>();
    static void Part(string name, Action a) { Parts.Add(new KeyValuePair<string, Action>(name, a)); }

    static SR()
    {
        Part("probe", Probe);
        Part("intro", () => CardPart("intro", "PURPHEX MUSIC", "Create. Convert. Master.", 4.5));
        ShowcaseParts(); DownloadParts(); TourParts();
        Part("outro", () => CardPart("outro", "PURPHEX MUSIC", "Download it free at purphexmusic.com", 6.0));
    }
    // writes the labels of every button on the main pages (to find what a part can press)
    static void Probe()
    {
        Session(); cursorOn = false; Begin("probe"); Hold(0.3);
        foreach (var t in new[] { 0, 1, 2, 9 }) { SetF("tab", t); Hold(0.2); var bm = (Dictionary<string, RectangleF>)F("btnMap"); Console.WriteLine("PAGE " + t + ": " + string.Join(" | ", bm.Keys.Where(k => k.Length > 1 && k.Length < 34).OrderBy(k => k))); }
        SetF("tab", 0); SetF("menuOpen", true); Hold(0.2); Console.WriteLine("MENU: " + string.Join(" | ", ((Dictionary<string, RectangleF>)F("btnMap")).Keys.Where(k => k.Length > 1 && k.Length < 34))); SetF("menuOpen", false);
        End(); cursorOn = true;
    }

    // =====================================================================================================================================
    // 1. THE SHOWCASE: what Purphex Music looks like, quick cuts while the song plays (no words needed beyond a line each)
    static void ShowcaseParts()
    {
        Part("sc-studio", () =>
        {
            Session(); cur = new PointF(1120, 640); Begin("sc-studio"); Play(BarF(8)); Sub("Purphex Music: a full studio for making music on your PC");
            Hold(1.0); MoveTo(680, 390, 1.4); Hold(0.8); Wheel(-120, 3, 0.3); Hold(0.6); Wheel(120, 3, 0.3); Hold(0.8); End(); Stop();
        });
        Part("sc-mixer", () => { Session(); cur = new PointF(900, 400); Begin("sc-mixer"); Play(BarF(10)); Sub("Mix with a real mixing desk"); ClickBtn("MIXER TOP", 0.6, 0.4); Hold(3.0); End(); Stop(); });
        Part("sc-master", () => { Session(); SetF("tab", 1); cursorOn = false; Begin("sc-master"); Play(BarF(10)); Sub("Master your song, and watch it move"); Hold(3.0); SetF("tab", 5); Hold(3.5); End(); Stop(); SetF("tab", 0); cursorOn = true; });
        Part("sc-basslab", () =>
        {
            Session(); var keys = InstTrack("Keys"); var bl = AddFx(keys, "synth"); M("OpenFxWindow", keys, bl); Pump(300); var f = ExtraForms().LastOrDefault();
            cursorOn = false; Begin("sc-basslab"); Sub("Bass Lab, the Purphex synthesizer"); Play(BarF(10)); var names = new[] { "Wobble Bass", "Dark Reese", "Shimmer Pad", "Slow Wub" }; if (f != null) LiveWindow(f, 4.0, i => { if (i % 30 == 0) { try { SynthPresets.Apply((SynthFx)bl, names[(i / 30) % names.Length]); } catch { } } }); else Hold(4.0); End(); Stop(); CloseExtraForms(); cursorOn = true;
        });
    }

    // =====================================================================================================================================
    // 2. HOW TO DOWNLOAD: a box over the blurred, paused studio (the website, Save, the Downloads folder, a double-click, Purphex opening)
    static Bitmap Blur(Bitmap src, int div) { var small = new Bitmap(Math.Max(1, src.Width / div), Math.Max(1, src.Height / div)); using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(src, 0, 0, small.Width, small.Height); } var o = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(o)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(small, 0, 0, o.Width, o.Height); } small.Dispose(); return o; }
    static Bitmap StudioFrame() { using (var g = Graphics.FromImage(frame)) { g.Clear(Color.Black); g.ScaleTransform(SC, SC); onPaint.Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, W, H)) }); } return (Bitmap)frame.Clone(); }
    static Font UiF(float px, bool bold = false) { return new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
    static void RR(Graphics g, Brush b, Pen p, RectangleF r, float d) { using (var pa = new GraphicsPath()) { pa.AddArc(r.X, r.Y, d, d, 180, 90); pa.AddArc(r.Right - d, r.Y, d, d, 270, 90); pa.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); pa.AddArc(r.X, r.Bottom - d, d, d, 90, 90); pa.CloseFigure(); if (b != null) g.FillPath(b, pa); if (p != null) g.DrawPath(p, pa); } }
    // the inside of the box: a Windows-like desktop at 1600x900, made up (no names of this PC anywhere)
    const int BW = 1600, BH = 900; static Bitmap boxBmp = new Bitmap(BW, BH, PixelFormat.Format32bppArgb);
    static Bitmap webDl;   // the real download page of purphexmusic.com (a picture taken by video/make.sh)
    static RectangleF webDlBtn = new RectangleF(372, 530, 230, 50);   // where its Download button is in that picture (make.sh writes the real place to web/download.btn)
    static void DrawBrowser(Graphics g, float dl)
    {
        g.Clear(Color.FromArgb(255, 32, 32, 36));
        using (var b = new SolidBrush(Color.FromArgb(255, 44, 44, 50))) g.FillRectangle(b, 0, 0, BW, 46); RR(g, new SolidBrush(Color.FromArgb(255, 60, 60, 68)), null, new RectangleF(14, 8, 300, 32), 10);
        using (var f = UiF(15)) using (var b = new SolidBrush(Color.White)) g.DrawString("Download - Purphex Music", f, b, 30, 14);
        RR(g, new SolidBrush(Color.FromArgb(255, 24, 24, 28)), null, new RectangleF(120, 54, BW - 240, 34), 17); using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 220, 220, 230))) g.DrawString("purphexmusic.com/download", f, b, 144, 61);
        if (webDl != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(webDl, 0, 96, BW, BH - 96); }
        else using (var f = UiF(40, true)) using (var b = new SolidBrush(Color.White)) g.DrawString("purphexmusic.com", f, b, 600, 400);
        if (dl > 0)
        {   // the browser's download bubble
            var r = new RectangleF(BW - 430, 100, 400, 110); RR(g, new SolidBrush(Color.FromArgb(250, 40, 40, 46)), new Pen(Color.FromArgb(255, 70, 70, 80)), r, 12);
            using (var f = UiF(17, true)) using (var b = new SolidBrush(Color.White)) g.DrawString("Purphex Music.exe", f, b, r.X + 62, r.Y + 22); DrawPIcon(g, new RectangleF(r.X + 16, r.Y + 22, 34, 34));
            using (var f = UiF(14)) using (var b = new SolidBrush(Color.FromArgb(255, 180, 180, 190))) g.DrawString(dl >= 1 ? "Done  -  Open file" : (dl * 64).ToString("0") + " MB of 64 MB", f, b, r.X + 62, r.Y + 50);
            using (var b = new SolidBrush(Color.FromArgb(255, 70, 70, 80))) g.FillRectangle(b, r.X + 62, r.Y + 80, 310, 5); using (var b = new SolidBrush(Color.FromArgb(255, 120, 160, 255))) g.FillRectangle(b, r.X + 62, r.Y + 80, 310 * Math.Min(1, dl), 5);
        }
    }
    static void DrawPIcon(Graphics g, RectangleF r) { var lg = Logo(); g.InterpolationMode = InterpolationMode.HighQualityBicubic; float s = Math.Min(r.Width / lg.Width, r.Height / lg.Height) * 1.0f; float w = lg.Width * s * 0.9f, h = lg.Height * s * 0.9f; RR(g, new SolidBrush(Color.FromArgb(255, 20, 10, 40)), null, r, 8); g.DrawImage(lg, r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h); }
    // the Save As window of Windows: an empty Downloads folder, the file name and a Save button
    static RectangleF saveBtn = new RectangleF(1110, 690, 130, 40);
    static void DrawSaveAs(Graphics g)
    {
        using (var sh = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(sh, 0, 0, BW, BH);
        var r = new RectangleF(340, 150, 920, 600); RR(g, new SolidBrush(Color.FromArgb(255, 32, 32, 36)), new Pen(Color.FromArgb(255, 64, 64, 72)), r, 10);
        using (var f = UiF(15)) using (var b = new SolidBrush(Color.White)) g.DrawString("Save As", f, b, r.X + 16, r.Y + 12);
        using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 200, 200, 210))) g.DrawString("\u2190   \u2192   \u2191      This PC  \u203A  Downloads", f, b, r.X + 16, r.Y + 50);
        using (var b = new SolidBrush(Color.FromArgb(255, 26, 26, 30))) g.FillRectangle(b, r.X + 1, r.Y + 86, 200, 420);
        string[] nav = { "Home", "Desktop", "Downloads", "Documents", "Music", "Pictures" }; for (int i = 0; i < nav.Length; i++) { if (nav[i] == "Downloads") using (var b = new SolidBrush(Color.FromArgb(255, 52, 52, 60))) g.FillRectangle(b, r.X + 6, r.Y + 96 + i * 34, 188, 30); using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 220, 220, 228))) g.DrawString(nav[i], f, b, r.X + 20, r.Y + 101 + i * 34); }
        using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 150, 150, 160))) g.DrawString("This folder is empty.", f, b, r.X + 480, r.Y + 270);
        using (var b = new SolidBrush(Color.FromArgb(255, 40, 40, 46))) g.FillRectangle(b, r.X + 1, r.Y + 506, r.Width - 2, 93);
        using (var f = UiF(14)) using (var b = new SolidBrush(Color.FromArgb(255, 200, 200, 210))) { g.DrawString("File name:", f, b, r.X + 30, r.Y + 520); g.DrawString("Save as type:", f, b, r.X + 30, r.Y + 556); }
        RR(g, new SolidBrush(Color.FromArgb(255, 26, 26, 30)), new Pen(Color.FromArgb(255, 80, 80, 90)), new RectangleF(r.X + 140, r.Y + 514, 600, 28), 4); RR(g, new SolidBrush(Color.FromArgb(255, 26, 26, 30)), new Pen(Color.FromArgb(255, 80, 80, 90)), new RectangleF(r.X + 140, r.Y + 550, 600, 28), 4);
        using (var f = UiF(14)) using (var b = new SolidBrush(Color.White)) { g.DrawString("Purphex Music.exe", f, b, r.X + 150, r.Y + 519); g.DrawString("Application (*.exe)", f, b, r.X + 150, r.Y + 555); }
        saveBtn = new RectangleF(r.Right - 270, r.Bottom - 50, 120, 34); RR(g, new SolidBrush(Color.FromArgb(255, 96, 140, 255)), null, saveBtn, 4); using (var f = UiF(14)) using (var b = new SolidBrush(Color.Black)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString("Save", f, b, saveBtn, sfm);
        var cb = new RectangleF(r.Right - 140, r.Bottom - 50, 120, 34); RR(g, new SolidBrush(Color.FromArgb(255, 60, 60, 68)), null, cb, 4); using (var f = UiF(14)) using (var b = new SolidBrush(Color.White)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString("Cancel", f, b, cb, sfm);
    }
    // File Explorer, Downloads: only Purphex Music.exe is in it
    static RectangleF exeIcon = new RectangleF(330, 230, 110, 130);
    static void DrawExplorer(Graphics g, bool picked)
    {
        g.Clear(Color.FromArgb(255, 24, 24, 28));
        using (var b = new SolidBrush(Color.FromArgb(255, 36, 36, 42))) g.FillRectangle(b, 0, 0, BW, 92);
        using (var f = UiF(16)) using (var b = new SolidBrush(Color.White)) g.DrawString("Downloads", f, b, 24, 14);
        RR(g, new SolidBrush(Color.FromArgb(255, 28, 28, 32)), null, new RectangleF(140, 50, 900, 32), 6); using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 210, 210, 220))) g.DrawString("This PC  \u203A  Downloads", f, b, 156, 56);
        using (var b = new SolidBrush(Color.FromArgb(255, 30, 30, 34))) g.FillRectangle(b, 0, 92, 230, BH - 92);
        string[] nav = { "Home", "Desktop", "Downloads", "Documents", "Music", "Pictures", "This PC" }; for (int i = 0; i < nav.Length; i++) { if (nav[i] == "Downloads") using (var b = new SolidBrush(Color.FromArgb(255, 52, 52, 60))) g.FillRectangle(b, 8, 108 + i * 36, 214, 32); using (var f = UiF(15)) using (var b = new SolidBrush(Color.FromArgb(255, 220, 220, 228))) g.DrawString(nav[i], f, b, 24, 114 + i * 36); }
        if (picked) RR(g, new SolidBrush(Color.FromArgb(255, 50, 60, 90)), new Pen(Color.FromArgb(255, 90, 120, 200)), RectangleF.Inflate(exeIcon, 8, 6), 6);
        DrawPIcon(g, new RectangleF(exeIcon.X + 15, exeIcon.Y, 80, 80)); using (var f = UiF(14)) using (var b = new SolidBrush(Color.White)) using (var sfm = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString("Purphex Music.exe", f, b, new RectangleF(exeIcon.X - 20, exeIcon.Y + 88, exeIcon.Width + 40, 40), sfm);
        using (var f = UiF(13)) using (var b = new SolidBrush(Color.FromArgb(255, 150, 150, 160))) g.DrawString("1 item", f, b, 244, BH - 28);
    }
    static void DrawLoading(Graphics g, double k)
    {
        using (var br = new LinearGradientBrush(new Rectangle(0, 0, BW, BH), Color.FromArgb(255, 18, 8, 40), Color.FromArgb(255, 4, 2, 10), 70f)) g.FillRectangle(br, 0, 0, BW, BH);
        var lg = Logo(); float lh = 220, lw = lh * lg.Width / Math.Max(1, lg.Height); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(lg, (BW - lw) / 2, 230, lw, lh);
        using (var f = UiF(22)) using (var b = new SolidBrush(Color.FromArgb(255, 200, 190, 240))) using (var sfm = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString("Opening Purphex Music...", f, b, new RectangleF(0, 500, BW, 40), sfm);
        RR(g, new SolidBrush(Color.FromArgb(255, 40, 30, 70)), null, new RectangleF(BW / 2 - 260, 560, 520, 8), 4); RR(g, new SolidBrush(Color.FromArgb(255, 170, 110, 255)), null, new RectangleF(BW / 2 - 260, 560, (float)(520 * Math.Max(0.02, Math.Min(1, k))), 8), 4);
    }
    static Bitmap bgSharp, bgBlur; static float boxScale; static Action<Graphics> boxDraw; static float blurK;
    static Bitmap insertBmp;
    // the frame: the studio behind (blurred by blurK and darkened), and the box in front (grown by boxScale) holding boxDraw
    static Bitmap InsertFrame()
    {
        if (insertBmp == null) insertBmp = new Bitmap(OW, OH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(insertBmp))
        {
            g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(bgSharp, 0, 0); g.CompositingMode = CompositingMode.SourceOver;
            if (blurK > 0) { var cm = new ColorMatrix { Matrix33 = blurK }; using (var ia = new ImageAttributes()) { ia.SetColorMatrix(cm); g.DrawImage(bgBlur, new Rectangle(0, 0, OW, OH), 0, 0, OW, OH, GraphicsUnit.Pixel, ia); } using (var b = new SolidBrush(Color.FromArgb((int)(120 * blurK), 0, 0, 0))) g.FillRectangle(b, 0, 0, OW, OH); }
            if (boxScale > 0.01f && boxDraw != null)
            {
                using (var bg = Graphics.FromImage(boxBmp)) { bg.SmoothingMode = SmoothingMode.AntiAlias; bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; boxDraw(bg); if (cursorOn) { var keep = cur; bg.ResetTransform(); DrawCursorAt(bg, keep); } }
                float bw = 1440 * boxScale, bh = 810 * boxScale, x = (OW - bw) / 2, y = (OH - bh) / 2 - 20;
                var r = new RectangleF(x, y, bw, bh); for (int i = 6; i >= 1; i--) using (var b = new SolidBrush(Color.FromArgb(14, 0, 0, 0))) g.FillRectangle(b, RectangleF.Inflate(r, i * 5, i * 5));
                using (var pa = new GraphicsPath()) { float d = 26 * boxScale; pa.AddArc(r.X, r.Y, d, d, 180, 90); pa.AddArc(r.Right - d, r.Y, d, d, 270, 90); pa.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); pa.AddArc(r.X, r.Bottom - d, d, d, 90, 90); pa.CloseFigure();
                    var st = g.Save(); g.SetClip(pa); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(boxBmp, r); g.Restore(st); using (var pen = new Pen(Color.FromArgb(180, 170, 130, 255), 2f)) g.DrawPath(pen, pa); }
                using (var f = new Font("Bahnschrift", 30, FontStyle.Bold, GraphicsUnit.Pixel)) using (var b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, boxScale)), 255, 255, 255))) g.DrawString("HOW TO DOWNLOAD", f, b, x + 4, y - 44);
            }
        }
        return insertBmp;
    }
    static void DrawCursorAt(Graphics g, PointF p) { var keep = cur; cur = p; DrawCursor(g); cur = keep; }
    static void Animate(double sec, Action<double> k) { int n = Math.Max(1, (int)(sec * FPS)); for (int i = 1; i <= n; i++) { k(Ease(i / (double)n)); Snap(); } }
    static void BoxMove(float x, float y, double sec) { var a = cur; int n = Math.Max(1, (int)(sec * FPS)); for (int i = 1; i <= n; i++) { double t = Ease(i / (double)n); cur = new PointF((float)(a.X + (x - a.X) * t), (float)(a.Y + (y - a.Y) * t)); Snap(); } }
    static void BoxClick(double after) { Glow(); Snap(); Hold(after); }
    static void DownloadParts()
    {
        Part("download", () =>
        {
            try { webDl = new Bitmap(Path.Combine(webShots, "download.png")); var btn = File.ReadAllText(Path.Combine(webShots, "download.btn")).Split(','); webDlBtn = new RectangleF(float.Parse(btn[0]), float.Parse(btn[1]) + 96, float.Parse(btn[2]), float.Parse(btn[3])); } catch (Exception e) { Console.WriteLine("  (no picture of the website: " + e.Message + ")"); }
            Session(); Play(BarF(9)); for (int i = 0; i < 8; i++) WaitFrame(); Stop(); bgSharp = StudioFrame(); bgBlur = Blur(bgSharp, 14); blurK = 0; boxScale = 0; boxDraw = null;
            Begin("download"); bool keepC = cursorOn; scene = InsertFrame; cursorOn = false;
            Animate(0.7, k => blurK = (float)k); Sub("How to download Purphex Music");
            boxDraw = g => DrawBrowser(g, 0); cursorOn = true; cur = new PointF(1200, 300); Animate(0.6, k => boxScale = (float)k);
            Sub("Go to purphexmusic.com and press Download"); Hold(0.8); BoxMove(webDlBtn.X + webDlBtn.Width / 2, webDlBtn.Y + webDlBtn.Height / 2, 1.0); Hold(0.3); BoxClick(0.3);
            Sub("Choose where to save it (your Downloads folder is fine), then press Save"); boxDraw = g => { DrawBrowser(g, 0); DrawSaveAs(g); }; Hold(1.2); BoxMove(saveBtn.X + saveBtn.Width / 2, saveBtn.Y + saveBtn.Height / 2, 1.0); Hold(0.3); BoxClick(0.2);
            double dl = 0; boxDraw = g => DrawBrowser(g, (float)dl); Sub("It downloads in a few seconds"); Animate(1.6, k => dl = k); Hold(0.8);
            Sub("Open your Downloads folder and double-click Purphex Music"); bool picked = false; boxDraw = g => DrawExplorer(g, picked); cur = new PointF(900, 500); Hold(0.8); BoxMove(exeIcon.X + 55, exeIcon.Y + 40, 1.0); Hold(0.2); picked = true; BoxClick(0.12); BoxClick(0.4);
            double ld = 0; boxDraw = g => DrawLoading(g, ld); cursorOn = false; Sub("Purphex Music opens"); Animate(1.8, k => ld = k); Hold(0.4);
            Animate(0.6, k => boxScale = (float)(1 - k)); boxDraw = null; Animate(0.7, k => blurK = (float)(1 - k)); Sub(""); scene = null; cursorOn = keepC; End();
        });
    }

    // =====================================================================================================================================
    // 3. THE TOUR: every part of the DAW, one after the other, each with what it does underneath
    static void TourParts()
    {
        // projects: the welcome box, your projects, a new project
        Part("t-projects", () =>
        {
            Session(); string d0 = Path.Combine(StudioEnv.DataDir, "projects"); Directory.CreateDirectory(d0); ProjectLibrary.Entries.Clear(); string[] names = { "Late Night Session", "Skyline", "Rain On Glass", "Sunday Loop", "Bass In The Park" };
            for (int i = 0; i < names.Length; i++) { string pth = Path.Combine(d0, names[i] + ".ytmproj"); File.WriteAllText(pth, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = pth, Name = names[i], Tracks = 4 + i, Bpm = 75 + i * 10, Seconds = 150 + i * 20, Updated = DateTime.Now.AddHours(-i * 5), Opened = DateTime.Now.AddHours(-i * 5) }); }
            lock (eng.Sync) eng.Tracks.Clear(); SetF("projectsLoaded", true); sf.ShowWelcome(); Repaint(); cur = new PointF(900, 500); Begin("t-projects"); Sub("When Purphex opens, pick a project or start a new one"); Hold(2.2);
            if (!ClickBtn("ALL MY PROJECTS", 0.9, 0.8)) M("GoTab", 3); Sub("My Projects: every song you made, with its picture"); Hold(1.2); MoveTo(560, 420, 1.0); Hold(0.8); MoveTo(980, 420, 1.0); Hold(0.8);
            Sub("Search, sort, favourites, and the recycling bin"); HoverBtn("SORT", 0.8, 0.8); HoverBtn("RECYCLING BIN", 0.9, 0.8);
            Sub("The first card starts a new project"); MoveTo(140, 420, 1.0); Hold(1.2); End(); SetF("tab", 0); SetF("welcomeOn", false);
        });
        // importing music and trimming it, and the right-click menu of a track
        Part("t-import", () =>
        {
            Session(); cur = new PointF(900, 500); Begin("t-import"); Sub("ADD MUSIC brings in a song or a sound from your PC (MP3, WAV, FLAC and more)"); HoverBtn("ADD MUSIC", 0.9, 1.6);
            lock (eng.Sync) eng.Tracks.RemoveRange(3, eng.Tracks.Count - 3); SetF("autoFit", true); var song = Loop("My Song", "song", Path.Combine(LofiDir, "Music Loops", "Grand Piano Chords 002 75 BPM A min.wav"), 12, 2, unchecked((int)0xFF9B5CFF)); lock (eng.Sync) eng.Tracks.Add(song); SetF("selTrack", eng.Tracks.Count - 1); SetF("autoFit", true); Glow(); Hold(1.4);
            Sub("It lands on a track of its own"); Hold(1.0);
            // the right-click menu of the new track (Windows' own menu, pictured)
            var lanes = sf.ClientSize; float laneY = 0; try { var mixH = (int)F("MixH"); } catch { }
            float ty = 0; { var rows = (List<RectangleF>)null; } float rowY = Convert.ToSingle(M("RowTopOf", song) ?? 0f);
            Sub("Right-click a track for everything you can do to it: split, cut, fade, gain, trim the silence, save it as a WAV...");
            float px = 760, py = rowY > 0 ? rowY + 40 : 560; MoveTo(px, py, 0.9); Hold(0.3); Glow();
            Bitmap menu = null; try { M("LaneMenu", song); Pump(150); menu = GrabPopupMenu(); } catch (Exception e) { Console.WriteLine("  (menu: " + e.Message + ")"); }
            if (menu != null) { var m0 = menu; overlay = g => { float s = Math.Min(1f, (H - 150f) / m0.Height); float mx = Math.Min(px, W - m0.Width * s - 10), my = 60; using (var sh = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(sh, mx + 6, my + 8, m0.Width * s, m0.Height * s); g.DrawImage(m0, mx, my, m0.Width * s, m0.Height * s); }; } Hold(3.6); overlay = null;
            Sub("Trim the silence at the start of a sound in one click"); try { M("TrimLeadingSilence", song); } catch { } Glow(); Hold(1.2);
            Sub("Or drag the end of a clip to trim it by hand"); Repaint(); float ex = 0; try { ex = ((Dictionary<Track, float>)F("clipRightX"))[song]; } catch { } if (ex > 300) { hover = true; MoveTo(ex - 3, py, 0.8); Hold(0.4); Drag(ex - 180, py, 1.2); hover = false; } else { try { lock (eng.Sync) song.ClipFrames = (long)(song.UsedLength * 0.7); } catch { } } Hold(1.4); End();
        });
        // every button of the toolbars, one after the other, with what it does
        Part("t-toolbar", () =>
        {
            Session(); cur = new PointF(700, 300); Begin("t-toolbar"); Sub("Every button says what it does when you point at it"); Hold(1.0);
            string[] order = { "ADD MUSIC", "SPLIT VOCALS + BEAT", "AUTO MIX", "DETECT TRACK KEY", "UNDO MIX", "THIN TRACKS", "SAVE AS", "SAVE", "OPEN", "FULLSCREEN", "HELP", "EXPORT", "MIDI REC", "MIDI MAP", "MIC", "DETECT BPM", "COUNT IN", "METRONOME", "GRID SIZE", "GAIN", "LOCK", "FLEX", "DEL", "UNDO", "REDO", "FIT", "NEW", "PERFORMANCE" };
            var extra = new Dictionary<string, string> { { "FULLSCREEN", "the studio fills the whole screen" }, { "HELP", "guides for every part of Purphex" }, { "SAVE", "saves your project" }, { "SAVE AS", "saves it under a new name" }, { "OPEN", "opens a project or a song" }, { "NEW", "starts a new project" }, { "FIT", "the whole song fits the screen" }, { "UNDO", "takes back the last change" }, { "REDO", "puts it back again" } };
            hover = true; foreach (var b in order) { var r = Btn(b); if (r.IsEmpty) continue; SetF("hintNow", ""); MoveTo(r, 0.32); Repaint(); var hint = (string)F("hintNow"); string ex2; if (string.IsNullOrEmpty(hint) && extra.TryGetValue(b, out ex2)) hint = ex2; Sub(b.Replace("=", "") + (string.IsNullOrEmpty(hint) ? "" : ": " + Shorten(hint))); Hold(0.9); } hover = false;
            End();
        });
        // the mixer and the effects on a track
        Part("t-mixer", () =>
        {
            Session(); cur = new PointF(900, 400); Begin("t-mixer"); Play(BarF(9)); Sub("The mixer: a channel for every track, with its level, pan and effects"); Hold(0.4);
            if (!ClickBtn("MIXER TOP", 0.9, 1.0)) sf.OpenInspector(1); Hold(1.6);
            Sub("Every track has its own effects: EQ, compressor, reverb, delay and 250+ more"); var keys = eng.Tracks[1]; var eq = keys.Fx.FirstOrDefault(f => f.Type == "eq"); if (eq != null) M("OpenFxWindow", keys, eq); Hold(2.6); MoveTo(700, 360, 1.0); Hold(1.2);
            Sub("+ ADD EFFECT opens the whole list, sorted into groups"); M("OpenBrowser", false, 1); Hold(2.2); Wheel(-120, 4, 0.35); Hold(0.8); SetF("browserOpen", false); try { ((System.Collections.IList)F("fxWins")).Clear(); } catch { }
            End(); Stop();
        });
        // vocal plugins and vocal presets
        Part("t-vocals", () =>
        {
            Session(); SetF("selTrack", 3); cur = new PointF(900, 400); Begin("t-vocals"); Play(BarF(9)); Sub("Vocal plugins: pitch correction, de-esser, doubler, vocal EQ and more");
            var ch = eng.Tracks[3]; var pc = AddFx(ch, "pitch"); if (pc != null) M("OpenFxWindow", ch, pc); Hold(3.0); try { ((System.Collections.IList)F("fxWins")).Clear(); } catch { }
            Sub("Vocal presets: a whole vocal chain in one click"); try { M("OpenStyles"); } catch { } Hold(2.6); MoveTo(W / 2, H * 0.55f, 0.8); Wheel(-120, 3, 0.4); Hold(1.2); SetF("stylesOpen", false); End(); Stop();
        });
        // the VSTs: every one, quickly; then how a sound is put on a VST (Bass Lab), and the same on the others
        Part("t-vsts", () =>
        {
            Session(); var keys = eng.Tracks[1]; SetF("selTrack", 1); cur = new PointF(900, 500); Begin("t-vsts"); Sub("VSTS in the right panel: Bass Lab, the Purphex VSTs and your own VST plugins");
            if (!ClickBtn("VSTS", 0.9, 1.0)) M("OpenVsts"); Hold(1.0); var list = new List<FxRegistry.Info>(); try { list = (List<FxRegistry.Info>)M("BrowserList"); } catch { } SetF("browserOpen", false);
            Sub("Every Purphex VST, one after the other"); int shown = 0;
            foreach (var info in list)
            {
                if (info.Type == "synth") continue; var f = AddFx(keys, info.Type); if (f == null) continue; int before = ExtraForms().Count; M("OpenFxWindow", keys, f); Pump(120);
                Sub(info.Name); var nf = ExtraForms(); if (nf.Count > before) { LiveWindow(nf.Last(), 0.8); CloseExtraForms(); } else { Hold(0.8); try { ((System.Collections.IList)F("fxWins")).Clear(); } catch { } }
                lock (eng.Sync) keys.Fx.Remove(f); shown++;
            }
            Console.WriteLine("  VSTs shown: " + shown); End();
        });
        Part("t-assign", () =>
        {
            Session(); var keys = InstTrack("Keys"); cur = new PointF(900, 500); Begin("t-assign");
            Func<string, string> snd = (word) => Directory.GetFiles(LofiDir, "*.wav", SearchOption.AllDirectories).FirstOrDefault(x => Path.GetFileName(x).ToLower().Contains(word)) ?? Directory.GetFiles(LofiDir, "*.wav", SearchOption.AllDirectories).First();
            Sub("Putting your own sound on a VST: open it, then drop a sound on it (or pick one from the library)");
            var smp = (SamplerFx)AddFx(keys, "sampler"); M("OpenFxWindow", keys, smp); Pump(250); var f = ExtraForms().LastOrDefault(); if (f != null) LiveWindow(f, 1.6); else Hold(1.6);
            string kick = snd("kick"); try { smp.SetSingle(kick, Path.GetFileNameWithoutExtension(kick), true, 60); } catch (Exception e) { Console.WriteLine("  (sampler: " + e.Message + ")"); }
            Sub("Now that sound plays from every key"); if (f != null) LiveWindow(f, 2.2); CloseExtraForms();
            Sub("The same on Purphex Key Map: a sound on each key you choose");
            var km = (SamplerFx)AddFx(keys, "pxkeymap"); M("OpenFxWindow", keys, km); Pump(250); f = ExtraForms().LastOrDefault();
            string[] words = { "kick", "snare", "hat", "clap" }; if (f != null) LiveWindow(f, 2.6, i => { if (i % 18 == 0 && i / 18 < words.Length) { try { var w0 = snd(words[i / 18]); km.AddToKit(w0, Path.GetFileNameWithoutExtension(w0), 48 + i / 18 * 2); } catch { } } }); CloseExtraForms();
            Sub("And on Purphex Pads: a sound on each pad");
            var pd = (SamplerFx)AddFx(keys, "pxpads"); M("OpenFxWindow", keys, pd); Pump(250); f = ExtraForms().LastOrDefault();
            if (f != null) LiveWindow(f, 2.8, i => { if (i % 14 == 0 && i / 14 < 6) { try { var w0 = snd(words[(i / 14) % 4]); pd.AddToKit(w0, Path.GetFileNameWithoutExtension(w0), 36 + i / 14); } catch { } } }); CloseExtraForms();
            End();
        });
        // Purphex Key Map and Purphex Pads (for your MIDI keyboard), MIDI MAP
        Part("t-pads", () =>
        {
            Session(); var keys = InstTrack("Keys"); cur = new PointF(900, 500); Begin("t-pads");
            Sub("Purphex Key Map: any sound on any key of your MIDI keyboard"); var km = AddFx(keys, "pxkeymap"); M("OpenFxWindow", keys, km); Pump(150); var w = ExtraForms().LastOrDefault(); if (w != null) { LiveWindow(w, 3.0); CloseExtraForms(); } else Hold(1);
            Sub("Purphex Pads: 16 pads you play from the pads of your MIDI keyboard"); var pd = AddFx(keys, "pxpads"); try { var all = Directory.GetFiles(Path.Combine(LofiDir, "Drum One-Shots"), "*.wav", SearchOption.AllDirectories); for (int q = 0; q < Math.Min(12, all.Length); q++) ((SamplerFx)pd).AddToKit(all[q], Path.GetFileNameWithoutExtension(all[q]), 36 + q); } catch { } M("OpenFxWindow", keys, pd); Pump(150); w = ExtraForms().LastOrDefault(); if (w != null) { var pads = pd as PadsFx; LiveWindow(w, 3.4, i => { if (pads != null && i % 9 == 0) { try { pads.HitAt[36 + (i / 9) % 16] = Environment.TickCount; } catch { } } }); CloseExtraForms(); } else Hold(1);
            Sub("MIDI MAP: put any knob or button of the studio on your MIDI controller"); ClickBtn("MIDI MAP", 0.9, 2.2); var mf = ExtraForms().LastOrDefault(); if (mf != null) { LiveWindow(mf, 1.6); CloseExtraForms(); } End();
        });
        // BEAT LAB: make a beat on a grid
        Part("t-beatlab", () =>
        {
            Session(); Prefs.InsH = 560; cur = new PointF(900, 400); Begin("t-beatlab"); if (!ClickBtn("BEAT LAB", 0.9, 0.4)) { try { M("OpenBeatLab"); } catch { } } Sub("BEAT LAB: make a drum beat on a grid, then drop it on your song");
            try { var pats = (System.Collections.IList)F("blPats"); var pat = pats[0]; var vel = (byte[][])pat.GetType().GetField("Vel").GetValue(pat); foreach (var st in new[] { 0, 6, 8, 11 }) vel[0][st] = 115; foreach (var st in new[] { 4, 12 }) vel[1][st] = 110; for (int st = 0; st < 16; st += 2) vel[2][st] = (byte)(st % 4 == 0 ? 90 : 60); SetF("blPlaying", true); } catch (Exception e) { Console.WriteLine("  (beat lab: " + e.Message + ")"); }
            Play(BarF(9)); MoveTo(700, 420, 1.0); Hold(1.6); MoveTo(980, 480, 1.0); Hold(1.6); End(); Stop(); try { SetF("beatLabOpen", false); } catch { } SetF("tab", 0);
        });
        // PRODUCER TAG
        Part("t-tag", () =>
        {
            Session(); cur = new PointF(900, 400); Begin("t-tag"); Sub("PRODUCER TAG: make your own producer tag, the voice that says your name at the start of a beat"); if (!ClickBtn("PRODUCER TAG", 0.9, 1.4)) { try { M("OpenProducerTag"); } catch { } }
            MoveTo(720, 430, 1.0); Hold(2.4); End(); try { M("CloseToolWins"); } catch { } SetF("tab", 0);
        });
        // Bass Lab
        Part("t-basslab", () =>
        {
            Session(); var keys = InstTrack("Keys"); var bl = AddFx(keys, "synth"); cur = new PointF(900, 500); M("OpenFxWindow", keys, bl); Pump(300); var f = ExtraForms().LastOrDefault(); Begin("t-basslab"); Play(BarF(9));
            Sub("Bass Lab: oscillators, warp, filters, wobble LFOs and a matrix, with hundreds of sounds"); var names = new[] { "Wobble Bass", "Dark Reese", "Shimmer Pad", "Slow Wub", "Growl" }; if (f != null) LiveWindow(f, 6.0, i => { if (i % 36 == 0) { try { SynthPresets.Apply((SynthFx)bl, names[(i / 36) % names.Length]); } catch { } } }); else Hold(2); End(); Stop(); CloseExtraForms();
        });
        // the piano roll
        Part("t-pianoroll", () =>
        {
            Session(); var keys = StudioEngine.NewTrack("Keys", "inst", null); keys.Instrument = true; keys.Color = unchecked((int)0xFFFF9A3D); lock (eng.Sync) eng.Tracks.Add(keys); SetF("selTrack", eng.Tracks.Count - 1); sf.Invalidate(); Pump(100);
            cur = new PointF(760, 420); Begin("t-pianoroll"); Sub("The piano roll: write notes, chords and melodies"); Hold(0.6); if (!ClickBtn("PIANO ROLL", 0.9, 1.0)) M("OpenPianoRoll", keys); ClickBtn("PIANO=", 0.8, 1.0);
            try
            {
                var grid = (RectangleF)F("prGrid"); double ppb = Convert.ToDouble(F("prPpb")); float rowH = Convert.ToSingle(F("prRowH")); float sy = Convert.ToSingle(F("prScrollY")); int hi = 120;
                Func<int, double, PointF> at = (note, beat) => new PointF((float)(grid.X + beat * ppb + 8), grid.Y + (hi - note) * rowH - sy + rowH / 2f);
                int[][] notes = { new[] { 57, 0 }, new[] { 60, 0 }, new[] { 64, 0 }, new[] { 55, 4 }, new[] { 59, 4 }, new[] { 62, 4 }, new[] { 53, 8 }, new[] { 57, 8 }, new[] { 60, 8 } };
                foreach (var nn in notes) { var pt = at(nn[0], nn[1]); if (!grid.Contains(pt)) continue; MoveTo(pt.X, pt.Y, 0.28); Click(0.1); }
            }
            catch (Exception e) { Console.WriteLine("  (piano roll: " + e.Message + ")"); }
            Sub("GENERATE makes a pattern for you, in the key of your song"); var gen = Btn("GENERATE"); if (!gen.IsEmpty) { MoveTo(gen, 0.8); Hold(0.2); Glow(); Snap(); try { SetF("prBox", M("BuildPattern", 2, 8, -1)); } catch { } Hold(2.0); }
            End(); try { M("ClosePianoRoll"); } catch { }
        });
        // Flex Pitch
        Part("t-flex", () =>
        {
            Session(); SetF("selTrack", 3); try { M("FlexOpen"); } catch (Exception e) { Console.WriteLine("  (flex: " + e.Message + ")"); } var fw = Stopwatch.StartNew(); while (fw.Elapsed.TotalSeconds < 90) { bool an = true; try { an = (bool)F("flexAnalysing"); } catch { an = false; } if (!an) break; Pump(100); }
            cur = new PointF(900, 400); Begin("t-flex"); Sub("Flex Pitch: every note of a vocal on a piano roll. Move a note to change its pitch"); MoveTo(700, 420, 1.0); Hold(1.4); MoveTo(980, 380, 1.0); Hold(1.6); End(); try { M("FlexClose", true); } catch { } try { SetF("flexOpen", false); } catch { }
        });
        // sound packs: browse, drag one onto the song
        Part("t-sounds", () =>
        {
            Session(); cur = new PointF(900, 400); Begin("t-sounds"); Sub("SOUND PACKS: drums, basses, keys, loops and one-shots in every style"); if (!ClickBtn("SOUND PACKS", 0.9, 1.2)) { try { M("OpenSounds"); } catch { } }
            MoveTo(W * 0.82f, H * 0.5f, 0.8); Wheel(-120, 3, 0.4); Hold(0.6);
            ClickBtn("SOUNDS=", 0.7, 0.8); Sub("Drag a sound from the list and drop it on the timeline"); hover = true; MoveTo(900, 425, 0.8); Hold(0.4); Drag(520, 640, 1.6); hover = false; Hold(1.4); SetF("soundsOpen", false); End();
        });
        // Music Hub
        Part("t-hub", () =>
        {
            Session(); M("OpenHub"); SetF("hubScroll", 0f); cur = new PointF(1100, 600); Begin("t-hub"); Sub("Music Hub: your music services, social apps and distribution, inside the studio"); Hold(1.0);
            Action<float, float, double> glide = (from, to, sec) => { int n = (int)(sec * FPS); for (int i = 1; i <= n; i++) { double t = Ease(i / (double)n); SetF("hubScroll", (float)(from + (to - from) * t)); WaitFrame(); Snap(); } };
            foreach (var x in new float[] { 375, 585, 795, 1000, 1200 }) { MoveTo(x, 245, 0.45); Hold(0.3); }
            Sub("YouTube, Spotify, SoundCloud and more: open one and it plays while you work"); MoveTo(1180, 520, 0.6); glide(0, 430, 2.0); Hold(0.4);
            // what you see when you click away while something plays: the studio's own little player in the bottom right corner (a Hub browser is made, no page is loaded)
            try { var okb = M("EnsureHubBrowser"); Console.WriteLine("  hub browser: " + okb + " " + F("hubErr")); } catch (Exception e) { Console.WriteLine("  (hub browser: " + e.Message + ")"); }
            SetF("hubSvc", "youtube"); SetF("npTitle", "Late Night Session"); SetF("npArtist", "Neo Wolf"); SetF("npHas", true); SetF("npPlaying", true); SetF("npDur", 184.0); SetF("npPos", 42.0); SetF("miniHidden", false);
            perFrameHook = () => { SetF("npPlaying", true); SetF("npHas", true); SetF("npTitle", "Late Night Session"); SetF("npArtist", "Neo Wolf"); SetF("miniHidden", false); };   /* (the Hub browser, with no page loaded, keeps saying nothing plays) */
            SetF("tab", 0); Sub("Click away while a song plays and its player stays in the bottom right corner"); Hold(0.6); MoveTo(W - 24 - 190, H - 90, 1.2); Hold(1.6);
            Sub("Play, pause, skip, or jump back to the Music Hub from any page"); MoveTo(W - 24 - 380 + 222 + 21, H - 90, 0.6); Hold(1.6); End(); SetF("npPlaying", false); SetF("npHas", false);
        });
        // mastering and the visualiser
        Part("t-master", () =>
        {
            Session(); cur = new PointF(900, 400); Begin("t-master"); Play(BarF(10)); Sub("MASTERING: make your song loud and clear for release"); ClickBtn("MASTERING", 0.9, 1.2);
            Sub("AUTO MASTERING, 60+ master presets, EQ, loudness, and a picture of the sound before and after"); MoveTo(700, 420, 1.0); Hold(2.4);
            Sub("The visualiser moves with your music"); SetF("tab", 5); cursorOn = false; Hold(3.4); cursorOn = true; End(); Stop(); SetF("tab", 0);
        });
        // lyrics
        Part("t-lyrics", () =>
        {
            Session(); cur = new PointF(900, 400); Begin("t-lyrics"); Sub("LYRICS: write your words next to your song (it counts syllables and finds rhymes)"); ClickBtn("LYRICS", 0.9, 0.3);
            string words = "[Verse]\nCity lights are fading slow\nRain is tapping on the glass\nPlaying back the songs we know\nLetting all the hours pass\n\n[Chorus]\nLate night, keep the record spinning\nLate night, every end's a beginning"; var box = (Control)F("lyricBox"); try { box.Text = words; } catch { }
            overlay = g => { if (box == null || !box.Visible) return; var rb = box.Bounds; using (var b = new SolidBrush(Color.FromArgb(255, 10, 5, 22))) g.FillRectangle(b, rb); using (var f2 = new Font("Segoe UI", 15, GraphicsUnit.Pixel)) using (var b = new SolidBrush(Color.FromArgb(255, 235, 228, 255))) g.DrawString(words, f2, b, new RectangleF(rb.X + 14, rb.Y + 10, rb.Width - 28, rb.Height - 20)); };
            int shownN = 0; string[] lines = words.Split('\n'); overlay = g => { if (box == null || !box.Visible) return; var rb = box.Bounds; using (var b = new SolidBrush(Color.FromArgb(255, 10, 5, 22))) g.FillRectangle(b, rb); using (var f2 = new Font("Segoe UI", 15, GraphicsUnit.Pixel)) using (var b = new SolidBrush(Color.FromArgb(255, 235, 228, 255))) g.DrawString(string.Join("\n", lines.Take(shownN)), f2, b, new RectangleF(rb.X + 14, rb.Y + 10, rb.Width - 28, rb.Height - 20)); };
            for (int q = 1; q <= lines.Length; q++) { shownN = q; try { box.Text = string.Join("\n", lines.Take(q)); } catch { } Hold(0.45); } Hold(1.4); overlay = null; End(); SetF("tab", 0);
        });
        // the side panel: every list and every page
        Part("t-sidepanel", () =>
        {
            Session(); cur = new PointF(900, 400); hover = true; Begin("t-sidepanel"); Sub("The side panel: the three lines at the top left");
            MoveTo(36, 36, 0.9); Hold(0.2); Click(0.6); SetF("menuOpen", true); Hold(0.4);
            string[] what = { "File: new, open, save, export", "Edit: undo, redo, lock, duplicate or delete a track", "Tools: the 3D sound stage, the profiler, history, the time machine and more", "Pages: every page of the studio", "View: what you see and how big", "Community: share, artists, messages" };
            for (int i = 0; i < 6; i++) { if (i == 1) continue; SetF("menuSec", i); try { var ys = (float[])F("menuYs"); MoveTo(110, ys[i] + 18, 0.5); } catch { } Sub(what[i]); Hold(1.5); }
            SetF("menuSec", -1); Sub("Music Hub, Projects, Profile, Themes, Fonts, Settings and Help are one click away"); try { var ys = (float[])F("menuYs"); for (int i = 6; i < ys.Length - 1; i++) { MoveTo(110, ys[i] + 18, 0.35); Hold(0.35); } } catch { } Hold(0.6);
            SetF("menuOpen", false); End();
        });
        // themes and the profile
        Part("t-themes", () =>
        {
            Session(); sf.OpenThemeSheet(false); cur = new PointF(700, 460); Begin("t-themes"); Sub("THEMES change the look of the whole studio");
            Action<float, float> pick = (x, y) => { MoveTo(x, y, 0.5); Hold(0.1); Click(0.05); SetF("revertUntil", DateTime.MinValue); Hold(0.8); };
            pick(690, 262); pick(930, 262); pick(690, 370); pick(457, 262); Hold(0.4); SetF("themeOpen", false); DefaultLook();
            Sub("Your profile: your picture, your banner, your songs and your colour"); SetF("tab", 6); SetF("profScroll", 0f); Hold(3.0); End(); SetF("tab", 0);
        });
        // settings (a window of its own): a quick look at every page
        Part("t-settings", () =>
        {
            Session(); var set = new SettingsForm(app); set.StartPosition = FormStartPosition.Manual; set.Location = new Point(-5600, -5600); set.ShowInTaskbar = false; set.Show(); Pump(300); HidePaths(set);
            cur = new PointF(900, 400); Begin("t-settings"); Sub("SETTINGS: your sound card, the look, your account and more"); var pages = (List<Panel>)set.GetType().GetField("pages", BF).GetValue(set);
            for (int i = 0; i < pages.Count; i++) { try { set.GetType().GetMethod("ShowPage", BF).Invoke(set, new object[] { i }); } catch { } Pump(60); HidePaths(set); Sub("Settings: " + (pages[i].Tag as string ?? "")); var b = FormShot(set); ShowWindow(b, FitScale(b)); Hold(1.3); b.Dispose(); }
            End(); set.Close();
        });
        // linking this PC (Supporter Edition or Fantasy): Settings, Account, LINK THIS PC
        Part("t-link", () =>
        {
            Session(); var set = new SettingsForm(app); set.StartPosition = FormStartPosition.Manual; set.Location = new Point(-5600, -5600); set.ShowInTaskbar = false; set.Show(); Pump(300);
            try { set.ShowAccount(); } catch { } Edition.Override = Tier.Free; try { set.GetType().GetMethod("RefreshAccount", BF).Invoke(set, null); } catch { } Pump(100); HidePaths(set);
            cur = new PointF(900, 400); Begin("t-link"); var b = FormShot(set); float s = FitScale(b); ShowWindow(b, s); Sub("Bought the Supporter or Fantasy Edition? Open Settings, then ACCOUNT"); Hold(2.0);
            var lb = (Button)set.GetType().GetField("linkBtn", BF).GetValue(set); var wr = WindowRect(b, s); var bp = lb.PointToScreen(Point.Empty); var fp = set.PointToScreen(Point.Empty); float bx = wr.X + (bp.X - set.Left + lb.Width / 2f) * s, by = wr.Y + (bp.Y - set.Top + lb.Height / 2f) * s;
            Sub("Press LINK THIS PC"); MoveTo(bx, by, 1.0); Hold(0.2); Glow(); Hold(0.6);
            Sub("The website opens: sign in, press Yes, link it"); Bitmap web = null; try { web = new Bitmap(Path.Combine(webShots, "link.png")); } catch { }
            if (web != null)
            {
                var w0 = web; float sc2 = Math.Min((W - 120f) / w0.Width, (H - 120f) / w0.Height); float ww = w0.Width * sc2, hh = w0.Height * sc2, wx = (W - ww) / 2, wy = (H - hh) / 2;
                overlay = g => { using (var sh = new SolidBrush(Color.FromArgb(140, 0, 0, 0))) g.FillRectangle(sh, 0, 0, W, H); g.DrawImage(w0, wx, wy, ww, hh); }; Hold(1.4);
                // YES, LINK IT (where the website draws it on that page)
                MoveTo(wx + 406 * sc2, wy + 325 * sc2, 1.0); Hold(0.2); Glow(); Hold(1.0);
            }
            Edition.Override = Tier.Fantasy; try { set.GetType().GetMethod("RefreshAccount", BF).Invoke(set, null); } catch { } try { ((Label)set.GetType().GetField("accSub", BF).GetValue(set)).Text = "This PC is linked to Neo Wolf. Every feature of the Fantasy Edition is on."; lb.Text = "UNLINK THIS PC"; } catch { } Pump(100); HidePaths(set); b = FormShot(set); ShowWindow(b, s);
            Sub("This PC unlocks by itself: every feature of your edition is on"); Hold(2.6); End(); set.Close();
        });
        // export
        Part("t-export", () =>
        {
            Session(); Form exportForm = null; StudioDialogs.ExportOptions(sf, "Late Night Session", 76.8, eng.Tracks.ConvertAll(t => t.Name), x => { }, f => { exportForm = f; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ShowInTaskbar = false; });
            Pump(400); if (exportForm != null) HidePaths(exportForm); cur = new PointF(1200, 700); Begin("t-export"); Sub("EXPORT: your song as a file"); ClickBtn("EXPORT", 0.9, 0.3);
            if (exportForm != null) { var shot = FormShot(exportForm); ShowWindow(shot, FitScale(shot)); Sub("WAV, MP3, FLAC or MP4; the mix, the master or every track as stems; up to 384 kHz"); MoveTo(W / 2, H / 2 + 60, 1.0); Hold(3.4); }
            End(); if (exportForm != null) exportForm.Close();
        });
        // the converter
        Part("t-converter", () =>
        {
            app.StartPosition = FormStartPosition.Manual; app.Location = new Point(-6000, -6000); app.ShowInTaskbar = false; app.Show(); Pump(400); Session(); var links = (TextBox)typeof(App).GetField("links", BF).GetValue(app); links.Text = ""; HidePaths(app);
            cur = new PointF(900, 400); Begin("t-converter"); Sub("CONVERTER: paste a link and get an MP3 or MP4, free for everyone"); HoverBtn("CONVERTER", 0.9, 0.4);
            Bitmap cb = FormShot(app); ShowWindow(cb, Math.Min(1f, (H - 120f) / cb.Height)); Hold(0.8); string wordsC = "late night lo-fi beat"; for (int q = 1; q <= wordsC.Length; q++) { links.Text = wordsC.Substring(0, q); cb = FormShot(app); ShowWindow(cb, Math.Min(1f, (H - 120f) / cb.Height)); Hold(0.07); }
            Sub("Search words work too: it finds the song for you"); Hold(2.0); End(); links.Text = ""; app.Hide(); SetF("tab", 0);
        });
    }
    static string Shorten(string s) { s = s.Replace("\r", " ").Replace("\n", " "); int i = s.IndexOf(". "); if (i > 20 && i < 110) s = s.Substring(0, i + 1); return s.Length > 120 ? s.Substring(0, 117) + "..." : s; }
}
