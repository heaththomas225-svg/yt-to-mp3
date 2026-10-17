using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

// User settings that need to be reachable from every window. Saved in the app's settings file.
static class Prefs
{
    public static string BgPath = "";        // image or video used as the background ("" = animated leaves only)
    public static int BgDim = 45;            // 0..90, how much the background is darkened so the controls stay readable
    public static bool BgLeaves = true;      // keep the floating leaves on top of a custom background
    public static bool BgStudio = true;      // also use the background (still) in Purphex Music
    public static int RainbowSpeed = 15;     // how fast the Rainbow theme fades through the colours, 0 (very slow) to 100
    public static int OutVol = 100;          // Purphex Music playback volume, percent (does not change exported files)
    public static int OutDev = -1;           // winmm device id (-1 = system default)
    public static int Buf = 1;               // playback buffer: 0 low latency, 1 normal, 2 safe (YOUR choice: it is saved and never changed behind your back)
    public static int BufBoost = 0;          // extra size the studio adds for this session only when the sound breaks up (never saved)
    public static int BufEff { get { return Math.Min(2, Buf + BufBoost); } }
    public static int FpsCap = 60;           // animation frame rate cap: 30, 60, 120, 144, 165 or 240
    public static bool PerfMode;             // PERFORMANCE MODE: almost no animation and 30 fps, for slow PCs
    public static int FpsEff { get { return PerfMode ? 240 : FpsCap; } }   // PERFORMANCE MODE takes the effects away, not the frames: 240 (the screen still decides how many are shown)
    public static bool ShowFps { get { return true; } set { } }      // the frame rate is always shown in the title bar (there is no switch for it any more)
    public static string MidiDev = "";       // the MIDI keyboard Bass Lab listens to ("" = off)
    public static int MicMode = 0;           // the MIC button's mode: 0 Automatic, 1 Standard, 2 Voice Isolation, 3 Wide Spectrum (only for what is recorded)
    public static string FontId = "";        // the studio font chosen on the Fonts page ("" = the original, Bahnschrift)
    public static int VisMotion = 1;   // the visualisers: 0 CALM, 1 SMOOTH (the default), 2 LIVELY, 3 WILD (how wild they move)
    public static bool ThinTracks;   // THIN TRACKS is remembered (the tracks open thin next time)
    public static bool HeadHidden;   // the list of tracks on the left is tucked away (a thin strip and an arrow bring it back)
    public static bool PanelHidden;   // the plugin panel on the right is tucked away (a thin tab brings it back)
    public static float LyricSize = 15f;   // the lyrics page text size (A- / A+)
    public static string InDev = "";         // recording input (microphone), "" = the Windows default
    public static string Theme = "Purphex";   // the colour theme (Settings and the Theme tab)
    public static int FantasyThemeV;   // which version of the "Fantasy theme on the DAW" has been applied on this PC (it is applied again when the number goes up)
    public static bool FantasyThemed;   // the Fantasy edition puts the Fantasy theme (Royal Gold) on the whole program the first time it is on this PC; after that the theme is yours to change
    public static bool CountUses = true;     // every finished conversion adds one to the worldwide TIMES USED number (only a number is sent; Settings > General turns it off)
    public static bool SaveLyrics = true;    // after a song is converted its lyrics are looked up and saved as a .txt next to it (Settings > General turns it off)
    public static bool ShareSaved = true;    // a preset you save is also shared with the Community presets: this is always on (community presets are for everybody)
    public static Action Changed = delegate { };

