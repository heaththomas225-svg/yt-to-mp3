using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

// =====================================================================================================
//  Bass Boost: a fine-tunable low shelf, with optional added harmonics so the bass is still audible on small speakers.
// =====================================================================================================
class BassBoost : Fx
{
    Biquad shelf = new Biquad(), hp = new Biquad(), band = new Biquad(), harmHp = new Biquad();
    double harm, lvl, mix;
    public BassBoost()
    {
        Type = "bassboost"; Title = "Bass Boost"; Category = "EQ"; Blurb = "Boost the low end: choose the frequency, amount and width";
        AddP("freq", "Frequency", 30, 200, 70, "Hz"); AddP("gain", "Boost", 0, 24, 8, "dB"); AddP("q", "Width (Q)", 0.3f, 2f, 0.7f, "");
        AddP("harm", "Harmonics", 0, 100, 25, "%"); AddP("tight", "Tighten (low cut)", 10, 60, 22, "Hz");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("level", "Output level", -12, 6, 0, "dB");
    }
    protected override void Update()
    {
        double f = Get("freq");
        shelf.Set(Biquad.LowShelf, SR, f, Math.Max(0.3, Get("q")), Get("gain"));
        hp.Set(Biquad.HighPass, SR, Get("tight"), 0.7071, 0);
        band.Set(Biquad.LowPass, SR, f * 1.7, 0.7071, 0); harmHp.Set(Biquad.HighPass, SR, f * 1.15, 0.7071, 0);
        harm = Get("harm") / 100.0 * 0.6; lvl = DspMath.DbToLin(Get("level")); mix = Get("mix") / 100.0;
    }
    public override void Reset() { base.Reset(); shelf.Reset(); hp.Reset(); band.Reset(); harmHp.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            float dl = l[i], dr = r[i];
            float wl = shelf.TickL(hp.TickL(dl)), wr = shelf.TickR(hp.TickR(dr));
            if (harm > 0)
            {
                // the bass band is saturated (that creates harmonics), the fundamental is filtered out again and the rest is added
                float bl = band.TickL(dl), br = band.TickR(dr);
                float hl = harmHp.TickL((float)Math.Tanh(bl * 4.0)), hr = harmHp.TickR((float)Math.Tanh(br * 4.0));
                wl += (float)(hl * harm); wr += (float)(hr * harm);
            }
            l[i] = (float)((dl * (1 - mix) + wl * mix) * lvl); r[i] = (float)((dr * (1 - mix) + wr * mix) * lvl);
        }
    }
}

// =====================================================================================================
//  Bass Lab: a polyphonic synthesizer for bass and leads (3 oscillators with warp, sub, noise, 2 filters,
//  4 envelopes, 4 tempo-synced LFOs, 8-slot mod matrix, 4 macros, effects, bass boost).
// =====================================================================================================
static class SynthTables
{
    public static readonly string[] Waves = { "Sine", "Triangle", "Saw", "Square", "Pulse" };
    public static readonly string[] Warps = { "Off", "Sync", "Bend", "Fold" };
    public static readonly string[] FilterTypes = { "Low-pass 12", "Low-pass 24", "High-pass 12", "Band-pass", "Notch" };
    public static readonly string[] LfoShapes = { "Sine", "Triangle", "Saw up", "Saw down", "Square", "Random", "Wub" };
    public static readonly string[] Syncs = { "Hz", "4 bars", "2 bars", "1 bar", "1/2", "1/4", "1/4 dot", "1/4 trip", "1/8", "1/8 dot", "1/8 trip", "1/16", "1/16 dot", "1/16 trip", "1/32" };
    public static readonly double[] SyncBeats = { 0, 16, 8, 4, 2, 1, 1.5, 2.0 / 3, 0.5, 0.75, 1.0 / 3, 0.25, 0.375, 1.0 / 6, 0.125 };
    public static readonly string[] Sources = { "None", "LFO 1", "LFO 2", "LFO 3", "LFO 4", "Env 2", "Env 3", "Env 4", "Velocity", "Key", "Macro 1", "Macro 2", "Macro 3", "Macro 4" };
    public static readonly string[] Dests = { "None", "Volume", "Osc A pitch", "Osc B pitch", "Osc C pitch", "All pitch", "Osc A wave", "Osc B wave", "Osc C wave", "Osc A warp", "Osc B warp", "Osc C warp",
        "Osc A level", "Osc B level", "Osc C level", "Sub level", "Noise level", "Filter 1 cutoff", "Filter 1 resonance", "Filter 1 drive", "Filter 2 cutoff", "Filter 2 resonance", "Unison detune", "Pan" };
    public static readonly string[] OffOn = { "Off", "On" };
    public const int SrcNone = 0, SrcLfo1 = 1, SrcEnv2 = 5, SrcVel = 8, SrcKey = 9, SrcMacro1 = 10;
}

class SynthFx : InstrumentFx
{
    const int NV = 8, CTRL = 16;
    static readonly string[] OscIds = { "a", "b", "c" };

