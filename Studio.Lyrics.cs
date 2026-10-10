using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ---- helpers for writing lyrics: syllables, rhymes (a free public word service) and an AI writing helper (needs your own key) ----
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

    // Datamuse (api.datamuse.com): free, no key. rel = "rel_rhy" (rhymes), "rel_nry" (near rhymes), "ml" (similar meaning)
    public static List<string> Rhymes(string word, string rel, out string error)
    {
        error = null; var list = new List<string>(); word = (word ?? "").Trim(); if (word.Length == 0) { error = "Type a word first."; return list; }
        try
        {
            var req = (HttpWebRequest)WebRequest.Create("https://api.datamuse.com/words?" + rel + "=" + Uri.EscapeDataString(word) + "&max=60"); req.Timeout = 12000; req.UserAgent = "YtToMp3-Lyrics";
            using (var rs = req.GetResponse()) using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                foreach (Match m in Regex.Matches(sr.ReadToEnd(), "\"word\":\"([^\"]+)\"")) list.Add(m.Groups[1].Value);
            if (list.Count == 0) error = "No matches found for \"" + word + "\".";
        }
        catch (Exception ex) { error = "Couldn't reach the rhyme service (" + ex.Message + "). Check your internet connection."; }
        return list;
    }

    // ---- AI (Anthropic Messages API). The key is yours: it is stored on this PC, protected by Windows for your account only. ----
    public const string Model = "claude-sonnet-5-5";
    static string KeyFile { get { return Path.Combine(StudioEnv.DataDir, "ai-key.bin"); } }
    public static bool HasKey { get { return File.Exists(KeyFile); } }
    public static string GetKey()
    {
        try { if (!File.Exists(KeyFile)) return null; return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), null, DataProtectionScope.CurrentUser)); } catch { return null; }
    }
    public static void SetKey(string key)
    {
        Directory.CreateDirectory(StudioEnv.DataDir); if (string.IsNullOrWhiteSpace(key)) { try { File.Delete(KeyFile); } catch { } return; }
        File.WriteAllBytes(KeyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
    }
    public static string Esc(string s)
    {
        var sb = new StringBuilder(); foreach (char c in s ?? "") { switch (c) { case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\n': sb.Append("\\n"); break; case '\r': break; case '\t': sb.Append("\\t"); break; default: if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4")); else sb.Append(c); break; } }
        return sb.ToString();
    }
    public static string Unesc(string s)
    {
        var sb = new StringBuilder(); for (int i = 0; i < s.Length; i++)
        {
            char c = s[i]; if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; } char n = s[++i];
            switch (n) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': break; case 'u': if (i + 4 < s.Length) { int v; if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) sb.Append((char)v); i += 4; } break; default: sb.Append(n); break; }
        }
        return sb.ToString();
    }
    public static string BuildRequest(string system, string user, int maxTokens)
    {
        return "{\"model\":\"" + Model + "\",\"max_tokens\":" + maxTokens + ",\"system\":\"" + Esc(system) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + Esc(user) + "\"}]}";
    }
    // the text of the reply (all text parts joined), or null with the service's error message
    public static string ParseReply(string json, out string error)
    {
        error = null; var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(json ?? "", "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")) { if (sb.Length > 0) sb.Append("\n"); sb.Append(Unesc(m.Groups[1].Value)); }
        if (sb.Length > 0) return sb.ToString();
        var e = Regex.Match(json ?? "", "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); error = e.Success ? Unesc(e.Groups[1].Value) : "The AI service gave no answer."; return null;
    }
    public static string Ask(string system, string user, int maxTokens, out string error)
    {
        error = null; string key = GetKey(); if (string.IsNullOrEmpty(key)) { error = "Add your AI key first (press AI KEY)."; return null; }
        try
        {
            byte[] body = Encoding.UTF8.GetBytes(BuildRequest(system, user, maxTokens)); var req = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/v1/messages");
            req.Method = "POST"; req.ContentType = "application/json"; req.Headers["x-api-key"] = key; req.Headers["anthropic-version"] = "2023-06-01"; req.Timeout = 90000; req.ContentLength = body.Length;
            using (var rq = req.GetRequestStream()) rq.Write(body, 0, body.Length);
            using (var rs = req.GetResponse()) using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) return ParseReply(sr.ReadToEnd(), out error);
        }
        catch (WebException ex)
        {
            string detail = ""; try { if (ex.Response != null) using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) { string j = sr.ReadToEnd(); string pe; ParseReply(j, out pe); detail = pe; } } catch { }
            var hr = ex.Response as HttpWebResponse; if (hr != null && (int)hr.StatusCode == 401) error = "The AI service did not accept your key. Check it and try again."; else error = "The AI service said: " + (string.IsNullOrEmpty(detail) ? ex.Message : detail);
            return null;
        }
        catch (Exception ex) { error = "Couldn't reach the AI service (" + ex.Message + ")."; return null; }
    }
    public const string System = "You are a songwriting partner for a rapper and singer-songwriter. Write in the style and mood they ask for. Keep lines singable, match syllable counts to the lines already written, and keep rhyme schemes consistent. Reply with only the lyrics or the short advice asked for, no preamble. Never reproduce lines from existing published songs.";
}

