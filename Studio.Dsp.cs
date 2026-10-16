using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// ======================================================================
//  Studio DSP library: filters + the effect plugins.
//  Pure C#, no dependencies. Everything here is real signal processing.
// ======================================================================

public static class DspMath
{
    public const double TwoPi = Math.PI * 2.0;
    public static float DbToLin(double db) { return (float)Math.Pow(10.0, db / 20.0); }
    public static double LinToDb(double lin) { return lin < 1e-9 ? -180.0 : 20.0 * Math.Log10(lin); }
    public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
    public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
    // time constant (ms) -> one-pole coefficient
    public static float Tc(double ms, int sr) { return ms <= 0.001 ? 0f : (float)Math.Exp(-1.0 / (ms * 0.001 * sr)); }
}

// ---------------- biquad (RBJ cookbook), double precision, stereo state ----------------
public class Biquad
{
    public const int Bell = 0, LowShelf = 1, HighShelf = 2, LowPass = 3, HighPass = 4, BandPass = 5, Notch = 6;
    public double b0 = 1, b1, b2, a1, a2;
    double z1L, z2L, z1R, z2R;

    // returns normalised {b0,b1,b2,a1,a2}
    public static double[] Coeffs(int type, double fs, double f, double q, double gDb)
    {
        f = DspMath.Clamp(f, 10, fs * 0.49); q = Math.Max(0.05, q);
        double A = Math.Pow(10, gDb / 40), w0 = DspMath.TwoPi * f / fs, cw = Math.Cos(w0), sw = Math.Sin(w0);
        double al = sw / (2 * q), b0, b1, b2, a0, a1, a2;
        switch (type)
        {
            case Bell:
                b0 = 1 + al * A; b1 = -2 * cw; b2 = 1 - al * A; a0 = 1 + al / A; a1 = -2 * cw; a2 = 1 - al / A; break;
            case LowShelf:
                { double sq = 2 * Math.Sqrt(A) * al;
                  b0 = A * ((A + 1) - (A - 1) * cw + sq); b1 = 2 * A * ((A - 1) - (A + 1) * cw); b2 = A * ((A + 1) - (A - 1) * cw - sq);
                  a0 = (A + 1) + (A - 1) * cw + sq; a1 = -2 * ((A - 1) + (A + 1) * cw); a2 = (A + 1) + (A - 1) * cw - sq; break; }
            case HighShelf:
                { double sq = 2 * Math.Sqrt(A) * al;
                  b0 = A * ((A + 1) + (A - 1) * cw + sq); b1 = -2 * A * ((A - 1) + (A + 1) * cw); b2 = A * ((A + 1) + (A - 1) * cw - sq);
                  a0 = (A + 1) - (A - 1) * cw + sq; a1 = 2 * ((A - 1) - (A + 1) * cw); a2 = (A + 1) - (A - 1) * cw - sq; break; }
            case LowPass:
                b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; a0 = 1 + al; a1 = -2 * cw; a2 = 1 - al; break;
            case HighPass:
                b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2; a0 = 1 + al; a1 = -2 * cw; a2 = 1 - al; break;
            case BandPass:
                b0 = al; b1 = 0; b2 = -al; a0 = 1 + al; a1 = -2 * cw; a2 = 1 - al; break;
            default:
                b0 = 1; b1 = -2 * cw; b2 = 1; a0 = 1 + al; a1 = -2 * cw; a2 = 1 - al; break;
        }
        return new[] { b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0 };
    }

    public static double MagDb(double[] c, double f, double fs)
    {
        double w = DspMath.TwoPi * f / fs, c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
        double nr = c[0] + c[1] * c1 + c[2] * c2, ni = -(c[1] * s1 + c[2] * s2);
        double dr = 1 + c[3] * c1 + c[4] * c2, di = -(c[3] * s1 + c[4] * s2);
        return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di) + 1e-30);
    }

    public void Set(int type, double fs, double f, double q, double gDb)
    {
        var c = Coeffs(type, fs, f, q, gDb);
        b0 = c[0]; b1 = c[1]; b2 = c[2]; a1 = c[3]; a2 = c[4];
    }

    public void SetRaw(double nb0, double nb1, double nb2, double na1, double na2) { b0 = nb0; b1 = nb1; b2 = nb2; a1 = na1; a2 = na2; }

    public void Reset() { z1L = z2L = z1R = z2R = 0; }

    public float TickL(float x)
    {
        double y = b0 * x + z1L; z1L = b1 * x - a1 * y + z2L; z2L = b2 * x - a2 * y; return (float)y;
    }
    public float TickR(float x)
    {
        double y = b0 * x + z1R; z1R = b1 * x - a1 * y + z2R; z2R = b2 * x - a2 * y; return (float)y;
    }
    public void Block(float[] l, float[] r, int n)
    {
        for (int i = 0; i < n; i++) { l[i] = TickL(l[i]); r[i] = TickR(r[i]); }
    }
}

// ---------------- parameters ----------------
public class Param
{
    public string Id, Name, Unit = ""; public float Min, Max, Def, Val; public string[] Choices;
    public Param(string id, string name, float min, float max, float def, string unit)
    { Id = id; Name = name; Min = min; Max = max; Def = def; Val = def; Unit = unit; }
    public static Param Choice(string id, string name, string[] choices, int def)
    { var p = new Param(id, name, 0, choices.Length - 1, def, ""); p.Choices = choices; return p; }
    public bool IsChoice { get { return Choices != null; } }
    public string Text
    {
        get
        {
            if (Choices != null) return Choices[(int)Math.Max(0, Math.Min(Choices.Length - 1, Math.Round(Val)))];
            string f = Math.Abs(Max - Min) >= 100 ? "0" : "0.0";
            return Val.ToString(f, CultureInfo.InvariantCulture) + (Unit.Length > 0 ? " " + Unit : "");
        }
    }
}

// ---------------- effect base ----------------
public abstract class Fx
{
    public string CurPreset = "", CurPresetData = "";   // the preset that is on this plugin (and its settings right after it was applied: if they differ now it was changed by hand)
    public string Type = "", Title = "", Category = "Plugins", Blurb = "", Error = "";   // Category / Blurb show in the effect browser; Error is set if the plugin crashes
    public bool Bypass;
    public bool AutoKeyPending;   // a pitch correction plugin that was just added: the studio finds the key of its track by itself and sets it (a plugin from a saved project keeps its key)
    public string Preset = "", PresetId = "";   // PresetId: which use of the preset it came with (the same preset can be on a track more than once, each in its own box)        // the name of the preset this plugin came with ("" = a plugin of your own); the plugin panel shows those apart
    public List<Param> P = new List<Param>();
    public float[] ScL, ScR;          // external sidechain (vocal signal), may be null
    public volatile float GR;         // gain reduction (dB, >= 0) for meters
    public int SR = 44100;
    protected volatile bool dirty = true;
    public static double ProjectBpm = 90;
    [ThreadStatic] public static double RenderBpm;
    public static double Bpm { get { return RenderBpm > 0 ? RenderBpm : ProjectBpm; } }

    public virtual void PrepareOffline() { }   // offline render: wait for background builds (e.g. convolution IR) so the first block is complete
    public virtual int Latency { get { return 0; } }
    public virtual bool WantsSidechain { get { return false; } }   // true: the host feeds the vocal track into ScL/ScR
    protected Param Add(Param p) { P.Add(p); return p; }
    protected Param AddC(string id, string name, string[] ch, int def) { return Add(Param.Choice(id, name, ch, def)); }
    protected Param AddP(string id, string name, float min, float max, float def, string unit) { return Add(new Param(id, name, min, max, def, unit)); }

