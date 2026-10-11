using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// ======================================================================
//  Clip editing: split as often as you like, fade in / fade out (drag the round handles), CUT a section out, GAIN for one piece, LOCK a track in place.
//  A "clip" is a track that holds audio. Pieces made by splitting share the row of the track they came from.
// ======================================================================
partial class StudioForm
{
    long cutMark = -1;                       // CUT: where the cut starts (needle position when you pressed CUT once)
    bool laneLocked, lockToldYou;            // a drag on a locked track
    bool gainOpen; Track gainClip; RectangleF gainAnchor;

    // ---------- which clip / track the buttons work on ----------
    Track RowHost(Track t) { return t == null ? null : (t.Host ?? t); }
    bool IsLocked(Track t) { var h = RowHost(t); return h != null && h.Locked; }
    static string TimeText(long frames) { double s = Math.Max(0, frames) / (double)StudioEngine.SR; return ((int)(s / 60)) + ":" + (s % 60).ToString("00.0", CultureInfo.InvariantCulture); }
    // the piece of this row that the needle is on (null when the needle is not on any piece)
    Track ClipAtNeedle(Track row, long at)
    {
        if (row == null) return null;
        foreach (var t in eng.Tracks.ToArray()) { if (RowHost(t) != row || t.Clip == null || t.Instrument) continue; if (at >= t.Offset && at < t.Offset + t.UsedLength) return t; }
        return null;
    }
    // the clip the toolbar buttons (fade, gain, delete...) act on: the piece you clicked, else the piece under the needle, else the first piece of the selected row
    Track CurClip()
    {
        var row = Selected; if (row == null || row.IsBus) return null;
        if (toolsTrack != null && eng.Tracks.Contains(toolsTrack) && toolsTrack.Clip != null && !toolsTrack.Instrument && RowHost(toolsTrack) == row) return toolsTrack;
        var c = ClipAtNeedle(row, DispPos); if (c != null) return c;
        foreach (var t in eng.Tracks.ToArray()) if (RowHost(t) == row && t.Clip != null && !t.Instrument) return t;
        return null;
    }

    // ---------- split: as many times as you want ----------
    void SplitSelected()
    {
        var row = Selected; if (row == null) { Toast("Select a track first."); return; }
        long at = Snap(DispPos); var c = ClipAtNeedle(row, at);
        if (c == null) { Toast("Move the needle onto a clip of the selected track (click the ruler), then press SPLIT."); return; }
        SplitTrack(c, at);
    }

    // ---------- delete one piece (not the whole row) ----------
    void DeleteClip(Track t)
    {
        if (t == null) return; if (IsLocked(t)) { Toast("Locked: press LOCK to unlock this track first."); return; }
        if (t.Host != null || t.Instrument || t.Clip == null || t.IsBus) { DeleteTrackQuiet(t); return; }
        var layers = eng.Tracks.FindAll(x => x.Host == t && !x.Instrument && x.Clip != null);
        if (layers.Count == 0) { DeleteTrackQuiet(t); return; }
        layers.Sort((a, b) => a.Offset.CompareTo(b.Offset)); var nx = layers[0]; PushUndo();
        lock (eng.Sync)
        {   // the row keeps its name, volume and plugins and takes over the next piece
            t.Clip = nx.Clip; t.Source = nx.Source; t.Offset = nx.Offset; t.ClipStart = nx.ClipStart; t.ClipFrames = nx.ClipFrames; t.FadeIn = nx.FadeIn; t.FadeOut = nx.FadeOut; t.ClipGainDb = nx.ClipGainDb;
            eng.Tracks.Remove(nx); retired.AddRange(nx.Fx);
        }
        toolsTrack = t; Toast("Piece deleted (Ctrl+Z brings it back)");
    }

