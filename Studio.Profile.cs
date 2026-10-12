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
sealed class ArtistTrack { public string Title = "", Tags = ""; public int Bpm, Secs; }
sealed class ArtistInfo
{
    public string Id = "", Name = "", Bio = "", Location = ""; public List<string> Links = new List<string>(); public string[] Tags = new string[0]; public int Tracks, Followers, Accent = -1, Av, Bv; public bool Avatar, Banner, Fantasy, FollowersHidden; public List<ArtistTrack> TrackList = new List<ArtistTrack>();
}

// what is saved about your profile on this PC (profile.txt next to the other settings)
static class ProfileStore
{
    public static string Bio = "", Location = "", Tags = "", BannerPath = "", Key = ""; public static readonly List<string> Links = new List<string>(); public static int Accent = -1, BannerY = 50, BannerX = 50, BannerZoom = 100, AvX = 50, AvY = 50, AvZoom = 100, Followers;   /* Accent: -1 = theme colour, else 0xRRGGBB. Zoom is a percent: 100 fills the frame, less shows the whole picture with bars, more zooms in */ public static bool Published, Private, HideLocation, HideFollowers;
    static string File0 { get { return Path.Combine(StudioEnv.DataDir, "profile-page.txt"); } }   // (profile.txt belongs to the project library: your name and picture)
    public static string Dir { get { string d = Path.Combine(StudioEnv.DataDir, "profile"); try { Directory.CreateDirectory(d); } catch { } return d; } }
    public static void Load()
    {
        try
        {
            if (!File.Exists(File0)) return; bool sawRgb = false;
            foreach (var l in File.ReadAllLines(File0, Encoding.UTF8)) { int i = l.IndexOf('='); if (i < 1) continue; string k = l.Substring(0, i), v = l.Substring(i + 1); int n;
                switch (k) { case "bio": Bio = v; break; case "location": Location = v; break; case "link": { string cl = CleanLink(v); if (cl.Length > 0 && !Links.Contains(cl)) Links.Add(cl); } break; case "links": Links.Clear(); foreach (var x in v.Split(new[] { (char)31 }, StringSplitOptions.RemoveEmptyEntries)) { string cl2 = CleanLink(x); if (cl2.Length > 0 && Links.Count < 6 && !Links.Contains(cl2)) Links.Add(cl2); } break; case "tags": Tags = v; break; case "banner": BannerPath = v; break; case "key": Key = v; break;
                    case "private": Private = v == "1"; break; case "hideloc": HideLocation = v == "1"; break; case "hidefol": HideFollowers = v == "1"; break; case "accent": if (int.TryParse(v, out n) && n >= 0 && n < 360 && !sawRgb) Accent = HueToPacked(n); break; case "accentrgb": { int rgb; if (v.Length == 6 && int.TryParse(v, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb)) { Accent = rgb; sawRgb = true; } } break; case "bannerx": if (int.TryParse(v, out n)) BannerX = Math.Max(0, Math.Min(100, n)); break; case "bannerzoom": if (int.TryParse(v, out n)) BannerZoom = Math.Max(5, Math.Min(400, n)); break; case "avx": if (int.TryParse(v, out n)) AvX = Math.Max(0, Math.Min(100, n)); break; case "avy": if (int.TryParse(v, out n)) AvY = Math.Max(0, Math.Min(100, n)); break; case "avzoom": if (int.TryParse(v, out n)) AvZoom = Math.Max(5, Math.Min(400, n)); break; case "bannery": if (int.TryParse(v, out n)) BannerY = Math.Max(0, Math.Min(100, n)); break; case "followers": if (int.TryParse(v, out n)) Followers = Math.Max(0, n); break; case "published": Published = v == "1"; break; } }
        }
        catch { }
    }
    public static void Save()
    {
        try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(File0, "bio=" + Bio.Replace("\n", " ") + "\nlocation=" + Location + "\nlinks=" + string.Join(((char)31).ToString(), Links.ToArray()) + "\nprivate=" + (Private ? "1" : "0") + "\nhideloc=" + (HideLocation ? "1" : "0") + "\nhidefol=" + (HideFollowers ? "1" : "0") + "\ntags=" + Tags + "\nbanner=" + BannerPath + "\nkey=" + Key + "\naccentrgb=" + (Accent < 0 ? "" : Accent.ToString("x6")) + "\nbannerx=" + BannerX + "\nbannerzoom=" + BannerZoom + "\navx=" + AvX + "\navy=" + AvY + "\navzoom=" + AvZoom + "\nbannery=" + BannerY + "\nfollowers=" + Followers + "\npublished=" + (Published ? "1" : "0") + "\n", Encoding.UTF8); } catch { }
    }
    static readonly string[] BadSchemes = { "javascript", "data", "file", "vbscript", "about", "blob", "chrome", "view-source", "ms-msdt", "search-ms", "ms-office", "cmd", "powershell" };
    // any web address (or a shop, streaming or app link) is fine; a bare "example.com/me" becomes https://example.com/me; only links that could run something or open local files are refused ("" = not usable)
    public static string CleanLink(string raw)
    {
        if (raw == null) return ""; var sb = new StringBuilder(); foreach (char c in raw) if (!char.IsControl(c) && !char.IsWhiteSpace(c) && "<>\"`\\".IndexOf(c) < 0) sb.Append(c); string s = sb.ToString(); if (s.Length > 200) s = s.Substring(0, 200); if (s.Length == 0) return "";
        var m = System.Text.RegularExpressions.Regex.Match(s, "^([A-Za-z][A-Za-z0-9+.\\-]{1,19}):");
        if (!m.Success) return System.Text.RegularExpressions.Regex.IsMatch(s, "^[^/:?#]+\\.[^/:?#]{2,}") ? "https://" + s : "";
        string sc = m.Groups[1].Value.ToLowerInvariant(); if (Array.IndexOf(BadSchemes, sc) >= 0) return "";
        if (sc == "mailto") return System.Text.RegularExpressions.Regex.IsMatch(s, "^mailto:[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? s : "";
        return System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Za-z][A-Za-z0-9+.\\-]*://[^/\\s]+") ? s : "";
    }
    // the words to show for a link: the site's name for a web address
    public static string LinkLabel(string url)
    {
        try { if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return url.Substring(7); var u = new Uri(url); string h = u.Host.StartsWith("www.") ? u.Host.Substring(4) : u.Host; return h.Length > 0 ? h + (u.AbsolutePath.Length > 1 ? u.AbsolutePath : "") : url; } catch { return url; }
    }
    // ---- colours and framing shared by the app and the published profile ----
    public static Color ColorOf(int packed) { return Color.FromArgb(255, (packed >> 16) & 255, (packed >> 8) & 255, packed & 255); }
    public static int Pack(Color c) { return (c.R << 16) | (c.G << 8) | c.B; }
    public static int HueToPacked(int hue) { return Pack(ThemeDef.Hsl(hue, 0.82, 0.60)); }   // the nine swatches of older versions were hues
    public static int AccentHue { get { return Accent < 0 ? -1 : (int)Math.Round(ColorOf(Accent).GetHue()) % 360; } }   // what older programs and the website read
    // the picture drawn into a w x h frame: zoom 100 fills the frame (whatever size or shape the picture has), less shows more of it with bars, more zooms in; x and y (0 to 100) slide it
    public static Bitmap RenderFramed(Image src, int w, int h, int zoomPct, int xPct, int yPct, Color bg)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double cover = Math.Max(w / (double)src.Width, h / (double)src.Height), sc = cover * Math.Max(5, Math.Min(400, zoomPct)) / 100.0, dw = src.Width * sc, dh = src.Height * sc;
        double ox = dw >= w ? -(dw - w) * Math.Max(0, Math.Min(100, xPct)) / 100.0 : (w - dw) / 2.0, oy = dh >= h ? -(dh - h) * Math.Max(0, Math.Min(100, yPct)) / 100.0 : (h - dh) / 2.0;
        using (var g = Graphics.FromImage(bmp)) { g.Clear(bg); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.SmoothingMode = SmoothingMode.HighQuality; g.DrawImage(src, new RectangleF((float)ox, (float)oy, (float)dw, (float)dh)); }
        return bmp;
    }
    // the zoom (percent) at which the WHOLE picture is visible in the frame
    public static int ContainZoom(int iw, int ih, int w, int h) { double a = w / (double)Math.Max(1, iw), b = h / (double)Math.Max(1, ih); return (int)Math.Max(5, Math.Min(100, Math.Floor(Math.Min(a, b) / Math.Max(a, b) * 100))); }
    public static string EnsureKey()
    {
        if (Key.Length >= 32) return Key; var b = new byte[24]; using (var r = new System.Security.Cryptography.RNGCryptoServiceProvider()) r.GetBytes(b); var sb = new StringBuilder(); foreach (var x in b) sb.Append(x.ToString("x2")); Key = sb.ToString(); Save(); return Key;
    }
}

