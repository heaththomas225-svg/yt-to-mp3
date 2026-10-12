using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;

// The rules of a profile that the website keeps too (lib/linkpolicy.js, lib/profanity.js): which links are allowed, clean bios, clean tags, and how a picture is fitted in its frame.
static partial class ProfileStore
{
    // music streaming services and social media platforms: nothing else may be linked (the same list as the website)
    static readonly string[][] Hosts = {
        new[] { "spotify.com", "Spotify" }, new[] { "spoti.fi", "Spotify" }, new[] { "music.apple.com", "Apple Music" }, new[] { "itunes.apple.com", "Apple Music" }, new[] { "podcasts.apple.com", "Apple Podcasts" },
        new[] { "youtube.com", "YouTube" }, new[] { "youtu.be", "YouTube" }, new[] { "soundcloud.com", "SoundCloud" }, new[] { "bandcamp.com", "Bandcamp" }, new[] { "tidal.com", "Tidal" }, new[] { "deezer.com", "Deezer" },
        new[] { "audiomack.com", "Audiomack" }, new[] { "beatport.com", "Beatport" }, new[] { "mixcloud.com", "Mixcloud" }, new[] { "pandora.com", "Pandora" }, new[] { "napster.com", "Napster" }, new[] { "qobuz.com", "Qobuz" },
        new[] { "bandlab.com", "BandLab" }, new[] { "audius.co", "Audius" }, new[] { "anghami.com", "Anghami" }, new[] { "boomplay.com", "Boomplay" }, new[] { "jiosaavn.com", "JioSaavn" }, new[] { "gaana.com", "Gaana" },
        new[] { "vimeo.com", "Vimeo" }, new[] { "dailymotion.com", "Dailymotion" }, new[] { "twitch.tv", "Twitch" }, new[] { "kick.com", "Kick" },
        new[] { "instagram.com", "Instagram" }, new[] { "facebook.com", "Facebook" }, new[] { "fb.com", "Facebook" }, new[] { "fb.me", "Facebook" }, new[] { "twitter.com", "X (Twitter)" }, new[] { "x.com", "X (Twitter)" },
        new[] { "tiktok.com", "TikTok" }, new[] { "snapchat.com", "Snapchat" }, new[] { "reddit.com", "Reddit" }, new[] { "redd.it", "Reddit" }, new[] { "discord.com", "Discord" }, new[] { "discord.gg", "Discord" },
        new[] { "threads.net", "Threads" }, new[] { "bsky.app", "Bluesky" }, new[] { "mastodon.social", "Mastodon" }, new[] { "t.me", "Telegram" }, new[] { "telegram.me", "Telegram" }, new[] { "wa.me", "WhatsApp" }, new[] { "whatsapp.com", "WhatsApp" },
        new[] { "pinterest.com", "Pinterest" }, new[] { "tumblr.com", "Tumblr" }, new[] { "linkedin.com", "LinkedIn" }, new[] { "purphexmusic.com", "Purphex Music" } };
    static readonly Regex AmazonMusic = new Regex("^music\\.amazon\\.[a-z.]{2,8}$");
    // the name of the service a web address belongs to ("" when it is not a streaming or social platform)
    public static string ServiceOf(string host)
    {
        host = (host ?? "").ToLowerInvariant().TrimEnd('.'); if (AmazonMusic.IsMatch(host)) return "Amazon Music";
        foreach (var h in Hosts) if (host == h[0] || host.EndsWith("." + h[0])) return h[1]; return "";
    }
    // "" when the link is not allowed, else the clean https address
    public static string CleanLink(string raw)
    {
        if (raw == null) return ""; var sb = new StringBuilder(); foreach (char c in raw) if (!char.IsControl(c) && !char.IsWhiteSpace(c) && "<>\"`\\".IndexOf(c) < 0) sb.Append(c); string s = sb.ToString(); if (s.Length > 200) s = s.Substring(0, 200); if (s.Length == 0) return "";
        if (!Regex.IsMatch(s, "^[A-Za-z][A-Za-z0-9+.\\-]*:")) s = "https://" + s;
        Uri u; if (!Uri.TryCreate(s, UriKind.Absolute, out u)) return ""; if (u.Scheme != "https" && u.Scheme != "http") return "";
        if (u.UserInfo.Length > 0 || (!u.IsDefaultPort)) return ""; if (ServiceOf(u.IdnHost).Length == 0) return "";
        string path = u.AbsolutePath == "/" && u.Query.Length == 0 && u.Fragment.Length == 0 ? "" : u.AbsolutePath; return "https://" + u.IdnHost + path + u.Query + u.Fragment;
    }
    // the words to show for a link: the name of the service
    public static string LinkLabel(string url)
    {
        try { var u = new Uri(url); string sv = ServiceOf(u.IdnHost); if (sv.Length > 0) return sv; string h = u.Host.StartsWith("www.") ? u.Host.Substring(4) : u.Host; return h.Length > 0 ? h + (u.AbsolutePath.Length > 1 ? u.AbsolutePath : "") : url; } catch { return url; }
    }
    const string Tlds = "com|net|org|io|co|uk|xyz|me|tv|ly|gg|link|site|app|dev|info|biz|ru|cn|top|shop|store|online|page|live|fm|cc|ws|to|us|eu|de|fr";
    static readonly Regex UrlLike = new Regex("(?:https?://|www\\.)[^\\s]+|\\b[a-z0-9-]+(?:\\.[a-z0-9-]+)*\\.(?:" + Tlds + ")\\b(?:/[^\\s]*)?", RegexOptions.IgnoreCase);
    // a bio keeps the streaming and social links in it and loses every other web address
    public static string CleanBio(string s) { s = UrlLike.Replace(s ?? "", m => CleanLink(m.Value).Length > 0 ? m.Value : ""); return Regex.Replace(s, "\\s{2,}", " ").Trim(); }
    // the allowed links written in a bio (shown as buttons)
    public static List<string> BioLinks(string s) { var l = new List<string>(); foreach (Match m in UrlLike.Matches(s ?? "")) { string c = CleanLink(m.Value); if (c.Length > 0 && !l.Contains(c)) l.Add(c); } return l; }
    // every link to show on a profile: the ones added as links, then the ones written in the bio
    public static List<string> AllLinks(List<string> links, string bio) { var l = new List<string>(); foreach (var x in links) if (!l.Contains(x)) l.Add(x); foreach (var x in BioLinks(bio)) if (!l.Contains(x)) l.Add(x); return l; }

