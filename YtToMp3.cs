using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
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

class Leaf { public float X, Y, S, Rot, Spin, VY, Phase, Sway; public int A; public Color C; }
class Spore { public float X, Y, R, VY, Phase; }

// Snapshot of the UI taken on the UI thread and handed to the worker thread.
class Job
{
    public string[] Items; public string Dir, TrimA, TrimB;
    public int Fmt, Sr, Name; public bool Playlist, Normalize, OpenDone;
}

class App : Form
{
    // ---- Auto-update settings ----
    const string Version = "2.1.1";
    // Plain-text manifest: line 1 = newest version, line 2 = direct exe download URL.
    const string UpdateManifestUrl = "https://raw.githubusercontent.com/heaththomas225-svg/yt-to-mp3/main/version.txt";

    static string AppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3");
    static string Tools = Path.Combine(AppData, "tools");
    static string SettingsFile = Path.Combine(AppData, "settings.txt");
    static string YtDlp = Path.Combine(Tools, "yt-dlp.exe");
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
    static readonly string[] SrNames = { "Original", "44.1 kHz", "48 kHz" };
    static readonly string[] SrArgs = { null, "-ar 44100", "-ar 48000" };
    static readonly string[] NameNames = { "Title", "Artist - Title", "Title [video id]" };
    static readonly string[] NameTpl = { "%(title).150B", "%(artist,uploader)s - %(title).150B", "%(title).140B [%(id)s]" };

    BgPanel bg = new BgPanel();
    TextBox links = new TextBox(), outDir = new TextBox(), log = new TextBox(), trimA = new TextBox(), trimB = new TextBox();
    ComboBox fmt = new ComboBox(), sr = new ComboBox(), nameFmt = new ComboBox();
    CheckBox playlist = new CheckBox(), norm = new CheckBox(), openDone = new CheckBox(), animate = new CheckBox();
    Button go = new Button(), cancel = new Button(), browse = new Button();
    ToolTip tip = new ToolTip();
    List<KeyValuePair<Control, string>> lbls = new List<KeyValuePair<Control, string>>();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    Random rnd = new Random();
    List<Leaf> leaves = new List<Leaf>();
    List<Spore> spores = new List<Spore>();
    float t = 0, targetPct = 0, shownPct = 0;
    int itemIdx = 0, itemTotal = 1;
    volatile bool busy = false, cancelReq = false;
    Process cur;
    string status = "";
    Panel card;

    static readonly Color Ink = Color.FromArgb(235, 225, 255);
    static readonly Color Field = Color.FromArgb(30, 12, 56);
    static readonly Color Pill = Color.FromArgb(40, 16, 72);

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
        Text = "YouTube to MP3 v" + Version;
        ClientSize = new Size(640, 650);
        MinimumSize = new Size(560, 600);
        Font = new Font("Segoe UI", 9.5f);
        Shown += (s, e) => new Thread(UpdateLoop) { IsBackground = true }.Start();
        FormClosing += (s, e) => { SaveSettings(); KillCurrent(); };

        bg.Dock = DockStyle.Fill;
        bg.Paint += PaintBg;
        bg.Resize += (s, e) => CenterCard();
        Controls.Add(bg);

        // links (batch queue)
        links.Multiline = true; links.ScrollBars = ScrollBars.Vertical; links.AcceptsReturn = true; StyleBox(links);
        links.SetBounds(16, 94, 608, 64); links.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Lbl(links, "Links or search terms  (one per line, batch supported)");

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
        StyleChk(norm, "Normalize loudness (-14 LUFS)", 240, 240, 220);
        StyleChk(playlist, "Whole playlists", 470, 240, 154);
        tip.SetToolTip(playlist, "If a link points at a playlist, convert every video in it.");

        StyleChk(openDone, "Open folder when done", 16, 276, 200);
        StyleChk(animate, "Animated background", 240, 276, 200);
        openDone.Checked = true; animate.Checked = true;

