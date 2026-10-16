using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX VORTEX: moves the sound around you. ROTATE sets how fast it circles (free, or locked to the song), SHAPE bends the circle into a
//  figure of eight, SPACE adds the room around it and ECHO throws repeats that bounce side to side. Five movement sections, each with its
//  own amount and MIX: GAIN (louder in front, softer behind; COLOR), FILTER (darker behind your head), WIDTH (how far left and right it pans),
//  BEND (a little pitch sweep as it passes, like a car going by) and SLICE (it chops in time as it turns). INPUT, MIX and OUTPUT.
// ======================================================================
class VortexFx : Fx
{
    public static readonly string[] Sync = { "Free", "4 bars", "2 bars", "1 bar", "1/2", "1/4", "1/8", "1/16" }; static readonly double[] SyncBeats = { 0, 16, 8, 4, 2, 1, 0.5, 0.25 };
    readonly float[] dlL = new float[96000], dlR = new float[96000], bl = new float[8192], br = new float[8192]; int dw, bw;
    double ph, fL, fR, inG = 1, outG = 1, mixS = 1, revL, revR; readonly float[][] rb = new float[4][]; readonly int[] ri = new int[4]; static readonly int[] RT = { 1557, 1617, 1491, 1422 };
    public volatile float PosX, PosZ, Angle;
    public VortexFx()
    {
        Type = "pxvortex"; Title = "Purphex Vortex"; Category = "Purphex VST"; Blurb = "3D auto-panner: rotate, shape, space and echo with five movement sections";
        AddP("rate", "Rotate", 0.02f, 10, 0.5f, "Hz"); AddC("sync", "Rotate sync", Sync, 3); AddP("shape", "Shape", 0, 100, 0, "%"); AddP("space", "Space", 0, 100, 20, "%");
        AddP("echo", "Echo", 0, 100, 0, "%"); AddC("esync", "Echo time", new[] { "1/4", "1/8 dotted", "1/8", "1/16" }, 1);
        AddP("color", "Gain colour", 0, 100, 50, "%"); AddP("g_mix", "Gain mix", 0, 100, 100, "%");
        AddP("freq", "Filter frequency", 300, 18000, 3000, "Hz"); AddP("f_mix", "Filter mix", 0, 100, 70, "%");
        AddP("pan", "Width", 0, 100, 100, "%"); AddP("w_mix", "Width mix", 0, 100, 100, "%");
        AddP("range", "Bend range", 0, 100, 20, "%"); AddP("b_mix", "Bend mix", 0, 100, 0, "%");
        AddP("time", "Slice time", 0, 100, 50, "%"); AddP("s_mix", "Slice mix", 0, 100, 0, "%");
        AddP("in", "Input", -24, 24, 0, "dB"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 24, 0, "dB");
        for (int i = 0; i < 4; i++) rb[i] = new float[RT[i] * 3];
    }
    public override void Reset() { base.Reset(); Array.Clear(dlL, 0, dlL.Length); Array.Clear(dlR, 0, dlR.Length); Array.Clear(bl, 0, bl.Length); Array.Clear(br, 0, br.Length); dw = bw = 0; ph = 0; fL = fR = 0; revL = revR = 0; for (int i = 0; i < 4; i++) { Array.Clear(rb[i], 0, rb[i].Length); ri[i] = 0; } }
    public override void BeginBlock(object track, long pos, int n, bool transport) { if (transport && (int)Get("sync") > 0) { double beats = pos / (double)SR * Fx.Bpm / 60.0; ph = (beats / SyncBeats[(int)Get("sync")]) % 1.0 * 2 * Math.PI; } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int sy = (int)Get("sync"); double rate = sy == 0 ? Get("rate") : Fx.Bpm / 60.0 / SyncBeats[sy], dph = 2 * Math.PI * rate / SR;
        double shape = Get("shape") / 100.0, space = Get("space") / 100.0, echo = Get("echo") / 100.0, color = Get("color") / 100.0, gMix = Get("g_mix") / 100.0, fMix = Get("f_mix") / 100.0, freq = Get("freq");
        double pan = Get("pan") / 100.0, wMix = Get("w_mix") / 100.0, range = Get("range") / 100.0, bMix = Get("b_mix") / 100.0, sTime = Get("time") / 100.0, sMix = Get("s_mix") / 100.0;
        double inT = Math.Pow(10, Get("in") / 20), outT = Math.Pow(10, Get("out") / 20), mixT = Get("mix") / 100.0, gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        double eBeats = EchoBeats[(int)Get("esync")]; int eLen = (int)Math.Min(dlL.Length - 2, eBeats * 60.0 / Math.Max(20, Fx.Bpm) * SR);
        double sliceDiv = 1 + Math.Round(sTime * 7);   // how many chops per turn
        for (int i = 0; i < n; i++)
        {
            ph += dph; if (ph > 2 * Math.PI) ph -= 2 * Math.PI;
            double x = Math.Sin(ph), z = Math.Cos(ph); z = z * (1 - shape) + Math.Sin(2 * ph) * shape;   // where the sound is: left/right and front/back
            inG += (inT - inG) * gs; outG += (outT - outG) * gs; mixS += (mixT - mixS) * gs;
            double xl = l[i] * inG, xr = r[i] * inG, m = (xl + xr) * 0.5, yl = xl, yr = xr;
            // BEND: a short delay that moves with the distance (the pitch sweeps as it passes)
            bl[bw] = (float)yl; br[bw] = (float)yr; double d = 2 + range * 0.008 * SR * (1 + z) * 0.5; double rp = bw - d; if (rp < 0) rp += bl.Length; int i0 = (int)rp, i1 = (i0 + 1) % bl.Length; double fr = rp - i0;
            double bL = bl[i0] + (bl[i1] - bl[i0]) * fr, bR = br[i0] + (br[i1] - br[i0]) * fr; bw = (bw + 1) % bl.Length; yl += (bL - yl) * bMix; yr += (bR - yr) * bMix;
            // FILTER: darker when it is behind you
            double fc = Math.Min(SR * 0.45, freq * Math.Pow(4, (z + 1) * 0.5)), a = 1 - Math.Exp(-2 * Math.PI * fc / SR); fL += (yl - fL) * a; fR += (yr - fR) * a; yl += (fL - yl) * fMix; yr += (fR - yr) * fMix;
            // GAIN: louder in front, softer behind
            double gz = Math.Pow(10, color * 6 * z / 20); yl += (yl * gz - yl) * gMix; yr += (yr * gz - yr) * gMix;
            // WIDTH: panned to where it is (constant power)
            double pp = x * pan, pa = (pp + 1) * Math.PI / 4, mono = (yl + yr) * 0.5, pl = mono * Math.Cos(pa) * 1.4142, pr = mono * Math.Sin(pa) * 1.4142; yl += (pl - yl) * wMix; yr += (pr - yr) * wMix;
            // SLICE: chops in time with the turn
            double sp = (ph / (2 * Math.PI) * sliceDiv) % 1.0, sg = sp < 0.6 ? 1 : Math.Max(0, 1 - (sp - 0.6) * 8); sg = Math.Min(sg, Math.Min(1, sp * 40)); yl *= 1 - sMix + sMix * sg; yr *= 1 - sMix + sMix * sg;
            // ECHO: ping-pong repeats
            if (echo > 0) { int rd = (dw - eLen + dlL.Length) % dlL.Length; double eL = dlL[rd], eR = dlR[rd]; dlL[dw] = (float)((yl + yr) * 0.5 + eR * 0.45); dlR[dw] = (float)(eL * 0.45); dw = (dw + 1) % dlL.Length; yl += eL * echo; yr += eR * echo; }
            // SPACE: a little room
            if (space > 0)
            {
                double inv = (yl + yr) * 0.25, o0 = 0, o1 = 0;
                for (int k = 0; k < 4; k++) { var b = rb[k]; int len = (int)(RT[k] * SR / 44100.0); if (len >= b.Length) len = b.Length - 1; int j = ri[k]; if (j >= len) j = 0; double o = b[j]; b[j] = (float)(inv + o * 0.8); ri[k] = j + 1; if (k % 2 == 0) o0 += o; else o1 += o; }
                revL += (o0 - revL) * 0.5; revR += (o1 - revR) * 0.5; yl += revL * space * 0.5; yr += revR * space * 0.5;
            }
            double ol = (xl + (yl - xl) * mixS) * outG, or = (xr + (yr - xr) * mixS) * outG;
            if (double.IsNaN(ol) || double.IsNaN(or) || double.IsInfinity(ol) || double.IsInfinity(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)ol; r[i] = (float)or;
            if (i == n - 1) { PosX = (float)x; PosZ = (float)z; Angle = (float)ph; }
        }
    }
    static readonly double[] EchoBeats = { 1, 0.75, 0.5, 0.25 };
}

class VortexWindow : PxWindow
{
    readonly VortexFx v;
    public VortexWindow(VortexFx f) : base(f, "Vortex", new Size(940, 600)) { v = f; Accent = Cyan; }
    protected override string SubTitle { get { return "3D auto-panner"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the swirl in the middle turns with the sound; the dot is where the sound is now (top = in front)
        float cx = a.X + a.Width / 2, cy = a.Y + 150, R = 140;
        for (int arm = 0; arm < 4; arm++)
        {
            var pts = new PointF[60]; for (int i = 0; i < 60; i++) { double t = i / 59.0, ang = v.Angle * 1.0 + arm * Math.PI / 2 + t * 4.2; float rr = (float)(R * t); pts[i] = new PointF(cx + (float)Math.Cos(ang) * rr, cy + (float)Math.Sin(ang) * rr * 0.9f); }
            var col = arm % 2 == 0 ? Color.FromArgb(150, 60, 190, 255) : Color.FromArgb(150, 255, 150, 70); using (var pen = new Pen(col, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(pen, pts);
        }
        using (var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.FillEllipse(b, cx - 8, cy - 8, 16, 16);
        float dx = cx + v.PosX * R * 0.85f, dy = cy - v.PosZ * R * 0.85f; using (var b = new SolidBrush(Gold)) g.FillEllipse(b, dx - 9, dy - 9, 18, 18); using (var pen = new Pen(Color.White, 2)) g.DrawEllipse(pen, dx - 9, dy - 9, 18, 18);
        Txt(g, "FRONT", 10, Dim, new RectangleF(cx - 40, cy - R - 16, 80, 14), StringAlignment.Center, true); Txt(g, "BEHIND", 10, Dim, new RectangleF(cx - 40, cy + R + 2, 80, 14), StringAlignment.Center, true);
        // the four big knobs
        bool free = fx.Get("sync") < 0.5f;
        Knob(g, a.X + 90, a.Y + 80, 46, free ? "rate" : "sync", "ROTATE", Cyan, true); Choice(g, new RectangleF(a.X + 40, a.Y + 160, 100, 24), "sync", Cyan, 10.5f);
        Knob(g, a.X + 230, a.Y + 200, 40, "shape", "SHAPE", Cyan, true);
        Knob(g, a.Right - 90, a.Y + 80, 46, "echo", "ECHO", Orange, true); Choice(g, new RectangleF(a.Right - 140, a.Y + 160, 100, 24), "esync", Orange, 10.5f);
        Knob(g, a.Right - 230, a.Y + 200, 40, "space", "SPACE", Orange, true);
        // the five sections
        var sec = new RectangleF(a.X, a.Y + 330, a.Width, 150); Panel(g, sec, "", Violet); float w = sec.Width / 5;
        string[] names = { "GAIN", "FILTER", "WIDTH", "BEND", "SLICE" }; string[] k1 = { "color", "freq", "pan", "range", "time" }, l1 = { "COLOR", "FREQ", "PAN", "RANGE", "TIME" }, k2 = { "g_mix", "f_mix", "w_mix", "b_mix", "s_mix" };
        for (int i = 0; i < 5; i++) { float x0 = sec.X + w * i; Txt(g, names[i], 11, Ink, new RectangleF(x0, sec.Y + 8, w, 16), StringAlignment.Center, true); Knob(g, x0 + w / 2 - 34, sec.Y + 62, 20, k1[i], l1[i], Violet); Knob(g, x0 + w / 2 + 34, sec.Y + 62, 20, k2[i], "MIX", Violet); }
        HSlider(g, new RectangleF(a.X + 20, a.Bottom - 30, 220, 8), "in", "IN", Cyan); HSlider(g, new RectangleF(a.X + a.Width / 2 - 110, a.Bottom - 30, 220, 8), "mix", "MIX", Violet); HSlider(g, new RectangleF(a.Right - 240, a.Bottom - 30, 220, 8), "out", "OUT", Orange);
    }
}
