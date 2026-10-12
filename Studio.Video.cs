using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// MUSIC VIDEOS. A project can have a music video: any file (any size, any shape: wide, square, tall) is turned into a web video (MP4, never more than 100 MB) and goes up with the song.
// Playing a song that has one asks "watch the video too?"; the video then plays in a small player above the strip along the bottom (where the logo rides), in time with the song,
// with a FULL SCREEN button that fits the picture to any screen without stretching it (black bars where the shapes differ).
static class VideoRecs
{
    static readonly object lk = new object(); static readonly HashSet<string> pids = new HashSet<string>(); static bool loaded;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "communityvideos.txt"); } }
    static void Load() { if (loaded) return; loaded = true; try { if (File.Exists(FileP)) foreach (var l in File.ReadAllLines(FileP)) if (l.Trim().Length > 0) pids.Add(l.Trim()); } catch { } }
    static void Save() { try { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllLines(FileP, new List<string>(pids).ToArray()); } catch { } }
    public static bool Has(string pid) { lock (lk) { Load(); return pids.Contains(pid); } }
    public static void Add(string pid) { lock (lk) { Load(); if (pids.Add(pid)) Save(); } }
    public static void Remove(string pid) { lock (lk) { Load(); if (pids.Remove(pid)) Save(); } }
    public static void ResetForTest() { lock (lk) { pids.Clear(); loaded = false; } }
}

static class VideoPrep
{
    public const long MaxBytes = 95L * 1024 * 1024;   // the website takes 100 MB: a little room is kept
    // the video bitrate (kbit/s) that keeps a video of this length under the limit (audio 128 kbit/s and some room for the container are counted); 0 = it cannot fit
    public static int VideoKbps(double seconds)
    {
        if (seconds < 1) seconds = 1; double k = MaxBytes * 8.0 / 1000.0 / seconds * 0.93 - 128; if (k < 300) return 0; return (int)Math.Min(8000, k);
    }
    // can it go up as it is? (a web video: H.264 in an MP4, or VP8, VP9 or AV1 in a WebM, small enough)
    public static bool PlainOk(string ext, string vcodec, string pixfmt, long bytes)
    {
        ext = (ext ?? "").ToLowerInvariant().TrimStart('.'); vcodec = (vcodec ?? "").ToLowerInvariant(); if (bytes > MaxBytes) return false;
        if ((ext == "mp4" || ext == "m4v") && vcodec == "h264" && (pixfmt == "yuv420p" || pixfmt == "yuvj420p")) return true;
        return ext == "webm" && (vcodec == "vp8" || vcodec == "vp9" || vcodec == "av1");
    }
    // the ffmpeg arguments: any size and shape stays as it is (only larger than 1920 is made smaller, and both sides are made even)
    public static string TranscodeArgs(string src, string dst, int kbps)
    {
        return "-hide_banner -loglevel error -y -i \"" + src + "\" -map 0:v:0 -map 0:a:0? -vf \"scale='min(1920,iw)':'min(1920,ih)':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p\" -c:v libx264 -preset veryfast -b:v " + kbps + "k -maxrate " + (int)(kbps * 1.5) + "k -bufsize " + (kbps * 3) + "k -c:a aac -b:a 128k -movflags +faststart \"" + dst + "\"";
    }
    static string Run(string exe, string args, int timeoutMs, out int code)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using (var p = Process.Start(psi))
        {
            var err = new StringBuilder(); p.ErrorDataReceived += (s, e) => { if (e.Data != null && err.Length < 4000) err.AppendLine(e.Data); }; p.BeginErrorReadLine(); string o = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } code = -1; return "it took too long"; } p.WaitForExit(); code = p.ExitCode; return o + err;
        }
    }
    // null = ready: outPath is the file to send (the file itself, or a converted copy in the temp folder, made = true)
    public static string Prepare(string file, out string outPath, out bool made, Action<string> note)
    {
        outPath = file; made = false; string ff = StudioEnv.Ffmpeg(); if (!File.Exists(file)) return "That file is not there any more.";
        string fp = StudioEnv.Ffprobe(); string vcodec = "", pix = ""; double dur = 0; int code;
        if (fp != null)
        {
            string o = Run(fp, "-v error -select_streams v:0 -show_entries stream=codec_name,pix_fmt:format=duration -of default=nw=1 \"" + file + "\"", 60000, out code);
            foreach (var line in o.Split('\n')) { var l = line.Trim(); if (l.StartsWith("codec_name=")) vcodec = l.Substring(11); else if (l.StartsWith("pix_fmt=")) pix = l.Substring(8); else if (l.StartsWith("duration=")) double.TryParse(l.Substring(9), NumberStyles.Float, CultureInfo.InvariantCulture, out dur); }
            if (vcodec.Length == 0) return "That does not look like a video file.";
        }
        long bytes = new FileInfo(file).Length; if (PlainOk(Path.GetExtension(file), vcodec, pix, bytes)) return null;
        if (ff == null) return "ffmpeg is not installed yet: convert something once in the main window, then try again (it is needed to prepare the video).";
        if (dur <= 0) dur = 240; int kbps = VideoKbps(dur); if (kbps == 0) return "The video is too long to fit in 100 MB (" + (int)(dur / 60) + " minutes). The longest is about " + (int)(MaxBytes * 8.0 / 1000.0 * 0.93 / 428 / 60) + " minutes.";
        string dst = Path.Combine(Path.GetTempPath(), "purphex_mv_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mp4"); if (note != null) note("Preparing the music video (it is made into a web video, the shape and size stay as they are)...");
        string log = Run(ff, TranscodeArgs(file, dst, kbps), 3 * 3600 * 1000, out code); if (code != 0 || !File.Exists(dst)) { try { File.Delete(dst); } catch { } return "The video could not be prepared" + (log.Length > 0 ? ": " + log.Trim().Split('\n')[0] : "."); }
        if (new FileInfo(dst).Length > 99L * 1024 * 1024) { try { File.Delete(dst); } catch { } return "The prepared video is bigger than 100 MB. Try a shorter video."; }
        outPath = dst; made = true; return null;
    }
}

