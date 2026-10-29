using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// =====================================================================================================
//  THE BEAT LAB: a drum machine inside the piano roll (the pads button next to PIANO ROLL at the top, or BEAT LAB in the piano roll).
//  16 or 32 pads: tap one (the mouse, the keys 1234 / QWER / ASDF / ZXCV, or the pads of a MIDI keyboard: notes 36 and up, or channel 10)
//  and it sounds at once, recording or not. Drag a sound from SOUNDS, Bass Lab or a folder onto a pad to put it there; KIT fills the pads
//  from a genre's pack. Every pad with a sound is a row of steps (16 or 32): click a step to switch it on, drag along to paint, the wheel
//  on a step changes how hard it hits, and the lane under the steps sets the velocity of the picked pad. SWING pushes every second 16th,
//  HUMANISE moves the hits and their strength a little (the same way every time, so the groove keeps its feel). PLAY loops the beat on
//  its own (the song does not need to play) and REC writes your pad hits into the steps while it loops. VARIATION makes a new version of
//  the beat in its own slot (A, B, C...): the beat you made is never replaced. The beat goes onto the song like GENERATE's box: drag it
//  onto a bar (one audio clip) or ADD TO SONG (a note track per pad that you can edit). It is saved with the project.
// =====================================================================================================
partial class StudioForm
{
    internal class BlPad { public string Name = ""; public SamplerFx Sound; public int Job = 4; public bool Mute, Solo; public float VolDb; }
    internal class BlPat { public string Name = "A"; public byte[][] Vel = NewGrid(); public BlPat Copy(string name) { var c = new BlPat { Name = name }; for (int i = 0; i < 32; i++) Array.Copy(Vel[i], c.Vel[i], 32); return c; } }
    static byte[][] NewGrid() { var v = new byte[32][]; for (int i = 0; i < 32; i++) v[i] = new byte[32]; return v; }   // [pad][step]: 0 = off, else the velocity
    internal readonly BlPad[] blPads = Enumerable.Range(0, 32).Select(i => new BlPad()).ToArray();
    internal readonly List<BlPat> blPats = new List<BlPat> { new BlPat() }; internal int blCur, blSteps = 16, blPadCount = 16, blSel; internal float blSwing = 0.5f, blHuman;
    internal bool blOn, blRec; internal volatile bool blPlaying; bool blFocus, blKitTried; float blScroll; readonly int[] blFlash = new int[32];
    readonly List<string> blUndo = new List<string>(), blRedo = new List<string>(); byte blPaint; int blPaintPad = -1;
    internal BlPat BlNow { get { blCur = Math.Max(0, Math.Min(blPats.Count - 1, blCur)); return blPats[blCur]; } }
    static readonly Keys[] BlKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.Q, Keys.W, Keys.E, Keys.R, Keys.A, Keys.S, Keys.D, Keys.F, Keys.Z, Keys.X, Keys.C, Keys.V,
                                      Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.T, Keys.Y, Keys.U, Keys.I, Keys.G, Keys.H, Keys.J, Keys.K, Keys.B, Keys.N, Keys.M, Keys.Oemcomma };
    static readonly string[] BlKeyNames = { "1", "2", "3", "4", "Q", "W", "E", "R", "A", "S", "D", "F", "Z", "X", "C", "V", "5", "6", "7", "8", "T", "Y", "U", "I", "G", "H", "J", "K", "B", "N", "M", "," };
    static readonly int[] BlJobColors = { unchecked((int)0xFFFF5C7A), unchecked((int)0xFFFFB84D), unchecked((int)0xFF4DD9FF), unchecked((int)0xFF7AF0C2), unchecked((int)0xFFB98CFF), unchecked((int)0xFF9B5CFF) };
    internal bool BlShown { get { return PrShown && blOn; } }

    internal void OpenBeatLab() { blOn = true; OpenInspector(4); if (Prefs.InsH < 420) { Prefs.InsH = 420; Prefs.Changed(); } if (!blPads.Any(p => p.Sound != null) && !blKitTried) { blKitTried = true; BlLoadKit(0); } blFocus = true; Invalidate(); }

    // ---- the sound ----
    static readonly object blLock = new object(); double blPos; readonly List<int[]> blHits = new List<int[]>(); int blRingUntil;   // blHits: {pad, velocity} tapped since the last block
    readonly Track blTmp = new Track { Instrument = true }; float[] blL, blR, blSumL, blSumR; readonly Declip blGuard = new Declip(0, 0.88);   // never clipped, like every instrument
    static readonly bool blHooked = BlHook(); static bool BlHook() { StudioEngine.Extra = (l, r, n) => { var f = BlForm; if (f != null) f.BlRender(l, r, n); }; return true; }
    static StudioForm BlForm;
    void BlRender(float[] oL, float[] oR, int n)
    {
        bool playing = blPlaying; if (!playing && unchecked(Environment.TickCount - blRingUntil) > 0) { lock (blLock) if (blHits.Count == 0) return; }
        lock (blLock)
        {
            if (blL == null || blL.Length < n) { blL = new float[n]; blR = new float[n]; blSumL = new float[n]; blSumR = new float[n]; } Array.Clear(blSumL, 0, n); Array.Clear(blSumR, 0, n);
            double stepF = StudioEngine.SR * 60.0 / Math.Max(30, Fx.ProjectBpm) / 4; int steps = blSteps; double loop = stepF * steps; var pat = BlNow; bool anySolo = blPads.Any(p => p.Solo && p.Sound != null);
            var hits = blHits.ToArray(); blHits.Clear(); if (hits.Length > 0 || playing) blRingUntil = Environment.TickCount + 4000;
            for (int pi = 0; pi < 32; pi++)
            {
                var pad = blPads[pi]; var snd = pad.Sound; if (snd == null) continue; blTmp.Midi.Clear();
                foreach (var h in hits) if (h[0] == pi) blTmp.Midi.Add(new MidiEv { Frame = 0, S = 0x90, D1 = (byte)BlKeyOf(snd), D2 = (byte)h[1] });
                if (playing)
                    for (int s = 0; s < steps; s++)
                    {
                        int v = pat.Vel[pi][s]; if (v == 0) continue; double at = s * stepF + BlShift(s, pi, stepF); at = ((at % loop) + loop) % loop;
                        double rel = at - (blPos % loop); if (rel < 0) rel += loop; if (rel >= n) continue;
                        blTmp.Midi.Add(new MidiEv { Frame = (long)rel, S = 0x90, D1 = (byte)BlKeyOf(snd), D2 = (byte)BlVel(v, s, pi) });
                    }
                blTmp.Midi.Sort((a, b) => a.Frame.CompareTo(b.Frame));
                Array.Clear(blL, 0, n); Array.Clear(blR, 0, n);
                try { snd.BeginBlock(blTmp, 0, n, true); snd.Process(blL, blR, n); } catch { continue; }
                if (pad.Mute || (anySolo && !pad.Solo)) continue; float gn = (float)Math.Pow(10, pad.VolDb / 20) * 0.6f;
                for (int i = 0; i < n; i++) { blSumL[i] += blL[i] * gn; blSumR[i] += blR[i] * gn; }
            }
            blGuard.Run(blSumL, blSumR, n); for (int i = 0; i < n; i++) { oL[i] += blSumL[i]; oR[i] += blSumR[i]; }
            if (playing) blPos = (blPos + n) % loop;
        }
    }
    static int BlKeyOf(SamplerFx s) { return s.Zones.Count > 0 ? s.Zones[0].Root : 60; }
    // swing: every second 16th later; humanise: a small push or pull and a softer or harder hit, the same for a step every time
    double BlShift(int s, int pad, double stepF) { double sw = s % 2 == 1 ? (blSwing - 0.5) * 2 * stepF : 0; if (blHuman <= 0) return sw; double r = BlRand(s, pad, 1) * 2 - 1; return sw + r * blHuman * stepF * 0.18; }
    int BlVel(int v, int s, int pad) { if (blHuman <= 0) return v; double r = BlRand(s, pad, 2) * 2 - 1; return Math.Max(1, Math.Min(127, (int)Math.Round(v * (1 + r * blHuman * 0.3)))); }
    static double BlRand(int s, int pad, int salt) { unchecked { uint h = (uint)(s * 73856093 ^ pad * 19349663 ^ salt * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xFFFF) / 65535.0; } }

    // a pad is hit: heard straight away; with REC on while the beat loops, it is written into the nearest step
    internal void BlHit(int pad, int vel)
    {
        if (pad < 0 || pad >= blPadCount || blPads[pad].Sound == null) return; BlForm = this; eng.EnsureAudio(); lock (blLock) blHits.Add(new[] { pad, Math.Max(1, Math.Min(127, vel)) });
        blFlash[pad] = Environment.TickCount; blSel = pad;
        if (blRec && blPlaying) { double stepF = StudioEngine.SR * 60.0 / Math.Max(30, Fx.ProjectBpm) / 4; int s = (int)Math.Round(blPos / stepF) % blSteps; BlMark(); BlNow.Vel[pad][s] = (byte)Math.Max(1, Math.Min(127, vel)); }
    }
    internal void BlPlay(bool on) { BlForm = this; if (on) { eng.EnsureAudio(); lock (blLock) blPos = 0; } blPlaying = on; Invalidate(); }
    // the pads of a MIDI keyboard (notes 36 and up, or anything on channel 10) while the Beat Lab is open
    internal volatile bool blLive;
    internal bool BlMidi(byte s, byte d1, byte d2)
    {
        if (!blLive) return false; int kind = s & 0xF0; bool drumCh = (s & 0x0F) == 9; int pad = d1 - 36; if (pad < 0 || pad >= blPadCount) { if (!drumCh) return false; pad = ((d1 % blPadCount) + blPadCount) % blPadCount; }
        if (kind == 0x90 && d2 > 0) { try { BeginInvoke((Action)(() => BlHit(pad, d2))); } catch { } return true; }
        return kind == 0x80 || kind == 0x90 || drumCh;
    }
    internal bool BlKey(Keys k)
    {
        if (!BlShown || !blFocus || (k & (Keys.Control | Keys.Alt)) != 0) return false; int i = Array.IndexOf(BlKeys, k & Keys.KeyCode); if (i < 0 || i >= blPadCount) return false;
        BlHit(i, (k & Keys.Shift) != 0 ? 127 : 105); return true;
    }

    // ---- sounds onto pads ----
    internal int BlPadAt(PointF p) { if (!BlShown) return -1; for (int i = 0; i < blPadCount && i < blPadRects.Length; i++) if (blPadRects[i].Contains(p)) return i; return -1; }
    RectangleF[] blPadRects = new RectangleF[0];
    internal bool BlDrop(SoundItem si, Point p) { int i = BlPadAt(p); if (i < 0 || si == null) return false; if (!si.IsAudio) { Toast("Drop a sound (a WAV or MP3) on a pad."); return true; } BlSetPad(i, si); return true; }
    internal void BlSetPad(int i, SoundItem si)
    {
        if (si.Instant) { string e = InstantLib.Ensure(si); if (e != null) { Toast(e); return; } }
        bool follow = si.Type == "808" || si.Group == "Bass"; int root = 60; int rt; bool mi; if (follow && si.Key.Length > 0 && SoundLibrary.ParseKey(si.Key, out rt, out mi)) root = 36 + rt;
        var smp = new SamplerFx(); smp.Prepare(StudioEngine.SR); string err = smp.SetSingle(si.Path, si.Name.Replace(si.PackName + " - ", ""), follow, root); if (err != null) { Toast(err); return; }
        BlMark(); var pad = blPads[i]; pad.Sound = smp; pad.Name = si.Name.Replace(si.PackName + " - ", ""); pad.Job = BlGuessJob(pad.Name + " " + si.Type + " " + si.Group); blSel = i; BlHit(i, 105);
        Toast(pad.Name + " is on pad " + (i + 1) + ": tap it, or switch on its steps.");
    }
    // YOUR OWN TRACKS ON THE PADS: a song track's audio (its clips through its plugins, silence at the ends cut off) or one hit of its instrument
    internal int BlFreePad() { for (int i = 0; i < blPadCount; i++) if (blPads[i].Sound == null) return i; return blSel; }
    void BlTrackMenu(int pad) { var m = Win.NewMenu(); BlTrackItems(m.Items, pad); m.Show(this, Point.Round(MousePos)); }
    void BlTrackItems(ToolStripItemCollection items, int pad)
    {
        var rows = eng.Tracks.Where(t => t.Host == null && !t.IsBus && !t.Off && BlTrackHasSound(t)).ToList();
        if (rows.Count == 0) { items.Add(new ToolStripMenuItem("This song has no tracks with sound yet") { Enabled = false }); return; }
        items.Add(new ToolStripMenuItem("Onto pad " + (pad + 1)) { Enabled = false });
        foreach (var t in rows)
        {
            var tt = t;
            if (t.Instrument)
            {
                var it = new ToolStripMenuItem(t.Name); it.DropDownItems.Add(new ToolStripMenuItem("One hit of its instrument (plays in tune with the pad's notes)", null, (a, b) => Toast(BlPadFromTrack(pad, tt, true))));
                it.DropDownItems.Add(new ToolStripMenuItem("Its whole part as audio", null, (a, b) => Toast(BlPadFromTrack(pad, tt, false)))); items.Add(it);
            }
            else items.Add(new ToolStripMenuItem(t.Name, null, (a, b) => Toast(BlPadFromTrack(pad, tt, false))));
        }
    }
    bool BlTrackHasSound(Track t) { foreach (var p in Exporter.RowSet(eng, t)) { if (p.Clip != null && p.UsedLength > 0) return true; if (p.Instrument) lock (p.Midi) if (p.Midi.Count > 0 || p.Fx.Any(f => f is InstrumentFx)) return true; } return false; }
    internal string BlPadFromTrack(int pad, Track t, bool oneHit)
    {
        if (pad < 0 || pad >= 32) return "No pad to put it on."; int SR = StudioEngine.SR; float[] l, r;
        if (oneHit)
        {
            // a copy of the track playing one middle C for half a second, its tail let ring
            var c = CloneTrack(t, ""); c.Offset = 0; c.Mute = false; c.Out = null; lock (c.Midi) { c.Midi.Clear(); c.Midi.Add(new MidiEv { Frame = 0, S = 0x90, D1 = 60, D2 = 110 }); c.Midi.Add(new MidiEv { Frame = SR / 2, S = 0x80, D1 = 60, D2 = 0 }); } c.MClips.Clear();
            eng.Render(0, SR * 3, new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = new List<Track> { c } }, null, out l, out r);
        }
        else
        {
            var only = Exporter.RowSet(eng, t); long a = long.MaxValue, e = 0;
            foreach (var p in only) { if (p.Clip != null && p.UsedLength > 0) { a = Math.Min(a, p.Offset); e = Math.Max(e, p.Offset + p.UsedLength); } if (p.Instrument) lock (p.Midi) foreach (var m in p.Midi) { a = Math.Min(a, m.Frame); e = Math.Max(e, m.Frame + SR); } }
            if (a == long.MaxValue || e <= a) return t.Name + " has no sound to put on a pad yet.";
            e = Math.Min(e, a + SR * 30L);   // a pad holds up to 30 s
            eng.Render(a, e - a + SR / 2, new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = only }, null, out l, out r);
        }
        int s0 = 0, s1 = l.Length; const float gate = 0.001f;   // -60 dB
        while (s0 < s1 && Math.Abs(l[s0]) < gate && Math.Abs(r[s0]) < gate) s0++; while (s1 > s0 && Math.Abs(l[s1 - 1]) < gate && Math.Abs(r[s1 - 1]) < gate) s1--;
        if (s1 - s0 < SR / 100) return t.Name + " is silent there: nothing to put on the pad.";
        s0 = Math.Max(0, s0 - 32); s1 = Math.Min(l.Length, s1 + SR / 50); var L = new float[s1 - s0]; var R = new float[s1 - s0]; Array.Copy(l, s0, L, 0, L.Length); Array.Copy(r, s0, R, 0, R.Length);
        for (int i = 0; i < Math.Min(32, L.Length); i++) { float k = i / 32f; L[i] *= k; R[i] *= k; } for (int i = 0; i < Math.Min(SR / 50, L.Length); i++) { float k = i / (SR / 50f); L[L.Length - 1 - i] *= k; R[R.Length - 1 - i] *= k; }   // no click at either end
        string nm = t.Name + (oneHit ? " hit" : ""); string path = Path.Combine(PatternAudioDir(), "Pad " + string.Join("_", nm.Split(Path.GetInvalidFileNameChars())) + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".wav"); WavIO.Write(path, L, R, SR, 24);
        var smp = new SamplerFx(); smp.Prepare(SR); string err = smp.SetSingle(path, nm, oneHit, 60); if (err != null) return err;
        BlMark(); var pd = blPads[pad]; pd.Sound = smp; pd.Name = nm; pd.Job = BlGuessJob(t.Name + " " + t.Role); blSel = pad; if (pad >= blPadCount) blPadCount = 32; BlHit(pad, 105);
        return nm + " is on pad " + (pad + 1) + ": tap it, or switch on its steps.";
    }
    static int BlGuessJob(string h) { h = " " + h.ToLowerInvariant() + " "; if (h.Contains("kick")) return 0; if (h.Contains("snare") || h.Contains("clap") || h.Contains("rim")) return 1; if (h.Contains("open")) return 3; if (h.Contains("hat") || h.Contains("ride") || h.Contains("cymbal")) return 2; if (h.Contains("808") || h.Contains("bass")) return 5; return 4; }
    // KIT: the pads from a genre's pack (kicks, snares, claps, hats, open hat, percussion, ride, 808, an FX and a vocal chop)
    internal void BlLoadKit(int styleIndex)
    {
        var rec = SoundCatalog.Find(PackOfStyle(styleIndex)); if (rec == null) return; var items = InstantLib.ItemsOf(rec.Id).ToList();
        string[] plan = { "ok|0", "os|0", "oc|0", "oh|0", "oo|0", "op|0", "o8|0", "ok|1", "os|1", "oc|1", "oh|1", "op|1", "op|2", "oy|0", "of|0", "ov|0" };
        BlMark(); int put = 0;
        for (int i = 0; i < plan.Length && i < blPadCount; i++)
        {
            string code = plan[i].Substring(0, 3); int nth = plan[i][3] - '0'; var it = items.Where(x => x.Spec.StartsWith(code)).Skip(nth).FirstOrDefault(); if (it == null) { blPads[i].Sound = null; blPads[i].Name = ""; continue; }
            if (InstantLib.Ensure(it) != null) continue; var smp = new SamplerFx(); smp.Prepare(StudioEngine.SR); bool follow = code == "o8|"; int root = 60; int rt; bool mi; if (follow && SoundLibrary.ParseKey(it.Key, out rt, out mi)) root = 36 + rt;
            if (smp.SetSingle(it.Path, it.Name.Replace(it.PackName + " - ", ""), follow, root) != null) continue; blPads[i].Sound = smp; blPads[i].Name = it.Name.Replace(it.PackName + " - ", ""); blPads[i].Job = code == "ok|" ? 0 : code == "os|" || code == "oc|" ? 1 : code == "oh|" || code == "oy|" ? 2 : code == "oo|" ? 3 : code == "o8|" ? 5 : 4; put++;
        }
        if (put > 0) Toast(PatStyles[Math.Max(0, Math.Min(PatStyles.Length - 1, styleIndex))].Name + " kit on the pads (" + rec.Name + "): tap them, or drag your own sounds on.");
    }
    // FILL: a genre's beat written into the steps of the pads with those jobs
    internal void BlFill(int styleIndex)
    {
        var st = PatStyles[styleIndex]; string[] P = { st.Kick, st.Snare, st.Hat, st.Open, st.Perc }; BlMark(); var pat = BlNow; var used = new HashSet<int>();
        for (int i = 0; i < 32; i++) Array.Clear(pat.Vel[i], 0, 32);
        for (int j = 0; j < 6; j++)
        {
            int pi = -1; for (int i = 0; i < blPadCount; i++) if (blPads[i].Sound != null && blPads[i].Job == j && !used.Contains(i)) { pi = i; break; } if (pi < 0) continue; used.Add(pi);
            string p = j == 5 ? st.Kick : P[j]; if (string.IsNullOrEmpty(p)) continue;
            for (int s = 0; s < blSteps; s++) { char c = p[s % p.Length]; if (c == '.' || (j == 5 && c != 'x')) continue; if (j == 5 && s % 4 != 0 && s % 3 != 0) continue; pat.Vel[pi][s] = (byte)(c == 'g' ? 60 : c == 'r' ? 90 : (j == 2 && s % 2 == 1 ? 80 : 108)); }
        }
        if (Math.Abs(blSwing - 0.5) < 0.01 && st.Swing > 0.5) blSwing = (float)st.Swing; Toast(st.Name + " beat in slot " + pat.Name + ". VARIATION makes new versions of it without touching this one.");
    }
    // VARIATION: a new slot with a version of the beat (hats rolled or thinned, a kick moved, ghost snares, a fill at the end); the beat it came from stays
    internal void BlVariation()
    {
        var src = BlNow; string nm = ((char)('A' + blPats.Count)).ToString(); if (blPats.Count >= 26) { Toast("26 versions is the most: delete one first."); return; }
        var v = src.Copy(nm); int seed = blPats.Count * 7919 + Environment.TickCount % 997; var rnd = new Random(seed); int S = blSteps;
        for (int pi = 0; pi < blPadCount; pi++)
        {
            var pad = blPads[pi]; if (pad.Sound == null) continue; var row = v.Vel[pi]; int hits = 0; for (int s = 0; s < S; s++) if (row[s] > 0) hits++;
            if (pad.Job == 2) { for (int s = 0; s < S; s++) { if (row[s] > 0 && rnd.NextDouble() < 0.18 && s + 1 < S) row[s + 1] = (byte)(row[s] * 3 / 4); else if (row[s] > 0 && s % 2 == 1 && rnd.NextDouble() < 0.2) row[s] = 0; } }
            else if (pad.Job == 0) { var on = Enumerable.Range(0, S).Where(s => row[s] > 0 && s % 8 != 0).ToList(); if (on.Count > 0) { int a = on[rnd.Next(on.Count)]; int b = Math.Max(0, Math.Min(S - 1, a + (rnd.Next(2) == 0 ? -1 : 2))); if (row[b] == 0) { row[b] = row[a]; row[a] = 0; } } if (rnd.NextDouble() < 0.5) { int s = S - 3; if (row[s] == 0) row[s] = 96; } }
            else if (pad.Job == 1) { for (int s = 0; s < S; s++) if (row[s] == 0 && s % 4 == 3 && rnd.NextDouble() < 0.22) row[s] = 48; if (rnd.NextDouble() < 0.4) for (int s = S - 4; s < S; s++) if (row[s] == 0 && rnd.NextDouble() < 0.6) row[s] = (byte)(60 + (s - S + 4) * 15); }
            else if (hits > 0) { for (int s = 0; s < S; s++) if (rnd.NextDouble() < 0.12) row[s] = row[s] > 0 ? (byte)0 : (byte)(70 + rnd.Next(30)); }
        }
        BlMark(); blPats.Add(v); blCur = blPats.Count - 1; Toast("Version " + nm + " made from " + src.Name + " (" + src.Name + " is still there: click its letter to go back).");
    }

    // ---- undo (the Beat Lab's own) ----
    void BlMark() { blUndo.Add(BlSave()); if (blUndo.Count > 60) blUndo.RemoveAt(0); blRedo.Clear(); }
    void BlUndoStep(bool back) { var from = back ? blUndo : blRedo; var to = back ? blRedo : blUndo; if (from.Count == 0) return; to.Add(BlSave()); string s = from[from.Count - 1]; from.RemoveAt(from.Count - 1); BlLoad(s, false); }

    // ---- saved with the project ----
    internal string BlSave()
    {
        var sb = new StringBuilder(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        sb.Append("v1|").Append(blSteps).Append('|').Append(blPadCount).Append('|').Append(blSwing.ToString("R", ci)).Append('|').Append(blHuman.ToString("R", ci)).Append('|').Append(blCur).Append('\n');
        for (int i = 0; i < 32; i++) { var p = blPads[i]; if (p.Sound == null) continue; sb.Append("pad|").Append(i).Append('|').Append(B64(p.Name)).Append('|').Append(p.Job).Append('|').Append(p.VolDb.ToString("R", ci)).Append('|').Append(p.Mute ? 1 : 0).Append('|').Append(p.Solo ? 1 : 0).Append('|').Append(B64(p.Sound.Serialize())).Append('\n'); }
        foreach (var pt in blPats) { sb.Append("pat|").Append(B64(pt.Name)); for (int i = 0; i < 32; i++) { sb.Append('|'); foreach (var b in pt.Vel[i]) sb.Append(b.ToString("x2")); } sb.Append('\n'); }
        return sb.ToString();
    }
    static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); }
    static string Un64(string s) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return ""; } }
    internal void BlLoad(string text, bool fresh)
    {
        lock (blLock)
        {
            foreach (var p in blPads) { p.Sound = null; p.Name = ""; p.Job = 4; p.VolDb = 0; p.Mute = p.Solo = false; } blPats.Clear(); blCur = 0; blSteps = 16; blPadCount = 16; blSwing = 0.5f; blHuman = 0;
            if (fresh) { blUndo.Clear(); blRedo.Clear(); blPlaying = false; blKitTried = false; }
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in (text ?? "").Split('\n'))
            {
                var f = line.Split('|'); int iv; float fv;
                try
                {
                    if (f[0] == "v1" && f.Length >= 6) { if (int.TryParse(f[1], out iv)) blSteps = iv == 32 ? 32 : 16; if (int.TryParse(f[2], out iv)) blPadCount = iv == 32 ? 32 : 16; if (float.TryParse(f[3], System.Globalization.NumberStyles.Float, ci, out fv)) blSwing = Math.Max(0.5f, Math.Min(0.75f, fv)); if (float.TryParse(f[4], System.Globalization.NumberStyles.Float, ci, out fv)) blHuman = Math.Max(0, Math.Min(1, fv)); int.TryParse(f[5], out blCur); }
                    else if (f[0] == "pad" && f.Length >= 8 && int.TryParse(f[1], out iv) && iv >= 0 && iv < 32)
                    {
                        var p = blPads[iv]; p.Name = Un64(f[2]); int.TryParse(f[3], out p.Job); float.TryParse(f[4], System.Globalization.NumberStyles.Float, ci, out p.VolDb); p.Mute = f[5] == "1"; p.Solo = f[6] == "1";
                        var smp = new SamplerFx(); smp.Prepare(StudioEngine.SR); smp.Deserialize(Un64(f[7])); p.Sound = smp;
                    }
                    else if (f[0] == "pat" && f.Length >= 34)
                    {
                        var pt = new BlPat { Name = Un64(f[1]) }; for (int i = 0; i < 32; i++) { string h = f[2 + i]; for (int s = 0; s < 32 && s * 2 + 1 < h.Length; s++) pt.Vel[i][s] = Convert.ToByte(h.Substring(s * 2, 2), 16); } blPats.Add(pt);
                    }
                }
                catch { }
            }
            if (blPats.Count == 0) blPats.Add(new BlPat()); blCur = Math.Max(0, Math.Min(blPats.Count - 1, blCur));
        }
        Invalidate();
    }

    // ---- onto the song: the beat as GENERATE's box (one part per pad with steps on) ----
    internal PatBox BlToBox()
    {
        var pat = BlNow; double stepF = StudioEngine.SR * 60.0 / Fx.ProjectBpm / 4; int bars = Math.Max(1, blSteps / 16); var box = new PatBox { Style = "Beat Lab " + pat.Name, StyleIndex = 0, Bars = bars, Bpm = Fx.ProjectBpm };
        bool anySolo = blPads.Any(p => p.Solo && p.Sound != null);
        for (int pi = 0; pi < blPadCount; pi++)
        {
            var pad = blPads[pi]; if (pad.Sound == null || pad.Mute || (anySolo && !pad.Solo)) continue; var part = new PatPart { Job = pad.Job, Name = pad.Name.Length > 0 ? pad.Name : "Pad " + (pi + 1) };
            for (int s = 0; s < blSteps; s++) { int v = pat.Vel[pi][s]; if (v == 0) continue; long at = (long)Math.Round(s * stepF + BlShift(s, pi, stepF)); part.Notes.Add(new PrNote { Start = Math.Max(0, at), Len = (long)(stepF * 0.9), Note = BlKeyOf(pad.Sound), Vel = BlVel(v, s, pi) }); }
            if (part.Notes.Count == 0) continue; var c = new SamplerFx(); c.Prepare(StudioEngine.SR); c.Deserialize(pad.Sound.Serialize()); part.Sound = c; box.Parts.Add(part);
        }
        return box;
    }
    string BlAddToSong()
    {
        var box = BlToBox(); if (box.Parts.Count == 0) return "Switch on some steps first (or FILL a genre's beat), then add it to the song.";
        long at = (long)Math.Round(Math.Floor(DispPos / (PrBeatFrames * 4)) * PrBeatFrames * 4); return DropPatternBox(box, at);
    }

    // ---- drawing ----
    void PaintBeatLab(Graphics g, RectangleF C)
    {
        BlForm = this; blLive = true; float x = C.X, y = C.Y, w = C.Width, h = C.Height; int now = Environment.TickCount; var pat = BlNow;
        { var gs = g.Save(); g.SetClip(C); PaintGalaxy(g, C, 0xBEA7, 0.55f); g.Restore(gs); }
        regs.Add(new HitReg { R = C, OnDown = () => blFocus = true, Overlay = false });
        // the bar
        float bx = x; Func<float, string, int, Action, string, RectangleF> chip = (cw, label, style, act, hint) => { var r = Rc(bx, y, cw, 26); Btn(g, r, label, style, act, 9.5f); Hint2(r, hint); bx += cw + 5; return r; };
        chip(110, "< PIANO ROLL", 0, () => { blOn = false; BlPlay(false); }, "Back to the piano roll (the beat keeps everything you made)");
        using (var f = new Font("Segoe UI Black", 19f, FontStyle.Italic, GraphicsUnit.Pixel)) GalaxyText(g, "BEAT LAB", f, bx + 4, y); bx += 116;
        chip(70, blPlaying ? "STOP" : "PLAY", blPlaying ? 3 : 4, () => BlPlay(!blPlaying), "Loop the beat on its own (the song does not play)");
        chip(54, "REC", blRec ? 3 : 0, () => { blRec = !blRec; if (blRec && !blPlaying) BlPlay(true); }, "While the beat loops, the pads you hit are written into the steps");
        chip(86, blSteps + " STEPS", 0, () => { BlMark(); blSteps = blSteps == 16 ? 32 : 16; if (blSteps == 32) foreach (var p2 in blPats) for (int i = 0; i < 32; i++) if (Enumerable.Range(16, 16).All(s => p2.Vel[i][s] == 0)) Array.Copy(p2.Vel[i], 0, p2.Vel[i], 16, 16); }, "16 steps (one bar) or 32 (two bars: the first bar is copied into the second to start from)");
        chip(80, blPadCount + " ROWS", 0, () => { blPadCount = blPadCount == 16 ? 32 : 16; blSel = Math.Min(blSel, blPadCount - 1); }, "Up to 16 or 32 sounds (rows)");
        chip(70, "KIT  v", 0, () => BlStyleMenu("Put a genre's drum kit on the pads", BlLoadKit), "Fill the pads with a genre's kit from the sound library (kicks, snares, claps, hats, percussion, 808...)");
        chip(92, "+ TRACK  v", 2, () => BlTrackMenu(BlFreePad()), "Put one of this song's tracks on a pad: its audio (with its plugins), or one hit of its instrument");
        chip(70, "FILL  v", 0, () => BlStyleMenu("Write a genre's beat into the steps", BlFill), "Write a genre's beat into this slot's steps, on the pads with those jobs");
        chip(96, "VARIATION", 2, () => BlVariation(), "A new version of this beat in its own slot: hats rolled or thinned, the kick moved, ghost snares, a fill. The beat you made stays as it is");
        chip(62, "CLEAR", 0, () => { BlMark(); for (int i = 0; i < 32; i++) Array.Clear(pat.Vel[i], 0, 32); }, "Switch off every step in this slot");
        chip(30, "<", 0, () => BlUndoStep(true), "Undo in the Beat Lab"); chip(30, ">", 0, () => BlUndoStep(false), "Redo in the Beat Lab");
        // swing and humanise
        float sx = bx + 6, sw = Math.Max(90, Math.Min(150, (x + w - 240 - sx) / 2 - 10));
        // galaxy knobs: drag up / down, double-click for the default (the wheel scrolls)
        var ss = KnobCtl(g, sx + 12, y + 12, 20, (blSwing - 0.5f) / 0.25f, v => blSwing = 0.5f + Math.Max(0, Math.Min(1, v)) * 0.25f, NeonCyan, () => blSwing = 0.5f); ss.OnWheel = null; Txt(g, "SWING " + (int)Math.Round(blSwing * 100) + "%", 9, DimC, Rc(sx + 28, y + 5, sw - 28, 14), StringAlignment.Near, true);
        Hint2(ss.R, "Swing " + (int)Math.Round(blSwing * 100) + "%: pushes every second 16th later for a looser groove (50% = straight, double-click)");
        sx += sw + 14; var hs = KnobCtl(g, sx + 12, y + 12, 20, blHuman, v => blHuman = Math.Max(0, Math.Min(1, v)), NeonPink, () => blHuman = 0); hs.OnWheel = null; Txt(g, "HUMANISE " + (int)Math.Round(blHuman * 100) + "%", 9, DimC, Rc(sx + 28, y + 5, sw - 28, 14), StringAlignment.Near, true);
        Hint2(hs.R, "Humanise " + (int)Math.Round(blHuman * 100) + "%: moves every hit a little in time and strength, like a drummer (0% = exact, double-click)");
        // onto the song
        var add = Rc(x + w - 226, y, 110, 26); Btn(g, add, "ADD TO SONG", 2, () => Toast(BlAddToSong()), 9.5f); Hint2(add, "Put this beat into the song at the bar of the needle: a note track per pad, editable in the piano roll");
        var drg = Rc(x + w - 110, y, 110, 26); var dr = Btn(g, drg, "DRAG TO SONG", 4, () => { var b = BlToBox(); if (b.Parts.Count == 0) { Toast("Switch on some steps first."); return; } prBox = b; prBoxDrag = true; prBoxAt = MousePos; }, 9.5f); Hint2(drg, "Hold and drag the beat onto a bar of the song (it goes in as one audio clip)");
        { dr.OnDrag = pt => { if (prBox != null && prBoxDrag) { prBoxAt = pt; Invalidate(); } }; dr.OnUp = () => PrBoxDropped(); }
        if (prBoxDrag) PaintPatternBox(g, C);
        y += 34;
        // the slots: A, B, C... (VARIATION adds one)
        { float vx = x; Txt(g, "SLOTS", 9, DimC, Rc(vx, y + 4, 44, 16), StringAlignment.Near, true); vx += 46;
          for (int i = 0; i < blPats.Count; i++) { int k = i; var r = Rc(vx, y, 30, 22); var hr = Btn(g, r, blPats[i].Name, i == blCur ? 4 : 0, () => blCur = k, 10); Hint2(r, "Slot " + blPats[i].Name + ": click to hear and edit it"); hr.OnRight = () => { if (blPats.Count > 1) { BlMark(); blPats.RemoveAt(k); blCur = Math.Max(0, Math.Min(blCur, blPats.Count - 1)); Toast("Slot removed (the Beat Lab's undo brings it back)."); } }; vx += 34; }
          var cp = Rc(vx + 4, y, 70, 22); Btn(g, cp, "+ COPY", 0, () => { if (blPats.Count >= 26) return; BlMark(); blPats.Add(BlNow.Copy(((char)('A' + blPats.Count)).ToString())); blCur = blPats.Count - 1; }, 9); Hint2(cp, "A copy of this slot to change by hand (right-click a slot's letter to delete it)");
          Txt(g, "Tap a row's name or press its key. Drag a sound from SOUNDS or a folder onto a row (or below them for a new one). Click steps to switch them on, drag to paint, the wheel sets how hard.", 9, DimC, Rc(cp.Right + 12, y + 3, x + w - cp.Right - 12, 16), StringAlignment.Near, false); }
        y += 30;
        // no pad grid: the rows are the pads (tap a row's name, press its key, drop a sound on a row); their spots are kept for dropping
        if (blPadRects.Length != 32) blPadRects = new RectangleF[32];
        for (int i = 0; i < 32; i++) blPadRects[i] = RectangleF.Empty;
        // the steps: one row per pad with a sound
        float gx = x, gw = w; var list = Enumerable.Range(0, blPadCount).Where(i => blPads[i].Sound != null).ToList();
        float velH = 54, rowH = 26, nameW = Math.Min(190, gw * 0.26f), top = y, bottom = C.Bottom - velH - 8; var area = Rc(gx, top, gw, bottom - top);
        float total = (list.Count + 1) * rowH; blScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), blScroll)); regs.Add(new HitReg { R = area, OnWheel = d => blScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), blScroll - d * 40)) });
        float cellX = gx + nameW + 112, cellW = (gx + gw - cellX - 4) / blSteps; double stepF = StudioEngine.SR * 60.0 / Math.Max(30, Fx.ProjectBpm) / 4; int playStep = blPlaying ? (int)(blPos / stepF) % blSteps : -1;
        if (list.Count == 0) Txt(g, "Press KIT, + TRACK, or drag a sound here: each sound gets its own row of steps.", 11, DimC, area, StringAlignment.Center, false);
        var gsS = g.Save(); g.SetClip(area);
        for (int li = 0; li < list.Count; li++)
        {
            int pi = list[li]; var pad = blPads[pi]; float ry = top + li * rowH - blScroll; if (ry + rowH < top || ry > bottom) continue; blPadRects[pi] = RectangleF.Intersect(Rc(gx, ry, gw, rowH), area); Color pc = Neon(BlJobColors[Math.Max(0, Math.Min(5, pad.Job))]);
            var mb = Rc(gx, ry + 2, 24, rowH - 4); Btn(g, mb, "M", pad.Mute ? 3 : 0, () => pad.Mute = !pad.Mute, 9); Hint2(mb, "Mute this pad");
            var sb2 = Rc(gx + 27, ry + 2, 24, rowH - 4); Btn(g, sb2, "S", pad.Solo ? 4 : 0, () => pad.Solo = !pad.Solo, 9); Hint2(sb2, "Solo this pad");
            var nb = Rc(gx + 54, ry + 2, nameW - 76, rowH - 4); int kq = pi; var nh = Btn(g, nb, pad.Name, pi == blSel ? 4 : 1, () => { blSel = kq; BlHit(kq, 105); }, 9); nh.OnRight = () => BlPadMenu(kq); Hint2(nb, pad.Name + ": tap to hear it and pick it (its velocity lane shows below). Key " + BlKeyNames[pi] + ". Right-click: change its sound");
            { float fl = Math.Max(0, 1 - unchecked(now - blFlash[pi]) / 260f); if (fl > 0) { Glow(g, pc, nb, 6, 0.5f + fl, 3); Invalidate(); } }   // a row lights up when it plays
            var ch = Rc(nb.Right + 3, ry + 2, 73, rowH - 4); Btn(g, ch, "TRACK ▼", 2, () => BlPadMenu(kq), 9); Hint2(ch, "Choose what this row plays: any track from your song, a sound file, or rename it");
            var vk = KnobCtl(g, gx + 58 + nameW + 24, ry + rowH / 2, Math.Min(16, rowH - 8), (pad.VolDb + 24) / 30f, v => pad.VolDb = Math.Max(-24, Math.Min(6, v * 30 - 24)), pc, () => pad.VolDb = 0); vk.OnWheel = null; Hint2(vk.R, pad.Name + " volume " + pad.VolDb.ToString("0.0") + " dB: drag up or down, double-click for 0 dB");
            for (int s = 0; s < blSteps; s++)
            {
                var cr = Rc(cellX + s * cellW + 1, ry + 3, cellW - 2, rowH - 6); int v = pat.Vel[pi][s]; bool beat = s % 4 == 0;
                bool alt = (s / 4) % 2 == 1; using (var b = new SolidBrush(v > 0 ? Color.FromArgb(90 + v, pc) : (alt ? TM.C(215, 52, 36, 96) : TM.C(215, 70, 48, 122)))) g.FillRectangle(b, cr);   // the empty steps in groups of four, easy to see
                using (var p = new Pen(v > 0 ? Color.FromArgb(230, Color.White) : Color.FromArgb(beat ? 150 : 90, NeonViolet), v > 0 ? 1.2f : 1f)) g.DrawRectangle(p, cr.X, cr.Y, cr.Width - 1, cr.Height - 1);
                if (s == playStep) using (var p = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f)) g.DrawRectangle(p, cr.X, cr.Y, cr.Width, cr.Height);
            }
        }
        // under the rows: a new row (drop a sound on it, or click it to put one of your tracks there)
        { int fp = -1; for (int i = 0; i < blPadCount; i++) if (blPads[i].Sound == null) { fp = i; break; }
          float ny = top + list.Count * rowH - blScroll; if (fp >= 0 && list.Count > 0 && ny + rowH > top && ny < bottom)
          { var nr = Rc(gx + 54, ny + 2, gw - 58, rowH - 4); blPadRects[fp] = RectangleF.Intersect(Rc(gx, ny, gw, rowH), area); bool hn = Hot(nr);
            using (var pen = new Pen(Color.FromArgb(hn ? 230 : 130, NeonCyan), 1.2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) g.DrawRectangle(pen, nr.X, nr.Y, nr.Width, nr.Height);
            Txt(g, "+  NEW ROW: drop a sound here, or click to use one of your tracks", 9, hn ? Ink : DimC, nr, StringAlignment.Center, true); int fq = fp; Reg(nr, () => BlTrackMenu(fq)); Hint2(nr, "A new row of steps: drag a sound from SOUNDS or a folder here, or click to put one of your song's tracks on it"); } }
        // dragging a sound: every row shows it can take it, the one under the mouse lights up (that is where it lands)
        if ((sndDragging && sndDrag != null) || dragOver)
        {
            int over = BlPadAt(Point.Round(MousePos));
            for (int i = 0; i < blPadCount; i++) { var r = blPadRects[i]; if (r.Width <= 0) continue; bool on = i == over;
                using (var b = new SolidBrush(Color.FromArgb(on ? 70 : 18, NeonCyan))) g.FillRectangle(b, r); using (var pen = new Pen(Color.FromArgb(on ? 255 : 90, NeonCyan), on ? 2.2f : 1f)) g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3);
                if (on) { Glow(g, NeonCyan, r, 4, 0.9f, 3); string dl = blPads[i].Sound == null ? "DROP: A NEW ROW" : "DROP: REPLACES " + blPads[i].Name.ToUpperInvariant(); float dw = Math.Min(r.Width - 8, g.MeasureString(dl, GfxCache.Font(10, true)).Width + 22); var lr = Rc(r.Right - dw - 6, r.Y + 2, dw, r.Height - 4); FillRR(g, TM.C(235, 10, 30, 52), lr, 6); StrokeRR(g, NeonCyan, lr, 6, 1.2f); Txt(g, dl, 10, Color.White, lr, StringAlignment.Center, true); } }
            Invalidate();
        }
        g.Restore(gsS);
        // one region for every step: click to switch, drag to paint, the wheel changes how hard
        Func<PointF, int[]> cellAt = p => { if (p.X < cellX || p.Y < top || p.Y >= bottom) return null; int s = (int)((p.X - cellX) / cellW); int li = (int)((p.Y - top + blScroll) / rowH); if (s < 0 || s >= blSteps || li < 0 || li >= list.Count) return null; return new[] { list[li], s }; };
        var stepsR = Rc(cellX, top, gx + gw - cellX, bottom - top);
        var sr = Reg(stepsR, () => { blFocus = true; var c = cellAt(MousePos); if (c == null) return; BlMark(); blPaintPad = c[0]; blPaint = pat.Vel[c[0]][c[1]] > 0 ? (byte)0 : (byte)100; pat.Vel[c[0]][c[1]] = blPaint; blSel = c[0]; if (blPaint > 0 && !blPlaying) BlHit(c[0], 100); });
        sr.OnDrag = p => { var c = cellAt(p); if (c != null && c[0] == blPaintPad) pat.Vel[c[0]][c[1]] = blPaint; };
        sr.OnWheel = d => { var c = cellAt(MousePos); if (c == null || pat.Vel[c[0]][c[1]] == 0) { blScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), blScroll - d * 40)); return; } pat.Vel[c[0]][c[1]] = (byte)Math.Max(1, Math.Min(127, pat.Vel[c[0]][c[1]] + (d > 0 ? 8 : -8))); };
        Hint2(stepsR, "Click a step to switch it on or off, drag along a row to paint, the wheel on a step sets how hard it hits");
        // the velocity lane of the picked pad
        var vl = Rc(cellX, bottom + 6, gx + gw - cellX - 4, velH); FillRR(g, TM.C(200, 10, 5, 24), vl, 6); StrokeRR(g, Color.FromArgb(120, NeonViolet), vl, 6, 1);
        Txt(g, "VELOCITY: " + (blPads[blSel].Sound != null ? blPads[blSel].Name : "pick a pad"), 9, DimC, Rc(gx, vl.Y + 4, cellX - gx - 6, 30), StringAlignment.Near, true);
        if (blPads[blSel].Sound != null)
        {
            Color vc = Neon(BlJobColors[Math.Max(0, Math.Min(5, blPads[blSel].Job))]);
            for (int s = 0; s < blSteps; s++) { int v = pat.Vel[blSel][s]; if (v == 0) continue; float bh = (vl.Height - 8) * v / 127f; using (var b = new SolidBrush(Color.FromArgb(220, vc))) g.FillRectangle(b, vl.X + s * cellW + cellW * 0.25f, vl.Bottom - 4 - bh, cellW * 0.5f, bh); }
            Action<PointF> setV = p => { int s = (int)((p.X - vl.X) / cellW); if (s < 0 || s >= blSteps || pat.Vel[blSel][s] == 0) return; pat.Vel[blSel][s] = (byte)Math.Max(1, Math.Min(127, (int)Math.Round((vl.Bottom - 4 - p.Y) / (vl.Height - 8) * 127))); };
            var vr = Reg(vl, () => { BlMark(); setV(MousePos); }); vr.OnDrag = setV; Hint2(vl, "Drag the bars up or down: how hard each step of " + blPads[blSel].Name + " hits");
        }
        if (blPlaying) Invalidate();
    }
    void BlStyleMenu(string title, Action<int> pick)
    {
        var m = Win.NewMenu(); m.Items.Add(new ToolStripMenuItem(title) { Enabled = false }); var fams = new List<ToolStripMenuItem>();
        for (int i = 0; i < PatStyles.Length; i++) { int k = i; var it = new ToolStripMenuItem(PatStyles[i].Name); it.Click += (a, b) => pick(k); Fam(fams, PatStyles[i].Family).DropDownItems.Add(it); }
        foreach (var f in fams) m.Items.Add(f); m.Show(this, Point.Round(MousePos));
    }
    void BlPadMenu(int i)
    {
        var pad = blPads[i]; var m = Win.NewMenu();
        var tm = new ToolStripMenuItem("Use a track from this song"); BlTrackItems(tm.DropDownItems, i); m.Items.Add(tm);
        m.Items.Add(new ToolStripMenuItem("Load a sound from a file...", null, (a, b) => { using (var d = new OpenFileDialog { Filter = "Sounds|*.wav;*.mp3;*.flac;*.ogg;*.aif;*.aiff|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) BlSetPad(i, new SoundItem { Name = Path.GetFileNameWithoutExtension(d.FileName), Path = d.FileName, Kind = "one-shot", Rel = Path.GetFileName(d.FileName) }); }));
        if (pad.Sound != null)
        {
            m.Items.Add(new ToolStripMenuItem("Rename...", null, (a, b) => { string nm = StudioDialogs.Ask(this, "Name of pad " + (i + 1), pad.Name); if (!string.IsNullOrEmpty(nm)) { BlMark(); pad.Name = nm.Trim(); } }));
            var job = new ToolStripMenuItem("Its job (colour, and where FILL and ADD TO SONG put it)"); for (int j = 0; j < Jobs.Length; j++) { int jj = j; job.DropDownItems.Add(new ToolStripMenuItem(Jobs[j], null, (a, b) => { BlMark(); pad.Job = jj; }) { Checked = pad.Job == j }); } m.Items.Add(job);
            m.Items.Add(new ToolStripMenuItem("Clear this pad", null, (a, b) => { BlMark(); pad.Sound = null; pad.Name = ""; foreach (var p in blPats) Array.Clear(p.Vel[i], 0, 32); }));
        }
        m.Show(this, Point.Round(MousePos));
    }
}
