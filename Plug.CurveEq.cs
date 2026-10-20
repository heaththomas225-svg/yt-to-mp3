using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX CURVE EQ: an 8-band EQ drawn over a live spectrum (before and after). Drag a node to move its band, the wheel on a node
//  sets its width, double-click an empty spot to add a band there, right-click a node for its type or to remove it.
//  Every band can also be DYNAMIC: it only moves when that part of the sound goes over its threshold (great for harsh S sounds or a boomy note).
//  AUTO GAIN keeps the level about the same, SCALE sets how much of the curve the view shows, MIX blends with the dry sound.
// ======================================================================
class CurveEqFx : Fx
{
    public const int N = 8;
    public static readonly string[] Types = { "Bell", "Low shelf", "High shelf", "Low cut 12", "Low cut 24", "High cut 12", "High cut 24", "Notch" };
    static readonly float[] DefF = { 40, 120, 300, 800, 2000, 4500, 9000, 15000 };
    readonly Biquad[] f1 = new Biquad[N], f2 = new Biquad[N], det = new Biquad[N]; readonly double[] env = new double[N], dynG = new double[N];
    public readonly double[] LiveGain = new double[N];   // the gain each band has right now (dynamic bands move): the window draws it
    public readonly PxScope Pre = new PxScope(8192), Post = new PxScope(8192); public volatile float InPk, OutPk;
    double autoG = 1, mixS = 1, outG = 1; int cnt; readonly bool[] cOn = new bool[N], cTwice = new bool[N]; readonly double[] cDyn = new double[N], cThr = new double[N], cGain = new double[N];   // the settings, read once per change (no work by name on the audio thread)
    public CurveEqFx()
    {
        Type = "pxcurve"; Title = "Purphex Curve EQ"; Category = "Purphex VST"; Blurb = "8-band dynamic EQ with draggable nodes on a live spectrum";
        for (int k = 0; k < N; k++)
        {
            int t = k == 0 ? 3 : k == N - 1 ? 2 : 0;
            AddC("b" + k + "_on", "Band " + (k + 1) + " on", new[] { "Off", "On" }, 0); AddC("b" + k + "_t", "Band " + (k + 1) + " type", Types, t);
            AddP("b" + k + "_f", "Band " + (k + 1) + " frequency", 20, 20000, DefF[k], "Hz"); AddP("b" + k + "_g", "Band " + (k + 1) + " gain", -30, 30, 0, "dB"); AddP("b" + k + "_q", "Band " + (k + 1) + " Q", 0.1f, 18, 1, "");
            AddP("b" + k + "_dyn", "Band " + (k + 1) + " dynamic range", -24, 24, 0, "dB"); AddP("b" + k + "_thr", "Band " + (k + 1) + " threshold", -60, 0, -24, "dB");
            f1[k] = new Biquad(); f2[k] = new Biquad(); det[k] = new Biquad();
        }
        AddC("pre", "Spectrum before", new[] { "Off", "On" }, 1); AddC("post", "Spectrum after", new[] { "Off", "On" }, 1); AddC("auto", "Auto gain", new[] { "Off", "On" }, 0);
        AddC("scale", "Scale", new[] { "6 dB", "12 dB", "18 dB", "30 dB" }, 1); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 24, 0, "dB");
    }
    public override void Reset() { base.Reset(); for (int k = 0; k < N; k++) { f1[k].Reset(); f2[k].Reset(); det[k].Reset(); env[k] = 0; dynG[k] = 0; } cnt = 0; }
    public bool On(int k) { return Get("b" + k + "_on") >= 0.5f; }
    public int TypeOf(int k) { return (int)Get("b" + k + "_t"); }
    // the biquad type and gain for a band (cuts have no gain)
    static int BqType(int t) { switch (t) { case 1: return Biquad.LowShelf; case 2: return Biquad.HighShelf; case 3: case 4: return Biquad.HighPass; case 5: case 6: return Biquad.LowPass; case 7: return Biquad.Notch; default: return Biquad.Bell; } }
    public static bool HasGain(int t) { return t <= 2; }
    public double[] Coeffs(int k, double gain, double fs) { int t = TypeOf(k); return Biquad.Coeffs(BqType(t), fs, Get("b" + k + "_f"), t >= 3 && t <= 6 ? 0.707 * Math.Max(0.5, Get("b" + k + "_q")) : Get("b" + k + "_q"), HasGain(t) ? gain : 0); }
    public double ResponseDb(double f, double fs, bool live)
    {
        double db = 0;
        for (int k = 0; k < N; k++) { if (!On(k)) continue; int t = TypeOf(k); var c = Coeffs(k, live ? LiveGain[k] : Get("b" + k + "_g"), fs); double m = Biquad.MagDb(c, f, fs); db += (t == 4 || t == 6) ? 2 * m : m; }
        return db;
    }
    // how far the static curve lifts or cuts on average over the musical range (auto gain undoes that)
    double AverageDb() { double s = 0; int c = 0; for (double f = 60; f < 12000; f *= 1.12) { s += ResponseDb(f, SR, false); c++; } return s / Math.Max(1, c); }
    protected override void Update()
    {
        for (int k = 0; k < N; k++) { cOn[k] = On(k); int tk = TypeOf(k); cTwice[k] = tk == 4 || tk == 6; cDyn[k] = Get("b" + k + "_dyn"); cThr[k] = Get("b" + k + "_thr"); cGain[k] = Get("b" + k + "_g"); if (cDyn[k] == 0) dynG[k] = 0; det[k].Set(Biquad.BandPass, SR, Math.Min(Get("b" + k + "_f"), SR * 0.45), Math.Max(0.5, Get("b" + k + "_q")), 0); SetBand(k, Get("b" + k + "_g") + dynG[k]); }
        autoG = Get("auto") >= 0.5f ? Math.Pow(10, -AverageDb() / 20) : 1;
    }
    void SetBand(int k, double gain) { var c = Coeffs(k, gain, SR); f1[k].SetRaw(c[0], c[1], c[2], c[3], c[4]); f2[k].SetRaw(c[0], c[1], c[2], c[3], c[4]); LiveGain[k] = gain; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double mixT = Get("mix") / 100.0, outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR)), ea = 1 - Math.Exp(-1.0 / (0.005 * SR)), er = 1 - Math.Exp(-1.0 / (0.08 * SR));
        float inPk = 0, outPk = 0;
        for (int i = 0; i < n; i++)
        {
            float xl = l[i], xr = r[i]; inPk = Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr))); Pre.Push((xl + xr) * 0.5f);
            // dynamic bands: follow the level of their part of the sound and update every 32 samples
            for (int k = 0; k < N; k++)
            {
                if (!cOn[k]) continue; double dyn = cDyn[k]; if (dyn == 0) continue;
                double d = Math.Abs(det[k].TickL((xl + xr) * 0.5f)); env[k] += (d - env[k]) * (d > env[k] ? ea : er);
                if ((cnt & 31) == 0)
                {
                    double lev = 20 * Math.Log10(env[k] + 1e-9), over = Math.Max(0, Math.Min(1, (lev - cThr[k]) / 12)), tgt = dyn * over;
                    dynG[k] += (tgt - dynG[k]) * 0.35; SetBand(k, cGain[k] + dynG[k]);
                }
            }
            cnt++;
            float yl = xl, yr = xr;
            for (int k = 0; k < N; k++) { if (!cOn[k]) continue; yl = f1[k].TickL(yl); yr = f1[k].TickR(yr); if (cTwice[k]) { yl = f2[k].TickL(yl); yr = f2[k].TickR(yr); } }
            mixS += (mixT - mixS) * gs; outG += (outT - outG) * gs;
            double ol = (xl + (yl * autoG - xl) * mixS) * outG, or = (xr + (yr * autoG - xr) * mixS) * outG;
            if (double.IsNaN(ol) || double.IsInfinity(ol) || double.IsNaN(or) || double.IsInfinity(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or; outPk = (float)Math.Max(outPk, Math.Max(Math.Abs(ol), Math.Abs(or))); Post.Push((float)((ol + or) * 0.5));
        }
        InPk = Math.Max(InPk * 0.85f, inPk); OutPk = Math.Max(OutPk * 0.85f, outPk);
    }
}

