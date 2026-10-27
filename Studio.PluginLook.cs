using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

// =====================================================================================================
//  THE PLUGIN WINDOW LOOK: a floating plugin window is a Purphex unit. A galaxy header with the PURPHEX logo and the plugin's name under
//  it, a galaxy behind the controls (nebula light and stars), and every number setting as a knob you turn: drag up or down (Shift for fine
//  steps), double-click for the default, or type the value in the box under it. Choice settings are pill buttons. The colours are the Purphex
//  brand colours whatever the theme (like every plugin and preset window).
// =====================================================================================================
partial class StudioForm
{
    static readonly Color[] KnobCols = { Color.FromArgb(255, 61, 217, 255), Color.FromArgb(255, 255, 92, 200), Color.FromArgb(255, 176, 92, 255), Color.FromArgb(255, 90, 140, 255), Color.FromArgb(255, 122, 240, 194) };
    const float KnobCellW = 96, KnobCellH = 118, ChoiceRowH = 36, VizBoxH = 150;
    float knobDragFrac, knobDragY;

    // how tall the knob grid of a plugin is in a window this wide
    internal static float KnobGridHeight(Fx f, float width, int extraSkip = 0)
    {
        var hid = f.Hidden.Length > 0 ? new HashSet<string>(f.Hidden.Split(',')) : new HashSet<string>(); int nums = 0, chs = 0;
        foreach (var p in f.P) { if (hid.Contains(p.Id)) continue; if (p.IsChoice) chs++; else nums++; }
        int cols = Math.Max(1, (int)((width - 12) / KnobCellW)); float h;
        if (cols >= 4) { int rest = Math.Max(0, nums - 4); h = 2 * KnobCellH + ((rest + cols - 1) / cols) * KnobCellH; }   // the display in the middle of the first two rows, a knob each side
        else h = VizBoxH + 10 + ((nums + cols - 1) / cols) * KnobCellH;   // narrow: the display across the top
        return h + chs * ChoiceRowH + 16;
    }

