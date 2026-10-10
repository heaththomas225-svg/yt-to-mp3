using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Neon look for Purphex Music: glowing outlines, LED meters and light strips in each track's colour.
partial class StudioForm
{
    static readonly Dictionary<int, Color> neonCache = new Dictionary<int, Color>();
    // any stored track colour is pushed to full-bright neon so old projects glow too
    static Color Neon(int argb)
    {
        Color r; if (neonCache.TryGetValue(argb, out r)) return r;
        var c = Color.FromArgb(argb); double R = c.R / 255.0, G = c.G / 255.0, B = c.B / 255.0, mx = Math.Max(R, Math.Max(G, B)), mn = Math.Min(R, Math.Min(G, B));
        if (mx <= 0.001) { r = Color.FromArgb(0, 229, 255); }
        else
        {
            double s = Math.Max((mx - mn) / mx, 0.78); double v = 1.0;
            double h = c.GetHue(); double cc = v * s, x = cc * (1 - Math.Abs((h / 60.0) % 2 - 1)), m = v - cc; double rr, gg, bb;
            if (h < 60) { rr = cc; gg = x; bb = 0; } else if (h < 120) { rr = x; gg = cc; bb = 0; } else if (h < 180) { rr = 0; gg = cc; bb = x; } else if (h < 240) { rr = 0; gg = x; bb = cc; } else if (h < 300) { rr = x; gg = 0; bb = cc; } else { rr = cc; gg = 0; bb = x; }
            r = Color.FromArgb((int)((rr + m) * 255), (int)((gg + m) * 255), (int)((bb + m) * 255));
        }
        neonCache[argb] = r; return r;
    }
    static readonly Color NeonCyan = Color.FromArgb(0, 229, 255), NeonPink = Color.FromArgb(255, 43, 214), NeonViolet = Color.FromArgb(150, 90, 255), NeonLime = Color.FromArgb(120, 255, 120);

