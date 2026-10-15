using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Spotify links in the converter. Spotify's audio is locked (it cannot be downloaded), so a Spotify link is read for its songs
// (the title, the artists and the length of a track, every track of an album or playlist, or an artist's top songs, from Spotify's public embed page: no account needed).
// Each song is then looked for on YouTube and only a result that really is that song is converted: its title matches, the artist is in its title or channel,
// and its length is within a few seconds of Spotify's. Covers, remixes, slowed or sped-up versions, loops and karaoke are passed over. When nothing matches, the song is
// skipped with a message (it used to take the first search result, which could be a random song).
static class SpotifyLinks
{
    public sealed class Song { public string Key = "", Value = "", Artist = "", Title = ""; public int Secs; }   // Key: the name shown; Value: the search for YouTube
    static readonly Regex Re = new Regex(@"(?:open\.spotify\.com/(?:intl-[a-z]{2}(?:-[a-z]{2})?/)?(?:embed/)?|spotify:)(track|album|playlist|artist)[/:]([A-Za-z0-9]{10,40})", RegexOptions.IgnoreCase);
    static readonly Regex Short = new Regex(@"(?:^|//)(?:spotify\.link|spotify\.app\.link)/[A-Za-z0-9]+", RegexOptions.IgnoreCase);
    // Apple Music and Deezer links are read the same way (Apple's public iTunes lookup, Deezer's public API): their audio is locked too
    static readonly Regex AppleRe = new Regex(@"music\.apple\.com/(?:[a-z]{2}/)?(album|song|artist)/(?:[^/?#]+/)?(\d+)(?:[^#]*?[?&]i=(\d+))?", RegexOptions.IgnoreCase);
    static readonly Regex DeezerRe = new Regex(@"deezer\.com/(?:[a-z]{2}/)?(track|album|playlist|artist)/(\d+)", RegexOptions.IgnoreCase);
    static readonly Regex DeezerShort = new Regex(@"(?:^|//)(?:deezer\.page\.link|link\.deezer\.com)/", RegexOptions.IgnoreCase);
    public static bool Is(string s) { return s != null && (Re.IsMatch(s) || Short.IsMatch(s) || AppleRe.IsMatch(s) || DeezerRe.IsMatch(s) || DeezerShort.IsMatch(s)); }
    // which service a link is from (for the messages)
    public static string ServiceOf(string s) { if (s == null) return ""; if (AppleRe.IsMatch(s)) return "Apple Music"; if (DeezerRe.IsMatch(s) || DeezerShort.IsMatch(s)) return "Deezer"; return "Spotify"; }
    public static Func<string, string> FetchHook;   // a test answers for the website