    public Param Find(string id) { for (int i = 0; i < P.Count; i++) if (P[i].Id == id) return P[i]; return null; }
    public float Get(string id) { var p = Find(id); return p == null ? 0 : p.Val; }
    public void Set(string id, float v)
    {
        var p = Find(id); if (p == null) return;
        p.Val = DspMath.Clamp(v, p.Min, p.Max); dirty = true;
    }
    public void MarkDirty() { dirty = true; }

    // Called on the audio thread before processing when parameters changed.
    protected virtual void Update() { }
    // Called just before Process for every block; instruments use it to receive MIDI. transport = the song is playing (or an export is running).
    public virtual void BeginBlock(object track, long pos, int n, bool transport) { }
    public abstract void Process(float[] l, float[] r, int n);
    public virtual void Reset() { dirty = true; }
    public void Prepare(int sr) { SR = sr; dirty = true; }

    protected void CheckDirty() { if (dirty) { dirty = false; Update(); } }

    public virtual string Serialize()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < P.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(P[i].Id).Append('=').Append(P[i].Val.ToString("R", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public virtual void Deserialize(string s)
    {
        if (s == null) return;
        foreach (var part in s.Split(';'))
        {
            int i = part.IndexOf('='); if (i < 1) continue;
            float v;
            if (float.TryParse(part.Substring(i + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Set(part.Substring(0, i), v);
        }
    }

    public void ResetDefaults() { foreach (var p in P) p.Val = p.Def; dirty = true; }
    public string Hidden = "";        // comma-separated parameter ids the editor does not show (they still work and are saved)
    // Built-in plugin variants: set the starting values and make them the defaults (double-click resets to them).
    public void SetPreset(string s) { Deserialize(s); foreach (var p in P) p.Def = p.Val; dirty = true; }
}

// ---------------- compressor core (shared) ----------------
class CompCore
{
    public double Thr = -18, Ratio = 3, Knee = 6; public float Att, Rel; double env;
    public float GrDb;       // current gain reduction (>=0)
    public void Set(double thr, double ratio, double knee, double attMs, double relMs, int sr)
    { Thr = thr; Ratio = Math.Max(1, ratio); Knee = Math.Max(0, knee); Att = DspMath.Tc(attMs, sr); Rel = DspMath.Tc(relMs, sr); }
    public void Reset() { env = 0; GrDb = 0; }
    // detector: linear |level|. returns linear gain (no makeup)
    public float Step(float detLin)
    {
        double x = DspMath.LinToDb(detLin), over = x - Thr, outDb;
        if (2 * over < -Knee) outDb = x;
        else if (2 * Math.Abs(over) <= Knee) outDb = x + (1 / Ratio - 1) * (over + Knee / 2) * (over + Knee / 2) / (2 * Knee);
        else outDb = Thr + over / Ratio;
        double gr = x - outDb; if (gr < 0) gr = 0;
        if (gr > env) env = Att * env + (1 - Att) * gr; else env = Rel * env + (1 - Rel) * gr;
        GrDb = (float)env;
        return (float)Math.Pow(10, -env / 20.0);
    }
}

// ======================================================================
//  Parametric EQ: HP / LP + 6 bands (bell / low shelf / high shelf / notch)
// ======================================================================
class ParametricEq : Fx
{
    public const int Bands = 6;
    public static readonly string[] BandTypes = { "Bell", "Low shelf", "High shelf", "Notch" };
    Biquad[] bq = new Biquad[Bands]; bool[] on = new bool[Bands];
    Biquad[] hp = { new Biquad(), new Biquad() }, lp = { new Biquad(), new Biquad() };
    bool hpOn, lpOn, hp24, lp24;

    public ParametricEq()
    {
        Type = "eq"; Title = "Parametric EQ";
        AddC("hp_on", "High-pass", new[] { "Off", "On" }, 0); AddP("hp_f", "HP freq", 20, 600, 80, "Hz"); AddC("hp_s", "HP slope", new[] { "12 dB", "24 dB" }, 0);
        AddC("lp_on", "Low-pass", new[] { "Off", "On" }, 0); AddP("lp_f", "LP freq", 1000, 20000, 16000, "Hz"); AddC("lp_s", "LP slope", new[] { "12 dB", "24 dB" }, 0);
        int[] t = { 1, 0, 0, 0, 0, 2 }; float[] f = { 100, 300, 1000, 3000, 6500, 10000 };
        for (int i = 0; i < Bands; i++)
        {
            AddC("b" + i + "_on", "Band " + (i + 1), new[] { "Off", "On" }, 1);
            AddC("b" + i + "_t", "Type", BandTypes, t[i]);
            AddP("b" + i + "_f", "Freq", 20, 20000, f[i], "Hz");
            AddP("b" + i + "_g", "Gain", -18, 18, 0, "dB");
            AddP("b" + i + "_q", "Q", 0.2f, 10, 1.0f, "");
        }
        for (int i = 0; i < Bands; i++) bq[i] = new Biquad();
    }

    static int TypeMap(int t) { return t == 1 ? Biquad.LowShelf : (t == 2 ? Biquad.HighShelf : (t == 3 ? Biquad.Notch : Biquad.Bell)); }

    protected override void Update()
    {
        for (int i = 0; i < Bands; i++)
        {
            on[i] = Get("b" + i + "_on") > 0.5f;
            bq[i].Set(TypeMap((int)Get("b" + i + "_t")), SR, Get("b" + i + "_f"), Get("b" + i + "_q"), Get("b" + i + "_g"));
        }
        hpOn = Get("hp_on") > 0.5f; lpOn = Get("lp_on") > 0.5f; hp24 = Get("hp_s") > 0.5f; lp24 = Get("lp_s") > 0.5f;
        for (int i = 0; i < 2; i++)
        {
            hp[i].Set(Biquad.HighPass, SR, Get("hp_f"), 0.7071, 0);
            lp[i].Set(Biquad.LowPass, SR, Get("lp_f"), 0.7071, 0);
        }
    }

    public override void Reset() { base.Reset(); foreach (var b in bq) b.Reset(); foreach (var b in hp) b.Reset(); foreach (var b in lp) b.Reset(); }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (hpOn) { hp[0].Block(l, r, n); if (hp24) hp[1].Block(l, r, n); }
        for (int i = 0; i < Bands; i++)
            if (on[i] && (Math.Abs(P[6 + i * 5 + 3].Val) > 0.01f || P[6 + i * 5 + 1].Val > 2.5f)) bq[i].Block(l, r, n);
        if (lpOn) { lp[0].Block(l, r, n); if (lp24) lp[1].Block(l, r, n); }
    }

    // Combined magnitude response in dB (for the curve display).
    public double ResponseDb(double f)
    {
        double sum = 0;
        if (Get("hp_on") > 0.5f)
        {
            var c = Biquad.Coeffs(Biquad.HighPass, SR, Get("hp_f"), 0.7071, 0);
            sum += Biquad.MagDb(c, f, SR) * (Get("hp_s") > 0.5f ? 2 : 1);
        }
        if (Get("lp_on") > 0.5f)
        {
            var c = Biquad.Coeffs(Biquad.LowPass, SR, Get("lp_f"), 0.7071, 0);
            sum += Biquad.MagDb(c, f, SR) * (Get("lp_s") > 0.5f ? 2 : 1);
        }
        for (int i = 0; i < Bands; i++)
        {
            if (Get("b" + i + "_on") < 0.5f) continue;
            var c = Biquad.Coeffs(TypeMap((int)Get("b" + i + "_t")), SR, Get("b" + i + "_f"), Get("b" + i + "_q"), Get("b" + i + "_g"));
            sum += Biquad.MagDb(c, f, SR);
        }
        return sum;
    }
}

// ======================================================================
//  Compressor
// ======================================================================
class Compressor : Fx
{
    CompCore core = new CompCore(); Biquad schp = new Biquad(); float makeup, mix; bool ext;
    public Compressor()
    {
        Type = "comp"; Title = "Compressor";
        AddP("thr", "Threshold", -60, 0, -18, "dB"); AddP("ratio", "Ratio", 1, 20, 3, ":1");
        AddP("att", "Attack", 0.1f, 200, 10, "ms"); AddP("rel", "Release", 10, 1500, 120, "ms");
        AddP("knee", "Knee", 0, 24, 6, "dB"); AddP("makeup", "Makeup", 0, 24, 0, "dB");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("schp", "Sidechain HP", 20, 500, 60, "Hz");
        AddC("ext", "Detector", new[] { "Internal", "External" }, 0);
    }
    protected override void Update()
    {
        core.Set(Get("thr"), Get("ratio"), Get("knee"), Get("att"), Get("rel"), SR);
        makeup = DspMath.DbToLin(Get("makeup")); mix = Get("mix") / 100f; ext = Get("ext") > 0.5f;
        schp.Set(Biquad.HighPass, SR, Get("schp"), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); core.Reset(); schp.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); float peakGr = 0;
        bool useSc = ext && ScL != null;
        for (int i = 0; i < n; i++)
        {
            float dl = useSc ? ScL[i] : l[i], dr = useSc ? ScR[i] : r[i];
            float d = Math.Max(Math.Abs(schp.TickL(dl)), Math.Abs(schp.TickR(dr)));
            float g = core.Step(d) * makeup;
            float ol = l[i] * g, or = r[i] * g;
            l[i] = l[i] + (ol - l[i]) * mix; r[i] = r[i] + (or - r[i]) * mix;
            if (core.GrDb > peakGr) peakGr = core.GrDb;
        }
        GR = peakGr;
    }
}

// ======================================================================
//  Multiband compressor (3 bands, Linkwitz-Riley 4th order crossovers)
// ======================================================================
class MultibandComp : Fx
{
    Biquad[] lo1 = { new Biquad(), new Biquad() }, hi1 = { new Biquad(), new Biquad() };
    Biquad[] lo2 = { new Biquad(), new Biquad() }, hi2 = { new Biquad(), new Biquad() };
    Biquad[] apL = { new Biquad(), new Biquad() }, apH = { new Biquad(), new Biquad() };   // phase-align the low band
    CompCore[] cc = { new CompCore(), new CompCore(), new CompCore() }; float[] mk = new float[3]; float outG, mix;
    float[] bl = new float[4096], br = new float[4096], ml = new float[4096], mr = new float[4096], hl = new float[4096], hr = new float[4096];
    public MultibandComp()
    {
        Type = "mbcomp"; Title = "Multiband Compressor";
        AddP("xo1", "Low / Mid", 60, 800, 180, "Hz"); AddP("xo2", "Mid / High", 1000, 12000, 3500, "Hz");
        string[] nm = { "Low", "Mid", "High" }; float[] thr = { -22, -24, -26 }, rat = { 2.5f, 2.5f, 2.5f };
        for (int b = 0; b < 3; b++)
        {
            AddP("t" + b, nm[b] + " thresh", -60, 0, thr[b], "dB"); AddP("r" + b, nm[b] + " ratio", 1, 20, rat[b], ":1");
            AddP("a" + b, nm[b] + " attack", 0.5f, 200, b == 0 ? 25 : (b == 1 ? 12 : 5), "ms");
            AddP("l" + b, nm[b] + " release", 20, 1500, b == 0 ? 180 : 120, "ms"); AddP("m" + b, nm[b] + " makeup", 0, 18, 0, "dB");
        }
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -12, 12, 0, "dB");
    }
    protected override void Update()
    {
        double x1 = Get("xo1"), x2 = Get("xo2");
        for (int i = 0; i < 2; i++)
        {
            lo1[i].Set(Biquad.LowPass, SR, x1, 0.7071, 0); hi1[i].Set(Biquad.HighPass, SR, x1, 0.7071, 0);
            lo2[i].Set(Biquad.LowPass, SR, x2, 0.7071, 0); hi2[i].Set(Biquad.HighPass, SR, x2, 0.7071, 0);
            apL[i].Set(Biquad.LowPass, SR, x2, 0.7071, 0); apH[i].Set(Biquad.HighPass, SR, x2, 0.7071, 0);
        }
        for (int b = 0; b < 3; b++)
        {
            cc[b].Set(Get("t" + b), Get("r" + b), 6, Get("a" + b), Get("l" + b), SR); mk[b] = DspMath.DbToLin(Get("m" + b));
        }
        outG = DspMath.DbToLin(Get("out")); mix = Get("mix") / 100f;
    }
    public override void Reset() { base.Reset(); foreach (var b in lo1) b.Reset(); foreach (var b in hi1) b.Reset(); foreach (var b in lo2) b.Reset(); foreach (var b in hi2) b.Reset(); foreach (var b in apL) b.Reset(); foreach (var b in apH) b.Reset(); foreach (var c in cc) c.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (n > bl.Length) { bl = new float[n]; br = new float[n]; ml = new float[n]; mr = new float[n]; hl = new float[n]; hr = new float[n]; }
        // low band = LR4 low of x1, then all-pass aligned by (LP2 + HP2) of itself
        Array.Copy(l, bl, n); Array.Copy(r, br, n); Array.Copy(l, hl, n); Array.Copy(r, hr, n);
        lo1[0].Block(bl, br, n); lo1[1].Block(bl, br, n);
        hi1[0].Block(hl, hr, n); hi1[1].Block(hl, hr, n);
        Array.Copy(hl, ml, n); Array.Copy(hr, mr, n);
        lo2[0].Block(ml, mr, n); lo2[1].Block(ml, mr, n);
        hi2[0].Block(hl, hr, n); hi2[1].Block(hl, hr, n);
        float peak = 0;
        for (int i = 0; i < n; i++)
        {
            // all-pass alignment of the low band: AP(x) = LP4(x) + HP4(x) using the x2 pair
            float al = apL[0].TickL(bl[i]); al = apL[1].TickL(al); float ah = apH[0].TickL(bl[i]); ah = apH[1].TickL(ah);
            float ar = apL[0].TickR(br[i]); ar = apL[1].TickR(ar); float ahr = apH[0].TickR(br[i]); ahr = apH[1].TickR(ahr);
            float lowL = al + ah, lowR = ar + ahr;
            float gl = cc[0].Step(Math.Max(Math.Abs(lowL), Math.Abs(lowR))) * mk[0];
            float gm = cc[1].Step(Math.Max(Math.Abs(ml[i]), Math.Abs(mr[i]))) * mk[1];
            float gh = cc[2].Step(Math.Max(Math.Abs(hl[i]), Math.Abs(hr[i]))) * mk[2];
            float wl = (lowL * gl + ml[i] * gm + hl[i] * gh) * outG, wr = (lowR * gl + mr[i] * gm + hr[i] * gh) * outG;
            l[i] = l[i] + (wl - l[i]) * mix; r[i] = r[i] + (wr - r[i]) * mix;
            float m = Math.Max(cc[0].GrDb, Math.Max(cc[1].GrDb, cc[2].GrDb)); if (m > peak) peak = m;
        }
        GR = peak;
    }
    public float BandGr(int b) { return cc[b].GrDb; }
}

// ======================================================================
//  Exciter: harmonics generated from a frequency range, blended with the dry signal
// ======================================================================
class Exciter : Fx
{
    Biquad[] hpf = { new Biquad(), new Biquad() }, lpf = { new Biquad(), new Biquad() }, post = { new Biquad(), new Biquad() };
    double driveG, harm, mix, outG;
    public Exciter()
    {
        Type = "exciter"; Title = "Exciter";
        AddP("drive", "Drive", 0, 30, 8, "dB"); AddP("harm", "Harmonics", 0, 100, 45, "%");
        AddP("flo", "Range low", 500, 12000, 3000, "Hz"); AddP("fhi", "Range high", 2000, 20000, 16000, "Hz");
        AddP("mix", "Wet / Dry", 0, 100, 40, "%"); AddP("out", "Output", -12, 12, 0, "dB");
    }
    protected override void Update()
    {
        double flo = Get("flo"), fhi = Math.Max(Get("fhi"), flo * 1.2);
        for (int i = 0; i < 2; i++)
        {
            hpf[i].Set(Biquad.HighPass, SR, flo, 0.7071, 0); lpf[i].Set(Biquad.LowPass, SR, fhi, 0.7071, 0);
            post[i].Set(Biquad.HighPass, SR, flo, 0.7071, 0);
        }
        driveG = Math.Pow(10, Get("drive") / 20.0); harm = Get("harm") / 100.0; mix = Get("mix") / 100.0; outG = DspMath.DbToLin(Get("out"));
    }
    public override void Reset() { base.Reset(); for (int i = 0; i < 2; i++) { hpf[i].Reset(); lpf[i].Reset(); post[i].Reset(); } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            float bl = lpf[0].TickL(hpf[0].TickL(l[i])), br = lpf[0].TickR(hpf[0].TickR(r[i]));
            // asymmetric soft saturation: odd + even harmonics; DC / low products removed afterwards
            float sl = (float)(Math.Tanh(driveG * bl + 0.18 * driveG * bl * Math.Abs(bl)) - 0.0);
            float sr = (float)(Math.Tanh(driveG * br + 0.18 * driveG * br * Math.Abs(br)) - 0.0);
            sl = post[0].TickL(sl); sr = post[0].TickR(sr);
            float ex = (float)(harm * 0.55);
            l[i] = (float)((l[i] + sl * ex * mix) * outG);
            r[i] = (float)((r[i] + sr * ex * mix) * outG);
        }
    }
}

// ======================================================================
//  Saturation (tape / tube / soft clip)
// ======================================================================
class Saturation : Fx
{
    double drive, mix, outG, toneLp; double dcL, dcR, lpL, lpR; double lpA;
    public Saturation()
    {
        Type = "sat"; Title = "Saturation";
        AddC("mode", "Character", new[] { "Tape", "Tube", "Soft clip" }, 0); AddP("drive", "Drive", 0, 30, 8, "dB");
        AddP("tone", "Tone", 500, 20000, 12000, "Hz"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -12, 12, 0, "dB");
    }
    protected override void Update()
    {
        drive = Math.Pow(10, Get("drive") / 20.0); mix = Get("mix") / 100.0; outG = DspMath.DbToLin(Get("out"));
        lpA = 1 - Math.Exp(-DspMath.TwoPi * Get("tone") / SR);
    }
    double Shape(double x, int mode)
    {
        if (mode == 0) return Math.Tanh(x);
        if (mode == 1) { double b = x + 0.12; double y = b / (1 + Math.Abs(b)); return y - 0.12 / 1.12; }
        double a = DspMath.Clamp(x, -1.5, 1.5); return a - a * a * a / 6.75;
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode");
        double comp = 1.0 / Math.Max(1.0, Math.Sqrt(drive));
        for (int i = 0; i < n; i++)
        {
            double yl = Shape(l[i] * drive, mode) * comp, yr = Shape(r[i] * drive, mode) * comp;
            // DC blocker (asymmetric shapers create DC)
            double ol = yl - dcL; dcL += ol * 0.0015; double or = yr - dcR; dcR += or * 0.0015;
            lpL += lpA * (ol - lpL); lpR += lpA * (or - lpR);
            l[i] = (float)((l[i] * (1 - mix) + lpL * mix) * outG);
            r[i] = (float)((r[i] * (1 - mix) + lpR * mix) * outG);
        }
    }
}

// ======================================================================
//  Distortion
// ======================================================================
class Distortion : Fx
{
    double drive, mix, lvl, a; double lpL, lpR;
    public Distortion()
    {
        Type = "dist"; Title = "Distortion";
        AddC("mode", "Type", new[] { "Hard clip", "Foldback", "Fuzz" }, 0); AddP("drive", "Drive", 0, 40, 14, "dB");
        AddP("tone", "Tone", 500, 16000, 6000, "Hz"); AddP("mix", "Mix", 0, 100, 60, "%"); AddP("level", "Level", -18, 6, -6, "dB");
    }
    protected override void Update()
    {
        drive = Math.Pow(10, Get("drive") / 20.0); mix = Get("mix") / 100.0; lvl = DspMath.DbToLin(Get("level"));
        a = 1 - Math.Exp(-DspMath.TwoPi * Get("tone") / SR);
    }
    double Shape(double x, int m)
    {
        if (m == 0) return DspMath.Clamp(x, -1, 1);
        if (m == 1) { double t = x; for (int k = 0; k < 4 && Math.Abs(t) > 1; k++) t = t > 1 ? 2 - t : (t < -1 ? -2 - t : t); return t; }
        return Math.Tanh(x * 1.5 + 0.3) - Math.Tanh(0.3);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int m = (int)Get("mode");
        for (int i = 0; i < n; i++)
        {
            double yl = Shape(l[i] * drive, m), yr = Shape(r[i] * drive, m);
            lpL += a * (yl - lpL); lpR += a * (yr - lpR);
            l[i] = (float)((l[i] * (1 - mix) + lpL * lvl * mix)); r[i] = (float)((r[i] * (1 - mix) + lpR * lvl * mix));
        }
    }
}

// ======================================================================
//  Reverb (Freeverb: 8 parallel combs + 4 series all-passes per channel)
// ======================================================================
class Reverb : Fx
{
    static readonly int[] CombT = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 }, AllT = { 556, 441, 341, 225 };
    float[][] cbL = new float[8][], cbR = new float[8][], abL = new float[4][], abR = new float[4][];
    int[] ciL = new int[8], ciR = new int[8], aiL = new int[4], aiR = new int[4]; float[] fsL = new float[8], fsR = new float[8];
    float[] pre = new float[44100 / 2]; int pw; Biquad wetHp = new Biquad(), wetLp = new Biquad();
    float fb, damp, wet, dry, width; int preN;
    public Reverb()
    {
        Type = "reverb"; Title = "Reverb";
        AddP("size", "Room size", 0, 100, 55, "%"); AddP("damp", "Damping", 0, 100, 45, "%");
        AddP("pre", "Pre-delay", 0, 200, 20, "ms"); AddP("width", "Width", 0, 100, 100, "%");
        AddP("lc", "Low cut", 20, 1000, 200, "Hz"); AddP("hc", "High cut", 2000, 20000, 10000, "Hz");
        AddP("mix", "Mix", 0, 100, 20, "%");
        for (int i = 0; i < 8; i++) { cbL[i] = new float[CombT[i]]; cbR[i] = new float[CombT[i] + 23]; }
        for (int i = 0; i < 4; i++) { abL[i] = new float[AllT[i]]; abR[i] = new float[AllT[i] + 23]; }
    }
    protected override void Update()
    {
        fb = 0.70f + 0.28f * Get("size") / 100f; damp = Get("damp") / 100f * 0.8f;
        wet = Get("mix") / 100f; dry = 1 - wet * 0.5f; width = Get("width") / 100f; preN = (int)(Get("pre") * 0.001 * SR);
        wetHp.Set(Biquad.HighPass, SR, Get("lc"), 0.7071, 0); wetLp.Set(Biquad.LowPass, SR, Get("hc"), 0.7071, 0);
    }
    public override void Reset()
    {
        base.Reset();
        for (int i = 0; i < 8; i++) { Array.Clear(cbL[i], 0, cbL[i].Length); Array.Clear(cbR[i], 0, cbR[i].Length); fsL[i] = fsR[i] = 0; }
        for (int i = 0; i < 4; i++) { Array.Clear(abL[i], 0, abL[i].Length); Array.Clear(abR[i], 0, abR[i].Length); }
        Array.Clear(pre, 0, pre.Length); wetHp.Reset(); wetLp.Reset();
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int s = 0; s < n; s++)
        {
            float inp = (l[s] + r[s]) * 0.015f;
            pre[pw] = inp; int rp = pw - preN; if (rp < 0) rp += pre.Length; inp = pre[rp]; pw++; if (pw >= pre.Length) pw = 0;
            float oL = 0, oR = 0;
            for (int i = 0; i < 8; i++)
            {
                float yl = cbL[i][ciL[i]]; fsL[i] = yl * (1 - damp) + fsL[i] * damp; cbL[i][ciL[i]] = inp + fsL[i] * fb; if (++ciL[i] >= cbL[i].Length) ciL[i] = 0; oL += yl;
                float yr = cbR[i][ciR[i]]; fsR[i] = yr * (1 - damp) + fsR[i] * damp; cbR[i][ciR[i]] = inp + fsR[i] * fb; if (++ciR[i] >= cbR[i].Length) ciR[i] = 0; oR += yr;
            }
            for (int i = 0; i < 4; i++)
            {
                float bl = abL[i][aiL[i]]; abL[i][aiL[i]] = oL + bl * 0.5f; oL = bl - oL; if (++aiL[i] >= abL[i].Length) aiL[i] = 0;
                float br = abR[i][aiR[i]]; abR[i][aiR[i]] = oR + br * 0.5f; oR = br - oR; if (++aiR[i] >= abR[i].Length) aiR[i] = 0;
            }
            oL = wetLp.TickL(wetHp.TickL(oL)); oR = wetLp.TickR(wetHp.TickR(oR));
            float wl = oL * (0.5f + 0.5f * width) + oR * (0.5f - 0.5f * width), wr = oR * (0.5f + 0.5f * width) + oL * (0.5f - 0.5f * width);
            l[s] = l[s] * dry + wl * wet * 3f; r[s] = r[s] * dry + wr * wet * 3f;
        }
    }
}

