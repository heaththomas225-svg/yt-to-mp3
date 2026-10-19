using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Linq;
// renders wobble patches with the engine, measures each (sweep, LFO rate, grit, sub) and draws a spectrogram sheet
static class WobTest
{
    static void Fft(double[] re, double[] im)
    {
        int n = re.Length; for (int i = 1, j = 0; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; } }
        for (int len = 2; len <= n; len <<= 1) { double a = -2 * Math.PI / len; double wr = Math.Cos(a), wi = Math.Sin(a); for (int i = 0; i < n; i += len) { double cr = 1, ci = 0; for (int k = 0; k < len / 2; k++) { int p = i + k, q = p + len / 2; double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr; re[q] = re[p] - tr; im[q] = im[p] - ti; re[p] += tr; im[p] += ti; double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr; } } }
    }
    static void Draw(Graphics g, float[] x, int X, int Y, int W, int H, string title)
    {
        g.FillRectangle(Brushes.Black, X, Y, W, H); using (var f = new Font("Segoe UI", 9)) g.DrawString(title, f, Brushes.White, X + 2, Y + 1);
        int wh = 40; for (int c = 0; c < W; c++) { int a = (int)((long)c * x.Length / W), b = (int)((long)(c + 1) * x.Length / W); float mn = 0, mx = 0; for (int i = a; i < b && i < x.Length; i++) { mn = Math.Min(mn, x[i]); mx = Math.Max(mx, x[i]); } var pen = (mx > 0.98f || mn < -0.98f) ? Pens.Red : Pens.Lime; g.DrawLine(pen, X + c, Y + 18 + wh / 2 - mx * wh / 2, X + c, Y + 18 + wh / 2 - mn * wh / 2); }
        int N = 1024, sy = Y + 18 + wh + 4, sh = H - 18 - wh - 6; var bmp = new Bitmap(W, sh);
        for (int c = 0; c < W; c++) { int at = (int)((long)c * Math.Max(1, x.Length - N) / W); var re = new double[N]; var im = new double[N]; for (int i = 0; i < N; i++) re[i] = (at + i < x.Length ? x[at + i] : 0) * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1))); Fft(re, im);
            for (int r = 0; r < sh; r++) { double frac = 1 - r / (double)sh; double hz = 30 * Math.Pow(16000 / 30.0, frac); int bin = Math.Min(N / 2 - 1, (int)(hz / 44100 * N)); double db = 10 * Math.Log10(re[bin] * re[bin] + im[bin] * im[bin] + 1e-12) - 50; double v = Math.Max(0, Math.Min(1, (db + 100) / 100)); int R = (int)(255 * Math.Min(1, v * 2)), Gc = (int)(255 * Math.Max(0, v * 2 - 1)), B = (int)(120 * (1 - v)); bmp.SetPixel(c, r, Color.FromArgb(R, Gc, B)); } }
        g.DrawImage(bmp, X, sy);
    }
    static void Main(string[] a)
    {
        var tests = new List<KeyValuePair<string, WobblePatch>>();
        Func<string, WobblePatch> P = s => WobblePatch.Load(s);
        tests.Add(new KeyValuePair<string, WobblePatch>("Classic 1/8 wub (saw)", P("saw=1;cutoff=180;depth=4;res=0.55;rates=8;shape=0;drive=0.45")));
        tests.Add(new KeyValuePair<string, WobblePatch>("Square rubber 1/4", P("saw=0;square=1;pw=0.35;pwmod=0.4;cutoff=150;depth=4.2;res=0.7;rates=4;shape=1;drive=0.3;envamt=1.5")));
        tests.Add(new KeyValuePair<string, WobblePatch>("Talking (vowels)", P("saw=1;square=0.5;formant=1;vowels=aoiu;res=0.6;cutoff=700;depth=2.2;rates=8;shape=5;drive=0.35")));
        tests.Add(new KeyValuePair<string, WobblePatch>("FM growl 1/16", P("saw=0;fm=1;fmratio=1;fmindex=3;fmmod=0.8;cutoff=300;depth=3.5;res=0.5;rates=16;shape=2;drive=0.7")));
        tests.Add(new KeyValuePair<string, WobblePatch>("Reese neuro", P("saw=0;reese=1;detune=22;cutoff=220;depth=3.5;res=0.4;rates=8,16,8,12;shape=0;drive=0.6;fold=0.3")));
        tests.Add(new KeyValuePair<string, WobblePatch>("Rate sequence", P("saw=1;cutoff=170;depth=4.5;res=0.6;rates=4,8,16,12;shape=0;drive=0.5")));
        if (a.Length > 0) { tests.Clear(); foreach (var x in a) tests.Add(new KeyValuePair<string, WobblePatch>(x.Length > 26 ? x.Substring(0, 26) : x, P(x))); }
        double bpm = 140; int sr = SoundGen.SR; int W = 700, H = 210; var sheet = new Bitmap(W * 2, H * ((tests.Count + 1) / 2));
        using (var g = Graphics.FromImage(sheet)) for (int t = 0; t < tests.Count; t++)
        {
            var notes = new List<double[]> { new double[] { 0, 3.8, 36, 112 }, new double[] { 4, 3.8, 36, 112 }, new double[] { 8, 3.8, 39, 112 }, new double[] { 12, 3.8, 34, 112 } };
            var sw = System.Diagnostics.Stopwatch.StartNew(); var b = WobbleEngine.Render(tests[t].Value, notes, bpm, 16, 0.2); sw.Stop(); float pk = b.Peak();
            var m = WobAudit.Measure(b.L, b.R, sr);
            Console.WriteLine(tests[t].Key.PadRight(26) + " sweep x" + m.Range.ToString("0.0").PadRight(5) + " rate " + m.Rate.ToString("0.0").PadRight(5) + "Hz grit " + (m.Grit * 100).ToString("0.0").PadRight(5) + "% sub " + (m.Sub * 100).ToString("0").PadRight(4) + "% mid " + (m.Mid * 100).ToString("0").PadRight(4) + "% swing " + m.Swing.ToString("0").PadRight(3) + "dB" + "% peak " + pk.ToString("0.00") + " render " + sw.ElapsedMilliseconds + " ms (" + (16 * 60 / bpm).ToString("0.0") + " s audio)");
            b.Normalise(-1, 64, 512); Draw(g, b.L, (t % 2) * W, (t / 2) * H, W - 4, H - 4, tests[t].Key);
            SoundIO.WriteWav("C:/Users/thoma/nfout/wob" + t + ".wav", b.L, b.R, sr, 16);
        }
        sheet.Save("C:/Users/thoma/nfout/wob.png", ImageFormat.Png);
    }
}