    // ---- cached parameters (read once when something changes) ----
    class OscP { public bool On; public double Wt, Oct, Sem, Fin, Lvl, Pan, Uni, Det, Blend, Phase, WarpAmt; public int Warp; }
    class FiltP { public bool On; public int Type; public double Cut, Res, Drive, Key; }
    class EnvP { public double A, D, S, R; }
    class LfoP { public int Shape, Sync; public double Rate, Phase, Smooth, Fade; public bool Retrig, Uni; }
    OscP[] op = { new OscP(), new OscP(), new OscP() }; FiltP[] fp = { new FiltP(), new FiltP() }; EnvP[] ep = { new EnvP(), new EnvP(), new EnvP(), new EnvP() }; LfoP[] lp = { new LfoP(), new LfoP(), new LfoP(), new LfoP() };
    bool subOn, nzOn, parallel; int subWave, subOct, nzType; double subLvl, nzLvl, vol, glide, bendRange, tune, vsens; int mode; double[] macro = new double[4];
    int[] mSrc = new int[8], mDst = new int[8]; double[] mAmt = new double[8];
    bool dstOn, chOn, dlOn, rvOn, bbOn;

    // ---- voices ----
    class Voice
    {
        public bool On, Released, HeldBySustain; public int Note; public float Vel; public long Age; public double Cur;
        public double[,] Ph = new double[3, 7]; public double SubPh;
        public double[] F = new double[16];                  // filter state: [filter * 8 + stage * 4 + ch * 2 + {0,1}]
        public double[] Env = new double[4]; public int[] Stage = new int[4];
        public double[] Lfo = new double[4], LfoVal = new double[4], LfoHold = new double[4]; public double[] LfoSm = new double[4]; public double Time;
        public double Pink0, Pink1, Pink2;
        public double Slave;
    }
    Voice[] voices = new Voice[NV];
    long ageCounter; double[] gLfo = new double[4]; Random rng = new Random(12345); uint nz = 2463534242;
    readonly List<int> held = new List<int>(); bool sustain; double bend; List<KeyValuePair<int, MidiEv>> pending = new List<KeyValuePair<int, MidiEv>>();
    float[] outL = new float[8192], outR = new float[8192];
    Distortion dist = new Distortion(); Chorus chor = new Chorus(); Delay dly = new Delay(); Reverb rev = new Reverb(); BassBoost boost = new BassBoost();
    SynthWindow window; public string PresetName = "Wobble Bass";
    public double[] DebugMod = new double[24];

    public SynthFx()
    {
        Type = "synth"; Title = "Bass Lab"; Category = "Instruments"; Blurb = "Synthesizer for bass and leads: wobble LFOs, warp, filters, bass boost";
        for (int i = 0; i < NV; i++) voices[i] = new Voice();
        BuildParams(); SynthPresets.Apply(this, SynthPresets.Names[0]); dirty = true;
    }
    public override bool IsInstrument { get { return true; } }

