using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

// ======================================================================
//  Parameter automation: a setting of a plugin follows a curve while the song plays (and in every export).
//  Each automated setting has a "lane": points of (song position, value) joined by straight lines (steps for a choice list).
// ======================================================================
class AutoPoint { public long Frame; public float Value; }

class AutoLane
{
    public Fx Fx; public string Param = ""; public bool On = true; public readonly List<AutoPoint> Pts = new List<AutoPoint>();
    public void SortPts() { Pts.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
    // the value at a song position (frames). Before the first point and after the last one it holds that point's value. NaN when there are no points.
    public float ValueAt(long frame, bool steps)
    {
        int n = Pts.Count; if (n == 0) return float.NaN; if (n == 1 || frame <= Pts[0].Frame) return Pts[0].Value; if (frame >= Pts[n - 1].Frame) return Pts[n - 1].Value;
        int lo = 0, hi = n - 1; while (hi - lo > 1) { int m = (lo + hi) / 2; if (Pts[m].Frame <= frame) lo = m; else hi = m; }
        var a = Pts[lo]; var b = Pts[hi]; if (steps) return a.Value;
        double t = (frame - a.Frame) / (double)Math.Max(1, b.Frame - a.Frame); return (float)(a.Value + (b.Value - a.Value) * t);
    }
    public AutoLane CloneFor(Fx fx) { var c = new AutoLane { Fx = fx, Param = Param, On = On }; foreach (var p in Pts) c.Pts.Add(new AutoPoint { Frame = p.Frame, Value = p.Value }); return c; }

    // "frame:value,frame:value" for the project file
    public string PointsText()
    {
        var sb = new StringBuilder(); foreach (var p in Pts) { if (sb.Length > 0) sb.Append(','); sb.Append(p.Frame.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.ToString("R", CultureInfo.InvariantCulture)); }
        return sb.ToString();
    }
    public static void ParsePoints(AutoLane lane, string s)
    {
        foreach (var part in (s ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var q = part.Split(':'); long fr; float v; if (q.Length == 2 && long.TryParse(q[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out fr) && float.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v) && fr >= 0) lane.Pts.Add(new AutoPoint { Frame = fr, Value = v });
        }
        lane.SortPts();
    }
}

// the engine side: run a plugin while its settings follow their lanes
partial class StudioEngine
{
    readonly List<AutoLane> autoTmp = new List<AutoLane>(); readonly float[] autoL = new float[256], autoR = new float[256];
    // the lanes that are switched on for this plugin (null when there are none)
    List<AutoLane> AutoLanes(Track t, Fx fx)
    {
        autoTmp.Clear(); var all = t.Auto; for (int i = 0; i < all.Count; i++) { var l = all[i]; if (l.On && l.Fx == fx && l.Pts.Count > 0) autoTmp.Add(l); }
        return autoTmp.Count == 0 ? null : autoTmp;
    }
    static void ApplyLanes(Fx fx, List<AutoLane> lanes, long pos)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            var l = lanes[i]; var p = fx.Find(l.Param); if (p == null) continue; float v = l.ValueAt(pos, p.IsChoice); if (float.IsNaN(v)) continue;
            if (p.IsChoice) { float r = (float)Math.Round(v); if ((int)r != (int)Math.Round(p.Val)) fx.Set(p.Id, r); }
            else if (Math.Abs(v - p.Val) > (p.Max - p.Min) * 0.0004f) fx.Set(p.Id, v);
        }
    }
    // a block with moving lanes is run in pieces of 256 frames so the setting glides instead of stepping once per block
    void ProcessAuto(Track t, Fx fx, List<AutoLane> lanes, long pos, int n, bool transport, bool hasSc)
    {
        bool moving = false;
        if (!hasSc && n > 256) for (int i = 0; i < lanes.Count; i++) { var p = fx.Find(lanes[i].Param); bool st = p != null && p.IsChoice; if (Math.Abs(lanes[i].ValueAt(pos, st) - lanes[i].ValueAt(pos + n - 1, st)) > 1e-6f) { moving = true; break; } }
        int step = moving ? 256 : n;
        for (int off = 0; off < n; off += step)
        {
            int m = Math.Min(step, n - off); long p0 = pos + off; ApplyLanes(fx, lanes, p0);
            if (step >= n) { fx.BeginBlock(t, p0, m, transport); fx.Process(t.tl, t.tr, m); }
            else { Array.Copy(t.tl, off, autoL, 0, m); Array.Copy(t.tr, off, autoR, 0, m); fx.BeginBlock(t, p0, m, transport); fx.Process(autoL, autoR, m); Array.Copy(autoL, 0, t.tl, off, m); Array.Copy(autoR, 0, t.tr, off, m); }
        }
    }
}

