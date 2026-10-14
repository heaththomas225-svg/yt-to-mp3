using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Spotify links in the converter. Spotify's audio is locked (it cannot be downloaded), so a Spotify link is read for its songs
// (the title and the artist of a track, every track of an album or playlist, or an artist's top songs, from Spotify's public embed page: no account needed),
// and each song is then found on YouTube and converted like any other link.
static class SpotifyLinks
{
    static readonly Regex Re = new Regex(@"(?:open\.spotify\.com/(?:intl-[a-z]{2}(?:-[a-z]{2})?/)?(?:embed/)?|spotify:)(track|album|playlist|artist)[/:]([A-Za-z0-9]{10,40})", RegexOptions.IgnoreCase);
    public static bool Is(string s) { return s != null && Re.IsMatch(s); }
    public static Func<string, string> FetchHook;   // a test answers for the website

    // the songs of a Spotify link as { title shown, search for YouTube }; empty with err set when it cannot be read
    public static List<KeyValuePair<string, string>> Resolve(string link, out string err)
    {
        err = null; var res = new List<KeyValuePair<string, string>>(); var m = Re.Match(link ?? "");
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
            if (title.Length > 0) Add(res, string.Join(", ", names.ToArray()), title);
        }
        else
        {
            object tl; if (ent.TryGetValue("trackList", out tl) && tl is List<object>) foreach (var t in (List<object>)tl) { var d = t as Dictionary<string, object>; if (d != null && Str(d, "title").Length > 0) Add(res, Str(d, "subtitle"), Str(d, "title")); }
        }
        if (res.Count == 0) err = "No songs were found in that Spotify link.";
        return res;
    }
    static void Add(List<KeyValuePair<string, string>> res, string artist, string title)
    {
        string shown = (artist.Length > 0 ? artist + " - " : "") + title;
        string q = Regex.Replace(shown, "[\"\\\\]", " ").Trim();   // nothing that could break the quoted search
        res.Add(new KeyValuePair<string, string>(shown, "ytsearch1:" + q + " audio"));
    }
    static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is string ? ((string)v).Trim() : ""; }
    static Dictionary<string, object> Dig(Dictionary<string, object> d, params string[] path) { foreach (var p in path) { object v; if (d == null || !d.TryGetValue(p, out v)) return null; d = v as Dictionary<string, object>; } return d; }
    static string Get(string url)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var rq = (HttpWebRequest)WebRequest.Create(url); rq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"; rq.Timeout = 20000; rq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
    }
}
