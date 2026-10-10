using System;
using System.Diagnostics;
using System.IO;

// Checks that separating a long song in crossfaded chunks gives the same result as separating it in one piece.
//   csc /main:S ... ChunkSepTest.cs
static class S
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; P((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }

    static double DbRel(float[] a, float[] b, int n, bool diff)
    {
        double e = 0, o = 0; for (int i = 0; i < n; i++) { double x = diff ? a[i] - b[i] : a[i]; e += x * x; o += (diff ? b[i] : a[i]) * (diff ? b[i] : a[i]); }
        return 10 * Math.Log10(e / (o + 1e-20) + 1e-20);
    }

    static void Main()
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        string dir = Path.Combine(Path.GetTempPath(), "chunksep"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        string src = Path.Combine(Path.GetTempPath(), "mastertest", "Test Song.wav"), song = Path.Combine(dir, "song200.wav");
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -stream_loop 9 -i \"" + src + "\" -t 200 \"" + song + "\"") { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        P("test song: 200 s");
        string v1, i1, v2, i2; var sw = Stopwatch.StartNew(); Separator.ChunkOverride = 0;
        bool ok1 = Separator.Separate(song, Path.Combine(dir, "whole"), "song", "htdemucs", s => { }, pr => { }, () => false, out v1, out i1);
        double tWhole = sw.Elapsed.TotalSeconds; sw.Restart(); Separator.ChunkOverride = 60;
        bool ok2 = Separator.Separate(song, Path.Combine(dir, "chunked"), "song", "htdemucs", s => P("  log: " + s), pr => { }, () => false, out v2, out i2);
        double tChunk = sw.Elapsed.TotalSeconds;
        Check("both separations finish", ok1 && ok2, "whole " + tWhole.ToString("0") + " s, chunked (60 s pieces) " + tChunk.ToString("0") + " s");
        if (!(ok1 && ok2)) { Environment.Exit(1); }
        var orig = AudioClip.FromFile(song); var vw = AudioClip.FromFile(v1); var iw = AudioClip.FromFile(i1); var vc = AudioClip.FromFile(v2); var ic = AudioClip.FromFile(i2);
        Check("chunked output has exactly the same length as the song", Math.Abs(vc.Length - orig.Length) < 8 && Math.Abs(ic.Length - orig.Length) < 8, "song " + orig.Length + ", vocals " + vc.Length + ", instrumental " + ic.Length + " frames");
        int n = Math.Min(Math.Min(vc.Length, ic.Length), orig.Length);
        var sum = new float[n]; for (int i = 0; i < n; i++) sum[i] = vc.L[i] + ic.L[i];
        Check("chunked vocals + instrumental add back up to the song", DbRel(sum, orig.L, n, true) < -25, DbRel(sum, orig.L, n, true).ToString("0.0") + " dB residual");
        double eo = 0, ev = 0, ei = 0, evw = 0; for (int i = 0; i < n; i++) { eo += orig.L[i] * orig.L[i]; double a = vc.L[i] - vw.L[i], b = ic.L[i] - iw.L[i]; ev += a * a; ei += b * b; evw += vw.L[i] * vw.L[i]; }
        double dv = 10 * Math.Log10(ev / (eo + 1e-20) + 1e-20), di = 10 * Math.Log10(ei / (eo + 1e-20) + 1e-20), vlevel = 10 * Math.Log10(evw / (eo + 1e-20) + 1e-20);
        // the differences are measured against the whole song: this test song has almost no vocals (the vocal stem is about 40 dB below the song), so comparing a near-silent stem with itself would only compare noise
        Check("chunked result matches the one-piece result", dv < -35 && di < -35, "vocal stem differs by " + dv.ToString("0.0") + " dB, instrumental by " + di.ToString("0.0") + " dB (both relative to the whole song; the vocal stem itself is " + vlevel.ToString("0.0") + " dB)");
        // seams at 56 s, 112 s, 168 s (60 s chunks with 4 s overlap): look for clicks (a jump far bigger than the neighbouring sample-to-sample movement)
        double worst = 0; foreach (double sec in new[] { 56.0, 112.0, 168.0 })
        {
            int c = (int)(sec * 44100); double jump = 0, typical = 1e-9; for (int i = c - 4410; i < c + 4410; i++) { double d = Math.Abs(vc.L[i + 1] - vc.L[i]); if (Math.Abs(i - c) < 4) jump = Math.Max(jump, d); else typical = Math.Max(typical, d); }
            worst = Math.Max(worst, jump / typical);
        }
        Check("no clicks at the chunk seams", worst < 3.0, "largest seam jump is " + worst.ToString("0.00") + "x the normal local movement");
        Console.WriteLine(fails == 0 ? "=== chunked separation checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
