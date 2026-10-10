using System;
using System.Collections.Generic;
using System.Threading;

// ======================================================================
//  Echo / delay and reverb engines
// ======================================================================

// ----------------------------------------------------------------------
//  Echo: mono, stereo, ping-pong, multi-tap or reverse repeats with filters, saturation, modulation, ducking,
//  diffusion and an optional reverb on the repeats.
// ----------------------------------------------------------------------
class EchoFx : Fx
{
    static readonly string[] Modes = { "Mono", "Stereo", "Ping-pong", "Multi-tap", "Reverse" };
    float[] bl, br; int len, w; double dLs, dRs, envIn, duckG = 1, lfoPh, wowPh, wowV, wowT; Random rnd = new Random(9);
    Biquad hpf = new Biquad(), lpf = new Biquad(); float[][] ap = new float[4][]; int[] apw = new int[4]; int[] apLen = { 241, 383, 277, 421 };
    int revCnt; int[] gws = new int[2]; double fbl, fbr; float[] wetL = new float[0], wetR = new float[0]; ReverbFx rv; int revInit;
    public EchoFx()
    {
        Type = "echo"; Title = "Echo"; Category = "Echo & Reverb";
        AddC("mode", "Style", Modes, 2); AddC("sync", "Time", Dsp2.SyncNames, 4); AddP("time", "Free time", 1, 2000, 350, "ms"); AddP("offset", "Right side offset", 0, 100, 0, "%");
        AddP("fb", "Repeats (feedback)", 0, 98, 40, "%"); AddP("lc", "Low cut", 20, 2000, 150, "Hz"); AddP("hc", "High cut", 500, 20000, 6500, "Hz"); AddP("sat", "Saturation", 0, 100, 0, "%");
        AddP("mod", "Wobble depth", 0, 20, 0, "ms"); AddP("modr", "Wobble rate", 0.05f, 8, 0.7f, "Hz"); AddP("wow", "Tape drift", 0, 100, 0, "%"); AddP("diff", "Smear (diffusion)", 0, 100, 0, "%");
        AddP("duck", "Duck while singing", 0, 100, 0, "%"); AddP("duckr", "Duck recovery", 20, 1500, 250, "ms"); AddP("width", "Width", 0, 200, 100, "%");
        AddP("rev", "Reverb on repeats", 0, 100, 0, "%"); AddP("revsize", "Reverb size", 0, 100, 55, "%"); AddP("decay", "Tap fade (multi-tap)", 20, 100, 70, "%"); AddP("mix", "Echo level", 0, 100, 35, "%");
    }
    protected override void Update()
    {
        int n = SR * 4 + 64; if (bl == null || bl.Length != n) { bl = new float[n]; br = new float[n]; len = n; w = 0; for (int i = 0; i < 4; i++) ap[i] = new float[apLen[i] * 2]; }
        hpf.Set(Biquad.HighPass, SR, Get("lc"), 0.7071, 0); lpf.Set(Biquad.LowPass, SR, Math.Min(Get("hc"), SR * 0.45), 0.7071, 0);
        if (rv == null && Get("rev") > 0) { rv = new ReverbFx(); rv.Prepare(SR); rv.SetPreset("mode=2;mix=100;er=0;tail=100;decay=2.2;pre=0;lc=150;hc=9000;width=100"); }
        if (rv != null) { rv.Set("size", Get("revsize")); rv.Prepare(SR); }
    }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } hpf.Reset(); lpf.Reset(); for (int i = 0; i < 4; i++) if (ap[i] != null) Array.Clear(ap[i], 0, ap[i].Length); fbl = fbr = 0; revCnt = 0; envIn = 0; if (rv != null) rv.Reset(); }
    double Target(double offs)
    {
        int sy = (int)Get("sync"); double smp = sy > 0 ? Dsp2.BeatsToSec(Dsp2.SyncBeats[sy]) * SR : Get("time") * 0.001 * SR; smp *= 1 + offs; return Math.Max(24, Math.Min(len - 40, smp));
    }
    float Allpass(int i, float x, float g) { var b = ap[i]; int L = apLen[i]; int rd = apw[i] - L; if (rd < 0) rd += b.Length; float d = b[rd]; float y = -g * x + d; b[apw[i]] = x + g * y; if (++apw[i] >= b.Length) apw[i] = 0; return y; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double fb = Get("fb") / 100.0, sat = Get("sat") / 100.0, satD = 1 + sat * 3.5; double mix = Get("mix") / 100.0, duck = Get("duck") / 100.0, width = Get("width") / 100.0;
        double modD = Get("mod") * 0.001 * SR, modR = Get("modr") / SR, wow = Get("wow") / 100.0 * 0.0016 * SR; double diff = Get("diff") / 100.0 * 0.6; double revA = Get("rev") / 100.0; double decay = Get("decay") / 100.0;
        double tgtL = Target(0), tgtR = Target(mode == 1 ? Get("offset") / 100.0 * 0.5 : 0); if (dLs == 0) { dLs = tgtL; dRs = tgtR; }
        double duckA = DspMath.Tc(5, SR), duckR = DspMath.Tc(Get("duckr"), SR); bool useDiff = diff > 0.01; bool filt = true;
        if (wetL.Length < n) { wetL = new float[n]; wetR = new float[n]; }
        for (int i = 0; i < n; i++)
        {
            dLs += (tgtL - dLs) * 0.0004; dRs += (tgtR - dRs) * 0.0004;
            lfoPh += modR; if (lfoPh >= 1) lfoPh -= 1; double mL = modD * Math.Sin(lfoPh * 2 * Math.PI), mR = modD * Math.Sin((lfoPh + 0.25) * 2 * Math.PI);
            if (wow > 0) { wowT -= 1; if (wowT <= 0) { wowT = SR * 0.12; wowV = rnd.NextDouble() * 2 - 1; } wowPh += (wowV - wowPh) * 0.0003; mL += wow * wowPh; mR += wow * wowPh * 0.8; }
            double dl = Math.Max(8, dLs + mL), dr = Math.Max(8, dRs + mR); float inL = l[i], inR = r[i], mono = (inL + inR) * 0.5f;
            float tl = Dsp2.Hermite(bl, len, w - dl), tr = Dsp2.Hermite(br, len, w - dr);
            float oL, oR;
            if (mode == 3)   // four taps, alternating sides, fading
            {
                float t1 = tl, t2 = Dsp2.Hermite(bl, len, w - Math.Min(len - 8, dl * 2)), t3 = Dsp2.Hermite(bl, len, w - Math.Min(len - 8, dl * 3)), t4 = Dsp2.Hermite(bl, len, w - Math.Min(len - 8, dl * 4));
                float g2 = (float)decay, g3 = g2 * g2, g4 = g3 * g2; oL = t1 + t3 * g3 * 0.9f; oR = t2 * g2 + t4 * g4; tl = t4; tr = t4;
            }
            else if (mode == 4)   // reverse: two overlapping grains playing the last stretch backwards
            {
                int rl = (int)Math.Max(512, Math.Min(len - 64, dLs)); revCnt %= rl; int u0 = revCnt, u1 = (revCnt + rl / 2) % rl; revCnt++;
                if (u0 == 0) gws[0] = w; if (u1 == 0) gws[1] = w; double w0 = Math.Sin(Math.PI * u0 / rl), w1 = Math.Sin(Math.PI * u1 / rl); w0 *= w0; w1 *= w1;
                int p0 = ((gws[0] - 1 - u0) % len + len) % len, p1 = ((gws[1] - 1 - u1) % len + len) % len; oL = (float)(bl[p0] * w0 + bl[p1] * w1); oR = (float)(br[p0] * w0 + br[p1] * w1); tl = oL; tr = oR;
            }
            else { oL = tl; oR = tr; }
            // what goes round the loop: filtered, saturated, smeared
            float fL = oL, fR = oR;
            if (filt) { fL = lpf.TickL(hpf.TickL(fL)); fR = lpf.TickR(hpf.TickR(fR)); }
            if (sat > 0) { fL = (float)(Math.Tanh(fL * satD) / satD * (1 + sat * 0.6)); fR = (float)(Math.Tanh(fR * satD) / satD * (1 + sat * 0.6)); }
            if (useDiff) { fL = Allpass(0, Allpass(1, fL, (float)diff), (float)diff); fR = Allpass(2, Allpass(3, fR, (float)diff), (float)diff); }
            float wl = fL, wr = fR; if (mode == 3) { wl = fL; wr = fR; }
            // write
            switch (mode)
            {
                case 0: bl[w] = mono + (float)(fbl * fb); br[w] = mono + (float)(fbr * fb); break;
                case 1: bl[w] = inL + (float)(fbl * fb); br[w] = inR + (float)(fbr * fb); break;
                case 2: bl[w] = mono + (float)(fbr * fb); br[w] = (float)(fbl * fb); break;
                default: bl[w] = inL + (float)(fbl * fb); br[w] = inR + (float)(fbr * fb); break;
            }
            if (mode == 3) { fbl = fL; fbr = fR; } else { fbl = fL; fbr = fR; }
            if (mode == 4) { fbl = fL * 0.5; fbr = fR * 0.5; }
            // ducking
            double ax = Math.Abs(mono); envIn = ax > envIn ? ax + (envIn - ax) * duckA : ax + (envIn - ax) * duckR; double tg = 1 - duck * Math.Min(1.0, envIn * 3.5); duckG += (tg - duckG) * 0.01;
            wetL[i] = (float)(wl * duckG); wetR[i] = (float)(wr * duckG); if (++w >= len) w = 0;
        }
        if (revA > 0.001 && rv != null) { var tl2 = new float[n]; var tr2 = new float[n]; Array.Copy(wetL, tl2, n); Array.Copy(wetR, tr2, n); rv.BeginBlock(null, 0, n, true); rv.Process(tl2, tr2, n); for (int i = 0; i < n; i++) { wetL[i] += tl2[i] * (float)revA; wetR[i] += tr2[i] * (float)revA; } }
        for (int i = 0; i < n; i++)
        {
            float a = wetL[i], b = wetR[i]; if (Math.Abs(width - 1) > 0.01) { float m = (a + b) * 0.5f, s = (a - b) * 0.5f * (float)width; a = m + s; b = m - s; }
            l[i] = Dsp2.Sanitize(l[i] + a * (float)mix); r[i] = Dsp2.Sanitize(r[i] + b * (float)mix);
        }
    }
}

