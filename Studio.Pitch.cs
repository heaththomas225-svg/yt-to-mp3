using System;
using System.Collections.Generic;

// ======================================================================
//  FFT helper, spectral noise reduction, and the pitch-correction plugin.
// ======================================================================

static class Fft
{
    static readonly Dictionary<int, double[]> cosT = new Dictionary<int, double[]>(), sinT = new Dictionary<int, double[]>();
    static readonly object lk = new object();

    static void Tables(int n, out double[] c, out double[] s)
    {
        lock (lk)
        {
            if (!cosT.TryGetValue(n, out c))
            {
                c = new double[n / 2]; s = new double[n / 2];
                for (int i = 0; i < n / 2; i++) { c[i] = Math.Cos(2 * Math.PI * i / n); s[i] = Math.Sin(2 * Math.PI * i / n); }
                cosT[n] = c; sinT[n] = s;
            }
            s = sinT[n];
        }
    }

    // In-place radix-2 FFT. inverse=true divides by n.
    public static void Transform(double[] re, double[] im, bool inverse)
    {
        int n = re.Length; double[] ct, st; Tables(n, out ct, out st);
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1, step = n / len;
            for (int i = 0; i < n; i += len)
                for (int k = 0; k < half; k++)
                {
                    double wr = ct[k * step], wi = inverse ? st[k * step] : -st[k * step];
                    int a = i + k, b = a + half;
                    double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi; re[a] += xr; im[a] += xi;
                }
        }
        if (inverse) for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
    }
}

// ======================================================================
//  Spectral noise reduction (min-statistics noise tracking + Wiener-style gain)
// ======================================================================
class NoiseReduction : Fx
{
    const int FN = 1024, HOP = 256, BINS = FN / 2 + 1;
    double[] win = new double[FN];
    double[] inL = new double[FN], inR = new double[FN]; int inPos;
    double[] ola = new double[FN * 4], olaR = new double[FN * 4]; long cnt;
    double[] reL = new double[FN], imL = new double[FN], reR = new double[FN], imR = new double[FN];
    double[] ps = new double[BINS], nz = new double[BINS], gs = new double[BINS], gt = new double[BINS];
    bool init; double floorG, sens, mix;
    public override int Latency { get { return FN; } }

    public NoiseReduction()
    {
        Type = "denoise"; Title = "Noise Reduction";
        AddP("amount", "Reduction", 0, 40, 12, "dB"); AddP("sens", "Sensitivity", 0.5f, 4, 1.8f, ""); AddP("mix", "Mix", 0, 100, 100, "%");
        for (int i = 0; i < FN; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / FN);
    }
    protected override void Update() { floorG = Math.Pow(10, -Get("amount") / 20.0); sens = Get("sens"); mix = Get("mix") / 100.0; }
    public override void Reset()
    {
        base.Reset(); Array.Clear(inL, 0, FN); Array.Clear(inR, 0, FN); Array.Clear(ola, 0, ola.Length); Array.Clear(olaR, 0, olaR.Length);
        Array.Clear(ps, 0, BINS); Array.Clear(nz, 0, BINS); init = false; cnt = 0; inPos = 0;
        for (int k = 0; k < BINS; k++) gs[k] = 1;
    }

