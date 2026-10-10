using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// ======================================================================
//  Built-in plugin library: the framework.
//  A "variant" is a named plugin (for example "Telephone Vocal") made from a real DSP engine plus starting values.
//  Several variants can share one engine; a variant can also be a chain of engines (written "a{p=v} > b{p=v}").
// ======================================================================
public static class PluginLibrary
{
    public class Variant { public string Id, Name, Cat, Blurb, Engine, Preset, Hide; }
    public static readonly List<Variant> All = new List<Variant>();
    static readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase), ids = new HashSet<string>();
    static bool done;

    public static string IdFor(string name)
    {
        var sb = new StringBuilder("lib_"); foreach (char c in name.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        string s = sb.ToString(); while (s.IndexOf("__", StringComparison.Ordinal) >= 0) s = s.Replace("__", "_"); return s.TrimEnd('_');
    }
    // a name used twice in the list of plugins is only added once
    public static void Add(string cat, string name, string engine, string preset, string blurb, string hide = "")
    {
        foreach (var b in FxRegistry.Builtin) if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) return;
        if (!names.Add(name)) return; string id = IdFor(name); if (!ids.Add(id)) return;
        All.Add(new Variant { Id = id, Name = name, Cat = cat, Blurb = blurb, Engine = engine, Preset = preset ?? "", Hide = hide ?? "" });
    }
    public static bool IsLibrary(string type) { return type != null && type.StartsWith("lib_", StringComparison.Ordinal) && ids.Contains(type); }

    public static void RegisterAll()
    {
        if (done) return; done = true; PluginTables.Fill();
        foreach (var v in All) { var vv = v; FxRegistry.Register(new FxRegistry.Info(vv.Id, vv.Name, vv.Cat, vv.Blurb), () => Make(vv)); }
    }

    public static Fx Make(Variant v)
    {
        Fx f = Build(v.Engine, v.Name); f.Title = v.Name; f.Category = v.Cat; f.Blurb = v.Blurb; f.Hidden = v.Hide;
        ApplyPreset(f, v.Preset); f.Type = v.Id; return f;
    }

    // "id=value;id[min,max]=value": starting values (and, in brackets, a narrower range) for a variant
    public static void ApplyPreset(Fx f, string preset)
    {
        if (!string.IsNullOrEmpty(preset))
            foreach (var tok in preset.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = tok.IndexOf('='); if (eq < 1) continue; string key = tok.Substring(0, eq).Trim(); float val; if (!float.TryParse(tok.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out val)) continue;
                int b = key.IndexOf('['); if (b > 0) { string[] mm = key.Substring(b + 1).TrimEnd(']').Split(','); key = key.Substring(0, b); var p = f.Find(key); float lo, hi; if (p != null && mm.Length == 2 && float.TryParse(mm[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lo) && float.TryParse(mm[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hi)) { p.Min = lo; p.Max = hi; } }
                f.Set(key, val);
            }
        foreach (var p in f.P) p.Def = p.Val; f.MarkDirty();
    }

    // "eq", or "a{x=1;y=2} > b" for a chain
    public static Fx Build(string spec, string title)
    {
        if (spec.IndexOf('>') < 0) { string id; string pre; Split(spec, out id, out pre); var f = Engine(id); if (f == null) throw new InvalidOperationException("no engine " + id); if (pre.Length > 0) ApplyPreset(f, pre); return f; }
        var stages = new List<Fx>(); var names2 = new List<string>();
        foreach (var part in spec.Split('>')) { string id, pre; Split(part.Trim(), out id, out pre); var f = Engine(id); if (f == null) throw new InvalidOperationException("no engine " + id); if (pre.Length > 0) ApplyPreset(f, pre); stages.Add(f); names2.Add(f.Title); }
        return new ChainFx(stages, title);
    }
    static void Split(string s, out string id, out string pre)
    {
        int i = s.IndexOf('{'); if (i < 0) { id = s.Trim(); pre = ""; return; }
        id = s.Substring(0, i).Trim(); int j = s.LastIndexOf('}'); pre = s.Substring(i + 1, (j > i ? j : s.Length) - i - 1);
    }

    public static Fx Engine(string id)
    {
        switch (id)
        {
            case "pshift": return new PitchShiftFx();
            case "cshift": return new ClassicShiftFx();
            case "formant": return new FormantFx();
            case "band": return new BandLimitFx();
            case "shaper": return new ShaperFx();
            case "crush": return new BitCrushFx();
            case "ring": return new RingModFx();
            case "fshift": return new FreqShiftFx();
            case "comb": return new CombFx();
            case "vocoder": return new VocoderFx();
            case "echo": return new EchoFx();
            case "reverb2": return new ReverbFx();
            case "conv": return new ConvFx();
            case "geq": return new GraphicEqFx();
            case "tilt": return new TiltEqFx();
            case "mseq": return new MsEqFx();
            case "dyneq": return new DynEqFx();
            case "vowel": return new VowelFx();
            case "lineq": return new LinEqFx();
            case "ressup": return new ResSupFx();
            case "specshape": return new SpectralShaperFx();
            case "autoeq": return new AutoEqFx();
            case "rider": return new VocalRiderFx();
            case "align": return new AlignFx();
            case "breath": return new BreathFx();
            case "plosive": return new PlosiveFx();
            case "vociso": return new VocalIsoFx();
            case "transient": return new TransientFx();
            case "declick": return new DeclickFx();
            case "chop": return new ChopFx();
            case "freeze": return new FreezeFx();
            case "grain": return new GranularFx();
            case "specfx": return new SpecCreateFx();
            case "fm": return new FmFx();
            case "tapestop": return new TapeStopFx();
            case "scratch": return new ScratchFx();
            case "repeat": return new RepeatFx();
            case "mod": return new ModFx();
            case "gen": return new GenFx();
            case "sub": return new SubFx();
            case "mbdist": return new MBDistFx();
            case "ott": return new OttFx();
            case "phaser": return new PhaserFx();
            case "flanger": return new FlangerFx();
            case "amp": return new AmpFx();
        }
        return FxRegistry.Create(id);
    }
}

// ======================================================================
//  Several engines in a row, shown as one plugin (parameters are listed per stage; one Mix for the whole chain).
// ======================================================================
class ChainFx : Fx
{
    readonly List<Fx> stages; float[] dl, dr; int dw, dlen, lat; float[] wl = new float[0], wr = new float[0];
    public ChainFx(List<Fx> st, string title)
    {
        stages = st; Type = "chain"; Title = title;
        for (int i = 0; i < st.Count; i++) foreach (var p in st[i].P)
            {
                if (p.IsChoice) { var c = Param.Choice("s" + (i + 1) + "." + p.Id, st[i].Title + ": " + p.Name, p.Choices, (int)p.Def); c.Val = p.Val; Add(c); }
                else { var q = new Param("s" + (i + 1) + "." + p.Id, st[i].Title + ": " + p.Name, p.Min, p.Max, p.Def, p.Unit); q.Val = p.Val; Add(q); }
            }
        AddP("mix", "Mix (whole chain)", 0, 100, 100, "%");
        foreach (var s in st) lat += s.Latency;
    }
    public override int Latency { get { int t = 0; foreach (var s in stages) t += s.Latency; return t; } }
    public override bool WantsSidechain { get { foreach (var s in stages) if (s.WantsSidechain) return true; return false; } }
    protected override void Update()
    {
        for (int i = 0; i < stages.Count; i++) foreach (var p in stages[i].P) { var mine = Find("s" + (i + 1) + "." + p.Id); if (mine != null && mine.Val != p.Val) stages[i].Set(p.Id, mine.Val); }
        foreach (var s in stages) s.Prepare(SR);
        dlen = Math.Max(16, Latency + 8); if (dl == null || dl.Length != dlen) { dl = new float[dlen]; dr = new float[dlen]; dw = 0; }
    }
    public override void Reset() { base.Reset(); foreach (var s in stages) s.Reset(); if (dl != null) { Array.Clear(dl, 0, dl.Length); Array.Clear(dr, 0, dr.Length); } }
    public override void BeginBlock(object track, long pos, int n, bool transport) { foreach (var s in stages) s.BeginBlock(track, pos, n, transport); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); float mix = Get("mix") / 100f; int L = Latency; if (dl == null || dlen <= L) { dlen = L + 8; dl = new float[dlen]; dr = new float[dlen]; dw = 0; }
        if (mix < 0.999f) { if (wl.Length < n) { wl = new float[n]; wr = new float[n]; } Array.Copy(l, wl, n); Array.Copy(r, wr, n); }
        foreach (var s in stages) { s.ScL = ScL; s.ScR = ScR; s.Process(l, r, n); }
        if (mix < 0.999f)
        {
            for (int i = 0; i < n; i++)
            {
                dl[dw] = wl[i]; dr[dw] = wr[i]; int rd = dw - L; if (rd < 0) rd += dlen; float a = dl[rd], b = dr[rd]; if (++dw >= dlen) dw = 0;
                l[i] = a + (l[i] - a) * mix; r[i] = b + (r[i] - b) * mix;
            }
        }
    }
}

