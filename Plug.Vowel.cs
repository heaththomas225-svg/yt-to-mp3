using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX VOWEL: makes any sound speak vowels. Drag the dot around the vowel pad (front to back, closed to open) or press a vowel; the sound
//  is shaped by the three formants of that vowel and glides between them. DRIVE adds grit first, SHIFT moves the formants up or down
//  (a smaller or bigger "mouth"), Q makes them sharper, MIX blends. SNAP pulls the dot to the nearest vowel, and MOVE sweeps it by itself
//  (left-right, up-down or a circle, free or in time with the song). LOUDNESS MATCH keeps the level even.
// ======================================================================
class PxVowelFx : Fx
{
    // the vowels: name, x (front 0 .. back 1), y (closed 0 .. open 1), and their first three formants (Hz)
    public static readonly string[] Names = { "i", "ɪ", "ɛ", "æ", "ɑ", "ʌ", "ɔ", "o", "ʊ", "u" };
    public static readonly float[,] Pos = { { 0.05f, 0.02f }, { 0.15f, 0.22f }, { 0.12f, 0.55f }, { 0.22f, 0.95f }, { 0.85f, 0.95f }, { 0.62f, 0.62f }, { 0.85f, 0.6f }, { 0.9f, 0.4f }, { 0.85f, 0.22f }, { 0.97f, 0.02f } };
    static readonly double[,] Fm = { { 270, 2290, 3010 }, { 390, 1990, 2550 }, { 530, 1840, 2480 }, { 660, 1720, 2410 }, { 730, 1090, 2440 }, { 640, 1190, 2390 }, { 570, 840, 2410 }, { 450, 830, 2380 }, { 440, 1020, 2240 }, { 300, 870, 2240 } };
    readonly double[] s1 = new double[6], s2 = new double[6]; readonly double[] f = new double[3]; double lfoPh, rin, rout, lmG = 1;
    public volatile float DotX, DotY;
    public PxVowelFx()
    {
        Type = "pxvowel"; Title = "Purphex Vowel"; Category = "Purphex VST"; Blurb = "Vowel formant filter: drag around a vowel pad, snap, shift and move";
        AddP("x", "Position (front - back)", 0, 1, 0.5f, ""); AddP("y", "Position (closed - open)", 0, 1, 0.6f, ""); AddP("drive", "Drive", 0, 100, 10, "%"); AddP("shift", "Shift", -12, 12, 0, "st"); AddP("q", "Q", 1, 30, 8, ""); AddP("mix", "Mix", 0, 100, 100, "%");
        AddP("snap", "Snap", 0, 100, 0, "%"); AddC("move", "Move", new[] { "Off", "Left - right", "Up - down", "Circle" }, 0); AddC("msync", "Move time", new[] { "Free", "1 bar", "1/2", "1/4", "1/8" }, 0); AddP("mrate", "Move rate", 0.05f, 8, 0.5f, "Hz"); AddP("mdepth", "Move depth", 0, 100, 50, "%");
        AddC("lm", "Loudness match", new[] { "Off", "On" }, 1);
    }
    static readonly double[] SyncBeats = { 0, 4, 2, 1, 0.5 };
    public override void Reset() { base.Reset(); Array.Clear(s1, 0, 6); Array.Clear(s2, 0, 6); lfoPh = 0; rin = rout = 0; lmG = 1; }
    // the formants at a point of the pad: a weighted blend of the nearby vowels (SNAP pulls toward the nearest one)
    void Formants(double x, double y, double snap)
    {
        double sw = 0; f[0] = f[1] = f[2] = 0; int near = 0; double nd = 9;
        for (int v = 0; v < 10; v++) { double dx = x - Pos[v, 0], dy = y - Pos[v, 1], d = dx * dx + dy * dy; if (d < nd) { nd = d; near = v; } }
        for (int v = 0; v < 10; v++) { double dx = x - Pos[v, 0], dy = y - Pos[v, 1], d = dx * dx + dy * dy, wgt = 1 / (d * d + 1e-6); if (v == near) wgt *= 1 + snap * 200; sw += wgt; for (int k = 0; k < 3; k++) f[k] += Math.Log(Fm[v, k]) * wgt; }
        for (int k = 0; k < 3; k++) f[k] = Math.Exp(f[k] / sw);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double x0 = Get("x"), y0 = Get("y"), drive = 1 + Get("drive") / 100.0 * 12, sh = Math.Pow(2, Get("shift") / 12.0), q = Get("q"), mix = Get("mix") / 100.0, snap = Get("snap") / 100.0;
        int mv = (int)Get("move"), ms = (int)Get("msync"); double rate = ms == 0 ? Get("mrate") : Fx.Bpm / 60.0 / SyncBeats[ms], md = Get("mdepth") / 100.0 * 0.5; bool lm = Get("lm") >= 0.5f; double ra = 1 - Math.Exp(-1.0 / (0.3 * SR));
        for (int i = 0; i < n; i++)
        {
            if ((i & 15) == 0)
            {   // the filter is moved every 16 samples
                double x = x0, y = y0; if (mv > 0) { lfoPh += rate * 16 / SR; if (lfoPh > 1) lfoPh -= 1; double s = Math.Sin(2 * Math.PI * lfoPh), c = Math.Cos(2 * Math.PI * lfoPh); if (mv == 1) x += s * md; else if (mv == 2) y += s * md; else { x += s * md; y += c * md; } }
                x = Math.Max(0, Math.Min(1, x)); y = Math.Max(0, Math.Min(1, y)); Formants(x, y, snap); DotX = (float)x; DotY = (float)y;
            }
            double xl = l[i], xr = r[i], ol = 0, or = 0, dl = Math.Tanh(xl * drive) / Math.Sqrt(drive), dr = Math.Tanh(xr * drive) / Math.Sqrt(drive);
            for (int k = 0; k < 3; k++)
            {
                double fc = Math.Min(SR * 0.45, f[k] * sh), g = Math.Tan(Math.PI * fc / SR), kk = 1.0 / (q * (k == 0 ? 1 : 1.4)), amp = k == 0 ? 1 : k == 1 ? 0.6 : 0.35;
                ol += Bp(dl, g, kk, k) * amp; or += Bp(dr, g, kk, k + 3) * amp;
            }
            ol *= 2.5; or *= 2.5;
            double yl = xl + (ol - xl) * mix, yr = xr + (or - xr) * mix;
            if (lm) { rin += ((xl * xl + xr * xr) * 0.5 - rin) * ra; rout += ((yl * yl + yr * yr) * 0.5 - rout) * ra; double t = rout > 1e-10 ? Math.Sqrt(rin / rout) : 1; t = Math.Max(0.25, Math.Min(4, t)); lmG += (t - lmG) * ra; yl *= lmG; yr *= lmG; }
            if (double.IsNaN(yl) || double.IsNaN(yr)) { Reset(); yl = xl; yr = xr; }
            l[i] = (float)yl; r[i] = (float)yr;
        }
    }
    double Bp(double x, double g, double k, int j) { double hp = (x - (k + g) * s1[j] - s2[j]) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1[j]; s1[j] = bp + v1; double v2 = g * bp, lp = v2 + s2[j]; s2[j] = lp + v2; return bp * k; }   // a state-variable band-pass, unity gain at its centre
}

