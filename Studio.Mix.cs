using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;

// ======================================================================
//  Studio logic: presets, key detection, Auto Mix, project files, export.
//  No UI in here, so it can be tested headless.
// ======================================================================

static class Presets
{
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string Root { get { return Path.Combine(StudioEnv.DataDir, "presets"); } }
    static string ChainDir { get { return Path.Combine(StudioEnv.DataDir, "chains"); } }

    // ---------- single-plugin presets (built in) ----------
    static readonly Dictionary<string, string[]> Builtin = new Dictionary<string, string[]> {
        { "eq", new[] {
            "Vocal Clarity|hp_on=1;hp_f=90;hp_s=1;b1_f=320;b1_g=-2.5;b1_q=1.2;b2_f=900;b2_g=-1;b3_f=3200;b3_g=2.5;b3_q=0.9;b4_f=6500;b4_g=1.5;b5_f=11000;b5_g=2.5",
            "Warm|b0_f=120;b0_g=2;b1_f=250;b1_g=1;b4_f=6000;b4_g=-1.5;b5_f=9000;b5_g=-2;lp_on=1;lp_f=15000",
            "Bright|b3_f=4000;b3_g=2;b3_q=0.8;b4_f=7500;b4_g=2.5;b4_q=0.8;b5_f=11000;b5_g=4",
            "Bass Control|hp_on=1;hp_f=30;hp_s=1;b0_f=80;b0_g=-1.5;b1_f=250;b1_g=-3;b1_q=1.4",
            "Clean Mix|hp_on=1;hp_f=35;b1_f=350;b1_g=-2;b1_q=1;b3_f=3000;b3_g=1;b5_f=12000;b5_g=1.5" } },
        { "comp", new[] {
            "Vocal gentle|thr=-22;ratio=2.5;att=15;rel=140;knee=8;makeup=3",
            "Vocal punchy|thr=-26;ratio=4;att=6;rel=80;knee=6;makeup=6",
            "Glue (bus)|thr=-16;ratio=2;att=30;rel=200;knee=10;makeup=1.5",
            "Beat control|thr=-18;ratio=3;att=25;rel=160;knee=8;makeup=2" } },
        { "mbcomp", new[] {
            "Gentle master|t0=-20;t1=-22;t2=-24;r0=2;r1=2;r2=1.8",
            "Tighten lows|t0=-26;r0=3.5;t1=-22;r1=2;t2=-24;r2=2",
            "Smooth highs|t0=-20;r0=1.5;t1=-22;r1=2;t2=-28;r2=3.5" } },
        { "exciter", new[] {
            "Air|drive=6;harm=40;flo=6000;fhi=18000;mix=35",
            "Presence|drive=10;harm=55;flo=2500;fhi=9000;mix=30",
            "Subtle|drive=5;harm=25;flo=3000;fhi=16000;mix=20",
            "Aggressive|drive=16;harm=80;flo=3000;fhi=16000;mix=55" } },
        { "sat", new[] { "Tape warmth|mode=0;drive=6;mix=60", "Tube|mode=1;drive=8;mix=70", "Crunch|mode=2;drive=14;mix=80" } },
        { "dist", new[] { "Light grit|mode=0;drive=10;mix=30;level=-6", "Fuzz|mode=2;drive=24;mix=50;level=-8", "Foldback|mode=1;drive=18;mix=40;level=-8" } },
        { "reverb", new[] {
            "Small room|size=25;damp=60;pre=8;mix=10", "Plate|size=55;damp=40;pre=20;mix=18",
            "Hall|size=80;damp=45;pre=35;mix=24", "Dark vocal|size=60;damp=75;lc=250;hc=5000;mix=18" } },
        { "delay", new[] {
            "Slap|mode=0;sync=0;time=90;fb=10;mix=15", "1/8 ping-pong|mode=2;sync=2;fb=35;mix=20", "Dotted echo|mode=2;sync=3;fb=40;mix=22" } },
        { "chorus", new[] { "Wide double|rate=0.6;depth=4;delay=14;spread=80;mix=35", "Subtle thickener|rate=0.4;depth=2;delay=12;mix=20" } },
        { "deess", new[] { "Gentle|freq=6500;thr=-24;range=6", "Strong|freq=7000;thr=-30;range=12" } },
        { "widener", new[] { "Wide|width=140;bass=120", "Subtle|width=115;bass=120", "Mono-safe|width=100;bass=150" } },
        { "limiter", new[] { "Mastering -1 dB|ceil=-1;gain=3;rel=80", "Loud|ceil=-0.8;gain=7;rel=50", "Gentle|ceil=-1.5;gain=0;rel=120" } },
        { "duck", new[] { "Subtle 2 dB|amount=2", "Standard 4 dB|amount=4", "Heavy 7 dB|amount=7;band=0" } },
        { "8d", new[] { "Classic 8D (10 s)|period=10;dir=0;width=100;depth=60;itd=80;room=15", "Slow orbit (20 s)|period=20;depth=70;room=20", "Fast spin (4 s)|period=4;depth=40;room=10", "Wide sweep (back and forth)|dir=2;period=12;width=100;depth=50", "Subtle width|period=16;width=40;depth=30;itd=50;room=10", "Hold: front|hold=1;angle=0", "Hold: right|hold=1;angle=90", "Hold: behind|hold=1;angle=180", "Hold: left|hold=1;angle=270" } },
        { "pitch", new[] {
            "Natural|speed=80;strength=70;natural=70", "Tight|speed=25;strength=100;natural=25",
            "Hard Auto-Tune|speed=0;strength=100;natural=0", "Subtle polish|speed=120;strength=40;natural=80" } },
        { "denoise", new[] { "Light|amount=8;sens=1.4", "Medium|amount=14;sens=1.8", "Heavy|amount=22;sens=2.4" } },
        { "bassboost", new[] {
            "Clean Low End (all-round)|freq=80;gain=5;q=0.7;harm=10;tight=25;mix=100;level=-2",
            "Subtle Lift|freq=100;gain=2.5;q=0.6;harm=5;tight=30;mix=100;level=-1",
            "Warm & Round|freq=110;gain=4;q=0.5;harm=35;tight=30;mix=100;level=-1.5",
            "Punchy Kick|freq=90;gain=6;q=1;harm=15;tight=28;mix=100;level=-2.5",
            "Bass Guitar Body|freq=100;gain=4;q=0.8;harm=20;tight=40;mix=100;level=-1.5",
            "Club Thump|freq=60;gain=8;q=0.9;harm=25;tight=24;mix=100;level=-3.5",
            "Deep 808 Sub|freq=45;gain=9;q=0.8;harm=20;tight=18;mix=100;level=-4",
            "Heavy Trap|freq=50;gain=11;q=0.9;harm=30;tight=20;mix=100;level=-5.5",
            "Boom (blended in)|freq=55;gain=14;q=0.7;harm=25;tight=22;mix=45;level=-3",
            "Small Speakers (adds harmonics)|freq=70;gain=3;q=0.7;harm=70;tight=35;mix=100;level=-1" } },
        { "gate", new[] { "Vocal gate|thr=-52;range=30;att=2;hold=80;rel=200", "Tight gate|thr=-40;range=50;att=1;hold=30;rel=120" } },
    };

    public static List<KeyValuePair<string, string>> For(string type)
    {
        var l = new List<KeyValuePair<string, string>>();
        string[] arr; if (Builtin.TryGetValue(type, out arr))
            foreach (var s in arr) { int i = s.IndexOf('|'); l.Add(new KeyValuePair<string, string>(s.Substring(0, i), s.Substring(i + 1))); }
        string[] more; if (PresetsExtra.Data.TryGetValue(type, out more)) foreach (var s in more) { int i = s.IndexOf('|'); l.Add(new KeyValuePair<string, string>(s.Substring(0, i), s.Substring(i + 1))); }
        return l;
    }

