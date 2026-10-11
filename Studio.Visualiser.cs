using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The MASTER VISUALISER: a page of its own (opened from Mastering) where the music you are mastering moves: a ring of light bars around the logo that dance to the spectrum, a thin ring that shows
// the sound before mastering, pulses and sparks on every beat, a mirrored spectrum along the bottom, level meters and the loudness numbers. Mastering also has a small strip along the bottom like
// the one in My Projects, with a button that opens this page.
partial class StudioForm
{
    readonly float[] visB = new float[96], visPre = new float[96], visPk = new float[96];
    double[] visRe = new double[2048], visIm = new double[2048], visHann; float visBeat, visBass, visHoldL, visHoldR, visIdle; readonly List<Spark> visSp = new List<Spark>(); readonly List<float[]> visRings = new List<float[]>(); readonly Random visRnd = new Random(77);

    static Color MixC(Color a, Color b, float t) { t = Math.Max(0, Math.Min(1, t)); return Color.FromArgb(a.A + (int)((b.A - a.A) * t), a.R + (int)((b.R - a.R) * t), a.G + (int)((b.G - a.G) * t), a.B + (int)((b.B - a.B) * t)); }
    // 96 bands from 30 Hz to 18 kHz (low to high), quick to rise and smooth to fall
    void VisBands(float[] tap, int pos, float[] outB, bool playing)
    {
        if (!playing) { for (int i = 0; i < outB.Length; i++) outB[i] *= 0.9f; return; }
        const int N = 2048; if (visHann == null) { visHann = new double[N]; for (int i = 0; i < N; i++) visHann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N); }
        for (int i = 0; i < N; i++) { visRe[i] = tap[(pos - N + i + 8192 * 2) & 8191] * visHann[i]; visIm[i] = 0; }
        Fft.Transform(visRe, visIm, false);
        for (int b = 0; b < outB.Length; b++)
        {
            double f0 = 30 * Math.Pow(600.0, b / (double)outB.Length), f1 = 30 * Math.Pow(600.0, (b + 1) / (double)outB.Length);
            int k0 = Math.Max(1, (int)(f0 * N / StudioEngine.SR)), k1 = Math.Max(k0 + 1, (int)(f1 * N / StudioEngine.SR)); double mx = 0, sum = 0; int c = 0;
            for (int k = k0; k < k1 && k < N / 2; k++) { double m = Math.Sqrt(visRe[k] * visRe[k] + visIm[k] * visIm[k]); if (m > mx) mx = m; sum += m; c++; }
            double v = c == 0 ? 0 : mx * 0.6 + (sum / c) * 0.4; double db = 20 * Math.Log10(v / (N / 4.0) + 1e-9); float nv = (float)Math.Max(0, Math.Min(1, (db + 78) / 60 + b / (double)outB.Length * 0.16));
            outB[b] = nv > outB[b] ? outB[b] + (nv - outB[b]) * 0.75f : outB[b] * 0.9f + nv * 0.1f;
        }
    }

    // ---------- the top bar of the page ----------
    void PaintVisBar(Graphics g)
    {
        float W = ClientSize.Width, y = 12, h = 46;
        using (var br = new SolidBrush(Color.FromArgb(245, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH); NeonRule(g, 0, ToolH, W);
        Btn(g, Rc(12, y, 48, h), "≡", 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 24);
        Txt(g, "VISUALISER", 18, Ink, Rc(76, y, 190, h), StringAlignment.Near, true); Btn(g, Rc(270, y + 4, 100, h - 8), "HOME", 2, () => GoHome(), 14); if (ShowBack) Btn(g, Rc(376, y + 4, 100, h - 8), "BACK", 0, () => GoBack(), 14);
        float rx = W - 14;
        Btn(g, Rc(rx - 108, y + 4, 108, h - 8), Win.FullLabel(this), 0, () => Win.ToggleFullscreen(this), 12); rx -= 114;
        var mb = Rc(rx - 130, y + 4, 130, h - 8); Btn(g, mb, "MASTERING", 1, () => tab = 1, 13); Hint2(mb, "Back to the Mastering page"); rx -= 136;
        var ab = Rc(rx - 150, y + 4, 150, h - 8); Btn(g, ab, eng.MasterBypass ? "HEAR: ORIGINAL" : "HEAR: MASTERED", eng.MasterBypass ? 0 : 4, () => { eng.MasterBypass = !eng.MasterBypass; ApplyMatch(); }, 12); Hint2(ab, "Switch between your original mix and the mastered sound while you watch"); rx -= 156;
        var pv = Rc(rx - 150, y + 4, 150, h - 8); Btn(g, pv, mpActive ? "STOP PREVIEW" : "PREVIEW", mpActive ? 4 : 2, () => MasterPreviewToggle(), 13); Hint2(pv, "Loop the loudest 20 seconds of your song with the mastering on"); rx -= 156;
        var pl = Rc(rx - 96, y + 4, 96, h - 8); Btn(g, pl, "PLAY", eng.Playing && !mpActive ? 4 : 1, () => { if (mpActive) MasterPreviewStop(); PlayToggle(); }, 14);
    }

    // ---------- the page ----------
    void PaintVisualiser(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, dt = hDt; bool playing = eng.Playing; lastInput = DateTime.Now; btnAnim = true; visIdle += dt;
        using (var b = new SolidBrush(Color.FromArgb(178, 3, 1, 10))) g.FillRectangle(b, 0, top, W, H - top);   // darkens the backdrop so the lights stand out
        VisBands(eng.MasterTap, eng.MasterTapPos, visB, playing); VisBands(eng.MasterTapPre, eng.MasterTapPos, visPre, playing);
        float bass = 0; for (int i = 0; i < 14; i++) bass += visB[i]; bass /= 14f; visBass += (bass - visBass) * 0.12f;
        bool beat = playing && bass > visBass * 1.22f + 0.035f && bass > 0.28f && visBeat < 0.45f; if (beat) visBeat = 1f; visBeat = Math.Max(0, visBeat - dt * 3.0f);
        for (int i = 0; i < visPk.Length; i++) visPk[i] = Math.Max(visB[i], visPk[i] - dt * 0.45f);
        float cx = W / 2, cy = top + (H - top - 120) * 0.5f; float R0 = Math.Max(48f, Math.Min((H - top - 250) / 2f / 2.3f, (W - 560) / 2f / 2.3f));
        // the glow behind everything, stronger on the beat
        using (var gp = new GraphicsPath()) { float gr = R0 * 3.1f; gp.AddEllipse(cx - gr, cy - gr, gr * 2, gr * 2); using (var pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb((int)(46 + 80 * visBeat), NeonViolet), SurroundColors = new[] { Color.FromArgb(0, NeonViolet) } }) g.FillEllipse(pg, cx - gr, cy - gr, gr * 2, gr * 2); }
        // rings that spread out on every beat, and sparks
        if (beat) { visRings.Add(new[] { R0 * 1.2f, 1f }); for (int k = 0; k < 26 && visSp.Count < 320; k++) { double an = visRnd.NextDouble() * Math.PI * 2; float sp = 110 + (float)visRnd.NextDouble() * 280, r = R0 * 1.3f; float life = 0.7f + (float)visRnd.NextDouble() * 0.7f; visSp.Add(new Spark { X = cx + (float)Math.Cos(an) * r, Y = cy + (float)Math.Sin(an) * r, VX = (float)Math.Cos(an) * sp, VY = (float)Math.Sin(an) * sp, Life = life, Max = life, R = 1.2f + (float)visRnd.NextDouble() * 1.8f, C = SparkCols[visRnd.Next(SparkCols.Length)] }); } }
        for (int i = visRings.Count - 1; i >= 0; i--) { var rg = visRings[i]; rg[0] += dt * R0 * 1.9f; rg[1] -= dt * 1.1f; if (rg[1] <= 0) { visRings.RemoveAt(i); continue; } using (var pen = new Pen(Color.FromArgb((int)(150 * rg[1]), NeonCyan), 2.4f)) g.DrawEllipse(pen, cx - rg[0], cy - rg[0], rg[0] * 2, rg[0] * 2); }
        // the bars: 96 of them in a ring (low sounds at the top, high at the bottom, the two halves mirror each other)
        int nb = 96; float r1 = R0 * 1.16f; using (var pen = new Pen(Color.White, 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < nb; i++)
                {
                    int idx = i < 48 ? i : 95 - i; float v = visB[Math.Min(95, idx * 2 + 1)]; if (!playing) v = Math.Max(v, 0.025f + 0.02f * (float)Math.Sin(visIdle * 1.6 + i * 0.35)); float len = R0 * 0.06f + v * R0 * 1.15f;
                    double an = (-90 + (i + 0.5) * 360.0 / nb) * Math.PI / 180; float ca = (float)Math.Cos(an), sa = (float)Math.Sin(an); float tf = idx / 47f; Color col = tf < 0.5f ? MixC(NeonCyan, NeonViolet, tf * 2) : MixC(NeonViolet, NeonPink, (tf - 0.5f) * 2);
                    if (pass == 0) { pen.Color = Color.FromArgb(46, col); pen.Width = 9f; } else { pen.Color = col; pen.Width = 3.6f; }
                    g.DrawLine(pen, cx + ca * r1, cy + sa * r1, cx + ca * (r1 + len), cy + sa * (r1 + len));
                }
        }
        // the peak dots
        for (int i = 0; i < nb; i += 2) { int idx = i < 48 ? i : 95 - i; float pv = visPk[Math.Min(95, idx * 2 + 1)]; if (pv < 0.06f) continue; double an = (-90 + (i + 0.5) * 360.0 / nb) * Math.PI / 180; float rr = r1 + R0 * 0.06f + pv * R0 * 1.15f + 7; using (var br = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(br, cx + (float)Math.Cos(an) * rr - 1.6f, cy + (float)Math.Sin(an) * rr - 1.6f, 3.2f, 3.2f); }
        // the sound before mastering as a thin ring inside the bars
        { var pts = new PointF[nb]; for (int i = 0; i < nb; i++) { int idx = i < 48 ? i : 95 - i; float v = visPre[Math.Min(95, idx * 2 + 1)]; double an = (-90 + i * 360.0 / nb) * Math.PI / 180; float rr = R0 * 1.06f + v * R0 * 0.2f; pts[i] = new PointF(cx + (float)Math.Cos(an) * rr, cy + (float)Math.Sin(an) * rr); } using (var pen = new Pen(Color.FromArgb(130, 215, 215, 240), 1.4f)) g.DrawClosedCurve(pen, pts); }
        // the logo in the middle, beating
        DrawLogo(g, cx, cy, R0 * (0.80f + 0.08f * visBeat), playing, false);
        // sparks
        for (int i = visSp.Count - 1; i >= 0; i--) { var s = visSp[i]; s.VX *= 0.985f; s.VY = s.VY * 0.985f + 60 * dt; s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt; if (s.Life <= 0) { visSp.RemoveAt(i); continue; } float k = s.Life / s.Max; using (var br = new SolidBrush(Color.FromArgb((int)(60 * k), s.C))) g.FillEllipse(br, s.X - s.R * 2.2f, s.Y - s.R * 2.2f, s.R * 4.4f, s.R * 4.4f); using (var br = new SolidBrush(Color.FromArgb((int)(230 * k), s.C))) g.FillEllipse(br, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2); }
        // the mirrored spectrum along the bottom
        { float baseY = H - 70, maxUp = Math.Min(130f, (H - top) * 0.2f), x0 = 70, bw = (W - 140) / 96f; using (var pen = new Pen(Color.FromArgb(120, NeonViolet), 1.4f)) g.DrawLine(pen, x0, baseY, W - 70, baseY);
          for (int i = 0; i < 96; i++) { float v = visB[i]; if (!playing) v = Math.Max(v, 0.02f + 0.015f * (float)Math.Sin(visIdle * 2 + i * 0.3)); float hh = 2 + v * maxUp; Color c = MixC(NeonCyan, NeonPink, i / 95f); var up = new RectangleF(x0 + i * bw + 1, baseY - hh, Math.Max(2f, bw - 2.4f), hh);
            using (var br = new LinearGradientBrush(new RectangleF(up.X, up.Y - 1, up.Width, up.Height + 2), Color.FromArgb(230, c), Color.FromArgb(40, c), 90f)) g.FillRectangle(br, up);
            using (var br = new SolidBrush(Color.FromArgb(46, c))) g.FillRectangle(br, up.X, baseY + 2, up.Width, Math.Min(22f, hh * 0.38f));
            float pk = 2 + visPk[i] * maxUp; using (var br = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.FillRectangle(br, up.X, baseY - pk - 3, up.Width, 2); } }
        // level meters on both sides
        { float mh = Math.Max(160f, H - top - 330f), my = cy - mh / 2; var ml = Rc(24, my, 24, mh); var mr = Rc(W - 48, my, 24, mh); float pl = Math.Max(1e-6f, eng.MasterPeakL), pr = Math.Max(1e-6f, eng.MasterPeakR);
          float fl = Clamp01(((float)(20 * Math.Log10(pl)) + 54f) / 54f), fr = Clamp01(((float)(20 * Math.Log10(pr)) + 54f) / 54f); visHoldL = Math.Max(fl, visHoldL - dt * 0.25f); visHoldR = Math.Max(fr, visHoldR - dt * 0.25f);
          LedMeter(g, ml, fl); LedMeter(g, mr, fr); using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 2f)) { float yl = ml.Bottom - visHoldL * ml.Height, yr = mr.Bottom - visHoldR * mr.Height; g.DrawLine(pen, ml.X - 3, yl, ml.Right + 3, yl); g.DrawLine(pen, mr.X - 3, yr, mr.Right + 3, yr); }
          Txt(g, "L", 12, DimC, Rc(ml.X - 4, ml.Bottom + 4, 32, 16), StringAlignment.Center, true); Txt(g, "R", 12, DimC, Rc(mr.X - 4, mr.Bottom + 4, 32, 16), StringAlignment.Center, true); }
        // the numbers
        { var lu = eng.Lufs; lu.UpdateIntegrated(); VisCard(g, Rc(70, top + 18, 230, 84), "MOMENTARY", Db(lu.Momentary), "LUFS", Ink); VisCard(g, Rc(70, top + 110, 230, 84), "INTEGRATED", Db(lu.Integrated), "LUFS", NeonCyan);
          VisCard(g, Rc(W - 300, top + 18, 230, 84), "TRUE PEAK", Db(lu.TruePeakDb), "dBTP", lu.TruePeakDb > -0.9 ? Bad : Good); VisCard(g, Rc(W - 300, top + 110, 230, 84), "GAIN REDUCTION", eng.MasterGr.ToString("0.0"), "dB", Warn); }
        // the title and what you are hearing
        Txt(g, projName.ToUpper(), 20, Ink, Rc(W / 2 - 300, top + 14, 600, 30), StringAlignment.Center, true); string st = eng.MasterBypass ? "ORIGINAL" : "MASTERED"; var pill = Rc(W / 2 - 70, top + 48, 140, 24); FillRR(g, eng.MasterBypass ? Color.FromArgb(200, 70, 60, 110) : Color.FromArgb(220, 30, 120, 90), pill, 10); Txt(g, st, 12, Color.White, pill, StringAlignment.Center, true);
        // the song position (click or drag to jump)
        { var sb = Rc(70, H - 40, W - 140, 12); FillRR(g, Color.FromArgb(200, 8, 4, 18), sb, 6); double tot = Math.Max(1, eng.EndFrame); float f = (float)Math.Max(0, Math.Min(1, dispPos / tot)); if (f > 0.002f) { using (var br = new LinearGradientBrush(new RectangleF(sb.X, sb.Y, Math.Max(2, sb.Width * f), sb.Height), NeonCyan, NeonPink, 0f)) { using (var pth = RR(Rc(sb.X, sb.Y, Math.Max(2, sb.Width * f), sb.Height), 6)) g.FillPath(br, pth); } }
          using (var br = new SolidBrush(Color.White)) g.FillEllipse(br, sb.X + sb.Width * f - 6, sb.Y - 1, 14, 14); StrokeRR(g, Color.FromArgb(140, NeonViolet), sb, 6, 1);
          Txt(g, Fmt(dispPos / (double)StudioEngine.SR), 12, DimC, Rc(sb.X, sb.Bottom + 3, 120, 16), StringAlignment.Near, false); Txt(g, Fmt(tot / StudioEngine.SR), 12, DimC, Rc(sb.Right - 120, sb.Bottom + 3, 120, 16), StringAlignment.Far, false);
          Action<float> seek = mx => { double ff = Math.Max(0, Math.Min(1, (mx - sb.X) / sb.Width)); eng.Seek((long)(ff * tot)); }; var rg = Reg(Rc(sb.X, sb.Y - 8, sb.Width, sb.Height + 16), () => seek(MousePos.X)); rg.OnDrag = pt => seek(pt.X); }
        if (!playing) Txt(g, "PRESS PLAY OR PREVIEW AND WATCH YOUR MUSIC MOVE", 13, Color.FromArgb(180, DimC), Rc(W / 2 - 360, cy + R0 * 2.45f - 6, 720, 22), StringAlignment.Center, true);
    }
    void VisCard(Graphics g, RectangleF r, string label, string value, string unit, Color vc)
    {
        FillRR(g, Color.FromArgb(150, 8, 4, 20), r, 12); Win.DrawNeonBox(g, r, false);
        Txt(g, label, 10, DimC, Rc(r.X, r.Y + 6, r.Width, 14), StringAlignment.Center, true); Txt(g, value, 30, vc, Rc(r.X, r.Y + 22, r.Width, 40), StringAlignment.Center, true); Txt(g, unit, 11, DimC, Rc(r.X, r.Y + 62, r.Width, 16), StringAlignment.Center, false);
    }

    // ---------- the strip along the bottom of Mastering (the same idea as the preview strip in My Projects) ----------
    void PaintMasterVizBar(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height; var ob = Rc(24, H - 44, W - 48, 34); bool playing = eng.Playing; VizUpdate(playing);
        FillRR(g, Color.Black, ob, 12); using (var pen = new Pen(Color.FromArgb(playing ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(ob, 12)) g.DrawPath(pen, p); }
        var pz = Rc(ob.X + 6, ob.Y + 4, 26, 26); FillRR(g, Hot(pz) ? Color.FromArgb(255, 70, 70, 170) : Color.FromArgb(255, 48, 44, 120), pz, 7);
        using (var pb = new SolidBrush(Color.White)) { if (playing) { g.FillRectangle(pb, pz.X + 8, pz.Y + 7, 4, 12); g.FillRectangle(pb, pz.X + 14, pz.Y + 7, 4, 12); } else g.FillPolygon(pb, new[] { new PointF(pz.X + 9, pz.Y + 6), new PointF(pz.X + 9, pz.Y + 20), new PointF(pz.X + 20, pz.Y + 13) }); }
        Reg(pz, () => { if (mpActive) MasterPreviewStop(); PlayToggle(); }); Hint2(pz, playing ? "Pause" : "Play your mix through the mastering");
        var vb = Rc(ob.Right - 132, ob.Y + 3, 126, 28); Btn(g, vb, "VISUALISER", 2, () => tab = 5, 12); Hint2(vb, "Open the Master Visualiser: a full page where your music moves");
        double dur = Math.Max(1, eng.EndFrame), pos = dispPos; var inner = Rc(ob.X + 40, ob.Y + 3, ob.Width - 40 - 132 - 96, ob.Height - 6); float cx = inner.X + inner.Width / 2, cy = ob.Y + ob.Height / 2; float px = inner.X + (float)(Math.Min(1.0, pos / dur) * inner.Width);
        using (var b = new SolidBrush(Color.FromArgb(60, 0, 110, 130))) g.FillRectangle(b, inner.X, inner.Y, Math.Max(0, px - inner.X), inner.Height); using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 1.6f)) g.DrawLine(pen, px, inner.Y, px, inner.Bottom);
        float lh = ob.Height / 2 - 2, lhw = lh * 1.16f; cx = Math.Max(inner.X + lhw + 4, Math.Min(inner.Right - lhw - 4, px));
        if (playing)
        {
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); float maxH = ob.Height / 2 - 4; int hl = vizHist.Length;
            for (int c = 0; c < 190; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxH * (1f - c / 260f) * (0.75f + 0.25f * vizBand[(c * 3) % vizBand.Length])); int al = (int)(235 * (1f - c / 200f)); if (al <= 4) break;
                float xl = cx - lhw - 6 - c * 2.2f, xr = cx + lhw + 6 + c * 2.2f;
                using (var pl = new Pen(Color.FromArgb(al, NeonCyan), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp); using (var pr = new Pen(Color.FromArgb(al, NeonPink), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
            }
            g.Restore(st);
        }
        DrawLogo(g, cx, cy, lh, playing, Hot(Rc(cx - lhw, cy - lh, lhw * 2, lh * 2)));
        Txt(g, Fmt(pos / StudioEngine.SR), 12, Ink, Rc(ob.Right - 132 - 92, ob.Y, 88, ob.Height), StringAlignment.Far, true);
        Action<float> seek = mx => { double f = Math.Max(0, Math.Min(1, (mx - inner.X) / inner.Width)); eng.Seek((long)(f * dur)); }; var rg = Reg(inner, () => seek(MousePos.X)); rg.OnDrag = pt => seek(pt.X); if (Hot(inner)) { bubble = "Click to jump to a place in the song"; bubblePt = MousePos; }
    }
}
