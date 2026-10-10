using System;
using System.Collections.Generic;

// ======================================================================
//  Pitch and voice engines: phase-vocoder pitch shifter (up to 4 voices, formant control),
//  classic time-domain shifter (fast, grainy), formant shifter.
// ======================================================================

// streaming short-time Fourier transform with overlap-add (Hann in and out, 75% overlap)
class Stft
{
    public readonly int N, Hop, H; public readonly double[] Win; public readonly double[] Re, Im; readonly float[] ring; readonly double[] outR; int w; long t; readonly double scale;
    public Action<double[], double[]> OnFrame;
    public Stft(int n, int over)
    {
        N = n; H = n / 2; Hop = n / over; Win = new double[n]; Re = new double[n]; Im = new double[n]; ring = new float[n]; outR = new double[n];
        for (int i = 0; i < n; i++) Win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n);
        double s = 0; for (int k = 0; k < over; k++) { double v = Win[(k * Hop) % n]; s += v * v; } scale = 1.0 / s;
    }
    public void Reset() { Array.Clear(ring, 0, N); Array.Clear(outR, 0, N); w = 0; t = 0; }
    public float Tick(float x)
    {
        ring[w] = x; w++; if (w >= N) w = 0; t++;
        if (t % Hop == 0)
        {
            for (int i = 0; i < N; i++) { Re[i] = ring[(w + i) % N] * Win[i]; Im[i] = 0; }
            Fft.Transform(Re, Im, false); if (OnFrame != null) OnFrame(Re, Im); Fft.Transform(Re, Im, true);
            for (int i = 0; i < N; i++) outR[(w + i) % N] += Re[i] * Win[i] * scale;
        }
        float o = (float)outR[w]; outR[w] = 0; return o;
    }
}

