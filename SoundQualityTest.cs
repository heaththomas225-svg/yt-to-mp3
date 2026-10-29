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

    // crackle: 2 ms windows of the top end (the 4th difference) much louder than the windows around them
    static int Bursts(float[] x, int sr)
    {
        var h = new float[x.Length]; for (int i = 4; i < x.Length; i++) h[i] = x[i] - 4 * x[i - 1] + 6 * x[i - 2] - 4 * x[i - 3] + x[i - 4];
        int W = sr / 500, n = h.Length / W; var e = new double[n]; for (int k = 0; k < n; k++) { double m = 0; for (int i = k * W; i < (k + 1) * W; i++) m = Math.Max(m, Math.Abs(h[i])); e[k] = m; }
        int b = 0; for (int k = 12; k < n - 12; k++) { var a = new List<double>(); for (int j = k - 12; j <= k + 12; j++) if (Math.Abs(j - k) > 2) a.Add(e[j]); a.Sort(); double med = a[a.Count / 2]; if (e[k] > 0.03 && e[k] / Math.Max(med, 0.004) > 5) { b++; k += 2; } }
        return b;
    }
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
        {   // the built-in piano (piano roll keys): a key struck again, and a loud chord, without clicks or clipping
            int ksr = StudioEngine.SR, B = 256, N = ksr * 2; var k = new KeysFx(); k.Prepare(ksr); var L = new float[N];
            Func<int, int, double> spike = (a0, b0) => { double m = 0; for (int i = Math.Max(2, a0); i < Math.Min(N, b0); i++) m = Math.Max(m, Math.Abs(L[i] - 2 * L[i - 1] + L[i - 2])); return m; };
            int re = ksr / 2 / B * B, ch = ksr / B * B;
            for (int p0 = 0; p0 < N; p0 += B)
            {
                if (p0 == 0) k.Midi(0x90, 60, 110, false); if (p0 == re) { k.Midi(0x80, 60, 0, false); k.Midi(0x90, 60, 110, false); }
                if (p0 == ch) foreach (int kn in new[] { 48, 52, 55, 60, 64, 67, 72 }) k.Midi(0x90, (byte)kn, 127, false);
                k.BeginBlock(null, p0, B, false); var bl = new float[B]; var br = new float[B]; k.Process(bl, br, B); Array.Copy(bl, 0, L, p0, Math.Min(B, N - p0));
            }
            double steady = spike(ksr / 4, ksr / 4 + 2000), rs = spike(re - 4, re + 400), pk = 0; for (int i = ch; i < N; i++) pk = Math.Max(pk, Math.Abs(L[i]));
            Check("the piano: a key struck again does not click (it fades the old note)", rs < steady * 2.5, "restrike " + rs.ToString("0.0000") + " vs steady " + steady.ToString("0.0000"));
            Check("the piano: a loud 7-note chord is not clipped", pk < 0.9, "peak " + pk.ToString("0.000"));
        }
        {   // every instrument: a key struck again or a note switched does not click, and nothing goes past full scale (the engine's guard on instruments)
            int isr = StudioEngine.SR, B = 256, N = isr * 2; var evs = new List<int[]> { new[] { 0, 0x90, 48, 110 }, new[] { isr / 2, 0x80, 48, 0 }, new[] { isr / 2, 0x90, 48, 110 }, new[] { isr, 0x90, 51, 110 }, new[] { isr, 0x80, 48, 0 } };
            foreach (int kn in new[] { 48, 55, 60, 63, 67 }) evs.Add(new[] { isr * 3 / 2, 0x90, kn, 127 });
            Func<InstrumentFx, float[]> render = k => { var L = new float[N]; for (int p0 = 0; p0 < N; p0 += B) { foreach (var m in evs) if (m[0] / B * B == p0) k.Midi((byte)m[1], (byte)m[2], (byte)m[3], false); k.BeginBlock(null, p0, B, false); var bl = new float[B]; var br = new float[B]; k.Process(bl, br, B); k.Guard.Run(bl, br, B); Array.Copy(bl, 0, L, p0, Math.Min(B, N - p0)); } return L; };
            Func<float[], int, int, double> spk = (L, a0, b0) => { double m = 0; for (int i = Math.Max(2, a0); i < Math.Min(L.Length, b0); i++) m = Math.Max(m, Math.Abs(L[i] - 2 * L[i - 1] + L[i - 2])); return m; };
            foreach (var nm in new[] { "Reese Bass", "Supersaw Lead", "Triplet Wobble", "Sub Square" })
            {
                var f = new SynthFx(); f.Prepare(isr); SynthPresets.Apply(f, nm); var L = render(f); double worst = 0;
                foreach (int t in new[] { isr / 2 / B * B, isr / B * B }) { double before = Math.Max(spk(L, t - 1500, t - 50), spk(L, t + 1500, t + 3000)), after = spk(L, t - 2, t + 300); worst = Math.Max(worst, after / Math.Max(before, 0.002)); }
                double pk = 0; foreach (var v in L) pk = Math.Max(pk, Math.Abs(v));
                Check("Bass Lab " + nm + ": a key struck again or a new note does not click, and it is not clipped", worst < 4 && pk < 1, "click x" + worst.ToString("0.0") + " peak " + pk.ToString("0.00"));
            }
            foreach (var ty in new[] { "pxchord", "wobble", "pxcurrent" }) { var f = (InstrumentFx)FxRegistry.Create(ty); f.Prepare(isr); var L = render(f); double pk = 0; foreach (var v in L) pk = Math.Max(pk, Math.Abs(v)); Check(f.Title + " never goes past full scale (it crackled when it did)", pk < 1, "peak " + pk.ToString("0.00")); }
        }
        { // ChordPad played fast with chords held (all 24 voices busy): a voice taken for a new note fades out instead of being cut (that clicked)
            var cp = (InstrumentFx)FxRegistry.Create("pxchord"); cp.Prepare(44100); var ct = StudioEngine.NewTrack("C", "inst", null); ct.Instrument = true; cp.Owner = ct; ct.Fx.Add(cp); StudioEngine.Current = null;   /* (no sound card) */ int blk = 1024, total = 44100 * 6; var L = new float[total]; var bl = new float[blk]; var br = new float[blk]; var rnd = new Random(7); int[] mel = { 48, 51, 55, 58, 60, 63, 55, 51 }; var down = new List<int>(); int ev = 0;
            for (int p = 0; p < total; p += blk)
            {
                double tt = p / 44100.0;
                if (rnd.NextDouble() < (tt > 2.5 && tt < 3.5 ? 0.9 : 0.45)) { int nn = mel[ev++ % mel.Length] + (rnd.Next(3) - 1) * 12; if (down.Count > 3 || (down.Count > 0 && rnd.NextDouble() < 0.5)) { int o = down[0]; down.RemoveAt(0); cp.NoteOff(o); } if (!down.Contains(nn)) { cp.NoteOn(nn, 70 + rnd.Next(50)); down.Add(nn); } }
                if (Math.Abs(tt - 4.5) < 0.012) foreach (int nn in new[] { 48, 55, 60, 63, 67 }) if (!down.Contains(nn)) { cp.NoteOn(nn, 120); down.Add(nn); }
                Array.Clear(bl, 0, blk); Array.Clear(br, 0, blk); cp.BeginBlock(ct, p, blk, false); cp.Process(bl, br, blk); Array.Copy(bl, 0, L, p, Math.Min(blk, total - p));
            }
            int bursts = Bursts(L, 44100); Check("ChordPad played fast (more notes than voices) has no clicks", bursts == 0, bursts + " clicks");
        }
        Console.WriteLine(fails == 0 ? "=== sound quality checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
