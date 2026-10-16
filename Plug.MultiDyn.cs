using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX MULTIDYN: a five-band compressor. The sound is split at four crossovers (the bands always add back to the input), each band has its
//  own threshold, ratio, attack, release and gain, plus ON / OFF and SOLO. A ratio under 1 lifts quiet parts instead (upward). LEARN sets every
//  threshold from the sound playing now. KNEE, CLEAN or VINTAGE (a little warmth in each band), a final LIMITER, MIX and OUTPUT.
//  The display shows the spectrum, the bands (drag a crossover line) and how much each band is turning down.
// ======================================================================
class MultiDynFx : Fx
{
    public const int B = 5;
    public static readonly string[] Names = { "LOW", "LOW-MID", "MID", "HIGH-MID", "HIGH" };
    readonly double[,] st = new double[4, 4]; readonly double[] env = new double[B], gr = new double[B], lvl = new double[B];
    readonly double[] band = new double[B * 2]; double limG = 1, mixS = 1, outG = 1;
    public readonly PxScope Scope = new PxScope(8192); public readonly float[] GrView = new float[B], LvlView = new float[B]; public volatile float InPk, OutPk;
    static readonly string[] Id = { "b0", "b1", "b2", "b3", "b4" };
    readonly bool[] cOn = new bool[B], cSolo = new bool[B]; readonly double[] cThr = new double[B], cRatio = new double[B], cAtt = new double[B], cRel = new double[B], cGain = new double[B]; readonly double[] cX = new double[4];
    public MultiDynFx()
    {
        Type = "pxmultidyn"; Title = "Purphex MultiDyn"; Category = "Purphex VST"; Blurb = "Five-band compressor with learn, solo, vintage mode and a limiter";
        double[] xd = { 120, 500, 2000, 7000 };
        for (int k = 0; k < 4; k++) AddP("x" + k, "Crossover " + (k + 1), 40, 16000, (float)xd[k], "Hz");
        for (int k = 0; k < B; k++)
        {
            string n = Names[k].Substring(0, 1) + Names[k].Substring(1).ToLowerInvariant();
            AddC(Id[k] + "_on", n + " on", new[] { "Off", "On" }, 1); AddC(Id[k] + "_solo", n + " solo", new[] { "Off", "On" }, 0);
            AddP(Id[k] + "_thr", n + " threshold", -60, 0, -24, "dB"); AddP(Id[k] + "_ratio", n + " ratio", 0.5f, 20, 2.5f, ":1"); AddP(Id[k] + "_att", n + " attack", 0.1f, 200, 20, "ms"); AddP(Id[k] + "_rel", n + " release", 5, 2000, 150, "ms"); AddP(Id[k] + "_gain", n + " gain", -24, 24, 0, "dB");
        }
        AddP("knee", "Knee", 0, 24, 6, "dB"); AddC("mode", "Mode", new[] { "Clean", "Vintage" }, 0); AddC("limit", "Limiter", new[] { "Off", "On" }, 0); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 24, 0, "dB");
    }
    public override void Reset() { base.Reset(); Array.Clear(st, 0, st.Length); Array.Clear(env, 0, B); Array.Clear(gr, 0, B); limG = 1; }
    protected override void Update()
    {
        double prev = 20; for (int k = 0; k < 4; k++) { cX[k] = Math.Max(prev * 1.2, Math.Min(SR * 0.45, Get("x" + k))); prev = cX[k]; }
        for (int k = 0; k < B; k++) { cOn[k] = Get(Id[k] + "_on") >= 0.5f; cSolo[k] = Get(Id[k] + "_solo") >= 0.5f; cThr[k] = Get(Id[k] + "_thr"); cRatio[k] = Get(Id[k] + "_ratio"); cAtt[k] = Math.Exp(-1.0 / (Get(Id[k] + "_att") * 0.001 * SR)); cRel[k] = Math.Exp(-1.0 / (Get(Id[k] + "_rel") * 0.001 * SR)); cGain[k] = Get(Id[k] + "_gain"); }
    }
    // LEARN: each threshold a little under what its band is doing now
    public void Learn() { for (int k = 0; k < B; k++) Set(Id[k] + "_thr", (float)Math.Max(-60, Math.Min(0, 20 * Math.Log10(lvl[k] + 1e-9) - 4))); }
    double Lp(int k, int ch, double x, double g)
    {
        int o = ch * 2; double s1 = st[k, o], s2 = st[k, o + 1];
        double hp = (x - (1.4142 + g) * s1 - s2) / (1 + g * (g + 1.4142)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, low = v2 + s2; s2 = low + v2;
        st[k, o] = s1; st[k, o + 1] = s2; return low;
    }
    public static double GrDb(double x, double thr, double ratio, double knee)
    {
        if (ratio >= 1) { double over = x - thr; if (2 * over < -knee) return 0; if (knee > 0 && 2 * Math.Abs(over) <= knee) return -(1 / ratio - 1) * (over + knee / 2) * (over + knee / 2) / (2 * knee); return over - over / ratio; }
        { double under = thr - x; if (under <= 0 || x < -80) return 0; return -Math.Min(18, under * (1 / ratio - 1)); }   // upward: quiet parts lifted (negative reduction)
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double knee = Get("knee"); bool vintage = Get("mode") >= 0.5f, limit = Get("limit") >= 0.5f; double mixT = Get("mix") / 100.0, outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        bool anySolo = false; for (int k = 0; k < B; k++) if (cSolo[k]) anySolo = true;
        double g0 = Math.Tan(Math.PI * cX[0] / SR), g1 = Math.Tan(Math.PI * cX[1] / SR), g2 = Math.Tan(Math.PI * cX[2] / SR), g3 = Math.Tan(Math.PI * cX[3] / SR), la = 1 - Math.Exp(-1.0 / (0.05 * SR)), lr = Math.Exp(-1.0 / (0.08 * SR));
        float inPk = 0, outPk = 0; for (int k = 0; k < B; k++) GrView[k] = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i]; inPk = (float)Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr))); Scope.Push((float)((xl + xr) * 0.5));
            for (int ch = 0; ch < 2; ch++)
            {
                double x = ch == 0 ? xl : xr, b0 = Lp(0, ch, x, g0), rest = x - b0, b1 = Lp(1, ch, rest, g1); rest -= b1; double b2 = Lp(2, ch, rest, g2); rest -= b2; double b3 = Lp(3, ch, rest, g3), b4 = rest - b3;
                band[ch * B] = b0; band[ch * B + 1] = b1; band[ch * B + 2] = b2; band[ch * B + 3] = b3; band[ch * B + 4] = b4;
            }
            double yl = 0, yr = 0;
            for (int k = 0; k < B; k++)
            {
                double bl = band[k], brr = band[B + k], a = Math.Max(Math.Abs(bl), Math.Abs(brr)); lvl[k] += (a - lvl[k]) * la;
                double g = 1;
                if (cOn[k]) { env[k] = a > env[k] ? cAtt[k] * env[k] + (1 - cAtt[k]) * a : cRel[k] * env[k] + (1 - cRel[k]) * a; gr[k] = GrDb(20 * Math.Log10(env[k] + 1e-9), cThr[k], cRatio[k], knee); g = Math.Pow(10, (cGain[k] - gr[k]) / 20); GrView[k] = (float)Math.Max(GrView[k], gr[k]); }
                bl *= g; brr *= g; if (vintage && cOn[k]) { bl = Math.Tanh(bl * 1.4) / 1.4; brr = Math.Tanh(brr * 1.4) / 1.4; }
                if (anySolo && !cSolo[k]) continue; yl += bl; yr += brr;
            }
            if (limit) { double p = Math.Max(Math.Abs(yl), Math.Abs(yr)) * limG; if (p > 0.97) limG = 0.97 / Math.Max(Math.Abs(yl), Math.Abs(yr)); else limG = Math.Min(1, 1 - (1 - limG) * lr); yl *= limG; yr *= limG; }
            mixS += (mixT - mixS) * gs; outG += (outT - outG) * gs;
            double ol = (xl + (yl - xl) * mixS) * outG, or = (xr + (yr - xr) * mixS) * outG;
            if (double.IsNaN(ol) || double.IsNaN(or) || double.IsInfinity(ol) || double.IsInfinity(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or; outPk = (float)Math.Max(outPk, Math.Max(Math.Abs(ol), Math.Abs(or)));
        }
        for (int k = 0; k < B; k++) LvlView[k] = (float)lvl[k]; InPk = Math.Max(InPk * 0.85f, inPk); OutPk = Math.Max(OutPk * 0.85f, outPk);
    }
}

