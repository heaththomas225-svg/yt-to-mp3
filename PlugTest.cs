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
                    if (!finite) msg = "NaN or infinity in the output"; else if (pk > 12) msg = "output peaks at " + pk.ToString("0.0"); else if (rout < rin * 0.003) msg = "silent (" + rout.ToString("0.0000") + ")"; else if (diff < rin * 0.005 && !v.Cat.Contains("EQ")) msg = "does nothing audible (difference " + diff.ToString("0.0000") + ")";
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
