using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// Bass Lab pages: OSC (three wavetable oscillators, sub, noise, two filters) and MIX (per-source level / pan / mute / solo / routing + meters).
partial class SynthCanvas
{
    const float ModH = 330;   // height of the main modules

    // a switch drawn as a button (filled when on): used for mute / solo
    void Toggle(Graphics g, string id, RectangleF r, string label, Color on)
    {
        var p = P(id); if (p == null) return; bool isOn = p.Val > 0.5f; var reg = Add(r); reg.Tip = p.Name + (isOn ? ": ON (click to switch off)" : ": off (click to switch on)");
        Fill(g, isOn ? on : (Hot(r) ? PanelHi : Color.FromArgb(20, 11, 38)), r, 5); Stroke(g, isOn ? Color.White : Edge, r, 5, 1); Txt(g, label, 11, isOn ? Color.FromArgb(14, 6, 30) : Ink, r, StringAlignment.Center, true);
        reg.Down = pt => SetVal(p, isOn ? 0 : 1);
    }

    void Meter(Graphics g, RectangleF r, float v, bool vertical)
    {
        Fill(g, Color.FromArgb(10, 5, 22), r, 3); double db = v > 1e-5 ? 20 * Math.Log10(v) : -60; float f = (float)Math.Max(0, Math.Min(1, (db + 48) / 48.0));
        if (f < 0.01f) return; Color c = db > -1 ? Color.FromArgb(255, 90, 110) : (db > -9 ? Color.FromArgb(250, 210, 90) : Good);
        using (var b = new SolidBrush(c)) { if (vertical) g.FillRectangle(b, r.X + 1, r.Bottom - 1 - (r.Height - 2) * f, r.Width - 2, (r.Height - 2) * f); else g.FillRectangle(b, r.X + 1, r.Y + 1, (r.Width - 2) * f, r.Height - 2); }
    }