    public static List<KeyValuePair<string, string>> User(string type)
    {
        var l = new List<KeyValuePair<string, string>>();
        try
        {
            string d = Path.Combine(Root, type);
            if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d, "*.txt")) l.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)));
        }
        catch { }
        return l;
    }

    public static void SaveUser(string type, string name, Fx fx)
    {
        string d = Path.Combine(Root, type); Directory.CreateDirectory(d);
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        File.WriteAllText(Path.Combine(d, name + ".txt"), fx.Serialize());
    }

    public static void DeleteUser(string type, string name) { try { File.Delete(Path.Combine(Root, type, name + ".txt")); } catch { } }

    public static void Apply(Fx fx, string data) { var e = StudioEngine.Current; object gate = e != null ? e.Sync : new object(); lock (gate) { fx.ResetDefaults(); fx.Deserialize(data); fx.Reset(); } }

    public static Fx Make(string type, string data)
    {
        var f = FxRegistry.Create(type); if (f == null) return null; f.Prepare(StudioEngine.SR); if (!string.IsNullOrEmpty(data)) f.Deserialize(data); return f;
    }

    // ---------- vocal chain presets (original sound-design, broad style inspirations only) ----------
    public static readonly string[] ChainNames = { "Melodic Rap", "Emotional Rap", "Technical Rap", "Aggressive Rap", "Clean Studio", "Wide Atmospheric" };
    public static readonly string[] ChainBlurbs = {
        "Tuned, airy and smooth: tight Auto-Tune, bright EQ, tape saturation, short echo.",
        "Warm and intimate: softer tuning, tube warmth, longer reverb and a 1/4 echo.",
        "Clear and forward for fast delivery: fast compression, strong de-essing, dry space.",
        "Dense and in-your-face: hard compression, saturation, exciter, short room.",
        "Neutral polished starting point: gentle tuning, light compression, small room.",
        "Big and spacious: stereo chorus, wide imager, long reverb and ping-pong delay." };

    // ---------- bus presets: an echoey bus, an ad-lib bus and a reverb bus (they work on any track, and are made for buses) ----------
    public static readonly string[] BusNames = { "Echo bus", "AdLib bus", "Reverb bus" };
    public static readonly string[] BusBlurbs = { "Echoey: a dotted-eighth ping-pong echo and a little room on everything sent to it.", "AdLib: bright, forward ad-libs with a short slap echo and a touch of space.", "Reverb: a big smooth room around everything sent to it." };
    public static List<Fx> BuildBus(string name)
    {
        var c = new List<Fx>();
        switch (name)
        {
            case "Echo bus":
                c.Add(Make("eq", "hp_on=1;hp_f=180;hp_s=1;lp_on=1;lp_f=7500")); c.Add(Make("delay", "mode=2;sync=3;fb=48;hc=4200;mix=45")); c.Add(Make("reverb", "size=35;damp=60;pre=15;mix=14")); break;
            case "AdLib bus":
                c.Add(Make("eq", "hp_on=1;hp_f=260;hp_s=1;b3_f=4200;b3_g=3;b3_q=0.9;b5_f=11000;b5_g=2")); c.Add(Make("comp", "thr=-20;ratio=3;att=6;rel=90;knee=6;makeup=3")); c.Add(Make("delay", "mode=2;sync=2;fb=26;hc=4800;mix=32"));
                c.Add(Make("reverb", "size=45;damp=50;pre=22;mix=22")); c.Add(Make("exciter", "drive=6;harm=35;flo=5500;fhi=16000;mix=20")); break;
            case "Reverb bus":
                c.Add(Make("eq", "hp_on=1;hp_f=220;hp_s=1;lp_on=1;lp_f=9500")); c.Add(Make("reverb", "size=82;damp=45;pre=30;mix=45")); break;
        }
        return c;
    }

    // Original presets inspired by broad styles (melodic trap, emotional rap, technical rap...). They are not
    // copies of any artist's settings or of proprietary plugins.
    public static List<Fx> BuildChain(string name)
    {
        var c = new List<Fx>();
        switch (name)
        {
            case "Melodic Rap":
                c.Add(Make("pitch", "speed=18;strength=90;natural=25"));
                c.Add(Make("eq", "hp_on=1;hp_f=90;hp_s=1;b1_f=300;b1_g=-2.5;b1_q=1.2;b3_f=3200;b3_g=2.5;b3_q=0.9;b5_f=10500;b5_g=2.5"));
                c.Add(Make("comp", "thr=-22;ratio=3.5;att=8;rel=90;knee=6;makeup=4"));
                c.Add(Make("deess", "freq=6800;thr=-30;range=8"));
                c.Add(Make("sat", "mode=0;drive=5;mix=40"));
                c.Add(Make("exciter", "drive=8;harm=40;flo=5000;fhi=16000;mix=25"));
                c.Add(Make("reverb", "size=40;damp=55;pre=25;mix=12"));
                c.Add(Make("delay", "mode=2;sync=2;fb=28;hc=4500;mix=14")); break;
            case "Emotional Rap":
                c.Add(Make("pitch", "speed=35;strength=70;natural=55"));
                c.Add(Make("eq", "hp_on=1;hp_f=85;hp_s=1;b0_f=120;b0_g=1.5;b1_f=300;b1_g=-1;b3_f=3500;b3_g=1.5;b5_f=10000;b5_g=1.5"));
                c.Add(Make("comp", "thr=-20;ratio=2.8;att=14;rel=140;knee=8;makeup=3"));
                c.Add(Make("deess", "freq=6500;thr=-28;range=8"));
                c.Add(Make("sat", "mode=1;drive=6;mix=50"));
                c.Add(Make("reverb", "size=62;damp=50;pre=35;mix=20"));
                c.Add(Make("delay", "mode=2;sync=1;fb=35;hc=4000;mix=16")); break;
            case "Technical Rap":
                c.Add(Make("pitch", "speed=60;strength=30;natural=80")); c[0].Bypass = true;
                c.Add(Make("eq", "hp_on=1;hp_f=100;hp_s=1;b1_f=250;b1_g=-2;b2_f=1000;b2_g=0.5;b3_f=4000;b3_g=3;b3_q=0.9"));
                c.Add(Make("comp", "thr=-24;ratio=4;att=4;rel=70;knee=5;makeup=5"));
                c.Add(Make("deess", "freq=7000;thr=-32;range=10"));
                c.Add(Make("exciter", "drive=6;harm=30;flo=4000;fhi=16000;mix=30"));
                c.Add(Make("reverb", "size=20;damp=65;pre=5;mix=6")); break;
            case "Aggressive Rap":
                c.Add(Make("pitch", "speed=40;strength=40;natural=70")); c[0].Bypass = true;
                c.Add(Make("eq", "hp_on=1;hp_f=110;hp_s=1;b1_f=400;b1_g=-2;b2_f=1500;b2_g=1.5;b3_f=3000;b3_g=3;b4_f=6000;b4_g=1.5"));
                c.Add(Make("comp", "thr=-26;ratio=6;att=3;rel=60;knee=4;makeup=7"));
                c.Add(Make("deess", "freq=7000;thr=-30;range=9"));
                c.Add(Make("sat", "mode=0;drive=12;mix=70"));
                c.Add(Make("exciter", "drive=12;harm=55;flo=3000;fhi=15000;mix=35"));
                c.Add(Make("reverb", "size=28;damp=60;pre=8;mix=8"));
                c.Add(Make("delay", "mode=1;sync=5;fb=15;hc=5000;mix=8")); break;
            case "Clean Studio":
                c.Add(Make("pitch", "speed=60;strength=40;natural=70")); c[0].Bypass = true;
                c.Add(Make("eq", "hp_on=1;hp_f=80;hp_s=1;b1_f=300;b1_g=-1.5;b3_f=4000;b3_g=1.5;b5_f=10000;b5_g=1"));
                c.Add(Make("comp", "thr=-20;ratio=2.5;att=15;rel=150;knee=8;makeup=3"));
                c.Add(Make("deess", "freq=6500;thr=-27;range=7"));
                c.Add(Make("reverb", "size=35;damp=55;pre=15;mix=10")); break;
            default:   // Wide Atmospheric
                c.Add(Make("pitch", "speed=22;strength=80;natural=35"));
                c.Add(Make("eq", "hp_on=1;hp_f=100;hp_s=1;b1_f=250;b1_g=-2;b3_f=5000;b3_g=2;b5_f=12000;b5_g=3"));
                c.Add(Make("comp", "thr=-22;ratio=3;att=10;rel=110;knee=6;makeup=4"));
                c.Add(Make("deess", "freq=6800;thr=-29;range=8"));
                c.Add(Make("chorus", "rate=0.5;depth=4;delay=14;spread=85;mix=20"));
                c.Add(Make("widener", "width=140;bass=150"));
                c.Add(Make("reverb", "size=80;damp=40;pre=40;mix=30"));
                c.Add(Make("delay", "mode=2;sync=3;fb=45;hc=4500;mix=25")); break;
        }
        return c;
    }

    // Instrumental chain with its own EQ / comp / multiband / saturation / exciter / imaging / limiter + vocal ducking.
    public static List<Fx> BeatChain()
    {
        var c = new List<Fx>();
        c.Add(Make("eq", "hp_on=1;hp_f=25;hp_s=0;b1_f=300;b1_g=0;b3_f=2500;b3_g=-1.5;b3_q=0.8"));
        c.Add(Make("comp", "thr=-18;ratio=2.2;att=25;rel=160;knee=8;makeup=1.5"));
        c.Add(Make("mbcomp", "")); c[c.Count - 1].Bypass = true;
        c.Add(Make("sat", "mode=0;drive=3;mix=40")); c[c.Count - 1].Bypass = true;
        c.Add(Make("exciter", "drive=5;harm=25;mix=15")); c[c.Count - 1].Bypass = true;
        c.Add(Make("widener", "width=110;bass=120"));
        c.Add(Make("duck", "amount=3;thr=-32;att=12;rel=220;band=1"));
        c.Add(Make("limiter", "ceil=-1.5;gain=0;rel=100"));
        return c;
    }

    // ---------- user chains (saveable, editable) ----------
    public static List<string> UserChains()
    {
        var l = new List<string>(); try { if (Directory.Exists(ChainDir)) foreach (var f in Directory.GetFiles(ChainDir, "*.chain")) l.Add(Path.GetFileNameWithoutExtension(f)); } catch { }
        return l;
    }
    public static void SaveChain(string name, List<Fx> chain)
    {
        Directory.CreateDirectory(ChainDir); foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
        File.WriteAllText(Path.Combine(ChainDir, name + ".chain"), ChainText(chain));
    }
    // a chain as text: one line per plugin,  type|bypass|settings  (what is saved to a file and what is shared with the community)
    public static string ChainText(List<Fx> chain) { var sb = new StringBuilder(); foreach (var f in chain) sb.Append(f.Type).Append('|').Append(f.Bypass ? 1 : 0).Append('|').Append(f.Serialize()).Append('\n'); return sb.ToString(); }
    public static List<Fx> LoadChain(string name) { return ChainFromText(File.ReadAllText(Path.Combine(ChainDir, name + ".chain"))); }
    public static List<Fx> ChainFromText(string text)
    {
        var c = new List<Fx>();
        foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
        {
            if (c.Count >= 24) break;
            var p = line.Split(new[] { '|' }, 3); if (p.Length < 3) continue;
            var f = Make(p[0], p[2]); if (f == null) continue; f.Bypass = p[1] == "1"; c.Add(f);
        }
        return c;
    }
    public static void DeleteChain(string name) { try { File.Delete(Path.Combine(ChainDir, name + ".chain")); } catch { } }

    // ---------- mastering presets (applied to the master chain; [name, target LUFS, per-plugin params]) ----------
    public static readonly string[] MasterNames = Array.ConvertAll(MasterPresets.All, p => p.Name);
    public static readonly float[] MasterTargets = Array.ConvertAll(MasterPresets.All, p => p.Target);
    public static void ApplyMaster(StudioEngine eng, int i)
    {
        string[][] d = new string[MasterPresets.All.Length][]; for (int q = 0; q < d.Length; q++) d[q] = MasterPresets.All[q].Slots;
        // take the engine lock like RestoreChain: the audio thread must never see a half-reset chain (a mid-block Limiter.Reset crashed it and left it bypassed)
        lock (eng.Sync)
            for (int k = 0; k < eng.MasterFx.Count && k < 7; k++)
            {
                var f = eng.MasterFx[k]; f.ResetDefaults();
                if (k == 0) f.Set("b1_g", 0);
                if (d[i][k] == null) { f.Bypass = true; }
                else { f.Bypass = false; f.Deserialize(d[i][k]); }
                f.Error = ""; f.Reset();
            }
    }
}

