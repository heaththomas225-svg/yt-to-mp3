using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// =====================================================================================================
//  Checking the sound library and writing its manifest. Check() opens every file the index lists (installed packs and your imports):
//  is it there, is it a real WAV / MIDI / preset, and is it as long as the index says. Instant sounds already made are checked too: a
//  damaged one is deleted so it is simply made again. The result says what is missing per pack (a pack can then be repaired by making
//  it again), and the manifest (sounds\library-manifest.json) lists the whole taxonomy with real counts.
// =====================================================================================================
static class LibraryCheck
{
    public class Problem { public string Pack, File, What; }
    public class Report
    {
        public int Files, Ok, Missing, Broken, InstantChecked, InstantRemoved; public List<Problem> Problems = new List<Problem>(); public DateTime At = DateTime.Now; public string ManifestPath = "", ReportPath = "";
        public List<string> PacksToRepair { get { var l = new List<string>(); foreach (var p in Problems) if (p.Pack != SoundLibrary.UserPackId && SoundCatalog.Find(p.Pack) != null && !l.Contains(p.Pack)) l.Add(p.Pack); return l; } }
        public string Summary
        {
            get
            {
                if (Missing + Broken == 0) return "Library checked: all " + Files.ToString("N0") + " installed files are there and play" + (InstantChecked > 0 ? ", and " + InstantChecked.ToString("N0") + " instant sounds you have made" + (InstantRemoved > 0 ? " (" + InstantRemoved + " damaged ones will be made again)" : "") : "") + ".";
                return "Library checked: " + Ok.ToString("N0") + " of " + Files.ToString("N0") + " files are fine; " + (Missing > 0 ? Missing + " missing" : "") + (Missing > 0 && Broken > 0 ? " and " : "") + (Broken > 0 ? Broken + " damaged" : "") + (PacksToRepair.Count > 0 ? ", in " + PacksToRepair.Count + " pack" + (PacksToRepair.Count == 1 ? "" : "s") + " that can be repaired" : "") + ".";
            }
        }
    }

