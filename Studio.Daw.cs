using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// Purphex Music: the editing layer that makes the studio a DAW: split, trim, move, duplicate, undo / redo, tempo + grid snapping, metronome.
static class Tempo
{
    // Finds the tempo (BPM) of a clip from the rhythm of its loudness changes. Returns 0 when it can't tell.
    public static double Detect(AudioClip clip, long startFrame, double maxSeconds)
    {
        if (clip == null || clip.L == null) return 0; int sr = StudioEngine.SR, hop = 256; long len = clip.Length;
        long from = Math.Max(0, Math.Min(startFrame, len)), to = Math.Min(len, from + (long)(maxSeconds * sr)); if (to - from < sr * 4) { from = 0; to = Math.Min(len, (long)(maxSeconds * sr)); }
        int n = (int)((to - from) / hop); if (n < 400) return 0;
        var e = new double[n]; for (int i = 0; i < n; i++) { double s = 0; long b = from + (long)i * hop; for (int k = 0; k < hop; k++) { double v = (clip.L[b + k] + clip.R[b + k]) * 0.5; s += v * v; } e[i] = Math.Sqrt(s / hop); }
        var on = new double[n]; double mean = 0; for (int i = 1; i < n; i++) { on[i] = Math.Max(0, e[i] - e[i - 1]); mean += on[i]; } mean /= n; for (int i = 0; i < n; i++) on[i] = Math.Max(0, on[i] - mean * 0.5);
        double hopSec = hop / (double)sr; int lagMin = (int)(60.0 / 190 / hopSec), lagMax = (int)(60.0 / 55 / hopSec); double best = 0; int bestLag = 0; var acf = new double[lagMax + 2];
        for (int lag = lagMin; lag <= lagMax; lag++) { double a = 0; for (int i = lag; i < n; i++) a += on[i] * on[i - lag]; a /= (n - lag); acf[lag] = a; }
        for (int lag = lagMin; lag <= lagMax; lag++)
        {
            double a = acf[lag] + 0.5 * (2 * lag <= lagMax ? acf[2 * lag] : 0);   // a beat is also supported by the bar that is twice as long
            double bpm = 60.0 / (lag * hopSec); double w = bpm >= 75 && bpm <= 160 ? 1.0 : 0.85; a *= w;
            if (a > best) { best = a; bestLag = lag; }
        }
        if (bestLag == 0 || best <= 0) return 0;
        // refine around the peak with a parabola
        double l0 = acf[Math.Max(lagMin, bestLag - 1)], l1 = acf[bestLag], l2 = acf[Math.Min(lagMax, bestLag + 1)], den = l0 - 2 * l1 + l2, off = Math.Abs(den) < 1e-12 ? 0 : 0.5 * (l0 - l2) / den; off = Math.Max(-1, Math.Min(1, off));
        double bp = 60.0 / ((bestLag + off) * hopSec); while (bp < 70) bp *= 2; while (bp > 170) bp /= 2; return Math.Round(bp * 10) / 10.0;
    }
}

partial class StudioForm
{
    // ---------- state ----------
    bool snapOn = true; int gridIdx = 2; static readonly string[] GridNames = { "1/1 bar", "1/2", "1/4 beat", "1/8", "1/16" }; static readonly double[] GridBeats = { 4, 2, 1, 0.5, 0.25 };
    readonly List<Fx> retired = new List<Fx>();   // effects of deleted tracks stay alive so Undo can bring the track back
    class TS { public long Offset, ClipStart, ClipFrames; public float Vol, Pan; public bool Mute, Solo; public string Name; }
    class Snapshot { public List<Track> Order = new List<Track>(); public Dictionary<Track, TS> St = new Dictionary<Track, TS>(); }
    readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();

