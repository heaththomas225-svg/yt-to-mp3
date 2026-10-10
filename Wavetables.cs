using System;
using System.Collections.Generic;
using System.Globalization;

// Band-limited wavetables for Bass Lab. Every frame is stored as harmonics (amplitude + phase) and turned into single-cycle
// tables with fewer and fewer harmonics ("mips"), so a high note never uses harmonics above the Nyquist frequency (no aliasing).
class WTable
{
    public const int Size = 2048, Harm = 512;
    public static readonly int[] Limits = { 512, 362, 256, 181, 128, 90, 64, 45, 32, 23, 16, 11, 8, 5, 4, 2, 1 };
    public string Name; public int Frames; public readonly List<double[]> Amp = new List<double[]>(), Ph = new List<double[]>();
    float[][][] mip; double[] norm; readonly object lk = new object();

    public WTable(string name) { Name = name; }
    public void AddFrame(double[] amp, double[] ph) { Amp.Add(amp); Ph.Add(ph); Frames = Amp.Count; mip = null; }
    public void Prepare() { Ensure(); }   // builds all the single-cycle tables now (so the audio never waits for them)

    // single-cycle table of one frame with at most 'limit' harmonics (2049 floats, the last repeats the first)
    float[] Build(int frame, int limit)
    {
        var t = new float[Size + 1]; var a = Amp[frame]; var p = Ph[frame]; int H = Math.Min(Math.Min(limit, Harm), a.Length - 1);
        for (int h = 1; h <= H; h++)
        {
            double amp = a[h]; if (Math.Abs(amp) < 1e-9) continue; double ph = p[h], w = 2 * Math.PI * h / Size, sn = Math.Sin(w), cs = Math.Cos(w), s = Math.Sin(ph), c = Math.Cos(ph);
            for (int i = 0; i < Size; i++) { t[i] += (float)(amp * s); double ns = s * cs + c * sn, nc = c * cs - s * sn; s = ns; c = nc; }
        }
        t[Size] = t[0]; return t;
    }

    void Ensure()
    {
        if (mip != null) return;
        lock (lk)
        {
            if (mip != null) return; var m = new float[Frames][][]; var nrm = new double[Frames];
            for (int f = 0; f < Frames; f++)
            {
                m[f] = new float[Limits.Length][]; var top = Build(f, Limits[0]); float pk = 0; for (int i = 0; i < Size; i++) pk = Math.Max(pk, Math.Abs(top[i])); nrm[f] = pk > 1e-6 ? 0.95 / pk : 1;
                for (int k = 0; k < Limits.Length; k++) { var t = k == 0 ? top : Build(f, Limits[k]); float g = (float)nrm[f]; for (int i = 0; i <= Size; i++) t[i] *= g; m[f][k] = t; }
            }
            norm = nrm; mip = m;
        }
    }

    public static int MipFor(double freq, int sr)
    {
        int maxH = (int)(0.5 * sr / Math.Max(10, freq)); for (int k = 0; k < Limits.Length; k++) if (Limits[k] <= maxH) return k; return Limits.Length - 1;
    }

    // value at wavetable position pos (0..1 across the frames) and phase (0..1), using the mip for this pitch
    public float Read(double pos, double phase, int mipLevel)
    {
        Ensure(); double fp = Math.Max(0, Math.Min(1, pos)) * (Frames - 1); int f0 = (int)fp; if (f0 >= Frames - 1) f0 = Math.Max(0, Frames - 2); double ft = Frames > 1 ? fp - f0 : 0;
        double idx = phase * Size; int i0 = (int)idx; if (i0 >= Size) i0 = Size - 1; if (i0 < 0) i0 = 0; float fr = (float)(idx - i0);
        var a = mip[f0][mipLevel]; float va = a[i0] + (a[i0 + 1] - a[i0]) * fr; if (ft < 1e-4 || Frames < 2) return va;
        var b = mip[f0 + 1][mipLevel]; float vb = b[i0] + (b[i0 + 1] - b[i0]) * fr; return (float)(va + (vb - va) * ft);
    }
}

