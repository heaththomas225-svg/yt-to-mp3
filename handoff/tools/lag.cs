using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// frame times of the studio's screens (with a copy of the real library's pack lists), to find what makes it slow
static class Lag
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static string Time(string what, int frames)
    {
        var sw = Stopwatch.StartNew(); double worst = 0; for (int i = 0; i < frames; i++) { var f = Stopwatch.StartNew(); sf.Invalidate(); sf.Update(); worst = Math.Max(worst, f.Elapsed.TotalMilliseconds); Application.DoEvents(); }
        return what.PadRight(44) + (sw.Elapsed.TotalMilliseconds / frames).ToString("0.0").PadLeft(7) + " ms/frame   worst " + worst.ToString("0") + " ms";
    }
    [STAThread]
    static void Main(string[] a)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_lag"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        string real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "sounds");
        int copied = 0; foreach (var d in Directory.GetDirectories(real)) { string pt = Path.Combine(d, "pack.txt"); if (!File.Exists(pt)) continue; string to = Path.Combine(tmp, "sounds", Path.GetFileName(d)); Directory.CreateDirectory(to); File.Copy(pt, Path.Combine(to, "pack.txt")); copied++; }
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        for (int i = 0; i < 6; i++) { var t = StudioEngine.NewTrack("Track " + i, "audio", AudioClip.FromArrays(new float[44100 * 20], new float[44100 * 20], "x")); eng.Tracks.Add(t); }
        sf = new StudioForm(app); sf.ClientSize = new Size(1920, 1000); sf.Show(); Pump(500);
        Console.WriteLine(copied + " packs copied");
        Console.WriteLine(Time("mix page (Green)", 40));
        var sw = Stopwatch.StartNew(); typeof(StudioForm).GetMethod("OpenSounds", BF).Invoke(sf, null); sf.Invalidate(); sf.Update(); Console.WriteLine("open SOUNDS (loads the library)".PadLeft(0).PadRight(44) + sw.ElapsedMilliseconds + " ms, " + SoundLibrary.InstalledCount() + " sounds");
        Console.WriteLine(Time("SOUNDS list", 40));
        sf.OpenPacks(); Console.WriteLine(Time("SOUND PACKS", 40));
        typeof(StudioForm).GetMethod("CloseSounds", BF).Invoke(sf, null);
        sf.OpenInspector(1); Console.WriteLine(Time("mixer (ALL PLUGINS)", 40));
        var tr = eng.Tracks[0]; var rv = FxRegistry.Create("reverb"); rv.Prepare(44100); tr.Fx.Add(rv); sf.OpenFxWindow(tr, rv); Console.WriteLine(Time("mixer + a plugin window", 40));
        eng.Playing = true; Console.WriteLine(Time("same while playing (visualisers)", 40)); eng.Playing = false; sf.CloseFxWindow(rv); sf.CloseInspector();
        Action<string> prof = what => { Prof.On = true; Prof.Totals.Clear(); Prof.Acc.Clear(); for (int i = 0; i < 30; i++) { Prof.Start(); sf.Invalidate(); sf.Update(); Application.DoEvents(); } Prof.On = false; Console.WriteLine("  " + what + ": " + string.Join(", ", Prof.Totals.OrderByDescending(kv => kv.Value).Take(7).Select(kv => kv.Key + " " + (kv.Value / 30).ToString("0.0"))) + "  | acc: " + string.Join(", ", Prof.Acc.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + (kv.Value / 30).ToString("0.0")))); };
        eng.Playing = true; Console.WriteLine(Time("mix page playing (nothing open)", 40)); prof("playing, nothing open"); sf.OpenInspector(1); sf.OpenFxWindow(tr, rv); prof("playing, mixer + window"); eng.Playing = false; prof("stopped, mixer + window"); sf.CloseFxWindow(rv); sf.CloseInspector();
        Themes.ApplyByName("Rainbow"); Pump(200);
        var t0 = Stopwatch.StartNew(); int frames = 0; double worst = 0; while (t0.ElapsedMilliseconds < 3000) { var f = Stopwatch.StartNew(); Themes.Tick(); sf.Invalidate(); sf.Update(); worst = Math.Max(worst, f.Elapsed.TotalMilliseconds); Application.DoEvents(); frames++; }
        Console.WriteLine("mix page (Rainbow, colours moving)".PadRight(44) + (3000.0 / frames).ToString("0.0").PadLeft(7) + " ms/frame   worst " + worst.ToString("0") + " ms");
        sf.Close();
    }
}
