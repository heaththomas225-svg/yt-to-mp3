using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX ECHOGRID: eight echoes placed on a grid of sixteen steps in time with the song. Each echo has its own step (when it comes back),
//  LEVEL, PAN, TONE (darker or thinner), DRIVE, CRUSH and PITCH (up to an octave up or down). GRID sets how long a step is, FEEDBACK sends
//  the whole pattern round again, MIX and OUTPUT.
// ======================================================================
class EchoGridFx : SyncFx
{
    public const int N = 8; public static readonly string[] Grids = { "1/8", "1/16", "1/32", "1/8 triplet" }; static readonly double[] GB = { 0.5, 0.25, 0.125, 1.0 / 3 };
    static readonly string[] On = new string[N], St = new string[N], Lv = new string[N], Pn = new string[N], Tn = new string[N], Dr = new string[N], Cr = new string[N], Pt = new string[N];
    static EchoGridFx() { for (int k = 0; k < N; k++) { string e = "e" + k + "_"; On[k] = e + "on"; St[k] = e + "step"; Lv[k] = e + "lvl"; Pn[k] = e + "pan"; Tn[k] = e + "tone"; Dr[k] = e + "drive"; Cr[k] = e + "crush"; Pt[k] = e + "pitch"; } }
    readonly float[] buf = new float[2400000]; int w; readonly PxShifter[] sh = new PxShifter[N]; readonly double[] lp = new double[N], hold = new double[N]; readonly int[] hc = new int[N];
    readonly bool[] on = new bool[N]; readonly double[] step = new double[N], lvl = new double[N], pl = new double[N], pr = new double[N], tone = new double[N], drive = new double[N], crush = new double[N], ratio = new double[N];
    double fb, mixW, outG, gridB, fbState; public volatile int ShowStep = -1;
    public EchoGridFx()
    {
        Type = "pxechogrid"; Title = "Purphex EchoGrid"; Category = "Purphex VST"; Blurb = "Eight echoes on a step grid, each with its own pitch, tone, drive and crush";
        int[] ds = { 3, 6, 8, 11, 12, 14, 15, 16 }; float[] dl = { -6, -9, -12, -12, -15, -15, -18, -18 }; float[] dp = { -40, 40, -70, 70, 0, -20, 20, 0 };
        for (int k = 0; k < N; k++) { AddC(On[k], "Echo " + (k + 1) + " on", new[] { "Off", "On" }, k < 3 ? 1 : 0); AddP(St[k], "Echo " + (k + 1) + " step", 1, 16, ds[k], ""); AddP(Lv[k], "Echo " + (k + 1) + " level", -36, 6, dl[k], "dB"); AddP(Pn[k], "Echo " + (k + 1) + " pan", -100, 100, dp[k], ""); AddP(Tn[k], "Echo " + (k + 1) + " tone", -100, 100, k * 8, ""); AddP(Dr[k], "Echo " + (k + 1) + " drive", 0, 100, 0, "%"); AddP(Cr[k], "Echo " + (k + 1) + " crush", 0, 100, 0, "%"); AddP(Pt[k], "Echo " + (k + 1) + " pitch", -12, 12, 0, "st"); sh[k] = new PxShifter(8192); }
        AddC("grid", "Grid", Grids, 1); AddP("fb", "Feedback", 0, 90, 20, "%"); AddP("mix", "Mix", 0, 100, 50, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); Array.Clear(buf, 0, buf.Length); w = 0; foreach (var s in sh) s.Reset(); Array.Clear(lp, 0, N); Array.Clear(hold, 0, N); Array.Clear(hc, 0, N); fbState = 0; }
    protected override void Update()
    {
        for (int k = 0; k < N; k++) { on[k] = Get(On[k]) >= 0.5f; step[k] = Math.Round(Get(St[k])); lvl[k] = Math.Pow(10, Get(Lv[k]) / 20); double a = (Get(Pn[k]) / 100.0 + 1) * Math.PI / 4; pl[k] = Math.Cos(a) * 1.4142; pr[k] = Math.Sin(a) * 1.4142; tone[k] = Get(Tn[k]) / 100.0; drive[k] = Get(Dr[k]) / 100.0; crush[k] = Get(Cr[k]) / 100.0; ratio[k] = Math.Pow(2, Get(Pt[k]) / 12.0); }
        gridB = GB[(int)Get("grid")]; fb = Get("fb") / 100.0; mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double spb = 60.0 / Math.Max(20, Fx.Bpm) * SR * gridB; int L = buf.Length;
        for (int i = 0; i < n; i++)
        {
            double x = (l[i] + r[i]) * 0.5; buf[w] = (float)(x + Math.Tanh(fbState * fb)); double ol = 0, or = 0, sum = 0;
            for (int k = 0; k < N; k++)
            {
                if (!on[k]) continue; double d = Math.Min(L - 2, step[k] * spb); double rp = w - d; if (rp < 0) rp += L; int i0 = (int)rp; double fr = rp - i0; double v = buf[i0] + (buf[(i0 + 1) % L] - buf[i0]) * fr;
                if (ratio[k] != 1) v = sh[k].Tick(v, ratio[k], 0, SR);
                // TONE: below 0 a low-pass (darker), above 0 a high-pass (thinner)
                double t = tone[k], c = 1 - Math.Exp(-2 * Math.PI * (t < 0 ? 18000 * Math.Pow(0.012, -t) : 30 + 4000 * t * t) / SR); lp[k] += (v - lp[k]) * c; v = t < 0 ? lp[k] : t > 0 ? v - lp[k] : v;
                if (drive[k] > 0) { double g = 1 + drive[k] * 12; v = Math.Tanh(v * g) / Math.Tanh(g) * (1 + drive[k] * 0.3); }
                if (crush[k] > 0) { int hold2 = 1 + (int)(crush[k] * 16); if (++hc[k] >= hold2) { hc[k] = 0; double st = Math.Pow(2, -(16 - crush[k] * 12) + 1); hold[k] = Math.Round(v / st) * st; } v = hold[k]; }
                v *= lvl[k]; ol += v * pl[k]; or += v * pr[k]; sum += v;
            }
            fbState = sum; w = (w + 1) % L;
            l[i] = (float)((l[i] * (1 - mixW) + ol * mixW) * outG); r[i] = (float)((r[i] * (1 - mixW) + or * mixW) * outG);
        }
        ShowStep = (int)(Math.Floor(BeatAt(n - 1) / gridB) % 16); EndBlock(n);
    }
}

