using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

// EPs and albums on the Community tab: under ALBUMS and EPS each release is ONE card; clicking it (or the "EP NAME - TRACK 2 OF 5" line on any of its songs)
// opens the release: its songs in order, each with its own play button, and a big PLAY EP / PLAY ALBUM button that plays them one after another.
partial class StudioForm
{
    internal bool epOpen; string epCid = "", epTitle = "", epKind = "ep", epArtist = "", epArtistId = ""; internal List<FeedSong> epSongs = new List<FeedSong>(); bool epFetching; float epScroll;
    internal List<FeedSong> epQueue; internal int epQueueAt;   // PLAY EP: the songs still to come

    bool FeedGrouped { get { return commFilter == 2 || commFilter == 3; } }
    // ALBUMS / EPS: one card per release (its first track stands for it)
    List<FeedSong> GroupReleases(List<FeedSong> l)
    {
        var seen = new Dictionary<string, FeedSong>(); var outL = new List<FeedSong>();
        foreach (var s in l) { if (s.Kind == "single" || s.Cid.Length == 0) { outL.Add(s); continue; } FeedSong cur; if (!seen.TryGetValue(s.Cid, out cur)) { seen[s.Cid] = s; outL.Add(s); } else if (s.Ci < cur.Ci) { outL[outL.IndexOf(cur)] = s; seen[s.Cid] = s; } }
        return outL;
    }
    internal void OpenRelease(FeedSong s)
    {
        if (s == null || s.Cid.Length == 0) return; epOpen = true; epCid = s.Cid; epTitle = s.Coll; epKind = s.Kind; epArtist = s.Artist; epArtistId = s.ArtistId; epScroll = 0;
        epSongs = feed.FindAll(x => x.Cid == s.Cid); epSongs.Sort((a, b) => a.Ci.CompareTo(b.Ci));
        if (epSongs.Count < s.Cn && !epFetching)
        {   // the filters (a genre) can hide some of its songs: ask the website for the whole release
            epFetching = true; string cid = s.Cid, kind = s.Kind;
            ThreadPool.QueueUserWorkItem(o => { List<FeedSong> l = null; try { List<string> tg; l = ProfApi.Feed(kind, false, "", "", out tg); } catch { } try { BeginInvoke(new Action(() => { epFetching = false; if (l != null && epCid == cid) { var all = l.FindAll(x => x.Cid == cid); all.Sort((a, b) => a.Ci.CompareTo(b.Ci)); if (all.Count > epSongs.Count) epSongs = all; } lastInput = DateTime.Now; })); } catch { epFetching = false; } });
        }
    }
    // PLAY EP: the first song now, the next one each time a song ends
    internal void PlayRelease()
    {
        if (epSongs.Count == 0) return; epQueue = new List<FeedSong>(epSongs); epQueueAt = 0; var s = epQueue[0];
        PlayArtistSong(StubArtist(s), StubTrack(s)); Toast("Playing the " + (epKind == "album" ? "album" : "EP") + " \"" + epTitle + "\": " + epQueue.Count + " songs, one after another.");
    }
    static string FeedKey(FeedSong s) { return "artist:" + s.ArtistId + ":" + s.Id; }
    // called when a preview reaches its end: true = the next song of the EP started
    internal bool EpQueueNext()
    {
        if (epQueue == null) return false;
        if (epQueueAt < 0 || epQueueAt >= epQueue.Count || !string.Equals(previewPath, FeedKey(epQueue[epQueueAt]), StringComparison.Ordinal)) { epQueue = null; return false; }   // something else was played meanwhile
        if (epQueueAt + 1 >= epQueue.Count) { epQueue = null; return false; }
        epQueueAt++; var s = epQueue[epQueueAt]; PlayArtistSong(StubArtist(s), StubTrack(s)); return true;
    }

