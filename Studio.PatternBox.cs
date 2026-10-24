using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  THE PATTERN BOX: GENERATE makes a beat as a box of its own (it does not write onto your tracks). Drag the box onto the song (or press
//  ADD TO SONG) and it becomes its own tracks at the bar you drop it on: kick, snare or clap, hats, open hat, percussion and 808, each a
//  note track you can edit in the piano roll. Each part plays your own sound when you have a track with that job (its sampler is copied),
//  else the sound of that style's pack in the library, so a generated beat always plays. The 808 follows the notes: SOUND can switch it to
//  Bass Lab or the Purphex Wobble to play the line with a synth.
// =====================================================================================================
partial class StudioForm
{
    internal class PatPart { public int Job; public string Name; public List<PrNote> Notes = new List<PrNote>(); public SamplerFx Sound; }
    internal class PatBox { public string Style; public int StyleIndex, Bars; public double Bpm; public List<PatPart> Parts = new List<PatPart>(); }
    internal PatBox prBox; bool prBoxDrag; PointF prBoxAt;
    static readonly string[] PatPackOf = { "trap-starter", "boom-bap-classics", "lofi-essentials", "uk-drill-starter", "dnb-rollers", "house-essentials", "afrobeats-essentials", "reggaeton-essentials", "uk-garage", "drift-phonk" };

    // the beat as notes from bar 0 (frames at the song's tempo), one part per job the style plays
    internal PatBox BuildPattern(int styleIndex, int bars)
    {
        var st = PatStyles[styleIndex]; var box = new PatBox { Style = st.Name, StyleIndex = styleIndex, Bars = bars, Bpm = Fx.ProjectBpm };
        double beat = StudioEngine.SR * 60.0 / Fx.ProjectBpm, step = beat / 4; string[] P = { st.Kick, st.Snare, st.Hat, st.Open, st.Perc };
        for (int j = 0; j < 5; j++)
        {
            string p = P[j]; if (string.IsNullOrEmpty(p)) continue; var part = new PatPart { Job = j, Name = Jobs[j] }; bool swingOk = j >= 2;
            for (int s = 0; s < bars * 16; s++)
            {
                char c = p[s % p.Length]; if (c == '.') continue; double sw = swingOk && st.Swing > 0.5 && s % 2 == 1 ? (st.Swing - 0.5) * 2 * step : 0; long at = (long)Math.Round(s * step + sw); int vel = j == 2 ? 100 : 112;
                int v = c == 'g' ? vel * 55 / 100 : (c == 'r' ? vel * 80 / 100 : vel); if (j == 2 && c == 'x') v = s % 2 == 0 ? vel : vel * 75 / 100;
                part.Notes.Add(new PrNote { Start = at, Len = (long)(step * 0.9), Note = 60, Vel = Math.Max(1, Math.Min(127, v)) });
                if (c == 'r') part.Notes.Add(new PrNote { Start = at + (long)(step / 2), Len = (long)(step * 0.45), Note = 60, Vel = Math.Max(1, v * 85 / 100) });
            }
            box.Parts.Add(part);
        }
        { // the 808 on the kick's rhythm, in the piano roll's key
            var part = new PatPart { Job = 5, Name = Jobs[5] }; var hits = new List<int>(); for (int s = 0; s < bars * 16; s++) if (st.Kick[s % st.Kick.Length] == 'x') hits.Add(s);
            bool minor = prScale != 2 && prScale != 7; int baseNote = 24 + prKeyRoot;
            for (int i = 0; i < hits.Count; i++) { int s = hits[i], next = i + 1 < hits.Count ? hits[i + 1] : bars * 16; int deg = st.Bass[(s / 16) % st.Bass.Length]; if (!minor && (deg == 3 || deg == 8 || deg == 10)) deg++; int n = baseNote + deg; if (st.Slide && i % 4 == 3) n += 12; part.Notes.Add(new PrNote { Start = (long)Math.Round(s * step), Len = (long)Math.Round((next - s) * step * 0.95), Note = n, Vel = 110 }); }
            box.Parts.Add(part);
        }
        foreach (var part in box.Parts) part.Sound = PartSound(part.Job, styleIndex);
        return box;
    }
    // the sound of a part: your track with that job (its sampler copied), else the style's pack in the library
    SamplerFx PartSound(int job, int styleIndex)
    {
        foreach (var t in eng.Tracks) { if (JobOf(t) != job) continue; var src = t.Fx.OfType<SamplerFx>().FirstOrDefault(); if (src == null || src.Zones.Count == 0) continue; var c = new SamplerFx(); c.Prepare(StudioEngine.SR); c.Deserialize(src.Serialize()); return c; }
        try
        {
            var rec = SoundCatalog.Find(PatPackOf[Math.Max(0, Math.Min(PatPackOf.Length - 1, styleIndex))]); if (rec == null) return null; string[] codes = { "ok|", "os|", "oh|", "oo|", "op|", "o8|" };
            var it = InstantLib.ItemsOf(rec.Id).FirstOrDefault(x => x.Spec.StartsWith(codes[job])); if (job == 1 && (rec.Drums == "trap" || rec.Drums == "drill" || rec.Drums == "house")) it = InstantLib.ItemsOf(rec.Id).FirstOrDefault(x => x.Spec.StartsWith("oc|")) ?? it;
            if (it == null || InstantLib.Ensure(it) != null) return null; var s = new SamplerFx(); s.Prepare(StudioEngine.SR);
            int root = 60; if (job == 5) { int rt; bool mi; root = SoundLibrary.ParseKey(it.Key, out rt, out mi) ? 24 + rt : 24; }
            s.SetSingle(it.Path, it.Name.Replace(it.PackName + " - ", ""), job == 5, root); return s;
        }
        catch { return null; }
    }
    // the box becomes its own tracks at a bar of the song
    internal string DropPatternBox(PatBox box, long at)
    {
        if (box == null) return null; PushUndo(); var colors = new[] { 0xFFFF5C7A, 0xFFFFB84D, 0xFF4DD9FF, 0xFF7AF0C2, 0xFFB98CFF, 0xFF9B5CFF }; Track first = null;
        foreach (var part in box.Parts)
        {
            var t = StudioEngine.NewTrack(box.Style + " " + part.Name.Replace(" / ", "/"), "inst", null); t.Instrument = true; t.Color = unchecked((int)colors[Math.Min(colors.Length - 1, part.Job)]);
            if (part.Sound != null) { part.Sound.Owner = t; lock (eng.Sync) t.Fx.Add(part.Sound); }
            var notes = part.Notes.Select(n => new PrNote { Start = at + n.Start, Len = n.Len, Note = part.Sound != null && !part.Sound.AnyFollow && part.Sound.Zones.Count > 0 ? part.Sound.Zones[0].Root : n.Note, Vel = n.Vel }).ToList();
            WriteNotes(t, notes); AddTrack(t); prJobs[t] = part.Job; if (first == null) first = t;
        }
        if (first != null) { int i = eng.Tracks.IndexOf(first); if (i >= 0) { selTrack = i; prLastSel = i; } prTrack = first; prStamp = -1; }
        prBox = null; prBoxDrag = false; Invalidate();
        long bar = (long)Math.Round(at / (StudioEngine.SR * 60.0 / Fx.ProjectBpm * 4)); return box.Style + " beat added at bar " + (bar + 1) + ": " + box.Parts.Count + " new tracks (" + string.Join(", ", box.Parts.Select(p => p.Name)) + "). SOUND in the piano roll changes any part's sound (the 808 can be Bass Lab or the Wobble).";
    }
    // the box in the piano roll (top right of the grid): drag it onto the song
    void PaintPatternBox(Graphics g, RectangleF grid)
    {
        if (prBox == null) return; var box = prBox; var r = Rc(grid.Right - 330, grid.Y + 8, 320, 84);
        if (prBoxDrag)
        {
            r = Rc(prBoxAt.X - 160, prBoxAt.Y - 42, 320, 84); var tl = TimelineRect();
            if (tl.Contains(prBoxAt) && prBoxAt.Y < MixH) { double barF = StudioEngine.SR * 60.0 / Fx.ProjectBpm * 4; long frame = scroll + (long)((prBoxAt.X - HeadW) * spp); long at = (long)(Math.Max(0, Math.Round(frame / barF)) * barF); float lx = HeadW + (float)((at - scroll) / spp); using (var pen = new Pen(NeonCyan, 2)) g.DrawLine(pen, lx, ArrTop, lx, MixH); Txt(g, "BAR " + (Math.Round(at / barF) + 1), 10, NeonCyan, Rc(lx + 4, ArrTop + 2, 80, 16), StringAlignment.Near, true); }
        }
        FillRR(g, TM.C(240, 22, 12, 44), r, 10); Glow(g, NeonCyan, r, 10, prBoxDrag ? 1.2f : 0.8f, 3);
        Txt(g, box.Style.ToUpperInvariant() + " BEAT  ·  " + box.Bars + " BARS  ·  " + box.Parts.Count + " PARTS", 11, Ink, Rc(r.X + 12, r.Y + 6, r.Width - 44, 18), StringAlignment.Near, true);
        // a little picture of the parts
        float px = r.X + 12, pw = r.Width - 24, py = r.Y + 28, ph = 22; var gsP = g.Save(); g.SetClip(Rc(px, py, pw, ph + 1), System.Drawing.Drawing2D.CombineMode.Intersect); long len = (long)(box.Bars * 4 * StudioEngine.SR * 60.0 / Fx.ProjectBpm); int k = 0;
        foreach (var part in box.Parts) { float y = py + k * (ph / box.Parts.Count); using (var br = new SolidBrush(Color.FromArgb(220, Neon(unchecked((int)new[] { 0xFFFF5C7A, 0xFFFFB84D, 0xFF4DD9FF, 0xFF7AF0C2, 0xFFB98CFF, 0xFF9B5CFF }[Math.Min(5, part.Job)]))))) foreach (var n in part.Notes) g.FillRectangle(br, px + pw * n.Start / (float)Math.Max(1, len), y, Math.Max(1.5f, pw * n.Len / (float)Math.Max(1, len)), Math.Max(1.5f, ph / box.Parts.Count - 0.5f)); k++; }
        g.Restore(gsP);
        Txt(g, prBoxDrag ? "DROP IT ON THE SONG (AUDIO CLIP)" : "DRAG ME ONTO THE SONG", 9, NeonCyan, Rc(r.X + 12, r.Bottom - 22, 190, 16), StringAlignment.Near, true);
        if (prBoxDrag) return;
        var reg = Reg(r, () => { prBoxDrag = true; prBoxAt = MousePos; }); reg.OnDrag = pt => { prBoxAt = pt; Invalidate(); }; reg.OnUp = () => PrBoxDropped();
        var x = Rc(r.Right - 30, r.Y + 6, 22, 20); Btn(g, x, "x", 0, () => { prBox = null; }, 10); Hint2(x, "Throw this beat away");
        var add = Rc(r.Right - 112, r.Bottom - 24, 100, 20); Btn(g, add, "ADD TO SONG", 2, () => { var b = prBox; Toast(DropPatternBox(b, (long)Math.Round(Math.Floor(prScrollBeats / 4) * 4 * PrBeatFrames))); }, 9); Hint2(add, "Put this beat into the song at the bar the piano roll shows, as note tracks you can still edit (dragging the box makes one audio clip instead)");
    }
    // ---- your pattern (the piano roll's scratch pad): a box you drag onto the song ----
    bool prDraftDrag; PointF prDraftAt;
    void PaintDraftBox(Graphics g, RectangleF grid)
    {
        var d = prDraft; if (d == null) return; var notes = NotesOf(d); if (notes.Count == 0 && !prDraftDrag) return;
        double barF = PrBeatFrames * 4; long first = notes.Count == 0 ? 0 : (long)(Math.Floor(notes.Min(n => n.Start) / barF) * barF), last = notes.Count == 0 ? 0 : notes.Max(n => n.Start + n.Len); int bars = Math.Max(1, (int)Math.Ceiling((last - first) / barF));
        var r = Rc(grid.Right - 330, grid.Y + 8 + (prBox != null ? 92 : 0), 320, 84);
        if (prDraftDrag)
        {
            r = Rc(prDraftAt.X - 160, prDraftAt.Y - 42, 320, 84); var tl = TimelineRect();
            if (tl.Contains(prDraftAt) && prDraftAt.Y < MixH) { long frame = scroll + (long)((prDraftAt.X - HeadW) * spp); long at = (long)(Math.Max(0, Math.Round(frame / barF)) * barF); float lx = HeadW + (float)((at - scroll) / spp); using (var pen = new Pen(NeonCyan, 2)) g.DrawLine(pen, lx, ArrTop, lx, MixH); Txt(g, "BAR " + (Math.Round(at / barF) + 1), 10, NeonCyan, Rc(lx + 4, ArrTop + 2, 80, 16), StringAlignment.Near, true); }
        }
        FillRR(g, TM.C(240, 22, 12, 44), r, 10); Glow(g, NeonPink, r, 10, prDraftDrag ? 1.2f : 0.8f, 3);
        Txt(g, "YOUR PATTERN  ·  " + bars + (bars == 1 ? " BAR" : " BARS") + "  ·  " + notes.Count + " NOTES", 11, Ink, Rc(r.X + 12, r.Y + 6, r.Width - 44, 18), StringAlignment.Near, true);
        float px = r.X + 12, pw = r.Width - 24, py = r.Y + 28, ph = 22; long len = Math.Max(1, (long)(bars * barF)); int lo = 127, hi = 0; foreach (var n in notes) { lo = Math.Min(lo, n.Note); hi = Math.Max(hi, n.Note); } int span = Math.Max(6, hi - lo + 1);
        using (var br = new SolidBrush(Color.FromArgb(230, Neon(d.Color)))) foreach (var n in notes) g.FillRectangle(br, px + pw * (n.Start - first) / (float)len, py + ph - (n.Note - lo + 1) * ph / span, Math.Max(1.5f, pw * n.Len / (float)len), Math.Max(1.5f, ph / (float)span - 0.3f));
        Txt(g, prDraftDrag ? "DROP IT ON THE SONG (AUDIO CLIP)" : "DRAG ME ONTO THE SONG", 9, NeonPink, Rc(r.X + 12, r.Bottom - 22, 190, 16), StringAlignment.Near, true);
        if (prDraftDrag) return;
        var reg = Reg(r, () => { prDraftDrag = true; prDraftAt = MousePos; }); reg.OnDrag = pt => { prDraftAt = pt; Invalidate(); }; reg.OnUp = () => DraftDropped();
        var x = Rc(r.Right - 30, r.Y + 6, 22, 20); Btn(g, x, "x", 0, () => { prDraft = null; eng.Draft = null; if (prTrack != null && !eng.Tracks.Contains(prTrack)) prTrack = null; }, 10); Hint2(x, "Throw your pattern away");
        var add = Rc(r.Right - 112, r.Bottom - 24, 100, 20); Btn(g, add, "ADD TO SONG", 2, () => Toast(DropDraft((long)Math.Round(Math.Floor(prScrollBeats / 4) * 4 * PrBeatFrames))), 9); Hint2(add, "Put your pattern into the song at the bar the piano roll shows, as a note track you can still edit (dragging the box makes an audio clip instead)");
    }
    void DraftDropped()
    {
        bool was = prDraftDrag; prDraftDrag = false; if (prDraft == null) return; var tl = TimelineRect();
        if (was && tl.Contains(prDraftAt) && prDraftAt.Y < MixH) { double barF = PrBeatFrames * 4; long frame = scroll + (long)((prDraftAt.X - HeadW) * spp); Toast(DropDraftAsAudio((long)(Math.Max(0, Math.Round(frame / barF)) * barF))); }
        else if (was) Toast("Drop your pattern on the song above (the tracks area) to add it, or press ADD TO SONG.");
        Invalidate();
    }
    // your pattern becomes a track at a bar of the song (its sound and notes move onto it); the scratch pad is then empty
    internal string DropDraft(long at)
    {
        var d = prDraft; if (d == null) return null; var notes = NotesOf(d); if (notes.Count == 0) return "Write some notes first (or GENERATE), then drag your pattern onto the song.";
        PushUndo(); double barF = PrBeatFrames * 4; long first = (long)(Math.Floor(notes.Min(n => n.Start) / barF) * barF);
        string nm = d.Name.StartsWith("Your pattern: ") ? d.Name.Substring(14) : "Pattern"; var t = StudioEngine.NewTrack(nm, "inst", null); t.Instrument = true; t.Color = d.Color;
        lock (eng.Sync) { foreach (var f in d.Fx.ToList()) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = t; t.Fx.Add(f); } d.Fx.Clear(); }
        WriteNotes(t, notes.Select(n => new PrNote { Start = at + (n.Start - first), Len = n.Len, Note = n.Note, Vel = n.Vel }).ToList()); AddTrack(t);
        prDraft = null; eng.Draft = null; int i = eng.Tracks.IndexOf(t); if (i >= 0) { selTrack = i; prLastSel = i; } prTrack = t; prStamp = -1; Invalidate();
        return nm + " added at bar " + ((long)Math.Round(at / barF) + 1) + " as a new track. Choose another sound in the piano roll to start a new pattern.";
    }
    // ---- dragged onto the song: the notes are played into an audio clip (a WAV, so the project can reopen it) that moves, trims and copies like any sound ----
    // the pattern stays where it was, so you can change it and drag it again; ADD TO SONG still adds note tracks you can edit
    static string PatternAudioDir() { string d = StudioEnv.UnderTest ? Path.Combine(StudioEnv.DataDir, "patterns") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Patterns"); Directory.CreateDirectory(d); return d; }
    // plays tracks (from frame 0) for len frames plus the ring-out, which is cut where it falls silent
    void RenderParts(List<Track> parts, long len, out float[] l, out float[] r)
    {
        long tail = StudioEngine.SR * 3; eng.Render(0, len + tail, new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = parts }, null, out l, out r);
        long end = len; for (long i = l.Length - 1; i >= len; i--) if (Math.Abs(l[i]) > 1e-4f || Math.Abs(r[i]) > 1e-4f) { end = i + 1; break; }
        if (end < l.Length) { Array.Resize(ref l, (int)end); Array.Resize(ref r, (int)end); }
        int fade = (int)Math.Min(l.Length, StudioEngine.SR / 100); for (int i = 0; i < fade; i++) { float k = i / (float)fade; l[l.Length - 1 - i] *= k; r[r.Length - 1 - i] *= k; }   // no click at the very end
    }
    Track AddPatternClip(float[] l, float[] r, string name, int color, long at)
    {
        string safe = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray()).Trim(); if (safe.Length == 0) safe = "Pattern";
        string path = Path.Combine(PatternAudioDir(), safe + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".wav"); WavIO.Write(path, l, r, StudioEngine.SR, 24);
        var clip = AudioClip.FromArrays(l, r, name); clip.Path = path; var a = StudioEngine.NewTrack(name, "audio", clip); a.Color = color; a.Offset = Math.Max(0, at);
        AddTrack(a); int i = eng.Tracks.IndexOf(a); if (i >= 0) selTrack = i; Invalidate(); return a;
    }
    internal string DropDraftAsAudio(long at)
    {
        var d = prDraft; if (d == null) return null; var notes = NotesOf(d); if (notes.Count == 0) return "Write some notes first (or GENERATE), then drag your pattern onto the song.";
        double barF = PrBeatFrames * 4; long first = (long)(Math.Floor(notes.Min(n => n.Start) / barF) * barF), last = notes.Max(n => n.Start + n.Len); long len = (long)(Math.Max(1, Math.Ceiling((last - first) / barF)) * barF);
        string nm = d.Name.StartsWith("Your pattern: ") ? d.Name.Substring(14) : "Pattern"; var t = StudioEngine.NewTrack(nm, "inst", null); t.Instrument = true;
        float[] l, r; var moved = new List<Fx>();
        lock (eng.Sync) { foreach (var f in d.Fx.ToList()) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = t; t.Fx.Add(f); moved.Add(f); } d.Fx.Clear(); }   // the pattern's sound plays it, then goes back
        try { WriteNotes(t, notes.Select(n => new PrNote { Start = n.Start - first, Len = n.Len, Note = n.Note, Vel = n.Vel }).ToList()); RenderParts(new List<Track> { t }, len, out l, out r); }
        finally { lock (eng.Sync) { t.Fx.Clear(); foreach (var f in moved) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = d; try { f.Reset(); } catch { } d.Fx.Add(f); } } }
        PushUndo(); AddPatternClip(l, r, nm, d.Color, at);
        return nm + " added at bar " + ((long)Math.Round(at / barF) + 1) + " as an audio clip: move, trim or copy it like any sound. Your pattern stays in the piano roll, so you can change it and drag it again (ADD TO SONG adds editable notes instead).";
    }
    internal string DropPatternBoxAsAudio(PatBox box, long at)
    {
        if (box == null) return null; var parts = new List<Track>();
        foreach (var part in box.Parts)
        {
            var t = StudioEngine.NewTrack(part.Name, "inst", null); t.Instrument = true;
            SamplerFx snd = null; if (part.Sound != null) { snd = new SamplerFx(); snd.Prepare(StudioEngine.SR); snd.Deserialize(part.Sound.Serialize()); snd.Owner = t; t.Fx.Add(snd); }   // a copy: the box keeps its own sounds
            WriteNotes(t, part.Notes.Select(n => new PrNote { Start = n.Start, Len = n.Len, Note = snd != null && !snd.AnyFollow && snd.Zones.Count > 0 ? snd.Zones[0].Root : n.Note, Vel = n.Vel }).ToList()); parts.Add(t);
        }
        long len = (long)Math.Round(box.Bars * 4 * StudioEngine.SR * 60.0 / Fx.ProjectBpm); float[] l, r; RenderParts(parts, len, out l, out r);
        PushUndo(); string nm = box.Style + " beat"; AddPatternClip(l, r, nm, unchecked((int)0xFFFF5C7A), at); prBox = null; prBoxDrag = false;
        long bar = (long)Math.Round(at / (StudioEngine.SR * 60.0 / Fx.ProjectBpm * 4)); return nm + " added at bar " + (bar + 1) + " as one audio clip: move, trim or copy it like any sound. (ADD TO SONG puts it in as note tracks you can edit.)";
    }
    // released: on the song it becomes tracks at the bar under the mouse
    void PrBoxDropped()
    {
        var b = prBox; bool wasDrag = prBoxDrag; prBoxDrag = false; if (b == null) return; var tl = TimelineRect();
        if (wasDrag && tl.Contains(prBoxAt) && prBoxAt.Y < MixH)
        {
            double barF = StudioEngine.SR * 60.0 / Fx.ProjectBpm * 4; long frame = scroll + (long)((prBoxAt.X - HeadW) * spp); long at = (long)(Math.Max(0, Math.Round(frame / barF)) * barF);
            Toast(DropPatternBoxAsAudio(b, at));
        }
        else if (wasDrag) Toast("Drop the beat on the song above (the tracks area) to add it, or press ADD TO SONG.");
        Invalidate();
    }
}
