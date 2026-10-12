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
    public sealed class Found { public string Artist, Title, Lyrics; public double Duration = -1; public bool Exact; }   // Exact: the title is the title asked for, word for word   // Duration: the length of the song the copy belongs to, in seconds

    // the name with the brackets, the format tag and "Official Video" taken off
    public static string CleanString(string fileNameNoExt)
    {
        string s = (fileNameNoExt ?? "").Replace('–', '-').Replace('—', '-').Replace('_', ' ');
        s = Regex.Replace(s, @"\s*\[[^\]]*\]", " "); s = Regex.Replace(s, @"\s*\([^)]*\)", " "); s = Regex.Replace(s, @"\s*\|.*$", ""); s = Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '_');
        s = Regex.Replace(s, @"\b(official|lyrics?|lyrical|video|audio|visuali[sz]er|hd|hq|4k|remaster(ed)?|music video)\b\s*$", "", RegexOptions.IgnoreCase).Trim(' ', '-'); return s;
    }
    // "Artist - Title (Official Video) [MP3 320k]" -> artist "Artist", title "Title"
    public static void CleanName(string fileNameNoExt, out string artist, out string title)
    {
        string s = CleanString(fileNameNoExt); int i = s.IndexOf(" - ", StringComparison.Ordinal);
        if (i > 0) { artist = s.Substring(0, i).Trim(); title = s.Substring(i + 3).Trim(); } else { artist = ""; title = s; }
    }
    // every sensible reading of a name, best first: "Artist - Title", "Uploader - Artist - Title", "Title - Artist", and the title alone (downloads are named in all of these ways)
    public static List<string[]> Candidates(string nameNoExt)
    {
        var res = new List<string[]>(); string s = CleanString(nameNoExt); if (s.Length == 0) return res;
        Action<string, string> add = (art, ttl) => { art = (art ?? "").Trim(); ttl = (ttl ?? "").Trim(); if (ttl.Length == 0) return; foreach (var r in res) if (r[0].Equals(art, StringComparison.OrdinalIgnoreCase) && r[1].Equals(ttl, StringComparison.OrdinalIgnoreCase)) return; res.Add(new[] { art, ttl }); };
        string[] p = s.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();
        if (p.Length >= 2) add(p[0], string.Join(" - ", p, 1, p.Length - 1));
        if (p.Length >= 3) { add(p[1], string.Join(" - ", p, 2, p.Length - 2)); add(p[p.Length - 2], p[p.Length - 1]); add(p[0], p[p.Length - 1]); }
        if (p.Length == 2) add(p[1], p[0]);
        if (p.Length >= 2) add("", p[p.Length - 1]); add("", s);
        return res;
    }
    // the artist of a tag or a channel without the extras YouTube adds ("Queen Official", "Adele - Topic", "TaylorSwiftVEVO")
    public static string CleanArtist(string a) { a = Regex.Replace(a ?? "", @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase); a = Regex.Replace(a, @"VEVO$", "", RegexOptions.IgnoreCase); a = Regex.Replace(a, @"\s*(Official|Music|Records|Channel)$", "", RegexOptions.IgnoreCase); return a.Trim(); }
    static List<string> Tokens(string s) { var l = new List<string>(); foreach (Match m in Regex.Matches((s ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+")) l.Add(m.Value); return l; }
    static double Overlap(string want, string have)
    {
        var a = Tokens(want); var b = new HashSet<string>(Tokens(have)); if (a.Count == 0) return 0; int hit = 0; foreach (var t in a) if (b.Contains(t)) hit++; return hit / (double)a.Count;
    }
    // a lookup that is tried again once when the network hiccups (the first request after a quiet spell often fails)
    internal static string GetText(string url)
    {
        try { return GetText1(url); }
        catch (WebException ex) { var r = ex.Response as HttpWebResponse; if (r != null && (int)r.StatusCode < 500) throw; System.Threading.Thread.Sleep(500); return GetText1(url); }
    }
    static string GetText1(string url)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "PurphexMusic/" + App.CurrentVersion + " (https://purphexmusic.com)"; rq.Timeout = 12000; rq.ReadWriteTimeout = 12000; rq.Accept = "application/json";
        using (var rs = (HttpWebResponse)rq.GetResponse()) using (var st = rs.GetResponseStream()) using (var rd = new StreamReader(st, Encoding.UTF8)) return rd.ReadToEnd();
    }
    static string S(Dictionary<string, object> d, string k) { object o; return d != null && d.TryGetValue(k, out o) && o is string ? (string)o : ""; }
    static string PlainOf(Dictionary<string, object> d)
    {
        string p = S(d, "plainLyrics"); if (p.Trim().Length > 20) return p; string sy = S(d, "syncedLyrics"); if (sy.Length == 0) return p;
        var lines = new List<string>(); foreach (string l in sy.Replace("\r\n", "\n").Split('\n')) lines.Add(Regex.Replace(l, @"^\s*(\[\d+:\d+(?:[.:]\d+)?\]\s*)+", "").TrimEnd()); return string.Join("\n", lines.ToArray());
    }
    static double Dur(Dictionary<string, object> d) { object o; if (d != null && d.TryGetValue("duration", out o) && o != null) { try { return Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture); } catch { } } return -1; }
    static int WordCount(string s) { return Tokens(s).Count; }
    static bool Usable(Dictionary<string, object> d) { object ins; bool instrumental = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins; return !instrumental && PlainOf(d).Trim().Length > 20; }

    // the best match, or null (nothing found / nothing close enough). Throws on a network problem so the caller can say so.
    // The database can hold several copies of a song and some are cut short, so every copy that is really this song (the same artist, the same title and nothing extra in it) is looked at
    // and the most complete one (the most words) is used. A copy under another singer, or a different song that only has the words in its name (a cover, a reply), is never used.
    public static Found Fetch(string artist, string title, double duration = -1)
    {
        if (string.IsNullOrWhiteSpace(title)) return null; var pool = new List<KeyValuePair<double, Found>>(); bool haveArtist = !string.IsNullOrWhiteSpace(artist);
        if (haveArtist)
        {
            try
            {
                var d = MiniJson.Parse(GetText(BaseUrl + "/api/get?artist_name=" + Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(title))) as Dictionary<string, object>;
                if (d != null && Usable(d) && Overlap(title, S(d, "trackName")) >= 0.6) pool.Add(new KeyValuePair<double, Found>(3.0, new Found { Artist = S(d, "artistName"), Title = S(d, "trackName"), Lyrics = PlainOf(d), Duration = Dur(d), Exact = Overlap(title, S(d, "trackName")) >= 0.99 && Overlap(S(d, "trackName"), title) >= 0.99 }));
            }
            catch (WebException ex) { var r = ex.Response as HttpWebResponse; if (r == null || (int)r.StatusCode >= 500) throw; }   // 404 = no exact match: search instead
        }
        string[] urls = { BaseUrl + "/api/search?q=" + Uri.EscapeDataString((artist + " " + title).Trim()), haveArtist ? BaseUrl + "/api/search?track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist) : null };
        foreach (string url in urls)
        {
            if (url == null || (url == urls[1] && pool.Count > 0)) continue;   /* the plain search always runs (the exact match is not always the fullest copy); the second one only when nothing was found */
            var list = MiniJson.Parse(GetText(url)) as List<object>; if (list == null) continue;
            foreach (var o in list)
            {
                var d = o as Dictionary<string, object>; if (d == null || !Usable(d)) continue; string tn = S(d, "trackName"), an = S(d, "artistName");
                double ts = Overlap(title, tn), tr = Overlap(tn, title), asx = haveArtist ? Overlap(artist, an) : 1;   // the title is in the track name and the track name is little more than the title; the artist is the artist (not just a word in the name)
                if (ts < 0.6 || tr < 0.5 || asx < 0.6) continue; pool.Add(new KeyValuePair<double, Found>(ts + tr + asx, new Found { Artist = an, Title = tn, Lyrics = PlainOf(d), Duration = Dur(d), Exact = ts >= 0.99 && tr >= 0.99 }));
            }
        }
        // the length of the file says which song it is: a copy is only used when its song is as long as the file (give or take six seconds). A title with no artist is only accepted this way: many songs share a name.
        if (!haveArtist) pool = pool.FindAll(kv => kv.Value.Exact);   // a title with no artist: only a copy with exactly this title (a tribute, a live version or a reply with the words in its name is another song)
        if (duration > 0 && pool.Count > 0) { var near = pool.FindAll(kv => kv.Value.Duration > 0 && Math.Abs(kv.Value.Duration - duration) <= 6); if (near.Count > 0) pool = near; else if (!haveArtist) return null; }
        if (pool.Count == 0) return null; double best = 0; foreach (var kv in pool) best = Math.Max(best, kv.Key);
        // the copy whose song is closest in length to the file, then the fullest
        Found pick = null; int most = -1, bucket = int.MaxValue;
        foreach (var kv in pool)
        {
            if (kv.Key < best - 0.15 && haveArtist) continue; int w = WordCount(kv.Value.Lyrics), bk = duration > 0 && kv.Value.Duration > 0 ? (int)(Math.Abs(kv.Value.Duration - duration) / 2.0) : 0;
            if (bk < bucket || (bk == bucket && w > most)) { bucket = bk; most = w; pick = kv.Value; }
        }
        return pick;
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
