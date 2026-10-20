using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX PHANTOM GATE: a gate for drums and vocals. It opens when the sound goes over THRESH and closes again only when it falls under
//  RETURN (a separate, lower line, so it does not chatter), down to FLOOR (how much it turns down when closed).
//  ATTACK, HOLD and RELEASE (in ms, or locked to a note value of the song), AHEAD looks into the sound so it is open before a hit lands
//  (the studio lines the track up), SPIKE lets the first crack of a hit through even below the line, INPUT sets the level it sees.
//  SIDE-CHAIN: it can listen to a filtered version (high-pass and low-pass, with LISTEN). CURVE: how the opening and closing are shaped.
//  DYNAMICS: a ratio below the line (a hard gate, or a softer expander). DE-BLEED: when closed, the spill is also made darker.
// ======================================================================
class PhantomGateFx : Fx
{
    public static readonly string[] Rel = { "ms", "1/4", "1/8", "1/16", "1/32" }; static readonly double[] RelBeats = { 0, 1, 0.5, 0.25, 0.125 };
    float[] dl = new float[48000], dr = new float[48000]; int dw;
    readonly Biquad scHp = new Biquad(), scLp = new Biquad(); double env, gGain, holdCnt, spkFast, spkSlow, bleedL, bleedR; bool open;
    public readonly float[] HistLvl = new float[360], HistGain = new float[360]; public volatile int HistW; int hc; float hL, hG = 1;
    public volatile float Gr, InPk;
    public PhantomGateFx()
    {
        Type = "pxpgate"; Title = "Purphex Phantom Gate"; Category = "Purphex VST"; Blurb = "Gate for drums and vocals: thresh and return lines, spike, look-ahead, side-chain, de-bleed";
        AddP("input", "Input", -24, 24, 0, "dB"); AddP("spike", "Spike", 0, 100, 0, "%"); AddP("thr", "Thresh", -72, 0, -30, "dB"); AddP("ret", "Return", 0, 24, 6, "dB"); AddP("floor", "Floor", -80, 0, -60, "dB");
        AddP("att", "Attack", 0, 50, 0.5f, "ms"); AddP("hold", "Hold", 0, 500, 30, "ms"); AddP("rel", "Release", 2, 2000, 80, "ms"); AddC("relsync", "Release time", Rel, 0); AddP("ahead", "Ahead", 0, 10, 3, "ms");
        AddC("sc_on", "Side-chain", new[] { "Off", "On" }, 0); AddP("sc_hp", "Side-chain high-pass", 20, 5000, 80, "Hz"); AddP("sc_lp", "Side-chain low-pass", 200, 20000, 12000, "Hz"); AddC("sc_listen", "Listen", new[] { "Off", "On" }, 0);
        AddC("curve", "Curve", new[] { "Linear", "Smooth", "Snappy" }, 1); AddP("ratio", "Ratio below the line", 1.5f, 20, 20, ":1");
        AddC("db_on", "De-bleed", new[] { "Off", "On" }, 0); AddP("db_f", "De-bleed darkness", 500, 12000, 3000, "Hz");
    }
    public override int Latency { get { return (int)(Get("ahead") * 0.001 * SR); } }
    public override void Reset() { base.Reset(); Array.Clear(dl, 0, dl.Length); Array.Clear(dr, 0, dr.Length); dw = 0; env = 0; gGain = 0; holdCnt = 0; spkFast = spkSlow = 0; bleedL = bleedR = 0; open = false; scHp.Reset(); scLp.Reset(); }
    protected override void Update() { scHp.Set(Biquad.HighPass, SR, Get("sc_hp"), 0.707, 0); scLp.Set(Biquad.LowPass, SR, Math.Min(Get("sc_lp"), SR * 0.45), 0.707, 0); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double inG = Math.Pow(10, Get("input") / 20), thr = Get("thr"), retDb = thr - Get("ret"), floorG = Math.Pow(10, Get("floor") / 20), spike = Get("spike") / 100.0, ratio = Get("ratio");
        int rs = (int)Get("relsync"); double relMs = rs == 0 ? Get("rel") : 60000.0 / Math.Max(20, Fx.Bpm) * RelBeats[rs];
        double aC = 1 - Math.Exp(-1.0 / (Math.Max(0.02, Get("att")) * 0.001 * SR)), rC = 1 - Math.Exp(-1.0 / (relMs * 0.001 * SR)), holdS = Get("hold") * 0.001 * SR;
        int curve = (int)Get("curve"); bool sc = Get("sc_on") >= 0.5f, listen = sc && Get("sc_listen") >= 0.5f, deb = Get("db_on") >= 0.5f; double dbA = 1 - Math.Exp(-2 * Math.PI * Math.Min(Get("db_f"), SR * 0.45) / SR);
        int ah = Latency, len = dl.Length; double ea = 1 - Math.Exp(-1.0 / (0.0005 * SR)), er = 1 - Math.Exp(-1.0 / (0.03 * SR)), sfA = 1 - Math.Exp(-1.0 / (0.001 * SR)), ssA = 1 - Math.Exp(-1.0 / (0.04 * SR));
        float inPk = 0; double grMax = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i] * inG, xr = r[i] * inG; inPk = (float)Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            double dlv = xl, drv = xr; if (sc) { dlv = scLp.TickL(scHp.TickL((float)xl)); drv = scLp.TickR(scHp.TickR((float)xr)); }
            double a = Math.Max(Math.Abs(dlv), Math.Abs(drv)); env += (a - env) * (a > env ? ea : er); double lev = 20 * Math.Log10(env + 1e-9);
            spkFast += (a - spkFast) * sfA; spkSlow += (a - spkSlow) * ssA; double spk = Math.Max(0, spkFast - spkSlow * 1.5) / (spkSlow + 1e-5);
            // open over THRESH, closed under RETURN (between the two it stays as it was), held for HOLD
            if (lev >= thr) { open = true; holdCnt = holdS; } else if (lev < retDb) { if (holdCnt > 0) holdCnt--; else open = false; }
            double closedG = ratio >= 19.9 || lev >= retDb ? floorG : Math.Max(floorG, Math.Pow(10, (lev - retDb) * (ratio - 1) / 20));   // a hard gate goes to FLOOR; a lower ratio is a softer expander
            double target = open ? 1 : closedG; target = Math.Max(target, Math.Min(1, spike * spk));
            gGain += (target - gGain) * (target > gGain ? aC : rC);
            double u = Math.Max(0, Math.Min(1, (gGain - floorG) / Math.Max(1e-6, 1 - floorG)));   // how open, 0..1
            double us = curve == 0 ? u : curve == 1 ? u * u * (3 - 2 * u) : Math.Sqrt(u), gNow = floorG + (1 - floorG) * us;
            // the sound itself comes from the delay line (AHEAD), so the gate is already open when a hit lands
            dl[dw] = (float)xl; dr[dw] = (float)xr; int rd = (dw - ah + len) % len; dw = (dw + 1) % len; double ol = dl[rd], or = dr[rd];
            if (listen) { ol = dlv; or = drv; gNow = 1; }
            else
            {
                if (deb) { bleedL += (ol - bleedL) * dbA; bleedR += (or - bleedR) * dbA; ol = ol * us + bleedL * (1 - us); or = or * us + bleedR * (1 - us); }   // closed: the spill is also darker
                ol *= gNow; or *= gNow;
            }
            if (double.IsNaN(ol) || double.IsNaN(or)) { Reset(); ol = 0; or = 0; }
            l[i] = (float)ol; r[i] = (float)or; grMax = Math.Max(grMax, -20 * Math.Log10(Math.Max(1e-5, gNow)));
            hL = Math.Max(hL, (float)a); hG = Math.Min(hG, (float)gNow); if (++hc >= SR / 90) { hc = 0; int w = HistW; HistLvl[w] = hL; HistGain[w] = hG; HistW = (w + 1) % HistLvl.Length; hL = 0; hG = 1; }
        }
        Gr = (float)grMax; InPk = Math.Max(InPk * 0.85f, inPk);
    }
}

