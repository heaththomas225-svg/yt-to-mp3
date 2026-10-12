using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// COLOUR EMOJI in writing. Windows draws emoji in a text box (and with GDI+) in one colour, as outlines. The colour pictures come from the emoji font of this PC itself:
// a web browser that is never shown (off the screen) draws the emoji on black and then on white, the two pictures are captured with PrintWindow, and from them each emoji gets
// a colour picture with a see-through background. Those pictures are kept and drawn over the outlines. Nothing is downloaded and no emoji pictures are shipped.
static class EmojiSprites
{
    public static event Action Ready;   // new pictures have arrived (on the UI thread): whatever shows emoji should be drawn again
    const int Cell = 64, Cols = 8, Batch = 24;
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(); static readonly HashSet<string> asked = new HashSet<string>(); static readonly Queue<string> queue = new Queue<string>();
    static HostForm host; static WebBrowser wb; static System.Windows.Forms.Timer tick; static List<string> cur; static Bitmap onBlack; static int stage, waited; static bool busy;
    public static bool Disabled = true;   // a test (or a PC where it cannot work) can switch it off
    public static int Count { get { lock (cache) return cache.Count; } }

    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    sealed class HostForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; } }   // never takes the focus, never in Alt+Tab
    }

    // the colour picture of one emoji (null until it has been made, and for an emoji this PC cannot draw); asking starts making it
    public static Bitmap Get(string emoji)
    {
        if (Disabled || string.IsNullOrEmpty(emoji)) return null; Bitmap b;
        lock (cache) { if (cache.TryGetValue(emoji, out b)) return b; if (asked.Contains(emoji)) return null; asked.Add(emoji); queue.Enqueue(emoji); }
        try { Pump(); } catch { }
        return null;
    }
    public static bool Known(string emoji) { lock (cache) return cache.ContainsKey(emoji); }
    static void Pump()
    {
        if (busy) return; lock (cache) { if (queue.Count == 0) return; cur = new List<string>(); while (queue.Count > 0 && cur.Count < Batch) cur.Add(queue.Dequeue()); }
        busy = true; stage = 0; waited = 0; onBlack = null;
        try
        {
            if (host == null)
            {
                host = new HostForm { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-6000, -6000), ShowInTaskbar = false };
                wb = new WebBrowser { Dock = DockStyle.Fill, ScrollBarsEnabled = false, ScriptErrorsSuppressed = true, IsWebBrowserContextMenuEnabled = false, AllowWebBrowserDrop = false };
                host.Controls.Add(wb); wb.DocumentCompleted += (s, e) => { if (busy && stage % 2 == 0) stage++; };
                tick = new System.Windows.Forms.Timer { Interval = 60 }; tick.Tick += (s, e) => Step();
            }
            int rows = (cur.Count + Cols - 1) / Cols; host.ClientSize = new Size(Cols * Cell, rows * Cell); if (!host.Visible) host.Show(); host.Location = new Point(-6000, -6000);
            wb.DocumentText = Html(cur, "#000"); tick.Start();
        }
        catch { Finish(false); }
    }
    static string Html(List<string> list, string bg)
    {
        var sb = new StringBuilder("<html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'></head><body style='margin:0;overflow:hidden;background:" + bg + "'>");
        foreach (var e in list) sb.Append("<div style=\"float:left;width:" + Cell + "px;height:" + Cell + "px;line-height:" + Cell + "px;text-align:center;overflow:hidden;font-family:'Segoe UI Emoji';font-size:44px;color:#fff\">" + e + "</div>");
        return sb.Append("</body></html>").ToString();
    }
    // the states: 0 loading the black page, 1 loaded (wait, then capture), 2 loading the white page, 3 loaded (wait, then capture and build)
    static void Step()
    {
        try
        {
            waited++; if (waited > 100) { Finish(false); return; }
            if (stage == 1 || stage == 3)
            {
                if (waited < 5 + (stage - 1) * 0) { return; }
                var cap = Capture(); if (cap == null) { Finish(false); return; }
                if (stage == 1) { onBlack = cap; stage = 2; waited = 0; wb.DocumentText = Html(cur, "#fff"); }
                else { Build(onBlack, cap); onBlack.Dispose(); cap.Dispose(); Finish(true); }
            }
        }
        catch { Finish(false); }
    }
    static IntPtr FindIe(IntPtr p)
    {
        IntPtr c = FindWindowEx(p, IntPtr.Zero, "Internet Explorer_Server", null); if (c != IntPtr.Zero) return c;
        IntPtr ch = FindWindowEx(p, IntPtr.Zero, null, null); while (ch != IntPtr.Zero) { var r = FindIe(ch); if (r != IntPtr.Zero) return r; ch = FindWindowEx(p, ch, null, null); }
        return IntPtr.Zero;
    }
    static Bitmap Capture()
    {
        IntPtr ie = FindIe(host.Handle); if (ie == IntPtr.Zero) ie = host.Handle; int w = host.ClientSize.Width, h = host.ClientSize.Height; var bm = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bm)) { g.Clear(Color.Magenta); IntPtr hdc = g.GetHdc(); try { PrintWindow(ie, hdc, 0); } finally { g.ReleaseHdc(hdc); } }
        return bm;
    }
    // colour = what was drawn on black (that is the colour already multiplied by its opacity); opacity = 1 - how much brighter white shows it than black
    static void Build(Bitmap black, Bitmap white)
    {
        var made = new Dictionary<string, Bitmap>(); var rb = new Rectangle(0, 0, black.Width, black.Height); var db = black.LockBits(rb, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); var dw = white.LockBits(rb, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pb = new byte[Math.Abs(db.Stride) * db.Height]; var pw = new byte[Math.Abs(dw.Stride) * dw.Height]; Marshal.Copy(db.Scan0, pb, 0, pb.Length); Marshal.Copy(dw.Scan0, pw, 0, pw.Length);
            for (int k = 0; k < cur.Count; k++)
            {
                int cx = (k % Cols) * Cell, cy = (k / Cols) * Cell; var sp = new Bitmap(Cell, Cell, PixelFormat.Format32bppPArgb); var ds = sp.LockBits(new Rectangle(0, 0, Cell, Cell), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb); var po = new byte[Math.Abs(ds.Stride) * Cell]; int lit = 0;
                for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++)
                {
                    int ib = (cy + y) * db.Stride + (cx + x) * 4, iw = (cy + y) * dw.Stride + (cx + x) * 4, io = y * ds.Stride + x * 4;
                    int diff = (pw[iw] - pb[ib]) + (pw[iw + 1] - pb[ib + 1]) + (pw[iw + 2] - pb[ib + 2]); int a = 255 - Math.Max(0, Math.Min(255, diff / 3)); if (a < 6) { po[io] = po[io + 1] = po[io + 2] = po[io + 3] = 0; continue; }
                    lit++; po[io] = (byte)Math.Min(a, (int)pb[ib]); po[io + 1] = (byte)Math.Min(a, (int)pb[ib + 1]); po[io + 2] = (byte)Math.Min(a, (int)pb[ib + 2]); po[io + 3] = (byte)a;
                }
                Marshal.Copy(po, 0, ds.Scan0, po.Length); sp.UnlockBits(ds);
                if (lit > 120) made[cur[k]] = sp; else sp.Dispose();   // nothing drawn: this PC has no picture for it
            }
        }
        finally { black.UnlockBits(db); white.UnlockBits(dw); }
        lock (cache) foreach (var e in cur) { Bitmap b; cache[e] = made.TryGetValue(e, out b) ? b : null; }
    }
    static void Finish(bool ok)
    {
        if (tick != null) tick.Stop(); busy = false;
        if (!ok && cur != null) lock (cache) foreach (var e in cur) if (!cache.ContainsKey(e)) cache[e] = null;   // it did not work: the outline stays, and it is not tried again
        cur = null; if (host != null && host.Visible) host.Hide();
        bool more; lock (cache) more = queue.Count > 0; var h = Ready; if (h != null) { try { h(); } catch { } }
        if (more) Pump();
    }

    // ---- the emoji in a piece of writing ----
    // the length of the emoji that starts at i (a pair of surrogates, or a symbol with the picture mark after it), 0 when there is none
    public static int ClusterAt(string s, int i)
    {
        if (i < 0 || i >= s.Length) return 0; char c = s[i];
        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { int n = 2; if (i + n < s.Length && (s[i + n] == '️' || s[i + n] == '︎')) n++; return n; }
        if (c >= '←' && c <= '⯿' && i + 1 < s.Length && s[i + 1] == '️') return 2;
        return 0;
    }
    // the text that asks for the colour picture: a symbol always with the picture mark, no text mark
    public static string KeyOf(string cluster)
    {
        var sb = new StringBuilder(); foreach (char c in cluster) if (c != '︎' && c != '️') sb.Append(c); if (sb.Length == 1) sb.Append('️'); return sb.ToString();
    }
}

