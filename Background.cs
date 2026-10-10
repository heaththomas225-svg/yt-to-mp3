using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Main window look: black-purple background with neon leaves, glowing orbs, aurora ribbons,
//  sparks and glowing cards. Built for speed:
//    - each leaf is pre-rendered once as a sprite (drawing one is a cheap image blit)
//    - the soft layers (atmosphere, cards, captions) are painted on a background thread and
//      the UI thread only blits finished images, so every frame costs about the same
//    - the number of leaves scales with the window; quality steps down automatically if needed
// ======================================================================
partial class App
{
    class Lab { public string T; public float X, Y; }

    Stopwatch frameWatch = new Stopwatch(); double frameMs = 6; int perfTick, quality;
    SizeF titleSize;
    volatile Bitmap atmoFront, overlayFront; volatile int reqW, reqH; volatile bool layerRun = true; Thread layerThread;
    readonly List<Bitmap> graveyard = new List<Bitmap>(); Lab[] labSnap = new Lab[0]; volatile string subtitle = "";

    static readonly Color[] LeafPal = {
        Color.FromArgb(170, 90, 255), Color.FromArgb(140, 60, 235), Color.FromArgb(205, 120, 255), Color.FromArgb(120, 50, 215),
        Color.FromArgb(235, 90, 225), Color.FromArgb(110, 80, 255) };
    static readonly int[] AlphaLv = { 55, 85, 115, 145 };
    static Bitmap[,] sprites;   // [palette, alpha level]
    const int SprW = 250, SprH = 230, SprOX = 125, SprOY = 170;

    static void BuildSprites()
    {
        if (sprites != null) return;
        var s = new Bitmap[LeafPal.Length, AlphaLv.Length];
        for (int c = 0; c < LeafPal.Length; c++)
            for (int a = 0; a < AlphaLv.Length; a++)
            {
                var bmp = new Bitmap(SprW, SprH, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.TranslateTransform(SprOX, SprOY);
                    using (var fill = new SolidBrush(Color.FromArgb(AlphaLv[a], LeafPal[c])))
                    using (var edge = new Pen(Color.FromArgb(Math.Min(255, AlphaLv[a] + 60), 225, 190, 255), 1.5f))
                    using (var rib = new Pen(Color.FromArgb(AlphaLv[a], 235, 215, 255), 1f))
                    using (var glow = new Pen(Color.FromArgb(32, LeafPal[c]), 9f))
                    {
                        for (int i = 0; i < 7; i++)
                        {
                            var st = g.Save(); g.RotateTransform(LeafAng[i]);
                            g.DrawPath(glow, LeafPaths[i]); g.FillPath(fill, LeafPaths[i]); g.DrawPath(edge, LeafPaths[i]); g.DrawLine(rib, 0, 0, 0, -LeafLen[i] * 0.9f);
                            g.Restore(st);
                        }
                        g.DrawLine(edge, 0, 0, 0, 45);
                    }
                }
                s[c, a] = bmp;
            }
        sprites = s;
    }

    Leaf NewLeaf(bool anywhere)
    {
        var l = new Leaf(); int w = Math.Max(bg.Width, 640), h = Math.Max(bg.Height, 690);
        l.S = 0.3f + (float)rnd.NextDouble() * 0.85f;
        l.X = (float)rnd.NextDouble() * w;
        l.Y = anywhere ? (float)rnd.NextDouble() * h : -130 * l.S - 20;
        l.Rot = (float)rnd.NextDouble() * 360; l.Spin = ((float)rnd.NextDouble() - 0.5f) * 1.3f;
        l.VY = (0.35f + l.S * 1.0f) * (float)Math.Sqrt(h / 690.0);
        l.Phase = (float)rnd.NextDouble() * 6.28f; l.Sway = 0.3f + (float)rnd.NextDouble() * 0.7f;
        l.CI = rnd.Next(LeafPal.Length); l.C = LeafPal[l.CI];
        l.A = Math.Min(150, 40 + (int)(l.S * 85));
        return l;
    }

    Spore NewSpore(bool anywhere)
    {
        var s = new Spore(); int w = Math.Max(bg.Width, 640), h = Math.Max(bg.Height, 690);
        s.X = (float)rnd.NextDouble() * w; s.Y = anywhere ? (float)rnd.NextDouble() * h : h + 5;
        s.R = 1f + (float)rnd.NextDouble() * 2.8f; s.VY = (0.25f + (float)rnd.NextDouble() * 0.9f) * (float)Math.Sqrt(h / 690.0);
        s.Phase = (float)rnd.NextDouble() * 6.28f;
        return s;
    }

