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
    public string Path = "", Name = "", Cover = ""; public DateTime Updated, Opened; public int Tracks; public double Bpm, Seconds;
    public bool Missing { get { return !File.Exists(Path); } }
}

static class ProjectLibrary
{
    public static readonly List<ProjectEntry> Entries = new List<ProjectEntry>(); public static string ProfileName = "Producer", AvatarPath = "";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture; static readonly object lk = new object();
    static string ListFile { get { return Path.Combine(StudioEnv.DataDir, "projects.txt"); } }
    public static string CoversDir { get { return Path.Combine(StudioEnv.DataDir, "covers"); } }
    static string ProfileFile { get { return Path.Combine(StudioEnv.DataDir, "profile.txt"); } }
    public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"); } }

    public static void Load()
    {
        lock (lk)
        {
            Entries.Clear();
            try
            {
                if (File.Exists(ListFile))
                    foreach (var line in File.ReadAllLines(ListFile, Encoding.UTF8))
                    {
                        var f = line.Split('|'); if (f.Length < 9 || f[0] != "P") continue; long u, o; int t; double b, s; long.TryParse(f[3], out u); long.TryParse(f[4], out o); int.TryParse(f[5], out t); double.TryParse(f[6], NumberStyles.Float, CI, out b); double.TryParse(f[7], NumberStyles.Float, CI, out s);
                        Entries.Add(new ProjectEntry { Path = f[1], Name = f[2], Updated = new DateTime(Math.Max(0, u)), Opened = new DateTime(Math.Max(0, o)), Tracks = t, Bpm = b, Seconds = s, Cover = f[8] });
                    }
            }
            catch { }
            try { if (File.Exists(ProfileFile)) foreach (var line in File.ReadAllLines(ProfileFile, Encoding.UTF8)) { int i = line.IndexOf('='); if (i < 1) continue; string k = line.Substring(0, i), v = line.Substring(i + 1); if (k == "name" && v.Trim().Length > 0) ProfileName = v.Trim(); else if (k == "avatar") AvatarPath = v; } } catch { }
        }
    }
    public static void Save()
    {
        lock (lk)
            try
            {
                Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder();
                foreach (var e in Entries) sb.Append("P|").Append(e.Path.Replace("|", "/")).Append('|').Append(e.Name.Replace("|", "/")).Append('|').Append(e.Updated.Ticks).Append('|').Append(e.Opened.Ticks).Append('|').Append(e.Tracks).Append('|').Append(e.Bpm.ToString("0.##", CI)).Append('|').Append(e.Seconds.ToString("0.#", CI)).Append('|').Append(e.Cover).Append('\n');
                File.WriteAllText(ListFile, sb.ToString(), Encoding.UTF8);
                File.WriteAllText(ProfileFile, "name=" + ProfileName + "\navatar=" + AvatarPath + "\n", Encoding.UTF8);
            }
            catch { }
    }

