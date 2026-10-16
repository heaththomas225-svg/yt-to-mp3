using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX VINYL STOP: the record (or tape) slows to a stop and spins back up. Press the big STOP (or automate it); LENGTH is how long the
//  stop takes in beats, CURVE how it slows (gently then fast, or straight), and the four extras: PITCH (how far it drops), STEP (the slow-down
//  in steps, like a turntable motor), DIGI (a digital, crushed stop) and WASH (it darkens and smears as it stops). VINYL adds crackle.
// ======================================================================
class VinylStopFx : Fx
{
    readonly float[] bL = new float[96000 * 8], bR = new float[96000 * 8]; int w; double rp, speed = 1, lpL, lpR, holdL, holdR, holdC; bool wasStop; uint seed = 31;
    public volatile float Speed = 1, Turn;
    public VinylStopFx()
    {
        Type = "pxvinyl"; Title = "Purphex Vinyl Stop"; Category = "Purphex VST"; Blurb = "Vinyl and tape stop: length in beats, curve, pitch, step, digi, wash";
        AddC("stop", "Stop", new[] { "Play", "Stop" }, 0); AddC("len", "Length", new[] { "1/4", "1/2", "1 beat", "2 beats", "4/1 bar", "2 bars" }, 4); AddP("curve", "Curve", 0, 100, 60, "%");
        AddP("pitch", "Pitch", 0, 100, 100, "%"); AddP("step", "Step", 0, 100, 0, "%"); AddP("digi", "Digi", 0, 100, 0, "%"); AddP("wash", "Wash", 0, 100, 40, "%"); AddC("vinyl", "Vinyl", new[] { "Off", "On" }, 0); AddP("start", "Spin-up time", 0, 100, 30, "%");
    }
    static readonly double[] LenBeats = { 0.25, 0.5, 1, 2, 4, 8 };
    public override void Reset() { base.Reset(); Array.Clear(bL, 0, bL.Length); Array.Clear(bR, 0, bR.Length); w = 0; rp = 0; speed = 1; lpL = lpR = 0; wasStop = false; }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool stop = Get("stop") >= 0.5f; double beats = LenBeats[(int)Get("len")], len = Math.Max(64, beats * 60.0 / Math.Max(20, Fx.Bpm) * SR), curve = Get("curve") / 100.0;
        double pitchAmt = Get("pitch") / 100.0, step = Get("step") / 100.0, digi = Get("digi") / 100.0, wash = Get("wash") / 100.0, upLen = Math.Max(64, len * (0.1 + Get("start") / 100.0)); bool vinyl = Get("vinyl") >= 0.5f;
        int L = bL.Length;
        for (int i = 0; i < n; i++)
        {
            bL[w] = l[i]; bR[w] = r[i];
            if (stop && !wasStop) rp = w; wasStop = stop;
            if (stop) { double dec = 1.0 / len * ((1 - curve) + curve * (0.4 + speed * 1.2)); speed = Math.Max(0, speed - dec); }   // CURVE: straight, or gently at first then faster
            else { speed = Math.Min(1, speed + 1.0 / upLen); if (speed >= 1) rp = w; }
            double sp = 1 - (1 - speed) * pitchAmt; if (step > 0) { double q = 4 + (1 - step) * 28; sp = Math.Round(sp * q) / q; }
            double ol, or;
            if (speed >= 1 && !stop) { ol = l[i]; or = r[i]; }
            else
            {
                rp += sp; if (rp >= L) rp -= L; double lag = w - rp; if (lag < 0) lag += L; if (lag > L - 4) rp = w;
                int i0 = (int)rp, i1 = (i0 + 1) % L; double fr = rp - i0; ol = bL[i0] + (bL[i1] - bL[i0]) * fr; or = bR[i0] + (bR[i1] - bR[i0]) * fr;
                double vol = Math.Min(1, sp * 1.4); ol *= vol; or *= vol;
                if (wash > 0) { double a = 1 - Math.Exp(-2 * Math.PI * (300 + 18000 * Math.Pow(sp, 1 + wash * 3)) / SR); lpL += (ol - lpL) * a; lpR += (or - lpR) * a; ol = ol + (lpL - ol) * wash; or = or + (lpR - or) * wash; }
                if (digi > 0) { double hold = 1 + digi * 30 * (1 - sp); holdC += 1; if (holdC >= hold) { holdC -= hold; double q = Math.Pow(2, 15 - digi * 11 * (1 - sp)); holdL = Math.Round(ol * q) / q; holdR = Math.Round(or * q) / q; } ol = holdL; or = holdR; }
            }
            if (vinyl) { double c = Rnd() < 0.0006 ? (Rnd() - 0.5) * 0.15 : 0; double hiss = (Rnd() - 0.5) * 0.004; ol += c + hiss; or += c + hiss; }
            l[i] = (float)ol; r[i] = (float)or; w = (w + 1) % L;
        }
        Speed = (float)speed; Turn = (float)((Turn + speed * n / (double)SR * 0.55 * 360) % 360);
    }
}

