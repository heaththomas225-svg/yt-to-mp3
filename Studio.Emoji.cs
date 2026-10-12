using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Text;

// Emoji in the writing the studio draws (your name, bio, project and track names...). The normal font has no emoji, so GDI+ drew empty boxes:
// the emoji are drawn with Segoe UI Emoji (one colour, the colour of the text) and the rest of the line keeps its own font.
static class EmojiText
{
    static readonly StringFormat Fmt = MakeFmt();
    static StringFormat MakeFmt() { var f = new StringFormat(StringFormat.GenericTypographic); f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap; return f; }
    static readonly Dictionary<int, Font> emojiFonts = new Dictionary<int, Font>();
    static string emojiFamily;
    static Font EmojiFont(float px)
    {
        int k = (int)(px * 10); Font f;
        lock (emojiFonts)
        {
            if (!emojiFonts.TryGetValue(k, out f))
            {
                if (emojiFamily == null) { emojiFamily = "Segoe UI Symbol"; try { foreach (var ff in FontFamily.Families) if (ff.Name == "Segoe UI Emoji") { emojiFamily = ff.Name; break; } } catch { } }
                f = new Font(emojiFamily, px, FontStyle.Regular, GraphicsUnit.Pixel); emojiFonts[k] = f;
            }
        }
        return f;
    }

    // marks that only say "show it as a picture" or join pictures together: the one-colour font has no use for them, so they are left out
    static bool Invisible(char c) { return c == '️' || c == '︎' || c == '‍' || c == '⃣'; }
    static bool IsPictureSymbol(string s, int i)
    {
        char c = s[i]; return (c >= '☀' && c <= '➿') && i + 1 < s.Length && s[i + 1] == '️';   // a heart or a star followed by the "picture" mark
    }
    // does the text hold anything that needs the emoji font (or marks that must be dropped)?
    public static bool Has(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++) { char c = s[i]; if (char.IsSurrogate(c) || Invisible(c)) return true; }
        return false;
    }

    struct Run { public string T; public bool Emoji; public float W; }
    static List<Run> Split(Graphics g, string s, float px, bool bold)
    {
        var runs = new List<Run>(); var sb = new StringBuilder(); bool inEmoji = false; var tf = GfxCache.Font(px, bold); var ef = EmojiFont(px);
        Action flush = () => { if (sb.Length == 0) return; string t = sb.ToString(); runs.Add(new Run { T = t, Emoji = inEmoji, W = g.MeasureString(t, inEmoji ? ef : tf, 100000, Fmt).Width }); sb.Length = 0; };
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i]; if (Invisible(c)) continue;
            bool em = char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) || IsPictureSymbol(s, i) || (char.IsLowSurrogate(c) && i > 0 && char.IsHighSurrogate(s[i - 1]));
            if (char.IsSurrogate(c) && !em) continue;   // half a pair (a cut-off emoji): nothing to draw
            if (em != inEmoji) { flush(); inEmoji = em; }
            sb.Append(c);
        }
        flush(); return runs;
    }
    static float Total(List<Run> runs) { float t = 0; foreach (var r in runs) t += r.W; return t; }

    // one line of writing in a box (the same way Txt does it: smaller when it does not fit, never cut off with a box)
    public static void Draw(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    {
        var runs = Split(g, s, px, bold); float total = Total(runs);
        if (total > r.Width && r.Width > 30) { float np = px, mn = Math.Max(9f, px * 0.72f); while (np > mn && total > r.Width) { np -= 0.5f; runs = Split(g, s, np, bold); total = Total(runs); } px = np; }
        float x = ha == StringAlignment.Near ? r.X : (ha == StringAlignment.Center ? r.X + (r.Width - total) / 2f : r.Right - total); var gs = g.Save(); g.SetClip(r, System.Drawing.Drawing2D.CombineMode.Intersect);
        using (var b = new SolidBrush(c))
            foreach (var run in runs)
            {
                var f = run.Emoji ? EmojiFont(px) : GfxCache.Font(px, bold); float fh = f.GetHeight(g); g.DrawString(run.T, f, b, x, r.Y + (r.Height - fh) / 2f, Fmt); x += run.W;
            }
        g.Restore(gs);
    }

    // writing that wraps onto as many lines as the box has room for (the last line ends with ... when something is left over)
    public static void DrawWrap(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha)
    {
        var tf = GfxCache.Font(px, false); float lh = tf.GetHeight(g) + 1; int maxLines = Math.Max(1, (int)Math.Floor((r.Height + 2) / lh)); var lines = new List<string>(); string cur = "";
        foreach (string word in s.Replace("\r", "").Replace("\n", " ").Split(' '))
        {
            if (word.Length == 0) continue; string test = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && Total(Split(g, test, px, false)) > r.Width) { lines.Add(cur); cur = word; } else cur = test;
        }
        if (cur.Length > 0) lines.Add(cur);
        if (lines.Count > maxLines) { lines = lines.GetRange(0, maxLines); string last = lines[maxLines - 1]; while (last.Length > 1 && Total(Split(g, last + "...", px, false)) > r.Width) { last = last.Substring(0, last.Length - 1); if (last.Length > 0 && char.IsHighSurrogate(last[last.Length - 1])) last = last.Substring(0, last.Length - 1); } lines[maxLines - 1] = last.TrimEnd() + "..."; }
        for (int i = 0; i < lines.Count; i++) Draw(g, lines[i], px, c, new RectangleF(r.X, r.Y + i * lh, r.Width, lh), ha, false);
    }

    // the first n characters without cutting an emoji in half
    public static string Cut(string s, int n)
    {
        if (s == null) return ""; if (s.Length <= n) return s; int k = Math.Max(0, n); if (k > 0 && k < s.Length && char.IsHighSurrogate(s[k - 1]) && char.IsLowSurrogate(s[k])) k--; return s.Substring(0, k);
    }
}
