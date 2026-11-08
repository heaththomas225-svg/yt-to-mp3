using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Your projects: a list of every project you have saved or opened (with its picture), your recent projects, and your profile.
class ProjectEntry
{
    public string Path = "", Name = "", Cover = "", Desc = "", Tags = ""; public DateTime Updated, Opened; public int Tracks; public double Bpm, Seconds;
    public bool Fav, Finished, OnProfile; public string Kind = "", Video = "";   // Video: a link to the music video of this project (a streaming or social link), shown on the profile   // Fav: starred; Finished: marked done (or exported); Kind: Song, Beat, Vocals or Empty
    public bool Custom; public long Bytes = -1; public bool SizeAsked;   // Custom: you renamed it (saving does not change the name back); Bytes: the project file plus the audio it uses
    int chkAt; bool miss, chk;
    public bool Missing { get { int now = Environment.TickCount; if (!chk || unchecked(now - chkAt) > 2000) { miss = !File.Exists(Path); chk = true; chkAt = now; } return miss; } }
    public void Recheck() { chk = false; }
}

static class ProjectLibrary
{
    public static readonly List<ProjectEntry> Entries = new List<ProjectEntry>(); public static string ProfileName = "Producer", AvatarPath = "";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture; static readonly object lk = new object();
    static string ListFile { get { return Path.Combine(StudioEnv.DataDir, "projects.txt"); } }
    public static string CoversDir { get { return Path.Combine(StudioEnv.DataDir, "covers"); } }
    static string ProfileFile { get { return Path.Combine(StudioEnv.DataDir, "profile.txt"); } }
    public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"); } }

    static bool loadedOnce;   // Save() reads the files first when nothing has yet, so the project list, your name and your picture are never overwritten with empty ones
    public static void LoadOnce() { if (!loadedOnce) Load(); }
    public static void Load()
    {
        lock (lk)
        {
            loadedOnce = true; Entries.Clear();
            try
            {
                if (File.Exists(ListFile))
                    foreach (var line in File.ReadAllLines(ListFile, Encoding.UTF8))
                    {
                        var f = line.Split('|'); if (f.Length < 9 || f[0] != "P") continue; long u, o; int t; double b, s; long.TryParse(f[3], out u); long.TryParse(f[4], out o); int.TryParse(f[5], out t); double.TryParse(f[6], NumberStyles.Float, CI, out b); double.TryParse(f[7], NumberStyles.Float, CI, out s);
                        Entries.Add(new ProjectEntry { Path = f[1], Name = f[2], Updated = new DateTime(Math.Max(0, u)), Opened = new DateTime(Math.Max(0, o)), Tracks = t, Bpm = b, Seconds = s, Cover = f[8], Desc = f.Length > 9 ? f[9] : "", Tags = f.Length > 10 ? f[10] : "", Custom = f.Length > 11 && f[11] == "1", Fav = f.Length > 12 && f[12] == "1", Finished = f.Length > 13 && f[13] == "1", Kind = f.Length > 14 ? f[14] : "", OnProfile = f.Length > 15 && f[15] == "1", Video = f.Length > 16 ? f[16] : "" });
                    }
            }
            catch { }
            LoadBin(); PurgeBin(DateTime.Now);
            try { if (File.Exists(ProfileFile)) foreach (var line in File.ReadAllLines(ProfileFile, Encoding.UTF8)) { int i = line.IndexOf('='); if (i < 1) continue; string k = line.Substring(0, i), v = line.Substring(i + 1); if (k == "name" && v.Trim().Length > 0) ProfileName = v.Trim(); else if (k == "avatar") AvatarPath = v; } } catch { }
        }
        RestoreGone();
    }
    // project files deleted on the PC are put back from Purphex's own copy, so the project is never lost
    public static void RestoreGone()
    {
        List<ProjectEntry> all; lock (lk) all = new List<ProjectEntry>(Entries);
        foreach (var e in all) { try { if (!File.Exists(e.Path) && ProjectVault.Has(e.Path)) { string why; EnsureFile(e, out why); } } catch { } }
    }
    public static void Save()
    {
        lock (lk)
            try
            {
                if (!loadedOnce && Entries.Count == 0) Load();   // nothing has been read yet and nothing is in memory: read what is saved first
                Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder();
                foreach (var e in Entries) sb.Append("P|").Append(e.Path.Replace("|", "/")).Append('|').Append(e.Name.Replace("|", "/")).Append('|').Append(e.Updated.Ticks).Append('|').Append(e.Opened.Ticks).Append('|').Append(e.Tracks).Append('|').Append(e.Bpm.ToString("0.##", CI)).Append('|').Append(e.Seconds.ToString("0.#", CI)).Append('|').Append(e.Cover).Append('|').Append((e.Desc ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "")).Append('|').Append((e.Tags ?? "").Replace("|", "/")).Append('|').Append(e.Custom ? 1 : 0).Append('|').Append(e.Fav ? 1 : 0).Append('|').Append(e.Finished ? 1 : 0).Append('|').Append(e.Kind ?? "").Append('|').Append(e.OnProfile ? 1 : 0).Append('|').Append((e.Video ?? "").Replace("|", "")).Append('\n');
                Win.WriteAtomic(ListFile, sb.ToString(), Encoding.UTF8);
                Win.WriteAtomic(ProfileFile, "name=" + ProfileName + "\navatar=" + AvatarPath + "\n", Encoding.UTF8);
            }
            catch { }
    }

    // what kind of project it is, from the roles of its tracks: Song (vocals with a beat or instruments), Vocals, Beat (beats / instruments only) or Empty
    public static string KindOfRoles(IEnumerable<string> roles)
    {
        bool v = false, o = false; foreach (var r in roles) { if (r == "bus") continue; if (r == "vocal") v = true; else o = true; }
        return v && o ? "Song" : (v ? "Vocals" : (o ? "Beat" : "Empty"));
    }
    public static void EnsureKind(ProjectEntry e)
    {
        if (e.Kind != null && e.Kind.Length > 0) return; var roles = new List<string>();
        try { if (File.Exists(e.Path)) foreach (var line in File.ReadAllLines(e.Path, Encoding.UTF8)) if (line.StartsWith("track|")) { var p = line.Split('|'); if (p.Length > 1) roles.Add(p[1]); } } catch { }
        e.Kind = KindOfRoles(roles);
    }
    public static void SetFlags(string path, bool? fav, bool? finished)
    {
        lock (lk) { var e = Find(path); if (e == null) return; if (fav.HasValue) e.Fav = fav.Value; if (finished.HasValue) e.Finished = finished.Value; }
        Save();
    }

