using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;

// ======================================================================
//  AI source separation using Demucs (MIT licence) running in a private,
//  embedded Python with CPU PyTorch. Nothing is installed system-wide.
//  Everything lives in %LocalAppData%\YtToMp3\py (about 0.5 GB with the model).
// ======================================================================
// A time-left guess from how fast the progress moved over the last few seconds (not from the whole time so far, which includes the start-up and made the guess far too high).
class EtaMeter
{
    readonly List<double[]> s = new List<double[]>(); readonly Stopwatch sw = Stopwatch.StartNew(); double smooth = -1;
    public void Reset() { lock (s) { s.Clear(); smooth = -1; } }
    public void Add(float p)
    {
        lock (s) { double t = sw.Elapsed.TotalSeconds; if (s.Count > 0 && p <= s[s.Count - 1][1] + 0.002) return; s.Add(new[] { t, p }); while (s.Count > 2 && t - s[0][0] > 12) s.RemoveAt(0); }
    }
    // "" while there is not enough to go on yet, else for example "35 s" or "2 min"
    public string Text(float p)
    {
        if (p >= 0.995f) return ""; double left;
        lock (s)
        {
            if (s.Count < 2) return ""; var a = s[0]; var b = s[s.Count - 1]; double dt = b[0] - a[0], dp = b[1] - a[1]; if (dt < 2.5 || dp <= 0) return "";
            left = (1 - p) / (dp / dt); smooth = smooth < 0 ? left : smooth * 0.7 + left * 0.3; left = smooth;
        }
        if (left < 4) return "a few seconds"; if (left < 90) return (int)(Math.Ceiling(left / 5) * 5) + " s"; if (left < 5400) return (int)Math.Ceiling(left / 60) + " min"; return (left / 3600).ToString("0.0") + " h";
    }
}

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

    // ---- the fast stack: a newer PyTorch (about 1.5x faster on the processor) plus DirectML (the graphics card), installed in their own folder so the working engine is never touched ----
    public static string FastDir { get { return Path.Combine(Root, "fast"); } }
    static string FastMarker { get { return Path.Combine(FastDir, ".fast_ready"); } }
    static string FastFailed { get { return Path.Combine(Root, ".fast_failed"); } }
    public static bool FastReady { get { return File.Exists(FastMarker) && !File.Exists(FastFailed); } }
    public static void Diag(string line)
    {
        try
        {
            string p = Path.Combine(StudioEnv.DataDir, "separation.log"); Directory.CreateDirectory(Path.GetDirectoryName(p));
            try { if (File.Exists(p) && new FileInfo(p).Length > 200000) { var l = File.ReadAllLines(p); File.WriteAllLines(p, new List<string>(l).GetRange(l.Length / 2, l.Length - l.Length / 2).ToArray()); } } catch { }
            File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + Environment.NewLine);
        }
        catch { }
    }
    static volatile bool fastBad, startupDied; public static volatile string Device = ""; public static bool DisableFast;   // DisableFast: tests only   // what the engine used last time: "cpu" or "gpu: name"

    public static void FastInstall(Action<string> log, Action<float> prog, Func<bool> cancelled)
    {
        if (!File.Exists(PyExe) || File.Exists(FastFailed)) return;
        try
        {
            if (Directory.Exists(FastDir)) { try { Directory.Delete(FastDir, true); } catch { } }
            Directory.CreateDirectory(FastDir);
            log("Making the separation engine faster (one time: about 250 MB to download, 1.4 GB on disk)...");
            int code = Exec(PyExe, "-m pip install --target \"" + FastDir + "\" torch-directml==0.2.5.dev240914 \"numpy<2\" --no-warn-script-location --disable-pip-version-check",
                l => { if (l.Contains("Downloading") || l.Contains("Installing") || l.Contains("Successfully") || l.Contains("ERROR")) log(l.Trim()); }, cancelled);
            if (code == -999) { try { Directory.Delete(FastDir, true); } catch { } return; }
            if (code != 0) throw new Exception("pip exit " + code);
            prog(0.8f);
            code = Exec(PyExe, "-m pip install --target \"" + FastDir + "\" torchaudio==2.4.1 --no-deps --no-warn-script-location --disable-pip-version-check", log, cancelled);
            if (code == -999) { try { Directory.Delete(FastDir, true); } catch { } return; }
            if (code != 0) throw new Exception("pip exit " + code);
            prog(0.92f);
            string outp = ""; string chk = "import sys; sys.path.insert(0, r'" + FastDir + "'); import torch, torchaudio, torch_directml, demucs.separate; print('FAST_OK', torch.__version__)";
            code = Exec(PyExe, "-c \"" + chk.Replace("\"", "\\\"") + "\"", l => { outp += l; }, cancelled);
            if (code != 0 || !outp.Contains("FAST_OK")) throw new Exception("the new engine did not load: " + outp);
            File.WriteAllText(FastMarker, "ok"); prog(1f); log("The separation engine is now faster.");
        }
        catch (Exception ex)
        {
            log("(could not install the faster engine: " + ex.Message + ". The normal engine keeps working.)");
            try { Directory.Delete(FastDir, true); } catch { }
            try { File.WriteAllText(FastFailed, DateTime.Now.ToString("s")); } catch { }
        }
    }

    // ---- the NVIDIA turbo engine: the CUDA build of PyTorch (about 2.5 GB to download), only installed when the PC has an NVIDIA card AND the user said yes ----
    public static string CudaDir { get { return Path.Combine(Root, "cuda"); } }
    static string CudaMarker { get { return Path.Combine(CudaDir, ".cuda_ready"); } }
    static string CudaFailed { get { return Path.Combine(Root, ".cuda_failed"); } }
    static string CudaDeclined { get { return Path.Combine(Root, ".cuda_declined"); } }
    public static bool CudaReady { get { return File.Exists(CudaMarker) && !File.Exists(CudaFailed); } }
    public static volatile bool WantCuda; static volatile bool cudaBad, offeredThisSession; public static bool CudaTestSkipAvail;   // CudaTestSkipAvail: tests only
    static string nvName; static bool nvDone;
    // the name of the NVIDIA graphics card, or "" when there is none (asks the NVIDIA driver tool, which is installed with the driver)
    public static string Nvidia()
    {
        if (nvDone) return nvName; nvDone = true; nvName = "";
        try
        {
            string exe = null; foreach (var c in new[] { Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"), @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe" }) if (File.Exists(c)) { exe = c; break; }
            if (exe == null) return nvName;
            var psi = new ProcessStartInfo(exe, "--query-gpu=name --format=csv,noheader") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return nvName; } if (p.ExitCode == 0) foreach (var line in o.Split('\n')) if (line.Trim().Length > 2) { nvName = line.Trim(); break; } }
        }
        catch { }
        return nvName;
    }
    static double FreeGb() { try { return new DriveInfo(Path.GetPathRoot(Root)).AvailableFreeSpace / 1e9; } catch { return 0; } }
    public static bool CudaOffer { get { return Installed && !CudaReady && !File.Exists(CudaFailed) && !File.Exists(CudaDeclined) && Nvidia().Length > 0 && FreeGb() >= 8; } }
    // Ask once (per session) whether to install the NVIDIA turbo engine. Yes = it is installed during the next split; No = never ask again; Cancel = ask next time.
    public static void OfferTurbo(IWin32Window owner)
    {
        try
        {
            if (offeredThisSession || !CudaOffer) return; offeredThisSession = true;
            var r = MessageBox.Show(owner, "This PC has an NVIDIA graphics card (" + Nvidia() + ").\n\nVocal splitting can use it and run several times faster than it does now. That needs a one-time download of about 2.5 GB (about 5 GB on disk). The normal engine keeps working if you say no.\n\nInstall the turbo engine during this split?\n\nYes = install it now      No = never ask again      Cancel = ask me next time", "Faster splitting for NVIDIA", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) WantCuda = true; else if (r == DialogResult.No) { try { File.WriteAllText(CudaDeclined, DateTime.Now.ToString("s")); } catch { } }
        }
        catch { }
    }
    public static void CudaInstall(Action<string> log, Action<float> prog, Func<bool> cancelled)
    {
        if (!File.Exists(PyExe) || File.Exists(CudaFailed)) return;
        try
        {
            if (Directory.Exists(CudaDir)) { try { Directory.Delete(CudaDir, true); } catch { } }
            Directory.CreateDirectory(CudaDir); log("Installing the NVIDIA turbo engine (one time, about 2.5 GB: this can take several minutes)..."); Diag("installing the NVIDIA engine for " + Nvidia());
            float last = 0.02f; prog(last);
            int code = Exec(PyExe, "-m pip install --target \"" + CudaDir + "\" torch==2.4.1 torchaudio==2.4.1 \"numpy<2\" --index-url https://download.pytorch.org/whl/cu121 --extra-index-url https://pypi.org/simple --no-warn-script-location --disable-pip-version-check",
                l => { if (l.Contains("Downloading")) last = Math.Max(last, 0.1f); else if (l.Contains("Installing collected")) last = Math.Max(last, 0.85f); else if (l.Contains("Successfully")) last = 0.95f; prog(last); if (l.Contains("Downloading") || l.Contains("Installing") || l.Contains("Successfully") || l.Contains("ERROR")) log(l.Trim()); }, cancelled);
            if (code == -999) { try { Directory.Delete(CudaDir, true); } catch { } return; }
            if (code != 0) throw new Exception("pip exit " + code);
            string outp = ""; string chk = "import sys; sys.path.insert(0, r'" + CudaDir + "'); import torch, torchaudio, demucs.separate; print('CUDA_PKG_OK', torch.__version__, torch.version.cuda, torch.cuda.is_available())";
            code = Exec(PyExe, "-c \"" + chk + "\"", l => { outp += l + " "; }, cancelled);
            if (code != 0 || !outp.Contains("CUDA_PKG_OK")) throw new Exception("the NVIDIA engine did not load: " + outp);
            if (!outp.Contains("+cu")) throw new Exception("the download was not the NVIDIA build: " + outp);
            if (!outp.Trim().EndsWith("True") && !CudaTestSkipAvail) throw new Exception("CUDA is not available on this PC (the NVIDIA driver may be too old: update it from nvidia.com). " + outp);
            File.WriteAllText(CudaMarker, "ok"); prog(1f); log("The NVIDIA turbo engine is installed."); Diag("NVIDIA engine installed: " + outp.Trim());
        }
        catch (Exception ex)
        {
            log("(could not install the NVIDIA turbo engine: " + ex.Message + " The normal engines keep working.)"); Diag("NVIDIA engine failed: " + ex.Message);
            try { Directory.Delete(CudaDir, true); } catch { }
            try { File.WriteAllText(CudaFailed, ex.Message); } catch { }
        }
    }

    public static string Model = "htdemucs_ft";   // the separation model: htdemucs_ft (four fine-tuned models: the cleanest vocals and beat; downloaded once, about 300 MB)
    static Dictionary<string, string> Env(bool fast = false)
    {
        var env = new Dictionary<string, string>();
        if (fast && !DisableFast && CudaReady && !cudaBad) { env["PX_FAST"] = CudaDir; env["PX_CUDA"] = "1"; env["PX_ROOT"] = Root; }
        else if (fast && !DisableFast && FastReady && !fastBad) { env["PX_FAST"] = FastDir; env["PX_ROOT"] = Root; }
        string ff = StudioEnv.Ffmpeg(); string ffDir = ff == null ? "" : Path.GetDirectoryName(ff);
        env["PATH"] = ffDir + ";" + Root + ";" + Path.Combine(Root, "Scripts") + ";" + Environment.GetEnvironmentVariable("PATH");
        env["PX_MODEL"] = Model; env["TORCH_HOME"] = ModelDir; env["PYTHONUTF8"] = "1"; env["PYTHONIOENCODING"] = "utf-8"; env["PYTHONNOUSERSITE"] = "1";
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
        File.WriteAllText(Marker, "ok"); FastInstall(log, p => prog(0.92f + p * 0.08f), cancelled); prog(1f); log("Separation engine ready.");
    }

    // Splits a song into real VOCAL and INSTRUMENTAL audio files (two-stem Demucs).
    // Returns false on failure. Output WAV files are 32-bit float, same length as the input.
    // ---------------- warm worker: the model stays loaded between songs ----------------
    const string WorkerPy = @"import sys, os, time, json, random