    public static ProjectEntry Find(string path) { lock (lk) return Entries.Find(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)); }

    // called whenever a project is saved or opened
    public static void Touch(string path, string name, int tracks, double bpm, double seconds, bool saved, byte[] coverJpeg)
    {
        lock (lk)
        {
            var e = Find(path); if (e == null) { e = new ProjectEntry { Path = path }; Entries.Add(e); }
            e.Name = name; e.Tracks = tracks; e.Bpm = bpm; e.Seconds = seconds; e.Opened = DateTime.Now; if (saved) e.Updated = DateTime.Now; else if (e.Updated.Ticks == 0) { try { e.Updated = File.GetLastWriteTime(path); } catch { } }
            if (coverJpeg != null && coverJpeg.Length > 0) { try { Directory.CreateDirectory(CoversDir); string cp = Path_(path); File.WriteAllBytes(cp, coverJpeg); e.Cover = cp; Forget(cp); } catch { } }
            else if (e.Cover.Length > 0 && !File.Exists(e.Cover)) e.Cover = "";
        }
        Save();
    }
    static string Path_(string projectPath) { using (var sha = SHA1.Create()) return Path.Combine(CoversDir, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(projectPath.ToLowerInvariant()))).Replace("-", "").Substring(0, 16) + ".jpg"); }
    public static void Remove(string path) { lock (lk) Entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)); Save(); }
    public static List<ProjectEntry> Recent(int n) { lock (lk) { var l = new List<ProjectEntry>(Entries); l.RemoveAll(e => e.Missing); l.Sort((a, b) => b.Opened.CompareTo(a.Opened)); if (l.Count > n) l.RemoveRange(n, l.Count - n); return l; } }
    public static List<ProjectEntry> AllByEdited() { lock (lk) { var l = new List<ProjectEntry>(Entries); l.Sort((a, b) => { int m = a.Missing.CompareTo(b.Missing); return m != 0 ? m : b.Updated.CompareTo(a.Updated); }); return l; } }

    // projects saved in the usual folder before this list existed are picked up too
    public static void Discover()
    {
        try
        {
            if (!Directory.Exists(DefaultFolder)) return; bool changed = false;
            foreach (var f in Directory.GetFiles(DefaultFolder, "*.ytmproj"))
            {
                if (Find(f) != null) continue; int tracks = 0; double bpm = 0; byte[] cov = null;
                try { foreach (var line in File.ReadAllLines(f, Encoding.UTF8)) { if (line.StartsWith("track|")) tracks++; else if (line.StartsWith("bpm|")) double.TryParse(line.Substring(4), NumberStyles.Float, CI, out bpm); else if (line.StartsWith("cover|")) { try { cov = Convert.FromBase64String(line.Substring(6)); } catch { } } } } catch { }
                lock (lk) { var e = new ProjectEntry { Path = f, Name = System.IO.Path.GetFileNameWithoutExtension(f), Tracks = tracks, Bpm = bpm, Updated = File.GetLastWriteTime(f), Opened = File.GetLastWriteTime(f) }; if (cov != null) { try { Directory.CreateDirectory(CoversDir); File.WriteAllBytes(Path_(f), cov); e.Cover = Path_(f); } catch { } } Entries.Add(e); changed = true; }
            }
            if (changed) Save();
        }
        catch { }
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
                using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality; int side = Math.Min(src.Width, src.Height); var sr = new Rectangle((src.Width - side) / 2, (src.Height - side) / 2, side, side); g.DrawImage(src, new Rectangle(0, 0, size, size), sr, GraphicsUnit.Pixel); }
                var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.MimeType == "image/jpeg"); var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                using (var o = new MemoryStream()) { bmp.Save(o, codec, ep); return o.ToArray(); }
            }
        }
        catch { return null; }
    }

    // gives a saved project a picture: kept in the list and written into the project file too (so it travels with it)
    public static bool SetCover(string projectPath, string imageFile, out string error)
    {
        error = null; var big = MakeSquare(imageFile, 420); if (big == null) { error = "That file is not a picture Purphex can read (use a JPG, PNG, GIF or BMP)."; return false; }
        var small = MakeSquare(imageFile, 256);
        try
        {
            if (File.Exists(projectPath))
            {
                var lines = new List<string>(File.ReadAllLines(projectPath, Encoding.UTF8)); lines.RemoveAll(l => l.StartsWith("cover|")); lines.Insert(Math.Min(2, lines.Count), "cover|" + Convert.ToBase64String(small));
                string tmp = projectPath + ".tmp"; File.WriteAllLines(tmp, lines.ToArray(), Encoding.UTF8); File.Delete(projectPath); File.Move(tmp, projectPath);
            }
            Directory.CreateDirectory(CoversDir); string cp = Path_(projectPath); File.WriteAllBytes(cp, big); Forget(cp);
            lock (lk) { var e = Find(projectPath); if (e != null) e.Cover = cp; }
            Save(); return true;
        }
        catch (Exception ex) { error = "Couldn't save the picture: " + ex.Message; return false; }
    }
    public static void ClearCover(string projectPath)
    {
        try
        {
            if (File.Exists(projectPath)) { var lines = new List<string>(File.ReadAllLines(projectPath, Encoding.UTF8)); if (lines.RemoveAll(l => l.StartsWith("cover|")) > 0) { string tmp = projectPath + ".tmp"; File.WriteAllLines(tmp, lines.ToArray(), Encoding.UTF8); File.Delete(projectPath); File.Move(tmp, projectPath); } }
            lock (lk) { var e = Find(projectPath); if (e != null) { try { if (e.Cover.Length > 0) File.Delete(e.Cover); } catch { } Forget(e.Cover); e.Cover = ""; } }
            Save();
        }
        catch { }
    }
    public static bool SetAvatar(string imageFile, out string error)
    {
        error = null; var b = MakeSquare(imageFile, 256); if (b == null) { error = "That file is not a picture Purphex can read (use a JPG, PNG, GIF or BMP)."; return false; }
        try { Directory.CreateDirectory(CoversDir); string p = Path.Combine(CoversDir, "profile.jpg"); File.WriteAllBytes(p, b); Forget(p); AvatarPath = p; Save(); return true; } catch (Exception ex) { error = ex.Message; return false; }
    }

    // loaded pictures are kept so the lists draw fast
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    static void Forget(string path) { lock (cache) { Bitmap b; if (path != null && cache.TryGetValue(path, out b)) { cache.Remove(path); try { b.Dispose(); } catch { } } } }
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
    public static void DrawArt(Graphics g, RectangleF r, string name, int seed)
    {
        int h = Math.Abs((name ?? "").GetHashCode() + seed) % 360; Color a = FromHsv(h, 0.75, 0.55), b = FromHsv((h + 70) % 360, 0.85, 0.28);
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
