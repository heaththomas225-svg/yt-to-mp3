using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Settings window: background picture / video, animation, audio. Every change applies and is saved immediately.
class SettingsForm : Form
{
    readonly App app; bool setting;
    Label bgName; TrackBar dimBar, volBar; NumericUpDown dimNum, volNum; ComboBox devBox, bufBox, fpsBox, inBox;
    CheckBox leavesChk, studioChk, fpsChk;
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
        dimNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 90 }; StyleNum(dimNum);
        Controls.Add(new Label { Text = "Darken (%)", Left = 20, Top = y + 2, Width = 100, ForeColor = Ink }); Controls.Add(dimBar); Controls.Add(dimNum);
        dimBar.Scroll += (s, e) => { if (!setting) { dimNum.Value = dimBar.Value; } };
        dimNum.ValueChanged += (s, e) => { if (setting) return; dimBar.Value = (int)dimNum.Value; Prefs.BgDim = (int)dimNum.Value; Prefs.Changed(); };
        y += 46;
        leavesChk = Chk("Keep the floating leaves on top of my background", 20, ref y); leavesChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgLeaves = leavesChk.Checked; Prefs.Changed(); } };
        studioChk = Chk("Also use it behind Purphex Music", 20, ref y); studioChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.BgStudio = studioChk.Checked; UserBackground.Version++; Prefs.Changed(); } };
        y += 6; Header("Animation", ref y);
        Controls.Add(new Label { Text = "Frame rate limit", Left = 20, Top = y + 3, Width = 150, ForeColor = Ink });
        fpsBox = Combo(180, y, 190, new[] { "30 fps (lightest)", "60 fps (smooth)", "120 fps (fastest screens)" }); fpsBox.SelectedIndexChanged += (s, e) => { if (!setting) { Prefs.FpsCap = fpsBox.SelectedIndex == 0 ? 30 : (fpsBox.SelectedIndex == 2 ? 120 : 60); Prefs.Changed(); } };
        y += 38;
        Controls.Add(new Label { Text = "Your screen refreshes " + FrameLoop.DisplayHz() + " times a second, so that is the most you will see. 120 fps needs a 120 Hz (or faster) screen and a quicker PC.", Left = 20, Top = y - 6, Width = 480, Height = 34, ForeColor = Color.FromArgb(150, 140, 190) }); y += 30;
        fpsChk = Chk("Show the frame rate (title bar and a badge on every screen)", 20, ref y); fpsChk.CheckedChanged += (s, e) => { if (!setting) { Prefs.ShowFps = fpsChk.Checked; Prefs.Changed(); } };
        y += 6; Header("Audio (Purphex Music playback)", ref y);
        volBar = new TrackBar { Left = 130, Top = y - 4, Width = 300, Minimum = 0, Maximum = 100, TickFrequency = 10, BackColor = BackColor };
        volNum = new NumericUpDown { Left = 440, Top = y, Width = 64, Minimum = 0, Maximum = 100 }; StyleNum(volNum);
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
        y += 4; Header("General", ref y);
        var open = Btn("Open settings folder", 20, y, 180, Color.FromArgb(70, 40, 120)); open.Click += (s, e) => { try { Process.Start("explorer.exe", app.DataFolder); } catch { } };
        var reset = Btn("Reset appearance and audio", 210, y, 220, Color.FromArgb(150, 40, 80)); reset.Click += (s, e) => ResetAll();
        var done = Btn("Done", 460, y, 80, Color.FromArgb(140, 60, 220)); done.Click += (s, e) => Close();
        AutoScroll = true; AutoScrollMinSize = new Size(540, y + 76);   // everything stays reachable on small screens or high display scaling
        ClientSize = new Size(576, Math.Min(640, Screen.PrimaryScreen.WorkingArea.Height - 100));
        Refresh2();
    }

    void Header(string t, ref int y) { Controls.Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 26, Font = new Font("Segoe UI Semibold", 12.5f), ForeColor = Head }); y += 30; }
    void Note(string t, ref int y) { Controls.Add(new Label { Text = t, Left = 20, Top = y, Width = 520, Height = 38, ForeColor = Dim, Font = new Font("Segoe UI", 9f) }); y += 40; }
    Button Btn(string t, int x, int y, int w, Color c) { var b = new Button { Text = t, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 255); Controls.Add(b); return b; }
    CheckBox Chk(string t, int x, ref int y) { var c = new DarkCheck { Text = t, Left = x, Top = y, Width = 520, Height = 24, ForeColor = Ink }; Controls.Add(c); y += 28; return c; }
    ComboBox Combo(int x, int y, int w, string[] items) { var c = new DarkCombo { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); Controls.Add(c); return c; }
    static void StyleNum(NumericUpDown n) { n.BackColor = Fld; n.ForeColor = Ink; n.BorderStyle = BorderStyle.FixedSingle; }

    void Pick()
    {
        using (var d = new OpenFileDialog { Title = "Choose a background", Filter = "Pictures and videos|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif;*.webp;*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (!UserBackground.IsImage(d.FileName) && App.FfmpegPath() == null) { MessageBox.Show(this, "Videos need ffmpeg, which is installed the first time you convert something. Convert any link once, then pick the video again. (Pictures work right away.)", "Background"); return; }
            UserBackground.Set(d.FileName);
            if (UserBackground.Active && !UserBackground.IsVideo && UserBackground.Still(Math.Max(16, app.ClientSize.Width), Math.Max(16, app.ClientSize.Height)) == null)
            { MessageBox.Show(this, "That file couldn't be opened as a picture: " + UserBackground.Error, "Background"); UserBackground.Set(""); }
            Refresh2();
        }
    }

    void ResetAll()
    {
        Prefs.BgDim = 45; Prefs.BgLeaves = true; Prefs.BgStudio = true; Prefs.OutVol = 100; Prefs.OutDev = -1; Prefs.Buf = 1; Prefs.FpsCap = 60; Prefs.ShowFps = false;
        UserBackground.Set(""); Refresh2(); app.StudioEng.ReopenOutput(); Prefs.Changed();
    }

    void Refresh2()
    {
        setting = true;
        bgName.Text = UserBackground.Active ? (UserBackground.IsVideo ? "Video: " : "Picture: ") + Prefs.BgPath : "None - using the animated purple leaves";
        dimBar.Value = Prefs.BgDim; dimNum.Value = Prefs.BgDim; leavesChk.Checked = Prefs.BgLeaves; studioChk.Checked = Prefs.BgStudio;
        fpsBox.SelectedIndex = Prefs.FpsCap == 30 ? 0 : (Prefs.FpsCap >= 120 ? 2 : 1); fpsChk.Checked = Prefs.ShowFps;
        { int ii = Prefs.InDev.Length == 0 ? 0 : inBox.Items.IndexOf(Prefs.InDev); inBox.SelectedIndex = Math.Max(0, ii); }
        volBar.Value = Prefs.OutVol; volNum.Value = Prefs.OutVol; devBox.SelectedIndex = Math.Max(0, Math.Min(devBox.Items.Count - 1, Prefs.OutDev + 1)); bufBox.SelectedIndex = Prefs.Buf;
        setting = false;
    }
}
