using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// ======================================================================
//  MUSIC HUB: the services, their official web players and what each one allows.
//  Every service opens its own official web player inside Purphex (you sign in on the service's own page). Three services also have an
//  official API that lists your library in Purphex itself (playlists, liked songs, albums, artists, recently played): Spotify, YouTube and
//  SoundCloud. Those need a free developer app of your own (a Client ID) and you sign in through the service's official page in your
//  browser (OAuth with PKCE); the tokens are kept encrypted for your Windows user, and DISCONNECT deletes them.
//  Search with no account at all uses the public catalogues of Deezer and Apple (iTunes Search).
// ======================================================================
sealed class HubService
{
    public string Id, Name, Home, SearchUrl, Note; public Color Col; public string[] Origins; public string CookieUrl, LoginCookie;   // LoginCookie: the cookie that is there while you are signed in (null: the service keeps it elsewhere)
    public string[][] Links;           // {label, url}: straight to your library pages on the service
    public string[] PlaySel, NextSel, PrevSel, PagePlaySel;   // the service's own buttons (play / pause, next, previous, play this page)
    public string Api;                 // "spotify", "youtube", "soundcloud": it has an official API for your library (with your own developer app)
    public string Search(string q) { return SearchUrl.Replace("{q}", Uri.EscapeDataString(q ?? "")); }
}

