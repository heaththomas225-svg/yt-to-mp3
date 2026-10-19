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
    // removing the open project's picture must clear the copy in memory too, or the next save writes it back
    void RemoveCover(string p) { ProjectLibrary.ClearCover(p); if (string.Equals(p, projPath, StringComparison.OrdinalIgnoreCase)) ProjectIO.CoverBytes = null; }
    bool menuOpen, projectsLoaded; int menuSec = -1, menuSub = -1; float projScroll; const string ImgFilter = "Pictures|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files|*.*";
    // the side menu: six lists (File, Edit, Tools, Pages, View, Community) and then single buttons that just take you there (Projects, Profile, Themes, Fonts, Settings, Help, Exit)
    static readonly string[] MenuNames = { "File", "Edit", "Tools", "Pages", "View", "Community", "Music Hub", "Video Studio", "Photo Studio PRO", "Projects", "Profile", "Themes", "Fonts", "Settings", "Help", "Exit" }; const int MenuLists = 6;
    static readonly int[] MenuIcon = { 0, 1, 2, 8, 3, 12, 13, 14, 15, 11, 4, 5, 10, 6, 7, 9 };
    Action MenuDirect(int i)
    {
        switch (MenuNames[i])
        {
            case "Music Hub": return () => OpenHub();
            case "Video Studio": return () => OpenCreative("video");   // Purphex Creative Studio: the video editor
            case "Photo Studio PRO": return () => OpenCreative("cover");   // Purphex Creative Studio: the photo editor (covers, posts, thumbnails, flyers)   // listen to your streaming services inside Purphex
            case "Projects": return () => GoTab(3);   // Projects: straight to your saved projects
            case "Profile": return () => { GoTab(6); profScroll = 0; };
            case "Themes": return () => { CloseMenu(); OpenThemeSheet(); };
            case "Fonts": return () => { GoTab(8); fontScroll = 0; };
            case "Settings": return () => { CloseMenu(); app.ShowSettings(); };
            case "Help": return () => { CloseMenu(); app.ShowHelp(null); };
            default: return () => { CloseMenu(); ExitDawClick(); };
        }
    }

    internal Func<string, DialogResult> exitAsk;   // a test answers for itself
    void ExitDawClick()
    {
        Func<string, DialogResult> ask = exitAsk ?? (t => MessageBox.Show(this, t, "Exit", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3));
        if (HasProject)
        {
            var r = ask("Exit will close the DAW (Purphex Music).\n\nSave your project now?\n\nYes: save it, then close the DAW.\nNo: close the DAW without saving.\nCancel: stay here.");
            if (r == DialogResult.Cancel) return; if (r == DialogResult.Yes && !SaveBeforeClose()) return;
        }
        else if (ask("Exit will close the DAW (Purphex Music).\n\nClose it?\n\nYes: close.  No or Cancel: stay here.") != DialogResult.Yes) return;
        leaving = true; Close();
    }

    // remember a project in the list (called on save and open)
    void NoteProject(string path, bool saved)
    {
        savedEdit = editCount;
        ProjectLibrary.Touch(path, projName, eng.Tracks.Count, Fx.ProjectBpm, eng.EndFrame / (double)StudioEngine.SR, saved, ProjectIO.CoverBytes); ProjectIO.CoverBytes = null; var e = ProjectLibrary.Find(path); if (e != null) { var roles = new List<string>(); foreach (var t in eng.Tracks) roles.Add(t.Instrument ? "inst" : (t.IsBus ? "bus" : t.Role)); e.Kind = ProjectLibrary.KindOfRoles(roles); } if (e != null && e.Cover.Length > 0) ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(e.Cover, 256);
    }

    void OpenMenu() { menuOpen = true; menuSec = -1; menuSub = -1; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); } }
    void CloseMenu() { menuOpen = false; }
    // the menu button: four glowing tiles that spread out when you point at it, and turn into an X while the menu is open
    void MenuBtn(Graphics g, RectangleF r)
    {
        Btn(g, r, "", menuOpen ? 4 : 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 12); Hint2(r, menuOpen ? "Close the menu" : "Menu: new, open, recent projects, my projects with pictures, edit, tools, community, settings and help");
        bool hot = Hot(r); float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2; var sm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (menuOpen)
        {
            using (var pen = new Pen(Color.White, 3.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round }) { g.DrawLine(pen, cx - 8, cy - 8, cx + 8, cy + 8); g.DrawLine(pen, cx + 8, cy - 8, cx - 8, cy + 8); }
        }
        else
        {
            float s = 8.5f, gap = hot ? 4.5f : 3f; Color[] cs = { NeonViolet, NeonCyan, NeonPink, Color.White };
            for (int k = 0; k < 4; k++)
            {
                var t = Rc(cx + (k % 2 == 0 ? -gap / 2 - s : gap / 2), cy + (k < 2 ? -gap / 2 - s : gap / 2), s, s);
                if (hot) Glow(g, cs[k], t, 3, 0.7f, 2);
                FillRR(g, cs[k], t, 2.6f); StrokeRR(g, Color.FromArgb(150, 255, 255, 255), t, 2.6f, 0.8f);
            }
        }
        g.SmoothingMode = sm;
    }
    void GoMixClose() { tab = 0; menuOpen = false; }   // the tools work on the tracks, so they take you to the Mix page
    readonly float[] menuYs = new float[16];
    void GoTab(int t) { tab = t; if (t == 3 && !projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); } if (t == 3) projScroll = 0; CloseMenu(); }

    List<MItem> MenuItems(int sec)
    {
        var l = new List<MItem>(); Func<string, Action, MItem> I = (t, a) => { var m = new MItem { Text = t, Do = a }; l.Add(m); return m; };
        switch (sec)
        {
            case 0:   // File
                I("New Project", () => { CloseMenu(); NewProject(); });
                I("Recent Projects", null).Sub = true;
                I("Open (project or music)...", () => { CloseMenu(); OpenClick(); });
                I("Save", () => { CloseMenu(); SaveClick(false); }).Key = "Ctrl+S";
                I("Save as...", () => { CloseMenu(); SaveClick(true); }).Key = "Ctrl+Shift+S";
                I("Set project picture...", () => { CloseMenu(); SetCurrentCover(); });
                I("Export...", () => { CloseMenu(); ExportClick(); });
                break;
            case 1:   // Edit (only on the Mix page)
                I("Undo", () => { CloseMenu(); DoUndo(); }).Key = "Ctrl+Z"; I("Redo", () => { CloseMenu(); DoRedo(); }).Key = "Ctrl+Y";
                I("Lock or unlock a track", () => { CloseMenu(); OpenTrackPicker(0); }); I("Duplicate track", () => { CloseMenu(); OpenTrackPicker(1); }); I("Delete track", () => { CloseMenu(); OpenTrackPicker(2); });
                break;
            case 2:   // Tools: they all work on the tracks, so they take you to the Mix page
                I("Auto Mix", () => { GoMixClose(); AutoMixClick(); }); I("Split vocals + beat", () => { GoMixClose(); SeparateClick(); }); I("Split into 4 stems", () => { GoMixClose(); Separate4Click(); }); I("Detect BPM", () => { GoMixClose(); DetectBpmClick(); });
                I("Flex Pitch (edit notes)", () => { GoMixClose(); FlexOpen(); }); I("Sounds and sound packs", () => { GoMixClose(); OpenPacks(); }); I("Vocal Presets", () => { GoMixClose(); OpenStyles(); }); I("Bus Presets", () => { GoMixClose(); OpenBusPresets(); }); I("AI Effect Generator", () => { GoMixClose(); OpenAiEffect(false); });
                I("Add a track...", () => { GoMixClose(); ShowAddTrackMenuMain(); });
                I("Metronome on / off", () => { GoMixClose(); eng.Metronome = !eng.Metronome; }); I(snapOn ? "Snap to grid: ON" : "Snap to grid: OFF", () => { GoMixClose(); snapOn = !snapOn; Toast(snapOn ? "Snap to the grid is on." : "Snap to the grid is off."); });
                break;
            case 5:   // Community: the side panel stays open so you pick the community tab or the artists page
                I("Community tab  (songs, albums, EPs, genres)", () => GoTab(4)); I("Artists page  (messages, friends, collabs)", () => { GoTab(7); OpenArtists(); });
                break;
            case 3:   // Pages
                I("Mix (tracks)", () => GoTab(0)); I("Mastering", () => GoTab(1)); I("Lyrics", () => GoTab(2));
                break;
            default:  // View
                I("Fit the whole song", () => { CloseMenu(); tab = 0; FitView(); });
                I("Thin tracks: " + (LaneH < 96 ? "ON" : "OFF") + "  (click to change)", () => { CloseMenu(); ToggleThinTracks(); });   // the same thing as the THIN TRACKS button
                I("Chat with artists (a window you can move)", () => { CloseMenu(); OpenChat(""); });
                I("Performance mode: " + (Prefs.PerfMode ? "ON" : "OFF") + "  (click to change)", () => { CloseMenu(); Prefs.PerfMode = !Prefs.PerfMode; Prefs.Changed(); });
                I(Win.IsFull(this) ? "MINIMISE (back to a normal window)" : "FULLSCREEN", () => { CloseMenu(); Win.ToggleFullscreen(this); });
                I("Converter window", () => { CloseMenu(); Win.Bring(app); });
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
            Toast("Picture added: cut and fitted by itself. Zoom and move it if you like."); if (ProjectLibrary.HasCoverSrc(path)) OpenCoverAdjust(path);
        }
    }
    void PickAvatar()
    {
        using (var d = new OpenFileDialog { Title = "Choose your profile picture", Filter = ImgFilter })
        { if (d.ShowDialog(this) != DialogResult.OK) return; string err; if (!ProjectLibrary.SetAvatar(d.FileName, out err)) MessageBox.Show(this, err, "Picture"); else Toast("Profile picture changed"); }
    }
    void EditProfileName() { string nm = Ask("Your name:", ProjectLibrary.ProfileName); if (nm == null) return; ProjectLibrary.ProfileName = nm.Trim(); ProjectLibrary.Save(); }

    // the Purphex emblem in a circle: it is the same everywhere and cannot be changed here (your own picture lives on the PROFILE page only)
    void DrawAvatar(Graphics g, RectangleF r)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); if (AppLogo.Logo != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(AppLogo.Logo, r); } else ProjectLibrary.DrawArt(g, r, "Purphex", 7); }
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
        var all = Rc(0, 0, W, H); using (var b = new SolidBrush(TM.C(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseMenu());
        float w1 = 270, w2 = 310, w3 = 340; var c1 = Rc(0, 0, w1, H); using (var br = new LinearGradientBrush(c1, TM.C(252, 22, 12, 42), TM.C(252, 10, 5, 22), 90f)) g.FillRectangle(br, c1); Reg(c1, () => { });
        using (var pen = new Pen(Color.FromArgb(180, NeonViolet), 1.5f)) g.DrawLine(pen, w1, 0, w1, H);
        // header: your picture and name
        Win.DrawNeonBox(g, Rc(8, 8, w1 - 16, 84), false);
        var av = Rc(18, 18, 64, 64); DrawAvatar(g, av);   // a picture is never a button: it is changed on the PROFILE page
        Txt(g, ProjectLibrary.ProfileName, 16, Ink, Rc(94, 24, w1 - 106, 24), StringAlignment.Near, true);
        { string cd = Edition.Countdown();   // an edition that ends counts down live here (the free trial, a monthly plan, a tester pass)
          if (cd.Length == 0) Txt(g, "PURPHEX MUSIC", 11, DimC, Rc(94, 50, w1 - 106, 18), StringAlignment.Near, false);
          else { var cr = Rc(94, 48, w1 - 106, 34); string lbl = Edition.OnTrial ? "FANTASY TRIAL" : Edition.Current == Tier.Fantasy ? "FANTASY ENDS IN" : "SUPPORTER ENDS IN"; Txt(g, lbl, 10, Color.FromArgb(255, 255, 200, 70), Rc(cr.X, cr.Y, cr.Width, 14), StringAlignment.Near, true); Txt(g, cd, 14, Color.FromArgb(255, 255, 222, 140), Rc(cr.X, cr.Y + 14, cr.Width, 20), StringAlignment.Near, true); Hint2(cr, Edition.OnTrial ? Edition.TrialLine() : "Time left on your edition. Renew it at purphexmusic.com."); } }
        var close = Rc(w1 - 40, 6, 30, 26); Txt(g, "X", 14, DimC, close, StringAlignment.Center, true); Reg(close, () => CloseMenu());
        float y = 104; using (var pen = new Pen(TM.C(70, 140, 90, 220))) g.DrawLine(pen, 14, y - 10, w1 - 14, y - 10);
        int shown = MenuNames.Length - (tab != 0 ? 1 : 0); float pitch = Math.Min(46f, (H - y - 30f) / shown), ih = Math.Min(40f, pitch - 6f);   // thirteen buttons: they tighten up on a short window so Exit is never cut off
        for (int i = 0; i < MenuNames.Length; i++)
        {
            if (i == 1 && tab != 0) { if (menuSec == 1) menuSec = -1; continue; }   // Edit (undo, split...) only makes sense on the Mix page
            bool list = i < MenuLists; bool last = i == MenuNames.Length - 1; if (i == MenuLists || last) y += 8;   // a gap before the single buttons and before Exit
            menuYs[i] = y; int k = i; var r = Rc(10, y, w1 - 20, ih); bool sel = list && menuSec == i;
            if (Hot(r) && MenuHoverOpens) { int want = list ? i : -1; if (menuSec != want) { menuSec = want; menuSub = -1; sel = list && menuSec == i; } }   // pointing at a menu shows its tabs at once, from where the pointer is (no click needed); pointing at a single button closes the tabs
            if (sel || Hot(r)) { FillRR(g, sel ? TM.C(150, 70, 38, 130) : TM.C(90, 60, 34, 110), r, 10); if (sel) Glow(g, NeonViolet, r, 10, 0.45f, 2); }
            Win.DrawNeonBox(g, r, sel || Hot(r));
            var ic = Rc(r.X + 12, r.Y + (ih - 24) / 2, 24, 24); DrawMenuIcon(g, ic, MenuIcon[i], sel); Txt(g, MenuNames[i], 15, sel ? Ink : TM.C(255, 225, 215, 245), Rc(r.X + 50, r.Y, 150, r.Height), StringAlignment.Near, sel);
            if (list) { Txt(g, ">", 14, DimC, Rc(r.Right - 34, r.Y, 24, r.Height), StringAlignment.Center, true); Reg(r, () => { menuSec = k; menuSub = -1; }); }
            else Reg(r, () => { menuSec = -1; MenuDirect(k)(); });
            y += pitch;
        }
        if (menuSec >= 0 && menuSec < MenuLists) PaintMenuColumn(g, w1, w2, H);
    }
    internal bool MenuHoverOpens = true;   // tests that press buttons turn it off
    void DrawMenuIcon(Graphics g, RectangleF r, int i, bool sel)
    {
        Color c = sel ? NeonCyan : TM.C(200, 190, 150, 255); using (var pen = new Pen(c, 1.8f))
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
                case 8: g.DrawRectangle(pen, r.X + 7, r.Y + 2, r.Width - 11, r.Height - 10); g.DrawRectangle(pen, r.X + 3, r.Y + 7, r.Width - 11, r.Height - 10); break;
                case 10: Txt(g, "Aa", 15, c, r, StringAlignment.Center, true); break;
                case 13: g.DrawArc(pen, r.X + 3, r.Y + 3, r.Width - 6, r.Height - 4, 180, 180); using (var fb = new SolidBrush(c)) { g.FillRectangle(fb, r.X + 2, r.Y + 13, 5, 9); g.FillRectangle(fb, r.Right - 7, r.Y + 13, 5, 9); } break;   // headphones: Music Hub
                case 14: g.DrawRectangle(pen, r.X + 2, r.Y + 5, r.Width - 4, r.Height - 10); for (int q = 0; q < 4; q++) { g.DrawLine(pen, r.X + 5 + q * 5, r.Y + 5, r.X + 5 + q * 5, r.Y + 8); g.DrawLine(pen, r.X + 5 + q * 5, r.Bottom - 8, r.X + 5 + q * 5, r.Bottom - 5); } using (var fb = new SolidBrush(c)) g.FillPolygon(fb, new[] { new PointF(r.X + 10, r.Y + 9), new PointF(r.X + 10, r.Bottom - 9), new PointF(r.X + 16, r.Y + r.Height / 2) }); break;   // film: Video Studio
                case 15: g.DrawRectangle(pen, r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6); g.DrawLine(pen, r.X + 5, r.Bottom - 7, r.X + 11, r.Y + 12); g.DrawLine(pen, r.X + 11, r.Y + 12, r.X + 15, r.Bottom - 10); g.DrawLine(pen, r.X + 15, r.Bottom - 10, r.Right - 5, r.Y + 9); using (var fb = new SolidBrush(c)) g.FillEllipse(fb, r.X + 6, r.Y + 6, 4, 4); break;   // picture: Cover Studio
                case 11: g.DrawRectangle(pen, r.X + 3, r.Y + 8, r.Width - 6, r.Height - 12); g.DrawLine(pen, r.X + 3, r.Y + 8, r.X + 3, r.Y + 4); g.DrawLine(pen, r.X + 3, r.Y + 4, r.X + 11, r.Y + 4); g.DrawLine(pen, r.X + 11, r.Y + 4, r.X + 13, r.Y + 8); break;
                case 12: g.DrawEllipse(pen, r.X + 9, r.Y + 2, 7, 7); g.DrawEllipse(pen, r.X + 2, r.Y + 14, 7, 7); g.DrawEllipse(pen, r.X + 15, r.Y + 14, 7, 7); g.DrawLine(pen, r.X + 11, r.Y + 9, r.X + 6, r.Y + 14); g.DrawLine(pen, r.X + 14, r.Y + 9, r.X + 19, r.Y + 14); break;
                default: g.DrawLine(pen, r.X + 4, r.Y + 12, r.Right - 4, r.Y + 12); g.DrawLine(pen, r.X + 4, r.Y + 12, r.X + 10, r.Y + 6); g.DrawLine(pen, r.X + 4, r.Y + 12, r.X + 10, r.Y + 18); break;
            }
        }
    }
    void PaintMenuColumn(Graphics g, float x, float w, float H)
    {
        var items = MenuItems(menuSec); float h = items.Count * 46 + 20; var col = Rc(x + 8, menuYs[menuSec] - 8, w, h); if (col.Bottom > H - 8) col.Y = Math.Max(8, H - 8 - col.Height);
        FillRR(g, TM.C(252, 24, 14, 46), col, 12); Win.DrawNeonBox(g, col, false); Reg(col, () => { });
        float y = col.Y + 12;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; int k = i; var r = Rc(col.X + 8, y, w - 16, 42); bool hot = Hot(r) || (it.Sub && menuSub == i);
            if (hot) FillRR(g, TM.C(120, 76, 42, 140), r, 9);
            Win.DrawNeonBox(g, r, hot);
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
        FillRR(g, TM.C(252, 24, 14, 46), col, 12); Win.DrawNeonBox(g, col, false); Reg(col, () => { });
        if (rec.Count == 0) { Txt(g, "No recent projects yet. Save a project and it will show up here.", 12, DimC, Rc(col.X + 14, col.Y + 12, col.Width - 28, 40), StringAlignment.Near, false); return; }
        float yy = col.Y + 12;
        foreach (var e in rec)
        {
            string p = e.Path; var r = Rc(col.X + 8, yy, w - 16, 50); if (Hot(r)) FillRR(g, TM.C(120, 76, 42, 140), r, 9); Win.DrawNeonBox(g, r, Hot(r)); DrawCover(g, Rc(r.X + 6, r.Y + 5, 40, 40), e);
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
        var list = VisibleProjects(); float y0 = PaintProjectsBar(g, W, top, ProjectLibrary.Entries.Count, list.Count); float cw = 230, ch = 428, gap = 18, x0 = 24; int cols = Math.Max(1, (int)((W - 48 - 56 + gap) / (cw + gap))); var relCards = GroupProjectReleases(list); int count = list.Count + relCards.Count + 1;   /* an EP or album is one card (its projects are inside it) */ int rows = (count + cols - 1) / cols; float bottomRoom = previewPath.Length > 0 ? 70 : 20;
        float maxScroll = Math.Max(0, y0 + rows * (ch + gap) - H + bottomRoom); projScroll = Math.Max(0, Math.Min(maxScroll, projScroll)); var area = Rc(0, y0 - 8, W, H - y0 + 8); Reg(area, () => { }).OnWheel = d => { projScroll = Math.Max(0, Math.Min(maxScroll, projScroll - d * 70)); };
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < count; i++)
        {
            float cx = x0 + (i % cols) * (cw + gap), cy = y0 + (i / cols) * (ch + gap) - projScroll; var card = Rc(cx, cy, cw, ch); if (card.Bottom < y0 - 8 || card.Y > H) continue;
            if (i == 0)
            {
                FillRR(g, TM.C(170, 20, 11, 40), card, 14); Win.DrawNeonBox(g, card, Hot(card)); Txt(g, "+", 54, NeonCyan, Rc(card.X, card.Y + 100, card.Width, 70), StringAlignment.Center, true); Txt(g, "NEW PROJECT", 15, Ink, Rc(card.X, card.Y + 180, card.Width, 26), StringAlignment.Center, true);
                Txt(g, "START WITH A CLEAN PAGE", 11, DimC, Rc(card.X, card.Y + 208, card.Width, 20), StringAlignment.Center, false); Reg(card, () => { StopPreview(); tab = 0; NewProject(); }); continue;
            }
            object it = i - 1 < projCardOrder.Count ? projCardOrder[i - 1] : null; if (it is Release) { PaintReleaseCard(g, card, (Release)it, i); continue; } var e = it as ProjectEntry; if (e == null) continue; string path = e.Path; bool hot = Hot(card); bool prev = string.Equals(previewPath, path, StringComparison.OrdinalIgnoreCase);
            FillRR(g, TM.C(205, 22, 12, 42), card, 14); Win.DrawNeonBox(g, card, hot || prev); if (card.Bottom > y0 && card.Y < H) AnimRing(g, card, i % 2 == 0 ? NeonCyan : NeonPink);   // the border runs with lights, like the picture on a profile
            var pic = Rc(card.X + 8, card.Y + 8, cw - 16, cw - 16); DrawCover(g, pic, e); Win.DrawNeonBox(g, pic, false); if (e.Missing) { using (var b = new SolidBrush(TM.C(170, 8, 4, 16))) g.FillRectangle(b, pic); Txt(g, "FILE NOT FOUND", 13, Color.FromArgb(255, 150, 150), pic, StringAlignment.Center, true); }
            Txt(g, e.Name, 15, Ink, Rc(card.X + 12, card.Y + 228, cw - 24, 24), StringAlignment.Near, true);
            string meta = (e.Bpm > 0 ? e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "  " + ((int)(e.Seconds / 60)) + ":" + ((int)(e.Seconds % 60)).ToString("00") : ""); meta = meta.Trim(); Txt(g, meta, 11, DimC, Rc(card.X + 12, card.Y + 252, cw - 24, 18), StringAlignment.Near, false);
            ProjectLibrary.EnsureKind(e); Txt(g, "EDITED " + ProjectLibrary.Ago(e.Updated), 11, DimC, Rc(card.X + 12, card.Y + 270, cw - 24 - 62, 18), StringAlignment.Near, false); if (e.Kind.Length > 0 && e.Kind != "Empty") Txt(g, e.Kind, 11, NeonCyan, Rc(card.Right - 12 - 62, card.Y + 270, 62, 18), StringAlignment.Far, true);
            var pv = Rc(card.X + 12 + 2 * 34, card.Y + 290, 30, 26); var cam = Rc(pv.Right + 4, card.Y + 290, 30, 26); var nop = Rc(cam.Right + 4, card.Y + 290, 30, 26); var rm = Rc(nop.Right + 4, card.Y + 290, 26, 26);   /* six buttons in one row, side by side: they never touch */
            Reg(card, () => OpenProjectAsk(e));   // no right-click menu: every option is a button on the card
            var star = Rc(card.X + 12, card.Y + 290, 30, 26); FillRR(g, Hot(star) ? TM.C(220, 90, 50, 170) : TM.C(150, 44, 24, 80), star, 7); DrawStar(g, star, e.Fav, Hot(star)); Reg(star, () => ToggleFavourite(e)); Hint2(star, e.Fav ? "Remove from your favourites" : "Add to your favourites (a star)");
            var pfb = Rc(star.Right + 4, card.Y + 290, 30, 26); FillRR(g, e.OnProfile ? TM.C(235, 150, 40, 130) : (Hot(pfb) ? TM.C(220, 90, 50, 170) : TM.C(150, 44, 24, 80)), pfb, 7); DrawPerson(g, pfb, e.OnProfile, Hot(pfb)); Reg(pfb, () => ToggleOnProfile(e)); Hint2(pfb, e.OnProfile ? "On your profile (public): press to make it private again" : "Private: press to PUBLISH TO PROFILE so other people see it on your profile");
            { var rnB = Rc(card.X + 12, card.Y + 392, cw - 24, 26); Btn(g, rnB, "RENAME", 1, () => RenameEntry(e), 10); Hint2(rnB, "Give this project a new name (it shows on your profile and in the community too)"); }
            { var vidB = Rc(card.X + 12, card.Y + 358, cw - 24, 26); bool hv = VideoRecs.Has(CommunitySongs.PidOf(e.Path)); Btn(g, vidB, hv ? "MUSIC VIDEO: ON (CHANGE)" : "UPLOAD MUSIC VIDEO", hv ? 4 : 1, () => UploadMusicVideo(e), 10); Hint2(vidB, "Add a music video of any size or shape. It goes with the song on the community page (publish the project first)."); }
            { var pubB = Rc(card.X + 12, card.Y + 324, cw - 24, 26); Btn(g, pubB, e.OnProfile ? "PUBLISHED: PRESS TO MAKE PRIVATE" : "PUBLISH TO PROFILE + COMMUNITY", e.OnProfile ? 4 : 2, () => ToggleOnProfile(e), 10); Hint2(pubB, e.OnProfile ? "This project is on your profile and in the community: press to make it private again" : "Publish this project to your profile and the community (it asks first). People see its name, tempo, length and tags, never the audio"); }
            FillRR(g, prev ? TM.C(235, 40, 140, 160) : (Hot(pv) ? TM.C(220, 90, 50, 170) : TM.C(150, 44, 24, 80)), pv, 7); using (var b = new SolidBrush(Ink)) { if (prev) g.FillRectangle(b, pv.X + 11, pv.Y + 8, 10, 10); else g.FillPolygon(b, new[] { new PointF(pv.X + 11, pv.Y + 6), new PointF(pv.X + 11, pv.Y + 20), new PointF(pv.X + 22, pv.Y + 13) }); } Reg(pv, () => PreviewToggle(e)); if (Hot(pv)) { bubble = prev ? "Stop the preview" : "PREVIEW: hear this project without opening it"; bubblePt = MousePos; }
            FillRR(g, Hot(cam) ? TM.C(220, 90, 50, 170) : TM.C(150, 44, 24, 80), cam, 7); DrawCamera(g, cam); Reg(cam, () => { if (!e.Missing) CoverClick(path); }); if (Hot(cam)) { bubble = "Add the picture, or resize it"; bubblePt = MousePos; }
            bool has = e.Cover.Length > 0; FillRR(g, has && Hot(nop) ? TM.C(220, 160, 50, 100) : TM.C(has ? 150 : 70, 44, 24, 80), nop, 7); DrawCamera(g, nop); using (var pen = new Pen(Color.FromArgb(has ? 255 : 110, 255, 90, 120), 2.2f)) g.DrawLine(pen, nop.X + 7, nop.Y + 20, nop.Right - 7, nop.Y + 5);
            Reg(nop, () => { if (e.Cover.Length > 0) { RemoveCover(path); Toast("Picture removed: the Purphex logo is shown again."); } else Toast("This project has no picture of its own (it shows the Purphex logo)."); }); if (Hot(nop)) { bubble = has ? "Remove the picture (back to the Purphex logo)" : "No picture to remove: it shows the Purphex logo"; bubblePt = MousePos; }
            FillRR(g, Hot(rm) ? TM.C(220, 160, 40, 80) : TM.C(150, 44, 24, 80), rm, 7); Txt(g, "X", 13, Ink, rm, StringAlignment.Center, true); Reg(rm, () => RemoveProjectAsk(e)); if (Hot(rm)) { bubble = "Remove from this list (it asks if the file should go too)"; bubblePt = MousePos; }
        }
        g.Restore(gs);
        { float barTop = y0 - 8, barH = H - barTop - (previewPath.Length > 0 ? 56 : 8); VScroll(g, Rc(W - 54, barTop, 42, barH), barH + maxScroll, barH, () => projScroll, v => projScroll = v); }
        if (list.Count == 0 && relCards.Count == 0) {   /* the songs of an EP are inside its card: the list is not empty then */ if (ProjectLibrary.Entries.Count > 0) TxtWrap(g, "NO PROJECT MATCHES. Change the search or press CLEAR to see all your projects.", 14, DimC, Rc(x0 + cw + 40, y0 + 90, W - cw - 100, 60)); else TxtWrap(g, "You have not saved a project yet. Make something, press Save, and it will appear here.", 14, DimC, Rc(x0 + cw + 40, y0 + 90, W - cw - 100, 60)); }
    }
    void DrawCamera(Graphics g, RectangleF r)
    {
        using (var pen = new Pen(Ink, 1.6f)) { g.DrawRectangle(pen, r.X + 7, r.Y + 8, r.Width - 14, r.Height - 14); g.DrawEllipse(pen, r.X + r.Width / 2 - 4, r.Y + 11, 8, 8); g.DrawLine(pen, r.X + 11, r.Y + 8, r.X + 13, r.Y + 5); g.DrawLine(pen, r.X + 13, r.Y + 5, r.X + 19, r.Y + 5); }
    }

    // ---------------- Community (a place to build on later) ----------------
}
