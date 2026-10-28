using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  MIDI CLIPS: the notes of a note track sit in clips on the song, like audio does. Notes always land in a clip: recorded, written in the
//  piano roll, generated or from an older project, a clip is made around them (whole bars; a bar or more of silence starts a new clip).
//  Drag a clip to move the whole melody (onto another note track too), its bottom-right corner to trim or extend it, its left edge to
//  trim its start, its top-right loop corner to repeat it. Right-click: duplicate, split at the needle, merge with the selected clips,
//  rename, open in the piano roll, delete. Shift+click selects several; Ctrl+D duplicates, Delete deletes. Every change is one undo step.
//  The notes stay in Track.Midi (the engine plays them as before): a clip is the part of the song its notes belong to.
// =====================================================================================================
class MidiClip { public long Start, Len; public string Name = ""; public MidiClip Copy() { return (MidiClip)MemberwiseClone(); } }

partial class StudioForm
{
    internal readonly HashSet<MidiClip> mcSel = new HashSet<MidiClip>(); readonly Dictionary<Track, long> mcStamp = new Dictionary<Track, long>(); readonly Dictionary<Track, RectangleF> mcLanes = new Dictionary<Track, RectangleF>();
    MidiClip mcDrag; Track mcDragTrack; int mcMode; long mcGrab, mcStart0, mcLen0; List<MidiEv> mcEvs; long[] mcFrames0; List<MidiEv> mcLoopSrc; bool mcMoved; PointF mcDown;
    double McBar { get { return PrBeatFrames * 4; } }

    // every note-on lies in a clip: the notes outside make new clips (or grow the clip they touch); a clip left with no notes goes
    internal void McSync(Track t)
    {
        if (t == null || !t.Instrument) return; MidiEv[] evs; lock (t.Midi) evs = t.Midi.ToArray(); long stamp = evs.Length; foreach (var e in evs) stamp = stamp * 31 + e.Frame + e.D1; foreach (var c in t.MClips) stamp = stamp * 17 + c.Start * 3 + c.Len;
        long old; if (mcStamp.TryGetValue(t, out old) && old == stamp) return;
        double bar = McBar; var ons = evs.Where(e => (e.S & 0xF0) == 0x90 && e.D2 > 0).ToList();
        t.MClips.RemoveAll(c => c.Len <= 0 || !ons.Any(e => e.Frame >= c.Start && e.Frame < c.Start + c.Len));
        var loose = ons.Where(e => !t.MClips.Any(c => e.Frame >= c.Start && e.Frame < c.Start + c.Len)).Select(e => e.Frame).OrderBy(f => f).ToList();
        if (loose.Count > 0)
        {
            var groups = new List<long[]>(); foreach (var f in loose) { long b0 = (long)(Math.Floor(f / bar) * bar), b1 = (long)(Math.Floor(f / bar) * bar + bar); if (groups.Count > 0 && b0 <= groups[groups.Count - 1][1] + 1) groups[groups.Count - 1][1] = Math.Max(groups[groups.Count - 1][1], b1); else groups.Add(new[] { b0, b1 }); }
            foreach (var gp in groups)
            {
                // a note just past a clip (recording on) grows that clip; otherwise a clip of its own (kept off the clips beside it)
                var touch = t.MClips.FirstOrDefault(c => gp[0] <= c.Start + c.Len + 1 && gp[1] >= c.Start - 1);
                if (touch != null) { long s = Math.Min(touch.Start, gp[0]), e2 = Math.Max(touch.Start + touch.Len, gp[1]); touch.Start = s; touch.Len = e2 - s; }
                else t.MClips.Add(new MidiClip { Start = gp[0], Len = gp[1] - gp[0], Name = "" });
            }
            t.MClips.Sort((a, b) => a.Start.CompareTo(b.Start));
        }
        stamp = evs.Length; foreach (var e in evs) stamp = stamp * 31 + e.Frame + e.D1; foreach (var c in t.MClips) stamp = stamp * 17 + c.Start * 3 + c.Len; mcStamp[t] = stamp;
    }
    // the events of a clip: its note-ons, each one's note-off (wherever it falls) and the other events inside it
    List<MidiEv> McEvents(Track t, MidiClip c)
    {
        var r = new List<MidiEv>(); lock (t.Midi)
        {
            var used = new HashSet<MidiEv>(); long a = c.Start, b = c.Start + c.Len;
            for (int i = 0; i < t.Midi.Count; i++)
            {
                var e = t.Midi[i]; int k = e.S & 0xF0; bool on = k == 0x90 && e.D2 > 0, off = k == 0x80 || (k == 0x90 && e.D2 == 0);
                if (on && e.Frame >= a && e.Frame < b) { r.Add(e); used.Add(e); for (int j = i + 1; j < t.Midi.Count; j++) { var o = t.Midi[j]; int kk = o.S & 0xF0; if ((kk == 0x80 || (kk == 0x90 && o.D2 == 0)) && o.D1 == e.D1 && !used.Contains(o)) { r.Add(o); used.Add(o); break; } } }
                else if (!on && !off && e.Frame >= a && e.Frame < b) r.Add(e);
            }
        }
        return r;
    }
    static MidiEv EvCopy(MidiEv e, long shift) { return new MidiEv { Frame = e.Frame + shift, S = e.S, D1 = e.D1, D2 = e.D2 }; }
    void McSort(Track t) { lock (t.Midi) t.Midi.Sort((a, b) => a.Frame != b.Frame ? a.Frame.CompareTo(b.Frame) : ((a.S & 0xF0) == 0x80 ? -1 : 1).CompareTo((b.S & 0xF0) == 0x80 ? -1 : 1)); mcStamp.Remove(t); }
    long McSnap(long f) { long s = Snap(f); return s; }