// ---------------- key detection (pitch histogram + Krumhansl-Kessler profiles) ----------------
static class KeyDetect
{
    static readonly double[] Maj = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
    static readonly double[] Min = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

    public static bool Detect(AudioClip c, out int key, out int scale, out double conf)
    {
        key = 0; scale = 1; conf = 0; if (c == null || c.Length < 44100) return false;
        var pc = new PitchCorrector(); pc.Prepare(StudioEngine.SR); pc.Set("mask", 4095); pc.Set("strength", 0); pc.Set("range", 3); pc.Reset();
        double[] hist = new double[12]; int blocks = 0, voiced = 0; int n = Math.Min(c.Length, StudioEngine.SR * 150);
        var l = new float[512]; var r = new float[512];
        for (int i = 0; i + 512 <= n; i += 512)
        {
            Array.Copy(c.L, i, l, 0, 512); Array.Copy(c.R, i, r, 0, 512); pc.Process(l, r, 512); blocks++;
            if (pc.Voiced && blocks > 12 && pc.DetHz > 50) { double midi = 69 + 12 * Math.Log(pc.DetHz / 440.0) / Math.Log(2); int p = ((int)Math.Round(midi) % 12 + 12) % 12; hist[p] += 1; voiced++; }
        }
        if (voiced < 40) return false;
        double best = -2;
        for (int k = 0; k < 12; k++)
            for (int s = 0; s < 2; s++)
            {
                var prof = s == 0 ? Maj : Min; double[] rot = new double[12]; for (int i = 0; i < 12; i++) rot[i] = prof[(i - k + 12) % 12];
                double r2 = Corr(hist, rot); if (r2 > best) { best = r2; key = k; scale = s == 0 ? 1 : 2; }
            }
        conf = best; return true;
    }

    static double Corr(double[] a, double[] b)
    {
        double ma = 0, mb = 0; for (int i = 0; i < 12; i++) { ma += a[i]; mb += b[i]; } ma /= 12; mb /= 12; double sab = 0, sa = 0, sb = 0;
        for (int i = 0; i < 12; i++) { sab += (a[i] - ma) * (b[i] - mb); sa += (a[i] - ma) * (a[i] - ma); sb += (b[i] - mb) * (b[i] - mb); }
        return sa * sb < 1e-12 ? 0 : sab / Math.Sqrt(sa * sb);
    }
}

// ---------------- analysis used by Auto Mix ----------------
class TrackStats
{
    public LoudnessResult Loud; public double[] BandDb = new double[11]; public double NoiseFloorDb; public int Clips; public double SibilanceDb; public double ActiveFrac;
}

static class AutoMix
{
    public static readonly string[] Styles = { "Natural", "Clean Studio", "Melodic Rap", "Aggressive Rap", "Technical Rap" };
    static readonly double[] VocalOffset = { 2.0, 3.0, 2.5, 4.0, 3.5 };            // how far (LU) the vocal sits above the beat
    static readonly double[] DuckAmount = { 3, 3.5, 3, 2, 4 };
    static readonly double[] Pocket = { -1.5, -2, -1.5, -1, -3 };                   // beat cut at 2.5 kHz for the vocal
    static readonly double[] BandF = { 100, 160, 250, 400, 630, 1000, 1600, 2500, 4000, 6300, 10000 };
    static readonly double[] VocalTarget = { -8, -2, 3, 3, 1.5, 0, -2.5, -4.5, -7, -10, -16 };    // dB relative to the 1 kHz band
    static readonly double[] BeatTarget = { 4, 4, 2.5, 0.5, -1, -2, -3.5, -5, -7, -9.5, -14 };
    public static string LastSnapshot;

    // the EQ moves that bring a track (or the whole mix) closer to a balanced tone for what it is: bass, mud, presence, upper-mids, air (dB, at most 4 either way; nothing when it is close enough)
    internal static double[] ToneGains(TrackStats st, string role, out double[] deviation)
    {
        double[] T = role == "vocal" ? VocalTarget : (role == "beat" ? BeatTarget : new double[] { 5, 4.5, 3.5, 2, 0.5, 0, -1.5, -3, -5, -8, -13 }); var dev = new double[11]; for (int b = 0; b < 11; b++) dev[b] = st.BandDb[b] - T[b];
        deviation = new[] { (dev[0] + dev[1]) / 2, (dev[2] + dev[3]) / 2, (dev[7] + dev[8]) / 2, dev[9], dev[10] }; var g = new double[5];
        for (int i = 0; i < 5; i++) { double d = deviation[i]; g[i] = Math.Abs(d) < 1.5 ? 0 : Math.Max(-4, Math.Min(4, -0.55 * (d - Math.Sign(d) * 1.5))); }
        return g;
    }

