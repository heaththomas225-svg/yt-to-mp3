using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Cache;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Panel that paints flicker-free so the animated background is smooth.
class BgPanel : Panel
{
    public BgPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
}

class Leaf { public float X, Y, S, Rot, Spin, VY, Phase, Sway; public int A, CI; public Color C; }
class Spore { public float X, Y, R, VY, Phase; }

// Snapshot of the UI taken on the UI thread and handed to the worker thread.
class Job
{
    public string[] Items; public string Dir, TrimA, TrimB;
    public int Fmt, Sr, Name; public bool Playlist, Normalize, OpenDone;
    public MasterSettings Master;   // null when mastering is off
    public string ExtraFilter = "";
}

partial class App : Form
{
    // ---- Auto-update settings ----
    const string Version = "3.0.0";
    // Plain-text manifest: line 1 = newest version, line 2 = direct exe download URL.
    const string UpdateManifestUrl = "https://raw.githubusercontent.com/heaththomas225-svg/yt-to-mp3/main/version.txt";

    static string AppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3");
    static string Tools = Path.Combine(AppData, "tools");
    static string SettingsFile = Path.Combine(AppData, "settings.txt");
    internal static string YtDlp = Path.Combine(Tools, "yt-dlp.exe");
    static string FfmpegDir = Path.Combine(Tools, "ffmpeg");
    static string DenoDir = Path.Combine(Tools, "deno");

    // ---- Output formats: name, container, quality arg, can embed cover art, extra ffmpeg args ----
    static readonly string[] FmtNames = {
        "MP3  320 kbps (CBR)", "MP3  V0 (VBR, ~245 kbps)", "WAV  24-bit PCM", "FLAC  (lossless container)",
        "M4A  AAC 256 kbps", "Opus  (best quality per size)" };
    static readonly string[] FmtExt = { "mp3", "mp3", "wav", "flac", "m4a", "opus" };
    static readonly string[] FmtQual = { "320K", "0", null, null, "256K", null };
    static readonly bool[] FmtArt = { true, true, false, true, true, false };
    static readonly string[] FmtCodec = { null, null, "-c:a pcm_s24le", null, null, null };
    static readonly string[] FmtTag = { "MP3 320k", "MP3 V0", "WAV 24bit", "FLAC", "AAC 256k", "Opus" };

    // Label put in every file name so you can tell what settings produced it.
    static string FormatTag(int fmt, int srIdx, MasterSettings m, bool normalized)
    {
        string s = FmtTag[fmt];
        if (srIdx == 1) s += " 44.1k"; else if (srIdx == 2) s += " 48k";
        if (m != null)
            s += m.Comp ? " mastered matched" : " mastered " + m.Loud.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "LUFS";
        else if (normalized) s += " -14LUFS";
        return s;
    }

    static readonly string[] SrNames = { "Original", "44.1 kHz", "48 kHz" };
    static readonly string[] SrArgs = { null, "-ar 44100", "-ar 48000" };
    static readonly string[] NameNames = { "Title", "Artist - Title", "Title [video id]" };
    static readonly string[] NameTpl = { "%(title).150B", "%(artist,uploader)s - %(title).150B", "%(title).140B [%(id)s]" };

    BgPanel bg = new BgPanel();
    TextBox links = new TextBox(), outDir = new TextBox(), log = new TextBox(), trimA = new TextBox(), trimB = new TextBox();
    ComboBox fmt = new DarkCombo(), sr = new DarkCombo(), nameFmt = new DarkCombo();
    CheckBox playlist = new DarkCheck(), norm = new DarkCheck(), openDone = new DarkCheck(), animate = new DarkCheck();
    Button go = new Button(), cancel = new Button(), browse = new Button();
    ToolTip tip = new ToolTip();
    List<KeyValuePair<Control, string>> lbls = new List<KeyValuePair<Control, string>>();
    FrameLoop frameLoop; Stopwatch clock = Stopwatch.StartNew(); double lastClock, nextFpsAt = 1000; int fpsFrames, fpsShown;
    Random rnd = new Random();
    List<Leaf> leaves = new List<Leaf>();
    List<Spore> spores = new List<Spore>();
    float t = 0, targetPct = 0, shownPct = 0;
    int itemIdx = 0, itemTotal = 1;
    volatile bool busy = false, cancelReq = false;
    Process cur;
    string status = "";
    Panel card;
    float pctScale = 1f;

    // shared with the mastering window
    internal MasterSettings Master = new MasterSettings();
    internal MasterAnalysis LastAn;
    internal string LastFile;
    internal int MZoomPct = 75;
    MasterForm mf;
    Button mastBtn = new Button(), vocalBtn = new Button(), phoneBtn = new Button(), whatsBtn = new Button(), prevNormBtn = new Button(), helpBtn = new Button(); string seenVer = ""; internal string ExtraFilter = "";
    PhoneServer phone;
    internal StudioEngine StudioEng = new StudioEngine();
    StudioForm studioForm; Button studioBtn = new Button(), addMusicBtn = new Button();

    internal void ShowHelp(string topic)
    {
        using (var h = new HelpForm(id => true, id => { string tid = id; BeginInvoke(new Action(() => RunTour(tid))); }, topic)) h.ShowDialog(this);
    }

    void RunTour(string id)
    {
        Func<Control, Func<Rectangle>> R = c => (() => new Rectangle(c.Left, c.Top, c.Width, c.Height)); var s = new List<TourStep>();
        if (id.StartsWith("studio")) { OpenStudio(); studioForm.RunTour(id); return; }
        if (id == "start") { s.Add(new TourStep(R(links), "1. Paste a link", "Paste one or more YouTube or BandLab links here, or type words to search.")); s.Add(new TourStep(R(fmt), "2. Pick a format", "MP3 320 kbps is the best all-round choice.")); s.Add(new TourStep(R(go), "3. Press Convert", "Your file is saved in the Save to folder, which opens when it is done.")); }
        else if (id == "normalize") { s.Add(new TourStep(R(norm), "Normalize loudness", "Tick this to bring downloads to a standard -14 LUFS loudness.")); s.Add(new TourStep(R(prevNormBtn), "Hear it first", "Press Hear to listen to a 30 second sample, original versus normalized, before you convert.")); }
        else if (id == "vocal") { s.Add(new TourStep(R(vocalBtn), "Vocal remover", "Click this, then choose Remove vocals from a song, or Preview to hear a 30 second sample first.")); s.Add(new TourStep(R(outDir), "Where files go", "The instrumental and the vocals-only file are saved in this folder.")); }
        else if (id == "phone") { s.Add(new TourStep(R(phoneBtn), "Phone mode", "Press this, then open the address it shows on your phone (same Wi-Fi). Use the firewall button if it will not open.")); }
        else if (id == "masterassistant") { s.Add(new TourStep(R(mastBtn), "Mastering Assistant", "Opens the live EQ, loudness and width window. Turn on its power button and your downloads are processed.")); }
        if (s.Count > 0) Tour.Run(this, s);
    }

