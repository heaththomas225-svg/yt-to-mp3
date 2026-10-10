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
        fx = New("Init"); fx.Set("awt", 50); fx.Set("f1on", 1); fx.Set("f1type", 0); fx.Set("f1slope", 1); fx.Set("f1cut", 20000); o = Render(fx, new[] { 45 }, 22050, -1); double bright = 0; for (int i = 8000; i < 22050; i++) { double d = o[0][i] - o[0][i - 1]; bright += d * d; }
        fx = New("Init"); fx.Set("awt", 50); fx.Set("f1on", 1); fx.Set("f1type", 0); fx.Set("f1slope", 1); fx.Set("f1cut", 300); o = Render(fx, new[] { 45 }, 22050, -1); double dull = 0; for (int i = 8000; i < 22050; i++) { double d = o[0][i] - o[0][i - 1]; dull += d * d; }
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
        // ================= 11. oscillators, wavetables, drawn waves, FM / ring mod =================
        {
            Func<float[], int, int, double, double> mag = (x, from, n, hzz) => { double re = 0, im = 0; for (int i = 0; i < n; i++) { double a = 2 * Math.PI * hzz * (from + i) / 44100.0; re += x[from + i] * Math.Cos(a); im += x[from + i] * Math.Sin(a); } return Math.Sqrt(re * re + im * im) * 2 / n; };
            Func<SynthFx> clean = () => { var f = New("Init"); f.Set("bbon", 0); f.Set("f1on", 0); f.Set("e1a", 0); f.Set("e1d", 5000); f.Set("e1s", 100); f.Set("vol", -2); return f; };
            Func<SynthFx, int, double, double> h2 = (f, src, dummy) => 0;
            Func<SynthFx, int, double, int, double> H = (f, note, f0, harm) => { var cp = new SynthFx(); cp.Prepare(44100); cp.Deserialize(f.Serialize()); var o2 = Render(cp, new[] { note }, 30000, -1); return mag(o2[0], 6000, 22050, f0 * harm); };
            // fixed shapes: sine has no 2nd harmonic, saw has a strong one, square has none (only odd harmonics)
            { var tw = clean(); tw.Set("atbl", 13); double sw1x = H(tw, 45, 110, 1), sw2x = H(tw, 45, 110, 2); tw.Set("atbl", 14); double q1 = H(tw, 45, 110, 1), q2 = H(tw, 45, 110, 2), q3 = H(tw, 45, 110, 3);
              Check("wavetable list has a Sawtooth (strong 2nd harmonic) and a Square (odd only)", Wavetables.TableNames[13] == "Sawtooth" && Wavetables.TableNames[14] == "Square" && sw2x / sw1x > 0.4 && q2 / q1 < 0.05 && q3 / q1 > 0.28, "saw 2nd/1st " + (sw2x / sw1x).ToString("0.00") + ", square 2nd " + (q2 / q1).ToString("0.000") + ", 3rd " + (q3 / q1).ToString("0.00")); }
            var s1 = clean(); s1.Set("asrc", 1); double sine1 = H(s1, 45, 110, 1), sine2 = H(s1, 45, 110, 2);
            var s2 = clean(); s2.Set("asrc", 3); double saw1 = H(s2, 45, 110, 1), saw2 = H(s2, 45, 110, 2);
            var s3 = clean(); s3.Set("asrc", 4); double sq1 = H(s3, 45, 110, 1), sq2 = H(s3, 45, 110, 2), sq3 = H(s3, 45, 110, 3);
            Check("Sine source: a pure tone (no 2nd harmonic)", sine1 > 0.1 && sine2 / sine1 < 0.02, "2nd/1st = " + (sine2 / sine1).ToString("0.000"));
            Check("Saw source: all harmonics (2nd is half the 1st)", saw2 / saw1 > 0.4 && saw2 / saw1 < 0.6, "2nd/1st = " + (saw2 / saw1).ToString("0.00"));
            Check("Square source: odd harmonics only (3rd is a third of the 1st, no 2nd)", sq2 / sq1 < 0.05 && sq3 / sq1 > 0.28 && sq3 / sq1 < 0.4, "2nd " + (sq2 / sq1).ToString("0.000") + ", 3rd " + (sq3 / sq1).ToString("0.00"));
            // wavetable position scans between frames (Basic Shapes: sine .. saw .. square)
            var w0 = clean(); w0.Set("asrc", 0); w0.Set("atbl", 0); w0.Set("awt", 0); double wa = H(w0, 45, 110, 2) / H(w0, 45, 110, 1); var w1 = clean(); w1.Set("asrc", 0); w1.Set("atbl", 0); w1.Set("awt", 50); double wb = H(w1, 45, 110, 2) / H(w1, 45, 110, 1);
            Check("WAVE knob scans the wavetable (sine at 0%, saw at 50%)", wa < 0.02 && wb > 0.4, "2nd harmonic: " + wa.ToString("0.00") + " at 0%, " + wb.ToString("0.00") + " at 50%");
            // other factory tables sound different from each other
            double[] cen = new double[3]; int[] tb = { 3, 4, 7 }; for (int q = 0; q < 3; q++) { var ft = clean(); ft.Set("atbl", tb[q]); ft.Set("awt", 60); var o3 = Render(ft, new[] { 45 }, 30000, -1); double e = 0; for (int i = 8001; i < 30000; i++) { double dd = o3[0][i] - o3[0][i - 1]; e += dd * dd; } cen[q] = e; }
            Check("different factory wavetables give different sounds", Math.Abs(cen[0] - cen[1]) > cen[0] * 0.05 || Math.Abs(cen[0] - cen[2]) > cen[0] * 0.05, "brightness " + cen[0].ToString("0.0") + " / " + cen[1].ToString("0.0") + " / " + cen[2].ToString("0.0"));
            // no aliasing on a high note: a saw at 2637 Hz must not leave energy at the folded-back frequency
            var hi = clean(); hi.Set("asrc", 3); var oh = Render(hi, new[] { 100 }, 30000, -1); double f0h = 440 * Math.Pow(2, (100 - 69) / 12.0); double fund = mag(oh[0], 6000, 22050, f0h), alias = mag(oh[0], 6000, 22050, 44100 - 9 * f0h);
            Check("high notes are band-limited (no aliasing)", alias / fund < 0.01, "alias level " + (alias / fund).ToString("0.0000") + " of the fundamental");
            // drawn waveforms
            var sine = new short[256]; var saw = new short[256]; for (int i = 0; i < 256; i++) { sine[i] = (short)(Math.Sin(2 * Math.PI * i / 256.0) * 30000); saw[i] = (short)((2.0 * i / 256 - 1) * 30000); }
            var cw = clean(); Wavetables.SetCustom(0, new[] { sine, saw }); cw.Set("atbl", Wavetables.CustomBase); cw.Set("awt", 0); double c0 = H(cw, 45, 110, 2) / H(cw, 45, 110, 1); cw.Set("awt", 100); double c1 = H(cw, 45, 110, 2) / H(cw, 45, 110, 1);
            Check("a drawn wave plays: frame 1 (sine) is pure, frame 2 (saw) is rich", c0 < 0.02 && c1 > 0.4, "2nd harmonic " + c0.ToString("0.00") + " then " + c1.ToString("0.00"));
            string ser2 = cw.Serialize(); Wavetables.SetCustom(0, new[] { Wavetables.DefaultFrame() }); var cw2 = new SynthFx(); cw2.Prepare(44100); cw2.Deserialize(ser2); var back = Wavetables.GetCustom(0);
            Check("drawn waves are saved inside the patch", ser2.Contains("wtc1=") && back != null && back.Length == 2 && back[1][10] == saw[10], back == null ? "none" : back.Length + " frames restored");
            // FM and ring mod
            var nofm = clean(); nofm.Set("asrc", 1); nofm.Set("bon", 1); nofm.Set("bsrc", 1); nofm.Set("blvl", 0); double plain = H(nofm, 45, 110, 2);
            var fm = clean(); fm.Set("asrc", 1); fm.Set("bon", 1); fm.Set("bsrc", 1); fm.Set("blvl", 0); fm.Set("afm", 60); double fmv = H(fm, 45, 110, 2) / H(fm, 45, 110, 1);
            Check("FM adds sidebands (a silent modulator oscillator still modulates)", plain < 0.01 && fmv > 0.1, "2nd harmonic: " + plain.ToString("0.000") + " without FM, " + fmv.ToString("0.00") + " with FM");
            var rg = clean(); rg.Set("asrc", 1); rg.Set("bon", 1); rg.Set("bsrc", 1); rg.Set("boct", 1); rg.Set("blvl", 0); rg.Set("arm", 100); var orr = Render(rg, new[] { 45 }, 30000, -1);
            double sq1x = mag(orr[0], 6000, 22050, 110), sq2x = mag(orr[0], 6000, 22050, 220), sq3x = mag(orr[0], 6000, 22050, 330);
            Check("ring modulation: sine x sine an octave up leaves the 1st and 3rd harmonics, not the 2nd", sq1x > 0.05 && sq3x > 0.05 && sq2x < 0.1 * sq1x, "110 Hz " + sq1x.ToString("0.00") + ", 220 Hz " + sq2x.ToString("0.00") + ", 330 Hz " + sq3x.ToString("0.00"));
            var wp = clean(); wp.Set("asrc", 3); wp.Set("awarp", 3); wp.Set("awamt", 80); double warped = H(wp, 45, 110, 3) / H(wp, 45, 110, 1); var wn = clean(); wn.Set("asrc", 3); double unw = H(wn, 45, 110, 3) / H(wn, 45, 110, 1);
            Check("WARP (fold) changes the harmonics", Math.Abs(warped - unw) > 0.1, "3rd harmonic ratio " + unw.ToString("0.00") + " -> " + warped.ToString("0.00"));
            var un = clean(); un.Set("asrc", 3); un.Set("auni", 5); un.Set("adet", 40); var ou = Render(un, new[] { 45 }, 120000, -1); double beating = 0; for (int i = 4000; i + 4410 < 120000; i += 2205) beating += Math.Abs(Rms(ou[0], i, i + 2205) - Rms(ou[0], i + 2205, i + 4410)); var os = Render(clean(), new[] { 45 }, 120000, -1); double steady = 0; for (int i = 4000; i + 4410 < 120000; i += 2205) steady += Math.Abs(Rms(os[0], i, i + 2205) - Rms(os[0], i + 2205, i + 4410));
            Check("unison and detune thicken the sound (the voices beat against each other)", beating > steady * 3, "movement " + steady.ToString("0.000") + " -> " + beating.ToString("0.000"));
        }
        // ================= 12. mixer: level, pan, mute, solo, routing, meters =================
        {
            Func<SynthFx> two = () => { var f = New("Init"); f.Set("bbon", 0); f.Set("f1on", 0); f.Set("e1a", 0); f.Set("e1d", 5000); f.Set("e1s", 100); f.Set("asrc", 3); f.Set("bon", 1); f.Set("bsrc", 4); return f; };
            Func<SynthFx, float[][]> run = f => Render(f, new[] { 45 }, 24000, -1);
            var both = two(); double lvBoth = Rms(run(both)[0], 8000, 24000);
            var mA = two(); mA.Set("amute", 1); double lvMuteA = Rms(run(mA)[0], 8000, 24000); var mAB = two(); mAB.Set("amute", 1); mAB.Set("bmute", 1); double lvAllMuted = Rms(run(mAB)[0], 8000, 24000);
            Check("mute silences one source and keeps the others", lvMuteA > 0.02 && lvMuteA < lvBoth * 1.05 && lvAllMuted < 1e-5, "both " + lvBoth.ToString("0.000") + ", A muted " + lvMuteA.ToString("0.000") + ", both muted " + lvAllMuted.ToString("0.0000"));
            var sB = two(); sB.Set("bsolo", 1); var oB = run(sB); var onlyB = two(); onlyB.Set("aon", 0); var oOnlyB = run(onlyB);
            Check("solo plays only the soloed source", Math.Abs(Rms(oB[0], 8000, 24000) - Rms(oOnlyB[0], 8000, 24000)) < 0.003, "solo B " + Rms(oB[0], 8000, 24000).ToString("0.000") + " = B alone " + Rms(oOnlyB[0], 8000, 24000).ToString("0.000"));
            var pL = two(); pL.Set("bon", 0); pL.Set("apan", -100); var opl = run(pL); Check("pan -100 puts the sound in the left channel", Rms(opl[0], 8000, 24000) > 20 * Rms(opl[1], 8000, 24000) + 1e-4, "L " + Rms(opl[0], 8000, 24000).ToString("0.000") + ", R " + Rms(opl[1], 8000, 24000).ToString("0.0000"));
            var l1 = two(); l1.Set("bon", 0); l1.Set("alvl", 100); var l2 = two(); l2.Set("bon", 0); l2.Set("alvl", 25); double ratio = Rms(run(l1)[0], 8000, 24000) / Rms(run(l2)[0], 8000, 24000);
            Check("level knob scales the volume (100% vs 25% = 4x)", ratio > 3.5 && ratio < 4.5, "ratio " + ratio.ToString("0.00"));
            var met = two(); run(met); Check("the level meters move with the sound", met.SrcPeak[0] > 0.02 && met.SrcPeak[1] > 0.02 && met.SrcPeak[2] < 1e-4 && met.OutPeakL > 0.02, "A " + met.SrcPeak[0].ToString("0.00") + ", B " + met.SrcPeak[1].ToString("0.00") + ", C " + met.SrcPeak[2].ToString("0.00") + ", out " + met.OutPeakL.ToString("0.00"));
            // routing: both filters closed (low-pass at 20 Hz) = silence, unless a source goes direct
            var rt = two(); rt.Set("bon", 0); rt.Set("f1on", 1); rt.Set("f1cut", 20); rt.Set("f1slope", 3); rt.Set("f2on", 1); rt.Set("f2cut", 20); rt.Set("f2slope", 3); double closed = Rms(run(rt)[0], 8000, 24000);
            var rd = two(); rd.Set("bon", 0); rd.Set("f1on", 1); rd.Set("f1cut", 20); rd.Set("f1slope", 3); rd.Set("f2on", 1); rd.Set("f2cut", 20); rd.Set("f2slope", 3); rd.Set("aroute", 3); double direct = Rms(run(rd)[0], 8000, 24000);
            var r2f = two(); r2f.Set("bon", 0); r2f.Set("f1on", 1); r2f.Set("f1cut", 20); r2f.Set("f1slope", 3); r2f.Set("f2on", 1); r2f.Set("f2cut", 20000); r2f.Set("aroute", 1); double viaF2 = Rms(run(r2f)[0], 8000, 24000);
            Check("routing: a source sent to Filter 1 is silenced by a closed Filter 1, 'Direct' bypasses both filters", closed < 0.002 && direct > 0.03, "through closed filters " + closed.ToString("0.0000") + ", direct " + direct.ToString("0.000"));
            Check("routing: a source sent to Filter 2 skips a closed Filter 1", viaF2 > 0.03, "level " + viaF2.ToString("0.000"));
        }
        // ================= 13. filters: modes, slopes, routing =================
        {
            Func<float[], int, int, double, double> mag = (x, from, n, hzz) => { double re = 0, im = 0; for (int i = 0; i < n; i++) { double a = 2 * Math.PI * hzz * (from + i) / 44100.0; re += x[from + i] * Math.Cos(a); im += x[from + i] * Math.Sin(a); } return Math.Sqrt(re * re + im * im) * 2 / n; };
            Func<int, int, double, SynthFx> flt = (type, slope, cut) => { var f = New("Init"); f.Set("bbon", 0); f.Set("e1a", 0); f.Set("e1d", 5000); f.Set("e1s", 100); f.Set("asrc", 3); f.Set("f1on", 1); f.Set("f1type", type); f.Set("f1slope", slope); f.Set("f1cut", (float)cut); f.Set("f1res", 0); f.Set("f1drive", 0); return f; };
            Func<SynthFx, double, double> at = (f, hzz) => { var cp = new SynthFx(); cp.Prepare(44100); cp.Deserialize(f.Serialize()); var oo = Render(cp, new[] { 45 }, 30000, -1); return mag(oo[0], 6000, 22050, hzz); };   // saw A2 (110 Hz) has a harmonic at every multiple
            var flat = flt(0, 0, 20000); double ref1980 = at(flat, 1980), ref110 = at(flat, 110), ref990 = at(flat, 990);
            double[] att = new double[4]; for (int s = 0; s < 4; s++) att[s] = at(flt(0, s, 500), 1980) / ref1980;
            Check("low-pass slopes: each step (12, 24, 36, 48 dB) cuts the highs more", att[0] < 0.3 && att[1] < att[0] * 0.5 && att[2] < att[1] * 0.5 && att[3] < att[2] * 0.6, "level of the 1980 Hz harmonic: " + att[0].ToString("0.000") + " / " + att[1].ToString("0.0000") + " / " + att[2].ToString("0.00000") + " / " + att[3].ToString("0.000000"));
            double lpLow = at(flt(0, 1, 2000), 110) / ref110;
            Check("low-pass keeps the lows", lpLow > 0.85, (lpLow).ToString("0.00") + " of the 110 Hz harmonic");
            double hpLow = at(flt(1, 1, 2000), 110) / ref110, hpHigh = at(flt(1, 1, 2000), 4950) / at(flat, 4950);
            Check("high-pass removes the lows and keeps the highs", hpLow < 0.02 && hpHigh > 0.8, "110 Hz " + hpLow.ToString("0.000") + ", 4950 Hz " + hpHigh.ToString("0.00"));
            var bp = flt(2, 0, 990); bp.Set("f1res", 95); double bpC = at(bp, 990), bpL = at(bp, 110), bpH = at(bp, 4950);
            Check("band-pass keeps only a band around the cutoff", bpC > bpL * 3 && bpC > bpH * 3, "990 Hz " + bpC.ToString("0.000") + ", 110 Hz " + bpL.ToString("0.000") + ", 4950 Hz " + bpH.ToString("0.000"));
            var nt = flt(3, 0, 990); nt.Set("f1res", 70); double ntC = at(nt, 990) / ref990, ntO = at(nt, 110) / ref110;
            Check("notch cuts a hole at the cutoff", ntC < 0.4 && ntO > 0.8, "990 Hz " + ntC.ToString("0.00") + ", 110 Hz " + ntO.ToString("0.00"));
            var pkf = flt(4, 0, 990); pkf.Set("f1res", 50); pkf.Set("f1gain", 12); double pkC = at(pkf, 990) / ref990, pkO = at(pkf, 110) / ref110;
            Check("peak boosts around the cutoff by its gain", pkC > 2.5 && pkO > 0.85 && pkO < 1.2, "990 Hz x" + pkC.ToString("0.0") + ", 110 Hz x" + pkO.ToString("0.00"));
            var ls = flt(5, 0, 400); ls.Set("f1gain", 12); double lsL = at(ls, 110) / ref110, lsH = at(ls, 4950) / at(flat, 4950); var hs = flt(6, 0, 3000); hs.Set("f1gain", 12); double hsH = at(hs, 9900) / at(flat, 9900), hsL = at(hs, 110) / ref110;
            Check("low shelf boosts the lows, high shelf boosts the highs", lsL > 2.5 && lsH < 1.3 && hsH > 2.5 && hsL < 1.3, "low shelf x" + lsL.ToString("0.0") + ", high shelf x" + hsH.ToString("0.0"));
            foreach (int ty in new[] { 7, 8, 9 }) { var cf = flt(ty, 0, 500); cf.Set("f1res", 50); var oc = Render(cf, new[] { 45, 52 }, 30000, -1); double pkv = 0; bool nan = false; for (int i = 6000; i < 30000; i++) { float a = Math.Abs(oc[0][i]); if (float.IsNaN(a)) nan = true; pkv = Math.Max(pkv, a); }
                Check(SynthTables.FilterTypes[ty] + " filter is audible and stable", !nan && pkv > 0.01 && pkv < 1.0, "peak " + pkv.ToString("0.00")); }
            // serial vs parallel: low-pass 300 then high-pass 3000 = almost nothing in series; side by side both bands pass
            Func<bool, double> sper = par => { var f = flt(0, 1, 300); f.Set("f2on", 1); f.Set("f2type", 1); f.Set("f2slope", 1); f.Set("f2cut", 3000); f.Set("froute", par ? 1 : 0); f.Set("aroute", par ? 2 : 0); var oo = Render(f, new[] { 45 }, 24000, -1); return Rms(oo[0], 8000, 24000); };
            double serial = sper(false), parallel = sper(true); Check("filters in series (low-pass then high-pass) cancel out, in parallel both bands pass", parallel > serial * 4 && parallel > 0.02, "series " + serial.ToString("0.0000") + ", parallel " + parallel.ToString("0.000"));
            // filter drive and no clipping with a loud resonant setting
            var dr = flt(0, 1, 600); dr.Set("f1res", 95); dr.Set("f1drive", 100); var od = Render(dr, new[] { 33, 40, 45, 52 }, 30000, -1); double pd = 0; for (int i = 0; i < 30000; i++) pd = Math.Max(pd, Math.Abs(od[0][i]));
            Check("a loud, resonant, driven filter stays inside the output range (no hard clipping)", pd <= 1.0001, "peak " + pd.ToString("0.000"));
            // key tracking: with 100% tracking the cutoff follows the note
            var kt0 = flt(0, 1, 500); var kt1 = flt(0, 1, 500); kt1.Set("f1key", 100); var oa = Render(kt0, new[] { 81 }, 30000, -1); var ob = Render(kt1, new[] { 81 }, 30000, -1); double fo = 440 * Math.Pow(2, (81 - 69) / 12.0);
            Check("key tracking opens the filter on high notes", Rms(ob[0], 6000, 30000) > Rms(oa[0], 6000, 30000) * 3, "level " + Rms(oa[0], 6000, 30000).ToString("0.000") + " -> " + Rms(ob[0], 6000, 30000).ToString("0.000"));
        }
        // 8b. drawn LFO shapes
        {
            Func<string, bool, SynthFx> lf = (shape, loop) => { var f = New("Init"); f.Set("awt", 50); f.Set("f1on", 1); f.Set("f1type", 0); f.Set("f1slope", 1); f.Set("f1cut", 700); f.Set("f1res", 0); f.Set("f1drive", 0); f.Set("bbon", 0); f.Set("e1s", 100); f.Set("e1d", 8000);
                f.LfoCurves[0].Load(shape); f.Set("l1shape", 7); f.Set("l1sync", 0); f.Set("l1rate", 2); f.Set("l1retrig", 1); f.Set("l1loop", loop ? 1 : 0); f.Set("m1s", 1); f.Set("m1d", 17); f.Set("m1a", 100); return f; };
            Func<float[], double, double, double> brt = (x, t0, t1) => { double e = 0; int a = (int)(t0 * 44100), b = (int)(t1 * 44100); for (int i = a + 1; i < b; i++) { double d = x[i] - x[i - 1]; e += d * d; } return Math.Sqrt(e / (b - a)); };
            var sqw = Render(lf("Square", true), new[] { 40 }, 44100, -1);
            double hi = brt(sqw[0], 0.05, 0.2), lo = brt(sqw[0], 0.30, 0.45), hi2 = brt(sqw[0], 0.55, 0.7);
            Check("a drawn square LFO opens then closes the filter exactly where the shape says", hi > lo * 2 && hi2 > lo * 2, "open " + hi.ToString("0.0000") + ", closed " + lo.ToString("0.0000") + ", open again " + hi2.ToString("0.0000"));
            var os = Render(lf("Saw up", false), new[] { 40 }, 44100, -1); double early = brt(os[0], 0.02, 0.08), late1 = brt(os[0], 0.65, 0.75), late2 = brt(os[0], 0.9, 1.0);
            Check("one-shot LFO plays the shape once and holds its end", late1 > early * 2 && Math.Abs(late1 - late2) < late1 * 0.2, "start " + early.ToString("0.0000") + ", then " + late1.ToString("0.0000") + " / " + late2.ToString("0.0000"));
            var sx0 = lf("Wobble 2", true); sx0.LfoCurves[0].Pts[2].Y = 0.8f; sx0.LfoCurves[0].Rebuild(); string sx = sx0.Serialize(); var dst2 = New("Init"); dst2.Deserialize(sx);
            bool same = true; for (int i = 0; i <= 1000; i += 37) if (Math.Abs(sx0.LfoCurves[0].Lut[i] - dst2.LfoCurves[0].Lut[i]) > 0.002) same = false;
            Check("a drawn LFO shape is saved in the patch and restored exactly", sx.Contains("lc1=") && same && dst2.LfoCurves[0].Pts.Count == sx0.LfoCurves[0].Pts.Count, dst2.LfoCurves[0].Pts.Count + " points restored");
            var cv = new LfoCurve(); cv.Load("Triangle"); Check("the on-screen curve and the audio table agree (triangle peaks at 1 in the middle, -1 at the ends)", Math.Abs(cv.Read(0.5) - 1) < 0.01 && Math.Abs(cv.Read(0) + 1) < 0.01 && Math.Abs(cv.Read(0.25)) < 0.01, "mid " + cv.Read(0.5).ToString("0.00") + ", start " + cv.Read(0).ToString("0.00") + ", quarter " + cv.Read(0.25).ToString("0.00"));
        }
        // 8c. modulation on any knob
        {
            var mf = New("Init"); bool mapOk = SynthTables.DestParam.Length == SynthTables.Dests.Length; string badp = ""; foreach (var pid in SynthTables.DestParam) if (pid != null && mf.Find(pid) == null) { mapOk = false; badp += pid + " "; }
            Check("every modulation target is matched to a real knob", mapOk, badp.Length == 0 ? SynthTables.Dests.Length + " targets" : "missing: " + badp);
            Func<bool, int, int, SynthFx> pn = (on, pol, dest) => { var f = New("Init"); f.Set("bbon", 0); f.Set("apan", 0); f.Set("mac1", 100); f.Set("m1s", 10); f.Set("m1d", dest); f.Set("m1a", 100); f.Set("m1e", on ? 1 : 0); f.Set("m1p", pol); return f; };
            var pr = Render(pn(true, 0, 27), new[] { 45 }, 24000, -1); double pL = Rms(pr[0], 8000, 24000), pR = Rms(pr[1], 8000, 24000);
            Check("any knob can be modulated: a macro on Osc A pan sends the sound to the right", pR > pL * 20, "L " + pL.ToString("0.0000") + ", R " + pR.ToString("0.000"));
            var po = Render(pn(false, 0, 27), new[] { 45 }, 24000, -1); double qL = Rms(po[0], 8000, 24000), qR = Rms(po[1], 8000, 24000);
            Check("a matrix connection switched off does nothing", Math.Abs(qL - qR) < qL * 0.05, "L " + qL.ToString("0.000") + ", R " + qR.ToString("0.000"));
            var pi = Render(pn(true, 2, 27), new[] { 45 }, 24000, -1); Check("Negative polarity flips the direction (now to the left)", Rms(pi[0], 8000, 24000) > Rms(pi[1], 8000, 24000) * 20, "L " + Rms(pi[0], 8000, 24000).ToString("0.000") + ", R " + Rms(pi[1], 8000, 24000).ToString("0.0000"));
            Func<bool, double> dm = on => { var f = pn(on, 0, 35); f.Set("dston", 1); f.Set("dstmode", 2); f.Set("dstdrive", 30); f.Set("dstmix", 0); f.Set("awt", 50); var oo = Render(f, new[] { 45 }, 24000, -1); return Rms(oo[0], 8000, 24000); };
            double dry = dm(false), wet = dm(true); Check("modulation reaches the effects: a macro on Distortion mix changes the sound", Math.Abs(wet - dry) > dry * 0.1, "dry " + dry.ToString("0.000") + ", modulated " + wet.ToString("0.000"));
        }
        // 8f. presets: library, user presets, initialise, global settings
        {
            string miss = ""; foreach (var ct in SynthPresets.Categories) if (SynthPresets.InCategory(ct).Count < 1) miss += ct + " "; int minc = 99; foreach (var ct in SynthPresets.Categories) if (ct != "Init") minc = Math.Min(minc, SynthPresets.InCategory(ct).Count);
            Check("the preset library covers every style with at least 3 each", miss.Length == 0 && minc >= 3 && SynthPresets.Names.Length >= 45, SynthPresets.Names.Length + " presets, fewest in a style: " + minc + (miss.Length > 0 ? ", empty: " + miss : ""));
            var pf = New("Riddim Chug"); pf.LfoCurves[2].Pts[1].Y = 0.3f; pf.LfoCurves[2].Rebuild(); pf.Set("fo0", 4); pf.Set("fo4", 0); pf.Set("m6s", 3); pf.Set("m6d", 27); pf.Set("m6a", -33); string uname = "ZZ test preset"; try { SynthPresets.DeleteUser(uname); SynthPresets.DeleteUser("ZZ renamed"); SynthPresets.DeleteUser(uname + " copy"); } catch { }
            SynthPresets.SaveUser(pf, uname); var ld = new SynthFx(); ld.Prepare(44100); SynthPresets.Apply(ld, uname);
            bool same = ld.Serialize() == pf.Serialize(); Check("a saved preset loads back identical (knobs, LFO shapes, matrix, effect order)", same && ld.Get("m6a") == -33 && ld.Get("fo0") == 4 && Math.Abs(ld.LfoCurves[2].Pts[1].Y - 0.3f) < 0.01f, same ? "identical" : "differs");
            string dup = SynthPresets.DuplicateUser(uname); SynthPresets.RenameUser(dup, "ZZ renamed"); bool listed = SynthPresets.UserNames().Contains("ZZ renamed") && !SynthPresets.UserNames().Contains(dup);
            SynthPresets.DeleteUser("ZZ renamed"); SynthPresets.DeleteUser(uname); Check("presets can be duplicated, renamed and deleted", listed && !SynthPresets.UserNames().Contains("ZZ renamed") && !SynthPresets.UserNames().Contains(uname), "ok");
            bool refused = false; try { SynthPresets.SaveUser(pf, "Init"); } catch { refused = true; } Check("a built-in preset name cannot be overwritten", refused, "");
            ld.Set("m6s", 4); ld.LfoCurves[0].Load("Saw up"); Wavetables.SetCustom(0, new[] { new short[256], new short[256] }); SynthPresets.Initialise(ld);
            Check("Initialise patch clears knobs, LFO shapes, drawn waves and the modulation matrix", ld.Get("m6s") == 0 && ld.Get("fo0") == 0 && ld.LfoCurves[0].Pts.Count == 3 && Wavetables.GetCustom(0).Length == 1 && ld.PresetName == "Init", "name " + ld.PresetName);
            var gf = New("Init"); gf.Set("voices", 3); gf.Set("tune", 20); gf.SaveGlobals(); var g2 = new SynthFx(); g2.Prepare(44100); SynthPresets.Apply(g2, "Deep Sub");
            Check("global settings (voice limit, tuning) survive a preset change and a restart", g2.Get("voices") == 3 && g2.Get("tune") == 20, "voices " + g2.Get("voices") + ", tune " + g2.Get("tune"));
            gf.Set("voices", 8); gf.Set("tune", 0); gf.Set("bend", 2); gf.Set("vsens", 60); gf.SaveGlobals();
        }
        // 8e. envelopes: plucks die away on a held key, sustains hold, release fades after the key goes up
        {
            Func<float, float, float, float, float[][]> en = (a, d, s, r) => { var f = New("Init"); f.Set("bbon", 0); f.Set("e1a", a); f.Set("e1d", d); f.Set("e1s", s); f.Set("e1r", r); return Render(f, new[] { 45 }, 66150, 44100); };
            var pl = en(1, 150, 0, 200); var su = en(1, 150, 80, 200); var lo = en(400, 150, 80, 200);
            double plEarly = Rms(pl[0], 500, 2500), plLate = Rms(pl[0], 30000, 40000), suLate = Rms(su[0], 30000, 40000), suRel = Rms(su[0], 56000, 66000), atk0 = Rms(lo[0], 0, 1500), atk1 = Rms(lo[0], 20000, 30000);
            Check("pluck envelope (sustain 0) dies away while the key is still held", plEarly > 0.02 && plLate < plEarly * 0.05, "start " + plEarly.ToString("0.000") + ", 0.7 s later " + plLate.ToString("0.0000"));
            Check("sustain envelope holds while the key is down and fades after release", suLate > 0.02 && suRel < suLate * 0.02, "held " + suLate.ToString("0.000") + ", after release " + suRel.ToString("0.00000"));
            Check("a slow attack fades the note in", atk1 > atk0 * 5, "first 30 ms " + atk0.ToString("0.0000") + ", later " + atk1.ToString("0.000"));
        }
        // 8d. effect chain
        {
            Func<SynthFx> baseFx = () => { var f = New("Init"); f.Set("awt", 50); f.Set("bbon", 0); f.Set("e1s", 100); f.Set("e1d", 8000); f.Set("f1cut", 3000); return f; };
            var plain = Render(baseFx(), new[] { 45, 52 }, 30000, -1); var plainL = baseFx(); plainL.Set("apan", -100); var plainP = Render(plainL, new[] { 45, 52 }, 30000, -1);
            string[] names = { "Distortion", "Saturation", "Compressor", "EQ", "Phaser", "Flanger", "Chorus", "Delay", "Reverb", "Stereo width" }; string[] ons = { "dston", "saton", "cpon", "eqon", "phon", "flon", "chon", "dlon", "rvon", "wdon" };
            for (int e = 0; e < ons.Length; e++)
            {
                var f = baseFx(); f.Set(ons[e], 1); if (e == 3) { f.Set("eqlg", 12); f.Set("eqhg", -12); } if (e == 9) { f.Set("wdwidth", 0); f.Set("apan", -100); } if (e == 2) { f.Set("cpthr", -40); f.Set("cpratio", 12); }
                var o2 = Render(f, new[] { 45, 52 }, 30000, -1); double diff = 0, pkx = 0; bool badx = false; for (int i = 8000; i < 30000; i++) { double d = o2[0][i] - (e == 9 ? plainP : plain)[0][i]; diff += d * d; float a = Math.Abs(o2[0][i]); if (float.IsNaN(a) || float.IsInfinity(a)) badx = true; pkx = Math.Max(pkx, a); }
                diff = Math.Sqrt(diff / 22000); Check(names[e] + " in the chain changes the sound and stays in range", !badx && diff > 0.002 && pkx <= 1.0001, "difference " + diff.ToString("0.0000") + ", peak " + pkx.ToString("0.00"));
            }
            Func<bool, float[][]> ord = swap => { var f = baseFx(); f.Set("saton", 1); f.Set("satdrive", 24); f.Set("eqon", 1); f.Set("eqlg", 15); if (swap) { f.Set("fo1", 3); f.Set("fo3", 1); } return Render(f, new[] { 45 }, 30000, -1); };
            var oax = ord(false); var obx = ord(true); double odx = 0; for (int i = 8000; i < 30000; i++) odx += Math.Abs(oax[0][i] - obx[0][i]); odx /= 22000;
            Check("changing the order of the chain changes the sound", odx > 0.003, "average difference " + odx.ToString("0.0000"));
            var off = baseFx(); off.Set("saton", 1); off.Set("satdrive", 24); off.Set("satmix", 0); var oox = Render(off, new[] { 45, 52 }, 30000, -1); double mdx = 0; for (int i = 8000; i < 30000; i++) mdx += Math.Abs(oox[0][i] - plain[0][i]); mdx /= 22000;
            Check("wet/dry at 0% leaves the sound untouched", mdx < 0.002, "difference " + mdx.ToString("0.00000"));
            var sv = baseFx(); sv.Set("fo0", 5); sv.Set("fo5", 0); var sv2 = new SynthFx(); sv2.Deserialize(sv.Serialize()); Check("the effect order is saved in the patch", sv2.Get("fo0") == 5 && sv2.Get("fo5") == 0, "slot 1 = " + sv2.Get("fo0"));
        }
        // 9. THE PIANO: a key press must reach the real sound device without pressing PLAY first, and a held note must stop on release / panic
        {
            var e2 = new StudioEngine(); var t2 = StudioEngine.NewTrack("Bass", "inst", null); t2.Instrument = true; var s2 = New("Deep Sub"); s2.Owner = t2; t2.Fx.Add(s2); e2.Tracks.Add(t2);
            s2.NoteOn(45, 110); double peak = 0; for (int i = 0; i < 25; i++) { System.Threading.Thread.Sleep(20); peak = Math.Max(peak, Math.Max(e2.MasterPeakL, e2.MasterPeakR)); }
            Check("piano key makes sound on the real audio device without pressing PLAY", peak > 0.01, "output level reached " + peak.ToString("0.000") + "; audio: " + e2.AudioStatus);
            s2.Panic(); System.Threading.Thread.Sleep(250); double afterPanic = Math.Max(e2.MasterPeakL, e2.MasterPeakR);
            Check("panic stops every note at once", afterPanic < 0.002, "level after panic " + afterPanic.ToString("0.0000"));
            s2.NoteOn(45, 110); s2.NoteOn(52, 110); System.Threading.Thread.Sleep(300); double two = Math.Max(e2.MasterPeakL, e2.MasterPeakR); s2.NoteOff(45); s2.NoteOff(52); System.Threading.Thread.Sleep(1500); double rel = Math.Max(e2.MasterPeakL, e2.MasterPeakR);
            Check("notes held then released fade out (no stuck note)", two > 0.01 && rel < two * 0.1, "held " + two.ToString("0.000") + ", 1.5 s after release " + rel.ToString("0.0000"));
            e2.Shutdown();
        }
        // 10. MIDI input decoding, device listing, voice limit
        {
            var fm = New("Init"); fm.Owner = null; MidiInput.Target = fm; MidiInput.Handle(0x90u | (60u << 8) | (100u << 16)); var res = Render(fm, new int[0], 8192, -1); double lvl = Rms(res[0], 4000, 8192);
            Check("a MIDI note-on message plays a note", lvl > 0.005 && fm.ActiveVoices == 1, "level " + lvl.ToString("0.000") + ", voices " + fm.ActiveVoices);
            MidiInput.Handle(0x80u | (60u << 8)); Render(fm, new int[0], 44100, -1); Check("a MIDI note-off message releases it", fm.ActiveVoices == 0, "voices left " + fm.ActiveVoices);
            MidiInput.Handle(0xE0u | (0u << 8) | (96u << 16)); MidiInput.Handle(0xB0u | (64u << 8) | (127u << 16)); MidiInput.Target = null;
            var devs = MidiInput.Devices(); Check("MIDI devices can be listed without errors", devs != null, devs.Count + " device(s) found on this PC: " + string.Join(", ", devs.ToArray()));
            bool ok = !MidiInput.Open("Not A Real Device") && MidiInput.Status.Length > 0; Check("opening a missing MIDI device gives a clear message", ok, MidiInput.Status);
            var lim = New("Init"); lim.Set("voices", 2); Render(lim, new[] { 40, 44, 47, 52 }, 4096, -1); Check("the voice limit is respected", lim.ActiveVoices <= 2, lim.ActiveVoices + " voices sounding with a limit of 2");
            var mono = New("Init"); mono.Set("mode", 1); Render(mono, new[] { 40, 44, 47 }, 4096, -1); Check("mono mode plays one note at a time", mono.ActiveVoices == 1, mono.ActiveVoices + " voice(s)");
        }
        Console.WriteLine(fails == 0 ? "=== synth checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