class VinylStopWindow : PxWindow
{
    readonly VinylStopFx v;
    public VinylStopWindow(VinylStopFx f) : base(f, "Vinyl Stop", new Size(760, 600)) { v = f; Accent = Violet; }
    protected override string SubTitle { get { return "vinyl and tape stop"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var top = new RectangleF(a.X, a.Y, a.Width - 160, 330); Panel(g, top, "", Violet);
        float cx = top.X + top.Width / 2, cy = top.Y + 160, R = 120; bool stop = fx.Get("stop") >= 0.5f;
        using (var pen = new Pen(Color.FromArgb(stop ? 255 : 200, 120, 90, 230), 26)) g.DrawEllipse(pen, cx - R, cy - R, R * 2, R * 2);
        using (var pen = new Pen(Color.FromArgb(150, 120, 90, 230), 2)) g.DrawEllipse(pen, cx - R + 22, cy - R + 22, (R - 22) * 2, (R - 22) * 2);
        for (int k = 0; k < 4; k++) { double ang = (v.Turn + k * 90) * Math.PI / 180; using (var pen = new Pen(Color.FromArgb(200, 120, 90, 230), 3)) g.DrawLine(pen, cx + (float)Math.Cos(ang) * (R - 30), cy + (float)Math.Sin(ang) * (R - 30), cx + (float)Math.Cos(ang) * (R - 16), cy + (float)Math.Sin(ang) * (R - 16)); }
        Txt(g, stop ? "STOP" : "PLAY", 22, Color.FromArgb(160, 140, 255), new RectangleF(cx - 60, cy - 16, 120, 32), StringAlignment.Center, true);
        Reg(new RectangleF(cx - R, cy - R, R * 2, R * 2), () => { Mark(); fx.Set("stop", stop ? 0 : 1); }, stop ? "Press to spin back up" : "Press to stop the record (or automate STOP)");
        Toggle(g, new RectangleF(top.X + 16, top.Bottom - 34, 80, 22), "vinyl", Violet, "VINYL", "VINYL"); Choice(g, new RectangleF(top.Right - 110, top.Bottom - 34, 96, 22), "len", Violet, 10);
        Txt(g, "LENGTH", 9.5f, Dim, new RectangleF(top.Right - 170, top.Bottom - 34, 56, 22), StringAlignment.Far, true);
        var cv = new RectangleF(top.Right + 10, a.Y, 150, 330); Panel(g, cv, "CURVE", Violet); HSliderV(g, new RectangleF(cv.X + cv.Width / 2 - 14, cv.Y + 40, 28, cv.Height - 100), "curve");
        var bot = new RectangleF(a.X, top.Bottom + 10, a.Width, a.Bottom - top.Bottom - 10); float w4 = bot.Width / 5; string[] ks = { "pitch", "step", "digi", "wash", "start" }, ls = { "PITCH", "STEP", "DIGI", "WASH", "SPIN-UP" };
        for (int i = 0; i < 5; i++) { var cell = new RectangleF(bot.X + w4 * i + 3, bot.Y, w4 - 6, bot.Height); Fill(g, Color.FromArgb(34, 30, 44), cell, 8); Knob(g, cell.X + cell.Width / 2, cell.Y + 60, 26, ks[i], ls[i], Violet); }
    }
    void HSliderV(Graphics g, RectangleF r, string id)
    {
        var p = fx.Find(id); float f = Frac(p); Fill(g, Color.FromArgb(40, 30, 70), new RectangleF(r.X + r.Width / 2 - 2, r.Y, 4, r.Height), 2);
        for (int k = 0; k <= 8; k++) using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255))) g.DrawLine(pen, r.X + 4, r.Y + r.Height * k / 8, r.Right - 4, r.Y + r.Height * k / 8);
        float y = r.Bottom - f * r.Height; Fill(g, Violet, new RectangleF(r.X - 6, y - 7, r.Width + 12, 14), 4); Txt(g, Val(p), 10, Ink, new RectangleF(r.X - 30, r.Bottom + 4, r.Width + 60, 14), StringAlignment.Center, false);
        var h = Reg(new RectangleF(r.X - 10, r.Y - 8, r.Width + 20, r.Height + 16), null, p.Name + ": drag up or down"); h.P = p; h.DragAt = pt => fx.Set(id, FromFrac(p, Math.Max(0, Math.Min(1, (r.Bottom - pt.Y) / r.Height)))); h.Double = () => { Mark(); fx.Set(id, p.Def); };
    }
}

