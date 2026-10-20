using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  THE PIANO ROLL: a tab of the panel at the bottom of the Mix page, for every track that plays notes (an instrument, a MIDI track with
//  the built-in keys, a sampler with one sample or a kit). It edits the track's own MIDI (Track.Midi), which the studio's engine plays:
//  there is no second player. The track picker switches tracks without closing it; the track is held by reference, so renaming or
//  moving tracks never sends an edit to the wrong one. Every other note track shows as faint GHOST NOTES behind the active notes (on
//  their pitch, or as timing columns for drum tracks): they are only drawn, never hit-tested, so they cannot be clicked, moved or deleted.
//  Mouse: click the grid to add a note (it then drags with the mouse), drag a note to move it, drag its right edge to resize it,
//  right-click or double-click a note to delete it, Ctrl+drag on the grid to select a box of notes, Shift+drag notes to copy them.
//  Keys: Delete, Ctrl+A, Ctrl+C / Ctrl+V / Ctrl+D (duplicate after itself), Ctrl+Q (quantise), arrows (move by step / semitone,
//  Shift+up/down an octave), Ctrl+Z / Ctrl+Y undo and redo. The velocity lane under the grid sets each note's velocity without moving it.
// =====================================================================================================
partial class StudioForm
{
    public class PrNote { public long Start, Len; public int Note, Vel = 100; public PrNote Copy() { return (PrNote)MemberwiseClone(); } }
    internal Track prTrack; internal readonly List<PrNote> prNotes = new List<PrNote>(); internal readonly HashSet<PrNote> prSel = new HashSet<PrNote>(); long prStamp = -1; Track prStampTrack;
    internal double prPpb = 64; internal int prRowH = 14; internal double prScrollBeats; internal float prScrollY = -1; internal int prSnap = 4; internal double prLenBeats = 0.25;
    internal int prKeyRoot = 9; internal int prScale; internal bool prLock, prLabels = true, prPreview = true, prFollow = true, prGhosts = true; internal int prChord; internal readonly HashSet<Track> prGhostOff = new HashSet<Track>();
    internal bool prFocus; internal RectangleF prGrid, prVel, prKeys, prRuler; int prMode; PointF prDownAt; long prGrabFrame; int prGrabNote; Dictionary<PrNote, PrNote> prOrig; bool prDupDone; int prCreatedTick; int prAudNote = -1; Track prAudTrack;
    static readonly string[] PrSnapNames = { "1 bar", "1/2 bar", "1 beat", "1/2 beat", "1/4 beat (1/16)", "1/8 beat (1/32)", "1/3 beat (triplet 1/8)", "1/6 beat (triplet 1/16)", "None" };
    static readonly double[] PrSnapBeats = { 4, 2, 1, 0.5, 0.25, 0.125, 1 / 3.0, 1 / 6.0, 0 };
    static readonly string[] PrScales = { "No scale", "Minor", "Major", "Harmonic minor", "Dorian", "Phrygian", "Minor pentatonic", "Major pentatonic", "Blues" };
    static readonly int[][] PrScaleSteps = { null, new[] { 0, 2, 3, 5, 7, 8, 10 }, new[] { 0, 2, 4, 5, 7, 9, 11 }, new[] { 0, 2, 3, 5, 7, 8, 11 }, new[] { 0, 2, 3, 5, 7, 9, 10 }, new[] { 0, 1, 3, 5, 7, 8, 10 }, new[] { 0, 3, 5, 7, 10 }, new[] { 0, 2, 4, 7, 9 }, new[] { 0, 3, 5, 6, 7, 10 } };
    static readonly string[] PrChords = { "Single notes", "Major chord", "Minor chord", "Major 7", "Minor 7", "Power chord", "Octave" };
    static readonly int[][] PrChordSteps = { new[] { 0 }, new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 0, 4, 7, 11 }, new[] { 0, 3, 7, 10 }, new[] { 0, 7 }, new[] { 0, 12 } };
    static readonly string[] NoteNm = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    internal static string PrNoteName(int n) { return NoteNm[((n % 12) + 12) % 12] + (n / 12 - 1); }
    const int PrLow = 12, PrHigh = 120;
    internal bool PrShown { get { return InsShown && insTab == 4; } }

    // ---- the settings saved with the project: snap, length, key, scale, lock, labels, preview, ghosts, follow, hidden ghost tracks and jobs (by track number) ----
    internal string PrPrefsText()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var off = new List<string>(); foreach (var t in prGhostOff) { int i = eng.Tracks.IndexOf(t); if (i >= 0) off.Add(i.ToString()); }
        var jobs = new List<string>(); foreach (var kv in prJobs) { int i = eng.Tracks.IndexOf(kv.Key); if (i >= 0) jobs.Add(i + ":" + kv.Value); }
        return prSnap + ";" + prLenBeats.ToString("R", ci) + ";" + prKeyRoot + ";" + prScale + ";" + (prLock ? 1 : 0) + ";" + (prLabels ? 1 : 0) + ";" + (prPreview ? 1 : 0) + ";" + (prGhosts ? 1 : 0) + ";" + (prFollow ? 1 : 0) + ";" + string.Join(",", off) + ";" + string.Join(",", jobs);
    }
    internal void PrApplyPrefs(string text)
    {
        prGhostOff.Clear(); prJobs.Clear(); prTrack = null; prSel.Clear(); prStamp = -1; prScrollBeats = 0; prScrollY = -1;
        prSnap = 4; prLenBeats = 0.25; prKeyRoot = 9; prScale = 0; prLock = false; prLabels = true; prPreview = true; prFollow = true; prGhosts = true; prBox = null; prDraft = null; eng.Draft = null;   // (a project without piano roll settings starts from the defaults, not from the last project)
        if (string.IsNullOrEmpty(text)) return;
        var f = text.Split(';'); var ci = System.Globalization.CultureInfo.InvariantCulture; int iv; double dv;
        try
        {
            if (int.TryParse(f[0], out iv)) prSnap = Math.Max(0, Math.Min(PrSnapNames.Length - 1, iv)); if (double.TryParse(f[1], System.Globalization.NumberStyles.Float, ci, out dv) && dv > 0) prLenBeats = dv;
            if (int.TryParse(f[2], out iv)) prKeyRoot = ((iv % 12) + 12) % 12; if (int.TryParse(f[3], out iv)) prScale = Math.Max(0, Math.Min(PrScales.Length - 1, iv));
            prLock = f[4] == "1"; prLabels = f[5] == "1"; prPreview = f[6] == "1"; prGhosts = f[7] == "1"; prFollow = f[8] == "1";
            foreach (var x in f[9].Split(',')) if (int.TryParse(x, out iv) && iv >= 0 && iv < eng.Tracks.Count) prGhostOff.Add(eng.Tracks[iv]);
            foreach (var x in f[10].Split(',')) { var kv = x.Split(':'); int ti, j; if (kv.Length == 2 && int.TryParse(kv[0], out ti) && int.TryParse(kv[1], out j) && ti >= 0 && ti < eng.Tracks.Count) prJobs[eng.Tracks[ti]] = j; }
        }
        catch { }
    }

    // a small keyboard drawn on the piano button
    void MixerGlyph(Graphics g, RectangleF r)
    {
        float hgt = Math.Min(20, r.Height - 10), oy = r.Y + (r.Height - hgt) / 2, ox = r.X + r.Width / 2 - 10; float[] knob = { 0.65f, 0.25f, 0.5f };
        using (var line = new Pen(Color.FromArgb(220, 255, 255, 255), 2f)) using (var kb = new SolidBrush(Color.FromArgb(255, 255, 255, 255))) using (var kp = new Pen(Color.FromArgb(200, 20, 12, 36), 1f))
            for (int i = 0; i < 3; i++) { float lx = ox + i * 10; g.DrawLine(line, lx, oy, lx, oy + hgt); float ky = oy + hgt * knob[i]; g.FillRectangle(kb, lx - 4, ky - 2.5f, 8, 5); g.DrawRectangle(kp, lx - 4, ky - 2.5f, 8, 5); }
    }
    void PianoGlyph(Graphics g, RectangleF r)
    {
        float kw = 5, kh = Math.Min(18, r.Height - 12), ox = r.X + (r.Width - kw * 5) / 2, oy = r.Y + (r.Height - kh) / 2;
        using (var wb = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) using (var bb = new SolidBrush(Color.FromArgb(255, 20, 12, 36))) using (var pen = new Pen(Color.FromArgb(160, 20, 12, 36)))
        {
            for (int i = 0; i < 5; i++) { g.FillRectangle(wb, ox + i * kw, oy, kw - 1, kh); }
            foreach (int i in new[] { 0, 1, 3 }) g.FillRectangle(bb, ox + i * kw + kw * 0.6f, oy, kw * 0.75f, kh * 0.58f);
        }
    }
    // ---- choosing the sound of a track (any track: pick it, then choose what it plays) ----
    void PaintChooseSound(Graphics g, Track t, RectangleF msg)
    {
        Txt(g, t == null ? "PICK A TRACK, THEN CHOOSE ITS SOUND" : "CHOOSE A SOUND FOR " + t.Name.ToUpperInvariant(), 14, Ink, Rc(msg.X, msg.Y + 12, msg.Width, 24), StringAlignment.Center, true);
        Txt(g, t == null ? "Click a track on the left, or make a new one here." : "Then write its notes in this piano roll, or play them on your keyboard.", 11, DimC, Rc(msg.X, msg.Y + 38, msg.Width, 20), StringAlignment.Center, false);
        var opts = new List<KeyValuePair<string, Action>> {
            new KeyValuePair<string, Action>("PIANO", () => PrUseSound(t, null, null)),
            new KeyValuePair<string, Action>("A SOUND FROM SOUNDS", () => { var nt = PrUseSound(t, "sampler", null); if (nt != null) { OpenSounds(); Toast("Drag a sound from SOUNDS onto the piano roll (or right-click it > Load it into the piano roll)"); } }),
            new KeyValuePair<string, Action>("PURPHEX WOBBLE", () => PrWobbleMenu(t)),
            new KeyValuePair<string, Action>("BASS LAB", () => PrUseSound(t, "synth", null)),
            new KeyValuePair<string, Action>("MORE INSTRUMENTS...", () => PrSoundMenu(t)) };
        float bw = 168, gap = 10, total = opts.Count * bw + (opts.Count - 1) * gap, ox = msg.X + Math.Max(10, (msg.Width - total) / 2);
        for (int i = 0; i < opts.Count; i++) { var r = Rc(ox + i * (bw + gap), msg.Y + 70, bw, 40); var act = opts[i].Value; Btn(g, r, opts[i].Key, i == 0 ? 2 : 0, act, 11); }
    }
    // the menu of every sound a track can play
    void PrSoundMenu(Track t)
    {
        var m = Win.NewMenu();
        m.Items.Add("Piano (built-in keys)").Click += (a, b) => PrUseSound(t, null, null);
        m.Items.Add("A sound from SOUNDS (sampler: drums, 808s, one-shots...)").Click += (a, b) => { var nt = PrUseSound(t, "sampler", null); if (nt != null) { OpenSounds(); Toast("Drag a sound from SOUNDS onto the piano roll"); } };
        var wm = new ToolStripMenuItem("Purphex Wobble (wobble bass presets)"); foreach (var s in WobbleFx.FactoryPresets()) { int k = s.IndexOf('|'); string nm = s.Substring(0, k), data = s.Substring(k + 1); wm.DropDownItems.Add(nm).Click += (a, b) => PrUseSound(t, "wobble", data, nm); } m.Items.Add(wm);
        m.Items.Add(new ToolStripSeparator());
        foreach (var i in FxRegistry.All) { if (i.Category != "Instruments" || i.Type == "keys" || i.Type == "sampler" || i.Type == "wobble" || i.Type == "pxdrum2midi") continue; string ty = i.Type; m.Items.Add(i.Name).Click += (a, b) => PrUseSound(t, ty, null); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrWobbleMenu(Track t)
    {
        var m = Win.NewMenu(); foreach (var s in WobbleFx.FactoryPresets()) { int k = s.IndexOf('|'); string nm = s.Substring(0, k), data = s.Substring(k + 1); m.Items.Add(nm).Click += (a, b) => PrUseSound(t, "wobble", data, nm); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    // the track plays this sound from now on (an audio track with a clip keeps its audio: a new track gets the sound). type null = the built-in piano
    Track PrUseSound(Track t, string type, string preset, string presetName = null)
    {
        if (t != null && t.IsBus) return null;
        bool draft = t == null || t == prDraft || !PrCanEdit(t);   // (a note track you already have changes its sound; anything else becomes your pattern, not a track)
        if (draft) { if (prDraft == null) { prDraft = StudioEngine.NewTrack("Your pattern", "inst", null); prDraft.Instrument = true; prDraft.Color = unchecked((int)0xFF3B8CF5); } t = prDraft; eng.Draft = prDraft; }
        else PushUndo();
        bool fresh = false;
        InstrumentFx f = null;
        if (type != null) { f = FxRegistry.Create(type) as InstrumentFx; if (f == null) { Toast("That instrument couldn't be started."); return null; } f.Prepare(StudioEngine.SR); if (preset != null) { Presets.Apply(f, preset); f.CurPreset = presetName ?? ""; f.CurPresetData = f.Serialize(); } f.Owner = t; }
        lock (eng.Sync) { t.Fx.RemoveAll(x => x is InstrumentFx); if (f != null) t.Fx.Insert(0, f); t.Instrument = true; }
        bool plainName = fresh || t.Name.StartsWith("Audio") || t.Name.StartsWith("MIDI") || t.Name.StartsWith("Instrument") || t.Name == "Piano" || t.Name == "Drums";
        if (plainName) t.Name = type == null ? "Piano" : (presetName ?? (f != null ? f.Title : t.Name));
        if (t == prDraft) t.Name = "Your pattern: " + (type == null ? "Piano" : (presetName ?? (f != null ? f.Title : "Instrument")));
        int ti = eng.Tracks.IndexOf(t); if (ti >= 0) { selTrack = ti; prLastSel = ti; } prTrack = t; prStamp = -1; prScrollY = -1; OpenInspector(4); Invalidate();
        Toast(t == prDraft ? "Your pattern plays " + (type == null ? "the built-in piano" : (presetName ?? f.Title)) + ": write notes (or GENERATE), then drag the pattern box onto the song. Nothing goes on your tracks until you do." : t.Name + " plays " + (type == null ? "the built-in piano" : (presetName ?? f.Title)) + " now: write notes here or play your keyboard"); return t;
    }

    // ---- opening ----
    internal void OpenPianoRoll(Track t)
    {
        if (t == null) { t = PrDefaultTrack(); }
        if (t != null) PrSetTrack(t); OpenInspector(4); if (Prefs.InsH < 420) { Prefs.InsH = 420; Prefs.Changed(); }
    }
    Track PrDefaultTrack() { var cur = eng.Tracks.Count > 0 ? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))] : null; if (cur != null && !cur.IsBus) return cur;   /* the track you picked, whatever it is (no sound yet: choose one here) */ foreach (var x in eng.Tracks) if (PrCanEdit(x)) return x; return cur; }
    internal static bool PrCanEdit(Track t) { return t != null && !t.IsBus && t.Instrument; }
    internal void PrSetTrack(Track t)
    {
        if (t == prTrack) return; PrSilence(); prTrack = t; prSel.Clear(); prStamp = -1; prScrollY = -1; int i = eng.Tracks.IndexOf(t); if (i >= 0 && !t.IsBus) { selTrack = i; prLastSel = i; }
    }
    // the notes of the active track, read again whenever the track's MIDI changed (recording, undo, another editor)
    static long MidiStamp(Track t) { long h = 17; lock (t.Midi) { h = h * 31 + t.Midi.Count; foreach (var m in t.Midi) h = h * 31 + m.Frame * 7 + m.D1 * 131 + m.D2 + m.S * 977; } return h; }
    int prLastSel = -1;
    internal Track prDraft; RectangleF prArea;   // your pattern that is not on the song yet (the piano roll's scratch pad): it becomes a track when you drag it onto the song
    void PrSync()
    {
        if (prTrack != null && prTrack != prDraft && (!eng.Tracks.Contains(prTrack))) { prTrack = null; prNotes.Clear(); prSel.Clear(); }
        if (selTrack != prLastSel) { prLastSel = selTrack; if (selTrack >= 0 && selTrack < eng.Tracks.Count && !eng.Tracks[selTrack].IsBus && eng.Tracks[selTrack] != prTrack) { PrSilence(); prTrack = eng.Tracks[selTrack]; prSel.Clear(); prStamp = -1; prScrollY = -1; } }   // click a track: the piano roll shows it
        if (prTrack == null) { var d = PrDefaultTrack(); if (d != null) prTrack = d; else return; }
        long st = MidiStamp(prTrack); if (st == prStamp && prStampTrack == prTrack) return;
        prNotes.Clear(); prNotes.AddRange(NotesOf(prTrack)); prSel.Clear(); prStamp = st; prStampTrack = prTrack;
    }
    internal static List<PrNote> NotesOf(Track t)
    {
        var l = new List<PrNote>(); var open = new Dictionary<int, PrNote>();
        lock (t.Midi) foreach (var m in t.Midi)
        {
            int k = m.S & 0xF0;
            if (k == 0x90 && m.D2 > 0) { PrNote o; if (open.TryGetValue(m.D1, out o)) o.Len = Math.Max(1, m.Frame - o.Start); var n = new PrNote { Start = m.Frame, Note = m.D1, Vel = m.D2, Len = StudioEngine.SR / 8 }; l.Add(n); open[m.D1] = n; }
            else if (k == 0x80 || k == 0x90) { PrNote n; if (open.TryGetValue(m.D1, out n)) { n.Len = Math.Max(1, m.Frame - n.Start); open.Remove(m.D1); } }
        }
        return l;
    }
    // write the notes back to the track (each note: a note-on and a note-off; at one moment the note-offs come first)
    internal static void NoOverlap(List<PrNote> notes) { foreach (var g in notes.GroupBy(n => n.Note)) { var l = g.OrderBy(n => n.Start).ToList(); for (int i = 0; i + 1 < l.Count; i++) if (l[i].Start + l[i].Len > l[i + 1].Start) l[i].Len = Math.Max(1, l[i + 1].Start - l[i].Start); } }
    void PrCommit()
    {
        if (prTrack == null) return; NoOverlap(prNotes); var ev = new List<MidiEv>();
        foreach (var n in prNotes) { int note = Math.Max(0, Math.Min(127, n.Note)); int vel = Math.Max(1, Math.Min(127, n.Vel)); long s = Math.Max(0, n.Start), len = Math.Max(1, n.Len); ev.Add(new MidiEv { Frame = s, S = 0x90, D1 = (byte)note, D2 = (byte)vel }); ev.Add(new MidiEv { Frame = s + len, S = 0x80, D1 = (byte)note, D2 = 0 }); }
        ev.Sort((a, b) => { int c = a.Frame.CompareTo(b.Frame); return c != 0 ? c : a.S.CompareTo(b.S); });
        lock (prTrack.Midi) { prTrack.Midi.Clear(); prTrack.Midi.AddRange(ev); } prStamp = MidiStamp(prTrack); prStampTrack = prTrack;
    }

    // ---- time and pitch <-> screen (one coordinate system for drawing and clicking) ----
    double PrBeatFrames { get { return StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm); } }
    internal float PrX(long frame) { return prGrid.X + (float)((frame / PrBeatFrames - prScrollBeats) * prPpb); }
    internal long PrFrameAt(float x) { return Math.Max(0, (long)Math.Round(((x - prGrid.X) / prPpb + prScrollBeats) * PrBeatFrames)); }
    internal float PrY(int note) { return prGrid.Y + (PrHigh - note) * prRowH - prScrollY; }
    internal int PrNoteAt(float y) { return PrHigh - (int)Math.Floor((y - prGrid.Y + prScrollY) / prRowH); }
    double PrSnapFrames { get { return PrSnapBeats[prSnap] * PrBeatFrames; } }
    internal long PrSnapFrame(long f) { double g = PrSnapFrames; if (g <= 0) return f; return (long)Math.Round(Math.Round(f / g) * g); }
    long PrSnapDown(long f) { double g = PrSnapFrames; if (g <= 0) return f; return (long)Math.Round(Math.Floor(f / g + 1e-9) * g); }
    long PrLenFrames { get { return Math.Max(64, (long)Math.Round(prLenBeats * PrBeatFrames)); } }
    bool PrInScale(int n) { var s = PrScaleSteps[prScale]; if (s == null) return true; return Array.IndexOf(s, ((n - prKeyRoot) % 12 + 12) % 12) >= 0; }
    int PrToScale(int n) { if (!prLock || PrScaleSteps[prScale] == null) return n; for (int d = 0; d < 7; d++) { if (PrInScale(n - d)) return n - d; if (PrInScale(n + d)) return n + d; } return n; }

    // the instrument that plays the track (its own, or the built-in keys of a MIDI track with none)
    static readonly bool hookPlayer = SetPlayerHook(); static bool SetPlayerHook() { StudioEngine.InstrumentOf = PlayerOf; return true; }
    internal static InstrumentFx PlayerOf(Track t) { if (t == null) return null; foreach (var f in t.Fx) { var i = f as InstrumentFx; if (i != null && i.IsInstrument && !f.Bypass) return i; } return t.Instrument ? t.Keys : null; }
    internal SamplerFx PrSampler { get { if (prTrack == null) return null; foreach (var f in prTrack.Fx) { var s = f as SamplerFx; if (s != null) return s; } return null; } }
    void PrAudition(int note, int vel)
    {
        if (!prPreview || prTrack == null) return; PrSilence(); var p = PlayerOf(prTrack); if (p == null) return; eng.EnsureAudio(); p.NoteOn(note, Math.Max(1, Math.Min(127, vel))); prAudNote = note; prAudTrack = prTrack;
    }
    internal void PrSilence() { if (prAudNote >= 0 && prAudTrack != null) { var p = PlayerOf(prAudTrack); if (p != null) p.NoteOff(prAudNote); } prAudNote = -1; prAudTrack = null; }

    // the notes that do not belong to the active track (shown faintly; never edited here)
    List<KeyValuePair<Track, List<PrNote>>> PrGhostTracks()
    {
        var l = new List<KeyValuePair<Track, List<PrNote>>>(); if (!prGhosts) return l;
        foreach (var t in eng.Tracks) { if (t == prTrack || !PrCanEdit(t) || prGhostOff.Contains(t)) continue; int c; lock (t.Midi) c = t.Midi.Count; if (c == 0) continue; l.Add(new KeyValuePair<Track, List<PrNote>>(t, NotesOf(t))); }
        return l;
    }
    static bool DrumLike(Track t) { foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null) return !s.AnyFollow; } return false; }

    // ---- painting ----
    void PaintPianoRoll(Graphics g, RectangleF C)
    {
        PrSync(); var t = prTrack; float x = C.X, y = C.Y, w = C.Width, h = C.Height; prArea = C;
        // the bar: track picker, what kind of track, snap, length, scale, chords, ghosts, preview, tools
        float bx = x; var tp = Rc(bx, y, 28, 24); Btn(g, tp, "<", 0, () => PrStepTrack(-1), 11); Hint2(tp, "The previous note track"); bx += 30;
        var tn = Rc(bx, y, 190, 24); string tname = t == null ? "NO NOTE TRACK" : (t == prDraft ? "YOUR PATTERN" : t.Name); Btn(g, tn, tname, 4, () => PrTrackMenu(), 10); Hint2(tn, "The track you are editing: click to pick another (the piano roll stays open)"); bx += 192;
        var tx2 = Rc(bx, y, 28, 24); Btn(g, tx2, ">", 0, () => PrStepTrack(1), 11); Hint2(tx2, "The next note track"); bx += 34;
        if (t != null) { using (var cb = new SolidBrush(Neon(t.Color))) g.FillEllipse(cb, tn.X + 6, tn.Y + 7, 10, 10); }
        string kind = t == null ? "" : !PrCanEdit(t) ? (t.Clip != null ? "AUDIO TRACK" : "NO SOUND YET") : (PrSampler != null ? (PrSampler.IsKit ? "DRUM KIT (SAMPLER)" : (PrSampler.Zones.Count == 0 ? "SAMPLER: NO SAMPLE YET" : (PrSampler.AnyFollow ? "SAMPLER: FOLLOWS THE NOTES" : "SAMPLER: FIXED PITCH (DRUM)"))) : (t.HasInstrumentFx ? "INSTRUMENT: " + PlayerOf(t).Title.ToUpperInvariant() : "MIDI TRACK: BUILT-IN KEYS"));
        if (t != null && PrCanEdit(t)) { var sb = Rc(bx, y, 190, 24); Btn(g, sb, "SOUND: " + (kind.StartsWith("MIDI") ? "PIANO" : kind.Replace("INSTRUMENT: ", "")), 2, () => PrSoundMenu(t), 9); Hint2(sb, "What this track plays: the built-in piano, a sample from SOUNDS, the Purphex Wobble, Bass Lab or another instrument (the notes stay)"); }
        else Txt(g, kind, 9, DimC, Rc(bx, y, 190, 24), StringAlignment.Near, true);
        bx += 194;
        Func<float, string, bool, Action, string, RectangleF> chip = (cw, label, on, act, hint) => { var r = Rc(bx, y, cw, 24); Btn(g, r, label, on ? 4 : 0, act, 9); Hint2(r, hint); bx += cw + 4; return r; };
        chip(122, "SNAP " + PrSnapShort(prSnap), false, () => PrSnapMenu(), "Where notes land: 1 bar down to 1/32, triplets, or no snap");
        chip(104, "LENGTH " + PrBeatsName(prLenBeats), false, () => PrLenMenu(), "The length of the next note you add (drag a note's right edge to change one)");
        chip(118, prScale == 0 ? "SCALE: OFF" : NoteNm[prKeyRoot] + " " + PrScales[prScale].ToUpperInvariant(), prScale != 0, () => PrScaleMenu(), "Light up the notes of a key and scale (you can still use any note unless LOCK is on)");
        chip(54, "LOCK", prLock, () => { prLock = !prLock; }, "Notes you add or move stay on the scale");
        chip(96, prChord == 0 ? "CHORDS: OFF" : PrChords[prChord].ToUpperInvariant(), prChord != 0, () => PrChordMenu(), "Add a whole chord with one click");
        chip(82, prGhosts ? "GHOSTS ON" : "GHOSTS OFF", prGhosts, () => PrGhostMenu(), "The other tracks' notes, faint, behind yours (they cannot be edited here)");
        chip(76, "PREVIEW", prPreview, () => { prPreview = !prPreview; PrSilence(); }, "Hear notes as you add, move or click them");
        chip(66, "LABELS", prLabels, () => prLabels = !prLabels, "Note names on the notes");
        chip(82, "QUANTISE", false, () => PrQuantise(), "Move the selected notes (or all) onto the snap grid (Ctrl+Q)");
        chip(82, "PATTERN", false, () => PrPatternMenu(), "Repeat a pattern of bars along the song, or clear bars");
        chip(92, "GENERATE", false, () => PrGenerateMenu(), "A starting beat in a style (trap, hip-hop, lo-fi, drill, DnB...) written onto your kick, snare, hats and 808 tracks");
        if (PrSampler != null) chip(84, "SAMPLES", PrSampler.Zones.Count == 0, () => PrSamplesMenu(), "Load a sample, build a kit (one sample per key), fixed pitch or follow the notes");
        if (t == null || !PrCanEdit(t))
        {
            var msg = Rc(x, y + 40, w, h - 40);
            if (t != null && t.Clip != null) { Txt(g, t.Name + " is an audio track: its clip stays real audio on the arrangement (move, trim and stretch it there).", 12, Ink, Rc(msg.X, msg.Y + 20, msg.Width, 22), StringAlignment.Center, true); var lb = Rc(msg.X + msg.Width / 2 - 140, msg.Y + 56, 280, 32); Btn(g, lb, "LOAD IT INTO THE PIANO ROLL", 2, () => PrAudioToSampler(t), 11); Hint2(lb, "Play this sound from notes in the piano roll (nothing is added to the song until you drag your pattern there; the audio track stays as it is)"); }
            else PaintChooseSound(g, t, msg);
            if (!prBoxDrag) PaintPatternBox(g, msg);
            return;
        }
        // the areas
        var sam = PrSampler; float keyW = sam != null && sam.IsKit ? 120 : 64, velH = Math.Min(70, Math.Max(40, h * 0.18f)), rulerH = 18;
        prKeys = Rc(x, y + 30 + rulerH, keyW, h - 30 - rulerH - velH - 6); prRuler = Rc(x + keyW, y + 30, w - keyW, rulerH); prGrid = Rc(x + keyW, prKeys.Y, w - keyW, prKeys.Height); prVel = Rc(x + keyW, prGrid.Bottom + 6, w - keyW, velH);
        int rows = PrHigh - PrLow + 1; float maxY = Math.Max(0, rows * prRowH - prGrid.Height);
        if (prScrollY < 0) { int mid = 60; if (prNotes.Count > 0) { int lo = 127, hi = 0; foreach (var n in prNotes) { lo = Math.Min(lo, n.Note); hi = Math.Max(hi, n.Note); } mid = (lo + hi) / 2; } prScrollY = (PrHigh - mid) * prRowH - prGrid.Height / 2; }
        prScrollY = Math.Max(0, Math.Min(maxY, prScrollY)); prScrollBeats = Math.Max(0, prScrollBeats);
        // follow the playhead
        if (prFollow && eng.Playing && prMode == 0) { double pb = DispPos / PrBeatFrames, vis = prGrid.Width / prPpb; if (pb < prScrollBeats || pb > prScrollBeats + vis * 0.92) prScrollBeats = Math.Max(0, Math.Floor(pb / 4) * 4); }
        var gs = g.Save(); g.SetClip(prGrid, CombineMode.Intersect);
        using (var b = new SolidBrush(TM.C(255, 12, 6, 26))) g.FillRectangle(b, prGrid);
        for (int n = PrHigh; n >= PrLow; n--)
        {
            float ry = PrY(n); if (ry + prRowH < prGrid.Y || ry > prGrid.Bottom) continue; bool black = NoteNm[n % 12].Length > 1, inS = prScale != 0 && PrInScale(n), root = prScale != 0 && ((n - prKeyRoot) % 12 + 12) % 12 == 0;
            Color rc = root ? TM.C(255, 44, 22, 78) : (inS ? TM.C(255, 32, 16, 60) : (black ? TM.C(255, 14, 7, 28) : TM.C(255, 20, 10, 38))); using (var b = new SolidBrush(rc)) g.FillRectangle(b, prGrid.X, ry, prGrid.Width, prRowH);
            if (n % 12 == 0) using (var p = new Pen(TM.C(120, 120, 80, 200))) g.DrawLine(p, prGrid.X, ry + prRowH, prGrid.Right, ry + prRowH);
        }
        // the beat grid: snap lines faint, beats stronger, bars strongest
        { double step = PrSnapBeats[prSnap] > 0 ? PrSnapBeats[prSnap] : 0.25; while (step * prPpb < 7) step *= 2; double b0 = Math.Floor(prScrollBeats / step) * step;
          using (var thin = new Pen(TM.C(28, 190, 150, 255))) using (var beatP = new Pen(TM.C(60, 190, 150, 255))) using (var barP = new Pen(TM.C(130, 210, 170, 255), 1.4f))
            for (double bt = b0; ; bt += step) { float gx = prGrid.X + (float)((bt - prScrollBeats) * prPpb); if (gx > prGrid.Right) break; bool bar = Math.Abs(bt / 4 - Math.Round(bt / 4)) < 1e-6, beat = Math.Abs(bt - Math.Round(bt)) < 1e-6; g.DrawLine(bar ? barP : (beat ? beatP : thin), gx, prGrid.Y, gx, prGrid.Bottom); } }
        // the song's loop section
        if (eng.Loop && eng.LoopB > eng.LoopA) { float lx0 = PrX(eng.LoopA), lx1 = PrX(eng.LoopB); using (var b = new SolidBrush(Color.FromArgb(18, NeonCyan))) g.FillRectangle(b, lx0, prGrid.Y, lx1 - lx0, prGrid.Height); }
        // ghost notes: faint, behind, never clickable
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var kv in PrGhostTracks())
        {
            bool cols = DrumLike(kv.Key) || (sam != null && sam.IsKit && DrumLike(kv.Key)); Color gc = Color.FromArgb(255, (Neon(kv.Key.Color).R + 150) / 2, (Neon(kv.Key.Color).G + 140) / 2, (Neon(kv.Key.Color).B + 200) / 2);
            foreach (var n in kv.Value)
            {
                float gx0 = PrX(n.Start), gx1 = PrX(n.Start + n.Len); if (gx1 < prGrid.X || gx0 > prGrid.Right) continue;
                if (cols) { using (var b = new SolidBrush(Color.FromArgb(26, gc))) g.FillRectangle(b, gx0, prGrid.Y, Math.Max(3, Math.Min(gx1 - gx0, (float)prPpb * 0.25f)), prGrid.Height); using (var p = new Pen(Color.FromArgb(70, gc), 1f)) g.DrawLine(p, gx0, prGrid.Y, gx0, prGrid.Bottom); }
                float gy = PrY(n.Note); if (gy + prRowH < prGrid.Y || gy > prGrid.Bottom) continue; var gr = Rc(gx0, gy + 2, Math.Max(3, gx1 - gx0), prRowH - 4);
                using (var b = new SolidBrush(Color.FromArgb(46, gc))) using (var p = RR(gr, 3)) { g.FillPath(b, p); using (var pen = new Pen(Color.FromArgb(90, gc), 1f) { DashStyle = DashStyle.Dot }) g.DrawPath(pen, p); }
            }
        }
        // the active track's notes: bright, on top
        Color acc = UiTokens.Sel(NeonViolet), accSel = NeonPink;
        foreach (var n in prNotes)
        {
            float nx0 = PrX(n.Start), nx1 = PrX(n.Start + n.Len), ny = PrY(n.Note); if (nx1 < prGrid.X || nx0 > prGrid.Right || ny + prRowH < prGrid.Y || ny > prGrid.Bottom) continue;
            var nr = Rc(nx0, ny + 1, Math.Max(4, nx1 - nx0), prRowH - 2); bool sl = prSel.Contains(n); Color c = sl ? accSel : acc; int a = 150 + n.Vel * 105 / 127;
            using (var p = RR(nr, 3)) { using (var b = new LinearGradientBrush(nr, Color.FromArgb(Math.Min(255, a), c), Color.FromArgb(Math.Min(255, a - 40), MixC(c, Color.Black, 0.35f)), 90f)) g.FillPath(b, p); using (var sh = new LinearGradientBrush(nr, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(sh, nr.X, nr.Y, nr.Width, nr.Height * 0.45f); using (var pen = new Pen(sl ? Color.White : Color.FromArgb(200, MixC(c, Color.White, 0.4f)), sl ? 1.6f : 1f)) g.DrawPath(pen, p); }
            using (var hb = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillRectangle(hb, nr.Right - 3, nr.Y + 2, 2, nr.Height - 4);   // the resize edge
            if (prLabels && nr.Width > 22 && prRowH >= 11) Txt(g, PrNoteName(n.Note), 7.5f, Color.FromArgb(235, 255, 255, 255), Rc(nr.X + 3, nr.Y, nr.Width - 6, nr.Height), StringAlignment.Near, true);
        }
        // the box being drawn, and the playhead
        if (prMode == 4) { var br = PrBox(); using (var b = new SolidBrush(Color.FromArgb(40, NeonCyan))) g.FillRectangle(b, br); using (var p = new Pen(NeonCyan, 1f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(p, br.X, br.Y, br.Width, br.Height); }
        { float px = PrX((long)DispPos); if (px >= prGrid.X && px <= prGrid.Right) using (var p = new Pen(Color.FromArgb(240, NeonPink), 2f)) g.DrawLine(p, px, prGrid.Y, px, prGrid.Bottom); }
        g.Restore(gs);
        // the grid's mouse: one region, one coordinate system (ghosts are never in it)
        var gr2 = Reg(prGrid, () => PrDown(MousePos)); gr2.OnDrag = pt => PrDrag(pt); gr2.OnUp = () => PrUp(); gr2.OnRight = () => PrRight(MousePos); gr2.OnDouble = () => PrDouble(MousePos); gr2.OnWheel = d => PrWheel(d, MousePos); gr2.Overlay = true;
        if (Hot(prGrid) && prMode == 0) { bool edge; var hn = PrHit(MousePos, out edge); Cursor = hn == null ? Cursors.Cross : (edge ? Cursors.SizeWE : Cursors.SizeAll); }
        // ruler: bars, the pattern of the selection, click to move the song there
        using (var b = new SolidBrush(TM.C(255, 22, 11, 42))) g.FillRectangle(b, prRuler);
        { double b0 = Math.Floor(prScrollBeats / 4) * 4; for (double bt = b0; ; bt += 4) { float rx = prRuler.X + (float)((bt - prScrollBeats) * prPpb); if (rx > prRuler.Right) break; if (rx < prRuler.X - 30) continue; using (var p = new Pen(TM.C(140, 210, 170, 255))) g.DrawLine(p, rx, prRuler.Y + 8, rx, prRuler.Bottom); Txt(g, ((int)(bt / 4) + 1).ToString(), 8.5f, Ink, Rc(rx + 3, prRuler.Y, 40, prRuler.Height), StringAlignment.Near, false); } }
        { float px = PrX((long)DispPos); if (px >= prRuler.X && px <= prRuler.Right) using (var b = new SolidBrush(NeonPink)) g.FillPolygon(b, new[] { new PointF(px - 5, prRuler.Y), new PointF(px + 5, prRuler.Y), new PointF(px, prRuler.Bottom) }); }
        var rr = Reg(prRuler, () => { eng.Seek(PrFrameAt(MousePos.X)); }); rr.OnDrag = pt => eng.Seek(PrFrameAt(pt.X)); Hint2(prRuler, "Click or drag to move the song's position");
        // the keyboard (kit tracks: the sample on each key)
        var kgs = g.Save(); g.SetClip(prKeys, CombineMode.Intersect);
        for (int n = PrHigh; n >= PrLow; n--)
        {
            float ky = PrY(n); if (ky + prRowH < prKeys.Y || ky > prKeys.Bottom) continue; bool black = NoteNm[n % 12].Length > 1, on = n == prAudNote; string kitName = sam != null ? sam.KeyName(n) : null;
            Color kc = on ? NeonPink : (kitName != null ? TM.C(255, 70, 40, 130) : (black ? TM.C(255, 16, 9, 30) : TM.C(255, 226, 220, 244)));
            using (var b = new SolidBrush(kc)) g.FillRectangle(b, prKeys.X, ky, black && kitName == null ? prKeys.Width * 0.62f : prKeys.Width, prRowH); using (var p = new Pen(TM.C(90, 60, 40, 100))) g.DrawLine(p, prKeys.X, ky + prRowH, prKeys.Right, ky + prRowH);
            if (kitName != null) Txt(g, kitName, 8, Color.White, Rc(prKeys.X + 4, ky, prKeys.Width - 30, prRowH), StringAlignment.Near, true);
            if (n % 12 == 0 || (prRowH >= 13 && kitName != null)) Txt(g, PrNoteName(n), 7.5f, kitName != null || black ? Color.White : Color.FromArgb(255, 30, 20, 50), Rc(prKeys.Right - 28, ky, 26, prRowH), StringAlignment.Far, false);
        }
        g.Restore(kgs);
        var kr = Reg(prKeys, () => { int kn = PrNoteAt(MousePos.Y); PrAudition(kn, 100); }); kr.OnDrag = pt => { int kn = PrNoteAt(pt.Y); if (kn != prAudNote) PrAudition(kn, 100); }; kr.OnUp = () => PrSilence(); kr.OnWheel = d => PrWheel(d, MousePos); Hint2(prKeys, sam != null && sam.IsKit ? "Each key plays its sample: click to hear it (drag a sound from SOUNDS onto a key to put it there)" : "Click a key to hear it on this track's sound");
        // the velocity lane: each note's velocity, set without moving the note
        using (var b = new SolidBrush(TM.C(255, 14, 7, 28))) g.FillRectangle(b, prVel); StrokeRR(g, Color.FromArgb(90, NeonViolet), prVel, 4, 1f); Txt(g, "VELOCITY", 8, DimC, Rc(x, prVel.Y, keyW - 4, 16), StringAlignment.Far, true);
        var vgs = g.Save(); g.SetClip(prVel, CombineMode.Intersect);
        foreach (var n in prNotes) { float vx = PrX(n.Start); if (vx < prVel.X - 4 || vx > prVel.Right) continue; float vh = (prVel.Height - 6) * n.Vel / 127f; bool sl = prSel.Contains(n); using (var b = new SolidBrush(Color.FromArgb(sl ? 255 : 200, sl ? accSel : acc))) g.FillRectangle(b, vx, prVel.Bottom - 3 - vh, 4, vh); using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, vx - 1.5f, prVel.Bottom - 3 - vh - 3, 7, 7); }
        g.Restore(vgs);
        var vr = Reg(prVel, () => PrVelAt(MousePos, true)); vr.OnDrag = pt => PrVelAt(pt, false); vr.OnUp = () => { prMode = 0; PrCommit(); }; Hint2(prVel, "Drag a bar to set that note's velocity (drag across to set several)");
        if (!prBoxDrag) PaintPatternBox(g, prGrid);
        if (!prDraftDrag) PaintDraftBox(g, prGrid);
        // the arrangement link: where this view sits in the song
        { var mini = Rc(x, prVel.Bottom - 16, keyW - 6, 14); double songBeats = Math.Max(16, SongFrames() / PrBeatFrames); FillRR(g, TM.C(200, 8, 4, 18), mini, 3); float vx0 = (float)(prScrollBeats / songBeats), vw = (float)(prGrid.Width / prPpb / songBeats); FillRR(g, Color.FromArgb(160, NeonCyan), Rc(mini.X + mini.Width * Math.Min(1, vx0), mini.Y + 2, Math.Max(3, mini.Width * Math.Min(1, vw)), mini.Height - 4), 2); Reg(mini, () => { prScrollBeats = Math.Max(0, (MousePos.X - mini.X) / mini.Width * songBeats - prGrid.Width / prPpb / 2); }).OnDrag = pt => { prScrollBeats = Math.Max(0, (pt.X - mini.X) / mini.Width * songBeats - prGrid.Width / prPpb / 2); }; Hint2(mini, "Where you are in the song: drag to move along it"); }
    }
    long SongFrames() { long end = 0; lock (eng.Sync) foreach (var t in eng.Tracks) { if (t.Clip != null) end = Math.Max(end, t.Offset + t.Clip.Length); lock (t.Midi) if (t.Midi.Count > 0) end = Math.Max(end, t.Midi[t.Midi.Count - 1].Frame); } return end; }
    static string PrBeatsName(double b) { if (Math.Abs(b - 4) < 1e-6) return "1 BAR"; if (Math.Abs(b - 2) < 1e-6) return "1/2 BAR"; if (Math.Abs(b - 1) < 1e-6) return "1/4"; if (Math.Abs(b - 0.5) < 1e-6) return "1/8"; if (Math.Abs(b - 0.25) < 1e-6) return "1/16"; if (Math.Abs(b - 0.125) < 1e-6) return "1/32"; if (Math.Abs(b - 1 / 3.0) < 1e-6) return "1/8T"; if (Math.Abs(b - 1 / 6.0) < 1e-6) return "1/16T"; return b.ToString("0.##") + " BEATS"; }
    static string PrSnapShort(int i) { return new[] { "1 BAR", "1/2 BAR", "1/4", "1/8", "1/16", "1/32", "1/8T", "1/16T", "OFF" }[i]; }

    // ---- mouse on the grid ----
    internal PrNote PrHit(PointF p, out bool edge)
    {
        edge = false; for (int i = prNotes.Count - 1; i >= 0; i--) { var n = prNotes[i]; float x0 = PrX(n.Start), x1 = Math.Max(x0 + 4, PrX(n.Start + n.Len)), ny = PrY(n.Note); if (p.Y >= ny && p.Y < ny + prRowH && p.X >= x0 && p.X <= x1) { edge = p.X >= x1 - Math.Min(6, (x1 - x0) / 3); return n; } }
        return null;
    }
    RectangleF PrBox() { var m = MousePos; return RectangleF.FromLTRB(Math.Min(prDownAt.X, m.X), Math.Min(prDownAt.Y, m.Y), Math.Max(prDownAt.X, m.X), Math.Max(prDownAt.Y, m.Y)); }
    void PrDown(PointF p)
    {
        prFocus = true; if (prTrack == null) return; prDownAt = p; prDupDone = false; bool ctrl = (ModifierKeys & Keys.Control) != 0, shift = (ModifierKeys & Keys.Shift) != 0;
        if (ctrl) { prMode = 4; if (!shift) prSel.Clear(); return; }   // a box of notes
        bool edge; var hit = PrHit(p, out edge);
        if (hit != null)
        {
            if (!prSel.Contains(hit)) { prSel.Clear(); prSel.Add(hit); }
            PushUndo(); prOrig = prSel.ToDictionary(n => n, n => n.Copy()); prGrabFrame = PrFrameAt(p.X); prGrabNote = PrNoteAt(p.Y);
            prMode = edge ? 2 : 1; if (shift && !edge) prMode = 3; PrAudition(hit.Note, hit.Vel); return;
        }
        // an empty spot: a new note (or chord) where you clicked, which then follows the mouse
        int note = PrToScale(PrNoteAt(p.Y)); if (note < PrLow || note > PrHigh) return; long start = PrSnapDown(PrFrameAt(p.X));
        PushUndo(); prSel.Clear(); var made = new List<PrNote>();
        foreach (int st in PrChordSteps[prChord]) { int nn = PrToScale(note + st); if (nn > 127) continue; var n = new PrNote { Start = start, Len = PrLenFrames, Note = nn, Vel = 100 }; if (prNotes.Any(o => o.Note == n.Note && o.Start == n.Start)) continue; prNotes.Add(n); prSel.Add(n); made.Add(n); }
        prCreatedTick = Environment.TickCount; PrCommit(); prOrig = prSel.ToDictionary(n => n, n => n.Copy()); prGrabFrame = PrFrameAt(p.X); prGrabNote = PrNoteAt(p.Y); prMode = 1; if (made.Count > 0) PrAudition(made[0].Note, 100);
    }
    void PrDrag(PointF p)
    {
        if (prTrack == null || prMode == 0) return;
        if (prMode == 4) return;   // the box is drawn from MousePos
        long df = PrFrameAt(p.X) - prGrabFrame; int dn = PrNoteAt(p.Y) - prGrabNote;
        if (prMode == 3 && !prDupDone && (Math.Abs(p.X - prDownAt.X) > 4 || Math.Abs(p.Y - prDownAt.Y) > 4))
        {   // Shift+drag: copies move, the originals stay
            var copies = new Dictionary<PrNote, PrNote>(); foreach (var kv in prOrig) { var c = kv.Value.Copy(); prNotes.Add(c); copies[c] = kv.Value.Copy(); } prSel.Clear(); foreach (var c in copies.Keys) prSel.Add(c); prOrig = copies; prDupDone = true; prMode = 1;
        }
        if (prMode == 1)
        {
            var first = prOrig.Values.OrderBy(o => o.Start).First(); long newFirst = PrSnapFrame(Math.Max(0, first.Start + df)); long shift = newFirst - first.Start;
            foreach (var kv in prOrig) { kv.Key.Start = Math.Max(0, kv.Value.Start + shift); int nn = kv.Value.Note + dn; kv.Key.Note = Math.Max(PrLow, Math.Min(PrHigh, prLock ? PrToScale(nn) : nn)); }
            var lead = prOrig.Keys.First(); if (dn != 0 && lead.Note != prAudNote) PrAudition(lead.Note, lead.Vel);
        }
        else if (prMode == 2) { long end = PrFrameAt(p.X); foreach (var kv in prOrig) { long len = kv.Value.Len + (end - prGrabFrame); long e2 = PrSnapBeats[prSnap] > 0 ? PrSnapFrame(kv.Value.Start + len) - kv.Value.Start : len; kv.Key.Len = Math.Max(Math.Max(64, (long)(PrSnapFrames > 0 ? PrSnapFrames : 64)), e2); } }
    }
    void PrUp()
    {
        if (prTrack == null) { prMode = 0; return; }
        if (prMode == 4) { var br = PrBox(); foreach (var n in prNotes) { var nr = RectangleF.FromLTRB(PrX(n.Start), PrY(n.Note), PrX(n.Start + n.Len), PrY(n.Note) + prRowH); if (nr.IntersectsWith(br)) prSel.Add(n); } }
        else if (prMode == 2 && prSel.Count > 0) prLenBeats = prSel.First().Len / PrBeatFrames;   // the next note gets the length you just made
        if (prMode != 0 && prMode != 4) PrCommit(); prMode = 0; prOrig = null; PrSilence();
    }
    void PrRight(PointF p) { prFocus = true; bool e; var hit = PrHit(p, out e); if (hit == null) { PrContextMenu(); return; } PushUndo(); prNotes.Remove(hit); prSel.Remove(hit); PrCommit(); }
    void PrDouble(PointF p) { bool e; var hit = PrHit(p, out e); if (hit == null) return; if (unchecked(Environment.TickCount - prCreatedTick) < 600 && prSel.Contains(hit)) return; /* (the second click of the double-click that just made it) */ PushUndo(); prNotes.Remove(hit); prSel.Remove(hit); PrCommit(); }
    void PrWheel(int d, PointF p)
    {
        var mk = ModifierKeys;
        if ((mk & Keys.Control) != 0) { double old = prPpb, at = (p.X - prGrid.X) / old + prScrollBeats; prPpb = Math.Max(8, Math.Min(480, prPpb * (d > 0 ? 1.2 : 1 / 1.2))); prScrollBeats = Math.Max(0, at - (p.X - prGrid.X) / prPpb); }
        else if ((mk & Keys.Alt) != 0) { float atN = (p.Y - prGrid.Y + prScrollY) / prRowH; prRowH = Math.Max(7, Math.Min(30, prRowH + (d > 0 ? 2 : -2))); prScrollY = atN * prRowH - (p.Y - prGrid.Y); }
        else if ((mk & Keys.Shift) != 0) prScrollBeats = Math.Max(0, prScrollBeats - d / 120.0 * 2);
        else prScrollY -= d / 120f * prRowH * 3;
    }
    void PrVelAt(PointF p, bool first)
    {
        prFocus = true; if (first) { PushUndo(); prMode = 5; }
        PrNote best = null; float bd = 7; foreach (var n in prNotes) { float d = Math.Abs(PrX(n.Start) + 2 - p.X); if (d < bd || (d <= bd + 0.01f && best != null && prSel.Contains(n) && !prSel.Contains(best))) { bd = d; best = n; } }
        if (best == null) return; int v = (int)Math.Round((prVel.Bottom - 3 - p.Y) / (prVel.Height - 6) * 127); best.Vel = Math.Max(1, Math.Min(127, v));
        if (prSel.Contains(best) && prSel.Count > 1 && first) { }
    }

    // ---- editing commands ----
    List<PrNote> prClip = new List<PrNote>();
    internal bool PrKey(Keys k)
    {
        if (!PrShown || !prFocus || prTrack == null) return false; Keys code = k & Keys.KeyCode; bool ctrl = (k & Keys.Control) != 0, shift = (k & Keys.Shift) != 0;
        if (code == Keys.Delete || code == Keys.Back) { if (prSel.Count == 0) return true; PushUndo(); prNotes.RemoveAll(n => prSel.Contains(n)); prSel.Clear(); PrCommit(); return true; }
        if (ctrl && code == Keys.A) { prSel.Clear(); foreach (var n in prNotes) prSel.Add(n); return true; }
        if (ctrl && code == Keys.C) { PrCopy(); return true; }
        if (ctrl && code == Keys.V) { PrPaste(); return true; }
        if (ctrl && code == Keys.D) { PrDuplicate(); return true; }
        if (ctrl && code == Keys.Q) { PrQuantise(); return true; }
        if (!ctrl && (code == Keys.Up || code == Keys.Down) && prSel.Count > 0) { PushUndo(); int d = (code == Keys.Up ? 1 : -1) * (shift ? 12 : 1); foreach (var n in prSel) n.Note = Math.Max(PrLow, Math.Min(PrHigh, n.Note + d)); PrCommit(); var f = prSel.First(); PrAudition(f.Note, f.Vel); return true; }
        if (!ctrl && (code == Keys.Left || code == Keys.Right) && prSel.Count > 0) { PushUndo(); long step = (long)Math.Round(PrSnapFrames > 0 ? PrSnapFrames : PrBeatFrames / 4); long d = code == Keys.Right ? step : -step; if (prSel.Min(n => n.Start) + d < 0) d = -prSel.Min(n => n.Start); foreach (var n in prSel) n.Start += d; PrCommit(); return true; }
        return false;
    }
    void PrCopy() { prClip = prSel.Select(n => n.Copy()).OrderBy(n => n.Start).ToList(); Toast(prClip.Count + " note" + (prClip.Count == 1 ? "" : "s") + " copied"); }
    void PrPaste()
    {
        if (prClip.Count == 0 || prTrack == null) return; PushUndo(); long at = PrSnapFrame((long)DispPos), first = prClip.Min(n => n.Start); prSel.Clear();
        foreach (var c in prClip) { var n = c.Copy(); n.Start = at + (c.Start - first); prNotes.Add(n); prSel.Add(n); } PrCommit(); Toast("Pasted at the song's position");
    }
    internal void PrDuplicate()
    {
        if (prSel.Count == 0 || prTrack == null) return; PushUndo(); long s = prSel.Min(n => n.Start), e = prSel.Max(n => n.Start + n.Len); double bar = 4 * PrBeatFrames; long span = (long)Math.Round(Math.Max(1, Math.Ceiling((e - s) / bar - 1e-9)) * bar);
        var copies = prSel.Select(n => { var c = n.Copy(); c.Start += span; return c; }).ToList(); prSel.Clear(); foreach (var c in copies) { prNotes.Add(c); prSel.Add(c); } PrCommit();
    }
    internal void PrQuantise()
    {
        if (prTrack == null || PrSnapFrames <= 0) { Toast("Pick a snap first (SNAP)."); return; } var set = prSel.Count > 0 ? prSel.ToList() : prNotes.ToList(); if (set.Count == 0) return; PushUndo();
        foreach (var n in set) { long end = n.Start + n.Len; n.Start = PrSnapFrame(n.Start); n.Len = Math.Max((long)PrSnapFrames, PrSnapFrame(end) - n.Start); } PrCommit(); Toast(set.Count + " note" + (set.Count == 1 ? "" : "s") + " quantised to " + PrSnapNames[prSnap]);
    }
    // repeat bars [from, from+bars) along the song (the copies replace what was in those bars)
    internal void PrRepeat(int fromBar, int bars, int times)
    {
        if (prTrack == null || bars <= 0 || times <= 0) return; double bar = 4 * PrBeatFrames; long a = (long)Math.Round(fromBar * bar), len = (long)Math.Round(bars * bar);
        var src = prNotes.Where(n => n.Start >= a && n.Start < a + len).Select(n => n.Copy()).ToList(); if (src.Count == 0) { Toast("Bars " + (fromBar + 1) + " to " + (fromBar + bars) + " have no notes to repeat."); return; } PushUndo();
        long end = a + len * (times + 1); prNotes.RemoveAll(n => n.Start >= a + len && n.Start < end);
        for (int k = 1; k <= times; k++) foreach (var s in src) { var c = s.Copy(); c.Start += len * k; prNotes.Add(c); }
        PrCommit(); Toast("Bars " + (fromBar + 1) + "-" + (fromBar + bars) + " repeated " + times + " time" + (times == 1 ? "" : "s"));
    }

    // ---- menus ----
    void PrStepTrack(int d) { var l = eng.Tracks.Where(x => !x.IsBus).ToList(); if (l.Count == 0) return; int i = l.IndexOf(prTrack); PrSetTrack(l[((i < 0 ? 0 : i + d) % l.Count + l.Count) % l.Count]); }
    void PrTrackMenu()
    {
        var m = Win.NewMenu(); foreach (var t0 in eng.Tracks) { if (t0.IsBus) continue; var t = t0; string kind = PrCanEdit(t) ? (DrumLike(t) ? "drums" : "notes") : (t.Clip != null ? "audio" : "no sound yet"); var it = new ToolStripMenuItem(t.Name + "   (" + kind + ")") { Checked = t == prTrack, ForeColor = Neon(t.Color) }; it.Click += (a, b) => PrSetTrack(t); m.Items.Add(it); }
        if (prDraft != null) m.Items.Insert(0, new ToolStripMenuItem("Your pattern (not on the song yet)", null, (a, b) => { prTrack = prDraft; prStamp = -1; }) { Checked = prTrack == prDraft });
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("New pattern: choose its sound (nothing goes on the song until you drag it)").Click += (a, b) => { prTrack = null; selTrack = -1; prLastSel = -1; Invalidate(); };
        m.Items.Add("New MIDI track (built-in keys)").Click += (a, b) => { var nt = AddMidiTrack(); if (nt != null) PrSetTrack(nt); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrSnapMenu() { var m = Win.NewMenu(); for (int i = 0; i < PrSnapNames.Length; i++) { int k = i; var it = new ToolStripMenuItem(PrSnapNames[i]) { Checked = prSnap == i }; it.Click += (a, b) => prSnap = k; m.Items.Add(it); } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    void PrLenMenu() { var m = Win.NewMenu(); double[] l = { 4, 2, 1, 0.5, 0.25, 0.125, 1 / 3.0, 1 / 6.0 }; foreach (var v0 in l) { double v = v0; var it = new ToolStripMenuItem(PrBeatsName(v)) { Checked = Math.Abs(prLenBeats - v) < 1e-6 }; it.Click += (a, b) => prLenBeats = v; m.Items.Add(it); } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    void PrScaleMenu()
    {
        var m = Win.NewMenu(); var key = new ToolStripMenuItem("Key: " + NoteNm[prKeyRoot]); for (int i = 0; i < 12; i++) { int k = i; var it = new ToolStripMenuItem(NoteNm[i]) { Checked = prKeyRoot == i }; it.Click += (a, b) => prKeyRoot = k; key.DropDownItems.Add(it); } m.Items.Add(key); m.Items.Add(new ToolStripSeparator());
        for (int i = 0; i < PrScales.Length; i++) { int k = i; var it = new ToolStripMenuItem(PrScales[i]) { Checked = prScale == i }; it.Click += (a, b) => prScale = k; m.Items.Add(it); } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrChordMenu() { var m = Win.NewMenu(); for (int i = 0; i < PrChords.Length; i++) { int k = i; var it = new ToolStripMenuItem(PrChords[i]) { Checked = prChord == i }; it.Click += (a, b) => prChord = k; m.Items.Add(it); } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    void PrGhostMenu()
    {
        var m = Win.NewMenu(); var all = new ToolStripMenuItem("Show ghost notes") { Checked = prGhosts }; all.Click += (a, b) => prGhosts = !prGhosts; m.Items.Add(all); m.Items.Add(new ToolStripSeparator());
        int n = 0; foreach (var t0 in eng.Tracks) { var t = t0; if (t == prTrack || !PrCanEdit(t)) continue; n++; var it = new ToolStripMenuItem(t.Name) { Checked = !prGhostOff.Contains(t), ForeColor = Neon(t.Color) }; it.Click += (a, b) => { if (!prGhostOff.Remove(t)) prGhostOff.Add(t); }; m.Items.Add(it); }
        if (n == 0) m.Items.Add(new ToolStripMenuItem("No other note tracks yet") { Enabled = false });
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrPatternMenu()
    {
        var m = Win.NewMenu(); int from = prSel.Count > 0 ? (int)Math.Floor(prSel.Min(n => n.Start) / (4 * PrBeatFrames)) : (int)Math.Floor(prScrollBeats / 4);
        foreach (int bars0 in new[] { 1, 2, 4, 8 }) { int bars = bars0; var it = new ToolStripMenuItem("Repeat bars " + (from + 1) + "-" + (from + bars) + "..."); foreach (int tm0 in new[] { 1, 3, 7, 15 }) { int tm = tm0; it.DropDownItems.Add("x" + (tm + 1) + " in total").Click += (a, b) => PrRepeat(from, bars, tm); } m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Duplicate the selection after itself (Ctrl+D)").Click += (a, b) => PrDuplicate();
        m.Items.Add("Clear bar " + (from + 1)).Click += (a, b) => { double bar = 4 * PrBeatFrames; long s = (long)Math.Round(from * bar), e = (long)Math.Round((from + 1) * bar); PushUndo(); prNotes.RemoveAll(n => n.Start >= s && n.Start < e); prSel.Clear(); PrCommit(); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrContextMenu()
    {
        var m = Win.NewMenu(); m.Items.Add("Select all (Ctrl+A)").Click += (a, b) => { prSel.Clear(); foreach (var n in prNotes) prSel.Add(n); };
        m.Items.Add("Copy (Ctrl+C)").Click += (a, b) => PrCopy(); m.Items.Add("Paste at the song's position (Ctrl+V)").Click += (a, b) => PrPaste(); m.Items.Add("Quantise (Ctrl+Q)").Click += (a, b) => PrQuantise();
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Velocity of the selection: louder").Click += (a, b) => { PushUndo(); foreach (var n in prSel) n.Vel = Math.Min(127, n.Vel + 16); PrCommit(); };
        m.Items.Add("Velocity of the selection: softer").Click += (a, b) => { PushUndo(); foreach (var n in prSel) n.Vel = Math.Max(1, n.Vel - 16); PrCommit(); };
        m.Items.Add("Velocity: alternate loud and soft (hi-hats)").Click += (a, b) => { var l = (prSel.Count > 0 ? prSel.ToList() : prNotes.ToList()).OrderBy(n => n.Start).ToList(); PushUndo(); for (int i = 0; i < l.Count; i++) l[i].Vel = i % 2 == 0 ? 112 : 72; PrCommit(); };
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Delete the selection (Delete)").Click += (a, b) => { PushUndo(); prNotes.RemoveAll(n => prSel.Contains(n)); prSel.Clear(); PrCommit(); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PrSamplesMenu()
    {
        var s = PrSampler; if (s == null) return; var m = Win.NewMenu();
        m.Items.Add("Load a sample (fixed pitch: drums)...").Click += (a, b) => PrLoadSample(s, false);
        m.Items.Add("Load a sample that follows the notes (808, bass, melodic)...").Click += (a, b) => PrLoadSample(s, true);
        var add = new ToolStripMenuItem("Add a sample to the kit on a key..."); foreach (var kv in GmKeys) { int key = kv.Key; add.DropDownItems.Add(kv.Value + "  (" + PrNoteName(key) + ")").Click += (a, b) => PrAddKitSample(s, key); } m.Items.Add(add);
        m.Items.Add(new ToolStripSeparator());
        if (s.Zones.Count == 1 && s.Zones[0].Lo != s.Zones[0].Hi)
        {
            var z = s.Zones[0]; var fp = new ToolStripMenuItem("Fixed pitch (every note plays it as it is)") { Checked = !z.Follow }; fp.Click += (a, b) => { PushUndo(); z.Follow = false; z.OneShot = true; }; m.Items.Add(fp);
            var fo = new ToolStripMenuItem("Follows the notes (root " + PrNoteName(z.Root) + ")") { Checked = z.Follow }; fo.Click += (a, b) => { PushUndo(); z.Follow = true; z.OneShot = false; }; m.Items.Add(fo);
            var root = new ToolStripMenuItem("Root note (plays the sample as it is)"); for (int n = 24; n <= 84; n++) { int nn = n; if (n % 12 != 0 && n % 12 != 5 && n % 12 != 7 && n % 12 != 9) continue; var it = new ToolStripMenuItem(PrNoteName(n)) { Checked = z.Root == n }; it.Click += (a, b) => { PushUndo(); z.Root = nn; }; root.DropDownItems.Add(it); } m.Items.Add(root);
            var os = new ToolStripMenuItem("One-shot (plays to the end, whatever the note's length)") { Checked = z.OneShot }; os.Click += (a, b) => { PushUndo(); z.OneShot = !z.OneShot; }; m.Items.Add(os);
        }
        if (s.Zones.Count > 0) { var rm = new ToolStripMenuItem("Remove a sample"); foreach (var z0 in s.Zones.ToArray()) { var z = z0; rm.DropDownItems.Add(z.Name + (z.Lo == z.Hi ? "  (" + PrNoteName(z.Lo) + ")" : "")).Click += (a, b) => { PushUndo(); lock (s.Zones) s.Zones.Remove(z); }; } m.Items.Add(rm); }
        foreach (var z in s.Zones) if (z.Missing) { m.Items.Add(new ToolStripSeparator()); m.Items.Add(new ToolStripMenuItem("Missing: " + z.Name) { Enabled = false }); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    internal static readonly KeyValuePair<int, string>[] GmKeys = { new KeyValuePair<int, string>(36, "Kick"), new KeyValuePair<int, string>(38, "Snare"), new KeyValuePair<int, string>(39, "Clap"), new KeyValuePair<int, string>(42, "Closed hat"), new KeyValuePair<int, string>(46, "Open hat"), new KeyValuePair<int, string>(41, "Low tom"), new KeyValuePair<int, string>(45, "Mid tom"), new KeyValuePair<int, string>(37, "Rim"), new KeyValuePair<int, string>(49, "Crash"), new KeyValuePair<int, string>(51, "Ride"), new KeyValuePair<int, string>(54, "Percussion"), new KeyValuePair<int, string>(56, "Bell") };
    string PickSampleFile() { using (var d = new OpenFileDialog { Title = "Choose a sample", Filter = "Audio|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.m4a|All files|*.*", InitialDirectory = SoundLibrary.Root }) return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null; }
    void PrLoadSample(SamplerFx s, bool follow) { string f = PickSampleFile(); if (f == null) return; PushUndo(); string err = s.SetSingle(f, null, follow, follow ? 36 : 60); Toast(err ?? "Loaded " + System.IO.Path.GetFileName(f)); }
    void PrAddKitSample(SamplerFx s, int key) { string f = PickSampleFile(); if (f == null) return; PushUndo(); string err = s.AddToKit(f, null, key); Toast(err ?? System.IO.Path.GetFileNameWithoutExtension(f) + " is on " + PrNoteName(key)); }

    // ---- sampler tracks ----
    // a new instrument track with the sampler on it (and its sample, if one is given)
    internal Track NewSamplerTrack(string name, string path, bool follow, int root)
    {
        var s = (SamplerFx)FxRegistry.Create("sampler"); if (s == null) { Toast("The sampler couldn't be started."); return null; } s.Prepare(StudioEngine.SR);
        if (path != null) { string err = s.SetSingle(path, name, follow, root); if (err != null) Toast(err); }
        var t = StudioEngine.NewTrack(name, "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFFFF9A3D); s.Owner = t; lock (eng.Sync) t.Fx.Add(s); AddTrack(t); int i = eng.Tracks.IndexOf(t); if (i >= 0) selTrack = i; return t;
    }
    // a sound from SOUNDS on a sampler track: drums keep their pitch, 808s / basses / notes follow the notes from their root
    internal Track SoundToSampler(SoundItem si)
    {
        if (si == null || !si.IsAudio) return null; if (si.Instant) { string e = InstantLib.Ensure(si); if (e != null) { Toast(e); return null; } }
        bool follow = si.Type == "808" || si.Group == "Bass" || si.Type == "Instrument One-Shot" || si.Type == "Chord Hit"; int root = 60; if (si.Key.Length > 0) { int rt; bool mi; if (SoundLibrary.ParseKey(si.Key, out rt, out mi)) root = (si.Type == "808" ? 24 : (si.Group == "Bass" ? 36 : 48)) + rt; }
        // into the piano roll only (your pattern's sound): nothing goes on the song until you drag the pattern there
        string nm = si.Name.Replace(si.PackName + " - ", ""); if (!PrShown) OpenPianoRoll(null); if (!PrDraftSample(si.Path, nm, follow, root)) return null;
        Toast(nm + " is loaded into the piano roll: click the grid to add notes" + (follow ? " (higher notes play it higher)" : "") + ", then drag the pattern box onto the song."); return prDraft;
    }
    // your pattern (the scratch pad of the piano roll) plays this sample
    bool PrDraftSample(string path, string name, bool follow, int root)
    {
        var smp = FxRegistry.Create("sampler") as SamplerFx; if (smp == null) return false; smp.Prepare(StudioEngine.SR); string err = smp.SetSingle(path, name, follow, root); if (err != null) { Toast(err); return false; }
        if (prDraft == null) { prDraft = StudioEngine.NewTrack("Your pattern", "inst", null); prDraft.Instrument = true; prDraft.Color = unchecked((int)0xFFFF9A3D); }
        lock (eng.Sync) { prDraft.Fx.RemoveAll(x => x is InstrumentFx); smp.Owner = prDraft; prDraft.Fx.Insert(0, smp); } prDraft.Name = "Your pattern: " + name; eng.Draft = prDraft; prTrack = prDraft; prStamp = -1; prScrollY = -1; Invalidate(); return true;
    }
    // an audio track's clip into a sampler (the audio track stays as it is)
    void PrAudioToSampler(Track a)
    {
        if (a == null || a.Clip == null) return; string path = a.Clip.Path; if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) { path = System.IO.Path.Combine(StudioEnv.DataDir, "samples", "clip-" + DateTime.Now.Ticks + ".wav"); SoundIO.WriteWav(path, a.Clip.L, a.Clip.R, StudioEngine.SR, 16); }
        // into the piano roll only: your pattern plays this audio from notes (nothing goes on the song until you drag the pattern there)
        if (!PrDraftSample(path, a.Name, true, 60)) return;
        Toast(a.Name + " is loaded into the piano roll: C4 plays it as it is, other keys play it higher or lower. Write notes, then drag the pattern box onto the song.");
    }
    // a sound dropped on the piano roll: on a kit's key (or the key under the mouse), or as the sample of an empty sampler
    internal bool PrDrop(SoundItem si, Point p)
    {
        if (!PrShown || si == null || !si.IsAudio || !(prKeys.Contains(p) || prGrid.Contains(p) || prArea.Contains(p))) return false; var s = PrSampler; if (si.Instant) { string e = InstantLib.Ensure(si); if (e != null) { Toast(e); return true; } }
        if (s == null)
        {   // into your pattern: a sampler that plays this sound (a drum keeps its pitch, an 808 / bass / note follows the notes)
            bool follow = si.Type == "808" || si.Group == "Bass" || si.Type == "Instrument One-Shot" || si.Type == "Chord Hit"; int root = 60; int rt; bool mi; if (follow && si.Key.Length > 0 && SoundLibrary.ParseKey(si.Key, out rt, out mi)) root = (si.Type == "808" ? 24 : (si.Group == "Bass" ? 36 : 48)) + rt;
            var smp = FxRegistry.Create("sampler") as SamplerFx; if (smp == null) return true; smp.Prepare(StudioEngine.SR); string e2 = smp.SetSingle(si.Path, si.Name.Replace(si.PackName + " - ", ""), follow, root); if (e2 != null) { Toast(e2); return true; }
            if (prDraft == null) { prDraft = StudioEngine.NewTrack("Your pattern", "inst", null); prDraft.Instrument = true; prDraft.Color = unchecked((int)0xFFFF9A3D); }
            lock (eng.Sync) { prDraft.Fx.RemoveAll(x => x is InstrumentFx); smp.Owner = prDraft; prDraft.Fx.Insert(0, smp); } prDraft.Name = "Your pattern: " + si.Name.Replace(si.PackName + " - ", ""); eng.Draft = prDraft; prTrack = prDraft; prStamp = -1; prScrollY = -1; Invalidate();
            Toast(si.Name.Replace(si.PackName + " - ", "") + " is your pattern's sound: write notes (or GENERATE), then drag the pattern box onto the song. Nothing goes on your tracks until you do."); return true;
        }
        PushUndo(); int key = PrNoteAt(p.Y); string err = s.Zones.Count == 0 && prGrid.Contains(p) ? s.SetSingle(si.Path, si.Name.Replace(si.PackName + " - ", ""), false, 60) : s.AddToKit(si.Path, si.Name.Replace(si.PackName + " - ", ""), key);
        Toast(err ?? si.Name.Replace(si.PackName + " - ", "") + (s.IsKit ? " is on " + PrNoteName(key) : " loaded")); return true;
    }
}