// ======================================================================
//  Delay (mono / stereo / ping-pong, tempo sync)
// ======================================================================
class Delay : Fx
{
    public static readonly string[] Syncs = { "Free (ms)", "1/4", "1/8", "1/8 dotted", "1/8 triplet", "1/16" };
    static readonly double[] SyncBeats = { 0, 1, 0.5, 0.75, 1.0 / 3.0, 0.25 };
    float[] bl, br; int w; Biquad lc = new Biquad(), hc = new Biquad(); double fb, mix; int dl, dr; int mode;
    public Delay()
    {
        Type = "delay"; Title = "Delay";
        AddC("mode", "Mode", new[] { "Mono", "Stereo", "Ping-pong" }, 2); AddC("sync", "Time sync", Syncs, 0);
        AddP("time", "Time", 10, 1500, 300, "ms"); AddP("fb", "Feedback", 0, 95, 35, "%");
        AddP("lc", "Low cut", 20, 1000, 150, "Hz"); AddP("hc", "High cut", 1000, 20000, 6000, "Hz"); AddP("mix", "Mix", 0, 100, 22, "%");
    }
    protected override void Update()
    {
        int len = SR * 2 + 16; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; }
        int sy = (int)Get("sync"); double ms = Get("time");
        if (sy > 0) ms = 60000.0 / Bpm * SyncBeats[sy];
        dl = (int)Math.Max(1, Math.Min(len - 2, ms * 0.001 * SR));
        mode = (int)Get("mode"); dr = mode == 1 ? (int)(dl * 1.12) : dl;
        fb = Get("fb") / 100.0; mix = Get("mix") / 100.0;
        lc.Set(Biquad.HighPass, SR, Get("lc"), 0.7071, 0); hc.Set(Biquad.LowPass, SR, Get("hc"), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, bl.Length); Array.Clear(br, 0, br.Length); } lc.Reset(); hc.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int len = bl.Length;
        for (int i = 0; i < n; i++)
        {
            int rl = w - dl; if (rl < 0) rl += len; int rr = w - dr; if (rr < 0) rr += len;
            float tl = bl[rl], tr = br[rr];
            float fl, fr;
            if (mode == 2) { fl = tr; fr = tl; } else { fl = tl; fr = tr; }
            fl = hc.TickL(lc.TickL(fl)); fr = hc.TickR(lc.TickR(fr));
            float inL = l[i], inR = r[i];
            if (mode == 0) { float m = (inL + inR) * 0.5f; inL = m; inR = m; }
            if (mode == 2) { float m = (inL + inR) * 0.5f; bl[w] = m + fl * (float)fb; br[w] = fr * (float)fb; }
            else { bl[w] = inL + fl * (float)fb; br[w] = inR + fr * (float)fb; }
            l[i] += (float)(tl * mix); r[i] += (float)(tr * mix);
            if (++w >= len) w = 0;
        }
    }
}

