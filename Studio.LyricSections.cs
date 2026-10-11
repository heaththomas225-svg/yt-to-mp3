using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Adds the section labels ([Verse 1], [Chorus], [Pre-chorus], [Bridge], [Intro], [Outro]) to lyrics that do not have them yet, so they look like proper lyrics.
// Parts you already labelled are left exactly as they are. It works from the shape of the writing: blank lines make the parts, a part that comes back
// (the same or nearly the same lines) is the chorus, a short part right before the chorus is a pre-chorus, and so on. It is a good first guess you can change.
static class LyricSections
{
    sealed class Block { public string Label; public List<string> Lines = new List<string>(); public bool Chorus; }
    static readonly Regex LabelLine = new Regex(@"^\s*\[[^\]\r\n]{1,30}\]\s*$");

    static string Key(string line) { return Regex.Replace((line ?? "").ToLowerInvariant(), "[^a-z0-9]", ""); }
    static double Same(Block a, Block b)
    {
        var ka = new HashSet<string>(); foreach (var l in a.Lines) { string k = Key(l); if (k.Length > 0) ka.Add(k); }
        var kb = new HashSet<string>(); foreach (var l in b.Lines) { string k = Key(l); if (k.Length > 0) kb.Add(k); }
        if (ka.Count == 0 || kb.Count == 0) return 0; int common = 0; foreach (var k in ka) if (kb.Contains(k)) common++; return common / (double)Math.Max(ka.Count, kb.Count);
    }
    public static bool HasLabels(string text) { foreach (var l in (text ?? "").Replace("\r", "").Split('\n')) if (LabelLine.IsMatch(l)) return true; return false; }