    void Frame(long end)
    {
        for (int i = 0; i < FN; i++)
        {
            int idx = (inPos + i) % FN;   // oldest first
            reL[i] = inL[idx] * win[i]; imL[i] = 0; reR[i] = inR[idx] * win[i]; imR[i] = 0;
        }
        Fft.Transform(reL, imL, false); Fft.Transform(reR, imR, false);
        for (int k = 0; k < BINS; k++)
        {
            double p = reL[k] * reL[k] + imL[k] * imL[k] + reR[k] * reR[k] + imR[k] * imR[k];
            if (!init) { ps[k] = p; nz[k] = p; }
            ps[k] = 0.6 * ps[k] + 0.4 * p;
            if (ps[k] < nz[k]) nz[k] = ps[k]; else nz[k] *= 1.004;
            double noise = nz[k] * 2.2 * sens;
            double g = ps[k] > 1e-14 ? 1.0 - noise / ps[k] : 1.0;
            gt[k] = Math.Max(floorG, Math.Min(1.0, g));
        }
        init = true;
        for (int k = 0; k < BINS; k++)
        {
            double a = gt[Math.Max(0, k - 1)], b = gt[k], c = gt[Math.Min(BINS - 1, k + 1)], g = 0.25 * a + 0.5 * b + 0.25 * c;
            gs[k] = g > gs[k] ? gs[k] * 0.3 + g * 0.7 : gs[k] * 0.55 + g * 0.45;
        }
        for (int k = 0; k < BINS; k++)
        {
            double g = gs[k]; reL[k] *= g; imL[k] *= g; reR[k] *= g; imR[k] *= g;
            if (k > 0 && k < FN / 2) { reL[FN - k] = reL[k]; imL[FN - k] = -imL[k]; reR[FN - k] = reR[k]; imR[FN - k] = -imR[k]; }
        }
        Fft.Transform(reL, imL, true); Fft.Transform(reR, imR, true);
        long start = end - FN;
        for (int i = 0; i < FN; i++)
        {
            long p = start + i; int idx = (int)(((p % ola.Length) + ola.Length) % ola.Length);
            ola[idx] += reL[i] * win[i] / 1.5; olaR[idx] += reR[i] * win[i] / 1.5;
        }
    }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            float dl = l[i], dr = r[i];
            double dryL = inL[inPos], dryR = inR[inPos];   // the sample from exactly FN samples ago = latency-matched dry
            inL[inPos] = dl; inR[inPos] = dr; inPos = (inPos + 1) % FN; cnt++;
            if (cnt % HOP == 0 && cnt >= FN) Frame(cnt);
            long p = cnt - FN; // complete sample index
            double ol = 0, or = 0;
            if (p >= 0)
            {
                int idx = (int)(p % ola.Length); ol = ola[idx]; or = olaR[idx]; ola[idx] = 0; olaR[idx] = 0;
            }
            l[i] = (float)(ol * mix + dryL * (1 - mix)); r[i] = (float)(or * mix + dryR * (1 - mix));
        }
    }
}

// ======================================================================
//  Pitch correction (Auto-Tune style)
//  - YIN pitch detection on a decimated signal
//  - note/scale quantiser with hysteresis, retune speed, strength and "natural" (keeps vibrato)
//  - period-synchronous overlap-add (PSOLA-style) pitch shifter, formants largely preserved
// ======================================================================
class PitchCorrector : Fx
{
    public static readonly string[] Keys = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    public static readonly string[] Scales = { "Chromatic", "Major", "Minor", "Harmonic minor", "Major pentatonic", "Minor pentatonic", "Blues", "Dorian" };
    static readonly int[][] Steps = {
        new[]{0,1,2,3,4,5,6,7,8,9,10,11}, new[]{0,2,4,5,7,9,11}, new[]{0,2,3,5,7,8,10}, new[]{0,2,3,5,7,8,11},
        new[]{0,2,4,7,9}, new[]{0,3,5,7,10}, new[]{0,3,5,6,7,10}, new[]{0,2,3,5,7,9,10} };
    public static readonly string[] Ranges = { "Low voice (70-450 Hz)", "Standard (90-700 Hz)", "High voice (140-1000 Hz)", "Wide (60-1100 Hz)" };
    static readonly double[] RangeLo = { 70, 90, 140, 60 }, RangeHi = { 450, 700, 1000, 1100 };

    public static int MaskFor(int key, int scale)
    {
        int m = 0; foreach (int s in Steps[Math.Max(0, Math.Min(Steps.Length - 1, scale))]) m |= 1 << ((key + s) % 12); return m;
    }

    // live info for the UI
    public volatile float DetHz, TargetHz, ShiftCents; public volatile bool Voiced; public volatile int NoteMidi = -1;