// ======================================================================
//  Shared DSP helpers
// ======================================================================
static class Dsp2
{
    public static readonly string[] SyncNames = { "Free", "1/1", "1/2", "1/2 dotted", "1/4", "1/4 dotted", "1/4 triplet", "1/8", "1/8 dotted", "1/8 triplet", "1/16", "1/16 dotted", "1/16 triplet", "1/32" };
    public static readonly double[] SyncBeats = { 0, 4, 2, 3, 1, 1.5, 2.0 / 3.0, 0.5, 0.75, 1.0 / 3.0, 0.25, 0.375, 1.0 / 6.0, 0.125 };
    public static double BeatsToSec(double beats) { return 60.0 / Math.Max(20, Fx.ProjectBpm) * beats; }
    public static float Sanitize(float v) { return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v; }
    public static float Hermite(float[] b, int len, double pos)   // 4-point cubic read from a circular buffer (pos = fractional index)
    {
        pos %= len; if (pos < 0) pos += len; int i = (int)pos; float f = (float)(pos - i);
        float ym1 = b[(i - 1 + len) % len], y0 = b[i], y1 = b[(i + 1) % len], y2 = b[(i + 2) % len];
        float c0 = y0, c1 = 0.5f * (y1 - ym1), c2 = ym1 - 2.5f * y0 + 2f * y1 - 0.5f * y2, c3 = 0.5f * (y2 - ym1) + 1.5f * (y0 - y1);
        return ((c3 * f + c2) * f + c1) * f + c0;
    }
    // smooth spectral envelope (linear magnitude) from a magnitude spectrum, by cepstral liftering. mag has N/2+1 bins.
    public static void Envelope(double[] mag, int N, int Q, double[] re, double[] im, double[] env, int sr = 0)
    {
        int H = N / 2;
        double mx = 0; for (int k = 0; k <= H; k++) if (mag[k] > mx) mx = mag[k]; double fl = mx * 0.004 + 1e-9;   // a small floor so the deep gaps between harmonics do not drag the envelope down
        for (int k = 0; k <= H; k++) { re[k] = Math.Log(mag[k] + fl); im[k] = 0; if (k > 0 && k < H) { re[N - k] = re[k]; im[N - k] = 0; } }
        Fft.Transform(re, im, true);   // real cepstrum
        if (sr > 0)
        {
            // the pitch shows up as a peak in the cepstrum: cut the smoothing just below it so the harmonics never leak into the envelope
            int lo = Math.Max(8, sr / 1200), hi = Math.Min(N / 2 - 1, sr / 60); int bi = -1; double bv = 0.10; for (int n = lo; n <= hi; n++) if (re[n] > bv) { bv = re[n]; bi = n; }
            if (bi > 0) Q = Math.Max(10, Math.Min(Q, (int)(bi * 0.36)));
        }
        for (int n = 0; n < N; n++) { double w = (n <= Q || n >= N - Q) ? 1.0 : 0.0; re[n] *= w; im[n] = 0; }
        Fft.Transform(re, im, false);
        for (int k = 0; k <= H; k++) env[k] = Math.Exp(Math.Max(-18, Math.Min(18, re[k])));
    }
    // read the envelope at a fractional bin
    public static double EnvAt(double[] env, int H, double bin)
    {
        if (bin <= 0) return env[0]; if (bin >= H) return env[H]; int i = (int)bin; double f = bin - i; return env[i] + (env[i + 1] - env[i]) * f;
    }
}

