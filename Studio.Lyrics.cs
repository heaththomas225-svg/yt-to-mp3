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
    LyricBox lyricBox; TextBox lyricTitle; System.Windows.Forms.Timer lyricSave; bool lyricUi; Size lyricLayout;

    bool lyricStatsDirty = true; int statLines, statWords, statSyl;
    void EnsureLyricUi()
    {
        if (lyricUi) return; lyricUi = true;
        Func<bool, float, TextBox> mk = (multi, size) => { var t = new TextBox { Multiline = multi, Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", size), AcceptsReturn = multi, ScrollBars = ScrollBars.None }; Win.NeonBox(t); Controls.Add(t); Win.DarkControl(t); return t; };
        lyricBox = new LyricBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", Prefs.LyricSize), ScrollBars = RichTextBoxScrollBars.None, DetectUrls = false, WordWrap = true, AcceptsTab = false };
        Win.NeonBox(lyricBox); Controls.Add(lyricBox); Win.DarkControl(lyricBox); lyricBox.Wheel = d => LyricWheel(d);   // a rich text box: it scrolls smoothly (by pixels, not whole lines); pasting is always plain text
         lyricBox.KeyUp += (s, e) => Invalidate(); lyricBox.TextChanged += (s, e) => Invalidate(); lyricBox.MouseUp += (s, e) => Invalidate();   /* the logo on the scroll bar follows when you scroll or type in the box */ lyricTitle = mk(false, 13f);
        lyricBox.TextChanged += (s, e) => { lyricStatsDirty = true; ProjectIO.Lyrics = lyricBox.Text; if (lyricBox.Text.Length == 0 && lyricBox.Focused && lyricTitle.Text.Length > 0) lyricTitle.Text = ""; /* clearing all the words clears the title too */ lyricSave.Stop(); lyricSave.Start(); };
        lyricTitle.TextChanged += (s, e) => { ProjectIO.LyricsTitle = lyricTitle.Text; lyricSave.Stop(); lyricSave.Start(); };
        lyricBox.KeyUp += (s, e) => Invalidate(); lyricBox.MouseUp += (s, e) => Invalidate();
        lyricSave = new System.Windows.Forms.Timer { Interval = 900 }; lyricSave.Tick += (s, e) => { lyricSave.Stop(); try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"), ProjectIO.LyricsTitle + "\n" + ProjectIO.Lyrics, Encoding.UTF8); } catch { } };
        if (ProjectIO.Lyrics.Length == 0 && ProjectIO.LyricsTitle.Length == 0) { try { string f = Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"); if (File.Exists(f)) { string all = File.ReadAllText(f, Encoding.UTF8); int nl = all.IndexOf('\n'); if (nl >= 0) { ProjectIO.LyricsTitle = all.Substring(0, nl); ProjectIO.Lyrics = all.Substring(nl + 1); } } } catch { } }
    }

    // keeps the text boxes in step with the project (a loaded project replaces the lyrics) and with the window size
    void SyncLyricUi(bool show)
    {
        if (!lyricUi) { if (!show) return; EnsureLyricUi(); }
        if (lyricBox.Visible != show) lyricBox.Visible = show; bool titleOn = show && !lyricFill; if (lyricTitle.Visible != titleOn) lyricTitle.Visible = titleOn;   // (FILL SCREEN: only the lyrics box)
        if (!show) return;
        if (!lyricBox.Focused) { string lyr = (ProjectIO.Lyrics ?? "").Replace("\r\n", "\n"); if (lyricBox.Text.Replace("\r\n", "\n") != lyr) lyricBox.Text = lyr; }
        if (lyricTitle.Text != ProjectIO.LyricsTitle && !lyricTitle.Focused) lyricTitle.Text = ProjectIO.LyricsTitle;
        var sz = ClientSize; if (sz == lyricLayout) return; lyricLayout = sz;
        float top = ToolH + TransH, w = lyricFill ? sz.Width - 32 : Math.Min(1100, sz.Width - 64), x = (sz.Width - w) / 2;
        lyricTitle.SetBounds((int)x + 140, (int)top + 12, (int)w - 140, 28);
        if (lyricFill) lyricBox.SetBounds((int)x, (int)top + 52, (int)w - 56, (int)(sz.Height - top - 52 - 64)); else lyricBox.SetBounds((int)x, (int)top + 90, (int)w - 56, (int)(sz.Height - top - 90 - 64));
    }

    // A- / A+ and FILL SCREEN on the lyrics page
    // the lyrics scroll smoothly, by pixels: the wheel glides 3 lines quickly, a click on the scroll bar glides slowly (about 13 lines a second, so you can keep reading)
    internal int lyricGlideTo = -1; bool lyricGlideSlow; System.Windows.Forms.Timer lyricGlideT;
    internal void LyricScrollTo(int y, bool slow)
    {
        if (lyricBox == null || !lyricBox.IsHandleCreated) return; lyricGlideTo = Math.Max(0, Math.Min(lyricBox.MaxScrollY, y)); lyricGlideSlow = slow; lastInput = DateTime.Now;
        if (lyricGlideT == null) { lyricGlideT = new System.Windows.Forms.Timer { Interval = 15 }; lyricGlideT.Tick += (s, e) => LyricGlideStep(); }
        if (!lyricGlideT.Enabled) { LyricGlideStep(); lyricGlideT.Start(); }
    }
    internal void LyricGlide(int line) { if (lyricBox != null) LyricScrollTo(line * lyricBox.LineH, true); }
    internal void LyricGlideStep()
    {
        if (lyricBox == null || lyricGlideTo < 0 || !lyricBox.IsHandleCreated) { if (lyricGlideT != null) lyricGlideT.Stop(); lyricGlideTo = -1; return; }
        int cur = lyricBox.ScrollY, d = lyricGlideTo - cur; if (d == 0) { lyricGlideTo = -1; lyricGlideT.Stop(); return; }
        int step = lyricGlideSlow ? Math.Max(1, (int)Math.Round(lyricBox.LineH * 13 * 0.0156)) : Math.Max(1, (int)Math.Ceiling(Math.Abs(d) * 0.22));   // slow: a steady reading speed; the wheel: quick, easing out
        lyricBox.ScrollY = cur + Math.Sign(d) * Math.Min(Math.Abs(d), step); lastInput = DateTime.Now;
        if (lyricBox.ScrollY == cur) { lyricGlideTo = -1; lyricGlideT.Stop(); }   // (it cannot go further)
    }
    // the mouse wheel over the lyrics or beside them: 3 lines a notch, gliding there smoothly
    internal void LyricWheel(int delta)
    {
        if (lyricBox == null || !lyricBox.IsHandleCreated || delta == 0) return; int from = lyricGlideTo >= 0 && !lyricGlideSlow ? lyricGlideTo : lyricBox.ScrollY;
        LyricScrollTo(from - (int)Math.Round(delta / 120.0 * 3 * lyricBox.LineH), false); Invalidate();
    }
    internal bool lyricFill;
    internal void LyricSizeBy(float d)
    {
        float ns = Math.Max(9f, Math.Min(40f, Prefs.LyricSize + d)); if (Math.Abs(ns - Prefs.LyricSize) < 0.01f) { Toast(d > 0 ? "That is the biggest size." : "That is the smallest size."); return; }
        Prefs.LyricSize = ns; Prefs.Changed(); if (lyricBox != null) { int sel = lyricBox.SelectionStart; var old = lyricBox.Font; lyricBox.Font = new Font("Segoe UI", ns); old.Dispose(); lyricLineH = 0; lyricBox.SelectionStart = sel; lyricBox.ScrollToCaret(); }
        Invalidate();
    }
    internal void LyricFillToggle() { lyricFill = !lyricFill; lyricLayout = Size.Empty; lyricLineH = 0; Invalidate(); }
    int CaretLineIndex() { return lyricBox.GetLineFromCharIndex(lyricBox.SelectionStart); }
    string CaretLine() { int i = CaretLineIndex(); var ls = lyricBox.Lines; return i >= 0 && i < ls.Length ? ls[i] : ""; }
    void InsertAtCaret(string text, bool onNewLine)
    {
        text = text.Replace("\r\n", "\n"); int at = lyricBox.SelectionStart; string pre = lyricBox.Text.Substring(0, Math.Min(at, lyricBox.Text.Length)); if (onNewLine && pre.Length > 0 && !pre.EndsWith("\n")) text = "\n" + text;
        lyricBox.SelectedText = text; lyricBox.Focus();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);
    // the lyrics box scrolls with the same logo bar as everything else (the box itself shows no Windows scroll bar)
    void PaintLyricBar(Graphics g)
    {
        var b = lyricBox; if (b == null || !b.Visible || !b.IsHandleCreated) return; float viewH = b.ClientSize.Height, content = Math.Max(b.MaxScrollY + viewH, viewH + 2);   // in pixels: the bottom of the bar = the end of the lyrics   // the bar is always there (it only moves once the lyrics are longer than the box)
        { float mxL = Math.Max(0, content - viewH); regs.Insert(0, new HitReg { R = Rc(0, b.Top - 60, ClientSize.Width, b.Height + 120), OnWheel = d => LyricWheel(d * 120) }); }   // the wheel anywhere around the lyrics scrolls them (over the box itself Windows does it)
        VScroll(g, Rc(b.Right + 8, b.Top, 42, b.Height), content, viewH, () => lyricGlideTo >= 0 ? lyricGlideTo : b.ScrollY, v => { if (vLogoDrag) { lyricGlideTo = -1; if (lyricGlideT != null) lyricGlideT.Stop(); b.ScrollY = (int)Math.Round(v); } else LyricScrollTo((int)Math.Round(v), true); });
    }
    // the real distance between two lines in the text box (where line 2 starts minus where line 1 starts); the font height is only a guess and is a pixel or two short
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessage")] static extern IntPtr SendMessageP(IntPtr h, int msg, IntPtr w, IntPtr l);
    int lyricLineH;
    internal int LyricLineHeight(TextBoxBase tb)
    {
        var lb = tb as LyricBox; if (lb != null) return lb.LineH; var b = tb;
        try
        {
            if (SendMessage(b.Handle, 0xBA, 0, 0) >= 2) { int i0 = SendMessage(b.Handle, 0xBB, 0, 0), i1 = SendMessage(b.Handle, 0xBB, 1, 0);   // EM_LINEINDEX: the first character of line 1 and line 2
                long p0 = (long)SendMessageP(b.Handle, 0xD6, (IntPtr)i0, IntPtr.Zero), p1 = (long)SendMessageP(b.Handle, 0xD6, (IntPtr)i1, IntPtr.Zero);   // EM_POSFROMCHAR: y is the high word
                int y0 = (short)((p0 >> 16) & 0xFFFF), y1 = (short)((p1 >> 16) & 0xFFFF); if (i1 > i0 && y1 - y0 >= 8 && y1 - y0 <= 200) lyricLineH = y1 - y0; }
        }
        catch { }
        return lyricLineH > 0 ? lyricLineH : Math.Max(12, TextRenderer.MeasureText("Ag", b.Font).Height);
    }
    void PaintLyrics(Graphics g)
    {
        SyncLyricUi(!OverlayOpen);   // real text boxes sit above everything drawn by hand, so they step aside while a menu or popup is open
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + TransH, w = Math.Min(1100, W - 64), x = (W - w) / 2;
        if (lyricFill) { w = W - 32; x = 16; }
        Action<float, float> sizeBtns = (bx0, by0) => {
            var mn = Rc(bx0, by0, 46, 28); Btn(g, mn, "A-", 1, () => LyricSizeBy(-1.5f), 12); Hint2(mn, "Smaller lyrics (now " + Prefs.LyricSize.ToString("0.#") + " pt)");
            var pl = Rc(bx0 + 52, by0, 46, 28); Btn(g, pl, "A+", 1, () => LyricSizeBy(1.5f), 12); Hint2(pl, "Bigger lyrics (now " + Prefs.LyricSize.ToString("0.#") + " pt)");
            var fb = Rc(bx0 + 104, by0, lyricFill ? 172 : 128, 28); Btn(g, fb, lyricFill ? "EXIT FILL SCREEN" : "FILL SCREEN", lyricFill ? 4 : 2, () => LyricFillToggle(), 11); Hint2(fb, lyricFill ? "Back to the normal lyrics page (title and section buttons)" : "The lyrics fill the whole window (great for recording, or reading them while you play)"); };
        if (lyricFill) { Txt(g, "LYRICS" + (ProjectIO.LyricsTitle.Length > 0 ? "  -  " + ProjectIO.LyricsTitle.ToUpper() : ""), 15, Ink, Rc(x, top + 12, w - 340, 28), StringAlignment.Near, true); sizeBtns(x + w - 56 - 276, top + 12); }
        else {
        Txt(g, "LYRICS", 16, Ink, Rc(x, top + 10, 130, 28), StringAlignment.Near, true); Txt(g, "TITLE", 11, DimC, Rc(x + 74, top + 16, 60, 16), StringAlignment.Near, false);
        string[] sec = { "Verse", "Chorus", "Pre-chorus", "Bridge", "Hook", "Intro", "Outro" }; float sx = x, sy = top + 50;
        foreach (var s0 in sec) { string s1 = s0; float bw = s0.Length > 6 ? 96 : 66; Btn(g, Rc(sx, sy, bw, 28), s0, 0, () => { InsertAtCaret("[" + s1 + "]\r\n", true); }, 11); sx += bw + 6; }
        Btn(g, Rc(sx + 10, sy, 132, 28), "ADD SECTIONS", 2, () => AddLyricSections(), 11);
        if (sx + 10 + 132 + 12 + 232 <= x + w - 56) sizeBtns(sx + 10 + 132 + 12, sy); else sizeBtns(x + w - 56 - 232, top + 10);   // (on a narrow window they move up beside the title)
        }
        if (lyricStatsDirty && lyricBox != null) { lyricStatsDirty = false; string txt = lyricBox.Text; int ln = 0; foreach (var l in txt.Split('\n')) if (l.Trim().Length > 0) ln++; statLines = ln; statSyl = LyricsTools.LineSyllables(txt); statWords = LyricsTools.Words(txt); }
        string cur = lyricBox != null ? CaretLine() : ""; int ci = lyricBox != null ? CaretLineIndex() + 1 : 0;
        float stw = Math.Max(120f, x + w - 4 * 84 - 6 - x - 124); Txt(g, statLines + " lines   " + statWords + " words   " + statSyl + " syllables      Line " + ci + ": " + LyricsTools.LineSyllables(cur) + " syllables", 12, DimC, Rc(x, H - 52, stw, 20), StringAlignment.Near, false);
        Txt(g, "Saved with your project, and as a draft on this PC.", 11, DimC, Rc(x, H - 32, stw, 18), StringAlignment.Near, false);
        PaintLyricBar(g); float bx = x + w - 4 * 84 + 6; Btn(g, Rc(bx - 84, H - 54, 78, 30), "Open .txt", 0, () => OpenLyricsFile(), 12);
        { var emb = Rc(bx - 84 - 8 - 116, H - 54, 116, 30); Btn(g, emb, "EMOJIS", EmojiPanel.IsOpen ? 4 : 1, () => EmojiPanel.Toggle(this, em => InsertAtCaret(em, false)), 12); Hint2(emb, "Open the emoji panel: search by word (fire, heart, guitar...) and click an emoji to put it in your lyrics where the cursor is"); }
        Btn(g, Rc(bx, H - 54, 78, 30), "Copy", 0, () => { try { Clipboard.SetText(lyricBox.Text); Toast("Lyrics copied."); } catch { } }, 12);
        Btn(g, Rc(bx + 84, H - 54, 78, 30), "Save .txt", 0, () => SaveLyricsTxt(), 12);
        Btn(g, Rc(bx + 168, H - 54, 78, 30), "Clear", 3, () => { if (lyricBox.Text.Length == 0 || MessageBox.Show(this, "Clear all the lyrics?", "Lyrics", MessageBoxButtons.YesNo) == DialogResult.Yes) { lyricBox.Text = ""; lyricTitle.Text = ""; } }, 12);
    }

    // SAVE .TXT (the button, and the question when you leave the DAW with lyrics that were never saved): false = nothing was saved
    internal Func<string, string> lyricsPathHook;   // a test answers for itself (the file to write)
    internal bool SaveLyricsTxt()
    {
        string text = ProjectIO.Lyrics, title = ProjectIO.LyricsTitle, def = (title.Length > 0 ? title : "lyrics"); foreach (char c in Path.GetInvalidFileNameChars()) def = def.Replace(c, ' ');
        string path = null;
        if (lyricsPathHook != null) path = lyricsPathHook(def + ".txt");
        else using (var d = new SaveFileDialog { Title = "Save your lyrics", Filter = "Text|*.txt", FileName = def + ".txt" }) { if (d.ShowDialog(this) == DialogResult.OK) path = d.FileName; }
        if (string.IsNullOrEmpty(path)) return false;
        try { File.WriteAllText(path, text, Encoding.UTF8); ProjectIO.LyricsKept = text; Toast("Saved " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the lyrics: " + ex.Message, "Lyrics"); return false; }
    }
    // OPEN .TXT: load lyrics you wrote somewhere else (a notes app, a text file) into the writer
    string sectionNote = "";
    void OpenLyricsFile()
    {
        using (var d = new OpenFileDialog { Title = "Open lyrics", Filter = "Text files|*.txt|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return; string text;
            try { var fi = new FileInfo(d.FileName); if (fi.Length > 2 * 1024 * 1024) { Toast("That file is too big to be lyrics."); return; } text = File.ReadAllText(d.FileName, Encoding.UTF8); }
            catch (Exception ex) { Toast("Could not open that file: " + ex.Message); return; }
            string nl = "\n"; text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", nl); bool add = false;
            if (lyricBox.Text.Trim().Length > 0)
            {
                var r = MessageBox.Show(this, "You already have lyrics here." + nl + nl + "Yes = replace them with this file" + nl + "No = add this file to the end" + nl + "Cancel = leave everything as it is", "Open lyrics", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return; add = r == DialogResult.No;
            }
            int nAdded; string secNote; text = LyricSections.Add(text, out nAdded, out secNote).Replace("\n", nl);   // a text file without parts gets [Verse 1], [Chorus]... put in
            lyricBox.Text = add ? lyricBox.Text.TrimEnd() + nl + nl + text : text; if (nAdded > 0) sectionNote = secNote;
            if (!add || lyricTitle.Text.Trim().Length == 0) lyricTitle.Text = Path.GetFileNameWithoutExtension(d.FileName);   // the file name becomes the title at the top
            Toast("Opened " + Path.GetFileName(d.FileName) + (add ? " (added to the end). " : ". ") + (sectionNote.Length > 0 ? sectionNote : "")); sectionNote = "";
        }
    }
}

// The lyrics box: a rich text box, because it can scroll by single pixels (smooth), not only by whole lines. It always holds plain text:
// pasting drops any formatting, and the rich-text shortcuts (centre, right, line spacing...) do nothing. The mouse wheel is handed to the studio, which glides the scroll.
sealed class LyricBox : RichTextBox
{
    public Action<int> Wheel;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref Point p);
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x020A && Wheel != null) { Wheel((short)((m.WParam.ToInt64() >> 16) & 0xFFFF)); m.Result = IntPtr.Zero; return; }   // WM_MOUSEWHEEL
        base.WndProc(ref m);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { e.Handled = true; e.SuppressKeyPress = true; try { if (Clipboard.ContainsText()) SelectedText = Clipboard.GetText().Replace("\r\n", "\n").Replace("\r", "\n"); } catch { } return; }
        if (e.Control && (e.KeyCode == Keys.L || e.KeyCode == Keys.E || e.KeyCode == Keys.R || e.KeyCode == Keys.J || e.KeyCode == Keys.D1 || e.KeyCode == Keys.D2 || e.KeyCode == Keys.D5 || e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.I || e.KeyCode == Keys.B || e.KeyCode == Keys.U)) { e.Handled = true; e.SuppressKeyPress = true; return; }
        base.OnKeyDown(e);
    }
    // where the box is scrolled to, in pixels
    public int ScrollY { get { var p = new Point(); if (IsHandleCreated) SendMessage(Handle, 0x4DD, IntPtr.Zero, ref p); return p.Y; } set { if (!IsHandleCreated) return; var p = new Point(0, Math.Max(0, Math.Min(MaxScrollY, value))); SendMessage(Handle, 0x4DE, IntPtr.Zero, ref p); } }
    // the height of one line (from where line 2 starts), and how far it can scroll: until the last line is at the bottom
    public int LineH { get { try { if (TextLength > 0 && GetLineFromCharIndex(TextLength) >= 1) { int i1 = GetFirstCharIndexFromLine(1); if (i1 > 0) { int d = GetPositionFromCharIndex(i1).Y - GetPositionFromCharIndex(0).Y; if (d >= 8 && d <= 300) return d; } } } catch { } return Math.Max(12, TextRenderer.MeasureText("Ag", Font).Height); } }
    public int ContentH { get { if (TextLength == 0) return 0; int y = GetPositionFromCharIndex(TextLength - 1).Y + ScrollYRaw() + LineH; if (Text.EndsWith("\n")) y += LineH; return y + 4; } }
    int ScrollYRaw() { var p = new Point(); if (IsHandleCreated) SendMessage(Handle, 0x4DD, IntPtr.Zero, ref p); return p.Y; }
    public int MaxScrollY { get { return Math.Max(0, ContentH - ClientSize.Height); } }
}