    public static string[] Lines()
    {
        return new[] { "panelhid=" + (PanelHidden ? 1 : 0), "thin=" + (ThinTracks ? 1 : 0), "vismotion=" + VisMotion, "headhid=" + (HeadHidden ? 1 : 0), "lyrsize=" + LyricSize.ToString(System.Globalization.CultureInfo.InvariantCulture), "bgpath=" + BgPath, "bgdim=" + BgDim, "bgleaves=" + BgLeaves, "bgstudio=" + BgStudio, "outvol=" + OutVol, "rbspeed=" + RainbowSpeed, "outdev=" + OutDev, "buf=" + Buf, "fpscap=" + FpsCap, "perfmode=" + PerfMode, "showfps=" + ShowFps, "savelyrics=" + SaveLyrics, "countuses=" + CountUses, "fantasythemev=" + FantasyThemeV, "fantasytheme=" + FantasyThemed, "midiin=" + MidiDev, "indev=" + InDev, "micmode=" + MicMode, "fontid=" + FontId, "sharesaved=" + ShareSaved, "theme=" + Theme };
    }
    public static bool Parse(string k, string v)
    {
        int n; bool b;
        switch (k)
        {
            case "bgpath": return true;   // (no more picture or video backgrounds)
            case "bgdim": if (int.TryParse(v, out n)) BgDim = Math.Max(0, Math.Min(90, n)); return true;
            case "bgleaves": if (bool.TryParse(v, out b)) BgLeaves = b; return true;
            case "bgstudio": if (bool.TryParse(v, out b)) BgStudio = b; return true;
            case "outvol": if (int.TryParse(v, out n)) OutVol = Math.Max(0, Math.Min(100, n)); return true;
            case "rbspeed": if (int.TryParse(v, out n)) RainbowSpeed = Math.Max(0, Math.Min(100, n)); return true;
            case "outdev": if (int.TryParse(v, out n)) OutDev = n; return true;
            case "buf": if (int.TryParse(v, out n)) Buf = Math.Max(0, Math.Min(2, n)); return true;
            case "fpscap": if (int.TryParse(v, out n)) FpsCap = n >= 240 ? 240 : (n >= 165 ? 165 : (n >= 144 ? 144 : (n >= 120 ? 120 : (n >= 60 ? 60 : 30)))); return true;
            case "perfmode": if (bool.TryParse(v, out b)) PerfMode = b; return true;
            case "showfps": if (bool.TryParse(v, out b)) ShowFps = b; return true;
            case "midiin": MidiDev = v; return true;
            case "panelhid": PanelHidden = v.Trim() == "1"; return true;
            case "thin": ThinTracks = v.Trim() == "1"; return true;
            case "vismotion": if (int.TryParse(v, out n)) VisMotion = Math.Max(0, Math.Min(3, n)); return true;
            case "headhid": HeadHidden = v.Trim() == "1"; return true;
            case "lyrsize": { float ls; if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ls)) LyricSize = Math.Max(9f, Math.Min(40f, ls)); return true; }
            case "indev": InDev = v; return true;
            case "fontid": { string fv = v ?? ""; bool okId = fv.Length <= 40; foreach (char fc in fv) if (!((fc >= 'a' && fc <= 'z') || (fc >= '0' && fc <= '9') || fc == '-')) okId = false; FontId = okId ? fv : ""; return true; }
            case "micmode": if (int.TryParse(v, out n) && n >= 0 && n <= 3) MicMode = n; return true;
            case "savelyrics": if (bool.TryParse(v, out b)) SaveLyrics = b; return true;
            case "countuses": if (bool.TryParse(v, out b)) CountUses = b; return true;
            case "fantasythemev": { int n2; if (int.TryParse(v, out n2)) FantasyThemeV = n2; return true; }
            case "fantasytheme": if (bool.TryParse(v, out b)) FantasyThemed = b; return true;
            case "sharesaved": ShareSaved = true; return true;   // (always on)
            case "theme": Theme = v; Themes.ApplyByName(v); return true;
        }
        return false;
    }
}

