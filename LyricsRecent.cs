using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// The lyrics you generated and chose to keep "in the DAW": a title so you know the song, the lyrics, the date and where they came from. The newest is first; the last 30 are kept.
// One small file in the data folder (lyrics-recent.txt, one line per entry, the text escaped), so the generator window and the Lyrics page of Purphex Music read the same list.
sealed class RecentLyric { public string Id = "", Title = "", Source = "", Text = ""; public DateTime When; }

static class LyricsRecent
{
    public const int Max = 30; static readonly object lk = new object();
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "lyrics-recent.txt"); } }
    static string Esc(string s) { return Uri.EscapeDataString(s ?? ""); }
    static string Unesc(string s) { try { return Uri.UnescapeDataString(s ?? ""); } catch { return s ?? ""; } }

    static List<RecentLyric> Load()
    {
        var l = new List<RecentLyric>(); try
        {
            if (!File.Exists(FileP)) return l;
            foreach (string line in File.ReadAllLines(FileP, Encoding.UTF8))
            {
                string[] p = line.Split('\t'); if (p.Length < 5) continue; DateTime w; if (!DateTime.TryParse(p[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out w)) w = DateTime.Now;
                l.Add(new RecentLyric { Id = p[0], When = w, Title = Unesc(p[2]), Source = Unesc(p[3]), Text = Unesc(p[4]) });
            }
        }
        catch { }
        l.Sort((a, b) => b.When.CompareTo(a.When)); return l;
    }
    static void Save(List<RecentLyric> l)
    {
        try
        {
            Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder();
            foreach (var r in l) sb.Append(r.Id).Append('\t').Append(r.When.ToString("o", CultureInfo.InvariantCulture)).Append('\t').Append(Esc(r.Title)).Append('\t').Append(Esc(r.Source)).Append('\t').Append(Esc(r.Text)).Append('\n');
            Win.WriteAtomic(FileP, sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
    }

    // newest first
    public static List<RecentLyric> All() { lock (lk) return Load(); }
    public static RecentLyric Last() { lock (lk) { var l = Load(); return l.Count > 0 ? l[0] : null; } }
    public static int Count { get { lock (lk) return Load().Count; } }

    // keep these lyrics under this title. The same title with the same words only moves to the top.
    public static RecentLyric Add(string title, string text, string source)
    {
        title = (title ?? "").Trim(); if (title.Length == 0) title = "Untitled lyrics"; if (title.Length > 120) title = title.Substring(0, 120);
        lock (lk)
        {
            var l = Load(); RecentLyric same = null; foreach (var r in l) if (string.Equals(r.Title, title, StringComparison.OrdinalIgnoreCase) && r.Text == text) { same = r; break; }
            if (same != null) { same.When = DateTime.Now; same.Source = source ?? same.Source; Save(l); return same; }
            var n = new RecentLyric { Id = Guid.NewGuid().ToString("N").Substring(0, 12), Title = title, Text = text ?? "", Source = source ?? "", When = DateTime.Now }; l.Insert(0, n);
            l.Sort((a, b) => b.When.CompareTo(a.When)); while (l.Count > Max) l.RemoveAt(l.Count - 1); Save(l); return n;
        }
    }
    public static bool Remove(string id) { lock (lk) { var l = Load(); int n = l.RemoveAll(r => r.Id == id); if (n > 0) Save(l); return n > 0; } }
    public static void Clear() { lock (lk) { try { if (File.Exists(FileP)) File.Delete(FileP); } catch { } } }

    // "Today 14:05", "Yesterday", "12 Oct" for the list
    public static string Ago(DateTime w)
    {
        var d = DateTime.Now.Date - w.Date; if (d.Days == 0) return "today " + w.ToString("HH:mm", CultureInfo.InvariantCulture); if (d.Days == 1) return "yesterday"; return w.ToString("d MMM", CultureInfo.InvariantCulture);
    }
}
