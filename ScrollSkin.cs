using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// The scroll bar of the text boxes and lists in the program's windows (the log, the links box, the lyrics, the changelog, the tags...): slim, neon-outlined, in the colours of the theme,
// like the bar of the DAW: a track with a lit thumb and a chevron at each end (hold one to scroll, click the track to page). It replaces the grey Windows bar.
abstract class NeonBar : Control
{
    public const int BarW = 16;
    protected int total = 1, first, vis = 1; int drag = -1, dragFirst, dragY, hold, holdTick; bool hotThumb, hotUp, hotDown;
    static readonly List<NeonBar> all = new List<NeonBar>(); static System.Windows.Forms.Timer tick;
    protected abstract void Metrics(out int t, out int f, out int v);   // the size of what is scrolled, where it is and how much shows (in lines, or in steps of 20 pixels)
    protected abstract void ScrollBy(int units);
    protected NeonBar()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); SetStyle(ControlStyles.Selectable, false); Cursor = Cursors.Default; TabStop = false; all.Add(this);
        if (tick == null) { tick = new System.Windows.Forms.Timer { Interval = 60 }; tick.Tick += (a, b) => { foreach (var x in all.ToArray()) x.Sync(); }; tick.Start(); }
    }
    protected override void Dispose(bool d) { all.Remove(this); base.Dispose(d); }
    protected void Sync()
    {
        if (!Visible || IsDisposed) return; int t, f, v; Metrics(out t, out f, out v); t = Math.Max(1, t); v = Math.Max(1, v);
        if (t != total || f != first || v != vis) { total = t; first = f; vis = v; Invalidate(); }
        if (hold != 0 && unchecked(Environment.TickCount - holdTick) > 350) Scroll(hold);
    }
    void Scroll(int lines) { ScrollBy(lines); Sync(); }
    int ArrowH { get { return Math.Min(14, Height / 4); } }
    RectangleF Track { get { return new RectangleF(2, ArrowH + 1, Width - 4, Math.Max(10, Height - 2 * ArrowH - 2)); } }
    bool Scrolls { get { return total > vis; } }
    RectangleF Thumb
    {
        get
        {
            var t = Track; if (!Scrolls) return new RectangleF(t.X, t.Y, t.Width, t.Height); float h = Math.Max(26f, t.Height * vis / total), range = t.Height - h; int maxFirst = Math.Max(1, total - vis);
            return new RectangleF(t.X, t.Y + range * Math.Min(1f, first / (float)maxFirst), t.Width, h);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent != null ? Parent.BackColor : Color.Black);
        Color cold = Win.NeonIdle, hot = Win.NeonHot; var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var p = Round(box, 7)) { using (var br = new SolidBrush(Color.FromArgb(235, 8, 4, 18))) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, cold), 1.2f)) g.DrawPath(pen, p); }
        float aw = ArrowH; foreach (int up in new[] { 1, 0 })
        {
            bool h2 = up == 1 ? hotUp : hotDown; float cy = up == 1 ? aw / 2f + 1 : Height - aw / 2f - 1, d = 3.2f, cx = Width / 2f;
            using (var pen = new Pen(h2 ? Color.White : Color.FromArgb(220, hot), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(pen, up == 1 ? new[] { new PointF(cx - d, cy + 1.5f), new PointF(cx, cy - 2f), new PointF(cx + d, cy + 1.5f) } : new[] { new PointF(cx - d, cy - 1.5f), new PointF(cx, cy + 2f), new PointF(cx + d, cy - 1.5f) });
        }
        var th = Thumb; th.Inflate(-1.5f, 0); if (th.Height < 8) return; bool on = Scrolls;
        using (var p = Round(th, 5.5f))
        {
            using (var br = new LinearGradientBrush(th, Color.FromArgb(on ? 255 : 90, hotThumb || drag >= 0 ? Win.NeonHot : cold), Color.FromArgb(on ? 255 : 90, Mix(cold, Color.FromArgb(255, 150, 40, 210), 0.55f)), 0f)) g.FillPath(br, p);
            using (var br = new LinearGradientBrush(th, Color.FromArgb(on ? 110 : 30, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p);
            using (var pen = new Pen(Color.FromArgb(on ? 230 : 90, hot), 1.1f)) g.DrawPath(pen, p);
            if (on && th.Height > 30) using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.4f)) { float cy = th.Y + th.Height / 2f, cx = th.X + th.Width / 2f; for (int i = -1; i <= 1; i++) g.DrawLine(pen, cx - 3, cy + i * 3.5f, cx + 3, cy + i * 3.5f); }
        }
    }
    static Color Mix(Color a, Color b, float t) { return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
    static GraphicsPath Round(RectangleF r, float rad) { var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; } p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); bool a = e.Y < ArrowH, b = e.Y > Height - ArrowH, t = !a && !b && Thumb.Contains(e.X, e.Y) && Scrolls;
        if (a != hotUp || b != hotDown || t != hotThumb) { hotUp = a; hotDown = b; hotThumb = t; Invalidate(); }
        if (drag >= 0) { var tr = Track; float range = Math.Max(1f, tr.Height - Thumb.Height); int maxFirst = Math.Max(1, total - vis), want = (int)Math.Round(dragFirst + (e.Y - dragY) / range * maxFirst); Scroll(Math.Max(0, Math.Min(maxFirst, want)) - first); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hotUp = hotDown = hotThumb = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left || !Scrolls) return;
        if (e.Y < ArrowH) { hold = -1; holdTick = Environment.TickCount; Scroll(-1); }
        else if (e.Y > Height - ArrowH) { hold = 1; holdTick = Environment.TickCount; Scroll(1); }
        else if (Thumb.Contains(e.X, e.Y)) { drag = 1; dragFirst = first; dragY = e.Y; Capture = true; }
        else Scroll(e.Y < Thumb.Y ? -Math.Max(1, vis - 1) : Math.Max(1, vis - 1));
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = -1; hold = 0; Capture = false; Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Scroll(-Math.Sign(e.Delta) * 3); }
}