    // ---- the Recycling Bin: a removed project waits here for 30 days (restore it, or delete it for good) ----
    public sealed class BinItem { public string Id = "", OrigPath = "", Name = "", Cover = "", Kind = ""; public DateTime Deleted; public int Tracks; public double Bpm, Seconds; public bool Fav, Finished; }
    public static readonly List<BinItem> Bin = new List<BinItem>(); public const int BinDays = 30;
    static string BinFile { get { return Path.Combine(StudioEnv.DataDir, "bin.txt"); } }
    public static string BinDir { get { return Path.Combine(StudioEnv.DataDir, "bin"); } }
    public static string BinPath(BinItem b) { return Path.Combine(BinDir, b.Id + ".ytmproj"); }
    public static int DaysLeft(BinItem b, DateTime now) { return Math.Max(0, BinDays - (int)Math.Floor((now - b.Deleted).TotalDays)); }
    public static void LoadBin()
    {
        lock (lk)
        {
            Bin.Clear();
            try
            {
                if (File.Exists(BinFile))
                    foreach (var line in File.ReadAllLines(BinFile, Encoding.UTF8))
                    {
                        var f = line.Split('|'); if (f.Length < 11 || f[0] != "B") continue; long d; int t; double b, s2; long.TryParse(f[4], out d); int.TryParse(f[5], out t); double.TryParse(f[6], NumberStyles.Float, CI, out b); double.TryParse(f[7], NumberStyles.Float, CI, out s2);
                        Bin.Add(new BinItem { Id = f[1], OrigPath = f[2], Name = f[3], Deleted = new DateTime(Math.Max(0, d)), Tracks = t, Bpm = b, Seconds = s2, Cover = f[8], Fav = f[9] == "1", Finished = f[10] == "1", Kind = f.Length > 11 ? f[11] : "" });
                    }
            }
            catch { }
        }
    }
    public static void SaveBin()
    {
        lock (lk)
            try
            {
                Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder();
                foreach (var b in Bin) sb.Append("B|").Append(b.Id).Append('|').Append(b.OrigPath.Replace("|", "/")).Append('|').Append(b.Name.Replace("|", "/")).Append('|').Append(b.Deleted.Ticks).Append('|').Append(b.Tracks).Append('|').Append(b.Bpm.ToString("0.##", CI)).Append('|').Append(b.Seconds.ToString("0.#", CI)).Append('|').Append(b.Cover).Append('|').Append(b.Fav ? 1 : 0).Append('|').Append(b.Finished ? 1 : 0).Append('|').Append(b.Kind ?? "").Append('\n');
                Win.WriteAtomic(BinFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
    }
    // takes the project off the list and moves its file into the bin (only the project file moves: the audio it uses is not touched)
    public static BinItem MoveToBin(ProjectEntry e)
    {
        if (e == null || !File.Exists(e.Path)) return null; EnsureKind(e);
        var b = new BinItem { Id = Guid.NewGuid().ToString("N"), OrigPath = e.Path, Name = e.Name, Cover = e.Cover ?? "", Kind = e.Kind ?? "", Deleted = DateTime.Now, Tracks = e.Tracks, Bpm = e.Bpm, Seconds = e.Seconds, Fav = e.Fav, Finished = e.Finished };
        try { Directory.CreateDirectory(BinDir); File.Move(e.Path, BinPath(b)); } catch { return null; }
        lock (lk) { Bin.Add(b); Entries.RemoveAll(x => string.Equals(x.Path, e.Path, StringComparison.OrdinalIgnoreCase)); }
        ProjectVault.MarkRemoved(e.Path); SaveBin(); Save(); return b;
    }
    // puts a project back where it was (or beside it, under a new name, if that name is taken now); null + a reason if it cannot
    public static string Restore(BinItem b, out string error)
    {
        error = null; string from = BinPath(b); if (!File.Exists(from)) { error = "The file is no longer in the bin."; return null; }
        string target = b.OrigPath, dir = Path.GetDirectoryName(target);
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Directory.CreateDirectory(DefaultFolder); dir = DefaultFolder; target = Path.Combine(dir, Path.GetFileName(b.OrigPath)); }
            if (File.Exists(target)) { string stem = Path.GetFileNameWithoutExtension(target), ext = Path.GetExtension(target); int n = 1; do { target = Path.Combine(dir, stem + " (restored" + (n > 1 ? " " + n : "") + ")" + ext); n++; } while (File.Exists(target)); }
            File.Move(from, target);
        }
        catch (Exception ex) { error = ex.Message; return null; }
        lock (lk) { Bin.Remove(b); Entries.RemoveAll(x => string.Equals(x.Path, target, StringComparison.OrdinalIgnoreCase)); Entries.Add(new ProjectEntry { Path = target, Name = b.Name, Custom = true, Cover = b.Cover != null && b.Cover.Length > 0 && File.Exists(b.Cover) ? b.Cover : "", Tracks = b.Tracks, Bpm = b.Bpm, Seconds = b.Seconds, Fav = b.Fav, Finished = b.Finished, Kind = b.Kind, Updated = File.GetLastWriteTime(target), Opened = DateTime.Now }); }
        SaveBin(); Save(); ProjectVault.Unmark(target); ProjectVault.Unmark(b.OrigPath); ProjectVault.KeepAsync(target, b.Name); return target;
    }
    public static void DeleteForever(BinItem b)
    {
        try { string p = BinPath(b); if (File.Exists(p)) File.Delete(p); } catch { }
        ProjectVault.Forget(b.OrigPath);   /* deleted for good means Purphex's own copy goes too */
        lock (lk) Bin.Remove(b);
        try { if (b.Cover != null && b.Cover.StartsWith(CoversDir, StringComparison.OrdinalIgnoreCase) && File.Exists(b.Cover) && !Entries.Exists(x => x.Cover == b.Cover) && !Bin.Exists(x => x.Cover == b.Cover)) File.Delete(b.Cover); } catch { }
        SaveBin();
    }
    public static int EmptyBin() { List<BinItem> all; lock (lk) all = new List<BinItem>(Bin); foreach (var b in all) DeleteForever(b); return all.Count; }
    // what has waited 30 days is deleted for good (how many went)
    public static int PurgeBin(DateTime now)
    {
        List<BinItem> old; lock (lk) old = Bin.FindAll(b => (now - b.Deleted).TotalDays >= BinDays); foreach (var b in old) DeleteForever(b); return old.Count;
    }

    // the size of a project = the project file plus every audio file it uses (measured once, in the background)
    public static void MeasureAsync(ProjectEntry e)
    {
        if (e.SizeAsked) return; e.SizeAsked = true; string path = e.Path;
        System.Threading.ThreadPool.QueueUserWorkItem(o =>
        {
            long total = 0; try
            {
                if (!File.Exists(path)) { e.Bytes = 0; return; } total = new FileInfo(path).Length; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) { if (!line.StartsWith("track|")) continue; var p = line.Split('|'); if (p.Length < 5) continue; string src = p[4].Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%"); if (src.Length > 0 && File.Exists(src) && seen.Add(src)) total += new FileInfo(src).Length; }
            }
            catch { }
            e.Bytes = total;
        });
    }
    public static string Size(long b)
    {
        if (b < 0) return "..."; if (b < 1024) return b + " B"; if (b < 1024L * 1024) return (b / 1024.0).ToString("0.#", CI) + " KB"; if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.#", CI) + " MB"; return (b / 1073741824.0).ToString("0.##", CI) + " GB";
    }
    public static string CleanTags(string raw)
    {
        var l = new List<string>(); foreach (var t in (raw ?? "").Split(new[] { ' ', ',', ';', '#' }, StringSplitOptions.RemoveEmptyEntries)) { string x = t.Trim().ToLowerInvariant(); if (x.Length > 0 && x.Length <= 20 && !l.Contains(x) && l.Count < 1000 && !ProfileStore.IsBadTag(x)) l.Add(x); /* any number of tags, none with a swear word */ }
        return string.Join(" ", l.ToArray());
    }

