using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Purphex Music window (part 2): actions (import, separate, auto mix, export, save/open),
//  busy overlay, mastering tab, and small dialogs.
// ======================================================================
class ExportSettings { public bool MixMastered = true, MixDry, ProcVocal, ProcBeat, DryVocal, DryBeat, Stems; public int Fmt; public string Dir = "", Prefix = ""; }

partial class StudioForm
{
    // ---------------- busy overlay + background work ----------------
    readonly EtaMeter busyEta = new EtaMeter();
    void RunBusy(string text, bool cancelable, Action<Action<float>> work, Action<bool> done)
    {
        if (busy) return;
        eng.Pause(); browserOpen = false; busyEta.Reset(); busy = true; busyText = text; busyPct = -1; cancelFlag = false; busyCancelable = cancelable; busyStart = DateTime.Now;
        new Thread(() =>
        {
            bool ok = true;
            try { work(p => busyPct = p); }
            catch (Exception ex) { ok = false; string m = ex.Message; try { BeginInvoke(new Action(() => MessageBox.Show(this, m, "Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning))); } catch { } }
            finally { busy = false; busyPct = -1; }
            try { if (done != null) BeginInvoke(new Action(() => done(ok))); } catch { }
        }) { IsBackground = true }.Start();
    }

    void PaintBusy(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(185, 6, 3, 14))) g.FillRectangle(br, 0, 0, ClientSize.Width, ClientSize.Height);
        Reg(Rc(0, 0, ClientSize.Width, ClientSize.Height), () => { });
        float w = 520; foreach (var ln in busyText.Split((char)10)) w = Math.Max(w, g.MeasureString(ln, GfxCache.Font(17, true)).Width + 80); w = Math.Min(w, ClientSize.Width - 30);   // the box grows to fit its text...
        float tpx = 17; { float widest = 0; foreach (var ln in busyText.Split((char)10)) widest = Math.Max(widest, g.MeasureString(ln, GfxCache.Font(17, true)).Width); if (widest > w - 50) tpx = Math.Max(10, 17 * (w - 50) / widest); }   // ...and if the window is too small for that, the text gets smaller
        float h = busyCancelable ? 170 : 130, x = (ClientSize.Width - w) / 2, y = (ClientSize.Height - h) / 2; var box = Rc(x, y, w, h);
        FillRR(g, Color.FromArgb(252, 28, 15, 52), box, 14); StrokeRR(g, Accent, box, 14, 1.5f);
        Txt(g, busyText, tpx, Ink, Rc(x + 20, y + 18, w - 40, 52), StringAlignment.Center, true);
        var bar = Rc(x + 30, y + 78, w - 60, 14); FillRR(g, Color.FromArgb(220, 8, 4, 20), bar, 7);
        float p = busyPct;
        if (p >= 0) { float f = Clamp01(p); if (f > 0.01f) using (var br = new LinearGradientBrush(bar, Violet, Accent2, 0f)) using (var pth = RR(Rc(bar.X, bar.Y, bar.Width * f, bar.Height), 7)) g.FillPath(br, pth); }
        else { float cx = bar.X + (float)((t * 0.7) % 1.0) * (bar.Width - 90); using (var br = new LinearGradientBrush(Rc(cx, bar.Y, 90, bar.Height), Violet, Accent2, 0f)) using (var pth = RR(Rc(cx, bar.Y, 90, bar.Height), 7)) g.FillPath(br, pth); }
        double el = (DateTime.Now - busyStart).TotalSeconds; if (p >= 0) busyEta.Add(p); string es = p >= 0 ? busyEta.Text(p) : ""; string eta = es.Length > 0 ? "about " + es + " left" : (el > 2 ? "working for " + FmtEta(el) : "");
        Txt(g, eta, 13, DimC, Rc(x + 30, y + 96, w - 60, 20), StringAlignment.Center, false);
        if (busyCancelable) Btn(g, Rc(x + w / 2 - 60, y + h - 50, 120, 34), "Cancel (Esc)", 3, () => { cancelFlag = true; busyText = "Cancelling..."; }, 14).WhileBusy = true;
    }

    // ---------------- Add Music ----------------
    int nextInsert = -1;   // where the next track added should go (-1 = at the end); set by the + ADD TRACK strips between tracks
    internal void AddMusicClick(bool keepInsert = false)
    {
        if (!keepInsert) nextInsert = -1;
        using (var d = new OpenFileDialog())
        {
            d.Title = "Add music: choose a song, a vocal recording or a beat / instrumental"; d.Multiselect = true;
            d.Filter = "Audio files|*.mp3;*.wav;*.flac;*.aiff;*.aif;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.mp4|All files|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            foreach (var f in d.FileNames) PromptImport(f);
        }
    }

    internal void PromptImportPublic(string p) { PromptImport(p); }
    readonly Queue<string> importQueue = new Queue<string>();   // several files chosen at once are added one after the other
    internal void PromptImport(string path)
    {
        if (path.EndsWith(".ytmproj", StringComparison.OrdinalIgnoreCase)) { OpenProjectPath(path); return; }
        if (busy) { importQueue.Enqueue(path); Toast("Queued " + Path.GetFileName(path)); return; }
        ImportFile(path, "song");   // straight onto the chart: no "what is this file?" question
    }

    internal void ImportFile(string path, string role)
    {
        AudioClip clip = null;
        RunBusy("Loading " + Path.GetFileName(path) + "...", true, p => { clip = AudioClip.FromFile(path, p, () => cancelFlag); }, ok =>
        {
            if (!ok || clip == null) { if (importQueue.Count > 0) { string nx = importQueue.Dequeue(); BeginInvoke(new Action(() => PromptImport(nx))); } return; }
            string nm = role == "vocal" ? "Vocals" : (role == "beat" ? "Instrumental" : clip.Name);
            AddTrack(StudioEngine.NewTrack(nm, role, clip)); Toast("Added " + nm);
            if (importQueue.Count > 0) { string nx = importQueue.Dequeue(); BeginInvoke(new Action(() => PromptImport(nx))); }
        });
    }

    void AddTrack(Track t)
    {
        PushUndo();
        bool first = eng.Tracks.Count == 0; int at = nextInsert >= 0 && nextInsert <= eng.Tracks.Count ? nextInsert : eng.Tracks.Count; nextInsert = -1; at = t.IsBus ? eng.Tracks.Count : (t.Host != null ? FirstBusIndex() : Math.Min(at, LaneCount));   // buses always sit after the tracks
        lock (eng.Sync) eng.Tracks.Insert(at, t); selTrack = at; selFx = -1; if (first) FitView();
    }

    // ---------------- Separate vocals & beat ----------------
    void SeparateClick() { Separator.Warmup(); PickTrackMenu("Split which track into vocals + beat?", t => t.Clip != null && !t.Instrument && !t.IsBus, SeparateTrack); }
    void SeparateTrack(Track song)
    {
        if (song == null) return;
        if (song.Clip == null || string.IsNullOrEmpty(song.Clip.Path) || !File.Exists(song.Clip.Path)) { MessageBox.Show(this, "That track's audio file couldn't be found on disk."); return; }
        if (StudioEnv.Ffmpeg() == null) { MessageBox.Show(this, "ffmpeg isn't installed yet. Convert something once in the main window first (that installs ffmpeg)."); return; }
        if (!Separator.Installed && MessageBox.Show(this,
            "Vocal / beat separation uses an AI model (Demucs, MIT licence) that runs on your PC.\n\nOne-time download: about 0.5 GB (a private copy of Python, PyTorch and the model), stored in your AppData folder. Nothing is installed system-wide.\n\nDownload and set it up now?",
            "Separation setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string src = song.Clip.Path, baseName = Path.GetFileNameWithoutExtension(src); long off = song.Offset; Track st = song;
        string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Studio", baseName);
        Separator.OfferTurbo(this);   // NVIDIA cards can run the AI several times faster (asks once)
        AudioClip vc = null, ic = null; bool installNow = !Separator.Installed;
        RunBusy("Separating vocals and beat with AI...\n(under a minute with a good graphics card)", true, prog =>
        {
            if (installNow) { busyText = "Setting up the separation engine (one time)..."; Separator.Install(m => { }, p => prog(p * 0.45f), () => cancelFlag); }
            busyText = "Separating vocals and beat with AI...\n(under a minute with a good graphics card)";
            string v, i; float lo = installNow ? 0.45f : 0f;
            if (!Separator.Separate(src, outDir, baseName, "htdemucs", m => { }, p => prog(lo + p * (0.92f - lo)), () => cancelFlag, out v, out i)) throw new Exception(cancelFlag ? "Cancelled." : "Separation failed.");
            busyText = "Loading the separated tracks..."; vc = AudioClip.FromFile(v); prog(0.96f); ic = AudioClip.FromFile(i);
        }, ok =>
        {
            if (!ok || vc == null || ic == null) return;
            string sname = st.Name; var tv = StudioEngine.NewTrack(sname + " - Vocals", "vocal", vc); var tb = StudioEngine.NewTrack(sname + " - Instrumental", "beat", ic); tv.Offset = off; tb.Offset = off; tv.ClipStart = tb.ClipStart = st.ClipStart; tv.ClipFrames = tb.ClipFrames = st.ClipFrames;
            st.Mute = true; if (!st.Name.EndsWith("(original)")) st.Name += " (original)";
            if (st.Clip != null && st.Clip.Length > StudioEngine.SR * 600) st.Clip.Unload();   // long originals leave memory; reloaded if you un-mute them
            lock (eng.Sync) { int ins = LaneCount; eng.Tracks.Insert(ins, tv); eng.Tracks.Insert(ins + 1, tb); } selTrack = eng.Tracks.IndexOf(tv); selFx = -1;   // the new tracks go with the other rows, never after the buses
            Toast("Separated: vocals and instrumental are ready, perfectly in sync. Stems saved in " + outDir);
        });
    }

    // ---------------- key detection ----------------
    void DetectKey(PitchCorrector pc, Track tr)
    {
        if (tr == null || tr.Clip == null) return; int key = 0, scale = 1; double conf = 0; bool found = false;
        RunBusy("Detecting the key of " + tr.Name + "...", false, p => { found = KeyDetect.Detect(tr.Clip, out key, out scale, out conf); }, ok =>
        {
            if (!ok) return;
            if (!found) { Toast("Couldn't find a clear key (needs sung or rapped pitched notes)."); return; }
            pc.Set("key", key); pc.Set("scale", scale); pc.ApplyScale(); Toast("Detected " + PitchCorrector.Keys[key] + (scale == 2 ? " minor" : " major") + "  (confidence " + conf.ToString("0.00") + ")");
        });
    }

    // ---------------- Auto Mix ----------------
    void AutoMixClick()
    {
        bool hasV = false, hasB = false; foreach (var t in eng.Tracks) { if (t.Role == "vocal" && t.Clip != null) hasV = true; if (t.Role == "beat" && t.Clip != null) hasB = true; }
        bool hasSong = false; foreach (var t in eng.Tracks) if (t.Clip != null && !t.Instrument && (t.Role == "song" || t.Role == "other")) hasSong = true;
        if (!hasV && !hasB && !hasSong) { Toast("Add a song, a vocal or a beat first."); return; }
        if (!AskYes("Run Auto Mix?\n\nIt changes the volume, pan and effects of your tracks. UNDO MIX puts everything back, and you can cancel while it works.", "Auto Mix")) return;
        List<string> rep = null; string style = amStyle; bool enh = amEnhance;
        RunBusy("Auto Mix: analysing loudness, tone and clipping...", true, p => { rep = AutoMix.Run(eng, style, enh, s => busyText = "Auto Mix: " + s); }, ok =>
        {
            if (cancelFlag && ok) { AutoMix.Undo(eng); Toast("Auto Mix cancelled: nothing was changed."); return; }
            if (!ok || rep == null) return; selFx = -1;
            StudioDialogs.Report(this, "Auto Mix: what changed", rep, () => { AutoMix.Undo(eng); Toast("Auto Mix undone"); });
        });
    }

    // ---------------- Export / Save / Open ----------------
    void ExportClick()
    {
        if (eng.Tracks.Count == 0) { Toast("Nothing to export yet."); return; }
        var s = StudioDialogs.ExportOptions(this, projName == "Untitled project" ? "My song" : projName); if (s == null) return;
        var plan = Exporter.Plan(eng, s.MixMastered, s.MixDry, s.ProcVocal, s.ProcBeat, s.DryVocal, s.DryBeat, s.Stems, s.Prefix, s.Fmt);
        if (plan.Count == 0) { MessageBox.Show(this, "Nothing selected to export (or the needed tracks aren't in the project)."); return; }
        var exist = new List<string>(); foreach (var it in plan) { string p = Path.Combine(s.Dir, it.FileName); if (File.Exists(p)) exist.Add(it.FileName); }
        if (exist.Count > 0 && MessageBox.Show(this, exist.Count + " file(s) already exist in that folder and will be replaced:\n\n" + string.Join("\n", exist.GetRange(0, Math.Min(6, exist.Count)).ToArray()) + (exist.Count > 6 ? "\n..." : "") + "\n\nReplace them? (Your original source audio is never overwritten.)", "Replace files?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        // never overwrite one of the project's own source files
        foreach (var it in plan) { string p = Path.GetFullPath(Path.Combine(s.Dir, it.FileName)); foreach (var t in eng.Tracks) if (t.Clip != null && t.Clip.Path != null && string.Equals(Path.GetFullPath(t.Clip.Path), p, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Export would overwrite the original audio file \"" + Path.GetFileName(p) + "\". Choose another folder or name."); return; } }
        int n = plan.Count; var done = new List<string>();
        RunBusy("Exporting " + n + " file(s)...", true, prog =>
        {
            for (int i = 0; i < n; i++)
            {
                if (cancelFlag) throw new Exception("Export cancelled."); int ii = i; busyText = "Exporting " + (i + 1) + " of " + n + ": " + plan[i].Label;
                done.Add(Exporter.Write(eng, plan[i], s.Dir, s.Fmt, p => prog((ii + p) / n), () => cancelFlag));
            }
        }, ok =>
        {
            if (!ok) return; Toast("Exported " + done.Count + " file(s)"); try { System.Diagnostics.Process.Start("explorer.exe", "\"" + s.Dir + "\""); } catch { }
        });
    }

    void SaveClick(bool asNew)
    {
        if (eng.Tracks.Count == 0) { Toast("Nothing to save yet."); return; }
        FlexCommitIfOpen();
        string path = projPath;
        if (asNew || string.IsNullOrEmpty(path))
        {
            using (var d = new SaveFileDialog())
            {
                d.Title = "Save project"; d.Filter = "Studio project (*.ytmproj)|*.ytmproj"; d.FileName = projName == "Untitled project" ? "My song" : projName;
                d.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3");
                try { Directory.CreateDirectory(d.InitialDirectory); } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return; path = d.FileName;
            }
        }
        try { ProjectIO.Save(eng, path); projPath = path; projName = Path.GetFileNameWithoutExtension(path); NoteProject(path, true); Toast("Project saved"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed"); }
    }

    const string AudioExts = "*.mp3;*.wav;*.flac;*.aiff;*.aif;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.mp4;*.mkv;*.webm";
    void OpenClick()
    {
        string[] picked;
        using (var d = new OpenFileDialog()) { d.Title = "Open a project, or add music (mp3, wav, flac...)"; d.Multiselect = true; d.Filter = "Projects and audio|*.ytmproj;" + AudioExts + "|Studio project (*.ytmproj)|*.ytmproj|Audio files|" + AudioExts + "|All files|*.*"; if (d.ShowDialog(this) != DialogResult.OK) return; picked = d.FileNames; }
        foreach (var f in picked) if (f.EndsWith(".ytmproj", StringComparison.OrdinalIgnoreCase)) { OpenProjectPath(f); return; }
        foreach (var f in picked) PromptImport(f);   // audio files are added as tracks
    }
    internal void OpenProjectPath(string path)
    {
        if (busy) { Toast("Wait for the current job to finish"); return; }
        List<string> warn = null;
        RunBusy("Opening project...", false, p => { warn = ProjectIO.Load(eng, path, s => busyText = s); }, ok =>
        {
            if (!ok) return; bpmUserSet = true; projPath = path; projName = Path.GetFileNameWithoutExtension(path); selTrack = 0; selFx = -1; undo.Clear(); redo.Clear(); FitView(); NoteProject(path, false);
            if (warn != null && warn.Count > 0) MessageBox.Show(this, string.Join("\n", warn.ToArray()), "Some audio files are missing"); else Toast("Project opened");
        });
    }

    static string FmtEta(double s) { if (s < 90) return (int)s + " s"; if (s < 5400) return (int)Math.Ceiling(s / 60) + " min"; return (s / 3600).ToString("0.0", CultureInfo.InvariantCulture) + " h"; }

    // A long original that was unloaded after separation is read back from disk when you un-mute / solo it.
    void ToggleMute(Track tk)
    {
        if (tk.Mute && tk.Clip != null && !tk.Clip.Loaded) { RunBusy("Loading " + tk.Name + " back into memory...", true, p => tk.Clip.Reload(p, () => cancelFlag), ok => { if (ok) tk.Mute = false; }); return; }
        tk.Mute = !tk.Mute;
    }
    void ToggleSolo(Track tk)
    {
        if (!tk.Solo && tk.Clip != null && !tk.Clip.Loaded) { RunBusy("Loading " + tk.Name + " back into memory...", true, p => tk.Clip.Reload(p, () => cancelFlag), ok => { if (ok) tk.Solo = true; }); return; }
        tk.Solo = !tk.Solo;
    }

    internal string Ask(string prompt, string def) { return StudioDialogs.Ask(this, prompt, def); }

    // ======================================================================
    //  MASTERING TAB
    // ======================================================================
    bool abMastered = true, matchOn = true; LoudnessResult abOrig, abMast; float targetLufs = -14;
    static readonly string[] ModNames = { "Master EQ", "Compressor", "Multiband comp.", "Exciter", "Saturation", "Stereo imaging", "Limiter" };

    void ApplyMatch() { eng.AbMatchDb = (matchOn && abOrig != null && abMast != null && !double.IsInfinity(abOrig.I) && !double.IsInfinity(abMast.I)) ? (float)(abMast.I - abOrig.I) : 0f; }

    // How far each tone region is from a balanced mix, and the EQ gain (dB) that corrects it, for master bands 1, 2, 4, 5, 6.
    internal static double[] MasterToneGains(TrackStats st, out double[] deviation)
    {
        double[] T = { 5, 4.5, 3.5, 2, 0.5, 0, -1.5, -3, -5, -8, -13 }; var dev = new double[11]; for (int b = 0; b < 11; b++) dev[b] = st.BandDb[b] - T[b];
        deviation = new[] { (dev[0] + dev[1]) / 2, (dev[2] + dev[3]) / 2, (dev[7] + dev[8]) / 2, dev[9], dev[10] }; var g = new double[5];
        for (int i = 0; i < 5; i++) { double d = deviation[i]; g[i] = Math.Abs(d) < 1.5 ? 0 : Math.Max(-4, Math.Min(4, -0.55 * (d - Math.Sign(d) * 1.5))); }
        return g;
    }

    void AutoLoudness(string[] revertTo = null)
    {
        Fx lim = null; foreach (var f in eng.MasterFx) if (f.Type == "limiter") lim = f; if (lim == null) { Toast("Add a Limiter to the master chain first."); return; }
        if (eng.Tracks.Count == 0) { Toast("Add music first."); return; }
        long frames = Math.Min(eng.EndFrame, StudioEngine.SR * 120); float target = targetLufs; double finalI = 0; var chainBefore = revertTo ?? SnapChain(); float targetBefore = targetLufs;
        RunBusy("Setting the mix to " + target.ToString("0.0") + " LUFS...", false, prog =>
        {
            bool was = eng.MasterBypass; eng.MasterBypass = false;
            for (int it = 0; it < 4; it++)
            {
                float[] l, r; eng.Render(0, frames, new RenderOpts { Master = true }, null, out l, out r); var m = Loudness.Measure(l, r, StudioEngine.SR, 0, l.Length); finalI = m.I; prog((it + 1) / 4f);
                if (double.IsInfinity(m.I) || Math.Abs(target - m.I) < 0.25) break;
                lim.Set("gain", (float)DspMath.Clamp(lim.Get("gain") + (target - m.I), 0, 24));
            }
            eng.MasterBypass = was;
        }, ok => { if (ok) { Toast("Loudness now about " + finalI.ToString("0.0") + " LUFS (limiter gain " + lim.Get("gain").ToString("0.0") + " dB, ceiling " + lim.Get("ceil").ToString("0.0") + ")"); abOrig = abMast = null; OfferRevert("Loudness set to about " + finalI.ToString("0.0") + " LUFS", () => { RestoreChain(chainBefore); abOrig = abMast = null; }); } });
    }
}

// ======================================================================
//  Small dialogs (WinForms, themed)
// ======================================================================
static class StudioDialogs
{
    static readonly Color Bg = Color.FromArgb(24, 12, 44), Fld = Color.FromArgb(34, 16, 62), Ink = Color.FromArgb(240, 232, 255);

    internal static Form Make(string title, int w, int h)
    {
        var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Bg, ForeColor = Ink, ClientSize = new Size(w, h), Font = new Font("Segoe UI", 10f) };
        Win.Dark(f); f.Shown += (s, e) => Win.CapsAll(f); return f;
    }
    internal static Button B(string text, int x, int y, int w, int h, Color c) { var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); return b; }

    // ADD MUSIC in the main window: what do you want to do with a song? 0 = cancelled, 1 = edit in the studio, 2 = vocal remover, 3 = trim
    public static int AddMusicChoice(IWin32Window owner)
    {
        int result = 0; var f = Make("Add music", 520, 292);
        f.Controls.Add(new Label { Text = "What do you want to do with your music?", Left = 20, Top = 16, Width = 480, Height = 24, ForeColor = Color.FromArgb(200, 170, 255), Font = new Font("Segoe UI Semibold", 11f) });
        var b1 = B("Edit it in Purphex Music  (tracks, mixing, effects, mastering)", 20, 52, 480, 44, Color.FromArgb(118, 52, 212)); var b2 = B("Vocal remover  (take the vocals or the beat out of a song)", 20, 104, 480, 44, Color.FromArgb(160, 56, 170)); var b3 = B("Trim start and end  (cut it and save as MP3)", 20, 156, 480, 44, Color.FromArgb(70, 44, 140));
        b1.Click += (s, e) => { result = 1; f.Close(); }; b2.Click += (s, e) => { result = 2; f.Close(); }; b3.Click += (s, e) => { result = 3; f.Close(); };
        f.Controls.AddRange(new Control[] { b1, b2, b3 }); f.Controls.Add(new Label { Text = "Your original file is never changed.", Left = 20, Top = 214, Width = 480, Height = 22, ForeColor = Color.FromArgb(170, 150, 200) });
        var cancel = B("Cancel", 400, 246, 100, 30, Color.FromArgb(60, 30, 90)); cancel.Click += (s, e) => f.Close(); f.Controls.Add(cancel);
        f.ShowDialog(owner); return result;
    }

    static bool ParseClock(string t, out double sec)
    {
        sec = 0; t = (t ?? "").Trim(); if (t.Length == 0) return true; double total = 0; foreach (var part in t.Split(':')) { double v; if (!double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < 0) return false; total = total * 60 + v; }
        sec = total; return true;
    }
    // Trim a file you pick: set where it starts and ends and save the cut as MP3 or WAV in the converter's save folder
    public static void TrimFile(IWin32Window owner, string outDir)
    {
        var f = Make("Trim start and end", 540, 300); var tb = new TextBox { Left = 20, Top = 44, Width = 400, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(tb); var browse = B("Browse...", 430, 42, 90, 28, Color.FromArgb(70, 44, 140));
        f.Controls.Add(new Label { Text = "File to trim", Left = 20, Top = 20, Width = 300, Height = 22 }); f.Controls.AddRange(new Control[] { tb, browse });
        var a = new TextBox { Left = 20, Top = 106, Width = 110, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(a); var b = new TextBox { Left = 150, Top = 106, Width = 110, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(b);
        f.Controls.Add(new Label { Text = "Trim start  (m:ss)", Left = 20, Top = 82, Width = 130, Height = 22 }); f.Controls.Add(new Label { Text = "Trim end  (m:ss)", Left = 150, Top = 82, Width = 130, Height = 22 }); f.Controls.AddRange(new Control[] { a, b });
        var fmt = new ComboBox { Left = 290, Top = 106, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Fld, ForeColor = Ink, FlatStyle = FlatStyle.Flat }; fmt.Items.AddRange(new object[] { "MP3 320 kbps", "WAV 24-bit" }); fmt.SelectedIndex = 0;
        f.Controls.Add(new Label { Text = "Save as", Left = 290, Top = 82, Width = 130, Height = 22 }); f.Controls.Add(fmt);
        var note = new Label { Text = "Leave the start empty to begin at 0:00 and the end empty to keep everything to the end. Saved in: " + outDir, Left = 20, Top = 146, Width = 500, Height = 44, ForeColor = Color.FromArgb(170, 150, 200) }; f.Controls.Add(note);
        var go = B("Trim and save", 20, 214, 250, 44, Color.FromArgb(118, 52, 212)); var close = B("Close", 420, 214, 100, 44, Color.FromArgb(60, 30, 90)); close.Click += (s, e) => f.Close(); f.Controls.AddRange(new Control[] { go, close });
        browse.Click += (s, e) => { using (var d = new OpenFileDialog { Filter = "Audio and video|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.mp4;*.webm;*.mkv|All files|*.*" }) if (d.ShowDialog(f) == DialogResult.OK) tb.Text = d.FileName; };
        go.Click += (s, e) =>
        {
            string src = tb.Text.Trim().Trim('"'); double t0, t1; string ff = StudioEnv.Ffmpeg();
            if (!System.IO.File.Exists(src)) { MessageBox.Show(f, "Choose the file to trim first.", "Trim"); return; }
            if (!ParseClock(a.Text, out t0) || !ParseClock(b.Text, out t1)) { MessageBox.Show(f, "Times look like 1:30 or 0:01:30.5", "Trim"); return; }
            if (b.Text.Trim().Length > 0 && t1 <= t0) { MessageBox.Show(f, "The end has to be after the start.", "Trim"); return; }
            if (ff == null) { MessageBox.Show(f, "ffmpeg isn't installed yet. Convert something once first (that installs it).", "Trim"); return; }
            string dir = string.IsNullOrEmpty(outDir) ? System.IO.Path.GetDirectoryName(src) : outDir; try { System.IO.Directory.CreateDirectory(dir); } catch { dir = System.IO.Path.GetDirectoryName(src); }
            bool wav = fmt.SelectedIndex == 1; string baseName = System.IO.Path.GetFileNameWithoutExtension(src) + " (trimmed)", dst = System.IO.Path.Combine(dir, baseName + (wav ? ".wav" : ".mp3")); for (int n = 2; System.IO.File.Exists(dst); n++) dst = System.IO.Path.Combine(dir, baseName + " " + n + (wav ? ".wav" : ".mp3"));
            var args = "-y -hide_banner -loglevel error " + (t0 > 0 ? "-ss " + t0.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " : "") + "-i \"" + src + "\" " + (b.Text.Trim().Length > 0 ? "-t " + (t1 - t0).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " : "") + "-vn " + (wav ? "-c:a pcm_s24le" : "-c:a libmp3lame -b:a 320k") + " \"" + dst + "\"";
            go.Enabled = false; go.Text = "Working..."; string outPath = dst;
            System.Threading.ThreadPool.QueueUserWorkItem(o =>
            {
                string err = ""; try { var p = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(ff, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } }; p.Start(); err = p.StandardError.ReadToEnd(); p.WaitForExit(); if (p.ExitCode != 0 && err.Length == 0) err = "ffmpeg stopped with code " + p.ExitCode; else if (p.ExitCode == 0) err = ""; } catch (Exception ex) { err = ex.Message; }
                try { f.BeginInvoke(new Action(() => { go.Enabled = true; go.Text = "Trim and save"; if (err.Length > 0 || !System.IO.File.Exists(outPath)) MessageBox.Show(f, "Couldn't trim that file.\n\n" + err, "Trim"); else { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + outPath + "\""); } catch { } MessageBox.Show(f, "Saved: " + outPath, "Trim"); } })); } catch { }
            });
        };
        f.ShowDialog(owner);
    }

    public static string ChooseRole(IWin32Window owner, string fileName)
    {
        string result = null; var f = Make("What is this file?", 520, 250);
        f.Controls.Add(new Label { Text = fileName, Left = 20, Top = 16, Width = 480, Height = 24, ForeColor = Color.FromArgb(200, 170, 255), Font = new Font("Segoe UI Semibold", 10.5f) });
        f.Controls.Add(new Label { Text = "Pick what is IN this file. Only the first option removes music; the others just label it.", Left = 20, Top = 44, Width = 480, Height = 22 });
        var b1 = B("Full song with music  (AI splits it into vocals + beat)", 20, 76, 480, 40, Color.FromArgb(118, 52, 212)); var b2 = B("Vocals only  (acapella: the file has NO music)", 20, 122, 480, 40, Color.FromArgb(150, 50, 160)); var b3 = B("Beat / instrumental", 20, 168, 480, 40, Color.FromArgb(70, 40, 120));
        b1.Click += (s, e) => { result = "song"; f.Close(); }; b2.Click += (s, e) => { result = "vocal"; f.Close(); }; b3.Click += (s, e) => { result = "beat"; f.Close(); };
        f.Controls.AddRange(new Control[] { b1, b2, b3 }); f.Controls.Add(new Label { Text = "Your original file is never changed.", Left = 20, Top = 216, Width = 480, Height = 22, ForeColor = Color.FromArgb(170, 150, 190) });
        f.ShowDialog(owner); return result;
    }

    // A small neon box to type a number into, next to the mouse. Enter or clicking anywhere else confirms it, Esc cancels. (Used by the Bass Lab knobs.)
    public static void QuickValue(Form owner, string label, string def, Action<string> apply, Action closed)
    {
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, BackColor = Color.FromArgb(14, 7, 30), ClientSize = new Size(250, 78), KeyPreview = true };
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea; f.Location = new Point(Math.Max(wa.X, Math.Min(wa.Right - f.Width, Cursor.Position.X - 30)), Math.Max(wa.Y, Math.Min(wa.Bottom - f.Height, Cursor.Position.Y - 20)));
        var lb = new Label { Text = label, Left = 14, Top = 8, Width = 224, Height = 20, ForeColor = Color.FromArgb(200, 170, 255), Font = new Font("Segoe UI", 9.5f), AutoEllipsis = true };
        var tb = new TextBox { Left = 16, Top = 38, Width = 218, Text = def, TextAlign = HorizontalAlignment.Center, Font = new Font("Segoe UI", 11f) }; f.Controls.Add(lb); f.Controls.Add(tb); Win.NeonBox(tb);
        f.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; Win.DrawNeonBox(e.Graphics, new RectangleF(3, 3, f.Width - 7, f.Height - 7), true); };
        bool done = false; Action<bool> finish = ok => { if (done) return; done = true; string t = tb.Text; try { f.Close(); } catch { } if (ok && t != def && apply != null) { try { apply(t); } catch { } } if (closed != null) closed(); };
        tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; finish(true); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; finish(false); } };
        f.Deactivate += (s, e) => finish(true); f.Shown += (s, e) => { tb.Focus(); tb.SelectAll(); }; f.FormClosed += (s, e) => { if (!done) { done = true; if (closed != null) closed(); } };
        f.Show(owner);
    }

    // Taking a project off the list: 0 = cancel, 1 = off the list only, 2 = off the list and the file to the Recycling Bin (kept 30 days)
    public static int RemoveProject(IWin32Window owner, string name, string path, bool fileExists)
    {
        int result = 0; var f = Make("Remove project", 560, 276);
        f.Controls.Add(new Label { Text = "Remove \"" + name + "\" from your projects?", Left = 20, Top = 16, Width = 520, Height = 26, ForeColor = Color.FromArgb(220, 190, 255), Font = new Font("Segoe UI Semibold", 12f), AutoEllipsis = true });
        f.Controls.Add(new Label { Text = "This takes it off the list. The project file stays on your computer unless you tick the box below. A deleted project can be restored from RECYCLING BIN on the projects page for 30 days.", Left = 20, Top = 50, Width = 520, Height = 62, ForeColor = Ink });
        var chk = new DarkCheck { Text = "Also delete the project file (it waits 30 days in the Recycling Bin)", Left = 20, Top = 120, Width = 520, Height = 28, ForeColor = Color.FromArgb(255, 170, 170), Enabled = fileExists }; f.Controls.Add(chk);
        f.Controls.Add(new Label { Text = fileExists ? path : "The file was not found on this computer, so there is nothing to delete.", Left = 20, Top = 154, Width = 520, Height = 36, ForeColor = Color.FromArgb(150, 140, 190), Font = new Font("Segoe UI", 8.5f), AutoEllipsis = true });
        var ok = B("Remove", 330, 208, 100, 34, Color.FromArgb(160, 40, 80)); var cancel = B("Cancel", 440, 208, 100, 34, Color.FromArgb(70, 40, 120));
        ok.Click += (s, e) => { result = chk.Checked ? 2 : 1; f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.Controls.AddRange(new Control[] { ok, cancel }); f.ShowDialog(owner); return result;
    }

    public static string Ask(IWin32Window owner, string prompt, string def)
    {
        string result = null; var f = Make("Studio", 440, 140);
        f.Controls.Add(new Label { Text = prompt, Left = 16, Top = 14, Width = 400, Height = 22 });
        var tb = new TextBox { Left = 16, Top = 42, Width = 404, Text = def, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(tb); f.Controls.Add(tb);
        var ok = B("OK", 230, 86, 90, 32, Color.FromArgb(118, 52, 212)); var cancel = B("Cancel", 330, 86, 90, 32, Color.FromArgb(70, 40, 120));
        ok.Click += (s, e) => { if (tb.Text.Trim().Length > 0) { result = tb.Text.Trim(); f.Close(); } }; cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, cancel });
        f.ShowDialog(owner); return result;
    }

    public static void Report(IWin32Window owner, string title, List<string> lines, Action undo)
    {
        var f = Make(title, 760, 520);
        var tb = new TextBox { Left = 16, Top = 16, Width = 728, Height = 440, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) }; Win.NeonBox(tb);
        var sb = new StringBuilder(); foreach (var l in lines) sb.Append(" - ").Append(l).Append("\r\n\r\n"); tb.Text = sb.ToString(); f.Controls.Add(tb);
        var ok = B("Keep this mix", 590, 468, 154, 36, Color.FromArgb(118, 52, 212)); var un = B("Undo Auto Mix", 424, 468, 154, 36, Color.FromArgb(150, 40, 80));
        ok.Click += (s, e) => f.Close(); un.Click += (s, e) => { undo(); f.Close(); }; f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, un });
        f.ShowDialog(owner);
    }

    public static ExportSettings ExportOptions(IWin32Window owner, string defName)
    {
        ExportSettings result = null; var f = Make("Export", 560, 560);
        var cks = new List<CheckBox>(); string[] names = { "Complete mix (mixed + mastered)", "Complete mix without mastering", "Processed vocals only", "Processed instrumental only", "Dry vocals (no effects)", "Dry instrumental (no effects)", "Individual stems (every track, processed)" };
        bool[] def = { true, false, false, false, false, false, false }; int y = 16;
        f.Controls.Add(new Label { Text = "What to export", Left = 16, Top = y, Width = 300, Height = 22, Font = new Font("Segoe UI Semibold", 10.5f) }); y += 28;
        for (int i = 0; i < names.Length; i++) { var c = new DarkCheck { Text = names[i], Left = 24, Top = y, Width = 500, Height = 26, Checked = def[i] }; cks.Add(c); f.Controls.Add(c); y += 28; }
        y += 6; f.Controls.Add(new Label { Text = "Format", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var fm = new DarkCombo { Left = 90, Top = y, Width = 250 }; fm.Items.AddRange(Exporter.Formats); fm.SelectedIndex = 0; f.Controls.Add(fm); y += 40;
        f.Controls.Add(new Label { Text = "Name", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var nm = new TextBox { Left = 90, Top = y, Width = 440, Text = defName, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(nm); f.Controls.Add(nm); y += 38;
        f.Controls.Add(new Label { Text = "Folder", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var dir = new TextBox { Left = 90, Top = y, Width = 350, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Exports"), BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(dir); f.Controls.Add(dir);
        var br = B("Browse...", 448, y - 2, 84, 28, Color.FromArgb(70, 40, 120)); br.Click += (s, e) => { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) dir.Text = d.SelectedPath; }; f.Controls.Add(br); y += 42;
        f.Controls.Add(new Label { Text = "All files start at 0:00 with the same length, so stems line up in any DAW. Existing files are never replaced without asking.", Left = 16, Top = y, Width = 520, Height = 40, ForeColor = Color.FromArgb(170, 150, 190) });
        var ok = B("Export", 330, 508, 100, 34, Color.FromArgb(118, 52, 212)); var cancel = B("Cancel", 440, 508, 100, 34, Color.FromArgb(70, 40, 120));
        ok.Click += (s, e) =>
        {
            if (nm.Text.Trim().Length == 0 || dir.Text.Trim().Length == 0) return;
            result = new ExportSettings { MixMastered = cks[0].Checked, MixDry = cks[1].Checked, ProcVocal = cks[2].Checked, ProcBeat = cks[3].Checked, DryVocal = cks[4].Checked, DryBeat = cks[5].Checked, Stems = cks[6].Checked, Fmt = fm.SelectedIndex, Dir = dir.Text.Trim(), Prefix = nm.Text.Trim() }; f.Close();
        };
        cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, cancel });
        f.ShowDialog(owner); return result;
    }
}