// ----------------------------------------------------------------------
//  Reverb: 8-line feedback delay network (room, hall, plate, chamber, spring, cathedral, ambient)
//  with early reflections, pre-delay, modulation, gating, reverse, ducking, freeze and shimmer.
// ----------------------------------------------------------------------
class ReverbFx : Fx
{
    public static readonly string[] Modes = { "Room", "Hall", "Plate", "Chamber", "Spring", "Cathedral", "Ambient" };
    static readonly double[] ModeScale = { 0.55, 1.3, 0.85, 1.0, 0.3, 2.3, 1.7 }, ModeDiff = { 0.55, 0.7, 0.78, 0.6, 0.45, 0.72, 0.8 };
    const int NL = 8; static readonly int[] BaseLen = { 1117, 1327, 1559, 1747, 1973, 2179, 2417, 2683 };
    float[][] ln = new float[NL][]; int[] wp = new int[NL]; double[] lpS = new double[NL], lphase = new double[NL]; int[] len = new int[NL]; double[] gain = new double[NL]; double[] y = new double[NL];
    float[][] apb = new float[4][]; int[] apw = new int[4]; static readonly int[] ApLen = { 113, 337, 191, 541 };
    float[] pre; int prew; double[] erTL = { 5.3, 11.7, 17.9, 24.1, 31.3, 39.7, 46.9, 57.3 }, erTR = { 7.1, 13.3, 19.7, 27.9, 35.1, 41.3, 52.7, 61.9 }, erG = { 0.8, 0.65, 0.55, 0.5, 0.4, 0.35, 0.28, 0.22 }; float[] erB; int erW;
    Biquad lc = new Biquad(), hc = new Biquad(); double envI, gateG, hold, duckG = 1, envD; float[] rbL, rbR; int rbw; int revCnt; int[] gws = new int[2]; ClassicShiftFx sh; float[] shBuf = new float[0], shBufR = new float[0], tmpL = new float[0], tmpR = new float[0];
    // spring tank
    float[][] sp = new float[2][]; int[] spw = new int[2]; double[][] spAp = new double[2][]; double[] spLp = new double[2]; float[][] spApIn = new float[2][];
    int lenSR; bool built;
    public ReverbFx()
    {
        Type = "reverb2"; Title = "Reverb"; Category = "Echo & Reverb";
        AddC("mode", "Space", Modes, 1); AddP("size", "Size", 0, 100, 55, "%"); AddP("decay", "Decay time", 0.1f, 30, 2.4f, "s"); AddP("damp", "Damping (high cut in tail)", 1000, 20000, 7500, "Hz");
        AddP("pre", "Pre-delay", 0, 250, 20, "ms"); AddP("diff", "Diffusion", 0, 100, 70, "%"); AddP("mod", "Movement", 0, 100, 25, "%"); AddP("er", "Early reflections", 0, 100, 35, "%"); AddP("tail", "Tail", 0, 100, 100, "%");
        AddP("lc", "Low cut", 20, 1000, 120, "Hz"); AddP("hc", "High cut", 2000, 20000, 11000, "Hz"); AddP("width", "Width", 0, 200, 110, "%");
        AddC("gate", "Gate the tail", new[] { "Off", "On" }, 0); AddP("gthr", "Gate threshold", -60, -6, -30, "dB"); AddP("ghold", "Gate hold", 20, 800, 160, "ms"); AddP("grel", "Gate close time", 5, 400, 40, "ms");
        AddC("rev", "Reverse the tail", new[] { "Off", "On" }, 0); AddP("rtime", "Reverse length", 150, 2500, 900, "ms"); AddP("duck", "Duck while singing", 0, 100, 0, "%"); AddP("duckr", "Duck recovery", 50, 1500, 350, "ms");
        AddC("freeze", "Freeze", new[] { "Off", "On" }, 0); AddP("shim", "Shimmer", 0, 100, 0, "%"); AddP("shimst", "Shimmer pitch", -12, 24, 12, "st"); AddP("spring", "Spring drive", 0, 100, 60, "%"); AddP("mix", "Mix", 0, 100, 28, "%");
    }
    void Build()
    {
        built = true; lenSR = SR; double k = SR / 44100.0; int mx = (int)(BaseLen[NL - 1] * 5.5 * k) + 64;
        for (int i = 0; i < NL; i++) { ln[i] = new float[mx]; wp[i] = 0; lphase[i] = i * 0.37; }
        for (int i = 0; i < 4; i++) apb[i] = new float[(int)(ApLen[i] * k) * 2 + 8];
        pre = new float[(int)(SR * 0.3) + 8]; erB = new float[(int)(SR * 0.12) + 8]; rbL = new float[(int)(SR * 3.0)]; rbR = new float[(int)(SR * 3.0)];
        for (int c = 0; c < 2; c++) { sp[c] = new float[(int)(SR * 0.06) + 8]; spApIn[c] = new float[(int)(SR * 0.002) + 4]; spAp[c] = new double[32]; }
    }
    protected override void Update()
    {
        if (!built || lenSR != SR) Build(); int mode = (int)Get("mode"); double k = SR / 44100.0; double scale = ModeScale[mode] * (0.35 + Get("size") / 100.0 * 1.9); double rt = Math.Max(0.05, Get("decay")); bool fr = Get("freeze") > 0.5f;
        for (int i = 0; i < NL; i++) { len[i] = Math.Max(32, Math.Min(ln[i].Length - 16, (int)(BaseLen[i] * k * scale))); gain[i] = fr ? 1.0 : Math.Pow(10, -3.0 * len[i] / (SR * rt)); }
        lc.Set(Biquad.HighPass, SR, Get("lc"), 0.7071, 0); hc.Set(Biquad.LowPass, SR, Math.Min(Get("hc"), SR * 0.45), 0.7071, 0);
        if (Get("shim") > 0.5f && sh == null) { sh = new ClassicShiftFx(); sh.SetPreset("win=26;p1=12;l1=100;dry=0;mix=100"); sh.Prepare(SR); }
        if (sh != null) { sh.Set("p1", Get("shimst")); sh.Prepare(SR); }
    }
    public override void Reset()
    {
        base.Reset(); if (!built) return; for (int i = 0; i < NL; i++) { Array.Clear(ln[i], 0, ln[i].Length); lpS[i] = 0; } for (int i = 0; i < 4; i++) Array.Clear(apb[i], 0, apb[i].Length);
        Array.Clear(pre, 0, pre.Length); Array.Clear(erB, 0, erB.Length); Array.Clear(rbL, 0, rbL.Length); Array.Clear(rbR, 0, rbR.Length); for (int c = 0; c < 2; c++) { Array.Clear(sp[c], 0, sp[c].Length); Array.Clear(spAp[c], 0, 32); spLp[c] = 0; }
        lc.Reset(); hc.Reset(); envI = 0; gateG = 0; hold = 0; revCnt = 0; if (sh != null) sh.Reset(); Array.Clear(shBuf, 0, shBuf.Length); Array.Clear(shBufR, 0, shBufR.Length);
    }
    static void Wht8(double[] a)
    {
        for (int h = 1; h < 8; h <<= 1) for (int i = 0; i < 8; i += h << 1) for (int j = i; j < i + h; j++) { double x = a[j], z = a[j + h]; a[j] = x + z; a[j + h] = x - z; }
    }
    float Ap(int i, float x, float g) { var b = apb[i]; int L = (int)(ApLen[i] * SR / 44100.0); int rd = apw[i] - L; if (rd < 0) rd += b.Length; float d = b[rd]; float yy = -g * x + d; b[apw[i]] = x + g * yy; if (++apw[i] >= b.Length) apw[i] = 0; return yy; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (!built) Build(); int mode = (int)Get("mode"); double k = SR / 44100.0; double scale = ModeScale[mode] * (0.35 + Get("size") / 100.0 * 1.9);
        double dampA = 1 - Math.Exp(-2 * Math.PI * Get("damp") / SR); float diffG = (float)(Get("diff") / 100.0 * ModeDiff[mode] * 0.95); double modD = Get("mod") / 100.0 * 7 * k; double erL = Get("er") / 100.0, tailL = Get("tail") / 100.0; int preS = (int)(Get("pre") * 0.001 * SR);
        double mix = Get("mix") / 100.0, width = Get("width") / 100.0; bool gate = Get("gate") > 0.5f, rev = Get("rev") > 0.5f, frz = Get("freeze") > 0.5f; double duck = Get("duck") / 100.0; double shim = Get("shim") / 100.0;
        double gthr = Math.Pow(10, Get("gthr") / 20.0), gholdS = Get("ghold") * 0.001 * SR, grelA = DspMath.Tc(Get("grel"), SR), atkA = DspMath.Tc(3, SR); double duckA = DspMath.Tc(5, SR), duckR = DspMath.Tc(Get("duckr"), SR);
        int rtS = (int)Math.Max(2000, Math.Min(rbL.Length - 16, Get("rtime") * 0.001 * SR)); double spDrive = Get("spring") / 100.0; if (shBuf.Length < n) { shBuf = new float[n]; shBufR = new float[n]; tmpL = new float[n]; tmpR = new float[n]; }
        double[] outL = new double[1], outR = new double[1];
        for (int i = 0; i < n; i++)
        {
            float inL = l[i], inR = r[i], mono = (inL + inR) * 0.5f; double wetL = 0, wetR = 0;
            // pre-delay
            pre[prew] = mono; int pr = prew - preS; if (pr < 0) pr += pre.Length; float x = preS > 0 ? pre[pr] : mono; if (++prew >= pre.Length) prew = 0;
            // early reflections
            erB[erW] = mono; double eL = 0, eR = 0; if (erL > 0.001)
                for (int t = 0; t < 8; t++)
                {
                    double es = Math.Sqrt(Math.Max(0.3, scale)); int a = (int)(erTL[t] * es * 0.001 * SR), b = (int)(erTR[t] * es * 0.001 * SR); a = Math.Min(a, erB.Length - 2); b = Math.Min(b, erB.Length - 2); int ra = erW - a; if (ra < 0) ra += erB.Length; int rb = erW - b; if (rb < 0) rb += erB.Length; eL += erB[ra] * erG[t]; eR += erB[rb] * erG[t];
                }
            if (++erW >= erB.Length) erW = 0; eL *= erL * 0.5; eR *= erL * 0.5;
            if (tailL > 0.001 && mode != 4)
            {
                float xi = x; if (!frz) { xi = Ap(0, xi, diffG); xi = Ap(1, xi, diffG); xi = Ap(2, xi, diffG); xi = Ap(3, xi, diffG); } else xi = 0;
                if (shim > 0 && sh != null) xi += shBuf[i] * (float)(shim * 0.9);
                for (int j = 0; j < NL; j++)
                {
                    lphase[j] += (0.23 + j * 0.071) / SR; if (lphase[j] >= 1) lphase[j] -= 1; double rp = wp[j] - len[j] + modD * Math.Sin(lphase[j] * 2 * Math.PI) * (1 + j * 0.05);
                    double v = Dsp2.Hermite(ln[j], ln[j].Length, rp); lpS[j] += dampA * (v - lpS[j]); y[j] = lpS[j] * gain[j];
                    wetL += y[j] * ((j & 1) == 0 ? 1 : -1) * (j < 4 ? 1 : -1 * 0 + 1) * 0.5; wetR += y[j] * ((j & 2) == 0 ? 1 : -1) * 0.5;
                }
                double[] t8 = (double[])y.Clone(); Wht8(t8); double hn = 0.35355339;
                for (int j = 0; j < NL; j++) { double z = t8[j] * hn + xi * (j % 2 == 0 ? 0.5 : 0.45) * (frz ? 0 : 1); if (z > 8) z = 8; if (z < -8) z = -8; ln[j][wp[j]] = (float)z; if (++wp[j] >= ln[j].Length) wp[j] = 0; }
                wetL *= 0.6; wetR *= 0.6;
            }
            else if (mode == 4)   // spring: dispersive loops
            {
                for (int c = 0; c < 2; c++)
                {
                    var b = sp[c]; int D = (int)((c == 0 ? 0.031 : 0.043) * SR * (0.5 + Get("size") / 100.0)); D = Math.Max(16, Math.Min(b.Length - 4, D)); int rd = spw[c] - D; if (rd < 0) rd += b.Length; double v = b[rd];
                    double a = 0.62 + 0.1 * spDrive; double s = v; for (int q = 0; q < 24; q++) { double yy = -a * s + spAp[c][q]; spAp[c][q] = s + a * yy; s = yy; }   // chirp: the spring's dispersion
                    spLp[c] += dampA * (s - spLp[c]); double fbv = spLp[c] * Math.Pow(10, -3.0 * D / (SR * Math.Max(0.1, Get("decay")))); double inp = x * (0.6 + spDrive); double wv = Math.Tanh(inp + fbv * 0.98); b[spw[c]] = (float)wv; if (++spw[c] >= b.Length) spw[c] = 0;
                    if (c == 0) wetL = wv * 0.7 + spLp[c] * 0.3; else wetR = wv * 0.7 + spLp[c] * 0.3;
                }
            }
            wetL = wetL * tailL + eL; wetR = wetR * tailL + eR;
            // gate (reverb of a snare cut off short)
            double ax = Math.Abs(mono); envI = ax > envI ? ax + (envI - ax) * atkA : ax + (envI - ax) * DspMath.Tc(60, SR);
            if (gate) { if (envI > gthr) { hold = gholdS; gateG += (1 - gateG) * 0.05; } else if (hold > 0) { hold -= 1; gateG += (1 - gateG) * 0.05; } else gateG *= grelA; wetL *= gateG; wetR *= gateG; }
            // reverse tail
            if (rev)
            {
                rbL[rbw] = (float)wetL; rbR[rbw] = (float)wetR; revCnt %= rtS; int u0 = revCnt, u1 = (revCnt + rtS / 2) % rtS; revCnt++; if (u0 == 0) gws[0] = rbw; if (u1 == 0) gws[1] = rbw;
                double w0 = Math.Sin(Math.PI * u0 / rtS), w1 = Math.Sin(Math.PI * u1 / rtS); w0 *= w0; w1 *= w1; int p0 = ((gws[0] - u0) % rbL.Length + rbL.Length) % rbL.Length, p1 = ((gws[1] - u1) % rbL.Length + rbL.Length) % rbL.Length;
                wetL = rbL[p0] * w0 + rbL[p1] * w1; wetR = rbR[p0] * w0 + rbR[p1] * w1; if (++rbw >= rbL.Length) rbw = 0;
            }
            // duck
            envD = ax > envD ? ax + (envD - ax) * duckA : ax + (envD - ax) * duckR; double tg = 1 - duck * Math.Min(1.0, envD * 3.5); duckG += (tg - duckG) * 0.01; wetL *= duckG; wetR *= duckG;
            tmpL[i] = (float)wetL; tmpR[i] = (float)wetR;
        }
        // filters, width, shimmer feedback for the next block
        for (int i = 0; i < n; i++)
        {
            float a = hc.TickL(lc.TickL(tmpL[i])), b = hc.TickR(lc.TickR(tmpR[i])); tmpL[i] = a; tmpR[i] = b;
            if (Math.Abs(width - 1) > 0.01) { float m = (a + b) * 0.5f, s = (a - b) * 0.5f * (float)width; a = m + s; b = m - s; }
            l[i] = Dsp2.Sanitize(l[i] + a * (float)mix); r[i] = Dsp2.Sanitize(r[i] + b * (float)mix);
        }
        if (shim > 0 && sh != null) { var cl = new float[n]; var cr = new float[n]; Array.Copy(tmpL, cl, n); Array.Copy(tmpR, cr, n); sh.BeginBlock(null, 0, n, true); sh.Process(cl, cr, n); for (int i = 0; i < n; i++) shBuf[i] = (cl[i] + cr[i]) * 0.5f; }
    }
}

