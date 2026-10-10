using System;
using System.Collections.Generic;
using System.Threading;

// ======================================================================
//  EQ and clarity engines: graphic EQ, tilt, mid/side EQ, dynamic EQ, vowel filter, linear-phase EQ,
//  and spectral tools (resonance suppressor, spectral shaper, automatic EQ).
// ======================================================================

class GraphicEqFx : Fx
{
    public static readonly double[] Freqs = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
    Biquad[] bq = new Biquad[10]; bool[] act = new bool[10];
    public GraphicEqFx()
    {
        Type = "geq"; Title = "Graphic EQ"; Category = "EQ & Clarity";
        for (int i = 0; i < 10; i++) { AddP("g" + i, Freqs[i] >= 1000 ? (Freqs[i] / 1000).ToString("0") + " kHz" : Freqs[i].ToString("0") + " Hz", -15, 15, 0, "dB"); bq[i] = new Biquad(); }
        AddP("out", "Output", -12, 12, 0, "dB");
    }
    protected override void Update() { for (int i = 0; i < 10; i++) { double g = Get("g" + i); act[i] = Math.Abs(g) > 0.01; bq[i].Set(Biquad.Bell, SR, Math.Min(Freqs[i], SR * 0.45), 1.4, g); } }
    public override void Reset() { base.Reset(); foreach (var b in bq) b.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); for (int i = 0; i < 10; i++) if (act[i]) bq[i].Block(l, r, n);
        float g = DspMath.DbToLin(Get("out")); if (Math.Abs(g - 1) > 0.001f) for (int i = 0; i < n; i++) { l[i] *= g; r[i] *= g; }
    }
}

class TiltEqFx : Fx
{
    Biquad lo = new Biquad(), hi = new Biquad();
    public TiltEqFx() { Type = "tilt"; Title = "Tilt EQ"; Category = "EQ & Clarity"; AddP("tilt", "Tilt (bright + / dark -)", -9, 9, 2, "dB"); AddP("pivot", "Pivot frequency", 150, 5000, 650, "Hz"); AddP("out", "Output", -12, 12, 0, "dB"); }
    protected override void Update() { double t = Get("tilt"), f = Get("pivot"); lo.Set(Biquad.LowShelf, SR, f, 0.6, -t * 0.5); hi.Set(Biquad.HighShelf, SR, f, 0.6, t * 0.5); }
    public override void Reset() { base.Reset(); lo.Reset(); hi.Reset(); }
    public override void Process(float[] l, float[] r, int n) { CheckDirty(); lo.Block(l, r, n); hi.Block(l, r, n); float g = DspMath.DbToLin(Get("out")); if (Math.Abs(g - 1) > 0.001f) for (int i = 0; i < n; i++) { l[i] *= g; r[i] *= g; } }
}

// three bands on each of two channels: Mid and Side, or Left and Right
class MsEqFx : Fx
{
    Biquad[] a = { new Biquad(), new Biquad(), new Biquad() }, b = { new Biquad(), new Biquad(), new Biquad() }; bool[] ao = new bool[3], bo = new bool[3]; bool ms;
    public MsEqFx()
    {
        Type = "mseq"; Title = "Mid-Side EQ"; Category = "EQ & Clarity"; AddC("mode", "Works on", new[] { "Mid and Side", "Left and Right" }, 0);
        foreach (var ch in new[] { "a", "b" })
        {
            string nm = ch == "a" ? "Mid / Left" : "Side / Right";
            AddP(ch + "_lf", nm + ": low shelf freq", 40, 500, 120, "Hz"); AddP(ch + "_lg", nm + ": low shelf gain", -15, 15, 0, "dB");
            AddP(ch + "_mf", nm + ": bell freq", 100, 12000, 1500, "Hz"); AddP(ch + "_mg", nm + ": bell gain", -15, 15, 0, "dB"); AddP(ch + "_mq", nm + ": bell Q", 0.3f, 8, 1.0f, "");
            AddP(ch + "_hf", nm + ": high shelf freq", 2000, 16000, 8000, "Hz"); AddP(ch + "_hg", nm + ": high shelf gain", -15, 15, 0, "dB");
        }
    }
    protected override void Update()
    {
        ms = Get("mode") < 0.5f; Biquad[] x = a; for (int c = 0; c < 2; c++, x = b)
        {
            string p = c == 0 ? "a" : "b"; x[0].Set(Biquad.LowShelf, SR, Get(p + "_lf"), 0.7, Get(p + "_lg")); x[1].Set(Biquad.Bell, SR, Get(p + "_mf"), Get(p + "_mq"), Get(p + "_mg")); x[2].Set(Biquad.HighShelf, SR, Get(p + "_hf"), 0.7, Get(p + "_hg"));
            bool[] o = c == 0 ? ao : bo; o[0] = Math.Abs(Get(p + "_lg")) > 0.01; o[1] = Math.Abs(Get(p + "_mg")) > 0.01; o[2] = Math.Abs(Get(p + "_hg")) > 0.01;
        }
    }
    public override void Reset() { base.Reset(); foreach (var q in a) q.Reset(); foreach (var q in b) q.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            float x = l[i], y = r[i]; if (ms) { float m = (x + y) * 0.5f, s = (x - y) * 0.5f; x = m; y = s; }
            for (int k = 0; k < 3; k++) { if (ao[k]) x = a[k].TickL(x); if (bo[k]) y = b[k].TickL(y); }
            if (ms) { l[i] = x + y; r[i] = x - y; } else { l[i] = x; r[i] = y; }
        }
    }
}

