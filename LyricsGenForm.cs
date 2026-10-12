using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// The GENERATE LYRICS window: its own page, opened from the converter. Add any song or video, press GENERATE LYRICS, tidy the words, resize them to fit, copy / save them
// or keep them in the DAW under a title (the Lyrics page of Purphex Music lists them under RECENT). Free in every edition; on the Fantasy edition it wears the Fantasy gold and follows the Fantasy theme.
sealed class LyricsDropZone : Panel
{
    public string Path = ""; public bool Over, Gold; public string Hint = "";
    public LyricsDropZone() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = TM.C(255, 14, 7, 30); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        var rc = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f); Color edge = Gold ? Color.FromArgb(255, 255, 206, 110) : (Over ? TM.C(255, 0, 229, 255) : TM.C(255, 150, 100, 255));
        using (var p = Round(rc, 14)) { using (var br = new SolidBrush(Over ? TM.C(255, 26, 14, 56) : BackColor)) g.FillPath(br, p); for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((Over ? 36 : 20) + i * 4, edge), 1f + i * 2f)) g.DrawPath(pen, p); using (var pen = new Pen(edge, 1.6f) { DashStyle = Path.Length == 0 ? DashStyle.Dash : DashStyle.Solid }) g.DrawPath(pen, p); }
        int right = Width - 200; var area = new Rectangle(22, 12, Math.Max(40, right - 22), Height - 24); var ink = TM.C(255, 240, 232, 255); var dim = TM.C(255, 198, 182, 232);
        var fl = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        using (var f1 = Win.UiFont(13f, true)) using (var f2 = Win.UiFont(10f, false))
        {
            string l1 = Path.Length == 0 ? "DROP A SONG OR VIDEO HERE" : System.IO.Path.GetFileName(Path), l2 = Path.Length == 0 ? "mp3, wav, flac, m4a, ogg, opus, aac, mp4, mkv, webm, mov and more" : Hint;
            TextRenderer.DrawText(g, l1, f1, new Rectangle(area.X, area.Y, area.Width, area.Height / 2 + 2), ink, fl); TextRenderer.DrawText(g, l2, f2, new Rectangle(area.X, area.Y + area.Height / 2, area.Width, area.Height / 2 - 2), dim, fl);
        }
    }
    static GraphicsPath Round(RectangleF r, float rad) { var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
}

sealed class LyricsGenForm : Form
{
    readonly App app; static readonly Color Ink = TM.C(255, 240, 232, 255), Dim = TM.C(255, 198, 182, 232), Head = TM.C(255, 236, 120, 230); static readonly Color Gold = Color.FromArgb(255, 255, 206, 110);
    readonly bool fantasy;
    Label linkLbl, trimALbl, trimBLbl, trimNote; TextBox linkBox, trimA, trimB; bool linkSet; PxButton recheckBtn, removeBtn; string countBase = ""; System.Windows.Forms.Timer moreTimer;
    public LyricsCheck.Report LastReport;   // the report of the last RECHECK
    public static Func<LyricsCheck.Report, bool> ReportHook;   // a test answers here (true = add the missing passages)
    Label titleLbl, subLbl, badge, statusLbl, tLbl, rLbl, sizeLbl, countLbl, noteLbl; LyricsDropZone zone; PxButton chooseBtn, genBtn, cancelBtn, minusBtn, plusBtn, fitBtn, fixBtn, secBtn, undoBtn, copyBtn, saveBtn, dawBtn, recentBtn;
    DarkCheck listenChk; TextBox titleBox, lyricsBox; string path = "", source = "", prevText; float fontSize = 14f; bool fit = true, busy, setting; volatile bool cancel; DateTime startedAt; System.Windows.Forms.Timer tick, fitLater; string baseStatus = "";
    public static Func<string, bool> Consent;   // a test answers the download question here
    public static Func<string, string> PickSave;