    const int RB = 16384, RM = RB - 1, HOPS = 256, D = 3072;
    float[] inL = new float[RB], inR = new float[RB], accL = new float[RB], accR = new float[RB], wsum = new float[RB];
    long inW, outN; double nextTs; List<double> marks = new List<double>(); double lastMark; int markPtr;
    float[] histRatio = new float[256], histPer = new float[256]; long hopCount;
    // detection
    float[] dec = new float[4096]; int decW; float decAcc; bool decHalf; double sr2;
    double s, devSlow; int curNote = int.MinValue; double f0hist1, f0hist2; double lastT = 441; int unvoicedHops;
    double speedMs, strength, natural, detune, mix, fmin, fmax; int mask = 0xFFF;
    double[] dd = new double[1024]; float[] tmp = new float[2800];

    public override int Latency { get { return D; } }

    public PitchCorrector()
    {
        Type = "pitch"; Title = "Pitch Correction"; AutoKeyPending = true;
        AddC("key", "Key", Keys, 0); AddC("scale", "Scale", Scales, 1); AddP("mask", "Notes", 0, 4095, MaskFor(0, 1), "");
        AddP("speed", "Retune speed", 0, 400, 25, "ms"); AddP("strength", "Strength", 0, 100, 100, "%");
        AddP("natural", "Keep vibrato", 0, 100, 40, "%"); AddC("range", "Voice range", Ranges, 1);
        AddP("detune", "Reference detune", -50, 50, 0, "cents"); AddP("mix", "Mix", 0, 100, 100, "%");
    }

    // Apply key/scale to the note mask (UI calls this after the key or scale changes).
    public void ApplyScale() { Set("mask", MaskFor((int)Get("key"), (int)Get("scale"))); }

    protected override void Update()
    {
        speedMs = Get("speed"); strength = Get("strength") / 100.0; natural = Get("natural") / 100.0; detune = Get("detune"); mix = Get("mix") / 100.0;
        int ri = (int)Get("range"); fmin = RangeLo[ri]; fmax = RangeHi[ri]; mask = (int)Get("mask") & 0xFFF; if (mask == 0) mask = 0xFFF;
        sr2 = SR / 2.0;
    }

    public override void Reset()
    {
        base.Reset();
        Array.Clear(inL, 0, RB); Array.Clear(inR, 0, RB); Array.Clear(accL, 0, RB); Array.Clear(accR, 0, RB); Array.Clear(wsum, 0, RB);
        inW = 0; outN = 0; nextTs = 0; marks.Clear(); marks.Add(0); lastMark = 0; markPtr = 0;
        Array.Clear(histRatio, 0, 256); Array.Clear(histPer, 0, 256); hopCount = 0; Array.Clear(dec, 0, dec.Length); decW = 0; decHalf = false;
        s = 0; devSlow = 0; curNote = int.MinValue; f0hist1 = f0hist2 = 0; unvoicedHops = 0; lastT = 441;
        for (int i = 0; i < 256; i++) { histRatio[i] = 1; histPer[i] = 441; }
    }

    // ---------- YIN ----------
    bool Detect(out double f0)
    {
        f0 = 0;
        int W = 704, tauMin = Math.Max(2, (int)(sr2 / fmax)), tauMax = Math.Min(480, (int)(sr2 / fmin));
        int need = W + tauMax + 2; if (need > tmp.Length) return false;
        for (int i = 0; i < need; i++) tmp[i] = dec[((decW - need + i) % dec.Length + dec.Length) % dec.Length];
        double e = 0; for (int i = need - W; i < need; i++) e += tmp[i] * tmp[i];
        if (Math.Sqrt(e / W) < 0.0025) return false;
        double run = 0; dd[0] = 1;
        double[] raw = new double[tauMax + 2];
        for (int tau = 1; tau <= tauMax + 1; tau++)
        {
            double sum = 0;
            for (int j = 0; j < W; j++) { double df = tmp[j] - tmp[j + tau]; sum += df * df; }
            raw[tau] = sum; run += sum; dd[tau] = run > 0 ? sum * tau / run : 1;
        }
        int best = -1;
        for (int tau = tauMin; tau <= tauMax; tau++)
        {
            if (dd[tau] < 0.15)
            {
                while (tau + 1 <= tauMax && dd[tau + 1] < dd[tau]) tau++;
                best = tau; break;
            }
        }
        if (best < 0)
        {
            double m = 1e9; int bi = -1;
            for (int tau = tauMin; tau <= tauMax; tau++) if (dd[tau] < m) { m = dd[tau]; bi = tau; }
            if (bi < 0 || m > 0.30) return false;
            best = bi;
        }
        double t = best;
        if (best > 1 && best < tauMax)
        {
            double a = dd[best - 1], b = dd[best], c = dd[best + 1], den = a - 2 * b + c;
            if (Math.Abs(den) > 1e-12) t = best + 0.5 * (a - c) / den;
        }
        f0 = sr2 / t;
        if (!(f0 >= fmin * 0.9 && f0 <= fmax * 1.1)) return false;
        f0 = RefineFullRate(f0);
        return true;
    }

