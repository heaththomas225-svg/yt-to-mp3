using System;
using System.Collections.Generic;

// ======================================================================
//  Vocal tools: rider, alignment, breath, plosives, vocal isolation, transients, declick, chopper, freeze
// ======================================================================

// keeps a vocal at an even level by moving the volume up and down, slowly and musically (like riding a fader)
class VocalRiderFx : Fx
{
    double ms, gdb, gl = 1; int sub; double want;
    public VocalRiderFx()
    {
        Type = "rider"; Title = "Vocal Rider"; Category = "Vocal Tools";
        AddP("target", "Target level", -40, -6, -20, "dB"); AddP("range", "Most it may move", 0, 24, 9, "dB"); AddP("react", "Reaction time", 20, 1200, 220, "ms");
        AddP("floor", "Ignore quieter than", -80, -20, -52, "dB"); AddP("window", "Listening window", 20, 400, 90, "ms"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); ms = 0; gdb = 0; gl = 1; want = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double target = Get("target"), range = Get("range"), floor = Get("floor"), mix = Get("mix") / 100.0;
        double aw = 1 - Math.Exp(-1.0 / (Get("window") * 0.001 * SR)), ar = Math.Exp(-16.0 / (Get("react") * 0.001 * SR)), glk = 1 - Math.Exp(-1.0 / (0.004 * SR));
        for (int i = 0; i < n; i++)
        {
            double m = (l[i] + r[i]) * 0.5; ms += (m * m - ms) * aw;
            if ((sub++ & 15) == 0)
            {
                double lv = 10 * Math.Log10(ms + 1e-12); double w = lv > floor ? Math.Max(-range, Math.Min(range, target - lv)) : 0;
                gdb = gdb * ar + w * (1 - ar);   // one pole toward the wanted gain, slower than the window so it never pumps
            }
            double tg = Math.Pow(10, gdb / 20.0); gl += (tg - gl) * glk; float g = (float)(1 + (gl - 1) * mix);
            l[i] *= g; r[i] *= g;
        }
    }
}

// lines up a double with the lead: delay, polarity, or find the delay by listening to the vocal track
class AlignFx : Fx
{
    float[] bl = new float[0], br = new float[0]; int w, len; double dCur; bool ready; float[] ringIn, ringSc; int rw; const int RN = 65536; int lag; int since;
    public AlignFx()
    {
        Type = "align"; Title = "Vocal Alignment"; Category = "Vocal Tools";
        AddP("delay", "Delay", 0, 60, 0, "ms"); AddC("pol", "Polarity", new[] { "Normal", "Inverted" }, 0); AddC("auto", "Find the delay automatically", new[] { "Off", "Match the vocal track" }, 0); AddP("search", "Search up to", 5, 50, 30, "ms");
        AddP("found", "Delay found", 0, 60, 0, "ms");
    }
    public override bool WantsSidechain { get { return Get("auto") > 0.5f; } }
    protected override void Update() { len = SR / 10 + 64; if (bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } if (ringIn == null) { ringIn = new float[RN]; ringSc = new float[RN]; } }
    public override void Reset() { base.Reset(); if (bl.Length > 0) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } if (ringIn != null) { Array.Clear(ringIn, 0, RN); Array.Clear(ringSc, 0, RN); } dCur = 0; ready = false; lag = 0; since = 0; }
    void Search()
    {
        int maxLag = (int)(Get("search") * 0.001 * SR) / 4; int W = 2048, dec = 4; double best = -1e30; int bl2 = 0;
        for (int L = 0; L <= maxLag; L++)
        {
            double s = 0; for (int j = 0; j < W; j++) { int a = (rw - 1 - j * dec - L * dec) & (RN - 1), b = (rw - 1 - j * dec) & (RN - 1); s += ringIn[a] * ringSc[b]; }
            if (s > best) { best = s; bl2 = L; }
        }
        lag = best > 1e-6 ? bl2 * dec : lag; Set("found", (float)(lag * 1000.0 / SR));
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool auto = Get("auto") > 0.5f && ScL != null; float pol = Get("pol") > 0.5f ? -1f : 1f; double tgt = auto ? lag : Get("delay") * 0.001 * SR; tgt = Math.Max(0, Math.Min(len - 4, tgt));
        for (int i = 0; i < n; i++)
        {
            if (auto) { ringIn[rw] = (l[i] + r[i]) * 0.5f; ringSc[rw] = (ScL[i] + (ScR != null ? ScR[i] : ScL[i])) * 0.5f; rw = (rw + 1) & (RN - 1); if (++since >= 8192) { since = 0; Search(); } }
            if (!ready) { dCur = tgt; ready = true; } dCur += (tgt - dCur) * 0.002; bl[w] = l[i]; br[w] = r[i]; double rp = w - dCur; l[i] = Dsp2.Hermite(bl, len, rp) * pol; r[i] = Dsp2.Hermite(br, len, rp) * pol; if (++w >= len) w = 0;
        }
    }
}

