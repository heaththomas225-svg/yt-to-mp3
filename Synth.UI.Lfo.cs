using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The LFO page: a shape you can draw. Click empty space to add a point, drag a point to move it, right-click a point to delete it,
// double-click a point to make it round (smooth), drag the little diamond between two points to bend that stretch of the line.
partial class SynthCanvas
{
    LfoPt dragPt; LfoPt bendPt; float bendC0, bendDownY; bool lfoSnap;
    static readonly string[] BuiltIn = { "Sine", "Triangle", "Saw up", "Saw down", "Square", "Random steps", "Wobble 1" };

    RectangleF LfoInner(RectangleF disp) { return Rc(disp.X + 12, disp.Y + 24, disp.Width - 24, disp.Height - 38); }
    PointF LfoPx(RectangleF ir, float x, float y) { return new PointF(ir.X + x * ir.Width, ir.Y + (1 - y) / 2f * ir.Height); }
    float LfoSnapX(float x) { return lfoSnap ? (float)Math.Round(x * 16) / 16f : x; }
    float LfoSnapY(float y) { return lfoSnap ? (float)Math.Round(y * 4) / 4f : y; }

    // make the drawn curve the active shape; if a built-in shape was showing, start from a copy of it
    LfoCurve LfoMakeCustom(int k)
    {
        var c = fx.LfoCurves[k - 1]; int shape = (int)P("l" + k + "shape").Val;
        if (shape != 7) { c.Load(shape < BuiltIn.Length ? BuiltIn[shape] : "Sine"); SetVal(P("l" + k + "shape"), 7); }
        return c;
    }
    LfoPt LfoNearest(LfoCurve c, RectangleF ir, PointF m, float rad)
    {
        LfoPt best = null; float bd = rad * rad;
        foreach (var p in c.Pts) { var q = LfoPx(ir, p.X, p.Y); float d = (q.X - m.X) * (q.X - m.X) + (q.Y - m.Y) * (q.Y - m.Y); if (d <= bd) { bd = d; best = p; } }
        return best;
    }
    LfoPt LfoNearestBend(LfoCurve c, RectangleF ir, PointF m)
    {
        for (int i = 0; i < c.Pts.Count - 1; i++)
        {
            var a = c.Pts[i]; var b = c.Pts[i + 1]; if (b.X - a.X < 0.03f) continue;
            float mx = (a.X + b.X) / 2; var q = LfoPx(ir, mx, c.Eval(mx)); if (Math.Abs(q.X - m.X) < 8 && Math.Abs(q.Y - m.Y) < 8) return a;
        }
        return null;
    }

