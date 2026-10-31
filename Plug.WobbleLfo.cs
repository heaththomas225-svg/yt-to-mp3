using System;

// ======================================================================
//  PURPHEX WOBBLE LFO: an effect for any track (made for bass) that makes it wobble. An LFO, locked to the song's tempo (1/1 down to 1/32,
//  triplets and dotted) or free, opens and closes a resonant low-pass filter between FILTER LOW and FILTER HIGH, as deep as DEPTH says.
//  SHAPE: sine (smooth wub), triangle, saw down (yoy), saw up, square (chop) or random steps. VOLUME WOBBLE moves the level with it too.
//  START POINT moves where in the bar the wobble begins; MIX blends the wobbled sound with the dry one; OUTPUT sets the level.
//  While the song plays, the wobble follows the song's position, so it is on the beat every time (stopped, it runs freely).
// ======================================================================
class WobbleLfoFx : Fx
{
    public static readonly string[] Rates = { "1/1", "1/2", "1/4", "1/8", "1/16", "1/32", "1/4 triplet", "1/8 triplet", "1/16 triplet", "1/4 dotted", "1/8 dotted", "Free (Hz)" };
    static readonly double[] RateBeats = { 4, 2, 1, 0.5, 0.25, 0.125, 2 / 3.0, 1 / 3.0, 1 / 6.0, 1.5, 0.75, 0 };
    public static readonly string[] Shapes = { "Sine", "Triangle", "Saw down", "Saw up", "Square", "Random steps" };
    double ph, uS = 1, ic1L, ic2L, ic1R, ic2R, a1, a2, a3, k, gOut = 1, lvl; long blockPos; bool transport; int coefLeft; uint seed = 12345; double stepVal = 1; int lastStep = -1;
    public volatile float Phase;   // where the wobble is now (0..1), for the display
    public WobbleLfoFx()
    {
        Type = "pxwobblelfo"; Title = "Purphex Wobble LFO"; Category = "Purphex VST"; Blurb = "Makes any sound wobble (made for bass): a tempo-synced LFO opens and closes a filter, and the volume if you like, as much as you want";
        AddC("rate", "Rate", Rates, 3); AddP("free", "Free rate", 0.1f, 20, 2, "Hz"); AddC("shape", "Shape", Shapes, 0);
        AddP("depth", "Depth", 0, 100, 80, "%"); AddP("low", "Filter low", 40, 2000, 140, "Hz"); AddP("high", "Filter high", 300, 18000, 5000, "Hz"); AddP("res", "Resonance", 0, 100, 45, "%");
        AddP("vol", "Volume wobble", 0, 100, 0, "%"); AddP("phase", "Start point", 0, 360, 0, "deg"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); ic1L = ic2L = ic1R = ic2R = 0; uS = 1; coefLeft = 0; lastStep = -1; }
    public override void BeginBlock(object track, long pos, int n, bool transport) { base.BeginBlock(track, pos, n, transport); blockPos = pos; this.transport = transport; }
    // one cycle of the wobble's shape: 1 = filter fully open, 0 = closed
    public double ShapeAt(double p, int shape)
    {
        p -= Math.Floor(p);
        switch (shape)
        {
            case 1: return p < 0.5 ? p * 2 : 2 - p * 2;
            case 2: return 1 - p;
            case 3: return p;
            case 4: return p < 0.5 ? 1 : 0;
            case 5: return stepVal;
            default: return 0.5 + 0.5 * Math.Cos(2 * Math.PI * p);   // (starts open, like a wub)
        }
    }
    // the length of one wobble in samples (tempo-synced) or 0 for free
    double CycleFrames() { int ri = (int)Math.Round(Get("rate")); double b = RateBeats[Math.Max(0, Math.Min(RateBeats.Length - 1, ri))]; return b <= 0 ? 0 : b * 60.0 / Math.Max(20, Bpm) * SR; }
    public string RateText { get { int ri = (int)Math.Round(Get("rate")); return ri >= RateBeats.Length - 1 ? Get("free").ToString("0.00") + " Hz" : Rates[Math.Max(0, ri)]; } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double depth = Get("depth") / 100.0, lo = Get("low"), hi = Math.Max(lo + 10, Get("high")), res = Get("res") / 100.0, vol = Get("vol") / 100.0, mix = Get("mix") / 100.0, off = Get("phase") / 360.0;
        int shape = (int)Math.Round(Get("shape")); double cyc = CycleFrames(), inc = cyc > 0 ? 1.0 / cyc : Get("free") / SR; double outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR)), us = 1 - Math.Exp(-1.0 / (0.002 * SR));
        k = 2 - 1.96 * res; double lvA = 1 - Math.Exp(-1.0 / (0.3 * SR)); double act = 0;
        for (int i = 0; i < n; i++)
        {
            // where in the wobble: from the song's position while it plays (on the beat every time), else free running
            double p = transport && cyc > 0 ? (blockPos + i) / cyc + off : ph + off; if (!(transport && cyc > 0)) { ph += inc; if (ph > 1e6) ph -= Math.Floor(ph); }
            if (shape == 5) { int st = (int)Math.Floor(p); if (st != lastStep) { lastStep = st; seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; stepVal = (seed & 0xFFFF) / 65535.0; } }
            double u = ShapeAt(p, shape); uS += (u - uS) * us;   // (smoothed a little: a square or a step never clicks)
            double open = 1 - depth * (1 - uS);
            if (--coefLeft <= 0)
            {   // the filter follows the wobble (worked out every 16 samples: cheap and smooth enough)
                coefLeft = 16; double fc = lo * Math.Pow(hi / lo, open); fc = Math.Min(fc, SR * 0.45); double g = Math.Tan(Math.PI * fc / SR); a1 = 1 / (1 + g * (g + k)); a2 = g * a1; a3 = g * a2;
            }
            double xl = l[i], xr = r[i];
            double v3 = xl - ic2L, v1 = a1 * ic1L + a2 * v3, v2 = ic2L + a2 * ic1L + a3 * v3; ic1L = 2 * v1 - ic1L; ic2L = 2 * v2 - ic2L; double yl = v2;
            v3 = xr - ic2R; v1 = a1 * ic1R + a2 * v3; v2 = ic2R + a2 * ic1R + a3 * v3; ic1R = 2 * v1 - ic1R; ic2R = 2 * v2 - ic2R; double yr = v2;
            double vg = 1 - vol * (1 - uS); yl *= vg; yr *= vg;
            gOut += (outT - gOut) * gs; double ol = (xl + (yl - xl) * mix) * gOut, or = (xr + (yr - xr) * mix) * gOut;
            if (double.IsNaN(ol) || double.IsNaN(or) || Math.Abs(ol) > 1e4) { Reset(); ol = xl; or = xr; }
            act = Math.Max(act, Math.Abs(ol - xl)); lvl += (Math.Abs((ol + or) * 0.5) - lvl) * lvA; l[i] = (float)ol; r[i] = (float)or;
        }
        double pNow = transport && cyc > 0 ? (blockPos + n) / cyc + off : ph + off; Phase = (float)(pNow - Math.Floor(pNow));
        Act = (float)Math.Min(1, act * 2); OutLvl = (float)lvl;
    }
}
