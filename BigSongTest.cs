using System;
using System.Diagnostics;
using System.IO;

// Long-song stress test:  csc /main:B ... BigSongTest.cs   (generates a 60 minute file, ~650 MB on disk)
static class B
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string Mem() { var pr = Process.GetCurrentProcess(); return "managed " + (GC.GetTotalMemory(false) / 1048576) + " MB, peak working set " + (pr.PeakWorkingSet64 / 1048576) + " MB"; }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; P((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }

    static void Main(string[] a)
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        string dir = Path.Combine(Path.GetTempPath(), "bigsong"); Directory.CreateDirectory(dir); string big = Path.Combine(dir, "mix60.wav");
        int minutes = a.Length > 0 ? int.Parse(a[0]) : 60;
        if (!File.Exists(big) || new FileInfo(big).Length < minutes * 60L * 44100 * 4 * 0.9)
        {
            P("generating a " + minutes + " minute test file...");
            var psi = new ProcessStartInfo(ff, "-y -loglevel error -f lavfi -i \"sine=f=220:d=" + minutes * 60 + ":r=44100\" -f lavfi -i \"anoisesrc=d=" + minutes * 60 + ":c=pink:r=44100:a=0.06\" -filter_complex \"[0:a]volume=0.25[a];[a][1:a]amix=inputs=2:duration=first,pan=stereo|c0=c0|c1=c0\" -c:a pcm_s16le \"" + big + "\"") { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(psi)) p.WaitForExit();
        }
        P("test file: " + new FileInfo(big).Length / 1048576 + " MB");

        var sw = Stopwatch.StartNew(); int last = -1;
        var clip = AudioClip.FromFile(big, pr => { int q = (int)(pr * 4); if (q != last) { last = q; P("  decoding " + (int)(pr * 100) + "%"); } }, null);
        double secs = clip.Length / 44100.0;
        Check("decode: a " + minutes + " minute song loads as a stream", Math.Abs(secs - minutes * 60) < 2, secs.ToString("0.0") + " s of audio in " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s;  " + Mem());

        // waveform overview works at any zoom
        var mn = new float[1000]; var mx = new float[1000]; clip.GetPeaks(0, clip.Length / 1000.0, 1000, mn, mx); float pk = 0; for (int i = 0; i < 1000; i++) pk = Math.Max(pk, Math.Max(mx[i], -mn[i]));
        Check("waveform overview of the whole song", pk > 0.02f, "peak " + pk.ToString("0.00"));

        // mixer: two tracks with plugins, streamed export to every format
        var eng = new StudioEngine(); var tr = StudioEngine.NewTrack("Long", "other", clip); foreach (var f in Presets.BuildChain("Clean Studio")) tr.Fx.Add(f); eng.Tracks.Add(tr);
        string outDir = Path.Combine(dir, "out"); if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        foreach (var fmt in new[] { 0, 3, 4 })
        {
            sw.Restart(); var it = Exporter.Plan(eng, true, false, false, false, false, false, false, "big", fmt)[0]; last = -1;
            string o = Exporter.Write(eng, it, outDir, fmt, pr => { int q = (int)(pr * 4); if (q != last) { last = q; P("  exporting " + Exporter.Formats[fmt] + " " + (int)(pr * 100) + "%"); } });
            double d = AudioClip.ProbeSeconds(o);
            Check("export " + Exporter.Formats[fmt] + ": streamed to disk", Math.Abs(d - (secs + 2)) < 3, new FileInfo(o).Length / 1048576 + " MB, " + d.ToString("0.0") + " s long, took " + sw.Elapsed.TotalSeconds.ToString("0") + " s;  " + Mem());
        }

        // unload frees the samples; the mixer treats them as silence; reload restores them
        long before = GC.GetTotalMemory(true); clip.Unload(); GC.Collect(); long after = GC.GetTotalMemory(true);
        Check("unload: the song's samples leave memory", before - after > minutes * 60L * 44100 * 6, ((before - after) / 1048576) + " MB freed");
        clip.GetPeaks(clip.Length / 2, 441000, 5, mn, mx);
        Check("unload: waveform still draws", mx[0] > 0.01f, "peak " + mx[0].ToString("0.00"));
        sw.Restart(); clip.Reload(null, null); Check("reload: samples come back from disk", clip.Loaded && Math.Abs(clip.Length / 44100.0 - secs) < 0.01, sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
        P("final memory: " + Mem());
        Console.WriteLine(fails == 0 ? "=== all long-song checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
