using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX FROST: spectral freeze with a ring modulator and a feedback delay around it.
//  FREEZE catches the sound as it is (its whole spectrum) and holds it as an endless pad, with FADE (how slowly it blooms in) and WIDTH
//  (the two sides drift apart); the TILT points shape the frozen spectrum; CLEAR lets go. RING MOD multiplies the sound with a wave (bells,
//  robots), through its own filter. FEEDBACK is a delay (ping-pong or not) with its own filter. An LFO can move the ring frequency,
//  the delay time or a filter. MASTER: the three in a row or side by side, DRY, WET and a soft clip.
// ======================================================================
class FrostFx : Fx
{
    const int N = 2048, H = 512;
    readonly double[] win = new double[N], re = new double[N], im = new double[N], mag = new double[N / 2 + 1], ph0 = new double[N / 2 + 1], ph1 = new double[N / 2 + 1];
    readonly float[] inBuf = new float[N], outL = new float[N * 2], outR = new float[N * 2]; int inW, hopCnt, outRd; double frozenAmt; bool haveFrame, wasFreeze; uint seed = 1234;
    readonly float[] dL = new float[192000], dR = new float[192000]; int dW;
    double rmPh, lfoPh, rs1L, rs2L, rs1R, rs2R, fs1L, fs2L, fs1R, fs2R;
    public readonly float[] Spec = new float[256]; public volatile bool Frozen; public volatile float OutPk;
    public FrostFx()
    {
        Type = "pxfrost"; Title = "Purphex Frost"; Category = "Purphex VST"; Blurb = "Spectral freeze with a ring modulator, a feedback delay and an LFO";
        AddC("fz_on", "Freeze section", new[] { "Off", "On" }, 1); AddC("freeze", "Freeze", new[] { "Off", "On" }, 0); AddP("fade", "Freeze fade", 1, 4000, 400, "ms"); AddP("width", "Freeze width", 0, 100, 50, "%");
        AddP("lo", "Frozen low", -24, 12, 0, "dB"); AddP("mid", "Frozen mid", -24, 12, 0, "dB"); AddP("hi", "Frozen high", -24, 12, 0, "dB"); AddP("fz_mix", "Freeze mix", 0, 100, 100, "%");
        AddC("rm_on", "Ring mod", new[] { "Off", "On" }, 0); AddC("rm_wave", "Ring wave", new[] { "Sine", "Triangle", "Square", "Saw" }, 0); AddP("rm_freq", "Ring frequency", 1, 5000, 220, "Hz"); AddC("rm_ft", "Ring filter", new[] { "Low-pass", "Band-pass", "High-pass" }, 0); AddP("rm_cut", "Ring cutoff", 50, 20000, 8000, "Hz"); AddP("rm_res", "Ring resonance", 0, 100, 20, "%"); AddP("rm_mix", "Ring mix", 0, 100, 50, "%");
        AddC("fb_on", "Feedback", new[] { "Off", "On" }, 0); AddP("fb_time", "Delay", 10, 2000, 375, "ms"); AddC("fb_pp", "Ping-pong", new[] { "Off", "On" }, 1); AddP("fb_amt", "Feedback amount", 0, 95, 55, "%"); AddC("fb_ft", "Delay filter", new[] { "Low-pass", "Band-pass", "High-pass" }, 0); AddP("fb_cut", "Delay cutoff", 50, 20000, 5000, "Hz"); AddP("fb_res", "Delay resonance", 0, 100, 10, "%"); AddP("fb_mix", "Delay mix", 0, 100, 35, "%");
        AddC("lfo_wave", "LFO wave", new[] { "Sine", "Triangle", "Square", "Random" }, 0); AddP("lfo_rate", "LFO rate", 0.02f, 20, 0.5f, "Hz"); AddC("lfo_dest", "LFO destination", new[] { "Ring frequency", "Delay time", "Ring cutoff", "Delay cutoff" }, 0); AddP("lfo_amt", "LFO amount", 0, 100, 0, "%");
        AddC("serial", "Mode", new[] { "Serial", "Parallel" }, 0); AddP("dry", "Dry", 0, 100, 100, "%"); AddP("wet", "Wet", 0, 100, 60, "%"); AddC("clip", "Soft clip", new[] { "Off", "On" }, 1);
        for (int i = 0; i < N; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N);
    }
    public override void Reset() { base.Reset(); Array.Clear(inBuf, 0, N); Array.Clear(outL, 0, outL.Length); Array.Clear(outR, 0, outR.Length); inW = 0; hopCnt = 0; outRd = 0; frozenAmt = 0; haveFrame = false; Array.Clear(dL, 0, dL.Length); Array.Clear(dR, 0, dR.Length); dW = 0; rmPh = lfoPh = 0; rs1L = rs2L = rs1R = rs2R = fs1L = fs2L = fs1R = fs2R = 0; }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    static double Svf(double x, double g, double k, int type, ref double s1, ref double s2) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return type == 0 ? lp : type == 1 ? bp * k : hp; }
    // the frozen pad: every hop one new frame from the held spectrum (phases keep turning at each bin's own speed, a little random for life)
    void Synth(double width, double lo, double mid, double hi)
    {
        for (int side = 0; side < (width < 0.01 ? 1 : 2); side++)   // width 0: one pad for both sides (mono)
        {
            var ph = side == 0 ? ph0 : ph1;
            for (int k = 0; k <= N / 2; k++)
            {
                double f = k * (double)SR / N, gdb = f < 300 ? lo : f < 3000 ? mid : hi, g = Math.Pow(10, gdb / 20);
                ph[k] += 2 * Math.PI * k * H / N + (Rnd() - 0.5) * (0.3 + width * 2.5);
                re[k] = mag[k] * g * Math.Cos(ph[k]); im[k] = mag[k] * g * Math.Sin(ph[k]); if (k > 0 && k < N / 2) { re[N - k] = re[k]; im[N - k] = -im[k]; }
            }
            Fft.Transform(re, im, true); var o = side == 0 ? outL : outR;
            for (int i = 0; i < N; i++) { float v = (float)(re[i] * win[i] / 1.5); o[(outRd + i) % (N * 2)] += v; if (width < 0.01) outR[(outRd + i) % (N * 2)] += v; }   // Hann in and out at a quarter hop add up to 1.5
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        bool fz = Get("fz_on") >= 0.5f, freeze = fz && Get("freeze") >= 0.5f, rm = Get("rm_on") >= 0.5f, fb = Get("fb_on") >= 0.5f, par = Get("serial") >= 0.5f, clip = Get("clip") >= 0.5f, pp = Get("fb_pp") >= 0.5f;
        double fadeC = 1 - Math.Exp(-1.0 / (Get("fade") * 0.001 * SR)), width = Get("width") / 100.0, lo = Get("lo"), mid = Get("mid"), hi = Get("hi"), fzMix = Get("fz_mix") / 100.0;
        int rmWave = (int)Get("rm_wave"), rmFt = (int)Get("rm_ft"), fbFt = (int)Get("fb_ft"); double rmFreq = Get("rm_freq"), rmCut = Get("rm_cut"), rmK = 2 - 1.9 * Get("rm_res") / 100.0, rmMix = Get("rm_mix") / 100.0;
        double fbTime = Get("fb_time"), fbAmt = Get("fb_amt") / 100.0, fbCut = Get("fb_cut"), fbK = 2 - 1.9 * Get("fb_res") / 100.0, fbMix = Get("fb_mix") / 100.0;
        int lw = (int)Get("lfo_wave"), dest = (int)Get("lfo_dest"); double lr = Get("lfo_rate"), la = Get("lfo_amt") / 100.0, dry = Get("dry") / 100.0, wet = Get("wet") / 100.0;
        if (freeze && !wasFreeze) haveFrame = false; wasFreeze = freeze; Frozen = freeze;
        float pk = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i];
            lfoPh += lr / SR; if (lfoPh >= 1) lfoPh -= 1; double lfo = lw == 0 ? Math.Sin(2 * Math.PI * lfoPh) : lw == 1 ? 1 - 4 * Math.Abs(lfoPh - 0.5) : lw == 2 ? (lfoPh < 0.5 ? 1 : -1) : Math.Sin(2 * Math.PI * Math.Floor(lfoPh * 8) * 1.618) ; double mod = lfo * la;
            // RING MOD
            double al = xl, ar = xr;
            if (rm)
            {
                double f = rmFreq * (dest == 0 ? Math.Pow(2, mod * 3) : 1); rmPh += f / SR; if (rmPh >= 1) rmPh -= 1;
                double c = rmWave == 0 ? Math.Sin(2 * Math.PI * rmPh) : rmWave == 1 ? 1 - 4 * Math.Abs(rmPh - 0.5) : rmWave == 2 ? (rmPh < 0.5 ? 1 : -1) : 2 * rmPh - 1;
                double cut = Math.Min(SR * 0.45, rmCut * (dest == 2 ? Math.Pow(2, mod * 3) : 1)), g = Math.Tan(Math.PI * cut / SR);
                double ml = Svf(xl * c, g, rmK, rmFt, ref rs1L, ref rs2L), mr = Svf(xr * c, g, rmK, rmFt, ref rs1R, ref rs2R); al = xl + (ml - xl) * rmMix; ar = xr + (mr - xr) * rmMix;
            }
            // FREEZE: the analysis keeps running; when frozen the held spectrum plays as a pad
            double bl = par ? xl : al, br = par ? xr : ar, fl = bl, fr = br;
            if (fz)
            {
                inBuf[inW] = (float)((bl + br) * 0.5); inW = (inW + 1) % N;
                if (++hopCnt >= H)
                {
                    hopCnt = 0;
                    if (freeze && !haveFrame) { for (int k = 0; k < N; k++) { re[k] = inBuf[(inW + k) % N] * win[k]; im[k] = 0; } Fft.Transform(re, im, false); for (int k = 0; k <= N / 2; k++) { mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); ph0[k] = Math.Atan2(im[k], re[k]); ph1[k] = ph0[k]; } haveFrame = true; }
                    if (haveFrame && frozenAmt > 0.0001) Synth(width, lo, mid, hi);
                    if (n > 0) for (int k = 0; k < 256; k++) { int b = (int)(Math.Pow(N / 2.0, k / 255.0)); Spec[k] = (float)Math.Max(0, 20 * Math.Log10(mag[Math.Min(N / 2, b)] / (N / 4.0) + 1e-9) + 90) / 90f; }
                }
                frozenAmt += ((freeze ? 1 : 0) - frozenAmt) * fadeC;
                double pl = outL[outRd], pr = outR[outRd]; outL[outRd] = 0; outR[outRd] = 0; outRd = (outRd + 1) % (N * 2);
                fl = bl * (1 - frozenAmt * fzMix) + pl * frozenAmt * fzMix; fr = br * (1 - frozenAmt * fzMix) + pr * frozenAmt * fzMix;
            }
            // FEEDBACK delay
            double cl = par ? xl : fl, cr = par ? xr : fr, el = cl, er = cr;
            if (fb)
            {
                double t = fbTime * (dest == 1 ? Math.Pow(2, mod) : 1); int dl = (int)Math.Min(dL.Length - 2, Math.Max(1, t * 0.001 * SR)); int rd = (dW - dl + dL.Length) % dL.Length; double ol = dL[rd], or = dR[rd];
                double cut = Math.Min(SR * 0.45, fbCut * (dest == 3 ? Math.Pow(2, mod * 3) : 1)), g = Math.Tan(Math.PI * cut / SR); double flt = Svf(ol, g, fbK, fbFt, ref fs1L, ref fs2L), frt = Svf(or, g, fbK, fbFt, ref fs1R, ref fs2R);
                if (pp) { dL[dW] = (float)((cl + cr) * 0.5 + frt * fbAmt); dR[dW] = (float)(flt * fbAmt); } else { dL[dW] = (float)(cl + flt * fbAmt); dR[dW] = (float)(cr + frt * fbAmt); }
                dW = (dW + 1) % dL.Length; el = cl + flt * fbMix * 1.5; er = cr + frt * fbMix * 1.5;
            }
            // the effect is what the chain adds; DRY sets the original, WET the effect (both full = the whole chain)
            double addL = par ? (al - xl) + (fl - xl) + (el - xl) : el - xl, addR = par ? (ar - xr) + (fr - xr) + (er - xr) : er - xr;
            double yl = xl * dry + addL * wet, yr = xr * dry + addR * wet;
            if (clip) { yl = Math.Tanh(yl); yr = Math.Tanh(yr); }
            if (double.IsNaN(yl) || double.IsNaN(yr) || double.IsInfinity(yl) || double.IsInfinity(yr)) { Reset(); yl = xl; yr = xr; }
            l[i] = (float)yl; r[i] = (float)yr; pk = (float)Math.Max(pk, Math.Max(Math.Abs(yl), Math.Abs(yr)));
        }
        OutPk = Math.Max(OutPk * 0.85f, pk);
    }
}