    // soft bloom around a rounded rectangle, then a crisp bright edge
    static void Glow(Graphics g, Color c, RectangleF r, float rad, float k, int spread)
    {
        if (k <= 0.01f || r.Width < 4 || r.Height < 4) return;
        for (int i = spread; i >= 1; i--)
        {
            int a = (int)(64 * k / (i * 0.9f)); if (a < 3) continue;
            using (var pen = new Pen(Color.FromArgb(Math.Min(255, a), c), i * 2f)) { pen.LineJoin = LineJoin.Round; using (var p = RR(r, rad)) g.DrawPath(pen, p); }
        }
        using (var pen = new Pen(Color.FromArgb((int)Math.Min(255, 120 + 135 * k), c), 1.6f)) using (var p = RR(r, rad)) g.DrawPath(pen, p);
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
            Color c = f > 0.9f ? Color.FromArgb(255, 60, 110) : (f > 0.7f ? Color.FromArgb(255, 200, 40) : Color.FromArgb(0, 255, 150));
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
    // A thin strip on the line between two tracks (and above the first): hover it and press + ADD TRACK to put a new track right there.
    void AddTrackZone(Graphics g, float y, int i)
    {
        float yy = i == 0 ? y : y;   // the strip sits on the top edge of this track, so every track gets a "above" strip; the last also gets "below"
        var above = Rc(0, y - 8, HeadW, 16); AddTrackStrip(g, above, y, i);
        if (i == eng.Tracks.Count - 1) { var below = Rc(0, y + LaneH - 8, HeadW, 16); AddTrackStrip(g, below, y + LaneH, i + 1); }
    }
    void AddTrackStrip(Graphics g, RectangleF zone, float lineY, int insertIndex)
    {
        bool hot = Hot(zone); var pill = Rc(HeadW / 2f - 62, lineY - 10, 124, 20);
        if (hot)
        {
            FillRR(g, Color.FromArgb(235, 14, 8, 30), pill, 10); Glow(g, NeonCyan, pill, 10, 0.9f, 3); Txt(g, "+ ADD TRACK HERE", 10, Ink, pill, StringAlignment.Center, true);
            bubble = "Add a track here: audio from your PC, or an instrument such as Bass Lab."; bubblePt = MousePos;
        }
        else { var dot = Rc(HeadW / 2f - 8, lineY - 8, 16, 16); using (var b = new SolidBrush(Color.FromArgb(215, 12, 8, 28))) g.FillEllipse(b, dot); using (var pen = new Pen(Color.FromArgb(190, NeonCyan), 1.4f)) { g.DrawEllipse(pen, dot); g.DrawLine(pen, dot.X + 4, dot.Y + 8, dot.Right - 4, dot.Y + 8); g.DrawLine(pen, dot.X + 8, dot.Y + 4, dot.X + 8, dot.Bottom - 4); } }
        int at = insertIndex; Reg(zone, () => ShowAddTrackMenu(at));
    }
    void ShowAddTrackMenu(int at)
    {
        var m = Win.NewMenu();
        m.Items.Add("Add music / audio file here...").Click += (a, b) => { nextInsert = at; AddMusicClick(true); };
        m.Items.Add("Add an instrument here (Bass Lab, Vital...)...").Click += (a, b) => { nextInsert = at; AddInstrumentClick(true); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
}

partial class StudioForm
{
    // ---- moving around the song: an overview bar along the bottom, and the middle mouse button to drag the view ----
    bool panning; Point panStart; long panScroll; float panV; const double MinSpp = 4;
    double MaxSpp { get { return Math.Max(2000.0, eng.EndFrame * 1.15 / Math.Max(300, TimelineRect().Width)); } }   // the most zoomed-out level is the whole song in the window
    double zoomAnchor;

    // The bar shows the whole song. The lit window is what you see: drag it to move, drag its ends to zoom, click outside it to jump, double-click to fit the whole song.
    void PaintOverview(Graphics g, RectangleF tl)
    {
        var ob = Rc(HeadW, ClientSize.Height - 20, tl.Width, 16); double total = Math.Max(eng.EndFrame * 1.04, StudioEngine.SR * 10.0); float tw = ob.Width;
        FillRR(g, Color.FromArgb(235, 9, 5, 20), ob, 6); StrokeRR(g, Color.FromArgb(90, NeonViolet), ob, 6, 1);
        int n = eng.Tracks.Count; if (n > 0)
            for (int i = 0; i < n; i++)
            {
                var tr = eng.Tracks[i]; float a = ob.X + (float)(Math.Max(0, tr.Offset) / total * tw), b = ob.X + (float)(Math.Min(total, tr.Offset + Math.Max(1, tr.UsedLength)) / total * tw), th = (ob.Height - 6f) / n;
                using (var br = new SolidBrush(Color.FromArgb(150, Neon(tr.Color)))) g.FillRectangle(br, a, ob.Y + 3 + i * th, Math.Max(2, b - a), Math.Max(1.5f, th - 1));
            }
        double win = tl.Width * spp; float x0 = ob.X + (float)(scroll / total * tw), ww = Math.Max(14f, (float)(win / total * tw)); x0 = Math.Max(ob.X, Math.Min(ob.Right - ww, x0));
        var thumb = Rc(x0, ob.Y + 1, ww, ob.Height - 2); using (var br = new SolidBrush(Color.FromArgb(46, NeonCyan))) g.FillRectangle(br, thumb); Glow(g, NeonCyan, thumb, 5, 0.7f, 2);
        using (var br = new SolidBrush(Color.FromArgb(220, NeonCyan))) { g.FillRectangle(br, thumb.X, thumb.Y + 2, 3, thumb.Height - 4); g.FillRectangle(br, thumb.Right - 3, thumb.Y + 2, 3, thumb.Height - 4); }
        int mode = 0; double startScroll = scroll, startSpp = spp; float grab = 0;
        var rg = Reg(ob, () =>
        {
            autoFit = false; float mx = MousePos.X; startScroll = scroll; startSpp = spp;
            if (Math.Abs(mx - thumb.X) < 8) mode = 1; else if (Math.Abs(mx - thumb.Right) < 8) mode = 2; else { mode = 0; grab = mx >= thumb.X && mx <= thumb.Right ? mx - thumb.X : thumb.Width / 2; if (mx < thumb.X || mx > thumb.Right) Apply(mx - grab); }
        });
        rg.OnDouble = () => FitView();
        rg.OnDrag = pt =>
        {
            float mx = pt.X;
            if (mode == 0) Apply(mx - grab);
            else if (mode == 2) { double w = Math.Max(30, mx - thumb.X) / tw * total; spp = Math.Max(MinSpp, Math.Min(MaxSpp, w / tl.Width)); }
            else { double right = startScroll + tl.Width * startSpp; double nl = Math.Max(0, Math.Min(right - tl.Width * MinSpp, (mx - ob.X) / tw * total)); spp = Math.Max(MinSpp, Math.Min(MaxSpp, (right - nl) / tl.Width)); scroll = (long)Math.Max(0, right - tl.Width * spp); }
        };
    }
    void Apply(float thumbLeft)
    {
        var tl = TimelineRect(); double total = Math.Max(eng.EndFrame * 1.04, StudioEngine.SR * 10.0); var ob = Rc(HeadW, 0, tl.Width, 1);
        double s = (thumbLeft - ob.X) / ob.Width * total; scroll = (long)Math.Max(0, Math.Min(total - tl.Width * spp, s));
    }

    void MiddleDown(Point p) { panning = true; panStart = p; panScroll = scroll; panV = vscroll; autoFit = false; Cursor = Cursors.SizeAll; }
    void MiddleMove(Point p)
    {
        if (!panning) return; scroll = Math.Max(0, panScroll - (long)((p.X - panStart.X) * spp)); var tl = TimelineRect();
        vscroll = Math.Max(0, Math.Min(Math.Max(0, eng.Tracks.Count * LaneH - (ClientSize.Height - ToolH - TransH - RulerH - 40)), panV - (p.Y - panStart.Y)));
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
    void ScrollView(double screens)
    {
        autoFit = false; var tl = TimelineRect(); double maxS = Math.Max(0, eng.EndFrame * 1.04 - tl.Width * spp); scroll = (long)Math.Max(0, Math.Min(maxS, scroll + screens * tl.Width * spp));
    }
}

partial class StudioForm
{
    // the add-track button that is always on screen: right after the last track, or pinned to the bottom of the list when the tracks run off the screen
    void AddTrackRow(Graphics g, float afterLastY, int n)
    {
        float by = Math.Min(afterLastY + 6, ClientSize.Height - 44); var r = Rc(10, by, HeadW - 20, 34);
        FillRR(g, Color.FromArgb(235, 14, 8, 30), r, 8); Glow(g, NeonCyan, r, 8, Hot(r) ? 1.1f : 0.55f, 3);
        Txt(g, "+ ADD TRACK", 13, Ink, r, StringAlignment.Center, true); int at = n; Reg(r, () => ShowAddTrackMenu(at));
    }
    // the + TRACK button in the toolbar: choose what to add and where it goes
    void ShowAddTrackMenuMain()
    {
        var m = Win.NewMenu(); int n = eng.Tracks.Count, sel = n == 0 ? 0 : Math.Max(0, Math.Min(selTrack, n - 1));
        string[] where = n == 0 ? new[] { "" } : new[] { "below the selected track", "above the selected track", "at the end" }; int[] idx = n == 0 ? new[] { 0 } : new[] { sel + 1, sel, n };
        for (int i = 0; i < where.Length; i++) { int at = idx[i]; m.Items.Add("Add music / audio file " + where[i] + "...").Click += (a, b) => { nextInsert = at; AddMusicClick(true); }; }
        m.Items.Add(new ToolStripSeparator());
        for (int i = 0; i < where.Length; i++) { int at = idx[i]; m.Items.Add("Add an instrument " + where[i] + "...").Click += (a, b) => { nextInsert = at; AddInstrumentClick(true); }; }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
}
