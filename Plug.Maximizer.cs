using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX MAXIMIZER: one big LOUDNESS knob pushes the sound into a look-ahead limiter that never goes over the CEILING.
//  RELEASE sets how fast it lets go, CHARACTER: clean, warm (soft saturation first) or punch (the first peaks are clipped so the drums stay
//  sharp). TONE: low and high shelves. MONO BASS keeps the low end in the middle, WIDTH spreads or narrows the rest. TARGET turns the
//  loudness by itself towards a level (streaming -14, loud -11, club -9, max -7 LUFS); the meter shows the loudness now.
// ======================================================================
class MaximizerFx : Fx
{
    public static readonly string[] Targets = { "Free", "Streaming -14", "Loud -11", "Club -9", "Max -7" }; static readonly double[] TargetLufs = { 0, -14, -11, -9, -7 };
    const int Cap = 4096;
    readonly float[] dL = new float[Cap], dR = new float[Cap], avg = new float[Cap]; readonly double[] dqv = new double[Cap]; readonly long[] dqi = new long[Cap];
    int dqHead, dqTail; long m; double avgSum, env = 1, autoDb, lufsMs; int L, lastL = -1;
    double gain, ceil, relC; int chr, monoMode, target; double width;
    double ls1L, ls2L, ls1R, ls2R, hs1L, hs2L, hs1R, hs2R, mlpL, mlpR, mlp2L, mlp2R, kh1, kh2, kx1, kx2, ky1, ky2, kp1, kp2;
    double lsA, lsB, lsK, hsA, hsB, hsK, monoC; double kb0, kb1, kb2, ka1, ka2, kc0, kc1, kc2, kd1, kd2;
    public volatile float GRdb, InPk, OutPk, Lufs = -70, AutoGain;
    public MaximizerFx()
    {
        Type = "pxmax"; Title = "Purphex Maximizer"; Category = "Purphex VST"; Blurb = "One-knob loudness: limiter, character, tone, mono bass, width and LUFS targets";
        AddP("loud", "Loudness", 0, 100, 30, "%"); AddP("ceil", "Ceiling", -3, 0, -0.3f, "dB"); AddP("rel", "Release", 10, 1000, 120, "ms"); AddC("char", "Character", new[] { "Clean", "Warm", "Punch" }, 0);
        AddP("low", "Low", -6, 6, 0, "dB"); AddP("high", "High", -6, 6, 0, "dB"); AddC("mono", "Mono bass", new[] { "Off", "80 Hz", "120 Hz", "200 Hz" }, 0); AddP("width", "Width", 0, 200, 100, "%");
        AddC("target", "Target", Targets, 0);
    }
    public override int Latency { get { return Math.Max(8, Math.Min(Cap - 8, (int)(0.005 * SR))) - 1; } }   // worked out from the rate (not from L, which is only set once it plays)
    public override void Reset() { base.Reset(); Array.Clear(dL, 0, Cap); Array.Clear(dR, 0, Cap); Array.Clear(avg, 0, Cap); dqHead = dqTail = 0; m = 0; avgSum = 0; env = 1; lastL = -1; autoDb = 0; lufsMs = 0; ls1L = ls2L = ls1R = ls2R = hs1L = hs2L = hs1R = hs2R = mlpL = mlpR = mlp2L = mlp2R = 0; kh1 = kh2 = kx1 = kx2 = ky1 = ky2 = kp1 = kp2 = 0; }
    protected override void Update()
    {
        L = Math.Max(8, Math.Min(Cap - 8, (int)(0.005 * SR))); if (L != lastL) { for (int i = 0; i < Cap; i++) avg[i] = 1; avgSum = L; lastL = L; }   // the smoothing starts at full level (no silent start)
        gain = Math.Pow(10, Get("loud") * 0.18 / 20); ceil = Math.Pow(10, Get("ceil") / 20); relC = 1 - Math.Exp(-1.0 / (Get("rel") * 0.001 * SR)); chr = (int)Get("char");
        monoMode = (int)Get("mono"); double mf = monoMode == 1 ? 80 : monoMode == 2 ? 120 : 200; monoC = Math.Tan(Math.PI * mf / SR); width = Get("width") / 100.0; target = (int)Get("target");
        // tone: low shelf at 100 Hz, high shelf at 8 kHz (state variable shelves)
        lsA = Math.Pow(10, Get("low") / 20); lsK = Math.Tan(Math.PI * 100 / SR); hsA = Math.Pow(10, Get("high") / 20); hsK = Math.Tan(Math.PI * Math.Min(SR * 0.45, 8000) / SR);
        // K-weighting for the loudness meter (high shelf +4 dB around 1.7 kHz, then a 38 Hz high-pass), as biquads
        { double f0 = 1681.97, G = 3.99984, Q = 0.7071752, K = Math.Tan(Math.PI * f0 / SR), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416), a0 = 1 + K / Q + K * K;
          kb0 = (Vh + Vb * K / Q + K * K) / a0; kb1 = 2 * (K * K - Vh) / a0; kb2 = (Vh - Vb * K / Q + K * K) / a0; ka1 = 2 * (K * K - 1) / a0; ka2 = (1 - K / Q + K * K) / a0; }
        { double f0 = 38.13547, Q = 0.5003270, K = Math.Tan(Math.PI * f0 / SR), a0 = 1 + K / Q + K * K; kc0 = 1 / a0; kc1 = -2 / a0; kc2 = 1 / a0; kd1 = 2 * (K * K - 1) / a0; kd2 = (1 - K / Q + K * K) / a0; }
    }
    static double SvfHp(double x, double g, ref double s1, ref double s2) { const double k = 1.41421356; double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return hp; }
    static double Shelf(double x, double k, double a, bool low, ref double s1, ref double s2)
    {   // a 2-pole low-pass split: low part times a (low shelf) or high part times a (high shelf)
        double g = k, R = 0.7071; double hp = (x - (2 * R + g) * s1 - s2) / (1 + 2 * R * g + g * g); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2;
        return low ? x + (a - 1) * lp : x + (a - 1) * (x - lp);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double tgt = TargetLufs[target]; double msC = 1 - Math.Exp(-1.0 / (0.4 * SR)); float ip = 0, op = 0, grMax = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i]; ip = (float)Math.Max(ip, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            if (lsA != 1) { xl = Shelf(xl, lsK, lsA, true, ref ls1L, ref ls2L); xr = Shelf(xr, lsK, lsA, true, ref ls1R, ref ls2R); }
            if (hsA != 1) { xl = Shelf(xl, hsK, hsA, false, ref hs1L, ref hs2L); xr = Shelf(xr, hsK, hsA, false, ref hs1R, ref hs2R); }
            // width and mono bass on mid / side
            double mid = (xl + xr) * 0.5, side = (xl - xr) * 0.5;
            if (monoMode > 0) { side = SvfHp(side, monoC, ref mlpL, ref mlpR); side = SvfHp(side, monoC, ref mlp2L, ref mlp2R); }
            side *= width; xl = mid + side; xr = mid - side;
            double gIn = gain * Math.Pow(10, autoDb / 20); xl *= gIn; xr *= gIn;
            if (chr == 1) { xl = Math.Tanh(xl * 0.9 + 0.05) / 0.9 - Math.Tanh(0.05) / 0.9; xr = Math.Tanh(xr * 0.9 + 0.05) / 0.9 - Math.Tanh(0.05) / 0.9; }
            else if (chr == 2) { double t = ceil * 1.6; xl = Math.Abs(xl) > t ? Math.Sign(xl) * (t + Math.Tanh(Math.Abs(xl) - t) * 0.2) : xl; xr = Math.Abs(xr) > t ? Math.Sign(xr) * (t + Math.Tanh(Math.Abs(xr) - t) * 0.2) : xr; }
            // the look-ahead limiter: the gain needed for this sample, the lowest of the next L samples (a running minimum), released slowly, then averaged over L so it bends smoothly
            double pk = Math.Max(Math.Abs(xl), Math.Abs(xr)), req = pk > ceil ? ceil / pk : 1;
            while (dqTail != dqHead && dqv[(dqTail - 1 + Cap) % Cap] >= req) dqTail = (dqTail - 1 + Cap) % Cap;
            dqv[dqTail] = req; dqi[dqTail] = m; dqTail = (dqTail + 1) % Cap; while (dqi[dqHead] <= m - L) dqHead = (dqHead + 1) % Cap;
            double h = dqv[dqHead]; env = h < env ? h : env + (h - env) * relC;
            int ai = (int)(m % L); avgSum += env - avg[ai]; avg[ai] = (float)env; double gl = avgSum / L;
            int wi = (int)(m % Cap), ri = (int)((m - (L - 1) + Cap * 1024L) % Cap); dL[wi] = (float)xl; dR[wi] = (float)xr; m++;
            double yl = dL[ri] * gl, yr = dR[ri] * gl;
            if (yl > ceil) yl = ceil; else if (yl < -ceil) yl = -ceil; if (yr > ceil) yr = ceil; else if (yr < -ceil) yr = -ceil;   // a last safety net: never over the ceiling
            l[i] = (float)yl; r[i] = (float)yr; op = (float)Math.Max(op, Math.Max(Math.Abs(yl), Math.Abs(yr))); grMax = (float)Math.Max(grMax, -20 * Math.Log10(Math.Max(1e-5, gl)));
            // momentary loudness (K-weighted, 400 ms) of what comes out
            double km = (yl + yr) * 0.5, k1 = kb0 * km + kb1 * kx1 + kb2 * kx2 - ka1 * ky1 - ka2 * ky2; kx2 = kx1; kx1 = km; ky2 = ky1; ky1 = k1;
            double k2 = kc0 * k1 + kc1 * kp1 + kc2 * kp2 - kd1 * kh1 - kd2 * kh2; kp2 = kp1; kp1 = k1; kh2 = kh1; kh1 = k2; lufsMs += (k2 * k2 * 2 - lufsMs) * msC;
            if (target > 0)
            {   // TARGET: the loudness is turned slowly (about 2 dB a second at most) towards the level picked
                double now = -0.691 + 10 * Math.Log10(lufsMs + 1e-12); if (now > -50) autoDb += Math.Max(-1, Math.Min(1, (tgt - now) * 0.5)) * 2.0 / SR; autoDb = Math.Max(-12, Math.Min(12, autoDb));
            }
        }
        if (avgSum < 0) avgSum = 0;
        InPk = Math.Max(InPk * 0.9f, ip); OutPk = Math.Max(OutPk * 0.9f, op); GRdb = grMax > GRdb ? grMax : GRdb * 0.85f; Lufs = (float)(-0.691 + 10 * Math.Log10(lufsMs + 1e-12)); AutoGain = (float)autoDb;
    }
}