// the bar of a multi-line text box
sealed class NeonScroll : NeonBar
{
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);
    const int EM_GETLINECOUNT = 0xBA, EM_GETFIRSTVISIBLELINE = 0xCE, EM_LINESCROLL = 0xB6, EM_SETMARGINS = 0xD3, EC_RIGHTMARGIN = 2;
    readonly TextBox tb; static readonly List<NeonScroll> mine = new List<NeonScroll>();
    // skins the scroll bar of a multi-line text box (call after its other settings; it can be called before the box is placed in its window)
    public static NeonScroll Attach(TextBox tb)
    {
        if (tb == null || !tb.Multiline) return null; foreach (var o in mine) if (o.tb == tb) return o;
        tb.ScrollBars = ScrollBars.None; var s = new NeonScroll(tb); mine.Add(s); tb.Disposed += (a, b) => { mine.Remove(s); s.Dispose(); }; return s;
    }
    NeonScroll(TextBox box)
    {
        tb = box; Action place = Place; tb.ParentChanged += (a, b) => place(); tb.Resize += (a, b) => place(); tb.LocationChanged += (a, b) => place(); tb.VisibleChanged += (a, b) => place(); tb.HandleCreated += (a, b) => { Margin(); place(); };
        tb.TextChanged += (a, b) => Sync(); if (tb.IsHandleCreated) Margin(); place();
    }
    void Margin() { try { SendMessage(tb.Handle, EM_SETMARGINS, EC_RIGHTMARGIN, (BarW + 6) << 16); } catch { } }
    void Place()
    {
        var p = tb.Parent; if (p == null) { Visible = false; return; }
        if (Parent != p) { if (Parent != null) Parent.Controls.Remove(this); p.Controls.Add(this); }
        int bw = tb.BorderStyle == BorderStyle.None ? 0 : 2; SetBounds(tb.Right - BarW - 2 - bw, tb.Top + 2 + bw, BarW, Math.Max(20, tb.Height - 4 - 2 * bw)); Visible = tb.Visible; BringToFront();
    }
    protected override void Metrics(out int t, out int f, out int v)
    {
        t = 1; f = 0; v = 1; if (!tb.IsHandleCreated) return;
        t = SendMessage(tb.Handle, EM_GETLINECOUNT, 0, 0); f = SendMessage(tb.Handle, EM_GETFIRSTVISIBLELINE, 0, 0); v = (tb.ClientSize.Height - 2) / Math.Max(8, tb.Font.Height);
    }
    protected override void ScrollBy(int lines) { if (tb.IsHandleCreated) SendMessage(tb.Handle, EM_LINESCROLL, 0, lines); }
}

// the bar of a panel whose contents (a taller panel inside it) are moved up and down: the view stays still and the bar sits on its right edge
sealed class NeonPanelScroll : NeonBar
{
    const int Step = 20; readonly Control view, inner;
    public NeonPanelScroll(Control view, Control inner)
    {
        this.view = view; this.inner = inner; inner.Resize += (a, b) => Sync(); view.Resize += (a, b) => Place(); Place(); view.Controls.Add(this);
        MouseWheelHook(view); MouseWheelHook(inner);
    }
    void MouseWheelHook(Control c) { c.MouseWheel += (a, e) => ScrollBy(-Math.Sign(e.Delta) * 3); }
    void Place() { SetBounds(view.ClientSize.Width - BarW - 2, 2, BarW, Math.Max(20, view.ClientSize.Height - 4)); BringToFront(); }
    protected override void Metrics(out int t, out int f, out int v) { t = (inner.Height + Step - 1) / Step; f = (-inner.Top) / Step; v = view.ClientSize.Height / Step; }
    protected override void ScrollBy(int units) { int max = Math.Max(0, inner.Height - view.ClientSize.Height); inner.Top = -Math.Max(0, Math.Min(max, -inner.Top + units * Step)); Sync(); }
}
