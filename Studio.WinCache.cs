using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// =====================================================================================================
//  WINDOW PICTURES: a plugin or tool window the mouse is not on is drawn once into a picture and that picture is put back every frame
//  until something in it changes (its place, size, settings, the theme). While the song plays it is drawn again a few times a second so
//  its display keeps moving. The window under the mouse (and one being dragged) is always drawn live. With several windows open this
//  keeps the studio smooth: each one used to be drawn from scratch every frame.
// =====================================================================================================
partial class StudioForm
{
    sealed class WinPic { public Bitmap Bmp; public Rectangle R; public string Sig = ""; public int At; public List<HitReg> Regs = new List<HitReg>(); }
    readonly Dictionary<object, WinPic> winPics = new Dictionary<object, WinPic>();
    const int WinPicPad = 16;   // room round the window for its shadow and glow
    // draws the window through its picture; maxAgeMs: how old the picture may get (0 = until its signature changes)
    void PaintCachedWin(Graphics g, object key, RectangleF r, string sig, int maxAgeMs, Action<Graphics> paint)
    {
        var ri = Rectangle.Round(r); bool live = dragReg != null || Rectangle.Inflate(ri, 6, 6).Contains(Point.Round(MousePos)) || TxtAudit != null;
        WinPic p; winPics.TryGetValue(key, out p);
        if (live) { if (p != null) { p.Bmp.Dispose(); winPics.Remove(key); } paint(g); return; }
        string full = sig + "|" + UiTokens.Version + "|" + Themes.Index + "|" + ri;
        int now = Environment.TickCount;
        if (p != null && p.Sig == full && (maxAgeMs <= 0 || unchecked(now - p.At) < maxAgeMs)) { g.DrawImageUnscaled(p.Bmp, ri.X - WinPicPad, ri.Y - WinPicPad); regs.AddRange(p.Regs); return; }
        int w = ri.Width + WinPicPad * 2, h = ri.Height + WinPicPad * 2; if (w < 4 || h < 4 || w * h > 2600 * 1600) { paint(g); return; }
        if (p == null || p.Bmp.Width != w || p.Bmp.Height != h) { if (p != null) p.Bmp.Dispose(); p = new WinPic { Bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb) }; winPics[key] = p; }
        int r0 = regs.Count;
        using (var gb = Graphics.FromImage(p.Bmp))
        {
            gb.Clear(Color.Transparent); gb.SmoothingMode = SmoothingMode.AntiAlias; gb.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; Sharp(gb);
            gb.TranslateTransform(WinPicPad - ri.X, WinPicPad - ri.Y); paint(gb);
        }
        p.Regs = regs.GetRange(r0, regs.Count - r0); p.Sig = full; p.R = ri; p.At = now;
        g.DrawImageUnscaled(p.Bmp, ri.X - WinPicPad, ri.Y - WinPicPad);
    }
    // pictures of windows that are closed are let go
    void TrimWinPics(ICollection<object> open)
    {
        List<object> gone = null; foreach (var k in winPics.Keys) if (!open.Contains(k)) (gone ?? (gone = new List<object>())).Add(k);
        if (gone != null) foreach (var k in gone) { winPics[k].Bmp.Dispose(); winPics.Remove(k); }
    }
}

// THE GALAXY FOR OTHER WINDOWS (the Purphex VSTs, GlitchFlow, the bar on top of a VST's own window): the studio's galaxy, made once per
// size step and cut to size, then darkened with the window's own colour so everything on it still reads.
static class GalaxyBg
{
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
    public static void Paint(Graphics g, Rectangle r, int seed, float strength, Color dim, int dimAlpha)
    {
        if (r.Width < 2 || r.Height < 2) return; int bw = (r.Width + 127) / 128 * 128, bh = (r.Height + 127) / 128 * 128; string key = bw + "x" + bh + ":" + seed + ":" + strength.ToString("0.00");
        Bitmap bmp; lock (cache) { if (!cache.TryGetValue(key, out bmp)) { if (cache.Count > 16) { foreach (var b in cache.Values) b.Dispose(); cache.Clear(); } bmp = StudioForm.MakeGalaxy(bw, bh, seed, strength); cache[key] = bmp; } }
        g.DrawImage(bmp, r, new Rectangle(0, 0, r.Width, r.Height), GraphicsUnit.Pixel);
        if (dimAlpha > 0) using (var b = new SolidBrush(Color.FromArgb(dimAlpha, dim))) g.FillRectangle(b, r);
    }
    // a knob's body: a glossy glass sphere with a galaxy inside it, lit from the top left, with a purple rim (the plugin windows' knob)
    public static void Sphere(Graphics g, float cx, float cy, float d, bool hot)
    {
        if (d < 6) return; var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); var oldSm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var pth = new System.Drawing.Drawing2D.GraphicsPath())
        {
            pth.AddEllipse(r); var gs = g.Save(); g.SetClip(pth, System.Drawing.Drawing2D.CombineMode.Intersect);
            Paint(g, new Rectangle((int)r.X, (int)r.Y, (int)Math.Ceiling(d) + 1, (int)Math.Ceiling(d) + 1), 0x5F3E, 1.3f, Color.FromArgb(12, 6, 28), 70);   // the galaxy inside
            g.Restore(gs);
            using (var pb = new System.Drawing.Drawing2D.PathGradientBrush(pth) { CenterPoint = new PointF(cx - d * 0.18f, cy - d * 0.22f), CenterColor = Color.FromArgb(90, 150, 110, 230), SurroundColors = new[] { Color.FromArgb(170, 8, 4, 20) } }) g.FillPath(pb, pth);   // the glass: lit top left, dark at the edge
        }
        using (var pen = new Pen(Color.FromArgb(hot ? 255 : 200, 176, 92, 255), Math.Max(1.2f, d / 30f))) g.DrawEllipse(pen, r);
        using (var hl = new System.Drawing.Drawing2D.GraphicsPath()) { hl.AddEllipse(cx - d * 0.3f, cy - d * 0.42f, d * 0.45f, d * 0.26f); using (var hb = new System.Drawing.Drawing2D.PathGradientBrush(hl) { CenterColor = Color.FromArgb(150, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) } }) g.FillPath(hb, hl); }   // the shine
        g.SmoothingMode = oldSm;
    }
    // a button's face: a piece of galaxy in a rounded box, tinted with the button's colour
    public static void Face(Graphics g, RectangleF r, float rad, Color tint, int tintAlpha)
    {
        if (r.Width < 4 || r.Height < 4) return; float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
        using (var p = new System.Drawing.Drawing2D.GraphicsPath())
        {
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            var gs = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect);
            Paint(g, new Rectangle((int)r.X, (int)r.Y, (int)Math.Ceiling(r.Width) + 1, (int)Math.Ceiling(r.Height) + 1), 0xB077, 1.1f, Color.FromArgb(255, tint), tintAlpha);
            g.Restore(gs);
        }
    }
}
