using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// GENERATE LYRICS for any song or video file you add. Free in every edition.
// 1. the name and the tags of the file say which song it is, and the lyrics database (LRCLIB) is asked for them; 2. when the song is not in the database the words can be written by listening to it
//    (the Whisper speech recogniser, which runs on this PC and needs a one-time download that is only done after you say yes); 3. either way the text is tidied: capitals, apostrophes, punctuation, verses and chorus.
static class LyricsGen
{
    public static readonly string[] Exts = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".opus", ".aac", ".wma", ".aiff", ".aif", ".alac", ".ape", ".mka", ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".wmv", ".flv", ".mpg", ".mpeg", ".3gp", ".ts" };
    public static bool IsMedia(string path) { return !string.IsNullOrEmpty(path) && Array.IndexOf(Exts, Path.GetExtension(path).ToLowerInvariant()) >= 0; }
    public static string DialogFilter { get { return "Songs and videos|" + string.Join(";", Array.ConvertAll(Exts, e => "*" + e)) + "|All files|*.*"; } }

    // ---- links: the song is found from the title of the video, so nothing has to be downloaded ----
    public static Func<string> YtDlpPath = () => App.YtDlp;   // tests point this at nothing
    public static string OEmbedUrl = "https://www.youtube.com/oembed";   // tests point this at a small local server
    public static bool IsLink(string s) { s = (s ?? "").Trim(); return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && s.IndexOf(' ') < 0); }
    static string Field(Dictionary<string, object> d, string k) { object o; return d != null && d.TryGetValue(k, out o) && o is string ? ((string)o).Trim() : ""; }
    static string CleanChannel(string a) { a = Regex.Replace(a ?? "", @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase); a = Regex.Replace(a, @"VEVO$", "", RegexOptions.IgnoreCase); a = Regex.Replace(a, @"\s*(Official|Music)$", "", RegexOptions.IgnoreCase); return a.Trim(); }
    // the artist and title of the song a link points to (YouTube answers by itself; any other site needs the link reader that the converter installs), or err says why not
    public static void ResolveLink(string url, out string artist, out string title, out string err)
    {
        artist = ""; title = ""; err = ""; string u = (url ?? "").Trim(); if (u.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) u = "https://" + u;
        Uri uri; if (!Uri.TryCreate(u, UriKind.Absolute, out uri)) { err = "That does not look like a link."; return; }
        string host = uri.Host.ToLowerInvariant(), rawTitle = "", author = "", ytA = "", ytT = ""; bool yt = host == "youtu.be" || host.EndsWith("youtube.com");
        if (yt) { try { var d = MiniJson.Parse(LyricsGet.GetText(OEmbedUrl + "?format=json&url=" + Uri.EscapeDataString(u))) as Dictionary<string, object>; rawTitle = Field(d, "title"); author = Field(d, "author_name"); } catch { } }
        string yd = null; try { yd = YtDlpPath(); } catch { }
        if (rawTitle.Length == 0 && yd != null && File.Exists(yd))
        {
            try
            {
                int code; string o = Run(yd, "--no-playlist --no-warnings --skip-download --print \"%(artist)s|||%(track)s|||%(title)s|||%(uploader)s\" \"" + u + "\"", 45000, null, out code);
                foreach (string line in o.Split('\n')) { if (line.IndexOf("|||", StringComparison.Ordinal) < 0) continue; string[] p = line.Trim().Split(new[] { "|||" }, StringSplitOptions.None); if (p.Length < 4) continue; Func<string, string> na = x => x.Trim() == "NA" ? "" : x.Trim(); ytA = na(p[0]); ytT = na(p[1]); rawTitle = na(p[2]); author = na(p[3]); break; }
            }
            catch { }
        }
        if (rawTitle.Length == 0 && ytT.Length == 0) { err = yt || (yd != null && File.Exists(yd)) ? "Could not read that link (is it private, or not a song or video?)." : "That link is not YouTube, so it needs the link reader that the converter installs: convert any link once (or add the file), then try again."; return; }
        if (ytT.Length > 0 && ytA.Length > 0) { artist = ytA; title = ytT; return; }
        string a, t; LyricsGet.CleanName(rawTitle, out a, out t); artist = a.Length > 0 ? a : (ytA.Length > 0 ? ytA : CleanChannel(author)); title = ytT.Length > 0 ? ytT : t;
    }

    // the lyrics for a link (no file, no download): the video's title finds the song in the lyrics database
    static Result GenerateFromLink(string url, Action<string> say, Func<bool> stop)
    {
        var r = new Result();
        try
        {
            say("Reading the link..."); string artist, title, err; ResolveLink(url, out artist, out title, out err); if (stop()) { r.Error = "Cancelled."; return r; }
            if (err.Length > 0) { r.Error = err; return r; }
            r.Artist = artist; r.Title = title; say("Looking for the lyrics of \"" + (artist.Length > 0 ? artist + " - " : "") + title + "\"..."); LyricsGet.Found f = null;
            try { f = LyricsGet.Fetch(artist, title); } catch (WebException) { r.Error = "Could not reach the lyrics database. Check your connection and try again."; return r; }
            if (stop()) { r.Error = "Cancelled."; return r; }
            if (f == null) { r.Error = "\"" + (artist.Length > 0 ? artist + " - " : "") + title + "\" is not in the lyrics database. To write the words by listening, add the song or video file instead of the link."; return r; }
            string n; r.Lyrics = Finish(f.Lyrics, false, out n); r.Artist = f.Artist; r.Title = f.Title; r.Source = "LRCLIB lyrics database (found from the link)"; r.Found = true; r.Note = "Found from the link, nothing was downloaded. " + n; return r;
        }
        catch (Exception ex) { r.Error = ex.Message; return r; }
    }

    public sealed class Result
    {
        public string Artist = "", Title = "", Lyrics = "", Source = "", Note = "", Error = ""; public bool NeedSetup, Found;
        public string DefaultTitle { get { return (Artist.Length > 0 ? Artist + " - " : "") + Title; } }
    }

    static string Run(string exe, string args, int timeoutMs, Func<bool> cancelled, out int code)
    {
        code = -1; var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8 };
        var sb = new StringBuilder(); using (var p = Process.Start(psi))
        {
            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.BeginOutputReadLine(); p.BeginErrorReadLine();
            Current = p; int waited = 0;
            try { while (!p.WaitForExit(250)) { waited += 250; if ((cancelled != null && cancelled()) || (timeoutMs > 0 && waited > timeoutMs)) { try { p.Kill(); } catch { } p.WaitForExit(2000); break; } } }
            finally { Current = null; }
            try { code = p.HasExited ? p.ExitCode : -1; } catch { } p.WaitForExit(500);
        }
        lock (sb) return sb.ToString();
    }
    static volatile Process Current;
    public static void Cancel() { var p = Current; if (p != null) { try { p.Kill(); } catch { } } }

    // the title and artist written inside the file (ffmpeg shows them), or empty
    public static void ReadTags(string path, out string artist, out string title)
    {
        artist = ""; title = ""; string ff = null; try { ff = StudioEnv.Ffmpeg(); } catch { } if (ff == null || !File.Exists(ff) || !File.Exists(path)) return;
        try
        {
            int code; string o = Run(ff, "-hide_banner -i \"" + path + "\"", 15000, null, out code);
            foreach (string line in o.Split('\n'))
            {
                var m = Regex.Match(line, @"^\s*(title|artist|album_artist)\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase); if (!m.Success) continue; string k = m.Groups[1].Value.ToLowerInvariant(), v = m.Groups[2].Value.Trim();
                if (k == "title" && title.Length == 0) title = v; else if (k == "artist" && artist.Length == 0) artist = v; else if (k == "album_artist" && artist.Length == 0) artist = v;
            }
        }
        catch { }
    }

    public static string Finish(string raw, bool fromRecording, out string note) { return LyricsPolish.Finish(raw, fromRecording, out note); }

    // the whole job for one file; status gets what is happening now. Never throws: a problem comes back in Result.Error.
    // "90", "1:30", "1:02:03" or "1:30.5" as seconds; empty is no trim (-1); err says what is wrong
    public static double ParseTime(string s, out string err)
    {
        err = ""; s = (s ?? "").Trim(); if (s.Length == 0) return -1; string[] p = s.Split(':'); if (p.Length > 3) { err = "write the time like 1:30"; return -1; }
        double total = 0; foreach (string x in p) { double v; if (!double.TryParse(x.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < 0 || x.Trim().Length == 0) { err = "write the time like 1:30"; return -1; } total = total * 60 + v; }
        return total;
    }
    // the trim pair of the page: what is wrong with it, or empty
    public static string TrimError(string a, string b, out double ta, out double tb)
    {
        string ea, eb; ta = ParseTime(a, out ea); tb = ParseTime(b, out eb); if (ea.Length > 0) return "TRIM START: " + ea + "."; if (eb.Length > 0) return "TRIM END: " + eb + ".";
        if (ta >= 0 && tb >= 0 && tb <= ta) return "TRIM END has to be after TRIM START."; return "";
    }
    public static Result Generate(string path, bool allowListen, Action<string> status, Func<bool> cancelled, double trimA = -1, double trimB = -1)
    {
        var r = new Result(); Action<string> say = s => { if (status != null) status(s); }; Func<bool> stop = () => cancelled != null && cancelled();
        if (IsLink(path)) return GenerateFromLink(path, say, stop);
        try
        {
            if (!File.Exists(path)) { r.Error = "That file is not there any more."; return r; }
            say("Reading the name of the song..."); string fa, ft; LyricsGet.CleanName(Path.GetFileNameWithoutExtension(path), out fa, out ft);
            string ta, tt; ReadTags(path, out ta, out tt); if (stop()) { r.Error = "Cancelled."; return r; }
            string artist = ta.Length > 0 ? ta : fa, title = tt.Length > 0 ? tt : ft; r.Artist = artist; r.Title = title;
            say("Looking for the lyrics of \"" + (artist.Length > 0 ? artist + " - " : "") + title + "\"..."); LyricsGet.Found f = null; bool offline = false;
            try
            {
                f = LyricsGet.Fetch(artist, title);
                if (f == null && (tt.Length > 0 && (ft != tt || fa != artist))) f = LyricsGet.Fetch(fa, ft);   // the tags did not match: try the file name
            }
            catch (WebException) { offline = true; }
            if (stop()) { r.Error = "Cancelled."; return r; }
            if (f != null)
            {
                string n; r.Lyrics = Finish(f.Lyrics, false, out n); r.Artist = f.Artist; r.Title = f.Title; r.Source = "LRCLIB lyrics database"; r.Found = true; r.Note = "Found in the lyrics database. " + n; return r;
            }
            if (!allowListen) { r.Note = offline ? "Could not reach the lyrics database, and listening to the song is switched off." : "This song is not in the lyrics database."; r.Error = r.Note; return r; }
            if (!LyricsWhisper.Ready) { r.NeedSetup = true; r.Note = (offline ? "Could not reach the lyrics database. " : "This song is not in the lyrics database. ") + "Purphex can write the words by listening to it, which needs a one-time download first."; return r; }
            string raw = LyricsWhisper.Transcribe(path, say, stop, trimA, trimB);
            if (stop()) { r.Error = "Cancelled."; return r; }
            if (raw.Trim().Length < 12) { r.Error = "No words could be heard in this file (an instrumental, or the voice is too quiet)."; return r; }
            string n2; r.Lyrics = Finish(raw, true, out n2); r.Source = "Listened to the song (Whisper, on this PC)"; r.Found = true;
            r.Note = "Written by listening, so check the words against the song: a speech recogniser can mishear a lyric. " + n2; return r;
        }
        catch (Exception ex) { r.Error = ex is WebException ? "Could not reach the internet: " + ex.Message : ex.Message; return r; }
    }
}

// the speech recogniser: whisper.cpp (an exe) and its model, kept under tools/whisper in the data folder
static class LyricsWhisper
{
    public static string ZipUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip";
    public static string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";
    public static string FfmpegZipUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
    public static long MinModelBytes = 100L * 1024 * 1024;
    public const string DownloadSizeText = "about 160 MB";
    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "tools", "whisper"); } }
    static string Find(string name) { try { if (Directory.Exists(Dir)) foreach (var f in Directory.EnumerateFiles(Dir, name, SearchOption.AllDirectories)) return f; } catch { } return null; }
    public static string Exe() { return Find("whisper-cli.exe") ?? Find("main.exe"); }
    public static string Model() { string m = Find("ggml-*.bin"); return m != null && new FileInfo(m).Length >= MinModelBytes ? m : null; }
    static bool HaveFfmpeg { get { try { string f = StudioEnv.Ffmpeg(); return f != null && File.Exists(f); } catch { return false; } } }
    public static bool Ready { get { return Exe() != null && Model() != null && HaveFfmpeg; } }
    public static bool WhisperReady { get { return Exe() != null && Model() != null; } }

    static void Download(string url, string to, Action<string, int> progress, string what, Func<bool> cancelled)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        string part = to + ".part"; try { if (File.Exists(part)) File.Delete(part); } catch { }
        using (var wc = new WebClient())
        {
            wc.Headers[HttpRequestHeader.UserAgent] = "PurphexMusic/" + App.CurrentVersion; var done = new ManualResetEvent(false); Exception err = null; bool cancel = false;
            wc.DownloadProgressChanged += (s, e) => { if (progress != null) progress(what, e.ProgressPercentage); };
            wc.DownloadFileCompleted += (s, e) => { err = e.Error; cancel = e.Cancelled; done.Set(); };
            wc.DownloadFileAsync(new Uri(url), part);
            while (!done.WaitOne(250)) if (cancelled != null && cancelled()) { wc.CancelAsync(); }
            if (cancel) { try { File.Delete(part); } catch { } throw new OperationCanceledException(); }
            if (err != null) { try { File.Delete(part); } catch { } throw err; }
        }
        if (File.Exists(to)) File.Delete(to); File.Move(part, to);
    }

    // downloads what is missing: the recogniser (~10 MB), its model (~150 MB) and ffmpeg (~100 MB, only if the converter has not installed it yet)
    public static void Install(Action<string, int> progress, Func<bool> cancelled)
    {
        Directory.CreateDirectory(Dir);
        if (Exe() == null)
        {
            string zip = Path.Combine(Dir, "whisper.zip"); Download(ZipUrl, zip, progress, "Downloading the speech recogniser", cancelled);
            try { ZipFile.ExtractToDirectory(zip, Path.Combine(Dir, "bin")); } finally { try { File.Delete(zip); } catch { } }
            if (Exe() == null) throw new Exception("The download did not contain the speech recogniser (whisper-cli.exe).");
        }
        if (Model() == null)
        {
            string mp = Path.Combine(Dir, "ggml-base.bin"); Download(ModelUrl, mp, progress, "Downloading the speech model", cancelled);
            if (Model() == null) { try { File.Delete(mp); } catch { } throw new Exception("The speech model did not download completely."); }
        }
        if (!HaveFfmpeg)
        {
            string fd = App.FfmpegDirPath; Directory.CreateDirectory(Path.GetDirectoryName(fd)); string zip = Path.Combine(Path.GetDirectoryName(fd), "ffmpeg.zip");
            Download(FfmpegZipUrl, zip, progress, "Downloading ffmpeg", cancelled);
            try { if (Directory.Exists(fd)) Directory.Delete(fd, true); ZipFile.ExtractToDirectory(zip, fd); } finally { try { File.Delete(zip); } catch { } }
            if (!HaveFfmpeg) throw new Exception("ffmpeg could not be set up.");
        }
    }

    // the words of the song, one line per sentence the recogniser heard
    public static string Transcribe(string mediaPath, Action<string> status, Func<bool> cancelled, double trimA = -1, double trimB = -1)
    {
        string ff = StudioEnv.Ffmpeg(), exe = Exe(), model = Model(); if (ff == null || exe == null || model == null) throw new Exception("The speech recogniser is not set up yet.");
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_lyr_" + Guid.NewGuid().ToString("N")); string wav = tmp + ".wav", outTxt = tmp + "_words"; int code;
        try
        {
            if (status != null) status("Getting the sound ready...");
            string o = Run16k(ff, mediaPath, wav, cancelled, trimA, trimB, out code); if (cancelled != null && cancelled()) return ""; if (code != 0 || !File.Exists(wav)) throw new Exception("The sound of this file could not be read." + (o.Length > 0 ? " " + LastLine(o) : ""));
            if (status != null) status("Listening to the song and writing the words... this can take a few minutes");
            int threads = Math.Max(2, Environment.ProcessorCount / 2); string w = RunWhisper(exe, model, wav, outTxt, threads, cancelled, out code);
            if (cancelled != null && cancelled()) return ""; string txt = outTxt + ".txt";
            if (File.Exists(txt)) return File.ReadAllText(txt, Encoding.UTF8);
            if (code != 0) throw new Exception("The speech recogniser stopped with an error." + (w.Length > 0 ? " " + LastLine(w) : "")); return StripLog(w);
        }
        finally { foreach (var f in new[] { wav, outTxt + ".txt" }) { try { if (File.Exists(f)) File.Delete(f); } catch { } } }
    }
    static string LastLine(string s) { var l = s.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries); return l.Length > 0 ? l[l.Length - 1].Trim() : ""; }
    // when there is no text file the words were printed: drop the log lines
    static string StripLog(string s) { var sb = new StringBuilder(); foreach (string l in s.Replace("\r", "").Split('\n')) { string t = l.Trim(); if (t.Length == 0 || t.StartsWith("whisper_") || t.StartsWith("system_info") || t.StartsWith("main:") || t.StartsWith("ggml_")) continue; sb.AppendLine(t); } return sb.ToString(); }

    static string RunProc(string exe, string args, Func<bool> cancelled, out int code)
    {
        code = -1; var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8 };
        var sb = new StringBuilder(); using (var p = Process.Start(psi))
        {
            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) { if (sb.Length < 200000) sb.AppendLine(e.Data); } }; p.BeginOutputReadLine(); p.BeginErrorReadLine(); procNow = p;
            try { while (!p.WaitForExit(250)) if (cancelled != null && cancelled()) { try { p.Kill(); } catch { } p.WaitForExit(2000); break; } } finally { procNow = null; }
            try { code = p.HasExited ? p.ExitCode : -1; } catch { } p.WaitForExit(500);
        }
        lock (sb) return sb.ToString();
    }
    static volatile Process procNow;
    public static void Cancel() { var p = procNow; if (p != null) { try { p.Kill(); } catch { } } }
    // the part of the song that is heard: from TRIM START to TRIM END (either can be left out)
    static string TrimArgs(double a, double b) { var ci = System.Globalization.CultureInfo.InvariantCulture; string s = ""; if (a > 0) s += "-ss " + a.ToString("0.###", ci) + " "; if (b > 0) s += "-t " + (a > 0 ? Math.Max(0.1, b - a) : b).ToString("0.###", ci) + " "; return s; }
    static string Run16k(string ff, string input, string wav, Func<bool> cancelled, double trimA, double trimB, out int code) { return RunProc(ff, "-hide_banner -y " + TrimArgs(trimA, trimB) + "-i \"" + input + "\" -vn -ac 1 -ar 16000 -c:a pcm_s16le \"" + wav + "\"", cancelled, out code); }
    static string RunWhisper(string exe, string model, string wav, string outBase, int threads, Func<bool> cancelled, out int code) { return RunProc(exe, "-m \"" + model + "\" -f \"" + wav + "\" -l auto -nt -np -t " + threads + " -otxt -of \"" + outBase + "\"", cancelled, out code); }
}
