using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// The lyrics getter: after a song is converted, its lyrics are looked up in LRCLIB (lrclib.net, a free community lyrics database) by the artist and title in the file name,
// the parts get labels ([Verse 1], [Chorus]...) and everything is saved as a .txt next to the song. It is in every edition (Settings > General turns it off).
// The lyrics belong to their writers: the file says so and is for your own use. Nothing is saved when nothing matches well enough (a wrong song's lyrics are worse than none).
static class LyricsGet
{
    public static string BaseUrl = "https://lrclib.net";   // tests point this at a small local server
    static readonly string[] Media = { ".mp3", ".m4a", ".flac", ".wav", ".opus", ".ogg", ".aac", ".mp4", ".mkv", ".webm", ".mov" };
    public sealed class Found { public string Artist, Title, Lyrics; }

    // "Artist - Title (Official Video) [MP3 320k]" -> artist "Artist", title "Title"
    public static void CleanName(string fileNameNoExt, out string artist, out string title)
    {
        string s = (fileNameNoExt ?? "").Replace('–', '-').Replace('—', '-');
        s = Regex.Replace(s, @"\s*\[[^\]]*\]", " "); s = Regex.Replace(s, @"\s*\([^)]*\)", " "); s = Regex.Replace(s, @"\s*\|.*$", ""); s = Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '_');
        s = Regex.Replace(s, @"\b(official|lyrics?|lyrical|video|audio|visuali[sz]er|hd|hq|4k|remaster(ed)?)\b\s*$", "", RegexOptions.IgnoreCase).Trim(' ', '-');
        int i = s.IndexOf(" - ", StringComparison.Ordinal);
        if (i > 0) { artist = s.Substring(0, i).Trim(); title = s.Substring(i + 3).Trim(); } else { artist = ""; title = s; }
    }
    static List<string> Tokens(string s) { var l = new List<string>(); foreach (Match m in Regex.Matches((s ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+")) l.Add(m.Value); return l; }
    static double Overlap(string want, string have)
    {
        var a = Tokens(want); var b = new HashSet<string>(Tokens(have)); if (a.Count == 0) return 0; int hit = 0; foreach (var t in a) if (b.Contains(t)) hit++; return hit / (double)a.Count;
    }
    static string GetText(string url)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "PurphexMusic/" + App.CurrentVersion + " (https://purphexmusic.com)"; rq.Timeout = 12000; rq.ReadWriteTimeout = 12000; rq.Accept = "application/json";
        using (var rs = (HttpWebResponse)rq.GetResponse()) using (var st = rs.GetResponseStream()) using (var rd = new StreamReader(st, Encoding.UTF8)) return rd.ReadToEnd();
    }
    static string S(Dictionary<string, object> d, string k) { object o; return d != null && d.TryGetValue(k, out o) && o is string ? (string)o : ""; }
    static bool Usable(Dictionary<string, object> d) { object ins; bool instrumental = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins; return !instrumental && S(d, "plainLyrics").Trim().Length > 20; }

    // the best match, or null (nothing found / nothing close enough). Throws on a network problem so the caller can say so.
    public static Found Fetch(string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        if (!string.IsNullOrWhiteSpace(artist))
        {
            try
            {
                var d = MiniJson.Parse(GetText(BaseUrl + "/api/get?artist_name=" + Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(title))) as Dictionary<string, object>;
                if (d != null && Usable(d) && Overlap(title, S(d, "trackName")) >= 0.6) return new Found { Artist = S(d, "artistName"), Title = S(d, "trackName"), Lyrics = S(d, "plainLyrics") };
            }
            catch (WebException ex) { var r = ex.Response as HttpWebResponse; if (r == null || (int)r.StatusCode >= 500) throw; }   // 404 = no exact match: search instead
        }
        var list = MiniJson.Parse(GetText(BaseUrl + "/api/search?q=" + Uri.EscapeDataString((artist + " " + title).Trim()))) as List<object>;
        if (list == null) return null; Found best = null; double bestScore = 0;
        foreach (var o in list)
        {
            var d = o as Dictionary<string, object>; if (d == null || !Usable(d)) continue;
            double ts = Overlap(title, S(d, "trackName")), asx = string.IsNullOrWhiteSpace(artist) ? 1 : Overlap(artist, S(d, "artistName") + " " + S(d, "trackName"));
            if (ts < 0.6 || asx < 0.5) continue; double score = ts * 2 + asx; if (score > bestScore) { bestScore = score; best = new Found { Artist = S(d, "artistName"), Title = S(d, "trackName"), Lyrics = S(d, "plainLyrics") }; }
        }
        return best;
    }

    // the text of the file: the song, the writer, the labelled lyrics and a note about whose they are
    public static string Compose(Found f, out string note)
    {
        int added; string labelled = LyricSections.Add(f.Lyrics.Replace("\r\n", "\n").Trim(), out added, out note);
        var sb = new StringBuilder(); sb.Append(f.Title).Append("\r\n").Append(f.Artist).Append("\r\n\r\n").Append(labelled.Replace("\n", "\r\n")).Append("\r\n\r\n")
          .Append("Lyrics found on LRCLIB (lrclib.net). They belong to their writers and are saved here for your own use. Purphex Music added the [Verse], [Chorus] labels by looking at the shape of the lyrics, so a few may need a change.\r\n");
        return sb.ToString();
    }

    // one song: look up, label, save "<song>.txt"; returns a sentence for the log
    public static string SaveFor(string mediaPath)
    {
        try
        {
            string txt = Path.ChangeExtension(mediaPath, ".txt"); string shown = Path.GetFileName(txt); if (File.Exists(txt)) return "Lyrics: " + shown + " is already there.";
            string artist, title; CleanName(Path.GetFileNameWithoutExtension(mediaPath), out artist, out title);
            var f = Fetch(artist, title); if (f == null) return "No lyrics found for \"" + (artist.Length > 0 ? artist + " - " : "") + title + "\" (it may be an instrumental, brand new, or named differently).";
            string note; File.WriteAllText(txt, Compose(f, out note), new UTF8Encoding(true)); return "Lyrics saved: " + shown + " (" + note.Replace(" Ctrl+Z undoes it.", "") + ")";
        }
        catch (WebException) { return "Could not look up the lyrics (no connection to the lyrics database)."; }
        catch (Exception ex) { return "Could not save the lyrics: " + ex.Message; }
    }

    public static HashSet<string> Snapshot(string dir)
    {
        var h = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) h.Add(f); } catch { } return h;
    }
    // every song that appeared in the folder since the snapshot
    public static void ForNewFiles(string dir, HashSet<string> before, Action<string> log)
    {
        try
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                if (before.Contains(f)) continue; string ext = Path.GetExtension(f).ToLowerInvariant(); if (Array.IndexOf(Media, ext) < 0) continue; if (log != null) log(SaveFor(f));
            }
        }
        catch { }
    }
}
