using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// The look of your profile: the banner and the picture (zoom, left and right, up and down, fit and fill). The colours are in the COLOUR MIXER.
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
        g.Restore(gs); using (var p = RR(r, rad)) using (var pen = new Pen(ring, 3f)) g.DrawPath(pen, p); using (var p = RR(RectangleF.Inflate(r, 3, 3), rad + 3)) using (var pen = new Pen(Color.FromArgb(70, ring), 8f)) g.DrawPath(pen, p); if (r.Width >= 56) AnimRing(g, RectangleF.Inflate(r, 1, 1), ring);   // the lights that run round the picture
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

    // ---------- the profile colour: set from the COLOUR MIXER (the old colour square is gone) ----------
    int palSeen = int.MinValue;
    void PalCommit() { ProfileStore.Save(); QueueRepublish(); }

    // ---------- the whole look column of the profile editor ----------
    float PaintLookColumn(Graphics g, float c1, float ey, float colW, Color acc)
    {
        float ly = ey; Txt(g, "BANNER", 12, DimC, Rc(c1, ly, 200, 18), StringAlignment.Near, true); ly += 22; var b1 = Rc(c1, ly, (colW - 10) / 2, 34); var b2 = Rc(b1.Right + 10, ly, (colW - 10) / 2, 34); bool hasB = ProfileStore.BannerPath.Length > 0 && File.Exists(ProfileStore.BannerPath);
        Btn(g, b1, "CHOOSE BANNER", 2, () => PickBanner(), 12); Hint2(b1, "Pick any picture: it is resized to fit the banner by itself, then zoom and move it as you like"); Btn(g, b2, "REMOVE BANNER", hasB ? 3 : 0, () => { if (hasB && Sure("remove your banner")) ClearBanner(); }, 12); Hint2(b2, "Go back to the colour banner"); ly += 44;
        if (hasB) { var sz = PicSize(ProfileStore.BannerPath); int minZ = ProfileStore.ContainZoom(sz.Width, sz.Height, 1000, 200); ly = AdjustPicture(g, c1, ly, colW, acc, minZ, () => Math.Max(minZ, ProfileStore.BannerZoom), v => { ProfileStore.BannerZoom = v; ForgetFramed(); }, () => ProfileStore.BannerX, v => { ProfileStore.BannerX = v; ForgetFramed(); }, () => ProfileStore.BannerY, v => { ProfileStore.BannerY = v; ForgetFramed(); }, () => { ProfileStore.Save(); QueueRepublish(); }, "BANNER"); }
        Txt(g, "PICTURE", 12, DimC, Rc(c1, ly, 200, 18), StringAlignment.Near, true); ly += 22; var p1 = Rc(c1, ly, (colW - 10) / 2, 34); var p2 = Rc(p1.Right + 10, ly, (colW - 10) / 2, 34); bool hasP = ProjectLibrary.AvatarPath.Length > 0;
        Btn(g, p1, "CHOOSE PICTURE", 2, () => PickAvatar(), 12); Hint2(p1, "Your profile picture (only shown on this page and on your public profile): it is resized to fit its frame by itself");
        Btn(g, p2, "REMOVE PICTURE", hasP ? 3 : 0, () => { if (hasP && Sure("remove your picture")) { ProjectLibrary.ClearAvatar(); ForgetFramed(); QueueRepublish(); Toast("Picture removed: the Purphex logo is shown again."); } }, 12); ly += 44;
        if (hasP) { string af = AvatarFile; var sz = PicSize(af); int minZ = ProfileStore.ContainZoom(sz.Width, sz.Height, 120, 120); ly = AdjustPicture(g, c1, ly, colW, acc, minZ, () => Math.Max(minZ, ProfileStore.AvZoom), v => { ProfileStore.AvZoom = v; ForgetFramed(); }, () => ProfileStore.AvX, v => { ProfileStore.AvX = v; ForgetFramed(); }, () => ProfileStore.AvY, v => { ProfileStore.AvY = v; ForgetFramed(); }, () => { ProfileStore.Save(); ProjectLibrary.BakeAvatar(); QueueRepublish(); }, "PICTURE"); }
        return ly;
    }
}