// up to three EQ bands that only act when their own frequency range gets loud (de-mud, de-harsh, tame a resonance)
class DynEqFx : Fx
{
    class Band { public Biquad det = new Biquad(), bell = new Biquad(); public double env, gDb; }
    Band[] bd = { new Band(), new Band(), new Band() }; double aA, aR; int sub;
    public DynEqFx()
    {
        Type = "dyneq"; Title = "Dynamic EQ"; Category = "EQ & Clarity"; float[] f = { 250, 3200, 8000 }, thr = { -32, -34, -36 };
        for (int i = 0; i < 3; i++)
        {
            int n = i + 1; AddC("b" + n + "_on", "Band " + n, new[] { "Off", "On" }, i == 0 ? 1 : 0); AddC("b" + n + "_m", "Band " + n + " action", new[] { "Cut when loud", "Boost when loud" }, 0);
            AddP("b" + n + "_f", "Band " + n + " freq", 40, 16000, f[i], "Hz"); AddP("b" + n + "_q", "Band " + n + " width (Q)", 0.3f, 8, 1.2f, ""); AddP("b" + n + "_t", "Band " + n + " threshold", -70, -6, thr[i], "dB"); AddP("b" + n + "_r", "Band " + n + " range", 0, 18, 6, "dB");
        }
        AddP("att", "Attack", 0.5f, 100, 8, "ms"); AddP("rel", "Release", 10, 1000, 140, "ms");
    }
    protected override void Update() { aA = DspMath.Tc(Get("att"), SR); aR = DspMath.Tc(Get("rel"), SR); for (int i = 0; i < 3; i++) { int n = i + 1; bd[i].det.Set(Biquad.BandPass, SR, Get("b" + n + "_f"), Get("b" + n + "_q") + 0.5, 0); } }
    public override void Reset() { base.Reset(); foreach (var b in bd) { b.det.Reset(); b.bell.Reset(); b.env = 0; b.gDb = 0; } }
    public override void Process(float[] l, float[] r, int nn)
    {
        CheckDirty(); bool[] on = new bool[3]; for (int i = 0; i < 3; i++) on[i] = Get("b" + (i + 1) + "_on") > 0.5f;
        for (int s = 0; s < nn; s++)
        {
            float m = (l[s] + r[s]) * 0.5f; bool upd = (sub++ & 15) == 0;
            for (int i = 0; i < 3; i++)
            {
                if (!on[i]) continue; var b = bd[i]; int n = i + 1; double d = Math.Abs(b.det.TickL(m)); b.env = d > b.env ? d + (b.env - d) * aA : d + (b.env - d) * aR;
                if (upd)
                {
                    double db = 20 * Math.Log10(b.env + 1e-9), thr = Get("b" + n + "_t"), range = Get("b" + n + "_r"); double x = Math.Max(0, Math.Min(1, (db - thr) / 12.0)); double target = (Get("b" + n + "_m") > 0.5f ? 1 : -1) * range * x; b.gDb += (target - b.gDb) * 0.3;
                    b.bell.Set(Biquad.Bell, SR, Get("b" + n + "_f"), Get("b" + n + "_q"), b.gDb);
                }
                if (Math.Abs(b.gDb) > 0.02) { l[s] = b.bell.TickL(l[s]); r[s] = b.bell.TickR(r[s]); }
            }
        }
    }
}