_fd = os.environ.get('PX_FAST', '')   # the newer PyTorch (and DirectML) installed next to the old one
if _fd and os.path.isdir(_fd): sys.path.insert(0, _fd)
try:
    import torch, numpy as np, soundfile as sf
    from demucs.pretrained import get_model
    from demucs.apply import apply_model
    from demucs.audio import AudioFile
    _mn = os.environ.get('PX_MODEL', 'htdemucs')   # htdemucs_ft: four fine-tuned models, the cleanest separation
    try:
        model = get_model(_mn)
    except Exception as e:
        sys.stderr.write('model %s not available (%s): using htdemucs\n' % (_mn, e)); sys.stderr.flush(); model = get_model('htdemucs')
    model.eval()
except Exception as e:
    print('ERR|startup: %s' % e, flush=True); sys.exit(1)
SR = model.samplerate
try:
    torch.set_num_threads(max(1, (os.cpu_count() or 4) - 2))   # leave two cores free so the app and the audio stay smooth while separating
    torch.set_flush_denormal(True)
except Exception:
    pass
VI = model.sources.index('vocals')
DEV = 'cpu'; GPU = {'info': 'no graphics card test (DirectML not available)'}

def to_cpu():
    global DEV
    try:
        for k, inner in enumerate(model.models):   # every model of the bag (htdemucs_ft has four)
            if 'orig' in GPU: inner._spec, inner._ispec, inner._mask = GPU['orig'][k]
            inner.to('cpu')
    except Exception:
        pass
    DEV = 'cpu'