// ======================================================================
//  PURPHEX RADIO: a broken radio. TUNE drifts off and on the station (between stations: static); STATIC, FEEDBACK (it howls), RAW (dry signal
//  leaking through), AM and RM (wobble and metal), BITCRUSH and SKIP (it cuts out). BANDWIDTH from narrow to wide, DRIVE from clean to dirty,
//  NOISE and SQUELCH (a gate that closes on quiet parts). MIX and OUTPUT. The display reads like a radio.
// ======================================================================
class RadioFx : Fx
{
    double clk, hp1L, hp2L, lp1L, lp2L, hp1R, hp2R, lp1R, lp2R, amPh, rmPh, crushC, crushL, crushR, sqEnv, fbL, fbR, skipT; uint seed = 99; readonly float[] fbBuf = new float[8192]; int fbw;
    public volatile float Signal, InLvl, OutLvl;
    public RadioFx()
    {
        Type = "pxradio"; Title = "Purphex Radio"; Category = "Purphex VST"; Blurb = "Broken radio: tune, static, squelch, bandwidth, drive, AM / RM, bitcrush, skip";
        AddP("tune", "Tune", 0, 100, 50, ""); AddP("static", "Static", 0, 100, 25, "%"); AddP("fb", "Feedback", 0, 100, 0, "%"); AddP("raw", "Raw", 0, 100, 0, "%"); AddP("am", "AM", 0, 100, 0, "%"); AddP("rm", "RM", 0, 100, 0, "%");
        AddP("crush", "Bitcrush", 0, 100, 0, "%"); AddP("skip", "Skip", 0, 100, 0, "%"); AddP("bw", "Bandwidth", 0, 100, 40, "%"); AddP("drive", "Drive", 0, 100, 30, "%"); AddP("noise", "Noise", 0, 100, 20, "%"); AddP("squelch", "Squelch", 0, 100, 0, "%");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); hp1L = hp2L = lp1L = lp2L = hp1R = hp2R = lp1R = lp2R = 0; amPh = rmPh = 0; crushC = 0; sqEnv = 0; fbL = fbR = 0; Array.Clear(fbBuf, 0, fbBuf.Length); fbw = 0; skipT = 0; }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double tune = Get("tune") / 100.0, off = Math.Abs(tune - 0.5) * 2, clarity = Math.Max(0, 1 - off * 1.6), stat = Get("static") / 100.0, fb = Get("fb") / 100.0, raw = Get("raw") / 100.0, am = Get("am") / 100.0, rm = Get("rm") / 100.0;
        double crush = Get("crush") / 100.0, skip = Get("skip") / 100.0, bw = Get("bw") / 100.0, drive = 1 + Get("drive") / 100.0 * 20, noise = Get("noise") / 100.0, sq = Get("squelch") / 100.0, mix = Get("mix") / 100.0, outG = Math.Pow(10, Get("out") / 20);
        double lo = 120 + (1 - bw) * 700, hi = 1800 + bw * 7000, ha = Math.Exp(-2 * Math.PI * lo / SR), la = 1 - Math.Exp(-2 * Math.PI * Math.Min(SR * 0.45, hi) / SR), sqThr = Math.Pow(10, (-60 + sq * 45) / 20);
        float il = 0, ol2 = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i]; il = (float)Math.Max(il, Math.Abs(xl));
            // the band of the radio (2 high-pass, 2 low-pass)
            double yl = Band(xl, ha, la, ref hp1L, ref hp2L, ref lp1L, ref lp2L), yr = Band(xr, ha, la, ref hp1R, ref hp2R, ref lp1R, ref lp2R);
            yl = Math.Tanh(yl * drive) / Math.Sqrt(drive) * 1.6; yr = Math.Tanh(yr * drive) / Math.Sqrt(drive) * 1.6;
            if (am > 0) { amPh += (3 + am * 9) / SR; if (amPh > 1) amPh -= 1; double g = 1 - am * 0.5 * (1 + Math.Sin(2 * Math.PI * amPh)); yl *= g; yr *= g; }
            if (rm > 0) { rmPh += (200 + rm * 1200) / SR; if (rmPh > 1) rmPh -= 1; double c = Math.Sin(2 * Math.PI * rmPh); yl = yl * (1 - rm) + yl * c * rm; yr = yr * (1 - rm) + yr * c * rm; }
            // off the station: the signal fades into static
            double hiss = (Rnd() * 2 - 1); double st = hiss * (0.02 + stat * 0.15 + (1 - clarity) * 0.25) * (0.6 + 0.4 * Math.Sin((clk += 0.001) + tune * 40)); yl = yl * clarity + st + (Rnd() * 2 - 1) * noise * 0.03; yr = yr * clarity + st * 0.9 + (Rnd() * 2 - 1) * noise * 0.03;
            if (fb > 0) { int d = 1200 + (int)(tune * 900); int rd = (fbw - d + fbBuf.Length) % fbBuf.Length; double f = fbBuf[rd]; fbBuf[fbw] = (float)Math.Tanh((yl + yr) * 0.5 + f * fb * 0.95); fbw = (fbw + 1) % fbBuf.Length; yl += f * fb; yr += f * fb; }
            if (crush > 0) { double hold = 1 + crush * 20; crushC += 1; if (crushC >= hold) { crushC -= hold; double q = Math.Pow(2, 12 - crush * 9); crushL = Math.Round(yl * q) / q; crushR = Math.Round(yr * q) / q; } yl = crushL; yr = crushR; }
            if (skip > 0) { if (skipT <= 0 && Rnd() < skip * 4.0 / SR) skipT = (0.03 + Rnd() * 0.12) * SR; if (skipT > 0) { skipT--; yl *= 0.05; yr *= 0.05; } }
            sqEnv = Math.Max(Math.Abs(xl + xr) * 0.5, sqEnv * 0.9995); if (sq > 0 && sqEnv < sqThr) { yl *= 0.02; yr *= 0.02; }
            yl += xl * raw; yr += xr * raw;
            double ol = (xl + (yl - xl) * mix) * outG, or = (xr + (yr - xr) * mix) * outG;
            if (double.IsNaN(ol) || double.IsNaN(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or; ol2 = (float)Math.Max(ol2, Math.Abs(ol));
        }
        Signal = (float)clarity; InLvl = Math.Max(InLvl * 0.9f, il); OutLvl = Math.Max(OutLvl * 0.9f, ol2);
    }
    // the radio band: two low-pass poles, then two high-pass poles
    static double Band(double x, double ha, double la, ref double h1, ref double h2, ref double l1, ref double l2) { l1 += (x - l1) * la; l2 += (l1 - l2) * la; h1 += (l2 - h1) * (1 - ha); double y = l2 - h1; h2 += (y - h2) * (1 - ha); return y - h2; }
}

