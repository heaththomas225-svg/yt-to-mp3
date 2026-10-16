using System;
using System.Drawing;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX GLOW: a four-band enhancer. The sound is split into LOW, MID, HIGH and AIR (the four parts add back to exactly the input),
//  each band gets its own enhancement with two characters to pick from, and only the difference (what the enhancement adds) is mixed in:
//  so every band at 0 is perfectly transparent. WET INJECT scales all four at once, FX ONLY lets you hear just what is added,
//  LEVEL MATCH keeps the loudness the same as the input, M/S works the lows and mids on the middle and the highs and air on the sides.
//  Then high-pass / low-pass filters and a saturation stage (drive, distort or crush).
// ======================================================================
class GlowFx : Fx
{
    public static readonly string[] Bands = { "LOW", "MID", "HIGH", "AIR" };
    public static readonly string[][] Chars = { new[] { "Thick", "Punch" }, new[] { "Warm", "Broad" }, new[] { "Shine", "Excite" }, new[] { "Silk", "Sparkle" } };
    static readonly double[] XF = { 180, 2200, 8000 };
    // the band splits: a 2nd-order low-pass per crossover (the rest is the input minus the low part, so the bands always add up)
    readonly double[,] lp = new double[3, 8];   // per crossover: L s1 s2, R s1 s2 (TPT state-variable)
    double fastL, slowL, compEnvL, compEnvR, crushHold, crushL, crushR, inRms, outRms, lmGain = 1, inG = 1, outG = 1;
    readonly Biquad hp1 = new Biquad(), hp2 = new Biquad(), lp1 = new Biquad(), lp2 = new Biquad();
    static readonly string[] IdOn = { "l_on", "m_on", "h_on", "a_on" }, IdAmt = { "l_amt", "m_amt", "h_amt", "a_amt" }, IdChr = { "l_chr", "m_chr", "h_chr", "a_chr" };
    readonly double[] amt = new double[4], bs = new double[4], bandE = new double[4], bandA = new double[4]; readonly int[] chr = new int[4];
    public readonly float[] BandIn = new float[4], BandAdd = new float[4]; public volatile float InPk, OutPk;
    public GlowFx()
    {
        Type = "pxglow"; Title = "Purphex Glow"; Category = "Purphex VST"; Blurb = "Four-band enhancer: low, mid, high and air, wet inject, saturation";
        for (int b = 0; b < 4; b++)
        {
            string k = "lmha"[b].ToString();
            AddP(k + "_amt", Bands[b].Substring(0, 1) + Bands[b].Substring(1).ToLowerInvariant() + " amount", 0, 100, new[] { 35f, 25f, 30f, 25f }[b], "");
            AddC(k + "_chr", Bands[b].Substring(0, 1) + Bands[b].Substring(1).ToLowerInvariant() + " character", Chars[b], b == 0 ? 1 : 0);
            AddC(k + "_on", Bands[b].Substring(0, 1) + Bands[b].Substring(1).ToLowerInvariant() + " on", new[] { "Off", "On" }, 1);
        }
        AddP("inject", "Wet inject", 0, 100, 50, "%"); AddC("ms", "Stereo mode", new[] { "Stereo", "M/S" }, 0); AddC("fxonly", "FX only", new[] { "Off", "On" }, 0); AddC("lvl", "Level match", new[] { "Off", "On" }, 0);
        AddC("filt", "Filters", new[] { "Off", "On" }, 0); AddP("hp", "High-pass", 10, 1000, 20, "Hz"); AddP("lp", "Low-pass", 1000, 22000, 22000, "Hz"); AddC("steep", "Filter slope", new[] { "12 dB", "24 dB" }, 0);
        AddP("sat", "Saturation", 0, 100, 0, ""); AddC("satmode", "Saturation type", new[] { "Off", "Drive", "Distort", "Crush" }, 1);
        AddP("in", "Input", -24, 24, 0, "dB"); AddP("out", "Output", -24, 24, 0, "dB");
    }
    public override void Reset() { base.Reset(); Array.Clear(lp, 0, lp.Length); fastL = slowL = compEnvL = compEnvR = 0; crushHold = 0; inRms = outRms = 0; lmGain = 1; hp1.Reset(); hp2.Reset(); lp1.Reset(); lp2.Reset(); }
    protected override void Update()
    {
        hp1.Set(Biquad.HighPass, SR, Get("hp"), 0.707, 0); hp2.Set(Biquad.HighPass, SR, Get("hp"), 0.707, 0);
        lp1.Set(Biquad.LowPass, SR, Math.Min(Get("lp"), SR * 0.45), 0.707, 0); lp2.Set(Biquad.LowPass, SR, Math.Min(Get("lp"), SR * 0.45), 0.707, 0);
    }
    double Lp(int k, int ch, double x, double g)
    {
        int o = ch * 2; double s1 = lp[k, o], s2 = lp[k, o + 1];
        double hpv = (x - (2 * 0.7071 + g) * s1 - s2) / (1 + g * (g + 2 * 0.7071)); double v1 = g * hpv, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, low = v2 + s2; s2 = low + v2;
        lp[k, o] = s1; lp[k, o + 1] = s2; return low;
    }
    static double Tube(double x) { return x >= 0 ? Math.Tanh(x) : Math.Tanh(x * 0.8) / 0.8; }
    // what one band adds (enhanced minus original), for one channel
    double Enh(int band, int chr, double b, double a, int ch)
    {
        if (a <= 0) return 0;
        switch (band)
        {
            case 0:
                if (chr == 0) return a * (Math.Tanh(b * (1 + 4 * a)) / (1 + 1.5 * a) * 1.6 - b * 0.6) + a * 0.5 * b;   // thick: rounded, denser lows
                { double e = Math.Max(0, fastL - slowL * 1.2) / (slowL + 1e-4); return b * a * Math.Min(2.5, e * 1.5) + a * 0.25 * b; }   // punch: the attacks jump out
            case 1:
                if (chr == 0) return a * 0.9 * (Tube(b * (1 + 3 * a)) / (1 + a) - b) + a * 0.35 * b;   // warm: tube colour
                { double env = ch == 0 ? compEnvL : compEnvR; double gain = 1 / Math.Max(0.05, Math.Sqrt(env + 1e-6) * 4 + 0.25); return a * 0.6 * b * Math.Min(3, gain); }   // broad: quiet detail lifted (parallel squash)
            case 2:
                if (chr == 0) return a * (Math.Tanh(b * 3) / 3 * 1.8 - b * 0.4) + a * 0.45 * b;   // shine: soft harmonics
                return a * (Math.Tanh(b * 5) * 0.35 + b * Math.Abs(b) * 3) + a * 0.3 * b;   // excite: brighter, edgier harmonics
            default:
                if (chr == 0) return a * 1.1 * b;   // silk: a smooth lift of the air band
                return a * (Math.Tanh(b * 8) / 8 * 2.5 + b * Math.Abs(b) * 2) + a * 0.6 * b;   // sparkle: fine harmonics on top
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double inj = Get("inject") / 50.0; Array.Clear(bandE, 0, 4); Array.Clear(bandA, 0, 4);
        for (int b = 0; b < 4; b++) { amt[b] = Get(IdOn[b]) < 0.5f ? 0 : Math.Min(2, Get(IdAmt[b]) / 100.0 * inj); chr[b] = (int)Get(IdChr[b]); }
        bool ms = Get("ms") >= 0.5f, fxOnly = Get("fxonly") >= 0.5f, lvl = Get("lvl") >= 0.5f, filt = Get("filt") >= 0.5f, steep = Get("steep") >= 0.5f;
        double sat = Get("sat") / 100.0; int satMode = (int)Get("satmode"); double inT = Math.Pow(10, Get("in") / 20), outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        double g0 = Math.Tan(Math.PI * XF[0] / SR), g1 = Math.Tan(Math.PI * Math.Min(XF[1], SR * 0.45) / SR), g2 = Math.Tan(Math.PI * Math.Min(XF[2], SR * 0.45) / SR);
        double fa = 1 - Math.Exp(-1.0 / (0.002 * SR)), sa = 1 - Math.Exp(-1.0 / (0.06 * SR)), ca = 1 - Math.Exp(-1.0 / (0.03 * SR)), rmsA = 1 - Math.Exp(-1.0 / (0.4 * SR));
        float inPk = 0, outPk = 0;
        for (int i = 0; i < n; i++)
        {
            inG += (inT - inG) * gs; outG += (outT - outG) * gs;
            double xl = l[i] * inG, xr = r[i] * inG; inPk = (float)Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            double c0 = ms ? (xl + xr) * 0.5 : xl, c1 = ms ? (xl - xr) * 0.5 : xr;   // the two channels worked on (L/R, or mid/side)
            double addL = 0, addR = 0;
            for (int ch = 0; ch < 2; ch++)
            {
                double x = ch == 0 ? c0 : c1;
                double lo = Lp(0, ch, x, g0), rest = x - lo, mi = Lp(1, ch, rest, g1), rest2 = rest - mi, hi = Lp(2, ch, rest2, g2), air = rest2 - hi;
                if (ch == 0) { double al = Math.Abs(lo); fastL += (al - fastL) * fa; slowL += (al - slowL) * sa; compEnvL += (mi * mi - compEnvL) * ca; } else compEnvR += (mi * mi - compEnvR) * ca;
                bs[0] = lo; bs[1] = mi; bs[2] = hi; bs[3] = air; double add = 0;
                for (int b = 0; b < 4; b++)
                {
                    if (ms && ((ch == 1 && b < 2) || (ch == 0 && b >= 2))) continue;   // M/S: lows and mids on the middle, highs and air on the sides
                    double e = Enh(b, chr[b], bs[b], amt[b], ch); add += e; bandE[b] += bs[b] * bs[b]; bandA[b] += e * e;
                }
                if (ch == 0) addL = add; else addR = add;
            }
            double wl, wr; if (ms) { wl = addL + addR; wr = addL - addR; } else { wl = addL; wr = addR; }
            double yl = fxOnly ? wl : xl + wl, yr = fxOnly ? wr : xr + wr;
            if (filt) { yl = hp1.TickL((float)yl); yr = hp1.TickR((float)yr); if (steep) { yl = hp2.TickL((float)yl); yr = hp2.TickR((float)yr); } yl = lp1.TickL((float)yl); yr = lp1.TickR((float)yr); if (steep) { yl = lp2.TickL((float)yl); yr = lp2.TickR((float)yr); } }
            if (sat > 0 && satMode > 0)
            {
                double dl = yl, dr = yr;
                if (satMode == 1) { double k = 1 + sat * 8; dl = yl + (Math.Tanh(yl * k) / Math.Sqrt(k) - yl) * sat; dr = yr + (Math.Tanh(yr * k) / Math.Sqrt(k) - yr) * sat; }
                else if (satMode == 2) { double c = 1 - sat * 0.85; dl = Math.Max(-c, Math.Min(c, yl)) / Math.Sqrt(c); dr = Math.Max(-c, Math.Min(c, yr)) / Math.Sqrt(c); dl = yl + (dl - yl) * Math.Min(1, sat * 1.5); dr = yr + (dr - yr) * Math.Min(1, sat * 1.5); }
                else { double bits = 16 - sat * 12, q = Math.Pow(2, bits - 1), hold = 1 + sat * 15; crushHold += 1; if (crushHold >= hold) { crushHold -= hold; crushL = Math.Round(yl * q) / q; crushR = Math.Round(yr * q) / q; } dl = yl + (crushL - yl) * sat; dr = yr + (crushR - yr) * sat; }
                yl = dl; yr = dr;
            }
            if (lvl && !fxOnly)
            {
                inRms += ((xl * xl + xr * xr) * 0.5 - inRms) * rmsA; outRms += ((yl * yl + yr * yr) * 0.5 - outRms) * rmsA;
                double t = outRms > 1e-9 ? Math.Sqrt(inRms / outRms) : 1; t = Math.Max(0.25, Math.Min(2, t)); lmGain += (t - lmGain) * rmsA; yl *= lmGain; yr *= lmGain;
            }
            yl *= outG; yr *= outG;
            if (double.IsNaN(yl) || double.IsInfinity(yl) || double.IsNaN(yr) || double.IsInfinity(yr)) { Reset(); yl = xl; yr = xr; }
            l[i] = (float)yl; r[i] = (float)yr; outPk = (float)Math.Max(outPk, Math.Max(Math.Abs(yl), Math.Abs(yr)));
        }
        for (int b = 0; b < 4; b++) { BandIn[b] = BandIn[b] * 0.8f + 0.2f * (float)Math.Sqrt(bandE[b] / Math.Max(1, n)); BandAdd[b] = BandAdd[b] * 0.8f + 0.2f * (float)Math.Sqrt(bandA[b] / Math.Max(1, n)); }
        InPk = Math.Max(InPk * 0.85f, inPk); OutPk = Math.Max(OutPk * 0.85f, outPk);
    }
}

class GlowWindow : PxWindow
{
    readonly GlowFx g0;
    public GlowWindow(GlowFx f) : base(f, "Glow", new Size(900, 640)) { g0 = f; Accent = Pink; }
    protected override string SubTitle { get { return "four-band enhancer"; } }
    static readonly Color[] BandCol = { Color.FromArgb(255, 120, 90), Color.FromArgb(255, 80, 150), Color.FromArgb(200, 90, 255), Color.FromArgb(90, 150, 255) };
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the four bands: their level and what is added, an amount bar you drag, the two characters and bypass
        var top = new RectangleF(a.X, a.Y, a.Width, 300); Panel(g, top, "", Pink);
        float bw = (top.Width - 40) / 4f;
        for (int b = 0; b < 4; b++)
        {
            string k = "lmha"[b].ToString(); var col = BandCol[b]; var area = new RectangleF(top.X + 20 + b * bw + 6, top.Y + 16, bw - 12, 190);
            Fill(g, Color.FromArgb(14, 8, 30), area, 8);
            var p = fx.Find(k + "_amt"); float f = p.Val / 100f; bool byp = fx.Get(k + "_on") < 0.5f;
            float lv = (float)Math.Min(1, Math.Sqrt(g0.BandIn[b]) * 1.6), ad = (float)Math.Min(1, Math.Sqrt(g0.BandAdd[b]) * 2.2);
            int bars = 14; float bwid = (area.Width - 16) / bars;
            for (int q = 0; q < bars; q++) { float h = (area.Height - 30) * Math.Max(0.03f, Math.Min(1, lv * (0.6f + 0.4f * (float)Math.Sin(q * 0.9 + b + Environment.TickCount / 600.0)))); using (var br = new SolidBrush(Color.FromArgb(byp ? 40 : 110, col))) g.FillRectangle(br, area.X + 8 + q * bwid, area.Bottom - 10 - h, bwid - 2, h); }
            float ah = (area.Height - 30) * f; var amtR = new RectangleF(area.X + 8, area.Bottom - 10 - ah, area.Width - 16, ah);
            using (var br = new SolidBrush(Color.FromArgb(byp ? 30 : 70 + (int)(ad * 120), col))) g.FillRectangle(br, amtR); using (var pen = new Pen(col, 2)) g.DrawLine(pen, amtR.X, amtR.Y, amtR.Right, amtR.Y);
            var tag = new RectangleF(area.X + area.Width / 2 - 22, amtR.Y - 22, 44, 18); Fill(g, Color.FromArgb(220, 20, 12, 40), tag, 5); Txt(g, p.Val.ToString("0"), 11, Ink, tag, StringAlignment.Center, true);
            Txt(g, GlowFx.Bands[b], 10, Dim, new RectangleF(area.X, area.Bottom + 2, area.Width, 14), StringAlignment.Center, true);
            var hb = Reg(area, null, GlowFx.Bands[b] + " amount: drag up or down"); hb.P = p; var rr = area; string id = k + "_amt";
            hb.DragAt = pt => fx.Set(id, Math.Max(0, Math.Min(100, (rr.Bottom - 10 - pt.Y) / (rr.Height - 30) * 100))); hb.Double = () => { Mark(); fx.Set(id, p.Def); }; hb.Wheel = d => { Mark(); fx.Set(id, p.Val + d * 2); };
            Segments(g, new RectangleF(area.X, area.Bottom + 22, area.Width, 26), k + "_chr", col, 10.5f);
            Toggle(g, new RectangleF(area.X + area.Width / 2 - 30, area.Bottom + 54, 60, 22), k + "_on", Gold);
        }
        // the wet inject macro in the middle, with stereo / M/S, FX ONLY and LEVEL MATCH beside it; meters on the sides
        var mid = new RectangleF(a.X, top.Bottom + 10, a.Width, a.Bottom - top.Bottom - 10); Panel(g, mid, "", Violet);
        Meter(g, new RectangleF(mid.X + 18, mid.Y + 18, 14, mid.Height - 50), g0.InPk, "IN"); Meter(g, new RectangleF(mid.Right - 32, mid.Y + 18, 14, mid.Height - 50), g0.OutPk, "OUT");
        float cx = mid.X + mid.Width / 2;
        Knob(g, cx, mid.Y + 92, 62, "inject", "WET INJECT", Pink, true);
        Segments(g, new RectangleF(cx - 230, mid.Y + 40, 150, 28), "ms", Violet); Toggle(g, new RectangleF(cx + 100, mid.Y + 40, 110, 28), "fxonly", Cyan, "FX ONLY", "FX ONLY");
        Toggle(g, new RectangleF(cx + 100, mid.Y + 76, 110, 28), "lvl", Cyan, "LEVEL MATCH", "LEVEL MATCH");
        Knob(g, mid.X + 90, mid.Y + 80, 22, "in", "INPUT", Violet); Knob(g, mid.Right - 90, mid.Y + 80, 22, "out", "OUTPUT", Violet);
        float by = mid.Bottom - 70;
        Toggle(g, new RectangleF(mid.X + 60, by + 6, 80, 26), "filt", Lime, "FILTERS", "FILTERS"); Segments(g, new RectangleF(mid.X + 60, by + 38, 80, 22), "steep", Lime, 9.5f);
        Knob(g, mid.X + 190, by + 22, 18, "hp", "HIGH-PASS", Lime); Knob(g, mid.X + 270, by + 22, 18, "lp", "LOW-PASS", Lime);
        Knob(g, mid.Right - 230, by + 22, 22, "sat", "SATURATION", Orange); Choice(g, new RectangleF(mid.Right - 170, by + 10, 100, 26), "satmode", Orange);
    }
}
