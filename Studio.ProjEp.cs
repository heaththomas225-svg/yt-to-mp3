using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// EPs and albums on My Projects: the projects of a release are not separate cards, they are ONE card (EP or ALBUM). Opening it lists its projects in order:
// PLAY hears one without opening it, clicking a project asks to open it. (While you search, the projects show one by one so you can find them.)
partial class StudioForm
{
    internal List<ProjectEntry> projQueue; internal int projQueueAt; internal Release projQueueRel;   // PLAY NOW: the songs of an EP / album playing one after another
    internal bool projEpOpen; Release projEp; List<ProjectEntry> projEpEnts = new List<ProjectEntry>(); float projEpScroll;
    readonly Dictionary<Release, List<ProjectEntry>> projRelEnts = new Dictionary<Release, List<ProjectEntry>>();
    internal readonly List<object> projCardOrder = new List<object>();   // the cards in order: an EP or album sits where its newest song sits (like any project), never first just because it is an EP

    // takes the projects of each EP / album out of the list and returns the releases to show as cards (in the order they were made)
    internal List<Release> GroupProjectReleases(List<ProjectEntry> list)
    {
        projRelEnts.Clear(); projCardOrder.Clear(); var rels = new List<Release>(); if (projSearch.Trim().Length > 0) { projCardOrder.AddRange(list); return rels; } ReleaseStore.Load(); var order = new List<ProjectEntry>(list); var relOf = new Dictionary<ProjectEntry, Release>();
        var byPid = new Dictionary<string, ProjectEntry>(); foreach (var e in list) { string pid = CommunitySongs.PidOf(e.Path); if (!byPid.ContainsKey(pid)) byPid[pid] = e; }
        foreach (var r in ReleaseStore.All.ToArray())
        {
            var ents = new List<ProjectEntry>(); foreach (var p in r.Pids) { ProjectEntry e; if (byPid.TryGetValue(p, out e) && !ents.Contains(e)) ents.Add(e); }
            if (ents.Count == 0) continue; rels.Add(r); projRelEnts[r] = ents; foreach (var e in ents) { list.Remove(e); if (!relOf.ContainsKey(e)) relOf[e] = r; }
        }
        var placed = new HashSet<Release>(); foreach (var e in order) { Release r; if (!relOf.TryGetValue(e, out r)) projCardOrder.Add(e); else if (placed.Add(r)) projCardOrder.Add(r); }
        return rels;
    }
    void PaintReleaseCard(Graphics g, RectangleF card, Release r, int i)
    {
        List<ProjectEntry> ents; if (!projRelEnts.TryGetValue(r, out ents) || ents.Count == 0) return; bool hot = Hot(card); string kw = r.Kind == "album" ? "ALBUM" : "EP";
        FillRR(g, TM.C(215, 26, 14, 50), card, 14); Win.DrawNeonBox(g, card, hot); AnimRing(g, card, i % 2 == 0 ? NeonPink : NeonCyan);
        // the picture: the first song's, with the next two peeking out behind it like a stack of records
        float cw = card.Width; var pic = Rc(card.X + 8, card.Y + 8, cw - 16, cw - 16);
        for (int k = Math.Min(2, ents.Count - 1); k >= 1; k--) { var bk = Rc(pic.X + k * 6, pic.Y - k * 4, pic.Width - k * 12, pic.Height); FillRR(g, TM.C(200, 40 + k * 20, 20, 80), bk, 10); StrokeRR(g, Color.FromArgb(150, NeonViolet), bk, 10, 1f); }
        DrawCover(g, pic, ents[0]); Win.DrawNeonBox(g, pic, false);
        var pill = Rc(pic.X + 8, pic.Y + 8, 64, 20); FillRR(g, TM.C(240, 190, 50, 170), pill, 10); Txt(g, kw, 10, Color.White, pill, StringAlignment.Center, true);
        Txt(g, r.Title, 15, Ink, Rc(card.X + 12, card.Y + 228, cw - 24, 24), StringAlignment.Near, true);
        Txt(g, ents.Count + (ents.Count == 1 ? " SONG" : " SONGS") + (ents.Count < r.Pids.Count ? "  (" + (r.Pids.Count - ents.Count) + " NOT ON THIS PC)" : ""), 11, NeonCyan, Rc(card.X + 12, card.Y + 252, cw - 24, 18), StringAlignment.Near, true);
        string names = string.Join(", ", ents.ConvertAll(x => x.Name).ToArray()); TxtWrap(g, names, 10.5f, DimC, Rc(card.X + 12, card.Y + 272, cw - 24, 44));
        var ob = Rc(card.X + 12, card.Y + 324, cw - 24, 34); var rr = r; Btn(g, ob, "OPEN " + kw, 2, () => OpenProjEp(rr), 12); Hint2(ob, "See the " + ents.Count + " songs of this " + kw.ToLower() + ": play one, or click one to open it");
        bool qOn = ProjQueueOn(r); var pn = Rc(card.X + 12, card.Y + 366, cw - 24, 34); Btn(g, pn, qOn ? "STOP  (" + (projQueueAt + 1) + " OF " + projQueue.Count + ")" : "PLAY NOW", qOn ? 4 : 2, () => PlayProjEp(rr), 12); Hint2(pn, qOn ? "Stop playing the " + kw.ToLower() : "Play every song of the " + kw.ToLower() + " in order, one after another");
        Reg(card, () => OpenProjEp(rr)); Hint2(card, "Open the " + kw.ToLower() + " " + r.Title);
    }
    internal bool ProjQueueOn(Release r) { return projQueue != null && projQueueRel == r && projQueueAt < projQueue.Count && string.Equals(previewPath, projQueue[projQueueAt].Path, StringComparison.OrdinalIgnoreCase); }
    internal void PlayProjEp(Release r)
    {
        List<ProjectEntry> ents; if (r == null || !projRelEnts.TryGetValue(r, out ents) || ents.Count == 0) return;
        if (ProjQueueOn(r)) { projQueue = null; StopPreview(); return; }
        projQueue = new List<ProjectEntry>(ents); projQueueRel = r; projQueueAt = -1; StopPreview(); epQueue = null;
        if (ProjQueueNext(true)) Toast("Playing the " + (r.Kind == "album" ? "album" : "EP") + " \"" + r.Title + "\": " + projQueue.Count + " songs, one after another."); else projQueue = null;
    }
    // the next song of the EP / album that is playing (a song whose file is gone is skipped); false = it has ended or something else was played
    internal bool ProjQueueNext(bool first = false)
    {
        if (projQueue == null) return false;
        if (!first && (projQueueAt < 0 || projQueueAt >= projQueue.Count || !string.Equals(previewPath, projQueue[projQueueAt].Path, StringComparison.OrdinalIgnoreCase))) { projQueue = null; return false; }
        while (++projQueueAt < projQueue.Count) { var e = projQueue[projQueueAt]; if (e.Missing || !File.Exists(e.Path)) continue; StopPreview(); PreviewToggle(e); if (string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase)) return true; }
        projQueue = null; return false;
    }
    internal void OpenProjEp(Release r) { List<ProjectEntry> ents; if (r == null || !projRelEnts.TryGetValue(r, out ents)) return; projEp = r; projEpEnts = new List<ProjectEntry>(ents); projEpOpen = true; projEpScroll = 0; }
    void PaintProjEpSheet(Graphics g)
    {
        if (!projEpOpen || projEp == null) return; if (tab != 3) { projEpOpen = false; return; } float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); lastInput = DateTime.Now;
        Reg(all, () => projEpOpen = false); using (var dim = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.FillRectangle(dim, all);
        float bw = Math.Min(640, W - 40), bh = Math.Min(H - 60, 600); var box = Rc(W / 2 - bw / 2, H / 2 - bh / 2, bw, bh); FillRR(g, TM.C(252, 14, 8, 30), box, 22); Glow(g, NeonPink, box, 22, 0.9f, 4); StrokeRR(g, NeonPink, box, 22, 1.8f); Reg(box, () => { });
        string kw = projEp.Kind == "album" ? "ALBUM" : "EP"; var pill = Rc(box.X + 24, box.Y + 20, 70, 22); FillRR(g, TM.C(255, 190, 50, 170), pill, 11); Txt(g, kw, 11, Color.White, pill, StringAlignment.Center, true);
        Txt(g, projEp.Title, 22, Ink, Rc(box.X + 24, box.Y + 46, box.Width - 110, 34), StringAlignment.Near, true);
        Txt(g, "PLAY HEARS A SONG WITHOUT OPENING IT.  CLICK A SONG TO OPEN ITS PROJECT.", 11, DimC, Rc(box.X + 24, box.Y + 82, box.Width - 48, 18), StringAlignment.Near, true);
        var cl = Rc(box.Right - 52, box.Y + 18, 32, 30); Btn(g, cl, "x", 3, () => projEpOpen = false, 13);
        { bool qOn = ProjQueueOn(projEp); var pa = Rc(box.X + 24, box.Y + 108, box.Width - 48, 40); var rel = projEp; Btn(g, pa, qOn ? "PLAYING THE " + kw + "  (" + (projQueueAt + 1) + " OF " + projQueue.Count + ")  -  STOP" : "PLAY " + kw, qOn ? 4 : 2, () => PlayProjEp(rel), 15); Hint2(pa, qOn ? "Stop playing the " + kw.ToLower() : "Play every song of the " + kw.ToLower() + " in order, one after another"); }
        var lr = Rc(box.X + 20, box.Y + 160, box.Width - 40, box.Bottom - box.Y - 178); float rowH = 56, total = projEpEnts.Count * rowH, maxS = Math.Max(0, total - lr.Height); projEpScroll = Math.Max(0, Math.Min(maxS, projEpScroll));
        regs.Add(new HitReg { R = lr, OnWheel = d => projEpScroll = Math.Max(0, Math.Min(maxS, projEpScroll - d * 50)) });
        var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
        for (int i = 0; i < projEpEnts.Count; i++)
        {
            var e = projEpEnts[i]; var row = Rc(lr.X, lr.Y + i * rowH - projEpScroll, lr.Width - (maxS > 0 ? 50 : 0), rowH - 8); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue;
            bool prev = string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase), hot = Hot(row);
            if (prev) PaintPickedFx(g, row, 10); else { FillRR(g, hot ? TM.C(220, 70, 36, 130) : TM.C(170, 30, 16, 58), row, 10); StrokeRR(g, Color.FromArgb(hot ? 220 : 110, NeonViolet), row, 10, 1.2f); }
            var ee = e; Reg(row, () => { projEpOpen = false; OpenProjectAsk(ee); }); Hint2(row, "Open the project " + e.Name + " (it asks first)");
            { var tp = Rc(row.X + 10, row.Y + row.Height / 2 - 11, 74, 22); FillRR(g, NeonCyan, tp, 11); Txt(g, "TRACK " + (i + 1), 10, Color.Black, tp, StringAlignment.Center, true); }   // which track of the release it is
            var pic = Rc(row.X + 92, row.Y + 5, row.Height - 10, row.Height - 10); DrawCover(g, pic, e);
            Txt(g, e.Name, 14, Ink, Rc(pic.Right + 10, row.Y, row.Width - (pic.Right + 10 - row.X) - 120 - 62, row.Height), StringAlignment.Near, true);
            { int fi = i; var ub = Rc(row.Right - 108 - 62, row.Y + 8, 28, row.Height - 16); var dbn = Rc(ub.Right + 4, row.Y + 8, 28, row.Height - 16); Btn(g, ub, " ", 1, () => MoveInRelease(fi, -1), 11); ArrowIcon(g, ub, true); Hint2(ub, "Move " + e.Name + " earlier in the " + kw.ToLower()); Btn(g, dbn, "  ", 1, () => MoveInRelease(fi, 1), 11); ArrowIcon(g, dbn, false); Hint2(dbn, "Move " + e.Name + " later in the " + kw.ToLower()); }
            var pb = Rc(row.Right - 108, row.Y + 8, 96, row.Height - 16); Btn(g, pb, prev ? "STOP" : "PLAY", prev ? 4 : 1, () => PreviewToggle(ee), 12); Hint2(pb, prev ? "Stop the preview" : "Hear " + e.Name + " without opening it");
        }
        g.Restore(gs); if (maxS > 0) VScroll(g, Rc(lr.Right - 44, lr.Y, 42, lr.Height), total, lr.Height, () => projEpScroll, v => projEpScroll = v);
    }
}

partial class StudioForm
{
    // the order of an EP or album: a song moves one place earlier (-1) or later (+1); the profile gets the new order straight away
    internal void MoveInRelease(int i, int d)
    {
        if (projEp == null) return; int j = i + d; if (i < 0 || j < 0 || i >= projEpEnts.Count || j >= projEpEnts.Count) return;
        var t = projEpEnts[i]; projEpEnts[i] = projEpEnts[j]; projEpEnts[j] = t;
        string pa = CommunitySongs.PidOf(projEpEnts[i].Path), pb = CommunitySongs.PidOf(projEpEnts[j].Path); int ia = projEp.Pids.IndexOf(pa), ib = projEp.Pids.IndexOf(pb);
        if (ia >= 0 && ib >= 0) { projEp.Pids[ia] = pb; projEp.Pids[ib] = pa; }   // (songs that are not on this PC keep their places)
        ReleaseStore.Save(); List<ProjectEntry> ents; if (projRelEnts.TryGetValue(projEp, out ents)) { ents.Clear(); ents.AddRange(projEpEnts); }
        if (ProfileStore.Published) QueueRepublish();
        Toast(projEpEnts[j].Name + " is now track " + (j + 1) + " of " + projEp.Title + ".");
    }
}
