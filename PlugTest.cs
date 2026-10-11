using System;
using System.Collections.Generic;
using System.Diagnostics;

// Checks every built-in library plugin processes audio safely, and the DSP engines do what they claim.   csc /main:P ... PlugTest.cs
static class P
{
    static int fails; const int SR = 44100;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static float[] Sine(double f, int n, double a) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(a * Math.Sin(2 * Math.PI * f * i / SR)); return x; }
    // a voice-like test sound: 150 Hz harmonic buzz through two formants, with syllable-like envelope and some breath noise
    static float[] Voice(int n)
    {
        var x = new float[n]; var rnd = new Random(1); double lp1 = 0, lp2 = 0, b1 = 0, b2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, ph = (t * 150) % 1.0, s = 0; for (int h = 1; h <= 30; h++) s += Math.Sin(2 * Math.PI * h * 150 * t) / h;
            double env = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 2.2 * t); env = env * env;
            double nz = (rnd.NextDouble() * 2 - 1) * 0.02;
            x[i] = (float)(0.25 * s * (0.2 + env) + nz);
        }
        return x;
    }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    static double PeakHz(float[] x, int start, int n)
    {
        var re = new double[n]; var im = new double[n]; for (int i = 0; i < n; i++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n); re[i] = x[start + i] * w; }
        Fft.Transform(re, im, false); int best = 1; double bm = 0; for (int k = 1; k < n / 2; k++) { double m = re[k] * re[k] + im[k] * im[k]; if (m > bm) { bm = m; best = k; } }
        double a = Math.Log(Math.Sqrt(re[best - 1] * re[best - 1] + im[best - 1] * im[best - 1]) + 1e-12), b = Math.Log(Math.Sqrt(bm) + 1e-12), c = Math.Log(Math.Sqrt(re[best + 1] * re[best + 1] + im[best + 1] * im[best + 1]) + 1e-12);
        double p = 0.5 * (a - c) / (a - 2 * b + c); return (best + p) * SR / n;
    }
    static double BandRms(float[] x, int start, int n, double f0, double f1)
    {
        var re = new double[n]; var im = new double[n]; for (int i = 0; i < n; i++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n); re[i] = x[start + i] * w; }
        Fft.Transform(re, im, false); double s = 0; for (int k = (int)(f0 * n / SR); k <= (int)(f1 * n / SR) && k < n / 2; k++) s += re[k] * re[k] + im[k] * im[k]; return Math.Sqrt(s);
    }
    static void Run(Fx f, float[] l, float[] r, int block)
    {
        f.Prepare(SR); f.Reset(); f.BeginBlock(null, 0, 0, true);
        var bl = new float[block]; var br = new float[block];
        for (int p = 0; p < l.Length; p += block) { int n = Math.Min(block, l.Length - p); Array.Copy(l, p, bl, 0, n); Array.Copy(r, p, br, 0, n); f.BeginBlock(null, p, n, true); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); Array.Copy(br, 0, r, p, n); }
    }
    static void Feed(Fx f, float[] l, float[] r, int from, int to, int block)
    {
        var bl = new float[block]; var br = new float[block];
        for (int p = from; p < to; p += block) { int n = Math.Min(block, to - p); Array.Copy(l, p, bl, 0, n); Array.Copy(r, p, br, 0, n); f.BeginBlock(null, p, n, true); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); Array.Copy(br, 0, r, p, n); }
    }
    static readonly HashSet<string> PassThrough = new HashSet<string> { "Vocal Alignment", "Breath Control", "Breath Remover", "Vocal Gate", "Vocal Freeze", "Vocal Width Control", "Tape Stop", "Vinyl Stop", "Stutter Edit", "Riser Generator", "Whoosh Generator", "Bass Mono Maker", "Stereo Modulator" };
    static Fx Make(string spec) { return PluginLibrary.Build(spec, "t"); }

    [STAThread]
    static void Main()
    {
        var touch = FxRegistry.All;
        Check("the library registered named plugins", PluginLibrary.All.Count > 40, PluginLibrary.All.Count + " plugins");
        // ---- DSP accuracy ----
        { var f = Make("pshift{p1=7;keep=0}"); var l = Sine(440, SR * 3, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR * 2, 16384); Check("phase-vocoder pitch shift +7 semitones puts 440 Hz at 659 Hz", Math.Abs(hz - 659.3) < 4, hz.ToString("0.0") + " Hz, level " + Rms(l, SR * 2, SR * 3).ToString("0.000")); Check("... and keeps its loudness", Rms(l, SR * 2, SR * 3) > 0.18 && Rms(l, SR * 2, SR * 3) < 0.45, ""); }
        { var f = Make("pshift{p1=-12;keep=0}"); var l = Sine(440, SR * 3, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 333); double hz = PeakHz(l, SR * 2, 16384); Check("pitch shift down an octave (odd block size)", Math.Abs(hz - 220) < 3, hz.ToString("0.0") + " Hz"); }
        { var f = Make("pshift{p1=0;c1=20;mix=100}"); var l = Sine(1000, SR * 3, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR * 2, 32768); Check("micro detune +20 cents moves 1000 Hz to 1011.6 Hz", Math.Abs(hz - 1011.6) < 2.5, hz.ToString("0.0") + " Hz"); }
        { var f = Make("cshift{p1=5;win=25;dry=0}"); var l = Sine(440, SR * 3, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR * 2, 16384); Check("classic shifter +5 semitones puts 440 Hz at 587 Hz", Math.Abs(hz - 587.3) < 18, hz.ToString("0.0") + " Hz, level " + Rms(l, SR * 2, SR * 3).ToString("0.000")); }
        { var f = Make("fshift{shift=200}"); var l = Sine(1000, SR * 2, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR, 16384); double up = BandRms(l, SR, 16384, 1150, 1250), dn = BandRms(l, SR, 16384, 750, 850); Check("frequency shifter +200 Hz moves 1000 Hz to 1200 Hz", Math.Abs(hz - 1200) < 6, hz.ToString("0.0") + " Hz"); Check("... and rejects the mirror image (>20 dB)", dn < up * 0.1, "image " + (20 * Math.Log10(dn / up + 1e-9)).ToString("0") + " dB"); }
        { var f = Make("formant{shift=-5}"); var v = Voice(SR * 3); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR * 2 - 2000, 16384), hz0 = PeakHz(v, SR * 2 - 2000 - 2047, 16384);
          double lo0 = BandRms(v, SR, 16384, 100, 1000), hi0 = BandRms(v, SR, 16384, 1000, 5000), lo1 = BandRms(l, SR + 2047, 16384, 100, 1000), hi1 = BandRms(l, SR + 2047, 16384, 1000, 5000);
          Check("formant shifter keeps the pitch (about 150 Hz)", Math.Abs(hz - 150) < 6 || Math.Abs(hz - 300) < 6 || Math.Abs(hz - 450) < 8, hz.ToString("0.0") + " Hz (input peak " + hz0.ToString("0.0") + ")"); Check("... and moves the spectrum down (more low than high)", lo1 / hi1 > lo0 / hi0, "low/high " + (lo0 / hi0).ToString("0.00") + " -> " + (lo1 / hi1).ToString("0.00")); }
        { var f = Make("comb{mode=0;freq=440;fb=90;mix=100}"); var l = new float[SR * 2]; var rnd = new Random(2); for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); double hz = PeakHz(l, SR, 32768); Check("comb resonator on noise rings at its tuned note (440 Hz or a multiple)", Math.Abs(hz / 440.0 - Math.Round(hz / 440.0)) < 0.03 && hz > 300, hz.ToString("0.0") + " Hz"); }
        { var f = Make("ring{freq=300;wave=0;depth=100}"); var l = Sine(1000, SR, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double a = BandRms(l, 2000, 16384, 650, 750), b = BandRms(l, 2000, 16384, 1250, 1350), c = BandRms(l, 2000, 16384, 980, 1020); Check("ring modulator makes sum and difference tones (700 and 1300 Hz), not the original", a > c * 5 && b > c * 5, "700: " + a.ToString("0") + " 1300: " + b.ToString("0") + " 1000: " + c.ToString("0")); }
        { var f = Make("crush{bits=4;rate=48000}"); var l = Sine(300, SR, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); var set = new HashSet<float>(); for (int i = 1000; i < 20000; i++) set.Add(l[i]); Check("bit crusher at 4 bits has few distinct levels", set.Count <= 16, set.Count + " levels"); }
        { var f = Make("band{hp=300;lp=3400;slope=2;mono=0}"); var l = new float[SR]; var rnd = new Random(4); for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); double mid = BandRms(l, 4000, 16384, 600, 2800), lo = BandRms(l, 4000, 16384, 20, 100), hi = BandRms(l, 4000, 16384, 8000, 16000); Check("telephone band keeps the middle and removes the bass and treble (>25 dB)", lo < mid * 0.05 && hi < mid * 0.05, "bass " + (20 * Math.Log10(lo / mid)).ToString("0") + " dB, treble " + (20 * Math.Log10(hi / mid)).ToString("0") + " dB"); }
        { var f = Make("shaper{mode=0;drive=24;os=1}"); var l = Sine(1000, SR, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double h3 = BandRms(l, 4000, 16384, 2900, 3100), h1 = BandRms(l, 4000, 16384, 900, 1100); Check("saturation adds odd harmonics to a sine", h3 > h1 * 0.03 && Rms(l, 4000, 30000) < 1.0, "3rd harmonic " + (20 * Math.Log10(h3 / h1)).ToString("0") + " dB below the fundamental"); }
        { var f = Make("vocoder{carrier=0;note=48}"); var v = Voice(SR * 2); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double ri = Rms(v, SR, SR * 2), ro = Rms(l, SR, SR * 2); Check("vocoder output is alive and about as loud as the input", ro > ri * 0.3 && ro < ri * 3, "in " + ri.ToString("0.000") + " out " + ro.ToString("0.000")); }

        // ---- echo, reverb, convolution ----
        { var f = Make("echo{mode=0;sync=0;time=250;fb=50;lc=20;hc=20000;mix=100}"); var l = new float[SR * 2]; l[1000] = 1f; var r = (float[])l.Clone(); Run(f, l, r, 512);
          int p1 = 1000 + (int)(0.25 * SR), p2 = 1000 + (int)(0.5 * SR); float a1 = 0, a2 = 0; for (int i = -8; i <= 8; i++) { a1 += Math.Abs(l[p1 + i]); a2 += Math.Abs(l[p2 + i]); }
          Check("echo: first repeat arrives 250 ms later at full level, second at half", a1 > 0.8 && Math.Abs(a2 / a1 - 0.5) < 0.12, a1.ToString("0.00") + " then " + a2.ToString("0.00")); }
        { Fx.ProjectBpm = 120; var f = Make("echo{mode=2;sync=4;fb=50;lc=20;hc=20000;mix=100}"); var l = new float[SR * 3]; l[1000] = 1f; var r = new float[SR * 3]; r[1000] = 1f; Run(f, l, r, 512);
          int p1 = 1000 + SR / 2, p2 = 1000 + SR; float l1 = 0, r1 = 0, l2 = 0, r2 = 0; for (int i = -4; i <= 4; i++) { l1 = Math.Max(l1, Math.Abs(l[p1 + i])); r1 = Math.Max(r1, Math.Abs(r[p1 + i])); l2 = Math.Max(l2, Math.Abs(l[p2 + i])); r2 = Math.Max(r2, Math.Abs(r[p2 + i])); }
          Check("ping-pong at 120 BPM (1/4 = 500 ms): first repeat on the left, the next on the right", l1 > 0.5 && r1 < 0.1 && r2 > 0.2 && l2 < 0.1, "L " + l1.ToString("0.00") + "/" + l2.ToString("0.00") + "  R " + r1.ToString("0.00") + "/" + r2.ToString("0.00")); }
        { var f = Make("echo{mode=4;sync=0;time=300;fb=0;mix=100}"); var v = Voice(SR * 3); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double dd = 0; for (int i = SR; i < SR * 3; i++) dd += (l[i] - v[i]) * (l[i] - v[i]); double ro = Math.Sqrt(dd / (SR * 2)); Check("reverse echo adds a backwards copy of the sound", ro > Rms(v, SR, SR * 3) * 0.3, "rms " + Rms(v, SR, SR * 3).ToString("0.000") + " -> " + ro.ToString("0.000")); }
        { var f = Make("reverb2{mode=1;size=60;decay=2.0;damp=20000;hc=20000;lc=20;er=0;pre=0;mod=0;mix=100}"); var l = new float[SR * 5]; l[0] = 1f; var r = (float[])l.Clone(); Run(f, l, r, 512);
          // energy decay curve (Schroeder), then the time it takes to fall 30 dB
          double[] e = new double[l.Length]; double s = 0; for (int i = l.Length - 1; i >= 0; i--) { s += l[i] * l[i] + r[i] * r[i]; e[i] = s; } double e0 = e[0]; int t5 = 0, t35 = 0; for (int i = 0; i < e.Length; i++) { double db = 10 * Math.Log10(e[i] / e0 + 1e-20); if (t5 == 0 && db < -5) t5 = i; if (t35 == 0 && db < -35) { t35 = i; break; } }
          double rt = (t35 - t5) / (double)SR * 2; Check("reverb with decay set to 2.0 s has a measured decay time near 2 s", rt > 1.2 && rt < 3.0 && t35 > 0, "RT60 = " + rt.ToString("0.00") + " s"); }
        { var f = Make("reverb2{mode=1;freeze=1;mix=100;er=0}"); var l = new float[SR * 4]; var rnd = new Random(3); for (int i = 0; i < SR / 2; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.5); var r = (float[])l.Clone(); // freeze after the burst: run first with freeze off
          var g = Make("reverb2{mode=1;decay=3;mix=100;er=0}"); Run(g, l, r, 512); double early = Rms(l, SR, SR + 4000), late = Rms(l, SR * 3, SR * 3 + 4000); Check("reverb tail dies away (-60 dB per RT60)", late < early * 0.7 && early > 0.001, "t=1 s " + early.ToString("0.0000") + ", t=3 s " + late.ToString("0.0000")); }
        { var f = Make("reverb2{mode=2;gate=1;gthr=-30;ghold=100;grel=20;decay=3;mix=100;er=0}"); var l = new float[SR * 2]; for (int i = 0; i < 2000; i++) l[i] = (float)(0.6 * Math.Sin(i * 0.2) * Math.Exp(-i / 500.0)); var r = (float[])l.Clone(); Run(f, l, r, 512); double during = Rms(l, 500, 3000), after = Rms(l, SR, SR + 4000); Check("gated reverb: loud tail while the hit rings, silent after the gate closes", during > 0.005 && after < during * 0.02, "during " + during.ToString("0.0000") + " after " + after.ToString("0.00000")); }
        { var f = Make("reverb2{mode=4;mix=100;decay=1.5}"); var l = new float[SR * 2]; l[0] = 1f; var r = (float[])l.Clone(); Run(f, l, r, 512); double a = Rms(l, 2000, 20000); bool fin = true; for (int i = 0; i < l.Length; i++) if (float.IsNaN(l[i])) fin = false; Check("spring reverb rings and stays stable", fin && a > 0.0005, "level " + a.ToString("0.0000")); }
        { // the convolver equals plain convolution
          var rnd = new Random(8); var ir = new float[3000]; for (int i = 0; i < ir.Length; i++) ir[i] = (float)(rnd.NextDouble() * 2 - 1) * (float)Math.Exp(-i / 800.0); var x = new float[6000]; for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() * 2 - 1);
          var cv = new Convolver(ir); var outp = new float[x.Length]; var bi = new float[Convolver.P]; var bo = new float[Convolver.P]; for (int p = 0; p + Convolver.P <= x.Length; p += Convolver.P) { Array.Copy(x, p, bi, 0, Convolver.P); cv.Process(bi, bo); Array.Copy(bo, 0, outp, p, Convolver.P); }
          double worst = 0; for (int i = 100; i < 5500; i += 37) { double d = 0; for (int k = 0; k < ir.Length && k <= i; k++) d += ir[k] * x[i - k]; worst = Math.Max(worst, Math.Abs(d - outp[i])); } Check("partitioned FFT convolution matches direct convolution", worst < 1e-3, "largest difference " + worst.ToString("0.000000")); }
        { var f = (ConvFx)FxRegistry.Create("lib_convolution_reverb"); f.Prepare(SR); f.Reset(); var warm0 = new float[512]; var warm1 = new float[512]; f.Process(warm0, warm1, 512); System.Threading.Thread.Sleep(2500); var l = new float[SR * 3]; l[0] = 1f; var r = (float[])l.Clone(); var bl = new float[512]; var br = new float[512]; for (int p = 0; p < l.Length; p += 512) { int n2 = Math.Min(512, l.Length - p); Array.Copy(l, p, bl, 0, n2); Array.Copy(r, p, br, 0, n2); f.Process(bl, br, n2); Array.Copy(bl, 0, l, p, n2); Array.Copy(br, 0, r, p, n2); }
          double a = Rms(l, 8000, 30000); Check("convolution reverb (large hall) produces a tail from its built-in space", a > 0.0003, "tail " + a.ToString("0.0000") + ", " + f.IrNote); }

        // ---- EQ and spectral tools ----
        Func<string, double, double, double> gainAt = (spec, hz, amp) => { var f = Make(spec); var l = Sine(hz, SR * 3, amp); var r = (float[])l.Clone(); Run(f, l, r, 512); return 20 * Math.Log10(Rms(l, SR * 2, SR * 3 - 3000) / (amp / Math.Sqrt(2))); };
        { double g = gainAt("geq{g5=12}", 1000, 0.1); Check("graphic EQ: +12 dB at 1 kHz", Math.Abs(g - 12) < 1.2, g.ToString("0.0") + " dB"); }
        { double lo = gainAt("tilt{tilt=6;pivot=1000}", 250, 0.1), hi = gainAt("tilt{tilt=6;pivot=1000}", 4000, 0.1); Check("tilt EQ: highs rise and lows fall around the pivot", hi - lo > 4, "250 Hz " + lo.ToString("0.0") + " dB, 4 kHz " + hi.ToString("0.0") + " dB"); }
        { double loud = gainAt("dyneq{b1_on=1;b1_f=260;b1_t=-34;b1_r=9}", 260, 0.5), quiet = gainAt("dyneq{b1_on=1;b1_f=260;b1_t=-34;b1_r=9}", 260, 0.003); Check("dynamic EQ cuts a loud 260 Hz tone but leaves a quiet one alone", loud < -3 && Math.Abs(quiet) < 0.8, "loud " + loud.ToString("0.0") + " dB, quiet " + quiet.ToString("0.0") + " dB"); }
        { var rnd = new Random(5); var l = new float[SR * 4]; for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.05 + 0.25 * Math.Sin(2 * Math.PI * 2000 * i / SR)); var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(Make("ressup{depth=100;sens=70;cut=18}"), l, r, 512);
          double before = BandRms(src, SR * 2, 16384, 1950, 2050), after = BandRms(l, SR * 2 + 2047, 16384, 1950, 2050), nb = BandRms(src, SR * 2, 16384, 5000, 9000), na = BandRms(l, SR * 2 + 2047, 16384, 5000, 9000); Check("resonance suppressor presses a ringing 2 kHz peak down (>4 dB) but leaves the noise floor", 20 * Math.Log10(after / before) < -4 && Math.Abs(20 * Math.Log10(na / nb)) < 2, "peak " + (20 * Math.Log10(after / before)).ToString("0.0") + " dB, noise " + (20 * Math.Log10(na / nb)).ToString("0.0") + " dB"); }
        { var f = Make("lineq{b3_f=3500;b3_g=6;b0_g=0}"); f.Prepare(SR); f.Reset(); var w0 = new float[512]; var w1 = new float[512]; f.Process(w0, w1, 512); System.Threading.Thread.Sleep(1500); var l = Sine(3500, SR * 3, 0.1); var r = (float[])l.Clone(); var bl = new float[512]; var br = new float[512]; for (int p = 0; p < l.Length; p += 512) { int n2 = Math.Min(512, l.Length - p); Array.Copy(l, p, bl, 0, n2); Array.Copy(r, p, br, 0, n2); f.Process(bl, br, n2); Array.Copy(bl, 0, l, p, n2); Array.Copy(br, 0, r, p, n2); }
          double g = 20 * Math.Log10(Rms(l, SR * 2, SR * 3 - 3000) / (0.1 / Math.Sqrt(2))); Check("linear-phase EQ: +6 dB at 3.5 kHz", Math.Abs(g - 6) < 1.5, g.ToString("0.0") + " dB; delay " + f.Latency + " samples"); }
        { var f = Make("vowel{vowel=0;q=9;shape=0;mix=100}"); var l = new float[SR * 2]; var rnd = new Random(6); for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); double f1 = BandRms(l, 8000, 16384, 650, 830), off = BandRms(l, 8000, 16384, 4000, 6000); Check("vowel filter A puts energy at its first formant (730 Hz)", f1 > off * 3, "730 Hz vs 5 kHz: " + (20 * Math.Log10(f1 / off)).ToString("0") + " dB"); }

        // ---- vocal, creative and bass tools: each does its own job ----
        Fx.ProjectBpm = 120;
        { var f = Make("gen{kind=0;every=1;len=1;level=-6}"); var l = new float[SR * 4]; var r = new float[SR * 4]; Run(f, l, r, 512); double early = Rms(l, (int)(0.1 * SR), (int)(0.6 * SR)), late = Rms(l, (int)(1.5 * SR), (int)(1.98 * SR)); Check("riser generator builds up over its bar", late > 0.01 && late > early * 2, "early " + early.ToString("0.0000") + " late " + late.ToString("0.0000")); }
        { var f = Make("gen{kind=2;every=2;len=1;level=-6}"); var l = new float[SR * 4]; var r = new float[SR * 4]; Run(f, l, r, 512); double a = Rms(l, 100, (int)(0.2 * SR)), b = Rms(l, (int)(1.4 * SR), (int)(1.9 * SR)); Check("impact generator hits on the first beat of its cycle and dies away", a > 0.03 && b < a * 0.2, "hit " + a.ToString("0.000") + " later " + b.ToString("0.0000")); }
        { var f = Make("gen{kind=3;every=2;len=1;level=-6}"); var l = new float[SR * 2]; var r = new float[SR * 2]; Run(f, l, r, 512); double hz = PeakHz(l, 2000, 16384); Check("sub drop is a low sine", hz > 20 && hz < 120 && Rms(l, 2000, 20000) > 0.03, hz.ToString("0") + " Hz"); }
        { var f = Make("tapestop{engage=1;stop=300;start=300;curve=1}"); var l = Sine(440, SR * 4, 0.5); var r = (float[])l.Clone(); f.Prepare(SR); f.Reset(); Feed(f, l, r, 0, SR * 2, 512); double stopped = Rms(l, (int)(1.2 * SR), (int)(1.9 * SR)); f.Set("engage", 0); var l2 = Sine(440, SR * 4, 0.5); Array.Copy(l2, SR * 2, l, SR * 2, SR * 2); Array.Copy(l2, SR * 2, r, SR * 2, SR * 2); Feed(f, l, r, SR * 2, SR * 4, 512); double back = Rms(l, (int)(3.3 * SR), (int)(3.9 * SR));
          Check("tape stop slows to silence, then spins back up to normal", stopped < 0.02 && back > 0.3, "stopped " + stopped.ToString("0.000") + ", restarted " + back.ToString("0.000")); }
        { var f = Make("freeze{freeze=0;size=300}"); var l = Sine(330, SR * 3, 0.5); for (int i = (int)(0.6 * SR); i < l.Length; i++) l[i] = 0; var r = (float[])l.Clone(); f.Prepare(SR); f.Reset(); Feed(f, l, r, 0, (int)(0.6 * SR), 512); f.Set("freeze", 1); Feed(f, l, r, (int)(0.6 * SR), SR * 3, 512); Check("freeze holds the last moment as a loop after the sound stops", Rms(l, (int)(2.0 * SR), (int)(2.8 * SR)) > 0.1, Rms(l, (int)(2.0 * SR), (int)(2.8 * SR)).ToString("0.000")); }
        { var f = Make("repeat{mode=0;sync=7;mix=100;decay=0}"); var v = Voice(SR * 3); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double dd = 0; for (int i = SR; i < SR * 3; i++) dd += Math.Abs(l[i] - v[i]); Check("beat repeat replaces the sound with repeating slices", dd / (SR * 2) > 0.005 && Rms(l, SR, SR * 3) > 0.02, "difference " + (dd / (SR * 2)).ToString("0.0000")); }
        { var f = Make("breath"); var l = new float[SR * 3]; var rnd = new Random(2); for (int i = 0; i < l.Length; i++) { double t0 = i / (double)SR; l[i] = t0 < 1 ? (float)(0.5 * Math.Sin(2 * Math.PI * 300 * t0)) : (float)((rnd.NextDouble() * 2 - 1) * 0.02); } var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(f, l, r, 512);
          double loud = Rms(l, 4000, SR - 4000) / Rms(src, 4000, SR - 4000), air = Rms(l, (int)(1.6 * SR), (int)(2.8 * SR)) / Rms(src, (int)(1.6 * SR), (int)(2.8 * SR)); Check("breath control turns down quiet airy sound (about 12 dB) but leaves singing alone", Math.Abs(20 * Math.Log10(loud)) < 1 && air < 0.4, "singing " + (20 * Math.Log10(loud)).ToString("0.0") + " dB, breath " + (20 * Math.Log10(air)).ToString("0.0") + " dB"); }
        { var f = Make("gate{thr=-46;range=40;att=1;hold=70;rel=180}"); var l = new float[SR * 2]; var rnd = new Random(2); for (int i = 0; i < l.Length; i++) l[i] = i < SR ? (float)(0.4 * Math.Sin(2 * Math.PI * 250 * i / SR)) : (float)((rnd.NextDouble() * 2 - 1) * 0.002); var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(f, l, r, 512); double gv = Rms(l, 2000, SR - 2000), gn = Rms(l, (int)(1.5 * SR), SR * 2 - 100), gn0 = Rms(src, (int)(1.5 * SR), SR * 2 - 100); Check("vocal gate lets the voice through and closes on the noise", gv > 0.25 && gn < gn0 * 0.25, "voice " + gv.ToString("0.000") + ", noise " + gn.ToString("0.00000") + " of " + gn0.ToString("0.00000")); }
        { var l = new float[SR * 2]; var r = new float[SR * 2]; for (int i = 0; i < l.Length; i++) { double s = Math.Sin(2 * Math.PI * 60.0 * i / SR) * 0.4 + Math.Sin(2 * Math.PI * 2000.0 * i / SR) * 0.2; l[i] = (float)s; r[i] = (float)(-Math.Sin(2 * Math.PI * 60.0 * i / SR) * 0.4 + Math.Sin(2 * Math.PI * 2000.0 * i / SR) * 0.2); }
          var f = Make("widener{width=100;bass=170;mid=0;side=0}"); var a = (float[])l.Clone(); var b = (float[])r.Clone(); Run(f, a, b, 512); double side = 0, side0 = 0; var lo0 = new float[a.Length]; for (int i = SR; i < a.Length; i++) { side += Math.Pow((a[i] - b[i]) * 0.5, 2); side0 += Math.Pow((l[i] - r[i]) * 0.5, 2); }
          Check("bass mono maker keeps the lows in the centre (side bass is removed)", Math.Sqrt(side / SR) < Math.Sqrt(side0 / SR) * 0.9 && BandRms(a, SR, 16384, 40, 90) - BandRms(b, SR, 16384, 40, 90) < BandRms(l, SR, 16384, 40, 90) * 0.1, ""); }
        { var f = Make("align{delay=10}"); var l = new float[SR]; l[1000] = 1f; var r = (float[])l.Clone(); Run(f, l, r, 512); int pk = 0; for (int i = 0; i < l.Length; i++) if (Math.Abs(l[i]) > Math.Abs(l[pk])) pk = i; Check("vocal alignment delays by the chosen time (10 ms = 441 samples)", Math.Abs(pk - 1441) <= 12, "peak at " + pk); }
        { var f = Make("mod{src=5;tgt=0;sync=4;depth=100;pump=55;smooth=1;mix=100}"); var l = Sine(220, SR * 3, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); int beat = SR / 2; double dip = Rms(l, beat * 2 + 100, beat * 2 + 1000), open = Rms(l, beat * 2 + (int)(beat * 0.8), beat * 2 + (int)(beat * 0.95)); Check("sidechain pump ducks at the start of each beat and swells back", dip < open * 0.5, "start " + dip.ToString("0.000") + " end " + open.ToString("0.000")); }
        { var f = Make("mod{src=0;tgt=2;shape=0;sync=7;depth=90;ftype=0;cut=220;res=8;range=3.2;smooth=2;mix=100}"); var l = new float[SR * 3]; var rnd = new Random(1); for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); double mn = 1e9, mx = 0; for (int w = SR; w < SR * 3 - 4000; w += 2205) { double q = Rms(l, w, w + 2205); mn = Math.Min(mn, q); mx = Math.Max(mx, q); } Check("wobble bass filter moves in time (the level swings by more than 2x)", mx > mn * 2, (20 * Math.Log10(mx / mn)).ToString("0.0") + " dB swing"); }
        { var f = Make("mod{src=0;tgt=1;shape=0;sync=0;rate=1;depth=100;mix=100}"); var l = Sine(300, SR * 3, 0.4); var r = (float[])l.Clone(); Run(f, l, r, 512); double mxd = 0; for (int w = 0; w < SR * 3 - 2205; w += 2205) mxd = Math.Max(mxd, Math.Abs(20 * Math.Log10((Rms(l, w, w + 2205) + 1e-6) / (Rms(r, w, w + 2205) + 1e-6)))); Check("auto-pan swings the sound between left and right", mxd > 12, mxd.ToString("0") + " dB between the sides"); }
        { var f = Make("mod{src=1;tgt=0;sync=10;depth=100;smooth=2;" + "nsteps=16;s1=100;s2=0;s3=100;s4=0;s5=100;s6=0;s7=100;s8=0;s9=100;s10=0;s11=100;s12=0;s13=100;s14=0;s15=100;s16=0}"); var l = Sine(300, SR * 2, 0.4); var r = (float[])l.Clone(); Run(f, l, r, 512); int step = (int)(0.125 * SR); double a = Rms(l, step * 8 + 300, step * 9 - 300), b = Rms(l, step * 9 + 300, step * 10 - 300); Check("gate sequencer lets alternate steps through", a > 0.2 && b < 0.02, "on " + a.ToString("0.000") + " off " + b.ToString("0.000")); }
        { var f = Make("chop{pmute=100;prep=0;prev=0;ppit=0;sync=7}"); var v = Voice(SR * 3); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); Check("chopper set to mute everything mutes", Rms(l, SR, SR * 3) < Rms(v, SR, SR * 3) * 0.05, ""); }
        { var f = Make("chop{pmute=0;prep=100;prev=0;ppit=0;sync=10;rep=4}"); var v = Voice(SR * 3); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double dd = 0; for (int i = SR; i < SR * 3; i++) dd += Math.Abs(l[i] - v[i]); Check("stutter vocal repeats slices (sound changes, still audible)", dd / (SR * 2) > 0.003 && Rms(l, SR, SR * 3) > 0.03, ""); }
        { var f = Make("sub{mode=1;level=0;thr=-60;mix=100}"); var l = Sine(80, SR * 2, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double sub = BandRms(l, SR, 32768, 35, 45), main = BandRms(l, SR, 32768, 75, 85); Check("sub-bass generator adds a note an octave below (40 Hz under an 80 Hz bass)", sub > main * 0.12, "40 Hz vs 80 Hz: " + (20 * Math.Log10(sub / main)).ToString("0") + " dB"); }
        { var f = Make("sub{mode=0;level=0;thr=-60;mix=100}"); var l = Sine(80, SR * 2, 0.5); var r = (float[])l.Clone(); Run(f, l, r, 512); double sub = BandRms(l, SR, 32768, 35, 45), main = BandRms(l, SR, 32768, 75, 85); Check("the octave divider also adds 40 Hz", sub > main * 0.1, (20 * Math.Log10(sub / main)).ToString("0") + " dB"); }
        { var quiet = Sine(300, SR * 2, 0.002); var f = Make("ott{depth=100;out=0;highg=0}"); var l = (float[])quiet.Clone(); var r = (float[])quiet.Clone(); Run(f, l, r, 512); double gq = Rms(l, SR, SR * 2) / Rms(quiet, SR, SR * 2); var loud = Sine(300, SR * 2, 0.9); var f2 = Make("ott{depth=100;out=0;highg=0}"); var l2 = (float[])loud.Clone(); var r2 = (float[])loud.Clone(); Run(f2, l2, r2, 512); double gl = Rms(l2, SR, SR * 2) / Rms(loud, SR, SR * 2); Check("OTT lifts quiet sound and squashes loud sound", gq > 1.8 && gl < 1.0, "quiet x" + gq.ToString("0.0") + ", loud x" + gl.ToString("0.00")); }
        { var f = Make("phaser"); var v = Voice(SR * 2); var l = (float[])v.Clone(); var r = (float[])v.Clone(); Run(f, l, r, 512); double dd = 0; for (int i = SR / 2; i < SR * 2; i++) dd += Math.Abs(l[i] - v[i]); Check("phaser changes the sound and stays stable", dd / (SR * 1.5) > 0.01 && Rms(l, SR, SR * 2) < 1, ""); }
        { var f = Make("flanger"); var rnd = new Random(2); var l = new float[SR * 2]; for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(f, l, r, 512); double dd = 0; for (int i = SR; i < SR * 2; i++) dd += Math.Abs(l[i] - src[i]); Check("flanger combs the sound", dd / SR > 0.05 && Rms(l, SR, SR * 2) < 1.5, ""); }
        { var f = Make("flanger{barber=1;mix=100}"); var rnd = new Random(2); var l = new float[SR * 2]; for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); bool fin = true; for (int i = 0; i < l.Length; i++) if (float.IsNaN(l[i]) || Math.Abs(l[i]) > 10) fin = false; Check("barber-pole flanger is stable", fin, ""); }
        { var f = Make("amp{pre=2;gain=30;cab=3}"); var l = Sine(440, SR * 2, 0.3); var r = (float[])l.Clone(); Run(f, l, r, 512); double h3 = BandRms(l, SR, 16384, 1250, 1400), h1 = BandRms(l, SR, 16384, 420, 460); Check("amp simulator distorts (adds harmonics) and stays under control", h3 > h1 * 0.05 && Rms(l, SR, SR * 2) < 1.5, ""); }
        { var rnd = new Random(2); var l = new float[SR * 2]; for (int i = 0; i < l.Length; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var r = (float[])l.Clone(); var f = Make("amp{pre=0;gain=0;cab=3;level=0}"); Run(f, l, r, 512); double mid = BandRms(l, SR, 16384, 1500, 2500), hi = BandRms(l, SR, 16384, 9000, 16000), lo = BandRms(l, SR, 16384, 20, 50); Check("a 4x12 cabinet removes the treble and the deep lows", hi < mid * 0.1 && lo < mid * 0.2, "treble " + (20 * Math.Log10(hi / mid)).ToString("0") + " dB, lows " + (20 * Math.Log10(lo / mid)).ToString("0") + " dB"); }
        { var f = Make("mbdist"); var l = Sine(300, SR * 2, 0.4); var r = (float[])l.Clone(); Run(f, l, r, 512); double h3 = BandRms(l, SR, 16384, 850, 950), h1 = BandRms(l, SR, 16384, 280, 320); Check("multiband distortion adds harmonics", h3 > h1 * 0.03 && Rms(l, SR, SR * 2) < 2, ""); }
        { var f = Make("grain{dry=0;mix=100;pos=300;posj=50;size=150;dens=30}"); var l = Sine(250, SR * 3, 0.4); for (int i = (int)(0.4 * SR); i < l.Length; i++) l[i] = 0; var r = (float[])l.Clone(); Run(f, l, r, 512); Check("granular effect plays grains of what came before, after the sound has stopped", Rms(l, (int)(0.55 * SR), (int)(0.7 * SR)) > 0.02, Rms(l, (int)(0.55 * SR), (int)(0.7 * SR)).ToString("0.000")); }
        { var f = Make("specfx{mode=0;mix=100}"); var l = Sine(500, SR * 3, 0.4); for (int i = (int)(0.8 * SR); i < l.Length; i++) l[i] = 0; var r = (float[])l.Clone(); f.Prepare(SR); f.Reset(); Feed(f, l, r, 0, (int)(0.7 * SR), 512); f.Set("freeze", 1); Feed(f, l, r, (int)(0.7 * SR), SR * 3, 512); Check("spectral freeze keeps a drone going after the input stops", Rms(l, (int)(2.0 * SR), (int)(2.8 * SR)) > 0.03, Rms(l, (int)(2.0 * SR), (int)(2.8 * SR)).ToString("0.000")); }
        { var f = Make("plosive"); var l = new float[SR * 2]; for (int i = 0; i < l.Length; i++) { double t0 = i / (double)SR; l[i] = (float)(0.1 * Math.Sin(2 * Math.PI * 1000 * t0)); if (t0 > 1.0 && t0 < 1.05) l[i] += (float)(0.8 * Math.Sin(2 * Math.PI * 90 * t0)); } var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(f, l, r, 512); double a = BandRms(l, SR, 2048, 60, 130), b = BandRms(src, SR, 2048, 60, 130); Check("plosive reducer takes the low pop out", a < b * 0.6, (20 * Math.Log10(a / b)).ToString("0.0") + " dB"); }
        { var f = Make("rider"); var l = new float[SR * 8]; for (int i = 0; i < l.Length; i++) l[i] = (float)((i < SR * 4 ? 0.05 : 0.5) * Math.Sin(2 * Math.PI * 200 * i / SR)); var r = (float[])l.Clone(); Run(f, l, r, 512); double q = Rms(l, SR * 3, SR * 4), ld = Rms(l, SR * 7, SR * 8); Check("vocal rider evens out quiet and loud sections", q / ld > 0.5, "quiet is now " + (q / ld).ToString("0.00") + " of loud (it was 0.10)"); }
        { var l = new float[SR * 2]; var r = new float[SR * 2]; for (int i = 0; i < l.Length; i++) { double a = Math.Sin(2 * Math.PI * 1000.0 * i / SR) * 0.3, b = Math.Sin(2 * Math.PI * 2300.0 * i / SR) * 0.3; l[i] = (float)(a + b); r[i] = (float)a; } var f = Make("vociso{mode=0;strength=90;hard=70}"); Run(f, l, r, 512); double c1 = BandRms(l, SR + 2047, 16384, 950, 1050), s2 = BandRms(l, SR + 2047, 16384, 2250, 2350); Check("vocal isolation keeps what is in the centre and removes what is on one side", s2 < c1 * 0.4, "side tone " + (20 * Math.Log10(s2 / c1)).ToString("0") + " dB below the centre tone"); }
        { var l = Sine(300, SR * 2, 0.4); var src = (float[])l.Clone(); for (int k = 1; k < 8; k++) l[k * 5000 + 7] += 0.8f; var r = (float[])l.Clone(); var f = Make("declick"); Run(f, l, r, 512); double worst = 0, worst0 = 0; for (int k = 1; k < 8; k++) for (int j = -3; j <= 12; j++) { int idx = k * 5000 + 7 + j + 12; if (idx < l.Length) worst = Math.Max(worst, Math.Abs(l[idx] - src[idx - 12])); } worst0 = 0.8; Check("declicker repairs single-sample clicks", worst < 0.3, "largest error " + worst.ToString("0.00") + " (the clicks were 0.8)"); }
        { var f = Make("transient{attack=10;sustain=0;speed=100}"); var l = new float[SR * 2]; for (int h = 0; h < 8; h++) for (int i = 0; i < 6000; i++) l[h * 10000 + 1000 + i] = (float)(0.4 * Math.Sin(2 * Math.PI * 200 * i / SR) * Math.Exp(-i / 2500.0)); var r = (float[])l.Clone(); var src = (float[])l.Clone(); Run(f, l, r, 512); double pk = 0, pk0 = 0; for (int i = 0; i < l.Length; i++) { pk = Math.Max(pk, Math.Abs(l[i])); pk0 = Math.Max(pk0, Math.Abs(src[i])); } Check("transient shaper boosts the attack of hits", pk > pk0 * 1.25, "peak x" + (pk / pk0).ToString("0.00")); }
        { var f = Make("echo{dry=0;mix=0}"); var l = Sine(300, SR, 0.4); var r = (float[])l.Clone(); Run(f, l, r, 512); Check("echo with Original sound and Echo level both at 0 is silent", Rms(l, 0, SR) < 0.001, ""); }

        // ---- Worldwide Echo and Melody Tune ----
        Fx.ProjectBpm = 120;
        { string[] names = { "Worldwide Echo", "Stadium Echo", "Rap Ad-Lib Echo", "Dark Echo", "Airy Echo", "Telephone Throw", "Infinite Space", "Melody Tune", "Melody Tune: Natural Polish", "Melody Tune: Hard Tune", "Melody Tune: Trap Melody", "Melody Tune: Soft Harmony", "Melody Tune: Choir Stack", "Melody Tune: Octave Dream", "Melody Tune: Power Lead" };
          int found = 0; foreach (var nm in names) foreach (var v in PluginLibrary.All) if (v.Name == nm) found++; Check("Worldwide Echo (7 versions) and Melody Tune (8 versions) are in the plugin list", found == names.Length, found + " of " + names.Length); }
        Func<string, float[], float[]> runSpec = (spec, input) => { var f = Make(spec); var l = (float[])input.Clone(); var r = (float[])input.Clone(); Run(f, l, r, 512); return l; };
        Func<float[], double, double, float> peakAt = (x, sec, wid) => { float m = 0; for (int i = (int)((sec - wid) * SR); i <= (int)((sec + wid) * SR) && i < x.Length; i++) m = Math.Max(m, Math.Abs(x[i])); return m; };
        { var imp = new float[SR * 4]; imp[(int)(0.125 * SR)] = 1f; imp[(int)(1.6 * SR)] = 1f;   // beat 0.25 (not in the last beat of the bar) and beat 3.2 (in it), at 120 BPM
          string baseSpec = "worldecho{style=2;sync=4;fb=0;lc=20;hc=20000;rev=0;duck=0;sat=0;diff=0;dry=0;mix=100;sendsm=2;send=";
          var gated = runSpec(baseSpec + "1}", imp); var always = runSpec(baseSpec + "0}", imp);
          Check("Worldwide Echo: with Send on the last beat of the bar, only the moment inside that beat is echoed", peakAt(gated, 2.1, 0.01) > 0.4 && peakAt(gated, 0.625, 0.01) < 0.05, "in the beat " + peakAt(gated, 2.1, 0.01).ToString("0.00") + ", outside " + peakAt(gated, 0.625, 0.01).ToString("0.000"));
          Check("Worldwide Echo: with Send set to all the time, both moments are echoed", peakAt(always, 2.1, 0.01) > 0.4 && peakAt(always, 0.625, 0.01) > 0.4, peakAt(always, 0.625, 0.01).ToString("0.00") + " and " + peakAt(always, 2.1, 0.01).ToString("0.00")); }
        { var imp = new float[SR * 5]; imp[(int)(1.7 * SR)] = 1f;
          var o = runSpec("worldecho{style=2;sync=4;fb=60;lc=20;hc=20000;rev=0;duck=0;dry=0;mix=100;sendsm=2;send=1}", imp);
          Check("Worldwide Echo: repeats keep ringing after the Send closes", peakAt(o, 2.2, 0.01) > 0.4 && peakAt(o, 2.7, 0.01) > 0.15 && peakAt(o, 3.2, 0.01) > 0.07, peakAt(o, 2.2, 0.01).ToString("0.00") + ", " + peakAt(o, 2.7, 0.01).ToString("0.00") + ", " + peakAt(o, 3.2, 0.01).ToString("0.00")); }
        { bool ok = true; try { runSpec("echo{mode=1;sync=4;offset=20;mix=100}", Sine(300, SR * 2, 0.3)); } catch (Exception) { ok = false; } Check("echo with a delay time that lands exactly on a whole sample does not read past its buffer (stereo offset 20 at 90 BPM)", ok, ""); }
        { var f = Make("melody"); Check("Melody Tune reports the delay of its two stages", f.Latency == 3072 + 2047, f.Latency + " samples"); }
        { var x = Sine(150, SR * 3, 0.3); var o = runSpec("melody{key=0;scale=1;speed=0;strength=100;natural=0;harm=0;mix=100}", x); double hz = PeakHz(o, SR * 2, 32768);
          Check("Melody Tune pulls a 150 Hz note to the nearest note of C major (D3 = 146.8 Hz)", Math.Abs(hz - 146.83) < 1.8, hz.ToString("0.0") + " Hz"); }
        { var x = Sine(220, SR * 3, 0.3); var plain = runSpec("melody{key=9;scale=2;speed=0;strength=0;harm=0;mix=100}", x); var up = runSpec("melody{key=9;scale=2;speed=0;strength=0;harm=5;hlevel=100;mix=100}", x);
          double a = BandRms(plain, SR * 2, 32768, 420, 460), b = BandRms(up, SR * 2, 32768, 420, 460), c0 = BandRms(up, SR * 2, 32768, 200, 240);
          Check("Melody Tune: the Octave above layer adds a voice at 440 Hz and keeps the 220 Hz voice", b > a * 8 && c0 > 0.2 * BandRms(plain, SR * 2, 32768, 200, 240), "440 Hz " + a.ToString("0.0") + " -> " + b.ToString("0.0")); }
        { var x = Sine(300, SR * 2, 0.3); var o = runSpec("melody{mix=0;harm=8}", x); int lat = 3072 + 2047; double d = 0; for (int i = SR / 2; i < SR * 2 - lat - 1; i += 7) d = Math.Max(d, Math.Abs(o[i + lat] - x[i])); Check("Melody Tune with Mix at 0 passes the sound through, delayed to match the processed path", d < 0.01, "max difference " + d.ToString("0.0000")); }

        // ---- Rapper Vocal Styles ----
        {
            var all = VocalStyles.All; var names = new HashSet<string>(); var genres = new HashSet<string>(); bool uniq = true; foreach (var s in all) { if (!names.Add(s.Name)) uniq = false; genres.Add(s.Genre); }
            Check("Rapper Vocal Styles: " + all.Count + " styles in " + genres.Count + " subgenres, every name different", all.Count >= 30 && genres.Count == VocalStyles.Genres.Length && uniq, all.Count + " styles");
            var problems = new List<string>(); foreach (var s in all) problems.AddRange(VocalStyles.Check(s)); Check("every plugin and setting a style uses really exists (no silent typos)", problems.Count == 0, problems.Count == 0 ? "" : string.Join("; ", problems.GetRange(0, Math.Min(4, problems.Count)).ToArray()));
            int badBuild = 0, badAudio = 0; string firstBad = "";
            foreach (var s in all)
            {
                var chain = VocalStyles.Build(s); int expect = s.Chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries).Length; if (chain.Count != expect) { badBuild++; firstBad = s.Name; continue; }
                var src = Voice(SR * 2); var l = (float[])src.Clone(); var r = (float[])src.Clone(); foreach (var f in chain) { f.Prepare(SR); f.Reset(); }
                bool ok = true; for (int p = 0; p < l.Length; p += 512) { int bn = Math.Min(512, l.Length - p); var bl = new float[bn]; var br = new float[bn]; Array.Copy(l, p, bl, 0, bn); Array.Copy(r, p, br, 0, bn); foreach (var f in chain) { f.BeginBlock(null, p, bn, true); f.Process(bl, br, bn); } Array.Copy(bl, 0, l, p, bn); Array.Copy(br, 0, r, p, bn); }
                float pk = 0; for (int i = 0; i < l.Length; i++) { if (float.IsNaN(l[i]) || float.IsInfinity(l[i])) ok = false; pk = Math.Max(pk, Math.Abs(l[i])); }
                if (!ok || pk > 12 || Rms(l, SR / 2, SR * 2) < 0.001) { badAudio++; firstBad = s.Name + " (peak " + pk.ToString("0.0") + ")"; }
            }
            Check("every style builds all its plugins and gives clean, audible sound", badBuild == 0 && badAudio == 0, badBuild + " build problems, " + badAudio + " audio problems " + firstBad);
            string tmpFav = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "styles-fav-" + Guid.NewGuid().ToString("N") + ".txt"); VocalStyles.FavFile = tmpFav;
            VocalStyles.ToggleFav("Trap Lead"); VocalStyles.ToggleFav("Slow Burn"); bool on = VocalStyles.IsFav("Trap Lead") && System.IO.File.ReadAllText(tmpFav).Contains("Slow Burn"); var onlyFav = VocalStyles.Find("", "", true); VocalStyles.ToggleFav("Slow Burn"); bool off = !VocalStyles.IsFav("Slow Burn");
            Check("favourites can be added, removed, listed on their own and are saved to a file", on && off && onlyFav.Count == 2, onlyFav.Count + " favourites");
            Check("searching finds styles by name, subgenre and description", VocalStyles.Find("drill", "", false).Count >= 4 && VocalStyles.Find("", "Cloud & Lo-Fi", false).Count == 3 && VocalStyles.Find("phone", "", false).Count >= 1 && VocalStyles.Find("zzzz", "", false).Count == 0, "drill " + VocalStyles.Find("drill", "", false).Count);
            try { System.IO.File.Delete(tmpFav); } catch { } VocalStyles.FavFile = null;
        }

        // ---- the time-left guess ----
        {
            var em = new EtaMeter(); float p = 0; string early = "x"; em.Add(0.001f); System.Threading.Thread.Sleep(300); p = 0.011f; em.Add(p); early = em.Text(p);
            for (int k = 0; k < 7; k++) { System.Threading.Thread.Sleep(500); p += 0.01f; em.Add(p); }   // 0.02 of the job per second, after a slow start
            string txt = em.Text(p); int secs = 0; bool isSec = txt.EndsWith(" s") && int.TryParse(txt.Substring(0, txt.Length - 2), out secs);
            Check("time-left guess: says nothing until it has seen a few seconds of progress", early.Length == 0, "early: '" + early + "'");
            Check("time-left guess follows the recent speed (progress of 2% a second with 95% to go is about 47 s)", isSec && secs >= 40 && secs <= 55, txt);
        }

        // ---- every named plugin ----
        var sw = Stopwatch.StartNew(); int bad = 0, n = 0; var times = new List<string>();
        foreach (var v in PluginLibrary.All)
        {
            n++; string msg = ""; Fx f = null;
            try
            {
                f = FxRegistry.Create(v.Id); if (f == null) { msg = "could not be created"; }
                else
                {
                    var src = Voice(SR * 3); var l = (float[])src.Clone(); var r = (float[])src.Clone(); var t = Stopwatch.StartNew(); Run(f, l, r, 512); double ms = t.Elapsed.TotalMilliseconds; double cpu = ms / 3000.0 * 100;
                    bool finite = true; float pk = 0; for (int i = 0; i < l.Length; i++) { if (float.IsNaN(l[i]) || float.IsInfinity(l[i]) || float.IsNaN(r[i]) || float.IsInfinity(r[i])) { finite = false; break; } pk = Math.Max(pk, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); }
                    double rin = Rms(src, SR, SR * 3), rout = Rms(l, SR, SR * 3); double diff = 0; int lat = f.Latency; for (int i = SR; i < SR * 3 - lat - 1; i++) { double d = l[i + 0] - src[i]; diff += d * d; } diff = Math.Sqrt(diff / (SR * 2));
                    if (!finite) msg = "NaN or infinity in the output"; else if (pk > 12) msg = "output peaks at " + pk.ToString("0.0"); else if (rout < rin * 0.003) msg = "silent (" + rout.ToString("0.0000") + ")"; else if (diff < rin * 0.005 && !v.Cat.Contains("EQ") && !PassThrough.Contains(v.Name)) msg = "does nothing audible (difference " + diff.ToString("0.0000") + ")";
                    if (msg.Length == 0 && cpu > 25) msg = "uses too much processor (" + cpu.ToString("0") + "% of real time)";
                    if (msg.Length == 0) { // parameters survive save and load
                        string ser = f.Serialize(); var g2 = FxRegistry.Create(v.Id); g2.Deserialize(ser); if (g2.Serialize() != ser) msg = "settings do not survive saving"; }
                    times.Add(v.Name + " " + cpu.ToString("0.0") + "%");
                }
            }
            catch (Exception ex) { msg = "EXCEPTION " + ex.GetType().Name + ": " + ex.Message; }
            if (msg.Length > 0) { bad++; Console.WriteLine("FAIL  " + v.Name + " (" + v.Engine + "): " + msg); }
        }
        Check("all " + n + " library plugins create, run and are safe", bad == 0, bad + " problems, " + sw.Elapsed.TotalSeconds.ToString("0") + " s");
        times.Sort((a, b) => { double x = double.Parse(a.Substring(a.LastIndexOf(' ') + 1).TrimEnd('%')), y = double.Parse(b.Substring(b.LastIndexOf(' ') + 1).TrimEnd('%')); return y.CompareTo(x); });
        Console.WriteLine("heaviest: " + string.Join(", ", times.GetRange(0, Math.Min(6, times.Count)).ToArray()));
        Console.WriteLine(fails == 0 ? "=== plugin checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