// three formant filters that morph between vowels: a talking, wah-like filter
class VowelFx : Fx
{
    static readonly double[][] V = { new[] { 730.0, 1090, 2440 }, new[] { 530.0, 1840, 2480 }, new[] { 270.0, 2290, 3010 }, new[] { 570.0, 840, 2410 }, new[] { 300.0, 870, 2240 } };
    static readonly double[] Amp = { 1.0, 0.5, 0.25 };
    Biquad[] f = { new Biquad(), new Biquad(), new Biquad() }; double lfoPh; int sub; Lfo2 lfo = new Lfo2();
    public VowelFx()
    {
        Type = "vowel"; Title = "Formant Filter"; Category = "EQ & Clarity";
        AddP("vowel", "Vowel (A  E  I  O  U)", 0, 4, 0, ""); AddP("q", "Sharpness", 2, 25, 9, ""); AddC("shape", "Movement", new[] { "Off", "Sine", "Triangle", "Saw up", "Saw down", "Random" }, 0); AddP("rate", "Movement rate", 0.05f, 12, 0.8f, "Hz"); AddP("depth", "Movement depth", 0, 4, 2, "");
        AddP("drive", "Drive", 0, 24, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); foreach (var b in f) b.Reset(); lfo.Reset(); }
    static readonly int[] Map = { -1, 0, 1, 2, 3, 5 };
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double q = Get("q"), pos = Get("vowel"); int sh = (int)Get("shape"); double dep = Get("depth"), rate = Get("rate") / SR, mix = Get("mix") / 100.0, drive = DspMath.DbToLin(Get("drive"));
        for (int i = 0; i < n; i++)
        {
            if ((sub++ & 31) == 0)
            {
                double p = pos + (sh > 0 ? lfo.Tick(Map[sh], rate * 32) * dep : 0); p = Math.Max(0, Math.Min(4, p)); int a = (int)Math.Min(3, Math.Floor(p)); double fr = p - a;
                for (int k = 0; k < 3; k++) { double fc = V[a][k] + (V[a + 1][k] - V[a][k]) * fr; f[k].Set(Biquad.BandPass, SR, Math.Min(fc, SR * 0.45), q, 0); }
            }
            float m = (l[i] + r[i]) * 0.5f; float x = (float)(m * drive); float ol = 0; for (int k = 0; k < 3; k++) ol += f[k].TickL(x) * (float)(Amp[k] * 2.2);
            if (drive > 1.01) ol = (float)Math.Tanh(ol);
            l[i] = (float)(l[i] + (ol - l[i]) * mix); r[i] = (float)(r[i] + (ol - r[i]) * mix);
        }
    }
}

// ----------------------------------------------------------------------
//  Convolution core with a swappable impulse response (used by the linear-phase EQ)
// ----------------------------------------------------------------------
class ConvCore
{
    class Rig { public Convolver c; }
    volatile Rig rig; readonly float[] inb = new float[Convolver.P], oq = new float[Convolver.P], dry = new float[Convolver.P]; int fill, rd;
    public void SetIr(float[] ir) { rig = new Rig { c = new Convolver(ir) }; }
    public bool Ready { get { return rig != null; } }
    public void Reset() { fill = 0; rd = 0; Array.Clear(oq, 0, oq.Length); Array.Clear(inb, 0, inb.Length); Array.Clear(dry, 0, dry.Length); var rg = rig; if (rg != null) rg.c.Reset(); }
    // convolves one channel in place; output is delayed by Convolver.P samples. Until an IR exists the signal passes (delayed).
    public void Process(float[] x, int n)
    {
        var rg = rig; int P = Convolver.P;
        for (int i = 0; i < n; i++)
        {
            float o = rg != null ? oq[rd] : dry[rd]; rd++; inb[fill] = x[i]; fill++; x[i] = o;
            if (fill >= P) { Array.Copy(inb, dry, P); if (rg != null) rg.c.Process(inb, oq); fill = 0; rd = 0; }
        }
    }
}

