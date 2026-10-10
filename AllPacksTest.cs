using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

// Builds EVERY pack once and checks it is non-empty, audible and every file exists (slow, ~20 min; not in run_tests).   csc /main:X0 ... AllPacksTest.cs
static class X0
{
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_allpacks"); try { if (Directory.Exists(StudioEnv.DataDir)) Directory.Delete(StudioEnv.DataDir, true); } catch { }
        int bad = 0, n = 0; long total = 0; var sw = Stopwatch.StartNew();
        foreach (var rec in SoundCatalog.Recipes)
        {
            n++; var t = Stopwatch.StartNew(); string msg = "";
            try
            {
                var pack = PackBuilder.Build(rec, null, null); if (pack == null || pack.Items.Count < 10) { msg = "too few sounds"; }
                else
                {
                    int silent = 0, missing = 0, wav = 0; foreach (var s in pack.Items) { if (!File.Exists(s.Path)) { missing++; continue; } if (s.IsAudio) { wav++; float[] l, r; int sr; string fmt; if (!SoundIO.Read(s.Path, out l, out r, out sr, out fmt)) { silent++; continue; } float pk = 0; for (int i = 0; i < l.Length; i++) { float a = Math.Abs(l[i]); if (a > pk) pk = a; if (float.IsNaN(l[i]) || float.IsInfinity(l[i])) { pk = float.NaN; break; } } if (!(pk > 0.005f) || pk > 1.2f) silent++; } }
                    if (missing > 0 || silent > 0) msg = missing + " missing, " + silent + " silent/bad of " + pack.Items.Count; total += pack.Items.Count;
                    RunAll.Cleanup(pack);
                }
            }
            catch (Exception ex) { msg = "EXCEPTION " + ex.GetType().Name + ": " + ex.Message; }
            if (msg.Length > 0) bad++; Console.WriteLine((msg.Length > 0 ? "FAIL " : "ok   ") + rec.Id + "  " + t.Elapsed.TotalSeconds.ToString("0.0") + " s  " + msg); Console.Out.Flush();
        }
        Console.WriteLine("=== " + n + " packs, " + total + " sounds in " + sw.Elapsed.TotalMinutes.ToString("0.0") + " min, " + bad + " failed ==="); Environment.Exit(bad == 0 ? 0 : 1);
    }
}
static class RunAll { public static void Cleanup(SoundPack p) { try { string d = Path.Combine(SoundLibrary.Root, p.Id); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { } } }
