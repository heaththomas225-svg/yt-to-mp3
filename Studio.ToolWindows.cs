using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

// =====================================================================================================
//  TOOL WINDOWS: the 3D sound stage, the live profiler, arrangement intelligence and the history each open in their own window over the
//  studio: drag it by its title, resize it from the corner, FULLSCREEN fills the studio with it (WINDOW puts it back), X closes it.
//  Galaxy look like the plugin windows.
// =====================================================================================================
partial class StudioForm
{
    sealed class ToolWin { public int Kind; public RectangleF R, Before; public bool Full; public int Seq; }
    readonly List<ToolWin> toolWins = new List<ToolWin>(); int toolSeq; ToolWin toolDrag; bool toolResize; PointF toolGrab;
    static string ToolTitle(int k) { return k == 12 ? "3D SOUND STAGE" : k == 13 ? "LIVE PROFILER" : k == 14 ? "ARRANGEMENT" : k == 15 ? "HISTORY" : k == 16 ? "TIME MACHINE" : k == 17 ? "INSTRUMENT AUTOPSY" : "TOOL"; }
    internal void OpenToolWin(int kind)
    {
        CloseMenu(); var w = toolWins.Find(x => x.Kind == kind);
        if (w == null) { float ww = Math.Min(ClientSize.Width - 80, Math.Max(760, ClientSize.Width * 0.72f)), hh = Math.Min(ClientSize.Height - ToolH - 40, Math.Max(480, ClientSize.Height * 0.74f)); int k = toolWins.Count; w = new ToolWin { Kind = kind, R = new RectangleF((ClientSize.Width - ww) / 2 + k * 26, ToolH + 20 + k * 26, ww, hh) }; toolWins.Add(w); }
        w.Seq = ++toolSeq; if (tab != 0) tab = 0; Invalidate();
    }
    void PaintToolWindows(Graphics g)
    {
        if (toolWins.Count == 0 || tab != 0) return; TrimWinPics(new HashSet<object>(fxWins.Cast<object>().Concat(toolWins.Cast<object>())));
        foreach (var w in toolWins.OrderBy(x => x.Seq).ToList())
        {
            if (w.Full) w.R = Rc(4, ToolH + 4, ClientSize.Width - 8, ClientSize.Height - ToolH - 8);
            var r = w.R; r.Width = Math.Max(520, Math.Min(ClientSize.Width - 8, r.Width)); r.Height = Math.Max(360, Math.Min(ClientSize.Height - ToolH - 8, r.Height)); r.X = Math.Max(-r.Width + 140, Math.Min(ClientSize.Width - 140, r.X)); r.Y = Math.Max(ToolH, Math.Min(ClientSize.Height - 50, r.Y)); w.R = r;
            var wq = w; PaintCachedWin(g, w, r, "tool|" + w.Kind + "|" + w.Full + "|" + (w.Seq == toolSeq) + "|" + selTrack + "|" + eng.Tracks.Count + "|" + eng.Playing + "|" + (toolDrag != null), 250, gg => PaintToolWin(gg, wq));
        }
    }
    void PaintToolWin(Graphics g, ToolWin w)
    {
        {
            var r = w.R; bool front = w.Seq == toolSeq; Color acc = front ? NeonCyan : NeonViolet; const float th = 46;
            using (var sh = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(sh, r.X + 8, r.Y + 10, r.Width, r.Height);
            using (var p = RR(r, 14)) { using (var b = new SolidBrush(TM.C(255, 8, 3, 20))) g.FillPath(b, p); var gs = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect); PaintGalaxy(g, r, w.Kind * 7919, 0.75f); g.Restore(gs); }
            Glow(g, acc, r, 14, front ? 0.9f : 0.4f, 3); StrokeRR(g, Color.FromArgb(front ? 230 : 140, acc), r, 14, 1.5f);
            Reg(r, () => { w.Seq = ++toolSeq; }).Overlay = true; regs.Add(new HitReg { R = r, OnWheel = d => { } });
            // the title bar: drag to move
            var title = Rc(r.X, r.Y, r.Width, th); using (var p = RR(Rc(r.X + 1, r.Y + 1, r.Width - 2, th), 13)) using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(title, TM.C(255, 22, 9, 46), TM.C(255, 9, 4, 22), 90f)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(160, acc), 1.1f)) g.DrawLine(pen, r.X + 10, r.Y + th, r.Right - 10, r.Y + th);
            using (var f = new Font("Segoe UI Black", 20f, FontStyle.Italic, GraphicsUnit.Pixel)) { var oldSm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; GalaxyText(g, ToolTitle(w.Kind), f, r.X + 16, r.Y + 11); g.SmoothingMode = oldSm; }
            var ww0 = w; var tr = Reg(Rc(r.X, r.Y, r.Width - 230, th), () => { ww0.Seq = ++toolSeq; if (!ww0.Full) { toolDrag = ww0; toolResize = false; toolGrab = new PointF(MousePos.X - ww0.R.X, MousePos.Y - ww0.R.Y); } });
            tr.OnDrag = pt => { if (toolDrag == ww0 && !toolResize) ww0.R = new RectangleF(pt.X - toolGrab.X, pt.Y - toolGrab.Y, ww0.R.Width, ww0.R.Height); }; tr.OnUp = () => toolDrag = null; tr.OnDouble = () => ToggleToolFull(ww0);
            var cl = Rc(r.Right - 44, r.Y + 8, 32, 30); Btn(g, cl, "X", 3, () => toolWins.Remove(ww0), 12); Hint2(cl, "Close this window");
            var fs = Rc(cl.X - 132, r.Y + 8, 124, 30); Btn(g, fs, w.Full ? "WINDOW" : "FULLSCREEN", w.Full ? 4 : 1, () => ToggleToolFull(ww0), 11); Hint2(fs, w.Full ? "Back to a window you can move" : "Fill the studio with this window (double-click the title does the same)");
            // the page itself
            var body = Rc(r.X + 10, r.Y + th + 8, r.Width - 20, r.Height - th - 18); var gs2 = g.Save(); g.SetClip(body, System.Drawing.Drawing2D.CombineMode.Intersect); int reg0 = regs.Count;
            try { if (w.Kind == 12) PaintStage3D(g, body); else if (w.Kind == 13) PaintProfiler(g, body); else if (w.Kind == 14) PaintArrangement(g, body); else if (w.Kind == 15) PaintHistory(g, body); else if (w.Kind == 16) PaintTimeMachine(g, body); else if (w.Kind == 17) PaintAutopsy(g, body); } finally { g.Restore(gs2); }
            for (int i = regs.Count - 1; i >= reg0; i--) { var rr = regs[i]; if (!rr.R.IntersectsWith(body)) regs.RemoveAt(i); else rr.R = RectangleF.Intersect(rr.R, body); }
            // resize from the corner
            if (!w.Full) { var grip = Rc(r.Right - 20, r.Bottom - 20, 18, 18); using (var pen = new Pen(Color.FromArgb(200, acc), 1.6f)) { g.DrawLine(pen, grip.Right - 4, grip.Y + 4, grip.X + 4, grip.Bottom - 4); g.DrawLine(pen, grip.Right - 4, grip.Y + 10, grip.X + 10, grip.Bottom - 4); }
              var gr = Reg(grip, () => { toolDrag = ww0; toolResize = true; }); gr.OnDrag = pt => { if (toolDrag == ww0 && toolResize) ww0.R = new RectangleF(ww0.R.X, ww0.R.Y, pt.X - ww0.R.X + 6, pt.Y - ww0.R.Y + 6); }; gr.OnUp = () => { toolDrag = null; toolResize = false; }; }
        }
    }
    void ToggleToolFull(ToolWin w) { if (w.Full) { w.Full = false; w.R = w.Before; } else { w.Before = w.R; w.Full = true; } w.Seq = ++toolSeq; }
}
