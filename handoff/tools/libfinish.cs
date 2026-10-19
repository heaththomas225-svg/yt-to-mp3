using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// finishes the real library: one copy per recipe in each pack, failed lists without sounds that are installed, MIDI hashes, no audio twice in the library, report
static class LibFinish
{
    static void Main(string[] a)
    {
        StudioEnv.DataDir = a[0]; SoundLibrary.Load(); var log = new StringBuilder(); int removedSame = 0, removedDup = 0, failedCleaned = 0, midiHashed = 0;
        var packs = SoundLibrary.Packs.Where(p => p.Id != SoundLibrary.UserPackId && p.Items.Count > 0).OrderBy(p => p.Id).ToList(); var seen = new Dictionary<string, string>();
        foreach (var p in packs)
        {
            bool changed = false;
            // one copy of each recipe (the original file: the shortest name)
            foreach (var g in p.Items.Where(s => s.Origin.Length > 0).GroupBy(s => LibraryBaker.OriginKey(s.Origin)).Where(g => g.Count() > 1).ToList())
                foreach (var extra in g.OrderBy(s => s.Rel.Length).ThenBy(s => s.Rel).Skip(1).ToList()) { try { File.Delete(extra.Path); } catch { } p.Items.Remove(extra); removedSame++; changed = true; }
            // MIDI hash (the notes) for MIDI made before MIDI had one
            foreach (var s in p.Items.Where(x => x.Kind == "midi" && x.Hash.Length == 0))
            {
                double bpm; var notes = SoundIO.ReadMidi(s.Path, out bpm); if (notes.Count == 0) continue; var nsb = new StringBuilder(); nsb.Append(s.Bpm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                foreach (var nt in notes) nsb.Append(';').Append(string.Join(",", Array.ConvertAll(nt, v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
                using (var sha = System.Security.Cryptography.SHA1.Create()) s.Hash = "m" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(nsb.ToString()))).Replace("-", "").Substring(0, 19).ToLowerInvariant(); midiHashed++; changed = true;
            }
            // nothing in the library twice (the first pack keeps it)
            var fails = new List<string>(); string ff = Path.Combine(p.Dir, "failed.txt"); if (File.Exists(ff)) fails.AddRange(File.ReadAllLines(ff).Where(l => l.Length > 0));
            foreach (var s in p.Items.ToList()) { if (s.Hash.Length == 0) continue; string other; if (seen.TryGetValue(s.Hash, out other)) { try { File.Delete(s.Path); } catch { } p.Items.Remove(s); removedDup++; changed = true; fails.Add(LibraryBaker.OriginKey(s.Origin) + "\t" + s.Name.Replace(p.Name + " - ", "") + ": exact copy of " + other); } else seen[s.Hash] = s.Id; }
            // a sound that is installed is not on the failed list
            var have = new HashSet<string>(p.Items.Select(s => LibraryBaker.OriginKey(s.Origin))); int before = fails.Count; fails = fails.Where(l => { int k = l.IndexOf('\t'); return k < 0 || !have.Contains(LibraryBaker.OriginKey(l.Substring(0, k))); }).Distinct().ToList(); failedCleaned += before - fails.Count;
            File.WriteAllText(ff, fails.Count > 0 ? string.Join("\n", fails) + "\n" : "", Encoding.UTF8);
            if (changed) SoundLibrary.WritePack(p);
        }
        SoundLibrary.Load(); var rep = LibraryCheck.Check(false, null);
        Console.WriteLine("removed same-recipe copies " + removedSame + ", library-wide copies " + removedDup + ", failed lines cleaned " + failedCleaned + ", MIDI hashed " + midiHashed);
        Console.WriteLine(rep.Summary); Console.WriteLine("report: " + rep.ReportPath);
    }
}
