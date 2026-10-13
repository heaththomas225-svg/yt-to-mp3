using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Purphex Music: Detect Track key. It listens to the selected track (then the others, longest first) until it finds a clear key,
// and tells you in a neon box in the middle of the screen (click it, press Esc, or wait: it goes away by itself).
partial class StudioForm
{
    bool keyOn, keyFound; string keyTitle = "", keySub = "", keyNote = ""; int keyAt;

    void ShowKeyBox(bool found, string title, string sub, string note) { keyOn = true; keyFound = found; keyTitle = title; keySub = sub; keyNote = note; keyAt = Environment.TickCount; lastInput = DateTime.Now; }

    // asks which track, then listens to it
    // asks which track: a sheet with a close button (x) in the corner, so you never have to click somewhere random to get out of it
    bool pickOpen; string pickTitle = ""; List<Track> pickList = new List<Track>(); Action<Track> pickAct; float pickScroll;
    void PickTrackMenu(string title, Func<Track, bool> ok, Action<Track> pick)
    {
        // one button for each ROW of the timeline: the pieces of a split track (and its layers) are not separate tracks, so they are not listed one by one; the piece that is used is the longest one of the row
        var list = new List<Track>(); var best = new Dictionary<Track, Track>();
        foreach (var t0 in eng.Tracks.ToArray())
        {
            if (!ok(t0)) continue; var h = t0.Host ?? t0; Track cur; long len = t0.Clip != null ? t0.Clip.Length : 0;
            if (!best.TryGetValue(h, out cur)) { best[h] = t0; list.Add(h); } else if (len > (cur.Clip != null ? cur.Clip.Length : 0)) best[h] = t0;
        }
        { var pick0 = pick; pick = row => { Track piece; pick0(best.TryGetValue(row, out piece) ? piece : row); }; }
        if (list.Count == 0) { Toast("There is no track with audio yet. Add a song or a recording first."); return; }
        pickTitle = title; pickList = list; pickAct = pick; pickScroll = 0; pickOpen = true; stylesOpen = false; autoOpen = false; gainOpen = false; browserOpen = false;
    }
    void PaintPickSheet(Graphics g)
    {
        if (!pickOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => pickOpen = false);
        const float rowH = 60, head = 76; float sw = Math.Min(620, W - 24), sh = Math.Min(H - 40, head + 16 + pickList.Count * rowH); var sheet = PopMove("pick", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 56, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 20); Glow(g, NeonViolet, sheet, 20, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 20, 1.5f); Reg(sheet, () => { });
        float tpx = 20; { float tw = g.MeasureString(pickTitle, GfxCache.Font(20, true)).Width; if (tw > sheet.Width - 90) tpx = Math.Max(12, 20 * (sheet.Width - 90) / tw); }
        Txt(g, pickTitle, tpx, Ink, Rc(sheet.X + 22, sheet.Y + 16, sheet.Width - 80, 32), StringAlignment.Near, true);
        Txt(g, "Click a track to choose it. The x in the corner (or Esc) closes this.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 44, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 50, sheet.Y + 14, 34, 34); FillRR(g, Hot(close) ? TM.C(200, 150, 50, 110) : TM.C(120, 70, 30, 90), close, 17); Txt(g, "X", 17, Ink, close, StringAlignment.Center, true); Reg(close, () => pickOpen = false);
        float top = sheet.Y + head, areaH = sheet.Bottom - top - 10; var area = Rc(sheet.X, top, sheet.Width, areaH); float total = pickList.Count * rowH; pickScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), pickScroll)); float vsw = total > areaH ? 54 : 0;
        regs.Add(new HitReg { R = area, OnWheel = d => pickScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), pickScroll - d * 50)) });
        g.SetClip(area); var sel = Selected;
        for (int i = 0; i < pickList.Count; i++)
        {
            var t = pickList[i]; var row = Rc(sheet.X + 12, top + i * rowH - pickScroll, sheet.Width - 24 - vsw, rowH - 6); if (row.Bottom < top || row.Y > area.Bottom) continue;
            bool hot = Hot(RectangleF.Intersect(row, area)) && area.Contains(MousePos); FillRR(g, hot ? TM.C(200, 70, 40, 130) : TM.C(150, 26, 15, 52), row, 12); StrokeRR(g, Color.FromArgb(hot ? 180 : 80, NeonViolet), row, 12, 1);
            double sec = t.UsedLength / (double)StudioEngine.SR; string sub = ((int)(sec / 60)) + ":" + ((int)(sec % 60)).ToString("00") + (t.Role == "vocal" ? "   vocal" : (t.Role == "beat" ? "   beat" : (t.Role == "song" ? "   full song" : ""))) + (t == sel ? "   (selected)" : "");
            float npx = 16; { float nw = g.MeasureString(t.Name, GfxCache.Font(16, true)).Width; if (nw > row.Width - 40) npx = Math.Max(11, 16 * (row.Width - 40) / nw); }
            Txt(g, t.Name, npx, Ink, Rc(row.X + 18, row.Y + 7, row.Width - 30, 24), StringAlignment.Near, true); Txt(g, sub, 12, DimC, Rc(row.X + 18, row.Y + 32, row.Width - 30, 18), StringAlignment.Near, false);
            var tt = t; var act = pickAct; Reg(RectangleF.Intersect(row, area), () => { pickOpen = false; act(tt); });
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 54, top, 42, areaH), total, areaH, () => pickScroll, v => pickScroll = v);
    }
    void DetectTrackKey() { PickTrackMenu("Detect the key of which track?", t => t.Clip != null && !t.Instrument && !t.IsBus, DetectTrackKeyOf); }
    void DetectTrackKeyOf(Track chosen)
    {
        var cands = new List<Track> { chosen };
        if (cands.Count == 0) return;
        int key = 0, scale = 1; double conf = 0; bool found = false; Track used = null;
        RunBusy("Finding the key of " + cands[0].Name + "...", false, p =>
        {
            foreach (var t in cands) { int k, s; double c; bool ok; try { ok = KeyDetect.Detect(t.Clip, out k, out s, out c); } catch { ok = false; k = 0; s = 1; c = 0; } if (ok) { found = true; key = k; scale = s; conf = c; used = t; break; } }
        }, ok =>
        {
            if (!ok) return;
            if (!found) { ShowKeyBox(false, "No clear key", "I could not hear a clear key in " + cands[0].Name + ". It works best on notes with a clear pitch: a voice, a melody, a bass line.", "Try a different track (the vocal or the melody)."); return; }
            string name = PitchCorrector.Keys[key] + (scale == 2 ? " minor" : " major"), rel = scale == 2 ? PitchCorrector.Keys[(key + 3) % 12] + " major" : PitchCorrector.Keys[(key + 9) % 12] + " minor";
            ShowKeyBox(true, name, "Relative " + (scale == 2 ? "major" : "minor") + ": " + rel + "     Confidence " + (int)Math.Round(Math.Max(0, Math.Min(1, conf)) * 100) + "%", "Listened to: " + used.Name);
        });
    }

    // the big number on screen during the Count In: the Purphex hexagon (the size of the logo) with a neon glow that pulses on every beat and neon sparks that burst off its edges
    static System.Drawing.Imaging.ImageAttributes countLogoAttr;
    int cinBeat; double cinBeatAt, cinLast, cinAcc; readonly List<Spark> cinSparks = new List<Spark>(); readonly Random cinRnd = new Random(5);
    void PaintCountLogo(Graphics g, float cx, float cy, GraphicsPath hex)
    {
        var lg = AppLogo.Logo; if (lg == null) return;
        if (countLogoAttr == null) { countLogoAttr = new System.Drawing.Imaging.ImageAttributes(); countLogoAttr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.8f }); }
        float side = 300f, aspect = lg.Width / (float)Math.Max(1, lg.Height), w = aspect >= 1 ? side : side * aspect, h = aspect >= 1 ? side / aspect : side;
        var dst = Rectangle.Round(new RectangleF(cx - w / 2, cy - h / 2, w, h)); var im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var gsv = g.Save(); g.SetClip(hex, CombineMode.Intersect); g.DrawImage(lg, dst, 0, 0, lg.Width, lg.Height, GraphicsUnit.Pixel, countLogoAttr); g.Restore(gsv); g.InterpolationMode = im;
    }
    void PaintCountIn(Graphics g)
    {
        int beat = eng.CountBeat; if (beat <= 0 && cinSparks.Count == 0) { cinBeat = 0; return; }
        float W = ClientSize.Width, H = ClientSize.Height, cx = W / 2, cy = H / 2, hh = 104, hw = hh * 1.16f; double now = needleClock.Elapsed.TotalSeconds, dt = Math.Min(0.05, Math.Max(0, now - cinLast)); cinLast = now; lastInput = DateTime.Now;
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; var verts = new[] { new PointF(cx - hw, cy), new PointF(cx - hw / 2, cy - hh), new PointF(cx + hw / 2, cy - hh), new PointF(cx + hw, cy), new PointF(cx + hw / 2, cy + hh), new PointF(cx - hw / 2, cy + hh) };
        if (beat > 0 && beat != cinBeat) { cinBeat = beat; cinBeatAt = now; for (int i = 0; i < 34; i++) CinSpark(verts, cx, cy); }   // a burst of sparks on every beat
        double pulse = beat > 0 ? Math.Exp(-(now - cinBeatAt) * 4.2) : 0;
        if (beat > 0)
        {
            using (var hex = HexPath(cx, cy, hw, hh))
            {
                using (var b = new SolidBrush(TM.C(215, 6, 3, 16))) g.FillPath(b, hex);
                PaintCountLogo(g, cx, cy, hex);
                for (int i = 6; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)Math.Min(255, (16 + 70 * pulse) / i * 2.2 + 6), NeonPink), i * 6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, hex);
                for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)Math.Min(255, (30 + 60 * pulse) / i), NeonCyan), 2 + i * 3f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, hex);
                var rb = Rc(cx - hw, cy - hh, hw * 2, hh * 2); using (var gb = new LinearGradientBrush(rb, TM.C(255, 0, 229, 255), TM.C(255, 255, 43, 214), 25f)) using (var pen = new Pen(gb, 3.4f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, hex);
            }
            DrawBigNumber(g, beat.ToString(), cx, cy, 150, beat == 1 ? NeonPink : NeonCyan);   // exactly in the middle of the hexagon (measured from the real shape of the digit)
            Txt(g, "COUNT IN", 15, Ink, Rc(cx - 150, cy + hh + 12, 300, 26), StringAlignment.Center, true);
        }
        // the sparks
        if (beat > 0) { cinAcc += 46 * dt; while (cinAcc >= 1 && cinSparks.Count < 160) { cinAcc -= 1; CinSpark(verts, cx, cy); } }
        for (int i = cinSparks.Count - 1; i >= 0; i--)
        {
            var sp = cinSparks[i]; sp.VY += 300f * (float)dt; sp.X += sp.VX * (float)dt; sp.Y += sp.VY * (float)dt; sp.Life -= (float)dt;
            if (sp.Life <= 0 || sp.Y > H + 40) { cinSparks.RemoveAt(i); continue; }
            float a = Math.Max(0, Math.Min(1, sp.Life / sp.Max)); float rad = sp.R * (0.55f + 0.45f * a);
            using (var b = new SolidBrush(Color.FromArgb((int)(70 * a), sp.C))) g.FillEllipse(b, sp.X - rad * 2.6f, sp.Y - rad * 2.6f, rad * 5.2f, rad * 5.2f);
            using (var b = new SolidBrush(Color.FromArgb((int)(235 * a), sp.C))) g.FillEllipse(b, sp.X - rad, sp.Y - rad, rad * 2, rad * 2);
            using (var b = new SolidBrush(Color.FromArgb((int)(255 * a), 255, 255, 255))) g.FillEllipse(b, sp.X - rad * 0.42f, sp.Y - rad * 0.42f, rad * 0.84f, rad * 0.84f);
        }
        g.SmoothingMode = sm;
    }
    // a big number drawn as clean neon strokes (the font's own 4 was lumpy at this size): every digit is a few points joined by a smooth line, with a soft glow under it
    static readonly float[][] DigitPts = {
        /*0*/ new float[0], /*1*/ new[] { 0.28f, 0.28f, 0.58f, 0.05f, 0.58f, 0.95f },
        /*2*/ new[] { 0.18f, 0.28f, 0.26f, 0.12f, 0.48f, 0.05f, 0.7f, 0.12f, 0.8f, 0.28f, 0.72f, 0.46f, 0.18f, 0.95f, 0.86f, 0.95f },
        /*3*/ new[] { 0.18f, 0.14f, 0.5f, 0.05f, 0.8f, 0.16f, 0.78f, 0.36f, 0.48f, 0.49f, 0.8f, 0.62f, 0.82f, 0.84f, 0.5f, 0.95f, 0.16f, 0.86f },
        /*4*/ new[] { 0.72f, 0.95f, 0.72f, 0.05f, 0.12f, 0.68f, 0.92f, 0.68f },
        /*5*/ new[] { 0.8f, 0.05f, 0.26f, 0.05f, 0.2f, 0.47f, 0.5f, 0.41f, 0.78f, 0.52f, 0.83f, 0.76f, 0.5f, 0.95f, 0.16f, 0.88f },
        /*6*/ new[] { 0.78f, 0.1f, 0.5f, 0.05f, 0.24f, 0.26f, 0.18f, 0.7f, 0.38f, 0.95f, 0.66f, 0.95f, 0.82f, 0.73f, 0.7f, 0.5f, 0.44f, 0.46f, 0.2f, 0.62f },
        /*7*/ new[] { 0.14f, 0.05f, 0.86f, 0.05f, 0.4f, 0.95f },
        /*8*/ new float[0],
        /*9*/ new[] { 0.22f, 0.9f, 0.5f, 0.95f, 0.76f, 0.74f, 0.82f, 0.3f, 0.62f, 0.05f, 0.34f, 0.05f, 0.18f, 0.27f, 0.3f, 0.5f, 0.56f, 0.54f, 0.8f, 0.38f } };
    static readonly bool[] DigitSmooth = { false, false, true, true, false, true, true, false, false, true };
    void DrawBigNumber(Graphics g, string t, float cx, float cy, float em, Color c)
    {
        float H = em * 0.80f, W = H * 0.62f, gap = H * 0.16f, total = t.Length * W + (t.Length - 1) * gap, x0 = cx - total / 2, y0 = cy - H / 2; float lw = Math.Max(4f, H * 0.115f);
        for (int pass = 3; pass >= 0; pass--)
        {
            float w = pass == 0 ? lw : lw + pass * 7f; int al = pass == 0 ? 255 : 34 / pass + 8; using (var pen = new Pen(Color.FromArgb(al, pass == 0 ? Color.FromArgb(Math.Min(255, c.R + 40), Math.Min(255, c.G + 40), Math.Min(255, c.B + 40)) : c), w) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int k = 0; k < t.Length; k++)
                {
                    int d = t[k] - '0'; if (d < 0 || d > 9) continue; float ox = x0 + k * (W + gap);
                    if (d == 0) { g.DrawEllipse(pen, ox + W * 0.08f, y0 + H * 0.05f, W * 0.84f, H * 0.9f); continue; }
                    if (d == 8) { g.DrawEllipse(pen, ox + W * 0.18f, y0 + H * 0.05f, W * 0.64f, H * 0.43f); g.DrawEllipse(pen, ox + W * 0.1f, y0 + H * 0.46f, W * 0.8f, H * 0.49f); continue; }
                    var p = DigitPts[d]; var pts = new PointF[p.Length / 2]; for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(ox + p[i * 2] * W, y0 + p[i * 2 + 1] * H);
                    if (DigitSmooth[d]) g.DrawCurve(pen, pts, 0.45f); else g.DrawLines(pen, pts);
                }
        }
    }
    // one spark: it starts on the edge of the hexagon and flies straight out from it
    void CinSpark(PointF[] v, float cx, float cy)
    {
        int e = cinRnd.Next(6); var p0 = v[e]; var p1 = v[(e + 1) % 6]; float t = (float)cinRnd.NextDouble(); float x = p0.X + (p1.X - p0.X) * t, y = p0.Y + (p1.Y - p0.Y) * t; float dx = x - cx, dy = y - cy, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 1) return; dx /= len; dy /= len;
        float sp = 70 + (float)cinRnd.NextDouble() * 190;
        cinSparks.Add(new Spark { X = x, Y = y, VX = dx * sp + (float)(cinRnd.NextDouble() - 0.5) * 30, VY = dy * sp - 40 - (float)cinRnd.NextDouble() * 60, Max = 0.6f + (float)cinRnd.NextDouble() * 0.7f, R = 2.0f + (float)cinRnd.NextDouble() * 1.8f, C = SparkCols[cinRnd.Next(SparkCols.Length)], Life = 0 });
        cinSparks[cinSparks.Count - 1].Life = cinSparks[cinSparks.Count - 1].Max;
    }

    void PaintKeyBox(Graphics g)
    {
        if (!keyOn) return; int age = unchecked(Environment.TickCount - keyAt); if (age > 12000) { keyOn = false; return; } lastInput = DateTime.Now;
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => keyOn = false);
        float k = Math.Min(1f, age / 260f), ease = 1 - (1 - k) * (1 - k); float bw = Math.Min(560, W - 40), bh = 230, grow = 0.92f + 0.08f * ease;
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2 - 20, bw * grow, bh * grow); double pulse = 0.5 + 0.5 * Math.Sin(age / 260.0); Color c1 = keyFound ? NeonCyan : Color.FromArgb(255, 150, 110), c2 = keyFound ? NeonPink : Color.FromArgb(255, 90, 130);
        for (int i = 6; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((20 + 22 * pulse) / i * 2.2f) + 6, c1), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 24 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 24)) { using (var br = new LinearGradientBrush(box, TM.C(250, 24, 10, 56), TM.C(250, 8, 3, 22), 90f)) g.FillPath(br, p); using (var pen = new Pen(c1, 2.4f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(120, c2), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -10, -10), 18)) g.DrawPath(pen, p);
        Reg(box, () => keyOn = false);
        Txt(g, keyFound ? "THE KEY OF YOUR PROJECT" : "KEY DETECTOR", 12, DimC, Rc(box.X, box.Y + 22, box.Width, 20), StringAlignment.Center, true);
        Txt(g, keyTitle, keyFound ? 54 : 34, c1, Rc(box.X + 14, box.Y + 48, box.Width - 28, keyFound ? 78 : 50), StringAlignment.Center, true);
        Txt(g, keySub, 14, Ink, Rc(box.X + 24, box.Y + (keyFound ? 130 : 100), box.Width - 48, keyFound ? 24 : 60), StringAlignment.Center, false);
        if (keyNote.Length > 0) Txt(g, keyNote, 12, DimC, Rc(box.X + 24, box.Y + (keyFound ? 158 : 150), box.Width - 48, 20), StringAlignment.Center, false);
        Txt(g, "CLICK ANYWHERE TO CLOSE", 11, TM.C(120, 150, 140, 190), Rc(box.X, box.Bottom - 30, box.Width, 18), StringAlignment.Center, false);
    }
}
