using System;
using System.Drawing;
using System.Drawing.Drawing2D;

// Envelope editor: drag the three handles. Left/right on the first moves ATTACK; the second sets DECAY (left/right) and SUSTAIN (up/down); the third sets RELEASE.
partial class SynthCanvas
{
    void PaintEnv(Graphics g, RectangleF pn, int k)
    {
        var disp = Rc(pn.X + 12, pn.Y + 12, 560, 250); Fill(g, Disp, disp, 6);
        double a = P("e" + k + "a").Val, d = P("e" + k + "d").Val, s = P("e" + k + "s").Val / 100.0, rr = P("e" + k + "r").Val;
        double wa = Math.Sqrt(a + 20), wd = Math.Sqrt(d + 20), wh = Math.Sqrt(300), wr = Math.Sqrt(rr + 20), tot = wa + wd + wh + wr; float iw = disp.Width - 24, ih = disp.Height - 56; float x0 = disp.X + 12, yb = disp.Bottom - 14;
        PointF p0 = new PointF(x0, yb), p1 = new PointF(x0 + (float)(wa / tot) * iw, yb - ih), p2 = new PointF(p1.X + (float)(wd / tot) * iw, yb - (float)s * ih), p3 = new PointF(p2.X + (float)(wh / tot) * iw, p2.Y), p4 = new PointF(p3.X + (float)(wr / tot) * iw, yb);
        var poly = new[] { p0, p1, p2, p3, p4 };
        using (var pen = new Pen(Color.FromArgb(40, 150, 130, 200))) { for (int i = 1; i < 8; i++) g.DrawLine(pen, x0 + iw * i / 8f, yb - ih, x0 + iw * i / 8f, yb); for (int i = 0; i <= 4; i++) g.DrawLine(pen, x0, yb - ih * i / 4f, x0 + iw, yb - ih * i / 4f); }
        using (var b = new SolidBrush(Color.FromArgb(45, Acc2))) g.FillPolygon(b, poly); using (var pen = new Pen(Color.FromArgb(70, Acc2), 7f)) g.DrawLines(pen, poly); using (var pen = new Pen(Acc2, 2.5f)) g.DrawLines(pen, poly);
        Txt(g, k == 1 ? "AMPLITUDE ENVELOPE: drag the dots. This shapes the volume of every note." : "ENVELOPE " + k + ": drag the dots. Then drag the DRAG chip onto a knob to use it.", 11, Dim, Rc(disp.X + 10, disp.Y + 4, disp.Width - 20, 16), StringAlignment.Near, false);
        Txt(g, "ATTACK " + a.ToString("0") + " ms   DECAY " + d.ToString("0") + " ms   SUSTAIN " + (s * 100).ToString("0") + "%   RELEASE " + rr.ToString("0") + " ms", 11, Ink, Rc(disp.X + 10, disp.Y + 20, disp.Width - 20, 16), StringAlignment.Near, true);
        float tot0 = (float)tot; string ka = "e" + k + "a", kd = "e" + k + "d", ks = "e" + k + "s", kr = "e" + k + "r"; float p1x = p1.X, p3x = p3.X;
        Handle(g, disp, p1, "ATTACK: drag left / right", pt => { double w = Math.Max(0, (mouse.X - x0) / iw * tot0); SetVal(P(ka), (float)Math.Max(0, Math.Min(5000, w * w - 20))); });
        Handle(g, disp, p2, "DECAY (left / right) and SUSTAIN (up / down)", pt => { double w = Math.Max(0, (mouse.X - p1x) / iw * tot0); SetVal(P(kd), (float)Math.Max(1, Math.Min(8000, w * w - 20))); SetVal(P(ks), (float)Math.Round(Math.Max(0, Math.Min(100, (yb - mouse.Y) / ih * 100)))); });
        Handle(g, disp, p4, "RELEASE: drag left / right", pt => { double w = Math.Max(0, (mouse.X - p3x) / iw * tot0); SetVal(P(kr), (float)Math.Max(1, Math.Min(8000, w * w - 20))); });
        float kx = pn.X + 640; Knob(g, "e" + k + "a", kx, pn.Y + 80, "ATTACK", Acc2, 24); Knob(g, "e" + k + "d", kx + 120, pn.Y + 80, "DECAY", Acc2, 24); Knob(g, "e" + k + "s", kx + 240, pn.Y + 80, "SUSTAIN", Acc, 24); Knob(g, "e" + k + "r", kx + 360, pn.Y + 80, "RELEASE", Acc, 24);
        if (k > 1) SourceChip(g, Rc(kx - 30, pn.Y + 124, 150, 22), 5 + (k - 2), "Envelope " + k);
        if (k > 1) QuickAssign(g, Rc(kx - 30, pn.Y + 150, 480, 100), 5 + (k - 2), "Envelope " + k);
    }

    void Handle(Graphics g, RectangleF disp, PointF c, string tip, Action<PointF> move)
    {
        var r = Rc(c.X - 9, c.Y - 9, 18, 18); bool hot = Hot(r) || (active != null && active.R == r);
        using (var b = new SolidBrush(Color.FromArgb(hot ? 90 : 40, Acc2))) g.FillEllipse(b, c.X - 11, c.Y - 11, 22, 22);
        using (var b = new SolidBrush(hot ? Color.White : Acc2)) g.FillEllipse(b, c.X - 6, c.Y - 6, 12, 12); using (var pen = new Pen(Color.White, 1.2f)) g.DrawEllipse(pen, c.X - 6, c.Y - 6, 12, 12);
        var rg = Add(r); rg.Tip = tip; rg.Down = pt => move(pt); rg.Drag = (dx, dy) => move(mouse);
    }
}