static partial class ProfApi
{
    // null = it worked, otherwise a sentence (the file is streamed, so a big one does not fill the memory)
    public static string VideoUpload(string key, string pid, string path)
    {
        try
        {
            string boundary = "----purphex" + Guid.NewGuid().ToString("N"); Func<string, string, string> part = (n, v) => "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + n + "\"\r\n\r\n" + v + "\r\n";
            byte[] head = Encoding.UTF8.GetBytes(part("key", key) + part("pid", pid) + "--" + boundary + "\r\nContent-Disposition: form-data; name=\"video\"; filename=\"video.mp4\"\r\nContent-Type: video/mp4\r\n\r\n"), tail = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n"); long len = new FileInfo(path).Length;
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/profiles/song-video"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 3600000; rq.ReadWriteTimeout = 300000; rq.ContentType = "multipart/form-data; boundary=" + boundary; rq.ContentLength = head.Length + len + tail.Length; rq.AllowWriteStreamBuffering = false;
            using (var s = rq.GetRequestStream()) { s.Write(head, 0, head.Length); using (var fs = File.OpenRead(path)) fs.CopyTo(s, 262144); s.Write(tail, 0, tail.Length); }
            string body; try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            catch (WebException ex) { if (ex.Response == null) throw; using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            var o = MiniJson.Parse(body) as Dictionary<string, object>; if (o != null && B(o, "ok")) return null; string e2 = o == null ? "" : S(o, "err");
            switch (e2) { case "video": return "That is not a video the website can use (MP4 or WEBM)."; case "videobig": return "The video is bigger than 100 MB."; case "nosong": return "The song of this project is not on the community page yet. Wait a moment and try again."; case "quota": return "You have used all the room you have on the community page."; case "rate": return "Too many uploads this hour. Try again later."; default: return Say(e2); }
        }
        catch (Exception ex) { return "Could not reach the Purphex community (" + ex.Message + ")."; }
    }
    public static bool VideoRemove(string key, string pid) { try { var o = PostJ("/api/profiles/song-video/remove", "{\"key\":" + J(key) + ",\"pid\":" + J(pid) + "}"); return B(o, "ok"); } catch { return false; } }
}

// the picture of a video, made by ffmpeg one frame at a time (any file, any address of the website), in the size it is shown at
sealed class VideoFeed : IDisposable
{
    public readonly int W, H; public readonly double Start; public double CurT = -1; public readonly DateTime StartedAt = DateTime.Now; public volatile bool Ended;
    readonly Process proc; readonly Thread th; readonly object lk = new object(); readonly LinkedList<VFrame> q = new LinkedList<VFrame>(); readonly Stack<byte[]> pool = new Stack<byte[]>(); volatile bool stop; readonly int frameBytes; const int Fps = 24;
    sealed class VFrame { public double T; public byte[] D; }
    public static string Args(string src, double start, int w, int h)
    {
        return "-loglevel error -ss " + start.ToString("0.###", CultureInfo.InvariantCulture) + " -i \"" + src + "\" -an -sn -vf \"scale=" + w + ":" + h + ":force_original_aspect_ratio=decrease,pad=" + w + ":" + h + ":(ow-iw)/2:(oh-ih)/2:color=black,fps=" + Fps + ",format=bgra\" -f rawvideo -pix_fmt bgra pipe:1";
    }
    public VideoFeed(string ffmpeg, string src, double start, int w, int h)
    {
        W = w; H = h; Start = start; frameBytes = w * h * 4; var psi = new ProcessStartInfo(ffmpeg, Args(src, start, w, h)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        proc = Process.Start(psi); proc.ErrorDataReceived += (s, e) => { }; proc.BeginErrorReadLine(); th = new Thread(Pump) { IsBackground = true, Name = "video-frames" }; th.Start();
    }
    void Pump()
    {
        try
        {
            var s = proc.StandardOutput.BaseStream; int n = 0;
            while (!stop)
            {
                byte[] buf; lock (lk) buf = pool.Count > 0 ? pool.Pop() : null; if (buf == null) buf = new byte[frameBytes]; int got = 0;
                while (got < frameBytes) { int r = s.Read(buf, got, frameBytes - got); if (r <= 0) { Ended = true; return; } got += r; }
                lock (lk) { while (q.Count >= 8 && !stop) Monitor.Wait(lk, 40); if (stop) return; q.AddLast(new VFrame { T = Start + n / (double)Fps, D = buf }); }
                n++;
            }
        }
        catch { Ended = true; }
    }
    // the newest frame that is not later than t (seconds); null while there is none yet or when the one shown is still the right one
    public byte[] FrameFor(double t)
    {
        lock (lk)
        {
            VFrame best = null; while (q.Count > 0 && q.First.Value.T <= t) { if (best != null && pool.Count < 6) pool.Push(best.D); best = q.First.Value; q.RemoveFirst(); } Monitor.PulseAll(lk);
            if (best == null) return null; CurT = best.T; return best.D;
        }
    }
    public void Recycle(byte[] d) { lock (lk) { if (pool.Count < 6 && d != null && d.Length == frameBytes) pool.Push(d); } }
    public void Dispose() { stop = true; lock (lk) Monitor.PulseAll(lk); try { if (!proc.HasExited) proc.Kill(); } catch { } try { proc.Dispose(); } catch { } }
}

partial class StudioForm
{
    VideoFeed vidFeed; string vidKey = "", vidSrc = ""; double vidLastPos; bool vidFull, vidWentFull; Bitmap vidBmp; DateTime vidRestartAt = DateTime.MinValue; bool vidShown;
    internal Func<string> videoPickHook;   // a test answers for itself (the path of the file)
    bool VideoOn { get { return vidKey.Length > 0; } }
    string VideoUrl(string aid, string sid) { return CommunityPresets.BaseUrl + "/api/profiles/" + aid + "/video/" + sid; }

