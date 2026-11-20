using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Free backup of your songs and sounds through your own OneDrive (or Google Drive, or Dropbox): Purphex copies its project vault (every project and
// the audio it uses) and the sounds you imported into a "Purphex Backup" folder in it, and Windows uploads that folder to your Microsoft (Google,
// Dropbox) account. On a new PC, or after Purphex or its data was deleted, everything comes back from there by itself.
// It is laid out like the account backup: each PC has its own part (pc-<id>), a PC only writes its own part, files are only ever added to it (never
// deleted, never overwritten by another PC), a file lost on this PC is copied back (never over anything), and the other PCs' songs and sounds come
// over as theirs ("Song (from PC)" and a "My samples (from PC)" pack).
static class FolderBackup
{
    public static string RootOverride;   // a test points it at its own folder
    public static volatile string Status = "";
    public static int Sent, Got, Failed, Waiting;
    static readonly object lk = new object();
    static string StateFile { get { return Path.Combine(StudioEnv.DataDir, "cloud", "folder-synced.txt"); } }
    static string SamplesDir { get { return Path.Combine(SoundLibrary.Root, SoundLibrary.UserPackId); } }
    static readonly StringComparer Keys = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // the synced folder of this PC: OneDrive first, then Google Drive, then Dropbox (null when none is here)
    public static string Service = "";
    public static string FindRoot()
    {
        if (RootOverride != null) { Service = "test"; return RootOverride; }
        var cand = new List<KeyValuePair<string, string>>(); string work = Environment.GetEnvironmentVariable("OneDriveCommercial") ?? "";
        foreach (var v in new[] { "OneDriveConsumer", "OneDrive" }) { var p = Environment.GetEnvironmentVariable(v); if (!string.IsNullOrEmpty(p) && !string.Equals(p, work, StringComparison.OrdinalIgnoreCase)) cand.Add(new KeyValuePair<string, string>("OneDrive", p)); }   /* your own OneDrive, never a work or school one by surprise */
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Path.DirectorySeparatorChar == '/' && home.Length > 0)
        {   // macOS keeps them in ~/Library/CloudStorage
            try { string cs = Path.Combine(home, "Library", "CloudStorage"); if (Directory.Exists(cs)) foreach (var d in Directory.GetDirectories(cs)) { string n = Path.GetFileName(d); if (n.StartsWith("OneDrive")) cand.Add(new KeyValuePair<string, string>("OneDrive", d)); else if (n.StartsWith("GoogleDrive")) cand.Add(new KeyValuePair<string, string>("Google Drive", Path.Combine(d, "My Drive"))); else if (n.StartsWith("Dropbox")) cand.Add(new KeyValuePair<string, string>("Dropbox", d)); } } catch { }
        }
        else try { foreach (var d in DriveInfo.GetDrives()) { try { if (d.IsReady && d.DriveType != DriveType.CDRom) cand.Add(new KeyValuePair<string, string>("Google Drive", Path.Combine(d.RootDirectory.FullName, "My Drive"))); } catch { } } } catch { }
        if (home.Length > 0) { cand.Add(new KeyValuePair<string, string>("Google Drive", Path.Combine(home, "Google Drive"))); cand.Add(new KeyValuePair<string, string>("Dropbox", Path.Combine(home, "Dropbox"))); }
        foreach (var c in cand) { try { if (Directory.Exists(c.Value)) { Service = c.Key; return Path.Combine(c.Value, "Purphex Backup"); } } catch { } }
        if (work.Length > 0 && Directory.Exists(work)) { Service = "OneDrive"; return Path.Combine(work, "Purphex Backup"); }   /* only a work OneDrive on this PC: that one */
        Service = ""; return null;
    }
    // this PC's part: the same id as the account backup gives it (from this PC's fingerprint), so both backups agree on what is whose
    public static string MyPcOverride;
    public static string MyPc { get { if (MyPcOverride != null) return MyPcOverride; string fp = ""; try { fp = Edition.Fingerprint; } catch { } using (var sha = SHA1.Create()) return "pc-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("pc|" + fp))).Replace("-", "").ToLowerInvariant().Substring(0, 12); } }

    sealed class Rec { public long Size, Ticks; }
    // one pass (on the backup thread, at the lowest priority): send what is new here, fetch back what this PC lost, bring over the other PCs'
    public static bool Run()
    {
        lock (lk)
        {
            Sent = 0; Got = 0; Failed = 0; Waiting = 0; string root = FindRoot();
            if (root == null) { Status = Path.DirectorySeparatorChar == '/' ? "Sign in to OneDrive, Google Drive or Dropbox on this computer and your songs and sounds are backed up there for free" : "Sign in to OneDrive on this PC (it comes with Windows) and your songs and sounds are backed up there for free"; return false; }
            try
            {
                string mine = MyPc; string myRoot = Path.Combine(root, mine); Directory.CreateDirectory(myRoot);
                try { string nf = Path.Combine(ProjectVault.Dir, "pcname.txt"); Directory.CreateDirectory(ProjectVault.Dir); if (!File.Exists(nf) || File.ReadAllText(nf).Trim() != Environment.MachineName) File.WriteAllText(nf, Environment.MachineName); } catch { }   /* (other PCs show where the songs came from) */
                CleanParts(myRoot);
                var state = ReadState(); var seen = new HashSet<string>(Keys);
                bool gotProjects = false, gotSounds = false;
                // ---- this PC's part: first what was lost here comes back, then what is here goes up ----
                foreach (var pair in new[] { new[] { "vault", ProjectVault.Dir }, new[] { "samples", SamplesDir } })
                {
                    string loc = pair[1], bak = Path.Combine(myRoot, pair[0]); var localNow = new HashSet<string>(Keys); foreach (var f in Files(loc)) localNow.Add(pair[0] + "/" + Rel(loc, f));
                    bool listedAll = Directory.Exists(loc) && localNow.Count > 0;   /* a vault that is gone or empty never takes markers out of the backup */
                    var gone = new List<string>();
                    foreach (var f in Files(bak))
                    {
                        string rel = pair[0] + "/" + Rel(bak, f); if (localNow.Contains(rel)) continue; string to = Path.Combine(loc, Rel(bak, f));
                        bool marker = f.EndsWith(".removed", StringComparison.OrdinalIgnoreCase);
                        if (marker && listedAll && state.ContainsKey(rel)) { gone.Add(f); state.Remove(rel); continue; }   // put back on the list here: the marker goes from the backup too
                        if (marker) { string mp = ""; try { mp = File.ReadAllText(f).Trim(); } catch { } if (mp.Length > 0 && ProjectLibrary.Find(mp) != null) { gone.Add(f); continue; } }   // an old marker never hides a song that is on the list
                        if (File.Exists(to)) continue;
                        try { Copy(f, to, false); var fi = new FileInfo(to); state[rel] = new Rec { Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks }; Got++; if (rel.StartsWith("vault/projects/")) gotProjects = true; if (rel.StartsWith("samples/")) gotSounds = true; } catch { Failed++; }   // lost from this PC: copied back
                    }
                    foreach (var g in gone) try { File.Delete(g); } catch { }
                    // the sounds list: a list in the backup this PC did not write (a reinstall, a wiped My samples) has its sounds added here first, so none is lost
                    if (pair[0] == "samples") { if (MergePack(Path.Combine(bak, "pack.txt"), state)) gotSounds = true; }
                    foreach (var f in Files(loc))
                    {
                        string rel = pair[0] + "/" + Rel(loc, f); seen.Add(rel); string to = Path.Combine(bak, Rel(loc, f));
                        try
                        {
                            var fi = new FileInfo(f); Rec was; state.TryGetValue(rel, out was);
                            bool sent = was != null && was.Size == fi.Length && was.Ticks == fi.LastWriteTimeUtc.Ticks && File.Exists(to);   // exactly what was sent last time
                            if (sent || (was == null && Same(fi, to))) { state[rel] = new Rec { Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks }; continue; }
                            if (!Room(to, fi.Length)) { Waiting++; continue; }
                            Copy(f, to, true); state[rel] = new Rec { Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks }; Sent++;
                        }
                        catch { Failed++; }
                    }
                }
                // ---- the other PCs that use the same OneDrive: their songs and sounds come over as theirs ----
                foreach (var dir in Directory.GetDirectories(root))
                {
                    string dev = Path.GetFileName(dir); if (string.Equals(dev, mine, StringComparison.OrdinalIgnoreCase) || !dev.StartsWith("pc-") || dev.Length != 15) continue;
                    string vSrc = Path.Combine(dir, "vault"), vDst = Path.Combine(CloudBackup.OthersDir, dev), sSrc = Path.Combine(dir, "samples"), sDst = Path.Combine(SoundLibrary.Root, dev);
                    foreach (var f in Files(vSrc)) { if (f.EndsWith(".removed", StringComparison.OrdinalIgnoreCase)) continue; string to = Path.Combine(vDst, Rel(vSrc, f)); try { if (!Newer(new FileInfo(f), to)) continue; Copy(f, to, true); Got++; if (to.IndexOf(Path.DirectorySeparatorChar + "projects" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) gotProjects = true; } catch { } }
                    bool packChanged = false;
                    foreach (var f in Files(sSrc)) { string rel = Rel(sSrc, f); string to = Path.Combine(sDst, rel.Equals("pack.txt", StringComparison.OrdinalIgnoreCase) ? "pack.cloud.txt" : rel); try { if (!Newer(new FileInfo(f), to)) continue; Copy(f, to, true); Got++; gotSounds = true; packChanged = true; } catch { Failed++; } }
                    if (packChanged) { string cl = Path.Combine(sDst, "pack.cloud.txt"); if (File.Exists(cl)) CloudBackup.MakeOtherPack(dev, cl); }
                }
                WriteState(state);
                if (gotProjects) try { ProjectVault.AdoptOthers(); } catch { }
                if (gotSounds) try { SoundLibrary.Load(); } catch { }   /* (read in before the account backup runs: it merges into this list) */
                if (gotProjects) CloudBackup.RunOnUi(() => ProjectLibrary.Discover());
                if (Failed > 0) Status = Failed + " file" + (Failed == 1 ? "" : "s") + " could not be copied to your " + Service + " yet. They are safe on this PC and Purphex keeps trying.";
                else if (Waiting > 0) Status = "Your " + Service + " folder's drive is nearly full: " + Waiting + " file" + (Waiting == 1 ? "" : "s") + " wait for space (safe on this PC).";
                else Status = "Backed up to your " + Service + " (free)" + (Got > 0 ? ": " + Got + " file" + (Got == 1 ? "" : "s") + " brought back" : "");
                return true;
            }
            catch (Exception ex) { Status = "The " + Service + " backup could not run: " + ex.Message; return false; }
        }
    }
    // ---- files ----
    static IEnumerable<string> Files(string dir)
    {
        var l = new List<string>(); if (!Directory.Exists(dir)) return l; var todo = new Stack<string>(); todo.Push(dir);
        while (todo.Count > 0) { string d = todo.Pop(); try { foreach (var s in Directory.GetDirectories(d)) todo.Push(s); } catch { } try { foreach (var f in Directory.GetFiles(d)) { string e = Path.GetExtension(f).ToLowerInvariant(); if (e == ".tmp" || e == ".part") continue; l.Add(f); } } catch { } }
        return l;
    }
    // the file there is newer than this one by more than the 2 s rounding (or this one is missing)
    static bool Newer(FileInfo src, string dst) { try { var d = new FileInfo(dst); return !d.Exists || (src.LastWriteTimeUtc - d.LastWriteTimeUtc).TotalSeconds > 2.1 || (d.Length != src.Length && Math.Abs((src.LastWriteTimeUtc - d.LastWriteTimeUtc).TotalSeconds) <= 2.1 && src.LastWriteTimeUtc > d.LastWriteTimeUtc); } catch { return false; } }
    // room on the drive the synced folder is on (never its last 500 MB)
    static bool Room(string path, long size) { try { var root = Path.GetPathRoot(Path.GetFullPath(path)); if (string.IsNullOrEmpty(root)) return true; var di = new DriveInfo(root); return di.AvailableFreeSpace > size + 500L * 1024 * 1024; } catch { return true; } }
    // half-copied files (the PC was turned off in the middle) are taken away when they are older than ten minutes
    static void CleanParts(string dir) { try { foreach (var f in Directory.GetFiles(dir, "*.part", SearchOption.AllDirectories)) try { if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalMinutes > 10) File.Delete(f); } catch { } } catch { } }
    // the sounds list in the backup holds sounds the list here does not (and this PC did not write it last): they are added here, the files come back with them
    static bool MergePack(string bakPack, Dictionary<string, Rec> state)
    {
        try
        {
            if (!File.Exists(bakPack)) return false; string lp = Path.Combine(SamplesDir, "pack.txt"); Rec was; state.TryGetValue("samples/pack.txt", out was);
            var bi = new FileInfo(bakPack); if (was != null && was.Size == bi.Length && Math.Abs(bi.LastWriteTimeUtc.Ticks - was.Ticks) < TimeSpan.TicksPerSecond * 2) return false;   // it is the list this PC sent
            if (File.Exists(lp) && Same(new FileInfo(lp), bakPack)) return false;
            var theirs = SoundLibrary.ReadPackFile(bakPack, SamplesDir); if (theirs == null) return false; bool changed = false;
            lock (SoundLibrary.Lock)
            {
                var up = File.Exists(lp) ? SoundLibrary.ReadPackFile(lp, SamplesDir) : null; if (up == null) up = SoundLibrary.Pack(SoundLibrary.UserPackId); if (up == null) return false;
                foreach (var t in theirs.Items) if (!up.Items.Exists(x => string.Equals(x.Rel, t.Rel, StringComparison.OrdinalIgnoreCase))) { t.PackId = up.Id; t.PackName = up.Name; t.Id = up.Id + "/" + t.Rel; up.Items.Add(t); changed = true; }
                if (changed || !File.Exists(lp)) { SoundLibrary.WritePack(up); changed = true; }
            }
            return changed;
        }
        catch { return false; }
    }
    static string Rel(string root, string f) { return f.Substring(root.Length).TrimStart('\\', '/'); }
    // the copy there is the same file (same size, written at the same time to within 2 s: OneDrive and FAT round the time)
    static bool Same(FileInfo a, string b) { try { var fb = new FileInfo(b); return fb.Exists && fb.Length == a.Length && Math.Abs((fb.LastWriteTimeUtc - a.LastWriteTimeUtc).TotalSeconds) < 2.1; } catch { return false; } }
    // copied whole (to a .part first), keeping the time it was written; never over an existing file unless asked
    static void Copy(string from, string to, bool replace)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)); string tmp = to + ".part";
        try
        {
            File.Copy(from, tmp, true); try { File.SetLastWriteTimeUtc(tmp, File.GetLastWriteTimeUtc(from)); } catch { }
            if (File.Exists(to)) { if (!replace) { File.Delete(tmp); return; } File.Replace(tmp, to, null); return; }   /* (swapped in one step: never a moment with no file) */
            File.Move(tmp, to);
        }
        catch { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } throw; }
    }
    static Dictionary<string, Rec> ReadState()
    {
        var d = new Dictionary<string, Rec>(Keys);
        try { if (File.Exists(StateFile)) foreach (var line in File.ReadAllLines(StateFile, Encoding.UTF8)) { var p = line.Split('\t'); long a, b; if (p.Length >= 3 && long.TryParse(p[1], out a) && long.TryParse(p[2], out b)) d[p[0]] = new Rec { Size = a, Ticks = b }; } } catch { }
        return d;
    }
    static void WriteState(Dictionary<string, Rec> d)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(StateFile)); var sb = new StringBuilder(); foreach (var kv in d) sb.Append(kv.Key).Append('\t').Append(kv.Value.Size).Append('\t').Append(kv.Value.Ticks).Append('\n'); string tmp = StateFile + ".tmp"; File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8); if (File.Exists(StateFile)) File.Delete(StateFile); File.Move(tmp, StateFile); } catch { }
    }
}