// saving and loading
static class AutoIO
{
    public static void Write(StringBuilder sb, int trackIndex, Track t)
    {
        foreach (var l in t.Auto) { int fi = t.Fx.IndexOf(l.Fx); if (fi < 0 || l.Pts.Count == 0) continue; sb.Append("auto|").Append(trackIndex).Append('|').Append(fi).Append('|').Append(l.Param).Append('|').Append(l.On ? 1 : 0).Append('|').Append(l.PointsText()).Append('\n'); }
    }
    // p = the split line: auto | track | plugin number | setting | on | points
    public static void Read(string[] p, Track[] trs, List<Fx>[] newFx, List<KeyValuePair<Track, AutoLane>> into)
    {
        int ti, fi; if (p.Length < 6 || !int.TryParse(p[1], out ti) || !int.TryParse(p[2], out fi) || ti < 0 || ti >= trs.Length || fi < 0 || fi >= newFx[ti].Count || newFx[ti][fi] == null) return;
        var lane = new AutoLane { Fx = newFx[ti][fi], Param = p[3], On = p[4] == "1" }; AutoLane.ParsePoints(lane, p[5]); if (lane.Pts.Count > 0) into.Add(new KeyValuePair<Track, AutoLane>(trs[ti], lane));
    }
}

// the editor
partial class StudioForm
{
    bool autoOpen; Track autoTrack; AutoLane autoLane; AutoPoint autoPt; Track paramTrack;

    internal AutoLane FindLane(Track tr, Fx f, string param) { if (tr == null) return null; foreach (var l in tr.Auto) if (l.Fx == f && l.Param == param) return l; return null; }

    // press A next to a setting: open (or start) its automation lane
    void OpenAuto(Track tr, Fx fx, Param p)
    {
        if (tr == null) { Toast("Select a track first."); return; }
        var lane = FindLane(tr, fx, p.Id);
        if (lane == null)
        {
            PushUndo(); lane = new AutoLane { Fx = fx, Param = p.Id }; long end = Math.Max(eng.EndFrame, StudioEngine.SR * 10L);
            lane.Pts.Add(new AutoPoint { Frame = 0, Value = p.Val }); lane.Pts.Add(new AutoPoint { Frame = end, Value = p.Val });
            lock (eng.Sync) tr.Auto.Add(lane);
        }
        autoTrack = tr; autoLane = lane; autoOpen = true; autoPt = null; stylesOpen = false; browserOpen = false;
    }
    void CloseAuto() { autoOpen = false; autoPt = null; }