    void BuildParams()
    {
        AddP("vol", "Volume", -30, 6, -8, "dB"); AddP("glide", "Glide", 0, 1000, 0, "ms"); AddP("bend", "Bend range", 0, 24, 2, "st");
        AddC("mode", "Voices", new[] { "Poly", "Mono", "Legato" }, 0); AddP("tune", "Fine tune", -100, 100, 0, "ct"); AddP("vsens", "Velocity", 0, 100, 60, "%");
        foreach (var x in OscIds)
        {
            AddC(x + "on", "On", SynthTables.OffOn, x == "a" ? 1 : 0); AddP(x + "wt", "Wave", 0, 100, 50, "%"); AddP(x + "oct", "Octave", -3, 3, 0, ""); AddP(x + "sem", "Semi", -12, 12, 0, "");
            AddP(x + "fin", "Fine", -100, 100, 0, "ct"); AddP(x + "lvl", "Level", 0, 100, 80, "%"); AddP(x + "pan", "Pan", -100, 100, 0, "");
            AddP(x + "uni", "Unison", 1, 7, 1, ""); AddP(x + "det", "Detune", 0, 100, 20, "ct"); AddP(x + "blend", "Blend", 0, 100, 70, "%"); AddP(x + "phase", "Phase", 0, 360, 0, "deg");
            AddC(x + "warp", "Warp", SynthTables.Warps, 0); AddP(x + "wamt", "Warp amount", 0, 100, 0, "%");
        }
        AddC("subon", "Sub on", SynthTables.OffOn, 0); AddC("subwave", "Sub wave", new[] { "Sine", "Triangle", "Square" }, 0); AddP("suboct", "Sub octave", -2, 0, -1, ""); AddP("sublvl", "Sub level", 0, 100, 70, "%");
        AddC("nzon", "Noise on", SynthTables.OffOn, 0); AddC("nztype", "Noise type", new[] { "White", "Pink" }, 1); AddP("nzlvl", "Noise level", 0, 100, 10, "%");
        for (int k = 1; k <= 2; k++)
        {
            AddC("f" + k + "on", "On", SynthTables.OffOn, k == 1 ? 1 : 0); AddC("f" + k + "type", "Type", SynthTables.FilterTypes, k == 1 ? 1 : 0);
            AddP("f" + k + "cut", "Cutoff", 20, 20000, k == 1 ? 900 : 8000, "Hz"); AddP("f" + k + "res", "Resonance", 0, 100, 25, "%"); AddP("f" + k + "drive", "Drive", 0, 100, 10, "%"); AddP("f" + k + "key", "Key track", 0, 100, 0, "%");
        }
        AddC("froute", "Routing", new[] { "Serial", "Parallel" }, 0);
        for (int k = 1; k <= 4; k++)
        {
            AddP("e" + k + "a", "Attack", 0, 5000, k == 1 ? 2 : 0, "ms"); AddP("e" + k + "d", "Decay", 1, 8000, k == 1 ? 250 : 350, "ms");
            AddP("e" + k + "s", "Sustain", 0, 100, k == 1 ? 80 : 25, "%"); AddP("e" + k + "r", "Release", 1, 8000, k == 1 ? 140 : 250, "ms");
        }
        for (int k = 1; k <= 4; k++)
        {
            AddC("l" + k + "shape", "Shape", SynthTables.LfoShapes, 0); AddC("l" + k + "sync", "Rate sync", SynthTables.Syncs, k == 1 ? 8 : (k == 2 ? 5 : 0));
            AddP("l" + k + "rate", "Rate", 0.05f, 40, 2, "Hz"); AddP("l" + k + "phase", "Phase", 0, 100, 0, "%"); AddP("l" + k + "smooth", "Smooth", 0, 100, 0, "%");
            AddC("l" + k + "retrig", "Start", new[] { "Free", "Retrigger" }, 1); AddP("l" + k + "fade", "Fade in", 0, 3000, 0, "ms"); AddC("l" + k + "uni", "Range", new[] { "Bipolar", "Unipolar" }, 0);
        }
        for (int k = 1; k <= 4; k++) AddP("mac" + k, "Macro " + k, 0, 100, 0, "%");
        for (int i = 1; i <= 8; i++)
        {
            AddC("m" + i + "s", "Source", SynthTables.Sources, 0); AddC("m" + i + "d", "Destination", SynthTables.Dests, 0); AddP("m" + i + "a", "Amount", -100, 100, 0, "%");
        }
        AddC("dston", "Distortion", SynthTables.OffOn, 0); AddC("dstmode", "Type", new[] { "Hard clip", "Foldback", "Fuzz" }, 2); AddP("dstdrive", "Drive", 0, 40, 12, "dB"); AddP("dsttone", "Tone", 500, 16000, 6000, "Hz"); AddP("dstmix", "Mix", 0, 100, 50, "%");
        AddC("chon", "Chorus", SynthTables.OffOn, 0); AddP("chrate", "Rate", 0.05f, 5, 0.8f, "Hz"); AddP("chdepth", "Depth", 0, 10, 3, "ms"); AddP("chmix", "Mix", 0, 100, 35, "%");
        AddC("dlon", "Delay", SynthTables.OffOn, 0); AddC("dlsync", "Time sync", Delay.Syncs, 3); AddP("dlfb", "Feedback", 0, 95, 30, "%"); AddP("dlmix", "Mix", 0, 100, 20, "%");
        AddC("rvon", "Reverb", SynthTables.OffOn, 0); AddP("rvsize", "Size", 0, 100, 45, "%"); AddP("rvdamp", "Damping", 0, 100, 55, "%"); AddP("rvmix", "Mix", 0, 100, 15, "%");
        AddC("bbon", "Bass boost", SynthTables.OffOn, 1); AddP("bbfreq", "Frequency", 30, 200, 60, "Hz"); AddP("bbgain", "Boost", 0, 24, 6, "dB"); AddP("bbq", "Width (Q)", 0.3f, 2f, 0.7f, ""); AddP("bbharm", "Harmonics", 0, 100, 20, "%"); AddP("bbtight", "Tighten", 10, 60, 22, "Hz");
    }

