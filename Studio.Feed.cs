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

// The Community tab: every song that was published from Purphex Music, from all artists. Filter by genre, new releases, albums and EPs; play a song (and its music video),
// save it to your SAVED songs, go to the artist, or open it on the website. Your own projects are in My Projects (that is where you open them).
sealed class FeedSong
{
    public string Id = "", Title = "", ArtistId = "", Artist = "", Kind = "single", Coll = "", Cid = ""; public string[] Tags = new string[0]; public long At; public int Plays, Likes, Ci, Cn, CoverVer; public bool Cover, Video;
}

// the songs you saved (kept on this PC as a short list of ids; the songs themselves stay on the community page)
static class SavedSongs
{
    static readonly object lk = new object(); static readonly HashSet<string> ids = new HashSet<string>(); static bool loaded;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "savedsongs.txt"); } }
    static void Load() { if (loaded) return; loaded = true; try { if (File.Exists(FileP)) foreach (var l in File.ReadAllLines(FileP)) if (l.Trim().Length > 0) ids.Add(l.Trim()); } catch { } }
    public static bool Has(string id) { lock (lk) { Load(); return ids.Contains(id); } }
    public static bool Toggle(string id) { lock (lk) { Load(); bool now; if (ids.Contains(id)) { ids.Remove(id); now = false; } else { ids.Add(id); now = true; } try { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllLines(FileP, new List<string>(ids).ToArray()); } catch { } return now; } }
    public static int Count { get { lock (lk) { Load(); return ids.Count; } } }
    public static void ResetForTest() { lock (lk) { ids.Clear(); loaded = false; } }
}

static partial class ProfApi
{
    // the feed from the website (public): kind = "" | single | ep | album
    public static List<FeedSong> Feed(string kind, bool fresh, string genre, string query, out List<string> tags)
    {
        tags = new List<string>(); string path = "/api/community/feed?kind=" + Uri.EscapeDataString(kind ?? "") + (fresh ? "&new=1" : "") + "&genre=" + Uri.EscapeDataString(genre ?? "") + "&q=" + Uri.EscapeDataString(query ?? "");
        var o = MiniJson.Parse(Http("GET", path, null)) as Dictionary<string, object>; if (o == null) return null; object arr; if (!o.TryGetValue("songs", out arr) || !(arr is List<object>)) return null; var l = new List<FeedSong>();
        foreach (var x in (List<object>)arr)
        {
            var d = x as Dictionary<string, object>; if (d == null) continue; var s = new FeedSong { Id = S(d, "id"), Title = Clip(S(d, "t"), 80), ArtistId = S(d, "a"), Artist = Clip(S(d, "an"), 30), Kind = S(d, "kind"), Coll = Clip(S(d, "coll"), 50), Cid = S(d, "cid"), Plays = N(d, "plays"), Likes = N(d, "likes"), Ci = N(d, "ci"), Cn = N(d, "cn"), CoverVer = N(d, "cv"), Cover = B(d, "cover"), Video = B(d, "video") };
            if (s.Kind != "ep" && s.Kind != "album") s.Kind = "single"; object at; if (d.TryGetValue("at", out at) && at is double) s.At = (long)(double)at; object tg; if (d.TryGetValue("tags", out tg) && tg is List<object>) { var tl = new List<string>(); foreach (var y in (List<object>)tg) if (y is string && tl.Count < 5) tl.Add(Clip((string)y, 20)); s.Tags = tl.ToArray(); }
            if (s.Id.Length > 0 && s.ArtistId.Length > 0 && s.Title.Length > 0) l.Add(s);
        }
        object tgs; if (o.TryGetValue("tags", out tgs) && tgs is List<object>) foreach (var y in (List<object>)tgs) if (y is string && tags.Count < 20) tags.Add(Clip((string)y, 20));
        return l;
    }
}