// ======================================================================
//  Chorus
// ======================================================================
class Chorus : Fx
{
    float[] bl = new float[4096], br = new float[4096]; int w; double ph; double rate, depth, delay, mix, spread;
    public Chorus()
    {
        Type = "chorus"; Title = "Chorus";
        AddP("rate", "Rate", 0.05f, 5, 0.8f, "Hz"); AddP("depth", "Depth", 0, 10, 3, "ms"); AddP("delay", "Delay", 5, 30, 14, "ms");
        AddP("spread", "Stereo spread", 0, 100, 70, "%"); AddP("mix", "Mix", 0, 100, 40, "%");
    }
    protected override void Update() { rate = Get("rate"); depth = Get("depth") * 0.001 * SR; delay = Get("delay") * 0.001 * SR; spread = Get("spread") / 100.0 * 0.5; mix = Get("mix") / 100.0; }
    public override void Reset() { base.Reset(); Array.Clear(bl, 0, bl.Length); Array.Clear(br, 0, br.Length); }
    float Tap(float[] b, double d)
    {
        double rp = w - d; while (rp < 0) rp += b.Length; int i0 = (int)rp, i1 = (i0 + 1) % b.Length; double f = rp - i0;
        return (float)(b[i0 % b.Length] * (1 - f) + b[i1] * f);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            bl[w] = l[i]; br[w] = r[i];
            double lfoL = Math.Sin(DspMath.TwoPi * ph), lfoR = Math.Sin(DspMath.TwoPi * (ph + spread));
            float cl = Tap(bl, delay + depth * lfoL), cr = Tap(br, delay + depth * lfoR);
            l[i] = (float)(l[i] * (1 - mix * 0.5) + cl * mix); r[i] = (float)(r[i] * (1 - mix * 0.5) + cr * mix);
            ph += rate / SR; if (ph >= 1) ph -= 1; if (++w >= bl.Length) w = 0;
        }
    }
}

