using System;
using System.Collections.Generic;
using System.Linq;

// The MIC modes (Automatic, Standard, Voice Isolation, Wide Spectrum) on made-up sounds: hum, hiss with speech-like bursts, a DC offset, loud peaks, odd block sizes.   csc /main:MT ... MicTest.cs
static class MT
{
    static int fails; const int SR = 44100;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static float[] Sine(double f, double amp, int n, double phase = 0) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * f * i / SR + phase)); return x; }
    static Random rnd = new Random(7);
    static float[] Noise(double rms, int n) { var x = new float[n]; for (int i = 0; i < n; i++) { double u = 0; for (int k = 0; k < 12; k++) u += rnd.NextDouble(); x[i] = (float)((u - 6.0) * rms); } return x; }   // (the sum of 12 is nearly bell shaped with a spread of 1)
    static float[] Add(params float[][] a) { var y = new float[a[0].Length]; foreach (var s in a) for (int i = 0; i < y.Length; i++) y[i] += s[i]; return y; }
    static double Rms(float[] x, double fromS, double toS) { int a = (int)(fromS * SR), b = Math.Min(x.Length, (int)(toS * SR)); double s = 0; for (int i = a; i < b; i++) s += x[i] * (double)x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(1e-9, v)); }
    // the size of one frequency in a part of the sound (a sine of that size gives its amplitude back)
    static double Mag(float[] x, double f, double fromS, double toS) { int a = (int)(fromS * SR), b = Math.Min(x.Length, (int)(toS * SR)); double re = 0, im = 0; for (int i = a; i < b; i++) { double w = 2 * Math.PI * f * i / SR; re += x[i] * Math.Cos(w); im += x[i] * Math.Sin(w); } return 2 * Math.Sqrt(re * re + im * im) / Math.Max(1, b - a); }
    static double Peak(float[] x) { double p = 0; foreach (float v in x) p = Math.Max(p, Math.Abs(v)); return p; }

    // what the recorder would keep for this sound in this mode, fed in blocks like the sound card does
    static float[] Rec(MicMode mode, float[] x, params int[] blocks)
    {
        var r = new AudioRecorder(); r.SetMode(mode); int p = 0, bi = 0; if (blocks.Length == 0) blocks = new[] { 1024 };
        while (p < x.Length) { int n = Math.Min(blocks[bi++ % blocks.Length], x.Length - p); var pcm = new short[n]; for (int i = 0; i < n; i++) pcm[i] = (short)Math.Max(-32768, Math.Min(32767, Math.Round(x[p + i] * 32768.0))); r.Feed(pcm, n); p += n; }
        int frames; var o = r.Take(out frames); return o;
    }
    // hiss with speech-like bursts (a 1 kHz tone, as loud as a voice) at 1.0 - 1.5 s and 2.0 - 2.5 s, 3 seconds in all
    static float[] Speech(double hiss) { int n = SR * 3; var tone = new float[n]; for (int i = 0; i < n; i++) { double t = i / (double)SR; if ((t >= 1.0 && t < 1.5) || (t >= 2.0 && t < 2.5)) tone[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * t)); } return Add(tone, Noise(hiss, n)); }

    static void Main()
    {
        var all = new[] { MicMode.Automatic, MicMode.Standard, MicMode.VoiceIsolation, MicMode.WideSpectrum };
        // ---- every mode: the take is as long as what was heard, and a sound is not moved in time ----
        var tone2 = Sine(1000, 0.3, SR * 2);
        foreach (var m in all) { var o = Rec(m, tone2); Check(MicProcessor.Name(m) + ": the take is exactly as long as the sound that was heard", o.Length == tone2.Length, o.Length + " of " + tone2.Length); }
        {   // Standard: nothing changes
            var o = Rec(MicMode.Standard, Add(Sine(300, 0.4, SR), Noise(0.01, SR))); double md = 0; var src = new float[SR]; rnd = new Random(7); src = Add(Sine(300, 0.4, SR), Noise(0.01, SR)); rnd = new Random(8); o = Rec(MicMode.Standard, src); for (int i = 0; i < o.Length; i++) md = Math.Max(md, Math.Abs(o[i] - src[i]));
            Check("Standard: the microphone exactly as it is (only the 16 bit rounding)", md < 1.0 / 32768 + 1e-6, "largest difference " + md.ToString("0.000000"));
        }
        {   // timing: the start of a burst lands where it was (also for Voice Isolation, which hears a little ahead)
            var sp = Speech(0.01);
            foreach (var m in all)
            {
                var o = Rec(m, sp); double lvl = Rms(o, 1.1, 1.4); int first = -1; for (int i = 0; i < o.Length - 441; i += 44) { if (Rms(o, i / (double)SR, (i + 441) / (double)SR) > lvl * 0.5) { first = i; break; } }
                Check(MicProcessor.Name(m) + ": the sound starts where it started (no shift in time)", first >= 0 && Math.Abs(first / (double)SR - 0.95) < 0.07, "first loud 10 ms window at " + (first / (double)SR).ToString("0.000") + " s (the burst starts at 1.000 s)");
            }
        }
        // ---- Automatic ----
        {
            var hum = Add(Sine(50, 0.2, SR * 2), Sine(1000, 0.2, SR * 2)); var o = Rec(MicMode.Automatic, hum);
            double h = Db(Mag(o, 50, 0.5, 2) / Mag(hum, 50, 0.5, 2)), v = Db(Mag(o, 1000, 0.5, 2) / Mag(hum, 1000, 0.5, 2));
            Check("Automatic: low rumble and hum are taken out (50 Hz down by " + (-h).ToString("0.0") + " dB) and the voice range is left alone (1 kHz " + v.ToString("+0.0;-0.0") + " dB)", h < -6 && Math.Abs(v) < 1.0, "");
            var sp = Speech(0.01); var q = Rec(MicMode.Automatic, sp); double nIn = Rms(sp, 2.7, 2.95), nOut = Rms(q, 2.7, 2.95), bIn = Rms(sp, 1.1, 1.4), bOut = Rms(q, 1.1, 1.4);
            Check("Automatic: the quiet background is eased down between words (" + (Db(nIn) - Db(nOut)).ToString("0.0") + " dB) and the words keep their level (" + (Db(bOut) - Db(bIn)).ToString("+0.0;-0.0") + " dB)", Db(nIn) - Db(nOut) > 3 && Math.Abs(Db(bOut) - Db(bIn)) < 1.5, "");
        }
        // ---- Voice Isolation ----
        {
            var sp = Speech(0.02); var o = Rec(MicMode.VoiceIsolation, sp); double nIn = Rms(sp, 2.6, 2.9), nOut = Rms(o, 2.6, 2.9), bIn = Rms(sp, 1.1, 1.4), bOut = Rms(o, 1.1, 1.4);
            Check("Voice Isolation: the room noise is taken down hard (" + (Db(nIn) - Db(nOut)).ToString("0.0") + " dB) while the voice stays (" + (Db(bOut) - Db(bIn)).ToString("+0.0;-0.0") + " dB)", Db(nIn) - Db(nOut) > 12 && Math.Abs(Db(bOut) - Db(bIn)) < 3.0, "");
            var rum = Add(Sine(40, 0.3, SR * 2), Sine(1000, 0.2, SR * 2)); var r2 = Rec(MicMode.VoiceIsolation, rum); double h = Db(Mag(r2, 40, 0.8, 2) / Mag(rum, 40, 0.8, 2));
            Check("Voice Isolation: rumble below the voice is cut hard (40 Hz down by " + (-h).ToString("0") + " dB)", h < -15, "");
            var hiss = Add(Sine(1000, 0.2, SR * 2), Sine(14000, 0.2, SR * 2)); var r3 = Rec(MicMode.VoiceIsolation, hiss); double hh = Db(Mag(r3, 14000, 0.8, 2) / Mag(hiss, 14000, 0.8, 2));
            Check("Voice Isolation: the top that is only hiss is cut (14 kHz down by " + (-hh).ToString("0") + " dB)", hh < -10, "");
        }
        // ---- Wide Spectrum ----
        {
            var x = Add(Sine(100, 0.2, SR * 2), Sine(10000, 0.1, SR * 2)); for (int i = 0; i < x.Length; i++) x[i] += 0.1f; var o = Rec(MicMode.WideSpectrum, x);
            double mean = 0; for (int i = SR; i < o.Length; i++) mean += o[i]; mean /= (o.Length - SR); double b = Db(Mag(o, 100, 0.5, 2) / Mag(x, 100, 0.5, 2)), a = Db(Mag(o, 10000, 0.5, 2) / Mag(x, 10000, 0.5, 2));
            Check("Wide Spectrum: the DC offset is removed (mean " + mean.ToString("0.0000") + ")", Math.Abs(mean) < 0.005, "");
            Check("Wide Spectrum: the whole range is kept (100 Hz " + b.ToString("+0.0;-0.0") + " dB) with a little air on top (10 kHz " + a.ToString("+0.0;-0.0") + " dB)", Math.Abs(b) < 1.0 && a > 0.8 && a < 3.5, "");
            var quiet = Noise(0.002, SR * 2); var q = Rec(MicMode.WideSpectrum, quiet); double d = Db(Rms(q, 0.5, 2)) - Db(Rms(quiet, 0.5, 2));
            Check("Wide Spectrum: very quiet sound is not gated away (" + d.ToString("+0.0;-0.0") + " dB)", d > -1.5, "");
        }
        // ---- never above full scale ----
        foreach (var m in new[] { MicMode.Automatic, MicMode.VoiceIsolation, MicMode.WideSpectrum })
        { var loud = Sine(1000, 0.995, SR * 2); loud = Add(loud, Sine(333, 0.0, SR * 2)); var o = Rec(m, loud); Check(MicProcessor.Name(m) + ": a loud peak is rounded off, never clipped (highest " + Peak(o).ToString("0.000") + ")", Peak(o) <= 1.0, ""); }
        // ---- the same sound gives the same result whatever the block size ----
        {
            var sp = Speech(0.01);
            foreach (var m in new[] { MicMode.Automatic, MicMode.VoiceIsolation, MicMode.WideSpectrum })
            {
                var a = Rec(m, sp, 1024); var b = Rec(m, sp, 333, 1024, 17, 512, 1); double md = 0; int len = Math.Min(a.Length, b.Length); for (int i = 0; i < len; i++) md = Math.Max(md, Math.Abs(a[i] - b[i]));
                Check(MicProcessor.Name(m) + ": the block size of the sound card does not change the result", a.Length == b.Length && md < 1e-4, "largest difference " + md.ToString("0.000000") + ", lengths " + a.Length + "/" + b.Length);
            }
        }
        // ---- a very short take, and a second take after the first ----
        foreach (var m in all) { var tiny = Sine(1000, 0.3, 300); var o = Rec(m, tiny); Check(MicProcessor.Name(m) + ": even a take of 300 samples comes back with 300 samples", o.Length == 300, o.Length + ""); }
        {
            var r = new AudioRecorder(); r.SetMode(MicMode.VoiceIsolation); var pcm = new short[1024]; for (int i = 0; i < 20; i++) r.Feed(pcm, 1024); int f1; var t1 = r.Take(out f1);
            r.SetMode(MicMode.VoiceIsolation); for (int i = 0; i < 20; i++) r.Feed(pcm, 1024); int f2; var t2 = r.Take(out f2);
            Check("a second recording starts clean (both takes are the length that was heard)", f1 == 20480 && f2 == 20480, f1 + " and " + f2);
        }
        // ---- the choice is remembered ----
        {
            Prefs.MicMode = 0; Prefs.Parse("micmode", "2"); bool kept = Prefs.MicMode == 2; Prefs.Parse("micmode", "9"); bool refused = Prefs.MicMode == 2; bool saved = Prefs.Lines().Contains("micmode=2");
            Check("the chosen mode is remembered in the settings (and nonsense is refused)", kept && refused && saved, "mode " + Prefs.MicMode); Prefs.MicMode = 0;
            Check("every mode has a name and a short explanation", all.All(m => MicProcessor.Name(m).Length > 3 && MicProcessor.Blurb(m).Length > 20), "");
        }
        Console.WriteLine(fails == 0 ? "=== mic checks passed ===" : "=== " + fails + " FAILED ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