    void PaintEpSheet(Graphics g)
    {
        if (!epOpen) return; if (tab != 4) { epOpen = false; return; } float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); lastInput = DateTime.Now;
        Reg(all, () => epOpen = false); using (var dim = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.FillRectangle(dim, all);
        float bw = Math.Min(640, W - 40), bh = Math.Min(H - 60, 640); var box = Rc(W / 2 - bw / 2, H / 2 - bh / 2, bw, bh); FillRR(g, TM.C(252, 14, 8, 30), box, 22); Glow(g, NeonCyan, box, 22, 0.9f, 4); StrokeRR(g, NeonCyan, box, 22, 1.8f); Reg(box, () => { });
        string kindW = epKind == "album" ? "ALBUM" : "EP";
        var pill = Rc(box.X + 24, box.Y + 20, 70, 22); FillRR(g, TM.C(255, 190, 50, 170), pill, 11); Txt(g, kindW, 11, Color.White, pill, StringAlignment.Center, true);
        Txt(g, epTitle, 22, Ink, Rc(box.X + 24, box.Y + 46, box.Width - 110, 34), StringAlignment.Near, true);
        var an = Rc(box.X + 24, box.Y + 80, box.Width - 48, 22); Txt(g, epArtist + "  -  " + epSongs.Count + (epSongs.Count == 1 ? " SONG" : " SONGS"), 13, NeonCyan, an, StringAlignment.Near, true); string aid = epArtistId; Reg(an, () => { epOpen = false; OpenArtist(aid); }); Hint2(an, "Open " + epArtist + "'s page");
        var cl = Rc(box.Right - 52, box.Y + 18, 32, 30); Btn(g, cl, "x", 3, () => epOpen = false, 13);
        bool qOn = epQueue != null && epQueue.Count > 0 && epQueue[0].Cid == epCid;
        var playAll = Rc(box.X + 24, box.Y + 112, box.Width - 48, 44); Btn(g, playAll, qOn ? "PLAYING THE " + kindW + "  (" + (epQueueAt + 1) + " OF " + epQueue.Count + ")" : "PLAY " + kindW, qOn ? 4 : 2, () => PlayRelease(), 16); Hint2(playAll, "Play every song of the " + kindW.ToLower() + ", in order, one after another");
        var lr = Rc(box.X + 20, playAll.Bottom + 14, box.Width - 40, box.Bottom - playAll.Bottom - 30); float rowH = 52, total = epSongs.Count * rowH, maxS = Math.Max(0, total - lr.Height); epScroll = Math.Max(0, Math.Min(maxS, epScroll));
        regs.Add(new HitReg { R = lr, OnWheel = d => epScroll = Math.Max(0, Math.Min(maxS, epScroll - d * 50)) });
        var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
        for (int i = 0; i < epSongs.Count; i++)
        {
            var s = epSongs[i]; var row = Rc(lr.X, lr.Y + i * rowH - epScroll, lr.Width - (maxS > 0 ? 50 : 0), rowH - 8); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue;
            bool mine = string.Equals(previewPath, FeedKey(s), StringComparison.Ordinal), playing = mine && previewEng != null && previewEng.Playing && !previewPaused, hot = Hot(row);
            if (mine) PaintPickedFx(g, row, 10); else { FillRR(g, hot ? TM.C(220, 70, 36, 130) : TM.C(170, 30, 16, 58), row, 10); StrokeRR(g, Color.FromArgb(hot ? 220 : 110, NeonViolet), row, 10, 1.2f); }
            Txt(g, (i + 1).ToString(), 15, NeonCyan, Rc(row.X + 12, row.Y, 30, row.Height), StringAlignment.Near, true);
            Txt(g, s.Title, 14, Ink, Rc(row.X + 46, row.Y, row.Width - 46 - 120, row.Height), StringAlignment.Near, true);
            var fs = s; var pb = Rc(row.Right - 108, row.Y + 7, 96, row.Height - 14); Btn(g, pb, playing ? "PAUSE" : "PLAY", playing ? 4 : 1, () => { epQueue = null; FeedClick(fs); }, 12); Hint2(pb, "Play only this song");
            Reg(row, () => { epQueue = null; FeedClick(fs); });
        }
        g.Restore(gs); if (maxS > 0) VScroll(g, Rc(lr.Right - 44, lr.Y, 42, lr.Height), total, lr.Height, () => epScroll, v => epScroll = v);
        if (epSongs.Count == 0) Txt(g, epFetching ? "LOOKING FOR THE SONGS..." : "NO SONGS FOUND", 13, DimC, lr, StringAlignment.Center, true);
    }
}
