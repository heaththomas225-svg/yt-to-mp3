using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// An LFO shape drawn with control points. Each point can be a corner or a smooth peak, and the segment after it can be bent.
// The audio engine and the editor on screen both read the same lookup table, so what you see is what modulates the sound.
class LfoPt
{
    public float X, Y, C; public bool Smooth;   // C: -1..1 bends the segment that starts here; Smooth: eased into the next point (sine-like)
    public LfoPt(float x, float y, float c = 0, bool smooth = false) { X = x; Y = y; C = c; Smooth = smooth; }
    public LfoPt Copy() { return new LfoPt(X, Y, C, Smooth); }
}

class LfoCurve
{
    public const int N = 1024;
    public readonly List<LfoPt> Pts = new List<LfoPt>(); public float[] Lut = new float[N + 1];
    public static readonly string[] PresetNames = { "Sine", "Triangle", "Saw up", "Saw down", "Square", "Pulse 25%", "Stepped", "Random steps", "Smooth random", "Wobble 1", "Wobble 2", "Wobble 3" };

    public LfoCurve() { Load("Sine"); }
    public LfoCurve Clone() { var c = new LfoCurve(); c.Pts.Clear(); foreach (var p in Pts) c.Pts.Add(p.Copy()); c.Rebuild(); return c; }

    public void Load(string name)
    {
        var a = Pts; a.Clear(); var rnd = new Random(name.GetHashCode() & 0x7fff);
        switch (name)
        {
            case "Triangle": a.Add(new LfoPt(0, -1)); a.Add(new LfoPt(0.5f, 1)); a.Add(new LfoPt(1, -1)); break;
            case "Saw up": a.Add(new LfoPt(0, -1)); a.Add(new LfoPt(1, 1)); break;
            case "Saw down": a.Add(new LfoPt(0, 1)); a.Add(new LfoPt(1, -1)); break;
            case "Square": a.Add(new LfoPt(0, 1)); a.Add(new LfoPt(0.5f, 1)); a.Add(new LfoPt(0.5f, -1)); a.Add(new LfoPt(1, -1)); break;
            case "Pulse 25%": a.Add(new LfoPt(0, 1)); a.Add(new LfoPt(0.25f, 1)); a.Add(new LfoPt(0.25f, -1)); a.Add(new LfoPt(1, -1)); break;
            case "Stepped": for (int i = 0; i < 8; i++) { float y = -1 + 2f * i / 7f; a.Add(new LfoPt(i / 8f, y)); a.Add(new LfoPt((i + 1) / 8f, y)); } break;
            case "Random steps": for (int i = 0; i < 8; i++) { float y = (float)(rnd.NextDouble() * 2 - 1); a.Add(new LfoPt(i / 8f, y)); a.Add(new LfoPt((i + 1) / 8f, y)); } break;
            case "Smooth random": for (int i = 0; i < 8; i++) a.Add(new LfoPt(i / 8f, (float)(rnd.NextDouble() * 2 - 1), 0, true)); a.Add(new LfoPt(1, a[0].Y)); break;
            case "Wobble 1": a.Add(new LfoPt(0, 1, 0, true)); a.Add(new LfoPt(0.25f, -1, 0, true)); a.Add(new LfoPt(0.5f, 1, 0, true)); a.Add(new LfoPt(0.75f, -1, 0, true)); a.Add(new LfoPt(1, 1)); break;      // two quick humps per cycle
            case "Wobble 2": a.Add(new LfoPt(0, -1, 0.6f)); a.Add(new LfoPt(0.3f, 1, -0.5f)); a.Add(new LfoPt(0.5f, -0.2f, 0.5f)); a.Add(new LfoPt(0.7f, 1, -0.5f)); a.Add(new LfoPt(1, -1)); break;          // double hit then fall
            case "Wobble 3": a.Add(new LfoPt(0, 1, -0.7f)); a.Add(new LfoPt(0.12f, -1)); a.Add(new LfoPt(0.25f, 0.6f, -0.6f)); a.Add(new LfoPt(0.4f, -1)); a.Add(new LfoPt(0.55f, 1, -0.7f)); a.Add(new LfoPt(0.7f, -1)); a.Add(new LfoPt(1, -1)); break;   // stutter
            default: a.Add(new LfoPt(0, 1, 0, true)); a.Add(new LfoPt(0.5f, -1, 0, true)); a.Add(new LfoPt(1, 1)); break;   // sine (a cosine start at the top)
        }
        Rebuild();
    }

