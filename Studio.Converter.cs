using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

// =====================================================================================================
//  THE CONVERTER IN THE STUDIO (tab 11, the CONVERTER button at the top): the MP3 / MP4 converter's own panel is moved onto this page
//  while it is open (everything in it works as before) and goes back to its own window when you leave the page.
// =====================================================================================================
partial class StudioForm
{
    const float TabsW = 444;   // MIX, MASTERING, LYRICS and CONVERTER at the top right
    Rectangle ConverterRect { get { int w = 640, h = Math.Max(300, Math.Min(712, ClientSize.Height - ToolH - 24)); return new Rectangle(Math.Max(10, (ClientSize.Width - w) / 2), ToolH + 12, w, h); } }
    bool convSyncQueued;
    // the panel is moved after painting (moving controls while painting is not allowed)
    void SyncConverter()
    {
        if (app == null || convSyncQueued) return; bool want = tab == 11, has = app.ConverterInStudio;
        if (want == has && (!want || app.ConverterBounds == ConverterRect)) return;   // already where it belongs
        convSyncQueued = true; BeginInvokeSafe(() => { convSyncQueued = false; if (tab == 11) app.EmbedConverter(this, ConverterRect); else app.ReleaseConverter(); Invalidate(); });
    }
    // an update was found while you are in the studio: a box over the studio with UPDATE NOW and LATER (the converter window, where its card lives, is hidden)
    string updVer = "", updHave = ""; List<string> updNotes; Action updYes, updNo;
    internal void ShowUpdateBox(string ver, string have, List<string> notes, Action yes, Action later) { updVer = ver; updHave = have; updNotes = notes ?? new List<string>(); updYes = yes; updNo = later; Invalidate(); }
    void PaintUpdateBox(Graphics g)
    {
        if (updYes == null) return; float W = ClientSize.Width, H = ClientSize.Height; int nl = Math.Min(8, updNotes.Count); var box = Rc(W / 2 - 300, H / 2 - (150 + nl * 22) / 2f, 600, 150 + nl * 22 + (nl > 0 ? 30 : 0));
        using (var b = new SolidBrush(TM.C(160, 2, 1, 8))) g.FillRectangle(b, 0, 0, W, H); Reg(Rc(0, 0, W, H), () => { });
        using (var p = RR(box, 18)) { using (var b = new SolidBrush(TM.C(255, 12, 5, 28))) g.FillPath(b, p); var gs = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect); PaintGalaxy(g, box, 0x0DA7E, 0.7f); g.Restore(gs); }
        Glow(g, NeonCyan, box, 18, 0.9f, 4); StrokeRR(g, NeonCyan, box, 18, 2f);
        using (var f = new Font("Segoe UI Black", 22f, FontStyle.Italic, GraphicsUnit.Pixel)) { var sm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; GalaxyText(g, "UPDATE AVAILABLE", f, box.X + 24, box.Y + 18); g.SmoothingMode = sm; }
        Txt(g, "Purphex " + updVer + " is ready (you have " + updHave + "). Update now? Purphex restarts by itself; save your project first.", 11.5f, Ink, Rc(box.X + 24, box.Y + 54, box.Width - 48, 36), StringAlignment.Near, false);
        float y = box.Y + 96; if (nl > 0) { Txt(g, "WHAT'S NEW", 11, NeonPink, Rc(box.X + 24, y, 200, 18), StringAlignment.Near, true); y += 24; foreach (var l in updNotes.Take(nl)) { Txt(g, "•  " + l, 10.5f, DimC, Rc(box.X + 24, y, box.Width - 48, 20), StringAlignment.Near, false); y += 22; } }
        var yb = Rc(box.X + 24, box.Bottom - 52, 260, 36); Btn(g, yb, "UPDATE NOW", 2, () => { var a = updYes; updYes = null; updNo = null; if (a != null) a(); }, 13); Hint2(yb, "Download " + updVer + " and restart Purphex with it");
        var lb = Rc(box.Right - 284, box.Bottom - 52, 260, 36); Btn(g, lb, "LATER", 1, () => { var a = updNo; updYes = null; updNo = null; if (a != null) a(); }, 13); Hint2(lb, "Keep working: Purphex asks again the next time you open it");
    }
    // where the studio window was, so it opens there next time (on the same screen, the same size, maximised or not)
    void SavePlace()
    {
        if (StudioEnv.UnderTest || WindowState == FormWindowState.Minimized) return; var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        Prefs.StudioPlace = b.X + "," + b.Y + "," + b.Width + "," + b.Height + "," + (WindowState == FormWindowState.Maximized ? 1 : 0);
    }
    void RestorePlace()
    {
        if (StudioEnv.UnderTest) return; var p = (Prefs.StudioPlace ?? "").Split(','); int x, y, w, h; if (p.Length < 5 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
        var r = new Rectangle(x, y, Math.Max(MinimumSize.Width, w), Math.Max(MinimumSize.Height, h));
        foreach (var scr in Screen.AllScreens)
        {
            var vis = Rectangle.Intersect(scr.WorkingArea, r); if (vis.Width < 200 || vis.Height < 150) continue;   // that screen is still connected and the window would be on it
            if (r.Width > scr.WorkingArea.Width || r.Height > scr.WorkingArea.Height) r = new Rectangle(scr.WorkingArea.X, scr.WorkingArea.Y, Math.Min(r.Width, scr.WorkingArea.Width), Math.Min(r.Height, scr.WorkingArea.Height));
            StartPosition = FormStartPosition.Manual; Bounds = r; if (p[4] == "1") WindowState = FormWindowState.Maximized; return;
        }
    }
    void BeginInvokeSafe(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }
    void PaintConverterPage(Graphics g)
    {
        var r = ConverterRect; var fr = RectangleF.Inflate(new RectangleF(r.X, r.Y, r.Width, r.Height), 8, 8);
        Glow(g, NeonCyan, fr, 14, 0.8f, 4); StrokeRR(g, NeonCyan, fr, 14, 1.6f);
        Txt(g, "Convert links to MP3 or MP4 here. Your files go to the folder in SAVE TO; drag one onto the MIX page to use it in a song.", 11, DimC, Rc(10, fr.Bottom + 6, ClientSize.Width - 20, 18), StringAlignment.Center, false);
    }
}