class MaximizerWindow : PxWindow
{
    readonly MaximizerFx mx;
    public MaximizerWindow(MaximizerFx f) : base(f, "Maximizer", new Size(900, 620)) { mx = f; Accent = Gold; }
    protected override string SubTitle { get { return "one-knob loudness"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var lp = new RectangleF(a.X, a.Y, 230, a.Height); Panel(g, lp, "TONE", Cyan);
        Knob(g, lp.X + 60, lp.Y + 90, 26, "low", "LOW", Cyan); Knob(g, lp.X + 170, lp.Y + 90, 26, "high", "HIGH", Cyan);
        Txt(g, "MONO BASS", 10, Dim, new RectangleF(lp.X + 16, lp.Y + 170, 120, 14), StringAlignment.Near, true); Choice(g, new RectangleF(lp.X + 16, lp.Y + 188, lp.Width - 32, 24), "mono", Cyan, 10.5f);
        Knob(g, lp.X + lp.Width / 2, lp.Y + 290, 30, "width", "WIDTH", Cyan);
        Txt(g, "CHARACTER", 10, Dim, new RectangleF(lp.X + 16, lp.Bottom - 70, 120, 14), StringAlignment.Near, true); Segments(g, new RectangleF(lp.X + 16, lp.Bottom - 52, lp.Width - 32, 26), "char", Gold, 10);
        // the big knob with the gain reduction as a ring round it
        var c = new RectangleF(lp.Right + 10, a.Y, a.Width - 480, a.Height); Panel(g, c, "", Gold); float cx = c.X + c.Width / 2, cy = c.Y + 190;
        float gr = Math.Min(1, mx.GRdb / 12f); using (var pen = new Pen(Color.FromArgb(60, Pink), 10)) g.DrawArc(pen, cx - 118, cy - 118, 236, 236, 135, 270); using (var pen = new Pen(Pink, 10)) if (gr > 0.005f) g.DrawArc(pen, cx - 118, cy - 118, 236, 236, 135, 270 * gr);
        Knob(g, cx, cy, 90, "loud", "LOUDNESS", Gold, true);
        Txt(g, "GAIN REDUCTION  " + mx.GRdb.ToString("0.0") + " dB", 11, Pink, new RectangleF(c.X + 10, c.Y + 12, c.Width - 20, 16), StringAlignment.Center, true);
        Knob(g, c.X + c.Width * 0.3f, c.Bottom - 80, 24, "ceil", "CEILING", Ink); Knob(g, c.X + c.Width * 0.7f, c.Bottom - 80, 24, "rel", "RELEASE", Ink);
        // loudness
        var rp = new RectangleF(c.Right + 10, a.Y, a.Right - c.Right - 10, a.Height); Panel(g, rp, "LOUDNESS", Lime);
        Txt(g, mx.Lufs < -60 ? "-inf" : mx.Lufs.ToString("0.0"), 34, Lime, new RectangleF(rp.X, rp.Y + 34, rp.Width, 46), StringAlignment.Center, true); Txt(g, "LUFS (momentary)", 10, Dim, new RectangleF(rp.X, rp.Y + 80, rp.Width, 14), StringAlignment.Center, false);
        Txt(g, "TARGET", 10, Dim, new RectangleF(rp.X + 16, rp.Y + 110, 120, 14), StringAlignment.Near, true); Choice(g, new RectangleF(rp.X + 16, rp.Y + 128, rp.Width - 32, 24), "target", Lime, 10.5f);
        Txt(g, fx.Get("target") >= 0.5f ? "auto gain " + (mx.AutoGain >= 0 ? "+" : "") + mx.AutoGain.ToString("0.0") + " dB" : "loudness set by hand", 10, Dim, new RectangleF(rp.X + 16, rp.Y + 156, rp.Width - 32, 14), StringAlignment.Near, false);
        Meter(g, new RectangleF(rp.X + 40, rp.Y + 190, 16, rp.Height - 220), mx.InPk, "IN"); Meter(g, new RectangleF(rp.X + rp.Width / 2 - 8, rp.Y + 190, 16, rp.Height - 220), mx.GRdb, "GR", true); Meter(g, new RectangleF(rp.Right - 56, rp.Y + 190, 16, rp.Height - 220), mx.OutPk, "OUT");
    }
}

// ======================================================================
//  PURPHEX SPECTRAL GATE: a gate that works on every frequency on its own, so spill (other drums in a mic, hiss, hum, room) is turned down
//  where it sits while the wanted sound passes. LEARN SPILL listens to the unwanted sound alone (play a quiet part) and LEARN WANTED to the
//  wanted one; with spill learned each frequency opens MARGIN dB above it, with both learned the line sits between them (BIAS).
//  Nothing learned: one THRESHOLD for every frequency (drag the line). REDUCTION, ATTACK, RELEASE, SMOOTH (across frequencies), a
//  RANGE (low and high edge: outside it nothing is gated), LISTEN to what is removed, MIX and OUTPUT.
// ======================================================================
class SpectralGateFx : Fx
{
    const int N = 2048, H = 512, NB = 32, K = N / 2 + 1;
    readonly double[] win = new double[N], reL = new double[N], imL = new double[N], reR = new double[N], imR = new double[N], gain = new double[K], gsm = new double[K], mag = new double[K];
    readonly float[] inL = new float[N], inR = new float[N], outL = new float[N * 2], outR = new float[N * 2], dryL = new float[N * 2], dryR = new float[N * 2]; int inW, hop, outRd;
    readonly int[] bandOf = new int[K]; readonly double[] bandMag = new double[NB]; readonly int[] bandCnt = new int[NB];
    readonly Param[] sp = new Param[NB], wp = new Param[NB];
    public readonly float[] ShowIn = new float[256], ShowGain = new float[256]; public static readonly int Bands = NB;
    double thrDb, margin, bias, redG, attC, relC, smooth, loHz, hiHz, mixW, outG; int learn, listen;
    public SpectralGateFx()
    {
        Type = "pxsgate"; Title = "Purphex Spectral Gate"; Category = "Purphex VST"; Blurb = "Gates every frequency on its own: learns the spill and the wanted sound";
        AddP("thr", "Threshold", -80, 0, -45, "dB"); AddP("margin", "Margin", 0, 24, 6, "dB"); AddP("bias", "Bias", 0, 100, 30, "%"); AddP("red", "Reduction", 0, 60, 30, "dB");
        AddP("att", "Attack", 1, 100, 5, "ms"); AddP("rel", "Release", 10, 1000, 120, "ms"); AddP("smooth", "Smooth", 0, 100, 30, "%");
        AddP("lo", "Range low", 20, 2000, 20, "Hz"); AddP("hi", "Range high", 1000, 20000, 20000, "Hz");
        AddC("learn", "Learn", new[] { "Off", "Learn spill", "Learn wanted" }, 0); AddC("listen", "Listen", new[] { "Result", "Removed" }, 0);
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
        var hid = new System.Text.StringBuilder();
        for (int b = 0; b < NB; b++) { sp[b] = AddP("sp" + b, "Spill " + (b + 1), -120, 0, -120, "dB"); wp[b] = AddP("wp" + b, "Wanted " + (b + 1), -120, 0, -120, "dB"); hid.Append((b > 0 ? "," : "") + "sp" + b + ",wp" + b); }
        Hidden = hid.ToString();
        for (int i = 0; i < N; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N);
        for (int k = 0; k < K; k++) gsm[k] = 1;
    }
    public override int Latency { get { return N - 1; } }
    public bool SpillLearned { get { return sp[0].Val > -119.5f; } } public bool WantedLearned { get { return wp[0].Val > -119.5f; } }
    public float SpillDb(int b) { return sp[b].Val; } public float WantedDb(int b) { return wp[b].Val; }
    public void ClearLearned() { for (int b = 0; b < NB; b++) { sp[b].Val = -120; wp[b].Val = -120; } dirty = true; }
    public static double BandHz(int b) { return 20 * Math.Pow(1000, (b + 0.5) / NB); }
    public override void Reset() { base.Reset(); Array.Clear(inL, 0, N); Array.Clear(inR, 0, N); Array.Clear(outL, 0, outL.Length); Array.Clear(outR, 0, outR.Length); Array.Clear(dryL, 0, dryL.Length); Array.Clear(dryR, 0, dryR.Length); inW = hop = outRd = 0; for (int k = 0; k < K; k++) gsm[k] = 1; }
    protected override void Update()
    {
        thrDb = Get("thr"); margin = Get("margin"); bias = Get("bias") / 100.0; redG = Math.Pow(10, -Get("red") / 20); smooth = Get("smooth") / 100.0;
        attC = Math.Exp(-H / (Get("att") * 0.001 * SR)); relC = Math.Exp(-H / (Get("rel") * 0.001 * SR)); loHz = Get("lo"); hiHz = Math.Max(loHz * 1.5, Get("hi"));
        learn = (int)Get("learn"); listen = (int)Get("listen"); mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
        for (int k = 0; k < K; k++) { double hz = Math.Max(20, k * (double)SR / N); bandOf[k] = (int)Math.Max(0, Math.Min(NB - 1, Math.Floor(Math.Log(hz / 20) / Math.Log(1000) * NB))); }
    }
    void Frame()
    {
        for (int i = 0; i < N; i++) { double w = win[i]; reL[i] = inL[(inW + i) % N] * w; imL[i] = 0; reR[i] = inR[(inW + i) % N] * w; imR[i] = 0; }
        Fft.Transform(reL, imL, false); Fft.Transform(reR, imR, false);
        Array.Clear(bandMag, 0, NB); Array.Clear(bandCnt, 0, NB);
        for (int k = 0; k < K; k++) { mag[k] = (Math.Sqrt(reL[k] * reL[k] + imL[k] * imL[k]) + Math.Sqrt(reR[k] * reR[k] + imR[k] * imR[k])) * 0.5 / (N / 4.0); bandMag[bandOf[k]] += mag[k] * mag[k]; bandCnt[bandOf[k]]++; }
        if (learn > 0)
        {   // learning: each band's level is averaged into the profile (the sound passes untouched meanwhile)
            var prof = learn == 1 ? sp : wp;
            for (int b = 0; b < NB; b++) { if (bandCnt[b] == 0) continue; double db = 10 * Math.Log10(bandMag[b] / bandCnt[b] + 1e-14); double v = prof[b].Val < -119.5f ? db : prof[b].Val * 0.92 + db * 0.08; prof[b].Val = (float)Math.Max(-119, Math.Min(0, v)); }
            for (int k = 0; k < K; k++) gain[k] = 1;
        }
        else
        {
            bool sl = SpillLearned, wl = WantedLearned;
            for (int k = 0; k < K; k++)
            {
                double hz = k * (double)SR / N; if ((loHz > 20.5 && hz < loHz) || (hiHz < 19999 && hz > hiHz)) { gain[k] = 1; continue; }   // a range knob at its end means no limit on that side
                int b = bandOf[k]; double t = sl ? (wl ? sp[b].Val + (wp[b].Val - sp[b].Val) * bias : sp[b].Val + margin) : thrDb;
                gain[k] = 20 * Math.Log10(mag[k] + 1e-12) >= t ? 1 : redG;
            }
        }
        if (smooth > 0) for (int pass = 0; pass < 2; pass++) { double prev = gain[0]; for (int k = 1; k < K - 1; k++) { double cur = gain[k]; gain[k] = cur * (1 - smooth * 0.66) + (prev + gain[k + 1]) * smooth * 0.33; prev = cur; } }
        for (int k = 0; k < K; k++) { double t = gain[k]; gsm[k] = t > gsm[k] ? t + (gsm[k] - t) * attC : t + (gsm[k] - t) * relC; double g = listen == 1 ? 1 - gsm[k] : gsm[k]; reL[k] *= g; imL[k] *= g; reR[k] *= g; imR[k] *= g; if (k > 0 && k < N / 2) { reL[N - k] = reL[k]; imL[N - k] = -imL[k]; reR[N - k] = reR[k]; imR[N - k] = -imR[k]; } }
        Fft.Transform(reL, imL, true); Fft.Transform(reR, imR, true);
        for (int i = 0; i < N; i++) { int o = (outRd + i) % (N * 2); outL[o] += (float)(reL[i] * win[i] / 1.5); outR[o] += (float)(reR[i] * win[i] / 1.5); }
        for (int j = 0; j < 256; j++) { int k = (int)Math.Min(K - 1, 20 * Math.Pow(1000, j / 255.0) / SR * N); ShowIn[j] = (float)(20 * Math.Log10(mag[k] + 1e-12)); ShowGain[j] = (float)gsm[k]; }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            inL[inW] = l[i]; inR[inW] = r[i]; inW = (inW + 1) % N;
            int dw = (outRd + N - 1) % (N * 2); dryL[dw] = l[i]; dryR[dw] = r[i];   // the dry sound, delayed by the same amount as the processed one
            if (++hop >= H) { hop = 0; Frame(); }
            double y1 = outL[outRd], y2 = outR[outRd], d1 = dryL[outRd], d2 = dryR[outRd]; outL[outRd] = 0; outR[outRd] = 0; outRd = (outRd + 1) % (N * 2);
            if (learn > 0) { y1 = d1; y2 = d2; }
            l[i] = (float)((d1 + (y1 - d1) * mixW) * outG); r[i] = (float)((d2 + (y2 - d2) * mixW) * outG);
        }
    }
}

