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
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("level", "Output level", -12, 6, -12, "dB");   // starts at -12 dB: big bass that leaves room for the vocals (and no clipping)
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
//  Bass Lab: a polyphonic wavetable synthesizer for bass and leads.
// =====================================================================================================
static class SynthTables
{
    public static readonly string[] FilterTypes = { "Low-pass", "High-pass", "Band-pass", "Notch", "Peak", "Low shelf", "High shelf", "Comb +", "Comb -", "Formant" };
    public static readonly string[] Slopes = { "12 dB", "24 dB", "36 dB", "48 dB" };
    public static readonly string[] Routes = { "Filter 1", "Filter 2", "Both", "Direct" };
    public static readonly string[] LfoShapes = { "Sine", "Triangle", "Saw up", "Saw down", "Square", "Random", "Wub", "Custom (drawn)" };
    public static readonly string[] Syncs = { "Hz", "4 bars", "2 bars", "1 bar", "1/2", "1/4", "1/4 dot", "1/4 trip", "1/8", "1/8 dot", "1/8 trip", "1/16", "1/16 dot", "1/16 trip", "1/32" };
    public static readonly double[] SyncBeats = { 0, 16, 8, 4, 2, 1, 1.5, 2.0 / 3, 0.5, 0.75, 1.0 / 3, 0.25, 0.375, 1.0 / 6, 0.125 };
    public static readonly string[] Sources = { "None", "LFO 1", "LFO 2", "LFO 3", "LFO 4", "Env 2", "Env 3", "Env 4", "Velocity", "Key", "Macro 1", "Macro 2", "Macro 3", "Macro 4" };
    public static readonly string[] Dests = { "None", "Volume", "Osc A pitch", "Osc B pitch", "Osc C pitch", "All pitch", "Osc A wave", "Osc B wave", "Osc C wave", "Osc A warp", "Osc B warp", "Osc C warp",
        "Osc A level", "Osc B level", "Osc C level", "Sub level", "Noise level", "Filter 1 cutoff", "Filter 1 resonance", "Filter 1 drive", "Filter 2 cutoff", "Filter 2 resonance", "Unison detune", "Pan",
        "Osc A fine", "Osc B fine", "Osc C fine", "Osc A pan", "Osc B pan", "Osc C pan", "Osc A blend", "Osc B blend", "Osc C blend", "Filter 2 drive",
        "FX Distortion drive", "FX Distortion mix", "FX Chorus mix", "FX Delay mix", "FX Delay feedback", "FX Reverb mix", "FX Reverb size", "FX Bass boost gain", "FX Chorus depth", "FX Chorus rate",
        "Osc A FM", "Osc B FM", "Osc C FM", "Osc A ring mod", "Osc B ring mod", "Osc C ring mod", "Sub pan", "Noise pan",
        "FX Saturation drive", "FX Saturation mix", "FX Phaser rate", "FX Phaser depth", "FX Phaser mix", "FX Flanger rate", "FX Flanger depth", "FX Flanger mix", "FX Stereo width", "FX EQ mid gain" };
    // the knob each destination belongs to (null = not a knob), so a knob can show its modulation ring and accept a drop
    public static readonly string[] DestParam = { null, "vol", null, null, null, null, "awt", "bwt", "cwt", "awamt", "bwamt", "cwamt", "alvl", "blvl", "clvl", "sublvl", "nzlvl", "f1cut", "f1res", "f1drive", "f2cut", "f2res", null, null,
        "afin", "bfin", "cfin", "apan", "bpan", "cpan", "ablend", "bblend", "cblend", "f2drive", "dstdrive", "dstmix", "chmix", "dlmix", "dlfb", "rvmix", "rvsize", "bbgain", "chdepth", "chrate",
        "afm", "bfm", "cfm", "arm", "brm", "crm", "subpan", "nzpan",
        "satdrive", "satmix", "phrate", "phdepth", "phmix", "flrate", "fldepth", "flmix", "wdwidth", "eqmg" };
    public static readonly string[] Polarities = { "Bipolar", "Positive", "Negative" };
    public static readonly string[] OffOn = { "Off", "On" };
    public static readonly string[] NoiseTypes = { "White", "Pink", "Brown", "Blue", "Crackle" };
    public const int SrcNone = 0, SrcLfo1 = 1, SrcEnv2 = 5, SrcVel = 8, SrcKey = 9, SrcMacro1 = 10;
}

class SynthFx : InstrumentFx
{
    const int NV = 16, CTRL = 16;
    static readonly string[] OscIds = { "a", "b", "c" };
    static readonly int[] ModSource = { 1, 2, 0 };   // oscillator A is modulated by B, B by C, C by A (FM and ring modulation)

    // ---- cached parameters (read once when something changes) ----
    class OscP { public bool On, Mute, Solo; public double Wt, Oct, Sem, Fin, Lvl, Pan, Uni, Det, Blend, Phase, WarpAmt, Fm, Rm; public int Warp, Src, Tbl, Route; }
    class FiltP { public bool On; public int Type, Stages; public double Cut, Res, Drive, Key, Gain; }
    class EnvP { public double A, D, S, R; }
    class LfoP { public int Shape, Sync; public double Rate, Phase, Smooth, Fade; public bool Retrig, Uni, Loop = true; }
    public readonly LfoCurve[] LfoCurves = { new LfoCurve(), new LfoCurve(), new LfoCurve(), new LfoCurve() };   // the drawn shape of each LFO (used when its Shape is Custom)
    OscP[] op = { new OscP(), new OscP(), new OscP() }; FiltP[] fp = { new FiltP(), new FiltP() }; EnvP[] ep = { new EnvP(), new EnvP(), new EnvP(), new EnvP() }; LfoP[] lp = { new LfoP(), new LfoP(), new LfoP(), new LfoP() };
    bool subOn, nzOn, parallel, subMute, subSolo, nzMute, nzSolo; int subWave, subOct, nzType, subRoute, nzRoute; double subLvl, subPan, nzLvl, nzPan, vol, glide, bendRange, tune, vsens; int mode; double[] macro = new double[4];
    int[] mSrc = new int[16], mDst = new int[16], mPol = new int[16]; bool[] mOn = new bool[16]; double[] mAmt = new double[16];
    bool dstOn, chOn, dlOn, rvOn, bbOn; int maxV = 8;
    public static readonly string[] FxNames = { "Distortion", "Saturation", "Compressor", "EQ", "Phaser", "Flanger", "Chorus", "Delay", "Reverb", "Stereo width", "Bass boost" };
    public static readonly string[] FxOnIds = { "dston", "saton", "cpon", "eqon", "phon", "flon", "chon", "dlon", "rvon", "wdon", "bbon" };
    readonly bool[] fxOn = new bool[11]; readonly int[] fxOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; float[] dryL = new float[8192], dryR = new float[8192]; double eqMix = 1, wdMix = 1;