    public static void SetOnProfile(ProjectEntry e, bool on) { lock (lk) e.OnProfile = on; Save(); }
    public static bool AddIfNew(ProjectEntry e) { lock (lk) { if (Find(e.Path) != null) return false; Entries.Add(e); return true; } }
    public static ProjectEntry Find(string path) { lock (lk) return Entries.Find(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)); }

    // called whenever a project is saved or opened
    public static void Touch(string path, string name, int tracks, double bpm, double seconds, bool saved, byte[] coverJpeg)
    {
        lock (lk)
        {
            var e = Find(path); if (e == null) { e = new ProjectEntry { Path = path }; Entries.Add(e); } ProjectVault.Unmark(path);   /* saved or opened again: back on the list for good */
            e.Recheck(); if (!e.Custom) e.Name = name; e.Bytes = -1; e.SizeAsked = false; e.Tracks = tracks; e.Bpm = bpm; e.Seconds = seconds; e.Opened = DateTime.Now; if (saved) e.Updated = DateTime.Now; else if (e.Updated.Ticks == 0) { try { e.Updated = File.GetLastWriteTime(path); } catch { } }
            if (coverJpeg != null && coverJpeg.Length > 0) { try { Directory.CreateDirectory(CoversDir); string cp = Path_(path); File.WriteAllBytes(cp, coverJpeg); e.Cover = cp; Forget(cp); } catch { } }
            else if (e.Cover.Length > 0 && !File.Exists(e.Cover)) e.Cover = "";
        }
        Save();
    }
    static string Path_(string projectPath) { using (var sha = SHA1.Create()) return Path.Combine(CoversDir, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(projectPath.ToLowerInvariant()))).Replace("-", "").Substring(0, 16) + ".jpg"); }
    public static void Remove(string path) { lock (lk) Entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)); ProjectVault.MarkRemoved(path); Save(); }
    public static List<ProjectEntry> Recent(int n) { lock (lk) { var l = new List<ProjectEntry>(Entries); l.RemoveAll(e => e.Missing); l.Sort((a, b) => b.Opened.CompareTo(a.Opened)); if (l.Count > n) l.RemoveRange(n, l.Count - n); return l; } }
    public static List<ProjectEntry> AllByEdited() { lock (lk) { var l = new List<ProjectEntry>(Entries); l.Sort((a, b) => { int m = a.Missing.CompareTo(b.Missing); return m != 0 ? m : b.Updated.CompareTo(a.Updated); }); return l; } }

    // projects saved in the usual folder before this list existed are picked up too
    public static void Discover()
    {
        try
        {
            bool changed = false; if (!StudioEnv.UnderTest && Directory.Exists(DefaultFolder))   /* a test run never reads your real Music folder */
            foreach (var f in Directory.GetFiles(DefaultFolder, "*.ytmproj"))
            {
                if (Find(f) != null || ProjectVault.IsRemoved(f)) continue; int tracks = 0;   /* taken off the list on purpose: stays off */ double bpm = 0; byte[] cov = null;
                try { foreach (var line in File.ReadAllLines(f, Encoding.UTF8)) { if (line.StartsWith("track|")) tracks++; else if (line.StartsWith("bpm|")) double.TryParse(line.Substring(4), NumberStyles.Float, CI, out bpm); else if (line.StartsWith("cover|")) { try { cov = Convert.FromBase64String(line.Substring(6)); } catch { } } } } catch { }
                lock (lk) { var e = new ProjectEntry { Path = f, Name = System.IO.Path.GetFileNameWithoutExtension(f), Tracks = tracks, Bpm = bpm, Updated = File.GetLastWriteTime(f), Opened = File.GetLastWriteTime(f) }; if (cov != null) { try { Directory.CreateDirectory(CoversDir); File.WriteAllBytes(Path_(f), cov); e.Cover = Path_(f); } catch { } } Entries.Add(e); changed = true; }
            }
            if (changed) Save();
        }
        catch { }
        // projects Purphex kept a copy of come back on the list even if the list or the file was lost
        try { bool more = false; foreach (var k in ProjectVault.All()) { if (k.FromPc != null || k.Removed || Find(k.Path) != null) continue; int tracks = 0; double bpm = 0; string kind = ""; try { var roles = new List<string>(); foreach (var line in File.ReadAllLines((ProjectVault.CopyOf(k.Path) ?? ProjectVault.CopyFor(k.Path)), Encoding.UTF8)) { if (line.StartsWith("track|")) { tracks++; var p = line.Split('|'); if (p.Length > 1) roles.Add(p[1]); } else if (line.StartsWith("bpm|")) double.TryParse(line.Substring(4), NumberStyles.Float, CI, out bpm); } kind = KindOfRoles(roles); } catch { } lock (lk) Entries.Add(new ProjectEntry { Path = k.Path, Name = k.Name, Tracks = tracks, Bpm = bpm, Kind = kind, Updated = k.Saved, Opened = k.Saved }); more = true; } if (more) { Save(); RestoreGone(); } } catch { }
        ProjectRescue.StartOnce();   /* and your songs are looked for everywhere in your folders, once a session */
        CloudBackup.Start();   /* and backed up to your account (and brought down from it on a new PC) */
    }
    // a project whose file was deleted on the PC is put back from Purphex's own copy first (false + a reason if there is none)
    public static bool EnsureFile(ProjectEntry e, out string error)
    {
        error = null; if (e == null) { error = "No project."; return false; } if (File.Exists(e.Path)) return true;
        string old = e.Path, now = ProjectVault.Bring(old, out error); if (now == null) return false;
        if (!string.Equals(now, old, StringComparison.OrdinalIgnoreCase)) { lock (lk) e.Path = now; ProjectVault.MarkRemoved(old); ProjectVault.Unmark(now); Save(); ProjectVault.KeepAsync(now, e.Name); }   /* it lives at the new place now: the old place is not put back on the list */
        e.Recheck(); return true;
    }

    // ---- pictures ----
    // a square, centre-cropped JPEG of the picture you chose (null if the file is not a picture we can read)
    public static byte[] MakeSquare(string imageFile, int size)
    {
        try
        {
            Image src; using (var fs = new FileStream(imageFile, FileMode.Open, FileAccess.Read)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; src = Image.FromStream(ms); }
            using (src)
            using (var bmp = new Bitmap(size, size, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality; using (var tiny = new Bitmap(12, 12)) { using (var tg = Graphics.FromImage(tiny)) { tg.InterpolationMode = InterpolationMode.HighQualityBilinear; tg.DrawImage(src, new Rectangle(0, 0, 12, 12)); } g.DrawImage(tiny, new Rectangle(-size / 6, -size / 6, size * 4 / 3, size * 4 / 3)); using (var dim = new SolidBrush(Color.FromArgb(150, 4, 2, 12))) g.FillRectangle(dim, 0, 0, size, size); }   /* a soft, dark copy of the picture fills the square */ double k = Math.Min(size / (double)src.Width, size / (double)src.Height); int dw = Math.Max(1, (int)(src.Width * k)), dh = Math.Max(1, (int)(src.Height * k)); g.DrawImage(src, new Rectangle((size - dw) / 2, (size - dh) / 2, dw, dh)); }   // the whole picture, never cropped
                var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.MimeType == "image/jpeg"); var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                using (var o = new MemoryStream()) { bmp.Save(o, codec, ep); return o.ToArray(); }
            }
        }
        catch { return null; }
    }

    // ---- the picture of a project is kept whole (with its zoom and position), like the profile picture: the framed pictures are made from it ----
    public static string CoverSrc(string projectPath) { return Path_(projectPath) + ".src"; }
    static string CoverAdjFile(string projectPath) { return CoverSrc(projectPath) + ".adj"; }
    public static bool HasCoverSrc(string projectPath) { return File.Exists(CoverSrc(projectPath)); }
    // zoom (0 = the whole picture, 100 = fills the frame, more = closer), left/right and up/down in percent
    public static void LoadAdj(string projectPath, out int z, out int x, out int y)
    {
        z = 100; x = 50; y = 50;
        try { var f = File.ReadAllText(CoverAdjFile(projectPath)).Trim().Split(' '); z = Math.Max(0, Math.Min(400, int.Parse(f[0]))); x = Math.Max(0, Math.Min(100, int.Parse(f[1]))); y = Math.Max(0, Math.Min(100, int.Parse(f[2]))); } catch { z = 100; x = 50; y = 50; }
    }
    public static void SaveAdj(string projectPath, int z, int x, int y) { try { Directory.CreateDirectory(CoversDir); File.WriteAllText(CoverAdjFile(projectPath), z + " " + x + " " + y); } catch { } }
    // cuts away an even border around the picture (the black or white bars round a photo) so the picture itself fills the frame; null when there is nothing to cut
    public static Rectangle? AutoTrim(Image src)
    {
        try
        {
            int tw = 96, th = Math.Max(8, (int)Math.Round(96.0 * src.Height / Math.Max(1, src.Width))); if (src.Height > src.Width) { th = 96; tw = Math.Max(8, (int)Math.Round(96.0 * src.Width / Math.Max(1, src.Height))); }
            using (var t = new Bitmap(tw, th, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(t)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(src, 0, 0, tw, th); }
                Func<int, int, int[]> px = (x, y) => { var c = t.GetPixel(x, y); return new int[] { c.R, c.G, c.B }; };
                var c0 = px(0, 0); var cs = new[] { px(tw - 1, 0), px(0, th - 1), px(tw - 1, th - 1) }; Func<int[], int[], int> d = (p, q) => Math.Abs(p[0] - q[0]) + Math.Abs(p[1] - q[1]) + Math.Abs(p[2] - q[2]);
                foreach (var c in cs) if (d(c0, c) > 60) return null;   // the corners differ: there is no even border
                int ring = 0, off = 0; for (int x = 0; x < tw; x++) { ring += 2; if (d(px(x, 0), c0) > 60) off++; if (d(px(x, th - 1), c0) > 60) off++; } for (int y = 0; y < th; y++) { ring += 2; if (d(px(0, y), c0) > 60) off++; if (d(px(tw - 1, y), c0) > 60) off++; }
                if (off > ring / 25) return null;   // the edge has real picture in it
                int x0 = tw, x1 = -1, y0 = th, y1 = -1;
                for (int y = 0; y < th; y++) for (int x = 0; x < tw; x++) if (d(px(x, y), c0) > 90) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                if (x1 < 0) return null; int w = x1 - x0 + 1, h = y1 - y0 + 1; if (w < tw * 0.18 || h < th * 0.18) return null; if (w * h > 0.9 * tw * th) return null;
                int padx = Math.Max(1, (int)Math.Round(w * 0.06)), pady = Math.Max(1, (int)Math.Round(h * 0.06)); x0 = Math.Max(0, x0 - padx); y0 = Math.Max(0, y0 - pady); x1 = Math.Min(tw - 1, x1 + padx); y1 = Math.Min(th - 1, y1 + pady);
                double kx = src.Width / (double)tw, ky = src.Height / (double)th; return new Rectangle((int)Math.Floor(x0 * kx), (int)Math.Floor(y0 * ky), Math.Max(1, (int)Math.Ceiling((x1 - x0 + 1) * kx)), Math.Max(1, (int)Math.Ceiling((y1 - y0 + 1) * ky)));
            }
        }
        catch { return null; }
    }
    // the chosen picture, borders cut away and no larger than 1024 pixels; also says the zoom that suits it (fill the frame when it is roughly square, the whole picture when it is long)
    static byte[] CoverSource(string imageFile, out int zoom)
    {
        zoom = 100; Image src; using (var fs = new FileStream(imageFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; src = Image.FromStream(ms); }
        using (src)
        {
            var tr = AutoTrim(src); Rectangle cut = tr.HasValue ? tr.Value : new Rectangle(0, 0, src.Width, src.Height);
            double sc = Math.Min(1.0, 1024.0 / Math.Max(cut.Width, cut.Height)); int w = Math.Max(1, (int)Math.Round(cut.Width * sc)), h = Math.Max(1, (int)Math.Round(cut.Height * sc)); double ar = cut.Width / (double)cut.Height; zoom = ar >= 0.7 && ar <= 1.43 ? 100 : 0;
            using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb)) { using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.FromArgb(255, 10, 5, 22)); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(src, new Rectangle(0, 0, w, h), cut, GraphicsUnit.Pixel); } return JpegBytes(bmp, 90); }
        }
    }
    // the framed picture (zoom and position applied) as JPEG bytes
    public static byte[] FramedCover(string srcFile, int size, int z, int x, int y)
    {
        try
        {
            Image src; using (var fs = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; src = Image.FromStream(ms); }
            using (src) using (var bmp = ProfileStore.RenderFramed(src, size, size, z, x, y, Color.FromArgb(255, 10, 5, 22), false)) using (var rgb = new Bitmap(size, size, PixelFormat.Format24bppRgb)) { using (var g = Graphics.FromImage(rgb)) g.DrawImage(bmp, 0, 0, size, size); return JpegBytes(rgb, 88); }
        }
        catch { return null; }
    }
    // gives a saved project a picture: it is cut to its content and fitted to the frame by itself; the whole picture is kept so it can be zoomed and moved later
    public static bool SetCover(string projectPath, string imageFile, out string error)
    {
        error = null; byte[] srcBytes = null; int z = 100; try { srcBytes = CoverSource(imageFile, out z); } catch { srcBytes = null; }
        if (srcBytes == null) { error = "That file is not a picture Purphex can read (use a JPG, PNG, GIF or BMP)."; return false; }
        try { Directory.CreateDirectory(CoversDir); File.WriteAllBytes(CoverSrc(projectPath), srcBytes); SaveAdj(projectPath, z, 50, 50); } catch (Exception ex) { error = "Couldn't save the picture: " + ex.Message; return false; }
        return BakeCover(projectPath, out error);
    }
    // makes the pictures the lists and the project file use from the kept whole picture and its zoom and position
    public static bool BakeCover(string projectPath, out string error)
    {
        error = null; string sf = CoverSrc(projectPath); if (!File.Exists(sf)) { error = "This project has no picture to adjust."; return false; }
        int z, x, y; LoadAdj(projectPath, out z, out x, out y); var big = FramedCover(sf, 420, z, x, y); var small = FramedCover(sf, 256, z, x, y); if (big == null || small == null) { error = "That file is not a picture Purphex can read (use a JPG, PNG, GIF or BMP)."; return false; }
        try
        {
            if (File.Exists(projectPath))
            {
                var lines = new List<string>(File.ReadAllLines(projectPath, Encoding.UTF8)); lines.RemoveAll(l => l.StartsWith("cover|")); lines.Insert(Math.Min(2, lines.Count), "cover|" + Convert.ToBase64String(small));
                Win.WriteAtomic(projectPath, string.Join("\r\n", lines.ToArray()) + "\r\n", Encoding.UTF8);   // replaced in one step: the project is never deleted before the new file is in place
            }
            Directory.CreateDirectory(CoversDir); string cp = Path_(projectPath); File.WriteAllBytes(cp, big); Forget(cp);
            lock (lk) { var e = Find(projectPath); if (e != null) e.Cover = cp; }
            Save(); return true;
        }
        catch (Exception ex) { error = "Couldn't save the picture: " + ex.Message; return false; }
    }
    // pictures made by older versions had a blurred, stretched copy of the picture filling the square: the picture itself is found in the middle (where the detail is) and kept on its own,
    // so every project picture shows undistorted on a plain background. Done once for the pictures that are there; the new pictures are made this way from the start.
    public static Rectangle? FindContent(Image im)
    {
        try
        {
            const int N = 128; using (var t = new Bitmap(N, N, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(t)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(im, 0, 0, N, N); }
                var l = new int[N, N]; for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { var c = t.GetPixel(x, y); l[y, x] = (c.R * 3 + c.G * 6 + c.B) / 10; }
                var er = new double[N]; var ec = new double[N]; for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { if (x + 1 < N) er[y] += Math.Abs(l[y, x + 1] - l[y, x]); if (y + 1 < N) ec[x] += Math.Abs(l[y + 1, x] - l[y, x]); if (x + 1 < N) ec[x] += 0; }
                double mr = 0, mc = 0; for (int i = 0; i < N; i++) { mr = Math.Max(mr, er[i]); mc = Math.Max(mc, ec[i]); } if (mr < 40 || mc < 40) return null;
                int r0 = N, r1 = -1, c0 = N, c1 = -1; for (int i = 0; i < N; i++) { if (er[i] > mr * 0.22) { if (i < r0) r0 = i; if (i > r1) r1 = i; } if (ec[i] > mc * 0.22) { if (i < c0) c0 = i; if (i > c1) c1 = i; } }
                if (r1 < 0 || c1 < 0) return null; int h = r1 - r0 + 1, w = c1 - c0 + 1; if (h > N * 0.88 && w > N * 0.88) return null; if (h < N * 0.2 || w < N * 0.2) return null;
                int px = Math.Max(1, (int)Math.Round(w * 0.04)), py = Math.Max(1, (int)Math.Round(h * 0.04)); c0 = Math.Max(0, c0 - px); c1 = Math.Min(N - 1, c1 + px); r0 = Math.Max(0, r0 - py); r1 = Math.Min(N - 1, r1 + py);
                double kx = im.Width / (double)N, ky = im.Height / (double)N; return new Rectangle((int)Math.Floor(c0 * kx), (int)Math.Floor(r0 * ky), Math.Max(1, (int)Math.Ceiling((c1 - c0 + 1) * kx)), Math.Max(1, (int)Math.Ceiling((r1 - r0 + 1) * ky)));
            }
        }
        catch { return null; }
    }
    static bool fitDone;
    public static int FitOldCovers()
    {
        if (fitDone) return 0; fitDone = true; int n = 0; string mark = Path.Combine(CoversDir, "fit-v1.txt"); List<string> paths; lock (lk) { paths = new List<string>(); foreach (var e in Entries) if (e.Cover.Length > 0 && File.Exists(e.Cover) && !File.Exists(CoverSrc(e.Path))) paths.Add(e.Path); }
        HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { if (File.Exists(mark)) foreach (var l in File.ReadAllLines(mark)) done.Add(l.Trim()); } catch { }
        foreach (var p in paths)
        {
            if (done.Contains(p)) continue; done.Add(p);
            try
            {
                var e = Find(p); if (e == null) continue; Rectangle? box; byte[] outBytes = null; int z = 100;
                using (var im = LoadImageCopy(e.Cover))
                {
                    box = FindContent(im); if (!box.HasValue) continue; var cut = box.Value; double ar = cut.Width / (double)cut.Height; z = ar >= 0.7 && ar <= 1.43 ? 100 : 0;
                    using (var bmp = new Bitmap(cut.Width, cut.Height, PixelFormat.Format24bppRgb)) { using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(im, new Rectangle(0, 0, cut.Width, cut.Height), cut, GraphicsUnit.Pixel); } outBytes = JpegBytes(bmp, 92); }
                }
                File.WriteAllBytes(CoverSrc(p), outBytes); SaveAdj(p, z, 50, 50); string err; if (BakeCover(p, out err)) n++;
            }
            catch { }
        }
        try { Directory.CreateDirectory(CoversDir); File.WriteAllLines(mark, new List<string>(done).ToArray()); } catch { }
        return n;
    }
    static Image LoadImageCopy(string file) { using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; return Image.FromStream(ms); } }
    // a picture made by an older version has no whole picture kept: the framed one is used as the start
    public static bool EnsureCoverSrc(string projectPath)
    {
        if (HasCoverSrc(projectPath)) return true; var e = Find(projectPath); if (e == null || e.Cover.Length == 0 || !File.Exists(e.Cover)) return false;
        try { File.Copy(e.Cover, CoverSrc(projectPath), true); SaveAdj(projectPath, 100, 50, 50); return true; } catch { return false; }
    }
    public static void ClearCover(string projectPath)
    {
        try
        {
            if (File.Exists(projectPath)) { var lines = new List<string>(File.ReadAllLines(projectPath, Encoding.UTF8)); if (lines.RemoveAll(l => l.StartsWith("cover|")) > 0) { Win.WriteAtomic(projectPath, string.Join("\r\n", lines.ToArray()) + "\r\n", Encoding.UTF8); } }   // replaced in one step: the project is never deleted before the new file is in place
            lock (lk) { var e = Find(projectPath); if (e != null) { try { if (e.Cover.Length > 0) File.Delete(e.Cover); } catch { } Forget(e.Cover); e.Cover = ""; } }
            try { File.Delete(CoverSrc(projectPath)); File.Delete(CoverAdjFile(projectPath)); } catch { }
            Save();
        }
        catch { }
    }
    // back to the Purphex logo
    public static string AvatarSrc { get { return Path.Combine(CoversDir, "profile-src.jpg"); } }
    public static void ClearAvatar() { try { if (AvatarPath.Length > 0 || File.Exists(AvatarSrc)) { foreach (var f in new[] { AvatarPath, AvatarSrc }) { if (f.Length == 0) continue; try { File.Delete(f); } catch { } Forget(f); } AvatarPath = ""; ProfileStore.AvZoom = 0; ProfileStore.AvX = 50; ProfileStore.AvY = 50; ProfileStore.Save(); Save(); } } catch { } }
    // the chosen picture is kept whole (no larger than 1024 pixels) so it can be zoomed and moved later; the framed 256 pixel picture is made from it
    public static bool SetAvatar(string imageFile, out string error)
    {
        error = null; byte[] src = null; try { src = ScaledJpeg(imageFile, 1024); } catch { src = null; } if (src == null) { error = "That file is not a picture Purphex can read (use a JPG, PNG, GIF or BMP)."; return false; }
        try { Directory.CreateDirectory(CoversDir); File.WriteAllBytes(AvatarSrc, src); Forget(AvatarSrc); ProfileStore.AvZoom = 0; ProfileStore.AvX = 50; ProfileStore.AvY = 50; ProfileStore.Save(); return BakeAvatar(out error); } catch (Exception ex) { error = ex.Message; return false; }
    }
    // the picture as it shows in its frame (zoom and position applied) saved at 256 pixels: the file the rest of the program reads
    public static bool BakeAvatar() { string e; return BakeAvatar(out e); }
    public static bool BakeAvatar(out string error)
    {
        error = null;
        try
        {
            if (!File.Exists(AvatarSrc)) return false; Image im; using (var fs = new FileStream(AvatarSrc, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; im = Image.FromStream(ms); }
            using (im) using (var bmp = ProfileStore.RenderFramed(im, 256, 256, ProfileStore.AvZoom, ProfileStore.AvX, ProfileStore.AvY, Color.FromArgb(255, 10, 5, 22))) { string p = Path.Combine(CoversDir, "profile.jpg"); File.WriteAllBytes(p, JpegBytes(bmp, 88)); Forget(p); AvatarPath = p; Save(); }
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
    public static byte[] JpegBytes(Bitmap b, long q) { var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.MimeType == "image/jpeg"); var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, q); using (var o = new MemoryStream()) { b.Save(o, codec, ep); return o.ToArray(); } }
    // any picture file as JPEG bytes, no larger than max pixels on its longest side (smaller ones stay as they are); throws when it is not a picture
    public static byte[] ScaledJpeg(string file, int max)
    {
        Image src; using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; src = Image.FromStream(ms); }
        using (src)
        {
            double sc = Math.Min(1.0, max / (double)Math.Max(src.Width, src.Height)); int w = Math.Max(1, (int)Math.Round(src.Width * sc)), h = Math.Max(1, (int)Math.Round(src.Height * sc));
            using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb)) { using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.FromArgb(255, 10, 5, 22)); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(src, 0, 0, w, h); } return JpegBytes(bmp, 90); }
        }
    }

    // loaded pictures are kept so the lists draw fast
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Bitmap> scaled = new Dictionary<string, Bitmap>();
    static void Forget(string path) { lock (cache) { Bitmap b; if (path != null && cache.TryGetValue(path, out b)) { cache.Remove(path); try { b.Dispose(); } catch { } } if (path != null) { var ks = new List<string>(); foreach (var k in scaled.Keys) if (k.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase)) ks.Add(k); foreach (var k in ks) { try { scaled[k].Dispose(); } catch { } scaled.Remove(k); } } } }
    // the picture pre-scaled once to the size it is drawn at (null when there is no picture)
    public static Bitmap Scaled(string path, int w, int h, InterpolationMode mode)
    {
        var src = Picture(path); if (src == null || w < 1 || h < 1) return null;
        lock (cache)
        {
            string key = path + "|" + w + "|" + h; Bitmap b; if (scaled.TryGetValue(key, out b)) return b;
            if (scaled.Count > 60) { foreach (var kv in scaled) { try { kv.Value.Dispose(); } catch { } } scaled.Clear(); }
            try { b = new Bitmap(w, h, PixelFormat.Format32bppPArgb); using (var gg = Graphics.FromImage(b)) { gg.InterpolationMode = mode; gg.PixelOffsetMode = PixelOffsetMode.HighQuality; float k = Math.Max(w / (float)src.Width, h / (float)src.Height), sw = w / k, sh = h / k; gg.DrawImage(src, new Rectangle(0, 0, w, h), (src.Width - sw) / 2, (src.Height - sh) / 2, sw, sh, GraphicsUnit.Pixel); } scaled[key] = b; return b; } catch { return null; }
        }
    }
    public static Bitmap Picture(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        lock (cache)
        {
            Bitmap b; if (cache.TryGetValue(path, out b)) return b; if (!File.Exists(path)) return null;
            try { using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) { var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0; using (var im = Image.FromStream(ms)) b = new Bitmap(im); } cache[path] = b; return b; } catch { return null; }
        }
    }

    // artwork for a project with no picture: colours picked from its name
    static readonly Dictionary<string, Bitmap> artCache = new Dictionary<string, Bitmap>();
    public static void DrawArt(Graphics g, RectangleF r, string name, int seed)
    {
        int w = (int)Math.Round(r.Width), hh = (int)Math.Round(r.Height); if (w < 1 || hh < 1) return;
        string key = (name ?? "") + "|" + seed + "|" + w + "|" + hh; Bitmap bm;
        lock (artCache)
        {
            if (!artCache.TryGetValue(key, out bm))
            {
                if (artCache.Count > 200) { foreach (var kv in artCache) { try { kv.Value.Dispose(); } catch { } } artCache.Clear(); }
                bm = new Bitmap(w, hh, PixelFormat.Format32bppPArgb);
                using (var bg = Graphics.FromImage(bm)) { bg.SmoothingMode = SmoothingMode.AntiAlias; bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; DrawArtRaw(bg, new RectangleF(0, 0, w, hh), name, seed); }
                artCache[key] = bm;
            }
            var oi = g.InterpolationMode; var po = g.PixelOffsetMode; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(bm, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), w, hh)); g.InterpolationMode = oi; g.PixelOffsetMode = po;
        }
    }
    static void DrawArtRaw(Graphics g, RectangleF r, string name, int seed)
    {
        int h =Math.Abs((name ?? "").GetHashCode() + seed) % 360; Color a = FromHsv(h, 0.75, 0.55), b = FromHsv((h + 70) % 360, 0.85, 0.28);
        using (var br = new LinearGradientBrush(r, a, b, 55f)) g.FillRectangle(br, r);
        using (var pen = new Pen(Color.FromArgb(46, 255, 255, 255), 1.5f)) { for (int i = 1; i < 6; i++) { float yy = r.Y + r.Height * i / 6f; g.DrawLine(pen, r.X, yy, r.Right, yy); } }
        var rnd = new Random(h); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 2f)) { var pts = new List<PointF>(); for (int i = 0; i <= 24; i++) { float u = i / 24f; float amp = (float)(Math.Sin(u * Math.PI) * (0.18 + 0.22 * rnd.NextDouble())); pts.Add(new PointF(r.X + r.Width * u, r.Y + r.Height * 0.62f + (i % 2 == 0 ? -1 : 1) * amp * r.Height * 0.5f)); } g.DrawLines(pen, pts.ToArray()); }
        string ini = string.IsNullOrWhiteSpace(name) ? "P" : name.Trim().Substring(0, 1).ToUpperInvariant(); using (var f = new Font("Segoe UI Black", Math.Max(14, r.Height * 0.26f), FontStyle.Bold, GraphicsUnit.Pixel)) using (var br = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) { var sz = g.MeasureString(ini, f); g.DrawString(ini, f, br, r.X + (r.Width - sz.Width) / 2, r.Y + r.Height * 0.14f); }
    }
    static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60.0) % 2 - 1)), m = v - c, r, g, b; if (h < 60) { r = c; g = x; b = 0; } else if (h < 120) { r = x; g = c; b = 0; } else if (h < 180) { r = 0; g = c; b = x; } else if (h < 240) { r = 0; g = x; b = c; } else if (h < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
    public static string Ago(DateTime t)
    {
        var d = DateTime.Now - t; if (d.TotalMinutes < 2) return "just now"; if (d.TotalHours < 1) return (int)d.TotalMinutes + " min ago"; if (d.TotalDays < 1) return (int)d.TotalHours + " h ago"; if (d.TotalDays < 2) return "yesterday"; if (d.TotalDays < 30) return (int)d.TotalDays + " days ago"; return t.ToString("d MMM yyyy", CI);
    }
}

