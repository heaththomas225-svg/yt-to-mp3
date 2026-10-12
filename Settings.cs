using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Settings window: background picture / video, animation, audio. Every change applies and is saved immediately.
// the wheel never changes a setting: it scrolls the Settings page (a drop-down that is open still scrolls its own list)
sealed class SettingsWheel : IMessageFilter
{
    readonly SettingsForm f; public SettingsWheel(SettingsForm f) { this.f = f; }
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != 0x20A || f.IsDisposed || !f.IsHandleCreated || !f.Visible) return false;
        long lp = (long)m.LParam; var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)); if (!f.Bounds.Contains(pt)) return false;
        var under = Control.FromChildHandle(WindowFromPoint(pt)); if (under == null || !(under == f || f.Contains(under))) return false;
        var cb = under as ComboBox; if (cb != null && cb.DroppedDown) return false; if (under.Parent is ComboBox && ((ComboBox)under.Parent).DroppedDown) return false;
        int delta = (short)((long)m.WParam >> 16); f.WheelScroll(delta); return true;
    }
}

class SettingsForm : Form
{
    readonly App app; bool setting;
    static readonly int[] FpsChoices = { 30, 60, 120, 144, 165, 240 };
    Label bgName, updStatus; Button updBtn; TrackBar dimBar, volBar; NumericUpDown dimNum, volNum; ComboBox devBox, bufBox, fpsBox, inBox;
    CheckBox leavesChk, studioChk, fpsChk, shareChk; ComboBox themeBox; Label themeNote;
    static Color Ink { get { return TM.C(255, 240, 232, 255); } } static Color Dim { get { return TM.C(255, 160, 140, 200); } } static Color Head { get { return TM.C(255, 236, 120, 230); } } static Color Fld { get { return TM.C(255, 24, 12, 44); } }