    // Re-measure the period on the full-rate signal around the decimated estimate (sub-cent accuracy).
    float[] fr = new float[3200];
    double RefineFullRate(double f0)
    {
        double P0 = SR / f0; int lo = (int)Math.Round(P0) - 4; if (lo < 2) return f0;
        int hi = lo + 8, Wf = 1400, need = Wf + hi + 2; if (need > fr.Length || inW < need + 8) return f0;
        long baseIdx = inW - need;
        for (int i = 0; i < need; i++) { int ix = (int)((baseIdx + i) & RM); fr[i] = (inL[ix] + inR[ix]) * 0.5f; }
        double[] d = new double[hi + 2]; int bm = lo; double bv = double.MaxValue;
        for (int lag = lo; lag <= hi; lag++)
        {
            double sum = 0; for (int j = 0; j < Wf; j++) { double df = fr[j] - fr[j + lag]; sum += df * df; }
            d[lag] = sum; if (sum < bv) { bv = sum; bm = lag; }
        }
        double per = bm;
        if (bm > lo && bm < hi)
        {
            double a = d[bm - 1], b = d[bm], c = d[bm + 1], den = a - 2 * b + c;
            if (Math.Abs(den) > 1e-12) per = bm + 0.5 * (a - c) / den;
        }
        if (Math.Abs(per - P0) > 3.5) return f0;
        return SR / per;
    }

