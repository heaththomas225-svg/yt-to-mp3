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
    static readonly Color[] SrcCols = { Color.White, Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(120, 232, 150), Color.FromArgb(255, 190, 90), Color.FromArgb(255, 190, 90), Color.FromArgb(255, 190, 90), Color.FromArgb(120, 200, 255), Color.FromArgb(120, 200, 255), Color.FromArgb(255, 120, 180), Color.FromArgb(255, 120, 180), Color.FromArgb(255, 120, 180), Color.FromArgb(255, 120, 180) };

    static int DestOf(string paramId) { for (int i = 1; i < SynthTables.DestParam.Length; i++) if (SynthTables.DestParam[i] == paramId) return i; return 0; }

    // matrix slots (1..8) that are switched on and aimed at this knob
    List<int> SlotsOn(int dest)
    {
        var l = new List<int>(); if (dest <= 0) return l;
        for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "d").Val == dest && (int)P("m" + i + "s").Val != 0 && P("m" + i + "e").Val > 0.5) l.Add(i);
        return l;
    }
    static float RingScale(int dest) { return dest == 1 || dest == 17 || dest == 20 || (dest >= 27 && dest <= 29) ? 0.5f : 1f; }

    void DrawModRing(Graphics g, RectangleF ar, float f, List<int> slots, int dest)
    {
        var ring = RectangleF.Inflate(ar, 4.5f, 4.5f);
        foreach (int s in slots)
        {
            int src = (int)P("m" + s + "s").Val; double amt = P("m" + s + "a").Val / 100.0 * RingScale(dest); int pol = (int)P("m" + s + "p").Val; bool bipolarSrc = src <= 4 || src == 9;
            double lo, hi; if (pol == 0 && bipolarSrc) { lo = f - Math.Abs(amt); hi = f + Math.Abs(amt); } else { double a = amt * (pol == 2 ? -1 : 1); lo = Math.Min(f, f + a); hi = Math.Max(f, f + a); }
            lo = Math.Max(0, lo); hi = Math.Min(1, hi); if (hi - lo < 0.004) continue;
            using (var pen = new Pen(Color.FromArgb(220, SrcCols[Math.Min(src, SrcCols.Length - 1)]), 2.2f)) g.DrawArc(pen, ring, 135 + (float)lo * 270f, (float)(hi - lo) * 270f);
        }
    }

    // a small handle you grab and drop on a knob
    void SourceChip(Graphics g, RectangleF r, int src, string name)
    {
        bool hot = Hot(r) || dragSrc == src; Fill(g, hot ? Color.FromArgb(110, Acc2) : Color.FromArgb(24, 13, 46), r, 6); Stroke(g, SrcCols[Math.Min(src, SrcCols.Length - 1)], r, 6, 1.5f);
        Txt(g, "DRAG" + (r.Width > 90 ? " " : "") + (r.Width > 90 ? "TO A KNOB" : ""), 10, Ink, r, StringAlignment.Center, true);
        var rg = Add(r); rg.Tip = "Drag this onto any knob to make " + name + " move it. Drag on the knob with Ctrl held to set how much."; rg.Down = pt => { dragSrc = src; dragName = name; }; rg.Drag = (dx, dy) => { };
    }

    void DropOnKnob(Reg target)
    {
        int src = dragSrc; dragSrc = 0; if (target == null || target.P == null) { dropMsg = "Drop it on a knob."; dropMsgAt = DateTime.Now; return; }
        int dest = DestOf(target.P.Id); if (dest == 0) { dropMsg = target.P.Name + " can't be modulated yet. Use the MATRIX page for the full list."; dropMsgAt = DateTime.Now; return; }
        int slot = -1; for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "s").Val == src && (int)P("m" + i + "d").Val == dest) { slot = i; break; }
        if (slot < 0) for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "s").Val == 0 || (int)P("m" + i + "d").Val == 0) { slot = i; break; }
        if (slot < 0) { dropMsg = "All 8 matrix slots are in use. Remove one on the MATRIX page."; dropMsgAt = DateTime.Now; return; }
        SetVal(P("m" + slot + "s"), src); SetVal(P("m" + slot + "d"), dest); SetVal(P("m" + slot + "e"), 1); if (Math.Abs(P("m" + slot + "a").Val) < 1) SetVal(P("m" + slot + "a"), 50);
        dropMsg = dragName + " now moves " + target.P.Name + ". Ctrl + drag the knob to set how much."; dropMsgAt = DateTime.Now;
    }

    void PaintDragGhost(Graphics g)
    {
        if (dragSrc != 0)
        {
            var h = HitTest(mouse); bool ok = h != null && h.P != null && DestOf(h.P.Id) != 0;
            if (h != null && h.P != null) using (var pen = new Pen(ok ? Good : Color.FromArgb(255, 120, 130), 2f)) g.DrawEllipse(pen, h.R.X + 4, h.R.Y + 2, h.R.Width - 8, h.R.Width - 8);
            var r = Rc(mouse.X + 12, mouse.Y + 10, 150, 20); Fill(g, Color.FromArgb(230, 40, 20, 80), r, 6); Stroke(g, SrcCols[Math.Min(dragSrc, SrcCols.Length - 1)], r, 6, 1.5f); Txt(g, dragName + (ok ? "  > drop" : ""), 11, Ink, r, StringAlignment.Center, true);
        }
        else if ((DateTime.Now - dropMsgAt).TotalSeconds < 4 && dropMsg.Length > 0) { var r = Rc(8, H - 44, W - 16, 20); Fill(g, Color.FromArgb(230, 40, 20, 80), r, 6); Txt(g, dropMsg, 11, Good, r, StringAlignment.Center, true); }
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