// ======================================================================
//  De-esser (band detector + reduction applied to the sibilant band)
// ======================================================================
class DeEsser : Fx
{
    Biquad det = new Biquad(), bandA = new Biquad(), bandB = new Biquad(); double env; float att, rel; double thr, range;
    public DeEsser()
    {
        Type = "deess"; Title = "De-esser";
        AddP("freq", "Frequency", 3000, 12000, 6500, "Hz"); AddP("thr", "Threshold", -50, 0, -28, "dB");
        AddP("range", "Max reduction", 0, 24, 10, "dB"); AddC("mode", "Mode", new[] { "Split band", "Wide band" }, 0);
    }
    protected override void Update()
    {
        det.Set(Biquad.BandPass, SR, Get("freq"), 1.2, 0); bandA.Set(Biquad.BandPass, SR, Get("freq"), 0.9, 0);
        thr = Get("thr"); range = Get("range"); att = DspMath.Tc(0.4, SR); rel = DspMath.Tc(40, SR);
    }
    public override void Reset() { base.Reset(); det.Reset(); bandA.Reset(); env = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool wide = Get("mode") > 0.5f; float peak = 0;
        for (int i = 0; i < n; i++)
        {
            float d = Math.Max(Math.Abs(det.TickL(l[i])), Math.Abs(det.TickR(r[i])));
            double db = DspMath.LinToDb(d), want = Math.Max(0, Math.Min(range, (db - thr) * 0.85));
            if (want > env) env = att * env + (1 - att) * want; else env = rel * env + (1 - rel) * want;
            float g = (float)Math.Pow(10, -env / 20.0); if ((float)env > peak) peak = (float)env;
            if (wide) { l[i] *= g; r[i] *= g; }
            else
            {
                float bl = bandA.TickL(l[i]), br = bandA.TickR(r[i]);
                l[i] -= bl * (1 - g); r[i] -= br * (1 - g);
            }
        }
        GR = peak;
    }
}

