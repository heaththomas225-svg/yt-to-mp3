using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ======================================================================
//  Dark theme helpers: title bar, scrollbars, menus, drop-downs, check boxes. No white anywhere.
// ======================================================================
static class Win
{
    public static volatile bool AnyMoving;   // true while any of our windows is being dragged or resized: every animation pauses so the drag stays smooth
    public static void TrackMove(ref Message m) { if (m.Msg == 0x231) AnyMoving = true; else if (m.Msg == 0x232) AnyMoving = false; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string ids);

    // Dark title bar with a near-black purple caption (Windows 10 2004+ / Windows 11; harmlessly ignored elsewhere).
    public static void Dark(Form f)
    {
        try { if (AppLogo.Icon != null) f.Icon = AppLogo.Icon; } catch { }   // every window wears the Purphex emblem
        Action a = delegate ()
        {
            try
            {
                int one = 1; DwmSetWindowAttribute(f.Handle, 20, ref one, 4); DwmSetWindowAttribute(f.Handle, 19, ref one, 4);
                int cap = 10 | (4 << 8) | (20 << 16), txt = 235 | (225 << 8) | (255 << 16), border = 110 | (60 << 8) | (190 << 16);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); DwmSetWindowAttribute(f.Handle, 36, ref txt, 4); DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
            }
            catch { }
        };
        if (f.IsHandleCreated) a(); else f.HandleCreated += delegate { a(); };
        f.HandleCreated += delegate { HookFullscreen(f); };
    }

    // F11 toggles full screen (Esc leaves it) in every resizable window.
    static readonly System.Collections.Generic.Dictionary<Form, Rectangle> fsBounds = new System.Collections.Generic.Dictionary<Form, Rectangle>();
    static readonly System.Collections.Generic.Dictionary<Form, FormWindowState> fsState = new System.Collections.Generic.Dictionary<Form, FormWindowState>();
    static readonly System.Collections.Generic.HashSet<Form> fsHooked = new System.Collections.Generic.HashSet<Form>();
    public static bool IsFullscreen(Form f) { return fsBounds.ContainsKey(f); }
    public static void HookFullscreen(Form f)
    {
        if (f.FormBorderStyle != FormBorderStyle.Sizable || !f.MaximizeBox || fsHooked.Contains(f)) return;
        fsHooked.Add(f); f.KeyPreview = true;
        f.KeyDown += (s, e) => { if (e.KeyCode == Keys.F11) { ToggleFullscreen(f); e.Handled = true; } else if (e.KeyCode == Keys.Escape && IsFullscreen(f)) { ToggleFullscreen(f); e.Handled = true; } };
        f.FormClosed += (s, e) => { fsBounds.Remove(f); fsState.Remove(f); fsHooked.Remove(f); };
    }
    public static void ToggleFullscreen(Form f)
    {
        if (IsFullscreen(f))
        {
            f.FormBorderStyle = FormBorderStyle.Sizable; f.WindowState = FormWindowState.Normal; f.Bounds = fsBounds[f];
            if (fsState[f] == FormWindowState.Maximized) f.WindowState = FormWindowState.Maximized;
            fsBounds.Remove(f); fsState.Remove(f);
        }
        else
        {
            fsState[f] = f.WindowState; fsBounds[f] = f.WindowState == FormWindowState.Normal ? f.Bounds : f.RestoreBounds;
            var scr = Screen.FromControl(f).Bounds; f.WindowState = FormWindowState.Normal; f.FormBorderStyle = FormBorderStyle.None; f.Bounds = scr;
        }
    }

    // Dark scroll bars / list popups for a control.
    public static void DarkControl(Control c)
    {
        Action a = delegate () { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
        if (c.IsHandleCreated) a(); else c.HandleCreated += delegate { a(); };
    }

    // brings a window to the front, restoring it if it was minimised
    public static void Bring(Form f)
    {
        if (f == null || f.IsDisposed) return; if (!f.Visible) f.Show(); if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal; f.Activate(); f.BringToFront();
    }

    public static ContextMenuStrip NewMenu()
    {
        var m = new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Color.FromArgb(16, 8, 32), ForeColor = Color.FromArgb(240, 232, 255), ShowImageMargin = false, Font = new Font("Segoe UI", 10f) };
        return m;
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(96, 44, 180); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(96, 44, 180); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(96, 44, 180); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(190, 120, 255); } }
        public override Color MenuBorder { get { return Color.FromArgb(120, 70, 200); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(16, 8, 32); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(16, 8, 32); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(16, 8, 32); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(16, 8, 32); } }
        public override Color SeparatorDark { get { return Color.FromArgb(80, 50, 130); } }
        public override Color SeparatorLight { get { return Color.FromArgb(16, 8, 32); } }
    }
    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Color.FromArgb(240, 232, 255) : Color.FromArgb(120, 105, 150); base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Color.FromArgb(200, 160, 255); base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { using (var b = new SolidBrush(Color.FromArgb(190, 120, 255))) e.Graphics.FillEllipse(b, e.ImageRectangle.X + 4, e.ImageRectangle.Y + 4, e.ImageRectangle.Width - 8, e.ImageRectangle.Height - 8); }
    }
}