// turns breaths down (or away): finds quiet, airy sounds between phrases
class BreathFx : Fx
{
    Biquad hp = new Biquad(), lp = new Biquad(); double eh, el, ef, g = 1;
    public BreathFx()
    {
        Type = "breath"; Title = "Breath Control"; Category = "Vocal Tools";
        AddP("reduce", "Turn breaths down by", 0, 60, 12, "dB"); AddP("sens", "Sensitivity", 0, 100, 55, "%"); AddP("speech", "Anything louder is singing", -50, -12, -30, "dB"); AddP("floor", "Ignore quieter than", -90, -40, -70, "dB"); AddP("rel", "Recovery", 20, 400, 90, "ms");
    }
    protected override void Update() { hp.Set(Biquad.HighPass, SR, 4000, 0.7071, 0); lp.Set(Biquad.LowPass, SR, 1200, 0.7071, 0); }
    public override void Reset() { base.Reset(); hp.Reset(); lp.Reset(); eh = el = ef = 0; g = 1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double red = DspMath.DbToLin(-Get("reduce")), ratioThr = 1.6 - Get("sens") / 100.0 * 1.3; double speech = DspMath.DbToLin(Get("speech")), floor = DspMath.DbToLin(Get("floor"));
        double ae = 1 - Math.Exp(-1.0 / (0.006 * SR)), ga = 1 - Math.Exp(-1.0 / (0.012 * SR)), gr = 1 - Math.Exp(-1.0 / (Get("rel") * 0.001 * SR));
        for (int i = 0; i < n; i++)
        {
            float m = (l[i] + r[i]) * 0.5f; double h = Math.Abs(hp.TickL(m)), lo = Math.Abs(lp.TickL(m)), f = Math.Abs(m);
            eh += (h - eh) * ae; el += (lo - el) * ae; ef += (f - ef) * ae;
            bool breath = ef < speech && ef > floor && eh > el * (1.0 / ratioThr) * 0.5;
            double tg = breath ? red : 1.0; g += (tg - g) * (tg < g ? ga : gr); l[i] *= (float)g; r[i] *= (float)g;
        }
    }
}

// lifts the high-pass filter for an instant when a "p" or "b" pops the microphone
class PlosiveFx : Fx
{
    Biquad hp = new Biquad(); double fast, slow, x; int sub; double fc = 40, pfc = -1;
    public PlosiveFx()
    {
        Type = "plosive"; Title = "Plosive Reducer"; Category = "Vocal Tools";
        AddP("amount", "Amount", 0, 100, 80, "%"); AddP("sens", "Sensitivity", 0, 100, 60, "%"); AddP("maxf", "Highest it cuts", 100, 400, 220, "Hz");
    }
    Biquad lp = new Biquad(); protected override void Update() { lp.Set(Biquad.LowPass, SR, 160, 0.7071, 0); }
    public override void Reset() { base.Reset(); hp.Reset(); lp.Reset(); fast = slow = x = 0; fc = 40; pfc = -1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double amount = Get("amount") / 100.0, thr = 3.2 - Get("sens") / 100.0 * 2.4, maxf = Get("maxf");
        double af = 1 - Math.Exp(-1.0 / (0.004 * SR)), as_ = 1 - Math.Exp(-1.0 / (0.25 * SR)), rel = Math.Exp(-1.0 / (0.05 * SR));
        for (int i = 0; i < n; i++)
        {
            double m = lp.TickL((l[i] + r[i]) * 0.5f); double a = Math.Abs(m); fast += (a - fast) * af; slow += (a - slow) * as_;
            double ratio = fast / (slow + 1e-5); double pop = ratio > thr && fast > 0.01 ? Math.Min(1, (ratio - thr) / thr) : 0; x = Math.Max(pop, x * rel);
            if ((sub++ & 7) == 0) { fc = 30 + x * amount * (maxf - 30); if (Math.Abs(fc - pfc) > 0.01) { pfc = fc; hp.Set(Biquad.HighPass, SR, fc, 0.7071, 0); } }
            if (x > 0.01) { l[i] = hp.TickL(l[i]); r[i] = hp.TickR(r[i]); } else { hp.TickL(l[i]); hp.TickR(r[i]); }
        }
    }
}

