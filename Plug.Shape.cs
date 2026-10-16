using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX SHAPE: a compressor and transient shaper in one. Ratio, knee, threshold, attack and release (fast modes for each), INTENSITY
//  (how much of the squeeze is used), AUTO GAIN (makes up the lost level), feed-forward or feedback detection, MIX and OUTPUT.
//  Four sections, each on or off: TRANSIENT (lift or soften the hits), SIDE-CHAIN (what the detector listens to: a peak EQ and a low cut,
//  with LISTEN), EQ (a tilt before or after the compressor) and CLIP (a final clipper from hard to soft).
//  The display shows the level and the gain reduction scrolling past, and meters for in, gain reduction and out.
// ======================================================================
class ShapeFx : Fx
{
    readonly Biquad scPeak = new Biquad(), scLc = new Biquad(), eqLo = new Biquad(), eqHi = new Biquad();
    double env, grDb, fbL, fbR, trFast, trSlow, mixS = 1, outG = 1, prevOut;
    public readonly float[] HistIn = new float[400], HistGr = new float[400]; public volatile int HistW; int histCnt; float hIn, hGr;
    public volatile float InPk, OutPk, GrNow;
    public ShapeFx()
    {
        Type = "pxshape"; Title = "Purphex Shape"; Category = "Purphex VST"; Blurb = "Compressor and transient shaper with side-chain EQ, EQ and clipper";
        AddC("mode", "Detection", new[] { "Feed-forward", "Feedback" }, 0);
        AddP("ratio", "Ratio", 1, 20, 3, ":1"); AddP("knee", "Knee", 0, 24, 6, "dB"); AddP("thr", "Threshold", -60, 0, -20, "dB"); AddP("int", "Intensity", 0, 100, 100, "%"); AddP("auto", "Auto gain", 0, 100, 50, "%");
        AddP("att", "Attack", 0.1f, 100, 5, "ms"); AddC("fatt", "Fast attack", new[] { "Off", "On" }, 0); AddP("rel", "Release", 5, 2000, 120, "ms"); AddC("frel", "Auto release", new[] { "Off", "On" }, 0);
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 24, 0, "dB");
        AddC("tr_on", "Transient", new[] { "Off", "On" }, 0); AddP("tr_gain", "Transient gain", -12, 12, 3, "dB"); AddP("tr_time", "Transient time", 5, 200, 50, "ms");
        AddC("sc_on", "Side-chain", new[] { "Off", "On" }, 0); AddP("sc_f", "Side-chain frequency", 20, 20000, 1000, "Hz"); AddP("sc_g", "Side-chain gain", -24, 24, 0, "dB"); AddP("sc_q", "Side-chain Q", 0.1f, 10, 1, "");
        AddP("sc_lc", "Side-chain low cut", 10, 500, 10, "Hz"); AddC("sc_listen", "Listen to the side-chain", new[] { "Off", "On" }, 0);
        AddC("eq_on", "EQ", new[] { "Off", "On" }, 0); AddC("eq_pos", "EQ position", new[] { "Pre", "Post" }, 1); AddP("eq_f", "EQ frequency", 100, 10000, 1000, "Hz"); AddP("eq_slope", "EQ tilt", -100, 100, 0, "%");
        AddC("cl_on", "Clip", new[] { "Off", "On" }, 0); AddP("cl_gain", "Clip drive", 0, 24, 0, "dB"); AddP("cl_soft", "Clip softness", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); env = 0; grDb = 0; fbL = fbR = 0; trFast = trSlow = 0; prevOut = 0; scPeak.Reset(); scLc.Reset(); eqLo.Reset(); eqHi.Reset(); }
    protected override void Update()
    {
        scPeak.Set(Biquad.Bell, SR, Math.Min(Get("sc_f"), SR * 0.45), Get("sc_q"), Get("sc_g")); scLc.Set(Biquad.HighPass, SR, Get("sc_lc"), 0.707, 0);
        double t = Get("eq_slope") / 100.0 * 9; eqLo.Set(Biquad.LowShelf, SR, Get("eq_f"), 0.6, -t); eqHi.Set(Biquad.HighShelf, SR, Math.Min(Get("eq_f"), SR * 0.45), 0.6, t);
    }
    // static gain reduction in dB for a level (soft knee)
    public static double Gr(double x, double thr, double ratio, double knee)
    {
        double over = x - thr, outDb;
        if (2 * over < -knee) outDb = x; else if (knee > 0 && 2 * Math.Abs(over) <= knee) outDb = x + (1 / ratio - 1) * (over + knee / 2) * (over + knee / 2) / (2 * knee); else outDb = thr + over / ratio;
        return Math.Max(0, x - outDb);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        bool fb = Get("mode") >= 0.5f, tr = Get("tr_on") >= 0.5f, sc = Get("sc_on") >= 0.5f, listen = sc && Get("sc_listen") >= 0.5f, eq = Get("eq_on") >= 0.5f, eqPost = Get("eq_pos") >= 0.5f, cl = Get("cl_on") >= 0.5f, autoRel = Get("frel") >= 0.5f;
        double ratio = Get("ratio"), knee = Get("knee"), thr = Get("thr"), inten = Get("int") / 100.0, autoAmt = Get("auto") / 100.0;
        double attMs = Get("fatt") >= 0.5f ? 0.05 : Get("att"), relMs = Get("rel"), aA = Math.Exp(-1.0 / (attMs * 0.001 * SR)), aR = Math.Exp(-1.0 / (relMs * 0.001 * SR)), aRf = Math.Exp(-1.0 / (Math.Max(5, relMs * 0.25) * 0.001 * SR));
        double makeup = autoAmt * Gr(0, thr, ratio, knee) * 0.5 * inten;   // half of the reduction a full-scale sound gets
        double trGain = Get("tr_gain"), trT = Get("tr_time"), tfA = 1 - Math.Exp(-1.0 / (0.0015 * SR)), tsA = 1 - Math.Exp(-1.0 / (trT * 0.001 * SR));
        double clDrive = Math.Pow(10, Get("cl_gain") / 20), soft = Get("cl_soft") / 100.0;
        double mixT = Get("mix") / 100.0, outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        float inPk = 0, outPk = 0; double grMax = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i]; inPk = (float)Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            double yl = xl, yr = xr;
            if (eq && !eqPost) { yl = eqHi.TickL(eqLo.TickL((float)yl)); yr = eqHi.TickR(eqLo.TickR((float)yr)); }
            // detector
            double dl = fb ? fbL : yl, dr = fb ? fbR : yr;
            if (sc) { dl = scPeak.TickL(scLc.TickL((float)dl)); dr = scPeak.TickR(scLc.TickR((float)dr)); }
            double lev = Math.Max(Math.Abs(dl), Math.Abs(dr)), levDb = 20 * Math.Log10(lev + 1e-9), target = Gr(levDb, thr, ratio, knee) * inten;
            if (target > grDb) grDb = aA * grDb + (1 - aA) * target; else { double rc = autoRel && grDb - target > 6 ? aRf : aR; grDb = rc * grDb + (1 - rc) * target; }
            double g = Math.Pow(10, (makeup - grDb) / 20); yl *= g; yr *= g; grMax = Math.Max(grMax, grDb);
            // transient: the gap between a fast and a slow follower is the attack of each hit
            if (tr)
            {
                double a = Math.Max(Math.Abs(xl), Math.Abs(xr)); trFast += (a - trFast) * tfA; trSlow += (a - trSlow) * tsA;
                double t = Math.Max(0, trFast - trSlow) / (trSlow + 1e-4); double tg = Math.Pow(10, trGain * Math.Min(1, t) / 20); yl *= tg; yr *= tg;
            }
            if (eq && eqPost) { yl = eqHi.TickL(eqLo.TickL((float)yl)); yr = eqHi.TickR(eqLo.TickR((float)yr)); }
            if (cl)
            {
                double cxl = yl * clDrive, cxr = yr * clDrive;
                double hl = Math.Max(-1, Math.Min(1, cxl)), hr = Math.Max(-1, Math.Min(1, cxr)), sl = Math.Tanh(cxl), srr = Math.Tanh(cxr);
                yl = hl + (sl - hl) * soft; yr = hr + (srr - hr) * soft;
            }
            fbL = yl; fbR = yr;
            if (listen) { yl = dl; yr = dr; }
            mixS += (mixT - mixS) * gs; outG += (outT - outG) * gs;
            double ol = (xl + (yl - xl) * (listen ? 1 : mixS)) * outG, or = (xr + (yr - xr) * (listen ? 1 : mixS)) * outG;
            if (double.IsNaN(ol) || double.IsInfinity(ol) || double.IsNaN(or) || double.IsInfinity(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or; outPk = (float)Math.Max(outPk, Math.Max(Math.Abs(ol), Math.Abs(or)));
            hIn = Math.Max(hIn, (float)Math.Max(Math.Abs(xl), Math.Abs(xr))); hGr = Math.Max(hGr, (float)grDb);
            if (++histCnt >= SR / 80) { histCnt = 0; int w = HistW; HistIn[w] = hIn; HistGr[w] = hGr; HistW = (w + 1) % HistIn.Length; hIn = 0; hGr = 0; }
        }
        InPk = Math.Max(InPk * 0.85f, inPk); OutPk = Math.Max(OutPk * 0.85f, outPk); GrNow = (float)grMax;
    }
}

