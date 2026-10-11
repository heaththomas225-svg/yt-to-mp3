using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Purphex Music: dragging tracks and clips. The thing you hold follows the mouse smoothly as a see-through "hologram" (the other tracks show through it),
// the row it came from shows an empty slot, the row it will land on lights up, and the other rows slide into place instead of jumping.
partial class StudioForm
{
    readonly Dictionary<Track, float> animOff = new Dictionary<Track, float>();
    bool ghostOn; int ghostKind; Track ghostTrack; float ghostY, ghostGrab; bool ghostInit;
    readonly Stopwatch ghostSw = Stopwatch.StartNew(); double ghostLast;

    float GhostDt() { double now = ghostSw.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.05, Math.Max(0.001, now - ghostLast)); ghostLast = now; return dt; }
    float AnimOff(Track t) { float v; return animOff.TryGetValue(t, out v) ? v : 0f; }
    void AnimShift(Track t, float rows) { animOff[t] = AnimOff(t) + rows * LaneH; }   // draw it where it was, then let it slide to its new row
    void StepAnim(float dt)
    {
        if (animOff.Count == 0) return; float k = (float)Math.Exp(-dt * 14);
        foreach (var t in new List<Track>(animOff.Keys)) { float v = animOff[t] * k; if (Math.Abs(v) < 0.6f) animOff.Remove(t); else animOff[t] = v; }
    }
    float SlotTop(Track t) { return ToolH + TransH + RulerH - vscroll + Math.Max(0, eng.Tracks.IndexOf(t.Host ?? t)) * LaneH; }
    void GhostGrab(Track t) { ghostGrab = MousePos.Y - SlotTop(t); }
    void GhostStart(Track t, int kind) { if (!ghostOn || ghostTrack != t || ghostKind != kind) ghostInit = false; ghostOn = true; ghostTrack = t; ghostKind = kind; }
    void GhostEnd() { ghostOn = false; ghostTrack = null; ghostInit = false; }

    // the empty slot a held track leaves behind
    void PaintGhostSlot(Graphics g, Track tr, RectangleF full)
    {
        if (!ghostOn || ghostKind != 1 || tr != ghostTrack) return;
        using (var b = new SolidBrush(Color.FromArgb(150, 6, 3, 16))) g.FillRectangle(b, full);
        using (var pen = new Pen(Color.FromArgb(150, NeonCyan), 1.4f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, full.X + 2, full.Y + 2, full.Width - 4, full.Height - 4);
    }

    void PaintGhost(Graphics g, RectangleF tl, float top)
    {
        if (!ghostOn || ghostTrack == null) return; var tr = ghostTrack; int from = eng.Tracks.IndexOf(tr.Host ?? tr); if (from < 0) { GhostEnd(); return; }
        bool header = ghostKind == 1; float h = header ? LaneH : LaneH - 14, laneTop = ToolH + TransH + RulerH, areaBottom = ClientSize.Height - 24;
        float want = MousePos.Y - ghostGrab + (header ? 0 : 7); want = Math.Max(laneTop - 4, Math.Min(areaBottom - h, want));
        if (!ghostInit) { ghostY = want; ghostInit = true; } else ghostY += (want - ghostY) * 0.38f;   // eases toward the mouse: a little glide instead of a snap
        double pulse = 0.5 + 0.5 * Math.Sin(ghostSw.Elapsed.TotalSeconds * 7); Color c = Neon(tr.Color); int a = 70 + (int)(26 * pulse);
        // where it would land
        float slot0 = ToolH + TransH + RulerH - vscroll; int row = (int)Math.Floor((MousePos.Y - slot0) / LaneH);
        if (!header && row != from && Math.Abs(MousePos.Y - laneStart.Y) >= LaneH * 0.5f)
        {
            bool fresh = row >= LaneCount || row < 0 || (row < LaneCount && (eng.Tracks[row].Clip != null || eng.Tracks[row].Instrument)); int rr = Math.Max(0, Math.Min(row, LaneCount)); var land = Rc(HeadW, slot0 + rr * LaneH, tl.Width, LaneH);
            using (var b = new SolidBrush(Color.FromArgb(34, NeonCyan))) g.FillRectangle(b, land);
            using (var pen = new Pen(Color.FromArgb(220, NeonCyan), 1.6f) { DashStyle = fresh ? DashStyle.Dash : DashStyle.Solid }) g.DrawRectangle(pen, land.X + 1, land.Y + 1, land.Width - 2, land.Height - 2);
            Txt(g, fresh ? "+ new track" : "Drop here", 12, NeonCyan, Rc(land.X + 10, land.Y + 4, 160, 18), StringAlignment.Near, true);
        }
        if (header && hdMode == 2 && hdRow >= 0 && hdRow < LaneCount)
        {
            var land = Rc(0, slot0 + hdRow * LaneH, tl.Right, LaneH); using (var b = new SolidBrush(Color.FromArgb(40, NeonCyan))) g.FillRectangle(b, land);
            using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 2f)) g.DrawRectangle(pen, land.X + 1, land.Y + 1, land.Width - 2, land.Height - 2); Txt(g, "Share this row", 13, NeonCyan, Rc(HeadW + 12, land.Y + 6, 220, 20), StringAlignment.Near, true);
        }
        else if (header && hdMode == 1)
        {
            float ly = slot0 + hdGap * LaneH; GlowLine(g, NeonCyan, 0, ly, tl.Right, ly, 1.6f); Txt(g, "Move here", 12, NeonCyan, Rc(HeadW + 12, ly - 20, 160, 18), StringAlignment.Near, true);
        }
        RectangleF r;
        if (header) r = Rc(0, ghostY, tl.Right, h);
        else
        {
            float x0 = HeadW + (float)((tr.Offset - scroll) / spp), x1 = x0 + (float)(tr.UsedLength / spp); x0 = Math.Max(HeadW, x0); x1 = Math.Min(tl.Right, Math.Max(x0 + 24, x1)); r = Rc(x0, ghostY, x1 - x0, h);
        }
        var gst = g.Save(); g.SetClip(Rc(0, laneTop, ClientSize.Width, areaBottom - laneTop), CombineMode.Intersect);
        for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(24 + 10 * i, NeonCyan), i * 3f)) using (var p = RR(r, 10)) g.DrawPath(pen, p);   // soft glow so it reads as lifted
        using (var p = RR(r, 10))
        {
            using (var br = new LinearGradientBrush(r, Color.FromArgb(a + 20, c), Color.FromArgb(a - 20, NeonViolet), 0f)) g.FillPath(br, p);
            g.SetClip(p, CombineMode.Intersect); using (var pen = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) for (float yy = r.Y + ((float)(ghostSw.Elapsed.TotalSeconds * 40) % 4); yy < r.Bottom; yy += 4) g.DrawLine(pen, r.X, yy, r.Right, yy);   // scanlines, drifting
            g.ResetClip(); g.SetClip(Rc(0, laneTop, ClientSize.Width, areaBottom - laneTop), CombineMode.Intersect);
            using (var pen = new Pen(Color.FromArgb(235, NeonCyan), 1.8f)) g.DrawPath(pen, p);
        }
        if (header) Txt(g, tr.Name, 15, Color.FromArgb(235, 255, 255, 255), Rc(r.X + 30, r.Y + 8, 220, 22), StringAlignment.Near, true);
        else Txt(g, tr.Name, 13, Color.FromArgb(235, 255, 255, 255), Rc(r.X + 10, r.Y + 6, Math.Max(40, r.Width - 20), 20), StringAlignment.Near, true);
        g.Restore(gst);
    }
}