// quick centre-channel vocal isolation or removal: keeps (or removes) what sits in the middle of the stereo picture
class VocalIsoFx : Fx
{
    const int N = 2048; Stft sS, sM; double[] magS, gPrev; float[] ringL, ringR; int rw; double strength, power, lo, hi, smooth; bool keep; bool inited; double wide;
    public VocalIsoFx()
    {
        Type = "vociso"; Title = "Vocal Isolation"; Category = "Vocal Tools";
        AddC("mode", "Mode", new[] { "Keep the centre (isolate vocals)", "Remove the centre (instrumental)" }, 0); AddP("strength", "Strength", 0, 100, 70, "%"); AddP("hard", "Hardness", 0, 100, 50, "%");
        AddP("lo", "Ignore below", 40, 400, 120, "Hz"); AddP("hi", "Ignore above", 4000, 20000, 11000, "Hz"); AddP("smooth", "Smoothing", 0, 90, 55, "%"); AddP("wide", "Keep some stereo (isolate mode)", 0, 100, 0, "%");
    }
    public override int Latency { get { return N - 1; } }
    void Init() { if (inited) return; inited = true; magS = new double[N / 2 + 1]; gPrev = new double[N / 2 + 1]; ringL = new float[N - 1]; ringR = new float[N - 1]; sS = new Stft(N, 4); sM = new Stft(N, 4); sS.OnFrame = (re, im) => { for (int k = 0; k <= N / 2; k++) magS[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); }; sM.OnFrame = MFrame; }
    protected override void Update() { Init(); strength = Get("strength") / 100.0 * 1.6; power = 0.5 + Get("hard") / 100.0 * 3; lo = Get("lo"); hi = Get("hi"); smooth = Get("smooth") / 100.0; keep = Get("mode") < 0.5f; wide = Get("wide") / 100.0; }
    public override void Reset() { base.Reset(); if (inited) { sS.Reset(); sM.Reset(); Array.Clear(gPrev, 0, gPrev.Length); Array.Clear(ringL, 0, ringL.Length); Array.Clear(ringR, 0, ringR.Length); } }
    void MFrame(double[] re, double[] im)
    {
        int H = N / 2; double bh = SR / (double)N;
        for (int k = 1; k < H; k++)
        {
            double mm = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); double c = 1 - Math.Min(1.0, strength * magS[k] / (mm + 1e-9)); c = Math.Pow(Math.Max(0, c), power);
            double hz = k * bh; double band = hz < lo ? Math.Max(0.05, hz / lo) : (hz > hi ? Math.Max(0.1, hi / hz) : 1.0); double g = c * band;
            g = gPrev[k] * smooth + g * (1 - smooth); gPrev[k] = g; double f = keep ? g : 1 - g; re[k] *= f; im[k] *= f; re[N - k] = re[k]; im[N - k] = -im[k];
        }
        im[0] = 0; im[H] = 0;
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); Init(); int M = N - 1;
        for (int i = 0; i < n; i++)
        {
            float m = (l[i] + r[i]) * 0.5f, s = (l[i] - r[i]) * 0.5f; float so = sS.Tick(s), mo = sM.Tick(m);
            if (keep) { float sw = (float)wide; l[i] = Dsp2.Sanitize(mo + so * sw); r[i] = Dsp2.Sanitize(mo - so * sw); } else { l[i] = Dsp2.Sanitize(mo + so); r[i] = Dsp2.Sanitize(mo - so); }
        }
    }
}

// boosts or softens the attack of sounds, and the body after it
class TransientFx : Fx
{
    double e1, e2, g = 1;
    public TransientFx()
    {
        Type = "transient"; Title = "Transient Shaper"; Category = "Vocal Tools";
        AddP("attack", "Attack", -15, 15, 4, "dB"); AddP("sustain", "Sustain", -15, 15, 0, "dB"); AddP("speed", "Speed", 20, 300, 100, "%"); AddP("out", "Output", -12, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); e1 = e2 = 0; g = 1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double att = Get("attack"), sus = Get("sustain"), sp = Get("speed") / 100.0, outG = DspMath.DbToLin(Get("out"));
        double f1 = 1 - Math.Exp(-1.0 / (0.001 * SR / sp)), fr1 = 1 - Math.Exp(-1.0 / (0.012 * SR / sp)), s1 = 1 - Math.Exp(-1.0 / (0.025 * SR / sp)), sr1 = 1 - Math.Exp(-1.0 / (0.09 * SR / sp)), gs = 1 - Math.Exp(-1.0 / (0.0015 * SR));
        for (int i = 0; i < n; i++)
        {
            double a = Math.Abs((l[i] + r[i]) * 0.5); e1 += (a - e1) * (a > e1 ? f1 : fr1); e2 += (a - e2) * (a > e2 ? s1 : sr1);
            double t = (e1 - e2) / (e2 + 1e-4); t = t < 0 ? 0 : (t > 1 ? 1 : t); double gdb = att * t + sus * (1 - t) * (e2 > 1e-4 ? 1 : 0); double tg = Math.Pow(10, gdb / 20.0) * outG; g += (tg - g) * gs; l[i] *= (float)g; r[i] *= (float)g;
        }
    }
}

