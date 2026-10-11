using System;
using System.Diagnostics;
using System.IO;

// Times the vocal separation on a real song with the old engine and with the fast one, and checks the results match.
//   csc /main:SpeedT ... SepSpeedTest.cs      (installs the fast stack the first time: about 300 MB)
static class SpeedT
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; P((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }

    static void Main()
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        string dir = Path.Combine(Path.GetTempPath(), "sepspeed"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        string music = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"); string src = null;
        foreach (var f in Directory.GetFiles(music, "*.mp3")) { src = f; break; }
        if (src == null) { P("no test song found"); Environment.Exit(2); }
        string song = Path.Combine(dir, "song.wav");
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -ss 40 -t 60 -i \"" + src + "\" -ar 44100 -ac 2 \"" + song + "\"") { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        P("test song: 60 s from " + Path.GetFileName(src));
        Check("the engine is installed", Separator.Installed, "");
        // old engine
        Separator.DisableFast = true; Separator.KillWorker(); string v1, i1, v2, i2;
        Separator.Separate(song, Path.Combine(dir, "warm"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v1, out i1);   // loads the model
        var sw = Stopwatch.StartNew();
        bool ok1 = Separator.Separate(song, Path.Combine(dir, "old"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v1, out i1); double tOld = sw.Elapsed.TotalSeconds;
        P("OLD engine: " + tOld.ToString("0.0") + " s   device " + Separator.Device);
        // fast engine
        Separator.DisableFast = false; Separator.KillWorker();
        if (!Separator.FastReady) Separator.FastInstall(s => P("  " + s), p => { }, () => false);
        Check("the fast stack is installed", Separator.FastReady, "");
        Separator.Separate(song, Path.Combine(dir, "warm2"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v2, out i2);
        sw.Restart();
        bool ok2 = Separator.Separate(song, Path.Combine(dir, "new"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v2, out i2); double tNew = sw.Elapsed.TotalSeconds;
        P("FAST engine: " + tNew.ToString("0.0") + " s   device " + Separator.Device);
        Check("both runs finish", ok1 && ok2, "old " + tOld.ToString("0.0") + " s, fast " + tNew.ToString("0.0") + " s  (" + (tOld / Math.Max(0.1, tNew)).ToString("0.0") + "x faster)");
        Check("the fast engine is faster", tNew < tOld * 0.8, "");
        if (ok1 && ok2)
        {
            var a = AudioClip.FromFile(v1); var b = AudioClip.FromFile(v2); var oa = AudioClip.FromFile(i1); var ob = AudioClip.FromFile(i2); int n = Math.Min(a.Length, b.Length);
            double e = 0, o = 0, e2 = 0, o2 = 0; for (int k = 0; k < n; k++) { double d = a.L[k] - b.L[k]; e += d * d; o += a.L[k] * a.L[k]; double d2 = oa.L[k] - ob.L[k]; e2 += d2 * d2; o2 += oa.L[k] * oa.L[k]; }
            double db = 10 * Math.Log10(e / (o + 1e-20) + 1e-20), db2 = 10 * Math.Log10(e2 / (o2 + 1e-20) + 1e-20);
            Check("same length", a.Length == b.Length, a.Length + " vs " + b.Length);
            Check("vocals from the fast engine match the old engine", db < -35, db.ToString("0.0") + " dB difference (random shifts make tiny differences normal)");
            Check("the beat from the fast engine matches the old engine", db2 < -35, db2.ToString("0.0") + " dB difference");
        }
        {   // long songs are processed in crossfaded chunks: the fast engine must give the same answer that way
            Separator.ChunkOverride = 20; string v3, i3; sw.Restart();
            bool ok3 = Separator.Separate(song, Path.Combine(dir, "chunked"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v3, out i3); P("chunked (20 s pieces): " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s"); Separator.ChunkOverride = 0;
            Check("chunked separation finishes", ok3, "");
            if (ok3 && ok2) { var a = AudioClip.FromFile(v2); var c = AudioClip.FromFile(v3); var orig = AudioClip.FromFile(song); int n = Math.Min(Math.Min(a.Length, c.Length), orig.Length); double e = 0, o = 0; for (int k = 0; k < n; k++) { double d = a.L[k] - c.L[k]; e += d * d; o += orig.L[k] * orig.L[k]; }
              double db = 10 * Math.Log10(e / (o + 1e-20) + 1e-20); Check("chunked vocals match the single-piece vocals", db < -20 && Math.Abs(a.Length - c.Length) < 8, db.ToString("0.0") + " dB, lengths " + a.Length + " / " + c.Length); }
        }
        Separator.KillWorker();
        P(fails == 0 ? "=== separation speed checks passed ===" : "=== " + fails + " FAILED ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