class SpectralGateWindow : PxWindow
{
    readonly SpectralGateFx s;
    public SpectralGateWindow(SpectralGateFx f) : base(f, "Spectral Gate", new Size(940, 640)) { s = f; Accent = Lime; }
    protected override string SubTitle { get { return "learns the spill, gates every frequency"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var sp = new RectangleF(a.X, a.Y, a.Width, 270); Fill(g, Well, sp, 10); float x0 = sp.X + 10, w = sp.Width - 20, y0 = sp.Y + 10, h = sp.Height - 20;
        Func<double, float> yOf = db => y0 + h - (float)Math.Max(0, Math.Min(1, (db + 100) / 100)) * h;
        using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255))) for (int d = -80; d <= -20; d += 20) g.DrawLine(pen, x0, yOf(d), x0 + w, yOf(d));
        // what was taken away (shaded), the sound, and the lines
        var pts = new PointF[256]; var gpts = new PointF[258];
        for (int j = 0; j < 256; j++) { float x = x0 + j * w / 255f; pts[j] = new PointF(x, yOf(s.ShowIn[j])); gpts[j + 1] = new PointF(x, y0 + (1 - s.ShowGain[j]) * h * 0.35f); }
        gpts[0] = new PointF(x0, y0); gpts[257] = new PointF(x0 + w, y0); using (var b = new SolidBrush(Color.FromArgb(70, Pink))) g.FillPolygon(b, gpts);
        using (var pen = new Pen(Color.FromArgb(230, Ink), 1.5f)) g.DrawLines(pen, pts);
        Func<int, float> bx = b => x0 + (float)(Math.Log(SpectralGateFx.BandHz(b) / 20) / Math.Log(1000)) * w;
        if (s.SpillLearned) { var lp = new PointF[SpectralGateFx.Bands]; for (int b = 0; b < lp.Length; b++) lp[b] = new PointF(bx(b), yOf(s.SpillDb(b))); using (var pen = new Pen(Orange, 2)) g.DrawLines(pen, lp); }
        if (s.WantedLearned) { var lp = new PointF[SpectralGateFx.Bands]; for (int b = 0; b < lp.Length; b++) lp[b] = new PointF(bx(b), yOf(s.WantedDb(b))); using (var pen = new Pen(Cyan, 2)) g.DrawLines(pen, lp); }
        if (!s.SpillLearned)
        {   // the one threshold line: drag it up or down
            float ty = yOf(fx.Get("thr")); using (var pen = new Pen(Lime, 2) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, x0, ty, x0 + w, ty);
            var hh = Reg(new RectangleF(x0, ty - 8, w, 16), null, "Drag the threshold up or down"); hh.P = fx.Find("thr"); hh.DragAt = pt => fx.Set("thr", (float)((1 - (pt.Y - y0) / h) * 100 - 100));
        }
        Txt(g, s.SpillLearned ? (s.WantedLearned ? "orange: spill   cyan: wanted   (the line sits between them: BIAS)" : "orange: the learned spill   (it opens MARGIN above it)") : "nothing learned: one threshold for every frequency (drag the green line)", 10, Dim, new RectangleF(x0 + 6, y0 + 2, w - 12, 14), StringAlignment.Near, false);
        // learning
        float by = sp.Bottom + 10; var lr = new RectangleF(a.X, by, 250, a.Bottom - by); Panel(g, lr, "LEARN", Orange);
        int ln = (int)fx.Get("learn");
        Button(g, new RectangleF(lr.X + 16, lr.Y + 34, lr.Width - 32, 34), ln == 1 ? "LISTENING TO SPILL... STOP" : "LEARN SPILL", ln == 1, Orange, () => { Mark(); fx.Set("learn", ln == 1 ? 0 : 1); }, "Play a part with only the unwanted sound, press, then press again to stop", 11);
        Button(g, new RectangleF(lr.X + 16, lr.Y + 76, lr.Width - 32, 34), ln == 2 ? "LISTENING TO WANTED... STOP" : "LEARN WANTED", ln == 2, Cyan, () => { Mark(); fx.Set("learn", ln == 2 ? 0 : 2); }, "Play a part with the wanted sound, press, then press again to stop", 11);
        Button(g, new RectangleF(lr.X + 16, lr.Y + 118, lr.Width - 32, 30), "CLEAR LEARNED", false, Violet, () => { Mark(); s.ClearLearned(); }, "Forget what was learned (back to one threshold)", 10.5f);
        Knob(g, lr.X + 70, lr.Y + 200, 20, "margin", "MARGIN", Orange); Knob(g, lr.X + 180, lr.Y + 200, 20, "bias", "BIAS", Cyan);
        // gate
        var gp = new RectangleF(lr.Right + 10, by, a.Width - 520, a.Bottom - by); Panel(g, gp, "GATE", Lime); float gy = gp.Y + 80;
        Knob(g, gp.X + gp.Width * 0.18f, gy, 22, "thr", "THRESHOLD", Lime); Knob(g, gp.X + gp.Width * 0.5f, gy, 22, "red", "REDUCTION", Lime); Knob(g, gp.X + gp.Width * 0.82f, gy, 22, "smooth", "SMOOTH", Lime);
        Knob(g, gp.X + gp.Width * 0.18f, gy + 110, 20, "att", "ATTACK", Lime); Knob(g, gp.X + gp.Width * 0.5f, gy + 110, 20, "rel", "RELEASE", Lime);
        Txt(g, "LISTEN", 10, Dim, new RectangleF(gp.X + gp.Width * 0.66f, gy + 78, 90, 14), StringAlignment.Near, true); Segments(g, new RectangleF(gp.X + gp.Width * 0.66f, gy + 96, gp.Width * 0.3f, 24), "listen", Pink, 9.5f);
        var rp = new RectangleF(gp.Right + 10, by, a.Right - gp.Right - 10, a.Bottom - by); Panel(g, rp, "RANGE", Violet);
        Knob(g, rp.X + rp.Width * 0.28f, rp.Y + 80, 20, "lo", "LOW", Violet); Knob(g, rp.X + rp.Width * 0.72f, rp.Y + 80, 20, "hi", "HIGH", Violet);
        Knob(g, rp.X + rp.Width * 0.28f, rp.Y + 190, 20, "mix", "MIX", Ink); Knob(g, rp.X + rp.Width * 0.72f, rp.Y + 190, 20, "out", "OUTPUT", Ink);
    }
}