// Every project is kept inside Purphex too: each save copies the project file and every audio file it uses into the app's own vault,
// so a project still shows on the Projects page, opens, and plays its audio even if the file or its audio is deleted or moved on the PC.
static class ProjectVault
{
    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "vault"); } }
    static string AudioDir { get { return Path.Combine(Dir, "audio"); } }
    static string ProjDir { get { return Path.Combine(Dir, "projects"); } }
    static readonly object lk = new object();
    static string Key(string s) { using (var sha = SHA1.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes((s ?? "").ToLowerInvariant()))).Replace("-", "").Substring(0, 16); }
    static string U(string s) { return s.Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%"); }
    static string AudioFor(string src) { string ext = ""; try { ext = Path.GetExtension(src); } catch { } return Path.Combine(AudioDir, Key(src) + ext); }
    public static string CopyFor(string projectPath) { return Path.Combine(ProjDir, Key(projectPath) + ".ytmproj"); }
    static string InfoFor(string projectPath) { return Path.Combine(ProjDir, Key(projectPath) + ".txt"); }
    static string RemovedFor(string projectPath) { return Path.Combine(ProjDir, Key(projectPath) + ".removed"); }
    // other PCs on your account (their vaults come down from the cloud backup into folders of their own)
    static List<string> Others() { var l = new List<string>(); try { if (Directory.Exists(CloudBackup.OthersDir)) l.AddRange(Directory.GetDirectories(CloudBackup.OthersDir)); } catch { } return l; }
    // the newest file with this name in this PC's vault folder or in one of the other PCs' (null if none)
    static string FindIn(string sub, string name)
    {
        string best = null; DateTime bt = DateTime.MinValue;
        try { string own = Path.Combine(Dir, sub, name); if (File.Exists(own)) return own; foreach (var o in Others()) { string p = Path.Combine(o, sub, name); if (File.Exists(p)) { var t = File.GetLastWriteTimeUtc(p); if (best == null || t > bt) { best = p; bt = t; } } } } catch { }
        return best;
    }
    // the vault's copy of an audio file that is gone from the PC (null if it was never kept, here or on another PC of yours)
    public static string FindAudio(string src) { try { if (string.IsNullOrEmpty(src)) return null; string p = AudioFor(src); return File.Exists(p) ? p : null; } catch { return null; } }
    public static string CopyOf(string projectPath) { return FindCopy(projectPath); }
    static string FindCopy(string projectPath) { try { if (string.IsNullOrEmpty(projectPath)) return null; string p = CopyFor(projectPath); return File.Exists(p) ? p : null; } catch { return null; } }
    public static bool Has(string projectPath) { return FindCopy(projectPath) != null; }

    public static void KeepAsync(string projectPath, string name)
    {
        if (string.IsNullOrEmpty(projectPath)) return;
        var th = new System.Threading.Thread(() => Keep(projectPath, name)); th.IsBackground = true; th.Priority = System.Threading.ThreadPriority.BelowNormal; th.Start();
    }
    // copies the project and the audio it uses into the vault (audio already kept and unchanged is not copied again)
    public static bool Keep(string projectPath, string name) { return Keep(projectPath, name, null); }
    // origin: "<pc>|<path>" of a song that came from another PC (kept with every later save, so it is never brought over twice)
    public static bool Keep(string projectPath, string name, string origin)
    {
        lock (lk)
            try
            {
                if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath)) return false;
                byte[] bytes; using (var fs = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { var ms = new MemoryStream(); fs.CopyTo(ms); bytes = ms.ToArray(); }   /* never blocks a save of the same file */
                var lines = Encoding.UTF8.GetString(bytes).Replace("\r", "").Split('\n'); Directory.CreateDirectory(AudioDir); Directory.CreateDirectory(ProjDir);
                foreach (var line in lines)
                {
                    if (!line.StartsWith("track|")) continue; var p = line.Split('|'); if (p.Length < 5) continue; string src = U(p[4]);
                    if (src.Length == 0 || src.StartsWith(Dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(src)) continue;
                    if (((int)File.GetAttributes(src) & (0x1000 | 0x40000 | 0x400000)) != 0) continue;   /* a file that is only in the cloud (OneDrive) is never downloaded just to copy it */
                    try
                    {
                        string dst = AudioFor(src); var si = new FileInfo(src);
                        if (File.Exists(dst)) { var di = new FileInfo(dst); if (di.Length == si.Length && di.LastWriteTimeUtc == si.LastWriteTimeUtc) continue; }
                        string tmp = dst + ".tmp"; File.Copy(src, tmp, true); if (File.Exists(dst)) File.Delete(dst); File.Move(tmp, dst);
                    }
                    catch { }
                }
                string copy = CopyFor(projectPath), tmpc = copy + ".tmp"; File.WriteAllBytes(tmpc, bytes); if (File.Exists(copy)) File.Delete(copy); File.Move(tmpc, copy);
                if (origin == null) try { var ol = File.ReadAllLines(InfoFor(projectPath), Encoding.UTF8); if (ol.Length > 3 && ol[3].Contains("|")) origin = ol[3].Trim(); } catch { }
                File.WriteAllText(InfoFor(projectPath), projectPath + "\n" + (string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(projectPath) : name) + "\n" + DateTime.Now.Ticks + "\n" + (origin ?? "") + "\n", Encoding.UTF8);
                CloudBackup.Soon(); return true;
            }
            catch { return false; }
    }
    // taken off the list on purpose: the vault keeps the copy but does not put it back on the list by itself
    public static void MarkRemoved(string projectPath) { try { if (string.IsNullOrEmpty(projectPath)) return; Directory.CreateDirectory(ProjDir); File.WriteAllText(RemovedFor(projectPath), projectPath); } catch { } }
    public static bool IsRemoved(string projectPath) { try { return !string.IsNullOrEmpty(projectPath) && File.Exists(RemovedFor(projectPath)); } catch { return false; } }
    public static void Unmark(string projectPath) { try { if (string.IsNullOrEmpty(projectPath)) return; string r = RemovedFor(projectPath); if (File.Exists(r)) File.Delete(r); } catch { } }
    // a project deleted for good: its copy goes (audio other projects may share stays)
    static string ForgottenFile { get { return Path.Combine(ProjDir, "forgotten.txt"); } }
    public static void Forget(string projectPath) { try { if (string.IsNullOrEmpty(projectPath)) return; try { string o = OriginOf(InfoFor(projectPath), CloudBackup.SavedPc, projectPath); Directory.CreateDirectory(ProjDir); File.AppendAllText(ForgottenFile, o + "\n", Encoding.UTF8); } catch { } foreach (var f in new[] { CopyFor(projectPath), InfoFor(projectPath) }) if (File.Exists(f)) File.Delete(f); MarkRemoved(projectPath); } catch { } }
    public sealed class Kept { public string Path = "", Name = "", Copy = "", Origin = "", FromPc; public DateTime Saved; public bool Removed; }   // FromPc: the other PC's folder (null for this PC's own)
    static string OriginOf(string info, string pc, string path) { try { var l = File.ReadAllLines(info, Encoding.UTF8); if (l.Length > 3 && l[3].Contains("|")) return l[3].Trim(); } catch { } return (pc ?? "") + "|" + (path ?? "").ToLowerInvariant(); }
    public static List<Kept> All()
    {
        var r = new List<Kept>(); var dirs = new List<string> { ProjDir }; foreach (var o in Others()) dirs.Add(Path.Combine(o, "projects"));
        var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { if (File.Exists(ForgottenFile)) foreach (var x in File.ReadAllLines(ForgottenFile, Encoding.UTF8)) if (x.Trim().Length > 0) mine.Add(x.Trim()); } catch { }
        foreach (var pd in dirs)
        try
        {
            if (!Directory.Exists(pd)) continue;
            foreach (var inf in Directory.GetFiles(pd, "*.txt"))
            {
                try
                {
                    var l = File.ReadAllLines(inf, Encoding.UTF8); if (l.Length < 1 || l[0].Trim().Length == 0) continue; if (!File.Exists(System.IO.Path.ChangeExtension(inf, ".ytmproj"))) continue;
                    long t = 0; if (l.Length > 2) long.TryParse(l[2], out t);
                    string kp = l[0].Trim(); bool own = pd == ProjDir; string from = own ? null : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(pd));
                    string origin = OriginOf(inf, own ? CloudBackup.SavedPc : from, kp);
                    if (own) mine.Add(origin);
                    else { if (mine.Contains(origin)) continue; int same = r.FindIndex(x => x.FromPc != null && string.Equals(x.Origin, origin, StringComparison.OrdinalIgnoreCase)); if (same >= 0) { if (r[same].Saved.Ticks >= t) continue; r.RemoveAt(same); } }   /* a song this PC already has (its own, or brought over before) is not brought over again; of two copies of one song the newest */
                    r.Add(new Kept { Path = kp, Name = l.Length > 1 && l[1].Trim().Length > 0 ? l[1].Trim() : System.IO.Path.GetFileNameWithoutExtension(kp), Saved = new DateTime(Math.Max(0, t)), Removed = own && IsRemoved(kp), Copy = System.IO.Path.ChangeExtension(inf, ".ytmproj"), Origin = origin, FromPc = from });
                }
                catch { }
            }
        }
        catch { }
        return r;
    }
    static string Home { get { return StudioEnv.UnderTest ? Path.Combine(StudioEnv.DataDir, "music") : ProjectLibrary.DefaultFolder; } }   // (a test never writes into your Music folder)
    static string E(string s) { return s.Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A"); }
    // the songs of your other PCs that this PC does not have yet become songs of this PC: "<name> (from <PC>)" in the usual folder, using that
    // PC's copies of their audio (never a file of this PC that happens to have the same path). Returns how many came over.
    public static int AdoptOthers()
    {
        int n = 0;
        foreach (var k in All())
        {
            if (k.FromPc == null) continue;
            try
            {
                string od = Path.Combine(CloudBackup.OthersDir, k.FromPc), pcName = k.FromPc; try { string nf = Path.Combine(od, "pcname.txt"); if (File.Exists(nf)) pcName = File.ReadAllText(nf).Trim(); } catch { }
                var lines = File.ReadAllText(k.Copy, Encoding.UTF8).Replace("\r", "").Split('\n');
                for (int i = 0; i < lines.Length; i++) { if (!lines[i].StartsWith("track|")) continue; var p = lines[i].Split('|'); if (p.Length < 5) continue; string src = U(p[4]); if (src.Length == 0) continue; string ext = ""; try { ext = Path.GetExtension(src); } catch { } string theirs = Path.Combine(od, "audio", Key(src) + ext); if (File.Exists(theirs)) { p[4] = E(theirs); lines[i] = string.Join("|", p); } }
                string dir = Home; Directory.CreateDirectory(dir); string stem = string.Join("_", (k.Name + " (from " + pcName + ")").Split(Path.GetInvalidFileNameChars())), target = Path.Combine(dir, stem + ".ytmproj");
                for (int m = 2; File.Exists(target); m++) target = Path.Combine(dir, stem + " " + m + ".ytmproj");
                File.WriteAllText(target, string.Join("\n", lines), Encoding.UTF8);
                if (!Keep(target, k.Name + " (from " + pcName + ")", k.Origin)) { File.Delete(target); continue; }
                ProjectLibrary.AddIfNew(new ProjectEntry { Path = target, Name = k.Name + " (from " + pcName + ")", Updated = k.Saved, Opened = k.Saved }); n++;
            }
            catch { }
        }
        if (n > 0) ProjectLibrary.Save();
        return n;
    }
    // puts a deleted project file back from the vault: where it was, or in the usual folder if that cannot be written (the path it is at now, or null)
    public static string Bring(string projectPath, out string error)
    {
        error = null;
        try
        {
            string copy = FindCopy(projectPath); if (copy == null) { error = "Purphex has no copy of this project."; return null; }
            if (File.Exists(projectPath)) return projectPath;
            try { string d = Path.GetDirectoryName(projectPath); if (!string.IsNullOrEmpty(d)) { Directory.CreateDirectory(d); File.Copy(copy, projectPath, false); return projectPath; } } catch { }
            string dir = Home; Directory.CreateDirectory(dir); string stem = Path.GetFileNameWithoutExtension(projectPath), target = Path.Combine(dir, stem + ".ytmproj"); int n = 1;
            while (File.Exists(target)) { target = Path.Combine(dir, stem + " (brought back" + (n > 1 ? " " + n : "") + ").ytmproj"); n++; }
            File.Copy(copy, target, false); return target;
        }
        catch (Exception ex) { error = ex.Message; return null; }
    }
}