    // returns the text with labels added; added = how many labels were added; note = a sentence about what was done
    public static string Add(string text, out int added, out string note)
    {
        added = 0; note = ""; text = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n"); var raw = text.Split('\n'); var blocks = new List<Block>(); Block cur = null;
        foreach (var rl in raw)
        {
            string line = rl.TrimEnd();
            if (line.Trim().Length == 0) { cur = null; continue; }
            if (LabelLine.IsMatch(line)) { cur = new Block { Label = line.Trim() }; blocks.Add(cur); continue; }
            if (cur == null) { cur = new Block(); blocks.Add(cur); }
            cur.Lines.Add(line);
        }
        blocks.RemoveAll(b => b.Label == null && b.Lines.Count == 0);
        // writing with no blank lines at all (or one line per paragraph): cut it into parts of four lines
        int unl = 0, unlLines = 0; bool anyLabel = false; foreach (var b in blocks) { if (b.Label == null) { unl++; unlLines += b.Lines.Count; } else anyLabel = true; }
        if (!anyLabel && unlLines >= 8 && (unl == 1 || unlLines == unl))
        {
            var all = new List<string>(); foreach (var b in blocks) all.AddRange(b.Lines); blocks.Clear(); int per = all.Count % 4 == 0 ? 4 : (all.Count % 3 == 0 ? 3 : 4);
            for (int i = 0; i < all.Count; i += per) { var nb = new Block(); for (int j = i; j < Math.Min(all.Count, i + per); j++) nb.Lines.Add(all[j]); blocks.Add(nb); }
        }
        var todo = new List<Block>(); foreach (var b in blocks) if (b.Label == null) todo.Add(b);
        if (todo.Count == 0) { note = "Every part already has a label."; return Join(blocks); }
        // the chorus: the part that comes back most (or matches a part you already called Chorus)
        foreach (var b in blocks) if (b.Label != null && b.Label.ToLowerInvariant().Contains("chorus") && !b.Label.ToLowerInvariant().Contains("pre")) b.Chorus = true;
        foreach (var t in todo) foreach (var b in blocks) if (b.Chorus && b.Label != null && Same(t, b) >= 0.6) t.Chorus = true;
        Block best = null; int bestGroup = 1; double bestScore = 0;
        foreach (var a in todo)
        {
            if (a.Chorus) continue; var group = new List<Block> { a }; foreach (var b in todo) if (b != a && Same(a, b) >= 0.6) group.Add(b);
            double score = group.Count * 100 + a.Lines.Count; if (group.Count > bestGroup || (group.Count == bestGroup && group.Count > 1 && score > bestScore)) { best = a; bestGroup = group.Count; bestScore = score; }
        }
        if (best != null) { best.Chorus = true; foreach (var b in todo) if (b != best && Same(best, b) >= 0.6) b.Chorus = true; }
        // the other parts: intro, pre-chorus, verses, bridge, outro
        int verse = 0; foreach (var b in blocks) if (b.Label != null && b.Label.ToLowerInvariant().StartsWith("[verse")) verse++;
        bool bridgeDone = false; int nBlocks = blocks.Count; int chorusCount = 0, verses = 0, pre = 0, bridge = 0, intro = 0, outro = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i]; if (b.Label != null) continue;
            if (b.Chorus) { b.Label = "[Chorus]"; chorusCount++; continue; }
            bool chorusBefore = false, chorusAfter = false; for (int k = 0; k < i; k++) if (blocks[k].Chorus) chorusBefore = true; for (int k = i + 1; k < blocks.Count; k++) if (blocks[k].Chorus) chorusAfter = true;
            bool nextIsChorus = i + 1 < blocks.Count && blocks[i + 1].Chorus;
            if (i == 0 && b.Lines.Count <= 2 && nBlocks >= 4 && (chorusAfter || nBlocks >= 5)) { b.Label = "[Intro]"; intro++; }
            else if (i == blocks.Count - 1 && b.Lines.Count <= 2 && chorusBefore && nBlocks >= 4) { b.Label = "[Outro]"; outro++; }
            else if (nextIsChorus && b.Lines.Count <= 3 && verse >= 1) { b.Label = "[Pre-chorus]"; pre++; }
            else if (!bridgeDone && chorusBefore && chorusAfter && verse >= 2 && chorusCount >= 2) { b.Label = "[Bridge]"; bridgeDone = true; bridge++; }
            else { verse++; verses++; b.Label = "[Verse " + verse + "]"; }
        }
        added = todo.Count; var parts = new List<string>();
        if (intro > 0) parts.Add(intro + " intro"); if (verses > 0) parts.Add(verses + (verses == 1 ? " verse" : " verses")); if (pre > 0) parts.Add(pre + " pre-chorus"); if (chorusCount > 0) parts.Add(chorusCount + (chorusCount == 1 ? " chorus" : " choruses")); if (bridge > 0) parts.Add(bridge + " bridge"); if (outro > 0) parts.Add(outro + " outro");
        note = "Added " + added + (added == 1 ? " label" : " labels") + " (" + string.Join(", ", parts.ToArray()) + ")." + (best == null && chorusCount == 0 ? " No part comes back, so there is no chorus: use the buttons to change any label." : "");
        return Join(blocks);
    }
    static string Join(List<Block> blocks)
    {
        var sb = new StringBuilder(); for (int i = 0; i < blocks.Count; i++) { var b = blocks[i]; if (i > 0) sb.Append("\n\n"); if (b.Label != null) { sb.Append(b.Label); if (b.Lines.Count > 0) sb.Append('\n'); } sb.Append(string.Join("\n", b.Lines.ToArray())); }
        return sb.ToString();
    }
}

partial class StudioForm
{
    // the ADD SECTIONS button: put labels on the parts of the lyrics that do not have one yet (Ctrl+Z in the box takes it back)
    void AddLyricSections()
    {
        if (lyricBox == null || lyricBox.Text.Trim().Length == 0) { Toast("Write or open some lyrics first, then press ADD SECTIONS."); return; }
        int added; string note; string result = LyricSections.Add(lyricBox.Text, out added, out note);
        if (added == 0) { Toast(note); return; }
        result = result.Replace("\n", "\r\n"); lyricBox.SelectAll(); lyricBox.SelectedText = result; lyricBox.SelectionStart = 0; lyricBox.SelectionLength = 0; lyricBox.Focus(); Toast(note + " Ctrl+Z undoes it.");
    }
}