// ----------------------------------------------------------------------
//  Convolution reverb: partitioned FFT convolution with built-in synthesised spaces or your own impulse-response file
// ----------------------------------------------------------------------
static class IrLib
{
    public static readonly string[] Names = { "Small room", "Live room", "Large hall", "Cathedral", "Plate", "Spring", "Tiled bathroom", "Metal tank", "Telephone booth", "Guitar cabinet", "Small speaker", "Cardboard tube", "Your own file..." };
    static readonly double[][] Spec = {
        //  rt60, pre ms, bright(0..1), ER strength, density, length s
        new[]{0.35,2,0.7,1.0,0.8,0.9}, new[]{0.7,5,0.65,0.8,0.85,1.5}, new[]{2.4,18,0.55,0.5,0.9,3.2}, new[]{4.6,35,0.4,0.4,0.95,4.0}, new[]{1.7,0,0.9,0.0,1.0,2.6},
        new[]{1.5,0,0.6,0.0,0.7,2.2}, new[]{1.1,3,0.95,1.2,0.8,1.8}, new[]{2.0,0,0.5,0.2,0.4,2.6}, new[]{0.12,0,0.3,1.0,0.5,0.4}, new[]{0,0,0,0,0,0.04}, new[]{0,0,0,0,0,0.03}, new[]{0,0,0,0,0,0.12} };
    public static void Make(int idx, int sr, out float[] L, out float[] R)
    {
        var s = Spec[idx]; double rt = s[0]; int n = (int)(s[5] * sr); L = new float[n]; R = new float[n];
        if (idx >= 9) { Speaker(idx, sr, L, R); return; }
        var rnd = new Random(1000 + idx); int pre = (int)(s[1] * 0.001 * sr);
        for (int c = 0; c < 2; c++)
        {
            var h = c == 0 ? L : R; double lp = 0; double tau = rt / 6.9078; double dens = s[4];
            for (int i = pre; i < n; i++)
            {
                double t = (i - pre) / (double)sr; double env = Math.Exp(-t / tau); double noise = (rnd.NextDouble() * 2 - 1);
                // brightness fades as the sound decays (air and walls absorb highs)
                double cut = 0.15 + 0.85 * s[2] * Math.Exp(-t / (tau * 1.8)); lp += cut * (noise - lp); double v = lp * env * (0.4 + 0.6 * dens);
                if (idx == 5) { double pp = (t % 0.033) / 0.033; v *= 0.35 + 0.65 * Math.Exp(-pp * 6); v += 0.5 * Math.Sin(2 * Math.PI * (300 + 3500 * (t % 0.033) / 0.033) * t) * env * 0.25; }
                if (idx == 7) v += 0.4 * env * Math.Sin(2 * Math.PI * 187 * t) + 0.25 * env * Math.Sin(2 * Math.PI * 452 * t) + 0.2 * env * Math.Sin(2 * Math.PI * 1123 * t);
                h[i] += (float)v;
            }
            // early reflections: a few distinct bounces
            if (s[3] > 0) for (int k = 0; k < 14; k++) { int pos = pre + (int)((0.004 + rnd.NextDouble() * 0.06 * (1 + rt * 0.4)) * sr); if (pos < n) h[pos] += (float)((rnd.NextDouble() < 0.5 ? -1 : 1) * s[3] * (0.9 - k * 0.05) * 0.6); }
        }
        double e = 0; for (int i = 0; i < n; i++) e += L[i] * L[i] + R[i] * R[i]; double g = 1.0 / Math.Sqrt(Math.Max(1e-12, e / 2)); for (int i = 0; i < n; i++) { L[i] *= (float)g; R[i] *= (float)g; }
        int fade = Math.Min(n / 8, (int)(0.1 * sr)); for (int i = 0; i < fade; i++) { float f = (float)i / fade; L[n - 1 - i] *= f; R[n - 1 - i] *= f; }
    }
    static void Speaker(int idx, int sr, float[] L, float[] R)
    {
        int n = L.Length; var imp = new double[n]; imp[0] = 1; var f = new List<Biquad>();
        Func<int, double, double, double, Biquad> B = (t, fr, q, gdb) => { var b = new Biquad(); b.Set(t, sr, fr, q, gdb); return b; };
        if (idx == 9) { f.Add(B(Biquad.HighPass, 85, 0.8, 0)); f.Add(B(Biquad.HighPass, 85, 0.7, 0)); f.Add(B(Biquad.Bell, 120, 1.2, 4)); f.Add(B(Biquad.Bell, 2400, 1.4, 6)); f.Add(B(Biquad.Bell, 800, 1.0, -3)); f.Add(B(Biquad.LowPass, 5200, 0.8, 0)); f.Add(B(Biquad.LowPass, 5200, 0.7, 0)); }
        else if (idx == 10) { f.Add(B(Biquad.HighPass, 220, 0.9, 0)); f.Add(B(Biquad.Bell, 900, 1.5, 6)); f.Add(B(Biquad.Bell, 3000, 2.0, 4)); f.Add(B(Biquad.LowPass, 6500, 0.8, 0)); }
        else { f.Add(B(Biquad.BandPass, 700, 3.5, 0)); f.Add(B(Biquad.Bell, 1500, 6.0, 8)); f.Add(B(Biquad.HighPass, 250, 0.7, 0)); f.Add(B(Biquad.LowPass, 4500, 0.7, 0)); }
        double e = 0; for (int i = 0; i < n; i++) { double x = imp[i]; foreach (var b in f) x = b.TickL((float)x); double w = i < n * 0.8 ? 1 : 1 - (i - n * 0.8) / (n * 0.2); L[i] = (float)(x * w); R[i] = L[i]; e += L[i] * L[i]; }
        double g = 1.0 / Math.Sqrt(Math.Max(1e-12, e)); for (int i = 0; i < n; i++) { L[i] *= (float)g; R[i] *= (float)g; }
    }
    public static float[] Resample(float[] x, int from, int to) { if (from == to) return x; int n = (int)((long)x.Length * to / from); var y = new float[n]; double st = from / (double)to; for (int i = 0; i < n; i++) { double p = i * st; int a = (int)p; double f = p - a; float v0 = a < x.Length ? x[a] : 0, v1 = a + 1 < x.Length ? x[a + 1] : v0; y[i] = (float)(v0 + (v1 - v0) * f); } return y; }
}

