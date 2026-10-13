using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// PROFILE (your own page, with everything for editing it) and ARTISTS (search the other producers, open their profiles, follow them).
// Your profile lives on this PC. PUBLISH puts it in the community, under a secret key this PC makes for itself (there is no sign-in): nobody can change it or follow as you without that key.
sealed class ArtistTrack { public string Title = "", Tags = "", Video = "", Song = ""; public int Bpm, Secs; public bool VideoUp; }   // Song: the song on the community page (plays here); VideoUp: a music video file was uploaded
sealed class ArtistInfo
{
    public string Id = "", Slug = "", Name = "", Nick = "", Bio = "", Location = ""; public string Shown { get { return Nick.Length > 0 ? Name + " (" + Nick + ")" : Name; } }   /* the name with the nickname in brackets, as the artists tabs show it */ public List<string> Links = new List<string>(); public Dictionary<string, string> LinkTitles = new Dictionary<string, string>(); public string[] Tags = new string[0]; public int Tracks, Followers, Accent = -1, Av, Bv, NameRgb = -1, NameRgb2 = -1; public bool Avatar, Banner, Fantasy, FollowersHidden; public long TimeMade; public int Friends; public List<ArtistTrack> TrackList = new List<ArtistTrack>();
}

// what is saved about your profile on this PC (profile.txt next to the other settings)
static partial class ProfileStore
{
    public static string Bio = "", Location = "", Tags = "", BannerPath = "", Key = "", Nick = "";   /* Nick: anything you like, shown in brackets after your name on the artists tabs */ public static readonly List<string> Links = new List<string>(); public static readonly Dictionary<string, string> LinkTitles = new Dictionary<string, string>();   /* every link has a title (url -> title) */ public static int Accent = -1, BannerY = 50, BannerX = 50, BannerZoom = 0, AvX = 50, AvY = 50, AvZoom = 0, Followers;   /* Accent: -1 = theme colour, else 0xRRGGBB. Zoom is a percent: 0 (the default) shows the WHOLE picture with its edges stretched to fill the frame, 100 fills the frame, more zooms in */ public static bool Published, Private, HideLocation, HideFollowers;
    public static bool ZoomMigrated; public static int NameRgb = -1, NameRgb2 = -1;   // the colour of your name on your profile: -1 = the normal writing, else 0xRRGGBB; with a second colour the name fades from one to the other
    static ProfileStore() { Load(); }   // read the saved profile the moment anything first touches it: a Save() that came first (the colour mix, the place picker...) used to write the empty defaults over it
    static string File0 { get { return Path.Combine(StudioEnv.DataDir, "profile-page.txt"); } }   // (profile.txt belongs to the project library: your name and picture)
    public static string Dir { get { string d = Path.Combine(StudioEnv.DataDir, "profile"); try { Directory.CreateDirectory(d); } catch { } return d; } }
    public static void Load()
    {
        try
        {
            if (!File.Exists(File0)) return; bool sawRgb = false, sawZoomV = false;
            foreach (var l in File.ReadAllLines(File0, Encoding.UTF8)) { int i = l.IndexOf('='); if (i < 1) continue; string k = l.Substring(0, i), v = l.Substring(i + 1); int n;
                switch (k) { case "nick": Nick = EmojiText.Cut(v.Trim(), 24); break; case "bio": Bio = UnescBio(v); break; case "location": Location = v; break; case "link": { string cl = CleanLink(v); if (cl.Length > 0 && !Links.Contains(cl)) Links.Add(cl); } break; case "links": Links.Clear(); foreach (var x in v.Split(new[] { (char)31 }, StringSplitOptions.RemoveEmptyEntries)) { string cl2 = CleanLink(x); if (cl2.Length > 0 && Links.Count < 10 && !Links.Contains(cl2)) Links.Add(cl2); } break; case "linktitles": LinkTitles.Clear(); foreach (var x in v.Split(new[] { (char)31 }, StringSplitOptions.RemoveEmptyEntries)) { int k2 = x.IndexOf((char)30); if (k2 > 0) { string uu = CleanLink(x.Substring(0, k2)); string tt = CleanTitle(x.Substring(k2 + 1)); if (uu.Length > 0 && tt.Length > 0) LinkTitles[uu] = tt; } } break; case "tags": Tags = v; break; case "banner": BannerPath = v; break; case "key": Key = v; break;
                    case "namergb": if (int.TryParse(v, out n) && n >= -1 && n <= 0xFFFFFF) NameRgb = n; break; case "namergb2": if (int.TryParse(v, out n) && n >= -1 && n <= 0xFFFFFF) NameRgb2 = n; break; case "private": Private = v == "1"; break; case "hideloc": HideLocation = v == "1"; break; case "hidefol": HideFollowers = v == "1"; break; case "accent": if (int.TryParse(v, out n) && n >= 0 && n < 360 && !sawRgb) Accent = HueToPacked(n); break; case "accentrgb": { int rgb; if (v.Length == 6 && int.TryParse(v, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb)) { Accent = rgb; sawRgb = true; } } break; case "bannerx": if (int.TryParse(v, out n)) BannerX = Math.Max(0, Math.Min(100, n)); break; case "zoomv": sawZoomV = true; break; case "bannerzoom": if (int.TryParse(v, out n)) BannerZoom = n <= 0 ? 0 : Math.Max(5, Math.Min(400, n)); break; case "avx": if (int.TryParse(v, out n)) AvX = Math.Max(0, Math.Min(100, n)); break; case "avy": if (int.TryParse(v, out n)) AvY = Math.Max(0, Math.Min(100, n)); break; case "avzoom": if (int.TryParse(v, out n)) AvZoom = n <= 0 ? 0 : Math.Max(5, Math.Min(400, n)); break; case "bannery": if (int.TryParse(v, out n)) BannerY = Math.Max(0, Math.Min(100, n)); break; case "followers": if (int.TryParse(v, out n)) Followers = Math.Max(0, n); break; case "published": Published = v == "1"; break; } }
            if (!sawZoomV) { if (BannerZoom == 100) BannerZoom = 0; if (AvZoom == 100) AvZoom = 0; ZoomMigrated = true; }   // older versions cut a picture to fill the frame: now the whole picture is shown
        }
        catch { }
    }
    public static void Save()
    {
        try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(File0, "nick=" + Nick + "\nbio=" + EscBio(Bio) + "\nlocation=" + Location + "\nlinks=" + string.Join(((char)31).ToString(), Links.ToArray()) + "\nlinktitles=" + string.Join(((char)31).ToString(), Links.ConvertAll(x => x + (char)30 + TitleOf(LinkTitles, x)).ToArray()) + "\nprivate=" + (Private ? "1" : "0") + "\nhideloc=" + (HideLocation ? "1" : "0") + "\nhidefol=" + (HideFollowers ? "1" : "0") + "\ntags=" + Tags + "\nbanner=" + BannerPath + "\nkey=" + Key + "\nnamergb=" + NameRgb + "\nnamergb2=" + NameRgb2 + "\naccentrgb=" + (Accent < 0 ? "" : Accent.ToString("x6")) + "\nbannerx=" + BannerX + "\nzoomv=2\nbannerzoom=" + BannerZoom + "\navx=" + AvX + "\navy=" + AvY + "\navzoom=" + AvZoom + "\nbannery=" + BannerY + "\nfollowers=" + Followers + "\npublished=" + (Published ? "1" : "0") + "\n", Encoding.UTF8); } catch { }
    }
    // ---- colours and framing shared by the app and the published profile ----
    public static Color ColorOf(int packed) { return Color.FromArgb(255, (packed >> 16) & 255, (packed >> 8) & 255, packed & 255); }
    public static int Pack(Color c) { return (c.R << 16) | (c.G << 8) | c.B; }
    public static int HueToPacked(int hue) { return Pack(ThemeDef.Hsl(hue, 0.82, 0.60)); }   // the nine swatches of older versions were hues
    public static int AccentHue { get { return Accent < 0 ? -1 : (int)Math.Round(ColorOf(Accent).GetHue()) % 360; } }   // what older programs and the website read
    // the zoom (percent) at which the WHOLE picture is visible in the frame
    public static int ContainZoom(int iw, int ih, int w, int h) { double a = w / (double)Math.Max(1, iw), b = h / (double)Math.Max(1, ih); return (int)Math.Max(5, Math.Min(100, Math.Floor(Math.Min(a, b) / Math.Max(a, b) * 100))); }
    public static string EnsureKey()
    {
        if (Key.Length >= 32) return Key; var b = new byte[24]; using (var r = new System.Security.Cryptography.RNGCryptoServiceProvider()) r.GetBytes(b); var sb = new StringBuilder(); foreach (var x in b) sb.Append(x.ToString("x2")); Key = sb.ToString(); Save(); return Key;
    }
}