static class HubServices
{
    static Color C(int r, int g, int b) { return Color.FromArgb(255, r, g, b); }
    static string[][] L(params string[] a) { var l = new List<string[]>(); for (int i = 0; i + 1 < a.Length; i += 2) l.Add(new[] { a[i], a[i + 1] }); return l.ToArray(); }
    public static readonly HubService[] All = {
        new HubService { Id = "ytmusic", Name = "YouTube Music", Col = C(255, 60, 60), Home = "https://music.youtube.com/", SearchUrl = "https://music.youtube.com/search?q={q}",
            Origins = new[] { "https://music.youtube.com", "https://www.youtube.com", "https://accounts.google.com" }, CookieUrl = "https://www.youtube.com", LoginCookie = "LOGIN_INFO",
            Links = L("Home", "https://music.youtube.com/", "Library", "https://music.youtube.com/library", "Liked", "https://music.youtube.com/playlist?list=LM", "Playlists", "https://music.youtube.com/library/playlists", "History", "https://music.youtube.com/history", "Explore", "https://music.youtube.com/explore"),
            PlaySel = new[] { "#play-pause-button" }, NextSel = new[] { ".next-button" }, PrevSel = new[] { ".previous-button" }, PagePlaySel = new string[0], Api = "youtube",
            Note = "Plays inside Purphex. Google may not let you sign in from an app window: if it says so, music still plays without signing in, or connect YouTube's API under CONNECT ACCOUNTS for your playlists and liked songs." },
        new HubService { Id = "youtube", Name = "YouTube", Col = C(255, 40, 40), Home = "https://www.youtube.com/", SearchUrl = "https://www.youtube.com/results?search_query={q}",
            Origins = new[] { "https://www.youtube.com", "https://accounts.google.com" }, CookieUrl = "https://www.youtube.com", LoginCookie = "LOGIN_INFO",
            Links = L("Home", "https://www.youtube.com/", "Subscriptions", "https://www.youtube.com/feed/subscriptions", "Liked", "https://www.youtube.com/playlist?list=LL", "Playlists", "https://www.youtube.com/feed/playlists", "History", "https://www.youtube.com/feed/history", "Type beats", "https://www.youtube.com/results?search_query=type+beat"),
            PlaySel = new[] { ".ytp-play-button" }, NextSel = new[] { ".ytp-next-button" }, PrevSel = new string[0], PagePlaySel = new string[0], Api = "youtube",
            Note = "Plays inside Purphex, videos too. If Google will not sign you in here, everything still plays without signing in." },
        new HubService { Id = "soundcloud", Name = "SoundCloud", Col = C(255, 120, 30), Home = "https://soundcloud.com/discover", SearchUrl = "https://soundcloud.com/search?q={q}",
            Origins = new[] { "https://soundcloud.com", "https://secure.soundcloud.com" }, CookieUrl = "https://soundcloud.com", LoginCookie = "oauth_token",
            Links = L("Discover", "https://soundcloud.com/discover", "Feed", "https://soundcloud.com/feed", "Library", "https://soundcloud.com/you/library", "Likes", "https://soundcloud.com/you/likes", "Playlists", "https://soundcloud.com/you/sets", "History", "https://soundcloud.com/you/history"),
            PlaySel = new[] { ".playControl" }, NextSel = new[] { ".skipControl__next" }, PrevSel = new[] { ".skipControl__previous" }, PagePlaySel = new[] { ".sc-button-play.sc-button-xlarge", ".fullHero__title .sc-button-play", ".sc-button-play" }, Api = "soundcloud",
            Note = "Plays inside Purphex with your account (likes, playlists, feed)." },
        new HubService { Id = "spotify", Name = "Spotify", Col = C(30, 215, 96), Home = "https://open.spotify.com/", SearchUrl = "https://open.spotify.com/search/{q}",
            Origins = new[] { "https://open.spotify.com", "https://accounts.spotify.com", "https://www.spotify.com" }, CookieUrl = "https://open.spotify.com", LoginCookie = "sp_dc",
            Links = L("Home", "https://open.spotify.com/", "Liked Songs", "https://open.spotify.com/collection/tracks", "Playlists", "https://open.spotify.com/collection/playlists", "Albums", "https://open.spotify.com/collection/albums", "Artists", "https://open.spotify.com/collection/artists", "Podcasts", "https://open.spotify.com/collection/podcasts"),
            PlaySel = new[] { "[data-testid=\"control-button-playpause\"]" }, NextSel = new[] { "[data-testid=\"control-button-skip-forward\"]" }, PrevSel = new[] { "[data-testid=\"control-button-skip-back\"]" }, PagePlaySel = new[] { "[data-testid=\"action-bar-row\"] [data-testid=\"play-button\"]", "[data-testid=\"play-button\"]" }, Api = "spotify",
            Note = "Spotify's own web player inside Purphex: full songs with Premium, with ads and shuffle limits on the free plan (Spotify's rules)." },
        new HubService { Id = "apple", Name = "Apple Music", Col = C(250, 60, 90), Home = "https://music.apple.com/", SearchUrl = "https://music.apple.com/search?term={q}",
            Origins = new[] { "https://music.apple.com", "https://idmsa.apple.com", "https://authorize.music.apple.com" }, CookieUrl = "https://music.apple.com", LoginCookie = "media-user-token",
            Links = L("Home", "https://music.apple.com/", "Songs", "https://music.apple.com/library/songs", "Playlists", "https://music.apple.com/library/playlists", "Albums", "https://music.apple.com/library/albums", "Recently added", "https://music.apple.com/library/recently-added", "Browse", "https://music.apple.com/browse"),
            PlaySel = new[] { ".playback-play__play", ".playback-play__pause", "button[data-testid=\"play-pause-button\"]" }, NextSel = new[] { ".playback-next", "button[data-testid=\"next-button\"]" }, PrevSel = new[] { ".playback-previous", "button[data-testid=\"previous-button\"]" }, PagePlaySel = new[] { "button[data-testid=\"play-button\"]", ".play-button" },
            Note = "Apple Music's web player inside Purphex: full songs with a subscription, 30-second previews without." },
        new HubService { Id = "deezer", Name = "Deezer", Col = C(160, 56, 255), Home = "https://www.deezer.com/", SearchUrl = "https://www.deezer.com/search/{q}",
            Origins = new[] { "https://www.deezer.com", "https://account.deezer.com", "https://auth.deezer.com" }, CookieUrl = "https://www.deezer.com", LoginCookie = "arl",
            Links = L("Home", "https://www.deezer.com/", "Explore", "https://www.deezer.com/en/channels/explore"),   // your favourites are under your name in Deezer (its address has your user number)
            PlaySel = new[] { "[data-testid=\"play_button_pause\"]", "[data-testid=\"play_button_play\"]" }, NextSel = new[] { "[data-testid=\"next_track_button\"]" }, PrevSel = new[] { "[data-testid=\"previous_track_button\"]" }, PagePlaySel = new[] { "[data-testid=\"play\"]" },
            Note = "Deezer's web player inside Purphex (Deezer's free plan has limits). Search also finds Deezer songs with no account." },
        new HubService { Id = "tidal", Name = "Tidal", Col = C(80, 220, 230), Home = "https://listen.tidal.com/", SearchUrl = "https://listen.tidal.com/search?q={q}",
            Origins = new[] { "https://listen.tidal.com", "https://login.tidal.com" }, CookieUrl = "https://listen.tidal.com", LoginCookie = null,
            Links = L("Home", "https://listen.tidal.com/", "Tracks", "https://listen.tidal.com/my-collection/tracks", "Playlists", "https://listen.tidal.com/my-collection/playlists", "Albums", "https://listen.tidal.com/my-collection/albums", "Artists", "https://listen.tidal.com/my-collection/artists"),
            PlaySel = new[] { "[data-test=\"pause\"]", "[data-test=\"play\"]" }, NextSel = new[] { "[data-test=\"next\"]" }, PrevSel = new[] { "[data-test=\"previous\"]" }, PagePlaySel = new string[0],
            Note = "Tidal's web player inside Purphex (needs a Tidal subscription to play)." },
        new HubService { Id = "amazon", Name = "Amazon Music", Col = C(37, 209, 218), Home = "https://music.amazon.com/", SearchUrl = "https://music.amazon.com/search/{q}",
            Origins = new[] { "https://music.amazon.com", "https://www.amazon.com" }, CookieUrl = "https://www.amazon.com", LoginCookie = "at-main",
            Links = L("Home", "https://music.amazon.com/", "Library", "https://music.amazon.com/my/library", "Playlists", "https://music.amazon.com/my/playlists", "Songs", "https://music.amazon.com/my/songs", "Albums", "https://music.amazon.com/my/albums"),
            PlaySel = new string[0], NextSel = new string[0], PrevSel = new string[0], PagePlaySel = new string[0],
            Note = "Amazon Music's web player inside Purphex (Prime or Music Unlimited). Use its own buttons for next and previous." },
        new HubService { Id = "bandcamp", Name = "Bandcamp", Col = C(98, 154, 169), Home = "https://bandcamp.com/", SearchUrl = "https://bandcamp.com/search?q={q}",
            Origins = new[] { "https://bandcamp.com" }, CookieUrl = "https://bandcamp.com", LoginCookie = "identity",
            Links = L("Discover", "https://bandcamp.com/discover", "Feed", "https://bandcamp.com/feed"),
            PlaySel = new[] { ".playbutton" }, NextSel = new[] { ".nextbutton" }, PrevSel = new[] { ".prevbutton" }, PagePlaySel = new[] { ".playbutton" },
            Note = "Bandcamp inside Purphex: full streams of what artists let you hear, and your collection." },
        new HubService { Id = "beatstars", Name = "BeatStars", Col = C(255, 70, 70), Home = "https://www.beatstars.com/", SearchUrl = "https://www.beatstars.com/search?q={q}",
            Origins = new[] { "https://www.beatstars.com" }, CookieUrl = "https://www.beatstars.com", LoginCookie = null,
            Links = L("Home", "https://www.beatstars.com/", "Top charts", "https://www.beatstars.com/top-charts"),
            PlaySel = new string[0], NextSel = new string[0], PrevSel = new string[0], PagePlaySel = new string[0],
            Note = "Find beats: BeatStars inside Purphex. Buy or lease them on BeatStars as usual." },
        new HubService { Id = "audiomack", Name = "Audiomack", Col = C(255, 165, 0), Home = "https://audiomack.com/", SearchUrl = "https://audiomack.com/search?q={q}",
            Origins = new[] { "https://audiomack.com" }, CookieUrl = "https://audiomack.com", LoginCookie = null,
            Links = L("Home", "https://audiomack.com/", "Trending", "https://audiomack.com/trending-now"),
            PlaySel = new string[0], NextSel = new string[0], PrevSel = new string[0], PagePlaySel = new string[0],
            Note = "Audiomack inside Purphex." },
        new HubService { Id = "mixcloud", Name = "Mixcloud", Col = C(80, 140, 255), Home = "https://www.mixcloud.com/", SearchUrl = "https://www.mixcloud.com/search/?q={q}",
            Origins = new[] { "https://www.mixcloud.com" }, CookieUrl = "https://www.mixcloud.com", LoginCookie = null,
            Links = L("Home", "https://www.mixcloud.com/", "Discover", "https://www.mixcloud.com/discover/"),
            PlaySel = new string[0], NextSel = new string[0], PrevSel = new string[0], PagePlaySel = new string[0],
            Note = "Mixcloud's DJ mixes and radio shows inside Purphex." },
    };
    public static HubService Get(string id) { foreach (var s in All) if (s.Id == id) return s; return null; }
}

