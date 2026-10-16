using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX STEREO LAB: see and fix the stereo picture. A scope shows where the sound sits (a line straight up is mono, wide clouds are wide,
//  sideways means out of phase) and a phase meter goes from -1 (cancels in mono) to +1 (mono). Each side has its input gain, a phase flip
//  and its own pan; then WIDTH, PAN, MID / SIDE balance and BASS MONO (everything under a frequency in the middle). A and B hold two settings
//  and the A-B slider morphs between them (it can be automated, for a mix that opens up into the chorus).
// ======================================================================
class StereoLabFx : Fx
{
    static readonly string[] Morph = { "lg", "rg", "lpan", "rpan", "width", "pan", "ms", "bass" };
    readonly double[] snapA = new double[8], snapB = new double[8]; bool haveA, haveB;
    double lpL, lpR, lpL2, lpR2, corrNum, corrL, corrR;
    public readonly float[] ScopeX = new float[2048], ScopeY = new float[2048]; public volatile int ScopeW; public volatile float Corr = 1, PkL, PkR, OutL, OutR;
    public StereoLabFx()
    {
        Type = "pxstereo"; Title = "Purphex Stereo Lab"; Category = "Purphex VST"; Blurb = "Stereo scope and phase meter with width, pan, mid / side, bass mono and A-B morph";
        AddP("lg", "Left input gain", -24, 24, 0, "dB"); AddP("rg", "Right input gain", -24, 24, 0, "dB"); AddC("lph", "Left phase flip", new[] { "Off", "On" }, 0); AddC("rph", "Right phase flip", new[] { "Off", "On" }, 0);
        AddP("lpan", "Left pan", -100, 100, -100, ""); AddP("rpan", "Right pan", -100, 100, 100, ""); AddP("width", "Width", 0, 200, 100, "%"); AddP("pan", "Pan", -100, 100, 0, "");
        AddP("ms", "Mid / side balance", -100, 100, 0, ""); AddP("bass", "Bass mono below", 0, 400, 0, "Hz"); AddP("morph", "A-B morph", 0, 100, 0, "%"); AddC("ab", "A-B morph on", new[] { "Off", "On" }, 0);
        for (int k = 0; k < 8; k++) { var p = Find(Morph[k]); AddP("a_" + Morph[k], "A " + p.Name, p.Min, p.Max, p.Def, p.Unit); AddP("b_" + Morph[k], "B " + p.Name, p.Min, p.Max, p.Def, p.Unit); }
        Hidden = string.Join(",", Array.ConvertAll(Morph, m => "a_" + m)) + "," + string.Join(",", Array.ConvertAll(Morph, m => "b_" + m));
    }
    public void Store(bool b) { foreach (var m in Morph) Set((b ? "b_" : "a_") + m, Get(m)); }
    public override void Reset() { base.Reset(); lpL = lpR = lpL2 = lpR2 = 0; corrNum = corrL = corrR = 0; }
    readonly double[] cur = new double[8];
    protected override void Update()
    {
        bool ab = Get("ab") >= 0.5f; double t = Get("morph") / 100.0;
        for (int k = 0; k < 8; k++) cur[k] = ab ? Get("a_" + Morph[k]) + (Get("b_" + Morph[k]) - Get("a_" + Morph[k])) * t : Get(Morph[k]);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double lg = Math.Pow(10, cur[0] / 20) * (Get("lph") >= 0.5f ? -1 : 1), rg = Math.Pow(10, cur[1] / 20) * (Get("rph") >= 0.5f ? -1 : 1);
        double lp = cur[2] / 100.0, rp = cur[3] / 100.0, width = cur[4] / 100.0, pan = cur[5] / 100.0, ms = cur[6] / 100.0, bassF = cur[7];
        // each side placed by its own pan (constant power), then width and mid / side, then the overall pan
        double lpa = (lp + 1) * Math.PI / 4, rpa = (rp + 1) * Math.PI / 4; double llG = Math.Cos(lpa) * 1.4142, lrG = Math.Sin(lpa) * 1.4142, rlG = Math.Cos(rpa) * 1.4142, rrG = Math.Sin(rpa) * 1.4142;
        double midG = Math.Min(1, 1 - ms) * 1, sideG = Math.Min(1, 1 + ms) * width; double pa = (pan + 1) * Math.PI / 4, pl = Math.Cos(pa) * 1.4142, pr = Math.Sin(pa) * 1.4142;
        double bA = bassF < 20 ? 0 : 1 - Math.Exp(-2 * Math.PI * bassF / SR);
        float pkL = 0, pkR = 0, oL = 0, oR = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i] * lg, xr = r[i] * rg; pkL = (float)Math.Max(pkL, Math.Abs(xl)); pkR = (float)Math.Max(pkR, Math.Abs(xr));
            double a = xl * llG + xr * rlG, b = xl * lrG + xr * rrG;
            double m = (a + b) * 0.5 * midG, s = (a - b) * 0.5 * sideG;
            if (bA > 0) { lpL += (s - lpL) * bA; lpL2 += (lpL - lpL2) * bA; s -= lpL2; }   // the side's low end is taken out: the bass is mono
            double yl = (m + s) * pl, yr = (m - s) * pr;
            l[i] = (float)yl; r[i] = (float)yr; oL = (float)Math.Max(oL, Math.Abs(yl)); oR = (float)Math.Max(oR, Math.Abs(yr));
            corrNum += (yl * yr - corrNum) * 0.0005; corrL += (yl * yl - corrL) * 0.0005; corrR += (yr * yr - corrR) * 0.0005;
            if ((i & 3) == 0) { int w = ScopeW; ScopeX[w] = (float)((yl - yr) * 0.7071); ScopeY[w] = (float)((yl + yr) * 0.7071); ScopeW = (w + 1) % ScopeX.Length; }
        }
        Corr = (float)(corrL * corrR > 1e-12 ? corrNum / Math.Sqrt(corrL * corrR) : 1);
        PkL = Math.Max(PkL * 0.85f, pkL); PkR = Math.Max(PkR * 0.85f, pkR); OutL = Math.Max(OutL * 0.85f, oL); OutR = Math.Max(OutR * 0.85f, oR);
    }
}