// finds and repairs short clicks and pops by drawing a straight line across them
class DeclickFx : Fx
{
    const int D = 12; float[] bl = new float[64], br = new float[64]; bool[] fl = new bool[64]; int w; double rms, lpL, lpR;
    public DeclickFx() { Type = "declick"; Title = "Declicker"; Category = "Vocal Tools"; AddP("sens", "Sensitivity", 0, 100, 60, "%"); AddP("size", "Longest click repaired", 1, 8, 4, "samples"); }
    public override int Latency { get { return D; } }
    public override void Reset() { base.Reset(); Array.Clear(bl, 0, 64); Array.Clear(br, 0, 64); Array.Clear(fl, 0, 64); rms = 0; lpL = lpR = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double k = 14 - Get("sens") / 100.0 * 10.5; int maxLen = (int)Get("size");
        for (int i = 0; i < n; i++)
        {
            lpL += 0.3 * (l[i] - lpL); lpR += 0.3 * (r[i] - lpR); double res = Math.Max(Math.Abs(l[i] - lpL), Math.Abs(r[i] - lpR)); bool bad = res > k * rms + 0.004; rms += (Math.Min(res, k * rms + 0.004) - rms) * 0.002;
            bl[w & 63] = l[i]; br[w & 63] = r[i]; fl[w & 63] = bad; w++;
            // the sample D back is final: if it was flagged, bridge it
            int j = w - 1 - D; float oL = bl[j & 63], oR = br[j & 63];
            if (j >= 0 && fl[j & 63])
            {
                int a = j; while (a > j - maxLen && fl[(a - 1) & 63]) a--; int b = j; while (b < j + maxLen && b < w - 1 && fl[(b + 1) & 63]) b++;
                if (b - a + 1 <= maxLen && b + 1 < w) { float x0 = bl[(a - 1) & 63], x1 = bl[(b + 1) & 63], y0 = br[(a - 1) & 63], y1 = br[(b + 1) & 63]; double t = (j - a + 1.0) / (b - a + 2.0); oL = (float)(x0 + (x1 - x0) * t); oR = (float)(y0 + (y1 - y0) * t); }
            }
            l[i] = oL; r[i] = oR;
        }
    }
}