    string AutoValText(Param p, float v)
    {
        if (p.IsChoice) return p.Choices[(int)Math.Max(0, Math.Min(p.Choices.Length - 1, Math.Round(v)))];
        return v.ToString(Math.Abs(p.Max - p.Min) >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + (p.Unit.Length > 0 ? " " + p.Unit : "");
    }

    void PaintAutoSheet(Graphics g)
    {
        if (!autoOpen || autoLane == null || autoTrack == null) return;
        var fx = autoLane.Fx; var prm = fx.Find(autoLane.Param); if (prm == null || !autoTrack.Fx.Contains(fx)) { autoOpen = false; return; }
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseAuto());
        float sw = Math.Min(1040, W - 24), sh = Math.Min(480, H - 30); var sheet = Rc((W - sw) / 2, (H - sh) / 2, sw, sh);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 22); Glow(g, NeonCyan, sheet, 22, 0.7f, 4); StrokeRR(g, Color.FromArgb(180, NeonCyan), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "Automation", 20, Ink, Rc(sheet.X + 22, sheet.Y + 12, 200, 32), StringAlignment.Near, true);
        Txt(g, autoTrack.Name + "  >  " + fx.Title + "  >  " + prm.Name, 14, NeonCyan, Rc(sheet.X + 150, sheet.Y + 16, sheet.Width - 560, 26), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 50, sheet.Y + 12, 32, 32); FillRR(g, Hot(close) ? Color.FromArgb(160, 70, 38, 130) : Color.FromArgb(110, 40, 24, 80), close, 16); Txt(g, "x", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => CloseAuto());
        var lane = autoLane;
        Chip(g, Rc(sheet.Right - 330, sheet.Y + 14, 110, 28), lane.On ? "Automation on" : "Automation off", lane.On, () => { lock (eng.Sync) lane.On = !lane.On; });
        Btn(g, Rc(sheet.Right - 214, sheet.Y + 14, 72, 28), "Flatten", 0, () => { PushUndo(); lock (eng.Sync) { float v = prm.Val; foreach (var pt in lane.Pts) pt.Value = v; } }, 12);
        Btn(g, Rc(sheet.Right - 136, sheet.Y + 14, 78, 28), "Remove", 3, () => { PushUndo(); lock (eng.Sync) autoTrack.Auto.Remove(lane); CloseAuto(); }, 12);
        // the other lanes on this track: press one to switch to it
        float cx = sheet.X + 22, cy = sheet.Y + 54; Txt(g, "Lanes on this track:", 11, DimC, Rc(cx, cy, 120, 22), StringAlignment.Near, false); cx += 122;
        foreach (var other in autoTrack.Auto.ToArray())
        {
            var ofx = other.Fx; var op = ofx == null ? null : ofx.Find(other.Param); if (op == null || !autoTrack.Fx.Contains(ofx)) continue; string label = ofx.Title + ": " + op.Name; float cw = 24 + label.Length * 6.6f; if (cx + cw > sheet.Right - 20) break;
            var ol = other; Chip(g, Rc(cx, cy - 2, cw, 24), label, ol == lane, () => { autoLane = ol; autoPt = null; }); cx += cw + 6;
        }
        // the plot
        float lm = 74; if (prm.IsChoice) foreach (var ch in prm.Choices) lm = Math.Max(lm, g.MeasureString(ch, GfxCache.Font(10, false)).Width + 16); lm = Math.Min(lm, sheet.Width * 0.4f);   // room for the names of a list of choices
        var plot = Rc(sheet.X + lm, sheet.Y + 90, sheet.Width - lm - 26, sheet.Height - 90 - 74); long T = Math.Max(eng.EndFrame, StudioEngine.SR * 10L);
        FillRR(g, Color.FromArgb(235, 8, 4, 20), plot, 8); StrokeRR(g, Color.FromArgb(120, NeonViolet), plot, 8, 1);
        float min = prm.Min, max = prm.Max; if (max - min < 1e-6f) max = min + 1;
        Func<long, float> X = fr => plot.X + (float)(fr / (double)T) * plot.Width; Func<float, float> Y = v => plot.Bottom - (v - min) / (max - min) * plot.Height;
        g.SetClip(Rc(sheet.X + 2, plot.Y - 6, plot.Right - sheet.X + 10, plot.Height + 30));
        // the grid: values up the side, seconds along the bottom
        int rows = prm.IsChoice ? Math.Min(prm.Choices.Length, 9) : 5;
        for (int i = 0; i < rows; i++)
        {
            float v = prm.IsChoice ? (prm.Choices.Length <= 9 ? i : min + (max - min) * i / (rows - 1)) : min + (max - min) * i / (rows - 1); float yy = Y(v);
            using (var pen = new Pen(Color.FromArgb(50, 190, 160, 255))) g.DrawLine(pen, plot.X, yy, plot.Right, yy);
            Txt(g, AutoValText(prm, v), 10, DimC, Rc(sheet.X + 6, yy - 9, lm - 12, 18), StringAlignment.Far, false);
        }
        double secs = T / (double)StudioEngine.SR; double stepS = secs > 600 ? 120 : secs > 240 ? 60 : secs > 90 ? 30 : secs > 40 ? 10 : 5;
        for (double s = 0; s <= secs + 0.01; s += stepS)
        {
            float xx = X((long)(s * StudioEngine.SR)); using (var pen = new Pen(Color.FromArgb(40, 190, 160, 255))) g.DrawLine(pen, xx, plot.Y, xx, plot.Bottom);
            Txt(g, ((int)s / 60) + ":" + ((int)s % 60).ToString("00"), 10, DimC, Rc(xx - 24, plot.Bottom + 4, 48, 16), StringAlignment.Center, false);
        }
        g.SetClip(plot);
        // the curve (straight lines, or steps for a list of choices)
        var pts = new List<AutoPoint>(lane.Pts); var path = new List<PointF>();
        if (pts.Count > 0)
        {
            path.Add(new PointF(plot.X, Y(pts[0].Value)));
            for (int i = 0; i < pts.Count; i++) { float px = X(pts[i].Frame), py = Y(pts[i].Value); if (prm.IsChoice && i > 0) path.Add(new PointF(px, Y(pts[i - 1].Value))); path.Add(new PointF(px, py)); }
            path.Add(new PointF(plot.Right, Y(pts[pts.Count - 1].Value)));
        }
        if (path.Count > 1)
        {
            var fill = new List<PointF>(path); fill.Add(new PointF(plot.Right, plot.Bottom)); fill.Add(new PointF(plot.X, plot.Bottom));
            using (var br = new SolidBrush(Color.FromArgb(lane.On ? 38 : 16, NeonCyan))) g.FillPolygon(br, fill.ToArray());
            using (var pen = new Pen(lane.On ? NeonCyan : Color.FromArgb(120, 140, 140, 170), 2.4f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, path.ToArray());
        }
        foreach (var pt in pts) { float px = X(pt.Frame), py = Y(pt.Value); bool hot = pt == autoPt || Math.Abs(MousePos.X - px) < 9 && Math.Abs(MousePos.Y - py) < 9; using (var b = new SolidBrush(hot ? Color.White : NeonCyan)) g.FillEllipse(b, px - 6, py - 6, 12, 12); using (var pen = new Pen(Color.FromArgb(220, 10, 5, 25), 1.5f)) g.DrawEllipse(pen, px - 6, py - 6, 12, 12); }
        // the playhead
        { float xx = X(Math.Min(T, eng.Pos)); using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f)) g.DrawLine(pen, xx, plot.Y, xx, plot.Bottom); }
        // what the plugin is set to right now
        { float cur = lane.ValueAt(eng.Pos, prm.IsChoice); if (!float.IsNaN(cur)) Txt(g, "now: " + AutoValText(prm, cur), 12, Ink, Rc(plot.Right - 190, plot.Y + 6, 180, 20), StringAlignment.Far, true); }
        g.ResetClip();
        // the mouse: press an empty spot to add a point, press a point to drag it, right-click a point to delete it
        var area = plot;
        Func<PointF, AutoPoint> nearest = pt0 => { AutoPoint best = null; double bd = 14; foreach (var q in lane.Pts.ToArray()) { double d = Math.Sqrt(Math.Pow(X(q.Frame) - pt0.X, 2) + Math.Pow(Y(q.Value) - pt0.Y, 2)); if (d < bd) { bd = d; best = q; } } return best; };
        Action<PointF, AutoPoint> place = (m, pt) =>
        {
            long fr = (long)Math.Max(0, Math.Min(T, (m.X - plot.X) / plot.Width * T)); float v = min + (plot.Bottom - Math.Max(plot.Y, Math.Min(plot.Bottom, m.Y))) / plot.Height * (max - min); v = prm.IsChoice ? (float)Math.Round(v) : v; v = Math.Max(prm.Min, Math.Min(prm.Max, v));
            lock (eng.Sync) { pt.Frame = fr; pt.Value = v; lane.SortPts(); }
        };
        var reg = new HitReg { R = area };
        reg.OnDown = () => { var hit = nearest(MousePos); PushUndo(); if (hit == null) { hit = new AutoPoint(); lock (eng.Sync) lane.Pts.Add(hit); } autoPt = hit; place(MousePos, hit); };
        reg.OnDrag = m => { if (autoPt != null) place(m, autoPt); };
        reg.OnUp = () => { autoPt = null; };
        reg.OnRight = () => { var hit = nearest(MousePos); if (hit != null && lane.Pts.Count > 1) { PushUndo(); lock (eng.Sync) lane.Pts.Remove(hit); } };
        regs.Add(reg);
        Txt(g, "Click to add a point, drag a point to move it, right-click a point to delete it. The plugin follows this line while the song plays. Switch Automation off to set it by hand.", 11, DimC, Rc(sheet.X + 22, sheet.Bottom - 40, sheet.Width - 44, 30), StringAlignment.Near, false);
    }
}