// Finds your songs again wherever they are: every project file in your own folders (Music, Documents, Desktop, Downloads, OneDrive...) comes back on the
// Projects page, a project whose file was deleted but whose history is still there is rebuilt from its newest saved state, and work that only lives in
// Purphex's history (never saved) is put on your Projects as a recovered song. Nothing is ever deleted or overwritten.
static class ProjectRescue
{
    static int started; static readonly DateTime started_at = System.Diagnostics.Process.GetCurrentProcess().StartTime;   /* the history of what you are working on right now is left alone */
    public static volatile int Found;   // how many songs came back this time
    public static Func<string> LiveHistory;   // the history folder of what is open right now (left alone)
    static readonly object rlk = new object();
    // a history folder that is already on your Projects (the crash prompt kept it): never recovered a second time
    public static void MarkRescued(string histDir) { if (string.IsNullOrEmpty(histDir)) return; lock (rlk) try { File.AppendAllText(RescuedFile, histDir + Environment.NewLine); } catch { } }
    public static void StartOnce()
    {
        if ((StudioEnv.UnderTest || Environment.GetEnvironmentVariable("PURPHEX_TESTRUN") == "1") && RootsOverride == null) return;   /* a test run never scans or writes into your real folders */
        if (System.Threading.Interlocked.Exchange(ref started, 1) == 1) return;
        var th = new System.Threading.Thread(() => { try { Run(); } catch { } }); th.IsBackground = true; th.Priority = System.Threading.ThreadPriority.Lowest; th.Start();
    }
    static string RescuedFile { get { return Path.Combine(StudioEnv.DataDir, "rescued.txt"); } }
    public static List<string> RootsOverride; public static string TargetOverride;   // a test points the search at its own folders
    static string Target { get { return TargetOverride ?? ProjectLibrary.DefaultFolder; } }
    static IEnumerable<string> Roots()
    {
        if (RootsOverride != null) return RootsOverride;
        var l = new List<string>(); Func<Environment.SpecialFolder, string> sf = f => { try { return Environment.GetFolderPath(f); } catch { return ""; } };
        foreach (var r in new[] { ProjectLibrary.DefaultFolder, sf(Environment.SpecialFolder.MyMusic), sf(Environment.SpecialFolder.MyDocuments), sf(Environment.SpecialFolder.DesktopDirectory), sf(Environment.SpecialFolder.MyVideos) }) if (!string.IsNullOrEmpty(r)) l.Add(r);
        string home = sf(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0) { l.Add(Path.Combine(home, "Downloads")); try { foreach (var d in Directory.GetDirectories(home, "OneDrive*")) l.Add(d); } catch { } }
        var full = new List<string>(); foreach (var r in l) { try { if (Directory.Exists(r)) { string f = Path.GetFullPath(r).TrimEnd('\\'); if (!full.Exists(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase))) full.Add(f); } } catch { } }
        return full.FindAll(f => !full.Exists(o => !string.Equals(o, f, StringComparison.OrdinalIgnoreCase) && f.StartsWith(o + "\\", StringComparison.OrdinalIgnoreCase)));   /* a folder inside another one is searched once */
    }
    // every project file and project history folder under the root (a few levels deep, never into hidden or system folders)
    static void Walk(string dir, int depth, List<string> projects, List<string> histories, ref int budget)
    {
        if (depth > 7 || budget-- <= 0) return;
        try { foreach (var f in Directory.GetFiles(dir, "*.ytmproj")) projects.Add(f); } catch { return; }
        string[] subs; try { subs = Directory.GetDirectories(dir); } catch { return; }
        foreach (var s in subs)
        {
            try
            {
                var di = new DirectoryInfo(s); var at = di.Attributes; if ((at & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                if ((at & FileAttributes.ReparsePoint) != 0 && s.IndexOf("\\OneDrive", StringComparison.OrdinalIgnoreCase) < 0) continue;   /* links and junctions are not followed; OneDrive folders are (only their names are read)*/
                if (s.EndsWith(".ytmproj.history", StringComparison.OrdinalIgnoreCase)) { histories.Add(s); continue; }
                string n = di.Name; if (n == "node_modules" || n.StartsWith(".")) continue;
                Walk(s, depth + 1, projects, histories, ref budget);
            }
            catch { }
        }
    }
    // the newest state in a history folder that still loads (null if none)
    public static string NewestState(string histDir)
    {
        try
        {
            string idx = Path.Combine(histDir, "index.txt"); var cands = new List<KeyValuePair<int, string>>();
            if (File.Exists(idx)) foreach (var line in File.ReadAllLines(idx)) { var p = line.Split('|'); int n; if (p.Length >= 5 && int.TryParse(p[0], out n)) { string f = Path.Combine(histDir, p[4]); if (File.Exists(f)) cands.Add(new KeyValuePair<int, string>(n, f)); } }
            foreach (var f in Directory.GetFiles(histDir, "*.ytproj")) { int n; if (int.TryParse(Path.GetFileNameWithoutExtension(f), out n) && !cands.Exists(c => string.Equals(c.Value, f, StringComparison.OrdinalIgnoreCase))) cands.Add(new KeyValuePair<int, string>(n, f)); }
            cands.Sort((a, b) => b.Key.CompareTo(a.Key));
            foreach (var c in cands) { try { var l = File.ReadAllLines(c.Value); if (l.Length > 0 && l[0].StartsWith("YTMPROJ") && Array.Exists(l, x => x.StartsWith("track|"))) return c.Value; } catch { } }
        }
        catch { }
        return null;
    }
    static void Add(string path, string name)
    {
        int tracks = 0; double bpm = 0; var roles = new List<string>();
        try { foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) { if (line.StartsWith("track|")) { tracks++; var p = line.Split('|'); if (p.Length > 1) roles.Add(p[1]); } else if (line.StartsWith("bpm|")) double.TryParse(line.Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out bpm); } } catch { }
        if (!ProjectLibrary.AddIfNew(new ProjectEntry { Path = path, Name = name, Tracks = tracks, Bpm = bpm, Kind = ProjectLibrary.KindOfRoles(roles), Updated = File.GetLastWriteTime(path), Opened = File.GetLastWriteTime(path) })) return;
        ProjectVault.Keep(path, name); Found++;   /* one at a time, on this background thread */
    }
    static string FreeName(string dir, string stem) { stem = string.Join("_", stem.Split(Path.GetInvalidFileNameChars())); string t = Path.Combine(dir, stem + ".ytmproj"); int n = 2; while (File.Exists(t)) t = Path.Combine(dir, stem + " " + n++ + ".ytmproj"); return t; }
    public static int Run()
    {
        ProjectLibrary.LoadOnce(); int before = Found;
        var rescued = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { if (File.Exists(RescuedFile)) foreach (var l in File.ReadAllLines(RescuedFile)) if (l.Trim().Length > 0) rescued.Add(l.Trim()); } catch { }
        var projects = new List<string>(); var histories = new List<string>();
        int budget = 30000; foreach (var r in Roots()) Walk(r, 0, projects, histories, ref budget);
        string live = null; try { if (LiveHistory != null) live = LiveHistory(); } catch { }
        // 1. project files anywhere in your folders
        foreach (var p in projects) { if (ProjectVault.IsRemoved(p)) continue; try { Add(p, Path.GetFileNameWithoutExtension(p)); } catch { } }
        // 2. a project file that is gone but its history is still beside it: rebuilt from its newest state, at its own place
        foreach (var h in histories)
        {
            try
            {
                string proj = h.Substring(0, h.Length - ".history".Length); if (File.Exists(proj) || ProjectVault.IsRemoved(proj) || rescued.Contains(h)) continue;
                string st = NewestState(h); if (st == null) continue; File.Copy(st, proj, false); rescued.Add(h); MarkRescued(h); Add(proj, Path.GetFileNameWithoutExtension(proj));
            }
            catch { }
        }
        // 3. songs that only live in Purphex's history (never saved, or the history of a project that is gone): put on your Projects as recovered songs
        try
        {
            string root = Path.Combine(StudioEnv.DataDir, "history");
            if (Directory.Exists(root))
                foreach (var h in Directory.GetDirectories(root))
                {
                    try
                    {
                        if (rescued.Contains(h) || Directory.GetLastWriteTime(h) >= started_at || (live != null && string.Equals(Path.GetFullPath(h).TrimEnd('\\'), Path.GetFullPath(live).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) continue; string st = NewestState(h); if (st == null) continue;
                        string dir = Target; Directory.CreateDirectory(dir);
                        string stem = "Recovered song " + File.GetLastWriteTime(st).ToString("d MMM yyyy HH-mm"), same = Path.Combine(dir, stem + ".ytmproj");
                        if (File.Exists(same) && new FileInfo(same).Length == new FileInfo(st).Length) { rescued.Add(h); MarkRescued(h); Add(same, stem); continue; }   /* already recovered once: never a second copy */
                        string target = FreeName(dir, stem); File.Copy(st, target, false); rescued.Add(h); MarkRescued(h);
                        Add(target, Path.GetFileNameWithoutExtension(target));
                    }
                    catch { }
                }
        }
        catch { }
        if (Found > before) { ProjectLibrary.Save(); ProjectLibrary.RestoreGone(); }
        return Found - before;
    }
}