    protected override void Update()
    {
        vol = Get("vol"); glide = Get("glide"); bendRange = Get("bend"); mode = (int)Get("mode"); tune = Get("tune"); vsens = Get("vsens") / 100.0;
        for (int i = 0; i < 3; i++)
        {
            string x = OscIds[i]; var o = op[i];
            o.On = Get(x + "on") > 0.5; o.Wt = Get(x + "wt") / 100.0; o.Oct = Math.Round(Get(x + "oct")); o.Sem = Math.Round(Get(x + "sem")); o.Fin = Get(x + "fin"); o.Lvl = Get(x + "lvl") / 100.0;
            o.Pan = Get(x + "pan") / 100.0; o.Uni = Math.Max(1, Math.Min(7, Math.Round(Get(x + "uni")))); o.Det = Get(x + "det"); o.Blend = Get(x + "blend") / 100.0; o.Phase = Get(x + "phase") / 360.0;
            o.Warp = (int)Get(x + "warp"); o.WarpAmt = Get(x + "wamt") / 100.0;
        }
        subOn = Get("subon") > 0.5; subWave = (int)Get("subwave"); subOct = (int)Math.Round(Get("suboct")); subLvl = Get("sublvl") / 100.0;
        nzOn = Get("nzon") > 0.5; nzType = (int)Get("nztype"); nzLvl = Get("nzlvl") / 100.0;
        for (int k = 0; k < 2; k++)
        {
            var f = fp[k]; string p = "f" + (k + 1); f.On = Get(p + "on") > 0.5; f.Type = (int)Get(p + "type"); f.Cut = Get(p + "cut"); f.Res = Get(p + "res") / 100.0; f.Drive = Get(p + "drive") / 100.0; f.Key = Get(p + "key") / 100.0;
        }
        parallel = Get("froute") > 0.5;
        for (int k = 0; k < 4; k++) { var e = ep[k]; string p = "e" + (k + 1); e.A = Get(p + "a"); e.D = Get(p + "d"); e.S = Get(p + "s") / 100.0; e.R = Get(p + "r"); }
        for (int k = 0; k < 4; k++)
        {
            var l = lp[k]; string p = "l" + (k + 1); l.Shape = (int)Get(p + "shape"); l.Sync = (int)Get(p + "sync"); l.Rate = Get(p + "rate"); l.Phase = Get(p + "phase") / 100.0; l.Smooth = Get(p + "smooth") / 100.0;
            l.Retrig = Get(p + "retrig") > 0.5; l.Fade = Get(p + "fade"); l.Uni = Get(p + "uni") > 0.5; macro[k] = Get("mac" + (k + 1)) / 100.0;
        }
        for (int i = 0; i < 8; i++) { mSrc[i] = (int)Get("m" + (i + 1) + "s"); mDst[i] = (int)Get("m" + (i + 1) + "d"); mAmt[i] = Get("m" + (i + 1) + "a") / 100.0; }
        dstOn = Get("dston") > 0.5; chOn = Get("chon") > 0.5; dlOn = Get("dlon") > 0.5; rvOn = Get("rvon") > 0.5; bbOn = Get("bbon") > 0.5;
        dist.Prepare(SR); dist.Set("mode", Get("dstmode")); dist.Set("drive", Get("dstdrive")); dist.Set("tone", Get("dsttone")); dist.Set("mix", Get("dstmix")); dist.Set("level", -3);
        chor.Prepare(SR); chor.Set("rate", Get("chrate")); chor.Set("depth", Get("chdepth")); chor.Set("mix", Get("chmix"));
        dly.Prepare(SR); dly.Set("mode", 2); dly.Set("sync", Get("dlsync")); dly.Set("fb", Get("dlfb")); dly.Set("mix", Get("dlmix"));
        rev.Prepare(SR); rev.Set("size", Get("rvsize")); rev.Set("damp", Get("rvdamp")); rev.Set("mix", Get("rvmix"));
        boost.Prepare(SR); boost.Set("freq", Get("bbfreq")); boost.Set("gain", Get("bbgain")); boost.Set("q", Get("bbq")); boost.Set("harm", Get("bbharm")); boost.Set("tight", Get("bbtight"));
    }

