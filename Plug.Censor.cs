using System;
using System.Collections.Generic;

// ======================================================================
//  CENSOR: hides a word the way radio and TV edits do: a bleep, silence, the word played backwards, muffled, scrambled, radio static
//  or just much quieter. CENSOR (On/Off) is the switch: right-click a section and press Censor this section (it draws the switch for you),
//  or draw its automation by hand. Fade keeps the edges click-free.
// ======================================================================
class CensorFx : Fx
{
    public static readonly string[] Modes = { "Bleep (TV beep)", "Mute (silence)", "Reverse (backwards)", "Muffle (under water)", "Scramble (garbled)", "Static (radio noise)", "Duck (much quieter)" };
    double ph, g, envL, envR, fL1, fL2, fR1, fR2, hpL, hpR, hpxL, hpxR, rmPh; uint rnd = 22222;
    float[] bl, br; int w, gk; const int Ring = 32768;
    public CensorFx()
    {
        Type = "censor"; Title = "Censor"; Category = "Vocal Tools";
        AddC("mode", "Sound", Modes, 0); AddC("censor", "CENSOR", new[] { "Off", "On" }, 1);
        AddP("tone", "Bleep pitch", 300, 3000, 1000, "Hz"); AddP("level", "Bleep level", -30, 0, -12, "dB"); AddC("follow", "Bleep only while the voice sounds", new[] { "Off", "On" }, 0);
        AddP("fade", "Fade", 1, 50, 6, "ms"); AddP("keep", "Voice kept under it", 0, 100, 0, "%"); AddP("grain", "Reverse / scramble piece", 30, 300, 120, "ms");
    }
    public override void Reset() { base.Reset(); ph = 0; g = 0; envL = envR = 0; fL1 = fL2 = fR1 = fR2 = 0; hpL = hpR = hpxL = hpxR = 0; rmPh = 0; if (bl != null) { Array.Clear(bl, 0, Ring); Array.Clear(br, 0, Ring); } w = 0; gk = 0; }
    double Noise() { rnd ^= rnd << 13; rnd ^= rnd >> 17; rnd ^= rnd << 5; return (rnd & 0xFFFFFF) / 8388608.0 - 1.0; }
    // two overlapping pieces read backwards (sine-squared windows that add up to one): the word plays in reverse with no clicks
    double Rev(float[] b, int len, int k)
    {
        int half = len / 2; int k1 = k % len, k2 = (k + half) % len;
        double w1 = Math.Sin(Math.PI * k1 / len), w2 = Math.Sin(Math.PI * k2 / len); w1 *= w1; w2 *= w2;
        int i1 = (w - 2 * k1 - 1) & (Ring - 1), i2 = (w - 2 * k2 - 1) & (Ring - 1);
        return b[i1] * w1 + b[i2] * w2;
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (bl == null) { bl = new float[Ring]; br = new float[Ring]; }
        int mode = (int)Get("mode"); double target = Get("censor") >= 0.5f ? 1 : 0, tone = Get("tone"), lev = Math.Pow(10, Get("level") / 20.0), keep = Get("keep") / 100.0; bool follow = Get("follow") >= 0.5f;
        double fc = 1 - Math.Exp(-1.0 / Math.Max(1, Get("fade") * 0.001 * SR)), envRel = Math.Exp(-1.0 / (0.06 * SR)), lpA = 1 - Math.Exp(-2 * Math.PI * 380 / SR), hpA = Math.Exp(-2 * Math.PI * 1200 / SR);
        int len = (int)Math.Max(256, Math.Min(Ring / 3, Get("grain") * 0.001 * SR)); if (mode == 4) len = Math.Max(256, len / 3);
        for (int i = 0; i < n; i++)
        {
            double x = l[i], y = r[i]; bl[w] = (float)x; br[w] = (float)y; w = (w + 1) & (Ring - 1);
            envL = Math.Max(Math.Abs(x), envL * envRel); envR = Math.Max(Math.Abs(y), envR * envRel);
            g += (target - g) * fc;
            if (g < 1e-5 && target == 0) { gk = (gk + 1) % (len * 4); continue; }
            double cl, cr;
            switch (mode)
            {
                case 0: { ph += tone / SR; ph -= Math.Floor(ph); double s = Math.Sin(ph * 2 * Math.PI) * lev; if (follow) s *= Math.Min(1, Math.Max(envL, envR) * 8); cl = cr = s; break; }
                case 1: cl = cr = 0; break;
                case 2: cl = Rev(bl, len, gk); cr = Rev(br, len, gk); break;
                case 3: fL1 += (x - fL1) * lpA; fL2 += (fL1 - fL2) * lpA; fR1 += (y - fR1) * lpA; fR2 += (fR1 - fR2) * lpA; cl = fL2 * 0.8; cr = fR2 * 0.8; break;
                case 4: { rmPh += 170.0 / SR; rmPh -= Math.Floor(rmPh); double m = Math.Sin(rmPh * 2 * Math.PI); cl = Rev(bl, len, gk) * m * 1.2; cr = Rev(br, len, gk) * m * 1.2; break; }
                case 5: { double nz = Noise(); hpL = hpA * (hpL + nz - hpxL); hpxL = nz; double e = Math.Max(envL, envR); cl = cr = hpL * Math.Min(1, e * 4) * 0.5; break; }
                default: cl = x * 0.1; cr = y * 0.1; break;
            }
            gk = (gk + 1) % (len * 4);
            cl += x * keep; cr += y * keep;
            l[i] = (float)(x + (cl - x) * g); r[i] = (float)(y + (cr - y) * g);
        }
    }
}