class StereoLabWindow : PxWindow
{
    readonly StereoLabFx s;
    public StereoLabWindow(StereoLabFx f) : base(f, "Stereo Lab", new Size(900, 640)) { s = f; Accent = Gold; }
    protected override string SubTitle { get { return "see and fix the stereo picture"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the scope in the middle
        float sz = 330; var sc = new RectangleF(a.X + a.Width / 2 - sz / 2, a.Y + 6, sz, sz);
        using (var b = new SolidBrush(Color.FromArgb(8, 5, 16))) g.FillEllipse(b, sc); using (var pen = new Pen(Color.FromArgb(140, 160, 150, 200), 3)) g.DrawEllipse(pen, sc);
        float cx = sc.X + sz / 2, cy = sc.Y + sz / 2; using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255))) { g.DrawLine(pen, cx, sc.Y + 10, cx, sc.Bottom - 10); g.DrawLine(pen, sc.X + 10, cy, sc.Right - 10, cy); g.DrawLine(pen, cx - sz * 0.3f, cy - sz * 0.3f, cx + sz * 0.3f, cy + sz * 0.3f); g.DrawLine(pen, cx - sz * 0.3f, cy + sz * 0.3f, cx + sz * 0.3f, cy - sz * 0.3f); }
        Txt(g, "MONO", 10, Dim, new RectangleF(cx - 30, sc.Y + 12, 60, 14), StringAlignment.Center, true); Txt(g, "L", 10, Dim, new RectangleF(cx - sz * 0.3f - 20, cy - sz * 0.3f - 20, 20, 14), StringAlignment.Center, true); Txt(g, "R", 10, Dim, new RectangleF(cx + sz * 0.3f, cy - sz * 0.3f - 20, 20, 14), StringAlignment.Center, true);
        Txt(g, "+S", 10, Dim, new RectangleF(sc.X + 14, cy - 16, 24, 14), StringAlignment.Center, true); Txt(g, "-S", 10, Dim, new RectangleF(sc.Right - 38, cy - 16, 24, 14), StringAlignment.Center, true);
        int n = s.ScopeX.Length, w = s.ScopeW;
        using (var b = new SolidBrush(Color.FromArgb(200, 255, 236, 170)))
            for (int i = 0; i < n; i++) { float x = s.ScopeX[(w + i) % n], y = s.ScopeY[(w + i) % n]; float px = cx - x * sz * 0.42f, py = cy - y * sz * 0.42f; if ((px - cx) * (px - cx) + (py - cy) * (py - cy) < sz * sz / 4.2f) g.FillRectangle(b, px, py, 1.6f, 1.6f); }
        // left and right columns
        Side(g, new RectangleF(a.X, a.Y + 6, 200, 330), "LEFT", "lg", "lph", "lpan", Cyan); Side(g, new RectangleF(a.Right - 200, a.Y + 6, 200, 330), "RIGHT", "rg", "rph", "rpan", Pink);
        // phase meter and levels
        var pm = new RectangleF(a.X + 60, sc.Bottom + 18, a.Width - 120, 70); Panel(g, pm, "", Gold);
        Lvl(g, new RectangleF(pm.X + 70, pm.Y + 10, pm.Width - 90, 8), s.PkL, "L in"); Lvl(g, new RectangleF(pm.X + 70, pm.Y + 22, pm.Width - 90, 8), s.PkR, "R in"); Lvl(g, new RectangleF(pm.X + 70, pm.Y + 34, pm.Width - 90, 8), s.OutL, "L out"); Lvl(g, new RectangleF(pm.X + 70, pm.Y + 46, pm.Width - 90, 8), s.OutR, "R out");
        var ph = new RectangleF(pm.X + 70, pm.Y + 58, pm.Width - 90, 8); Fill(g, Color.FromArgb(14, 8, 28), ph, 3); float c = Math.Max(-1, Math.Min(1, s.Corr)); float zx = ph.X + ph.Width / 2, vx = zx + c * ph.Width / 2;
        using (var b = new SolidBrush(c < 0 ? Color.Red : c < 0.3f ? Gold : Lime)) g.FillRectangle(b, Math.Min(zx, vx), ph.Y, Math.Abs(vx - zx) + 2, ph.Height);
        Txt(g, "Phase", 9.5f, Dim, new RectangleF(pm.X + 8, ph.Y - 3, 60, 13), StringAlignment.Near, true); Txt(g, c.ToString("+0.00;-0.00"), 9.5f, c < 0 ? Color.Red : Ink, new RectangleF(pm.Right - 50, pm.Y + 2, 44, 13), StringAlignment.Far, true);
        // the main controls and A-B
        var mc = new RectangleF(a.X, pm.Bottom + 10, a.Width, a.Bottom - pm.Bottom - 10); Panel(g, mc, "", Violet);
        float k = mc.Width / 6; Knob(g, mc.X + k * 0.5f, mc.Y + 44, 22, "width", "WIDTH", Gold); Knob(g, mc.X + k * 1.5f, mc.Y + 44, 22, "pan", "PAN", Gold); Knob(g, mc.X + k * 2.5f, mc.Y + 44, 22, "ms", "MID - SIDE", Gold); Knob(g, mc.X + k * 3.5f, mc.Y + 44, 22, "bass", "BASS MONO", Gold);
        Toggle(g, new RectangleF(mc.X + k * 4.2f, mc.Y + 14, 80, 24), "ab", Violet, "A-B ON", "A-B OFF");
        Button(g, new RectangleF(mc.X + k * 4.2f, mc.Y + 46, 38, 24), "A", false, Cyan, () => { Mark(); s.Store(false); }, "Store the settings now in A", 11);
        Button(g, new RectangleF(mc.X + k * 4.2f + 42, mc.Y + 46, 38, 24), "B", false, Pink, () => { Mark(); s.Store(true); }, "Store the settings now in B", 11);
        HSlider(g, new RectangleF(mc.X + k * 5 - 20, mc.Y + 40, k - 10, 8), "morph", "A  -  B", Violet);
    }
    void Side(Graphics g, RectangleF r, string title, string gain, string phase, string pan, Color col)
    {
        Txt(g, title, 18, Color.FromArgb(160, 170, 160, 200), new RectangleF(r.X, r.Y, r.Width, 26), StringAlignment.Center, true);
        Knob(g, r.X + r.Width / 2, r.Y + 80, 28, gain, "INPUT GAIN", col); Toggle(g, new RectangleF(r.X + r.Width / 2 - 40, r.Y + 150, 80, 24), phase, Orange, "PHASE Ø", "PHASE Ø");
        Knob(g, r.X + r.Width / 2, r.Y + 240, 28, pan, title == "LEFT" ? "LEFT PAN" : "RIGHT PAN", col);
    }
    void Lvl(Graphics g, RectangleF r, float lin, string label)
    {
        Fill(g, Color.FromArgb(14, 8, 28), r, 3); double db = 20 * Math.Log10(Math.Max(1e-5, lin)); float f = (float)Math.Max(0, Math.Min(1, (db + 72) / 72));
        using (var b = new SolidBrush(db > -1 ? Color.Red : label.Contains("out") ? Color.FromArgb(220, 90, 110) : Color.FromArgb(230, 220, 180))) g.FillRectangle(b, r.X, r.Y, r.Width * f, r.Height);
        Txt(g, label, 9.5f, Dim, new RectangleF(r.X - 64, r.Y - 3, 60, 13), StringAlignment.Far, false);
    }
}
