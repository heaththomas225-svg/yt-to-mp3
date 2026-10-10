using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Purphex Music: VST instrument tracks (for example Vital): add one, play it, record what you play, and hear it in the mix and exports.
partial class StudioForm
{
    void AddInstrumentClick()
    {
        var items = new List<FxRegistry.Info>(); foreach (var i in FxRegistry.All) if (i.Category == "Instruments") items.Add(i);
        if (items.Count == 0)
        {
            MessageBox.Show(this, "No VST instrument is set up yet.\n\nTo use one (for example Vital):\n  1. On the main window press Menu, then Plugins and extensions.\n  2. Your installed VST2 plugins are listed there (or press Open plugins folder and copy the .dll in).\n  3. Tick the instrument. It loads straight away.\n  4. Come back here and press + INSTRUMENT.\n\nOnly 64-bit VST2 plugins work (VST3 .vst3 files are not supported yet).", "Add instrument", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (items.Count == 1) { AddInstrument(items[0].Type); return; }
        var m = Win.NewMenu(); foreach (var it in items) { string ty = it.Type; m.Items.Add(it.Name).Click += (a, b) => AddInstrument(ty); }
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
    void PaintMidi(Graphics g, Track tr, RectangleF lane)
    {
        int count; lock (tr.Midi) count = tr.Midi.Count;
        if (count == 0) { Txt(g, "Instrument track: press REC, then PLAY, then play notes in the instrument window (Piano button).", 12, DimC, Rc(lane.X + 10, lane.Y + 44, lane.Width - 20, 40), StringAlignment.Near, false); return; }
        var open = new Dictionary<int, long>(); var bars = new List<float[]>(); int lo = 127, hi = 0;
        lock (tr.Midi)
            foreach (var m in tr.Midi)
            {
                int note = m.D1;
                if ((m.S & 0xF0) == 0x90 && m.D2 > 0) { open[note] = m.Frame; lo = Math.Min(lo, note); hi = Math.Max(hi, note); }
                else if ((m.S & 0xF0) == 0x80 || ((m.S & 0xF0) == 0x90 && m.D2 == 0)) { long st; if (open.TryGetValue(note, out st)) { bars.Add(new float[] { st, m.Frame, note }); open.Remove(note); } }
            }
        foreach (var kv in open) bars.Add(new float[] { kv.Value, kv.Value + StudioEngine.SR / 2, kv.Key });
        float range = Math.Max(12, hi - lo + 2); var col = Neon(tr.Color); g.SetClip(lane);
        using (var br = new SolidBrush(Color.FromArgb(220, col)))
            foreach (var b in bars)
            {
                float x0 = lane.X + (float)((b[0] - scroll) / spp), x1 = lane.X + (float)((b[1] - scroll) / spp); if (x1 < lane.X || x0 > lane.Right) continue;
                float y = lane.Bottom - 10 - (b[2] - lo + 1) / range * (lane.Height - 24);
                g.FillRectangle(br, x0, y, Math.Max(2, x1 - x0), 5);
            }
        g.ResetClip();
        Txt(g, count / 2 + " notes", 11, Color.FromArgb(200, 255, 255, 255), Rc(lane.X + 8, lane.Y + 6, 200, 16), StringAlignment.Near, true);
    }
}