    // ---- voices ----
    class Voice
    {
        public bool On, Released, HeldBySustain; public int Note; public float Vel; public long Age; public double Cur;
        public double[,] Ph = new double[3, 7]; public double SubPh; public double[] LastOut = new double[3];
        public double[] F = new double[64];                  // filter state: [filter * 32 + stage * 4 + ch * 2 + {0,1}]
        public float[][] Comb = { new float[2048], new float[2048], new float[2048], new float[2048] }; public int[] CombW = new int[4];
        public double[] Env = new double[4]; public int[] Stage = new int[4];
        public double[] Lfo = new double[4], LfoVal = new double[4], LfoHold = new double[4]; public double[] LfoSm = new double[4], LfoAcc = new double[4]; public double Time;
        public double Pink0, Pink1, Pink2, Brown, PrevW;
    }
    Voice[] voices = new Voice[NV];
    long ageCounter; double[] gLfo = new double[4]; uint nz = 2463534242;
    readonly List<int> held = new List<int>(); bool sustain; double bend; List<KeyValuePair<int, MidiEv>> pending = new List<KeyValuePair<int, MidiEv>>();
    float[] outL = new float[8192], outR = new float[8192];
    Distortion dist = new Distortion(); Chorus chor = new Chorus(); Delay dly = new Delay(); Reverb rev = new Reverb(); BassBoost boost = new BassBoost();
    Saturation sat = new Saturation(); Compressor comp = new Compressor(); ParametricEq eq = new ParametricEq(); Phaser phs = new Phaser(); Flanger fln = new Flanger(); Widener wid = new Widener();
    SynthWindow window; public string PresetName = "Wobble Bass";
    public volatile float OutPeakL, OutPeakR; public readonly float[] SrcPeak = new float[5];   // level meters (OSC A, B, C, SUB, NOISE)
    readonly float[] blockPeak = new float[5];