// talking to the website (always from a background thread)
static class ProfApi
{
    static string Http(string method, string path, string body, string bearer = null)
    {
        var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = 15000; rq.ReadWriteTimeout = 15000; if (!string.IsNullOrEmpty(bearer)) rq.Headers["Authorization"] = "Bearer " + bearer;
        if (body != null) { var b = Encoding.UTF8.GetBytes(body); rq.ContentType = "application/json"; rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length); }
        try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
        catch (WebException ex) { if (ex.Response != null) { using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); } throw; }
    }
    public static byte[] Bytes(string path)
    {
        var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + path); rq.UserAgent = "PurphexMusic"; rq.Timeout = 15000; rq.ReadWriteTimeout = 15000;
        using (var r = (HttpWebResponse)rq.GetResponse()) using (var s = r.GetResponseStream()) using (var ms = new MemoryStream()) { var buf = new byte[8192]; int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 600 * 1024) throw new IOException("too big"); } return ms.ToArray(); }
    }
    public static string J(string s) { var sb = new StringBuilder("\""); foreach (char c in s ?? "") { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c < 32) sb.Append(' '); else sb.Append(c); } return sb.Append('"').ToString(); }
    static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is string ? (string)v : ""; }
    static int N(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (int)Math.Max(-1, Math.Min(int.MaxValue, (double)v)) : 0; }
    static bool B(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v; }
    // a published colour: accentRgb ("rrggbb"), else the hue older programs publish, else -1 (the theme colour)
    static int AccentOf(Dictionary<string, object> d) { string h = S(d, "accentRgb"); int rgb; if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb)) return rgb; if (d != null && d.ContainsKey("accent")) { int hue = N(d, "accent"); if (hue >= 0 && hue < 360) return ProfileStore.HueToPacked(hue); } return -1; }
    static string Clip(string s, int n) { s = (s ?? "").Replace('<', ' ').Replace('>', ' '); return EmojiText.Cut(s, n); }
    public static ArtistInfo Parse(Dictionary<string, object> d)
    {
        if (d == null) return null; var a = new ArtistInfo { Id = S(d, "id"), Name = Clip(S(d, "name"), 30), Bio = Clip(S(d, "bio"), 200), Location = Clip(S(d, "location"), 30), Tracks = N(d, "tracks"), Followers = N(d, "followers"), Avatar = B(d, "avatar"), Banner = B(d, "banner"), Fantasy = B(d, "fantasy"), FollowersHidden = B(d, "followersHidden"), Accent = AccentOf(d), Av = N(d, "av"), Bv = N(d, "bv") };
        if (a.Id.Length == 0 || a.Name.Length == 0) return null; object t; if (d.TryGetValue("tags", out t) && t is List<object>) { var l = new List<string>(); foreach (var x in (List<object>)t) if (x is string && l.Count < 10) l.Add(Clip((string)x, 20)); a.Tags = l.ToArray(); }
        object lk; if (d.TryGetValue("links", out lk) && lk is List<object>) foreach (var x in (List<object>)lk) { if (!(x is string)) continue; string cl = ProfileStore.CleanLink((string)x); if (cl.Length > 0 && a.Links.Count < 10) a.Links.Add(cl); }
        object tl; if (d.TryGetValue("trackList", out tl) && tl is List<object>) foreach (var x in (List<object>)tl) { var td = x as Dictionary<string, object>; if (td == null || a.TrackList.Count >= 40) continue; var at = new ArtistTrack { Title = Clip(S(td, "t"), 40), Bpm = N(td, "b"), Secs = N(td, "s") }; object g; if (td.TryGetValue("g", out g) && g is List<object>) { var gl = new List<string>(); foreach (var y in (List<object>)g) if (y is string) gl.Add(Clip((string)y, 20)); at.Tags = string.Join(" ", gl.ToArray()); } if (at.Title.Length > 0) a.TrackList.Add(at); }
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
    public static string Publish(string json, out ArtistInfo me)
    {
        me = null;
        try
        {
            var o = MiniJson.Parse(Http("POST", "/api/profiles/publish", json, Edition.Token)) as Dictionary<string, object>; if (B(o, "ok")) { object p; if (o.TryGetValue("profile", out p)) me = Parse(p as Dictionary<string, object>); return null; }
            string err = S(o, "err"); if (err == "rate") return "You have published a lot of times in the last hour. Try again a bit later."; if (err == "name") return "Your name needs at least 2 letters."; if (err == "big") return "A picture is too big: choose a smaller one."; if (err == "image") return "A picture could not be read. Choose a JPG or PNG."; if (err == "full") return "The community is full right now."; return "The community could not take your profile.";
        }
        catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    public static bool Remove(string key) { try { var o = MiniJson.Parse(Http("POST", "/api/profiles/remove", "{\"key\":" + J(key) + "}")) as Dictionary<string, object>; return B(o, "ok"); } catch { return false; } }
    // returns the new follower count, or -1 when it failed
    public static int Follow(string key, string id, bool on, out bool following) { following = false; try { var o = MiniJson.Parse(Http("POST", "/api/profiles/follow", "{\"key\":" + J(key) + ",\"id\":" + J(id) + ",\"on\":" + (on ? "true" : "false") + "}")) as Dictionary<string, object>; if (!B(o, "ok")) return -1; following = B(o, "following"); return N(o, "followers"); } catch { return -1; } }
    public static bool State(string key, string id) { try { var o = MiniJson.Parse(Http("POST", "/api/profiles/state", "{\"key\":" + J(key) + ",\"id\":" + J(id) + "}")) as Dictionary<string, object>; return B(o, "following"); } catch { return false; } }
    public static ArtistInfo Me(string key, List<ArtistInfo> following)
    {
        try
        {
            var o = MiniJson.Parse(Http("POST", "/api/profiles/me", "{\"key\":" + J(key) + "}")) as Dictionary<string, object>; if (o == null) return null; object f; if (following != null && o.TryGetValue("following", out f) && f is List<object>) foreach (var it in (List<object>)f) { var a = Parse(it as Dictionary<string, object>); if (a != null) following.Add(a); }
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
        if (pic != null) { g.DrawImageUnscaled(pic, (int)Math.Round(r.X), (int)Math.Round(r.Y)); }
        else
        {
            Color a = ProfAccent(accent); using (var br = new LinearGradientBrush(r, MixC(Color.FromArgb(255, 8, 4, 20), a, 0.55f), MixC(Color.FromArgb(255, 8, 4, 20), NeonPink, 0.4f), 20f)) g.FillRectangle(br, r);
            using (var pen = new Pen(Color.FromArgb(60, Color.White), 1.2f)) for (int i = 0; i < 9; i++) g.DrawBezier(pen, r.X, r.Bottom - 20 - i * 6, r.X + r.Width * 0.3f, r.Y + 20 + i * 5, r.X + r.Width * 0.6f, r.Bottom - 10 - i * 8, r.Right, r.Y + 30 + i * 6);
        }
        g.Restore(gs); using (var p = RR(r, 18)) using (var pen = new Pen(Color.FromArgb(160, ProfAccent(accent)), 2f)) g.DrawPath(pen, p);
    }
    void StatCard(Graphics g, RectangleF r, string label, string value, Color vc)
    {
        FillRR(g, TM.C(255, 12, 6, 28), r, 14); Win.DrawNeonBox(g, r, false); Txt(g, label, 10, DimC, Rc(r.X, r.Y + 8, r.Width, 14), StringAlignment.Center, true); Txt(g, value, 24, vc, Rc(r.X, r.Y + 24, r.Width, 34), StringAlignment.Center, true);
    }
    // links as little buttons: the site's name, and a click opens it in your browser
    float LinkChips(Graphics g, float x0, float y, float maxX, List<string> links)
    {
        float x = x0; int rows = 1;
        foreach (var l in links)
        {
            string lb = ProfileStore.LinkLabel(l); if (lb.Length > 26) lb = lb.Substring(0, 25) + "..."; float w = Math.Min(36 + lb.Length * 7.4f, Math.Max(60, maxX - x0)); if (x + w > maxX && x > x0) { x = x0; y += 34; rows++; } var r = Rc(x, y, w, 28); string url = l;
            Btn(g, r, "o  " + lb, 1, () => OpenLink(url), 11); Hint2(r, "Open " + url + " in your browser"); x += w + 8;
        }
        return rows * 34 + 2;   // the height used (a row per 34 pixels)
    }
    void OpenLink(string url)
    {
        string u = ProfileStore.CleanLink(url); if (u.Length == 0) { Toast("That link cannot be opened."); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u) { UseShellExecute = true }); } catch (Exception ex) { Toast("Could not open that link: " + ex.Message); }
    }
    void EditLinks()
    {
        string t = StudioDialogs.Links(this, string.Join("\r\n", ProfileStore.Links.ToArray())); if (t == null) return; var l = new List<string>(); int dropped = 0;
        foreach (var raw in t.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) { if (raw.Trim().Length == 0) continue; string cl = ProfileStore.CleanLink(raw); if (cl.Length == 0) { dropped++; continue; } if (!l.Contains(cl) && l.Count < 10) l.Add(cl); }
        ProfileStore.Links.Clear(); ProfileStore.Links.AddRange(l); ProfileStore.Save(); Toast(l.Count + (l.Count == 1 ? " link saved." : " links saved.") + (dropped > 0 ? " " + dropped + " could not be used (they were not web addresses)." : ""));
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
    void TrackRow(Graphics g, RectangleF r, string name, string meta, ProjectEntry playable, Action<RectangleF> after = null)
    {
        FillRR(g, TM.C(255, 14, 8, 30), r, 12); Win.DrawNeonBox(g, r, false); float tx = r.X + 14;
        if (playable != null)
        {
            var pb = Rc(r.X + 10, r.Y + (r.Height - 36) / 2, 36, 36); bool on = string.Equals(previewPath, playable.Path, StringComparison.OrdinalIgnoreCase) && !previewPaused; var e0 = playable;
            FillRR(g, Hot(pb) ? TM.C(255, 120, 60, 220) : TM.C(255, 70, 40, 170), pb, 18); using (var b = new SolidBrush(Color.White)) { if (on) { g.FillRectangle(b, pb.X + 12, pb.Y + 10, 4, 16); g.FillRectangle(b, pb.X + 20, pb.Y + 10, 4, 16); } else g.FillPolygon(b, new[] { new PointF(pb.X + 13, pb.Y + 9), new PointF(pb.X + 13, pb.Y + 27), new PointF(pb.X + 28, pb.Y + 18) }); }
            Reg(pb, () => PreviewToggle(e0)); Hint2(pb, on ? "Pause" : "Play this track"); tx = pb.Right + 12;
        }
        float rp = after != null ? 196 : 14; Txt(g, name, 14, Ink, Rc(tx, r.Y + 6, r.Width - (tx - r.X) - rp, 24), StringAlignment.Near, true); Txt(g, meta, 11, DimC, Rc(tx, r.Y + 30, r.Width - (tx - r.X) - rp, 18), StringAlignment.Near, false);
        if (after != null) after(r);
    }

    // ================================================================= PROFILE =================================================================
    float profScroll, profContentH, profEditH; string profStatus = ""; volatile bool profBusy; List<ArtistInfo> profFollowing = new List<ArtistInfo>(); bool profLoaded;

    void ProfileLoad() { if (profLoaded) return; profLoaded = true; ProfileStore.Load(); if (ProfileStore.Published) ProfileRefresh(); }
    // what the community knows about you (the number of followers and the artists you follow)
    void ProfileRefresh()
    {
        if (ProfileStore.Key.Length < 32 || profBusy) return; profBusy = true; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { var fol = new List<ArtistInfo>(); var me = ProfApi.Me(key, fol); try { BeginInvoke(new Action(() => { profBusy = false; if (me != null) { ProfileStore.Followers = me.Followers; ProfileStore.Save(); } profFollowing = fol; lastInput = DateTime.Now; })); } catch { profBusy = false; } });
    }
    // the banner is kept whole (no larger than 2400 pixels), so any picture of any size or shape works; it is fitted to the frame when drawn and can be zoomed and moved
    void SetBanner(string file)
    {
        try
        {
            byte[] b = ProjectLibrary.ScaledJpeg(file, 2400); Directory.CreateDirectory(ProfileStore.Dir); string dest = Path.Combine(ProfileStore.Dir, "banner.jpg"); File.WriteAllBytes(dest, b);
            ProfileStore.BannerPath = dest; ProfileStore.BannerZoom = 100; ProfileStore.BannerX = 50; ProfileStore.BannerY = 50; ProfileStore.Save(); ForgetFramed(); QueueRepublish(); Toast("Banner changed: it was resized to fit. Use ZOOM and the MOVE buttons to place it.");
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
    void ClearBanner() { try { if (ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath)) File.Delete(ProfileStore.BannerPath); } catch { } ProfileStore.BannerPath = ""; ProfileStore.BannerZoom = 100; ProfileStore.BannerX = 50; ProfileStore.BannerY = 50; ProfileStore.Save(); ForgetFramed(); QueueRepublish(); Toast("Banner removed."); }
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
        var sb = new StringBuilder("{\"key\":" + ProfApi.J(ProfileStore.EnsureKey()) + ",\"name\":" + ProfApi.J(ProjectLibrary.ProfileName) + ",\"bio\":" + ProfApi.J(ProfileStore.Bio) + ",\"location\":" + ProfApi.J(ProfileStore.Location) + ",\"links\":[" + string.Join(",", ProfileStore.Links.ConvertAll(x => ProfApi.J(x)).ToArray()) + "],\"tags\":" + ProfApi.J(ProfileStore.Tags) + ",\"accent\":" + ProfileStore.AccentHue + ",\"accentRgb\":" + ProfApi.J(ProfileStore.Accent < 0 ? "" : ProfileStore.Accent.ToString("x6")) + ",\"tracks\":[");
        int n = 0; foreach (var e in ProjectLibrary.AllByEdited()) { if (n >= 40 || e.Missing || !e.OnProfile) continue; /* private projects never leave this PC */ if (n > 0) sb.Append(','); sb.Append("{\"t\":" + ProfApi.J(e.Name) + ",\"b\":" + (int)Math.Round(e.Bpm) + ",\"s\":" + (int)Math.Round(e.Seconds) + ",\"g\":" + ProfApi.J(e.Tags) + "}"); n++; }
        sb.Append(']'); sb.Append(",\"privacy\":{\"private\":" + (ProfileStore.Private ? "true" : "false") + ",\"hideLocation\":" + (ProfileStore.HideLocation ? "true" : "false") + ",\"hideFollowers\":" + (ProfileStore.HideFollowers ? "true" : "false") + "}");
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
    void ToggleOnProfile(ProjectEntry e)
    {
        ProjectLibrary.SetOnProfile(e, !e.OnProfile);
        Toast(e.OnProfile ? "On your profile: people see its name, tempo, length and tags (never the audio)." + (ProfileStore.Published ? "" : " Publish your profile to share it.") : "Private again: it is not on your profile.");
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
        ThreadPool.QueueUserWorkItem(o => { ArtistInfo me; string err = ProfApi.Publish(json, out me); try { BeginInvoke(new Action(() => { profBusy = false; if (err == null) { ProfileStore.Published = true; if (me != null) ProfileStore.Followers = me.Followers; ProfileStore.Save(); profStatus = "Your profile is public: other producers can find you in ARTISTS."; ProfileRefresh(); } else profStatus = err; lastInput = DateTime.Now; })); } catch { profBusy = false; } });
    }
    void UnpublishProfile()
    {
        if (profBusy || ProfileStore.Key.Length < 32) return; profBusy = true; profStatus = "Making your profile private..."; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { bool ok = ProfApi.Remove(key); try { BeginInvoke(new Action(() => { profBusy = false; if (ok) { ProfileStore.Published = false; ProfileStore.Followers = 0; ProfileStore.Save(); profStatus = "Your profile is private again. Nobody can find it."; } else profStatus = "Could not reach the Purphex community. Check your internet connection."; })); } catch { profBusy = false; } });
    }
    void EditText(string what, string cur, int max, Action<string> set) { string t = Ask(what, cur); if (t == null) return; t = EmojiText.Cut(t.Trim(), max); set(t); ProfileStore.Save(); ProjectLibrary.Save(); }

    void PaintProfile(Graphics g)
    {
        ProfileLoad(); float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, bottom = H - (previewPath.Length > 0 ? 62 : 0); PaintLogoBackdrop(g, Rc(0, top, W, bottom - top));
        float cw = Math.Min(1100, W - 110), cx = (W - 54 - cw) / 2f; if (cx < 20) { cx = 20; cw = W - 94; } float viewH = bottom - top;
        profScroll = Math.Max(0, Math.Min(Math.Max(0, profContentH - viewH + 20), profScroll)); regs.Add(new HitReg { R = Rc(0, top, W, viewH), OnWheel = d => profScroll = Math.Max(0, Math.Min(Math.Max(0, profContentH - viewH + 20), profScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(Rc(0, top, W - 56, viewH), CombineMode.Intersect); float y = top + 14 - profScroll, y00 = y; Color acc = ProfAccent(ProfileStore.Accent);
        // ---- the profile card: banner, picture, name, about ----
        var ban = Rc(cx, y, cw, Math.Max(110, Math.Min(240, cw / 5f))); Bitmap bp = ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath) ? FramedPic(ProfileStore.BannerPath + File.GetLastWriteTime(ProfileStore.BannerPath).Ticks, () => LoadFileImage(ProfileStore.BannerPath), (int)ban.Width, (int)ban.Height, ProfileStore.BannerZoom, ProfileStore.BannerX, ProfileStore.BannerY) : null;   /* the banner is 5 to 1: any picture is resized to fit it */
        PaintBannerArt(g, ban, bp, ProfileStore.Accent); if (Edition.Has(Tier.Fantasy)) { FantasyBorder(g, Rc(ban.X - 3, ban.Y - 3, ban.Width + 6, ban.Height + 6), 16, "me" + ProjectLibrary.ProfileName); FantasyBadge(g, Rc(ban.X + 12, ban.Y + 12, 100, 24)); } var pill = Rc(ban.Right - 120, ban.Y + 12, 108, 24); FillRR(g, ProfileStore.Published ? TM.C(235, 28, 112, 86) : TM.C(235, 60, 52, 98), pill, 12); Txt(g, ProfileStore.Published ? "PUBLIC" : "PRIVATE", 11, Color.White, pill, StringAlignment.Center, true);
        var av = Rc(ban.X + 28, ban.Bottom - 60, 120, 120); Bitmap ab = ProjectLibrary.AvatarPath.Length > 0 ? FramedPic(AvatarFile + File.GetLastWriteTime(AvatarFile).Ticks, () => LoadFileImage(AvatarFile), 120, 120, ProfileStore.AvZoom, ProfileStore.AvX, ProfileStore.AvY) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 7, av.Y - 7, av.Width + 14, av.Height + 14), 30); DrawFrameImage(g, av, ab, acc);   /* a rounded square: the whole picture can be fitted in it */
        float tx = av.Right + 24, ty = ban.Bottom + 12; Txt(g, ProjectLibrary.ProfileName.ToUpper(), 28, Ink, Rc(tx, ty, cw - (tx - cx) - 20, 40), StringAlignment.Near, true);
        string sub = (ProfileStore.Location.Length > 0 ? ProfileStore.Location.ToUpper() + "   -   " : "") + ProfileStore.Followers + (ProfileStore.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   " + profFollowing.Count + " FOLLOWING"; Txt(g, sub, 12, NeonCyan, Rc(tx, ty + 40, cw - (tx - cx) - 20, 20), StringAlignment.Near, true);
        float by = ban.Bottom + 70 + 6; if (by < av.Bottom + 10) by = av.Bottom + 10; y = by;
        if (ProfileStore.Bio.Length > 0) { TxtWrap(g, ProfileStore.Bio, 14, Ink, Rc(cx + 6, y, cw - 12, 44)); y += 48; } else { Txt(g, "NO BIO YET. WRITE A FEW WORDS ABOUT YOUR MUSIC BELOW.", 12, DimC, Rc(cx + 6, y, cw - 12, 22), StringAlignment.Near, false); y += 28; }
        if (ProfileStore.Tags.Length > 0) { y += TagChips(g, cx + 6, y, cx + cw, ProfileStore.Tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), acc, true); }
        if (ProfileStore.Links.Count > 0) { y += LinkChips(g, cx + 6, y, cx + cw, ProfileStore.Links); }
        y += 8;
        // ---- numbers ----
        var all = ProjectLibrary.AllByEdited(); int tracks = 0; double secs = 0; foreach (var e in all) { tracks += e.Tracks; secs += e.Seconds; } UseTime.Seed(secs);
        { float sw = (cw - 3 * 14) / 4f; StatCard(g, Rc(cx, y, sw, 64), "PROJECTS", all.Count.ToString(), Ink); StatCard(g, Rc(cx + (sw + 14), y, sw, 64), "TRACKS", tracks.ToString(), NeonCyan); StatCard(g, Rc(cx + 2 * (sw + 14), y, sw, 64), "TIME MADE", DurMade((long)UseTime.Seconds), NeonPink); StatCard(g, Rc(cx + 3 * (sw + 14), y, sw, 64), "FOLLOWERS", ProfileStore.Followers.ToString(), acc); y += 64 + 20; }
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
        row("YOUR NAME", ProjectLibrary.ProfileName, "Change the name shown on your profile", () => { string nm = Ask("Your name:", ProjectLibrary.ProfileName); if (nm == null) return; nm = nm.Trim(); if (nm.Length > 30) nm = nm.Substring(0, 30); if (nm.Length > 0) { ProjectLibrary.ProfileName = nm; ProjectLibrary.Save(); } });
        row("BIO", ProfileStore.Bio, "A few words about you and your music (200 letters)", () => EditText("Your bio (up to 200 letters):", ProfileStore.Bio, 200, s => ProfileStore.Bio = s));
        row("WHERE YOU ARE", ProfileStore.Location, "Your city or country (optional)", () => EditText("Where are you from? (optional)", ProfileStore.Location, 30, s => ProfileStore.Location = s));
        row("YOUR LINKS", ProfileStore.Links.Count == 0 ? "" : ProfileStore.Links.Count + (ProfileStore.Links.Count == 1 ? " link: " : " links: ") + ProfileStore.LinkLabel(ProfileStore.Links[0]), "Add any links you like (your website, YouTube, SoundCloud, Spotify, Instagram, a shop...): one on every line", () => EditLinks());
        row("YOUR GENRES", ProfileStore.Tags, "Pick the genres you make, or add your own (up to 10)", () => { string t = StudioDialogs.Tags(this, ProfileStore.Tags); if (t != null) { ProfileStore.Tags = ProjectLibrary.CleanTags(t); ProfileStore.Save(); } });
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
        profTracksTop = Math.Max(0, y - y00 - 8); Txt(g, "YOUR PUBLISHED PROJECTS", 18, Ink, Rc(cx + 6, y, 400, 28), StringAlignment.Near, true);
        { int pubN = 0; foreach (var e0 in all) if (e0.OnProfile) pubN++; var allB = Rc(cx + cw - 330, y - 2, 160, 32); var noB = Rc(cx + cw - 164, y - 2, 164, 32); Btn(g, allB, "PUBLISH ALL", 2, () => { foreach (var e1 in ProjectLibrary.AllByEdited()) if (!e1.Missing) ProjectLibrary.SetOnProfile(e1, true); QueueRepublish(); }, 11); Hint2(allB, "Put every project on your profile");
          Btn(g, noB, "MAKE ALL PRIVATE", pubN > 0 ? 3 : 0, () => { foreach (var e1 in ProjectLibrary.AllByEdited()) ProjectLibrary.SetOnProfile(e1, false); QueueRepublish(); }, 11); Hint2(noB, "Take every project off your profile"); y += 34;
          Txt(g, pubN + " of " + all.Count + " projects are public. The rest are private.", 12, DimC, Rc(cx + 6, y, cw - 12, 20), StringAlignment.Near, false); y += 28; }
        var shownPub = all.FindAll(x0 => x0.OnProfile);
        if (shownPub.Count == 0) { Txt(g, all.Count == 0 ? "Nothing here yet. Make something and press Save: it is private until you publish it." : "No project is published. Press the person button on a project in My Projects, or PUBLISH ALL.", 13, DimC, Rc(cx + 6, y, cw - 12, 24), StringAlignment.Near, false); y += 40; }
        foreach (var e in shownPub) { var rr0 = Rc(cx, y, cw, 58); if (rr0.Bottom > top - 4 && rr0.Y < bottom + 4) { string meta = e.Tracks + (e.Tracks == 1 ? " TRACK" : " TRACKS") + (e.Bpm > 0 ? "   -   " + e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "   -   " + (int)(e.Seconds / 60) + ":" + ((int)(e.Seconds % 60)).ToString("00") : "") + (e.Tags.Length > 0 ? "   -   #" + e.Tags.Replace(" ", " #") : ""); var ee = e; TrackRow(g, rr0, e.Name, meta, e, rr => { var chip = Rc(rr.Right - 184, rr.Y + 13, 172, 32); Btn(g, chip, ee.OnProfile ? "PUBLIC ON PROFILE" : "PUBLISH TO PROFILE", ee.OnProfile ? 4 : 1, () => ToggleOnProfile(ee), 11); Hint2(chip, ee.OnProfile ? "Public: press to make it private again" : "Private: press to put it on your profile"); }); } y += 66; }
        profContentH = y - y00 + 30; g.Restore(gs);
        VScroll(g, Rc(W - 52, top + 8, 42, viewH - 16), profContentH + 20, viewH, () => profScroll, v => profScroll = v);
    }

    // ================================================================= ARTISTS =================================================================
    string artQuery = "", artErr = ""; bool artSearchOn, artLoading, artOnlyFollowing; int artSort; List<ArtistInfo> artList = new List<ArtistInfo>(); ArtistInfo artSel; bool artSelLoading, artFollowing; float artScroll, artContentH; DateTime artQueryAt = DateTime.MinValue; bool artDirty, artFetched; string artReported = "";
    static readonly string[] ArtSortNames = { "MOST FOLLOWED", "NEWEST", "A TO Z" }, ArtSortArgs = { "followers", "new", "name" };

    void OpenArtists() { tab = 7; artSel = null; if (!artFetched) { artFetched = true; ArtFetch(); } }
    void OpenArtist(string id)
    {
        tab = 7; artSelLoading = true; artErr = ""; artScroll = 0; var stub = artList.Find(a => a.Id == id); if (stub == null) foreach (var f in profFollowing) if (f.Id == id) stub = f; artSel = stub; string key = ProfileStore.EnsureKey();
        ThreadPool.QueueUserWorkItem(o => { ArtistInfo a = null; bool fol = false; try { a = ProfApi.Get(id); if (a != null) fol = ProfApi.State(key, id); } catch { } try { BeginInvoke(new Action(() => { artSelLoading = false; if (a != null) { artSel = a; artFollowing = fol; } else if (artSel == null) { artErr = "Could not open that profile. It may have been removed, or you are offline."; } lastInput = DateTime.Now; })); } catch { } });
    }
    void ArtFetch()
    {
        artLoading = true; artErr = ""; string q = artQuery, sort = ArtSortArgs[artSort]; bool only = artOnlyFollowing; string key = ProfileStore.EnsureKey();
        ThreadPool.QueueUserWorkItem(o =>
        {
            List<ArtistInfo> l = null; string err = ""; try { if (only) { var fol = new List<ArtistInfo>(); ProfApi.Me(key, fol); l = fol.FindAll(a => q.Length == 0 || (a.Name + " " + string.Join(" ", a.Tags)).ToLowerInvariant().Contains(q.ToLowerInvariant())); } else l = ProfApi.Search(q, sort); } catch { err = "Could not reach the Purphex community. Check your internet connection."; }
            try { BeginInvoke(new Action(() => { artLoading = false; if (l != null) { artList = l; artErr = ""; } else artErr = err; artScroll = 0; lastInput = DateTime.Now; })); } catch { }
        });
    }
    // pictures of artists come from the website in the background and are kept
    readonly Dictionary<string, Bitmap> artImgs = new Dictionary<string, Bitmap>(); readonly HashSet<string> artImgPending = new HashSet<string>();
    Bitmap ArtistImage(string id, string kind, int ver, int w, int h)
    {
        string k = id + "|" + kind + "|" + ver + "|" + w + "x" + h; Bitmap b; if (artImgs.TryGetValue(k, out b)) return b; if (artImgPending.Contains(k)) return null; artImgPending.Add(k);
        ThreadPool.QueueUserWorkItem(o => { Bitmap res = null; try { var data = ProfApi.Bytes("/api/profiles/" + id + "/" + kind); using (var ms = new MemoryStream(data)) using (var im = Image.FromStream(ms, false, false)) { res = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double sc = Math.Max(w / (double)im.Width, h / (double)im.Height); float sw = (float)(w / sc), sh = (float)(h / sc); using (var gg = Graphics.FromImage(res)) { gg.InterpolationMode = InterpolationMode.HighQualityBicubic; gg.DrawImage(im, new RectangleF(0, 0, w, h), new RectangleF((im.Width - sw) / 2f, (im.Height - sh) / 2f, sw, sh), GraphicsUnit.Pixel); } } } catch { res = null; }
            try { BeginInvoke(new Action(() => { if (artImgs.Count > 120) { foreach (var v in artImgs.Values) if (v != null) v.Dispose(); artImgs.Clear(); } artImgs[k] = res; artImgPending.Remove(k); lastInput = DateTime.Now; })); } catch { } });
        return null;
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
        float y = top + 12, h = 36; var sb = Rc(24, y, Math.Max(200, Math.Min(440, W - 48 - 360)), h); FillRR(g, TM.C(255, 14, 7, 32), sb, 9); Win.DrawNeonBox(g, sb, artSearchOn);
        bool blink = ((int)(Environment.TickCount / 500) % 2) == 0 && artSearchOn; Txt(g, artQuery.Length == 0 ? (blink ? "|  " : "   ") + "SEARCH ARTISTS BY NAME OR GENRE" : artQuery + (blink ? "|" : ""), 13, artQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 14, sb.Y, sb.Width - 54, sb.Height), StringAlignment.Near, artQuery.Length > 0);
        artSearchR = sb; Reg(sb, () => artSearchOn = true); Hint2(sb, "Type a name or a genre such as dnb, trap or lofi. Click here first.");
        if (artQuery.Length > 0) { var x = Rc(sb.Right - 34, sb.Y + 6, 24, 24); Btn(g, x, "x", 3, () => { artQuery = ""; artDirty = true; artQueryAt = DateTime.MinValue; }, 12); Hint2(x, "Clear the search"); }
        float x0 = sb.Right + 14; var sortB = Rc(x0, y, 190, h); Btn(g, sortB, "SORT: " + ArtSortNames[artSort], 0, () => { artSort = (artSort + 1) % 3; artFetched = true; ArtFetch(); }, 12); Hint2(sortB, "Change how the artists are ordered"); x0 = sortB.Right + 10;
        var folB = Rc(x0, y, 150, h); Chip(g, folB, "FOLLOWING", artOnlyFollowing, () => { artOnlyFollowing = !artOnlyFollowing; ArtFetch(); }, "Show only the artists you follow"); x0 = folB.Right + 10;
        y += h + 14; float cardW = 250, cardH = 238, gap = 16; float areaW = W - 56 - 24; int cols = Math.Max(1, (int)((areaW + gap) / (cardW + gap))); float used = cols * cardW + (cols - 1) * gap, ox = 24 + (areaW - used) / 2f; var area = Rc(0, y, W - 56, bottom - y);
        int rows = (artList.Count + cols - 1) / cols; artContentH = rows * (cardH + gap); float viewH = bottom - y; artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 6), artScroll)); regs.Add(new HitReg { R = area, OnWheel = d => artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 6), artScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < artList.Count; i++)
        {
            var a = artList[i]; float cx = ox + (i % cols) * (cardW + gap), cy = y + (i / cols) * (cardH + gap) - artScroll; var card = Rc(cx, cy, cardW, cardH); if (card.Bottom < y || card.Y > bottom) continue; bool hot = Hot(card) && area.Contains(MousePos); Color ac = ProfAccent(a.Accent);
            FillRR(g, TM.C(hot ? 255 : 235, 16, 9, 34), card, 16); Win.DrawNeonBox(g, card, hot); if (a.Fantasy) FantasyBorder(g, card, 16, a.Id); var ban = Rc(card.X + 8, card.Y + 8, card.Width - 16, 78); Bitmap bp = a.Banner ? ArtistImage(a.Id, "banner", a.Bv, (int)ban.Width, (int)ban.Height) : null; PaintBannerArt(g, ban, bp, a.Accent);
            var av = Rc(card.X + 18, ban.Bottom - 30, 60, 60); Bitmap ab = a.Avatar ? ArtistImage(a.Id, "avatar", a.Av, 60, 60) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 4, av.Y - 4, 68, 68), 15); DrawFrameImage(g, av, ab, ac);
            Txt(g, a.Name, 15, Ink, Rc(av.Right + 12, ban.Bottom + 4, card.Right - av.Right - 22, 22), StringAlignment.Near, true); if (a.Fantasy) FantasyBadge(g, Rc(ban.X + 8, ban.Y + 8, 92, 20)); Txt(g, (a.FollowersHidden ? "" : a.Followers + (a.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   ") + a.Tracks + (a.Tracks == 1 ? " TRACK" : " TRACKS"), 10, NeonCyan, Rc(av.Right + 12, ban.Bottom + 26, card.Right - av.Right - 22, 16), StringAlignment.Near, true);
            if (a.Bio.Length > 0) TxtWrap(g, a.Bio, 11, DimC, Rc(card.X + 14, av.Bottom + 8, card.Width - 28, 32)); if (a.Tags.Length > 0) TagChips(g, card.X + 14, card.Bottom - 34, card.Right - 12, a.Tags, ac);
            var open = Reg(card, () => OpenArtist(a.Id)); Hint2(card, "Open " + a.Name + "'s profile");
        }
        g.Restore(gs); VScroll(g, Rc(W - 52, y, 42, viewH - 6), artContentH + 6, viewH, () => artScroll, v => artScroll = v);
        string msg = artErr.Length > 0 ? artErr : (artLoading ? "LOOKING FOR ARTISTS..." : (artList.Count == 0 ? (artOnlyFollowing ? "YOU DO NOT FOLLOW ANYONE YET." : (artQuery.Length > 0 ? "NO ARTIST MATCHES THAT." : "NO ARTISTS HAVE PUBLISHED A PROFILE YET. BE THE FIRST: GO TO YOUR PROFILE AND PRESS PUBLISH.")) : ""));
        if (msg.Length > 0) TxtWrap(g, msg, 14, artErr.Length > 0 ? Bad : DimC, Rc(40, y + 30, W - 140, 60));
    }
    RectangleF artSearchR;

    void PaintArtistView(Graphics g, float W, float top, float bottom)
    {
        var a = artSel; float cw = Math.Min(1100, W - 110), cx = (W - 54 - cw) / 2f; if (cx < 20) { cx = 20; cw = W - 94; } float viewH = bottom - top; Color acc = ProfAccent(a.Accent);
        artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 20), artScroll)); regs.Add(new HitReg { R = Rc(0, top, W, viewH), OnWheel = d => artScroll = Math.Max(0, Math.Min(Math.Max(0, artContentH - viewH + 20), artScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(Rc(0, top, W - 56, viewH), CombineMode.Intersect); float y = top + 14 - artScroll, y00 = y;
        var back = Rc(cx, y, 150, 34); Btn(g, back, "< ALL ARTISTS", 1, () => { artSel = null; artScroll = 0; }, 12); Hint2(back, "Back to the list of artists"); y += 46;
        var ban = Rc(cx, y, cw, Math.Max(110, Math.Min(240, cw / 5f))); Bitmap bp = a.Banner ? ArtistImage(a.Id, "banner", a.Bv, (int)ban.Width, (int)ban.Height) : null; PaintBannerArt(g, ban, bp, a.Accent); if (a.Fantasy) { FantasyBorder(g, Rc(ban.X - 3, ban.Y - 3, ban.Width + 6, ban.Height + 6), 16, a.Id); FantasyBadge(g, Rc(ban.X + 12, ban.Y + 12, 100, 24)); }
        var av = Rc(ban.X + 28, ban.Bottom - 60, 120, 120); Bitmap ab = a.Avatar ? ArtistImage(a.Id, "avatar", a.Av, 120, 120) : null; FillRR(g, TM.C(255, 8, 4, 18), Rc(av.X - 7, av.Y - 7, av.Width + 14, av.Height + 14), 30); DrawFrameImage(g, av, ab, acc);
        float tx = av.Right + 24, ty = ban.Bottom + 12; Txt(g, a.Name.ToUpper(), 28, Ink, Rc(tx, ty, cw - (tx - cx) - 220, 40), StringAlignment.Near, true);
        Txt(g, (a.Location.Length > 0 ? a.Location.ToUpper() + "   -   " : "") + (a.FollowersHidden ? "" : a.Followers + (a.Followers == 1 ? " FOLLOWER" : " FOLLOWERS") + "   -   ") + a.Tracks + (a.Tracks == 1 ? " TRACK" : " TRACKS"), 12, NeonCyan, Rc(tx, ty + 40, cw - (tx - cx) - 20, 20), StringAlignment.Near, true);
        var fb = Rc(cx + cw - 190, ty + 4, 180, 40); Btn(g, fb, artSelLoading ? "WORKING..." : (artFollowing ? "FOLLOWING  (UNFOLLOW)" : "FOLLOW"), artFollowing ? 0 : 2, () => FollowToggle(), 12); Hint2(fb, artFollowing ? "Stop following this artist" : "Follow this artist: they appear under FOLLOWING and on your profile");
        y = Math.Max(ban.Bottom + 70 + 6, av.Bottom + 10); if (a.Bio.Length > 0) { TxtWrap(g, a.Bio, 14, Ink, Rc(cx + 6, y, cw - 12, 44)); y += 48; } if (a.Tags.Length > 0) { y += TagChips(g, cx + 6, y, cx + cw, a.Tags, acc, true); }
        if (a.Links.Count > 0) { y += LinkChips(g, cx + 6, y, cx + cw, a.Links); }
        y += 8; float sw = (cw - 2 * 14) / 3f; StatCard(g, Rc(cx, y, sw, 64), "FOLLOWERS", a.FollowersHidden ? "-" : a.Followers.ToString(), acc); StatCard(g, Rc(cx + sw + 14, y, sw, 64), "TRACKS", a.Tracks.ToString(), NeonCyan); int tot = 0; foreach (var t in a.TrackList) tot += t.Secs; StatCard(g, Rc(cx + 2 * (sw + 14), y, sw, 64), "TIME MADE", DurMade(tot), NeonPink); y += 64 + 22;
        Txt(g, "TRACKS", 18, Ink, Rc(cx + 6, y, 400, 28), StringAlignment.Near, true); y += 36;
        if (a.TrackList.Count == 0) { Txt(g, artSelLoading ? "LOADING..." : "THIS ARTIST HAS NOT LISTED ANY TRACKS YET.", 13, DimC, Rc(cx + 6, y, cw - 12, 24), StringAlignment.Near, false); y += 40; }
        foreach (var t in a.TrackList) { var rr0 = Rc(cx, y, cw, 58); if (rr0.Bottom > top - 4 && rr0.Y < bottom + 4) TrackRow(g, rr0, t.Title, (t.Bpm > 0 ? t.Bpm + " BPM" : "") + (t.Bpm > 0 && t.Secs > 0 ? "   -   " : "") + (t.Secs > 0 ? Dur(t.Secs) : "") + (t.Tags.Length > 0 ? "   -   #" + t.Tags.Replace(" ", " #") : ""), null); y += 66; }
        var rp = Rc(cx, y + 8, 220, 30); Btn(g, rp, artReported == a.Id ? "REPORTED, THANK YOU" : "REPORT THIS PROFILE", 0, () => { if (artReported == a.Id) return; artReported = a.Id; string id = a.Id; ThreadPool.QueueUserWorkItem(o => ProfApi.Report(id)); Toast("Thank you. Three reports hide a profile."); }, 11); Hint2(rp, "Tell us if this profile is not OK"); y += 54;
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