    void PaintOsc(Graphics g)
    {
        float y = 66, h = ModH; float[] ws = { 108, 198, 198, 198, 120, 192, 192 }; float x = 8;
        // SUB
        var sub = Rc(x, y, ws[0], h); ModuleFrame(g, sub, "SUB", "subon"); DrawSubShape(g, Rc(sub.X + 6, sub.Y + 30, sub.Width - 12, 84));
        Choose(g, "subwave", Rc(sub.X + 8, sub.Y + 120, sub.Width - 16, 20), Acc); NumBox(g, "suboct", Rc(sub.X + 8, sub.Y + 144, sub.Width - 16, 18), "OCT");
        Knob(g, "sublvl", sub.X + 30, sub.Y + 206, "LEVEL", Acc); Knob(g, "subpan", sub.X + 80, sub.Y + 206, "PAN", Acc, 13); x += ws[0] + 3;
        for (int o = 0; o < 3; o++)
        {
            string s = "abc"[o].ToString(); var r = Rc(x, y, ws[1 + o], h); ModuleFrame(g, r, "OSC " + s.ToUpper(), s + "on");
            DrawOscShape(g, Rc(r.X + 6, r.Y + 28, r.Width - 12, 86), s);
            Choose(g, s + "src", Rc(r.X + 6, r.Y + 118, 92, 20), Acc); var tbl = P(s + "tbl"); bool wtMode = P(s + "src").Val < 0.5f;
            Choose(g, s + "tbl", Rc(r.X + 102, r.Y + 118, 90, 20), wtMode ? Acc2 : Color.FromArgb(90, 80, 120));
            Choose(g, s + "warp", Rc(r.X + 6, r.Y + 142, 82, 20), Acc2); NumBox(g, s + "oct", Rc(r.X + 92, r.Y + 142, 48, 20), "OCT"); NumBox(g, s + "sem", Rc(r.X + 142, r.Y + 142, 50, 20), "SEM");
            float kx = r.X + 28, dx = 47, ky = r.Y + 198;
            Knob(g, s + "wt", kx, ky, wtMode ? "WAVE" : "(shape)", Acc, 15); Knob(g, s + "wamt", kx + dx, ky, "WARP", Acc2, 15); Knob(g, s + "uni", kx + dx * 2, ky, "UNISON", Acc, 15); Knob(g, s + "det", kx + dx * 3, ky, "DETUNE", Acc, 15);
            ky = r.Y + 252; Knob(g, s + "fin", kx, ky, "FINE", Acc, 13); Knob(g, s + "blend", kx + dx, ky, "BLEND", Acc, 13); Knob(g, s + "pan", kx + dx * 2, ky, "PAN", Acc, 13); Knob(g, s + "lvl", kx + dx * 3, ky, "LEVEL", Acc2, 13);
            string modName = "ABC"[(o + 1) % 3].ToString(); ky = r.Y + 303;
            Knob(g, s + "fm", kx - 4, ky, "FM " + modName, Acc2, 11); Knob(g, s + "rm", kx + 40, ky, "RING " + modName, Acc2, 11);
            var dr = Rc(r.X + 118, r.Y + 292, 74, 22); Fill(g, Hot(dr) ? PanelHi : Color.FromArgb(20, 11, 38), dr, 5); Stroke(g, Acc, dr, 5, 1); Txt(g, "DRAW WAVE", 10, Ink, dr, StringAlignment.Center, true);
            var drr = Add(dr); drr.Tip = "Draw your own waveform (or several frames to scan with WAVE)."; string ss = s; drr.Down = pt => OpenWaveEditor(ss);
            x += ws[1 + o] + 3;
        }
        var nz = Rc(x, y, ws[4], h); ModuleFrame(g, nz, "NOISE", "nzon"); DrawNoise(g, Rc(nz.X + 6, nz.Y + 30, nz.Width - 12, 84));
        Choose(g, "nztype", Rc(nz.X + 8, nz.Y + 120, nz.Width - 16, 20), Acc); Knob(g, "nzlvl", nz.X + 32, nz.Y + 206, "LEVEL", Acc); Knob(g, "nzpan", nz.X + 86, nz.Y + 206, "PAN", Acc, 13); x += ws[4] + 3;
        for (int k = 1; k <= 2; k++)
        {
            var r = Rc(x, y, ws[4 + k], h); ModuleFrame(g, r, "FILTER " + k, "f" + k + "on"); DrawFilter(g, Rc(r.X + 6, r.Y + 28, r.Width - 12, 90), k);
            Choose(g, "f" + k + "type", Rc(r.X + 6, r.Y + 122, 108, 20), Acc2); Choose(g, "f" + k + "slope", Rc(r.X + 118, r.Y + 122, 68, 20), Acc);
            if (k == 2) Choose(g, "froute", Rc(r.X + 6, r.Y + 146, 108, 20), Acc); NumBox(g, "f" + k + "key", Rc(r.X + 118, r.Y + 146, 68, 20), "KEY");
            Knob(g, "f" + k + "cut", r.X + 40, r.Y + 214, "CUTOFF", Acc2, 17); Knob(g, "f" + k + "res", r.X + 96, r.Y + 214, "RES", Acc, 17); Knob(g, "f" + k + "drive", r.X + 152, r.Y + 214, "DRIVE", Acc, 17);
            Knob(g, "f" + k + "gain", r.X + 68, r.Y + 286, "GAIN", Acc, 14); Txt(g, "gain: peak and shelf only", 9, Dim, Rc(r.X + 92, r.Y + 276, 100, 24), StringAlignment.Near, false);
            x += ws[4 + k] + 3;
        }
    }