    // ---------- fade in / fade out ----------
    void ToggleFade(bool fadeIn)
    {
        var c = CurClip(); if (c == null) { Toast("Click a clip first, then press FADE IN or FADE OUT."); return; }
        long used = c.UsedLength; if (used < 6000) { Toast("That clip is too short for a fade."); return; }
        long cur = fadeIn ? c.FadeIn : c.FadeOut; PushUndo();
        if (cur > 0) { if (fadeIn) c.FadeIn = 0; else c.FadeOut = 0; Toast((fadeIn ? "Fade in" : "Fade out") + " removed"); return; }
        long other = fadeIn ? c.FadeOut : c.FadeIn; long len = Math.Min(StudioEngine.SR * 2L, used / 4); len = Math.Max(2000, Math.Min(len, used - other - 1000));
        if (fadeIn) c.FadeIn = len; else c.FadeOut = len; toolsTrack = c;
        Toast((fadeIn ? "Fade in" : "Fade out") + " added. Drag the round handle at the top of the clip to make it shorter or longer.");
    }

    // ---------- lock ----------
    void ToggleLock()
    {
        var row = Selected; if (row == null || row.IsBus) { Toast("Select a track first."); return; }
        PushUndo(); row.Locked = !row.Locked;
        Toast(row.Locked ? row.Name + " is locked in place: it cannot be moved, trimmed, split, cut or deleted until you press UNLOCK." : row.Name + " is unlocked.");
    }

