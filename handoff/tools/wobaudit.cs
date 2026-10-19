using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// measures how much a bass sound actually moves: the spectral centroid every 12 ms (where the brightness sits), its range (p90/p10),
// the wobble rate (the strongest repeat of the centroid between 1 and 20 Hz), and the grit (energy above 1 kHz against the whole)
static class WobAudit
{
    internal class M { public double Range, Rate, Grit, Sub, Swing, Mid; }
    internal static M Measure(float[] l, float[] r, int sr)
    {
        int N = 1024, hop = sr * 12 / 1000; var re = new double[N]; var im = new double[N]; var cent = new List<double>(); var band = new List<double>(); var lvl = new List<double>(); double hi = 0, all = 0, low = 0, midE = 0;
        for (int a = 0; a + N < l.Length; a += hop)
        {
            for (int k = 0; k < N; k++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * k / (N - 1)); re[k] = (l[a + k] + r[a + k]) * 0.5 * w; im[k] = 0; }
            Fft.Transform(re, im, false); double num = 0, den = 0, bE = 0, tE = 0;
            for (int k = 1; k < N / 2; k++) { double hz = k * (double)sr / N, e = re[k] * re[k] + im[k] * im[k]; if (hz > 140 && hz < 12000) { num += hz * e; den += e; } if (hz > 1000) hi += e; if (hz > 300 && hz < 3000) bE += e; if (hz > 140) midE += e; tE += e; if (hz < 100) low += e; all += e; }
            if (den > 1e-6) cent.Add(num / den); if (tE > 1e-6) { band.Add(10 * Math.Log10(bE + 1e-12)); lvl.Add(10 * Math.Log10(tE)); }
        }
        var m = new M(); if (cent.Count < 20) return m; var s = cent.OrderBy(x => x).ToList(); m.Range = s[(int)(s.Count * 0.9)] / Math.Max(1, s[(int)(s.Count * 0.1)]); m.Grit = hi / Math.Max(1e-9, all); m.Sub = low / Math.Max(1e-9, all); m.Mid = midE / Math.Max(1e-9, all);
        { /* the swing: the 300 Hz - 3 kHz band's p90 - p10 over frames that are sounding (within 30 dB of the loudest) */ double top = lvl.Max(); var bs = band.Where((x, i) => lvl[i] > top - 30).OrderBy(x => x).ToList(); if (bs.Count > 10) m.Swing = bs[(int)(bs.Count * 0.9)] - bs[(int)(bs.Count * 0.1)]; }
        double mean = cent.Average(); var c = cent.Select(x => x - mean).ToArray(); double fps = 1000.0 / 12; double best = 0; int bl = 0;
        int lo = (int)(fps / 20), hi2 = (int)(fps / 1); var ac = new double[hi2 + 2];
        for (int lag = lo; lag <= hi2 + 1; lag++) { double acc = 0, n0 = 0; for (int i = 0; i + lag < c.Length; i++) { acc += c[i] * c[i + lag]; n0 += c[i] * c[i]; } ac[lag] = acc / Math.Max(1e-9, n0); if (lag <= hi2 && ac[lag] > best) { best = ac[lag]; bl = lag; } }
        /* the first strong peak is the wobble (later peaks are its multiples) */
        for (int lag = lo + 1; lag <= hi2; lag++) if (ac[lag] >= ac[lag - 1] && ac[lag] >= ac[lag + 1] && ac[lag] > 0.3 && ac[lag] >= best * 0.8) { bl = lag; best = ac[lag]; break; }
        m.Rate = best > 0.3 && bl > 0 ? fps / bl : 0; return m;
    }
    static void Main(string[] a)
    {
        string root = a[0]; foreach (var pack in a.Skip(1))
        {
            string dir = Path.Combine(root, pack); if (!Directory.Exists(dir)) { Console.WriteLine(pack + ": not installed"); continue; }
            Console.WriteLine("== " + pack);
            foreach (var f in Directory.GetFiles(dir, "*.wav", SearchOption.AllDirectories).Where(x => x.Contains("Bass")).OrderBy(x => x))
            {
                float[] l, r; int sr; string fmt; if (!SoundIO.Read(f, out l, out r, out sr, out fmt)) continue; var m = Measure(l, r, sr);
                Console.WriteLine("  " + Path.GetFileNameWithoutExtension(f).PadRight(44) + " sweep x" + m.Range.ToString("0.0").PadRight(5) + " wobble " + (m.Rate > 0 ? m.Rate.ToString("0.0") + " Hz" : "none").PadRight(8) + " grit " + (m.Grit * 100).ToString("0.0") + "%   sub " + (m.Sub * 100).ToString("0") + "%");
            }
        }
    }
}
