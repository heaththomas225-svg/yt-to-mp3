using System;
using System.Collections.Generic;

// A high quality sample rate converter for exports (44.1 kHz up to 384 kHz): a polyphase filter with a Kaiser window (about 90 dB of rejection), working on blocks so memory stays flat.
// It only ever raises the rate: the music is made at 44.1 kHz, so a higher rate holds the same sound in a bigger file (some studios and players ask for it).
sealed class RatioResampler
{
    const int H = 32;                          // taps on each side of the point being worked out
    readonly int up, down; readonly float[][] tab; float[] bl = new float[8192], br = new float[8192]; int have; long inBase, m, totalIn;
    public int Up { get { return up; } } public int Down { get { return down; } }

    static double Bessel0(double x) { double s = 1, t = 1; for (int k = 1; k < 40; k++) { t *= (x / (2 * k)) * (x / (2 * k)); s += t; if (t < 1e-12 * s) break; } return s; }
    public static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

    public RatioResampler(int fromRate, int toRate)
    {
        int g = Gcd(fromRate, toRate); up = toRate / g; down = fromRate / g; tab = new float[up][]; const double beta = 9.0; double b0 = Bessel0(beta);
        for (int p = 0; p < up; p++)
        {
            var t = new double[2 * H]; double sum = 0;
            for (int k = -H + 1; k <= H; k++)
            {
                double tau = k - p / (double)up, a = Math.Abs(tau) / H; double sinc = Math.Abs(tau) < 1e-9 ? 1.0 : Math.Sin(Math.PI * tau) / (Math.PI * tau); double w = a >= 1 ? 0 : Bessel0(beta * Math.Sqrt(1 - a * a)) / b0;
                t[k + H - 1] = sinc * w; sum += t[k + H - 1];
            }
            var f = new float[2 * H]; for (int i = 0; i < f.Length; i++) f[i] = (float)(t[i] / sum); tab[p] = f;
        }
        inBase = -H; have = H;   // the music is preceded by silence
    }
    void Ensure(int extra) { if (have + extra > bl.Length) { int n = Math.Max(bl.Length * 2, have + extra + 16); Array.Resize(ref bl, n); Array.Resize(ref br, n); } }
    // feeds n samples and writes every output sample that can be worked out; returns how many were written (oL and oR must hold at least n * up / down + 8)
    public int Process(float[] l, float[] r, int off, int n, float[] oL, float[] oR)
    {
        Ensure(n); Array.Copy(l, off, bl, have, n); Array.Copy(r, off, br, have, n); have += n; totalIn += n; return Run(oL, oR, long.MaxValue);
    }
    int Run(float[] oL, float[] oR, long limit)
    {
        int cnt = 0;
        while (m < limit)
        {
            long num = m * down; long i0 = num / up; int p = (int)(num % up); long need = i0 + H; if (need >= inBase + have) break;
            int s = (int)(i0 - H + 1 - inBase); var f = tab[p]; float al = 0, ar = 0; for (int k = 0; k < 2 * H; k++) { float c = f[k]; al += bl[s + k] * c; ar += br[s + k] * c; }
            oL[cnt] = al; oR[cnt] = ar; cnt++; m++;
        }
        // drop what is no longer needed
        long keepFrom = (m * down) / up - H + 1; int drop = (int)(keepFrom - inBase); if (drop > 4096) { Array.Copy(bl, drop, bl, 0, have - drop); Array.Copy(br, drop, br, 0, have - drop); have -= drop; inBase += drop; }
        return cnt;
    }
    // the end of the music: the filter's tail, so the last samples are right (oL and oR must hold up to 2 * H * up / down + 8)
    public int Flush(float[] oL, float[] oR)
    {
        long total = totalIn * up / down; Ensure(2 * H + 4); for (int i = 0; i < 2 * H + 2; i++) { bl[have] = 0; br[have] = 0; have++; }
        int c = Run(oL, oR, total); return c;
    }
}
