using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// =====================================================================================================
//  THE PURPHEX WOBBLE ENGINE: the bass sound of UK bassline, bass music and DnB. Two layers, like a producer builds it:
//   SUB  a clean sine an octave or at the note, low-passed and mono, that never moves (the weight);
//   MID  the character: band-limited saw / square (pulse width), a reese (two detuned saws), FM (a sine modulated by a sine) or a
//        mix, high-passed away from the sub, through a resonant filter (or a vowel formant filter for talking bass) whose cutoff (or
//        vowel) is swept by a TEMPO-SYNCED LFO. The LFO's rate can change every beat (a rhythm of 1/4, 1/8, 1/16, triplets...), with
//        shapes from a smooth "wub" to a sawtooth "yoy" and a chopped stutter. Then smooth drive (grit), an optional wavefold, a
//        top-end roll-off so the grit never fizzes, and width only on the upper part (the sub stays in the middle).
//  Glide (portamento) between notes, an amp envelope with soft ends (no clicks), and the nonlinear parts (oscillators, filter, drive)
//  run at twice the sample rate and are brought back down with a half-band filter (no aliasing crackle). The same engine renders
//  the library's bass sounds and plays live as the Purphex Wobble instrument (so a preset is the same sound on any note).
// =====================================================================================================
class WobblePatch
{
    public double Saw = 1, Square, Reese, Fm, Fold;           // the mid layer's sources (levels)
    public double Pw = 0.5, PwMod, Detune = 18, FmRatio = 2, FmIndex = 2, FmMod;   // pulse width (and LFO on it), reese detune (cents), FM ratio, index, LFO on the index
    public double Cutoff = 300, Depth = 3, Res = 0.45, EnvAmt = 0, EnvDecay = 0.12;    // filter: base cutoff (Hz), LFO sweep in octaves, resonance 0..0.95, an envelope in octaves (rubber "bloop")
    public bool Formant; public string Vowels = "aoui";       // vowel filter instead of the low-pass (talking bass)
    public string Rates = "8"; public int Shape;                // LFO: divisions per beat ("8" = 1/8 notes, "8,16,8,12" changes every beat: 12 = 1/8 triplets, 24 = 1/16 triplets), shape: 0 wub, 1 sine, 2 saw down (yoy), 3 ramp up, 4 chop, 5 triangle
    public bool Retrig = true;                                  // the LFO starts at the top of each note (classic wobble)
    public double Drive = 0.4, Sub = 0.55, Mid = 1.3, SubOct = 0, Attack = 0.004, Release = 0.06, Glide = 0, Width = 0.25, PitchEnv = 0, PitchDecay = 0.06, Pump = 0, Tone = 7000;
    public WobblePatch Copy() { return (WobblePatch)MemberwiseClone(); }
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public string Save()
    {
        var sb = new StringBuilder(); Action<string, double> p = (k, v) => sb.Append(k).Append('=').Append(v.ToString("0.####", CI)).Append(';');
        p("saw", Saw); p("square", Square); p("reese", Reese); p("fm", Fm); p("fold", Fold); p("pw", Pw); p("pwmod", PwMod); p("detune", Detune); p("fmratio", FmRatio); p("fmindex", FmIndex); p("fmmod", FmMod);
        p("cutoff", Cutoff); p("depth", Depth); p("res", Res); p("envamt", EnvAmt); p("envdecay", EnvDecay); p("formant", Formant ? 1 : 0); p("shape", Shape); p("retrig", Retrig ? 1 : 0);
        p("drive", Drive); p("sub", Sub); p("mid", Mid); p("suboct", SubOct); p("attack", Attack); p("release", Release); p("glide", Glide); p("width", Width); p("pitchenv", PitchEnv); p("pitchdecay", PitchDecay); p("pump", Pump); p("tone", Tone);
        sb.Append("rates=").Append(Rates).Append(";vowels=").Append(Vowels); return sb.ToString();
    }
    public static WobblePatch Load(string s)
    {
        var w = new WobblePatch(); if (string.IsNullOrEmpty(s)) return w;
        foreach (var part in s.Split(';'))
        {
            int i = part.IndexOf('='); if (i < 1) continue; string k = part.Substring(0, i), v = part.Substring(i + 1); double d; bool num = double.TryParse(v, NumberStyles.Float, CI, out d);
            switch (k)
            {
                case "rates": w.Rates = v; break; case "vowels": w.Vowels = v; break;
                default: if (!num) break; var f = typeof(WobblePatch).GetField(char.ToUpperInvariant(k[0]) + k.Substring(1), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase); if (f == null) break; if (f.FieldType == typeof(bool)) f.SetValue(w, d > 0.5); else if (f.FieldType == typeof(int)) f.SetValue(w, (int)d); else f.SetValue(w, d); break;
            }
        }
        return w;
    }
}