class RadioWindow : PxWindow
{
    readonly RadioFx rd;
    public RadioWindow(RadioFx f) : base(f, "Radio", new Size(960, 600)) { rd = f; Accent = Lime; }
    protected override string SubTitle { get { return "broken radio"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var sig = new RectangleF(a.X, a.Y, 150, 330); Panel(g, sig, "SIGNAL", Lime); Meter(g, new RectangleF(sig.X + 34, sig.Y + 34, 18, sig.Height - 70), rd.InLvl, "IN"); Meter(g, new RectangleF(sig.X + 86, sig.Y + 34, 18, sig.Height - 70), rd.OutLvl, "OUT");
        var mid = new RectangleF(sig.Right + 10, a.Y, a.Width - 470, 330); Panel(g, mid, "", Lime);
        var lcd = new RectangleF(mid.X + 30, mid.Y + 14, mid.Width - 60, 34); Fill(g, Color.FromArgb(10, 30, 16), lcd, 4); string msg = rd.Signal > 0.85f ? "10-4 SIGNAL CLEAR" : rd.Signal > 0.3f ? "10-77 WEAK CONTACT" : "SIGNAL LOST...";
        Txt(g, msg, 14, Color.FromArgb(120, 255, 140), lcd, StringAlignment.Center, true);
        Knob(g, mid.X + mid.Width / 2, mid.Y + 170, 70, "tune", "TUNE", Lime, true);
        string[] sk = { "static", "fb", "raw", "am", "rm", "crush", "skip" }, sl = { "STATIC", "FEEDBACK", "RAW", "AM", "RM", "BITCRUSH", "SKIP" };
        float[,] pos = { { 0.12f, 0.36f }, { 0.88f, 0.36f }, { 0.12f, 0.62f }, { 0.12f, 0.88f }, { 0.35f, 0.92f }, { 0.65f, 0.92f }, { 0.88f, 0.62f } };
        for (int i = 0; i < 7; i++) Knob(g, mid.X + mid.Width * pos[i, 0], mid.Y + mid.Height * pos[i, 1] - 10, 16, sk[i], sl[i], Lime);
        var rc = new RectangleF(mid.Right + 10, a.Y, a.Right - mid.Right - 10, 330); Panel(g, rc, "", Lime); string[] rk = { "bw", "drive", "noise", "squelch" }, rl = { "BANDWIDTH", "DRIVE", "NOISE", "SQUELCH" };
        for (int i = 0; i < 4; i++) Knob(g, rc.X + rc.Width / 2, rc.Y + 48 + i * 74, 22, rk[i], rl[i], i == 1 ? Orange : Lime);
        var bot = new RectangleF(a.X, a.Y + 340, a.Width, a.Bottom - a.Y - 340); Panel(g, bot, "", Lime);
        Knob(g, bot.X + 120, bot.Y + 60, 26, "mix", "MIX", Lime); Knob(g, bot.X + 260, bot.Y + 60, 26, "out", "OUTPUT", Lime);
        Txt(g, "GOOD LUCK OUT THERE", 16, Color.FromArgb(90, 170, 200, 160), new RectangleF(bot.X + 380, bot.Y + 40, bot.Width - 400, 40), StringAlignment.Center, true);
    }
}
