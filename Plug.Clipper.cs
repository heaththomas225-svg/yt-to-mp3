using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX CLIPPER: a multi-mode clipper. THRESHOLD is where it starts to clip, SOFTNESS goes from a hard edge to a smooth curve,
//  PRE GAIN pushes into it and POST GAIN sets the level after (LINK keeps the loudness about the same when you push harder).
//  MODE: soft, hard, tape, tube or fold. The curve shows what it does, the waveform shows the peaks it cuts (red), and the meters show
//  peak and RMS in and out.
// ======================================================================
class ClipperFx : Fx
{
    public static readonly string[] Modes = { "Soft", "Hard", "Tape", "Tube", "Fold" };
    public readonly float[] HistIn = new float[300], HistOut = new float[300]; public volatile int HistW; int hc; float hi, ho;
    public volatile float InPk, OutPk, InRms, OutRms; double rin, rout;
    public ClipperFx()
    {
        Type = "pxclip"; Title = "Purphex Clipper"; Category = "Purphex VST"; Blurb = "Multi-mode clipper: threshold, softness, pre and post gain";
        AddC("mode", "Mode", Modes, 0); AddP("thr", "Threshold", -24, 0, -6, "dB"); AddP("soft", "Softness", 0, 100, 100, "%"); AddP("pre", "Pre gain", -12, 24, 0, "dB"); AddP("post", "Post gain", -24, 12, 0, "dB"); AddC("link", "Link", new[] { "Off", "On" }, 0);
    }
    // one sample through the clip curve (t = threshold as a level, s = softness 0..1)
    public static double Curve(int mode, double x, double t, double s)
    {
        double a = Math.Abs(x), sg = Math.Sign(x);
        switch (mode)
        {
            case 1: return Math.Max(-t, Math.Min(t, x));
            case 2: return t * Math.Tanh(x / t * (1 + 0.3 * s)) / Math.Tanh(1 + 0.3 * s) * (a > t * 3 ? 1 : 1);
            case 3: { double y = x >= 0 ? Math.Tanh(x / t) : Math.Tanh(x / t * 0.8) / 0.8; return y * t; }
            case 4: { double y = x / t; while (Math.Abs(y) > 1) y = Math.Sign(y) * 2 - y; return y * t; }
            default:
                {   // soft: straight up to the knee, then a smooth curve into the threshold
                    double knee = t * (1 - Math.Max(0.02, s) * 0.9); if (a <= knee) return x; double over = (a - knee) / (t - knee); return sg * (knee + (t - knee) * Math.Tanh(over));
                }
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double t = Math.Pow(10, Get("thr") / 20), s = Get("soft") / 100.0, pre = Math.Pow(10, Get("pre") / 20), post = Math.Pow(10, (Get("post") - (Get("link") >= 0.5f ? Get("pre") : 0)) / 20);
        float ip = 0, op = 0; double ra = 1 - Math.Exp(-1.0 / (0.3 * SR));
        for (int i = 0; i < n; i++)
        {
            double xl = l[i] * pre, xr = r[i] * pre; double yl = Curve(mode, xl, t, s) * post, yr = Curve(mode, xr, t, s) * post;
            l[i] = (float)yl; r[i] = (float)yr; double a = Math.Max(Math.Abs(xl), Math.Abs(xr)), b = Math.Max(Math.Abs(yl), Math.Abs(yr)); ip = (float)Math.Max(ip, a); op = (float)Math.Max(op, b);
            rin += (a * a - rin) * ra; rout += (b * b - rout) * ra; hi = (float)Math.Max(hi, a); ho = (float)Math.Max(ho, b);
            if (++hc >= SR / 60) { hc = 0; int w = HistW; HistIn[w] = hi; HistOut[w] = ho; HistW = (w + 1) % HistIn.Length; hi = ho = 0; }
        }
        InPk = Math.Max(InPk * 0.9f, ip); OutPk = Math.Max(OutPk * 0.9f, op); InRms = (float)Math.Sqrt(rin); OutRms = (float)Math.Sqrt(rout);
    }
}

#if !PURPHEX_CORE
class ClipperWindow : PxWindow
{
    readonly ClipperFx c;
    public ClipperWindow(ClipperFx f) : base(f, "Clipper", new Size(920, 600)) { c = f; Accent = Cyan; }
    protected override string SubTitle { get { return "multi-mode clipper"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // waveform: what comes in, with the part the clipper cuts in red
        var wv = new RectangleF(a.X, a.Y, a.Width, 170); Fill(g, Color.FromArgb(18, 22, 34), wv, 8); float mid = wv.Bottom - 6, t = (float)Math.Pow(10, fx.Get("thr") / 20);
        int n = c.HistIn.Length, w = c.HistW;
        for (int i = 0; i < n; i++) { float x = wv.X + i * wv.Width / n, a1 = Math.Min(1, c.HistIn[(w + i) % n]), a2 = Math.Min(1, c.HistOut[(w + i) % n]); using (var b = new SolidBrush(Color.FromArgb(200, 50, 110, 200))) g.FillRectangle(b, x, mid - a2 * (wv.Height - 12), wv.Width / n + 1, a2 * (wv.Height - 12)); if (a1 > a2 + 0.005f) using (var b = new SolidBrush(Color.FromArgb(220, 240, 90, 70))) g.FillRectangle(b, x, mid - a1 * (wv.Height - 12), wv.Width / n + 1, (a1 - a2) * (wv.Height - 12)); }
        // the curve
        var cv = new RectangleF(a.X, wv.Bottom + 14, 330, a.Bottom - wv.Bottom - 14); Panel(g, cv, "", Cyan); var gr = new RectangleF(cv.X + 16, cv.Y + 16, cv.Width - 32, cv.Height - 32); Fill(g, Color.FromArgb(14, 14, 24), gr, 4);
        using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) for (int k = 1; k < 6; k++) { g.DrawLine(pen, gr.X + gr.Width * k / 6, gr.Y, gr.X + gr.Width * k / 6, gr.Bottom); g.DrawLine(pen, gr.X, gr.Y + gr.Height * k / 6, gr.Right, gr.Y + gr.Height * k / 6); }
        int mode = (int)fx.Get("mode"); double s = fx.Get("soft") / 100.0, pre = Math.Pow(10, fx.Get("pre") / 20); var pts = new PointF[80]; var fillP = new PointF[82];
        for (int i = 0; i < 80; i++) { double db = -30 + 36.0 * i / 79, x = Math.Pow(10, db / 20) * pre, y = Math.Abs(ClipperFx.Curve(mode, x, t, s)); double ydb = 20 * Math.Log10(y + 1e-9); pts[i] = new PointF(gr.X + (float)(i / 79.0 * gr.Width), gr.Bottom - (float)Math.Max(0, Math.Min(1, (ydb + 30) / 36)) * gr.Height); fillP[i + 1] = pts[i]; }
        fillP[0] = new PointF(gr.X, gr.Bottom); fillP[81] = new PointF(gr.Right, gr.Bottom); using (var b = new SolidBrush(Color.FromArgb(70, 160, 170, 190))) g.FillPolygon(b, fillP); using (var pen = new Pen(Color.FromArgb(70, 140, 220), 2.4f)) g.DrawLines(pen, pts);
        // controls
        var ct = new RectangleF(cv.Right + 10, cv.Y, a.Right - cv.Right - 220, cv.Height); Panel(g, ct, "", Cyan);
        Segments(g, new RectangleF(ct.X + 16, ct.Y + 14, ct.Width - 32, 26), "mode", Cyan, 10);
        Knob(g, ct.X + ct.Width * 0.27f, ct.Y + 96, 30, "thr", "THRESHOLD", Cyan); Knob(g, ct.X + ct.Width * 0.73f, ct.Y + 96, 30, "soft", "SOFTNESS", Cyan);
        Knob(g, ct.X + ct.Width * 0.27f, ct.Y + 220, 30, "pre", "PRE GAIN", Cyan); Knob(g, ct.X + ct.Width * 0.73f, ct.Y + 220, 30, "post", "POST GAIN", Cyan);
        Toggle(g, new RectangleF(ct.X + ct.Width / 2 - 30, ct.Y + 160, 60, 22), "link", Gold, "LINK", "LINK");
        var mt = new RectangleF(ct.Right + 10, cv.Y, a.Right - ct.Right - 10, cv.Height); Panel(g, mt, "OUTPUT", Violet);
        Txt(g, "Peak " + Db(c.InPk) + " / " + Db(c.OutPk), 10, Ink, new RectangleF(mt.X + 10, mt.Y + 28, mt.Width - 20, 14), StringAlignment.Center, false); Txt(g, "RMS " + Db(c.InRms) + " / " + Db(c.OutRms), 10, Dim, new RectangleF(mt.X + 10, mt.Y + 44, mt.Width - 20, 14), StringAlignment.Center, false);
        Meter(g, new RectangleF(mt.X + 30, mt.Y + 66, 14, mt.Height - 96), c.InPk, "IN"); Meter(g, new RectangleF(mt.X + 54, mt.Y + 66, 14, mt.Height - 96), c.InRms, ""); Meter(g, new RectangleF(mt.Right - 68, mt.Y + 66, 14, mt.Height - 96), c.OutPk, "OUT"); Meter(g, new RectangleF(mt.Right - 44, mt.Y + 66, 14, mt.Height - 96), c.OutRms, "");
    }
    static string Db(float v) { double d = 20 * Math.Log10(Math.Max(1e-5, v)); return d < -90 ? "-inf" : d.ToString("0.0"); }
}
#endif

// ======================================================================
//  PURPHEX SMACK: a transient shaper. ATTACK lifts or softens the start of each hit, SUSTAIN lifts or shortens the tail, each with its own
//  SENSITIVITY, a SHAPE (how the change is drawn: short, medium, long) and DURATION. MIX, OUTPUT and a GUARD (off, clip or limit) that keeps
//  the peaks safe. The display shows the sound with the attack (cyan) and sustain (gold) changes drawn over it.
// ======================================================================
class SmackFx : Fx
{
    double fA, sA, fS, sS, lim = 1; public readonly float[] HistA = new float[300], HistS = new float[300], HistL = new float[300]; public volatile int HistW; int hc; float ha, hs, hl; public volatile float Gr;
    public SmackFx()
    {
        Type = "pxsmack"; Title = "Purphex Smack"; Category = "Purphex VST"; Blurb = "Transient shaper: attack and sustain, sensitivity, shape, duration, guard";
        AddP("att", "Attack", -100, 100, 26, ""); AddP("asens", "Attack sensitivity", 0, 100, 50, "%"); AddC("ashape", "Attack shape", new[] { "Short", "Medium", "Long" }, 1); AddP("adur", "Attack duration", 1, 100, 30, "ms");
        AddP("sus", "Sustain", -100, 100, 16, ""); AddP("ssens", "Sustain sensitivity", 0, 100, 50, "%"); AddC("sshape", "Sustain shape", new[] { "Short", "Medium", "Long" }, 1); AddP("sdur", "Sustain duration", 20, 2000, 250, "ms");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 24, 0, "dB"); AddC("guard", "Guard", new[] { "Off", "Clip", "Limit" }, 1);
    }
    public override void Reset() { base.Reset(); fA = sA = fS = sS = 0; lim = 1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double att = Get("att") / 100.0, sus = Get("sus") / 100.0, asn = Get("asens") / 100.0, ssn = Get("ssens") / 100.0; int ash = (int)Get("ashape"), ssh = (int)Get("sshape");
        double fa = 1 - Math.Exp(-1.0 / (0.0007 * SR)), sa = 1 - Math.Exp(-1.0 / (Get("adur") * (ash + 1) * 0.5 * 0.001 * SR)), fs = 1 - Math.Exp(-1.0 / (0.015 * SR)), ss = 1 - Math.Exp(-1.0 / (Get("sdur") * (ssh + 1) * 0.5 * 0.001 * SR));
        double mix = Get("mix") / 100.0, outG = Math.Pow(10, Get("out") / 20); int guard = (int)Get("guard"); double gr = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i], a = Math.Max(Math.Abs(xl), Math.Abs(xr));
            fA += (a - fA) * fa; sA += (a - sA) * sa; fS += (a - fS) * fs; sS += (a - sS) * ss;
            double tr = Math.Max(0, fA - sA) / (sA + 1e-4 + (1 - asn) * 0.05), tail = Math.Max(0, sS - fS) / (fS + 1e-4 + (1 - ssn) * 0.05);
            double gdb = att * 18 * Math.Min(1, tr) + sus * 18 * Math.Min(1, tail * 2), g = Math.Pow(10, gdb / 20);
            double yl = xl + (xl * g - xl) * mix, yr = xr + (xr * g - xr) * mix; yl *= outG; yr *= outG;
            if (guard == 1) { yl = Math.Max(-1, Math.Min(1, yl)); yr = Math.Max(-1, Math.Min(1, yr)); }
            else if (guard == 2) { double p = Math.Max(Math.Abs(yl), Math.Abs(yr)) * lim; if (p > 0.98) lim = 0.98 / Math.Max(Math.Abs(yl), Math.Abs(yr)); else lim = Math.Min(1, lim + (1 - lim) * 0.0005); yl *= lim; yr *= lim; gr = Math.Max(gr, -20 * Math.Log10(lim)); }
            l[i] = (float)yl; r[i] = (float)yr;
            ha = (float)Math.Max(ha, att * Math.Min(1, tr)); hs = (float)Math.Max(hs, sus * Math.Min(1, tail * 2)); hl = (float)Math.Max(hl, a);
            if (++hc >= SR / 60) { hc = 0; int w = HistW; HistA[w] = ha; HistS[w] = hs; HistL[w] = hl; HistW = (w + 1) % HistA.Length; ha = hs = hl = 0; }
        }
        Gr = (float)gr;
    }
}