    // the oscillator as it really sounds: the same wavetable lookup, warp and wave position the engine uses
    void DrawOscShape(Graphics g, RectangleF r, string s)
    {
        Fill(g, Disp, r, 5); var pts = new List<PointF>(); int N = 160; bool on = P(s + "on").Val > 0.5f;
        int src = (int)P(s + "src").Val, tbl = (int)P(s + "tbl").Val, warp = (int)P(s + "warp").Val; double wt = P(s + "wt").Val / 100.0, wamt = P(s + "wamt").Val / 100.0;
        for (int i = 0; i <= N; i++) { double p = Math.Min(0.9999, i / (double)N); double y = Math.Max(-1.2, Math.Min(1.2, SynthFx.OscSample(src, tbl, src == 0 ? wt : 0, warp, wamt, p))); pts.Add(new PointF(r.X + 4 + (r.Width - 8) * i / N, r.Y + r.Height / 2 - (float)y * (r.Height / 2 - 8))); }
        var c = on ? Color.FromArgb(120, 230, 150) : Color.FromArgb(90, 90, 110); var poly = new List<PointF>(pts); poly.Add(new PointF(r.Right - 4, r.Bottom - 2)); poly.Add(new PointF(r.X + 4, r.Bottom - 2));
        using (var b = new SolidBrush(Color.FromArgb(on ? 50 : 20, c))) g.FillPolygon(b, poly.ToArray());
        using (var pen = new Pen(c, 2f)) g.DrawLines(pen, pts.ToArray()); using (var pen = new Pen(Color.FromArgb(40, 150, 130, 200))) g.DrawLine(pen, r.X + 4, r.Y + r.Height / 2, r.Right - 4, r.Y + r.Height / 2);
        if (src == 0) { var t = Wavetables.Table(tbl); string nm = t.Name + "  (" + t.Frames + (t.Frames == 1 ? " frame)" : " frames)"); Txt(g, nm, 9, Dim, Rc(r.X + 6, r.Y + 2, r.Width - 12, 12), StringAlignment.Near, false); }
        else Txt(g, Wavetables.Sources[src], 9, Dim, Rc(r.X + 6, r.Y + 2, r.Width - 12, 12), StringAlignment.Near, false);
    }
    void DrawSubShape(Graphics g, RectangleF r)
    {
        Fill(g, Disp, r, 5); var pts = new List<PointF>(); int N = 120; int sw = (int)P("subwave").Val; bool on = P("subon").Val > 0.5f;
        for (int i = 0; i <= N; i++) { double p = i / (double)N; double y = sw == 0 ? Math.Sin(6.283185307179586 * p) : (sw == 1 ? 4 * Math.Abs(p - 0.5) - 1 : (sw == 2 ? (p < 0.5 ? 0.7 : -0.7) : 2 * p - 1)); pts.Add(new PointF(r.X + 4 + (r.Width - 8) * i / N, r.Y + r.Height / 2 - (float)y * (r.Height / 2 - 8))); }
        var c = on ? Color.FromArgb(120, 230, 150) : Color.FromArgb(90, 90, 110); using (var pen = new Pen(c, 2f)) g.DrawLines(pen, pts.ToArray());
    }
    void DrawNoise(Graphics g, RectangleF r)
    {
        Fill(g, Disp, r, 5); var rnd = new Random(7); var pts = new List<PointF>(); bool on = P("nzon").Val > 0.5f; int type = (int)P("nztype").Val; double v = 0, prev = 0;
        for (int i = 0; i < 70; i++)
        {
            double w = rnd.NextDouble() * 2 - 1, y = w;
            switch (type) { case 1: v = v * 0.7 + w * 0.5; y = v; break; case 2: v = (v + 0.25 * w) / 1.1; y = v * 2; break; case 3: y = (w - prev) * 0.6; prev = w; break; case 4: y = rnd.NextDouble() < 0.12 ? w : 0; break; }
            pts.Add(new PointF(r.X + 4 + (r.Width - 8) * i / 69f, r.Y + r.Height / 2 - (float)Math.Max(-1, Math.Min(1, y)) * (r.Height / 2 - 10)));
        }
        using (var pen = new Pen(on ? Color.FromArgb(120, 230, 150) : Color.FromArgb(90, 90, 110), 1.6f)) g.DrawLines(pen, pts.ToArray());
    }