// a song, playlist, album or artist from an API or a catalogue
sealed class HubItem
{
    public string Service = "", Kind = "track", Title = "", Sub = "", Art = "", Open = "", Id = "", Section = "";   // Open: the page of it on the service (played in the hub)
}

// ---------------- keys and tokens: encrypted for this Windows user ----------------
static class HubVault
{
    static string P(string n) { return Path.Combine(StudioEnv.DataDir, n); }
    static Dictionary<string, string> Read(string file)
    {
        var d = new Dictionary<string, string>();
        try { if (!File.Exists(P(file))) return d; var txt = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(P(file)), null, DataProtectionScope.CurrentUser)); foreach (var ln in txt.Split('\n')) { int i = ln.IndexOf('='); if (i > 0) d[ln.Substring(0, i)] = Uri.UnescapeDataString(ln.Substring(i + 1)); } } catch { }
        return d;
    }
    static void Write(string file, Dictionary<string, string> d)
    {
        var sb = new StringBuilder(); foreach (var kv in d) if (!string.IsNullOrEmpty(kv.Value)) sb.Append(kv.Key).Append('=').Append(Uri.EscapeDataString(kv.Value)).Append('\n');
        Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllBytes(P(file), ProtectedData.Protect(Encoding.UTF8.GetBytes(sb.ToString()), null, DataProtectionScope.CurrentUser));
    }
    static readonly object lk = new object();
    public static string Get(string key) { lock (lk) { string v; return Read("hubvault.dat").TryGetValue(key, out v) ? v : ""; } }
    public static void Set(string key, string val) { lock (lk) { var d = Read("hubvault.dat"); if (string.IsNullOrEmpty(val)) d.Remove(key); else d[key] = val; Write("hubvault.dat", d); } }
    public static void Forget(string prefix) { lock (lk) { var d = Read("hubvault.dat"); foreach (var k in d.Keys.ToList()) if (k.StartsWith(prefix, StringComparison.Ordinal)) d.Remove(k); Write("hubvault.dat", d); } }
    public static bool FileIsEncrypted() { try { var b = File.ReadAllBytes(P("hubvault.dat")); return Encoding.UTF8.GetString(b).IndexOf("token", StringComparison.Ordinal) < 0; } catch { return true; } }
}

