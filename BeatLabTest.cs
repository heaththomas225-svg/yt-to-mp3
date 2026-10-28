using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// The Beat Lab (pads that sound at once, steps, swing and humanise, variations in their own slots, saved with the project, onto the song)
// and the MIDI clips (notes always sit in clips; move, duplicate, split, merge, loop and trim them; saved; undone).
static class BeatLabTest
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Paint() { using (var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void F(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static int Ons(Track t) { lock (t.Midi) return t.Midi.Count(e => (e.S & 0xF0) == 0x90 && e.D2 > 0); }
    static double Render(int blocks, out double peak) { var m = typeof(StudioForm).GetMethod("BlRender", BF); int n = 512; var l = new float[n]; var r = new float[n]; double sum = 0; peak = 0; for (int b = 0; b < blocks; b++) { Array.Clear(l, 0, n); Array.Clear(r, 0, n); m.Invoke(sf, new object[] { l, r, n }); foreach (var v in l) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; } } return Math.Sqrt(sum / (blocks * 512.0)); }

    [STAThread] static void Main() { try { Run(); } catch (Exception ex) { Check("no crash", false, ex.ToString()); } Console.WriteLine(fails == 0 ? "=== beat lab checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1); }
    static void Run()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_bltest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(400);
        // ---- the Beat Lab ----
        Paint(); var bm = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the Beat Lab button sits next to PIANO ROLL at the top", bm.ContainsKey("BEAT LAB") && bm.ContainsKey("PIANO ROLL") && Math.Abs(bm["BEAT LAB"].X - bm["PIANO ROLL"].Right - 6) < 2, bm.ContainsKey("BEAT LAB") ? bm["BEAT LAB"].ToString() : "missing");
        sf.OpenBeatLab(); Paint(); Check("it opens inside the piano roll with a kit on the pads", sf.BlShown && sf.blPads.Count(p => p.Sound != null) >= 12, sf.blPads.Count(p => p.Sound != null) + " pads");
        double pk; sf.BlHit(0, 110); Render(30, out pk); Check("tapping a pad sounds straight away (the song is not playing)", pk > 0.05 && !eng.Playing, pk.ToString("0.000"));
        Check("a pad's key plays it (1 = pad 1, V = pad 16)", sf.BlKey(Keys.D1) && sf.BlKey(Keys.V) && !sf.BlKey(Keys.P));
        Check("a MIDI keyboard's pads play the pads (note 36 = pad 1, channel 10)", sf.BlMidi(0x90, 36, 100) && sf.BlMidi(0x99, 80, 100) && !sf.BlMidi(0x90, 72, 100));
        sf.BlFill(0); var A = sf.blPats[0]; int on = A.Vel.Sum(r => r.Count(v => v > 0)); Check("FILL writes a genre's beat into the steps", on >= 12, on + " steps");
        sf.blPlaying = true; double rms = Render(172, out pk); sf.blPlaying = false; Check("PLAY loops the beat on its own, never clipping", rms > 0.03 && pk <= 1.0, "rms " + rms.ToString("0.000") + " peak " + pk.ToString("0.000"));
        string before = string.Join(",", A.Vel.Select(r => string.Join("", r.Select(v => v > 0 ? "1" : "0")))); sf.BlVariation(); string after = string.Join(",", A.Vel.Select(r => string.Join("", r.Select(v => v > 0 ? "1" : "0"))));
        var B = sf.blPats[1]; string bs = string.Join(",", B.Vel.Select(r => string.Join("", r.Select(v => v > 0 ? "1" : "0"))));
        Check("VARIATION makes a new slot (B) that differs, and the original (A) is untouched", sf.blPats.Count == 2 && sf.blCur == 1 && before == after && bs != before);
        { double stepF = 44100 * 60.0 / Fx.ProjectBpm / 4; sf.blSwing = 0.66f; double s1 = (double)M("BlShift", 1, 2, stepF), s0 = (double)M("BlShift", 0, 2, stepF); sf.blHuman = 0.5f; double h1 = (double)M("BlShift", 4, 2, stepF), h2 = (double)M("BlShift", 4, 2, stepF); int v1 = (int)M("BlVel", 100, 5, 2);
          Check("SWING pushes every second 16th later; HUMANISE moves hits the same way every time", s1 > stepF * 0.25 && Math.Abs(s0) < 1e-9 && h1 == h2 && Math.Abs(h1) > 0 && v1 != 100, s1.ToString("0") + " / " + h1.ToString("0") + " / " + v1); sf.blSwing = 0.5f; sf.blHuman = 0; }
        string saved = sf.BlSave(); sf.BlLoad("", true); Check("a cleared Beat Lab is empty", sf.blPads.All(p => p.Sound == null) && sf.blPats.Count == 1);
        string roll = (string)M("PrPrefsText"); M("PrApplyPrefs", roll); sf.BlLoad(saved, true); Check("the Beat Lab is saved with the project and comes back the same (pads, steps, slots)", sf.BlSave() == saved && sf.blPats.Count == 2 && sf.blPads.Count(p => p.Sound != null) >= 12);
        { string withBl = (string)M("PrPrefsText"); M("PrApplyPrefs", ""); M("PrApplyPrefs", withBl); Check("...through the project's piano roll settings too", sf.BlSave() == saved, withBl.Length + " chars"); }
        { var si = new SoundItem { Name = "Test Clap", Path = MakeWav(), Kind = "one-shot", Rel = "x.wav", Type = "Clap" }; sf.blPadCount = 32; sf.BlSetPad(20, si); Check("a sound dropped on a pad goes on that pad (32 pads)", sf.blPads[20].Sound != null && sf.blPads[20].Name == "Test Clap" && sf.blPads[20].Job == 1); }
        { sf.blCur = 0; int n0 = eng.Tracks.Count; string msg = (string)M("BlAddToSong"); int added = eng.Tracks.Count - n0; Check("ADD TO SONG puts the beat in as note tracks, one per pad that plays", added >= 4 && eng.Tracks.Skip(n0).All(x => x.Instrument && x.Midi.Count > 0), added + " tracks: " + msg); }
        { var nt = eng.Tracks.Last(x => x.Instrument && x.Midi.Count > 0); string m1 = sf.BlPadFromTrack(25, nt, true); string m2 = sf.BlPadFromTrack(26, nt, false);
          var al = new float[44100]; for (int i = 0; i < al.Length; i++) al[i] = (float)(0.5 * Math.Sin(i * 2 * Math.PI * 220 / 44100) * (i < 22050 ? 1 : 0)); var at = StudioEngine.NewTrack("My Loop", "audio", AudioClip.FromArrays(al, al, "My Loop")); at.Offset = 44100; eng.Tracks.Add(at);
          string m3 = sf.BlPadFromTrack(27, at, false); var s27 = sf.blPads[27].Sound;
          Check("your own tracks go on pads: one hit of a note track, its whole part, an audio track (with silence cut off)", sf.blPads[25].Sound != null && sf.blPads[26].Sound != null && s27 != null && sf.blPads[27].Name == "My Loop", m1 + " / " + m2 + " / " + m3);
          eng.Tracks.Remove(at); }
        sf.blRec = true; sf.blPlaying = true; Render(3, out pk); int b4 = sf.blPats[0].Vel[5].Count(v => v > 0); sf.BlHit(5, 120); int af = sf.blPats[0].Vel[5].Count(v => v > 0); sf.blPlaying = false; sf.blRec = false;
        Check("REC writes a pad you hit into the steps while the beat loops", af >= b4 && sf.blPats[0].Vel[5].Any(v => v == 120), b4 + " -> " + af);
        // ---- the MIDI clips ----
        foreach (var t0 in eng.Tracks.ToList()) M("DeleteTrackQuiet", t0);
        var box = (StudioForm.PatBox)M("BuildPattern", 0, 4, -1); M("DropPatternBox", box, 0L); var t = eng.Tracks.First(x => x.Instrument); var bass = eng.Tracks.Last(x => x.Instrument);
        double bar = 44100 * 60.0 / Fx.ProjectBpm * 4; lock (t.Midi) foreach (var b in new[] { 6, 7 }) { t.Midi.Add(new MidiEv { Frame = (long)(b * bar), S = 0x90, D1 = 60, D2 = 100 }); t.Midi.Add(new MidiEv { Frame = (long)(b * bar + 2000), S = 0x80, D1 = 60, D2 = 0 }); }
        M("McSort", t); sf.McSync(t); Check("notes always sit in clips: a bar or more of silence starts a new one", t.MClips.Count == 2 && t.MClips[0].Start == 0 && Math.Abs(t.MClips[0].Len - 4 * bar) < 2 && Math.Abs(t.MClips[1].Start - 6 * bar) < 2, string.Join(", ", t.MClips.Select(c => (c.Start / bar).ToString("0.#") + "+" + (c.Len / bar).ToString("0.#"))));
        Paint(); int n0n = Ons(t);
        { var c0 = t.MClips[0]; var r = new RectangleF(400, 300, 200, 80); typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, new PointF(450, 340)); M("McDown", t, c0, r); F("mcGrab", 0L); long sp = (long)typeof(StudioForm).GetField("scroll", BF).GetValue(sf); double spp = (double)typeof(StudioForm).GetField("spp", BF).GetValue(sf); int hw = (int)typeof(StudioForm).GetProperty("HeadW", BF).GetValue(sf, null);
          float px = (float)(hw + (8 * bar - sp) / spp); M("McDragTo", new PointF(px, 340)); M("McUp"); sf.McSync(t); long minOn; lock (t.Midi) minOn = t.Midi.Where(e => (e.S & 0xF0) == 0x90 && e.D2 > 0).Min(e => e.Frame);
          Check("dragging a clip moves the whole melody (every note, none lost)", Ons(t) == n0n && Math.Abs(c0.Start - 8 * bar) < bar * 0.3 && minOn >= 6 * bar - 2, (c0.Start / bar).ToString("0.##") + " bars, " + Ons(t) + "/" + n0n + " notes");
          M("DoUndo"); sf.McSync(t); Check("Ctrl+Z puts it back", t.MClips.Any(c => c.Start == 0) && Ons(t) == n0n); }
        sf.mcSel.Clear(); sf.mcSel.Add(t.MClips[0]); sf.McDuplicate(); sf.McSync(t); Check("duplicate copies a clip right after itself", t.MClips.Count == 3 && Ons(t) == n0n + (n0n - 2), t.MClips.Count + " clips, " + Ons(t) + " notes");
        { var c = t.MClips.OrderBy(x => x.Start).First(); sf.McSplit(t, c, c.Start + (long)(2 * bar)); sf.McSync(t); Check("split at the needle makes two clips, every note kept", t.MClips.Count == 4 && Ons(t) == n0n * 2 - 2); }
        { var two = t.MClips.OrderBy(x => x.Start).Take(2).ToList(); sf.McMerge(t, two); sf.McSync(t); Check("merge joins the selected clips into one", t.MClips.Count == 3 && Math.Abs(t.MClips.OrderBy(x => x.Start).First().Len - 4 * bar) < 2); }
        { sf.McSync(bass); var bc = bass.MClips[0]; int bn = Ons(bass); typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, new PointF(590, 305)); M("McDown", bass, bc, new RectangleF(400, 300, 200, 80));
          Check("the top-right corner of a clip is its loop handle", (int)typeof(StudioForm).GetField("mcMode", BF).GetValue(sf) == 4);
          F("mcMoved", true); bc.Len = (long)(8 * bar); M("McUp"); sf.McSync(bass); Check("looping a 4-bar clip to 8 bars repeats its notes", Ons(bass) == bn * 2 && Math.Abs(bass.MClips[0].Len - 8 * bar) < 2, bn + " -> " + Ons(bass));
          typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, new PointF(595, 375)); M("McDown", bass, bass.MClips[0], new RectangleF(400, 300, 200, 80)); Check("the bottom-right corner trims", (int)typeof(StudioForm).GetField("mcMode", BF).GetValue(sf) == 2);
          F("mcMoved", true); bass.MClips[0].Len = (long)(2 * bar); M("McUp"); sf.McSync(bass); Check("trimming a clip to 2 bars drops the notes after its end", Ons(bass) == bn / 2 && bass.MClips.Count == 1, Ons(bass) + " notes");
          M("DoUndo"); sf.McSync(bass); Check("undo brings the trimmed notes back", Ons(bass) == bn * 2, Ons(bass) + ""); }
        { string pp = Path.Combine(StudioEnv.DataDir, "mc.ytmproj"); var names = t.MClips.Select(c => c.Start + ":" + c.Len).ToList(); t.MClips[0].Name = "Verse kick"; ProjectIO.Save(eng, pp); var e2 = new StudioEngine(); ProjectIO.Load(e2, pp, null); bool ok = e2.Tracks.Count > 0; var t2 = ok ? e2.Tracks.FirstOrDefault(x => x.Name == t.Name) : null;
          Check("the clips are saved with the project (where, how long, their names)", t2 != null && t2.MClips.Select(c => c.Start + ":" + c.Len).SequenceEqual(names) && t2.MClips[0].Name == "Verse kick", t2 == null ? "not loaded" : t2.MClips.Count + " clips"); }
        { var tl = (RectangleF)typeof(StudioForm).GetMethod("TimelineRect", BF).Invoke(sf, null); typeof(Control).GetMethod("OnMouseDown", BF).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.Left, 1, (int)tl.X + 30, (int)tl.Y + 4, 0) }); typeof(Control).GetMethod("OnMouseUp", BF).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.Left, 1, (int)tl.X + 30, (int)tl.Y + 4, 0) });
          Check("a click on the song gives the keys back from the Beat Lab", !(bool)typeof(StudioForm).GetField("blFocus", BF).GetValue(sf));
          sf.mcSel.Clear(); sf.mcSel.Add(t.MClips[0]); int before2 = t.MClips.Count; Check("Delete removes the selected clip and its notes", sf.McKey(Keys.Delete) && t.MClips.Count == before2 - 1); }
        Paint(); Check("the song draws with the clips (no errors)", true);
    }
    static string MakeWav() { string p = Path.Combine(StudioEnv.DataDir, "clap.wav"); var x = new float[8000]; var rnd = new Random(3); for (int i = 0; i < x.Length; i++) x[i] = (float)((rnd.NextDouble() * 2 - 1) * Math.Exp(-i / 900.0) * 0.8); SoundIO.WriteWav(p, x, x, 44100, 24); return p; }
}
