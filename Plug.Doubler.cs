using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// a small, cheap pitch shifter (two read heads that cross-fade), used by the voices of Doubler and Octave Choir
class PxShifter
{
    readonly float[] buf; int w; double ph; const double Win = 0.045;
    public PxShifter(int size) { buf = new float[size]; }
    public void Reset() { Array.Clear(buf, 0, buf.Length); w = 0; ph = 0; }
    // in: the new sample; ratio: pitch (1 = same, 2 = octave up); delay: extra delay in samples
    public double Tick(double x, double ratio, double delay, int sr)
    {
        int L = buf.Length; buf[w] = (float)x; double win = Win * sr; ph += (1 - ratio) / win; if (ph >= 1) ph -= 1; if (ph < 0) ph += 1;
        double d1 = delay + 2 + ph * win, d2 = delay + 2 + ((ph + 0.5) % 1.0) * win;
        double g1 = Math.Sin(Math.PI * ph), g2 = Math.Sin(Math.PI * ((ph + 0.5) % 1.0));
        double y = Read(d1, L) * g1 * g1 + Read(d2, L) * g2 * g2; w = (w + 1) % L; return y;
    }
    public double Delayed(double x, double delay) { int L = buf.Length; buf[w] = (float)x; double y = Read(delay, L); w = (w + 1) % L; return y; }
    double Read(double d, int L) { double rp = w - d; while (rp < 0) rp += L; int i0 = (int)rp % L, i1 = (i0 + 1) % L; double fr = rp - Math.Floor(rp); return buf[i0] + (buf[i1] - buf[i0]) * fr; }
}