// ---------------- the official APIs (your own developer app, OAuth 2 with PKCE) ----------------
sealed class HubApi
{
    public string Id, Name, AuthUrl, TokenUrl, Scope, Header = "Bearer", SetupUrl, Setup; public bool NeedsSecret; public string[] ExtraAuth = new string[0];
    public const int Port = 43821; public static string Redirect { get { return "http://127.0.0.1:" + Port + "/callback"; } }
    public static readonly HubApi[] All = {
        new HubApi { Id = "spotify", Name = "Spotify", AuthUrl = "https://accounts.spotify.com/authorize", TokenUrl = "https://accounts.spotify.com/api/token",
            Scope = "user-library-read playlist-read-private playlist-read-collaborative user-follow-read user-read-recently-played user-top-read", SetupUrl = "https://developer.spotify.com/dashboard",
            Setup = "developer.spotify.com/dashboard: Create app, tick Web API, add the Redirect URI below, then copy its Client ID here." },
        new HubApi { Id = "youtube", Name = "YouTube", AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth", TokenUrl = "https://oauth2.googleapis.com/token", NeedsSecret = true,
            Scope = "https://www.googleapis.com/auth/youtube.readonly", ExtraAuth = new[] { "access_type", "offline", "prompt", "consent" }, SetupUrl = "https://console.cloud.google.com/apis/credentials",
            Setup = "console.cloud.google.com: turn on YouTube Data API v3, make an OAuth client of type Desktop app, then copy its Client ID and Client secret here." },
        new HubApi { Id = "soundcloud", Name = "SoundCloud", AuthUrl = "https://secure.soundcloud.com/authorize", TokenUrl = "https://secure.soundcloud.com/oauth/token", NeedsSecret = true, Header = "OAuth",
            Scope = "", SetupUrl = "https://soundcloud.com/you/apps",
            Setup = "soundcloud.com/you/apps: register an app (SoundCloud reviews new apps), add the Redirect URI below, then copy its Client ID and Client secret here." },
    };
    public static HubApi Get(string id) { foreach (var a in All) if (a.Id == id) return a; return null; }
    public string ClientId { get { return HubVault.Get(Id + ".id"); } }
    public string Secret { get { return HubVault.Get(Id + ".secret"); } }
    public bool Configured { get { return ClientId.Length > 0 && (!NeedsSecret || Secret.Length > 0); } }
    public bool Connected { get { return HubVault.Get(Id + ".refresh").Length > 0 || HubVault.Get(Id + ".access").Length > 0; } }
    public void Disconnect() { HubVault.Set(Id + ".access", ""); HubVault.Set(Id + ".refresh", ""); HubVault.Set(Id + ".expires", ""); }

