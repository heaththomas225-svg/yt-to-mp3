using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX SUBFORGE: an 808 and bass instrument. A main oscillator (sine, triangle, saw, square; octave, tune, fine), a SUB layer
//  (a sine one or two octaves down), PUNCH (the pitch drops into the note, the classic 808 knock), an amp envelope (attack, decay,
//  sustain, release), a filter (low-pass, high-pass or band-pass with resonance, envelope amount and key tracking), GLIDE between notes,
//  mono (one note, sliding) or poly. Then a DISTORTION section (tube, hard, fold or fuzz, only above its low cut, darkened by its high cut)
//  and a COMPRESSOR, each on or off, and the level. The window shows the master spectrum, and has a keyboard to play it.
// ======================================================================
class SubForgeFx : InstrumentFx
{
    public static readonly string[] Waves = { "Sine", "Triangle", "Saw", "Square" };
    class Voice { public bool On, Rel; public int Note; public double Vel, Ph, SubPh, Cur, Target, Env, Fenv, T, S1, S2; public int Stage; public long Age; }
    readonly Voice[] voices = new Voice[8]; List<KeyValuePair<int, MidiEv>> pending; readonly List<int> held = new List<int>(); long age;
    double dLp, dHp, dHpx, compEnv, lastFreq;
    public readonly PxScope Scope = new PxScope(8192); public volatile float OutPk, CompGr; public volatile float EnvView;
    public SubForgeFx()
    {
        Type = "pxsubforge"; Title = "Purphex SubForge"; Category = "Instruments"; Blurb = "808 and bass instrument: punch, sub, glide, filter, distortion, compressor";
        AddC("wave", "Wave", Waves, 0); AddP("oct", "Octave", -3, 2, 0, ""); AddP("tune", "Tune", -12, 12, 0, "st"); AddP("fine", "Fine", -100, 100, 0, "ct");
        AddC("sub_on", "Sub", new[] { "Off", "On" }, 1); AddP("sub_lvl", "Sub level", 0, 100, 35, "%"); AddC("sub_oct", "Sub octave", new[] { "-1 octave", "-2 octaves" }, 0);
        AddP("punch", "Punch (pitch drop)", 0, 24, 7, "st"); AddP("ptime", "Punch time", 5, 300, 40, "ms");
        AddP("a", "Attack", 0.1f, 2000, 1, "ms"); AddP("d", "Decay", 5, 10000, 1300, "ms"); AddP("s", "Sustain", 0, 100, 14, "%"); AddP("r", "Release", 5, 5000, 300, "ms");
        AddC("ftype", "Filter", new[] { "Off", "Low-pass", "High-pass", "Band-pass" }, 1); AddP("cut", "Cutoff", 20, 20000, 4000, "Hz"); AddP("res", "Resonance", 0, 100, 15, "%"); AddP("fenv", "Filter envelope", -100, 100, 20, "%"); AddP("track", "Key tracking", 0, 100, 50, "%");
        AddP("glide", "Glide", 0, 1000, 60, "ms"); AddC("mono", "Voices", new[] { "Mono (slides)", "Poly" }, 0); AddP("vel", "Velocity", 0, 100, 60, "%");
        AddC("d_on", "Distortion", new[] { "Off", "On" }, 0); AddC("d_mode", "Distortion type", new[] { "Tube", "Hard", "Fold", "Fuzz" }, 0); AddP("d_drive", "Drive", 0, 100, 30, "%");
        AddP("d_lc", "Distortion low cut", 5, 500, 6, "Hz"); AddP("d_hc", "Distortion high cut", 500, 20000, 8000, "Hz"); AddP("d_mix", "Distortion mix", 0, 100, 100, "%");
        AddC("c_on", "Compressor", new[] { "Off", "On" }, 0); AddP("c_thr", "Compressor threshold", -60, 0, -20, "dB"); AddP("c_ratio", "Compressor ratio", 1, 20, 4, ":1"); AddP("c_rel", "Compressor release", 10, 1000, 120, "ms"); AddP("c_gain", "Compressor gain", 0, 24, 4, "dB"); AddP("c_mix", "Compressor mix", 0, 100, 100, "%");
        AddP("level", "Level", -24, 12, -3, "dB");
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }
    public override bool IsInstrument { get { return true; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new SubForgeWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    public override void CloseWindow() { }
    public override string WindowLabel { get { return "Open Purphex SubForge"; } }
    public override void Reset() { base.Reset(); foreach (var v in voices) { v.On = false; v.Env = 0; v.S1 = v.S2 = 0; } held.Clear(); dLp = dHp = dHpx = compEnv = 0; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }

    double NoteHz(double note) { return 440 * Math.Pow(2, (note + Get("oct") * 12 + Get("tune") + Get("fine") / 100 - 69) / 12.0); }
    void VOn(int note, int vel)
    {
        held.Remove(note); held.Add(note); bool mono = Get("mono") < 0.5f; Voice v;
        if (mono) v = voices[0];
        else { v = null; foreach (var x in voices) if (!x.On) { v = x; break; } if (v == null) { v = voices[0]; foreach (var x in voices) if (x.Age < v.Age) v = x; } }
        bool slide = mono && v.On && !v.Rel && Get("glide") > 0;
        v.Note = note; v.Vel = vel / 127.0; v.Target = note; v.Age = ++age; v.Rel = false;
        if (!slide) { v.Cur = note; v.T = 0; v.Stage = 1; if (!v.On) { v.Env = 0; v.Ph = 0; v.SubPh = 0; v.S1 = v.S2 = 0; } }
        v.On = true;
    }
    void VOff(int note)
    {
        held.Remove(note);
        if (Get("mono") < 0.5f) { var v = voices[0]; if (v.On && v.Note == note) { if (held.Count > 0) { v.Note = held[held.Count - 1]; v.Target = v.Note; } else v.Rel = true; } return; }
        foreach (var v in voices) if (v.On && !v.Rel && v.Note == note) v.Rel = true;
    }
    void Event(MidiEv e)
    {
        int st = e.S & 0xF0;
        if (st == 0x90 && e.D2 > 0) VOn(e.D1, e.D2); else if (st == 0x80 || (st == 0x90 && e.D2 == 0)) VOff(e.D1);
        else if (st == 0xB0 && (e.D1 == 123 || e.D1 == 120)) { foreach (var v in voices) v.Rel = true; held.Clear(); if (e.D1 == 120) foreach (var v in voices) { v.On = false; v.Env = 0; } }
    }
    static double PolyBlep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int ei = 0; var ev = pending; pending = null;
        int wave = (int)Get("wave"); bool sub = Get("sub_on") >= 0.5f; double subLvl = Get("sub_lvl") / 100.0, subMul = Get("sub_oct") >= 0.5f ? 0.25 : 0.5;
        double punch = Get("punch"), ptime = Get("ptime") * 0.001 * SR;
        double aS = Math.Max(1, Get("a") * 0.001 * SR), dC = Math.Exp(-4.6 / Math.Max(1, Get("d") * 0.001 * SR)), sus = Get("s") / 100.0, rC = Math.Exp(-4.6 / Math.Max(1, Get("r") * 0.001 * SR));
        int ftype = (int)Get("ftype"); double cut = Get("cut"), res = Get("res") / 100.0, fenv = Get("fenv") / 100.0, track = Get("track") / 100.0, k = 2 - 1.95 * res;
        double glideC = Get("glide") <= 0 ? 0 : Math.Exp(-1.0 / (Get("glide") * 0.001 * SR)), velAmt = Get("vel") / 100.0;
        bool dOn = Get("d_on") >= 0.5f; int dMode = (int)Get("d_mode"); double drive = 1 + Get("d_drive") / 100.0 * 30, dMix = Get("d_mix") / 100.0, dLcA = Math.Exp(-2 * Math.PI * Get("d_lc") / SR), dHcA = 1 - Math.Exp(-2 * Math.PI * Math.Min(Get("d_hc"), SR * 0.45) / SR);
        bool cOn = Get("c_on") >= 0.5f; double cThr = Get("c_thr"), cRatio = Get("c_ratio"), cRel = Math.Exp(-1.0 / (Get("c_rel") * 0.001 * SR)), cAtt = Math.Exp(-1.0 / (0.002 * SR)), cGain = Math.Pow(10, Get("c_gain") / 20), cMix = Get("c_mix") / 100.0;
        double level = Math.Pow(10, Get("level") / 20); float pk = 0; double grMax = 0;
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i) { Event(ev[ei].Value); ei++; }
            double s = 0;
            foreach (var v in voices)
            {
                if (!v.On) continue;
                // envelope
                if (v.Rel) { v.Env *= rC; if (v.Env < 0.0003) { v.On = false; v.Env = 0; continue; } }
                else if (v.Stage == 1) { v.Env += 1.0 / aS; if (v.Env >= 1) { v.Env = 1; v.Stage = 2; } }
                else v.Env = sus + (v.Env - sus) * dC;
                // pitch: glide toward the note, plus the punch drop at the start
                if (glideC > 0) v.Cur = v.Target + (v.Cur - v.Target) * glideC; else v.Cur = v.Target;
                double drop = punch * Math.Exp(-v.T / Math.Max(1, ptime)); v.T++;
                double hz = NoteHz(v.Cur + drop), dt = Math.Min(0.45, hz / SR); lastFreq = hz;
                v.Ph += dt; if (v.Ph >= 1) v.Ph -= 1;
                double o;
                switch (wave)
                {
                    case 1: o = 1 - 4 * Math.Abs(v.Ph - 0.5); break;
                    case 2: o = 2 * v.Ph - 1 - PolyBlep(v.Ph, dt); break;
                    case 3: { o = v.Ph < 0.5 ? 1 : -1; o += PolyBlep(v.Ph, dt); double t2 = v.Ph + 0.5; if (t2 >= 1) t2 -= 1; o -= PolyBlep(t2, dt); break; }
                    default: o = Math.Sin(2 * Math.PI * v.Ph); break;
                }
                if (sub) { v.SubPh += dt * subMul; if (v.SubPh >= 1) v.SubPh -= 1; o = o * (1 - subLvl * 0.5) + Math.Sin(2 * Math.PI * v.SubPh) * subLvl; }
                // filter (state-variable), cutoff moved by the envelope and the note
                if (ftype > 0)
                {
                    double fc = cut * Math.Pow(2, fenv * 6 * v.Env) * Math.Pow(hz / 261.6, track); fc = Math.Max(20, Math.Min(SR * 0.45, fc)); double g = Math.Tan(Math.PI * fc / SR);
                    double hp = (o - (k + g) * v.S1 - v.S2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + v.S1; v.S1 = bp + v1; double v2 = g * bp, lp = v2 + v.S2; v.S2 = lp + v2;
                    o = ftype == 1 ? lp : ftype == 2 ? hp : bp * k;
                }
                double vg = 1 - velAmt + velAmt * v.Vel; s += o * v.Env * vg * 0.6;
                EnvView = (float)v.Env;
            }
            // distortion: only above its low cut, then darkened by its high cut
            if (dOn)
            {
                dHp = dLcA * (dHp + s - dHpx); dHpx = s; double lo = s - dHp, x = dHp * drive, y;
                switch (dMode) { case 1: y = Math.Max(-1, Math.Min(1, x)); break; case 2: y = Math.Sin(x * 1.2); break; case 3: y = Math.Sign(x) * (1 - Math.Exp(-Math.Abs(x) * 2)); break; default: y = x >= 0 ? Math.Tanh(x) : Math.Tanh(x * 0.75) / 0.75; break; }
                y /= Math.Sqrt(drive) * 0.6 + 0.4; dLp += (y - dLp) * dHcA; double wet = lo + dLp; s = s + (wet - s) * dMix;
            }
            if (cOn)
            {
                double lev = 20 * Math.Log10(Math.Abs(s) + 1e-9), over = Math.Max(0, lev - cThr), gr = over - over / cRatio;
                compEnv = gr > compEnv ? cAtt * compEnv + (1 - cAtt) * gr : cRel * compEnv + (1 - cRel) * gr; grMax = Math.Max(grMax, compEnv);
                double c = s * Math.Pow(10, -compEnv / 20) * cGain; s = s + (c - s) * cMix;
            }
            s *= level; if (double.IsNaN(s) || double.IsInfinity(s)) { Reset(); s = 0; }
            l[i] = (float)s; r[i] = (float)s; pk = (float)Math.Max(pk, Math.Abs(s)); Scope.Push((float)s);
        }
        while (ev != null && ei < ev.Count) { Event(ev[ei].Value); ei++; }
        OutPk = Math.Max(OutPk * 0.85f, pk); CompGr = (float)grMax;
    }
}

