using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Neon look for Purphex Music: glowing outlines, LED meters and light strips in each track's colour.
partial class StudioForm
{
    bool dragOver;   // files are being dragged over the window
    static readonly Dictionary<int, Color> neonCache = new Dictionary<int, Color>();
    // any stored track colour is pushed to full-bright neon so old projects glow too
    static Color Neon(int argb)
    {
        Color r; if (neonCache.TryGetValue(argb, out r)) return r;
        var c = Color.FromArgb(argb); double R = c.R / 255.0, G = c.G / 255.0, B = c.B / 255.0, mx = Math.Max(R, Math.Max(G, B)), mn = Math.Min(R, Math.Min(G, B));
        if (mx <= 0.001) { r = TM.C(255, 0, 229, 255); }
        else
        {
            double s = Math.Max((mx - mn) / mx, 0.78); double v = 1.0;
            double h = c.GetHue(); double cc = v * s, x = cc * (1 - Math.Abs((h / 60.0) % 2 - 1)), m = v - cc; double rr, gg, bb;
            if (h < 60) { rr = cc; gg = x; bb = 0; } else if (h < 120) { rr = x; gg = cc; bb = 0; } else if (h < 180) { rr = 0; gg = cc; bb = x; } else if (h < 240) { rr = 0; gg = x; bb = cc; } else if (h < 300) { rr = x; gg = 0; bb = cc; } else { rr = cc; gg = 0; bb = x; }
            r = Color.FromArgb((int)((rr + m) * 255), (int)((gg + m) * 255), (int)((bb + m) * 255));
        }
        neonCache[argb] = r; return r;
    }
    internal static void ApplyTheme(ThemeDef t) { TM.Set(t.Violet, t.Cyan, t.Pink); Ink = TM.C(255, 240, 232, 255); DimC = TM.C(255, 165, 148, 190); Win.NeonHot = t.Cyan; Win.NeonIdle = t.Violet; NeonViolet = t.Violet; NeonCyan = t.Cyan; NeonPink = t.Pink; NeonLime = t.Lime; Accent = t.Accent; Accent2 = t.Accent2; Violet = t.Btn; BtnC = t.BtnC; Edge = Color.FromArgb(120, t.EdgeC); Panel = Color.FromArgb(235, t.Panel); Lane = Color.FromArgb(255, t.Lane); Lane2 = Color.FromArgb(255, t.Lane2); Bg0 = t.Bg0; Bg1 = t.Bg1; SparkCols = new[] { t.Pink, t.Cyan, Color.White, t.Accent, t.Pink, t.Cyan }; if (UiTokens.Custom) { Accent = MixLegibleAccent(UiTokens.Accent); Win.NeonHot = UiTokens.Accent; } }   // (a theme change keeps your profile accent)
    static Color NeonCyan = TM.C(255, 0, 229, 255), NeonPink = TM.C(255, 255, 43, 214), NeonViolet = TM.C(255, 150, 90, 255), NeonLime = Color.FromArgb(120, 255, 120);

