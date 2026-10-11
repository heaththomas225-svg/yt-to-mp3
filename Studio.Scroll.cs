using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  The scroll bar under the tracks: a slim bar that fits the grid, with a hexagon logo you hold and drag to move along the song,
//  chevrons at both ends (hold to scroll), and a live audio visualiser: while music plays the logo pulses and waves flow out of it along the bar.
// ======================================================================
partial class StudioForm
{
    int scrollHold;
    readonly float[] vizHist = new float[300]; int vizHead; readonly float[] vizBand = new float[28]; long vizTick; float vizLevel;
    readonly double[] vzRe = new double[512], vzIm = new double[512]; static double[] vzHann;

    void VizUpdate(bool playing)
    {
        long now = Environment.TickCount; float lvl = playing ? Math.Max(eng.MasterPeakL, eng.MasterPeakR) : 0f; vizLevel += (lvl - vizLevel) * 0.4f;
        if (unchecked(now - vizTick) >= 15) { vizTick = now; vizHist[vizHead] = playing ? Math.Min(1f, vizLevel * 1.5f) : 0f; vizHead = (vizHead + 1) % vizHist.Length; }
        if (!playing) { for (int b = 0; b < vizBand.Length; b++) vizBand[b] *= 0.8f; return; }
        if (vzHann == null) { vzHann = new double[512]; for (int i = 0; i < 512; i++) vzHann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 511); }
        int p = (eng.MasterTapPos - 512) & 8191; var tap = eng.MasterTap; for (int i = 0; i < 512; i++) { vzRe[i] = tap[(p + i) & 8191] * vzHann[i]; vzIm[i] = 0; }
        Fft.Transform(vzRe, vzIm, false);
        for (int b = 0; b < vizBand.Length; b++)
        {
            int lo = 1 + (int)Math.Pow(210, b / (double)vizBand.Length), hi = Math.Max(lo + 1, 1 + (int)Math.Pow(210, (b + 1) / (double)vizBand.Length)); double sum = 0; int c = 0;
            for (int k = lo; k < hi && k < 256; k++) { sum += Math.Sqrt(vzRe[k] * vzRe[k] + vzIm[k] * vzIm[k]); c++; }
            float v = c == 0 ? 0 : (float)Math.Min(1.0, Math.Log10(1 + 9 * sum / c) / 2.4); vizBand[b] = Math.Max(v, vizBand[b] * 0.86f);
        }
    }

    static GraphicsPath HexPath(float cx, float cy, float hw, float hh)
    {
        var p = new GraphicsPath(); p.AddPolygon(new[] { new PointF(cx - hw, cy), new PointF(cx - hw / 2, cy - hh), new PointF(cx + hw / 2, cy - hh), new PointF(cx + hw, cy), new PointF(cx + hw / 2, cy + hh), new PointF(cx - hw / 2, cy + hh) }); return p;
    }
    // the logo: a neon hexagon; at rest a heartbeat line in a ring, while music plays a ring of bars that dance to the sound
    void DrawLogo(Graphics g, float cx, float cy, float hh, bool playing, bool hot)
    {
        float hw = hh * 1.16f; float glow = playing ? 0.35f + Math.Min(0.65f, vizLevel * 1.6f) : (hot ? 0.7f : 0.4f);
        using (var path = HexPath(cx, cy, hw, hh))
        {
            using (var fill = new SolidBrush(Color.FromArgb(245, 8, 4, 22))) g.FillPath(fill, path);
            for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)(34 * glow * (4 - i) / 3 * 2.2f), 130, 90, 255), i * 2.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
            var rb = Rc(cx - hw, cy - hh, hw * 2, hh * 2);
            using (var gb = new LinearGradientBrush(rb, Color.FromArgb(0, 229, 255), Color.FromArgb(255, 43, 214), 25f)) using (var pen = new Pen(gb, 2.2f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
        }
        float r0 = hh * 0.52f;
        if (!playing)
        {
            using (var pen = new Pen(Color.FromArgb(220, 200, 80, 255), 1.6f)) g.DrawEllipse(pen, cx - r0 * 1.12f, cy - r0 * 1.12f, r0 * 2.24f, r0 * 2.24f);
            float[] ys = { 0, 0.15f, -0.3f, 0.5f, -0.75f, 0.45f, -0.25f, 0.2f, 0 }; var pts = new PointF[ys.Length]; for (int i = 0; i < ys.Length; i++) pts[i] = new PointF(cx - hw * 0.62f + i * (hw * 1.24f / (ys.Length - 1)), cy + ys[i] * hh * 0.62f);
            using (var pen = new Pen(Color.FromArgb(235, 0, 229, 255), 1.6f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
        }
        else
        {
            int nb = vizBand.Length; for (int i = 0; i < nb; i++)
            {
                double a = i * 2 * Math.PI / nb - Math.PI / 2; float len = 1.5f + vizBand[i] * hh * 0.5f; float ra = r0, rb2 = r0 + len;
                var c0 = i < nb / 2 ? Color.FromArgb(0, 229, 255) : Color.FromArgb(255, 43, 214);
                using (var pen = new Pen(Color.FromArgb(235, c0), 1.6f)) g.DrawLine(pen, cx + (float)Math.Cos(a) * ra, cy + (float)Math.Sin(a) * ra * 0.9f, cx + (float)Math.Cos(a) * rb2, cy + (float)Math.Sin(a) * rb2 * 0.9f);
            }
            using (var b = new SolidBrush(Color.FromArgb((int)(120 + 120 * Math.Min(1f, vizLevel * 2)), 255, 255, 255))) g.FillEllipse(b, cx - 2, cy - 2, 4, 4);
        }
    }
    // a pair of chevrons ( << or >> ): they light up with the sound
    void DrawChevrons(Graphics g, float cx, float cy, float h, bool left, Color c, bool hot)
    {
        int a = (int)(hot ? 255 : 190 + 60 * Math.Min(1f, vizLevel * 2)); float d = left ? -1 : 1;
        for (int k = 0; k < 2; k++)
        {
            float x = cx + d * (k * 7 - 3.5f); var pts = new[] { new PointF(x + d * 4, cy - h), new PointF(x - d * 4, cy), new PointF(x + d * 4, cy + h) };
            Color cc2 = k == 0 ? c : Color.FromArgb(Math.Min(255, (int)c.R + 70), Math.Max(0, (int)c.G - 90), (int)c.B); using (var pen = new Pen(Color.FromArgb(a, cc2), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, pts);
        }
    }

    // The bar shows the whole song. The lit window is what you see: hold the logo and drag to move it, drag the window's ends to zoom, click the bar to jump, double-click to fit the whole song.
    void PaintOverview(Graphics g, RectangleF tl)
    {
        bool playing = eng.Playing; VizUpdate(playing); if (scrollHold != 0 && dragReg != null) ScrollView(scrollHold * 0.012); else scrollHold = 0;   // a chevron held down keeps scrolling
        const float barH = 34, cw = 44; var ob = Rc(HeadW, ClientSize.Height - barH - 6, tl.Width, barH); double total = Math.Max(eng.EndFrame * 1.04, StudioEngine.SR * 10.0);
        FillRR(g, Color.FromArgb(240, 8, 4, 18), ob, 12);
        using (var pen = new Pen(Color.FromArgb(playing ? 210 : 120, NeonViolet), 1.3f)) { using (var p = RR(ob, 12)) g.DrawPath(pen, p); }
        if (playing) Glow(g, NeonViolet, ob, 12, 0.35f + Math.Min(0.5f, vizLevel), 3);
        var inner = Rc(ob.X + cw, ob.Y + 3, ob.Width - 2 * cw, ob.Height - 6); float tw = inner.Width, cy = ob.Y + ob.Height / 2;
        // the clips, small and faint
        int n = LaneCount; if (n > 0)
            for (int i = 0; i < n; i++)
            {
                var tr = eng.Tracks[i]; float a = inner.X + (float)(Math.Max(0, tr.Offset) / total * tw), b = inner.X + (float)(Math.Min(total, tr.Offset + Math.Max(1, tr.UsedLength)) / total * tw), th = (inner.Height) / n;
                using (var br = new SolidBrush(Color.FromArgb(playing ? 34 : 80, Neon(tr.Color)))) g.FillRectangle(br, a, inner.Y + i * th, Math.Max(2, b - a), Math.Max(1.5f, th - 1));
            }
        // the window you are looking at
        double win = tl.Width * spp; float ww = Math.Max(16f, (float)(win / total * tw)), x0 = inner.X + (float)(scroll / total * tw); x0 = Math.Max(inner.X, Math.Min(inner.Right - ww, x0));
        var thumb = Rc(x0, inner.Y, ww, inner.Height);
        using (var br = new SolidBrush(Color.FromArgb(playing ? 26 : 40, NeonCyan))) g.FillRectangle(br, thumb);
        using (var pen = new Pen(Color.FromArgb(200, NeonCyan), 1.4f)) { g.DrawLine(pen, thumb.X, thumb.Y, thumb.X, thumb.Bottom); g.DrawLine(pen, thumb.Right, thumb.Y, thumb.Right, thumb.Bottom); }
        // the logo sits in the middle of the window
        float lh = ob.Height / 2 - 2, lhw = lh * 1.16f; float logoCx = Math.Max(inner.X + lhw, Math.Min(inner.Right - lhw, thumb.X + thumb.Width / 2));
        // the waves that flow out of the logo along the bar
        if (playing)
        {
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias; float maxH = ob.Height / 2 - 3; int hl = vizHist.Length;
            for (int c = 0; c < 190; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxH * (1f - c / 260f) * (0.75f + 0.25f * vizBand[(c * 3) % vizBand.Length])); float fade = 1f - c / 200f; int al = (int)(235 * fade); if (al <= 4) break;
                float xl = logoCx - lhw - 6 - c * 2.2f, xr = logoCx + lhw + 6 + c * 2.2f;
                using (var pl = new Pen(Color.FromArgb(al, 0, 229, 255), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp);
                using (var pr = new Pen(Color.FromArgb(al, 255, 43, 214), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
            }
            g.Restore(st);
        }
        // chevrons: click to step, hold to keep scrolling
        var lr = Rc(ob.X, ob.Y, cw, ob.Height); var rr = Rc(ob.Right - cw, ob.Y, cw, ob.Height); bool hl2 = Hot(lr), hr2 = Hot(rr);
        DrawChevrons(g, lr.X + cw / 2, cy, 7, true, NeonCyan, hl2); DrawChevrons(g, rr.X + cw / 2, cy, 7, false, NeonPink, hr2);
        // the bar itself: click to jump, drag the window's ends to zoom
        int mode = 0; double startScroll = scroll, startSpp = spp; float grab = 0; float inX = inner.X;
        var rg = Reg(Rc(inner.X, ob.Y, inner.Width, ob.Height), () =>
        {
            autoFit = false; float mx = MousePos.X; startScroll = scroll; startSpp = spp;
            if (Math.Abs(mx - thumb.X) < 10) mode = 1; else if (Math.Abs(mx - thumb.Right) < 10) mode = 2; else { mode = 0; grab = mx >= thumb.X && mx <= thumb.Right ? mx - thumb.X : thumb.Width / 2; if (mx < thumb.X || mx > thumb.Right) { double s = (mx - grab - inX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, total - win), s)); } }
        });
        rg.OnDouble = () => FitView();
        rg.OnDrag = pt =>
        {
            float mx = pt.X;
            if (mode == 0) { double s = (mx - grab - inX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, total - win), s)); }
            else if (mode == 2) { double w = Math.Max(30, mx - thumb.X) / tw * total; spp = Math.Max(MinSpp, Math.Min(MaxSpp, w / tl.Width)); }
            else { double right = startScroll + tl.Width * startSpp; double nl = Math.Max(0, Math.Min(right - tl.Width * MinSpp, (mx - inX) / tw * total)); spp = Math.Max(MinSpp, Math.Min(MaxSpp, (right - nl) / tl.Width)); scroll = (long)Math.Max(0, right - tl.Width * spp); }
        };
        var chL = Reg(lr, () => { autoFit = false; ScrollView(-0.15); scrollHold = -1; }); var chR = Reg(rr, () => { autoFit = false; ScrollView(0.15); scrollHold = 1; });
        chL.OnDrag = pt => { }; chR.OnDrag = pt => { }; chL.OnUp = () => scrollHold = 0; chR.OnUp = () => scrollHold = 0;
        // the logo: hold left click on it and drag left or right to scroll
        var logoR = Rc(logoCx - lhw, cy - lh, lhw * 2, lh * 2); bool hotLogo = Hot(logoR) || (dragReg != null && dragReg.R == logoR);
        DrawLogo(g, logoCx, cy, lh, playing, hotLogo);
        float grabX = 0; var lg = Reg(logoR, () => { autoFit = false; grabX = MousePos.X - logoCx; });
        lg.OnDrag = pt => { double s = (pt.X - grabX - thumb.Width / 2 - inX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, total - win), s)); };
        lg.OnDouble = () => FitView();
        if (Hot(logoR)) { bubble = "Hold and drag to scroll along the song"; bubblePt = MousePos; }
    }
}
