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
    // back in the studio: the keyboard plays the picked track again, unless a Purphex Pads or Key Map window is open (it keeps the keyboard until it closes)
    internal void StudioActivatedMidi() { if (!MidiWindow.Holds(MidiInput.Target)) MidiInput.Target = null; }
    volatile bool keysTrackAsked;
    internal bool MidiToBareTrack(byte s, byte d1, byte d2)
    {
        try
        {
            var tg = MidiInput.Target; var tgp = tg as PadsFx;
            if (tg != null && (tgp == null || tgp.Accepts(s, d1))) return false;   // an instrument's own window holds the keyboard (Pads, Key Map, Bass Lab, a VST); with Pads, only its pads: your keys still play your instrument
            if (BlMidi(s, d1, d2)) return true;   // the Beat Lab is open: the keyboard's pads play its pads
            // the picked track when it is a MIDI track, else the first MIDI track of the song (an audio track picked played nothing at all);
            // a song with no MIDI track gets one ("Keys", the built-in piano) the first time a key is pressed
            // the piano roll open on a track (or on your pattern): the keyboard plays and records that, the one you are looking at
            Track t = PrShown && prTrack != null && prTrack.Instrument && !prTrack.IsBus ? prTrack : (eng.Tracks.Count > 0 ? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))] : null);
            if (t == null || !t.Instrument || t.IsBus) { t = null; lock (eng.Sync) foreach (var q in eng.Tracks) if (q.Instrument && !q.IsBus && q.Host == null) { t = q; break; } }
            if (t == null) { if ((s & 0xF0) == 0x90 && d2 > 0 && !keysTrackAsked) { keysTrackAsked = true; try { BeginInvoke(new Action(() => { var kt = StudioEngine.NewTrack("Keys", "inst", null); kt.Instrument = true; kt.Color = unchecked((int)0xFF3B8CF5); AddTrack(kt); Toast("Your MIDI keyboard plays the new track Keys (the built-in piano). Add an instrument or a VST to it from + ADD EFFECT for other sounds."); keysTrackAsked = false; })); } catch { keysTrackAsked = false; } } return true; }
            // the picked MIDI track plays what is on it (a VST, Bass Lab, a Purphex instrument, or the built-in keys when it has none), through its effects, and MIDI REC records it
            // (before, only an instrument whose window was in front got the keyboard: pick a track with a VST and nothing played or recorded)
            InstrumentFx k = null; foreach (var f in t.Fx) { var i = f as InstrumentFx; if (i != null && i.IsInstrument && !f.Bypass) { k = i; break; } } if (k == null) k = t.Keys;
            if (k is PadsFx && !((PadsFx)k).Accepts(s, d1))
            {   // a key that is not a pad, on a Pads track: it plays the first MIDI track that is not Pads (the pads only take their pads)
                t = null; k = null; lock (eng.Sync) foreach (var q in eng.Tracks) { if (!q.Instrument || q.IsBus || q.Host != null) continue; InstrumentFx qi = null; foreach (var f in q.Fx) { var i = f as InstrumentFx; if (i != null && i.IsInstrument && !f.Bypass) { qi = i; break; } } if (qi == null) qi = q.Keys; if (qi is PadsFx) continue; t = q; k = qi; break; }
                if (k == null) return true;
            }
            k.Owner = t;
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
            if (Prefs.MidiDev.Length == 0)
            {   // every MIDI input at once (a keyboard often has two ports: its keys could be on the second one); new ones are picked up as they are plugged in
                var added = MidiInput.OpenAll();
                if (added.Count > 0) Toast("MIDI keyboard connected: " + string.Join(", ", added) + ". It plays the selected MIDI track (or your first MIDI track).");
            }
            else
            {
                var devs = MidiInput.Devices(); if (MidiInput.OpenName == Prefs.MidiDev && devs.Contains(Prefs.MidiDev)) { } else if (devs.Contains(Prefs.MidiDev)) { bool ok = MidiInput.Open(Prefs.MidiDev); string key = Prefs.MidiDev + (ok ? "|ok" : "|" + MidiInput.Status); if (key != midiToastFor) { midiToastFor = key; Toast(ok ? "MIDI keyboard connected: " + Prefs.MidiDev + "." : MidiInput.Status); } }
            }
            if (MidiInput.OpenName.Length > 0) StudioEngine.LiveUntil = Environment.TickCount + 4500;   // a keyboard plugged in keeps the sound engine awake: a hit never waits for the sound card
        }
        catch { }
    }
    void MidiDeviceMenu()
    {
        var m = Win.NewMenu(); var devs = MidiInput.Devices(); m.Items.Add(new ToolStripMenuItem("MIDI KEYBOARD: " + (MidiInput.OpenName.Length > 0 ? MidiInput.OpenName : "not connected")) { Enabled = false });
        { var all = new ToolStripMenuItem("All MIDI inputs at once (best: a keyboard often has two ports)") { Checked = Prefs.MidiDev.Length == 0 }; all.Click += (a, b) => { Prefs.MidiDev = ""; Prefs.Changed(); MidiInput.Close(); midiToastFor = ""; MidiAutoConnect(); }; m.Items.Add(all); }
        foreach (var d0 in devs) { string d = d0; var it = new ToolStripMenuItem("Only " + d) { Checked = Prefs.MidiDev == d }; it.Click += (a, b) => { Prefs.MidiDev = d; Prefs.Changed(); MidiInput.Close(); midiToastFor = ""; MidiAutoConnect(); }; m.Items.Add(it); }
        if (devs.Count == 0) m.Items.Add(new ToolStripMenuItem("No MIDI keyboard found: plug one in (it connects by itself)") { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("MIDI MAPPING... (your controller's knobs, keys and pads on any control, profiles)").Click += (a, b) => OpenMidiMap();
        m.Items.Add("MIDI Learn this button (MIDI REC)").Click += (a, b) => MidiMap.StartLearn("transport|rec", "MIDI REC");
        m.Items.Add(new ToolStripSeparator());
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
    Track recToastFor;
    void OnMidiNote(Track t, bool on, int note, int vel)
    {
        if (!recOn || t == null || !t.Instrument) return;
        if (on && recToastFor != t) { recToastFor = t; var tn = t.Name; try { BeginInvoke(new Action(() => Toast("Recording your MIDI keyboard onto " + tn + "."))); } catch { } }
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