    int NearestAllowed(double midi)
    {
        int c = (int)Math.Round(midi), best = c; double bd = 1e9;
        for (int n = c - 6; n <= c + 6; n++)
        {
            int pc = ((n % 12) + 12) % 12;
            if ((mask & (1 << pc)) == 0) continue;
            double d = Math.Abs(n - midi); if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    void Hop()
    {
        double f0; bool voiced = Detect(out f0);
        double hopSec = HOPS / (double)SR;
        if (voiced)
        {
            // median of 3 rejects single-frame octave errors
            double a = f0, b = f0hist1 > 0 ? f0hist1 : f0, c = f0hist2 > 0 ? f0hist2 : f0;
            double med = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
            f0hist2 = f0hist1; f0hist1 = f0; f0 = med;
            unvoicedHops = 0;
            double midi = 69 + 12 * Math.Log(f0 / (440.0 * Math.Pow(2, detune / 1200.0))) / Math.Log(2);
            if (curNote == int.MinValue || Math.Abs(midi - curNote) > 0.7)
            {
                curNote = NearestAllowed(midi); devSlow = (midi - curNote) * 100;
            }
            double dev = (midi - curNote) * 100;
            devSlow += (dev - devSlow) * (1 - Math.Exp(-hopSec / 0.12));
            double target = -strength * ((1 - natural) * dev + natural * devSlow);
            if (speedMs <= 0.5) s = target; else s += (target - s) * (1 - Math.Exp(-hopSec / (speedMs * 0.001)));
            lastT = lastT * 0.5 + (SR / f0) * 0.5;
            DetHz = (float)f0; NoteMidi = curNote; TargetHz = (float)(440.0 * Math.Pow(2, (curNote - 69) / 12.0)); Voiced = true;
        }
        else
        {
            unvoicedHops++;
            s *= Math.Exp(-hopSec / 0.04);
            if (unvoicedHops > 40) { curNote = int.MinValue; f0hist1 = f0hist2 = 0; }
            Voiced = false; if (unvoicedHops > 8) lastT = lastT * 0.9 + 441 * 0.1;
        }
        double ratio = Math.Pow(2, s / 1200.0); ratio = Math.Max(0.7, Math.Min(1.45, ratio));
        ShiftCents = (float)s;
        int hi = (int)(hopCount & 255);
        histRatio[hi] = (float)ratio; histPer[hi] = (float)Math.Max(60, Math.Min(900, voiced ? lastT : (unvoicedHops > 8 ? 441 : lastT)));
        hopCount++;
    }

    double PeriodAt(double t)
    {
        long h = (long)(t / HOPS); if (h < 0 || hopCount - h > 250 || h >= hopCount) return lastT > 0 ? Math.Max(60, Math.Min(900, lastT)) : 441;
        return histPer[(int)(h & 255)];
    }
    double RatioAt(double t)
    {
        long h = (long)(t / HOPS); if (h < 0 || hopCount - h > 250 || h >= hopCount) return 1.0;
        return histRatio[(int)(h & 255)];
    }

    void Schedule()
    {
        while (nextTs < inW + 1000)
        {
            double tin = nextTs - D;
            while (lastMark < tin + 1400) { lastMark += PeriodAt(lastMark); marks.Add(lastMark); }
            // nearest mark to tin
            if (markPtr >= marks.Count) markPtr = marks.Count - 1;
            while (markPtr + 1 < marks.Count && Math.Abs(marks[markPtr + 1] - tin) <= Math.Abs(marks[markPtr] - tin)) markPtr++;
            while (markPtr > 0 && Math.Abs(marks[markPtr - 1] - tin) < Math.Abs(marks[markPtr] - tin)) markPtr--;
            double m = marks[markPtr];
            int Tg = (int)Math.Round(PeriodAt(m)); Tg = Math.Max(40, Math.Min(900, Tg));
            double ratio = RatioAt(tin + 704);   // detection window is centred on the audio being shifted
            int ts = (int)Math.Round(nextTs), mi = (int)Math.Round(m);
            for (int c = -Tg; c < Tg; c++)
            {
                float w = (float)(0.5 * (1 + Math.Cos(Math.PI * c / Tg)));
                int io = (ts + c) & RM, ii = (mi + c) & RM;
                accL[io] += inL[ii] * w; accR[io] += inR[ii] * w; wsum[io] += w;
            }
            nextTs += PeriodAt(m) / ratio;   // exact (non-rounded) period keeps the output pitch accurate
            if (markPtr > 400) { marks.RemoveRange(0, markPtr - 20); markPtr = 20; }
        }
    }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (marks.Count == 0) Reset();
        for (int i = 0; i < n; i++)
        {
            int w = (int)(inW & RM); inL[w] = l[i]; inR[w] = r[i];
            float mono = (l[i] + r[i]) * 0.5f;
            if (!decHalf) { decAcc = mono; decHalf = true; }
            else { dec[decW % dec.Length] = (decAcc + mono) * 0.5f; decW++; decHalf = false; }
            inW++;
            if (inW % HOPS == 0) Hop();
        }
        Schedule();
        for (int i = 0; i < n; i++)
        {
            long p = outN + i; int idx = (int)(p & RM);
            float ws = wsum[idx], wl = 0, wr = 0;
            if (ws > 0.08f) { wl = accL[idx] / ws; wr = accR[idx] / ws; }
            accL[idx] = 0; accR[idx] = 0; wsum[idx] = 0;
            int di = (int)((p - D) & RM); float dl = p >= D ? inL[di] : 0, dr = p >= D ? inR[di] : 0;
            l[i] = (float)(dl + (wl - dl) * mix); r[i] = (float)(dr + (wr - dr) * mix);
        }
        outN += n;
    }
}