    // ---- tags: any number, but no swearing (the same words as the website) ----
    static readonly string[] BadStrong = { "fuck", "shit", "cunt", "nigg", "fagg", "bitch", "whore", "slut", "wank", "twat", "piss", "motherf", "dickhead", "asshole", "arsehole", "retard", "porn", "bollock", "tranny", "paki", "coon", "kike", "spic", "chink", "cocksuck", "jizz", "blowjob", "handjob", "dildo", "bukkake", "hitler", "nazi" };
    static readonly string[] BadExact = { "ass", "arse", "cock", "dick", "prick", "tit", "tits", "cum", "fag", "fck", "fuk", "fuq", "sht", "btch", "cnt", "wtf", "stfu", "milf", "hoe", "thot", "pussy", "pedo", "rapist" };
    static readonly string[] BadOk = { "scunthorpe", "penistone", "cocktail", "classic", "assassin", "assist", "class", "bass", "bassline", "brass", "grass", "mass", "pass", "glass", "jazz", "compass", "assembly", "passion", "hancock" };
    static string BadNorm(string s) { var sb = new StringBuilder(); foreach (char ch in (s ?? "").ToLowerInvariant()) { char c = ch; switch (c) { case '0': c = 'o'; break; case '1': c = 'i'; break; case '3': c = 'e'; break; case '4': c = 'a'; break; case '5': c = 's'; break; case '7': c = 't'; break; case '@': c = 'a'; break; case '$': c = 's'; break; case '!': c = 'i'; break; } if ((c >= 'a' && c <= 'z') || c == '-') sb.Append(c); } return sb.ToString(); }
    public static bool IsBadTag(string tag)
    {
        string n = BadNorm(tag), flat = n.Replace("-", ""); if (flat.Length == 0 || Array.IndexOf(BadOk, flat) >= 0) return false;
        if (Array.IndexOf(BadExact, flat) >= 0) return true; foreach (var w in n.Split('-')) if (Array.IndexOf(BadExact, w) >= 0) return true;
        foreach (var w in BadOk) flat = flat.Replace(w, ""); foreach (var w in BadStrong) if (flat.Contains(w)) return true; return false;
    }

    // ---- a picture in its frame: zoom 0 (the default) shows the WHOLE picture and stretches its edges to fill what is left over; a percent zooms (100 fills the frame) ----
    static void Band(Graphics g, Image src, RectangleF d, RectangleF s, ImageAttributes ia) { g.DrawImage(src, new[] { new PointF(d.Left, d.Top), new PointF(d.Right, d.Top), new PointF(d.Left, d.Bottom) }, s, GraphicsUnit.Pixel, ia); }
    public static Bitmap RenderFramed(Image src, int w, int h, int zoomPct, int xPct, int yPct, Color bg)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double contain = Math.Min(w / (double)src.Width, h / (double)src.Height), cover = Math.Max(w / (double)src.Width, h / (double)src.Height);
        double sc = zoomPct <= 0 ? contain : cover * Math.Max(5, Math.Min(400, zoomPct)) / 100.0, dw = src.Width * sc, dh = src.Height * sc;
        double ox = dw >= w ? -(dw - w) * Math.Max(0, Math.Min(100, xPct)) / 100.0 : (w - dw) / 2.0, oy = dh >= h ? -(dh - h) * Math.Max(0, Math.Min(100, yPct)) / 100.0 : (h - dh) / 2.0;
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(bg); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.SmoothingMode = SmoothingMode.HighQuality;
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY); int sw = Math.Max(1, Math.Min(src.Width, 3)), sh = Math.Max(1, Math.Min(src.Height, 3));
                // the empty bands beside (or above and below) the picture are its own edge pixels stretched out
                if (dw < w - 0.5) { float lw = (float)ox, rx = (float)(ox + dw), rw = w - rx; if (lw > 0.5f) Band(g, src, new RectangleF(0, (float)Math.Max(0, oy), lw, (float)Math.Min(h, dh)), new RectangleF(0, 0, sw, src.Height), ia); if (rw > 0.5f) Band(g, src, new RectangleF(rx, (float)Math.Max(0, oy), rw, (float)Math.Min(h, dh)), new RectangleF(src.Width - sw, 0, sw, src.Height), ia); }
                if (dh < h - 0.5) { float th = (float)oy, by = (float)(oy + dh), bh = h - by; if (th > 0.5f) Band(g, src, new RectangleF((float)Math.Max(0, ox), 0, (float)Math.Min(w, dw), th), new RectangleF(0, 0, src.Width, sh), ia); if (bh > 0.5f) Band(g, src, new RectangleF((float)Math.Max(0, ox), by, (float)Math.Min(w, dw), bh), new RectangleF(0, src.Height - sh, src.Width, sh), ia); }
            }
            g.DrawImage(src, new RectangleF((float)ox, (float)oy, (float)dw, (float)dh));
        }
        return bmp;
    }
}
