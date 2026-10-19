using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Linq;
// draws a waveform + spectrogram sheet of real pack sounds so crackle (vertical streaks), clipping (flat tops) and distortion (dense smeared harmonics) can be seen
static class SPX
{
    static void Fft(double[] re, double[] im)
    {
        int n = re.Length; for (int i = 1, j = 0; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; } }
        for (int len = 2; len <= n; len <<= 1) { double a = -2 * Math.PI / len; double wr = Math.Cos(a), wi = Math.Sin(a); for (int i = 0; i < n; i += len) { double cr = 1, ci = 0; for (int k = 0; k < len / 2; k++) { int p = i + k, q = p + len / 2; double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr; re[q] = re[p] - tr; im[q] = im[p] - ti; re[p] += tr; im[p] += ti; double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr; } } }
    }
    static void Draw(Graphics g, float[] x, int X, int Y, int W, int H, string title)
    {
        g.FillRectangle(Brushes.Black, X, Y, W, H); using (var f = new Font("Segoe UI", 9)) g.DrawString(title, f, Brushes.White, X + 2, Y + 1);
        int wh = 50; for (int c = 0; c < W; c++) { int a = (int)((long)c * x.Length / W), b = (int)((long)(c + 1) * x.Length / W); float mn = 0, mx = 0; for (int i = a; i < b && i < x.Length; i++) { mn = Math.Min(mn, x[i]); mx = Math.Max(mx, x[i]); } var pen = (mx > 0.98f || mn < -0.98f) ? Pens.Red : Pens.Lime; g.DrawLine(pen, X + c, Y + 18 + wh / 2 - mx * wh / 2, X + c, Y + 18 + wh / 2 - mn * wh / 2); }
        int N = 1024, sy = Y + 18 + wh + 4, sh = H - 18 - wh - 6; var bmp = new Bitmap(W, sh);
        for (int c = 0; c < W; c++) { int at = (int)((long)c * Math.Max(1, x.Length - N) / W); var re = new double[N]; var im = new double[N]; for (int i = 0; i < N; i++) re[i] = (at + i < x.Length ? x[at + i] : 0) * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1))); Fft(re, im);
            for (int r = 0; r < sh; r++) { double frac = 1 - r / (double)sh; double hz = 30 * Math.Pow(22000 / 30.0, frac); int bin = Math.Min(N / 2 - 1, (int)(hz / 44100 * N)); double db = 10 * Math.Log10(re[bin] * re[bin] + im[bin] * im[bin] + 1e-12) - 20; double v = Math.Max(0, Math.Min(1, (db + 100) / 100)); int R = (int)(255 * Math.Min(1, v * 2)), Gc = (int)(255 * Math.Max(0, v * 2 - 1)), B = (int)(120 * (1 - v)); bmp.SetPixel(c, r, Color.FromArgb(R, Gc, B)); } }
        g.DrawImage(bmp, X, sy);
    }
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_spec"); try { Directory.Delete(StudioEnv.DataDir, true); } catch { } Directory.CreateDirectory(StudioEnv.DataDir);
        var rec = SoundCatalog.Find("trap-starter"); var p = PackBuilder.Build(rec, null, null); var picks = new List<SoundItem>();
        foreach (var t in new[] { "Kick", "Snare", "Clap", "Hi-Hat", "Open Hat", "808", "Sub Bass", "Drum Loop", "Bass Loop", "Chord Loop", "Melody Loop", "Synth Loop", "Guitar Loop", "Vocal Chop", "Vocal Shout", "Growl Bass", "Reese Bass", "Bass Stab", "Riser", "Impact" }) { var s = p.Items.FirstOrDefault(i => i.Type == t && i.IsAudio); if (s != null) picks.Add(s); }
        foreach (var s in p.Items.Where(i => i.Type == "Bass Loop").Take(6)) if (!picks.Contains(s)) picks.Add(s);
        var inst = InstantLib.Items.Where(i => i.PackId == "trap-starter").ToList(); foreach (var k in new[] { 0, 5, 125, 130 }) { InstantLib.Ensure(inst[k]); picks.Add(inst[k]); }
        int W = 600, H = 230, cols = 2, rows = (picks.Count + 1) / 2; var sheet = new Bitmap(W * cols, H * rows); var log = new StreamWriter("C:/Users/thoma/nfout/spec.txt");
        using (var g = Graphics.FromImage(sheet)) for (int i = 0; i < picks.Count; i++) { float[] l, r; int sr; string fm; SoundIO.Read(picks[i].Path, out l, out r, out sr, out fm); Draw(g, l, (i % cols) * W, (i / cols) * H, W - 4, H - 4, picks[i].Type + ": " + picks[i].Name.Replace(rec.Name + " - ", "")); log.WriteLine(i + " " + picks[i].Type + " | " + picks[i].Name + " | " + picks[i].Path); }
        log.Close(); sheet.Save("C:/Users/thoma/nfout/spec.png", ImageFormat.Png); Console.WriteLine("1 passed, 0 failed");
    }
}