def pick_cuda():
    # NVIDIA cards: the CUDA build of PyTorch (installed only when the user agreed). Used only if a quick test shows it is faster AND gives the same result as the processor
    global DEV
    if os.environ.get('PX_CUDA') != '1' or os.environ.get('PX_GPU', '1') != '1': return
    try:
        if not torch.cuda.is_available():
            GPU['info'] = 'the NVIDIA engine is installed but CUDA is not available here (the graphics driver may be too old), so the processor is used'
            return
        name = str(torch.cuda.get_device_name(0)); key = 'cuda|' + name + '|' + torch.__version__
        cache = os.path.join(os.environ.get('PX_ROOT', '.'), 'device-cuda.json'); use = None
        try:
            c = json.load(open(cache))
            if c.get('key') == key: use = bool(c.get('use'))
        except Exception:
            pass
        inner = model.models[0]
        if use is False:
            GPU['info'] = name + ' (CUDA): earlier test showed it is not faster, so the processor is used'
            return
        if use is True: GPU['info'] = name + ' (CUDA): earlier test passed'
        if use is None:
            seg = int(float(inner.segment) * SR); t = np.arange(seg) / SR; rs = np.random.RandomState(5)
            x = sum(np.sin(2 * np.pi * f * t) * 0.1 for f in (110, 220, 330, 440, 880, 1760)) + rs.randn(seg) * 0.05
            w = torch.tensor(np.stack([x, x * 0.9]), dtype=torch.float32)[None]; w = (w - w.mean()) / (w.std() + 1e-8)
            with torch.no_grad():
                inner(w); t0 = time.time(); ref = inner(w); tc = time.time() - t0
            inner.to('cuda'); torch.backends.cudnn.benchmark = True
            with torch.no_grad():
                inner(w.to('cuda')); inner(w.to('cuda')); torch.cuda.synchronize(); t0 = time.time(); out = inner(w.to('cuda')).cpu(); torch.cuda.synchronize(); tg = time.time() - t0
            rel = float((out - ref).pow(2).mean().sqrt() / (ref.pow(2).mean().sqrt() + 1e-9))
            use = bool(tg < tc * 0.7 and rel < 0.02)
            try: json.dump({'key': key, 'use': use, 'cpu': tc, 'gpu': tg, 'rel': rel}, open(cache, 'w'))
            except Exception: pass
            GPU['info'] = '%s (CUDA): one segment takes %.2fs on the processor and %.2fs on the graphics card (difference %.4f), graphics card used: %s' % (name, tc, tg, rel, use)
        if use:
            for m in model.models: m.to('cuda')
            torch.backends.cudnn.benchmark = True; DEV = 'cuda'; GPU['name'] = name + ' (CUDA)'
        else:
            for m in model.models: m.to('cpu')
    except Exception as e:
        sys.stderr.write('cuda unavailable: %s\n' % e); sys.stderr.flush()
        GPU['info'] = 'CUDA failed: %s' % e
        try:
            for m in model.models: m.to('cpu')
        except Exception: pass
        DEV = 'cpu'