    public static List<Song> Resolve(string link, out string err)
    {
        err = null; var res = new List<Song>(); link = link ?? "";
        if (DeezerShort.IsMatch(link) && !DeezerRe.IsMatch(link)) { try { string full = ExpandAny(link, DeezerRe); if (full != null) link = full; } catch (Exception ex) { err = "Could not open that short Deezer link (" + ex.Message + ")."; return res; } }
        if (AppleRe.IsMatch(link)) return ResolveApple(link, out err);
        if (DeezerRe.IsMatch(link)) return ResolveDeezer(link, out err);
        if (!Re.IsMatch(link) && Short.IsMatch(link))
        {   // a short share link (spotify.link/...): follow it to the real address
            try { string full = Expand(link); if (full != null) link = full; } catch (Exception ex) { err = "Could not open that short Spotify link (" + ex.Message + ")."; return res; }
        }
        var m = Re.Match(link);
        if (!m.Success) { err = "That is not a Spotify song, album, playlist or artist link."; return res; }
        string kind = m.Groups[1].Value.ToLowerInvariant(), id = m.Groups[2].Value, html;
        try { html = FetchHook != null ? FetchHook("https://open.spotify.com/embed/" + kind + "/" + id) : Get("https://open.spotify.com/embed/" + kind + "/" + id); }
        catch (Exception ex) { err = "Could not reach Spotify (" + ex.Message + ")."; return res; }
        var nd = Regex.Match(html ?? "", "<script id=\"__NEXT_DATA__\"[^>]*>([\\s\\S]*?)</script>");
        if (!nd.Success) { err = "Spotify did not give the list of songs for that link (is it private?)."; return res; }
        Dictionary<string, object> ent = null;
        try { var root = MiniJson.Parse(nd.Groups[1].Value) as Dictionary<string, object>; ent = Dig(root, "props", "pageProps", "state", "data", "entity"); } catch { }
        if (ent == null) { err = "Spotify's page could not be read for that link."; return res; }
        if (kind == "track")
        {
            string title = Str(ent, "name"); if (title.Length == 0) title = Str(ent, "title"); var names = new List<string>();
            object ar; if (ent.TryGetValue("artists", out ar) && ar is List<object>) foreach (var a in (List<object>)ar) { var d = a as Dictionary<string, object>; if (d != null && Str(d, "name").Length > 0) names.Add(Str(d, "name")); }
            if (title.Length > 0) Add(res, string.Join(", ", names.ToArray()), title, Ms(ent, "duration"));
        }
        else
        {
            object tl; if (ent.TryGetValue("trackList", out tl) && tl is List<object>) foreach (var t in (List<object>)tl) { var d = t as Dictionary<string, object>; if (d != null && Str(d, "title").Length > 0) Add(res, Str(d, "subtitle"), Str(d, "title"), Ms(d, "duration")); }
        }
        if (res.Count == 0) err = "No songs were found in that Spotify link.";
        return res;
    }
    static void Add(List<Song> res, string artist, string title, int secs)
    {
        string shown = (artist.Length > 0 ? artist + " - " : "") + title;
        string q = Regex.Replace(shown, "[\"\\\\]", " ").Trim();   // nothing that could break the quoted search
        res.Add(new Song { Key = shown, Value = "ytsearch1:" + q + " audio", Artist = artist, Title = title, Secs = secs });
    }
    static int Ms(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (int)Math.Round((double)v / 1000.0) : 0; }