    public static string B64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    public static string Challenge(string verifier) { using (var sha = SHA256.Create()) return B64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier))); }
    static string Rand(int n) { var b = new byte[n]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b); return B64Url(b); }
    public string AuthorizeUrl(string challenge, string state)
    {
        var q = new List<string> { "client_id=" + Uri.EscapeDataString(ClientId), "response_type=code", "redirect_uri=" + Uri.EscapeDataString(Redirect), "code_challenge_method=S256", "code_challenge=" + challenge, "state=" + state };
        if (Scope.Length > 0) q.Add("scope=" + Uri.EscapeDataString(Scope)); for (int i = 0; i + 1 < ExtraAuth.Length; i += 2) q.Add(ExtraAuth[i] + "=" + ExtraAuth[i + 1]);
        return AuthUrl + "?" + string.Join("&", q);
    }
    public static Func<string, string, string> PostHook;   // tests answer for the token server
    static string Post(string url, string form)
    {
        if (PostHook != null) return PostHook(url, form);
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; var req = (HttpWebRequest)WebRequest.Create(url); req.Method = "POST"; req.ContentType = "application/x-www-form-urlencoded"; req.Accept = "application/json"; req.Timeout = 20000;
        var body = Encoding.ASCII.GetBytes(form); using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
        try { using (var r = (HttpWebResponse)req.GetResponse()) using (var rd = new StreamReader(r.GetResponseStream())) return rd.ReadToEnd(); }
        catch (WebException ex) { string msg = ex.Message; try { using (var rd = new StreamReader(ex.Response.GetResponseStream())) msg = rd.ReadToEnd(); } catch { } throw new Exception("The sign-in server said no: " + msg); }
    }
    void SaveToken(string json)
    {
        var d = MiniJson.Parse(json) as Dictionary<string, object>; if (d == null || !d.ContainsKey("access_token")) throw new Exception("No token came back: " + json);
        HubVault.Set(Id + ".access", Convert.ToString(d["access_token"])); if (d.ContainsKey("refresh_token") && d["refresh_token"] != null) HubVault.Set(Id + ".refresh", Convert.ToString(d["refresh_token"]));
        double exp = d.ContainsKey("expires_in") && d["expires_in"] is double ? (double)d["expires_in"] : 3600; HubVault.Set(Id + ".expires", DateTime.UtcNow.AddSeconds(exp - 60).Ticks.ToString());
    }
    public void Exchange(string code, string verifier)
    {
        string form = "grant_type=authorization_code&code=" + Uri.EscapeDataString(code) + "&redirect_uri=" + Uri.EscapeDataString(Redirect) + "&client_id=" + Uri.EscapeDataString(ClientId) + "&code_verifier=" + Uri.EscapeDataString(verifier) + (NeedsSecret ? "&client_secret=" + Uri.EscapeDataString(Secret) : "");
        SaveToken(Post(TokenUrl, form));
    }
    // a token that works now (refreshed when it has run out)
    public string Token()
    {
        string acc = HubVault.Get(Id + ".access"); long ex; long.TryParse(HubVault.Get(Id + ".expires"), out ex);
        if (acc.Length > 0 && DateTime.UtcNow.Ticks < ex) return acc;
        string rf = HubVault.Get(Id + ".refresh"); if (rf.Length == 0) return acc;
        string form = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(rf) + "&client_id=" + Uri.EscapeDataString(ClientId) + (NeedsSecret ? "&client_secret=" + Uri.EscapeDataString(Secret) : "");
        SaveToken(Post(TokenUrl, form)); return HubVault.Get(Id + ".access");
    }
    // CONNECT: your browser opens the service's own sign-in page; it sends you back to Purphex (127.0.0.1) with a one-time code
    public void Connect(Action<string> done)
    {
        string verifier = Rand(48), state = Rand(16), url = AuthorizeUrl(Challenge(verifier), state);
        new Thread(() =>
        {
            string err = null; TcpListener l = null;
            try
            {
                l = new TcpListener(IPAddress.Loopback, Port); l.Start();
                try { System.Diagnostics.Process.Start(url); } catch { throw new Exception("Could not open your browser."); }
                var until = DateTime.UtcNow.AddMinutes(4); string code = null;
                while (code == null)
                {
                    if (DateTime.UtcNow > until) throw new Exception("No answer from the sign-in page within 4 minutes. Press CONNECT to try again.");
                    if (!l.Pending()) { Thread.Sleep(150); continue; }
                    using (var c = l.AcceptTcpClient()) using (var ns = c.GetStream())
                    {
                        ns.ReadTimeout = 5000; var buf = new byte[8192]; int n = ns.Read(buf, 0, buf.Length); string req = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n)); string line = req.Split('\n')[0];
                        string path = line.Split(' ').Length > 1 ? line.Split(' ')[1] : ""; var qs = Query(path); bool ok = path.StartsWith("/callback") && qs.ContainsKey("state") && qs["state"] == state;
                        string msg = !path.StartsWith("/callback") ? "" : qs.ContainsKey("error") ? "Not connected: " + qs["error"] : ok && qs.ContainsKey("code") ? "Connected. You can close this tab and go back to Purphex Music." : "That answer did not match. Press CONNECT again in Purphex.";
                        string html = "<html><body style=\"background:#0a0518;color:#efe8ff;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh\"><h2>" + WebUtilityEncode(msg) + "</h2></body></html>";
                        var resp = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(html) + "\r\n\r\n" + html); ns.Write(resp, 0, resp.Length);
                        if (!path.StartsWith("/callback")) continue;
                        if (qs.ContainsKey("error")) throw new Exception(Name + " said: " + qs["error"]);
                        if (!ok || !qs.ContainsKey("code")) throw new Exception("The answer from " + Name + " did not match this sign-in (try again).");
                        code = qs["code"];
                    }
                }
                Exchange(code, verifier);
            }
            catch (SocketException) { err = "Port " + Port + " is in use by another program, so " + Name + " cannot send you back to Purphex."; }
            catch (Exception ex) { err = ex.Message; }
            finally { try { if (l != null) l.Stop(); } catch { } }
            done(err);
        }) { IsBackground = true, Name = "hub-connect" }.Start();
    }
    static string WebUtilityEncode(string s) { return System.Net.WebUtility.HtmlEncode(s ?? ""); }
    public static Dictionary<string, string> Query(string path)
    {
        var d = new Dictionary<string, string>(); int q = path.IndexOf('?'); if (q < 0) return d;
        foreach (var kv in path.Substring(q + 1).Split('&')) { int e = kv.IndexOf('='); if (e > 0) d[Uri.UnescapeDataString(kv.Substring(0, e))] = Uri.UnescapeDataString(kv.Substring(e + 1).Replace('+', ' ')); }
        return d;
    }
    public static Func<string, string> GetHook;   // tests answer for the API
    public string GetJson(string url)
    {
        if (GetHook != null) return GetHook(url);
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; var req = (HttpWebRequest)WebRequest.Create(url); req.Accept = "application/json"; req.Timeout = 20000; req.UserAgent = "PurphexMusic";
        req.Headers["Authorization"] = Header + " " + Token();
        try { using (var r = (HttpWebResponse)req.GetResponse()) using (var rd = new StreamReader(r.GetResponseStream())) return rd.ReadToEnd(); }
        catch (WebException ex)
        {
            var hr = ex.Response as HttpWebResponse; int code = hr == null ? 0 : (int)hr.StatusCode;
            if (code == 401) throw new Exception(Name + " signed you out (the sign-in ran out). Press CONNECT again.");
            if (code == 403) throw new Exception(Name + " did not allow this (your developer app may still need approval, or the account has to be added as a user of it).");
            if (code == 429) throw new Exception(Name + " says too many requests: wait a minute.");
            throw new Exception("Could not reach " + Name + ": " + ex.Message);
        }
    }

    // ---- what each API lists ----
    public static readonly string[] Sections = { "Playlists", "Liked", "Albums", "Artists", "Recent", "For you" };
    public List<HubItem> Load(string section)
    {
        var l = new List<HubItem>();
        if (Id == "spotify")
        {
            switch (section)
            {
                case "Playlists": foreach (var p in Arr(Js(GetJson("https://api.spotify.com/v1/me/playlists?limit=50")), "items")) l.Add(SpPlaylist(p)); break;
                case "Liked": foreach (var p in Arr(Js(GetJson("https://api.spotify.com/v1/me/tracks?limit=50")), "items")) l.Add(SpTrack(Obj(p, "track"))); break;
                case "Albums": foreach (var p in Arr(Js(GetJson("https://api.spotify.com/v1/me/albums?limit=50")), "items")) l.Add(SpAlbum(Obj(p, "album"))); break;
                case "Artists": foreach (var p in Arr(Obj(Js(GetJson("https://api.spotify.com/v1/me/following?type=artist&limit=50")), "artists"), "items")) l.Add(SpArtist(p)); break;
                case "Recent": foreach (var p in Arr(Js(GetJson("https://api.spotify.com/v1/me/player/recently-played?limit=50")), "items")) l.Add(SpTrack(Obj(p, "track"))); break;
                default: foreach (var p in Arr(Js(GetJson("https://api.spotify.com/v1/me/top/tracks?limit=50")), "items")) l.Add(SpTrack(p)); break;
            }
        }
        else if (Id == "youtube")
        {
            switch (section)
            {
                case "Playlists": foreach (var p in Arr(Js(GetJson("https://www.googleapis.com/youtube/v3/playlists?part=snippet,contentDetails&mine=true&maxResults=50")), "items")) { var sn = Obj(p, "snippet"); l.Add(new HubItem { Service = "ytmusic", Kind = "playlist", Title = S(sn, "title"), Sub = S(sn, "channelTitle"), Art = YtThumb(sn), Id = S(p, "id"), Open = "https://music.youtube.com/playlist?list=" + S(p, "id") }); } break;
                case "Liked": foreach (var p in Arr(Js(GetJson("https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&playlistId=LL&maxResults=50")), "items")) { var sn = Obj(p, "snippet"); string vid = S(Obj(sn, "resourceId"), "videoId"); l.Add(new HubItem { Service = "ytmusic", Title = S(sn, "title"), Sub = S(sn, "videoOwnerChannelTitle"), Art = YtThumb(sn), Id = vid, Open = "https://music.youtube.com/watch?v=" + vid }); } break;
                case "Artists": foreach (var p in Arr(Js(GetJson("https://www.googleapis.com/youtube/v3/subscriptions?part=snippet&mine=true&maxResults=50")), "items")) { var sn = Obj(p, "snippet"); string ch = S(Obj(sn, "resourceId"), "channelId"); l.Add(new HubItem { Service = "youtube", Kind = "artist", Title = S(sn, "title"), Art = YtThumb(sn), Id = ch, Open = "https://www.youtube.com/channel/" + ch }); } break;
                default: break;   // albums, recently played and recommendations are not in YouTube's public API
            }
        }
        else if (Id == "soundcloud")
        {
            switch (section)
            {
                case "Playlists": foreach (var p in ScList(GetJson("https://api.soundcloud.com/me/playlists?limit=50&linked_partitioning=true"))) l.Add(new HubItem { Service = "soundcloud", Kind = "playlist", Title = S(p, "title"), Sub = S(Obj(p, "user"), "username"), Art = ScArt(S(p, "artwork_url")), Open = S(p, "permalink_url") }); break;
                case "Liked": foreach (var p in ScList(GetJson("https://api.soundcloud.com/me/likes/tracks?limit=50&linked_partitioning=true"))) l.Add(ScTrack(p)); break;
                case "Artists": foreach (var p in ScList(GetJson("https://api.soundcloud.com/me/followings?limit=50&linked_partitioning=true"))) l.Add(new HubItem { Service = "soundcloud", Kind = "artist", Title = S(p, "username"), Art = ScArt(S(p, "avatar_url")), Open = S(p, "permalink_url") }); break;
                case "For you": foreach (var p in ScList(GetJson("https://api.soundcloud.com/me/feed/tracks?limit=50&linked_partitioning=true"))) { var o = Obj(p, "origin") ?? p; l.Add(ScTrack(o)); } break;
                default: break;
            }
        }
        foreach (var it in l) it.Section = section; l.RemoveAll(x => x.Title.Length == 0); return l;
    }
    public bool Has(string section)
    {
        if (Id == "spotify") return true; if (Id == "youtube") return section == "Playlists" || section == "Liked" || section == "Artists";
        return section == "Playlists" || section == "Liked" || section == "Artists" || section == "For you";
    }
    public List<HubItem> Search(string q)
    {
        var l = new List<HubItem>(); string e = Uri.EscapeDataString(q);
        if (Id == "spotify") { var d = Js(GetJson("https://api.spotify.com/v1/search?type=track,album,playlist,artist&limit=12&q=" + e)); foreach (var p in Arr(Obj(d, "tracks"), "items")) l.Add(SpTrack(p)); foreach (var p in Arr(Obj(d, "albums"), "items")) l.Add(SpAlbum(p)); foreach (var p in Arr(Obj(d, "playlists"), "items")) if (p != null) l.Add(SpPlaylist(p)); foreach (var p in Arr(Obj(d, "artists"), "items")) l.Add(SpArtist(p)); }
        else if (Id == "youtube") { foreach (var p in Arr(Js(GetJson("https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&videoCategoryId=10&maxResults=20&q=" + e)), "items")) { var sn = Obj(p, "snippet"); string vid = S(Obj(p, "id"), "videoId"); l.Add(new HubItem { Service = "ytmusic", Title = System.Net.WebUtility.HtmlDecode(S(sn, "title")), Sub = S(sn, "channelTitle"), Art = YtThumb(sn), Id = vid, Open = "https://music.youtube.com/watch?v=" + vid }); } }
        else if (Id == "soundcloud") { foreach (var p in ScList(GetJson("https://api.soundcloud.com/tracks?limit=20&linked_partitioning=true&q=" + e))) l.Add(ScTrack(p)); }
        l.RemoveAll(x => x.Title.Length == 0); return l;
    }
    // ---- reading the answers ----
    static object Js(string s) { return MiniJson.Parse(s); }
    public static Dictionary<string, object> Obj(object o, string k) { var d = o as Dictionary<string, object>; object v; return d != null && d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null; }
    public static List<object> Arr(object o, string k) { var d = o as Dictionary<string, object>; object v; return d != null && d.TryGetValue(k, out v) && v is List<object> ? (List<object>)v : new List<object>(); }
    public static string S(object o, string k) { var d = o as Dictionary<string, object>; object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : ""; }
    static string Img(object o) { var imgs = Arr(o, "images"); return imgs.Count > 0 ? S(imgs[imgs.Count > 1 ? 1 : 0], "url") : ""; }
    static string Artists(object o) { return string.Join(", ", Arr(o, "artists").Select(a => S(a, "name")).Where(x => x.Length > 0)); }
    static HubItem SpTrack(object t) { return new HubItem { Service = "spotify", Title = S(t, "name"), Sub = Artists(t), Art = Img(Obj(t, "album")), Id = S(t, "id"), Open = S(Obj(t, "external_urls"), "spotify") }; }
    static HubItem SpAlbum(object a) { return new HubItem { Service = "spotify", Kind = "album", Title = S(a, "name"), Sub = Artists(a), Art = Img(a), Id = S(a, "id"), Open = S(Obj(a, "external_urls"), "spotify") }; }
    static HubItem SpPlaylist(object p) { return new HubItem { Service = "spotify", Kind = "playlist", Title = S(p, "name"), Sub = S(Obj(p, "owner"), "display_name"), Art = Img(p), Id = S(p, "id"), Open = S(Obj(p, "external_urls"), "spotify") }; }
    static HubItem SpArtist(object a) { return new HubItem { Service = "spotify", Kind = "artist", Title = S(a, "name"), Art = Img(a), Id = S(a, "id"), Open = S(Obj(a, "external_urls"), "spotify") }; }
    static string YtThumb(object sn) { var th = Obj(sn, "thumbnails"); return S(Obj(th, "medium") ?? Obj(th, "default"), "url"); }
    static List<object> ScList(string json) { var o = MiniJson.Parse(json); if (o is List<object>) return (List<object>)o; return Arr(o, "collection"); }
    static string ScArt(string u) { return (u ?? "").Replace("-large.", "-t300x300."); }
    static HubItem ScTrack(object t) { return new HubItem { Service = "soundcloud", Title = S(t, "title"), Sub = S(Obj(t, "user"), "username"), Art = ScArt(S(t, "artwork_url")), Id = S(t, "id"), Open = S(t, "permalink_url") }; }
}