partial class StudioForm
{
    List<FeedSong> feed = new List<FeedSong>(); List<string> feedTags = new List<string>(); string feedGenre = "", feedErr = ""; volatile bool feedLoading; bool feedFetched; DateTime feedAt = DateTime.MinValue; int feedGen; float feedScroll;
    // the filters of the Community tab: commFilter 0 ALL, 1 NEW RELEASES, 2 ALBUMS, 3 EPS, 4 SAVED
    void FeedRefetch() { feedFetched = false; feedScroll = 0; commScroll = 0; }
    void FetchFeed()
    {
        feedFetched = true; feedLoading = true; feedErr = ""; feedAt = DateTime.Now; int gen = ++feedGen; int f = commFilter; string genre = feedGenre; string kind = f == 2 ? "album" : (f == 3 ? "ep" : ""); bool fresh = f == 1;
        ThreadPool.QueueUserWorkItem(o =>
        {
            List<string> tags = null; List<FeedSong> l = null; string err = ""; try { l = ProfApi.Feed(kind, fresh, genre, "", out tags); if (l == null) err = "The community gave an answer that could not be read."; } catch { err = "Could not reach the Purphex community. Check your internet connection."; }
            try { BeginInvoke(new Action(() => { if (gen != feedGen) return; feedLoading = false; if (l != null) { feed = l; feedErr = ""; if (tags != null && (genre.Length == 0 || feedTags.Count == 0)) feedTags = tags; } else feedErr = err; lastInput = DateTime.Now; })); } catch { feedLoading = false; }
        });
    }
    // the images of the covers come from the website in the background and are kept
    readonly Dictionary<string, Bitmap> feedImgs = new Dictionary<string, Bitmap>(); readonly HashSet<string> feedImgPending = new HashSet<string>(); readonly Dictionary<string, DateTime> feedImgFail = new Dictionary<string, DateTime>(); readonly Dictionary<string, int> feedImgFails = new Dictionary<string, int>();
    Bitmap FeedImage(FeedSong s, int w, int h)
    {
        string k = s.Id + "|" + s.CoverVer + "|" + w + "x" + h; Bitmap b; if (feedImgs.TryGetValue(k, out b)) return b; if (feedImgPending.Contains(k)) return null;
        DateTime ft; int fn; feedImgFails.TryGetValue(k, out fn); if (feedImgFail.TryGetValue(k, out ft) && (DateTime.Now - ft).TotalSeconds < Math.Min(60, 5 * Math.Pow(3, Math.Max(0, fn - 1)))) return null;   // a failed cover is tried again soon (5 s, 15 s, 45 s, then every minute), not every frame and not never
        feedImgPending.Add(k); string id = s.Id;
        ThreadPool.QueueUserWorkItem(o =>
        {
            Bitmap res = null; try { var data = ProfApi.Bytes("/api/community/cover/" + id, 3 * 1024 * 1024 + 64 * 1024); using (var ms = new MemoryStream(data)) using (var im = Image.FromStream(ms, false, false)) { res = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double sc = Math.Max(w / (double)im.Width, h / (double)im.Height); float sw = (float)(im.Width * sc), sh = (float)(im.Height * sc); using (var gg = Graphics.FromImage(res)) { gg.InterpolationMode = InterpolationMode.HighQualityBicubic; gg.DrawImage(im, (w - sw) / 2f, (h - sh) / 2f, sw, sh); } } } catch { res = null; }
            try { BeginInvoke(new Action(() => { if (feedImgs.Count > 150) { foreach (var v in feedImgs.Values) if (v != null) v.Dispose(); feedImgs.Clear(); } if (res != null) { feedImgs[k] = res; feedImgFail.Remove(k); feedImgFails.Remove(k); } else { feedImgFail[k] = DateTime.Now; int c0; feedImgFails.TryGetValue(k, out c0); feedImgFails[k] = c0 + 1; } feedImgPending.Remove(k); lastInput = DateTime.Now; })); } catch { }
        });
        return null;
    }
    static string Count(int n) { return n >= 1000000 ? (n / 1000000.0).ToString("0.#") + "M" : (n >= 1000 ? (n / 1000.0).ToString("0.#") + "K" : n.ToString()); }
    ArtistInfo StubArtist(FeedSong s) { return new ArtistInfo { Id = s.ArtistId, Name = s.Artist }; }
    ArtistTrack StubTrack(FeedSong s) { return new ArtistTrack { Title = s.Title, Song = s.Id, VideoUp = s.Video }; }
    void FeedClick(FeedSong s) { ArtistTrackClick(StubArtist(s), StubTrack(s)); }