    // Apple Music: a song (album link with ?i=, or a song link), a whole album, or an artist's top songs; playlists are not public there
    static List<Song> ResolveApple(string link, out string err)
    {
        err = null; var res = new List<Song>(); var m = AppleRe.Match(link); string kind = m.Groups[1].Value.ToLowerInvariant(), id = m.Groups[2].Value, song = m.Groups[3].Value;
        string url = song.Length > 0 || kind == "song" ? "https://itunes.apple.com/lookup?id=" + (song.Length > 0 ? song : id) : (kind == "album" ? "https://itunes.apple.com/lookup?id=" + id + "&entity=song&limit=200" : "https://itunes.apple.com/lookup?id=" + id + "&entity=song&limit=10");
        string js; try { js = FetchHook != null ? FetchHook(url) : Get(url); } catch (Exception ex) { err = "Could not reach Apple Music (" + ex.Message + ")."; return res; }
        try
        {
            var root = MiniJson.Parse(js) as Dictionary<string, object>; object rs; if (root != null && root.TryGetValue("results", out rs) && rs is List<object>)
                foreach (var o in (List<object>)rs) { var d = o as Dictionary<string, object>; if (d == null || Str(d, "wrapperType") != "track" || Str(d, "trackName").Length == 0) continue; object ms; int secs = d.TryGetValue("trackTimeMillis", out ms) && ms is double ? (int)Math.Round((double)ms / 1000.0) : 0; Add(res, Str(d, "artistName"), Str(d, "trackName"), secs); }
        }
        catch { }
        if (res.Count == 0) err = "No songs were found in that Apple Music link (Apple Music playlists cannot be read: share the songs or the album).";
        return res;
    }
    // Deezer: a song, an album, a playlist or an artist's top songs
    static List<Song> ResolveDeezer(string link, out string err)
    {
        err = null; var res = new List<Song>(); var m = DeezerRe.Match(link); string kind = m.Groups[1].Value.ToLowerInvariant(), id = m.Groups[2].Value;
        string url = "https://api.deezer.com/" + (kind == "track" ? "track/" + id : (kind == "artist" ? "artist/" + id + "/top?limit=10" : kind + "/" + id + "/tracks?limit=300"));
        string js; try { js = FetchHook != null ? FetchHook(url) : Get(url); } catch (Exception ex) { err = "Could not reach Deezer (" + ex.Message + ")."; return res; }
        try
        {
            var root = MiniJson.Parse(js) as Dictionary<string, object>; var list = new List<object>(); object data;
            if (root != null && root.TryGetValue("data", out data) && data is List<object>) list = (List<object>)data; else if (root != null && root.ContainsKey("title")) list.Add(root);
            foreach (var o in list) { var d = o as Dictionary<string, object>; if (d == null || Str(d, "title").Length == 0) continue; object ar, du; string artist = d.TryGetValue("artist", out ar) ? Str(ar as Dictionary<string, object>, "name") : ""; int secs = d.TryGetValue("duration", out du) && du is double ? (int)(double)du : 0; Add(res, artist, Str(d, "title"), secs); }
        }
        catch { }
        if (res.Count == 0) err = "No songs were found in that Deezer link.";
        return res;
    }
    static string ExpandAny(string link, Regex want)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; string url = link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : "https://" + link.TrimStart('/');
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"; rq.Timeout = 20000; rq.AllowAutoRedirect = true;
        using (var r = (HttpWebResponse)rq.GetResponse()) { string final = r.ResponseUri.ToString(); if (want.IsMatch(final)) return final; using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) { var mm = want.Match(sr.ReadToEnd()); return mm.Success ? "https://www." + mm.Value : null; } }
    }

    // ---- finding the right song on YouTube ----
    static string Norm(string s) { return Regex.Replace(Regex.Replace((s ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", " "), @"\s+", " ").Trim(); }
    static readonly string[] Wrong = { "cover", "remix", "karaoke", "instrumental", "slowed", "reverb", "sped", "nightcore", "8d", "loop", "hour", "hours", "bass boosted", "live", "acapella", "reaction", "tutorial", "type beat" };
    // the best search result for a song: { id, title } or null with why
    public static string[] Pick(Song s, IList<string[]> results, out string why)
    {
        why = null; string core = Regex.Replace(s.Title ?? "", @"\s*[\(\[].*?[\)\]]", ""); core = Regex.Replace(core, @"\s+(feat\.?|ft\.?|with)\s.*$", "", RegexOptions.IgnoreCase);
        var words = new List<string>(); foreach (var w in Norm(core).Split(' ')) if (w.Length > 0) words.Add(w); if (words.Count == 0) foreach (var w in Norm(s.Title).Split(' ')) if (w.Length > 0) words.Add(w);
        string mainArtist = Norm((s.Artist ?? "").Split(',')[0]), spotNorm = " " + Norm(s.Title) + " ";
        string[] best = null; int bestScore = int.MinValue;
        foreach (var r in results)
        {
            if (r.Length < 4) continue; string yt = " " + Norm(r[3]) + " ", ch = " " + Norm(r[2]) + " "; int dur; int.TryParse(r[1], out dur);
            int hit = 0; foreach (var w in words) if (yt.Contains(" " + w + " ")) hit++;
            if (hit < Math.Max(1, (int)Math.Ceiling(words.Count * 0.8))) continue;   // its title must be the song's title
            int score = 0; string tight = mainArtist.Replace(" ", ""); bool artist = mainArtist.Length > 0 && (yt.Contains(" " + mainArtist + " ") || ch.Contains(" " + mainArtist + " ") || (tight.Length >= 4 && (yt.Replace(" ", "").Contains(tight) || ch.Replace(" ", "").Contains(tight)))); if (artist) score += 3;   // (the artist also as YouTube writes it: BUSYGETTINLIZZY15, LilBaby)
            if (!artist && !(words.Count >= 3 && s.Secs > 0 && dur > 0 && Math.Abs(dur - s.Secs) <= 3)) continue;   // another artist's song with the same words (Wanted by someone else) is never taken: the artist must be there, unless a long title matches to the second
            if (ch.Contains(" topic ")) score += 2;   // "Artist - Topic": the official audio YouTube makes from the release itself
            if (yt.Contains(" audio ")) score += 1; else if ((yt.Contains(" video ") || yt.Contains(" visualizer ")) && !spotNorm.Contains(" video ")) score -= 1;   // with the same length, the audio beats a music video (videos can have an intro or sound effects)
            foreach (var bad in Wrong) if (yt.Contains(" " + bad + " ") && !spotNorm.Contains(" " + bad + " ")) score -= 6;   // a cover, remix, slowed version... that the Spotify song is not
            if (s.Secs > 0 && dur > 0) { int d = Math.Abs(dur - s.Secs); score += d <= 3 ? 4 : (d <= 8 ? 2 : (d <= 20 ? 0 : -8)); }   // the length must be the song's
            else if (!artist) continue;
            if (score > bestScore) { bestScore = score; best = r; }
        }
        if (best == null || bestScore < 2) { why = "\"" + s.Key + "\" was not found on YouTube" + (results.Count == 0 ? "." : " (no result had the same title, artist and length)."); return null; }
        return new[] { best[0], best[3] };
    }
    // search YouTube (the first 10 results, titles and lengths only: a couple of seconds) and pick the song; the address to convert, or null with why
    public static Func<string, string> SearchHook;   // a test answers for YouTube
    public static string FindOnYouTube(string ytdlp, Song s, out string why, out string found)
    {
        why = null; found = null; string q = Regex.Replace((s.Artist.Length > 0 ? s.Artist + " - " : "") + s.Title, "[\"\\\\]", " ").Trim(); string outp; int code = 0;
        try { outp = SearchHook != null ? SearchHook(q) : MasterDsp.RunCapture(ytdlp, "--flat-playlist --no-warnings --print \"PXR|%(id)s|%(duration)s|%(channel)s|%(title)s\" -- \"ytsearch10:" + q + "\"", null, out code); }
        catch (Exception ex) { why = "Could not search YouTube (" + ex.Message + ")."; return null; }
        var results = new List<string[]>(); foreach (var line in (outp ?? "").Split('\n')) { var l = line.Trim(); if (!l.StartsWith("PXR|")) continue; var p = l.Substring(4).Split(new[] { '|' }, 4); if (p.Length == 4 && Regex.IsMatch(p[0], "^[A-Za-z0-9_-]{6,20}$")) results.Add(p); }
        var pick = Pick(s, results, out why); if (pick == null) return null; found = pick[1];
        return "https://www.youtube.com/watch?v=" + pick[0];
    }

    static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is string ? ((string)v).Trim() : ""; }
    static Dictionary<string, object> Dig(Dictionary<string, object> d, params string[] path) { foreach (var p in path) { object v; if (d == null || !d.TryGetValue(p, out v)) return null; d = v as Dictionary<string, object>; } return d; }
    static string Get(string url)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"; rq.Timeout = 20000; rq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
    }
    // a short link: where it leads (the address it redirects to, or an open.spotify.com address written in the page it shows)
    static string Expand(string link)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; string url = link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : "https://" + link.TrimStart('/');
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"; rq.Timeout = 20000; rq.AllowAutoRedirect = true;
        using (var r = (HttpWebResponse)rq.GetResponse())
        {
            string final = r.ResponseUri.ToString(); if (Re.IsMatch(final)) return final;
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) { var mm = Regex.Match(sr.ReadToEnd(), @"https?://open\.spotify\.com/[^""'\s<>]+"); return mm.Success ? mm.Value.Replace("\\/", "/") : null; }
        }
    }
}