// ======================================================================
//  Stereo widener / imager (mid/side) with bass mono
// ======================================================================
class Widener : Fx
{
    Biquad sideHp = new Biquad(); double width, midG, sideG; bool bassMono;
    public Widener()
    {
        Type = "widener"; Title = "Stereo Imager";
        AddP("width", "Width", 0, 200, 120, "%"); AddP("mid", "Mid level", -12, 12, 0, "dB"); AddP("side", "Side level", -12, 12, 0, "dB");
        AddP("bass", "Mono below", 0, 400, 120, "Hz");
    }
    protected override void Update()
    {
        width = Get("width") / 100.0; midG = DspMath.DbToLin(Get("mid")); sideG = DspMath.DbToLin(Get("side"));
        bassMono = Get("bass") > 20; if (bassMono) sideHp.Set(Biquad.HighPass, SR, Get("bass"), 0.7071, 0);
    }
    public override void Reset() { base.Reset(); sideHp.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            float m = (l[i] + r[i]) * 0.5f, s = (l[i] - r[i]) * 0.5f;
            if (bassMono) s = sideHp.TickL(s);
            s *= (float)(width * sideG); m *= (float)midG;
            l[i] = m + s; r[i] = m - s;
        }
    }
}

// ======================================================================
//  Gate / expander
// ======================================================================
class Gate : Fx
{
    double env, g = 1; float att, rel; double thr, range; int hold, holdCnt;
    public Gate()
    {
        Type = "gate"; Title = "Noise Gate";
        AddP("thr", "Threshold", -80, 0, -50, "dB"); AddP("range", "Range", 0, 80, 40, "dB");
        AddP("att", "Attack", 0.1f, 50, 2, "ms"); AddP("hold", "Hold", 0, 500, 60, "ms"); AddP("rel", "Release", 10, 1500, 250, "ms");
    }
    protected override void Update() { thr = Get("thr"); range = Get("range"); att = DspMath.Tc(Get("att"), SR); rel = DspMath.Tc(Get("rel"), SR); hold = (int)(Get("hold") * 0.001 * SR); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double floor = Math.Pow(10, -range / 20.0);
        for (int i = 0; i < n; i++)
        {
            double lev = Math.Max(Math.Abs(l[i]), Math.Abs(r[i]));
            env = lev > env ? lev : env * 0.9995 + lev * 0.0005;
            bool open = DspMath.LinToDb(env) > thr;
            if (open) holdCnt = hold; else if (holdCnt > 0) { holdCnt--; open = true; }
            double target = open ? 1.0 : floor;
            g = target > g ? att * g + (1 - att) * target : rel * g + (1 - rel) * target;
            l[i] *= (float)g; r[i] *= (float)g;
        }
        GR = (float)(-DspMath.LinToDb(g));
    }
}

// ======================================================================
//  Sidechain ducker: lowers the beat when the vocal (sidechain) is present
// ======================================================================
class Ducker : Fx
{
    Biquad scHp = new Biquad(), band = new Biquad(); double env; float att, rel; double amount, thr;
    public Ducker()
    {
        Type = "duck"; Title = "Vocal Ducker (sidechain)";
        AddP("amount", "Reduction", 0, 18, 3, "dB"); AddP("thr", "Threshold", -60, 0, -32, "dB");
        AddP("att", "Attack", 1, 100, 12, "ms"); AddP("rel", "Release", 30, 1500, 220, "ms");
        AddC("band", "Affects", new[] { "Full band", "Mids only (keep bass)" }, 1); AddP("focus", "Detector HP", 50, 600, 150, "Hz");
    }
    protected override void Update()
    {
        amount = Get("amount"); thr = Get("thr"); att = DspMath.Tc(Get("att"), SR); rel = DspMath.Tc(Get("rel"), SR);
        scHp.Set(Biquad.HighPass, SR, Get("focus"), 0.7071, 0); band.Set(Biquad.BandPass, SR, 1100, 0.45, 0);
    }
    public override void Reset() { base.Reset(); scHp.Reset(); band.Reset(); env = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (ScL == null) { GR = 0; return; }
        bool midsOnly = Get("band") > 0.5f; float peak = 0;
        for (int i = 0; i < n; i++)
        {
            float d = Math.Max(Math.Abs(scHp.TickL(ScL[i])), Math.Abs(ScR != null ? Math.Abs(scHp.TickR(ScR[i])) : 0));
            double db = DspMath.LinToDb(d), want = DspMath.Clamp((db - thr) / 8.0, 0, 1) * amount;
            if (want > env) env = att * env + (1 - att) * want; else env = rel * env + (1 - rel) * want;
            float g = (float)Math.Pow(10, -env / 20.0); if ((float)env > peak) peak = (float)env;
            if (midsOnly)
            {
                float bl = band.TickL(l[i]), br = band.TickR(r[i]);
                l[i] -= bl * (1 - g); r[i] -= br * (1 - g);
            }
            else { l[i] *= g; r[i] *= g; }
        }
        GR = peak;
    }
}