// ---------------- search with no account: the public catalogues of Deezer and Apple ----------------
static class HubCatalog
{
    public static Func<string, string> GetHook;
    static string Get(string url) { if (GetHook != null) return GetHook(url); ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "PurphexMusic"; wc.Encoding = Encoding.UTF8; return wc.DownloadString(url); } }
    public static List<HubItem> Search(string q)
    {
        var l = new List<HubItem>(); string e = Uri.EscapeDataString(q ?? ""); if (e.Length == 0) return l;
        try { foreach (var t in HubApi.Arr(MiniJson.Parse(Get("https://api.deezer.com/search?limit=15&q=" + e)), "data")) l.Add(new HubItem { Service = "deezer", Title = HubApi.S(t, "title"), Sub = HubApi.S(HubApi.Obj(t, "artist"), "name"), Art = HubApi.S(HubApi.Obj(t, "album"), "cover_medium"), Id = HubApi.S(t, "id"), Open = HubApi.S(t, "link"), Section = "Search" }); } catch { }
        try { foreach (var t in HubApi.Arr(MiniJson.Parse(Get("https://itunes.apple.com/search?media=music&entity=song&limit=15&term=" + e)), "results")) l.Add(new HubItem { Service = "apple", Title = HubApi.S(t, "trackName"), Sub = HubApi.S(t, "artistName"), Art = HubApi.S(t, "artworkUrl100").Replace("100x100", "300x300"), Id = HubApi.S(t, "trackId"), Open = HubApi.S(t, "trackViewUrl"), Section = "Search" }); } catch { }
        l.RemoveAll(x => x.Title.Length == 0 || !x.Open.StartsWith("https://", StringComparison.Ordinal)); return l;
    }
}