    // sorts the points, keeps the ends at 0 and 1, and fills the lookup table
    public void Rebuild()
    {
        var sorted = new List<LfoPt>(Pts); var idx = new Dictionary<LfoPt, int>(); for (int i = 0; i < sorted.Count; i++) idx[sorted[i]] = i;
        sorted.Sort((p, q) => { int c = p.X.CompareTo(q.X); return c != 0 ? c : idx[p].CompareTo(idx[q]); });
        Pts.Clear(); Pts.AddRange(sorted);
        if (Pts.Count < 2) { Pts.Clear(); Pts.Add(new LfoPt(0, 0)); Pts.Add(new LfoPt(1, 0)); }
        Pts[0].X = 0; Pts[Pts.Count - 1].X = 1; foreach (var p in Pts) { p.X = Math.Max(0, Math.Min(1, p.X)); p.Y = Math.Max(-1, Math.Min(1, p.Y)); p.C = Math.Max(-1, Math.Min(1, p.C)); }
        var lut = new float[N + 1]; for (int i = 0; i <= N; i++) lut[i] = Eval(Math.Min(0.99999, i / (double)N)); lut[N] = lut[N - 1]; Lut = lut;
    }

    public float Eval(double p)
    {
        var a = Pts; int n = a.Count; if (n == 0) return 0; if (p <= a[0].X) return a[0].Y;
        for (int i = 0; i < n - 1; i++)
        {
            var p0 = a[i]; var p1 = a[i + 1];
            if (p >= p0.X && p < p1.X)
            {
                double t = (p - p0.X) / (p1.X - p0.X), tt = p0.C >= 0 ? Math.Pow(t, 1 + 3 * p0.C) : 1 - Math.Pow(1 - t, 1 + 3 * -p0.C); if (p0.Smooth) tt = (1 - Math.Cos(Math.PI * tt)) / 2;
                return (float)(p0.Y + (p1.Y - p0.Y) * tt);
            }
        }
        return a[n - 1].Y;
    }

    // value at a phase 0..1 from the table (this is what the audio uses)
    public float Read(double phase)
    {
        var l = Lut; double x = (phase - Math.Floor(phase)) * N; int i = (int)x; float f = (float)(x - i); return l[i] + (l[i + 1] - l[i]) * f;
    }

    public string Serialize()
    {
        var sb = new StringBuilder(); foreach (var p in Pts) { if (sb.Length > 0) sb.Append(','); sb.Append(p.X.ToString("0.####", CultureInfo.InvariantCulture)).Append(':').Append(p.Y.ToString("0.####", CultureInfo.InvariantCulture)).Append(':').Append(p.C.ToString("0.###", CultureInfo.InvariantCulture)).Append(':').Append(p.Smooth ? 1 : 0); }
        return sb.ToString();
    }
    public void Parse(string s)
    {
        var list = new List<LfoPt>();
        try
        {
            foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var q = part.Split(':'); if (q.Length < 4) continue; float x, y, c; int sm;
                if (float.TryParse(q[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && float.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c) && int.TryParse(q[3], out sm)) list.Add(new LfoPt(x, y, c, sm != 0));
            }
        }
        catch { }
        if (list.Count >= 2) { Pts.Clear(); Pts.AddRange(list); Rebuild(); }
    }
}