#if !PURPHEX_CORE
class EchoGridWindow : PxWindow
{
    readonly EchoGridFx e; int selEcho;
    public EchoGridWindow(EchoGridFx f) : base(f, "EchoGrid", new Size(980, 690)) { e = f; Accent = Pink; }
    protected override string SubTitle { get { return "eight echoes on a grid"; } }
    static readonly Color[] Col = { Color.FromArgb(255, 255, 80, 196), Color.FromArgb(255, 60, 225, 255), Color.FromArgb(255, 255, 205, 80), Color.FromArgb(255, 120, 240, 140), Color.FromArgb(255, 168, 92, 255), Color.FromArgb(255, 255, 150, 70), Color.FromArgb(255, 80, 160, 255), Color.FromArgb(255, 240, 240, 240) };
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the grid: one row per echo, one lit cell = the step it comes back on (click a cell to move it)
        var gr = new RectangleF(a.X, a.Y, a.Width, 300); Panel(g, gr, "", Pink); float hx = gr.X + 12, cx0 = gr.X + 170, cw = (gr.Right - 14 - cx0) / 16f, rh = (gr.Height - 40) / EchoGridFx.N, y0 = gr.Y + 30;
        for (int s = 0; s < 16; s++) Txt(g, (s + 1).ToString(), 9, s == e.ShowStep ? Ink : Dim, new RectangleF(cx0 + s * cw, gr.Y + 8, cw, 16), StringAlignment.Center, s == e.ShowStep);
        for (int k = 0; k < EchoGridFx.N; k++)
        {
            float y = y0 + k * rh; bool on = fx.Get("e" + k + "_on") >= 0.5f; int st = (int)Math.Round(fx.Get("e" + k + "_step")) - 1; int kk = k;
            var nameR = new RectangleF(hx, y + 3, 90, rh - 6); Button(g, nameR, "ECHO " + (k + 1), k == selEcho, Col[k], () => selEcho = kk, "Edit echo " + (k + 1) + " below", 10);
            Toggle(g, new RectangleF(hx + 96, y + 3, 54, rh - 6), "e" + k + "_on", Col[k]);
            for (int s = 0; s < 16; s++)
            {
                var c = new RectangleF(cx0 + s * cw + 2, y + 3, cw - 4, rh - 6); bool lit = s == st;
                Fill(g, lit ? Color.FromArgb(on ? 255 : 90, Col[k]) : (s % 4 == 0 ? CtlHot : Ctl), c, 5); if (s == e.ShowStep) Stroke(g, Color.FromArgb(120, 255, 255, 255), c, 5, 1);
                int ss = s; var h = Reg(c, () => { Mark(); fx.Set("e" + kk + "_step", ss + 1); if (fx.Get("e" + kk + "_on") < 0.5f) fx.Set("e" + kk + "_on", 1); selEcho = kk; }, "Echo " + (k + 1) + " comes back on step " + (s + 1));
            }
        }
        // the echo being edited
        var ep = new RectangleF(a.X, gr.Bottom + 10, a.Width - 300, a.Bottom - gr.Bottom - 10); Panel(g, ep, "ECHO " + (selEcho + 1), Col[selEcho]); string p = "e" + selEcho + "_"; float ky = ep.Y + ep.Height / 2 + 4, kw = ep.Width / 6;
        string[] ids = { "lvl", "pan", "tone", "drive", "crush", "pitch" }, lab = { "LEVEL", "PAN", "TONE", "DRIVE", "CRUSH", "PITCH" };
        for (int i = 0; i < 6; i++) Knob(g, ep.X + kw * (i + 0.5f), ky - 6, 24, p + ids[i], lab[i], Col[selEcho]);
        var gp = new RectangleF(ep.Right + 10, ep.Y, a.Right - ep.Right - 10, ep.Height); Panel(g, gp, "", Violet);
        Txt(g, "GRID", 10, Dim, new RectangleF(gp.X + 16, gp.Y + 12, 100, 14), StringAlignment.Near, true); Choice(g, new RectangleF(gp.X + 16, gp.Y + 30, gp.Width - 32, 24), "grid", Violet, 10.5f);
        Knob(g, gp.X + gp.Width * 0.25f, gp.Y + 110, 22, "fb", "FEEDBACK", Gold); Knob(g, gp.X + gp.Width * 0.5f, gp.Y + 110, 22, "mix", "MIX", Ink); Knob(g, gp.X + gp.Width * 0.75f, gp.Y + 110, 22, "out", "OUTPUT", Ink);
    }
}
#endif

