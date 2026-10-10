using System;

// Phaser and flanger for the Bass Lab effect chain.
class Phaser : Fx
{
    double[] zl = new double[12], zr = new double[12]; double ph, rate, depth, fb, mix, fbL, fbR; int stages = 4;
    public Phaser()
    {
        Type = "phaser"; Title = "Phaser";
        AddP("rate", "Rate", 0.05f, 8, 0.4f, "Hz"); AddP("depth", "Depth", 0, 100, 70, "%"); AddP("fb", "Feedback", 0, 90, 40, "%"); AddC("stages", "Stages", new[] { "4", "6", "8", "12" }, 0); AddP("mix", "Mix", 0, 100, 50, "%");
    }
    protected override void Update() { rate = Get("rate"); depth = Get("depth") / 100.0; fb = Get("fb") / 100.0; mix = Get("mix") / 100.0; stages = new[] { 4, 6, 8, 12 }[(int)Get("stages")]; }
    public override void Reset() { base.Reset(); Array.Clear(zl, 0, zl.Length); Array.Clear(zr, 0, zr.Length); fbL = fbR = 0; }
    double AP(double x, double[] z, int i, double a) { double y = -a * x + z[i]; z[i] = a * y + x; return y; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            double sl = 0.5 + 0.5 * Math.Sin(DspMath.TwoPi * ph), sr = 0.5 + 0.5 * Math.Sin(DspMath.TwoPi * (ph + 0.25));
            double fl = 250 * Math.Pow(16, sl * depth), fr = 250 * Math.Pow(16, sr * depth);   // sweep 250 Hz .. 4 kHz at full depth
            double al = (Math.Tan(Math.PI * fl / SR) - 1) / (Math.Tan(Math.PI * fl / SR) + 1), ar = (Math.Tan(Math.PI * fr / SR) - 1) / (Math.Tan(Math.PI * fr / SR) + 1);
            double xl = l[i] + fbL * fb, xr = r[i] + fbR * fb;
            for (int s = 0; s < stages; s++) { xl = AP(xl, zl, s, al); xr = AP(xr, zr, s, ar); }
            fbL = xl; fbR = xr;
            l[i] = (float)(l[i] * (1 - mix * 0.5) + xl * mix * 0.5 * 1.0); r[i] = (float)(r[i] * (1 - mix * 0.5) + xr * mix * 0.5 * 1.0);
            ph += rate / SR; if (ph >= 1) ph -= 1;
        }
    }
}

class Flanger : Fx
{
    float[] bl = new float[4096], br = new float[4096]; int w; double ph, rate, depth, fb, mix, delay;
    public Flanger()
    {
        Type = "flanger"; Title = "Flanger";
        AddP("rate", "Rate", 0.05f, 6, 0.3f, "Hz"); AddP("depth", "Depth", 0, 100, 60, "%"); AddP("delay", "Delay", 0.2f, 6, 1.5f, "ms"); AddP("fb", "Feedback", -90, 90, 45, "%"); AddP("mix", "Mix", 0, 100, 50, "%");
    }
    protected override void Update() { rate = Get("rate"); depth = Get("depth") / 100.0; delay = Get("delay") * 0.001 * SR; fb = Get("fb") / 100.0; mix = Get("mix") / 100.0; }
    public override void Reset() { base.Reset(); Array.Clear(bl, 0, bl.Length); Array.Clear(br, 0, br.Length); }
    static float Tap(float[] b, int w, double d) { double rp = w - d; while (rp < 0) rp += b.Length; int i0 = (int)rp % b.Length, i1 = (i0 + 1) % b.Length; double f = rp - Math.Floor(rp); return (float)(b[i0] * (1 - f) + b[i1] * f); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            double mod = delay * (1 + depth * Math.Sin(DspMath.TwoPi * ph)), mod2 = delay * (1 + depth * Math.Sin(DspMath.TwoPi * (ph + 0.25)));
            mod = Math.Max(1, Math.Min(bl.Length - 2, mod)); mod2 = Math.Max(1, Math.Min(bl.Length - 2, mod2));
            float dl = Tap(bl, w, mod), dr = Tap(br, w, mod2);
            bl[w] = (float)(l[i] + dl * fb); br[w] = (float)(r[i] + dr * fb);
            l[i] = (float)(l[i] * (1 - mix * 0.5) + dl * mix * 0.7); r[i] = (float)(r[i] * (1 - mix * 0.5) + dr * mix * 0.7);
            ph += rate / SR; if (ph >= 1) ph -= 1; if (++w >= bl.Length) w = 0;
        }
    }
}