    public LyricsGenForm(App app)
    {
        this.app = app; fantasy = Edition.Current == Tier.Fantasy;
        Text = "Generate Lyrics"; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; ShowInTaskbar = true; StartPosition = FormStartPosition.CenterScreen; BackColor = TM.C(255, 10, 4, 20); ForeColor = Ink; Font = new Font("Segoe UI", 10f);
        Win.Dark(this); DoubleBuffered = true; ClientSize = new Size(1000, Math.Max(780, Math.Min(940, Screen.PrimaryScreen.WorkingArea.Height - 110))); MinimumSize = SizeFromClientSize(new Size(860, 780)); AllowDrop = true;
        DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; zone.Over = true; zone.Invalidate(); } }; DragLeave += (s, e) => { zone.Over = false; zone.Invalidate(); };
        DragDrop += (s, e) => { zone.Over = false; var fs = e.Data.GetData(DataFormats.FileDrop) as string[]; if (fs != null && fs.Length > 0) LoadFile(fs[0]); };
        titleLbl = new Label { Text = "Generate Lyrics", Font = new Font("Segoe UI Semibold", 20f), ForeColor = Ink, AutoSize = false, Tag = "nocaps" };
        subLbl = new Label { Text = "Add any song or video, or paste a link, and get its lyrics tidied and labelled. Free in every edition.", ForeColor = Dim, AutoSize = false, Tag = "nocaps" };
        badge = new Label { Text = fantasy ? "FANTASY EDITION  -  " + Themes.Current.Name + " theme" : "FREE FOR EVERYONE", TextAlign = ContentAlignment.MiddleRight, ForeColor = fantasy ? Gold : Dim, Font = new Font("Segoe UI Semibold", 10f), AutoSize = false, Tag = "nocaps" };
        zone = new LyricsDropZone { Gold = fantasy, AllowDrop = true }; zone.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; zone.Over = true; zone.Invalidate(); } }; zone.DragLeave += (s, e) => { zone.Over = false; zone.Invalidate(); };
        zone.DragDrop += (s, e) => { zone.Over = false; var fs = e.Data.GetData(DataFormats.FileDrop) as string[]; if (fs != null && fs.Length > 0) LoadFile(fs[0]); };
        chooseBtn = B("CHOOSE FILE...", TM.C(255, 140, 60, 220), 160, 40); chooseBtn.Click += (s, e) => Choose(); zone.Controls.Add(chooseBtn);
        linkLbl = new Label { Text = "OR PASTE A LINK", ForeColor = Ink, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "nocaps" };
        linkBox = new TextBox { Font = new Font("Segoe UI", 11.5f) }; Win.NeonBox(linkBox);
        linkBox.TextChanged += (s, e) => { if (linkSet) return; if (linkBox.Text.Trim().Length > 0 && path.Length > 0) { path = ""; zone.Path = ""; zone.Hint = ""; zone.Invalidate(); } if (linkBox.Text.Trim().Length > 0) Say(LyricsGen.IsLink(linkBox.Text) ? "Link ready: press GENERATE LYRICS (nothing is downloaded)." : "That is not a link yet (it starts with https://)."); };
        linkBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Generate(); } };
        trimALbl = new Label { Text = "TRIM START", ForeColor = Ink, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "nocaps" }; trimBLbl = new Label { Text = "TRIM END", ForeColor = Ink, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "nocaps" };
        trimA = new TextBox { Font = new Font("Segoe UI", 11.5f), MaxLength = 12, TextAlign = HorizontalAlignment.Center }; trimB = new TextBox { Font = new Font("Segoe UI", 11.5f), MaxLength = 12, TextAlign = HorizontalAlignment.Center }; Win.NeonBox(trimA); Win.NeonBox(trimB);
        trimNote = new Label { Text = "Optional, like 0:30: used when the words are written by listening.", ForeColor = Dim, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "nocaps" };
        foreach (var tb in new[] { trimA, trimB }) tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Generate(); } };
        listenChk = new DarkCheck { Text = "If it is not in the lyrics database, listen to the song and write the words (one-time download)", Checked = true, ForeColor = Ink, AutoSize = false, Tag = "nocaps" };
        genBtn = B("Generate lyrics", TM.C(255, 150, 70, 235), 230, 42); genBtn.Font = new Font("Segoe UI Semibold", 12f); genBtn.Click += (s, e) => Generate(); cancelBtn = B("Cancel", TM.C(255, 110, 30, 70), 100, 42); cancelBtn.Click += (s, e) => { cancel = true; LyricsGen.Cancel(); LyricsWhisper.Cancel(); }; cancelBtn.Visible = false;
        statusLbl = new Label { ForeColor = Dim, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "nocaps" };
        tLbl = new Label { Text = "TITLE", ForeColor = Ink, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "nocaps" };
        titleBox = new TextBox { Font = new Font("Segoe UI", 12f), MaxLength = 120 }; Win.NeonBox(titleBox);
        rLbl = new Label { Text = "RESIZE LYRICS", ForeColor = Head, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "nocaps" };
        minusBtn = B("A-", TM.C(255, 90, 50, 170), 40, 30); minusBtn.Click += (s, e) => Bump(-1f); plusBtn = B("A+", TM.C(255, 90, 50, 170), 40, 30); plusBtn.Click += (s, e) => Bump(1f);
        fitBtn = B("Fit to box: on", TM.C(255, 120, 60, 200), 140, 30); fitBtn.Click += (s, e) => { fit = !fit; UpdateFitBtn(); if (fit) ApplyFit(); };
        fixBtn = B("Fix grammar", TM.C(255, 140, 60, 220), 130, 30); fixBtn.Click += (s, e) => FixGrammar(); secBtn = B("Add sections", TM.C(255, 140, 60, 220), 130, 30); secBtn.Click += (s, e) => AddSections();
        removeBtn = B("Remove lyrics", TM.C(255, 110, 30, 70), 150, 30); removeBtn.Click += (s, e) => RemoveLyrics();
        recheckBtn = B("Recheck video + words", TM.C(255, 120, 60, 200), 250, 42); recheckBtn.Click += (s, e) => Recheck();
        undoBtn = B("Undo", TM.C(255, 70, 40, 120), 76, 30); undoBtn.Click += (s, e) => Undo(); undoBtn.Visible = false;
        sizeLbl = new Label { ForeColor = Dim, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "nocaps" };
        lyricsBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, WordWrap = true, Font = new Font("Segoe UI", fontSize) }; Win.NeonBox(lyricsBox); Win.DarkControl(lyricsBox);
        lyricsBox.TextChanged += (s, e) => { if (setting) return; UpdateCounts(); if (fit) { fitLater.Stop(); fitLater.Start(); } }; titleBox.TextChanged += (s, e) => UpdateButtons();
        noteLbl = new Label { ForeColor = Dim, AutoSize = false, Tag = "nocaps" }; countLbl = new Label { ForeColor = Dim, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "nocaps" };
        copyBtn = B("Copy", TM.C(255, 90, 50, 170), 80, 36); copyBtn.Click += (s, e) => { if (lyricsBox.Text.Trim().Length == 0) { Say("There are no lyrics to copy yet."); return; } try { Clipboard.SetText(lyricsBox.Text); Say("Lyrics copied."); } catch { Say("Could not copy (another program is using the clipboard)."); } };
        saveBtn = B("Save .txt", TM.C(255, 90, 50, 170), 100, 36); saveBtn.Click += (s, e) => SaveTxt();
        dawBtn = B("Save to the DAW", TM.C(255, 150, 70, 235), 170, 36); dawBtn.Click += (s, e) => SaveToDaw();
        recentBtn = B("Recent", TM.C(255, 120, 60, 200), 110, 36); recentBtn.Click += (s, e) => ShowRecent();
        Controls.AddRange(new Control[] { titleLbl, subLbl, badge, zone, linkLbl, linkBox, trimALbl, trimA, trimBLbl, trimB, trimNote, listenChk, genBtn, recheckBtn, removeBtn, cancelBtn, statusLbl, tLbl, titleBox, rLbl, minusBtn, plusBtn, fitBtn, fixBtn, secBtn, undoBtn, sizeLbl, lyricsBox, noteLbl, countLbl, copyBtn, saveBtn, dawBtn, recentBtn });
        moreTimer = new System.Windows.Forms.Timer { Interval = 500 }; moreTimer.Tick += (s, e) => MoreHint(); moreTimer.Start();
        tick = new System.Windows.Forms.Timer { Interval = 1000 }; tick.Tick += (s, e) => { if (busy) { var d = DateTime.Now - startedAt; statusLbl.Text = baseStatus + "  (" + (int)d.TotalMinutes + ":" + d.Seconds.ToString("00") + ")"; } };
        fitLater = new System.Windows.Forms.Timer { Interval = 140 }; fitLater.Tick += (s, e) => { fitLater.Stop(); ApplyFit(); };
        Win.CapsAll(this); Resize += (s, e) => { DoLayout(); if (fit) { fitLater.Stop(); fitLater.Start(); } }; DoLayout(); UpdateFitBtn(); UpdateButtons(); UpdateCounts();
        statusLbl.Text = "Add a song or video, then press GENERATE LYRICS."; FormClosed += (s, e) => { cancel = true; LyricsGen.Cancel(); LyricsWhisper.Cancel(); tick.Dispose(); fitLater.Dispose(); moreTimer.Dispose(); };
        Shown += (s, e) => ApplyFit();
    }

    PxButton B(string text, Color c, int w, int h) { var b = new PxButton { Text = text, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = fantasy ? Gold : TM.C(255, 200, 150, 255); return b; }

    void DoLayout()
    {
        int W = ClientSize.Width, H = ClientSize.Height, m = 24, iw = W - 2 * m;
        titleLbl.SetBounds(m, 14, Math.Max(200, iw - 330), 40); badge.SetBounds(W - m - 320, 22, 320, 24); subLbl.SetBounds(m, 56, iw, 24);
        zone.SetBounds(m, 90, iw, 84); chooseBtn.Location = new Point(zone.Width - 180, (zone.Height - chooseBtn.Height) / 2);
        linkLbl.SetBounds(m, 184, 150, 30); linkBox.SetBounds(m + 158, 186, iw - 158 - 4, 28);
        trimALbl.SetBounds(m, 222, 104, 30); trimA.SetBounds(m + 108, 224, 84, 28); trimBLbl.SetBounds(m + 214, 222, 84, 30); trimB.SetBounds(m + 302, 224, 84, 28); trimNote.SetBounds(m + 404, 222, Math.Max(60, iw - 404), 30);
        listenChk.SetBounds(m, 260, iw, 26); genBtn.SetBounds(m, 294, 230, 42); recheckBtn.SetBounds(m + 240, 294, 250, 42); cancelBtn.SetBounds(m + 500, 294, 100, 42); statusLbl.SetBounds(m + (cancelBtn.Visible ? 612 : 502), 294, Math.Max(100, iw - (cancelBtn.Visible ? 612 : 502)), 42);
        tLbl.SetBounds(m, 354, 56, 30); titleBox.SetBounds(m + 64, 356, iw - 64 - 4, 28);
        int y = 396; rLbl.SetBounds(m, y, 124, 30); minusBtn.Location = new Point(m + 130, y); plusBtn.Location = new Point(m + 176, y); fitBtn.Location = new Point(m + 224, y); sizeLbl.SetBounds(m + 372, y, 70, 30);
        fixBtn.Location = new Point(W - m - 130 - 8 - 130 - (undoBtn.Visible ? 84 : 0), y); secBtn.Location = new Point(W - m - 130 - (undoBtn.Visible ? 84 : 0), y); undoBtn.Location = new Point(W - m - 76, y); removeBtn.Location = new Point(fixBtn.Left - 8 - 150, y);
        int by = H - 24 - 36, ny = by - 46;
        lyricsBox.SetBounds(m + 4, 438, iw - 8, Math.Max(80, ny - 8 - 438 - 4)); noteLbl.SetBounds(m, ny, iw, 40);
        recentBtn.Location = new Point(W - m - 110, by); dawBtn.Location = new Point(W - m - 110 - 8 - 170, by); saveBtn.Location = new Point(W - m - 110 - 8 - 170 - 8 - 100, by); copyBtn.Location = new Point(W - m - 110 - 8 - 170 - 8 - 100 - 8 - 80, by);
        countLbl.SetBounds(m, by, Math.Max(100, copyBtn.Left - m - 12), 36); Invalidate();
    }

    // ---- the file ----
    void Choose() { using (var d = new OpenFileDialog { Title = "Choose a song or video", Filter = LyricsGen.DialogFilter }) if (d.ShowDialog(this) == DialogResult.OK) LoadFile(d.FileName); }
    public void LoadFile(string p)
    {
        if (busy) { Say("Wait for the lyrics that are being made, or press Cancel."); return; }
        if (!File.Exists(p)) { Say("That file is not there."); return; }
        if (!LyricsGen.IsMedia(p)) { Say("That is not a song or a video I know (" + Path.GetExtension(p) + "). Use mp3, wav, flac, m4a, ogg, mp4, mkv, webm, mov and similar."); return; }
        path = p; linkSet = true; linkBox.Text = ""; linkSet = false; long len = 0; try { len = new FileInfo(p).Length; } catch { } string ext = Path.GetExtension(p).TrimStart('.').ToUpperInvariant(); bool video = Array.IndexOf(new[] { "MP4", "M4V", "MKV", "WEBM", "MOV", "AVI", "WMV", "FLV", "MPG", "MPEG", "3GP", "TS" }, ext) >= 0;
        zone.Path = p; zone.Hint = ext + (video ? " video" : " song") + "  -  " + (len / 1048576.0).ToString("0.0") + " MB  -  press GENERATE LYRICS"; zone.Invalidate(); string a, t; LyricsGet.CleanName(Path.GetFileNameWithoutExtension(p), out a, out t); setting = true; titleBox.Text = (a.Length > 0 ? a + " - " : "") + t; setting = false;
        Say("Ready: " + Path.GetFileName(p)); UpdateButtons();
    }

    // ---- making the lyrics ----
    void UI(Action a) { if (IsDisposed || !IsHandleCreated) return; try { BeginInvoke(a); } catch { } }
    void Say(string s) { baseStatus = s; statusLbl.Text = s; }
    public bool Busy { get { return busy; } }
    public string LyricsText { get { return lyricsBox.Text; } }
    public string TrimStartText { get { return trimA.Text; } set { trimA.Text = value; } }
    public string TrimEndText { get { return trimB.Text; } set { trimB.Text = value; } }
    public string LinkText { get { return linkBox.Text; } set { linkBox.Text = value; } }
    public string TitleText { get { return titleBox.Text; } set { titleBox.Text = value; } }
    public string StatusText { get { return statusLbl.Text; } }
    public string NoteText { get { return noteLbl.Text; } }
    public float FontPoints { get { return fontSize; } }
    public bool FitOn { get { return fit; } }
    public Thread Worker { get { return worker; } }
    Thread worker;
    public void SetBusy(bool b) { busy = b; recheckBtn.Enabled = !b; genBtn.Enabled = !b; chooseBtn.Enabled = !b; cancelBtn.Visible = b; if (b) startedAt = DateTime.Now; tick.Enabled = b; DoLayout(); UpdateButtons(); }
    public void Generate()
    {
        if (busy) return; string file = path.Length > 0 ? path : linkBox.Text.Trim();
        if (file.Length == 0) { Say("Add a song or video first (Choose file, or drop it here), or paste a link."); return; }
        if (path.Length == 0 && !LyricsGen.IsLink(file)) { Say("That is not a link. Paste the whole address, starting with https://"); return; }
        double ta, tb; string terr = LyricsGen.TrimError(trimA.Text, trimB.Text, out ta, out tb); if (terr.Length > 0) { Say(terr); return; }
        cancel = false; SetBusy(true); Say("Starting..."); bool listen = listenChk.Checked;
        worker = new Thread(() =>
        {
            var res = LyricsGen.Generate(file, listen, s => UI(() => Say(s)), () => cancel, ta, tb);
            if (res.NeedSetup && !cancel)
            {
                bool yes = false; string q = res.Note + "\n\nPurphex Music will download the speech recogniser and its model (" + LyricsWhisper.DownloadSizeText + ", once; ffmpeg too if it is not installed). They run on this PC: your song is not uploaded anywhere.\n\nDownload now?";
                var ev = new ManualResetEvent(false); UI(() => { try { yes = Consent != null ? Consent(q) : MessageBox.Show(this, q, "Generate lyrics", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; } finally { ev.Set(); } }); ev.WaitOne();
                if (yes)
                {
                    try { LyricsWhisper.Install((what, pct) => UI(() => Say(what + "... " + pct + "%")), () => cancel); res = LyricsGen.Generate(file, listen, s => UI(() => Say(s)), () => cancel, ta, tb); }
                    catch (OperationCanceledException) { res = new LyricsGen.Result { Error = "Cancelled." }; }
                    catch (Exception ex) { res = new LyricsGen.Result { Error = "The download did not work: " + ex.Message }; }
                }
                else res = new LyricsGen.Result { Error = "Not downloaded, so no lyrics for this song. (Songs that are in the lyrics database need no download.)" };
            }
            UI(() => Done(res));
        }) { IsBackground = true }; worker.Start();
    }
    void Done(LyricsGen.Result r)
    {
        SetBusy(false);
        if (!r.Found) { Say(r.Error.Length > 0 ? r.Error : "No lyrics were made."); noteLbl.Text = ""; return; }
        source = r.Source; prevText = null; undoBtn.Visible = false; setting = true; lyricsBox.Text = r.Lyrics.Replace("\n", "\r\n"); titleBox.Text = r.DefaultTitle; setting = false; lyricsBox.SelectionStart = 0; lyricsBox.ScrollToCaret();
        noteLbl.Text = r.Source + ". " + r.Note; Say("Done: lyrics for \"" + r.DefaultTitle + "\" are ready."); UpdateCounts(); UpdateButtons(); ApplyFit(); DoLayout();
    }

    // ---- the words ----
    static string Crlf(string s) { return (s ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"); }
    static string Lf(string s) { return (s ?? "").Replace("\r\n", "\n"); }
    string prevTitle;
    void Keep() { prevText = lyricsBox.Text; prevTitle = titleBox.Text; undoBtn.Visible = true; DoLayout(); }
    void Undo() { if (prevText == null) return; string cur = lyricsBox.Text, curT = titleBox.Text; lyricsBox.Text = prevText; prevText = cur; if (prevTitle != null) { titleBox.Text = prevTitle; prevTitle = curT; } Say("Undone. Press Undo again to bring the change back."); }
    public void FixGrammar()
    {
        if (lyricsBox.Text.Trim().Length == 0) { Say("There are no lyrics to fix yet."); return; }
        string n; string s = LyricsPolish.Polish(Lf(lyricsBox.Text), false, out n); Keep(); lyricsBox.Text = Crlf(s); noteLbl.Text = n; Say("Grammar and punctuation fixed. Undo brings your words back.");
    }
    public void AddSections()
    {
        if (lyricsBox.Text.Trim().Length == 0) { Say("There are no lyrics to label yet."); return; }
        int added; string note; string s = LyricSections.Add(Lf(lyricsBox.Text), out added, out note); Keep(); lyricsBox.Text = Crlf(s); noteLbl.Text = note.Replace(" Ctrl+Z undoes it.", ""); Say(added > 0 ? "Labels added. Undo brings the old text back." : note.Replace(" Ctrl+Z undoes it.", ""));
    }

    // ---- resize lyrics / fit to the box ----
    public static float FitSize(string text, Size box, string face, float lo, float hi)
    {
        if (box.Width < 40 || box.Height < 24) return lo; string t = string.IsNullOrEmpty(text) ? " " : text.Replace("\r\n", "\n").Replace("\n", "\r\n"); float a = lo, b = hi, best = lo;
        for (int i = 0; i < 9; i++)
        {
            float mid = (a + b) / 2f; using (var f = new Font(face, mid)) { var sz = TextRenderer.MeasureText(t, f, new Size(box.Width, 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding); if (sz.Height * 1.04f <= box.Height) { best = mid; a = mid; } else b = mid; }
        }
        return (float)Math.Floor(best * 2f) / 2f;
    }
    void SetFont(float pts) { fontSize = pts; var old = lyricsBox.Font; lyricsBox.Font = new Font("Segoe UI", pts); if (old != null && old != Font) old.Dispose(); sizeLbl.Text = "Size " + (pts == Math.Floor(pts) ? ((int)pts).ToString() : pts.ToString("0.0")); }
    void ApplyFit()
    {
        if (!fit || IsDisposed) return; var cs = lyricsBox.ClientSize; float p = lyricsBox.Text.Trim().Length == 0 ? 14f : FitSize(lyricsBox.Text, new Size(cs.Width - 10, cs.Height - 8), "Segoe UI", 8f, 28f); if (Math.Abs(p - fontSize) > 0.01f || lyricsBox.Font.Size != p) SetFont(p);
    }
    void Bump(float d) { fit = false; UpdateFitBtn(); SetFont(Math.Max(8f, Math.Min(48f, fontSize + d))); }
    void UpdateFitBtn() { fitBtn.Text = fit ? "FIT TO BOX: ON" : "FIT TO BOX: OFF"; fitBtn.Invalidate(); }

    // the box shows only part of a long lyric: say so, so a verse below the edge is never mistaken for a verse that is missing
    public int HiddenLines
    {
        get
        {
            if (lyricsBox == null || !lyricsBox.IsHandleCreated || lyricsBox.TextLength == 0) return 0; int total = lyricsBox.GetLineFromCharIndex(lyricsBox.TextLength) + 1; int last = lyricsBox.GetLineFromCharIndex(lyricsBox.GetCharIndexFromPosition(new Point(4, Math.Max(1, lyricsBox.ClientSize.Height - 2)))) + 1;
            int hidden = Math.Max(0, total - last); return hidden <= 1 ? 0 : hidden;
        }
    }
    void MoreHint() { if (countLbl == null || IsDisposed) return; int h = HiddenLines; string t = h > 0 ? countBase + "   -   " + h + " more lines below: scroll" : countBase; if (countLbl.Text != t) countLbl.Text = t; }

    // REMOVE LYRICS: the generated lyrics, the title and the notes are taken off the page (Undo brings the words back; the list kept in the DAW is not touched)
    public void RemoveLyrics()
    {
        if (busy) return; if (lyricsBox.Text.Trim().Length == 0 && titleBox.Text.Trim().Length == 0) { Say("There are no lyrics on the page to remove."); return; }
        Keep(); string was = titleBox.Text.Trim(); lyricsBox.Text = ""; titleBox.Text = ""; noteLbl.Text = ""; source = ""; LastReport = null; SetFont(14f); UpdateCounts();
        Say("Lyrics removed" + (was.Length > 0 ? " (" + was + ")" : "") + ". Undo brings them back. Anything you saved to the DAW is still in RECENT.");
    }

    // ---- RECHECK VIDEO + WORDS: listen to the file again and compare what is sung with the lyrics in the box ----
    public void Recheck()
    {
        if (busy) return; string lyr = Lf(lyricsBox.Text).Trim();
        if (lyr.Length == 0) { Say("There are no lyrics to check yet: generate or paste them first."); return; }
        if (path.Length == 0) { Say(LyricsGen.IsLink(linkBox.Text) ? "To recheck the words against the sound, add the song or video FILE: a link cannot be listened to." : "Add the song or video file first (Choose file, or drop it here): the recheck listens to it."); return; }
        double ta, tb; string terr = LyricsGen.TrimError(trimA.Text, trimB.Text, out ta, out tb); if (terr.Length > 0) { Say(terr); return; }
        cancel = false; SetBusy(true); Say("Starting the recheck..."); string file = path;
        worker = new Thread(() =>
        {
            string heard = null, err = "";
            try
            {
                if (!LyricsWhisper.Ready)
                {
                    bool yes = false; string q = "To recheck the words, Purphex has to listen to the song, which needs the speech recogniser and its model (" + LyricsWhisper.DownloadSizeText + ", once; ffmpeg too if it is not installed). They run on this PC: your song is not uploaded anywhere.\n\nDownload now?";
                    var ev = new ManualResetEvent(false); UI(() => { try { yes = Consent != null ? Consent(q) : MessageBox.Show(this, q, "Recheck video and words", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; } finally { ev.Set(); } }); ev.WaitOne();
                    if (!yes) err = "Not downloaded, so the recheck could not listen to the song."; else LyricsWhisper.Install((what, pct) => UI(() => Say(what + "... " + pct + "%")), () => cancel);
                }
                if (err.Length == 0) heard = LyricsWhisper.Transcribe(file, s => UI(() => Say(s)), () => cancel, ta, tb);
                if (cancel) err = "Cancelled.";
            }
            catch (OperationCanceledException) { err = "Cancelled."; } catch (Exception ex) { err = ex.Message; }
            UI(() => RecheckDone(lyr, heard, err));
        }) { IsBackground = true }; worker.Start();
    }
    void RecheckDone(string lyr, string heard, string err)
    {
        SetBusy(false); if (err.Length > 0) { Say(err); return; }
        if ((heard ?? "").Trim().Length < 12) { Say("No words could be heard in this file, so the lyrics could not be checked against it."); return; }
        var rep = LyricsCheck.Compare(lyr, heard); LastReport = rep; noteLbl.Text = rep.Summary; Say(rep.Clean ? "Rechecked: the lyrics and the song agree." : "Rechecked: " + rep.NotHeard.Count + " line(s) not heard, " + rep.Passages.Count + " passage(s) missing.");
        bool add = ReportHook != null ? ReportHook(rep) : ShowReport(rep);
        if (add && rep.Passages.Count > 0) AddPassages(lyr, rep);
    }
    void AddPassages(string lyr, LyricsCheck.Report rep)
    {
        string pn; string merged = LyricsPolish.Polish(LyricsCheck.Merge(lyr, rep), false, out pn); Keep(); lyricsBox.Text = Crlf(merged);
        Say("Added " + rep.Passages.Count + (rep.Passages.Count == 1 ? " passage" : " passages") + " that is sung but was missing, marked [Heard in the song: check these words]. Undo takes them out again.");
    }
    bool ShowReport(LyricsCheck.Report rep)
    {
        using (var f = new Form { Text = "Recheck video and words", FormBorderStyle = FormBorderStyle.Sizable, MinimumSize = new Size(560, 420), StartPosition = FormStartPosition.CenterParent, BackColor = TM.C(255, 10, 4, 20), ForeColor = Ink, Font = new Font("Segoe UI", 10f), ShowInTaskbar = false })
        {
            Win.Dark(f); f.ClientSize = new Size(760, 560);
            var head = new Label { Text = rep.Clean ? "Everything agrees" : "Check these", Font = new Font("Segoe UI Semibold", 18f), ForeColor = Ink, AutoSize = false, Tag = "nocaps", Left = 20, Top = 12, Width = 700, Height = 36 };
            var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 11f), Left = 24, Top = 58, Width = 712, Height = 430, Text = LyricsCheck.Describe(rep).Replace("\r\n", "\n").Replace("\n", "\r\n"), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right }; Win.NeonBox(box); Win.DarkControl(box);
            var add = new PxButton { Text = "ADD THE MISSING PASSAGES", Width = 260, Height = 38, Left = 24, Top = 506, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 150, 70, 235), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, DialogResult = DialogResult.Yes, Enabled = rep.Passages.Count > 0 };
            var copy = new PxButton { Text = "COPY REPORT", Width = 140, Height = 38, Left = 296, Top = 506, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 90, 50, 170), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left }; copy.Click += (s, e) => { try { Clipboard.SetText(box.Text); } catch { } };
            var close = new PxButton { Text = "CLOSE", Width = 120, Height = 38, Left = 616, Top = 506, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 70, 40, 120), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.No };
            f.Controls.AddRange(new Control[] { head, box, add, copy, close }); f.CancelButton = close; Win.CapsAll(f);
            return f.ShowDialog(this) == DialogResult.Yes;
        }
    }

    // ---- keeping it ----
    void UpdateCounts()
    {
        string t = lyricsBox.Text; int lines = 0, words = 0; foreach (string l in t.Split('\n')) if (l.Trim().Length > 0 && !LyricsPolish.IsSection(l.Trim())) { lines++; words += l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length; }
        countBase = lines + " lines   " + words + " words"; countLbl.Text = countBase; MoreHint(); sizeLbl.Text = "Size " + (fontSize == Math.Floor(fontSize) ? ((int)fontSize).ToString() : fontSize.ToString("0.0")); UpdateButtons();
    }
    void UpdateButtons() { bool has = lyricsBox != null && lyricsBox.Text.Trim().Length > 0; copyBtn.Enabled = saveBtn.Enabled = fixBtn.Enabled = secBtn.Enabled = dawBtn.Enabled = has && !busy; recentBtn.Enabled = !busy; genBtn.Enabled = !busy; recheckBtn.Enabled = has && !busy; removeBtn.Enabled = has && !busy; }
    static string FileSafe(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); s = s.Trim().TrimEnd('.'); return s.Length == 0 ? "lyrics" : (s.Length > 100 ? s.Substring(0, 100) : s); }
    void SaveTxt()
    {
        string name = FileSafe(titleBox.Text.Length > 0 ? titleBox.Text : "lyrics") + ".txt"; string to;
        if (PickSave != null) to = PickSave(name); else using (var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = name, InitialDirectory = path.Length > 0 ? Path.GetDirectoryName(path) : Win.DownloadsDir }) to = d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        if (to == null) return; try
        {
            var sb = new StringBuilder(); if (titleBox.Text.Trim().Length > 0) sb.Append(titleBox.Text.Trim()).Append("\r\n\r\n"); sb.Append(Crlf(lyricsBox.Text.TrimEnd())).Append("\r\n");
            if (source.Length > 0) sb.Append("\r\n").Append(source.StartsWith("LRCLIB") ? "Lyrics found on LRCLIB (lrclib.net). They belong to their writers and are saved here for your own use." : "Written by Purphex Music by listening to the song. Check the words against the song. The lyrics belong to their writers.").Append("\r\n");
            File.WriteAllText(to, sb.ToString(), new UTF8Encoding(true)); Say("Saved " + Path.GetFileName(to) + ".");
        }
        catch (Exception ex) { Say("Could not save: " + ex.Message); }
    }
    public void SaveToDaw()
    {
        string text = Lf(lyricsBox.Text).Trim(); if (text.Length == 0) { Say("There are no lyrics to save yet."); return; }
        string title = titleBox.Text.Trim(); if (title.Length == 0) { Say("Give the lyrics a title first (the song's name) so you can find them later."); titleBox.Focus(); return; }
        var r = LyricsRecent.Add(title, text, source.Length > 0 ? source : "Typed or edited");
        Say("Saved to the DAW as \"" + r.Title + "\". Open Purphex Music, Lyrics, RECENT to use it." + (Edition.Has(Tier.Supporter) ? "" : " (The DAW comes with Supporter and Fantasy; your lyrics are kept for when you have it.)"));
    }
    void ShowRecent()
    {
        var all = LyricsRecent.All(); var m = Win.NewMenu();
        if (all.Count == 0) { var none = m.Items.Add("Nothing saved yet - press SAVE TO THE DAW"); none.Enabled = false; }
        else
        {
            var first = all[0]; m.Items.Add("Last one: " + Clip(first.Title, 40) + "  (" + LyricsRecent.Ago(first.When) + ")").Click += (a, b) => LoadRecent(first); m.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < all.Count && i < LyricsRecent.Max; i++) { var r = all[i]; m.Items.Add(Clip(r.Title, 46) + "  -  " + LyricsRecent.Ago(r.When)).Click += (a, b) => LoadRecent(r); }
            m.Items.Add(new ToolStripSeparator()); m.Items.Add("Remove the list...").Click += (a, b) => { if (MessageBox.Show(this, "Remove all " + all.Count + " saved lyrics from the DAW list?", "Recent lyrics", MessageBoxButtons.YesNo) == DialogResult.Yes) { LyricsRecent.Clear(); Say("The recent list is empty."); } };
        }
        m.Show(recentBtn, new Point(0, -m.PreferredSize.Height));
    }
    static string Clip(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 1) + "..."; }
    public void LoadRecent(RecentLyric r)
    {
        if (busy) return; source = r.Source; prevText = null; undoBtn.Visible = false; titleBox.Text = r.Title; lyricsBox.Text = Crlf(r.Text); noteLbl.Text = "Loaded from your recent lyrics (" + LyricsRecent.Ago(r.When) + "). " + r.Source; Say("Loaded \"" + r.Title + "\"."); ApplyFit(); DoLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; using (var pen = new Pen(fantasy ? Color.FromArgb(150, Gold) : TM.C(120, 150, 100, 255), 1.4f)) g.DrawLine(pen, 24, 84, ClientSize.Width - 24, 84);
    }
}
