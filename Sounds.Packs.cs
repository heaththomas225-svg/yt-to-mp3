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

    // writes a sound as a 24-bit WAV and lists it
    public SoundItem Audio(Buf b, string folder, string name, string type, string group, string kind, double bpm, string key, string tags, string mood, double peakDb)
    {
        if (Cancelled) throw new OperationCanceledException();
        b = b.Copy(); b.Normalise(peakDb, kind == "loop" ? 0 : 24, kind == "loop" ? 0 : 160); string file = Clean(name) + ".wav", rel = folder + "/" + file; string path = Path.Combine(Pack.Dir, rel.Replace('/', Path.DirectorySeparatorChar));
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
    public double HatC = 0.045, HatO = 0.28, HatBright = 10, ClapCentre = 1200, ClapTail = 0.1, Swing = 0.5; public string Pattern = "dnb_twostep"; public double SnareBodyMul = 1;
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
        { "riddim", new Pat { K = "x.....x......x..", S = "........x.......", C = "........x.......", H = "x.x.x.x.x.x.x.x.", O = "", G = "", P = "" } },
    };
    public static Pat Get(string k) { Pat p; return d.TryGetValue(k, out p) ? p : d["dnb_twostep"]; }
}

static class DrumKit
{
    // the set of one-shots for a style (several slightly different hits of each kind)
    public class Kit { public List<Buf> Kicks = new List<Buf>(), Snares = new List<Buf>(), Claps = new List<Buf>(), HatsC = new List<Buf>(), HatsO = new List<Buf>(), Perc = new List<Buf>(), Rides = new List<Buf>(); public List<string> KickNames = new List<string>(), SnareNames = new List<string>(), PercNames = new List<string>(); }

    public static Kit Make(DrumStyle s, int seed)
    {
        var r = new Rnd(seed); var k = new Kit(); string[] kn = { "Punch", "Tight", "Deep", "Hard", "Thump", "Clean" };
        for (int i = 0; i < 6; i++)
        {
            double f0 = s.KickF0 * r.Range(0.9f, 1.12f), st = s.KickStart * r.Range(0.8f, 1.25f), dec = s.KickDecay * r.Range(0.7f, 1.4f), dr = s.KickDrive * r.Range(0.8f, 1.4f);
            k.Kicks.Add(SoundGen.Kick(r, f0, st, s.KickTau * r.Range(0.8f, 1.3f), dec, dr, s.KickClick * r.Range(0.5f, 1.4f), s.KickLen)); k.KickNames.Add(kn[i]);
        }
        string[] sn = { "Crack", "Snap", "Fat", "Tight", "Rim Crack", "Bright" };
        for (int i = 0; i < 6; i++)
        {
            double body = s.SnareBody * r.Range(0.85f, 1.2f), nz = s.SnareNoise * r.Range(0.8f, 1.3f), dec = s.SnareDecay * r.Range(0.7f, 1.5f);
            k.Snares.Add(SoundGen.Snare(r, body, nz, dec, s.SnareAmt * r.Range(0.8f, 1.2f), s.SnareSnap * r.Range(0.6f, 1.6f), s.SnareDrive * r.Range(0.9f, 1.4f), s.SnareLen)); k.SnareNames.Add(sn[i]);
        }
        for (int i = 0; i < 3; i++) k.Claps.Add(SoundGen.Clap(r, s.ClapCentre * r.Range(0.85f, 1.2f), s.ClapTail * r.Range(0.7f, 1.4f), 0.5));
        for (int i = 0; i < 4; i++) k.HatsC.Add(SoundGen.Hat(r, s.HatC * r.Range(0.7f, 1.5f), s.HatBright * r.Range(0.85f, 1.1f), 0.2, r.Range(0.9f, 1.15f)));
        for (int i = 0; i < 2; i++) k.HatsO.Add(SoundGen.Hat(r, s.HatO * r.Range(0.8f, 1.3f), s.HatBright * r.Range(0.85f, 1.1f), 0.8, r.Range(0.9f, 1.15f)));
        k.Rides.Add(SoundGen.Cymbal(r, 0.9, 2.0, true)); k.Rides.Add(SoundGen.Cymbal(r, 1.4, 2.6, false));
        k.Perc.Add(SoundGen.Tom(r, 110 * r.Range(0.9f, 1.2f), 0.28, 0.6)); k.PercNames.Add("Tom Low"); k.Perc.Add(SoundGen.Tom(r, 170 * r.Range(0.9f, 1.2f), 0.22, 0.5)); k.PercNames.Add("Tom Mid");
        k.Perc.Add(SoundGen.Shaker(r, 0.18, r.Range(0.85f, 1.15f))); k.PercNames.Add("Shaker"); k.Perc.Add(SoundGen.Rim(r, 1700 * r.Range(0.9f, 1.15f))); k.PercNames.Add("Rim"); k.Perc.Add(SoundGen.Conga(r, 330 * r.Range(0.9f, 1.2f))); k.PercNames.Add("Conga");
        k.Perc.Add(SoundGen.Cowbell(r, 540 * r.Range(0.95f, 1.1f), 0.22)); k.PercNames.Add("Bell");
        return k;
    }

    // a drum loop built from the kit; returns the full mix and the stems (kick, snare, hats, perc)
    public class Loop { public Buf Mix, Kick, Snare, Hats, Perc; }
    public static Loop Render(Kit kit, DrumStyle s, Pat p, double bpm, int bars, double swing, int seed, bool fillLastBar, double tailSec = 0.5)
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
            if (pc != '.' && kit.Perc.Count > 0) put(L.Perc, kit.Perc[(st / 2 + sv) % kit.Perc.Count], pos, 0.5f + r.Next() * 0.2f);
            if (lastBar && sp >= 8) { if (sp % 2 == 0 || sp >= 12) { float v = 0.5f + (sp - 8) / 8f * 0.5f; put(L.Snare, kit.Snares[sv], pos, v); if (sp >= 12) put(L.Perc, kit.Perc[0], pos, v * 0.7f); } }
        }
        L.Mix.Add(L.Kick, 0, 1f); L.Mix.Add(L.Snare, 0, 0.95f); L.Mix.Add(L.Hats, 0, 0.55f); L.Mix.Add(L.Perc, 0, 0.5f); SoundGen.Glue(L.Mix, 1.25f);
        return L;
    }
    static char Ch(string s, int i) { return string.IsNullOrEmpty(s) || i >= s.Length ? '.' : s[i]; }
}
