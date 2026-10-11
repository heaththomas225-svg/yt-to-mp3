using System;
using System.Collections.Generic;
using System.Reflection;

// Plays a pure tone through the real-time mixer (nothing is sent to a sound card) and looks for clicks, steps and distortion at the block edges.   csc /main:CK ... ClickTest.cs
static class CK
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }
    static AudioClip Tone(double sec, double amp, double hz) { int n = (int)(44100 * sec); var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / 44100.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "tone"); }
    // renders blocks the way the sound card thread does and returns the largest jump between neighbouring samples, and the peak
    static void Run(StudioEngine eng, int blocks, int frames, out double maxStep, out double peak, out double stepAtEdges)
    {
        var l = new float[frames]; var r = new float[frames]; maxStep = 0; peak = 0; stepAtEdges = 0; double prev = double.NaN; eng.Playing = true;
        for (int b = 0; b < blocks; b++)
        {
            eng.RenderRealtime(l, r, frames);
            for (int i = 0; i < frames; i++) { double v = l[i]; peak = Math.Max(peak, Math.Abs(v)); if (!double.IsNaN(prev)) { double d = Math.Abs(v - prev); if (b > 0 || i > 0) { maxStep = Math.Max(maxStep, d); if (i == 0) stepAtEdges = Math.Max(stepAtEdges, d); } } prev = v; }
        }
        eng.Playing = false;
    }
    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        var eng = new StudioEngine(); double step = 2 * Math.PI * 440 / 44100.0 * 0.5;   // the biggest step a clean 0.5 tone makes (scaled by the real peak below)
        double ms, pk, edge;
        var t = StudioEngine.NewTrack("A", "vocal", Tone(8, 0.5, 440)); eng.Tracks.Add(t); eng.Seek(0); Run(eng, 120, 1024, out ms, out pk, out edge);
        Check("a plain tone has no clicks at the block edges", ms < step * pk * 2.0 * 1.12 && edge < step * pk * 2.0 * 1.12, "largest step " + ms.ToString("0.0000") + " (clean " + step.ToString("0.0000") + "), at edges " + edge.ToString("0.0000"));
        t.Mono = true; eng.Seek(0); Run(eng, 60, 1024, out ms, out pk, out edge); Check("MONO has no clicks", ms < step * pk * 2.0 * 1.12, ms.ToString("0.0000")); t.Mono = false;
        t.FadeIn = 44100; t.FadeOut = 44100; eng.Seek(0); Run(eng, 120, 1024, out ms, out pk, out edge); Check("fades have no clicks", ms < step * pk * 2.0 * 1.12, ms.ToString("0.0000")); t.FadeIn = 0; t.FadeOut = 0;
        t.ClipGainDb = -6; eng.Seek(0); Run(eng, 60, 1024, out ms, out pk, out edge); Check("clip gain has no clicks", ms < step * pk * 2.0 * 1.12, ms.ToString("0.0000")); t.ClipGainDb = 0;
        t.Pan = -0.6f; eng.Seek(0); Run(eng, 60, 1024, out ms, out pk, out edge); Check("pan has no clicks", ms < step * pk * 2.0 * 1.12, ms.ToString("0.0000")); t.Pan = 0;
        foreach (int fr in new[] { 512, 1024, 2048 }) { eng.Seek(0); Run(eng, 60, fr, out ms, out pk, out edge); Check("no clicks with " + fr + " frame blocks", ms < step * pk * 2.0 * 1.12, ms.ToString("0.0000")); }
        // several loud tracks together: the sum goes past 1.0, which would be hard clipped at the sound card
        for (int i = 0; i < 3; i++) eng.Tracks.Add(StudioEngine.NewTrack("L" + i, "beat", Tone(8, 0.7, 440 + i * 3)));
        eng.Seek(0); Run(eng, 80, 1024, out ms, out pk, out edge); Console.WriteLine("INFO  four loud tracks: peak " + pk.ToString("0.00") + ", largest step " + ms.ToString("0.000"));
        Check("loud projects do not go over full scale before the sound card (they would crackle)", pk <= 1.0001, "peak " + pk.ToString("0.000"));
        // the last guard before the sound card
        { var sf = new OutSafety(); var l = new float[44100]; var r = new float[44100]; for (int i = 0; i < l.Length; i++) { l[i] = (float)(2.5 * Math.Sin(2 * Math.PI * 110 * i / 44100.0)); r[i] = l[i]; } sf.Process(l, r, l.Length); double mx = 0, mxs = 0; for (int i = 0; i < l.Length; i++) { mx = Math.Max(mx, Math.Abs(l[i])); if (i > 0) mxs = Math.Max(mxs, Math.Abs(l[i] - l[i - 1])); }
          Check("a far too loud signal never goes past full scale", mx <= 1.0, "peak " + mx.ToString("0.000")); Check("and it is turned down, not chopped off (still loud)", mx > 0.9, "peak " + mx.ToString("0.000"));
          var q = new float[4410]; var q2 = new float[4410]; for (int i = 0; i < q.Length; i++) { q[i] = (float)(0.4 * Math.Sin(i * 0.05)); q2[i] = q[i]; } var sf2 = new OutSafety(); sf2.Process(q, q2, q.Length); bool same = true; for (int i = 0; i < q.Length; i++) if (Math.Abs(q[i] - 0.4 * Math.Sin(i * 0.05)) > 1e-6) same = false; Check("a normal signal is not touched at all", same, ""); }
        eng.Shutdown(); Console.WriteLine(fails == 0 ? "=== click checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