// the same six-band EQ as Parametric EQ, but with no phase change at all (a long FIR filter). Adds delay.
class LinEqFx : ParametricEq
{
    const int N = 4096; ConvCore cl = new ConvCore(), cr = new ConvCore(); string sig = ""; int ver;
    public LinEqFx() { Type = "lineq"; Title = "Linear-Phase EQ"; Category = "EQ & Clarity"; }
    public override int Latency { get { return N / 2 + Convolver.P; } }
    protected override void Update()
    {
        base.Update(); var vals = new StringBuilder2(); foreach (var p in P) vals.Add(p.Val); string s = vals.ToString() + SR; if (s == sig) return; sig = s; int v = ++ver; int sr = SR; var tmp = new ParametricEq(); tmp.SR = sr; for (int i = 0; i < P.Count && i < tmp.P.Count; i++) tmp.P[i].Val = P[i].Val;
        ThreadPool.QueueUserWorkItem(o =>
        {
            try
            {
                if (v != ver) return; var dbs = new double[N / 2 + 1]; for (int k = 0; k <= N / 2; k++) dbs[k] = tmp.ResponseDb(Math.Max(1, k * (double)sr / N));
                var re = new double[N]; var im = new double[N];
                for (int k = 0; k <= N / 2; k++) { double m = Math.Pow(10, dbs[k] / 20.0); re[k] = m; if (k > 0 && k < N / 2) re[N - k] = m; }
                Fft.Transform(re, im, true); var h = new float[N];
                for (int i = 0; i < N; i++) { int src = (i - N / 2 + N) % N; double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * (i + 0.5) / N); h[i] = (float)(re[src] * w); }
                if (v != ver) return; cl.SetIr(h); cr.SetIr(h);
            }
            catch { }
        });
    }
    class StringBuilder2 { System.Text.StringBuilder sb = new System.Text.StringBuilder(); public void Add(float v) { sb.Append(v.ToString("R")).Append(','); } public override string ToString() { return sb.ToString(); } }
    public override void Reset() { base.Reset(); cl.Reset(); cr.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (!cl.Ready) { cl.Process(l, n); cr.Process(r, n); return; }
        cl.Process(l, n); cr.Process(r, n);
    }
}

// ----------------------------------------------------------------------
//  Spectral engines (short-time Fourier transform)
// ----------------------------------------------------------------------
abstract class StftFx : Fx
{
    protected int N = 2048; protected Stft[] st = new Stft[2]; float[] ringL, ringR; int rw; protected bool inited; protected double mixv = 1, sampleSR;
    protected abstract void Spec(int ch, double[] re, double[] im);
    protected virtual void Setup() { }
    protected void AddMix(float def) { AddP("mix", "Mix", 0, 100, def, "%"); }
    public override int Latency { get { return N - 1; } }
    protected override void Update() { if (!inited) { for (int c = 0; c < 2; c++) { int cc = c; st[c] = new Stft(N, 4); st[c].OnFrame = (re, im) => Spec(cc, re, im); } ringL = new float[N - 1]; ringR = new float[N - 1]; inited = true; Setup(); } mixv = Get("mix") / 100.0; SetupParams(); }
    protected virtual void SetupParams() { }
    public override void Reset() { base.Reset(); if (inited) { st[0].Reset(); st[1].Reset(); Array.Clear(ringL, 0, ringL.Length); Array.Clear(ringR, 0, ringR.Length); Setup(); } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (!inited) return; float mx = (float)mixv; int M = N - 1;
        for (int i = 0; i < n; i++)
        {
            float dl = ringL[rw], dr = ringR[rw]; ringL[rw] = l[i]; ringR[rw] = r[i]; if (++rw >= M) rw = 0;
            float a = st[0].Tick(l[i]), b = st[1].Tick(r[i]); l[i] = Dsp2.Sanitize(dl + (a - dl) * mx); r[i] = Dsp2.Sanitize(dr + (b - dr) * mx);
        }
    }
    protected static void Sym(double[] re, double[] im, int N) { int H = N / 2; for (int k = 1; k < H; k++) { re[N - k] = re[k]; im[N - k] = -im[k]; } im[0] = 0; im[H] = 0; }
}