    public static TrackStats Analyze(AudioClip c)
    {
        var st = new TrackStats(); int sr = StudioEngine.SR; int len = Math.Min(c.Length, sr * 180);
        st.Loud = Loudness.Measure(c.L, c.R, sr, 0, len);
        for (int i = 0; i < c.Length; i++) if (Math.Abs(c.L[i]) >= 0.999f || Math.Abs(c.R[i]) >= 0.999f) st.Clips++;
        // 50 ms RMS: noise floor (10th percentile of the active region) and activity
        int win = sr / 20, nw = len / win; var rms = new List<double>();
        for (int w = 0; w < nw; w++) { double s = 0; for (int i = w * win; i < (w + 1) * win; i++) s += 0.5 * (c.L[i] * c.L[i] + c.R[i] * c.R[i]); rms.Add(10 * Math.Log10(s / win + 1e-12)); }
        var sorted = new List<double>(rms); sorted.Sort();
        st.NoiseFloorDb = sorted.Count > 20 ? sorted[sorted.Count / 10] : -90;
        int act = 0; foreach (var r in rms) if (r > -45) act++; st.ActiveFrac = rms.Count == 0 ? 0 : act / (double)rms.Count;
        // average spectrum of active frames
        int N = 4096; var re = new double[N]; var im = new double[N]; double[] pw = new double[N / 2]; int frames = 0;
        for (int s = 0; s + N <= len; s += 4096)
        {
            double e = 0; for (int i = 0; i < N; i += 8) e += c.L[s + i] * c.L[s + i]; if (10 * Math.Log10(e / (N / 8) + 1e-12) < -45) continue;
            for (int i = 0; i < N; i++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N); re[i] = (c.L[s + i] + c.R[s + i]) * 0.5 * w; im[i] = 0; }
            Fft.Transform(re, im, false); for (int k = 0; k < N / 2; k++) pw[k] += re[k] * re[k] + im[k] * im[k]; frames++;
        }
        if (frames > 0)
        {
            for (int b = 0; b < 11; b++)
            {
                double lo = BandF[b] / Math.Pow(2, 1 / 6.0), hi = BandF[b] * Math.Pow(2, 1 / 6.0), sum = 0;
                for (int k = 1; k < N / 2; k++) { double f = k * (double)sr / N; if (f >= lo && f < hi) sum += pw[k]; }
                st.BandDb[b] = 10 * Math.Log10(sum / frames + 1e-12);
            }
            double ref1k = st.BandDb[5]; for (int b = 0; b < 11; b++) st.BandDb[b] -= ref1k;
            st.SibilanceDb = st.BandDb[9] - st.BandDb[8];
        }
        return st;
    }

    // ---------- project snapshot for "Undo Auto Mix" ----------
    public static string Snapshot(StudioEngine eng) { return ProjectIO.SerializeMix(eng); }

    static Fx Find(List<Fx> l, string t) { foreach (var f in l) if (f.Type == t) return f; return null; }
    static void Nudge(Fx eq, int band, double dg) { if (eq == null) return; float g = eq.Get("b" + band + "_g"); eq.Set("b" + band + "_g", (float)(g + dg)); }

    public static List<string> Run(StudioEngine eng, string style, bool enhance, Action<string> progress)
    {
        var rep = new List<string>(); int si = Math.Max(0, Array.IndexOf(Styles, style));
        Track voc = null, beat = null; foreach (var t in eng.Tracks) { if (t.Clip == null) continue; if (t.Role == "vocal" && voc == null) voc = t; else if (t.Role == "beat" && beat == null) beat = t; }
        bool fullSong = false;
        if (voc == null && beat == null) foreach (var t in eng.Tracks) if (t.Clip != null && !t.Instrument && (t.Role == "song" || t.Role == "other")) { beat = t; fullSong = true; break; }
        if (voc == null && beat == null) { rep.Add("Nothing to mix: add a song, a vocal or a beat first."); return rep; }
        LastSnapshot = Snapshot(eng);
        if (fullSong) rep.Add("This is a full song that has not been split, so it is mixed as ONE track (balance, tone and level only). For the best result press SPLIT VOCALS + BEAT first, then Auto Mix balances the vocal and the beat against each other.");
        rep.Add("Style: " + style + (enhance ? " + vocal enhancement" : ""));
        rep.Add("Original audio files are never modified; this only sets plugins and faders (use Undo Auto Mix to revert).");

        TrackStats vs = null, bs = null;
        if (voc != null) { progress("Analyzing vocals..."); vs = Analyze(voc.Clip); }
        if (beat != null) { progress("Analyzing instrumental..."); bs = Analyze(beat.Clip); }

        // ---- clipping ----
        foreach (var pair in new[] { new KeyValuePair<Track, TrackStats>(voc, vs), new KeyValuePair<Track, TrackStats>(beat, bs) })
        {
            if (pair.Key == null) continue;
            if (pair.Value.Clips > 8) rep.Add("Clipping: " + pair.Key.Name + " has " + pair.Value.Clips + " clipped samples (true peak " + pair.Value.Loud.TruePeakDb.ToString("0.0") + " dB). Clipping in the source can't be repaired; fader was set lower to leave headroom.");
            else rep.Add(pair.Key.Name + ": no clipping (peak " + pair.Value.Loud.PeakDb.ToString("0.0") + " dB, " + pair.Value.Loud.I.ToString("0.0") + " LUFS, range " + pair.Value.Loud.LRA.ToString("0.0") + " LU)");
        }

        // ---- vocal chain ----
        if (voc != null)
        {
            progress("Building vocal chain...");
            string[] chainFor = { "Clean Studio", "Clean Studio", "Melodic Rap", "Aggressive Rap", "Technical Rap" };
            var chain = Presets.BuildChain(chainFor[si]);
            var pitch = Find(chain, "pitch"); var eq = Find(chain, "eq"); var comp = Find(chain, "comp"); var de = Find(chain, "deess");
            rep.Add("Vocal chain: " + chainFor[si] + " preset (" + chain.Count + " plugins), all editable afterwards.");
            if (vs.NoiseFloorDb > -55)
            {
                var dn = Presets.Make("denoise", vs.NoiseFloorDb > -45 ? "amount=16;sens=2" : "amount=10;sens=1.6"); chain.Insert(0, dn);
                rep.Add("Noise: floor is " + vs.NoiseFloorDb.ToString("0") + " dBFS, added Noise Reduction (" + dn.Get("amount") + " dB).");
            }
            // tone: compare to the vocal target curve
            double[] dev = new double[11]; for (int b = 0; b < 11; b++) dev[b] = vs.BandDb[b] - VocalTarget[b];
            double mud = (dev[2] + dev[3]) / 2, pres = (dev[7] + dev[8]) / 2, air = dev[10];
            if (eq != null)
            {
                if (mud > 2) { double g = Math.Min(4, 0.6 * mud); Nudge(eq, 1, -g); rep.Add("EQ: extra " + (-g).ToString("0.0") + " dB cut near 300 Hz (low-mids are " + mud.ToString("0.0") + " dB above target)."); }
                if (pres < -2) { double g = Math.Min(3, 0.5 * -pres); Nudge(eq, 3, g); rep.Add("EQ: +" + g.ToString("0.0") + " dB presence near 3 kHz (vocal is " + (-pres).ToString("0.0") + " dB dull there)."); }
                if (air < -3) { double g = Math.Min(3, 0.4 * -air); Nudge(eq, 5, g); rep.Add("EQ: +" + g.ToString("0.0") + " dB air shelf at 10 kHz."); }
                rep.Add("EQ: high-pass at 80-110 Hz to remove rumble.");
            }
            if (comp != null)
            {
                double lra = double.IsNaN(vs.Loud.LRA) ? 6 : vs.Loud.LRA; comp.Set("thr", (float)Math.Max(-40, Math.Min(-12, vs.Loud.I - 3)));
                comp.Set("ratio", (float)(lra > 9 ? Math.Max(comp.Get("ratio"), 4) : (lra < 5 ? Math.Min(comp.Get("ratio"), 2.2) : comp.Get("ratio"))));
                rep.Add("Compressor: threshold " + comp.Get("thr").ToString("0") + " dB, ratio " + comp.Get("ratio").ToString("0.0") + ":1 (vocal dynamic range " + lra.ToString("0.0") + " LU).");
            }
            if (de != null && vs.SibilanceDb > 0) { de.Set("thr", de.Get("thr") - 3); rep.Add("De-esser: strengthened, sibilance is " + vs.SibilanceDb.ToString("0.0") + " dB above the upper-mids."); }
            if (pitch != null)
            {
                bool tune = enhance && (si == 2 || si == 3);
                if (!enhance) pitch.Bypass = true;
                int key, scale; double conf;
                if (enhance && KeyDetect.Detect(voc.Clip, out key, out scale, out conf))
                {
                    pitch.Set("key", key); pitch.Set("scale", scale); ((PitchCorrector)pitch).ApplyScale();
                    pitch.Bypass = !tune && si != 4 ? false : pitch.Bypass;
                    rep.Add("Pitch correction: detected key " + PitchCorrector.Keys[key] + (scale == 2 ? " minor" : " major") + " (confidence " + conf.ToString("0.00") + "), " + (pitch.Bypass ? "bypassed" : "enabled") + ".");
                }
                else rep.Add("Pitch correction: " + (enhance ? "key not detected, left bypassed" : "off (vocal enhancement not selected)") + ".");
            }
            lock (eng.Sync) { voc.Fx.Clear(); voc.Fx.AddRange(chain); }
        }

        // ---- beat chain ----
        if (beat != null)
        {
            progress("Building instrumental chain...");
            var chain = Presets.BeatChain(); var eq = Find(chain, "eq"); var duck = Find(chain, "duck");
            double[] dev = new double[11]; for (int b = 0; b < 11; b++) dev[b] = bs.BandDb[b] - BeatTarget[b];
            double mud = (dev[2] + dev[3]) / 2; if (eq != null)
            {
                eq.Set("b3_g", fullSong ? 0f : (float)Pocket[si]); if (!fullSong) rep.Add("Beat EQ: " + Pocket[si].ToString("0.0") + " dB pocket at 2.5 kHz so the vocal sits on top.");
                if (mud > 2) { double g = Math.Min(4, 0.6 * mud); eq.Set("b1_g", (float)-g); rep.Add("Beat EQ: " + (-g).ToString("0.0") + " dB cut near 300 Hz (muddy by " + mud.ToString("0.0") + " dB)."); }
            }
            if (duck != null) { duck.Set("amount", (float)DuckAmount[si]); rep.Add("Ducking: beat dips " + DuckAmount[si].ToString("0.0") + " dB (mid band only, bass untouched) when the vocal is present."); }
            if (voc == null) { var d = Find(chain, "duck"); if (d != null) d.Bypass = true; }
            lock (eng.Sync) { beat.Fx.Clear(); beat.Fx.AddRange(chain); }
        }

        // ---- levels: render each chain alone, set faders ----
        progress("Balancing levels...");
        double tb = -18.0; float[] oL, oR;
        lock (eng.Sync) eng.ResetStates();
        if (beat != null)
        {
            beat.VolDb = 0; beat.Pan = 0;
            eng.Render(0, Math.Max(1, Math.Min(Exporter.RowEnd(eng, beat), StudioEngine.SR * 180)), new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = Exporter.RowSet(eng, beat) }, null, out oL, out oR);
            var r = Loudness.Measure(oL, oR, StudioEngine.SR, 0, oL.Length);
            if (!double.IsInfinity(r.I)) { beat.VolDb = (float)Math.Max(-24, Math.Min(12, tb - r.I)); rep.Add("Level: instrumental measured " + r.I.ToString("0.0") + " LUFS, fader " + beat.VolDb.ToString("+0.0;-0.0") + " dB."); }
        }
        if (voc != null)
        {
            voc.VolDb = 0; voc.Pan = 0;
            eng.Render(0, Math.Max(1, Math.Min(Exporter.RowEnd(eng, voc), StudioEngine.SR * 180)), new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = Exporter.RowSet(eng, voc) }, null, out oL, out oR);
            var r = Loudness.Measure(oL, oR, StudioEngine.SR, 0, oL.Length);
            double target = (beat != null ? tb + VocalOffset[si] : -16.0);
            if (!double.IsInfinity(r.I)) { voc.VolDb = (float)Math.Max(-24, Math.Min(12, target - r.I)); rep.Add("Level: vocal measured " + r.I.ToString("0.0") + " LUFS, fader " + voc.VolDb.ToString("+0.0;-0.0") + " dB (sits " + (beat != null ? VocalOffset[si].ToString("0.0") + " LU above the beat" : "at -16 LUFS") + ")."); }
        }

        // ---- headroom check on the combined mix (before mastering) ----
        long frames = Math.Min(eng.EndFrame, StudioEngine.SR * 180);
        eng.Render(0, frames, new RenderOpts { Master = false }, null, out oL, out oR);
        var mix = Loudness.Measure(oL, oR, StudioEngine.SR, 0, oL.Length);
        if (mix.TruePeakDb > -1.5)
        {
            float cut = (float)(mix.TruePeakDb + 1.5); foreach (var t in eng.Tracks) if (t == voc || t == beat) t.VolDb -= cut;
            rep.Add("Headroom: mix peaked at " + mix.TruePeakDb.ToString("0.0") + " dB true peak, lowered both faders by " + cut.ToString("0.0") + " dB.");
        }
        lock (eng.Sync) eng.ResetStates();
        rep.Add("Result: mix before mastering is about " + (mix.I - Math.Max(0, mix.TruePeakDb + 1.5)).ToString("0.0") + " LUFS. Use the MASTERING tab for the final loudness.");
        rep.Add("Every setting above is a normal plugin parameter or fader you can edit.");
        return rep;
    }

    public static void Undo(StudioEngine eng) { if (LastSnapshot != null) ProjectIO.RestoreMix(eng, LastSnapshot); }
}

