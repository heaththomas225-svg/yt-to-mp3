// Purphex core smoke test: runs on Windows, macOS and Linux (dotnet run). It proves the shared engine works on the platform:
//  1. every built-in plugin type is made, prepared and fed audio: the output must stay finite (no NaN / infinity)
//  2. a small song (an audio track through EQ, compressor and reverb, plus Bass Lab playing notes) is rendered through the master
//  3. the mix is measured (loudness, true peak) and written to a WAV file
// Exit code 0 = everything passed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class Smoke
{
    static int fails;
    static void Check(string what, bool ok, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what + (detail.Length > 0 ? "   " + detail : "")); if (!ok) fails++; }

    static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "purphex-smoke"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(outDir, "data"); Directory.CreateDirectory(StudioEnv.DataDir);
        Console.WriteLine("Purphex core smoke test on " + System.Runtime.InteropServices.RuntimeInformation.OSDescription + " (" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ", .NET " + Environment.Version + ")");
        int sr = StudioEngine.SR, n = 4096; var rnd = new Random(1);

        // 1. every plugin type
        var types = FxRegistry.All.Select(i => i.Type).Where(t => t != "gain").Distinct().ToList(); int made = 0, bad = 0; var badNames = new List<string>();
        foreach (var type in types)
        {
            try
            {
                var f = FxRegistry.Create(type); if (f == null) continue; f.Prepare(sr); made++;
                var l = new float[n]; var r = new float[n];
                for (int b = 0; b < 4; b++) { for (int i = 0; i < n; i++) { float v = (float)(Math.Sin((b * n + i) * 0.031) * 0.4 + (rnd.NextDouble() - 0.5) * 0.2); l[i] = v; r[i] = v * 0.9f; } f.Process(l, r, n); }
                if (l.Any(x => float.IsNaN(x) || float.IsInfinity(x)) || r.Any(x => float.IsNaN(x) || float.IsInfinity(x))) { bad++; badNames.Add(type); }
            }
            catch (Exception e) { bad++; badNames.Add(type + " (" + e.GetType().Name + ": " + e.Message + ")"); }
        }
        Check("every plugin type runs and its output stays finite", made > 100 && bad == 0, made + " plugin types, " + bad + " bad" + (badNames.Count > 0 ? ": " + string.Join(", ", badNames.Take(8)) : ""));

        // 2. a small song through the engine
        var eng = new StudioEngine(); int len = sr * 6; var cl = new float[len]; var cr = new float[len];
        double[] chord = { 220, 261.63, 329.63 }; for (int i = 0; i < len; i++) { double t = i / (double)sr, v = 0; foreach (var fq in chord) v += Math.Sin(2 * Math.PI * fq * t) * 0.18; v *= 0.6 + 0.4 * Math.Sin(2 * Math.PI * 0.5 * t); cl[i] = (float)v; cr[i] = (float)(v * 0.95); }
        var keys = StudioEngine.NewTrack("Keys", "audio", AudioClip.FromArrays(cl, cr, "Keys")); foreach (var type in new[] { "eq", "comp", "reverb" }) { var f = FxRegistry.Create(type); if (f != null) { f.Prepare(sr); keys.Fx.Add(f); } }
        var bass = StudioEngine.NewTrack("Bass", "inst", null); bass.Instrument = true; var synth = FxRegistry.Create("synth") as InstrumentFx; Check("Bass Lab (the synth) can be made", synth != null);
        if (synth != null) { synth.Prepare(sr); synth.Owner = bass; bass.Fx.Insert(0, synth); }
        int[] notes = { 45, 45, 48, 43 }; for (int k = 0; k < notes.Length; k++) { long at = (long)(k * 1.4 * sr); bass.Midi.Add(new MidiEv { Frame = at, S = 0x90, D1 = (byte)notes[k], D2 = 100 }); bass.Midi.Add(new MidiEv { Frame = at + (long)(1.1 * sr), S = 0x80, D1 = (byte)notes[k], D2 = 0 }); }
        lock (eng.Sync) { eng.Tracks.Add(keys); eng.Tracks.Add(bass); }
        float[] oL, oR; eng.Render(0, len, new RenderOpts { Master = true }, p => { }, out oL, out oR);
        Check("the song renders through the master", oL != null && oL.Length >= len, oL == null ? "nothing" : oL.Length + " frames");
        bool finite = oL != null && !oL.Any(x => float.IsNaN(x) || float.IsInfinity(x)) && !oR.Any(x => float.IsNaN(x) || float.IsInfinity(x)); Check("the mix is finite (no NaN or infinity)", finite);
        // 3. measure and write
        if (oL != null)
        {
            var lr = Loudness.Measure(oL, oR, sr, 0, oL.Length); Check("the mix has sound at a sane level", lr.I > -45 && lr.I < 0, "integrated " + lr.I.ToString("0.0") + " LUFS, true peak " + lr.TruePeakDb.ToString("0.0") + " dBTP");
            double bassEnergy = 0; for (int i = 0; i < len; i++) bassEnergy += Math.Abs(oL[i]); Check("the instrument track adds sound", bassEnergy > 100, bassEnergy.ToString("0"));
            string wav = Path.Combine(outDir, "purphex-smoke.wav"); WavIO.Write(wav, oL, oR, sr, 16); Check("the mix is written to a WAV file", File.Exists(wav) && new FileInfo(wav).Length > len * 2, wav);
        }
        // 4. live playback through the platform sound output (SDL: CoreAudio, PulseAudio/PipeWire/ALSA, WASAPI; a dummy device on servers)
        bool audioOk = PlatformAudio.Install(); Check("the sound output starts", audioOk, PlatformAudio.Status);
        if (audioOk)
        {
            try { eng.Seek(0); eng.Play(); var sw = System.Diagnostics.Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < 1500) System.Threading.Thread.Sleep(20); long heard = eng.HeardPos; eng.Stop(); eng.Shutdown();
                  Check("the song plays in real time through the device", heard > StudioEngine.SR / 2 && heard < StudioEngine.SR * 4, (heard / (double)StudioEngine.SR).ToString("0.00") + " s heard after 1.5 s, " + AudioOut.Underruns + " underruns"); }
            catch (Exception e) { Check("the song plays in real time through the device", false, e.Message); }
        }
        Console.WriteLine(fails == 0 ? "=== core smoke test passed ===" : "=== " + fails + " FAILED ===");
        return fails == 0 ? 0 : 1;
    }
}
