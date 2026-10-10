using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
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

class App : Form
{
    // ---- Auto-update settings ----
    const string Version = "1.2.0";
    // Plain-text manifest: line 1 = newest version, line 2 = direct exe download URL.
    const string UpdateManifestUrl = "https://raw.githubusercontent.com/heaththomas225-svg/yt-to-mp3/main/version.txt";

    // Downloaded tools live in AppData so the exe can sit anywhere (Desktop etc.).
    static string Tools = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "tools");
    static string YtDlp = Path.Combine(Tools, "yt-dlp.exe");
    static string FfmpegDir = Path.Combine(Tools, "ffmpeg");
    static string DenoDir = Path.Combine(Tools, "deno");

    BgPanel bg = new BgPanel();
    TextBox url = new TextBox(), outDir = new TextBox(), log = new TextBox();
    Button go = new Button(), browse = new Button();
    CheckBox playlist = new CheckBox();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    Random rnd = new Random();
    List<Leaf> leaves = new List<Leaf>();
    List<Spore> spores = new List<Spore>();
    float t = 0, targetPct = 0, shownPct = 0;
    volatile bool busy = false;
    string status = "";

    static readonly Color Ink = Color.FromArgb(235, 225, 255);
    static readonly Color Field = Color.FromArgb(30, 12, 56);

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
        ClientSize = new Size(560, 470);
        MinimumSize = new Size(480, 420);
        Font = new Font("Segoe UI", 9.5f);
        Shown += (s, e) => new Thread(CheckForUpdate) { IsBackground = true }.Start();

        bg.Dock = DockStyle.Fill;
        bg.Paint += PaintBg;
        bg.Resize += (s, e) => CenterCard();
        Controls.Add(bg);

        StyleBox(url); url.SetBounds(16, 90, 528, 28);
        url.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        StyleBox(outDir); outDir.SetBounds(16, 150, 440, 28);
        outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3");

        StyleBtn(browse, Color.FromArgb(70, 30, 120)); browse.Text = "Browse...";
        browse.SetBounds(464, 149, 80, 30); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browse.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog() == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        playlist.Text = "Download whole playlist"; playlist.SetBounds(16, 188, 230, 24);
        playlist.BackColor = Color.FromArgb(40, 16, 72); playlist.ForeColor = Ink;

        StyleBtn(go, Color.FromArgb(120, 40, 210)); go.Text = "Convert to MP3";
        go.Font = new Font("Segoe UI Semibold", 11f);
        go.SetBounds(16, 220, 528, 42); go.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        go.Click += (s, e) => Start();

        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        StyleBox(log); log.Font = new Font("Consolas", 8.5f);
        log.SetBounds(16, 306, 528, 148);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        bg.Controls.AddRange(new Control[] { url, outDir, browse, playlist, go, log });
        AcceptButton = go;

        for (int i = 0; i < 16; i++) leaves.Add(NewLeaf(true));
        for (int i = 0; i < 40; i++) spores.Add(NewSpore(true));

        timer.Interval = 33;
        timer.Tick += (s, e) => Tick();
        timer.Start();
    }

    static void StyleBox(TextBox b)
    {
        b.BorderStyle = BorderStyle.FixedSingle; b.BackColor = Field; b.ForeColor = Ink;
    }

    static void StyleBtn(Button b, Color c)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = c; b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255);
        b.FlatAppearance.BorderSize = 1; b.Cursor = Cursors.Hand;
    }

    // ---------- animation ----------
    Leaf NewLeaf(bool anywhere)
    {
        Color[] pal = {
            Color.FromArgb(170, 90, 255), Color.FromArgb(140, 60, 230),
            Color.FromArgb(195, 125, 255), Color.FromArgb(120, 50, 200) };
        var l = new Leaf();
        l.S = 0.25f + (float)rnd.NextDouble() * 0.65f;
        l.X = (float)rnd.NextDouble() * Math.Max(ClientSize.Width, 560);
        l.Y = anywhere ? (float)rnd.NextDouble() * Math.Max(ClientSize.Height, 470) : -120 * l.S - 20;
        l.Rot = (float)rnd.NextDouble() * 360;
        l.Spin = ((float)rnd.NextDouble() - 0.5f) * 1.2f;
        l.VY = 0.3f + l.S * 0.9f;
        l.Phase = (float)rnd.NextDouble() * 6.28f;
        l.Sway = 0.3f + (float)rnd.NextDouble() * 0.6f;
        l.A = 35 + (int)(l.S * 85);
        l.C = pal[rnd.Next(pal.Length)];
        return l;
    }

    Spore NewSpore(bool anywhere)
    {
        var s = new Spore();
        s.X = (float)rnd.NextDouble() * Math.Max(ClientSize.Width, 560);
        s.Y = anywhere ? (float)rnd.NextDouble() * Math.Max(ClientSize.Height, 470) : ClientSize.Height + 5;
        s.R = 1f + (float)rnd.NextDouble() * 2.5f;
        s.VY = 0.2f + (float)rnd.NextDouble() * 0.7f;
        s.Phase = (float)rnd.NextDouble() * 6.28f;
        return s;
    }

    void Tick()
    {
        t += 0.033f;
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
        shownPct += (targetPct - shownPct) * 0.15f;

        // pulsing button colour
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

        using (var br = new LinearGradientBrush(r, Color.FromArgb(14, 5, 30), Color.FromArgb(48, 12, 86), 70f))
            g.FillRectangle(br, r);

        // two slowly drifting, pulsing glows
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
                    pg.CenterColor = Color.FromArgb(80, 150, 60, 240);
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

        // title with pulsing glow
        string title = "YouTube → MP3";
        using (var f = new Font("Segoe UI Black", 24f))
        {
            float glow = (float)(Math.Sin(t * 2) * 0.5 + 0.5);
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
            g.DrawString("Paste any video link (or type a search) and get an MP3", f, b, 18, 56);
            g.DrawString("Video link", f, b, 16, 72);
            g.DrawString("Save to", f, b, 16, 132);
        }

        // progress bar with moving shimmer
        var bar = new Rectangle(16, 272, r.Width - 32, 16);
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
                g.DrawString(status, f, b, 16, 289);
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
        busy = b; url.ReadOnly = b;
        go.Text = b ? "Working..." : "Convert to MP3";
        if (!b) { targetPct = 0; status = ""; }
    }

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

    // ---------- converting ----------
    void Start()
    {
        if (busy) return;
        string link = url.Text.Trim(), dir = outDir.Text.Trim();
        if (link.Length == 0) { MessageBox.Show(this, "Paste a video link first."); return; }
        bool pl = playlist.Checked;
        SetBusy(true);
        new Thread(() =>
        {
            try { Run(link, dir, pl); }
            catch (Exception ex) { Log("ERROR: " + ex.Message); }
            finally { SetBusy(false); }
        }) { IsBackground = true }.Start();
    }

    void Run(string link, string dir, bool pl)
    {
        Directory.CreateDirectory(Tools);
        Directory.CreateDirectory(dir);

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

        // Plain text instead of a link -> search YouTube and take the top result.
        string target = link;
        if (link.IndexOf("://") < 0 && link.IndexOf("www.", StringComparison.OrdinalIgnoreCase) < 0)
            target = "ytsearch1:" + link;

        string args = "-x --audio-format mp3 --audio-quality 0 --newline --windows-filenames "
            + "--embed-metadata --embed-thumbnail --convert-thumbnails jpg "
            + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" "
            + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "")
            + (pl ? "--yes-playlist --ignore-errors " : "--no-playlist ")
            + "-o \"" + Path.Combine(dir, "%(title).150B.%(ext)s") + "\" \"" + target + "\"";

        Log("Converting...");
        status = "Converting..."; targetPct = 0;
        int code = RunProc(YtDlp, args, true);
        if (code == 0)
        {
            targetPct = 100; status = "Done!";
            Log("Done! Saved to " + dir);
            Process.Start("explorer.exe", "\"" + dir + "\"");
            Thread.Sleep(1200);
        }
        else Log("yt-dlp failed (exit " + code + "). Check the messages above.");
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
                    System.Globalization.CultureInfo.InvariantCulture, out v)) targetPct = (float)v;
            }
            Log(e.Data);
        };
        p.OutputDataReceived += h; p.ErrorDataReceived += h;
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }

    // ---------- self-update ----------
    void CheckForUpdate()
    {
        if (UpdateManifestUrl.Length == 0) return;
        try
        {
            string[] lines;
            using (var wc = new WebClient())
                lines = wc.DownloadString(UpdateManifestUrl + "?t=" + DateTime.UtcNow.Ticks)
                          .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return;
            if (new Version(lines[0].Trim()) <= new Version(Version)) return;
            string newVer = lines[0].Trim(), dl = lines[1].Trim();

            BeginInvoke(new Action(() => ShowUpdateCard(newVer, dl)));
        }
        catch { /* offline or bad manifest: ignore silently */ }
    }

    // In-app update popup (a card over the window instead of a separate message box).
    Panel card;
    void ShowUpdateCard(string newVer, string dl)
    {
        if (card != null) return;
        card = new Panel { Size = new Size(380, 150), BackColor = Color.FromArgb(34, 12, 64), BorderStyle = BorderStyle.FixedSingle };
        var title = MakeLabel("Update available", new Font("Segoe UI Semibold", 13f), Color.FromArgb(225, 190, 255), 16, 14, 348, 28);
        var msg = MakeLabel("Version " + newVer + " is ready (you have " + Version + ").\nUpdate now? The app restarts automatically.", Font, Ink, 16, 50, 348, 40);
        var yes = new Button(); StyleBtn(yes, Color.FromArgb(130, 45, 220)); yes.Text = "Update now"; yes.SetBounds(16, 100, 170, 34);
        var no = new Button(); StyleBtn(no, Color.FromArgb(60, 26, 100)); no.Text = "Later"; no.SetBounds(194, 100, 170, 34);
        yes.Click += (s, e) => { HideCard(); ApplyUpdate(dl); };
        no.Click += (s, e) => HideCard();
        card.Controls.AddRange(new Control[] { title, msg, yes, no });
        bg.Controls.Add(card);
        CenterCard();
        card.BringToFront();
        Activate();
    }

    static Label MakeLabel(string text, Font f, Color c, int x, int y, int w, int h)
    {
        var l = new Label { Text = text, Font = f, ForeColor = c, AutoSize = false };
        l.SetBounds(x, y, w, h);
        return l;
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
        SetBusy(true); Log("Downloading update...");
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
                SetBusy(false);
            }
        }) { IsBackground = true }.Start();
    }
}