    // leaf / spark count follows the window area, so a maximised window is filled straight away
    void Repopulate()
    {
        BuildSprites();
        int w = Math.Max(bg.Width, 640), h = Math.Max(bg.Height, 690);
        int qcap = quality == 0 ? 44 : (quality == 1 ? 26 : 14); int cap = Math.Min(qcap, Math.Max(14, w * h / 38000));
        while (leaves.Count < cap) leaves.Add(NewLeaf(true));
        while (leaves.Count > cap) leaves.RemoveAt(leaves.Count - 1);
        int sc = Math.Max(30, Math.Min(110, w * h / 14000)); if (quality == 2) sc /= 2;
        while (spores.Count < sc) spores.Add(NewSpore(true));
        while (spores.Count > sc) spores.RemoveAt(spores.Count - 1);
        SnapLabels();
    }

    void SnapLabels()
    {
        var l = new List<Lab>(); foreach (var kv in lbls) l.Add(new Lab { T = kv.Value, X = kv.Key.Left, Y = kv.Key.Top - 18 }); labSnap = l.ToArray();
        subtitle = "Paste BandLab (track, post, album) or YouTube links. Pick format, sample rate, trim, batch.";
    }

    void StartLayerWorker()
    {
        layerThread = new Thread(LayerLoop) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; layerThread.Start();
    }

    // Background thread: paints the soft, slowly moving layers into fresh bitmaps and publishes them.
    void LayerLoop()
    {
        var sw = Stopwatch.StartNew();
        while (layerRun)
        {
            int W = reqW, H = reqH;
            if (W < 16 || H < 16) { Thread.Sleep(30); continue; }
            double t0 = sw.Elapsed.TotalMilliseconds;
            try
            {
                var a = BuildAtmosphere(W, H); var o = BuildOverlay(W, H);
                var oldA = atmoFront; var oldO = overlayFront; atmoFront = a; overlayFront = o;
                lock (graveyard) { if (oldA != null) graveyard.Add(oldA); if (oldO != null) graveyard.Add(oldO); while (graveyard.Count > 6) { var d = graveyard[0]; graveyard.RemoveAt(0); try { d.Dispose(); } catch { } } }
            }
            catch { }
            double spent = sw.Elapsed.TotalMilliseconds - t0;
            Thread.Sleep((int)Math.Max(12, (quality == 0 ? 70 : 140) - spent));
        }
    }

