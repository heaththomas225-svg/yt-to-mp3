using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Purphex Music: instrument tracks (Bass Lab, or any VST instrument): add one, play it, record what you play, and hear it in the mix and exports.
partial class StudioForm
{
    void AddInstrumentClick(bool keepInsert = false)
    {
        if (!keepInsert) nextInsert = -1;
        var items = new List<FxRegistry.Info>(); foreach (var i in FxRegistry.All) if (i.Category == "Instruments") items.Add(i);
        if (items.Count == 0)
        {
            MessageBox.Show(this, "No VST instrument is set up yet.\n\nTo use a VST instrument:\n  1. On the main window press Menu, then Plugins and extensions.\n  2. Your installed VST2 plugins are listed there (or press Open plugins folder and copy the .dll in).\n  3. Tick the instrument. It loads straight away.\n  4. Come back here and press + INSTRUMENT.\n\nOnly 64-bit VST2 plugins work (VST3 .vst3 files are not supported yet).", "Add instrument", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (items.Count == 1) { AddInstrument(items[0].Type); return; }
        var m = Win.NewMenu(); foreach (var it in items) { string ty = it.Type; m.Items.Add(it.Name).Click += (a, b) => AddInstrument(ty); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // an empty MIDI track: what you play on a MIDI keyboard (a piano, a guitar with MIDI...) is recorded onto it; Bass Lab or a VST can be added later from + ADD EFFECT
    internal Track AddMidiTrack()
    {
        var t = StudioEngine.NewTrack("MIDI", "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFF3B8CF5); AddTrack(t); int i = eng.Tracks.IndexOf(t); if (i >= 0) selTrack = i;
        recOn = true; Toast("MIDI track added and MIDI REC is on: play your MIDI keyboard and it is heard and recorded. It plays with the built-in keys; add Bass Lab or a VST from + ADD EFFECT for other sounds."); return t;
    }
    // notes from a MIDI keyboard when the picked track is a MIDI track with no instrument on it: they are recorded onto that track
    internal bool MidiToBareTrack(byte s, byte d1, byte d2)
    {
        try
        {
            if (BlMidi(s, d1, d2)) return true;   // the Beat Lab is open: the keyboard's pads play its pads
            if (MidiInput.Target != null) return false;   // an instrument's own window is in front: it gets the keys while you use it
            if (eng.Tracks.Count == 0) return false; var t = eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))]; if (!t.Instrument || t.IsBus) return false;
            // the picked MIDI track plays what is on it (a VST, Bass Lab, a Purphex instrument, or the built-in keys when it has none), through its effects, and MIDI REC records it
            // (before, only an instrument whose window was in front got the keyboard: pick a track with a VST and nothing played or recorded)
            InstrumentFx k = null; foreach (var f in t.Fx) { var i = f as InstrumentFx; if (i != null && i.IsInstrument && !f.Bypass) { k = i; break; } } if (k == null) k = t.Keys; k.Owner = t;
            int kind = s & 0xF0; if (kind == 0x90 && d2 > 0) k.NoteOn(d1, d2); else k.Midi(s, d1, d2);   // heard straight away
            if (kind == 0x90 || kind == 0x80) { bool on = kind == 0x90 && d2 > 0; OnMidiNote(t, on, d1, on ? d2 : 0); } return true;
        }
        catch { return false; }
    }
    // the MIDI keyboard is connected by itself: the one you picked, else the first one plugged in (checked every few seconds, so plugging it in later works too)
    System.Windows.Forms.Timer midiTimer; string midiToastFor = "";
    internal void MidiAutoConnect()
    {
        if (StudioEnv.UnderTest || Prefs.MidiDev == MidiInput.OffName) return;
        try
        {
            var devs = MidiInput.Devices(); if (MidiInput.OpenName.Length > 0) { if (devs.Contains(MidiInput.OpenName)) return; MidiInput.Close(); }
            if (devs.Count == 0) return; string want = Prefs.MidiDev.Length > 0 && devs.Contains(Prefs.MidiDev) ? Prefs.MidiDev : devs[0];
            bool ok = MidiInput.Open(want); string key = want + (ok ? "|ok" : "|" + MidiInput.Status);
            if (key != midiToastFor) { midiToastFor = key; Toast(ok ? "MIDI keyboard connected: " + want + ". It plays the selected MIDI track (right-click MIDI REC to pick another keyboard)." : MidiInput.Status); }
        }
        catch { }
    }
    void MidiDeviceMenu()
    {
        var m = Win.NewMenu(); var devs = MidiInput.Devices(); m.Items.Add(new ToolStripMenuItem("MIDI KEYBOARD: " + (MidiInput.OpenName.Length > 0 ? MidiInput.OpenName : "not connected")) { Enabled = false });
        foreach (var d0 in devs) { string d = d0; var it = new ToolStripMenuItem(d) { Checked = MidiInput.OpenName == d }; it.Click += (a, b) => { Prefs.MidiDev = d; Prefs.Changed(); MidiInput.Close(); midiToastFor = ""; MidiAutoConnect(); }; m.Items.Add(it); }
        if (devs.Count == 0) m.Items.Add(new ToolStripMenuItem("No MIDI keyboard found: plug one in (it connects by itself)") { Enabled = false });
        var off = new ToolStripMenuItem("Off (no MIDI keyboard)") { Checked = Prefs.MidiDev == MidiInput.OffName }; off.Click += (a, b) => { Prefs.MidiDev = MidiInput.OffName; Prefs.Changed(); MidiInput.Close(); Toast("MIDI keyboard off."); }; m.Items.Add(off);
        if (Prefs.MidiDev == MidiInput.OffName) { var on = new ToolStripMenuItem("Switch the MIDI keyboard back on"); on.Click += (a, b) => { Prefs.MidiDev = ""; Prefs.Changed(); midiToastFor = ""; MidiAutoConnect(); }; m.Items.Add(on); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void AddInstrument(string type)
    {
        InstrumentFx f = null;
        RunBusy("Starting the instrument (the first start of a big plugin can take a few seconds)...", false, p => { f = FxRegistry.Create(type) as InstrumentFx; }, ok =>
        {
            if (!ok || f == null) { Toast("That instrument couldn't be started."); return; }
            var t = StudioEngine.NewTrack(f.Title, "inst", null); t.Instrument = true; t.Color = unchecked((int)0xFFFFB84D); f.Owner = t;
            lock (eng.Sync) t.Fx.Add(f); AddTrack(t); f.ShowWindow(this);
            Toast("Instrument added. Play notes in its window. For recording: press REC, then PLAY.");
        });
    }

    // Notes played in an instrument window are recorded onto its track while the song plays and REC is armed.
    void OnMidiNote(Track t, bool on, int note, int vel)
    {
        if (!recOn || t == null || !t.Instrument) return;
        long frame;
        if (!eng.Playing) { frame = eng.Pos; try { BeginInvoke(new Action(() => { if (!eng.Playing) PlayGuard(); })); } catch { } }   // the first note starts the song and is recorded at the needle
        else frame = Math.Max(0, (long)dispPos);
        var ev = new MidiEv { Frame = frame, S = (byte)(on ? 0x90 : 0x80), D1 = (byte)note, D2 = (byte)(on ? vel : 0) };
        lock (t.Midi)
        {
            int i = t.Midi.Count; while (i > 0 && t.Midi[i - 1].Frame > frame) i--;
            t.Midi.Insert(i, ev);
        }
    }

    // Small bars for the recorded notes (length = how long the key was held, height = pitch).
    struct MBar { public long A, B; public int N; }
    void PaintMidi(Graphics g, Track tr, RectangleF lane)
    {
        int count; lock (tr.Midi) count = tr.Midi.Count;
        if (count == 0) { bool bare = !tr.HasInstrumentFx; Txt(g, bare ? "MIDI track (built-in keys): press MIDI REC and play your MIDI keyboard. Add Bass Lab or a VST from + ADD EFFECT for other sounds." : "Instrument track: press MIDI REC, then PLAY, then play your keyboard or the instrument's piano.", 12, DimC, Rc(lane.X + 10, lane.Y + 44, lane.Width - 20, 40), StringAlignment.Near, false); return; }
        PaintMidiClips(g, tr, lane); if (tr.MClips.Count > 0) return;   // the notes in their clips
        MidiEv[] evs; lock (tr.Midi) evs = tr.Midi.ToArray(); var open = new Dictionary<int, long>(); var bars = new List<MBar>(); int lo = 127, hi = 0;
            foreach (var m in evs)
            {
                int note = m.D1;
                if ((m.S & 0xF0) == 0x90 && m.D2 > 0) { open[note] = m.Frame; lo = Math.Min(lo, note); hi = Math.Max(hi, note); }
                else if ((m.S & 0xF0) == 0x80 || ((m.S & 0xF0) == 0x90 && m.D2 == 0)) { long st; if (open.TryGetValue(note, out st)) { bars.Add(new MBar { A = st, B = m.Frame, N = note }); open.Remove(note); } }
            }
        foreach (var kv in open) bars.Add(new MBar { A = kv.Value, B = kv.Value + StudioEngine.SR / 2, N = kv.Key });
        float range = Math.Max(12, hi - lo + 2); var col = Neon(tr.Color); g.SetClip(lane);
        using (var br = new SolidBrush(Color.FromArgb(220, col)))
            foreach (var b in bars)
            {
                float x0 = lane.X + (float)((b.A - scroll) / spp), x1 = lane.X + (float)((b.B - scroll) / spp); if (x1 < lane.X || x0 > lane.Right) continue;
                float y = lane.Bottom - 10 - (b.N - lo + 1) / range * (lane.Height - 24);
                g.FillRectangle(br, x0, y, Math.Max(2, x1 - x0), 5);
            }
        g.ResetClip();
        Txt(g, count / 2 + " notes", 11, Color.FromArgb(200, 255, 255, 255), Rc(lane.X + 8, lane.Y + 6, 200, 16), StringAlignment.Near, true);
    }
}
