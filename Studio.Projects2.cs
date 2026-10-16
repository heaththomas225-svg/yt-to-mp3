using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// My Projects and Community pages: asking before a project opens, removing a project (and optionally its file), pictures that default to the Purphex logo,
// and a preview player: press PREVIEW on a project to hear it, with the bottom strip as a live visualiser (the logo in the middle, waves flowing out of it).
partial class StudioForm
{
    // test seams: the real program asks with a window; a test answers for itself (and never plays sound or deletes a file)
    Func<ProjectEntry, bool> confirmOpenHook; Func<ProjectEntry, int> removeHook; Action<StudioEngine> previewPlayFn;
    StudioEngine previewEng; string previewPath = "", previewName = ""; volatile bool previewLoading; bool previewPaused;
    // Space (or the pause button on the bar) pauses and continues the preview
    void PreviewPauseToggle()
    {
        var pe = previewEng; if (pe == null) return;
        if (previewPaused) { previewPaused = false; try { pe.Play(); } catch { } } else { previewPaused = true; try { pe.Pause(); } catch { } }
    }

    // clicking a project asks first (it used to send you into the project without a word)
    void OpenProjectAsk(ProjectEntry e)
    {
        if (e == null) return; if (e.Missing || !File.Exists(e.Path)) { Toast("That project file has moved or been deleted. Remove it from the list with the x."); return; }
        bool ok = confirmOpenHook != null ? confirmOpenHook(e) : MessageBox.Show(this, (eng.Tracks.Count > 0 ? OverwriteText(e.Name) : "Open \"" + e.Name + "\" now?"), "Open project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        if (!ok) return; StopPreview(); tab = 0; OpenProjectPath(e.Path);
    }
    // 0 = cancelled, 1 = off the list only, 2 = off the list and the file to the Recycling Bin (kept 30 days)
    void RemoveProjectAsk(ProjectEntry e)
    {
        if (e == null) return; bool exists = File.Exists(e.Path); int r = removeHook != null ? removeHook(e) : StudioDialogs.RemoveProject(this, e.Name, e.Path, exists); if (r == 0) return;
        if (string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase)) StopPreview();
        if (r == 2 && exists) { if (ProjectLibrary.MoveToBin(e) != null) Toast("Moved to the Recycling Bin. It waits there " + ProjectLibrary.BinDays + " days: restore it any time from RECYCLING BIN."); else { ProjectLibrary.Remove(e.Path); Toast("Removed from the list, but the file could not be moved (it may be open elsewhere)."); } }
        else { ProjectLibrary.Remove(e.Path); Toast("Removed from the list (the file is still on your PC)."); }
    }
    // a project with no picture shows the Purphex logo, fitted inside the square
    void DrawLogoArt(Graphics g, RectangleF r)
    {
        var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); using (var br = new LinearGradientBrush(r, TM.C(255, 26, 11, 56), TM.C(255, 6, 2, 16), 90f)) g.FillRectangle(br, r);
        var lg = AppLogo.Logo; if (lg != null) { float side = Math.Min(r.Width, r.Height) * 0.86f; float asp = lg.Width / (float)Math.Max(1, lg.Height), w = asp >= 1 ? side : side * asp, h = asp >= 1 ? side / asp : side; var im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(lg, new RectangleF(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h)); g.InterpolationMode = im; }
        g.Restore(gs);
    }

    // ---------- preview ----------
    void PreviewToggle(ProjectEntry e)
    {
        if (e == null) return; if (string.Equals(previewPath, e.Path, StringComparison.OrdinalIgnoreCase)) { StopPreview(); return; }
        if (e.Missing || !File.Exists(e.Path)) { Toast("That project file has moved or been deleted."); return; }
        StopPreview(); if (eng.Playing) eng.Pause(); previewPath = e.Path; previewName = e.Name; previewLoading = true; string path = e.Path;
        new Thread(() =>
        {
            StudioEngine pe = null; string err = null;
            try
            {   // the preview load gets its own ProjMeta, so your tempo, lyrics and picture are never touched (a save during a slow load used to write the wrong ones)
                pe = new StudioEngine { ListenOnly = true }; StudioEngine.Current = eng;   /* the preview engine must not become the engine the instruments (the Bass Lab piano) play through */
                ProjectIO.Load(pe, path, s => { }, new ProjectIO.ProjMeta());
            }
            catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!string.Equals(previewPath, path, StringComparison.OrdinalIgnoreCase) || err != null || pe == null || pe.EndFrame < 1000) { if (pe != null) { try { pe.Shutdown(); } catch { } } if (string.Equals(previewPath, path, StringComparison.OrdinalIgnoreCase)) { previewPath = ""; previewLoading = false; Toast(err != null ? "Could not open that project to preview it: " + err : "There is no audio in that project to preview."); } return; }
                    previewEng = pe; previewLoading = false; try { if (previewPlayFn != null) previewPlayFn(pe); else pe.Play(); } catch (Exception ex2) { Toast("Could not play the preview: " + ex2.Message); StopPreview(); }
                }));
            }
            catch { }
        }) { IsBackground = true, Name = "project-preview" }.Start();
    }
    void StopPreview()
    {
        StopVideoOverlay();
        var pe = previewEng; previewEng = null; previewPath = ""; previewName = ""; previewLoading = false; previewPaused = false; if (StudioEngine.Current == pe) StudioEngine.Current = eng;
        if (pe != null) { try { pe.Stop(); } catch { } try { pe.Shutdown(); } catch { } }
    }
    void PreviewHousekeeping()
    {
        if (tab != 3) { projBin = false; projSearchOn = false; }
        if (previewPath.Length == 0) return;
        // a preview keeps playing when you go to another page (the strip with the pause button shows on the projects, community, profile, artists and fonts pages); it stops only when the song reaches its end, when you stop it, or when something plays in the Mix, Mastering or Lyrics pages (below)
        if (previewEng != null && !previewEng.Playing && previewPlayFn == null && !previewPaused) { if (!EpQueueNext() && !ProjQueueNext()) StopPreview(); }   // it reached the end (PLAY EP goes on with the next song)
        if (eng.Playing) StopPreview();
    }
    // the strip along the bottom while a preview plays: the same design as the one under the tracks (solid black, the logo in the middle, waves flowing out of it)
    void PaintPreviewBar(Graphics g)
    {
        if (previewPath.Length == 0) return; float W = ClientSize.Width, H = ClientSize.Height; var ob = Rc(24, H - 48, W - 48, 36); var pe = previewEng; bool playing = pe != null && (pe.Playing || previewPlayFn != null); lastInput = DateTime.Now;
        VizUpdate(playing, pe); FillRR(g, Color.Black, ob, 12); using (var pen = new Pen(Color.FromArgb(playing ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(ob, 12)) g.DrawPath(pen, p); }
        {   // the name of what is playing sits in a small black box with a neon edge, so it reads on top of buttons and pictures
            string lbl = (pe == null ? "LOADING  " : "PREVIEW  ") + previewName; float lw = Math.Min(Math.Min(540, ob.Width - 12), g.MeasureString(lbl, GfxCache.Font(11, true)).Width + 20);
            var lr = Rc(ob.X + 6, ob.Y - 26, lw, 22); FillRR(g, TM.C(245, 4, 2, 12), lr, 7); Glow(g, NeonCyan, lr, 7, 0.55f, 3); StrokeRR(g, Color.FromArgb(220, NeonCyan), lr, 7, 1.3f);
            Txt(g, lbl, 11, NeonCyan, Rc(lr.X + 9, lr.Y + 1, lr.Width - 14, lr.Height - 2), StringAlignment.Near, true);
        }
        var pz = Rc(ob.X + 36, ob.Y + 5, 26, 26); FillRR(g, Hot(pz) ? TM.C(255, 70, 70, 170) : TM.C(255, 48, 44, 120), pz, 7); using (var pb = new SolidBrush(Color.White)) { if (previewPaused) g.FillPolygon(pb, new[] { new PointF(pz.X + 9, pz.Y + 6), new PointF(pz.X + 9, pz.Y + 20), new PointF(pz.X + 20, pz.Y + 13) }); else { g.FillRectangle(pb, pz.X + 8, pz.Y + 7, 4, 12); g.FillRectangle(pb, pz.X + 14, pz.Y + 7, 4, 12); } } Reg(pz, () => PreviewPauseToggle()); if (Hot(pz)) { bubble = previewPaused ? "Continue the preview (Space)" : "Pause the preview (Space)"; bubblePt = MousePos; }
        var stop = Rc(ob.X + 6, ob.Y + 5, 26, 26); FillRR(g, Hot(stop) ? TM.C(255, 160, 40, 80) : TM.C(255, 110, 30, 60), stop, 7); using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, stop.X + 8, stop.Y + 8, 10, 10); Reg(stop, () => StopPreview()); if (Hot(stop)) { bubble = "Stop the preview"; bubblePt = MousePos; }
        double dur = pe == null ? 0 : Math.Max(1, pe.EndFrame), pos = pe == null ? 0 : pe.Pos; var inner = Rc(ob.X + 72, ob.Y + 4, ob.Width - 72 - 112, ob.Height - 8); float cx = inner.X + inner.Width / 2, cy = ob.Y + ob.Height / 2;
        float px = inner.X + (float)(Math.Min(1.0, pos / Math.Max(1, dur)) * inner.Width); using (var b = new SolidBrush(TM.C(60, 0, 110, 130))) g.FillRectangle(b, inner.X, inner.Y, Math.Max(0, px - inner.X), inner.Height); using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 1.6f)) g.DrawLine(pen, px, inner.Y, px, inner.Bottom);
        float lh = ob.Height / 2 - 2, lhw = lh * 1.16f; cx = Math.Max(inner.X + lhw + 4, Math.Min(inner.Right - lhw - 4, px));   // the logo rides along with the song position
        if (playing)
        {
            var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); float maxH = ob.Height / 2 - 4; int hl = vizHist.Length;
            for (int c = 0; c < 190; c++)
            {
                float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxH * (1f - c / 260f) * (0.75f + 0.25f * vizBand[(c * 3) % vizBand.Length])); int al = (int)(235 * (1f - c / 200f)); if (al <= 4) break;
                float xl = cx - lhw - 6 - c * 2.2f, xr = cx + lhw + 6 + c * 2.2f;
                using (var pl = new Pen(TM.C(al, 0, 229, 255), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp); using (var pr = new Pen(TM.C(al, 255, 43, 214), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
            }
            g.Restore(st);
        }
        DrawLogo(g, cx, cy, lh, playing, Hot(Rc(cx - lhw, cy - lh, lhw * 2, lh * 2)));
        string tm = pe == null ? "" : ((int)(pos / StudioEngine.SR / 60)) + ":" + ((int)(pos / StudioEngine.SR % 60)).ToString("00") + " / " + ((int)(dur / StudioEngine.SR / 60)) + ":" + ((int)(dur / StudioEngine.SR % 60)).ToString("00"); Txt(g, tm, 12, Ink, Rc(ob.Right - 110, ob.Y, 100, ob.Height), StringAlignment.Far, true);
        if (pe != null) { Action<float> seek = mx => { double f = Math.Max(0, Math.Min(1, (mx - inner.X) / inner.Width)); try { pe.Seek((long)(f * dur)); } catch { } }; var rg = Reg(inner, () => seek(MousePos.X)); rg.OnDrag = pt => seek(pt.X); if (Hot(inner)) { bubble = "Click to jump to a place in the preview"; bubblePt = MousePos; } }
    }
}
