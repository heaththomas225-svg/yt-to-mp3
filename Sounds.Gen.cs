using System;
using System.Collections.Generic;

// =====================================================================================================
//  Sound design for Purphex Sounds: every hit, loop and effect in the library is made here, from oscillators, noise, filters and
//  the Bass Lab synthesizer. Nothing is sampled from a recording by anyone else.
// =====================================================================================================
class Buf
{
    public float[] L, R; public int Length { get { return L.Length; } }
    public Buf(int n) { L = new float[n]; R = new float[n]; }
    public Buf(float[] mono) { L = mono; R = (float[])mono.Clone(); }
    public void Add(Buf b, int at, float gain)
    {
        for (int i = 0; i < b.Length; i++) { int k = at + i; if (k < 0) continue; if (k >= Length) break; L[k] += b.L[i] * gain; R[k] += b.R[i] * gain; }
    }
    public float Peak() { float p = 0; for (int i = 0; i < Length; i++) { float a = Math.Max(Math.Abs(L[i]), Math.Abs(R[i])); if (a > p) p = a; } return p; }
    public void Gain(float g) { for (int i = 0; i < Length; i++) { L[i] *= g; R[i] *= g; } }
    public Buf Copy() { var b = new Buf(Length); Array.Copy(L, b.L, Length); Array.Copy(R, b.R, Length); return b; }
    public Buf Trim(int n) { var b = new Buf(n); Array.Copy(L, b.L, Math.Min(n, Length)); Array.Copy(R, b.R, Math.Min(n, Length)); return b; }
    // brings the loudest peak to the given level (in dB) and rounds off the very ends so nothing clicks
    public void Normalise(double dB, int fadeInSamples, int fadeOutSamples)
    {
        float p = Peak(); if (p < 1e-9f) return; Gain((float)(Math.Pow(10, dB / 20) / p));
        for (int i = 0; i < fadeInSamples && i < Length; i++) { float g = i / (float)fadeInSamples; L[i] *= g; R[i] *= g; }
        for (int i = 0; i < fadeOutSamples && i < Length; i++) { int k = Length - 1 - i; float g = i / (float)fadeOutSamples; L[k] *= g; R[k] *= g; }
    }
    public void Reverse() { Array.Reverse(L); Array.Reverse(R); }
}

class Rnd
{
    uint s; public Rnd(int seed) { s = (uint)(seed * 2654435761u + 88172645u); if (s == 0) s = 1; for (int i = 0; i < 4; i++) Next(); }
    public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }
    public float Bi() { return Next() * 2 - 1; }
    public float Range(float a, float b) { return a + (b - a) * Next(); }
    public int Int(int n) { return (int)(Next() * n) % Math.Max(1, n); }
    public T Pick<T>(IList<T> l) { return l[Int(l.Count)]; }
}

// a state-variable filter (Chamberlin), one channel
struct Svf
{
    float ic1, ic2; public void Reset() { ic1 = ic2 = 0; }
    // a topology-preserving state-variable filter: stays stable at any cutoff, up to the top of the spectrum
    public float Process(float x, float freq, float q, int mode)   // mode 0 low-pass, 1 band-pass, 2 high-pass
    {
        float g = (float)Math.Tan(Math.PI * Math.Max(10f, Math.Min(freq, 20000f)) / 44100.0), k = 1f / Math.Max(0.4f, q);
        float a1 = 1f / (1f + g * (g + k)), a2 = g * a1, a3 = g * a2; float v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3; ic1 = 2f * v1 - ic1; ic2 = 2f * v2 - ic2;
        return mode == 0 ? v2 : (mode == 1 ? v1 : x - k * v1 - v2);
    }
}

