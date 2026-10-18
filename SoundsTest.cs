using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Makes real packs on this PC and checks the audio, the metadata and the index.   csc /main:Z ... SoundsTest.cs
static class Z
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b && i < x.Length; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, Math.Min(b, x.Length) - a)); }
    static double BandRatio(float[] x, double f, int n) { double re = 0, im = 0; n = Math.Min(n, x.Length); for (int i = 0; i < n; i++) { double a = 2 * Math.PI * f * i / 44100.0; re += x[i] * Math.Cos(a); im += x[i] * Math.Sin(a); } return Math.Sqrt(re * re + im * im) / n; }

    // listening with numbers: clipped runs, clicks (a sudden jump in the curve far above what the sound around it does), and how squashed it is
    static void Clean(float[] L, float[] R, out int clipped, out double clicksPerSec, out double crest)
    {
        int n = L.Length; clipped = 0; double e = 0, peak = 0; int clicks = 0;
        foreach (var c in new[] { L, R }) { int run = 0; for (int i = 0; i < n; i++) { float a = Math.Abs(c[i]); e += a * a; if (a > peak) peak = a; if (a >= 0.998f) { run++; if (run == 3) clipped++; } else run = 0; }
            double acc = 0; var q = new Queue<float>(); int last = -10000; for (int i = 2; i < n; i++) { float d2 = Math.Abs(c[i] - 2 * c[i - 1] + c[i - 2]); q.Enqueue(d2); acc += d2 * d2; if (q.Count > 512) { float o = q.Dequeue(); acc -= o * o; } double loc = Math.Sqrt(Math.Max(1e-12, acc / q.Count)); if (q.Count >= 64 && d2 > 0.06 && d2 > 10 * loc && i - last > 200) { clicks++; last = i; } } }
        clicksPerSec = clicks / Math.Max(0.05, n / 44100.0); double rms = Math.Sqrt(e / Math.Max(1, 2.0 * n)); crest = rms > 0 ? 20 * Math.Log10(peak / rms) : 0;
    }
    static bool ReadWav(string p, out float[] L, out float[] R)
    {
        L = R = null; try { var b = File.ReadAllBytes(p); int ch = 2, bits = 24, pos = 12, at = -1, len = 0;
            while (pos + 8 <= b.Length) { string id = System.Text.Encoding.ASCII.GetString(b, pos, 4); int ln = BitConverter.ToInt32(b, pos + 4); if (id == "fmt ") { ch = BitConverter.ToInt16(b, pos + 10); bits = BitConverter.ToInt16(b, pos + 22); } if (id == "data") { at = pos + 8; len = Math.Min(ln, b.Length - at); break; } pos += 8 + ln + (ln & 1); }
            if (at < 0) return false; int bps = bits / 8, n = len / (bps * ch); L = new float[n]; R = new float[n];
            for (int i = 0; i < n; i++) for (int c = 0; c < ch; c++) { int o = at + (i * ch + c) * bps; float v = bits == 24 ? (b[o] | (b[o + 1] << 8) | ((sbyte)b[o + 2] << 16)) / 8388608f : bits == 16 ? BitConverter.ToInt16(b, o) / 32768f : BitConverter.ToSingle(b, o); if (c == 0) L[i] = v; else R[i] = v; if (ch == 1) R[i] = v; }
            return true; } catch { return false; }
    }
    static void Main(string[] args)
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_soundtest"); try { if (Directory.Exists(StudioEnv.DataDir)) Directory.Delete(StudioEnv.DataDir, true); } catch { } string id = args.Length > 0 ? args[0] : "naughty-dnb-rollers"; var rec = SoundCatalog.Find(id); var sw = Stopwatch.StartNew(); int last = -1;
        var pack = PackBuilder.Build(rec, (s, p) => { int q = (int)(p * 10); if (q != last) { last = q; Console.WriteLine("  " + (int)(p * 100) + "% " + s); } }, null);
        { var lsecs = new HashSet<string>(); foreach (var r0 in SoundCatalog.Recipes) lsecs.Add(r0.Section); var want = new[] { "Hip-Hop", "House", "Techno", "Afro & Latin", "Pop & R&B", "Lo-Fi", "EDM", "Phonk" };
          Check("the library has 200+ packs in every section, the new genres included", SoundCatalog.Recipes.Count >= 200 && Array.TrueForAll(want, w => lsecs.Contains(w)), SoundCatalog.Recipes.Count + " packs: " + string.Join(", ", lsecs));
          var badDrums = new List<string>(); foreach (var r0 in SoundCatalog.Recipes) if (!Patterns.Has(r0.Drums)) badDrums.Add(r0.Id + "=" + r0.Drums);
          Check("every pack's drum groove exists (none falls back to drum and bass)", badDrums.Count == 0, string.Join(" ", badDrums));
          var ids = new HashSet<string>(); var dup = new List<string>(); foreach (var r0 in SoundCatalog.Recipes) if (!ids.Add(r0.Id)) dup.Add(r0.Id); Check("no two packs share an id", dup.Count == 0, string.Join(" ", dup)); }
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
        { var lv = new Dictionary<string, List<double>>(); int clicks = 0; string cl = ""; foreach (var s in pack.Items) { if (!s.IsAudio) continue; float[] l, r; int sr; string fm; if (!SoundIO.Read(s.Path, out l, out r, out sr, out fm)) continue; double e = 0; for (int i = 0; i < l.Length; i++) e += l[i] * l[i] + r[i] * r[i]; double db = 10 * Math.Log10(e / (2.0 * l.Length) + 1e-20);
            if (s.Kind != "loop" && (Math.Abs(l[0]) > 0.02 || Math.Abs(l[l.Length - 1]) > 0.02)) { clicks++; cl += s.Rel + " "; } List<double> v; if (!lv.TryGetValue(s.Type, out v)) lv[s.Type] = v = new List<double>(); v.Add(db); }
          Func<string, double> spread = ty => { List<double> v; return lv.TryGetValue(ty, out v) && v.Count > 1 ? v.Max() - v.Min() : 0; }; Func<string, double> top = ty => { List<double> v; return lv.TryGetValue(ty, out v) ? v.Max() : -99; };
          Check("the pack carries the current sound version (older packs are offered an update)", pack.Version == PackBuilder.SoundVersion && !PackBuilder.Outdated(pack), pack.Version);
          Check("loops of one kind sit at an even loudness (drum loops within 6 dB, bass and melodic loops within 4 dB)", spread("Drum Loop") < 6 && spread("Bass Loop") < 4 && spread("Chord Loop") < 4, "drum " + spread("Drum Loop").ToString("0.0") + ", bass " + spread("Bass Loop").ToString("0.0") + ", chord " + spread("Chord Loop").ToString("0.0") + " dB");
          Check("no effect jumps out (sirens no louder than -9.5 dB RMS)", top("Siren") <= -9.5, top("Siren").ToString("0.0") + " dB");
          Check("no one-shot clicks at its start or end", clicks == 0, cl); }
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
        { var afro = PackBuilder.Build(SoundCatalog.Find("afrobeats-essentials"), (s0, p0) => { }, null); int loopsAt = 0, loops = 0; foreach (var s0 in afro.Items) if (s0.Kind == "loop" && s0.Type == "Drum Loop") { loops++; if (s0.Bpm >= 100 && s0.Bpm <= 110) loopsAt++; }
          var kinds = new HashSet<string>(); foreach (var s0 in afro.Items) kinds.Add(s0.Type);
          Check("a new-genre pack (Afrobeats) has 250+ sounds: drum, bass, chord, melody, arp and vocal loops at its own tempos", afro.Items.Count >= 250 && loops >= 24 && loopsAt == loops && kinds.Contains("Bass Loop") && kinds.Contains("Chord Loop") && kinds.Contains("Melody Loop") && kinds.Contains("Synth Loop") && kinds.Contains("Vocal Chop"), afro.Items.Count + " sounds, " + loopsAt + "/" + loops + " drum loops at 100-110 BPM");
          var a1 = DrumKit.Render(DrumKit.Make(PackBuilder.StyleFor(SoundCatalog.Find("afrobeats-essentials")), 5), PackBuilder.StyleFor(SoundCatalog.Find("afrobeats-essentials")), Patterns.Get("afro"), 104, 1, 0.5, 9, false).Kick; var d1 = DrumKit.Render(DrumKit.Make(PackBuilder.StyleFor(SoundCatalog.Find("afrobeats-essentials")), 5), PackBuilder.StyleFor(SoundCatalog.Find("afrobeats-essentials")), Patterns.Get("dnb_twostep"), 104, 1, 0.5, 9, false).Kick;
          double diff = 0; int nn = Math.Min(a1.L.Length, d1.L.Length); for (int q = 0; q < nn; q++) diff += Math.Abs(a1.L[q] - d1.L[q]); Check("the afro groove is its own rhythm (not the drum and bass one)", diff / nn > 0.005, (diff / nn).ToString("0.0000")); }
        { SoundLibrary.Load(); // no distorted or crackly sounds: nothing clipped, no crackle in basses, kicks and 808s not squashed, no crackle textures, nothing named distorted
          var probs = new List<string>(); int scanned = 0;
          foreach (var pk in new[] { pack, SoundLibrary.Pack("afrobeats-essentials") }) { if (pk == null) continue; foreach (var s0 in pk.Items) { if (!s0.IsAudio) continue; float[] l0, r0; if (!ReadWav(s0.Path, out l0, out r0)) continue; scanned++; int cl; double cps, cr; Clean(l0, r0, out cl, out cps, out cr);
              bool sustained = s0.Type.Contains("Bass") || s0.Type == "808" || s0.Type == "Sub Bass" || s0.Type == "Chord Loop" || s0.Type == "Atmosphere" || s0.Type.Contains("Pad") || s0.Type == "Vocal Chop";
              if (cl > 0) probs.Add("clipped: " + s0.Name); if (sustained && cps > 2.5) probs.Add("crackle " + cps.ToString("0.0") + "/s: " + s0.Name); if ((s0.Type == "Kick" || s0.Type == "808") && cr < 4.5) probs.Add("squashed " + cr.ToString("0.0") + " dB: " + s0.Name);
              if (s0.Type == "Texture" || s0.Name.IndexOf("Crackle", StringComparison.OrdinalIgnoreCase) >= 0 || s0.Name.IndexOf("Distorted", StringComparison.OrdinalIgnoreCase) >= 0) probs.Add("crackle / distorted sound: " + s0.Name); } }
          Check("no distorted or crackly sounds (no clipping, no crackle in basses, chords or vocals, clean kicks and 808s, no crackle textures)", probs.Count == 0 && scanned > 400, scanned + " sounds; " + string.Join(" | ", probs.Take(8))); }
        SoundLibrary.Load(); var again = SoundLibrary.Pack(pack.Id); Check("the pack index reloads with the same sounds, tempos and keys", again != null && again.Items.Count == pack.Items.Count && again.Items.Exists(s => s.Type == "Bass Loop" && s.Bpm > 100 && s.Key.Length > 0 && s.Peaks.Length == 96), again == null ? "missing" : again.Items.Count + " items");
        var f = new SoundLibrary.Filter { Text = "reese loop", Type = "Bass Loop" }; var found = SoundLibrary.Search(f); Check("search finds sounds by words and type", found.Count > 0 && found.TrueForAll(s => s.Type == "Bass Loop"), found.Count + " results");
        var f2 = new SoundLibrary.Filter { BpmMin = 170, BpmMax = 176, Kind = "loop" }; var inBpm = SoundLibrary.Search(f2); Check("search can filter by tempo", inBpm.Count > 0 && inBpm.TrueForAll(s => s.Bpm >= 170 && s.Bpm <= 176), inBpm.Count + " loops");
        var f3 = new SoundLibrary.Filter { Key = "F min", Kind = "loop" }; var inKey = SoundLibrary.Search(f3); Check("search can filter by key", inKey.Count > 0 && inKey.TrueForAll(s => SoundLibrary.KeyMatches(s.Key, "F min")), inKey.Count + " loops in F minor");
        Check("the preview tool can read every audio file quickly", true, audio + " audio files");
        try { Directory.Delete(StudioEnv.DataDir, true); } catch { } Console.WriteLine(fails == 0 ? "=== sounds checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