    Bitmap BuildAtmosphere(int W, int H)
    {
        int lw = Math.Max(16, W / 3), lh = Math.Max(16, H / 3);
        using (var low = new Bitmap(lw, lh, PixelFormat.Format32bppPArgb))
        {
            using (var g = Graphics.FromImage(low)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(lw / (float)W, lh / (float)H); DrawAtmosphere(g, W, H, t); }
            var full = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(full)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(low, 0, 0, W, H); }
            return full;
        }
    }

    Bitmap BuildOverlay(int W, int H)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var cards = new[] { new RectangleF(8, 64, W - 16, 106), new RectangleF(8, 172, W - 16, 134), new RectangleF(8, 308, W - 16, 38),
                                new RectangleF(8, 350, W - 16, 96), new RectangleF(8, 450, W - 16, 38), new RectangleF(8, 490, W - 16, Math.Max(80, H - 498)) };
            float tt = t; for (int i = 0; i < cards.Length; i++) Card(g, cards[i], i, tt);
            using (var f = new Font("Segoe UI", 9f)) using (var b = new SolidBrush(Color.FromArgb(200, 205, 175, 255)))
            {
                g.DrawString(subtitle, f, b, 18, 54);
                var ls = labSnap; foreach (var l in ls) g.DrawString(l.T, f, b, l.X, l.Y);
            }
        }
        return bmp;
    }

    void StyleTips()
    {
        tip.OwnerDraw = true; tip.InitialDelay = 350;
        var f = new Font("Segoe UI", 9.5f);
        tip.Popup += (s, e) => { var sz = TextRenderer.MeasureText(tip.GetToolTip(e.AssociatedControl), f); e.ToolTipSize = new Size(Math.Min(360, sz.Width) + 16, sz.Height + 10); };
        tip.Draw += (s, e) =>
        {
            var g = e.Graphics; var r = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(30, 14, 56), Color.FromArgb(10, 5, 22), 90f)) g.FillRectangle(b, r);
            using (var pen = new Pen(Color.FromArgb(190, 120, 255))) g.DrawRectangle(pen, r);
            TextRenderer.DrawText(g, e.ToolTipText, f, new Rectangle(8, 4, r.Width - 12, r.Height - 6), Color.FromArgb(240, 232, 255), TextFormatFlags.Left | TextFormatFlags.WordBreak);
        };
    }

    void Card(Graphics g, RectangleF r, int idx, float tt)
    {
        float pulse = (float)(Math.Sin(tt * 1.7f + idx * 0.9f) * 0.5 + 0.5);
        using (var p = RoundRect(r, 14))
        {
            using (var br = new LinearGradientBrush(r, Color.FromArgb(178, 4, 2, 10), Color.FromArgb(178, 20, 8, 42), 90f)) g.FillPath(br, p);
            using (var pen = new Pen(Color.FromArgb((int)(16 + 26 * pulse), 190, 90, 255), 7f)) g.DrawPath(pen, p);
            using (var pen = new Pen(Color.FromArgb((int)(95 + 90 * pulse), 170, 100, 255), 1.4f)) g.DrawPath(pen, p);
        }
    }

    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    void DrawAtmosphere(Graphics g, float W, float H, float tt)
    {
        using (var br = new LinearGradientBrush(new RectangleF(0, 0, W, H), Color.FromArgb(3, 1, 8), Color.FromArgb(26, 6, 54), 72f)) g.FillRectangle(br, 0, 0, W, H);
        Color[] oc = { Color.FromArgb(150, 60, 245), Color.FromArgb(236, 70, 214), Color.FromArgb(70, 70, 240) };
        for (int i = 0; i < 3; i++)
        {
            float cx = W * (0.18f + 0.32f * i) + (float)Math.Sin(tt * 0.33f + i * 2.1f) * W * 0.11f;
            float cy = H * (0.28f + 0.26f * i) + (float)Math.Cos(tt * 0.27f + i) * H * 0.10f;
            float rad = Math.Max(W, H) * 0.34f + (float)Math.Sin(tt * 0.8f + i) * 36;
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
                using (var pg = new PathGradientBrush(gp)) { pg.CenterColor = Color.FromArgb(92, oc[i]); pg.SurroundColors = new[] { Color.FromArgb(0, oc[i]) }; g.FillPath(pg, gp); }
            }
        }
        for (int k = 0; k < 2; k++)
        {
            var poly = new List<PointF>(); var bot = new List<PointF>(); float baseY = H * (0.74f + 0.07f * k);
            for (float x = 0; x <= W + 24; x += 24)
            {
                float y = baseY + (float)(Math.Sin(x * 0.0055 + tt * 0.7 + k * 1.7) * H * 0.045 + Math.Sin(x * 0.013 - tt * 0.5 + k) * H * 0.018);
                poly.Add(new PointF(x, y)); bot.Add(new PointF(x, y + H * 0.2f));
            }
            for (int i = bot.Count - 1; i >= 0; i--) poly.Add(bot[i]);
            var rect = new RectangleF(0, baseY - H * 0.07f, W, H * 0.3f);
            using (var br = new LinearGradientBrush(rect, Color.FromArgb(60, k == 0 ? Color.FromArgb(170, 80, 255) : Color.FromArgb(240, 80, 220)), Color.FromArgb(0, 120, 60, 220), 90f)) g.FillPolygon(br, poly.ToArray());
        }
    }

    // Per-frame work on the UI thread: blit finished layers, draw sprites, title and progress bar.
    void PaintBg(object sender, PaintEventArgs e)
    {
        frameWatch.Restart();
        var g = e.Graphics; var r = bg.ClientRectangle; if (r.Width < 10 || r.Height < 10) return;
        reqW = r.Width; reqH = r.Height; float W = r.Width, H = r.Height; bool anim = animate.Checked;
        var atmo = atmoFront; var ov = overlayFront;

        g.CompositingQuality = CompositingQuality.HighSpeed; g.PixelOffsetMode = PixelOffsetMode.HighSpeed; g.SmoothingMode = SmoothingMode.HighSpeed;
        if (atmo != null && atmo.Width == r.Width && atmo.Height == r.Height)
        {
            g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(atmo, 0, 0); g.CompositingMode = CompositingMode.SourceOver;
        }
        else using (var br = new LinearGradientBrush(r, Color.FromArgb(3, 1, 8), Color.FromArgb(26, 6, 54), 72f)) g.FillRectangle(br, r);

        if (anim && sprites != null)
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            leaves.Sort((a, b) => a.S.CompareTo(b.S));
            for (int i = 0; i < leaves.Count; i++)
            {
                var l = leaves[i]; if (l.Y < -180 || l.Y > H + 180 || l.X < -180 || l.X > W + 180) continue;
                int lv = l.A < 70 ? 0 : (l.A < 100 ? 1 : (l.A < 125 ? 2 : 3)); var sp = sprites[l.CI, lv];
                var st = g.Save(); g.TranslateTransform(l.X, l.Y); g.RotateTransform(l.Rot); g.ScaleTransform(l.S, l.S); g.DrawImage(sp, -SprOX, -SprOY, SprW, SprH); g.Restore(st);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < spores.Count; i++)
            {
                var s = spores[i]; float tw = (float)(Math.Sin(t * 2.2f + s.Phase) * 0.5 + 0.5); int a = (int)(55 + 150 * tw);
                if (quality == 0) using (var b = new SolidBrush(Color.FromArgb(a / 4, 230, 150, 255))) g.FillEllipse(b, s.X - s.R * 2, s.Y - s.R * 2, s.R * 5, s.R * 5);
                using (var b = new SolidBrush(Color.FromArgb(a, 240, 200, 255))) g.FillEllipse(b, s.X, s.Y, s.R * 2, s.R * 2);
            }
            float lx = (t * 220) % (W + 300) - 150;
            using (var br = new LinearGradientBrush(new RectangleF(lx - 150, 0, 300, 3), Color.FromArgb(0, 236, 92, 226), Color.FromArgb(0, 150, 80, 255), 0f))
            { var cb = new ColorBlend(3); cb.Colors = new[] { Color.FromArgb(0, 236, 92, 226), Color.FromArgb(230, 236, 120, 240), Color.FromArgb(0, 150, 80, 255) }; cb.Positions = new[] { 0f, 0.5f, 1f }; br.InterpolationColors = cb; g.FillRectangle(br, lx - 150, 0, 300, 3); }
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (ov != null && ov.Width == r.Width && ov.Height == r.Height) g.DrawImageUnscaled(ov, 0, 0);

        string title = "BandLab + YouTube → MP3";
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var f = new Font("Segoe UI Black", 24f))
        {
            if (titleSize.Width < 1) titleSize = g.MeasureString(title, f);
            float glow = anim ? (float)(Math.Sin(t * 2) * 0.5 + 0.5) : 0.4f;
            using (var b = new SolidBrush(Color.FromArgb((int)(20 + 26 * glow), 200, 90, 255))) { g.DrawString(title, f, b, 12, 6); g.DrawString(title, f, b, 20, 14); }
            float sh = anim ? (t * 90) % (titleSize.Width * 2) : 0;
            using (var b = new LinearGradientBrush(new RectangleF(16 - sh, 10, titleSize.Width * 2, titleSize.Height), Color.FromArgb(245, 225, 255), Color.FromArgb(180, 100, 255), 0f))
            { b.WrapMode = WrapMode.TileFlipX; g.DrawString(title, f, b, 16, 10); }
        }

        var bar = new Rectangle(16, 454, r.Width - 32, 16);
        using (var tb = new SolidBrush(Color.FromArgb(200, 4, 2, 10))) g.FillRectangle(tb, bar);
        int fw = (int)(bar.Width * Math.Max(0, Math.Min(100, shownPct)) / 100f);
        if (fw > 2)
        {
            var fr = new Rectangle(bar.X, bar.Y, fw, bar.Height);
            using (var gl = new SolidBrush(Color.FromArgb(40, 230, 100, 240))) g.FillRectangle(gl, fr.X, fr.Y - 4, fr.Width, fr.Height + 8);
            using (var fb = new LinearGradientBrush(fr, Color.FromArgb(150, 60, 240), Color.FromArgb(240, 90, 230), 0f)) g.FillRectangle(fb, fr);
            float sx = bar.X + ((t * 130) % (fw + 70)) - 35; var old = g.Clip; g.SetClip(fr);
            using (var sb = new SolidBrush(Color.FromArgb(80, 255, 255, 255))) g.FillRectangle(sb, sx, bar.Y, 24, bar.Height); g.Clip = old;
        }
        using (var pn = new Pen(Color.FromArgb(170, 190, 120, 255))) g.DrawRectangle(pn, bar);
        if (status.Length > 0)
            using (var f = new Font("Segoe UI", 8.5f)) using (var b = new SolidBrush(Color.FromArgb(225, 230, 210, 255))) g.DrawString(status, f, b, 16, 471);

        // adaptive quality: step effects down on slower PCs, back up when there is headroom
        frameMs = frameMs * 0.9 + frameWatch.Elapsed.TotalMilliseconds * 0.1;
        if (++perfTick % 40 == 0)
        {
            double budget = 1000.0 / Math.Max(30, frameLoop.TargetFps);
            if (frameMs > budget * 0.92 && quality < 2) { quality++; Repopulate(); }
            else if (frameMs < budget * 0.40 && quality > 0) { quality--; Repopulate(); }
        }
    }
}
