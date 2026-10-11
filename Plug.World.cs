using System;
using System.Collections.Generic;

// ======================================================================
//  Worldwide Echo and Melody Tune: two bigger plugins built from the engines already in the library.
// ======================================================================

// ----------------------------------------------------------------------
//  Worldwide Echo: a wide, tempo-synced echo whose "send" opens and closes with the song, so only chosen moments
//  (the end of each bar, the off-beats...) are thrown into the echo. The repeats keep ringing after the send closes.
// ----------------------------------------------------------------------
class WorldEchoFx : SyncFx
{
    static readonly string[] Styles = { "Ping-pong", "Stereo", "Mono", "Multi-tap" };
    static readonly int[] StyleMode = { 2, 1, 0, 3 };
    public static readonly string[] Sends = { "All the time", "Last beat of each bar", "Second half of each bar", "Off-beats (the 'ands')", "Last beat of every 2nd bar", "First beat of each bar" };
    readonly EchoFx echo = new EchoFx(); float[] wl = new float[0], wr = new float[0]; double sendG = 1;

    public WorldEchoFx()
    {
        Type = "worldecho"; Title = "Worldwide Echo"; Category = "Echo & Reverb";
        AddC("style", "Style", Styles, 0); AddC("sync", "Time", Dsp2.SyncNames, 4); AddP("time", "Free time", 1, 2000, 350, "ms");
        AddP("fb", "Repeats", 0, 95, 45, "%"); AddP("lc", "Low cut", 20, 2000, 250, "Hz"); AddP("hc", "High cut", 500, 20000, 6500, "Hz"); AddP("sat", "Saturation", 0, 100, 0, "%");
        AddP("width", "Width", 0, 200, 150, "%"); AddP("diff", "Smear (diffusion)", 0, 100, 0, "%"); AddP("rev", "Reverb on repeats", 0, 100, 25, "%"); AddP("revsize", "Reverb size", 0, 100, 60, "%");
        AddP("duck", "Duck while singing", 0, 100, 40, "%"); AddC("send", "Send (when the echo hears you)", Sends, 0); AddP("sendlvl", "Send level", 0, 100, 100, "%"); AddP("sendsm", "Send smoothing", 2, 500, 40, "ms");
        AddP("dry", "Original sound", 0, 100, 100, "%"); AddP("mix", "Echo level", 0, 100, 40, "%");
    }
    protected override void Update()
    {
        echo.Prepare(SR); echo.Set("mode", StyleMode[(int)Get("style")]); echo.Set("sync", Get("sync")); echo.Set("time", Get("time")); echo.Set("offset", 20);
        echo.Set("fb", Get("fb")); echo.Set("lc", Get("lc")); echo.Set("hc", Get("hc")); echo.Set("sat", Get("sat")); echo.Set("width", Get("width")); echo.Set("diff", Get("diff"));
        echo.Set("rev", Get("rev")); echo.Set("revsize", Get("revsize")); echo.Set("duck", Get("duck")); echo.Set("dry", 0); echo.Set("mix", 100); echo.Set("decay", 70);
    }
    public override void Reset() { base.Reset(); echo.Reset(); sendG = 0; freeCnt = 0; }
    static bool Open(int pat, double beat)
    {
        double pb = beat % 4.0; if (pb < 0) pb += 4.0; long bar = (long)Math.Floor(beat / 4.0);
        switch (pat)
        {
            case 1: return pb >= 3.0;
            case 2: return pb >= 2.0;
            case 3: return (beat - Math.Floor(beat)) >= 0.5;
            case 4: return (bar & 1) == 1 && pb >= 3.0;
            case 5: return pb < 1.0;
            default: return true;
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (wl.Length < n) { wl = new float[n]; wr = new float[n]; }
        int pat = (int)Get("send"); double lvl = Get("sendlvl") / 100.0, a = 1 - Math.Exp(-1.0 / (Math.Max(1.0, Get("sendsm")) * 0.001 * SR));
        for (int i = 0; i < n; i++)
        {
            double tgt = pat == 0 ? lvl : (Open(pat, BeatAt(i)) ? lvl : 0.0); sendG += (tgt - sendG) * a;
            wl[i] = (float)(l[i] * sendG); wr[i] = (float)(r[i] * sendG);
        }
        echo.ScL = ScL; echo.ScR = ScR; echo.Process(wl, wr, n);
        float dry = Get("dry") / 100f, wet = Get("mix") / 100f;
        for (int i = 0; i < n; i++) { l[i] = Dsp2.Sanitize(l[i] * dry + wl[i] * wet); r[i] = Dsp2.Sanitize(r[i] * dry + wr[i] * wet); }
        EndBlock(n);
    }
}

// ----------------------------------------------------------------------
//  Melody Tune: pitch correction to a key and scale (retune speed, strength, humanise) plus melodic extras:
//  harmony voices that follow the tuned note (a third, a fifth, octaves, a choir) and a detuned thickener.
//  The harmony intervals follow the scale's character (major or minor), not each single note.
// ----------------------------------------------------------------------
class MelodyTuneFx : Fx
{
    public static readonly string[] Harm = { "Off", "Third above", "Third below", "Fifth above", "Fifth below", "Octave above", "Octave below", "Power (fifth + octave)", "Choir (third, fifth, octave)", "Thick double (detuned)" };
    readonly PitchCorrector pc = new PitchCorrector(); readonly PitchShiftFx hv = new PitchShiftFx();
    float[] dl, dr, wl = new float[0], wr = new float[0]; int dw, dlen;

