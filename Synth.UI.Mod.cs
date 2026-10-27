using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Modulation on the knobs: drag a source chip (LFO, envelope, macro) onto any knob that can be modulated.
// A ring around the knob shows how far the source pushes it; Ctrl + drag on the knob changes that depth.
partial class SynthCanvas
{
    int dragSrc; string dragName = ""; string dropMsg = ""; DateTime dropMsgAt = DateTime.MinValue;
    static readonly Color[] SrcCols = { Color.White, Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(255, 190, 90), Color.FromArgb(255, 190, 90), Color.FromArgb(255, 190, 90), TM.C(255, 120, 200, 255), TM.C(255, 120, 200, 255), TM.C(255, 255, 120, 180), TM.C(255, 255, 120, 180), TM.C(255, 255, 120, 180), TM.C(255, 255, 120, 180) };

    static int DestOf(string paramId) { for (int i = 1; i < SynthTables.DestParam.Length; i++) if (SynthTables.DestParam[i] == paramId) return i; return 0; }

    // matrix slots (1..8) that are switched on and aimed at this knob
    List<int> SlotsOn(int dest)
    {
        var l = new List<int>(); if (dest <= 0) return l;
        for (int i = 1; i <= 16; i++) if ((int)P("m" + i + "d").Val == dest && (int)P("m" + i + "s").Val != 0 && P("m" + i + "e").Val > 0.5) l.Add(i);
        return l;
    }
    static float RingScale(int dest) { return dest == 1 || dest == 17 || dest == 20 || (dest >= 27 && dest <= 29) ? 0.5f : 1f; }

    void DrawModRing(Graphics g, RectangleF ar, float f, List<int> slots, int dest)
    {
        var ring = RectangleF.Inflate(ar, 4.5f, 4.5f);
        foreach (int s in slots)
        {
            int src = (int)P("m" + s + "s").Val; double lo, hi, tip; ModRange(s, f, dest, out lo, out hi, out tip); if (hi - lo < 0.004) continue;
            using (var pen = new Pen(Color.FromArgb(220, SrcCols[Math.Min(src, SrcCols.Length - 1)]), 2.2f)) g.DrawArc(pen, ring, 135 + (float)lo * 270f, (float)(hi - lo) * 270f);
        }
    }

    // where slot s moves a knob that sits at f (0..1): the two ends of its sweep, as the coloured ring draws them; tip = the end the amount points to
    void ModRange(int s, float f, int dest, out double lo, out double hi, out double tip)
    {
        int src = (int)P("m" + s + "s").Val; double amt = P("m" + s + "a").Val / 100.0 * RingScale(dest); int pol = (int)P("m" + s + "p").Val; bool bipolarSrc = src <= 4 || src == 9;
        if (pol == 0 && bipolarSrc) { lo = f - Math.Abs(amt); hi = f + Math.Abs(amt); tip = hi; } else { double a = amt * (pol == 2 ? -1 : 1); lo = Math.Min(f, f + a); hi = Math.Max(f, f + a); tip = f + a; }
        lo = Math.Max(0, lo); hi = Math.Min(1, hi); tip = Math.Max(0, Math.Min(1, tip));
    }

