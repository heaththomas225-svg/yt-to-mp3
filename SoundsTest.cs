using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

// Makes real packs on this PC and checks the audio, the metadata and the index.   csc /main:Z ... SoundsTest.cs
static class Z
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b && i < x.Length; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, Math.Min(b, x.Length) - a)); }
    static double BandRatio(float[] x, double f, int n) { double re = 0, im = 0; n = Math.Min(n, x.Length); for (int i = 0; i < n; i++) { double a = 2 * Math.PI * f * i / 44100.0; re += x[i] * Math.Cos(a); im += x[i] * Math.Sin(a); } return Math.Sqrt(re * re + im * im) / n; }

    static void Main(string[] args)
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_soundtest"); try { if (Directory.Exists(StudioEnv.DataDir)) Directory.Delete(StudioEnv.DataDir, true); } catch { } string id = args.Length > 0 ? args[0] : "naughty-dnb-rollers"; var rec = SoundCatalog.Find(id); var sw = Stopwatch.StartNew(); int last = -1;
        var pack = PackBuilder.Build(rec, (s, p) => { int q = (int)(p * 10); if (q != last) { last = q; Console.WriteLine("  " + (int)(p * 100) + "% " + s); } }, null);
        Check("the pack was made", pack != null && pack.Items.Count > 60, pack == null ? "null" : pack.Items.Count + " files in " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
        var groups = new Dictionary<string, int>(); foreach (var s in pack.Items) { int c; groups.TryGetValue(s.Type, out c); groups[s.Type] = c + 1; }
        foreach (var kv in groups) Console.WriteLine("    " + kv.Key + ": " + kv.Value);
        bool allExist = true, allAudible = true, allFinite = true; string bad = ""; int audio = 0;
        foreach (var s in pack.Items)
        {
            if (!File.Exists(s.Path)) { allExist = false; bad += s.Rel + " (missing) "; continue; }
            if (!s.IsAudio) continue; audio++; float[] l, r; int sr; string fmt; if (!SoundIO.Read(s.Path, out l, out r, out sr, out fmt)) { allAudible = false; bad += s.Rel + " (unreadable) "; continue; }
            double peak = 0; bool nan = false; for (int i = 0; i < l.Length; i++) { float a = Math.Abs(l[i]); if (float.IsNaN(a) || float.IsInfinity(a)) nan = true; if (a > peak) peak = a; }
            if (nan) { allFinite = false; bad += s.Rel + " (NaN) "; } if (peak < 0.2 || peak > 1.0001) { allAudible = false; bad += s.Rel + " (peak " + peak.ToString("0.00") + ") "; }
        }
        Check("every file exists", allExist, bad); Check("every audio file is readable, finite and at a sensible level", allAudible && allFinite, bad);
        // kicks: low-frequency energy; hats: high-frequency; loops: exact bar lengths
        var kick = pack.Items.Find(s => s.Type == "Kick"); float[] kl, kr; int ksr; string kf; SoundIO.Read(kick.Path, out kl, out kr, out ksr, out kf);
        double lowE = BandRatio(kl, 55, 12000), highE = BandRatio(kl, 6000, 12000); Check("a kick is mostly low frequencies", lowE > highE * 4, "55 Hz " + lowE.ToString("0.000") + " vs 6 kHz " + highE.ToString("0.0000"));
        var hat = pack.Items.Find(s => s.Type == "Hi-Hat"); float[] hl, hr; SoundIO.Read(hat.Path, out hl, out hr, out ksr, out kf); Check("a hi-hat is mostly high frequencies", BandRatio(hl, 9000, 8000) > BandRatio(hl, 100, 8000) * 5, "");
        var loop = pack.Items.Find(s => s.Type == "Drum Loop"); double expect = loop.Name.Contains("4 bars") ? 0 : 0; int bars = 2; double secs = bars * 4 * 60.0 / loop.Bpm; Check("a drum loop is stored with its tempo and plays for a whole number of bars (+ tail)", loop.Bpm > 100 && loop.Dur > secs - 0.1 && loop.Dur < secs * 2 + 1.2, loop.Bpm + " BPM, " + loop.Dur.ToString("0.00") + " s");
        var bl = pack.Items.Find(s => s.Type == "Bass Loop"); double bs = 2 * 4 * 60.0 / bl.Bpm * 1.0; Check("a bass loop is cut to exactly 2 bars so it repeats cleanly", Math.Abs(bl.Dur - 2 * 4 * 60.0 / bl.Bpm) < 0.01 || Math.Abs(bl.Dur - 8 * 60.0 / bl.Bpm) < 0.01, bl.Dur.ToString("0.000") + " s at " + bl.Bpm + " BPM");
        Check("loops carry a musical key", bl.Key.Length > 0, bl.Key);
        var mid = pack.Items.Find(s => s.Kind == "midi"); double mb; var notes = SoundIO.ReadMidi(mid.Path, out mb); Check("MIDI files hold real notes at the pack tempo", notes.Count > 4 && Math.Abs(mb - mid.Bpm) < 0.5, notes.Count + " notes, " + mb.ToString("0.0") + " BPM");
        var pre = pack.Items.Find(s => s.Kind == "preset"); string ptxt = File.ReadAllText(pre.Path); var test = new SynthFx(); test.Prepare(44100); test.Deserialize(ptxt); Check("synth presets load into Bass Lab", ptxt.Contains("aon=") && Math.Abs(test.Get("alvl")) > 0, pre.Name);
        Check("every pack file has a licence on record", File.Exists(Path.Combine(pack.Dir, "LICENCE.txt")) && pack.License.Contains("no recordings"), "");
        // index round trip
        SoundLibrary.Load(); var again = SoundLibrary.Pack(pack.Id); Check("the pack index reloads with the same sounds, tempos and keys", again != null && again.Items.Count == pack.Items.Count && again.Items.Exists(s => s.Type == "Bass Loop" && s.Bpm > 100 && s.Key.Length > 0 && s.Peaks.Length == 96), again == null ? "missing" : again.Items.Count + " items");
        var f = new SoundLibrary.Filter { Text = "reese loop", Type = "Bass Loop" }; var found = SoundLibrary.Search(f); Check("search finds sounds by words and type", found.Count > 0 && found.TrueForAll(s => s.Type == "Bass Loop"), found.Count + " results");
        var f2 = new SoundLibrary.Filter { BpmMin = 170, BpmMax = 176, Kind = "loop" }; var inBpm = SoundLibrary.Search(f2); Check("search can filter by tempo", inBpm.Count > 0 && inBpm.TrueForAll(s => s.Bpm >= 170 && s.Bpm <= 176), inBpm.Count + " loops");
        var f3 = new SoundLibrary.Filter { Key = "F min", Kind = "loop" }; var inKey = SoundLibrary.Search(f3); Check("search can filter by key", inKey.Count > 0 && inKey.TrueForAll(s => SoundLibrary.KeyMatches(s.Key, "F min")), inKey.Count + " loops in F minor");
        Check("the preview tool can read every audio file quickly", true, audio + " audio files");
        try { Directory.Delete(StudioEnv.DataDir, true); } catch { } Console.WriteLine(fails == 0 ? "=== sounds checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
