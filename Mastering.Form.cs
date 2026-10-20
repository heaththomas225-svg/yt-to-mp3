using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  The window. Designed in a 1400x880 space and scaled by the View zoom.
// ======================================================================
class MasterForm : Form
{
    App app;
    MasterSettings ms { get { return app.Master; } }
    float z = 0.75f;
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    float t = 0;
    Random rnd = new Random();
    List<Leaf> leaves = new List<Leaf>();
    List<Spore> spores = new List<Spore>();

    // history / A-B
    List<string> undo = new List<string>(), redo = new List<string>();
    bool comparing = false; string cmpHold = "";
    string presetName = "Manual";
    int selNode = 2;

    // drag state
    int drag = 0; float dragY, dragVal; string dragBefore = "";
    volatile bool analyzing = false; string analyzeText = "";

    // ---- purple theme (from the main window) ----
    static Color Accent = TM.C(255, 190, 110, 255);
    static Color Accent2 = TM.C(255, 235, 90, 230);
    static Color AccentHi = TM.C(255, 232, 190, 255);
    static Color Violet = TM.C(255, 124, 52, 214);
    static Color PanelC = TM.C(205, 28, 11, 54);
    static Color PanelEdge = TM.C(120, 150, 80, 220);
    internal static void ApplyTheme(ThemeDef t) { Accent = t.Accent; Accent2 = t.Accent2; AccentHi = Color.FromArgb(Math.Min(255, t.Accent.R + 54), Math.Min(255, t.Accent.G + 54), Math.Min(255, t.Accent.B + 54)); Violet = t.Btn; PanelC = Color.FromArgb(205, t.Panel.R + 8, t.Panel.G + 4, t.Panel.B + 14); PanelEdge = Color.FromArgb(120, t.EdgeC); }
    static Color Dim { get { return TM.C(190, 175, 150, 215); } }
    static Color White { get { return TM.C(255, 244, 236, 255); } }
    static Color Chrome { get { return TM.C(255, 24, 10, 44); } }
    static Color Pink { get { return TM.C(255, 255, 120, 200); } }

    // ---- layout (design space) ----
    static readonly RectangleF PresetR = new RectangleF(92, 44, 368, 28);
    static readonly RectangleF NavL = new RectangleF(92, 84, 45, 28), NavR = new RectangleF(137, 84, 45, 28);
    static readonly RectangleF CompareR = new RectangleF(197, 84, 94, 28);
    static readonly RectangleF CopyR = new RectangleF(304, 84, 76, 28), PasteR = new RectangleF(380, 84, 80, 28);
    static readonly RectangleF UndoR = new RectangleF(474, 84, 77, 28), RedoR = new RectangleF(551, 84, 78, 28);
    static readonly RectangleF ViewR = new RectangleF(1230, 82, 88, 30);
    static readonly RectangleF CharR = new RectangleF(16, 138, 186, 78);
    static readonly RectangleF Plot = new RectangleF(147, 272, 601, 400);
    static readonly RectangleF CustomR = new RectangleF(16, 775, 97, 43);
    static readonly RectangleF DynR = new RectangleF(826, 138, 558, 382);
    static readonly RectangleF ExciteR = new RectangleF(891, 366, 60, 42);
    static readonly RectangleF StartR = new RectangleF(1244, 449, 60, 42), ResetR = new RectangleF(1310, 449, 60, 42);
    static readonly RectangleF SpreadR = new RectangleF(826, 531, 558, 230);
    static readonly RectangleF ReanR = new RectangleF(826, 775, 130, 43);
    static readonly RectangleF BypassR = new RectangleF(1106, 775, 135, 43), LoudR = new RectangleF(1248, 775, 136, 43);
    const float SlX = 70, SlY1 = 262, SlY2 = 680;
    static readonly PointF KnobL = new PointF(920, 285), KnobW = new PointF(920, 678);
    const float KnobR = 52;

    // presets
    static readonly string[] PresetNames = {
        "Streaming  (-14 LUFS)", "Loud / Club  (-9 LUFS)", "Gentle glue  (-16 LUFS)", "Warm tape",
        "Bright & airy", "Vocal forward", "Wide & punchy" };
    static readonly string[] PresetData = {
        "on=1;char=3;auto=100;custom=0;loud=-14;excite=0;width=100;comp=0",
        "on=1;char=2;auto=70;custom=0;loud=-9;excite=1;width=100;comp=0",
        "on=1;char=0;auto=60;custom=0;loud=-16;excite=0;width=100;comp=0",
        "on=1;char=1;auto=80;custom=1;loud=-14;excite=0;width=100;comp=0;nodes=80:2,250:1,1000:0,4000:-1,10000:-2",
        "on=1;char=3;auto=100;custom=1;loud=-14;excite=1;width=100;comp=0;nodes=80:0,250:0,1000:0,4000:2,10000:4",
        "on=1;char=0;auto=80;custom=1;loud=-14;excite=0;width=100;comp=0;nodes=80:-1,250:-1,1000:1,4000:3,10000:1",
        "on=1;char=2;auto=80;custom=0;loud=-12;excite=0;width=130;comp=0" };