// finds narrow spikes that stick out of the spectrum (ringing, whistles, harsh peaks) and presses them down only while they are there
class ResSupFx : StftFx
{
    double[][] g = new double[2][]; double[] mag, env, cum; double thr, depth, maxCut, flo, fhi, rel;
    public ResSupFx()
    {
        Type = "ressup"; Title = "Resonance Suppressor"; Category = "EQ & Clarity";
        AddP("depth", "Depth", 0, 100, 60, "%"); AddP("sens", "Sensitivity", 0, 100, 55, "%"); AddP("cut", "Most reduction", 1, 24, 10, "dB"); AddP("lo", "From", 80, 8000, 250, "Hz"); AddP("hi", "To", 1000, 20000, 9000, "Hz"); AddP("rel", "Recovery", 20, 600, 120, "ms"); AddMix(100);
    }
    protected override void Setup() { for (int c = 0; c < 2; c++) g[c] = new double[N / 2 + 1]; mag = new double[N / 2 + 1]; env = new double[N / 2 + 1]; cum = new double[N / 2 + 2]; }
    protected override void SetupParams() { depth = Get("depth") / 100.0; thr = 1.0 + (1 - Get("sens") / 100.0) * 2.2; maxCut = Get("cut"); flo = Get("lo"); fhi = Get("hi"); rel = Math.Exp(-(st[0].Hop / (double)SR) / (Get("rel") * 0.001)); }
    protected override void Spec(int ch, double[] re, double[] im)
    {
        int H = N / 2; cum[0] = 0; for (int k = 0; k <= H; k++) { mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); cum[k + 1] = cum[k] + mag[k]; }
        double binHz = SR / (double)N; var gs = g[ch];
        for (int k = 1; k < H; k++)
        {
            int w = (int)(2 + k * 0.10); int a = Math.Max(0, k - w), b = Math.Min(H, k + w); double e = (cum[b + 1] - cum[a]) / (b - a + 1); double ratio = mag[k] / Math.Max(1e-9, e); double hz = k * binHz;
            double band = hz < flo ? Math.Max(0, 1 - (flo - hz) / (flo * 0.5)) : (hz > fhi ? Math.Max(0, 1 - (hz - fhi) / (fhi * 0.3)) : 1);
            double want = 0; if (ratio > thr) want = -Math.Min(maxCut, depth * 20 * Math.Log10(ratio / thr) * 1.6) * band;
            gs[k] = want < gs[k] ? want : gs[k] * rel + want * (1 - rel);
            double gl = Math.Pow(10, gs[k] / 20.0); re[k] *= gl; im[k] *= gl;
        }
        Sym(re, im, N);
    }
}

// broad tone shaping of the spectrum, plus flattening or exaggerating its peaks
class SpectralShaperFx : StftFx
{
    double[] mag, cum, gcur; double lo, mid, hi, tilt, peaks;
    public SpectralShaperFx()
    {
        Type = "specshape"; Title = "Spectral Shaper"; Category = "EQ & Clarity";
        AddP("low", "Lows (below 200 Hz)", -18, 18, 0, "dB"); AddP("mid", "Mids (200 Hz - 3 kHz)", -18, 18, 0, "dB"); AddP("high", "Highs (above 3 kHz)", -18, 18, 0, "dB"); AddP("tilt", "Tilt per octave", -6, 6, 0, "dB"); AddP("peaks", "Peaks: flatten (-) or exaggerate (+)", -100, 100, 0, "%"); AddMix(100);
    }
    protected override void Setup() { mag = new double[N / 2 + 1]; cum = new double[N / 2 + 2]; }
    protected override void SetupParams() { lo = Get("low"); mid = Get("mid"); hi = Get("high"); tilt = Get("tilt"); peaks = Get("peaks") / 100.0; }
    protected override void Spec(int ch, double[] re, double[] im)
    {
        int H = N / 2; cum[0] = 0; for (int k = 0; k <= H; k++) { mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); cum[k + 1] = cum[k] + mag[k]; }
        double binHz = SR / (double)N;
        for (int k = 1; k < H; k++)
        {
            double hz = k * binHz; double dB = hz < 200 ? lo : (hz > 3000 ? hi : mid); double edge = hz < 200 ? 1 : 0; // soft joins between regions
            double wl = 1 / (1 + Math.Pow(hz / 200.0, 4)), wh = 1 / (1 + Math.Pow(3000.0 / hz, 4)); dB = lo * wl + hi * wh + mid * (1 - wl) * (1 - wh);
            dB += tilt * Math.Log(hz / 1000.0, 2);
            double g = Math.Pow(10, dB / 20.0);
            if (Math.Abs(peaks) > 0.01) { int w = (int)(2 + k * 0.1); int a = Math.Max(0, k - w), b = Math.Min(H, k + w); double e = (cum[b + 1] - cum[a]) / (b - a + 1); double ratio = Math.Max(1e-6, mag[k] / Math.Max(1e-9, e)); g *= Math.Pow(ratio, peaks * 0.6); }
            re[k] *= g; im[k] *= g;
        }
        Sym(re, im, N);
    }
}