class Convolver
{
    public const int P = 512; readonly int parts; readonly double[][] hRe, hIm, xRe, xIm; int xi; readonly double[] fr = new double[2 * P], fi = new double[2 * P], ar = new double[2 * P], ai = new double[2 * P]; readonly float[] tail = new float[P];
    public Convolver(float[] ir)
    {
        parts = Math.Max(1, (ir.Length + P - 1) / P); hRe = new double[parts][]; hIm = new double[parts][]; xRe = new double[parts][]; xIm = new double[parts][];
        for (int p = 0; p < parts; p++)
        {
            var re = new double[2 * P]; var im = new double[2 * P]; for (int i = 0; i < P; i++) { int k = p * P + i; re[i] = k < ir.Length ? ir[k] : 0; }
            Fft.Transform(re, im, false); hRe[p] = re; hIm[p] = im; xRe[p] = new double[2 * P]; xIm[p] = new double[2 * P];
        }
    }
    // one block of exactly P samples
    public void Process(float[] inB, float[] outB)
    {
        for (int i = 0; i < P; i++) { fr[i] = inB[i]; fi[i] = 0; fr[P + i] = 0; fi[P + i] = 0; }
        Fft.Transform(fr, fi, false); xi = (xi + 1) % parts; Array.Copy(fr, xRe[xi], 2 * P); Array.Copy(fi, xIm[xi], 2 * P);
        Array.Clear(ar, 0, 2 * P); Array.Clear(ai, 0, 2 * P);
        for (int p = 0; p < parts; p++)
        {
            int xx = (xi - p + parts * 2) % parts; var xr = xRe[xx]; var xim = xIm[xx]; var hr = hRe[p]; var him = hIm[p];
            for (int k = 0; k <= P; k++) { ar[k] += xr[k] * hr[k] - xim[k] * him[k]; ai[k] += xr[k] * him[k] + xim[k] * hr[k]; }
        }
        for (int k = 1; k < P; k++) { ar[2 * P - k] = ar[k]; ai[2 * P - k] = -ai[k]; }
        Fft.Transform(ar, ai, true);
        for (int i = 0; i < P; i++) { outB[i] = (float)ar[i] + tail[i]; tail[i] = (float)ar[P + i]; }
    }
}