class FrostWindow : PxWindow
{
    readonly FrostFx fr;
    public FrostWindow(FrostFx f) : base(f, "Frost", new Size(1000, 620)) { fr = f; Accent = Cyan; }
    protected override string SubTitle { get { return "spectral freeze, ring mod and feedback"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        float w = (a.Width - 20) / 3, h = 330;
        // RING MOD
        var rm = new RectangleF(a.X, a.Y, w, h); Sec(g, rm, "RING MOD", "rm_on", Cyan);
        Segments(g, new RectangleF(rm.X + 14, rm.Y + 36, rm.Width - 28, 24), "rm_wave", Cyan, 9.5f); Knob(g, rm.X + rm.Width / 2, rm.Y + 110, 30, "rm_freq", "FREQ", Cyan);
        Choice(g, new RectangleF(rm.X + 14, rm.Y + 176, 110, 24), "rm_ft", Cyan, 10); Knob(g, rm.X + 180, rm.Y + 200, 20, "rm_cut", "CUTOFF", Cyan); Knob(g, rm.X + 260, rm.Y + 200, 20, "rm_res", "RESO", Cyan); Knob(g, rm.X + 70, rm.Y + 270, 20, "rm_mix", "MIX", Cyan);
        // FREEZE
        var fz = new RectangleF(rm.Right + 10, a.Y, w, h); Sec(g, fz, "FREEZE", "fz_on", Color.FromArgb(150, 220, 255));
        var sp = new RectangleF(fz.X + 14, fz.Y + 36, fz.Width - 120, 110); Fill(g, Color.FromArgb(14, 20, 40), sp, 8);
        using (var pen = new Pen(Color.FromArgb(200, 150, 220, 255), 1.4f)) { var pts = new PointF[256]; for (int k = 0; k < 256; k++) pts[k] = new PointF(sp.X + k * sp.Width / 255, sp.Bottom - fr.Spec[k] * (sp.Height - 6)); g.DrawLines(pen, pts); }
        bool on = fx.Get("freeze") >= 0.5f; var fb = new RectangleF(fz.Right - 92, fz.Y + 40, 76, 76); bool hot = Hot(fb);
        using (var b = new SolidBrush(on ? Color.FromArgb(255, 150, 225, 255) : hot ? Color.FromArgb(70, 50, 110) : Color.FromArgb(40, 30, 80))) g.FillEllipse(b, fb); using (var pen = new Pen(Color.FromArgb(220, 150, 220, 255), 2)) g.DrawEllipse(pen, fb);
        Txt(g, "❄", 30, on ? Color.FromArgb(20, 30, 60) : Color.FromArgb(200, 230, 255), fb, StringAlignment.Center, false); Reg(fb, () => { Mark(); fx.Set("freeze", on ? 0 : 1); }, on ? "Frozen: press to let go (CLEAR)" : "Freeze the sound now");
        Button(g, new RectangleF(fz.Right - 92, fz.Y + 124, 76, 22), "CLEAR", false, Pink, () => { Mark(); fx.Set("freeze", 0); }, "Let go of the frozen sound", 10);
        Knob(g, fz.X + 50, fz.Y + 200, 20, "fade", "FADE", Cyan); Knob(g, fz.X + 125, fz.Y + 200, 20, "width", "WIDTH", Cyan); Knob(g, fz.X + 200, fz.Y + 200, 20, "fz_mix", "MIX", Cyan);
        Knob(g, fz.X + 50, fz.Y + 280, 17, "lo", "LOW", Cyan); Knob(g, fz.X + 125, fz.Y + 280, 17, "mid", "MID", Cyan); Knob(g, fz.X + 200, fz.Y + 280, 17, "hi", "HIGH", Cyan);
        // FEEDBACK
        var fd = new RectangleF(fz.Right + 10, a.Y, w, h); Sec(g, fd, "FEEDBACK", "fb_on", Violet);
        Knob(g, fd.X + 60, fd.Y + 80, 26, "fb_time", "DELAY", Violet); Toggle(g, new RectangleF(fd.X + 130, fd.Y + 50, 100, 24), "fb_pp", Violet, "PING PONG", "PING PONG"); Knob(g, fd.X + 260, fd.Y + 80, 26, "fb_amt", "AMOUNT", Violet);
        Choice(g, new RectangleF(fd.X + 14, fd.Y + 176, 110, 24), "fb_ft", Violet, 10); Knob(g, fd.X + 180, fd.Y + 200, 20, "fb_cut", "CUTOFF", Violet); Knob(g, fd.X + 260, fd.Y + 200, 20, "fb_res", "RESO", Violet); Knob(g, fd.X + 70, fd.Y + 270, 20, "fb_mix", "MIX", Violet);
        // LFO and MASTER
        var lf = new RectangleF(a.X, a.Y + h + 10, a.Width * 0.58f, a.Height - h - 10); Panel(g, lf, "LFO", Gold);
        Segments(g, new RectangleF(lf.X + 14, lf.Y + 34, 260, 24), "lfo_wave", Gold, 9.5f); Knob(g, lf.X + 340, lf.Y + 70, 24, "lfo_rate", "RATE", Gold); Choice(g, new RectangleF(lf.X + 14, lf.Y + 80, 180, 24), "lfo_dest", Gold, 10); Knob(g, lf.X + 460, lf.Y + 70, 24, "lfo_amt", "AMOUNT", Gold);
        var ms = new RectangleF(lf.Right + 10, lf.Y, a.Right - lf.Right - 10, lf.Height); Panel(g, ms, "MASTER", Pink);
        Segments(g, new RectangleF(ms.X + 14, ms.Y + 34, 170, 24), "serial", Pink, 10); Toggle(g, new RectangleF(ms.X + 14, ms.Y + 70, 100, 24), "clip", Pink, "SOFT CLIP", "SOFT CLIP");
        Knob(g, ms.Right - 150, ms.Y + 70, 24, "dry", "DRY", Pink); Knob(g, ms.Right - 70, ms.Y + 70, 24, "wet", "WET", Pink);
    }
    void Sec(Graphics g, RectangleF r, string title, string on, Color col) { bool o = fx.Get(on) >= 0.5f; Fill(g, o ? Color.FromArgb(34, 26, 70) : PanelC, r, 12); Stroke(g, Color.FromArgb(o ? 220 : 90, col), r, 12, o ? 1.6f : 1); Txt(g, title, 12, o ? Ink : Dim, new RectangleF(r.X + 12, r.Y + 8, r.Width - 80, 18), StringAlignment.Near, true); Toggle(g, new RectangleF(r.Right - 60, r.Y + 6, 50, 22), on, col); }
}
