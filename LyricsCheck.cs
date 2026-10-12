using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// RECHECK VIDEO AND WORDS: the lyrics in the box are checked against what is really sung in the song or video (the speech recogniser listens to the file).
// The two word streams are lined up (the longest run of words they share, allowing a one letter slip such as "loud" and "lowd"), and then:
//   - lyric lines that are not heard in the song are listed (wrong words, a line from another version, or a part the recogniser could not make out),
//   - passages that ARE sung but are not in the lyrics are found (a missing verse is the usual one) and can be added in the right place.
// It cannot be perfect: a speech recogniser can mishear, so everything is shown to you with the reason, and nothing is changed without you pressing a button.
static class LyricsCheck
{
    public sealed class Missing { public int AfterLine; public string Text = ""; public int Words; }   // AfterLine: the content line (0-based, labels and blank lines not counted) it comes after; -1 = before the first
    public sealed class Report
    {
        public int Lines, Words, HeardWords, MatchedWords; public double Coverage, HeardCoverage;
        public List<int> NotHeard = new List<int>(); public List<string> NotHeardText = new List<string>(); public List<Missing> Passages = new List<Missing>();
        public bool Clean { get { return NotHeard.Count == 0 && Passages.Count == 0; } }
        public string Summary = "";
    }
    const int MinMissingWords = 6;   // a shorter unmatched bit is a mishearing, not a missing line