    // a knob: drag up / down to turn it (Shift = fine), double-click = default
    HitReg KnobCtl(Graphics g, float cx, float cy, float d, float frac, Action<float> set, Color col, Action toDefault)
    {
        frac = Math.Max(0, Math.Min(1, frac)); var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); bool hot = Hot(Rc(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8));
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        // the glow and the ring it turns along
        using (var gl = new SolidBrush(Color.FromArgb(hot ? 50 : 26, col))) g.FillEllipse(gl, r.X - 6, r.Y - 6, r.Width + 12, r.Height + 12);
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 3f)) g.DrawArc(pen, r.X - 3, r.Y - 3, d + 6, d + 6, 135, 270);
        using (var pen = new Pen(col, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, r.X - 3, r.Y - 3, d + 6, d + 6, 135, Math.Max(1f, 270f * frac));
        // the body: a glossy glass sphere lit from the top left, with a purple rim
        using (var pth = new GraphicsPath()) { pth.AddEllipse(r); using (var pb = new PathGradientBrush(pth) { CenterPoint = new PointF(cx - d * 0.18f, cy - d * 0.22f), CenterColor = Color.FromArgb(255, 120, 80, 200), SurroundColors = new[] { Color.FromArgb(255, 12, 6, 28) } }) g.FillPath(pb, pth); }
        using (var pen = new Pen(Color.FromArgb(hot ? 255 : 200, 176, 92, 255), 1.6f)) g.DrawEllipse(pen, r);
        using (var hl = new GraphicsPath()) { var hr = new RectangleF(cx - d * 0.3f, cy - d * 0.42f, d * 0.45f, d * 0.26f); hl.AddEllipse(hr); using (var hb = new PathGradientBrush(hl) { CenterColor = Color.FromArgb(150, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) } }) g.FillPath(hb, hl); }   // the shine
        // the pointer
        double a = (135 + 270 * frac) * Math.PI / 180; float px = cx + (float)Math.Cos(a) * d * 0.36f, py = cy + (float)Math.Sin(a) * d * 0.36f;
        using (var pen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, cx + (float)Math.Cos(a) * d * 0.12f, cy + (float)Math.Sin(a) * d * 0.12f, px, py);
        using (var dot = new SolidBrush(col)) g.FillEllipse(dot, px - 2.2f, py - 2.2f, 4.4f, 4.4f);
        g.SmoothingMode = oldSm;
        var reg = Reg(Rc(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), () => { knobDragFrac = frac; knobDragY = MousePos.Y; });
        reg.OnDrag = pt => { float k = (ModifierKeys & System.Windows.Forms.Keys.Shift) != 0 ? 800f : 200f; set(Math.Max(0, Math.Min(1, knobDragFrac + (knobDragY - pt.Y) / k))); };
        reg.OnDouble = toDefault; reg.OnWheel = dd => set(Math.Max(0, Math.Min(1, frac + Math.Sign(dd) * 0.02f)));
        return reg;
    }

    // the settings of a plugin as knobs (in a floating window)
    void PaintParamKnobs(Graphics g, Fx f, RectangleF a, List<string> skip)
    {
        var nums = new List<Param>(); var chs = new List<Param>(); foreach (var p in f.P) { if (skip.Contains(p.Id)) continue; if (p.IsChoice) chs.Add(p); else nums.Add(p); }
        int cols = Math.Max(1, (int)((a.Width - 12) / KnobCellW)); float cw = (a.Width - 12) / cols, y0 = a.Y + 6;
        // where each knob goes: with room, the effect display sits in the middle of the first two rows (a knob each side of it), the rest follow below
        var slots = new List<PointF>(); bool mid = cols >= 4; RectangleF vizBox;
        if (mid) { vizBox = Rc(a.X + 6 + cw + 6, y0, cw * (cols - 2) - 12, 2 * KnobCellH - 6); slots.Add(new PointF(0, 0)); slots.Add(new PointF(cols - 1, 0)); slots.Add(new PointF(0, 1)); slots.Add(new PointF(cols - 1, 1)); for (int k = 4; k < nums.Count; k++) slots.Add(new PointF((k - 4) % cols, 2 + (k - 4) / cols)); }
        else { vizBox = Rc(a.X + 6, y0, a.Width - 12, VizBoxH); y0 += VizBoxH + 10; for (int k = 0; k < nums.Count; k++) slots.Add(new PointF(k % cols, k / cols)); }
        PaintWindowFlare(g, a, vizBox.Y + vizBox.Height / 2 - 6);   // the planet's light runs right across the window, behind the knobs
        PaintVizBox(g, vizBox, f);
        int lastRow = 0;
        for (int i = 0; i < nums.Count; i++)
        {
            var p = nums[i]; var pp = p; int cx0 = (int)slots[i].X, ry = (int)slots[i].Y; lastRow = Math.Max(lastRow, ry); var cell = Rc(a.X + 6 + cx0 * cw, y0 + ry * KnobCellH, cw, KnobCellH - 6);
            string nm = Win.Up(p.Name); Txt(g, nm, 9, Color.FromArgb(255, 220, 210, 245), Rc(cell.X + 4, cell.Y + 3, cell.Width - 8, 14), StringAlignment.Center, true);
            Color col = KnobCols[i % KnobCols.Length];
            KnobCtl(g, cell.X + cell.Width / 2, cell.Y + 50, 46, PFrac(p), fr => f.Set(pp.Id, PFromFrac(pp, fr)), col, () => f.Set(pp.Id, pp.Def));
            Hint2(Rc(cell.X, cell.Y, cell.Width, 80), nm + ": drag up or down to turn it (Shift for fine steps), double-click for the default, or type a value below");
            ValueBox(g, Rc(cell.X + 8, cell.Y + 84, cell.Width - 16 - (paramTrack != null ? 24 : 0), 24), p.Text, Num(p.Val), s => { float v; if (ParseNum(s, out v)) f.Set(pp.Id, v); });
            if (paramTrack != null) { var lane0 = FindLane(paramTrack, f, p.Id); var atr = paramTrack; Btn(g, Rc(cell.Right - 30, cell.Y + 84, 22, 24), "A", lane0 != null && lane0.On ? 4 : 0, () => OpenAuto(atr, f, pp), 10); }
        }
        float y = y0 + (mid ? Math.Max(2, lastRow + 1) : (nums.Count + cols - 1) / cols) * KnobCellH;
        foreach (var p in chs)
        {
            var pp = p; var row = Rc(a.X + 6, y, a.Width - 12, ChoiceRowH - 6); y += ChoiceRowH;
            Txt(g, Win.Up(p.Name), 11, Color.FromArgb(255, 220, 210, 245), Rc(row.X + 6, row.Y, row.Width * 0.38f, row.Height), StringAlignment.Near, true);
            float aw = paramTrack != null ? 26 : 0; var br = Rc(row.X + row.Width * 0.40f, row.Y + 2, row.Width * 0.60f - 6 - aw, row.Height - 4);
            Btn(g, br, p.Text, 4, () => { pp.Val = (pp.Val + 1) > pp.Max ? pp.Min : pp.Val + 1; f.MarkDirty(); }, 12); var rg = regs[regs.Count - 1]; rg.OnRight = () => { pp.Val = (pp.Val - 1) < pp.Min ? pp.Max : pp.Val - 1; f.MarkDirty(); };
            if (paramTrack != null) { var lane0 = FindLane(paramTrack, f, p.Id); var atr = paramTrack; Btn(g, Rc(row.Right - 22, row.Y + 3, 22, row.Height - 6), "A", lane0 != null && lane0.On ? 4 : 0, () => OpenAuto(atr, f, pp), 10); }
        }
    }

    // the live picture of what the plugin is doing, in a glass box (in the middle of the knobs)
    void PaintVizBox(Graphics g, RectangleF b, Fx f)
    {
        using (var p = RR(b, 12)) { using (var br = new LinearGradientBrush(b, Color.FromArgb(255, 6, 2, 18), Color.FromArgb(255, 18, 6, 40), 90f)) g.FillPath(br, p); var gs = g.Save(); g.SetClip(p, CombineMode.Intersect); PaintGalaxy(g, b, f.Type.GetHashCode() ^ 0x77, 1.3f); PaintOrb(g, b); PaintFxViz(g, Rc(b.X + 8, b.Y + 8, b.Width - 16, b.Height - 16), f, false); g.Restore(gs); }
        Glow(g, Color.FromArgb(255, 176, 92, 255), b, 12, 0.7f, 3); StrokeRR(g, Color.FromArgb(220, 176, 92, 255), b, 12, 1.4f);
        if (!eng.Playing) Txt(g, "PRESS PLAY TO SEE WHAT " + f.Title.ToUpperInvariant() + " IS DOING", 9, DimC, Rc(b.X + 8, b.Bottom - 26, b.Width - 16, 18), StringAlignment.Center, true);
    }

    // a glowing planet with orbit rings and a little moon, in the middle of the display (it turns slowly while the song plays)
    void PaintOrb(Graphics g, RectangleF b)
    {
        float R0 = Math.Min(b.Height * 0.3f, b.Width * 0.22f), cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2 - 6; var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        double t = eng.Playing ? Environment.TickCount / 1400.0 : 0.6; if (eng.Playing) lastInput = DateTime.Now;
        Action<float, float, float, float, Color, int> glow = (gx, gy, rw, rh, c, al) => { using (var pth = new GraphicsPath()) { pth.AddEllipse(gx - rw, gy - rh, rw * 2, rh * 2); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb(Math.Min(255, al), c), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, pth); } };
        // the bloom: a big soft violet light round the planet
        glow(cx, cy, R0 * 2.6f, R0 * 2.3f, Color.FromArgb(255, 150, 80, 255), 150); glow(cx, cy, R0 * 1.6f, R0 * 1.6f, Color.FromArgb(255, 190, 120, 255), 170);
        // the rings: a soft glow under a bright line (back halves first)
        Action<float, float, int, bool> ring = (rx, ry, al, front) => { int st = front ? 0 : 180; using (var pg = new Pen(Color.FromArgb(al / 3, 190, 140, 255), 6f)) g.DrawArc(pg, cx - rx, cy - ry, rx * 2, ry * 2, st, 180); using (var pen = new Pen(Color.FromArgb(al, 230, 205, 255), 1.6f)) g.DrawArc(pen, cx - rx, cy - ry, rx * 2, ry * 2, st, 180); };
        ring(R0 * 1.75f, R0 * 0.42f, 150, false); ring(R0 * 2.25f, R0 * 0.56f, 100, false);
        // the planet: a lit violet sphere with swirling bands and a bright rim
        var pr = new RectangleF(cx - R0, cy - R0, R0 * 2, R0 * 2);
        using (var pth = new GraphicsPath()) { pth.AddEllipse(pr); using (var pb = new PathGradientBrush(pth) { CenterPoint = new PointF(cx - R0 * 0.38f, cy - R0 * 0.42f), CenterColor = Color.FromArgb(255, 245, 220, 255), SurroundColors = new[] { Color.FromArgb(255, 70, 20, 160) } }) g.FillPath(pb, pth);
          var gs = g.Save(); g.SetClip(pth, CombineMode.Intersect); for (int k = 0; k < 5; k++) { float yy = cy - R0 + (k + 0.5f) * R0 * 0.4f + (float)Math.Sin(t + k) * 3; using (var pen = new Pen(Color.FromArgb(60, 120, 60, 255), R0 * 0.12f)) g.DrawArc(pen, cx - R0 * 1.4f, yy - R0 * 0.3f, R0 * 2.8f, R0 * 0.6f, 0, 180); }
          using (var rim = new Pen(Color.FromArgb(160, 210, 160, 255), R0 * 0.1f)) g.DrawArc(rim, pr.X + R0 * 0.05f, pr.Y + R0 * 0.05f, R0 * 1.9f, R0 * 1.9f, 20, 140); g.Restore(gs); }   // light catching the lower edge
        using (var pen = new Pen(Color.FromArgb(240, 225, 195, 255), 1.6f)) g.DrawEllipse(pen, pr);
        ring(R0 * 1.75f, R0 * 0.42f, 230, true); ring(R0 * 2.25f, R0 * 0.56f, 150, true);
        // the flare: a white starburst on the top left of the planet, with long rays
        float fx = cx - R0 * 0.42f, fy = cy - R0 * 0.48f; float pulse = eng.Playing ? 0.85f + 0.15f * (float)Math.Sin(t * 3) : 1f;
        glow(fx, fy, R0 * 0.75f * pulse, R0 * 0.75f * pulse, Color.FromArgb(255, 230, 210, 255), 230); glow(fx, fy, R0 * 0.22f, R0 * 0.22f, Color.White, 255);
        Action<float, float, float, int> ray = (ang, len, wid, al) => { float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang); using (var pth = new GraphicsPath()) { pth.AddPolygon(new[] { new PointF(fx - dy * wid, fy + dx * wid), new PointF(fx + dx * len, fy + dy * len), new PointF(fx + dy * wid, fy - dx * wid), new PointF(fx - dx * len, fy - dy * len) }); using (var lb = new PathGradientBrush(pth) { CenterPoint = new PointF(fx, fy), CenterColor = Color.FromArgb(al, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 220, 200, 255) } }) g.FillPath(lb, pth); } };
        ray(0, R0 * 1.9f * pulse, 2.2f, 230); ray((float)Math.PI / 2, R0 * 1.5f * pulse, 2.2f, 230); ray((float)Math.PI / 4, R0 * 0.8f, 1.4f, 170); ray(-(float)Math.PI / 4, R0 * 0.8f, 1.4f, 170);
        // the moon, and sparkles on the rings
        float mx = cx + (float)Math.Cos(t) * R0 * 2.25f, my = cy + (float)Math.Sin(t) * R0 * 0.56f; glow(mx, my, 9, 9, Color.FromArgb(255, 210, 230, 255), 200); using (var mb = new SolidBrush(Color.White)) g.FillEllipse(mb, mx - 2.5f, my - 2.5f, 5, 5);
        for (int k = 0; k < 6; k++) { double a2 = t * 0.6 + k * 1.05; float sx = cx + (float)Math.Cos(a2) * R0 * 1.75f, sy = cy + (float)Math.Sin(a2) * R0 * 0.42f; if (Math.Sin(a2) < 0) continue; glow(sx, sy, 5, 5, Color.White, 220); }
        g.SmoothingMode = oldSm;
    }

    // a galaxy in a box: a deep sky, nebula light and stars (the same picture every time for the same window)
    // a full galaxy: deep space, layered nebula clouds, a dusty band of stars, spiral galaxies and a sky of stars. It is drawn once per size
    // into a picture and reused (so a window full of it costs nothing while you play).
    static readonly Dictionary<string, Bitmap> galaxyCache = new Dictionary<string, Bitmap>();
    void PaintGalaxy(Graphics g, RectangleF r, int seed, float strength)
    {
        int w = Math.Max(1, (int)r.Width), h = Math.Max(1, (int)r.Height), bw = (w + 127) / 128 * 128, bh = (h + 127) / 128 * 128;   // made in steps of 128 pixels and cut to size, so windows of nearly the same size share one galaxy
        string key = bw + "x" + bh + ":" + seed + ":" + strength.ToString("0.00");
        Bitmap bmp; lock (galaxyCache) { if (!galaxyCache.TryGetValue(key, out bmp)) { if (galaxyCache.Count > 40) { foreach (var b0 in galaxyCache.Values) b0.Dispose(); galaxyCache.Clear(); } bmp = MakeGalaxy(bw, bh, seed, strength); galaxyCache[key] = bmp; } }
        g.DrawImage(bmp, new Rectangle((int)r.X, (int)r.Y, w, h), new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);   // unscaled: much faster than stretching
    }
    internal static Bitmap MakeGalaxy(int w, int h, int seed, float strength, Color[] palette = null)
    {
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); var rnd = new Random(seed);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; Func<int, int> A = v => Math.Max(0, Math.Min(255, (int)(v * strength)));
            Color[] neb = palette ?? new[] { Color.FromArgb(255, 150, 70, 255), Color.FromArgb(255, 220, 70, 230), Color.FromArgb(255, 110, 80, 255), Color.FromArgb(255, 180, 120, 255), Color.FromArgb(255, 120, 60, 230) };
            // deep space: nearly black, a touch of purple
            using (var sky = new LinearGradientBrush(new RectangleF(0, 0, w, h), Color.FromArgb(palette == null ? 255 : 200, 7, 2, 18), Color.FromArgb(palette == null ? 255 : 200, 14, 4, 32), 70f)) g.FillRectangle(sky, 0, 0, w, h);
            Action<float, float, float, float, Color, int> cloud = (cx, cy, rw, rh, c, al) => { using (var pth = new GraphicsPath()) { pth.AddEllipse(cx - rw, cy - rh, rw * 2, rh * 2); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb(A(al), c), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, pth); } };
            Func<Color, float, Color> deep = (c, k) => Color.FromArgb(255, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
            float big = Math.Max(w, h);
            // dark coloured clouds that the strands glow inside
            for (int i = 0; i < 6; i++) { float cx = (float)rnd.NextDouble() * w, cy = (float)rnd.NextDouble() * h, rw = big * (float)(0.18 + rnd.NextDouble() * 0.28); cloud(cx, cy, rw, rw * (float)(0.3 + rnd.NextDouble() * 0.35), deep(neb[rnd.Next(neb.Length)], 0.55f), 120); }
            // nebula strands: curved wisps, a wide faint glow down to a bright thin core (the look of real nebula filaments)
            int strands = Math.Max(10, Math.Min(70, (int)(w * h / 9000)));
            for (int i = 0; i < strands; i++)
            {
                float x0 = (float)rnd.NextDouble() * w, y0 = (float)rnd.NextDouble() * h, len = big * (float)(0.12 + rnd.NextDouble() * 0.35); double ang = rnd.NextDouble() * Math.PI * 2;
                var p0 = new PointF(x0, y0); var p3 = new PointF(x0 + (float)Math.Cos(ang) * len, y0 + (float)Math.Sin(ang) * len * 0.6f);
                var p1 = new PointF(x0 + (float)(rnd.NextDouble() - 0.5) * len, y0 + (float)(rnd.NextDouble() - 0.5) * len * 0.8f); var p2 = new PointF(p3.X + (float)(rnd.NextDouble() - 0.5) * len, p3.Y + (float)(rnd.NextDouble() - 0.5) * len * 0.8f);
                Color c = neb[rnd.Next(neb.Length)]; bool bright = rnd.Next(3) == 0;
                foreach (var lay in new[] { new[] { 34f, 9f }, new[] { 20f, 18f }, new[] { 10f, 32f }, new[] { 4.5f, bright ? 60f : 42f }, new[] { 1.8f, bright ? 85f : 55f } })
                    using (var pen = new Pen(Color.FromArgb(A((int)lay[1]), lay[0] < 2f ? LightC(c, 0.35f) : c), lay[0]) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawBezier(pen, p0, p1, p2, p3);
            }
            // bright knots where the gas glows hardest
            for (int i = 0; i < 10; i++) { float cx = (float)rnd.NextDouble() * w, cy = (float)rnd.NextDouble() * h, rw = big * (float)(0.02 + rnd.NextDouble() * 0.05); cloud(cx, cy, rw, rw * 0.6f, LightC(neb[rnd.Next(neb.Length)], 0.3f), 170); }
            // a dusty band of faint stars (a milky way)
            { double ang = (rnd.NextDouble() - 0.5) * 0.9; float by0 = h * (float)(0.3 + rnd.NextDouble() * 0.4); int n = Math.Min(3000, w * h / 50); float band = h * 0.2f;
              for (int i = 0; i < n; i++) { float t = (float)rnd.NextDouble() * (w + 40) - 20, off = (float)((rnd.NextDouble() + rnd.NextDouble() + rnd.NextDouble() - 1.5) / 1.5) * band; float x = t, y = by0 + (float)Math.Tan(ang) * t + off; using (var sb = new SolidBrush(Color.FromArgb(A(40 + rnd.Next(110)), 215, 205, 255))) g.FillRectangle(sb, x, y, 1.1f, 1.1f); } }
            // spiral galaxies
            int gals = w > 500 ? 2 : (w > 160 && h > 60 ? 1 : 0);
            for (int gi = 0; gi < gals; gi++)
            {
                float gr = Math.Min(h * 0.42f, 90f) * (gi == 0 ? 1f : 0.7f), gx = gi == 0 ? w - gr * 1.3f : gr * 1.4f, gy = h * (gi == 0 ? 0.4f : 0.7f); double tilt = 0.35 + rnd.NextDouble() * 0.3, rot = rnd.NextDouble() * Math.PI;
                cloud(gx, gy, gr * 1.3f, gr * 1.3f * (float)tilt * 1.4f, Color.FromArgb(255, 100, 60, 220), 100);
                for (int arm = 0; arm < 3; arm++) for (int k = 0; k < 220; k++)
                {
                    double t = k / 220.0, ang = rot + arm * Math.PI * 2 / 3 + t * 4.6, rr = gr * (0.08 + t) + (rnd.NextDouble() - 0.5) * gr * 0.12; float x = gx + (float)(Math.Cos(ang) * rr), y = gy + (float)(Math.Sin(ang) * rr * tilt);
                    Color c = t < 0.3 ? Color.FromArgb(255, 240, 230, 255) : (t < 0.65 ? Color.FromArgb(255, 170, 140, 255) : Color.FromArgb(255, 140, 110, 255)); float sz = (float)(0.9 + rnd.NextDouble() * 1.3);
                    using (var sb = new SolidBrush(Color.FromArgb(A((int)(235 * (1.05 - t))), c))) g.FillEllipse(sb, x - sz / 2, y - sz / 2, sz, sz);
                }
                cloud(gx, gy, gr * 0.28f, gr * 0.28f * (float)tilt * 1.3f, Color.FromArgb(255, 255, 245, 255), 255); cloud(gx, gy, gr * 0.1f, gr * 0.08f, Color.White, 255);
            }
            // stars: many sharp small ones, some coloured, the bright ones with a glow and a twinkle
            int stars = Math.Min(1400, w * h / 200);
            for (int i = 0; i < stars; i++)
            {
                float x = (float)rnd.NextDouble() * w, y = (float)rnd.NextDouble() * h; int kind = rnd.Next(50); float sz = kind == 0 ? 2.6f : (kind < 5 ? 1.7f : 1f);
                Color c = kind % 7 == 1 ? Color.FromArgb(255, 170, 200, 255) : (kind % 7 == 2 ? Color.FromArgb(255, 255, 200, 245) : Color.FromArgb(255, 245, 242, 255));
                using (var sb = new SolidBrush(Color.FromArgb(A(kind == 0 ? 255 : 110 + rnd.Next(140)), c))) g.FillEllipse(sb, x - sz / 2, y - sz / 2, sz, sz);
                if (kind == 0) { cloud(x, y, 8, 8, Color.FromArgb(255, 200, 180, 255), 140); using (var pen = new Pen(Color.FromArgb(A(190), 230, 225, 255), 0.9f)) { g.DrawLine(pen, x - 9, y, x + 9, y); g.DrawLine(pen, x, y - 9, x, y + 9); } }
            }
        }
        return bmp;
    }

    // the PURPHEX logo with a plugin's name under it (like PURPHEX / ANALOG EQ)
    void PaintPurphexMark(Graphics g, float x, float y, float h, string sub, Color glow)
    {
        float fs = h * 0.62f; var oldT = g.TextRenderingHint; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using (var f = new Font("Segoe UI Black", fs, FontStyle.Italic, GraphicsUnit.Pixel))
        {
            var sz = g.MeasureString("PURPHEX", f); var tr = new RectangleF(x, y, sz.Width + 4, sz.Height);
            for (int k = 3; k >= 1; k--) using (var gb = new SolidBrush(Color.FromArgb(40, 150, 90, 255))) g.DrawString("PURPHEX", f, gb, x - k * 0.8f, y + k * 0.5f);
            var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            GalaxyText(g, "PURPHEX", f, x, y + sz.Height * 0.08f);
            if (!string.IsNullOrEmpty(sub)) using (var f2 = new Font("Segoe UI", Math.Max(13f, h * 0.42f), FontStyle.Bold, GraphicsUnit.Pixel))
            {   // the plugin's name, in galaxy writing too, letters spaced out
                string spaced = string.Join("\u2009", sub.ToUpperInvariant().ToCharArray().Select(ch => ch.ToString()).ToArray());
                GalaxyText(g, spaced, f2, x + 3, y + sz.Height * 0.74f);
            }
            g.SmoothingMode = oldSm;
        }
        g.TextRenderingHint = oldT;
    }
}