static class Wavetables
{
    // the Source menu: Wavetable (scan the frames of the chosen table) or one fixed shape
    public static readonly string[] Sources = { "Wavetable", "Sine", "Triangle", "Saw", "Square", "Pulse 25%", "Pulse 10%", "Half sine", "Rectified sine", "Organ" };
    public static readonly string[] TableNames = { "Basic Shapes", "Saw to Square", "Vowels", "Growl", "Organ Drawbars", "Digital Bell", "Harmonic Sweep", "PWM", "Metallic", "Custom 1", "Custom 2", "Custom 3" };
    public const int CustomBase = 9;
    static readonly Dictionary<string, WTable> cache = new Dictionary<string, WTable>();
    static readonly object lk = new object();
    public static readonly short[][][] Custom = new short[3][][];   // up to 8 hand-drawn frames of 256 samples per custom table

    static double Hash(int n) { unchecked { uint x = (uint)(n * 2654435761u + 12345); x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; return (x & 0xFFFFFF) / 16777216.0; } }

    static void Shape(int kind, double[] a, double[] p)   // 0 sine .. 7 rectified, 8 organ
    {
        for (int h = 1; h < a.Length; h++)
        {
            switch (kind)
            {
                case 0: if (h == 1) a[h] = 1; break;
                case 1: if (h % 2 == 1) a[h] = ((h / 2) % 2 == 0 ? 1 : -1) / (double)(h * h); break;
                case 2: a[h] = (h % 2 == 1 ? 1 : -1) / (double)h; break;
                case 3: if (h % 2 == 1) a[h] = 1.0 / h; break;
                case 4: { double d = 0.25; a[h] = 2 / (h * Math.PI) * Math.Sin(h * Math.PI * d); p[h] = Math.PI / 2 - h * Math.PI * d; break; }
                case 5: { double d = 0.10; a[h] = 2 / (h * Math.PI) * Math.Sin(h * Math.PI * d); p[h] = Math.PI / 2 - h * Math.PI * d; break; }
                case 6: if (h == 1) a[h] = 0.5; else if (h % 2 == 0) { a[h] = -2 / (Math.PI * (h * h - 1)); p[h] = Math.PI / 2; } break;
                case 7: if (h % 2 == 0) { a[h] = -4 / (Math.PI * (h * h - 1)); p[h] = Math.PI / 2; } break;
                default: if (h == 1) a[h] = 1; else if (h == 2) a[h] = 0.8; else if (h == 4) a[h] = 0.6; else if (h == 8) a[h] = 0.4; break;
            }
        }
    }

    public static WTable ForSource(int source)   // source 1.. = fixed shapes
    {
        string key = "shape" + source; lock (lk) { WTable t; if (cache.TryGetValue(key, out t)) return t; t = new WTable(Sources[source]); var a = new double[WTable.Harm + 1]; var p = new double[WTable.Harm + 1]; Shape(source - 1 >= 8 ? 8 : source - 1, a, p); t.AddFrame(a, p); cache[key] = t; return t; }
    }

    public static WTable Table(int index)
    {
        if (index >= CustomBase) return CustomTable(index - CustomBase);
        string key = "table" + index; lock (lk) { WTable t; if (cache.TryGetValue(key, out t)) return t; t = BuildFactory(index); cache[key] = t; return t; }
    }

