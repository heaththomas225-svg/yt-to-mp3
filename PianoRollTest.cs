using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// The piano roll: real tracks with real samples (kick, snare, hats, 808 from the sound generator) and the built-in keys, edited with the
// mouse through the same coordinates the screen uses; ghost notes that cannot be edited; velocity; copy, duplicate, quantise, undo; zoom
// and scroll alignment; scale and chords; sampler pitch modes and kits heard through the real engine; GENERATE; saving and reopening.
//   csc /main:PR ... PianoRollTest.cs
static class PR
{
    static int fails; static StudioForm sf; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Click(float x, float y) { Ev("OnMouseMove", MouseButtons.None, 0, (int)x, (int)y); Ev("OnMouseDown", MouseButtons.Left, 1, (int)x, (int)y); Ev("OnMouseUp", MouseButtons.Left, 1, (int)x, (int)y); Paint(); }
    static void RClick(float x, float y) { Ev("OnMouseMove", MouseButtons.None, 0, (int)x, (int)y); Ev("OnMouseDown", MouseButtons.Right, 1, (int)x, (int)y); Ev("OnMouseUp", MouseButtons.Right, 1, (int)x, (int)y); Paint(); }
    static void Drag(float x0, float y0, float x1, float y1) { Ev("OnMouseMove", MouseButtons.None, 0, (int)x0, (int)y0); Ev("OnMouseDown", MouseButtons.Left, 1, (int)x0, (int)y0); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)(x0 + (x1 - x0) * i / 6), (int)(y0 + (y1 - y0) * i / 6)); Ev("OnMouseUp", MouseButtons.Left, 1, (int)x1, (int)y1); Paint(); }
    static void Shot(string name) { Paint(); var bm = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bm, new Rectangle(0, 0, sf.Width, sf.Height)); bm.Save("C:/Users/thoma/nfout/" + name + ".png", System.Drawing.Imaging.ImageFormat.Png); }
    const int SR = 44100;
    static double BeatF { get { return SR * 60.0 / Fx.ProjectBpm; } }
    static float XOfBeat(double beat) { return sf.PrX((long)Math.Round(beat * BeatF)); }
    static float YOfNote(int n) { return sf.PrY(n) + sf.prRowH / 2f; }
    static List<StudioForm.PrNote> Notes(Track t) { return StudioForm.NotesOf(t); }
    static float[] RenderOnly(Track t, double seconds) { float[] l, r; eng.Render(0, (long)(seconds * SR), new RenderOpts { Only = new List<Track> { t } }, null, out l, out r); return l; }
    static string Wav(string dir, string name, Buf b) { string p = Path.Combine(dir, name + ".wav"); SoundIO.WriteWav(p, b.L, b.R, SR, 16); return p; }
    // the first frame at or after 'from' where the sound jumps up (a hit)
    static long Onset(float[] x, long from, long to) { int win = 64; double prev = 1e-9; for (long i = Math.Max(0, from); i + win < Math.Min(x.Length, to); i += win) { double e = 0; for (long k = i; k < i + win; k++) e += x[k] * x[k]; e /= win; if (e > 1e-5 && e > prev * 8) return i; prev = Math.Max(e, 1e-9); } return -1; }
    static double Hz(float[] x, long a, long n) { int zc = 0; for (long i = a + 1; i < a + n && i < x.Length; i++) if (x[i - 1] < 0 && x[i] >= 0) zc++; return zc * (double)SR / n; }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy;
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_pianoroll"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true; Fx.ProjectBpm = 120;
        // real sounds from the sound generator: a kick, a snare, a hat and an 808 of a drill pack
        var rec = SoundCatalog.Find("uk-drill"); var items = InstantLib.ItemsOf(rec.Id); string err;
        Func<string, SoundItem> pick = ty => items.First(s => s.Type == ty);
        string kickP = Wav(tmp, "Kick Punch", InstantLib.MakeBuf(pick("Kick"), out err)), snareP = Wav(tmp, "Snare Crack", InstantLib.MakeBuf(pick("Snare"), out err)), hatP = Wav(tmp, "Hat Closed", InstantLib.MakeBuf(pick("Hi-Hat"), out err));
        var e8 = items.First(s => s.Type == "808" && s.Core.Contains("Clean")); string e8P = Wav(tmp, "808 Clean Sub", InstantLib.MakeBuf(e8, out err));
        sf = new StudioForm(app); sf.ClientSize = new Size(1600, 900); sf.Show(); Pump(400); Paint();
        var kick = sf.NewSamplerTrack("Kick", kickP, false, 60); var snare = sf.NewSamplerTrack("Snare", snareP, false, 60); var hats = sf.NewSamplerTrack("Hats", hatP, false, 60); var bass = sf.NewSamplerTrack("808", e8P, true, 36);
        var keysT = sf.AddMidiTrack(); keysT.Name = "Melody"; SetF("recOn", false);
        Check("tracks with real samples load (kick, snare, hats, 808) and a MIDI track", new[] { kick, snare, hats, bass }.All(t => t != null && t.Instrument && t.Fx.OfType<SamplerFx>().First().Zones.Count == 1 && t.Fx.OfType<SamplerFx>().First().Zones[0].L != null), "");

        // ---- opening it: the PIANO ROLL button at the top, a tab of the bottom panel ----
        Paint(); var bm0 = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("there is a piano button next to MIDI REC, then the mixer button (and no second PIANO ROLL button)", bm0.ContainsKey("PIANO ROLL") && bm0.ContainsKey("MIDI REC") && bm0.ContainsKey("MIXER TOP") && bm0["PIANO ROLL"].Right <= bm0["MIXER TOP"].X && bm0["MIXER TOP"].X - bm0["PIANO ROLL"].Right < 12 && bm0["MIXER TOP"].Right <= bm0["MIDI REC"].X && bm0["MIDI REC"].X - bm0["MIXER TOP"].Right < 12 && Math.Abs(bm0["PIANO ROLL"].Y - bm0["MIDI REC"].Y) < 2, bm0.ContainsKey("PIANO ROLL") ? bm0["PIANO ROLL"].ToString() : "missing");
        sf.OpenPianoRoll(kick); sf.prSnap = 2; sf.prLenBeats = 0.25; Paint(); Paint();
        Check("the piano roll opens on the picked track", sf.PrShown && sf.prTrack == kick, sf.prTrack == null ? "none" : sf.prTrack.Name);
        // ---- kick: four on the beats of bar 1 (clicks on the grid) ----
        for (int b = 0; b < 4; b++) Click(XOfBeat(b) + 3, YOfNote(60));
        var kn = Notes(kick); Check("clicking the grid adds notes exactly on the beats (snap 1 beat)", kn.Count == 4 && kn.Select(n => n.Start).SequenceEqual(new[] { 0L, (long)Math.Round(BeatF), (long)Math.Round(2 * BeatF), (long)Math.Round(3 * BeatF) }.Select(v => v)) && kn.All(n => n.Note == 60), string.Join(",", kn.Select(n => n.Start)));
        Check("the other tracks are untouched", Notes(snare).Count == 0 && Notes(hats).Count == 0, "");
        Shot("pr-kick");
        // ---- snare: the kick shows as ghost notes; clicking where a ghost note is makes a snare note, the kick stays ----
        sf.PrSetTrack(snare); Paint(); Paint(); var ghosts = (IList)typeof(StudioForm).GetMethod("PrGhostTracks", BF).Invoke(sf, null);
        Check("switching to the snare shows the kick as ghost notes", ghosts.Count == 1 && sf.prNotes.Count == 0, ghosts.Count + " ghost tracks");
        Click(XOfBeat(1) + 3, YOfNote(60)); Click(XOfBeat(3) + 3, YOfNote(60));
        Check("a click on a ghost note's spot adds a snare note and never edits the kick", Notes(snare).Count == 2 && Notes(kick).Count == 4, Notes(snare).Count + " snare, " + Notes(kick).Count + " kick");
        RClick(XOfBeat(0) + 3, YOfNote(60)); Check("right-clicking a ghost note does not delete it (it is not a note of this track)", Notes(kick).Count == 4, "");
        Shot("pr-ghosts");
        // ---- hats: 16ths ----
        sf.PrSetTrack(hats); sf.prSnap = 4; Paint(); for (int s = 0; s < 16; s++) Click(XOfBeat(s * 0.25) + 2, YOfNote(60));
        var hn = Notes(hats); Check("sixteen hi-hats on the 1/16 grid", hn.Count == 16 && hn.Select((n, i) => Math.Abs(n.Start - Math.Round(i * 0.25 * BeatF)) <= 1).All(x => x), hn.Count + "");
        // velocity: drag a bar without moving the note
        { var n3 = hn[2]; float vx = sf.PrX(n3.Start) + 2; var vel = sf.prVel; Drag(vx, vel.Bottom - 10, vx, vel.Y + 8); var n3b = Notes(hats).First(n => n.Start == n3.Start);
          Check("the velocity lane changes a note's velocity and leaves it where it is", n3b.Vel > 110 && Notes(hats).Count == 16, n3b.Vel + ""); }
        { var ns = Notes(hats); var ctx = ns.OrderBy(n => n.Start).ToList(); typeof(StudioForm).GetField("prSel", BF); sf.prSel.Clear(); foreach (var n in sf.prNotes) sf.prSel.Add(n); }
        // ---- moving and resizing ----
        { sf.prSel.Clear(); var target = sf.prNotes.OrderBy(n => n.Start).ElementAt(5); long s0 = target.Start; float x = sf.PrX(target.Start) + 3, y = YOfNote(target.Note); Drag(x, y, x + (float)(sf.prPpb * 0.25 * 2), y - sf.prRowH * 2);
          var moved = Notes(hats).OrderBy(n => n.Start).ToList(); Check("dragging a note moves it in time and pitch (on the grid)", moved.Any(n => Math.Abs(n.Start - (s0 + Math.Round(0.5 * BeatF))) <= 1 && n.Note == 62), "");
          M("DoUndo"); Paint(); Check("undo puts it back", Notes(hats).All(n => n.Note == 60) && Notes(hats).Count == 16, "");
          M("DoRedo"); Paint(); Check("redo moves it again", Notes(hats).Any(n => n.Note == 62), ""); M("DoUndo"); Paint(); }
        { sf.PrSetTrack(kick); Paint(); Paint(); var target = sf.prNotes.OrderBy(n => n.Start).First(); float x1 = sf.PrX(target.Start + target.Len) - 1, y = YOfNote(target.Note); long len0 = target.Len; Drag(x1, y, x1 + (float)(sf.prPpb * 0.5), y);
          var after = Notes(kick).OrderBy(n => n.Start).First(); Check("dragging a note's right edge makes it longer (on the grid)", after.Len > len0 + BeatF * 0.4 && Notes(kick).Count == 4, len0 + " -> " + after.Len); M("DoUndo"); Paint();
          sf.PrSetTrack(hats); Paint(); Paint(); var h0 = sf.prNotes.OrderBy(n => n.Start).First(); float hx = sf.PrX(h0.Start + h0.Len) - 1; Drag(hx, YOfNote(60), hx + (float)(sf.prPpb * 0.5), YOfNote(60));
          var hs2 = Notes(hats).OrderBy(n => n.Start).ToList(); Check("a note stretched over the next note on its key ends where that note starts (they never overlap)", hs2.Count == 16 && hs2[0].Start + hs2[0].Len <= hs2[1].Start, hs2[0].Len + ""); M("DoUndo"); Paint(); }
        // ---- box select, delete; copy and duplicate ----
        { Ev("OnMouseMove", MouseButtons.None, 0, (int)XOfBeat(0) + 1, (int)YOfNote(60) - 20); typeof(StudioForm).GetProperty("ModifierKeys"); }
        sf.PrSetTrack(kick); Paint(); sf.prSel.Clear(); foreach (var n in sf.prNotes) sf.prSel.Add(n); sf.PrDuplicate(); Paint();
        var kd = Notes(kick); Check("duplicate (Ctrl+D) copies the selected bar after itself", kd.Count == 8 && kd.Count(n => n.Start >= (long)Math.Round(4 * BeatF) - 1) == 4, kd.Count + "");
        sf.prFocus = true; Check("Delete removes the selected notes (only this track's)", (bool)M("PrKey", Keys.Delete) && Notes(kick).Count == 4 && Notes(snare).Count == 2, Notes(kick).Count + "");
        // ---- quantise ----
        { sf.prSnap = 8; Paint(); Click(XOfBeat(2.18) , YOfNote(64)); var off = Notes(kick).First(n => n.Note == 64); sf.prSnap = 4; sf.prSel.Clear(); sf.prSel.Add(sf.prNotes.First(n => n.Note == 64)); sf.PrQuantise(); var q = Notes(kick).First(n => n.Note == 64);
          Check("a note off the grid (snap off) is moved onto the grid by QUANTISE", Math.Abs(off.Start - Math.Round(2.18 * BeatF)) < BeatF * 0.02 && Math.Abs(q.Start - Math.Round(2.25 * BeatF)) <= 1, off.Start + " -> " + q.Start); sf.prSel.Clear(); sf.prSel.Add(sf.prNotes.First(n => n.Note == 64)); M("PrKey", Keys.Delete); }
        // ---- zoom and scroll: clicks still land where they are drawn ----
        { sf.prPpb = 150; sf.prScrollBeats = 6.5; sf.prRowH = 20; sf.prScrollY = 900; sf.prSnap = 4; Paint(); Paint(); int note = sf.PrNoteAt(sf.prGrid.Y + sf.prGrid.Height / 2); double beat = 7.0; Click(XOfBeat(beat) + 2, YOfNote(note));
          var z = Notes(kick).FirstOrDefault(n => n.Note == note); Check("zoomed in and scrolled, a click still makes the note exactly where it was clicked", z != null && Math.Abs(z.Start - Math.Round(beat * BeatF)) <= 1, z == null ? "none" : z.Start + " vs " + Math.Round(beat * BeatF)); if (z != null) { sf.prSel.Clear(); sf.prSel.Add(sf.prNotes.First(n => n.Note == note)); M("PrKey", Keys.Delete); }
          sf.prPpb = 64; sf.prScrollBeats = 0; sf.prRowH = 14; sf.prScrollY = -1; Paint(); Paint(); }
        // ---- scale lock and chords (on the melody track) ----
        sf.PrSetTrack(keysT); Paint(); Paint(); sf.prKeyRoot = 9; sf.prScale = 1; sf.prLock = true; sf.prSnap = 2; Paint();
        Click(XOfBeat(0) + 3, YOfNote(58)); var sn = Notes(keysT); Check("with LOCK on an A minor scale, a note clicked off the scale lands on it (A# -> A)", sn.Count == 1 && sn[0].Note == 57, sn.Count > 0 ? StudioForm.PrNoteName(sn[0].Note) : "none");
        sf.prLock = false; sf.prScale = 0; sf.prChord = 2; Click(XOfBeat(1) + 3, YOfNote(57)); var ch = Notes(keysT).Where(n => Math.Abs(n.Start - BeatF) < 2).Select(n => n.Note).OrderBy(n => n).ToList();
        Check("CHORDS: minor chord adds the three notes at once", ch.SequenceEqual(new[] { 57, 60, 64 }), string.Join(",", ch)); sf.prChord = 0;
        // ---- the real engine plays what the piano roll wrote ----
        { var kr = RenderOnly(kick, 2.5); bool ok = true; string got = ""; for (int b = 0; b < 4; b++) { long want = (long)Math.Round(b * BeatF), o = Onset(kr, want - 400, want + 2000); got += o + " "; if (o < 0 || Math.Abs(o - want) > 0.004 * SR) ok = false; }
          Check("the engine plays the kick notes at their places (within 4 ms)", ok, got); }
        { var sp = kick.Fx.OfType<SamplerFx>().First(); lock (kick.Midi) { kick.Midi.Clear(); kick.Midi.Add(new MidiEv { Frame = 0, S = 0x90, D1 = 60, D2 = 110 }); kick.Midi.Add(new MidiEv { Frame = 2000, S = 0x80, D1 = 60 }); kick.Midi.Add(new MidiEv { Frame = SR, S = 0x90, D1 = 72, D2 = 110 }); kick.Midi.Add(new MidiEv { Frame = SR + 2000, S = 0x80, D1 = 72 }); }
          var kr = RenderOnly(kick, 2); double d = 0, e = 0; for (int i = 0; i < SR / 2; i++) { d += Math.Abs(kr[i] - kr[SR + i]); e += Math.Abs(kr[i]); }
          Check("a fixed-pitch drum sample sounds the same on any key (C4 and C5 give the same kick)", d / e < 0.02, (d / e).ToString("0.0000")); }
        { lock (bass.Midi) { bass.Midi.Clear(); bass.Midi.Add(new MidiEv { Frame = 0, S = 0x90, D1 = 36, D2 = 110 }); bass.Midi.Add(new MidiEv { Frame = SR - 4000, S = 0x80, D1 = 36 }); bass.Midi.Add(new MidiEv { Frame = SR, S = 0x90, D1 = 43, D2 = 110 }); bass.Midi.Add(new MidiEv { Frame = 2 * SR - 4000, S = 0x80, D1 = 43 }); }
          var br = RenderOnly(bass, 2.2); double h1 = Hz(br, 8000, 16000), h2 = Hz(br, SR + 8000, 16000), ratio = h2 / Math.Max(1, h1);
          Check("the 808 follows the notes: G (7 semitones up from its C root) plays it 1.5x higher", Math.Abs(ratio - Math.Pow(2, 7 / 12.0)) < 0.06, h1.ToString("0.0") + " Hz -> " + h2.ToString("0.0") + " Hz (x" + ratio.ToString("0.00") + ")");
          double tail = 0; for (int i = (int)(2.1 * SR); i < br.Length; i++) tail = Math.Max(tail, Math.Abs(br[i])); Check("a note that ends stops its sound (no stuck notes)", tail < 0.02, tail.ToString("0.0000")); }
        // a kit: one track, a sample per key; an unmapped key is silent
        { var kitT = sf.NewSamplerTrack("Kit", null, false, 60); var ks = kitT.Fx.OfType<SamplerFx>().First(); ks.AddToKit(kickP, "Kick", 36); ks.AddToKit(snareP, "Snare", 38);
          lock (kitT.Midi) { foreach (var p in new[] { new[] { 0, 36 }, new[] { SR / 2, 38 }, new[] { SR, 40 } }) { kitT.Midi.Add(new MidiEv { Frame = p[0], S = 0x90, D1 = (byte)p[1], D2 = 110 }); kitT.Midi.Add(new MidiEv { Frame = p[0] + 1000, S = 0x80, D1 = (byte)p[1] }); } kitT.Midi.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
          var kr = RenderOnly(kitT, 1.6); double e1 = 0, e2 = 0, e3 = 0; for (int i = 0; i < 6000; i++) { e1 += kr[i] * kr[i]; e2 += kr[SR / 2 + i] * kr[SR / 2 + i]; e3 += kr[SR + 4000 + i] * kr[SR + 4000 + i]; }
          Check("a kit plays the kick on C2 and the snare on D2; a key with no sample is silent", e1 > 1 && e2 > 1 && e3 < e1 * 0.01 && ks.KeyName(38) == "Snare", e1.ToString("0") + " / " + e2.ToString("0") + " / " + e3.ToString("0.00"));
          sf.PrSetTrack(kitT); Paint(); Paint(); Shot("pr-kit"); M("DeleteTrackQuiet", kitT); }
        // mute is respected
        { kick.Mute = true; float[] l, r; eng.Render(0, SR, new RenderOpts { Only = new List<Track> { kick } }, null, out l, out r); double e = l.Sum(v => (double)v * v); kick.Mute = false; Check("a muted track's notes are not heard", e < 1e-6, e.ToString("0.000000")); }

        // ---- GENERATE: a starting beat on the tracks by their jobs ----
        foreach (var t in new[] { kick, snare, hats, bass }) lock (t.Midi) t.Midi.Clear();
        Check("each track's job is found from its sound (kick, snare, hats, 808)", sf.JobOf(kick) == 0 && sf.JobOf(snare) == 1 && sf.JobOf(hats) == 2 && sf.JobOf(bass) == 5 && sf.JobOf(keysT) == -1, sf.JobOf(kick) + "," + sf.JobOf(snare) + "," + sf.JobOf(hats) + "," + sf.JobOf(bass));
        string msg = sf.GeneratePattern(0, 4, 0, true);
        { var k2 = Notes(kick); var b2 = Notes(bass); double step = SR * 60.0 / 140 / 4;
          Check("Trap: 140 BPM and real notes on the kick, snare, hats and 808 for 4 bars", Fx.ProjectBpm == 140 && k2.Count > 8 && Notes(snare).Count >= 4 && Notes(hats).Count > 40 && b2.Count == k2.Count, msg);
          Check("the kick lands on the trap grid and the 808 plays with every kick", k2.All(n => Math.Abs(n.Start / step - Math.Round(n.Start / step)) < 0.01) && b2.Select(n => n.Start).SequenceEqual(k2.Select(n => n.Start)), "");
          { var hs = Notes(hats).OrderBy(n => n.Start).ToList(); bool dbl = false; for (int i = 1; i < hs.Count; i++) if (hs[i].Start - hs[i - 1].Start < step * 0.75) dbl = true; Check("the hi-hats have rolls (quick doubles) and loud and soft hits", hs.Select(n => n.Vel).Distinct().Count() >= 2 && dbl, hs.Count + " hats"); } }
        { foreach (var t in new[] { kick, snare, hats, bass }) lock (t.Midi) t.Midi.Clear(); var jobs = F<Dictionary<Track, int>>("prJobs"); jobs[snare] = -1; jobs[hats] = -1; string m2 = sf.GeneratePattern(3, 4, 0, false);
          Check("with only a kick and an 808 it still writes their parts, and says what would make it fuller", Notes(kick).Count > 0 && Notes(bass).Count > 0 && Notes(snare).Count == 0 && m2.Contains("snare") && m2.Contains("hi-hats"), m2); jobs.Clear(); }
        { foreach (var t in new[] { kick, snare, hats, bass, keysT }) lock (t.Midi) t.Midi.Clear(); string m4 = sf.GeneratePattern(0, 8, 0, false, bass);
          Check("JUST THIS TRACK: the 808 gets 8 bars and no other track gets anything (no claps added)", Notes(bass).Count > 8 && Notes(bass).Max(n => n.Start) > SR * 60.0 / Fx.ProjectBpm * 4 * 7 && Notes(kick).Count == 0 && Notes(snare).Count == 0 && Notes(hats).Count == 0 && Notes(keysT).Count == 0, m4);
          string m5 = sf.GeneratePattern(1, 4, 0, false, keysT); var kc = Notes(keysT);
          Check("JUST THIS TRACK on a piano: chords in the key for 4 bars, the others left alone", kc.Count >= 12 && kc.GroupBy(n => n.Start).All(gr => gr.Count() == 3) && Notes(kick).Count == 0 && Notes(snare).Count == 0, m5 + " / " + kc.Count); }
        { foreach (var t in eng.Tracks.ToList()) M("DeleteTrackQuiet", t); string m3 = sf.GeneratePattern(0, 4, 0, false); Check("with no sounds yet it says to add them on tracks first", m3.StartsWith("Add your sounds first"), m3); }

        // ---- saving and reopening: notes, samples and the piano roll's settings come back ----
        { kick = sf.NewSamplerTrack("Kick", kickP, false, 60); bass = sf.NewSamplerTrack("808", e8P, true, 36); sf.GeneratePattern(0, 2, 0, false); sf.prKeyRoot = 4; sf.prScale = 1; sf.prSnap = 5; sf.prGhostOff.Add(bass);
          var kn0 = Notes(kick); var bn0 = Notes(bass); ProjectIO.Roll = sf.PrPrefsText(); string path = Path.Combine(tmp, "beat.ytmproj"); ProjectIO.Save(eng, path);
          var e2 = new StudioEngine(); ProjectIO.Load(e2, path, s => { }); var k3 = e2.Tracks.First(t => t.Name == "Kick"); var b3 = e2.Tracks.First(t => t.Name == "808"); var s3 = b3.Fx.OfType<SamplerFx>().First();
          Check("save and reopen: every note (time, length, pitch, velocity) comes back", Notes(k3).Count == kn0.Count && Notes(b3).Select(n => n.Start + "/" + n.Len + "/" + n.Note + "/" + n.Vel).SequenceEqual(bn0.Select(n => n.Start + "/" + n.Len + "/" + n.Note + "/" + n.Vel)), Notes(k3).Count + " kick notes");
          Check("and the sampler's sample, root note and pitch mode", s3.Zones.Count == 1 && s3.Zones[0].Path == e8P && s3.Zones[0].Root == 36 && s3.Zones[0].Follow && s3.Zones[0].L != null, s3.Zones.Count > 0 ? s3.Zones[0].Path : "no zone");
          string roll = ProjectIO.Roll; sf.PrApplyPrefs(""); sf.PrApplyPrefs(roll); Check("and the piano roll's settings (key, scale, snap, hidden ghosts)", sf.prKeyRoot == 4 && sf.prScale == 1 && sf.prSnap == 5 && sf.prGhostOff.Contains(bass), roll); }
        // ---- an audio track stays audio; LOAD IT INTO A SAMPLER makes a sampler track from it ----
        { var clip = AudioClip.FromArrays(new float[SR], new float[SR], "loop"); for (int i = 0; i < SR; i++) clip.L[i] = clip.R[i] = (float)(0.3 * Math.Sin(i * 0.05)); var at = StudioEngine.NewTrack("Loop", "audio", clip); eng.Tracks.Add(at);
          Check("an audio track is not a note track (its clip stays real audio)", !StudioForm.PrCanEdit(at), ""); int before = eng.Tracks.Count; M("PrAudioToSampler", at); var st = sf.prDraft;
          Check("LOAD IT INTO THE PIANO ROLL puts the sound in the piano roll only (no new track; the audio track stays)", eng.Tracks.Count == before && st != null && st.Fx.OfType<SamplerFx>().Any(z => z.Zones.Count == 1 && z.Zones[0].L != null) && at.Clip == clip && sf.prTrack == st, eng.Tracks.Count + " tracks"); }

        // ---- any track: click it, choose its sound in the piano roll ----
        {
            var empty = StudioEngine.NewTrack("Audio 9", "audio", null); M("AddTrack", empty); int ei = eng.Tracks.IndexOf(empty); typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, ei); sf.OpenPianoRoll(null); Paint(); Paint();
            Check("clicking a track (here an empty one) shows it in the piano roll", sf.prTrack == empty, sf.prTrack == null ? "none" : sf.prTrack.Name);
            var bm = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf);
            Check("a track with no sound offers the sounds to choose (piano, a sound from SOUNDS, Purphex Wobble, Bass Lab, more)", bm.ContainsKey("PIANO") && bm.ContainsKey("A SOUND FROM SOUNDS") && bm.ContainsKey("PURPHEX WOBBLE") && bm.ContainsKey("BASS LAB"), string.Join(", ", bm.Keys.Where(k => k.Length > 3 && k.ToUpperInvariant() == k).Take(12)));
            int n00 = eng.Tracks.Count; M("PrUseSound", empty, null, null, null); Paint();
            Check("PIANO makes your pattern play the piano: nothing is added to or changed on the tracks", sf.prDraft != null && sf.prTrack == sf.prDraft && !StudioForm.PrCanEdit(empty) && eng.Tracks.Count == n00 && !eng.Tracks.Contains(sf.prDraft) && eng.Draft == sf.prDraft, eng.Tracks.Count + " tracks; draft " + (sf.prDraft == null ? "none" : sf.prDraft.Name));
            int n0 = eng.Tracks.Count; string pre = WobbleFx.FactoryPresets()[0]; int kq = pre.IndexOf('|');
            var dr = sf.prDraft; M("PrUseSound", dr, "wobble", pre.Substring(kq + 1), pre.Substring(0, kq)); Paint();
            var wf = dr.Fx.OfType<WobbleFx>().FirstOrDefault();
            Check("SOUND > a Purphex Wobble preset changes your pattern's sound (still no track)", wf != null && eng.Tracks.Count == n0 && dr.Fx.OfType<InstrumentFx>().Count() == 1 && wf.CurPreset == pre.Substring(0, kq) && sf.prDraft == dr, wf == null ? "none" : wf.CurPreset);
            M("PrUseSound", dr, "synth", null, null); Paint();
            Check("changing the sound again swaps the instrument (one instrument on your pattern)", dr.Fx.OfType<InstrumentFx>().Count() == 1 && dr.Fx.OfType<WobbleFx>().Count() == 0, string.Join(",", dr.Fx.Select(x => x.Type)));
            { double bt = SR * 60.0 / Fx.ProjectBpm; StudioForm.WriteNotes(dr, new List<StudioForm.PrNote> { new StudioForm.PrNote { Start = (long)(bt * 4), Len = (long)bt, Note = 48, Vel = 100 }, new StudioForm.PrNote { Start = (long)(bt * 6), Len = (long)bt, Note = 51, Vel = 100 } });
              Paint(); var bmD = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("your pattern shows as a box to drag onto the song (with ADD TO SONG)", bmD.ContainsKey("ADD TO SONG"), "");
              { int na = eng.Tracks.Count; string amsg = (string)M("DropDraftAsAudio", (long)(bt * 8)); var ac = eng.Tracks.Count == na + 1 ? eng.Tracks.Last() : null; float pk = 0; if (ac != null && ac.Clip != null) foreach (var v in ac.Clip.L) pk = Math.Max(pk, Math.Abs(v));
                Check("dragging your pattern onto the song makes ONE audio clip there (movable like any sound), with sound in it", ac != null && ac.Clip != null && !ac.Instrument && ac.Offset == (long)(bt * 8) && pk > 0.01f && ac.Clip.Length >= (long)(bt * 4) - 4, amsg + " peak " + pk.ToString("0.000"));
                Check("that clip is saved as a WAV (so the project reopens it) and your pattern stays in the piano roll with its sound", ac != null && System.IO.File.Exists(ac.Clip.Path) && sf.prDraft == dr && dr.Fx.OfType<InstrumentFx>().Count() == 1 && dr.Fx.OfType<InstrumentFx>().All(x => x.Owner == dr) && StudioForm.NotesOf(dr).Count == 2, ac == null ? "none" : ac.Clip.Path); }
              int n2 = eng.Tracks.Count; string dmsg2 = (string)M("DropDraft", (long)(bt * 16));
              var placed = eng.Tracks.Count == n2 + 1 ? eng.Tracks.Last() : null; long firstOn = placed == null ? -1 : placed.Midi.Where(e => (e.S & 0xF0) == 0x90).Min(e => e.Frame);
              Check("dragging it onto bar 5 makes ONE new track there, with your notes (from their first bar) and its sound", placed != null && Math.Abs(firstOn - (long)(bt * 16)) < 4 && placed.Fx.OfType<InstrumentFx>().Count() == 1 && sf.prDraft == null && eng.Draft == null, dmsg2 + " first note at " + (firstOn / bt).ToString("0.00") + " beats"); }
            var clip2 = AudioClip.FromArrays(new float[SR], new float[SR], "x"); var aud = StudioEngine.NewTrack("Vocal", "vocal", clip2); M("AddTrack", aud); int n1 = eng.Tracks.Count;
            var nt = (Track)M("PrUseSound", aud, null, null, null);
            Check("an audio track keeps its audio: choosing a sound for it makes your pattern (no new track until you drag it)", nt != null && nt == sf.prDraft && aud.Clip != null && !aud.Instrument && eng.Tracks.Count == n1, eng.Tracks.Count + " tracks");
            float wsum = 0; var lb = new float[4096]; var rb = new float[4096]; var w2 = new WobbleFx(); w2.Prepare(SR); w2.Owner = empty; w2.BeginBlock(null, 0, 4096, false); w2.NoteOn(36, 110); w2.BeginBlock(null, 0, 4096, false); for (int b = 0; b < 8; b++) { Array.Clear(lb, 0, 4096); Array.Clear(rb, 0, 4096); w2.Process(lb, rb, 4096); foreach (var v in lb) wsum += Math.Abs(v); }
            Check("the Purphex Wobble really sounds when a note plays", wsum > 100, wsum.ToString("0"));
        }
        // ---- GENERATE makes a box of its own; dropped on the song it becomes its own tracks ----
        {
            var before = eng.Tracks.ToList(); var notesBefore = before.ToDictionary(t => t, t => { lock (t.Midi) return t.Midi.Count; });
            var box = (StudioForm.PatBox)M("BuildPattern", 0, 4); sf.prBox = box; Paint();
            Check("GENERATE makes a beat in its own box (kick, snare, hats, open hat, 808...) without touching your tracks", box != null && box.Parts.Count >= 4 && box.Parts.Any(p => p.Job == 0) && box.Parts.Any(p => p.Job == 5) && before.All(t => { lock (t.Midi) return t.Midi.Count == notesBefore[t]; }), box == null ? "none" : box.Parts.Count + " parts");
            Check("every part of the box has a sound (your own or the style's pack)", box.Parts.All(p => p.Sound != null && p.Sound.Zones.Count == 1 && p.Sound.Zones[0].L != null), string.Join(", ", box.Parts.Select(p => p.Name + ":" + (p.Sound == null ? "none" : p.Sound.Zones[0].Name))));
            { var bad = new List<string>(); var sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < StudioForm.PatStyles.Length; i++) { var bx = (StudioForm.PatBox)M("BuildPattern", i, 4); if (bx == null || bx.Parts.Count < 3 || !bx.Parts.Any(p => p.Job == 0)) bad.Add(StudioForm.PatStyles[i].Name); }
              int fams = StudioForm.PatStyles.Select(p => p.Family).Distinct().Count(); bool named = StudioForm.PatStyles.All(p => p.Family.Length > 0 && p.Bpm >= 60 && p.Bpm <= 180) && StudioForm.PatStyles.Select(p => p.Name).Distinct().Count() == StudioForm.PatStyles.Length;
              Check("GENERATE has all the genres (" + StudioForm.PatStyles.Length + " styles in " + fams + " families) and every one makes a beat with a kick", StudioForm.PatStyles.Length >= 60 && fams >= 8 && named && bad.Count == 0, string.Join(", ", bad) + " " + sw.ElapsedMilliseconds + " ms"); sf.prBox = box; }
            { var sw = System.Diagnostics.Stopwatch.StartNew(); var mm = Win.NewMenu(); int nItems = 0; foreach (var gname in VocalStyles.Genres) { var it = new ToolStripMenuItem(gname); foreach (var st in VocalStyles.All.Where(x => x.Genre == gname)) { it.DropDownItems.Add(new ToolStripMenuItem(st.Name)); nItems++; } mm.Items.Add(it); }
              Check("a menu with every vocal preset in it is ready in moments (it took a minute and froze the studio)", sw.ElapsedMilliseconds < 3000 && nItems > 1000, nItems + " options in " + sw.ElapsedMilliseconds + " ms"); mm.Dispose(); }
            var bm = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the box shows in the piano roll with ADD TO SONG", bm.ContainsKey("ADD TO SONG"), "");
            double bar = SR * 60.0 / Fx.ProjectBpm * 4; string dmsg = (string)M("DropPatternBox", box, (long)(bar * 2));
            var added = eng.Tracks.Where(t => !before.Contains(t)).ToList();
            Check("dropping it at bar 3 makes its own tracks (one per part), the notes start at bar 3", added.Count == box.Parts.Count && added.All(t => StudioForm.PrCanEdit(t) && t.Midi.Count > 0 && t.Midi.Min(e => e.Frame) >= (long)(bar * 2) - 2), added.Count + " tracks; " + dmsg);
            Check("your own tracks keep their notes (nothing is written over them)", before.All(t => { lock (t.Midi) return t.Midi.Count == notesBefore[t]; }), "");
            { var box2 = (StudioForm.PatBox)M("BuildPattern", 0, 2); sf.prBox = box2; int nb = eng.Tracks.Count; string bmsg = (string)M("DropPatternBoxAsAudio", box2, (long)(bar * 4)); var bc = eng.Tracks.Count == nb + 1 ? eng.Tracks.Last() : null; float pk2 = 0; if (bc != null && bc.Clip != null) foreach (var v in bc.Clip.L) pk2 = Math.Max(pk2, Math.Abs(v));
              Check("dragging a GENERATE beat box onto the song makes ONE audio clip of the whole beat at that bar", bc != null && bc.Clip != null && !bc.Instrument && bc.Offset == (long)(bar * 4) && pk2 > 0.01f && bc.Clip.Length >= (long)(bar * 2) - 4 && System.IO.File.Exists(bc.Clip.Path) && sf.prBox == null, bmsg + " peak " + pk2.ToString("0.000"));
              Check("the box's own sounds are copies (the beat's parts still have their sounds)", box2.Parts.All(pp => pp.Sound != null && pp.Sound.Owner == null), ""); }
            var bassT = added.FirstOrDefault(t => t.Name.Contains("808")); Check("the 808 part follows the notes (it can be switched to Bass Lab with SOUND)", bassT != null && bassT.Fx.OfType<SamplerFx>().Any(z => z.AnyFollow), bassT == null ? "none" : bassT.Name);
            Check("the box is used up", sf.prBox == null, "");
            float sum = 0; var lb = new float[2048]; var rb = new float[2048]; var kickT = added[0]; var smp = kickT.Fx.OfType<SamplerFx>().First(); smp.BeginBlock(kickT, 0, 2048, false); smp.NoteOn(smp.Zones[0].Root, 110); for (int q = 0; q < 6; q++) { smp.BeginBlock(kickT, 0, 2048, false); Array.Clear(lb, 0, 2048); smp.Process(lb, rb, 2048); foreach (var v in lb) sum += Math.Abs(v); }
            Check("a generated part really plays its sound", sum > 10, sum.ToString("0"));
        }
        // ---- 3.73.1 fixes: whole-word track jobs; piano roll settings start from the defaults in a project without them ----
        {
            Func<string, int> job = nm => { var t = StudioEngine.NewTrack(nm, "inst", null); t.Instrument = true; return sf.JobOf(t); };
            Check("track jobs use whole words: Custom Lead is not a tom, Subtle Pad is not a sub, Sub Kick is a kick, Hi Hat is hats", job("Custom Lead") == -1 && job("Subtle Pad") == -1 && job("Sub Kick") == 0 && job("Hi Hat") == 2 && job("808 Slide") == 5 && job("Open Hat") == 3, job("Custom Lead") + " " + job("Subtle Pad") + " " + job("Sub Kick") + " " + job("Hi Hat") + " " + job("808 Slide") + " " + job("Open Hat"));
            sf.prSnap = 1; sf.prScale = 3; sf.prKeyRoot = 2; sf.prGhosts = false; sf.PrApplyPrefs("");
            Check("a project without piano roll settings starts from the defaults (not the last project's)", sf.prSnap == 4 && sf.prScale == 0 && sf.prKeyRoot == 9 && sf.prGhosts, sf.prSnap + " " + sf.prScale + " " + sf.prKeyRoot + " " + sf.prGhosts);
        }
        Console.WriteLine(fails == 0 ? "=== piano roll checks passed ===" : "=== " + fails + " FAILED ===");
        try { sf.Close(); } catch { } Environment.Exit(fails == 0 ? 0 : 1);
    }
}
