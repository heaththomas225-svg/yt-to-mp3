using System;
using System.Collections.Generic;

// ======================================================================
//  Bass and EDM engines: sub generator, multiband distortion, OTT-style multiband dynamics, phaser, flanger (with barber-pole), amp + cabinet
// ======================================================================

// adds a note an octave below the bass: a divider (square, filtered to a sine) or a pitch-following sine
class SubFx : Fx
{
    Biquad lp = new Biquad(), subLp = new Biquad(), lp2 = new Biquad(); double env, phase, fEst = 60, last; int cnt; bool st, armed = true; float prev;
    public SubFx()
    {
        Type = "sub"; Title = "Sub-Bass Generator"; Category = "Bass & EDM";
        AddC("mode", "Method", new[] { "Octave divider (classic)", "Sine that follows the note" }, 1); AddP("fc", "Follows notes below", 50, 400, 150, "Hz"); AddP("tone", "Sub softness (high cut)", 40, 240, 100, "Hz"); AddP("level", "Sub level", -30, 12, -4, "dB");
        AddP("thr", "Only above", -70, -20, -48, "dB"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("cut", "Remove original lows below", 0, 200, 0, "Hz");
    }
    Biquad hpOrig = new Biquad();
    protected override void Update() { lp.Set(Biquad.LowPass, SR, Get("fc"), 0.7071, 0); lp2.Set(Biquad.LowPass, SR, Get("fc"), 0.7071, 0); subLp.Set(Biquad.LowPass, SR, Get("tone"), 0.7071, 0); hpOrig.Set(Biquad.HighPass, SR, Math.Max(20, Get("cut")), 0.7071, 0); }
    public override void Reset() { base.Reset(); lp.Reset(); lp2.Reset(); subLp.Reset(); hpOrig.Reset(); env = 0; phase = 0; fEst = 60; cnt = 0; st = false; prev = 0; armed = true; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double lv = DspMath.DbToLin(Get("level")), thr = DspMath.DbToLin(Get("thr")), mix = Get("mix") / 100.0; bool cut = Get("cut") > 20.5f;
        double ae = 1 - Math.Exp(-1.0 / (0.004 * SR)), re = 1 - Math.Exp(-1.0 / (0.05 * SR)), fa = 0.2;
        for (int i = 0; i < n; i++)
        {
            float m = (l[i] + r[i]) * 0.5f; float x = lp2.TickL(lp.TickL(m)); double a = Math.Abs(x); env += (a - env) * (a > env ? ae : re); cnt++;
            if (armed && x > 0.0008f && prev <= 0.0008f) { // a rising crossing
                if (mode == 0) st = !st; else { if (cnt > SR / 420 && cnt < SR / 28) fEst += (SR / (double)cnt - fEst) * fa; }
                cnt = 0; armed = false; }
            if (x < -0.0008f) armed = true; prev = x;
            double sub = 0; if (env > thr)
            {
                if (mode == 0) sub = subLp.TickL(st ? 1f : -1f) * 1.8; else { phase += fEst * 0.5 / SR; phase -= Math.Floor(phase); sub = Math.Sin(2 * Math.PI * phase) * Math.Min(1.0, env * 6); }
                sub *= Math.Min(1.0, env * 7);
            }
            else if (mode == 0) subLp.TickL(0f);
            float o = (float)(sub * lv * mix); float ol = l[i], or = r[i]; if (cut) { ol = hpOrig.TickL(ol); or = hpOrig.TickR(or); }
            l[i] = Dsp2.Sanitize(ol + o); r[i] = Dsp2.Sanitize(or + o);
        }
    }
}

// three-band split (Linkwitz-Riley 4th order) used by the multiband engines
sealed class Split3
{
    Biquad[] a = new Biquad[8]; public Split3() { for (int i = 0; i < 8; i++) a[i] = new Biquad(); }
    public void Set(int sr, double f1, double f2)
    {
        a[0].Set(Biquad.LowPass, sr, f1, 0.7071, 0); a[1].Set(Biquad.LowPass, sr, f1, 0.7071, 0); a[2].Set(Biquad.HighPass, sr, f1, 0.7071, 0); a[3].Set(Biquad.HighPass, sr, f1, 0.7071, 0);
        a[4].Set(Biquad.LowPass, sr, f2, 0.7071, 0); a[5].Set(Biquad.LowPass, sr, f2, 0.7071, 0); a[6].Set(Biquad.HighPass, sr, f2, 0.7071, 0); a[7].Set(Biquad.HighPass, sr, f2, 0.7071, 0);
    }
    public void Reset() { foreach (var b in a) b.Reset(); }
    public void Run(float x, bool right, out float lo, out float mid, out float hi)
    {
        if (!right) { lo = a[1].TickL(a[0].TickL(x)); float rest = a[3].TickL(a[2].TickL(x)); mid = a[5].TickL(a[4].TickL(rest)); hi = a[7].TickL(a[6].TickL(rest)); }
        else { lo = a[1].TickR(a[0].TickR(x)); float rest = a[3].TickR(a[2].TickR(x)); mid = a[5].TickR(a[4].TickR(rest)); hi = a[7].TickR(a[6].TickR(rest)); }
    }
}

class MBDistFx : Fx
{
    Split3 sp = new Split3();
    public MBDistFx()
    {
        Type = "mbdist"; Title = "Multiband Distortion"; Category = "Bass & EDM";
        AddP("f1", "Low / mid split", 60, 600, 160, "Hz"); AddP("f2", "Mid / high split", 600, 8000, 2400, "Hz");
        string[] nm = { "Low", "Mid", "High" }; float[] dr = { 10, 14, 8 }; int[] md = { 1, 3, 0 };
        for (int i = 0; i < 3; i++) { AddC("m" + i, nm[i] + " shape", ShaperFx.Modes, md[i]); AddP("d" + i, nm[i] + " drive", 0, 40, dr[i], "dB"); AddP("g" + i, nm[i] + " level", -24, 9, 0, "dB"); }
        AddP("out", "Output", -12, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { sp.Set(SR, Get("f1"), Get("f2")); }
    public override void Reset() { base.Reset(); sp.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int[] md = { (int)Get("m0"), (int)Get("m1"), (int)Get("m2") }; double[] dr = new double[3], gn = new double[3], cp = new double[3]; for (int i = 0; i < 3; i++) { dr[i] = DspMath.DbToLin(Get("d" + i)); gn[i] = DspMath.DbToLin(Get("g" + i)); cp[i] = 1.0 / Math.Pow(Math.Max(1.0, dr[i]), 0.55); }
        double outG = DspMath.DbToLin(Get("out")), mix = Get("mix") / 100.0; float[] b = new float[3];
        for (int i = 0; i < n; i++)
            for (int c = 0; c < 2; c++)
            {
                float x = c == 0 ? l[i] : r[i]; sp.Run(x, c == 1, out b[0], out b[1], out b[2]); double y = 0;
                for (int k = 0; k < 3; k++) y += ShaperFx.Sh(b[k] * dr[k], md[k]) * cp[k] * gn[k];
                float o = (float)((x + (y * outG - x) * mix)); if (c == 0) l[i] = Dsp2.Sanitize(o); else r[i] = Dsp2.Sanitize(o);
            }
    }
}

// OTT-style: each of three bands is squashed when loud AND lifted when quiet, so everything ends up big and bright
class OttFx : Fx
{
    Split3 sp = new Split3(); double[,] env = new double[2, 3], gdb = new double[2, 3]; float[] xs = new float[2]; double[] outc = new double[2]; float[,] bands = new float[2, 3];
    public OttFx()
    {
        Type = "ott"; Title = "OTT-Style Multiband Dynamics"; Category = "Bass & EDM";
        AddP("depth", "Depth", 0, 100, 60, "%"); AddP("time", "Time (speed)", 10, 300, 100, "%"); AddP("lowg", "Low gain", -12, 12, 0, "dB"); AddP("midg", "Mid gain", -12, 12, 0, "dB"); AddP("highg", "High gain", -12, 12, 2, "dB");
        AddP("f1", "Low / mid split", 60, 400, 100, "Hz"); AddP("f2", "Mid / high split", 1000, 8000, 2500, "Hz"); AddP("thrd", "Squash above", -40, 0, -18, "dB"); AddP("thru", "Lift below", -70, -10, -42, "dB"); AddP("in", "Input", -12, 12, 0, "dB"); AddP("out", "Output", -18, 6, -3, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { sp.Set(SR, Get("f1"), Get("f2")); }
    public override void Reset() { base.Reset(); sp.Reset(); env = new double[2, 3]; gdb = new double[2, 3]; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double depth = Get("depth") / 100.0, sp2 = Get("time") / 100.0, inG = DspMath.DbToLin(Get("in")), outG = DspMath.DbToLin(Get("out")), mix = Get("mix") / 100.0, thrD = Get("thrd"), thrU = Get("thru");
        double[] bg = { DspMath.DbToLin(Get("lowg")), DspMath.DbToLin(Get("midg")), DspMath.DbToLin(Get("highg")) };
        double ea = 1 - Math.Exp(-1.0 / (0.0006 * SR / sp2)), er = 1 - Math.Exp(-1.0 / (0.045 * SR / sp2)), ga = 1 - Math.Exp(-1.0 / (0.003 * SR / sp2)), gr = 1 - Math.Exp(-1.0 / (0.06 * SR / sp2)); float[] b = new float[3]; double[] link = new double[3];
        for (int i = 0; i < n; i++)
        {
            float dl = l[i], dr = r[i]; xs[0] = (float)(dl * inG); xs[1] = (float)(dr * inG);
            for (int c = 0; c < 2; c++) { sp.Run(xs[c], c == 1, out b[0], out b[1], out b[2]); for (int k = 0; k < 3; k++) bands[c, k] = b[k]; }
            for (int k = 0; k < 3; k++)
            {
                double a = Math.Max(Math.Abs(bands[0, k]), Math.Abs(bands[1, k])); double e = env[0, k]; e += (a - e) * (a > e ? ea : er); env[0, k] = e;
                double L = 20 * Math.Log10(e + 1e-9), g = 0; if (L > thrD) g -= (L - thrD) * 0.75; if (L < thrU) g += Math.Min(24.0, (thrU - L) * 0.7);
                g *= depth; double cur = gdb[0, k]; cur += (g - cur) * (g < cur ? ga : gr); gdb[0, k] = cur; link[k] = Math.Pow(10, cur / 20.0) * bg[k];
            }
            for (int c = 0; c < 2; c++) { double y = 0; for (int k = 0; k < 3; k++) y += bands[c, k] * link[k]; outc[c] = y * outG; }
            l[i] = Dsp2.Sanitize((float)(dl + (outc[0] - dl) * mix)); r[i] = Dsp2.Sanitize((float)(dr + (outc[1] - dr) * mix));
        }
    }
}

class PhaserFx : SyncFx
{
    double[][] z = { new double[24], new double[24] }; double fbL, fbR; Lfo2[] lf = { new Lfo2(), new Lfo2() }; double freePh; int sub; double[] a = new double[2];
    public PhaserFx()
    {
        Type = "phaser"; Title = "Phaser"; Category = "Bass & EDM";
        AddC("stages", "Stages", new[] { "4", "6", "8", "12", "24" }, 2); AddC("sync", "Speed", Dsp2.SyncNames, 0); AddP("rate", "Free speed", 0.02f, 12, 0.35f, "Hz"); AddP("depth", "Depth", 0, 100, 75, "%"); AddP("center", "Centre", 80, 5000, 700, "Hz"); AddP("fb", "Feedback", -95, 95, 45, "%");
        AddP("offset", "Right side offset", 0, 180, 90, "deg"); AddC("shape", "Shape", new[] { "Sine", "Triangle" }, 0); AddP("mix", "Mix", 0, 100, 50, "%");
    }
    public override void Reset() { base.Reset(); z[0] = new double[24]; z[1] = new double[24]; fbL = fbR = 0; lf[0].Reset(); lf[1].Reset(); }
    static readonly int[] St = { 4, 6, 8, 12, 24 };
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int ns = St[(int)Get("stages")], sy = (int)Get("sync"); double rate = Get("rate"), depth = Get("depth") / 100.0, center = Get("center"), fb = Get("fb") / 100.0, off = Get("offset") / 360.0, mix = Get("mix") / 100.0; int shape = (int)Get("shape"); double cycle = sy > 0 ? Dsp2.SyncBeats[sy] : 0;
        for (int i = 0; i < n; i++)
        {
            double beat = BeatAt(i); double ph = cycle > 0 ? beat / cycle : freePh; if (cycle <= 0) { freePh += rate / SR; freePh -= Math.Floor(freePh); }
            if ((sub++ & 7) == 0)
                for (int c = 0; c < 2; c++) { double p = ph + c * off; p -= Math.Floor(p); double m = shape == 0 ? Math.Sin(p * 2 * Math.PI) : (p < 0.5 ? p * 4 - 1 : 3 - p * 4); double f = Math.Min(SR * 0.45, center * Math.Pow(2, depth * 2.2 * m)); double t = Math.Tan(Math.PI * f / SR); a[c] = (t - 1) / (t + 1); }
            for (int c = 0; c < 2; c++)
            {
                double x = c == 0 ? l[i] : r[i]; double s = x + (c == 0 ? fbL : fbR) * fb; var zz = z[c];
                for (int k = 0; k < ns; k++) { double y = a[c] * s + zz[k]; zz[k] = s - a[c] * y; s = y; }
                if (c == 0) fbL = s; else fbR = s; float o = (float)(x + (s - x) * mix * 0.5 * 2 / (1 + 0.0)); o = (float)((x * (1 - mix * 0.5) + s * mix * 0.5)); if (c == 0) l[i] = Dsp2.Sanitize(o); else r[i] = Dsp2.Sanitize(o);
            }
        }
        EndBlock(n);
    }
}

class FlangerFx : SyncFx
{
    float[] io = new float[2], ob = new float[2]; float[] bl, br; int len, w; double freePh, fbL, fbR, shPh; HilbertCh[] hb = { new HilbertCh(), new HilbertCh() };
    public FlangerFx()
    {
        Type = "flanger"; Title = "Flanger"; Category = "Bass & EDM";
        AddC("sync", "Speed", Dsp2.SyncNames, 0); AddP("rate", "Free speed", 0.02f, 10, 0.3f, "Hz"); AddP("manual", "Manual (delay centre)", 0.2f, 10, 2.2f, "ms"); AddP("depth", "Depth", 0, 8, 2.8f, "ms"); AddP("fb", "Feedback", -95, 95, 60, "%");
        AddP("offset", "Right side offset", 0, 180, 90, "deg"); AddC("shape", "Shape", new[] { "Sine", "Triangle" }, 1); AddC("barber", "Barber-pole (endless rise or fall)", new[] { "Off", "Rising", "Falling" }, 0); AddP("bshift", "Barber-pole speed", 0.05f, 4, 0.5f, "Hz"); AddP("mix", "Mix", 0, 100, 50, "%");
    }
    protected override void Update() { len = SR / 20 + 16; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } fbL = fbR = 0; hb[0].Reset(); hb[1].Reset(); shPh = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int sy = (int)Get("sync"); double rate = Get("rate"), man = Get("manual") * 0.001 * SR, dep = Get("depth") * 0.001 * SR, fb = Get("fb") / 100.0, off = Get("offset") / 360.0, mix = Get("mix") / 100.0; int shape = (int)Get("shape"), bar = (int)Get("barber"); double cycle = sy > 0 ? Dsp2.SyncBeats[sy] : 0, bs = Get("bshift") * (bar == 2 ? -1 : 1); double norm = 1.0 / (1 + 0.5 * mix);
        for (int i = 0; i < n; i++)
        {
            double ph = cycle > 0 ? BeatAt(i) / cycle : freePh; if (cycle <= 0) { freePh += rate / SR; freePh -= Math.Floor(freePh); }
            if (bar > 0) { shPh += bs / SR; shPh -= Math.Floor(shPh); }
            io[0] = l[i]; io[1] = r[i]; ob[0] = 0; ob[1] = 0;
            for (int c = 0; c < 2; c++)
            {
                double p = ph + c * off; p -= Math.Floor(p); double m = shape == 0 ? Math.Sin(p * 2 * Math.PI) : (p < 0.5 ? p * 4 - 1 : 3 - p * 4); double d = Math.Max(2, man + dep * m); var buf = c == 0 ? bl : br;
                float del = Dsp2.Hermite(buf, len, w - d); double fbs = del;
                if (bar > 0) { double ii, qq; hb[c].Tick(del, out ii, out qq); fbs = ii * Math.Cos(2 * Math.PI * shPh) - qq * Math.Sin(2 * Math.PI * shPh); }
                buf[w] = (float)(io[c] + fbs * fb); ob[c] = del;
            }
            if (++w >= len) w = 0;
            l[i] = Dsp2.Sanitize((float)((io[0] + ob[0] * mix) * norm)); r[i] = Dsp2.Sanitize((float)((io[1] + ob[1] * mix) * norm));
        }
        EndBlock(n);
    }
}

// guitar or bass amp: preamp (clean to fuzz), three-band tone, and a cabinet
class AmpFx : Fx
{
    public static readonly string[] Pre = { "Clean", "Crunch", "Overdrive", "High gain", "Fuzz", "Bass tube" }, Cabs = { "None (direct)", "1x12 combo", "2x12 open back", "4x12 stack", "Bass 8x10", "Bass 1x15", "Small radio speaker", "Megaphone horn" };
    static readonly int[] PreMode = { 0, 1, 3, 6, 6, 1 }; static readonly double[] PreK = { 0.6, 1.0, 1.2, 2.0, 1.6, 1.0 };
    // cabinet: list of (filter type, freq, q, gain dB) with 0 bell 1 low shelf 2 high shelf 3 low-pass 4 high-pass
    static readonly double[][][] CabTab = {
        new double[0][],
        new[] { new[] { 4.0, 90, 0.7, 0 }, new[] { 0.0, 140, 1.1, 3 }, new[] { 0.0, 2200, 1.3, 5 }, new[] { 0.0, 800, 1, -2 }, new[] { 3.0, 5200, 0.8, 0 }, new[] { 3.0, 6200, 0.7, 0 } },
        new[] { new[] { 4.0, 80, 0.7, 0 }, new[] { 0.0, 120, 1.0, 2 }, new[] { 0.0, 1800, 1.2, 4 }, new[] { 0.0, 3500, 1.5, -2 }, new[] { 3.0, 6000, 0.8, 0 } },
        new[] { new[] { 4.0, 75, 0.8, 0 }, new[] { 4.0, 75, 0.7, 0 }, new[] { 0.0, 110, 1.0, 4 }, new[] { 0.0, 1900, 1.1, 5 }, new[] { 0.0, 900, 1.0, -4 }, new[] { 3.0, 4800, 0.9, 0 }, new[] { 3.0, 4800, 0.7, 0 } },
        new[] { new[] { 4.0, 45, 0.7, 0 }, new[] { 0.0, 90, 1.0, 3 }, new[] { 0.0, 700, 1.0, 2 }, new[] { 0.0, 2500, 1.5, 2 }, new[] { 3.0, 4500, 0.8, 0 }, new[] { 3.0, 4500, 0.7, 0 } },
        new[] { new[] { 4.0, 40, 0.7, 0 }, new[] { 0.0, 80, 1.0, 4 }, new[] { 0.0, 400, 0.9, 2 }, new[] { 3.0, 3500, 0.8, 0 }, new[] { 3.0, 3500, 0.7, 0 } },
        new[] { new[] { 4.0, 300, 0.9, 0 }, new[] { 0.0, 1000, 1.5, 7 }, new[] { 0.0, 3200, 2.0, 4 }, new[] { 3.0, 6500, 0.8, 0 } },
        new[] { new[] { 4.0, 500, 0.9, 0 }, new[] { 4.0, 500, 0.7, 0 }, new[] { 0.0, 1800, 2.5, 9 }, new[] { 3.0, 4200, 0.9, 0 }, new[] { 3.0, 4200, 0.7, 0 } } };
    Biquad[] cab = new Biquad[8]; int ncab; Biquad bass = new Biquad(), mid = new Biquad(), treb = new Biquad(), pre = new Biquad(), post = new Biquad(); double dcL, dcR;
    public AmpFx()
    {
        Type = "amp"; Title = "Amp Simulator"; Category = "Bass & EDM";
        AddC("pre", "Amp", Pre, 2); AddP("gain", "Gain", 0, 48, 20, "dB"); AddP("tight", "Tightness (low cut before)", 20, 400, 70, "Hz"); AddP("bass", "Bass", -12, 12, 0, "dB"); AddP("mid", "Mid", -12, 12, 0, "dB"); AddP("midf", "Mid frequency", 250, 2500, 700, "Hz"); AddP("treble", "Treble", -12, 12, 0, "dB");
        AddC("cab", "Cabinet", Cabs, 1); AddP("level", "Level", -24, 12, -3, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
        for (int i = 0; i < 8; i++) cab[i] = new Biquad();
    }
    protected override void Update()
    {
        var t = CabTab[(int)Get("cab")]; ncab = t.Length; int[] map = { Biquad.Bell, Biquad.LowShelf, Biquad.HighShelf, Biquad.LowPass, Biquad.HighPass };
        for (int i = 0; i < ncab; i++) cab[i].Set(map[(int)t[i][0]], SR, Math.Min(t[i][1], SR * 0.45), t[i][2], t[i][3]);
        bass.Set(Biquad.LowShelf, SR, 110, 0.7, Get("bass")); mid.Set(Biquad.Bell, SR, Get("midf"), 0.9, Get("mid")); treb.Set(Biquad.HighShelf, SR, 3500, 0.7, Get("treble")); pre.Set(Biquad.HighPass, SR, Get("tight"), 0.7071, 0); post.Set(Biquad.LowPass, SR, Math.Min(12000, SR * 0.45), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); foreach (var b in cab) b.Reset(); bass.Reset(); mid.Reset(); treb.Reset(); pre.Reset(); post.Reset(); dcL = dcR = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int pi = (int)Get("pre"); double gain = DspMath.DbToLin(Get("gain")) * PreK[pi], lev = DspMath.DbToLin(Get("level")), mix = Get("mix") / 100.0, comp = 1.0 / Math.Pow(Math.Max(1.0, gain), 0.5); int mode = PreMode[pi]; double bias = pi == 5 ? 0.12 : 0;
        for (int i = 0; i < n; i++)
            for (int c = 0; c < 2; c++)
            {
                float x = c == 0 ? l[i] : r[i]; float v = c == 0 ? pre.TickL(x) : pre.TickR(x);
                double y = ShaperFx.Sh(v * gain + bias, mode) * comp * 1.4; if (c == 0) { double o = y - dcL; dcL += o * 0.0008; y = o; } else { double o = y - dcR; dcR += o * 0.0008; y = o; }
                float s = (float)y; s = c == 0 ? bass.TickL(s) : bass.TickR(s); s = c == 0 ? mid.TickL(s) : mid.TickR(s); s = c == 0 ? treb.TickL(s) : treb.TickR(s); s = c == 0 ? post.TickL(s) : post.TickR(s);
                for (int k = 0; k < ncab; k++) s = c == 0 ? cab[k].TickL(s) : cab[k].TickR(s);
                float o2 = (float)(x + (s * lev - x) * mix); if (c == 0) l[i] = Dsp2.Sanitize(o2); else r[i] = Dsp2.Sanitize(o2);
            }
    }
}
