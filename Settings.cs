using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Settings window: background picture / video, animation, audio. Every change applies and is saved immediately.
class SettingsForm : Form
{
    readonly App app; bool setting;
    Label bgName, updStatus; Button updBtn; TrackBar dimBar, volBar; NumericUpDown dimNum, volNum; ComboBox devBox, bufBox, fpsBox, inBox;
    CheckBox leavesChk, studioChk, fpsChk, shareChk; ComboBox themeBox; Label themeNote;
    static readonly Color Ink = Color.FromArgb(240, 232, 255), Dim = Color.FromArgb(160, 140, 200), Head = Color.FromArgb(236, 120, 230), Fld = Color.FromArgb(24, 12, 44);

    public SettingsForm(App app)
    {
        this.app = app; Text = "Settings"; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = true; BackColor = Color.FromArgb(10, 4, 20); ForeColor = Ink; ClientSize = new Size(560, 600); Font = new Font("Segoe UI", 10f);
        Win.Dark(this); DoubleBuffered = true;
        int y = 14;
        Header("Background", ref y);
        Note("Use your own picture or video behind the app. It is cropped to fill the window automatically and remembered next time.", ref y);
        bgName = new Label { Left = 20, Top = y, Width = 520, Height = 22, ForeColor = Dim, AutoEllipsis = true }; Controls.Add(bgName); y += 28;
        var pick = Btn("Choose picture or video...", 20, y, 210, Color.FromArgb(140, 60, 220)); pick.Click += (s, e) => Pick();
        var clear = Btn("Remove (use leaves)", 240, y, 170, Color.FromArgb(70, 40, 120)); clear.Click += (s, e) => { UserBackground.Set(""); Refresh2(); };
        y += 44;
        dimBar = new TrackBar { Left = 130, Top = y - 4, Width = 300, Minimum = 0, Maximum = 90, TickFrequency = 10, BackColor = BackColor };
        dimNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 90 }; Win.NeonBox(dimNum); StyleNum(dimNum);
        Controls.Add(new Label { Text = "Darken (%)", Left = 20, Top = y + 2, Width = 100, ForeColor = Ink }); Controls.Add(dimBar); Controls.Add(dimNum);
        dimBar.Scroll += (s, e) => { if (!setting) { dimNum.Value = dimBar.Value; } };
        dimNum.ValueChanged += (s, e) => { if (setting) return; dimBar.Value = (int)dimNum.Value; Prefs.BgDim = (int)dimNum.Value; Prefs.Changed(); };
        y += 46;
        leavesChk = Chk("Keep the floating leaves on top of my background", 20, ref y); leavesChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgLeaves = leavesChk.Checked; Prefs.Changed(); } };
        studioChk = Chk("Also use it behind Purphex Music", 20, ref y); studioChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgStudio = studioChk.Checked; UserBackground.Version++; Prefs.Changed(); } };
        y += 6; Header("Theme", ref y);
        Controls.Add(new Label { Text = "Colour theme", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        themeBox = Combo(180, y, 360, new string[0]); foreach (var td in Themes.All) themeBox.Items.Add(td.Name);
        themeBox.SelectedIndexChanged += (s, e) => { if (setting || themeBox.SelectedIndex < 0) return; Themes.Apply(themeBox.SelectedIndex); themeNote.Text = Themes.Current.Blurb + " It changes the glow, borders and buttons in Purphex Music, Mastering and Bass Lab."; Prefs.Changed(); };
        y += 38; themeNote = new Label { Left = 20, Top = y - 4, Width = 520, Height = 34, ForeColor = Dim, Font = new Font("Segoe UI", 9f) }; Controls.Add(themeNote); y += 36;
        y += 6; Header("Animation", ref y);
        Controls.Add(new Label { Text = "Frame rate limit", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        fpsBox = Combo(180, y, 190, new[] { "30 fps (lightest)", "60 fps (smooth)", "120 fps (fastest screens)" }); fpsBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.FpsCap = fpsBox.SelectedIndex == 0 ? 30 : (fpsBox.SelectedIndex == 2 ? 120 : 60); Prefs.Changed(); } };
        y += 38;
        Controls.Add(new Label { Text = "Your screen refreshes " + FrameLoop.DisplayHz() + " times a second, so that is the most you will see. 120 fps needs a 120 Hz (or faster) screen and a quicker PC.", Left = 20, Top = y - 6, Width = 480, Height = 34, ForeColor = Color.FromArgb(150, 140, 190) }); y += 30;
        fpsChk = Chk("Show the frame rate in the title bar", 20, ref y); fpsChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.ShowFps = fpsChk.Checked; Prefs.Changed(); } };
        y += 6; Header("Audio (Purphex Music playback)", ref y);
        volBar = new TrackBar { Left = 130, Top = y - 4, Width = 300, Minimum = 0, Maximum = 100, TickFrequency = 10, BackColor = BackColor };
        volNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 100 }; Win.NeonBox(volNum); StyleNum(volNum);
        Controls.Add(new Label { Text = "Volume (%)", Left = 20, Top = y + 2, Width = 100, ForeColor = Ink }); Controls.Add(volBar); Controls.Add(volNum);
        volBar.Scroll += (s, e) => { if (!setting) volNum.Value = volBar.Value; };
        volNum.ValueChanged += (s, e) => { if (setting) return; volBar.Value = (int)volNum.Value; Prefs.OutVol = (int)volNum.Value; Prefs.Changed(); };
        y += 46;
        Controls.Add(new Label { Text = "Output device", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        devBox = Combo(180, y, 360, new string[0]); devBox.Items.Add("System default"); foreach (var n in WaveOutPlayer.DeviceNames()) devBox.Items.Add(n);
        devBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.OutDev = devBox.SelectedIndex - 1; Prefs.Changed(); app.StudioEng.ReopenOutput(); } };
        y += 38;
        Controls.Add(new Label { Text = "Playback buffer", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        bufBox = Combo(180, y, 360, new[] { "Low latency (11 ms) - fastest response", "Normal (23 ms)", "Safe (46 ms) - for slow computers or heavy effects" });
        bufBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.Buf = bufBox.SelectedIndex; Prefs.Changed(); app.StudioEng.ReopenOutput(); } };
        y += 38;
        Controls.Add(new Label { Text = "Recording input", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        inBox = Combo(180, y, 360, new string[0]); inBox.Items.Add("System default"); foreach (var n in AudioRecorder.Devices()) inBox.Items.Add(n);
        inBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.InDev = inBox.SelectedIndex <= 0 ? "" : (string)inBox.SelectedItem; Prefs.Changed(); } };
        y += 38;
        Note("Volume only changes what you hear while playing. Exported files are never affected.", ref y);
        y += 4; Header("Updates", ref y);
        Controls.Add(new Label { Text = "You have version " + App.CurrentVersion + ". Purphex also checks by itself every few seconds while it is open.", Left = 20, Top = y, Width = 520, Height = 38, ForeColor = Dim, Font = new Font("Segoe UI", 9f) }); y += 40;
        updBtn = Btn("Scan for updates", 20, y, 170, Color.FromArgb(140, 60, 220));
        updStatus = new Label { Left = 200, Top = y, Width = 340, Height = 36, ForeColor = Ink, Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.MiddleLeft }; Controls.Add(updStatus);
        updBtn.Click += (s, e) =>
        {
            updBtn.Enabled = false; updStatus.ForeColor = Dim; updStatus.Text = "Checking...";
            app.ScanForUpdates(msg => { try { BeginInvoke(new Action(() => { updBtn.Enabled = true; updStatus.ForeColor = msg.StartsWith("Could not") ? Color.FromArgb(255, 150, 150) : Color.FromArgb(150, 255, 170); updStatus.Text = msg; })); } catch { } });
        };
        y += 44; Header("Community presets", ref y);
        shareChk = Chk("Share the presets I save with the community automatically", 20, ref y); shareChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.ShareSaved = shareChk.Checked; Prefs.Changed(); } };
        Note("When you save a preset (a plugin preset, a plugin chain or a Bass Lab sound) it also appears in Community presets for everyone to use, under your producer name. Turn this off to keep your presets only on this PC.", ref y);
        y += 4; Header("General", ref y);
        var open = Btn("Open settings folder", 20, y, 180, Color.FromArgb(70, 40, 120)); open.Click += (s, e) => { try { Process.Start("explorer.exe", app.DataFolder); } catch { } };
        var reset = Btn("Reset appearance and audio", 208, y, 244, Color.FromArgb(150, 40, 80)); reset.Click += (s, e) => ResetAll();
        var done = Btn("Done", 462, y, 80, Color.FromArgb(140, 60, 220)); done.Click += (s, e) => Close();
        AutoScroll = true; AutoScrollMinSize = new Size(540, y + 76);   // everything stays reachable on small screens or high display scaling
        ClientSize = new Size(576, Math.Min(640, Screen.PrimaryScreen.WorkingArea.Height - 100));
        Win.CapsAll(this); updStatus.Tag = "nocaps"; bgName.Tag = "nocaps";
        Refresh2();
    }

    void Header(string t, ref int y) { Controls.Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 26, Font = new Font("Segoe UI Semibold", 12.5f), ForeColor = Head }); y += 30; }
    void Note(string t, ref int y) { Controls.Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 46, ForeColor = Dim, Font = new Font("Segoe UI", 9f) }); y += 48; }
    Button Btn(string t, int x, int y, int w, Color c) { var b = new Button { Text = t, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 255); Controls.Add(b); return b; }
    CheckBox Chk(string t, int x, ref int y) { var c = new DarkCheck { Text = t, Left = x, Top = y, Width = 520, Height = 24, ForeColor = Ink }; Controls.Add(c); y += 28; return c; }
    ComboBox Combo(int x, int y, int w, string[] items) { var c = new DarkCombo { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); Controls.Add(c); return c; }
    static void StyleNum(NumericUpDown n) { n.BackColor = Fld; n.ForeColor = Ink; n.BorderStyle = BorderStyle.FixedSingle; Win.NeonBox(n); }

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
        Prefs.BgDim = 45; Prefs.BgLeaves = true; Prefs.BgStudio = true; Prefs.ShareSaved = true; Prefs.OutVol = 100; Prefs.OutDev = -1; Prefs.Buf = 1; Prefs.FpsCap = 60; Prefs.ShowFps = false; Themes.Apply(0);
        UserBackground.Set(""); Refresh2(); app.StudioEng.ReopenOutput(); Prefs.Changed();
    }

    void Refresh2()
    {
        setting = true;
        bgName.Text = UserBackground.Active ? (UserBackground.IsVideo ? "Video: " : "Picture: ") + Prefs.BgPath : "None - using the animated purple leaves";
        dimBar.Value = Prefs.BgDim; dimNum.Value = Prefs.BgDim; leavesChk.Checked = Prefs.BgLeaves; studioChk.Checked = Prefs.BgStudio; shareChk.Checked = Prefs.ShareSaved;
        themeBox.SelectedIndex = Themes.Index; themeNote.Text = Themes.Current.Blurb + " It changes the glow, borders and buttons in Purphex Music, Mastering and Bass Lab.";
        fpsBox.SelectedIndex = Prefs.FpsCap == 30 ? 0 : (Prefs.FpsCap >= 120 ? 2 : 1); fpsChk.Checked = Prefs.ShowFps;
        { int ii = Prefs.InDev.Length == 0 ? 0 : inBox.Items.IndexOf(Prefs.InDev); inBox.SelectedIndex = Math.Max(0, ii); }
        volBar.Value = Prefs.OutVol; volNum.Value = Prefs.OutVol; devBox.SelectedIndex = Math.Max(0, Math.Min(devBox.Items.Count - 1, Prefs.OutDev + 1)); bufBox.SelectedIndex = Prefs.Buf;
        setting = false;
    }
}
