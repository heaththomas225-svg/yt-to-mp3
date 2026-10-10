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
    bool menuOpen, projectsLoaded; int menuSec = 0, menuSub = -1; float projScroll; const string ImgFilter = "Pictures|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files|*.*";
    static readonly string[] MenuNames = { "Project", "Edit", "Tools", "View", "Community", "Settings", "Help", "Exit" };

    // remember a project in the list (called on save and open)
    void NoteProject(string path, bool saved)
    {
        ProjectLibrary.Touch(path, projName, eng.Tracks.Count, Fx.ProjectBpm, eng.EndFrame / (double)StudioEngine.SR, saved, ProjectIO.CoverBytes); ProjectIO.CoverBytes = null; var e = ProjectLibrary.Find(path); if (e != null && e.Cover.Length > 0) ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(e.Cover, 256);
    }

    void OpenMenu() { menuOpen = true; menuSub = -1; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); } }
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
                I("My Projects (pictures)", () => GoTab(3));
                I("Open (project or music)...", () => { CloseMenu(); OpenClick(); });
                I("Save", () => { CloseMenu(); SaveClick(false); }).Key = "Ctrl+S";
                I("Save as...", () => { CloseMenu(); SaveClick(true); }).Key = "Ctrl+Shift+S";
                I("Set project picture...", () => { CloseMenu(); SetCurrentCover(); });
                I("Export...", () => { CloseMenu(); ExportClick(); });
                break;
            case 1:
                I("Undo", () => { CloseMenu(); DoUndo(); }).Key = "Ctrl+Z"; I("Redo", () => { CloseMenu(); DoRedo(); }).Key = "Ctrl+Y"; I("Split at the needle", () => { CloseMenu(); SplitSelected(); }).Key = "B";
                I("Trim start here", () => { CloseMenu(); TrimStartHere(Selected); }); I("Trim end here", () => { CloseMenu(); TrimEndHere(Selected); }); I("Duplicate track", () => { CloseMenu(); DuplicateTrack(Selected); }); I("Delete track", () => { CloseMenu(); DeleteTrackQuiet(Selected); });
                break;
            case 2:
                I("Auto Mix", () => { CloseMenu(); AutoMixClick(); }); I("Split vocals + beat", () => { CloseMenu(); SeparateClick(); }); I("Detect BPM", () => { CloseMenu(); DetectBpmClick(); });
                I("Metronome on / off", () => { CloseMenu(); eng.Metronome = !eng.Metronome; }); I("Mastering", () => GoTab(1)); I("Add an instrument...", () => { CloseMenu(); AddInstrumentClick(); }); I("Add a track...", () => { CloseMenu(); ShowAddTrackMenuMain(); });
                break;
            case 3:
                I("Mix (tracks)", () => GoTab(0)); I("Mastering", () => GoTab(1)); I("Lyrics", () => GoTab(2)); I("My Projects", () => GoTab(3));
                I("Fit the whole song", () => { CloseMenu(); tab = 0; FitView(); }); I("Thin tracks", () => { CloseMenu(); LaneH = 84; }); I("Normal tracks", () => { CloseMenu(); LaneH = 104; }); I("Tall tracks", () => { CloseMenu(); LaneH = 168; });
                I(Win.IsFullscreen(this) ? "Leave full screen" : "Full screen", () => { CloseMenu(); Win.ToggleFullscreen(this); });
                break;
            case 4:
                I("Community", () => GoTab(4)); I("My profile (picture and name)", () => GoTab(4));
                break;
            case 5:
                I("Settings (background, audio...)", () => { CloseMenu(); app.ShowSettings(); });
                break;
            case 6:
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
    void EditProfileName() { string nm = Ask("Your name:", ProjectLibrary.ProfileName); if (nm == null) return; ProjectLibrary.ProfileName = nm.Trim(); ProjectLibrary.Save(); }

    void DrawAvatar(Graphics g, RectangleF r)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); var pic = ProjectLibrary.Picture(ProjectLibrary.AvatarPath); if (pic != null) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(pic, r); } else ProjectLibrary.DrawArt(g, r, ProjectLibrary.ProfileName, 7); }
        g.Restore(gs); using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 2f)) g.DrawEllipse(pen, r);
    }
    void DrawCover(Graphics g, RectangleF r, ProjectEntry e)
    {
        var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); var pic = e == null ? null : ProjectLibrary.Picture(e.Cover); if (pic != null) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(pic, r); } else ProjectLibrary.DrawArt(g, r, e == null ? "Purphex" : e.Name, 0); g.Restore(gs);
    }

    // ---------------- the side menu ----------------
    void PaintMenu(Graphics g)
    {
        if (!menuOpen) return; float W = ClientSize.Width, H = ClientSize.Height;
        var all = Rc(0, 0, W, H); using (var b = new SolidBrush(Color.FromArgb(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseMenu());
        float w1 = 270, w2 = 310, w3 = 340; var c1 = Rc(0, 0, w1, H); using (var br = new LinearGradientBrush(c1, Color.FromArgb(252, 22, 12, 42), Color.FromArgb(252, 10, 5, 22), 90f)) g.FillRectangle(br, c1); Reg(c1, () => { });
        using (var pen = new Pen(Color.FromArgb(180, NeonViolet), 1.5f)) g.DrawLine(pen, w1, 0, w1, H);
        // header: your picture and name
        var av = Rc(18, 18, 64, 64); DrawAvatar(g, av); Reg(av, () => PickAvatar()).OnRight = () => PickAvatar(); if (Hot(av)) bubble = "Click to change your picture"; bubblePt = MousePos;
        Txt(g, ProjectLibrary.ProfileName, 16, Ink, Rc(94, 24, w1 - 106, 24), StringAlignment.Near, true); var nm = Rc(94, 24, w1 - 106, 24); Reg(nm, () => EditProfileName());
        Txt(g, "Purphex Music", 11, DimC, Rc(94, 50, w1 - 106, 18), StringAlignment.Near, false);
        var close = Rc(w1 - 40, 6, 30, 26); Txt(g, "x", 14, DimC, close, StringAlignment.Center, true); Reg(close, () => CloseMenu());
        float y = 104; using (var pen = new Pen(Color.FromArgb(70, 140, 90, 220))) g.DrawLine(pen, 14, y - 10, w1 - 14, y - 10);
        for (int i = 0; i < MenuNames.Length; i++)
        {
            int k = i; var r = Rc(10, y, w1 - 20, 46); bool sel = menuSec == i; bool last = i == MenuNames.Length - 1; if (last) { y += 10; r = Rc(10, y, w1 - 20, 46); }
            if (sel || Hot(r)) { FillRR(g, sel ? Color.FromArgb(150, 70, 38, 130) : Color.FromArgb(90, 60, 34, 110), r, 10); if (sel) Glow(g, NeonViolet, r, 10, 0.45f, 2); }
            var ic = Rc(r.X + 12, r.Y + 11, 24, 24); DrawMenuIcon(g, ic, i, sel); Txt(g, MenuNames[i], 15, sel ? Ink : Color.FromArgb(225, 215, 245), Rc(r.X + 50, r.Y, 150, r.Height), StringAlignment.Near, sel);
            if (!last) Txt(g, ">", 14, DimC, Rc(r.Right - 34, r.Y, 24, r.Height), StringAlignment.Center, true);
            Reg(r, () => { menuSec = k; menuSub = -1; if (k == MenuNames.Length - 1) { var it = MenuItems(k)[0]; it.Do(); } }); y += 54;
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
                case 2: g.DrawLine(pen, r.X + 4, r.Y + 6, r.Right - 4, r.Y + 6); g.DrawLine(pen, r.X + 4, r.Y + 12, r.Right - 4, r.Y + 12); g.DrawLine(pen, r.X + 4, r.Y + 18, r.Right - 4, r.Y + 18); g.FillEllipse(new SolidBrush(c), r.X + 7, r.Y + 3, 6, 6); g.FillEllipse(new SolidBrush(c), r.X + 15, r.Y + 9, 6, 6); g.FillEllipse(new SolidBrush(c), r.X + 9, r.Y + 15, 6, 6); break;
                case 3: g.DrawEllipse(pen, r.X + 2, r.Y + 6, r.Width - 4, r.Height - 12); g.FillEllipse(new SolidBrush(c), r.X + 9, r.Y + 9, 6, 6); break;
                case 4: g.DrawEllipse(pen, r.X + 3, r.Y + 3, 9, 9); g.DrawEllipse(pen, r.X + 12, r.Y + 3, 9, 9); g.DrawArc(pen, r.X + 2, r.Y + 11, 20, 14, 200, 140); break;
                case 5: g.DrawEllipse(pen, r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8); g.DrawEllipse(pen, r.X + 9, r.Y + 9, r.Width - 18, r.Height - 18); break;
                case 6: g.DrawEllipse(pen, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4); Txt(g, "?", 14, c, r, StringAlignment.Center, true); break;
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
            Txt(g, it.Text, 14, it.Disabled ? DimC : Ink, Rc(r.X + 14, r.Y, r.Width - 110, r.Height), StringAlignment.Near, false);
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
            Reg(r, () => { CloseMenu(); OpenProjectPath(p); }); yy += 54;
        }
    }

    // ---------------- My Projects ----------------
    void PaintProjects(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + TransH; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); }
        Txt(g, "MY PROJECTS", 18, Ink, Rc(24, top + 12, 300, 30), StringAlignment.Near, true); Txt(g, "Your saved projects, newest first. Click one to open it. Use the camera to give it a picture.", 12, DimC, Rc(24, top + 44, W - 48, 20), StringAlignment.Near, false);
        Btn(g, Rc(W - 190, top + 12, 166, 32), "Back to the tracks", 0, () => tab = 0, 12);
        var list = ProjectLibrary.AllByEdited(); float cw = 230, ch = 270, gap = 18, x0 = 24, y0 = top + 80; int cols = Math.Max(1, (int)((W - 48 + gap) / (cw + gap))); int count = list.Count + 1; int rows = (count + cols - 1) / cols;
        float maxScroll = Math.Max(0, y0 + rows * (ch + gap) - H + 20); projScroll = Math.Max(0, Math.Min(maxScroll, projScroll)); var area = Rc(0, top + 74, W, H - top - 74); Reg(area, () => { }).OnWheel = d => { projScroll = Math.Max(0, Math.Min(maxScroll, projScroll - d * 70)); };
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < count; i++)
        {
            float cx = x0 + (i % cols) * (cw + gap), cy = y0 + (i / cols) * (ch + gap) - projScroll; var card = Rc(cx, cy, cw, ch); if (card.Bottom < top + 70 || card.Y > H) continue;
            if (i == 0)
            {
                FillRR(g, Color.FromArgb(150, 20, 11, 40), card, 14); Glow(g, NeonViolet, card, 14, Hot(card) ? 0.9f : 0.4f, 3); Txt(g, "+", 54, NeonCyan, Rc(card.X, card.Y + 70, card.Width, 70), StringAlignment.Center, true); Txt(g, "New project", 15, Ink, Rc(card.X, card.Y + 150, card.Width, 26), StringAlignment.Center, true);
                Txt(g, "Start with a clean page", 11, DimC, Rc(card.X, card.Y + 178, card.Width, 20), StringAlignment.Center, false); Reg(card, () => { tab = 0; NewProject(); }); continue;
            }
            var e = list[i - 1]; string path = e.Path; bool hot = Hot(card);
            FillRR(g, Color.FromArgb(200, 22, 12, 42), card, 14); Glow(g, e.Missing ? Color.FromArgb(200, 90, 90) : NeonViolet, card, 14, hot ? 0.9f : 0.35f, 3);
            var pic = Rc(card.X + 8, card.Y + 8, cw - 16, 170); DrawCover(g, pic, e); if (e.Missing) { using (var b = new SolidBrush(Color.FromArgb(170, 8, 4, 16))) g.FillRectangle(b, pic); Txt(g, "File not found", 13, Color.FromArgb(255, 150, 150), pic, StringAlignment.Center, true); }
            Txt(g, e.Name, 15, Ink, Rc(card.X + 12, card.Y + 184, cw - 24, 24), StringAlignment.Near, true);
            string meta = e.Tracks + " tracks" + (e.Bpm > 0 ? "  " + e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "  " + ((int)(e.Seconds / 60)) + ":" + ((int)(e.Seconds % 60)).ToString("00") : ""); Txt(g, meta, 11, DimC, Rc(card.X + 12, card.Y + 210, cw - 24, 18), StringAlignment.Near, false);
            Txt(g, "Edited " + ProjectLibrary.Ago(e.Updated), 11, DimC, Rc(card.X + 12, card.Y + 230, cw - 90, 18), StringAlignment.Near, false);
            var cam = Rc(card.Right - 78, card.Y + 226, 32, 26); var rm = Rc(card.Right - 40, card.Y + 226, 28, 26);
            Reg(card, () => { if (!e.Missing) { tab = 0; OpenProjectPath(path); } else Toast("That project file has moved or been deleted. Remove it from the list with the x."); }).OnRight = () => ProjectCardMenu(path);
            FillRR(g, Hot(cam) ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(150, 44, 24, 80), cam, 7); DrawCamera(g, cam); Reg(cam, () => { if (!e.Missing) PickCover(path); }); if (Hot(cam)) { bubble = "Add or change the picture"; bubblePt = MousePos; }
            FillRR(g, Hot(rm) ? Color.FromArgb(220, 160, 40, 80) : Color.FromArgb(150, 44, 24, 80), rm, 7); Txt(g, "x", 13, Ink, rm, StringAlignment.Center, true); Reg(rm, () => { ProjectLibrary.Remove(path); Toast("Removed from the list (the file is still on your PC)."); }); if (Hot(rm)) { bubble = "Remove from this list (does not delete the file)"; bubblePt = MousePos; }
        }
        g.Restore(gs);
        if (list.Count == 0) Txt(g, "You have not saved a project yet. Make something, press Save, and it will appear here.", 14, DimC, Rc(x0 + cw + 40, y0 + 90, W - cw - 100, 60), StringAlignment.Near, false);
    }
    void DrawCamera(Graphics g, RectangleF r)
    {
        using (var pen = new Pen(Ink, 1.6f)) { g.DrawRectangle(pen, r.X + 7, r.Y + 8, r.Width - 14, r.Height - 14); g.DrawEllipse(pen, r.X + r.Width / 2 - 4, r.Y + 11, 8, 8); g.DrawLine(pen, r.X + 11, r.Y + 8, r.X + 13, r.Y + 5); g.DrawLine(pen, r.X + 13, r.Y + 5, r.X + 19, r.Y + 5); }
    }
    void ProjectCardMenu(string path)
    {
        var m = Win.NewMenu(); m.Items.Add("Open").Click += (a, b) => { if (File.Exists(path)) { tab = 0; OpenProjectPath(path); } }; m.Items.Add("Add or change picture...").Click += (a, b) => { if (File.Exists(path)) PickCover(path); };
        m.Items.Add("Remove picture").Click += (a, b) => ProjectLibrary.ClearCover(path); m.Items.Add("Show in folder").Click += (a, b) => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { } };
        m.Items.Add("Remove from the list").Click += (a, b) => ProjectLibrary.Remove(path); m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- Community (a place to build on later) ----------------
    void PaintCommunity(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + TransH; float x = Math.Max(24, (W - 1040) / 2), w = Math.Min(1040, W - 48);
        Txt(g, "COMMUNITY", 18, Ink, Rc(x, top + 12, 300, 30), StringAlignment.Near, true); Btn(g, Rc(x + w - 166, top + 12, 166, 32), "Back to the tracks", 0, () => tab = 0, 12);
        // profile
        var pc = Rc(x, top + 60, w, 130); FillRR(g, Color.FromArgb(200, 22, 12, 42), pc, 14); Glow(g, NeonViolet, pc, 14, 0.4f, 3);
        var av = Rc(pc.X + 24, pc.Y + 20, 90, 90); DrawAvatar(g, av); Reg(av, () => PickAvatar()); if (Hot(av)) { bubble = "Click to add or change your picture"; bubblePt = MousePos; }
        Txt(g, ProjectLibrary.ProfileName, 22, Ink, Rc(pc.X + 134, pc.Y + 26, w - 400, 32), StringAlignment.Near, true); Reg(Rc(pc.X + 134, pc.Y + 26, w - 400, 32), () => EditProfileName()).OnDouble = EditProfileName;
        Txt(g, "Your profile. Click your picture or name to change them.", 12, DimC, Rc(pc.X + 134, pc.Y + 62, w - 300, 20), StringAlignment.Near, false);
        int n = ProjectLibrary.Entries.Count; Txt(g, n + (n == 1 ? " project" : " projects") + " on this PC", 12, DimC, Rc(pc.X + 134, pc.Y + 86, w - 300, 20), StringAlignment.Near, false);
        Btn(g, Rc(pc.Right - 190, pc.Y + 50, 166, 34), "Change picture", 1, () => PickAvatar(), 12);
        // the sections that will live here
        string[] names = { "Feed", "Following", "Challenges", "Collabs" }; string[] desc = { "Music from people you follow.", "Producers you follow.", "Weekly challenges.", "Find someone to make music with." };
        float cw = (w - 3 * 16) / 4f; float cy = pc.Bottom + 20;
        for (int i = 0; i < 4; i++)
        {
            var c = Rc(x + i * (cw + 16), cy, cw, 190); FillRR(g, Color.FromArgb(150, 20, 11, 40), c, 12); StrokeRR(g, Color.FromArgb(120, NeonViolet), c, 12, 1);
            Txt(g, names[i], 16, Ink, Rc(c.X + 14, c.Y + 14, c.Width - 28, 24), StringAlignment.Near, true); Txt(g, desc[i], 12, DimC, Rc(c.X + 14, c.Y + 48, c.Width - 28, 70), StringAlignment.Near, false);
            var tag = Rc(c.X + 14, c.Bottom - 40, 110, 24); FillRR(g, Color.FromArgb(120, 70, 40, 130), tag, 12); Txt(g, "Coming soon", 11, Ink, tag, StringAlignment.Center, true);
        }
        Txt(g, "The community is not built yet: these boxes are where it will go. Nothing you make is shared with anyone.", 12, DimC, Rc(x, cy + 206, w, 24), StringAlignment.Near, false);
    }
}
