using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX TRI-DRIVE: three-band distortion. The sound is split into LOW, MID and HIGH (drag the two split lines), and each band has its
//  own ON, TYPE (tube, tape, fuzz, rectify, crush), DRIVE, LEVEL and MIX. After the bands: a FILTER (low, band or high-pass with
//  resonance), a COMPRESSOR (one knob), FEEDBACK (a short delay fed back into the input, for ringing and metal), MIX and OUTPUT.
// ======================================================================
class TriDriveFx : Fx
{
    public static readonly string[] Types = { "Tube", "Tape", "Fuzz", "Rectify", "Crush" }, Bands = { "lo", "mid", "hi" };
    static readonly string[] OnId = { "lo_on", "mid_on", "hi_on" }, TyId = { "lo_type", "mid_type", "hi_type" }, DrId = { "lo_drive", "mid_drive", "hi_drive" }, LvId = { "lo_lvl", "mid_lvl", "hi_lvl" }, MxId = { "lo_mix", "mid_mix", "hi_mix" };
    readonly bool[] on = new bool[3]; readonly int[] ty = new int[3]; readonly double[] dr = new double[3], lv = new double[3], mx = new double[3];
    double gLo, gHi, kSplit = 1.41421356, filtG, filtK, compThr, compRatio, compMk, fbAmt, mixW, outG; int filt, fbLen;
    readonly double[] s1 = new double[8], s2 = new double[8];   // [4 + ch*2]: the output filter
    readonly double[] x1 = new double[24], x2 = new double[24];   // the crossover filters: 12 per side
    readonly double[] dcX = new double[6], dcY = new double[6]; readonly double[] held = new double[6]; readonly int[] holdN = new int[6];
    readonly float[] dlL = new float[96000], dlR = new float[96000]; int dw; double env; public volatile float GRdb, InPk, OutPk;
    public readonly PxScope Scope = new PxScope(4096);
    public TriDriveFx()
    {
        Type = "pxtri"; Title = "Purphex Tri-Drive"; Category = "Purphex VST"; Blurb = "Three-band distortion with a filter, a compressor and feedback";
        AddP("xlo", "Low / mid split", 40, 1000, 200, "Hz"); AddP("xhi", "Mid / high split", 1000, 12000, 3000, "Hz");
        int[] defTy = { 0, 1, 2 }; float[] defDr = { 30, 30, 25 };
        for (int b = 0; b < 3; b++)
        {
            string n = b == 0 ? "Low" : b == 1 ? "Mid" : "High";
            AddC(OnId[b], n + " on", new[] { "Off", "On" }, 1); AddC(TyId[b], n + " type", Types, defTy[b]); AddP(DrId[b], n + " drive", 0, 100, defDr[b], "%"); AddP(LvId[b], n + " level", -24, 12, 0, "dB"); AddP(MxId[b], n + " mix", 0, 100, 100, "%");
        }
        AddC("filt", "Filter", new[] { "Off", "Low-pass", "Band-pass", "High-pass" }, 0); AddP("cut", "Filter cutoff", 40, 20000, 2000, "Hz"); AddP("res", "Filter resonance", 0, 100, 20, "%");
        AddP("comp", "Compressor", 0, 100, 0, "%"); AddP("fb", "Feedback", 0, 90, 0, "%"); AddP("fbt", "Feedback time", 1, 500, 30, "ms");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); Array.Clear(s1, 0, 8); Array.Clear(s2, 0, 8); Array.Clear(x1, 0, 24); Array.Clear(x2, 0, 24); Array.Clear(dcX, 0, 6); Array.Clear(dcY, 0, 6); Array.Clear(held, 0, 6); Array.Clear(holdN, 0, 6); Array.Clear(dlL, 0, dlL.Length); Array.Clear(dlR, 0, dlR.Length); dw = 0; env = 0; }
    protected override void Update()
    {
        for (int b = 0; b < 3; b++) { on[b] = Get(OnId[b]) >= 0.5f; ty[b] = (int)Get(TyId[b]); dr[b] = Get(DrId[b]) / 100.0; lv[b] = Math.Pow(10, Get(LvId[b]) / 20); mx[b] = Get(MxId[b]) / 100.0; }
        gLo = Math.Tan(Math.PI * Math.Min(SR * 0.45, Get("xlo")) / SR); gHi = Math.Tan(Math.PI * Math.Min(SR * 0.45, Math.Max(Get("xlo") * 1.2, Get("xhi"))) / SR);
        filt = (int)Get("filt"); filtG = Math.Tan(Math.PI * Math.Min(SR * 0.45, Get("cut")) / SR); filtK = 2 - 1.9 * Get("res") / 100.0;
        double c = Get("comp") / 100.0; compThr = Math.Pow(10, (-6 - 30 * c) / 20); compRatio = 1 + 4 * c; compMk = Math.Min(4, Math.Pow(1 / compThr, (1 - 1 / compRatio) * 0.5));
        fbAmt = Get("fb") / 100.0; fbLen = (int)Math.Max(1, Math.Min(dlL.Length - 2, Get("fbt") * 0.001 * SR)); mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    static double Svf(double x, double g, double k, ref double a, ref double b, out double bp, out double hp)
    {
        hp = (x - (k + g) * a - b) / (1 + g * (g + k)); double v1 = g * hp; bp = v1 + a; a = bp + v1; double v2 = g * bp, lp = v2 + b; b = lp + v2; return lp;
    }
    // one band through its distortion (d = drive 0..1); the level falls as the drive rises so pushing it does not just make it louder
    double Shape(int t, double x, double d, int slot)
    {
        double g = 1 + d * d * 40, y;
        switch (t)
        {
            case 0: y = Math.Tanh(g * x + 0.25 * d) - Math.Tanh(0.25 * d); break;                       // tube: soft and a little lopsided (warm even harmonics)
            case 1: y = Math.Tanh(g * 0.7 * x) / 0.7; break;                                              // tape: soft and even
            case 2: y = Math.Max(-1, Math.Min(1, g * 2 * x)); break;                                      // fuzz: hard square edges
            case 3: { double s = Math.Tanh(g * x); y = s * (1 - d) + (Math.Abs(s) * 2 - 0.6) * d; break; } // rectify: folds the bottom half up (an octave up, gnarly)
            default:
                {   // crush: fewer bits and a lower sample rate as the drive goes up
                    int hold = 1 + (int)(d * 24); if (++holdN[slot] >= hold) { holdN[slot] = 0; double bits = 16 - d * 13, st = Math.Pow(2, -(bits - 1)); held[slot] = Math.Round(x * (1 + d * 3) / st) * st; }
                    return held[slot] / (1 + d * 1.5);
                }
        }
        // a DC blocker (rectify and tube make a little DC)
        double o = y - dcX[slot] + 0.9995 * dcY[slot]; dcX[slot] = y; dcY[slot] = o; return o / (1 + d * 3);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double ra = Math.Exp(-1.0 / (0.01 * SR)), rr = Math.Exp(-1.0 / (0.12 * SR)); float ip = 0, op = 0, grMax = 0;
        for (int i = 0; i < n; i++)
        {
            int rd = (dw - fbLen + dlL.Length) % dlL.Length; double fl = Math.Tanh(dlL[rd]) * fbAmt, fr = Math.Tanh(dlR[rd]) * fbAmt;
            double xl = l[i], xr = r[i]; ip = (float)Math.Max(ip, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            double yl = Chain(xl + fl, 0), yr = Chain(xr + fr, 1);
            dlL[dw] = (float)yl; dlR[dw] = (float)yr; dw = (dw + 1) % dlL.Length;
            if (filt > 0) { yl = Filter(yl, 0); yr = Filter(yr, 1); }
            double gr = 1;
            if (compRatio > 1.001)
            {   // one-knob compressor: lower threshold and higher ratio as it turns up, with half the loss made up
                double a = Math.Max(Math.Abs(yl), Math.Abs(yr)); env = a > env ? a + (env - a) * ra : a + (env - a) * rr;
                if (env > compThr) gr = Math.Pow(env / compThr, 1 / compRatio - 1); grMax = (float)Math.Max(grMax, -20 * Math.Log10(Math.Max(1e-5, gr))); gr *= compMk;
            }
            yl *= gr; yr *= gr;
            double ol = (xl + (yl - xl) * mixW) * outG, or = (xr + (yr - xr) * mixW) * outG; l[i] = (float)ol; r[i] = (float)or;
            op = (float)Math.Max(op, Math.Max(Math.Abs(ol), Math.Abs(or))); Scope.Push((float)((ol + or) * 0.5));
        }
        InPk = Math.Max(InPk * 0.9f, ip); OutPk = Math.Max(OutPk * 0.9f, op); GRdb = grMax > GRdb ? grMax : GRdb * 0.9f;
    }
    double Chain(double x, int ch)
    {
        double bp, hp, t; int a = ch * 12;
        double low = Svf(x, gLo, kSplit, ref x1[a], ref x2[a], out bp, out hp); low = Svf(low, gLo, kSplit, ref x1[a + 1], ref x2[a + 1], out bp, out hp);
        Svf(x, gLo, kSplit, ref x1[a + 2], ref x2[a + 2], out bp, out t); double rest; Svf(t, gLo, kSplit, ref x1[a + 3], ref x2[a + 3], out bp, out rest);
        double mid = Svf(rest, gHi, kSplit, ref x1[a + 4], ref x2[a + 4], out bp, out hp); mid = Svf(mid, gHi, kSplit, ref x1[a + 5], ref x2[a + 5], out bp, out hp);
        Svf(rest, gHi, kSplit, ref x1[a + 6], ref x2[a + 6], out bp, out t); double high; Svf(t, gHi, kSplit, ref x1[a + 7], ref x2[a + 7], out bp, out high);
        double lA = Svf(low, gHi, kSplit, ref x1[a + 8], ref x2[a + 8], out bp, out hp); lA = Svf(lA, gHi, kSplit, ref x1[a + 9], ref x2[a + 9], out bp, out hp);
        Svf(low, gHi, kSplit, ref x1[a + 10], ref x2[a + 10], out bp, out t); double lB; Svf(t, gHi, kSplit, ref x1[a + 11], ref x2[a + 11], out bp, out lB); low = lA + lB;   // the same phase turn as mid + high
        double y = 0; double band;
        for (int b = 0; b < 3; b++)
        {
            band = b == 0 ? low : b == 1 ? mid : high; if (!on[b]) { y += band; continue; }
            double s = dr[b] <= 0 && ty[b] != 4 ? band : Shape(ty[b], band, dr[b], ch * 3 + b);
            y += (band + (s - band) * mx[b]) * lv[b];
        }
        return y;
    }
    double Filter(double x, int ch)
    {
        double bp, hp; double lp = Svf(x, filtG, filtK, ref s1[4 + ch * 2], ref s2[4 + ch * 2], out bp, out hp); return filt == 1 ? lp : filt == 2 ? bp * filtK : hp;
    }
}

#if !PURPHEX_CORE
class TriDriveWindow : PxWindow
{
    readonly TriDriveFx t; static readonly string[] BandName = { "LOW", "MID", "HIGH" };
    public TriDriveWindow(TriDriveFx f) : base(f, "Tri-Drive", new Size(940, 720)) { t = f; Accent = Orange; }
    protected override string SubTitle { get { return "three-band distortion"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        Color[] col = { Pink, Orange, Cyan };
        // the spectrum with the three bands and their split lines (drag them)
        var sp = new RectangleF(a.X, a.Y, a.Width, 150); Fill(g, Well, sp, 10);
        Func<double, float> xOf = hz => sp.X + 8 + (float)(Math.Log(hz / 20) / Math.Log(1000)) * (sp.Width - 16);
        Func<float, double> hzOf = x => 20 * Math.Pow(1000, (x - sp.X - 8) / (sp.Width - 16));
        float x1 = xOf(fx.Get("xlo")), x2 = xOf(fx.Get("xhi"));
        using (var b = new SolidBrush(Color.FromArgb(40, col[0]))) g.FillRectangle(b, sp.X + 8, sp.Y + 6, x1 - sp.X - 8, sp.Height - 12);
        using (var b = new SolidBrush(Color.FromArgb(40, col[1]))) g.FillRectangle(b, x1, sp.Y + 6, x2 - x1, sp.Height - 12);
        using (var b = new SolidBrush(Color.FromArgb(40, col[2]))) g.FillRectangle(b, x2, sp.Y + 6, sp.Right - 8 - x2, sp.Height - 12);
        var db = t.Scope.Spectrum(2048); var pts = new PointF[180];
        for (int i = 0; i < pts.Length; i++) { float x = sp.X + 8 + i * (sp.Width - 16) / (pts.Length - 1); double hz = hzOf(x); int k = (int)Math.Min(db.Length - 1, hz / 44100.0 * 2048); pts[i] = new PointF(x, sp.Bottom - 8 - (float)Math.Max(0, Math.Min(1, (db[k] + 90) / 90)) * (sp.Height - 16)); }
        using (var pen = new Pen(Color.FromArgb(220, Ink), 1.5f)) g.DrawLines(pen, pts);
        for (int s = 0; s < 2; s++)
        {
            string id = s == 0 ? "xlo" : "xhi"; float x = s == 0 ? x1 : x2; using (var pen = new Pen(Gold, 2)) g.DrawLine(pen, x, sp.Y + 6, x, sp.Bottom - 6);
            var lab = new RectangleF(x - 40, sp.Y + 8, 80, 16); Fill(g, PBar, lab, 6); Txt(g, Val(fx.Find(id)), 10, Gold, lab, StringAlignment.Center, true);
            var h = Reg(new RectangleF(x - 8, sp.Y, 16, sp.Height), null, "Drag the split between the bands"); h.P = fx.Find(id); string iid = id; h.DragAt = pt => fx.Set(iid, (float)hzOf(pt.X));
        }
        for (int b = 0; b < 3; b++) Txt(g, BandName[b], 11, col[b], new RectangleF((b == 0 ? sp.X + 8 : b == 1 ? x1 : x2) + 6, sp.Bottom - 22, 60, 14), StringAlignment.Near, true);
        // the three bands
        float bw = (a.Width - 20) / 3f, by = sp.Bottom + 10, bh = 290;
        for (int b = 0; b < 3; b++)
        {
            var p = new RectangleF(a.X + b * (bw + 10), by, bw, bh); Panel(g, p, BandName[b], col[b]); string k = TriDriveFx.Bands[b];
            Toggle(g, new RectangleF(p.Right - 70, p.Y + 8, 56, 22), k + "_on", col[b]);
            Choice(g, new RectangleF(p.X + 16, p.Y + 40, p.Width - 32, 24), k + "_type", col[b], 10.5f);
            Knob(g, p.X + p.Width / 2, p.Y + 120, 40, k + "_drive", "DRIVE", col[b], true);
            Knob(g, p.X + p.Width * 0.27f, p.Y + 230, 20, k + "_lvl", "LEVEL", col[b]); Knob(g, p.X + p.Width * 0.73f, p.Y + 230, 20, k + "_mix", "MIX", col[b]);
        }
        // after the bands
        var q = new RectangleF(a.X, by + bh + 10, a.Width, a.Bottom - by - bh - 10); Panel(g, q, "", Violet); float cy = q.Y + q.Height / 2 - 12;
        Txt(g, "FILTER", 10, Dim, new RectangleF(q.X + 16, q.Y + 10, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(q.X + 16, q.Y + 28, 250, 24), "filt", Lime, 9.5f);
        Knob(g, q.X + 70, cy + 34, 18, "cut", "CUTOFF", Lime); Knob(g, q.X + 190, cy + 34, 18, "res", "RES", Lime);
        Knob(g, q.X + 340, cy + 6, 26, "comp", "COMP", Violet); Meter(g, new RectangleF(q.X + 384, q.Y + 14, 12, q.Height - 44), t.GRdb, "GR", true);
        Knob(g, q.X + 480, cy + 6, 24, "fb", "FEEDBACK", Pink); Knob(g, q.X + 580, cy + 6, 24, "fbt", "TIME", Pink);
        Knob(g, q.X + 690, cy + 6, 24, "mix", "MIX", Ink); Knob(g, q.X + 790, cy + 6, 24, "out", "OUTPUT", Ink);
        Meter(g, new RectangleF(q.Right - 50, q.Y + 14, 12, q.Height - 44), t.InPk, "IN"); Meter(g, new RectangleF(q.Right - 30, q.Y + 14, 12, q.Height - 44), t.OutPk, "OUT");
    }
}
#endif

// ======================================================================
//  PURPHEX SHIFT: two frequency shifters (A and B). A frequency shifter moves every frequency by the same number of Hz (not like pitch:
//  harmonics stop lining up, so small shifts give a moving phaser-like sweep and big ones turn sounds metallic or bell-like).
//  Each shifter: ON, SHIFT (Hz, up or down), DIRECTION (up, down, both = ring mod), an LFO (rate and depth in Hz) and MIX.
//  SERIAL or PARALLEL, FEEDBACK through a delay (endless rising or falling spirals), SPREAD (left and right shifted apart), MIX and OUTPUT.
// ======================================================================
sealed class PxFreqShifter
{
    static readonly double[] A = { 0.6923878 * 0.6923878, 0.9360654322959 * 0.9360654322959, 0.9882295226860 * 0.9882295226860, 0.9987488452737 * 0.9987488452737 };
    static readonly double[] B = { 0.4021921162426 * 0.4021921162426, 0.8561710882420 * 0.8561710882420, 0.9722909545651 * 0.9722909545651, 0.9952884791278 * 0.9952884791278 };
    readonly double[] ax1 = new double[4], ax2 = new double[4], ay1 = new double[4], ay2 = new double[4], bx1 = new double[4], bx2 = new double[4], by1 = new double[4], by2 = new double[4];
    double bPrev, ph;
    public void Reset() { Array.Clear(ax1, 0, 4); Array.Clear(ax2, 0, 4); Array.Clear(ay1, 0, 4); Array.Clear(ay2, 0, 4); Array.Clear(bx1, 0, 4); Array.Clear(bx2, 0, 4); Array.Clear(by1, 0, 4); Array.Clear(by2, 0, 4); bPrev = 0; ph = 0; }
    // two all-pass chains 90 degrees apart (a Hilbert pair): rotating them by the shift frequency moves the whole spectrum
    public double Process(double x, double hz, int dir, int sr)
    {
        double re = x, im = x;
        for (int k = 0; k < 4; k++) { double y = A[k] * (re + ay2[k]) - ax2[k]; ax2[k] = ax1[k]; ax1[k] = re; ay2[k] = ay1[k]; ay1[k] = y; re = y; }
        for (int k = 0; k < 4; k++) { double y = B[k] * (im + by2[k]) - bx2[k]; bx2[k] = bx1[k]; bx1[k] = im; by2[k] = by1[k]; by1[k] = y; im = y; }
        double imD = im; double reD = bPrev; bPrev = re; re = reD;   // the first chain is the one a sample behind
        ph += 2 * Math.PI * hz / sr; if (ph > Math.PI) ph -= 2 * Math.PI; else if (ph < -Math.PI) ph += 2 * Math.PI;
        double c = Math.Cos(ph), s = Math.Sin(ph);
        return dir == 0 ? re * c + imD * s : dir == 1 ? re * c - imD * s : re * c;
    }
}

class ShiftFx : Fx
{
    public static readonly string[] Dirs = { "Up", "Down", "Both" };
    readonly PxFreqShifter aL = new PxFreqShifter(), aR = new PxFreqShifter(), bL = new PxFreqShifter(), bR = new PxFreqShifter();
    readonly float[] dlL = new float[192000], dlR = new float[192000]; int dw; double lfoA, lfoB;
    bool aOn, bOn, par; int aDir, bDir, fbLen; double aHz, bHz, aRate, aDep, bRate, bDep, aMix, bMix, fb, spread, mixW, outG;
    public volatile float ShowA, ShowB; public readonly PxScope Scope = new PxScope(4096);
    public ShiftFx()
    {
        Type = "pxshift"; Title = "Purphex Shift"; Category = "Purphex VST"; Blurb = "Two frequency shifters with LFOs, feedback and stereo spread";
        AddC("a_on", "A on", new[] { "Off", "On" }, 1); AddP("a_shift", "A shift", -2000, 2000, 100, "Hz"); AddC("a_dir", "A direction", Dirs, 0); AddP("a_rate", "A LFO rate", 0.05f, 10, 0.5f, "Hz"); AddP("a_dep", "A LFO depth", 0, 1000, 0, "Hz"); AddP("a_mix", "A mix", 0, 100, 100, "%");
        AddC("b_on", "B on", new[] { "Off", "On" }, 0); AddP("b_shift", "B shift", -2000, 2000, -50, "Hz"); AddC("b_dir", "B direction", Dirs, 0); AddP("b_rate", "B LFO rate", 0.05f, 10, 0.25f, "Hz"); AddP("b_dep", "B LFO depth", 0, 1000, 0, "Hz"); AddP("b_mix", "B mix", 0, 100, 100, "%");
        AddC("route", "Routing", new[] { "Serial", "Parallel" }, 0); AddP("fb", "Feedback", 0, 95, 0, "%"); AddP("fbt", "Feedback time", 1, 1000, 120, "ms");
        AddP("spread", "Stereo spread", -500, 500, 0, "Hz"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); aL.Reset(); aR.Reset(); bL.Reset(); bR.Reset(); Array.Clear(dlL, 0, dlL.Length); Array.Clear(dlR, 0, dlR.Length); dw = 0; lfoA = lfoB = 0; }
    protected override void Update()
    {
        aOn = Get("a_on") >= 0.5f; bOn = Get("b_on") >= 0.5f; par = Get("route") >= 0.5f; aDir = (int)Get("a_dir"); bDir = (int)Get("b_dir");
        aHz = Get("a_shift"); bHz = Get("b_shift"); aRate = Get("a_rate"); aDep = Get("a_dep"); bRate = Get("b_rate"); bDep = Get("b_dep"); aMix = Get("a_mix") / 100.0; bMix = Get("b_mix") / 100.0;
        fb = Get("fb") / 100.0; fbLen = (int)Math.Max(1, Math.Min(dlL.Length - 2, Get("fbt") * 0.001 * SR)); spread = Get("spread"); mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double fa = 0, fbv = 0;
        for (int i = 0; i < n; i++)
        {
            lfoA += aRate / SR; if (lfoA >= 1) lfoA -= 1; lfoB += bRate / SR; if (lfoB >= 1) lfoB -= 1;
            fa = aHz + aDep * Math.Sin(2 * Math.PI * lfoA); fbv = bHz + bDep * Math.Sin(2 * Math.PI * lfoB);
            int rd = (dw - fbLen + dlL.Length) % dlL.Length; double xl = l[i], xr = r[i], il = xl + Math.Tanh(dlL[rd] * fb), ir = xr + Math.Tanh(dlR[rd] * fb);
            double yl = Chain(il, aL, bL, fa - spread / 2, fbv - spread / 2), yr = Chain(ir, aR, bR, fa + spread / 2, fbv + spread / 2);
            dlL[dw] = (float)yl; dlR[dw] = (float)yr; dw = (dw + 1) % dlL.Length;
            double ol = (xl + (yl - xl) * mixW) * outG, or = (xr + (yr - xr) * mixW) * outG; l[i] = (float)ol; r[i] = (float)or; Scope.Push((float)((ol + or) * 0.5));
        }
        ShowA = (float)fa; ShowB = (float)fbv;
    }
    double Chain(double x, PxFreqShifter a, PxFreqShifter b, double ha, double hb)
    {
        double ya = aOn ? x + (a.Process(x, ha, aDir, SR) - x) * aMix : x;
        if (!bOn) return ya;
        if (par) { double yb = x + (b.Process(x, hb, bDir, SR) - x) * bMix; return aOn ? (ya + yb) * 0.5 : yb; }
        return ya + (b.Process(ya, hb, bDir, SR) - ya) * bMix;
    }
}

#if !PURPHEX_CORE
class ShiftWindow : PxWindow
{
    readonly ShiftFx s;
    public ShiftWindow(ShiftFx f) : base(f, "Shift", new Size(940, 640)) { s = f; Accent = Cyan; }
    protected override string SubTitle { get { return "dual frequency shifter"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the spectrum, with where each shifter is moving the sound right now
        var sp = new RectangleF(a.X, a.Y, a.Width, 140); Fill(g, Well, sp, 10); var db = s.Scope.Spectrum(2048); var pts = new PointF[200];
        for (int i = 0; i < pts.Length; i++) { float x = sp.X + 8 + i * (sp.Width - 16) / (pts.Length - 1); double hz = 20 * Math.Pow(1000, (x - sp.X - 8) / (sp.Width - 16)); int k = (int)Math.Min(db.Length - 1, hz / 44100.0 * 2048); pts[i] = new PointF(x, sp.Bottom - 8 - (float)Math.Max(0, Math.Min(1, (db[k] + 90) / 90)) * (sp.Height - 16)); }
        using (var pen = new Pen(Color.FromArgb(220, Ink), 1.5f)) g.DrawLines(pen, pts);
        Txt(g, "A  " + (s.ShowA >= 0 ? "+" : "") + s.ShowA.ToString("0") + " Hz", 12, Cyan, new RectangleF(sp.X + 14, sp.Y + 8, 160, 18), StringAlignment.Near, true);
        Txt(g, "B  " + (s.ShowB >= 0 ? "+" : "") + s.ShowB.ToString("0") + " Hz", 12, Pink, new RectangleF(sp.X + 14, sp.Y + 28, 160, 18), StringAlignment.Near, true);
        float pw = (a.Width - 230) / 2f, py = sp.Bottom + 10, ph = a.Bottom - py;
        for (int k = 0; k < 2; k++)
        {
            string p = k == 0 ? "a_" : "b_"; Color c = k == 0 ? Cyan : Pink; var r = new RectangleF(k == 0 ? a.X : a.Right - pw, py, pw, ph); Panel(g, r, k == 0 ? "SHIFTER A" : "SHIFTER B", c);
            Toggle(g, new RectangleF(r.Right - 70, r.Y + 8, 56, 22), p + "on", c);
            Knob(g, r.X + r.Width / 2, r.Y + 110, 54, p + "shift", "SHIFT", c, true);
            Segments(g, new RectangleF(r.X + 20, r.Y + 206, r.Width - 40, 24), p + "dir", c, 10);
            Txt(g, "LFO", 10, Dim, new RectangleF(r.X + 20, r.Y + 240, 60, 14), StringAlignment.Near, true);
            Knob(g, r.X + r.Width * 0.22f, r.Y + 290, 20, p + "rate", "RATE", c); Knob(g, r.X + r.Width * 0.5f, r.Y + 290, 20, p + "dep", "DEPTH", c); Knob(g, r.X + r.Width * 0.78f, r.Y + 290, 20, p + "mix", "MIX", c);
        }
        var m = new RectangleF(a.X + pw + 10, py, 210, ph); Panel(g, m, "", Violet);
        Txt(g, "ROUTING", 10, Dim, new RectangleF(m.X + 16, m.Y + 12, 120, 14), StringAlignment.Near, true); Segments(g, new RectangleF(m.X + 16, m.Y + 30, m.Width - 32, 24), "route", Violet, 10);
        Knob(g, m.X + 60, m.Y + 110, 22, "fb", "FEEDBACK", Gold); Knob(g, m.X + 150, m.Y + 110, 22, "fbt", "TIME", Gold);
        Knob(g, m.X + m.Width / 2, m.Y + 200, 22, "spread", "SPREAD", Lime);
        Knob(g, m.X + 60, m.Y + 290, 20, "mix", "MIX", Ink); Knob(g, m.X + 150, m.Y + 290, 20, "out", "OUTPUT", Ink);
    }
}
#endif
