using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

// Separates a whole song the way the studio does and prints the progress timeline, to check the total time and how good the "time left" guess is.
//   csc /main:FlowT ... SepFlowTest.cs
static class FlowT
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }

    static void Main(string[] args)
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        string music = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"); string src = null; double best = 0;
        foreach (var f in Directory.GetFiles(music, "*.mp3")) { double d = AudioClip.ProbeSeconds(f); if (d > best) { best = d; src = f; } }
        if (src == null) { P("no song"); Environment.Exit(2); }
        string dir = Path.Combine(Path.GetTempPath(), "sepflow"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        P("song: " + Path.GetFileName(src) + "  " + best.ToString("0") + " s (" + (best / 60).ToString("0.0") + " min)   engine: fast=" + Separator.FastReady);
        var sw = Stopwatch.StartNew(); var samples = new List<double[]>(); double lastP = -1; string v, i; double tFirst = -1;
        bool ok = Separator.Separate(src, dir, "song", "htdemucs", s => P("  " + s), p => { if (p != lastP) { lastP = p; samples.Add(new[] { sw.Elapsed.TotalSeconds, p }); if (tFirst < 0 && p > 0) tFirst = sw.Elapsed.TotalSeconds; } }, () => false, out v, out i);
        double total = sw.Elapsed.TotalSeconds;
        P("finished: " + ok + " in " + total.ToString("0.0") + " s   (" + (best / total).ToString("0.0") + "x realtime)   device " + Separator.Device + "   first progress after " + tFirst.ToString("0.0") + " s");
        foreach (var s in samples) { if (s[1] < 0.03 || s[1] > 0.99) continue; double el = s[0], p = s[1]; double naive = el * (1 - p) / p; double actual = total - el; if (samples.IndexOf(s) % 4 == 0) P("   at " + el.ToString("0.0") + " s  progress " + (p * 100).ToString("0") + "%   old guess " + naive.ToString("0") + " s left, really " + actual.ToString("0") + " s left"); }
        Separator.KillWorker(); try { Directory.Delete(dir, true); } catch { }
    }
}
