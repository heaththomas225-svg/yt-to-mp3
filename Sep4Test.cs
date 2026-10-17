using System;
using System.Diagnostics;
using System.IO;

// Checks SPLIT 4 STEMS on a real song: four files (vocals, drums, bass, other) that add up to the song, each sounding like what it is,
// the same in crossfaded chunks as in one piece, and the vocals the same as the 2-stem split's.
//   csc /main:S4 ... Sep4Test.cs   (needs the separation engine installed)
static class S4
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; P((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }
    static float[] Mono(string path) { var c = AudioClip.FromFile(path); var m = new float[c.Length]; var r = c.R ?? c.L; for (int i = 0; i < m.Length; i++) m[i] = (c.L[i] + r[i]) * 0.5f; return m; }
    static double Rms(float[] x) { double s = 0; foreach (var v in x) s += v * v; return Math.Sqrt(s / Math.Max(1, x.Length)); }
    static double Db(double v) { return 20 * Math.Log10(v + 1e-12); }
    // how much of a sound is below 200 Hz (a one-pole low-pass, twice)
    static double LowShare(float[] x) { double a = Math.Exp(-2 * Math.PI * 200 / 44100.0), l1 = 0, l2 = 0, el = 0, e = 0; foreach (var v in x) { l1 = v + (l1 - v) * a; l2 = l1 + (l2 - l1) * a; el += l2 * l2; e += v * v; } return el / (e + 1e-20); }
    // how spiky it is: the share of the energy in its loudest 5% of 10 ms windows (drums are spiky, pads and bass are steady)
    static double Spiky(float[] x) { int w = 441, n = x.Length / w; var es = new double[n]; double tot = 0; for (int k = 0; k < n; k++) { double s = 0; for (int i = k * w; i < k * w + w; i++) s += x[i] * x[i]; es[k] = s; tot += s; } Array.Sort(es); double top = 0; for (int k = (int)(n * 0.95); k < n; k++) top += es[k]; return top / (tot + 1e-20); }
    static double Rel(float[] a, float[] b) { int n = Math.Min(a.Length, b.Length); double e = 0, o = 0; for (int i = 0; i < n; i++) { double d = a[i] - b[i]; e += d * d; o += b[i] * b[i]; } return 10 * Math.Log10(e / (o + 1e-20) + 1e-20); }

    static void Main()
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        Check("the separation engine is installed", Separator.Installed, ""); if (!Separator.Installed) { Console.WriteLine("=== 1 FAILED ==="); return; }
        string dir = Path.Combine(Path.GetTempPath(), "sep4test"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        string src = null; foreach (var f in Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"), "*.mp3")) { src = f; if (f.Contains("Lust")) break; }
        string song = Path.Combine(dir, "song.wav");
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -ss 40 -i \"" + src + "\" -t 30 -ar 44100 -ac 2 -c:a pcm_f32le \"" + song + "\"") { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        P("test song: 30 s of " + Path.GetFileName(src));
        var sw = Stopwatch.StartNew(); string[] st; Separator.ChunkOverride = 0;
        bool ok = Separator.Separate4(song, Path.Combine(dir, "whole"), "song", s => P("  log: " + s), pr => { }, () => false, out st);
        P("4 stems in " + sw.Elapsed.TotalSeconds.ToString("0") + " s");
        Check("SPLIT 4 STEMS makes four files: vocals, drums, bass, other", ok && st != null && st.Length == 4 && Array.TrueForAll(st, File.Exists) && st[1].EndsWith("drums.wav") && st[2].EndsWith("bass.wav") && st[3].EndsWith("other.wav"), ok ? string.Join(", ", Array.ConvertAll(st, Path.GetFileName)) : "failed");
        if (!ok) { Console.WriteLine("=== " + fails + " FAILED ==="); return; }
        var x = Mono(song); var s4 = Array.ConvertAll(st, Mono); var sum = new float[x.Length]; foreach (var s in s4) for (int i = 0; i < Math.Min(sum.Length, s.Length); i++) sum[i] += s[i];
        Check("every stem is as long as the song", Array.TrueForAll(s4, s => Math.Abs(s.Length - x.Length) < 441), string.Join(", ", Array.ConvertAll(s4, s => s.Length.ToString())) + " vs " + x.Length);
        Check("the four stems add up to the song (nothing is lost)", Rel(sum, x) < -60, Rel(sum, x).ToString("0.0") + " dB");
        double[] rms = Array.ConvertAll(s4, Rms), low = Array.ConvertAll(s4, LowShare), spk = Array.ConvertAll(s4, Spiky);
        P("levels (dB to the song): vocals " + Db(rms[0] / Rms(x)).ToString("0.0") + ", drums " + Db(rms[1] / Rms(x)).ToString("0.0") + ", bass " + Db(rms[2] / Rms(x)).ToString("0.0") + ", other " + Db(rms[3] / Rms(x)).ToString("0.0"));
        P("below 200 Hz: " + string.Join(", ", Array.ConvertAll(low, v => v.ToString("0.00"))) + "   spikiness: " + string.Join(", ", Array.ConvertAll(spk, v => v.ToString("0.00"))));
        Check("no stem is empty", Array.TrueForAll(rms, r => Db(r / Rms(x)) > -40), "");
        Check("the bass stem is the one with the most low end", low[2] > low[0] && low[2] > low[1] && low[2] > low[3] && low[2] > 0.5, low[2].ToString("0.00"));
        Check("the drum stem is the spikiest (hits), the bass and the vocals steadier", spk[1] > spk[2] && spk[1] > spk[0], spk[1].ToString("0.00") + " vs bass " + spk[2].ToString("0.00") + ", vocals " + spk[0].ToString("0.00"));
        string v2, i2; Separator.Separate(song, Path.Combine(dir, "two"), "song", "htdemucs", s => { }, pr => { }, () => false, out v2, out i2);
        Check("the 4-stem vocals are the same as the 2-stem split's", v2 != null && Rel(s4[0], Mono(v2)) < -40, v2 == null ? "2-stem failed" : Rel(s4[0], Mono(v2)).ToString("0.0") + " dB");
        Separator.ChunkOverride = 10; string[] sc; bool okc = Separator.Separate4(song, Path.Combine(dir, "chunked"), "song", s => { }, pr => { }, () => false, out sc); Separator.ChunkOverride = 0;
        bool same = okc; string det = ""; if (okc) for (int k = 0; k < 4; k++) { double r = Rel(Mono(sc[k]), s4[k]); det += (k > 0 ? ", " : "") + r.ToString("0.0"); if (r > -15) same = false; }
        Check("a long song split in crossfaded chunks gives the same four stems", same, det + " dB");
        Separator.KillWorker(); try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fails == 0 ? "=== 4 stem checks passed ===" : "=== " + fails + " FAILED ===");
    }
}
