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
    public static int OutVol = 100;          // Purphex Music playback volume, percent (does not change exported files)
    public static int OutDev = -1;           // winmm device id (-1 = system default)
    public static int Buf = 1;               // playback buffer: 0 low latency, 1 normal, 2 safe
    public static int FpsCap = 60;           // animation frame rate cap: 30 or 60
    public static bool ShowFps = false;      // show the frame rate in the title bar
    public static string MidiDev = "";       // the MIDI keyboard Bass Lab listens to ("" = off)
    public static string InDev = "";         // recording input (microphone), "" = the Windows default
    public static Action Changed = delegate { };

    public static string[] Lines()
    {
        return new[] { "bgpath=" + BgPath, "bgdim=" + BgDim, "bgleaves=" + BgLeaves, "bgstudio=" + BgStudio, "outvol=" + OutVol, "outdev=" + OutDev, "buf=" + Buf, "fpscap=" + FpsCap, "showfps=" + ShowFps, "midiin=" + MidiDev, "indev=" + InDev };
    }
    public static bool Parse(string k, string v)
    {
        int n; bool b;
        switch (k)
        {
            case "bgpath": BgPath = v; return true;
            case "bgdim": if (int.TryParse(v, out n)) BgDim = Math.Max(0, Math.Min(90, n)); return true;
            case "bgleaves": if (bool.TryParse(v, out b)) BgLeaves = b; return true;
            case "bgstudio": if (bool.TryParse(v, out b)) BgStudio = b; return true;
            case "outvol": if (int.TryParse(v, out n)) OutVol = Math.Max(0, Math.Min(100, n)); return true;
            case "outdev": if (int.TryParse(v, out n)) OutDev = n; return true;
            case "buf": if (int.TryParse(v, out n)) Buf = Math.Max(0, Math.Min(2, n)); return true;
            case "fpscap": if (int.TryParse(v, out n)) FpsCap = n >= 120 ? 120 : (n >= 60 ? 60 : 30); return true;
            case "showfps": if (bool.TryParse(v, out b)) ShowFps = b; return true;
            case "midiin": MidiDev = v; return true;
            case "indev": InDev = v; return true;
        }
        return false;
    }
}

// Custom background: a picture is cropped to fill the window ("cover"); a video is decoded by ffmpeg, cropped the same way, and looped.
static class UserBackground
{
    static Bitmap still;                 // picture, or the first frame of a video, cropped to stillW x stillH
    static int stillW, stillH, stillDim; static string stillPath = "";
    static void Dim(Bitmap b) { if (Prefs.BgDim <= 0) return; using (var g = Graphics.FromImage(b)) using (var br = new SolidBrush(Color.FromArgb(Prefs.BgDim * 255 / 100, 4, 1, 12))) g.FillRectangle(br, 0, 0, b.Width, b.Height); }
    public static string Key(int w, int h) { return Prefs.BgPath + "|" + Prefs.BgDim + "|" + w + "x" + h + "|" + Version; }
    static Bitmap frame; static readonly object fl = new object();   // newest video frame
    static Process proc; static Thread reader; static volatile bool run; static int vidW, vidH, vidDim; static string vidPath = "";
    public static volatile int Version;  // bumps whenever the picture or video changes (other windows rebuild their cache)
    public static Func<string> Ffmpeg = delegate { return null; };
    public static string Error = "";

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
    public static bool IsImage(string p) { return Array.IndexOf(ImgExt, Path.GetExtension(p ?? "").ToLowerInvariant()) >= 0; }
    static long actChecked; static string actPath = ""; static bool actVal;
    public static bool Active { get { string p = Prefs.BgPath; if (p.Length == 0) return false; long now = Environment.TickCount; if (p != actPath || now - actChecked > 1500) { actPath = p; actChecked = now; actVal = File.Exists(p); } return actVal; } }
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
            string src = Prefs.BgPath; if (!IsImage(src)) src = FirstFrame(src); if (src == null) return null;
            using (var fs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false)) return Cover(im, w, h);
        }
        catch (Exception ex) { Error = ex.Message; return null; }
    }

    // A still picture for this window size (null when there is no custom background). Cached per size.
    public static Bitmap Still(int w, int h)
    {
        if (!Active || w < 8 || h < 8) return null;
        lock (fl)
        {
            if (still != null && stillW == w && stillH == h && stillPath == Prefs.BgPath && stillDim == Prefs.BgDim) return still;
            try
            {
                string src = Prefs.BgPath;
                if (!IsImage(src)) src = FirstFrame(src);
                if (src == null) return null;
                using (var fs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false))
                { var nb = Cover(im, w, h); Dim(nb); if (still != null) still.Dispose(); still = nb; }
                stillW = w; stillH = h; stillPath = Prefs.BgPath; stillDim = Prefs.BgDim; return still;
            }
            catch (Exception ex) { Error = ex.Message; return null; }
        }
    }

    static string FirstFrame(string video)
    {
        string ff = Ffmpeg(); if (ff == null) return null;
        string o = Path.Combine(Path.GetTempPath(), "ytmp3_bg_first.png");
        try { if (File.Exists(o)) File.Delete(o); } catch { }
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -i \"" + video + "\" -frames:v 1 \"" + o + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return null; }
        return File.Exists(o) ? o : null;
    }

    // ---- video ----
    // Starts (or restarts, if the size changed) the looping decoder. The video is scaled and cropped by ffmpeg to the same aspect as the window.
    public static void EnsureVideo(int w, int h)
    {
        if (!IsVideo || Ffmpeg() == null) { StopVideo(); return; }
        // decode at no more than ~960 px wide; the picture is stretched to the window, which is fine behind a dimmed overlay
        double k = Math.Min(1.0, Math.Min(1600.0 / Math.Max(1, w), 900.0 / Math.Max(1, h))); int dw = Math.Max(16, ((int)(w * k)) & ~1), dh = Math.Max(16, ((int)(h * k)) & ~1);
        if (run && dw == vidW && dh == vidH && vidPath == Prefs.BgPath && vidDim == Prefs.BgDim) return;
        StopVideo(); vidW = dw; vidH = dh; vidPath = Prefs.BgPath; vidDim = Prefs.BgDim; double mul = 1.0 - Prefs.BgDim / 100.0 * 0.95;
        try
        {
            var psi = new ProcessStartInfo(Ffmpeg(), "-loglevel error -stream_loop -1 -re -i \"" + vidPath + "\" -an -vf \"scale=" + dw + ":" + dh + ":force_original_aspect_ratio=increase,crop=" + dw + ":" + dh + ",fps=24,format=bgra,lutrgb=r=val*" + mul.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ":g=val*" + mul.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ":b=val*" + mul.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\" -f rawvideo -pix_fmt bgra pipe:1")
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
        StopVideo(); lock (fl) { still = null; stillPath = ""; }
        Prefs.BgPath = path ?? ""; Version++; Prefs.Changed();
    }
}
