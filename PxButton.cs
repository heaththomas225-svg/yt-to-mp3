using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The button of every ordinary window (Settings, Help, the converter...): rounded corners, a soft top-to-bottom shade, a thin lit edge, a glow when the mouse is over it and a press that sinks.
// It keeps everything a flat Button has (BackColor is the colour, FlatAppearance.BorderColor the edge, ForeColor the writing), so the theme code that recolours buttons keeps working.
class PxButton : Button
{
    bool hot, down;
    public PxButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false;
    }
    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    static Color Mix(Color a, Color b, float t) { return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Color behind = Parent != null ? Parent.BackColor : Color.Black; if (behind.A < 255) behind = Color.FromArgb(255, behind); g.Clear(behind); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 6 || Height < 6) return;
        Color baseC = BackColor.A < 255 ? Mix(behind, Color.FromArgb(255, BackColor), BackColor.A / 255f) : BackColor; if (!Enabled) baseC = Mix(baseC, behind, 0.65f);
        if (down) baseC = Mix(baseC, Color.Black, 0.18f); else if (hot) baseC = Mix(baseC, Color.White, 0.14f);
        var rc = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f); float rad = Math.Min(10f, Height / 3f);
        if (hot && Enabled && !down) using (var gp = Round(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), rad + 1)) for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(24 + 12 * (4 - i), Mix(baseC, Color.White, 0.3f)), i * 2f)) g.DrawPath(pen, gp);
        using (var path = Round(rc, rad))
        {
            using (var br = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Mix(baseC, Color.White, 0.07f), Mix(baseC, Color.Black, 0.09f), 90f)) g.FillPath(br, path);
            Color edge = FlatAppearance.BorderColor.IsEmpty || FlatAppearance.BorderColor.A == 0 ? Mix(baseC, Color.White, 0.35f) : Mix(FlatAppearance.BorderColor, baseC, 0.35f); if (hot) edge = Mix(edge, Color.White, 0.3f); if (!Enabled) edge = Mix(edge, behind, 0.6f);
            using (var pen = new Pen(Color.FromArgb(200, edge), 1.2f)) g.DrawPath(pen, path);
            using (var hl = new Pen(Color.FromArgb(46, 255, 255, 255), 1f)) g.DrawLine(hl, rc.X + rad, rc.Y + 1.2f, rc.Right - rad, rc.Y + 1.2f);
        }
        Color ink = !Enabled ? Mix(ForeColor, baseC, 0.55f) : (Contrast.Ratio(Color.White, baseC) >= 3.0 ? Color.White : Contrast.LabelOn(baseC));   // white like every other button, unless the colour is too light for it
        if (ForeColor.GetBrightness() < 0.5f && Contrast.Ratio(ForeColor, baseC) >= 4.5) ink = ForeColor;   // a button that deliberately has dark writing on a light colour
        using (var sf = new StringFormat { Alignment = ContentAlignmentToH(TextAlign), LineAlignment = ContentAlignmentToV(TextAlign), Trimming = StringTrimming.EllipsisCharacter, HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None })
        using (var tb = new SolidBrush(ink))
        {
            var tr = new RectangleF(Padding.Left + 6, 1 + (down ? 1 : 0), Width - Padding.Horizontal - 12, Height - 2); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; g.DrawString(Text, Font, tb, tr, sf);
        }
        if (Focused && ShowFocusCues) using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f) { DashStyle = DashStyle.Dot }) using (var fp = Round(new RectangleF(4, 4, Width - 8, Height - 8), Math.Max(2, rad - 3))) g.DrawPath(pen, fp);
    }
    static StringAlignment ContentAlignmentToH(ContentAlignment a) { switch (a) { case ContentAlignment.TopLeft: case ContentAlignment.MiddleLeft: case ContentAlignment.BottomLeft: return StringAlignment.Near; case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: return StringAlignment.Far; default: return StringAlignment.Center; } }
    static StringAlignment ContentAlignmentToV(ContentAlignment a) { switch (a) { case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: return StringAlignment.Near; case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: return StringAlignment.Far; default: return StringAlignment.Center; } }
}