// Custom background: a picture is cropped to fill the window ("cover"); a video is decoded by ffmpeg, cropped the same way, and looped.
static class UserBackground
{
    static Bitmap srcBmp; static string srcStamp = ""; static readonly object srcLock = new object();   // decoded picture (or first frame of a video), shared by every window size
    static void Dim(Bitmap b) { if (Prefs.BgDim <= 0) return; using (var g = Graphics.FromImage(b)) using (var br = new SolidBrush(Color.FromArgb(Prefs.BgDim * 255 / 100, 4, 1, 12))) g.FillRectangle(br, 0, 0, b.Width, b.Height); }
    public static string Key(int w, int h) { return Prefs.BgPath + "|" + Prefs.BgDim + "|" + w + "x" + h + "|" + Version; }
    static Bitmap frame; static readonly object fl = new object();   // newest video frame
    static Process proc; static Thread reader; static volatile bool run; static int vidW, vidH, pendW, pendH; static long pendAt, ffMissAt; static string vidPath = "";
    public static volatile int Version;  // bumps whenever the picture or video changes (other windows rebuild their cache)
    public static Func<string> Ffmpeg = delegate { return null; };
    public static string Error = "";

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
    public static bool IsImage(string p) { return Array.IndexOf(ImgExt, Path.GetExtension(p ?? "").ToLowerInvariant()) >= 0; }
    static long actChecked; static string actPath = ""; static volatile bool actVal; static int actBusy;
    // The file check for a new path is immediate; re-checks of the same path run on the pool so a sleeping network drive never stalls the UI thread.
    public static bool Active { get { return false; } }   // (no picture or video backgrounds any more: the leaves, or a plain dark background)
    public static bool IsVideo { get { return Active && !IsImage(Prefs.BgPath); } }

    // Cropped-to-fill copy of an image, scaled to w x h.
    static Bitmap Cover(Image src, int w, int h)
    {
        var bm = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        double sc = Math.Max(w / (double)src.Width, h / (double)src.Height); float sw = (float)(w / sc), sh = (float)(h / sc);
        float sx = (src.Width - sw) / 2f, sy = (src.Height - sh) / 2f;
        using (var g = Graphics.FromImage(bm)) { g.Clear(Color.Black); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(src, new RectangleF(0, 0, w, h), new RectangleF(sx, sy, sw, sh), GraphicsUnit.Pixel); }
        return bm;
    }

    // A private, darkened copy for the background layer (caller owns it).
    public static Bitmap Baked(int w, int h)
    {
        var b = Fresh(w, h); if (b != null) Dim(b); return b;
    }

    // A private copy (caller disposes), used by other windows so they never disturb the main window's cache.
    public static Bitmap Fresh(int w, int h)
    {
        if (!Active || w < 8 || h < 8) return null;
        try
        {
            string path = Prefs.BgPath;
            lock (srcLock) { var s = Source(path); return s == null ? null : Cover(s, w, h); }
        }
        catch (Exception ex) { Error = ex.Message; return null; }
    }

