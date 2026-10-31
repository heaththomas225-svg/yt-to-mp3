using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

// PURPHEX SOUNDS in the studio: a browser panel (search, filters, preview, drag onto the timeline) and the pack manager.
partial class StudioForm
{
    bool soundsOpen, packsOpen, soundsLoaded, sndFull;   /* sndFull: the sounds panel fills the whole Mix page */ TextBox soundBox; readonly SoundLibrary.Filter sndFilter = new SoundLibrary.Filter(); List<SoundItem> sndResults = new List<SoundItem>(); SoundItem sndSel; float sndScroll; bool sndDirty = true; string sndText = "";
    SoundItem sndDrag; PointF sndDragFrom; bool sndDragging; Size soundLayout;
    // pack manager and installing
    readonly Queue<string> installQ = new Queue<string>(); string installing = ""; volatile float installPct; volatile string installNote = ""; volatile bool installCancel; string packSection = "All"; float packScroll; string packMsg = ""; readonly Dictionary<string, long> packBytes = new Dictionary<string, long>();

    void OpenSounds()
    {
        if (!soundsLoaded) { soundsLoaded = true; LibLoader.Done = () => { try { BeginInvoke(new Action(() => { sndDirty = true; Invalidate(); })); } catch { } }; LibLoader.Start(); }
        tab = 0; soundsOpen = true; sndDirty = true; EnsureSoundBox();
    }
    // the SOUND PACKS button: the packs first (install, browse); SOUNDS is one click away
    internal void OpenPacks() { OpenSounds(); packsOpen = true; packScroll = 0; }
    // the two tabs at the top of both screens
    void SoundTabs(Graphics g, float x, float y, bool packs)
    {
        var a = Rc(x, y, 132, 28); var b = Rc(x + 136, y, 96, 28); var c = Rc(x + 236, y, 124, 28); bool mineOn = !packs && sndFilter.Pack == SoundLibrary.UserPackId;
        Btn(g, a, "SOUND PACKS", packs ? 4 : 0, () => { packsOpen = true; packScroll = 0; }, 12); Hint2(a, "The packs: install them (each makes real, checked sound files on this PC) and browse what is in them");
        Btn(g, b, "SOUNDS", packs || mineOn ? 0 : 4, () => { packsOpen = false; if (sndFilter.Pack == SoundLibrary.UserPackId) sndFilter.Pack = ""; sndDirty = true; }, 12); Hint2(b, "Search every installed sound: loops, one-shots, 808s and MIDI, with filters");
        // MY SOUNDS: everything you imported, always one click away (it was only in the genre list before, so imports were hard to find)
        int mine = 0; lock (SoundLibrary.Lock) { var up = SoundLibrary.Packs.Find(q => q.Id == SoundLibrary.UserPackId); if (up != null) mine = up.Items.Count; }
        Btn(g, c, "MY SOUNDS" + (mine > 0 ? " (" + mine + ")" : ""), mineOn ? 4 : 0, () => { ShowMySounds(); if (mine == 0) Toast("No imported sounds yet: press IMPORT AUDIO to add sounds from your computer. They stay here for good."); }, 11);
        Hint2(c, mine > 0 ? "The " + mine + " sound" + (mine == 1 ? "" : "s") + " you imported (kept in your library for good)" : "Your imported sounds will be here (press IMPORT AUDIO)");
    }
    void CloseSounds() { soundsOpen = false; packsOpen = false; sndFull = false; Auditioner.Stop(); }
    TextBox packBox; internal string packQuery = "";
    // the search box of SOUND PACKS: every word must be in the pack's name, genre, style, mood, tags or description
    internal static bool PackMatches(Recipe r, string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true; string hay = (r.Name + " " + r.Genre + " " + r.Sub + " " + r.Section + " " + r.Mood + " " + r.Tags + " " + r.Desc + " " + string.Join(" ", r.Flavours) + " " + string.Join(" ", Array.ConvertAll(r.Bpms, b => b.ToString("0") + "bpm"))).ToLowerInvariant();
        foreach (var w in q.ToLowerInvariant().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) if (hay.IndexOf(w, StringComparison.Ordinal) < 0 && hay.IndexOf(w.TrimEnd('s'), StringComparison.Ordinal) < 0) return false;
        return true;
    }
    void EnsurePackBox()
    {
        if (packBox != null) return;
        packBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 11f) }; Win.NeonBox(packBox); Controls.Add(packBox); Win.DarkControl(packBox);
        packBox.TextChanged += (s, e) => { packQuery = packBox.Text.Trim(); packScroll = 0; Invalidate(); };
    }
    void EnsureSoundBox()
    {
        if (soundBox != null) return;
        soundBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 11f) }; Win.NeonBox(soundBox); Controls.Add(soundBox); Win.DarkControl(soundBox);
        soundBox.TextChanged += (s, e) => { sndFilter.Text = soundBox.Text.Trim(); sndDirty = true; sndScroll = 0; Invalidate(); };
    }
    void RefreshSounds()
    {
        sndResults = SoundLibrary.Search(sndFilter); sndDirty = false; if (sndSel != null && !sndResults.Contains(sndSel)) { /* keep showing the selected sound even if the filter hides it */ }
    }

    // ---- adding a sound to the song ----
    static int GroupColour(string g) { switch (g) { case "Drums": return unchecked((int)0xFFFF9A3D); case "Bass": return unchecked((int)0xFF9B6BFF); case "Synth": return unchecked((int)0xFF3DD9FF); case "FX": return unchecked((int)0xFFFF5CC8); case "Vocal": return unchecked((int)0xFFFF6BA0); case "Loops": return unchecked((int)0xFFFFD23D); default: return unchecked((int)0xFF6BFF9A); } }

    // returns null if it worked, or a message
    string AddSample(SoundItem s, long frame, int insertAt)
    {
        if (s == null) return "Pick a sound first."; if (!s.Instant && !s.Exists) return "This file is missing (" + s.Rel + "). Reinstall the pack in the pack manager."; if (busy) return "Wait for the current job to finish.";
        if (s.Kind == "preset") return InstallPreset(s);
        if (s.Kind == "midi") return DropMidi(s, frame, insertAt);
        if (s.Instant) { string err = InstantLib.Ensure(s); if (err != null) return err; }   // an instant loop is made the first time it is used
        float[] l, r; int sr; string fmt; if (!SoundIO.Read(s.Path, out l, out r, out sr, out fmt)) return "Couldn't read " + s.Rel + ".";
        if (sr != StudioEngine.SR && sr > 0) { l = Resample(l, sr); r = Resample(r, sr); }
        var clip = AudioClip.FromArrays(l, r, s.Name); clip.Path = s.Path; var t = StudioEngine.NewTrack(s.Name, "audio", clip); t.Color = GroupColour(s.Group); t.Offset = Math.Max(0, frame);
        float[] tr0 = SoundTrim(s); if (tr0 != null) { long n0 = clip.Length; t.ClipStart = (long)(tr0[0] * n0); t.ClipFrames = Math.Max(64, (long)((tr0[1] - tr0[0]) * n0)); }   // TRIM in MY SOUNDS: only the part you kept (the file itself is never changed)
        else if (s.Bpm <= 0) TrimLeadingSilence(t);   // AUTO TRIM a one-shot or a file of yours: it starts where the sound starts (a tempo loop keeps its start, its silence is part of the rhythm)
        bool first = eng.Tracks.Count == 0; if (first && s.Bpm > 0) Fx.ProjectBpm = s.Bpm; nextInsert = insertAt; PlaceDropped(t); if (s.PackId.Length > 0) SoundLibrary.MarkUsed(s);
        if (!first && s.Bpm > 0 && Math.Abs(s.Bpm - Fx.ProjectBpm) > 1.5) Toast("Added. This loop is " + s.Bpm.ToString("0") + " BPM and your project is " + Fx.ProjectBpm.ToString("0") + " BPM."); else Toast("Added " + s.Name);
        return null;
    }
    static float[] Resample(float[] x, int from) { return HqResample.Run(x, from, StudioEngine.SR); }   // windowed sinc: a straight line between samples made pack sounds gritty

    // a MIDI file dropped on an instrument track puts its notes there
    string DropMidi(SoundItem s, long frame, int laneIndex)
    {
        if (s.Instant) { string err = InstantLib.Ensure(s); if (err != null) return err; }
        Track t = laneIndex >= 0 && laneIndex < eng.Tracks.Count ? eng.Tracks[laneIndex] : (laneIndex < 0 ? Selected : null);   // dropped below the last row: always a new track
        double bpm; var notes = SoundIO.ReadMidi(s.Path, out bpm); if (notes.Count == 0) return "That MIDI file has no notes.";
        if (t == null || !t.Instrument) return MidiToNewTrack(s, notes, bpm, frame); double spb = 60.0 / Fx.ProjectBpm * StudioEngine.SR; PushUndo();
        lock (t.Midi) { foreach (var n in notes) { long on = frame + (long)(n[0] * spb), off = frame + (long)((n[0] + n[1]) * spb); t.Midi.Add(new MidiEv { Frame = on, S = 0x90, D1 = (byte)n[2], D2 = (byte)Math.Max(1, Math.Min(127, n[3])) }); t.Midi.Add(new MidiEv { Frame = Math.Max(on + 1, off), S = 0x80, D1 = (byte)n[2], D2 = 0 }); } t.Midi.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
        selTrack = eng.Tracks.IndexOf(t); SoundLibrary.MarkUsed(s); Toast("Added " + notes.Count + " notes to " + t.Name); return null;
    }
    // MIDI dropped where there is no instrument track: drums become an audio track played with the pack's kit; a bassline, chords or a melody get a new Bass Lab track with a sound that suits them
    internal string MidiToNewTrack(SoundItem s, List<double[]> notes, double bpm, long frame)
    {
        string ty = (s.Type + " " + s.Name).ToLowerInvariant(); var rec = SoundCatalog.Find(s.PackId) ?? SoundCatalog.Recipes[0]; double spb = 60.0 / Fx.ProjectBpm * StudioEngine.SR; PushUndo();
        if (ty.Contains("drum"))
        {
            double end = 0; foreach (var n in notes) end = Math.Max(end, n[0] + n[1]); end = Math.Max(4, Math.Ceiling(end / 4) * 4);
            var kit = DrumKit.Make(PackBuilder.StyleFor(rec), rec.Seed + 1); var b = PackWriter.Finish(Instruments.RenderDrums(kit, notes, Fx.ProjectBpm, end), "Drum Loop", "loop", -1.5); b = PackBuilder.LoopLen(b, end, Fx.ProjectBpm);
            var clip = AudioClip.FromArrays(b.L, b.R, s.Name); var tr = StudioEngine.NewTrack(s.Name.Replace(s.PackName + " - ", "") + " (drums)", "audio", clip); tr.Color = GroupColour("Drums"); tr.Offset = Math.Max(0, frame); AddTrack(tr); SoundLibrary.MarkUsed(s);
            Toast("Drum MIDI played with " + rec.Name + "'s kit on a new track, at your project tempo"); return null;
        }
        var fx = FxRegistry.Create("synth") as SynthFx; if (fx == null) return "Bass Lab couldn't be started.";
        fx.Prepare(StudioEngine.SR); fx.ResetAll(true); string patch, label;
        if (ty.Contains("bass") || ty.Contains("808")) { string fl = rec.Flavours != null && rec.Flavours.Length > 0 && rec.Flavours[0].Length > 0 ? rec.Flavours[0] : "sub"; patch = Music.BassPatch(fl, new Rnd(rec.Seed)); label = "Bass"; }
        else if (ty.Contains("chord")) { patch = Music.LeadPatch("Electric Keys"); label = "Keys"; }
        else { patch = Music.LeadPatch("Soft Pluck"); label = "Lead"; }
        fx.Deserialize(patch); var t = StudioEngine.NewTrack(label + ": " + s.Name.Replace(s.PackName + " - ", ""), "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFFFFB84D); fx.Owner = t; lock (eng.Sync) t.Fx.Add(fx); AddTrack(t);
        lock (t.Midi) { foreach (var n in notes) { long on = frame + (long)(n[0] * spb), off = frame + (long)((n[0] + n[1]) * spb); t.Midi.Add(new MidiEv { Frame = on, S = 0x90, D1 = (byte)n[2], D2 = (byte)Math.Max(1, Math.Min(127, n[3])) }); t.Midi.Add(new MidiEv { Frame = Math.Max(on + 1, off), S = 0x80, D1 = (byte)n[2], D2 = 0 }); } t.Midi.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
        selTrack = eng.Tracks.IndexOf(t); SoundLibrary.MarkUsed(s); Toast("Added a Bass Lab track with the " + notes.Count + " notes (change its sound in Bass Lab)"); return null;
    }
    string InstallPreset(SoundItem s)
    {
        if (s.Path.EndsWith(".purphexwobble", StringComparison.OrdinalIgnoreCase))   // a wobble preset: a new track with the Purphex Wobble on it, ready to play in the piano roll
        {
            try
            {
                var w = FxRegistry.Create("wobble") as WobbleFx; if (w == null) return "The Purphex Wobble couldn't be started."; w.Prepare(StudioEngine.SR); w.Deserialize(File.ReadAllText(s.Path)); w.CurPreset = s.Name.Replace(s.PackName + " - ", "");
                var t = StudioEngine.NewTrack(w.CurPreset, "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFF9B5CFF); w.Owner = t; lock (eng.Sync) t.Fx.Add(w); AddTrack(t); int ti = eng.Tracks.IndexOf(t); if (ti >= 0) selTrack = ti;
                SoundLibrary.MarkUsed(s); OpenPianoRoll(t); Toast("A new Wobble track with " + w.CurPreset + ": play it from the keyboard or write notes in the piano roll"); return null;
            }
            catch (Exception ex) { return "Couldn't open the preset: " + ex.Message; }
        }
        try { Directory.CreateDirectory(SynthPresets.Dir); string name = s.Name; string dest = Path.Combine(SynthPresets.Dir, PackWriter.Clean(name) + ".txt"); File.Copy(s.Path, dest, true); SoundLibrary.MarkUsed(s); Toast("Added to Bass Lab: open its preset browser > My presets > " + name); return null; }
        catch (Exception ex) { return "Couldn't install the preset: " + ex.Message; }
    }

    Track dropHost;
    // a sound dropped on a row goes onto that row (into an empty track, or overlapping what is already there); below the last row it makes a new track
    void PlaceDropped(Track t)
    {
        var row = dropHost; dropHost = null;
        if (row != null && row.Host == null && !row.IsBus && t.Clip != null)
        {
            if (row.Clip == null && !row.Instrument) { PushUndo(); lock (eng.Sync) { row.Clip = t.Clip; row.Source = t.Source; row.ClipStart = 0; row.ClipFrames = -1; row.Offset = t.Offset; row.Color = t.Color; if (row.Name.StartsWith("Audio ")) row.Name = t.Name; } selTrack = Math.Max(0, eng.Tracks.IndexOf(row)); return; }
            t.Host = row; t.Out = row.Out; AddTrack(t); selTrack = Math.Max(0, eng.Tracks.IndexOf(row)); return;
        }
        AddTrack(t);
    }

    // Up / Down after a click in the sound list: the next or previous sound is picked (and heard, with Autoplay), and the list follows it
    float sndListH = 300;
    internal bool SoundListKey(bool down)
    {
        if (sndResults.Count == 0) return false; int i = sndSel == null ? -1 : sndResults.IndexOf(sndSel); i = Math.Max(0, Math.Min(sndResults.Count - 1, i < 0 ? 0 : i + (down ? 1 : -1)));
        SelectSound(sndResults[i], true); const float rowH = 52; if (i * rowH < sndScroll) sndScroll = i * rowH; else if ((i + 1) * rowH > sndScroll + sndListH) sndScroll = (i + 1) * rowH - sndListH; Invalidate(); return true;
    }

    // ---- duplicates found by an import: LOCATE shows each sound it was a copy of (one after another), so you can find the one you already have ----
    readonly List<SoundItem> dupeFind = new List<SoundItem>(); int dupeAt;
    internal void NoteDupes() { dupeFind.Clear(); dupeAt = 0; foreach (var d in SoundLibrary.LastDupeItems) if (d != null && !dupeFind.Contains(d)) dupeFind.Add(d); }
    internal void LocateDupe()
    {
        if (dupeFind.Count == 0) return; var it = dupeFind[dupeAt % dupeFind.Count]; dupeAt = (dupeAt + 1) % dupeFind.Count; it = SoundLibrary.Find(it.Id) ?? it;
        ShowMySounds(); if (soundBox != null) soundBox.Text = it.Name; sndFilter.Text = it.Name; sndDirty = true; SelectSound(it, false); Toast("Here it is: " + it.Name + " (already in MY SOUNDS).");
    }
    void PaintDupeBar(Graphics g, RectangleF r)
    {
        var gold = Color.FromArgb(255, 255, 206, 84); FillRR(g, Color.FromArgb(70, 120, 90, 10), r, 5); StrokeRR(g, Color.FromArgb(200, gold), r, 5, 1f);
        int n = dupeFind.Count; Txt(g, n + " duplicate" + (n == 1 ? " was" : "s were") + " already in MY SOUNDS", 9, gold, Rc(r.X + 8, r.Y, r.Width - 180, r.Height), StringAlignment.Near, true);
        var lb = Rc(r.Right - 140, r.Y + 1, 112, r.Height - 2); Btn(g, lb, n > 1 ? "LOCATE (" + (dupeAt % n + 1) + "/" + n + ")" : "LOCATE", 2, () => LocateDupe(), 9); Hint2(lb, "Show the sound you already have (each press shows the next one)");
        var xb = Rc(r.Right - 24, r.Y + 1, 22, r.Height - 2); Btn(g, xb, "x", 0, () => dupeFind.Clear(), 9); Hint2(xb, "Hide this");
    }

    // ---- TRIM in the sounds panel: drag the two handles on the waveform to keep only a part of a sound; dragging it into the song, a pad or a key
    //      then uses that part. The sound file is never changed (the trim is remembered for that sound, and RESET puts it back to the whole sound). ----
    Dictionary<string, float[]> sndTrims;
    static string TrimFile { get { return Path.Combine(StudioEnv.DataDir, "sound_trims.txt"); } }
    internal float[] SoundTrim(SoundItem s)
    {
        if (sndTrims == null) { sndTrims = new Dictionary<string, float[]>(); try { if (File.Exists(TrimFile)) foreach (var ln in File.ReadAllLines(TrimFile)) { var p = ln.Split('|'); float a, b; if (p.Length == 3 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a) && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b)) sndTrims[p[0]] = new[] { a, b }; } } catch { } }
        float[] t; return s != null && sndTrims.TryGetValue(s.Id, out t) ? t : null;
    }
    internal void SetSoundTrim(SoundItem s, float a, float b)
    {
        SoundTrim(s); a = Math.Max(0, Math.Min(1, a)); b = Math.Max(0, Math.Min(1, b)); if (b - a < 0.01f) return;
        if (a <= 0.0005f && b >= 0.9995f) sndTrims.Remove(s.Id); else sndTrims[s.Id] = new[] { a, b };
        try { Directory.CreateDirectory(StudioEnv.DataDir); var l = new List<string>(); foreach (var kv in sndTrims) l.Add(kv.Key + "|" + kv.Value[0].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + kv.Value[1].ToString("R", System.Globalization.CultureInfo.InvariantCulture)); File.WriteAllLines(TrimFile, l.ToArray()); } catch { }
    }
    // the trimmed part as a WAV of its own (for a pad or a key), made once per trim
    string TrimmedFile(SoundItem s, string path)
    {
        var t = SoundTrim(s); if (t == null) return path;
        try
        {
            string dir = Path.Combine(StudioEnv.DataDir, "trimmed"); Directory.CreateDirectory(dir); string nm = s.Name; foreach (char ch in Path.GetInvalidFileNameChars()) nm = nm.Replace(ch, ' ');
            string f = Path.Combine(dir, nm + " (" + (int)(t[0] * 10000) + "-" + (int)(t[1] * 10000) + ").wav"); if (File.Exists(f)) return f;
            float[] l, r; int sr; string fmt; if (!SoundIO.Read(path, out l, out r, out sr, out fmt)) return path; r = r ?? l; int a = (int)(t[0] * l.Length), n = Math.Max(64, (int)((t[1] - t[0]) * l.Length)); n = Math.Min(n, l.Length - a);
            var L = new float[n]; var R = new float[n]; Array.Copy(l, a, L, 0, n); Array.Copy(r, a, R, 0, n); int fd = Math.Min(n / 4, sr / 500); for (int i = 0; i < fd; i++) { float k = i / (float)fd; L[i] *= k; R[i] *= k; L[n - 1 - i] *= k; R[n - 1 - i] *= k; }   // (2 ms fades: no click at the cut)
            WavIO.Write(f, L, R, sr, 24); return f;
        }
        catch { return path; }
    }
    void PaintSoundTrim(Graphics g, RectangleF wf, SoundItem s)
    {
        var t = SoundTrim(s); float a = t == null ? 0 : t[0], b = t == null ? 1 : t[1]; float xa = Math.Max(wf.X, Math.Min(wf.Right, SndX(a, wf))), xb = Math.Max(wf.X, Math.Min(wf.Right, SndX(b, wf))); bool showA = SndX(a, wf) >= wf.X - 1 && SndX(a, wf) <= wf.Right + 1, showB = SndX(b, wf) >= wf.X - 1 && SndX(b, wf) <= wf.Right + 1; var gold = Color.FromArgb(255, 255, 206, 84);
        using (var dim = new SolidBrush(Color.FromArgb(170, 6, 3, 16))) { if (xa > wf.X) g.FillRectangle(dim, wf.X, wf.Y, xa - wf.X, wf.Height); if (xb < wf.Right) g.FillRectangle(dim, xb, wf.Y, wf.Right - xb, wf.Height); }
        if (t != null) using (var pen = new Pen(Color.FromArgb(200, gold), 1.2f)) { g.DrawLine(pen, xa, wf.Y, xb, wf.Y); g.DrawLine(pen, xa, wf.Bottom - 1, xb, wf.Bottom - 1); }
        Action<float, bool> handle = (x, left) =>
        {
            using (var pen = new Pen(gold, 2f)) g.DrawLine(pen, x, wf.Y, x, wf.Bottom); var grip = Rc(left ? x : x - 9, wf.Y + wf.Height / 2 - 11, 9, 22); FillRR(g, gold, grip, 3);
            using (var pen = new Pen(Color.FromArgb(200, 60, 30, 0), 1f)) for (int k = -1; k <= 1; k++) g.DrawLine(pen, grip.X + 2, grip.Y + 11 + k * 4, grip.Right - 2, grip.Y + 11 + k * 4);
            var hr = Reg(Rc(x - 7, wf.Y, 14, wf.Height), () => { }); hr.Button = true;
            hr.OnDrag = pt => { float f = (float)SndF(pt.X, wf); var cur = SoundTrim(s); float ca = cur == null ? 0 : cur[0], cb = cur == null ? 1 : cur[1]; if (left) SetSoundTrim(s, Math.Min(f, cb - 0.01f), cb); else SetSoundTrim(s, ca, Math.Max(f, ca + 0.01f)); };
            Hint2(hr.R, left ? "TRIM: drag to where the part you want starts. Dragging the sound into the song, a pad or a key then uses only this part (the file is not changed)." : "TRIM: drag to where the part you want ends. The file itself is not changed.");
        };
        if (showA) handle(xa, true); if (showB) handle(xb, false);   // (zoomed in, a handle off the view is not drawn: zoom out or press FIT to reach it)
        if (t != null)
        {
            double len = s.Dur > 0 ? s.Dur : 0; Txt(g, "TRIMMED" + (len > 0 ? "  " + (a * len).ToString("0.00") + " - " + (b * len).ToString("0.00") + " s" : ""), 8, gold, Rc(wf.X + 6, wf.Y + 2, wf.Width - 70, 12), StringAlignment.Near, true);
            var rb = Rc(wf.Right - 58, wf.Y + 3, 54, 16); Btn(g, rb, "RESET", 0, () => SetSoundTrim(s, 0, 1), 8); Hint2(rb, "Use the whole sound again");
        }
        else if (Hot(wf)) Txt(g, "DRAG THE GOLD HANDLES TO TRIM", 8, Color.FromArgb(220, gold), Rc(wf.X + 14, wf.Y + 2, wf.Width - 28, 12), StringAlignment.Center, true);
    }

    // a sound let go over a window that takes sounds (Purphex Pads, Key Map): made on this PC first if it is a library sound that is made when first used
    bool SoundToWindow(SoundItem s)
    {
        var w = SoundDrop.At(Cursor.Position); if (w == null) return false;
        if (s.Spec.Length > 0) { string err = InstantLib.Ensure(s); if (err != null) { Toast(err); return true; } }
        string path = s.Path; if (string.IsNullOrEmpty(path) || !File.Exists(path) || !SoundSources.IsAudio(path)) { Toast(s.Name + " is not a sound file a pad can play (MIDI and loops-to-be go on the song or the piano roll)."); return true; }
        path = TrimmedFile(s, path); w.DropSound(Cursor.Position, path, s.Name.Replace(s.PackName + " - ", "")); return true;
    }
    // dropped from the browser onto the timeline
    void DropSampleAt(Point p)
    {
        if (sndDrag != null && SoundToWindow(sndDrag)) { sndDrag = null; sndDragging = false; SoundDrop.Hover = Point.Empty; return; }   // over Purphex Pads or Key Map: the pad or key under the mouse takes it
        if (sndDrag != null && BlDrop(sndDrag, p)) { sndDrag = null; sndDragging = false; return; }   // on a Beat Lab pad: the pad plays it
        if (sndDrag != null && PrDrop(sndDrag, p)) return;   // on the piano roll: into the sampler (a kit's key, or its sample)
        var s = sndDrag; sndDrag = null; sndDragging = false; if (s == null) return; var tl = TimelineRect(); float top = ArrTop + RulerH;
        if (p.X < HeadW || p.X > tl.Right || p.Y < top) { Toast("Drop it on the timeline (the area to the right of the track names)."); return; }
        long frame = DropFrame(p.X); int lane = (int)((p.Y - top + vscroll) / LaneH); int at = lane >= LaneCount ? LaneCount : lane + 1;
        dropHost = lane >= 0 && lane < LaneCount ? eng.Tracks[lane] : null;   // dropped on a row: it goes on that row (overlapping whatever is there)
        string msg = AddSample(s, frame, s.Kind == "midi" ? lane : at); dropHost = null; if (msg != null) Toast(msg);
    }
    // where a sound dragged from the packs lands: exactly under the mouse, so it can go anywhere (hold Shift to snap it to the grid)
    long DropFrame(float x) { long f = Math.Max(0, scroll + (long)((x - HeadW) * spp)); return (ModifierKeys & Keys.Shift) != 0 ? Snap(f) : f; }
    void AddSampleAtNeedle(SoundItem s) { string msg = AddSample(s, Snap(DispPos), eng.Tracks.Count); if (msg != null) Toast(msg); }

    // the sounds panel while the library loads in the background
    void PaintLibraryLoading(Graphics g)
    {
        float x = ClientSize.Width - PanelW, y = ToolH + TransH, w = PanelW, h = ClientSize.Height - y; var pr = Rc(x, y, w, h);
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, pr); using (var pen = new Pen(Color.FromArgb(150, NeonViolet))) g.DrawLine(pen, x, y, x, y + h); Reg(pr, () => { });
        if (soundBox != null && soundBox.Visible) soundBox.Visible = false; if (packBox != null && packBox.Visible) packBox.Visible = false;
        var cl = Rc(x + w - 38, y + 8, 28, 26); Btn(g, cl, "x", 0, () => CloseSounds(), 13);
        Txt(g, "LOADING THE SOUND LIBRARY...", 16, Ink, Rc(x, y + h / 2 - 40, w, 26), StringAlignment.Center, true);
        Txt(g, "Your sounds are being read in the background. Keep working: this panel fills in by itself in a few seconds.", 11, DimC, Rc(x + 30, y + h / 2 - 8, w - 60, 40), StringAlignment.Center, false);
        float ph = (Environment.TickCount % 1600) / 1600f; var bar = Rc(x + w / 2 - 120, y + h / 2 + 40, 240, 6); FillRR(g, TM.C(200, 8, 4, 18), bar, 3); FillRR(g, NeonCyan, Rc(bar.X + (bar.Width - 60) * ph, bar.Y, 60, bar.Height), 3); Invalidate();
    }
    // a MIDI file's notes as a small piano-roll picture (read once and kept); the preview also shows where it is playing
    readonly Dictionary<string, List<double[]>> midiPics = new Dictionary<string, List<double[]>>();
    void DrawMidiPic(Graphics g, RectangleF r, SoundItem s, Color col, bool big)
    {
        List<double[]> notes; string key = s.Id + "|" + s.Bpm + "|" + s.Key;
        if (!midiPics.TryGetValue(key, out notes))
        {
            notes = null; try { if (s.Instant) notes = InstantLib.MidiNotesFor(s); else if (File.Exists(s.Path)) { double bpm; notes = SoundIO.ReadMidi(s.Path, out bpm); } } catch { notes = null; }
            if (midiPics.Count > 600) midiPics.Clear(); midiPics[key] = notes ?? new List<double[]>();
        }
        if (notes == null || notes.Count == 0) { Txt(g, "MIDI", 9, DimC, r, StringAlignment.Center, true); return; }
        double end = 0; int lo = 127, hi = 0; foreach (var n in notes) { end = Math.Max(end, n[0] + n[1]); lo = Math.Min(lo, (int)n[2]); hi = Math.Max(hi, (int)n[2]); }
        end = Math.Max(4, Math.Ceiling(end / 4) * 4); int span = Math.Max(6, hi - lo + 1); float rowH = r.Height / span;
        if (big) using (var gp = new Pen(Color.FromArgb(40, col))) for (int b = 0; b <= (int)end; b += 4) { float gx = r.X + (float)(b / end) * r.Width; g.DrawLine(gp, gx, r.Y, gx, r.Bottom); }
        using (var br = new SolidBrush(col))
            foreach (var n in notes) { float x = r.X + (float)(n[0] / end) * r.Width, w = Math.Max(1.5f, (float)(n[1] / end) * r.Width - 0.5f), y = r.Bottom - ((int)n[2] - lo + 1) * rowH; g.FillRectangle(br, x, y, w, Math.Max(1.5f, rowH - (big ? 1 : 0.4f))); }
        if (big && Auditioner.Current == s && Auditioner.Length > 0) { float px = r.X + (float)(Auditioner.Seconds / Auditioner.Length) * r.Width; using (var pen = new Pen(Color.White, 1.5f)) g.DrawLine(pen, px, r.Y, px, r.Bottom); Invalidate(); }
    }
    // ---- the browser panel ----
    void PaintSoundsPanel(Graphics g)
    {
        if (!soundsLoaded) { soundsLoaded = true; LibLoader.Start(); }
        if (!LibLoader.Ready) { PaintLibraryLoading(g); return; }   // (it loads in the background: the studio keeps working)
        EnsureSoundBox(); if (sndDirty) RefreshSounds(); Auditioner.Tick();
        float x = ClientSize.Width - PanelW, y = ToolH + TransH, w = PanelW, h = ClientSize.Height - y; var pr = Rc(x, y, w, h);
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, pr); using (var pen = new Pen(Color.FromArgb(150, NeonViolet))) g.DrawLine(pen, x, y, x, y + h); Reg(pr, () => { });
        SoundTabs(g, x + 14, y + 7, false);
        var cl = Rc(x + w - 38, y + 8, 28, 26); Btn(g, cl, "x", 0, () => CloseSounds(), 13);
        { var ib = Rc(x + w - 150, y + 8, 104, 26); var ir = Btn(g, ib, "IMPORT AUDIO", 2, () => ImportAudioAsk(), 10); ir.OnRight = () => ImportMenu(); Hint2(ib, "Add sounds from your computer to your library (kept in My samples). Right-click: a whole folder or a sound pack"); }
        Btn(g, Rc(x + w - 268, y + 8, 112, 26), sndFull ? "Exit full" : "Fullscreen", sndFull ? 4 : 0, () => { sndFull = !sndFull; }, 11);
        int installedPacks = 0; lock (SoundLibrary.Lock) foreach (var p in SoundLibrary.Packs) if (p.Items.Count > 0) installedPacks++;
        if (dupeFind.Count > 0) PaintDupeBar(g, Rc(x + 14, y + 35, w - 28, 18));
        else Txt(g, "Search by name, pack, style, key or tempo (for example: reese 174, F min, kick):", 10, DimC, Rc(x + 14, y + 38, w - 28, 14), StringAlignment.Near, false);
        var sb = Rc(x + 14, y + 54, w - 28, 28); if (soundBox.Visible != !(packsOpen || menuOpen || browserOpen || busy)) soundBox.Visible = !(packsOpen || menuOpen || browserOpen || busy);
        var nb = new Rectangle((int)sb.X, (int)sb.Y, (int)sb.Width, (int)sb.Height); if (soundBox.Bounds != nb) soundBox.Bounds = nb;
        // filters
        float fy = y + 90, fw = (w - 28 - 3 * 4) / 4f; var F = sndFilter;
        Func<string, string, string> lab = (n, v) => v.Length > 0 ? n + ": " + v : n;
        string bpmLabel = F.BpmMax > 0 ? F.BpmMin.ToString("0") + "-" + F.BpmMax.ToString("0") : "";
        Chip(g, Rc(x + 14, fy, fw, 26), lab("Genre", F.Genre), F.Genre.Length > 0, GenreMenu); Chip(g, Rc(x + 14 + (fw + 4), fy, fw, 26), lab("Style", F.Sub), F.Sub.Length > 0, StyleMenu); Chip(g, Rc(x + 14 + 2 * (fw + 4), fy, fw, 26), lab("Instrument", F.Instrument), F.Instrument.Length > 0, InstrumentMenu); Chip(g, Rc(x + 14 + 3 * (fw + 4), fy, fw, 26), lab("Type", F.Type.Length > 0 ? F.Type : (F.Group.Length > 0 ? F.Group : "")), F.Type.Length > 0 || F.Group.Length > 0 || F.AnyType != null, TypeMenu);
        float fy2 = fy + 30; bool tgt = InstantLib.TargetKey.Length > 0 || InstantLib.TargetBpm > 0;
        int moreOn = MoreCount(); var pk0 = F.Pack.Length > 0 ? SoundCatalog.Find(F.Pack) : null; string pkName = F.Pack.Length == 0 ? "" : (pk0 != null ? pk0.Name : (SoundLibrary.Pack(F.Pack) != null ? SoundLibrary.Pack(F.Pack).Name : F.Pack));
        Chip(g, Rc(x + 14, fy2, fw, 26), lab("Key", F.Key), F.Key.Length > 0, KeyMenu); Chip(g, Rc(x + 14 + (fw + 4), fy2, fw, 26), lab("BPM", bpmLabel), F.BpmMax > 0, BpmMenu); Chip(g, Rc(x + 14 + 2 * (fw + 4), fy2, fw, 26), lab("Pack", pkName), F.Pack.Length > 0, PackMenu); Chip(g, Rc(x + 14 + 3 * (fw + 4), fy2, fw, 26), moreOn > 0 ? "More (" + moreOn + ")" : "More", moreOn > 0 || tgt, MoreMenu); if (false) Chip(g, Rc(0, 0, 1, 1), "Toolkit", false, ToolkitMenu);
        float ry = y + 150, rx = x + 14; float[] rw = { 92, 66, 100, 76, 84, 74, 62 }; string[] rn = { "Favourites", "Recent", "Folders", "One-shots", "Loops", "MIDI", "Clear" };
        for (int i = 0; i < rn.Length; i++)
        {
            var r = Rc(rx, ry, rw[i], 24); int k = i; bool on = (i == 0 && F.OnlyFav) || (i == 1 && F.OnlyRecent) || (i == 2 && F.Collection.Length > 0) || (i == 3 && F.Kind == "one-shot") || (i == 4 && F.Kind == "loop") || (i == 5 && F.Kind == "midi");
            Chip(g, r, i == 2 && F.Collection.Length > 0 ? F.Collection : rn[i], on, () => { SoundChipClick(k); }, i == 6 ? 0 : -1); rx += rw[i] + 4;
        }
        { int real = SoundLibrary.InstalledCount(); string what = F.Avail == "" ? "installed" : (F.Avail == "instant" ? "made on play" : "installed + made on play");
          var av = Rc(x + w - 14 - 150, y + 176, 150, 22); Chip(g, av, F.Avail == "" ? "Show: installed" : (F.Avail == "instant" ? "Show: made on play" : "Show: all"), F.Avail != "", AvailMenu); Hint2(av, "Installed: real sound files on this PC (from the packs you installed and your own samples). Made on play: sounds that are made the first time you play them.");
          Txt(g, sndResults.Count.ToString("#,0") + (sndResults.Count == 1 ? " sound" : " sounds") + " (" + what + ")  ·  " + real.ToString("#,0") + " real files in " + installedPacks + " pack" + (installedPacks == 1 ? "" : "s"), 11, DimC, Rc(x + 14, y + 178, w - 28 - 160, 18), StringAlignment.Near, false); }
        // list
        float py = 200, ph = 214; var list = Rc(x + 8, y + py, w - 16, h - py - ph - 6); var gs = g.Save(); g.SetClip(list, CombineMode.Intersect); float rowH = 52; sndListH = list.Height;
        float maxScroll = Math.Max(0, sndResults.Count * rowH - list.Height); sndScroll = Math.Max(0, Math.Min(maxScroll, sndScroll)); Reg(list, () => { }).OnWheel = d => { sndScroll = Math.Max(0, Math.Min(maxScroll, sndScroll - d * 52)); };
        if (sndResults.Count == 0 && F.Avail == "" && SoundLibrary.InstalledCount() == 0)
        {
            Txt(g, "NO SOUNDS INSTALLED YET", 18, Ink, Rc(list.X, list.Y + 24, list.Width, 28), StringAlignment.Center, true);
            Txt(g, "Install a pack on SOUND PACKS: its sounds are made as real files on this PC and checked one by one.", 12, DimC, Rc(list.X + 20, list.Y + 56, list.Width - 40, 20), StringAlignment.Center, false);
            Txt(g, "Or show the sounds that are made the first time you play them.", 12, DimC, Rc(list.X + 20, list.Y + 76, list.Width - 40, 20), StringAlignment.Center, false);
            Btn(g, Rc(list.X + list.Width / 2 - 200, list.Y + 108, 190, 34), "Go to SOUND PACKS", 2, () => { packsOpen = true; packScroll = 0; }, 12);
            Btn(g, Rc(list.X + list.Width / 2 + 10, list.Y + 108, 190, 34), "Show made on play", 0, () => { sndFilter.Avail = "instant"; sndDirty = true; }, 12);
        }
        else if (sndResults.Count == 0)
        {
            Txt(g, "NOTHING MATCHES", 16, Ink, Rc(list.X, list.Y + 24, list.Width, 26), StringAlignment.Center, true);
            Txt(g, EmptyWhy(), 12, DimC, Rc(list.X + 20, list.Y + 54, list.Width - 40, 40), StringAlignment.Center, false);
            Btn(g, Rc(list.X + list.Width / 2 - 200, list.Y + 100, 190, 32), "Clear the filters", 1, () => SoundChipClick(6), 12);
            if (F.Avail != "all") Btn(g, Rc(list.X + list.Width / 2 + 10, list.Y + 100, 190, 32), F.Avail == "" ? "Include made on play" : "Include installed", 0, () => { sndFilter.Avail = "all"; sndDirty = true; }, 12);
        }
        int first = (int)(sndScroll / rowH), last = Math.Min(sndResults.Count - 1, first + (int)(list.Height / rowH) + 1);
        for (int i = Math.Max(0, first); i <= last; i++) PaintSoundRow(g, sndResults[i], Rc(list.X, list.Y + i * rowH - sndScroll, list.Width - 52, rowH - 2));
        g.Restore(gs); VScroll(g, Rc(list.Right - 48, list.Y, 42, list.Height), sndResults.Count * rowH, list.Height, () => sndScroll, v => sndScroll = v);
        PaintSoundPreview(g, Rc(x + 8, y + h - ph, w - 16, ph - 6));
    }

    void Chip(Graphics g, RectangleF r, string text, bool on, Action a, int style = -1) { Btn(g, r, text, on ? 4 : (style < 0 ? 0 : style), a, 11); }
    void SoundChipClick(int k)
    {
        var F = sndFilter; sndDirty = true; sndScroll = 0;
        if (k == 0) F.OnlyFav = !F.OnlyFav; else if (k == 1) F.OnlyRecent = !F.OnlyRecent; else if (k == 2) CollectionsMenu(); else if (k == 3) F.Kind = F.Kind == "one-shot" ? "" : "one-shot"; else if (k == 4) F.Kind = F.Kind == "loop" ? "" : "loop"; else if (k == 5) F.Kind = F.Kind == "midi" ? "" : "midi";
        else { var t = F.Text; var n = new SoundLibrary.Filter(); F.Genre = n.Genre; F.Sub = ""; F.Instrument = ""; F.Type = ""; F.Group = ""; F.Key = ""; F.Kind = ""; F.Pack = ""; F.Mood = ""; F.BpmMin = 0; F.BpmMax = 0; F.HalfDouble = false; F.OnlyFav = false; F.OnlyRecent = false; F.Collection = ""; F.AnyType = null; F.DurMin = 0; F.DurMax = 0; F.Bars = 0; F.Channels = 0; F.Source = ""; F.Family = ""; F.AddedDays = 0; F.OnlyKeyed = false; if (soundBox != null) soundBox.Text = ""; }
    }

    void PaintSoundRow(Graphics g, SoundItem s, RectangleF r)
    {
        float dx = Math.Max(0, Math.Min(500, r.Width - 572)); bool sel = s == sndSel, hot = Hot(r), sex = s.Instant || s.Exists; FillRR(g, sel ? TM.C(150, 80, 44, 150) : (hot ? TM.C(130, 60, 34, 110) : TM.C(90, 34, 20, 64)), r, 8); if (sel) StrokeRR(g, NeonCyan, r, 8, 1.4f);
        var tile = Rc(r.X + 6, r.Y + 6, 38, 38); DrawPackTile(g, tile, SoundLibrary.Pack(s.PackId), s.PackName);
        Txt(g, s.Name.Replace(s.PackName + " - ", ""), 12, sex ? Ink : Color.FromArgb(255, 140, 140), Rc(r.X + 50, r.Y + 5, 250 + dx, 20), StringAlignment.Near, sel);
        Txt(g, s.PackName + "  ·  " + s.Type + (sex ? "" : "  (file missing)"), 10, DimC, Rc(r.X + 50, r.Y + 26, 250 + dx, 18), StringAlignment.Near, false);
        // little wave
        var wr = Rc(r.X + dx + 304, r.Y + 10, 90, 30); if (s.Kind == "midi") DrawMidiPic(g, wr, s, Color.FromArgb(sel ? 235 : 170, Neon(GroupColour(s.Group))), false); else if (s.Kind == "preset") Txt(g, s.Format == "Purphex Wobble preset" ? "WOBBLE PRESET" : "BASS LAB PRESET", 8, DimC, wr, StringAlignment.Center, true); else if (s.Peaks != null && s.Peaks.Length > 0) { using (var pen = new Pen(Color.FromArgb(sel ? 230 : 150, Neon(GroupColour(s.Group))), 1f)) for (int i = 0; i < wr.Width; i += 2) { float v = s.Peaks[Math.Min(s.Peaks.Length - 1, (int)(i / wr.Width * s.Peaks.Length))] / 255f * wr.Height / 2; g.DrawLine(pen, wr.X + i, wr.Y + wr.Height / 2 - v, wr.X + i, wr.Y + wr.Height / 2 + v); } } else if (s.Instant) Txt(g, "PLAY TO MAKE", 9, DimC, wr, StringAlignment.Center, false);   /* an instant loop is made the first time it plays */
        Txt(g, s.Bpm > 0 ? s.Bpm.ToString("0") : "-", 11, s.Bpm > 0 ? Ink : DimC, Rc(r.X + dx + 400, r.Y, 40, r.Height), StringAlignment.Center, false); Txt(g, s.Key.Length > 0 ? s.Key : "-", 11, s.Key.Length > 0 ? Ink : DimC, Rc(r.X + dx + 440, r.Y, 62, r.Height), StringAlignment.Center, false); Txt(g, s.Dur > 0 ? s.Dur.ToString("0.0") + "s" : "", 10, DimC, Rc(r.X + dx + 502, r.Y, 46, r.Height), StringAlignment.Center, false);
        var star = Rc(r.Right - 30, r.Y + 12, 22, 24); bool fav = SoundLibrary.Favourites.Contains(s.Id); Txt(g, fav ? "★" : "☆", 16, fav ? Color.FromArgb(255, 210, 80) : DimC, star, StringAlignment.Center, false);
        var rg = Reg(r, () => { SelectSound(s, true); sndDrag = s; sndDragFrom = MousePos; sndDragging = false; }); rg.OnDrag = pt => { if (sndDrag != null && !sndDragging && (Math.Abs(pt.X - sndDragFrom.X) + Math.Abs(pt.Y - sndDragFrom.Y)) > 8) sndDragging = true; }; rg.OnDouble = () => SelectSound(s, true);   // (drag it onto the timeline to use it: a double-click only plays it again) rg.OnRight = () => SoundMenu(s);
        var sr = Reg(star, () => { SoundLibrary.ToggleFav(s); sndDirty = sndFilter.OnlyFav; sndDrag = null; }); if (Hot(star)) { bubble = fav ? "Remove from favourites" : "Add to favourites"; bubblePt = MousePos; }
    }

    static Bitmap logoSmall;
    void DrawPackTile(Graphics g, RectangleF r, SoundPack p, string name)
    {
        int hue = p != null ? p.Hue : 280; var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); ProjectLibrary.DrawArt(g, r, name, hue); g.Restore(gs); using (var pen = new Pen(Color.FromArgb(160, NeonViolet), 1.2f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        if (logoSmall == null) { var lg = AppLogo.Logo; if (lg != null) { try { logoSmall = new Bitmap(28, 28, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); using (var lgg = Graphics.FromImage(logoSmall)) { lgg.InterpolationMode = InterpolationMode.HighQualityBicubic; lgg.DrawImage(lg, new Rectangle(0, 0, 28, 28)); } } catch { logoSmall = null; } } } if (logoSmall != null) { var q = Rc(r.Right - 15, r.Bottom - 15, 14, 14); g.DrawImage(logoSmall, q); }
    }

    // a sound previewed in SOUNDS or the packs: the song pauses (at the needle), so you hear only the sound you are trying
    internal void PreviewSound(SoundItem s) { if (eng.Playing) PauseHere(); Auditioner.Play(s, m => Toast(m)); }
    void SelectSound(SoundItem s, bool play) { sndSel = s; if (play && SoundLibrary.AutoPlay && (s.IsAudio || s.Kind == "midi")) PreviewSound(s); }   // a MIDI file is heard through an instrument

    void PaintSoundPreview(Graphics g, RectangleF r)
    {
        FillRR(g, TM.C(150, 24, 13, 46), r, 10); StrokeRR(g, Color.FromArgb(140, NeonViolet), r, 10, 1); var s = sndSel;
        if (s == null) { Txt(g, "Click a sound to hear it. Drag it onto the timeline to use it.", 12, DimC, Rc(r.X + 14, r.Y + 20, r.Width - 28, 40), StringAlignment.Near, false); return; }
        Txt(g, s.Name.Replace(s.PackName + " - ", ""), 13, Ink, Rc(r.X + 12, r.Y + 6, r.Width - 24, 20), StringAlignment.Near, true);
        var wf = Rc(r.X + 12, r.Y + 30, r.Width - 24, 56); FillRR(g, TM.C(200, 8, 4, 20), wf, 6);
        bool cur = Auditioner.Current == s && Auditioner.LeftSamples != null; var col = Neon(GroupColour(s.Group));
        if (Auditioner.Loading && Auditioner.Current == s) Txt(g, "LOADING...", 12, DimC, wf, StringAlignment.Center, false);
        else if (s.Kind == "midi") DrawMidiPic(g, Rc(wf.X + 6, wf.Y + 4, wf.Width - 12, wf.Height - 8), s, Color.FromArgb(230, col), true);
        else if (s.Kind == "preset") Txt(g, s.Format == "Purphex Wobble preset" ? "A PURPHEX WOBBLE PRESET: PLAY IN A WOBBLE TRACK OPENS IT ON A NEW TRACK" : "A BASS LAB PRESET: ADD TO BASS LAB PUTS IT IN ITS PRESET BROWSER", 10, DimC, wf, StringAlignment.Center, true);
        else { if (sndZoomFor != s.Id) { sndZoomFor = s.Id; sndViewA = 0; sndViewB = 1; } PaintSoundWave(g, wf, s, col); }
        if (cur && Auditioner.Length > 0) { float px = SndX(Auditioner.Seconds / Auditioner.Length, wf); if (px >= wf.X && px <= wf.Right) using (var pen = new Pen(Color.White, 1.5f)) g.DrawLine(pen, px, wf.Y + 2, px, wf.Bottom - 2); }
        var wr = Reg(wf, () => { if (cur) Auditioner.Seek(SndF(MousePos.X, wf)); }); wr.OnDrag = pt => { if (cur) Auditioner.Seek(SndF(pt.X, wf)); };
        if (s.IsAudio && s.Kind != "midi" && s.Kind != "preset") wr.OnWheel = d => SoundZoom(d > 0 ? 0.6 : 1 / 0.6, SndF(MousePos.X, wf));   // the wheel on the wave zooms in and out where the mouse is
        if (cur) SoundTrimPlayback(s);
        if (s.IsAudio && s.Kind != "midi" && s.Kind != "preset") PaintSoundTrim(g, wf, s);
        float cy = r.Y + 92; bool playing = cur && Auditioner.IsPlaying;
        Btn(g, Rc(r.X + 12, cy, 66, 28), playing ? "Pause" : "Play", 1, () => { if (cur) { if (!playing && eng.Playing) PauseHere(); Auditioner.Toggle(); } else if (s.IsAudio || s.Kind == "midi") PreviewSound(s); }, 12); Btn(g, Rc(r.X + 82, cy, 50, 28), "Stop", 0, () => Auditioner.Stop(), 11);
        Chip(g, Rc(r.X + 136, cy, 56, 28), "Loop", SoundLibrary.LoopPreview, () => { SoundLibrary.LoopPreview = !SoundLibrary.LoopPreview; }); Chip(g, Rc(r.X + 196, cy, 78, 28), "Autoplay", SoundLibrary.AutoPlay, () => { SoundLibrary.AutoPlay = !SoundLibrary.AutoPlay; SoundLibrary.SaveUser(); });
        Txt(g, "VOL", 10, DimC, Rc(r.X + 282, cy + 6, 24, 16), StringAlignment.Near, false); var vs = Slider(g, Rc(r.X + 306, cy + 5, r.Width - 306 - 16 + r.X - r.X, 18), SoundLibrary.AuditionVolume, f => { SoundLibrary.AuditionVolume = f; }, NeonCyan); vs.OnDouble = () => SoundLibrary.AuditionVolume = 0.8f;
        float by = cy + 34; bool preset = s.Kind == "preset", midi = s.Kind == "midi";
        Btn(g, Rc(r.X + 12, by, 168, 30), preset ? (s.Path.EndsWith(".purphexwobble", StringComparison.OrdinalIgnoreCase) ? "Play in a Wobble track" : "Add to Bass Lab") : (midi ? "Add to selected track" : "Add to timeline"), 2, () => { string m = midi ? DropMidi(s, Snap(DispPos), -1) : AddSample(s, Snap(DispPos), eng.Tracks.Count); if (m != null) Toast(m); }, 12);
        bool fav = SoundLibrary.Favourites.Contains(s.Id); Chip(g, Rc(r.X + 184, by, 96, 30), fav ? "★ Favourite" : "☆ Favourite", fav, () => { SoundLibrary.ToggleFav(s); sndDirty = sndFilter.OnlyFav; }); Btn(g, Rc(r.X + 284, by, 104, 30), "More...", 0, () => SoundMenu(s), 11);
        Btn(g, Rc(r.X + 392, by, 70, 30), "Show file", 0, () => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + s.Path + "\""); } catch { } }, 11); Btn(g, Rc(r.X + 466, by, 80, 30), "Licence", 0, () => { var p = SoundLibrary.Pack(s.PackId); MessageBox.Show(this, (p != null ? p.Name + "\n\n" + p.License : (s.Instant ? s.PackName + "\n\n" + SoundLibrary.LicenceText : "No licence information.")), "Licence"); }, 11);
        string info = (s.Format.Length > 0 ? s.Format : "") + (s.Rate > 0 && s.IsAudio ? "  ·  " + (s.Rate / 1000.0).ToString("0.#") + " kHz" : "") + (s.Dur > 0 ? "  ·  " + s.Dur.ToString("0.00") + " s" : "") + (s.Bpm > 0 ? "  ·  " + s.Bpm.ToString("0") + " BPM" : "") + (s.Key.Length > 0 ? "  ·  " + s.Key : "") + "  ·  " + s.PackName;
        Txt(g, info, 10, DimC, Rc(r.X + 12, by + 34, r.Width - 24, 16), StringAlignment.Near, false);
        if (s.IsAudio && s.Kind != "midi" && s.Kind != "preset")
        {   // zoom on the wave (for exact cut offs), keep the trimmed part as its own sound, delete a sound of yours
            float ey = by + 54; bool zoomed = sndViewA > 0.0001 || sndViewB < 0.9999;
            var zo = Rc(r.X + 12, ey, 34, 26); Btn(g, zo, "−", 0, () => SoundZoom(1 / 0.6, (sndViewA + sndViewB) / 2), 13); Hint2(zo, "Zoom out of the wave (or use the mouse wheel on it)");
            var zi = Rc(r.X + 50, ey, 34, 26); Btn(g, zi, "+", 0, () => SoundZoom(0.6, (sndViewA + sndViewB) / 2), 13); Hint2(zi, "Zoom into the wave to set the cut offs exactly (or use the mouse wheel on it)");
            var zf = Rc(r.X + 88, ey, 46, 26); Btn(g, zf, "FIT", zoomed ? 4 : 0, () => { sndViewA = 0; sndViewB = 1; }, 9); Hint2(zf, "See the whole sound again");
            var tr0 = SoundTrim(s); var st = Rc(r.X + 142, ey, 140, 26); Btn(g, st, "SAVE TRIMMED", tr0 != null ? 2 : 0, () => SaveTrimmedSound(s), 9); Hint2(st, tr0 != null ? "Keep the trimmed part as a sound of its own in MY SOUNDS, right next to this one (this sound stays as it is). Play hears just the trimmed part first" : "Drag the gold handles on the wave first, then keep that part as a sound of its own");
            bool mine = s.PackId == SoundLibrary.UserPackId; var dl = Rc(r.X + 290, ey, 90, 26); Btn(g, dl, "DELETE", mine ? 3 : 0, () => DeleteSound(s), 9); Hint2(dl, mine ? "Take this sound out of MY SOUNDS (it asks first)" : "A sound of a pack: remove the whole pack in SOUND PACKS instead");
            var fo = Rc(r.X + 388, ey, 134, 26); Btn(g, fo, "ADD TO FOLDER", 0, () => SoundMenu(s), 9); Hint2(fo, "Put this sound in a folder of yours (Drill piano, Drill bass...) to keep MY SOUNDS neat. FOLDERS at the top shows one");
        }
    }
    // ---- the wave of the picked sound, with zoom: the whole sound from its stored outline, a zoomed part in full detail from the sound itself ----
    double sndViewA, sndViewB = 1; string sndZoomFor = ""; string sndWaveFor = ""; float[] sndWave; int sndWaveRate = 44100;
    float SndX(double f, RectangleF wf) { return wf.X + (float)((f - sndViewA) / Math.Max(1e-6, sndViewB - sndViewA) * wf.Width); }
    double SndF(float x, RectangleF wf) { return Math.Max(0, Math.Min(1, sndViewA + (x - wf.X) / wf.Width * (sndViewB - sndViewA))); }
    void SoundZoom(double k, double at)
    {
        double w = Math.Max(0.002, Math.Min(1, (sndViewB - sndViewA) * k)); double a = at - (at - sndViewA) * (w / Math.Max(1e-6, sndViewB - sndViewA)); a = Math.Max(0, Math.Min(1 - w, a)); sndViewA = a; sndViewB = a + w; Invalidate();
    }
    void PaintSoundWave(Graphics g, RectangleF wf, SoundItem s, Color col)
    {
        bool zoomed = sndViewA > 0.0001 || sndViewB < 0.9999; float mid = wf.Y + wf.Height / 2, amp = wf.Height / 2 - 4;
        if (zoomed && sndWaveFor != s.Id)
        {   // the sound itself (as one channel), read once when you first zoom into it
            sndWaveFor = s.Id; sndWave = null; float[] l, r2; int sr; string fmt;
            try { if (s.Instant) InstantLib.Ensure(s); if (SoundIO.Read(s.Path, out l, out r2, out sr, out fmt) && l.Length > 0) { if (r2 != null && r2 != l) { var mono = new float[l.Length]; for (int i = 0; i < l.Length; i++) mono[i] = (l[i] + r2[i]) * 0.5f; l = mono; } sndWave = l; sndWaveRate = sr; } } catch { }
        }
        using (var pen = new Pen(Color.FromArgb(220, col), 1.4f))
        {
            if (zoomed && sndWave != null && sndWave.Length > 0)
            {
                int n = sndWave.Length; for (int i = 0; i < (int)wf.Width; i++)
                {
                    int a = (int)(SndF(wf.X + i, wf) * n), b = Math.Max(a + 1, (int)(SndF(wf.X + i + 1, wf) * n)); b = Math.Min(n, b); float mn = 0, mx = 0; int step = Math.Max(1, (b - a) / 64);
                    for (int k = a; k < b; k += step) { float v = sndWave[k]; if (v < mn) mn = v; if (v > mx) mx = v; }
                    g.DrawLine(pen, wf.X + i, mid - Math.Min(1, mx) * amp, wf.X + i, mid - Math.Max(-1, mn) * amp + 0.5f);
                }
                double secs = n / (double)sndWaveRate; Txt(g, (sndViewA * secs).ToString("0.000") + " s - " + (sndViewB * secs).ToString("0.000") + " s", 8, DimC, Rc(wf.Right - 150, wf.Bottom - 13, 146, 12), StringAlignment.Far, false);
            }
            else if (s.Peaks != null && s.Peaks.Length > 0)
                for (int i = 0; i < (int)wf.Width; i += 2) { float v = s.Peaks[Math.Min(s.Peaks.Length - 1, (int)(SndF(wf.X + i, wf) * s.Peaks.Length))] / 255f * amp; g.DrawLine(pen, wf.X + i, mid - v, wf.X + i, mid + v); }
        }
    }
    // a trimmed sound plays only its trimmed part (from the start handle; at the end handle it stops, or goes round again with Loop)
    void SoundTrimPlayback(SoundItem s)
    {
        var t = SoundTrim(s); if (t == null || !Auditioner.IsPlaying || Auditioner.Length <= 0) return; double f = Auditioner.Seconds / Auditioner.Length;
        if (f < t[0] - 0.002) Auditioner.Seek(t[0]); else if (f >= t[1]) { if (SoundLibrary.LoopPreview) Auditioner.Seek(t[0]); else { Auditioner.Pause(); Auditioner.Seek(t[0]); } }
    }
    // SAVE TRIMMED: the part between the handles becomes a sound of its own in MY SOUNDS, next to the original (which stays as it is)
    internal SoundItem SaveTrimmedSound(SoundItem s)
    {
        if (SoundTrim(s) == null) { Toast("Drag the gold handles on the wave first: the part between them is what is kept."); return null; }
        string f = TrimmedFile(s, s.Path); if (f == s.Path) { Toast("Couldn't make the trimmed sound."); return null; }
        var it = SoundLibrary.AddVersion(s, f, false); if (it == null) { Toast("Couldn't keep the trimmed sound."); return null; }
        ShowMySounds(); sndFilter.Text = it.Name; if (soundBox != null) soundBox.Text = it.Name; sndDirty = true; sndSel = it; Toast(it.Name + " is in MY SOUNDS: the trimmed part, as a sound of its own."); return it;
    }
    // DELETE: a sound of yours goes from MY SOUNDS (it asks first, in case it was a mistake)
    internal Func<string, bool> deleteAsk;   // a test answers for itself
    internal bool DeleteSound(SoundItem s)
    {
        if (s == null) return false; if (s.PackId != SoundLibrary.UserPackId) { Toast("That sound belongs to a pack: remove the whole pack in SOUND PACKS."); return false; }
        string q = "Delete \"" + s.Name + "\" from MY SOUNDS?\n\nIts copy in your library goes too. A song or pad that uses it would lose it. (Your own original file, if you imported it from somewhere, is not touched.)";
        if (!(deleteAsk != null ? deleteAsk(q) : AskYes(q, "Delete sound"))) return false;
        if (Auditioner.Current == s) Auditioner.Stop(); bool ok = SoundLibrary.DeleteMine(s); if (sndSel == s) sndSel = null; sndDirty = true; Toast(ok ? s.Name + " was deleted from MY SOUNDS." : "Couldn't delete " + s.Name + "."); return ok;
    }

    // ---- menus ----
    void ShowFilterMenu(ContextMenuStrip m) { m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    // how many sounds each genre and style has right now (the installed packs and the instant loops)
    Dictionary<string, int> CountBy(Func<SoundItem, string> key) { return CountBy(key, null); }
    // how many sounds each option would give: the search with every other filter kept and this one cleared
    Dictionary<string, int> CountBy(Func<SoundItem, string> key, Action<SoundLibrary.Filter> clear)
    {
        var f = sndFilter.Copy(); if (clear != null) clear(f); var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in SoundLibrary.Search(f)) { string k = key(s); if (string.IsNullOrEmpty(k)) continue; int n; d.TryGetValue(k, out n); d[k] = n + 1; } return d;
    }
    string EmptyWhy()
    {
        var F = sndFilter; var parts = new List<string>(); if (F.Genre.Length > 0) parts.Add(F.Genre); if (F.Sub.Length > 0) parts.Add(F.Sub); if (F.Type.Length > 0) parts.Add(F.Type); if (F.Kind.Length > 0) parts.Add(F.Kind + "s"); if (F.Key.Length > 0) parts.Add(F.Key); if (F.BpmMax > 0) parts.Add(F.BpmMin.ToString("0") + "-" + F.BpmMax.ToString("0") + " BPM"); if (F.Text.Length > 0) parts.Add("\"" + F.Text + "\"");
        return (parts.Count > 0 ? "No " + (F.Avail == "" ? "installed " : "") + "sound is " + string.Join(", ", parts) + "." : "No sound matches.") + (F.Avail == "" ? " Install more packs, or include the sounds made on play." : " Clear a filter to see more.");
    }
    static string Counted(string name, Dictionary<string, int> counts, string key) { int n; counts.TryGetValue(key, out n); return name + "   (" + n.ToString("#,0") + (n == 1 ? " sound)" : " sounds)"); }
    // every genre in the library, in its section, with how many sounds it has
    void GenreMenu() { ShowFilterMenu(GenreMenuStrip()); }
    internal ContextMenuStrip GenreMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any genre", () => { sndFilter.Genre = ""; sndFilter.Sub = ""; }); var counts = CountBy(x => x.Genre, f => { f.Genre = ""; f.Sub = ""; }); var bySec = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (counts.Count == 0) m.Items.Add(new ToolStripMenuItem("No sounds to choose from: install a pack, or show the sounds made on play") { Enabled = false });
        foreach (var g in SoundCatalog.Genres().Keys) { int have; if (!counts.TryGetValue(g, out have) || have == 0) continue; string sec = SoundCatalog.SectionOf(g); List<string> l; if (!bySec.TryGetValue(sec, out l)) bySec[sec] = l = new List<string>(); l.Add(g); }
        foreach (var kv in bySec) { var top = new ToolStripMenuItem(kv.Key.ToUpperInvariant()) { Enabled = false }; m.Items.Add(top); foreach (var g in kv.Value) { string gg = g; AddItem(m, "    " + Counted(g, counts, g), sndFilter.Genre == g, () => { sndFilter.Genre = gg; sndFilter.Sub = ""; }); } }
        int mine; if (counts.TryGetValue("Any", out mine) && mine > 0) { m.Items.Add(new ToolStripSeparator()); AddItem(m, Counted("My samples", counts, "Any"), sndFilter.Genre == "Any", () => { sndFilter.Genre = "Any"; sndFilter.Sub = ""; }); }
        return m;
    }
    // the styles of the chosen genre (or every genre's styles, one menu per genre)
    void StyleMenu() { ShowFilterMenu(StyleMenuStrip()); }
    internal ContextMenuStrip StyleMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any style", () => sndFilter.Sub = ""); var counts = CountBy(x => x.Genre + "|" + x.Sub, f => { f.Sub = ""; if (sndFilter.Genre.Length == 0) f.Genre = ""; }); var all = SoundCatalog.Genres();
        if (counts.Count == 0) m.Items.Add(new ToolStripMenuItem("No styles here yet") { Enabled = false });
        if (sndFilter.Genre.Length > 0 && all.ContainsKey(sndFilter.Genre)) { foreach (var v in all[sndFilter.Genre]) { int have; if (!counts.TryGetValue(sndFilter.Genre + "|" + v, out have) || have == 0) continue; string vv = v; AddItem(m, Counted(v, counts, sndFilter.Genre + "|" + v), sndFilter.Sub == v, () => sndFilter.Sub = vv); } }
        else foreach (var kv in all)
        {
            var top = new ToolStripMenuItem(kv.Key); string g = kv.Key; bool anyHere = false; foreach (var v in kv.Value) { int hv; if (counts.TryGetValue(g + "|" + v, out hv) && hv > 0) anyHere = true; } if (!anyHere) continue;
            foreach (var v in kv.Value) { int hv2; if (!counts.TryGetValue(g + "|" + v, out hv2) || hv2 == 0) continue; string vv = v; var it = new ToolStripMenuItem(Counted(v, counts, g + "|" + v)) { Checked = sndFilter.Genre == g && sndFilter.Sub == v }; it.Click += (a, b) => { sndFilter.Genre = g; sndFilter.Sub = vv; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(it); }
            m.Items.Add(top);
        }
        return m;
    }
    void TypeMenu() { ShowFilterMenu(TypeMenuStrip()); }
    // asset types under their group (Loops, Drums, Bass, ...), each with how many sounds it would give
    internal ContextMenuStrip TypeMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any type", () => { sndFilter.Type = ""; sndFilter.Group = ""; sndFilter.AnyType = null; });
        var counts = CountBy(x => x.Group + "|" + x.Type, f => { f.Type = ""; f.Group = ""; f.AnyType = null; }); var groups = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); var gCount = new Dictionary<string, int>();
        foreach (var kv in counts) { int bar = kv.Key.IndexOf('|'); string grp = kv.Key.Substring(0, bar), ty = kv.Key.Substring(bar + 1); if (grp.Length == 0) grp = "Other"; List<string> l; if (!groups.TryGetValue(grp, out l)) groups[grp] = l = new List<string>(); l.Add(ty); int n; gCount.TryGetValue(grp, out n); gCount[grp] = n + kv.Value; }
        if (groups.Count == 0) m.Items.Add(new ToolStripMenuItem("No sounds to choose from") { Enabled = false });
        foreach (var kv in groups)
        {
            string gg = kv.Key; var top = new ToolStripMenuItem(gg + "   (" + gCount[gg].ToString("#,0") + ")"); var all = new ToolStripMenuItem("All " + gg.ToLowerInvariant()); all.Click += (a, b) => { sndFilter.Group = gg == "Other" ? "" : gg; sndFilter.Type = ""; sndFilter.AnyType = null; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(all); top.DropDownItems.Add(new ToolStripSeparator());
            kv.Value.Sort(StringComparer.OrdinalIgnoreCase); foreach (var t in kv.Value) { string tt = t; var it = new ToolStripMenuItem(t + "   (" + counts[gg + "|" + t].ToString("#,0") + ")") { Checked = sndFilter.Type == t }; it.Click += (a, b) => { sndFilter.Type = tt; sndFilter.Group = ""; sndFilter.AnyType = null; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(it); }
            m.Items.Add(top);
        }
        return m;
    }
    void KeyMenu() { ShowFilterMenu(KeyMenuStrip()); }
    internal ContextMenuStrip KeyMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any key", () => sndFilter.Key = ""); var counts = CountBy(x => { int rt; bool mi; return x.Key.Length > 0 && SoundLibrary.ParseKey(x.Key, out rt, out mi) ? SoundLibrary.KeyName(rt, mi) : ""; }, f => f.Key = "");
        if (counts.Count == 0) m.Items.Add(new ToolStripMenuItem("None of these sounds has a key (drums, FX...)") { Enabled = false });
        for (int mi = 0; mi < 2; mi++) for (int i = 0; i < 12; i++) { string k = SoundLibrary.NoteNames[i] + (mi == 0 ? " min" : " maj"); int n; if (!counts.TryGetValue(k, out n) || n == 0) continue; string kk = k; AddItem(m, k + "   (" + n.ToString("#,0") + ")", sndFilter.Key == k, () => sndFilter.Key = kk); }
        m.Items.Add(new ToolStripSeparator()); var ok = new ToolStripMenuItem("Only sounds with a key (hide drums and FX)") { Checked = sndFilter.OnlyKeyed }; ok.Click += (a, b) => { sndFilter.OnlyKeyed = !sndFilter.OnlyKeyed; sndDirty = true; sndScroll = 0; }; m.Items.Add(ok);
        return m;
    }
    void BpmMenu()
    {
        var m = Win.NewMenu(); AddAny(m, "Any tempo", () => { sndFilter.BpmMin = 0; sndFilter.BpmMax = 0; });
        var withTempo = SoundLibrary.Search(BpmFree()); foreach (var o in BpmRanges()) { double a = o.Key, b = o.Value; int n = 0; foreach (var s in withTempo) if (s.Bpm >= a && s.Bpm <= b) n++; if (n == 0) continue; AddItem(m, a.ToString("0") + " - " + b.ToString("0") + " BPM   (" + n.ToString("#,0") + "  ·  " + BpmGenres(a, b) + ")", sndFilter.BpmMin == a && sndFilter.BpmMax == b, () => { sndFilter.BpmMin = a; sndFilter.BpmMax = b; }); }
        m.Items.Add("Exact tempo...").Click += (a, b) => { string v = Ask("Tempo in BPM (the search allows 3 BPM either way):", Fx.ProjectBpm.ToString("0")); double bpm; if (v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bpm)) { sndFilter.BpmMin = bpm - 3; sndFilter.BpmMax = bpm + 3; sndDirty = true; sndScroll = 0; } };
        m.Items.Add("Match my project's tempo").Click += (a, b) => { sndFilter.BpmMin = Fx.ProjectBpm - 3; sndFilter.BpmMax = Fx.ProjectBpm + 3; sndDirty = true; sndScroll = 0; };
        var hd = new ToolStripMenuItem("Also show half and double time") { Checked = sndFilter.HalfDouble }; hd.Click += (a, b) => { sndFilter.HalfDouble = !sndFilter.HalfDouble; sndDirty = true; }; m.Items.Add(hd); ShowFilterMenu(m);
    }
    SoundLibrary.Filter BpmFree() { var f = sndFilter.Copy(); f.BpmMin = 0; f.BpmMax = 0; return f; }
    // the Pack filter: every pack that has sounds here, by section, with counts
    void PackMenu() { ShowFilterMenu(PackMenuStrip()); }
    internal ContextMenuStrip PackMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any pack", () => sndFilter.Pack = ""); var counts = CountBy(x => x.PackId, f => f.Pack = ""); var bySec = new SortedDictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in counts) { var rec = SoundCatalog.Find(kv.Key); var ip = SoundLibrary.Pack(kv.Key); string name = rec != null ? rec.Name : (ip != null ? ip.Name : kv.Key), sec = rec != null ? rec.Section : (ip != null && ip.Section.Length > 0 ? ip.Section : "Yours"); List<KeyValuePair<string, string>> l; if (!bySec.TryGetValue(sec, out l)) bySec[sec] = l = new List<KeyValuePair<string, string>>(); l.Add(new KeyValuePair<string, string>(kv.Key, name)); }
        if (bySec.Count == 0) m.Items.Add(new ToolStripMenuItem("No packs here yet: install some on SOUND PACKS") { Enabled = false });
        foreach (var kv in bySec) { var top = new ToolStripMenuItem(kv.Key); kv.Value.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase)); foreach (var p in kv.Value) { string id = p.Key; var it = new ToolStripMenuItem(p.Value + "   (" + counts[id].ToString("#,0") + ")") { Checked = sndFilter.Pack == id }; it.Click += (a, b) => { sndFilter.Pack = id; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(it); } m.Items.Add(top); }
        return m;
    }
    // what is shown: installed sounds (real files), made on play, or both
    void AvailMenu()
    {
        var m = Win.NewMenu(); int inst = SoundLibrary.InstalledCount(), made = InstantLib.Items.Count;
        AddItem(m, "Installed: real files on this PC   (" + inst.ToString("#,0") + ")", sndFilter.Avail == "", () => sndFilter.Avail = "");
        AddItem(m, "Made on play: made the first time you play them   (" + made.ToString("#,0") + ")", sndFilter.Avail == "instant", () => sndFilter.Avail = "instant");
        AddItem(m, "All", sndFilter.Avail == "all", () => sndFilter.Avail = "all"); ShowFilterMenu(m);
    }
    int MoreCount() { var F = sndFilter; int n = 0; if (F.DurMax > 0) n++; if (F.Bars > 0) n++; if (F.Channels > 0) n++; if (F.Source.Length > 0) n++; if (F.Family.Length > 0) n++; if (F.AddedDays > 0) n++; if (F.Mood.Length > 0) n++; return n; }
    // the other details: length, bars, time signature, mono / stereo, sample rate, source and licence, recently added, family, mood, the key and tempo instant sounds are made in, and the toolkits
    void MoreMenu() { ShowFilterMenu(MoreMenuStrip()); }
    internal ContextMenuStrip MoreMenuStrip()
    {
        var m = Win.NewMenu(); var F = sndFilter;
        Func<string, Func<SoundItem, string>, Action<SoundLibrary.Filter>, Func<string, bool>, Action<string>, ToolStripMenuItem> sub = (title, key, clear, isOn, set) =>
        {
            var top = new ToolStripMenuItem(title); var any = new ToolStripMenuItem("Any"); any.Click += (a, b) => { set(""); sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(any); top.DropDownItems.Add(new ToolStripSeparator());
            var counts = CountBy(key, clear); var keys = new List<string>(counts.Keys); keys.Sort(StringComparer.OrdinalIgnoreCase);
            if (keys.Count == 0) top.DropDownItems.Add(new ToolStripMenuItem("(nothing here)") { Enabled = false });
            foreach (var k in keys) { string kk = k; var it = new ToolStripMenuItem(k + "   (" + counts[k].ToString("#,0") + ")") { Checked = isOn(k) }; it.Click += (a, b) => { set(kk); sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(it); }
            return top;
        };
        m.Items.Add(sub("Length", s => DurBand(s.Dur), f => { f.DurMin = 0; f.DurMax = 0; }, k => F.DurMax > 0 && DurBand((F.DurMin + F.DurMax) / 2) == k, k => { double a, b; DurRange(k, out a, out b); F.DurMin = a; F.DurMax = b; }));
        m.Items.Add(sub("Bars", s => { int b = s.Beats > 0 ? (int)Math.Round(s.Beats / 4) : s.Bars; return b > 0 ? b + (b == 1 ? " bar" : " bars") : ""; }, f => f.Bars = 0, k => F.Bars > 0 && k.StartsWith(F.Bars + " "), k => { int b; F.Bars = k.Length > 0 && int.TryParse(k.Split(' ')[0], out b) ? b : 0; }));
        m.Items.Add(sub("Time signature", s => s.Kind == "loop" || s.Kind == "midi" ? "4/4" : "", null, k => false, k => { }));
        m.Items.Add(sub("Channels", s => s.Channels == 1 ? "Mono" : (s.Channels == 2 ? "Stereo" : ""), f => f.Channels = 0, k => (k == "Mono" && F.Channels == 1) || (k == "Stereo" && F.Channels == 2), k => F.Channels = k == "Mono" ? 1 : (k == "Stereo" ? 2 : 0)));
        m.Items.Add(sub("Sample rate", s => s.IsAudio && s.Rate > 0 ? (s.Rate / 1000.0).ToString("0.#") + " kHz" : "", null, k => false, k => { }));
        m.Items.Add(sub("Family", s => SoundLibrary.FamilyOfItem(s), f => f.Family = "", k => F.Family == k, k => F.Family = k));
        m.Items.Add(sub("Character", s => s.Mood.Length > 0 ? s.Mood.Split(',')[0].Trim() : "", f => f.Mood = "", k => F.Mood == k, k => F.Mood = k));
        m.Items.Add(sub("Source and licence", s => SoundLibrary.SourceOf(s), f => f.Source = "", k => F.Source == k, k => F.Source = k));
        var rec = new ToolStripMenuItem("Recently added (last 7 days)") { Checked = F.AddedDays > 0 }; rec.Click += (a, b) => { F.AddedDays = F.AddedDays > 0 ? 0 : 7; sndDirty = true; sndScroll = 0; }; m.Items.Add(rec);
        m.Items.Add(new ToolStripSeparator()); var mk = new ToolStripMenuItem(TargetLabel() + " (made-on-play sounds)"); foreach (ToolStripItem it in TargetMenuStrip().Items.Cast<ToolStripItem>().ToArray()) mk.DropDownItems.Add(it); m.Items.Add(mk);
        var tk = new ToolStripMenuItem("Toolkits"); tk.Click += (a, b) => ToolkitMenu(); m.Items.Add(tk);
        return m;
    }
    static string DurBand(double d) { if (d <= 0) return ""; if (d < 1) return "Under 1 second"; if (d < 4) return "1 to 4 seconds"; if (d < 10) return "4 to 10 seconds"; return "Over 10 seconds"; }
    static void DurRange(string k, out double a, out double b) { a = 0; b = 0; if (k == "Under 1 second") { a = 0.001; b = 0.999; } else if (k == "1 to 4 seconds") { a = 1; b = 3.999; } else if (k == "4 to 10 seconds") { a = 4; b = 9.999; } else if (k == "Over 10 seconds") { a = 10; b = 100000; } }
    // tempo ranges, each labelled with the genres in the library that use it
    static readonly double[][] bpmR = { new[] { 60.0, 84 }, new[] { 85.0, 99 }, new[] { 100.0, 115 }, new[] { 116.0, 124 }, new[] { 125.0, 132 }, new[] { 133.0, 139 }, new[] { 140.0, 150 }, new[] { 151.0, 165 }, new[] { 166.0, 180 } };
    internal static List<KeyValuePair<double, double>> BpmRanges() { var l = new List<KeyValuePair<double, double>>(); foreach (var r in bpmR) if (BpmGenres(r[0], r[1]).Length > 0) l.Add(new KeyValuePair<double, double>(r[0], r[1])); return l; }
    internal static string BpmGenres(double a, double b)
    {
        var n = new Dictionary<string, int>(); foreach (var r in SoundCatalog.Recipes) if (r.Has("loops") || r.Has("drums")) foreach (var t in r.Bpms) if (t >= a && t <= b) { int c; n.TryGetValue(r.Genre, out c); n[r.Genre] = c + 1; }
        var l = new List<KeyValuePair<string, int>>(n); l.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : string.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase)); var names = new List<string>(); foreach (var kv in l) { if (names.Count == 5) { names.Add("more"); break; } names.Add(kv.Key); } return string.Join(", ", names);
    }
    // the Instrument filter: every instrument, under its family, with how many sounds it has
    void InstrumentMenu() { ShowFilterMenu(InstrumentMenuStrip()); }
    internal ContextMenuStrip InstrumentMenuStrip()
    {
        var m = Win.NewMenu(); AddAny(m, "Any instrument", () => sndFilter.Instrument = ""); var counts = CountBy(x => x.Instrument, f => f.Instrument = ""); var fam = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in counts) { string f = FamilyOf(kv.Key); List<string> l; if (!fam.TryGetValue(f, out l)) fam[f] = l = new List<string>(); l.Add(kv.Key); }
        foreach (var kv in fam) { m.Items.Add(new ToolStripMenuItem(kv.Key.ToUpperInvariant()) { Enabled = false }); kv.Value.Sort(StringComparer.OrdinalIgnoreCase); foreach (var ins in kv.Value) { string ii = ins; AddItem(m, "    " + Counted(ins, counts, ins), sndFilter.Instrument == ins, () => sndFilter.Instrument = ii); } }
        return m;
    }
    internal static string FamilyOf(string instrument)
    {
        switch (instrument) { case "Kick": case "Snare": case "Clap": case "Hi-Hat": case "Percussion": case "Cymbal": case "Drums": return "Drums"; case "808": case "Bass Synth": return "Bass"; case "Voice": return "Vocals"; case "FX": return "FX"; }
        var ins = Instruments.Get(instrument); return ins != null ? ins.Group : "Other";
    }
    // the key and tempo the instant sounds are made in
    internal static string TargetText() { string k = InstantLib.TargetKey, b = InstantLib.TargetBpm > 0 ? InstantLib.TargetBpm.ToString("0.##") + " BPM" : ""; return k.Length > 0 && b.Length > 0 ? k + ", " + b : (k.Length > 0 ? k : (b.Length > 0 ? b : "each pack's key and tempo")); }
    static string TargetLabel() { return InstantLib.TargetKey.Length == 0 && InstantLib.TargetBpm <= 0 ? "Make in: pack key" : "Make in: " + TargetText(); }
    void TargetMenu() { ShowFilterMenu(TargetMenuStrip()); }
    internal ContextMenuStrip TargetMenuStrip()
    {
        var m = Win.NewMenu(); Action<string, double> set = (k, b) => { InstantLib.SetTarget(k, b); sndDirty = true; sndScroll = 0; Invalidate(); };
        var head = new ToolStripMenuItem("INSTANT SOUNDS ARE MADE IN THIS KEY AND TEMPO") { Enabled = false }; m.Items.Add(head);
        var own = new ToolStripMenuItem("Each pack's own key and tempo") { Checked = InstantLib.TargetKey.Length == 0 && InstantLib.TargetBpm <= 0 }; own.Click += (a, b) => set("", 0); m.Items.Add(own);
        var proj = new ToolStripMenuItem("My project's tempo (" + Fx.ProjectBpm.ToString("0.##") + " BPM)") { Checked = InstantLib.TargetBpm > 0 && Math.Abs(InstantLib.TargetBpm - Fx.ProjectBpm) < 0.01 }; proj.Click += (a, b) => set(InstantLib.TargetKey, Fx.ProjectBpm); m.Items.Add(proj);
        m.Items.Add(new ToolStripSeparator());
        var keys = new ToolStripMenuItem("Key: " + (InstantLib.TargetKey.Length > 0 ? InstantLib.TargetKey : "each pack's own"));
        var kAny = new ToolStripMenuItem("Each pack's own keys") { Checked = InstantLib.TargetKey.Length == 0 }; kAny.Click += (a, b) => set("", InstantLib.TargetBpm); keys.DropDownItems.Add(kAny); keys.DropDownItems.Add(new ToolStripSeparator());
        for (int mi = 0; mi < 2; mi++) for (int i = 0; i < 12; i++) { string k = SoundLibrary.NoteNames[i] + (mi == 0 ? " min" : " maj"); var it = new ToolStripMenuItem(k) { Checked = InstantLib.TargetKey == k }; it.Click += (a, b) => set(k, InstantLib.TargetBpm); keys.DropDownItems.Add(it); }
        m.Items.Add(keys);
        var tempos = new ToolStripMenuItem("Tempo: " + (InstantLib.TargetBpm > 0 ? InstantLib.TargetBpm.ToString("0.##") + " BPM" : "each pack's own"));
        var tAny = new ToolStripMenuItem("Each pack's own tempos") { Checked = InstantLib.TargetBpm <= 0 }; tAny.Click += (a, b) => set(InstantLib.TargetKey, 0); tempos.DropDownItems.Add(tAny); tempos.DropDownItems.Add(new ToolStripSeparator());
        foreach (double t in new double[] { 70, 80, 85, 90, 95, 100, 105, 110, 115, 120, 124, 126, 128, 130, 132, 135, 138, 140, 145, 150, 160, 170, 174 }) { double tt = t; var it = new ToolStripMenuItem(t.ToString("0") + " BPM") { Checked = Math.Abs(InstantLib.TargetBpm - t) < 0.01 }; it.Click += (a, b) => set(InstantLib.TargetKey, tt); tempos.DropDownItems.Add(it); }
        var ex = new ToolStripMenuItem("Exact tempo..."); ex.Click += (a, b) => { string v = Ask("Tempo in BPM for the instant sounds (50 to 220):", (InstantLib.TargetBpm > 0 ? InstantLib.TargetBpm : Fx.ProjectBpm).ToString("0.##")); double bpm; if (v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bpm) && bpm >= 50 && bpm <= 220) set(InstantLib.TargetKey, bpm); }; tempos.DropDownItems.Add(ex);
        m.Items.Add(tempos);
        m.Items.Add(new ToolStripSeparator()); m.Items.Add(new ToolStripMenuItem("Loops, bass, notes, chords and MIDI follow the key; loops and MIDI follow the tempo. Drum hits and FX stay as they are.") { Enabled = false });
        return m;
    }
    void ToolkitMenu() { var m = Win.NewMenu(); foreach (var set in SoundToolkit.Sets) { var st = set; AddItem(m, set.Name, false, () => { var keep = sndFilter.Text; var f = st.Filter.Copy(); sndFilter.Type = f.Type; sndFilter.Group = f.Group; sndFilter.AnyType = f.AnyType; sndFilter.Kind = f.Kind; sndFilter.Pack = f.Pack; sndFilter.Genre = ""; sndFilter.Sub = ""; sndFilter.Key = ""; sndFilter.BpmMin = 0; sndFilter.BpmMax = 0; sndFilter.OnlyFav = false; sndFilter.OnlyRecent = false; sndFilter.Collection = ""; }); } ShowFilterMenu(m); }
    void CollectionsMenu()
    {
        var m = Win.NewMenu(); AddAny(m, "All sounds", () => sndFilter.Collection = ""); lock (SoundLibrary.Lock) foreach (var kv in SoundLibrary.Collections) { string n = kv.Key; AddItem(m, n + "  (" + kv.Value.Count + ")", sndFilter.Collection == n, () => sndFilter.Collection = n); }
        if (SoundLibrary.Collections.Count == 0) m.Items.Add("(none yet: use ADD TO FOLDER on a sound)").Enabled = false; ShowFilterMenu(m);
    }
    void SoundMenu(SoundItem s)
    {
        var m = Win.NewMenu(); m.Items.Add("Add to timeline").Click += (a, b) => AddSampleAtNeedle(s);
        if (s.IsAudio) { m.Items.Add("Load it into the piano roll (play it from notes)").Click += (a, b) => SoundToSampler(s); if (PrShown && PrSampler != null) m.Items.Add("Put it on the piano roll's sampler (" + prTrack.Name + ")").Click += (a, b) => { PushUndo(); var sp = PrSampler; string e2 = sp.Zones.Count == 0 ? sp.SetSingle(s.Path, s.Name.Replace(s.PackName + " - ", ""), s.Type == "808" || s.Group == "Bass", 60) : sp.AddToKit(s.Path, s.Name.Replace(s.PackName + " - ", ""), 36 + sp.Zones.Count); Toast(e2 ?? "Done"); }; } m.Items.Add(SoundLibrary.Favourites.Contains(s.Id) ? "Remove from favourites" : "Add to favourites").Click += (a, b) => { SoundLibrary.ToggleFav(s); sndDirty = true; }; m.Items.Add(new ToolStripSeparator());
        lock (SoundLibrary.Lock) foreach (var kv in SoundLibrary.Collections) { string n = kv.Key; bool inC = kv.Value.Contains(s.Id); m.Items.Add((inC ? "Take out of folder: " : "Add to folder: ") + n).Click += (a, b) => { if (inC) SoundLibrary.RemoveFromCollection(n, s); else SoundLibrary.AddToCollection(n, s); sndDirty = true; }; }
        m.Items.Add("New folder with this sound...").Click += (a, b) => { string n = Ask("Folder name (for example: Drill piano, Drill bass):", "My folder"); if (n != null) { SoundLibrary.AddToCollection(n.Trim().Replace("|", "/"), s); Toast("Added to " + n); } };
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Show in folder").Click += (a, b) => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + s.Path + "\""); } catch { } }; ShowFilterMenu(m);
    }
    void AddAny(ContextMenuStrip m, string text, Action a) { var it = new ToolStripMenuItem(text); it.Click += (x, y) => { a(); sndDirty = true; sndScroll = 0; }; m.Items.Add(it); m.Items.Add(new ToolStripSeparator()); }
    void AddItem(ContextMenuStrip m, string text, bool on, Action a) { var it = new ToolStripMenuItem(text) { Checked = on }; it.Click += (x, y) => { a(); sndDirty = true; sndScroll = 0; }; m.Items.Add(it); }
    // IMPORT AUDIO (under PRODUCER TAG and in the sound packs): sounds from your computer are copied into My samples, so they stay in your library
    internal void ImportAudioAsk()
    {
        using (var d = new OpenFileDialog { Title = "Import sounds from your computer", Multiselect = true, Filter = "Audio and MIDI|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif;*.m4a;*.mid|All files|*.*" })
            if (d.ShowDialog(this) == DialogResult.OK) Toast(ImportAudioFrom(d.FileNames));
    }
    internal string ImportAudioFrom(string[] files)
    {
        int n = SoundLibrary.ImportFiles(files, null); NoteDupes(); if (n == 0) return "Nothing new was imported (already in My samples, or not a sound file: WAV, MP3, FLAC, OGG, AIFF, M4A or MIDI)." + SoundLibrary.DupeText;
        ShowMySounds();
        return "Imported " + n + " sound" + (n == 1 ? "" : "s") + " into MY SOUNDS (kept in your library for good, the tab beside SOUNDS): drag them onto the song, a pad or the piano roll.";
    }
    // the sounds panel showing only your imported sounds (MY SOUNDS)
    internal void ShowMySounds()
    {
        OpenSounds(); packsOpen = false; sndFilter.Text = ""; sndFilter.Genre = ""; sndFilter.Sub = ""; sndFilter.Type = ""; sndFilter.Group = ""; sndFilter.Kind = ""; sndFilter.OnlyFav = false; sndFilter.OnlyRecent = false; sndFilter.Collection = ""; sndFilter.Pack = SoundLibrary.UserPackId;
        if (soundBox != null && soundBox.Text.Length > 0) soundBox.Text = ""; sndDirty = true; sndScroll = 0; Invalidate();
    }
    void ImportMenu()
    {
        var m = Win.NewMenu();
        m.Items.Add("Import my own samples (wav, mp3, flac, midi...)").Click += (a, b) =>
        {
            using (var d = new OpenFileDialog { Title = "Choose samples you own", Multiselect = true, Filter = "Audio and MIDI|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.m4a;*.mid|All files|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) { int n = SoundLibrary.ImportFiles(d.FileNames, null); ShowMySounds(); Toast("Imported " + n + " sound" + (n == 1 ? "" : "s") + " into MY SOUNDS (kept for good)" + SoundLibrary.DupeText); NoteDupes(); }
        };
        m.Items.Add("Import a folder of samples (and its sub-folders)").Click += (a, b) =>
        {
            using (var d = new FolderBrowserDialog { Description = "Choose a folder of samples you own (WAV, MP3, FLAC, OGG, AIFF, M4A and MIDI are read, with the tempo and key in their names)" })
                if (d.ShowDialog(this) == DialogResult.OK) { var files = SoundLibrary.SamplesIn(d.SelectedPath); int n = SoundLibrary.ImportFiles(files.ToArray(), null); ShowMySounds(); Toast("Imported " + n + " of " + files.Count + " files from " + Path.GetFileName(d.SelectedPath) + " into MY SOUNDS (kept for good)" + SoundLibrary.DupeText); NoteDupes(); }
        };
        m.Items.Add("Import a sound pack (.zip)").Click += (a, b) =>
        {
            using (var d = new OpenFileDialog { Title = "Choose a Purphex sound pack", Filter = "Sound pack|*.zip" })
                if (d.ShowDialog(this) == DialogResult.OK) { string err = SoundLibrary.ImportPackZip(d.FileName); sndDirty = true; Toast(err ?? "Pack added"); }
        };
        m.Items.Add("Open the sounds folder").Click += (a, b) => { try { Directory.CreateDirectory(SoundLibrary.Root); System.Diagnostics.Process.Start("explorer.exe", "\"" + SoundLibrary.Root + "\""); } catch { } }; ShowFilterMenu(m);
    }

    // ---- drag ghost ----
    void PaintSoundDrag(Graphics g)
    {
        if (!sndDragging || sndDrag == null) return; var tl = TimelineRect(); float top = ArrTop + RulerH; bool over = MousePos.X >= HeadW && MousePos.X <= tl.Right && MousePos.Y >= top;
        if (over)
        {
            long frame = DropFrame(MousePos.X); float gx = HeadW + (float)((frame - scroll) / spp); int lanes = LaneCount; int lane = Math.Max(0, Math.Min(lanes, (int)((MousePos.Y - top + vscroll) / LaneH)));
            float gy = top + lane * LaneH - vscroll; double dur = sndDrag.Dur > 0.01 ? sndDrag.Dur : 1; float gw = Math.Max(8, (float)(dur * StudioEngine.SR / spp));
            var gs = g.Save(); g.SetClip(Rc(HeadW, top, tl.Right - HeadW, ClientSize.Height - top), System.Drawing.Drawing2D.CombineMode.Intersect);
            var gr = Rc(gx, gy + 4, gw, LaneH - 8); FillRR(g, Color.FromArgb(70, NeonCyan), gr, 8);
            var pk = sndDrag.Peaks; if (pk != null && pk.Length > 1 && gw > 12)
            {   // the waveform of the sound, the same way a clip shows it
                float mid = gr.Y + gr.Height / 2 + 6, amp = (gr.Height - 26) / 2; int bars = (int)Math.Min(gw / 3, 600);
                using (var pen = new Pen(Color.FromArgb(200, Color.White), 1.4f)) for (int b = 0; b < bars; b++) { float v = pk[Math.Min(pk.Length - 1, b * pk.Length / Math.Max(1, bars))] / 255f * amp; float bx = gr.X + 2 + b * (gw - 4) / bars; g.DrawLine(pen, bx, mid - v, bx, mid + v); }
            }
            using (var pen = new Pen(Color.FromArgb(240, NeonCyan), 1.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) using (var p = RR(gr, 8)) g.DrawPath(pen, p);
            string where = lane >= lanes ? "NEW TRACK" : eng.Tracks[lane].Name.ToUpperInvariant(); Txt(g, sndDrag.Name.Replace(sndDrag.PackName + " - ", "") + "  -  " + where, 10, Color.White, Rc(gr.X + 8, gr.Y + 3, Math.Max(40, gr.Width - 16), 16), StringAlignment.Near, true);
            using (var pen = new Pen(Color.FromArgb(220, NeonCyan), 2f)) g.DrawLine(pen, gx, top, gx, ClientSize.Height - 20);
            g.Restore(gs);
        }
        var r = Rc(MousePos.X + 12, MousePos.Y + 10, 230, 24); FillRR(g, TM.C(235, 30, 14, 64), r, 8); Glow(g, over ? NeonCyan : NeonPink, r, 8, 0.8f, 2); Txt(g, (over ? "Drop to add: " : "Drag to the timeline: ") + sndDrag.Name.Replace(sndDrag.PackName + " - ", ""), 11, Ink, r, StringAlignment.Center, true);
    }

    // ---- the pack manager ----
    string PackSizeText(SoundPack p) { long b; if (!packBytes.TryGetValue(p.Id, out b)) { b = p.Bytes; packBytes[p.Id] = b; } return b > 1048576 ? (b / 1048576.0).ToString("0") + " MB" : (b / 1024) + " KB"; }
    internal LibraryCheck.Report lastCheck; volatile bool checking;
    // opens every file the library lists, says what is missing or damaged, offers to repair those packs, and writes the manifest
    internal void StartLibraryCheck()
    {
        if (checking) return; checking = true; packMsg = "Checking every file in the library...";
        new Thread(() => { LibraryCheck.Report rep = null; try { rep = LibraryCheck.Check(true, null); } catch (Exception ex) { packMsg = "Couldn't check the library: " + ex.Message; } checking = false; if (rep != null) { lastCheck = rep; packMsg = rep.Summary; } try { BeginInvoke(new Action(Invalidate)); } catch { } }) { IsBackground = true }.Start();
    }
    void StartInstall(Recipe r)
    {
        lock (installQ) { if (installQ.Contains(r.Id) || installing == r.Id) return; installQ.Enqueue(r.Id); }
        if (installing.Length > 0) return; installCancel = false; new Thread(InstallLoop) { IsBackground = true }.Start();
    }
    void InstallLoop()
    {
        while (true)
        {
            string id; lock (installQ) { if (installQ.Count == 0) { installing = ""; return; } id = installQ.Dequeue(); installing = id; } var rec = SoundCatalog.Find(id); installPct = 0; installNote = "Starting...";
            try
            {
                var res = LibraryBaker.BakePack(rec, (note, p) => { installNote = note; installPct = p; }, () => installCancel); var pack = res.Pack;
                if (pack != null) { SoundLibrary.AddOrReplace(pack); packBytes.Remove(id); packMsg = "Installed " + rec.Name + ": " + res.Made.ToString("#,0") + " sounds made and checked" + (res.Resumed > 0 ? " (+" + res.Resumed + " from before)" : "") + (res.Failed > 0 ? ", " + res.Failed + " left out (" + (res.Duplicates + res.Similar) + " copies, " + (res.Failed - res.Duplicates - res.Similar) + " failed checks)" : ""); }
            }
            catch (OperationCanceledException) { packMsg = "Stopped " + rec.Name + ": what was made is kept, Install carries on from there"; }
            catch (Exception ex) { packMsg = "Couldn't make " + rec.Name + ": " + ex.Message; }
            sndDirty = true; if (installCancel) { lock (installQ) installQ.Clear(); }
            try { BeginInvoke(new Action(Invalidate)); } catch { }
        }
    }

    // what a pack holds: its real sounds when installed (by kind), or what installing makes
    readonly Dictionary<string, string> packContents = new Dictionary<string, string>();
    string PackContents(Recipe rec, SoundPack ip)
    {
        bool inst = ip != null && ip.Items.Count > 0; string key = rec.Id + (inst ? "#" + ip.Items.Count : ""); string v; if (packContents.TryGetValue(key, out v)) return v;
        var n = new Dictionary<string, int>(); Func<SoundItem, string> kind = s => s.Kind == "preset" ? (s.Format == "Purphex Wobble preset" ? "Wobble presets" : "Bass Lab presets") : s.Kind == "midi" ? "MIDI" : (s.Kind == "loop" ? (s.Group == "Bass" ? "bass loops" : (s.Type == "Drum Loop" || s.Type == "Top Loop" ? "drum loops" : (s.Type == "Drum Fill" ? "fills" : (s.Type.EndsWith(" Loop") && (s.Instrument == "Kick" || s.Instrument == "Snare" || s.Instrument == "Hi-Hat" || s.Instrument == "Percussion") ? "kit loops" : "music loops")))) : (s.Type == "808" ? "808s" : (s.Group == "Drums" ? "drum hits" : (s.Group == "Bass" ? "bass shots" : (s.Group == "FX" ? "FX" : "one-shots")))));
        foreach (var s in inst ? ip.Items : LibraryBaker.Plan(rec)) { string k = kind(s); int c; n.TryGetValue(k, out c); n[k] = c + 1; }
        var order = new[] { "drum loops", "bass loops", "808s", "drum hits", "kit loops", "fills", "music loops", "bass shots", "one-shots", "FX", "MIDI", "Wobble presets", "Bass Lab presets" }; var parts = new List<string>(); int total = 0;
        foreach (var k in order) { int c; if (n.TryGetValue(k, out c) && c > 0) { parts.Add(c + " " + k); total += c; } }
        v = (inst ? "" : "Installs ") + total + " sounds: " + string.Join(", ", parts.Take(4).ToArray()) + (parts.Count > 4 ? " and more" : ""); packContents[key] = v; return v;
    }
    // hear a pack before installing: a drum loop, then a bass loop (made on play)
    void PreviewPack(Recipe rec)
    {
        SoundItem first = null; foreach (var s in InstantLib.ItemsOf(rec.Id)) if (s.Type == "Drum Loop" || (first == null && s.Kind == "loop")) { first = s; if (s.Type == "Drum Loop") break; }
        if (first == null) { Toast("This pack has nothing to preview."); return; } PreviewSound(first); Toast("Previewing " + rec.Name + ": " + first.Core);
    }
    void PaintPackManager(Graphics g)
    {
        if (!packsOpen) return; if (!soundsOpen || tab != 0) { packsOpen = false; return; }
        if (!LibLoader.Ready) return;   // (the loading screen of the sounds panel is showing)
        // the packs live in the sounds panel: same place, same size (or the whole Mix page in fullscreen)
        float px0 = ClientSize.Width - PanelW, py0 = ToolH + TransH; var box = Rc(px0, py0, PanelW, ClientSize.Height - py0); bool wide = box.Width >= 1000;
        using (var b = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(b, box); using (var pen = new Pen(Color.FromArgb(150, NeonViolet))) g.DrawLine(pen, box.X, box.Y, box.X, box.Bottom); Reg(box, () => { });
        SoundTabs(g, box.X + 14, box.Y + 7, true);
        var clp = Rc(box.Right - 38, box.Y + 8, 28, 26); Btn(g, clp, "x", 0, () => CloseSounds(), 13); Btn(g, Rc(box.Right - 156, box.Y + 8, 112, 26), sndFull ? "Exit full" : "Fullscreen", sndFull ? 4 : 0, () => { sndFull = !sndFull; }, 11);
        EnsurePackBox(); { var pr = wide ? new Rectangle((int)(box.X + 376), (int)(box.Y + 8), (int)Math.Max(160, box.Width - 376 - 176), 26) : new Rectangle((int)(box.X + 14), (int)(box.Y + 40), (int)(box.Width - 28), 26); if (packBox.Bounds != pr) packBox.Bounds = pr; if (!packBox.Visible) packBox.Visible = true; if (packQuery.Length == 0 && !packBox.Focused) Txt(g, "Search packs: drill, amapiano, dark, guitar, 140 bpm...", 11, DimC, Rc(pr.X + 8, pr.Y + 5, pr.Width - 16, 18), StringAlignment.Near, false); }
        float infoY = wide ? box.Y + 40 : box.Y + 70;
        { int instN = 0, files0 = 0; lock (SoundLibrary.Lock) foreach (var p in SoundLibrary.Packs) if (p.Items.Count > 0 && p.Id != SoundLibrary.UserPackId) { instN++; files0 += p.Items.Count; }
          Txt(g, SoundCatalog.Recipes.Count + " packs  ·  " + instN + " installed (" + files0.ToString("#,0") + " real sound files). Install makes a pack's sounds on this PC and checks every one" + (wide ? " (tempo, length, clipping, silence, copies)." : "."), 11, DimC, Rc(box.X + 14, infoY, box.Width - 28, 18), StringAlignment.Near, false); }
        // sections
        var secs = new List<string> { "All" }; foreach (var r in SoundCatalog.Recipes) if (!secs.Contains(r.Section)) secs.Add(r.Section); secs.Add("Installed"); float sx = box.X + 14, sy = wide ? box.Y + 66 : box.Y + 104;
        foreach (var sc in secs) { string s2 = sc; float bw = Math.Max(62, sc.Length * 8 + 26); if (sx + bw > box.Right - 14) { sx = box.X + 14; sy += 34; } Chip(g, Rc(sx, sy, bw, 28), sc, packSection == sc, () => { packSection = s2; packScroll = 0; }); sx += bw + 6; }
        float listTop = sy + 38;
        // list
        float footH = wide ? 70 : 104; var list = Rc(box.X + 8, listTop, box.Width - 16, box.Bottom - footH - listTop); var rows = new List<Recipe>(); foreach (var r in SoundCatalog.Recipes) { var ip = SoundLibrary.Pack(r.Id); bool inst = ip != null && ip.Items.Count > 0; if ((packSection == "All" || (packSection == "Installed" ? inst : r.Section == packSection)) && PackMatches(r, packQuery)) rows.Add(r); }
        float rowH = 66; float maxS = Math.Max(0, rows.Count * rowH - list.Height); packScroll = Math.Max(0, Math.Min(maxS, packScroll)); Reg(list, () => { }).OnWheel = d => { packScroll = Math.Max(0, Math.Min(maxS, packScroll - d * 66)); };
        var gs = g.Save(); g.SetClip(list, CombineMode.Intersect); int first = Math.Max(0, (int)(packScroll / rowH)), last = Math.Min(rows.Count - 1, first + (int)(list.Height / rowH) + 1);
        if (rows.Count == 0) Txt(g, packQuery.Length > 0 ? "NO PACK MATCHES \"" + packQuery.ToUpperInvariant() + "\"." : "NOTHING HERE YET.", 14, DimC, Rc(list.X + 10, list.Y + 20, 400, 24), StringAlignment.Near, false);
        for (int i = first; i <= last; i++)
        {
            var rec = rows[i]; var rr = Rc(list.X, list.Y + i * rowH - packScroll, list.Width - 52, rowH - 4); var ip = SoundLibrary.Pack(rec.Id); bool inst = ip != null && ip.Items.Count > 0; bool busyNow = installing == rec.Id; bool queued; lock (installQ) queued = installQ.Contains(rec.Id);
            FillRR(g, TM.C(120, 34, 20, 64), rr, 8); var tile = Rc(rr.X + 6, rr.Y + 6, 50, 50); var fake = new SoundPack { Hue = rec.Hue }; DrawPackTile(g, tile, fake, rec.Name);
            Txt(g, rec.Name, 14, Ink, Rc(rr.X + 66, rr.Y + 6, rr.Width - 330, 22), StringAlignment.Near, true); Txt(g, rec.Genre + "  ·  " + rec.Sub + "  ·  " + rec.Mood, 10, DimC, Rc(rr.X + 66, rr.Y + 28, rr.Width - 330, 16), StringAlignment.Near, false); Txt(g, PackContents(rec, ip), 10, DimC, Rc(rr.X + 66, rr.Y + 44, rr.Width - 330, 16), StringAlignment.Near, false); Hint2(Rc(rr.X + 66, rr.Y + 4, rr.Width - 330, 56), rec.Desc);
            float bx = rr.Right - 250;
            if (busyNow) { var pb = Rc(bx, rr.Y + 12, 170, 10); FillRR(g, TM.C(200, 8, 4, 18), pb, 5); FillRR(g, NeonCyan, Rc(pb.X, pb.Y, Math.Max(6, pb.Width * installPct), pb.Height), 5); Txt(g, ((int)(installPct * 100)) + "%  " + installNote, 10, DimC, Rc(bx, rr.Y + 26, 240, 16), StringAlignment.Near, false); Btn(g, Rc(bx + 176, rr.Y + 8, 66, 22), "Stop", 3, () => installCancel = true, 10); }
            else if (queued) Txt(g, "WAITING...", 12, DimC, Rc(bx, rr.Y + 18, 240, 24), StringAlignment.Near, false);
            else if (inst) { bool old = PackBuilder.Outdated(ip); Txt(g, (old ? "Older sound: " : "Installed: ") + ip.Items.Count + " sounds, " + PackSizeText(ip), 11, old ? Color.FromArgb(255, 200, 90) : Color.FromArgb(120, 232, 150), Rc(bx, rr.Y + 8, 240, 18), StringAlignment.Near, false); Btn(g, Rc(bx, rr.Y + 30, 76, 26), "Browse", 1, () => { packsOpen = false; sndFilter.Pack = rec.Id; sndDirty = true; sndScroll = 0; }, 11); Btn(g, Rc(bx + 80, rr.Y + 30, 76, 26), old ? "Update" : "Remake", old ? 2 : 0, () => StartInstall(rec), 11); Btn(g, Rc(bx + 160, rr.Y + 30, 76, 26), "Remove", 3, () => { if (MessageBox.Show(this, "Remove \"" + rec.Name + "\" and its files from this PC? You can install it again any time.", "Remove pack", MessageBoxButtons.YesNo) == DialogResult.Yes) { if (sndFilter.Pack == rec.Id) sndFilter.Pack = ""; if (sndSel != null && sndSel.PackId == rec.Id) { Auditioner.Stop(); sndSel = null; } SoundLibrary.RemovePack(rec.Id); sndDirty = true; packMsg = "Removed " + rec.Name; } }, 11); }
            else { var pv = Rc(bx, rr.Y + 16, 72, 32); Btn(g, pv, "Preview", 0, () => PreviewPack(rec), 11); Hint2(pv, "Hear a drum loop and a bass loop from this pack before installing it"); Btn(g, Rc(bx + 76, rr.Y + 16, 160, 32), "Install", 2, () => StartInstall(rec), 13); }
        }
        g.Restore(gs); VScroll(g, Rc(list.Right - 48, list.Y, 42, list.Height), rows.Count * rowH, list.Height, () => packScroll, v => packScroll = v);
        // footer
        float fy = box.Bottom - (wide ? 56 : 92); int notInst = 0; foreach (var r in rows) { var ip = SoundLibrary.Pack(r.Id); if (ip == null || ip.Items.Count == 0) notInst++; }
        Btn(g, Rc(box.X + 14, fy, wide ? 230 : 200, 32), (wide ? "Install all " : "Install all ") + notInst + (wide ? " in this list" : ""), notInst > 0 ? 1 : 0, () => { if (notInst > 5 && !AskYes("Install " + notInst + " packs? That is about " + (notInst * LibraryBaker.PlanSize).ToString("N0") + " sounds, roughly " + Math.Max(1, notInst * 30 / 1024.0).ToString("0.#") + " GB on this PC, and about " + Math.Max(1, notInst / 60.0).ToString("0.#") + " hours to make (Purphex keeps working while it does; Stop cancels the queue).", "Install many packs")) return; foreach (var r in rows) { var ip = SoundLibrary.Pack(r.Id); if (ip == null || ip.Items.Count == 0) StartInstall(r); } }, 12);
        float ix = box.X + (wide ? 252 : 222); Btn(g, Rc(ix, fy, 120, 32), "Import...", 0, () => ImportMenu(), 12);
        Btn(g, Rc(ix + 128, fy, 150, 32), checking ? "Checking..." : "Check library", 0, () => StartLibraryCheck(), 12);
        float row2 = wide ? fy : fy + 38, rx0 = wide ? ix + 286 : box.X + 14;
        if (lastCheck != null && lastCheck.PacksToRepair.Count > 0 && !checking) Btn(g, Rc(rx0, row2, 170, 32), "Repair " + lastCheck.PacksToRepair.Count + " pack" + (lastCheck.PacksToRepair.Count == 1 ? "" : "s"), 2, () => { foreach (var id in lastCheck.PacksToRepair) { var rr = SoundCatalog.Find(id); if (rr != null) StartInstall(rr); } lastCheck = null; packMsg = "Repairing: the packs with missing or damaged files are being made again."; }, 12);
        float fx2 = rx0 + (lastCheck != null && lastCheck.PacksToRepair.Count > 0 ? 178 : 0);
        { var olds = new List<Recipe>(); foreach (var r in SoundCatalog.Recipes) if (PackBuilder.Outdated(SoundLibrary.Pack(r.Id))) olds.Add(r);   // packs made before the sound got better: one press remakes them all
          if (olds.Count > 0) { Btn(g, Rc(fx2, row2, 250, 32), "Update " + olds.Count + " pack" + (olds.Count == 1 ? "" : "s") + " to the better sound", 2, () => { foreach (var r in olds) StartInstall(r); }, 12); } }
        { bool anyOld = false; foreach (var r in SoundCatalog.Recipes) if (PackBuilder.Outdated(SoundLibrary.Pack(r.Id))) { anyOld = true; break; } float mx = fx2 + (anyOld ? 258 : 0);
          Txt(g, installing.Length > 0 ? "Making " + (SoundCatalog.Find(installing) != null ? SoundCatalog.Find(installing).Name : "") + "..." : packMsg, 12, installing.Length > 0 ? NeonCyan : Color.FromArgb(120, 232, 150), Rc(mx, row2 + 6, Math.Max(40, box.Right - 14 - mx), 20), StringAlignment.Near, true); }
        Txt(g, "Licence: " + SoundLibrary.LicenceText, 9, DimC, Rc(box.X + 14, box.Bottom - 16, box.Width - 28, 14), StringAlignment.Near, false);
    }
}