// slices the sound on the beat and rearranges it: mutes, stutters, reverses and pitches slices at random but repeatably
class ChopFx : SyncFx
{
    float[] bl, br; int len, w; int curIdx = int.MinValue; int action; int w0; double rate; int repN; double sliceLen; int tPos; float mg = 1f;
    public ChopFx()
    {
        Type = "chop"; Title = "Vocal Chopper"; Category = "Vocal Tools";
        AddC("sync", "Slice length", Dsp2.SyncNames, 7); AddP("pmute", "Mute slices", 0, 100, 20, "%"); AddP("prep", "Stutter slices", 0, 100, 25, "%"); AddP("prev", "Reverse slices", 0, 100, 10, "%"); AddP("ppit", "Pitch-jump slices", 0, 100, 10, "%");
        AddP("rep", "Stutter repeats", 2, 12, 4, ""); AddP("seed", "Pattern number", 0, 99, 1, ""); AddP("fade", "Edge fade", 0.2f, 10, 1.5f, "ms"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { len = SR * 4; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } curIdx = int.MinValue; mg = 1f; }
    static readonly double[] Jumps = { 12, -12, 7, -5, 5, -7, 3, -3 };
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int sy = (int)Get("sync"); double gridBeats = sy > 0 ? Dsp2.SyncBeats[sy] : 0.5; double pm = Get("pmute") / 100.0, pr = Get("prep") / 100.0, pv = Get("prev") / 100.0, pp = Get("ppit") / 100.0; uint seed = (uint)Get("seed"); int reps = (int)Get("rep"); double mix = Get("mix") / 100.0; int fade = (int)Math.Max(8, Get("fade") * 0.001 * SR);
        double slice = 60.0 / Math.Max(20, Fx.ProjectBpm) * gridBeats * SR;
        for (int i = 0; i < n; i++)
        {
            float inL = l[i], inR = r[i]; bl[w] = inL; br[w] = inR;
            if (!tr) { w = (w + 1) % len; continue; }
            double beat = BeatAt(i); int idx = (int)Math.Floor(beat / gridBeats); double ph = beat / gridBeats - idx; int t = (int)(ph * slice);
            if (idx != curIdx)
            {
                curIdx = idx; double u = U01(Hash((uint)idx, seed * 7919u + 13)); w0 = w; sliceLen = slice; double c = pm;
                if (u < c) action = 1; else if (u < (c += pr)) { action = 2; repN = Math.Max(2, Math.Min(12, reps)); } else if (u < (c += pv)) action = 3; else if (u < (c += pp)) { action = 4; rate = Math.Pow(2, Jumps[(int)(U01(Hash((uint)idx, 99 + seed)) * Jumps.Length) % Jumps.Length] / 12.0); } else action = 0;
            }
            float oL = inL, oR = inR; int S = (int)sliceLen; if (S < 64) S = 64;
            if (action != 0)
            {
                float gain = Math.Min(1f, Math.Min(t / (float)fade, (S - t) / (float)fade)); if (gain < 0) gain = 0;
                if (action == 1) { oL = inL; oR = inR; }
                else if (action == 2) { int R = Math.Max(64, S / repN); int p = (w0 + (t % R)) % len; oL = inL * (1 - gain) + bl[p] * gain; oR = inR * (1 - gain) + br[p] * gain; if (t < R) { oL = inL; oR = inR; } }
                else if (action == 3) { int p = ((w0 - 1 - t) % len + len) % len; oL = inL * (1 - gain) + bl[p] * gain; oR = inR * (1 - gain) + br[p] * gain; }
                else { double p = w0 - S + t * rate; oL = inL * (1 - gain) + Dsp2.Hermite(bl, len, p) * gain; oR = inR * (1 - gain) + Dsp2.Hermite(br, len, p) * gain; }
            }
            { float tg = action == 1 ? 0f : 1f, stp = 1f / fade; mg += Math.Max(-stp, Math.Min(stp, tg - mg)); oL *= mg; oR *= mg; }
            w = (w + 1) % len; l[i] = (float)(inL + (oL - inL) * mix); r[i] = (float)(inR + (oR - inR) * mix);
        }
        EndBlock(n);
    }
}

// holds a moment of sound: switch Freeze on and the last stretch loops until you switch it off
class FreezeFx : Fx
{
    float[] bl, br, fl, fr; int len, w, flen, ph; bool frozen, was;
    public FreezeFx()
    {
        Type = "freeze"; Title = "Vocal Freeze"; Category = "Vocal Tools";
        AddC("freeze", "Freeze", new[] { "Off", "On" }, 0); AddP("size", "Loop length", 40, 2000, 350, "ms"); AddP("mix", "Frozen sound level", 0, 100, 100, "%"); AddP("dry", "Original while frozen", 0, 100, 0, "%");
    }
    protected override void Update() { len = SR * 3; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; fl = new float[len]; fr = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } frozen = was = false; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool on = Get("freeze") > 0.5f; float mix = Get("mix") / 100f, dry = Get("dry") / 100f;
        if (on && !was) { flen = Math.Max(256, Math.Min(len - 8, (int)(Get("size") * 0.001 * SR))); for (int i = 0; i < flen; i++) { int p = ((w - flen + i) % len + len) % len; fl[i] = bl[p]; fr[i] = br[p]; } ph = 0; }
        was = on;
        for (int i = 0; i < n; i++)
        {
            bl[w] = l[i]; br[w] = r[i]; if (++w >= len) w = 0;
            if (!on) continue;
            // two overlapping readers, half a loop apart, each faded in and out
            int p1 = ph % flen, p2 = (ph + flen / 2) % flen; double w1 = Math.Sin(Math.PI * p1 / flen), w2 = Math.Sin(Math.PI * p2 / flen); w1 *= w1; w2 *= w2;
            float oL = (float)(fl[p1] * w1 + fl[p2] * w2), oR = (float)(fr[p1] * w1 + fr[p2] * w2); ph++; if (ph >= flen * 2) ph = 0;
            l[i] = l[i] * dry + oL * mix; r[i] = r[i] * dry + oR * mix;
        }
    }
}
