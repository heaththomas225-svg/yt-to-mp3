using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX CONSTELLATION: a granular cloud. The sound of the last few seconds is kept, and tiny pieces of it (grains) are played back again
//  and again on five LANES, each with its own ON / OFF, GAIN and PITCH (an octave up, two up, down...). NUMBER OF GRAINS sets how thick the
//  cloud is, GRAIN LENGTH how long each piece is, MAX DELAY TIME how far back they can come from, FEEDBACK sends the cloud back into itself.
//  PLAYBACK forwards, backwards or both, WIDTH spreads the grains, REVERB MIX adds space, DRY and WET levels.
// ======================================================================
class ConstellationFx : Fx
{
    const int MaxG = 96;
    class Grain { public bool On; public int Lane; public double Pos, Step, Len, T; public float PanL, PanR; public float X; }
    readonly Grain[] grains = new Grain[MaxG]; float[] bufL = new float[96000 * 4 + 8], bufR = new float[96000 * 4 + 8]; int w;
    double spawnAcc, wetL, wetR; uint seed = 4242; readonly float[][] rb = new float[4][]; readonly int[] ri = new int[4]; static readonly int[] RT = { 1931, 2203, 1747, 2459 };
    public readonly float[] ViewX = new float[MaxG], ViewS = new float[MaxG]; public readonly int[] ViewLane = new int[MaxG];
    public ConstellationFx()
    {
        Type = "pxconst"; Title = "Purphex Constellation"; Category = "Purphex VST"; Blurb = "Granular cloud on five pitched lanes with feedback and reverb";
        AddC("dir", "Grain playback", new[] { "Forwards", "Backwards", "Both" }, 0);
        float[] pd = { 24, 12, 0, -12, 0 };
        for (int k = 0; k < 5; k++) { AddC("l" + k + "_on", "Lane " + (k + 1) + " on", new[] { "Off", "On" }, 1); AddP("l" + k + "_g", "Lane " + (k + 1) + " gain", 0, 100, 70, "%"); AddP("l" + k + "_p", "Lane " + (k + 1) + " pitch", -24, 24, pd[k], "st"); }
        AddP("num", "Number of grains", 1, 96, 40, ""); AddP("glen", "Grain length", 10, 2000, 250, "ms"); AddP("maxd", "Max delay time", 50, 4000, 1000, "ms"); AddP("fb", "Feedback", 0, 95, 30, "%");
        AddP("width", "Width", 0, 100, 80, "%"); AddP("rev", "Reverb mix", 0, 100, 40, "%"); AddP("dry", "Dry", -60, 6, 0, "dB"); AddP("wet", "Wet", -60, 6, -3, "dB");
        for (int i = 0; i < MaxG; i++) grains[i] = new Grain(); for (int i = 0; i < 4; i++) rb[i] = new float[RT[i] * 3];
    }
    public override void Reset() { base.Reset(); Array.Clear(bufL, 0, bufL.Length); Array.Clear(bufR, 0, bufR.Length); w = 0; foreach (var g in grains) g.On = false; spawnAcc = 0; wetL = wetR = 0; for (int i = 0; i < 4; i++) { Array.Clear(rb[i], 0, rb[i].Length); ri[i] = 0; } }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    static readonly string[] LOn = { "l0_on", "l1_on", "l2_on", "l3_on", "l4_on" }, LG = { "l0_g", "l1_g", "l2_g", "l3_g", "l4_g" }, LP = { "l0_p", "l1_p", "l2_p", "l3_p", "l4_p" };
    readonly bool[] lon = new bool[5]; readonly double[] lg = new double[5], lratio = new double[5];
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int dir = (int)Get("dir"); int lanes = 0; for (int k = 0; k < 5; k++) { lon[k] = Get(LOn[k]) >= 0.5f; lg[k] = Get(LG[k]) / 100.0; lratio[k] = Math.Pow(2, Get(LP[k]) / 12.0); if (lon[k]) lanes++; }
        double num = Get("num"), glen = Math.Max(64, Get("glen") * 0.001 * SR), maxd = Math.Min(bufL.Length - glen * 4 - 8, Get("maxd") * 0.001 * SR), fb = Get("fb") / 100.0, width = Get("width") / 100.0, rev = Get("rev") / 100.0;
        double dry = Math.Pow(10, Get("dry") / 20), wet = Math.Pow(10, Get("wet") / 20), rate = num / (glen / SR), norm = 1.4 / Math.Sqrt(Math.Max(1, num * 0.5));
        int len = bufL.Length;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i];
            // new grains, at the rate that keeps about NUMBER OF GRAINS playing at once
            spawnAcc += rate / SR;
            while (spawnAcc >= 1 && lanes > 0)
            {
                spawnAcc -= 1; Grain g = null; foreach (var x in grains) if (!x.On) { g = x; break; } if (g == null) break;
                int pick = (int)(Rnd() * lanes), lane = -1; for (int k = 0; k < 5; k++) if (lon[k] && pick-- == 0) { lane = k; break; } if (lane < 0) break;
                double ratio = lratio[lane]; bool back = dir == 1 || (dir == 2 && Rnd() < 0.5); double need = glen * ratio + 8, d = need + Rnd() * Math.Max(0, maxd - need);
                g.On = true; g.Lane = lane; g.Len = glen; g.T = 0; g.Step = back ? -ratio : ratio; g.Pos = back ? w - d + glen * ratio : w - d; g.X = (float)(d / Math.Max(1, maxd + need));
                double pan = 0.5 + (Rnd() - 0.5) * width; g.PanL = (float)Math.Cos(pan * Math.PI / 2); g.PanR = (float)Math.Sin(pan * Math.PI / 2);
            }
            double gl = 0, gr = 0;
            foreach (var g in grains)
            {
                if (!g.On) continue; double env = Math.Sin(Math.PI * g.T / g.Len); env *= env;
                double p = g.Pos; while (p < 0) p += len; while (p >= len) p -= len; int i0 = (int)p, i1 = (i0 + 1) % len; double fr = p - i0;
                double sl = bufL[i0] + (bufL[i1] - bufL[i0]) * fr, sr = bufR[i0] + (bufR[i1] - bufR[i0]) * fr, gg = env * lg[g.Lane] * norm;
                gl += (sl + sr) * 0.5 * gg * g.PanL * 1.414; gr += (sl + sr) * 0.5 * gg * g.PanR * 1.414;
                g.Pos += g.Step; g.T++; if (g.T >= g.Len) g.On = false;
            }
            if (rev > 0)
            {
                double inv = (gl + gr) * 0.25, o0 = 0, o1 = 0;
                for (int k = 0; k < 4; k++) { var b = rb[k]; int ln = Math.Min(b.Length - 1, (int)(RT[k] * SR / 44100.0)); int j = ri[k]; if (j >= ln) j = 0; double o = b[j]; b[j] = (float)(inv + o * 0.84); ri[k] = j + 1; if (k % 2 == 0) o0 += o; else o1 += o; }
                gl += o0 * rev * 0.6; gr += o1 * rev * 0.6;
            }
            // the sound (and the cloud, for FEEDBACK) is kept for the grains to come
            bufL[w] = (float)(xl + gl * fb * 0.7); bufR[w] = (float)(xr + gr * fb * 0.7); w = (w + 1) % len;
            double ol = xl * dry + gl * wet, or = xr * dry + gr * wet;
            if (double.IsNaN(ol) || double.IsNaN(or) || double.IsInfinity(ol) || double.IsInfinity(or)) { Reset(); ol = xl; or = xr; }
            l[i] = (float)Math.Max(-4, Math.Min(4, ol)); r[i] = (float)Math.Max(-4, Math.Min(4, or));
        }
        for (int k = 0; k < MaxG; k++) { var g = grains[k]; ViewX[k] = g.On ? g.X : -1; ViewS[k] = g.On ? (float)Math.Sin(Math.PI * g.T / g.Len) : 0; ViewLane[k] = g.Lane; }
    }
}