    public MelodyTuneFx()
    {
        Type = "melody"; Title = "Melody Tune"; Category = "Pitch & Voice";
        AddC("key", "Key", PitchCorrector.Keys, 0); AddC("scale", "Scale", PitchCorrector.Scales, 1);
        AddP("speed", "Tune speed (0 = instant, hard tune)", 0, 400, 30, "ms"); AddP("strength", "Strength", 0, 100, 100, "%"); AddP("natural", "Humanise (keep vibrato and slides)", 0, 100, 45, "%");
        AddC("range", "Voice range", PitchCorrector.Ranges, 1); AddC("harm", "Melodic layer", Harm, 0); AddP("hlevel", "Layer level", 0, 100, 50, "%"); AddP("spread", "Layer width", 0, 100, 70, "%");
        AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override int Latency { get { return pc.Latency + hv.Latency; } }
    bool Major() { int s = (int)Get("scale"); return s == 0 || s == 1 || s == 4; }
    // semitones, cents, level, pan, delay (ms) for the up to three layer voices
    double[][] Voices()
    {
        int third = Major() ? 4 : 3, thirdDown = Major() ? -3 : -4; double L = Get("hlevel") / 100.0, W = Get("spread") / 100.0;
        switch ((int)Get("harm"))
        {
            case 1: return new[] { new double[] { third, 0, 0.9 * L, -0.5 * W, 9 } };
            case 2: return new[] { new double[] { thirdDown, 0, 0.9 * L, 0.5 * W, 9 } };
            case 3: return new[] { new double[] { 7, 0, 0.9 * L, 0.5 * W, 11 } };
            case 4: return new[] { new double[] { -5, 0, 0.9 * L, -0.5 * W, 11 } };
            case 5: return new[] { new double[] { 12, 0, 0.8 * L, 0.3 * W, 6 } };
            case 6: return new[] { new double[] { -12, 0, 0.9 * L, -0.3 * W, 6 } };
            case 7: return new[] { new double[] { 7, 0, 0.8 * L, -0.6 * W, 10 }, new double[] { 12, 0, 0.7 * L, 0.6 * W, 14 } };
            case 8: return new[] { new double[] { third, 0, 0.8 * L, -0.8 * W, 8 }, new double[] { 7, 0, 0.75 * L, 0.8 * W, 13 }, new double[] { 12, 0, 0.6 * L, 0, 18 } };
            case 9: return new[] { new double[] { 0, 13, 0.8 * L, -0.9 * W, 14 }, new double[] { 0, -13, 0.8 * L, 0.9 * W, 22 } };
        }
        return new double[0][];
    }
    protected override void Update()
    {
        pc.Prepare(SR); pc.Set("key", Get("key")); pc.Set("scale", Get("scale")); pc.ApplyScale(); pc.Set("speed", Get("speed")); pc.Set("strength", Get("strength")); pc.Set("natural", Get("natural")); pc.Set("range", Get("range")); pc.Set("mix", 100);
        hv.Prepare(SR); hv.Set("quality", 1); hv.Set("mix", 100); hv.Set("keep", 100); hv.Set("p1", 0); hv.Set("c1", 0); hv.Set("l1", 100); hv.Set("pan1", 0); hv.Set("d1", 0);
        var v = Voices();
        for (int k = 2; k <= 4; k++)
        {
            if (k - 2 < v.Length) { var x = v[k - 2]; hv.Set("p" + k, (float)x[0]); hv.Set("c" + k, (float)x[1]); hv.Set("l" + k, (float)(x[2] * 100)); hv.Set("pan" + k, (float)(x[3] * 100)); hv.Set("d" + k, (float)x[4]); }
            else hv.Set("l" + k, 0);
        }
        int L = Latency; dlen = Math.Max(16, L + 8); if (dl == null || dl.Length != dlen) { dl = new float[dlen]; dr = new float[dlen]; dw = 0; }
    }
    public override void Reset() { base.Reset(); pc.Reset(); hv.Reset(); if (dl != null) { Array.Clear(dl, 0, dl.Length); Array.Clear(dr, 0, dr.Length); } }
    public override void BeginBlock(object track, long pos, int n, bool transport) { pc.BeginBlock(track, pos, n, transport); hv.BeginBlock(track, pos, n, transport); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); float mix = Get("mix") / 100f; int L = Latency; if (dl == null || dlen <= L) { dlen = L + 8; dl = new float[dlen]; dr = new float[dlen]; dw = 0; }
        if (mix < 0.999f) { if (wl.Length < n) { wl = new float[n]; wr = new float[n]; } Array.Copy(l, wl, n); Array.Copy(r, wr, n); }
        pc.Process(l, r, n); hv.Process(l, r, n);
        if (mix < 0.999f)
            for (int i = 0; i < n; i++)
            {
                dl[dw] = wl[i]; dr[dw] = wr[i]; int rd = dw - L; if (rd < 0) rd += dlen; float a = dl[rd], b = dr[rd]; if (++dw >= dlen) dw = 0;
                l[i] = a + (l[i] - a) * mix; r[i] = b + (r[i] - b) * mix;
            }
    }
}

// the named versions (presets) of the two plugins above
static partial class PluginTables
{
    static void FillWorld()
    {
        const string WE = "Echo & Reverb";
        A(WE, "Worldwide Echo", "worldecho", "style=0;sync=4;fb=45;lc=250;hc=7000;width=150;rev=22;revsize=60;duck=45;send=0;mix=40", "A wide ping-pong echo that locks to your tempo, with reverb on the repeats, ducking while you sing, and a Send pattern that decides which moments are thrown into the echo.", "");
        A(WE, "Stadium Echo", "worldecho", "style=1;sync=3;fb=55;lc=200;hc=5500;width=200;rev=55;revsize=85;duck=25;send=0;mix=42", "Huge slow repeats in a big wash of reverb, like singing across an empty arena.", "");
        A(WE, "Rap Ad-Lib Echo", "worldecho", "style=0;sync=8;fb=38;lc=400;hc=6500;width=170;rev=15;duck=60;send=2;sendsm=25;mix=45", "Bouncy dotted-eighth repeats that only open in the second half of each bar, so the ad-libs echo and the verse stays clean.", "");
        A(WE, "Dark Echo", "worldecho", "style=1;sync=4;fb=50;lc=120;hc=2400;sat=35;width=120;rev=20;duck=30;send=0;mix=38", "Dim, warm repeats that get darker each time round: moody and heavy.", "");
        A(WE, "Airy Echo", "worldecho", "style=0;sync=7;fb=40;lc=500;hc=14000;width=180;diff=30;rev=40;revsize=70;duck=35;send=0;mix=35", "Light, bright, floating repeats with a soft smear: sits on top of a vocal like a glow.", "");
        A(WE, "Telephone Throw", "worldecho", "style=2;sync=4;fb=55;lc=500;hc=2800;sat=40;width=100;rev=0;duck=70;send=1;sendsm=15;mix=50", "Only the last beat of each bar is thrown into a thin, phone-line echo: the classic end-of-line throw.", "");
        A(WE, "Infinite Space", "worldecho", "style=1;sync=2;fb=90;lc=200;hc=5000;sat=20;width=190;diff=60;rev=80;revsize=95;duck=20;send=0;mix=45", "Near-endless repeats that melt into reverb: an ambient cloud around the sound.", "");
    }
    static void FillMelody()
    {
        const string MT = "Pitch & Voice";
        A(MT, "Melody Tune", "melody", "speed=30;strength=100;natural=45;harm=0", "Pitch correction to a key and scale with a natural feel. Choose a Melodic layer to add harmonies that follow the tuned note.", "");
        A(MT, "Melody Tune: Natural Polish", "melody", "speed=90;strength=65;natural=75;harm=0", "Gentle: only pulls the notes that are off, and keeps the vibrato and slides.", "");
        A(MT, "Melody Tune: Hard Tune", "melody", "speed=0;strength=100;natural=0;harm=0", "Instant, robotic snapping from note to note: the hard-tuned pop and trap sound.", "");
        A(MT, "Melody Tune: Trap Melody", "melody", "scale=5;speed=8;strength=100;natural=15;harm=5;hlevel=30;spread=80", "Minor pentatonic with quick tuning and a quiet octave on top: the sound of a modern melodic rap hook.", "");
        A(MT, "Melody Tune: Soft Harmony", "melody", "speed=45;strength=90;natural=55;harm=1;hlevel=45;spread=70", "Tunes the lead and adds a gentle third above it.", "");
        A(MT, "Melody Tune: Choir Stack", "melody", "speed=40;strength=95;natural=40;harm=8;hlevel=55;spread=90", "Three extra voices (third, fifth and octave) spread wide, like a small choir behind the lead.", "");
        A(MT, "Melody Tune: Octave Dream", "melody", "speed=35;strength=95;natural=40;harm=5;hlevel=55;spread=60", "Tunes the voice and adds a floating octave above it.", "");
        A(MT, "Melody Tune: Power Lead", "melody", "speed=15;strength=100;natural=20;harm=7;hlevel=60;spread=80", "A tight, tuned lead with a fifth and an octave stacked on it: big and bold.", "");
    }
}