// ======================================================================
//  PURPHEX DOUBLER: up to four extra copies of the voice, each a little late, a little out of tune and placed in the stereo field, the way a
//  real double-tracked part sounds. Each VOICE has GAIN, PAN, DELAY, FEEDBACK, DETUNE (cents), an OCTAVE (up or down), and a slow wobble
//  (DEPTH, RATE). DIRECT sets the original's gain and pan; LOW and HIGH shelves shape the doubles; OUTPUT.
// ======================================================================
class DoublerFx : Fx
{
    readonly PxShifter[] sh = new PxShifter[4]; readonly double[] fbS = new double[4], modPh = new double[4]; readonly Biquad lo = new Biquad(), hi = new Biquad();
    static readonly string[] V = { "v0", "v1", "v2", "v3" };
    public DoublerFx()
    {
        Type = "pxdoubler"; Title = "Purphex Doubler"; Category = "Purphex VST"; Blurb = "Four-voice doubler: delay, detune, octave, pan and wobble for each voice";
        AddP("dgain", "Direct gain", -60, 6, 0, "dB"); AddP("dpan", "Direct pan", -100, 100, 0, "");
        float[] dl = { 10, 30, 20, 40 }, dt = { -5, 5, 5, -4 }, pn = { -70, 70, -35, 35 };
        for (int k = 0; k < 4; k++)
        {
            AddC(V[k] + "_on", "Voice " + (k + 1) + " on", new[] { "Off", "On" }, k < 2 ? 1 : 0); AddP(V[k] + "_g", "Voice " + (k + 1) + " gain", -60, 6, -6, "dB"); AddP(V[k] + "_pan", "Voice " + (k + 1) + " pan", -100, 100, pn[k], "");
            AddP(V[k] + "_d", "Voice " + (k + 1) + " delay", 0, 120, dl[k], "ms"); AddP(V[k] + "_fb", "Voice " + (k + 1) + " feedback", 0, 60, 8, "%"); AddP(V[k] + "_det", "Voice " + (k + 1) + " detune", -50, 50, dt[k], "ct");
            AddC(V[k] + "_oct", "Voice " + (k + 1) + " octave", new[] { "-1 oct", "0 oct", "+1 oct" }, 1); AddP(V[k] + "_dep", "Voice " + (k + 1) + " wobble depth", 0, 30, 0, "ct"); AddP(V[k] + "_rate", "Voice " + (k + 1) + " wobble rate", 0.05f, 8, 1, "Hz");
        }
        AddP("lo_g", "Low shelf", -12, 12, 0, "dB"); AddP("lo_f", "Low frequency", 20, 500, 40, "Hz"); AddP("hi_g", "High shelf", -12, 12, 0, "dB"); AddP("hi_f", "High frequency", 1000, 16000, 2200, "Hz"); AddP("out", "Output", -24, 12, 0, "dB");
        for (int k = 0; k < 4; k++) sh[k] = new PxShifter(16384);
    }
    readonly bool[] on = new bool[4]; readonly double[] g = new double[4], pl = new double[4], pr = new double[4], dly = new double[4], fb = new double[4], ratio = new double[4], dep = new double[4], rate = new double[4];
    double dg, dpl, dpr, outG;
    protected override void Update()
    {
        for (int k = 0; k < 4; k++) { on[k] = Get(V[k] + "_on") >= 0.5f; g[k] = Math.Pow(10, Get(V[k] + "_g") / 20); double pa = (Get(V[k] + "_pan") / 100.0 + 1) * Math.PI / 4; pl[k] = Math.Cos(pa); pr[k] = Math.Sin(pa); dly[k] = Get(V[k] + "_d") * 0.001 * SR; fb[k] = Get(V[k] + "_fb") / 100.0; ratio[k] = Math.Pow(2, ((int)Get(V[k] + "_oct") - 1) + Get(V[k] + "_det") / 1200.0); dep[k] = Get(V[k] + "_dep") / 1200.0; rate[k] = Get(V[k] + "_rate"); }
        dg = Math.Pow(10, Get("dgain") / 20); double dp = Get("dpan") / 100.0; dpl = Math.Min(1, 1 - dp); dpr = Math.Min(1, 1 + dp);   /* centre: both sides at full level */ outG = Math.Pow(10, Get("out") / 20);
        lo.Set(Biquad.LowShelf, SR, Get("lo_f"), 0.7, Get("lo_g")); hi.Set(Biquad.HighShelf, SR, Math.Min(Get("hi_f"), SR * 0.45), 0.7, Get("hi_g"));
    }
    public override void Reset() { base.Reset(); for (int k = 0; k < 4; k++) { sh[k].Reset(); fbS[k] = 0; modPh[k] = 0; } lo.Reset(); hi.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i], m = (xl + xr) * 0.5, wl = 0, wr = 0;
            for (int k = 0; k < 4; k++)
            {
                if (!on[k]) continue; modPh[k] += rate[k] / SR; if (modPh[k] > 1) modPh[k] -= 1;
                double rt = ratio[k] * Math.Pow(2, dep[k] * Math.Sin(2 * Math.PI * modPh[k]));
                double y = Math.Abs(rt - 1) < 1e-6 ? sh[k].Delayed(m + fbS[k] * fb[k], dly[k] + 1) : sh[k].Tick(m + fbS[k] * fb[k], rt, dly[k], SR); fbS[k] = y;
                wl += y * g[k] * pl[k]; wr += y * g[k] * pr[k];
            }
            wl = hi.TickL(lo.TickL((float)wl)); wr = hi.TickR(lo.TickR((float)wr));
            double ol = xl * dg * dpl + wl, or = xr * dg * dpr + wr;   // the original (balanced by DIRECT PAN) plus the doubles
            ol *= outG; or *= outG; if (double.IsNaN(ol) || double.IsNaN(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or;
        }
    }
}