    static WTable BuildFactory(int index)
    {
        var t = new WTable(TableNames[index]); int H = WTable.Harm;
        switch (index)
        {
            case 0: foreach (int k in new[] { 0, 1, 2, 3, 4 }) { var a = new double[H + 1]; var p = new double[H + 1]; Shape(k, a, p); t.AddFrame(a, p); } break;
            case 1: for (int f = 0; f < 16; f++) { double m = f / 15.0; var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) a[h] = (h % 2 == 1 ? 1.0 : -(1 - m)) / h; t.AddFrame(a, p); } break;
            case 2:
                {
                    double[][] F = { new[] { 800.0, 1150, 2900 }, new[] { 400.0, 1600, 2700 }, new[] { 270.0, 2300, 3000 }, new[] { 450.0, 800, 2830 }, new[] { 325.0, 700, 2530 } };   // A E I O U
                    foreach (var fm in F) { var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) { double f = h * 110.0, g = 0.04; for (int k = 0; k < 3; k++) g += (k == 0 ? 1.0 : 0.6 / (k + 0.5)) * Math.Exp(-Math.Pow((f - fm[k]) / (80 + 0.12 * fm[k]), 2)); a[h] = g / h * 3 * (h % 2 == 1 ? 1 : -1); } t.AddFrame(a, p); }
                    break;
                }
            case 3: for (int f = 0; f < 16; f++) { double c = 2 + f * 2.6; var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) a[h] = (h % 2 == 1 ? 1 : -1) / (double)h * (0.25 + 3.5 * Math.Exp(-Math.Pow((h - c) / (1.2 + 0.12 * c), 2))) + (h > 1 ? 0.15 * (Hash(h * 7 + f) - 0.5) / h : 0); t.AddFrame(a, p); } break;
            case 4: { int[][] bars = { new[] { 1 }, new[] { 1, 2 }, new[] { 1, 2, 3 }, new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4, 6 }, new[] { 1, 2, 3, 4, 6, 8 }, new[] { 1, 2, 3, 4, 5, 6, 8 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 } }; foreach (var bs in bars) { var a = new double[H + 1]; var p = new double[H + 1]; foreach (int h in bs) a[h] = 1.0 / Math.Sqrt(h); t.AddFrame(a, p); } break; }
            case 5: for (int f = 0; f < 16; f++) { var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) { double env = Math.Exp(-h / (4.0 + f * 1.6)); double r = Hash(h * 13 + 5); a[h] = env * (0.25 + 0.75 * r) * (1.0 / Math.Sqrt(h)) * (h % 2 == 0 && f < 8 ? 0.35 : 1); p[h] = Hash(h * 29 + 3) * 2 * Math.PI; } t.AddFrame(a, p); } break;
            case 6: for (int f = 0; f < 16; f++) { var a = new double[H + 1]; var p = new double[H + 1]; a[1] = 0.5; for (int h = 2; h <= H; h++) { double d = Math.Abs(h - (2 + f * 2.2)); a[h] = d < 3 ? (1 - d / 3) * 1.2 : 0; } t.AddFrame(a, p); } break;
            case 7: for (int f = 0; f < 16; f++) { double d = 0.5 - f * 0.03; var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) { a[h] = 2 / (h * Math.PI) * Math.Sin(h * Math.PI * d); p[h] = Math.PI / 2 - h * Math.PI * d; } t.AddFrame(a, p); } break;
            default: for (int f = 0; f < 16; f++) { var a = new double[H + 1]; var p = new double[H + 1]; for (int h = 1; h <= H; h++) { bool prime = IsPrime(h + f); a[h] = (prime ? 1.0 : 0.08) / Math.Sqrt(h); p[h] = Hash(h * 31 + f * 3) * 2 * Math.PI; } t.AddFrame(a, p); } break;
        }
        return t;
    }
    static bool IsPrime(int n) { if (n < 2) return false; for (int i = 2; i * i <= n; i++) if (n % i == 0) return false; return true; }

    // ---- hand-drawn tables ----
    static readonly Dictionary<int, WTable> customCache = new Dictionary<int, WTable>(); static readonly int[] customVer = new int[3];
    public static int CustomVersion(int slot) { return customVer[slot]; }
    public static short[][] GetCustom(int slot) { return Custom[slot]; }
    public static void SetCustom(int slot, short[][] frames)
    {
        // the new table is built completely before it replaces the old one, so playing notes switch over without a hiccup
        var copy = new short[frames.Length][]; for (int i = 0; i < frames.Length; i++) copy[i] = (short[])frames[i].Clone();
        var t = new WTable(TableNames[CustomBase + slot]); foreach (var fr in copy) AddDrawn(t, fr); t.Prepare();
        lock (lk) { Custom[slot] = copy; customVer[slot]++; customCache[slot] = t; }
    }
    static bool warming;
    public static void WarmUp()
    {
        if (warming) return; warming = true;
        var th = new System.Threading.Thread(() => { try { for (int i = 1; i < Sources.Length; i++) ForSource(i).Prepare(); for (int i = 0; i < CustomBase; i++) Table(i).Prepare(); } catch { } }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
        th.Start();
    }
    public static short[] DefaultFrame() { var f = new short[256]; for (int i = 0; i < 256; i++) f[i] = (short)(Math.Sin(2 * Math.PI * i / 256.0) * 30000); return f; }
    static WTable CustomTable(int slot)
    {
        lock (lk)
        {
            WTable t; if (customCache.TryGetValue(slot, out t)) return t;
            var frames = Custom[slot]; if (frames == null || frames.Length == 0) frames = new[] { DefaultFrame() };
            t = new WTable(TableNames[CustomBase + slot]); foreach (var fr in frames) AddDrawn(t, fr); customCache[slot] = t; return t;
        }
    }
    static void AddDrawn(WTable t, short[] fr)
    {
        int N = fr.Length; var a = new double[WTable.Harm + 1]; var p = new double[WTable.Harm + 1]; double mean = 0; for (int i = 0; i < N; i++) mean += fr[i]; mean /= N;
        for (int k = 1; k < N / 2 && k <= WTable.Harm; k++)
        {
            double ca = 0, sb = 0; for (int i = 0; i < N; i++) { double v = (fr[i] - mean) / 32768.0, w = 2 * Math.PI * k * i / N; ca += v * Math.Cos(w); sb += v * Math.Sin(w); }
            ca *= 2.0 / N; sb *= 2.0 / N; a[k] = Math.Sqrt(ca * ca + sb * sb); p[k] = Math.Atan2(ca, sb);
        }
        t.AddFrame(a, p);
    }

    // ---- saving the drawn tables inside a patch ----
    public static string SerializeCustom(int slot)
    {
        var f = Custom[slot]; if (f == null) return ""; var bytes = new List<byte>(); bytes.Add((byte)f.Length); foreach (var fr in f) foreach (short s in fr) { bytes.Add((byte)s); bytes.Add((byte)(s >> 8)); }
        return Convert.ToBase64String(bytes.ToArray());
    }
    public static void DeserializeCustom(int slot, string b64)
    {
        try
        {
            var b = Convert.FromBase64String(b64); int n = b[0]; var f = new short[n][]; int p = 1; for (int i = 0; i < n; i++) { f[i] = new short[256]; for (int k = 0; k < 256; k++) { f[i][k] = (short)(b[p] | (b[p + 1] << 8)); p += 2; } }
            SetCustom(slot, f);
        }
        catch { }
    }

    // ---- warp (used by the engine and by the display) ----
    public static readonly string[] Warps = { "Off", "Sync", "Bend +", "Fold", "Bend -", "Mirror", "Asym", "Crush" };
    public static double WarpPhase(int mode, double amt, double p)
    {
        switch (mode)
        {
            case 1: { double r = 1 + amt * 7; double q = p * r; return q - Math.Floor(q); }
            case 2: return Math.Pow(p, 1 + amt * 3);
            case 4: return 1 - Math.Pow(1 - p, 1 + amt * 3);
            case 5: { double q = p * 2 - Math.Floor(p * 2); double mir = p < 0.5 ? q : 1 - q; return p * (1 - amt) + mir * amt; }
            case 6: { double k = 0.5 + amt * 0.45; return p < k ? 0.5 * p / k : 0.5 + 0.5 * (p - k) / (1 - k); }
        }
        return p;
    }
    public static double WarpOut(int mode, double amt, double y)
    {
        if (mode == 3 && amt > 0.001) return Math.Sin(y * (1 + amt * 5) * 1.5707963);
        if (mode == 7 && amt > 0.001) { double steps = Math.Max(2, 64 * (1 - amt)); return Math.Round(y * steps) / steps; }
        return y;
    }
}