        // output folder
        StyleBox(outDir); outDir.SetBounds(16, 322, 536, 28);
        outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3");
        Lbl(outDir, "Save to");
        StyleBtn(browse, Color.FromArgb(70, 30, 120)); browse.Text = "Browse...";
        browse.SetBounds(560, 321, 64, 30); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browse.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog() == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        StyleBtn(go, Color.FromArgb(120, 40, 210)); go.Text = "Convert";
        go.Font = new Font("Segoe UI Semibold", 11f);
        go.SetBounds(16, 362, 500, 42); go.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        go.Click += (s, e) => Start();

        StyleBtn(cancel, Color.FromArgb(110, 30, 70)); cancel.Text = "Cancel"; cancel.Enabled = false;
        cancel.SetBounds(524, 362, 100, 42); cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Click += (s, e) => { cancelReq = true; KillCurrent(); Log("Cancelling..."); };

        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        StyleBox(log); log.Font = new Font("Consolas", 8.5f);
        log.SetBounds(16, 456, 608, 180);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        bg.Controls.AddRange(new Control[] { links, fmt, sr, nameFmt, trimA, trimB, norm, playlist, openDone, animate,
            outDir, browse, go, cancel, log });

        fmt.SelectedIndex = 0; sr.SelectedIndex = 0; nameFmt.SelectedIndex = 0;
        LoadSettings();

        for (int i = 0; i < 16; i++) leaves.Add(NewLeaf(true));
        for (int i = 0; i < 40; i++) spores.Add(NewSpore(true));