// the writing in a text box shows its emoji in colour: after the box has painted itself the colour pictures are drawn over the outlines
sealed class EmojiOverlay : NativeWindow
{
    static readonly List<TextBox> boxes = new List<TextBox>(); static bool hooked;
    readonly TextBox tb;
    EmojiOverlay(TextBox t) { tb = t; }
    public static void Attach(TextBox tb)
    {
        if (tb == null) return; lock (boxes) { if (boxes.Contains(tb)) return; boxes.Add(tb); }
        if (!hooked) { hooked = true; EmojiSprites.Ready += () => { TextBox[] all; lock (boxes) all = boxes.ToArray(); foreach (var b in all) { try { if (b.IsHandleCreated && !b.IsDisposed) b.Invalidate(); } catch { } } }; }
        var o = new EmojiOverlay(tb); if (tb.IsHandleCreated) o.AssignHandle(tb.Handle); tb.HandleCreated += (s, e) => { if (o.Handle == IntPtr.Zero) o.AssignHandle(tb.Handle); }; tb.HandleDestroyed += (s, e) => o.ReleaseHandle(); tb.Disposed += (s, e) => { lock (boxes) boxes.Remove(tb); };
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x000F || m.Msg == 0x0085) { try { Overlay(); } catch { } }   // WM_PAINT, WM_NCPAINT
    }
    void Overlay()
    {
        if (EmojiSprites.Disabled || tb.IsDisposed || !tb.IsHandleCreated) return; string t = tb.Text; if (t.Length == 0 || !EmojiText.Has(t)) return;
        int first = 0, last = t.Length - 1; try { first = Math.Max(0, tb.GetCharIndexFromPosition(new Point(1, 1)) - 2); last = Math.Min(t.Length - 1, tb.GetCharIndexFromPosition(new Point(tb.ClientSize.Width - 2, tb.ClientSize.Height - 2)) + 2); } catch { }
        int lineH = Math.Max(8, TextRenderer.MeasureText("A", tb.Font).Height); Graphics g = null; bool focused = tb.Focused; int ss = tb.SelectionStart, se = tb.SelectionStart + tb.SelectionLength;
        for (int i = first; i <= last; i++)
        {
            int n = EmojiSprites.ClusterAt(t, i); if (n == 0) continue; string cl = t.Substring(i, n); var sp = EmojiSprites.Get(EmojiSprites.KeyOf(cl)); int at = i; i += n - 1; if (sp == null) continue;
            Point p = tb.GetPositionFromCharIndex(at), p2 = tb.GetPositionFromCharIndex(Math.Min(t.Length, at + n)); int w = p2.Y == p.Y ? p2.X - p.X : lineH + 4; if (w < 6) w = lineH + 2; if (p.Y < -lineH || p.Y > tb.ClientSize.Height || p.X < -w || p.X > tb.ClientSize.Width) continue;
            if (g == null) { g = tb.CreateGraphics(); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality; }
            bool sel = focused && tb.SelectionLength > 0 && at >= ss && at < se; var back = new Rectangle(p.X, p.Y, w, lineH + 1);
            using (var br = new SolidBrush(sel ? SystemColors.Highlight : tb.BackColor)) g.FillRectangle(br, back);
            int s = Math.Min(w, lineH + 2); g.DrawImage(sp, new Rectangle(p.X + (w - s) / 2, p.Y + (lineH - s) / 2 + 1, s, s));
        }
        if (g != null) g.Dispose();
    }
}