// slow sweeps used by several engines (sine, triangle, saw up/down, square, random steps)
class Lfo2
{
    public static readonly string[] Shapes = { "Sine", "Triangle", "Saw up", "Saw down", "Square", "Random steps", "Random smooth" };
    double ph, cur, tgt, sm; static readonly Random rnd = new Random(7);
    public void Reset() { ph = 0; cur = tgt = sm = 0; }
    public void Sync(double phase) { ph = phase - Math.Floor(phase); }
    // advance by dPhase (cycles); returns -1..1
    public float Tick(int shape, double dPhase)
    {
        double old = ph; ph += dPhase; bool wrap = false; if (ph >= 1) { ph -= Math.Floor(ph); wrap = true; }
        switch (shape)
        {
            case 0: return (float)Math.Sin(ph * 2 * Math.PI);
            case 1: return (float)(ph < 0.5 ? ph * 4 - 1 : 3 - ph * 4);
            case 2: return (float)(ph * 2 - 1);
            case 3: return (float)(1 - ph * 2);
            case 4: return ph < 0.5 ? 1f : -1f;
            case 5: if (wrap || old == 0) cur = rnd.NextDouble() * 2 - 1; return (float)cur;
            default: if (wrap || old == 0) tgt = rnd.NextDouble() * 2 - 1; sm += (tgt - sm) * Math.Min(1, dPhase * 6); return (float)sm;
        }
    }
    public double Phase { get { return ph; } }
}