#if !PURPHEX_CORE
class DoublerWindow : PxWindow
{
    static readonly Color[] Col = { Cyan, Lime, Gold, Pink };
    public DoublerWindow(DoublerFx f) : base(f, "Doubler", new Size(1000, 640)) { Accent = Cyan; }
    protected override string SubTitle { get { return "four-voice doubler"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // where the voices sit: pan across, delay up
        var pv = new RectangleF(a.X, a.Y, 300, 180); Panel(g, pv, "", Cyan); float cx = pv.X + pv.Width / 2, by = pv.Bottom - 16;
        using (var pen = new Pen(Color.FromArgb(70, 120, 180, 255), 2)) { g.DrawArc(pen, cx - 120, by - 120, 240, 240, 180, 180); g.DrawArc(pen, cx - 70, by - 70, 140, 140, 180, 180); }
        Dot(g, cx, by, fx.Get("dpan"), 0, Color.White, "D");
        for (int k = 0; k < 4; k++) if (fx.Get("v" + k + "_on") >= 0.5f) Dot(g, cx, by, fx.Get("v" + k + "_pan"), fx.Get("v" + k + "_d"), Col[k], (k + 1).ToString());
        // direct and EQ
        var dr = new RectangleF(pv.Right + 10, a.Y, a.Width - pv.Width - 10, 180); Panel(g, dr, "DIRECT  /  EQ  /  OUTPUT", Cyan);
        Knob(g, dr.X + 60, dr.Y + 76, 22, "dgain", "GAIN", Ink); Knob(g, dr.X + 140, dr.Y + 76, 22, "dpan", "PAN", Ink);
        Knob(g, dr.X + 250, dr.Y + 76, 20, "lo_g", "LOW", Cyan); Knob(g, dr.X + 320, dr.Y + 76, 20, "lo_f", "LOW FREQ", Cyan); Knob(g, dr.X + 400, dr.Y + 76, 20, "hi_g", "HIGH", Cyan); Knob(g, dr.X + 470, dr.Y + 76, 20, "hi_f", "HIGH FREQ", Cyan);
        Knob(g, dr.Right - 60, dr.Y + 76, 24, "out", "OUTPUT", Violet);
        // the voice table
        var tb = new RectangleF(a.X, pv.Bottom + 10, a.Width, a.Bottom - pv.Bottom - 10); Panel(g, tb, "", Violet);
        string[] ks = { "_g", "_pan", "_d", "_fb", "_det", "_dep", "_rate" }, ls = { "GAIN", "PAN", "DELAY", "FDBK", "DETUNE", "DEPTH", "RATE" };
        float rowH = (tb.Height - 20) / 4f, colW = (tb.Width - 200) / 7f;
        for (int k = 0; k < 4; k++)
        {
            float y = tb.Y + 10 + k * rowH + rowH / 2 - 8; using (var b = new SolidBrush(Col[k])) g.FillEllipse(b, tb.X + 16, y - 10, 20, 20); Txt(g, (k + 1).ToString(), 10, Color.Black, new RectangleF(tb.X + 16, y - 10, 20, 20), StringAlignment.Center, true);
            Toggle(g, new RectangleF(tb.X + 44, y - 11, 50, 22), "v" + k + "_on", Col[k]); Choice(g, new RectangleF(tb.X + 100, y - 11, 74, 22), "v" + k + "_oct", Col[k], 10); if (k == 3) Txt(g, "OCTAVE", 9, Dim, new RectangleF(tb.X + 100, y + 14, 74, 14), StringAlignment.Center, true);
            for (int c = 0; c < 7; c++) Knob(g, tb.X + 190 + colW * c + colW / 2, y - 6, 13, "v" + k + ks[c], k == 3 ? ls[c] : "", Col[k]);
        }
    }
    void Dot(Graphics g, float cx, float by, float pan, float delayMs, Color c, string t)
    {
        double ang = Math.PI * (1 - (pan + 100) / 200.0); float rad = 30 + delayMs / 120f * 100; float x = cx + (float)Math.Cos(ang) * rad, y = by - (float)Math.Sin(ang) * rad;
        using (var b = new SolidBrush(c)) g.FillEllipse(b, x - 8, y - 8, 16, 16); Txt(g, t, 9, Color.Black, new RectangleF(x - 8, y - 8, 16, 16), StringAlignment.Center, true);
    }
}
#endif

// ======================================================================
//  PURPHEX OCTAVE CHOIR: eight harmony voices. Each VOICE has ON / OFF, LEVEL, PAN, its own DELAY and FEEDBACK, an INTERVAL (semitones up
//  or down) and CENTS. KEY and SCALE bend every interval to the nearest note of the key (so a third is major or minor as the song needs).
//  RANDOMIZE adds a little life to the timing, MIX blends with the original, INPUT and OUTPUT.
// ======================================================================
class OctaveChoirFx : Fx
{
    const int VN = 8; readonly PxShifter[] sh = new PxShifter[VN]; readonly double[] fbS = new double[VN]; uint seed = 5; readonly double[] jit = new double[VN];
    public static readonly string[] Scales = { "Exact intervals", "Major (bright)", "Minor (dark)" };
    
