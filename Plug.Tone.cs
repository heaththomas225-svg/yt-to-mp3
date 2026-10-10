using System;
using System.Collections.Generic;

// ======================================================================
//  Tone engines: band-limiting (telephone, radio), waveshaping, bit crusher, ring modulator,
//  frequency shifter, comb / resonator, channel vocoder.
// ======================================================================

class BandLimitFx : Fx
{
    Biquad[] hp = new Biquad[4], lp = new Biquad[4]; Biquad nhp = new Biquad(), nlp = new Biquad(); Biquad bump = new Biquad(); Random rnd = new Random(11);
    double clickEnv, flutPh, flutPh2; int stages;
    public BandLimitFx()
    {
        Type = "band"; Title = "Band Limiter"; Category = "Telephone & Distortion";
        for (int i = 0; i < 4; i++) { hp[i] = new Biquad(); lp[i] = new Biquad(); }
        AddP("hp", "Low cut", 20, 2500, 300, "Hz"); AddP("lp", "High cut", 800, 20000, 3400, "Hz"); AddC("slope", "Slope", new[] { "12 dB", "24 dB", "48 dB" }, 1);
        AddP("bump", "Mid boost", 0, 18, 0, "dB"); AddP("bumpf", "Boost freq", 300, 6000, 1800, "Hz");
        AddP("drive", "Drive", 0, 36, 0, "dB"); AddP("noise", "Hiss", 0, 100, 0, "%"); AddP("crackle", "Crackle", 0, 100, 0, "%"); AddP("flutter", "Signal wobble", 0, 100, 0, "%");
        AddC("mono", "Channels", new[] { "Stereo", "Mono" }, 0); AddP("out", "Output", -18, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update()
    {
        stages = 1 + (int)Get("slope") + ((int)Get("slope") == 2 ? 1 : 0); if (stages > 4) stages = 4;
        for (int i = 0; i < 4; i++) { hp[i].Set(Biquad.HighPass, SR, Get("hp"), 0.7071, 0); lp[i].Set(Biquad.LowPass, SR, Get("lp"), 0.7071, 0); }
        bump.Set(Biquad.Bell, SR, Get("bumpf"), 1.2, Get("bump")); nhp.Set(Biquad.HighPass, SR, Get("hp"), 0.7071, 0); nlp.Set(Biquad.LowPass, SR, Get("lp"), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); for (int i = 0; i < 4; i++) { hp[i].Reset(); lp[i].Reset(); } bump.Reset(); nhp.Reset(); nlp.Reset(); clickEnv = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double drive = Math.Pow(10, Get("drive") / 20.0), comp = 1.0 / Math.Max(1.0, Math.Sqrt(drive)), outG = DspMath.DbToLin(Get("out")), mix = Get("mix") / 100.0;
        double noise = Math.Pow(Get("noise") / 100.0, 2) * 0.06, crack = Get("crackle") / 100.0, flut = Get("flutter") / 100.0; bool mono = Get("mono") > 0.5f; bool useBump = Get("bump") > 0.05f;
        for (int i = 0; i < n; i++)
        {
            float a = l[i], b = r[i]; float da = a, db = b;
            if (mono) { a = (a + b) * 0.5f; b = a; }
            for (int s = 0; s < stages; s++) { a = hp[s].TickL(a); b = mono ? a : hp[s].TickR(b); }
            for (int s = 0; s < stages; s++) { a = lp[s].TickL(a); b = mono ? a : lp[s].TickR(b); }
            if (useBump) { a = bump.TickL(a); b = mono ? a : bump.TickR(b); }
            if (drive > 1.01) { a = (float)(Math.Tanh(a * drive) * comp); b = mono ? a : (float)(Math.Tanh(b * drive) * comp); }
            if (flut > 0) { flutPh += 6.3 / SR; flutPh2 += 9.1 / SR; double g = 1 - flut * 0.5 * (0.5 + 0.25 * Math.Sin(flutPh * 2 * Math.PI) + 0.25 * Math.Sin(flutPh2 * 2 * Math.PI) + 0.25); a *= (float)g; b *= (float)g; if (mono) b = a; }
            if (noise > 0 || crack > 0)
            {
                float nz = 0;
                if (noise > 0) nz = (float)((rnd.NextDouble() * 2 - 1) * noise);
                if (crack > 0) { if (rnd.NextDouble() < crack * crack * 0.0006) clickEnv = (rnd.NextDouble() * 0.5 + 0.2) * (rnd.NextDouble() < 0.5 ? -1 : 1); nz += (float)clickEnv * 0.5f; clickEnv *= 0.85; }
                nz = nlp.TickL(nhp.TickL(nz)) * 1.6f; a += nz; b += nz;
            }
            a = (float)(a * outG); b = (float)(b * outG);
            l[i] = Dsp2.Sanitize((float)(da + (a - da) * mix)); r[i] = Dsp2.Sanitize((float)(db + (b - db) * mix));
        }
    }
}

// ----------------------------------------------------------------------
class ShaperFx : Fx
{
    public static readonly string[] Modes = { "Soft clip (tanh)", "Tube (warm)", "Tape (smooth top)", "Overdrive", "Hard clip", "Soft clipper (cubic)", "Fuzz", "Wavefolder", "Sine shaper", "Chebyshev harmonics", "Rectifier (octave up)", "Diode" };
    Biquad pre = new Biquad(), tone = new Biquad(), os1 = new Biquad(), os2 = new Biquad(), lowSplit = new Biquad(), dcB = new Biquad(); double prevL, prevR, prevInL, prevInR; double dcL, dcR; int F = 1;
    public ShaperFx()
    {
        Type = "shaper"; Title = "Shaper"; Category = "Telephone & Distortion";
        AddC("mode", "Shape", Modes, 0); AddP("drive", "Drive", 0, 48, 12, "dB"); AddP("bias", "Asymmetry", -100, 100, 0, "%"); AddP("pre", "Tightness (low cut before)", 20, 800, 20, "Hz");
        AddP("tone", "Tone (high cut after)", 500, 20000, 12000, "Hz"); AddP("split", "Only affect below", 0, 800, 0, "Hz"); AddC("os", "Oversampling", new[] { "Off", "2x", "4x" }, 1);
        AddC("auto", "Auto level", new[] { "Off", "On" }, 1); AddP("out", "Output", -18, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update()
    {
        F = new[] { 1, 2, 4 }[(int)Get("os")]; int fsr = SR * F;
        pre.Set(Biquad.HighPass, SR, Math.Max(20, Get("pre")), 0.7071, 0); tone.Set(Biquad.LowPass, SR, Math.Min(Get("tone"), SR * 0.45), 0.7071, 0);
        os1.Set(Biquad.LowPass, fsr, SR * 0.44, 0.7071, 0); os2.Set(Biquad.LowPass, fsr, SR * 0.44, 0.54, 0); lowSplit.Set(Biquad.LowPass, SR, Math.Max(30, Get("split")), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); pre.Reset(); tone.Reset(); os1.Reset(); os2.Reset(); lowSplit.Reset(); dcL = dcR = prevL = prevR = prevInL = prevInR = 0; }
    internal static double Sh(double x, int m)
    {
        switch (m)
        {
            case 0: return Math.Tanh(x);
            case 1: return Math.Tanh(x + 0.25) - Math.Tanh(0.25);
            case 2: { double ax = Math.Abs(x); return x / Math.Pow(1 + Math.Pow(ax, 1.5), 1.0 / 1.5); }
            case 3: return x >= 0 ? 1 - Math.Exp(-x) : -(1 - Math.Exp(x)) * 0.92;
            case 4: return x > 1 ? 1 : (x < -1 ? -1 : x);
            case 5: { double c = x > 1 ? 1 : (x < -1 ? -1 : x); return 1.5 * (c - c * c * c / 3); }
            case 6: return Math.Tanh(x * 3 + 0.4) * 0.8 - Math.Tanh(0.4) * 0.8;
            case 7: return (2 / Math.PI) * Math.Asin(Math.Sin(Math.PI / 2 * x));
            case 8: return Math.Sin(Math.Tanh(x) * Math.PI * 0.5 * 1.8) * 0.9;
            case 9: { double t = Math.Tanh(x); return 0.55 * t + 0.3 * (4 * t * t * t - 3 * t) + 0.2 * (2 * t * t - 1); }
            case 10: { double t = Math.Tanh(x); return 0.5 * t + 0.5 * (2 * Math.Abs(t) - 0.6); }
            default: return x >= 0 ? 1 - Math.Exp(-x) : -0.35 * (1 - Math.Exp(3 * x));
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double drive = Math.Pow(10, Get("drive") / 20.0), bias = Get("bias") / 100.0 * 0.6; double outG = DspMath.DbToLin(Get("out")), mix = Get("mix") / 100.0;
        double comp = Get("auto") > 0.5f ? 1.0 / Math.Pow(Math.Max(1.0, drive), 0.6) : 1.0; double split = Get("split"); bool lowOnly = split > 0; bool usePre = Get("pre") > 21;
        for (int i = 0; i < n; i++)
        {
            for (int c = 0; c < 2; c++)
            {
                double x = c == 0 ? l[i] : r[i]; double dry = x, hi = 0;
                if (lowOnly) { double lo = c == 0 ? lowSplit.TickL((float)x) : lowSplit.TickR((float)x); hi = x - lo; x = lo; }
                if (usePre) x = c == 0 ? pre.TickL((float)x) : pre.TickR((float)x);
                double prevIn = c == 0 ? prevInL : prevInR, y = 0;
                for (int k = 1; k <= F; k++)
                {
                    double xi = F == 1 ? x : prevIn + (x - prevIn) * k / F; double s = Sh(xi * drive + bias, mode) * comp;
                    if (F > 1) { s = c == 0 ? os1.TickL((float)s) : os1.TickR((float)s); s = c == 0 ? os2.TickL((float)s) : os2.TickR((float)s); }
                    y = s;
                }
                if (c == 0) prevInL = x; else prevInR = x;
                // remove DC made by asymmetric shapes
                if (c == 0) { double o = y - dcL; dcL += o * 0.0008 + 0; y = o; } else { double o = y - dcR; dcR += o * 0.0008; y = o; }
                y = c == 0 ? tone.TickL((float)y) : tone.TickR((float)y); y = (y + hi) * outG;
                float wet = (float)(dry + (y - dry) * mix); if (c == 0) l[i] = Dsp2.Sanitize(wet); else r[i] = Dsp2.Sanitize(wet);
            }
        }
    }
}

// ----------------------------------------------------------------------
class BitCrushFx : Fx
{
    double holdL, holdR, ph; float[] pl = new float[1]; Random rnd = new Random(3);
    public BitCrushFx()
    {
        Type = "crush"; Title = "Bit Crusher"; Category = "Telephone & Distortion";
        AddP("bits", "Bit depth", 1, 24, 8, "bits"); AddP("rate", "Sample rate", 300, 48000, 12000, "Hz"); AddP("jitter", "Rate wobble", 0, 100, 0, "%"); AddP("dither", "Noise (dither)", 0, 100, 0, "%");
        AddP("tone", "Smooth (high cut)", 1000, 20000, 20000, "Hz"); AddP("out", "Output", -18, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    Biquad lp = new Biquad(); protected override void Update() { lp.Set(Biquad.LowPass, SR, Math.Min(Get("tone"), SR * 0.45), 0.7071, 0); }
    public override void Reset() { base.Reset(); holdL = holdR = 0; ph = 1; lp.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool useLp = Get("tone") < 19000; double bits = Get("bits"); double steps = Math.Pow(2, bits - 1); double rate = Math.Min(Get("rate"), SR), jit = Get("jitter") / 100.0, dith = Get("dither") / 100.0 / steps, outG = DspMath.DbToLin(Get("out")), mix = Get("mix") / 100.0;
        for (int i = 0; i < n; i++)
        {
            ph += rate / SR * (1 + (jit > 0 ? (rnd.NextDouble() * 2 - 1) * jit * 0.9 : 0));
            if (ph >= 1) { ph -= Math.Floor(ph); double dl = l[i] + (dith > 0 ? (rnd.NextDouble() - rnd.NextDouble()) * dith : 0), dr = r[i] + (dith > 0 ? (rnd.NextDouble() - rnd.NextDouble()) * dith : 0); holdL = Math.Round(dl * steps) / steps; holdR = Math.Round(dr * steps) / steps; }
            float a = useLp ? lp.TickL((float)holdL) : (float)holdL, b = useLp ? lp.TickR((float)holdR) : (float)holdR;
            l[i] = Dsp2.Sanitize((float)(l[i] + (a * outG - l[i]) * mix)); r[i] = Dsp2.Sanitize((float)(r[i] + (b * outG - r[i]) * mix));
        }
    }
}

// ----------------------------------------------------------------------
class RingModFx : Fx
{
    public static readonly string[] Waves = { "Sine", "Square", "Saw", "Triangle" };
    double ph, wob;
    public RingModFx()
    {
        Type = "ring"; Title = "Ring Modulator"; Category = "Telephone & Distortion";
        AddC("mode", "Mode", new[] { "Ring (classic robot)", "Amplitude (tremolo)" }, 0); AddC("wave", "Carrier", Waves, 0); AddP("freq", "Frequency", 0.5f, 5000, 440, "Hz");
        AddP("wob", "Wobble depth", 0, 100, 0, "%"); AddP("wobr", "Wobble rate", 0.1f, 20, 4, "Hz"); AddP("depth", "Depth", 0, 100, 100, "%"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); ph = 0; wob = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"), wave = (int)Get("wave"); double f = Get("freq"), wd = Get("wob") / 100.0, wr = Get("wobr"), depth = Get("depth") / 100.0, mix = Get("mix") / 100.0;
        for (int i = 0; i < n; i++)
        {
            wob += wr / SR; double fm = f * (1 + wd * 0.5 * Math.Sin(wob * 2 * Math.PI)); ph += fm / SR; ph -= Math.Floor(ph); double c;
            switch (wave) { case 0: c = Math.Sin(ph * 2 * Math.PI); break; case 1: c = ph < 0.5 ? 1 : -1; break; case 2: c = ph * 2 - 1; break; default: c = ph < 0.5 ? ph * 4 - 1 : 3 - ph * 4; break; }
            double g = mode == 0 ? (1 - depth) + depth * c : 1 - depth * 0.5 + depth * 0.5 * c;
            float a = (float)(l[i] * g), b = (float)(r[i] * g); l[i] = (float)(l[i] + (a - l[i]) * mix); r[i] = (float)(r[i] + (b - r[i]) * mix);
        }
    }
}

// ----------------------------------------------------------------------
//  Single-sideband frequency shifter (Hilbert transform from two all-pass chains)
// ----------------------------------------------------------------------
class FreqShiftFx : Fx
{
    static readonly double[] A1 = { 0.6923878, 0.9360654322959, 0.9882295226860, 0.9987488452737 }, A2 = { 0.4021921162426, 0.8561710882420, 0.9722909545651, 0.9952884791278 };
    class Chain { public double[] x1 = new double[4], x2 = new double[4], y1 = new double[4], y2 = new double[4]; }
    Chain[] c1 = { new Chain(), new Chain() }, c2 = { new Chain(), new Chain() }; double[] d1 = new double[2], fbv = new double[2]; double ph, wobPh;
    public FreqShiftFx()
    {
        Type = "fshift"; Title = "Frequency Shifter"; Category = "Creative";
        AddP("shift", "Shift", -2000, 2000, 50, "Hz"); AddP("fb", "Feedback (spiral)", 0, 90, 0, "%"); AddP("wob", "Wobble depth", 0, 500, 0, "Hz"); AddP("wobr", "Wobble rate", 0.05f, 15, 0.5f, "Hz"); AddC("dir", "Output", new[] { "Up", "Down", "Both (stereo)" }, 0); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); for (int c = 0; c < 2; c++) { c1[c] = new Chain(); c2[c] = new Chain(); d1[c] = 0; fbv[c] = 0; } ph = 0; }
    static double Run(Chain ch, double[] a, double x)
    {
        for (int k = 0; k < 4; k++)
        {
            double a2 = a[k] * a[k]; double y = a2 * (x + ch.y2[k]) - ch.x2[k];   // (a^2 - z^-2) / (1 - a^2 z^-2)
            ch.x2[k] = ch.x1[k]; ch.x1[k] = x; ch.y2[k] = ch.y1[k]; ch.y1[k] = y; x = y;
        }
        return x;
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double sh = Get("shift"), fb = Get("fb") / 100.0, wd = Get("wob"), wr = Get("wobr"), mix = Get("mix") / 100.0; int dir = (int)Get("dir");
        for (int i = 0; i < n; i++)
        {
            wobPh += wr / SR; double f = sh + wd * Math.Sin(wobPh * 2 * Math.PI); ph += f / SR; ph -= Math.Floor(ph); double cs = Math.Cos(ph * 2 * Math.PI), sn = Math.Sin(ph * 2 * Math.PI);
            double up0 = 0, up1 = 0, dn0 = 0, dn1 = 0;
            for (int c = 0; c < 2; c++)
            {
                double x = (c == 0 ? l[i] : r[i]) + fbv[c] * fb; double y1 = Run(c1[c], A1, x), y2 = Run(c2[c], A2, x); double yi = d1[c]; d1[c] = y1;   // path 1 delayed by one sample
                double up = yi * cs + y2 * sn, dn = yi * cs - y2 * sn; fbv[c] = (dir == 1 ? dn : up);
                if (c == 0) { up0 = up; dn0 = dn; } else { up1 = up; dn1 = dn; }
            }
            double oL, oR; if (dir == 0) { oL = up0; oR = up1; } else if (dir == 1) { oL = dn0; oR = dn1; } else { oL = up0; oR = dn1; }
            l[i] = Dsp2.Sanitize((float)(l[i] + (oL - l[i]) * mix)); r[i] = Dsp2.Sanitize((float)(r[i] + (oR - r[i]) * mix));
        }
    }
}

// ----------------------------------------------------------------------
//  Comb filters and tuned resonators (up to 4 notes)
// ----------------------------------------------------------------------
class CombFx : Fx
{
    public static readonly string[] Chords = { "Single note", "Octave", "Fifth", "Octave + fifth", "Major chord", "Minor chord", "Sus4", "Major 7th" };
    static readonly int[][] Sems = { new[] { 0 }, new[] { 0, 12 }, new[] { 0, 7 }, new[] { 0, 7, 12 }, new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 0, 5, 7 }, new[] { 0, 4, 7, 11 } };
    const int MAXV = 4; float[][][] buf = new float[2][][]; int[] wp = new int[2]; double[,] lpS = new double[2, MAXV]; int len; double env;
    public CombFx()
    {
        Type = "comb"; Title = "Comb Filter"; Category = "Creative";
        AddC("mode", "Type", new[] { "Resonator (feedback)", "Comb notch (feed-forward)" }, 0); AddP("freq", "Frequency", 20, 5000, 220, "Hz"); AddC("snap", "Snap to notes", new[] { "Off", "On" }, 0); AddP("fine", "Fine tune", -100, 100, 0, "cents");
        AddC("chord", "Notes", Chords, 0); AddP("fb", "Feedback", -98, 98, 75, "%"); AddP("damp", "Damping (high cut)", 500, 20000, 9000, "Hz"); AddP("spread", "Stereo detune", 0, 100, 0, "cents");
        AddP("dyn", "Follows the sound (dynamic)", 0, 100, 0, "%"); AddP("mix", "Mix", 0, 100, 60, "%");
    }
    protected override void Update() { int n = SR / 18 + 8; if (buf[0] == null || len != n) { len = n; for (int c = 0; c < 2; c++) { buf[c] = new float[MAXV][]; for (int v = 0; v < MAXV; v++) buf[c][v] = new float[n]; } } }
    public override void Reset() { base.Reset(); if (buf[0] != null) for (int c = 0; c < 2; c++) for (int v = 0; v < MAXV; v++) Array.Clear(buf[c][v], 0, len); lpS = new double[2, MAXV]; env = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double f0 = Get("freq"); if (Get("snap") > 0.5f) { double m = 69 + 12 * Math.Log(f0 / 440.0, 2); f0 = 440 * Math.Pow(2, (Math.Round(m) - 69) / 12.0); }
        f0 *= Math.Pow(2, Get("fine") / 1200.0); var sems = Sems[(int)Get("chord")]; int nv = sems.Length; double fb = Get("fb") / 100.0, spread = Get("spread"), dyn = Get("dyn") / 100.0, mix = Get("mix") / 100.0;
        double dampA = 1 - Math.Exp(-2 * Math.PI * Get("damp") / SR); double[,] D = new double[2, MAXV];
        for (int c = 0; c < 2; c++) for (int v = 0; v < nv; v++) { double f = f0 * Math.Pow(2, sems[v] / 12.0) * Math.Pow(2, (c == 0 ? -spread : spread) / 1200.0); D[c, v] = Math.Max(2, Math.Min(len - 3, SR / f)); }
        double norm = 1.0 / (1.0 + Math.Abs(fb) * (mode == 0 ? 1.0 : 0.6)) * (nv > 1 ? 1.0 / Math.Sqrt(nv) * 1.2 : 1.0);
        for (int i = 0; i < n; i++)
        {
            double ax = Math.Abs(l[i] + r[i]) * 0.5; env += (ax - env) * (ax > env ? 0.05 : 0.0008); double fbe = fb * (dyn > 0 ? (1 - dyn + dyn * Math.Min(1.0, env * 6)) : 1.0);
            for (int c = 0; c < 2; c++)
            {
                double x = c == 0 ? l[i] : r[i], acc = 0;
                for (int v = 0; v < nv; v++)
                {
                    var b = buf[c][v]; double rp = wp[c] - D[c, v]; if (rp < 0) rp += len; int i0 = (int)rp; double fr = rp - i0; int i1 = i0 + 1 >= len ? 0 : i0 + 1; double d = b[i0] + (b[i1] - b[i0]) * fr;
                    if (mode == 0) { lpS[c, v] += dampA * (d - lpS[c, v]); double y = x + fbe * lpS[c, v]; if (y > 4) y = 4; if (y < -4) y = -4; b[wp[c]] = (float)y; acc += y; }
                    else { b[wp[c]] = (float)x; acc += x + fbe * d; }
                }
                float o = (float)(acc * norm); float dry = c == 0 ? l[i] : r[i]; float res = Dsp2.Sanitize(dry + (o - dry) * (float)mix); if (c == 0) l[i] = res; else r[i] = res;
            }
            wp[0] = wp[0] + 1 >= len ? 0 : wp[0] + 1; wp[1] = wp[1] + 1 >= len ? 0 : wp[1] + 1;
        }
    }
}

// ----------------------------------------------------------------------
//  Channel vocoder with its own carrier (saw / pulse / noise, optional chord)
// ----------------------------------------------------------------------
class VocoderFx : Fx
{
    public static readonly string[] Carriers = { "Saw", "Pulse", "Noise (whisper)", "Saw + noise" }, BandsTxt = { "8", "12", "16", "24", "32" };
    static readonly int[] BandN = { 8, 12, 16, 24, 32 };
    static readonly int[][] Sems = { new[] { 0 }, new[] { 0, 12 }, new[] { 0, 7, 12 }, new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 0, 4, 7, 11 } };
    public static readonly string[] Chords = { "Single note", "Octave", "Octave + fifth", "Major chord", "Minor chord", "Major 7th" };
    const int MAXB = 32; Biquad[] mod1 = new Biquad[MAXB], mod2 = new Biquad[MAXB], car1 = new Biquad[MAXB], car2 = new Biquad[MAXB]; double[] env = new double[MAXB]; double[] ph = new double[4]; Random rnd = new Random(5);
    Biquad sib = new Biquad(); int nb; double aA, aR; double inE, outE; double nzLp; bool built;
    public VocoderFx()
    {
        Type = "vocoder"; Title = "Vocoder"; Category = "Telephone & Distortion";
        for (int i = 0; i < MAXB; i++) { mod1[i] = new Biquad(); mod2[i] = new Biquad(); car1[i] = new Biquad(); car2[i] = new Biquad(); }
        AddC("carrier", "Carrier", Carriers, 0); AddP("note", "Note (MIDI)", 24, 96, 48, "") ; AddC("chord", "Notes", Chords, 0); AddC("bands", "Bands", BandsTxt, 2); AddP("lo", "Lowest band", 80, 600, 140, "Hz"); AddP("hi", "Highest band", 3000, 16000, 8000, "Hz");
        AddP("sharp", "Sharpness", 0.5f, 2.5f, 1.2f, ""); AddP("fshift", "Formant shift", -12, 12, 0, "st"); AddP("att", "Attack", 0.5f, 40, 4, "ms"); AddP("rel", "Release", 5, 200, 30, "ms");
        AddP("sib", "Consonants (let S and T through)", 0, 100, 40, "%"); AddP("dry", "Original voice", 0, 100, 0, "%"); AddP("level", "Level", -12, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update()
    {
        nb = BandN[(int)Get("bands")]; double lo = Get("lo"), hi = Math.Min(Get("hi"), SR * 0.45), sh = Math.Pow(2, Get("fshift") / 12.0); double bw = Math.Log(hi / lo, 2) / nb; double q = 1.0 / (2 * Math.Sinh(Math.Log(2) / 2 * bw)) * Get("sharp");
        for (int i = 0; i < nb; i++)
        {
            double fc = lo * Math.Pow(hi / lo, (i + 0.5) / nb), fm = Math.Min(fc * sh, SR * 0.45);
            car1[i].Set(Biquad.BandPass, SR, fc, q, 0); car2[i].Set(Biquad.BandPass, SR, fc, q, 0); mod1[i].Set(Biquad.BandPass, SR, fm, q, 0); mod2[i].Set(Biquad.BandPass, SR, fm, q, 0);
        }
        sib.Set(Biquad.HighPass, SR, 5500, 0.7071, 0); aA = DspMath.Tc(Get("att"), SR); aR = DspMath.Tc(Get("rel"), SR);
    }
    public override void Reset() { base.Reset(); for (int i = 0; i < MAXB; i++) { mod1[i].Reset(); mod2[i].Reset(); car1[i].Reset(); car2[i].Reset(); env[i] = 0; } inE = outE = 0; sib.Reset(); }
    static double Blep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int car = (int)Get("carrier"); var sems = Sems[(int)Get("chord")]; int nv = sems.Length; double f0 = 440 * Math.Pow(2, (Get("note") - 69) / 12.0); double[] fr = new double[nv]; for (int v = 0; v < nv; v++) fr[v] = f0 * Math.Pow(2, sems[v] / 12.0);
        double mix = Get("mix") / 100.0, dry = Get("dry") / 100.0, sibA = Get("sib") / 100.0 * 2.0, lev = DspMath.DbToLin(Get("level"));
        for (int i = 0; i < n; i++)
        {
            double mIn = (l[i] + r[i]) * 0.5, cs = 0;
            if (car != 2) for (int v = 0; v < nv; v++)
                {
                    double dt = fr[v] / SR; ph[v] += dt; if (ph[v] >= 1) ph[v] -= 1; double t = ph[v];
                    if (car == 1) { double p = t < 0.3 ? 1 : -1; p += Blep(t, dt); p -= Blep((t + 0.7) % 1.0, dt); cs += p; } else { double s = 2 * t - 1; s -= Blep(t, dt); cs += s; }
                }
            cs /= Math.Max(1, Math.Sqrt(nv));
            double nz = rnd.NextDouble() * 2 - 1; if (car >= 2) { nzLp += 0.5 * (nz - nzLp); cs = car == 2 ? nz : cs * 0.8 + nz * 0.35; }
            double outS = 0;
            for (int b = 0; b < nb; b++)
            {
                double m = mod2[b].TickL(mod1[b].TickL((float)mIn)); double a = Math.Abs(m); env[b] = a > env[b] ? a + (env[b] - a) * aA : a + (env[b] - a) * aR;
                double c = car2[b].TickL(car1[b].TickL((float)cs)); outS += c * env[b];
            }
            outS *= 6.0;
            double sb = sib.TickL((float)mIn) * sibA; outS += sb;
            // keep the loudness near the input's
            inE += (mIn * mIn - inE) * 0.0005; outE += (outS * outS - outE) * 0.0005; double g = 1; if (outE > 1e-10 && inE > 1e-10) g = Math.Max(0.05, Math.Min(40, Math.Sqrt(inE / outE)));
            double o = (outS * g * lev) * (1 - dry) + mIn * dry; l[i] = Dsp2.Sanitize((float)(l[i] + (o - l[i]) * mix)); r[i] = Dsp2.Sanitize((float)(r[i] + (o - r[i]) * mix));
        }
    }
}
