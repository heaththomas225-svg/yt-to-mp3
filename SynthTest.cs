using System;
using System.Collections.Generic;

// Tests the Bass Lab synthesizer and the Bass Boost effect.   csc /main:Y ... SynthTest.cs
static class Y
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }

    static float[][] Render(SynthFx fx, int[] notes, int frames, int noteOffAt)
    {
        var l = new float[frames]; var r = new float[frames]; int pos = 0;
        foreach (var n in notes) fx.NoteOn(n, 110);
        while (pos < frames)
        {
            int c = Math.Min(1024, frames - pos); var bl = new float[c]; var br = new float[c];
            if (noteOffAt >= 0 && pos <= noteOffAt && noteOffAt < pos + c) foreach (var n in notes) fx.NoteOff(n);
            fx.BeginBlock(null, pos, c, false); fx.Process(bl, br, c); Array.Copy(bl, 0, l, pos, c); Array.Copy(br, 0, r, pos, c); pos += c;
        }
        return new[] { l, r };
    }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    static double Hz(float[] x, int a, int n) { int z = 0; for (int i = a + 1; i < a + n; i++) if ((x[i - 1] < 0) != (x[i] < 0)) z++; return z / 2.0 / (n / 44100.0); }
    static SynthFx New(string preset) { var f = new SynthFx(); f.Prepare(44100); SynthPresets.Apply(f, preset); return f; }

    static void Main()
    {
        Fx.ProjectBpm = 120;
        // 1. pitch
        var fx = New("Init"); fx.Set("aon", 1); fx.Set("awt", 0); var o = Render(fx, new[] { 69 }, 44100, -1);
        double hz = Hz(o[0], 8000, 30000), pk = 0; for (int i = 8000; i < 44100; i++) pk = Math.Max(pk, Math.Abs(o[0][i]));
        Check("A4 plays at 440 Hz", Math.Abs(hz - 440) < 6, hz.ToString("0.0") + " Hz, level " + pk.ToString("0.00"));
        fx = New("Init"); o = Render(fx, new[] { 45 }, 44100, -1); hz = Hz(o[0], 8000, 30000);
        Check("A2 plays at 110 Hz", Math.Abs(hz - 110) < 3, hz.ToString("0.0") + " Hz");
        // saw / square waves are band-limited: no huge spikes
        fx = New("Init"); fx.Set("awt", 50); o = Render(fx, new[] { 93 }, 44100, -1); double sp = 0; for (int i = 8000; i < 44100; i++) sp = Math.Max(sp, Math.Abs(o[0][i]));
        Check("high saw note stays in range", sp < 1.0 && sp > 0.02, sp.ToString("0.00"));

        // 2. polyphony: three notes are louder than one
        fx = New("Init"); var one = Render(fx, new[] { 57 }, 22050, -1); fx = New("Init"); var three = Render(fx, new[] { 57, 61, 64 }, 22050, -1);
        double r1 = Rms(one[0], 8000, 22050), r3 = Rms(three[0], 8000, 22050);
        Check("chords play (3 notes are louder than 1)", r3 > r1 * 1.3, r1.ToString("0.000") + " -> " + r3.ToString("0.000"));

        // 3. wobble: the default patch wobbles at the synced LFO rate (1/8 note at 120 BPM = 4 Hz)
        fx = New("Wobble Bass"); fx.Set("m4a", 0); fx.Set("m5a", 0); fx.Set("m3a", 0); fx.Set("m1a", 0); fx.Set("dston", 0); fx.Set("bbon", 0); fx.Set("e1s", 100); fx.Set("e1d", 5000);
        int frames = 44100 * 4; o = Render(fx, new[] { 40 }, frames, -1);
        int win = 441; var env = new List<double>();   // brightness over time: energy of the signal's difference (a rough high-pass)
        for (int s = 22050; s + win < frames; s += win) { double e = 0; for (int i = s + 1; i < s + win; i++) { double d = o[0][i] - o[0][i - 1]; e += d * d; } env.Add(Math.Sqrt(e / win)); }
        double mean = 0; foreach (var v in env) mean += v; mean /= env.Count; double best = 0, bestHz = 0; double fs = 100.0;
        for (double f = 0.5; f <= 12; f += 0.05) { double re = 0, im = 0; for (int i = 0; i < env.Count; i++) { double a = 2 * Math.PI * f * i / fs; re += (env[i] - mean) * Math.Cos(a); im += (env[i] - mean) * Math.Sin(a); } double m = Math.Sqrt(re * re + im * im); if (m > best) { best = m; bestHz = f; } }
        double depth = 0, mn = double.MaxValue, mx = 0; foreach (var v in env) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); } depth = mx / Math.Max(1e-9, mn);
        Check("wobble follows the tempo-synced LFO (1/8 at 120 BPM = 4 Hz)", Math.Abs(bestHz - 4.0) < 0.4 && depth > 2.0, "brightness wobbles at " + bestHz.ToString("0.00") + " Hz, depth " + depth.ToString("0.0") + "x");
        Fx.ProjectBpm = 90; fx = New("Wobble Bass"); fx.Set("m4a", 0); fx.Set("m5a", 0); fx.Set("m3a", 0); fx.Set("m1a", 0); fx.Set("dston", 0); fx.Set("bbon", 0); fx.Set("e1s", 100); fx.Set("e1d", 5000); o = Render(fx, new[] { 40 }, frames, -1);
        env.Clear(); for (int s = 22050; s + win < frames; s += win) { double e = 0; for (int i = s + 1; i < s + win; i++) { double d = o[0][i] - o[0][i - 1]; e += d * d; } env.Add(Math.Sqrt(e / win)); }
        mean = 0; foreach (var v in env) mean += v; mean /= env.Count; best = 0; bestHz = 0;
        for (double f = 0.5; f <= 12; f += 0.05) { double re = 0, im = 0; for (int i = 0; i < env.Count; i++) { double a = 2 * Math.PI * f * i / fs; re += (env[i] - mean) * Math.Cos(a); im += (env[i] - mean) * Math.Sin(a); } double m = Math.Sqrt(re * re + im * im); if (m > best) { best = m; bestHz = f; } }
        Check("wobble speed follows the song tempo (90 BPM = 3 Hz)", Math.Abs(bestHz - 3.0) < 0.35, bestHz.ToString("0.00") + " Hz");
        Fx.ProjectBpm = 120;

        // 4. filter: closing the cutoff removes the highs
        fx = New("Init"); fx.Set("awt", 50); fx.Set("f1on", 1); fx.Set("f1type", 1); fx.Set("f1cut", 20000); o = Render(fx, new[] { 45 }, 22050, -1); double bright = 0; for (int i = 8000; i < 22050; i++) { double d = o[0][i] - o[0][i - 1]; bright += d * d; }
        fx = New("Init"); fx.Set("awt", 50); fx.Set("f1on", 1); fx.Set("f1type", 1); fx.Set("f1cut", 300); o = Render(fx, new[] { 45 }, 22050, -1); double dull = 0; for (int i = 8000; i < 22050; i++) { double d = o[0][i] - o[0][i - 1]; dull += d * d; }
        Check("filter cutoff darkens the sound", dull < bright * 0.05, "bright " + bright.ToString("0.0") + ", closed " + dull.ToString("0.000"));

        // 5. envelope: note off dies away
        fx = New("Init"); fx.Set("e1r", 100); o = Render(fx, new[] { 57 }, 44100, 20000); double during = Rms(o[0], 10000, 19000), after = Rms(o[0], 30000, 44000);
        Check("note off releases to silence", during > 0.02 && after < during * 0.01, "during " + during.ToString("0.000") + ", after " + after.ToString("0.00000"));

        // 6. bass boost effect
        foreach (var tc in new[] { new { f = 40.0, exp = 8.0, tol = 1.5 }, new { f = 1000.0, exp = 0.0, tol = 0.4 } })
        {
            var bb = new BassBoost(); bb.Prepare(44100); bb.Set("freq", 80); bb.Set("gain", 8); bb.Set("harm", 0); bb.Set("tight", 10); bb.Set("q", 0.7f);
            int n = 44100; var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) l[i] = r[i] = (float)(0.2 * Math.Sin(2 * Math.PI * tc.f * i / 44100.0)); var l0 = (float[])l.Clone();
            bb.Process(l, r, n); double db = 20 * Math.Log10(Rms(l, 20000, n) / Rms(l0, 20000, n));
            Check("bass boost at " + tc.f + " Hz: " + tc.exp + " dB", Math.Abs(db - tc.exp) < tc.tol, db.ToString("0.0") + " dB");
        }
        { var bb = new BassBoost(); bb.Prepare(44100); bb.Set("freq", 60); bb.Set("gain", 0); bb.Set("harm", 0); bb.Set("tight", 10); bb.Set("mix", 100);
          int n = 44100; var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) l[i] = r[i] = (float)(0.2 * Math.Sin(2 * Math.PI * 200 * i / 44100.0)); var l0 = (float[])l.Clone(); bb.Process(l, r, n);
          Check("bass boost with 0 dB leaves the sound alone", Math.Abs(20 * Math.Log10(Rms(l, 20000, n) / Rms(l0, 20000, n))) < 0.3, ""); }
        { var bb = new BassBoost(); bb.Prepare(44100); bb.Set("freq", 60); bb.Set("gain", 0); bb.Set("harm", 100); bb.Set("tight", 10);
          int n = 44100; var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) l[i] = r[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 50 * i / 44100.0)); bb.Process(l, r, n);
          // energy at 150 Hz (3rd harmonic) should now exist
          double re = 0, im = 0; for (int i = 20000; i < n; i++) { double a = 2 * Math.PI * 150 * i / 44100.0; re += l[i] * Math.Cos(a); im += l[i] * Math.Sin(a); } double h3 = Math.Sqrt(re * re + im * im) / (n - 20000) * 2;
          Check("bass boost harmonics add overtones (so small speakers can hear it)", h3 > 0.005, "150 Hz level " + h3.ToString("0.0000")); }

        // 7. every preset is safe and audible
        bool allOk = true; string bad = "";
        foreach (var name in SynthPresets.Names)
        {
            fx = New(name); o = Render(fx, new[] { 43, 50, 55 }, 44100, -1); double peak = 0; bool nan = false; for (int i = 0; i < 44100; i++) { float a = Math.Abs(o[0][i]); if (float.IsNaN(a) || float.IsInfinity(a)) nan = true; peak = Math.Max(peak, a); }
            double lv = Rms(o[0], 2000, 22050); if (nan || peak > 1.001 || lv < 0.003) { allOk = false; bad += name + " (peak " + peak.ToString("0.00") + ", level " + lv.ToString("0.0000") + ", nan " + nan + ") "; }
        }
        Check("all " + SynthPresets.Names.Length + " presets are audible, finite and within range", allOk, bad.Length == 0 ? "ok" : bad);

        // 8. save / load roundtrip and instrument track in the engine
        fx = New("Growl Bass"); fx.Set("f1cut", 1234); string ser = fx.Serialize(); var fx2 = new SynthFx(); fx2.Prepare(44100); fx2.Deserialize(ser);
        Check("a sound survives save and load", Math.Abs(fx2.Get("f1cut") - 1234) < 0.5 && fx2.Get("dstmode") == fx.Get("dstmode") && fx2.Serialize() == ser, "");
        var eng = new StudioEngine(); var tr = StudioEngine.NewTrack("Bass", "inst", null); tr.Instrument = true; var sf = New("Deep Sub"); sf.Owner = tr; tr.Fx.Add(sf); eng.Tracks.Add(tr); foreach (var m in eng.MasterFx) m.Bypass = true;
        tr.Midi.Add(new MidiEv { Frame = 11025, S = 0x90, D1 = 45, D2 = 110 }); tr.Midi.Add(new MidiEv { Frame = 33075, S = 0x80, D1 = 45, D2 = 0 });
        var L = new float[44100]; long w = 0; eng.RenderStream(0, 44100, new RenderOpts { Master = false, Fader = false }, null, (bl, br, off, c) => { Array.Copy(bl, off, L, w, c); w += c; }, null);
        double pre = Rms(L, 0, 11000), mid = Rms(L, 14000, 32000), post = Rms(L, 40000, 44000);
        Check("synth track plays recorded notes in a mix/export", pre < 1e-4 && mid > 0.03 && post < mid * 0.1, "before " + pre.ToString("0.0000") + ", during " + mid.ToString("0.000") + ", after " + post.ToString("0.0000"));
        Check("synth appears in the Add Instrument list", Array.Exists(FxRegistry.All, i => i.Type == "synth" && i.Category == "Instruments") && Array.Exists(FxRegistry.All, i => i.Type == "bassboost"), "");
        Console.WriteLine(fails == 0 ? "=== synth checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
