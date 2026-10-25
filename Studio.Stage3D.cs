using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

// =====================================================================================================
//  THE 3D STAGE PAGE (tab 12; Tools > 3D sound stage): every track you place is a glowing dot round your head. Drag a dot to move it
//  (which way and how far), turn the mouse wheel on it to raise or lower it. Each placed track has a 3D Sound Stage plugin doing the
//  sound, so its place can be automated like any setting (the A buttons in the plugin's window). The result is binaural stereo for
//  headphones (it plays and exports as normal stereo).
// =====================================================================================================
partial class StudioForm
{
    Track stageSel; Track stageDrag;
    static Stage3DFx StageOf(Track t) { lock (t.Fx) return t.Fx.OfType<Stage3DFx>().FirstOrDefault(); }
    void PlaceOnStage(Track t)
    {
        if (StageOf(t) != null) return; PushUndo(); var f = new Stage3DFx(); f.Prepare(StudioEngine.SR);
        int k = 0; foreach (var x in eng.Tracks) if (StageOf(x) != null) k++; f.Set("az", (k % 2 == 0 ? 1 : -1) * (30 + 25 * (k / 2)));   // a new place for each track, round the front
        lock (eng.Sync) t.Fx.Add(f); stageSel = t; Toast(t.Name + " is on the 3D stage: drag its dot to move it, turn the wheel on it to raise or lower it.");
    }
    void RemoveFromStage(Track t) { var f = StageOf(t); if (f == null) return; PushUndo(); lock (eng.Sync) t.Fx.Remove(f); if (stageSel == t) stageSel = null; }