// Dark, glowing drop-down list (replaces the white system combo box).
class DarkCombo : ComboBox
{
    bool hot;
    public DarkCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; ItemHeight = 22;
        BackColor = Color.FromArgb(10, 5, 20); ForeColor = Color.FromArgb(240, 232, 255); Win.DarkControl(this);
    }
    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return; bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Color.FromArgb(104, 48, 196) : Color.FromArgb(14, 7, 28))) e.Graphics.FillRectangle(b, e.Bounds);
        TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), Color.FromArgb(240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0xF || m.Msg == 0x14)   // WM_PAINT / WM_ERASEBKGND: paint our own look on top
        {
            try
            {
                using (var g = Graphics.FromHwnd(Handle))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; var r = new Rectangle(0, 0, Width - 1, Height - 1);
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(18, 9, 36), Color.FromArgb(8, 4, 16), 90f)) g.FillRectangle(b, r);
                    using (var pen = new Pen(hot || Focused ? Color.FromArgb(200, 140, 255) : Color.FromArgb(120, 70, 200), 1.4f)) g.DrawRectangle(pen, r);
                    TextRenderer.DrawText(g, Text, Font, new Rectangle(8, 0, Width - 30, Height), Color.FromArgb(240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    using (var pen = new Pen(Color.FromArgb(210, 160, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { int cx = Width - 14, cy = Height / 2; g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2); g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2); }
                }
            }
            catch { }
        }
    }
}