    public MasterForm(App owner)
    {
        app = owner;
        z = Math.Max(0.4f, Math.Min(1.0f, app.MZoomPct / 100f));
        Text = "Purphex Mastering"; Win.Dark(this);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ApplyZoom();
        for (int i = 0; i < 10; i++) leaves.Add(NewLeaf(true));
        for (int i = 0; i < 30; i++) spores.Add(NewSpore(true));
        timer.Interval = 33; timer.Tick += (s, e) => { if (moving || Win.AnyMoving) return; if (ActiveForm != this && (++idleTick & 3) != 0) return; t += 0.033f; StepAnim(); Invalidate(); };
        timer.Start();
        FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); if (ms.On) { ms.On = false; app.SaveSettings(); } };   // closing the Mastering window turns mastering off
    }

    int idleTick; volatile bool moving;
    protected override void WndProc(ref Message m)
    {
        Win.TrackMove(ref m); if (m.Msg == 0x231) moving = true; else if (m.Msg == 0x232) moving = false;
        base.WndProc(ref m);
    }

    void ApplyZoom() { ClientSize = new Size((int)(1400 * z), (int)(880 * z)); }

    Leaf NewLeaf(bool any)
    {
        Color[] pal = { TM.C(255, 170, 90, 255), TM.C(255, 140, 60, 230), TM.C(255, 195, 125, 255), TM.C(255, 120, 50, 200) };
        var l = new Leaf();
        l.S = 0.5f + (float)rnd.NextDouble() * 1.2f;
        l.X = (float)rnd.NextDouble() * 1400;
        l.Y = any ? (float)rnd.NextDouble() * 880 : -200;
        l.Rot = (float)rnd.NextDouble() * 360; l.Spin = ((float)rnd.NextDouble() - 0.5f) * 0.8f;
        l.VY = 0.4f + l.S * 0.7f; l.Phase = (float)rnd.NextDouble() * 6.28f; l.Sway = 0.3f + (float)rnd.NextDouble() * 0.6f;
        l.A = 22 + (int)(l.S * 22); l.C = pal[rnd.Next(pal.Length)];
        return l;
    }

    Spore NewSpore(bool any)
    {
        var s = new Spore();
        s.X = (float)rnd.NextDouble() * 1400; s.Y = any ? (float)rnd.NextDouble() * 880 : 885;
        s.R = 1.5f + (float)rnd.NextDouble() * 3f; s.VY = 0.3f + (float)rnd.NextDouble();
        s.Phase = (float)rnd.NextDouble() * 6.28f;
        return s;
    }

    void StepAnim()
    {
        foreach (var l in leaves)
        {
            l.Y += l.VY; l.X += (float)Math.Sin(t * 0.8f + l.Phase) * l.Sway; l.Rot += l.Spin;
            if (l.Y > 1100) { var n = NewLeaf(false); l.X = n.X; l.Y = n.Y; l.S = n.S; l.VY = n.VY; l.A = n.A; l.C = n.C; }
        }
        foreach (var s in spores)
        {
            s.Y -= s.VY; s.X += (float)Math.Sin(t + s.Phase) * 0.5f;
            if (s.Y < -5) { var n = NewSpore(false); s.X = n.X; s.Y = n.Y; }
        }
    }

    // ---------- coordinate helpers ----------
    static float FX(double f) { return Plot.X + Plot.Width * (float)(Math.Log10(f / 20.0) / 3.0); }
    static float GY(double db) { return Plot.Y + Plot.Height * (float)((12 - db) / 24.0); }
    static double FromX(float x) { return 20.0 * Math.Pow(10, (x - Plot.X) / Plot.Width * 3.0); }
    static double FromY(float y) { return 12 - (y - Plot.Y) / Plot.Height * 24.0; }

    // ---------- drawing helpers ----------
    static GraphicsPath RR(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static void FillRR(Graphics g, Color c, RectangleF r, float rad)
    {
        using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
    }

    static void StrokeRR(Graphics g, Color c, RectangleF r, float rad, float w)
    {
        using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
    }

    static void Txt(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    {
        using (var b = new SolidBrush(c)) g.DrawString(s, GfxCache.Font(px, bold), b, r, GfxCache.Fmt(ha, false));
    }

    static RectangleF At(float cx, float cy, float w, float h) { return new RectangleF(cx - w / 2, cy - h / 2, w, h); }

    void Btn(Graphics g, RectangleF r, string text, bool on, Color onColor, Color textColor)
    {
        text = Win.Up(text);
        FillRR(g, on ? onColor : TM.C(150, 52, 28, 92), r, 5);
        Txt(g, text, 17, textColor, r, StringAlignment.Center, false);
    }

    void Knob(Graphics g, PointF c, float val01, bool centerRef)
    {
        var ring = new RectangleF(c.X - KnobR - 9, c.Y - KnobR - 9, (KnobR + 9) * 2, (KnobR + 9) * 2);
        using (var pen = new Pen(TM.C(120, 60, 30, 100), 7))
            g.DrawArc(pen, ring, 135, 270);
        float a0 = centerRef ? 270 : 135, a1 = 135 + 270 * val01;
        float sweep = a1 - a0;
        if (Math.Abs(sweep) > 0.5f)
        {
            using (var glow = new Pen(Color.FromArgb(70, Accent), 13)) g.DrawArc(glow, ring, a0, sweep);
            using (var pen = new Pen(Accent, 7)) g.DrawArc(pen, ring, a0, sweep);
        }
        var body = new RectangleF(c.X - KnobR, c.Y - KnobR, KnobR * 2, KnobR * 2);
        using (var br = new LinearGradientBrush(body, TM.C(255, 62, 30, 104), TM.C(255, 30, 12, 58), 90f))
            g.FillEllipse(br, body);
        using (var pen = new Pen(TM.C(150, 130, 80, 200), 2)) g.DrawEllipse(pen, body);
        double ang = (135 + 270 * val01) * Math.PI / 180.0;
        using (var pen = new Pen(AccentHi, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, c.X + (float)Math.Cos(ang) * KnobR * 0.35f, c.Y + (float)Math.Sin(ang) * KnobR * 0.35f,
                c.X + (float)Math.Cos(ang) * KnobR * 0.88f, c.Y + (float)Math.Sin(ang) * KnobR * 0.88f);
    }

    void BarMeter(Graphics g, float x, float y, float w, float h, double v, double lo, double hi)
    {
        FillRR(g, TM.C(150, 12, 4, 30), new RectangleF(x, y, w, h), h / 2);
        if (double.IsNaN(v)) return;
        float fw = (float)(w * Math.Max(0, Math.Min(1, (v - lo) / (hi - lo))));
        if (fw < 2) return;
        var r = new RectangleF(x, y, fw, h);
        using (var br = new LinearGradientBrush(new RectangleF(x, y, w, h), Violet, Accent2, 0f))
        using (var p = RR(r, h / 2)) g.FillPath(br, p);
    }

    static string Num(double v, string fmtStr) { return double.IsNaN(v) ? "-" : v.ToString(fmtStr, CultureInfo.InvariantCulture); }

    // ---------- main paint ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.ScaleTransform(z, z);

        // background: the main window's deep-purple gradient, glows, leaves and spores
        var full = new RectangleF(0, 0, 1400, 880);
        using (var br = new LinearGradientBrush(full, TM.C(255, 14, 5, 30), TM.C(255, 48, 12, 86), 70f))
            g.FillRectangle(br, full);
        for (int i = 0; i < 2; i++)
        {
            float cx = 1400 * (0.3f + 0.4f * i) + (float)Math.Sin(t * 0.4f + i * 2) * 90;
            float cy = 880 * (0.35f + 0.3f * i) + (float)Math.Cos(t * 0.3f + i) * 70;
            float rad = 330 + (float)Math.Sin(t * 0.9f + i) * 40;
            var gb = Glow(); var o1 = g.CompositingQuality; var o2 = g.PixelOffsetMode; var o3 = g.SmoothingMode; var o4 = g.InterpolationMode;
            g.CompositingQuality = CompositingQuality.HighSpeed; g.PixelOffsetMode = PixelOffsetMode.HighSpeed; g.SmoothingMode = SmoothingMode.HighSpeed; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(gb, new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), new RectangleF(0, 0, gb.Width, gb.Height), GraphicsUnit.Pixel);
            g.CompositingQuality = o1; g.PixelOffsetMode = o2; g.SmoothingMode = o3; g.InterpolationMode = o4;
        }
        leaves.Sort((a, b) => a.S.CompareTo(b.S));
        foreach (var l in leaves) App.DrawLeaf(g, l);
        foreach (var s in spores)
        {
            int a = (int)(60 + 90 * (Math.Sin(t * 2 + s.Phase) * 0.5 + 0.5));
            using (var b = new SolidBrush(TM.C(a, 215, 170, 255))) g.FillEllipse(b, s.X, s.Y, s.R * 2, s.R * 2);
        }

        PaintTopBar(g);
        PaintLeft(g);
        PaintEq(g);
        PaintDynamics(g);
        PaintSpread(g);
        PaintBottom(g);
    }

    static Bitmap glowBmp;
    static Bitmap Glow()
    {
        if (glowBmp != null) return glowBmp;
        var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var bg = Graphics.FromImage(bmp)) using (var gp = new GraphicsPath())
        {
            bg.SmoothingMode = SmoothingMode.AntiAlias; gp.AddEllipse(0, 0, 256, 256);
            using (var pg = new PathGradientBrush(gp)) { pg.CenterColor = TM.C(70, 150, 60, 240); pg.SurroundColors = new[] { TM.C(0, 150, 60, 240) }; bg.FillPath(pg, gp); }
        }
        glowBmp = bmp; return bmp;
    }

    void PaintTopBar(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(215, Chrome))) g.FillRectangle(br, 0, 0, 1400, 125);
        using (var pen = new Pen(TM.C(140, 150, 80, 220))) g.DrawLine(pen, 0, 125, 1400, 125);
        Txt(g, "PURPHEX MASTERING", 22, White, new RectangleF(400, 6, 600, 30), StringAlignment.Center, true);

        // power button (pulses while on)
        var pc = At(49, 78, 52, 52);
        if (ms.On)
        {
            float pulse = (float)(Math.Sin(t * 3) * 0.5 + 0.5);
            using (var gl = new SolidBrush(Color.FromArgb((int)(40 + 50 * pulse), Accent))) g.FillEllipse(gl, At(49, 78, 70, 70));
        }
        using (var br = new SolidBrush(ms.On ? Violet : TM.C(255, 70, 55, 90))) g.FillEllipse(br, pc);
        using (var pen = new Pen(Color.White, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawArc(pen, 49 - 12, 78 - 12, 24, 24, -55, 290);
            g.DrawLine(pen, 49, 78 - 16, 49, 78);
        }

        FillRR(g, TM.C(160, 52, 28, 92), PresetR, 5);
        Txt(g, presetName, 18, Dim, new RectangleF(PresetR.X + 12, PresetR.Y, 300, PresetR.Height), StringAlignment.Near, false);
        using (var pen = new Pen(Dim, 2)) { g.DrawLine(pen, PresetR.Right - 24, 55, PresetR.Right - 18, 61); g.DrawLine(pen, PresetR.Right - 18, 61, PresetR.Right - 12, 55); }

        FillRR(g, TM.C(150, 52, 28, 92), new RectangleF(NavL.X, NavL.Y, 90, 28), 5);
        using (var pen = new Pen(White, 2.2f))
        {
            g.DrawLine(pen, NavL.X + 26, NavL.Y + 8, NavL.X + 18, NavL.Y + 14); g.DrawLine(pen, NavL.X + 18, NavL.Y + 14, NavL.X + 26, NavL.Y + 20);
            g.DrawLine(pen, NavR.X + 19, NavR.Y + 8, NavR.X + 27, NavR.Y + 14); g.DrawLine(pen, NavR.X + 27, NavR.Y + 14, NavR.X + 19, NavR.Y + 20);
        }
        Btn(g, CompareR, "Compare", comparing, Violet, White);
        Btn(g, CopyR, "Copy", false, Violet, White); Btn(g, PasteR, "Paste", false, Violet, Dim);
        Btn(g, UndoR, "Undo", false, Violet, undo.Count > 0 ? White : Dim);
        Btn(g, RedoR, "Redo", false, Violet, redo.Count > 0 ? White : Dim);

        Txt(g, "View:", 18, Dim, new RectangleF(1150, 82, 74, 30), StringAlignment.Far, false);
        FillRR(g, TM.C(150, 52, 28, 92), ViewR, 5);
        Txt(g, app.MZoomPct + "%", 18, White, ViewR, StringAlignment.Center, false);
    }

    void Panel(Graphics g, RectangleF r)
    {
        FillRR(g, PanelC, r, 10);
        StrokeRR(g, PanelEdge, r, 10, 1.2f);
    }

    void PaintLeft(Graphics g)
    {
        Panel(g, CharR);
        Txt(g, "CHARACTER", 16, Dim, new RectangleF(CharR.X + 22, CharR.Y + 14, 150, 22), StringAlignment.Near, false);
        Txt(g, MasterSettings.CharNames[ms.Character], 21, Accent, new RectangleF(CharR.X + 22, CharR.Y + 38, 140, 32), StringAlignment.Near, true);
        using (var pen = new Pen(Accent, 2))
        {
            g.DrawLine(pen, CharR.Right - 26, CharR.Y + 44, CharR.Right - 20, CharR.Y + 38); g.DrawLine(pen, CharR.Right - 20, CharR.Y + 38, CharR.Right - 14, CharR.Y + 44);
            g.DrawLine(pen, CharR.Right - 26, CharR.Y + 52, CharR.Right - 20, CharR.Y + 58); g.DrawLine(pen, CharR.Right - 20, CharR.Y + 58, CharR.Right - 14, CharR.Y + 52);
        }

        // Auto EQ vertical slider
        FillRR(g, TM.C(150, 12, 4, 30), new RectangleF(SlX - 3, SlY1, 6, SlY2 - SlY1), 3);
        float hy = SlY2 - (SlY2 - SlY1) * ms.AutoEq / 100f;
        FillRR(g, Accent, new RectangleF(SlX - 3, hy, 6, SlY2 - hy), 3);
        using (var pen = new Pen(Color.FromArgb(90, Dim), 1.5f))
            for (int i = 0; i < 5; i++) { float ty = SlY1 + (SlY2 - SlY1) * i / 4f; g.DrawLine(pen, SlX + 14, ty, SlX + 24, ty); }
        var hr = new RectangleF(SlX - 19, hy - 7, 38, 14);
        using (var gl = new SolidBrush(Color.FromArgb(60, Accent))) g.FillEllipse(gl, At(SlX, hy, 56, 30));
        FillRR(g, AccentHi, hr, 6);
        Txt(g, "AUTO EQ", 17, Dim, At(SlX, 711, 110, 24), StringAlignment.Center, false);
        Txt(g, ((int)ms.AutoEq) + " %", 22, Accent, At(SlX + 2, 735, 110, 28), StringAlignment.Center, false);

        // Custom EQ toggle
        Btn(g, CustomR, "Custom EQ", ms.Custom, Violet, White);
    }

    void PaintEq(Graphics g)
    {
        // grid
        double[] decades = { 20, 30, 40, 50, 60, 70, 80, 90, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000,
            2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000, 20000 };
        using (var pen = new Pen(TM.C(34, 190, 140, 255), 1))
            foreach (var f in decades) { float x = FX(f); g.DrawLine(pen, x, Plot.Y - 16, x, Plot.Bottom + 4); }
        using (var pen = new Pen(Color.FromArgb(120, Dim), 1.4f) { DashStyle = DashStyle.Dot })
            g.DrawLine(pen, Plot.X, GY(0), Plot.Right + 10, GY(0));
        double[] lab = { 20, 50, 100, 300, 700, 2000, 5000, 10000 };
        string[] labT = { "20", "50", "100", "300", "700", "2k", "5k", "10k" };
        for (int i = 0; i < lab.Length; i++)
            Txt(g, labT[i], 17, Dim, At(FX(lab[i]), GY(0) + 20, 50, 22), StringAlignment.Center, false);
        int[] dbl = { 12, 6, 0, -6, -12 };
        foreach (var d in dbl) Txt(g, d.ToString(), 17, Color.FromArgb(150, Dim), new RectangleF(Plot.Right + 20, GY(d) - 11, 50, 22), StringAlignment.Near, false);

        // curves
        int n = 260;
        var total = new PointF[n]; var auto = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double f = 20 * Math.Pow(1000, i / (double)(n - 1));
            double a = MasterDsp.AutoResp(ms, app.LastAn, f), c = MasterDsp.CustomResp(ms, f);
            total[i] = new PointF(FX(f), GY(Math.Max(-12, Math.Min(12, a + c))));
            auto[i] = new PointF(FX(f), GY(Math.Max(-12, Math.Min(12, a))));
        }
        float pulse = (float)(Math.Sin(t * 2.2) * 0.5 + 0.5);
        using (var pen = new Pen(Color.FromArgb((int)(40 + 40 * pulse), Accent), 11) { LineJoin = LineJoin.Round }) g.DrawLines(pen, total);
        using (var pen = new Pen(Accent, 3.2f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, total);
        if (app.LastAn != null && app.LastAn.HasBands)
            using (var pen = new Pen(Color.FromArgb(190, AccentHi), 2) { DashStyle = DashStyle.Dot }) g.DrawLines(pen, auto);

        // nodes
        for (int i = 0; i < 5; i++)
        {
            float x = FX(ms.NF[i]), y = GY(ms.NG[i]);
            bool sel = i == selNode;
            if (sel) using (var gl = new SolidBrush(Color.FromArgb(70, Accent2))) g.FillEllipse(gl, At(x, y, 46, 46));
            using (var br = new SolidBrush(sel ? Accent2 : (ms.Custom ? TM.C(255, 150, 90, 200) : TM.C(120, 70, 50, 110))))
                g.FillEllipse(br, At(x, y, sel ? 24 : 18, sel ? 24 : 18));
            using (var pen = new Pen(Color.FromArgb(200, AccentHi), 1.6f)) g.DrawEllipse(pen, At(x, y, sel ? 24 : 18, sel ? 24 : 18));
        }

        string nm = MasterSettings.NodeNames[selNode];
        Txt(g, nm + " Freq", 17, Dim, At(413, 785, 140, 24), StringAlignment.Center, false);
        Txt(g, ms.NF[selNode].ToString("0.0", CultureInfo.InvariantCulture) + " Hz", 23, Accent, At(413, 807, 160, 28), StringAlignment.Center, false);
        Txt(g, nm + " Gain", 17, Dim, At(552, 785, 140, 24), StringAlignment.Center, false);
        Txt(g, ms.NG[selNode].ToString("0.0", CultureInfo.InvariantCulture) + " dB", 23, Accent, At(552, 807, 160, 28), StringAlignment.Center, false);
    }

    void PaintDynamics(Graphics g)
    {
        Panel(g, DynR);
        Txt(g, "DYNAMICS", 24, White, new RectangleF(853, 156, 200, 32), StringAlignment.Near, true);
        Txt(g, ms.Comp ? "Matched" : "Loudness", 17, Dim, At(920, 212, 120, 24), StringAlignment.Center, false);
        Knob(g, KnobL, (ms.Loud + 20f) / 14f, false);
        Txt(g, ms.Comp ? "= input" : Num(ms.Loud, "0.0") + " LUFS", 16, Accent, At(920, 346, 130, 22), StringAlignment.Center, false);
        Btn(g, ExciteR, "Excite", ms.Excite, Violet, ms.Excite ? White : AccentHi);

        var an = app.LastAn;
        // true peak
        Txt(g, "TRUE PEAK", 17, Dim, new RectangleF(1068, 200, 150, 24), StringAlignment.Near, false);
        double tp = an == null ? double.NaN : an.TP;
        BarMeter(g, 1068, 231, 232, 7, tp, -60, 6); BarMeter(g, 1068, 243, 232, 7, tp, -60, 6);
        Txt(g, "-60", 15, Dim, At(1068, 273, 40, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1068 + 232 * 60f / 66f, 273, 30, 20), StringAlignment.Center, false);
        Txt(g, "6", 15, Dim, At(1300, 273, 24, 20), StringAlignment.Far, false);
        FillRR(g, TM.C(150, 12, 4, 30), new RectangleF(1315, 222, 55, 32), 5);
        Txt(g, Num(tp, "0.0"), 15, !double.IsNaN(tp) && tp > -1 ? Pink : White, new RectangleF(1315, 222, 55, 32), StringAlignment.Center, false);

        // LUFS meters
        Txt(g, "LUFS", 17, Dim, new RectangleF(1068, 308, 100, 24), StringAlignment.Near, false);
        string[] ml = { "M", "S", "I" };
        double[] mv = { an == null ? double.NaN : an.M, an == null ? double.NaN : an.S, an == null ? double.NaN : an.I };
        for (int i = 0; i < 3; i++)
        {
            Txt(g, ml[i], 16, Dim, At(1046, 342 + i * 20, 20, 18), StringAlignment.Center, false);
            BarMeter(g, 1068, 338 + i * 20, 232, 9, mv[i], -60, 0);
            Txt(g, Num(mv[i], "0.0"), 14, AccentHi, new RectangleF(1306, 334 + i * 20, 60, 18), StringAlignment.Near, false);
        }
        Txt(g, "-60", 15, Dim, At(1068, 410, 40, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1300, 410, 24, 20), StringAlignment.Far, false);

        Txt(g, "LU RANGE", 17, Dim, At(1180, 460, 110, 24), StringAlignment.Center, false);
        Txt(g, an == null ? "-" : Num(an.LRA, "0.0"), 18, Accent, At(1180, 484, 110, 24), StringAlignment.Center, false);
        Btn(g, StartR, "Start", false, Violet, Accent); Btn(g, ResetR, "Reset", false, Violet, Accent);
    }

    void PaintSpread(Graphics g)
    {
        Panel(g, SpreadR);
        Txt(g, "SPREAD", 24, White, new RectangleF(853, 550, 200, 32), StringAlignment.Near, true);
        Txt(g, "WIDTH", 17, Dim, At(920, 606, 120, 24), StringAlignment.Center, false);
        Knob(g, KnobW, ms.Width / 200f, true);
        Txt(g, ((int)ms.Width) + " %", 16, Accent, At(920, 742, 100, 22), StringAlignment.Center, false);

        Txt(g, "CORRELATION", 17, Dim, new RectangleF(1068, 594, 150, 24), StringAlignment.Near, false);
        var bar = new RectangleF(1068, 639, 232, 8);
        using (var br = new LinearGradientBrush(bar, TM.C(255, 235, 70, 130), Color.FromArgb(120, 230, 150), 0f))
        {
            var cb = new ColorBlend(3);
            cb.Colors = new[] { TM.C(255, 235, 70, 130), Color.FromArgb(240, 210, 90), Color.FromArgb(120, 230, 150) };
            cb.Positions = new[] { 0f, 0.5f, 1f };
            br.InterpolationColors = cb;
            using (var p = RR(bar, 4)) g.FillPath(br, p);
        }
        double cv = app.LastAn == null ? double.NaN : app.LastAn.Corr;
        float mx = 1068 + 232 * (float)((double.IsNaN(cv) ? 0 : cv) + 1) / 2f;
        using (var pen = new Pen(White, 2.5f)) g.DrawLine(pen, mx, 626, mx, 654);
        Txt(g, "-1", 15, Dim, At(1068, 673, 30, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1184, 673, 24, 20), StringAlignment.Center, false);
        Txt(g, "+1", 15, Dim, At(1300, 673, 30, 20), StringAlignment.Far, false);
        Txt(g, Num(cv, "0.0"), 18, White, new RectangleF(1312, 628, 60, 28), StringAlignment.Near, false);
    }

    void PaintBottom(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(215, Chrome))) g.FillRectangle(br, 0, 832, 1400, 48);
        using (var pen = new Pen(TM.C(140, 150, 80, 220))) g.DrawLine(pen, 0, 832, 1400, 832);
        string cap = analyzing ? analyzeText : "Purphex Mastering";
        Txt(g, cap, 24, analyzing ? Accent : White, new RectangleF(300, 836, 800, 40), StringAlignment.Center, false);

        FillRR(g, TM.C(150, 52, 28, 92), ReanR, 6);
        // sparkle icon
        using (var pen = new Pen(Accent2, 2.2f))
        {
            g.DrawLine(pen, ReanR.X + 20, ReanR.Y + 12, ReanR.X + 20, ReanR.Y + 30); g.DrawLine(pen, ReanR.X + 11, ReanR.Y + 21, ReanR.X + 29, ReanR.Y + 21);
            g.DrawLine(pen, ReanR.X + 28, ReanR.Y + 9, ReanR.X + 28, ReanR.Y + 15); g.DrawLine(pen, ReanR.X + 25, ReanR.Y + 12, ReanR.X + 31, ReanR.Y + 12);
        }
        Txt(g, "REANALYZE", 18, Accent, new RectangleF(ReanR.X + 38, ReanR.Y, 90, ReanR.Height), StringAlignment.Near, false);
        Btn(g, BypassR, ms.On ? "ON" : "OFF", ms.On, Violet, ms.On ? White : Accent);
        FillRR(g, ms.Comp ? TM.C(200, 200, 70, 150) : TM.C(150, 90, 30, 90), LoudR, 6);
        Txt(g, "Loudness\nCompensation", 15, ms.Comp ? White : Pink, LoudR, StringAlignment.Center, false);
    }

    // ---------- state changes ----------
    void Commit(Action change)
    {
        string before = ms.Serialize();
        change();
        if (ms.Serialize() != before)
        {
            undo.Add(before); redo.Clear(); presetName = "Manual"; app.SaveSettings();
        }
        Invalidate();
    }

    void ApplyPreset(int i)
    {
        Commit(() => { ms.Parse(PresetData[i]); });
        presetName = PresetNames[i];
    }

    int PresetIndex() { for (int i = 0; i < PresetNames.Length; i++) if (PresetNames[i] == presetName) return i; return -1; }

    static bool In(RectangleF r, PointF p) { return r.Contains(p); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var p = new PointF(e.X / z, e.Y / z);

        for (int i = 0; i < 5; i++)
        {
            float dx = FX(ms.NF[i]) - p.X, dy = GY(ms.NG[i]) - p.Y;
            if (dx * dx + dy * dy < 22 * 22)
            {
                selNode = i; drag = 1; dragBefore = ms.Serialize(); ms.Custom = true; Invalidate(); return;
            }
        }
        if (p.X > SlX - 28 && p.X < SlX + 28 && p.Y > SlY1 - 16 && p.Y < SlY2 + 16)
        { drag = 2; dragBefore = ms.Serialize(); MoveDrag(p); return; }
        if (Dist(p, KnobL) < KnobR + 14)
        {
            if (e.Clicks == 2) { Commit(() => ms.Loud = -14); return; }
            drag = 3; dragY = p.Y; dragVal = ms.Loud; dragBefore = ms.Serialize(); return;
        }
        if (Dist(p, KnobW) < KnobR + 14)
        {
            if (e.Clicks == 2) { Commit(() => ms.Width = 100); return; }
            drag = 4; dragY = p.Y; dragVal = ms.Width; dragBefore = ms.Serialize(); return;
        }

        if (Dist(p, new PointF(49, 78)) < 28 || In(BypassR, p)) { Commit(() => ms.On = !ms.On); return; }
        if (In(CharR, p)) { CharMenu(e.Location); return; }
        if (In(CustomR, p)) { Commit(() => ms.Custom = !ms.Custom); return; }
        if (In(ExciteR, p)) { Commit(() => ms.Excite = !ms.Excite); return; }
        if (In(LoudR, p)) { Commit(() => ms.Comp = !ms.Comp); return; }
        if (In(PresetR, p)) { PresetMenu(e.Location); return; }
        if (In(NavL, p)) { int i = PresetIndex(); ApplyPreset(i <= 0 ? PresetNames.Length - 1 : i - 1); return; }
        if (In(NavR, p)) { int i = PresetIndex(); ApplyPreset(i < 0 || i >= PresetNames.Length - 1 ? 0 : i + 1); return; }
        if (In(UndoR, p) && undo.Count > 0) { redo.Add(ms.Serialize()); ms.Parse(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1); presetName = "Manual"; app.SaveSettings(); Invalidate(); return; }
        if (In(RedoR, p) && redo.Count > 0) { undo.Add(ms.Serialize()); ms.Parse(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1); presetName = "Manual"; app.SaveSettings(); Invalidate(); return; }
        if (In(CompareR, p)) { ToggleCompare(); return; }
        if (In(CopyR, p)) { try { Clipboard.SetText(ms.Serialize()); } catch { } return; }
        if (In(PasteR, p))
        {
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText().StartsWith("on="))
                { string txt = Clipboard.GetText(); Commit(() => ms.Parse(txt)); }
            }
            catch { }
            return;
        }
        if (In(ViewR, p)) { ViewMenu(e.Location); return; }
        if (In(ReanR, p)) { Analyze(true, (ModifierKeys & Keys.Control) != 0); return; }
        if (In(StartR, p)) { Analyze(false, (ModifierKeys & Keys.Control) != 0); return; }
        if (In(ResetR, p)) { app.LastAn = null; Invalidate(); return; }
    }

    static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }

    void MoveDrag(PointF p)
    {
        if (drag == 1)
        {
            float[] lo = { 20, 60, 200, 800, 2000 }, hi = { 500, 2000, 8000, 16000, 20000 };
            double f = Math.Max(lo[selNode], Math.Min(hi[selNode], FromX(p.X)));
            double gdb = Math.Max(-12, Math.Min(12, FromY(p.Y)));
            ms.NF[selNode] = (float)Math.Round(f, 1);
            ms.NG[selNode] = (float)Math.Round(gdb * 10) / 10f;
        }
        else if (drag == 2) ms.AutoEq = (float)Math.Round(Math.Max(0, Math.Min(100, (SlY2 - p.Y) / (SlY2 - SlY1) * 100)));
        else if (drag == 3) ms.Loud = (float)Math.Round(Math.Max(-20, Math.Min(-6, dragVal + (dragY - p.Y) * 0.08)) * 10) / 10f;
        else if (drag == 4) ms.Width = (float)Math.Round(Math.Max(0, Math.Min(200, dragVal + (dragY - p.Y))));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (drag != 0) MoveDrag(new PointF(e.X / z, e.Y / z));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag != 0)
        {
            drag = 0;
            if (ms.Serialize() != dragBefore) { undo.Add(dragBefore); redo.Clear(); presetName = "Manual"; app.SaveSettings(); }
            Invalidate();
        }
    }

    void ToggleCompare()
    {
        if (!comparing)
        {
            if (undo.Count == 0) return;
            cmpHold = ms.Serialize(); ms.Parse(undo[undo.Count - 1]); comparing = true;
        }
        else { ms.Parse(cmpHold); comparing = false; }
        Invalidate();
    }

    void CharMenu(Point at)
    {
        var m = Win.NewMenu();
        for (int i = 0; i < 4; i++)
        {
            int k = i; var it = new ToolStripMenuItem(MasterSettings.CharNames[i]) { Checked = ms.Character == i };
            it.Click += (s, e) => Commit(() => ms.Character = k);
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    void PresetMenu(Point at)
    {
        var m = Win.NewMenu();
        for (int i = 0; i < PresetNames.Length; i++)
        {
            int k = i; var it = new ToolStripMenuItem(PresetNames[i]) { Checked = presetName == PresetNames[i] };
            it.Click += (s, e) => ApplyPreset(k);
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    void ViewMenu(Point at)
    {
        var m = Win.NewMenu();
        foreach (int pct in new[] { 50, 60, 70, 75, 85, 100 })
        {
            int k = pct; var it = new ToolStripMenuItem(pct + "%") { Checked = app.MZoomPct == pct };
            it.Click += (s, e) => { app.MZoomPct = k; z = k / 100f; ApplyZoom(); app.SaveSettings(); Invalidate(); };
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    // Analyze the last converted file (or pick one) in the background.
    void Analyze(bool full, bool pick)
    {
        if (analyzing) return;
        string file = app.LastFile;
        if (pick || file == null || !File.Exists(file))
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Choose an audio file to analyze";
                d.Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                file = d.FileName; app.LastFile = file;
            }
        }
        if (StudioEnv.Ffmpeg() == null) { MessageBox.Show(this, "Convert something once first. That installs ffmpeg, which the analysis needs."); return; }
        analyzing = true; analyzeText = "Analyzing " + Path.GetFileName(file) + "...";
        string f = file;
        new Thread(() =>
        {
            MasterAnalysis an = null;
            try { an = MasterDsp.Analyze(f, full, true, null); } catch { }
            BeginInvoke(new Action(() =>
            {
                analyzing = false;
                if (an != null && an.Ok)
                {
                    if (!full && app.LastAn != null) { an.Auto = app.LastAn.Auto; an.HasBands = app.LastAn.HasBands; }
                    app.LastAn = an;
                }
                else analyzeText = "";
                Invalidate();
            }));
        }) { IsBackground = true }.Start();
    }
}