// ---------------- project files + mix snapshots ----------------
static class ProjectIO
{
    public static byte[] CoverBytes;   // the project's picture (a small JPEG), saved inside the project file
    public static string LyricsKept = "";   // the lyrics as they were when they were last put in a project file or saved as a .txt (leaving asks about lyrics that differ)
    public static string Lyrics = "", LyricsTitle = "";   // the lyrics page; saved in the project file
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string E(string s) { return (s ?? "").Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A").Replace("\r", ""); }
    static string U(string s) { return s.Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%"); }

    // mixer settings only (no audio): used by Auto Mix undo and inside project files
    public static string SerializeMix(StudioEngine eng)
    {
        var sb = new StringBuilder();
        sb.Append("master|").Append(eng.MasterVolDb.ToString("R", CI)).Append('|').Append(eng.MasterBypass ? 1 : 0).Append('|').Append(eng.AbMatchDb.ToString("R", CI)).Append('\n');
        for (int i = 0; i < eng.Tracks.Count; i++)
        {
            var t = eng.Tracks[i];
            sb.Append("tmix|").Append(i).Append('|').Append(t.VolDb.ToString("R", CI)).Append('|').Append(t.Pan.ToString("R", CI)).Append('|').Append(t.Mute ? 1 : 0).Append('|').Append(t.Solo ? 1 : 0).Append('|').Append(t.Off ? 1 : 0).Append('\n');
            { int fk = 0; foreach (var f in t.Fx) { sb.Append("fx|").Append(i).Append('|').Append(f.Type).Append('|').Append(f.Bypass ? 1 : 0).Append('|').Append(f.Serialize()).Append('\n'); if (f.Preset.Length > 0) sb.Append("fxp|").Append(i).Append('|').Append(fk).Append('|').Append(f.Preset.Replace('|', '/').Replace('\n', ' ')).Append('|').Append(f.PresetId.Replace('|', '/')).Append('\n'); fk++; } }
            AutoIO.Write(sb, i, t);
        }
        foreach (var f in eng.MasterFx) sb.Append("mfx|").Append(f.Type).Append('|').Append(f.Bypass ? 1 : 0).Append('|').Append(f.Serialize()).Append('\n');
        if (MasterPreset.Length > 0) sb.Append("mpreset|").Append(MasterPreset.Replace('|', ' ').Replace('\n', ' ')).Append('|').Append(MasterPresetEdited(eng) ? 1 : 0).Append('\n');   // the mastering preset on the master (shown on the Mastering page)
        return sb.ToString();
    }

    // the mastering preset that is on the master: its name, the chain as the preset left it, and whether it had been changed when the project was saved
    public static string MasterPreset = ""; public static string[] MasterPresetSnap; public static bool MasterPresetWasEdited;
    public static string[] ChainOf(StudioEngine eng) { var l = new List<string>(); lock (eng.Sync) foreach (var f in eng.MasterFx) l.Add((f.Bypass ? "1" : "0") + f.Serialize()); return l.ToArray(); }
    public static bool MasterPresetEdited(StudioEngine eng) { if (MasterPreset.Length == 0) return false; if (MasterPresetWasEdited) return true; var now = ChainOf(eng); var was = MasterPresetSnap; if (was == null || was.Length != now.Length) return was != null; for (int i = 0; i < now.Length; i++) if (now[i] != was[i]) return true; return false; }
    public static void RestoreMix(StudioEngine eng, string s) { RestoreMix(eng, s, null); }
    // order = the tracks in the order the saved lines number them (a project file numbers them as it wrote them, before they are put back into rows, pieces, buses)
    public static void RestoreMix(StudioEngine eng, string s, Track[] order)
    {
        // build everything first (plugin loads can take seconds), then swap under the lock and dispose the old plugins afterwards
        Track[] trs; lock (eng.Sync) trs = order ?? eng.Tracks.ToArray();
        var newFx = new List<Fx>[trs.Length]; for (int k = 0; k < newFx.Length; k++) newFx[k] = new List<Fx>();
        var origFx = new List<Fx>[trs.Length]; for (int k = 0; k < origFx.Length; k++) origFx[k] = new List<Fx>(); var autos = new List<string[]>();   // origFx keeps a place for a plugin that could not be loaded, so automation lanes still find the right plugin
        var newMaster = new List<Fx>(); var tmix = new List<string[]>(); string[] master = null, mpreset = null;
        foreach (var line in s.Split('\n'))
        {
            if (line.Length == 0) continue; var p = line.Split(new[] { '|' }, line.StartsWith("tmix|") ? 7 : 6);   // (a mix line has seven fields now: the last says the track is turned off)
            try
            {
                if (p[0] == "master") { F(p[1]); F(p[3]); master = p; }
                else if (p[0] == "mpreset") mpreset = p;
                else if (p[0] == "tmix") { int i = int.Parse(p[1]); if (i < trs.Length) tmix.Add(p); }
                else if (p[0] == "auto") autos.Add(p);
                else if (p[0] == "fx") { int i = int.Parse(p[1]); if (i < trs.Length) { var f = Presets.Make(p[2], p.Length > 4 ? p[4] : ""); origFx[i].Add(f); if (f != null) { f.Bypass = p[3] == "1"; var vf = f as InstrumentFx; if (vf != null) vf.Owner = trs[i]; newFx[i].Add(f); } } }
                else if (p[0] == "fxp") { int i = int.Parse(p[1]), k = int.Parse(p[2]); if (i < trs.Length && k >= 0 && k < origFx[i].Count && origFx[i][k] != null && p.Length > 3) origFx[i][k].Preset = p[3]; if (i < trs.Length && k >= 0 && k < origFx[i].Count && origFx[i][k] != null && p.Length > 4) origFx[i][k].PresetId = p[4]; }
                else if (p[0] == "mfx") { var q = line.Split(new[] { '|' }, 4); var f = Presets.Make(q[1], q.Length > 3 ? q[3] : ""); if (f != null) { f.Bypass = q[2] == "1"; newMaster.Add(f); } }
            }
            catch { }
        }
        var old = new List<Fx>();
        lock (eng.Sync)
        {
            foreach (var t in eng.Tracks) { old.AddRange(t.Fx); t.Fx.Clear(); }
            old.AddRange(eng.MasterFx); eng.MasterFx.Clear();
            for (int k = 0; k < trs.Length; k++) if (eng.Tracks.Contains(trs[k])) trs[k].Fx.AddRange(newFx[k]);
            foreach (var tq in trs) tq.Auto.Clear();
            foreach (var ap in autos) { var got = new List<KeyValuePair<Track, AutoLane>>(); AutoIO.Read(ap, trs, origFx, got); foreach (var kv in got) kv.Key.Auto.Add(kv.Value); }
            eng.MasterFx.AddRange(newMaster);
            if (master != null) { eng.MasterVolDb = F(master[1]); eng.MasterBypass = master[2] == "1"; eng.AbMatchDb = F(master[3]); }
            foreach (var p in tmix) { try { int i = int.Parse(p[1]); var t = trs[i]; t.VolDb = F(p[2]); t.Pan = F(p[3]); t.Mute = p[4] == "1"; t.Solo = p[5] == "1"; t.Off = p.Length > 6 && p[6] == "1"; } catch { } }
            if (eng.MasterFx.Count == 0) eng.InitMasterChain();
            eng.ResetStates();
        }
        foreach (var of in old) if (of is IDisposable) { try { ((IDisposable)of).Dispose(); } catch { } }
        MasterPreset = mpreset != null && mpreset.Length > 1 ? mpreset[1] : ""; MasterPresetSnap = MasterPreset.Length > 0 ? ChainOf(eng) : null; MasterPresetWasEdited = mpreset != null && mpreset.Length > 2 && mpreset[2].Trim() == "1";
    }
    static float F(string s) { float v; return float.TryParse(s, NumberStyles.Float, CI, out v) ? v : 0; }

    public static void Save(StudioEngine eng, string path)
    {
        var sb = new StringBuilder(); sb.Append("YTMPROJ|1\n"); sb.Append("bpm|").Append(Fx.ProjectBpm.ToString("R", CI)).Append('\n');
        string dir = Path.GetDirectoryName(path);
        for (int i = 0; i < eng.Tracks.Count; i++)
        {
            var t = eng.Tracks[i]; string src = t.Clip != null ? t.Clip.Path : t.Source;
            sb.Append("track|").Append(E(t.Instrument ? "inst" : (t.IsBus ? "bus" : t.Role))).Append('|').Append(E(t.Name)).Append('|').Append(t.Offset).Append('|').Append(E(src)).Append('|').Append(t.Color).Append('|').Append(t.ClipStart).Append('|').Append(t.ClipFrames).Append('\n');
            if (t.Host != null && eng.Tracks.IndexOf(t.Host) >= 0) sb.Append("host|").Append(i).Append('|').Append(eng.Tracks.IndexOf(t.Host)).Append('\n');
            if (t.Mono) sb.Append("mono|").Append(i).Append('\n');
            if (t.SectionLabel.Length > 0) sb.Append("label|").Append(i).Append('|').Append(E(t.SectionLabel)).Append('\n');   // the name of this one section
            if (t.FadeIn > 0 || t.FadeOut > 0 || t.FadeInDelay > 0 || t.FadeOutTail > 0 || t.ClipGainDb != 0f || t.Locked || t.PieceLocked || t.PieceMuted) sb.Append("clip|").Append(i).Append('|').Append(t.FadeIn).Append('|').Append(t.FadeOut).Append('|').Append(t.ClipGainDb.ToString("R", CI)).Append('|').Append(t.Locked ? 1 : 0).Append('|').Append(t.FadeInDelay).Append('|').Append(t.FadeOutTail).Append('|').Append(t.PieceLocked ? 1 : 0).Append('|').Append(t.PieceMuted ? 1 : 0).Append('\n');
            if (t.Out != null && eng.Tracks.IndexOf(t.Out) >= 0) sb.Append("route|").Append(i).Append('|').Append(eng.Tracks.IndexOf(t.Out)).Append('\n');
            if (t.Instrument && t.Midi.Count > 0) { sb.Append("midi|").Append(i).Append('|'); lock (t.Midi) foreach (var m in t.Midi) sb.Append(m.Frame).Append(':').Append(m.S).Append(':').Append(m.D1).Append(':').Append(m.D2).Append(';'); sb.Append('\n'); }
        }
        if (CoverBytes != null && CoverBytes.Length > 0) sb.Append("cover|").Append(Convert.ToBase64String(CoverBytes)).Append((char)10);
        if (Lyrics.Length > 0 || LyricsTitle.Length > 0) sb.Append("lyrics|").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(LyricsTitle))).Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(Lyrics))).Append((char)10);
        sb.Append(SerializeMix(eng)); Win.WriteAtomic(path, sb.ToString(), Encoding.UTF8); LyricsKept = Lyrics;
    }

    // Loads clips through ffmpeg (slow part), then restores plugin settings and track alignment.
    // what a project file holds besides its tracks; a preview or background render gets these here instead of overwriting the open project's tempo, lyrics and picture
    public class ProjMeta { public double Bpm = double.NaN; public string Lyrics = "", LyricsTitle = ""; public byte[] Cover; }
    public static List<string> Load(StudioEngine eng, string path, Action<string> progress) { return Load(eng, path, progress, null); }
    public static List<string> Load(StudioEngine eng, string path, Action<string> progress, ProjMeta meta)
    {
        var warn = new List<string>(); var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0 || !lines[0].StartsWith("YTMPROJ")) throw new Exception("That isn't a project file from this app.");
        string dir = Path.GetDirectoryName(path); var tracks = new List<Track>(); var mix = new StringBuilder(); var routes = new List<string[]>(); var hosts = new List<string[]>();
        // read into locals: the shared tempo/lyrics/picture change only once the file has loaded, so a failed open leaves the open project untouched
        var m = new ProjMeta();
        foreach (var line in lines)
        {
            var p = line.Split('|');
            if (p[0] == "cover" && p.Length >= 2) { try { m.Cover = Convert.FromBase64String(p[1]); } catch { } }
            else if (p[0] == "lyrics" && p.Length >= 3) { try { string lt0 = Encoding.UTF8.GetString(Convert.FromBase64String(p[1])), ly0 = Encoding.UTF8.GetString(Convert.FromBase64String(p[2])); m.LyricsTitle = lt0; m.Lyrics = ly0; } catch { } }
            else if (p[0] == "bpm" && p.Length > 1) { double b; if (double.TryParse(p[1], NumberStyles.Float, CI, out b)) m.Bpm = b; }
            else if (p[0] == "track" && p.Length >= 6)
            {
                string src = U(p[4]), name = U(p[2]); Track t = StudioEngine.NewTrack(name, U(p[1]), null); t.Source = src; t.Instrument = U(p[1]) == "inst"; if (U(p[1]) == "bus") { t.IsBus = true; t.Color = unchecked((int)0xFFFFD04D); }
                long off; long.TryParse(p[3], out off); t.Offset = off; int col; if (int.TryParse(p[5], out col)) t.Color = col;
                if (p.Length >= 8) { long cs, cf; if (long.TryParse(p[6], out cs)) t.ClipStart = cs; if (long.TryParse(p[7], out cf)) t.ClipFrames = cf; }
                if (t.Instrument || t.IsBus || src.Length == 0) { }
                else if (File.Exists(src)) { progress("Loading " + Path.GetFileName(src) + "..."); t.Clip = AudioClip.FromFile(src); }
                else warn.Add("Missing audio file for \"" + name + "\": " + src);
                tracks.Add(t);
            }
            else if (p[0] == "midi" && p.Length >= 3)
            {
                int ti; if (int.TryParse(p[1], out ti) && ti >= 0 && ti < tracks.Count)
                    foreach (var part in p[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    { var q = part.Split(':'); long fr; int s, d1, d2; if (q.Length == 4 && long.TryParse(q[0], out fr) && int.TryParse(q[1], out s) && int.TryParse(q[2], out d1) && int.TryParse(q[3], out d2)) tracks[ti].Midi.Add(new MidiEv { Frame = fr, S = (byte)s, D1 = (byte)d1, D2 = (byte)d2 }); }
            }
            else if (p[0] == "label" && p.Length >= 3) { int li; if (int.TryParse(p[1], out li) && li >= 0 && li < tracks.Count) tracks[li].SectionLabel = U(p[2]); }
            else if (p[0] == "mono" && p.Length >= 2) { int mi; if (int.TryParse(p[1], out mi) && mi >= 0 && mi < tracks.Count) tracks[mi].Mono = true; }
            else if (p[0] == "clip" && p.Length >= 6) { int ci; long fiv, fov; if (int.TryParse(p[1], out ci) && ci >= 0 && ci < tracks.Count) { if (long.TryParse(p[2], out fiv)) tracks[ci].FadeIn = fiv; if (long.TryParse(p[3], out fov)) tracks[ci].FadeOut = fov; tracks[ci].ClipGainDb = F(p[4]); tracks[ci].Locked = p[5] == "1"; long dlv, tlv; if (p.Length >= 8) { if (long.TryParse(p[6], out dlv)) tracks[ci].FadeInDelay = dlv; if (long.TryParse(p[7], out tlv)) tracks[ci].FadeOutTail = tlv; } if (p.Length >= 9) tracks[ci].PieceLocked = p[8] == "1"; if (p.Length >= 10) tracks[ci].PieceMuted = p[9] == "1"; } }
            else if (p[0] == "route" && p.Length >= 3) routes.Add(p);
            else if (p[0] == "host" && p.Length >= 3) hosts.Add(p);
            else if (p[0] == "master" || p[0] == "tmix" || p[0] == "fx" || p[0] == "fxp" || p[0] == "mfx" || p[0] == "mpreset" || p[0] == "auto") mix.Append(line).Append('\n');
        }
        foreach (var rp in routes) { int ra, rb; if (int.TryParse(rp[1], out ra) && int.TryParse(rp[2], out rb) && ra >= 0 && rb >= 0 && ra < tracks.Count && rb < tracks.Count && tracks[rb].IsBus && ra != rb) tracks[ra].Out = tracks[rb]; }
        foreach (var hp in hosts) { int ha, hb; if (int.TryParse(hp[1], out ha) && int.TryParse(hp[2], out hb) && ha >= 0 && hb >= 0 && ha < tracks.Count && hb < tracks.Count && ha != hb && !tracks[hb].IsBus && !tracks[ha].IsBus) tracks[ha].Host = tracks[hb]; }
        foreach (var q in tracks) if (q.Host != null && q.Host.Host != null) q.Host = q.Host.Host;   // one level only
        var fileOrder = tracks.ToArray();   // the fx, mix and automation lines number the tracks in the order the file wrote them
        { var ord = new List<Track>(); foreach (var q in tracks) if (!q.IsBus && q.Host == null) ord.Add(q); foreach (var q in tracks) if (!q.IsBus && q.Host != null) ord.Add(q); foreach (var q in tracks) if (q.IsBus) ord.Add(q); tracks = ord; }   // rows, then layers, then buses   // buses go after the tracks
        if (meta != null) { meta.Bpm = m.Bpm; meta.Lyrics = m.Lyrics; meta.LyricsTitle = m.LyricsTitle; meta.Cover = m.Cover; }
        else { if (!double.IsNaN(m.Bpm)) Fx.ProjectBpm = m.Bpm; Lyrics = m.Lyrics; LyricsTitle = m.LyricsTitle; CoverBytes = m.Cover; }
        lock (eng.Sync) { eng.Tracks.Clear(); eng.Tracks.AddRange(tracks); }
        RestoreMix(eng, mix.ToString(), fileOrder); eng.Seek(0);
        foreach (var lt in eng.Tracks) foreach (var lf in lt.Fx) lf.AutoKeyPending = false;   // the plugins of a saved project keep the key that was saved
        if (meta == null) LyricsKept = Lyrics; return warn;
    }
}