    public OctaveChoirFx()
    {
        Type = "pxchoir"; Title = "Purphex Octave Choir"; Category = "Purphex VST"; Blurb = "Eight harmony voices with their own interval, delay, pan and level, snapped to a key";
        int[] iv = { 0, 7, 0, -5, 0, 12, 0, -12 }; float[] cents = { 4, 0, 0, 0, -4, 0, 0, 0 }; float[] pan = { -100, -71, -43, -14, 15, 43, 72, 100 }; float[] dl = { 16.6f, 115.4f, 230.8f, 346.2f, 461.5f, 576.9f, 692.3f, 807.7f };
        for (int k = 0; k < VN; k++) { string v = "o" + k; AddC(v + "_on", "Voice " + (k + 1) + " on", new[] { "Off", "On" }, k < 4 ? 1 : 0); AddP(v + "_lvl", "Voice " + (k + 1) + " level", -60, 6, -3, "dB"); AddP(v + "_pan", "Voice " + (k + 1) + " pan", -100, 100, pan[k], ""); AddP(v + "_d", "Voice " + (k + 1) + " delay", 0, 1000, dl[k], "ms"); AddP(v + "_fb", "Voice " + (k + 1) + " feedback", 0, 80, 22, "%"); AddP(v + "_iv", "Voice " + (k + 1) + " interval", -24, 24, iv[k], "st"); AddP(v + "_ct", "Voice " + (k + 1) + " cents", -50, 50, cents[k], "ct"); sh[k] = new PxShifter(96000); }
        AddC("scale", "Harmony", Scales, 0); AddP("rand", "Randomize", 0, 50, 20, "ms"); AddP("mix", "Mix", 0, 100, 50, "%"); AddP("in", "Input", -24, 12, 0, "dB"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    readonly bool[] on = new bool[VN]; readonly double[] lv = new double[VN], pl = new double[VN], pr = new double[VN], dly = new double[VN], fb = new double[VN], ratio = new double[VN]; double mix, inG, outG, randS;
    // HARMONY: major makes every 3rd, 6th and 7th the major one (4, 9, 11), minor the minor one (3, 8, 10); exact leaves them as set
    public static int Snap(int interval, int scale)
    {
        if (scale == 0) return interval; int oct = (int)Math.Floor(interval / 12.0), deg = ((interval % 12) + 12) % 12;
        if (deg == 3 || deg == 4) deg = scale == 1 ? 4 : 3; else if (deg == 8 || deg == 9) deg = scale == 1 ? 9 : 8; else if (deg == 10 || deg == 11) deg = scale == 1 ? 11 : 10;
        return oct * 12 + deg;
    }
    protected override void Update()
    {
        int scale = (int)Get("scale");
        for (int k = 0; k < VN; k++) { string v = "o" + k; on[k] = Get(v + "_on") >= 0.5f; lv[k] = Math.Pow(10, Get(v + "_lvl") / 20); double pa = (Get(v + "_pan") / 100.0 + 1) * Math.PI / 4; pl[k] = Math.Cos(pa); pr[k] = Math.Sin(pa); dly[k] = Get(v + "_d") * 0.001 * SR; fb[k] = Get(v + "_fb") / 100.0; ratio[k] = Math.Pow(2, (Snap((int)Math.Round(Get(v + "_iv")), scale) + Get(v + "_ct") / 100.0) / 12.0); }
        mix = Get("mix") / 100.0; inG = Math.Pow(10, Get("in") / 20); outG = Math.Pow(10, Get("out") / 20); randS = Get("rand") * 0.001 * SR;
    }
    public override void Reset() { base.Reset(); for (int k = 0; k < VN; k++) { sh[k].Reset(); fbS[k] = 0; jit[k] = 0; } }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            double xl = l[i] * inG, xr = r[i] * inG, m = (xl + xr) * 0.5, wl = 0, wr = 0;
            for (int k = 0; k < VN; k++)
            {
                if (!on[k]) continue; jit[k] += ((Rnd() - 0.5) * randS - jit[k]) * 0.0002;
                double input = m + fbS[k] * fb[k], d = Math.Max(0, dly[k] + jit[k]); double y = Math.Abs(ratio[k] - 1) < 1e-6 ? sh[k].Delayed(input, d + 1) : sh[k].Tick(input, ratio[k], d, SR); fbS[k] = y;
                wl += y * lv[k] * pl[k]; wr += y * lv[k] * pr[k];
            }
            double ol = (xl * (1 - mix) + wl * mix * 0.7) * outG, or = (xr * (1 - mix) + wr * mix * 0.7) * outG;
            if (double.IsNaN(ol) || double.IsNaN(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or;
        }
    }
}

#if !PURPHEX_CORE
class OctaveChoirWindow : PxWindow
{
    public OctaveChoirWindow(OctaveChoirFx f) : base(f, "Octave Choir", new Size(1060, 640)) { Accent = Orange; }
    protected override string SubTitle { get { return "eight harmony voices"; } }
    static Color VC(int k) { int t = k * 255 / 7; return Color.FromArgb(255, 200 + t / 5 > 255 ? 255 : 200 + t / 5, 220 - t * 2 / 3, 60); }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var tb = new RectangleF(a.X, a.Y, a.Width - 230, a.Height); Panel(g, tb, "", Orange);
        string[] hd = { "VOICE", "LEVEL", "PAN", "DELAY", "FEEDBACK", "INTERVAL", "CENTS" }; float[] cx = { 0, 140, 230, 320, 410, 500, 590 };
        for (int c = 0; c < hd.Length; c++) Txt(g, hd[c], 10.5f, Ink, new RectangleF(tb.X + 20 + cx[c] - (c == 0 ? 0 : 40), tb.Y + 8, 80, 16), c == 0 ? StringAlignment.Near : StringAlignment.Center, true);
        float rowH = (tb.Height - 40) / 8f; string[] ks = { "_lvl", "_pan", "_d", "_fb", "_iv", "_ct" };
        for (int k = 0; k < 8; k++)
        {
            float y = tb.Y + 32 + k * rowH + rowH / 2; var col = VC(k); using (var b = new SolidBrush(col)) g.FillRectangle(b, tb.X + 10, y - rowH / 2 + 4, 5, rowH - 8);
            Txt(g, (k + 1).ToString(), 12, Ink, new RectangleF(tb.X + 20, y - 10, 20, 20), StringAlignment.Center, true); Toggle(g, new RectangleF(tb.X + 46, y - 10, 50, 20), "o" + k + "_on", col);
            for (int c = 0; c < 6; c++) KnobRow(g, tb.X + 20 + cx[c + 1], y, "o" + k + ks[c], col);
        }
        var pc = new RectangleF(tb.Right + 10, a.Y, 220, a.Height); Panel(g, pc, "PITCH CONTROL", Orange);
        Txt(g, "HARMONY", 10, Dim, new RectangleF(pc.X + 16, pc.Y + 40, 120, 14), StringAlignment.Near, true); Choice(g, new RectangleF(pc.X + 16, pc.Y + 58, pc.Width - 32, 24), "scale", Orange, 10.5f);
        Txt(g, "bends 3rds, 6ths and 7ths", 9.5f, Dim, new RectangleF(pc.X + 16, pc.Y + 86, pc.Width - 32, 14), StringAlignment.Near, false);
        Knob(g, pc.X + pc.Width / 2, pc.Y + 180, 24, "rand", "RANDOMIZE", Orange);
        Knob(g, pc.X + 60, pc.Y + 290, 22, "in", "INPUT", Violet); Knob(g, pc.X + 160, pc.Y + 290, 22, "out", "OUTPUT", Violet); Knob(g, pc.X + pc.Width / 2, pc.Y + 400, 30, "mix", "MIX", Orange);
    }
    void KnobRow(Graphics g, float x, float y, string id, Color col)
    {
        var p = fx.Find(id); var r = new RectangleF(x - 40, y - 11, 80, 22); bool hot = Hot(r); Fill(g, hot ? Color.FromArgb(70, 50, 100) : Color.FromArgb(40, 30, 64), r, 5);
        float f = Frac(p); if (p.Min < 0 && p.Max > 0) { float z = (0 - p.Min) / (p.Max - p.Min); using (var b = new SolidBrush(Color.FromArgb(120, col))) g.FillRectangle(b, r.X + r.Width * Math.Min(z, f), r.Bottom - 3, r.Width * Math.Abs(f - z), 3); } else using (var b = new SolidBrush(Color.FromArgb(120, col))) g.FillRectangle(b, r.X, r.Bottom - 3, r.Width * f, 3);
        string txt = id.EndsWith("_iv") ? IvName((int)Math.Round(p.Val)) : Val(p); Txt(g, txt, 10.5f, col, r, StringAlignment.Center, true);
        float[] start = { 0 }; var h = Reg(r, null, p.Name + ": drag up or down, wheel, double-click resets"); h.P = p; h.Click = () => start[0] = p.Val;
        h.Drag = dy => { float v = start[0] + dy * (p.Max - p.Min); fx.Set(id, id.EndsWith("_iv") ? (float)Math.Round(v) : v); }; h.Wheel = d => { Mark(); fx.Set(id, p.Val + d * (id.EndsWith("_iv") ? 1 : (p.Max - p.Min) / 50f)); }; h.Double = () => { Mark(); fx.Set(id, p.Def); };
    }
    static string IvName(int st) { if (st == 0) return "unison"; string[] n = { "", "min 2nd", "2nd", "min 3rd", "3rd", "4th", "tritone", "5th", "min 6th", "6th", "min 7th", "7th" }; int a = Math.Abs(st); string s = a % 12 == 0 ? (a / 12) + " oct" : (a > 12 ? (a / 12) + " oct + " : "") + n[a % 12]; return (st > 0 ? "+" : "-") + s; }
}
#endif