// Neon check box on a dark pill.
class DarkCheck : CheckBox
{
    public DarkCheck() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); ForeColor = Color.FromArgb(240, 232, 255); BackColor = Color.FromArgb(14, 7, 28); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.FromArgb(14, 7, 28));
        var box = new Rectangle(4, (Height - 16) / 2, 16, 16);
        bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
        if (Checked)
        {
            using (var gl = new SolidBrush(Color.FromArgb(60, 190, 110, 255))) g.FillEllipse(gl, box.X - 5, box.Y - 5, box.Width + 10, box.Height + 10);
            using (var b = new LinearGradientBrush(box, Color.FromArgb(150, 60, 240), Color.FromArgb(236, 92, 226), 45f)) g.FillRectangle(b, box);
            using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(pen, box.X + 3, box.Y + 8, box.X + 7, box.Y + 12); g.DrawLine(pen, box.X + 7, box.Y + 12, box.X + 13, box.Y + 4); }
        }
        else using (var b = new SolidBrush(Color.FromArgb(6, 3, 12))) g.FillRectangle(b, box);
        using (var pen = new Pen(hot ? Color.FromArgb(230, 170, 255) : Color.FromArgb(150, 90, 220), 1.3f)) g.DrawRectangle(pen, box);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(26, 0, Width - 28, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

// High-precision frame pacing (about 1 ms timer resolution) instead of the ~15 ms WinForms timer.
// The loop only posts a new frame when the previous one has finished, so a slow PC never queues up work.
class FrameLoop : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int idx);

    // refresh rate of the primary display (Hz); 60 if Windows doesn't say
    public static int DisplayHz()
    {
        try { IntPtr dc = GetDC(IntPtr.Zero); int hz = GetDeviceCaps(dc, 116); ReleaseDC(IntPtr.Zero, dc); return hz > 30 && hz < 500 ? hz : 60; } catch { return 60; }
    }

    public int TargetFps = 120; public Func<bool> Slow;
    readonly Control owner; readonly Action tick; System.Threading.Thread th; volatile bool run = true, pending;

    public FrameLoop(Control owner, Action tick)
    {
        this.owner = owner; this.tick = tick; TargetFps = Math.Max(30, Math.Min(60, DisplayHz()));   // 60 is plenty for this style of animation and leaves the UI thread time to respond
        try { timeBeginPeriod(1); } catch { }
        th = new System.Threading.Thread(Loop) { IsBackground = true, Priority = System.Threading.ThreadPriority.AboveNormal }; th.Start();
    }

    void Loop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); double next = 0;
        while (run)
        {
            double interval = 1000.0 / ((Slow != null && Slow()) ? 30 : TargetFps); next += interval;
            if (!pending && owner.IsHandleCreated && !owner.IsDisposed)
            {
                pending = true;
                try { owner.BeginInvoke(new Action(() => { try { tick(); } catch { } finally { pending = false; } })); } catch { pending = false; }
            }
            double wait = next - sw.Elapsed.TotalMilliseconds;
            if (wait > 1) System.Threading.Thread.Sleep((int)wait); else if (wait < -100) next = sw.Elapsed.TotalMilliseconds;
        }
    }

    public void Dispose() { run = false; try { th.Join(300); } catch { } try { timeEndPeriod(1); } catch { } }
}
// Counts painted frames and draws a small badge: used in the converter window and on every Purphex Music screen.
sealed class FpsCounter
{
    int frames; long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); public int Fps; static int hz;
    public void Frame()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp(); frames++; double s = (now - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
        if (s >= 0.5) { Fps = (int)(frames / s + 0.5); frames = 0; t0 = now; }
    }
    public void Draw(Graphics g, float right, float bottom, bool idle)
    {
        if (!Prefs.ShowFps) return; if (hz == 0) hz = FrameLoop.DisplayHz(); int target = Math.Min(Prefs.FpsCap, hz); string text = idle ? "idle" : Fps + " FPS";
        Color c = idle ? Color.FromArgb(150, 140, 190) : (Fps >= target * 0.85 ? Color.FromArgb(110, 255, 150) : (Fps >= target * 0.45 ? Color.FromArgb(255, 210, 80) : Color.FromArgb(255, 100, 120)));
        var f = GfxCache.Font(12, true); var sz = g.MeasureString(text, f); float w = sz.Width + 30, h = 22; var r = new RectangleF(right - w, bottom - h, w, h);
        var oldS = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Color.FromArgb(210, 8, 4, 20))) g.FillRectangle(b, r); using (var p = new Pen(Color.FromArgb(180, c), 1.2f)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        using (var b = new SolidBrush(c)) { g.FillEllipse(b, r.X + 8, r.Y + 7, 8, 8); g.DrawString(text, f, b, r.X + 21, r.Y + 3); }
        g.SmoothingMode = oldS;
    }
}

// Shared drawing resources. Creating a Font / StringFormat for every piece of text on every frame was the main cost of the custom-drawn windows.
static class GfxCache
{
    static readonly System.Collections.Generic.Dictionary<int, Font> fonts = new System.Collections.Generic.Dictionary<int, Font>();
    static readonly System.Collections.Generic.Dictionary<int, StringFormat> fmts = new System.Collections.Generic.Dictionary<int, StringFormat>();
    public static Font Font(float px, bool bold)
    {
        int k = (int)(px * 10) * 2 + (bold ? 1 : 0); Font f;
        lock (fonts) { if (!fonts.TryGetValue(k, out f)) { f = new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); fonts[k] = f; } }
        return f;
    }
    public static StringFormat Fmt(StringAlignment ha, bool ellipsis)
    {
        int k = (int)ha * 2 + (ellipsis ? 1 : 0); StringFormat s;
        lock (fmts) { if (!fmts.TryGetValue(k, out s)) { s = new StringFormat { Alignment = ha, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None }; fmts[k] = s; } }
        return s;
    }
}
