using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// The look of your profile: the banner and the picture (zoom, left and right, up and down, fit and fill) and the accent colour palette.
partial class StudioForm
{
    // ---------- pictures kept in their frame ----------
    readonly Dictionary<string, Bitmap> framedCache = new Dictionary<string, Bitmap>(); readonly Dictionary<string, Size> picSizes = new Dictionary<string, Size>();
    void ForgetFramed() { foreach (var v in framedCache.Values) if (v != null) v.Dispose(); framedCache.Clear(); foreach (var v in bannerCache.Values) if (v != null) v.Dispose(); bannerCache.Clear(); }
    // the picture drawn into a w x h frame with its zoom and position (kept until something changes)
    Bitmap FramedPic(string key, Func<Image> load, int w, int h, int zoom, int px, int py)
    {
        if (w < 2 || h < 2) return null; string k = key + "|" + w + "x" + h + "|" + zoom + "|" + px + "|" + py; Bitmap b; if (framedCache.TryGetValue(k, out b)) return b; Image src = null;
        try { src = load(); } catch { src = null; } if (src == null) { framedCache[k] = null; return null; }
        try { b = ProfileStore.RenderFramed(src, w, h, zoom, px, py, Color.FromArgb(255, 10, 5, 22)); } catch { b = null; } finally { src.Dispose(); }
        if (framedCache.Count > 24) { foreach (var v in framedCache.Values) if (v != null) v.Dispose(); framedCache.Clear(); } framedCache[k] = b; return b;
    }
    Size PicSize(string path)
    {
        string k = path + File.GetLastWriteTime(path).Ticks; Size s; if (picSizes.TryGetValue(k, out s)) return s;
        try { using (var im = LoadFileImage(path)) s = im.Size; } catch { s = new Size(1000, 250); } picSizes[k] = s; return s;
    }
    string AvatarFile { get { string p = ProjectLibrary.AvatarSrc; return File.Exists(p) ? p : ProjectLibrary.AvatarPath; } }