#if !PURPHEX_CORE
class PhantomGateWindow : PxWindow
{
    readonly PhantomGateFx gt;
    public PhantomGateWindow(PhantomGateFx f) : base(f, "Phantom Gate", new Size(960, 640)) { gt = f; Accent = Pink; }
    protected override string SubTitle { get { return "gate for drums and vocals"; } }
    float Y(RectangleF r, double db) { return r.Y + (float)(Math.Max(-72, Math.Min(0, db)) / -72.0 * r.Height); }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the scrolling sound, with the THRESH line (pink), the RETURN line (gold) and the gate opening (filled)
        var disp = new RectangleF(a.X, a.Y, a.Width, 250); Panel(g, disp, "", Pink); var wv = new RectangleF(disp.X + 50, disp.Y + 10, disp.Width - 64, disp.Height - 20); Fill(g, Color.FromArgb(18, 6, 30), wv, 8);
        Meter(g, new RectangleF(disp.X + 12, disp.Y + 12, 12, disp.Height - 40), gt.InPk, "IN"); Meter(g, new RectangleF(disp.X + 30, disp.Y + 12, 12, disp.Height - 40), gt.Gr, "GR", true);
        int n = gt.HistLvl.Length, w = gt.HistW; var pts = new PointF[n]; var fillPts = new PointF[n + 2]; fillPts[0] = new PointF(wv.X, wv.Bottom);
        for (int i = 0; i < n; i++) { float x = wv.X + i * wv.Width / (n - 1); double db = 20 * Math.Log10(gt.HistLvl[(w + i) % n] + 1e-9); pts[i] = new PointF(x, Y(wv, db)); double gdb = 20 * Math.Log10(Math.Max(1e-5, gt.HistGain[(w + i) % n]) * Math.Max(1e-5, gt.HistLvl[(w + i) % n])); fillPts[i + 1] = new PointF(x, Y(wv, gdb)); }
        fillPts[n + 1] = new PointF(wv.Right, wv.Bottom); using (var b = new SolidBrush(Color.FromArgb(110, 120, 110, 200))) g.FillPolygon(b, fillPts); using (var pen = new Pen(Color.FromArgb(220, 210, 215, 255), 1.6f)) g.DrawLines(pen, pts);
        float ty = Y(wv, fx.Get("thr")), ry = Y(wv, fx.Get("thr") - fx.Get("ret")); using (var pen = new Pen(Pink, 2)) g.DrawLine(pen, wv.X, ty, wv.Right, ty); using (var pen = new Pen(Gold, 2) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, wv.X, ry, wv.Right, ry);
        foreach (var db in new[] { 0, -36, -72 }) Txt(g, db.ToString(), 9.5f, Dim, new RectangleF(wv.X + 4, Y(wv, db) - (db == 0 ? 0 : db == -72 ? 14 : 7), 30, 13), StringAlignment.Near, false);
        var tp = fx.Find("thr"); var th = Reg(new RectangleF(wv.X, ty - 6, wv.Width, 12), null, "Thresh: drag the pink line"); th.P = tp; th.DragAt = pt => fx.Set("thr", (float)(-(pt.Y - wv.Y) / wv.Height * 72));
        // the main controls, in two rows like a mixing desk
        var main = new RectangleF(a.X, disp.Bottom + 10, a.Width - 260, a.Bottom - disp.Bottom - 10); Panel(g, main, "", Violet);
        float c1 = main.Width / 5; string[] r1 = { "input", "spike", "floor", "att", "hold" }, l1 = { "INPUT", "SPIKE", "FLOOR", "ATTACK", "HOLD" }; string[] r2 = { "thr", "ret", "rel", "ahead", "ratio" }, l2 = { "THRESH", "RETURN", "RELEASE", "AHEAD", "RATIO" };
        for (int i = 0; i < 5; i++) { Knob(g, main.X + c1 * i + c1 / 2, main.Y + 50, 24, r1[i], l1[i], Violet); Knob(g, main.X + c1 * i + c1 / 2, main.Y + 160, 24, r2[i], l2[i], i < 2 ? Pink : Violet); }
        Choice(g, new RectangleF(main.X + c1 * 2 + c1 / 2 - 34, main.Y + 210, 68, 22), "relsync", Violet, 10);
        // sections
        var sec = new RectangleF(main.Right + 10, main.Y, 250, main.Height); Panel(g, sec, "", Cyan);
        bool so = fx.Get("sc_on") >= 0.5f; Txt(g, "SIDE-CHAIN", 11, so ? Ink : Dim, new RectangleF(sec.X + 12, sec.Y + 8, 120, 16), StringAlignment.Near, true); Toggle(g, new RectangleF(sec.Right - 60, sec.Y + 6, 50, 20), "sc_on", Cyan);
        Knob(g, sec.X + 50, sec.Y + 54, 17, "sc_hp", "HIGH-PASS", Cyan); Knob(g, sec.X + 125, sec.Y + 54, 17, "sc_lp", "LOW-PASS", Cyan); Toggle(g, new RectangleF(sec.X + 170, sec.Y + 42, 70, 22), "sc_listen", Gold, "LISTENING", "LISTEN");
        Txt(g, "CURVE", 11, Ink, new RectangleF(sec.X + 12, sec.Y + 104, 120, 16), StringAlignment.Near, true); Segments(g, new RectangleF(sec.X + 12, sec.Y + 124, sec.Width - 24, 22), "curve", Cyan, 9.5f);
        bool dbOn = fx.Get("db_on") >= 0.5f; Txt(g, "DE-BLEED", 11, dbOn ? Ink : Dim, new RectangleF(sec.X + 12, sec.Y + 160, 120, 16), StringAlignment.Near, true); Toggle(g, new RectangleF(sec.Right - 60, sec.Y + 158, 50, 20), "db_on", Cyan);
        Knob(g, sec.X + 125, sec.Y + 205, 17, "db_f", "DARKNESS", Cyan);
    }
}
#endif