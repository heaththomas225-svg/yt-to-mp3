using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX TAPE DIRT: a worn-out tape machine. DRIVE pushes the tape (warm, rounded saturation with a little low-end bump).
//  DIRT goes from clean to bad: first the highs fade, then the tape wobbles (flutter), then it drops out now and then.
//  STEREO MODE: NORMAL moves both sides together, DE-COUPLED lets each side wobble and drop out on its own (wider, messier).
//  LOW CUT takes out rumble, OUTPUT sets the level. Two VU meters show what goes in and what comes out.
// ======================================================================
class TapeDirtFx : Fx
{
    readonly float[] bL = new float[4800], bR = new float[4800]; int bw;
    double lpL, lpR, bumpL, bumpR, hpL, hpR, hpxL, hpxR, wowPh, flPh, wowPh2, flPh2, drop = 1, drop2 = 1, dropT, dropT2, vuIn, vuOut, outG = 1; uint seed = 777;
    public volatile float VuIn, VuOut;
    public TapeDirtFx()
    {
        Type = "pxtape"; Title = "Purphex Tape Dirt"; Category = "Purphex VST"; Blurb = "Worn-out tape machine: drive, dirt (dull, flutter, drop-outs), VU meters";
        AddP("drive", "Drive", 0, 10, 5, ""); AddP("dirt", "Dirt", 0, 10, 3, ""); AddC("stereo", "Stereo mode", new[] { "Normal", "De-coupled" }, 0); AddP("lowcut", "Low cut", 10, 2000, 10, "Hz"); AddP("out", "Output volume", -24, 6, 0, "dB");
    }
    public override void Reset() { base.Reset(); Array.Clear(bL, 0, bL.Length); Array.Clear(bR, 0, bR.Length); bw = 0; lpL = lpR = bumpL = bumpR = hpL = hpR = hpxL = hpxR = 0; wowPh = flPh = wowPh2 = flPh2 = 0; drop = drop2 = 1; dropT = dropT2 = 0; }
    double Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216.0; }
    static double Tape(double x, double k) { double y = x * k; return (y >= 0 ? Math.Tanh(y) : Math.Tanh(y * 0.92) / 0.92) / Math.Sqrt(k); }   // more drive: rounder, about the same loudness
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double drive = Get("drive") / 10.0, dirt = Get("dirt") / 10.0; bool dec = Get("stereo") >= 0.5f; double lc = Get("lowcut"), outT = Math.Pow(10, Get("out") / 20), gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        double k = 1 + drive * 6, bumpA = 1 - Math.Exp(-2 * Math.PI * 90 / SR), bumpAmt = drive * 0.35;
        double dull = Math.Min(1, dirt / 0.35), hc = 20000 * Math.Pow(0.15, dull), lpA = 1 - Math.Exp(-2 * Math.PI * Math.Min(SR * 0.45, hc) / SR);   // the first third of DIRT darkens
        double flut = Math.Max(0, Math.Min(1, (dirt - 0.3) / 0.4)), wowD = flut * 0.0025 * SR, flD = flut * 0.0004 * SR;                      // the middle wobbles
        double drops = Math.Max(0, (dirt - 0.65) / 0.35);                                                                                       // the last third drops out
        double hpA = lc <= 10.5 ? 0 : Math.Exp(-2 * Math.PI * lc / SR), vuA = 1 - Math.Exp(-1.0 / (0.3 * SR));
        const double ws = 0.6;   // wow speed (Hz)
        for (int i = 0; i < n; i++)
        {
            outG += (outT - outG) * gs; double xl = l[i], xr = r[i];
            vuIn += (Math.Abs((xl + xr) * 0.5) - vuIn) * vuA;
            // drive: tape saturation and its low-end bump
            double yl = Tape(xl, k), yr = Tape(xr, k); bumpL += (yl - bumpL) * bumpA; bumpR += (yr - bumpR) * bumpA; yl += bumpL * bumpAmt; yr += bumpR * bumpAmt;
            // dirt 1: the highs go
            lpL += (yl - lpL) * lpA; lpR += (yr - lpR) * lpA; yl = lpL; yr = lpR;
            // dirt 2: wow and flutter (a moving delay)
            wowPh += ws / SR; flPh += 7.3 / SR; if (wowPh > 1) wowPh -= 1; if (flPh > 1) flPh -= 1; wowPh2 += 0.53 / SR; flPh2 += 6.1 / SR; if (wowPh2 > 1) wowPh2 -= 1; if (flPh2 > 1) flPh2 -= 1;
            bL[bw] = (float)yl; bR[bw] = (float)yr;
            double dl = 4 + wowD * (1 + Math.Sin(2 * Math.PI * wowPh)) + flD * (1 + Math.Sin(2 * Math.PI * flPh)), dr = dec ? 4 + wowD * (1 + Math.Sin(2 * Math.PI * wowPh2)) + flD * (1 + Math.Sin(2 * Math.PI * flPh2)) : dl;
            yl = Read(bL, dl); yr = Read(bR, dr); bw = (bw + 1) % bL.Length;
            // dirt 3: drop-outs (the level dips for a moment)
            if (drops > 0)
            {
                if (dropT <= 0 && Rnd() < drops * 3.0 / SR) dropT = (0.02 + Rnd() * 0.08) * SR; double tgt = dropT > 0 ? 1 - drops * (0.5 + 0.5 * Rnd()) : 1; if (dropT > 0) dropT--; drop += (tgt - drop) * 0.004;
                if (dec) { if (dropT2 <= 0 && Rnd() < drops * 3.0 / SR) dropT2 = (0.02 + Rnd() * 0.08) * SR; double t2 = dropT2 > 0 ? 1 - drops * 0.7 : 1; if (dropT2 > 0) dropT2--; drop2 += (t2 - drop2) * 0.004; } else drop2 = drop;
                yl *= drop; yr *= drop2;
            }
            if (hpA > 0) { hpL = hpA * (hpL + yl - hpxL); hpxL = yl; hpR = hpA * (hpR + yr - hpxR); hpxR = yr; yl = hpL; yr = hpR; }
            yl *= outG; yr *= outG;
            if (double.IsNaN(yl) || double.IsNaN(yr)) { Reset(); yl = xl; yr = xr; }
            l[i] = (float)yl; r[i] = (float)yr; vuOut += (Math.Abs((yl + yr) * 0.5) - vuOut) * vuA;
        }
        VuIn = (float)vuIn; VuOut = (float)vuOut;
    }
    double Read(float[] b, double d) { double rp = bw - d; if (rp < 0) rp += b.Length; int i0 = (int)rp, i1 = (i0 + 1) % b.Length; double fr = rp - i0; return b[i0] + (b[i1] - b[i0]) * fr; }
}