// ======================================================================
//  Lookahead brick-wall limiter with optional 4x true-peak detection
// ======================================================================
class Limiter : Fx
{
    int N;                       // lookahead (samples)
    float[] dl, dr, req, mn; double[] boxBuf; int pos; double boxSum; double relG = 1; float inG, ceil, relCoef; bool tp;
    float[] prevL = new float[12], prevR = new float[12]; float[][] phase = new float[3][];
    public override int Latency { get { return Math.Max(16, (int)(0.004 * SR)); } }   // known before the first block is processed, so delay compensation is right from the start
    public Limiter()
    {
        Type = "limiter"; Title = "Limiter";
        AddP("ceil", "Ceiling", -12, 0, -1, "dB"); AddP("gain", "Input gain", 0, 24, 0, "dB"); AddP("rel", "Release", 10, 500, 80, "ms");
        AddC("tp", "True-peak detect", new[] { "Off", "On" }, 1);
        // 12-tap windowed-sinc polyphase interpolator phases for 4x oversampling (0.25, 0.5, 0.75)
        for (int p = 1; p <= 3; p++)
        {
            phase[p - 1] = new float[12];
            for (int k = 0; k < 12; k++)
            {
                double x = (k - 5.5 + 0) - p / 4.0 + 0.0; x = (k - 5) - p / 4.0;
                double s = Math.Abs(x) < 1e-9 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
                double wv = 0.5 * (1 + Math.Cos(Math.PI * x / 6.5));
                phase[p - 1][k] = (float)(s * wv);
            }
        }
    }
    protected override void Update()
    {
        N = Math.Max(16, (int)(0.004 * SR));
        if (dl == null || dl.Length != N + 1)
        {
            dl = new float[N + 1]; dr = new float[N + 1]; req = new float[N + 1]; mn = new float[N + 1]; boxBuf = new double[N + 1]; pos = 0; boxSum = 0;
            for (int i = 0; i <= N; i++) { req[i] = 1; mn[i] = 1; boxBuf[i] = 1; } boxSum = N + 1;
        }
        inG = DspMath.DbToLin(Get("gain")); ceil = DspMath.DbToLin(Get("ceil")); relCoef = (float)(1 - Math.Exp(-1.0 / (Get("rel") * 0.001 * SR))); tp = Get("tp") > 0.5f;
    }
    public override void Reset() { base.Reset(); dl = null; relG = 1; Array.Clear(prevL, 0, 12); Array.Clear(prevR, 0, 12); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); float maxGr = 0;
        // read the rings once: a Reset from another thread mid-block nulls the fields, but these locals stay valid
        var dl = this.dl; var dr = this.dr; var req = this.req; var boxBuf = this.boxBuf;
        for (int i = 0; i < n; i++)
        {
            float xl = l[i] * inG, xr = r[i] * inG;
            // peak (optionally 4x oversampled)
            float pk = Math.Max(Math.Abs(xl), Math.Abs(xr));
            if (tp)
            {
                Array.Copy(prevL, 1, prevL, 0, 11); prevL[11] = xl; Array.Copy(prevR, 1, prevR, 0, 11); prevR[11] = xr;
                for (int p = 0; p < 3; p++)
                {
                    float sl = 0, sr = 0; var f = phase[p];
                    for (int k = 0; k < 12; k++) { sl += prevL[k] * f[k]; sr += prevR[k] * f[k]; }
                    float a = Math.Max(Math.Abs(sl), Math.Abs(sr)); if (a > pk) pk = a;
                }
            }
            float rq = pk > ceil ? ceil / pk : 1f;
            // write into lookahead ring
            dl[pos] = xl; dr[pos] = xr; req[pos] = rq;
            // min over window of the last N+1 requirements (sample at pos is the newest; output sample is the oldest)
            float m = 1f; for (int k = 0; k <= N; k++) if (req[k] < m) m = req[k];
            int oldest = pos + 1; if (oldest > N) oldest = 0;
            // box-car average of the min-filtered gain keeps the attack smooth but guarantees the ceiling
            boxSum += m - boxBuf[pos]; boxBuf[pos] = m; double g = boxSum / (N + 1);
            // release: recover slowly, never exceed the required gain
            relG = g < relG ? g : relG + (1 - relG) * relCoef; if (relG > g) relG = g;
            l[i] = (float)(dl[oldest] * relG); r[i] = (float)(dr[oldest] * relG);
            float gr = (float)(-DspMath.LinToDb(relG)); if (gr > maxGr) maxGr = gr;
            pos = oldest;
        }
        GR = maxGr;
    }
}


// ======================================================================
//  8D Audio Tuner: makes the sound orbit around the listener's head (best on headphones).
//  Cues used: left/right pan, interaural time difference, head-shadow low-pass on the far ear,
//  and a "behind you" level + brightness drop. Bass can stay centred. Hold mode parks the
//  source at a fixed angle so you can tune the position by ear.
// ======================================================================
class Spatial8D : Fx
{
    public volatile float AngleDeg;   // current source angle for the on-screen orbit (0 front, 90 right, 180 back, 270 left)
    double phase; float[] dL = new float[256], dR = new float[256]; int dw; double lpFarL, lpFarR, lpBackL, lpBackR, bassState;
    Reverb rv = new Reverb(); float[] sl = new float[4096], sr = new float[4096];
    double period, width, depth, itd, bassHz, mix, startDeg, holdDeg; int dirMode; bool hold; float room;

    public Spatial8D()
    {
        Type = "8d"; Title = "8D Audio Tuner";
        AddP("period", "Rotation time", 1.5f, 60, 10, "s"); AddC("dir", "Direction", new[] { "Clockwise", "Counter-clockwise", "Back and forth" }, 0);
        AddP("width", "Width", 0, 100, 100, "%"); AddP("depth", "Behind = darker", 0, 100, 60, "%"); AddP("itd", "Ear delay", 0, 100, 80, "%");
        AddP("bass", "Centre bass below", 0, 300, 120, "Hz"); AddP("room", "Room", 0, 60, 15, "%");
        AddP("start", "Start angle", 0, 360, 0, "deg"); AddC("hold", "Hold position", new[] { "Rotate", "Hold" }, 0); AddP("angle", "Hold angle", 0, 360, 0, "deg");
        AddP("mix", "Mix", 0, 100, 100, "%");
        rv.Prepare(StudioEngine.SR);
    }

    protected override void Update()
    {
        period = Get("period"); dirMode = (int)Get("dir"); width = Get("width") / 100.0; depth = Get("depth") / 100.0; itd = Get("itd") / 100.0;
        bassHz = Get("bass"); mix = Get("mix") / 100.0; startDeg = Get("start"); hold = Get("hold") > 0.5f; holdDeg = Get("angle"); room = Get("room");
        rv.Set("mix", room); rv.Set("size", 40); rv.Set("damp", 60); rv.Set("pre", 8); rv.Set("lc", 150); rv.Set("hc", 9000);
    }