#if !PURPHEX_CORE
class SmackWindow : PxWindow
{
    readonly SmackFx s;
    public SmackWindow(SmackFx f) : base(f, "Smack", new Size(780, 700)) { s = f; Accent = Cyan; }
    protected override string SubTitle { get { return "transient shaper"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var wv = new RectangleF(a.X, a.Y, a.Width, 170); Fill(g, Color.FromArgb(10, 14, 22), wv, 8); float mid = wv.Y + wv.Height / 2; int n = s.HistL.Length, w = s.HistW;
        using (var b = new SolidBrush(Color.FromArgb(160, 90, 170, 200))) for (int i = 0; i < n; i++) { float x = wv.X + i * wv.Width / n, h = Math.Min(1, s.HistL[(w + i) % n]) * wv.Height * 0.45f; g.FillRectangle(b, x, mid - h, wv.Width / n + 1, h * 2); }
        var pa = new PointF[n]; var ps = new PointF[n]; for (int i = 0; i < n; i++) { float x = wv.X + i * wv.Width / n; pa[i] = new PointF(x, mid - s.HistA[(w + i) % n] * wv.Height * 0.45f); ps[i] = new PointF(x, mid - s.HistS[(w + i) % n] * wv.Height * 0.45f); }
        using (var pen = new Pen(Cyan, 2)) g.DrawLines(pen, pa); using (var pen = new Pen(Orange, 2)) g.DrawLines(pen, ps);
        float cy = wv.Bottom + 130;
        Knob(g, a.X + 170, cy, 70, "att", "ATTACK", Cyan, true); Knob(g, a.Right - 170, cy, 70, "sus", "SUSTAIN", Gold, true);
        Knob(g, a.X + a.Width / 2, cy - 60, 26, "asens", "ATTACK SENS", Cyan); Knob(g, a.X + a.Width / 2, cy + 50, 26, "ssens", "SUSTAIN SENS", Gold);
        Segments(g, new RectangleF(a.X + 70, cy + 110, 200, 24), "ashape", Cyan, 10); HSlider(g, new RectangleF(a.X + 70, cy + 160, 200, 8), "adur", "DURATION", Cyan);
        Segments(g, new RectangleF(a.Right - 270, cy + 110, 200, 24), "sshape", Gold, 10); HSlider(g, new RectangleF(a.Right - 270, cy + 160, 200, 8), "sdur", "DURATION", Gold);
        float by = a.Bottom - 60; Knob(g, a.X + 170, by, 24, "mix", "MIX", Violet); Knob(g, a.X + a.Width / 2, by, 30, "out", "OUTPUT", Violet);
        Txt(g, "GUARD", 10, Dim, new RectangleF(a.Right - 280, by - 40, 200, 14), StringAlignment.Near, true); Segments(g, new RectangleF(a.Right - 280, by - 22, 200, 26), "guard", Orange, 10);
        Txt(g, s.Gr > 0.05f ? "-" + s.Gr.ToString("0.0") + " dB" : "", 10, Orange, new RectangleF(a.Right - 280, by + 8, 200, 14), StringAlignment.Near, false);
    }
}
#endif
