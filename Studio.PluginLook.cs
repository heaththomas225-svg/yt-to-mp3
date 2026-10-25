using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

// =====================================================================================================
//  THE PLUGIN WINDOW LOOK: a floating plugin window is a Purphex unit. A galaxy header with the PURPHEX logo and the plugin's name under
//  it, a galaxy behind the controls (nebula light and stars), and every number setting as a knob you turn: drag up or down (Shift for fine
//  steps), double-click for the default, or type the value in the box under it. Choice settings are pill buttons. The colours are the Purphex
//  brand colours whatever the theme (like every plugin and preset window).
// =====================================================================================================
partial class StudioForm
{
    static readonly Color[] KnobCols = { Color.FromArgb(255, 61, 217, 255), Color.FromArgb(255, 255, 92, 200), Color.FromArgb(255, 176, 92, 255), Color.FromArgb(255, 90, 140, 255), Color.FromArgb(255, 122, 240, 194) };
    const float KnobCellW = 96, KnobCellH = 118, ChoiceRowH = 36;
    float knobDragFrac, knobDragY;

    // how tall the knob grid of a plugin is in a window this wide
    internal static float KnobGridHeight(Fx f, float width, int extraSkip = 0)
    {
        var hid = f.Hidden.Length > 0 ? new HashSet<string>(f.Hidden.Split(',')) : new HashSet<string>(); int nums = 0, chs = 0;
        foreach (var p in f.P) { if (hid.Contains(p.Id)) continue; if (p.IsChoice) chs++; else nums++; }
        int cols = Math.Max(1, (int)((width - 12) / KnobCellW)); int rows = (nums + cols - 1) / cols;
        return rows * KnobCellH + chs * ChoiceRowH + 16;
    }