// ======================================================================
//  More shared pieces
// ======================================================================

// state-variable filter with cached coefficients (stable at any cutoff). mode 0 low-pass, 1 band-pass, 2 high-pass, 3 notch
sealed class Svf2
{
    float ic1, ic2, a1, a2, a3, k; double lastF = -1, lastQ = -1; int lastSr;
    public void Reset() { ic1 = ic2 = 0; }
    public void Set(double f, double q, int sr)
    {
        if (f == lastF && q == lastQ && sr == lastSr) return; lastF = f; lastQ = q; lastSr = sr;
        double g = Math.Tan(Math.PI * Math.Max(8.0, Math.Min(f, sr * 0.49)) / sr); k = (float)(1.0 / Math.Max(0.3, q));
        a1 = (float)(1.0 / (1.0 + g * (g + k))); a2 = (float)(g * a1); a3 = (float)(g * a2);
    }
    public float Tick(float x, int mode)
    {
        float v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3; ic1 = 2f * v1 - ic1; ic2 = 2f * v2 - ic2;
        switch (mode) { case 0: return v2; case 1: return v1 * k; case 2: return x - k * v1 - v2; default: return x - k * v1; }
    }
}

// a pair of all-pass chains that makes an in-phase and a 90 degree signal from one input (used for single-sideband shifting)
sealed class HilbertCh
{
    static readonly double[] A1 = { 0.6923878, 0.9360654322959, 0.9882295226860, 0.9987488452737 }, A2 = { 0.4021921162426, 0.8561710882420, 0.9722909545651, 0.9952884791278 };
    readonly double[] x1a = new double[4], x2a = new double[4], y1a = new double[4], y2a = new double[4], x1b = new double[4], x2b = new double[4], y1b = new double[4], y2b = new double[4]; double d;
    static double Run(double[] a, double[] x1, double[] x2, double[] y1, double[] y2, double x)
    {
        for (int k = 0; k < 4; k++) { double a2 = a[k] * a[k]; double y = a2 * (x + y2[k]) - x2[k]; x2[k] = x1[k]; x1[k] = x; y2[k] = y1[k]; y1[k] = y; x = y; }
        return x;
    }
    public void Tick(double x, out double i, out double q) { double y1 = Run(A1, x1a, x2a, y1a, y2a, x), y2 = Run(A2, x1b, x2b, y1b, y2b, x); i = d; d = y1; q = y2; }
    public void Reset() { Array.Clear(x1a, 0, 4); Array.Clear(x2a, 0, 4); Array.Clear(y1a, 0, 4); Array.Clear(y2a, 0, 4); Array.Clear(x1b, 0, 4); Array.Clear(x2b, 0, 4); Array.Clear(y1b, 0, 4); Array.Clear(y2b, 0, 4); d = 0; }
}

// base for effects that follow the song position (slicers, gates, pumps, risers)
abstract class SyncFx : Fx
{
    protected long bp; protected bool tr; protected long freeCnt;
    public override void BeginBlock(object track, long pos, int n, bool transport) { bp = pos; tr = transport; }
    protected double BeatAt(int i) { long s = tr ? bp + i : freeCnt + i; return s / (double)SR * Math.Max(20, Fx.ProjectBpm) / 60.0; }
    protected void EndBlock(int n) { freeCnt += n; }
    protected static uint Hash(uint a, uint b) { uint h = a * 2654435761u ^ (b + 0x9E3779B9u) * 2246822519u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16; return h; }
    protected static double U01(uint h) { return (h & 0xFFFFFF) / 16777216.0; }
}
