using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

// ======================================================================
//  PURPHEX CREATIVE STUDIO: the parts that do not need a window (they are tested on their own).
//  Projects and artwork are JSON files in the app's data folder (video and artwork kept apart from the song projects); a save is written to a
//  new file, checked, then swapped in, so a crash never leaves half a project. Autosaves go to their own file and are offered back after a crash.
//  Media files stay where they are (only their path is kept). FFmpeg (the one the converter installs) reads media, makes preview copies and
//  encodes the export from the frames the editor renders.
// ======================================================================
static class JsonOut
{
    public static string Write(object o) { var sb = new StringBuilder(); W(sb, o); return sb.ToString(); }
    static void W(StringBuilder sb, object o)
    {
        if (o == null) { sb.Append("null"); return; }
        var s = o as string; if (s != null) { Str(sb, s); return; }
        if (o is bool) { sb.Append((bool)o ? "true" : "false"); return; }
        if (o is double || o is float || o is int || o is long) { double d = Convert.ToDouble(o, CultureInfo.InvariantCulture); sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("R", CultureInfo.InvariantCulture)); return; }
        var dict = o as IDictionary<string, object>; if (dict != null) { sb.Append('{'); bool first = true; foreach (var kv in dict) { if (!first) sb.Append(','); first = false; Str(sb, kv.Key); sb.Append(':'); W(sb, kv.Value); } sb.Append('}'); return; }
        var list = o as System.Collections.IEnumerable; if (list != null) { sb.Append('['); bool first = true; foreach (var x in list) { if (!first) sb.Append(','); first = false; W(sb, x); } sb.Append(']'); return; }
        Str(sb, o.ToString());
    }
    public static void Str(StringBuilder sb, string s)
    {
        sb.Append('"'); foreach (char c in s) { switch (c) { case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\n': sb.Append("\\n"); break; case '\r': sb.Append("\\r"); break; case '\t': sb.Append("\\t"); break; default: if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break; } } sb.Append('"');
    }
}

static class CreativeStore
{
    public static string Root { get { return Path.Combine(StudioEnv.DataDir, "creative"); } }
    public static string Dir(string sub) { string d = Path.Combine(Root, sub); Directory.CreateDirectory(d); return d; }
    static bool SafeId(string id) { return !string.IsNullOrEmpty(id) && id.Length < 64 && id.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'); }
    // a save: written beside, read back and checked, then swapped in (a broken write never replaces a good project)
    public static void SafeWrite(string path, string json)
    {
        var d = MiniJson.Parse(json) as Dictionary<string, object>; if (d == null || !d.ContainsKey("id")) throw new Exception("The project data was incomplete, so nothing was saved.");
        string tmp = path + ".tmp"; File.WriteAllText(tmp, json, new UTF8Encoding(false));
        var back = MiniJson.Parse(File.ReadAllText(tmp, Encoding.UTF8)) as Dictionary<string, object>; if (back == null) { File.Delete(tmp); throw new Exception("The saved file did not read back, so the old one was kept."); }
        if (File.Exists(path)) { File.Copy(path, path + ".bak", true); File.Replace(tmp, path, null); } else File.Move(tmp, path);
    }
    public static string ProjPath(string kind, string id, bool autosave = false) { if (!SafeId(id)) throw new Exception("Bad project id."); return Path.Combine(Dir(kind == "art" ? "art/projects" : "projects"), id + (autosave ? ".autosave.json" : ".json")); }
    public static void Save(string kind, Dictionary<string, object> proj, bool autosave)
    {
        string id = Convert.ToString(proj["id"]); proj["modified"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        string json = JsonOut.Write(Clean(proj)); SafeWrite(ProjPath(kind, id, autosave), json);
        if (!autosave) { try { File.Delete(ProjPath(kind, id, true)); } catch { } File.WriteAllText(Path.Combine(Dir(""), kind + "-last.txt"), id); }
    }
    // runtime-only fields (names starting with _) are never written
    public static object Clean(object o)
    {
        var d = o as Dictionary<string, object>; if (d != null) { var n = new Dictionary<string, object>(); foreach (var kv in d) if (!kv.Key.StartsWith("_", StringComparison.Ordinal)) n[kv.Key] = Clean(kv.Value); return n; }
        var l = o as List<object>; if (l != null) return l.Select(Clean).ToList(); return o;
    }
    // opens a project; when an autosave is newer than the save (Purphex closed without saving) it is offered back
    public static Dictionary<string, object> Load(string kind, string id, out bool recovered)
    {
        recovered = false; string p = ProjPath(kind, id), a = ProjPath(kind, id, true); Dictionary<string, object> d = null;
        if (File.Exists(a) && (!File.Exists(p) || File.GetLastWriteTimeUtc(a) > File.GetLastWriteTimeUtc(p))) { try { d = MiniJson.Parse(File.ReadAllText(a, Encoding.UTF8)) as Dictionary<string, object>; recovered = d != null; } catch { d = null; } }
        if (d == null) { if (!File.Exists(p)) throw new Exception("That project is not there any more."); try { d = MiniJson.Parse(File.ReadAllText(p, Encoding.UTF8)) as Dictionary<string, object>; } catch { d = null; } if (d == null && File.Exists(p + ".bak")) { try { d = MiniJson.Parse(File.ReadAllText(p + ".bak", Encoding.UTF8)) as Dictionary<string, object>; } catch { } } }
        if (d == null) throw new Exception("That project file is damaged.");
        return d;
    }
    public static string LastId(string kind) { try { return File.ReadAllText(Path.Combine(Dir(""), kind + "-last.txt")).Trim(); } catch { return ""; } }
    public static List<Dictionary<string, object>> List(string kind)
    {
        var l = new List<Dictionary<string, object>>(); var dir = Dir(kind == "art" ? "art/projects" : "projects");
        foreach (var f in Directory.GetFiles(dir, "*.json").Where(x => !x.EndsWith(".autosave.json", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var d = MiniJson.Parse(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>; if (d == null) continue; int missing = 0;
                foreach (var m in HubApi.Arr(d, "media")) { string mp = HubApi.S(m, "path"); if (mp.Length > 0 && !File.Exists(mp)) missing++; }
                l.Add(new Dictionary<string, object> { { "id", HubApi.S(d, "id") }, { "name", HubApi.S(d, "name") }, { "modified", HubApi.S(d, "modified").Length > 0 ? HubApi.S(d, "modified") : File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm") }, { "missing", (double)missing } });
            }
            catch { }
        }
        return l;
    }
    public static void Delete(string kind, string id) { foreach (var p in new[] { ProjPath(kind, id), ProjPath(kind, id, true), ProjPath(kind, id) + ".bak" }) try { if (File.Exists(p)) File.Delete(p); } catch { } }
    public static void Rename(string kind, string id, string name) { bool r; var d = Load(kind, id, out r); d["name"] = Clip(name, 80); SafeWrite(ProjPath(kind, id), JsonOut.Write(Clean(d))); }
    public static string Duplicate(string kind, string id) { bool r; var d = Load(kind, id, out r); string nid = (kind == "art" ? "a" : "p") + Guid.NewGuid().ToString("N").Substring(0, 12); d["id"] = nid; d["name"] = Clip(HubApi.S(d, "name") + " copy", 80); SafeWrite(ProjPath(kind, nid), JsonOut.Write(Clean(d))); return nid; }
    public static string Clip(string s, int n) { s = (s ?? "").Trim(); return s.Length > n ? s.Substring(0, n) : s; }

    // ---- media ----
    static readonly string[] Vid = { ".mp4", ".mov", ".m4v", ".webm", ".mkv", ".avi", ".wmv", ".mpg", ".mpeg", ".3gp", ".flv", ".ts" }, Aud = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aif", ".aiff" }, Img = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };
    public static string KindOf(string path) { string e = Path.GetExtension(path ?? "").ToLowerInvariant(); return Vid.Contains(e) ? "video" : Aud.Contains(e) ? "audio" : Img.Contains(e) ? "image" : null; }
    public const string Filter = "Media (video, audio, pictures)|*.mp4;*.mov;*.m4v;*.webm;*.mkv;*.avi;*.wmv;*.mpg;*.mpeg;*.3gp;*.flv;*.ts;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aif;*.aiff;*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|All files|*.*";
    // a folder seen by the editor page as its own https host (only the folders of files you picked are reachable)
    public static string HostFor(string folder) { uint h = 2166136261; foreach (char ch in folder.ToLowerInvariant()) { h ^= ch; h *= 16777619; } return "m" + h.ToString("x8") + ".media"; }
    public static string UrlFor(string path, string id) { return "https://media.creative/" + Uri.EscapeDataString(id) + "/" + Uri.EscapeDataString(Path.GetFileName(path)); }
    public static string CacheKey(string path) { var fi = new FileInfo(path); string k = path.ToLowerInvariant() + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks; using (var sha = System.Security.Cryptography.SHA1.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(k))).Replace("-", "").Substring(0, 20).ToLowerInvariant(); }
    public static bool PngHasAlpha(string path) { try { using (var fs = File.OpenRead(path)) { var b = new byte[26]; if (fs.Read(b, 0, 26) < 26 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return false; return b[25] == 4 || b[25] == 6; } } catch { return false; } }

    // ---- ffmpeg ----
    public static string Run(string exe, string args, int timeoutMs, out int code)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        using (var p = Process.Start(psi)) { var err = new StringBuilder(); p.ErrorDataReceived += (s, e) => { if (e.Data != null && err.Length < 20000) err.AppendLine(e.Data); }; p.BeginErrorReadLine(); string o = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } code = -1; return o + err; } p.WaitForExit(); code = p.ExitCode; return o.Length > 0 ? o : err.ToString(); }
    }
    // what is in a media file (length, size, codecs, whether it has sound)
    public static Dictionary<string, object> Probe(string ffprobe, string path, out string error)
    {
        error = null; int code; string json = Run(ffprobe, "-v error -print_format json -show_format -show_streams \"" + path + "\"", 30000, out code);
        if (code != 0) { error = "Could not read \"" + Path.GetFileName(path) + "\": it is damaged or not a media file Purphex supports."; return null; }
        var d = MiniJson.Parse(json) as Dictionary<string, object>; var info = new Dictionary<string, object>(); bool hasA = false;
        double dur; if (double.TryParse(HubApi.S(HubApi.Obj(d, "format"), "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out dur)) info["dur"] = dur;
        foreach (var s in HubApi.Arr(d, "streams"))
        {
            string t = HubApi.S(s, "codec_type");
            if (t == "video" && !info.ContainsKey("vcodec") && HubApi.S(HubApi.Obj(s, "disposition"), "attached_pic") != "1") { info["vcodec"] = HubApi.S(s, "codec_name"); double w, h; if (double.TryParse(HubApi.S(s, "width"), out w)) info["w"] = w; if (double.TryParse(HubApi.S(s, "height"), out h)) info["h"] = h; info["fps"] = HubApi.S(s, "avg_frame_rate"); }
            if (t == "audio" && !hasA) { hasA = true; info["acodec"] = HubApi.S(s, "codec_name"); }
        }
        info["hasAudio"] = hasA; return info;
    }
    // the sound of a media file in a small file the editor can read quickly (Opus at 48 kHz, stereo)
    public static string ExtractAudio(string ffmpeg, string path, string key, out string error)
    {
        error = null; string outp = Path.Combine(Dir("cache"), key + ".ogg"); if (File.Exists(outp) && new FileInfo(outp).Length > 0) return outp;
        int code; string o = Run(ffmpeg, "-y -v error -i \"" + path + "\" -vn -ac 2 -ar 48000 -c:a libopus -b:a 160k \"" + outp + ".part.ogg\"", 600000, out code);
        if (code != 0 || !File.Exists(outp + ".part.ogg")) { error = "Could not read the sound of \"" + Path.GetFileName(path) + "\"."; try { File.Delete(outp + ".part.ogg"); } catch { } return null; }
        File.Move(outp + ".part.ogg", outp); return outp;
    }
    // a copy the preview can always play (720p H.264) for codecs the editor's engine cannot decode; the export still uses the original
    public static string Proxy(string ffmpeg, string path, string key, out string error)
    {
        error = null; string outp = Path.Combine(Dir("cache"), key + "_proxy.mp4"); if (File.Exists(outp) && new FileInfo(outp).Length > 0) return outp;
        int code; Run(ffmpeg, "-y -v error -i \"" + path + "\" -vf \"scale=-2:'min(720,ih)'\" -c:v libx264 -preset veryfast -crf 24 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart \"" + outp + ".part.mp4\"", 3600000, out code);
        if (code != 0 || !File.Exists(outp + ".part.mp4")) { error = "Could not make a preview copy of \"" + Path.GetFileName(path) + "\"."; try { File.Delete(outp + ".part.mp4"); } catch { } return null; }
        File.Move(outp + ".part.mp4", outp); return outp;
    }
    // ---- export: the encoder settings for each format and quality ----
    public static string VideoArgs(string fmt, string quality)
    {
        int q = quality == "draft" ? 0 : quality == "good" ? 1 : quality == "max" ? 3 : 2;
        if (fmt == "mp4-h265") return "-c:v libx265 -preset " + new[] { "veryfast", "medium", "medium", "slow" }[q] + " -crf " + new[] { 30, 26, 22, 18 }[q] + " -tag:v hvc1 -pix_fmt yuv420p";
        if (fmt == "webm") return "-c:v libvpx-vp9 -b:v 0 -crf " + new[] { 40, 34, 30, 24 }[q] + " -row-mt 1 -deadline good -cpu-used " + new[] { 6, 4, 2, 1 }[q] + " -pix_fmt yuv420p";
        return "-c:v libx264 -preset " + new[] { "veryfast", "medium", "medium", "slow" }[q] + " -crf " + new[] { 28, 22, 18, 15 }[q] + " -pix_fmt yuv420p";
    }
    public static string AudioArgs(string fmt, int kbps) { kbps = Math.Max(64, Math.Min(320, kbps)); return fmt == "webm" ? "-c:a libopus -b:a " + kbps + "k" : fmt == "wav" ? "-c:a pcm_s16le" : "-c:a aac -b:a " + kbps + "k"; }
    public static string Ext(string fmt) { return fmt == "webm" ? ".webm" : fmt == "m4a" ? ".m4a" : fmt == "wav" ? ".wav" : ".mp4"; }
    public static string ExportArgs(string fmt, string quality, int abr, double fps, string audioWav, string outPath)
    {
        if (fmt == "m4a" || fmt == "wav") return "-y -v error -i \"" + audioWav + "\" " + AudioArgs(fmt, abr) + " \"" + outPath + "\"";
        return "-y -v error -f image2pipe -framerate " + fps.ToString(CultureInfo.InvariantCulture) + " -c:v mjpeg -i - -i \"" + audioWav + "\" -map 0:v -map 1:a " + VideoArgs(fmt, quality) + " -r " + fps.ToString(CultureInfo.InvariantCulture) + " " + AudioArgs(fmt, abr) + (fmt == "webm" ? "" : " -movflags +faststart") + " -shortest \"" + outPath + "\"";
    }
    // checks a finished file: it must have the streams it should and about the right length
    public static string Validate(string ffprobe, string path, double wantDur, bool video, out bool ok)
    {
        ok = false; if (!File.Exists(path) || new FileInfo(path).Length < 1000) return "The file was not written.";
        string err; var info = Probe(ffprobe, path, out err); if (info == null) return err;
        double dur = info.ContainsKey("dur") ? (double)info["dur"] : 0; if (video && !info.ContainsKey("vcodec")) return "The file has no picture."; if (!(bool)info["hasAudio"]) return "The file has no sound.";
        if (Math.Abs(dur - wantDur) > Math.Max(0.5, wantDur * 0.03)) return "The file is " + dur.ToString("0.0") + " s long instead of " + wantDur.ToString("0.0") + " s.";
        ok = true; return (video ? info["w"] + "x" + info["h"] + " " + info["vcodec"] + " + " : "") + info["acodec"] + ", " + TimeSpan.FromSeconds(dur).ToString(@"m\:ss") + ", " + (new FileInfo(path).Length / 1048576.0).ToString("0.0") + " MB";
    }
}