class ShapeWindow : PxWindow
{
    readonly ShapeFx s;
    public ShapeWindow(ShapeFx f) : base(f, "Shape", new Size(980, 790)) { s = f; Accent = Cyan; }
    protected override string SubTitle { get { return "compressor and transient shaper"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // display: the level (blue) and the gain reduction (orange) scrolling past, and the meters
        var disp = new RectangleF(a.X, a.Y, a.Width, 190); Panel(g, disp, "", Cyan); var wv = new RectangleF(disp.X + 12, disp.Y + 12, disp.Width - 150, disp.Height - 24); Fill(g, Color.FromArgb(14, 8, 28), wv, 8);
        int n = s.HistIn.Length, w = s.HistW; float mid = wv.Y + wv.Height * 0.62f;
        using (var pen = new Pen(Color.FromArgb(200, 70, 190, 240), 1.4f))
            for (int i = 0; i < n; i++) { float v = s.HistIn[(w + i) % n]; float h = (float)Math.Min(1, v) * wv.Height * 0.36f; float x = wv.X + i * wv.Width / n; g.DrawLine(pen, x, mid - h, x, mid + h); }
        var pts = new PointF[n]; for (int i = 0; i < n; i++) { float gr = s.HistGr[(w + i) % n]; pts[i] = new PointF(wv.X + i * wv.Width / n, wv.Y + 8 + Math.Min(1, gr / 24) * wv.Height * 0.4f); }
        using (var pen = new Pen(Orange, 2)) g.DrawLines(pen, pts);
        Txt(g, "level", 10, Cyan, new RectangleF(wv.X + 8, wv.Bottom - 18, 60, 14), StringAlignment.Near, true); Txt(g, "gain reduction", 10, Orange, new RectangleF(wv.X + 8, wv.Y + 4, 120, 14), StringAlignment.Near, true);
        float mx = disp.Right - 128; Meter(g, new RectangleF(mx, disp.Y + 16, 14, disp.Height - 44), s.InPk, "IN"); Meter(g, new RectangleF(mx + 40, disp.Y + 16, 14, disp.Height - 44), s.GrNow, "GR", true); Meter(g, new RectangleF(mx + 80, disp.Y + 16, 14, disp.Height - 44), s.OutPk, "OUT");
        Txt(g, "-" + s.GrNow.ToString("0.0") + " dB", 11, Orange, new RectangleF(mx + 20, disp.Y + 2, 90, 14), StringAlignment.Center, true);
        // the compressor
        var comp = new RectangleF(a.X, disp.Bottom + 10, a.Width, 236); Panel(g, comp, "COMPRESSOR", Cyan);
        float y1 = comp.Y + 60, y2 = comp.Y + 150;
        Knob(g, comp.X + 90, y1, 24, "ratio", "RATIO", Cyan); Knob(g, comp.X + 200, y1, 24, "knee", "KNEE", Cyan);
        Knob(g, comp.X + 90, y2, 24, "att", "ATTACK", Cyan); Toggle(g, new RectangleF(comp.X + 60, y2 + 54, 60, 20), "fatt", Gold, "FAST", "fast");
        Knob(g, comp.X + 200, y2, 24, "rel", "RELEASE", Cyan); Toggle(g, new RectangleF(comp.X + 170, y2 + 54, 60, 20), "frel", Gold, "AUTO", "auto");
        Knob(g, comp.X + comp.Width / 2, comp.Y + 100, 52, "thr", "THRESHOLD", Cyan, true); Segments(g, new RectangleF(comp.X + comp.Width / 2 - 110, comp.Y + 22, 220, 24), "mode", Cyan, 10);
        Knob(g, comp.Right - 200, y1, 24, "int", "INTENSITY", Cyan); Knob(g, comp.Right - 90, y1, 24, "auto", "AUTO GAIN", Cyan);
        Knob(g, comp.Right - 200, y2, 24, "mix", "MIX", Violet); Knob(g, comp.Right - 90, y2, 24, "out", "OUTPUT", Violet);
        // the four sections
        var sec = new RectangleF(a.X, comp.Bottom + 10, a.Width, a.Bottom - comp.Bottom - 10); float sw = (sec.Width - 30) / 4;
        Section(g, new RectangleF(sec.X, sec.Y, sw, sec.Height), "TRANSIENT", "tr_on", Lime, new[] { "tr_gain", "tr_time" }, new[] { "GAIN", "TIME" }, null);
        Section(g, new RectangleF(sec.X + (sw + 10), sec.Y, sw, sec.Height), "SIDE-CHAIN", "sc_on", Gold, new[] { "sc_f", "sc_g", "sc_q", "sc_lc" }, new[] { "FREQ", "GAIN", "Q", "LOW CUT" }, "sc_listen");
        Section(g, new RectangleF(sec.X + (sw + 10) * 2, sec.Y, sw, sec.Height), "EQ", "eq_on", Pink, new[] { "eq_f", "eq_slope" }, new[] { "FREQ", "TILT" }, "eq_pos");
        Section(g, new RectangleF(sec.X + (sw + 10) * 3, sec.Y, sw, sec.Height), "CLIP", "cl_on", Orange, new[] { "cl_gain", "cl_soft" }, new[] { "DRIVE", "HARD - SOFT" }, null);
    }
    void Section(Graphics g, RectangleF r, string title, string on, Color col, string[] knobs, string[] labels, string extra)
    {
        bool o = fx.Get(on) >= 0.5f; Fill(g, o ? Color.FromArgb(40, 22, 74) : PanelC, r, 12); Stroke(g, Color.FromArgb(o ? 220 : 90, col), r, 12, o ? 1.6f : 1);
        Txt(g, title, 11, o ? Ink : Dim, new RectangleF(r.X + 12, r.Y + 6, r.Width - 80, 18), StringAlignment.Near, true); Toggle(g, new RectangleF(r.Right - 58, r.Y + 6, 50, 22), on, col);
        int cols = knobs.Length > 2 ? 2 : knobs.Length; float kw = r.Width / cols;
        for (int i = 0; i < knobs.Length; i++) { int row = i / 2, c = i % 2; Knob(g, r.X + kw * c + kw / 2, r.Y + 58 + row * 80, 18, knobs[i], labels[i], o ? col : Color.FromArgb(110, col)); }
        if (extra != null) { var p = fx.Find(extra); if (p.Choices.Length == 2 && p.Choices[0] == "Off") Toggle(g, new RectangleF(r.X + r.Width / 2 - 40, r.Bottom - 30, 80, 22), extra, col, "LISTENING", "LISTEN"); else Segments(g, new RectangleF(r.X + 14, r.Bottom - 30, r.Width - 28, 22), extra, col, 10); }
    }
}
