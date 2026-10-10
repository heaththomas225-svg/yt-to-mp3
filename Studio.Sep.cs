using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

// ======================================================================
//  AI source separation using Demucs (MIT licence) running in a private,
//  embedded Python with CPU PyTorch. Nothing is installed system-wide.
//  Everything lives in %LocalAppData%\YtToMp3\py (about 0.5 GB with the model).
// ======================================================================
static class Separator
{
    public static string Root { get { return Path.Combine(StudioEnv.DataDir, "py"); } }
    public static string PyExe { get { return Path.Combine(Root, "python.exe"); } }
    static string Marker { get { return Path.Combine(Root, ".demucs_ready"); } }
    public static string ModelDir { get { return Path.Combine(StudioEnv.DataDir, "models"); } }
    public static bool Installed { get { return File.Exists(PyExe) && File.Exists(Marker); } }

    public const string PyUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip";
    public const string GetPipUrl = "https://bootstrap.pypa.io/get-pip.py";

    static void Kill(Process p) { try { if (!p.HasExited) Process.Start(new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false }); } catch { } }

    static Dictionary<string, string> Env()
    {
        var env = new Dictionary<string, string>();
        string ff = StudioEnv.Ffmpeg(); string ffDir = ff == null ? "" : Path.GetDirectoryName(ff);
        env["PATH"] = ffDir + ";" + Root + ";" + Path.Combine(Root, "Scripts") + ";" + Environment.GetEnvironmentVariable("PATH");
        env["TORCH_HOME"] = ModelDir; env["PYTHONUTF8"] = "1"; env["PYTHONIOENCODING"] = "utf-8"; env["PYTHONNOUSERSITE"] = "1";
        return env;
    }

    static int Exec(string exe, string args, Action<string> onLine, Func<bool> cancelled)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Root,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        foreach (var kv in Env()) psi.EnvironmentVariables[kv.Key] = kv.Value;
        using (var p = new Process { StartInfo = psi })
        {
            DataReceivedEventHandler h = (s, e) => { if (e.Data != null && onLine != null) onLine(e.Data); };
            p.OutputDataReceived += h; p.ErrorDataReceived += h; p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            while (!p.WaitForExit(250)) if (cancelled != null && cancelled()) { Kill(p); p.WaitForExit(3000); return -999; }
            p.WaitForExit(); return p.ExitCode;
        }
    }

    static void Download(string url, string path, Action<float> prog, Func<bool> cancelled)
    {
        using (var wc = new WebClient())
        {
            wc.Headers[HttpRequestHeader.UserAgent] = "YtToMp3";
            var done = new ManualResetEvent(false); Exception err = null;
            wc.DownloadProgressChanged += (s, e) => { if (prog != null) prog(e.ProgressPercentage / 100f); };
            wc.DownloadFileCompleted += (s, e) => { err = e.Error; done.Set(); };
            wc.DownloadFileAsync(new Uri(url), path);
            while (!done.WaitOne(200)) if (cancelled != null && cancelled()) { wc.CancelAsync(); throw new Exception("Cancelled."); }
            if (err != null) throw err;
        }
    }

    // One-time setup: embedded Python, pip, CPU PyTorch, Demucs. Progress 0..1 across all steps.
    public static void Install(Action<string> log, Action<float> prog, Func<bool> cancelled)
    {
        Directory.CreateDirectory(Root);
        if (!File.Exists(PyExe))
        {
            log("Downloading Python (embedded, ~11 MB)...");
            string zip = Path.Combine(Root, "py.zip");
            Download(PyUrl, zip, p => prog(0.00f + 0.05f * p), cancelled);
            ZipFile.ExtractToDirectory(zip, Root); File.Delete(zip);
            foreach (var f in Directory.GetFiles(Root, "python3*._pth"))
            {
                var lines = new List<string>(File.ReadAllLines(f));
                for (int i = 0; i < lines.Count; i++) if (lines[i].StartsWith("#import site")) lines[i] = "import site";
                if (!lines.Contains("Lib\\site-packages")) lines.Add("Lib\\site-packages");
                File.WriteAllLines(f, lines.ToArray());
            }
        }
        prog(0.06f);
        string pipOk = Path.Combine(Root, "Lib", "site-packages", "pip");
        if (!Directory.Exists(pipOk))
        {
            log("Installing pip...");
            string gp = Path.Combine(Root, "get-pip.py"); Download(GetPipUrl, gp, null, cancelled);
            if (Exec(PyExe, "\"" + gp + "\" --no-warn-script-location", log, cancelled) != 0) throw new Exception("pip setup failed.");
        }
        prog(0.10f);
        int code;
        log("Installing PyTorch (CPU, about 200 MB, a few minutes)...");
        code = Exec(PyExe, "-m pip install torch==2.0.1 torchaudio==2.0.2 \"numpy<2\" --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://pypi.org/simple --no-warn-script-location --disable-pip-version-check",
            l => { if (l.Contains("Downloading") || l.Contains("Installing") || l.Contains("Successfully") || l.Contains("ERROR")) log(l.Trim()); }, cancelled);
        if (code == -999) throw new Exception("Cancelled."); if (code != 0) throw new Exception("PyTorch install failed (pip exit " + code + ").");
        prog(0.60f);
        log("Installing Demucs...");
        code = Exec(PyExe, "-m pip install demucs==4.0.1 soundfile --no-warn-script-location --disable-pip-version-check",
            l => { if (l.Contains("Downloading") || l.Contains("Installing") || l.Contains("Successfully") || l.Contains("ERROR")) log(l.Trim()); }, cancelled);
        if (code == -999) throw new Exception("Cancelled.");
        if (code != 0)
        {
            // lameenc (MP3 encoder, not needed for WAV output) sometimes has no wheel; install the rest without it
            log("Retrying without the optional MP3 encoder...");
            code = Exec(PyExe, "-m pip install demucs==4.0.1 --no-deps --no-warn-script-location --disable-pip-version-check", log, cancelled);
            if (code == 0) code = Exec(PyExe, "-m pip install dora-search einops julius openunmix pyyaml tqdm soundfile --no-warn-script-location --disable-pip-version-check", log, cancelled);
            if (code != 0) throw new Exception("Demucs install failed (pip exit " + code + ").");
        }
        prog(0.92f);
        log("Checking the installation...");
        string outp = "";
        code = Exec(PyExe, "-c \"import torch, torchaudio, demucs.separate; print('DEMUCS_OK', torch.__version__)\"", l => { outp += l; }, cancelled);
        if (code != 0 || !outp.Contains("DEMUCS_OK")) throw new Exception("The separation engine didn't load correctly.\n" + outp);
        File.WriteAllText(Marker, "ok"); prog(1f); log("Separation engine ready.");
    }

    // Splits a song into real VOCAL and INSTRUMENTAL audio files (two-stem Demucs).
    // Returns false on failure. Output WAV files are 32-bit float, same length as the input.
    public static bool Separate(string input, string outDir, string baseName, string model, Action<string> log, Action<float> prog, Func<bool> cancelled,
        out string vocalsPath, out string instPath)
    {
        vocalsPath = null; instPath = null;
        string tmp = Path.Combine(outDir, ".sep_tmp_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(tmp);
        try
        {
            log("Running AI separation (" + model + ")...  first run also downloads the model (~80 MB).");
            string args = "-m demucs.separate -n " + model + " --two-stems=vocals --float32 -o \"" + tmp + "\" --filename \"{stem}.{ext}\" \"" + input + "\"";
            var rx = new Regex(@"(\d+)%\|");
            int code = Exec(PyExe, args, l =>
            {
                var m = rx.Match(l); if (m.Success) { prog(Math.Min(0.98f, int.Parse(m.Groups[1].Value) / 100f)); return; }
                string t = l.Trim(); if (t.Length > 0 && !t.StartsWith("0%") && t.IndexOf("it/s") < 0) log(t);
            }, cancelled);
            if (code == -999) { log("Cancelled."); return false; }
            string dir = Path.Combine(tmp, model);
            string v = Path.Combine(dir, "vocals.wav"), i = Path.Combine(dir, "no_vocals.wav");
            if (code != 0 || !File.Exists(v) || !File.Exists(i)) { log("Separation failed (exit " + code + ")."); return false; }
            Directory.CreateDirectory(outDir);
            vocalsPath = Path.Combine(outDir, baseName + " - vocals.wav"); instPath = Path.Combine(outDir, baseName + " - instrumental.wav");
            File.Copy(v, vocalsPath, true); File.Copy(i, instPath, true); prog(1f);
            return true;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }
}
