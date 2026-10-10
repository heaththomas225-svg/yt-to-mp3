using System;
using System.Collections.Generic;

// Tests the DAW editing model: trim / split playback, project save of trims, tempo detection, Auto Mix on an unsplit song.   csc /main:D ... DawTest.cs
static class D
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static AudioClip BeatClip(double bpm, int seconds)
    {
        int sr = 44100, n = sr * seconds; var l = new float[n]; var rnd = new Random(5); double beat = sr * 60.0 / bpm;
        for (int b = 0; b * beat < n; b++) { int s = (int)(b * beat); for (int i = 0; i < 4000 && s + i < n; i++) l[s + i] += (float)(Math.Sin(2 * Math.PI * (60 + 80 * Math.Exp(-i / 500.0)) * i / sr) * Math.Exp(-i / 1200.0) * 0.9); int h = (int)(s + beat / 2); for (int i = 0; i < 900 && h + i < n; i++) l[h + i] += (float)((rnd.NextDouble() * 2 - 1) * Math.Exp(-i / 200.0) * 0.3); }
        return AudioClip.FromArrays(l, (float[])l.Clone(), "beat");
    }
    static void Main()
    {
        foreach (double bpm in new[] { 90.0, 128.0, 140.0 })
        {
            var c = BeatClip(bpm, 70); double d = Tempo.Detect(c, 0, 60);
            Check("tempo detection finds " + bpm + " BPM", Math.Abs(d - bpm) <= 1.5, "heard " + d.ToString("0.0") + " BPM");
        }
        // trimmed / moved clip plays only its part, at the right place
        int sr = 44100; var l = new float[sr * 4]; for (int i = 0; i < l.Length; i++) l[i] = (float)(i < sr * 2 ? 0.5 : -0.5);   // first 2 s are +0.5, last 2 s are -0.5
        var clip = AudioClip.FromArrays(l, (float[])l.Clone(), "t"); var eng = new StudioEngine(); foreach (var m in eng.MasterFx) m.Bypass = true;
        var t = StudioEngine.NewTrack("T", "other", clip); t.ClipStart = sr * 2; t.ClipFrames = sr; t.Offset = sr * 3; eng.Tracks.Add(t);   // use only seconds 2..3 of the file (the -0.5 part), placed at 3 s
        var o = new float[sr * 6]; long w = 0; eng.RenderStream(0, sr * 6, new RenderOpts { Master = false, Fader = false }, null, (bl, br, off, c2) => { Array.Copy(bl, off, o, w, c2); w += c2; }, null);
        Check("trimmed clip: silent before its start", Math.Abs(o[sr * 3 - 100]) < 1e-6, o[sr * 3 - 100].ToString());
        Check("trimmed clip: plays the chosen part (the -0.5 section)", Math.Abs(o[sr * 3 + 1000] + 0.5) < 1e-4 && Math.Abs(o[sr * 4 - 1000] + 0.5) < 1e-4, o[sr * 3 + 1000].ToString("0.00"));
        Check("trimmed clip: silent after its end", Math.Abs(o[sr * 4 + 1000]) < 1e-6, o[sr * 4 + 1000].ToString());
        Check("song length follows the trim", Math.Abs(t.EndFrame - sr * 4) < 2, t.EndFrame.ToString());
        // save and reopen keeps the trim
        string pf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dawtest.ytmproj"); System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dawtest.wav"), new byte[0]);
        t.Source = "x"; ProjectIO.Save(eng, pf); var e2 = new StudioEngine(); ProjectIO.Load(e2, pf, s => { });
        Check("project keeps the trim and position", e2.Tracks.Count == 1 && e2.Tracks[0].ClipStart == sr * 2 && e2.Tracks[0].ClipFrames == sr && e2.Tracks[0].Offset == sr * 3, e2.Tracks.Count == 1 ? e2.Tracks[0].ClipStart + "/" + e2.Tracks[0].ClipFrames + "/" + e2.Tracks[0].Offset : "none");
        // Auto Mix works on a full song that has not been split
        var e3 = new StudioEngine(); var song = StudioEngine.NewTrack("Song", "song", BeatClip(100, 20)); e3.Tracks.Add(song);
        var rep = AutoMix.Run(e3, "Natural", false, s => { });
        bool ok = rep.Count > 0 && !rep[0].StartsWith("Nothing to mix") && song.Fx.Count > 0; string txt = rep.Count > 0 ? rep[0].Substring(0, Math.Min(60, rep[0].Length)) : "";
        Check("Auto Mix works before the song is split into vocals and beat", ok, "effects added: " + song.Fx.Count + ", fader " + song.VolDb.ToString("0.0") + " dB; " + txt);
        // mastering analysis moves the EQ the right way
        { int n = 44100 * 12; var bl = new float[n]; var rn = new Random(3); for (int i = 0; i < n; i++) bl[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 120 * i / 44100.0) + 0.05 * (rn.NextDouble() - 0.5)); var sb = AutoMix.Analyze(AudioClip.FromArrays(bl, (float[])bl.Clone(), "bassy")); double[] dv; var gn = StudioForm.MasterToneGains(sb, out dv);
          Check("a bass-heavy mix gets its bass turned down by the master EQ", gn[0] < -1.0, "bass band gain " + gn[0].ToString("0.0") + " dB (bass is " + dv[0].ToString("0.0") + " dB over a balanced mix)");
          var bright = new float[n]; for (int i = 0; i < n; i++) bright[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 6000 * i / 44100.0) + 0.05 * (rn.NextDouble() - 0.5)); var sbr = AutoMix.Analyze(AudioClip.FromArrays(bright, (float[])bright.Clone(), "bright")); var gb = StudioForm.MasterToneGains(sbr, out dv);
          Check("a harsh bright mix gets its highs turned down", gb[3] < -1.0 || gb[4] < -1.0 || gb[2] < -1.0, "upper bands " + gb[2].ToString("0.0") + " / " + gb[3].ToString("0.0") + " / " + gb[4].ToString("0.0") + " dB"); }
        Console.WriteLine(fails == 0 ? "=== DAW checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
