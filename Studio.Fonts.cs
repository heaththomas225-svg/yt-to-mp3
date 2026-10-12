using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

// The Fonts page: the writing of the studio can be set in any of 190+ fonts (drippy, graffiti, neon, gothic, retro, script, handwritten, bold, serif, clean).
// The fonts are free open-licence fonts hosted on purphexmusic.com; a font is fetched the first time it is looked at or picked and kept on this PC. Windows' own fonts need no download.
// Fonts differ hugely in width and height (a drippy font is tall and loose, a stencil is wide), so every font gets a SCALE: the size at which a line of UI text is as wide, and as tall,
// as the original, which is what keeps every box, button and page tidy whatever font is chosen.
static class FontKit
{
    public sealed class Entry { public string Id, Name, Cat, Family; public int Bytes; public bool Installed; public float Scale; public FontFamily Fam; public volatile int State; public bool Pending; }   // State: 0 not here yet, 1 on its way, 2 failed
    public const string DefaultId = "default", InstalledCat = "On this PC";
    public static readonly string[] Cats = { "Drippy and horror", "Gothic and blackletter", "Graffiti and street", "Neon and futuristic", "Retro, pixel and arcade", "Script and calligraphy", "Handwritten", "Bold display", "Elegant serif", "Clean sans and mono", InstalledCat };
    public static readonly List<Entry> All = new List<Entry>();
    public static string CurrentId = DefaultId; public static FontFamily Family; public static float Scale = 1f;   // Family null = the original font
    public static string CurrentName { get { var e = Find(CurrentId); return e != null ? e.Name : "Bahnschrift (the original)"; } }
    static readonly List<PrivateFontCollection> keep = new List<PrivateFontCollection>(); static bool built; static readonly object lk = new object(); static readonly SemaphoreSlim gate = new SemaphoreSlim(3);
    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "fonts"); } }
    public static string FileOf(Entry e) { return Path.Combine(Dir, e.Id + ".ttf"); }
    static readonly string[] Windows = { "Segoe UI", "Segoe UI Black", "Segoe Script", "Segoe Print", "Ink Free", "Comic Sans MS", "Impact", "Arial Black", "Trebuchet MS", "Verdana", "Georgia", "Gabriola", "MV Boli", "Franklin Gothic Medium", "Candara", "Corbel", "Constantia", "Cambria", "Palatino Linotype", "Lucida Console", "Consolas", "Cascadia Mono", "Courier New", "Sitka Heading", "Tahoma", "Calibri", "Times New Roman", "Arial", "Lato", "Bahnschrift Condensed", "Segoe UI Variable Display" };

    // ---------- the list ----------
    public static Entry Find(string id) { foreach (var e in All) if (e.Id == id) return e; return null; }
    public static void Build()
    {
        lock (lk)
        {
            if (built) return; built = true; All.Clear();
            All.Add(new Entry { Id = DefaultId, Name = "Bahnschrift (the original)", Cat = InstalledCat, Family = Win.UiBoldFamily, Installed = true });
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { foreach (var f in new InstalledFontCollection().Families) have.Add(f.Name); } catch { }
            foreach (var n in Windows) if (have.Contains(n) && !string.Equals(n, Win.UiBoldFamily, StringComparison.OrdinalIgnoreCase)) All.Add(new Entry { Id = "win-" + n.ToLowerInvariant().Replace(' ', '-'), Name = n, Cat = InstalledCat, Family = n, Installed = true });
            ReadList(File.Exists(Path.Combine(Dir, "fonts.txt")) ? SafeRead(Path.Combine(Dir, "fonts.txt")) : "");
        }
    }
    static string SafeRead(string p) { try { return File.ReadAllText(p); } catch { return ""; } }
    // id <tab> name <tab> group <tab> bytes, one font per line
    internal static void ReadList(string text)
    {
        var known = new Dictionary<string, Entry>(); foreach (var e in All) known[e.Id] = e;
        foreach (var line in (text ?? "").Split('\n'))
        {
            var p = line.Trim('\r').Split('\t'); int b; if (p.Length < 4 || p[0].Length == 0 || p[0].Length > 40 || !int.TryParse(p[3], out b) || b < 1000 || b > 3000000) continue;
            foreach (char ch in p[0]) if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))) { p[0] = ""; break; } if (p[0].Length == 0) continue;
            if (known.ContainsKey(p[0])) continue; string cat = p[2]; if (Array.IndexOf(Cats, cat) < 0) cat = "Bold display";
            var e = new Entry { Id = p[0], Name = p[1].Length > 40 ? p[1].Substring(0, 40) : p[1], Cat = cat, Bytes = b }; All.Add(e); known[e.Id] = e;
        }
    }
    // the list is refreshed from the website in the background (what was kept on this PC is used meanwhile)
    public static void Refresh(Control ui, Action done)
    {
        ThreadPool.QueueUserWorkItem(o =>
        {
            try
            {
                var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/fonts/fonts.txt"); rq.UserAgent = "PurphexMusic"; rq.Timeout = 8000; rq.ReadWriteTimeout = 8000; string text;
                using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), System.Text.Encoding.UTF8)) text = sr.ReadToEnd();
                if (text.Length < 50 || text.Length > 100000) return; Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "fonts.txt"), text);
                Action apply = () => { lock (lk) ReadList(text); if (done != null) done(); }; if (ui != null && ui.IsHandleCreated && !ui.IsDisposed) ui.BeginInvoke(apply); else apply();
            }
            catch { }
        });
    }

    // ---------- getting a font ----------
    public static bool Ready(Entry e) { return e.Installed || File.Exists(FileOf(e)); }
    // fetch it in the background (at most three at once); done runs on the window's thread
    public static void Want(Entry e, Control ui, Action done)
    {
        if (Ready(e) || e.State == 1) return; e.State = 1;
        ThreadPool.QueueUserWorkItem(o =>
        {
            bool ok = false; gate.Wait();
            try
            {
                var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/fonts/" + e.Id + ".ttf"); rq.UserAgent = "PurphexMusic"; rq.Timeout = 20000; rq.ReadWriteTimeout = 20000;
                using (var r = (HttpWebResponse)rq.GetResponse()) using (var s = r.GetResponseStream()) using (var ms = new MemoryStream())
                {
                    var buf = new byte[16384]; int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 3000000) throw new IOException("too big"); }
                    var b = ms.ToArray(); bool sig = b.Length > 1000 && ((b[0] == 0 && b[1] == 1 && b[2] == 0 && b[3] == 0) || (b[0] == 'O' && b[1] == 'T' && b[2] == 'T' && b[3] == 'O') || (b[0] == 't' && b[1] == 'r' && b[2] == 'u' && b[3] == 'e'));
                    if (sig) { Directory.CreateDirectory(Dir); string tmp = FileOf(e) + ".tmp"; File.WriteAllBytes(tmp, b); if (File.Exists(FileOf(e))) File.Delete(FileOf(e)); File.Move(tmp, FileOf(e)); ok = true; }
                }
            }
            catch { ok = false; }
            finally { gate.Release(); }
            e.State = ok ? 0 : 2;
            Action fin = () => { if (done != null) done(); }; try { if (ui != null && ui.IsHandleCreated && !ui.IsDisposed) ui.BeginInvoke(fin); } catch { }
        });
    }
    // on the window's thread: the family of a font that is here (null when it cannot be loaded)
    public static FontFamily Load(Entry e)
    {
        if (e.Fam != null) return e.Fam;
        try
        {
            if (e.Installed) { e.Fam = new FontFamily(e.Family); }
            else if (File.Exists(FileOf(e))) { var pfc = new PrivateFontCollection(); pfc.AddFontFile(FileOf(e)); if (pfc.Families.Length == 0) return null; e.Fam = pfc.Families[0]; keep.Add(pfc); }
            if (e.Fam != null) e.Scale = ComputeScale(e.Fam);
        }
        catch { e.Fam = null; e.State = 2; try { File.Delete(FileOf(e)); } catch { } }
        return e.Fam;
    }

    // ---------- fitting ----------
    // the lines the studio really writes: capitals, a mix, numbers and signs. A font is sized so that the WORST of them is no wider than in the original font.
    static readonly string[] Lines = { "PURPHEX MUSIC", "SPLIT VOCALS + BEAT", "Lead vocal", "0:00 / 3:25", "-14.0 LUFS", "ADD EFFECT", "Parametric EQ", "SPLIT VOCALS + BEAT   MASTERING   PREVIEW MASTER   LOUDNESS 0123456789 dB", "Add Effect  Lead vocal  Parametric EQ  -14.0 LUFS  0:00 / 3:25  Purphex Music" };
    static Graphics scratch; static Bitmap scratchBmp;
    public static FontStyle StyleOf(FontFamily f, bool bold)
    {
        if (bold && f.IsStyleAvailable(FontStyle.Bold)) return FontStyle.Bold; if (f.IsStyleAvailable(FontStyle.Regular)) return FontStyle.Regular;
        foreach (var s in new[] { FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic }) if (f.IsStyleAvailable(s)) return s; return FontStyle.Regular;
    }
    static double WidthOf(FontFamily f, FontStyle st, string s)
    {
        lock (lk)
        {
            if (scratch == null) { scratchBmp = new Bitmap(1, 1); scratch = Graphics.FromImage(scratchBmp); scratch.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }
            using (var font = new Font(f, 100f, st, GraphicsUnit.Pixel)) return scratch.MeasureString(s, font, 1000000, StringFormat.GenericTypographic).Width;
        }
    }
    static double CapHeight(FontFamily f, FontStyle st)
    {
        using (var p = new GraphicsPath()) { p.AddString("HEXZ", f, (int)st, 100f, PointF.Empty, StringFormat.GenericTypographic); var b = p.GetBounds(); return b.Height > 1 ? b.Height : 70; }
    }
    // the size (relative to the original font) at which this font's UI text is as wide and as tall as the original's, so it fits the same boxes
    public static float ComputeScale(FontFamily f)
    {
        try
        {
            using (var fb = new FontFamily(Win.UiBoldFamily))
            {
                FontStyle sb = StyleOf(fb, true), sf = StyleOf(f, true);
                double wScale = 99; foreach (var line in Lines) { double wb = WidthOf(fb, sb, line), wf = WidthOf(f, sf, line); if (wf > 1 && wb > 1) wScale = Math.Min(wScale, wb / wf); } if (wScale > 50) return 1f;
                double lhBase = fb.GetLineSpacing(sb) / (double)Math.Max(1, fb.GetEmHeight(sb)), lhFont = f.GetLineSpacing(sf) / (double)Math.Max(1, f.GetEmHeight(sf));
                double capBase = CapHeight(fb, sb), capFont = CapHeight(f, sf);
                double s = Math.Min(wScale * 1.02, Math.Min((lhBase / lhFont) * 1.08, (capBase / capFont) * 1.22));
                return (float)Math.Max(0.5, Math.Min(1.6, s));
            }
        }
        catch { return 1f; }
    }

    // ---------- using it ----------
    public static Font Make(float px, bool bold)
    {
        var fam = Family; if (fam == null) return new Font(bold ? Win.UiBoldFamily : Win.UiFamily, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        return new Font(fam, Math.Max(5f, px * Scale), StyleOf(fam, bold), GraphicsUnit.Pixel);
    }
    // for the cards on the Fonts page: a font that is not the one in use
    public static Font Preview(Entry e, float px, bool bold)
    {
        var fam = e.Fam; if (fam == null) return GfxCache.Font(px, bold); return new Font(fam, Math.Max(5f, px * (e.Scale > 0 ? e.Scale : 1f)), StyleOf(fam, bold), GraphicsUnit.Pixel);
    }
    // on the window's thread; false when the font cannot be used
    public static bool Apply(Entry e)
    {
        if (e.Id == DefaultId) { Family = null; Scale = 1f; CurrentId = DefaultId; }
        else { var fam = Load(e); if (fam == null) return false; Family = fam; Scale = e.Scale > 0 ? e.Scale : 1f; CurrentId = e.Id; }
        Prefs.FontId = CurrentId == DefaultId ? "" : CurrentId; GfxCache.Clear(); Sprites.Clear(); Prefs.Changed(); return true;
    }
    // the font chosen last time (called when the studio opens); a font that is not on this PC any more is fetched again
    public static void Restore(Control ui)
    {
        string id = Prefs.FontId; if (id.Length == 0) { if (CurrentId != DefaultId) Apply(Find(DefaultId) ?? new Entry { Id = DefaultId }); return; }
        Build(); var e = Find(id); if (e == null) return;
        if (Ready(e)) { Apply(e); return; }
        Want(e, ui, () => { if (Prefs.FontId == e.Id) Apply(e); });
    }
}