    void PaintLfo(Graphics g, RectangleF pn, int k)
    {
        var disp = Rc(pn.X + 12, pn.Y + 12, 440, 250); Fill(g, Disp, disp, 6); var ir = LfoInner(disp); var cur = fx.LfoCurves[k - 1];
        int shape = (int)P("l" + k + "shape").Val; bool uni = P("l" + k + "uni").Val > 0.5f; double sm = P("l" + k + "smooth").Val / 100.0;
        int sy = (int)P("l" + k + "sync").Val; double bpm = Fx.ProjectBpm, hz = fx.LfoHzUi(k - 1); bool custom = shape == 7;
        // grid
        using (var pen = new Pen(TM.C(34, 150, 130, 200))) { for (int i = 0; i <= 16; i++) { float x = ir.X + ir.Width * i / 16f; if (i % 4 == 0) continue; g.DrawLine(pen, x, ir.Y, x, ir.Bottom); } for (int i = 1; i < 8; i++) { float y = ir.Y + ir.Height * i / 8f; if (i == 4) continue; g.DrawLine(pen, ir.X, y, ir.Right, y); } }
        using (var pen = new Pen(TM.C(70, 150, 130, 200))) { for (int i = 0; i <= 4; i++) { float x = ir.X + ir.Width * i / 4f; g.DrawLine(pen, x, ir.Y, x, ir.Bottom); } g.DrawLine(pen, ir.X, ir.Y + ir.Height / 2, ir.Right, ir.Y + ir.Height / 2); g.DrawRectangle(pen, ir.X, ir.Y, ir.Width, ir.Height); }
        Txt(g, uni ? "1" : "+1", 9, Dim, Rc(ir.X + 2, ir.Y + 1, 30, 12), StringAlignment.Near, false); Txt(g, uni ? "0" : "-1", 9, Dim, Rc(ir.X + 2, ir.Bottom - 13, 30, 12), StringAlignment.Near, false);
        // what the audio does: the shape, run through the same smoothing (second cycle shown so the smoothing has settled)
        int N = 240; var pts = new List<PointF>(); double dtc = 1.0 / (Math.Max(0.01, hz) * N), a = sm > 0 ? 1 - Math.Exp(-dtc / (0.002 + sm * 0.15)) : 1, last = 0; var rnd = new Random(k * 31); double hold = rnd.NextDouble() * 2 - 1;
        for (int c = 0; c < 2; c++) for (int i = 0; i <= N; i++)
        {
            double p = i / (double)N; if (i == 0 && c == 1) hold = rnd.NextDouble() * 2 - 1;
            double y = SynthFx.LfoShape(shape, Math.Min(p, 0.99999), hold, cur); if (c == 0 && i == 0) last = y; last += (y - last) * a;
            if (c == 1) pts.Add(LfoPx(ir, (float)p, (float)Math.Max(-1, Math.Min(1, last))));
        }
        using (var pen = new Pen(Good, 2.5f)) { var sv = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; g.DrawLines(pen, pts.ToArray()); g.SmoothingMode = sv; }
        // playback marker (follows the real LFO phase)
        double ph = fx.LfoPhaseUi(k - 1); float mx0 = ir.X + (float)ph * ir.Width; var mp = LfoPx(ir, (float)ph, SynthFx.LfoShape(shape, Math.Min(ph, 0.99999), hold, cur) * 1f > 1 ? 1f : (float)SynthFx.LfoShape(shape, Math.Min(ph, 0.99999), hold, cur));
        if (fx.ActiveVoices > 0) { using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawLine(pen, mx0, ir.Y, mx0, ir.Bottom); using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, mp.X - 4, mp.Y - 4, 8, 8); }
        // control points and bend handles (drawn shape only)
        if (custom)
        {
            for (int i = 0; i < cur.Pts.Count - 1; i++) { var p0 = cur.Pts[i]; var p1 = cur.Pts[i + 1]; if (p1.X - p0.X < 0.03f) continue; float mx = (p0.X + p1.X) / 2; var q = LfoPx(ir, mx, cur.Eval(mx)); using (var pen = new Pen(Color.FromArgb(200, Acc2), 1.2f)) g.DrawPolygon(pen, new[] { new PointF(q.X, q.Y - 4), new PointF(q.X + 4, q.Y), new PointF(q.X, q.Y + 4), new PointF(q.X - 4, q.Y) }); }
            foreach (var p in cur.Pts) { var q = LfoPx(ir, p.X, p.Y); bool sel = p == dragPt; using (var b = new SolidBrush(sel ? Color.White : (p.Smooth ? Acc2 : Acc))) g.FillEllipse(b, q.X - 5, q.Y - 5, 10, 10); using (var pen = new Pen(Color.White, 1f)) g.DrawEllipse(pen, q.X - 5, q.Y - 5, 10, 10); }
        }
        Txt(g, "LFO " + k + ":  " + SynthTables.LfoShapes[shape] + ",  " + (sy == 0 ? hz.ToString("0.00") + " Hz" : SynthTables.Syncs[sy] + " note  =  " + hz.ToString("0.00") + " Hz at " + bpm.ToString("0") + " BPM"), 11, Ink, Rc(disp.X + 10, disp.Y + 4, disp.Width - 20, 16), StringAlignment.Near, true);
        Txt(g, custom ? "Click: add. Drag: move. Right-click: delete. Double-click: round. Diamond: bend." : "Click or drag on the picture to draw your own shape from this one.", 10, Dim, Rc(disp.X + 10, disp.Bottom - 15, disp.Width - 20, 14), StringAlignment.Near, false);
        var rg = Add(disp); rg.Tip = "Draw your LFO shape. What you see here is what moves the sound.";
        rg.Down = pt =>
        {
            var c = LfoMakeCustom(k); var ird = LfoInner(disp); var m = mouse; dragPt = null; bendPt = null;
            var hit = LfoNearest(c, ird, m, 10); if (hit != null) { dragPt = hit; return; }
            var bd = LfoNearestBend(c, ird, m); if (bd != null) { bendPt = bd; bendC0 = bd.C; bendDownY = m.Y; return; }
            if (!ird.Contains(m)) return;
            float x = LfoSnapX(Math.Max(0, Math.Min(1, (m.X - ird.X) / ird.Width))), y = LfoSnapY(Math.Max(-1, Math.Min(1, 1 - 2 * (m.Y - ird.Y) / ird.Height)));
            var np = new LfoPt(x, y); if (x <= 0.001f || x >= 0.999f) { c.Rebuild(); return; }
            c.Pts.Add(np); c.Rebuild(); dragPt = np; modified = true;
        };
        rg.Drag = (dx, dy) =>
        {
            var c = fx.LfoCurves[k - 1]; var ird = LfoInner(disp); var m = mouse;
            if (bendPt != null) { int i = c.Pts.IndexOf(bendPt); if (i >= 0 && i < c.Pts.Count - 1) { float dir = c.Pts[i + 1].Y >= bendPt.Y ? 1 : -1; bendPt.C = Math.Max(-1, Math.Min(1, bendC0 + dir * (m.Y - bendDownY) / 70f)); c.Rebuild(); modified = true; } return; }
            if (dragPt == null) return; int idx = c.Pts.IndexOf(dragPt); if (idx < 0) return;
            float x = LfoSnapX(Math.Max(0, Math.Min(1, (m.X - ird.X) / ird.Width))), y = LfoSnapY(Math.Max(-1, Math.Min(1, 1 - 2 * (m.Y - ird.Y) / ird.Height)));
            if (idx == 0) x = 0; else if (idx == c.Pts.Count - 1) x = 1; else x = Math.Max(c.Pts[idx - 1].X, Math.Min(c.Pts[idx + 1].X, x));
            dragPt.X = x; dragPt.Y = y; c.Rebuild(); modified = true;
        };
        rg.Right = () =>
        {
            if ((int)P("l" + k + "shape").Val != 7) return; var c = fx.LfoCurves[k - 1]; var ird = LfoInner(disp); var m = mouse; var hit = LfoNearest(c, ird, m, 10);
            if (hit != null) { int i = c.Pts.IndexOf(hit); if (i > 0 && i < c.Pts.Count - 1 && c.Pts.Count > 2) { c.Pts.Remove(hit); c.Rebuild(); modified = true; } return; }
            var bd = LfoNearestBend(c, ird, m); if (bd != null) { bd.C = 0; c.Rebuild(); modified = true; }
        };
        rg.Double = () =>
        {
            if ((int)P("l" + k + "shape").Val != 7) return; var c = fx.LfoCurves[k - 1]; var hit = LfoNearest(c, LfoInner(disp), mouse, 10); if (hit != null) { hit.Smooth = !hit.Smooth; c.Rebuild(); modified = true; }
        };

