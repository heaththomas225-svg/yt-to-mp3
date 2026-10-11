using System;
using System.Collections.Generic;

// Headless numeric tests for the Studio DSP. Build:
//   csc /out:StudioTest.exe Studio.Dsp.cs Studio.Pitch.cs StudioTest.cs
static class T
{
    const int SR = 44100; static int pass, fail;

    static void Check(string name, bool ok, string detail)
    {
        if (ok) pass++; else fail++;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + "   " + detail);
    }

    static void Run(Fx fx, float[] l, float[] r, int block)
    {
        fx.Prepare(SR); fx.Reset();
        for (int i = 0; i < l.Length; i += block)
        {
            int n = Math.Min(block, l.Length - i);
            float[] bl = new float[n], br = new float[n];
            Array.Copy(l, i, bl, 0, n); Array.Copy(r, i, br, 0, n);
            fx.Process(bl, br, n);
            Array.Copy(bl, 0, l, i, n); Array.Copy(br, 0, r, i, n);
        }
    }

    static double RmsDb(float[] x, int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i] * x[i]; return 10 * Math.Log10(s / (b - a) + 1e-20); }
    static double PeakAbs(float[] x, int a, int b) { double p = 0; for (int i = a; i < b; i++) p = Math.Max(p, Math.Abs(x[i])); return p; }

    // dominant frequency by zero-crossing (clean periodic signals)
    static double ZeroCrossHz(float[] x, int a, int b)
    {
        int first = -1, last = -1, cnt = 0;
        for (int i = a + 1; i < b; i++)
            if (x[i - 1] <= 0 && x[i] > 0)
            {
                double frac = -x[i - 1] / (x[i] - x[i - 1]); double pos = i - 1 + frac;
                if (first < 0) first = (int)(pos * 1000); last = (int)(pos * 1000); cnt++;
                if (cnt == 1) { zc0 = pos; } zc1 = pos; zcn = cnt;
            }
        return zcn > 1 ? (zcn - 1) * SR / (zc1 - zc0) : 0;
    }
    static double zc0, zc1; static int zcn;

    static double MagAt(float[] x, int a, int len, double f)
    {
        double re = 0, im = 0; for (int i = 0; i < len; i++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / len); double ph = 2 * Math.PI * f * i / SR; re += x[a + i] * w * Math.Cos(ph); im -= x[a + i] * w * Math.Sin(ph); }
        return 20 * Math.Log10(Math.Sqrt(re * re + im * im) / len * 4 + 1e-12);
    }

    static float[] Sine(double f, double amp, int n, double ph0 = 0) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * f * i / SR + ph0)); return x; }

    // harmonic "voice-like" signal with optional vibrato (cents) and constant base frequency
    static float[] Voice(double f, int n, double vibCents, double vibHz)
    {
        var x = new float[n]; double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double fi = f * Math.Pow(2, vibCents * Math.Sin(2 * Math.PI * vibHz * i / SR) / 1200.0);
            ph += 2 * Math.PI * fi / SR; double v = 0;
            for (int h = 1; h <= 10; h++) v += Math.Sin(h * ph) / h;
            x[i] = (float)(0.25 * v);
        }
        return x;
    }

    static float[] Copy(float[] a) { return (float[])a.Clone(); }

    static void PitchTest(string name, double inHz, int key, int scale, double speed, double strength, double natural, double expectHz, double tolHz)
    {
        int n = SR * 4; var l = Voice(inHz, n, 0, 5); var r = Copy(l);
        var fx = new PitchCorrector(); fx.Set("key", key); fx.Set("scale", scale); fx.ApplyScale(); fx.Set("speed", (float)speed); fx.Set("strength", (float)strength); fx.Set("natural", (float)natural);
        Run(fx, l, r, 512);
        double hz = ZeroCrossHz(l, SR * 2, SR * 3 + 20000);
        Check(name, Math.Abs(hz - expectHz) <= tolHz, "in " + inHz + " Hz -> out " + hz.ToString("0.00") + " Hz (want " + expectHz.ToString("0.00") + ")");
    }

    static void Main()
    {
        // ---------- pitch correction ----------
        double A4 = 440;
        PitchTest("pitch: 450 Hz -> A4 (A major, hard)", 450, 9, 1, 0, 100, 0, A4, 2.0);
        PitchTest("pitch: 330 Hz -> E4 (A major)", 330, 9, 1, 0, 100, 0, 329.63, 1.5);
        PitchTest("pitch: 475 Hz -> B4 (A major snaps up)", 475, 9, 1, 0, 100, 0, 493.88, 3.0);
        PitchTest("pitch: 460 Hz -> A4 (A major, nearer than B)", 460, 9, 1, 0, 100, 0, 440.0, 2.0);
        PitchTest("pitch: 460 Hz -> A#4 (chromatic)", 460, 0, 0, 0, 100, 0, 466.16, 2.5);
        PitchTest("pitch: strength 0 leaves pitch alone", 450, 9, 1, 0, 0, 0, 450, 2.0);
        PitchTest("pitch: 50% strength lands halfway (cents)", 452.4, 9, 1, 0, 50, 0, 446.1, 2.5);
        {   // natural=100 keeps the vibrato, hard removes it
            int n = SR * 5; var a = Voice(446, n, 40, 5.5); var b = Copy(a); var a2 = Copy(a); var b2 = Copy(a);
            var fn = new PitchCorrector(); fn.Set("key", 9); fn.Set("scale", 1); fn.ApplyScale(); fn.Set("speed", 80); fn.Set("natural", 100);
            var fh = new PitchCorrector(); fh.Set("key", 9); fh.Set("scale", 1); fh.ApplyScale(); fh.Set("speed", 0); fh.Set("natural", 0);
            Run(fn, a, b, 512); Run(fh, a2, b2, 512);
            double dn = CentsSpread(a), dh = CentsSpread(a2);
            Check("pitch: natural mode keeps vibrato, hard flattens it", dn > dh * 2 && dn > 12, "cents spread natural " + dn.ToString("0.0") + " vs hard " + dh.ToString("0.0"));
        }

        // ---------- EQ ----------
        {
            int n = SR * 2; var l = Sine(1000, 0.1, n); var r = Copy(l); var fx = new ParametricEq();
            fx.Set("b2_g", 6); fx.Set("b2_f", 1000); fx.Set("b2_q", 1.0f);
            Run(fx, l, r, 512); double g = RmsDb(l, SR, n) - RmsDb(Sine(1000, 0.1, n), SR, n);
            Check("eq: +6 dB bell at 1 kHz", Math.Abs(g - 6) < 0.3, "gain " + g.ToString("0.00") + " dB");
            var lo = Sine(100, 0.1, n); var lo2 = Copy(lo); var fh = new ParametricEq(); fh.Set("hp_on", 1); fh.Set("hp_f", 1000); fh.Set("hp_s", 1);
            Run(fh, lo, lo2, 512); double att = RmsDb(lo, SR, n) - RmsDb(Sine(100, 0.1, n), SR, n);
            Check("eq: 24 dB/oct high-pass @1k cuts 100 Hz", att < -35, "attenuation " + att.ToString("0.0") + " dB");
            var fresp = new ParametricEq(); fresp.Prepare(SR); fresp.Set("b2_g", 6);
            Check("eq: response curve matches (peak ~ +6 dB)", Math.Abs(fresp.ResponseDb(1000) - 6) < 0.2, "curve " + fresp.ResponseDb(1000).ToString("0.00"));
        }

        // ---------- compressor ----------
        {
            int n = SR * 3; var l = Sine(1000, 0.5, n); var r = Copy(l); var fx = new Compressor();
            fx.Set("thr", -20); fx.Set("ratio", 4); fx.Set("knee", 0); fx.Set("att", 1); fx.Set("rel", 100);
            Run(fx, l, r, 512);
            double outDb = RmsDb(l, SR * 2, n); double inDb = RmsDb(Sine(1000, 0.5, n), SR * 2, n);
            // input peak -6.02 dBFS: expected reduction = (-6.02 - -20) * (1 - 1/4) = 10.5 dB
            Check("comp: ~10.5 dB reduction at 4:1", Math.Abs((inDb - outDb) - 10.5) < 0.8, "reduction " + (inDb - outDb).ToString("0.00") + " dB, meter " + fx.GR.ToString("0.0"));
        }

        // ---------- multiband flatness ----------
        {
            int n = SR * 2; bool ok = true; string d = "";
            foreach (double f in new double[] { 60, 150, 400, 1200, 5000, 12000 })
            {
                var l = Sine(f, 0.1, n); var r = Copy(l); var fx = new MultibandComp();
                for (int b = 0; b < 3; b++) { fx.Set("t" + b, 0); fx.Set("r" + b, 1); }
                Run(fx, l, r, 512); double g = RmsDb(l, SR, n) - RmsDb(Sine(f, 0.1, n), SR, n);
                d += f + "Hz:" + g.ToString("0.00") + " "; if (Math.Abs(g) > 0.35) ok = false;
            }
            Check("multiband: crossovers sum flat (no compression)", ok, d);
        }

        // ---------- limiter ----------
        {
            int n = SR * 2; var l = new float[n]; var rnd = new Random(3);
            for (int i = 0; i < n; i++) l[i] = (float)((rnd.NextDouble() * 2 - 1) * (i % 8000 < 400 ? 2.2 : 0.6));
            var r = Copy(l); var fx = new Limiter(); fx.Set("ceil", -1);
            Run(fx, l, r, 512);
            double pk = Math.Max(PeakAbs(l, 4000, n), PeakAbs(r, 4000, n));
            Check("limiter: output never exceeds -1 dB ceiling", pk <= 0.8913 * 1.002, "peak " + pk.ToString("0.0000") + " (ceiling 0.8913)  latency " + fx.Latency + " smp");
        }

        // ---------- exciter ----------
        {
            int n = SR * 2; var l = Sine(4000, 0.2, n); var r = Copy(l); var fx = new Exciter(); fx.Set("drive", 16); fx.Set("harm", 100); fx.Set("mix", 100);
            Run(fx, l, r, 512);
            double h3 = MagAt(l, SR, 8192, 12000), h3in = MagAt(Sine(4000, 0.2, n), SR, 8192, 12000);
            Check("exciter: generates harmonics (12 kHz)", h3 - h3in > 25, "12k: " + h3in.ToString("0.0") + " -> " + h3.ToString("0.0") + " dB");
            var l2 = Sine(1000, 0.2, n); var r2 = Copy(l2); Run(fx, l2, r2, 512);
            Check("exciter: leaves sub-range (1 kHz) mostly untouched", Math.Abs(RmsDb(l2, SR, n) - RmsDb(Sine(1000, 0.2, n), SR, n)) < 1.0, "delta " + (RmsDb(l2, SR, n) - RmsDb(Sine(1000, 0.2, n), SR, n)).ToString("0.00"));
        }

        // ---------- widener / de-esser / ducker / denoise / delay / reverb ----------
        {
            int n = SR; var l = Sine(500, 0.3, n); var r = Sine(500, 0.3, n, 1.2); var fx = new Widener(); fx.Set("width", 0); fx.Set("bass", 0);
            Run(fx, l, r, 512); double diff = 0; for (int i = 0; i < n; i++) diff += Math.Abs(l[i] - r[i]);
            Check("widener: width 0 = mono", diff / n < 1e-4, "L-R " + (diff / n).ToString("0.000000"));
        }
        {
            int n = SR * 2; var l = Sine(7000, 0.3, n); var r = Copy(l); var fx = new DeEsser(); fx.Set("freq", 7000); fx.Set("thr", -30); fx.Set("range", 12);
            Run(fx, l, r, 512); double red = RmsDb(Sine(7000, 0.3, n), SR, n) - RmsDb(l, SR, n);
            var l2 = Sine(500, 0.3, n); var r2 = Copy(l2); Run(fx, l2, r2, 512); double keep = RmsDb(Sine(500, 0.3, n), SR, n) - RmsDb(l2, SR, n);
            Check("de-esser: cuts 7 kHz, spares 500 Hz", red > 6 && Math.Abs(keep) < 1.0, "7k reduced " + red.ToString("0.0") + " dB, 500 Hz changed " + keep.ToString("0.00") + " dB");
        }
        {
            int n = SR * 2; var l = Sine(800, 0.3, n); var r = Copy(l); var sc = Sine(600, 0.5, n); var fx = new Ducker(); fx.Set("amount", 9); fx.Set("band", 0);
            fx.ScL = sc; fx.ScR = Copy(sc);
            Run(fx, l, r, n);   // one block so sidechain matches
            double red = RmsDb(Sine(800, 0.3, n), SR, n) - RmsDb(l, SR, n);
            Check("ducker: sidechain lowers the beat", red > 6, "reduced " + red.ToString("0.0") + " dB");
        }
        {
            int n = SR * 6; var rnd = new Random(7); var l = new float[n]; var r = new float[n];
            for (int i = 0; i < n; i++) { double nz = (rnd.NextDouble() * 2 - 1) * 0.01; double s = i > SR * 3 ? 0.2 * Math.Sin(2 * Math.PI * 1000 * i / SR) : 0; l[i] = (float)(nz + s); r[i] = l[i]; }
            var fx = new NoiseReduction(); fx.Set("amount", 24); Run(fx, l, r, 512);
            double noiseBefore = 20 * Math.Log10(0.01 / Math.Sqrt(3)), noiseAfter = RmsDb(l, SR * 2, SR * 3 - 2000);
            Check("denoise: reduces steady noise", noiseBefore - noiseAfter > 8, "noise " + noiseBefore.ToString("0.0") + " -> " + noiseAfter.ToString("0.0") + " dBFS");
            double tone = RmsDb(l, SR * 4, SR * 5) - RmsDb(Sine(1000, 0.2, SR), 0, SR);
            Check("denoise: keeps the signal", Math.Abs(tone) < 3, "tone change " + tone.ToString("0.0") + " dB");
        }
        {
            int n = SR * 2; var l = new float[n]; var r = new float[n]; l[0] = 1; r[0] = 1; var fx = new Delay(); fx.Set("mode", 1); fx.Set("time", 250); fx.Set("fb", 0); fx.Set("mix", 100); fx.Set("lc", 20); fx.Set("hc", 20000);
            Run(fx, l, r, 512); int idx = 0; double mx = 0; for (int i = 100; i < n; i++) if (Math.Abs(l[i]) > mx) { mx = Math.Abs(l[i]); idx = i; }
            Check("delay: echo arrives at 250 ms", Math.Abs(idx - 0.25 * SR) < 40, "echo at " + (idx * 1000.0 / SR).ToString("0.0") + " ms");
        }
        {
            int n = SR * 3; var l = new float[n]; var r = new float[n]; l[0] = 1; r[0] = 1; var fx = new Reverb(); fx.Set("mix", 50);
            Run(fx, l, r, 512); double tail = RmsDb(l, SR / 2, SR), early = RmsDb(l, 1000, 6000); bool finite = true; foreach (var v in l) if (float.IsNaN(v) || float.IsInfinity(v)) finite = false;
            Check("reverb: produces a decaying tail, stays finite", finite && tail > -90 && tail < early, "tail " + tail.ToString("0.0") + " dB, early " + early.ToString("0.0") + " dB");
        }
        {
            bool fin = true; foreach (var t in new[] { "chorus", "sat", "dist", "gate", "gain" })
            {
                int n = SR; var l = Sine(300, 0.5, n); var r = Copy(l); var fx = FxRegistry.Create(t); Run(fx, l, r, 512);
                foreach (var v in l) if (float.IsNaN(v) || float.IsInfinity(v)) fin = false;
            }
            Check("chorus / saturation / distortion / gate / gain: finite output", fin, "");
        }

        EngineTests();
        MixTests();
        PluginTests();

        Console.WriteLine();
        Console.WriteLine("=== " + pass + " passed, " + fail + " failed ===");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static int PeakIndex(float[] x) { int bi = 0; float bv = 0; for (int i = 0; i < x.Length; i++) if (Math.Abs(x[i]) > bv) { bv = Math.Abs(x[i]); bi = i; } return bi; }

    static void EngineTests()
    {
        string ffdir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "tools", "ffmpeg");
        string ff = null;
        if (System.IO.Directory.Exists(ffdir)) foreach (var f in System.IO.Directory.GetFiles(ffdir, "ffmpeg.exe", System.IO.SearchOption.AllDirectories)) { ff = f; break; }
        StudioEnv.Ffmpeg = delegate { return ff; };

        // ---- loudness ----
        {
            int n = SR * 8; var l = Sine(1000, 0.1, n); var r = Copy(l); var res = Loudness.Measure(l, r, SR, 0, n);
            Check("loudness: -20 dBFS stereo 1 kHz sine = -20 LUFS", Math.Abs(res.I - (-20.0)) < 0.2, "measured " + res.I.ToString("0.00") + " LUFS");
            var tl = new float[SR]; for (int i = 0; i < tl.Length; i++) tl[i] = (float)(Math.Sin(2 * Math.PI * 11025 * i / SR + Math.PI / 4));
            var tr = Copy(tl); var rt = Loudness.Measure(tl, tr, SR, 0, tl.Length);
            Check("true peak: catches inter-sample peak (sample peak -3 dB)", rt.TruePeakDb > -0.8 && rt.PeakDb < -2.5, "sample " + rt.PeakDb.ToString("0.00") + " dB, true " + rt.TruePeakDb.ToString("0.00") + " dB");
        }

        // ---- decode through ffmpeg ----
        if (ff != null)
        {
            string wav = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mastertest", "Test Song.wav");
            if (System.IO.File.Exists(wav))
            {
                var c = AudioClip.FromFile(wav);
                Check("decode: ffmpeg loads a WAV as 44.1k stereo float", Math.Abs(c.Length - SR * 20) < 2000, "frames " + c.Length + " (~20 s)");
            }
            // write a 24-bit wav and read it back through ffmpeg
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "stu_rt.wav");
            var s1 = Sine(440, 0.5, SR * 2); WavIO.Write(tmp, s1, s1, SR, 24); var back = AudioClip.FromFile(tmp);
            double e = 0; for (int i = 4000; i < 80000; i++) e = Math.Max(e, Math.Abs(back.L[i] - s1[i]));
            Check("export: 24-bit WAV round-trips", e < 0.0002, "max error " + e.ToString("0.000000")); System.IO.File.Delete(tmp);
        }

        // ---- engine: sync / solo / mute / fader / PDC ----
        {
            var eng = new StudioEngine(); foreach (var f in eng.MasterFx) f.Bypass = true;
            int n = SR * 3; var voc = new float[n]; var beat = new float[n]; voc[SR] = 0.8f; beat[SR] = 0.8f;
            var vt = StudioEngine.NewTrack("Vocals", "vocal", AudioClip.FromArrays(voc, Copy(voc), "v")); var bt = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(beat, Copy(beat), "b"));
            eng.Tracks.Add(vt); eng.Tracks.Add(bt);
            var pc = new PitchCorrector(); pc.Prepare(SR); vt.Fx.Add(pc);        // 3072-sample latency
            var o = new RenderOpts(); float[] oL, oR;
            eng.Render(0, n, o, null, out oL, out oR);
            // two impulses (vocal path delayed by PDC, beat path delayed to match) must land together
            int cnt = 0, first = -1; for (int i = 0; i < n; i++) if (Math.Abs(oL[i]) > 0.3f) { cnt++; if (first < 0) first = i; }
            Check("sync: plugin latency is compensated (impulses align)", first >= 0 && Math.Abs(first - SR) <= 2, "output impulse at frame " + first + " (expected " + SR + "), latency " + eng.RenderLatency(o));
            // solo vocal only
            vt.Solo = true; eng.Render(0, n, o, null, out oL, out oR); double pk = 0; for (int i = 0; i < n; i++) pk = Math.Max(pk, Math.Abs(oL[i]));
            Check("solo: only the vocal remains", pk > 0.3 && pk < 1.0, "peak " + pk.ToString("0.00")); vt.Solo = false;
            bt.Mute = true; vt.Mute = true; eng.Render(0, n, o, null, out oL, out oR); pk = 0; for (int i = 0; i < n; i++) pk = Math.Max(pk, Math.Abs(oL[i]));
            Check("mute: silence when everything is muted", pk < 1e-6, "peak " + pk); vt.Mute = false; bt.Mute = false;
            // fader -6 dB and pan
            var sine = Sine(1000, 0.4, n); var solo = new StudioEngine(); foreach (var f in solo.MasterFx) f.Bypass = true;
            var tt = StudioEngine.NewTrack("T", "other", AudioClip.FromArrays(sine, Copy(sine), "s")); tt.VolDb = -6; tt.Pan = 1; solo.Tracks.Add(tt);
            solo.Render(0, n, o, null, out oL, out oR);
            Check("fader/pan: -6 dB and hard right silences left", RmsDb(oL, SR, n) < -90 && Math.Abs((RmsDb(oR, SR, n) - RmsDb(sine, SR, n)) + 6.02) < 0.1, "L " + RmsDb(oL, SR, n).ToString("0.0") + " dB, R delta " + (RmsDb(oR, SR, n) - RmsDb(sine, SR, n)).ToString("0.00"));
            // dry render ignores plugins and faders
            var dry = new RenderOpts { Dry = true, Fader = false, Master = false }; solo.Render(0, n, dry, null, out oL, out oR);
            Check("dry render: original audio untouched", Math.Abs(RmsDb(oL, SR, n) - RmsDb(sine, SR, n)) < 0.01, "delta " + (RmsDb(oL, SR, n) - RmsDb(sine, SR, n)).ToString("0.000"));
        }

        // ---- mastering chain limiter on the master bus ----
        {
            var eng = new StudioEngine(); int n = SR * 4; var rnd = new Random(5); var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = (float)(0.6 * Math.Sin(2 * Math.PI * 80 * i / SR) + 0.5 * (rnd.NextDouble() * 2 - 1));
            eng.Tracks.Add(StudioEngine.NewTrack("M", "other", AudioClip.FromArrays(x, Copy(x), "m")));
            float[] oL, oR; eng.Render(0, n, new RenderOpts(), null, out oL, out oR);
            var res = Loudness.Measure(oL, oR, SR, 0, n);
            Check("master chain: true peak stays under -1 dB", res.TruePeakDb <= -0.8, "true peak " + res.TruePeakDb.ToString("0.00") + " dB, " + res.I.ToString("0.0") + " LUFS");
        }

        // ---- real-time device ----
        try
        {
            var eng = new StudioEngine(); var s1 = Sine(440, 0.2, SR * 3);
            eng.Tracks.Add(StudioEngine.NewTrack("T", "other", AudioClip.FromArrays(s1, Copy(s1), "t")));
            eng.Play(); System.Threading.Thread.Sleep(1500); long p = eng.Pos; eng.Stop(); eng.Shutdown();
            Check("realtime: audio device plays and the playhead advances", p > SR * 1.0 && p < SR * 2.2, "position after 1.5 s = " + (p / (double)SR).ToString("0.00") + " s");
        }
        catch (Exception ex) { Console.WriteLine("SKIP  realtime device test: " + ex.Message); }
    }

    static void PluginTests()
    {
        // ---- 8D audio tuner ----
        {
            int n = SR * 2; var fx = new Spatial8D(); fx.Set("hold", 1); fx.Set("angle", 90); fx.Set("room", 0); fx.Set("bass", 0); fx.Set("mix", 100);
            var l = Sine(1000, 0.3, n); var r = Copy(l); Run(fx, l, r, 512);
            double gl = RmsDb(l, SR / 2, n), gr = RmsDb(r, SR / 2, n);
            Check("8D: a sound parked on the right is louder in the right ear", gr - gl > 6, "right " + gr.ToString("0.0") + " dB vs left " + gl.ToString("0.0") + " dB");
            // the far (left) ear hears it later: cross-correlate
            int best = 0; double bv = -1; for (int lag = 0; lag < 60; lag++) { double s = 0; for (int i = SR; i < SR + 4000; i++) s += l[i + lag] * r[i]; if (s > bv) { bv = s; best = lag; } }
            Check("8D: the far ear is delayed (interaural time difference)", best >= 10 && best <= 40, "left lags right by " + best + " samples = " + (best * 1000.0 / SR).ToString("0.00") + " ms");
            var f2 = new Spatial8D(); f2.Set("hold", 1); f2.Set("angle", 180); f2.Set("room", 0); f2.Set("bass", 0); var a = Sine(8000, 0.3, n); var b = Copy(a); Run(f2, a, b, 512);
            var f3 = new Spatial8D(); f3.Set("hold", 1); f3.Set("angle", 0); f3.Set("room", 0); f3.Set("bass", 0); var c = Sine(8000, 0.3, n); var d = Copy(c); Run(f3, c, d, 512);
            Check("8D: behind you is quieter and darker than in front", RmsDb(c, SR / 2, n) - RmsDb(a, SR / 2, n) > 5, "front " + RmsDb(c, SR / 2, n).ToString("0.0") + " dB, behind " + RmsDb(a, SR / 2, n).ToString("0.0") + " dB (8 kHz)");
            // rotation: left/right balance must swing both ways over one 4 s turn
            int m = SR * 8; var f4 = new Spatial8D(); f4.Set("period", 4); f4.Set("room", 0); f4.Set("bass", 0); var x = Sine(500, 0.3, m); var y = Copy(x); Run(f4, x, y, 512);
            double maxd = -99, mind = 99; for (int w = 0; w < 14; w++) { int s0 = w * (SR / 2) + SR / 4; double dd = RmsDb(y, s0, s0 + SR / 10) - RmsDb(x, s0, s0 + SR / 10); if (dd > maxd) maxd = dd; if (dd < mind) mind = dd; }
            Check("8D: rotation swings the sound between the ears", maxd > 4 && mind < -4, "right-minus-left ranges " + mind.ToString("0.0") + " .. " + maxd.ToString("0.0") + " dB");
        }

        // ---- plugin SDK: compile a real plugin, approve it, load it ----
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_plug_test"); if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            string savedDir = StudioEnv.DataDir; StudioEnv.DataDir = dir;
            try
            {
                string exe = typeof(Fx).Assembly.Location, sdk = PluginHost.WriteSdk(exe); string csc = Environment.GetEnvironmentVariable("WINDIR") + @"\Microsoft.NET\Framework64\v4.0.30319\csc.exe"; int code;
                MasterDsp.RunCapture(csc, "/nologo /target:library /out:\"" + System.IO.Path.Combine(PluginHost.Dir, "SampleTremolo.dll") + "\" /r:\"" + exe + "\" \"" + System.IO.Path.Combine(sdk, "SamplePlugin.cs") + "\"", null, out code);
                Check("plugin SDK: the generated sample plugin compiles", code == 0 && System.IO.File.Exists(System.IO.Path.Combine(PluginHost.Dir, "SampleTremolo.dll")), "exit " + code);
                var list = PluginHost.Scan();
                Check("plugins: found but NOT enabled until you approve", list.Count == 1 && !list[0].Enabled && FxRegistry.Create("sample_tremolo") == null, list.Count + " found, enabled=" + (list.Count > 0 && list[0].Enabled));
                PluginHost.SetEnabled(list[0], true); var log = PluginHost.LoadEnabled();
                var fx = FxRegistry.Create("sample_tremolo");
                Check("plugins: an approved plugin loads and appears in the effect list", fx != null && Array.Exists(FxRegistry.All, i => i.Type == "sample_tremolo"), string.Join("; ", log.ToArray()));
                if (fx != null)
                {
                    fx.Prepare(SR); fx.Set("rate", 5); fx.Set("depth", 100); int n = SR * 2; var l = Sine(440, 0.5, n); var r = Copy(l); Run(fx, l, r, 512);
                    double lo = 99, hi = -99; for (int w = 0; w < 18; w++) { double v = RmsDb(l, SR / 2 + w * 441, SR / 2 + w * 441 + 441); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                    Check("plugins: the loaded plugin really processes audio (tremolo)", hi - lo > 12, "level swings " + (hi - lo).ToString("0.0") + " dB");
                    // it survives a project round trip by type id
                    var eng = new StudioEngine(); var tr = StudioEngine.NewTrack("T", "other", AudioClip.FromArrays(Sine(300, 0.3, SR), Sine(300, 0.3, SR), "t")); tr.Fx.Add(fx); eng.Tracks.Add(tr);
                    string mix = ProjectIO.SerializeMix(eng); var e2 = new StudioEngine(); e2.Tracks.Add(StudioEngine.NewTrack("T", "other", null)); ProjectIO.RestoreMix(e2, mix);
                    Check("plugins: plugin settings are saved in projects and restored", e2.Tracks[0].Fx.Count == 1 && e2.Tracks[0].Fx[0].Type == "sample_tremolo", "restored " + e2.Tracks[0].Fx.Count + " fx");
                }
                // changing the file must revoke approval
                string dll = System.IO.Path.Combine(PluginHost.Dir, "SampleTremolo.dll"); var bytes = System.IO.File.ReadAllBytes(dll); var more = new byte[bytes.Length + 4]; Array.Copy(bytes, more, bytes.Length); System.IO.File.WriteAllBytes(dll + ".tmp", more);
                System.IO.File.Copy(dll + ".tmp", System.IO.Path.Combine(PluginHost.Dir, "Tampered.dll")); var l2 = PluginHost.Scan(); bool tamperedOff = true; foreach (var q in l2) if (q.File == "Tampered.dll" && q.Enabled) tamperedOff = false;
                Check("plugins: a new or changed file is never auto-approved", tamperedOff, "Tampered.dll enabled=" + !tamperedOff);

                // ---- a plugin that crashes must be switched off, not crash the engine ----
                string crash = "public class Crashy : Fx { public Crashy() { Type = \"crashy\"; Title = \"Crashy\"; } public override void Process(float[] l, float[] r, int n) { throw new System.InvalidOperationException(\"boom\"); } }";
                string cs = System.IO.Path.Combine(dir, "crash.cs"); System.IO.File.WriteAllText(cs, crash);
                MasterDsp.RunCapture(csc, "/nologo /target:library /out:\"" + System.IO.Path.Combine(PluginHost.Dir, "Crashy.dll") + "\" /r:\"" + exe + "\" \"" + cs + "\"", null, out code);
                foreach (var q in PluginHost.Scan()) if (q.File == "Crashy.dll") PluginHost.SetEnabled(q, true); PluginHost.LoadEnabled();
                var cf = FxRegistry.Create("crashy"); var eng2 = new StudioEngine(); foreach (var f in eng2.MasterFx) f.Bypass = true;
                var tr2 = StudioEngine.NewTrack("T", "other", AudioClip.FromArrays(Sine(300, 0.3, SR * 2), Sine(300, 0.3, SR * 2), "t")); tr2.Fx.Add(cf); eng2.Tracks.Add(tr2); float[] oL, oR; bool threw = false;
                try { eng2.Render(0, SR * 2, new RenderOpts(), null, out oL, out oR); } catch { threw = true; }
                Check("plugins: a crashing plugin is bypassed and reported, the engine keeps running", cf != null && !threw && cf.Bypass && cf.Error.Contains("boom"), "bypass=" + (cf != null && cf.Bypass) + ", error=\"" + (cf == null ? "" : cf.Error) + "\"");
            }
            finally { StudioEnv.DataDir = savedDir; }
        }
    }
    // synthetic vocal: voiced notes (with formant-ish harmonics) in bursts + a little hiss; and a bass/noise "beat"
    static AudioClip SynthVocal(int seconds)
    {
        int n = SR * seconds; var x = new float[n]; var rnd = new Random(11); double[] notes = { 220, 261.63, 329.63, 293.66, 246.94, 220, 329.63, 261.63 };
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            int seg = i / (SR / 2); bool on = (seg % 4) != 3; double f = notes[seg % notes.Length]; ph += 2 * Math.PI * f / SR;
            double v = 0; for (int h = 1; h <= 12; h++) v += Math.Sin(h * ph) / h;
            double env = on ? Math.Min(1.0, (i % (SR / 2)) / 1500.0) : 0;
            x[i] = (float)(0.22 * v * env + 0.0008 * (rnd.NextDouble() * 2 - 1));
        }
        return AudioClip.FromArrays(x, (float[])x.Clone(), "SynthVocal");
    }
    static AudioClip SynthBeat(int seconds)
    {
        int n = SR * seconds; var l = new float[n]; var r = new float[n]; var rnd = new Random(5);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, kick = Math.Sin(2 * Math.PI * (55 + 80 * Math.Exp(-(t % 0.5) * 30)) * t) * Math.Exp(-(t % 0.5) * 9);
            double hat = ((i % (SR / 4)) < 600 ? (rnd.NextDouble() * 2 - 1) * 0.18 : 0), pad = 0.06 * Math.Sin(2 * Math.PI * 130.8 * t) + 0.05 * Math.Sin(2 * Math.PI * 196 * t);
            l[i] = (float)(0.6 * kick + hat + pad); r[i] = (float)(0.6 * kick + hat * 0.8 + pad * 1.1);
        }
        return AudioClip.FromArrays(l, r, "SynthBeat");
    }

    static void MixTests()
    {
        // ---- every rap chain builds, sounds finite and keeps level sane ----
        {
            bool fin = true; string d = ""; var v = SynthVocal(6);
            foreach (var name in Presets.ChainNames)
            {
                var chain = Presets.BuildChain(name); var l = (float[])v.L.Clone(); var r = (float[])v.R.Clone();
                int lat = 0; foreach (var f in chain) { f.Prepare(SR); f.Reset(); if (!f.Bypass) lat += f.Latency; }
                for (int i = 0; i < l.Length; i += 512) { int n = Math.Min(512, l.Length - i); var a = new float[n]; var b = new float[n]; Array.Copy(l, i, a, 0, n); Array.Copy(r, i, b, 0, n); foreach (var f in chain) if (!f.Bypass) f.Process(a, b, n); Array.Copy(a, 0, l, i, n); Array.Copy(b, 0, r, i, n); }
                foreach (var s in l) if (float.IsNaN(s) || float.IsInfinity(s) || Math.Abs(s) > 20) fin = false;
                d += name + ":" + chain.Count + "fx/" + lat + "smp ";
            }
            Check("presets: all 6 rap chains build and process cleanly", fin, d);
            bool presetOk = true; foreach (var kv in new[] { "eq", "comp", "exciter", "reverb", "delay", "pitch", "limiter", "deess", "duck", "mbcomp", "denoise" }) foreach (var p in Presets.For(kv)) { var f = Presets.Make(kv, p.Value); if (f == null) presetOk = false; }
            Check("presets: every built-in plugin preset loads", presetOk, "");
        }

        // ---- key detection ----
        {
            var v = SynthVocal(24); int key, scale; double conf; bool ok = KeyDetect.Detect(v, out key, out scale, out conf);
            // the melody uses A B? -> notes A3 C4 E4 D4 B3: contained in A minor / C major
            bool right = ok && ((key == 9 && scale == 2) || (key == 0 && scale == 1) || (key == 7 && scale == 1));
            Check("key detection: A-minor melody -> A minor / C major family", right, ok ? PitchCorrector.Keys[key] + (scale == 2 ? " minor" : " major") + " conf " + conf.ToString("0.00") : "no key found");
        }

        // ---- Auto Mix on a synthetic vocal + beat ----
        {
            var eng = new StudioEngine(); string tmp = System.IO.Path.GetTempPath();
            var vc = SynthVocal(14); var bc = SynthBeat(14);
            eng.Tracks.Add(StudioEngine.NewTrack("Vocals", "vocal", vc)); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", bc));
            var rep = AutoMix.Run(eng, "Natural", true, delegate (string s) { });
            Check("auto mix: produces a report and sets plugins", rep.Count >= 6 && eng.Tracks[0].Fx.Count >= 4 && eng.Tracks[1].Fx.Count >= 6, rep.Count + " lines, vocal " + eng.Tracks[0].Fx.Count + " fx, beat " + eng.Tracks[1].Fx.Count + " fx");
            float[] vl, vr, bl, br;
            eng.Render(0, SR * 14, new RenderOpts { Master = false, MuteSolo = false, Only = new List<Track> { eng.Tracks[0] } }, null, out vl, out vr);
            eng.Render(0, SR * 14, new RenderOpts { Master = false, MuteSolo = false, Only = new List<Track> { eng.Tracks[1] } }, null, out bl, out br);
            var lv = Loudness.Measure(vl, vr, SR, 0, vl.Length); var lb = Loudness.Measure(bl, br, SR, 0, bl.Length);
            double diff = lv.I - lb.I;
            Check("auto mix: vocal sits ~2 LU above the beat (Natural)", Math.Abs(diff - 2.0) < 2.5, "vocal " + lv.I.ToString("0.0") + " LUFS, beat " + lb.I.ToString("0.0") + " LUFS, diff " + diff.ToString("0.0"));
            float[] ml, mr; eng.Render(0, SR * 14, new RenderOpts { Master = false }, null, out ml, out mr); var lm = Loudness.Measure(ml, mr, SR, 0, ml.Length);
            Check("auto mix: leaves headroom before mastering", lm.TruePeakDb <= -1.0, "mix true peak " + lm.TruePeakDb.ToString("0.0") + " dB, " + lm.I.ToString("0.0") + " LUFS");
            // original audio untouched
            var refV = SynthVocal(14); double dmax = 0; for (int i = 0; i < vc.Length; i += 97) dmax = Math.Max(dmax, Math.Abs(vc.L[i] - refV.L[i]));
            Check("auto mix: original clips are untouched", dmax == 0, "max diff " + dmax);
            AutoMix.Undo(eng); Check("auto mix: undo restores the previous mixer state", eng.Tracks[0].Fx.Count == 0 && Math.Abs(eng.Tracks[0].VolDb) < 1e-6, "vocal fx after undo: " + eng.Tracks[0].Fx.Count);
        }

        // ---- project save / load round trip with plugin settings and alignment ----
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_proj_test"); System.IO.Directory.CreateDirectory(dir);
            var eng = new StudioEngine(); var v = SynthVocal(5); var b = SynthBeat(5);
            string vp = System.IO.Path.Combine(dir, "v.wav"), bp = System.IO.Path.Combine(dir, "b.wav"); WavIO.Write(vp, v.L, v.R, SR, 24); WavIO.Write(bp, b.L, b.R, SR, 24);
            var tv = StudioEngine.NewTrack("Vocals", "vocal", AudioClip.FromFile(vp)); tv.Offset = 4410; tv.VolDb = -2.5f; tv.Pan = 0.2f;
            var tb = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromFile(bp)); tb.Mute = true;
            tv.Fx.Add(Presets.Make("pitch", "key=9;scale=2;speed=12;strength=88;natural=33")); tv.Fx[0].Bypass = true; tv.Fx.Add(Presets.Make("eq", "b2_g=3.5;b2_f=1234"));
            tb.Fx.Add(Presets.Make("duck", "amount=5.5")); eng.Tracks.Add(tv); eng.Tracks.Add(tb); eng.MasterFx[1].Set("thr", -9.5f); eng.MasterVolDb = -1.5f;
            string pf = System.IO.Path.Combine(dir, "t.ytmproj"); ProjectIO.Save(eng, pf);
            var e2 = new StudioEngine(); var warn = ProjectIO.Load(e2, pf, delegate (string s) { });
            bool same = ProjectIO.SerializeMix(eng) == ProjectIO.SerializeMix(e2) && e2.Tracks.Count == 2 && e2.Tracks[0].Offset == 4410 && e2.Tracks[0].Clip != null && Math.Abs(e2.Tracks[0].Clip.Length - tv.Clip.Length) < 2;
            string sa = ProjectIO.SerializeMix(eng), sb2 = ProjectIO.SerializeMix(e2), firstDiff = ""; { var la = sa.Split((char)10); var lb = sb2.Split((char)10); for (int q = 0; q < Math.Max(la.Length, lb.Length); q++) { string x1 = q < la.Length ? la[q] : "<none>", x2 = q < lb.Length ? lb[q] : "<none>"; if (x1 != x2) { firstDiff = " DIFF: [" + x1 + "] vs [" + x2 + "]"; break; } } }
            Check("project: save + reopen keeps plugins, faders, mute and track alignment" + firstDiff, same && warn.Count == 0, "tracks " + e2.Tracks.Count + ", offset " + e2.Tracks[0].Offset + ", master comp thr " + e2.MasterFx[1].Get("thr"));
        }

        // ---- buses: tracks sent to a bus are mixed and processed there, then go to the master ----
        {
            int n = SR * 2; var eng = new StudioEngine(); eng.MasterBypass = true;
            var c1 = AudioClip.FromArrays(Sine(440, 0.2, n), Sine(440, 0.2, n), "a"); var c2 = AudioClip.FromArrays(Sine(440, 0.2, n), Sine(440, 0.2, n), "b");
            var t1 = StudioEngine.NewTrack("A", "audio", c1); var t2 = StudioEngine.NewTrack("B", "audio", c2); var bus = StudioEngine.NewTrack("Bus 1", "bus", null); bus.IsBus = true;
            eng.Tracks.Add(t1); eng.Tracks.Add(t2); eng.Tracks.Add(bus);
            float[] oL, oR; var ro = new RenderOpts { Master = false };
            eng.Render(0, n, ro, null, out oL, out oR); double direct = RmsDb(oL, SR, n);
            t1.Out = bus; t2.Out = bus; eng.Render(0, n, ro, null, out oL, out oR); double viaBus = RmsDb(oL, SR, n);
            Check("bus: tracks sent to a bus sound the same as going straight to the master", Math.Abs(direct - viaBus) < 0.05, direct.ToString("0.00") + " vs " + viaBus.ToString("0.00") + " dB");
            bus.VolDb = -6; eng.Render(0, n, ro, null, out oL, out oR); double quieter = RmsDb(oL, SR, n);
            Check("bus: the bus fader turns the whole group down", Math.Abs((viaBus - quieter) - 6) < 0.1, (viaBus - quieter).ToString("0.00") + " dB");
            bus.Mute = true; eng.Render(0, n, ro, null, out oL, out oR); Check("bus: muting the bus silences everything sent to it", RmsDb(oL, SR, n) < -80, RmsDb(oL, SR, n).ToString("0") + " dB");
            bus.Mute = false; bus.VolDb = 0; t1.Out = null; eng.Render(0, n, ro, null, out oL, out oR); double half = RmsDb(oL, SR, n);
            Check("bus: one track on the bus and one straight out still add up", Math.Abs(half - direct) < 0.05, half.ToString("0.00"));
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_bus_test"); System.IO.Directory.CreateDirectory(dir); string wp = System.IO.Path.Combine(dir, "a.wav"); WavIO.Write(wp, c1.L, c1.R, SR, 24);
            var e3 = new StudioEngine(); var ta = StudioEngine.NewTrack("A", "audio", AudioClip.FromFile(wp)); var bb = StudioEngine.NewTrack("Bus 1", "bus", null); bb.IsBus = true; var em = StudioEngine.NewTrack("Audio 2", "audio", null); ta.Out = bb; e3.Tracks.Add(ta); e3.Tracks.Add(bb); e3.Tracks.Add(em);
            string pf = System.IO.Path.Combine(dir, "b.ytmproj"); ProjectIO.Save(e3, pf); var e4 = new StudioEngine(); var warn = ProjectIO.Load(e4, pf, delegate (string s) { });
            Check("bus: a project keeps its buses, their routing and empty audio tracks (no missing-file warning)", e4.Tracks.Count == 3 && e4.Tracks[1].IsBus && e4.Tracks[0].Out == e4.Tracks[1] && !e4.Tracks[2].IsBus && e4.Tracks[2].Clip == null && warn.Count == 0, "tracks " + e4.Tracks.Count + ", warnings " + warn.Count);
        }

        // ---- export: formats, sync, dry vs processed ----
        {
            var eng = new StudioEngine(); var v = SynthVocal(6); var b = SynthBeat(6); string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_export_test");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            var tv = StudioEngine.NewTrack("Vocals", "vocal", v); var tb = StudioEngine.NewTrack("Beat", "beat", b); eng.Tracks.Add(tv); eng.Tracks.Add(tb);
            foreach (var f in Presets.BuildChain("Melodic Rap")) tv.Fx.Add(f); foreach (var f in Presets.BeatChain()) tb.Fx.Add(f);
            var plan = Exporter.Plan(eng, true, true, true, true, true, true, true, "Song", 0);
            Check("export: plan lists every requested file", plan.Count == 8, plan.Count + " files (mix x2, vocals x2, beat x2, stems x2)");
            bool allOk = true; string detail = "";
            foreach (var it in plan) { try { string p = Exporter.Write(eng, it, dir, 0, null); long len = new System.IO.FileInfo(p).Length; if (len < 100000) allOk = false; } catch (Exception ex) { allOk = false; detail += ex.Message; } }
            Check("export: every WAV file is written", allOk, detail + System.IO.Directory.GetFiles(dir).Length + " files");
            var dryV = AudioClip.FromFile(System.IO.Path.Combine(dir, "Song - vocals dry.wav")); double dm = 0; for (int i = 5000; i < v.Length - 100000; i += 31) dm = Math.Max(dm, Math.Abs(dryV.L[i] - v.L[i]));
            Check("export: dry vocals are the untouched original audio", dm < 0.0002, "max difference " + dm.ToString("0.000000"));
            var proV = AudioClip.FromFile(System.IO.Path.Combine(dir, "Song - vocals processed.wav")); double dp = 0; for (int i = 5000; i < v.Length - 100000; i += 31) dp = Math.Max(dp, Math.Abs(proV.L[i] - v.L[i]));
            Check("export: processed vocals really contain the effects", dp > 0.02, "max difference from dry " + dp.ToString("0.000"));
            // sync: onset of processed vocal vs dry vocal (first sample above threshold)
            int onD = -1, onP = -1; for (int i = 0; i < v.Length; i++) { if (onD < 0 && Math.Abs(dryV.L[i]) > 0.02f) onD = i; if (onP < 0 && Math.Abs(proV.L[i]) > 0.02f) onP = i; }
            Check("export: processed stem stays time-aligned with the dry stem", onD >= 0 && Math.Abs(onD - onP) < 700, "onset dry " + onD + " vs processed " + onP + " frames");
            if (StudioEnv.Ffmpeg() != null)
            {
                string[] extra = { "flac", "mp3" }; int[] fm = { 3, 4 }; bool encOk = true; string d2 = "";
                for (int k = 0; k < 2; k++) { var one = Exporter.Plan(eng, true, false, false, false, false, false, false, "Enc" + k, fm[k]); try { string p = Exporter.Write(eng, one[0], dir, fm[k], null); var back = AudioClip.FromFile(p); d2 += extra[k] + ":" + (back.Length / (double)SR).ToString("0.0") + "s "; if (back.Length < SR * 6) encOk = false; } catch (Exception ex) { encOk = false; d2 += ex.Message; } }
                Check("export: FLAC and MP3 encode and decode back", encOk, d2);
            }
        }
    }

    // standard deviation of instantaneous pitch (cents) of a periodic signal, from zero crossings
    static double CentsSpread(float[] x)
    {
        var per = new List<double>(); double last = -1;
        for (int i = SR * 2 + 1; i < x.Length - 1000; i++)
            if (x[i - 1] <= 0 && x[i] > 0)
            {
                double pos = i - 1 + (-x[i - 1] / (x[i] - x[i - 1]));
                if (last > 0) per.Add(1200 * Math.Log(SR / (pos - last)) / Math.Log(2));
                last = pos;
            }
        if (per.Count < 10) return 0;
        double m = 0; foreach (var c in per) m += c; m /= per.Count; double v = 0; foreach (var c in per) v += (c - m) * (c - m);
        return Math.Sqrt(v / per.Count);
    }
}