// ---------------- export ----------------
class ExportItem { public string Label, FileName; public RenderOpts Opts; }

static class Exporter
{
    public static readonly string[] Formats = { "WAV 24-bit", "WAV 16-bit", "WAV 32-bit float", "FLAC 24-bit", "MP3 320 kbps", "MP4 video" };
    public const int Mp4 = 5; public static string VideoFrame = "";   // MP4: the picture of the project (a PNG made by the studio just before the export) shown for the whole song
    // MP4 sizes (square, wide, tall) and sound qualities: the choice is yours
    public static readonly string[] VideoNames = { "Square 720 x 720  (fills it)", "Square 1080 x 1080  (fills it, best)", "Square 1440 x 1440  (fills it)", "Square 2160 x 2160  (4K, fills it)", "Wide 854 x 480  (black at the sides)", "Wide 1280 x 720  (black at the sides)", "Wide 1920 x 1080  (black at the sides)", "Wide 2560 x 1440  (black at the sides)", "Wide 3840 x 2160  (black at the sides)", "Tall 720 x 1280  (black above and below)", "Tall 1080 x 1920  (black above and below)", "Tall 2160 x 3840  (black above and below)" };
    public static readonly int[,] VideoWH = { { 720, 720 }, { 1080, 1080 }, { 1440, 1440 }, { 2160, 2160 }, { 854, 480 }, { 1280, 720 }, { 1920, 1080 }, { 2560, 1440 }, { 3840, 2160 }, { 720, 1280 }, { 1080, 1920 }, { 2160, 3840 } };
    public static readonly string[] SoundNames = { "AAC 128 kbps  (smallest)", "AAC 192 kbps  (small, sounds great)", "AAC 256 kbps", "AAC 320 kbps  (best)" };
    public static readonly int[] SoundKbps = { 128, 192, 256, 320 };
    public static int SoundRate = 192;   // the sound quality of the MP4 being made
    static readonly string[] Ext = { "wav", "wav", "wav", "flac", "mp3", "mp4" };