// learns the average tone of what you play and gently steers it toward a target curve
class AutoEqFx : StftFx
{
    public static readonly string[] Targets = { "Natural (pink)", "Bright", "Warm", "Vocal (presence)", "Flat (white)" };
    double[][] avg = new double[2][], gsm = new double[2][]; double[] mag, curve; double amount, range, adapt, tgtTilt; int target; int[] frames = new int[2];
    public AutoEqFx()
    {
        Type = "autoeq"; Title = "Automatic EQ"; Category = "EQ & Clarity";
        AddC("target", "Aim for", Targets, 0); AddP("amount", "Amount", 0, 100, 60, "%"); AddP("range", "Most change", 1, 12, 6, "dB"); AddP("adapt", "Learns over", 1, 30, 8, "s"); AddMix(100);
    }
    protected override void Setup() { for (int c = 0; c < 2; c++) { avg[c] = new double[N / 2 + 1]; gsm[c] = new double[N / 2 + 1]; } mag = new double[N / 2 + 1]; curve = new double[N / 2 + 1]; frames[0] = frames[1] = 0; }
    protected override void SetupParams() { amount = Get("amount") / 100.0; range = Get("range"); adapt = Get("adapt"); target = (int)Get("target"); }
    double Target(double hz)
    {
        double oct = Math.Log(Math.Max(20, hz) / 1000.0, 2);
        switch (target) { case 0: return -3.0 * oct; case 1: return -1.5 * oct; case 2: return -4.5 * oct; case 3: return -3.0 * oct + 3.5 * Math.Exp(-Math.Pow((Math.Log(hz / 3200.0, 2)) / 0.8, 2)); default: return 0; }
    }
    protected override void Spec(int ch, double[] re, double[] im)
    {
        int H = N / 2; double e = 0; for (int k = 0; k <= H; k++) { mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); e += mag[k] * mag[k]; }
        var av = avg[ch]; double alpha = Math.Min(1, st[0].Hop / (SR * adapt)); bool loud = e / H > 1e-7;
        if (loud) { if (frames[ch] == 0) Array.Copy(mag, av, H + 1); else for (int k = 0; k <= H; k++) av[k] += (mag[k] - av[k]) * alpha; frames[ch]++; }
        if (frames[ch] < 8) { return; }
        double binHz = SR / (double)N; var gs = gsm[ch];
        // smoothed log spectrum (about 1/3 octave), then the difference from the target
        double sum = 0; int cnt = 0; for (int k = 1; k < H; k++) { int w = Math.Max(1, (int)(k * 0.12)); int a = Math.Max(1, k - w), b = Math.Min(H - 1, k + w); double s = 0; for (int j = a; j <= b; j++) s += av[j]; s /= (b - a + 1); double ldb = 20 * Math.Log10(s + 1e-12) - Target(k * binHz); curve[k] = ldb; double hz = k * binHz; if (hz > 120 && hz < 8000) { sum += ldb; cnt++; } }
        double c0 = cnt > 0 ? sum / cnt : 0;
        for (int k = 1; k < H; k++)
        {
            double want = -(curve[k] - c0) * amount; want = Math.Max(-range, Math.Min(range, want)); double hz = k * binHz; if (hz < 60) want *= hz / 60; if (hz > 14000) want *= Math.Max(0, 1 - (hz - 14000) / 4000.0);
            gs[k] += (want - gs[k]) * 0.2; double g = Math.Pow(10, gs[k] / 20.0); re[k] *= g; im[k] *= g;
        }
        Sym(re, im, N);
    }
}
