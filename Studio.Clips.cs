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
    bool IsOffRow(Track t) { var h = RowHost(t); return (h != null && h.Off) || (t != null && t.Off); }
    // locked in place, or switched OFF: neither can be moved, trimmed, split, cut, faded or deleted
    bool IsLocked(Track t) { var h = RowHost(t); return (h != null && h.Locked) || (t != null && t.PieceLocked) || IsOffRow(t); }   // the whole track locked, this one piece locked, or the track switched off
    bool IsRowLocked(Track row) { var h = RowHost(row); return (h != null && h.Locked) || IsOffRow(row); }
    string FrozenWhy(Track t, string lockedMsg) { if (IsOffRow(t)) return "This track is OFF: press its power button (top left) to turn it on before you edit it."; var h = RowHost(t); if (t != null && t.PieceLocked && !(h != null && h.Locked)) return "This section is locked: right-click it and press Unlock this section to move or edit it."; return lockedMsg; }
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
    // where a split goes: exactly where the needle is. It only sits on the grid when a grid line is within a few pixels of it: zoomed in, the grid lines are far apart and snapping would throw the split away from the needle
    long SplitPoint(long raw) { return Math.Max(0, raw); }   // a split goes exactly where the needle or the click is (no pull to the grid), as free as moving a piece
    void SplitSelected()
    {
        var row = Selected; if (row == null) { Toast("Select a track first."); return; }
        long at = SplitPoint(DispPos); var c = ClipAtNeedle(row, at);
        if (c == null) { Toast("Move the needle onto a clip of the selected track (click the ruler), then press SPLIT."); return; }
        SplitTrack(c, at);
    }

    // ---------- delete one piece (not the whole row) ----------
    void DeleteClip(Track t)
    {
        if (t == null) return; if (IsLocked(t)) { Toast(FrozenWhy(t, "Locked: press LOCK to unlock this track first.")); return; }
        if (t.Host != null || t.Instrument || t.Clip == null || t.IsBus) { DeleteTrackQuiet(t); return; }
        var layers = eng.Tracks.FindAll(x => x.Host == t && !x.Instrument && x.Clip != null);
        if (layers.Count == 0) { DeleteTrackQuiet(t); return; }
        layers.Sort((a, b) => a.Offset.CompareTo(b.Offset)); var nx = layers[0]; PushUndo();
        lock (eng.Sync)
        {   // the row keeps its name, volume and plugins and takes over the next piece
            t.Clip = nx.Clip; t.Source = nx.Source; t.Offset = nx.Offset; t.ClipStart = nx.ClipStart; t.ClipFrames = nx.ClipFrames; t.FadeIn = nx.FadeIn; t.FadeOut = nx.FadeOut; t.ClipGainDb = nx.ClipGainDb; t.FadeInDelay = nx.FadeInDelay; t.FadeOutTail = nx.FadeOutTail; ClampFades(t);   // the silence before/after belongs to the piece too, not to the one that was deleted
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

    // after a trim: the two fades never add up to more than the clip
    static void ClampFades(Track t) { long u = t.UsedLength, sum = t.FadeIn + t.FadeOut + t.FadeInDelay + t.FadeOutTail; if (u > 0 && sum > u) { double k = u / (double)sum; t.FadeIn = (long)(t.FadeIn * k); t.FadeOut = (long)(t.FadeOut * k); t.FadeInDelay = (long)(t.FadeInDelay * k); t.FadeOutTail = (long)(t.FadeOutTail * k); } }

    // ---------- lock ----------
    // right-click a piece: lock or unlock just that section (the rest of the track can still be moved, cut and edited)
    internal void TogglePieceLock(Track p)
    {
        if (p == null || p.Clip == null || p.Instrument) return; PushUndo(); p.PieceLocked = !p.PieceLocked;
        Toast(p.PieceLocked ? "This section is locked: it cannot be moved, trimmed, split, cut or deleted. Right-click it and press Unlock this section to free it." : "This section is unlocked.");
    }
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
        popOff.Remove("gain"); gainClip = c; gainOpen = true; stylesOpen = false; autoOpen = false; browserOpen = false;
    }
    // GAIN: the same neon box as the track picker (TRACK 1, TRACK 2...): press a track to pick it, then set its gain under the list
    float gnScroll; int gnAt;
    void PaintGainPanel(Graphics g)
    {
        if (!gainOpen) { gnAt = 0; return; } var c = gainClip; if (c == null || !eng.Tracks.Contains(c)) { gainOpen = false; return; } if (gnAt == 0) gnAt = Environment.TickCount; lastInput = DateTime.Now;
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); using (var bd = new SolidBrush(TM.C(150, 3, 1, 10))) g.FillRectangle(bd, all); Reg(all, () => gainOpen = false);
        var gl = new List<int>(); for (int ti = 0; ti < eng.Tracks.Count; ti++) { var tk = eng.Tracks[ti]; if (tk.Clip != null && !tk.Instrument && !tk.IsBus) gl.Add(ti); }
        int n = gl.Count; float rowH = 46, bw = Math.Min(460, W - 40), want = 130 + Math.Min(n, 6) * rowH + 130, bh = Math.Min(H - 60, want);
        int age = unchecked(Environment.TickCount - gnAt); float k = Math.Min(1f, age / 220f), grow = 0.94f + 0.06f * (1 - (1 - k) * (1 - k)); double pulse = 0.5 + 0.5 * Math.Sin(age / 320.0);
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2, bw * grow, bh * grow);
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((16 + 22 * pulse) / i * 2.2f) + 5, NeonCyan), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 22 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 22)) { using (var br = new LinearGradientBrush(box, TM.C(250, 26, 12, 58), TM.C(250, 9, 4, 24), 90f)) g.FillPath(br, p); using (var pen = new Pen(NeonCyan, 2.2f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(110, NeonPink), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -9, -9), 16)) g.DrawPath(pen, p); Reg(box, () => { });
        Txt(g, "GAIN OF WHICH TRACK?", 20, Ink, Rc(box.X + 24, box.Y + 16, box.Width - 90, 32), StringAlignment.Near, true);
        Txt(g, "Pick the track by its number, then set its volume.", 12, DimC, Rc(box.X + 24, box.Y + 50, box.Width - 48, 20), StringAlignment.Near, false);
        var cl = Rc(box.Right - 46, box.Y + 14, 30, 28); Btn(g, cl, "x", 3, () => gainOpen = false, 12);
        float footH = 118; var lr = Rc(box.X + 20, box.Y + 80, box.Width - 40, box.Height - 80 - footH - 6); float total = n * rowH, maxS = Math.Max(0, total - lr.Height); gnScroll = Math.Max(0, Math.Min(maxS, gnScroll));
        regs.Add(new HitReg { R = lr, OnWheel = d => gnScroll = Math.Max(0, Math.Min(maxS, gnScroll - d * 40)) });
        { var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
          for (int q = 0; q < n; q++)
          {
              int ri = gl[q]; var t = eng.Tracks[ri]; var row = Rc(lr.X, lr.Y + q * rowH - gnScroll, lr.Width - (maxS > 0 ? 22 : 0), rowH - 6); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue; bool sel = t == c, hot = Hot(row); var tkk = t;
              FillRR(g, hot ? TM.C(225, 90, 50, 170) : (sel ? TM.C(210, 62, 34, 130) : TM.C(170, 28, 15, 56)), row, 10); StrokeRR(g, sel ? NeonCyan : Color.FromArgb(hot ? 220 : 90, NeonViolet), row, 10, sel ? 1.8f : 1f); Reg(row, () => { gainClip = tkk; selTrack = eng.Tracks.IndexOf(tkk); });
              Txt(g, "TRACK " + (ri + 1), 15, NeonCyan, Rc(row.X + 14, row.Y, 96, row.Height), StringAlignment.Near, true);
              Txt(g, t.Name, 14, Ink, Rc(row.X + 112, row.Y, row.Width - 112 - 96, row.Height), StringAlignment.Near, true);
              Txt(g, (t.ClipGainDb > 0 ? "+" : "") + t.ClipGainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB", 12, Math.Abs(t.ClipGainDb) > 0.04f ? Color.FromArgb(255, 255, 200, 90) : DimC, Rc(row.Right - 92, row.Y, 82, row.Height), StringAlignment.Far, true);
          }
          g.Restore(gs); }
        if (maxS > 0) VScroll(g, Rc(lr.Right - 20, lr.Y, 18, lr.Height), total, lr.Height, () => gnScroll, v => gnScroll = v);
        // the volume of the picked track
        const float lo = -30f, hi = 12f; float frac = (c.ClipGainDb - lo) / (hi - lo); float fy = box.Bottom - footH;
        Txt(g, "VOLUME OF TRACK " + (eng.Tracks.IndexOf(c) + 1) + "  (just this piece, on top of the track fader)", 11, DimC, Rc(box.X + 24, fy, box.Width - 48, 18), StringAlignment.Near, true);
        var sr = Rc(box.X + 24, fy + 28, box.Width - 48 - 96, 22);
        var rg = Slider(g, sr, Clamp01(frac), f => { float v = (float)Math.Round(lo + f * (hi - lo), 1); if (Math.Abs(v) < 0.4f) v = 0; c.ClipGainDb = v; }, Accent2);
        var dn = rg.OnDown; rg.OnDown = () => { PushUndo(); if (dn != null) dn(); }; rg.OnDouble = () => c.ClipGainDb = 0;
        Txt(g, (c.ClipGainDb > 0 ? "+" : "") + c.ClipGainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB", 15, Ink, Rc(sr.Right + 10, fy + 26, 86, 26), StringAlignment.Near, true);
        Btn(g, Rc(box.X + 24, box.Bottom - 54, 130, 36), "RESET TO 0", 1, () => { PushUndo(); c.ClipGainDb = 0; }, 12);
        Txt(g, "-30 dB  ...  +12 dB", 10, DimC, Rc(box.X + 166, fy + 54, 150, 18), StringAlignment.Near, false);
        Btn(g, Rc(box.Right - 24 - 130, box.Bottom - 54, 130, 36), "DONE", 2, () => gainOpen = false, 13);
    }

    // ---------- CUT: take a section out of every track that is not locked ----------
    // CUT: the selected piece (or every clip lit by a selection box) is DELETED and the pieces after it on that track move up to close the gap.
    // (SPLIT is the one that makes two pieces: split where the part you do not want starts and ends, click that piece, press CUT.)
    void CutClick()
    {
        var picks = new List<Track>(); if (multiSel.Count > 0) picks.AddRange(multiSel); else { var c = CurClip(); if (c != null) picks.Add(c); }
        picks.RemoveAll(t => !eng.Tracks.Contains(t) || t.Clip == null || t.Instrument);
        if (picks.Count == 0) { Toast("Click the piece you want to cut out, then press CUT. (SPLIT the track where the part starts and ends first.)"); return; }
        var ok = new List<Track>(); int locked = 0; foreach (var t in picks) { if (IsLocked(t)) locked++; else ok.Add(t); }
        if (ok.Count == 0) { Toast("Locked: press UNLOCK before cutting this track."); return; }
        PushUndo(); undoHold++; long total = 0;
        try
        {
            ok.Sort((a, b) => b.Offset.CompareTo(a.Offset));   // from the end of the song backwards, so the places of the earlier ones do not change
            foreach (var t in ok)
            {
                if (!eng.Tracks.Contains(t)) continue; var row = RowHost(t); long len = t.UsedLength, end = t.Offset + len; total += len; DeleteClip(t);
                // nothing after it moves: CUT leaves a gap where the piece was (the audio after it stays exactly where it is)
            }
        }
        finally { undoHold--; }
        multiSel.Clear(); toolsTrack = null; cutMark = -1;
        Toast("Cut out " + ok.Count + (ok.Count == 1 ? " piece" : " pieces") + " (" + (total / (double)StudioEngine.SR).ToString("0.0") + " s): deleted. Nothing else moved." + (locked > 0 ? " " + locked + " locked left alone." : "") + " Ctrl+Z undoes it.");
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
                    lock (t.Midi) { var keep = new List<MidiEv>(); foreach (var m in t.Midi) { if (m.Frame >= a && m.Frame < b) continue; keep.Add(m); } t.Midi.Clear(); t.Midi.AddRange(keep); }
                    touched++; continue;
                }
                if (t.Clip == null) continue;
                long s = t.Offset, e = t.Offset + t.UsedLength;
                if (e <= a) continue;
                if (s >= b) continue;   // after the cut: it stays exactly where it is
                if (s < a && e > b)
                {   // the cut is in the middle of this clip: it becomes two pieces with a gap between them (the right one stays where it was)
                    var host = t.Host ?? t; var right = CloneTrack(t, " (2)"); right.Host = host; right.Out = host.Out; right.Offset = b; right.ClipStart = t.ClipStart + (b - s); right.ClipFrames = e - b; right.FadeIn = 0; right.FadeOut = t.FadeOut;
                    t.ClipFrames = a - s; t.FadeOut = 0; eng.Tracks.Insert(FirstBusIndex(), right);
                }
                else if (s < a) { t.ClipFrames = a - s; t.FadeOut = 0; }
                else if (e > b) { long d = b - s; t.ClipStart += d; t.ClipFrames = e - b; t.Offset = b; t.FadeIn = 0; }
                else if (t.Host != null) { eng.Tracks.Remove(t); retired.AddRange(t.Fx); }   // the whole piece was inside the cut
                else t.ClipFrames = 0;                                                       // a row's own piece: emptied below
                touched++;
            }
            // a row whose own piece was cut away takes over its next piece (or becomes an empty track)
            foreach (var h in eng.Tracks.ToArray())
            {
                if (h.Host != null || h.IsBus || h.Instrument || h.Clip == null || h.UsedLength > 0) continue;
                var layers = eng.Tracks.FindAll(x => x.Host == h && !x.Instrument && x.Clip != null); layers.Sort((p, q) => p.Offset.CompareTo(q.Offset));
                if (layers.Count > 0) { var nx = layers[0]; h.Clip = nx.Clip; h.Source = nx.Source; h.Offset = nx.Offset; h.ClipStart = nx.ClipStart; h.ClipFrames = nx.ClipFrames; h.FadeIn = nx.FadeIn; h.FadeOut = nx.FadeOut; h.ClipGainDb = nx.ClipGainDb; h.FadeInDelay = nx.FadeInDelay; h.FadeOutTail = nx.FadeOutTail; ClampFades(h); eng.Tracks.Remove(nx); retired.AddRange(nx.Fx); }
                else { h.Clip = null; h.Source = ""; h.ClipStart = 0; h.ClipFrames = -1; h.Offset = 0; h.FadeIn = 0; h.FadeOut = 0; h.FadeInDelay = 0; h.FadeOutTail = 0; }
            }
            // the automation stays where it is too: nothing on the timeline moves
        }
        eng.Seek(a);
        Toast("Cut " + TimeText(a) + " to " + TimeText(b) + " out of " + touched + (touched == 1 ? " track" : " tracks") + " (the audio after it stays where it is)" + (skipped > 0 ? " (" + skipped + " locked left as they were)" : "") + ". Ctrl+Z undoes it.");
    }
    void PaintCutMark(Graphics g)
    {
        if (cutMark < 0 || tab != 0) return; var tl = TimelineRect(); float x = tl.X + (float)((cutMark - scroll) / spp); if (x < tl.X || x > tl.Right) return;
        float top = ToolH + TransH, bottom = tl.Bottom;
        using (var pen = new Pen(Color.FromArgb(235, 255, 80, 120), 2f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, x, top + RulerH - 4, x, bottom);
        var flag = Rc(x - 1, Math.Min(bottom, ClientSize.Height - 48) - 24, 70, 18); FillRR(g, TM.C(235, 255, 60, 110), flag, 5); Txt(g, "CUT START", 10, Color.White, flag, StringAlignment.Center, true);
    }

    // ---------- what is drawn on a clip: fades, gain tag, lock ----------
    void PaintClipExtras(Graphics g, Track tr, RectangleF lane, float x0, float x1, RectangleF cr)
    {
        if (cr.Width < 2) return; var gs = g.Save(); g.SetClip(Rectangle.Round(RectangleF.Intersect(cr, lane)), CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (multiSel.Contains(tr)) PaintSelectedClip(g, cr);
        float top = cr.Y, bot = cr.Bottom, hgt = bot - top;
        if (tr.FadeIn > 0)
        {
            float fw = (float)(tr.FadeIn / spp), dl = (float)(tr.FadeInDelay / spp); var curve = new List<PointF>(); if (dl > 0.5f) curve.Add(new PointF(x0, bot)); for (int i = 0; i <= 24; i++) { double u = i / 24.0; curve.Add(new PointF(x0 + dl + (float)(u * fw), bot - Track.FadeCurve(u) * hgt)); }
            var poly = new List<PointF>(curve); poly.Add(new PointF(x0 + dl + fw, top)); poly.Add(new PointF(x0, top));
            using (var b = new SolidBrush(TM.C(120, 6, 3, 16))) g.FillPolygon(b, poly.ToArray()); using (var pen = new Pen(TM.C(235, 0, 229, 255), 1.8f)) g.DrawLines(pen, curve.ToArray());
        }
        if (tr.FadeOut > 0)
        {
            float fw = (float)(tr.FadeOut / spp), tl2 = (float)(tr.FadeOutTail / spp); var curve = new List<PointF>(); for (int i = 0; i <= 24; i++) { double u = i / 24.0; curve.Add(new PointF(x1 - tl2 - fw + (float)(u * fw), bot - Track.FadeCurve(1 - u) * hgt)); } if (tl2 > 0.5f) curve.Add(new PointF(x1, bot));
            var poly = new List<PointF>(curve); poly.Add(new PointF(x1, top)); poly.Add(new PointF(x1 - tl2 - fw, top));
            using (var b = new SolidBrush(TM.C(120, 6, 3, 16))) g.FillPolygon(b, poly.ToArray()); using (var pen = new Pen(TM.C(235, 0, 229, 255), 1.8f)) g.DrawLines(pen, curve.ToArray());
        }
        g.Restore(gs);
        // the small tags at the top of the clip: gain, lock
        float tx = Math.Max(lane.X, x0) + 8 + g.MeasureString(tr.Name, GfxCache.Font(12, true)).Width + 6;
        if (Math.Abs(tr.ClipGainDb) > 0.04f && tx < cr.Right - 60) { string gt = (tr.ClipGainDb > 0 ? "+" : "") + tr.ClipGainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB"; var rr = Rc(tx, cr.Y + 3, g.MeasureString(gt, GfxCache.Font(10, true)).Width + 10, 16); FillRR(g, TM.C(200, 70, 40, 150), rr, 5); Txt(g, gt, 10, Color.White, rr, StringAlignment.Center, true); }
        if (tr.PieceLocked && !IsRowLocked(tr) && cr.Width > 12)
        {   // a locked SECTION (only this piece) has its own look: gold stripes across it, a glowing gold edge and a LOCKED SECTION tag, so it is never mixed up with a locked track
            var gold2 = Color.FromArgb(255, 255, 196, 80); var vis = RectangleF.Intersect(cr, lane); var gs2 = g.Save(); g.SetClip(vis, CombineMode.Intersect);
            using (var hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(70, gold2), Color.Transparent)) g.FillRectangle(hb, cr);
            using (var b = new SolidBrush(Color.FromArgb(28, gold2))) g.FillRectangle(b, cr); g.Restore(gs2);
            Glow(g, gold2, vis, 5, 1.0f, 4); using (var pen = new Pen(gold2, 2.4f)) using (var pp = RR(RectangleF.Inflate(vis, -1.2f, -1.2f), 5)) g.DrawPath(pen, pp);
            float tagW = 108, tagX = Math.Min(cr.Right, lane.Right) - 30 - tagW - 10; if (tagX > Math.Max(lane.X, cr.X) + 6 && cr.Height > 34) { var tg = Rc(tagX, cr.Y + 6, tagW, 20); FillRR(g, Color.FromArgb(235, 40, 26, 4), tg, 6); StrokeRR(g, gold2, tg, 6, 1.2f); Txt(g, "LOCKED SECTION", 9.5f, gold2, tg, StringAlignment.Center, true); }
        }
        if (IsLocked(tr) && cr.Width > 40) {   // a locked piece is gold all round, with a big gold padlock: it is plain which tracks are locked
            var gold = Color.FromArgb(255, 255, 196, 80); float lx = Math.Min(cr.Right, lane.Right) - 30, ly = cr.Y + 8; var smk = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(60, gold), 5f)) using (var pp = RR(RectangleF.Inflate(cr, -1, -1), 5)) g.DrawPath(pen, pp); using (var pen = new Pen(Color.FromArgb(230, gold), 1.6f)) using (var pp = RR(RectangleF.Inflate(cr, -1, -1), 5)) g.DrawPath(pen, pp);
            using (var bg = new SolidBrush(Color.FromArgb(210, 20, 12, 2))) g.FillEllipse(bg, lx - 6, ly - 8, 30, 32); using (var pen = new Pen(gold, 2.4f)) g.DrawArc(pen, lx + 2, ly - 3, 12, 13, 180, 180); using (var b = new SolidBrush(gold)) g.FillPath(b, RR(Rc(lx, ly + 5, 16, 12), 3)); using (var b = new SolidBrush(Color.FromArgb(255, 60, 36, 8))) g.FillEllipse(b, lx + 6, ly + 9, 4, 4); g.SmoothingMode = smk; }
    }
}
