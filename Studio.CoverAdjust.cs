using System;
using System.Drawing;
using System.Windows.Forms;

// The sheet for the picture of a project: the picture is cut to its content and fitted by itself; here it can be zoomed and moved (the same controls as the profile picture), changed or removed.
partial class StudioForm
{
    bool caOpen; string caPath = "", caSrc = ""; int caZ = 100, caX = 50, caY = 50;

    // the picture button on a project card: no picture yet = choose one (then it opens here), a picture = zoom and move it
    internal void CoverClick(string path)
    {
        var e = ProjectLibrary.Find(path); if (e != null && e.Cover.Length > 0 && ProjectLibrary.EnsureCoverSrc(path)) OpenCoverAdjust(path); else PickCover(path);
    }
    internal void OpenCoverAdjust(string path)
    {
        if (!ProjectLibrary.EnsureCoverSrc(path)) return; caPath = path; caSrc = ProjectLibrary.CoverSrc(path); ProjectLibrary.LoadAdj(path, out caZ, out caX, out caY);
        ntOpen = false; busOpen = false; stylesOpen = false; themeOpen = false; psOpen = false; browserOpen = false; cpOpen = false; menuOpen = false; caOpen = true; ForgetFramed();
    }
    void CoverAdjCommit()
    {
        if (caPath.Length == 0) return; ProjectLibrary.SaveAdj(caPath, caZ, caX, caY); string err; if (!ProjectLibrary.BakeCover(caPath, out err)) { Toast(err); return; }
        if (string.Equals(caPath, projPath, StringComparison.OrdinalIgnoreCase)) { var e = ProjectLibrary.Find(caPath); if (e != null && e.Cover.Length > 0) ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(e.Cover, 256); }
    }
    void PaintCoverSheet(Graphics g)
    {
        if (!caOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => { caOpen = false; });
        var ent = ProjectLibrary.Find(caPath); if (ent == null || !System.IO.File.Exists(caSrc)) { caOpen = false; return; }
        float sw = Math.Min(520, W - 24), sh = Math.Min(H - 30, 640); var sheet = PopMove("coveradj", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "PICTURE", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, sheet.Width - 90, 34), StringAlignment.Near, true);
        TxtWrap(g, "It was cut to the picture itself and fitted to the frame by itself. Zoom and move it as you like.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 44, 34));
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => { caOpen = false; });
        float x = sheet.X + 22, w = sheet.Width - 44, y = sheet.Y + 88;
        float side = Math.Max(110, Math.Min(Math.Min(w, 280), H - 30 - 90 - 134 - 70 - 30)); var pv = Rc(sheet.X + (sheet.Width - side) / 2, y, side, side);
        var pic = FramedPic(caSrc + System.IO.File.GetLastWriteTime(caSrc).Ticks, () => LoadFileImage(caSrc), (int)side, (int)side, caZ, caX, caY);
        if (pic != null) DrawFrameImage(g, pv, pic, NeonViolet); y = pv.Bottom + 14;
        var sz = PicSize(caSrc); int minZ = ProfileStore.ContainZoom(sz.Width, sz.Height, (int)side, (int)side);
        y = AdjustPicture(g, x, y, w, NeonViolet, minZ, () => Math.Max(minZ, caZ), v => { caZ = v; }, () => caX, v => { caX = v; }, () => caY, v => { caY = v; }, () => CoverAdjCommit(), "PICTURE");
        float bw = (w - 20) / 3f; var b1 = Rc(x, y, bw, 32); var b2 = Rc(b1.Right + 10, y, bw, 32); var b3 = Rc(b2.Right + 10, y, bw, 32);
        Btn(g, b1, "CHOOSE ANOTHER", 1, () => PickCover(caPath), 11); Hint2(b1, "Pick a different picture: it is cut and fitted by itself again");
        Btn(g, b2, "REMOVE", 3, () => { if (Sure("remove the picture")) { ProjectLibrary.ClearCover(caPath); caOpen = false; Toast("Picture removed: the Purphex logo is shown again."); } }, 11); Hint2(b2, "Back to the Purphex logo");
        Btn(g, b3, "DONE", 2, () => { caOpen = false; }, 12); Hint2(b3, "Keep the picture as it is shown");
    }
}