    // soft bloom around a rounded rectangle, then a crisp bright edge
    static void Glow(Graphics g, Color c, RectangleF r, float rad, float k, int spread)
    {
        if (k <= 0.01f || r.Width < 4 || r.Height < 4) return;
        if (Prefs.PerfMode) { spread = Math.Min(spread, 2); if (r.Width * r.Height >= 60000) spread = 1; }   // PERFORMANCE MODE: a thin glow, nothing wide
        if (r.Width * r.Height < 60000 && spread <= 6 && Math.Abs(r.Width - Math.Round(r.Width)) < 0.01 + 0.5 && Math.Abs(g.Transform.Elements[4]) < 0.5f)
        {   // small boxes: a stored picture of the glow, copied
            int w = (int)Math.Round(r.Width), h = (int)Math.Round(r.Height), pad = spread * 2 + 3; int kq = Math.Max(1, (int)Math.Round(k * 8));
            long key = Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.Seed, 77), c.ToArgb()), w), h), (long)(rad * 4) * 64 + spread), kq);
            var sp = Sprites.Get(key);
            if (sp == null) { sp = Sprites.Make(w + pad * 2, h + pad * 2); using (var sg = Graphics.FromImage(sp)) { sg.SmoothingMode = SmoothingMode.AntiAlias; GlowDirect(sg, c, new RectangleF(pad, pad, w, h), rad, kq / 8f, spread); } Sprites.Put(key, sp); }
            g.DrawImageUnscaled(sp, (int)Math.Round(r.X) - pad, (int)Math.Round(r.Y) - pad); return;
        }
        GlowDirect(g, c, r, rad, k, spread);
    }
    static void GlowDirect(Graphics g, Color c, RectangleF r, float rad, float k, int spread)
    {
        long t0 = Prof.T();
        bool big = r.Width * r.Height >= 60000; var smWas = g.SmoothingMode; if (big) g.SmoothingMode = SmoothingMode.None;   // a big soft glow does not need smoothed edges (it is much faster without)
        using (var p = RR(r, rad)) for (int i = spread; i >= 1; i--)
        {
            int a = (int)(64 * k / (i * 0.9f)); if (a < 3) continue;
            using (var pen = new Pen(Color.FromArgb(Math.Min(255, a), c), i * 2f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
        }
        g.SmoothingMode = smWas;
        using (var pen = new Pen(Color.FromArgb((int)Math.Min(255, 120 + 135 * k), c), 1.6f)) using (var p = RR(r, rad)) g.DrawPath(pen, p); Prof.A("Glow", t0);
    }

    // a glowing line (playhead, rules)
    static void GlowLine(Graphics g, Color c, float x0, float y0, float x1, float y1, float k)
    {
        for (int i = 4; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(Math.Min(255, (int)(46 * k / (i * 0.8f))), c), i * 2.5f)) g.DrawLine(pen, x0, y0, x1, y1);
        using (var pen = new Pen(Color.FromArgb(240, Color.White), 1.2f)) g.DrawLine(pen, x0, y0, x1, y1);
    }

    // a horizontal neon strip: violet > pink > cyan
    static void NeonRule(Graphics g, float x, float y, float w)
    {
        if (w < 4) return; var r = new RectangleF(x, y - 1, w, 3);
        using (var br = new LinearGradientBrush(new RectangleF(x, y - 3, w, 7), NeonViolet, NeonCyan, 0f))
        {
            var cb = new ColorBlend(3) { Colors = new[] { NeonViolet, NeonPink, NeonCyan }, Positions = new[] { 0f, 0.5f, 1f } }; br.InterpolationColors = cb;
            using (var glow = new LinearGradientBrush(new RectangleF(x, y - 3, w, 7), NeonViolet, NeonCyan, 0f)) { glow.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(50, NeonViolet), Color.FromArgb(50, NeonPink), Color.FromArgb(50, NeonCyan) }, Positions = new[] { 0f, 0.5f, 1f } }; g.FillRectangle(glow, x, y - 4, w, 8); }
            g.FillRectangle(br, r);
        }
    }

    // segmented LED level meter (vertical)
    static void LedMeter(Graphics g, RectangleF r, float frac)
    {
        int n = 16; float seg = r.Height / n;
        for (int i = 0; i < n; i++)
        {
            float f = (i + 1) / (float)n; bool lit = frac >= f - 0.5f / n; var s = new RectangleF(r.X, r.Bottom - (i + 1) * seg + 1, r.Width, seg - 2);
            Color c = f > 0.9f ? TM.C(255, 255, 60, 110) : (f > 0.7f ? Color.FromArgb(255, 200, 40) : Color.FromArgb(0, 255, 150));
            if (lit) { using (var b = new SolidBrush(Color.FromArgb(55, c))) g.FillRectangle(b, s.X - 1.5f, s.Y - 1.5f, s.Width + 3, s.Height + 3); using (var b = new SolidBrush(c)) g.FillRectangle(b, s); }
            else using (var b = new SolidBrush(Color.FromArgb(46, c.R, c.G, c.B))) g.FillRectangle(b, s);
        }
    }

    // a round LED
    static void Led(Graphics g, Color c, float cx, float cy, float rad, bool on)
    {
        if (on) { for (int i = 3; i >= 1; i--) using (var b = new SolidBrush(Color.FromArgb(34, c))) g.FillEllipse(b, cx - rad - i * 2.2f, cy - rad - i * 2.2f, (rad + i * 2.2f) * 2, (rad + i * 2.2f) * 2); }
        using (var b = new SolidBrush(on ? c : Color.FromArgb(70, c))) g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
        if (on) using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, cx - rad * 0.45f, cy - rad * 0.55f, rad * 0.7f, rad * 0.7f);
    }

    // the light seam between a track's header and its lane, plus a bright hairline under the lane
    void NeonLaneChrome(Graphics g, Track tr, RectangleF header, RectangleF lane, bool sel)
    {
        var c = Neon(tr.Color); float live = Math.Max(tr.PeakL, tr.PeakR); float k = (sel ? 0.9f : 0.45f) + Math.Min(0.4f, live * 0.6f);
        if (sel && UiTokens.Custom) using (var pa = new Pen(Color.FromArgb(200, UiTokens.Accent), 1.6f)) g.DrawRectangle(pa, header.X + 1, header.Y + 1, header.Width - 3, header.Height - 3);   // the selection outline in your accent
        for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)(40 * k / i * 1.4f), c), i * 2.2f)) g.DrawLine(pen, lane.X, lane.Y + 2, lane.X, lane.Bottom - 2);
        using (var pen = new Pen(Color.FromArgb((int)(220 * Math.Min(1f, k)), c), 2f)) g.DrawLine(pen, lane.X, lane.Y + 2, lane.X, lane.Bottom - 2);
        using (var pen = new Pen(Color.FromArgb(sel ? 120 : 60, c), 1f)) g.DrawLine(pen, 0, lane.Bottom - 0.5f, lane.Right, lane.Bottom - 0.5f);
        // left LED strip on the header
        using (var b = new SolidBrush(Color.FromArgb(sel ? 255 : 200, c))) g.FillRectangle(b, header.X, header.Y + 3, 4, header.Height - 6);
        using (var b = new SolidBrush(Color.FromArgb(30, c))) g.FillRectangle(b, header.X + 4, header.Y + 3, 5, header.Height - 6);
    }
}