partial class StudioForm
{
    TextBox lyricBox, lyricTitle, rhymeBox, aiBox, aiOut; System.Windows.Forms.Timer lyricSave; bool lyricUi; string lyricLoaded = ""; Size lyricLayout;
    List<string> rhymeResults = new List<string>(); string rhymeNote = ""; volatile bool rhymeBusy, aiBusy; string aiNote = "";

    void EnsureLyricUi()
    {
        if (lyricUi) return; lyricUi = true;
        Func<bool, float, TextBox> mk = (multi, size) => { var t = new TextBox { Multiline = multi, Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", size), AcceptsReturn = multi, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None }; Controls.Add(t); Win.DarkControl(t); return t; };
        lyricBox = mk(true, 14f); lyricBox.AcceptsTab = false; lyricTitle = mk(false, 13f); rhymeBox = mk(false, 12f); aiBox = mk(true, 11f); aiOut = mk(true, 11f); aiOut.ReadOnly = true;
        lyricBox.TextChanged += (s, e) => { ProjectIO.Lyrics = lyricBox.Text; lyricSave.Stop(); lyricSave.Start(); };
        lyricTitle.TextChanged += (s, e) => { ProjectIO.LyricsTitle = lyricTitle.Text; lyricSave.Stop(); lyricSave.Start(); };
        lyricBox.KeyUp += (s, e) => Invalidate(); lyricBox.MouseUp += (s, e) => Invalidate();
        rhymeBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindRhymes("rel_rhy"); } };
        lyricSave = new System.Windows.Forms.Timer { Interval = 900 }; lyricSave.Tick += (s, e) => { lyricSave.Stop(); try { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllText(Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"), ProjectIO.LyricsTitle + "\n" + ProjectIO.Lyrics, Encoding.UTF8); } catch { } };
        if (ProjectIO.Lyrics.Length == 0 && ProjectIO.LyricsTitle.Length == 0) { try { string f = Path.Combine(StudioEnv.DataDir, "lyrics-draft.txt"); if (File.Exists(f)) { string all = File.ReadAllText(f, Encoding.UTF8); int nl = all.IndexOf('\n'); if (nl >= 0) { ProjectIO.LyricsTitle = all.Substring(0, nl); ProjectIO.Lyrics = all.Substring(nl + 1); } } } catch { } }
    }

    // keeps the native text boxes in step with the project (a loaded project replaces the lyrics) and with the window size
    void SyncLyricUi(bool show)
    {
        if (!lyricUi) { if (!show) return; EnsureLyricUi(); }
        foreach (var c in new Control[] { lyricBox, lyricTitle, rhymeBox, aiBox, aiOut }) if (c.Visible != show) c.Visible = show;
        if (!show) return;
        string lyr = LyricsTools.ToBoxText(ProjectIO.Lyrics); if (lyricBox.Text != lyr && !lyricBox.Focused) lyricBox.Text = lyr;
        if (lyricTitle.Text != ProjectIO.LyricsTitle && !lyricTitle.Focused) lyricTitle.Text = ProjectIO.LyricsTitle;
        var sz = ClientSize; if (sz == lyricLayout) return; lyricLayout = sz;
        float top = ToolH + TransH, lw = Math.Max(420, sz.Width * 0.5f), rx = lw + 40, rw = sz.Width - rx - 16;
        lyricTitle.SetBounds(16 + 140, (int)top + 12, (int)lw - 160, 28);
        lyricBox.SetBounds(16, (int)top + 90, (int)lw, (int)(sz.Height - top - 90 - 64));
        rhymeBox.SetBounds((int)rx, (int)top + 40, (int)Math.Min(260, rw * 0.4f), 28);
        float ay = top + 196;   // top of the AI panel, below the rhyme panel
        aiBox.SetBounds((int)rx, (int)ay + 54, (int)rw, 54);
        aiOut.SetBounds((int)rx, (int)ay + 170, (int)rw, (int)Math.Max(80, sz.Height - (ay + 170) - 52));
    }

    int CaretLineIndex() { return lyricBox.GetLineFromCharIndex(lyricBox.SelectionStart); }
    string CaretLine() { int i = CaretLineIndex(); var ls = lyricBox.Lines; return i >= 0 && i < ls.Length ? ls[i] : ""; }
    void InsertAtCaret(string text, bool onNewLine)
    {
        int at = lyricBox.SelectionStart; string pre = lyricBox.Text.Substring(0, at); if (onNewLine && pre.Length > 0 && !pre.EndsWith("\n")) text = "\r\n" + text;
        lyricBox.Text = lyricBox.Text.Substring(0, at) + text + lyricBox.Text.Substring(lyricBox.SelectionStart + lyricBox.SelectionLength); lyricBox.SelectionStart = at + text.Length; lyricBox.Focus();
    }

    void FindRhymes(string rel)
    {
        if (rhymeBusy) return; string w = rhymeBox.Text.Trim(); if (w.Length == 0) w = LyricsTools.LastWord(CaretLine()); if (w.Length == 0) { rhymeNote = "Type a word, or put the cursor on a line."; return; }
        rhymeBox.Text = w; rhymeBusy = true; rhymeNote = "Looking up \"" + w + "\"..."; rhymeResults = new List<string>(); string kind = rel == "rel_rhy" ? "Rhymes" : (rel == "rel_nry" ? "Near rhymes" : "Similar meaning");
        new Thread(() => { string err; var l = LyricsTools.Rhymes(w, rel, out err); rhymeResults = l; rhymeNote = err ?? (kind + " for \"" + w + "\": click one to put it in your lyrics"); rhymeBusy = false; }) { IsBackground = true }.Start();
    }

    void RunAi(string task, bool wantSelection)
    {
        if (aiBusy) return;
        if (!LyricsTools.HasKey) { AskAiKey(); if (!LyricsTools.HasKey) return; }
        string lyrics = lyricBox.Text, sel = lyricBox.SelectedText, extra = aiBox.Text.Trim(); if (wantSelection && sel.Trim().Length == 0) { aiNote = "Select some lyrics first, then press this."; return; }
        if (MessageBox.Show(this, "Pressing an AI button sends your lyrics and what you typed to Anthropic's AI service to get a reply, using your own key.\n\nContinue?", "AI writing helper", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var sb = new StringBuilder(); sb.Append("Song: ").Append(lyricTitle.Text.Length > 0 ? lyricTitle.Text : "(untitled)").Append(". Tempo ").Append(Fx.ProjectBpm.ToString("0")).Append(" BPM.\n\nLyrics so far:\n").Append(lyrics.Trim().Length > 0 ? lyrics : "(nothing yet)").Append("\n\n");
        switch (task)
        {
            case "continue": sb.Append("Task: write the next 4 to 8 lines that continue these lyrics, matching their flow, syllables and rhyme scheme."); break;
            case "rhyme": sb.Append("Task: here is the current line: \"").Append(CaretLine()).Append("\". Give 6 alternative versions of this line that rhyme better with the lines around it, keeping the meaning and a similar syllable count. One per line, numbered."); break;
            case "rewrite": sb.Append("Task: rewrite this selected part, keeping the meaning and syllable count but making it stronger:\n").Append(sel); break;
            case "feedback": sb.Append("Task: give short, honest feedback on these lyrics (flow, rhyme, imagery, what to fix first). Maximum 8 bullet points."); break;
            case "hook": sb.Append("Task: write 3 different hook (chorus) ideas of 4 lines each for this song."); break;
            default: sb.Append("Task: answer the writer's request below."); break;
        }
        if (extra.Length > 0) sb.Append("\n\nThe writer's instruction: ").Append(extra);
        string prompt = sb.ToString(); aiBusy = true; aiNote = "Thinking..."; aiOut.Text = "";
        new Thread(() =>
        {
            string err; string reply = LyricsTools.Ask(LyricsTools.System, prompt, 900, out err);
            try { BeginInvoke(new Action(() => { aiBusy = false; if (reply != null) { aiOut.Text = reply.Replace("\n", "\r\n"); aiNote = "Done. Insert it, replace your selection, or copy it."; } else aiNote = err ?? "No answer."; Invalidate(); })); } catch { aiBusy = false; }
        }) { IsBackground = true }.Start();
    }

    void AskAiKey()
    {
        try
        {
            string k = StudioDialogs.Ask(this, "Paste your AI key (from console.anthropic.com). It is stored only on this PC, protected for your Windows account. Leave empty to remove it:", ""); if (k == null) return;
            LyricsTools.SetKey(k); aiNote = LyricsTools.HasKey ? "Key saved." : "Key removed.";
        }
        finally { }
    }

    void PaintLyrics(Graphics g)
    {
        SyncLyricUi(true);
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + TransH, lw = Math.Max(420, W * 0.5f), rx = lw + 40, rw = W - rx - 16;
        Txt(g, "LYRICS", 16, Ink, Rc(16, top + 10, 130, 28), StringAlignment.Near, true); Txt(g, "title", 11, DimC, Rc(16 + 74, top + 16, 60, 16), StringAlignment.Near, false);
        // section buttons
        string[] sec = { "Verse", "Chorus", "Pre-chorus", "Bridge", "Hook", "Intro", "Outro" }; float sx = 16, sy = top + 50;
        foreach (var s0 in sec) { string s1 = s0; float bw = s0.Length > 6 ? 96 : 66; Btn(g, Rc(sx, sy, bw, 28), s0, 0, () => { InsertAtCaret("[" + s1 + "]\r\n", true); }, 11); sx += bw + 6; }
        // numbers
        string txt = lyricBox != null ? lyricBox.Text : ""; int lines = 0; foreach (var l in txt.Split('\n')) if (l.Trim().Length > 0) lines++; int syl = LyricsTools.LineSyllables(txt);
        string cur = lyricBox != null ? CaretLine() : ""; int ci = lyricBox != null ? CaretLineIndex() + 1 : 0;
        Txt(g, lines + " lines   " + LyricsTools.Words(txt) + " words   " + syl + " syllables      Line " + ci + ": " + LyricsTools.LineSyllables(cur) + " syllables", 12, DimC, Rc(16, H - 52, lw, 20), StringAlignment.Near, false);
        Txt(g, "Saved with your project, and as a draft on this PC.", 11, DimC, Rc(16, H - 32, lw, 18), StringAlignment.Near, false);
        float bx = 16 + lw - 3 * 84; Btn(g, Rc(bx, H - 54, 78, 30), "Copy", 0, () => { try { Clipboard.SetText(lyricBox.Text); Toast("Lyrics copied."); } catch { } }, 12);
        Btn(g, Rc(bx + 84, H - 54, 78, 30), "Save .txt", 0, () => { using (var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = (lyricTitle.Text.Length > 0 ? lyricTitle.Text : "lyrics") + ".txt" }) if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(d.FileName, lyricBox.Text, Encoding.UTF8); Toast("Saved " + Path.GetFileName(d.FileName)); } }, 12);
        Btn(g, Rc(bx + 168, H - 54, 78, 30), "Clear", 3, () => { if (lyricBox.Text.Length == 0 || MessageBox.Show(this, "Clear all the lyrics?", "Lyrics", MessageBoxButtons.YesNo) == DialogResult.Yes) lyricBox.Text = ""; }, 12);
        // right: rhymes
        var rp = Rc(rx - 12, top + 8, rw + 24, 180); FillRR(g, Color.FromArgb(120, 30, 16, 56), rp, 10); StrokeRR(g, Edge, rp, 10, 1);
        Txt(g, "RHYME FINDER", 12, Ink, Rc(rx, top + 14, 200, 20), StringAlignment.Near, true);
        float bx2 = rx + Math.Min(260, rw * 0.4f) + 8, by2 = top + 38; float bw2 = Math.Max(70, (rx + rw - bx2 - 12) / 3);
        Btn(g, Rc(bx2, by2, bw2, 30), "Rhymes", 1, () => FindRhymes("rel_rhy"), 11); Btn(g, Rc(bx2 + bw2 + 4, by2, bw2, 30), "Near", 0, () => FindRhymes("rel_nry"), 11); Btn(g, Rc(bx2 + 2 * (bw2 + 4), by2, bw2, 30), "Meaning", 0, () => FindRhymes("ml"), 11);
        Btn(g, Rc(rx, top + 72, 190, 24), "Use last word of this line", 0, () => { rhymeBox.Text = LyricsTools.LastWord(CaretLine()); FindRhymes("rel_rhy"); }, 10);
        Txt(g, rhymeNote, 11, DimC, Rc(rx + 196, top + 74, rw - 196, 20), StringAlignment.Near, false);
        float cx = rx, cy = top + 102; var shown = rhymeResults;
        foreach (var w in shown) { var sz = g.MeasureString(w, GfxCache.Font(12, false)); float cw = sz.Width + 16; if (cx + cw > rx + rw) { cx = rx; cy += 26; } if (cy > top + 168) break; string w1 = w; var chip = Rc(cx, cy, cw, 22); FillRR(g, Hot(chip) ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(160, 44, 24, 80), chip, 11); Txt(g, w, 12, Ink, chip, StringAlignment.Center, false); Reg(chip, () => InsertAtCaret(w1 + " ", false)); cx += cw + 6; }
        // right: AI
        float ay = top + 196;
        var ap = Rc(rx - 12, ay, rw + 24, H - ay - 12); FillRR(g, Color.FromArgb(120, 30, 16, 56), ap, 10); StrokeRR(g, Color.FromArgb(150, NeonViolet), ap, 10, 1);
        Txt(g, "AI WRITING HELPER", 12, Ink, Rc(rx, ay + 10, 220, 20), StringAlignment.Near, true);
        Btn(g, Rc(rx + rw - 110, ay + 6, 110, 26), LyricsTools.HasKey ? "AI KEY: set" : "AI KEY...", LyricsTools.HasKey ? 0 : 1, () => AskAiKey(), 11);
        Txt(g, "Tell it what you want (optional), then press a button:", 11, DimC, Rc(rx, ay + 34, rw, 16), StringAlignment.Near, false);
        float ab = ay + 114, aw = (rw - 5 * 4) / 6f; string[] an = { "Continue", "Rhyme better", "Rewrite selection", "Feedback", "Hook ideas", "Ask" }; string[] ak = { "continue", "rhyme", "rewrite", "feedback", "hook", "ask" };
        for (int i = 0; i < 6; i++) { string k = ak[i]; Btn(g, Rc(rx + i * (aw + 4), ab, aw, 30), an[i], i == 0 ? 1 : 0, () => RunAi(k, k == "rewrite"), aw < 100 ? 10 : 11); }
        Txt(g, aiBusy ? "Thinking..." : aiNote, 11, aiBusy ? Warn : Good, Rc(rx, ab + 34, rw, 16), StringAlignment.Near, false);
        float oy = (aiOut != null ? aiOut.Bottom + 6 : H - 60); float ob = oy; float w3 = (rw - 8) / 3f;
        Btn(g, Rc(rx, ob, w3, 28), "Insert at cursor", 1, () => { if (aiOut.Text.Length > 0) InsertAtCaret(aiOut.Text.Trim() + "\r\n", true); }, 11);
        Btn(g, Rc(rx + w3 + 4, ob, w3, 28), "Replace selection", 0, () => { if (aiOut.Text.Length > 0 && lyricBox.SelectionLength > 0) InsertAtCaret(aiOut.Text.Trim(), false); else Toast("Select the lyrics to replace first."); }, 11);
        Btn(g, Rc(rx + 2 * (w3 + 4), ob, w3, 28), "Copy reply", 0, () => { try { if (aiOut.Text.Length > 0) Clipboard.SetText(aiOut.Text); } catch { } }, 11);
    }
}