    void PaintStage3D(Graphics g) { PaintStage3D(g, Rc(14, ToolH + 10, ClientSize.Width - 28, ClientSize.Height - ToolH - 24)); }
    void PaintStage3D(Graphics g, RectangleF area)
    {
        float lw = Math.Min(300, area.Width * 0.3f); var listR = Rc(area.X, area.Y, lw, area.Height); var stage = Rc(listR.Right + 14, area.Y, area.Right - listR.Right - 14, area.Height);
        // the track list
        FillRR(g, TM.C(225, 14, 7, 32), listR, 14); StrokeRR(g, Color.FromArgb(160, NeonViolet), listR, 14, 1.2f);
        Txt(g, "TRACKS", 14, Ink, Rc(listR.X + 14, listR.Y + 10, 200, 22), StringAlignment.Near, true);
        float y = listR.Y + 40; int idx = 0;
        foreach (var t in eng.Tracks.ToArray())
        {
            if (t.IsBus || t.Host != null) continue; var row = Rc(listR.X + 10, y, listR.Width - 20, 40); if (row.Bottom > listR.Bottom - 70) break; y += 46; var st = StageOf(t); var tt = t; bool sel = stageSel == t;
            FillRR(g, sel ? TM.C(230, 60, 30, 120) : TM.C(200, 26, 14, 52), row, 10); if (sel) StrokeRR(g, NeonCyan, row, 10, 1.4f);
            using (var b = new SolidBrush(Neon(t.Color))) g.FillEllipse(b, row.X + 10, row.Y + 14, 12, 12);
            Txt(g, (idx + 1) + "  " + t.Name, 12, Ink, Rc(row.X + 30, row.Y, row.Width - 140, row.Height), StringAlignment.Near, true); idx++;
            var bb = Rc(row.Right - 100, row.Y + 7, 92, 26);
            if (st == null) { Btn(g, bb, "PLACE IT", 2, () => PlaceOnStage(tt), 10); Hint2(bb, "Put " + t.Name + " on the 3D stage"); }
            else { Btn(g, bb, "REMOVE", 0, () => RemoveFromStage(tt), 10); Hint2(bb, "Take " + t.Name + " off the stage (it goes back to normal stereo)"); }
            Reg(Rc(row.X, row.Y, row.Width - 106, row.Height), () => stageSel = tt);
        }
        TxtWrap(g, "Drag a dot to move it. Mouse wheel on a dot: higher or lower. Each placed track plays through a 3D Sound Stage plugin: automate it in the plugin's window. For headphones.", 10, DimC, Rc(listR.X + 14, listR.Bottom - 66, listR.Width - 28, 60));
        // the stage: a galaxy, the floor rings, you in the middle
        using (var p = RR(stage, 16)) { var gs = g.Save(); g.SetClip(p, CombineMode.Intersect); PaintGalaxy(g, stage, 0x3D3D, 0.9f); g.Restore(gs); }
        StrokeRR(g, Color.FromArgb(200, NeonViolet), stage, 16, 1.4f);
        float cx = stage.X + stage.Width / 2, cy = stage.Y + stage.Height * 0.56f, scale = Math.Min(stage.Width / 2.2f, stage.Height / 1.25f) / 12f;   // pixels per metre (12 m to the edge)
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var m in new[] { 1, 3, 6, 12 }) { float rx = m * scale, ry = rx * 0.5f; using (var pen = new Pen(Color.FromArgb(m == 12 ? 120 : 80, NeonCyan), 1f) { DashPattern = m == 12 ? new[] { 1f } : new[] { 4f, 4f } }) g.DrawEllipse(pen, cx - rx, cy - ry, rx * 2, ry * 2); Txt(g, m + " m", 8.5f, DimC, Rc(cx + rx - 30, cy - 12, 28, 12), StringAlignment.Far, false); }
        using (var pen = new Pen(Color.FromArgb(60, NeonCyan), 1f)) { g.DrawLine(pen, cx - 12 * scale, cy, cx + 12 * scale, cy); g.DrawLine(pen, cx, cy - 6 * scale, cx, cy + 6 * scale); }
        Txt(g, "FRONT", 9, DimC, Rc(cx - 40, cy - 6 * scale - 16, 80, 14), StringAlignment.Center, true); Txt(g, "BEHIND", 9, DimC, Rc(cx - 40, cy + 6 * scale + 2, 80, 14), StringAlignment.Center, true);
        // you
        using (var hb = new SolidBrush(TM.C(255, 230, 220, 255))) g.FillEllipse(hb, cx - 9, cy - 11, 18, 22); using (var pen = new Pen(NeonCyan, 1.5f)) g.DrawEllipse(pen, cx - 9, cy - 11, 18, 22);
        using (var nb = new SolidBrush(TM.C(255, 230, 220, 255))) g.FillPolygon(nb, new[] { new PointF(cx - 4, cy - 10), new PointF(cx, cy - 17), new PointF(cx + 4, cy - 10) });   // your nose: the way you face
        // each placed track: its spot on the floor, a stem up to its height, and the glowing dot
        Track hotDot = null;
        foreach (var t in eng.Tracks.ToArray())
        {
            var st = StageOf(t); if (st == null) continue; double az = st.Get("az") * Math.PI / 180, el = st.Get("el") * Math.PI / 180, d = st.Get("dist");
            float fx = cx + (float)(Math.Sin(az) * d * Math.Cos(el)) * scale, fy = cy - (float)(Math.Cos(az) * d * Math.Cos(el)) * scale * 0.5f, hy = fy - (float)(Math.Sin(el) * d) * scale * 0.5f;
            Color c = Neon(t.Color); bool sel = stageSel == t;
            using (var pen = new Pen(Color.FromArgb(150, c), 1.2f) { DashPattern = new[] { 2f, 2f } }) g.DrawLine(pen, fx, fy, fx, hy);
            using (var sb = new SolidBrush(Color.FromArgb(90, c))) g.FillEllipse(sb, fx - 6, fy - 3, 12, 6);   // its shadow on the floor
            for (int k = 3; k >= 1; k--) using (var gb = new SolidBrush(Color.FromArgb(sel ? 50 : 30, c))) g.FillEllipse(gb, fx - 9 - k * 4, hy - 9 - k * 4, 18 + k * 8, 18 + k * 8);
            using (var db = new SolidBrush(c)) g.FillEllipse(db, fx - 9, hy - 9, 18, 18); using (var pen = new Pen(Color.White, sel ? 2.2f : 1.2f)) g.DrawEllipse(pen, fx - 9, hy - 9, 18, 18);
            Txt(g, t.Name, 10, Ink, Rc(fx - 70, hy - 30, 140, 16), StringAlignment.Center, true);
            var dotR = Rc(fx - 14, hy - 14, 28, 28); var tt = t; var ss = st;
            var rg = Reg(dotR, () => { stageSel = tt; stageDrag = tt; PushUndo(); });
            rg.OnDrag = pt => { if (stageDrag != tt) return; double x = (pt.X - cx) / scale, z = (cy - pt.Y) / (scale * 0.5); double nd = Math.Max(0.5, Math.Min(12, Math.Sqrt(x * x + z * z))); ss.Set("az", (float)(Math.Atan2(x, z) * 180 / Math.PI)); ss.Set("dist", (float)nd); Invalidate(); };
            rg.OnUp = () => stageDrag = null; rg.OnWheel = dd => { ss.Set("el", Math.Max(-60, Math.Min(60, ss.Get("el") + Math.Sign(dd) * 5))); Invalidate(); };
            if (Hot(dotR)) hotDot = t;
            Hint2(dotR, t.Name + ": " + Math.Round(st.Get("az")) + " deg, " + st.Get("dist").ToString("0.0") + " m away, height " + Math.Round(st.Get("el")) + " deg (drag to move, wheel for height)");
        }
        g.SmoothingMode = oldSm;
        if (!eng.Tracks.Any(t => StageOf(t) != null)) Txt(g, "PRESS PLACE IT ON A TRACK TO PUT IT ON THE STAGE", 13, DimC, Rc(stage.X, stage.Bottom - 50, stage.Width, 20), StringAlignment.Center, true);
    }
}
