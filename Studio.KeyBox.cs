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
        var list = new List<Track>(); foreach (var t0 in eng.Tracks.ToArray()) if (ok(t0)) list.Add(t0);
        if (list.Count == 0) { Toast("There is no track with audio yet. Add a song or a recording first."); return; }
        pickTitle = title; pickList = list; pickAct = pick; pickScroll = 0; pickOpen = true; stylesOpen = false; autoOpen = false; gainOpen = false; browserOpen = false;
    }
    void PaintPickSheet(Graphics g)
    {
        if (!pickOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => pickOpen = false);
        const float rowH = 60, head = 76; float sw = Math.Min(620, W - 24), sh = Math.Min(H - 40, head + 16 + pickList.Count * rowH); var sheet = Rc((W - sw) / 2, (H - sh) / 2, sw, sh);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 20); Glow(g, NeonViolet, sheet, 20, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 20, 1.5f); Reg(sheet, () => { });
        float tpx = 20; { float tw = g.MeasureString(pickTitle, GfxCache.Font(20, true)).Width; if (tw > sheet.Width - 90) tpx = Math.Max(12, 20 * (sheet.Width - 90) / tw); }
        Txt(g, pickTitle, tpx, Ink, Rc(sheet.X + 22, sheet.Y + 16, sheet.Width - 80, 32), StringAlignment.Near, true);
        Txt(g, "Click a track to choose it. The x in the corner (or Esc) closes this.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 44, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 50, sheet.Y + 14, 34, 34); FillRR(g, Hot(close) ? Color.FromArgb(200, 150, 50, 110) : Color.FromArgb(120, 70, 30, 90), close, 17); Txt(g, "x", 17, Ink, close, StringAlignment.Center, true); Reg(close, () => pickOpen = false);
        float top = sheet.Y + head, areaH = sheet.Bottom - top - 10; var area = Rc(sheet.X, top, sheet.Width, areaH); float total = pickList.Count * rowH; pickScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), pickScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => pickScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), pickScroll - d * 50)) });
        g.SetClip(area); var sel = Selected;
        for (int i = 0; i < pickList.Count; i++)
        {
            var t = pickList[i]; var row = Rc(sheet.X + 12, top + i * rowH - pickScroll, sheet.Width - 24, rowH - 6); if (row.Bottom < top || row.Y > area.Bottom) continue;
            bool hot = Hot(RectangleF.Intersect(row, area)) && area.Contains(MousePos); FillRR(g, hot ? Color.FromArgb(200, 70, 40, 130) : Color.FromArgb(150, 26, 15, 52), row, 12); StrokeRR(g, Color.FromArgb(hot ? 180 : 80, NeonViolet), row, 12, 1);
            double sec = t.UsedLength / (double)StudioEngine.SR; string sub = ((int)(sec / 60)) + ":" + ((int)(sec % 60)).ToString("00") + (t.Role == "vocal" ? "   vocal" : (t.Role == "beat" ? "   beat" : (t.Role == "song" ? "   full song" : ""))) + (t == sel ? "   (selected)" : "");
            float npx = 16; { float nw = g.MeasureString(t.Name, GfxCache.Font(16, true)).Width; if (nw > row.Width - 40) npx = Math.Max(11, 16 * (row.Width - 40) / nw); }
            Txt(g, t.Name, npx, Ink, Rc(row.X + 18, row.Y + 7, row.Width - 30, 24), StringAlignment.Near, true); Txt(g, sub, 12, DimC, Rc(row.X + 18, row.Y + 32, row.Width - 30, 18), StringAlignment.Near, false);
            var tt = t; var act = pickAct; Reg(RectangleF.Intersect(row, area), () => { pickOpen = false; act(tt); });
        }
        g.ResetClip();
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

    // the big number on screen during the Count In
    // the Purphex logo, see-through, in the middle of the count-in circle (the number sits on top of it)
    static System.Drawing.Imaging.ImageAttributes countLogoAttr;
    void PaintCountLogo(Graphics g, RectangleF box)
    {
        var lg = AppLogo.Logo; if (lg == null) return;
        if (countLogoAttr == null) { countLogoAttr = new System.Drawing.Imaging.ImageAttributes(); countLogoAttr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.72f }); }
        float side = box.Width * 0.96f, aspect = lg.Width / (float)Math.Max(1, lg.Height), w = aspect >= 1 ? side : side * aspect, h = aspect >= 1 ? side / aspect : side;
        var dst = Rectangle.Round(new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2 - 8, w, h)); var im = g.InterpolationMode; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var gsv = g.Save(); using (var cp = new System.Drawing.Drawing2D.GraphicsPath()) { cp.AddEllipse(RectangleF.Inflate(box, -5, -5)); g.SetClip(cp, System.Drawing.Drawing2D.CombineMode.Intersect); }
        g.DrawImage(lg, dst, 0, 0, lg.Width, lg.Height, GraphicsUnit.Pixel, countLogoAttr); g.Restore(gsv); g.InterpolationMode = im;
    }
    void PaintCountIn(Graphics g)
    {
        int beat = eng.CountBeat; if (beat <= 0) return; float W = ClientSize.Width, H = ClientSize.Height; double pulse = 0.5 + 0.5 * Math.Sin(Environment.TickCount / 90.0); lastInput = DateTime.Now;
        var box = Rc(W / 2 - 150, H / 2 - 150, 300, 300); using (var b = new SolidBrush(Color.FromArgb(120, 3, 1, 10))) g.FillEllipse(b, box);
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((22 + 30 * pulse) / i * 2f) + 6, NeonPink), i * 5f)) g.DrawEllipse(pen, RectangleF.Inflate(box, i * 1.5f, i * 1.5f));
        using (var pen = new Pen(NeonCyan, 3f)) g.DrawEllipse(pen, box);
        PaintCountLogo(g, box);
        Txt(g, beat.ToString(), 150, beat == 1 ? NeonPink : NeonCyan, Rc(box.X, box.Y + 20, box.Width, 200), StringAlignment.Center, true); Txt(g, "COUNT IN", 14, Ink, Rc(box.X, box.Bottom - 70, box.Width, 24), StringAlignment.Center, true);
    }

    void PaintKeyBox(Graphics g)
    {
        if (!keyOn) return; int age = unchecked(Environment.TickCount - keyAt); if (age > 12000) { keyOn = false; return; } lastInput = DateTime.Now;
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(130, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => keyOn = false);
        float k = Math.Min(1f, age / 260f), ease = 1 - (1 - k) * (1 - k); float bw = Math.Min(560, W - 40), bh = 230, grow = 0.92f + 0.08f * ease;
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2 - 20, bw * grow, bh * grow); double pulse = 0.5 + 0.5 * Math.Sin(age / 260.0); Color c1 = keyFound ? NeonCyan : Color.FromArgb(255, 150, 110), c2 = keyFound ? NeonPink : Color.FromArgb(255, 90, 130);
        for (int i = 6; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((20 + 22 * pulse) / i * 2.2f) + 6, c1), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 24 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 24)) { using (var br = new LinearGradientBrush(box, Color.FromArgb(250, 24, 10, 56), Color.FromArgb(250, 8, 3, 22), 90f)) g.FillPath(br, p); using (var pen = new Pen(c1, 2.4f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(120, c2), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -10, -10), 18)) g.DrawPath(pen, p);
        Reg(box, () => keyOn = false);
        Txt(g, keyFound ? "THE KEY OF YOUR PROJECT" : "KEY DETECTOR", 12, DimC, Rc(box.X, box.Y + 22, box.Width, 20), StringAlignment.Center, true);
        Txt(g, keyTitle, keyFound ? 54 : 34, c1, Rc(box.X + 14, box.Y + 48, box.Width - 28, keyFound ? 78 : 50), StringAlignment.Center, true);
        Txt(g, keySub, 14, Ink, Rc(box.X + 24, box.Y + (keyFound ? 130 : 100), box.Width - 48, keyFound ? 24 : 60), StringAlignment.Center, false);
        if (keyNote.Length > 0) Txt(g, keyNote, 12, DimC, Rc(box.X + 24, box.Y + (keyFound ? 158 : 150), box.Width - 48, 20), StringAlignment.Center, false);
        Txt(g, "click anywhere to close", 11, Color.FromArgb(120, 150, 140, 190), Rc(box.X, box.Bottom - 30, box.Width, 18), StringAlignment.Center, false);
    }
}