#if !PURPHEX_CORE
class CurveEqWindow : PxWindow
{
    readonly CurveEqFx eq; RectangleF graph; static readonly float[] ScaleDb = { 6, 12, 18, 30 };
    static readonly Color[] Col = { Color.FromArgb(255, 90, 90), Color.FromArgb(255, 150, 70), Color.FromArgb(255, 210, 70), Color.FromArgb(140, 230, 90), Color.FromArgb(60, 210, 200), Color.FromArgb(70, 150, 255), Color.FromArgb(160, 110, 255), Color.FromArgb(240, 100, 220) };
    int sel = -1;
    public CurveEqWindow(CurveEqFx f) : base(f, "Curve EQ", new Size(1040, 640)) { eq = f; Accent = Orange; }
    protected override string SubTitle { get { return "dynamic EQ on a live spectrum"; } }
    float X(double f) { return graph.X + (float)(Math.Log(f / 20) / Math.Log(1000) * graph.Width); }
    double F(float x) { return 20 * Math.Pow(1000, Math.Max(0, Math.Min(1, (x - graph.X) / graph.Width))); }
    float Y(double db) { float s = ScaleDb[(int)fx.Get("scale")]; return graph.Y + graph.Height / 2 - (float)(db / s * graph.Height / 2); }
    double Db(float y) { float s = ScaleDb[(int)fx.Get("scale")]; return (graph.Y + graph.Height / 2 - y) / (graph.Height / 2) * s; }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        graph = new RectangleF(a.X + 10, a.Y + 6, a.Width - 20, a.Height - 130); Fill(g, Color.FromArgb(14, 9, 28), new RectangleF(graph.X - 10, graph.Y - 6, graph.Width + 20, graph.Height + 12), 14);
        float s = ScaleDb[(int)fx.Get("scale")];
        using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255)))
        {
            foreach (var hz in new[] { 50, 100, 200, 500, 1000, 2000, 5000, 10000 }) { float x = X(hz); g.DrawLine(pen, x, graph.Y, x, graph.Bottom); Txt(g, hz >= 1000 ? (hz / 1000) + "k" : hz.ToString(), 9.5f, Dim, new RectangleF(x - 20, graph.Bottom - 14, 40, 12), StringAlignment.Center, false); }
            foreach (var db in new[] { -s, -s / 2, s / 2, s }) { float y = Y(db); g.DrawLine(pen, graph.X, y, graph.Right, y); Txt(g, (db > 0 ? "+" : "") + db.ToString("0"), 9.5f, Dim, new RectangleF(graph.X + 2, y - 7, 30, 12), StringAlignment.Near, false); }
        }
        using (var pen = new Pen(Color.FromArgb(80, Lime), 1)) g.DrawLine(pen, graph.X, Y(0), graph.Right, Y(0));
        // spectra
        int nfft = 4096; int sr = eq.SR;
        if (fx.Get("pre") >= 0.5f) DrawSpectrum(g, eq.Pre.Spectrum(nfft), nfft, sr, Color.FromArgb(60, 120, 120, 200), false);
        if (fx.Get("post") >= 0.5f) DrawSpectrum(g, eq.Post.Spectrum(nfft), nfft, sr, Color.FromArgb(150, 110, 160, 255), true);
        // each band's own shape (faint) and the total curve (static, and live for dynamic bands)
        for (int k = 0; k < CurveEqFx.N; k++)
        {
            if (!eq.On(k)) continue; var pts = new List<PointF>(); var c = eq.Coeffs(k, fx.Get("b" + k + "_g"), sr); int t = eq.TypeOf(k);
            for (float x = graph.X; x <= graph.Right; x += 3) { double m = Biquad.MagDb(c, F(x), sr); if (t == 4 || t == 6) m *= 2; pts.Add(new PointF(x, Math.Max(graph.Y, Math.Min(graph.Bottom, Y(m))))); }
            using (var pen = new Pen(Color.FromArgb(k == sel ? 160 : 70, Col[k]), 1.2f)) g.DrawLines(pen, pts.ToArray());
        }
        DrawCurve(g, false, Color.FromArgb(255, 255, 110, 90), 2.4f); DrawCurve(g, true, Color.FromArgb(200, 255, 230, 120), 1.2f);
        // nodes
        for (int k = 0; k < CurveEqFx.N; k++)
        {
            if (!eq.On(k)) continue; int t = eq.TypeOf(k); float x = X(fx.Get("b" + k + "_f")), y = CurveEqFx.HasGain(t) ? Y(fx.Get("b" + k + "_g")) : Y(0);
            var nr = new RectangleF(x - 9, y - 9, 18, 18); bool hot = Hot(nr) || sel == k;
            using (var b = new SolidBrush(Color.FromArgb(hot ? 255 : 220, Col[k]))) g.FillEllipse(b, nr); if (hot) using (var pen = new Pen(Color.White, 2)) g.DrawEllipse(pen, nr);
            Txt(g, (k + 1).ToString(), 10, Color.Black, nr, StringAlignment.Center, true);
            if (fx.Get("b" + k + "_dyn") != 0) { float ly = Y(eq.LiveGain[k]); using (var pen = new Pen(Color.FromArgb(200, Col[k]), 2) { DashStyle = DashStyle.Dot }) g.DrawLine(pen, x, y, x, ly); }
            int kk = k; var h = Reg(new RectangleF(x - 12, y - 12, 24, 24), () => sel = kk, "Band " + (k + 1) + ": drag to move, wheel = width, right-click = type / remove");
            h.DragAt = pt => { fx.Set("b" + kk + "_f", (float)F(pt.X)); if (CurveEqFx.HasGain(eq.TypeOf(kk))) fx.Set("b" + kk + "_g", (float)Db(pt.Y)); };
            h.Wheel = d => { Mark(); fx.Set("b" + kk + "_q", (float)(fx.Get("b" + kk + "_q") * (d > 0 ? 1.15 : 1 / 1.15))); };
            h.Right = () => BandMenu(kk); h.Double = () => { Mark(); fx.Set("b" + kk + "_g", 0); };
        }
        // double-click on an empty spot adds a band there
        var gh = Reg(graph, () => sel = -1, "Double-click to add a band here; drag a node to move it"); hits.Remove(gh); hits.Insert(0, gh);
        gh.Double = () => { for (int k = 0; k < CurveEqFx.N; k++) if (!eq.On(k)) { Mark(); fx.Set("b" + k + "_on", 1); fx.Set("b" + k + "_t", 0); fx.Set("b" + k + "_f", (float)F(mouse.X)); fx.Set("b" + k + "_g", (float)Db(mouse.Y)); fx.Set("b" + k + "_q", 1); sel = k; return; } };
        // bottom bar: the selected band's settings, then the global ones
        var bar = new RectangleF(a.X, graph.Bottom + 14, a.Width, a.Bottom - graph.Bottom - 14); Panel(g, bar, "", Orange);
        if (sel >= 0 && eq.On(sel))
        {
            string k = "b" + sel; Txt(g, "BAND " + (sel + 1), 12, Col[sel], new RectangleF(bar.X + 16, bar.Y + 8, 80, 18), StringAlignment.Near, true);
            Choice(g, new RectangleF(bar.X + 16, bar.Y + 32, 110, 26), k + "_t", Col[sel]);
            Knob(g, bar.X + 170, bar.Y + 44, 20, k + "_f", "FREQ", Col[sel]); Knob(g, bar.X + 240, bar.Y + 44, 20, k + "_g", "GAIN", Col[sel]); Knob(g, bar.X + 310, bar.Y + 44, 20, k + "_q", "Q", Col[sel]);
            Knob(g, bar.X + 390, bar.Y + 44, 20, k + "_dyn", "DYNAMIC", Cyan); Knob(g, bar.X + 460, bar.Y + 44, 20, k + "_thr", "THRESHOLD", Cyan);
            Button(g, new RectangleF(bar.X + 510, bar.Y + 32, 80, 26), "REMOVE", false, Pink, () => { Mark(); fx.Set("b" + sel + "_on", 0); sel = -1; }, "Turn this band off", 10.5f);
        }
        else Txt(g, "Double-click the graph to add a band, or click a node to edit it.", 12, Dim, new RectangleF(bar.X + 16, bar.Y + 10, 560, 80), StringAlignment.Near, false);
        float gx = bar.Right - 420;
        Toggle(g, new RectangleF(gx, bar.Y + 12, 64, 24), "pre", Violet, "PRE", "PRE"); Toggle(g, new RectangleF(gx + 70, bar.Y + 12, 64, 24), "post", Violet, "POST", "POST"); Toggle(g, new RectangleF(gx + 140, bar.Y + 12, 100, 24), "auto", Cyan, "AUTO GAIN", "AUTO GAIN");
        Txt(g, "SCALE", 9.5f, Dim, new RectangleF(gx, bar.Y + 44, 60, 14), StringAlignment.Near, true); Segments(g, new RectangleF(gx, bar.Y + 60, 240, 24), "scale", Orange, 10);
        Knob(g, bar.Right - 140, bar.Y + 44, 22, "mix", "MIX", Orange); Knob(g, bar.Right - 75, bar.Y + 44, 22, "out", "OUT", Orange);
        Meter(g, new RectangleF(bar.Right - 30, bar.Y + 12, 6, bar.Height - 36), eq.InPk, ""); Meter(g, new RectangleF(bar.Right - 20, bar.Y + 12, 6, bar.Height - 36), eq.OutPk, "");
    }
    void DrawSpectrum(Graphics g, double[] db, int n, int sr, Color col, bool line)
    {
        var pts = new List<PointF>(); pts.Add(new PointF(graph.X, graph.Bottom));
        for (float x = graph.X; x <= graph.Right; x += 2) { double f = F(x); int bin = (int)(f * n / sr); if (bin < 1 || bin >= db.Length) continue; double v = Math.Max(db[bin], db[Math.Min(db.Length - 1, bin + 1)]); float y = graph.Y + (float)((-v) / 90.0 * graph.Height); pts.Add(new PointF(x, Math.Max(graph.Y, Math.Min(graph.Bottom, y)))); }
        pts.Add(new PointF(graph.Right, graph.Bottom)); if (pts.Count < 3) return;
        if (line) using (var pen = new Pen(col, 1.3f)) g.DrawLines(pen, pts.GetRange(1, pts.Count - 2).ToArray()); else using (var b = new SolidBrush(col)) g.FillPolygon(b, pts.ToArray());
    }
    void DrawCurve(Graphics g, bool live, Color col, float w)
    {
        var pts = new List<PointF>(); for (float x = graph.X; x <= graph.Right; x += 2) pts.Add(new PointF(x, Math.Max(graph.Y, Math.Min(graph.Bottom, Y(eq.ResponseDb(F(x), eq.SR, live))))));
        using (var pen = new Pen(col, w)) g.DrawLines(pen, pts.ToArray());
    }
    void BandMenu(int k)
    {
        var m = new ContextMenuStrip(); for (int t = 0; t < CurveEqFx.Types.Length; t++) { int tt = t; var it = new ToolStripMenuItem(CurveEqFx.Types[t]) { Checked = eq.TypeOf(k) == t }; it.Click += (a, b) => { Mark(); fx.Set("b" + k + "_t", tt); }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Remove this band").Click += (a, b) => { Mark(); fx.Set("b" + k + "_on", 0); if (sel == k) sel = -1; };
        m.Show(this, mouse);
    }
}
#endif
