using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// The side menu (the three-lines button at the top left), the My Projects page with pictures, and the Community page.
partial class StudioForm
{
    class MItem { public string Text, Hint = ""; public Action Do; public bool Sub, Disabled; public string Key = ""; }
    bool menuOpen, projectsLoaded; int menuSec = -1, menuSub = -1; float projScroll; const string ImgFilter = "Pictures|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files|*.*";
    static readonly string[] MenuNames = { "Project", "Edit", "Tools", "View", "Community", "Theme", "Settings", "Help", "Exit" };

    // remember a project in the list (called on save and open)
    void NoteProject(string path, bool saved)
    {
        ProjectLibrary.Touch(path, projName, eng.Tracks.Count, Fx.ProjectBpm, eng.EndFrame / (double)StudioEngine.SR, saved, ProjectIO.CoverBytes); ProjectIO.CoverBytes = null; var e = ProjectLibrary.Find(path); if (e != null) { var roles = new List<string>(); foreach (var t in eng.Tracks) roles.Add(t.Instrument ? "inst" : (t.IsBus ? "bus" : t.Role)); e.Kind = ProjectLibrary.KindOfRoles(roles); } if (e != null && e.Cover.Length > 0) ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(e.Cover, 256);
    }

    void OpenMenu() { menuOpen = true; menuSec = -1; menuSub = -1; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); } }
    void CloseMenu() { menuOpen = false; }
    void GoTab(int t) { tab = t; if (t == 3 && !projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); } if (t == 3) projScroll = 0; CloseMenu(); }

    List<MItem> MenuItems(int sec)
    {
        var l = new List<MItem>(); Func<string, Action, MItem> I = (t, a) => { var m = new MItem { Text = t, Do = a }; l.Add(m); return m; };
        switch (sec)
        {
            case 0:
                I("New Project", () => { CloseMenu(); NewProject(); });
                I("Recent Projects", null).Sub = true;
                I("My Projects", () => GoTab(3));
                I("Open (project or music)...", () => { CloseMenu(); OpenClick(); });
                I("Save", () => { CloseMenu(); SaveClick(false); }).Key = "Ctrl+S";
                I("Save as...", () => { CloseMenu(); SaveClick(true); }).Key = "Ctrl+Shift+S";
                I("Set project picture...", () => { CloseMenu(); SetCurrentCover(); });
                I("Export...", () => { CloseMenu(); ExportClick(); });
                break;
            case 1:
                I("Undo", () => { CloseMenu(); DoUndo(); }).Key = "Ctrl+Z"; I("Redo", () => { CloseMenu(); DoRedo(); }).Key = "Ctrl+Y"; I("Split at the needle", () => { CloseMenu(); SplitSelected(); }).Key = "B";
                I("Cut a section out (CUT)", () => { CloseMenu(); CutClick(); }); I("Lock or unlock the track", () => { CloseMenu(); ToggleLock(); }); I("Duplicate track", () => { CloseMenu(); DuplicateTrack(Selected); }); I("Delete the selected piece", () => { CloseMenu(); DeleteClip(CurClip() ?? Selected); });
                break;
            case 2:
                I("Flex Pitch (edit notes)", () => { CloseMenu(); FlexOpen(); }); I("Purphex Sounds", () => { CloseMenu(); OpenSounds(); }); I("Sound packs (install)", () => { CloseMenu(); OpenSounds(); packsOpen = true; packScroll = 0; });
                I("Rapper Vocal Styles", () => { CloseMenu(); OpenStyles(); });
                I("Auto Mix", () => { CloseMenu(); AutoMixClick(); }); I("Split vocals + beat", () => { CloseMenu(); SeparateClick(); }); I("Detect BPM", () => { CloseMenu(); DetectBpmClick(); });
                I("Metronome on / off", () => { CloseMenu(); eng.Metronome = !eng.Metronome; }); I(snapOn ? "Snap to grid: ON" : "Snap to grid: OFF", () => { CloseMenu(); snapOn = !snapOn; Toast(snapOn ? "Snap to the grid is on." : "Snap to the grid is off."); }); I("Mastering", () => GoTab(1)); I("Add an instrument...", () => { CloseMenu(); AddInstrumentClick(); }); I("Add a track...", () => { CloseMenu(); ShowAddTrackMenuMain(); });
                break;
            case 3:
                I("Converter window", () => { CloseMenu(); Win.Bring(app); }); I(Prefs.ShowFps ? "Hide the FPS counter" : "Show the FPS counter", () => { CloseMenu(); Prefs.ShowFps = !Prefs.ShowFps; Prefs.Changed(); });
                I("Mix (tracks)", () => GoTab(0)); I("Mastering", () => GoTab(1)); I("Lyrics", () => GoTab(2)); I("My Projects", () => GoTab(3));
                I("Fit the whole song", () => { CloseMenu(); tab = 0; FitView(); }); I("Thin tracks", () => { CloseMenu(); LaneH = 84; }); I("Normal tracks", () => { CloseMenu(); LaneH = 104; }); I("Tall tracks", () => { CloseMenu(); LaneH = 168; });
                I(Win.IsFull(this) ? "MINIMISE (back to a normal window)" : "FULLSCREEN", () => { CloseMenu(); Win.ToggleFullscreen(this); });
                break;
            case 4:
                I("Community", () => GoTab(4)); I("My profile", () => GoTab(4));
                break;
            case 5:
                for (int ti = 0; ti < Themes.All.Length; ti++) { int k2 = ti; var tm = I(Themes.All[ti].Name, () => { Themes.Apply(k2); Prefs.Changed(); Toast("Theme: " + Themes.All[k2].Name + ". " + Themes.All[k2].Blurb); }); if (ti == Themes.Index) tm.Key = "IN USE"; }
                break;
            case 6:
                I("Settings", () => { CloseMenu(); app.ShowSettings(); });
                break;
            case 7:
                I("Help and guides", () => { CloseMenu(); app.ShowHelp(null); }).Key = "F1"; I("What's new", () => { CloseMenu(); Changelog.Show(this); });
                break;
            default:
                I("Close Purphex Music", () => { CloseMenu(); Close(); });
                break;
        }
        return l;
    }

    void SetCurrentCover()
    {
        if (string.IsNullOrEmpty(projPath) || !File.Exists(projPath)) { Toast("Save the project first, then you can give it a picture."); return; }
        PickCover(projPath);
    }
    void PickCover(string path)
    {
        using (var d = new OpenFileDialog { Title = "Choose a picture for this project", Filter = ImgFilter })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return; string err; if (!ProjectLibrary.SetCover(path, d.FileName, out err)) { MessageBox.Show(this, err, "Picture"); return; }
            if (string.Equals(path, projPath, StringComparison.OrdinalIgnoreCase)) { var e = ProjectLibrary.Find(path); if (e != null) { ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(e.Cover, 256); } }
            Toast("Picture added");
        }
    }
    void PickAvatar()
    {
        using (var d = new OpenFileDialog { Title = "Choose your profile picture", Filter = ImgFilter })
        { if (d.ShowDialog(this) != DialogResult.OK) return; string err; if (!ProjectLibrary.SetAvatar(d.FileName, out err)) MessageBox.Show(this, err, "Picture"); else Toast("Profile picture changed"); }
    }
    void AvatarMenu() { var m = Win.NewMenu(); m.Items.Add("Choose a picture...").Click += (a, b) => PickAvatar(); var rm = m.Items.Add("Remove picture (use the Purphex logo)"); rm.Enabled = ProjectLibrary.AvatarPath.Length > 0; rm.Click += (a, b) => { ProjectLibrary.ClearAvatar(); Toast("Picture removed: the Purphex logo is your picture now."); }; m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    void EditProfileName() { string nm = Ask("Your name:", ProjectLibrary.ProfileName); if (nm == null) return; ProjectLibrary.ProfileName = nm.Trim(); ProjectLibrary.Save(); }

    void DrawAvatar(Graphics g, RectangleF r)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); var pic = ProjectLibrary.Scaled(ProjectLibrary.AvatarPath, (int)Math.Round(r.Width), (int)Math.Round(r.Height), InterpolationMode.HighQualityBicubic); if (pic != null) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(pic, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), pic.Width, pic.Height)); } else if (AppLogo.Logo != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(AppLogo.Logo, r); } else ProjectLibrary.DrawArt(g, r, ProjectLibrary.ProfileName, 7); }   // no picture chosen: the Purphex emblem
        g.Restore(gs); using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 2f)) g.DrawEllipse(pen, r);
    }
    void DrawCover(Graphics g, RectangleF r, ProjectEntry e)
    {
        var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); var pic = e == null ? null : ProjectLibrary.Scaled(e.Cover, (int)Math.Round(r.Width), (int)Math.Round(r.Height), InterpolationMode.HighQualityBilinear);
        if (pic != null) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(pic, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), pic.Width, pic.Height)); } else DrawLogoArt(g, r);   // no picture: the Purphex logo
        g.Restore(gs);
    }

    // ---------------- the side menu ----------------
    void PaintMenu(Graphics g)
    {
        if (!menuOpen) return; float W = ClientSize.Width, H = ClientSize.Height;
        var all = Rc(0, 0, W, H); using (var b = new SolidBrush(Color.FromArgb(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseMenu());
        float w1 = 270, w2 = 310, w3 = 340; var c1 = Rc(0, 0, w1, H); using (var br = new LinearGradientBrush(c1, Color.FromArgb(252, 22, 12, 42), Color.FromArgb(252, 10, 5, 22), 90f)) g.FillRectangle(br, c1); Reg(c1, () => { });
        using (var pen = new Pen(Color.FromArgb(180, NeonViolet), 1.5f)) g.DrawLine(pen, w1, 0, w1, H);
        // header: your picture and name
        var av = Rc(18, 18, 64, 64); DrawAvatar(g, av); Reg(av, () => PickAvatar()).OnRight = () => AvatarMenu(); if (Hot(av)) bubble = "Click to change your picture. Right-click for more."; bubblePt = MousePos;
        if (ProjectLibrary.AvatarPath.Length > 0) { var ax = Rc(av.Right - 12, av.Y - 6, 22, 22); FillRR(g, Hot(ax) ? Color.FromArgb(255, 200, 50, 90) : Color.FromArgb(255, 140, 36, 70), ax, 11); Txt(g, "X", 11, Color.White, ax, StringAlignment.Center, true); Reg(ax, () => { ProjectLibrary.ClearAvatar(); Toast("Picture removed: the Purphex logo is your picture now."); }); if (Hot(ax)) { bubble = "Remove your picture (back to the Purphex logo)"; bubblePt = MousePos; } }
        Txt(g, ProjectLibrary.ProfileName, 16, Ink, Rc(94, 24, w1 - 106, 24), StringAlignment.Near, true); var nm = Rc(94, 24, w1 - 106, 24); Reg(nm, () => EditProfileName());
        Txt(g, "PURPHEX MUSIC", 11, DimC, Rc(94, 50, w1 - 106, 18), StringAlignment.Near, false);
        var close = Rc(w1 - 40, 6, 30, 26); Txt(g, "X", 14, DimC, close, StringAlignment.Center, true); Reg(close, () => CloseMenu());
        float y = 104; using (var pen = new Pen(Color.FromArgb(70, 140, 90, 220))) g.DrawLine(pen, 14, y - 10, w1 - 14, y - 10);
        for (int i = 0; i < MenuNames.Length; i++)
        {
            int k = i; var r = Rc(10, y, w1 - 20, 46); bool sel = menuSec == i; bool last = i == MenuNames.Length - 1; if (last) { y += 10; r = Rc(10, y, w1 - 20, 46); }
            if (sel || Hot(r)) { FillRR(g, sel ? Color.FromArgb(150, 70, 38, 130) : Color.FromArgb(90, 60, 34, 110), r, 10); if (sel) Glow(g, NeonViolet, r, 10, 0.45f, 2); }
            var ic = Rc(r.X + 12, r.Y + 11, 24, 24); DrawMenuIcon(g, ic, i, sel); Txt(g, MenuNames[i], 15, sel ? Ink : Color.FromArgb(225, 215, 245), Rc(r.X + 50, r.Y, 150, r.Height), StringAlignment.Near, sel);
            if (!last) Txt(g, ">", 14, DimC, Rc(r.Right - 34, r.Y, 24, r.Height), StringAlignment.Center, true);
            Reg(r, () => { menuSec = menuSec == k ? -1 : k; menuSub = -1; if (k == MenuNames.Length - 1) { var it = MenuItems(k)[0]; it.Do(); } }); y += 54;
        }
        if (menuSec >= 0 && menuSec < MenuNames.Length - 1) PaintMenuColumn(g, w1, w2, H);
    }
    void DrawMenuIcon(Graphics g, RectangleF r, int i, bool sel)
    {
        Color c = sel ? NeonCyan : Color.FromArgb(200, 190, 150, 255); using (var pen = new Pen(c, 1.8f))
        {
            switch (i)
            {
                case 0: g.DrawRectangle(pen, r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6); g.DrawLine(pen, r.X + 8, r.Y + 8, r.X + 8, r.Bottom - 8); g.DrawLine(pen, r.X + 8, r.Y + 8, r.Right - 8, r.Y + 6); break;
                case 1: g.DrawLine(pen, r.X + 5, r.Bottom - 5, r.Right - 5, r.Y + 5); g.DrawLine(pen, r.X + 5, r.Bottom - 5, r.X + 5, r.Bottom - 10); g.DrawLine(pen, r.X + 5, r.Bottom - 5, r.X + 10, r.Bottom - 5); break;
                case 2: g.DrawLine(pen, r.X + 4, r.Y + 6, r.Right - 4, r.Y + 6); g.DrawLine(pen, r.X + 4, r.Y + 12, r.Right - 4, r.Y + 12); g.DrawLine(pen, r.X + 4, r.Y + 18, r.Right - 4, r.Y + 18); using (var fb = new SolidBrush(c)) { g.FillEllipse(fb, r.X + 7, r.Y + 3, 6, 6); g.FillEllipse(fb, r.X + 15, r.Y + 9, 6, 6); g.FillEllipse(fb, r.X + 9, r.Y + 15, 6, 6); } break;
                case 3: g.DrawEllipse(pen, r.X + 2, r.Y + 6, r.Width - 4, r.Height - 12); using (var fb = new SolidBrush(c)) g.FillEllipse(fb, r.X + 9, r.Y + 9, 6, 6); break;
                case 4: g.DrawEllipse(pen, r.X + 3, r.Y + 3, 9, 9); g.DrawEllipse(pen, r.X + 12, r.Y + 3, 9, 9); g.DrawArc(pen, r.X + 2, r.Y + 11, 20, 14, 200, 140); break;
                case 5: g.DrawEllipse(pen, r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6); using (var fb = new SolidBrush(NeonPink)) g.FillEllipse(fb, r.X + 7, r.Y + 8, 5, 5); using (var fb = new SolidBrush(NeonCyan)) g.FillEllipse(fb, r.X + 13, r.Y + 8, 5, 5); using (var fb = new SolidBrush(NeonLime)) g.FillEllipse(fb, r.X + 10, r.Y + 14, 5, 5); break;
                case 6: g.DrawEllipse(pen, r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8); g.DrawEllipse(pen, r.X + 9, r.Y + 9, r.Width - 18, r.Height - 18); break;
                case 7: g.DrawEllipse(pen, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4); Txt(g, "?", 14, c, r, StringAlignment.Center, true); break;
                default: g.DrawLine(pen, r.X + 4, r.Y + 12, r.Right - 4, r.Y + 12); g.DrawLine(pen, r.X + 4, r.Y + 12, r.X + 10, r.Y + 6); g.DrawLine(pen, r.X + 4, r.Y + 12, r.X + 10, r.Y + 18); break;
            }
        }
    }
    void PaintMenuColumn(Graphics g, float x, float w, float H)
    {
        var items = MenuItems(menuSec); float h = 24 + items.Count * 46 + 14; var col = Rc(x + 8, 96 + menuSec * 54 - 4, w, h); if (col.Bottom > H - 8) col.Y = Math.Max(8, H - 8 - col.Height);
        FillRR(g, Color.FromArgb(252, 24, 14, 46), col, 12); StrokeRR(g, Color.FromArgb(200, NeonViolet), col, 12, 1.2f); Reg(col, () => { });
        float y = col.Y + 12;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; int k = i; var r = Rc(col.X + 8, y, w - 16, 42); bool hot = Hot(r) || (it.Sub && menuSub == i);
            if (hot) FillRR(g, Color.FromArgb(120, 76, 42, 140), r, 9);
            Txt(g, it.Text, 14, it.Disabled ? DimC : Ink, Rc(r.X + 14, r.Y, r.Width - (it.Key.Length > 0 ? 114 : (it.Sub ? 44 : 24)), r.Height), StringAlignment.Near, false);
            if (it.Key.Length > 0) Txt(g, it.Key, 11, DimC, Rc(r.Right - 110, r.Y, 100, r.Height), StringAlignment.Far, false);
            if (it.Sub) Txt(g, ">", 14, DimC, Rc(r.Right - 30, r.Y, 22, r.Height), StringAlignment.Center, true);
            if (it.Sub) Reg(r, () => { menuSub = k; }); else if (!it.Disabled) Reg(r, () => it.Do());
            y += 46;
        }
        if (menuSec == 0 && menuSub >= 0 && menuSub < items.Count && items[menuSub].Text == "Recent Projects") PaintRecent(g, col.Right + 6, col.Y + 12 + menuSub * 46 - 6, 350);
    }
    void PaintRecent(Graphics g, float x, float y, float w)
    {
        var rec = ProjectLibrary.Recent(8); float h = 20 + Math.Max(1, rec.Count) * 54 + 8; var col = Rc(x, Math.Min(y, ClientSize.Height - h - 8), w, h);
        FillRR(g, Color.FromArgb(252, 24, 14, 46), col, 12); StrokeRR(g, Color.FromArgb(200, NeonCyan), col, 12, 1.2f); Reg(col, () => { });
        if (rec.Count == 0) { Txt(g, "No recent projects yet. Save a project and it will show up here.", 12, DimC, Rc(col.X + 14, col.Y + 12, col.Width - 28, 40), StringAlignment.Near, false); return; }
        float yy = col.Y + 12;
        foreach (var e in rec)
        {
            string p = e.Path; var r = Rc(col.X + 8, yy, w - 16, 50); if (Hot(r)) FillRR(g, Color.FromArgb(120, 76, 42, 140), r, 9); DrawCover(g, Rc(r.X + 6, r.Y + 5, 40, 40), e);
            Txt(g, e.Name, 14, Ink, Rc(r.X + 56, r.Y + 4, r.Width - 64, 22), StringAlignment.Near, true); Txt(g, ProjectLibrary.Ago(e.Opened) + "  " + e.Tracks + " tracks", 11, DimC, Rc(r.X + 56, r.Y + 26, r.Width - 64, 18), StringAlignment.Near, false);
            var ee = e; Reg(r, () => { CloseMenu(); OpenProjectAsk(ee); }); yy += 54;
        }
    }

    // ---------------- My Projects ----------------
    void PaintProjects(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); }
        PaintLogoBackdrop(g, Rc(0, top, W, H - top));
        if (projBin) { PaintBin(g); return; }
        var list = VisibleProjects(); float y0 = PaintProjectsBar(g, W, top, ProjectLibrary.Entries.Count, list.Count); float cw = 230, ch = 322, gap = 18, x0 = 24; int cols = Math.Max(1, (int)((W - 48 + gap) / (cw + gap))); int count = list.Count + 1; int rows = (count + cols - 1) / cols; float bottomRoom = previewPath.Length > 0 ? 70 : 20;
        float maxScroll = Math.Max(0, y0 + rows * (ch + gap) - H + bottomRoom); projScroll = Math.Max(0, Math.Min(maxScroll, projScroll)); var area = Rc(0, y0 - 8, W, H - y0 + 8); Reg(area, () => { }).OnWheel = d => { projScroll = Math.Max(0, Math.Min(maxScroll, projScroll - d * 70)); };
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < count; i++)
        {
            float cx = x0 + (i % cols) * (cw + gap), cy = y0 + (i / cols) * (ch + gap) - projScroll; var card = Rc(cx, cy, cw, ch); if (card.Bottom < y0 - 8 || card.Y > H) continue;
            if (i == 0)
            {
                FillRR(g, Color.FromArgb(170, 20, 11, 40), card, 14); Win.DrawNeonBox(g, card, Hot(card)); Txt(g, "+", 54, NeonCyan, Rc(card.X, card.Y + 100, card.Width, 70), StringAlignment.Center, true); Txt(g, "NEW PROJECT", 15, Ink, Rc(card.X, card.Y + 180, card.Width, 26), StringAlignment.Center, true);
                Txt(g, "START WITH A CLEAN PAGE", 11, DimC, Rc(card.X, card.Y + 208, card.Width, 20), StringAlignment.Center, false); Reg(card, () => { StopPreview(); tab = 0; NewProject(); }); continue;
            }
            var e = list[i - 1]; string path = e.Path; bool hot = Hot(card); bool prev = string.Equals(previewPath, path, StringComparison.OrdinalIgnoreCase);
            FillRR(g, Color.FromArgb(205, 22, 12, 42), card, 14); Win.DrawNeonBox(g, card, hot || prev);
            var pic = Rc(card.X + 8, card.Y + 8, cw - 16, cw - 16); DrawCover(g, pic, e); Win.DrawNeonBox(g, pic, false); if (e.Missing) { using (var b = new SolidBrush(Color.FromArgb(170, 8, 4, 16))) g.FillRectangle(b, pic); Txt(g, "FILE NOT FOUND", 13, Color.FromArgb(255, 150, 150), pic, StringAlignment.Center, true); }
            Txt(g, e.Name, 15, Ink, Rc(card.X + 12, card.Y + 228, cw - 24, 24), StringAlignment.Near, true);
            string meta = e.Tracks + " tracks" + (e.Bpm > 0 ? "  " + e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "  " + ((int)(e.Seconds / 60)) + ":" + ((int)(e.Seconds % 60)).ToString("00") : ""); Txt(g, meta, 11, DimC, Rc(card.X + 12, card.Y + 252, cw - 24, 18), StringAlignment.Near, false);
            ProjectLibrary.EnsureKind(e); Txt(g, "EDITED " + ProjectLibrary.Ago(e.Updated), 11, DimC, Rc(card.X + 12, card.Y + 270, cw - 24 - 62, 18), StringAlignment.Near, false); if (e.Kind.Length > 0 && e.Kind != "Empty") Txt(g, e.Kind, 11, NeonCyan, Rc(card.Right - 12 - 62, card.Y + 270, 62, 18), StringAlignment.Far, true);
            var pv = Rc(card.Right - 12 - 142, card.Y + 290, 32, 26); var cam = Rc(pv.Right + 6, card.Y + 290, 32, 26); var nop = Rc(cam.Right + 6, card.Y + 290, 32, 26); var rm = Rc(nop.Right + 6, card.Y + 290, 28, 26);
            Reg(card, () => OpenProjectAsk(e)).OnRight = () => ProjectCardMenu(path);
            var star = Rc(card.X + 12, card.Y + 290, 32, 26); FillRR(g, Hot(star) ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(150, 44, 24, 80), star, 7); DrawStar(g, star, e.Fav, Hot(star)); Reg(star, () => ToggleFavourite(e)); Hint2(star, e.Fav ? "Remove from your favourites" : "Add to your favourites (a star)");
            FillRR(g, prev ? Color.FromArgb(235, 40, 140, 160) : (Hot(pv) ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(150, 44, 24, 80)), pv, 7); using (var b = new SolidBrush(Ink)) { if (prev) g.FillRectangle(b, pv.X + 11, pv.Y + 8, 10, 10); else g.FillPolygon(b, new[] { new PointF(pv.X + 11, pv.Y + 6), new PointF(pv.X + 11, pv.Y + 20), new PointF(pv.X + 22, pv.Y + 13) }); } Reg(pv, () => PreviewToggle(e)); if (Hot(pv)) { bubble = prev ? "Stop the preview" : "PREVIEW: hear this project without opening it"; bubblePt = MousePos; }
            FillRR(g, Hot(cam) ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(150, 44, 24, 80), cam, 7); DrawCamera(g, cam); Reg(cam, () => { if (!e.Missing) PickCover(path); }); if (Hot(cam)) { bubble = "Add or change the picture"; bubblePt = MousePos; }
            bool has = e.Cover.Length > 0; FillRR(g, has && Hot(nop) ? Color.FromArgb(220, 160, 50, 100) : Color.FromArgb(has ? 150 : 70, 44, 24, 80), nop, 7); DrawCamera(g, nop); using (var pen = new Pen(Color.FromArgb(has ? 255 : 110, 255, 90, 120), 2.2f)) g.DrawLine(pen, nop.X + 7, nop.Y + 20, nop.Right - 7, nop.Y + 5);
            Reg(nop, () => { if (e.Cover.Length > 0) { ProjectLibrary.ClearCover(path); Toast("Picture removed: the Purphex logo is shown again."); } else Toast("This project has no picture of its own (it shows the Purphex logo)."); }); if (Hot(nop)) { bubble = has ? "Remove the picture (back to the Purphex logo)" : "No picture to remove: it shows the Purphex logo"; bubblePt = MousePos; }
            FillRR(g, Hot(rm) ? Color.FromArgb(220, 160, 40, 80) : Color.FromArgb(150, 44, 24, 80), rm, 7); Txt(g, "X", 13, Ink, rm, StringAlignment.Center, true); Reg(rm, () => RemoveProjectAsk(e)); if (Hot(rm)) { bubble = "Remove from this list (it asks if the file should go too)"; bubblePt = MousePos; }
        }
        g.Restore(gs);
        if (list.Count == 0) { if (ProjectLibrary.Entries.Count > 0) TxtWrap(g, "NO PROJECT MATCHES. Change the search or press CLEAR to see all your projects.", 14, DimC, Rc(x0 + cw + 40, y0 + 90, W - cw - 100, 60)); else TxtWrap(g, "You have not saved a project yet. Make something, press Save, and it will appear here.", 14, DimC, Rc(x0 + cw + 40, y0 + 90, W - cw - 100, 60)); }
    }
    void DrawCamera(Graphics g, RectangleF r)
    {
        using (var pen = new Pen(Ink, 1.6f)) { g.DrawRectangle(pen, r.X + 7, r.Y + 8, r.Width - 14, r.Height - 14); g.DrawEllipse(pen, r.X + r.Width / 2 - 4, r.Y + 11, 8, 8); g.DrawLine(pen, r.X + 11, r.Y + 8, r.X + 13, r.Y + 5); g.DrawLine(pen, r.X + 13, r.Y + 5, r.X + 19, r.Y + 5); }
    }
    void ProjectCardMenu(string path)
    {
        var e = ProjectLibrary.Find(path); var m = Win.NewMenu(); bool ok = e != null && !e.Missing && File.Exists(path);
        m.Items.Add("Open").Click += (a, b) => { if (e != null) OpenProjectAsk(e); };
        m.Items.Add(string.Equals(previewPath, path, StringComparison.OrdinalIgnoreCase) ? "Stop the preview" : "Preview (hear it without opening)").Click += (a, b) => { if (e != null) PreviewToggle(e); };
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Rename...").Click += (a, b) => { if (e != null) RenameEntry(e); };
        m.Items.Add("Add or change picture...").Click += (a, b) => { if (ok) PickCover(path); };
        if (e != null) { m.Items.Add(e.Fav ? "Remove from favourites" : "Add to favourites").Click += (a, b) => ToggleFavourite(e); }
        m.Items.Add("Remove picture").Click += (a, b) => { if (e != null && e.Cover.Length > 0) { ProjectLibrary.ClearCover(path); Toast("Picture removed: the Purphex logo is shown again."); } else Toast("This project has no picture of its own."); };
        m.Items.Add("Show in folder").Click += (a, b) => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { } };
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Remove (list or Recycling Bin)...").Click += (a, b) => { if (e != null) RemoveProjectAsk(e); };
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("BACK").Click += (a, b) => GoBack();
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- Community (a place to build on later) ----------------
}