    // The decoded picture (capped at 3840 px), cached per file and modified time. Call with srcLock held.
    static Bitmap Source(string path)
    {
        string stamp = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
        if (srcBmp != null && srcStamp == stamp) return srcBmp;
        if (srcBmp != null) { srcBmp.Dispose(); srcBmp = null; } srcStamp = "";
        string file = path; bool tmp = false;
        if (!IsImage(path)) { file = FirstFrame(path); if (file == null) return null; tmp = true; }
        try
        {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false))
            {
                int mx = Math.Max(im.Width, im.Height); double k = mx > 3840 ? 3840.0 / mx : 1.0;
                int sw = Math.Max(1, (int)(im.Width * k)), sh = Math.Max(1, (int)(im.Height * k));
                var bm = new Bitmap(sw, sh, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bm)) { g.Clear(Color.Black); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, new Rectangle(0, 0, sw, sh)); }
                srcBmp = bm; srcStamp = stamp; return bm;
            }
        }
        finally { if (tmp) { try { File.Delete(file); } catch { } } }
    }

    static string FirstFrame(string video)
    {
        string ff = Ffmpeg(); if (ff == null) return null;
        string o = Path.Combine(Path.GetTempPath(), "ytmp3_bg_" + Guid.NewGuid().ToString("N") + ".png");
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -i \"" + video + "\" -frames:v 1 \"" + o + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } try { File.Delete(o); } catch { } return null; }
        return File.Exists(o) ? o : null;
    }

    // ---- video ----
    // Starts (or restarts, if the size changed) the looping decoder. The video is scaled and cropped by ffmpeg to the same aspect as the window.
    public static void EnsureVideo(int w, int h)
    {
        if (!IsVideo) { StopVideo(); return; }
        // decode at no more than ~960 px wide; the picture is stretched to the window, which is fine behind a dimmed overlay
        double k = Math.Min(1.0, Math.Min(1600.0 / Math.Max(1, w), 900.0 / Math.Max(1, h))); int dw = Math.Max(16, ((int)(w * k)) & ~1), dh = Math.Max(16, ((int)(h * k)) & ~1);
        if (run && dw == vidW && dh == vidH && vidPath == Prefs.BgPath) { pendW = 0; return; }
        // a size change while running: keep drawing the current frame (stretched) and restart only once the size has settled
        if (run && vidPath == Prefs.BgPath)
        {
            long nowT = Environment.TickCount;
            if (dw != pendW || dh != pendH) { pendW = dw; pendH = dh; pendAt = nowT; return; }
            if (nowT - pendAt < 300) return;
        }
        long nowF = Environment.TickCount; if (ffMissAt != 0 && nowF - ffMissAt < 5000) { StopVideo(); return; }
        string ff = Ffmpeg(); if (ff == null) { ffMissAt = nowF == 0 ? 1 : nowF; StopVideo(); return; } ffMissAt = 0;
        StopVideo(); vidW = dw; vidH = dh; vidPath = Prefs.BgPath; pendW = 0;
        try
        {
            var psi = new ProcessStartInfo(ff, "-loglevel error -stream_loop -1 -re -i \"" + vidPath + "\" -an -vf \"scale=" + dw + ":" + dh + ":force_original_aspect_ratio=increase,crop=" + dw + ":" + dh + ",fps=24,format=bgra\" -f rawvideo -pix_fmt bgra pipe:1")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            proc = Process.Start(psi); var pr = proc; run = true;
            pr.ErrorDataReceived += (s, e) => { };
            ThreadPool.QueueUserWorkItem(_ => { try { pr.StandardError.ReadToEnd(); } catch { } });
            int fw = dw, fh = dh;
            reader = new Thread(() => ReadLoop(pr, fw, fh)) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; reader.Start();
        }
        catch (Exception ex) { Error = ex.Message; run = false; }
    }

    static void ReadLoop(Process pr, int w, int h)
    {
        int len = w * h * 4; var buf = new byte[len];
        try
        {
            var st = pr.StandardOutput.BaseStream;
            while (run && proc == pr)
            {
                int got = 0; while (got < len) { int n = st.Read(buf, got, len - got); if (n <= 0) return; got += n; }
                var bm = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                var bd = bm.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                System.Runtime.InteropServices.Marshal.Copy(buf, 0, bd.Scan0, len); bm.UnlockBits(bd);
                Dim(bm);   // darken here, off the UI thread, so the Darken slider needs no decoder restart
                lock (fl) { var old = frame; frame = bm; if (old != null) old.Dispose(); }
            }
        }
        catch { }
    }

    public static void StopVideo()
    {
        run = false;
        var p = proc; proc = null;
        if (p != null) { try { if (!p.HasExited) p.Kill(); } catch { } try { p.Dispose(); } catch { } }
        lock (fl) { if (frame != null) { frame.Dispose(); frame = null; } }
    }

    // UI thread, never blocks: draws the newest (already darkened) video frame. Returns false until the first frame has arrived.
    public static bool DrawVideo(Graphics g, int w, int h)
    {
        if (!IsVideo) return false;
        EnsureVideo(w, h);
        lock (fl)
        {
            if (frame == null) return false;
            if (frame.Width == w && frame.Height == h) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(frame, 0, 0); g.CompositingMode = CompositingMode.SourceOver; }
            else { g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(frame, 0, 0, w, h); }
            return true;
        }
    }

    public static void Set(string path)
    {
        StopVideo();
        if (string.IsNullOrEmpty(path) && Monitor.TryEnter(srcLock)) { try { if (srcBmp != null) { srcBmp.Dispose(); srcBmp = null; srcStamp = ""; } } finally { Monitor.Exit(srcLock); } }   // free the cached picture when the background is removed
        Prefs.BgPath = path ?? ""; Version++; Prefs.Changed();
    }
}
