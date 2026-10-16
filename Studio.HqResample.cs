using System;

// ======================================================================
//  High-quality sample-rate conversion for sounds that are not at the studio rate (sound packs, dropped files, impulse responses).
//  A windowed-sinc filter (Blackman window, 32 zero crossings each side) instead of a straight line between two samples: a straight line
//  adds a gritty, metallic edge to the top end and lets high notes fold back down as whistles; this keeps them clean.
//  When going down in rate the filter cuts just below the new top frequency so nothing folds back.
// ======================================================================
static class HqResample
{
    const int Zc = 32, Res = 512;   // zero crossings each side, table steps between two of them
    static float[] table;
    static float[] Table()
    {
        if (table != null) return table; var t = new float[Zc * Res + 2];
        for (int i = 0; i < t.Length; i++)
        {
            double x = i / (double)Res; if (x >= Zc) { t[i] = 0; continue; }
            double s = x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x), w = 0.42 + 0.5 * Math.Cos(Math.PI * x / Zc) + 0.08 * Math.Cos(2 * Math.PI * x / Zc);
            t[i] = (float)(s * w);
        }
        return table = t;
    }
    public static float[] Run(float[] x, int from, int to)
    {
        if (x == null || from <= 0 || to <= 0 || from == to || x.Length == 0) return x;
        var tb = Table(); long n = (long)x.Length * to / from; var y = new float[n];
        double step = from / (double)to, sc = Math.Min(1.0, to / (double)from) * 0.98;   // sc < 1: the cut-off moves down for a lower rate
        double half = Zc / sc; int len = x.Length;
        for (long i = 0; i < n; i++)
        {
            double c = i * step; int k0 = (int)Math.Ceiling(c - half), k1 = (int)Math.Floor(c + half); double sum = 0, wsum = 0;
            for (int k = k0; k <= k1; k++)
            {
                double d = Math.Abs(c - k) * sc * Res; int di = (int)d; if (di >= Zc * Res) continue; float fr = (float)(d - di); double h = tb[di] + (tb[di + 1] - tb[di]) * fr;
                wsum += h; if (k >= 0 && k < len) sum += x[k] * h;
            }
            y[i] = (float)(wsum > 1e-9 ? sum / wsum : 0);   // dividing by the weights keeps the level exactly the same
        }
        return y;
    }
}
