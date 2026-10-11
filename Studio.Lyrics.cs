using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// A plain page for writing your own lyrics. No suggestions, no generated text: just your words, with counts to keep your bars even.
static class LyricsTools
{
    // rough English syllable count: groups of vowels, minus a silent final e
    public static int Syllables(string word)
    {
        string w = Regex.Replace((word ?? "").ToLowerInvariant(), "[^a-z]", ""); if (w.Length == 0) return 0; if (w.Length <= 3) return 1;
        w = Regex.Replace(w, "(?:[^laeiouy]es|ed|[^laeiouy]e)$", ""); w = Regex.Replace(w, "^y", "");
        int n = Regex.Matches(w, "[aeiouy]+").Count; return Math.Max(1, n);
    }
    public static int LineSyllables(string line) { int n = 0; foreach (Match m in Regex.Matches(line ?? "", "[A-Za-z']+")) n += Syllables(m.Value); return n; }
    public static string LastWord(string text) { var ms = Regex.Matches(text ?? "", "[A-Za-z']+"); return ms.Count == 0 ? "" : ms[ms.Count - 1].Value; }
    // a text box wants Windows line breaks; projects may hold either kind
    public static string ToBoxText(string s) { return (s ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"); }
    public static int Words(string text) { return Regex.Matches(text ?? "", "[A-Za-z0-9']+").Count; }
}

partial class StudioForm
{
    TextBox lyricBox, lyricTitle; System.Windows.Forms.Timer lyricSave; bool lyricUi; Size lyricLayout;

    bool lyricStatsDirty = true; int statLines, statWords, statSyl;
    void EnsureLyricUi()
    {
        if (lyricUi) return; lyricUi = true;
        Func<bool, float, TextBox> mk = (multi, size) => { var t = new TextBox { Multiline = multi, Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", size), AcceptsReturn = multi, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None }; Win.NeonBox(t); Controls.Add(t); Win.DarkControl(t); return t; };
        lyricBox = mk(true, 15f); lyricBox.AcceptsTab = false; lyricTitle = mk(false, 13f);
        lyricBox.TextChanged += (s, e) => { lyricStatsDirty = true; ProjectIO.Lyrics = lyricBox.Text; lyricSave.Stop(); lyricSave.Start(); };
        lyricTitle.TextChanged += (s, e) => { ProjectIO.LyricsTitle = lyricTitle.Text; lyricSave.Stop(); lyricSave.Start(); };
        lyricBox.KeyUp += (s, e) => Invalidate(); lyricBox.MouseUp += (s, e) => Invalidate();
        lyricSave = new System.Windows.Forms.Timer { Interval = 900 }; lyricSave.Tick += (s, e) => { lyricSave.Stop(); try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"), ProjectIO.LyricsTitle + "\n" + ProjectIO.Lyrics, Encoding.UTF8); } catch { } };
        if (ProjectIO.Lyrics.Length == 0 && ProjectIO.LyricsTitle.Length == 0) { try { string f = Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"); if (File.Exists(f)) { string all = File.ReadAllText(f, Encoding.UTF8); int nl = all.IndexOf('\n'); if (nl >= 0) { ProjectIO.LyricsTitle = all.Substring(0, nl); ProjectIO.Lyrics = all.Substring(nl + 1); } } } catch { } }
    }

    // keeps the text boxes in step with the project (a loaded project replaces the lyrics) and with the window size
    void SyncLyricUi(bool show)
    {
        if (!lyricUi) { if (!show) return; EnsureLyricUi(); }
        foreach (var c in new Control[] { lyricBox, lyricTitle }) if (c.Visible != show) c.Visible = show;
        if (!show) return;
        if (!lyricBox.Focused) { string lyr = LyricsTools.ToBoxText(ProjectIO.Lyrics); if (lyricBox.Text != lyr) lyricBox.Text = lyr; }
        if (lyricTitle.Text != ProjectIO.LyricsTitle && !lyricTitle.Focused) lyricTitle.Text = ProjectIO.LyricsTitle;
        var sz = ClientSize; if (sz == lyricLayout) return; lyricLayout = sz;
        float top = ToolH + TransH, w = Math.Min(1100, sz.Width - 64), x = (sz.Width - w) / 2;
        lyricTitle.SetBounds((int)x + 140, (int)top + 12, (int)w - 140, 28);
        lyricBox.SetBounds((int)x, (int)top + 90, (int)w, (int)(sz.Height - top - 90 - 64));
    }

    int CaretLineIndex() { return lyricBox.GetLineFromCharIndex(lyricBox.SelectionStart); }
    string CaretLine() { int i = CaretLineIndex(); var ls = lyricBox.Lines; return i >= 0 && i < ls.Length ? ls[i] : ""; }
    void InsertAtCaret(string text, bool onNewLine)
    {
        int at = lyricBox.SelectionStart; string pre = lyricBox.Text.Substring(0, at); if (onNewLine && pre.Length > 0 && !pre.EndsWith("\n")) text = "\r\n" + text;
        lyricBox.Text = lyricBox.Text.Substring(0, at) + text + lyricBox.Text.Substring(lyricBox.SelectionStart + lyricBox.SelectionLength); lyricBox.SelectionStart = at + text.Length; lyricBox.Focus();
    }

    void PaintLyrics(Graphics g)
    {
        SyncLyricUi(!(menuOpen || browserOpen || busy));   // real text boxes sit above everything drawn by hand, so they step aside while a menu or popup is open
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + TransH, w = Math.Min(1100, W - 64), x = (W - w) / 2;
        Txt(g, "LYRICS", 16, Ink, Rc(x, top + 10, 130, 28), StringAlignment.Near, true); Txt(g, "title", 11, DimC, Rc(x + 74, top + 16, 60, 16), StringAlignment.Near, false);
        string[] sec = { "Verse", "Chorus", "Pre-chorus", "Bridge", "Hook", "Intro", "Outro" }; float sx = x, sy = top + 50;
        foreach (var s0 in sec) { string s1 = s0; float bw = s0.Length > 6 ? 96 : 66; Btn(g, Rc(sx, sy, bw, 28), s0, 0, () => { InsertAtCaret("[" + s1 + "]\r\n", true); }, 11); sx += bw + 6; }
        if (lyricStatsDirty && lyricBox != null) { lyricStatsDirty = false; string txt = lyricBox.Text; int ln = 0; foreach (var l in txt.Split('\n')) if (l.Trim().Length > 0) ln++; statLines = ln; statSyl = LyricsTools.LineSyllables(txt); statWords = LyricsTools.Words(txt); }
        string cur = lyricBox != null ? CaretLine() : ""; int ci = lyricBox != null ? CaretLineIndex() + 1 : 0;
        float stw = Math.Max(120f, x + w - 4 * 84 - 6 - x); Txt(g, statLines + " lines   " + statWords + " words   " + statSyl + " syllables      Line " + ci + ": " + LyricsTools.LineSyllables(cur) + " syllables", 12, DimC, Rc(x, H - 52, stw, 20), StringAlignment.Near, false);
        Txt(g, "Saved with your project, and as a draft on this PC.", 11, DimC, Rc(x, H - 32, stw, 18), StringAlignment.Near, false);
        float bx = x + w - 4 * 84 + 6; Btn(g, Rc(bx - 84, H - 54, 78, 30), "Open .txt", 0, () => OpenLyricsFile(), 12);
        Btn(g, Rc(bx, H - 54, 78, 30), "Copy", 0, () => { try { Clipboard.SetText(lyricBox.Text); Toast("Lyrics copied."); } catch { } }, 12);
        Btn(g, Rc(bx + 84, H - 54, 78, 30), "Save .txt", 0, () => { using (var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = (lyricTitle.Text.Length > 0 ? lyricTitle.Text : "lyrics") + ".txt" }) if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(d.FileName, lyricBox.Text, Encoding.UTF8); Toast("Saved " + Path.GetFileName(d.FileName)); } }, 12);
        Btn(g, Rc(bx + 168, H - 54, 78, 30), "Clear", 3, () => { if (lyricBox.Text.Length == 0 || MessageBox.Show(this, "Clear all the lyrics?", "Lyrics", MessageBoxButtons.YesNo) == DialogResult.Yes) lyricBox.Text = ""; }, 12);
    }

    // OPEN .TXT: load lyrics you wrote somewhere else (a notes app, a text file) into the writer
    void OpenLyricsFile()
    {
        using (var d = new OpenFileDialog { Title = "Open lyrics", Filter = "Text files|*.txt|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return; string text;
            try { var fi = new FileInfo(d.FileName); if (fi.Length > 2 * 1024 * 1024) { Toast("That file is too big to be lyrics."); return; } text = File.ReadAllText(d.FileName, Encoding.UTF8); }
            catch (Exception ex) { Toast("Could not open that file: " + ex.Message); return; }
            string nl = Environment.NewLine; text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", nl); bool add = false;
            if (lyricBox.Text.Trim().Length > 0)
            {
                var r = MessageBox.Show(this, "You already have lyrics here." + nl + nl + "Yes = replace them with this file" + nl + "No = add this file to the end" + nl + "Cancel = leave everything as it is", "Open lyrics", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return; add = r == DialogResult.No;
            }
            lyricBox.Text = add ? lyricBox.Text.TrimEnd() + nl + nl + text : text;
            if (lyricTitle.Text.Trim().Length == 0) lyricTitle.Text = Path.GetFileNameWithoutExtension(d.FileName);
            Toast("Opened " + Path.GetFileName(d.FileName) + (add ? " (added to the end)." : "."));
        }
    }
}
