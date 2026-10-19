using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// =====================================================================================================
//  How a pack is made. A Recipe says what kind of pack it is (genre, drum character, tempo, keys, bass flavours); PackBuilder turns it
//  into real files on your PC: one-shots, loops, stems, MIDI, synth presets, effects, vocal-style chops and a songstarter kit.
// =====================================================================================================
class Recipe
{
    public string Id, Name, Section, Genre, Sub, Desc, Family, Drums, Mood, Tags, Parts = "all"; public string[] Flavours, Keys; public double[] Bpms; public int Seed, Hue;
    public bool Has(string part) { return Parts == "all" || ("," + Parts + ",").IndexOf("," + part + ",", StringComparison.Ordinal) >= 0; }
}

class PackWriter
{
    public readonly SoundPack Pack; readonly Action<string, float> progress; readonly Func<bool> cancel; public int Planned = 90; public readonly Rnd R;
    public PackWriter(SoundPack p, int seed, Action<string, float> progress, Func<bool> cancel) { Pack = p; this.progress = progress; this.cancel = cancel; R = new Rnd(seed); Directory.CreateDirectory(p.Dir); }
    public bool Cancelled { get { return cancel != null && cancel(); } }

    // even levels while browsing: loops of one kind sit at the same loudness (never above -1 dB peak), and dense one-shots
    // (sirens, long effects) are kept from jumping out. The stems of a song starter keep their mix, so they are left alone.
    // a clean look-ahead limiter: the level comes down smoothly just before a peak and back up after it (no clipping, no distortion)
    public static void Limit(Buf b, float ceiling)
    {
        int n = b.Length, la = 441; if (n == 0) return; var need = new float[n];   // 10 ms look-ahead
        for (int i = 0; i < n; i++) { float a = Math.Max(Math.Abs(b.L[i]), Math.Abs(b.R[i])); need[i] = a > ceiling ? ceiling / a : 1f; }
        var g = new float[n]; var dq = new int[n]; int h = 0, t = 0;   // the smallest gain needed in the next la samples (a sliding minimum)
        for (int i = n - 1; i >= 0; i--) { while (t > h && need[dq[t - 1]] >= need[i]) t--; dq[t++] = i; while (dq[h] > i + la) h++; g[i] = need[dq[h]]; }
        // the average of those minimums over the look-ahead: a smooth ramp that reaches the needed gain exactly at the peak (never above it)
        double acc = 0, env = 1, rel = 1 - Math.Exp(-1.0 / (0.12 * 44100));
        for (int i = 0; i < n; i++) { acc += g[i]; if (i >= la) acc -= g[i - la]; double sm = acc / Math.Min(i + 1, la); env = sm < env ? sm : env + (sm - env) * rel; float k = (float)Math.Min(env, 1.0); b.L[i] *= k; b.R[i] *= k; }
        for (int i = 0; i < n; i++) { if (b.L[i] > ceiling) b.L[i] = ceiling; else if (b.L[i] < -ceiling) b.L[i] = -ceiling; if (b.R[i] > ceiling) b.R[i] = ceiling; else if (b.R[i] < -ceiling) b.R[i] = -ceiling; }   // (only a safety net: the gain above already keeps it under)
    }
    static void Level(Buf b, string type, string kind)
    {
        if (type == "Stem" || type == "Songstarter") return; double e = 0; for (int i = 0; i < b.Length; i++) e += b.L[i] * b.L[i] + b.R[i] * b.R[i]; if (e <= 0) return;
        double rms = 10 * Math.Log10(e / (2.0 * b.Length)), peak = 20 * Math.Log10(Math.Max(1e-9, b.Peak())), gain;
        if (kind == "loop") { double target = type == "Drum Loop" || type == "Top Loop" || type == "Drum Fill" || type == "Percussion Loop" ? -11 : type == "Bass Loop" ? -12 : -15; gain = Math.Min(target - rms, -1 - peak + 2);   /* a sparse loop may go up to 2 dB past its peak: the clean limiter catches only the peaks */
            if (gain > -1 - peak) { b.Gain((float)Math.Pow(10, gain / 20)); Limit(b, 0.891f); return; } }
        else { bool heavy = type == "Kick" || type == "808" || type == "Sub Bass"; double cap = heavy ? -6 : -10; gain = rms > cap ? cap - rms : 0; }
        if (Math.Abs(gain) > 0.05) b.Gain((float)Math.Pow(10, gain / 20));
    }
    // the level every sound is written at (instant loops use it too, so they sit at the same loudness as the packs)
    public static Buf Finish(Buf b, string type, string kind, double peakDb)
    {
        b = b.Copy(); b.Normalise(peakDb, kind == "loop" ? 0 : 24, kind == "loop" ? 0 : Math.Min(1323, b.Length / 10));   /* one-shots end with a 30 ms fade */ Level(b, type, kind); return b;
    }
    // writes a sound as a 24-bit WAV and lists it
    public SoundItem Audio(Buf b, string folder, string name, string type, string group, string kind, double bpm, string key, string tags, string mood, double peakDb)
    {
        if (Cancelled) throw new OperationCanceledException();
        b = Finish(b, type, kind, peakDb); string file = Clean(name) + ".wav", rel = folder + "/" + file; string path = Path.Combine(Pack.Dir, rel.Replace('/', Path.DirectorySeparatorChar));
        SoundIO.WriteWav(path, b.L, b.R, SoundGen.SR, 24);
        var s = new SoundItem { PackId = Pack.Id, PackName = Pack.Name, Rel = rel, Path = path, Id = Pack.Id + "/" + rel, Name = name, Type = type, Group = group, Kind = kind, Genre = Pack.Genre, Sub = Pack.Sub, Tags = tags, Mood = mood, Bpm = bpm, Key = key ?? "", Dur = b.Length / (double)SoundGen.SR, Rate = SoundGen.SR, Format = "WAV 24-bit", Peaks = SoundIO.Peaks(b.L, b.R, 96) };
        Pack.Items.Add(s); Report(name); return s;
    }
    public SoundItem Midi(string folder, string name, string type, double bpm, string key, string tags, string mood, List<double[]> notes)
    {
        if (Cancelled) throw new OperationCanceledException(); string rel = folder + "/" + Clean(name) + ".mid", path = Path.Combine(Pack.Dir, rel.Replace('/', Path.DirectorySeparatorChar)); SoundIO.WriteMidi(path, bpm, name, notes);
        double end = 0; foreach (var n in notes) end = Math.Max(end, n[0] + n[1]); var s = new SoundItem { PackId = Pack.Id, PackName = Pack.Name, Rel = rel, Path = path, Id = Pack.Id + "/" + rel, Name = name, Type = type, Group = "MIDI", Kind = "midi", Genre = Pack.Genre, Sub = Pack.Sub, Tags = tags, Mood = mood, Bpm = bpm, Key = key ?? "", Dur = end * 60.0 / bpm, Format = "MIDI", Rate = 0 };
        Pack.Items.Add(s); Report(name); return s;
    }
    public SoundItem Preset(string folder, string name, string patch, string tags, string mood)
    {
        string rel = folder + "/" + Clean(name) + ".purphexsynth", path = Path.Combine(Pack.Dir, rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, patch, Encoding.UTF8);
        var s = new SoundItem { PackId = Pack.Id, PackName = Pack.Name, Rel = rel, Path = path, Id = Pack.Id + "/" + rel, Name = name, Type = "Synth Preset", Group = "Presets", Kind = "preset", Genre = Pack.Genre, Sub = Pack.Sub, Tags = tags + ",bass lab", Mood = mood, Format = "Bass Lab preset" };
        Pack.Items.Add(s); Report(name); return s;
    }
    void Report(string what) { if (progress != null) progress(what, Math.Min(0.99f, Pack.Items.Count / (float)Planned)); }
    public static string Clean(string s) { var sb = new StringBuilder(); foreach (char c in s) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == '/' ? '-' : c); return sb.ToString().Trim(); }
}