class PxVowelWindow : PxWindow
{
    readonly PxVowelFx v;
    public PxVowelWindow(PxVowelFx f) : base(f, "Vowel", new Size(960, 540)) { v = f; Accent = Gold; }
    protected override string SubTitle { get { return "vowel formant filter"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var pad = new RectangleF(a.Right - 420, a.Y, 420, 380);
        using (var b = new LinearGradientBrush(pad, Color.FromArgb(90, 110, 60), Color.FromArgb(70, 60, 120), 0f)) using (var p = RR(pad, 10)) g.FillPath(b, p);
        using (var b = new LinearGradientBrush(pad, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(120, 140, 70, 40), 90f)) using (var p = RR(pad, 10)) g.FillPath(b, p);
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255))) for (int k = 1; k < 4; k++) { g.DrawLine(pen, pad.X + pad.Width * k / 4, pad.Y, pad.X + pad.Width * k / 4, pad.Bottom); g.DrawLine(pen, pad.X, pad.Y + pad.Height * k / 4, pad.Right, pad.Y + pad.Height * k / 4); }
        for (int k = 0; k < 10; k++) { float x = pad.X + PxVowelFx.Pos[k, 0] * pad.Width, y = pad.Y + PxVowelFx.Pos[k, 1] * pad.Height; Txt(g, PxVowelFx.Names[k], 16, Color.FromArgb(230, 255, 230, 170), new RectangleF(x - 14, y - 10, 28, 20), StringAlignment.Center, true); }
        float dx = pad.X + v.DotX * pad.Width, dy = pad.Y + v.DotY * pad.Height; using (var b = new SolidBrush(Color.FromArgb(220, 255, 140, 170))) g.FillEllipse(b, dx - 10, dy - 10, 20, 20); using (var pen = new Pen(Color.White, 2)) g.DrawEllipse(pen, dx - 10, dy - 10, 20, 20);
        var h = Reg(pad, null, "Drag around the vowels"); h.P = fx.Find("x"); h.DragAt = pt => { fx.Set("x", (pt.X - pad.X) / pad.Width); fx.Set("y", (pt.Y - pad.Y) / pad.Height); };
        // the vowel buttons
        float bw = a.Width / 10; for (int k = 0; k < 10; k++) { int kk = k; bool near = Math.Abs(fx.Get("x") - PxVowelFx.Pos[k, 0]) < 0.04f && Math.Abs(fx.Get("y") - PxVowelFx.Pos[k, 1]) < 0.04f; Button(g, new RectangleF(a.X + bw * k + 2, a.Bottom - 40, bw - 4, 34), PxVowelFx.Names[k], near, Pink, () => { Mark(); fx.Set("x", PxVowelFx.Pos[kk, 0]); fx.Set("y", PxVowelFx.Pos[kk, 1]); }, "Jump to this vowel", 16); }
        // knobs
        var lk = new RectangleF(a.X, a.Y, pad.X - a.X - 10, 380); Panel(g, lk, "", Gold);
        Knob(g, lk.X + 70, lk.Y + 80, 30, "drive", "DRIVE", Gold); Knob(g, lk.X + 190, lk.Y + 80, 30, "shift", "SHIFT", Gold); Knob(g, lk.X + 310, lk.Y + 80, 30, "q", "Q", Gold); Knob(g, lk.X + 430, lk.Y + 80, 30, "mix", "MIX", Gold);
        Knob(g, lk.X + 70, lk.Y + 230, 26, "snap", "SNAP", Pink); Txt(g, "MOVEMENT", 10, Dim, new RectangleF(lk.X + 140, lk.Y + 180, 130, 14), StringAlignment.Near, true); Choice(g, new RectangleF(lk.X + 140, lk.Y + 198, 130, 24), "move", Pink, 10); Choice(g, new RectangleF(lk.X + 140, lk.Y + 232, 130, 24), "msync", Pink, 10);
        Knob(g, lk.X + 320, lk.Y + 230, 24, "mrate", "RATE", Pink); Knob(g, lk.X + 430, lk.Y + 230, 24, "mdepth", "DEPTH", Pink);
        Toggle(g, new RectangleF(lk.X + 16, lk.Bottom - 40, 150, 26), "lm", Cyan, "LOUDNESS MATCH", "LOUDNESS MATCH");
    }
}