// talking to the website (always from a background thread)
static partial class ProfApi
{
    // the id the website gives the profile of this key (the same sum the server makes)
    public static string IdOfKey(string key) { try { using (var sh = System.Security.Cryptography.SHA256.Create()) { var h = sh.ComputeHash(System.Text.Encoding.UTF8.GetBytes("purphex-profile:" + key)); var sb = new System.Text.StringBuilder("a"); for (int i = 0; i < 6; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); } } catch { return ""; } }
    static string Http(string method, string path, string body, string bearer = null)
    {
        var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = 15000; rq.ReadWriteTimeout = 15000; if (!string.IsNullOrEmpty(bearer)) rq.Headers["Authorization"] = "Bearer " + bearer;
        if (body != null) { var b = Encoding.UTF8.GetBytes(body); rq.ContentType = "application/json"; rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length); }
        try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
        catch (WebException ex) { if (ex.Response != null) { using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); } throw; }
    }
    public static byte[] Bytes(string path, int max = 600 * 1024)   // 600 KB fits avatars and banners; community covers pass a bigger limit (the server keeps them up to 3 MB)
    {
        var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + path); rq.UserAgent = "PurphexMusic"; rq.Timeout = 15000; rq.ReadWriteTimeout = 15000;
        using (var r = (HttpWebResponse)rq.GetResponse()) using (var s = r.GetResponseStream()) using (var ms = new MemoryStream()) { var buf = new byte[8192]; int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > max) throw new IOException("too big"); } return ms.ToArray(); }
    }
    public static string J(string s) { var sb = new StringBuilder("\""); foreach (char c in s ?? "") { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c < 32) sb.Append(' '); else sb.Append(c); } return sb.Append('"').ToString(); }
    static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is string ? (string)v : ""; }
    static int N(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (int)Math.Max(-1, Math.Min(int.MaxValue, (double)v)) : 0; }
    static bool B(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v; }
    // a published colour: accentRgb ("rrggbb"), else the hue older programs publish, else -1 (the theme colour)
    static int AccentOf(Dictionary<string, object> d) { string h = S(d, "accentRgb"); int rgb; if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb)) return rgb; if (d != null && d.ContainsKey("accent")) { int hue = N(d, "accent"); if (hue >= 0 && hue < 360) return ProfileStore.HueToPacked(hue); } return -1; }
    static int HexRgb(string h) { int rgb; return h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb) ? rgb : -1; }
    static string Clip(string s, int n) { s = (s ?? "").Replace('<', ' ').Replace('>', ' '); return EmojiText.Cut(s, n); }
    public static ArtistInfo Parse(Dictionary<string, object> d)
    {
        if (d == null) return null; var a = new ArtistInfo { Id = S(d, "id"), Slug = Clip(S(d, "slug"), 60), Name = Clip(S(d, "name"), 30), Nick = Clip(S(d, "nick"), 24), Bio = Clip(S(d, "bio"), 20000), Location = Clip(S(d, "location"), 30), Tracks = N(d, "tracks"), Followers = N(d, "followers"), Avatar = B(d, "avatar"), Banner = B(d, "banner"), Fantasy = B(d, "fantasy"), FollowersHidden = B(d, "followersHidden"), Accent = AccentOf(d), Av = N(d, "av"), Bv = N(d, "bv"), NameRgb = HexRgb(S(d, "nameRgb")), NameRgb2 = HexRgb(S(d, "nameRgb2")) };
        a.Bio = ProfileStore.CleanBio(a.Bio); a.TimeMade = N(d, "timeMade"); a.Friends = N(d, "friends");
        if (a.Id.Length == 0 || a.Name.Length == 0) return null; object t; if (d.TryGetValue("tags", out t) && t is List<object>) { var l = new List<string>(); foreach (var x in (List<object>)t) if (x is string && l.Count < 1000 && !ProfileStore.IsBadTag((string)x)) l.Add(Clip((string)x, 20)); a.Tags = l.ToArray(); }
        object lk, lt; List<object> ltl = d.TryGetValue("linkTitles", out lt) ? lt as List<object> : null; if (d.TryGetValue("links", out lk) && lk is List<object>) { var lkl = (List<object>)lk; for (int li = 0; li < lkl.Count; li++) { var x = lkl[li]; if (!(x is string)) continue; string cl = ProfileStore.CleanLink((string)x); if (cl.Length > 0 && a.Links.Count < 10 && !a.Links.Contains(cl)) { a.Links.Add(cl); string tt = ltl != null && li < ltl.Count && ltl[li] is string ? ProfileStore.CleanTitle((string)ltl[li]) : ""; if (tt.Length > 0) a.LinkTitles[cl] = tt; } } }
        object tl; if (d.TryGetValue("trackList", out tl) && tl is List<object>) foreach (var x in (List<object>)tl) { var td = x as Dictionary<string, object>; if (td == null) continue; var at = new ArtistTrack { Title = Clip(S(td, "t"), 40), Bpm = N(td, "b"), Secs = N(td, "s"), Video = ProfileStore.CleanLink(S(td, "v")), Song = S(td, "song"), VideoUp = B(td, "videoUp") }; object g; if (td.TryGetValue("g", out g) && g is List<object>) { var gl = new List<string>(); foreach (var y in (List<object>)g) if (y is string) gl.Add(Clip((string)y, 20)); at.Tags = string.Join(" ", gl.ToArray()); } if (at.Title.Length > 0) a.TrackList.Add(at); }
        return a;
    }
    public static List<ArtistInfo> Search(string q, string sort)
    {
        var o = MiniJson.Parse(Http("GET", "/api/profiles?limit=60&q=" + Uri.EscapeDataString(q ?? "") + "&sort=" + Uri.EscapeDataString(sort ?? ""), null)) as Dictionary<string, object>; var res = new List<ArtistInfo>(); object arr;
        if (o == null || !o.TryGetValue("profiles", out arr) || !(arr is List<object>)) throw new FormatException("answer"); foreach (var it in (List<object>)arr) { var a = Parse(it as Dictionary<string, object>); if (a != null) res.Add(a); } return res;
    }
    public static ArtistInfo Get(string id)
    {
        var o = MiniJson.Parse(Http("GET", "/api/profiles/" + Uri.EscapeDataString(id), null)) as Dictionary<string, object>; object p; if (o == null || !o.TryGetValue("profile", out p)) return null; return Parse(p as Dictionary<string, object>);
    }
    // null = it worked, otherwise a sentence for the person
    public static List<string> LastSuggestions = new List<string>();   // the other ways to write the name when it is taken
    public static string Publish(string json, out ArtistInfo me)
    {
        me = null;
        try
        {
            var o = MiniJson.Parse(Http("POST", "/api/profiles/publish", json, Edition.Token)) as Dictionary<string, object>; if (B(o, "ok")) { object p; if (o.TryGetValue("profile", out p)) me = Parse(p as Dictionary<string, object>); return null; }
            string err = S(o, "err"); if (err == "taken") { LastSuggestions = new List<string>(); object sg; if (o.TryGetValue("suggestions", out sg) && sg is List<object>) foreach (var x in (List<object>)sg) if (x is string && LastSuggestions.Count < 8) LastSuggestions.Add(Clip((string)x, 30)); return "taken"; } if (err == "rate") return "You have published a lot of times in the last hour. Try again a bit later."; if (err == "name") return "Your name needs at least 2 letters."; if (err == "big") return "A picture is too big: choose a smaller one."; if (err == "image") return "A picture could not be read. Choose a JPG or PNG."; if (err == "full") return "The community is full right now."; return "The community could not take your profile.";
        }
        catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    public static bool Remove(string key) { try { var o = MiniJson.Parse(Http("POST", "/api/profiles/remove", "{\"key\":" + J(key) + "}")) as Dictionary<string, object>; return B(o, "ok"); } catch { return false; } }
    // returns the new follower count, or -1 when it failed
    public static int Follow(string key, string id, bool on, out bool following) { following = false; try { var o = MiniJson.Parse(Http("POST", "/api/profiles/follow", "{\"key\":" + J(key) + ",\"id\":" + J(id) + ",\"on\":" + (on ? "true" : "false") + "}")) as Dictionary<string, object>; if (!B(o, "ok")) return -1; following = B(o, "following"); return N(o, "followers"); } catch { return -1; } }
    public static bool State(string key, string id) { try { var o = MiniJson.Parse(Http("POST", "/api/profiles/state", "{\"key\":" + J(key) + ",\"id\":" + J(id) + "}")) as Dictionary<string, object>; return B(o, "following"); } catch { return false; } }
    public static ArtistInfo Me(string key, List<ArtistInfo> following, List<ArtistInfo> friends = null, List<ArtistInfo> requests = null)
    {
        try
        {
            var o = MiniJson.Parse(Http("POST", "/api/profiles/me", "{\"key\":" + J(key) + "}")) as Dictionary<string, object>; if (o == null) return null; object f;
            Action<string, List<ArtistInfo>> fill = (k, lst) => { object v; if (lst != null && o.TryGetValue(k, out v) && v is List<object>) foreach (var it0 in (List<object>)v) { var a0 = Parse(it0 as Dictionary<string, object>); if (a0 != null) lst.Add(a0); } }; fill("friends", friends); fill("requests", requests); if (following != null && o.TryGetValue("following", out f) && f is List<object>) foreach (var it in (List<object>)f) { var a = Parse(it as Dictionary<string, object>); if (a != null) following.Add(a); }
            object p; return o.TryGetValue("profile", out p) ? Parse(p as Dictionary<string, object>) : null;
        }
        catch { return null; }
    }
    public static bool Report(string id) { try { var o = MiniJson.Parse(Http("POST", "/api/profiles/report", "{\"id\":" + J(id) + "}")) as Dictionary<string, object>; return B(o, "ok"); } catch { return false; } }
}