class ConvFx : Fx
{
    class Rig { public Convolver cl, cr; }
    volatile Rig rig; volatile int version; int builtVersion = -1; string userPath = ""; string loadedSig = "";
    float[] inL = new float[Convolver.P], inR = new float[Convolver.P], outL = new float[Convolver.P], outR = new float[Convolver.P], oqL = new float[Convolver.P], oqR = new float[Convolver.P], dryL = new float[Convolver.P], dryR = new float[Convolver.P]; int fill; int rd; bool primed;
    Biquad lc = new Biquad(), hc = new Biquad(); public string IrNote = "";
    public ConvFx()
    {
        Type = "conv"; Title = "Convolution Reverb"; Category = "Echo & Reverb";
        AddC("ir", "Space", IrLib.Names, 2); AddP("len", "Length used", 10, 100, 100, "%"); AddP("pre", "Pre-delay", 0, 200, 0, "ms"); AddP("lc", "Low cut", 20, 1000, 80, "Hz"); AddP("hc", "High cut", 2000, 20000, 14000, "Hz");
        AddP("width", "Width", 0, 200, 100, "%"); AddP("dry", "Dry", 0, 100, 100, "%"); AddP("mix", "Wet", 0, 100, 30, "%");
    }
    public string UserPath { get { return userPath; } set { userPath = value ?? ""; Set("ir", IrLib.Names.Length - 1); MarkDirty(); } }
    public override int Latency { get { return Convolver.P; } }
    public override string Serialize() { string s = base.Serialize(); if (userPath.Length > 0) s += ";irpath=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userPath)); return s; }
    public override void Deserialize(string s)
    {
        if (s == null) return; var keep = new List<string>();
        foreach (var part in s.Split(';')) { if (part.StartsWith("irpath=", StringComparison.Ordinal)) { try { userPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(part.Substring(7))); } catch { } } else keep.Add(part); }
        base.Deserialize(string.Join(";", keep.ToArray()));
    }
    protected override void Update()
    {
        lc.Set(Biquad.HighPass, SR, Get("lc"), 0.7071, 0); hc.Set(Biquad.LowPass, SR, Math.Min(Get("hc"), SR * 0.45), 0.7071, 0);
        string sig = (int)Get("ir") + "|" + (int)Get("len") + "|" + (int)Get("pre") + "|" + SR + "|" + userPath; if (sig == loadedSig) return; loadedSig = sig; int v = ++version; int idx = (int)Get("ir"); double lenP = Get("len") / 100.0; double pre = Get("pre"); int sr = SR; string path = userPath;
        ThreadPool.QueueUserWorkItem(o =>
        {
            try
            {
                float[] L, R;
                if (idx == IrLib.Names.Length - 1)
                {
                    if (path.Length == 0 || !System.IO.File.Exists(path)) { IrNote = "Choose an impulse response file."; L = new float[Convolver.P]; R = L; L[0] = 1; }
                    else { float[] l2, r2; int fsr; string fmt; if (!SoundIO.Read(path, out l2, out r2, out fsr, out fmt)) { IrNote = "Could not read that file."; L = new float[Convolver.P]; R = L; L[0] = 1; } else { L = IrLib.Resample(l2, fsr, sr); R = IrLib.Resample(r2, fsr, sr); IrNote = System.IO.Path.GetFileName(path); if (L.Length > sr * 8) { Array.Resize(ref L, sr * 8); Array.Resize(ref R, sr * 8); } double e = 0; for (int i = 0; i < L.Length; i++) e += L[i] * L[i] + R[i] * R[i]; double g = 1.0 / Math.Sqrt(Math.Max(1e-12, e / 2)); for (int i = 0; i < L.Length; i++) { L[i] *= (float)g; R[i] *= (float)g; } } }
                }
                else { IrLib.Make(idx, sr, out L, out R); IrNote = IrLib.Names[idx]; if (sr != 44100 && false) { } }
                int n = Math.Max(Convolver.P, (int)(L.Length * lenP)); int p = (int)(pre * 0.001 * sr); var a = new float[n + p]; var b = new float[n + p];
                for (int i = 0; i < n && i < L.Length; i++) { float f = i > n * 0.85 ? (float)((n - i) / (n * 0.15)) : 1f; a[i + p] = L[i] * f; b[i + p] = R[i] * f; }
                if (v != version) return; rig = new Rig { cl = new Convolver(a), cr = new Convolver(b) };
            }
            catch (Exception ex) { IrNote = "Error: " + ex.Message; }
        });
    }
    public override void Reset() { base.Reset(); fill = 0; rd = 0; primed = false; Array.Clear(outL, 0, outL.Length); Array.Clear(outR, 0, outR.Length); Array.Clear(oqL, 0, oqL.Length); Array.Clear(oqR, 0, oqR.Length); Array.Clear(dryL, 0, dryL.Length); Array.Clear(dryR, 0, dryR.Length); lc.Reset(); hc.Reset(); loadedSig = ""; MarkDirty(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); var rg = rig; float wet = (float)(Get("mix") / 100.0), dry = (float)(Get("dry") / 100.0); double width = Get("width") / 100.0; int P = Convolver.P;
        for (int i = 0; i < n; i++)
        {
            // output side: the previous block's result (latency = one block)
            float wl = oqL[rd], wr = oqR[rd]; float dl = dryL[rd], dr = dryR[rd]; rd++;
            inL[fill] = l[i]; inR[fill] = r[i]; fill++;
            if (fill >= P)
            {
                Array.Copy(inL, dryL, P); Array.Copy(inR, dryR, P);
                if (rg != null) { rg.cl.Process(inL, oqL); rg.cr.Process(inR, oqR); } else { Array.Clear(oqL, 0, P); Array.Clear(oqR, 0, P); }
                fill = 0; rd = 0;
            }
            wl = hc.TickL(lc.TickL(wl)); wr = hc.TickR(lc.TickR(wr)); if (Math.Abs(width - 1) > 0.01) { float m = (wl + wr) * 0.5f, s = (wl - wr) * 0.5f * (float)width; wl = m + s; wr = m - s; }
            l[i] = Dsp2.Sanitize(dl * dry + wl * wet); r[i] = Dsp2.Sanitize(dr * dry + wr * wet);
        }
    }
}
