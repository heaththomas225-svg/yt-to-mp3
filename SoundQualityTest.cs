using System;
using System.Collections.Generic;

// Measures the sound quality fixes against exact references: the band-limited metal tone (hi-hats, cymbals) and saws against ideal additive
// versions, and plucked notes against their target pitch.   csc /main:SQ ... SoundQualityTest.cs
static class SQ
{
    const int SR = 44100; static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    // an ideal square: only the odd harmonics that fit below 20 kHz
    static float[] IdealMetal(int n, double pitch, double[] ratios)
    {
        var x = new float[n];
        foreach (var rt in ratios) { double f = rt * pitch; for (int k = 1; k * f < 20000; k += 2) { double a = 4 / (Math.PI * k) / ratios.Length, w = 2 * Math.PI * k * f / SR; for (int i = 0; i < n; i++) x[i] += (float)(a * Math.Sin(w * i)); } }
        return x;
    }
    static float[] NaiveMetal(int n, double pitch, double[] ratios) { var x = new float[n]; for (int i = 0; i < n; i++) { double t = i / (double)SR, s = 0; foreach (var f in ratios) s += Math.Sin(2 * Math.PI * f * pitch * t) > 0 ? 1.0 : -1.0; x[i] = (float)(s / ratios.Length); } return x; }
    // the error against the ideal, in dB, ignoring the very top (above 18 kHz both are filtered away by any playback)
    static double ErrDb(float[] a, float[] ideal)
    {
        var lp1 = new Svf(); var lp2 = new Svf(); var lq1 = new Svf(); var lq2 = new Svf(); double e = 0, o = 0;
        for (int i = 0; i < a.Length; i++) { float x = lp2.Process(lp1.Process(a[i], 16000f, 0.7f, 0), 16000f, 0.7f, 0), y = lq2.Process(lq1.Process(ideal[i], 16000f, 0.7f, 0), 16000f, 0.7f, 0); if (i < 2000) continue; e += (x - y) * (x - y); o += y * y; }
        return 10 * Math.Log10(e / (o + 1e-20));
    }
    // pitch by autocorrelation (with a parabola between lags for the fraction)
    static double PitchHz(float[] x, int from, int len, double guess)
    {
        int lo = (int)(SR / (guess * 1.2)), hi = (int)(SR / (guess / 1.2)) + 2; double best = -1; int bl = lo; var c = new double[hi + 2];
        for (int lag = lo - 1; lag <= hi + 1; lag++) { double s = 0; for (int i = from; i < from + len; i++) s += x[i] * x[i + lag]; c[lag] = s; }
        for (int lag = lo; lag <= hi; lag++) if (c[lag] > best && c[lag] >= c[lag - 1] && c[lag] >= c[lag + 1]) { best = c[lag]; bl = lag; }
        double den = c[bl - 1] - 2 * c[bl] + c[bl + 1], off = Math.Abs(den) < 1e-12 ? 0 : 0.5 * (c[bl - 1] - c[bl + 1]) / den; return SR / (bl + off);
    }
    static float[] OldPluck(Rnd r, double hz, double len, double bright) { int n = (int)(len * SR), d = Math.Max(2, (int)(SR / hz)); var line = new float[d]; for (int i = 0; i < d; i++) line[i] = r.Bi(); var x = new float[n]; int p = 0; float prev = 0; float damp = (float)(0.992 - (1 - bright) * 0.03); for (int i = 0; i < n; i++) { float v = line[p]; float nv = damp * 0.5f * (v + prev); prev = v; line[p] = nv; p = (p + 1) % d; x[i] = v; } return x; }

    static void Main()
    {
        double[] ratios = { 205.3, 304.4, 369.6, 522.7, 540.0, 800.0 }; int n = SR;
        foreach (double pitch in new[] { 1.0, 1.15, 2.6 })
        {
            var ideal = IdealMetal(n, pitch, ratios); double before = ErrDb(NaiveMetal(n, pitch, ratios), ideal), after = ErrDb(SoundGen.MetalTone(n, pitch, ratios), ideal);
            Check("hi-hat / cymbal metal tone at pitch " + pitch + ": the folded-back harshness is gone (error against the ideal)", after < -28 && after < before - 10, before.ToString("0.0") + " dB -> " + after.ToString("0.0") + " dB");
        }
        { double f = 1400; var ideal = new float[n]; for (int k = 1; k * f < 20000; k++) { double a = -2 / (Math.PI * k), w = 2 * Math.PI * k * f / SR; for (int i = 0; i < n; i++) ideal[i] += (float)(a * Math.Sin(w * i)); }   /* 2p-1 = -(2/pi) sum sin(k w t)/k */
          var nv = new float[n]; var bl = new float[n]; double ph = 0, dt = f / SR; for (int i = 0; i < n; i++) { double p0 = ph; nv[i] = (float)(2 * p0 - 1); bl[i] = (float)SoundGen.Saw(p0, dt); ph += dt; if (ph >= 1) ph -= 1; }
          double b0 = ErrDb(nv, ideal), b1 = ErrDb(bl, ideal); Check("saw (riser, siren, vocal sounds) at 1.4 kHz: band-limited", b1 < -25 && b1 < b0 - 8, b0.ToString("0.0") + " dB -> " + b1.ToString("0.0") + " dB"); }
        double worstOld = 0, worstNew = 0; string det = "";
        foreach (int midi in new[] { 48, 55, 60, 67, 72, 79, 84 })
        {
            double hz = SoundGen.NoteHz(midi); var a = OldPluck(new Rnd(midi), hz, 0.6, 0.7); var b = SoundGen.Pluck(new Rnd(midi), hz, 0.6, 0.7).L;
            double ca = 1200 * Math.Log(PitchHz(a, 4000, 8000, hz) / hz, 2), cb = 1200 * Math.Log(PitchHz(b, 4000, 8000, hz) / hz, 2); worstOld = Math.Max(worstOld, Math.Abs(ca)); worstNew = Math.Max(worstNew, Math.Abs(cb)); det += " " + midi + ":" + ca.ToString("0") + "/" + cb.ToString("0.0");
        }
        Check("plucked notes (guitar and harp loops) are in tune from C3 to C6", worstNew < 3, "worst " + worstOld.ToString("0.0") + " cents -> " + worstNew.ToString("0.0") + " cents  (note:old/new" + det + ")");
        { var h = SoundGen.Hat(new Rnd(3), 0.05, 10, 0.2, 1.0); bool fin = true; foreach (var v in h.L) if (float.IsNaN(v) || float.IsInfinity(v)) fin = false; var c = SoundGen.Cymbal(new Rnd(3), 1.2, 2.0, false); double dl = 0, sm = 0; for (int i = 0; i < c.Length; i++) { dl += (c.L[i] - c.R[i]) * (c.L[i] - c.R[i]); sm += c.L[i] * c.L[i]; }
          Check("hats and cymbals still render cleanly, cymbals wide in stereo", fin && h.Peak() > 0.05 && c.Peak() > 0.05 && dl / (sm + 1e-12) > 0.3, "cymbal side/mid " + (dl / (sm + 1e-12)).ToString("0.00")); }
        Console.WriteLine(fails == 0 ? "=== sound quality checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