partial class StudioForm
{
}

partial class StudioForm
{
    // ---- moving around the song: an overview bar along the bottom, and the middle mouse button to drag the view ----
    bool panning; Point panStart; long panScroll; float panV; const double MinSpp = 4;
    double MaxSpp { get { return Math.Max(2000.0, eng.EndFrame * 1.15 / Math.Max(300, TimelineRect().Width)); } }   // the most zoomed-out level is the whole song in the window
    double zoomAnchor;

    void Apply(float thumbLeft)
    {
        var tl = TimelineRect(); double total = Math.Max(eng.EndFrame * 1.04, StudioEngine.SR * 10.0); var ob = Rc(HeadW, 0, tl.Width, 1);
        double s = (thumbLeft - ob.X) / ob.Width * total; scroll = (long)Math.Max(0, Math.Min(total - tl.Width * spp, s));
    }

    void MiddleDown(Point p) { panning = true; panStart = p; panScroll = scroll; panV = vscroll; autoFit = false; Cursor = Cursors.SizeAll; }
    void MiddleMove(Point p)
    {
        if (!panning) return; scroll = Math.Max(0, panScroll - (long)((p.X - panStart.X) * spp)); var tl = TimelineRect();
        vscroll = Math.Max(0, Math.Min(Math.Max(0, LaneCount * LaneH - (ClientSize.Height - ToolH - TransH - RulerH - 40)), panV - (p.Y - panStart.Y)));
    }
}

