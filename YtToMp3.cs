using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

class App : Form
{
    TextBox url = new TextBox(), outDir = new TextBox();
    Button go = new Button(), browse = new Button();
    CheckBox playlist = new CheckBox();
    TextBox log = new TextBox();
    ProgressBar bar = new ProgressBar();

    // Tools live next to the exe (in a "tools" folder).
    static string Tools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
    static string YtDlp = Path.Combine(Tools, "yt-dlp.exe");
    static string FfmpegDir = Path.Combine(Tools, "ffmpeg");

    // ---- Auto-update settings ----
    const string Version = "1.0.0";
    // URL of a plain-text manifest you host (e.g. a raw GitHub file). Two lines:
    //   1.1.0
    //   https://example.com/YtToMp3.exe
    // Leave empty to disable update checks.
    const string UpdateManifestUrl = "https://raw.githubusercontent.com/heaththomas225-svg/yt-to-mp3/main/version.txt";

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
        try { File.Delete(Application.ExecutablePath + ".old"); } catch { }
        Application.EnableVisualStyles();
        Application.Run(new App());
    }

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

            BeginInvoke(new Action(() =>
            {
                if (MessageBox.Show(this, "Version " + newVer + " is available (you have " + Version + ").\nUpdate now?",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                ApplyUpdate(dl);
            }));
        }
        catch { /* offline or bad manifest: ignore silently */ }
    }

    void ApplyUpdate(string downloadUrl)
    {
        go.Enabled = false; Log("Downloading update...");
        new Thread(() =>
        {
            string exe = Application.ExecutablePath, tmp = exe + ".new", old = exe + ".old";
            try
            {
                using (var wc = new WebClient()) wc.DownloadFile(downloadUrl, tmp);
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
                BeginInvoke(new Action(() => go.Enabled = true));
            }
        }) { IsBackground = true }.Start();
    }

    App()
    {
        Text = "YouTube to MP3 v" + Version;
        Shown += (s, e) => new Thread(CheckForUpdate) { IsBackground = true }.Start();
        ClientSize = new Size(560, 360);
        MinimumSize = new Size(480, 320);
        Font = new Font("Segoe UI", 9.5f);

        var l1 = new Label { Text = "YouTube link", Left = 12, Top = 12, AutoSize = true };
        url.SetBounds(12, 34, 536, 26); url.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var l2 = new Label { Text = "Save to", Left = 12, Top = 70, AutoSize = true };
        outDir.SetBounds(12, 92, 450, 26); outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3");
        browse.Text = "Browse..."; browse.SetBounds(470, 91, 78, 28); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browse.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog() == DialogResult.OK) outDir.Text = d.SelectedPath;
        };

        playlist.Text = "Download whole playlist"; playlist.SetBounds(12, 126, 220, 24);
        go.Text = "Convert to MP3"; go.SetBounds(12, 156, 536, 36); go.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        go.Click += (s, e) => Start();

        bar.SetBounds(12, 200, 536, 12); bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bar.Style = ProgressBarStyle.Continuous;

        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        log.SetBounds(12, 222, 536, 126); log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        log.BackColor = Color.White; log.Font = new Font("Consolas", 8.5f);

        Controls.AddRange(new Control[] { l1, url, l2, outDir, browse, playlist, go, bar, log });
        AcceptButton = go;
    }

    void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), s); return; }
        log.AppendText(s + Environment.NewLine);
    }

    void SetBusy(bool busy)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool>(SetBusy), busy); return; }
        go.Enabled = !busy; url.Enabled = !busy;
        if (!busy) bar.Value = 0;
    }

    void SetProgress(int pct)
    {
        if (InvokeRequired) { BeginInvoke(new Action<int>(SetProgress), pct); return; }
        bar.Value = Math.Max(0, Math.Min(100, pct));
    }

    void Start()
    {
        string link = url.Text.Trim(), dir = outDir.Text.Trim();
        if (link.Length == 0) { MessageBox.Show("Paste a YouTube link first."); return; }
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
            using (var wc = new WebClient())
                wc.DownloadFile("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", YtDlp);
        }
        else
        {
            Log("Checking for yt-dlp updates...");
            RunProc(YtDlp, "-U", false);
        }

        if (FindFfmpegBin() == null)
        {
            Log("First run: downloading ffmpeg (~100 MB, one time)...");
            string zip = Path.Combine(Tools, "ffmpeg.zip");
            using (var wc = new WebClient())
                wc.DownloadFile("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", zip);
            if (Directory.Exists(FfmpegDir)) Directory.Delete(FfmpegDir, true);
            ZipFile.ExtractToDirectory(zip, FfmpegDir);
            File.Delete(zip);
        }

        string ffbin = FindFfmpegBin();
        if (ffbin == null) throw new Exception("ffmpeg setup failed.");

        string args = "-x --audio-format mp3 --audio-quality 0 --embed-thumbnail --embed-metadata --newline "
            + "--ffmpeg-location \"" + ffbin + "\" "
            + (pl ? "--yes-playlist " : "--no-playlist ")
            + "-o \"" + Path.Combine(dir, "%(title)s.%(ext)s") + "\" \"" + link + "\"";

        Log("Converting...");
        int code = RunProc(YtDlp, args, true);
        if (code == 0)
        {
            Log("Done! Saved to " + dir);
            Process.Start("explorer.exe", "\"" + dir + "\"");
        }
        else Log("yt-dlp failed (exit " + code + ").");
    }

    static string FindFfmpegBin()
    {
        if (!Directory.Exists(FfmpegDir)) return null;
        foreach (var f in Directory.GetFiles(FfmpegDir, "ffmpeg.exe", SearchOption.AllDirectories))
            return Path.GetDirectoryName(f);
        return null;
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
                    System.Globalization.CultureInfo.InvariantCulture, out v)) SetProgress((int)v);
            }
            Log(e.Data);
        };
        p.OutputDataReceived += h; p.ErrorDataReceived += h;
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }
}