    // magnitude (dB) of the filter at f Hz: the same filter families and slopes the engine runs
    static double FilterDb(int type, int stages, double fc, double res, double gainDb, double f)
    {
        double x = f / fc, k = 2 - 1.96 * res, kb = 1.41421; double G = Math.Pow(10, gainDb / 20);
        Func<double, double, double[]> H = (kk, xx) => { double re = 1 - xx * xx, im = kk * xx, d = re * re + im * im; return new[] { re / d, -im / d }; };   // 1 / (1 - x^2 + j k x)
        switch (type)
        {
            case 0: case 1: case 2: case 3:
                {
                    double mag = 1;
                    for (int s = 0; s < stages; s++)
                    {
                        double kk = s == 0 ? k : kb, re = 1 - x * x, im = kk * x, den = Math.Sqrt(re * re + im * im) + 1e-12;
                        mag *= type == 0 ? 1 / den : (type == 1 ? x * x / den : (type == 2 ? x / den : Math.Abs(re) / den));
                    }
                    return 20 * Math.Log10(mag + 1e-9);
                }
            case 4: { var h = H(k, x); double bpRe = -x * h[1], bpIm = x * h[0]; double re = 1 + (G - 1) * k * bpRe, im = (G - 1) * k * bpIm; return 20 * Math.Log10(Math.Sqrt(re * re + im * im) + 1e-9); }
            case 5: { var h = H(kb, x); double re = 1 + (G - 1) * h[0], im = (G - 1) * h[1]; return 20 * Math.Log10(Math.Sqrt(re * re + im * im) + 1e-9); }
            case 6: { var h = H(kb, x); double hpRe = -x * x * h[0], hpIm = -x * x * h[1]; double re = 1 + (G - 1) * hpRe, im = (G - 1) * hpIm; return 20 * Math.Log10(Math.Sqrt(re * re + im * im) + 1e-9); }
            case 7: case 8:
                {
                    double fb = 0.2 + 0.74 * res; if (type == 8) fb = -fb; double ph = 2 * Math.PI * x, re = 1 - fb * Math.Cos(ph), im = fb * Math.Sin(ph); return 20 * Math.Log10((1 - 0.5 * Math.Abs(fb)) / Math.Sqrt(re * re + im * im) + 1e-9);
                }
            default:
                {
                    double pos = Math.Max(0, Math.Min(4, Math.Log(fc / 20.0) / Math.Log(1000.0) * 4)); int v0 = (int)pos; if (v0 > 3) v0 = 3; double ft = pos - v0, sr = 0, si = 0;
                    double[][] F = { new[] { 800.0, 1150, 2900 }, new[] { 400.0, 1600, 2700 }, new[] { 270.0, 2300, 3000 }, new[] { 450.0, 800, 2830 }, new[] { 325.0, 700, 2530 } };
                    for (int q = 0; q < 3; q++) { double fr = F[v0][q] * (1 - ft) + F[v0 + 1][q] * ft, xx = f / fr, kk = 0.18 + 0.5 * (1 - res); var h = H(kk, xx); sr += (-xx * h[1]) * kk * (q == 0 ? 1.4 : (q == 1 ? 1 : 0.7)); si += (xx * h[0]) * kk * (q == 0 ? 1.4 : (q == 1 ? 1 : 0.7)); }
                    return 20 * Math.Log10(Math.Sqrt(sr * sr + si * si) + 1e-9);
                }
        }
    }
    void DrawFilter(Graphics g, RectangleF r, int k)
    {
        Fill(g, Disp, r, 5); bool on = P("f" + k + "on").Val > 0.5f; int type = (int)P("f" + k + "type").Val, st = (int)P("f" + k + "slope").Val + 1; double fc = P("f" + k + "cut").Val, res = P("f" + k + "res").Val / 100.0, gdb = P("f" + k + "gain").Val; var pts = new List<PointF>(); int N = 110;
        using (var pen = new Pen(Color.FromArgb(30, 150, 130, 200))) for (int q = 100; q <= 10000; q *= 10) { float gx = r.X + 4 + (r.Width - 8) * (float)(Math.Log(q / 20.0) / Math.Log(1000)); g.DrawLine(pen, gx, r.Y + 4, gx, r.Bottom - 4); }
        for (int i = 0; i <= N; i++)
        {
            double f = 20 * Math.Pow(1000, i / (double)N), db = Math.Max(-48, Math.Min(24, FilterDb(type, st, fc, res, gdb, f)));
            pts.Add(new PointF(r.X + 4 + (r.Width - 8) * i / (float)N, r.Bottom - 6 - (float)((db + 48) / 72.0) * (r.Height - 12)));
        }
        var c = on ? Color.FromArgb(236, 92, 226) : Color.FromArgb(90, 90, 110); var poly = new List<PointF>(pts); poly.Add(new PointF(r.Right - 4, r.Bottom - 2)); poly.Add(new PointF(r.X + 4, r.Bottom - 2));
        using (var b = new SolidBrush(Color.FromArgb(on ? 55 : 20, c))) g.FillPolygon(b, poly.ToArray()); using (var pen = new Pen(c, 2f)) g.DrawLines(pen, pts.ToArray());
        float cx = r.X + 4 + (r.Width - 8) * (float)(Math.Log(fc / 20.0) / Math.Log(1000)); using (var pen = new Pen(Color.FromArgb(120, Ink), 1f)) g.DrawLine(pen, cx, r.Y + 4, cx, r.Bottom - 4);
        var reg = Add(r); reg.Tip = "Drag left / right = cutoff, up / down = resonance"; reg.P = P("f" + k + "cut"); var pc = P("f" + k + "cut"); var pr = P("f" + k + "res"); RectangleF rr = r;
        reg.Down = pt => { }; reg.Drag = (dx, dy) => { double fr = Math.Max(0, Math.Min(1, (mouse.X - rr.X - 4) / (rr.Width - 8))); SetVal(pc, (float)Math.Max(20, Math.Min(20000, 20 * Math.Pow(1000, fr)))); SetVal(pr, (float)Math.Max(0, Math.Min(100, (rr.Bottom - mouse.Y) / rr.Height * 130 - 10))); };
    }

