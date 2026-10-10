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
        CheckDirty(); float mix = Get("mix") / 100f; int L = Latency;
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
    public static void Envelope(double[] mag, int N, int Q, double[] re, double[] im, double[] env)
    {
        int H = N / 2;
        for (int k = 0; k <= H; k++) { re[k] = Math.Log(mag[k] + 1e-9); im[k] = 0; if (k > 0 && k < H) { re[N - k] = re[k]; im[N - k] = 0; } }
        Fft.Transform(re, im, true);   // real cepstrum
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