    public override void Reset()
    {
        base.Reset();
        foreach (var v in voices) { v.On = false; v.Released = false; for (int i = 0; i < 4; i++) { v.Env[i] = 0; v.Stage[i] = 0; } Array.Clear(v.F, 0, v.F.Length); }
        held.Clear(); sustain = false; dist.Reset(); chor.Reset(); dly.Reset(); rev.Reset(); boost.Reset();
    }

    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }

    // ---------------- notes ----------------
    void NoteOnV(int note, int vel)
    {
        held.Remove(note); held.Add(note); var v = AllocVoice(note);
        bool legatoOverlap = mode == 2 && v.On && !v.Released && v.Stage[0] != 0;
        v.Note = note; v.Vel = vel / 127f; v.Age = ++ageCounter; v.Released = false; v.HeldBySustain = false;
        if (!v.On || glide <= 0 || mode == 0) v.Cur = note;
        if (!legatoOverlap)
        {
            v.Time = 0; for (int i = 0; i < 4; i++) { v.Stage[i] = 1; if (!v.On) v.Env[i] = 0; }
            for (int k = 0; k < 4; k++) { if (lp[k].Retrig) { v.Lfo[k] = lp[k].Phase; v.LfoSm[k] = 0; v.LfoHold[k] = RandBi(); } }
            for (int o = 0; o < 3; o++) for (int u = 0; u < 7; u++) v.Ph[o, u] = op[o].Phase + (u == 0 ? 0 : RandUni() * (op[o].Phase > 0 ? 0 : 1));
            if (!v.On) { Array.Clear(v.F, 0, v.F.Length); }
        }
        v.On = true;
    }

    void NoteOffV(int note)
    {
        held.Remove(note);
        if (mode != 0)
        {
            var v = voices[0];
            if (held.Count > 0 && v.On && v.Note == note) { int prev = held[held.Count - 1]; v.Note = prev; if (mode == 1) { v.Time = 0; for (int i = 0; i < 4; i++) v.Stage[i] = 1; } return; }
            if (v.On && v.Note == note) ReleaseVoice(v);
            return;
        }
        foreach (var v in voices) if (v.On && !v.Released && v.Note == note) ReleaseVoice(v);
    }
    void ReleaseVoice(Voice v) { if (sustain) { v.HeldBySustain = true; return; } v.Released = true; for (int i = 0; i < 4; i++) if (v.Stage[i] != 0) v.Stage[i] = 4; }

    Voice AllocVoice(int note)
    {
        if (mode != 0) return voices[0];
        foreach (var v in voices) if (!v.On) return v;
        Voice best = null; foreach (var v in voices) if (v.Released && (best == null || v.Age < best.Age)) best = v;
        if (best == null) foreach (var v in voices) if (best == null || v.Age < best.Age) best = v;
        for (int i = 0; i < 4; i++) { best.Env[i] = 0; }   // stolen: restart cleanly
        best.On = false; return best;
    }

    void Midi(MidiEv m)
    {
        int s = m.S & 0xF0;
        if (s == 0x90 && m.D2 > 0) NoteOnV(m.D1, m.D2);
        else if (s == 0x80 || s == 0x90) NoteOffV(m.D1);
        else if (s == 0xB0 && m.D1 == 123) { held.Clear(); foreach (var v in voices) if (v.On) { v.HeldBySustain = false; ReleaseVoice(v); } }
        else if (s == 0xB0 && m.D1 == 120) { held.Clear(); foreach (var v in voices) { v.On = false; } }
        else if (s == 0xB0 && m.D1 == 64) { bool on = m.D2 >= 64; if (!on && sustain) { sustain = false; foreach (var v in voices) if (v.On && v.HeldBySustain) { v.HeldBySustain = false; ReleaseVoice(v); } } sustain = on; }
        else if (s == 0xE0) bend = ((m.D2 << 7 | m.D1) - 8192) / 8192.0;
    }

    // ---------------- helpers ----------------
    double RandUni() { nz ^= nz << 13; nz ^= nz >> 17; nz ^= nz << 5; return (nz & 0xFFFFFF) / 16777216.0; }
    double RandBi() { return RandUni() * 2 - 1; }
    static double Blep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }
    static double Frac(double x) { return x - Math.Floor(x); }
    static double Shape(int k, double p, double dt)
    {
        switch (k)
        {
            case 0: return Math.Sin(6.283185307179586 * p);
            case 1: return 4 * Math.Abs(p - 0.5) - 1;
            case 2: return 2 * p - 1 - Blep(p, dt);
            case 3: return (p < 0.5 ? 1.0 : -1.0) + Blep(p, dt) - Blep(Frac(p + 0.5), dt);
            default: return (p < 0.25 ? 1.0 : -1.0) + Blep(p, dt) - Blep(Frac(p + 0.75), dt);
        }
    }
    public static double Morph(double wt, double p, double dt)
    {
        double pos = Math.Max(0, Math.Min(0.99999, wt)) * 4; int i = (int)pos; double f = pos - i;
        double a = Shape(i, p, dt); return f < 1e-4 ? a : a * (1 - f) + Shape(i + 1, p, dt) * f;
    }
    // one oscillator sample incl. warp (also used by the window's wave display)
    public static double OscSample(double wt, int warp, double wamt, double p, double dt)
    {
        switch (warp)
        {
            case 1: { double ratio = 1 + wamt * 7; return Morph(wt, Frac(p * ratio), Math.Min(0.4, dt * ratio)); }
            case 2: { double k = 1 + wamt * 3; return Morph(wt, Math.Pow(p, k), dt); }
            case 3: { double y = Morph(wt, p, dt); return wamt < 0.001 ? y : Math.Sin(y * (1 + wamt * 5) * 1.5707963); }
            default: return Morph(wt, p, dt);
        }
    }
    static double LfoShape(int shape, double p, double hold)
    {
        switch (shape)
        {
            case 0: return Math.Sin(6.283185307179586 * p);
            case 1: return 4 * Math.Abs(p - 0.5) - 1;
            case 2: return 2 * p - 1;
            case 3: return 1 - 2 * p;
            case 4: return p < 0.5 ? 1 : -1;
            case 5: return hold;
            default: return Math.Abs(Math.Sin(3.141592653589793 * p)) * 2 - 1;   // Wub: rectified sine, two humps per cycle
        }
    }

    double LfoHz(int k, double bpm) { var l = lp[k]; if (l.Sync == 0) return l.Rate; return bpm / 60.0 / SynthTables.SyncBeats[l.Sync]; }

    // ---------------- rendering ----------------
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (n > outL.Length) { outL = new float[n]; outR = new float[n]; }
        Array.Clear(outL, 0, n); Array.Clear(outR, 0, n);
        int pos = 0, ei = 0; var ev = pending; pending = new List<KeyValuePair<int, MidiEv>>();
        while (pos < n)
        {
            while (ei < ev.Count && ev[ei].Key <= pos) { Midi(ev[ei].Value); ei++; }
            int next = ei < ev.Count ? Math.Min(n, ev[ei].Key) : n; int len = Math.Min(next - pos, CTRL); if (len <= 0) { if (ei < ev.Count) { Midi(ev[ei].Value); ei++; } continue; }
            RenderBlock(pos, len); pos += len;
        }
        while (ei < ev.Count) { Midi(ev[ei].Value); ei++; }
        // effects on the summed voices
        if (dstOn) dist.Process(outL, outR, n);
        if (chOn) chor.Process(outL, outR, n);
        if (dlOn) dly.Process(outL, outR, n);
        if (rvOn) rev.Process(outL, outR, n);
        if (bbOn) boost.Process(outL, outR, n);
        for (int i = 0; i < n; i++) { l[i] += Soft(outL[i]); r[i] += Soft(outR[i]); }
    }
    static float Soft(float x) { float a = Math.Abs(x); if (a < 0.8f) return x; float s = x < 0 ? -1f : 1f; return s * (0.8f + 0.2f * (float)Math.Tanh((a - 0.8f) / 0.2f)); }

    double[] src = new double[14], dst = new double[24];
    double[] ofreq = new double[3], olvl = new double[3], owt = new double[3], owa = new double[3], fg = new double[2], fk = new double[2], fdr = new double[2], onrm = new double[3], opl = new double[3], opr = new double[3];
    double[,] ufreq = new double[3, 7], ugl = new double[3, 7], ugr = new double[3, 7], uw = new double[3, 7];

    void RenderBlock(int start, int len)
    {
        double bpm = Fx.ProjectBpm, dtc = len / (double)SR;
        for (int k = 0; k < 4; k++) { gLfo[k] = Frac(gLfo[k] + LfoHz(k, bpm) * dtc); }
        double volLin = DspMath.DbToLin(vol);
        foreach (var v in voices)
        {
            if (!v.On) continue;
            // ---- envelopes (control rate) ----
            for (int e = 1; e < 4; e++) StepEnv(v, e, len);   // envelope 1 (amp) is stepped per sample below
            if (v.Stage[0] == 0 && v.Released) { v.On = false; continue; }
            v.Time += dtc;
            // ---- modulation sources ----
            for (int k = 0; k < 4; k++)
            {
                var lf = lp[k]; double hz = LfoHz(k, bpm), ph;
                if (lf.Retrig)
                {
                    double before = v.Lfo[k]; v.Lfo[k] = Frac(before + hz * dtc); if (v.Lfo[k] < before) v.LfoHold[k] = RandBi(); ph = v.Lfo[k];
                }
                else { ph = Frac(gLfo[k] + lf.Phase); if (ph < Frac(gLfo[k] - hz * dtc + lf.Phase) && lf.Shape == 5) v.LfoHold[k] = RandBi(); }
                double x = LfoShape(lf.Shape, ph, v.LfoHold[k]);
                if (lf.Uni) x = (x + 1) * 0.5;
                if (lf.Smooth > 0) { double a = 1 - Math.Exp(-dtc / (0.002 + lf.Smooth * 0.15)); v.LfoSm[k] += (x - v.LfoSm[k]) * a; x = v.LfoSm[k]; }
                if (lf.Fade > 0) x *= Math.Min(1.0, v.Time * 1000.0 / lf.Fade);
                v.LfoVal[k] = x; src[1 + k] = x;
            }
            src[5] = v.Env[1]; src[6] = v.Env[2]; src[7] = v.Env[3]; src[8] = v.Vel; src[9] = Math.Max(-1, Math.Min(1, (v.Note - 60) / 36.0));
            for (int m = 0; m < 4; m++) src[10 + m] = macro[m];
            Array.Clear(dst, 0, dst.Length);
            for (int s = 0; s < 8; s++) if (mSrc[s] != 0 && mDst[s] != 0) dst[mDst[s]] += src[mSrc[s]] * mAmt[s];
            // ---- glide / pitch ----
            if (glide > 0 && mode != 0) v.Cur += (v.Note - v.Cur) * (1 - Math.Exp(-len / (glide * 0.001 * SR))); else v.Cur = v.Note;
            double note = v.Cur + tune / 100.0 + bend * bendRange + dst[5] * 24.0;
            double velAmp = (1 - vsens) + vsens * v.Vel;
            // ---- oscillator setup ----
            for (int o = 0; o < 3; o++)
            {
                var P = op[o]; if (!P.On) { olvl[o] = 0; continue; }
                double nn = note + P.Oct * 12 + P.Sem + P.Fin / 100.0 + dst[2 + o] * 24.0;
                ofreq[o] = 440.0 * Math.Pow(2, (nn - 69) / 12.0);
                olvl[o] = Math.Max(0, Math.Min(1, P.Lvl + dst[12 + o])); owt[o] = Math.Max(0, Math.Min(1, P.Wt + dst[6 + o])); owa[o] = Math.Max(0, Math.Min(1, P.WarpAmt + dst[9 + o]));
            }
            double detMod = Math.Max(0, 1 + dst[22]);
            for (int o = 0; o < 3; o++)
            {
                if (olvl[o] <= 0) continue; var P = op[o]; int uni = (int)P.Uni; double wsum = 0;
                for (int u = 0; u < uni; u++)
                {
                    double rel = uni == 1 ? 0 : (u - (uni - 1) / 2.0) / ((uni - 1) / 2.0);
                    ufreq[o, u] = ofreq[o] * Math.Pow(2, rel * P.Det * detMod / 1200.0);
                    double w = uni == 1 ? 1.0 : ((uni % 2 == 1 && u == (uni - 1) / 2) ? 1.0 : (uni % 2 == 1 ? P.Blend : 1.0)); uw[o, u] = w; wsum += w * w;
                    double pn = rel * 0.8; ugl[o, u] = Math.Sqrt(0.5 * (1 - pn)); ugr[o, u] = Math.Sqrt(0.5 * (1 + pn));
                }
                onrm[o] = olvl[o] / Math.Sqrt(wsum); opl[o] = Math.Sqrt(0.5 * (1 - P.Pan)) * 1.4142; opr[o] = Math.Sqrt(0.5 * (1 + P.Pan)) * 1.4142;
            }
            double subF = 440.0 * Math.Pow(2, (note + subOct * 12 - 69) / 12.0), subL = Math.Max(0, Math.Min(1, subLvl + dst[15])), nzL = Math.Max(0, Math.Min(1, nzLvl + dst[16]));
            // ---- filter setup ----
            for (int k = 0; k < 2; k++)
            {
                var F = fp[k]; double cut = F.Cut * Math.Pow(2, dst[k == 0 ? 17 : 20] * 5.0 + (v.Note - 60) / 12.0 * F.Key);
                cut = Math.Max(20, Math.Min(SR * 0.45, cut)); fg[k] = Math.Tan(Math.PI * cut / SR);
                double res = Math.Max(0, Math.Min(1, F.Res + dst[k == 0 ? 18 : 21])); fk[k] = 2 - 1.96 * res;
                fdr[k] = 1 + Math.Max(0, Math.Min(1, F.Drive + (k == 0 ? dst[19] : 0))) * 7;
            }
            double gain = volLin * velAmp * 0.8 * Math.Max(0, 1 + dst[1]);
            double pan = Math.Max(-1, Math.Min(1, dst[23]));
            // ---- per-sample ----
            for (int i = 0; i < len; i++)
            {
                double aenv = v.Env[0];
                double sl = 0, sr = 0;
                for (int o = 0; o < 3; o++)
                {
                    if (olvl[o] <= 0) continue; var P = op[o]; int uni = (int)P.Uni; double acc0 = 0, acc1 = 0;
                    for (int u = 0; u < uni; u++)
                    {
                        double dt = Math.Min(0.45, ufreq[o, u] / SR);
                        double p = v.Ph[o, u] + dt; if (p >= 1) p -= 1; v.Ph[o, u] = p;
                        double y = OscSample(owt[o], P.Warp, owa[o], p, dt) * uw[o, u];
                        acc0 += y * ugl[o, u]; acc1 += y * ugr[o, u];
                    }
                    sl += acc0 * onrm[o] * opl[o]; sr += acc1 * onrm[o] * opr[o];
                }
                if (subOn && subL > 0)
                {
                    v.SubPh += subF / SR; if (v.SubPh >= 1) v.SubPh -= 1; double sp = v.SubPh, sy = subWave == 0 ? Math.Sin(6.283185307179586 * sp) : (subWave == 1 ? 4 * Math.Abs(sp - 0.5) - 1 : (sp < 0.5 ? 0.7 : -0.7));
                    sl += sy * subL * 0.8; sr += sy * subL * 0.8;
                }
                if (nzOn && nzL > 0)
                {
                    double w0 = RandBi(), nv = w0;
                    if (nzType == 1) { v.Pink0 = 0.99765 * v.Pink0 + w0 * 0.0990460; v.Pink1 = 0.96300 * v.Pink1 + w0 * 0.2965164; v.Pink2 = 0.57000 * v.Pink2 + w0 * 1.0526913; nv = (v.Pink0 + v.Pink1 + v.Pink2 + w0 * 0.1848) * 0.25; }
                    sl += nv * nzL * 0.5; sr += nv * nzL * 0.5;
                }
                // filters
                double xl = sl, xr = sr;
                if (fp[0].On || fp[1].On)
                {
                    double al = 0, ar = 0, bl = xl, br = xr;
                    if (fp[0].On) { al = Filter(v, 0, fp[0].Type, fg[0], fk[0], fdr[0], xl, 0); ar = Filter(v, 0, fp[0].Type, fg[0], fk[0], fdr[0], xr, 1); }
                    if (parallel) { if (fp[1].On) { bl = Filter(v, 1, fp[1].Type, fg[1], fk[1], fdr[1], xl, 0); br = Filter(v, 1, fp[1].Type, fg[1], fk[1], fdr[1], xr, 1); } xl = fp[0].On && fp[1].On ? (al + bl) * 0.5 : (fp[0].On ? al : bl); xr = fp[0].On && fp[1].On ? (ar + br) * 0.5 : (fp[0].On ? ar : br); }
                    else
                    {
                        xl = fp[0].On ? al : xl; xr = fp[0].On ? ar : xr;
                        if (fp[1].On) { xl = Filter(v, 1, fp[1].Type, fg[1], fk[1], fdr[1], xl, 0); xr = Filter(v, 1, fp[1].Type, fg[1], fk[1], fdr[1], xr, 1); }
                    }
                }
                // amp envelope (per sample, exponential smoothing keeps it click-free)
                double g = gain * aenv; double pl2 = Math.Sqrt(0.5 * (1 - pan)) * 1.4142, pr2 = Math.Sqrt(0.5 * (1 + pan)) * 1.4142;
                outL[start + i] += (float)(xl * g * pl2); outR[start + i] += (float)(xr * g * pr2);
                StepAmp(v);
            }
        }
    }

    // state-variable filter (TPT), mono per call
    double Filter(Voice v, int fi, int type, double g, double k, double drive, double x, int ch)
    {
        int b = fi * 8 + ch * 2; bool two = type == 1;
        if (drive > 1.001) x = Math.Tanh(x * drive) / Math.Tanh(drive) * 1.0;
        double y = Svf(v, b, type, g, k, x);
        if (two) y = Svf(v, b + 4, type, g, k, y);
        return y;
    }
    static double Svf(Voice v, int b, int type, double g, double k, double x)
    {
        double a1 = 1 / (1 + g * (g + k)), a2 = g * a1, a3 = g * a2;
        double ic1 = v.F[b], ic2 = v.F[b + 1], v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
        v.F[b] = 2 * v1 - ic1; v.F[b + 1] = 2 * v2 - ic2;
        switch (type) { case 0: case 1: return v2; case 2: return x - k * v1 - v2; case 3: return v1; default: return x - k * v1; }
    }

    // ADSR: attack is linear, decay and release are exponential
    void StepEnv(Voice v, int e, int len)
    {
        var P = ep[e]; double sr = SR;
        switch (v.Stage[e])
        {
            case 1: { if (P.A <= 0.5) { v.Env[e] = 1; v.Stage[e] = 2; } else { v.Env[e] += len / (P.A * 0.001 * sr); if (v.Env[e] >= 1) { v.Env[e] = 1; v.Stage[e] = 2; } } break; }
            case 2: { double c = Math.Exp(-4.6 * len / (Math.Max(1, P.D) * 0.001 * sr)); v.Env[e] = P.S + (v.Env[e] - P.S) * c; if (Math.Abs(v.Env[e] - P.S) < 0.0005) { v.Env[e] = P.S; v.Stage[e] = 3; } break; }
            case 3: v.Env[e] = P.S; break;
            case 4: { double c = Math.Exp(-4.6 * len / (Math.Max(1, P.R) * 0.001 * sr)); v.Env[e] *= c; if (v.Env[e] < 0.0003) { v.Env[e] = 0; v.Stage[e] = 0; } break; }
        }
    }
    // the amp envelope also moves per sample inside a block so notes start and end without clicks
    void StepAmp(Voice v)
    {
        var P = ep[0]; double sr = SR;
        switch (v.Stage[0])
        {
            case 1: if (P.A <= 0.5) { v.Env[0] = 1; v.Stage[0] = 2; } else { v.Env[0] += 1.0 / (P.A * 0.001 * sr); if (v.Env[0] >= 1) { v.Env[0] = 1; v.Stage[0] = 2; } } break;
            case 2: { double c = Math.Exp(-4.6 / (Math.Max(1, P.D) * 0.001 * sr)); v.Env[0] = P.S + (v.Env[0] - P.S) * c; break; }
            case 3: v.Env[0] = P.S; break;
            case 4: v.Env[0] *= Math.Exp(-4.6 / (Math.Max(1, P.R) * 0.001 * sr)); if (v.Env[0] < 0.0003) { v.Env[0] = 0; v.Stage[0] = 0; v.On = false; } break;
        }
    }

    public int ActiveVoices { get { int c = 0; foreach (var v in voices) if (v.On) c++; return c; } }

    // ---- window ----
    public override void ShowWindow(IWin32Window owner)
    {
        if (window != null && !window.IsDisposed) { window.Activate(); window.WindowState = FormWindowState.Normal; return; }
        window = new SynthWindow(this, Owner); window.Show(owner);
    }
    public override void CloseWindow() { try { if (window != null && !window.IsDisposed) window.Close(); } catch { } window = null; }
    public override string WindowLabel { get { return "Open Bass Lab (the synthesizer)"; } }
}
