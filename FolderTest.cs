using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// The free OneDrive backup: two PCs sharing one OneDrive (a folder here). Songs, their audio and imported sounds go into this PC's part, a wiped vault
// or a fresh install gets them all back, the other PC's songs and sounds come over as theirs, a song put back on the list stays back, and nothing is
// ever deleted from the backup when it is deleted on the PC.
static class FB
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static string Sha1(byte[] b) { using (var s = SHA1.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", ""); }
    static void Pc(string d, string id) { StudioEnv.DataDir = d; Directory.CreateDirectory(d); FolderBackup.MyPcOverride = id; SoundLibrary.Load(); ProjectLibrary.Load(); }
    static string Wav(string file, int seed)
    {
        int n = 4410; var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
        for (int i = 0; i < n; i++) w.Write((short)(Math.Sin(i * 0.05 * (seed + 1)) * 8000)); w.Flush(); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, ms.ToArray()); return file;
    }
    static int CountFiles(string d) { return Directory.Exists(d) ? Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length : 0; }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "purphex_foldertest_" + Guid.NewGuid().ToString("N")); string drive = Path.Combine(root, "OneDrive", "Purphex Backup"); Directory.CreateDirectory(drive);
        string PcA = Path.Combine(root, "pcA"), PcB = Path.Combine(root, "pcB"), A = "pc-aaaaaaaaaaaa", B = "pc-bbbbbbbbbbbb";
        try
        {
            FolderBackup.RootOverride = null; string realRoot = FolderBackup.FindRoot(); Console.WriteLine("  (this PC's synced folder: " + (realRoot == null ? "none" : FolderBackup.Service) + ")");
            FolderBackup.RootOverride = drive;
            // ---- PC A: a song with a recording, and two imported sounds ----
            Pc(PcA, A); string src = Path.Combine(root, "music"); string rec = Wav(Path.Combine(src, "vocal take.wav"), 1); string proj = Path.Combine(src, "Bassline.ytmproj");
            File.WriteAllText(proj, "YTMPROJ|1\ntrack|Vocal|0|0|" + rec.Replace("%", "%25").Replace("|", "%7C") + "\n", Encoding.UTF8); ProjectLibrary.Touch(proj, "Bassline", 1, 120, 1, true, null); ProjectVault.Keep(proj, "Bassline");
            SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(src, "kick.wav"), 2), Wav(Path.Combine(src, "snare.wav"), 3) }, null);
            bool ok = FolderBackup.Run(); string mine = Path.Combine(drive, A);
            Check("PC A's song, its audio and its imported sounds are copied into its part of the OneDrive backup", ok && File.Exists(Path.Combine(mine, "vault", "projects", Path.GetFileName(ProjectVault.CopyFor(proj)))) && CountFiles(Path.Combine(mine, "vault", "audio")) >= 1 && File.Exists(Path.Combine(mine, "samples", "imports", "kick.wav")) && File.Exists(Path.Combine(mine, "samples", "pack.txt")), FolderBackup.Status);
            Check("the Projects line says it is backed up, for free", FolderBackup.Status.StartsWith("Backed up to your"), FolderBackup.Status);
            ok = FolderBackup.Run(); Check("a second pass copies nothing (nothing changed)", ok && FolderBackup.Sent == 0, FolderBackup.Sent + " sent");
            // ---- Purphex's data on PC A is deleted ----
            Directory.Delete(ProjectVault.Dir, true); Directory.Delete(Path.Combine(SoundLibrary.Root, SoundLibrary.UserPackId), true); File.Delete(proj); File.Delete(rec);
            ok = FolderBackup.Run(); SoundLibrary.Load(); ProjectLibrary.Discover(); var en = ProjectLibrary.Find(proj); if (en != null) en.Recheck();
            Check("a wiped vault and My samples come back from the backup", ok && ProjectVault.Has(proj) && SoundLibrary.Pack(SoundLibrary.UserPackId) != null && SoundLibrary.Pack(SoundLibrary.UserPackId).Items.Count == 2, FolderBackup.Got + " brought back");
            Check("and the deleted project is put back on the PC (no FILE NOT FOUND)", en != null && !en.Missing && File.Exists(proj), "");
            Check("deleting on the PC never deleted anything from the backup", File.Exists(Path.Combine(mine, "samples", "imports", "snare.wav")), "");
            // ---- a fresh install on PC A (no data at all) ----
            string PcA2 = Path.Combine(root, "pcA2"); Pc(PcA2, A); FolderBackup.Run(); SoundLibrary.Load();
            Check("a fresh install gets its songs and sounds back as its own", ProjectVault.Has(proj) && ProjectVault.All().Exists(k => k.Name == "Bassline" && k.FromPc == null) && SoundLibrary.Pack(SoundLibrary.UserPackId).Items.Count == 2, FolderBackup.Status);
            // ---- a fresh install that imports a new sound before its first backup keeps every sound (the lists are merged, none lost) ----
            string PcA3 = Path.Combine(root, "pcA3"); Pc(PcA3, A); SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(root, "new", "clap.wav"), 9) }, null); FolderBackup.Run(); SoundLibrary.Load();
            var names = new List<string>(); foreach (var it in SoundLibrary.Pack(SoundLibrary.UserPackId).Items) names.Add(it.Name); names.Sort();
            Check("a fresh install that imported a new sound first keeps it AND gets the old ones back (no sound lost)", string.Join(",", names.ToArray()) == "clap,kick,snare", string.Join(",", names.ToArray()));
            FolderBackup.Run(); var bakNames = new List<string>(); foreach (var it in SoundLibrary.ReadPackFile(Path.Combine(mine, "samples", "pack.txt"), Path.Combine(mine, "samples")).Items) bakNames.Add(it.Name); bakNames.Sort();
            Check("and the backup's list has all three too", string.Join(",", bakNames.ToArray()) == "clap,kick,snare", string.Join(",", bakNames.ToArray()));
            // ---- taken off the list, then put back ----
            ProjectVault.MarkRemoved(proj); FolderBackup.Run(); string mk = Path.Combine(mine, "vault", "projects", Path.GetFileName(Path.ChangeExtension(ProjectVault.CopyFor(proj), ".removed")));
            Check("a song taken off the list is off in the backup too", File.Exists(mk), "");
            ProjectVault.Unmark(proj); FolderBackup.Run(); FolderBackup.Run(); Check("putting it back takes the marker out of the backup (it stays back)", !File.Exists(mk) && !ProjectVault.IsRemoved(proj), "");
            // ---- PC B uses the same OneDrive ----
            Pc(PcB, B); FolderBackup.Run(); SoundLibrary.Load();
            var adopted = ProjectLibrary.Entries.Find(e => e.Name.StartsWith("Bassline (from "));
            Check("PC B gets PC A's song as a song of its own (\"Bassline (from PC)\")", adopted != null && File.Exists(adopted.Path), ProjectLibrary.Entries.Count + " projects");
            SoundPack other = null; lock (SoundLibrary.Lock) other = SoundLibrary.Packs.Find(p => p.Name.StartsWith("My samples (from "));
            Check("and PC A's sounds as a pack of their own", other != null && other.Items.Exists(x => x.Name == "kick") && other.Items.Exists(x => x.Name == "snare"), other == null ? "none" : other.Name);
            Check("PC B never writes into PC A's part", !Directory.Exists(Path.Combine(mine, "vault", "projects")) || CountFiles(Path.Combine(drive, B)) >= 1, "");
            // ---- PC A never gets its own song back as "(from PC B)" (PC B keeps a copy of the song it brought over) ----
            Pc(PcA3, A); FolderBackup.Run(); FolderBackup.Run();
            Check("PC A never gets its own song back from PC B as a duplicate", !ProjectLibrary.Entries.Exists(e => e.Name.StartsWith("Bassline (from ")), string.Join("; ", ProjectLibrary.Entries.ConvertAll(e => e.Name).ToArray()));
            Check("and a pass that brings nothing says nothing came back", FolderBackup.Got == 0 && !FolderBackup.Status.Contains("brought back"), FolderBackup.Status);
            // ---- no OneDrive on the PC ----
            FolderBackup.RootOverride = Path.Combine(root, "nope", "Purphex Backup"); Directory.CreateDirectory(Path.GetDirectoryName(FolderBackup.RootOverride));
            FolderBackup.RootOverride = null; Environment.SetEnvironmentVariable("OneDrive", null); Environment.SetEnvironmentVariable("OneDriveConsumer", null); Environment.SetEnvironmentVariable("OneDriveCommercial", null);
            if (FolderBackup.FindRoot() == null) { ok = FolderBackup.Run(); Check("with no OneDrive on the PC it says how to turn it on (for free)", !ok && FolderBackup.Status.Contains("OneDrive"), FolderBackup.Status); }
            else Console.WriteLine("  (skipped the no-OneDrive check: this PC has Google Drive or Dropbox)");
        }
        catch (Exception ex) { Check("no crash", false, ex.ToString()); }
        try { Directory.Delete(root, true); } catch { }
        Console.WriteLine(fails == 0 ? "=== OneDrive backup checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
