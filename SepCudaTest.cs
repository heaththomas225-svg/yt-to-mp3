using System;
using System.Diagnostics;
using System.IO;

// Checks the NVIDIA turbo engine plumbing on a PC that has NO NVIDIA card: the CUDA build installs, the worker starts with it, finds no CUDA and quietly uses the processor,
// and the result still matches the normal engine. (Downloads about 2.5 GB, then removes it again.)   csc /main:CudaT ... SepCudaTest.cs
static class CudaT
{
    static void P(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); Console.Out.Flush(); }
    static string FfBin(string name) { foreach (var f in Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), name, SearchOption.AllDirectories)) return f; return null; }
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; P((ok ? "PASS  " : "FAIL  ") + n + "   " + d); }

    static void Main()
    {
        string ff = FfBin("ffmpeg.exe"), fp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        string dir = Path.Combine(Path.GetTempPath(), "sepcuda"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        string music = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"); string src = null; foreach (var f in Directory.GetFiles(music, "*.mp3")) { src = f; break; }
        string song = Path.Combine(dir, "song.wav");
        using (var p = Process.Start(new ProcessStartInfo(ff, "-y -loglevel error -ss 40 -t 40 -i \"" + src + "\" -ar 44100 -ac 2 \"" + song + "\"") { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        Check("this PC has no NVIDIA card, so the turbo engine is not offered", Separator.Nvidia().Length == 0 && !Separator.CudaOffer, "nvidia: '" + Separator.Nvidia() + "'");
        string v1, i1, v2, i2;
        Separator.DisableFast = true; Separator.KillWorker(); Separator.Separate(song, Path.Combine(dir, "warm"), "s", "htdemucs", s => { }, p => { }, () => false, out v1, out i1);
        bool ok1 = Separator.Separate(song, Path.Combine(dir, "old"), "s", "htdemucs", s => { }, p => { }, () => false, out v1, out i1);
        Separator.DisableFast = false; Separator.KillWorker(); Separator.CudaTestSkipAvail = true;
        var sw = Stopwatch.StartNew(); Separator.CudaInstall(s => P("  " + s), p => { }, () => false);
        Check("the NVIDIA build installs and loads (" + sw.Elapsed.TotalSeconds.ToString("0") + " s)", Separator.CudaReady, "");
        if (Separator.CudaReady)
        {
            Separator.KillWorker(); bool ok2 = Separator.Separate(song, Path.Combine(dir, "cuda"), "s", "htdemucs", s => P("  " + s), p => { }, () => false, out v2, out i2);
            Check("the worker starts with the NVIDIA build and finishes", ok2, "device: " + Separator.Device);
            Check("with no CUDA available it uses the processor, not a broken device", Separator.Device == "cpu", Separator.Device);
            if (ok1 && ok2)
            {
                var a = AudioClip.FromFile(v1); var b = AudioClip.FromFile(v2); int n = Math.Min(a.Length, b.Length); double e = 0, o = 0; for (int k = 0; k < n; k++) { double d = a.L[k] - b.L[k]; e += d * d; o += a.L[k] * a.L[k]; }
                double db = 10 * Math.Log10(e / (o + 1e-20) + 1e-20); Check("the vocals match the normal engine", db < -35 && a.Length == b.Length, db.ToString("0.0") + " dB, lengths " + a.Length + "/" + b.Length);
            }
        }
        Separator.KillWorker(); System.Threading.Thread.Sleep(1500);
        try { Directory.Delete(Separator.CudaDir, true); } catch (Exception ex) { P("could not remove the test install: " + ex.Message); }
        try { File.Delete(Path.Combine(Separator.Root, ".cuda_failed")); } catch { } try { File.Delete(Path.Combine(Separator.Root, "device-cuda.json")); } catch { }
        Check("the test install was removed again", !Directory.Exists(Separator.CudaDir), "");
        P(fails == 0 ? "=== NVIDIA engine plumbing checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