        float x1 = pn.X + 464; float cw = 114, gap = 4;
        Choose(g, "l" + k + "shape", Rc(x1, pn.Y + 14, cw, 22), Acc); Choose(g, "l" + k + "sync", Rc(x1 + (cw + gap), pn.Y + 14, cw, 22), Acc2); Choose(g, "l" + k + "retrig", Rc(x1 + 2 * (cw + gap), pn.Y + 14, cw, 22), Acc); Choose(g, "l" + k + "uni", Rc(x1 + 3 * (cw + gap), pn.Y + 14, cw, 22), Acc); Choose(g, "l" + k + "loop", Rc(x1 + 4 * (cw + gap), pn.Y + 14, 100, 22), Acc2);
        var pb = Rc(x1, pn.Y + 42, 150, 24); bool hp = Hot(pb); Fill(g, hp ? PanelHi : TM.C(255, 18, 10, 34), pb, 5); Stroke(g, Acc, pb, 5, 1); Txt(g, "SHAPE PRESETS  V", 11, Ink, pb, StringAlignment.Center, true);
        var pr = Add(pb); pr.Tip = "Pick a ready-made shape: sine, saw, stepped, random, wobbles. You can then reshape it."; pr.Down = pt => { var m = Win.NewMenu(); foreach (var nm in LfoCurve.PresetNames) { string n2 = nm; var it = new ToolStripMenuItem(nm); it.Click += (s2, e2) => { fx.LfoCurves[k - 1].Load(n2); SetVal(P("l" + k + "shape"), 7); Poke(); }; m.Items.Add(it); } ShowMenu(m, new Point((int)(pb.X * scale + ox), (int)(pb.Bottom * scale + oy))); };
        var sb = Rc(x1 + 156, pn.Y + 42, 110, 24); Fill(g, lfoSnap ? Color.FromArgb(120, Acc2) : (Hot(sb) ? PanelHi : TM.C(255, 18, 10, 34)), sb, 5); Stroke(g, lfoSnap ? Acc2 : Edge, sb, 5, 1); Txt(g, lfoSnap ? "SNAP: ON" : "SNAP: OFF", 11, Ink, sb, StringAlignment.Center, lfoSnap);
        var sr = Add(sb); sr.Tip = "Snap points to the grid when drawing."; sr.Down = pt => { lfoSnap = !lfoSnap; };
        SourceChip(g, Rc(x1 + 272, pn.Y + 42, 150, 24), k, "LFO " + k); Knob(g, "l" + k + "rate", x1 + 40, pn.Y + 106, sy == 0 ? "RATE Hz" : "RATE", Acc2, 22); Knob(g, "l" + k + "phase", x1 + 160, pn.Y + 106, "PHASE", Acc, 22); Knob(g, "l" + k + "smooth", x1 + 280, pn.Y + 106, "SMOOTH", Acc, 22); Knob(g, "l" + k + "fade", x1 + 400, pn.Y + 106, "FADE IN", Acc, 22);
        QuickAssign(g, Rc(x1 - 6, pn.Y + 156, 520, 108), k, "LFO " + k);
    }
}