// ======================================================================
//  PURPHEX MODAL FILTER: two filters (A and B), each with its own TYPE (low-pass, high-pass, band-pass, notch, peak, comb, formant),
//  CUTOFF, RESONANCE and DRIVE. ROUTING: morph between them, run them one after the other, or side by side. The ENVELOPE follower (how loud
//  the sound is) and the LFO move the cutoffs or the morph. MIX and OUTPUT.
// ======================================================================
class ModalFilterFx : Fx
{
    public static readonly string[] Types = { "Low-pass", "High-pass", "Band-pass", "Notch", "Peak", "Comb", "Formant" }, Routes = { "Morph", "Serial", "Parallel" }, Shapes = { "Sine", "Triangle", "Square", "Random" }, Targets = { "Both cutoffs", "Cutoff A", "Cutoff B", "Morph" };
    sealed class Flt { public double s1L, s2L, s1R, s2R, f1L, f2L, f1R, f2R, g1L, g2L, g1R, g2R; public readonly float[] cL = new float[4096], cR = new float[4096]; public int cw; public void Reset() { s1L = s2L = s1R = s2R = f1L = f2L = f1R = f2R = g1L = g2L = g1R = g2R = 0; Array.Clear(cL, 0, cL.Length); Array.Clear(cR, 0, cR.Length); cw = 0; } }
    readonly Flt A = new Flt(), B = new Flt(); double env, lfoPh, rnd = 0, gA, gB, kA, kB; long cnt; uint seed = 7;
    int tA, tB, route, shape, target; double fA, fB, rA, rB, dA, dB, morph, envAmt, attC, relC, lfoRate, lfoDepth, mixW, outG;
    public volatile float ShowA, ShowB, ShowMorph;
    public ModalFilterFx()
    {
        Type = "pxmodal"; Title = "Purphex Modal Filter"; Category = "Purphex VST"; Blurb = "Two morphable filters (seven types) moved by an envelope and an LFO";
        AddC("a_type", "A type", Types, 0); AddP("a_cut", "A cutoff", 20, 20000, 1200, "Hz"); AddP("a_res", "A resonance", 0, 100, 40, "%"); AddP("a_drive", "A drive", 0, 100, 0, "%");
        AddC("b_type", "B type", Types, 2); AddP("b_cut", "B cutoff", 20, 20000, 2500, "Hz"); AddP("b_res", "B resonance", 0, 100, 60, "%"); AddP("b_drive", "B drive", 0, 100, 0, "%");
        AddC("route", "Routing", Routes, 0); AddP("morph", "Morph", 0, 100, 0, "%");
        AddP("env", "Envelope amount", -100, 100, 0, "%"); AddP("att", "Envelope attack", 1, 200, 10, "ms"); AddP("rel", "Envelope release", 10, 2000, 200, "ms");
        AddP("lrate", "LFO rate", 0.05f, 20, 1, "Hz"); AddP("ldepth", "LFO depth", 0, 100, 0, "%"); AddC("lshape", "LFO shape", Shapes, 0); AddC("ltarget", "LFO target", Targets, 0);
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); A.Reset(); B.Reset(); env = 0; lfoPh = 0; cnt = 0; seed = 7; rnd = 0; }
    protected override void Update()
    {
        tA = (int)Get("a_type"); tB = (int)Get("b_type"); fA = Get("a_cut"); fB = Get("b_cut"); rA = Get("a_res") / 100.0; rB = Get("b_res") / 100.0; dA = Get("a_drive") / 100.0; dB = Get("b_drive") / 100.0;
        route = (int)Get("route"); morph = Get("morph") / 100.0; envAmt = Get("env") / 100.0; attC = 1 - Math.Exp(-1.0 / (Get("att") * 0.001 * SR)); relC = 1 - Math.Exp(-1.0 / (Get("rel") * 0.001 * SR));
        lfoRate = Get("lrate"); lfoDepth = Get("ldepth") / 100.0; shape = (int)Get("lshape"); target = (int)Get("ltarget"); mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    double Lfo() { double p = lfoPh; switch (shape) { case 1: return 1 - 4 * Math.Abs(p - 0.5); case 2: return p < 0.5 ? 1 : -1; case 3: return rnd; default: return Math.Sin(2 * Math.PI * p); } }
    // one filter on one sample of one side (the state variable filter gives low, band and high at once)
    double Run(Flt f, int type, double x, double fc, double g, double res, double drive, bool left)
    {
        if (drive > 0) x = Math.Tanh(x * (1 + drive * 8)) / (1 + drive * 2);
        double k = 2 - 1.95 * res;
        if (type == 5)
        {   // comb: a short delay of one period of the cutoff, fed back (rings at the cutoff and its harmonics)
            var buf = left ? f.cL : f.cR; double d = Math.Min(buf.Length - 2, Math.Max(2, SR / fc)); double rp = f.cw - d; if (rp < 0) rp += buf.Length; int i0 = (int)rp; double fr = rp - i0; double v = buf[i0] + (buf[(i0 + 1) % buf.Length] - buf[i0]) * fr; double y = x + v * res * 0.97; buf[f.cw] = (float)Math.Tanh(y); if (!left) f.cw = (f.cw + 1) % buf.Length; return y * (1 - res * 0.5);
        }
        if (type == 6)
        {   // formant: two band-passes at the first two vowel resonances, both moved by the cutoff
            double g1 = g, g2 = Math.Tan(Math.PI * Math.Min(SR * 0.45, fc * 2.6) / SR); double kk = 2 - 1.8 * res;
            double b1, b2; if (left) { b1 = Band(x, g1, kk, ref f.f1L, ref f.f2L); b2 = Band(x, g2, kk, ref f.g1L, ref f.g2L); } else { b1 = Band(x, g1, kk, ref f.f1R, ref f.f2R); b2 = Band(x, g2, kk, ref f.g1R, ref f.g2R); } return (b1 + b2 * 0.7) * kk;
        }
        double s1 = left ? f.s1L : f.s1R, s2 = left ? f.s2L : f.s2R; double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2;
        if (left) { f.s1L = s1; f.s2L = s2; } else { f.s1R = s1; f.s2R = s2; }
        switch (type) { case 1: return hp; case 2: return bp * k; case 3: return lp + hp; case 4: return x + bp * k * (1 + res * 3); default: return lp; }
    }
    static double Band(double x, double g, double k, ref double s1, ref double s2) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return bp; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double ca = fA, cb = fB, mo = morph;
        for (int i = 0; i < n; i++, cnt++)
        {
            double xl = l[i], xr = r[i], a = Math.Max(Math.Abs(xl), Math.Abs(xr)); env += (a - env) * (a > env ? attC : relC);
            lfoPh += lfoRate / SR; if (lfoPh >= 1) { lfoPh -= 1; seed = seed * 1664525 + 1013904223; rnd = (seed >> 8) / 8388608.0 - 1; }
            if ((cnt & 15) == 0)
            {   // the modulation is worked out every 16 samples (counted from the start, so the result never depends on the block size)
                double em = envAmt * Math.Min(1, env * 3) * 4, lm = lfoDepth * Lfo() * 3;
                ca = fA * Math.Pow(2, em + (target == 0 || target == 1 ? lm : 0)); cb = fB * Math.Pow(2, em + (target == 0 || target == 2 ? lm : 0)); mo = Math.Max(0, Math.Min(1, morph + (target == 3 ? lfoDepth * Lfo() * 0.5 : 0)));
                ca = Math.Max(20, Math.Min(SR * 0.45, ca)); cb = Math.Max(20, Math.Min(SR * 0.45, cb)); gA = Math.Tan(Math.PI * ca / SR); gB = Math.Tan(Math.PI * cb / SR); ShowA = (float)ca; ShowB = (float)cb; ShowMorph = (float)mo;
            }
            double yl, yr;
            if (route == 1) { yl = Run(B, tB, Run(A, tA, xl, ca, gA, rA, dA, true), cb, gB, rB, dB, true); yr = Run(B, tB, Run(A, tA, xr, ca, gA, rA, dA, false), cb, gB, rB, dB, false); }
            else { double al = Run(A, tA, xl, ca, gA, rA, dA, true), ar = Run(A, tA, xr, ca, gA, rA, dA, false), bl = Run(B, tB, xl, cb, gB, rB, dB, true), br = Run(B, tB, xr, cb, gB, rB, dB, false); if (route == 2) { yl = (al + bl) * 0.6; yr = (ar + br) * 0.6; } else { yl = al + (bl - al) * mo; yr = ar + (br - ar) * mo; } }
            l[i] = (float)((xl + (yl - xl) * mixW) * outG); r[i] = (float)((xr + (yr - xr) * mixW) * outG);
        }
    }
    // how loud a frequency comes out of one filter (for the picture)
    public static double Response(int type, double f, double fc, double res, int sr)
    {
        double w = f / fc, k = 2 - 1.95 * res;
        if (type == 5) { double ph = 2 * Math.PI * f / fc; double re = 1 - res * 0.97 * Math.Cos(ph), im = res * 0.97 * Math.Sin(ph); return (1 - res * 0.5) / Math.Sqrt(re * re + im * im); }
        if (type == 6) { Func<double, double> bp = fq => { double ww = f / fq, kk = 2 - 1.8 * res; return kk * ww / Math.Sqrt((1 - ww * ww) * (1 - ww * ww) + kk * kk * ww * ww); }; return (bp(fc) + bp(fc * 2.6) * 0.7) * (2 - 1.8 * res) / 2; }
        double den = Math.Sqrt((1 - w * w) * (1 - w * w) + k * k * w * w); switch (type) { case 1: return w * w / den; case 2: return k * w / den; case 3: return Math.Abs(1 - w * w) / den; case 4: return 1 + (k * w / den) * (1 + res * 3); default: return 1 / den; }
    }
}

