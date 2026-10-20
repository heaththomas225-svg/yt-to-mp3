using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX BREATHOFF: finds the breaths in a vocal and turns them down, by itself.
//  Every 5 ms it measures how loud the sound is (ENERGY) and how breath-like it is (BREATH: hiss with little low end and no clear note).
//  A part that is quieter than the GATE line and more breath-like than the SENSITIVITY line is a breath: it is turned down by REDUCTION,
//  with FADE OUT / FADE IN so the edits are smooth. It looks 10 ms ahead (the studio lines the track up for that), so a breath is caught
//  from its very start. MONITOR lets you hear only the voice or only the breaths it found; ROOM TONE puts quiet noise in the gaps.
// ======================================================================
class BreathOffFx : Fx
{
    const int Look = 441 * 2;   // 10 ms at 44.1 kHz (scaled in Prepare)
    float[] dl = new float[96000], dr = new float[96000]; int dw, look = Look;
    double lpS, hpS, hpX, eLow, eHigh, eAll, zc, prevX; int frameN, frameLen = 220, zcCount; double score, energyDb = -90, gain = 1, rnd = 1;
    double roomLp; uint seed = 99;
    public readonly float[] HistBreath = new float[300], HistEnergy = new float[300], HistGain = new float[300]; public volatile int HistW;
    public volatile float Reduction;
    public BreathOffFx()
    {
        Type = "pxbreath"; Title = "Purphex BreathOff"; Category = "Purphex VST"; Blurb = "Finds the breaths in a vocal and turns them down, smoothly";
        AddP("sens", "Sensitivity", 0, 100, 35, ""); AddP("gate", "Gate", -64, 0, -30, "dB"); AddP("red", "Reduction", -60, 0, -60, "dB");
        AddP("fout", "Fade out", 1, 50, 5, "ms"); AddP("fin", "Fade in", 1, 50, 5, "ms");
        AddC("mon", "Monitor", new[] { "Voice", "Breath" }, 0); AddC("room", "Room tone", new[] { "Off", "On" }, 0); AddP("roomlvl", "Room tone level", -80, -40, -62, "dB");
    }
    public override int Latency { get { return look; } }
    public override void Reset() { base.Reset(); Array.Clear(dl, 0, dl.Length); Array.Clear(dr, 0, dr.Length); dw = 0; lpS = hpS = hpX = eLow = eHigh = eAll = zc = prevX = 0; frameN = 0; zcCount = 0; score = 0; energyDb = -90; gain = 1; roomLp = 0; }
    protected override void Update() { look = Math.Max(1, (int)(0.010 * SR)); frameLen = Math.Max(32, (int)(0.005 * SR)); }
    double Noise() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 8388608.0 - 1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double sens = Get("sens"), gateDb = Get("gate"), redG = Get("red") <= -59.9f ? 0 : Math.Pow(10, Get("red") / 20);
        double fo = 1 - Math.Exp(-1.0 / (Get("fout") * 0.001 * SR)), fi = 1 - Math.Exp(-1.0 / (Get("fin") * 0.001 * SR));
        bool monBreath = Get("mon") >= 0.5f, room = Get("room") >= 0.5f; double roomG = Math.Pow(10, Get("roomlvl") / 20);
        double lpA = 1 - Math.Exp(-2 * Math.PI * 900.0 / SR), hpA = Math.Exp(-2 * Math.PI * 2500.0 / SR), ea = 1 - Math.Exp(-1.0 / (0.012 * SR));
        int len = dl.Length; double redMin = 1;
        for (int i = 0; i < n; i++)
        {
            double x = (l[i] + r[i]) * 0.5;
            // analysis on the incoming (ahead) sound
            lpS += (x - lpS) * lpA; hpS = hpA * (hpS + x - hpX); hpX = x;
            eLow += (lpS * lpS - eLow) * ea; eHigh += (hpS * hpS - eHigh) * ea; eAll += (x * x - eAll) * ea;
            if ((x > 0) != (prevX > 0)) zcCount++; prevX = x;
            if (++frameN >= frameLen)
            {
                frameN = 0; double zcr = zcCount / (double)frameLen; zcCount = 0;
                energyDb = 10 * Math.Log10(eAll + 1e-12);
                double tilt = 10 * Math.Log10((eHigh + 1e-12) / (eLow + 1e-12));   // breaths have more hiss than body
                score = Math.Max(0, Math.Min(100, 50 + tilt * 2.2 + (zcr - 0.12) * 120));
                int w = HistW; HistBreath[w] = (float)score; HistEnergy[w] = (float)energyDb; HistGain[w] = (float)gain; HistW = (w + 1) % HistBreath.Length;
            }
            bool breath = energyDb < gateDb && energyDb > -75 && score > sens;
            double target = breath ? redG : 1; gain += (target - gain) * (target < gain ? fo : fi); redMin = Math.Min(redMin, gain);
            // output: the delayed sound, so the fade starts before the breath does
            dl[dw] = l[i]; dr[dw] = r[i]; int rd = (dw - look + len) % len; dw = (dw + 1) % len;
            double ol = dl[rd], or = dr[rd];
            if (monBreath) { double rem = 1 - gain; ol *= rem; or *= rem; }
            else
            {
                ol *= gain; or *= gain;
                if (room && gain < 0.999) { double nz = Noise(); roomLp += (nz - roomLp) * 0.15; double rt = roomLp * roomG * (1 - gain) * 3; ol += rt; or += rt; }
            }
            l[i] = (float)ol; r[i] = (float)or;
        }
        Reduction = (float)(20 * Math.Log10(Math.Max(1e-4, redMin)));
    }
}