    // ---------- gain for one piece ----------
    void OpenGain()
    {
        var c = CurClip(); if (c == null) { Toast("Click a clip first, then press GAIN."); return; }
        gainClip = c; gainOpen = true; stylesOpen = false; autoOpen = false; browserOpen = false;
    }
    void PaintGainPanel(Graphics g)
    {
        if (!gainOpen) return; var c = gainClip; if (c == null || !eng.Tracks.Contains(c)) { gainOpen = false; return; }
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); Reg(all, () => gainOpen = false);
        float pw = 330, ph = 150; float px = Math.Max(8, Math.Min(W - pw - 8, gainAnchor.X + gainAnchor.Width / 2 - pw / 2)), py = gainAnchor.Bottom + 8; if (py + ph > H - 8) py = H - ph - 8; var box = Rc(px, py, pw, ph);
        FillRR(g, Color.FromArgb(252, 18, 10, 38), box, 14); Glow(g, NeonViolet, box, 14, 0.7f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), box, 14, 1.4f); Reg(box, () => { });
        Txt(g, "Gain", 17, Ink, Rc(box.X + 16, box.Y + 10, 80, 26), StringAlignment.Near, true);
        Txt(g, c.Name, 12, DimC, Rc(box.X + 70, box.Y + 14, box.Width - 130, 20), StringAlignment.Near, false);
        var cl = Rc(box.Right - 42, box.Y + 8, 30, 30); FillRR(g, Hot(cl) ? Color.FromArgb(160, 70, 38, 130) : Color.FromArgb(110, 40, 24, 80), cl, 15); Txt(g, "x", 15, Ink, cl, StringAlignment.Center, true); Reg(cl, () => gainOpen = false);
        Txt(g, "Volume of just this piece, on top of the track fader.", 11, DimC, Rc(box.X + 16, box.Y + 44, box.Width - 32, 18), StringAlignment.Near, false);
        const float lo = -30f, hi = 12f; float frac = (c.ClipGainDb - lo) / (hi - lo);
        var sr = Rc(box.X + 18, box.Y + 74, box.Width - 36 - 84, 22);
        var rg = Slider(g, sr, Clamp01(frac), f => { float v = (float)Math.Round(lo + f * (hi - lo), 1); if (Math.Abs(v) < 0.4f) v = 0; c.ClipGainDb = v; }, Accent2);
        var dn = rg.OnDown; rg.OnDown = () => { PushUndo(); if (dn != null) dn(); }; rg.OnDouble = () => c.ClipGainDb = 0;
        Txt(g, (c.ClipGainDb > 0 ? "+" : "") + c.ClipGainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB", 14, Ink, Rc(sr.Right + 8, box.Y + 74, 76, 22), StringAlignment.Near, true);
        Btn(g, Rc(box.X + 18, box.Bottom - 40, 90, 28), "Reset to 0", 0, () => { PushUndo(); c.ClipGainDb = 0; }, 12);
        Txt(g, "-30 dB  ...  +12 dB", 10, DimC, Rc(box.X + 118, box.Bottom - 38, box.Width - 136, 24), StringAlignment.Far, false);
    }

    // ---------- CUT: take a section out of every track that is not locked ----------
    void CutClick()
    {
        if (eng.Tracks.Count == 0) { Toast("Add some music first."); return; }
        long here = Snap(DispPos);
        if (cutMark < 0) { cutMark = here; Toast("Cut starts at " + TimeText(here) + ". Move the needle to where the cut should END, then press CUT again. (Esc cancels)"); return; }
        long a = Math.Min(cutMark, here), b = Math.Max(cutMark, here); cutMark = -1;
        if (b - a < 2000) { Toast("Cut cancelled: the end of the cut has to be later than its start. Press CUT again to start over."); return; }
        CutRange(a, b);
    }
    void CutCancel() { if (cutMark >= 0) { cutMark = -1; Toast("Cut cancelled"); } }
    void CutRange(long a, long b)
    {
        long len = b - a; PushUndo(); int touched = 0, skipped = 0;
        lock (eng.Sync)
        {
            foreach (var t in eng.Tracks.ToArray())
            {
                if (t.IsBus) continue; if (IsLocked(t)) { skipped++; continue; }
                if (t.Instrument)
                {
                    lock (t.Midi) { var keep = new List<MidiEv>(); foreach (var m in t.Midi) { if (m.Frame >= a && m.Frame < b) continue; if (m.Frame >= b) m.Frame -= len; keep.Add(m); } t.Midi.Clear(); t.Midi.AddRange(keep); }
                    touched++; continue;
                }
                if (t.Clip == null) continue;
                long s = t.Offset, e = t.Offset + t.UsedLength;
                if (e <= a) continue;
                if (s >= b) { t.Offset = s - len; touched++; continue; }
                if (s < a && e > b)
                {   // the cut is in the middle of this clip: it becomes two pieces and the right one slides back
                    var host = t.Host ?? t; var right = CloneTrack(t, " (2)"); right.Host = host; right.Out = host.Out; right.Offset = a; right.ClipStart = t.ClipStart + (b - s); right.ClipFrames = e - b; right.FadeIn = 0; right.FadeOut = t.FadeOut;
                    t.ClipFrames = a - s; t.FadeOut = 0; eng.Tracks.Insert(FirstBusIndex(), right);
                }
                else if (s < a) { t.ClipFrames = a - s; t.FadeOut = 0; }
                else if (e > b) { long d = b - s; t.ClipStart += d; t.ClipFrames = e - b; t.Offset = a; t.FadeIn = 0; }
                else if (t.Host != null) { eng.Tracks.Remove(t); retired.AddRange(t.Fx); }   // the whole piece was inside the cut
                else t.ClipFrames = 0;                                                       // a row's own piece: emptied below
                touched++;
            }
            // a row whose own piece was cut away takes over its next piece (or becomes an empty track)
            foreach (var h in eng.Tracks.ToArray())
            {
                if (h.Host != null || h.IsBus || h.Instrument || h.Clip == null || h.UsedLength > 0) continue;
                var layers = eng.Tracks.FindAll(x => x.Host == h && !x.Instrument && x.Clip != null); layers.Sort((p, q) => p.Offset.CompareTo(q.Offset));
                if (layers.Count > 0) { var nx = layers[0]; h.Clip = nx.Clip; h.Source = nx.Source; h.Offset = nx.Offset; h.ClipStart = nx.ClipStart; h.ClipFrames = nx.ClipFrames; h.FadeIn = nx.FadeIn; h.FadeOut = nx.FadeOut; h.ClipGainDb = nx.ClipGainDb; eng.Tracks.Remove(nx); retired.AddRange(nx.Fx); }
                else { h.Clip = null; h.Source = ""; h.ClipStart = 0; h.ClipFrames = -1; h.Offset = 0; h.FadeIn = 0; h.FadeOut = 0; }
            }
            foreach (var t in eng.Tracks) foreach (var l in t.Auto) { if (IsLocked(t)) continue; foreach (var pt in l.Pts) { if (pt.Frame >= b) pt.Frame -= len; else if (pt.Frame > a) pt.Frame = a; } l.SortPts(); }
        }
        eng.Seek(a);
        Toast("Cut " + TimeText(a) + " to " + TimeText(b) + " out of " + touched + (touched == 1 ? " track" : " tracks") + " and closed the gap" + (skipped > 0 ? " (" + skipped + " locked left as they were)" : "") + ". Ctrl+Z undoes it.");
    }
    void PaintCutMark(Graphics g)
    {
        if (cutMark < 0 || tab != 0) return; var tl = TimelineRect(); float x = tl.X + (float)((cutMark - scroll) / spp); if (x < tl.X || x > tl.Right) return;
        float top = ToolH + TransH, bottom = tl.Bottom;
        using (var pen = new Pen(Color.FromArgb(235, 255, 80, 120), 2f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, x, top + RulerH - 4, x, bottom);
        var flag = Rc(x - 1, top + 2, 70, 18); FillRR(g, Color.FromArgb(235, 255, 60, 110), flag, 5); Txt(g, "CUT START", 10, Color.White, flag, StringAlignment.Center, true);
    }

    // ---------- what is drawn on a clip: fades, gain tag, lock ----------
    void PaintClipExtras(Graphics g, Track tr, RectangleF lane, float x0, float x1, RectangleF cr)
    {
        if (cr.Width < 2) return; var gs = g.Save(); g.SetClip(Rectangle.Round(RectangleF.Intersect(cr, lane)), CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (multiSel.Contains(tr)) PaintSelectedClip(g, cr);
        float top = cr.Y, bot = cr.Bottom, hgt = bot - top;
        if (tr.FadeIn > 0)
        {
            float fw = (float)(tr.FadeIn / spp); var curve = new List<PointF>(); for (int i = 0; i <= 14; i++) { double u = i / 14.0; curve.Add(new PointF(x0 + (float)(u * fw), bot - (float)(Math.Sin(u * Math.PI / 2) * hgt))); }
            var poly = new List<PointF>(curve); poly.Add(new PointF(x0 + fw, top)); poly.Add(new PointF(x0, top));
            using (var b = new SolidBrush(Color.FromArgb(120, 6, 3, 16))) g.FillPolygon(b, poly.ToArray()); using (var pen = new Pen(Color.FromArgb(235, 0, 229, 255), 1.8f)) g.DrawLines(pen, curve.ToArray());
        }
        if (tr.FadeOut > 0)
        {
            float fw = (float)(tr.FadeOut / spp); var curve = new List<PointF>(); for (int i = 0; i <= 14; i++) { double u = i / 14.0; curve.Add(new PointF(x1 - fw + (float)(u * fw), top + (float)(Math.Sin(u * Math.PI / 2) * hgt))); }
            var poly = new List<PointF>(curve); poly.Add(new PointF(x1, top)); poly.Add(new PointF(x1 - fw, top));
            using (var b = new SolidBrush(Color.FromArgb(120, 6, 3, 16))) g.FillPolygon(b, poly.ToArray()); using (var pen = new Pen(Color.FromArgb(235, 0, 229, 255), 1.8f)) g.DrawLines(pen, curve.ToArray());
        }
        g.Restore(gs);
        // the small tags at the top of the clip: gain, lock
        float tx = Math.Max(lane.X, x0) + 8 + g.MeasureString(tr.Name, GfxCache.Font(12, true)).Width + 6;
        if (Math.Abs(tr.ClipGainDb) > 0.04f && tx < cr.Right - 60) { string gt = (tr.ClipGainDb > 0 ? "+" : "") + tr.ClipGainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB"; var rr = Rc(tx, cr.Y + 3, g.MeasureString(gt, GfxCache.Font(10, true)).Width + 10, 16); FillRR(g, Color.FromArgb(200, 70, 40, 150), rr, 5); Txt(g, gt, 10, Color.White, rr, StringAlignment.Center, true); }
        if (IsLocked(tr) && cr.Width > 40) { float lx = Math.Min(cr.Right, lane.Right) - 22, ly = cr.Y + 6; using (var pen = new Pen(Color.FromArgb(235, 255, 255, 255), 1.8f)) { g.DrawArc(pen, lx + 2, ly - 2, 8, 9, 180, 180); } using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillRectangle(b, lx, ly + 3, 12, 9); }
    }
}