#if !PURPHEX_CORE
class ModalFilterWindow : PxWindow
{
    readonly ModalFilterFx m;
    public ModalFilterWindow(ModalFilterFx f) : base(f, "Modal Filter", new Size(960, 680)) { m = f; Accent = Cyan; }
    protected override string SubTitle { get { return "two morphable filters"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the frequency picture: filter A, filter B and the result, live with the modulation
        var gr = new RectangleF(a.X, a.Y, a.Width, 220); Fill(g, Well, gr, 10); float x0 = gr.X + 10, w = gr.Width - 20, y0 = gr.Y + 10, h = gr.Height - 20;
        Func<double, float> yOf = db => y0 + h / 2 - (float)Math.Max(-1, Math.Min(1, db / 30)) * h / 2;
        using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255))) { g.DrawLine(pen, x0, yOf(0), x0 + w, yOf(0)); foreach (var hz in new[] { 100.0, 1000, 10000 }) { float x = x0 + (float)(Math.Log(hz / 20) / Math.Log(1000)) * w; g.DrawLine(pen, x, y0, x, y0 + h); } }
        int tA = (int)fx.Get("a_type"), tB = (int)fx.Get("b_type"), route = (int)fx.Get("route"); double ca = m.ShowA > 0 ? m.ShowA : fx.Get("a_cut"), cb = m.ShowB > 0 ? m.ShowB : fx.Get("b_cut"), ra = fx.Get("a_res") / 100.0, rb = fx.Get("b_res") / 100.0, mo = m.ShowMorph;
        var pa = new PointF[160]; var pb = new PointF[160]; var pr = new PointF[160];
        for (int i = 0; i < 160; i++)
        {
            double hz = 20 * Math.Pow(1000, i / 159.0); float x = x0 + i / 159f * w; double A = ModalFilterFx.Response(tA, hz, ca, ra, 44100), B = ModalFilterFx.Response(tB, hz, cb, rb, 44100);
            double R = route == 1 ? A * B : route == 2 ? (A + B) * 0.6 : A + (B - A) * mo; Func<double, float> y = v => yOf(20 * Math.Log10(Math.Max(1e-4, v)));
            pa[i] = new PointF(x, y(A)); pb[i] = new PointF(x, y(B)); pr[i] = new PointF(x, y(R));
        }
        using (var pen = new Pen(Color.FromArgb(120, Cyan), 1.5f)) g.DrawLines(pen, pa); using (var pen = new Pen(Color.FromArgb(120, Pink), 1.5f)) g.DrawLines(pen, pb); using (var pen = new Pen(Ink, 2.5f)) g.DrawLines(pen, pr);
        Txt(g, "A", 12, Cyan, new RectangleF(x0 + 6, y0 + 4, 20, 16), StringAlignment.Near, true); Txt(g, "B", 12, Pink, new RectangleF(x0 + 26, y0 + 4, 20, 16), StringAlignment.Near, true);
        // the two filters and the routing in the middle
        float py = gr.Bottom + 10, ph = 250, pw = (a.Width - 240) / 2;
        for (int k = 0; k < 2; k++)
        {
            string p = k == 0 ? "a_" : "b_"; Color c = k == 0 ? Cyan : Pink; var r = new RectangleF(k == 0 ? a.X : a.Right - pw, py, pw, ph); Panel(g, r, k == 0 ? "FILTER A" : "FILTER B", c);
            Choice(g, new RectangleF(r.X + 16, r.Y + 34, r.Width - 32, 24), p + "type", c, 10.5f);
            Knob(g, r.X + r.Width * 0.5f, r.Y + 120, 38, p + "cut", "CUTOFF", c, true); Knob(g, r.X + r.Width * 0.2f, r.Y + 200, 18, p + "res", "RES", c); Knob(g, r.X + r.Width * 0.8f, r.Y + 200, 18, p + "drive", "DRIVE", c);
        }
        var mid = new RectangleF(a.X + pw + 10, py, a.Width - pw * 2 - 20, ph); Panel(g, mid, "", Violet);
        Txt(g, "ROUTING", 10, Dim, new RectangleF(mid.X + 14, mid.Y + 12, 120, 14), StringAlignment.Near, true); Choice(g, new RectangleF(mid.X + 14, mid.Y + 30, mid.Width - 28, 24), "route", Violet, 10.5f);
        Knob(g, mid.X + mid.Width / 2, mid.Y + 130, 34, "morph", "MORPH A - B", Violet, true);
        // modulation and output
        var mr = new RectangleF(a.X, py + ph + 10, a.Width, a.Bottom - py - ph - 10); Panel(g, mr, "", Lime); float cy = mr.Y + mr.Height / 2 - 4;
        Txt(g, "ENVELOPE", 10, Dim, new RectangleF(mr.X + 16, mr.Y + 8, 120, 14), StringAlignment.Near, true);
        Knob(g, mr.X + 60, cy, 18, "env", "AMOUNT", Lime); Knob(g, mr.X + 140, cy, 18, "att", "ATTACK", Lime); Knob(g, mr.X + 220, cy, 18, "rel", "RELEASE", Lime);
        Txt(g, "LFO", 10, Dim, new RectangleF(mr.X + 300, mr.Y + 8, 120, 14), StringAlignment.Near, true);
        Knob(g, mr.X + 330, cy, 18, "lrate", "RATE", Gold); Knob(g, mr.X + 410, cy, 18, "ldepth", "DEPTH", Gold);
        Choice(g, new RectangleF(mr.X + 460, cy - 26, 130, 22), "lshape", Gold, 10); Choice(g, new RectangleF(mr.X + 460, cy + 4, 130, 22), "ltarget", Gold, 10);
        Knob(g, mr.Right - 160, cy, 20, "mix", "MIX", Ink); Knob(g, mr.Right - 70, cy, 20, "out", "OUTPUT", Ink);
    }
}
#endif