    public SettingsForm(App app)
    {
        this.app = app; Text = "Settings"; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = true; BackColor = TM.C(255, 10, 4, 20); ForeColor = Ink; ClientSize = new Size(560, 600); Font = new Font("Segoe UI", 10f);
        Win.Dark(this); DoubleBuffered = true; ClientSize = new Size(800, 640);
        int y = 14; NewPage("Background", ref y);
        Header("Background", ref y);
        Note("Use your own picture or video behind the app. It is cropped to fill the window automatically and remembered next time.", ref y);
        bgName = new Label { Left = 20, Top = y, Width = 520, Height = 22, ForeColor = Dim, AutoEllipsis = true }; Add(bgName); y += 28;
        var pick = Btn("Choose picture or video...", 20, y, 210, TM.C(255, 140, 60, 220)); pick.Click += (s, e) => Pick();
        var clear = Btn("Remove (use leaves)", 240, y, 170, TM.C(255, 70, 40, 120)); clear.Click += (s, e) => { UserBackground.Set(""); Refresh2(); };
        y += 44;
        dimBar = new TrackBar { Left = 130, Top = y - 4, Width = 300, Minimum = 0, Maximum = 90, TickFrequency = 10, BackColor = BackColor };
        dimNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 90 }; Win.NeonBox(dimNum); StyleNum(dimNum);
        Add(new Label { Text = "Darken (%)", Left = 20, Top = y + 2, Width = 100, ForeColor = Ink }); Add(dimBar); Add(dimNum);
        dimBar.Scroll += (s, e) => { if (!setting) { dimNum.Value = dimBar.Value; } };
        dimNum.ValueChanged += (s, e) => { if (setting) return; dimBar.Value = (int)dimNum.Value; Prefs.BgDim = (int)dimNum.Value; Prefs.Changed(); };
        y += 46;
        leavesChk = Chk("Keep the floating leaves on top of my background", 20, ref y); leavesChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgLeaves = leavesChk.Checked; Prefs.Changed(); } };
        studioChk = Chk("Also use it behind Purphex Music", 20, ref y); studioChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgStudio = studioChk.Checked; UserBackground.Version++; Prefs.Changed(); } };
        NewPage("Theme", ref y); Header("Theme", ref y);
        Add(new Label { Text = "Colour theme", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        themeBox = Combo(180, y, 360, new string[0]); foreach (var td in Themes.All) themeBox.Items.Add(ThemeLabel(td));   // built in and imported
        themeBox.SelectedIndexChanged += (s, e) => { if (setting || themeBox.SelectedIndex < 0) return; { var tdx = Themes.All[themeBox.SelectedIndex]; if (!Themes.Allowed(tdx)) { Edition.Upsell(tdx.Req, "The " + tdx.Name + " theme", this); setting = true; themeBox.SelectedIndex = Themes.Index; setting = false; return; } } Themes.Apply(themeBox.SelectedIndex); themeNote.Text = Themes.Current.Blurb + " It changes the glow, borders and buttons in Purphex Music, Mastering and Bass Lab."; Prefs.Changed(); };
        y += 38; themeNote = new Label { Left = 20, Top = y - 4, Width = 520, Height = 34, ForeColor = Dim, Font = new Font("Segoe UI", 10f) }; Add(themeNote); y += 36;
        var comT = Btn("Community themes...", 20, y, 190, TM.C(255, 140, 60, 220)); comT.Click += (s, e) => app.ShowThemes(true); y += 40;
        NewPage("Animation", ref y); Header("Animation", ref y);
        Add(new Label { Text = "Frame rate limit", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        fpsBox = Combo(180, y, 290, new[] { "30 fps (lightest)", "60 fps (smooth)", "120 fps (fast screens)", "144 fps (gaming screens)", "165 fps", "240 fps (the fastest screens)" }); fpsBox.SelectedIndexChanged += (s, e) => { if (!setting && fpsBox.SelectedIndex >= 0) { Prefs.FpsCap = FpsChoices[fpsBox.SelectedIndex]; Prefs.Changed(); } };
        y += 38;
        Add(new Label { Text = "Your screen refreshes " + FrameLoop.DisplayHz() + " times a second, so that is the most you will see. 120 fps needs a 120 Hz (or faster) screen and a quicker PC.", Left = 20, Top = y - 6, Width = 480, Height = 34, ForeColor = TM.C(255, 150, 140, 190) }); y += 30;
        fpsChk = Chk("Show the frame rate in the title bar", 20, ref y); fpsChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.ShowFps = fpsChk.Checked; Prefs.Changed(); } };
        NewPage("Audio", ref y); Header("Audio (Purphex Music playback)", ref y);
        volBar = new TrackBar { Left = 130, Top = y - 4, Width = 300, Minimum = 0, Maximum = 100, TickFrequency = 10, BackColor = BackColor };
        volNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 100 }; Win.NeonBox(volNum); StyleNum(volNum);
        Add(new Label { Text = "Volume (%)", Left = 20, Top = y + 2, Width = 100, ForeColor = Ink }); Add(volBar); Add(volNum);
        volBar.Scroll += (s, e) => { if (!setting) volNum.Value = volBar.Value; };
        volNum.ValueChanged += (s, e) => { if (setting) return; volBar.Value = (int)volNum.Value; Prefs.OutVol = (int)volNum.Value; Prefs.Changed(); };
        y += 46;
        Add(new Label { Text = "Output device", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        devBox = Combo(180, y, 360, new string[0]); devBox.Items.Add("System default"); foreach (var n in WaveOutPlayer.DeviceNames()) devBox.Items.Add(n);
        devBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.OutDev = devBox.SelectedIndex - 1; Prefs.Changed(); app.StudioEng.ReopenOutput(); } };
        y += 38;
        Add(new Label { Text = "Playback buffer", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        bufBox = Combo(180, y, 360, new[] { "Low latency (11 ms) - fastest response", "Normal (23 ms)", "Safe (46 ms) - for slow computers or heavy effects" });
        bufBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.Buf = bufBox.SelectedIndex; Prefs.BufBoost = 0; Prefs.Changed(); app.StudioEng.ReopenOutput(); } };
        y += 38;
        Add(new Label { Text = "Recording input", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        inBox = Combo(180, y, 360, new string[0]); inBox.Items.Add("System default"); foreach (var n in AudioRecorder.Devices()) inBox.Items.Add(n);
        inBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.InDev = inBox.SelectedIndex <= 0 ? "" : (string)inBox.SelectedItem; Prefs.Changed(); } };
        y += 38;
        Note("Volume only changes what you hear while playing. Exported files are never affected.", ref y);
        NewPage("Updates", ref y); Header("Updates", ref y);
        Add(new Label { Text = "You have version " + App.CurrentVersion + ". Purphex also checks by itself every few seconds while it is open.", Left = 20, Top = y, Width = 520, Height = 38, ForeColor = Dim, Font = new Font("Segoe UI", 10f) }); y += 40;
        updBtn = Btn("Scan for updates", 20, y, 170, TM.C(255, 140, 60, 220));
        updStatus = new Label { Left = 200, Top = y, Width = 340, Height = 36, ForeColor = Ink, Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.MiddleLeft }; Add(updStatus);
        updBtn.Click += (s, e) =>
        {
            updBtn.Enabled = false; updStatus.ForeColor = Dim; updStatus.Text = "Checking...";
            app.ScanForUpdates(msg => { try { BeginInvoke(new Action(() => { updBtn.Enabled = true; updStatus.ForeColor = msg.StartsWith("Could not") ? Color.FromArgb(255, 150, 150) : Color.FromArgb(150, 255, 170); updStatus.Text = msg; })); } catch { } });
        };
        NewPage("Community", ref y); Header("Community presets", ref y);
        shareChk = Chk("Share the presets I save with the community automatically", 20, ref y); shareChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.ShareSaved = shareChk.Checked; Prefs.Changed(); } };
        Note("When you save a preset (a plugin preset, a plugin chain or a Bass Lab sound) it also appears in Community presets for everyone to use, under your producer name. Turn this off to keep your presets only on this PC.", ref y);
        NewPage("General", ref y); Header("General", ref y);
        var open = Btn("Open settings folder", 20, y, 180, TM.C(255, 70, 40, 120)); open.Click += (s, e) => { try { Process.Start("explorer.exe", app.DataFolder); } catch { } };
        var reset = Btn("Reset appearance and audio", 208, y, 244, TM.C(255, 150, 40, 80)); reset.Click += (s, e) => ResetAll();
        NewPage("Account", ref y); accountIdx = pages.Count - 1; Header("Your edition", ref y);
        accLine = new Label { Left = 20, Top = y, Width = 540, Height = 30, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Head, Tag = "nocaps" }; Add(accLine); y += 36;
        accSub = new Label { Left = 20, Top = y, Width = 540, Height = 44, ForeColor = Dim, Tag = "nocaps" }; Add(accSub); y += 50;
        linkBtn = Btn("LINK THIS PC", 20, y, 180, TM.C(255, 140, 60, 220)); linkBtn.Tag = "nocaps"; linkBtn.Click += (s, e) => LinkClick();
        var seeB = Btn("See the editions", 208, y, 170, TM.C(255, 70, 40, 150)); seeB.Click += (s, e) => Edition.OpenEditions();
        var chkB = Btn("Check again", 386, y, 140, TM.C(255, 70, 40, 120)); chkB.Click += (s, e) => CheckAgain(); y += 44;
        accMsg = new Label { Left = 20, Top = y, Width = 540, Height = 44, ForeColor = Ink, Tag = "nocaps" }; Add(accMsg); y += 50;
        Add(new Label { Left = 20, Top = y, Width = 540, Height = 96, ForeColor = Dim, Font = new Font("Segoe UI", 10f), Text = "How it works: press LINK THIS PC. Purphex opens the website, you sign in and press Yes, link it, and this PC unlocks by itself. Free is the MP3 and MP4 converter and the vocal remover; Supporter opens the whole studio; Fantasy is everything, for life. Every paid edition has a code (PX-XXXX-XXXX-XXXX): it is linked to one account at a time, and EDITION CODES lets you unlink it and use it on another account. Nothing is paid inside the app." }); y += 102;
        var web = Btn("My account on the website", 20, y, 230, TM.C(255, 70, 40, 120)); web.Click += (s, e) => Edition.OpenUrl(Edition.Site + "/account"); 
        var codesB = Btn("Edition codes: unlink or redeem", 258, y, 270, TM.C(255, 70, 40, 120)); codesB.Click += (s, e) => Edition.OpenUrl(Edition.Site + "/redeem"); y += 40;
        FinishPage(y); BuildFrame();
        ClientSize = new Size(800, Math.Min(660, Screen.PrimaryScreen.WorkingArea.Height - 100)); LayoutFrame(); ShowPage(0);
        Win.CapsAll(this); updStatus.Tag = "nocaps"; bgName.Tag = "nocaps";
        LockPage("Background"); LockPage("Theme"); Refresh2(); RefreshAccount(); RefreshLocks(); Action onEd = () => { try { RefreshAccount(); RefreshLocks(); } catch { } }; Edition.Changed += onEd; FormClosed += (s3, e3) => Edition.Changed -= onEd;
        wheel = new SettingsWheel(this); Application.AddMessageFilter(wheel); FormClosed += (s2, e2) => { try { Application.RemoveMessageFilter(wheel); } catch { } };
    }
    SettingsWheel wheel;
    static string ThemeLabel(ThemeDef t) { string g = Themes.Tag(t); return g.Length > 0 ? t.Name + "   (" + g + ")" : t.Name; }

    // ---- the edition: the Account page and the padlocks on the pages the Free edition does not have ----
    int accountIdx; Label accLine, accSub, accMsg; Button linkBtn; readonly System.Collections.Generic.Dictionary<Panel, Panel> lockBanners = new System.Collections.Generic.Dictionary<Panel, Panel>();
    internal void ShowAccount() { if (accountIdx > 0) ShowPage(accountIdx); }
    void LockPage(string name)
    {
        var p = pages.Find(x => (string)x.Tag == name); if (p == null) return; foreach (Control c in p.Controls) c.Top += 64; p.AutoScrollMinSize = new Size(560, p.AutoScrollMinSize.Height + 64);
        var ban = new Panel { Left = 14, Top = 4, Width = 560, Height = 54, BackColor = TM.C(255, 40, 22, 74) };
        ban.Controls.Add(new Label { Text = "Themes and photo backgrounds are part of the Supporter Edition.", Left = 46, Top = 8, Width = 340, Height = 38, ForeColor = Ink, Tag = "nocaps" });
        var go = new PxButton { Text = "SEE THE EDITIONS", Left = 396, Top = 10, Width = 152, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 190, 60, 200), ForeColor = Color.White, Cursor = Cursors.Hand }; go.FlatAppearance.BorderColor = TM.C(255, 220, 140, 255); go.Click += (s, e) => Edition.OpenEditions(); ban.Controls.Add(go);
        ban.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; Edition.Padlock(e.Graphics, new RectangleF(12, 10, 24, 30), Color.FromArgb(255, 255, 200, 90)); using (var pen = new Pen(TM.C(255, 190, 120, 255))) e.Graphics.DrawRectangle(pen, 0, 0, ban.Width - 1, ban.Height - 1); };
        p.Controls.Add(ban); lockBanners[p] = ban;
    }
    void RefreshLocks()
    {
        bool locked = !Edition.Has(Tier.Supporter);
        foreach (var kv in lockBanners) { foreach (Control c in kv.Key.Controls) if (c != kv.Value) c.Enabled = !locked; kv.Value.Visible = locked; }
    }
    void RefreshAccount()
    {
        if (accLine == null) return; var t = Edition.Current; accLine.Text = Edition.Name(t); accLine.ForeColor = t == Tier.Fantasy ? TM.C(255, 255, 110, 230) : (t == Tier.Supporter ? TM.C(255, 255, 200, 90) : Head);
        string sub;
        if (!Edition.Linked) sub = "This PC is not linked to a Purphex account. Free: the MP3 and MP4 converter and the vocal remover.";
        else
        {
            sub = "Linked to " + (Edition.Who.Length > 0 ? Edition.Who : "your account") + ".";
            if (t == Tier.Supporter && Edition.Until.Length > 0) { DateTime u; if (DateTime.TryParse(Edition.Until, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out u)) sub += " " + (Edition.Billing == "gift" ? "A gift until " : "Paid until ") + u.ToLocalTime().ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture) + "."; }
            else if (t == Tier.Fantasy) sub += " Yours for life.";
            else if (t == Tier.Free) sub += " The account has no paid edition (or this PC could not reach the website for a long time).";
        }
        if (Edition.Note.Length > 0) sub += "\n" + Edition.Note; accSub.Text = sub; linkBtn.Text = Edition.Linking ? "CANCEL LINKING" : (Edition.Linked ? "UNLINK THIS PC" : "LINK THIS PC");
    }
    void Say(string s) { try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => { if (accMsg != null) accMsg.Text = s; RefreshAccount(); })); } catch { } }
    void LinkClick()
    {
        if (Edition.Linking) { Edition.CancelLink(); return; }
        if (Edition.Linked) { if (MessageBox.Show(this, "Unlink this PC? The studio locks again until you link it.", "Unlink this PC", MessageBoxButtons.YesNo) == DialogResult.Yes) { Edition.Unlink(); accMsg.Text = "This PC is unlinked."; } return; }
        accMsg.Text = "Opening the website..."; Edition.StartLink(m => Say(m), (ok, m) => Say(m)); RefreshAccount();
    }
    void CheckAgain()
    {
        if (!Edition.Linked) { accMsg.Text = "This PC is not linked yet. Press LINK THIS PC."; return; } accMsg.Text = "Asking the website...";
        Edition.Refresh(ok => Say(ok ? "Up to date." : "Could not reach the website: your edition stays as it is."));
    }
    Panel page, shown; readonly System.Collections.Generic.List<Panel> pages = new System.Collections.Generic.List<Panel>(); readonly System.Collections.Generic.List<Button> sideBtns = new System.Collections.Generic.List<Button>(); const int SideW = 176, TopH = 56, BotH = 60;
    void Add(Control c) { page.Controls.Add(c); }
    // the mouse wheel scrolls the page, whatever is under the pointer: it never changes a drop-down, a number or a slider by accident
    internal void WheelScroll(int delta) { if (shown == null) return; int cur = -shown.AutoScrollPosition.Y; shown.AutoScrollPosition = new Point(0, Math.Max(0, cur - delta / 2)); }

    // ---- the pages: a list of sections down the left, one page at a time on the right, SAVE at the top and at the bottom ----
    void NewPage(string name, ref int y)
    {
        if (page != null) FinishPage(y);
        page = new Panel { Left = SideW, Top = TopH, Width = ClientSize.Width - SideW, Height = ClientSize.Height - TopH - BotH, AutoScroll = true, BackColor = TM.C(255, 10, 4, 20), Visible = false, Tag = name }; pages.Add(page); base.Controls.Add(page); y = 14;
    }
    void FinishPage(int y) { if (page != null) page.AutoScrollMinSize = new Size(560, y + 24); }
    Button botSave; Label titleLbl;
    void BuildFrame()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            int k = i; var b = new PxButton { Text = ((string)pages[i].Tag).ToUpper(), Left = 12, Top = TopH + 10 + i * 46, Width = SideW - 24, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 40, 22, 74), ForeColor = Ink, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font("Segoe UI Semibold", 10.5f) };
            b.FlatAppearance.BorderColor = TM.C(255, 120, 90, 180); b.Click += (s, e) => ShowPage(k); sideBtns.Add(b); base.Controls.Add(b);
        }
        titleLbl = new Label { Text = "SETTINGS", Left = 16, Top = 12, Width = 300, Height = 34, ForeColor = Head, Font = new Font("Segoe UI Semibold", 16f), Tag = "nocaps" }; base.Controls.Add(titleLbl);
        botSave = new PxButton { Text = "SAVE", Width = 130, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 140, 60, 220), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 11.5f) }; botSave.FlatAppearance.BorderColor = TM.C(255, 200, 150, 255); botSave.Click += (s, e) => SaveClose(); base.Controls.Add(botSave);
        Resize += (s, e) => LayoutFrame();
    }
    void LayoutFrame()
    {
        if (botSave == null) return; botSave.Left = ClientSize.Width - botSave.Width - 16; botSave.Top = ClientSize.Height - BotH + 10;
        foreach (var p in pages) { p.Left = SideW; p.Top = TopH; p.Width = ClientSize.Width - SideW; p.Height = Math.Max(100, ClientSize.Height - TopH - BotH); }
    }
    void ShowPage(int i)
    {
        for (int k = 0; k < pages.Count; k++) { pages[k].Visible = k == i; sideBtns[k].BackColor = k == i ? TM.C(255, 140, 60, 220) : TM.C(255, 40, 22, 74); }
        shown = pages[i]; shown.AutoScrollPosition = new Point(0, 0);
    }
    // everything already applies the moment you change it; SAVE writes it down and closes the window
    void SaveClose() { Prefs.Changed(); Close(); }

    void Header(string t, ref int y) { Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 26, Font = new Font("Segoe UI Semibold", 12.5f), ForeColor = Head }); y += 30; }
    void Note(string t, ref int y) { Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 58, ForeColor = Dim, Font = new Font("Segoe UI", 10f) }); y += 60; }
    Button Btn(string t, int x, int y, int w, Color c) { var b = new PxButton { Text = t, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = TM.C(255, 200, 150, 255); Add(b); return b; }
    CheckBox Chk(string t, int x, ref int y) { var c = new DarkCheck { Text = t, Left = x, Top = y, Width = 520, Height = 24, ForeColor = Ink }; Add(c); y += 28; return c; }
    ComboBox Combo(int x, int y, int w, string[] items) { var c = new DarkCombo { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); Add(c); return c; }
    static void StyleNum(NumericUpDown n) { n.BackColor = Fld; n.ForeColor = Ink; n.BorderStyle = BorderStyle.FixedSingle; Win.NeonBox(n); }

    void ImportTheme()
    {
        using (var d = new OpenFileDialog { Title = "Import a theme", Filter = "Purphex themes|*.purphextheme;*.txt|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return; string text; try { if (new FileInfo(d.FileName).Length > 16 * 1024) { MessageBox.Show(this, "That file is too big to be a theme."); return; } text = File.ReadAllText(d.FileName); } catch (Exception ex) { MessageBox.Show(this, "Could not open that file: " + ex.Message); return; }
            ThemeDef t; string err = Themes.Import(text, Path.GetFileNameWithoutExtension(d.FileName), out t); if (err != null) { MessageBox.Show(this, err, "Import a theme"); return; }
            setting = true; themeBox.Items.Clear(); foreach (var td in Themes.All) themeBox.Items.Add(ThemeLabel(td)); setting = false; Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed(); Refresh2();
        }
    }

    void Pick()
    {
        using (var d = new OpenFileDialog { Title = "Choose a background", Filter = "Pictures and videos|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif;*.webp;*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (!UserBackground.IsImage(d.FileName) && App.FfmpegPath() == null) { MessageBox.Show(this, "Videos need ffmpeg, which is installed the first time you convert something. Convert any link once, then pick the video again. (Pictures work right away.)", "Background"); return; }
            UserBackground.Set(d.FileName);
            if (UserBackground.Active && !UserBackground.IsVideo)
            {
                string perr = null;
                try { using (var fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false)) { var sz = im.Size; } }   // header check only; the real decode happens on the background thread
                catch (Exception ex) { perr = ex.Message; }
                if (perr != null) { MessageBox.Show(this, "That file couldn't be opened as a picture: " + perr, "Background"); UserBackground.Set(""); }
            }
            Refresh2();
        }
    }

    void ResetAll()
    {
        Prefs.BgDim = 45; Prefs.BgLeaves = true; Prefs.BgStudio = true; Prefs.ShareSaved = true; Prefs.OutVol = 100; Prefs.OutDev = -1; Prefs.Buf = 1; Prefs.BufBoost = 0; Prefs.FpsCap = 60; Prefs.ShowFps = false; Themes.Apply(0);
        UserBackground.Set(""); Refresh2(); app.StudioEng.ReopenOutput(); Prefs.Changed();
    }

    void Refresh2()
    {
        setting = true;
        bgName.Text = UserBackground.Active ? (UserBackground.IsVideo ? "Video: " : "Picture: ") + Prefs.BgPath : "None - using the animated purple leaves";
        dimBar.Value = Prefs.BgDim; dimNum.Value = Prefs.BgDim; leavesChk.Checked = Prefs.BgLeaves; studioChk.Checked = Prefs.BgStudio; shareChk.Checked = Prefs.ShareSaved;
        themeBox.SelectedIndex = Themes.Index; themeNote.Text = Themes.Current.Blurb + " It changes the glow, borders and buttons in Purphex Music, Mastering and Bass Lab.";
        fpsBox.SelectedIndex = Math.Max(0, Array.IndexOf(FpsChoices, Prefs.FpsCap)); fpsChk.Checked = Prefs.ShowFps;
        { int ii = Prefs.InDev.Length == 0 ? 0 : inBox.Items.IndexOf(Prefs.InDev); inBox.SelectedIndex = Math.Max(0, ii); }
        volBar.Value = Prefs.OutVol; volNum.Value = Prefs.OutVol; devBox.SelectedIndex = Math.Max(0, Math.Min(devBox.Items.Count - 1, Prefs.OutDev + 1)); bufBox.SelectedIndex = Prefs.Buf;
        setting = false;
    }
}