    // a knob: drag up / down to turn it (Shift = fine), double-click = default
    HitReg KnobCtl(Graphics g, float cx, float cy, float d, float frac, Action<float> set, Color col, Action toDefault)
    {
        frac = Math.Max(0, Math.Min(1, frac)); var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); bool hot = Hot(Rc(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8));
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        // the glow and the ring it turns along
        using (var gl = new SolidBrush(Color.FromArgb(hot ? 50 : 26, col))) g.FillEllipse(gl, r.X - 6, r.Y - 6, r.Width + 12, r.Height + 12);
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 3f)) g.DrawArc(pen, r.X - 3, r.Y - 3, d + 6, d + 6, 135, 270);
        using (var pen = new Pen(col, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, r.X - 3, r.Y - 3, d + 6, d + 6, 135, Math.Max(1f, 270f * frac));
        // the body: dark metal with a soft top light
        using (var body = new LinearGradientBrush(r, Color.FromArgb(255, 58, 44, 92), Color.FromArgb(255, 10, 6, 22), 90f)) g.FillEllipse(body, r);
        var inner = new RectangleF(r.X + d * 0.12f, r.Y + d * 0.12f, d * 0.76f, d * 0.76f);
        using (var ib = new LinearGradientBrush(inner, Color.FromArgb(255, 34, 24, 60), Color.FromArgb(255, 16, 10, 32), 90f)) g.FillEllipse(ib, inner);
        using (var pen = new Pen(Color.FromArgb(hot ? 220 : 140, col), 1.2f)) g.DrawEllipse(pen, r);
        // the pointer
        double a = (135 + 270 * frac) * Math.PI / 180; float px = cx + (float)Math.Cos(a) * d * 0.36f, py = cy + (float)Math.Sin(a) * d * 0.36f;
        using (var pen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, cx + (float)Math.Cos(a) * d * 0.12f, cy + (float)Math.Sin(a) * d * 0.12f, px, py);
        using (var dot = new SolidBrush(col)) g.FillEllipse(dot, px - 2.2f, py - 2.2f, 4.4f, 4.4f);
        g.SmoothingMode = oldSm;
        var reg = Reg(Rc(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), () => { knobDragFrac = frac; knobDragY = MousePos.Y; });
        reg.OnDrag = pt => { float k = (ModifierKeys & System.Windows.Forms.Keys.Shift) != 0 ? 800f : 200f; set(Math.Max(0, Math.Min(1, knobDragFrac + (knobDragY - pt.Y) / k))); };
        reg.OnDouble = toDefault; reg.OnWheel = dd => set(Math.Max(0, Math.Min(1, frac + Math.Sign(dd) * 0.02f)));
        return reg;
    }

    // the settings of a plugin as knobs (in a floating window)
    void PaintParamKnobs(Graphics g, Fx f, RectangleF a, List<string> skip)
    {
        var nums = new List<Param>(); var chs = new List<Param>(); foreach (var p in f.P) { if (skip.Contains(p.Id)) continue; if (p.IsChoice) chs.Add(p); else nums.Add(p); }
        int cols = Math.Max(1, (int)((a.Width - 12) / KnobCellW)); float cw = (a.Width - 12) / cols, y0 = a.Y + 6;
        for (int i = 0; i < nums.Count; i++)
        {
            var p = nums[i]; var pp = p; int cx0 = i % cols, ry = i / cols; var cell = Rc(a.X + 6 + cx0 * cw, y0 + ry * KnobCellH, cw, KnobCellH - 6);
            using (var cb = new SolidBrush(Color.FromArgb(70, 12, 6, 28))) using (var cp = RR(Rc(cell.X + 3, cell.Y, cell.Width - 6, cell.Height), 10)) g.FillPath(cb, cp);
            string nm = Win.Up(p.Name); Txt(g, nm, 9, Color.FromArgb(255, 220, 210, 245), Rc(cell.X + 4, cell.Y + 3, cell.Width - 8, 14), StringAlignment.Center, true);
            Color col = KnobCols[i % KnobCols.Length];
            KnobCtl(g, cell.X + cell.Width / 2, cell.Y + 50, 46, PFrac(p), fr => f.Set(pp.Id, PFromFrac(pp, fr)), col, () => f.Set(pp.Id, pp.Def));
            Hint2(Rc(cell.X, cell.Y, cell.Width, 80), nm + ": drag up or down to turn it (Shift for fine steps), double-click for the default, or type a value below");
            ValueBox(g, Rc(cell.X + 8, cell.Y + 84, cell.Width - 16 - (paramTrack != null ? 24 : 0), 24), p.Text, Num(p.Val), s => { float v; if (ParseNum(s, out v)) f.Set(pp.Id, v); });
            if (paramTrack != null) { var lane0 = FindLane(paramTrack, f, p.Id); var atr = paramTrack; Btn(g, Rc(cell.Right - 30, cell.Y + 84, 22, 24), "A", lane0 != null && lane0.On ? 4 : 0, () => OpenAuto(atr, f, pp), 10); }
        }
        float y = y0 + ((nums.Count + cols - 1) / cols) * KnobCellH;
        foreach (var p in chs)
        {
            var pp = p; var row = Rc(a.X + 6, y, a.Width - 12, ChoiceRowH - 6); y += ChoiceRowH;
            Txt(g, Win.Up(p.Name), 11, Color.FromArgb(255, 220, 210, 245), Rc(row.X + 6, row.Y, row.Width * 0.38f, row.Height), StringAlignment.Near, true);
            float aw = paramTrack != null ? 26 : 0; var br = Rc(row.X + row.Width * 0.40f, row.Y + 2, row.Width * 0.60f - 6 - aw, row.Height - 4);
            Btn(g, br, p.Text, 4, () => { pp.Val = (pp.Val + 1) > pp.Max ? pp.Min : pp.Val + 1; f.MarkDirty(); }, 12); var rg = regs[regs.Count - 1]; rg.OnRight = () => { pp.Val = (pp.Val - 1) < pp.Min ? pp.Max : pp.Val - 1; f.MarkDirty(); };
            if (paramTrack != null) { var lane0 = FindLane(paramTrack, f, p.Id); var atr = paramTrack; Btn(g, Rc(row.Right - 22, row.Y + 3, 22, row.Height - 6), "A", lane0 != null && lane0.On ? 4 : 0, () => OpenAuto(atr, f, pp), 10); }
        }
    }

    // a galaxy in a box: a deep sky, nebula light and stars (the same picture every time for the same window)
    void PaintGalaxy(Graphics g, RectangleF r, int seed, float strength)
    {
        var rnd = new Random(seed); var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        Color[] neb = { Color.FromArgb(255, 120, 60, 255), Color.FromArgb(255, 210, 60, 230), Color.FromArgb(255, 60, 130, 255), Color.FromArgb(255, 150, 80, 255) };
        for (int i = 0; i < 5; i++)
        {
            float nx = r.X + (float)rnd.NextDouble() * r.Width, ny = r.Y + (float)rnd.NextDouble() * r.Height, nr = (float)(0.22 + rnd.NextDouble() * 0.3) * Math.Max(r.Width, r.Height); Color c = neb[i % neb.Length];
            using (var pth = new GraphicsPath()) { pth.AddEllipse(nx - nr, ny - nr * 0.5f, nr * 2, nr); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb(Math.Min(255, (int)(105 * strength)), c), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, pth); }
        }
        // a small spiral galaxy in one corner (when there is room)
        if (r.Width > 200 && r.Height > 80)
        {
            float gx = rnd.Next(2) == 0 ? r.Right - r.Height * 0.55f : r.X + r.Height * 0.55f, gy = r.Y + r.Height * 0.45f, gr = Math.Min(60f, r.Height * 0.4f);
            using (var core = new GraphicsPath()) { core.AddEllipse(gx - gr * 0.35f, gy - gr * 0.2f, gr * 0.7f, gr * 0.4f); using (var pb = new PathGradientBrush(core) { CenterColor = Color.FromArgb(Math.Min(255, (int)(230 * strength)), 230, 220, 255), SurroundColors = new[] { Color.FromArgb(0, 120, 90, 255) } }) g.FillPath(pb, core); }
            for (int arm = 0; arm < 2; arm++) for (int k = 0; k < 70; k++) { double t = k / 70.0 * 4.2, ang = t + arm * Math.PI; float rr = (float)(gr * 0.12 + gr * t / 4.2); float sx = gx + (float)Math.Cos(ang) * rr, sy = gy + (float)Math.Sin(ang) * rr * 0.45f; int al = (int)(Math.Min(255, 200 * strength) * (1 - t / 5)); using (var sb = new SolidBrush(Color.FromArgb(Math.Max(0, al), 170, 150, 255))) g.FillEllipse(sb, sx - 1.2f, sy - 1.2f, 2.4f, 2.4f); }
        }
        int stars = Math.Min(260, (int)(r.Width * r.Height / 900));
        for (int i = 0; i < stars; i++)
        {
            float sx = r.X + (float)rnd.NextDouble() * r.Width, sy = r.Y + (float)rnd.NextDouble() * r.Height; bool big = rnd.Next(14) == 0; float sz = big ? 2.6f : 1.5f;
            using (var sb = new SolidBrush(Color.FromArgb(Math.Min(255, (int)((big ? 240 : 170) * strength)), 235, 230, 255))) g.FillEllipse(sb, sx - sz / 2, sy - sz / 2, sz, sz);
            if (big) using (var pen = new Pen(Color.FromArgb(Math.Min(255, (int)(110 * strength)), 220, 210, 255), 0.8f)) { g.DrawLine(pen, sx - 4, sy, sx + 4, sy); g.DrawLine(pen, sx, sy - 4, sx, sy + 4); }   // a little twinkle
        }
        g.SmoothingMode = oldSm;
    }

    // the PURPHEX logo with a plugin's name under it (like PURPHEX / ANALOG EQ)
    void PaintPurphexMark(Graphics g, float x, float y, float h, string sub, Color glow)
    {
        float fs = h * 0.62f; var oldT = g.TextRenderingHint; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using (var f = new Font("Segoe UI Black", fs, FontStyle.Italic, GraphicsUnit.Pixel))
        {
            var sz = g.MeasureString("PURPHEX", f); var tr = new RectangleF(x, y, sz.Width + 4, sz.Height);
            for (int k = 3; k >= 1; k--) using (var gb = new SolidBrush(Color.FromArgb(28, glow))) g.DrawString("PURPHEX", f, gb, x - k * 0.6f, y - k * 0.4f);
            using (var br = new LinearGradientBrush(tr, Color.White, Color.FromArgb(255, 210, 170, 255), 90f)) g.DrawString("PURPHEX", f, br, x, y);
            if (!string.IsNullOrEmpty(sub)) using (var f2 = new Font("Segoe UI Semibold", Math.Max(9.5f, h * 0.29f), FontStyle.Regular, GraphicsUnit.Pixel)) using (var b2 = new SolidBrush(Color.FromArgb(255, 225, 215, 250)))
            { string spaced = string.Join(" ", sub.ToUpperInvariant().ToCharArray().Select(ch => ch.ToString()).ToArray()); g.DrawString(spaced, f2, b2, x + 3, y + sz.Height * 0.74f); }
        }
        g.TextRenderingHint = oldT;
    }
}
