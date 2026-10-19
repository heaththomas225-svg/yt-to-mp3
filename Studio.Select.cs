using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Select several clips at once: hold the left mouse button on empty space and drag a box, and every clip it touches lights up.
// Drag any lit clip to move them all together, press Delete to remove them, Esc to let go.
// (The needle only moves from the ruler at the top, so a click on empty space is free for this.)
partial class StudioForm
{
    readonly List<Track> multiSel = new List<Track>(); bool marqueeOn, marqueeMoved; PointF marqueeA, marqueeB; int undoHold;
    readonly Dictionary<Track, long> groupOrig = new Dictionary<Track, long>();
    bool InGroup(Track t) { return t != null && multiSel.Count > 1 && multiSel.Contains(t); }

    void MarqueeBegin() { multiSel.Clear(); marqueeOn = true; marqueeMoved = false; marqueeA = marqueeB = MousePos; }
    void MarqueeTo(PointF p)
    {
        if (!marqueeOn) return; marqueeB = p;
        if (!marqueeMoved && (Math.Abs(p.X - marqueeA.X) > 4 || Math.Abs(p.Y - marqueeA.Y) > 4)) marqueeMoved = true;
        if (marqueeMoved) RebuildMarquee();
    }
    void MarqueeEnd()
    {
        if (!marqueeOn) return; marqueeOn = false;
        if (marqueeMoved)
        {
            RebuildMarquee();
            Toast(multiSel.Count == 0 ? "No clips in that box." : multiSel.Count + (multiSel.Count == 1 ? " clip" : " clips") + " selected. Drag one to move them all, press Delete to remove them, Esc to let go.");
        }
        marqueeMoved = false;
    }
    void RebuildMarquee()
    {
        var box = RectangleF.FromLTRB(Math.Min(marqueeA.X, marqueeB.X), Math.Min(marqueeA.Y, marqueeB.Y), Math.Max(marqueeA.X, marqueeB.X), Math.Max(marqueeA.Y, marqueeB.Y));
        multiSel.Clear(); float top = ArrTop + RulerH - vscroll;
        foreach (var t in eng.Tracks.ToArray())
        {
            if (t.IsBus || t.Clip == null || t.Instrument) continue; int row = eng.Tracks.IndexOf(t.Host ?? t); if (row < 0 || row >= LaneCount) continue;
            float ly = top + row * LaneH, x0 = HeadW + (float)((t.Offset - scroll) / spp), x1 = x0 + (float)(t.UsedLength / spp);
            if (box.IntersectsWith(RectangleF.FromLTRB(x0, ly + 4, Math.Max(x1, x0 + 2), ly + LaneH - 4))) multiSel.Add(t);
        }
    }
    void PaintMarquee(Graphics g)
    {
        multiSel.RemoveAll(t => !eng.Tracks.Contains(t)); if (!marqueeOn || !marqueeMoved) return;
        var tl = TimelineRect(); var box = RectangleF.Intersect(RectangleF.FromLTRB(Math.Min(marqueeA.X, marqueeB.X), Math.Min(marqueeA.Y, marqueeB.Y), Math.Max(marqueeA.X, marqueeB.X), Math.Max(marqueeA.Y, marqueeB.Y)), Rc(tl.X, ArrTop + RulerH, tl.Width, ClientSize.Height - ToolH - TransH - RulerH));
        if (box.IsEmpty) return; lastInput = DateTime.Now;
        using (var b = new SolidBrush(TM.C(46, 0, 229, 255))) g.FillRectangle(b, box); using (var pen = new Pen(TM.C(230, 0, 229, 255), 1.6f)) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
    }
    // the glow on a selected clip
    void PaintSelectedClip(Graphics g, RectangleF cr)
    {
        using (var b = new SolidBrush(Color.FromArgb(48, 255, 255, 255))) g.FillRectangle(b, cr);
        using (var pen = new Pen(TM.C(240, 0, 229, 255), 2.4f)) g.DrawRectangle(pen, cr.X + 1, cr.Y + 1, Math.Max(1, cr.Width - 2), Math.Max(1, cr.Height - 2));
    }
    // Delete with several clips selected: one Ctrl+Z brings them all back; locked tracks are left alone
    void DeleteSelectedClips()
    {
        var list = new List<Track>(); foreach (var t in multiSel) if (eng.Tracks.Contains(t) && !IsLocked(t)) list.Add(t); int skipped = multiSel.Count - list.Count;
        if (list.Count == 0) { Toast(skipped > 0 ? "Those tracks are locked. Press UNLOCK first." : "Nothing is selected."); multiSel.Clear(); return; }
        PushUndo(); undoHold++;
        try { list.Sort((a, b) => (b.Host != null ? 1 : 0).CompareTo(a.Host != null ? 1 : 0)); foreach (var t in list) if (eng.Tracks.Contains(t)) DeleteClip(t); }
        finally { undoHold--; }
        multiSel.Clear(); Toast(list.Count + (list.Count == 1 ? " clip" : " clips") + " deleted" + (skipped > 0 ? " (" + skipped + " locked left as they were)" : "") + ". Ctrl+Z brings them back.");
    }
}
