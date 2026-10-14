using System;
using System.Drawing;
using System.Windows.Forms;

// LOOP: the button asks what to loop. The whole song (it keeps playing round), or a section: drag across the timeline over the part
// you want to hear again and again. The section shows as a band on the ruler and over the tracks; drag its ends on the ruler to change it.
partial class StudioForm
{
    internal bool loopPick;   // waiting for you to drag the section
    bool loopPicking; long loopDragA, loopDragB; int loopEdge;   // loopEdge: 1 = dragging the start of the band, 2 = its end

    long FrameAtX(float x) { return Math.Max(0, scroll + (long)((x - HeadW) * spp)); }
    // the dragged part, widened out to whole grid steps when snapping is on (the start goes down to a grid line, the end up to one), so a short drag still loops what you dragged
    void SnapRange(ref long a, ref long b) { if (b < a) { long t = a; a = b; b = t; } if (!snapOn) return; double gf = GridFrames; if (gf < 1) return; a = (long)(Math.Floor(a / gf) * gf); b = (long)(Math.Ceiling(b / gf) * gf); if (b <= a) b = a + (long)gf; }
    static string LoopTime(long f) { double s = f / (double)StudioEngine.SR; return ((int)(s / 60)) + ":" + ((int)(s % 60)).ToString("00") + "." + ((int)(s * 10 % 10)); }

    internal void LoopMenu(RectangleF at)
    {
        var m = Win.NewMenu();
        var whole = new ToolStripMenuItem("Loop the whole song (it keeps playing round)") { Checked = eng.Loop && !eng.LoopSection }; whole.Click += (a, b) => LoopWhole(); m.Items.Add(whole);
        var sec = new ToolStripMenuItem(eng.LoopSection ? "Loop a different section (drag it on the timeline)" : "Loop a section: drag on the timeline where you want it") { Checked = eng.LoopSection }; sec.Click += (a, b) => LoopPickStart(); m.Items.Add(sec);
        if (eng.Loop) { m.Items.Add(new ToolStripSeparator()); m.Items.Add("Turn the loop off").Click += (a, b) => LoopOff(); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    internal void LoopWhole() { loopPick = false; eng.LoopA = -1; eng.LoopB = -1; eng.Loop = true; Toast("LOOP: the whole song plays round and round. Press LOOP to turn it off or to loop a section."); }
    internal void LoopOff() { loopPick = false; eng.Loop = false; eng.LoopA = -1; eng.LoopB = -1; Toast("Loop off."); }
    internal void LoopPickStart() { loopPick = true; loopPicking = false; tab = 0; Toast("Drag across the timeline (over the tracks or the ruler) over the part you want to hear again and again. Esc cancels."); }
    // a section picked (a and b in frames): it plays round from its start straight away
    internal void LoopSet(long a, long b)
    {
        SnapRange(ref a, ref b);
        if (b - a < StudioEngine.SR / 4) { Toast("That is too short to loop: drag across a wider part."); return; }
        loopPick = false; eng.LoopA = a; eng.LoopB = b; eng.Loop = true; eng.Seek(a); if (!eng.Playing) PlayGuard();
        Toast("LOOP: " + LoopTime(a) + " to " + LoopTime(b) + " plays again and again. Drag the ends of the band on the ruler to change it; press LOOP to turn it off.");
    }

    // drawn over the tracks and the ruler, after the lanes (so a drag to pick the section wins over the clips while you are picking)
    void PaintLoop(Graphics g, RectangleF tl, float top)
    {
        float bottom = ClientSize.Height - 46; var area = Rc(HeadW, top, tl.Width, bottom - top);
        var cs = g.Clip; g.ResetClip(); var gs = g.Save(); g.SetClip(Rc(HeadW, top, tl.Width, bottom - top));
        long a = -1, b = -1; if (loopPicking) { a = loopDragA; b = loopDragB; SnapRange(ref a, ref b); } else if (eng.LoopSection) { a = eng.LoopA; b = eng.LoopB; }
        if (a >= 0 && b > a)
        {
            float x0 = HeadW + (float)((a - scroll) / spp), x1 = HeadW + (float)((b - scroll) / spp);
            using (var br = new SolidBrush(Color.FromArgb(loopPicking ? 46 : 30, NeonCyan))) g.FillRectangle(br, x0, top + RulerH, x1 - x0, bottom - top - RulerH);
            using (var br = new SolidBrush(Color.FromArgb(150, NeonCyan))) g.FillRectangle(br, x0, top + 2, x1 - x0, RulerH - 4);
            using (var pen = new Pen(NeonCyan, 2f)) { g.DrawLine(pen, x0, top, x0, bottom); g.DrawLine(pen, x1, top, x1, bottom); }
            if (x1 - x0 > 50) Txt(g, "LOOP", 11, Color.Black, Rc(x0, top + 2, x1 - x0, RulerH - 4), StringAlignment.Center, true);
            if (!loopPick && eng.LoopSection)
            {   // the two ends of the band on the ruler can be dragged
                var h0 = Rc(x0 - 7, top, 14, RulerH); var r0 = Reg(h0, () => loopEdge = 1); r0.OnDrag = pt => { eng.LoopA = Math.Max(0, Math.Min(eng.LoopB - StudioEngine.SR / 4, Snap(FrameAtX(pt.X)))); }; r0.OnUp = () => loopEdge = 0; Hint2(h0, "Drag to move the start of the loop");
                var h1 = Rc(x1 - 7, top, 14, RulerH); var r1 = Reg(h1, () => loopEdge = 2); r1.OnDrag = pt => { eng.LoopB = Math.Max(eng.LoopA + StudioEngine.SR / 4, Snap(FrameAtX(pt.X))); }; r1.OnUp = () => loopEdge = 0; Hint2(h1, "Drag to move the end of the loop");
            }
        }
        if (loopPick)
        {
            if (!loopPicking) { var tip = Rc(HeadW + tl.Width / 2 - 230, top + RulerH + 10, 460, 34); FillRR(g, TM.C(235, 10, 6, 24), tip, 10); StrokeRR(g, NeonCyan, tip, 10, 1.6f); Txt(g, "DRAG OVER THE PART YOU WANT TO LOOP  (ESC CANCELS)", 12, Ink, tip, StringAlignment.Center, true); }
            var pr = Reg(area, () => { loopPicking = true; loopDragA = loopDragB = FrameAtX(MousePos.X); });
            pr.OnDrag = pt => { if (loopPicking) loopDragB = FrameAtX(pt.X); };
            pr.OnUp = () => { if (!loopPicking) return; loopPicking = false; if (Math.Abs(loopDragB - loopDragA) < 6 * spp) { Toast("Hold the button and drag across the part you want to loop (Esc cancels)."); return; } LoopSet(loopDragA, loopDragB); };   // a click without a drag is not a loop
        }
        g.Restore(gs); g.Clip = cs;
    }
}