// ----------------------------------------------------------------------
class PitchShiftFx : Fx
{
    static readonly string[] Quals = { "Fast (1024)", "Normal (2048)", "Smooth (4096)" };
    static readonly string[] MShapes = { "Off", "Sine", "Triangle", "Saw up", "Saw down", "Random steps" };
    static readonly int[] MMap = { -1, 0, 1, 2, 3, 5 };
    const int V = 4, OVER = 4;
    int N, H, Hop; double[] win; double scale; bool inited;
    class Ch
    {
        public float[] inRing; public int w; public double[] lastPh, mag, freq, flat, env, tre, tim;
        public double[][] sumPh = new double[V][], outR = new double[V][]; public float[][] vdl = new float[V][]; public int vw;
        public double[] sre, sim, smag, sfreq;
    }
    Ch[] ch = new Ch[2]; long count; Lfo2 lfo = new Lfo2(); double modOff; double[] ratio = new double[V], fratio = new double[V];
    public PitchShiftFx()
    {
        Type = "pshift"; Title = "Pitch Shifter"; Category = "Pitch & Voice";
        AddC("quality", "Quality", Quals, 1);
        for (int v = 1; v <= V; v++)
        {
            string nm = v == 1 ? "Pitch" : "Voice " + v + " pitch";
            AddP("p" + v, nm, -24, 24, 0, "st"); AddP("c" + v, v == 1 ? "Fine tune" : "Voice " + v + " fine", -100, 100, 0, "cents");
            AddP("l" + v, v == 1 ? "Level" : "Voice " + v + " level", 0, 150, v == 1 ? 100 : 0, "%"); AddP("pan" + v, v == 1 ? "Pan" : "Voice " + v + " pan", -100, 100, 0, "");
            AddP("d" + v, v == 1 ? "Delay" : "Voice " + v + " delay", 0, 100, 0, "ms");
        }
        AddP("fshift", "Formant shift", -12, 12, 0, "st"); AddP("keep", "Keep original formants", 0, 100, 0, "%");
        AddC("mshape", "Pitch movement", MShapes, 0); AddP("mrate", "Movement rate", 0.05f, 20, 0.5f, "Hz"); AddP("mdepth", "Movement depth", 0, 24, 2, "st");
        AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override int Latency { get { return N > 0 ? N - 1 : 2047; } }
    void Init()
    {
        int q = (int)Get("quality"); int n = q == 0 ? 1024 : (q == 2 ? 4096 : 2048); if (inited && n == N) return;
        N = n; H = N / 2; Hop = N / OVER; win = new double[N]; for (int i = 0; i < N; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N);
        double s = 0; for (int k = 0; k < OVER; k++) { double v = win[(k * Hop) % N]; s += v * v; } scale = 1.0 / s;
        for (int c = 0; c < 2; c++)
        {
            var x = new Ch(); x.inRing = new float[N]; x.lastPh = new double[H + 1]; x.mag = new double[H + 1]; x.freq = new double[H + 1]; x.flat = new double[H + 1]; x.env = new double[H + 1]; x.tre = new double[N]; x.tim = new double[N];
            x.sre = new double[N]; x.sim = new double[N]; x.smag = new double[H + 1]; x.sfreq = new double[H + 1];
            for (int v = 0; v < V; v++) { x.sumPh[v] = new double[H + 1]; x.outR[v] = new double[N]; x.vdl[v] = new float[8192]; }
            ch[c] = x;
        }
        count = 0; inited = true;
    }
    public override void Reset() { base.Reset(); inited = false; lfo.Reset(); modOff = 0; }
    protected override void Update() { Init(); }

    double Mod() { int s = (int)Get("mshape"); if (s <= 0) return 0; double v = lfo.Tick(MMap[s], Get("mrate") * Hop / SR); return v * Get("mdepth"); }

    void Frame(Ch c)
    {
        double expct = 2 * Math.PI * Hop / N; var re = c.sre; var im = c.sim;
        for (int i = 0; i < N; i++) { re[i] = c.inRing[(c.w + i) % N] * win[i]; im[i] = 0; }
        Fft.Transform(re, im, false);
        for (int k = 0; k <= H; k++)
        {
            double m = Math.Sqrt(re[k] * re[k] + im[k] * im[k]), ph = Math.Atan2(im[k], re[k]);
            double d = ph - c.lastPh[k]; c.lastPh[k] = ph; d -= k * expct; d -= 2 * Math.PI * Math.Round(d / (2 * Math.PI));
            c.mag[k] = m; c.freq[k] = k + d * OVER / (2 * Math.PI);
        }
        bool needEnv = false; for (int v = 0; v < V; v++) if (Math.Abs(fratio[v] - ratio[v]) > 0.002 && Get("l" + (v + 1)) > 0) needEnv = true;
        if (needEnv) { Dsp2.Envelope(c.mag, N, Math.Max(10, (int)(SR / 520.0)), c.tre, c.tim, c.env, SR); for (int k = 0; k <= H; k++) c.flat[k] = c.mag[k] / Math.Max(1e-7, c.env[k]); }
        for (int v = 0; v < V; v++)
        {
            if (Get("l" + (v + 1)) <= 0) continue; double r = ratio[v]; bool env = Math.Abs(fratio[v] - r) > 0.002; if (Math.Abs(r - 1) < 1e-4 && !env) continue;
            var src = env ? c.flat : c.mag; var sm = c.smag; var sf = c.sfreq; Array.Clear(sm, 0, H + 1); Array.Clear(sf, 0, H + 1);
            for (int k = 0; k <= H; k++) { int idx = (int)(k * r + 0.5); if (idx <= H) { sm[idx] += src[k]; sf[idx] = c.freq[k] * r; } }
            if (env) for (int k = 0; k <= H; k++) sm[k] *= Dsp2.EnvAt(c.env, H, k / fratio[v]);
            { double ei = 0, eo = 0; for (int k = 0; k <= H; k++) { ei += c.mag[k] * c.mag[k]; eo += sm[k] * sm[k]; } if (eo > 1e-12 && ei > 1e-12) { double g = Math.Sqrt(ei / eo); g = Math.Max(0.7, Math.Min(2.2, g)); for (int k = 0; k <= H; k++) sm[k] *= g; } }
            var sp = c.sumPh[v];
            for (int k = 0; k <= H; k++)
            {
                double dev = (sf[k] - k) * 2 * Math.PI / OVER + k * expct; sp[k] += dev; sp[k] -= 2 * Math.PI * Math.Round(sp[k] / (2 * Math.PI));
                re[k] = sm[k] * Math.Cos(sp[k]); im[k] = sm[k] * Math.Sin(sp[k]);
            }
            for (int k = 1; k < H; k++) { re[N - k] = re[k]; im[N - k] = -im[k]; }
            im[0] = 0; im[H] = 0;
            Fft.Transform(re, im, true); var o = c.outR[v];
            for (int i = 0; i < N; i++) o[(c.w + i) % N] += re[i] * win[i] * scale;
        }
    }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (!inited) Init();
        double mix = Get("mix") / 100.0; float[] gl = new float[V], gr = new float[V], lv = new float[V]; int[] dly = new int[V];
        for (int v = 0; v < V; v++) { lv[v] = Get("l" + (v + 1)) / 100f; double pan = Get("pan" + (v + 1)) / 100.0; gl[v] = (float)Math.Min(1, 1 - pan); gr[v] = (float)Math.Min(1, 1 + pan); dly[v] = (int)(Get("d" + (v + 1)) * 0.001 * SR); }
        for (int i = 0; i < n; i++)
        {
            if (count % Hop == 0)
            {
                modOff = Mod(); double fs = Get("fshift"), keep = Get("keep") / 100.0;
                for (int v = 0; v < V; v++) { double st = Get("p" + (v + 1)) + Get("c" + (v + 1)) / 100.0 + modOff; ratio[v] = Math.Pow(2, st / 12.0); fratio[v] = Math.Pow(2, fs / 12.0) * Math.Pow(ratio[v], 1 - keep); }
            }
            float wl = 0, wr = 0, dl0 = 0, dr0 = 0;
            for (int c = 0; c < 2; c++)
            {
                var x = ch[c]; x.inRing[x.w] = c == 0 ? l[i] : r[i]; x.w++; if (x.w >= N) x.w = 0;
                if ((count + 1) % Hop == 0) Frame(x);
                float dry = x.inRing[x.w]; if (c == 0) dl0 = dry; else dr0 = dry;
                for (int v = 0; v < V; v++)
                {
                    if (lv[v] <= 0) continue; float s;
                    if (Math.Abs(ratio[v] - 1) < 1e-4 && Math.Abs(fratio[v] - ratio[v]) <= 0.002) s = dry;
                    else { s = (float)x.outR[v][x.w]; }
                    x.outR[v][x.w] = 0;
                    var d = x.vdl[v]; d[x.vw & 8191] = s; if (dly[v] > 0) s = d[(x.vw - dly[v]) & 8191];
                    s *= lv[v]; if (c == 0) wl += s * gl[v]; else wr += s * gr[v];
                }
            }
            ch[0].vw++; ch[1].vw++; count++;
            l[i] = Dsp2.Sanitize((float)(dl0 + (wl - dl0) * mix)); r[i] = Dsp2.Sanitize((float)(dr0 + (wr - dr0) * mix));
        }
    }
}