// ======================================================================
//  PURPHEX STEP SHAPER: sixteen steps move the volume (and a low-pass filter) in time with the song. Click or drag the bars to set each step,
//  GRID sets how long a step is, SMOOTH rounds the edges, SWING pushes every second step late, RATE plays the pattern at half, normal or double.
//  VOLUME is how deep the steps cut, LP FILTER and RESONANCE shape the filter, FOLLOW makes the filter follow the steps too.
// ======================================================================
class StepShaperFx : SyncFx
{
    public static readonly string[] Grids = { "1/4", "1/8", "1/16", "1/32" }; static readonly double[] GB = { 1, 0.5, 0.25, 0.125 };
    double cur, s1L, s2L, s1R, s2R; public volatile int Step = -1;
    public StepShaperFx()
    {
        Type = "pxstep"; Title = "Purphex Step Shaper"; Category = "Purphex VST"; Blurb = "Sixteen steps of volume and filter movement in time with the song";
        float[] d = { 100, 25, 60, 10, 100, 40, 100, 20, 60, 25, 45, 100, 100, 100, 100, 100 };
        for (int k = 0; k < 16; k++) AddP("s" + k, "Step " + (k + 1), 0, 100, d[k], "%");
        AddC("grid", "Grid", Grids, 2); AddC("rate", "Rate", new[] { "Half", "Normal", "Double" }, 1); AddP("smooth", "Smooth", 0, 100, 20, "%"); AddP("swing", "Swing", 0, 75, 0, "%");
        AddP("vol", "Volume depth", 0, 100, 100, "%"); AddP("cut", "LP filter", 100, 20000, 20000, "Hz"); AddP("res", "Resonance", 0, 100, 10, "%"); AddC("follow", "Follow", new[] { "Off", "On" }, 0);
    }
    static readonly string[] SId = { "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9", "s10", "s11", "s12", "s13", "s14", "s15" };
    public override void Reset() { base.Reset(); cur = 1; s1L = s2L = s1R = s2R = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double stepBeats = GB[(int)Get("grid")] * RateMul[(int)Get("rate")];
        double sm = Get("smooth") / 100.0, sw = Get("swing") / 100.0, vol = Get("vol") / 100.0, cut = Get("cut"), k = 2 - 1.9 * Get("res") / 100.0; bool follow = Get("follow") >= 0.5f;
        double sc = 1 - Math.Exp(-1.0 / (Math.Max(0.0005, sm * 0.04) * SR));
        for (int i = 0; i < n; i++)
        {
            double beat = BeatAt(i), pos = beat / stepBeats, pair = Math.Floor(pos / 2), inPair = pos - pair * 2; int st;
            double split = 1 + sw; st = (int)(pair * 2) + (inPair < split ? 0 : 1); st = ((st % 16) + 16) % 16;
            double target = 1 - vol * (1 - Get(SId[st]) / 100.0); cur += (target - cur) * sc;
            double yl = l[i] * cur, yr = r[i] * cur;
            if (cut < 19999 || follow) { double fc = follow ? 100 + (cut - 100) * Math.Pow(Get(SId[st]) / 100.0, 2) : cut; double g = Math.Tan(Math.PI * Math.Min(SR * 0.45, Math.Max(40, fc)) / SR); yl = Lp(yl, g, k, ref s1L, ref s2L); yr = Lp(yr, g, k, ref s1R, ref s2R); }
            l[i] = (float)yl; r[i] = (float)yr; if (i == n - 1) Step = st;
        }
        EndBlock(n);
    }
    static readonly double[] RateMul = { 2, 1, 0.5 };
    static double Lp(double x, double g, double k, ref double s1, ref double s2) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return lp; }
}