    // the songs the filters show (SAVED is your own short list)
    List<FeedSong> FeedList()
    {
        if (FeedGrouped) return GroupReleases(feed);   // ALBUMS / EPS: one card per release
        if (commFilter != 4) return feed; var l = new List<FeedSong>(); foreach (var s in feed) if (SavedSongs.Has(s.Id)) l.Add(s); return l;
    }

    // the genres, then the songs as cards: art, kind (SINGLE / EP / ALBUM), video, the artist, the numbers, PLAY, SAVE SONG and a share link
    void PaintFeed(Graphics g, float W, float Hb, float ty, float cx0, float cw)
    {
        if (!feedFetched && !feedLoading) FetchFeed(); else if (!feedLoading && (DateTime.Now - feedAt).TotalSeconds > 120 && tab == 4) FetchFeed(); if (feedLoading) lastInput = DateTime.Now;
        float y = ty + 46, cx = cx0 + 4, chH = 28;
        Action<string, bool, Action> chip = (label, on, act) => { float cwid = 22 + label.Length * 7.4f; if (cx + cwid > cx0 + cw - 70) { cx = cx0 + 4; y += chH + 6; } Chip(g, Rc(cx, y, cwid, chH), label, on, act); cx += cwid + 6; };
        Txt(g, "GENRE", 11, DimC, Rc(cx0 + 4, y + 6, 50, 18), StringAlignment.Near, true); cx += 56;
        chip("ALL GENRES", feedGenre.Length == 0, () => { feedGenre = ""; FeedRefetch(); });
        foreach (var t0 in feedTags) { string t = t0; chip("#" + t.ToUpper(), feedGenre == t, () => { feedGenre = feedGenre == t ? "" : t; FeedRefetch(); }); }
        float fy = y + chH + 14, fh = Hb - fy; var area = Rc(cx0 - 6, fy, cw + 12, fh); var list = FeedList();
        float cardW = 214, cardH = 440, gap = 16; int cols = Math.Max(1, (int)((cw - 56 + gap) / (cardW + gap))); float used = cols * cardW + (cols - 1) * gap, ox = cx0 + (cw - 56 - used) / 2;
        int rows = (list.Count + cols - 1) / cols; float maxS = Math.Max(0, rows * (cardH + gap) - fh + 10); commScroll = Math.Max(0, Math.Min(maxS, commScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => commScroll = Math.Max(0, Math.Min(maxS, commScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect); bool overlayOk = !menuOpen && !ntOpen && !busOpen && !busy; int ringN = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i]; float x = ox + (i % cols) * (cardW + gap), yy = fy + (i / cols) * (cardH + gap) - commScroll; var card = Rc(x, yy, cardW, cardH); if (card.Bottom < fy || card.Y > Hb) continue;
            bool hot = overlayOk && area.Contains(MousePos) && card.Contains(MousePos); FeedCard(g, card, s, hot); if (ringN++ < 12) AnimRing(g, card, i % 2 == 0 ? NeonCyan : NeonPink);   // the border runs with lights, like the picture on a profile
        }
        g.Restore(gs); VScroll(g, Rc(cx0 + cw - 52, fy, 42, fh - 8), fh - 8 + maxS, fh - 8, () => commScroll, v => commScroll = v);
        string msg = feedErr.Length > 0 ? feedErr : (feedLoading && list.Count == 0 ? "LOOKING FOR SONGS..." : (list.Count == 0 ? (commFilter == 4 ? "NO SAVED SONGS YET. PRESS SAVE SONG UNDER A SONG." : (feedGenre.Length > 0 || commFilter > 0 ? "NO SONG MATCHES THESE FILTERS." : "NO SONGS HAVE BEEN PUBLISHED YET. BE THE FIRST: PUBLISH A PROJECT IN MY PROJECTS.")) : ""));
        if (msg.Length > 0) TxtWrap(g, msg, 14, feedErr.Length > 0 ? Bad : DimC, Rc(cx0 + 10, fy + 30, cw - 20, 60));
    }
    void FeedCard(Graphics g, RectangleF card, FeedSong s, bool hot)
    {
        FillRR(g, TM.C(hot ? 215 : 175, 18, 10, 38), card, 16); Win.DrawNeonBox(g, card, hot);
        float S = card.Width - 28; var art = Rc(card.X + 14, card.Y + 14, S, S); Bitmap bp = s.Cover ? FeedImage(s, (int)S, (int)S) : null;
        if (bp != null) { var gs2 = g.Save(); using (var p = RR(art, 14)) { g.SetClip(p, CombineMode.Intersect); g.DrawImage(bp, art); } g.Restore(gs2); using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 2f)) using (var p = RR(art, 14)) g.DrawPath(pen, p); }
        else DrawSquareArt(g, art, new ProjectEntry { Name = s.Title });
        string pkey = "artist:" + s.ArtistId + ":" + s.Id; bool mine = string.Equals(previewPath, pkey, StringComparison.Ordinal), playing = mine && previewEng != null && previewEng.Playing && !previewPaused;
        // a round play button on the picture
        var pb = Rc(art.X + S / 2 - 26, art.Y + S / 2 - 26, 52, 52); if (hot || playing || mine) { Glow(g, NeonCyan, pb, 26, 0.7f, 4); using (var br = new LinearGradientBrush(pb, playing ? TM.C(255, 255, 43, 214) : TM.C(255, 120, 70, 235), TM.C(255, 70, 40, 180), 90f)) g.FillEllipse(br, pb); using (var pen = new Pen(Color.White, 1.6f)) g.DrawEllipse(pen, pb); using (var b = new SolidBrush(Color.White)) { if (playing) { g.FillRectangle(b, pb.X + 17, pb.Y + 15, 7, 22); g.FillRectangle(b, pb.X + 29, pb.Y + 15, 7, 22); } else g.FillPolygon(b, new[] { new PointF(pb.X + 20, pb.Y + 14), new PointF(pb.X + 20, pb.Y + 38), new PointF(pb.X + 39, pb.Y + 26) }); } }
        Action<string, float, float, Color> pill = (txt, px, py, col) => { float w = 16 + txt.Length * 6.6f; var r = Rc(px, py, w, 18); FillRR(g, Color.FromArgb(225, col), r, 9); Txt(g, txt, 9, Color.White, r, StringAlignment.Center, true); };
        pill(s.Kind == "album" ? "ALBUM" : (s.Kind == "ep" ? "EP" : "SINGLE"), art.X + 8, art.Y + 8, s.Kind == "single" ? TM.C(255, 70, 40, 140) : TM.C(255, 190, 50, 170)); if (s.Video) pill("VIDEO", art.Right - 8 - (16 + 5 * 6.6f), art.Y + 8, TM.C(255, 255, 43, 214)); if ((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(s.At)).TotalDays < 14) pill("NEW", art.X + 8, art.Bottom - 26, TM.C(255, 20, 150, 90));
        bool rel = FeedGrouped && s.Kind != "single" && s.Cid.Length > 0; string relW = s.Kind == "album" ? "ALBUM" : "EP";   // a release card: it opens the release (its songs and PLAY EP)
        if (rel) { Reg(card, () => OpenRelease(s)); Hint2(card, "Open the " + relW.ToLower() + ": see its " + s.Cn + " songs and play them all"); } else { Reg(card, () => FeedClick(s)); Hint2(card, mine ? (playing ? "Pause" : "Continue") : "Play this song" + (s.Video ? " (it has a music video)" : "")); }
        float ty2 = art.Bottom + 8; Txt(g, rel ? s.Coll : s.Title, 15, Ink, Rc(card.X + 14, ty2, card.Width - 28, 22), StringAlignment.Near, true);
        var an = Rc(card.X + 14, ty2 + 22, card.Width - 28, 20); bool hotA = Hot(an); Txt(g, s.Artist, 13, hotA ? Color.White : NeonCyan, an, StringAlignment.Near, true); string aid = s.ArtistId; Reg(an, () => OpenArtist(aid)); Hint2(an, "Open " + s.Artist + "'s page: message, add friend, follow");
        { var cr2 = Rc(card.X + 14, ty2 + 44, card.Width - 28, 16); bool hc = s.Kind != "single" && Hot(cr2); Txt(g, s.Kind == "single" ? "SINGLE" : (rel ? relW + "  -  " + s.Cn + " SONGS" : s.Coll + "  -  TRACK " + (s.Ci + 1) + " OF " + s.Cn), 10, s.Kind == "single" ? DimC : (hc ? Color.White : NeonCyan), cr2, StringAlignment.Near, s.Kind != "single"); if (s.Kind != "single" && s.Cid.Length > 0) { Reg(cr2, () => OpenRelease(s)); Hint2(cr2, "Open the " + (s.Kind == "album" ? "album" : "EP") + " " + s.Coll + ": its songs and PLAY " + (s.Kind == "album" ? "ALBUM" : "EP")); } }
        Txt(g, Count(s.Plays) + (s.Plays == 1 ? " PLAY" : " PLAYS") + "   -   " + Count(s.Likes) + (s.Likes == 1 ? " LIKE" : " LIKES"), 10, DimC, Rc(card.X + 14, ty2 + 62, card.Width - 28, 16), StringAlignment.Near, false);
        { float tx = card.X + 14; foreach (var tg in s.Tags) { string lb = "#" + tg; float tw = 14 + lb.Length * 6.6f; if (tx + tw > card.Right - 12) break; var tr = Rc(tx, ty2 + 82, tw, 18); FillRR(g, TM.C(110, 60, 36, 120), tr, 9); Txt(g, lb, 10, Ink, tr, StringAlignment.Center, false); tx += tw + 5; } }
        float by = card.Bottom - 12 - 16 - 6 - 30 - 6 - 34; var play = Rc(card.X + 14, by, card.Width - 28, 34); if (rel) Btn(g, play, "OPEN " + relW, 2, () => OpenRelease(s), 13); else Btn(g, play, playing ? "PAUSE" : "PLAY", playing ? 4 : 2, () => FeedClick(s), 13); Hint2(play, s.Video ? "Play the song. It has a music video: you are asked if you want to watch it" : "Play the song (AUDIO ONLY: it has no music video)");
        var save = Rc(card.X + 14, by + 40, card.Width - 28, 30); if (rel) { bool qOn = epQueue != null && epQueue.Count > 0 && epQueue[0].Cid == s.Cid; Btn(g, save, qOn ? "STOP  (" + (epQueueAt + 1) + " OF " + epQueue.Count + ")" : "PLAY NOW", qOn ? 4 : 2, () => PlayReleaseNow(s, qOn), 12); Hint2(save, qOn ? "Stop playing the " + relW.ToLower() : "Play every song of the " + relW.ToLower() + " in order, one after another"); } else { bool sv = SavedSongs.Has(s.Id); Btn(g, save, sv ? "SAVED  (REMOVE)" : "SAVE SONG", sv ? 4 : 1, () => { bool now = SavedSongs.Toggle(s.Id); Toast(now ? "Saved \"" + s.Title + "\". Find it under SAVED." : "Removed from your saved songs."); }, 12); Hint2(save, "Keep this song in your SAVED list (only the name is kept, the song stays on the community page)"); }
        var sh = Rc(card.X + 14, card.Bottom - 12 - 16, card.Width - 28, 16); bool hs = Hot(sh); Txt(g, "SHARE  -  OPEN ON THE WEBSITE", 10, hs ? Color.White : NeonCyan, sh, StringAlignment.Center, true); using (var pen = new Pen(Color.FromArgb(hs ? 220 : 120, NeonCyan), 1f)) g.DrawLine(pen, sh.X + sh.Width / 2 - 78, sh.Bottom - 1, sh.X + sh.Width / 2 + 78, sh.Bottom - 1);
        string sid = s.Id; Reg(sh, () => OpenCommunitySite("/community?song=" + sid)); Hint2(sh, "Open this song on the community page of the website (sign in there to like it and comment)");
    }
}