#if !PURPHEX_CORE
class BreathOffWindow : PxWindow
{
    readonly BreathOffFx b;
    public BreathOffWindow(BreathOffFx f) : base(f, "BreathOff", new Size(900, 600)) { b = f; Accent = Cyan; }
    protected override string SubTitle { get { return "takes the breaths out of a vocal"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        float gw = a.Width - 230;
        var br = new RectangleF(a.X + 70, a.Y + 6, gw - 70, (a.Height - 40) / 2 - 10); var er = new RectangleF(br.X, br.Bottom + 30, br.Width, br.Height);
        Graph(g, br, "BREATH", b.HistBreath, 0, 100, fx.Get("sens"), Color.FromArgb(150, 130, 255), "sens");
        Graph(g, er, "ENERGY (GATE)", b.HistEnergy, -72, 0, fx.Get("gate"), Color.FromArgb(110, 230, 140), "gate");
        Knob(g, a.X + 32, br.Y + br.Height / 2 - 10, 22, "sens", "SENSITIVITY", Violet); Knob(g, a.X + 32, er.Y + er.Height / 2 - 10, 22, "gate", "GATE", Lime);
        // the right column: reduction, fades, monitor, room tone
        var rc = new RectangleF(a.Right - 210, a.Y, 210, a.Height - 30); Panel(g, rc, "REDUCTION", Cyan);
        Meter(g, new RectangleF(rc.X + 22, rc.Y + 30, 14, 150), -b.Reduction, "", true); Txt(g, b.Reduction <= -59 ? "-inf" : b.Reduction.ToString("0.0"), 11, Orange, new RectangleF(rc.X + 6, rc.Y + 184, 50, 14), StringAlignment.Center, true);
        Knob(g, rc.X + 130, rc.Y + 80, 30, "red", "AMOUNT", Cyan);
        Knob(g, rc.X + 60, rc.Y + 250, 20, "fout", "FADE OUT", Cyan); Knob(g, rc.X + 150, rc.Y + 250, 20, "fin", "FADE IN", Cyan);
        Txt(g, "MONITOR", 10, Dim, new RectangleF(rc.X + 16, rc.Y + 310, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(rc.X + 16, rc.Y + 328, rc.Width - 32, 26), "mon", Cyan);
        Txt(g, "ROOM TONE", 10, Dim, new RectangleF(rc.X + 16, rc.Y + 370, 100, 14), StringAlignment.Near, true); Toggle(g, new RectangleF(rc.X + 16, rc.Y + 388, 70, 26), "room", Lime);
        Knob(g, rc.X + 150, rc.Y + 400, 18, "roomlvl", "LEVEL", Lime);
    }
    void Graph(Graphics g, RectangleF r, string title, float[] hist, float lo, float hi, float line, Color col, string id)
    {
        Txt(g, title, 11, Ink, new RectangleF(r.X, r.Y - 18, r.Width, 16), StringAlignment.Center, true); Fill(g, Color.FromArgb(12, 10, 34), r, 8);
        int n = hist.Length, w = b.HistW; var pts = new PointF[n + 2]; pts[0] = new PointF(r.X, r.Bottom);
        for (int i = 0; i < n; i++) { float v = Math.Max(lo, Math.Min(hi, hist[(w + i) % n])); pts[i + 1] = new PointF(r.X + i * r.Width / (n - 1), r.Bottom - (v - lo) / (hi - lo) * r.Height); }
        pts[n + 1] = new PointF(r.Right, r.Bottom);
        using (var bb = new LinearGradientBrush(r, Color.FromArgb(200, col), Color.FromArgb(60, col), 90f)) g.FillPolygon(bb, pts);
        float ly = r.Bottom - (line - lo) / (hi - lo) * r.Height; using (var pen = new Pen(Color.White, 1.6f)) g.DrawLine(pen, r.X, ly, r.Right, ly);
        // drag the line to set it
        var p = fx.Find(id); var h = Reg(r, null, title + ": drag the white line up or down"); h.P = p; h.DragAt = pt => fx.Set(id, lo + (r.Bottom - pt.Y) / r.Height * (hi - lo)); h.Double = () => { Mark(); fx.Set(id, p.Def); };
        for (int k = 0; k <= 4; k++) { float v = lo + (hi - lo) * k / 4; Txt(g, v.ToString("0"), 9.5f, Dim, new RectangleF(r.X - 34, r.Bottom - k * r.Height / 4 - 7, 30, 13), StringAlignment.Far, false); }
    }
}
#endif