def pick_gpu():
    # use the graphics card (DirectML) only when a quick test shows it is clearly faster AND gives the same result as the processor
    global DEV
    if DEV != 'cpu': return
    if not _fd or os.environ.get('PX_GPU', '1') != '1' or os.environ.get('PX_CUDA') == '1': return
    try:
        import torch_directml
        if torch_directml.device_count() < 1: return
        name = str(torch_directml.device_name(0)).replace(chr(0), '').strip()
        key = name + '|' + torch.__version__
        cache = os.path.join(os.environ.get('PX_ROOT', '.'), 'device.json'); use = None
        try:
            c = json.load(open(cache))
            if c.get('key') == key: use = bool(c.get('use'))
        except Exception:
            pass
        if use is False:
            GPU['info'] = name + ': earlier test showed the graphics card is not faster, so the processor is used'
            return
        if use is True: GPU['info'] = name + ': graphics card (earlier test passed)'
        dev = torch_directml.device(); GPU['orig'] = [(m._spec, m._ispec, m._mask) for m in model.models]
        def patch(m, o):
            o_spec, o_ispec, o_mask = o
            m._spec = lambda x: o_spec(x.cpu())            # the FFT needs complex numbers: that part stays on the processor
            m._mask = lambda z, mm: o_mask(z, mm.cpu())
            m._ispec = lambda z, length=None, scale=0: o_ispec(z, length, scale).to(dev)
        inner = model.models[0]
        if use is None:
            seg = int(float(inner.segment) * SR); t = np.arange(seg) / SR; rs = np.random.RandomState(5)
            x = sum(np.sin(2 * np.pi * f * t) * 0.1 for f in (110, 220, 330, 440, 880, 1760)) + rs.randn(seg) * 0.05
            w = torch.tensor(np.stack([x, x * 0.9]), dtype=torch.float32)[None]; w = (w - w.mean()) / (w.std() + 1e-8)
            with torch.no_grad():
                inner(w); t0 = time.time(); ref = inner(w); tc = time.time() - t0
            patch(inner, GPU['orig'][0]); inner.to(dev)
            with torch.no_grad():
                inner(w.to(dev)); t0 = time.time(); out = inner(w.to(dev)).cpu(); tg = time.time() - t0
            rel = float((out - ref).pow(2).mean().sqrt() / (ref.pow(2).mean().sqrt() + 1e-9))
            use = bool(tg < tc * 0.7 and rel < 0.02)
            try: json.dump({'key': key, 'use': use, 'cpu': tc, 'gpu': tg, 'rel': rel}, open(cache, 'w'))
            except Exception: pass
            GPU['info'] = '%s: one segment takes %.2fs on the processor and %.2fs on the graphics card (difference %.4f), graphics card used: %s' % (name, tc, tg, rel, use)
            sys.stderr.write('gpu test: cpu %.2fs gpu %.2fs diff %.4f use=%s\n' % (tc, tg, rel, use)); sys.stderr.flush()
        if use:
            for m, o in zip(model.models, GPU['orig']): patch(m, o); m.to(dev)   # every model of the bag on the card
            DEV = dev; GPU['name'] = name
        else:
            to_cpu()
    except Exception as e:
        sys.stderr.write('gpu unavailable: %s\n' % e); sys.stderr.flush(); to_cpu()