// ======================================================================
//  GROWL: the croaky demon layer: vocal fry (the voice opens and shuts in uneven little pulses, like a croak), a sub-octave that follows
//  the voice, rasp (breathy noise that follows the voice), drive and a darker top. Put a pitch / formant shift before it and a dark reverb
//  after it for the satan voice the pros use (the Demon & Monster presets are built exactly like that).
// ======================================================================
class GrowlFx : Fx
{
    double ph, rate, env, envS, l1, l2, sl1, sl2, sq = 1, prev, sub1, sub2, hp, hpx, rl1, dL1, dL2, dR1, dR2; uint rnd = 1234567;
    public GrowlFx()
    {
        Type = "growl"; Title = "Growl"; Category = "Pitch & Voice";
        AddP("rate", "Croak speed", 15, 120, 45, "Hz"); AddP("jitter", "Unevenness", 0, 100, 45, "%"); AddP("depth", "Croak", 0, 100, 75, "%");
        AddP("sub", "Sub-octave", 0, 100, 45, "%"); AddP("rasp", "Rasp", 0, 100, 30, "%"); AddP("drive", "Drive", 0, 36, 10, "dB"); AddP("dark", "Top cut", 800, 16000, 6000, "Hz"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); ph = 0; rate = 0; env = envS = l1 = l2 = sl1 = sl2 = prev = sub1 = sub2 = hp = hpx = rl1 = dL1 = dL2 = dR1 = dR2 = 0; sq = 1; }
    double Rnd() { rnd ^= rnd << 13; rnd ^= rnd >> 17; rnd ^= rnd << 5; return (rnd & 0xFFFFFF) / 16777216.0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double baseRate = Get("rate"), jit = Get("jitter") / 100.0, depth = Get("depth") / 100.0, subL = Get("sub") / 100.0, rasp = Get("rasp") / 100.0, mix = Get("mix") / 100.0;
        double gain = Math.Pow(10, Get("drive") / 20.0), norm = 1.0 / Math.Sqrt(gain), dA = 1 - Math.Exp(-2 * Math.PI * Get("dark") / SR);
        double lpA = 1 - Math.Exp(-2 * Math.PI * 280 / SR), subA = 1 - Math.Exp(-2 * Math.PI * 180 / SR), hpA = Math.Exp(-2 * Math.PI * 1500 / SR), rlA = 1 - Math.Exp(-2 * Math.PI * 4500 / SR);
        double envAtt = 1 - Math.Exp(-1.0 / (0.003 * SR)), envRel = 1 - Math.Exp(-1.0 / (0.08 * SR));
        if (rate <= 0) rate = baseRate;
        for (int i = 0; i < n; i++)
        {
            double x = l[i], y = r[i], mono = (x + y) * 0.5, a = Math.Abs(mono); env += (a - env) * (a > env ? envAtt : envRel);
            // vocal fry: uneven glottal pulses (a sharp open, a quick close) that the voice is shaped by
            ph += rate / SR; if (ph >= 1) { ph -= 1; rate = baseRate * (1 + jit * (Rnd() - 0.5) * 0.9); }
            double pulse = Math.Exp(-ph * 6) * 3.46, m = (1 - depth) + depth * pulse;
            // the sub-octave: the low part of the voice flips a switch on every second crossing (a classic octave divider), at the voice's level
            l1 += (mono - l1) * lpA; l2 += (l1 - l2) * lpA; if (prev <= 0 && l2 > 0) sq = -sq; prev = l2; envS += (Math.Abs(l2) - envS) * 0.002;
            sub1 += (sq * envS * 2.2 - sub1) * subA; sub2 += (sub1 - sub2) * subA;
            // rasp: breathy noise in the upper mids that follows the voice
            double nz = Rnd() * 2 - 1; hp = hpA * (hp + nz - hpx); hpx = nz; rl1 += (hp - rl1) * rlA; double rs = rl1 * env * 3 * rasp;
            double add = sub2 * subL + rs;
            double ol = Math.Tanh((x * m + add) * gain) * norm, or = Math.Tanh((y * m + add) * gain) * norm;
            dL1 += (ol - dL1) * dA; dL2 += (dL1 - dL2) * dA; dR1 += (or - dR1) * dA; dR2 += (dR1 - dR2) * dA;
            l[i] = (float)(x + (dL2 - x) * mix); r[i] = (float)(y + (dR2 - y) * mix);
        }
    }
}