// one voice of the engine (mono, legato with glide): Render adds into the buffers; the caller gives notes and the tempo
sealed class WobbleVoice
{
    readonly WobblePatch P; readonly int sr; readonly double osr; double bpm;
    double freq, target, ph1, ph2, ph3, phFm, phSub, env, envTarget, noteT, fenv, lfoPh, beatPos; bool on; int note = -1;
    double s1, s2, s3, s4, hp1, hpx, subLp, toneL, toneR, dcx, dcy, pumpEnv; readonly double[] fz = new double[6]; readonly double[] fz2 = new double[6]; int beatIdx;
    readonly double[] hbL = new double[23]; int hbI; readonly double[] dl = new double[2048]; int dlI;
    int[] rates; double[][] vowelF;
    static readonly Dictionary<char, double[]> VF = new Dictionary<char, double[]> { { 'a', new[] { 800.0, 1150, 2900 } }, { 'e', new[] { 400.0, 1600, 2700 } }, { 'i', new[] { 300.0, 2100, 2900 } }, { 'o', new[] { 450.0, 800, 2830 } }, { 'u', new[] { 325.0, 700, 2530 } } };
    public WobbleVoice(WobblePatch p, int sampleRate, double tempo)
    {
        P = p; sr = sampleRate; osr = sr * 2.0; bpm = Math.Max(40, tempo); Refresh();
    }
    // the live instrument: the tempo, the song's beat (rate sequences line up with the bars) and changed settings
    public double Tempo { get { return bpm; } set { bpm = Math.Max(40, Math.Min(300, value)); } }
    public void SetBeat(double beat) { beatPos = beat; beatIdx = (int)Math.Floor(beat); }
    public int Note { get { return on ? note : -1; } }
    public void Refresh()
    {
        var p = P; var rl = new List<int>(); foreach (var x in (p.Rates ?? "8").Split(',')) { int v; if (int.TryParse(x.Trim(), out v) && v > 0) rl.Add(v); } if (rl.Count == 0) rl.Add(8); rates = rl.ToArray();
        var vl = new List<double[]>(); foreach (char c in (p.Vowels ?? "aoui")) { double[] f; if (VF.TryGetValue(c, out f)) vl.Add(f); } if (vl.Count < 2) { vl.Add(VF['a']); vl.Add(VF['o']); } vowelF = vl.ToArray();
    }
    public void NoteOn(int n, int vel, double atBeat)
    {
        double f = 440 * Math.Pow(2, (n - 69) / 12.0); bool legato = on && P.Glide > 0; target = f; if (!legato) { freq = f; noteT = 0; fenv = 1; if (P.Retrig) lfoPh = 0; } else noteT = Math.Min(noteT, 0.02);
        if (!legato) { env = Math.Min(env, 1); } envTarget = 0.35 + 0.65 * vel / 127.0; on = true; note = n;
    }
    public void NoteOff(int n) { if (n == note) { on = false; envTarget = 0; } }
    static double Soft(double x) { double a = Math.Abs(x); return a < 0.8 ? x : Math.Sign(x) * (0.8 + 0.2 * Math.Tanh((a - 0.8) / 0.2)); }   // a safety limit (never above 1)
    public bool Active { get { return on || env > 1e-4; } }
    static double PolyBlep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }
    // the LFO shapes; a shape that jumps at the end of its cycle (yoy, ramp, chop) is bridged over the first 4% of the next one, so the filter never jumps (a jump is a click)
    double Lfo(double ph)
    {
        ph -= Math.Floor(ph); if (ph >= 0.04) return Raw(ph);
        double a = Raw(0.9999), b = Raw(0.04), t = ph / 0.04; t = t * t * (3 - 2 * t); return a + (b - a) * t;
    }
    double Raw(double ph)
    {
        switch (P.Shape)
        {
            case 1: return 0.5 - 0.5 * Math.Cos(2 * Math.PI * ph);
            case 2: return 1 - ph;                                            // yoy: falls each cycle
            case 3: return ph;                                                // ramp up
            case 4: return ph < 0.5 ? 1 - Math.Pow(ph * 2, 6) * 0.15 : 0.08 + 0.1 * Math.Exp(-(ph - 0.5) * 20);   // chop (still smooth at the edges)
            case 5: return ph < 0.5 ? ph * 2 : 2 - ph * 2;
            default: { double c = 0.5 - 0.5 * Math.Cos(2 * Math.PI * ph); return Math.Pow(c, 1.6); }   // wub: a rounded swell
        }
    }
    // one output sample (both sides); beatStep = beats per output sample (tempo)
    public void Next(out double outL, out double outR)
    {
        double dtBeat = bpm / 60.0 / sr; beatPos += dtBeat; int bi = (int)Math.Floor(beatPos); if (bi != beatIdx) beatIdx = bi;
        double div = rates[((beatIdx % rates.Length) + rates.Length) % rates.Length]; double lfoHz = bpm / 60.0 * div / 4.0;   // div 8 = eighth notes: 2 per beat
        double accL = 0, accR = 0;
        for (int os = 0; os < 2; os++)
        {
            double dt = 1.0 / osr;
            // pitch: glide, pitch envelope
            if (P.Glide > 0) freq += (target - freq) * (1 - Math.Exp(-dt / Math.Max(0.002, P.Glide))); else freq = target;
            double f = freq * Math.Pow(2, P.PitchEnv * Math.Exp(-noteT / Math.Max(0.005, P.PitchDecay)) / 12.0); noteT += dt;
            lfoPh += lfoHz * dt; double lfo = Lfo(lfoPh); fenv *= Math.Exp(-dt / Math.Max(0.01, P.EnvDecay));
            // envelope (soft ends)
            double ek = envTarget > env ? 1 - Math.Exp(-dt / Math.Max(0.001, P.Attack)) : 1 - Math.Exp(-dt / Math.Max(0.005, P.Release)); env += (envTarget - env) * ek;
            // oscillators
            double inc = f * dt; ph1 += inc; if (ph1 >= 1) ph1 -= 1;
            double saw = 2 * ph1 - 1 - PolyBlep(ph1, inc);
            double pw = Math.Max(0.08, Math.Min(0.92, P.Pw + P.PwMod * (lfo - 0.5))); double sq = (ph1 < pw ? 1 : -1) + PolyBlep(ph1, inc) - PolyBlep((ph1 - pw + 1) % 1, inc);
            double d = Math.Pow(2, P.Detune / 1200.0); ph2 += inc * d; if (ph2 >= 1) ph2 -= 1; ph3 += inc / d; if (ph3 >= 1) ph3 -= 1;
            double reese = 0.5 * ((2 * ph2 - 1 - PolyBlep(ph2, inc * d)) + (2 * ph3 - 1 - PolyBlep(ph3, inc / d)));
            phFm += inc * P.FmRatio; if (phFm >= 1) phFm -= 1; double idx = P.FmIndex * (1 + P.FmMod * (lfo - 0.5) * 2); double fm = Math.Sin(2 * Math.PI * ph1 + idx * Math.Sin(2 * Math.PI * phFm));
            double mid = P.Saw * saw + P.Square * sq + P.Reese * reese + P.Fm * fm; double norm = P.Saw + P.Square + P.Reese + P.Fm; if (norm > 0) mid /= Math.Max(1, norm * 0.7);
            if (P.Fold > 0) { double g = 1 + P.Fold * 4 * lfo; mid = Math.Sin(mid * g * 1.4) / 1.0; }
            // grit before the filter (the filter then shapes the harmonics the drive made: open = filthy, closed = dark)
            { double pd = 1 + P.Drive * 6; mid = Math.Tanh(mid * pd) / Math.Tanh(pd); }
            // keep the mid away from the sub
            double hpk = 1 - Math.Exp(-2 * Math.PI * 60 * dt); hp1 += (mid - hp1) * hpk; mid -= hp1;   /* (DC and rumble; the real split is after the drive) */
            // the filter (state variable, 2 poles) or the vowels
            double y = mid;
            if (P.Formant)
            {
                double pos = lfo * (vowelF.Length - 1); int a0 = Math.Min(vowelF.Length - 2, (int)Math.Floor(pos)); double fr = pos - a0; double fy = 0;
                for (int j = 0; j < 3; j++)
                {
                    double fc = vowelF[a0][j] * (1 - fr) + vowelF[a0 + 1][j] * fr; double g = Math.Tan(Math.PI * Math.Min(fc, osr * 0.45) / osr), k = 0.5 - 0.3 * Math.Min(0.95, P.Res);
                    double hp = (mid - (k + g) * fz[j * 2] - fz[j * 2 + 1]) / (1 + g * (g + k)); double bp = g * hp + fz[j * 2]; fz[j * 2] = g * hp + bp; double lp = g * bp + fz[j * 2 + 1]; fz[j * 2 + 1] = g * bp + lp;
                    fy += bp * k * (j == 0 ? 2.2 : (j == 1 ? 1.6 : 0.8));
                }
                y = fy * 1.5;
            }
            {
                double fc = Math.Min(osr * 0.45, P.Cutoff * Math.Pow(2, P.Depth * lfo + P.EnvAmt * fenv)); double g = Math.Tan(Math.PI * fc / osr), k = 2 - 2 * Math.Min(0.95, P.Res);
                double hp = (y - (k + g) * s1 - s2) / (1 + g * (g + k)); double bp = g * hp + s1; s1 = g * hp + bp; double lp = g * bp + s2; s2 = g * bp + lp; y = (lp + bp * P.Res * 1.1) * 1.8;
                /* a second pole pair (24 dB/oct): closed is properly dark, open is bright - the deep "wub" */
                double k2 = 1.4142; double hq = (y - (k2 + g) * s3 - s4) / (1 + g * (g + k2)); double bq = g * hq + s3; s3 = g * hq + bq; double lq = g * bq + s4; s4 = g * bq + lq; y = lq;
            }
            // grit: smooth drive (tanh), level kept
            double dr = 1 + P.Drive * 0.35; y = Math.Tanh(y * dr) / Math.Tanh(dr);   /* (gentle: strong drive after the filter would fill the closed filter back up and flatten the wobble) */
            // the mid layer above the sub: 24 dB/oct high-pass at 150 Hz (two Butterworth stages), so the low end is only the clean sub
            for (int st = 0; st < 2; st++)
            {
                double g = Math.Tan(Math.PI * 150 / osr), k = 1.4142; int oi = st * 2;
                double hp = (y - (k + g) * fz2[oi] - fz2[oi + 1]) / (1 + g * (g + k)); double bp = g * hp + fz2[oi]; fz2[oi] = g * hp + bp; double lp = g * bp + fz2[oi + 1]; fz2[oi + 1] = g * bp + lp; y = hp;
            }
            // sub: a clean sine (never filtered by the LFO), mono
            phSub += f * Math.Pow(2, -P.SubOct) * dt; if (phSub >= 1) phSub -= 1; double sub = Math.Sin(2 * Math.PI * phSub); double sk = 1 - Math.Exp(-2 * Math.PI * 160 * dt); subLp += (sub - subLp) * sk;
            // tone: roll off the very top of the mid (no fizz)
            double tk = 1 - Math.Exp(-2 * Math.PI * Math.Min(P.Tone, osr * 0.4) * dt); toneL += (y - toneL) * tk; toneR += (toneL - toneR) * tk;
            double v = (toneR * P.Mid + subLp * P.Sub * 0.9) * env;
            accL += v; accR += toneR * P.Mid * env;   // (accR: the mid alone, for the width below)
        }
        // back to the output rate (a simple 2x average after the 2-pole tone roll-off), DC block, width on the mid only, sidechain pump
        double o = accL * 0.17, midOnly = accR * 0.17; double dcOut = o - dcx + 0.9995 * dcy; dcx = o; dcy = dcOut;
        dl[dlI] = midOnly; int tap = (dlI - (int)(sr * 0.009) + dl.Length) % dl.Length; dlI = (dlI + 1) % dl.Length; double side = (midOnly - dl[tap]) * P.Width * 0.5;
        double pump = 1; if (P.Pump > 0) { double bf = beatPos - Math.Floor(beatPos); pump = 1 - P.Pump * Math.Exp(-bf * 9); }
        outL = Soft((dcOut + side) * pump); outR = Soft((dcOut - side) * pump);
    }
}