partial class StudioForm
{
    // ---------- shared bits ----------
    Color ProfAccent(int packed) { return packed >= 0 ? ProfileStore.ColorOf(packed) : NeonViolet; }
    // the TIME MADE card: 45s, 2m33s, 2h33m, and past a day 1d2h33m
    static string DurMade(long secs) { if (secs < 0) secs = 0; long d = secs / 86400, h = (secs % 86400) / 3600, m = (secs % 3600) / 60, s = secs % 60; return d > 0 ? d + "d" + h + "h" + m + "m" : (h > 0 ? h + "h" + m + "m" : (m > 0 ? m + "m" + s.ToString("00") + "s" : s + "s")); }
    static string Dur(int secs) { return secs >= 3600 ? (secs / 3600) + "H " + ((secs % 3600) / 60) + "M" : (secs / 60) + ":" + (secs % 60).ToString("00"); }
    // a picture drawn in a circle with a neon ring (these pictures are never buttons)
    void DrawCircleImage(Graphics g, RectangleF r, Bitmap img, Color ring)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); }
        if (img != null) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(img, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height))); } else DrawLogoArt(g, r);
        g.Restore(gs); using (var pen = new Pen(ring, 3f)) g.DrawEllipse(pen, r); using (var pen = new Pen(Color.FromArgb(70, ring), 8f)) g.DrawEllipse(pen, RectangleF.Inflate(r, 3, 3));
    }
    // a banner cut to fill a box (a picture, or the accent colour as a soft gradient when there is no picture)
    readonly Dictionary<string, Bitmap> bannerCache = new Dictionary<string, Bitmap>();
    Bitmap BannerFit(string key, Func<Image> load, int w, int h, int yPct)
    {
        string k = key + "|" + w + "x" + h + "|" + yPct; Bitmap b; if (bannerCache.TryGetValue(k, out b)) return b; Image src = null;
        try { src = load(); } catch { src = null; } if (src == null) { bannerCache[k] = null; return null; }
        try
        {
            b = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double sc = Math.Max(w / (double)src.Width, h / (double)src.Height); float sw = (float)(w / sc), sh = (float)(h / sc), sx = (src.Width - sw) / 2f, sy = (src.Height - sh) * Math.Max(0, Math.Min(100, yPct)) / 100f;
            using (var g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(src, new RectangleF(0, 0, w, h), new RectangleF(sx, sy, sw, sh), GraphicsUnit.Pixel); }
        }
        catch { b = null; }
        finally { src.Dispose(); }
        if (bannerCache.Count > 40) { foreach (var v in bannerCache.Values) if (v != null) v.Dispose(); bannerCache.Clear(); } bannerCache[k] = b; return b;
    }
    static Image LoadFileImage(string path) { using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false)) return new Bitmap(im); }
    void PaintBannerArt(Graphics g, RectangleF r, Bitmap pic, int accent)
    {
        var gs = g.Save(); using (var p = RR(r, 18)) { g.SetClip(p, CombineMode.Intersect); }
        if (pic != null) { if (pic.Width == (int)r.Width && pic.Height == (int)r.Height) g.DrawImageUnscaled(pic, (int)Math.Round(r.X), (int)Math.Round(r.Y)); else g.DrawImage(pic, r); }   // another size stands in (stretched) while this size is drawn
        else
        {
            Color a = ProfAccent(accent); using (var br = new LinearGradientBrush(r, MixC(Color.FromArgb(255, 8, 4, 20), a, 0.55f), MixC(Color.FromArgb(255, 8, 4, 20), NeonPink, 0.4f), 20f)) g.FillRectangle(br, r);
            using (var pen = new Pen(Color.FromArgb(60, Color.White), 1.2f)) for (int i = 0; i < 9; i++) g.DrawBezier(pen, r.X, r.Bottom - 20 - i * 6, r.X + r.Width * 0.3f, r.Y + 20 + i * 5, r.X + r.Width * 0.6f, r.Bottom - 10 - i * 8, r.Right, r.Y + 30 + i * 6);
        }
        g.Restore(gs); AnimBanner(g, r, ProfAccent(accent)); using (var p = RR(r, 18)) using (var pen = new Pen(Color.FromArgb(160, ProfAccent(accent)), 2f)) g.DrawPath(pen, p);   // (AnimBanner: the light and the sparks that move over the banner)
    }
    void StatCard(Graphics g, RectangleF r, string label, string value, Color vc)
    {
        FillRR(g, TM.C(255, 12, 6, 28), r, 14); Win.DrawNeonBox(g, r, false); Txt(g, label, 10, DimC, Rc(r.X, r.Y + 8, r.Width, 14), StringAlignment.Center, true); Txt(g, value, 24, vc, Rc(r.X, r.Y + 24, r.Width, 34), StringAlignment.Center, true);
    }
    // the colour of a service's brand (the links shine in it)
    static Color LinkTint(string url)
    {
        string sv = ""; try { sv = ProfileStore.ServiceOf(new Uri(url).IdnHost); } catch { }
        switch (sv)
        {
            case "YouTube": return Color.FromArgb(255, 70, 70); case "Spotify": return Color.FromArgb(40, 220, 100); case "SoundCloud": return Color.FromArgb(255, 120, 30); case "Instagram": return Color.FromArgb(235, 60, 170);
            case "TikTok": return Color.FromArgb(40, 240, 235); case "Apple Music": return Color.FromArgb(255, 90, 125); case "Facebook": return Color.FromArgb(80, 140, 255); case "X (Twitter)": return Color.FromArgb(225, 225, 240);
            case "Snapchat": return Color.FromArgb(255, 245, 60); case "Bandcamp": return Color.FromArgb(100, 175, 200); case "Discord": return Color.FromArgb(120, 140, 255); case "Twitch": return Color.FromArgb(160, 90, 255);
            default: return NeonViolet;
        }
    }
    // a sweep of light across a link and a rim in its colour (keeps the page animating while a link is on screen)
    void LinkShine(Graphics g, RectangleF r, Color tint, int delay)
    {
        float ph = ((Environment.TickCount + delay) % 2800) / 2800f; float cx = r.X - r.Height + (r.Width + r.Height * 2) * ph; var gs = g.Save();
        using (var p = RR(r, 8)) g.SetClip(p, CombineMode.Intersect);
        using (var br = new SolidBrush(Color.FromArgb(70, Math.Min(255, tint.R + 80), Math.Min(255, tint.G + 80), Math.Min(255, tint.B + 80)))) g.FillPolygon(br, new[] { new PointF(cx - 10, r.Bottom), new PointF(cx + 8, r.Bottom), new PointF(cx + 28, r.Y), new PointF(cx + 10, r.Y) });
        g.Restore(gs); StrokeRR(g, Color.FromArgb(Hot(r) ? 255 : 170, tint), r, 8, Hot(r) ? 1.8f : 1.2f); if (Hot(r)) Glow(g, tint, r, 8, 0.7f, 3);
        lastInput = DateTime.Now; btnAnim = true;
    }
    // links as little buttons: the site's name, and a click opens it in your browser
    float LinkChips(Graphics g, float x0, float y, float maxX, List<string> links, Dictionary<string, string> titles = null)
    {
        float x = x0; int rows = 1;
        foreach (var l in links)
        {
            string lb = ProfileStore.TitleOf(titles, l); if (lb.Length > 26) lb = lb.Substring(0, 25) + "..."; float w = Math.Min(36 + lb.Length * 7.4f, Math.Max(60, maxX - x0)); if (x + w > maxX && x > x0) { x = x0; y += 34; rows++; } var r = Rc(x, y, w, 28); string url = l;
            Btn(g, r, "o  " + lb, 1, () => OpenLink(url), 11); LinkShine(g, r, LinkTint(url), (int)(x * 7)); Hint2(r, ProfileStore.TitleOf(titles, url) + ": " + url + "  (click to open it in your browser)"); x += w + 8;
        }
        return rows * 34 + 2;   // the height used (a row per 34 pixels)
    }
    // SHARE PROFILE: your own public page on the website (/artist/...). The link is copied so it can be sent anywhere, then the page opens (it asks before leaving). A profile that is not public yet opens the Community instead.
    internal string ProfilePageUrl() { string id = ProfileStore.Key.Length >= 32 ? ProfApi.IdOfKey(ProfileStore.Key) : ""; return id.Length > 0 && ProfileStore.Published && !ProfileStore.Private ? CommunityPresets.BaseUrl + "/artist/" + id : ""; }
    internal string profSlug = "";   // the name the website knows this profile by (it may differ from what you typed: a name somebody else holds gets a number after it)
    void ShareProfile()
    {
        string url = ProfilePageUrl();
        if (url.Length == 0) { Toast("Publish your profile first (and not private) to get your own page: opening the Community meanwhile."); OpenCommunitySite("/community"); return; }
        string id = url.Substring(url.LastIndexOf('/') + 1);
        if (profSlug.Length > 0) { ShareProfileGo(profSlug); return; }
        Toast("Getting your profile link...");
        System.Threading.ThreadPool.QueueUserWorkItem(_ => { string sl = ""; try { var a = ProfApi.Get(id); if (a != null) sl = a.Slug; } catch { } try { BeginInvoke(new Action(() => { if (sl.Length > 0) profSlug = sl; ShareProfileGo(sl.Length > 0 ? sl : id); })); } catch { } });
    }
    void ShareProfileGo(string slugOrId)
    {
        string path = "/artist/" + slugOrId, full = CommunityPresets.BaseUrl + path;
        try { Clipboard.SetText(full); Toast("Your profile link is copied: " + full); } catch { Toast("Your profile page: " + full); }
        OpenCommunitySite(path);
    }
    void OpenLink(string url)
    {
        string u = ProfileStore.CleanLink(url); if (u.Length == 0) { Toast("That link cannot be opened."); return; }
        if (!ConfirmLeave(u)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u) { UseShellExecute = true }); } catch (Exception ex) { Toast("Could not open that link: " + ex.Message); }
    }
    // YOUR LINKS: one on each line as a title, a bar and the address; every link needs a title (the window will not save until each one has one)
    void EditLinks()
    {
        var cur = new List<string>(); foreach (var u in ProfileStore.Links) cur.Add(ProfileStore.TitleOf(ProfileStore.LinkTitles, u) + " | " + u);
        string t = StudioDialogs.Links(this, string.Join("\r\n", cur.ToArray())); if (t == null) return; var urls = new List<string>(); var titles = new Dictionary<string, string>();
        foreach (var raw in t.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) { string ti, ur; if (!ProfileStore.ParseLinkLine(raw, out ti, out ur)) continue; if (!urls.Contains(ur) && urls.Count < 10) { urls.Add(ur); titles[ur] = ti; } }
        ProfileStore.Links.Clear(); ProfileStore.Links.AddRange(urls); ProfileStore.LinkTitles.Clear(); foreach (var kv in titles) ProfileStore.LinkTitles[kv.Key] = kv.Value; ProfileStore.Save(); QueueRepublish();
        Toast(urls.Count + (urls.Count == 1 ? " link saved." : " links saved."));
    }
    float TagChips(Graphics g, float x0, float y, float maxX, string[] tags, Color c, bool wrap = false)
    {
        float x = x0; int rows = 1;
        foreach (var tg in tags)
        {
            string lb = "#" + tg; float w = 16 + lb.Length * 6.8f; if (x + w > maxX) { if (!wrap) break; if (x > x0) { x = x0; y += 28; rows++; } }
            var r = Rc(x, y, w, 22); FillRR(g, Color.FromArgb(70, c), r, 11); StrokeRR(g, Color.FromArgb(150, c), r, 11, 1); Txt(g, lb, 11, Ink, r, StringAlignment.Center, true); x += w + 6;
        }
        return rows * 28 + 4;
    }
    // one line of a list of tracks: a round play button (your own tracks), the name, and the details
    void TrackRow(Graphics g, RectangleF r, string name, string meta, ProjectEntry playable, Action<RectangleF> after = null, float reserve = 196)
    {
        FillRR(g, TM.C(255, 14, 8, 30), r, 12); Win.DrawNeonBox(g, r, false); float tx = r.X + 14;
        if (playable != null)
        {
            var pb = Rc(r.X + 10, r.Y + (r.Height - 36) / 2, 36, 36); bool on = string.Equals(previewPath, playable.Path, StringComparison.OrdinalIgnoreCase) && !previewPaused; var e0 = playable;
            FillRR(g, Hot(pb) ? TM.C(255, 120, 60, 220) : TM.C(255, 70, 40, 170), pb, 18); using (var b = new SolidBrush(Color.White)) { if (on) { g.FillRectangle(b, pb.X + 12, pb.Y + 10, 4, 16); g.FillRectangle(b, pb.X + 20, pb.Y + 10, 4, 16); } else g.FillPolygon(b, new[] { new PointF(pb.X + 13, pb.Y + 9), new PointF(pb.X + 13, pb.Y + 27), new PointF(pb.X + 28, pb.Y + 18) }); }
            Reg(pb, () => PreviewToggle(e0)); Hint2(pb, on ? "Pause" : "Play this track"); tx = pb.Right + 12;
        }
        float rp = after != null ? reserve : 14; Txt(g, name, 14, Ink, Rc(tx, r.Y + 6, r.Width - (tx - r.X) - rp, 24), StringAlignment.Near, true); Txt(g, meta, 11, DimC, Rc(tx, r.Y + 30, r.Width - (tx - r.X) - rp, 18), StringAlignment.Near, false);
        if (after != null) after(r);
    }

    // ================================================================= PROFILE =================================================================
    float profScroll, profContentH, profEditH; string profStatus = ""; volatile bool profBusy; List<ArtistInfo> profFollowing = new List<ArtistInfo>(); bool profLoaded;

    void ProfileLoad() { if (profLoaded) return; profLoaded = true; ProfileStore.Load(); if (ProfileStore.Published) ProfileRefresh(); }
    // what the community knows about you (the number of followers and the artists you follow)
    void ProfileRefresh()
    {
        if (ProfileStore.Key.Length < 32 || profBusy) return; profBusy = true; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { var fol = new List<ArtistInfo>(); var frs = new List<ArtistInfo>(); var rqs = new List<ArtistInfo>(); var me = ProfApi.Me(key, fol, frs, rqs); try { BeginInvoke(new Action(() => { profBusy = false; if (me != null) { ProfileStore.Followers = me.Followers; ProfileStore.Save(); } profFollowing = fol; profFriends = frs; profReqs = rqs; lastInput = DateTime.Now; })); } catch { profBusy = false; } });
    }
    // the banner is kept whole (no larger than 2400 pixels), so any picture of any size or shape works; it is fitted to the frame when drawn and can be zoomed and moved
    void SetBanner(string file)
    {
        try
        {
            byte[] b = ProjectLibrary.ScaledJpeg(file, 2400); Directory.CreateDirectory(ProfileStore.Dir); string dest = Path.Combine(ProfileStore.Dir, "banner.jpg"); File.WriteAllBytes(dest, b);
            ProfileStore.BannerPath = dest; ProfileStore.BannerZoom = 0; ProfileStore.BannerX = 50; ProfileStore.BannerY = 50; ProfileStore.Save(); ForgetFramed(); QueueRepublish(); Toast("Banner changed: it was resized to fit. Use ZOOM and the MOVE buttons to place it.");
        }
        catch (Exception ex) { Toast("That picture could not be used: " + ex.Message); }
    }
    static void SaveJpeg(Bitmap b, string path, long quality)
    {
        ImageCodecInfo codec = null; foreach (var c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") codec = c; var tmp = path + ".tmp";
        using (var ep = new EncoderParameters(1)) { ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality); if (codec != null) b.Save(tmp, codec, ep); else b.Save(tmp, ImageFormat.Jpeg); }
        if (File.Exists(path)) File.Delete(path); File.Move(tmp, path);
    }
    void PickBanner()
    {
        using (var d = new OpenFileDialog { Title = "Choose a banner picture", Filter = ImgFilter }) { if (d.ShowDialog(this) != DialogResult.OK) return; SetBanner(d.FileName); }
    }
    void ClearBanner() { try { if (ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath)) File.Delete(ProfileStore.BannerPath); } catch { } ProfileStore.BannerPath = ""; ProfileStore.BannerZoom = 0; ProfileStore.BannerX = 50; ProfileStore.BannerY = 50; ProfileStore.Save(); ForgetFramed(); QueueRepublish(); Toast("Banner removed."); }
    // the picture and banner as small JPEGs for the community
    static string ImgB64(Image src, int w, int h, long q)
    {
        using (var b = new Bitmap(w, h, PixelFormat.Format24bppRgb)) { double sc = Math.Max(w / (double)src.Width, h / (double)src.Height); float sw = (float)(w / sc), sh = (float)(h / sc); using (var g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, new RectangleF(0, 0, w, h), new RectangleF((src.Width - sw) / 2f, (src.Height - sh) / 2f, sw, sh), GraphicsUnit.Pixel); }
            string tmp = Path.Combine(Path.GetTempPath(), "purphex-img-" + Guid.NewGuid().ToString("N") + ".jpg"); try { SaveJpeg(b, tmp, q); return Convert.ToBase64String(File.ReadAllBytes(tmp)); } finally { try { File.Delete(tmp); } catch { } } }
    }
    // a picture drawn into its frame with the zoom and position, as a small JPEG for the community
    static string ImgB64Framed(Image src, int w, int h, int zoom, int px, int py, long q)
    {
        using (var b = ProfileStore.RenderFramed(src, w, h, zoom, px, py, Color.FromArgb(255, 10, 5, 22))) using (var rgb = new Bitmap(w, h, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(rgb)) g.DrawImageUnscaled(b, 0, 0);
            string tmp = Path.Combine(Path.GetTempPath(), "purphex-img-" + Guid.NewGuid().ToString("N") + ".jpg"); try { SaveJpeg(rgb, tmp, q); return Convert.ToBase64String(File.ReadAllBytes(tmp)); } finally { try { File.Delete(tmp); } catch { } }
        }
    }
    string BuildProfileJson(bool withImages)
    {
        var sb = new StringBuilder("{\"key\":" + ProfApi.J(ProfileStore.EnsureKey()) + ",\"name\":" + ProfApi.J(ProjectLibrary.ProfileName) + ",\"nick\":" + ProfApi.J(ProfileStore.Nick) + ",\"bio\":" + ProfApi.J(ProfileStore.Bio) + ",\"location\":" + ProfApi.J(ProfileStore.Location) + ",\"links\":[" + string.Join(",", ProfileStore.Links.ConvertAll(x => ProfApi.J(x)).ToArray()) + "],\"linkTitles\":[" + string.Join(",", ProfileStore.Links.ConvertAll(x => ProfApi.J(ProfileStore.TitleOf(ProfileStore.LinkTitles, x))).ToArray()) + "],\"tags\":" + ProfApi.J(ProfileStore.Tags) + ",\"accent\":" + ProfileStore.AccentHue + ",\"accentRgb\":" + ProfApi.J(ProfileStore.Accent < 0 ? "" : ProfileStore.Accent.ToString("x6")) + ",\"nameRgb\":" + ProfApi.J(ProfileStore.NameRgb < 0 ? "" : ProfileStore.NameRgb.ToString("x6")) + ",\"nameRgb2\":" + ProfApi.J(ProfileStore.NameRgb2 < 0 ? "" : ProfileStore.NameRgb2.ToString("x6")) + ",\"tracks\":[");
        int n = 0; foreach (var e in ProjectLibrary.AllByEdited()) { if ( e.Missing || !e.OnProfile) continue; /* private projects never leave this PC */ if (n > 0) sb.Append(','); sb.Append("{\"t\":" + ProfApi.J(e.Name) + ",\"b\":" + (int)Math.Round(e.Bpm) + ",\"s\":" + (int)Math.Round(e.Seconds) + ",\"g\":" + ProfApi.J(e.Tags) + ",\"v\":" + ProfApi.J(ProfileStore.CleanLink(e.Video ?? "")) + ",\"p\":" + ProfApi.J(CommunitySongs.PidOf(e.Path)) + "}"); n++; }
        sb.Append(']'); sb.Append(",\"timeMade\":" + (long)UseTime.Seconds); sb.Append(",\"collections\":" + ReleaseStore.Json(ProjectLibrary.AllByEdited())); sb.Append(",\"privacy\":{\"private\":" + (ProfileStore.Private ? "true" : "false") + ",\"hideLocation\":" + (ProfileStore.HideLocation ? "true" : "false") + ",\"hideFollowers\":" + (ProfileStore.HideFollowers ? "true" : "false") + "}");
        if (withImages)
        {
            try { if (ProjectLibrary.AvatarPath.Length > 0 && File.Exists(ProjectLibrary.AvatarPath)) using (var im = LoadFileImage(AvatarFile)) sb.Append(",\"avatar\":\"" + ImgB64Framed(im, 192, 192, ProfileStore.AvZoom, ProfileStore.AvX, ProfileStore.AvY, 82) + "\""); else sb.Append(",\"avatarClear\":true"); } catch { }
            try { if (ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath)) using (var im = LoadFileImage(ProfileStore.BannerPath)) sb.Append(",\"banner\":\"" + ImgB64Framed(im, 1000, 200, ProfileStore.BannerZoom, ProfileStore.BannerX, ProfileStore.BannerY, 78) + "\""); else sb.Append(",\"bannerClear\":true"); } catch { }
        }
        return sb.Append('}').ToString();
    }
    // a project is private until you publish it to your profile; the profile is sent again a moment after a change
    System.Windows.Forms.Timer repubTimer; float profTracksTop;
    void QueueRepublish()
    {
        if (!ProfileStore.Published) return;
        if (repubTimer == null) { repubTimer = new System.Windows.Forms.Timer { Interval = 1500 }; repubTimer.Tick += (s, e) => { repubTimer.Stop(); if (profBusy) { repubTimer.Start(); return; } PublishProfile(); }; }
        repubTimer.Stop(); repubTimer.Start();
    }
    // a song can only be seen by others on a public profile: publishing a song makes the profile public too (the question says so first)
    string ProfNote() { return ProfileStore.Published ? "" : "\n\nYour profile is not public yet. Publishing also makes your profile public (artist name: " + ProjectLibrary.ProfileName + ") so people can find the song in ARTISTS."; }
    bool PublishProfileToo() { if (ProfileStore.Published) return false; if (ProjectLibrary.ProfileName.Trim().Length < 2) { Toast("Give your profile an artist name first (Profile tab): then the song goes up."); return true; } PublishProfile(); return true; }
    void ToggleOnProfile(ProjectEntry e)
    {
        if (!e.OnProfile && !AskYes("Publish \"" + e.Name + "\" to your profile and the community?\n\nThe song itself goes on the community page of the website: it is exported as an MP3 (mixed and mastered, up to 30 MB) and members can listen to it, like it and comment. Only publish music you made or have the right to share. You can make it private again whenever you like and it comes off the community page." + ProfNote(), "Publish to profile and community")) return;
        EnsureSongMarks(); ProjectLibrary.SetOnProfile(e, !e.OnProfile);
        if (e.OnProfile) PublishProfileToo();
        Toast(e.OnProfile ? "On your profile and, in a moment, on the community page (the song is exported and uploaded)." + (ProfileStore.Published ? "" : " Publish your profile to share it: the song goes up when you do.") : "Private again: it is hidden from your profile and the community page. Its plays, likes and comments are all kept, and they are back the moment it is public again.");
        if (!e.OnProfile) { var ee = e; OfferRevert("\"" + e.Name + "\" is private", () => { EnsureSongMarks(); ProjectLibrary.SetOnProfile(ee, true); PublishProfileToo(); QueueRepublish(); }); }   // REVERT puts it back (public again, with every number it had)
        QueueRepublish();
    }
    static void DrawPerson(Graphics g, RectangleF r, bool on, bool hot)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; Color c = on ? Color.FromArgb(255, 255, 240, 220) : Color.FromArgb(hot ? 255 : 200, 235, 225, 250);
        using (var b = new SolidBrush(c)) { g.FillEllipse(b, r.X + r.Width / 2f - 4f, r.Y + 5f, 8f, 8f); using (var p = new GraphicsPath()) { p.AddArc(r.X + r.Width / 2f - 8f, r.Y + 14f, 16f, 14f, 180, 180); p.CloseFigure(); g.FillPath(b, p); } }
        g.SmoothingMode = sm;
    }
    void PublishProfile()
    {
        if (profBusy) return; if (ProjectLibrary.ProfileName.Trim().Length < 2) { Toast("Give your profile a name with at least 2 letters first."); return; }
        profBusy = true; profStatus = "Publishing your profile..."; string json; try { json = BuildProfileJson(true); } catch (Exception ex) { profBusy = false; profStatus = "Could not prepare your profile: " + ex.Message; return; }
        ThreadPool.QueueUserWorkItem(o => { ArtistInfo me; string err = ProfApi.Publish(json, out me); try { BeginInvoke(new Action(() => { profBusy = false; if (err == null) { nameEdited = false; ProfileStore.Published = true; string renamed = ""; if (me != null) { ProfileStore.Followers = me.Followers; if (me.Name.Length >= 2 && me.Name != ProjectLibrary.ProfileName) { ProjectLibrary.ProfileName = me.Name; ProjectLibrary.Save(); renamed = "That name was already used by another artist, so yours is shown as " + me.Name + " (the first one keeps the plain name). "; } } ProfileStore.Save(); profStatus = renamed + "Your profile is public: other producers can find you in ARTISTS."; ProfileRefresh(); SyncCommunitySongs(); } else if (err == "taken") NameIsTaken(); else profStatus = err; lastInput = DateTime.Now; })); } catch { profBusy = false; } });
    }
    // the artist name is taken by somebody else: a window says so and offers the same name written in other ways (or your own)
    // the server says another artist has this name: the window with other ways to write it opens only when the name was just changed on the Profile tab; a name that is already yours is never asked about
    void NameIsTaken()
    {
        if (nameEdited) { profStatus = "That artist name is taken: pick another way to write it."; AskNewName(); }
        else profStatus = "Your artist name is held by another profile. If it is yours, link this PC to your Purphex account (Settings); or change the name here in the Profile tab.";
    }
    bool nameEdited;   // the name is only checked against other artists when you change it on the Profile tab (a name that is already yours is never asked about)
    internal Func<string, List<string>, string> nameTakenHook;   // a test answers for itself
    void AskNewName()
    {
        string name = ProjectLibrary.ProfileName; var sug = new List<string>(ProfApi.LastSuggestions); string nn = nameTakenHook != null ? nameTakenHook(name, sug) : StudioDialogs.NameTaken(this, name, sug);
        if (nn == null || nn.Trim().Length < 2) return; ProjectLibrary.ProfileName = EmojiText.Cut(nn.Trim(), 30); ProjectLibrary.Save(); nameEdited = true; profBusy = false; PublishProfile();
    }
    void UnpublishProfile()
    {
        if (profBusy || ProfileStore.Key.Length < 32) return; if (!Sure("make your profile private (nobody can find it until you publish it again)")) return; profBusy = true; profStatus = "Making your profile private..."; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { bool ok = ProfApi.Remove(key); try { BeginInvoke(new Action(() => { profBusy = false; if (ok) { ProfileStore.Published = false; ProfileStore.Followers = 0; ProfileStore.Save(); profStatus = "Your profile is private again. Nobody can find it."; } else profStatus = "Could not reach the Purphex community. Check your internet connection."; })); } catch { profBusy = false; } });
    }
    // PUBLISH ALL (on the Projects page): every project that is not published yet, after asking
    void PublishAllClick()
    {
        var todo = ProjectLibrary.AllByEdited().FindAll(x => !x.OnProfile && !x.Missing); if (todo.Count == 0) { Toast("Every project is already published."); return; }
        if (!AskYes("This will publish all " + todo.Count + (todo.Count == 1 ? " project" : " projects") + " that " + (todo.Count == 1 ? "is" : "are") + " not published yet to your profile and the community.\n\nEvery song goes on the community page of the website (each is exported as an MP3 and uploaded, which can take a while), where members can listen to it, like it and comment. Only publish music you made or have the right to share. You can make any of them private again." + ProfNote() + "\n\nAre you sure?", "Publish all")) return;
        EnsureSongMarks(); foreach (var x in todo) ProjectLibrary.SetOnProfile(x, true); if (!PublishProfileToo()) QueueRepublish(); Toast(todo.Count + (todo.Count == 1 ? " project is" : " projects are") + " published to your profile and the community.");
    }
    void EditText(string what, string cur, int max, Action<string> set) { string t = Ask(what, cur); if (t == null) return; t = EmojiText.Cut(t.Trim(), max); set(t); ProfileStore.Save(); ProjectLibrary.Save(); }

    void PaintProfile0(Graphics g)
    {
        ProfileLoad(); float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, bottom = H - (previewPath.Length > 0 ? 62 : 0); PaintLogoBackdrop(g, Rc(0, top, W, bottom - top));
        float cw = Math.Min(1100, W - 110), cx = (W - 54 - cw) / 2f; if (cx < 20) { cx = 20; cw = W - 94; } float viewH = bottom - top;
        profScroll = Math.Max(0, Math.Min(Math.Max(0, profContentH - viewH + 20), profScroll)); regs.Add(new HitReg { R = Rc(0, top, W, viewH), OnWheel = d => profScroll = Math.Max(0, Math.Min(Math.Max(0, profContentH - viewH + 20), profScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(Rc(0, top, W - 56, viewH), CombineMode.Intersect); float y = top + 14 - profScroll, y00 = y; Color acc = ProfAccent(ProfileStore.Accent);
        // ---- the profile card: banner, picture, name, about ----
        var ban = Rc(cx, y, cw, Math.Max(110, Math.Min(240, cw / 5f))); Bitmap bp = ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath) ? FramedPic(ProfileStore.BannerPath + File.GetLastWriteTime(ProfileStore.BannerPath).Ticks, () => LoadFileImage(ProfileStore.BannerPath), (int)ban.Width, (int)ban.Height, ProfileStore.BannerZoom, ProfileStore.BannerX, ProfileStore.BannerY) : null;   /* the banner is 5 to 1: any picture is resized to fit it */
        PaintBannerArt(g, ban, bp, ProfileStore.Accent); if (Edition.Has(Tier.Fantasy)) { FantasyBorder(g, Rc(ban.X - 3, ban.Y - 3, ban.Width + 6, ban.Height + 6), 16, "me" + ProjectLibrary.ProfileName); FantasyBadge(g, Rc(ban.X + 12, ban.Y + 12, 100, 24)); } var pill = Rc(ban.Right - 120, ban.Y + 12, 108, 24); FillRR(g, ProfileStore.Published ? TM.C(235, 28, 112, 86) : TM.C(235, 60, 52, 98), pill, 12); Txt(g, ProfileStore.Published ? "PUBLIC" : "PRIVATE", 11, Color.White, pill, StringAlignment.Center, true);
        var av = Rc(ban.X + 28, ban.Bottom - 60, 120, 120); Bitmap ab = ProjectLibrary.AvatarPath.Length > 0 ? FramedPic(AvatarFile + File.GetLastWriteTime(AvatarFile).Ticks, () => LoadFileImage(AvatarFile), 120, 120, ProfileStore.AvZoom, ProfileStore.AvX, ProfileStore.AvY) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 7, av.Y - 7, av.Width + 14, av.Height + 14), 30); DrawFrameImage(g, av, ab, acc);   /* a rounded square: the whole picture can be fitted in it */
        float tx = av.Right + 24, ty = ban.Bottom + 12; NameText(g, (ProjectLibrary.ProfileName + (ProfileStore.Nick.Length > 0 ? " (" + ProfileStore.Nick + ")" : "")).ToUpper(), 28, Rc(tx, ty, cw - (tx - cx) - (cw > 760 ? 400 : 220), 40), PlainInk, true);
        if (cw > 760) { var shB = Rc(cx + cw - 190 - 178, ty + 4, 168, 40); Btn(g, shB, "SHARE PROFILE", 2, () => ShareProfile(), 12); Hint2(shB, "Copy the link to your own page on the website (name, picture, links and songs) and open it. Anybody you send it to can see it"); }
        LinksButton(g, Rc(cx + cw - 190, ty + 4, 180, 40), ProfileStore.AllLinks(ProfileStore.Links, ProfileStore.Bio), true, ProfileStore.LinkTitles);
        string sub = (ProfileStore.Location.Length > 0 ? ProfileStore.Location.ToUpper() + "   -   " : "") + ProfileStore.Followers + (ProfileStore.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   " + profFollowing.Count + " FOLLOWING"; Txt(g, sub, 12, NeonCyan, Rc(tx, ty + 40, cw - (tx - cx) - 20, 20), StringAlignment.Near, true);
        float by = ban.Bottom + 70 + 6; if (by < av.Bottom + 10) by = av.Bottom + 10; y = by;
        if (ProfileStore.Bio.Length > 0) { y += BioBlock(g, ProfileStore.Bio, 14, ProfileStore.NameRgb >= 0 ? ProfileStore.ColorOf(ProfileStore.NameRgb) : PlainInk, cx + 6, y, cw - 12); } else { Txt(g, "NO BIO YET. WRITE A FEW WORDS ABOUT YOUR MUSIC BELOW.", 12, DimC, Rc(cx + 6, y, cw - 12, 22), StringAlignment.Near, false); y += 28; }
        if (ProfileStore.Tags.Length > 0) { y += TagBox(g, cx + 6, y, cw - 12, ProfileStore.Tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), acc, true); }
        var myLinks = ProfileStore.AllLinks(ProfileStore.Links, ProfileStore.Bio); if (myLinks.Count > 0) { y += LinkChips(g, cx + 6, y, cx + cw, myLinks, ProfileStore.LinkTitles); }
        y += 8;
        // ---- numbers ----
        var all = ProjectLibrary.AllByEdited(); int tracks = 0; double secs = 0; foreach (var e in all) { tracks += e.Tracks; secs += e.Seconds; } UseTime.Seed(secs);
        { float sw = (cw - 2 * 14) / 3f; StatCard(g, Rc(cx, y, sw, 64), "PROJECTS", all.Count.ToString(), Ink); StatCard(g, Rc(cx + (sw + 14), y, sw, 64), "TIME MADE", DurMade((long)UseTime.Seconds), NeonPink); StatCard(g, Rc(cx + 2 * (sw + 14), y, sw, 64), "FOLLOWERS", ProfileStore.Followers.ToString(), acc); y += 64 + 20; }
        // ---- the editing system ----
        var ed = Rc(cx, y, cw, Math.Max(200, profEditH)); using (var p = RR(ed, 18)) { using (var br = new SolidBrush(TM.C(255, 12, 6, 28))) g.FillPath(br, p); } Win.DrawNeonBox(g, ed, false);   // (the box is drawn first, with the height it had last time)
        float ey = y; Txt(g, "EDIT YOUR PROFILE", 18, Ink, Rc(cx + 18, ey + 12, 400, 28), StringAlignment.Near, true); ey += 48; bool two = cw >= 760; float colW = two ? (cw - 54) / 2f : cw - 36, c1 = cx + 18, c2 = two ? cx + 36 + colW : c1;
        float ly = PaintLookColumn(g, c1, ey, colW, acc);
        // right column: the words
        float ry = ey; Action<string, string, string, Action> row = (label, val, hint, act) =>
        {
            Txt(g, label, 12, DimC, Rc(c2, ry, 200, 18), StringAlignment.Near, true); ry += 20; var box = Rc(c2, ry, colW - 96, 32); FillRR(g, TM.C(255, 8, 4, 20), box, 8); Win.DrawNeonBox(g, box, Hot(box)); Txt(g, val.Length == 0 ? "-" : val, 12, val.Length == 0 ? DimC : Ink, Rc(box.X + 10, box.Y, box.Width - 16, box.Height), StringAlignment.Near, false);
            var eb = Rc(box.Right + 8, ry, 88, 32); Btn(g, eb, "EDIT", 1, act, 12); Hint2(eb, hint); Reg(box, act); ry += 40;
        };
        row("YOUR NAME", ProjectLibrary.ProfileName, "Change the name shown on your profile", () => { string nm = Ask("Your name:", ProjectLibrary.ProfileName); if (nm == null) return; nm = nm.Trim(); if (nm.Length > 30) nm = nm.Substring(0, 30); if (nm.Length > 0 && nm != ProjectLibrary.ProfileName) { ProjectLibrary.ProfileName = nm; ProjectLibrary.Save(); nameEdited = true; if (ProfileStore.Published) { profBusy = false; PublishProfile(); } } });
        row("NICKNAME", ProfileStore.Nick, "A nickname can be anything you like: it shows in brackets after your name on the artists tabs (it is not checked against other artists)", () => { string nk = AskBlank("Your nickname (anything you like, shown in brackets after your name). Leave it empty for no nickname:", ProfileStore.Nick); if (nk == null) return; nk = EmojiText.Cut(nk.Trim(), 24); if (nk != ProfileStore.Nick) { ProfileStore.Nick = nk; ProfileStore.Save(); QueueRepublish(); } });
        row("BIO", ProfileStore.Bio.Replace("\n", "  "), "Write as much as you like about you and your music: the box grows with every line", () => { string t = StudioDialogs.AskMulti(this, "Your bio (as long as you like):", ProfileStore.Bio.Replace("\n", "\r\n")); if (t == null) return; ProfileStore.Bio = ProfileStore.CleanBio(t.Replace("\r\n", "\n")); ProfileStore.Save(); ProjectLibrary.Save(); QueueRepublish(); });
        row("WHERE YOU ARE", ProfileStore.Location, "Search any country, county, town or area (like Poole, Dorset) and pick it", () => PickLocation());
        row("YOUR LINKS", ProfileStore.Links.Count == 0 ? "" : ProfileStore.Links.Count + (ProfileStore.Links.Count == 1 ? " link: " : " links: ") + ProfileStore.LinkLabel(ProfileStore.Links[0]), "Add any links you like (your website, YouTube, SoundCloud, Spotify, Instagram, a shop...): one on every line", () => EditLinks());
        row("YOUR GENRES", ProfileStore.Tags, "Pick the genres you make, or add your own: as many tags as you like (no swearing)", () => { string t = StudioDialogs.Tags(this, ProfileStore.Tags); if (t != null) { ProfileStore.Tags = ProjectLibrary.CleanTags(t); ProfileStore.Save(); QueueRepublish(); } });
        { var rmB = Rc(c2, ry, 190, 30); Btn(g, rmB, "REMOVE TAGS", ProfileStore.Tags.Length > 0 ? 3 : 0, () => RemoveTagsMenu(rmB), 11); Hint2(rmB, "Remove all your tags, only the ones you typed yourself, or one at a time"); ry += 38; }
        ey = Math.Max(ly, ry) + 6;
        // privacy
        using (var pen = new Pen(Color.FromArgb(70, NeonViolet))) g.DrawLine(pen, cx + 18, ey, cx + cw - 18, ey); ey += 12; Txt(g, "PRIVACY", 12, DimC, Rc(cx + 18, ey, 400, 18), StringAlignment.Near, true); ey += 24;
        {
            float pw = Math.Min(210, (cw - 36 - 3 * 10) / 4f); var q0 = Rc(cx + 18, ey, pw, 34); var q1 = Rc(q0.Right + 10, ey, pw, 34); var q2 = Rc(q1.Right + 10, ey, pw, 34); var q3 = Rc(q2.Right + 10, ey, pw, 34);
            Chip(g, q0, "PRIVATE ACCOUNT", ProfileStore.Private, () => { ProfileStore.Private = !ProfileStore.Private; ProfileStore.Save(); QueueRepublish(); }, 1); Hint2(q0, "ON: you are hidden from the Artists page and from search. People who already follow you still see you in their FOLLOWING list.");
            Chip(g, q1, "HIDE LOCATION", ProfileStore.HideLocation, () => { ProfileStore.HideLocation = !ProfileStore.HideLocation; ProfileStore.Save(); QueueRepublish(); }, 1); Hint2(q1, "ON: other people do not see where you are (and cannot search for it).");
            Chip(g, q2, "HIDE FOLLOWERS", ProfileStore.HideFollowers, () => { ProfileStore.HideFollowers = !ProfileStore.HideFollowers; ProfileStore.Save(); QueueRepublish(); }, 1); Hint2(q2, "ON: other people do not see how many followers you have.");
            Btn(g, q3, "PROJECTS", 2, () => { profScroll = profTracksTop; }, 12); Hint2(q3, "Choose which projects are public on your profile. Every project is private until you publish it.");
            TxtWrap(g, "Your projects are private: only the ones you publish (the person button on a project in My Projects, or PUBLISH ALL below) appear on your profile, and only their name, tempo, length and tags are shared, never the audio. Changes are sent to the community by themselves if your profile is public.", 11, DimC, Rc(cx + 18, ey + 40, cw - 36, 32)); ey += 80;
        }
        // community
        using (var pen = new Pen(Color.FromArgb(70, NeonViolet))) g.DrawLine(pen, cx + 18, ey, cx + cw - 18, ey); ey += 12; Txt(g, "SHARE IT WITH THE COMMUNITY", 12, DimC, Rc(cx + 18, ey, 400, 18), StringAlignment.Near, true); ey += 24;
        var pubB = Rc(cx + 18, ey, 220, 38); Btn(g, pubB, profBusy ? "WORKING..." : "PUBLISH MY PROFILE", 2, () => PublishProfile(), 13); Hint2(pubB, ProfileStore.Published ? "Send your changes to the community (your profile is already public)" : "Put your profile (name, picture, banner, bio and your tracks) in the community so other producers can find and follow you");
        if (ProfileStore.Published) { var priv = Rc(pubB.Right + 10, ey, 200, 38); Btn(g, priv, "MAKE IT PRIVATE", 3, () => UnpublishProfile(), 12); Hint2(priv, "Take your profile out of the community"); }
        { var ab2 = Rc(cx + 18, ey + 46, cw - 36, 38); TxtWrap(g, profStatus.Length > 0 ? profStatus : (ProfileStore.Published ? "Your profile is public. Press UPDATE after you change something." : "Only you can see it until you publish. It is shared under a secret key made on this PC (no sign-in)."), 12, profStatus.Length > 0 ? NeonCyan : DimC, ab2); }
        ey += 92; profEditH = ey - y; y = ey + 10;
        y = PaintColourMixer(g, cx, y, cw, top, bottom) + 14;   // the COLOUR MIXER: a box of its own on this page
        // ---- who you follow ----
        if (profFollowing.Count > 0) { Txt(g, "YOU FOLLOW", 14, Ink, Rc(cx + 6, y, 300, 22), StringAlignment.Near, true); y += 28; float fx = cx + 6; foreach (var f in profFollowing) { float fw = Math.Min(220, 30 + f.Name.Length * 9.5f); if (fx + fw > cx + cw) break; var fr = Rc(fx, y, fw, 32); var fa = f; Btn(g, fr, f.Name, 0, () => OpenArtist(fa.Id), 12); Hint2(fr, "Open " + f.Name + "'s profile"); fx += fw + 8; } y += 48; }
        // ---- your tracks ----
        profTracksTop = Math.Max(0, y - y00 - 8); SectionBox(g, cx, y, cw, profPubH); float pubTop = y; y += 14; cx += 14; cw -= 28; Txt(g, "YOUR PUBLISHED PROJECTS", 18, Ink, Rc(cx + 6, y, 400, 28), StringAlignment.Near, true);
        { int pubN = 0; foreach (var e0 in all) if (e0.OnProfile) pubN++; var noB = Rc(cx + cw - 164, y - 2, 164, 32);           Btn(g, noB, "MAKE ALL PRIVATE", pubN > 0 ? 3 : 0, () => { if (!Sure("take every project off your profile")) return; foreach (var e1 in ProjectLibrary.AllByEdited()) ProjectLibrary.SetOnProfile(e1, false); QueueRepublish(); }, 11); Hint2(noB, "Take every project off your profile"); y += 34;
          Txt(g, pubN + " of " + all.Count + " projects are public. The rest are private.", 12, DimC, Rc(cx + 6, y, cw - 12, 20), StringAlignment.Near, false); y += 28; }
        var shownPub = all.FindAll(x0 => x0.OnProfile);
        if (shownPub.Count == 0) { Txt(g, all.Count == 0 ? "Nothing here yet. Make something and press Save: it is private until you publish it." : "No project is published. Press the person button on a project in My Projects, or PUBLISH ALL.", 13, DimC, Rc(cx + 6, y, cw - 12, 24), StringAlignment.Near, false); y += 40; }
        foreach (var e in shownPub) { var rr0 = Rc(cx, y, cw, 58); if (rr0.Bottom > top - 4 && rr0.Y < bottom + 4) { string meta = e.Tracks + (e.Tracks == 1 ? " TRACK" : " TRACKS") + (e.Bpm > 0 ? "   -   " + e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "   -   " + (int)(e.Seconds / 60) + ":" + ((int)(e.Seconds % 60)).ToString("00") : "") + (e.Tags.Length > 0 ? "   -   #" + e.Tags.Replace(" ", " #") : ""); var ee = e; TrackRow(g, rr0, e.Name, meta, e, rr => { var vb = Rc(rr.Right - 184 - 150, rr.Y + 13, 142, 32); Btn(g, vb, (ee.Video ?? "").Length > 0 ? "MUSIC VIDEO" : "+ MUSIC VIDEO", (ee.Video ?? "").Length > 0 ? 2 : 1, () => VideoMenu(vb, ee), 11); Hint2(vb, (ee.Video ?? "").Length > 0 ? "Watch, change or remove the music video of this project" : "Add a music video to this project (a link: YouTube, Vimeo, TikTok, Instagram...)"); var chip = Rc(rr.Right - 184, rr.Y + 13, 172, 32); Btn(g, chip, ee.OnProfile ? "PUBLIC ON PROFILE" : "PUBLISH TO PROFILE", ee.OnProfile ? 4 : 1, () => ToggleOnProfile(ee), 11); Hint2(chip, ee.OnProfile ? "Public: press to make it private again" : "Private: press to put it on your profile"); }, 346); } y += 66; }
        cx -= 14; cw += 28; profPubH = y - pubTop + 4; profContentH = y - y00 + 30; g.Restore(gs);
        VScroll(g, Rc(W - 52, top + 8, 42, viewH - 16), profContentH + 20, viewH, () => profScroll, v => profScroll = v);
    }

    // ================================================================= ARTISTS =================================================================
    string artQuery = "", artErr = ""; bool artSearchOn, artLoading, artOnlyFollowing, artOnlyFriends; int artSort; List<ArtistInfo> artList = new List<ArtistInfo>(); ArtistInfo artSel; bool artSelLoading, artFollowing; float artScroll, artContentH; DateTime artQueryAt = DateTime.MinValue; bool artDirty, artFetched; string artReported = "";
    static readonly string[] ArtSortNames = { "MOST FOLLOWED", "NEWEST", "A TO Z" }, ArtSortArgs = { "followers", "new", "name" };

    void OpenArtists() { tab = 7; artSel = null; if (!artFetched) { artFetched = true; ArtFetch(); } }
    void OpenArtist(string id)
    {
        tab = 7; artView = 0; artSelLoading = true; artErr = ""; artScroll = 0; var stub = artList.Find(a => a.Id == id); if (stub == null) foreach (var f in profFollowing) if (f.Id == id) stub = f; artSel = stub; string key = ProfileStore.EnsureKey();
        ThreadPool.QueueUserWorkItem(o => { ArtistInfo a = null; bool fol = false, blk = false; string fr = "none"; try { a = ProfApi.Get(id); if (a != null) fol = ProfApi.StateOf(key, id, out fr, out blk); } catch { } try { BeginInvoke(new Action(() => { artSelLoading = false; if (a != null) { artSel = a; artFollowing = fol; artFriend = fr; artBlocked = blk; } else if (artSel == null) { artErr = "Could not open that profile. It may have been removed, or you are offline."; } lastInput = DateTime.Now; })); } catch { } });
    }
    void ArtFetch()
    {
        artLoading = true; artErr = ""; string q = artQuery, sort = ArtSortArgs[artSort]; bool only = artOnlyFollowing, onlyF = artOnlyFriends; string key = ProfileStore.EnsureKey();
        ThreadPool.QueueUserWorkItem(o =>
        {
            List<ArtistInfo> l = null; string err = ""; try { if (onlyF) { var fol = new List<ArtistInfo>(); var frs = new List<ArtistInfo>(); ProfApi.Me(key, fol, frs); l = frs.FindAll(a => q.Length == 0 || (a.Name + " " + string.Join(" ", a.Tags)).ToLowerInvariant().Contains(q.ToLowerInvariant())); } else if (only) { var fol = new List<ArtistInfo>(); ProfApi.Me(key, fol); l = fol.FindAll(a => q.Length == 0 || (a.Name + " " + string.Join(" ", a.Tags)).ToLowerInvariant().Contains(q.ToLowerInvariant())); } else l = ProfApi.Search(q, sort); } catch { err = "Could not reach the Purphex community. Check your internet connection."; }
            try { BeginInvoke(new Action(() => { artLoading = false; if (l != null) { artList = l; artErr = ""; } else artErr = err; artScroll = 0; lastInput = DateTime.Now; })); } catch { }
        });
    }
    // pictures of artists come from the website in the background and are kept
    readonly Dictionary<string, Bitmap> artImgs = new Dictionary<string, Bitmap>(); readonly HashSet<string> artImgPending = new HashSet<string>();
    readonly Dictionary<string, byte[]> artSrc = new Dictionary<string, byte[]>(); readonly HashSet<string> artSrcPending = new HashSet<string>();   // the downloaded file per picture version (id|kind|ver), shared by every size
    Bitmap ArtistImage(string id, string kind, int ver, int w, int h)
    {
        string bk = id + "|" + kind + "|" + ver, k = bk + "|" + w + "x" + h; Bitmap b; if (artImgs.TryGetValue(k, out b)) return b;
        // until this size is ready, show any size already made of the same picture (no flash to the placeholder while the window is resized)
        Bitmap near = null; foreach (var kv in artImgs) if (kv.Value != null && kv.Key.StartsWith(bk + "|", StringComparison.Ordinal)) { near = kv.Value; break; }
        if (artImgPending.Contains(k)) return near;
        // the file itself is downloaded once per picture version; a new size is only re-drawn from it, so a resize never starts a download for every width
        byte[] src; artSrc.TryGetValue(bk, out src); if (src == null && artSrcPending.Contains(bk)) return near;
        artImgPending.Add(k); if (src == null) artSrcPending.Add(bk);
        ThreadPool.QueueUserWorkItem(o => { Bitmap res = null; byte[] got = src; try { var data = got ?? ProfApi.Bytes("/api/profiles/" + id + "/" + kind); got = data; using (var ms = new MemoryStream(data)) using (var im = Image.FromStream(ms, false, false)) { res = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double sc = Math.Max(w / (double)im.Width, h / (double)im.Height); float sw = (float)(w / sc), sh = (float)(h / sc); using (var gg = Graphics.FromImage(res)) { gg.InterpolationMode = InterpolationMode.HighQualityBicubic; gg.DrawImage(im, new RectangleF(0, 0, w, h), new RectangleF((im.Width - sw) / 2f, (im.Height - sh) / 2f, sw, sh), GraphicsUnit.Pixel); } } } catch { res = null; }
            try { BeginInvoke(new Action(() => { if (artImgs.Count > 120) { foreach (var v in artImgs.Values) if (v != null) v.Dispose(); artImgs.Clear(); } artImgs[k] = res; artImgPending.Remove(k); artSrcPending.Remove(bk); if (got != null && src == null) { if (artSrc.Count > 80) artSrc.Clear(); artSrc[bk] = got; } lastInput = DateTime.Now; })); } catch { } });
        return near;
    }

    bool ArtSearchActive { get { return tab == 7 && artSel == null && artSearchOn && !OverlayOpen && (editBox == null || !editBox.Visible); } }
    bool ArtSearchKey(char c)
    {
        if (!ArtSearchActive) return false; if (c == '\b') { if (artQuery.Length > 0) artQuery = artQuery.Substring(0, artQuery.Length - 1); } else if (!char.IsControl(c) && artQuery.Length < 30) artQuery += c; else return true;
        artQueryAt = DateTime.Now; artDirty = true; return true;
    }
    bool ArtSearchCmd(Keys k)
    {
        if (!ArtSearchActive) return false; if (k == Keys.Escape) { if (artQuery.Length > 0) { artQuery = ""; artDirty = true; artQueryAt = DateTime.Now; } else artSearchOn = false; return true; } if (k == Keys.Enter || k == Keys.Return) { artSearchOn = false; artDirty = true; artQueryAt = DateTime.MinValue; return true; } return false;
    }
    bool ArtSearchTakesKey(Keys k) { return ArtSearchActive && (k & Keys.Control) == 0 && (k & Keys.Alt) == 0 && !((k & Keys.KeyCode) >= Keys.F1 && (k & Keys.KeyCode) <= Keys.F24); }

    void FollowToggle()
    {
        var a = artSel; if (a == null || artSelLoading) return; bool want = !artFollowing; string key = ProfileStore.EnsureKey(); artSelLoading = true;
        ThreadPool.QueueUserWorkItem(o => { bool fol; int n = ProfApi.Follow(key, a.Id, want, out fol); try { BeginInvoke(new Action(() => { artSelLoading = false; if (n >= 0) { artFollowing = fol; a.Followers = n; Toast(fol ? "You follow " + a.Name + " now." : "You stopped following " + a.Name + "."); if (ProfileStore.Published) ProfileRefresh(); else { var f = new List<ArtistInfo>(); ThreadPool.QueueUserWorkItem(o2 => { ProfApi.Me(key, f); try { BeginInvoke(new Action(() => profFollowing = f)); } catch { } }); } } else Toast("Could not do that: you cannot follow yourself, and you need an internet connection."); lastInput = DateTime.Now; })); } catch { } });
    }

    // the Fantasy Edition border: a pink to cyan line with splatters and sparks around it (the same colours as the pointer over a song)
    void FantasyBorder(Graphics g, RectangleF r, float rad, string seed)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var br = new LinearGradientBrush(r, NeonPink, NeonCyan, 45f)) using (var pen = new Pen(br, 2.4f)) using (var p = RR(r, rad)) { g.DrawPath(pen, p); }
        int h = 17; foreach (char c in seed ?? "") h = h * 31 + c; var rnd = new Random(h); Color[] cols = { NeonPink, NeonCyan, Color.FromArgb(255, 255, 224, 90), NeonLime, Color.White }; float per = 2 * (r.Width + r.Height);
        for (int i = 0; i < 16; i++)
        {
            float t = (float)rnd.NextDouble() * per, x, y; if (t < r.Width) { x = r.X + t; y = r.Y; } else if (t < r.Width + r.Height) { x = r.Right; y = r.Y + (t - r.Width); } else if (t < 2 * r.Width + r.Height) { x = r.Right - (t - r.Width - r.Height); y = r.Bottom; } else { x = r.X; y = r.Bottom - (t - 2 * r.Width - r.Height); }
            float off = (float)(rnd.NextDouble() * 7 - 3.5), s = 1.2f + (float)rnd.NextDouble() * 2.6f; var col = cols[rnd.Next(cols.Length)]; if (x <= r.X + 1 || x >= r.Right - 1) x += off; else y += off;
            using (var gl = new SolidBrush(Color.FromArgb(50, col))) g.FillEllipse(gl, x - s * 2.2f, y - s * 2.2f, s * 4.4f, s * 4.4f); using (var d = new SolidBrush(Color.FromArgb(235, col))) g.FillEllipse(d, x - s, y - s, s * 2, s * 2);
        }
        g.SmoothingMode = sm;
    }
    void FantasyBadge(Graphics g, RectangleF pill)
    {
        FillRR(g, Color.FromArgb(215, 14, 6, 30), pill, pill.Height / 2f); FillRR(g, TM.C(70, 255, 43, 214), pill, pill.Height / 2f); StrokeRR(g, Color.FromArgb(220, NeonPink), pill, pill.Height / 2f, 1.2f);   /* a dark back first: the badge sits on any picture and its writing must be readable */
        var cr = new PointF[] { new PointF(pill.X + 9, pill.Bottom - 6), new PointF(pill.X + 7, pill.Y + 6), new PointF(pill.X + 12, pill.Y + 10), new PointF(pill.X + 16, pill.Y + 5), new PointF(pill.X + 20, pill.Y + 10), new PointF(pill.X + 25, pill.Y + 6), new PointF(pill.X + 23, pill.Bottom - 6) };
        using (var b = new SolidBrush(Color.FromArgb(255, 255, 224, 90))) g.FillPolygon(b, cr); Txt(g, "FANTASY", 10, Ink, Rc(pill.X + 28, pill.Y, pill.Width - 30, pill.Height), StringAlignment.Near, true);
    }
    void PaintArtists(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, bottom = H - (previewPath.Length > 0 ? 62 : 0); PaintLogoBackdrop(g, Rc(0, top, W, bottom - top));
        if (artDirty && (DateTime.Now - artQueryAt).TotalMilliseconds > 450 && !artLoading) { artDirty = false; ArtFetch(); } if (artDirty || artLoading || artSelLoading) lastInput = DateTime.Now;
        if (artSel != null) { PaintArtistView(g, W, top, bottom); return; }
        if (artView == 1) { PaintMessages(g, W, top, bottom); return; }
        if (artView == 2) { PaintCollabs(g, W, top, bottom); return; }
        if (artView == 3) { PaintFriends(g, W, top, bottom); return; }
        FetchInbox(false);
        float y = top + 12, h = 36; var sb = Rc(24, y, Math.Max(200, Math.Min(440, W - 48 - 360)), h); FillRR(g, TM.C(255, 14, 7, 32), sb, 9); Win.DrawNeonBox(g, sb, artSearchOn);
        bool blink = ((int)(Environment.TickCount / 500) % 2) == 0 && artSearchOn; Txt(g, artQuery.Length == 0 ? (blink ? "|  " : "   ") + "SEARCH ARTISTS BY NAME OR GENRE" : artQuery + (blink ? "|" : ""), 13, artQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 14, sb.Y, sb.Width - 54, sb.Height), StringAlignment.Near, artQuery.Length > 0);
        artSearchR = sb; Reg(sb, () => artSearchOn = true); Hint2(sb, "Type a name or a genre such as dnb, trap or lofi. Click here first.");
        if (artQuery.Length > 0) { var x = Rc(sb.Right - 34, sb.Y + 6, 24, 24); Btn(g, x, "x", 3, () => { artQuery = ""; artDirty = true; artQueryAt = DateTime.MinValue; }, 12); Hint2(x, "Clear the search"); }
        float x0 = sb.Right + 14; var sortB = Rc(x0, y, 190, h); Btn(g, sortB, "SORT: " + ArtSortNames[artSort], 0, () => { artSort = (artSort + 1) % 3; artFetched = true; ArtFetch(); }, 12); Hint2(sortB, "Change how the artists are ordered"); x0 = sortB.Right + 10;
        var folB = Rc(x0, y, 150, h); Chip(g, folB, "FOLLOWING", artOnlyFollowing, () => { artOnlyFollowing = !artOnlyFollowing; if (artOnlyFollowing) artOnlyFriends = false; ArtFetch(); }, "Show only the artists you follow"); x0 = folB.Right + 10;
        { var colB = Rc(W - 56 - 150, y, 150, h); Btn(g, colB, "COLLABS", 1, () => { artView = 2; FetchCollabs(true); }, 12); Hint2(colB, "The tracks you make together with your friends"); var msgB = Rc(colB.X - 8 - 190, y, 190, h); Btn(g, msgB, "MESSAGES" + (msgUnread > 0 ? " (" + msgUnread + ")" : ""), 2, () => { artView = 1; FetchInbox(true); }, 12); Hint2(msgB, "Write to artists and read what they wrote. Friend requests are here too"); var friB = Rc(msgB.X - 8 - 130, y, 130, h); if (friB.X > x0) { Btn(g, friB, "FRIENDS" + (profReqs.Count > 0 ? " (" + profReqs.Count + ")" : ""), 2, () => OpenFriends(), 12); Hint2(friB, "Your friends and your friend requests"); } }
        y += h + 14; float cardW = 250, cardH = 238, gap = 16; float areaW = W - 56 - 24; int cols = Math.Max(1, (int)((areaW + gap) / (cardW + gap))); float used = cols * cardW + (cols - 1) * gap, ox = 24 + (areaW - used) / 2f; var area = Rc(0, y, W - 56, bottom - y);
        int rows = (artList.Count + cols - 1) / cols; artContentH = rows * (cardH + gap); float viewH = bottom - y; artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 6), artScroll)); regs.Add(new HitReg { R = area, OnWheel = d => artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 6), artScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < artList.Count; i++)
        {
            var a = artList[i]; float cx = ox + (i % cols) * (cardW + gap), cy = y + (i / cols) * (cardH + gap) - artScroll; var card = Rc(cx, cy, cardW, cardH); if (card.Bottom < y || card.Y > bottom) continue; bool hot = Hot(card) && area.Contains(MousePos); Color ac = ProfAccent(a.Accent);
            FillRR(g, TM.C(hot ? 255 : 235, 16, 9, 34), card, 16); Win.DrawNeonBox(g, card, hot); if (a.Fantasy) FantasyBorder(g, card, 16, a.Id); var ban = Rc(card.X + 8, card.Y + 8, card.Width - 16, 78); Bitmap bp = a.Banner ? ArtistImage(a.Id, "banner", a.Bv, (int)ban.Width, (int)ban.Height) : null; PaintBannerArt(g, ban, bp, a.Accent);
            var av = Rc(card.X + 18, ban.Bottom - 30, 60, 60); Bitmap ab = a.Avatar ? ArtistImage(a.Id, "avatar", a.Av, 60, 60) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 4, av.Y - 4, 68, 68), 15); DrawFrameImage(g, av, ab, ac);
            var nameBox = Rc(av.Right + 8, ban.Bottom + 3, card.Right - av.Right - 20, 42); FillRR(g, TM.C(200, 8, 4, 20), nameBox, 8); Win.DrawNeonBox(g, nameBox, hot);
            NameTextC(g, a.Shown, 15, Rc(nameBox.X + 8, nameBox.Y + 3, nameBox.Width - 14, 20), Ink, true, a.NameRgb, a.NameRgb2); if (a.Fantasy) FantasyBadge(g, Rc(ban.X + 8, ban.Y + 8, 92, 20)); Txt(g, (a.FollowersHidden ? "" : a.Followers + (a.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   ") + a.Tracks + (a.Tracks == 1 ? " TRACK" : " TRACKS"), 10, NeonCyan, Rc(nameBox.X + 8, nameBox.Y + 23, nameBox.Width - 14, 16), StringAlignment.Near, true);
            // the bio has a box of its own; when it is longer than the box a little arrow opens the rest
            bool bioMore = false; RectangleF bioBox = Rc(card.X + 12, Math.Max(av.Bottom, nameBox.Bottom) + 6, card.Width - 24, 38);
            if (a.Bio.Length > 0) { FillRR(g, TM.C(190, 8, 4, 20), bioBox, 8); StrokeRR(g, Color.FromArgb(hot ? 200 : 110, ac), bioBox, 8, 1f); TxtWrap(g, a.Bio, 11, DimC, Rc(bioBox.X + 7, bioBox.Y + 3, bioBox.Width - 30, bioBox.Height - 6)); var fnt = GfxCache.Exact(GfxCache.Grow(11), false); bioMore = g.MeasureString(a.Bio.Replace("\r", "").Replace("\n", " "), fnt, (int)(bioBox.Width - 30)).Height > bioBox.Height - 8; }
            if (a.Tags.Length > 0) TagChips(g, card.X + 14, card.Bottom - 34, card.Right - 12, a.Tags, ac);
            var open = Reg(card, () => OpenArtist(a.Id)); Hint2(card, "Open " + a.Name + "'s profile");
            if (bioMore)
            {
                var ar = Rc(bioBox.Right - 26, bioBox.Bottom - 22, 22, 18); bool ao = artBioId == a.Id; bool ah = Hot(ar); FillRR(g, ao ? TM.C(220, 118, 52, 212) : (ah ? TM.C(220, 90, 50, 170) : TM.C(170, 44, 24, 80)), ar, 6); StrokeRR(g, Color.FromArgb(ah || ao ? 255 : 160, ac), ar, 6, 1f);
                { var sm0 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; using (var b0 = new SolidBrush(Color.White)) g.FillPolygon(b0, ao ? new[] { new PointF(ar.X + 6, ar.Y + 11), new PointF(ar.Right - 6, ar.Y + 11), new PointF(ar.X + 11, ar.Y + 6) } : new[] { new PointF(ar.X + 6, ar.Y + 6), new PointF(ar.Right - 6, ar.Y + 6), new PointF(ar.X + 11, ar.Y + 12) }); g.SmoothingMode = sm0; }
                var aa = a; Reg(ar, () => { if (artBioId == aa.Id) artBioId = ""; else { artBioId = aa.Id; artBioArt = aa; artBioAnchor = ar; } }); Hint2(ar, "Read the rest of the bio");
            }
        }
        g.Restore(gs); VScroll(g, Rc(W - 52, y, 42, viewH - 6), artContentH + 6, viewH, () => artScroll, v => artScroll = v);
        // the rest of a long bio: a neon box that opens under the little arrow
        if (artBioId.Length > 0 && artBioArt != null && artBioArt.Id == artBioId)
        {
            string full = artBioArt.Bio.Replace("\r", "").Replace("\n", "  "); bool cut = full.Length > 900; if (cut) full = full.Substring(0, 900).TrimEnd() + "...";
            float pw = Math.Min(340, W - 80), ph = Math.Min(230, 74 + (float)Math.Ceiling(full.Length / 40.0) * 15); var pb = Rc(Math.Max(10, Math.Min(W - pw - 70, artBioAnchor.Right - pw)), Math.Min(bottom - ph - 4, artBioAnchor.Bottom + 6), pw, ph);
            FillRR(g, TM.C(252, 20, 10, 42), pb, 12); Glow(g, NeonCyan, pb, 12, 0.5f, 3); StrokeRR(g, Color.FromArgb(220, NeonCyan), pb, 12, 1.3f); Reg(pb, () => { });
            Txt(g, "BIO  -  " + artBioArt.Name.ToUpper(), 11, NeonCyan, Rc(pb.X + 12, pb.Y + 8, pb.Width - 60, 18), StringAlignment.Near, true); var xb = Rc(pb.Right - 34, pb.Y + 6, 24, 22); Btn(g, xb, "x", 3, () => artBioId = "", 11);
            TxtWrap(g, full, 12, Ink, Rc(pb.X + 12, pb.Y + 30, pb.Width - 24, pb.Height - 70)); string oid = artBioArt.Id; var ob = Rc(pb.Right - 12 - 120, pb.Bottom - 34, 120, 26); Btn(g, ob, "OPEN PROFILE", 1, () => { artBioId = ""; OpenArtist(oid); }, 10);
        }
        string msg = artErr.Length > 0 ? artErr : (artLoading ? "LOOKING FOR ARTISTS..." : (artList.Count == 0 ? (artOnlyFriends ? "YOU HAVE NO FRIENDS HERE YET. OPEN AN ARTIST AND PRESS ADD FRIEND." : artOnlyFollowing ? "YOU DO NOT FOLLOW ANYONE YET." : (artQuery.Length > 0 ? "NO ARTIST MATCHES THAT." : "NO ARTISTS HAVE PUBLISHED A PROFILE YET. BE THE FIRST: GO TO YOUR PROFILE AND PRESS PUBLISH.")) : ""));
        if (msg.Length > 0) TxtWrap(g, msg, 14, artErr.Length > 0 ? Bad : DimC, Rc(40, y + 30, W - 140, 60));
    }
    RectangleF artSearchR, artBioAnchor; string artBioId = ""; ArtistInfo artBioArt;   // the open bio box of a long bio (an arrow on the artist card)

    void PaintArtistView0(Graphics g, float W, float top, float bottom)
    {
        var a = artSel; float cw = Math.Min(1100, W - 110), cx = (W - 54 - cw) / 2f; if (cx < 20) { cx = 20; cw = W - 94; } float viewH = bottom - top; Color acc = ProfAccent(a.Accent);
        artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 20), artScroll)); regs.Add(new HitReg { R = Rc(0, top, W, viewH), OnWheel = d => artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 20), artScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(Rc(0, top, W - 56, viewH), CombineMode.Intersect); float y = top + 14 - artScroll, y00 = y;
        var back = Rc(cx, y, 150, 34); Btn(g, back, "< ALL ARTISTS", 1, () => { artSel = null; artScroll = 0; }, 12); Hint2(back, "Back to the list of artists"); y += 46;
        var ban = Rc(cx, y, cw, Math.Max(110, Math.Min(240, cw / 5f))); Bitmap bp = a.Banner ? ArtistImage(a.Id, "banner", a.Bv, (int)ban.Width, (int)ban.Height) : null; PaintBannerArt(g, ban, bp, a.Accent); if (a.Fantasy) { FantasyBorder(g, Rc(ban.X - 3, ban.Y - 3, ban.Width + 6, ban.Height + 6), 16, a.Id); FantasyBadge(g, Rc(ban.X + 12, ban.Y + 12, 100, 24)); }
        var av = Rc(ban.X + 28, ban.Bottom - 60, 120, 120); Bitmap ab = a.Avatar ? ArtistImage(a.Id, "avatar", a.Av, 120, 120) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 7, av.Y - 7, av.Width + 14, av.Height + 14), 30); DrawFrameImage(g, av, ab, acc);
        float tx = av.Right + 24, ty = ban.Bottom + 12; NameTextC(g, a.Shown.ToUpper(), 28, Rc(tx, ty, cw - (tx - cx) - 420, 40), PlainInk, true, a.NameRgb, a.NameRgb2);
        Txt(g, (a.Location.Length > 0 ? a.Location.ToUpper() + "   -   " : "") + (a.FollowersHidden ? "" : a.Followers + (a.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   ") + a.Tracks + (a.Tracks == 1 ? " TRACK" : " TRACKS"), 12, NeonCyan, Rc(tx, ty + 40, cw - (tx - cx) - 20, 20), StringAlignment.Near, true);
        SocialButtons(g, a, cx, cw, ty);
        var fb = Rc(cx + cw - 190, ty + 4, 180, 40); Btn(g, fb, artSelLoading ? "WORKING..." : (artFollowing ? "FOLLOWING  (UNFOLLOW)" : "FOLLOW"), artFollowing ? 0 : 2, () => FollowToggle(), 12); Hint2(fb, artFollowing ? "Stop following this artist" : "Follow this artist: they appear under FOLLOWING and on your profile"); LinksButton(g, Rc(fb.X - 152, ty + 4, 142, 40), ProfileStore.AllLinks(a.Links, a.Bio), false, a.LinkTitles);
        y = Math.Max(ban.Bottom + 118, av.Bottom + 10); y += ArtistBioBox(g, a, cx + 6, y, cw - 12); if (a.Tags.Length > 0) { y += TagBox(g, cx + 6, y, cw - 12, a.Tags, acc, false); }
        var theirLinks = ProfileStore.AllLinks(a.Links, a.Bio); if (theirLinks.Count > 0) { y += LinkChips(g, cx + 6, y, cx + cw, theirLinks, a.LinkTitles); }
        y += 8; float sw = (cw - 4 * 14) / 5f; StatCard(g, Rc(cx, y, sw, 64), "FOLLOWERS", a.FollowersHidden ? "-" : a.Followers.ToString(), acc); StatCard(g, Rc(cx + sw + 14, y, sw, 64), "FRIENDS", a.Friends.ToString(), NeonPink); StatCard(g, Rc(cx + 2 * (sw + 14), y, sw, 64), "PROJECTS", a.Tracks.ToString(), NeonCyan); int tot = 0; foreach (var t in a.TrackList) tot += t.Secs; StatCard(g, Rc(cx + 3 * (sw + 14), y, sw, 64), "PROJECTS LENGTH", DurMade(tot), Ink); long tmade = a.TimeMade; if (ProfileStore.Key.Length > 0 && ProfApi.IdOfKey(ProfileStore.Key) == a.Id) tmade = Math.Max(tmade, (long)UseTime.Seconds);   /* your own page shows the time as it is now, not as it was when you last published */
        StatCard(g, Rc(cx + 4 * (sw + 14), y, sw, 64), "TIME MADE", tmade > 0 ? DurMade(tmade) : "-", NeonPink); y += 64 + 22;
        SectionBox(g, cx, y, cw, artPubH); float pTop = y; y += 14; cx += 14; cw -= 28; Txt(g, "PROJECTS", 18, Ink, Rc(cx + 6, y, 400, 28), StringAlignment.Near, true); y += 36;
        if (a.TrackList.Count == 0) { Txt(g, artSelLoading ? "LOADING..." : "THIS ARTIST HAS NOT LISTED ANY TRACKS YET.", 13, DimC, Rc(cx + 6, y, cw - 12, 24), StringAlignment.Near, false); y += 40; }
        foreach (var t in a.TrackList) { var rr0 = Rc(cx, y, cw, 82); if (rr0.Bottom > top - 4 && rr0.Y < bottom + 4) ArtistTrackRow(g, rr0, a, t, acc); y += 90; }
        cx -= 14; cw += 28; artPubH = y - pTop + 4; var rp = Rc(cx, y + 8, 220, 30); Btn(g, rp, artReported == a.Id ? "REPORTED, THANK YOU" : "REPORT THIS PROFILE", 0, () => ReportArtist(a), 11); Hint2(rp, "Tell the staff why this profile is not OK (you must say why)"); y += 54;
        artContentH = y - y00 + 30; g.Restore(gs); VScroll(g, Rc(W - 52, top + 8, 42, viewH - 16), artContentH + 20, viewH, () => artScroll, v => artScroll = v);
    }
}

partial class StudioForm
{
    // a round play / pause button laid over a picture
    // PLAY on a project card: a box of its own under SIZE, with a round play button and the word PLAY / PAUSE (or LOADING while the project opens)
    void PlayBox(Graphics g, RectangleF r, ProjectEntry e)
    {
        bool mine = string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase), on = mine && !previewLoading, loading = mine && previewLoading; bool hot = Hot(r);
        using (var p = RR(r, 10)) using (var br = new LinearGradientBrush(r, hot ? TM.C(255, 150, 80, 240) : TM.C(255, 120, 56, 215), hot ? TM.C(255, 110, 50, 200) : TM.C(255, 84, 38, 160), 90f)) g.FillPath(br, p);   // solid, no outline: nothing but the button itself
        var dot = Rc(r.X + 6, r.Y + 5, r.Height - 10, r.Height - 10); using (var b = new SolidBrush(TM.C(235, 20, 10, 46))) g.FillEllipse(b, dot);
        using (var b = new SolidBrush(Color.White)) { if (mine) g.FillRectangle(b, dot.X + dot.Width * 0.32f, dot.Y + dot.Height * 0.32f, dot.Width * 0.36f, dot.Height * 0.36f); else g.FillPolygon(b, new[] { new PointF(dot.X + dot.Width * 0.38f, dot.Y + dot.Height * 0.27f), new PointF(dot.X + dot.Width * 0.38f, dot.Y + dot.Height * 0.73f), new PointF(dot.X + dot.Width * 0.74f, dot.Y + dot.Height * 0.5f) }); }
        Txt(g, loading ? "LOADING..." : (mine ? "STOP" : "PLAY"), 13, Ink, Rc(dot.Right + 10, r.Y, r.Width - dot.Width - 24, r.Height), StringAlignment.Near, true);
        Reg(r, () => PreviewToggle(e)); Hint2(r, mine ? "Stop" : "Play this project (a preview of the saved song)");
    }
    void PlayDot(Graphics g, RectangleF r, bool on, Action act)
    {
        bool hot = Hot(r); using (var b = new SolidBrush(hot ? TM.C(235, 120, 60, 230) : TM.C(215, 12, 6, 30))) g.FillEllipse(b, r); using (var pen = new Pen(on ? NeonCyan : NeonPink, 2f)) g.DrawEllipse(pen, r);
        using (var b = new SolidBrush(Color.White)) { if (on) { g.FillRectangle(b, r.X + r.Width * 0.34f, r.Y + r.Height * 0.28f, r.Width * 0.11f, r.Height * 0.44f); g.FillRectangle(b, r.X + r.Width * 0.55f, r.Y + r.Height * 0.28f, r.Width * 0.11f, r.Height * 0.44f); } else g.FillPolygon(b, new[] { new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.27f), new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.73f), new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.5f) }); }
        Reg(r, act); Hint2(r, on ? "Pause" : "Play this project");
    }
}