static class SoundGen
{
    public const int SR = 44100;
    static float Sat(float x, float drive) { return (float)(Math.Tanh(x * drive) / Math.Tanh(drive)); }
    static double Hz(double midi) { return 440.0 * Math.Pow(2, (midi - 69) / 12.0); }
    public static double NoteHz(int midi) { return Hz(midi); }
    static float[] Noise(Rnd r, int n) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = r.Bi(); return x; }
    // a gentle low-pass (two poles, Butterworth): synthetic noise runs flat up to 22 kHz, which real drums never do; that top end is heard as fizz and crackle
    public static float[] Soft(float[] x, double hz) { var a = new Svf(); var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = a.Process(x[i], (float)hz, 0.707f, 0); return o; }
    // band-limited oscillators (PolyBLEP): a raw square or saw has harmonics far above what 44.1 kHz can hold, and they fold back as harsh tones that do not belong
    static double Blep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }
    public static double Square(double ph, double dt) { double v = ph < 0.5 ? 1 : -1; v += Blep(ph, dt); double p2 = ph + 0.5; if (p2 >= 1) p2 -= 1; return v - Blep(p2, dt); }   // ph in 0..1
    public static double Saw(double ph, double dt) { return 2 * ph - 1 - Blep(ph, dt); }
    // the metallic heart of a hi-hat or cymbal: six square waves at inharmonic ratios, band-limited
    public static float[] MetalTone(int n, double pitch, double[] ratios)
    {
        var x = new float[n]; var ph = new double[ratios.Length];
        for (int i = 0; i < n; i++) { double s = 0; for (int k = 0; k < ratios.Length; k++) { double dt = ratios[k] * pitch / SR; s += Square(ph[k], dt); ph[k] += dt; if (ph[k] >= 1) ph[k] -= 1; } x[i] = (float)(s / ratios.Length); }
        return x;
    }

    // ---------------- drums ----------------
    public static Buf Kick(Rnd r, double f0, double fStart, double tauPitch, double decay, double drive, double click, double len)
    {
        int n = (int)(len * SR); var x = new float[n]; double ph = 0; var hp = new Svf(); var nz = Soft(Noise(r, n), 9000);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, f = f0 + (fStart - f0) * Math.Exp(-t / tauPitch); ph += 2 * Math.PI * f / SR; double env = Math.Exp(-t / decay) * Math.Min(1.0, t / 0.0008);
            float s = (float)(Math.Sin(ph) * env); s += hp.Process(nz[i], 3500f, 0.7f, 1) * (float)(click * 0.7 * Math.Exp(-t / 0.004) * Math.Min(1.0, t / 0.0003)); s += (float)(Math.Sin(2 * Math.PI * f0 * t) * Math.Exp(-t / (decay * 1.5)) * 0.25);
            x[i] = Sat(s, (float)drive);
        }
        return new Buf(x);
    }
    public static Buf Snare(Rnd r, double body, double noiseHz, double decay, double noiseAmt, double snap, double drive, double len)
    {
        int n = (int)(len * SR); var x = new float[n]; var nz = Soft(Noise(r, n), 11000); var bp = new Svf(); var hp = new Svf(); double ph1 = 0, ph2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, drop = 1 + 0.35 * Math.Exp(-t / 0.02); ph1 += 2 * Math.PI * body * drop / SR; ph2 += 2 * Math.PI * body * 1.83 * drop / SR; double be = Math.Exp(-t / 0.085);
            float s = (float)((Math.Sin(ph1) * 0.8 + Math.Sin(ph2) * 0.35) * be); float nzz = bp.Process(nz[i], (float)noiseHz, 0.8f, 1) * 3.2f + hp.Process(nz[i], 6500f, 0.8f, 1) * 0.55f;
            s += nzz * (float)(noiseAmt * Math.Exp(-t / decay)); s += nz[i] * (float)(snap * 0.6 * Math.Exp(-t / 0.0025)); x[i] = Sat(s, (float)(1 + (drive - 1) * 0.3));
        }
        return new Buf(x);
    }
    public static Buf Clap(Rnd r, double centre, double tail, double len)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; double[] at = { 0, 0.011, 0.023, 0.036 };
        for (int c = 0; c < 2; c++)
        {
            var x = c == 0 ? l : rr; var nz = Soft(Noise(r, n), 10000); var bp = new Svf();
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, env = 0; for (int k = 0; k < at.Length; k++) { double d = t - at[k]; if (d >= 0) env = Math.Max(env, Math.Exp(-d / (k == at.Length - 1 ? tail : 0.006)) * (k == at.Length - 1 ? 1 : 0.8)); }
                x[i] = Sat(bp.Process(nz[i], (float)centre * (c == 0 ? 1f : 1.04f), 1.4f, 1) * (float)env * 3f, 1.15f);
            }
        }
        var b = new Buf(n); b.L = l; b.R = rr; return b;
    }
    static readonly double[] MetalRatios = { 205.3, 304.4, 369.6, 522.7, 540.0, 800.0 };
    // other clusters of inharmonic partials: a pack's hats and cymbals are built on one of these, so two packs do not share a metal
    static readonly double[][] MetalSets = { MetalRatios, new[] { 263.0, 349, 421, 593, 709, 853 }, new[] { 180.0, 259, 394, 480, 677, 910 }, new[] { 232.0, 337, 459, 566, 640, 760 } };
    public static Buf Hat(Rnd r, double decay, double bright, double len, double pitch, double hp = 6800, double metalMix = 0.7, int set = 0)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var hpL = new Svf(); var hpR = new Svf(); var bpL = new Svf(); var bpR = new Svf(); var nz = Noise(r, n); var metal = MetalTone(n, pitch, MetalSets[((set % 4) + 4) % 4]);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR; double env = Math.Exp(-t / decay) * Math.Min(1.0, t / 0.0004);
            float m = metal[i] * (float)metalMix + nz[i] * (float)(1 - metalMix); float a = hpL.Process(bpL.Process(m, (float)(bright * 1000), 0.9f, 1) * 2.2f + m * 0.4f, (float)hp, 0.7f, 2); float b = hpR.Process(bpR.Process(m * 0.96f, (float)(bright * 1040), 0.9f, 1) * 2.2f + m * 0.4f, (float)(hp * 1.06), 0.7f, 2);
            l[i] = a * (float)env; rr[i] = b * (float)env;
        }
        var buf = new Buf(Soft(l, 15000)); buf.R = Soft(rr, 15000); return buf;
    }
    public static Buf Cymbal(Rnd r, double decay, double len, bool ride, double pitch = 1, double tone = 1, int set = 0)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var hpL = new Svf(); var hpR = new Svf(); var bpL = new Svf(); var bpR = new Svf(); var nz = Noise(r, n);
        var mA = MetalTone(n, (ride ? 1.9 : 2.6) * pitch * r.Range(0.97f, 1.03f), MetalSets[((set % 4) + 4) % 4]); var mB = MetalTone(n, (ride ? 2.05 : 2.85) * pitch * r.Range(0.97f, 1.03f), MetalSets[((set % 4) + 4) % 4]);   // two slightly different clusters: left and right shimmer apart
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, env = Math.Exp(-t / decay) * (ride ? 1 : Math.Min(1.0, t / 0.002)), body = ride ? 0.45 : 0.25; float nzl = nz[i] * (ride ? 0.25f : 0.55f), nzr = nz[(i + 37) % n] * (ride ? 0.25f : 0.55f);
            float tn = (float)tone; float va = hpL.Process(bpL.Process(mA[i], (ride ? 5200f : 7400f) * tn, 0.7f, 1) * (float)(1 + body) + nzl, (ride ? 3200f : 4600f) * tn, 0.7f, 2), vb = hpR.Process(bpR.Process(mB[i], (ride ? 5400f : 7800f) * tn, 0.7f, 1) * (float)(1 + body) + nzr, (ride ? 3200f : 4600f) * tn, 0.7f, 2);
            l[i] = va * (float)env; rr[i] = vb * (float)env;
        }
        var b = new Buf(n); b.L = l; b.R = rr; return b;
    }
    public static Buf Tom(Rnd r, double f0, double decay, double len) { return Kick(r, f0, f0 * 1.9, 0.045, decay, 1.15, 0.15, len); }
    public static Buf Shaker(Rnd r, double len, double bright)
    {
        int n = (int)(len * SR); var x = new float[n]; var bp = new Svf(); var nz = Noise(r, n);
        for (int i = 0; i < n; i++) { double t = i / (double)SR; double env = Math.Min(1.0, t / 0.012) * Math.Exp(-t / (len * 0.28)); x[i] = bp.Process(nz[i], (float)(6500 * bright), 1.2f, 1) * (float)env * 2.2f; }
        return new Buf(x);
    }
    public static Buf Rim(Rnd r, double f0)
    {
        int n = (int)(0.12 * SR); var x = new float[n]; var nz = Soft(Noise(r, n), 7000);
        for (int i = 0; i < n; i++) { double t = i / (double)SR; x[i] = (float)((Math.Sin(2 * Math.PI * f0 * t) * 0.7 + Math.Sin(2 * Math.PI * f0 * 2.3 * t) * 0.5) * Math.Exp(-t / 0.018) + nz[i] * Math.Exp(-t / 0.002) * 0.3); }
        return new Buf(x);
    }
    public static Buf Cowbell(Rnd r, double f0, double decay)
    {
        int n = (int)(0.5 * SR); var x = new float[n]; var bp = new Svf(); double pa = 0, pb = 0, da = f0 / SR, db = f0 * 1.48 / SR;
        for (int i = 0; i < n; i++) { double t = i / (double)SR; float sq = (float)(Square(pa, da) + Square(pb, db)); pa += da; if (pa >= 1) pa -= 1; pb += db; if (pb >= 1) pb -= 1; x[i] = bp.Process(sq * 0.4f, (float)(f0 * 1.6), 2f, 1) * 2f * (float)(Math.Exp(-t / decay) * 0.8 + Math.Exp(-t / 0.01) * 0.4); }
        return new Buf(x);
    }
    public static Buf Conga(Rnd r, double f0)
    {
        int n = (int)(0.4 * SR); var x = new float[n]; var nz = Soft(Noise(r, n), 6000);
        for (int i = 0; i < n; i++) { double t = i / (double)SR, f = f0 * (1 + 0.25 * Math.Exp(-t / 0.012)); x[i] = (float)(Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t / 0.11) + Math.Sin(2 * Math.PI * f * 1.6 * t) * Math.Exp(-t / 0.05) * 0.3 + nz[i] * Math.Exp(-t / 0.004) * 0.3); }
        return new Buf(x);
    }

    // a long sub-bass hit tuned to a note: the 808
    public static Buf Eight08(Rnd r, int midi, double decay, double drive, double click, double glideSemis, double len, double slideToMidi = 0, double slideAtSec = 0)
    {
        int n = (int)(len * SR); var x = new float[n]; double ph = 0, f = Hz(midi); var nz = Soft(Noise(r, n), 6000); var hp = new Svf();
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, target = Hz(midi); if (slideToMidi > 0 && t > slideAtSec) { double k = Math.Min(1.0, (t - slideAtSec) / 0.09); target = Hz(midi) * Math.Pow(Hz(slideToMidi) / Hz(midi), k); }
            f = target * Math.Pow(2, glideSemis / 12.0 * Math.Exp(-t / 0.03)); ph += 2 * Math.PI * f / SR; double env = Math.Min(1.0, t / 0.002) * Math.Exp(-t / decay);
            float s = (float)(Math.Sin(ph) * env + Math.Sin(ph * 2) * env * 0.08); s += hp.Process(nz[i], 1800f, 0.8f, 2) * (float)(click * 0.5 * Math.Exp(-t / 0.005) * Math.Min(1.0, t / 0.0003)); x[i] = Sat(s, (float)drive);
        }
        var o8 = new Buf(x); EndFade(o8, 0.3, (int)(0.35 * SR)); return o8;
    }

    // ---------------- effects and transitions ----------------
    public static Buf Riser(Rnd r, double len, double f0, double f1, bool tone)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var bpL = new Svf(); var bpR = new Svf(); var nzL = Noise(r, n); var nzR = Noise(r, n); double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)n, f = f0 * Math.Pow(f1 / f0, u), env = Math.Pow(u, 2.2) * (1 - Math.Pow(Math.Max(0, (u - 0.985) / 0.015), 1));
            float a = bpL.Process(nzL[i], (float)f, 3f, 1) * 4f, b = bpR.Process(nzR[i], (float)f, 3f, 1) * 4f; float tn = 0; if (tone) { double dtn = 180 * Math.Pow(8, u) / SR; ph += 2 * Math.PI * dtn; double cyc = (ph / (2 * Math.PI)) % 1.0; tn = (float)(Math.Sin(ph) * 0.18 + Saw(cyc, dtn) * 0.07); }
            l[i] = (a + tn) * (float)env; rr[i] = (b + tn) * (float)env;
        }
        var buf = new Buf(n); buf.L = l; buf.R = rr; return buf;
    }
    public static Buf Impact(Rnd r, double len, double subHz, double drive)
    {
        int n = (int)(len * SR); var x = new float[n]; var nz = Soft(Noise(r, n), 6000); var lp = new Svf(); double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, f = subHz * (1 + 1.6 * Math.Exp(-t / 0.07)); ph += 2 * Math.PI * f / SR; float s = (float)(Math.Sin(ph) * Math.Exp(-t / (len * 0.28)) * Math.Min(1.0, t / 0.002));
            s += lp.Process(nz[i], 2400f, 0.7f, 0) * (float)(Math.Exp(-t / 0.35) * 0.9) + nz[i] * (float)(Math.Exp(-t / 0.004) * 0.35); x[i] = Sat(s, (float)(1 + (drive - 1) * 0.3));
        }
        var b = new Buf(x); return Wet(b, 85, 55, 0.5f, (int)(len * SR));
    }
    public static Buf Sweep(Rnd r, double len, double f0, double f1, double q)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var a = new Svf(); var b = new Svf(); var nl = Noise(r, n); var nr = Noise(r, n);
        for (int i = 0; i < n; i++) { double u = i / (double)n, f = f0 * Math.Pow(f1 / f0, u), env = Math.Sin(Math.PI * Math.Pow(u, 0.9)); l[i] = a.Process(nl[i], (float)f, (float)q, 1) * (float)env * 3f; rr[i] = b.Process(nr[i], (float)f, (float)q, 1) * (float)env * 3f; }
        var buf = new Buf(n); buf.L = l; buf.R = rr; return buf;
    }
    public static Buf Siren(double len, double lo, double hi, double rate)
    {
        int n = (int)(len * SR); var x = new float[n]; double ph = 0;
        for (int i = 0; i < n; i++) { double t = i / (double)SR, f = lo + (hi - lo) * (0.5 + 0.5 * Math.Sin(2 * Math.PI * rate * t - Math.PI / 2)); ph += 2 * Math.PI * f / SR; double env = Math.Min(1, t / 0.05) * Math.Min(1, (len - t) / 0.12); x[i] = (float)((Math.Sin(ph) * 0.6 + Saw((ph / (2 * Math.PI)) % 1.0, f / SR) * 0.45) * env * 0.5); }
        return new Buf(x);
    }
    // the chord an atmosphere is built on: nine voicings, from a warm minor or major ninth to open fifths and stacked fourths
    static int[] AtmoShapes(int shape, bool minor)
    {
        switch (((shape % 9) + 9) % 9)
        {
            case 1: return new[] { 0, 2, 7, 9, 14 }; case 2: return new[] { 0, 5, 7, 12, 17 }; case 3: return minor ? new[] { 0, 7, 12, 15, 19 } : new[] { 0, 7, 12, 16, 19 };
            case 4: return minor ? new[] { 0, 3, 7, 10, 14, 17 } : new[] { 0, 4, 7, 9, 14 }; case 5: return new[] { 0, 5, 10, 15, 20 }; case 6: return minor ? new[] { 0, 3, 7, 8, 12 } : new[] { 0, 4, 7, 11, 18 };
            case 7: return new[] { 0, 7, 14, 19, 26 }; case 8: return minor ? new[] { 0, 3, 6, 10, 15 } : new[] { 0, 4, 8, 11, 14 };
        }
        return minor ? new[] { 0, 3, 7, 10, 14 } : new[] { 0, 4, 7, 11, 14 };
    }
    public static Buf Atmosphere(Rnd r, double len, int rootMidi, bool minor, double bright, int shape = 0, double lfoHz = 0.11, double detune = 0.07, double noise = 0.07)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; int[] ch = AtmoShapes(shape, minor); var lpA = new Svf(); var lpB = new Svf(); var nzA = Noise(r, n); var nzB = Noise(r, n); var ph = new double[ch.Length * 2];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, lfo = 0.5 + 0.5 * Math.Sin(2 * Math.PI * lfoHz * t), env = Math.Min(1, t / (len * 0.3)) * Math.Min(1, (len - t) / (len * 0.35)); double sl = 0, sr = 0;
            for (int k = 0; k < ch.Length; k++) { for (int s = 0; s < 2; s++) { ph[k * 2 + s] += 2 * Math.PI * Hz(rootMidi + ch[k] + (s == 0 ? detune : -detune)) / SR; double v = Math.Sin(ph[k * 2 + s]) + Math.Sin(ph[k * 2 + s] * 2) * 0.3; if (s == 0) sl += v; else sr += v; } }
            l[i] = lpA.Process((float)(sl * 0.1) + nzA[i] * (float)noise, (float)(500 + 2600 * bright * lfo), 1.1f, 0) * (float)env; rr[i] = lpB.Process((float)(sr * 0.1) + nzB[i] * (float)noise, (float)(500 + 2600 * bright * (1 - lfo)), 1.1f, 0) * (float)env;
        }
        var b = new Buf(n); b.L = l; b.R = rr; return Wet(b, 80, 45, 0.45f, n);
    }
    public static Buf Crackle(Rnd r, double len, double density)
    {
        int n = (int)(len * SR); var x = new float[n]; var hp = new Svf(); float last = 0;
        for (int i = 0; i < n; i++) { float imp = r.Next() < density / SR * 100 ? r.Bi() * 0.9f : 0f; last = last * 0.55f + imp; x[i] = hp.Process(last + r.Bi() * 0.012f, 1500f, 0.7f, 2); }
        return new Buf(x);
    }
    public static Buf Wet(Buf b, float size, float damp, float mix, int outLen)
    {
        int n = Math.Max(outLen, b.Length) + (int)(SR * 1.6); var o = new Buf(n); o.Add(b, 0, 1f); var rv = new Reverb(); rv.Prepare(SR); rv.Set("size", size); rv.Set("damp", damp); rv.Set("mix", mix * 100f); rv.Set("pre", 10); rv.Set("lc", 80);
        for (int p = 0; p < n; p += 4096) { int c = Math.Min(4096, n - p); var l = new float[c]; var r2 = new float[c]; Array.Copy(o.L, p, l, 0, c); Array.Copy(o.R, p, r2, 0, c); rv.Process(l, r2, c); Array.Copy(l, 0, o.L, p, c); Array.Copy(r2, 0, o.R, p, c); }
        return o.Trim(outLen);
    }

    // ---------------- vocal-style sounds (formant synthesis: there is no recorded voice anywhere in this library) ----------------
    public static readonly double[][] Vowels = { new[] { 800.0, 1150, 2900 }, new[] { 400.0, 1600, 2700 }, new[] { 270.0, 2300, 3000 }, new[] { 450.0, 800, 2830 }, new[] { 325.0, 700, 2530 } };   // ah, eh, ee, oh, oo
    public static Buf VocalSyllable(Rnd r, double f0, int vowel, int vowel2, double len, double bend, double shout, string consonant)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var f = new Svf[6]; for (int i = 0; i < 6; i++) f[i] = new Svf(); double[] ph = new double[3]; int voices = shout > 0.5 ? 3 : 1; var nz = Noise(r, n);
        for (int ch = 0; ch < 2; ch++)
        {
            var o = ch == 0 ? l : rr; for (int i = 0; i < 6; i++) f[i].Reset(); Array.Clear(ph, 0, 3);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, u = i / (double)n, fz = f0 * (1 + bend * u) * (1 + 0.004 * Math.Sin(2 * Math.PI * 5.2 * t)); double src = 0;
                for (int v = 0; v < voices; v++) { double dv = fz * (1 + (v == 1 ? 0.008 : (v == 2 ? -0.009 : 0))) * (ch == 1 ? 1.002 : 1.0) / SR; ph[v] += dv; ph[v] -= Math.Floor(ph[v]); src += Saw(ph[v], dv); }
                src /= voices; float x = (float)(src * 0.6) + nz[i] * (float)(0.04 + (shout > 0.5 ? 0.04 : 0)); var A = Vowels[vowel]; var B = Vowels[vowel2 >= 0 ? vowel2 : vowel]; double m = Math.Min(1.0, u * 1.6); float s = 0;
                for (int k = 0; k < 3; k++) s += f[k].Process(x, (float)(A[k] + (B[k] - A[k]) * m), 8f + k * 2, 1) * (k == 0 ? 1.0f : (k == 1 ? 0.7f : 0.4f));
                double env = Math.Min(1.0, t / 0.012) * Math.Min(1.0, (len - t) / 0.08) * (1 - 0.35 * u); double cons = 0;
                if (consonant == "h") cons = f[3].Process(nz[i], 3500f, 0.7f, 2) * Math.Exp(-t / 0.05) * 1.6; else if (consonant == "t") cons = f[3].Process(nz[i], 4200f, 0.8f, 2) * Math.Exp(-t / 0.012) * 2.2; else if (consonant == "s") cons = f[3].Process(nz[i], 6500f, 0.8f, 2) * Math.Exp(-t / 0.13) * 1.2;
                o[i] = Sat((float)(s * env * 0.8 + cons * 0.5), shout > 0.5 ? 1.35f : 1.05f);
            }
        }
        var b = new Buf(n); b.L = l; b.R = rr; return b;
    }

    // plucked string (Karplus-Strong): guitar-like and harp-like loops
    public static Buf Pluck(Rnd r, double hz, double len, double bright)
    {
        double period = SR / hz - 0.5; int d = Math.Max(2, (int)Math.Floor(period - 0.1)); double frac = period - d; double c = (1 - frac) / (1 + frac);   // whole samples + an all-pass for the fraction: the note is in tune
        var line = new float[d]; float sm = 0, k0 = (float)(0.35 + 0.5 * bright); for (int i = 0; i < d; i++) { sm += (r.Bi() - sm) * k0; line[i] = sm * 1.4f; } int n = (int)(len * SR); var x = new float[n]; int p = 0; float prev = 0; double apX = 0, apY = 0; float damp = (float)(0.992 - (1 - bright) * 0.03);
        for (int i = 0; i < n; i++) { float v = line[p]; float avg = damp * 0.5f * (v + prev); prev = v; double ap = c * avg + apX - c * apY; apX = avg; apY = ap; line[p] = (float)ap; p = (p + 1) % d; x[i] = v; }
        var o = new Buf(x); EndFade(o, 0.2, (int)(0.06 * SR)); return o;
    }

    // ---------------- the Bass Lab synthesizer, played by notes ----------------
    public static Buf RenderSynth(string patch, List<double[]> notes, double bpm, double beats, double tailSec)
    {
        double spb = 60.0 / bpm * SR; int total = (int)(beats * spb + tailSec * SR); var l = new float[total]; var r = new float[total];
        var fx = new SynthFx(); fx.Prepare(SR); fx.ResetAll(false); fx.Deserialize(patch); var tr = StudioEngine.NewTrack("render", "inst", null); tr.Instrument = true;
        foreach (var n in notes)
        {
            long on = (long)Math.Round(n[0] * spb), off = (long)Math.Round((n[0] + n[1]) * spb); tr.Midi.Add(new MidiEv { Frame = on, S = 0x90, D1 = (byte)n[2], D2 = (byte)Math.Max(1, Math.Min(127, n[3])) }); tr.Midi.Add(new MidiEv { Frame = Math.Max(on + 1, off), S = 0x80, D1 = (byte)n[2], D2 = 0 });
        }
        tr.Midi.Sort((a, b) => a.Frame != b.Frame ? a.Frame.CompareTo(b.Frame) : (a.S == 0x80 ? -1 : 1).CompareTo(b.S == 0x80 ? -1 : 1));
        double keep = Fx.RenderBpm; Fx.RenderBpm = bpm;
        try
        {
            var bl = new float[256]; var br = new float[256];
            for (int p = 0; p < total; p += 256)
            {
                int c = Math.Min(256, total - p); Array.Clear(bl, 0, c); Array.Clear(br, 0, c); fx.BeginBlock(tr, p, c, true); fx.Process(bl, br, c); Array.Copy(bl, 0, l, p, c); Array.Copy(br, 0, r, p, c);
            }
        }
        finally { Fx.RenderBpm = keep; }
        var o = new Buf(total); o.L = l; o.R = r; return o;
    }

    // light mastering for rendered material: tame the peaks so level and loudness are consistent
    public static void Glue(Buf b, float drive)
    {
        float p = b.Peak(); if (p > 1f) b.Gain(1f / p);   // (it used to saturate the mix: that is distortion, so it only levels now)
    }
    // rounds off the end of a sound over its last part (a raised cosine): a hit that is still ringing never stops dead (that stop is a click)
    public static void EndFade(Buf b, double frac, int maxSamples)
    {
        int n = b.Length, f = Math.Min(n, Math.Min(maxSamples, (int)(n * frac))); if (f < 2) return;
        for (int i = 0; i < f; i++) { int k = n - f + i; float g = (float)(0.5 + 0.5 * Math.Cos(Math.PI * (i + 1) / f)); b.L[k] *= g; b.R[k] *= g; }
    }
    public static Buf Lowpass(Buf b, double hz) { var o = b.Copy(); var a = new Svf(); var c = new Svf(); for (int i = 0; i < o.Length; i++) { o.L[i] = a.Process(o.L[i], (float)hz, 0.7f, 0); o.R[i] = c.Process(o.R[i], (float)hz, 0.7f, 0); } return o; }
    public static Buf Highpass(Buf b, double hz) { var o = b.Copy(); var a = new Svf(); var c = new Svf(); for (int i = 0; i < o.Length; i++) { o.L[i] = a.Process(o.L[i], (float)hz, 0.7f, 2); o.R[i] = c.Process(o.R[i], (float)hz, 0.7f, 2); } return o; }
}