    // on a graph: every source aimed at this param shows its sweep as a band in its colour, with a dot at the end you drag to set how much (like Vital)
    void ModOnGraph(Graphics g, RectangleF r, string paramId, float baseY)
    {
        int dest = DestOf(paramId); var p = P(paramId); if (dest == 0 || p == null) return; var sl = SlotsOn(dest); if (sl.Count == 0) return;
        float f = Math.Max(0, Math.Min(1, FracOf(p, IsLog(p)))), x0 = r.X + 4, w = r.Width - 8, xf = x0 + w * f; int row = 0; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (int s in sl)
        {
            double lo, hi, tip; ModRange(s, f, dest, out lo, out hi, out tip); int src = (int)P("m" + s + "s").Val; Color sc = SrcCols[Math.Min(src, SrcCols.Length - 1)];
            float xl = x0 + w * (float)lo, xh = x0 + w * (float)hi, xt = x0 + w * (float)tip, hy = baseY - row * 14;
            using (var b = new SolidBrush(Color.FromArgb(40, sc))) g.FillRectangle(b, xl, r.Y + 2, Math.Max(1, xh - xl), r.Height - 4);
            using (var pen = new Pen(Color.FromArgb(170, sc), 1.2f) { DashPattern = new[] { 3f, 2f } }) { g.DrawLine(pen, xl, r.Y + 3, xl, r.Bottom - 3); g.DrawLine(pen, xh, r.Y + 3, xh, r.Bottom - 3); }
            using (var pen = new Pen(Color.FromArgb(230, sc), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, xf, hy, xt, hy);   // from where the knob is to how far the source pushes it
            var hr = Rc(xt - 8, hy - 8, 16, 16); bool hot = Hot(hr); float d = hot ? 14 : 10;
            using (var b = new SolidBrush(Color.FromArgb(hot ? 90 : 45, sc))) g.FillEllipse(b, xt - d, hy - d, d * 2, d * 2);
            using (var b = new SolidBrush(sc)) g.FillEllipse(b, xt - d / 2, hy - d / 2, d, d); using (var pen = new Pen(Color.White, 1.5f)) g.DrawEllipse(pen, xt - d / 2, hy - d / 2, d, d);
            string nm = SynthTables.Sources[Math.Min(src, SynthTables.Sources.Length - 1)].ToUpperInvariant() + " " + (int)Math.Round(P("m" + s + "a").Val) + "%"; bool left = xt > r.X + r.Width / 2;
            float lw = nm.Length * 5.4f + 8; var lr = Rc(left ? xt - 10 - lw : xt + 10, hy - 7, lw, 14); Fill(g, TM.C(200, 12, 5, 28), lr, 4); Txt(g, nm, 9, Ink, lr, StringAlignment.Center, true);   // on a dark tag so it reads over the curve
            var rg = Add(hr); rg.P = p; rg.Tip = nm + " moves " + p.Name + ": drag the dot left / right to set how much. Right-click removes it.";
            int ss = s, pol = (int)P("m" + s + "p").Val; bool sym = pol == 0 && (src <= 4 || src == 9); float scl = RingScale(dest), sign = P("m" + s + "a").Val < 0 ? -1 : 1;
            rg.Down = pt => { }; rg.Drag = (dx, dy) => { double t = Math.Max(0, Math.Min(1, (mouse.X - x0) / w)), a = (t - f) / scl * 100; if (sym) a = Math.Abs(a) * sign; else if (pol == 2) a = -a; SetVal(P("m" + ss + "a"), (float)Math.Round(Math.Max(-100, Math.Min(100, a)))); };
            rg.Right = () => SetVal(P("m" + ss + "s"), 0);
            row++;
        }
        g.SmoothingMode = sm;
    }
    // the dot on a graph you grab to change it
    void GraphDot(Graphics g, float x, float y, Color c, bool hot)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; float d = hot ? 13 : 10;
        using (var b = new SolidBrush(Color.FromArgb(hot ? 100 : 50, c))) g.FillEllipse(b, x - d, y - d, d * 2, d * 2);
        using (var b = new SolidBrush(hot ? Color.White : c)) g.FillEllipse(b, x - d / 2, y - d / 2, d, d); using (var pen = new Pen(Color.White, 1.5f)) g.DrawEllipse(pen, x - d / 2, y - d / 2, d, d);
        g.SmoothingMode = sm;
    }

    // a small handle you grab and drop on a knob
    void SourceChip(Graphics g, RectangleF r, int src, string name)
    {
        bool hot = Hot(r) || dragSrc == src; Color sc = SrcCols[Math.Min(src, SrcCols.Length - 1)];
        float d = Math.Min(r.Height + 8, 30); if (r.Width < 60) { DragOrb(g, r.X + r.Width / 2, r.Y + r.Height / 2, Math.Min(d, r.Width), sc, hot); }
        else { DragOrb(g, r.X + d / 2, r.Y + r.Height / 2, d, sc, hot); Txt(g, "DRAG ONTO A KNOB", 10, hot ? Ink : Dim, Rc(r.X + d + 4, r.Y, r.Width - d - 4, r.Height), StringAlignment.Near, true); }
        var rg = Add(r); rg.Tip = "Drag this onto any knob to make " + name + " move it. Drag on the knob with Ctrl held to set how much."; rg.Down = pt => { dragSrc = src; dragName = name; }; rg.Drag = (dx, dy) => { };
    }

