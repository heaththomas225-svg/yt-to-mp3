using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// bakes the real library: _libbake.exe <dataDir> <shard> <shards> [packId,packId...]   (writes a log line per pack; resumable)
static class LibBake
{
    static void Main(string[] a)
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle; } catch { }   // never in the way of anything else on this PC
        StudioEnv.DataDir = a[0]; int shard = int.Parse(a[1]), shards = int.Parse(a[2]); SoundLibrary.Load();
        var recs = SoundCatalog.Recipes.OrderBy(r => r.Id).ToList(); if (a.Length > 3) { var ids = a[3] == "all" ? recs.Select(r => r.Id).ToArray() : a[3].Split(','); recs = recs.Where(r => ids.Any(x => r.Id == x || r.Name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)).ToList(); }
        var mine = new List<Recipe>(); for (int i = 0; i < recs.Count; i++) if (i % shards == shard) mine.Add(recs[i]);
        string log = Path.Combine(a[0], "bake-" + shard + ".log");
        foreach (var rec in mine)
        {
            var have = SoundLibrary.Pack(rec.Id); if (!(a.Length > 4 && a[4] == "topup") && have != null && have.Version == LibraryBaker.Version && have.Items.Count > 0 && !File.Exists(Path.Combine(have.Dir, "pack.part"))) { File.AppendAllText(log, rec.Id + "\tskip\t" + have.Items.Count + "\n"); continue; }
            var sw = Stopwatch.StartNew(); LibraryBaker.Result res;
            try { res = LibraryBaker.BakePack(rec, null, null); } catch (Exception ex) { File.AppendAllText(log, rec.Id + "\terror\t" + ex.Message.Replace("\n", " ") + "\n"); continue; }
            SoundLibrary.AddOrReplace(res.Pack);
            File.AppendAllText(log, rec.Id + "\tok\t" + res.Made + "\tfailed " + res.Failed + "\tdup " + res.Duplicates + "\tsimilar " + res.Similar + "\tresumed " + res.Resumed + "\t" + (res.Bytes / 1048576.0).ToString("0.0") + " MB\t" + sw.Elapsed.TotalSeconds.ToString("0") + " s\n");
            foreach (var f in res.Failures) File.AppendAllText(log, "   " + f + "\n");
        }
        File.AppendAllText(log, "DONE\n");
    }
}