// ---------------- the pictures (album covers), loaded in the background and kept for a while ----------------
static class HubArt
{
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(); static readonly HashSet<string> busy = new HashSet<string>(); static readonly object lk = new object();
    public static Action Loaded = delegate { };
    public static Bitmap Get(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.Ordinal) || StudioEnv.UnderTest) return null;
        lock (lk) { Bitmap b; if (cache.TryGetValue(url, out b)) return b; if (busy.Contains(url) || busy.Count > 12) return null; busy.Add(url); }
        ThreadPool.QueueUserWorkItem(o =>
        {
            Bitmap bm = null;
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; using (var wc = new WebClient()) { var data = wc.DownloadData(url); if (data.Length < 4 * 1024 * 1024) using (var ms = new MemoryStream(data)) using (var im = Image.FromStream(ms)) { bm = new Bitmap(160, 160); using (var g = Graphics.FromImage(bm)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; double k = Math.Max(160.0 / im.Width, 160.0 / im.Height); float w = (float)(im.Width * k), h = (float)(im.Height * k); g.DrawImage(im, (160 - w) / 2, (160 - h) / 2, w, h); } } } } catch { }
            lock (lk) { busy.Remove(url); if (cache.Count > 400) { foreach (var b in cache.Values) if (b != null) b.Dispose(); cache.Clear(); } cache[url] = bm; }
            Loaded();
        });
        return null;
    }
}

// ---------------- the scripts that read and drive the player in the page ----------------
static class HubPlayerJs
{
    // what is playing: the page's own media session (title, artist, cover) and its audio or video element (playing, position, length, volume)
    public const string Poll = "(()=>{try{const ms=navigator.mediaSession&&navigator.mediaSession.metadata;const els=[...document.querySelectorAll('video,audio')];let m=els.find(e=>!e.paused&&!e.ended)||els.find(e=>e.currentSrc&&e.duration>0)||null;let art='';if(ms&&ms.artwork&&ms.artwork.length)art=ms.artwork[ms.artwork.length-1].src||'';return JSON.stringify({t:ms?ms.title||'':'',a:ms?ms.artist||'':'',art:art,p:m?(!m.paused&&!m.ended):false,end:m?m.ended:false,pos:m?m.currentTime:0,dur:m&&isFinite(m.duration)?m.duration:0,v:m?m.volume:-1,has:!!m})}catch(e){return '{}'}})()";
    static string Arr(string[] sel) { var sb = new StringBuilder("["); for (int i = 0; i < sel.Length; i++) sb.Append(i > 0 ? "," : "").Append(Str(sel[i])); return sb.Append(']').ToString(); }
    public static string Str(string s) { return "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'") + "'"; }
    const string Click = "function click(l){for(const s of l){const b=document.querySelector(s);if(b&&b.offsetParent!==null){b.click();return true}}return false}";
    const string Media = "const els=[...document.querySelectorAll('video,audio')];const m=els.find(e=>!e.paused)||els.find(e=>e.currentSrc)||null;";
    // play / pause: the service's own button when it has one (its own player keeps its state), else the audio element
    public static string Toggle(HubService s) { return "(()=>{" + Click + Media + "if(click(" + Arr(s.PlaySel) + "))return 'button';if(m){if(m.paused)m.play();else m.pause();return 'media'}return 'none'})()"; }
    public static string Pause(HubService s) { return "(()=>{" + Media + "if(m&&!m.paused){" + Click + "if(!click(" + Arr(s.PlaySel) + "))m.pause();return 'paused'}return 'idle'})()"; }
    public static string Next(HubService s) { return "(()=>{" + Click + "return click(" + Arr(s.NextSel) + ")?'ok':'none'})()"; }
    public static string Prev(HubService s) { return "(()=>{" + Click + Media + "if(click(" + Arr(s.PrevSel) + "))return 'ok';if(m){m.currentTime=0;return 'start'}return 'none'})()"; }
    public static string PlayPage(HubService s) { return "(()=>{" + Click + Media + "if(m&&!m.paused)return 'playing';return click(" + Arr(s.PagePlaySel) + ")?'ok':'none'})()"; }
    public static string Seek(double sec) { return "(()=>{" + Media + "if(m&&isFinite(m.duration)){m.currentTime=" + sec.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";return 'ok'}return 'none'})()"; }
    public static string Volume(double v) { return "(()=>{const els=[...document.querySelectorAll('video,audio')];els.forEach(e=>e.volume=" + Math.Max(0, Math.Min(1, v)).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ");return els.length})()"; }
}