class ConstellationWindow : PxWindow
{
    readonly ConstellationFx c;
    public ConstellationWindow(ConstellationFx f) : base(f, "Constellation", new Size(900, 720)) { c = f; Accent = Pink; }
    protected override string SubTitle { get { return "granular cloud"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        Choice(g, new RectangleF(a.X, a.Y, 140, 26), "dir", Pink, 10.5f); Txt(g, "GRAIN PLAYBACK", 9.5f, Dim, new RectangleF(a.X, a.Y + 28, 140, 14), StringAlignment.Near, true);
        var lanes = new RectangleF(a.X, a.Y + 52, a.Width, 270); Panel(g, lanes, "", Pink);
        for (int k = 0; k < 5; k++)
        {
            float y = lanes.Y + 30 + k * 48; bool on = fx.Get("l" + k + "_on") >= 0.5f;
            Toggle(g, new RectangleF(lanes.X + 16, y - 11, 50, 22), "l" + k + "_on", Pink); Knob(g, lanes.X + 96, y, 14, "l" + k + "_g", "", Orange); hits[hits.Count - 1].R = new RectangleF(lanes.X + 82, y - 14, 28, 28);
            var pb = new RectangleF(lanes.X + 124, y - 11, 52, 22); Fill(g, Color.FromArgb(40, 30, 70), pb, 11); Txt(g, (fx.Get("l" + k + "_p") > 0 ? "+" : "") + fx.Get("l" + k + "_p").ToString("0"), 10.5f, Ink, pb, StringAlignment.Center, true);
            var pp = fx.Find("l" + k + "_p"); string id = "l" + k + "_p"; var h = Reg(pb, null, "Lane " + (k + 1) + " pitch: drag or wheel (semitones)"); h.P = pp; float[] start = { 0 }; h.Click = () => start[0] = pp.Val; h.Drag = dy => fx.Set(id, (float)Math.Round(start[0] + dy * 48)); h.Wheel = d => { Mark(); fx.Set(id, pp.Val + d); }; h.Double = () => { Mark(); fx.Set(id, pp.Def); };
            using (var pen = new Pen(Color.FromArgb(on ? 120 : 40, 180, 150, 220), 1)) g.DrawLine(pen, lanes.X + 190, y, lanes.Right - 16, y);
            for (int i = 0; i < c.ViewX.Length; i++) { if (c.ViewLane[i] != k || c.ViewX[i] < 0) continue; float x = lanes.X + 190 + (1 - c.ViewX[i]) * (lanes.Width - 210), s = 6 + c.ViewS[i] * 18; using (var b = new SolidBrush(Color.FromArgb((int)(60 + c.ViewS[i] * 150), i % 2 == 0 ? Pink : Orange))) g.FillEllipse(b, x - s / 2, y - s / 2, s, s); }
        }
        var mid = new RectangleF(a.X, lanes.Bottom + 10, a.Width, 150); Panel(g, mid, "", Orange); float w4 = mid.Width / 4; string[] ks = { "num", "glen", "maxd", "fb" }, ls = { "GRAINS", "GRAIN LENGTH", "MAX DELAY", "FEEDBACK" };
        for (int i = 0; i < 4; i++) Knob(g, mid.X + w4 * i + w4 / 2, mid.Y + 60, 38, ks[i], ls[i], Orange, true);
        var bot = new RectangleF(a.X, mid.Bottom + 10, a.Width, a.Bottom - mid.Bottom - 10); Panel(g, bot, "", Pink);
        Knob(g, bot.X + 80, bot.Y + 50, 24, "width", "WIDTH", Pink); Knob(g, bot.X + 200, bot.Y + 50, 24, "rev", "REVERB MIX", Pink);
        HSlider(g, new RectangleF(bot.X + 320, bot.Y + 40, bot.Width - 360, 8), "dry", "DRY", Violet); HSlider(g, new RectangleF(bot.X + 320, bot.Y + 84, bot.Width - 360, 8), "wet", "WET", Pink);
    }
}