class StepShaperWindow : PxWindow
{
    readonly StepShaperFx s;
    public StepShaperWindow(StepShaperFx f) : base(f, "Step Shaper", new Size(900, 600)) { s = f; Accent = Orange; }
    protected override string SubTitle { get { return "volume and filter steps"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var top = new RectangleF(a.X, a.Y, a.Width, 150); Panel(g, top, "", Orange);
        Knob(g, top.X + 70, top.Y + 64, 26, "smooth", "SMOOTH", Orange); Knob(g, top.X + 170, top.Y + 64, 26, "swing", "SWING", Orange);
        Txt(g, "GRID", 10, Dim, new RectangleF(top.X + 240, top.Y + 26, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(top.X + 240, top.Y + 44, 220, 24), "grid", Orange, 10);
        Txt(g, "RATE", 10, Dim, new RectangleF(top.X + 240, top.Y + 82, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(top.X + 240, top.Y + 100, 220, 24), "rate", Orange, 10);
        Knob(g, top.X + 530, top.Y + 64, 26, "vol", "VOLUME", Orange); Knob(g, top.X + 630, top.Y + 64, 26, "cut", "LP FILTER", Orange); Knob(g, top.X + 730, top.Y + 64, 26, "res", "RESONANCE", Orange);
        Toggle(g, new RectangleF(top.Right - 100, top.Y + 50, 80, 26), "follow", Pink, "FOLLOW", "FOLLOW");
        // the steps: click or drag each bar
        var st = new RectangleF(a.X, top.Bottom + 12, a.Width, a.Bottom - top.Bottom - 12); Fill(g, Color.FromArgb(24, 20, 30), st, 10); float bw = (st.Width - 20) / 16;
        for (int k = 0; k < 16; k++)
        {
            float v = fx.Get("s" + k) / 100f; var bar = new RectangleF(st.X + 10 + k * bw + 3, st.Y + 10, bw - 6, st.Height - 20); bool cur = s.Step == k;
            using (var pen = new Pen(cur ? Color.White : Color.FromArgb(220, 255, 90, 40), cur ? 3 : 2)) g.DrawRectangle(pen, bar.X, bar.Bottom - bar.Height * v, bar.Width, Math.Max(1, bar.Height * v));
            if (cur) using (var b = new SolidBrush(Color.FromArgb(60, 255, 120, 60))) g.FillRectangle(b, bar.X, bar.Bottom - bar.Height * v, bar.Width, bar.Height * v);
        }
        var h = Reg(st, null, "Click or drag across the bars to draw the steps"); h.DragAt = pt => { int k = (int)((pt.X - st.X - 10) / bw); if (k < 0 || k > 15) return; fx.Set("s" + k, Math.Max(0, Math.Min(100, (st.Bottom - 10 - pt.Y) / (st.Height - 20) * 100))); };
    }
}
