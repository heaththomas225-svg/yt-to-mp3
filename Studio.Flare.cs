using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// =====================================================================================================
//  GALAXY FLARE: every button sits in a soft glow of the theme's colour, with a beam of light streaking out of both ends and a little
//  star glint on its corner (brighter under the mouse and on lit buttons). Each look is drawn once per size and colour into a picture
//  and reused. It is a cached picture, so it costs almost nothing.
// =====================================================================================================
partial class StudioForm
{
    static readonly Dictionary<long, Bitmap> haloCache = new Dictionary<long, Bitmap>();
    const int HaloPadX = 46, HaloPadY = 16;
    void DrawBtnHalo(Graphics g, RectangleF r, int style, float ht)
    {
        if (r.Width < 18 || r.Height < 14 || r.Width > 700) return;   // (a cached picture: cheap even in PERFORMANCE MODE)
        int lvl = style == 4 || style == 2 ? 2 : (ht > 0.4f ? 2 : (ht > 0.05f ? 1 : 0)); Color c = style == 3 ? TM.C(255, 230, 60, 110) : (style == 4 ? (UiTokens.Custom ? UiTokens.Accent : Accent) : NeonViolet);
        int w = (int)r.Width, h = (int)r.Height; long key = ((long)w << 40) ^ ((long)h << 28) ^ ((long)lvl << 24) ^ (uint)c.ToArgb();
        Bitmap b; lock (haloCache) { if (!haloCache.TryGetValue(key, out b)) { if (haloCache.Count > 300) { foreach (var x in haloCache.Values) x.Dispose(); haloCache.Clear(); } b = MakeHalo(w, h, lvl, c); haloCache[key] = b; } }
        g.DrawImageUnscaled(b, (int)r.X - HaloPadX, (int)r.Y - HaloPadY);
    }
    static Bitmap MakeHalo(int w, int h, int lvl, Color c)
    {
        int W = w + HaloPadX * 2, H = h + HaloPadY * 2; var bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        float k = lvl == 2 ? 1f : (lvl == 1 ? 0.7f : 0.45f);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; float cx = W / 2f, cy = H / 2f;
            // the glow round the button
            using (var pth = new GraphicsPath()) { pth.AddEllipse(HaloPadX * 0.45f, HaloPadY * 0.15f, W - HaloPadX * 0.9f, H - HaloPadY * 0.3f); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb((int)(95 * k), c), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, pth); }
            // the beam: a thin bright line through the middle that runs out past both ends and fades
            using (var lb = new LinearGradientBrush(new RectangleF(0, cy - 1, W, 2), Color.Transparent, Color.Transparent, 0f))
            {
                lb.InterpolationColors = new ColorBlend(5) { Colors = new[] { Color.FromArgb(0, c), Color.FromArgb((int)(170 * k), Light(c)), Color.FromArgb((int)(60 * k), c), Color.FromArgb((int)(170 * k), Light(c)), Color.FromArgb(0, c) }, Positions = new[] { 0f, 0.2f, 0.5f, 0.8f, 1f } };
                g.FillRectangle(lb, 0, cy - 0.7f, W, 1.4f);
                using (var lb2 = new LinearGradientBrush(new RectangleF(0, cy - 4, W, 8), Color.Transparent, Color.Transparent, 0f)) { lb2.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0, c), Color.FromArgb((int)(40 * k), c), Color.FromArgb(0, c) }, Positions = new[] { 0f, 0.5f, 1f } }; g.FillRectangle(lb2, 0, cy - 4, W, 8); }
            }
            // a star glint on the top right corner
            float sx = HaloPadX + w - 4, sy = HaloPadY + 2, sr = 7 + 4 * k;
            using (var pth = new GraphicsPath()) { pth.AddEllipse(sx - sr, sy - sr, sr * 2, sr * 2); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb((int)(200 * k), 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, pth); }
            using (var pen = new Pen(Color.FromArgb((int)(200 * k), 255, 255, 255), 1f)) { g.DrawLine(pen, sx - sr * 1.6f, sy, sx + sr * 1.6f, sy); g.DrawLine(pen, sx, sy - sr * 1.2f, sx, sy + sr * 1.2f); }
        }
        return bmp;
    }
    // GALAXY BUTTONS: a button's face is a little piece of galaxy (in the theme's colours), tinted with the button's own colour
    static Bitmap btnGalaxy; static long btnGalaxyKey;
    Bitmap ButtonGalaxy()
    {
        long key = ((long)NeonViolet.ToArgb() << 32) ^ (uint)Accent.ToArgb() ^ ((long)NeonCyan.ToArgb() << 7);
        if (btnGalaxy == null || btnGalaxyKey != key) { if (btnGalaxy != null) btnGalaxy.Dispose(); btnGalaxy = MakeGalaxy(420, 160, 0xB77, 1.35f, new[] { NeonViolet, Accent, NeonCyan, Light(NeonViolet), Accent }); btnGalaxyKey = key; }
        return btnGalaxy;
    }
    void GalaxyFace(Graphics g, RectangleF r, float rad, Color tint, int style, string label)
    {
        if (r.Width < 8 || r.Height < 8) return; var tex = ButtonGalaxy(); int off = Math.Abs((label ?? "").GetHashCode());
        using (var tb = new TextureBrush(tex, WrapMode.TileFlipXY)) { tb.TranslateTransform(r.X - off % tex.Width, r.Y - (off / 7) % tex.Height); using (var p = RR(r, rad)) g.FillPath(tb, p); }
        using (var p = RR(r, rad)) using (var br = new LinearGradientBrush(r, Color.FromArgb(style == 2 ? 105 : 115, tint), Color.FromArgb(style == 2 ? 55 : 75, tint), 90f)) g.FillPath(br, p);   // the button's own colour over its galaxy
    }
    // GALAXY WRITING: words filled with a bright piece of galaxy, with a dark edge so they always read
    static Bitmap textGalaxy;
    void GalaxyText(Graphics g, string text, Font f, float x, float y)
    {
        if (textGalaxy == null)
        {   // a bright, pale galaxy: the nebula and stars laid over white, so the words glow and still read
            var gal = MakeGalaxy(300, 90, 0x7E7, 2.0f, new[] { Color.FromArgb(255, 220, 60, 255), Color.FromArgb(255, 140, 70, 255), Color.FromArgb(255, 80, 170, 255), Color.FromArgb(255, 255, 90, 210), Color.FromArgb(255, 170, 90, 255) });
            textGalaxy = new Bitmap(300, 90); using (var tg = Graphics.FromImage(textGalaxy)) { tg.Clear(Color.FromArgb(255, 250, 240, 255)); var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.62f }; using (var ia = new System.Drawing.Imaging.ImageAttributes()) { ia.SetColorMatrix(cm); tg.DrawImage(gal, new Rectangle(0, 0, 300, 90), 0, 0, 300, 90, GraphicsUnit.Pixel, ia); } } gal.Dispose();
        }
        using (var pth = new GraphicsPath())
        {
            pth.AddString(text, f.FontFamily, (int)f.Style, f.Size, new PointF(x, y), StringFormat.GenericTypographic);
            using (var pen = new Pen(Color.FromArgb(235, 8, 2, 20), 3.2f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, pth);   // the dark edge
            using (var white = new SolidBrush(Color.FromArgb(255, 245, 235, 255))) g.FillPath(white, pth);
            using (var tb = new TextureBrush(textGalaxy, WrapMode.TileFlipXY)) { tb.TranslateTransform(x, y); var gs = g.Save(); using (var tex = new SolidBrush(Color.FromArgb(0, 0, 0, 0))) { } g.FillPath(tb, pth); g.Restore(gs); }
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 0.7f)) g.DrawPath(pen, pth);   // a fine light rim
        }
    }
    static Color Light(Color c) { return Color.FromArgb(255, c.R + (255 - c.R) / 2, c.G + (255 - c.G) / 2, c.B + (255 - c.B) / 2); }

    // the big flare of a plugin window: a long beam of light from the planet out across the whole window, behind the knobs
    void PaintWindowFlare(Graphics g, RectangleF a, float cy)
    {
        Color c = Color.FromArgb(255, 190, 120, 255);
        using (var lb = new LinearGradientBrush(new RectangleF(a.X, cy - 2, a.Width, 4), Color.Transparent, Color.Transparent, 0f))
        { lb.InterpolationColors = new ColorBlend(5) { Colors = new[] { Color.FromArgb(0, c), Color.FromArgb(200, c), Color.FromArgb(255, 255, 250, 255), Color.FromArgb(200, c), Color.FromArgb(0, c) }, Positions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f } }; g.FillRectangle(lb, a.X, cy - 1.6f, a.Width, 3.2f); }
        using (var lb = new LinearGradientBrush(new RectangleF(a.X, cy - 14, a.Width, 28), Color.Transparent, Color.Transparent, 0f))
        { lb.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0, c), Color.FromArgb(110, c), Color.FromArgb(0, c) }, Positions = new[] { 0f, 0.5f, 1f } }; using (var pth = new GraphicsPath()) { pth.AddEllipse(a.X, cy - 18, a.Width, 36); g.FillPath(lb, pth); } }
    }
}