pick_cuda()
pick_gpu()
print('INFO|torch %s, %d threads, %s logical cores; %s' % (torch.__version__, torch.get_num_threads(), os.cpu_count(), GPU.get('info', '')), flush=True)
print('DEVICE|%s' % (('gpu: ' + GPU.get('name', '')) if DEV != 'cpu' else 'cpu'), flush=True)

def run_model(wav, overlap, show, stats=None):
    # the level the model sees must be the same for every piece of a long song, so the pieces match at the seams: stats = (mean, std) of the WHOLE song
    if stats is None:
        ref = wav.mean(0); mu = float(ref.mean()); sd = float(ref.std()) + 1e-8
    else:
        mu, sd = stats
    wav = (wav - mu) / sd
    random.seed(1234)   # the model shifts the song by a random amount: a fixed seed makes every run give the same result
    try:
        with torch.no_grad():
            out = apply_model(model, wav[None], device=DEV, shifts=1, split=True, overlap=overlap, progress=show, num_workers=0)[0].cpu()
    except Exception as e:
        if DEV == 'cpu': raise
        sys.stderr.write('graphics card failed (%s): continuing on the processor\n' % e); sys.stderr.flush(); to_cpu()
        with torch.no_grad():
            out = apply_model(model, wav[None], device='cpu', shifts=1, split=True, overlap=overlap, progress=show, num_workers=0)[0]
    out = out * sd + mu
    v = out[VI]; i = (wav * sd + mu) - v   # the beat is the song itself minus the separated vocal: nothing of the beat is lost, and vocals + beat add up to the song exactly
    return v.cpu().numpy().T, i.cpu().numpy().T

def read(inp, start, dur):
    kw = dict(streams=0, samplerate=SR, channels=model.audio_channels)
    if start is not None:
        kw['seek_time'] = start; kw['duration'] = dur
    return AudioFile(inp).read(**kw)

def song_stats(inp, total):
    s = 0.0; s2 = 0.0; n = 0; p = 0.0
    while p < total - 0.01:
        d = min(120.0, total - p); x = read(inp, p, d).mean(0).double()
        s += float(x.sum()); s2 += float((x * x).sum()); n += x.numel(); p += d
    mu = s / max(1, n); var = max(0.0, s2 / max(1, n) - mu * mu)
    return (mu, var ** 0.5 + 1e-8)