    // the length of a WAV file without decoding it: frames and sample rate from the header (false if it is not a valid WAV)
    public static bool WavInfo(string path, out long frames, out int rate)
    {
        frames = 0; rate = 0;
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 44) return false; if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "RIFF") return false; br.ReadInt32(); if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "WAVE") return false;
                int ch = 0, bits = 0; while (fs.Position + 8 <= fs.Length)
                {
                    string id = Encoding.ASCII.GetString(br.ReadBytes(4)); int len = br.ReadInt32(); long next = fs.Position + len + (len & 1);
                    if (id == "fmt ") { br.ReadInt16(); ch = br.ReadInt16(); rate = br.ReadInt32(); br.ReadInt32(); br.ReadInt16(); bits = br.ReadInt16(); }
                    else if (id == "data") { if (ch <= 0 || bits <= 0 || rate <= 0) return false; long avail = Math.Min(len, fs.Length - fs.Position); frames = avail / (ch * (bits / 8)); return frames > 0; }
                    if (next < fs.Position || next > fs.Length + 1) return false; fs.Position = Math.Min(next, fs.Length);
                }
            }
        }
        catch { }
        return false;
    }

    public static Report Check(bool removeDamagedInstant, Action<string, float> progress)
    {
        var rep = new Report(); List<SoundPack> packs; lock (SoundLibrary.Lock) packs = new List<SoundPack>(SoundLibrary.Packs); int total = 0; foreach (var p in packs) total += p.Items.Count; int n = 0;
        foreach (var p in packs) foreach (var s in p.Items)
        {
            rep.Files++; n++; if (progress != null && n % 200 == 0) progress("Checking " + p.Name + "...", n / (float)Math.Max(1, total)); string why = null;
            if (!File.Exists(s.Path)) why = "missing";
            else if (s.IsAudio && Path.GetExtension(s.Path).Equals(".wav", StringComparison.OrdinalIgnoreCase)) { long fr; int sr; if (!WavInfo(s.Path, out fr, out sr)) why = "not a valid WAV"; else if (s.Dur > 0 && Math.Abs(fr / (double)sr - s.Dur) > 0.05) why = "shorter or longer than listed (" + (fr / (double)sr).ToString("0.00") + " s, listed " + s.Dur.ToString("0.00") + " s)"; }
            else if (s.Kind == "midi") { double b; if (SoundIO.ReadMidi(s.Path, out b).Count == 0) why = "MIDI file with no notes"; }
            else if (new FileInfo(s.Path).Length == 0) why = "empty file";
            if (why == null) rep.Ok++; else { if (why == "missing") rep.Missing++; else rep.Broken++; if (rep.Problems.Count < 2000) rep.Problems.Add(new Problem { Pack = p.Id, File = s.Rel, What = why }); }
        }
        // instant sounds already made: a damaged file (a crash while it was written) is removed and made again next time
        try
        {
            if (Directory.Exists(InstantLib.Dir)) foreach (var f in Directory.GetFiles(InstantLib.Dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant(); bool bad;
                if (ext == ".wav") { long fr; int sr; bad = !WavInfo(f, out fr, out sr); } else if (ext == ".mid") { double b; bad = SoundIO.ReadMidi(f, out b).Count == 0; } else if (ext == ".tmp") bad = true; else continue;
                rep.InstantChecked++; if (bad) { rep.InstantRemoved++; if (removeDamagedInstant) { try { File.Delete(f); } catch { } } }
            }
        }
        catch { }
        try { rep.ManifestPath = WriteManifest(); } catch { } try { rep.ReportPath = Diagnostics(); } catch { }
        if (progress != null) progress("Done", 1f); return rep;
    }

    // the taxonomy with real counts: sections > genres > styles, groups > types, instruments, and what is installed (the instant sounds are listed as made on demand)
    public static string WriteManifest()
    {
        var all = SoundLibrary.All(); var sb = new StringBuilder(); var ci = CultureInfo.InvariantCulture;
        Func<string, string> J = s => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        Func<Func<SoundItem, string>, SortedDictionary<string, int[]>> count = key => { var d = new SortedDictionary<string, int[]>(StringComparer.OrdinalIgnoreCase); foreach (var s in all) { string k = key(s); if (string.IsNullOrEmpty(k)) continue; int[] c; if (!d.TryGetValue(k, out c)) d[k] = c = new int[2]; c[s.Instant ? 1 : 0]++; } return d; };
        Action<string, SortedDictionary<string, int[]>> block = (name, d) => { sb.Append("  ").Append(J(name)).Append(": {"); bool first = true; foreach (var kv in d) { sb.Append(first ? "\n" : ",\n").Append("    ").Append(J(kv.Key)).Append(": { \"installed\": ").Append(kv.Value[0]).Append(", \"instant\": ").Append(kv.Value[1]).Append(" }"); first = false; } sb.Append("\n  },\n"); };
        int inst = 0, made = 0; foreach (var s in all) if (s.Instant) { inst++; } try { if (Directory.Exists(InstantLib.Dir)) made = Directory.GetFiles(InstantLib.Dir, "*.*", SearchOption.AllDirectories).Length; } catch { }
        sb.Append("{\n  \"generated\": ").Append(J(DateTime.UtcNow.ToString("o"))).Append(",\n  \"soundVersion\": ").Append(J(PackBuilder.SoundVersion)).Append(",\n  \"packs\": ").Append(SoundCatalog.Recipes.Count).Append(",\n");
        sb.Append("  \"installedSounds\": ").Append(all.Count - inst).Append(",\n  \"instantSounds\": ").Append(inst).Append(",\n  \"instantMadeOnThisPc\": ").Append(made).Append(",\n");
        block("byKind", count(s => s.Kind)); block("byGroup", count(s => s.Group)); block("byType", count(s => s.Type)); block("byInstrument", count(s => s.Instrument)); block("byGenre", count(s => s.Genre)); block("byStyle", count(s => s.Genre + " / " + s.Sub));
        sb.Append("  \"sections\": {"); bool f1 = true; var secs = new SortedDictionary<string, List<string>>(); foreach (var r in SoundCatalog.Recipes) { List<string> l; if (!secs.TryGetValue(r.Section, out l)) secs[r.Section] = l = new List<string>(); if (!l.Contains(r.Genre)) l.Add(r.Genre); }
        foreach (var kv in secs) { sb.Append(f1 ? "\n" : ",\n").Append("    ").Append(J(kv.Key)).Append(": [").Append(string.Join(", ", kv.Value.ConvertAll(x => J(x)))).Append("]"); f1 = false; } sb.Append("\n  },\n");
        sb.Append("  \"installedPacks\": ["); bool f2 = true; lock (SoundLibrary.Lock) foreach (var p in SoundLibrary.Packs) { if (p.Items.Count == 0) continue; sb.Append(f2 ? "\n" : ",\n").Append("    { \"id\": ").Append(J(p.Id)).Append(", \"name\": ").Append(J(p.Name)).Append(", \"version\": ").Append(J(p.Version)).Append(", \"sounds\": ").Append(p.Items.Count).Append(" }"); f2 = false; }
        sb.Append("\n  ]\n}\n"); Directory.CreateDirectory(SoundLibrary.Root); string path = Path.Combine(SoundLibrary.Root, "library-manifest.json"); File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); return path;
    }

    // the diagnostic report (sounds\library-report.txt): how many checked sounds there are by section, genre, kind, type and pack, how big the library is, every sound that
    // failed its checks with the reason, any audio that is in the library twice, and whether every sound carries its licence and where it came from
    public static string Diagnostics()
    {
        var sb = new StringBuilder(); var inst = SoundLibrary.Installed(); long bytes = 0; int noLic = 0, noSrc = 0; var hashes = new Dictionary<string, string>(); var dupes = new List<string>(); const string NL = "\r\n";
        foreach (var s in inst)
        {
            try { if (File.Exists(s.Path)) bytes += new FileInfo(s.Path).Length; } catch { }
            if (s.PackId != SoundLibrary.UserPackId) { if (s.Licence.Length == 0) noLic++; if (s.Source.Length == 0) noSrc++; }
            if (s.Hash.Length > 0) { string o; if (hashes.TryGetValue(s.Hash, out o)) dupes.Add(s.Id + " = " + o); else hashes[s.Hash] = s.Id; }
        }
        Func<Func<SoundItem, string>, string> table = key =>
        {
            var d = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase); foreach (var s in inst) { string k = key(s); if (string.IsNullOrEmpty(k)) k = "(none)"; int n; d.TryGetValue(k, out n); d[k] = n + 1; }
            var t = new StringBuilder(); foreach (var kv in d) t.Append("  ").Append(kv.Key.PadRight(40)).Append(kv.Value.ToString("N0").PadLeft(8)).Append(NL); return t.ToString();
        };
        int packs = 0; lock (SoundLibrary.Lock) foreach (var p in SoundLibrary.Packs) if (p.Items.Count > 0) packs++;
        sb.Append("PURPHEX SOUND LIBRARY REPORT  ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(NL).Append(NL);
        sb.Append("Installed, checked sounds (real files): ").Append(inst.Count.ToString("N0")).Append(" in ").Append(packs).Append(" packs, ").Append((bytes / 1048576.0).ToString("N0")).Append(" MB").Append(NL);
        sb.Append("Made-on-play sounds listed (not files until played): ").Append(InstantLib.Items.Count.ToString("N0")).Append(NL);
        sb.Append("Audio in the library twice: ").Append(dupes.Count).Append("    without a licence: ").Append(noLic).Append("    without a source: ").Append(noSrc).Append(NL).Append(NL);
        sb.Append("BY SECTION").Append(NL).Append(table(s => { var r = SoundCatalog.Find(s.PackId); return r != null ? r.Section : "Yours"; })).Append(NL);
        sb.Append("BY GENRE").Append(NL).Append(table(s => s.Genre)).Append(NL).Append("BY KIND").Append(NL).Append(table(s => s.Kind)).Append(NL);
        sb.Append("BY TYPE").Append(NL).Append(table(s => s.Type)).Append(NL).Append("BY PACK").Append(NL).Append(table(s => s.PackName)).Append(NL);
        sb.Append("FAILED CHECKS (left out of the library)").Append(NL); int failed = 0; var why = new SortedDictionary<string, int>();
        List<SoundPack> all; lock (SoundLibrary.Lock) all = new List<SoundPack>(SoundLibrary.Packs);
        foreach (var p in all)
        {
            string ff = Path.Combine(p.Dir, "failed.txt"); if (!File.Exists(ff)) continue;
            foreach (var line in File.ReadAllLines(ff))
            {
                int k = line.IndexOf('\t'); if (k < 0) continue; failed++; string w = line.Substring(k + 1); sb.Append("  ").Append(p.Name).Append(": ").Append(w).Append(NL);
                int c = w.IndexOf(": "); string reason = c >= 0 ? w.Substring(c + 2) : w;
                if (reason.StartsWith("exact copy")) reason = "exact copy of another sound"; else if (reason.StartsWith("too much like")) reason = "too much like another sound of its kind"; else { int cut = reason.IndexOf('('); if (cut > 0) reason = reason.Substring(0, cut).Trim(); }
                int n; why.TryGetValue(reason, out n); why[reason] = n + 1;
            }
        }
        sb.Append(NL).Append("FAILED: ").Append(failed).Append(NL); foreach (var kv in why) sb.Append("  ").Append(kv.Key.PadRight(44)).Append(kv.Value.ToString().PadLeft(6)).Append(NL);
        if (dupes.Count > 0) { sb.Append(NL).Append("DUPLICATES").Append(NL); foreach (var d in dupes) sb.Append("  ").Append(d).Append(NL); }
        Directory.CreateDirectory(SoundLibrary.Root); string path = Path.Combine(SoundLibrary.Root, "library-report.txt"); File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); return path;
    }
}
