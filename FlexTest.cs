using System;
using System.Collections.Generic;
using System.Diagnostics;

// Tests for the Flex Pitch engine (analysis, editing, resynthesis).   csc /main:F ... FlexTest.cs
static class F
{
    static int fails; const int SR = 44100;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    // a sung-like sound: harmonic series with a gentle envelope; freq(t) in Hz (0 = silence)
    static float[] Sing(int n, Func<double, double> freq, double amp)
    {
        var x = new float[n]; double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, f = freq(t); if (f <= 0) { ph = 0; continue; } ph += f / SR; double s = 0; for (int h = 1; h <= 20; h++) { if (h * f > 9000) break; s += Math.Sin(2 * Math.PI * ph * h) / (h * 0.9); }
            double env = Math.Min(1, Math.Min(t * 60, 1)); x[i] = (float)(amp * s * 0.5);
        }
        return x;
    }
    static double Cents(double a, double b) { return 1200 * Math.Log(a / b, 2); }
    // median pitch (Hz) of the analysed output between two times
    static double Med(FlexAnalysis a, double t0, double t1) { var v = new List<double>(); for (int f = (int)(t0 * SR / a.Hop); f < (int)(t1 * SR / a.Hop) && f < a.Frames; f++) if (a.F0[f] > 0) v.Add(a.F0[f]); if (v.Count == 0) return 0; v.Sort(); return v[v.Count / 2]; }
    static double Std(FlexAnalysis a, double t0, double t1) { var v = new List<double>(); for (int f = (int)(t0 * SR / a.Hop); f < (int)(t1 * SR / a.Hop) && f < a.Frames; f++) if (a.Midi[f] > 0) v.Add(a.Midi[f] * 100.0); if (v.Count < 3) return 0; double m = 0; foreach (var q in v) m += q; m /= v.Count; double s = 0; foreach (var q in v) s += (q - m) * (q - m); return Math.Sqrt(s / v.Count); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        // three notes: A3 (220), E4 (329.63), a gap, then C4 (261.63)
        Func<double, double> melody = t => t < 0.8 ? 220.0 : (t < 1.6 ? 329.63 : (t < 1.8 ? 0 : (t < 2.6 ? 261.63 : 0)));
        int n = (int)(2.8 * SR); var x = Sing(n, melody, 0.6); var xr = (float[])x.Clone();
        var sw = Stopwatch.StartNew(); var a = FlexPitch.Analyse(x, xr, n, SR, null, null); double ms = sw.Elapsed.TotalMilliseconds;
        Check("analysis finds the three notes", a != null && a.Notes.Count == 3, a == null ? "null" : a.Notes.Count + " notes, " + ms.ToString("0") + " ms for 2.8 s");
        if (a == null || a.Notes.Count != 3) { Console.WriteLine("=== " + (fails + 1) + " FAILED ==="); Environment.Exit(1); }
        double[] want = { 220.0, 329.63, 261.63 }; bool pitchOk = true; string det = ""; for (int i = 0; i < 3; i++) { double hz = FlexPitch.Hz(a.Notes[i].OrigMidi); double c = Cents(hz, want[i]); det += FlexNote.Name(a.Notes[i].OrigMidi) + " " + c.ToString("+0.0;-0.0") + "c  "; if (Math.Abs(c) > 15) pitchOk = false; }
        Check("each note's pitch is measured to within 15 cents", pitchOk, det);
        Check("note boundaries sit near the real note changes (0.8 s and 1.6 s, 1.8 s)", Math.Abs(a.Notes[0].End * a.Hop / (double)SR - 0.8) < 0.08 && Math.Abs(a.Notes[1].End * a.Hop / (double)SR - 1.6) < 0.08 && Math.Abs(a.Notes[2].Start * a.Hop / (double)SR - 1.8) < 0.08, "");
        // untouched: render must equal the original exactly
        float[] ol, orr; FlexPitch.Render(x, xr, n, a, out ol, out orr); double dif = 0; for (int i = 0; i < n; i++) dif = Math.Max(dif, Math.Abs(ol[i] - x[i])); Check("with no edits the audio is returned unchanged", dif < 1e-6, "largest difference " + dif.ToString("0.0e0"));
        // move note 1 up two semitones
        a.Notes[0].Shift = 2; sw.Restart(); FlexPitch.Render(x, xr, n, a, out ol, out orr); double rms = sw.Elapsed.TotalMilliseconds;
        var a2 = FlexPitch.Analyse(ol, orr, n, SR, null, null); double got = Med(a2, 0.25, 0.65), exp = 220.0 * Math.Pow(2, 2 / 12.0);
        Check("shifting a note up 2 semitones moves it to B3 (246.94 Hz)", Math.Abs(Cents(got, exp)) < 20, got.ToString("0.0") + " Hz (" + Cents(got, exp).ToString("+0;-0") + " cents), rendered in " + rms.ToString("0") + " ms");
        Check("the other notes are untouched", Math.Abs(Cents(Med(a2, 1.0, 1.5), 329.63)) < 10 && Math.Abs(Cents(Med(a2, 1.95, 2.5), 261.63)) < 10, "");
        double dif2 = 0; for (int i = (int)(1.7 * SR); i < n; i++) dif2 = Math.Max(dif2, Math.Abs(ol[i] - x[i])); Check("audio away from the edited note is bit-for-bit the same", dif2 < 1e-6, dif2.ToString("0.0e0"));
        Check("the length is kept (same number of samples, no gap or silence)", ol.Length == n, ""); double er = 0, eo = 0; for (int i = (int)(0.2 * SR); i < (int)(0.7 * SR); i++) { er += ol[i] * ol[i]; eo += x[i] * x[i]; } Check("the edited note keeps its loudness (within 3 dB)", Math.Abs(10 * Math.Log10(er / eo)) < 3, (10 * Math.Log10(er / eo)).ToString("0.0") + " dB");
        // fine pitch and big shifts
        a.Notes[0].Shift = 0; a.Notes[0].Fine = 50; FlexPitch.Render(x, xr, n, a, out ol, out orr); a2 = FlexPitch.Analyse(ol, orr, n, SR, null, null); Check("fine pitch +50 cents works", Math.Abs(Cents(Med(a2, 0.25, 0.65), 220.0 * Math.Pow(2, 0.5 / 12)) ) < 15, Cents(Med(a2, 0.25, 0.65), 220.0).ToString("0") + " cents above A3");
        a.Notes[0].Fine = 0; a.Notes[1].Shift = -7; FlexPitch.Render(x, xr, n, a, out ol, out orr); a2 = FlexPitch.Analyse(ol, orr, n, SR, null, null); Check("a big move down a fifth (E4 to A3) works", Math.Abs(Cents(Med(a2, 0.95, 1.5), 329.63 * Math.Pow(2, -7 / 12.0))) < 25, Med(a2, 0.95, 1.5).ToString("0.0") + " Hz"); a.Notes[1].Shift = 0;
        // gain
        a.Notes[2].GainDb = 6; FlexPitch.Render(x, xr, n, a, out ol, out orr); er = 0; eo = 0; for (int i = (int)(2.0 * SR); i < (int)(2.5 * SR); i++) { er += ol[i] * ol[i]; eo += x[i] * x[i]; } Check("note gain +6 dB", Math.Abs(10 * Math.Log10(er / eo) - 6) < 0.6, (10 * Math.Log10(er / eo)).ToString("0.0") + " dB"); a.Notes[2].GainDb = 0;
        // delete a note: not processed
        a.Notes[0].Shift = 3; a.Notes[0].Deleted = true; FlexPitch.Render(x, xr, n, a, out ol, out orr); dif = 0; for (int i = 0; i < n; i++) dif = Math.Max(dif, Math.Abs(ol[i] - x[i])); Check("a deleted note is left as recorded", dif < 1e-6, ""); a.Notes[0].Shift = 0; a.Notes[0].Deleted = false;
        // formant: a vowel-like sound (energy around 900 Hz) so there is a formant to move
        {
            int vn = (int)(1.6 * SR); var vx = new float[vn]; double vph = 0; for (int i = 0; i < vn; i++) { vph += 330.0 / SR; double s = 0; for (int h = 1; h <= 25; h++) { double fr = 330.0 * h; if (fr > 9000) break; double wgt = 0.15 + 3.0 * Math.Exp(-Math.Pow((fr - 900) / 450, 2)) + 1.2 * Math.Exp(-Math.Pow((fr - 2400) / 600, 2)); s += Math.Sin(2 * Math.PI * vph * h) * wgt / Math.Sqrt(h); } vx[i] = (float)(0.2 * s); }
            var vxr = (float[])vx.Clone(); var af = FlexPitch.Analyse(vx, vxr, vn, SR, null, null); Func<float[], double> centroid = (s) => { var re = new double[16384]; var im = new double[16384]; for (int i = 0; i < 16384; i++) re[i] = s[SR / 4 + i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 16384)); Fft.Transform(re, im, false); double num = 0, den = 0; for (int k = 1; k < 8192; k++) { double p = re[k] * re[k] + im[k] * im[k]; num += p * k * SR / 16384.0; den += p; } return num / den; };
            double c0 = centroid(vx); foreach (var nt in af.Notes) nt.Formant = -4; float[] fl, fr2; FlexPitch.Render(vx, vxr, vn, af, out fl, out fr2); double c1 = centroid(fl); var afo = FlexPitch.Analyse(fl, fr2, vn, SR, null, null);
            Check("formant -4 darkens the vowel (brightness drops) without changing its pitch", af.Notes.Count >= 1 && c1 < c0 * 0.97 && Math.Abs(Cents(Med(afo, 0.3, 1.3), 330.0)) < 20, "brightness " + c0.ToString("0") + " -> " + c1.ToString("0") + " Hz, pitch " + Med(afo, 0.3, 1.3).ToString("0.0") + " Hz");
            foreach (var nt in af.Notes) nt.Formant = 4; FlexPitch.Render(vx, vxr, vn, af, out fl, out fr2); Check("formant +4 brightens it", centroid(fl) > c0 * 1.03, centroid(fl).ToString("0") + " Hz");
        }

        // drift and vibrato
        Func<double, double> wob = t => t < 1.6 ? 220.0 * Math.Pow(2, 0.4 * Math.Sin(2 * Math.PI * 0.6 * t) / 12.0) : 0;   // +-40 cents slow wandering
        var w = Sing((int)(1.8 * SR), wob, 0.6); var wr = (float[])w.Clone(); var aw = FlexPitch.Analyse(w, wr, w.Length, SR, null, null); Check("a wandering note is found as one note", aw.Notes.Count >= 1 && aw.Notes.Count <= 2, aw.Notes.Count + " notes");
        double before = Std(aw, 0.3, 1.4); foreach (var nt in aw.Notes) nt.Drift = 100; float[] dl, dr; FlexPitch.Render(w, wr, w.Length, aw, out dl, out dr); var ad = FlexPitch.Analyse(dl, dr, w.Length, SR, null, null); double after = Std(ad, 0.3, 1.4); Check("pitch drift at 100% steadies a wandering note", after < before * 0.6, before.ToString("0") + " cents spread -> " + after.ToString("0"));
        Func<double, double> vib = t => t < 1.6 ? 220.0 * Math.Pow(2, 0.5 * Math.Sin(2 * Math.PI * 5.5 * t) / 12.0) : 0;   // +-50 cents vibrato at 5.5 Hz
        var v = Sing((int)(1.8 * SR), vib, 0.6); var vr = (float[])v.Clone(); var av = FlexPitch.Analyse(v, vr, v.Length, SR, null, null); double vb = Std(av, 0.3, 1.4); foreach (var nt in av.Notes) nt.Vib = 0; FlexPitch.Render(v, vr, v.Length, av, out dl, out dr); var avo = FlexPitch.Analyse(dl, dr, v.Length, SR, null, null); double va = Std(avo, 0.3, 1.4);
        Check("vibrato at 0% flattens the vibrato", va < vb * 0.6, vb.ToString("0") + " cents -> " + va.ToString("0")); foreach (var nt in av.Notes) nt.Vib = 180; FlexPitch.Render(v, vr, v.Length, av, out dl, out dr); avo = FlexPitch.Analyse(dl, dr, v.Length, SR, null, null); Check("vibrato at 180% exaggerates it", Std(avo, 0.3, 1.4) > vb * 1.3, Std(avo, 0.3, 1.4).ToString("0") + " cents");
        // split and join
        var a3 = FlexPitch.Analyse(x, xr, n, SR, null, null); var first = a3.Notes[0]; int mid = (first.Start + first.End) / 2; var sec = FlexPitch.Split(a3, first, mid); Check("a note can be split in two", sec != null && a3.Notes.Count == 4 && first.End == mid && sec.Start == mid, ""); Check("and joined again", FlexPitch.Join(a3, first) && a3.Notes.Count == 3, "");
        // snapping to a scale
        Check("snapping to C major takes C# to C or D, and 61.4 to 61 (C#) when chromatic", FlexPitch.Snap(61.4, 4095) == 61 && (FlexPitch.Snap(61.1, 0x5AB5) == 60 || FlexPitch.Snap(61.1, 0x5AB5) == 62), "");
        // speed and silence
        var big = Sing(SR * 30, t => 220.0 * Math.Pow(2, ((int)(t * 2) % 5) * 2 / 12.0), 0.5); var br2 = (float[])big.Clone(); sw.Restart(); var ab = FlexPitch.Analyse(big, br2, big.Length, SR, null, null); double ta = sw.Elapsed.TotalSeconds; foreach (var nt in ab.Notes) nt.Shift = 1; sw.Restart(); FlexPitch.Render(big, br2, big.Length, ab, out dl, out dr); double tr = sw.Elapsed.TotalSeconds;
        Check("a 30 second take is analysed and re-tuned quickly", ta < 12 && tr < 4, "analysis " + ta.ToString("0.0") + " s (" + ab.Notes.Count + " notes), render " + tr.ToString("0.00") + " s");
        var silent = new float[SR * 2]; var asl = FlexPitch.Analyse(silent, silent, silent.Length, SR, null, null); Check("silence gives no notes and does not crash", asl.Notes.Count == 0, "");
        var noise = new float[SR * 2]; var rnd = new Random(3); for (int i = 0; i < noise.Length; i++) noise[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.3); var an = FlexPitch.Analyse(noise, noise, noise.Length, SR, null, null); Check("noise gives few or no notes", an.Notes.Count <= 2, an.Notes.Count + " notes");
        bool finite = true; for (int i = 0; i < dl.Length; i++) if (float.IsNaN(dl[i]) || float.IsInfinity(dl[i])) finite = false; Check("rendered audio is finite", finite, "");
        // ---- moving a note in time: it keeps its length and pitch, the gap before it shrinks, the rest stays where it was ----
        {
            var tma = FlexPitch.Analyse(x, xr, n, SR, null, null); var c4 = tma.Notes[tma.Notes.Count - 1]; int mv = -(int)Math.Round(0.1 * SR / tma.Hop); double sh = mv * tma.Hop / (double)SR; c4.Move = mv; float[] tl, tmr; FlexPitch.Render(x, xr, n, tma, out tl, out tmr);
            Func<float[], double, double, bool, double> edge = (y, from, to, rising) => { int ww = 441; for (int i = (int)(from * SR); i + ww < Math.Min(y.Length, (int)(to * SR)); i += 110) { double s = 0; for (int k = i; k < i + ww; k++) s += y[k] * y[k]; double r = Math.Sqrt(s / ww); if (rising ? r > 0.2 : r < 0.05) return (i + ww / 2) / (double)SR; } return -1; };
            double on0 = edge(x, 1.62, 2.7, true), on1 = edge(tl, 1.62, 2.7, true), off0 = edge(x, on0 + 0.2, 2.8, false), off1 = edge(tl, on1 + 0.2, 2.8, false);
            Check("a note dragged sideways starts earlier by the amount it was moved", Math.Abs((on1 - on0) - sh) < 0.015, "onset " + on0.ToString("0.000") + " -> " + on1.ToString("0.000") + " s (moved " + sh.ToString("0.000") + ")");
            Check("and keeps its length (it ends earlier by the same amount)", Math.Abs((off1 - off0) - sh) < 0.02, "end " + off0.ToString("0.000") + " -> " + off1.ToString("0.000") + " s");
            var tb = FlexPitch.Analyse(tl, tmr, n, SR, null, null); double hz = Med(tb, on1 + 0.1, off1 - 0.1); Check("and keeps its pitch (still C4)", Math.Abs(Cents(hz, 261.63)) < 15, hz.ToString("0.0") + " Hz");
            double d0 = 0; for (int i = 0; i < (int)(0.75 * SR); i++) d0 = Math.Max(d0, Math.Abs(tl[i] - x[i])); Check("the notes before it are untouched", d0 < 1e-3, d0.ToString("0.000000"));
            Check("the song stays the same length", tl.Length == n && tmr.Length == n, "");
            double jx = 0, jo = 0; for (int i = 1; i < n; i++) { jx = Math.Max(jx, Math.Abs(x[i] - x[i - 1])); jo = Math.Max(jo, Math.Abs(tl[i] - tl[i - 1])); } Check("no clicks where the audio was stretched", jo < jx * 1.6, jx.ToString("0.000") + " -> " + jo.ToString("0.000"));
            c4.Move = 0; FlexPitch.Render(x, xr, n, tma, out tl, out tmr); double dz = 0; for (int i = 0; i < n; i++) dz = Math.Max(dz, Math.Abs(tl[i] - x[i])); Check("moved back to 0 the audio is exactly as it was", dz < 1e-6, dz.ToString("0.0000000"));
            c4.Move = -500; var anc = FlexTime.Anchors(tma, n); bool mono = true; for (int i = 1; i < anc.Count; i++) if (anc[i][0] <= anc[i - 1][0] || anc[i][1] <= anc[i - 1][1]) mono = false; Check("even a move past the previous note never folds time back (the map always runs forward)", mono, ""); c4.Move = 0;
        }
        Console.WriteLine(fails == 0 ? "=== flex pitch checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
