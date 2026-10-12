using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
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
    const string Version = "3.33.0";
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
        "MP3  320 kbps (best MP3 quality)", "WAV  24-bit PCM", "FLAC  (lossless container)",
        "M4A  AAC 256 kbps", "Opus  (best quality per size)",
        "MP4  VIDEO  best quality (up to 4K+)", "MP4  VIDEO  1440p", "MP4  VIDEO  1080p", "MP4  VIDEO  720p", "MP4  VIDEO  480p", "MP4  VIDEO  360p", "MKV  VIDEO  best quality (no re-encode)" };
    // video entries: the tallest picture to accept (9999 = no limit) and the container; 0 = an audio format
    static readonly int[] FmtVid = { 0, 0, 0, 0, 0, 9999, 1440, 1080, 720, 480, 360, 9999 };
    static readonly string[] FmtBox = { null, null, null, null, null, "mp4", "mp4", "mp4", "mp4", "mp4", "mp4", "mkv" };
    static readonly string[] FmtExt = { "mp3", "wav", "flac", "m4a", "opus", "mp4", "mp4", "mp4", "mp4", "mp4", "mp4", "mkv" };
    static readonly string[] FmtQual = { "320K", null, null, "256K", null, null, null, null, null, null, null, null };
    static readonly bool[] FmtArt = { true, false, true, true, false, false, false, false, false, false, false, false };
    static readonly string[] FmtCodec = { null, "-c:a pcm_s24le", null, null, null, null, null, null, null, null, null, null };
    static readonly string[] FmtTag = { "MP3 320k", "WAV 24bit", "FLAC", "AAC 256k", "Opus", "MP4 best", "MP4 1440p", "MP4 1080p", "MP4 720p", "MP4 480p", "MP4 360p", "MKV best" };

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

    static readonly string[] SrNames = { "Original", "44.1 kHz", "48 kHz", "88.2 kHz", "96 kHz", "176.4 kHz", "192 kHz", "352.8 kHz", "384 kHz (highest)" };
    static readonly string[] SrArgs = { null, "-ar 44100", "-ar 48000", "-ar 88200", "-ar 96000", "-ar 176400", "-ar 192000", "-ar 352800", "-ar 384000" };
    static readonly int[] SrHz = { 0, 44100, 48000, 88200, 96000, 176400, 192000, 352800, 384000 };
    // the highest rate each format can really hold: MP3 and Opus stop at 48 kHz, AAC (M4A) at 96 kHz
    static bool FmtOk(int i) { return Edition.Has(Tier.Supporter) || i == 0 || (i >= 5 && i <= 10); }   // the Free Edition: MP3 and the MP4 videos
    static bool SrOk(int i) { return i < 0 || i >= SrHz.Length || SrHz[i] == 0 || SrHz[i] <= Edition.MaxRate; }
    int lastFmtOk, lastSrOk; bool relabel;
    // the entries the edition does not have say so in their name
    void RelabelLocks()
    {
        relabel = true; try
        {
            int a = fmt.SelectedIndex, b = sr.SelectedIndex;
            for (int i = 0; i < FmtNames.Length && i < fmt.Items.Count; i++) { string t = FmtNames[i] + (FmtOk(i) ? "" : "   (Supporter)"); if ((string)fmt.Items[i] != t) fmt.Items[i] = t; }
            for (int i = 0; i < SrNames.Length && i < sr.Items.Count; i++) { string t = SrNames[i] + (SrOk(i) ? "" : (SrHz[i] > 192000 ? "   (Fantasy)" : "   (Supporter)")); if ((string)sr.Items[i] != t) sr.Items[i] = t; }
            if (fmt.SelectedIndex != a) fmt.SelectedIndex = a; if (sr.SelectedIndex != b) sr.SelectedIndex = b;
        }
        finally { relabel = false; }
    }
    void HookEditionGates()
    {
        fmt.SelectedIndexChanged += (s, e) => { if (relabel) return; int i = fmt.SelectedIndex; if (i < 0) return; if (FmtOk(i)) { lastFmtOk = i; return; } bool quiet = loading; fmt.SelectedIndex = lastFmtOk; if (!quiet) Edition.Upsell(Tier.Supporter, "That format", this); };
        sr.SelectedIndexChanged += (s, e) => { if (relabel) return; int i = sr.SelectedIndex; if (i < 0) return; if (SrOk(i)) { lastSrOk = i; return; } bool quiet = loading; sr.SelectedIndex = lastSrOk; if (!quiet) Edition.Upsell(SrHz[i] > 192000 ? Tier.Fantasy : Tier.Supporter, "Exporting at " + SrNames[i].Replace(" (highest)", ""), this); };
        Edition.OnShowAccount = () => ShowSettings(true);
        Edition.Changed += () => { RelabelLocks(); Edition.RepaintLocked(); Themes.EnforceEdition(); if (!Edition.Has(Tier.Supporter) && norm.Checked) norm.Checked = false; try { bg.Invalidate(); foreach (Form f in Application.OpenForms) f.Invalidate(true); } catch { } };
        RelabelLocks(); Themes.EnforceEdition(); Edition.Refresh(); Edition.SelfCheck(Version, UpdateManifestUrl); Application.ApplicationExit += (s, e) => Edition.SaveSeen();
        var edTimer = new System.Windows.Forms.Timer { Interval = 6 * 3600 * 1000 }; edTimer.Tick += (s, e) => Edition.Refresh(); edTimer.Start();
    }
    static string SrArg(int srIdx, int fmtIdx) { if (srIdx <= 0 || srIdx >= SrArgs.Length) return null; int max = fmtIdx == 0 || fmtIdx == 4 ? 48000 : (fmtIdx == 3 ? 96000 : 384000); int hz = Math.Min(Math.Min(SrHz[srIdx], max), Edition.MaxRate); return "-ar " + hz; }
    static readonly string[] NameNames = { "Title", "Artist - Title", "Title [video id]" };
    static readonly string[] NameTpl = { "%(title).150B", "%(artist,uploader)s - %(title).150B", "%(title).140B [%(id)s]" };

    BgPanel bg = new BgPanel();
    TextBox links = new TextBox(), outDir = new TextBox(), log = new TextBox(), trimA = new TextBox(), trimB = new TextBox();
    ComboBox fmt = new DarkCombo(), sr = new DarkCombo(), nameFmt = new DarkCombo();
    CheckBox playlist = new DarkCheck(), norm = new DarkCheck(), openDone = new DarkCheck(), animate = new DarkCheck();
    Button go = new PxButton(), cancel = new PxButton(), browse = new PxButton();
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
    Button lyricsBtn = new PxButton(), usedBtn = new PxButton();
    Button mastBtn = new PxButton(), vocalBtn = new PxButton(), whatsBtn = new PxButton(), prevNormBtn = new PxButton(), helpBtn = new PxButton(); string seenVer = ""; internal string ExtraFilter = ""; Button changeBtn = new PxButton(); Button settingsBtn = new PxButton();
    internal StudioEngine StudioEng = new StudioEngine();
    StudioForm studioForm; Button studioBtn = new PxButton(), addMusicBtn = new PxButton();
    internal void ShowThemes(bool community) { if (OpenStudio()) studioForm.OpenThemeSheet(community); }

    HelpForm helpForm;
    internal void ShowHelp(string topic)
    {
        // not modal: you can keep using and moving the other windows while Help is open
        if (helpForm != null && !helpForm.IsDisposed) helpForm.Close();
        helpForm = new HelpForm(id => true, id => { string tid = id; BeginInvoke(new Action(() => RunTour(tid))); }, topic); helpForm.Show(); Win.Bring(helpForm);   // its own window (no owner), so it can never end up hidden behind another one
    }

    void RunTour(string id)
    {
        Func<Control, Func<Rectangle>> R = c => (() => new Rectangle(c.Left, c.Top, c.Width, c.Height)); var s = new List<TourStep>();
        if (id.StartsWith("target:")) { if (OpenStudio()) studioForm.ShowOnScreen(id.Substring(7)); return; }
        if (id.StartsWith("studio") || id == "daw" || id == "makesong" || id == "typing") { if (OpenStudio()) studioForm.RunGuide(id); return; }
        if (id == "start") { s.Add(new TourStep(R(links), "1. Paste a link", "Paste one or more links here (YouTube and 1000+ other video and music sites), or type words to search.")); s.Add(new TourStep(R(fmt), "2. Pick a format", "MP3 320 kbps is the best all-round choice.")); s.Add(new TourStep(R(go), "3. Press Convert", "Your file is saved in the Save to folder, which opens when it is done.")); }
        else if (id == "normalize") { s.Add(new TourStep(R(norm), "Normalize loudness", "Tick this to bring downloads to a standard -14 LUFS loudness.")); s.Add(new TourStep(R(prevNormBtn), "Hear it first", "Press Hear to listen to a 30 second sample, original versus normalized, before you convert.")); }
        else if (id == "vocal") { s.Add(new TourStep(R(vocalBtn), "Vocal remover", "Click this, then choose Remove vocals from a song, or Preview to hear a 30 second sample first.")); s.Add(new TourStep(R(outDir), "Where files go", "The instrumental and the vocals-only file are saved in this folder.")); }
        else if (id == "masterassistant") { s.Add(new TourStep(R(addMusicBtn), "Purphex Mastering", "Press ADD MUSIC, then Edit in Purphex Music. The Mastering tab there has the live EQ, loudness and width.")); }
        else if (id == "formats") { s.Add(new TourStep(R(fmt), "1. Pick a format", "MP3 320 kbps is the best all-round choice. M4A suits Apple devices, FLAC and WAV are for editing, Opus makes the smallest files.")); s.Add(new TourStep(R(sr), "2. Sample rate", "Leave it on Original unless your project needs 44.1 kHz or 48 kHz.")); s.Add(new TourStep(R(nameFmt), "3. File names", "Choose how the finished files are named.")); }
        else if (id == "batch") { s.Add(new TourStep(R(links), "1. Paste several links", "One per line, or separated by commas. They are converted one after another.")); s.Add(new TourStep(R(playlist), "2. Whole playlists", "Tick this to convert every video of a playlist link. Leave it off for just the one video.")); s.Add(new TourStep(R(cancel), "3. Cancel", "Stops at any time. Files already finished are kept.")); }
        else if (id == "trim") { s.Add(new TourStep(R(trimA), "1. Trim start", "Type where to start, such as 1:30. Leave it empty to begin at 0:00.")); s.Add(new TourStep(R(trimB), "2. Trim end", "Type where to stop. Leave it empty to go to the end of the song.")); }
        else if (id == "download-video") { s.Add(new TourStep(R(fmt), "Pick MP4 here", "Choose one of the MP4 video qualities in this list, paste the link and press Convert.")); }
        else if (id == "settings") { s.Add(new TourStep(R(settingsBtn), "Settings", "Background picture or video, colour theme, animation speed, audio device and buffer, and updates. It has a sidebar of sections and a SAVE button at the top and the bottom.")); }
        else if (id == "updates") { s.Add(new TourStep(R(changeBtn), "What changed", "The Changelog shows what is new in each version.")); s.Add(new TourStep(R(settingsBtn), "Scan for updates", "Open Settings, then the UPDATES section, to scan for a new version.")); }
        else if (id == "fullscreen") { s.Add(new TourStep(() => Rectangle.Empty, "Full screen", "Press F11 in any window to fill the screen, and Esc (or the MINIMISE button) to come back. Pop-up boxes can be dragged by their top strip.")); }
        if (s.Count > 0) Tour.Run(this, s);
    }

    bool OpenStudio()
    {
        if (!Edition.Gate(Tier.Supporter, "The Purphex Music studio", this)) return false;
        if (studioForm == null || studioForm.IsDisposed) { studioForm = new StudioForm(this); studioForm.Show(); Win.Bring(studioForm); }   // an independent window: click either window and it comes to the front
        else Win.Bring(studioForm);
        return true;
    }

    static Color Ink { get { return TM.C(255, 235, 225, 255); } }
    static Color Field { get { return TM.C(255, 8, 4, 16); } }
    static Color Pill { get { return TM.C(255, 14, 7, 28); } }

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
        try { File.Delete(Application.ExecutablePath + ".old"); } catch { }
        Application.EnableVisualStyles(); Win.WidenBuffers(); CrashGuard.Install();   // a problem is logged to crash.log, not shown as the grey .NET box
        Application.Run(new App());
    }

    App()
    {
        Text = "Purphex  v" + Version;
        ClientSize = new Size(640, 712);
        MinimumSize = new Size(560, 640);
        StudioEnv.Ffmpeg = FfmpegExe; StudioEnv.Ffprobe = FfprobeExe; UserBackground.Ffmpeg = FfmpegExe; Prefs.Changed = ApplyPrefs;
        Font = new Font("Segoe UI", 9.5f);
        Shown += (s, e) =>
        {
            Activated += (s2, e2) => ThreadPool.QueueUserWorkItem(_ => { try { CheckForUpdate(); } catch { } });   // coming back to the app checks for a new version right away
            BeginInvoke(new Action(() => { try { PluginHost.LoadEnabled(); } catch { } }));   // on the UI thread (VST plugins expect it), after the window is visible
            new Thread(UpdateLoop) { IsBackground = true }.Start();
        };
        FormClosing += (s, e) => { var others = new List<Form>(); foreach (Form of in Application.OpenForms) if (of != this) others.Add(of); foreach (var of2 in others) { try { of2.Close(); } catch { } } };   // closing the main window closes the others
        FormClosing += (s, e) => { SaveSettings(); KillCurrent(); StudioEng.Shutdown(); Separator.KillWorker(); layerRun = false; UserBackground.StopVideo(); frameLoop.Dispose(); };

        bg.Dock = DockStyle.Fill;
        bg.Paint += PaintBg;
        bg.Resize += (s, e) => CenterCard();
        Controls.Add(bg);

        // links (batch queue)
        links.Multiline = true; links.ScrollBars = ScrollBars.Vertical; links.AcceptsReturn = true; StyleBox(links); NeonScroll.Attach(links);
        links.SetBounds(16, 98, 608, 54); links.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Lbl(links, "Paste video or music links, or type words to search  (one per line)");

        // format row
        StyleCombo(fmt, FmtNames, 16, 190, 230); fmt.DropDownWidth = 330; Lbl(fmt, "Format");
        StyleCombo(sr, SrNames, 256, 190, 130); Lbl(sr, "Sample rate");
        StyleCombo(nameFmt, NameNames, 396, 190, 228); Lbl(nameFmt, "File name");
        tip.SetToolTip(fmt, "YouTube audio is already lossy (about 128-160 kbps).\nWAV / FLAC keep it intact from that point on, but can't restore lost quality.");
        fmt.SelectedIndexChanged += (s, e) =>
        {
            bool vid = FmtVid[Math.Max(0, fmt.SelectedIndex)] > 0; sr.Enabled = !vid; norm.Enabled = !vid; prevNormBtn.Enabled = !vid; if (vid) Log("Video mode: your file keeps the original picture and sound. Pick a quality in the Format list."); if (fmt.SelectedIndex == 1 || fmt.SelectedIndex == 2)
                Log("Note: source audio from YouTube is lossy; WAV/FLAC won't add quality, they just avoid a second lossy encode.");
        };

        // trim + processing
        StyleBox(trimA); trimA.SetBounds(16, 246, 84, 28); Lbl(trimA, "Trim start"); tip.SetToolTip(trimA, "Where the file should start, like 1:30 (minutes:seconds). Leave empty to start at the beginning.");
        StyleBox(trimB); trimB.SetBounds(108, 246, 84, 28); Lbl(trimB, "Trim end"); tip.SetToolTip(trimB, "Where the file should end, like 3:45 (minutes:seconds). Leave empty to go to the end.");
        StyleChk(norm, "Normalize loudness", 200, 246, 192);
        tip.SetToolTip(norm, "Brings every download to a standard -14 LUFS loudness (streaming level).");
        StyleBtn(prevNormBtn, TM.C(255, 60, 26, 110)); prevNormBtn.Text = "Hear"; prevNormBtn.SetBounds(398, 246, 54, 28);
        tip.SetToolTip(prevNormBtn, "Listen to a 30 second sample, original vs normalized, before you convert.");
        prevNormBtn.Click += (s, e) => { if (Edition.Gate(Tier.Supporter, "Hearing the normalized sample", this)) PreviewNormalize(); }; Edition.Lock(prevNormBtn, Tier.Supporter); Edition.Lock(norm, Tier.Supporter);
        norm.CheckedChanged += (s, e) => { if (norm.Checked && !Edition.Has(Tier.Supporter)) { norm.Checked = false; if (!loading) Edition.Upsell(Tier.Supporter, "Normalize loudness", this); } };
        StyleChk(playlist, "Whole playlists", 458, 246, 166);
        tip.SetToolTip(playlist, "If a link points at a playlist, convert every video in it.");

        StyleChk(openDone, "Open folder when done", 16, 279, 236);
        StyleChk(animate, "Animated background", 258, 279, 236);
        openDone.Checked = true; animate.Checked = true;

        StyleBtn(mastBtn, TM.C(255, 90, 36, 160)); mastBtn.Text = "Mastering: OFF";
        mastBtn.SetBounds(464, 274, 160, 30); mastBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(mastBtn, "Master your own song: opens Purphex Music on its Mastering tab, where you can add a song, master it and export it.");
        mastBtn.Text = "Master a song";
        mastBtn.Click += (s, e) =>
        {
            if (OpenStudio()) studioForm.ShowMastering();
        };
        mastBtn.Visible = false;   // the main window is kept simple: mastering is on the Mastering tab inside Purphex Music

        // output folder
        StyleBtn(vocalBtn, TM.C(255, 90, 36, 160)); vocalBtn.Text = "Vocal remover...";
        vocalBtn.SetBounds(16, 312, 140, 30);
        tip.SetToolTip(vocalBtn, "Pick a song and get an instrumental (no vocals) plus a vocals-only file, made by an AI model.");
        vocalBtn.Click += (s, e) =>
        {
            var m = Win.NewMenu();
            m.Items.Add("Remove vocals from a song...").Click += (a, b) => RemoveVocals();
            m.Items.Add("Preview: hear a 30 second sample first...").Click += (a, b) => PreviewVocals();
            m.Show(vocalBtn, new Point(0, vocalBtn.Height));
        };
        StyleBtn(whatsBtn, TM.C(255, 60, 26, 110)); whatsBtn.Text = "Menu"; whatsBtn.SetBounds(560, 12, 64, 28);
        StyleBtn(helpBtn, TM.C(255, 190, 60, 200)); helpBtn.Text = "Help"; helpBtn.SetBounds(490, 12, 64, 28); helpBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; helpBtn.Click += (s, e) => ShowHelp(null);
        tip.SetToolTip(helpBtn, "Guides for every feature, with Show me on screen tours (F1).");
        StyleBtn(settingsBtn, TM.C(255, 70, 40, 150)); settingsBtn.Text = "Settings"; settingsBtn.SetBounds(400, 12, 84, 28); settingsBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; settingsBtn.Click += (s, e) => ShowSettings();
        tip.SetToolTip(settingsBtn, "Background picture or video, animation, audio and other options.");
        StyleBtn(changeBtn, TM.C(255, 60, 26, 110)); changeBtn.Text = "Changelog"; changeBtn.SetBounds(290, 12, 104, 28); changeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; changeBtn.Click += (s, e) => { seenVer = Changelog.Latest; SaveSettings(); changeBtn.Text = "Changelog"; Changelog.Show(this); };
        tip.SetToolTip(changeBtn, "See what changed in each version (nothing pops up by itself any more).");
        KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { e.Handled = true; ShowHelp(null); } }; whatsBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; whatsBtn.Click += (s, e) =>
        {
            var m = Win.NewMenu();
            m.Items.Add("Settings...").Click += (a, b) => ShowSettings();
            m.Items.Add("Generate lyrics from a song or video...").Click += (a, b) => ShowLyricsGen();
            m.Items.Add("Full screen  (F11)").Click += (a, b) => Win.ToggleFullscreen(this);
            m.Items.Add("Mastering for downloads (Purphex Mastering)...").Click += (a, b) => { if (!Edition.Gate(Tier.Supporter, "Mastering", this)) return; if (mf == null || mf.IsDisposed) { mf = new MasterForm(this); mf.Show(); Win.Bring(mf); } else Win.Bring(mf); };
            m.Items.Add("What's new").Click += (a, b) => Changelog.Show(this);
            m.Items.Add("Plugins and extensions...").Click += (a, b) => { if (!Edition.Gate(Tier.Supporter, "Plugins and extensions", this)) return; new PluginManagerForm(() => ExtraFilter, v => { ExtraFilter = v; SaveSettings(); }).Show(); };
            m.Items.Add("Open data folder").Click += (a, b) => { try { Directory.CreateDirectory(AppData); Process.Start("explorer.exe", "\"" + AppData + "\""); } catch { } };
            m.Show(whatsBtn, new Point(0, whatsBtn.Height));
        };
        StyleBtn(studioBtn, TM.C(255, 90, 36, 160)); studioBtn.Text = "Purphex Music"; studioBtn.SetBounds(162, 312, 140, 30);
        StyleBtn(usedBtn, TM.C(255, 70, 40, 150)); usedBtn.Text = "Times used"; usedBtn.SetBounds(196, 12, 88, 28); usedBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;   // next to Changelog, clear of the Purphex logo
        tip.SetToolTip(usedBtn, "How many MP3s and MP4s have been made with Purphex Music all over the world.");
        usedBtn.Click += (s, e) => ShowUsage();
        StyleBtn(lyricsBtn, TM.C(255, 150, 60, 210)); lyricsBtn.Text = "Generate lyrics"; lyricsBtn.SetBounds(308, 312, 140, 30);
        tip.SetToolTip(lyricsBtn, "Add any song or video and get its lyrics, tidied and labelled. Free in every edition.");
        lyricsBtn.Click += (s, e) => ShowLyricsGen();
        tip.SetToolTip(studioBtn, "Open the Logic-style studio: tracks, mixing, Auto-Tune, EQ, effects, mastering and export.");
        studioBtn.Click += (s, e) => OpenStudio(); Edition.Lock(studioBtn, Tier.Supporter);
        StyleBtn(addMusicBtn, TM.C(255, 190, 60, 200)); addMusicBtn.Text = "ADD MUSIC"; addMusicBtn.Font = new Font("Segoe UI Semibold", 10.5f);
        addMusicBtn.SetBounds(454, 312, 170, 30); addMusicBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(addMusicBtn, "Add music from your PC: edit it in Purphex Music, remove the vocals or the beat, or trim the start and end.");
        addMusicBtn.Click += (s, e) =>
        {
            int c = StudioDialogs.AddMusicChoice(this);
            if (c == 1) { if (OpenStudio()) studioForm.AddMusicClick(); }
            else if (c == 2) vocalBtn.PerformClick();
            else if (c == 3) StudioDialogs.TrimFile(this, outDir.Text.Trim());
        };

        StyleBox(outDir); outDir.SetBounds(16, 377, 520, 28);
        outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outDir.Text = Win.DownloadsDir;   // downloads go to the Downloads folder of this PC
        Lbl(outDir, "Save to");
        StyleBtn(browse, TM.C(255, 70, 30, 120)); browse.Text = "Browse...";
        browse.SetBounds(544, 376, 80, 30); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browse.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog() == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        StyleBtn(go, TM.C(255, 120, 40, 210)); go.Text = "Convert";
        go.Font = new Font("Segoe UI Semibold", 11f);
        go.SetBounds(16, 414, 500, 40); go.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        go.Click += (s, e) => Start();

        StyleBtn(cancel, TM.C(255, 110, 30, 70)); cancel.Text = "Cancel"; cancel.Enabled = false;
        cancel.SetBounds(524, 414, 100, 40); cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Click += (s, e) => { cancelReq = true; KillCurrent(); Log("Cancelling..."); };

        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        StyleBox(log); log.Font = new Font("Consolas", 9f); NeonScroll.Attach(log);
        log.SetBounds(16, 514, 608, 180);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        bg.Controls.AddRange(new Control[] { links, fmt, sr, nameFmt, trimA, trimB, norm, playlist, openDone, animate,
            mastBtn, vocalBtn, lyricsBtn, usedBtn, prevNormBtn, helpBtn, settingsBtn, changeBtn, studioBtn, addMusicBtn, whatsBtn, outDir, browse, go, cancel, log });
        Win.CapsAll(bg);   // capitals in the bold font
        CaptureBase();   // the design positions, before any saved window size moves the anchored controls

        fmt.SelectedIndex = 0; sr.SelectedIndex = 0; nameFmt.SelectedIndex = 0;
        LoadSettings();
        ApplyPrefs();
        if (seenVer != Changelog.Latest) changeBtn.Text = "Changelog *";   // a star shows there is something new to read
        // plugins load after the window is shown (see Shown)
        loading = false;
        HookAutoSave(); HookEditionGates();

        ApplyScale(); bg.Resize += (s, e) => ApplyScale(); Shown += (s, e) => ApplyScale();   // text and controls follow the size of the window (must run before Repopulate below)
        Repopulate(); bg.Resize += (s, e) => Repopulate();
        Win.Dark(this); Win.DarkControl(links); Win.DarkControl(log); StyleTips();

        StartLayerWorker(); frameLoop = new FrameLoop(this, Tick) { Slow = () => moving || Win.AnyMoving || WindowState == FormWindowState.Minimized || (ActiveForm != this && ActiveForm != studioForm) }; ApplyPrefs();
    }

    // ---------- styling helpers ----------
    void Lbl(Control c, string text) { lbls.Add(new KeyValuePair<Control, string>(c, text)); }

    static void StyleBox(TextBox b)
    {
        b.BorderStyle = BorderStyle.FixedSingle; b.BackColor = Field; b.ForeColor = Ink; if (!b.Multiline) Win.FixedBox(b); Win.NeonBox(b);
    }

    void StyleCombo(ComboBox c, string[] items, int x, int y, int w)
    {
        c.DropDownStyle = ComboBoxStyle.DropDownList; c.FlatStyle = FlatStyle.Flat;
        c.BackColor = Field; c.ForeColor = Ink; c.Items.AddRange(items);
        c.SetBounds(x, y, w, 26);
    }

    static void StyleChk(CheckBox c, string text, int x, int y, int w)
    {
        c.Text = text; c.SetBounds(x, y, w, 26); c.BackColor = Pill; c.ForeColor = Ink; c.Font = new Font("Segoe UI", 10f);
    }

    static void StyleBtn(Button b, Color c)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = c; b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255);
        b.FlatAppearance.BorderSize = 1; b.Cursor = Cursors.Hand;
    }

    static Label MakeLabel(string text, Font f, Color c, int x, int y, int w, int h)
    {
        var l = new Label { Text = text, Font = f, ForeColor = c, AutoSize = false };
        l.SetBounds(x, y, w, h);
        return l;
    }

    // Applies the options from the Settings window to the running app.
    internal void ApplyPrefs()
    {
        if (frameLoop != null) frameLoop.TargetFps = Math.Max(30, Math.Min(Prefs.FpsCap, FrameLoop.DisplayHz()));
        try { var sfm = studioForm as StudioForm; if (sfm != null && !sfm.IsDisposed) sfm.ApplyFps(); } catch { }
        WaveOutPlayer.Gain = Prefs.OutVol / 100f;
        if (!Prefs.ShowFps) Text = "Purphex  v" + Version;
        if (!UserBackground.Active) UserBackground.StopVideo();
        bg.Invalidate(); RequestSave();
    }
    internal string DataFolder { get { Directory.CreateDirectory(AppData); return AppData; } }
    internal static string FfmpegPath() { return FfmpegExe(); }
    SettingsForm settingsForm; LyricsGenForm lyricsGenForm; UsageForm usageForm;
    internal void ShowUsage()
    {
        if (usageForm == null || usageForm.IsDisposed) { usageForm = new UsageForm(); usageForm.Show(this); }
        Win.Bring(usageForm);
    }
    // the Generate Lyrics page: a window of its own, free in every edition
    internal void ShowLyricsGen(string file = null)
    {
        if (lyricsGenForm == null || lyricsGenForm.IsDisposed) { lyricsGenForm = new LyricsGenForm(this); lyricsGenForm.Show(); }
        Win.Bring(lyricsGenForm); if (file != null) lyricsGenForm.LoadFile(file);
    }
    internal void ShowSettings(bool account = false)
    {
        // not modal: the other windows stay usable (and movable) while Settings is open
        if (settingsForm == null || settingsForm.IsDisposed) { settingsForm = new SettingsForm(this); settingsForm.Show(); Win.Bring(settingsForm); }
        else Win.Bring(settingsForm);
        if (account) settingsForm.ShowAccount();
    }

    // ---------- settings ----------
    bool loading = true;   // suppress saves while settings are being applied

    // Debounced save for things that change many times in a row (typing, slider drags).
    System.Windows.Forms.Timer saveTimer;
    internal void RequestSave()
    {
        if (loading) return;
        if (saveTimer == null) { saveTimer = new System.Windows.Forms.Timer { Interval = 500 }; saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveSettings(); }; }
        saveTimer.Stop(); saveTimer.Start();
    }

    internal void SaveSettings()
    {
        if (loading) return;
        try
        {
            Directory.CreateDirectory(AppData);
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Win.WriteAtomic(SettingsFile, string.Join("\r\n", new[] {
                "fmt=" + fmt.SelectedIndex, "fmtv=2", "sr=" + sr.SelectedIndex, "name=" + nameFmt.SelectedIndex,
                "dir=" + outDir.Text, "norm=" + norm.Checked, "playlist=" + playlist.Checked,
                "open=" + openDone.Checked, "anim=" + animate.Checked,
                "x=" + b.X, "y=" + b.Y, "w=" + b.Width, "h=" + b.Height,
                "max=" + (WindowState == FormWindowState.Maximized), "seen=" + seenVer, "ffilter=" + ExtraFilter,
                "master=" + Master.Serialize(), "mzoom=" + MZoomPct }.Concat(Prefs.Lines()).ToArray()) + Environment.NewLine, System.Text.Encoding.UTF8);
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
        outDir.TextChanged += (s, e) => RequestSave();
        ResizeEnd += h;
    }

    void LoadSettings()
    {
        int wx = 0, wy = 0, ww = 0, wh = 0, savedFmt = -1, fmtVer = 1; bool wmax = false;
        try
        {
            if (!File.Exists(SettingsFile)) return;
            foreach (var line in File.ReadAllLines(SettingsFile))
            {
                int i = line.IndexOf('='); if (i < 1) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                int n; bool b;
                if (k == "fmt" && int.TryParse(v, out n) && n >= 0) savedFmt = n;
                else if (k == "fmtv") fmtVer = 2;
                else if (k == "sr" && int.TryParse(v, out n) && n >= 0 && n < sr.Items.Count) sr.SelectedIndex = n;
                else if (k == "name" && int.TryParse(v, out n) && n >= 0 && n < nameFmt.Items.Count) nameFmt.SelectedIndex = n;
                else if (k == "dir" && v.Length > 0) outDir.Text = string.Equals(v.TrimEnd('\\'), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3"), StringComparison.OrdinalIgnoreCase) ? Win.DownloadsDir : v;   // the old default folder moves to Downloads, a folder you chose yourself stays
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
                else Prefs.Parse(k, v);
            }
            if (savedFmt >= 0) { if (fmtVer < 2) savedFmt = savedFmt <= 1 ? 0 : savedFmt - 1; if (savedFmt < fmt.Items.Count) fmt.SelectedIndex = savedFmt; }   // the list lost its MP3 V0 entry: older saved choices move up one
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
    int idleTick;
    volatile bool moving;   // true while the window is being dragged or resized: no animation work then, so dragging stays as smooth as any native window
    protected override void WndProc(ref Message m)
    {
        Win.TrackMove(ref m);
        if (m.Msg == 0x231) moving = true; else if (m.Msg == 0x232) moving = false;   // WM_ENTERSIZEMOVE / WM_EXITSIZEMOVE
        base.WndProc(ref m);
    }

    void Tick()
    {
        double now = clock.Elapsed.TotalMilliseconds; minimizedNow = WindowState == FormWindowState.Minimized;
        if (minimizedNow) { lastClock = now; reqW = reqH = 0; UserBackground.StopVideo(); return; }
        if (moving || Win.AnyMoving) { lastClock = now; return; }
        if (ActiveForm != this && ActiveForm != null && (++idleTick % 4) != 0) { lastClock = now; return; } float dt = (float)Math.Min(0.05, Math.Max(0.0005, (now - lastClock) / 1000.0)); lastClock = now;
        if (animate.Checked) t += dt; float k30 = dt * 30f;   // the motion constants below were tuned per 1/30 s
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
                // pulse the Convert button, but only repaint the real control when the colour visibly changes
        float k = (float)(Math.Sin(t * 2.5f) * 0.5 + 0.5); k = (float)Math.Round(k * 12) / 12f;
        Color gc = busy ? TM.C(255, 70, 30, 120) : TM.C(255, (int)(105 + 70 * k), (int)(35 + 30 * k), (int)(195 + 45 * k));
        if (go.BackColor != gc) go.BackColor = gc;
        bg.Invalidate(); if ((++paintTick & 7) == 0) Win.PaintPending(bg);   // give the controls on top their turn to repaint
        fpsFrames++;
        if (now >= nextFpsAt) { fpsShown = (int)(fpsFrames * 1000.0 / (now - (nextFpsAt - 1000)) + 0.5); fpsFrames = 0; nextFpsAt = now + 1000; Text = "Purphex  v" + Version + (Prefs.ShowFps ? "    " + fpsShown + " fps" : ""); }
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
        using (var edge = new Pen(TM.C(Math.Min(255, l.A + 60), 225, 190, 255), 1.5f / l.S))
        using (var rib = new Pen(TM.C(l.A, 235, 215, 255), 1f / l.S))
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
        foreach (var f in Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories)) return f;
        return null;
    }

    internal static string DenoDirPath { get { return DenoDir; } }
    internal static string FfmpegDirPath { get { return FfmpegDir; } }
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

    // a link that was converted is taken out of the box (links that failed or were cancelled stay, so you can try again)
    internal void RemoveLinkFromBox(string link)
    {
        if (string.IsNullOrEmpty(link)) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                var lines = links.Text.Replace("\r\n", "\n").Split('\n'); var keep = new List<string>(); bool removed = false;
                foreach (var raw in lines)
                {
                    string line = raw; int at = removed ? -1 : line.IndexOf(link, StringComparison.Ordinal);
                    if (at >= 0) { removed = true; line = (line.Substring(0, at) + line.Substring(at + link.Length)).Trim(' ', '\t', ',', ';'); if (line.Length == 0) continue; }
                    keep.Add(line);
                }
                if (removed) links.Text = string.Join("\r\n", keep.ToArray());
            }));
        }
        catch { }
    }

    // ---------- converting ----------
    void Start()
    {
        if (busy) return;
        if (!FmtOk(fmt.SelectedIndex)) fmt.SelectedIndex = 0; if (!SrOk(sr.SelectedIndex)) sr.SelectedIndex = 0; if (norm.Checked && !Edition.Has(Tier.Supporter)) norm.Checked = false;   // a saved choice the edition does not have any more
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
            Verify(YtDlp, "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS", "yt-dlp.exe");
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
            Verify(zip, "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256", "ffmpeg-master-latest-win64-gpl.zip");
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
                Verify(zip, "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum", "deno-x86_64-pc-windows-msvc.zip");
                if (Directory.Exists(DenoDir)) Directory.Delete(DenoDir, true);
                ZipFile.ExtractToDirectory(zip, DenoDir);
                File.Delete(zip);
                deno = FindFile(DenoDir, "deno.exe");
            }
            catch (Exception ex) { Log("Couldn't set up Deno (" + ex.Message + "); YouTube may fail."); }
        }

        // shared yt-dlp arguments from the chosen options
        bool video = FmtVid[job.Fmt] > 0; bool mastering = job.Master != null && !video;
        if (video && job.Master != null) Log("Note: mastering and loudness are for audio formats, so they are skipped for video.");
        string tag = video ? FmtTag[job.Fmt] : FormatTag(job.Fmt, job.Sr, job.Master, job.Normalize && !mastering);
        string tmpDir = Path.Combine(job.Dir, ".yt2mp3_tmp");
        string common;
        if (video)
        {
            int cap = FmtVid[job.Fmt]; string sel = cap >= 9999 ? "bv*+ba/b" : "bv*[height<=" + cap + "]+ba/b[height<=" + cap + "]/bv*+ba/b";
            common = "-f \"" + sel + "\" -S \"res,vcodec:h264,acodec:m4a\" --merge-output-format " + FmtBox[job.Fmt] + " --newline --windows-filenames --embed-metadata "
                + (FmtBox[job.Fmt] == "mp4" ? "--embed-thumbnail --convert-thumbnails jpg " : "")
                + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" " + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "");
            Log("Video download: " + FmtNames[job.Fmt].Replace("  ", " ") + ". Sample rate and loudness options only apply to audio.");
        }
        else if (mastering)
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
            if (SrArg(job.Sr, job.Fmt) != null) ppa.Add(SrArg(job.Sr, job.Fmt));
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
            var madeBefore = !mastering ? LyricsGet.Snapshot(job.Dir) : null;   // what was in the folder before: the lyrics and the TIMES USED count look at what is new
            var lyricsBefore = Prefs.SaveLyrics && !mastering ? madeBefore : null;
            int code = RunProc(YtDlp, args, true);
            if (cancelReq) break;
            if (code != 0) { fail++; Log("Failed (exit " + code + ")."); }
            else if (!mastering) { ok++; RemoveLinkFromBox(link); UsageStat.CountNew(job.Dir, madeBefore, job.Fmt, 1); if (lyricsBefore != null) LyricsGet.ForNewFiles(job.Dir, lyricsBefore, Log); }
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
                        ok++; RemoveLinkFromBox(link); LastFile = outp; if (res != null) LastAn = res;
                        try { File.Delete(w); } catch { }
                        Log("Saved: " + Path.GetFileName(outp)); UsageStat.CountFile(outp); if (Prefs.SaveLyrics) Log(LyricsGet.SaveFor(outp));
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
        if (MessageBox.Show(this, "The vocal remover gives you three separate files in your save folder:\n\n  1. the VOCALS on their own\n  2. the BEAT (instrumental) on its own\n  3. the ORIGINAL song\n\nYour original file is not duplicated: if it is already in the save folder it is left exactly as it is, and it is never changed.\n\nPick a song now?", "Vocal remover", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        string file;
        using (var d = new OpenFileDialog())
        {
            d.Title = "Choose a song to remove the vocals from";
            d.Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.aiff;*.aif|All files|*.*";
            try { if (LastFile != null && File.Exists(LastFile)) d.InitialDirectory = Path.GetDirectoryName(LastFile); } catch { }
            if (d.ShowDialog(this) != DialogResult.OK) return;
            file = d.FileName;
        }
        int f = Math.Min(4, fmt.SelectedIndex); string dir = outDir.Text.Trim(); bool open = openDone.Checked;
        if (dir.Length == 0) { MessageBox.Show(this, "Choose a folder to save to."); return; }
        Separator.OfferTurbo(this);   // NVIDIA cards can run the AI several times faster (asks once)
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
        string v, inst; var etaM = new EtaMeter();
        try
        {
            if (!Separator.Separate(file, tmp, baseName, "htdemucs", Log, p => { targetPct = lo + p * (hi - lo) * 0.92f; etaM.Add(p); string es = etaM.Text(p); status = "Separating vocals with AI... " + (es.Length > 0 ? "about " + es + " left" : "starting"); }, () => cancelReq, out v, out inst))
            { status = cancelReq ? "Cancelled" : "Failed"; return; }
            status = "Encoding..."; string ext = MasterDsp.FmtExt[f], tag = FmtTag[f]; int code;
            string outInst = Path.Combine(dir, baseName + " [instrumental " + tag + "]." + ext), outVoc = Path.Combine(dir, baseName + " [vocals " + tag + "]." + ext);
            string ff = FfmpegExe();
            MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + inst + "\" -vn " + MasterDsp.FmtEnc[f] + " \"" + outInst + "\"", p => { cur = p; }, out code);
            if (code != 0 || !File.Exists(outInst)) throw new Exception("Couldn't encode the instrumental.");
            MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + v + "\" -vn " + MasterDsp.FmtEnc[f] + " \"" + outVoc + "\"", p => { cur = p; }, out code);
            if (code != 0 || !File.Exists(outVoc)) throw new Exception("Couldn't encode the vocals.");
            // the original goes alongside the two new files, but only once: never a copy of something that is already there
            try
            {
                string outOrig = Path.Combine(dir, Path.GetFileName(file));
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(outOrig), StringComparison.OrdinalIgnoreCase)) Log("The original is already in the save folder: left as it is.");
                else if (File.Exists(outOrig)) Log("A file called " + Path.GetFileName(outOrig) + " is already in the save folder: the original was not copied again.");
                else { File.Copy(file, outOrig); Log("Saved: " + Path.GetFileName(outOrig) + " (the original)"); }
            }
            catch (Exception ex) { Log("Could not copy the original: " + ex.Message); }
            LastFile = outInst; targetPct = 100; status = "Done";
            Log("Saved: " + Path.GetFileName(outInst)); Log("Saved: " + Path.GetFileName(outVoc));
            if (open) Process.Start("explorer.exe", "\"" + dir + "\"");
            Thread.Sleep(1200);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
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
    const int UpdateCheckSeconds = 4;
    const string LatestReleasePage = "https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest";
    const string LatestExeUrl = "https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest/download/YtToMp3.exe";
    string dismissedVer = "";
    volatile bool updating = false;

    volatile bool minimizedNow;
    void UpdateLoop()
    {
        while (true)
        {
            try { if (!minimizedNow) CheckForUpdate(); } catch { }
            Thread.Sleep(UpdateCheckSeconds * 1000);
        }
    }

    internal static string CurrentVersion { get { return Version; } }
    // Settings > Scan for updates: looks right now (ignoring an earlier "Later") and always tells you what it found
    internal void ScanForUpdates(Action<string> report)
    {
        ThreadPool.QueueUserWorkItem(_ => { try { CheckForUpdate(true, report); } catch { report("Could not check for updates. Try again in a moment."); } });
    }
    void CheckForUpdate() { CheckForUpdate(false, null); }
    void CheckForUpdate(bool manual, Action<string> report)
    {
        if (manual) dismissedVer = "";
        if (updating) { if (manual) report("An update is already downloading."); return; }
        if (card != null) { if (manual) { report("An update is waiting - its window is open in Purphex Music."); BeginInvoke(new Action(() => Win.Bring(this))); } return; }
        string newVer = null, dl = LatestExeUrl;
        updateSha = "";
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
        if (newVer == null) { if (manual) report("Could not reach the update server. Check your internet connection and try again."); return; }   // offline or unreadable: try again next round
        System.Version nv;
        if (!System.Version.TryParse(newVer, out nv)) { if (manual) report("Could not read the newest version number. Try again in a moment."); return; }
        if (nv <= new System.Version(Version)) { if (manual) report("You are up to date. Version " + Version + " is the newest."); return; }
        if (newVer == dismissedVer) return;   // user said "Later" for this one
        var notes = new List<string>();
        try
        {
            using (var wc = new WebClient())
            {
                string[] lines = wc.DownloadString(UpdateManifestUrl + "?t=" + DateTime.UtcNow.Ticks).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 2 && lines[0].Trim() == newVer) for (int i = 2; i < lines.Length && notes.Count < 14; i++) { string ln = lines[i].Trim(); if (ln.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) updateSha = ln.Substring(7).Trim().ToLowerInvariant(); else notes.Add(ln); }
            }
        }
        catch { }
        BeginInvoke(new Action(() => { ShowUpdateCard(newVer, dl, notes); if (manual) Win.Bring(this); }));
        if (manual) report("Version " + newVer + " is available. The update window is open in Purphex Music.");
    }

    // In-app update popup (a card over the window instead of a separate message box).
    void ShowUpdateCard(string newVer, string dl, List<string> notes)
    {
        if (card != null) return;
        var nfont = new Font("Segoe UI", 9.5f); string ntext = string.Join("\n", notes.ToArray());
        int ntH = notes.Count > 0 ? TextRenderer.MeasureText(ntext, nfont, new Size(404, 0), TextFormatFlags.WordBreak).Height + 8 : 0;
        int npH = Math.Min(ntH, Math.Max(120, Math.Min(230, bg.ClientSize.Height - 330)));   // taller notes scroll inside the card instead of being cut off
        int nh = notes.Count > 0 ? npH + 30 : 0;
        card = new Panel { Size = new Size(470, 160 + nh), BackColor = TM.C(255, 14, 7, 28), BorderStyle = BorderStyle.FixedSingle };
        var title = MakeLabel("Update available", new Font("Segoe UI Semibold", 14f), TM.C(255, 225, 190, 255), 16, 14, 430, 28);
        var msg = MakeLabel("Version " + newVer + " is ready (you have " + Version + ").\nUpdate now? The app restarts automatically.", Font, Ink, 16, 48, 430, 40);
        var ctl = new List<Control> { title, msg };
        if (notes.Count > 0)
        {
            ctl.Add(MakeLabel("What's new", new Font("Segoe UI Semibold", 10.5f), TM.C(255, 236, 120, 230), 16, 94, 200, 22));
            var np = new Panel { Left = 16, Top = 116, Width = 438, Height = npH, AutoScroll = true, BackColor = card.BackColor };
            var nl = new Label { Text = ntext, Font = nfont, ForeColor = Ink, BackColor = card.BackColor, AutoSize = true, MaximumSize = new Size(404, 0), Left = 0, Top = 0 };
            np.Controls.Add(nl); np.MouseEnter += (s, e) => np.Focus(); nl.MouseEnter += (s, e) => np.Focus(); ctl.Add(np);
        }
        var yes = new PxButton(); StyleBtn(yes, TM.C(255, 130, 45, 220)); yes.Text = "Update now"; yes.SetBounds(16, 108 + nh, 205, 36);
        var no = new PxButton(); StyleBtn(no, TM.C(255, 60, 26, 100)); no.Text = "Later"; no.SetBounds(235, 108 + nh, 205, 36);
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

    // checks a tool we downloaded against the checksum its publisher posts next to it; a file that does not match is deleted and never run
    internal static string CheckSum(string file, string sumsUrl, string name)
    {
        try
        {
            string text; var req = (HttpWebRequest)WebRequest.Create(sumsUrl); req.UserAgent = "Purphex"; req.Timeout = 15000; using (var rs = req.GetResponse()) using (var sr = new StreamReader(rs.GetResponseStream())) text = sr.ReadToEnd();
            string actual = Sha256Of(file);
            var hm = Regex.Match(text, @"(?im)^\s*Hash\s*:\s*([0-9a-f]{64})");   // Deno posts its checksum in PowerShell's Get-FileHash layout
            if (hm.Success) return string.Equals(hm.Groups[1].Value, actual, StringComparison.OrdinalIgnoreCase) ? null : "does not match its published checksum";
            foreach (var raw in text.Split(new[] { (char)10 }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = raw.Trim().Split(new[] { ' ', '*', (char)9 }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 1 && p[0].Length == 64 && text.Trim().Length < 130) return string.Equals(p[0], actual, StringComparison.OrdinalIgnoreCase) ? null : "does not match its published checksum";
                if (p.Length >= 2 && p[0].Length == 64 && string.Equals(p[1], name, StringComparison.OrdinalIgnoreCase)) return string.Equals(p[0], actual, StringComparison.OrdinalIgnoreCase) ? null : "does not match its published checksum";
            }
        }
        catch { }
        return null;   // no checksum available (offline, or not listed): carry on
    }
    void Verify(string file, string sumsUrl, string name)
    {
        string bad = CheckSum(file, sumsUrl, name); if (bad == null) { Log("Checked " + name + " against its published checksum: ok."); return; }
        try { File.Delete(file); } catch { }
        throw new Exception(name + " " + bad + ", so it was deleted and not used. Try again; if it keeps happening, check your connection or antivirus.");
    }
    string updateSha = "";   // the SHA-256 the release publishes for its program file; a download that does not match is thrown away
    static string Sha256Of(string file) { using (var sha = System.Security.Cryptography.SHA256.Create()) using (var fs = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant(); }
    void ApplyUpdate(string downloadUrl)
    {
        if (!downloadUrl.StartsWith("https://github.com/heaththomas225-svg/yt-to-mp3/", StringComparison.OrdinalIgnoreCase)) { Log("Update refused: the download address is not this project's release page."); return; }
        string expectSha = updateSha;
        updating = true; SetBusy(true); Log("Downloading update...");
        new Thread(() =>
        {
            string exe = Application.ExecutablePath, tmp = exe + ".new", old = exe + ".old";
            try
            {
                Download(downloadUrl, tmp, "Downloading update...");
                if (new FileInfo(tmp).Length < 10000) throw new Exception("Downloaded file looks invalid.");
                if (expectSha.Length > 0 && Sha256Of(tmp) != expectSha) { try { File.Delete(tmp); } catch { } throw new Exception("The downloaded update does not match the checksum the release publishes, so it was thrown away and not installed."); }
                try { File.Delete(old); } catch { }
                File.Move(exe, old);          // a running exe can be renamed, not overwritten
                File.Move(tmp, exe);
                try { string named = Path.Combine(Path.GetDirectoryName(exe), "Purphex Music.exe"); if (!string.Equals(exe, named, StringComparison.OrdinalIgnoreCase)) File.Copy(exe, named, true); } catch { }   // the program is now called Purphex Music: older copies get a file with the new name next to them
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