    // ---------- MIX page ----------
    void PaintMix(Graphics g)
    {
        var r = Rc(8, 66, W - 16, ModH); Fill(g, Panel, r, 8); Stroke(g, Edge, r, 8, 1);
        Txt(g, "MIXER: level, pan, mute, solo and where each sound goes (Filter 1, Filter 2, both, or straight to the output)", 12, Ink, Rc(r.X + 12, r.Y + 6, r.Width - 24, 20), StringAlignment.Near, true);
        string[] names = { "OSC A", "OSC B", "OSC C", "SUB", "NOISE" }, ons = { "aon", "bon", "con", "subon", "nzon" }, lv = { "alvl", "blvl", "clvl", "sublvl", "nzlvl" }, pn = { "apan", "bpan", "cpan", "subpan", "nzpan" }, mu = { "amute", "bmute", "cmute", "submute", "nzmute" }, so = { "asolo", "bsolo", "csolo", "subsolo", "nzsolo" }, ro = { "aroute", "broute", "croute", "subroute", "nzroute" };
        for (int i = 0; i < 5; i++)
        {
            float sx = r.X + 12 + i * 168; var st = Rc(sx, r.Y + 34, 160, r.Height - 44); Fill(g, Color.FromArgb(24, 13, 46), st, 8); Stroke(g, Edge, st, 8, 1);
            Power(g, ons[i], Rc(sx + 4, st.Y + 4, 24, 24)); Txt(g, names[i], 13, Ink, Rc(sx + 30, st.Y + 4, 100, 24), StringAlignment.Near, true);
            Knob(g, lv[i], sx + 54, st.Y + 90, "LEVEL", Acc2, 26); Meter(g, Rc(sx + 112, st.Y + 44, 14, 120), SrcPeak(i), true);
            Knob(g, pn[i], sx + 54, st.Y + 176, "PAN", Acc, 15);
            Toggle(g, mu[i], Rc(sx + 10, st.Y + 214, 70, 24), "MUTE", Color.FromArgb(255, 120, 130)); Toggle(g, so[i], Rc(sx + 84, st.Y + 214, 66, 24), "SOLO", Color.FromArgb(250, 210, 90));
            Choose(g, ro[i], Rc(sx + 10, st.Y + 246, 140, 24), Acc, "to: ");
        }
        var ms = Rc(r.X + 12 + 5 * 168 + 6, r.Y + 34, r.Width - (12 + 5 * 168 + 6) - 12, r.Height - 44); Fill(g, Color.FromArgb(24, 13, 46), ms, 8); Stroke(g, Acc, ms, 8, 1.4f);
        Txt(g, "MASTER", 13, Ink, Rc(ms.X + 10, ms.Y + 4, 100, 24), StringAlignment.Near, true);
        Knob(g, "vol", ms.X + 56, ms.Y + 92, "VOLUME", Acc, 28); Meter(g, Rc(ms.X + 120, ms.Y + 44, 14, 140), fx.OutPeakL, true); Meter(g, Rc(ms.X + 138, ms.Y + 44, 14, 140), fx.OutPeakR, true);
        Txt(g, "L", 9, Dim, Rc(ms.X + 120, ms.Y + 186, 14, 12), StringAlignment.Center, false); Txt(g, "R", 9, Dim, Rc(ms.X + 138, ms.Y + 186, 14, 12), StringAlignment.Center, false);
        Choose(g, "froute", Rc(ms.X + 10, ms.Y + 214, ms.Width - 20, 24), Acc, "Filters: ");
        Txt(g, "Serial: sources sent to Filter 1 run through both filters. Parallel: Filter 1 and Filter 2 side by side.", 10, Dim, Rc(ms.X + 10, ms.Y + 242, ms.Width - 20, 40), StringAlignment.Near, false);
    }
    float SrcPeak(int i) { return fx.SrcPeak[i]; }

    // ---------- the wave drawing window ----------
    void OpenWaveEditor(string s)
    {
        int slot = 0; var tbl = P(s + "tbl"); if (tbl.Val >= Wavetables.CustomBase && tbl.Val < Wavetables.CustomBase + 3) slot = (int)tbl.Val - Wavetables.CustomBase; else { SetVal(tbl, Wavetables.CustomBase); }
        SetVal(P(s + "src"), 0); var f = new WaveEditorForm(fx, slot, s); f.Show(FindForm()); Poke();
    }
}