partial class StudioForm
{
    // zoom to an exact level around the middle of the view (used by the zoom slider)
    void ZoomTo(double newSpp)
    {
        autoFit = false; var tl = TimelineRect(); double cur = zoomAnchor > 0 ? zoomAnchor : scroll + tl.Width / 2 * spp; spp = Math.Max(MinSpp, Math.Min(MaxSpp, newSpp)); scroll = Math.Max(0, (long)Math.Min(Math.Max(0, eng.EndFrame * 1.04 - tl.Width * spp), cur - tl.Width / 2 * spp));
    }
    // move the view along the song by a fraction of its width (wheel, touch pad, keys)
    // the tracks up and down by some pixels (the wheel, Up / Down, the bar on the left)
    void ScrollTracks(float by)
    {
        float viewH = MixH - (ArrTop + RulerH) - ArrGap, contentH = LaneCount * LaneH + 50; vscroll = Math.Max(0, Math.Min(Math.Max(0, contentH - viewH), vscroll + by)); Invalidate();
    }
    // the slim up/down bar of the tracks, on the left: always drawn (when the tracks all fit its handle fills it); drag the handle, click above or below it to page
    void TrackScrollBar(Graphics g, RectangleF bar, float contentH, float viewH)
    {
        if (bar.Height < 30) return; float max = Math.Max(0, contentH - viewH);
        using (var b = new SolidBrush(TM.C(255, 12, 6, 24))) g.FillRectangle(b, bar); using (var p = new Pen(Color.FromArgb(120, NeonViolet), 1f)) { g.DrawLine(p, bar.X, bar.Y, bar.X, bar.Bottom); g.DrawLine(p, bar.Right - 1, bar.Y, bar.Right - 1, bar.Bottom); }
        var track = Rc(bar.X + 3, bar.Y + 4, bar.Width - 6, bar.Height - 8); float th = max <= 0 ? track.Height : Math.Max(36, track.Height * viewH / contentH), ty = track.Y + (max <= 0 ? 0 : (track.Height - th) * (vscroll / max));
        var thumb = Rc(track.X, ty, track.Width, th); bool hot = Hot(thumb) || (dragReg != null && dragReg.R == thumb);
        FillRR(g, Color.FromArgb(max <= 0 ? 70 : (hot ? 255 : 200), NeonViolet), thumb, 5); if (max > 0) StrokeRR(g, Color.FromArgb(hot ? 255 : 160, NeonCyan), thumb, 5, 1f);
        var areaR = new HitReg { R = bar, Overlay = true, ScrollBar = true, OnDown = () => { if (max <= 0) return; ScrollTracks(MousePos.Y < ty ? -viewH * 0.9f : viewH * 0.9f); }, OnWheel = d => ScrollTracks(-d * 40) }; regs.Add(areaR);
        float grabDy = 0; var thR = new HitReg { R = thumb, Overlay = true, ScrollBar = true, OnWheel = d => ScrollTracks(-d * 40) };
        thR.OnDown = () => grabDy = MousePos.Y - ty; thR.OnDrag = pt => { if (max <= 0) return; float f = (pt.Y - grabDy - track.Y) / Math.Max(1, track.Height - th); vscroll = Math.Max(0, Math.Min(max, f * max)); Invalidate(); };
        regs.Add(thR); Hint2(bar, max <= 0 ? "The tracks all fit: this bar scrolls them up and down when there are more (so do the wheel and the Up / Down keys)" : "Scroll the tracks up and down: drag the handle, click above or below it, or use the wheel and the Up / Down keys");
    }
    void ScrollView(double screens)
    {
        autoFit = false; if (scrollHold == 0) UserScrolled(); var tl = TimelineRect(); double maxS = Math.Max(0, eng.EndFrame * 1.04 - tl.Width * spp); scroll = (long)Math.Max(0, Math.Min(maxS, scroll + screens * tl.Width * spp));
    }
}

partial class StudioForm
{
    // the add-track button that is always on screen: right after the last track, or pinned to the bottom of the list when the tracks run off the screen
    void AddTrackRow(Graphics g, float afterLastY, int n)
    {
        float by = Math.Min(afterLastY + 6, MixH - 40); var r = Rc(HeadPanelW < 100 ? 4 : 10, by, HeadPanelW < 100 ? HeadPanelW - 8 : HeadPanelW - 20, 34); var gsA = g.Save(); g.ResetClip();   // always on screen: never cut by the list of tracks
        FillRR(g, TM.C(235, 14, 8, 30), r, 8); Glow(g, NeonCyan, r, 8, Hot(r) ? 1.1f : 0.55f, 3);
        Txt(g, r.Width < 140 ? "+" : "+ ADD TRACK", r.Width < 140 ? 18 : 13, Ink, r, StringAlignment.Center, true); Reg(r, () => OpenNewTrackSheet(2)); g.Restore(gsA);
    }
    // the + TRACK button in the toolbar: choose what to add and where it goes
    void ShowAddTrackMenuMain() { OpenNewTrackSheet(0); }
}
