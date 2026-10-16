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

    void VizUpdate(bool playing, StudioEngine src = null)
    { var en = src ?? eng;
        long now = Environment.TickCount; float lvl = playing ? Math.Max(en.MasterPeakL, en.MasterPeakR) : 0f; vizLevel += (lvl - vizLevel) * 0.4f;
        if (unchecked(now - vizTick) >= 15) { vizTick = now; vizHist[vizHead] = playing ? Math.Min(1f, vizLevel * 1.5f) : 0f; vizHead = (vizHead + 1) % vizHist.Length; }
        if (!playing) { for (int b = 0; b < vizBand.Length; b++) vizBand[b] *= 0.8f; return; }
        if (vzHann == null) { vzHann = new double[512]; for (int i = 0; i < 512; i++) vzHann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 511); }
        int p = (en.MasterTapPos - 512) & 8191; var tap = en.MasterTap; for (int i = 0; i < 512; i++) { vzRe[i] = tap[(p + i) & 8191] * vzHann[i]; vzIm[i] = 0; }
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
            using (var fill = new SolidBrush(TM.C(255, 8, 4, 22))) g.FillPath(fill, path);
            for (int i = 3; i >= 1; i--) using (var pen = new Pen(TM.C((int)(34 * glow * (4 - i) / 3 * 2.2f), 130, 90, 255), i * 2.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
            var rb = Rc(cx - hw, cy - hh, hw * 2, hh * 2);
            using (var gb = new LinearGradientBrush(rb, TM.C(255, 0, 229, 255), TM.C(255, 255, 43, 214), 25f)) using (var pen = new Pen(gb, 2.2f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
        }
        float r0 = hh * 0.52f;
        if (!playing)
        {
            using (var pen = new Pen(TM.C(220, 200, 80, 255), 1.6f)) g.DrawEllipse(pen, cx - r0 * 1.12f, cy - r0 * 1.12f, r0 * 2.24f, r0 * 2.24f);
            float[] ys = { 0, 0.15f, -0.3f, 0.5f, -0.75f, 0.45f, -0.25f, 0.2f, 0 }; var pts = new PointF[ys.Length]; for (int i = 0; i < ys.Length; i++) pts[i] = new PointF(cx - hw * 0.62f + i * (hw * 1.24f / (ys.Length - 1)), cy + ys[i] * hh * 0.62f);
            using (var pen = new Pen(TM.C(235, 0, 229, 255), 1.6f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
        }
        else
        {
            int nb = vizBand.Length; for (int i = 0; i < nb; i++)
            {
                double a = i * 2 * Math.PI / nb - Math.PI / 2; float len = 1.5f + vizBand[i] * hh * 0.5f; float ra = r0, rb2 = r0 + len;
                var c0 = i < nb / 2 ? TM.C(255, 0, 229, 255) : TM.C(255, 255, 43, 214);
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

    // The vertical version of that scroll strip, for lists (styles, presets, effects...): solid black, arrows at both ends, the lit window shows where you are,
    // and the logo in the middle is what you hold and drag (waves flow out of it while music plays).
    int vHold; Func<float> vHoldGet; Action<float> vHoldSet; float vHoldMax;
    internal bool vLogoDrag;   // the logo of a scroll bar is held and dragged right now
    void VScroll(Graphics g, RectangleF bar, float contentH, float viewH, Func<float> get, Action<float> set)
    {
        if (contentH <= viewH + 1 || bar.Height < 160) return; bool playing = eng.Playing; VizUpdate(playing); float max = contentH - viewH;
        if (vHold != 0 && dragReg != null && vHoldSet != null) vHoldSet(Math.Max(0, Math.Min(vHoldMax, vHoldGet() + vHold * 8))); else vHold = 0;
        FillRR(g, Color.Black, bar, 12); using (var pen = new Pen(Color.FromArgb(playing ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(bar, 12)) g.DrawPath(pen, p); }
        const float cap = 34; var up = Rc(bar.X, bar.Y, bar.Width, cap); var dn = Rc(bar.X, bar.Bottom - cap, bar.Width, cap); var inner = Rc(bar.X + 3, bar.Y + cap, bar.Width - 6, bar.Height - 2 * cap); bool hu = Hot(up), hd = Hot(dn);
        float cx = bar.X + bar.Width / 2, lh = Math.Min(14f, bar.Width / 2 - 3), lhw = lh * 1.16f, pad = lh + 3, trav = Math.Max(1f, inner.Height - 2 * pad);
        using (var pen = new Pen(Color.FromArgb(hu ? 255 : 200, NeonCyan), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(cx - 6, up.Y + 18 + k * 6), new PointF(cx, up.Y + 12 + k * 6), new PointF(cx + 6, up.Y + 18 + k * 6) });
        using (var pen = new Pen(Color.FromArgb(hd ? 255 : 200, NeonPink), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(cx - 6, dn.Bottom - 18 - k * 6), new PointF(cx, dn.Bottom - 12 - k * 6), new PointF(cx + 6, dn.Bottom - 18 - k * 6) });
        // the logo rides along the bar: where it is = where you are in the list
        float frac = max <= 0 ? 0 : Math.Max(0, Math.Min(1, get() / max)); float lcy = inner.Y + pad + frac * trav; var logoR = Rc(cx - lhw - 3, lcy - lh - 3, lhw * 2 + 6, lh * 2 + 6);
        using (var pen = new Pen(Color.FromArgb(60, NeonViolet), 2f)) g.DrawLine(pen, cx, inner.Y + 2, cx, inner.Bottom - 2);
        if (playing)
        {   // waves flow out of the logo, up and down the bar
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); int hl = vizHist.Length; float maxW = bar.Width / 2 - 4;
            for (int c = 0; c < 90; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxW * (1f - c / 130f)); int al = (int)(220 * (1f - c / 100f)); if (al <= 4) break;
                float yu = lcy - lh - 4 - c * 2.2f, yd = lcy + lh + 4 + c * 2.2f;
                using (var pu = new Pen(TM.C(al, 0, 229, 255), 1.5f)) g.DrawLine(pu, cx - amp, yu, cx + amp, yu); using (var pd = new Pen(TM.C(al, 255, 43, 214), 1.5f)) g.DrawLine(pd, cx - amp, yd, cx + amp, yd);
            }
            g.Restore(st);
        }
        bool hotLogo = Hot(logoR) || (dragReg != null && dragReg.R == logoR); DrawLogo(g, cx, lcy, lh, playing, hotLogo);
        // click the bar to jump (the logo goes there), arrows: click to step, hold to keep going, logo: hold and drag
        Action<float> jump = my => set(Math.Max(0, Math.Min(max, (my - inner.Y - pad) / trav * max)));
        var blk = Reg(bar, () => { }); blk.Overlay = true; blk.ScrollBar = true;   // the whole bar is on top of the list (its edges too)
        var rg = Reg(inner, () => jump(MousePos.Y)); rg.OnDrag = pt => jump(pt.Y);
        var ru = Reg(up, () => { set(Math.Max(0, get() - 60)); vHold = -1; vHoldGet = get; vHoldSet = set; vHoldMax = max; }); ru.OnDrag = pt => { }; ru.OnUp = () => vHold = 0;
        var rd = Reg(dn, () => { set(Math.Min(max, get() + 60)); vHold = 1; vHoldGet = get; vHoldSet = set; vHoldMax = max; }); rd.OnDrag = pt => { }; rd.OnUp = () => vHold = 0;
        float downY = 0, downS = 0; var lg = Reg(logoR, () => { downY = MousePos.Y; downS = get(); vLogoDrag = true; }); lg.OnUp = () => vLogoDrag = false; lg.OnDrag = pt => set(Math.Max(0, Math.Min(max, downS + (pt.Y - downY) / trav * max)));
        Action<int> wheel = d => set(Math.Max(0, Math.Min(max, get() - d * 80)));   // the mouse wheel over any part of the bar scrolls too (it used to do nothing there)
        blk.OnWheel = wheel; rg.OnWheel = wheel; ru.OnWheel = wheel; rd.OnWheel = wheel; lg.OnWheel = wheel; rg.ScrollBar = ru.ScrollBar = rd.ScrollBar = lg.ScrollBar = true;
        if (Hot(logoR)) { bubble = "Hold the logo and drag it to scroll the list"; bubblePt = MousePos; }
    }

    // The same logo scroll bar lying down, for rows that scroll sideways (the projects in the welcome box): arrows at both ends, the logo is what you hold and drag
    int hHold; Func<float> hHoldGet; Action<float> hHoldSet; float hHoldMax, hHoldStep;
    void HScroll(Graphics g, RectangleF bar, float contentW, float viewW, Func<float> get, Action<float> set, float step)
    {
        if (contentW <= viewW + 1 || bar.Width < 160) return; bool playing = eng.Playing; VizUpdate(playing); float max = contentW - viewW;
        if (hHold != 0 && dragReg != null && hHoldSet != null) hHoldSet(Math.Max(0, Math.Min(hHoldMax, hHoldGet() + hHold * 8))); else hHold = 0;
        FillRR(g, Color.Black, bar, 12); using (var pen = new Pen(Color.FromArgb(playing ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(bar, 12)) g.DrawPath(pen, p); }
        const float cap = 34; var lf = Rc(bar.X, bar.Y, cap, bar.Height); var rt = Rc(bar.Right - cap, bar.Y, cap, bar.Height); var inner = Rc(bar.X + cap, bar.Y + 3, bar.Width - 2 * cap, bar.Height - 6); bool hl = Hot(lf), hr = Hot(rt);
        float cy = bar.Y + bar.Height / 2, lh = Math.Min(14f, bar.Height / 2 - 3), lhw = lh * 1.16f, pad = lhw + 3, trav = Math.Max(1f, inner.Width - 2 * pad);
        using (var pen = new Pen(Color.FromArgb(hl ? 255 : 200, NeonCyan), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(lf.X + 18 + k * 6, cy - 6), new PointF(lf.X + 12 + k * 6, cy), new PointF(lf.X + 18 + k * 6, cy + 6) });
        using (var pen = new Pen(Color.FromArgb(hr ? 255 : 200, NeonPink), 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(rt.Right - 18 - k * 6, cy - 6), new PointF(rt.Right - 12 - k * 6, cy), new PointF(rt.Right - 18 - k * 6, cy + 6) });
        float frac = max <= 0 ? 0 : Math.Max(0, Math.Min(1, get() / max)); float lcx = inner.X + pad + frac * trav; var logoR = Rc(lcx - lhw - 3, cy - lh - 3, lhw * 2 + 6, lh * 2 + 6);
        using (var pen = new Pen(Color.FromArgb(60, NeonViolet), 2f)) g.DrawLine(pen, inner.X + 2, cy, inner.Right - 2, cy);
        if (playing)
        {   // waves flow out of the logo, to the left and to the right
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); int hlen = vizHist.Length; float maxH = bar.Height / 2 - 4;
            for (int c = 0; c < 140; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hlen * 4) % hlen]; float amp = Math.Max(1f, v * maxH * (1f - c / 200f)); int al = (int)(220 * (1f - c / 150f)); if (al <= 4) break;
                float xl = lcx - lhw - 4 - c * 2.2f, xr = lcx + lhw + 4 + c * 2.2f;
                using (var pl = new Pen(TM.C(al, 0, 229, 255), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp); using (var pr = new Pen(TM.C(al, 255, 43, 214), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
            }
            g.Restore(st);
        }
        bool hotLogo = Hot(logoR) || (dragReg != null && dragReg.R == logoR); DrawLogo(g, lcx, cy, lh, playing, hotLogo);
        Action<float> jump = mx => set(Math.Max(0, Math.Min(max, (mx - inner.X - pad) / trav * max)));
        { var hb = Reg(bar, () => { }); hb.Overlay = true; hb.ScrollBar = true; }
        var rg = Reg(inner, () => jump(MousePos.X)); rg.OnDrag = pt => jump(pt.X); rg.ScrollBar = true;
        var rl = Reg(lf, () => { set(Math.Max(0, get() - step)); hHold = -1; hHoldGet = get; hHoldSet = set; hHoldMax = max; }); rl.OnDrag = pt => { }; rl.OnUp = () => hHold = 0;
        var rr = Reg(rt, () => { set(Math.Min(max, get() + step)); hHold = 1; hHoldGet = get; hHoldSet = set; hHoldMax = max; }); rr.OnDrag = pt => { }; rr.OnUp = () => hHold = 0;
        float downX = 0, downS = 0; var lg = Reg(logoR, () => { downX = MousePos.X; downS = get(); }); lg.OnDrag = pt => set(Math.Max(0, Math.Min(max, downS + (pt.X - downX) / trav * max)));
        if (Hot(logoR)) { bubble = "Hold the logo and drag it to see all your projects"; bubblePt = MousePos; }
    }

    // The bar shows the whole song. The lit window is what you see: hold the logo and drag to move it, drag the window's ends to zoom, click the bar to jump, double-click to fit the whole song.
    void PaintOverview(Graphics g, RectangleF tl)
    {
        const float barH = 34; var ob = Rc(HeadW, ClientSize.Height - barH - 6, tl.Width, barH); double total = Math.Max(eng.EndFrame * 1.04, StudioEngine.SR * 10.0);
        PaintScrollStrip(g, ob, total, 0, tl.Width, true);
    }
    // One scroll strip, used under the tracks and at the top of Flex Pitch: ob = the strip, total = the frames it stands for, baseFrame = the frame at its left end,
    // viewW = the width in pixels of the part of the song you are looking at. Solid black, so nothing behind it can show through or flicker.
    void PaintScrollStrip(Graphics g, RectangleF ob, double total, long baseFrame, float viewW, bool clips)
    {
        bool playing = eng.Playing; VizUpdate(playing); if (scrollHold != 0 && dragReg != null) ScrollView(scrollHold * 0.012); else scrollHold = 0;   // a chevron held down keeps scrolling
        const float cw = 44;
        FillRR(g, Color.Black, ob, 12);
        using (var pen = new Pen(Color.FromArgb(playing ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(ob, 12)) g.DrawPath(pen, p); }
        var inner = Rc(ob.X + cw, ob.Y + 3, ob.Width - 2 * cw, ob.Height - 6); float tw = inner.Width, cy = ob.Y + ob.Height / 2;
        // the clips, small and faint
        int n = clips ? LaneCount : 0; if (n > 0)
            for (int i = 0; i < n; i++)
            {
                var tr = eng.Tracks[i]; float a = inner.X + (float)(Math.Max(0, tr.Offset) / total * tw), b = inner.X + (float)(Math.Min(total, tr.Offset + Math.Max(1, tr.UsedLength)) / total * tw), th = (inner.Height) / n;
                using (var br = new SolidBrush(Color.FromArgb(playing ? 34 : 80, Neon(tr.Color)))) g.FillRectangle(br, a, inner.Y + i * th, Math.Max(2, b - a), Math.Max(1.5f, th - 1));
            }
        // the window you are looking at
        double win = viewW * spp; float ww = Math.Max(16f, (float)(win / total * tw)), x0 = inner.X + (float)((scroll - baseFrame) / total * tw); x0 = Math.Max(inner.X, Math.Min(inner.Right - ww, x0));
        var thumb = Rc(x0, inner.Y, ww, inner.Height);
        using (var br = new SolidBrush(TM.C(34, 140, 150, 200))) g.FillRectangle(br, thumb);   // a soft window (no bright box); its two ends can still be dragged to zoom
        using (var pen = new Pen(TM.C(120, 200, 205, 240), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { float gy = ob.Y + ob.Height / 2; g.DrawLine(pen, thumb.X + 1, gy - 7, thumb.X + 1, gy + 7); g.DrawLine(pen, thumb.Right - 1, gy - 7, thumb.Right - 1, gy + 7); }
        // the logo rides on the window: where the logo is = where you are, and it is what you hold and drag
        float lh = ob.Height / 2 - 2, lhw = lh * 1.16f; float logoCx = Math.Max(inner.X + lhw + 2, Math.Min(inner.Right - lhw - 2, thumb.X + thumb.Width / 2));
        // the waves that flow out of the logo along the bar
        if (playing)
        {
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias; float maxH = ob.Height / 2 - 3; int hl = vizHist.Length;
            for (int c = 0; c < 190; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxH * (1f - c / 260f) * (0.75f + 0.25f * vizBand[(c * 3) % vizBand.Length])); float fade = 1f - c / 200f; int al = (int)(235 * fade); if (al <= 4) break;
                float xl = logoCx - lhw - 6 - c * 2.2f, xr = logoCx + lhw + 6 + c * 2.2f;
                using (var pl = new Pen(TM.C(al, 0, 229, 255), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp);
                using (var pr = new Pen(TM.C(al, 255, 43, 214), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
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
            autoFit = false; UserScrolled(); float mx = MousePos.X; startScroll = scroll; startSpp = spp;
            if (Math.Abs(mx - thumb.X) < 10) mode = 1; else if (Math.Abs(mx - thumb.Right) < 10) mode = 2; else { mode = 0; grab = mx >= thumb.X && mx <= thumb.Right ? mx - thumb.X : thumb.Width / 2; if (mx < thumb.X || mx > thumb.Right) { double s = baseFrame + (mx - grab - inX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, baseFrame + total - win), s)); } }
        });
        rg.OnDouble = () => FitView();
        rg.OnDrag = pt =>
        {
            float mx = pt.X;
            if (mode == 0) { double s = baseFrame + (mx - grab - inX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, baseFrame + total - win), s)); }
            else if (mode == 2) { double w = Math.Max(30, mx - thumb.X) / tw * total; spp = Math.Max(MinSpp, Math.Min(MaxSpp, w / viewW)); }
            else { double right = startScroll + viewW * startSpp; double nl = Math.Max(0, Math.Min(right - viewW * MinSpp, baseFrame + (mx - inX) / tw * total)); spp = Math.Max(MinSpp, Math.Min(MaxSpp, (right - nl) / viewW)); scroll = (long)Math.Max(0, right - viewW * spp); }
        };
        var chL = Reg(lr, () => { autoFit = false; UserScrolled(); ScrollView(-0.15); scrollHold = -1; }); var chR = Reg(rr, () => { autoFit = false; UserScrolled(); ScrollView(0.15); scrollHold = 1; });
        chL.OnDrag = pt => { }; chR.OnDrag = pt => { }; chL.OnUp = () => scrollHold = 0; chR.OnUp = () => scrollHold = 0;
        // the logo: hold left click on it and drag left or right to scroll
        var logoR = Rc(logoCx - lhw, cy - lh, lhw * 2, lh * 2); bool hotLogo = Hot(logoR) || (dragReg != null && dragReg.R == logoR);
        DrawLogo(g, logoCx, cy, lh, playing, hotLogo);
        float downX = 0; long downScroll = 0; var lg = Reg(logoR, () => { autoFit = false; UserScrolled(); downX = MousePos.X; downScroll = scroll; });
        lg.OnDrag = pt => { double s = downScroll + (pt.X - downX) / tw * total; scroll = (long)Math.Max(0, Math.Min(Math.Max(0, baseFrame + total - win), s)); };
        lg.OnDouble = () => FitView();
        if (Hot(logoR)) { bubble = "Hold and drag to scroll along the song"; bubblePt = MousePos; }
    }
}