def open_wav(path, big):
    return sf.SoundFile(path, 'w', samplerate=SR, channels=2, subtype='FLOAT', format='RF64' if big else 'WAV')

print('READY', flush=True)
for line in sys.stdin:
    line = line.strip()
    if not line.startswith('JOB|'): continue
    try:
        _, inp, outdir, overlap, dur, chunk = line.split('|', 5)
        overlap = float(overlap); total = float(dur); CH = float(chunk)
        os.makedirs(outdir, exist_ok=True)
        vp = os.path.join(outdir, 'vocals.wav'); ip = os.path.join(outdir, 'no_vocals.wav')
        if CH <= 0 or total <= 0 or total <= CH * 1.3:
            v, i = run_model(read(inp, None, None), overlap, True)
            sf.write(vp, v, SR, subtype='FLOAT'); sf.write(ip, i, SR, subtype='FLOAT')
        else:
            # long song: process it in chunks with a crossfaded overlap, writing as we go, so memory stays small
            OV = 4.0; ovn = int(OV * SR); big = total > 2400
            stats = song_stats(inp, total)
            wv = open_wav(vp, big); wi = open_wav(ip, big)
            pos = 0.0; tail_v = None; tail_i = None; first = True
            while True:
                d = min(CH, total - pos)
                if total - (pos + d) < 2 * OV: d = total - pos
                last = (pos + d >= total - 0.01)
                v, i = run_model(read(inp, pos, d), overlap, False, stats)
                n = v.shape[0]
                if first:
                    cut = n if last else n - ovn
                    wv.write(v[:cut]); wi.write(i[:cut])
                else:
                    k = min(ovn, n, tail_v.shape[0])
                    w = np.linspace(0.0, 1.0, k, dtype=np.float32)[:, None]
                    wv.write(tail_v[:k] * (1 - w) + v[:k] * w); wi.write(tail_i[:k] * (1 - w) + i[:k] * w)
                    cut = n if last else n - ovn
                    if cut > k:
                        wv.write(v[k:cut]); wi.write(i[k:cut])
                if not last:
                    tail_v = v[n - ovn:]; tail_i = i[n - ovn:]
                first = False
                sys.stderr.write('%d%%|\n' % int(min(99, (pos + d) / total * 100))); sys.stderr.flush()
                if last: break
                pos += d - OV
            wv.close(); wi.close()
        print('DONE|ok', flush=True)
    except Exception as e:
        print('ERR|%s' % e, flush=True)