    public static string FileExt(int fmt) { return Ext[fmt]; }
    // the sample rate of the finished file: the music is made at 44.1 kHz and converted with a high quality filter (MP3 stops at 48 kHz, which is as high as that format goes)
    public static readonly int[] Rates = { 44100, 48000, 88200, 96000, 176400, 192000, 352800, 384000 };
    public static readonly string[] RateNames = { "44.1 kHz (as made)", "48 kHz", "88.2 kHz", "96 kHz", "176.4 kHz", "192 kHz", "352.8 kHz (DXD)", "384 kHz (the highest)" };
    public static int RateFor(int fmt, int rateIdx) { int r = Math.Min(Rates[Math.Max(0, Math.Min(Rates.Length - 1, rateIdx))], Edition.MaxRate); return fmt >= 4 ? Math.Min(r, 48000) : r; }

    public static List<ExportItem> Plan(StudioEngine eng, bool mixMastered, bool mixDry, bool procVocal, bool procBeat, bool dryVocal, bool dryBeat, bool stems, string prefix, int fmt, HashSet<Track> onlyStems = null)
    {
        var l = new List<ExportItem>(); string ext = "." + Ext[fmt]; Track voc = eng.VocalTrack(), beat = null; foreach (var t in eng.Tracks) if (t.Role == "beat" && t.Host == null) { beat = t; break; }
        if (mixMastered) l.Add(new ExportItem { Label = "Full mix (mastered)", FileName = prefix + " - mix mastered" + ext, Opts = new RenderOpts { Master = true } });
        if (mixDry) l.Add(new ExportItem { Label = "Full mix (no mastering)", FileName = prefix + " - mix unmastered" + ext, Opts = new RenderOpts { Master = false } });
        if (procVocal && voc != null) l.Add(new ExportItem { Label = "Processed vocals", FileName = prefix + " - vocals processed" + ext, Opts = new RenderOpts { Master = false, MuteSolo = false, Only = RowSet(eng, voc) } });
        if (procBeat && beat != null) l.Add(new ExportItem { Label = "Processed instrumental", FileName = prefix + " - instrumental processed" + ext, Opts = new RenderOpts { Master = false, MuteSolo = false, Only = RowSet(eng, beat) } });
        if (dryVocal && voc != null) l.Add(new ExportItem { Label = "Dry vocals", FileName = prefix + " - vocals dry" + ext, Opts = new RenderOpts { Master = false, Dry = true, Fader = false, MuteSolo = false, Only = RowSet(eng, voc) } });
        if (dryBeat && beat != null) l.Add(new ExportItem { Label = "Dry instrumental", FileName = prefix + " - instrumental dry" + ext, Opts = new RenderOpts { Master = false, Dry = true, Fader = false, MuteSolo = false, Only = RowSet(eng, beat) } });
        // one stem per row with all of its pieces, played through the row's plugins and fader (a piece on its own would use its stale cloned chain)
        if (stems)
            foreach (var t in eng.Tracks)
            {
                if (t.Host != null) continue; var set = RowSet(eng, t); bool has = false, picked = false;
                foreach (var p in set) { if ((p.Clip != null && p.Clip.Loaded) || p.Instrument) has = true; if (onlyStems != null && onlyStems.Contains(p)) picked = true; }
                if (has && (onlyStems != null ? picked : (!t.Mute && !t.Off))) l.Add(new ExportItem { Label = "Stem: " + t.Name, FileName = prefix + " - stem " + Safe(t.Name) + ext, Opts = new RenderOpts { Master = false, MuteSolo = false, Only = set } });
            }
        return l;
    }
    // a row and every piece or layer on it, so a render of "this track" hears the whole row
    public static List<Track> RowSet(StudioEngine eng, Track row) { var l = new List<Track> { row }; foreach (var p in eng.Tracks) if (p.Host == row && !l.Contains(p)) l.Add(p); return l; }
    // where the last piece on the row ends (the row's own clip may end early after a split)
    public static long RowEnd(StudioEngine eng, Track row) { long e = 0; foreach (var p in RowSet(eng, row)) e = Math.Max(e, p.Offset + p.UsedLength); return e; }