static class WobbleEngine
{
    // renders notes {start beat, length beats, midi, velocity} at a tempo into a buffer of 'lengthBeats' (+ a short tail that the caller folds)
    public static Buf Render(WobblePatch p, List<double[]> notes, double bpm, double lengthBeats, double tailSec)
    {
        int sr = SoundGen.SR; double spb = 60.0 / bpm * sr; int n = (int)(lengthBeats * spb + tailSec * sr); var b = new Buf(n); var v = new WobbleVoice(p, sr, bpm);
        var ev = new List<KeyValuePair<int, double[]>>(); foreach (var nt in notes) { ev.Add(new KeyValuePair<int, double[]>((int)Math.Round(nt[0] * spb), new[] { 1, nt[2], nt[3] })); ev.Add(new KeyValuePair<int, double[]>((int)Math.Round((nt[0] + nt[1]) * spb), new[] { 0, nt[2], 0 })); }
        ev.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value[0].CompareTo(y.Value[0])); int ei = 0;
        for (int i = 0; i < n; i++)
        {
            while (ei < ev.Count && ev[ei].Key <= i) { var e = ev[ei++].Value; if (e[0] == 1) v.NoteOn((int)e[1], (int)e[2], i / spb); else v.NoteOff((int)e[1]); }
            double l, r; v.Next(out l, out r); b.L[i] = (float)l; b.R[i] = (float)r;
        }
        return b;
    }
}