// ----------------------------------------------------------------------
//  Time-domain shifter: two read heads cross-fading through a delay line. Low delay, grainy, great for big or moving shifts.
// ----------------------------------------------------------------------
class ClassicShiftFx : Fx
{
    static readonly string[] MShapes = { "Off", "Sine", "Triangle", "Saw up", "Saw down", "Random steps" };
    static readonly int[] MMap = { -1, 0, 1, 2, 3, 5 };
    double[] ratio = new double[2]; float[] bl, br; int wp, len; double[] ph = new double[2]; Lfo2 lfo = new Lfo2(); double fbl, fbr; double mod; int sub;
    public ClassicShiftFx()
    {
        Type = "cshift"; Title = "Classic Pitch Shifter"; Category = "Pitch & Voice";
        AddP("p1", "Pitch", -24, 24, 0, "st"); AddP("l1", "Level", 0, 150, 100, "%"); AddP("p2", "Voice 2 pitch", -24, 24, 0, "st"); AddP("l2", "Voice 2 level", 0, 150, 0, "%");
        AddP("win", "Grain size", 4, 80, 25, "ms"); AddP("fb", "Feedback", 0, 85, 0, "%");
        AddC("mshape", "Pitch movement", MShapes, 0); AddP("mrate", "Movement rate", 0.05f, 20, 0.5f, "Hz"); AddP("mdepth", "Movement depth", 0, 24, 2, "st");
        AddP("dry", "Dry", 0, 100, 0, "%"); AddP("mix", "Wet", 0, 100, 100, "%");
    }
    public override int Latency { get { return (int)(Get("win") * 0.0005 * SR); } }
    protected override void Update() { int n = SR / 4 + 16; if (bl == null || bl.Length != n) { bl = new float[n]; br = new float[n]; len = n; wp = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } ph[0] = 0; ph[1] = 0.37; fbl = fbr = 0; lfo.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double W = Math.Max(16, Get("win") * 0.001 * SR); int shape = (int)Get("mshape"); double mrate = Get("mrate") / SR, mdepth = Get("mdepth");
        double p1 = Get("p1"), p2 = Get("p2"); float l1 = Get("l1") / 100f, l2 = Get("l2") / 100f; float fb = Get("fb") / 100f, dry = Get("dry") / 100f, wet = Get("mix") / 100f;
        for (int i = 0; i < n; i++)
        {
            if ((sub++ & 15) == 0 || ratio[0] == 0) { mod = shape > 0 ? lfo.Tick(MMap[shape], mrate * 16) * mdepth : 0; ratio[0] = Math.Pow(2, (p1 + mod) / 12); ratio[1] = Math.Pow(2, (p2 + mod) / 12); }
            bl[wp] = l[i] + (float)fbl * fb; br[wp] = r[i] + (float)fbr * fb;
            float ol = 0, or = 0;
            for (int v = 0; v < 2; v++)
            {
                float lev = v == 0 ? l1 : l2; if (lev <= 0) continue; double rt = ratio[v] > 0 ? ratio[v] : 1;
                if (Math.Abs(rt - 1) < 1e-4) { double dd = W * 0.5; ol += Dsp2.Hermite(bl, len, wp - dd) * lev; or += Dsp2.Hermite(br, len, wp - dd) * lev; }
                else
                {
                    double p = ph[v]; double w1 = 0.5 - 0.5 * Math.Cos(2 * Math.PI * p), w2 = 1 - w1; double p2h = p + 0.5; if (p2h >= 1) p2h -= 1;
                    double d1 = p * W + 2, d2 = p2h * W + 2;
                    ol += (Dsp2.Hermite(bl, len, wp - d1) * (float)w1 + Dsp2.Hermite(bl, len, wp - d2) * (float)w2) * lev;
                    or += (Dsp2.Hermite(br, len, wp - d1) * (float)w1 + Dsp2.Hermite(br, len, wp - d2) * (float)w2) * lev;
                    p += (1 - rt) / W; p -= Math.Floor(p); ph[v] = p;
                }
            }
            fbl = ol; fbr = or;
            double dc = W * 0.5; float dl = Dsp2.Hermite(bl, len, wp - dc), dr = Dsp2.Hermite(br, len, wp - dc);
            l[i] = Dsp2.Sanitize(dl * dry + ol * wet); r[i] = Dsp2.Sanitize(dr * dry + or * wet);
            if (++wp >= len) wp = 0;
        }
    }
}