    static string Safe(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }

    // Renders one item straight to disk in small blocks, so memory stays flat and hour-long songs export fine.
    public static string Write(StudioEngine eng, ExportItem it, string dir, int fmt, Action<float> prog, Func<bool> cancelled = null, int rate = 44100, double trimA = 0, double trimB = 0)
    {
        // the whole song, or only the part between "trim start" and "trim end" (seconds; an end of 0 means the end of the song)
        long allFrames = eng.EndFrame + StudioEngine.SR * 2; long from = Math.Max(0, Math.Min(allFrames - 1, (long)(trimA * StudioEngine.SR))), to = trimB > 0 ? Math.Min(allFrames, (long)(trimB * StudioEngine.SR)) : allFrames;
        if (to <= from) throw new Exception("The trim end must be after the trim start.");
        long frames = to - from; Directory.CreateDirectory(dir); if (rate < 8000) rate = StudioEngine.SR;
        string outp = Path.Combine(dir, Safe(it.FileName));
        if (fmt <= 2)
        {
            int bits = fmt == 0 ? 24 : (fmt == 1 ? 16 : 32);
            if (frames * rate / StudioEngine.SR * 2L * (bits / 8) > 4000000000L) throw new Exception("WAV files are limited to about 4 GB (about " + (int)(4000000000.0 / ((double)rate * 2 * (bits / 8) * 60)) + " minutes at this rate and depth). Choose a lower sample rate, or FLAC.");
            using (var ws = new WavStream(outp, rate, bits))
            {
                if (rate == StudioEngine.SR) eng.RenderStream(from, frames, it.Opts, prog, (l, r, o, n) => ws.Write(l, r, o, n), cancelled);
                else
                {   // the sound is converted to the chosen rate as it is made, block by block
                    var rs = new RatioResampler(StudioEngine.SR, rate); float[] oL = new float[1], oR = new float[1];
                    eng.RenderStream(from, frames, it.Opts, prog, (l, r, o, n) => { int cap = (int)((long)n * rs.Up / rs.Down) + 16; if (oL.Length < cap) { oL = new float[cap]; oR = new float[cap]; } int c = rs.Process(l, r, o, n, oL, oR); ws.Write(oL, oR, 0, c); }, cancelled);
                    int fc = 2 * 32 * rs.Up / rs.Down + 64; var fL = new float[fc]; var fR = new float[fc]; int c2 = rs.Flush(fL, fR); ws.Write(fL, fR, 0, c2);
                }
            }
            if (cancelled != null && cancelled()) { try { File.Delete(outp); } catch { } throw new Exception("Export cancelled."); }
            return outp;
        }
        string ff = StudioEnv.Ffmpeg(); if (ff == null) throw new Exception("ffmpeg isn't installed yet (convert something once in the main window first).");
        string enc = fmt == 3 ? "-c:a flac -compression_level 8 -sample_fmt s32 -bits_per_raw_sample 24" : "-c:a libmp3lame -b:a 320k";
        // Render to a temporary lossless WAV in small blocks (flat memory), then encode from the file.
        string tmp = Path.Combine(Path.GetTempPath(), "ytm_export_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".wav");
        try
        {
            int tbits = frames * 2L * 4 > 3900000000L ? 24 : 32;
            if (frames * 2L * (tbits / 8) > 4000000000L) throw new Exception("That is too long to export as one file (about 4 hours). Export in parts.");
            using (var ws = new WavStream(tmp, StudioEngine.SR, tbits)) eng.RenderStream(from, frames, it.Opts, prog == null ? null : (Action<float>)(x => prog(x * 0.9f)), (l, r, o, n) => ws.Write(l, r, o, n), cancelled);
            if (cancelled != null && cancelled()) throw new Exception("Export cancelled.");
            int code; string o2;
            if (fmt == Mp4)
            {   // MP4: the project's picture fills the video for exactly as long as the song (a still picture: H.264 1080x1080, AAC 320 kbps at 48 kHz), ready for YouTube, Instagram, TikTok...
                if (string.IsNullOrEmpty(VideoFrame) || !File.Exists(VideoFrame)) throw new Exception("The picture for the video could not be made.");
                string vin = "-hide_banner -y -loop 1 -framerate 24 -i \"" + VideoFrame + "\" -i \"" + tmp + "\" -map 0:v:0 -map 1:a:0 ", vout = " -r 24 -c:a aac -b:a " + SoundRate + "k -ar 48000 -shortest -movflags +faststart \"" + outp + "\"";
                o2 = MasterDsp.RunCapture(ff, vin + "-c:v libx264 -preset slow -tune stillimage -crf 23 -g 7200 -keyint_min 7200 -sc_threshold 0 -x264-params mbtree=0:ipratio=6:pbratio=6:aq-mode=0 -pix_fmt yuv420p" + vout, null, out code);   // as small as it can be: the picture never changes, so after the first frame each frame is almost nothing (a long key-frame gap); nearly all of the file is the sound
                if (code != 0 || !File.Exists(outp)) o2 = MasterDsp.RunCapture(ff, vin + "-c:v mpeg4 -q:v 3 -g 7200 -pix_fmt yuv420p" + vout, null, out code);   // an ffmpeg without H.264: plain MPEG-4 video still plays everywhere
            }
            else o2 = MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + tmp + "\" -vn " + (rate != StudioEngine.SR ? "-af aresample=resampler=swr:filter_size=64:cutoff=0.99 -ar " + rate + " " : "") + enc + " \"" + outp + "\"", null, out code);
            if (code != 0 || !File.Exists(outp)) throw new Exception("Encoding failed.\n" + o2);
            if (prog != null) prog(1f);
        }
        catch { try { if (File.Exists(outp) && cancelled != null && cancelled()) File.Delete(outp); } catch { } throw; }
        finally { try { File.Delete(tmp); } catch { } }
        return outp;
    }
}