";    public static int ChunkOverride;   // seconds; for tests (0 = automatic: chunks of 240 s for songs over 10 minutes)
    static Process wproc; static StreamWriter win; static volatile bool wready; static volatile string wresult; static volatile Action<float> wprog;
    static DateTime wlast = DateTime.Now; static volatile bool wbusy; static readonly object wlock = new object(); static System.Threading.Timer idleTimer;
    static readonly Regex PctRx = new Regex(@"(\d+)%\|");

    // Start loading the model in the background (call when the user opens the vocal remover).
    public static void Warmup() { if (!Installed) return; new Thread(() => { try { EnsureWorker(); } catch { } }) { IsBackground = true }.Start(); }

    // The script is not left on disk as readable text: it is packed (compressed, then scrambled with a key) and a one-line starter unpacks it inside Python.
    static readonly byte[] WorkerKey = { 0x5A, 0xC3, 0x19, 0xE7, 0x82, 0x4D, 0xB6, 0x2F };
    internal const string WorkerHead = "import sys,zlib;d=open(sys.argv[1],'rb').read();k=bytes([90,195,25,231,130,77,182,47]);", WorkerDecode = "zlib.decompress(bytes(c^k[i%8] for i,c in enumerate(d)),-15).decode('utf-8')";
    const string WorkerBoot = WorkerHead + "exec(compile(" + WorkerDecode + ",'worker','exec'))";
    internal static string WorkerSource { get { return WorkerPy; } }
    internal static byte[] PackWorker() { return Pack(WorkerPy); }
    internal static byte[] Pack(string text)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(text); byte[] b; using (var ms = new MemoryStream()) { using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length); b = ms.ToArray(); }
        for (int i = 0; i < b.Length; i++) b[i] ^= WorkerKey[i % WorkerKey.Length]; return b;
    }
    internal static string Unpack(byte[] packed)
    {
        var b = (byte[])packed.Clone(); for (int i = 0; i < b.Length; i++) b[i] ^= WorkerKey[i % WorkerKey.Length];
        using (var ms = new MemoryStream(b)) using (var ds = new DeflateStream(ms, CompressionMode.Decompress)) using (var sr = new StreamReader(ds, System.Text.Encoding.UTF8)) return sr.ReadToEnd();
    }
    static bool EnsureWorker()
    {
        lock (wlock)
        {
            if (wproc != null && !wproc.HasExited) return true;
            if (!Installed) return false;
            try { File.Delete(Path.Combine(Root, "demucs_worker.py")); } catch { }   // older versions left the script here as readable text
            string wdat = Path.Combine(Root, "demucs_worker.dat"); File.WriteAllBytes(wdat, PackWorker());
            var psi = new ProcessStartInfo(PyExe, "-u -c \"" + WorkerBoot + "\" \"" + wdat + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Root,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            foreach (var kv in Env(true)) psi.EnvironmentVariables[kv.Key] = kv.Value;
            wready = false; wresult = null; wproc = new Process { StartInfo = psi };
            wproc.OutputDataReceived += (s, e) => { if (e.Data == null) return; if (e.Data == "READY") wready = true; else if (e.Data.StartsWith("DEVICE|")) Device = e.Data.Substring(7); else if (e.Data.StartsWith("INFO|")) Diag("engine: " + e.Data.Substring(5)); else if (e.Data.StartsWith("DONE|") || e.Data.StartsWith("ERR|")) wresult = e.Data; };
            wproc.ErrorDataReceived += (s, e) => { if (e.Data == null) return; var m = PctRx.Match(e.Data); var pr = wprog; if (m.Success && pr != null) pr(Math.Min(0.98f, int.Parse(m.Groups[1].Value) / 100f)); };
            wproc.Start(); try { wproc.PriorityClass = ProcessPriorityClass.Normal; } catch { } wproc.BeginOutputReadLine(); wproc.BeginErrorReadLine(); win = wproc.StandardInput; wlast = DateTime.Now;
            if (idleTimer == null) idleTimer = new System.Threading.Timer(_ => { if (!wbusy && (DateTime.Now - wlast).TotalMinutes > 10) KillWorker(); }, null, 60000, 60000);
        }
        return true;
    }

    public static void KillWorker()
    {
        lock (wlock) { try { if (wproc != null && !wproc.HasExited) Kill(wproc); } catch { } wproc = null; wready = false; }
    }

    static void MoveOver(string from, string to) { try { if (File.Exists(to)) File.Delete(to); File.Move(from, to); } catch { File.Copy(from, to, true); } }

    // 1 = ok, 0 = worker unusable (caller falls back), -1 = cancelled
    static int SeparateWorker(string input, string outDir, string baseName, Action<string> log, Action<float> prog, Func<bool> cancelled, out string vocalsPath, out string instPath)
    {
        vocalsPath = null; instPath = null; startupDied = false;
        string tmp = Path.Combine(outDir, ".sep_tmp_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        try
        {
            if (!EnsureWorker()) return 0;
            var sw = Stopwatch.StartNew(); bool said = false;
            while (!wready)
            {
                if (cancelled()) return -1; if (wproc == null || wproc.HasExited) { startupDied = true; return 0; } if (sw.Elapsed.TotalSeconds > 240) { startupDied = true; return 0; }
                if (!said && sw.Elapsed.TotalSeconds > 1) { log("Loading the AI model (a few seconds)..."); said = true; }
                Thread.Sleep(100);
            }
            double ready = sw.Elapsed.TotalSeconds; var jobSw = Stopwatch.StartNew(); double firstP = -1; var userProg = prog; prog = pp => { if (firstP < 0 && pp > 0) firstP = jobSw.Elapsed.TotalSeconds; userProg(pp); };
            wbusy = true; wresult = null; wprog = prog; Directory.CreateDirectory(outDir);
            double dur = AudioClip.ProbeSeconds(input); if (dur > 600) log("Long song (" + (int)(dur / 60) + " min): processing it in chunks to keep memory low.");
            win.WriteLine("JOB|" + input + "|" + tmp + "|0.25|" + dur.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + (ChunkOverride > 0 ? ChunkOverride : (dur > 600 ? 240 : 0))); win.Flush();
            while (wresult == null)
            {
                if (cancelled()) { KillWorker(); return -1; }
                if (wproc == null || wproc.HasExited) return 0;
                Thread.Sleep(120);
            }
            wlast = DateTime.Now; string r = wresult; double took = jobSw.Elapsed.TotalSeconds;
            Diag("app " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + ", " + (DisableFast ? "normal engine" : (CudaReady && !cudaBad ? "NVIDIA engine" : (FastReady && !fastBad ? "fast engine" : "normal engine"))) + ", device " + Device + "; model ready after " + ready.ToString("0.0") + " s; song " + dur.ToString("0") + " s; first progress after " + (firstP < 0 ? 0 : firstP).ToString("0.0") + " s; total " + took.ToString("0.0") + " s = " + (dur / Math.Max(0.1, took)).ToString("0.0") + "x realtime; result " + (r.Length > 60 ? r.Substring(0, 60) : r));
            if (!r.StartsWith("DONE|")) { log("Worker: " + r); return 0; }
            string v = Path.Combine(tmp, "vocals.wav"), i = Path.Combine(tmp, "no_vocals.wav");
            if (!File.Exists(v) || !File.Exists(i)) return 0;
            vocalsPath = Path.Combine(outDir, baseName + " - vocals.wav"); instPath = Path.Combine(outDir, baseName + " - instrumental.wav");
            MoveOver(v, vocalsPath); MoveOver(i, instPath); prog(1f); return 1;
        }
        catch (Exception ex) { log("Worker error: " + ex.Message); return 0; }
        finally { wbusy = false; wprog = null; try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } }
    }

    // Splits a song into real VOCAL and INSTRUMENTAL audio files. Uses the warm worker when possible.
    public static bool Separate(string input, string outDir, string baseName, string model, Action<string> log, Action<float> prog, Func<bool> cancelled,
        out string vocalsPath, out string instPath)
    {
        vocalsPath = null; instPath = null;
        if (model == "htdemucs" && input.IndexOf('|') < 0 && outDir.IndexOf('|') < 0)
        {
            // the NVIDIA turbo engine, when the user agreed to it
            if (WantCuda && CudaOffer)
            {
                CudaInstall(log, p => prog(p * 0.5f), cancelled); WantCuda = false; if (cancelled()) { log("Cancelled."); return false; }
                KillWorker(); var inner0 = prog; prog = p => inner0(0.5f + p * 0.5f);
            }
            // an engine installed before the fast stack existed gets it once, the first time it is used
            if (Installed && !FastReady && !CudaReady && !File.Exists(FastFailed))
            {
                FastInstall(log, p => prog(p * 0.15f), cancelled); if (cancelled()) { log("Cancelled."); return false; }
                KillWorker();   // a worker started earlier (Warmup) used the old engine: start a new one with the fast stack
                var inner = prog; prog = p => inner(0.15f + p * 0.85f);
            }
            string v, i; int r = SeparateWorker(input, outDir, baseName, log, prog, cancelled, out v, out i);
            if (r == 0 && startupDied && CudaReady && !cudaBad)
            {
                cudaBad = true; try { File.WriteAllText(CudaFailed, "worker did not start"); } catch { } KillWorker(); log("(the NVIDIA engine did not start here; using the other engine)"); Diag("NVIDIA engine did not start");
                r = SeparateWorker(input, outDir, baseName, log, prog, cancelled, out v, out i);
            }
            if (r == 0 && startupDied && FastReady && !fastBad)
            {
                // the faster engine would not start on this PC: remember that and use the normal one from now on
                fastBad = true; try { File.WriteAllText(FastFailed, "worker did not start"); } catch { } KillWorker(); log("(the faster engine did not start here; using the normal one)");
                r = SeparateWorker(input, outDir, baseName, log, prog, cancelled, out v, out i);
            }
            if (r == 1) { vocalsPath = v; instPath = i; return true; }
            if (r == -1) { log("Cancelled."); return false; }
            log("(fast worker unavailable, using the standard path)");
        }
        return SeparateCli(input, outDir, baseName, model, log, prog, cancelled, out vocalsPath, out instPath);
    }
    static bool SeparateCli(string input, string outDir, string baseName, string model, Action<string> log, Action<float> prog, Func<bool> cancelled,
        out string vocalsPath, out string instPath)
    {
        vocalsPath = null; instPath = null;
        string tmp = Path.Combine(outDir, ".sep_tmp_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(tmp);
        try
        {
            log("Running AI separation (" + model + ")...  first run also downloads the model (~80 MB).");
            string args = "-m demucs.separate -n " + model + " --two-stems=vocals --float32 --overlap 0.1 -o \"" + tmp + "\" --filename \"{stem}.{ext}\" \"" + input + "\"";
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
