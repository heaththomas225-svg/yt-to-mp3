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
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string ids);

    // Dark title bar with a near-black purple caption (Windows 10 2004+ / Windows 11; harmlessly ignored elsewhere).
    public static void Dark(Form f)
    {
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
    }

    // Dark scroll bars / list popups for a control.
    public static void DarkControl(Control c)
    {
        Action a = delegate () { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
        if (c.IsHandleCreated) a(); else c.HandleCreated += delegate { a(); };
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