#if !PURPHEX_CORE
class SubForgeWindow : PxWindow
{
    readonly SubForgeFx sf; int octave = 2; readonly HashSet<int> down = new HashSet<int>();
    public SubForgeWindow(SubForgeFx f) : base(f, "SubForge", new Size(1060, 760)) { sf = f; Accent = Gold; FormClosed += (a, b) => { foreach (var nn in down) sf.NoteOff(nn); }; }
    protected override string SubTitle { get { return "808 and bass instrument"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        float lw = 500;
        // SOUND: oscillator, sub, punch, envelope
        var snd = new RectangleF(a.X, a.Y, lw, 290); Panel(g, snd, "SOUND", Gold);
        Segments(g, new RectangleF(snd.X + 16, snd.Y + 28, 300, 26), "wave", Gold);
        Knob(g, snd.X + 360, snd.Y + 50, 20, "oct", "OCTAVE", Gold); Knob(g, snd.X + 430, snd.Y + 50, 20, "tune", "TUNE", Gold);
        // the envelope drawn
        var er = new RectangleF(snd.X + 16, snd.Y + 96, 300, 90); Fill(g, Color.FromArgb(14, 8, 28), er, 8);
        { float at = Frac(fx.Find("a")), de = Frac(fx.Find("d")), su = fx.Get("s") / 100f, re = Frac(fx.Find("r")); float x0 = er.X + 8, w = er.Width - 16, h = er.Height - 16, y0 = er.Bottom - 8;
          var pts = new[] { new PointF(x0, y0), new PointF(x0 + w * 0.22f * at + 2, er.Y + 8), new PointF(x0 + w * 0.22f * at + 2 + w * 0.3f * de + 4, y0 - h * su), new PointF(x0 + w * 0.75f, y0 - h * su), new PointF(x0 + w * 0.75f + w * 0.25f * re, y0) };
          using (var pen = new Pen(Gold, 2.2f)) g.DrawLines(pen, pts); foreach (var p in pts) using (var b = new SolidBrush(Ink)) g.FillEllipse(b, p.X - 3, p.Y - 3, 6, 6);
          using (var pen = new Pen(Color.FromArgb(160, Cyan), 1.5f)) { float lv = sf.EnvView; g.DrawLine(pen, er.X + 4, y0 - h * lv, er.Right - 4, y0 - h * lv); } }
        Knob(g, snd.X + 360, snd.Y + 120, 20, "fine", "FINE", Gold); Knob(g, snd.X + 430, snd.Y + 120, 20, "glide", "GLIDE", Gold);
        float ey = snd.Y + 232; Knob(g, snd.X + 50, ey, 20, "a", "ATTACK", Gold); Knob(g, snd.X + 125, ey, 20, "d", "DECAY", Gold); Knob(g, snd.X + 200, ey, 20, "s", "SUSTAIN", Gold); Knob(g, snd.X + 275, ey, 20, "r", "RELEASE", Gold);
        Knob(g, snd.X + 360, ey - 30, 20, "punch", "PUNCH", Pink); Knob(g, snd.X + 430, ey - 30, 20, "ptime", "PUNCH TIME", Pink);
        Segments(g, new RectangleF(snd.X + 330, snd.Bottom - 30, 150, 22), "mono", Gold, 9.5f);
        // SUB and FILTER
        var flt = new RectangleF(a.X, snd.Bottom + 10, lw, 180); Panel(g, flt, "SUB  &  FILTER", Lime);
        Toggle(g, new RectangleF(flt.X + 16, flt.Y + 30, 70, 24), "sub_on", Lime, "SUB ON", "SUB OFF"); Segments(g, new RectangleF(flt.X + 16, flt.Y + 62, 140, 22), "sub_oct", Lime, 9.5f); Knob(g, flt.X + 200, flt.Y + 60, 20, "sub_lvl", "SUB LEVEL", Lime);
        Choice(g, new RectangleF(flt.X + 260, flt.Y + 30, 110, 24), "ftype", Lime); Knob(g, flt.X + 300, flt.Y + 104, 20, "cut", "CUTOFF", Lime); Knob(g, flt.X + 370, flt.Y + 104, 20, "res", "RESO", Lime);
        Knob(g, flt.X + 440, flt.Y + 104, 20, "fenv", "ENV", Lime); Knob(g, flt.X + 150, flt.Y + 130, 18, "track", "TRACK", Lime); Knob(g, flt.X + 60, flt.Y + 130, 18, "vel", "VELOCITY", Lime);
        // master spectrum
        var ms = new RectangleF(a.X + lw + 10, a.Y, a.Width - lw - 10, 290); Panel(g, ms, "MASTER", Cyan);
        var sp = new RectangleF(ms.X + 14, ms.Y + 28, ms.Width - 80, ms.Height - 44); Fill(g, Color.FromArgb(14, 8, 28), sp, 8);
        { var db = sf.Scope.Spectrum(4096); var pts = new List<PointF> { new PointF(sp.X, sp.Bottom) };
          for (float x = sp.X; x <= sp.Right; x += 2) { double f = 20 * Math.Pow(1000, (x - sp.X) / sp.Width); int bin = (int)(f * 4096 / sf.SR); if (bin < 1 || bin >= db.Length) continue; float y = sp.Y + (float)(-db[bin] / 80.0 * sp.Height); pts.Add(new PointF(x, Math.Max(sp.Y, Math.Min(sp.Bottom, y)))); }
          pts.Add(new PointF(sp.Right, sp.Bottom)); if (pts.Count > 3) using (var b = new LinearGradientBrush(sp, Color.FromArgb(200, Lime), Color.FromArgb(120, Gold), 0f)) g.FillPolygon(b, pts.ToArray());
          foreach (var hz in new[] { 30, 65, 260, 1000, 5000 }) { float x = sp.X + (float)(Math.Log(hz / 20.0) / Math.Log(1000) * sp.Width); Txt(g, hz >= 1000 ? (hz / 1000) + "k" : hz.ToString(), 9.5f, Dim, new RectangleF(x - 18, sp.Bottom - 14, 36, 12), StringAlignment.Center, false); } }
        Meter(g, new RectangleF(ms.Right - 50, ms.Y + 28, 14, ms.Height - 60), sf.OutPk, "OUT");
        // DISTORTION and COMPRESSOR
        var dist = new RectangleF(ms.X, ms.Bottom + 10, (ms.Width - 10) / 2, 180); Sec(g, dist, "DISTORTION", "d_on", Orange);
        Choice(g, new RectangleF(dist.X + 14, dist.Y + 34, 90, 24), "d_mode", Orange); Knob(g, dist.X + 160, dist.Y + 60, 24, "d_drive", "DRIVE", Orange);
        Knob(g, dist.X + 40, dist.Y + 120, 17, "d_lc", "LOW CUT", Orange); Knob(g, dist.X + 110, dist.Y + 120, 17, "d_hc", "HIGH CUT", Orange); Knob(g, dist.X + 180, dist.Y + 128, 17, "d_mix", "MIX", Orange);
        var cmp = new RectangleF(dist.Right + 10, dist.Y, dist.Width, 180); Sec(g, cmp, "COMPRESSOR", "c_on", Violet);
        Knob(g, cmp.X + 45, cmp.Y + 62, 22, "c_thr", "THRESHOLD", Violet); Knob(g, cmp.X + 130, cmp.Y + 62, 22, "c_ratio", "RATIO", Violet); Meter(g, new RectangleF(cmp.Right - 26, cmp.Y + 34, 10, 120), sf.CompGr, "", true);
        Knob(g, cmp.X + 45, cmp.Y + 132, 17, "c_rel", "RELEASE", Violet); Knob(g, cmp.X + 110, cmp.Y + 132, 17, "c_gain", "GAIN", Violet); Knob(g, cmp.X + 175, cmp.Y + 132, 17, "c_mix", "MIX", Violet);
        // level and keyboard
        HSlider(g, new RectangleF(a.Right - 300, a.Bottom - 146, 290, 8), "level", "LEVEL", Cyan);
        var kb = new RectangleF(a.X, a.Bottom - 120, a.Width - 90, 112); Keyboard(g, kb);
        Button(g, new RectangleF(kb.Right + 10, kb.Y + 10, 70, 30), "OCT -", false, Gold, () => octave = Math.Max(0, octave - 1), "Keyboard an octave lower", 11);
        Button(g, new RectangleF(kb.Right + 10, kb.Y + 48, 70, 30), "OCT +", false, Gold, () => octave = Math.Min(6, octave + 1), "Keyboard an octave higher", 11);
        Txt(g, "C" + octave, 12, Ink, new RectangleF(kb.Right + 10, kb.Y + 82, 70, 20), StringAlignment.Center, true);
    }
    void Sec(Graphics g, RectangleF r, string title, string on, Color col) { bool o = fx.Get(on) >= 0.5f; Fill(g, o ? Color.FromArgb(40, 22, 74) : PanelC, r, 12); Stroke(g, Color.FromArgb(o ? 220 : 90, col), r, 12, o ? 1.6f : 1); Txt(g, title, 11, o ? Ink : Dim, new RectangleF(r.X + 12, r.Y + 6, r.Width - 80, 18), StringAlignment.Near, true); Toggle(g, new RectangleF(r.Right - 58, r.Y + 6, 50, 22), on, col); }
    void Keyboard(Graphics g, RectangleF r)
    {
        int whites = 21; float ww = r.Width / whites; int[] wn = { 0, 2, 4, 5, 7, 9, 11 };
        for (int i = 0; i < whites; i++)
        {
            int note = 12 * (octave + 1) + 12 * (i / 7) + wn[i % 7]; var kr = new RectangleF(r.X + i * ww, r.Y, ww - 2, r.Height); bool on = down.Contains(note);
            Fill(g, on ? Gold : Color.FromArgb(230, 228, 240), kr, 4); int nn = note; var h = Reg(kr, () => Play(nn), "Click to play"); h.Up = () => Stop(nn); h.DragAt = pt => { }; h.P = null;
            if (i % 7 == 0) Txt(g, "C" + (octave + i / 7), 9.5f, Color.FromArgb(80, 70, 110), new RectangleF(kr.X, kr.Bottom - 16, kr.Width, 14), StringAlignment.Center, true);
        }
        for (int i = 0; i < whites; i++)
        {
            int k = i % 7; if (k == 2 || k == 6) continue; int note = 12 * (octave + 1) + 12 * (i / 7) + wn[k] + 1; var kr = new RectangleF(r.X + i * ww + ww * 0.65f, r.Y, ww * 0.7f, r.Height * 0.6f); bool on = down.Contains(note);
            Fill(g, on ? Gold : Color.FromArgb(30, 22, 50), kr, 3); int nn = note; var h = Reg(kr, () => Play(nn), "Click to play"); h.Up = () => Stop(nn); h.DragAt = pt => { }; h.P = null;
        }
    }
    void Play(int n) { if (down.Add(n)) sf.NoteOn(n, 100); }
    void Stop(int n) { if (down.Remove(n)) sf.NoteOff(n); }
}
#endif