    // the handle of a modulation source: a glowing circle with arrows, pulsing so you can see it can be picked up (like Vital)
    void DragOrb(Graphics g, float cx, float cy, float d, Color c, bool hot)
    {
        float pulse = (float)(0.5 + 0.5 * Math.Sin(Environment.TickCount / 220.0)); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int k = 3; k >= 1; k--) using (var gb = new SolidBrush(Color.FromArgb((int)((hot ? 40 : 18 + 18 * pulse) / k * 1.6f), c))) g.FillEllipse(gb, cx - d / 2 - k * 3, cy - d / 2 - k * 3, d + k * 6, d + k * 6);
        using (var pth = new GraphicsPath()) { pth.AddEllipse(cx - d / 2, cy - d / 2, d, d); using (var pb = new PathGradientBrush(pth) { CenterPoint = new PointF(cx - d * 0.15f, cy - d * 0.2f), CenterColor = Color.FromArgb(255, Math.Min(255, c.R / 2 + 120), Math.Min(255, c.G / 2 + 120), Math.Min(255, c.B / 2 + 120)), SurroundColors = new[] { Color.FromArgb(255, c.R / 3, c.G / 3, c.B / 3) } }) g.FillPath(pb, pth); }
        using (var pen = new Pen(hot ? Color.White : c, 1.6f)) g.DrawEllipse(pen, cx - d / 2, cy - d / 2, d, d);
        float a = d * 0.28f, h = d * 0.1f; using (var pen = new Pen(Color.FromArgb(240, 255, 255, 255), Math.Max(1.2f, d / 16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {   // four arrows: it moves
            g.DrawLine(pen, cx - a, cy, cx + a, cy); g.DrawLine(pen, cx, cy - a, cx, cy + a);
            g.DrawLine(pen, cx - a, cy, cx - a + h, cy - h); g.DrawLine(pen, cx - a, cy, cx - a + h, cy + h); g.DrawLine(pen, cx + a, cy, cx + a - h, cy - h); g.DrawLine(pen, cx + a, cy, cx + a - h, cy + h);
            g.DrawLine(pen, cx, cy - a, cx - h, cy - a + h); g.DrawLine(pen, cx, cy - a, cx + h, cy - a + h); g.DrawLine(pen, cx, cy + a, cx - h, cy + a - h); g.DrawLine(pen, cx, cy + a, cx + h, cy + a - h);
        }
        g.SmoothingMode = sm;
    }
    // a display you can drop a source on (the filter's curve moves its cutoff, an oscillator's wave its wave position), like Vital
    readonly List<KeyValuePair<RectangleF, string>> dispTargets = new List<KeyValuePair<RectangleF, string>>();
    void DisplayTarget(Graphics g, RectangleF r, string paramId)
    {
        dispTargets.Add(new KeyValuePair<RectangleF, string>(r, paramId)); if (dragSrc == 0) return;
        bool over = r.Contains(mouse); Color sc = SrcCols[Math.Min(dragSrc, SrcCols.Length - 1)]; float pulse = (float)(0.5 + 0.5 * Math.Sin(Environment.TickCount / 160.0));
        using (var b = new SolidBrush(Color.FromArgb(over ? 70 : 30, sc))) g.FillRectangle(b, r);
        using (var pen = new Pen(Color.FromArgb(over ? 255 : (int)(140 + 100 * pulse), sc), over ? 2.4f : 1.5f) { DashPattern = over ? new[] { 1f } : new[] { 4f, 3f } }) g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        float d = Math.Min(30, r.Height * 0.45f); var c = Rc(r.X + r.Width / 2 - d / 2, r.Y + r.Height / 2 - d / 2, d, d);
        using (var b = new SolidBrush(Color.FromArgb(over ? 200 : 120, sc))) g.FillEllipse(b, c); using (var pen = new Pen(Color.White, 1.6f)) g.DrawEllipse(pen, c);
        var p = P(paramId); if (p != null) Txt(g, "DROP: " + p.Name.ToUpperInvariant(), 10, Ink, Rc(r.X, c.Bottom + 2, r.Width, 14), StringAlignment.Center, true);
    }
    Reg DisplayTargetAt(PointF pt) { foreach (var kv in dispTargets) if (kv.Key.Contains(pt)) { var p = P(kv.Value); if (p != null) return new Reg { R = kv.Key, P = p }; } return null; }
    void DropOnKnob(Reg target)
    {
        int src = dragSrc; dragSrc = 0; if (target == null || target.P == null) { dropMsg = "Drop it on a knob."; dropMsgAt = DateTime.Now; return; }
        int dest = DestOf(target.P.Id); if (dest == 0) { dropMsg = target.P.Name + " can't be modulated yet. Use the MATRIX page for the full list."; dropMsgAt = DateTime.Now; return; }
        int slot = -1; for (int i = 1; i <= 16; i++) if ((int)P("m" + i + "s").Val == src && (int)P("m" + i + "d").Val == dest) { slot = i; break; }
        if (slot < 0) for (int i = 1; i <= 16; i++) if ((int)P("m" + i + "s").Val == 0 || (int)P("m" + i + "d").Val == 0) { slot = i; break; }
        if (slot < 0) { dropMsg = "All 8 matrix slots are in use. Remove one on the MATRIX page."; dropMsgAt = DateTime.Now; return; }
        SetVal(P("m" + slot + "s"), src); SetVal(P("m" + slot + "d"), dest); SetVal(P("m" + slot + "e"), 1); if (Math.Abs(P("m" + slot + "a").Val) < 1) SetVal(P("m" + slot + "a"), 50);
        dropMsg = dragName + " now moves " + target.P.Name + ". Drag the dot on the graph or the ring round the knob to set how much."; dropMsgAt = DateTime.Now;
    }

    void PaintDragGhost(Graphics g)
    {
        if (dragSrc != 0)
        {
            var h = HitTest(mouse); bool ok = h != null && h.P != null && DestOf(h.P.Id) != 0;
            if (h != null && h.P != null && !ok && Math.Abs(h.R.Width - 56) < 0.5f) { float kr = (h.R.Height - 24) / 2 + 9, kx = h.R.X + h.R.Width / 2, ky = h.R.Y + 4 + (h.R.Height - 24) / 2; using (var pen = new Pen(Color.FromArgb(255, 120, 130), 2f)) g.DrawEllipse(pen, kx - kr, ky - kr, kr * 2, kr * 2); }   // it cannot go here: a red ring centred on the knob (a knob it can go on lights up by itself)
            var r = Rc(mouse.X + 12, mouse.Y + 10, 150, 20); Fill(g, TM.C(230, 40, 20, 80), r, 6); Stroke(g, SrcCols[Math.Min(dragSrc, SrcCols.Length - 1)], r, 6, 1.5f); Txt(g, dragName + (ok ? "  > drop" : ""), 11, Ink, r, StringAlignment.Center, true);
        }
        else if ((DateTime.Now - dropMsgAt).TotalSeconds < 4 && dropMsg.Length > 0) { var r = Rc(8, H - 44, W - 16, 20); Fill(g, TM.C(230, 40, 20, 80), r, 6); Txt(g, dropMsg, 11, Good, r, StringAlignment.Center, true); }
    }
}

partial class SynthCanvas
{
    // drop-down lists close by themselves: when you pick something, click elsewhere, leave the list alone for a moment, or switch windows
    void ShowMenu(ContextMenuStrip m, Point at)
    {
        var form = FindForm(); var t = new Timer { Interval = 900 }; EventHandler deact = (s, e) => { try { m.Close(); } catch { } };
        t.Tick += (s, e) => { if (!m.Bounds.Contains(Cursor.Position)) { t.Stop(); try { m.Close(); } catch { } } };
        m.MouseLeave += (s, e) => { t.Stop(); t.Start(); }; m.MouseEnter += (s, e) => t.Stop();
        if (form != null) form.Deactivate += deact;
        m.Closed += (s, e) => { t.Stop(); t.Dispose(); if (form != null) form.Deactivate -= deact; Poke(); };
        m.Show(this, at);
    }
}
