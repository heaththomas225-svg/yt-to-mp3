using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// EPs and albums on My Projects: the projects of a release are not separate cards, they are ONE card (EP or ALBUM). Opening it lists its projects in order:
// PLAY hears one without opening it, clicking a project asks to open it. (While you search, the projects show one by one so you can find them.)
partial class StudioForm
{
    internal bool projEpOpen; Release projEp; List<ProjectEntry> projEpEnts = new List<ProjectEntry>(); float projEpScroll;
    readonly Dictionary<Release, List<ProjectEntry>> projRelEnts = new Dictionary<Release, List<ProjectEntry>>();

    // takes the projects of each EP / album out of the list and returns the releases to show as cards (in the order they were made)
    internal List<Release> GroupProjectReleases(List<ProjectEntry> list)
    {
        projRelEnts.Clear(); var rels = new List<Release>(); if (projSearch.Trim().Length > 0) return rels; ReleaseStore.Load();
        var byPid = new Dictionary<string, ProjectEntry>(); foreach (var e in list) { string pid = CommunitySongs.PidOf(e.Path); if (!byPid.ContainsKey(pid)) byPid[pid] = e; }
        foreach (var r in ReleaseStore.All.ToArray())
        {
            var ents = new List<ProjectEntry>(); foreach (var p in r.Pids) { ProjectEntry e; if (byPid.TryGetValue(p, out e) && !ents.Contains(e)) ents.Add(e); }
            if (ents.Count == 0) continue; rels.Add(r); projRelEnts[r] = ents; foreach (var e in ents) list.Remove(e);
        }
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
        Reg(card, () => OpenProjEp(rr)); Hint2(card, "Open the " + kw.ToLower() + " " + r.Title);
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
        var lr = Rc(box.X + 20, box.Y + 112, box.Width - 40, box.Bottom - box.Y - 130); float rowH = 56, total = projEpEnts.Count * rowH, maxS = Math.Max(0, total - lr.Height); projEpScroll = Math.Max(0, Math.Min(maxS, projEpScroll));
        regs.Add(new HitReg { R = lr, OnWheel = d => projEpScroll = Math.Max(0, Math.Min(maxS, projEpScroll - d * 50)) });
        var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
        for (int i = 0; i < projEpEnts.Count; i++)
        {
            var e = projEpEnts[i]; var row = Rc(lr.X, lr.Y + i * rowH - projEpScroll, lr.Width - (maxS > 0 ? 50 : 0), rowH - 8); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue;
            bool prev = string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase), hot = Hot(row);
            if (prev) PaintPickedFx(g, row, 10); else { FillRR(g, hot ? TM.C(220, 70, 36, 130) : TM.C(170, 30, 16, 58), row, 10); StrokeRR(g, Color.FromArgb(hot ? 220 : 110, NeonViolet), row, 10, 1.2f); }
            var ee = e; Reg(row, () => { projEpOpen = false; OpenProjectAsk(ee); }); Hint2(row, "Open the project " + e.Name + " (it asks first)");
            Txt(g, (i + 1).ToString(), 15, NeonCyan, Rc(row.X + 12, row.Y, 30, row.Height), StringAlignment.Near, true);
            var pic = Rc(row.X + 44, row.Y + 5, row.Height - 10, row.Height - 10); DrawCover(g, pic, e);
            Txt(g, e.Name, 14, Ink, Rc(pic.Right + 10, row.Y, row.Width - (pic.Right + 10 - row.X) - 120, row.Height), StringAlignment.Near, true);
            var pb = Rc(row.Right - 108, row.Y + 8, 96, row.Height - 16); Btn(g, pb, prev ? "STOP" : "PLAY", prev ? 4 : 1, () => PreviewToggle(ee), 12); Hint2(pb, prev ? "Stop the preview" : "Hear " + e.Name + " without opening it");
        }
        g.Restore(gs); if (maxS > 0) VScroll(g, Rc(lr.Right - 44, lr.Y, 42, lr.Height), total, lr.Height, () => projEpScroll, v => projEpScroll = v);
    }
}