// ----------------------------------------------------------------------
//  Formant shifter: moves the vocal "colour" (the spectral envelope) without moving the pitch.
// ----------------------------------------------------------------------
class FormantFx : Fx
{
    Stft[] st = new Stft[2]; int N = 2048; double[] mag, env, tre, tim; double ratio, mixv, gainDb; int Q; bool inited;
    public FormantFx()
    {
        Type = "formant"; Title = "Formant Shifter"; Category = "Pitch & Voice";
        AddP("shift", "Formant shift", -12, 12, 0, "st"); AddP("tilt", "Brightness", -12, 12, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override int Latency { get { return N - 1; } }
    void Init()
    {
        if (inited) return; mag = new double[N / 2 + 1]; env = new double[N / 2 + 1]; tre = new double[N]; tim = new double[N];
        for (int c = 0; c < 2; c++) { st[c] = new Stft(N, 4); st[c].OnFrame = Spec; }
        inited = true;
    }
    protected override void Update() { Init(); ratio = Math.Pow(2, Get("shift") / 12.0); mixv = Get("mix") / 100.0; gainDb = Get("tilt"); Q = Math.Max(10, (int)(SR / 520.0)); }
    public override void Reset() { base.Reset(); if (inited) { st[0].Reset(); st[1].Reset(); } }
    void Spec(double[] re, double[] im)
    {
        int H = N / 2; for (int k = 0; k <= H; k++) mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
        Dsp2.Envelope(mag, N, Q, tre, tim, env, SR);
        for (int k = 0; k <= H; k++)
        {
            double e0 = Math.Max(1e-7, env[k]), e1 = Dsp2.EnvAt(env, H, k / ratio); double g = e1 / e0; g = Math.Max(0.03, Math.Min(25, g));
            if (gainDb != 0) g *= Math.Pow(10, gainDb * (k / (double)H - 0.5) / 20.0);
            re[k] *= g; im[k] *= g; if (k > 0 && k < H) { re[N - k] = re[k] * 1; im[N - k] = -im[k]; }
        }
        re[H] *= 1; im[0] = 0; im[H] = 0;
    }
    float[] ringL, ringR; int rw;
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (!inited) Init(); int M = N - 1; if (ringL == null) { ringL = new float[M]; ringR = new float[M]; }
        float mx = (float)mixv;
        for (int i = 0; i < n; i++)
        {
            float dl0 = ringL[rw], dr0 = ringR[rw]; ringL[rw] = l[i]; ringR[rw] = r[i]; if (++rw >= M) rw = 0;
            float a = st[0].Tick(l[i]), b = st[1].Tick(r[i]);
            l[i] = Dsp2.Sanitize(dl0 + (a - dl0) * mx); r[i] = Dsp2.Sanitize(dr0 + (b - dr0) * mx);
        }
    }
}