    // ---- drawing: the clips on a note track's row ----
    void PaintMidiClips(Graphics g, Track tr, RectangleF lane)
    {
        McSync(tr); if (tr.MClips.Count == 0) return; var col = Neon(tr.Color); MidiEv[] evs; lock (tr.Midi) evs = tr.Midi.ToArray();
        foreach (var c in tr.MClips)
        {
            float x0 = lane.X + (float)((c.Start - scroll) / spp), x1 = lane.X + (float)((c.Start + c.Len - scroll) / spp); if (x1 < lane.X || x0 > lane.Right) continue;
            var r = Rc(x0, lane.Y + 3, Math.Max(6, x1 - x0), lane.Height - 6); bool sel = mcSel.Contains(c), hot = Hot(r);
            FillRR(g, Color.FromArgb(sel ? 120 : 78, col), r, 6); StrokeRR(g, sel ? Color.White : Color.FromArgb(hot ? 255 : 200, col), r, 6, sel ? 2f : 1.3f);
            using (var hb = new SolidBrush(Color.FromArgb(sel ? 200 : 150, col))) g.FillRectangle(hb, r.X + 1, r.Y + 1, r.Width - 2, Math.Min(15, r.Height / 4));
            if (r.Width > 40) Txt(g, c.Name.Length > 0 ? c.Name : tr.Name, 9, Color.White, Rc(r.X + 5, r.Y, r.Width - 30, 15), StringAlignment.Near, true);
            // the notes inside (height = pitch)
            int lo = 127, hi = 0; var open = new Dictionary<int, long>(); var bars = new List<long[]>();
            foreach (var m in evs) { if (m.Frame < c.Start - StudioEngine.SR * 8 || m.Frame > c.Start + c.Len + StudioEngine.SR * 8) continue; int k = m.S & 0xF0; if (k == 0x90 && m.D2 > 0) { if (m.Frame >= c.Start && m.Frame < c.Start + c.Len) { open[m.D1] = m.Frame; lo = Math.Min(lo, m.D1); hi = Math.Max(hi, m.D1); } } else if ((k == 0x80 || k == 0x90) && open.ContainsKey(m.D1)) { bars.Add(new[] { open[m.D1], Math.Min(m.Frame, c.Start + c.Len), m.D1 }); open.Remove(m.D1); } }
            foreach (var kv in open) bars.Add(new[] { kv.Value, Math.Min(kv.Value + StudioEngine.SR / 4, c.Start + c.Len), kv.Key });
            float top = r.Y + Math.Min(16, r.Height / 4) + 2, hgt = r.Bottom - 4 - top; float range = Math.Max(8, hi - lo + 1); var gs = g.Save(); g.SetClip(r, System.Drawing.Drawing2D.CombineMode.Intersect);
            using (var nb = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) foreach (var b in bars) { float bx0 = lane.X + (float)((b[0] - scroll) / spp), bx1 = lane.X + (float)((b[1] - scroll) / spp); float ny = top + hgt - (b[2] - lo + 1 + (range - (hi - lo + 1)) / 2) / range * hgt;   /* a few pitches (drums) sit in the middle */ g.FillRectangle(nb, bx0, ny, Math.Max(2, bx1 - bx0 - 1), Math.Max(2, Math.Min(5, hgt / range))); }
            g.Restore(gs);
            // the handles: loop (top right), trim (bottom right)
            if (r.Width > 30 && (hot || sel)) { using (var lb = new SolidBrush(Color.FromArgb(230, NeonCyan))) g.FillEllipse(lb, r.Right - 13, r.Y + 2, 11, 11); using (var p = new Pen(Color.FromArgb(20, 10, 40), 1.4f)) g.DrawArc(p, r.Right - 11, r.Y + 4, 7, 7, 30, 280); using (var tb = new SolidBrush(Color.FromArgb(230, Color.White))) g.FillPolygon(tb, new[] { new PointF(r.Right - 2, r.Bottom - 12), new PointF(r.Right - 2, r.Bottom - 2), new PointF(r.Right - 12, r.Bottom - 2) }); }
        }
    }
    // the clicks on the clips (added after the row's own region, so they come first)
    void RegMidiClips(Track tr, RectangleF lane)
    {
        if (!tr.Instrument) return; mcLanes[tr] = lane;
        foreach (var c0 in tr.MClips.ToList())
        {
            var c = c0; float x0 = lane.X + (float)((c.Start - scroll) / spp), x1 = lane.X + (float)((c.Start + c.Len - scroll) / spp); if (x1 < lane.X || x0 > lane.Right) continue;
            var r = Rc(x0, lane.Y + 3, Math.Max(6, x1 - x0), lane.Height - 6);
            var reg = Reg(r, () => McDown(tr, c, r)); reg.OnDrag = pt => McDragTo(pt); reg.OnUp = () => McUp(); reg.OnRight = () => McMenu(tr, c); reg.OnDouble = () => { OpenPianoRoll(tr); prScrollBeats = Math.Max(0, Math.Floor(c.Start / PrBeatFrames / 4) * 4); };
            if (Hot(r)) hintNow = "MIDI clip: drag to move the whole melody (onto another note track too)  •  bottom-right corner: trim or extend  •  top-right circle: loop it  •  right-click: duplicate, split, merge  •  double-click: piano roll";
        }
    }
    void McDown(Track t, MidiClip c, RectangleF r)
    {
        var p = MousePos; mcDown = p; bool shift = (ModifierKeys & Keys.Shift) != 0; if (shift) { if (!mcSel.Remove(c)) mcSel.Add(c); } else if (!mcSel.Contains(c)) { mcSel.Clear(); mcSel.Add(c); } multiSel.Clear();
        mcDrag = c; mcDragTrack = t; mcMoved = false; mcStart0 = c.Start; mcLen0 = c.Len; mcGrab = (long)((p.X - r.X) * spp);
        mcMode = r.Width > 30 && p.X > r.Right - 15 && p.Y < r.Y + 15 ? 4 : (r.Width > 30 && p.X > r.Right - 12 && p.Y > r.Bottom - 14 ? 2 : (r.Width > 30 && p.X < r.X + 7 ? 3 : 1));
        mcEvs = McEvents(t, c); mcFrames0 = mcEvs.Select(e => e.Frame).ToArray(); mcLoopSrc = mcMode == 4 ? mcEvs.Select(e => EvCopy(e, 0)).ToList() : null;
        PushUndo();
    }
    void McDragTo(PointF pt)
    {
        var c = mcDrag; var t = mcDragTrack; if (c == null || t == null) return; if (!mcMoved && Math.Abs(pt.X - mcDown.X) + Math.Abs(pt.Y - mcDown.Y) < 4) return; mcMoved = true;
        long at = scroll + (long)((pt.X - HeadW) * spp);
        if (mcMode == 1)
        {
            long ns = Math.Max(0, McSnap(at - mcGrab)); long d = ns - mcStart0; c.Start = ns;
            lock (t.Midi) for (int i = 0; i < mcEvs.Count; i++) mcEvs[i].Frame = Math.Max(0, mcFrames0[i] + d);
            McSort(t);
        }
        else if (mcMode == 2 || mcMode == 4) { long ne = Math.Max(c.Start + (long)PrBeatFrames, McSnap(at)); c.Len = ne - c.Start; }
        else if (mcMode == 3) { long ns = Math.Max(0, Math.Min(mcStart0 + mcLen0 - (long)PrBeatFrames, McSnap(at))); c.Start = ns; c.Len = mcStart0 + mcLen0 - ns; }
        Invalidate();
    }
    void McUp()
    {
        var c = mcDrag; var t = mcDragTrack; mcDrag = null; if (c == null || t == null || !mcMoved) { mcDragTrack = null; return; }
        long end = c.Start + c.Len;
        if (mcMode == 1)
        {   // dropped on another note track: the clip and its notes go there
            var dest = mcLanes.Where(kv => kv.Key != t && kv.Key.Instrument && eng.Tracks.Contains(kv.Key) && kv.Value.Top <= MousePos.Y && kv.Value.Bottom > MousePos.Y).Select(kv => kv.Key).FirstOrDefault();
            if (dest != null && !IsLocked(dest))
            {
                lock (t.Midi) foreach (var e in mcEvs) t.Midi.Remove(e); lock (dest.Midi) dest.Midi.AddRange(mcEvs); t.MClips.Remove(c); dest.MClips.Add(c); McSort(t); McSort(dest);
                Toast("The clip moved to " + dest.Name + " (it plays " + dest.Name + "'s sound now). Ctrl+Z puts it back.");
            }
        }
        else if (mcMode == 2) McCut(t, c.Start, end, mcStart0, mcStart0 + mcLen0);
        else if (mcMode == 3) McCut(t, c.Start, end, mcStart0, mcStart0 + mcLen0);
        else if (mcMode == 4)
        {   // loop: the clip's first length repeats to the new end
            long per = mcLen0; var add = new List<MidiEv>();
            if (end > mcStart0 + per) for (long k = 1; mcStart0 + k * per < end; k++) foreach (var e in mcLoopSrc) { long f = e.Frame + k * per; bool on = (e.S & 0xF0) == 0x90 && e.D2 > 0; if (on && f >= end) continue; add.Add(EvCopy(e, k * per)); if (!on && f > end) add[add.Count - 1].Frame = end; }
            McCut(t, c.Start, end, mcStart0, mcStart0 + mcLen0); lock (t.Midi) t.Midi.AddRange(add); McSort(t);
            if (end > mcStart0 + per) Toast("Looped " + Math.Round((end - mcStart0) / (double)per, 1) + " times.");
        }
        mcDragTrack = null; mcStamp.Remove(t); Invalidate();
    }
    // the notes outside a clip's new edges go (notes that run past its end are shortened to it)
    void McCut(Track t, long a, long b, long a0, long b0)
    {
        var evs = McEvents(t, new MidiClip { Start = Math.Min(a, a0), Len = Math.Max(b, b0) - Math.Min(a, a0) }); var drop = new HashSet<MidiEv>(); var open = new Dictionary<int, MidiEv>();
        foreach (var e in evs.OrderBy(x => x.Frame)) { int k = e.S & 0xF0; bool on = k == 0x90 && e.D2 > 0; if (on) { if (e.Frame < a || e.Frame >= b) { drop.Add(e); } open[e.D1] = e; } else if (k == 0x80 || k == 0x90) { MidiEv o; if (open.TryGetValue(e.D1, out o)) { if (drop.Contains(o)) drop.Add(e); else if (e.Frame > b) e.Frame = b; open.Remove(e.D1); } } }
        if (drop.Count > 0) lock (t.Midi) t.Midi.RemoveAll(x => drop.Contains(x)); McSort(t);
    }
    // ---- right-click ----
    void McMenu(Track t, MidiClip c)
    {
        if (!mcSel.Contains(c)) { mcSel.Clear(); mcSel.Add(c); } var m = Win.NewMenu();
        m.Items.Add(new ToolStripMenuItem("Duplicate (Ctrl+D)", null, (a, b) => McDuplicate()));
        long nd = DispPos; bool can = nd > c.Start && nd < c.Start + c.Len; m.Items.Add(new ToolStripMenuItem("Split at the needle", null, (a, b) => McSplit(t, c, Snap(nd))) { Enabled = can });
        var same = mcSel.Where(x => t.MClips.Contains(x)).ToList(); m.Items.Add(new ToolStripMenuItem("Merge the selected clips into one (" + same.Count + ")", null, (a, b) => McMerge(t, same)) { Enabled = same.Count > 1 });
        m.Items.Add(new ToolStripMenuItem("Rename...", null, (a, b) => { string nm = StudioDialogs.Ask(this, "Name of this clip", c.Name.Length > 0 ? c.Name : t.Name); if (nm != null) { PushUndo(); c.Name = nm.Trim(); } }));
        m.Items.Add(new ToolStripMenuItem("Open in the piano roll", null, (a, b) => { OpenPianoRoll(t); prScrollBeats = Math.Max(0, Math.Floor(c.Start / PrBeatFrames / 4) * 4); }));
        m.Items.Add(new ToolStripSeparator()); m.Items.Add(new ToolStripMenuItem("Delete (Delete key)", null, (a, b) => McDelete()));
        m.Show(this, Point.Round(MousePos));
    }
    Track McTrackOf(MidiClip c) { return eng.Tracks.FirstOrDefault(t => t.MClips.Contains(c)); }
    internal void McDuplicate()
    {
        var list = mcSel.Select(c => new KeyValuePair<Track, MidiClip>(McTrackOf(c), c)).Where(kv => kv.Key != null).ToList(); if (list.Count == 0) return; PushUndo(); var made = new List<MidiClip>();
        foreach (var kv in list) { var t = kv.Key; var c = kv.Value; var evs = McEvents(t, c).Select(e => EvCopy(e, c.Len)).ToList(); lock (t.Midi) t.Midi.AddRange(evs); var nc = new MidiClip { Start = c.Start + c.Len, Len = c.Len, Name = c.Name }; t.MClips.Add(nc); t.MClips.Sort((a, b) => a.Start.CompareTo(b.Start)); McSort(t); made.Add(nc); }
        mcSel.Clear(); foreach (var c in made) mcSel.Add(c); Toast(made.Count == 1 ? "Clip duplicated right after itself." : made.Count + " clips duplicated.");
    }
    internal void McSplit(Track t, MidiClip c, long at)
    {
        if (at <= c.Start || at >= c.Start + c.Len) { Toast("Put the needle inside the clip to split it there."); return; }
        PushUndo(); var right = new MidiClip { Start = at, Len = c.Start + c.Len - at, Name = c.Name }; c.Len = at - c.Start; t.MClips.Add(right); t.MClips.Sort((a, b) => a.Start.CompareTo(b.Start)); mcStamp.Remove(t); mcSel.Clear(); mcSel.Add(right); Toast("Clip split in two at the needle.");
    }
    internal void McMerge(Track t, List<MidiClip> cl)
    {
        if (cl.Count < 2) return; PushUndo(); long a = cl.Min(c => c.Start), b = cl.Max(c => c.Start + c.Len); var keep = cl.OrderBy(c => c.Start).First(); foreach (var c in cl) if (c != keep) t.MClips.Remove(c); keep.Start = a; keep.Len = b - a;
        t.MClips.RemoveAll(c => c != keep && c.Start >= a && c.Start + c.Len <= b); mcStamp.Remove(t); mcSel.Clear(); mcSel.Add(keep); Toast(cl.Count + " clips merged into one.");
    }
    internal void McDelete()
    {
        var list = mcSel.Select(c => new KeyValuePair<Track, MidiClip>(McTrackOf(c), c)).Where(kv => kv.Key != null).ToList(); if (list.Count == 0) return; PushUndo();
        foreach (var kv in list) { var evs = new HashSet<MidiEv>(McEvents(kv.Key, kv.Value)); lock (kv.Key.Midi) kv.Key.Midi.RemoveAll(e => evs.Contains(e)); kv.Key.MClips.Remove(kv.Value); mcStamp.Remove(kv.Key); }
        mcSel.Clear(); Toast(list.Count == 1 ? "Clip deleted (Ctrl+Z brings it back)." : list.Count + " clips deleted.");
    }
    internal bool McKey(Keys k)
    {
        if (mcSel.Count == 0 || prFocus || BlShown && blFocus) return false; Keys code = k & Keys.KeyCode; bool ctrl = (k & Keys.Control) != 0;
        mcSel.RemoveWhere(c => McTrackOf(c) == null); if (mcSel.Count == 0) return false;
        if (code == Keys.Delete || code == Keys.Back) { McDelete(); return true; }
        if (ctrl && code == Keys.D) { McDuplicate(); return true; }
        if (code == Keys.Escape) { mcSel.Clear(); return true; }
        return false;
    }
}