    void OpenStudio()
    {
        if (studioForm == null || studioForm.IsDisposed) { studioForm = new StudioForm(this); studioForm.Show(this); }
        else { studioForm.Activate(); if (studioForm.WindowState == FormWindowState.Minimized) studioForm.WindowState = FormWindowState.Normal; }
    }

    static readonly Color Ink = Color.FromArgb(235, 225, 255);
    static readonly Color Field = Color.FromArgb(8, 4, 16);
    static readonly Color Pill = Color.FromArgb(14, 7, 28);

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
        try { File.Delete(Application.ExecutablePath + ".old"); } catch { }
        Application.EnableVisualStyles();
        Application.Run(new App());
    }

    App()
    {
        Text = "BandLab + YouTube to MP3 v" + Version;
        ClientSize = new Size(640, 690);
        MinimumSize = new Size(560, 640);
        StudioEnv.Ffmpeg = FfmpegExe; StudioEnv.Ffprobe = FfprobeExe;
        Font = new Font("Segoe UI", 9.5f);
        Shown += (s, e) =>
        {
            new Thread(UpdateLoop) { IsBackground = true }.Start();
            if (seenVer != Changelog.Latest) BeginInvoke(new Action(() => { seenVer = Changelog.Latest; SaveSettings(); Changelog.Show(this); }));
        };
        FormClosing += (s, e) => { SaveSettings(); KillCurrent(); if (phone != null) phone.Stop(); StudioEng.Shutdown(); Separator.KillWorker(); layerRun = false; frameLoop.Dispose(); };

        bg.Dock = DockStyle.Fill;
        bg.Paint += PaintBg;
        bg.Resize += (s, e) => CenterCard();
        Controls.Add(bg);

        // links (batch queue)
        links.Multiline = true; links.ScrollBars = ScrollBars.Vertical; links.AcceptsReturn = true; StyleBox(links);
        links.SetBounds(16, 94, 608, 64); links.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Lbl(links, "BandLab / YouTube links or search terms  (one per line)");

        // format row
        StyleCombo(fmt, FmtNames, 16, 186, 230); Lbl(fmt, "Format");
        StyleCombo(sr, SrNames, 256, 186, 130); Lbl(sr, "Sample rate");
        StyleCombo(nameFmt, NameNames, 396, 186, 228); Lbl(nameFmt, "File name");
        tip.SetToolTip(fmt, "YouTube audio is already lossy (about 128-160 kbps).\nWAV / FLAC keep it intact from that point on, but can't restore lost quality.");
        fmt.SelectedIndexChanged += (s, e) =>
        {
            if (fmt.SelectedIndex == 2 || fmt.SelectedIndex == 3)
                Log("Note: source audio from YouTube is lossy; WAV/FLAC won't add quality, they just avoid a second lossy encode.");
        };

        // trim + processing
        StyleBox(trimA); trimA.SetBounds(16, 240, 100, 26); Lbl(trimA, "Trim start  (m:ss)");
        StyleBox(trimB); trimB.SetBounds(126, 240, 100, 26); Lbl(trimB, "Trim end  (m:ss)");
        StyleChk(norm, "Normalize loudness", 240, 240, 172);
        tip.SetToolTip(norm, "Brings every download to a standard -14 LUFS loudness (streaming level).");
        StyleBtn(prevNormBtn, Color.FromArgb(60, 26, 110)); prevNormBtn.Text = "Hear"; prevNormBtn.SetBounds(416, 240, 50, 26);
        tip.SetToolTip(prevNormBtn, "Listen to a 30 second sample, original vs normalized, before you convert.");
        prevNormBtn.Click += (s, e) => PreviewNormalize();
        StyleChk(playlist, "Whole playlists", 470, 240, 154);
        tip.SetToolTip(playlist, "If a link points at a playlist, convert every video in it.");

        StyleChk(openDone, "Open folder when done", 16, 276, 200);
        StyleChk(animate, "Animated background", 240, 276, 200);
        openDone.Checked = true; animate.Checked = true;

        StyleBtn(mastBtn, Color.FromArgb(90, 36, 160)); mastBtn.Text = "Mastering: OFF";
        mastBtn.SetBounds(464, 274, 160, 30); mastBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(mastBtn, "Open the Mastering Assistant: custom EQ, auto EQ, loudness, stereo width.");
        mastBtn.Click += (s, e) =>
        {
            if (mf == null || mf.IsDisposed) { mf = new MasterForm(this); mf.Show(this); }
            else { mf.Activate(); }
        };

        // output folder
        StyleBtn(vocalBtn, Color.FromArgb(90, 36, 160)); vocalBtn.Text = "Vocal remover...";
        vocalBtn.SetBounds(16, 312, 150, 30);
        tip.SetToolTip(vocalBtn, "Pick a song and get an instrumental (no vocals) plus a vocals-only file, made by an AI model.");
        vocalBtn.Click += (s, e) =>
        {
            var m = Win.NewMenu();
            m.Items.Add("Remove vocals from a song...").Click += (a, b) => RemoveVocals();
            m.Items.Add("Preview: hear a 30 second sample first...").Click += (a, b) => PreviewVocals();
            m.Show(vocalBtn, new Point(0, vocalBtn.Height));
        };
        StyleBtn(phoneBtn, Color.FromArgb(90, 36, 160)); phoneBtn.Text = "Phone mode: OFF";
        phoneBtn.SetBounds(172, 312, 150, 30);
        StyleBtn(whatsBtn, Color.FromArgb(60, 26, 110)); whatsBtn.Text = "Menu"; whatsBtn.SetBounds(560, 12, 64, 28);
        StyleBtn(helpBtn, Color.FromArgb(190, 60, 200)); helpBtn.Text = "Help"; helpBtn.SetBounds(488, 12, 66, 28); helpBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; helpBtn.Click += (s, e) => ShowHelp(null);
        tip.SetToolTip(helpBtn, "Guides for every feature, with Show me on screen tours (F1).");
        KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { e.Handled = true; ShowHelp(null); } }; whatsBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; whatsBtn.Click += (s, e) =>
        {
            var m = Win.NewMenu();
            m.Items.Add("What's new").Click += (a, b) => Changelog.Show(this);
            m.Items.Add("Plugins and extensions...").Click += (a, b) => { new PluginManagerForm(() => ExtraFilter, v => { ExtraFilter = v; SaveSettings(); }).ShowDialog(this); };
            m.Items.Add("Open data folder").Click += (a, b) => { try { Directory.CreateDirectory(AppData); Process.Start("explorer.exe", "\"" + AppData + "\""); } catch { } };
            m.Show(whatsBtn, new Point(0, whatsBtn.Height));
        };
        StyleBtn(studioBtn, Color.FromArgb(90, 36, 160)); studioBtn.Text = "Music Studio"; studioBtn.SetBounds(328, 312, 140, 30);
        tip.SetToolTip(studioBtn, "Open the Logic-style studio: tracks, mixing, Auto-Tune, EQ, effects, mastering and export.");
        studioBtn.Click += (s, e) => OpenStudio();
        StyleBtn(addMusicBtn, Color.FromArgb(190, 60, 200)); addMusicBtn.Text = "ADD MUSIC"; addMusicBtn.Font = new Font("Segoe UI Semibold", 10.5f);
        addMusicBtn.SetBounds(474, 312, 150, 30); addMusicBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(addMusicBtn, "Pick a song, vocal or beat from your PC and open it in the Music Studio.");
        addMusicBtn.Click += (s, e) => { OpenStudio(); studioForm.AddMusicClick(); };
        tip.SetToolTip(phoneBtn, "Use this app from your phone (same Wi-Fi): open a web page, paste a link, download the file.");
        phoneBtn.Click += (s, e) => TogglePhone();

        StyleBox(outDir); outDir.SetBounds(16, 358, 536, 28);
        outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3");
        Lbl(outDir, "Save to");
        StyleBtn(browse, Color.FromArgb(70, 30, 120)); browse.Text = "Browse...";
        browse.SetBounds(560, 357, 64, 30); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browse.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog() == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        StyleBtn(go, Color.FromArgb(120, 40, 210)); go.Text = "Convert";
        go.Font = new Font("Segoe UI Semibold", 11f);
        go.SetBounds(16, 398, 500, 42); go.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        go.Click += (s, e) => Start();

        StyleBtn(cancel, Color.FromArgb(110, 30, 70)); cancel.Text = "Cancel"; cancel.Enabled = false;
        cancel.SetBounds(524, 398, 100, 42); cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Click += (s, e) => { cancelReq = true; KillCurrent(); Log("Cancelling..."); };

        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        StyleBox(log); log.Font = new Font("Consolas", 8.5f);
        log.SetBounds(16, 492, 608, 180);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        bg.Controls.AddRange(new Control[] { links, fmt, sr, nameFmt, trimA, trimB, norm, playlist, openDone, animate,
            mastBtn, vocalBtn, prevNormBtn, helpBtn, phoneBtn, studioBtn, addMusicBtn, whatsBtn, outDir, browse, go, cancel, log });

        fmt.SelectedIndex = 0; sr.SelectedIndex = 0; nameFmt.SelectedIndex = 0;
        LoadSettings();
        PluginHost.LoadEnabled();
        loading = false;
        HookAutoSave();

        Repopulate(); bg.Resize += (s, e) => Repopulate();
        Win.Dark(this); Win.DarkControl(links); Win.DarkControl(log); StyleTips();

        StartLayerWorker(); frameLoop = new FrameLoop(this, Tick) { Slow = () => WindowState == FormWindowState.Minimized || (ActiveForm != this && ActiveForm != studioForm) };
    }

    // ---------- styling helpers ----------
    void Lbl(Control c, string text) { lbls.Add(new KeyValuePair<Control, string>(c, text)); }

    static void StyleBox(TextBox b)
    {
        b.BorderStyle = BorderStyle.FixedSingle; b.BackColor = Field; b.ForeColor = Ink;
    }

    void StyleCombo(ComboBox c, string[] items, int x, int y, int w)
    {
        c.DropDownStyle = ComboBoxStyle.DropDownList; c.FlatStyle = FlatStyle.Flat;
        c.BackColor = Field; c.ForeColor = Ink; c.Items.AddRange(items);
        c.SetBounds(x, y, w, 26);
    }

    static void StyleChk(CheckBox c, string text, int x, int y, int w)
    {
        c.Text = text; c.SetBounds(x, y, w, 26); c.BackColor = Pill; c.ForeColor = Ink;
    }

    static void StyleBtn(Button b, Color c)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = c; b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255);
        b.FlatAppearance.BorderSize = 1; b.Cursor = Cursors.Hand;
    }

    static Label MakeLabel(string text, Font f, Color c, int x, int y, int w, int h)
    {
        var l = new Label { Text = text, Font = f, ForeColor = c, AutoSize = false };
        l.SetBounds(x, y, w, h);
        return l;
    }

    // ---------- settings ----------
    bool loading = true;   // suppress saves while settings are being applied

    internal void SaveSettings()
    {
        if (loading) return;
        try
        {
            Directory.CreateDirectory(AppData);
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            File.WriteAllLines(SettingsFile, new[] {
                "fmt=" + fmt.SelectedIndex, "sr=" + sr.SelectedIndex, "name=" + nameFmt.SelectedIndex,
                "dir=" + outDir.Text, "norm=" + norm.Checked, "playlist=" + playlist.Checked,
                "open=" + openDone.Checked, "anim=" + animate.Checked,
                "x=" + b.X, "y=" + b.Y, "w=" + b.Width, "h=" + b.Height,
                "max=" + (WindowState == FormWindowState.Maximized), "seen=" + seenVer, "ffilter=" + ExtraFilter,
                "master=" + Master.Serialize(), "mzoom=" + MZoomPct });
        }
        catch { }
    }

    // Save the moment anything changes, so nothing is lost on a crash or an update restart.
    void HookAutoSave()
    {
        EventHandler h = (s, e) => SaveSettings();
        fmt.SelectedIndexChanged += h; sr.SelectedIndexChanged += h; nameFmt.SelectedIndexChanged += h;
        norm.CheckedChanged += h; playlist.CheckedChanged += h;
        openDone.CheckedChanged += h; animate.CheckedChanged += h;
        outDir.TextChanged += h;
        ResizeEnd += h;
        LocationChanged += (s, e) => { if (WindowState == FormWindowState.Normal) SaveSettings(); };
    }

    void LoadSettings()
    {
        int wx = 0, wy = 0, ww = 0, wh = 0; bool wmax = false;
        try
        {
            if (!File.Exists(SettingsFile)) return;
            foreach (var line in File.ReadAllLines(SettingsFile))
            {
                int i = line.IndexOf('='); if (i < 1) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                int n; bool b;
                if (k == "fmt" && int.TryParse(v, out n) && n >= 0 && n < fmt.Items.Count) fmt.SelectedIndex = n;
                else if (k == "sr" && int.TryParse(v, out n) && n >= 0 && n < sr.Items.Count) sr.SelectedIndex = n;
                else if (k == "name" && int.TryParse(v, out n) && n >= 0 && n < nameFmt.Items.Count) nameFmt.SelectedIndex = n;
                else if (k == "dir" && v.Length > 0) outDir.Text = v;
                else if (k == "norm" && bool.TryParse(v, out b)) norm.Checked = b;
                else if (k == "playlist" && bool.TryParse(v, out b)) playlist.Checked = b;
                else if (k == "open" && bool.TryParse(v, out b)) openDone.Checked = b;
                else if (k == "anim" && bool.TryParse(v, out b)) animate.Checked = b;
                else if (k == "x" && int.TryParse(v, out n)) wx = n;
                else if (k == "y" && int.TryParse(v, out n)) wy = n;
                else if (k == "w" && int.TryParse(v, out n)) ww = n;
                else if (k == "h" && int.TryParse(v, out n)) wh = n;
                else if (k == "max" && bool.TryParse(v, out b)) wmax = b;
                else if (k == "master") Master.Parse(v);
                else if (k == "seen") seenVer = v;
                else if (k == "ffilter") ExtraFilter = v;
                else if (k == "mzoom" && int.TryParse(v, out n) && n >= 40 && n <= 100) MZoomPct = n;
            }
            // Restore the window only if it still lands on a connected screen.
            if (ww >= MinimumSize.Width && wh >= MinimumSize.Height)
            {
                var rect = new Rectangle(wx, wy, ww, wh);
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.IntersectsWith(rect))
                    {
                        StartPosition = FormStartPosition.Manual; Bounds = rect;
                        if (wmax) WindowState = FormWindowState.Maximized;
                        break;
                    }
            }
        }
        catch { }
    }

    // ---------- animation ----------
    void Tick()
    {
        double now = clock.Elapsed.TotalMilliseconds; float dt = (float)Math.Min(0.05, Math.Max(0.0005, (now - lastClock) / 1000.0)); lastClock = now;
        t += dt; float k30 = dt * 30f;   // the motion constants below were tuned per 1/30 s
        if (animate.Checked)
        {
            foreach (var l in leaves)
            {
                l.Y += l.VY * k30;
                l.X += (float)Math.Sin(t * 0.8f + l.Phase) * l.Sway * k30;
                l.Rot += l.Spin * k30;
                if (l.Y > bg.Height + 120) { var n = NewLeaf(false); l.X = n.X; l.Y = n.Y; l.S = n.S; l.VY = n.VY; l.A = n.A; l.C = n.C; l.CI = n.CI; }
            }
            foreach (var s in spores)
            {
                s.Y -= s.VY * k30;
                s.X += (float)Math.Sin(t + s.Phase) * 0.4f * k30;
                if (s.Y < -5) { var n = NewSpore(false); s.X = n.X; s.Y = n.Y; }
            }
        }
        shownPct += (targetPct - shownPct) * (1f - (float)Math.Pow(0.85, k30));
        string mt = Master.On ? "Mastering: ON" : "Mastering: OFF"; if (mastBtn.Text != mt) mastBtn.Text = mt;
        // pulse the Convert button, but only repaint the real control when the colour visibly changes
        float k = (float)(Math.Sin(t * 2.5f) * 0.5 + 0.5); k = (float)Math.Round(k * 12) / 12f;
        Color gc = busy ? Color.FromArgb(70, 30, 120) : Color.FromArgb((int)(105 + 70 * k), (int)(35 + 30 * k), (int)(195 + 45 * k));
        if (go.BackColor != gc) go.BackColor = gc;
        bg.Invalidate(); bg.Update();
        fpsFrames++;
        if (now >= nextFpsAt) { fpsShown = (int)(fpsFrames * 1000.0 / (now - (nextFpsAt - 1000)) + 0.5); fpsFrames = 0; nextFpsAt = now + 1000; Text = "BandLab + YouTube to MP3 v" + Version + "    " + fpsShown + " fps"; }
    }
    // Leaf outline built once and reused (drawing is the hot path of the animation).
    static readonly GraphicsPath[] LeafPaths = BuildLeafPaths();
    static readonly float[] LeafAng = { 0, -38, 38, -72, 72, -104, 104 }, LeafLen = { 100, 88, 88, 66, 66, 42, 42 };
    static GraphicsPath[] BuildLeafPaths()
    {
        float[] len = { 100, 88, 88, 66, 66, 42, 42 }, wid = { 17, 15, 15, 12, 12, 8, 8 }; var ps = new GraphicsPath[7];
        for (int i = 0; i < 7; i++)
        {
            float L = len[i], w = wid[i]; var p = new GraphicsPath();
            p.AddBezier(0, 0, -w, -L * 0.30f, -w * 0.7f, -L * 0.80f, 0, -L);
            p.AddBezier(0, -L, w * 0.7f, -L * 0.80f, w, -L * 0.30f, 0, 0); ps[i] = p;
        }
        return ps;
    }

    internal static void DrawLeaf(Graphics g, Leaf l, bool glow = false)
    {
        var st = g.Save();
        g.TranslateTransform(l.X, l.Y); g.RotateTransform(l.Rot); g.ScaleTransform(l.S, l.S);
        using (var fill = new SolidBrush(Color.FromArgb(l.A, l.C)))
        using (var edge = new Pen(Color.FromArgb(Math.Min(255, l.A + 60), 225, 190, 255), 1.5f / l.S))
        using (var rib = new Pen(Color.FromArgb(l.A, 235, 215, 255), 1f / l.S))
        using (var gp = glow ? new Pen(Color.FromArgb(34, l.C), 9f / l.S) : null)
        {
            for (int i = 0; i < 7; i++)
            {
                var s2 = g.Save(); g.RotateTransform(LeafAng[i]);
                if (glow) g.DrawPath(gp, LeafPaths[i]);
                g.FillPath(fill, LeafPaths[i]); g.DrawPath(edge, LeafPaths[i]);
                g.DrawLine(rib, 0, 0, 0, -LeafLen[i] * 0.9f);
                g.Restore(s2);
            }
            g.DrawLine(edge, 0, 0, 0, 45);
        }
        g.Restore(st);
    }
    // ---------- helpers ----------
    void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), s); return; }
        log.AppendText(s + Environment.NewLine);
    }

    void SetBusy(bool b)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool>(SetBusy), b); return; }
        busy = b; links.ReadOnly = b; cancel.Enabled = b;
        go.Text = b ? "Working..." : "Convert";
        if (!b) { targetPct = 0; status = ""; itemIdx = 0; itemTotal = 1; }
    }

    void KillCurrent()
    {
        try
        {
            var p = cur;
            if (p != null && !p.HasExited)
                Process.Start(new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false });
        }
        catch { }
    }

    void SetItemPct(float p) { targetPct = (itemIdx * 100f + p) / Math.Max(1, itemTotal); }

    void Download(string from, string to, string what)
    {
        status = what; targetPct = 0;
        using (var wc = new WebClient())
        {
            wc.Headers[HttpRequestHeader.UserAgent] = "YtToMp3";
            var done = new ManualResetEvent(false);
            Exception err = null;
            wc.DownloadProgressChanged += (s, e) => { targetPct = e.ProgressPercentage; };
            wc.DownloadFileCompleted += (s, e) => { err = e.Error; done.Set(); };
            wc.DownloadFileAsync(new Uri(from), to);
            done.WaitOne();
            if (err != null) throw err;
        }
    }

    static string FindFile(string dir, string name)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (var f in Directory.GetFiles(dir, name, SearchOption.AllDirectories)) return f;
        return null;
    }

    internal static string DenoDirPath { get { return DenoDir; } }
    internal static string FindFilePublic(string dir, string name) { return FindFile(dir, name); }
    internal static string FfmpegExe() { return FindFile(FfmpegDir, "ffmpeg.exe"); }
    internal static string FfprobeExe() { return FindFile(FfmpegDir, "ffprobe.exe"); }

    // Most recently written audio file in a folder (used so "Reanalyze" knows what you just made).
    static string NewestAudio(string dir)
    {
        string best = null; DateTime bt = DateTime.MinValue;
        try
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                string x = Path.GetExtension(f).ToLowerInvariant();
                if (x != ".mp3" && x != ".wav" && x != ".flac" && x != ".m4a" && x != ".opus") continue;
                var w = File.GetLastWriteTime(f);
                if (w > bt) { bt = w; best = f; }
            }
        }
        catch { }
        return best;
    }

    // ---- BandLab helpers ----
    // yt-dlp's BandLab extractors accept /track|post|revision/<id>, /embed/?id=, and
    // albums/collections (optionally under a username). Profile pages aren't supported.
    static bool IsBandlab(string s) { return s.IndexOf("bandlab.com", StringComparison.OrdinalIgnoreCase) >= 0; }

    static bool BandlabSupported(string s)
    {
        return Regex.IsMatch(s, @"bandlab\.com/(track|post|revision)/[0-9a-f_-]+|bandlab\.com/embed/|bandlab\.com/(\w+/)?(albums|collections)/[0-9a-f-]+",
            RegexOptions.IgnoreCase);
    }

    static bool BandlabCollection(string s)
    {
        return Regex.IsMatch(s, @"bandlab\.com/(\w+/)?(albums|collections)/|bandlab\.com/embed/collection", RegexOptions.IgnoreCase);
    }

    // Link, bare domain link ("bandlab.com/post/..."), or plain text (-> YouTube search).
    static string NormalizeTarget(string s)
    {
        if (s.IndexOf("://") >= 0) return s;
        if (Regex.IsMatch(s, @"^(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)+(/|$)", RegexOptions.IgnoreCase)) return "https://" + s;
        return "ytsearch1:" + s;
    }

    // Pull links out of the box however they're separated (new lines, commas, spaces).
    // A line with no http(s) links is a bare-domain list ("a.com/x, b.com/y") or a search term.
    static List<string> ParseItems(string text)
    {
        var items = new List<string>();
        foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            var ms = Regex.Matches(line, @"https?://[^\s,;]+");
            if (ms.Count > 0) { foreach (Match m in ms) items.Add(m.Value); continue; }
            string[] parts = line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool allDomains = parts.Length > 1;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
                if (!Regex.IsMatch(parts[i], @"^(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)+/\S*$", RegexOptions.IgnoreCase)) allDomains = false;
            }
            if (allDomains) items.AddRange(parts); else items.Add(line);
        }
        return items;
    }

    static bool ValidTime(string s)
    {
        return s.Length == 0 || Regex.IsMatch(s, @"^\d+(:\d{1,2}){0,2}(\.\d+)?$");
    }

    // ---------- converting ----------
    void Start()
    {
        if (busy) return;
        var items = ParseItems(links.Text);
        if (items.Count == 0) { MessageBox.Show(this, "Paste at least one link (or type a search) first."); return; }
        string a = trimA.Text.Trim(), b = trimB.Text.Trim();
        if (!ValidTime(a) || !ValidTime(b)) { MessageBox.Show(this, "Trim times look like 1:30 or 0:01:30.5"); return; }
        if (outDir.Text.Trim().Length == 0) { MessageBox.Show(this, "Choose a folder to save to."); return; }

        var job = new Job
        {
            Items = items.ToArray(), Dir = outDir.Text.Trim(), TrimA = a, TrimB = b,
            Fmt = fmt.SelectedIndex, Sr = sr.SelectedIndex, Name = nameFmt.SelectedIndex,
            Playlist = playlist.Checked, Normalize = norm.Checked, OpenDone = openDone.Checked,
            Master = Master.On ? Master.Clone() : null,
            ExtraFilter = ExtraFilter
        };
        SaveSettings();
        cancelReq = false;
        SetBusy(true);
        new Thread(() =>
        {
            try { Run(job); }
            catch (Exception ex) { Log("ERROR: " + ex.Message); }
            finally { SetBusy(false); }
        }) { IsBackground = true }.Start();
    }

    void Run(Job job)
    {
        itemIdx = 0; itemTotal = 1;
        Directory.CreateDirectory(Tools);
        Directory.CreateDirectory(job.Dir);

        if (!File.Exists(YtDlp))
        {
            Log("First run: downloading yt-dlp...");
            Download("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", YtDlp, "Downloading yt-dlp...");
        }
        else
        {
            status = "Checking for yt-dlp updates..."; targetPct = 0;
            RunProc(YtDlp, "-U", false);   // keeps site support working as sites change
        }

        string ff = FindFile(FfmpegDir, "ffmpeg.exe");
        if (ff == null)
        {
            Log("First run: downloading ffmpeg (~100 MB, one time)...");
            string zip = Path.Combine(Tools, "ffmpeg.zip");
            Download("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", zip, "Downloading ffmpeg...");
            status = "Unpacking ffmpeg..."; targetPct = 100;
            if (Directory.Exists(FfmpegDir)) Directory.Delete(FfmpegDir, true);
            ZipFile.ExtractToDirectory(zip, FfmpegDir);
            File.Delete(zip);
            ff = FindFile(FfmpegDir, "ffmpeg.exe");
        }
        if (ff == null) throw new Exception("ffmpeg setup failed.");

        // YouTube needs a JavaScript runtime or downloads fail with 403 / missing formats.
        string deno = FindFile(DenoDir, "deno.exe");
        if (deno == null)
        {
            try
            {
                Log("First run: downloading Deno (JavaScript runtime YouTube needs)...");
                string zip = Path.Combine(Tools, "deno.zip");
                Download("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip", zip, "Downloading Deno...");
                if (Directory.Exists(DenoDir)) Directory.Delete(DenoDir, true);
                ZipFile.ExtractToDirectory(zip, DenoDir);
                File.Delete(zip);
                deno = FindFile(DenoDir, "deno.exe");
            }
            catch (Exception ex) { Log("Couldn't set up Deno (" + ex.Message + "); YouTube may fail."); }
        }

        // shared yt-dlp arguments from the chosen options
        bool mastering = job.Master != null;
        string tag = FormatTag(job.Fmt, job.Sr, job.Master, job.Normalize && !mastering);
        string tmpDir = Path.Combine(job.Dir, ".yt2mp3_tmp");
        string common;
        if (mastering)
        {
            // Mastering works from a lossless WAV intermediate, then encodes once to the chosen format.
            common = "-x --audio-format wav --newline --windows-filenames --embed-metadata "
                + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" "
                + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "");
            if (job.Normalize) Log("Note: mastering is on, so the plain \"Normalize loudness\" option is ignored.");
        }
        else
        {
            var ppa = new List<string>();
            if (SrArgs[job.Sr] != null) ppa.Add(SrArgs[job.Sr]);
            if (FmtCodec[job.Fmt] != null) ppa.Add(FmtCodec[job.Fmt]);
            {
                string af = (job.ExtraFilter ?? "").Replace("\"", "").Trim(); if (af.StartsWith("-")) af = "";
                if (job.Normalize) af += (af.Length > 0 ? "," : "") + "loudnorm=I=-14:TP=-1:LRA=11";
                if (af.Length > 0) { ppa.Add("-af " + af); if ((job.ExtraFilter ?? "").Length > 0) Log("Custom ffmpeg filter: " + job.ExtraFilter); }
            }

            common = "-x --audio-format " + FmtExt[job.Fmt]
                + (FmtQual[job.Fmt] != null ? " --audio-quality " + FmtQual[job.Fmt] : "")
                + " --newline --windows-filenames --embed-metadata "
                + (FmtArt[job.Fmt] ? "--embed-thumbnail --convert-thumbnails jpg " : "")
                + (ppa.Count > 0 ? "--postprocessor-args \"ExtractAudio:" + string.Join(" ", ppa.ToArray()) + "\" " : "")
                + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" "
                + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "");
        }
        pctScale = mastering ? 0.6f : 1f;
        Log("Output label: [" + tag + "]");
        if (job.TrimA.Length > 0 || job.TrimB.Length > 0)
            common += "--download-sections \"*" + (job.TrimA.Length > 0 ? job.TrimA : "0") + "-"
                + (job.TrimB.Length > 0 ? job.TrimB : "inf") + "\" --force-keyframes-at-cuts ";

        int ok = 0, fail = 0;
        itemTotal = job.Items.Length;
        for (int i = 0; i < job.Items.Length; i++)
        {
            if (cancelReq) break;
            itemIdx = i;
            string link = job.Items[i];
            string target = NormalizeTarget(link);   // plain text -> YouTube search, top result
            bool bl = IsBandlab(target), coll = bl && BandlabCollection(target);

            status = "Item " + (i + 1) + " of " + itemTotal + ": " + (link.Length > 60 ? link.Substring(0, 60) + "..." : link);
            Log("--- " + (i + 1) + "/" + itemTotal + ": " + link);
            if (bl && !BandlabSupported(target))
                Log("Heads up: that BandLab link isn't a direct track/post/album link, so it may fail. "
                    + "Open the track on BandLab, use Share > Copy link (bandlab.com/post/...), and paste that.");
            else if (coll) Log("BandLab album/collection detected: converting every track.");
            // BandLab albums/collections are always playlists; otherwise honor the checkbox.
            string pl = (job.Playlist || coll) ? "--yes-playlist --ignore-errors " : "--no-playlist ";
            string tpl = mastering ? NameTpl[job.Name] : NameTpl[job.Name] + " [" + tag + "]";
            if (mastering)
            {
                Directory.CreateDirectory(tmpDir);
                foreach (var old in Directory.GetFiles(tmpDir)) { try { File.Delete(old); } catch { } }
            }
            string args = common + pl + "-o \"" + Path.Combine(mastering ? tmpDir : job.Dir, tpl + ".%(ext)s") + "\" \"" + target + "\"";
            int code = RunProc(YtDlp, args, true);
            if (cancelReq) break;
            if (code != 0) { fail++; Log("Failed (exit " + code + ")."); }
            else if (!mastering) ok++;
            else
            {
                var wavs = Directory.GetFiles(tmpDir, "*.wav");
                if (wavs.Length == 0) { fail++; Log("No audio was produced."); }
                foreach (var w in wavs)
                {
                    if (cancelReq) break;
                    status = "Mastering: " + Path.GetFileNameWithoutExtension(w);
                    Log("Mastering " + Path.GetFileName(w));
                    MasterAnalysis res;
                    string outp = MasterDsp.Master(w, job.Dir, job.Master, job.Fmt, job.Sr, tag, Log,
                        p => { cur = p; }, () => cancelReq,
                        pr => { targetPct = (itemIdx * 100f + 60f + 40f * pr) / Math.Max(1, itemTotal); }, out res);
                    if (outp != null)
                    {
                        ok++; LastFile = outp; if (res != null) LastAn = res;
                        try { File.Delete(w); } catch { }
                        Log("Saved: " + Path.GetFileName(outp));
                    }
                    else if (!cancelReq)
                    {
                        fail++;
                        try { File.Move(w, Path.Combine(job.Dir, Path.GetFileName(w))); } catch { }
                        Log("Mastering failed; kept the unmastered WAV in your folder.");
                    }
                }
            }
            SetItemPct(100);
        }
        pctScale = 1f;
        try { if (Directory.Exists(tmpDir) && Directory.GetFiles(tmpDir).Length == 0) Directory.Delete(tmpDir); } catch { }
        if (!mastering && ok > 0) { string nf = NewestAudio(job.Dir); if (nf != null) LastFile = nf; }

        if (cancelReq) { Log("Cancelled."); status = "Cancelled"; return; }
        targetPct = 100; status = "Done: " + ok + " converted" + (fail > 0 ? ", " + fail + " failed" : "");
        Log(status + ". Saved to " + job.Dir);
        if (ok > 0 && job.OpenDone) Process.Start("explorer.exe", "\"" + job.Dir + "\"");
        Thread.Sleep(1500);
    }

    // ---------- vocal remover (AI source separation) ----------
    void RemoveVocals()
    {
        if (busy) return;
        Separator.Warmup();   // load the AI model while the file dialog is open
        if (!Separator.Installed)
        {
            if (MessageBox.Show(this,
                "The vocal remover uses an AI model (Demucs, MIT licence) that runs on your PC.\n\n" +
                "One-time download: about 0.5 GB (a private copy of Python, PyTorch and the model). It's stored in your AppData folder and nothing is installed system-wide.\n\n" +
                "Download and set it up now?", "Vocal remover setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        }
        string file;
        using (var d = new OpenFileDialog())
        {
            d.Title = "Choose a song to remove the vocals from";
            d.Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.aiff;*.aif|All files|*.*";
            try { if (LastFile != null && File.Exists(LastFile)) d.InitialDirectory = Path.GetDirectoryName(LastFile); } catch { }
            if (d.ShowDialog(this) != DialogResult.OK) return;
            file = d.FileName;
        }
        int f = fmt.SelectedIndex; string dir = outDir.Text.Trim(); bool open = openDone.Checked;
        if (dir.Length == 0) { MessageBox.Show(this, "Choose a folder to save to."); return; }
        cancelReq = false; SetBusy(true);
        new Thread(() =>
        {
            try { DoRemoveVocals(file, dir, f, open); }
            catch (Exception ex) { Log("ERROR: " + ex.Message); status = "Failed"; }
            finally { SetBusy(false); }
        }) { IsBackground = true }.Start();
    }

    static string FmtEta(double s) { if (s < 90) return (int)s + " s"; if (s < 5400) return (int)Math.Ceiling(s / 60) + " min"; return (s / 3600).ToString("0.0") + " h"; }

    void DoRemoveVocals(string file, string dir, int f, bool open)
    {
        if (FfmpegExe() == null) throw new Exception("ffmpeg isn't installed yet. Convert something once first (that installs ffmpeg).");
        Directory.CreateDirectory(dir); itemIdx = 0; itemTotal = 1; pctScale = 1f;
        bool installNow = !Separator.Installed; float lo = 0f, hi = 100f;
        if (installNow)
        {
            status = "Setting up the vocal remover (one time)..."; lo = 50f; targetPct = 0;
            Separator.Install(Log, p => { targetPct = p * 50f; }, () => cancelReq);
        }
        if (cancelReq) { Log("Cancelled."); status = "Cancelled"; return; }
        status = "Separating vocals with AI (this can take a few minutes)..."; targetPct = lo;
        string baseName = Path.GetFileNameWithoutExtension(file), tmp = Path.Combine(Path.GetTempPath(), "yt2mp3_sep_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        string v, inst; var swSep = Stopwatch.StartNew();
        try
        {
            if (!Separator.Separate(file, tmp, baseName, "htdemucs", Log, p => { targetPct = lo + p * (hi - lo) * 0.92f; status = "Separating vocals with AI... " + (p > 0.04f ? "about " + FmtEta(swSep.Elapsed.TotalSeconds * (1 - p) / p) + " left" : "starting"); }, () => cancelReq, out v, out inst))
            { status = cancelReq ? "Cancelled" : "Failed"; return; }
            status = "Encoding..."; string ext = MasterDsp.FmtExt[f], tag = FmtTag[f]; int code;
            string outInst = Path.Combine(dir, baseName + " [instrumental " + tag + "]." + ext), outVoc = Path.Combine(dir, baseName + " [vocals " + tag + "]." + ext);
            string ff = FfmpegExe();
            MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + inst + "\" -vn " + MasterDsp.FmtEnc[f] + " \"" + outInst + "\"", p => { cur = p; }, out code);
            if (code != 0 || !File.Exists(outInst)) throw new Exception("Couldn't encode the instrumental.");
            MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + v + "\" -vn " + MasterDsp.FmtEnc[f] + " \"" + outVoc + "\"", p => { cur = p; }, out code);
            if (code != 0 || !File.Exists(outVoc)) throw new Exception("Couldn't encode the vocals.");
            LastFile = outInst; targetPct = 100; status = "Done";
            Log("Saved: " + Path.GetFileName(outInst)); Log("Saved: " + Path.GetFileName(outVoc));
            if (open) Process.Start("explorer.exe", "\"" + dir + "\"");
            Thread.Sleep(1200);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    // ---------- phone mode ----------
    void TogglePhone()
    {
        if (phone != null)
        {
            phone.Stop(); phone = null; phoneBtn.Text = "Phone mode: OFF"; Log("Phone mode turned off."); return;
        }
        var ps = new PhoneServer { Log = Log }; string err;
        if (!ps.Start(out err)) { MessageBox.Show(this, "Couldn't start phone mode: " + err + "\n\nAnother program may be using port 8765.", "Phone mode"); return; }
        phone = ps; phoneBtn.Text = "Phone mode: ON";
        Log("Phone mode is ON. On your phone (same Wi-Fi) open:"); Log("  " + ps.Url);
        PhoneUi.Show(this, ps);
    }

    int RunProc(string exe, string args, bool progress)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        var p = new Process { StartInfo = psi };
        DataReceivedEventHandler h = (s, e) =>
        {
            if (e.Data == null) return;
            if (progress && e.Data.StartsWith("[download]") && e.Data.Contains("%"))
            {
                int i = e.Data.IndexOf('%');
                int st = e.Data.LastIndexOf(' ', i) + 1;
                double v;
                if (double.TryParse(e.Data.Substring(st, i - st),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v)) SetItemPct((float)v * pctScale);
            }
            Log(e.Data);
        };
        p.OutputDataReceived += h; p.ErrorDataReceived += h;
        p.Start(); cur = p;
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.WaitForExit();
        cur = null;
        return p.ExitCode;
    }

    // ---------- self-update ----------
    // Runs for the life of the app: checks right away, then every few minutes, so an update
    // published while the app is open pops up live instead of waiting for a restart.
    const int UpdateCheckSeconds = 20;
    const string LatestReleasePage = "https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest";
    const string LatestExeUrl = "https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest/download/YtToMp3.exe";
    string dismissedVer = "";
    volatile bool updating = false;

    void UpdateLoop()
    {
        while (true)
        {
            try { CheckForUpdate(); } catch { }
            Thread.Sleep(UpdateCheckSeconds * 1000);
        }
    }

    void CheckForUpdate()
    {
        if (updating || card != null) return;
        string newVer = null, dl = LatestExeUrl;
        try
        {
            // /releases/latest redirects to /releases/tag/vX.Y.Z. It is uncached and doesn't use
            // the API rate limit, so we can poll every few seconds and see a release right away.
            var req = (HttpWebRequest)WebRequest.Create(LatestReleasePage);
            req.UserAgent = "YtToMp3";
            req.Method = "HEAD";
            req.AllowAutoRedirect = false;
            req.Timeout = 10000;
            req.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                var m = Regex.Match(resp.Headers["Location"] ?? "", "/tag/v?([0-9][0-9.]*)$");
                if (m.Success) newVer = m.Groups[1].Value;
            }
        }
        catch { }
        if (newVer == null && UpdateManifestUrl.Length > 0)
        {
            try
            {
                using (var wc = new WebClient())
                {
                    string[] lines = wc.DownloadString(UpdateManifestUrl + "?t=" + DateTime.UtcNow.Ticks)
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2) { newVer = lines[0].Trim(); dl = lines[1].Trim(); }
                }
            }
            catch { }
        }
        if (newVer == null) return;   // offline or unreadable: try again next round
        System.Version nv;
        if (!System.Version.TryParse(newVer, out nv) || nv <= new System.Version(Version)) return;
        if (newVer == dismissedVer) return;   // user said "Later" for this one
        var notes = new List<string>();
        try
        {
            using (var wc = new WebClient())
            {
                string[] lines = wc.DownloadString(UpdateManifestUrl + "?t=" + DateTime.UtcNow.Ticks).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 2 && lines[0].Trim() == newVer) for (int i = 2; i < lines.Length && notes.Count < 8; i++) notes.Add(lines[i].Trim());
            }
        }
        catch { }
        BeginInvoke(new Action(() => ShowUpdateCard(newVer, dl, notes)));
    }

    // In-app update popup (a card over the window instead of a separate message box).
    void ShowUpdateCard(string newVer, string dl, List<string> notes)
    {
        if (card != null) return;
        int nh = notes.Count > 0 ? notes.Count * 20 + 30 : 0;
        card = new Panel { Size = new Size(470, 160 + nh), BackColor = Color.FromArgb(14, 7, 28), BorderStyle = BorderStyle.FixedSingle };
        var title = MakeLabel("Update available", new Font("Segoe UI Semibold", 14f), Color.FromArgb(225, 190, 255), 16, 14, 430, 28);
        var msg = MakeLabel("Version " + newVer + " is ready (you have " + Version + ").\nUpdate now? The app restarts automatically.", Font, Ink, 16, 48, 430, 40);
        var ctl = new List<Control> { title, msg };
        if (notes.Count > 0)
        {
            ctl.Add(MakeLabel("What's new", new Font("Segoe UI Semibold", 10.5f), Color.FromArgb(236, 120, 230), 16, 94, 200, 22));
            ctl.Add(MakeLabel(string.Join("\n", notes.ToArray()), new Font("Segoe UI", 9.5f), Ink, 16, 116, 438, notes.Count * 20 + 4));
        }
        var yes = new Button(); StyleBtn(yes, Color.FromArgb(130, 45, 220)); yes.Text = "Update now"; yes.SetBounds(16, 108 + nh, 205, 36);
        var no = new Button(); StyleBtn(no, Color.FromArgb(60, 26, 100)); no.Text = "Later"; no.SetBounds(235, 108 + nh, 205, 36);
        yes.Click += (s, e) => { HideCard(); ApplyUpdate(dl); };
        no.Click += (s, e) => { dismissedVer = newVer; HideCard(); };
        ctl.Add(yes); ctl.Add(no); card.Controls.AddRange(ctl.ToArray());
        bg.Controls.Add(card);
        CenterCard();
        card.BringToFront();
        Activate();
    }
    void HideCard()
    {
        if (card == null) return;
        bg.Controls.Remove(card); card.Dispose(); card = null;
    }

    void CenterCard()
    {
        if (card != null)
            card.Location = new Point((bg.ClientSize.Width - card.Width) / 2, Math.Max(10, (bg.ClientSize.Height - card.Height) / 2 - 20));
    }

    void ApplyUpdate(string downloadUrl)
    {
        updating = true; SetBusy(true); Log("Downloading update...");
        new Thread(() =>
        {
            string exe = Application.ExecutablePath, tmp = exe + ".new", old = exe + ".old";
            try
            {
                Download(downloadUrl, tmp, "Downloading update...");
                if (new FileInfo(tmp).Length < 10000) throw new Exception("Downloaded file looks invalid.");
                try { File.Delete(old); } catch { }
                File.Move(exe, old);          // a running exe can be renamed, not overwritten
                File.Move(tmp, exe);
                SaveSettings();
                Process.Start(exe);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(old) && !File.Exists(exe)) File.Move(old, exe); } catch { }
                Log("Update failed: " + ex.Message);
                updating = false;
                SetBusy(false);
            }
        }) { IsBackground = true }.Start();
    }
}
