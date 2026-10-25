using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// The X of the DAW: YOU ARE LEAVING THE DAW. If the project is not saved it offers to save it (you give it a name, and it is in My Projects afterwards).
partial class StudioForm
{
    int editCount, savedEdit; bool leaving, welcomeOn;
    // when Purphex Music is opened it starts on the mix, with a neon box on top: open one of your projects or start a new one (the box goes away with any choice, and the mix is right behind it)
    internal void ShowWelcome() { welcomeOn = true; tab = 0; welcomeAt = Environment.TickCount; if (!projectsLoaded) { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); if (!StudioEnv.UnderTest) System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { if (ProjectLibrary.FitOldCovers() > 0) lastInput = DateTime.Now; } catch { } }); }
        var wt = new System.Windows.Forms.Timer { Interval = 6000 }; wt.Tick += (s2, e2) => { wt.Stop(); wt.Dispose(); EmojiPanel.WarmColours(); }; wt.Start();   // a few seconds after the DAW opens the colour of every emoji is made in the background (once: they are kept in a folder)
    }
    int welcomeAt; internal bool WelcomeShowing { get { return welcomeOn && !HasProject && tab == 0 && !menuOpen; } }
    void PaintWelcome(Graphics g)
    {
        if (!WelcomeShowing) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(TM.C(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => welcomeOn = false);
        var list = ProjectLibrary.AllByEdited(); int n = list.Count; bool cards = H >= 520; float bw = Math.Min(1080, W - 40), side = bw >= 900 ? 280 : 0, gap = 12, cw = (bw - side - 56 - gap * 3) / 4f, bh = cards ? 168 + cw + 56 + 28 + (n > 4 ? 44 : 0) : 196;   // every project, in a row that scrolls
        int age = unchecked(Environment.TickCount - welcomeAt); float k = Math.Min(1f, age / 260f), ease = 1 - (1 - k) * (1 - k), grow = 0.93f + 0.07f * ease; lastInput = DateTime.Now;
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2 - 10, bw * grow, bh * grow); double pulse = 0.5 + 0.5 * Math.Sin(age / 320.0);
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((16 + 22 * pulse) / i * 2.2f) + 5, NeonCyan), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 22 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 22)) { using (var br = new LinearGradientBrush(box, TM.C(250, 26, 12, 58), TM.C(250, 9, 4, 24), 90f)) g.FillPath(br, p); using (var pen = new Pen(NeonCyan, 2.2f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(110, NeonPink), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -9, -9), 16)) g.DrawPath(pen, p); AnimRing(g, box, NeonPink); Reg(box, () => { });
        Txt(g, "WELCOME TO PURPHEX MUSIC", 24, Ink, Rc(box.X + 28, box.Y + 18, box.Width - 110, 36), StringAlignment.Near, true);
        Txt(g, "Open one of your projects, or start something new.", 13, DimC, Rc(box.X + 28, box.Y + 56, box.Width - 110, 22), StringAlignment.Near, false);
        var cl = Rc(box.Right - 46, box.Y + 14, 30, 28); Btn(g, cl, "x", 3, () => welcomeOn = false, 12); Hint2(cl, "Close this and go to the mix");
        var nb = Rc(box.X + 28, box.Y + 92, 240, 34); Btn(g, nb, "START A NEW PROJECT", 2, () => { welcomeOn = false; tab = 0; NewProject(); }, 13); Hint2(nb, "An empty project: add music, record or add an instrument");
        var ob = Rc(nb.Right + 12, box.Y + 92, 240, 34); Btn(g, ob, "OPEN A PROJECT FILE...", 1, () => { welcomeOn = false; if (!OpenClick()) { welcomeOn = true; welcomeAt = Environment.TickCount; } }, 13);   /* cancelling the window brings the welcome box back */ Hint2(ob, "Find a project file (.ytmproj) on your PC");
        if (list.Count > 4 || !cards) { var mp = Rc(ob.Right + 12, box.Y + 92, Math.Max(120, box.Right - 28 - ob.Right - 12), 34); Btn(g, mp, "ALL MY PROJECTS", 1, () => { welcomeOn = false; GoTab(3); }, 13); Hint2(mp, "Every project you have, with search and filters"); }
        if (side > 0)
        {   // the converter beside your projects
            var cv = Rc(box.Right - side * grow - 4, box.Y + 92, side * grow - 24, box.Height - 120); FillRR(g, TM.C(200, 14, 6, 34), cv, 14); StrokeRR(g, Color.FromArgb(200, NeonPink), cv, 14, 1.4f);
            Txt(g, "CONVERTER", 16, Ink, Rc(cv.X + 16, cv.Y + 14, cv.Width - 32, 24), StringAlignment.Near, true);
            TxtWrap(g, "Paste a link and get an MP3 or an MP4, right here in the studio. It is always one click away: the CONVERTER button at the top.", 12, DimC, Rc(cv.X + 16, cv.Y + 44, cv.Width - 32, 90));
            var go = Rc(cv.X + 16, cv.Bottom - 50, cv.Width - 32, 34); Btn(g, go, "OPEN THE CONVERTER", 2, () => { welcomeOn = false; GoTab(11); }, 13); Hint2(go, "The MP3 / MP4 converter, inside the studio");
        }
        if (!cards) return;
        Txt(g, "YOUR PROJECTS", 12, DimC, Rc(box.X + 28, box.Y + 142, 300, 20), StringAlignment.Near, true);
        if (n == 0) { TxtWrap(g, "You have no projects yet. Press START A NEW PROJECT, drop in some music and press SAVE: it will be here next time.", 13, DimC, Rc(box.X + 28, box.Y + 176, box.Width - 56 - side * grow, 60)); return; }
        float viewW = box.Width - 56 - (side > 0 ? side * grow + 8 : 0); cw = (viewW - gap * 3) / 4f; float total = n * (cw + gap) - gap, maxS = Math.Max(0, total - viewW); welScroll = Math.Max(0, Math.Min(maxS, welScroll));
        var strip = Rc(box.X + 28, box.Y + 168, viewW, cw + 56); var gsw = g.Save(); g.SetClip(Rc(strip.X - 4, strip.Y - 4, strip.Width + 8, strip.Height + 8), CombineMode.Intersect);
        for (int i = 0; i < n; i++)
        {
            var e = list[i]; var card = Rc(box.X + 28 + i * (cw + gap) - welScroll, box.Y + 168, cw, cw + 56); if (card.Right < strip.X || card.X > strip.Right) continue; bool hot = Hot(RectangleF.Intersect(card, strip));
            FillRR(g, TM.C(205, 22, 12, 42), card, 12); Win.DrawNeonBox(g, card, hot); var pic = Rc(card.X + 8, card.Y + 8, cw - 16, cw - 16); DrawCover(g, pic, e); Win.DrawNeonBox(g, pic, false);
            if (e.Missing) { using (var b = new SolidBrush(TM.C(170, 8, 4, 16))) g.FillRectangle(b, pic); Txt(g, "FILE NOT FOUND", 12, Color.FromArgb(255, 150, 150), pic, StringAlignment.Center, true); }
            Txt(g, e.Name, 14, Ink, Rc(card.X + 10, card.Y + cw, cw - 20, 22), StringAlignment.Near, true);
            Txt(g, "EDITED " + ProjectLibrary.Ago(e.Updated), 10, DimC, Rc(card.X + 10, card.Y + cw + 22, cw - 20, 18), StringAlignment.Near, false);
            var vis = RectangleF.Intersect(card, strip); if (vis.Width > 8) { Reg(vis, () => { OpenProjectAsk(e); if (HasProject) welcomeOn = false; }); Hint2(vis, "Open this project"); }
        }
        g.Restore(gsw);
        if (maxS > 0)
        {
            regs.Add(new HitReg { R = strip, OnWheel = d => welScroll = Math.Max(0, Math.Min(maxS, welScroll - d * (cw + gap))) });
            HScroll(g, Rc(strip.X, strip.Bottom + 10, strip.Width, 34), total, viewW, () => welScroll, v => welScroll = v, cw + gap);   // the app's own logo scroll bar: arrows at the ends, hold the logo and drag
        }
    }
    float welScroll, welGrab, welGrabS; bool welDrag;
    // ---- the track list of the Edit menu: LOCK / DUPLICATE / DELETE show a tab of your tracks (TRACK 1, TRACK 2...), so you pick the one to work on ----
    bool tpOpen; int tpMode, tpAt; float tpScroll;
    // the toolbar buttons LOCK, FLEX and DEL: with one track they just do it, with several they ask which track (the tab of TRACK 1, 2, 3...)
    void TrackAction(int mode, Action direct) { int n = eng.Tracks.FindAll(tk => !tk.IsBus && (mode == 0 ? tk.Host == null : (tk.Clip != null && !tk.Instrument))).Count; if (n <= 1) direct(); else OpenTrackPicker(mode); }
    internal void OpenTrackPicker(int mode) { if (eng.Tracks.Count == 0) { Toast("Add a track first."); return; } if ((mode == 3 || mode == 4) && !eng.Tracks.Exists(tk => tk.Clip != null && !tk.Instrument && !tk.IsBus)) { Toast("No track has a recording yet."); return; } tpMode = mode; tpOpen = true; tpAt = Environment.TickCount; tpScroll = 0; Invalidate(); }
    void PickTrack(int i)
    {
        if (i < 0 || i >= eng.Tracks.Count) { tpOpen = false; return; } var t = eng.Tracks[i]; if (tpMode != 0) tpOpen = false;   // (LOCK keeps the tab open so several tracks can be locked or unlocked one after another)
        selTrack = i; if (tpMode == 0) ToggleLock(); else if (tpMode == 1) DuplicateTrack(t); else if (tpMode == 3) FlexOpen(); else if (tpMode == 4) DeleteClip(t); else RemoveTrack(t);
    }
    void PaintTrackPicker(Graphics g)
    {
        if (!tpOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); lastInput = DateTime.Now;
        using (var b = new SolidBrush(TM.C(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => tpOpen = false);
        var shownT = new List<int>(); for (int ti = 0; ti < eng.Tracks.Count; ti++) { var tk = eng.Tracks[ti]; if ((tpMode == 3 || tpMode == 4) && (tk.Clip == null || tk.Instrument || tk.IsBus)) continue; if (tpMode != 3 && tpMode != 4 && (tk.Host != null || (tpMode == 0 && tk.IsBus))) continue; shownT.Add(ti); }   // lock, duplicate and delete list the tracks you see on the left (not the pieces sharing a row), numbered the same
        int n = shownT.Count; float rowH = 46, bw = Math.Min(460, W - 40), bh = Math.Min(H - 60, 150 + n * rowH + 60); int age = unchecked(Environment.TickCount - tpAt); float k = Math.Min(1f, age / 220f), grow = 0.94f + 0.06f * (1 - (1 - k) * (1 - k)); double pulse = 0.5 + 0.5 * Math.Sin(age / 320.0);
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2, bw * grow, bh * grow); Color accent = tpMode == 2 || tpMode == 4 ? Color.FromArgb(255, 255, 90, 120) : NeonCyan;
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((16 + 22 * pulse) / i * 2.2f) + 5, accent), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 22 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 22)) { using (var br = new LinearGradientBrush(box, TM.C(250, 26, 12, 58), TM.C(250, 9, 4, 24), 90f)) g.FillPath(br, p); using (var pen = new Pen(accent, 2.2f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(110, NeonPink), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -9, -9), 16)) g.DrawPath(pen, p); Reg(box, () => { });
        Txt(g, tpMode == 0 ? "LOCK OR UNLOCK WHICH TRACK?" : (tpMode == 1 ? "DUPLICATE WHICH TRACK?" : (tpMode == 3 ? "FLEX PITCH ON WHICH TRACK?" : (tpMode == 4 ? "DELETE THE PIECE OF WHICH TRACK?" : "DELETE WHICH TRACK?"))), 20, Ink, Rc(box.X + 24, box.Y + 16, box.Width - 90, 32), StringAlignment.Near, true);
        Txt(g, tpMode == 0 ? "Locked tracks are gold with a padlock. Press DONE when finished." : "Pick the track by its number.", 12, DimC, Rc(box.X + 24, box.Y + 50, box.Width - 48, 20), StringAlignment.Near, false);
        var cl = Rc(box.Right - 46, box.Y + 14, 30, 28); Btn(g, cl, "x", 3, () => tpOpen = false, 12);
        var lr = Rc(box.X + 20, box.Y + 80, box.Width - 40, box.Bottom - box.Y - 80 - 64); float total = n * rowH, maxS = Math.Max(0, total - lr.Height); tpScroll = Math.Max(0, Math.Min(maxS, tpScroll));
        regs.Add(new HitReg { R = lr, OnWheel = d => tpScroll = Math.Max(0, Math.Min(maxS, tpScroll - d * 40)) });
        { var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
          for (int i = 0; i < n; i++)
          {
              int realI = shownT[i]; var t = eng.Tracks[realI]; var row = Rc(lr.X, lr.Y + i * rowH - tpScroll, lr.Width - (maxS > 0 ? 22 : 0), rowH - 6); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue; bool sel = realI == selTrack, hot = Hot(row); int idx = realI;
              bool lockedRow = tpMode == 0 && t.Locked; Color amber = Color.FromArgb(255, 255, 196, 80);
              FillRR(g, lockedRow ? TM.C(hot ? 235 : 215, 120, 82, 14) : (hot ? TM.C(225, 90, 50, 170) : (sel ? TM.C(210, 62, 34, 130) : TM.C(170, 28, 15, 56))), row, 10); StrokeRR(g, lockedRow ? amber : (sel ? accent : Color.FromArgb(hot ? 220 : 90, NeonViolet)), row, 10, lockedRow ? 2f : (sel ? 1.4f : 1f)); Reg(row, () => PickTrack(idx));
              if (lockedRow) { var pl = Rc(row.Right - 150, row.Y + row.Height / 2 - 12, 20, 24); var smp = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(amber, 2.4f)) g.DrawArc(pen, pl.X + 3, pl.Y, pl.Width - 6, 16, 180, 180); using (var br = new SolidBrush(amber)) g.FillPath(br, RR(Rc(pl.X, pl.Y + 9, pl.Width, 15), 3)); using (var br = new SolidBrush(Color.FromArgb(255, 60, 36, 8))) g.FillEllipse(br, pl.X + pl.Width / 2 - 2.2f, pl.Y + 14, 4.4f, 4.4f); g.SmoothingMode = smp; }
              Txt(g, t.IsBus ? "BUS" : "TRACK " + (eng.Tracks.IndexOf(t.Host ?? t) + 1), 15, accent, Rc(row.X + 14, row.Y, 96, row.Height), StringAlignment.Near, true);   // the number of its row, as on the left
              Txt(g, t.Name, 14, Ink, Rc(row.X + 112, row.Y, row.Width - 112 - (lockedRow ? 160 : 90), row.Height), StringAlignment.Near, true);   // (it stops before the padlock)
              string tag = t.Locked ? "LOCKED" : (t.Mute ? "MUTED" : ""); if (tag.Length > 0) Txt(g, tag, t.Locked ? 12 : 10, t.Locked ? Color.FromArgb(255, 255, 200, 90) : DimC, Rc(row.Right - 122, row.Y, 112, row.Height), StringAlignment.Far, true);
          }
          g.Restore(gs); }
        if (maxS > 0) VScroll(g, Rc(lr.Right - 20, lr.Y, 18, lr.Height), total, lr.Height, () => tpScroll, v => tpScroll = v);
        var cb = Rc(box.Right - 24 - 130, box.Bottom - 54, 130, 36); Btn(g, cb, tpMode == 0 ? "DONE" : "CANCEL", tpMode == 0 ? 2 : 1, () => tpOpen = false, 13);
    }
    // the question when opening something would replace the project that is open now
    string OverwriteText(string name)
    {
        return "OPENING \"" + name + "\" WILL OVERWRITE THE PROJECT THAT IS OPEN NOW." + "\n\n" + (HasUnsaved ? "It has changes that are not saved: they will be lost (press No, then SAVE, if you want to keep them)." + "\n\n" : "It is closed and replaced by this one (it stays in My Projects if you saved it)." + "\n\n") + "Confirm?";
    }
    internal Func<string, string> nameHook;   // a test answers for itself (the name of the project)
    // the project has something that is not saved (it was never saved, or it was changed since)
    internal bool HasUnsaved { get { return eng.Tracks.Count > 0 && (string.IsNullOrEmpty(projPath) || editCount != savedEdit); } }
    // only a click on the X (or Alt+F4, or Close in the window menu) asks: the program closing the window itself (it has asked already) or a test does not
    bool userX;
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool fromX = userX; userX = false;
        if (!leaving && fromX && e.CloseReason == CloseReason.UserClosing && !AskLeave()) { e.Cancel = true; return; }
        base.OnFormClosing(e); if (!e.Cancel) SilenceAll();
    }
    // the DAW window closes (any way: the X, EXIT, the taskbar, the main window): nothing it started keeps playing. The engine belongs to the main window, so without this the mix went on playing
    internal void SilenceAll()
    {
        try { if (mic != null) StopMic(); } catch { } try { if (eng.Playing) eng.Pause(); } catch { } try { StopPreview(); } catch { } try { Auditioner.Stop(); } catch { }
    }
    internal void NoteUserX() { userX = true; }
    // leaving the DAW (the X, after the questions were answered) resets it: the next time it opens it is empty and the welcome box offers your projects again, like the first time
    protected override void OnFormClosed(FormClosedEventArgs e) { base.OnFormClosed(e); SilenceAll(); if (leaving) ResetAfterLeave(); }
    internal void ResetAfterLeave()
    {
        try
        {
            if (mic != null) StopMic(); eng.Stop(); FlexCommitIfOpen();
            lock (eng.Sync) { foreach (var t in eng.Tracks) retired.AddRange(t.Fx); eng.Tracks.Clear(); }
            foreach (var f in retired) { var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }
            bpmUserSet = false; bpmAutoSet = false; ProjectIO.Lyrics = ""; ProjectIO.LyricsKept = ""; ProjectIO.LyricsTitle = ""; ProjectIO.CoverBytes = null; undo.Clear(); redo.Clear(); revertAction = null;
            projPath = ""; projName = "Untitled project"; selTrack = 0; selFx = -1; scroll = 0; editCount = 0; savedEdit = 0; welcomeOn = true; tab = 0;
        }
        catch { }
    }
    protected override void OnHandleDestroyed(EventArgs e) { EmojiSprites.Ready -= EmojiReady; base.OnHandleDestroyed(e); }
    void EmojiReady() { try { lastInput = DateTime.Now; Invalidate(); } catch { } }
    bool AskLeave()
    {
        Func<string, MessageBoxButtons, DialogResult> ask = (t, b) => exitAsk != null ? exitAsk(t) : MessageBox.Show(this, t, "Leaving the DAW", b, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (ask("YOU ARE LEAVING THE DAW.\n\nThis closes Purphex Music (the DAW). Whatever is playing stops.\n\nLeave?", MessageBoxButtons.YesNo) != DialogResult.Yes) return false;
        if (HasUnsaved)
        {
            var r = ask("Your project is not saved.\n\nSave it before you leave? You can give it a name, and you will find it in My Projects.\n\nYes: save it.\nNo: leave without saving.\nCancel: stay in the DAW.", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Cancel) return false; if (r == DialogResult.Yes && !QuickSaveNamed()) return false;
        }
        if (ProjectIO.Lyrics.Trim().Length > 0 && ProjectIO.Lyrics != ProjectIO.LyricsKept)
        {
            var r = ask("YOU HAVE UNSAVED LYRICS.\n\nSave them as a text file (.txt) before you leave?\n\nYes: save them.\nNo: leave without saving them.\nCancel: stay in the DAW.", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Cancel) return false; if (r == DialogResult.Yes && !SaveLyricsTxt()) return false;
        }
        leaving = true; return true;
    }
    // saves the project (asking for a name when it has none) into the projects folder, so it shows up in My Projects; false = it was not saved
    internal bool QuickSaveNamed()
    {
        if (eng.Tracks.Count == 0) return true; FlexCommitIfOpen(); string path = projPath;
        if (string.IsNullOrEmpty(path))
        {
            string def = projName == "Untitled project" ? "My song" : projName; string nm = nameHook != null ? nameHook(def) : Ask("Name your project (you will find it in My Projects):", def); if (nm == null) return false;
            foreach (char c in Path.GetInvalidFileNameChars()) nm = nm.Replace(c, ' '); nm = nm.Trim(); if (nm.Length == 0) return false;
            try { Directory.CreateDirectory(ProjectLibrary.DefaultFolder); } catch { }
            path = Path.Combine(ProjectLibrary.DefaultFolder, nm + ".ytmproj"); int n = 2; while (File.Exists(path)) path = Path.Combine(ProjectLibrary.DefaultFolder, nm + " " + (n++) + ".ytmproj");
        }
        try { ProjectIO.Roll = PrPrefsText(); ProjectIO.Save(eng, path); projPath = path; projName = Path.GetFileNameWithoutExtension(path); NoteProject(path, true); Toast("Project saved: " + projName + " (in My Projects)"); return true; }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the project: " + ex.Message, "Save"); return false; }
    }
}
