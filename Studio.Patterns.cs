using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  GENERATE PATTERN: a starting beat in a style (trap, hip-hop, lo-fi, UK drill, drum & bass, house, afrobeats, reggaeton, UK garage,
//  phonk), written as real notes onto YOUR tracks, each by its job: kick, snare or clap, hi-hats, open hat, percussion, 808 or bass.
//  A track's job comes from its sound (a sampler's sample name, the track name: "kick", "808", "hat"...) or from you (JOBS in the menu).
//  A drum kit on one track gets every drum part on its keys. It works with whatever you have: only an 808 and a kick still get their
//  parts (and it tells you which parts would make it fuller). It writes over the bars it fills, in one undo step; ghost notes then
//  show every part while you change them. The 808 / bass follows the kick, in the key picked in the piano roll (minor by default).
// =====================================================================================================
partial class StudioForm
{
    internal class PatStyle { public string Name; public double Bpm, Swing; public string Kick, Snare, Hat, Open, Perc; public int[] Bass; public bool Slide; }
    // 16 steps per bar, 2 bars: x = a hit, g = a soft (ghost) hit, r = a quick double (two 32nds), . = rest
    internal static readonly PatStyle[] PatStyles = {
        new PatStyle { Name = "Trap", Bpm = 140, Kick = "x.......x.....x.x.x.......x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.rrx.x.x.x.x.x.r.rrx.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 0, 8, 10 }, Slide = true },
        new PatStyle { Name = "Hip-Hop (boom bap)", Bpm = 90, Swing = 0.58, Kick = "x......x.x......x......x..x.....", Snare = "....x.......x.......x.......x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.xxx.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 3 } },
        new PatStyle { Name = "Lo-Fi", Bpm = 80, Swing = 0.6, Kick = "x.....x...x.....x.....x..x......", Snare = "....x.......x..g....x.......x...", Hat = "x.xgx.xgx.xgx.xgx.xgx.xgx.xgx.xg", Open = "", Perc = "......x.......x.......x.......x.", Bass = new[] { 0, 3, 5, 7 } },
        new PatStyle { Name = "UK Drill", Bpm = 142, Kick = "x......x..x.......x....x...x....", Snare = "........x..x............x.......", Hat = "x..x..x..x..x.x.x..x..x..x.rx.x.", Open = "", Perc = "...x...........x...x............", Bass = new[] { 0, 10, 8, 7 }, Slide = true },
        new PatStyle { Name = "Drum & Bass", Bpm = 174, Kick = "x.........x.....x.....x...x.....", Snare = "....x.......x.......x.......x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.xxx.x.", Open = "......x...............x.........", Perc = "", Bass = new[] { 0, 0, 10, 8 } },
        new PatStyle { Name = "House", Bpm = 124, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Afrobeats", Bpm = 104, Swing = 0.55, Kick = "x..x..x...x..x..x..x..x...x..x..", Snare = "...x..x....x..x....x..x....x..x.", Hat = "x.xxx.xxx.xxx.xxx.xxx.xxx.xxx.xx", Open = "", Perc = "..x...x...x.x.x...x...x...x.x.x.", Bass = new[] { 0, 5, 7, 5 } },
        new PatStyle { Name = "Reggaeton", Bpm = 95, Kick = "x...x...x...x...x...x...x...x...", Snare = "...x..x....x..x....x..x....x..x.", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 7, 5 } },
        new PatStyle { Name = "UK Garage", Bpm = 132, Swing = 0.58, Kick = "x.........x..x..x.........x.....", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "......x...............x.........", Perc = "...x.......x.......x.......x....", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Phonk", Bpm = 130, Kick = "x.......x.x.....x.......x.x..x..", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.xxx.x.x.x.x.x.x.xxx.x.x.", Open = "", Perc = "x..x..x...x..x..x..x..x...x..x..", Bass = new[] { 0, 0, 8, 7 } } };
    internal static readonly string[] Jobs = { "Kick", "Snare / clap", "Hi-hats", "Open hat", "Percussion", "808 / bass" };
    internal readonly Dictionary<Track, int> prJobs = new Dictionary<Track, int>();   // jobs you set yourself (they win over the guess)

    // the job of a track: what you set, else a guess from its sound and name (-1: not a drum or bass part)
    internal int JobOf(Track t)
    {
        int j; if (prJobs.TryGetValue(t, out j)) return j; if (!PrCanEdit(t)) return -1; string h = t.Name.ToLowerInvariant();
        foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null) { if (s.IsKit) return 100; foreach (var z in s.Zones) h += " " + z.Name.ToLowerInvariant() + " " + System.IO.Path.GetFileNameWithoutExtension(z.Path).ToLowerInvariant(); } }
        if (h.Contains("808") || h.Contains("bass") || h.Contains("sub")) return 5; if (h.Contains("kick") || h.Contains(" bd")) return 0; if (h.Contains("snare") || h.Contains("clap") || h.Contains("rim crack")) return 1;
        if (h.Contains("open hat") || h.Contains("openhat") || h.Contains("hat open")) return 3; if (h.Contains("hat") || h.Contains("hh")) return 2; if (h.Contains("perc") || h.Contains("shaker") || h.Contains("conga") || h.Contains("tom") || h.Contains("rim") || h.Contains("bell") || h.Contains("cowbell")) return 4;
        return -1;
    }
    static readonly int[] KitKeys = { 36, 38, 42, 46, 54 };   // a kit track: kick, snare, closed hat, open hat, percussion (General MIDI keys)

    // write the pattern; returns what it did (or why it could not)
    internal string GeneratePattern(int styleIndex, int bars, int fromBar, bool setTempo)
    {
        var st = PatStyles[styleIndex]; var parts = new Dictionary<int, List<Track>>(); var kits = new List<Track>();
        foreach (var t in eng.Tracks) { int j = JobOf(t); if (j == 100) kits.Add(t); else if (j >= 0) { List<Track> l; if (!parts.TryGetValue(j, out l)) parts[j] = l = new List<Track>(); l.Add(t); } }
        if (parts.Count == 0 && kits.Count == 0) return "Add your sounds first: put a kick, a snare or clap, hi-hats and an 808 on tracks 1, 2, 3, 4... (in SOUNDS: right-click a sound > Load into a new sampler track, or + ADD TRACK > Drum Track), then generate again. A track's job comes from its sound or name (JOBS sets it yourself).";
        PushUndo(); if (setTempo) { Fx.ProjectBpm = st.Bpm; bpmUserSet = true; }
        double beat = StudioEngine.SR * 60.0 / Fx.ProjectBpm, step = beat / 4; long a = (long)Math.Round(fromBar * 4 * beat), len = (long)Math.Round(bars * 4 * beat);
        Func<string, string> pat = j => j == "" ? "" : j;
        Action<Track, string, int, int, bool> write = (t, p, note, vel, swingOk) =>
        {
            if (string.IsNullOrEmpty(p)) return; var notes = NotesOf(t); notes.RemoveAll(n => n.Start >= a && n.Start < a + len && (JobOf(t) != 100 || n.Note == note));
            int steps = bars * 16;
            for (int s = 0; s < steps; s++)
            {
                char c = p[s % p.Length]; if (c == '.') continue; double sw = swingOk && st.Swing > 0.5 && s % 2 == 1 ? (st.Swing - 0.5) * 2 * step : 0; long at = a + (long)Math.Round(s * step + sw);
                int v = c == 'g' ? vel * 55 / 100 : (c == 'r' ? vel * 80 / 100 : vel); if (p == st.Hat && c == 'x') v = s % 2 == 0 ? vel : vel * 75 / 100;
                notes.Add(new PrNote { Start = at, Len = (long)(step * 0.9), Note = note, Vel = Math.Max(1, Math.Min(127, v)) });
                if (c == 'r') notes.Add(new PrNote { Start = at + (long)(step / 2), Len = (long)(step * 0.45), Note = note, Vel = Math.Max(1, v * 85 / 100) });
            }
            WriteNotes(t, notes);
        };
        var done = new List<string>(); string[] P = { st.Kick, st.Snare, st.Hat, st.Open, st.Perc };
        for (int j = 0; j < 5; j++) { List<Track> l; if (!parts.TryGetValue(j, out l)) continue; if (string.IsNullOrEmpty(P[j])) continue; foreach (var t in l) write(t, P[j], DrumNote(t), j == 2 ? 100 : 112, j >= 2); done.Add(Jobs[j]); }
        foreach (var k in kits) { for (int j = 0; j < 5; j++) if (!string.IsNullOrEmpty(P[j])) write(k, P[j], KitKeys[j], j == 2 ? 100 : 112, j >= 2); done.Add("the kit on " + k.Name); }
        // the 808 / bass: on the kick's rhythm, notes from the key, each held until the next
        List<Track> bassT; if (parts.TryGetValue(5, out bassT))
        {
            foreach (var t in bassT)
            {
                var notes = NotesOf(t); notes.RemoveAll(n => n.Start >= a && n.Start < a + len); var hits = new List<int>(); for (int s = 0; s < bars * 16; s++) if (st.Kick[s % st.Kick.Length] == 'x') hits.Add(s);
                int baseNote = BassRoot(t); bool minor = prScale != 2 && prScale != 7;
                for (int i = 0; i < hits.Count; i++)
                {
                    int s = hits[i], next = i + 1 < hits.Count ? hits[i + 1] : bars * 16; int barIx = s / 16; int deg = st.Bass[barIx % st.Bass.Length]; if (!minor && (deg == 3 || deg == 8 || deg == 10)) deg++;
                    int n = baseNote + deg; if (st.Slide && i % 4 == 3) n += 12;   // (trap and drill: an octave jump now and then)
                    notes.Add(new PrNote { Start = a + (long)Math.Round(s * step), Len = (long)Math.Round((next - s) * step * 0.95), Note = n, Vel = 110 });
                }
                WriteNotes(t, notes);
            }
            done.Add(Jobs[5]);
        }
        prStamp = -1; var missing = new List<string>(); if (!parts.ContainsKey(0) && kits.Count == 0) missing.Add("a kick"); if (!parts.ContainsKey(1) && kits.Count == 0) missing.Add("a snare or clap"); if (!parts.ContainsKey(2) && kits.Count == 0) missing.Add("hi-hats"); if (!parts.ContainsKey(5)) missing.Add("an 808");
        return st.Name + " pattern written for " + string.Join(", ", done) + " (bars " + (fromBar + 1) + "-" + (fromBar + bars) + (setTempo ? ", " + st.Bpm + " BPM" : "") + ")." + (missing.Count > 0 ? " Add " + string.Join(", ", missing) + " to make it fuller." : "");
    }
    // the note a drum track's hits use (a fixed-pitch sample plays the same on any key: its root, so it shows on one row)
    int DrumNote(Track t) { foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null && s.Zones.Count > 0) return s.Zones[0].Follow ? s.Zones[0].Root : (s.Zones[0].Lo == s.Zones[0].Hi ? s.Zones[0].Lo : 60); } return 60; }
    // the 808 / bass key: the sampler's root (an 808 sample tuned to its key) moved to the piano roll's key, low
    int BassRoot(Track t) { int r = 24 + prKeyRoot; foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null && s.Zones.Count > 0 && s.Zones[0].Follow) { r = s.Zones[0].Root - ((s.Zones[0].Root - 24) % 12) + prKeyRoot; while (r > 40) r -= 12; } } return r; }
    internal static void WriteNotes(Track t, List<PrNote> notes)
    {
        NoOverlap(notes); var ev = new List<MidiEv>(); foreach (var n in notes) { int note = Math.Max(0, Math.Min(127, n.Note)); ev.Add(new MidiEv { Frame = Math.Max(0, n.Start), S = 0x90, D1 = (byte)note, D2 = (byte)Math.Max(1, Math.Min(127, n.Vel)) }); ev.Add(new MidiEv { Frame = Math.Max(0, n.Start) + Math.Max(1, n.Len), S = 0x80, D1 = (byte)note, D2 = 0 }); }
        ev.Sort((x, y) => { int c = x.Frame.CompareTo(y.Frame); return c != 0 ? c : x.S.CompareTo(y.S); }); lock (t.Midi) { t.Midi.Clear(); t.Midi.AddRange(ev); }
    }
    // the GENERATE menu: styles, how many bars, the song's tempo, and each track's job
    void PrGenerateMenu()
    {
        var m = Win.NewMenu(); int from = (int)Math.Floor(prScrollBeats / 4);
        m.Items.Add(new ToolStripMenuItem("A STARTING BEAT ON YOUR TRACKS (KICK, SNARE, HATS, 808...)") { Enabled = false });
        for (int i = 0; i < PatStyles.Length; i++)
        {
            int k = i; var it = new ToolStripMenuItem(PatStyles[i].Name + "   (" + PatStyles[i].Bpm + " BPM)");
            foreach (int bars0 in new[] { 4, 8, 16 }) { int bars = bars0; it.DropDownItems.Add(bars + " bars from bar " + (from + 1)).Click += (a, b) => { bool tempo = Math.Abs(Fx.ProjectBpm - PatStyles[k].Bpm) > 0.5 && AskYes("Set the song to " + PatStyles[k].Bpm + " BPM (the usual " + PatStyles[k].Name + " tempo)? No keeps " + Fx.ProjectBpm.ToString("0.##") + " BPM.", "Generate pattern"); Toast(GeneratePattern(k, bars, from, tempo)); }; }
            m.Items.Add(it);
        }
        m.Items.Add(new ToolStripSeparator()); var jobs = new ToolStripMenuItem("Jobs of your tracks");
        foreach (var t0 in eng.Tracks) { var t = t0; if (!PrCanEdit(t)) continue; int j = JobOf(t); var it = new ToolStripMenuItem(t.Name + ":  " + (j == 100 ? "drum kit" : (j >= 0 ? Jobs[j] : "not a drum or bass part")), null) { ForeColor = Neon(t.Color) };
            for (int k = 0; k < Jobs.Length; k++) { int kk = k; var ji = new ToolStripMenuItem(Jobs[k]) { Checked = j == k }; ji.Click += (a, b) => prJobs[t] = kk; it.DropDownItems.Add(ji); }
            var none = new ToolStripMenuItem("Not part of the beat") { Checked = j == -1 && prJobs.ContainsKey(t) }; none.Click += (a, b) => prJobs[t] = -1; it.DropDownItems.Add(none); jobs.DropDownItems.Add(it); }
        if (jobs.DropDownItems.Count == 0) jobs.DropDownItems.Add(new ToolStripMenuItem("No note tracks yet") { Enabled = false }); m.Items.Add(jobs);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
}