        timer.Interval = 33;
        timer.Tick += (s, e) => Tick();
        timer.Start();
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
    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(AppData);
            File.WriteAllLines(SettingsFile, new[] {
                "fmt=" + fmt.SelectedIndex, "sr=" + sr.SelectedIndex, "name=" + nameFmt.SelectedIndex,
                "dir=" + outDir.Text, "norm=" + norm.Checked, "playlist=" + playlist.Checked,
                "open=" + openDone.Checked, "anim=" + animate.Checked });
        }
        catch { }
    }

    void LoadSettings()
    {
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
            }
        }
        catch { }
    }

    // ---------- animation ----------
    Leaf NewLeaf(bool anywhere)
    {
        Color[] pal = {
            Color.FromArgb(170, 90, 255), Color.FromArgb(140, 60, 230),
            Color.FromArgb(195, 125, 255), Color.FromArgb(120, 50, 200) };
        var l = new Leaf();
        l.S = 0.25f + (float)rnd.NextDouble() * 0.65f;
        l.X = (float)rnd.NextDouble() * Math.Max(ClientSize.Width, 640);
        l.Y = anywhere ? (float)rnd.NextDouble() * Math.Max(ClientSize.Height, 650) : -120 * l.S - 20;
        l.Rot = (float)rnd.NextDouble() * 360;
        l.Spin = ((float)rnd.NextDouble() - 0.5f) * 1.2f;
        l.VY = 0.3f + l.S * 0.9f;
        l.Phase = (float)rnd.NextDouble() * 6.28f;
        l.Sway = 0.3f + (float)rnd.NextDouble() * 0.6f;
        l.A = 30 + (int)(l.S * 70);
        l.C = pal[rnd.Next(pal.Length)];
        return l;
    }

    Spore NewSpore(bool anywhere)
    {
        var s = new Spore();
        s.X = (float)rnd.NextDouble() * Math.Max(ClientSize.Width, 640);
        s.Y = anywhere ? (float)rnd.NextDouble() * Math.Max(ClientSize.Height, 650) : ClientSize.Height + 5;
        s.R = 1f + (float)rnd.NextDouble() * 2.5f;
        s.VY = 0.2f + (float)rnd.NextDouble() * 0.7f;
        s.Phase = (float)rnd.NextDouble() * 6.28f;
        return s;
    }

    void Tick()
    {
        t += 0.033f;
        if (animate.Checked)
        {
            foreach (var l in leaves)
            {
                l.Y += l.VY;
                l.X += (float)Math.Sin(t * 0.8f + l.Phase) * l.Sway;
                l.Rot += l.Spin;
                if (l.Y > ClientSize.Height + 120) { var n = NewLeaf(false); l.X = n.X; l.Y = n.Y; l.S = n.S; l.VY = n.VY; l.A = n.A; l.C = n.C; }
            }
            foreach (var s in spores)
            {
                s.Y -= s.VY;
                s.X += (float)Math.Sin(t + s.Phase) * 0.4f;
                if (s.Y < -5) { var n = NewSpore(false); s.X = n.X; s.Y = n.Y; }
            }
        }
        shownPct += (targetPct - shownPct) * 0.15f;

        float k = (float)(Math.Sin(t * 2.5f) * 0.5 + 0.5);
        go.BackColor = busy ? Color.FromArgb(70, 30, 120)
            : Color.FromArgb((int)(105 + 70 * k), (int)(35 + 30 * k), (int)(195 + 45 * k));
        bg.Invalidate();
    }

    void DrawLeaf(Graphics g, Leaf l)
    {
        float[] ang = { 0, -38, 38, -72, 72, -104, 104 };
        float[] len = { 100, 88, 88, 66, 66, 42, 42 };
        float[] wid = { 17, 15, 15, 12, 12, 8, 8 };
        var st = g.Save();
        g.TranslateTransform(l.X, l.Y);
        g.RotateTransform(l.Rot);
        g.ScaleTransform(l.S, l.S);
        using (var fill = new SolidBrush(Color.FromArgb(l.A, l.C)))
        using (var edge = new Pen(Color.FromArgb(Math.Min(255, l.A + 60), 225, 190, 255), 1.5f / l.S))
        using (var rib = new Pen(Color.FromArgb(l.A, 235, 215, 255), 1f / l.S))
        {
            for (int i = 0; i < 7; i++)
            {
                var s2 = g.Save();
                g.RotateTransform(ang[i]);
                float L = len[i], w = wid[i];
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(0, 0, -w, -L * 0.30f, -w * 0.7f, -L * 0.80f, 0, -L);
                    p.AddBezier(0, -L, w * 0.7f, -L * 0.80f, w, -L * 0.30f, 0, 0);
                    g.FillPath(fill, p);
                    g.DrawPath(edge, p);
                }
                g.DrawLine(rib, 0, 0, 0, -L * 0.9f);
                g.Restore(s2);
            }
            g.DrawLine(edge, 0, 0, 0, 45);
        }
        g.Restore(st);
    }

    void PaintBg(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = bg.ClientRectangle;
        if (r.Width < 10 || r.Height < 10) return;
        bool anim = animate.Checked;

        using (var br = new LinearGradientBrush(r, Color.FromArgb(14, 5, 30), Color.FromArgb(48, 12, 86), 70f))
            g.FillRectangle(br, r);

        if (anim)
        {
            for (int i = 0; i < 2; i++)
            {
                float cx = r.Width * (0.3f + 0.4f * i) + (float)Math.Sin(t * 0.4f + i * 2) * 60;
                float cy = r.Height * (0.35f + 0.3f * i) + (float)Math.Cos(t * 0.3f + i) * 50;
                float rad = 190 + (float)Math.Sin(t * 0.9f + i) * 25;
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
                    using (var pg = new PathGradientBrush(gp))
                    {
                        pg.CenterColor = Color.FromArgb(70, 150, 60, 240);
                        pg.SurroundColors = new[] { Color.FromArgb(0, 150, 60, 240) };
                        g.FillPath(pg, gp);
                    }
                }
            }
            leaves.Sort((a, b) => a.S.CompareTo(b.S));
            foreach (var l in leaves) DrawLeaf(g, l);
            foreach (var s in spores)
            {
                int a = (int)(60 + 90 * (Math.Sin(t * 2 + s.Phase) * 0.5 + 0.5));
                using (var b = new SolidBrush(Color.FromArgb(a, 215, 170, 255)))
                    g.FillEllipse(b, s.X, s.Y, s.R * 2, s.R * 2);
            }
        }

        string title = "YouTube → MP3";
        using (var f = new Font("Segoe UI Black", 24f))
        {
            float glow = anim ? (float)(Math.Sin(t * 2) * 0.5 + 0.5) : 0.4f;
            for (int i = 4; i >= 1; i--)
                using (var b = new SolidBrush(Color.FromArgb((int)(18 + 22 * glow), 190, 100, 255)))
                {
                    g.DrawString(title, f, b, 16 - i, 10 - i);
                    g.DrawString(title, f, b, 16 + i, 10 + i);
                }
            var sz = g.MeasureString(title, f);
            using (var b = new LinearGradientBrush(new RectangleF(16, 10, sz.Width, sz.Height),
                Color.FromArgb(240, 220, 255), Color.FromArgb(185, 110, 255), 0f))
                g.DrawString(title, f, b, 16, 10);
        }
        using (var f = new Font("Segoe UI", 9f))
        using (var b = new SolidBrush(Color.FromArgb(190, 200, 170, 255)))
        {
            g.DrawString("Audio extraction for producers: pick format, sample rate, trim and batch", f, b, 18, 54);
            foreach (var kv in lbls) g.DrawString(kv.Value, f, b, kv.Key.Left, kv.Key.Top - 18);
        }

        var bar = new Rectangle(16, 416, r.Width - 32, 16);
        using (var tb = new SolidBrush(Color.FromArgb(150, 20, 8, 40)))
            g.FillRectangle(tb, bar);
        int fw = (int)(bar.Width * Math.Max(0, Math.Min(100, shownPct)) / 100f);
        if (fw > 2)
        {
            var fr = new Rectangle(bar.X, bar.Y, fw, bar.Height);
            using (var fb = new LinearGradientBrush(fr, Color.FromArgb(150, 60, 240), Color.FromArgb(235, 90, 230), 0f))
                g.FillRectangle(fb, fr);
            float sx = bar.X + ((t * 120) % (fw + 60)) - 30;
            var old = g.Clip;
            g.SetClip(fr);
            using (var sb = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillRectangle(sb, sx, bar.Y, 22, bar.Height);
            g.Clip = old;
        }
        using (var pn = new Pen(Color.FromArgb(160, 190, 120, 255)))
            g.DrawRectangle(pn, bar);
        if (status.Length > 0)
            using (var f = new Font("Segoe UI", 8.5f))
            using (var b = new SolidBrush(Color.FromArgb(220, 230, 210, 255)))
                g.DrawString(status, f, b, 16, 434);
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

    static bool ValidTime(string s)
    {
        return s.Length == 0 || Regex.IsMatch(s, @"^\d+(:\d{1,2}){0,2}(\.\d+)?$");
    }

    // ---------- converting ----------
    void Start()
    {
        if (busy) return;
        var items = new List<string>();
        foreach (var l in links.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            if (l.Trim().Length > 0) items.Add(l.Trim());
        if (items.Count == 0) { MessageBox.Show(this, "Paste at least one link (or type a search) first."); return; }
        string a = trimA.Text.Trim(), b = trimB.Text.Trim();
        if (!ValidTime(a) || !ValidTime(b)) { MessageBox.Show(this, "Trim times look like 1:30 or 0:01:30.5"); return; }
        if (outDir.Text.Trim().Length == 0) { MessageBox.Show(this, "Choose a folder to save to."); return; }

        var job = new Job
        {
            Items = items.ToArray(), Dir = outDir.Text.Trim(), TrimA = a, TrimB = b,
            Fmt = fmt.SelectedIndex, Sr = sr.SelectedIndex, Name = nameFmt.SelectedIndex,
            Playlist = playlist.Checked, Normalize = norm.Checked, OpenDone = openDone.Checked
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
        var ppa = new List<string>();
        if (SrArgs[job.Sr] != null) ppa.Add(SrArgs[job.Sr]);
        if (FmtCodec[job.Fmt] != null) ppa.Add(FmtCodec[job.Fmt]);
        if (job.Normalize) ppa.Add("-af loudnorm=I=-14:TP=-1:LRA=11");

        string common = "-x --audio-format " + FmtExt[job.Fmt]
            + (FmtQual[job.Fmt] != null ? " --audio-quality " + FmtQual[job.Fmt] : "")
            + " --newline --windows-filenames --embed-metadata "
            + (FmtArt[job.Fmt] ? "--embed-thumbnail --convert-thumbnails jpg " : "")
            + (ppa.Count > 0 ? "--postprocessor-args \"ExtractAudio:" + string.Join(" ", ppa.ToArray()) + "\" " : "")
            + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" "
            + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "")
            + (job.Playlist ? "--yes-playlist --ignore-errors " : "--no-playlist ");
        if (job.TrimA.Length > 0 || job.TrimB.Length > 0)
            common += "--download-sections \"*" + (job.TrimA.Length > 0 ? job.TrimA : "0") + "-"
                + (job.TrimB.Length > 0 ? job.TrimB : "inf") + "\" --force-keyframes-at-cuts ";

        int ok = 0, fail = 0;
        itemTotal = job.Items.Length;
        for (int i = 0; i < job.Items.Length; i++)
        {
            if (cancelReq) break;
            itemIdx = i;
            string link = job.Items[i], target = link;
            // Plain text instead of a link -> search YouTube and take the top result.
            if (link.IndexOf("://") < 0 && link.IndexOf("www.", StringComparison.OrdinalIgnoreCase) < 0)
                target = "ytsearch1:" + link;

            status = "Item " + (i + 1) + " of " + itemTotal + ": " + (link.Length > 60 ? link.Substring(0, 60) + "..." : link);
            Log("--- " + (i + 1) + "/" + itemTotal + ": " + link);
            string args = common + "-o \"" + Path.Combine(job.Dir, NameTpl[job.Name] + ".%(ext)s") + "\" \"" + target + "\"";
            int code = RunProc(YtDlp, args, true);
            if (cancelReq) break;
            if (code == 0) ok++; else { fail++; Log("Failed (exit " + code + ")."); }
            SetItemPct(100);
        }

        if (cancelReq) { Log("Cancelled."); status = "Cancelled"; return; }
        targetPct = 100; status = "Done: " + ok + " converted" + (fail > 0 ? ", " + fail + " failed" : "");
        Log(status + ". Saved to " + job.Dir);
        if (ok > 0 && job.OpenDone) Process.Start("explorer.exe", "\"" + job.Dir + "\"");
        Thread.Sleep(1500);
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
                    System.Globalization.CultureInfo.InvariantCulture, out v)) SetItemPct((float)v);
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
    const int UpdateCheckMinutes = 5;
    const string LatestReleaseApi = "https://api.github.com/repos/heaththomas225-svg/yt-to-mp3/releases/latest";
    const string LatestExeUrl = "https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest/download/YtToMp3.exe";
    string dismissedVer = "";
    volatile bool updating = false;

    void UpdateLoop()
    {
        while (true)
        {
            try { CheckForUpdate(); } catch { }
            Thread.Sleep(UpdateCheckMinutes * 60 * 1000);
        }
    }

    void CheckForUpdate()
    {
        if (updating || card != null) return;
        string newVer = null, dl = LatestExeUrl;
        try
        {
            // The releases API reflects a new release within a minute or so.
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "YtToMp3";
                var m = Regex.Match(wc.DownloadString(LatestReleaseApi), "\"tag_name\"\\s*:\\s*\"v?([0-9][0-9.]*)\"");
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
        BeginInvoke(new Action(() => ShowUpdateCard(newVer, dl)));
    }

    // In-app update popup (a card over the window instead of a separate message box).
    void ShowUpdateCard(string newVer, string dl)
    {
        if (card != null) return;
        card = new Panel { Size = new Size(380, 150), BackColor = Color.FromArgb(34, 12, 64), BorderStyle = BorderStyle.FixedSingle };
        var title = MakeLabel("Update available", new Font("Segoe UI Semibold", 13f), Color.FromArgb(225, 190, 255), 16, 14, 348, 28);
        var msg = MakeLabel("Version " + newVer + " is ready (you have " + Version + ").\nUpdate now? The app restarts automatically.", Font, Ink, 16, 50, 348, 40);
        var yes = new Button(); StyleBtn(yes, Color.FromArgb(130, 45, 220)); yes.Text = "Update now"; yes.SetBounds(16, 100, 170, 34);
        var no = new Button(); StyleBtn(no, Color.FromArgb(60, 26, 100)); no.Text = "Later"; no.SetBounds(194, 100, 170, 34);
        yes.Click += (s, e) => { HideCard(); ApplyUpdate(dl); };
        no.Click += (s, e) => { dismissedVer = newVer; HideCard(); };
        card.Controls.AddRange(new Control[] { title, msg, yes, no });
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