    public override void Reset() { base.Reset(); Array.Clear(dL, 0, dL.Length); Array.Clear(dR, 0, dR.Length); lpFarL = lpFarR = lpBackL = lpBackR = bassState = 0; rv.Reset(); }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        if (n > sl.Length) { sl = new float[n]; sr = new float[n]; }
        double sr_ = SR, bassA = bassHz > 20 ? 1 - Math.Exp(-2 * Math.PI * bassHz / sr_) : 0;
        double dPhase = period > 0 ? 1.0 / (period * sr_) : 0; if (dirMode == 1) dPhase = -dPhase;
        double a = 0, aBack = 1, farA = 1, backA = 1; int blk = 0;
        for (int i = 0; i < n; i++)
        {
            float inL = l[i], inR = r[i]; double m = 0.5 * (inL + inR), side = 0.5 * (inL - inR);
            double bass = 0; if (bassA > 0) { bassState += bassA * (m - bassState); bass = bassState; m -= bass; }
            // source angle
            double ang;
            if (hold) ang = holdDeg * Math.PI / 180.0;
            else
            {
                phase += dPhase; if (phase > 1) phase -= 1; else if (phase < 0) phase += 1;
                ang = startDeg * Math.PI / 180.0 + (dirMode == 2 ? Math.Sin(2 * Math.PI * phase) * Math.PI * 0.5 : 2 * Math.PI * phase);
            }
            double pan = Math.Sin(ang) * width, behind = (1 - Math.Cos(ang)) * 0.5;      // behind: 0 front .. 1 back
            if ((i & 15) == 0)
            {
                // per-block coefficients: far-ear head shadow and behind darkening
                double farFc = 18000 - 14500 * Math.Abs(pan) * (0.4 + 0.6 * depth); farA = 1 - Math.Exp(-2 * Math.PI * Math.Max(1500, farFc) / sr_);
                double backFc = 20000 - 15000 * behind * depth; backA = 1 - Math.Exp(-2 * Math.PI * Math.Max(2500, backFc) / sr_);
                AngleDeg = (float)((ang * 180 / Math.PI) % 360 + 360) % 360;
            }
            double gain = Math.Pow(10, -6.0 * behind * depth / 20.0);
            double pl = Math.Cos((pan + 1) * Math.PI / 4), pr = Math.Sin((pan + 1) * Math.PI / 4);
            // interaural time difference: the far ear hears it later
            double delay = Math.Abs(pan) * itd * 0.00065 * sr_; int wi = dw & 255; dL[wi] = (float)m; dR[wi] = (float)m;
            double dl = pan > 0 ? delay : 0, dr = pan < 0 ? delay : 0;
            double xl = Tap(dL, dl), xr = Tap(dR, dr); dw++;
            // head shadow (far ear) then behind darkening on both ears
            if (pan > 0) { lpFarL += farA * (xl - lpFarL); xl = lpFarL; } else { lpFarR += farA * (xr - lpFarR); xr = lpFarR; }
            lpBackL += backA * (xl - lpBackL); lpBackR += backA * (xr - lpBackR);
            double wl = lpBackL * pl * 1.414 * gain + bass + side * 0.4, wr = lpBackR * pr * 1.414 * gain + bass - side * 0.4;
            sl[i] = (float)wl; sr[i] = (float)wr;
        }
        if (room > 0.5f) rv.Process(sl, sr, n);
        for (int i = 0; i < n; i++) { l[i] = (float)(l[i] * (1 - mix) + sl[i] * mix); r[i] = (float)(r[i] * (1 - mix) + sr[i] * mix); }
    }

    float Tap(float[] b, double delay)
    {
        double rp = dw - delay; int i0 = (int)Math.Floor(rp); double f = rp - i0; float a = b[i0 & 255], c = b[(i0 + 1) & 255];
        return (float)(a * (1 - f) + c * f);
    }
}
// ---------------- simple gain / pan utility (inserted automatically for trims) ----------------
class GainFx : Fx
{
    public GainFx() { Type = "gain"; Title = "Gain"; AddP("gain", "Gain", -24, 24, 0, "dB"); }
    public override void Process(float[] l, float[] r, int n)
    {
        float g = DspMath.DbToLin(Get("gain")); for (int i = 0; i < n; i++) { l[i] *= g; r[i] *= g; }
    }
}

// ---------------- plugin registry ----------------
public static class FxRegistry
{
    public class Info { public string Type, Name, Category, Blurb; public Info(string t, string n, string c, string b) { Type = t; Name = n; Category = c; Blurb = b; } }
    static Info[] allCache;   // the full list is built once (it has over ten thousand entries) and again only when something is registered
    public static Info[] All { get { lock (extra) { if (allCache == null) { var l = new List<Info>(Builtin); l.AddRange(extra); allCache = l.ToArray(); } return allCache; } } }
    static readonly List<Info> extra = new List<Info>(); static readonly Dictionary<string, Func<Fx>> factories = new Dictionary<string, Func<Fx>>();
    // Plugins call this (the host does it for you when a plugin DLL is loaded).
    public static void Register(Info info, Func<Fx> factory) { lock (extra) { if (factories.ContainsKey(info.Type)) extra.RemoveAll(i => i.Type == info.Type); extra.Add(info); factories[info.Type] = factory; allCache = null; } }
    // made when a project asks for it, but not in the list of effects (a retired version of a listed plugin)
    public static void RegisterHidden(Info info, Func<Fx> factory) { lock (extra) { factories[info.Type] = factory; hidden[info.Type] = info; } }
    static readonly Dictionary<string, Info> hidden = new Dictionary<string, Info>();
    public static Info Hidden(string type) { Info i; lock (extra) return hidden.TryGetValue(type, out i) ? i : null; }
    public static bool IsBuiltin(string type) { if (PxPlugins.Types.Contains(type)) return true; foreach (var i in Builtin) if (i.Type == type) return true; return PluginLibrary.IsLibrary(type); }
    static FxRegistry() { PluginLibrary.RegisterAll(); }   // the named built-in plugin library
    public static readonly Info[] Builtin = {
        new Info("eq", "Parametric EQ", "EQ", "6 bands, shelves, high-pass / low-pass, draggable curve"),
        new Info("comp", "Compressor", "Dynamics", "Soft-knee compressor with sidechain filter"),
        new Info("mbcomp", "Multiband Compressor", "Dynamics", "3-band compressor with LR4 crossovers"),
        new Info("limiter", "Limiter", "Dynamics", "Lookahead true-peak brick-wall limiter"),
        new Info("gate", "Noise Gate", "Dynamics", "Gate / expander with hold"),
        new Info("duck", "Vocal Ducker", "Dynamics", "Sidechain ducking from the vocal track"),
        new Info("deess", "De-esser", "Dynamics", "Tames harsh S sounds"),
        new Info("exciter", "Exciter", "Colour", "Adds harmonics: drive, intensity, range, wet/dry"),
        new Info("sat", "Saturation", "Colour", "Tape / tube / soft-clip warmth"),
        new Info("dist", "Distortion", "Colour", "Hard clip, foldback and fuzz"),
        new Info("reverb", "Reverb", "Space", "Freeverb-style room / hall"),
        new Info("delay", "Delay", "Space", "Mono / stereo / ping-pong with tempo sync"),
        new Info("chorus", "Chorus", "Space", "Modulated stereo doubling"),
        new Info("widener", "Stereo Imager", "Space", "Mid/side width with bass mono"),
        new Info("denoise", "Noise Reduction", "Repair", "Spectral noise reduction"),
        new Info("pitch", "Pitch Correction", "Pitch", "Key / scale pitch correction (Auto-Tune style)"),
        new Info("8d", "8D Audio Tuner", "Space", "Orbit the sound around your head (headphones)"),
        new Info("bassboost", "Bass Boost", "EQ", "Raise the low end: pick the frequency, amount and width, plus optional harmonics"),
        new Info("synth", "Bass Lab (synthesizer)", "Instruments", "Wobble bass synth: oscillators, warp, filters, LFOs, matrix, bass boost"),
        new Info("gain", "Gain", "Utility", "Simple gain trim") };

    public static Fx Create(string type)
    {
        switch (type)
        {
            case "eq": return new ParametricEq();
            case "comp": return new Compressor();
            case "mbcomp": return new MultibandComp();
            case "limiter": return new Limiter();
            case "gate": return new Gate();
            case "duck": return new Ducker();
            case "deess": return new DeEsser();
            case "exciter": return new Exciter();
            case "sat": return new Saturation();
            case "dist": return new Distortion();
            case "reverb": return new Reverb();
            case "delay": return new Delay();
            case "chorus": return new Chorus();
            case "widener": return new Widener();
            case "denoise": return new NoiseReduction();
            case "pitch": return new PitchCorrector();
            case "8d": return new Spatial8D();
            case "bassboost": return new BassBoost();
            case "synth": return new SynthFx();
            case "gain": return new GainFx();
        }
        Func<Fx> fac; lock (extra) { if (factories.TryGetValue(type, out fac)) { try { var f = fac(); if (f != null) f.Type = type; return f; } catch { return null; } } }
        return null;
    }
}
