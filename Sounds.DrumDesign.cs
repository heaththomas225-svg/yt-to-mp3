using System;
using System.Collections.Generic;

// =====================================================================================================
//  THE DRUM DESIGNER: every pack gets its own drum kit, not the same kick with a different pitch. A pack's seed picks a KIT RECIPE: how its
//  kick is built (body sweep shape and length, a mid "knock", the click type, a sub tail, drive and tone), how its snare is built (body tones,
//  wire noise colour and length, crack, an optional clap layer and room), its hats (the metal ratios, brightness, noise against metal) and
//  its clap (how many hands, their spacing, the tone). The six kick and snare characters of a kit (Punch, Tight, Deep...) move around that
//  recipe, so a kit hangs together while two packs never share a kit. Everything ends at silence (no clicks in loops).
// =====================================================================================================
static class DrumDesign
{
    const int SR = 44100;
    sealed class F2   // a small state-variable filter (low / band / high pass)
    {
        double s1, s2; readonly double g, k; public F2(double hz, double q) { g = Math.Tan(Math.PI * Math.Min(hz, SR * 0.45) / SR); k = 1.0 / Math.Max(0.3, q); }
        public double Lp, Bp, Hp; public void Run(double x) { Hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); Bp = g * Hp + s1; s1 = g * Hp + Bp; Lp = g * Bp + s2; s2 = g * Bp + Lp; }
    }
    static double R(Rnd r, double a, double b) { return a + (b - a) * r.Next(); }

    public class KitRecipe
    {
        // kick
        public double Tau, StartMul, Hold, KnockHz, Knock, Sub, Click, ClickHz, Drive, Tone; public int ClickType, DriveType;
        // snare
        public double Body2, BodyMix, WireHz, WireQ, WireLen, Crack, ClapLayer, Room, SnDrive;
        // hats and clap
        public double[] Metal; public double HatNoise, HatTone, ClapSpread, ClapQ; public int ClapHands;
    }
    static readonly double[][] MetalSets = { new[] { 205.3, 304.4, 369.6, 522.7, 540.0, 800.0 }, new[] { 263.0, 400.0, 421.0, 474.0, 587.0, 845.0 }, new[] { 180.0, 296.0, 345.0, 489.0, 612.0, 733.0 }, new[] { 317.0, 341.0, 503.0, 557.0, 691.0, 912.0 }, new[] { 240.0, 357.0, 455.0, 528.0, 669.0, 1003.0 } };
    public static KitRecipe Recipe(int seed)
    {
        var r = new Rnd(seed * 5 + 1234567); var k = new KitRecipe();
        k.Tau = R(r, 0.008, 0.05); k.StartMul = R(r, 2.5, 7); k.Hold = r.Next() < 0.4f ? R(r, 0.02, 0.07) : 0; k.KnockHz = R(r, 100, 220); k.Knock = r.Next() < 0.6f ? R(r, 0.15, 0.6) : 0; k.Sub = R(r, 0, 0.5);
        k.ClickType = r.Int(3); k.Click = R(r, 0.15, 1.0); k.ClickHz = R(r, 1200, 6000); k.DriveType = r.Int(3); k.Drive = R(r, 1.0, 3.5); k.Tone = R(r, 3000, 12000);
        k.Body2 = R(r, 1.45, 2.1); k.BodyMix = R(r, 0.15, 0.6); k.WireHz = R(r, 2200, 7000); k.WireQ = R(r, 0.5, 1.4); k.WireLen = R(r, 0.6, 1.05); k.Crack = R(r, 0.1, 0.9); k.ClapLayer = r.Next() < 0.35f ? R(r, 0.2, 0.55) : 0; k.Room = r.Next() < 0.4f ? R(r, 0.1, 0.35) : 0; k.SnDrive = R(r, 1.0, 2.6);
        k.Metal = (double[])MetalSets[r.Int(MetalSets.Length)].Clone(); for (int i = 0; i < k.Metal.Length; i++) k.Metal[i] *= R(r, 0.94, 1.06); k.HatNoise = R(r, 0.15, 0.6); k.HatTone = R(r, 0.85, 1.2);
        k.ClapHands = 3 + r.Int(3); k.ClapSpread = R(r, 0.007, 0.016); k.ClapQ = R(r, 0.7, 1.4);
        return k;
    }
    static double Shape(double x, int type, double drive)
    {
        switch (type) { case 1: { double y = x * drive; return (y + 0.25 * y * y) / (1 + Math.Abs(y)) / Math.Max(1, drive * 0.6); } case 2: { double y = x * drive; return Math.Sin(Math.Max(-1.5, Math.Min(1.5, y))) / Math.Sin(Math.Min(1.5, drive)); } default: return Math.Tanh(x * drive) / Math.Tanh(drive); }
    }
    static Buf EndClean(float[] x) { int n = x.Length, f = Math.Min(n / 4, (int)(0.012 * SR)); for (int i = 0; i < f; i++) x[n - 1 - i] *= i / (float)f; double dc = 0; for (int i = 0; i < n; i++) dc += x[i]; dc /= n; if (Math.Abs(dc) > 1e-4) for (int i = 0; i < n; i++) x[i] -= (float)(dc * Math.Min(1.0, (n - 1 - i) / (double)Math.Max(1, f))); return new Buf(x); }

    // a kick: f0 (Hz), the length of its body (s), its character within the recipe (drive and click scale)
    public static Buf Kick(Rnd r, KitRecipe k, double f0, double startMul, double tauMul, double decay, double driveMul, double clickMul, double len)
    {
        int n = (int)(len * SR); var x = new float[n]; double ph = 0, phS = 0, phK = 0; var clickF = new F2(k.ClickHz, 0.9); var tone = new F2(k.Tone, 0.707); var tone2 = new F2(k.Tone, 0.707); double fStart = f0 * k.StartMul * startMul; double tau = k.Tau * tauMul;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR; double f = f0 + (fStart - f0) * Math.Exp(-t / tau); ph += 2 * Math.PI * f / SR; phS += 2 * Math.PI * f0 * 0.5 * (k.Sub > 0.3 ? 2 : 1) / SR; phK += 2 * Math.PI * k.KnockHz / SR;
            double envB = Math.Min(1, t / 0.0006) * (t < k.Hold ? 1 : Math.Exp(-(t - k.Hold) / decay)); double s = Math.Sin(ph) * envB;
            s += Math.Sin(phS) * k.Sub * Math.Exp(-t / (decay * 1.6)) * Math.Min(1, t / 0.004);
            s += Math.Sin(phK) * k.Knock * Math.Exp(-t / 0.032) * Math.Min(1, t / 0.001);
            double c = 0, cw = k.Click * clickMul;
            if (cw > 0 && t < 0.02)
            {
                if (k.ClickType == 0) { clickF.Run(r.Bi()); c = clickF.Hp * Math.Exp(-t / 0.003); }
                else if (k.ClickType == 1) { c = (Math.Sin(2 * Math.PI * k.ClickHz * t) > 0 ? 1 : -1) * Math.Exp(-t / 0.0015) * 0.6; }
                else { clickF.Run(r.Bi()); c = clickF.Bp * 2.2 * Math.Exp(-t / 0.006); }
                c *= Math.Min(1, t / 0.0002);
            }
            s += c * cw * 0.7; s = Shape(s, k.DriveType, 1 + (k.Drive - 1) * 0.5 * driveMul);
            tone.Run(s); tone2.Run(tone.Lp); x[i] = (float)tone2.Lp;
        }
        return EndClean(x);
    }
    // a snare: body pitch, its length, the noise amount, the snap
    public static Buf Snare(Rnd r, KitRecipe k, double body, double decay, double noiseAmt, double snapMul, double len)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n]; var wire = new F2(k.WireHz, k.WireQ); var hi = new F2(6500, 0.7); var crackF = new F2(3200, 1.2); var clapF = new F2(1200 + k.WireHz * 0.1, 1.4); double p1 = 0, p2 = 0;
        var room = new float[(int)(0.031 * SR)]; int ri = 0; var room2 = new float[(int)(0.047 * SR)]; int ri2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, drop = 1 + 0.3 * Math.Exp(-t / 0.018); p1 += 2 * Math.PI * body * drop / SR; p2 += 2 * Math.PI * body * k.Body2 * drop / SR;
            double s = (Math.Sin(p1) * (1 - k.BodyMix * 0.5) + Math.Sin(p2) * k.BodyMix) * Math.Exp(-t / 0.07) * Math.Min(1, t / 0.0006);
            double nz = r.Bi(); wire.Run(nz); hi.Run(nz); s += (wire.Bp * 2.8 + hi.Hp * 0.45) * noiseAmt * Math.Exp(-t / (decay * k.WireLen)) * Math.Min(1, t / 0.0008);
            crackF.Run(nz); s += crackF.Bp * k.Crack * snapMul * 2.0 * Math.Exp(-t / 0.004);
            if (k.ClapLayer > 0) { double env = 0; for (int h = 0; h < 3; h++) { double d = t - h * 0.009; if (d >= 0) env = Math.Max(env, Math.Exp(-d / (h == 2 ? 0.05 : 0.006))); } clapF.Run(nz); s += clapF.Bp * 2.5 * env * k.ClapLayer; }
            s = Math.Tanh(s * k.SnDrive) / Math.Tanh(k.SnDrive);
            double wl = s, wr = s;
            if (k.Room > 0) { double a = room[ri], b = room2[ri2]; room[ri] = (float)(s * 0.6 + a * 0.45); room2[ri2] = (float)(s * 0.6 + b * 0.4); ri = (ri + 1) % room.Length; ri2 = (ri2 + 1) % room2.Length; wl += a * k.Room; wr += b * k.Room; }
            l[i] = (float)wl; rr[i] = (float)wr;
        }
        var bl = EndClean(l); var br = EndClean(rr); bl.R = br.L; return bl;
    }
    // a hi-hat: length, brightness, how open (pitch of the metal)
    public static Buf Hat(Rnd r, KitRecipe k, double decay, double bright, double len, double pitch)
    {
        int n = (int)(len * SR); var x = new float[n]; var ph = new double[k.Metal.Length]; var bp = new F2(bright * 1000 * k.HatTone, 0.9); var hp = new F2(6800, 0.7); var hp2 = new F2(6800, 0.7); var lp = new F2(15000, 0.7);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, m = 0; for (int j = 0; j < ph.Length; j++) { double dt = k.Metal[j] * pitch / SR; m += ph[j] < 0.5 ? 1 : -1; ph[j] += dt; if (ph[j] >= 1) ph[j] -= 1; } m /= ph.Length;
            double s = m * (1 - k.HatNoise) + r.Bi() * k.HatNoise; bp.Run(s); hp.Run(bp.Bp * 2.2 + s * 0.4); hp2.Run(hp.Hp); lp.Run(hp2.Hp);
            x[i] = (float)(lp.Lp * Math.Exp(-t / decay) * Math.Min(1, t / 0.0004));
        }
        return EndClean(x);
    }
    // a clap: centre frequency, tail
    public static Buf Clap(Rnd r, KitRecipe k, double centre, double tail, double len)
    {
        int n = (int)(len * SR); var l = new float[n]; var rr = new float[n];
        for (int c = 0; c < 2; c++)
        {
            var x = c == 0 ? l : rr; var bp = new F2(centre * (c == 0 ? 1 : 1.04), k.ClapQ);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, env = 0; for (int h = 0; h < k.ClapHands; h++) { double d = t - h * k.ClapSpread * (1 + 0.15 * ((h * 7 + c) % 3)); if (d >= 0) env = Math.Max(env, Math.Exp(-d / (h == k.ClapHands - 1 ? tail : 0.006)) * (h == k.ClapHands - 1 ? 1 : 0.8)); }
                bp.Run(r.Bi()); x[i] = (float)(Math.Tanh(bp.Bp * env * 3.2 * 1.15) / Math.Tanh(1.15));
            }
        }
        var bl = EndClean(l); var br = EndClean(rr); bl.R = br.L; return bl;
    }
}