    // the video of a song that is playing in the preview strip starts in its player
    void StartVideoOverlay(string aid, string sid, string pkey)
    {
        if (StudioEnv.Ffmpeg() == null) { Toast("ffmpeg is not installed yet, so the video cannot play here: convert something once in the main window."); return; }
        StopVideoOverlay(); vidKey = pkey; vidSrc = VideoUrl(aid, sid); vidShown = true; lastInput = DateTime.Now;
    }
    void StopVideoOverlay()
    {
        if (vidFull) ExitVideoFull(); var f = vidFeed; vidFeed = null; vidKey = ""; vidSrc = ""; vidShown = false; if (f != null) { try { f.Dispose(); } catch { } } if (vidBmp != null) { vidBmp.Dispose(); vidBmp = null; }
    }
    internal static bool VideoNoOsFull;   // (a recording of the program keeps its window size)
    void EnterVideoFull() { if (!VideoOn) return; vidFull = true; if (!VideoNoOsFull && !Win.IsFull(this)) { Win.ToggleFullscreen(this); vidWentFull = true; } vidRestartAt = DateTime.MinValue; }
    void ExitVideoFull() { vidFull = false; if (vidWentFull) { vidWentFull = false; Win.ToggleFullscreen(this); } vidRestartAt = DateTime.MinValue; }
    // sizes that fit a box (even numbers, 1280 x 720 at most: it is drawn larger when the screen is larger)
    static void VideoSize(RectangleF r, out int w, out int h)
    {
        double sc = Math.Min(1.0, Math.Min(1280.0 / Math.Max(1, r.Width), 720.0 / Math.Max(1, r.Height))); w = Math.Max(64, ((int)(r.Width * sc)) / 2 * 2); h = Math.Max(64, ((int)(r.Height * sc)) / 2 * 2);
    }
    void PaintVideoOverlay(Graphics g)
    {
        if (!VideoOn) return; if (previewPath != vidKey || previewEng == null && !previewLoading) { StopVideoOverlay(); return; }
        float W = ClientSize.Width, H = ClientSize.Height; var pe = previewEng; double pos = pe == null ? 0 : pe.Pos / (double)StudioEngine.SR; lastInput = DateTime.Now;
        RectangleF box; if (vidFull) box = Rc(0, 0, W, H); else { float bw = Math.Min(460, Math.Max(240, W * 0.34f)), bh = bw * 9f / 16f; float innerX = 24 + 72, innerW = (W - 48) - 72 - 112; double dur = pe == null ? 1 : Math.Max(1, pe.EndFrame); float lx = innerX + (float)Math.Min(1.0, pe == null ? 0 : pe.Pos / dur) * innerW; box = Rc(Math.Max(24, Math.Min(W - 24 - bw, lx - bw / 2)), H - 48 - 26 - bh, bw, bh); }
        int vw, vh; VideoSize(box, out vw, out vh);
        // (re)start the picture at the place of the song when it is not running, the size changed, or it drifted away (a jump in the song)
        bool drift = vidFeed != null && vidFeed.CurT >= 0 && (pos - vidFeed.CurT > 1.5 || vidFeed.CurT - pos > 1.2);
        if (pe != null && (vidFeed == null || Math.Abs(vidFeed.W - vw) > vw / 6 || Math.Abs(vidFeed.H - vh) > vh / 6 || drift) && (DateTime.Now - vidRestartAt).TotalMilliseconds > 900)
        {
            var old = vidFeed; vidFeed = null; if (old != null) old.Dispose(); vidRestartAt = DateTime.Now; try { vidFeed = new VideoFeed(StudioEnv.Ffmpeg(), vidSrc, Math.Max(0, pos), vw, vh); } catch { vidFeed = null; Toast("The video could not be started."); StopVideoOverlay(); return; }
        }
        if (vidFeed != null && pe != null && !previewPaused)
        {
            var d = vidFeed.FrameFor(pos); if (d != null) { if (vidBmp == null || vidBmp.Width != vidFeed.W || vidBmp.Height != vidFeed.H) { if (vidBmp != null) vidBmp.Dispose(); vidBmp = new Bitmap(vidFeed.W, vidFeed.H, PixelFormat.Format32bppPArgb); } var bd = vidBmp.LockBits(new Rectangle(0, 0, vidBmp.Width, vidBmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb); try { System.Runtime.InteropServices.Marshal.Copy(d, 0, bd.Scan0, Math.Min(d.Length, Math.Abs(bd.Stride) * bd.Height)); } finally { vidBmp.UnlockBits(bd); } vidFeed.Recycle(d); }
        }
        // the player
        if (vidFull) { using (var b = new SolidBrush(Color.Black)) g.FillRectangle(b, box); } else { Glow(g, NeonViolet, box, 12, 0.7f, 4); FillRR(g, Color.Black, box, 12); }
        var im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.Low;
        if (vidBmp != null) { var gs = g.Save(); if (!vidFull) using (var p = RR(box, 12)) g.SetClip(p, CombineMode.Intersect); g.DrawImage(vidBmp, box); g.Restore(gs); } else Txt(g, vidFeed != null && vidFeed.Ended ? "THE VIDEO ENDED" : "LOADING THE VIDEO...", 12, DimC, box, StringAlignment.Center, true);
        g.InterpolationMode = im; if (!vidFull) using (var pen = new Pen(Color.FromArgb(220, NeonViolet), 1.5f)) using (var p = RR(box, 12)) g.DrawPath(pen, p);
        var rest = Reg(box, () => { if (vidFull) PreviewPauseToggle(); }); if (!vidFull) rest.OnRight = () => { };
        if (vidFull)
        {
            var ex = Rc(W - 24 - 190, 18, 190, 36); Btn(g, ex, "EXIT FULL SCREEN", 3, () => ExitVideoFull(), 12); Hint2(ex, "Esc also leaves full screen. Click the picture to pause or continue");
            Txt(g, previewName, 13, Color.FromArgb(230, Color.White), Rc(24, 20, W - 280, 28), StringAlignment.Near, true);
        }
        else
        {
            var fb = Rc(box.Right - 6 - 124, box.Y + 6, 124, 26); Btn(g, fb, "FULL SCREEN", 2, () => EnterVideoFull(), 10); Hint2(fb, "Full screen: the picture is fitted to your screen without stretching it");
            var cb = Rc(box.X + 6, box.Y + 6, 26, 26); Btn(g, cb, "x", 3, () => StopVideoOverlay(), 11); Hint2(cb, "Close the video (the song keeps playing)");
        }
    }

    // ================================================================= UPLOAD MUSIC VIDEO =================================================================
    void UploadMusicVideo(ProjectEntry e)
    {
        if (!HaveProfile("upload a music video")) return; string pid = CommunitySongs.PidOf(e.Path); var rec = CommunitySongs.Load().Find(r => r.Pid == pid);
        if (!e.OnProfile || rec == null || rec.SongId.Length < 2 || rec.SongId == "-") { Toast("Publish this project first (the button under it): the music video goes with its song on the community page."); return; }
        if (VideoRecs.Has(pid)) { var m = Win.NewMenu(); m.Items.Add("Choose a new music video file...").Click += (a, b) => PickAndSendVideo(e, pid); m.Items.Add("Remove the music video").Click += (a, b) => RemoveMusicVideo(e, pid); m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); return; }
        PickAndSendVideo(e, pid);
    }
    void PickAndSendVideo(ProjectEntry e, string pid)
    {
        string file = null; if (videoPickHook != null) file = videoPickHook(); else using (var d = new OpenFileDialog { Title = "Choose the music video of \"" + e.Name + "\" (any size or shape)", Filter = "Video files|*.mp4;*.m4v;*.mov;*.mkv;*.webm;*.avi;*.wmv;*.flv;*.mpg;*.mpeg;*.3gp;*.ts;*.mts|All files|*.*" }) { if (d.ShowDialog(this) != DialogResult.OK) return; file = d.FileName; }
        if (file == null) return; string key = ProfileStore.Key, name = e.Name; Toast("Preparing the music video of \"" + name + "\"...");
        new Thread(() =>
        {
            string outp, err; bool made = false; err = VideoPrep.Prepare(file, out outp, out made, s => SongNote(s));
            if (err == null) { SongNote("Sending the music video of \"" + name + "\"..."); err = ProfApi.VideoUpload(key, pid, outp); }
            if (made) { try { File.Delete(outp); } catch { } }
            try { BeginInvoke(new Action(() => { if (err == null) { VideoRecs.Add(pid); Toast("The music video of \"" + name + "\" is on the community page, with the song."); QueueRepublish(); } else Toast("The music video was not sent: " + err); })); } catch { }
        }) { IsBackground = true, Name = "music-video-upload" }.Start();
    }
    void RemoveMusicVideo(ProjectEntry e, string pid)
    {
        if (!AskYes("Remove the music video of \"" + e.Name + "\" from the community page? (The song stays.)", "Remove music video")) return; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { bool ok = ProfApi.VideoRemove(key, pid); try { BeginInvoke(new Action(() => { if (ok) { VideoRecs.Remove(pid); Toast("The music video was removed."); QueueRepublish(); } else Toast("Could not reach the Purphex community. Check your internet connection."); })); } catch { } });
    }
}