class MultiDynWindow : PxWindow
{
    readonly MultiDynFx m; int sel = 2; RectangleF gr;
    static readonly Color[] Col = { Color.FromArgb(255, 110, 90), Color.FromArgb(255, 190, 70), Color.FromArgb(120, 230, 120), Color.FromArgb(70, 190, 255), Color.FromArgb(190, 120, 255) };
    public MultiDynWindow(MultiDynFx f) : base(f, "MultiDyn", new Size(1000, 660)) { m = f; Accent = Lime; }
    protected override string SubTitle { get { return "five-band compressor"; } }
    float X(double f) { return gr.X + (float)(Math.Log(f / 20) / Math.Log(1000) * gr.Width); }
    double F(float x) { return 20 * Math.Pow(1000, Math.Max(0, Math.Min(1, (x - gr.X) / gr.Width))); }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        gr = new RectangleF(a.X + 10, a.Y + 6, a.Width - 20, 260); Fill(g, Color.FromArgb(14, 9, 28), new RectangleF(gr.X - 10, gr.Y - 6, gr.Width + 20, gr.Height + 12), 12);
        // bands (selected one lit), spectrum, crossovers, gain reduction per band
        float prev = gr.X;
        for (int k = 0; k < MultiDynFx.B; k++)
        {
            float right = k < 4 ? X(fx.Get("x" + k)) : gr.Right; var br = new RectangleF(prev, gr.Y, right - prev, gr.Height);
            using (var b = new SolidBrush(Color.FromArgb(k == sel ? 60 : 22, Col[k]))) g.FillRectangle(b, br);
            float grv = m.GrView[k]; float gh = Math.Min(1, Math.Abs(grv) / 24) * gr.Height * 0.5f; using (var b = new SolidBrush(Color.FromArgb(170, grv >= 0 ? Orange : Lime))) g.FillRectangle(b, br.X + 4, grv >= 0 ? gr.Y : gr.Y + gr.Height * 0.5f - gh, br.Width - 8, gh);
            Txt(g, MultiDynFx.Names[k], 10, Col[k], new RectangleF(br.X, gr.Bottom - 16, br.Width, 14), StringAlignment.Center, true);
            int kk = k; var h = Reg(br, () => sel = kk, MultiDynFx.Names[k] + ": click to edit"); prev = right;
        }
        { var db = m.Scope.Spectrum(4096); var pts = new System.Collections.Generic.List<PointF>(); for (float x = gr.X; x <= gr.Right; x += 2) { int bin = (int)(F(x) * 4096 / m.SR); if (bin < 1 || bin >= db.Length) continue; pts.Add(new PointF(x, Math.Max(gr.Y, Math.Min(gr.Bottom, gr.Y + (float)(-db[bin] / 90.0 * gr.Height))))); } if (pts.Count > 2) using (var pen = new Pen(Color.FromArgb(200, 220, 220, 240), 1.3f)) g.DrawLines(pen, pts.ToArray()); }
        for (int k = 0; k < 4; k++)
        {
            float x = X(fx.Get("x" + k)); using (var pen = new Pen(Color.White, 2)) g.DrawLine(pen, x, gr.Y, x, gr.Bottom); var p = fx.Find("x" + k); string id = "x" + k;
            var h = Reg(new RectangleF(x - 5, gr.Y, 10, gr.Height), null, "Crossover " + (k + 1) + ": drag sideways (" + Val(p) + ")"); h.P = p; h.DragAt = pt => fx.Set(id, (float)F(pt.X)); h.Double = () => { Mark(); fx.Set(id, p.Def); };
        }
        // the selected band
        var bp = new RectangleF(a.X, gr.Bottom + 16, a.Width - 260, a.Bottom - gr.Bottom - 16); Panel(g, bp, "BAND: " + MultiDynFx.Names[sel], Col[sel]);
        string id0 = "b" + sel; Toggle(g, new RectangleF(bp.Right - 130, bp.Y + 6, 56, 22), id0 + "_on", Col[sel]); Toggle(g, new RectangleF(bp.Right - 68, bp.Y + 6, 56, 22), id0 + "_solo", Gold, "SOLO", "solo");
        string[] ks = { "_thr", "_ratio", "_att", "_rel", "_gain" }, ls = { "THRESHOLD", "RATIO", "ATTACK", "RELEASE", "GAIN" }; float kw = bp.Width / 5;
        for (int i = 0; i < 5; i++) Knob(g, bp.X + kw * i + kw / 2, bp.Y + 80, 30, id0 + ks[i], ls[i], Col[sel]);
        Button(g, new RectangleF(bp.X + 16, bp.Bottom - 40, 120, 28), "LEARN", false, Lime, () => { Mark(); m.Learn(); }, "Set every threshold from the sound playing now", 11);
        Txt(g, "ratio under 1:1 lifts quiet parts", 10, Dim, new RectangleF(bp.X + 150, bp.Bottom - 38, 260, 24), StringAlignment.Near, false);
        // global
        var gp = new RectangleF(bp.Right + 10, bp.Y, 250, bp.Height); Panel(g, gp, "", Violet);
        Knob(g, gp.X + 60, gp.Y + 50, 22, "knee", "KNEE", Violet); Segments(g, new RectangleF(gp.X + 110, gp.Y + 30, 126, 24), "mode", Violet, 10); Toggle(g, new RectangleF(gp.X + 140, gp.Y + 66, 80, 24), "limit", Gold, "LIMITER", "LIMITER");
        Knob(g, gp.X + 60, gp.Y + 150, 22, "mix", "MIX", Violet); Knob(g, gp.X + 140, gp.Y + 150, 22, "out", "OUTPUT", Violet);
        Meter(g, new RectangleF(gp.Right - 50, gp.Y + 110, 10, gp.Height - 140), m.InPk, "IN"); Meter(g, new RectangleF(gp.Right - 30, gp.Y + 110, 10, gp.Height - 140), m.OutPk, "OUT");
    }
}