    static List<string> Words(string s)
    {
        var l = new List<string>(); foreach (Match m in Regex.Matches((s ?? "").ToLowerInvariant().Replace('’', '\''), @"[\p{L}\p{N}']+")) { string w = m.Value.Trim('\''); if (w.Length > 0) l.Add(w.Replace("'", "")); }
        return l;
    }
    static bool Same(string a, string b)
    {
        if (a == b) return true; if (a.Length < 4 || b.Length < 4 || Math.Abs(a.Length - b.Length) > 1) return false;   // short words must match exactly: one letter is too much of them
        if (a.Length == b.Length) { int d = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i] && ++d > 1) return false; return true; }   // one letter different
        string s = a.Length < b.Length ? a : b, l = a.Length < b.Length ? b : a; int k = 0; while (k < s.Length && s[k] == l[k]) k++; return s.Substring(k) == l.Substring(k + 1);   // one letter more or less
    }
    // the content lines of a lyric: not blank, not a [Verse] label
    public static List<string> ContentLines(string lyrics) { var l = new List<string>(); foreach (string x in (lyrics ?? "").Replace("\r\n", "\n").Split('\n')) { string t = x.Trim(); if (t.Length > 0 && !LyricsPolish.IsSection(t)) l.Add(t); } return l; }

    public static Report Compare(string lyrics, string heard)
    {
        var rep = new Report(); var lines = ContentLines(lyrics); rep.Lines = lines.Count;
        var lw = new List<string>(); var lwLine = new List<int>(); for (int i = 0; i < lines.Count; i++) foreach (string w in Words(lines[i])) { lw.Add(w); lwLine.Add(i); }
        var hw = Words(heard); rep.Words = lw.Count; rep.HeardWords = hw.Count;
        if (lw.Count == 0 || hw.Count == 0) { rep.Summary = lw.Count == 0 ? "There are no lyrics to check." : "No words could be heard in the song, so nothing could be checked."; return rep; }
        // the longest run of shared words (a table of how much of each stream lines up)
        int n = lw.Count, m = hw.Count; if ((long)n * m > 30000000L) { rep.Summary = "This song is too long to check in one go (" + n + " lyric words, " + m + " heard). Trim it into parts with TRIM START and TRIM END and check each part."; return rep; }
        var dp = new ushort[(n + 1) * (m + 1)]; int W = m + 1;
        for (int i = n - 1; i >= 0; i--) for (int j = m - 1; j >= 0; j--) dp[i * W + j] = Same(lw[i], hw[j]) ? (ushort)(dp[(i + 1) * W + j + 1] + 1) : Math.Max(dp[(i + 1) * W + j], dp[i * W + j + 1]);
        var lMatched = new bool[n]; var hMatched = new bool[m]; var hToL = new int[m]; for (int j = 0; j < m; j++) hToL[j] = -1;
        { int i = 0, j = 0; while (i < n && j < m) { if (Same(lw[i], hw[j]) && dp[i * W + j] == dp[(i + 1) * W + j + 1] + 1) { lMatched[i] = true; hMatched[j] = true; hToL[j] = i; i++; j++; } else if (dp[(i + 1) * W + j] >= dp[i * W + j + 1]) i++; else j++; } }
        int lm = 0, hm = 0; foreach (bool b in lMatched) if (b) lm++; foreach (bool b in hMatched) if (b) hm++; rep.MatchedWords = lm; rep.Coverage = lm / (double)n; rep.HeardCoverage = hm / (double)m;
        // lyric lines that are not in the song
        var per = new int[lines.Count]; var tot = new int[lines.Count]; for (int i = 0; i < n; i++) { tot[lwLine[i]]++; if (lMatched[i]) per[lwLine[i]]++; }
        for (int li = 0; li < lines.Count; li++) if (tot[li] > 0 && per[li] < Math.Ceiling(tot[li] * 0.5)) { rep.NotHeard.Add(li); rep.NotHeardText.Add(lines[li]); }
        // passages that are sung but are not in the lyrics: runs of unmatched heard words (long enough not to be a mishearing)
        int lastLyricWord = -1;
        for (int j = 0; j < m;)
        {
            if (hMatched[j]) { lastLyricWord = hToL[j]; j++; continue; }
            int s = j; while (j < m && !hMatched[j]) j++; int len = j - s;
            if (len >= MinMissingWords) { var sb = new StringBuilder(); for (int k = s; k < j; k++) { if (k > s) sb.Append(' '); sb.Append(hw[k]); } rep.Passages.Add(new Missing { AfterLine = lastLyricWord < 0 ? -1 : lwLine[lastLyricWord], Text = sb.ToString(), Words = len }); }
        }
        var parts = new List<string>(); parts.Add("The song matches " + (int)Math.Round(rep.Coverage * 100) + "% of the words in the box (" + lm + " of " + n + " words found in what is sung).");
        if (rep.NotHeard.Count == 0) parts.Add("Every line of the lyrics was heard in the song."); else parts.Add(rep.NotHeard.Count + (rep.NotHeard.Count == 1 ? " line was" : " lines were") + " not heard in the song (wrong words, a line from another version, or something the recogniser could not make out).");
        if (rep.Passages.Count == 0) parts.Add("No sung passage is missing from the lyrics."); else { int w = 0; foreach (var p in rep.Passages) w += p.Words; parts.Add(rep.Passages.Count + (rep.Passages.Count == 1 ? " passage" : " passages") + " (about " + w + " words) " + (rep.Passages.Count == 1 ? "is" : "are") + " sung in the song but missing from the lyrics, which is how a missing verse shows up."); }
        rep.Summary = string.Join(" ", parts.ToArray()); return rep;
    }

    // the lyrics with the missing passages added where they are sung (each in lines of about seven words, marked so they are easy to find and check)
    public static string Merge(string lyrics, Report r)
    {
        var src = (lyrics ?? "").Replace("\r\n", "\n").Split('\n'); var outl = new List<string>(); int content = -1; var byLine = new Dictionary<int, List<Missing>>();
        foreach (var p in r.Passages) { List<Missing> l; if (!byLine.TryGetValue(p.AfterLine, out l)) byLine[p.AfterLine] = l = new List<Missing>(); l.Add(p); }
        Action<int> insert = after => { List<Missing> l; if (!byLine.TryGetValue(after, out l)) return; foreach (var p in l) { outl.Add(""); outl.Add("[Heard in the song: check these words]"); var w = p.Text.Split(' '); for (int i = 0; i < w.Length; i += 7) outl.Add(string.Join(" ", w, i, Math.Min(7, w.Length - i))); outl.Add(""); } };
        insert(-1);
        foreach (string line in src) { outl.Add(line); string t = line.Trim(); if (t.Length > 0 && !LyricsPolish.IsSection(t)) { content++; insert(content); } }
        string text = string.Join("\n", outl.ToArray()); while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n"); return text.Trim('\n');
    }

    // the report as plain text for the window: the summary, the lines that were not heard and the passages that are missing
    public static string Describe(Report r)
    {
        var sb = new StringBuilder(); sb.AppendLine(r.Summary); sb.AppendLine();
        if (r.NotHeard.Count > 0) { sb.AppendLine("NOT HEARD IN THE SONG (check these lines):"); foreach (string t in r.NotHeardText) sb.AppendLine("   " + t); sb.AppendLine(); }
        if (r.Passages.Count > 0) { sb.AppendLine("SUNG BUT NOT IN THE LYRICS (press ADD THE MISSING PASSAGES to put them in):"); foreach (var p in r.Passages) sb.AppendLine("   " + (p.AfterLine < 0 ? "at the start" : "after line " + (p.AfterLine + 1)) + ": " + p.Text); sb.AppendLine(); }
        if (r.Clean) sb.AppendLine("Nothing to change: the lyrics and the song agree.");
        sb.AppendLine("A speech recogniser can mishear a word, so use this as a guide and listen to the song for anything it flags.");
        return sb.ToString();
    }
}