    // a picture in a rounded square with a neon ring (these pictures are never buttons)
    void DrawFrameImage(Graphics g, RectangleF r, Bitmap img, Color ring)
    {
        float rad = Math.Max(8, r.Width * 0.2f); var gs = g.Save(); using (var p = RR(r, rad)) { g.SetClip(p, CombineMode.Intersect); }
        if (img != null) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(img, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height))); } else DrawLogoArt(g, r);
        g.Restore(gs); using (var p = RR(r, rad)) using (var pen = new Pen(ring, 3f)) g.DrawPath(pen, p); using (var p = RR(RectangleF.Inflate(r, 3, 3), rad + 3)) using (var pen = new Pen(Color.FromArgb(70, ring), 8f)) g.DrawPath(pen, p);
    }

    // ---------- the controls ----------
    // one line: a name, a minus button, a slider and a plus button
    void AdjRow(Graphics g, float x, float y, float w, Color acc, string label, string minus, string plus, float frac, Action<float> set, Action stepMinus, Action stepPlus, Action commit, string hint)
    {
        Txt(g, label, 11, DimC, Rc(x, y, 104, 24), StringAlignment.Near, false);
        var m = Rc(x + 104, y, 24, 24); var p = Rc(x + w - 24, y, 24, 24); var sr = Rc(m.Right + 8, y, p.X - m.Right - 16, 24);
        Btn(g, m, minus, 0, () => { stepMinus(); commit(); }, 12); Btn(g, p, plus, 0, () => { stepPlus(); commit(); }, 12);
        var sl = Slider(g, sr, frac, set, acc); sl.OnUp = commit; Hint2(sr, hint); Hint2(m, hint); Hint2(p, hint);
    }
    // zoom, left and right, up and down, FIT and FILL for a picture; returns the next free y
    float AdjustPicture(Graphics g, float x, float y, float w, Color acc, int minZoom, Func<int> gz, Action<int> sz, Func<int> gx, Action<int> sx, Func<int> gy, Action<int> sy, Action commit, string what)
    {
        Func<int, int, int, int> cl = (v, lo, hi) => Math.Max(lo, Math.Min(hi, v));
        AdjRow(g, x, y, w, acc, "ZOOM " + what, "-", "+", (gz() - minZoom) / (float)Math.Max(1, 400 - minZoom), f => sz(cl((int)Math.Round(minZoom + f * (400 - minZoom)), minZoom, 400)), () => sz(cl(gz() - 10, minZoom, 400)), () => sz(cl(gz() + 10, minZoom, 400)), commit, "Zoom the " + what.ToLower() + " in or out"); y += 30;
        AdjRow(g, x, y, w, acc, "LEFT / RIGHT", "<", ">", gx() / 100f, f => sx(cl((int)Math.Round(f * 100), 0, 100)), () => sx(cl(gx() - 5, 0, 100)), () => sx(cl(gx() + 5, 0, 100)), commit, "Move the " + what.ToLower() + " left or right inside its frame"); y += 30;
        AdjRow(g, x, y, w, acc, "UP / DOWN", "^", "v", gy() / 100f, f => sy(cl((int)Math.Round(f * 100), 0, 100)), () => sy(cl(gy() - 5, 0, 100)), () => sy(cl(gy() + 5, 0, 100)), commit, "Move the " + what.ToLower() + " up or down inside its frame"); y += 34;
        float bw = (w - 10) / 2f; var fit = Rc(x, y, bw, 30); var fill = Rc(fit.Right + 10, y, bw, 30);
        Btn(g, fit, "FIT TO FRAME", gz() == minZoom && gx() == 50 && gy() == 50 ? 2 : 1, () => { sz(minZoom); sx(50); sy(50); commit(); }, 11); Hint2(fit, "Shrink the whole " + what.ToLower() + " so all of it fits inside the frame");
        Btn(g, fill, "FILL THE FRAME", gz() == 100 && gx() == 50 && gy() == 50 ? 2 : 1, () => { sz(100); sx(50); sy(50); commit(); }, 11); Hint2(fill, "Cover the frame completely (the edges may be cut off)");
        return y + 40;
    }

    // ---------- the accent colour palette ----------
    double palH = 270, palS = 0.82, palL = 0.60; int palSeen = int.MinValue; Bitmap palBmp; int palBw, palBh; double palBl = -1; Bitmap palBar; double palBarH = -1, palBarS = -1; int palBarH2;
    void PalSync()
    {
        int cur = ProfileStore.Accent; if (cur == palSeen) return; palSeen = cur; double h, s, l; ThemeDef.ToHsl(cur >= 0 ? ProfileStore.ColorOf(cur) : NeonViolet, out h, out s, out l); palH = h; palS = s; palL = l;
    }
    void PalApply() { ProfileStore.Accent = ProfileStore.Pack(ThemeDef.Hsl(palH, palS, palL)); palSeen = ProfileStore.Accent; }
    void PalCommit() { ProfileStore.Save(); QueueRepublish(); }
    // colour field: the hue along the top, and from strong colour (top) to grey (bottom) at the chosen lightness
    Bitmap PalField(int w, int h)
    {
        if (palBmp != null && palBw == w && palBh == h && Math.Abs(palBl - palL) < 0.0005) return palBmp; if (palBmp != null) palBmp.Dispose(); palBmp = new Bitmap(w, h, PixelFormat.Format32bppRgb); palBw = w; palBh = h; palBl = palL;
        var d = palBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try { var row = new int[w]; for (int y = 0; y < h; y++) { double s = 1 - y / (double)(h - 1); for (int x = 0; x < w; x++) row[x] = ThemeDef.Hsl(x / (double)(w - 1) * 360, s, palL).ToArgb(); System.Runtime.InteropServices.Marshal.Copy(row, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w); } }
        finally { palBmp.UnlockBits(d); } return palBmp;
    }
    // the lightness bar for the chosen hue and strength (light on top, dark at the bottom)
    Bitmap PalBar(int h)
    {
        if (palBar != null && palBarH2 == h && Math.Abs(palBarH - palH) < 0.01 && Math.Abs(palBarS - palS) < 0.001) return palBar; if (palBar != null) palBar.Dispose(); palBar = new Bitmap(1, h, PixelFormat.Format32bppRgb); palBarH2 = h; palBarH = palH; palBarS = palS;
        for (int y = 0; y < h; y++) palBar.SetPixel(0, y, ThemeDef.Hsl(palH, palS, 0.95 - 0.85 * (y / (double)(h - 1))));
        return palBar;
    }
    float PaintPalette(Graphics g, float x, float y, float w)
    {
        PalSync(); const int FH = 112; float fw = Math.Max(120, w - 112); var field = Rc(x, y, fw, FH); var bar = Rc(field.Right + 12, y, 24, FH); float ix = bar.Right + 12;
        using (var p = RR(field, 10)) { var gs = g.Save(); g.SetClip(p, CombineMode.Intersect); g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(PalField((int)fw, FH), field); g.Restore(gs); using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.2f)) g.DrawPath(pen, p); }
        using (var p = RR(bar, 8)) { var gs = g.Save(); g.SetClip(p, CombineMode.Intersect); g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(PalBar(FH), bar); g.Restore(gs); using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.2f)) g.DrawPath(pen, p); }
        // where the colour is
        float mx = field.X + (float)(palH / 360.0) * (field.Width - 1), my = field.Y + (float)((1 - palS)) * (field.Height - 1); using (var pen = new Pen(Color.Black, 3f)) g.DrawEllipse(pen, mx - 7, my - 7, 14, 14); using (var pen = new Pen(Color.White, 1.8f)) g.DrawEllipse(pen, mx - 7, my - 7, 14, 14);
        float by = bar.Y + (float)((0.95 - palL) / 0.85) * (bar.Height - 1); by = Math.Max(bar.Y, Math.Min(bar.Bottom, by)); using (var pen = new Pen(Color.Black, 4f)) g.DrawLine(pen, bar.X - 3, by, bar.Right + 3, by); using (var pen = new Pen(Color.White, 2f)) g.DrawLine(pen, bar.X - 3, by, bar.Right + 3, by);
        Action<PointF> pickField = pt => { palH = Math.Max(0, Math.Min(360, (pt.X - field.X) / (field.Width - 1) * 360)); palS = Math.Max(0, Math.Min(1, 1 - (pt.Y - field.Y) / (field.Height - 1))); PalApply(); };
        var rf = new HitReg { R = field, OnDown = () => pickField(MousePos), OnDrag = pt => pickField(pt), OnUp = PalCommit }; regs.Add(rf); Hint2(field, "Pick your colour: across is the colour, down is how strong it is");
        Action<PointF> pickBar = pt => { palL = Math.Max(0.10, Math.Min(0.95, 0.95 - (pt.Y - bar.Y) / (bar.Height - 1) * 0.85)); PalApply(); };
        var rb = new HitReg { R = Rc(bar.X - 4, bar.Y - 4, bar.Width + 8, bar.Height + 8), OnDown = () => pickBar(MousePos), OnDrag = pt => pickBar(pt), OnUp = PalCommit }; regs.Add(rb); Hint2(bar, "Lighter or darker");
        // the colour you have, its code, and the way back to the theme colour
        var sw = Rc(ix, y, Math.Max(40, x + w - ix), 44); Color cur = ProfAccent(ProfileStore.Accent); FillRR(g, cur, sw, 10); StrokeRR(g, Color.FromArgb(200, 255, 255, 255), sw, 10, 1.4f);
        Txt(g, ProfileStore.Accent < 0 ? "THEME" : "#" + ProfileStore.Accent.ToString("X6"), 11, Ink, Rc(ix, y + 50, sw.Width, 16), StringAlignment.Near, true);
        var rs = Rc(ix, y + 72, sw.Width, 28); Btn(g, rs, "RESET", ProfileStore.Accent < 0 ? 0 : 1, () => { if (ProfileStore.Accent >= 0) { ProfileStore.Accent = -1; PalCommit(); } }, 11); Hint2(rs, "Go back to the colour of your theme");
        return y + FH + 14;
    }

    // ---------- the whole look column of the profile editor ----------
    float PaintLookColumn(Graphics g, float c1, float ey, float colW, Color acc)
    {
        float ly = ey; Txt(g, "BANNER", 12, DimC, Rc(c1, ly, 200, 18), StringAlignment.Near, true); ly += 22; var b1 = Rc(c1, ly, (colW - 10) / 2, 34); var b2 = Rc(b1.Right + 10, ly, (colW - 10) / 2, 34); bool hasB = ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath);
        Btn(g, b1, "CHOOSE BANNER", 2, () => PickBanner(), 12); Hint2(b1, "Pick any picture: it is resized to fit the banner by itself, then zoom and move it as you like"); Btn(g, b2, "REMOVE BANNER", hasB ? 3 : 0, () => { if (hasB) ClearBanner(); }, 12); Hint2(b2, "Go back to the colour banner"); ly += 44;
        if (hasB) { var sz = PicSize(ProfileStore.BannerPath); int minZ = ProfileStore.ContainZoom(sz.Width, sz.Height, 1000, 200); ly = AdjustPicture(g, c1, ly, colW, acc, minZ, () => Math.Max(minZ, ProfileStore.BannerZoom), v => { ProfileStore.BannerZoom = v; ForgetFramed(); }, () => ProfileStore.BannerX, v => { ProfileStore.BannerX = v; ForgetFramed(); }, () => ProfileStore.BannerY, v => { ProfileStore.BannerY = v; ForgetFramed(); }, () => { ProfileStore.Save(); QueueRepublish(); }, "BANNER"); }
        Txt(g, "PICTURE", 12, DimC, Rc(c1, ly, 200, 18), StringAlignment.Near, true); ly += 22; var p1 = Rc(c1, ly, (colW - 10) / 2, 34); var p2 = Rc(p1.Right + 10, ly, (colW - 10) / 2, 34); bool hasP = ProjectLibrary.AvatarPath.Length > 0;
        Btn(g, p1, "CHOOSE PICTURE", 2, () => PickAvatar(), 12); Hint2(p1, "Your profile picture (only shown on this page and on your public profile): it is resized to fit its frame by itself");
        Btn(g, p2, "REMOVE PICTURE", hasP ? 3 : 0, () => { if (hasP) { ProjectLibrary.ClearAvatar(); ForgetFramed(); QueueRepublish(); Toast("Picture removed: the Purphex logo is shown again."); } }, 12); ly += 44;
        if (hasP) { string af = AvatarFile; var sz = PicSize(af); int minZ = ProfileStore.ContainZoom(sz.Width, sz.Height, 120, 120); ly = AdjustPicture(g, c1, ly, colW, acc, minZ, () => Math.Max(minZ, ProfileStore.AvZoom), v => { ProfileStore.AvZoom = v; ForgetFramed(); }, () => ProfileStore.AvX, v => { ProfileStore.AvX = v; ForgetFramed(); }, () => ProfileStore.AvY, v => { ProfileStore.AvY = v; ForgetFramed(); }, () => { ProfileStore.Save(); ProjectLibrary.BakeAvatar(); QueueRepublish(); }, "PICTURE"); }
        Txt(g, "ACCENT COLOUR", 12, DimC, Rc(c1, ly, 200, 18), StringAlignment.Near, true); ly += 22; ly = PaintPalette(g, c1, ly, colW);
        return ly;
    }
}
