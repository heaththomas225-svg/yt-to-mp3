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
    bool snapOn = true; int gridIdx = 2; static readonly string[] GridNames = { "Bar", "1/2 bar", "Beat", "1/8 note", "1/16 note" }; static readonly string[] GridWhy = { "every bar (4 beats)", "every half bar (2 beats)", "every beat", "every half beat (eighth note)", "every quarter beat (sixteenth note)" }; int gridFlashAt = -100000; static readonly double[] GridBeats = { 4, 2, 1, 0.5, 0.25 };
    readonly List<Fx> retired = new List<Fx>();   // effects of deleted tracks stay alive so Undo can bring the track back
    class TS { public List<MidiClip> MClips; public List<MidiEv> Midi; public float Trim; public bool Invert; public Send[] Sends; public long Offset, ClipStart, ClipFrames; public float Vol, Pan; public bool Mute, Solo; public string Name; public AudioClip Clip; public Track Host, Out; public List<Fx> Fx; public List<AutoLane> Auto; public long FadeIn, FadeOut, FadeInDelay, FadeOutTail; public bool Mono; public float ClipGain; public bool Locked, PieceLocked, PieceMuted; public string Label = ""; }
    class Snapshot { public List<Track> Order = new List<Track>(); public Dictionary<Track, TS> St = new Dictionary<Track, TS>(); }
    readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();

    Snapshot TakeSnapshot()
    {
        var s = new Snapshot(); foreach (var t in eng.Tracks.ToArray()) { s.Order.Add(t); s.St[t] = new TS { Offset = t.Offset, ClipStart = t.ClipStart, ClipFrames = t.ClipFrames, Clip = t.Clip, Vol = t.VolDb, Pan = t.Pan, Mute = t.Mute, Solo = t.Solo, Name = t.Name, Host = t.Host, Out = t.Out, Fx = new List<Fx>(t.Fx), Auto = CloneLanes(t.Auto), FadeIn = t.FadeIn, FadeOut = t.FadeOut, FadeInDelay = t.FadeInDelay, FadeOutTail = t.FadeOutTail, Mono = t.Mono, Trim = t.TrimDb, Invert = t.Invert, Midi = CopyMidi(t), MClips = t.MClips.ConvertAll(c => c.Copy()), Sends = Array.ConvertAll(t.Sends, x => x.Copy()), ClipGain = t.ClipGainDb, Locked = t.Locked, PieceLocked = t.PieceLocked, PieceMuted = t.PieceMuted, Label = t.SectionLabel }; }
        return s;
    }
    static List<AutoLane> CloneLanes(List<AutoLane> l) { var r = new List<AutoLane>(); foreach (var x in l) r.Add(x.CloneFor(x.Fx)); return r; }
    void PushUndo() { editCount++; HistNote(); if (undoHold > 0) return; undo.Add(TakeSnapshot()); if (undo.Count > 60) undo.RemoveAt(0); redo.Clear(); }
    void Restore(Snapshot s)
    {
        toolsTrack = null; if (flexOpen) FlexClose(false);
        Track keep = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null; var gone = new List<Track>();
        lock (eng.Sync)
        {
            foreach (var t in eng.Tracks) if (!s.St.ContainsKey(t)) gone.Add(t);   // tracks this undo drops (e.g. a just-added instrument): their plugins are retired and windows closed below
            eng.Tracks.Clear(); eng.Tracks.AddRange(s.Order);
            foreach (var t in s.Order) { var d = s.St[t]; t.EverHadClip = false; t.Clip = d.Clip; t.Offset = d.Offset; t.ClipStart = d.ClipStart; t.ClipFrames = d.ClipFrames; t.VolDb = d.Vol; t.Pan = d.Pan; t.Mute = d.Mute; t.Solo = d.Solo; t.Name = d.Name; t.Host = d.Host; t.Out = d.Out; t.FadeIn = d.FadeIn; t.FadeOut = d.FadeOut; t.FadeInDelay = d.FadeInDelay; t.FadeOutTail = d.FadeOutTail; t.Mono = d.Mono; t.TrimDb = d.Trim; t.Invert = d.Invert; if (d.MClips != null) t.MClips = d.MClips.ConvertAll(c => c.Copy()); mcSel.Clear(); mcStamp.Remove(t); if (d.Midi != null) lock (t.Midi) { t.Midi.Clear(); foreach (var me in d.Midi) t.Midi.Add(new MidiEv { Frame = me.Frame, S = me.S, D1 = me.D1, D2 = me.D2 }); } t.Sends = d.Sends ?? new Send[0]; t.ClipGainDb = d.ClipGain; t.Locked = d.Locked; t.PieceLocked = d.PieceLocked; t.PieceMuted = d.PieceMuted; t.SectionLabel = d.Label ?? ""; if (d.Fx != null) { t.Fx.Clear(); t.Fx.AddRange(d.Fx); } if (d.Auto != null) { t.Auto.Clear(); t.Auto.AddRange(CloneLanes(d.Auto)); } }
        }
        foreach (var t in gone) foreach (var f in t.Fx) { if (!retired.Contains(f)) retired.Add(f); var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }
        // keep the same track selected by object; the list order may have changed, and layers/buses after the lanes must never be selected
        int k = keep != null ? eng.Tracks.IndexOf(keep) : -1; selTrack = k >= 0 ? k : selTrack; selTrack = Math.Max(0, Math.Min(selTrack, LaneCount - 1)); selFx = -1;
    }
    void DoUndo() { if (undo.Count == 0) { Toast("Nothing to undo."); return; } redo.Add(TakeSnapshot()); var s = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); Restore(s); Toast("Undone"); }
    void DoRedo() { if (redo.Count == 0) { Toast("Nothing to redo."); return; } undo.Add(TakeSnapshot()); var s = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1); Restore(s); Toast("Redone"); }

    // ---------- grid ----------
    double GridFrames { get { return StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm) * GridBeats[gridIdx]; } }
    long Snap(long frame) { if (!snapOn) return frame; double g = GridFrames; return (long)(Math.Round(frame / g) * g); }

    readonly Bitmap[] gridStrips = new Bitmap[2]; readonly long[] gridStripKeys = new long[2]; double first0(double gf) { return Math.Floor(scroll / gf) * gf; }
    void PaintGrid(Graphics g, RectangleF tl, float top, float bottom, float dim = 1f, Color? laneBg = null)
    {
        if (!snapOn) { if (laneBg.HasValue) using (var lb = new SolidBrush(laneBg.Value)) g.FillRectangle(lb, tl.X, top, tl.Width, bottom - top); return; } double gf = GridFrames, bar = StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm) * 4; while (gf / spp < 7 && gf < bar * 64) gf *= 2;   // too close together to see: show every 2nd, 4th... line
        bool flash = unchecked(Environment.TickCount - gridFlashAt) < 1800, active = flash || (dragReg != null && laneMoved) || ghostOn; if (flash) lastInput = DateTime.Now;
        double fl = flash ? 1.0 - unchecked(Environment.TickCount - gridFlashAt) / 1800.0 : 0; int boost = flash ? 6 + (int)(26 * fl) : (active ? 22 : 0);   // a gentle brightening that fades out (it used to be thick and very bright)
        if (bottom - top <= LaneH + 2 && tl.Width * (bottom - top) < 1200000)
        {   // the lines of one row are drawn once per frame into a picture and copied into every row (every row has the same lines)
            int sw = (int)Math.Ceiling(tl.Width), sh = (int)Math.Ceiling(bottom - top); long gk = Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.Seed, scroll), (long)(spp * 1000)), (long)(gf * 100)), boost), (long)(dim * 100)), sw), sh * 4096L + (long)(Fx.ProjectBpm * 10)); gk = Sprites.H(gk, ThemeKeyNow()); int si = laneBg.HasValue && laneBg.Value.ToArgb() == Lane2.ToArgb() ? 1 : 0; if (laneBg.HasValue) gk = Sprites.H(gk, laneBg.Value.ToArgb()); var gridStrip = gridStrips[si];
            if (gridStrip == null || gridStripKeys[si] != gk)
            {
                if (gridStrip == null || gridStrip.Width != sw || gridStrip.Height != sh) { if (gridStrip != null) gridStrip.Dispose(); gridStrip = Sprites.Make(sw, sh); gridStrips[si] = gridStrip; }
                using (var sg = Graphics.FromImage(gridStrip)) { sg.Clear(laneBg.HasValue ? laneBg.Value : Color.Transparent); sg.SmoothingMode = SmoothingMode.None; using (var thin = new Pen(TM.C((int)((46 + boost) * dim), 190, 150, 255))) using (var thick = new Pen(TM.C((int)((96 + boost) * dim), 210, 170, 255), 1.2f))
                    for (double f = first0(gf); ; f += gf) { float x = (float)((f - scroll) / spp); if (x > tl.Width) break; if (x < 0) continue; bool isBar = Math.Abs(f / bar - Math.Round(f / bar)) < 0.0005; sg.DrawLine(isBar ? thick : thin, x, 0, x, sh); } }
                gridStripKeys[si] = gk;
            }
            if (laneBg.HasValue) g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(gridStrip, (int)Math.Round(tl.X), (int)Math.Round(top)); g.CompositingMode = CompositingMode.SourceOver; return;
        }
        if (laneBg.HasValue) using (var lb = new SolidBrush(laneBg.Value)) g.FillRectangle(lb, tl.X, top, tl.Width, bottom - top);
        long first = (long)(Math.Floor(scroll / gf) * gf); var gridState = g.Save(); g.SetClip(Rc(tl.X, top, tl.Width, bottom - top), CombineMode.Intersect); var smoothWas = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;   // straight vertical lines need no anti-aliasing (it made the grid the slowest part of a frame)
        using (var thin = new Pen(TM.C((int)((46 + boost) * dim), 190, 150, 255))) using (var thick = new Pen(TM.C((int)((96 + boost) * dim), 210, 170, 255), 1.2f))
            for (double f = first; ; f += gf)
            {
                float x = tl.X + (float)((f - scroll) / spp); if (x > tl.Right) break; if (x < tl.X) continue;
                bool isBar = Math.Abs(f / bar - Math.Round(f / bar)) < 0.0005; g.DrawLine(isBar ? thick : thin, x, top, x, bottom);
            }
        g.SmoothingMode = smoothWas; g.Restore(gridState);
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
        var n = StudioEngine.NewTrack(t.Name + suffix, t.Role, t.Clip); n.SectionLabel = t.SectionLabel; n.PieceMuted = t.PieceMuted; n.Instrument = t.Instrument; n.Offset = t.Offset; n.ClipStart = t.ClipStart; n.ClipFrames = t.ClipFrames; n.Color = t.Color; n.VolDb = t.VolDb; n.Pan = t.Pan; n.Mute = t.Mute; n.Source = t.Source; n.Bpm = t.Bpm; n.BpmChecked = t.BpmChecked; n.FadeIn = t.FadeIn; n.FadeOut = t.FadeOut; n.FadeInDelay = t.FadeInDelay; n.FadeOutTail = t.FadeOutTail; n.Mono = t.Mono; n.TrimDb = t.TrimDb; n.Invert = t.Invert; n.Sends = Array.ConvertAll(t.Sends, x => x.Copy()); n.ClipGainDb = t.ClipGainDb;
        lock (t.Midi) foreach (var m in t.Midi) n.Midi.Add(new MidiEv { Frame = m.Frame, S = m.S, D1 = m.D1, D2 = m.D2 }); n.MClips = t.MClips.ConvertAll(c => c.Copy());
        foreach (var f in t.Fx) { var c = CloneFx(f); if (c != null) { var ic = c as InstrumentFx; if (ic != null) ic.Owner = n; n.Fx.Add(c); } }
        return n;
    }
    // COPY / PASTE of a piece of audio: right-click a piece, Copy; right-click any audio track where you want it, Paste here (it lands at the spot you clicked)
    // what was copied: one piece, or every highlighted section (each keeps its distance in time and its row distance from the first)
    class ClipCopyItem { public Track Copy; public long Rel; public int Row; }
    readonly List<ClipCopyItem> clipCopies = new List<ClipCopyItem>();
    Track clipCopy { get { return clipCopies.Count > 0 ? clipCopies[0].Copy : null; } }
    internal void CopyClip(Track piece)
    {
        var picks = new List<Track>();
        if (piece != null && InGroup(piece)) { foreach (var o in multiSel) if (o.Clip != null && !o.Instrument && eng.Tracks.Contains(o)) picks.Add(o); }
        else if (piece != null && piece.Clip != null && !piece.Instrument) picks.Add(piece);
        if (picks.Count == 0) { Toast("Copy works on a piece of audio: click the piece (or highlight several), then Ctrl+C."); return; }
        long t0 = long.MaxValue; int r0 = int.MaxValue; foreach (var p in picks) { t0 = Math.Min(t0, p.Offset); r0 = Math.Min(r0, eng.Tracks.IndexOf(p.Host ?? p)); }
        clipCopies.Clear(); foreach (var p in picks) clipCopies.Add(new ClipCopyItem { Copy = CloneTrack(p, ""), Rel = p.Offset - t0, Row = eng.Tracks.IndexOf(p.Host ?? p) - r0 });
        Toast(picks.Count == 1 ? "Copied. Ctrl+V pastes it at the needle on the picked track." : "Copied " + picks.Count + " sections. Ctrl+V pastes them at the needle, starting on the picked track (they keep their spacing).");
    }
    // Ctrl+C: the highlighted sections, or the picked piece
    void CopyKey() { if (multiSel.Count > 0) { var f = multiSel.Find(o => o.Clip != null && !o.Instrument && eng.Tracks.Contains(o)); if (f != null) { CopyClip(f); return; } } CopyClip(CurClip()); }
    // Ctrl+X: copy what is highlighted (or the picked piece), then take it off the timeline (Ctrl+V puts it back, Ctrl+Z undoes)
    internal void CutKey()
    {
        var before = clipCopies.Count > 0 ? clipCopies[0].Copy : null; CopyKey();
        if (clipCopies.Count == 0 || clipCopies[0].Copy == before) return;   // nothing could be copied: nothing is deleted
        int n = clipCopies.Count; if (multiSel.Count > 0) DeleteSelectedClips(); else { var c = CurClip() ?? Selected; if (c != null && c.Clip != null) DeleteClip(c); }
        Toast(n == 1 ? "Cut. Ctrl+V pastes it at the needle on the picked track." : "Cut " + n + " sections. Ctrl+V pastes them at the needle (they keep their spacing).");
    }
    // Ctrl+V: at the needle, on the picked track
    void PasteKey() { var row = Selected; PasteClip(row, Math.Max(0, (long)DispPos)); }
    bool undoPushed;
    internal void PasteClip(Track tr, long at)
    {
        if (clipCopies.Count == 0) { Toast("Nothing is copied yet: click a piece (or highlight several) and press Ctrl+C."); return; }
        var host = tr == null ? null : (tr.Host ?? tr);
        if (host == null && !eng.Tracks.Exists(t => !t.IsBus && !t.Instrument && t.Host == null)) { PushUndo(); host = EmptyAudioTrack(); lock (eng.Sync) eng.Tracks.Insert(LaneCount, host); undoPushed = true; }   // nothing to paste onto (for example everything was cut): a new audio track
        if (host == null || host.IsBus || host.Instrument) { Toast("Paste works on audio tracks: click an audio track first."); return; }
        if (IsRowLocked(host)) { Toast(FrozenWhy(host, "Locked: press UNLOCK before pasting on this track.")); return; }   // (a locked section on the track does not stop a paste: nothing on it moves)
        if (!undoPushed) PushUndo(); undoPushed = false; int baseRow = eng.Tracks.IndexOf(host); var made = new List<Track>(); int newTracks = 0;
        foreach (var it in clipCopies)
        {
            int rowI = baseRow + it.Row; Track rowT = rowI < LaneCount ? eng.Tracks[rowI] : null;
            if (rowT != null && (rowT.IsBus || rowT.Instrument || IsRowLocked(rowT))) rowT = null;   // a bus, an instrument or a locked track in the way: the section goes on a new track instead
            if (rowT == null) { rowT = EmptyAudioTrack(); lock (eng.Sync) eng.Tracks.Insert(LaneCount, rowT); newTracks++; }
            var n = CloneTrack(it.Copy, " (copy)"); n.Offset = Math.Max(0, at + it.Rel);
            if (rowT.Clip == null)
            {   // an empty track: the piece becomes its audio
                rowT.Clip = n.Clip; rowT.Offset = n.Offset; rowT.ClipStart = n.ClipStart; rowT.ClipFrames = n.ClipFrames; rowT.ClipGainDb = n.ClipGainDb; rowT.SectionLabel = n.SectionLabel; rowT.PieceMuted = n.PieceMuted; rowT.FadeIn = n.FadeIn; rowT.FadeOut = n.FadeOut; rowT.FadeInDelay = n.FadeInDelay; rowT.FadeOutTail = n.FadeOutTail; rowT.Bpm = n.Bpm; rowT.BpmChecked = n.BpmChecked; if (rowT.Role == "audio" || rowT.Role == null || rowT.Role == "") rowT.Role = n.Role; toolsTrack = host;
                made.Add(rowT);
            }
            else { n.Host = rowT; n.Out = rowT.Out; lock (eng.Sync) eng.Tracks.Insert(FirstBusIndex(), n); made.Add(n); }
        }
        selTrack = Math.Max(0, eng.Tracks.IndexOf(host)); selFx = -1; toolsTrack = made[0]; multiSel.Clear(); if (made.Count > 1) multiSel.AddRange(made);
        Toast(made.Count == 1 ? "Pasted. It plays through this track's plugins." : "Pasted " + made.Count + " sections" + (newTracks > 0 ? " (" + newTracks + " new track" + (newTracks == 1 ? "" : "s") + ")" : "") + ". Ctrl+Z undoes it.");
    }
    void InsertAfter(Track after, Track n) { n.Host = after.Host; n.Out = after.Out; lock (eng.Sync) { int i = eng.Tracks.IndexOf(after); eng.Tracks.Insert(i < 0 ? FirstBusIndex() : i + 1, n); } selTrack = Math.Max(0, eng.Tracks.IndexOf(n.Host ?? n)); selFx = -1; }   // a copy of a layer stays a layer; rows stay before layers and buses

    void SplitTrack(Track t, long at)
    {
        if (t == null) { Toast("Select a track first."); return; }
        if (t.Clip == null || t.Instrument) { Toast("Splitting works on audio tracks."); return; }
        if (IsLocked(t)) { Toast(FrozenWhy(t, "Locked: press UNLOCK before splitting this track.")); return; }
        long rel = at - t.Offset, used = t.UsedLength; if (rel < 64 || rel > used - 64) {   /* (anywhere inside the clip: only the first and last 1.5 ms are off limits, so neither half is empty) */ Toast("Move the needle into the clip first (click the ruler above the tracks), then split."); return; }
        PushUndo(); var host = t.Host ?? t; var right = CloneTrack(t, " (2)"); { string baseName = System.Text.RegularExpressions.Regex.Replace(t.Name, @"( (d+))+$", ""); int cnt = 1; foreach (var q in eng.Tracks) if ((q.Host ?? q) == host && q.Clip != null && !q.Instrument) cnt++; right.Name = baseName + " (" + cnt + ")"; } /* the pieces are Vox (2), Vox (3)... not Vox (2) (2) (2) */ right.Offset = at; right.ClipStart = t.ClipStart + rel; right.ClipFrames = used - rel; t.ClipFrames = rel;
        right.FadeIn = 0; right.FadeInDelay = 0; right.FadeOut = t.FadeOut; right.FadeOutTail = t.FadeOutTail; t.FadeOut = 0; t.FadeOutTail = 0;   // the fade out belongs to the end of the clip, which is now the right piece
        right.Host = host; right.Out = host.Out; lock (eng.Sync) eng.Tracks.Insert(FirstBusIndex(), right);   // the new piece stays on the same row: no new track is made
        selTrack = Math.Max(0, eng.Tracks.IndexOf(host)); toolsTrack = right; Toast("Split in two on the same track. Split as often as you like. Drag a piece to move it, drag its ends to trim.");
    }
    // DUPLICATE on a piece: a copy of exactly that piece (not the whole track) on a new track right under its track, at the same time, with that track's plugins and settings
    internal void DuplicatePiece(Track piece)
    {
        if (piece == null || piece.Clip == null) return; var host = piece.Host ?? piece; PushUndo();
        var n = CloneTrack(host, ""); n.Name = piece.Name + " copy"; n.Clip = piece.Clip; n.Source = piece.Source; n.Offset = piece.Offset; n.ClipStart = piece.ClipStart; n.ClipFrames = piece.ClipFrames; n.SectionLabel = piece.SectionLabel; n.PieceMuted = piece.PieceMuted; n.ClipGainDb = piece.ClipGainDb; n.FadeIn = piece.FadeIn; n.FadeOut = piece.FadeOut; n.FadeInDelay = piece.FadeInDelay; n.FadeOutTail = piece.FadeOutTail; n.Host = null; n.Locked = false;
        lock (eng.Sync) { int i = eng.Tracks.IndexOf(host); eng.Tracks.Insert(i < 0 ? LaneCount : i + 1, n); }
        selTrack = Math.Max(0, eng.Tracks.IndexOf(n)); selFx = -1; toolsTrack = n; Toast("Duplicated that piece onto a new track under " + host.Name + ".");
    }
    void DuplicateTrack(Track t)
    {
        if (t == null) return; PushUndo(); var n = CloneTrack(t, " copy"); InsertAfter(t, n); Toast("Duplicated");
    }
    void DeleteTrackQuiet(Track t)
    {
        if (t == null) return; if (toolsTrack == t) toolsTrack = null; PushUndo();
        Track keep = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null; var removedLayers = new List<Track>();
        lock (eng.Sync) { eng.Tracks.Remove(t); retired.AddRange(t.Fx); if (t.IsBus) foreach (var x in eng.Tracks) { if (x.Out == t) x.Out = null; if (Array.Exists(x.Sends, s => s.Bus == t)) x.Sends = Array.FindAll(x.Sends, s => s.Bus != t); }   /* nothing is left pointing at a deleted bus (outputs go to the master, sends to it go) */ foreach (var lay in eng.Tracks.FindAll(x => x.Host == t)) { eng.Tracks.Remove(lay); retired.AddRange(lay.Fx); removedLayers.Add(lay); } }
        // keep the same track selected by object (a row above it may be gone), never a layer or bus after the lanes
        int k = keep != null ? eng.Tracks.IndexOf(keep) : -1; selTrack = k >= 0 ? k : selTrack; selTrack = Math.Max(0, Math.Min(selTrack, LaneCount - 1)); selFx = -1;
        foreach (var f in t.Fx) { var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }
        foreach (var lay in removedLayers) foreach (var f in lay.Fx) { var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }   // an instrument layer's window would otherwise stay open on a track that is gone
        Toast("Deleted (Undo brings it back)");
    }
    void DisposeRetired() { foreach (var f in retired) { var d = f as IDisposable; if (d != null) { try { d.Dispose(); } catch { } } } retired.Clear(); }

    // ---------- mouse on a lane: seek, move the clip (name bar), trim (ends) ----------
    static List<MidiEv> CopyMidi(Track t) { if (!t.Instrument || t.Midi.Count == 0) return null; var l = new List<MidiEv>(t.Midi.Count); lock (t.Midi) foreach (var m in t.Midi) l.Add(new MidiEv { Frame = m.Frame, S = m.S, D1 = m.D1, D2 = m.D2 }); return l; }
    Track toolsTrack;   // the track you clicked: only its button row and grips are shown
    int laneMode; bool laneMoved; PointF laneStart; long l0Offset, l0ClipStart, l0ClipFrames, l0Used;
    // 0 = empty space (seek), 1 = on the clip (move), 2 = left end (trim start), 3 = right end (trim end)
    int ZoneAt(Track tr, RectangleF lane, PointF p)
    {
        if (tr.Clip == null || tr.Instrument || !lane.Contains(p)) return 0;
        float x0 = lane.X + (float)((tr.Offset - scroll) / spp), x1 = x0 + (float)(tr.UsedLength / spp), w = x1 - x0;
        if (w > 40 && toolsTrack == tr)   // the round fade handles of the clip you selected: the top end of each fade (its length) and the bottom end (where it starts or stops)
        {
            float dIn = (float)(tr.FadeInDelay / spp), dOut = (float)(tr.FadeOutTail / spp), by = lane.Bottom - 12;
            if (Math.Abs(p.Y - (lane.Y + 12)) <= 11)
            {
                if (Math.Abs(p.X - (x0 + dIn + Math.Max(12f, (float)(tr.FadeIn / spp)))) <= 11) return 4;
                if (Math.Abs(p.X - (x1 - dOut - Math.Max(12f, (float)(tr.FadeOut / spp)))) <= 11) return 5;
            }
            if (Math.Abs(p.Y - by) <= 11)
            {
                if (tr.FadeIn > 0 && Math.Abs(p.X - (x0 + dIn)) <= 11) return 6;
                if (tr.FadeOut > 0 && Math.Abs(p.X - (x1 - dOut)) <= 11) return 7;
            }
        }
        if (w > 20 && Math.Abs(p.X - x0) <= 8) return 2; if (w > 20 && Math.Abs(p.X - x1) <= 8) return 3; if (p.X > x0 && p.X < x1 && p.Y > lane.Y + 4 && p.Y < lane.Bottom - 4) return 1; return 0;
    }
    static readonly string[] ZoneText = { "Click: select this track", "Drag: move this clip", "Drag: trim the start", "Drag: trim the end", "Drag: how long the fade in lasts", "Drag: how long the fade out lasts", "Drag: where the fade in starts (silence before it)", "Drag: where the fade out ends (silence after it)" };
    internal Cursor CursorAt(PointF p)
    {
        foreach (var ph in popRects) if (ph.Contains(p)) return Cursors.SizeAll;   // the top strip of a pop-up box: drag to move it
        if (tab != 0 || busy || browserOpen) return Cursors.Default; float top = ArrTop + RulerH - vscroll; var tl = TimelineRect();
        for (int i = 0; i < LaneCount; i++)
        {
            var lane = Rc(HeadW, top + i * LaneH, tl.Width, LaneH); if (!lane.Contains(p)) continue;
            int z = ZoneAt(eng.Tracks[i], lane, p); if (laneMode != 0 && dragReg != null) z = laneMode; return z == 1 ? Cursors.SizeAll : (z >= 2 ? Cursors.SizeWE : Cursors.Cross);
        }
        if (p.X < HeadW && p.Y > top) return Cursors.Hand; return Cursors.Default;
    }
    HitReg LaneReg(RectangleF lane, Track tr)
    {
        Track cur = tr;   // the clip under the mouse: this row's own track or one of the tracks layered on it
        var lr = Reg(lane, () => { cur = PickLayer(tr, MousePos); LaneDown(cur, lane); }); lr.OnDrag = pt => LaneDrag(cur, pt); lr.OnUp = () => { LaneDrop(cur); GhostEnd(); MarqueeEnd(); }; lr.OnRight = () => LaneMenu(tr); lr.OnDouble = () => { var pc = PickLayer(tr, MousePos); int z = ZoneAt(pc, lane, MousePos); if (z >= 4) { if (IsLocked(pc)) return; PushUndo(); if (z == 4) { pc.FadeIn = 0; pc.FadeInDelay = 0; } else if (z == 5) { pc.FadeOut = 0; pc.FadeOutTail = 0; } else if (z == 6) pc.FadeInDelay = 0; else pc.FadeOutTail = 0; Toast(z >= 6 ? "Fade start moved back to the edge of the clip" : "Fade " + (z == 4 ? "in" : "out") + " removed"); } else if (tr.Instrument) OpenRoll(tr); };
        if (Hot(lane)) hintNow = "Click a track's box to select it  \u2022  drag a clip to move it, its ends to trim  \u2022  drag on empty space to select several  \u2022  move the needle on the ruler  \u2022  right-click: split, cut, fades, gain, lock, delete";
        return lr;
    }
    // an audio row where the mouse is past the audio (or between pieces): clicking there does nothing to the selection (it used to switch to this track)
    bool OverNoAudio(Track tr, PointF p)
    {
        var host = tr.Host ?? tr; bool any = false;
        foreach (var L in eng.Tracks.ToArray()) { if ((L.Host ?? L) != host || L.Instrument || L.Clip == null) continue; any = true; float x0 = HeadW + (float)((L.Offset - scroll) / spp), x1 = x0 + (float)(L.UsedLength / spp); if (p.X >= x0 - 8 && p.X <= x1 + 8) return false; }
        return any;
    }
    void LaneDown(Track tr, RectangleF lane)
    {
        mcSel.Clear();   // a click on the row (not on a MIDI clip) lets go of the selected clips
        if (OverNoAudio(tr, MousePos)) { laneMode = 0; laneStart = MousePos; laneLocked = false; toolsTrack = null; MarqueeBegin(); return; }   // empty part of the row: no change of track; hold and drag a box there to select several clips
        laneMode = 0; laneStart = MousePos; toolsTrack = tr; marqueeOn = false;   // the selected track changes only from its box on the left (the header): clicking the audio moves, trims and edits the clip but leaves the selection alone
        if (tr.Clip == null && !tr.Instrument && !tr.IsBus) { laneLocked = false; toolsTrack = null; MarqueeBegin(); return; }   // an empty row: hold and drag a box to select clips, like any empty space
        if (tr.Clip != null && !tr.Instrument)
        {
            laneMode = ZoneAt(tr, lane, MousePos); laneMoved = false; lockToldYou = false; laneLocked = IsLocked(tr) && laneMode >= 1 && laneMode <= 3;
            if (laneLocked) return;   // clicking a locked clip selects it, but it will not move (a message tells you when you try)
            if (laneMode >= 2) multiSel.Clear();
            if (laneMode >= 4) { PushUndo(); laneMoved = true; return; }
            if (laneMode >= 2) { PushUndo(); laneMoved = true; l0Offset = tr.Offset; l0ClipStart = tr.ClipStart; l0ClipFrames = tr.ClipFrames; l0Used = tr.UsedLength; return; }
            if (laneMode == 1) { if (!multiSel.Contains(tr)) multiSel.Clear(); groupOrig.Clear(); if (InGroup(tr)) foreach (var o in multiSel) groupOrig[o] = o.Offset; GhostGrab(tr); l0Offset = tr.Offset; l0ClipStart = tr.ClipStart; l0ClipFrames = tr.ClipFrames; l0Used = tr.UsedLength; return; }   // clicking a clip selects it, dragging moves it: the needle stays where it is
        }
    }
    void LaneDrag(Track tr, PointF pt)
    {
        if (laneMode == 0) { MarqueeTo(pt); return; }
        if (laneLocked) { if (!lockToldYou && (Math.Abs(pt.X - laneStart.X) > 4 || Math.Abs(pt.Y - laneStart.Y) > 4)) { lockToldYou = true; Toast(FrozenWhy(tr, "This track is locked in place. Press UNLOCK in the toolbar (or right-click it) to move it.")); } return; }
        if (laneMode >= 4)
        {
            long usedL = tr.UsedLength; float fx0 = HeadW + (float)((tr.Offset - scroll) / spp), fx1 = fx0 + (float)(usedL / spp);
            if (laneMode == 4) { long v = (long)Math.Max(0, (pt.X - fx0) * spp) - tr.FadeInDelay; if (v < 1500) v = 0; tr.FadeIn = Math.Max(0, Math.Min(v, usedL - tr.FadeOut - tr.FadeInDelay - tr.FadeOutTail - 500)); }
            else if (laneMode == 5) { long v = (long)Math.Max(0, (fx1 - pt.X) * spp) - tr.FadeOutTail; if (v < 1500) v = 0; tr.FadeOut = Math.Max(0, Math.Min(v, usedL - tr.FadeIn - tr.FadeInDelay - tr.FadeOutTail - 500)); }
            else if (laneMode == 6) { long v = (long)Math.Max(0, (pt.X - fx0) * spp); if (v < 1500) v = 0; tr.FadeInDelay = Math.Max(0, Math.Min(v, usedL - tr.FadeIn - tr.FadeOut - tr.FadeOutTail - 500)); }
            else { long v = (long)Math.Max(0, (fx1 - pt.X) * spp); if (v < 1500) v = 0; tr.FadeOutTail = Math.Max(0, Math.Min(v, usedL - tr.FadeIn - tr.FadeOut - tr.FadeInDelay - 500)); }
            return;
        }
        long delta = (long)((pt.X - laneStart.X) * spp);
        if (laneMode == 1) { if (!laneMoved) { if (Math.Abs(pt.X - laneStart.X) < 2 && Math.Abs(pt.Y - laneStart.Y) < 2) return; PushUndo(); laneMoved = true; } bool grp = InGroup(tr); GhostStart(tr, 0);   /* the landing row shows for a group too */ tr.Offset = Math.Max(0, l0Offset + delta);   /* free: the clip goes exactly as far as you drag (no jumping to grid lines) */ if (grp) { long nd = tr.Offset - l0Offset; foreach (var o in multiSel) { long b0; if (o != tr && !IsLocked(o) && groupOrig.TryGetValue(o, out b0)) o.Offset = Math.Max(0, b0 + nd); } } return; }
        long avail = Math.Max(0, tr.Clip.Length - l0ClipStart);
        if (laneMode == 2)
        {
            long newStart = l0Offset + delta; long d = newStart - l0Offset;   /* trimming is free too */ d = Math.Max(-l0ClipStart, Math.Min(l0Used - 1000, d)); if (l0Offset + d < 0) d = -l0Offset;
            lock (eng.Sync) { tr.Offset = l0Offset + d; tr.ClipStart = l0ClipStart + d; tr.ClipFrames = l0ClipFrames < 0 ? -1 : l0Used - d; } ClampFades(tr);
        }
        else
        {
            long end = l0Offset + l0Used + delta; long used = Math.Max(1000, Math.Min(avail, end - l0Offset)); tr.ClipFrames = used >= avail ? (l0ClipFrames < 0 ? -1 : used) : used; ClampFades(tr);
        }
    }
    void LaneMenu(Track tr)
    {
        selTrack = Math.Max(0, eng.Tracks.IndexOf(tr.Host ?? tr)); var m = Win.NewMenu(); long at = SplitPoint(Math.Max(0, scroll + (long)((MousePos.X - HeadW) * spp)));
        if (tr.Instrument) { m.Items.Add("Edit notes (piano roll)...").Click += (a, b) => OpenPianoRoll(tr); m.Items.Add(new ToolStripSeparator()); }
        var piece = PickLayer(tr, MousePos);
        if (tr.Clip == null && !tr.Instrument && !tr.IsBus) { var tk = tr; m.Items.Add("Add music to this track...").Click += (a, b) => { selTrack = Math.Max(0, eng.Tracks.IndexOf(tk)); fillTrack = tk; try { AddMusicClick(true); } finally { fillTrack = null; } }; m.Items.Add(new ToolStripSeparator()); }
        m.Items.Add("Split here (at the needle)").Click += (a, b) => SplitSelected();
        m.Items.Add("Split where I clicked").Click += (a, b) => SplitTrack(piece, at);
        m.Items.Add("Cut this piece out (delete it, nothing else moves)").Click += (a, b) => { toolsTrack = piece; CutClick(); };
        m.Items.Add("Fade in: add / remove").Click += (a, b) => { toolsTrack = piece; ToggleFade(true); };
        m.Items.Add("Fade out: add / remove").Click += (a, b) => { toolsTrack = piece; ToggleFade(false); };
        m.Items.Add("Gain: the volume of this piece...").Click += (a, b) => { toolsTrack = piece; gainAnchor = Rc(MousePos.X - 40, MousePos.Y, 80, 10); OpenGain(); };
        m.Items.Add((tr.Host ?? tr).Locked ? "Unlock this track" : "Lock this track in place").Click += (a, b) => ToggleLock();
        if (piece != null && piece.Clip != null && !piece.Instrument)
        {
            if (InGroup(piece)) { var grp = multiSel.FindAll(x => x.Clip != null && !x.Instrument && eng.Tracks.Contains(x)); bool allL = grp.TrueForAll(x => x.PieceLocked); m.Items.Add((allL ? "Unlock these " : "Lock these ") + grp.Count + " sections").Click += (a, b) => SetPieceLocks(grp, !allL); }   // several clips picked: lock (or unlock) every one of them
            else { var pk = piece; m.Items.Add(pk.PieceLocked ? "Unlock this section" : "Lock this section only (the rest of the track stays editable)").Click += (a, b) => TogglePieceLock(pk); }
            if (InGroup(piece)) { var grpM = multiSel.FindAll(x => x.Clip != null && !x.Instrument && eng.Tracks.Contains(x)); bool allM = grpM.TrueForAll(x => x.PieceMuted); m.Items.Add((allM ? "Unmute these " : "Mute these ") + grpM.Count + " sections").Click += (a, b) => SetPieceMutes(grpM, !allM); }   // several clips picked: mute (or unmute) every one of them
            else { var pm = piece; m.Items.Add(pm.PieceMuted ? "Unmute this section" : "Mute this section only (the rest of the track still plays)").Click += (a, b) => TogglePieceMute(pm); }
            { var gl2 = InGroup(piece) ? multiSel.FindAll(x => x.Clip != null && !x.Instrument && eng.Tracks.Contains(x)) : new List<Track> { piece }; var cm = new ToolStripMenuItem(gl2.Count > 1 ? "Censor these " + gl2.Count + " sections" : "Censor this section (bleep a word)");
              for (int ci = 0; ci < CensorFx.Modes.Length; ci++) { int cc = ci; cm.DropDownItems.Add(CensorFx.Modes[ci]).Click += (a, b) => CensorSections(gl2, cc); }
              cm.DropDownItems.Add(new ToolStripSeparator()); cm.DropDownItems.Add("Uncensor").Click += (a, b) => UncensorSections(gl2); m.Items.Add(cm); }
            { var gl = InGroup(piece) ? multiSel.FindAll(x => x.Clip != null && !x.Instrument && eng.Tracks.Contains(x)) : new List<Track> { piece }; var gm = new ToolStripMenuItem(gl.Count > 1 ? "Gain of these " + gl.Count + " sections" : "Section gain (now " + (piece.ClipGainDb > 0 ? "+" : "") + piece.ClipGainDb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " dB)");
              foreach (var d in new[] { 6f, 3f, 1f, -1f, -3f, -6f }) { float dd = d; gm.DropDownItems.Add((dd > 0 ? "+" : "") + dd + " dB").Click += (a, b) => NudgeSectionGain(gl, dd); }
              gm.DropDownItems.Add(new ToolStripSeparator()); gm.DropDownItems.Add("Set to 0 dB (as recorded)").Click += (a, b) => SetSectionGain(gl, 0f); gm.DropDownItems.Add("Set to -12 dB (quiet backing)").Click += (a, b) => SetSectionGain(gl, -12f);
              gm.DropDownItems.Add("Set exactly...").Click += (a, b) => { selTrack = eng.Tracks.IndexOf(piece); OpenGain(); }; m.Items.Add(gm); }
            if (InGroup(piece)) { var grpN = multiSel.FindAll(x => x.Clip != null && !x.Instrument && eng.Tracks.Contains(x)); m.Items.Add("Rename these " + grpN.Count + " sections...").Click += (a, b) => RenameSections(grpN); }
            else { var pn = piece; m.Items.Add(pn.SectionLabel.Length > 0 ? "Rename this section (" + pn.SectionLabel + ")..." : "Rename this section (Yeahs, Adlibs, Hook...)").Click += (a, b) => RenameSections(new List<Track> { pn }); }
        }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(InGroup(piece) ? "Copy these " + multiSel.Count + " sections  (Ctrl+C)" : "Copy this piece  (Ctrl+C)").Click += (a, b) => CopyClip(piece);
        { var pasteIt = m.Items.Add(clipCopies.Count > 1 ? "Paste the " + clipCopies.Count + " sections here  (Ctrl+V)" : "Paste here  (Ctrl+V)"); pasteIt.Enabled = clipCopy != null; pasteIt.Click += (a, b) => PasteClip(tr, at); }
        if (piece != null && piece.Clip != null && !piece.Instrument) m.Items.Add("Duplicate this piece (onto a new track under this one)").Click += (a, b) => DuplicatePiece(piece);
        m.Items.Add("Duplicate the whole track").Click += (a, b) => DuplicateTrack(tr);
        m.Items.Add("Rename...  (F2)").Click += (a, b) => RenameTrack(tr);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // what the mouse is over on a clip: edge grips and a highlight, plus a label next to the cursor
    string bubble = ""; PointF bubblePt;
    void PaintClipChrome(Graphics g, Track tr, RectangleF lane, RectangleF cr, bool sel)
    {
        if (cr.Width < 6 || tr.Instrument) return; bool over = lane.Contains(MousePos) && hover != null && hover.R == lane; int zone = over ? (dragReg != null && laneMode != 0 ? laneMode : ZoneAt(tr, lane, MousePos)) : -1; if (zone == 0 && OverNoAudio(tr, MousePos)) zone = -1;   // past the audio nothing is clickable: no label either
        if (!(toolsTrack == tr || over)) return; var c = Color.FromArgb(tr.Color);
        if (zone == 1) { using (var b = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillRectangle(b, cr); StrokeRR(g, Color.FromArgb(230, 255, 255, 255), cr, 6, 1.6f); }
        float gh = Math.Min(46, cr.Height - 16), gy = cr.Y + (cr.Height - gh) / 2; bool leftEdge = cr.X > lane.X - 1, rightEdge = cr.Right < lane.Right - 1;
        bool lockedClip = IsLocked(tr); float gw = cr.Width < 24 ? 0f : Math.Min(9f, Math.Max(3f, cr.Width / 5f));   // a narrow clip gets thin grips (none when it is tiny): they never cover the clip
        if (toolsTrack == tr && cr.Width > 40)
        {   // the two round fade handles
            float hx0 = lane.X + (float)((tr.Offset - scroll) / spp), hx1 = hx0 + (float)(tr.UsedLength / spp), hy = lane.Y + 12, bhy = lane.Bottom - 12, dIn = (float)(tr.FadeInDelay / spp), dOut = (float)(tr.FadeOutTail / spp);
            foreach (var hz in new[] { 4, 5, 6, 7 })
            {
                if (hz == 6 && tr.FadeIn <= 0) continue; if (hz == 7 && tr.FadeOut <= 0) continue;
                float hx = hz == 4 ? hx0 + dIn + Math.Max(12f, (float)(tr.FadeIn / spp)) : (hz == 5 ? hx1 - dOut - Math.Max(12f, (float)(tr.FadeOut / spp)) : (hz == 6 ? hx0 + dIn : hx1 - dOut)); float hyy = hz >= 6 ? bhy : hy; if (hx < lane.X - 10 || hx > lane.Right + 10) continue; bool hv = zone == hz; float rr = hv ? 7.5f : (hz >= 6 ? 5f : 6f);
                using (var b = new SolidBrush(Color.FromArgb(hv ? 255 : 220, hv ? Color.White : (hz >= 6 ? TM.C(255, 255, 70, 225) : TM.C(255, 0, 229, 255))))) g.FillEllipse(b, hx - rr, hyy - rr, rr * 2, rr * 2);
                using (var pen = new Pen(TM.C(230, 10, 5, 25), 1.5f)) g.DrawEllipse(pen, hx - rr, hyy - rr, rr * 2, rr * 2);
            }
        }
        if (gw > 0 && !lockedClip && (leftEdge || zone == 2)) { var r = Rc(cr.X + 1, gy, gw, gh); FillRR(g, Color.FromArgb(zone == 2 ? 250 : 175, zone == 2 ? Color.White : c), r, 4); Txt(g, "<", 11, TM.C(255, 10, 4, 20), r, StringAlignment.Center, true); }
        if (gw > 0 && !lockedClip && (rightEdge || zone == 3)) { var r = Rc(cr.Right - 1 - gw, gy, gw, gh); FillRR(g, Color.FromArgb(zone == 3 ? 250 : 175, zone == 3 ? Color.White : c), r, 4); Txt(g, ">", 11, TM.C(255, 10, 4, 20), r, StringAlignment.Center, true); }
        if (zone >= 0 && zone < ZoneText.Length) { bubble = ZoneText[zone] + (zone == 4 ? "  (" + (tr.FadeIn / (double)StudioEngine.SR).ToString("0.0") + " s, double-click removes it)" : (zone == 5 ? "  (" + (tr.FadeOut / (double)StudioEngine.SR).ToString("0.0") + " s, double-click removes it)" : (zone == 6 ? "  (" + (tr.FadeInDelay / (double)StudioEngine.SR).ToString("0.0") + " s of silence first, double-click resets)" : (zone == 7 ? "  (" + (tr.FadeOutTail / (double)StudioEngine.SR).ToString("0.0") + " s of silence at the end, double-click resets)" : "")))); bubblePt = MousePos; }
    }
    // ADD MUSIC asks first: music from your files, or an empty track (an empty vocal track or an empty audio track)
    internal void AddMusicMenu(RectangleF at)
    {
        var m = Win.NewMenu();
        m.Items.Add("Pick music from your computer...").Click += (a, b) => AddMusicClick();
        m.Items.Add("Add an empty vocal track (no music)").Click += (a, b) => AddEmptyTrack("vocal");
        m.Items.Add("Add an empty audio track (no music)").Click += (a, b) => AddEmptyTrack("audio");
        m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    internal Track AddEmptyTrack(string role)
    {
        PushUndo(); var t = EmptyAudioTrack(); if (role == "vocal") { int k = 1; foreach (var x in eng.Tracks) if (x.Role == "vocal" && x.Host == null) k++; t.Name = "Vocal " + k; t.Role = "vocal"; }
        lock (eng.Sync) eng.Tracks.Insert(LaneCount, t); selTrack = Math.Max(0, eng.Tracks.IndexOf(t)); selFx = -1; welcomeOn = false; Toast("Added " + t.Name + ": an empty track. Drag audio onto it, drop files on it, record with MIC, or right-click it to add music."); return t;
    }
    Track EmptyAudioTrack()
    {
        int k = 1; foreach (var x in eng.Tracks) if (!x.IsBus && !x.Instrument) k++;
        return StudioEngine.NewTrack("Audio " + k, "audio", null);
    }
    // letting go of a dragged clip on another row moves it there. On a row that already has something, the clip joins that row as a layer (nothing is pushed up or down); below the last row a new track is made.
    Track PickLayer(Track host, PointF p)
    {
        Track best = host; foreach (var L in eng.Tracks) if (L.Host == host && !L.Instrument && L.Clip != null) { float x0 = HeadW + (float)((L.Offset - scroll) / spp), x1 = x0 + (float)(L.UsedLength / spp); if (p.X >= x0 - 8 && p.X <= x1 + 8) best = L; }
        return best;
    }
    void LaneDrop(Track src)
    {
        if (laneMode != 1 || !laneMoved || src.Clip == null || src.Instrument) return;
        float top = ArrTop + RulerH - vscroll; int row = (int)Math.Floor((MousePos.Y - top) / LaneH), fromRow = eng.Tracks.IndexOf(src.Host ?? src);
        if (row < 0 || fromRow < 0 || row == fromRow || Math.Abs(MousePos.Y - laneStart.Y) < LaneH * 0.5f) return;
        if (InGroup(src)) { GroupToRows(row - fromRow); return; }   // several clips picked: they all go up or down together
        PushUndo(); bool occupied; var dst = MoveClipToRow(src, row, out occupied);
        selTrack = Math.Max(0, eng.Tracks.IndexOf(dst.Host ?? dst)); toolsTrack = dst; Toast(occupied ? "Added to the row of " + (dst.Host != null ? dst.Host.Name : dst.Name) + " (its + chip separates them)" : "Moved to " + dst.Name);
    }
    // one clip onto row number 'row' (a new track below the last one): on a row that has something it joins as a piece, on an empty row it becomes the row's audio. Its fades and gain go with it.
    Track MoveClipToRow(Track src, int row, out bool occupied)
    {
        Track dst = row < LaneCount ? eng.Tracks[row] : null;
        occupied = dst != null && (dst.Clip != null || dst.Instrument);
        if (dst == null) { dst = EmptyAudioTrack(); lock (eng.Sync) eng.Tracks.Insert(LaneCount, dst); occupied = false; }
        if (occupied)
        {
            var L = EmptyAudioTrack(); L.Name = src.Name; L.Role = src.Role; L.Color = src.Color; L.Host = dst; L.Out = dst.Out; L.Clip = src.Clip; L.Source = src.Source; L.ClipStart = src.ClipStart; L.ClipFrames = src.ClipFrames; L.Offset = src.Offset;
            L.FadeIn = src.FadeIn; L.FadeOut = src.FadeOut; L.FadeInDelay = src.FadeInDelay; L.FadeOutTail = src.FadeOutTail; L.ClipGainDb = src.ClipGainDb; L.SectionLabel = src.SectionLabel; L.PieceMuted = src.PieceMuted;
            lock (eng.Sync) eng.Tracks.Insert(FirstBusIndex(), L); dst = L;
        }
        else lock (eng.Sync) { dst.Clip = src.Clip; dst.Source = src.Source; dst.ClipStart = src.ClipStart; dst.ClipFrames = src.ClipFrames; dst.Offset = src.Offset; dst.FadeIn = src.FadeIn; dst.FadeOut = src.FadeOut; dst.FadeInDelay = src.FadeInDelay; dst.FadeOutTail = src.FadeOutTail; dst.ClipGainDb = src.ClipGainDb; if (dst.Role == "audio" && src.Role != "audio") dst.Role = src.Role; dst.Color = src.Color; if (dst.Name.StartsWith("Audio ")) dst.Name = src.Name; }
        lock (eng.Sync)
        {
            if (src.Host != null) { eng.Tracks.Remove(src); retired.AddRange(src.Fx); }   // a piece that was moved away is simply gone from its old row
            else { src.Clip = null; src.Source = ""; src.ClipStart = 0; src.ClipFrames = -1; src.Offset = 0; src.FadeIn = 0; src.FadeOut = 0; src.FadeInDelay = 0; src.FadeOutTail = 0; src.ClipGainDb = 0; }
        }
        return dst;
    }
    // the picked clips all move the same number of rows (each keeps its place in the group); new tracks are made below the last one when needed
    internal void GroupToRows(int delta)
    {
        if (delta == 0) return; var items = new List<KeyValuePair<Track, int>>();
        foreach (var o in multiSel.ToArray()) { if (o.Clip == null || o.Instrument || IsLocked(o)) continue; int r = eng.Tracks.IndexOf(o.Host ?? o); if (r < 0) continue; items.Add(new KeyValuePair<Track, int>(o, r + delta)); }
        if (items.Count == 0) return; foreach (var it in items) if (it.Value < 0) { Toast("They cannot go higher than the first track."); return; }
        PushUndo(); int maxRow = 0; foreach (var it in items) maxRow = Math.Max(maxRow, it.Value);
        while (LaneCount <= maxRow) { var nt = EmptyAudioTrack(); lock (eng.Sync) eng.Tracks.Insert(LaneCount, nt); }   // the new tracks first, so every clip finds its row
        var moved = new List<Track>(); foreach (var it in items) { bool occ; moved.Add(MoveClipToRow(it.Key, it.Value, out occ)); }
        multiSel.Clear(); foreach (var m in moved) multiSel.Add(m); toolsTrack = moved[0];
        Toast("Moved " + moved.Count + " clips " + Math.Abs(delta) + (Math.Abs(delta) == 1 ? " track " : " tracks ") + (delta > 0 ? "down" : "up") + ". Ctrl+Z undoes it.");
    }
    // a track dragged by its header: the middle of another row = share that row, near a boundary = move between rows. Nothing moves until you let go, so it is easy to aim.
    int hdMode, hdRow, hdGap;
    void MergeRows(Track src, Track dst)
    {
        if (src == dst || src.IsBus || dst.IsBus || src.Host != null) return; PushUndo();
        lock (eng.Sync)
        {
            foreach (var x in eng.Tracks) if (x.Host == src) { x.Host = dst; x.Solo = false; }
            src.Host = dst; src.Out = dst.Out; src.Solo = false;   // a piece has no S button, so a Solo left on it could never be turned off
            eng.Tracks.Remove(src); eng.Tracks.Insert(FirstBusIndex(), src);   // pieces live after the rows: left among them, a row number pointed at the wrong track (plugins went onto another track)
        }
        selTrack = Math.Max(0, eng.Tracks.IndexOf(dst)); toolsTrack = null; Toast(src.Name + " now shares the row of " + dst.Name + ". Use the + chip on that row to separate them again.");
    }
    void LayerMenu(Track host)
    {
        var m = Win.NewMenu();
        foreach (var l0 in eng.Tracks.ToArray()) if (l0.Host == host)
        {
            var L = l0; var sub = new ToolStripMenuItem(L.Name + (L.Instrument ? "  (MIDI)" : "")); m.Items.Add(sub);
            sub.DropDownItems.Add(L.Mute ? "Unmute" : "Mute").Click += (a, b) => L.Mute = !L.Mute;
            sub.DropDownItems.Add("Move to its own row").Click += (a, b) => { PushUndo(); lock (eng.Sync) { L.Host = null; eng.Tracks.Remove(L); eng.Tracks.Insert(LaneCount, L); } };
            sub.DropDownItems.Add("Delete").Click += (a, b) => DeleteTrackQuiet(L);
        }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void MoveClip(Track tr, int dir)
    {
        if (tr.Clip == null) return; PushUndo(); double g = GridFrames;
        tr.Offset = Math.Max(0, snapOn ? (long)(Math.Round(tr.Offset / g) * g + dir * g) : tr.Offset + (long)(dir * StudioEngine.SR / 10));
    }
    void MoveTrackBy(Track tr, int dir)
    {
        if (tr.Host != null || tr.IsBus) { Toast("Drag it to another row instead."); return; }
        if (IsLocked(tr)) { Toast(FrozenWhy(tr, "Locked: press UNLOCK first.")); return; }
        int i = eng.Tracks.IndexOf(tr); if (i < 0) return; PushUndo();
        if (i + dir < 0 || i + dir >= LaneCount) { lock (eng.Sync) eng.Tracks.Insert(dir < 0 ? 0 : LaneCount, EmptyAudioTrack()); if (dir < 0) i++; }   // nothing there yet: make an empty track so it can still move
        int j = i + dir; AnimShift(tr, -dir); AnimShift(eng.Tracks[j], dir); lock (eng.Sync) { eng.Tracks.RemoveAt(i); eng.Tracks.Insert(j, tr); } selTrack = j;
    }
    // dragging a track's header: see HeaderDrag / HeaderDrop
    bool headerUndo;
    void HeaderDown(Track tr) { selTrack = Math.Max(0, eng.Tracks.IndexOf(tr)); selFx = -1; headerUndo = false; toolsTrack = tr; hdMode = 0; lockToldYou = false; if (!IsLocked(tr)) GhostGrab(tr); }
    void HeaderDrag(Track tr, PointF pt)
    {
        if (IsLocked(tr)) { if (!lockToldYou) { lockToldYou = true; Toast(FrozenWhy(tr, "This track is locked in place. Press UNLOCK in the toolbar to move it.")); } return; }
        int i = eng.Tracks.IndexOf(tr); if (i < 0 || tr.Host != null) return; GhostStart(tr, 1);
        float pos = (pt.Y - (ArrTop + RulerH - vscroll)) / LaneH; int row = (int)Math.Floor(pos); float fr = pos - row; int n = LaneCount;
        if (row >= n) { hdRow = n - 1; hdGap = n; hdMode = n - 1 == i ? 0 : 1; if (i == n - 1) hdMode = 0; return; }
        if (row < 0) { hdRow = 0; hdGap = 0; hdMode = i == 0 ? 0 : 1; return; }
        if (row == i) { hdMode = 0; return; }
        if (fr > 0.28f && fr < 0.72f) { hdMode = 2; hdRow = row; return; }
        hdMode = 1; hdGap = fr < 0.5f ? row : row + 1; int fin = hdGap > i ? hdGap - 1 : hdGap; if (fin == i) hdMode = 0;
    }
    void HeaderDrop(Track tr)
    {
        int mode = hdMode, i = eng.Tracks.IndexOf(tr); hdMode = 0; if (i < 0 || tr.Host != null || IsLocked(tr)) return;
        if (mode == 2 && hdRow >= 0 && hdRow < LaneCount) { MergeRows(tr, eng.Tracks[hdRow]); return; }
        if (mode != 1) return; int target = hdGap > i ? hdGap - 1 : hdGap; target = Math.Max(0, Math.Min(LaneCount - 1, target)); if (target == i) return; PushUndo();
        for (int k = Math.Min(i, target); k <= Math.Max(i, target); k++) if (k != i && k < eng.Tracks.Count) AnimShift(eng.Tracks[k], target > i ? 1 : -1);   // the rows we pass slide one place
        lock (eng.Sync) { eng.Tracks.RemoveAt(i); eng.Tracks.Insert(target, tr); } selTrack = target;
    }
    void PaintBubble(Graphics g)
    {
        string t = bubble; if (t.Length == 0 || sndDragging) return;   // while a sound is dragged in, its own label says what happens
        using (var f = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            var sz = g.MeasureString(t, f); float w = sz.Width + 18, h = 26; float x = Math.Min(ClientSize.Width - w - 6, bubblePt.X + 16), y = Math.Min(ClientSize.Height - h - 6, bubblePt.Y + 22); var r = Rc(x, y, w, h);
            FillRR(g, TM.C(240, 30, 16, 58), r, 8); StrokeRR(g, Accent, r, 8, 1.2f); Txt(g, t, 12, Ink, r, StringAlignment.Center, false);
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
            Fx.ProjectBpm = bpm; bpmUserSet = true; Toast("Tempo: " + bpm.ToString("0.0") + " BPM (the grid, the metronome and the synth's tempo-synced wobbles follow it)");
        });
    }

    // ---------- the strip under the transport ----------
    // ZOOM + its slider (the same one sits at the end of the editing row and under the scroll bar in Flex Pitch)
    void PaintZoomSlider(Graphics g, float x, float y, float sliderW)
    {
        Txt(g, "ZOOM", 11, DimC, Rc(x, y + 6, 36, 18), StringAlignment.Near, false); float zf = Clamp01((float)(1 - Math.Log(spp / MinSpp) / Math.Log(MaxSpp / MinSpp)));
        var zs = Slider(g, Rc(x + 38, y + 6, sliderW, 18), zf, f => ZoomTo(MinSpp * Math.Pow(MaxSpp / MinSpp, 1 - f)), NeonCyan); zs.OnDouble = () => FitView(); var zd = zs.OnDown;
        zs.OnDown = () => { var tz = TimelineRect(); zoomAnchor = Math.Max(0, Math.Min(eng.EndFrame, scroll + tz.Width / 2 * spp)); if (zd != null) zd(); };
        if (Hot(Rc(x, y + 6, sliderW + 38, 18))) hintNow = "Zoom in and out along the time. Double-click it to see the whole recording.";
    }
    class DawBtn { public string Text, Hint = "", Key = ""; public float W, Px = 10, Gap = 4; public int Style; public Action Do, Right; }
    void PaintDawBar(Graphics g, float y0, float W)
    {
        float x = 14, y = y0 + 3, h = 30; var cc = CurClip(); var row = Selected; bool locked = row != null && row.Locked;
        var L = new List<DawBtn>();
        L.Add(new DawBtn { Text = "DETECT BPM", W = 92, Do = () => DetectBpmClick(), Hint = "Find the tempo of the selected track from its beat." });
        L.Add(new DawBtn { Text = "COUNT IN", W = 70, Style = eng.CountIn ? 4 : 0, Do = () => { eng.CountIn = !eng.CountIn; Toast(eng.CountIn ? "Count In on: press PLAY (or record) and you get one bar of clicks, 1 2 3 4, before it starts." : "Count In off."); } });
        L.Add(new DawBtn { Text = "METRONOME", W = 84, Style = eng.Metronome ? 4 : 0, Do = () => { eng.Metronome = !eng.Metronome; Toast(eng.Metronome ? "Metronome on: a click on every beat while it plays." : "Metronome off."); } });
        L.Add(new DawBtn { Text = "Grid Size", W = 76, Style = 0, Key = "grid", Hint = "Grid Size: how finely clips, cuts and fades snap to the beat. Now: " + GridWhy[gridIdx] + ". Click to change it (the grid lines show briefly).",
            Do = () => { var m = Win.NewMenu(); m.Items.Add("Grid Size: clips and cuts snap to...").Enabled = false; for (int i = 0; i < GridNames.Length; i++) { int k = i; var it = new ToolStripMenuItem(GridNames[i] + "   (" + GridWhy[i] + ")") { Checked = gridIdx == i }; it.Click += (a, b) => { gridIdx = k; snapOn = true; gridFlashAt = Environment.TickCount; Toast("Grid Size: " + GridNames[k] + ". Everything now snaps to " + GridWhy[k] + "."); }; m.Items.Add(it); } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); } });
        L.Add(new DawBtn { Text = "GAIN", W = 46, Style = cc != null && Math.Abs(cc.ClipGainDb) > 0.04f ? 4 : 0, Key = "gain", Do = () => { if (gainOpen) gainOpen = false; else OpenGain(); }, Hint = "The volume of the selected piece on its own (each piece of a split clip can have its own gain)." });
        L.Add(new DawBtn { Text = locked ? "UNLOCK" : "LOCK", W = locked ? 58 : 50, Style = locked ? 4 : 0, Do = () => TrackAction(0, () => ToggleLock()), Hint = "Lock a track in place so it cannot be moved, trimmed, split, cut or deleted until you unlock it. With more than one track it asks which track (TRACK 1, 2, 3...)." });
        L.Add(new DawBtn { Text = flexOpen ? "FLEX*" : "FLEX", W = 46, Style = flexOpen ? 4 : 0, Do = () => { if (flexOpen) FlexClose(true); else TrackAction(3, () => FlexOpen()); }, Hint = "Flex Pitch: drag the notes of a recording to tune them. With more than one track it asks which track (TRACK 1, 2, 3...)." });
        L.Add(new DawBtn { Text = "DEL", W = 40, Style = 3, Gap = 8, Do = () => { if (multiSel.Count > 0) DeleteSelectedClips(); else TrackAction(4, () => DeleteClip(cc ?? Selected)); }, Hint = "Delete a piece: every clip lit by a selection box, or (with more than one track) the piece of the track you choose (TRACK 1, 2, 3...). Ctrl+Z brings them back." });
        L.Add(new DawBtn { Text = "UNDO", W = 48, Do = () => DoUndo() });
        L.Add(new DawBtn { Text = "REDO", W = 48, Gap = 8, Do = () => DoRedo() });
        L.Add(new DawBtn { Text = "FIT", W = 40, Gap = 6, Do = () => FitView() });
        L.Add(new DawBtn { Text = "NEW", W = 46, Gap = 6, Do = () => { NewProject(); if (eng.Tracks.Count == 0) { tab = 0; welcomeOn = true; } } });   // a new project starts on the welcome page
        L.Add(new DawBtn { Text = Prefs.PerfMode ? "PERFORMANCE: ON" : "PERFORMANCE: OFF", W = 142, Px = 10, Style = Prefs.PerfMode ? 4 : 0, Do = () => { Prefs.PerfMode = !Prefs.PerfMode; Prefs.Changed(); Toast(Prefs.PerfMode ? "Performance mode is on: fewer effects and the highest frame rate your screen can show (up to 240), for a smoother DAW." : "Performance mode is off."); }, Hint = "PERFORMANCE MODE: ON turns off the glitch and glow effects and runs at the highest frame rate your screen can show (up to 240 frames a second), so the DAW runs smoother (it is also in the VIEW menu and in Settings)" });
        float need = 0; foreach (var it in L) need += it.W + it.Gap;
        // the row is centred in the window (like the transport above it): BPM, the buttons and the zoom as one group; on a narrow window it starts at the left and every button shrinks together
        { float bpmW = 104, zoomW = 176, kf0 = Math.Min(1f, (W - 14 - bpmW - 20) / need), total = bpmW + need * kf0 + 6 + zoomW; x = Math.Max(14, (W - total) / 2); }
        Txt(g, "BPM", 11, DimC, Rc(x, y, 34, h), StringAlignment.Near, true); x += 34;
        var bb = Rc(x, y, 64, h); if (Hot(bb)) hintNow = "The tempo of your song in beats per minute. It is found automatically when you add music; click the number to type your own, or press DETECT to listen again."; FillRR(g, TM.C(200, 8, 4, 18), bb, 6); StrokeRR(g, Hot(bb) ? Accent : Edge, bb, 6, 1); Txt(g, Fx.ProjectBpm.ToString("0.#", CultureInfo.InvariantCulture), 15, Ink, bb, StringAlignment.Center, true);
        Win.DrawNeonBox(g, bb, Hot(bb));
        ValueEdit(bb, Fx.ProjectBpm.ToString("0.#", CultureInfo.InvariantCulture), s => { float v; if (ParseNum(s, out v) && v >= 20 && v <= 300) { Fx.ProjectBpm = v; bpmUserSet = true; } }); x += 70;
        float kf = Math.Min(1f, (W - x - 20) / need);   // every button shrinks together when the window is narrow   // every button shrinks together when the window is narrow
        foreach (var it in L)
        {
            var r = Rc(x, y, it.W * kf, h); var rg = Btn(g, r, it.Text, it.Style, it.Do, it.Px); if (it.Right != null) rg.OnRight = it.Right; if (it.Key == "gain") gainAnchor = r;
            if (Hot(r)) { string hh2; if (it.Hint.Length > 0) hintNow = it.Hint; else if (Hints.TryGetValue(it.Text.TrimEnd('*'), out hh2)) hintNow = hh2; } x += (it.W + it.Gap) * kf;
        }
        x += 6; if (x + 176 < W - 20) PaintZoomSlider(g, x, y, 130);
    }
    void ZoomBy(double f)
    {
        autoFit = false;
        var tl = TimelineRect(); double cur = scroll + tl.Width / 2 * spp; spp = Math.Max(MinSpp, Math.Min(MaxSpp, spp * f)); scroll = Math.Max(0, (long)(cur - tl.Width / 2 * spp));
    }
}
