using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// A song named only by its title is told apart by its length, against the REAL lyrics database: silent files of the real lengths are made with ffmpeg.   csc /main:RLEN ... RealLen.cs
static class RLEN
{
    [STAThread]
    static void Main()
    {
        string ff = null; string td = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "tools", "ffmpeg"); if (Directory.Exists(td)) ff = Directory.EnumerateFiles(td, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (ff == null) { Console.WriteLine("no ffmpeg installed here: skipped"); return; }
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_reallen_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); StudioEnv.Ffmpeg = () => ff; string d = Path.Combine(StudioEnv.DataDir, "songs"); Directory.CreateDirectory(d);
        object[][] cases = { new object[] { "High_Voltage.mp3", 254 }, new object[] { "High Voltage.mp3", 233 }, new object[] { "Hello.mp3", 295 }, new object[] { "Yellow.mp3", 269 } };
        try { var f0 = LyricsGet.Fetch("", "High Voltage", 254); Console.WriteLine("direct: " + (f0 == null ? "null" : f0.Artist + " | " + f0.Title + " | " + f0.Duration)); } catch (Exception ex) { Console.WriteLine("direct threw: " + ex.GetType().Name + " " + ex.Message); }
        foreach (var c in cases)
        {
            string p = Path.Combine(d, (string)c[0]); var psi = new ProcessStartInfo(ff, "-hide_banner -y -f lavfi -i anullsrc=r=22050:cl=mono -t " + c[1] + " -q:a 9 \"" + p + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using (var pr = Process.Start(psi)) { pr.StandardError.ReadToEnd(); pr.WaitForExit(); }
            var r = LyricsGen.Generate(p, false, null, () => false); Console.WriteLine((r.Found ? "FOUND  " : "none   ") + c[0] + " (" + c[1] + " s)   -> " + r.Artist + " | " + r.Title + " | " + (r.Found ? r.Lyrics.Split('\n').Length + " lines" : r.Error));
        }
    }
}