class DrumStyle
{
    public double KickF0 = 55, KickStart = 240, KickTau = 0.025, KickDecay = 0.22, KickDrive = 2.2, KickClick = 0.5, KickLen = 0.55;
    public double SnareBody = 190, SnareNoise = 3200, SnareDecay = 0.14, SnareAmt = 0.9, SnareDrive = 1.8, SnareSnap = 0.3, SnareLen = 0.4;
    public double HatC = 0.045, HatO = 0.28, HatBright = 10, ClapCentre = 1200, ClapTail = 0.1, Swing = 0.5, HatPitch = 1, CymDecay = 1, CymPitch = 1, HatHp = 6800, HatMetal = 0.7, CymTone = 1; public int Variant, HatSet, CymSet; public string Pattern = "dnb_twostep"; public double SnareBodyMul = 1;
    public Pat Pat { get { return Patterns.Get(Pattern); } }
}
class Pat { public string K = "", S = "", C = "", H = "", O = "", P = "", G = ""; public int Bars = 1; public string Fill = ""; }

static class Patterns
{
    static readonly Dictionary<string, Pat> d = new Dictionary<string, Pat> {
        { "dnb_twostep", new Pat { K = "x.........x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = ".............x..", G = ".......g.g.....g", P = ".....x.....x...." } },
        { "dnb_roller", new Pat { K = "x.........x.x...", S = "....x.......x...", H = "xxxxxxxxxxxxxxxx", O = "..............x.", G = "..g...g.g..g..g.", P = "x.....x...x....." } },
        { "dnb_jumpup", new Pat { K = "x.....x...x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..x...x...x...x.", G = "...g.......g....", P = "x...x...x...x..." } },
        { "dnb_liquid", new Pat { K = "x.........x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "", G = "......g......g..", P = "..x.....x......." } },
        { "dnb_halftime", new Pat { K = "x.........x.....", S = "........x.......", H = "x.x.x.x.x.x.x.x.", O = "............x...", G = "...........g....", P = "..x.....x...x..." } },
        { "dnb_neuro", new Pat { K = "x.....x.x.......", S = "....x.......x...", H = "x.xxx.x.x.xxx.x.", O = "", G = "..g.g..g..g.g...", P = "x.......x......." } },
        { "dnb_minimal", new Pat { K = "x.........x.....", S = "....x.......x...", H = "..x...x...x...x.", O = "", G = ".........g......", P = "" } },
        { "dnb_dark", new Pat { K = "x.......x.x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..............x.", G = "...g.....g......", P = "......x......x.." } },
        { "dnb_rave", new Pat { K = "x...x...x...x...", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..x...x...x...x.", G = "", P = "x.x.x.x.x.x.x.x." } },
        { "jungle", new Pat { Bars = 2, K = "x.x.......x.x...x.......x.x.....", S = "....x..x.x..x..x....x..x.x..x.x.", H = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", O = "", G = ".......g.g..........g..g........", P = "" } },
        { "ukg_2step", new Pat { K = "x.........x.....", S = "....x.......x...", H = "x.xx.xx.xx.xx.x.", O = "..x...........x.", G = "..g..g...g...g..", P = "...x..x....x..x.", Fill = "" } },
        { "ukg_speed", new Pat { K = "x...x...x...x...", S = "....x.......x...", C = "....x.......x...", H = "..x...x...x...x.", O = "..x...x...x...x.", G = "", P = "x.x.x.x.x.x.x.x." } },
        { "bassline_4x4", new Pat { K = "x...x...x...x...", S = "", C = "....x.......x...", H = "xxxxxxxxxxxxxxxx", O = "..x...x...x...x.", G = "", P = "..x...x...x...x." } },
        { "house", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "x.xxx.xxx.xxx.xx", O = "..x...x...x...x.", G = "", P = "x.x.x.x.x.x.x.x." } },
        { "trap", new Pat { K = "x.......x..x....", S = "........x.......", C = "........x.......", H = "xxxxxxxxxxxxxtxx", O = "..............x.", G = "", P = "" } },
        { "drill", new Pat { K = "x.......x.....x.", S = "........x.......", C = "........x.......", H = "x.x.x.xtx.x.xtx.", O = "", G = "", P = "" } },
        { "boombap", new Pat { K = "x.....x...x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..............x.", G = "......g.........", P = "" } },
        { "dubstep", new Pat { K = "x.........x.....", S = "........x.......", C = "........x.......", H = "x.x.x.x.x.x.x.x.", O = "..............x.", G = "", P = "" } },
        { "afro", new Pat { K = "x.....x...x.....", S = "", C = "......x.......x.", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "..x..x....x..x.." } },
        { "amapiano", new Pat { K = "x...x...x...x...", S = "", C = ".......x.......x", H = "..x...x...x...x.", O = "", G = "", P = "x.xx.x.xx.x.x.xx" } },
        { "dembow", new Pat { K = "x...x...x...x...", S = "...x..x....x..x.", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "" } },
        { "techno", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "..x...x...x...x.", O = "......x.......x.", G = "", P = "x..x..x..x..x..x" } },
        { "techhouse", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "x.xxx.xxx.xxx.xx", O = "..x...x...x...x.", G = "", P = ".x.x.x.x.x.x.x.x" } },
        { "deephouse", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "..x.x.x...x.x.x.", O = "..x.......x.....", G = "", P = "...x.......x...." } },
        { "lofi", new Pat { K = "x......x..x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "", G = "......g.......g.", P = "" } },
        { "rnb", new Pat { K = "x......x.x......", S = "....x.......x...", C = "....x.......x...", H = "x.x.x.x.x.x.x.xx", O = "", G = "..........g.....", P = "" } },
        { "pop", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..............x.", G = "", P = "" } },
        { "phonk", new Pat { K = "x.....x...x.....", S = "....x.......x...", H = "xxxxxxxxxxxxxxxx", O = "", G = "", P = "x..x..x...x..x.." } },
        { "jersey", new Pat { K = "x..x..x.x...x...", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "" } },
        { "hyperpop", new Pat { K = "x.....x.x.......", S = "....x.......x...", H = "xxxxxxxxxxxxtxxx", O = "", G = "", P = "" } },
        { "grime", new Pat { K = "x.......x.x.....", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "......x......x.." } },
        { "dancehall", new Pat { K = "x..x..x.x..x..x.", S = "...x..x....x..x.", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "" } },
        { "baile", new Pat { K = "x..x...x..x.x...", S = "....x.......x..x", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "x.x..x.x.x..x.x." } },
        { "trance", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "xxxxxxxxxxxxxxxx", O = "..x...x...x...x.", G = "", P = "" } },
        { "futurebass", new Pat { K = "x.........x.....", S = "........x.......", C = "........x.......", H = "x.x.x.x.x.x.x.x.", O = "..............x.", G = "", P = "" } },
        { "disco", new Pat { K = "x...x...x...x...", C = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "..x...x...x...x.", G = "", P = "x.x.x.x.x.x.x.x." } },
        { "breaks", new Pat { K = "x.........x.x...", S = "....x.......x...", H = "x.x.x.x.x.x.x.x.", O = "", G = ".......g.g......", P = "" } },
        { "riddim", new Pat { K = "x.....x......x..", S = "........x.......", C = "........x.......", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "" } },
    };
    public static Pat Get(string k) { Pat p; return d.TryGetValue(k, out p) ? p : d["dnb_twostep"]; }
    public static bool Has(string k) { return d.ContainsKey(k); }
}

static class DrumKit
{
    // the set of one-shots for a style (several slightly different hits of each kind)
    public class Kit { public List<Buf> Kicks = new List<Buf>(), Snares = new List<Buf>(), Claps = new List<Buf>(), HatsC = new List<Buf>(), HatsO = new List<Buf>(), Perc = new List<Buf>(), Rides = new List<Buf>(); public List<string> KickNames = new List<string>(), SnareNames = new List<string>(), PercNames = new List<string>(); }

    public static Kit Make(DrumStyle s, int seed)
    {
        var r = new Rnd(seed); var k = new Kit(); string[] kn = { "Punch", "Tight", "Deep", "Hard", "Thump", "Clean" };
        // Punch, Tight, Deep, Hard, Thump, Clean: pitch, sweep, sweep speed, length, drive and click set per character (the genre's kick stays the centre)
        double[][] kc = { new[] { 1.0, 1.25, 0.8, 0.85, 1.2, 1.5 }, new[] { 1.1, 1.1, 0.7, 0.55, 1.0, 1.0 }, new[] { 0.84, 0.85, 1.25, 1.6, 0.9, 0.45 }, new[] { 1.02, 1.35, 0.85, 1.0, 1.7, 1.8 }, new[] { 0.92, 0.75, 1.4, 1.25, 1.1, 0.7 }, new[] { 1.0, 1.0, 1.0, 1.0, 0.6, 0.6 } };
        for (int i = 0; i < 6; i++)
        {
            var c = kc[i]; double f0 = s.KickF0 * c[0] * r.Range(0.97f, 1.03f), st = s.KickStart * c[1] * r.Range(0.95f, 1.05f), dec = s.KickDecay * c[3] * r.Range(0.93f, 1.07f), dr = 1 + (s.KickDrive - 1) * 0.3 * c[4];   // clean kicks: only a little saturation
            k.Kicks.Add(SoundGen.Kick(r, f0, st, s.KickTau * c[2], dec, dr, s.KickClick * c[5], s.KickLen * Math.Max(0.8, Math.Min(1.4, c[3])))); k.KickNames.Add(kn[i]);
        }
        string[] sn = { "Crack", "Snap", "Fat", "Tight", "Rim Crack", "Bright" };
        // Crack, Snap, Fat, Tight, Rim Crack, Bright: body pitch, noise colour, length, noise amount, snap and drive per character
        double[][] sc = { new[] { 1.1, 1.0, 0.8, 1.0, 1.7, 1.2 }, new[] { 1.0, 1.35, 0.7, 1.15, 1.3, 1.0 }, new[] { 0.82, 0.8, 1.6, 0.9, 0.7, 1.1 }, new[] { 1.05, 1.1, 0.5, 0.85, 1.1, 1.0 }, new[] { 1.3, 0.75, 0.6, 0.6, 2.0, 1.25 }, new[] { 1.0, 1.6, 1.0, 1.2, 1.0, 0.95 } };
        for (int i = 0; i < 6; i++)
        {
            var c = sc[i]; double body = s.SnareBody * c[0] * r.Range(0.97f, 1.03f), nz = s.SnareNoise * c[1], dec = s.SnareDecay * c[2] * r.Range(0.94f, 1.06f);
            k.Snares.Add(SoundGen.Snare(r, body, nz, dec, s.SnareAmt * c[3], s.SnareSnap * c[4], s.SnareDrive * c[5], s.SnareLen * Math.Max(0.8, Math.Min(1.5, c[2])))); k.SnareNames.Add(sn[i]);
        }
        // claps: tight and bright, wide and roomy, low and thick; closed hats: tight, crisp, dark, long; open hats: short and long
        double[][] cc = { new[] { 1.25, 0.6 }, new[] { 1.0, 1.6 }, new[] { 0.75, 1.0 } }; for (int i = 0; i < 3; i++) k.Claps.Add(SoundGen.Clap(r, s.ClapCentre * cc[i][0], s.ClapTail * cc[i][1], 0.5 * Math.Max(1, cc[i][1])));
        double[][] hc = { new[] { 0.6, 1.08, 1.1 }, new[] { 1.0, 1.15, 1.0 }, new[] { 1.1, 0.8, 0.85 }, new[] { 1.9, 0.95, 1.0 } }; for (int i = 0; i < 4; i++) k.HatsC.Add(SoundGen.Hat(r, s.HatC * hc[i][0], s.HatBright * hc[i][1], 0.2 * Math.Max(1, hc[i][0]), hc[i][2] * s.HatPitch, s.HatHp, s.HatMetal, s.HatSet));
        double[][] ho = { new[] { 0.65, 1.05, 1.05 }, new[] { 1.5, 0.92, 0.95 } }; for (int i = 0; i < 2; i++) k.HatsO.Add(SoundGen.Hat(r, s.HatO * ho[i][0], s.HatBright * ho[i][1], 0.8 * Math.Max(1, ho[i][0]), ho[i][2] * s.HatPitch, s.HatHp, s.HatMetal, s.HatSet));
        k.Rides.Add(SoundGen.Cymbal(r, 0.9 * s.CymDecay, 2.0, true, s.CymPitch, s.CymTone, s.CymSet)); k.Rides.Add(SoundGen.Cymbal(r, 1.4 * s.CymDecay, 2.6, false, s.CymPitch, s.CymTone, s.CymSet));
        k.Perc.Add(SoundGen.Tom(r, 110 * r.Range(0.9f, 1.2f), 0.28, 0.6)); k.PercNames.Add("Tom Low"); k.Perc.Add(SoundGen.Tom(r, 170 * r.Range(0.9f, 1.2f), 0.22, 0.5)); k.PercNames.Add("Tom Mid");
        k.Perc.Add(SoundGen.Shaker(r, 0.18, r.Range(0.85f, 1.15f))); k.PercNames.Add("Shaker"); k.Perc.Add(SoundGen.Rim(r, 1700 * r.Range(0.9f, 1.15f))); k.PercNames.Add("Rim"); k.Perc.Add(SoundGen.Conga(r, 330 * r.Range(0.9f, 1.2f))); k.PercNames.Add("Conga");
        k.Perc.Add(SoundGen.Cowbell(r, 540 * r.Range(0.95f, 1.1f), 0.22)); k.PercNames.Add("Bell");
        // every hit ends at silence: a kick or tom cut off while it still rings clicks each time it plays in a loop
        foreach (var l in new[] { k.Kicks, k.Snares, k.Claps, k.HatsC, k.HatsO, k.Perc, k.Rides }) foreach (var b in l) SoundGen.EndFade(b, 0.35, (int)(0.25 * SoundGen.SR));
        return k;
    }

    // a drum loop built from the kit; returns the full mix and the stems (kick, snare, hats, perc)
    public class Loop { public Buf Mix, Kick, Snare, Hats, Perc; }
    public static Loop Render(Kit kit, DrumStyle s, Pat p, double bpm, int bars, double swing, int seed, bool fillLastBar, double tailSec = 1.2)
    {
        var r = new Rnd(seed); double stepS = 60.0 / bpm / 4 * SoundGen.SR; int steps = bars * 16, total = (int)(steps * stepS + tailSec * SoundGen.SR);
        var L = new Loop { Mix = new Buf(total), Kick = new Buf(total), Snare = new Buf(total), Hats = new Buf(total), Perc = new Buf(total) };
        Func<int, int> at = st => (int)Math.Round(st * stepS + ((st % 2 == 1) ? (swing - 0.5) * 2 * stepS : 0) + r.Bi() * 0.0006 * SoundGen.SR);
        Action<Buf, Buf, int, float> put = (dst, src, pos, gain) => { dst.Add(src, pos, gain); };
        int kv = r.Int(kit.Kicks.Count), sv = r.Int(kit.Snares.Count), cv = r.Int(kit.Claps.Count), hv = r.Int(kit.HatsC.Count);
        for (int st = 0; st < steps; st++)
        {
            int bar = st / 16, sp = st % 16, pi = st % (p.Bars * 16); bool lastBar = fillLastBar && bar == bars - 1; int pos = at(st);
            char k = Ch(p.K, pi), sn = Ch(p.S, pi), c = Ch(p.C, pi), h = Ch(p.H, pi), o = Ch(p.O, pi), pc = Ch(p.P, pi), g = Ch(p.G, pi);
            if (k != '.') put(L.Kick, kit.Kicks[kv], pos, k == 'X' ? 1f : 0.92f + r.Next() * 0.08f);
            if (sn != '.') put(L.Snare, kit.Snares[sv], pos, 0.95f + r.Next() * 0.05f); if (c != '.') put(L.Snare, kit.Claps[cv], pos, 0.8f); if (g != '.') put(L.Snare, kit.Snares[(sv + 1) % kit.Snares.Count], pos, 0.22f + r.Next() * 0.12f);
            if (h != '.') { float v = (sp % 4 == 0 ? 0.9f : (sp % 2 == 0 ? 0.62f : 0.42f)) * (0.9f + r.Next() * 0.2f); if (h == 't') { for (int t = 0; t < 3; t++) put(L.Hats, kit.HatsC[(hv + t) % kit.HatsC.Count], pos + (int)(t * stepS / 3), v * 0.8f); } else put(L.Hats, kit.HatsC[(hv + st) % kit.HatsC.Count], pos, v); }
            if (o != '.') put(L.Hats, kit.HatsO[st % kit.HatsO.Count], pos, 0.7f);
            if (pc != '.' && kit.Perc.Count > 0) put(L.Perc, PercHit(kit, s, pc), pos, 0.5f + r.Next() * 0.2f);   // (the genre's own percussion: x, y and z are its first, second and third)
            if (lastBar && sp >= 8) { if (sp % 2 == 0 || sp >= 12) { float v = 0.5f + (sp - 8) / 8f * 0.5f; put(L.Snare, kit.Snares[sv], pos, v); if (sp >= 12) put(L.Perc, PercNamed(kit, Grooves.FillKind(s.Pattern)), pos, v * 0.7f); } }
        }
        L.Mix.Add(L.Kick, 0, 1f); L.Mix.Add(L.Snare, 0, 0.95f); L.Mix.Add(L.Hats, 0, 0.55f); L.Mix.Add(L.Perc, 0, 0.5f);
        double beats = bars * 4; L.Mix = PackBuilder.LoopLen(L.Mix, beats, bpm); L.Kick = PackBuilder.LoopLen(L.Kick, beats, bpm); L.Snare = PackBuilder.LoopLen(L.Snare, beats, bpm); L.Hats = PackBuilder.LoopLen(L.Hats, beats, bpm); L.Perc = PackBuilder.LoopLen(L.Perc, beats, bpm);
        return L;
    }
    static Buf PercNamed(Kit kit, string name) { int i = kit.PercNames.IndexOf(name); return kit.Perc[i >= 0 ? i : 0]; }
    static Buf PercHit(Kit kit, DrumStyle s, char c) { var kinds = Grooves.PercKinds(s.Pattern); return PercNamed(kit, kinds[(c == 'y' ? 1 : c == 'z' ? 2 : 0) % kinds.Length]); }
    static char Ch(string s, int i) { return string.IsNullOrEmpty(s) || i >= s.Length ? '.' : s[i]; }
}