    Snapshot TakeSnapshot()
    {
        var s = new Snapshot(); foreach (var t in eng.Tracks.ToArray()) { s.Order.Add(t); s.St[t] = new TS { Offset = t.Offset, ClipStart = t.ClipStart, ClipFrames = t.ClipFrames, Vol = t.VolDb, Pan = t.Pan, Mute = t.Mute, Solo = t.Solo, Name = t.Name }; }
        return s;
    }
    void PushUndo() { undo.Add(TakeSnapshot()); if (undo.Count > 60) undo.RemoveAt(0); redo.Clear(); }
    void Restore(Snapshot s)
    {
        lock (eng.Sync)
        {
            eng.Tracks.Clear(); eng.Tracks.AddRange(s.Order);
            foreach (var t in s.Order) { var d = s.St[t]; t.Offset = d.Offset; t.ClipStart = d.ClipStart; t.ClipFrames = d.ClipFrames; t.VolDb = d.Vol; t.Pan = d.Pan; t.Mute = d.Mute; t.Solo = d.Solo; t.Name = d.Name; }
        }
        selTrack = Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1)); selFx = -1;
    }
    void DoUndo() { if (undo.Count == 0) { Toast("Nothing to undo."); return; } redo.Add(TakeSnapshot()); var s = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); Restore(s); Toast("Undone"); }
    void DoRedo() { if (redo.Count == 0) { Toast("Nothing to redo."); return; } undo.Add(TakeSnapshot()); var s = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1); Restore(s); Toast("Redone"); }

    // ---------- grid ----------
    double GridFrames { get { return StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm) * GridBeats[gridIdx]; } }
    long Snap(long frame) { if (!snapOn) return frame; double g = GridFrames; return (long)(Math.Round(frame / g) * g); }

    void PaintGrid(Graphics g, RectangleF tl, float top, float bottom)
    {
        if (!snapOn) return; double gf = GridFrames, bar = StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm) * 4; if (gf / spp < 6) { gf *= 4; if (gf / spp < 6) return; }
        long first = (long)(Math.Floor(scroll / gf) * gf); g.SetClip(Rc(tl.X, top, tl.Width, bottom - top));
        using (var thin = new Pen(Color.FromArgb(28, 190, 150, 255))) using (var thick = new Pen(Color.FromArgb(64, 210, 170, 255)))
            for (double f = first; ; f += gf)
            {
                float x = tl.X + (float)((f - scroll) / spp); if (x > tl.Right) break; if (x < tl.X) continue;
                bool isBar = Math.Abs(f / bar - Math.Round(f / bar)) < 0.0005; g.DrawLine(isBar ? thick : thin, x, top, x, bottom);
            }
        g.ResetClip();
    }

    // ---------- editing the selected track ----------
    Track Selected { get { return selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null; } }

    Fx CloneFx(Fx f)
    {
        try { var c = FxRegistry.Create(f.Type); if (c == null) return null; c.Prepare(StudioEngine.SR); c.Deserialize(f.Serialize()); c.Bypass = f.Bypass; var ic = c as InstrumentFx; if (ic != null) { } return c; }
        catch { return null; }
    }
    Track CloneTrack(Track t, string suffix)
    {
        var n = StudioEngine.NewTrack(t.Name + suffix, t.Role, t.Clip); n.Instrument = t.Instrument; n.Offset = t.Offset; n.ClipStart = t.ClipStart; n.ClipFrames = t.ClipFrames; n.Color = t.Color; n.VolDb = t.VolDb; n.Pan = t.Pan; n.Mute = t.Mute; n.Source = t.Source;
        lock (t.Midi) foreach (var m in t.Midi) n.Midi.Add(new MidiEv { Frame = m.Frame, S = m.S, D1 = m.D1, D2 = m.D2 });
        foreach (var f in t.Fx) { var c = CloneFx(f); if (c != null) { var ic = c as InstrumentFx; if (ic != null) ic.Owner = n; n.Fx.Add(c); } }
        return n;
    }
    void InsertAfter(Track after, Track n) { lock (eng.Sync) { int i = eng.Tracks.IndexOf(after); eng.Tracks.Insert(i < 0 ? eng.Tracks.Count : i + 1, n); } selTrack = eng.Tracks.IndexOf(n); selFx = -1; }

    void SplitSelected() { SplitTrack(Selected, Snap(DispPos)); }
    void SplitTrack(Track t, long at)
    {
        if (t == null) { Toast("Select a track first."); return; }
        if (t.Clip == null || t.Instrument) { Toast("Splitting works on audio tracks."); return; }
        long rel = at - t.Offset, used = t.UsedLength; if (rel < 800 || rel > used - 800) { Toast("Move the needle into the clip first (click on the track), then split."); return; }
        PushUndo(); var right = CloneTrack(t, " (2)"); right.Offset = at; right.ClipStart = t.ClipStart + rel; right.ClipFrames = used - rel; t.ClipFrames = rel;
        InsertAfter(t, right); Toast("Split in two. Drag a piece to move it, drag its ends to trim.");
    }
    void TrimStartHere(Track t) { TrimAt(t, Snap(DispPos), true); }
    void TrimEndHere(Track t) { TrimAt(t, Snap(DispPos), false); }
    void TrimAt(Track t, long at, bool start)
    {
        if (t == null || t.Clip == null) { Toast("Select an audio track first."); return; } long rel = at - t.Offset, used = t.UsedLength;
        if (rel < 800 || rel > used - 800) { Toast("Put the needle inside the clip to trim there."); return; }
        PushUndo();
        if (start) { t.Offset = at; t.ClipStart += rel; t.ClipFrames = t.ClipFrames < 0 ? -1 : used - rel; }
        else t.ClipFrames = rel;
        Toast(start ? "Start trimmed" : "End trimmed");
    }
    void DuplicateTrack(Track t)
    {
        if (t == null) return; PushUndo(); var n = CloneTrack(t, " copy"); InsertAfter(t, n); Toast("Duplicated");
    }
    void DeleteTrackQuiet(Track t)
    {
        if (t == null) return; PushUndo(); lock (eng.Sync) { eng.Tracks.Remove(t); retired.AddRange(t.Fx); } selTrack = Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1)); selFx = -1;
        foreach (var f in t.Fx) { var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }
        Toast("Deleted (Undo brings it back)");
    }
    void DisposeRetired() { foreach (var f in retired) { var d = f as IDisposable; if (d != null) { try { d.Dispose(); } catch { } } } retired.Clear(); }

    // ---------- mouse on a lane: seek, move the clip (name bar), trim (ends) ----------
    int laneMode; bool laneMoved; PointF laneStart; long l0Offset, l0ClipStart, l0ClipFrames, l0Used;
    // 0 = empty space (seek), 1 = on the clip (move), 2 = left end (trim start), 3 = right end (trim end)
    int ZoneAt(Track tr, RectangleF lane, PointF p)
    {
        if (tr.Clip == null || tr.Instrument || !lane.Contains(p)) return 0;
        float x0 = lane.X + (float)((tr.Offset - scroll) / spp), x1 = x0 + (float)(tr.UsedLength / spp), w = x1 - x0;
        if (w > 20 && Math.Abs(p.X - x0) <= 8) return 2; if (w > 20 && Math.Abs(p.X - x1) <= 8) return 3; if (p.X > x0 && p.X < x1 && p.Y > lane.Y + 4 && p.Y < lane.Bottom - 4) return 1; return 0;
    }
    static readonly string[] ZoneText = { "Click: put the needle here", "Drag: move this clip", "Drag: trim the start", "Drag: trim the end" };
    internal Cursor CursorAt(PointF p)
    {
        if (tab != 0 || busy || browserOpen) return Cursors.Default; float top = ToolH + TransH + RulerH - vscroll; var tl = TimelineRect();
        for (int i = 0; i < eng.Tracks.Count; i++)
        {
            var lane = Rc(HeadW, top + i * LaneH, tl.Width, LaneH); if (!lane.Contains(p)) continue;
            int z = ZoneAt(eng.Tracks[i], lane, p); if (laneMode != 0 && dragReg != null) z = laneMode; return z == 1 ? Cursors.SizeAll : (z >= 2 ? Cursors.SizeWE : Cursors.IBeam);
        }
        if (p.X < HeadW && p.Y > top) return Cursors.Hand; return Cursors.Default;
    }
    HitReg LaneReg(RectangleF lane, Track tr)
    {
        var lr = Reg(lane, () => LaneDown(tr, lane)); lr.OnDrag = pt => LaneDrag(tr, pt); lr.OnRight = () => LaneMenu(tr);
        if (Hot(lane)) hintNow = "Drag a clip to move it.  Drag its ends to trim.  Click = put the needle.  Right-click = split, trim, copy, delete.  The buttons at the top right of the selected track do the same.";
        return lr;
    }
    void LaneDown(Track tr, RectangleF lane)
    {
        selTrack = Math.Max(0, eng.Tracks.IndexOf(tr)); selFx = -1; laneMode = 0; laneStart = MousePos;
        if (tr.Clip != null && !tr.Instrument)
        {
            laneMode = ZoneAt(tr, lane, MousePos); laneMoved = false;
            if (laneMode >= 2) { PushUndo(); laneMoved = true; l0Offset = tr.Offset; l0ClipStart = tr.ClipStart; l0ClipFrames = tr.ClipFrames; l0Used = tr.UsedLength; return; }
            if (laneMode == 1) { l0Offset = tr.Offset; l0ClipStart = tr.ClipStart; l0ClipFrames = tr.ClipFrames; l0Used = tr.UsedLength; eng.Seek(Math.Max(0, Snap(scroll + (long)((MousePos.X - HeadW) * spp)))); return; }   // a click puts the needle, a drag moves the clip
        }
        eng.Seek(Math.Max(0, Snap(scroll + (long)((MousePos.X - HeadW) * spp))));
    }
    void LaneDrag(Track tr, PointF pt)
    {
        if (laneMode == 0) { eng.Seek(Math.Max(0, Snap(scroll + (long)((pt.X - HeadW) * spp)))); return; }
        long delta = (long)((pt.X - laneStart.X) * spp);
        if (laneMode == 1) { if (!laneMoved) { if (Math.Abs(pt.X - laneStart.X) < 4) return; PushUndo(); laneMoved = true; } tr.Offset = Math.Max(0, Snap(l0Offset + delta)); return; }
        long avail = Math.Max(0, tr.Clip.Length - l0ClipStart);
        if (laneMode == 2)
        {
            long newStart = Snap(l0Offset + delta); long d = newStart - l0Offset; d = Math.Max(-l0ClipStart, Math.Min(l0Used - 1000, d)); if (l0Offset + d < 0) d = -l0Offset;
            tr.Offset = l0Offset + d; tr.ClipStart = l0ClipStart + d; tr.ClipFrames = l0ClipFrames < 0 ? -1 : l0Used - d;
        }
        else
        {
            long end = Snap(l0Offset + l0Used + delta); long used = Math.Max(1000, Math.Min(avail, end - l0Offset)); tr.ClipFrames = used >= avail ? (l0ClipFrames < 0 ? -1 : used) : used;
        }
    }
    void LaneMenu(Track tr)
    {
        selTrack = Math.Max(0, eng.Tracks.IndexOf(tr)); var m = Win.NewMenu(); long at = Snap(Math.Max(0, scroll + (long)((MousePos.X - HeadW) * spp)));
        m.Items.Add("Split here (at the needle)").Click += (a, b) => SplitTrack(tr, Snap(DispPos));
        m.Items.Add("Split where I clicked").Click += (a, b) => SplitTrack(tr, at);
        m.Items.Add("Trim the start to the needle").Click += (a, b) => TrimStartHere(tr);
        m.Items.Add("Trim the end to the needle").Click += (a, b) => TrimEndHere(tr);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Duplicate track").Click += (a, b) => DuplicateTrack(tr);
        m.Items.Add("Rename...  (F2)").Click += (a, b) => RenameTrack(tr);
        m.Items.Add("Delete track").Click += (a, b) => DeleteTrackQuiet(tr);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // what the mouse is over on a clip: edge grips and a highlight, plus a label next to the cursor
    string bubble = ""; PointF bubblePt;
    void PaintClipChrome(Graphics g, Track tr, RectangleF lane, RectangleF cr, bool sel)
    {
        if (cr.Width < 6 || tr.Instrument) return; bool over = lane.Contains(MousePos) && hover != null && hover.R == lane; int zone = over ? (dragReg != null && laneMode != 0 ? laneMode : ZoneAt(tr, lane, MousePos)) : -1;
        if (!(sel || over)) return; var c = Color.FromArgb(tr.Color);
        if (zone == 1) { using (var b = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillRectangle(b, cr); StrokeRR(g, Color.FromArgb(230, 255, 255, 255), cr, 6, 1.6f); }
        float gh = Math.Min(46, cr.Height - 16), gy = cr.Y + (cr.Height - gh) / 2; bool leftEdge = cr.X > lane.X - 1, rightEdge = cr.Right < lane.Right - 1;
        if (leftEdge || zone == 2) { var r = Rc(cr.X + 1, gy, 9, gh); FillRR(g, Color.FromArgb(zone == 2 ? 250 : 175, zone == 2 ? Color.White : c), r, 4); Txt(g, "<", 11, Color.FromArgb(10, 4, 20), r, StringAlignment.Center, true); }
        if (rightEdge || zone == 3) { var r = Rc(cr.Right - 10, gy, 9, gh); FillRR(g, Color.FromArgb(zone == 3 ? 250 : 175, zone == 3 ? Color.White : c), r, 4); Txt(g, ">", 11, Color.FromArgb(10, 4, 20), r, StringAlignment.Center, true); }
        if (zone >= 0 && zone < ZoneText.Length) { bubble = ZoneText[zone]; bubblePt = MousePos; }
    }
    void PaintLaneTools(Graphics g, Track tr, RectangleF lane)
    {
        if (lane.Width < 640 || Selected != tr) return;
        string[] n = tr.Instrument ? new[] { "Up", "Dn", "Copy", "Del" } : new[] { "Up", "Dn", "<<", ">>", "Split", "<Trim", "Trim>", "Copy", "Del" };
        float w = 46, gap = 3, tot = n.Length * (w + gap) - gap, x = lane.Right - tot - 8, y = lane.Y + 6;
        FillRR(g, Color.FromArgb(205, 12, 6, 26), Rc(x - 5, y - 3, tot + 10, 28), 8);
        for (int i = 0; i < n.Length; i++)
        {
            string label = n[i]; var r = Rc(x + i * (w + gap), y, w, 22); Action act;
            switch (label)
            {
                case "Up": act = () => MoveTrackBy(tr, -1); break; case "Dn": act = () => MoveTrackBy(tr, 1); break;
                case "<<": act = () => MoveClip(tr, -1); break; case ">>": act = () => MoveClip(tr, 1); break; case "Split": act = () => SplitTrack(tr, Snap(DispPos)); break;
                case "<Trim": act = () => TrimStartHere(tr); break; case "Trim>": act = () => TrimEndHere(tr); break; case "Copy": act = () => DuplicateTrack(tr); break; default: act = () => DeleteTrackQuiet(tr); break;
            }
            Btn(g, r, label, label == "Del" ? 3 : (label == "Split" ? 1 : 0), act, 11);
            if (Hot(r))
            {
                bubble = label == "Up" ? "Move this track up one row" : label == "Dn" ? "Move this track down one row" : label == "<<" ? "Move this clip one grid step earlier" : label == ">>" ? "Move this clip one grid step later" : label == "Split" ? "Split at the needle (key B)" : label == "<Trim" ? "Cut the start off at the needle" : label == "Trim>" ? "Cut the end off at the needle" : label == "Copy" ? "Duplicate this track" : "Delete this track (Undo brings it back)";
                bubblePt = MousePos;
            }
        }
    }
    void MoveClip(Track tr, int dir)
    {
        if (tr.Clip == null) return; PushUndo(); double g = GridFrames;
        tr.Offset = Math.Max(0, snapOn ? (long)(Math.Round(tr.Offset / g) * g + dir * g) : tr.Offset + (long)(dir * StudioEngine.SR / 10));
    }
    void MoveTrackBy(Track tr, int dir)
    {
        int i = eng.Tracks.IndexOf(tr), j = i + dir; if (i < 0 || j < 0 || j >= eng.Tracks.Count) return; PushUndo();
        lock (eng.Sync) { eng.Tracks.RemoveAt(i); eng.Tracks.Insert(j, tr); } selTrack = j;
    }
    // dragging a track's header up or down changes its row
    bool headerUndo;
    void HeaderDown(Track tr) { selTrack = Math.Max(0, eng.Tracks.IndexOf(tr)); selFx = -1; headerUndo = false; }
    void HeaderDrag(Track tr, PointF pt)
    {
        int i = eng.Tracks.IndexOf(tr); if (i < 0) return; float top = ToolH + TransH + RulerH - vscroll;
        int target = (int)Math.Floor((pt.Y - top) / LaneH); target = Math.Max(0, Math.Min(eng.Tracks.Count - 1, target)); if (target == i) return;
        if (!headerUndo) { PushUndo(); headerUndo = true; }
        lock (eng.Sync) { eng.Tracks.RemoveAt(i); eng.Tracks.Insert(target, tr); } selTrack = target;
    }
    void PaintBubble(Graphics g)
    {
        string t = bubble; if (t.Length == 0) return;
        using (var f = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            var sz = g.MeasureString(t, f); float w = sz.Width + 18, h = 26; float x = Math.Min(ClientSize.Width - w - 6, bubblePt.X + 16), y = Math.Min(ClientSize.Height - h - 6, bubblePt.Y + 22); var r = Rc(x, y, w, h);
            FillRR(g, Color.FromArgb(240, 30, 16, 58), r, 8); StrokeRR(g, Accent, r, 8, 1.2f); Txt(g, t, 12, Ink, r, StringAlignment.Center, false);
        }
    }

    // ---------- tempo ----------
    void DetectBpmClick()
    {
        Track t = null; var s = Selected; if (s != null && s.Clip != null && !s.Instrument) t = s; if (t == null) foreach (var x in eng.Tracks) if (x.Clip != null && !x.Instrument && (x.Role == "beat" || x.Role == "song")) { t = x; break; }
        if (t == null) foreach (var x in eng.Tracks) if (x.Clip != null && !x.Instrument) { t = x; break; }
        if (t == null) { Toast("Add a song or a beat first."); return; }
        double bpm = 0; var clip = t.Clip; long st = t.ClipStart + (long)(StudioEngine.SR * 8); string nm = t.Name;
        RunBusy("Finding the tempo of " + nm + "...", false, p => { bpm = Tempo.Detect(clip, st, 70); }, ok =>
        {
            if (!ok) return; if (bpm <= 0) { Toast("Couldn't hear a steady beat. Type the BPM instead (click the number)."); return; }
            Fx.ProjectBpm = bpm; Toast("Tempo: " + bpm.ToString("0.0") + " BPM (the grid, the metronome and the synth's tempo-synced wobbles follow it)");
        });
    }

    // ---------- the strip under the transport ----------
    void PaintDawBar(Graphics g, float y0, float W)
    {
        float x = 14, y = y0 + 3, h = 30;
        Txt(g, "BPM", 11, DimC, Rc(x, y, 34, h), StringAlignment.Near, true); x += 34;
        var bb = Rc(x, y, 64, h); FillRR(g, Color.FromArgb(200, 8, 4, 18), bb, 6); StrokeRR(g, Hot(bb) ? Accent : Edge, bb, 6, 1); Txt(g, Fx.ProjectBpm.ToString("0.#", CultureInfo.InvariantCulture), 15, Ink, bb, StringAlignment.Center, true);
        ValueEdit(bb, Fx.ProjectBpm.ToString("0.#", CultureInfo.InvariantCulture), s => { float v; if (ParseNum(s, out v) && v >= 20 && v <= 300) Fx.ProjectBpm = v; }); x += 70;
        Btn(g, Rc(x, y, 62, h), "DETECT", 0, () => DetectBpmClick(), 11); x += 68;
        Btn(g, Rc(x, y, 56, h), snapOn ? "SNAP" : "snap", snapOn ? 4 : 0, () => snapOn = !snapOn, 12); x += 60;
        Btn(g, Rc(x, y, 76, h), GridNames[gridIdx], 0, () => { var m = Win.NewMenu(); for (int i = 0; i < GridNames.Length; i++) { int k = i; m.Items.Add(GridNames[i]).Click += (a, b) => { gridIdx = k; snapOn = true; }; } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 11); x += 82;
        Btn(g, Rc(x, y, 62, h), eng.Metronome ? "CLICK" : "click", eng.Metronome ? 4 : 0, () => eng.Metronome = !eng.Metronome, 12); x += 76;
        Btn(g, Rc(x, y, 62, h), "SPLIT", 1, () => SplitSelected(), 12); x += 66;
        Btn(g, Rc(x, y, 62, h), "TRIM <", 0, () => TrimStartHere(Selected), 11); x += 66;
        Btn(g, Rc(x, y, 62, h), "TRIM >", 0, () => TrimEndHere(Selected), 11); x += 66;
        Btn(g, Rc(x, y, 50, h), "DUP", 0, () => DuplicateTrack(Selected), 11); x += 54;
        Btn(g, Rc(x, y, 50, h), "DEL", 3, () => DeleteTrackQuiet(Selected), 11); x += 64;
        Btn(g, Rc(x, y, 58, h), "UNDO", 0, () => DoUndo(), 11); x += 62;
        Btn(g, Rc(x, y, 58, h), "REDO", 0, () => DoRedo(), 11); x += 70;
        Btn(g, Rc(x, y, 30, h), "-", 0, () => ZoomBy(1.4), 14); x += 34; Btn(g, Rc(x, y, 30, h), "+", 0, () => ZoomBy(0.7), 14); x += 34; Btn(g, Rc(x, y, 44, h), "FIT", 0, () => { FitView(); }, 11);
    }
    void ZoomBy(double f)
    {
        var tl = TimelineRect(); double cur = scroll + tl.Width / 2 * spp; spp = Math.Max(2, Math.Min(Math.Max(20000.0, eng.EndFrame / 200.0), spp * f)); scroll = Math.Max(0, (long)(cur - tl.Width / 2 * spp));
    }
}