#if !PURPHEX_CORE
class TapeDirtWindow : PxWindow
{
    readonly TapeDirtFx t;
    public TapeDirtWindow(TapeDirtFx f) : base(f, "Tape Dirt", new Size(780, 640)) { t = f; Accent = Gold; }
    protected override string SubTitle { get { return "worn-out tape machine"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        Vu(g, new RectangleF(a.X + 10, a.Y + 6, a.Width / 2 - 20, 130), t.VuIn, "INPUT"); Vu(g, new RectangleF(a.X + a.Width / 2 + 10, a.Y + 6, a.Width / 2 - 20, 130), t.VuOut, "OUTPUT");
        var body = new RectangleF(a.X, a.Y + 150, a.Width, a.Height - 150); Panel(g, body, "", Gold);
        Knob(g, body.X + 150, body.Y + 90, 52, "drive", "DRIVE", Gold, true);
        Knob(g, body.X + 150, body.Y + 270, 52, "dirt", "DIRT", Orange, true);
        Txt(g, "clean  >  less highs  >  more flutter  >  more drop-outs  >  bad", 10, Dim, new RectangleF(body.X + 10, body.Y + 368, 300, 16), StringAlignment.Center, false);
        Txt(g, "STEREO MODE", 10, Dim, new RectangleF(body.X + 440, body.Y + 30, 200, 14), StringAlignment.Near, true); Segments(g, new RectangleF(body.X + 440, body.Y + 48, 240, 26), "stereo", Gold, 10);
        Knob(g, body.X + 560, body.Y + 170, 30, "lowcut", "LOW CUT", Gold); Txt(g, fx.Get("lowcut") <= 10.5f ? "off" : "", 10, Dim, new RectangleF(body.X + 520, body.Y + 120, 80, 14), StringAlignment.Center, false);
        Knob(g, body.X + 560, body.Y + 290, 30, "out", "OUTPUT VOLUME", Gold);
    }
    void Vu(Graphics g, RectangleF r, float lin, string label)
    {
        Fill(g, Color.FromArgb(30, 24, 16), r, 10); var face = new RectangleF(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16);
        using (var b = new LinearGradientBrush(face, Color.FromArgb(255, 236, 170), Color.FromArgb(240, 190, 90), 90f)) using (var p = RR(face, 6)) g.FillPath(b, p);
        float cx = face.X + face.Width / 2, cy = face.Bottom + 30, rad = face.Height + 10;
        for (int db = -20; db <= 3; db += db < -10 ? 5 : db < 0 ? 2 : 1) { double a = -50 + (db + 20) / 23.0 * 100; double ra = a * Math.PI / 180; float x1 = cx + (float)Math.Sin(ra) * (rad - 18), y1 = cy - (float)Math.Cos(ra) * (rad - 18), x2 = cx + (float)Math.Sin(ra) * (rad - 8), y2 = cy - (float)Math.Cos(ra) * (rad - 8); using (var pen = new Pen(db >= 0 ? Color.FromArgb(200, 40, 30) : Color.FromArgb(60, 40, 20), 1.5f)) g.DrawLine(pen, x1, y1, x2, y2); }
        double vdb = 20 * Math.Log10(Math.Max(1e-5, lin * 2.2)); double ang = (-50 + (Math.Max(-20, Math.Min(3, vdb)) + 20) / 23.0 * 100) * Math.PI / 180;
        g.SetClip(face); using (var pen = new Pen(Color.FromArgb(40, 30, 20), 2)) g.DrawLine(pen, cx, cy, cx + (float)Math.Sin(ang) * rad, cy - (float)Math.Cos(ang) * rad); g.ResetClip();
        Txt(g, "VU", 14, Color.FromArgb(60, 40, 20), new RectangleF(cx - 30, face.Bottom - 30, 60, 20), StringAlignment.Center, true); Txt(g, label, 10, Dim, new RectangleF(r.X, r.Y - 2, r.Width, 12), StringAlignment.Center, true);
    }
}
#endif