    public SynthFx()
    {
        Type = "synth"; Title = "Bass Lab"; Category = "Instruments"; Blurb = "Wavetable synthesizer for bass and leads: warp, FM, filters, wobble LFOs, bass boost";
        for (int i = 0; i < NV; i++) voices[i] = new Voice();
        Wavetables.WarmUp();
        BuildParams(); SynthPresets.Apply(this, SynthPresets.Names[0], false); dirty = true;
    }
    public override bool IsInstrument { get { return true; } }
    // settings that belong to you and your playing, not to a sound: they stay the same when you change preset and when the app restarts
    public static readonly string[] GlobalIds = { "voices", "tune", "bend", "vsens" };
    static string GlobalFile { get { return System.IO.Path.Combine(StudioEnv.DataDir, "synthglobal.txt"); } }
    public void SaveGlobals() { try { var sb = new System.Text.StringBuilder(); foreach (var id in GlobalIds) sb.Append(id).Append('=').Append(Get(id).ToString("R", CultureInfo.InvariantCulture)).Append(';'); System.IO.Directory.CreateDirectory(StudioEnv.DataDir); System.IO.File.WriteAllText(GlobalFile, sb.ToString()); } catch { } }
    public void ApplyGlobals() { try { if (!System.IO.File.Exists(GlobalFile)) return; foreach (var part in System.IO.File.ReadAllText(GlobalFile).Split(';')) { int e = part.IndexOf('='); float v; if (e > 0 && float.TryParse(part.Substring(e + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { string id = part.Substring(0, e); if (Array.IndexOf(GlobalIds, id) >= 0) Set(id, v); } } } catch { } }
    // everything back to factory: knobs, the four LFO shapes and the three drawn waves
    public void ResetAll(bool customWaves = true) { ResetDefaults(); foreach (var c in LfoCurves) c.Load("Sine"); if (customWaves) for (int i = 0; i < 3; i++) Wavetables.SetCustom(i, new[] { Wavetables.DefaultFrame() }); }

    void BuildParams()
    {
        AddP("vol", "Volume", -30, 6, -8, "dB"); AddP("glide", "Glide", 0, 1000, 0, "ms"); AddP("bend", "Bend range", 0, 24, 2, "st");
        AddC("mode", "Voices", new[] { "Poly", "Mono", "Legato" }, 0); AddP("voices", "Voice limit", 1, 16, 8, ""); AddP("tune", "Fine tune", -100, 100, 0, "ct"); AddP("vsens", "Velocity", 0, 100, 60, "%");
        foreach (var x in OscIds)
        {
            AddC(x + "on", "On", SynthTables.OffOn, x == "a" ? 1 : 0); AddC(x + "src", "Source", Wavetables.Sources, 0); AddC(x + "tbl", "Wavetable", Wavetables.TableNames, 0);
            AddP(x + "wt", "Wave", 0, 100, 50, "%"); AddP(x + "oct", "Octave", -3, 3, 0, ""); AddP(x + "sem", "Semi", -12, 12, 0, "");
            AddP(x + "fin", "Fine", -100, 100, 0, "ct"); AddP(x + "lvl", "Level", 0, 100, 80, "%"); AddP(x + "pan", "Pan", -100, 100, 0, "");
            AddP(x + "uni", "Unison", 1, 7, 1, ""); AddP(x + "det", "Detune", 0, 100, 20, "ct"); AddP(x + "blend", "Blend", 0, 100, 70, "%"); AddP(x + "phase", "Phase", 0, 360, 0, "deg");
            AddC(x + "warp", "Warp", Wavetables.Warps, 0); AddP(x + "wamt", "Warp amount", 0, 100, 0, "%");
            AddP(x + "fm", "FM", 0, 100, 0, "%"); AddP(x + "rm", "Ring mod", 0, 100, 0, "%");
            AddC(x + "mute", "Mute", SynthTables.OffOn, 0); AddC(x + "solo", "Solo", SynthTables.OffOn, 0); AddC(x + "route", "Goes to", SynthTables.Routes, 0);
        }
        AddC("subon", "Sub on", SynthTables.OffOn, 0); AddC("subwave", "Sub wave", new[] { "Sine", "Triangle", "Square", "Saw" }, 0); AddP("suboct", "Sub octave", -2, 0, -1, ""); AddP("sublvl", "Sub level", 0, 100, 70, "%"); AddP("subpan", "Sub pan", -100, 100, 0, "");
        AddC("submute", "Mute", SynthTables.OffOn, 0); AddC("subsolo", "Solo", SynthTables.OffOn, 0); AddC("subroute", "Goes to", SynthTables.Routes, 3);
        AddC("nzon", "Noise on", SynthTables.OffOn, 0); AddC("nztype", "Noise type", SynthTables.NoiseTypes, 1); AddP("nzlvl", "Noise level", 0, 100, 10, "%"); AddP("nzpan", "Noise pan", -100, 100, 0, "");
        AddC("nzmute", "Mute", SynthTables.OffOn, 0); AddC("nzsolo", "Solo", SynthTables.OffOn, 0); AddC("nzroute", "Goes to", SynthTables.Routes, 0);
        for (int k = 1; k <= 2; k++)
        {
            AddC("f" + k + "on", "On", SynthTables.OffOn, k == 1 ? 1 : 0); AddC("f" + k + "type", "Type", SynthTables.FilterTypes, 0); AddC("f" + k + "slope", "Slope", SynthTables.Slopes, k == 1 ? 1 : 0);
            AddP("f" + k + "cut", "Cutoff", 20, 20000, k == 1 ? 900 : 8000, "Hz"); AddP("f" + k + "res", "Resonance", 0, 100, 25, "%"); AddP("f" + k + "drive", "Drive", 0, 100, 10, "%"); AddP("f" + k + "key", "Key track", 0, 100, 0, "%");
            AddP("f" + k + "gain", "Gain (peak / shelf)", -18, 18, 6, "dB");
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
            AddC("l" + k + "retrig", "Start", new[] { "Free", "Retrigger" }, 1); AddP("l" + k + "fade", "Fade in", 0, 3000, 0, "ms"); AddC("l" + k + "uni", "Range", new[] { "Bipolar", "Unipolar" }, 0); AddC("l" + k + "loop", "Loop", new[] { "One-shot", "Loop" }, 1);
        }
        for (int k = 1; k <= 4; k++) AddP("mac" + k, "Macro " + k, 0, 100, 0, "%");
        for (int i = 1; i <= 16; i++)
        {
            AddC("m" + i + "s", "Source", SynthTables.Sources, 0); AddC("m" + i + "d", "Destination", SynthTables.Dests, 0); AddP("m" + i + "a", "Amount", -100, 100, 0, "%"); AddC("m" + i + "p", "Polarity", SynthTables.Polarities, 0); AddC("m" + i + "e", "Enabled", SynthTables.OffOn, 1);
        }
        AddC("dston", "Distortion", SynthTables.OffOn, 0); AddC("dstmode", "Type", new[] { "Hard clip", "Foldback", "Fuzz" }, 2); AddP("dstdrive", "Drive", 0, 40, 12, "dB"); AddP("dsttone", "Tone", 500, 16000, 6000, "Hz"); AddP("dstmix", "Mix", 0, 100, 50, "%");
        AddC("chon", "Chorus", SynthTables.OffOn, 0); AddP("chrate", "Rate", 0.05f, 5, 0.8f, "Hz"); AddP("chdepth", "Depth", 0, 10, 3, "ms"); AddP("chmix", "Mix", 0, 100, 35, "%");
        AddC("dlon", "Delay", SynthTables.OffOn, 0); AddC("dlsync", "Time sync", Delay.Syncs, 3); AddP("dlfb", "Feedback", 0, 95, 30, "%"); AddP("dlmix", "Mix", 0, 100, 20, "%");
        AddC("rvon", "Reverb", SynthTables.OffOn, 0); AddP("rvsize", "Size", 0, 100, 45, "%"); AddP("rvdamp", "Damping", 0, 100, 55, "%"); AddP("rvmix", "Mix", 0, 100, 15, "%");
        AddC("bbon", "Bass boost", SynthTables.OffOn, 1); AddP("bbfreq", "Frequency", 30, 200, 60, "Hz"); AddP("bbgain", "Boost", 0, 24, 6, "dB"); AddP("bbq", "Width (Q)", 0.3f, 2f, 0.7f, ""); AddP("bbharm", "Harmonics", 0, 100, 20, "%"); AddP("bbtight", "Tighten", 10, 60, 22, "Hz");
        AddC("saton", "Saturation", SynthTables.OffOn, 0); AddC("satmode", "Character", new[] { "Tape", "Tube", "Soft clip" }, 0); AddP("satdrive", "Drive", 0, 30, 8, "dB"); AddP("sattone", "Tone", 500, 20000, 12000, "Hz"); AddP("satmix", "Mix", 0, 100, 100, "%");
        AddC("cpon", "Compressor", SynthTables.OffOn, 0); AddP("cpthr", "Threshold", -60, 0, -18, "dB"); AddP("cpratio", "Ratio", 1, 20, 3, ":1"); AddP("cpatt", "Attack", 0.1f, 200, 10, "ms"); AddP("cprel", "Release", 10, 1500, 120, "ms"); AddP("cpmake", "Makeup", 0, 24, 3, "dB"); AddP("cpmix", "Mix", 0, 100, 100, "%");
        AddC("eqon", "EQ", SynthTables.OffOn, 0); AddP("eqlf", "Low freq", 30, 400, 100, "Hz"); AddP("eqlg", "Low gain", -18, 18, 0, "dB"); AddP("eqmf", "Mid freq", 100, 8000, 1000, "Hz"); AddP("eqmg", "Mid gain", -18, 18, 0, "dB"); AddP("eqmq", "Mid width (Q)", 0.2f, 6, 1, ""); AddP("eqhf", "High freq", 2000, 18000, 8000, "Hz"); AddP("eqhg", "High gain", -18, 18, 0, "dB"); AddP("eqmix", "Mix", 0, 100, 100, "%");
        AddC("phon", "Phaser", SynthTables.OffOn, 0); AddP("phrate", "Rate", 0.05f, 8, 0.4f, "Hz"); AddP("phdepth", "Depth", 0, 100, 70, "%"); AddP("phfb", "Feedback", 0, 90, 40, "%"); AddC("phstages", "Stages", new[] { "4", "6", "8", "12" }, 0); AddP("phmix", "Mix", 0, 100, 50, "%");
        AddC("flon", "Flanger", SynthTables.OffOn, 0); AddP("flrate", "Rate", 0.05f, 6, 0.3f, "Hz"); AddP("fldepth", "Depth", 0, 100, 60, "%"); AddP("fldelay", "Delay", 0.2f, 6, 1.5f, "ms"); AddP("flfb", "Feedback", -90, 90, 45, "%"); AddP("flmix", "Mix", 0, 100, 50, "%");
        AddC("wdon", "Stereo width", SynthTables.OffOn, 0); AddP("wdwidth", "Width", 0, 200, 140, "%"); AddP("wdbass", "Mono below", 0, 400, 120, "Hz"); AddP("wdmix", "Mix", 0, 100, 100, "%");
        for (int i = 0; i < 11; i++) AddP("fo" + i, "Chain slot " + (i + 1), 0, 10, i, "");
    }

    // ---- patches: the hand-drawn wavetables travel inside the patch text; patches saved before the filter slopes existed are converted ----
    public override string Serialize()
    {
        string s = base.Serialize();
        for (int i = 0; i < 3; i++) { string c = Wavetables.SerializeCustom(i); if (c.Length > 0) s += ";wtc" + (i + 1) + "=" + c; }
        for (int i = 0; i < 4; i++) s += ";lc" + (i + 1) + "=" + LfoCurves[i].Serialize();
        return s;
    }
    public override void Deserialize(string s)
    {
        if (s == null) return;
        var keep = new List<string>(); bool hasSlope1 = s.Contains("f1slope="), hasSlope2 = s.Contains("f2slope=");
        foreach (var part in s.Split(';'))
        {
            int e = part.IndexOf('='); if (e < 1) continue; string k = part.Substring(0, e), v = part.Substring(e + 1);
            if (k.StartsWith("wtc") && k.Length == 4) { int slot; if (int.TryParse(k.Substring(3), out slot) && slot >= 1 && slot <= 3) Wavetables.DeserializeCustom(slot - 1, v); continue; }
            if (k.Length == 3 && k.StartsWith("lc")) { int li; if (int.TryParse(k.Substring(2), out li) && li >= 1 && li <= 4) LfoCurves[li - 1].Parse(v); continue; }
            if ((k == "f1type" && !hasSlope1) || (k == "f2type" && !hasSlope2))
            {
                float old; if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out old))
                {
                    int o = (int)Math.Round(old), t = o == 1 ? 0 : (o >= 2 ? o - 1 : 0), sl = o == 1 ? 1 : 0; string f = k.Substring(0, 2);   // old: 0 LP12, 1 LP24, 2 HP, 3 BP, 4 Notch
                    keep.Add(f + "type=" + t); keep.Add(f + "slope=" + sl); continue;
                }
            }
            keep.Add(part);
        }
        base.Deserialize(string.Join(";", keep.ToArray()));
    }

    protected override void Update()
    {
        maxV = Math.Max(1, Math.Min(NV, (int)Math.Round(Get("voices"))));
        vol = Get("vol"); glide = Get("glide"); bendRange = Get("bend"); mode = (int)Get("mode"); tune = Get("tune"); vsens = Get("vsens") / 100.0;
        for (int i = 0; i < 3; i++)
        {
            string x = OscIds[i]; var o = op[i];
            o.On = Get(x + "on") > 0.5; o.Mute = Get(x + "mute") > 0.5; o.Solo = Get(x + "solo") > 0.5; o.Src = (int)Get(x + "src"); o.Tbl = (int)Get(x + "tbl"); o.Route = (int)Get(x + "route");
            o.Wt = Get(x + "wt") / 100.0; o.Oct = Math.Round(Get(x + "oct")); o.Sem = Math.Round(Get(x + "sem")); o.Fin = Get(x + "fin"); o.Lvl = Get(x + "lvl") / 100.0;
            o.Pan = Get(x + "pan") / 100.0; o.Uni = Math.Max(1, Math.Min(7, Math.Round(Get(x + "uni")))); o.Det = Get(x + "det"); o.Blend = Get(x + "blend") / 100.0; o.Phase = Get(x + "phase") / 360.0;
            o.Warp = (int)Get(x + "warp"); o.WarpAmt = Get(x + "wamt") / 100.0; o.Fm = Get(x + "fm") / 100.0; o.Rm = Get(x + "rm") / 100.0;
        }
        subOn = Get("subon") > 0.5; subWave = (int)Get("subwave"); subOct = (int)Math.Round(Get("suboct")); subLvl = Get("sublvl") / 100.0; subPan = Get("subpan") / 100.0; subMute = Get("submute") > 0.5; subSolo = Get("subsolo") > 0.5; subRoute = (int)Get("subroute");
        nzOn = Get("nzon") > 0.5; nzType = (int)Get("nztype"); nzLvl = Get("nzlvl") / 100.0; nzPan = Get("nzpan") / 100.0; nzMute = Get("nzmute") > 0.5; nzSolo = Get("nzsolo") > 0.5; nzRoute = (int)Get("nzroute");
        for (int k = 0; k < 2; k++)
        {
            var f = fp[k]; string p = "f" + (k + 1); f.On = Get(p + "on") > 0.5; f.Type = (int)Get(p + "type"); f.Stages = (int)Get(p + "slope") + 1; f.Cut = Get(p + "cut"); f.Res = Get(p + "res") / 100.0; f.Drive = Get(p + "drive") / 100.0; f.Key = Get(p + "key") / 100.0; f.Gain = DspMath.DbToLin(Get(p + "gain"));
        }
        parallel = Get("froute") > 0.5;
        for (int k = 0; k < 4; k++) { var e = ep[k]; string p = "e" + (k + 1); e.A = Get(p + "a"); e.D = Get(p + "d"); e.S = Get(p + "s") / 100.0; e.R = Get(p + "r"); }
        for (int k = 0; k < 4; k++)
        {
            var l = lp[k]; string p = "l" + (k + 1); l.Shape = (int)Get(p + "shape"); l.Sync = (int)Get(p + "sync"); l.Rate = Get(p + "rate"); l.Phase = Get(p + "phase") / 100.0; l.Smooth = Get(p + "smooth") / 100.0;
            l.Retrig = Get(p + "retrig") > 0.5; l.Fade = Get(p + "fade"); l.Uni = Get(p + "uni") > 0.5; l.Loop = Get(p + "loop") > 0.5; macro[k] = Get("mac" + (k + 1)) / 100.0;
        }
        for (int i = 0; i < 16; i++) { mSrc[i] = (int)Get("m" + (i + 1) + "s"); mDst[i] = (int)Get("m" + (i + 1) + "d"); mAmt[i] = Get("m" + (i + 1) + "a") / 100.0; mPol[i] = (int)Get("m" + (i + 1) + "p"); mOn[i] = Get("m" + (i + 1) + "e") > 0.5; }
        dstOn = Get("dston") > 0.5; chOn = Get("chon") > 0.5; dlOn = Get("dlon") > 0.5; rvOn = Get("rvon") > 0.5; bbOn = Get("bbon") > 0.5;
        dist.Prepare(SR); dist.Set("mode", Get("dstmode")); dist.Set("drive", Get("dstdrive")); dist.Set("tone", Get("dsttone")); dist.Set("mix", Get("dstmix")); dist.Set("level", -3);
        chor.Prepare(SR); chor.Set("rate", Get("chrate")); chor.Set("depth", Get("chdepth")); chor.Set("mix", Get("chmix"));
        dly.Prepare(SR); dly.Set("mode", 2); dly.Set("sync", Get("dlsync")); dly.Set("fb", Get("dlfb")); dly.Set("mix", Get("dlmix"));
        rev.Prepare(SR); rev.Set("size", Get("rvsize")); rev.Set("damp", Get("rvdamp")); rev.Set("mix", Get("rvmix"));
        boost.Prepare(SR); boost.Set("level", 0); boost.Set("freq", Get("bbfreq"));   /* (the boost inside Bass Lab keeps its level: only a Bass Boost plugin starts at -12 dB) */ boost.Set("gain", Get("bbgain")); boost.Set("q", Get("bbq")); boost.Set("harm", Get("bbharm")); boost.Set("tight", Get("bbtight"));
        sat.Prepare(SR); sat.Set("mode", Get("satmode")); sat.Set("drive", Get("satdrive")); sat.Set("tone", Get("sattone")); sat.Set("mix", Get("satmix")); sat.Set("out", 0);
        comp.Prepare(SR); comp.Set("thr", Get("cpthr")); comp.Set("ratio", Get("cpratio")); comp.Set("att", Get("cpatt")); comp.Set("rel", Get("cprel")); comp.Set("makeup", Get("cpmake")); comp.Set("mix", Get("cpmix"));
        eq.Prepare(SR); for (int b = 0; b < 6; b++) eq.Set("b" + b + "_on", b < 3 ? 1 : 0); eq.Set("b0_t", 1); eq.Set("b0_f", Get("eqlf")); eq.Set("b0_g", Get("eqlg")); eq.Set("b1_t", 0); eq.Set("b1_f", Get("eqmf")); eq.Set("b1_g", Get("eqmg")); eq.Set("b1_q", Get("eqmq")); eq.Set("b2_t", 2); eq.Set("b2_f", Get("eqhf")); eq.Set("b2_g", Get("eqhg")); eqMix = Get("eqmix") / 100.0;
        phs.Prepare(SR); phs.Set("rate", Get("phrate")); phs.Set("depth", Get("phdepth")); phs.Set("fb", Get("phfb")); phs.Set("stages", Get("phstages")); phs.Set("mix", Get("phmix"));
        fln.Prepare(SR); fln.Set("rate", Get("flrate")); fln.Set("depth", Get("fldepth")); fln.Set("delay", Get("fldelay")); fln.Set("fb", Get("flfb")); fln.Set("mix", Get("flmix"));
        wid.Prepare(SR); wid.Set("width", Get("wdwidth")); wid.Set("bass", Get("wdbass")); wdMix = Get("wdmix") / 100.0;
        for (int e = 0; e < 11; e++) fxOn[e] = Get(FxOnIds[e]) > 0.5;
        { var seen = new bool[11]; bool ok = true; var tmp = new int[11]; for (int s = 0; s < 11; s++) { int v = (int)Math.Round(Get("fo" + s)); if (v < 0 || v > 10 || seen[v]) { ok = false; break; } seen[v] = true; tmp[s] = v; } for (int s = 0; s < 11; s++) fxOrder[s] = ok ? tmp[s] : s; }
    }

    public override void Reset()
    {
        base.Reset();
        foreach (var v in voices) { v.On = false; v.Released = false; for (int i = 0; i < 4; i++) { v.Env[i] = 0; v.Stage[i] = 0; } Array.Clear(v.F, 0, v.F.Length); foreach (var c in v.Comb) Array.Clear(c, 0, c.Length); }
        held.Clear(); sustain = false; dist.Reset(); chor.Reset(); dly.Reset(); rev.Reset(); boost.Reset(); sat.Reset(); comp.Reset(); eq.Reset(); phs.Reset(); fln.Reset(); wid.Reset();
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
            for (int k = 0; k < 4; k++) { if (lp[k].Retrig) { v.Lfo[k] = lp[k].Phase; v.LfoAcc[k] = 0; v.LfoSm[k] = 0; v.LfoHold[k] = RandBi(); } }
            for (int o = 0; o < 3; o++) for (int u = 0; u < 7; u++) v.Ph[o, u] = op[o].Phase + (u == 0 ? 0 : RandUni() * (op[o].Phase > 0 ? 0 : 1));
            if (!v.On) { Array.Clear(v.F, 0, v.F.Length); foreach (var c in v.Comb) Array.Clear(c, 0, c.Length); }
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
        for (int i = 0; i < maxV; i++) if (!voices[i].On) return voices[i];
        Voice best = null; for (int i = 0; i < maxV; i++) { var v = voices[i]; if (v.Released && (best == null || v.Age < best.Age)) best = v; }
        if (best == null) for (int i = 0; i < maxV; i++) { var v = voices[i]; if (best == null || v.Age < best.Age) best = v; }
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
    static double Frac(double x) { return x - Math.Floor(x); }
    // the oscillator as drawn in the window: same tables, same warp
    public static double OscSample(int src, int tbl, double wt, int warp, double wamt, double p)
    {
        var t = src == 0 ? Wavetables.Table(tbl) : Wavetables.ForSource(src); double pw = Wavetables.WarpPhase(warp, wamt, p); int mip = 1;
        return Wavetables.WarpOut(warp, wamt, t.Read(wt, pw, mip));
    }
    public static double LfoShape(int shape, double p, double hold, LfoCurve curve)
    {
        switch (shape)
        {
            case 0: return Math.Sin(6.283185307179586 * p);
            case 1: return 4 * Math.Abs(p - 0.5) - 1;
            case 2: return 2 * p - 1;
            case 3: return 1 - 2 * p;
            case 4: return p < 0.5 ? 1 : -1;
            case 5: return hold;
            case 7: return curve.Read(p);
            default: return Math.Abs(Math.Sin(3.141592653589793 * p)) * 2 - 1;   // Wub: rectified sine, two humps per cycle
        }
    }

    public double LfoPhaseUi(int k) { return Frac(gLfo[k] + lp[k].Phase); }   // where the LFO is now, for the playback marker
    public double LfoHzUi(int k) { return LfoHz(k, Fx.ProjectBpm); }
    double LfoHz(int k, double bpm) { var l = lp[k]; if (l.Sync == 0) return l.Rate; return bpm / 60.0 / SynthTables.SyncBeats[l.Sync]; }

    // ---------------- rendering ----------------
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (n > outL.Length) { outL = new float[n]; outR = new float[n]; }
        gAge = 0; Array.Clear(gmod, 0, gmod.Length); Array.Clear(outL, 0, n); Array.Clear(outR, 0, n); for (int i = 0; i < 5; i++) blockPeak[i] = 0;
        int pos = 0, ei = 0; var ev = pending; pending = new List<KeyValuePair<int, MidiEv>>();
        while (pos < n)
        {
            while (ei < ev.Count && ev[ei].Key <= pos) { Midi(ev[ei].Value); ei++; }
            int next = ei < ev.Count ? Math.Min(n, ev[ei].Key) : n; int len = Math.Min(next - pos, CTRL); if (len <= 0) { if (ei < ev.Count) { Midi(ev[ei].Value); ei++; } continue; }
            RenderBlock(pos, len); pos += len;
        }
        while (ei < ev.Count) { Midi(ev[ei].Value); ei++; }
        ApplyGlobalMods();
        // effects on the summed voices
        for (int s = 0; s < 11; s++) RunFx(fxOrder[s], n);
        float pl = 0, pr = 0; for (int i = 0; i < n; i++) { float a = Soft(outL[i]), b = Soft(outR[i]); l[i] += a; r[i] += b; if (Math.Abs(a) > pl) pl = Math.Abs(a); if (Math.Abs(b) > pr) pr = Math.Abs(b); }
        OutPeakL = Math.Max(pl, OutPeakL * 0.85f); OutPeakR = Math.Max(pr, OutPeakR * 0.85f); for (int i = 0; i < 5; i++) SrcPeak[i] = Math.Max(blockPeak[i], SrcPeak[i] * 0.85f);
    }
    // one slot of the chain (the order is set by the fo0..fo10 knobs); effects without their own mix get a wet/dry blend here
    void RunFx(int e, int n)
    {
        if (!fxOn[e]) return; Fx f = null; double wet = 1;
        switch (e) { case 0: f = dist; break; case 1: f = sat; break; case 2: f = comp; break; case 3: f = eq; wet = eqMix; break; case 4: f = phs; break; case 5: f = fln; break; case 6: f = chor; break; case 7: f = dly; break; case 8: f = rev; break; case 9: f = wid; wet = wdMix; break; default: f = boost; break; }
        if (wet >= 0.999) { f.Process(outL, outR, n); return; }
        if (n > dryL.Length) { dryL = new float[n]; dryR = new float[n]; }
        Array.Copy(outL, dryL, n); Array.Copy(outR, dryR, n); f.Process(outL, outR, n);
        float w = (float)wet; for (int i = 0; i < n; i++) { outL[i] = dryL[i] + (outL[i] - dryL[i]) * w; outR[i] = dryR[i] + (outR[i] - dryR[i]) * w; }
    }
    static readonly string[] GKeys = { "drive", "mix", "mix", "mix", "fb", "mix", "size", "gain", "depth", "rate", "drive", "mix", "rate", "depth", "mix", "rate", "depth", "mix", "width", "b1_g" }, GIds = { "dstdrive", "dstmix", "chmix", "dlmix", "dlfb", "rvmix", "rvsize", "bbgain", "chdepth", "chrate", "satdrive", "satmix", "phrate", "phdepth", "phmix", "flrate", "fldepth", "flmix", "wdwidth", "eqmg" };
    readonly bool[] gWas = new bool[20];
    // modulation aimed at the effects: follows the newest sounding note
    void ApplyGlobalMods()
    {
        Fx[] t = { dist, dist, chor, dly, dly, rev, rev, boost, chor, chor, sat, sat, phs, phs, phs, fln, fln, fln, wid, eq };
        for (int q = 0; q < 20; q++)
        {
            double m = gmod[q]; if (Math.Abs(m) < 1e-9 && !gWas[q]) continue; var p = Find(GIds[q]); double val = Math.Max(p.Min, Math.Min(p.Max, p.Val + m * (p.Max - p.Min)));
            t[q].Set(GKeys[q], (float)val); gWas[q] = Math.Abs(m) > 1e-9;
        }
    }
    static float Soft(float x) { float a = Math.Abs(x); if (a < 0.8f) return x; float s = x < 0 ? -1f : 1f; return s * (0.8f + 0.2f * (float)Math.Tanh((a - 0.8f) / 0.2f)); }

    double[] src = new double[14], dst = new double[62];
    double[] oBlend = new double[3], oPan = new double[3], oFm = new double[3], oRm = new double[3], gmod = new double[20]; long gAge; double[] ofreq = new double[3], olvl = new double[3], owt = new double[3], owa = new double[3], onrm = new double[3], ounit = new double[3], opl = new double[3], opr = new double[3], oOut = new double[3];
    bool[] oCompute = new bool[3], oAudible = new bool[3], oUsedAsMod = new bool[3]; WTable[] otbl = new WTable[3];
    double[,] ufreq = new double[3, 7], ugl = new double[3, 7], ugr = new double[3, 7], uw = new double[3, 7]; int[,] umip = new int[3, 7];
    double[] fg = new double[2], fk = new double[2], fdr = new double[2], fa1 = new double[2], fa2 = new double[2], fa3 = new double[2], fcut = new double[2], fb1 = new double[2], fb2 = new double[2], fb3 = new double[2];

    static readonly double[][] VowelF = { new[] { 800.0, 1150, 2900 }, new[] { 400.0, 1600, 2700 }, new[] { 270.0, 2300, 3000 }, new[] { 450.0, 800, 2830 }, new[] { 325.0, 700, 2530 } };

    void RenderBlock(int start, int len)
    {
        double bpm = Fx.Bpm, dtc = len / (double)SR;
        for (int k = 0; k < 4; k++) { gLfo[k] = Frac(gLfo[k] + LfoHz(k, bpm) * dtc); }
        double volLin = DspMath.DbToLin(vol);
        bool anySolo = (op[0].On && op[0].Solo) || (op[1].On && op[1].Solo) || (op[2].On && op[2].Solo) || (subOn && subSolo) || (nzOn && nzSolo);
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
                    if (!lf.Loop) { v.LfoAcc[k] += hz * dtc; ph = Math.Min(0.99999, lf.Phase + v.LfoAcc[k]); }   // one-shot: play the shape once and hold its end
                }
                else { ph = Frac(gLfo[k] + lf.Phase); if (ph < Frac(gLfo[k] - hz * dtc + lf.Phase) && lf.Shape == 5) v.LfoHold[k] = RandBi(); }
                double x = LfoShape(lf.Shape, ph, v.LfoHold[k], LfoCurves[k]);
                if (lf.Uni) x = (x + 1) * 0.5;
                if (lf.Smooth > 0) { double a = 1 - Math.Exp(-dtc / (0.002 + lf.Smooth * 0.15)); v.LfoSm[k] += (x - v.LfoSm[k]) * a; x = v.LfoSm[k]; }
                if (lf.Fade > 0) x *= Math.Min(1.0, v.Time * 1000.0 / lf.Fade);
                v.LfoVal[k] = x; src[1 + k] = x;
            }
            src[5] = v.Env[1]; src[6] = v.Env[2]; src[7] = v.Env[3]; src[8] = v.Vel; src[9] = Math.Max(-1, Math.Min(1, (v.Note - 60) / 36.0));
            for (int m = 0; m < 4; m++) src[10 + m] = macro[m];
            Array.Clear(dst, 0, dst.Length);
            for (int s = 0; s < 16; s++) if (mOn[s] && mSrc[s] != 0 && mDst[s] != 0) { double x = src[mSrc[s]]; if (mPol[s] != 0) { if (mSrc[s] <= 4 || mSrc[s] == 9) x = (x + 1) * 0.5; if (mPol[s] == 2) x = -x; } dst[mDst[s]] += x * mAmt[s]; }
            if (v.Age >= gAge) { gAge = v.Age; for (int q = 0; q < 20; q++) gmod[q] = dst[q < 10 ? 34 + q : 52 + q - 10]; }
            // ---- glide / pitch ----
            if (glide > 0 && mode != 0) v.Cur += (v.Note - v.Cur) * (1 - Math.Exp(-len / (glide * 0.001 * SR))); else v.Cur = v.Note;
            double note = v.Cur + tune / 100.0 + bend * bendRange + dst[5] * 24.0;
            double velAmp = (1 - vsens) + vsens * v.Vel;
            // ---- oscillators: who is audible, who modulates whom ----
            for (int o = 0; o < 3; o++) oUsedAsMod[o] = false;
            for (int c = 0; c < 3; c++) { oFm[c] = Math.Max(0, Math.Min(1, op[c].Fm + dst[44 + c])); oRm[c] = Math.Max(0, Math.Min(1, op[c].Rm + dst[47 + c])); }
            for (int c = 0; c < 3; c++) if (op[c].On && (oFm[c] > 0 || oRm[c] > 0)) oUsedAsMod[ModSource[c]] = true;
            for (int o = 0; o < 3; o++)
            {
                var P = op[o]; oAudible[o] = P.On && !P.Mute && (!anySolo || P.Solo); olvl[o] = oAudible[o] ? Math.Max(0, Math.Min(1, P.Lvl + dst[12 + o])) : 0; oCompute[o] = P.On && (olvl[o] > 0 || oUsedAsMod[o]);
                if (!oCompute[o]) continue;
                double nn = note + P.Oct * 12 + P.Sem + P.Fin / 100.0 + dst[2 + o] * 24.0 + dst[24 + o] * 12.0; ofreq[o] = 440.0 * Math.Pow(2, (nn - 69) / 12.0);
                owt[o] = Math.Max(0, Math.Min(1, P.Wt + dst[6 + o])); owa[o] = Math.Max(0, Math.Min(1, P.WarpAmt + dst[9 + o])); oBlend[o] = Math.Max(0, Math.Min(1, P.Blend + dst[30 + o])); oPan[o] = Math.Max(-1, Math.Min(1, P.Pan + dst[27 + o]));
                otbl[o] = P.Src == 0 ? Wavetables.Table(P.Tbl) : Wavetables.ForSource(P.Src);
            }
            double detMod = Math.Max(0, 1 + dst[22]);
            for (int o = 0; o < 3; o++)
            {
                if (!oCompute[o]) continue; var P = op[o]; int uni = (int)P.Uni; double wsum = 0;
                double mult = 1 + (P.Warp == 1 ? owa[o] * 7 : (P.Warp == 2 || P.Warp == 4 ? owa[o] * 2 : 0)) + oFm[o] * 2;   // warping and FM add harmonics: use a smaller (safer) table
                for (int u = 0; u < uni; u++)
                {
                    double rel = uni == 1 ? 0 : (u - (uni - 1) / 2.0) / ((uni - 1) / 2.0);
                    ufreq[o, u] = ofreq[o] * Math.Pow(2, rel * P.Det * detMod / 1200.0); umip[o, u] = WTable.MipFor(ufreq[o, u] * mult, SR);
                    double w = uni == 1 ? 1.0 : ((uni % 2 == 1 && u == (uni - 1) / 2) ? 1.0 : (uni % 2 == 1 ? oBlend[o] : 1.0)); uw[o, u] = w; wsum += w * w;
                    double pn = rel * 0.8; ugl[o, u] = Math.Sqrt(0.5 * (1 - pn)); ugr[o, u] = Math.Sqrt(0.5 * (1 + pn));
                }
                ounit[o] = 1 / Math.Sqrt(wsum); onrm[o] = olvl[o] * ounit[o]; opl[o] = Math.Sqrt(0.5 * (1 - oPan[o])) * 1.4142; opr[o] = Math.Sqrt(0.5 * (1 + oPan[o])) * 1.4142;
            }
            double subF = 440.0 * Math.Pow(2, (note + subOct * 12 - 69) / 12.0);
            bool subAud = subOn && !subMute && (!anySolo || subSolo), nzAud = nzOn && !nzMute && (!anySolo || nzSolo);
            double subL = subAud ? Math.Max(0, Math.Min(1, subLvl + dst[15])) : 0, nzL = nzAud ? Math.Max(0, Math.Min(1, nzLvl + dst[16])) : 0;
            double sPan = Math.Max(-1, Math.Min(1, subPan + dst[50])), nPan = Math.Max(-1, Math.Min(1, nzPan + dst[51]));
            double subPl = Math.Sqrt(0.5 * (1 - sPan)) * 1.4142, subPr = Math.Sqrt(0.5 * (1 + sPan)) * 1.4142, nzPl = Math.Sqrt(0.5 * (1 - nPan)) * 1.4142, nzPr = Math.Sqrt(0.5 * (1 + nPan)) * 1.4142;
            // ---- filter setup ----
            for (int k = 0; k < 2; k++)
            {
                var F = fp[k]; double cut = F.Cut * Math.Pow(2, dst[k == 0 ? 17 : 20] * 5.0 + (v.Note - 60) / 12.0 * F.Key);
                cut = Math.Max(20, Math.Min(SR * 0.45, cut)); fcut[k] = cut; fg[k] = Math.Tan(Math.PI * cut / SR);
                double res = Math.Max(0, Math.Min(1, F.Res + dst[k == 0 ? 18 : 21])); fk[k] = 2 - 1.96 * res;
                fdr[k] = 1 + Math.Max(0, Math.Min(1, F.Drive + (k == 0 ? dst[19] : dst[33]))) * 7;
                fa1[k] = 1 / (1 + fg[k] * (fg[k] + fk[k])); fa2[k] = fg[k] * fa1[k]; fa3[k] = fg[k] * fa2[k];
                double kb = 1.41421; fb1[k] = 1 / (1 + fg[k] * (fg[k] + kb)); fb2[k] = fg[k] * fb1[k]; fb3[k] = fg[k] * fb2[k];   // later stages: plain Butterworth, so the slope is clean
            }
            double gain = volLin * velAmp * 0.55 * Math.Max(0, 1 + dst[1]);
            double pan = Math.Max(-1, Math.Min(1, dst[23]));
            double pl2 = Math.Sqrt(0.5 * (1 - pan)) * 1.4142, pr2 = Math.Sqrt(0.5 * (1 + pan)) * 1.4142;
            // ---- per-sample ----
            for (int i = 0; i < len; i++)
            {
                double aenv = v.Env[0]; double b1l = 0, b1r = 0, b2l = 0, b2r = 0, dl = 0, dr = 0;
                for (int oi = 2; oi >= 0; oi--)
                {
                    if (!oCompute[oi]) { oOut[oi] = 0; continue; }
                    var P = op[oi]; int uni = (int)P.Uni; double acc0 = 0, acc1 = 0; var tb = otbl[oi];
                    int m = ModSource[oi]; double ms = (oFm[oi] > 0 || oRm[oi] > 0) ? (oi == 2 ? v.LastOut[0] : oOut[m]) : 0; if (!op[m].On) ms = 0;
                    for (int u = 0; u < uni; u++)
                    {
                        double dt = Math.Min(0.45, ufreq[oi, u] / SR); double p = v.Ph[oi, u] + dt; if (p >= 1) p -= 1; v.Ph[oi, u] = p;
                        double pp = p; if (oFm[oi] > 0) { pp = p + oFm[oi] * ms * 0.9; pp -= Math.Floor(pp); }
                        double y = Wavetables.WarpOut(P.Warp, owa[oi], tb.Read(P.Src == 0 ? owt[oi] : 0, Wavetables.WarpPhase(P.Warp, owa[oi], pp), umip[oi, u]));
                        if (oRm[oi] > 0) y = y * (1 - oRm[oi]) + y * ms * oRm[oi];
                        y *= uw[oi, u]; acc0 += y * ugl[oi, u]; acc1 += y * ugr[oi, u];
                    }
                    oOut[oi] = (acc0 + acc1) * 0.5 * ounit[oi];   // this oscillator on its own (before its level), the signal that FM / ring modulation uses
                    if (olvl[oi] > 0)
                    {
                        double sl = acc0 * onrm[oi] * opl[oi], sr = acc1 * onrm[oi] * opr[oi]; double pk = Math.Max(Math.Abs(sl), Math.Abs(sr)); if (pk > blockPeak[oi]) blockPeak[oi] = (float)pk;
                        Route(P.Route, sl, sr, ref b1l, ref b1r, ref b2l, ref b2r, ref dl, ref dr);
                    }
                }
                v.LastOut[0] = oOut[0]; v.LastOut[1] = oOut[1]; v.LastOut[2] = oOut[2];
                if (subL > 0)
                {
                    v.SubPh += subF / SR; if (v.SubPh >= 1) v.SubPh -= 1; double sp = v.SubPh, sy = subWave == 0 ? Math.Sin(6.283185307179586 * sp) : (subWave == 1 ? 4 * Math.Abs(sp - 0.5) - 1 : (subWave == 2 ? (sp < 0.5 ? 0.7 : -0.7) : 2 * sp - 1));
                    double sl = sy * subL * 0.8 * subPl, sr = sy * subL * 0.8 * subPr; if (Math.Max(Math.Abs(sl), Math.Abs(sr)) > blockPeak[3]) blockPeak[3] = (float)Math.Max(Math.Abs(sl), Math.Abs(sr));
                    Route(subRoute, sl, sr, ref b1l, ref b1r, ref b2l, ref b2r, ref dl, ref dr);
                }
                if (nzL > 0)
                {
                    double w0 = RandBi(), nv = w0;
                    switch (nzType)
                    {
                        case 1: v.Pink0 = 0.99765 * v.Pink0 + w0 * 0.0990460; v.Pink1 = 0.96300 * v.Pink1 + w0 * 0.2965164; v.Pink2 = 0.57000 * v.Pink2 + w0 * 1.0526913; nv = (v.Pink0 + v.Pink1 + v.Pink2 + w0 * 0.1848) * 0.25; break;
                        case 2: v.Brown = (v.Brown + 0.02 * w0) / 1.02; nv = v.Brown * 3.5; break;
                        case 3: nv = (w0 - v.PrevW) * 0.7; v.PrevW = w0; break;
                        case 4: nv = RandUni() < 0.0012 ? RandBi() * 1.5 : 0; break;
                    }
                    double sl = nv * nzL * 0.5 * nzPl, sr = nv * nzL * 0.5 * nzPr; if (Math.Max(Math.Abs(sl), Math.Abs(sr)) > blockPeak[4]) blockPeak[4] = (float)Math.Max(Math.Abs(sl), Math.Abs(sr));
                    Route(nzRoute, sl, sr, ref b1l, ref b1r, ref b2l, ref b2r, ref dl, ref dr);
                }
                // filters: serial = filter 1 then filter 2 (sources routed to filter 2 join in front of it); parallel = side by side
                double xl, xr;
                double y1l = b1l, y1r = b1r;
                if (fp[0].On) { y1l = Filt(v, 0, y1l, 0); y1r = Filt(v, 0, y1r, 1); }
                if (parallel)
                {
                    double y2l = b2l, y2r = b2r; if (fp[1].On) { y2l = Filt(v, 1, y2l, 0); y2r = Filt(v, 1, y2r, 1); }
                    xl = y1l + y2l; xr = y1r + y2r;
                }
                else
                {
                    xl = y1l + b2l; xr = y1r + b2r; if (fp[1].On) { xl = Filt(v, 1, xl, 0); xr = Filt(v, 1, xr, 1); }
                }
                xl += dl; xr += dr;
                double g = gain * aenv;
                outL[start + i] += (float)(xl * g * pl2); outR[start + i] += (float)(xr * g * pr2);
                StepAmp(v);
            }
        }
    }

    // where a sound source goes: filter 1, filter 2, both (in series that is just filter 1, which feeds filter 2), or straight to the output
    void Route(int route, double l, double r, ref double b1l, ref double b1r, ref double b2l, ref double b2r, ref double dl, ref double dr)
    {
        switch (route)
        {
            case 0: b1l += l; b1r += r; break;
            case 1: b2l += l; b2r += r; break;
            case 2: if (parallel) { b1l += l * 0.7071; b1r += r * 0.7071; b2l += l * 0.7071; b2r += r * 0.7071; } else { b1l += l; b1r += r; } break;
            default: dl += l; dr += r; break;
        }
    }

    // one filter, one channel. Types: low-pass, high-pass, band-pass, notch, peak, low shelf, high shelf, comb +, comb -, formant
    double Filt(Voice v, int fi, double x, int ch)
    {
        var F = fp[fi]; double drive = fdr[fi]; if (drive > 1.001) x = Math.Tanh(x * drive) / Math.Tanh(drive);
        int b = fi * 32 + ch * 2, type = F.Type;
        switch (type)
        {
            case 0: case 1: case 2: case 3:
                {
                    double y = x; int st = Math.Min(4, F.Stages);
                    for (int s = 0; s < st; s++) y = Svf(v, b + s * 4, type, s == 0 ? fa1[fi] : fb1[fi], s == 0 ? fa2[fi] : fb2[fi], s == 0 ? fa3[fi] : fb3[fi], s == 0 ? fk[fi] : 1.41421, y);
                    return y;
                }
            case 4: { double bp = Svf(v, b, 2, fa1[fi], fa2[fi], fa3[fi], fk[fi], x); return x + (F.Gain - 1) * fk[fi] * bp; }
            case 5: { double lpv = Svf(v, b, 0, fa1[fi], fa2[fi], fa3[fi], 1.41421, x); return x + (F.Gain - 1) * lpv; }
            case 6: { double hpv = Svf(v, b, 1, fa1[fi], fa2[fi], fa3[fi], 1.41421, x); return x + (F.Gain - 1) * hpv; }
            case 7: case 8:
                {
                    int ci = fi * 2 + ch; var buf = v.Comb[ci]; int w = v.CombW[ci]; int d = (int)Math.Max(2, Math.Min(2040, SR / fcut[fi])); double fb = 0.2 + 0.74 * F.Res; if (type == 8) fb = -fb;
                    double y = x + fb * buf[(w - d) & 2047]; if (y > 4) y = 4; else if (y < -4) y = -4; buf[w] = (float)y; v.CombW[ci] = (w + 1) & 2047; return y * (1 - 0.5 * Math.Abs(fb));
                }
            default:
                {
                    double pos = Math.Max(0, Math.Min(4, Math.Log(fcut[fi] / 20.0) / Math.Log(1000.0) * 4)); int v0 = (int)pos; if (v0 > 3) v0 = 3; double ft = pos - v0, y = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        double fr = VowelF[v0][k] * (1 - ft) + VowelF[v0 + 1][k] * ft, gg = Math.Tan(Math.PI * Math.Min(SR * 0.45, fr) / SR), kk = 0.18 + 0.5 * (1 - F.Res), a1 = 1 / (1 + gg * (gg + kk)), a2 = gg * a1, a3 = gg * a2;
                        y += Svf(v, b + k * 4, 2, a1, a2, a3, kk, x) * kk * (k == 0 ? 1.4 : (k == 1 ? 1.0 : 0.7));
                    }
                    return y;
                }
        }
    }
    static double Svf(Voice v, int b, int type, double a1, double a2, double a3, double k, double x)
    {
        double ic1 = v.F[b], ic2 = v.F[b + 1], v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
        v.F[b] = 2 * v1 - ic1; v.F[b + 1] = 2 * v2 - ic2;
        switch (type) { case 0: return v2; case 1: return x - k * v1 - v2; case 2: return v1; default: return x - k * v1; }
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
