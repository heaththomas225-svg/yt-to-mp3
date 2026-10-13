using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Purphex Music window (part 2): actions (import, separate, auto mix, export, save/open),
//  busy overlay, mastering tab, and small dialogs.
// ======================================================================
class ExportSettings { public bool MixMastered = true, MixDry, ProcVocal, ProcBeat, DryVocal, DryBeat, Stems; public List<int> StemTracks;   /* the tracks (by number) picked for separate files; null = every track */ public double TrimStart, TrimEnd; public int Fmt, RateIdx; public string Dir = "", Prefix = ""; }

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
        }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();   // loading never takes the processor from the sound
    }

    void PaintBusy(Graphics g)
    {
        using (var br = new SolidBrush(TM.C(185, 6, 3, 14))) g.FillRectangle(br, 0, 0, ClientSize.Width, ClientSize.Height);
        Reg(Rc(0, 0, ClientSize.Width, ClientSize.Height), () => { });
        float w = 520; foreach (var ln in busyText.Split((char)10)) w = Math.Max(w, g.MeasureString(ln, GfxCache.Font(17, true)).Width + 80); w = Math.Min(w, ClientSize.Width - 30);   // the box grows to fit its text...
        float tpx = 17; { float widest = 0; foreach (var ln in busyText.Split((char)10)) widest = Math.Max(widest, g.MeasureString(ln, GfxCache.Font(17, true)).Width); if (widest > w - 50) tpx = Math.Max(10, 17 * (w - 50) / widest); }   // ...and if the window is too small for that, the text gets smaller
        float h = busyCancelable ? 170 : 130, x = (ClientSize.Width - w) / 2, y = (ClientSize.Height - h) / 2; var box = Rc(x, y, w, h);
        FillRR(g, TM.C(252, 28, 15, 52), box, 14); StrokeRR(g, Accent, box, 14, 1.5f);
        Txt(g, busyText, tpx, Ink, Rc(x + 20, y + 18, w - 40, 52), StringAlignment.Center, true);
        var bar = Rc(x + 30, y + 78, w - 60, 14); FillRR(g, TM.C(220, 8, 4, 20), bar, 7);
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
        AudioClip clip = null; var fillT = fillTrack; fillTrack = null;   // the empty track this file was picked for (or dropped on): it goes into that track
        RunBusy("Loading " + Path.GetFileName(path) + "...", true, p => { clip = AudioClip.FromFile(path, p, () => cancelFlag); }, ok =>
        {
            if (!ok || clip == null) { if (importQueue.Count > 0) { string nx = importQueue.Dequeue(); BeginInvoke(new Action(() => PromptImport(nx))); } return; }
            string nm = role == "vocal" ? "Vocals" : (role == "beat" ? "Instrumental" : clip.Name);
            if (fillT != null && fillT.Clip == null && !fillT.Instrument && !fillT.IsBus && eng.Tracks.Contains(fillT)) { PushUndo(); lock (eng.Sync) { fillT.Clip = clip; fillT.ClipStart = 0; fillT.ClipFrames = -1; fillT.Offset = 0; fillT.Armed = false; if (fillT.Name.StartsWith("Audio ", StringComparison.OrdinalIgnoreCase) || fillT.Name.StartsWith("Beat", StringComparison.OrdinalIgnoreCase)) fillT.Name = nm; } selTrack = eng.Tracks.IndexOf(fillT); Toast("Added " + nm + " to this track"); }
            else { AddTrack(StudioEngine.NewTrack(nm, role, clip)); Toast("Added " + nm); }
            try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { }   // the memory of the decoding is cleaned up now, while nothing is playing, not in the middle of the next play
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
        pc.AutoKeyPending = false; if (tr == null || tr.Clip == null) return; int key = 0, scale = 1; double conf = 0; bool found = false;
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
        var s = StudioDialogs.ExportOptions(this, projName == "Untitled project" ? "My song" : projName, (eng.EndFrame + StudioEngine.SR * 2) / (double)StudioEngine.SR, eng.Tracks.ConvertAll(t => t.Name)); if (s == null) return;
        HashSet<Track> only = null; if (s.Stems && s.StemTracks != null) { only = new HashSet<Track>(); foreach (int ti in s.StemTracks) if (ti >= 0 && ti < eng.Tracks.Count) only.Add(eng.Tracks[ti]); }
        var plan = Exporter.Plan(eng, s.MixMastered, s.MixDry, s.ProcVocal, s.ProcBeat, false, false, s.Stems, s.Prefix, s.Fmt, only);
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
                done.Add(Exporter.Write(eng, plan[i], s.Dir, s.Fmt, p => prog((ii + p) / n), () => cancelFlag, Exporter.RateFor(s.Fmt, s.RateIdx), s.TrimStart, s.TrimEnd));
            }
        }, ok =>
        {
            if (!ok) return; Toast("Exported " + done.Count + " file(s)"); try { System.Diagnostics.Process.Start("explorer.exe", "\"" + s.Dir + "\""); } catch { }
        });
    }

    internal bool HasProject { get { return eng.Tracks.Count > 0; } }
    // SAVE for the question asked when the program is closed: true when the project is saved (a cancelled Save dialog keeps the program open)
    internal bool SaveBeforeClose() { return QuickSaveNamed(); }
    void SaveClick(bool asNew)
    {
        if (eng.Tracks.Count == 0) { Toast("Nothing to save yet."); return; }
        FlexCommitIfOpen();
        string path = projPath;
        if (!asNew && !string.IsNullOrEmpty(path) && !AskYes("SAVING WILL OVERWRITE THE CURRENT PROJECT.\n\n\"" + projName + "\" will be replaced by what you have now. (SAVE AS keeps the old one and makes a new project.)\n\nConfirm?", "Save project")) return;
        if (asNew || string.IsNullOrEmpty(path))
        {
            using (var d = new SaveFileDialog())
            {
                d.Title = "Save project"; d.Filter = "Studio project (*.ytmproj)|*.ytmproj"; d.FileName = projName == "Untitled project" ? "My song" : projName;
                d.InitialDirectory = Win.DownloadsDir;
                try { Directory.CreateDirectory(d.InitialDirectory); } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return; path = d.FileName;
            }
        }
        try { ProjectIO.Save(eng, path); projPath = path; projName = Path.GetFileNameWithoutExtension(path); NoteProject(path, true); Toast("Project saved"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed"); }
    }

    const string AudioExts = "*.mp3;*.wav;*.flac;*.aiff;*.aif;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.mp4;*.mkv;*.webm";
    // false when nothing was chosen (the window was cancelled, or the open was declined)
    internal Func<string[]> openPickHook;   // a test answers for itself (null = the window was cancelled)
    bool OpenClick()
    {
        string[] picked;
        if (openPickHook != null) { picked = openPickHook(); if (picked == null || picked.Length == 0) return false; }
        else using (var d = new OpenFileDialog()) { d.Title = "Open a project, or add music (mp3, wav, flac...)"; d.Multiselect = true; d.Filter = "Projects and audio|*.ytmproj;" + AudioExts + "|Studio project (*.ytmproj)|*.ytmproj|Audio files|" + AudioExts + "|All files|*.*"; if (d.ShowDialog(this) != DialogResult.OK) return false; picked = d.FileNames; }
        foreach (var f in picked) if (f.EndsWith(".ytmproj", StringComparison.OrdinalIgnoreCase)) { if (eng.Tracks.Count > 0 && !AskYes(OverwriteText(Path.GetFileNameWithoutExtension(f)), "Open project")) return false; OpenProjectPath(f); return true; }
        foreach (var f in picked) PromptImport(f);   // audio files are added as tracks
        return true;
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
    internal string AskBlank(string prompt, string def) { return StudioDialogs.Ask(this, prompt, def, true); }

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
    // the folder of the last export (kept in a small file): the Export box opens with it again; a folder that is gone falls back to Downloads
    static string ExportDirFile { get { return Path.Combine(StudioEnv.DataDir, "export-folder.txt"); } }
    internal static string LastExportDir
    {
        get { try { if (File.Exists(ExportDirFile)) { string t = File.ReadAllText(ExportDirFile).Trim(); if (t.Length > 0 && Directory.Exists(t)) return t; } } catch { } return Path.Combine(Win.DownloadsDir, "Purphex Music"); }
        set { try { if (value.Length > 0) { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllText(ExportDirFile, value); } } catch { } }
    }
    static Color Bg { get { return TM.C(255, 24, 12, 44); } } static Color Fld { get { return TM.C(255, 34, 16, 62); } } static Color Ink { get { return TM.C(255, 240, 232, 255); } }

    internal static Form Make(string title, int w, int h)
    {
        var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Bg, ForeColor = Ink, ClientSize = new Size(w, h), Font = new Font("Segoe UI", 10f) };
        Win.Dark(f); f.Shown += (s, e) => Win.CapsAll(f); return f;
    }
    internal static Button B(string text, int x, int y, int w, int h, Color c) { var b = new PxButton { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); return b; }

    // ADD MUSIC in the main window: what do you want to do with a song? 0 = cancelled, 1 = edit in the studio, 2 = vocal remover, 3 = trim
    public static int AddMusicChoice(IWin32Window owner)
    {
        int result = 0; var f = Make("Add music", 520, 292);
        f.Controls.Add(new Label { Text = "What do you want to do with your music?", Left = 20, Top = 16, Width = 480, Height = 24, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 11f) });
        var b1 = B("Edit it in Purphex Music  (tracks, mixing, effects, mastering)", 20, 52, 480, 44, TM.C(255, 118, 52, 212)); var b2 = B("Vocal remover  (take the vocals or the beat out of a song)", 20, 104, 480, 44, TM.C(255, 160, 56, 170)); var b3 = B("Trim start and end  (cut it and save as MP3)", 20, 156, 480, 44, TM.C(255, 70, 44, 140));
        if (!Edition.Has(Tier.Supporter)) b1.Text += "   (Supporter Edition)";
        b1.Click += (s, e) => { result = 1; f.Close(); }; b2.Click += (s, e) => { result = 2; f.Close(); }; b3.Click += (s, e) => { result = 3; f.Close(); };
        f.Controls.AddRange(new Control[] { b1, b2, b3 }); f.Controls.Add(new Label { Text = "Your original file is never changed.", Left = 20, Top = 214, Width = 480, Height = 22, ForeColor = TM.C(255, 170, 150, 200) });
        var cancel = B("Cancel", 400, 246, 100, 30, TM.C(255, 60, 30, 90)); cancel.Click += (s, e) => f.Close(); f.Controls.Add(cancel);
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
        var f = Make("Trim start and end", 540, 300); var tb = new TextBox { Left = 20, Top = 44, Width = 400, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(tb); var browse = B("Browse...", 430, 42, 90, 28, TM.C(255, 70, 44, 140));
        f.Controls.Add(new Label { Text = "File to trim", Left = 20, Top = 20, Width = 300, Height = 22 }); f.Controls.AddRange(new Control[] { tb, browse });
        var a = new TextBox { Left = 20, Top = 106, Width = 110, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(a); var b = new TextBox { Left = 150, Top = 106, Width = 110, Height = 26, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(b);
        f.Controls.Add(new Label { Text = "Trim start", Left = 20, Top = 82, Width = 130, Height = 22 }); f.Controls.Add(new Label { Text = "Trim end", Left = 150, Top = 82, Width = 130, Height = 22 }); f.Controls.AddRange(new Control[] { a, b });
        var fmt = new ComboBox { Left = 290, Top = 106, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Fld, ForeColor = Ink, FlatStyle = FlatStyle.Flat }; fmt.Items.AddRange(new object[] { "MP3 320 kbps", "WAV 24-bit" }); fmt.SelectedIndex = 0;
        f.Controls.Add(new Label { Text = "Save as", Left = 290, Top = 82, Width = 130, Height = 22 }); f.Controls.Add(fmt);
        var note = new Label { Text = "Leave the start empty to begin at 0:00 and the end empty to keep everything to the end. Saved in: " + outDir, Left = 20, Top = 146, Width = 500, Height = 44, ForeColor = TM.C(255, 170, 150, 200) }; f.Controls.Add(note);
        var go = B("Trim and save", 20, 214, 250, 44, TM.C(255, 118, 52, 212)); var close = B("Close", 420, 214, 100, 44, TM.C(255, 60, 30, 90)); close.Click += (s, e) => f.Close(); f.Controls.AddRange(new Control[] { go, close });
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
        f.Controls.Add(new Label { Text = fileName, Left = 20, Top = 16, Width = 480, Height = 24, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 10.5f) });
        f.Controls.Add(new Label { Text = "Pick what is IN this file. Only the first option removes music; the others just label it.", Left = 20, Top = 44, Width = 480, Height = 22 });
        var b1 = B("Full song with music  (AI splits it into vocals + beat)", 20, 76, 480, 40, TM.C(255, 118, 52, 212)); var b2 = B("Vocals only  (acapella: the file has NO music)", 20, 122, 480, 40, TM.C(255, 150, 50, 160)); var b3 = B("Beat / instrumental", 20, 168, 480, 40, TM.C(255, 70, 40, 120));
        b1.Click += (s, e) => { result = "song"; f.Close(); }; b2.Click += (s, e) => { result = "vocal"; f.Close(); }; b3.Click += (s, e) => { result = "beat"; f.Close(); };
        f.Controls.AddRange(new Control[] { b1, b2, b3 }); f.Controls.Add(new Label { Text = "Your original file is never changed.", Left = 20, Top = 216, Width = 480, Height = 22, ForeColor = TM.C(255, 170, 150, 190) });
        f.ShowDialog(owner); return result;
    }

    // A small neon box to type a number into, next to the mouse. Enter or clicking anywhere else confirms it, Esc cancels. (Used by the Bass Lab knobs.)
    public static void QuickValue(Form owner, string label, string def, Action<string> apply, Action closed)
    {
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, BackColor = TM.C(255, 14, 7, 30), ClientSize = new Size(250, 78), KeyPreview = true };
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea; f.Location = new Point(Math.Max(wa.X, Math.Min(wa.Right - f.Width, Cursor.Position.X - 30)), Math.Max(wa.Y, Math.Min(wa.Bottom - f.Height, Cursor.Position.Y - 20)));
        var lb = new Label { Text = label, Left = 14, Top = 8, Width = 224, Height = 20, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI", 9.5f), AutoEllipsis = true };
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
        f.Controls.Add(new Label { Text = "Remove \"" + name + "\" from your projects?", Left = 20, Top = 16, Width = 520, Height = 26, ForeColor = TM.C(255, 220, 190, 255), Font = new Font("Segoe UI Semibold", 12f), AutoEllipsis = true });
        f.Controls.Add(new Label { Text = "This takes it off the list. The project file stays on your computer unless you tick the box below. A deleted project can be restored from RECYCLING BIN on the projects page for 30 days.", Left = 20, Top = 50, Width = 520, Height = 62, ForeColor = Ink });
        var chk = new DarkCheck { Text = "Also delete the project file (it waits 30 days in the Recycling Bin)", Left = 20, Top = 120, Width = 520, Height = 28, ForeColor = Color.FromArgb(255, 170, 170), Enabled = fileExists }; f.Controls.Add(chk);
        f.Controls.Add(new Label { Text = fileExists ? path : "The file was not found on this computer, so there is nothing to delete.", Left = 20, Top = 154, Width = 520, Height = 36, ForeColor = TM.C(255, 150, 140, 190), Font = new Font("Segoe UI", 8.5f), AutoEllipsis = true });
        var ok = B("Remove", 330, 208, 100, 34, TM.C(255, 160, 40, 80)); var cancel = B("Cancel", 440, 208, 100, 34, TM.C(255, 70, 40, 120));
        ok.Click += (s, e) => { result = chk.Checked ? 2 : 1; f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.Controls.AddRange(new Control[] { ok, cancel }); f.ShowDialog(owner); return result;
    }

    // a question with a long answer (a bio, a description): the box grows with every line that is written, so all of it is in view and easy to edit
    public static string AskMulti(IWin32Window owner, string prompt, string def)
    {
        string result = null; var f = Make("Studio", 560, 220); var pl = new Label { Text = prompt, Left = 16, Top = 14, Width = 528, AutoSize = false, Height = 24, ForeColor = TM.C(255, 200, 170, 255) }; f.Controls.Add(pl);
        var tb = new TextBox { Left = 16, Top = 44, Width = 528, Height = 44, Multiline = true, WordWrap = true, AcceptsReturn = true, MaxLength = 20000, Text = def ?? "", BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11f) }; Win.NeonBox(tb); f.Controls.Add(tb); NeonScroll.Attach(tb);
        var ok = B("OK", 340, 0, 90, 32, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 440, 0, 90, 32, TM.C(255, 70, 40, 120)); var emo = B("EMOJIS", 16, 0, 110, 32, TM.C(255, 150, 70, 235)); f.Controls.AddRange(new Control[] { ok, cancel, emo });
        Action relayout = () =>
        {
            int lines = 1; try { lines = Math.Max(1, tb.GetLineFromCharIndex(Math.Max(0, tb.TextLength)) + 1); } catch { }
            int maxH = (int)(Screen.FromControl(f).WorkingArea.Height * 0.62), want = Math.Max(52, Math.Min(maxH, lines * (tb.Font.Height + 1) + 14)); if (tb.Height != want) tb.Height = want;
            int cy = tb.Bottom + 14; ok.Top = cancel.Top = emo.Top = cy; int ch = cy + 32 + 16; if (f.ClientSize.Height != ch) { f.ClientSize = new Size(560, ch); }
        };
        tb.TextChanged += (s, e) => relayout(); f.Shown += (s, e) => { relayout(); tb.Focus(); tb.SelectionStart = tb.TextLength; }; relayout();
        emo.Click += (s, e) => EmojiPanel.Toggle(f, em => { int at = tb.SelectionStart; tb.Text = tb.Text.Substring(0, at) + em + tb.Text.Substring(Math.Min(tb.Text.Length, at + tb.SelectionLength)); tb.SelectionStart = at + em.Length; tb.Focus(); });
        ok.Click += (s, e) => { result = tb.Text.Trim(); f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.ShowDialog(owner); return result;
    }
    public static string Ask(IWin32Window owner, string prompt, string def) { return Ask(owner, prompt, def, false); }
    // allowBlank: OK with nothing typed gives "" (it is how a box is emptied, for example no nickname); Cancel still gives null
    public static string Ask(IWin32Window owner, string prompt, string def, bool allowBlank)
    {
        string result = null; var f = Make("Studio", 440, 140);
        var pl = new Label { Text = prompt, Left = 16, Top = 14, Width = 404, AutoSize = false, Height = 22 };   // the question gets as many lines as it needs (it used to be cut off)
        using (var gm = f.CreateGraphics()) { var sz = gm.MeasureString(prompt, f.Font, 404); pl.Height = (int)Math.Ceiling(sz.Height) + 6; }
        f.Controls.Add(pl); int dy = pl.Height - 22; f.ClientSize = new Size(440, 140 + dy);
        var tb = new TextBox { Left = 16, Top = 42 + dy, Width = 404, Text = def, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(tb); f.Controls.Add(tb);
        var ok = B("OK", 230, 86 + dy, 90, 32, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 330, 86 + dy, 90, 32, TM.C(255, 70, 40, 120));
        var emo = B("EMOJIS", 16, 86 + dy, 110, 32, TM.C(255, 150, 70, 235)); emo.Click += (s, e) => EmojiPanel.Toggle(f, em => { int at = tb.SelectionStart; tb.Text = tb.Text.Substring(0, at) + em + tb.Text.Substring(Math.Min(tb.Text.Length, at + tb.SelectionLength)); tb.SelectionStart = at + em.Length; tb.Focus(); }); f.Controls.Add(emo);   // anywhere you write a name, a bio or a description
        ok.Click += (s, e) => { if (allowBlank || tb.Text.Trim().Length > 0) { result = tb.Text.Trim(); f.Close(); } }; cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, cancel });
        f.ShowDialog(owner); return result;
    }

    // Genre tags: tick the ones you like and add your own (as many as you like, no swearing). Returns the tags (spaces between them) or null if cancelled.
    static readonly string[] SuggestedTags = { "dnb", "trap", "drill", "hiphop", "rap", "lofi", "house", "techno", "edm", "dubstep", "phonk", "pop", "rock", "metal", "rnb", "afrobeat", "reggaeton", "kpop", "indie", "ambient", "cinematic", "jazz", "soul", "funk", "country", "bassline", "dark", "chill", "hard", "melodic", "vocal", "instrumental", "remix", "demo", "banger", "sad" };
    public static string[] SuggestedTagList { get { return SuggestedTags; } }
    public static string Tags(IWin32Window owner, string current)
    {
        string result = null; var f = Make("Genre tags", 600, 470); var sel = new List<string>(); foreach (var t in ProjectLibrary.CleanTags(current).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) sel.Add(t);
        f.Controls.Add(new Label { Text = "Pick the tags that fit this project, or add your own.", Left = 16, Top = 12, Width = 560, Height = 22, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 11f) });
        var count = new Label { Left = 16, Top = 36, Width = 560, Height = 20, ForeColor = TM.C(255, 160, 140, 200) }; f.Controls.Add(count);
        var panel = new Panel { Left = 16, Top = 62, Width = 568, Height = 276, AutoScroll = false, BackColor = TM.C(255, 14, 7, 30) }; f.Controls.Add(panel);
        var all = new List<string>(SuggestedTags); foreach (var t in sel) if (!all.Contains(t)) all.Add(t);
        var btns = new Dictionary<string, Button>(); Action refresh = null; FlowLayoutPanel fp = null;
        Action<string> addChip = null; addChip = tag =>
        {
            if (btns.ContainsKey(tag)) return; var b = new PxButton { Text = tag.ToUpper(), AutoSize = true, MinimumSize = new Size(0, 30), Padding = new Padding(8, 0, 8, 0), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, ForeColor = Color.White, Margin = new Padding(4) }; b.FlatAppearance.BorderColor = TM.C(255, 150, 110, 220);
            string tg = tag; b.Click += (s, e) => { if (sel.Contains(tg)) sel.Remove(tg); else sel.Add(tg); refresh(); }; btns[tag] = b; fp.Controls.Add(b);
        };
        fp = new FlowLayoutPanel { Left = 0, Top = 0, Width = 568 - NeonBar.BarW - 8, MaximumSize = new Size(568 - NeonBar.BarW - 8, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, AutoScroll = false, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = TM.C(255, 14, 7, 30), Padding = new Padding(4) }; panel.Controls.Add(fp); new NeonPanelScroll(panel, fp);   // the tags scroll with the neon bar
        foreach (var t in all) addChip(t);
        refresh = () => { foreach (var kv in btns) { bool on = sel.Contains(kv.Key); kv.Value.BackColor = on ? TM.C(255, 150, 70, 230) : TM.C(255, 46, 26, 84); } count.Text = sel.Count + (sel.Count == 1 ? " tag picked" : " tags picked") + " (as many as you like)"; };
        var own = new TextBox { Left = 16, Top = 350, Width = 270, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, MaxLength = 20 }; Win.NeonBox(own); f.Controls.Add(own);
        var add = B("ADD MY OWN", 296, 346, 130, 32, TM.C(255, 140, 60, 220)); f.Controls.Add(add); var rmOwn = B("REMOVE MY OWN", 434, 346, 150, 32, TM.C(255, 150, 40, 80)); f.Controls.Add(rmOwn); Label hintLbl = null;
        Action doAdd = () => { string raw = own.Text.Trim(); string t = ProjectLibrary.CleanTags(raw).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(); if (t == null) { if (raw.Length > 0 && hintLbl != null) { hintLbl.Text = "That word is not allowed: no swearing, please."; hintLbl.ForeColor = TM.C(255, 255, 140, 140); } return; } if (hintLbl != null) { hintLbl.Text = "Type a word (letters and numbers, up to 20) and press ADD MY OWN or Enter."; hintLbl.ForeColor = TM.C(255, 150, 140, 190); } addChip(t); if (!sel.Contains(t)) sel.Add(t); own.Text = ""; refresh(); own.Focus(); };
        rmOwn.Click += (s, e) => { foreach (var k in btns.Keys.ToList()) if (Array.IndexOf(SuggestedTags, k) < 0) { var bb = btns[k]; fp.Controls.Remove(bb); bb.Dispose(); btns.Remove(k); sel.Remove(k); } refresh(); };
        add.Click += (s, e) => doAdd(); own.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; doAdd(); } };
        hintLbl = new Label { Text = "Type a word (letters and numbers, up to 20) and press ADD MY OWN or Enter.", Left = 16, Top = 384, Width = 560, Height = 20, ForeColor = TM.C(255, 150, 140, 190) }; f.Controls.Add(hintLbl);
        var ok = B("SAVE TAGS", 360, 418, 120, 36, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 490, 418, 94, 36, TM.C(255, 70, 40, 120));
        ok.Click += (s, e) => { result = string.Join(" ", sel.ToArray()); f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.Controls.AddRange(new Control[] { ok, cancel }); refresh(); f.ShowDialog(owner); return result;
    }

    // "this artist name is taken": the same name written in other ways to pick from, or type your own
    public static string NameTaken(IWin32Window owner, string name, List<string> suggestions)
    {
        string result = null; var f = Make("Artist name taken", 520, 420);
        f.Controls.Add(new Label { Text = "The artist name \"" + name + "\" is already taken by another artist.\n\nPick it written another way, or type your own name:", Left = 16, Top = 12, Width = 488, Height = 66, ForeColor = TM.C(255, 255, 200, 170), Font = new Font("Segoe UI Semibold", 10.5f) });
        var list = new ListBox { Left = 16, Top = 84, Width = 488, Height = 170, BackColor = TM.C(255, 14, 7, 30), ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12f), ItemHeight = 28, DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false }; Win.NeonBox(list); Win.DarkControl(list); f.Controls.Add(list);
        foreach (var sx in suggestions) list.Items.Add(sx); if (suggestions.Count == 0) list.Items.Add("(no suggestions: type your own name below)");
        list.DrawItem += (s, e) => { if (e.Index < 0) return; bool on = (e.State & DrawItemState.Selected) != 0; using (var br = new SolidBrush(on ? TM.C(255, 118, 52, 212) : TM.C(255, 14, 7, 30))) e.Graphics.FillRectangle(br, e.Bounds); TextRenderer.DrawText(e.Graphics, Convert.ToString(list.Items[e.Index]), list.Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), on ? Color.White : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix); };
        var tb = new TextBox { Left = 16, Top = 268, Width = 488, MaxLength = 30, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12f), Text = suggestions.Count > 0 ? suggestions[0] : name }; Win.NeonBox(tb); f.Controls.Add(tb);
        f.Controls.Add(new Label { Text = "Names count as the same when only the capitals, spaces, dots or dashes differ.", Left = 16, Top = 304, Width = 488, Height = 22, ForeColor = TM.C(255, 160, 140, 190) });
        list.SelectedIndexChanged += (s, e) => { if (list.SelectedIndex >= 0 && list.SelectedIndex < suggestions.Count) tb.Text = suggestions[list.SelectedIndex]; };
        var ok = B("USE THIS NAME", 300, 356, 130, 36, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 440, 356, 64, 36, TM.C(255, 70, 40, 120));
        ok.Click += (s, e) => { if (tb.Text.Trim().Length >= 2) { result = tb.Text.Trim(); f.Close(); } }; cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.CancelButton = cancel; f.Controls.AddRange(new Control[] { ok, cancel }); f.ShowDialog(owner); return result;
    }

    // links: a box for the TITLE and a box for the ADDRESS on every row (up to 10). Only streaming services and social media are allowed; a row is ready when it has both, and the window does not save until every row is right.
    // returns "Title | https://address" lines (what the profile keeps), or null when cancelled
    public static string Links(IWin32Window owner, string current)
    {
        string result = null; var f = Make("Your links", 680, 612);
        f.Controls.Add(new Label { Text = "Add up to 10 links: give each a TITLE and paste its ADDRESS. Only streaming services and social media work (YouTube, TikTok, Instagram, Snapchat, Spotify, Apple Music, SoundCloud, Bandcamp, X, Facebook, Twitch...). Any other website is refused.", Left = 16, Top = 12, Width = 648, Height = 62, ForeColor = TM.C(255, 255, 200, 170), Font = new Font("Segoe UI Semibold", 9.5f) });
        f.Controls.Add(new Label { Text = "TITLE  (what people read)", Left = 16, Top = 80, Width = 200, Height = 18, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 9f) });
        f.Controls.Add(new Label { Text = "ADDRESS  (paste the link)", Left = 226, Top = 80, Width = 330, Height = 18, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 9f) });
        const int N = 10; var tt = new TextBox[N]; var uu = new TextBox[N]; var st = new Label[N];
        for (int i = 0; i < N; i++)
        {
            int top = 102 + i * 39;
            tt[i] = new TextBox { Left = 16, Top = top, Width = 200, MaxLength = 30, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f) }; Win.NeonBox(tt[i]); f.Controls.Add(tt[i]);
            uu[i] = new TextBox { Left = 226, Top = top, Width = 330, MaxLength = 300, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f) }; Win.NeonBox(uu[i]); f.Controls.Add(uu[i]);
            st[i] = new Label { Left = 566, Top = top + 3, Width = 98, Height = 22, ForeColor = TM.C(255, 160, 140, 190), Font = new Font("Segoe UI Semibold", 8.5f), AutoEllipsis = true }; f.Controls.Add(st[i]);
        }
        { int k = 0; foreach (var raw in (current ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) { if (k >= N) break; string ti, ur; if (ProfileStore.ParseLinkLine(raw, out ti, out ur)) { tt[k].Text = ti; uu[k].Text = ur; k++; } else if (raw.Trim().Length > 0) { int bar = raw.IndexOf('|'); tt[k].Text = bar > 0 ? ProfileStore.CleanTitle(raw.Substring(0, bar)) : ""; uu[k].Text = (bar >= 0 ? raw.Substring(bar + 1) : raw).Trim(); k++; } } }
        var info = new Label { Left = 16, Top = 102 + N * 39 + 4, Width = 648, Height = 40, ForeColor = TM.C(255, 160, 140, 200) }; f.Controls.Add(info); bool allGood = true; var lines = new List<string>();
        Action check = () =>
        {
            int ok = 0, bad = 0; lines.Clear(); var seen = new HashSet<string>();
            for (int i = 0; i < N; i++)
            {
                string t = ProfileStore.CleanTitle(tt[i].Text), raw = uu[i].Text.Trim(); if (t.Length == 0 && raw.Length == 0) { st[i].Text = ""; continue; }
                string cu = raw.Length == 0 ? "" : ProfileStore.CleanLink(raw);
                if (raw.Length == 0) { st[i].Text = "address needed"; st[i].ForeColor = TM.C(255, 255, 150, 150); bad++; }
                else if (cu.Length == 0) { st[i].Text = "not allowed"; st[i].ForeColor = TM.C(255, 255, 150, 150); bad++; }
                else if (t.Length == 0) { st[i].Text = "title needed"; st[i].ForeColor = TM.C(255, 255, 150, 150); bad++; }
                else if (!seen.Add(cu)) { st[i].Text = "same link twice"; st[i].ForeColor = TM.C(255, 255, 150, 150); bad++; }
                else { string host = ""; try { host = new Uri(cu).IdnHost; } catch { } string sv = ProfileStore.ServiceOf(host); st[i].Text = "OK  " + sv; st[i].ForeColor = TM.C(255, 150, 240, 180); ok++; lines.Add(t + " | " + cu); }
            }
            allGood = bad == 0; info.ForeColor = allGood ? TM.C(255, 160, 220, 190) : TM.C(255, 255, 150, 150);
            info.Text = allGood ? (ok == 0 ? "No links yet: type a title and paste an address." : ok + " link" + (ok == 1 ? "" : "s") + " ready: press SAVE LINKS.") : bad + " row" + (bad == 1 ? "" : "s") + " to fix: every link needs a title and an address from a streaming service or social media.";
        };
        foreach (var b in tt) b.TextChanged += (s, e) => check(); foreach (var b in uu) b.TextChanged += (s, e) => check(); check();
        var ok2 = B("SAVE LINKS", 440, 102 + N * 39 + 50, 120, 36, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 570, 102 + N * 39 + 50, 94, 36, TM.C(255, 70, 40, 120));
        ok2.Click += (s, e) => { check(); if (!allGood) return; result = string.Join("\r\n", lines.ToArray()); f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.Controls.AddRange(new Control[] { ok2, cancel }); f.ShowDialog(owner); return result;
    }

    public static void Report(IWin32Window owner, string title, List<string> lines, Action undo)
    {
        var f = Make(title, 760, 520);
        var tb = new TextBox { Left = 16, Top = 16, Width = 728, Height = 440, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) }; Win.NeonBox(tb); NeonScroll.Attach(tb);
        var sb = new StringBuilder(); foreach (var l in lines) sb.Append(" - ").Append(l).Append("\r\n\r\n"); tb.Text = sb.ToString(); f.Controls.Add(tb);
        var ok = B("Keep this mix", 590, 468, 154, 36, TM.C(255, 118, 52, 212)); var un = B("Undo Auto Mix", 424, 468, 154, 36, TM.C(255, 150, 40, 80));
        ok.Click += (s, e) => f.Close(); un.Click += (s, e) => { undo(); f.Close(); }; f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, un });
        f.ShowDialog(owner);
    }

        // "1:23", "83" or "83.5" -> seconds
    public static bool ParseTime(string t, out double sec)
    {
        sec = 0; t = (t ?? "").Trim().Replace(',', '.'); if (t.Length == 0) return false; double total = 0; var parts = t.Split(':'); if (parts.Length > 3) return false;
        foreach (var p in parts) { double v; if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < 0) return false; total = total * 60 + v; }
        sec = total; return true;
    }
    static string TimeText(double s) { int m = (int)(s / 60); double r = s - m * 60; return m + ":" + (r < 10 ? "0" : "") + r.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }
    // plain-words help for the Format and Rate boxes of the Export window
    internal static string FormatHint(int i)
    {
        switch (i)
        {
            case 0: return "WAV 24-bit: studio quality, nothing is thrown away. The best choice to keep, to mix or master somewhere else, or to send to a producer. Big files (about 16 MB a minute).";
            case 1: return "WAV 16-bit: CD quality, nothing compressed. Plays everywhere and the files are a little smaller (about 10 MB a minute). Good for sharing, but not the best to keep: 24-bit has more room.";
            case 2: return "WAV 32-bit float: the HIGHEST QUALITY. Nothing is ever clipped or cut off, even if it is louder than the limit. Made for editing in another program. The biggest files (about 21 MB a minute).";
            case 3: return "FLAC 24-bit: the same quality as WAV 24-bit at about half the size, and nothing is lost. A great way to keep your best version (a few players and phones cannot open it).";
            default: return "MP3 320 kbps: the smallest file (about 2.4 MB a minute) and it plays on everything: phones, cars, social media. A little quality is thrown away, so use it to share, not to keep or to edit more.";
        }
    }
    internal static string RateHint(int r, int fmt)
    {
        if (fmt == 4) return "Rate: MP3 only goes up to 48 kHz, so 44.1 kHz (or 48 kHz) is what you get.";
        if (r <= 1) return "Rate: " + (r == 0 ? "44.1 kHz is CD quality and how your project is made: the right choice for music." : "48 kHz is the standard for video: pick it if the song goes into a video.");
        return "Rate: higher rates make much bigger files and only matter if you will keep processing the sound. For listening, 44.1 or 48 kHz sounds the same.";
    }
    public static ExportSettings ExportOptions(IWin32Window owner, string defName, double songLen = 0, IList<string> trackNames = null)
    {
        bool side = trackNames != null && trackNames.Count > 0; ExportSettings result = null; var f = Make("Export", side ? 850 : 560, 640);
        var cks = new List<CheckBox>(); string[] names = { "Complete mix (mixed + mastered)", "Complete mix without mastering", "Processed vocals only", "Processed instrumental only", "Individual stems (every track, processed)" };
        bool[] def = { true, false, false, false, false }; int y = 16;
        f.Controls.Add(new Label { Text = "What to export", Left = 16, Top = y, Width = 300, Height = 22, Font = new Font("Segoe UI Semibold", 10.5f) }); y += 28;
        for (int i = 0; i < names.Length; i++) { var c = new DarkCheck { Text = names[i], Left = 24, Top = y, Width = 500, Height = 26, Checked = def[i] }; cks.Add(c); f.Controls.Add(c); y += 28; }
        // only ONE of the boxes can be ticked at a time: ticking one un-ticks the others, and the last one cannot be un-ticked
        { bool settling = false; for (int ci = 0; ci < cks.Count; ci++) { int me = ci; cks[me].CheckedChanged += (s0, e0) => { if (settling) return; settling = true; if (cks[me].Checked) { for (int cj = 0; cj < cks.Count; cj++) if (cj != me) cks[cj].Checked = false; } else { bool any = false; foreach (var cc in cks) if (cc.Checked) any = true; if (!any) cks[me].Checked = true; } settling = false; }; } }
        // the tracks of this project, to pick the ones that are exported as separate files (the tab on the right is live when INDIVIDUAL STEMS is ticked)
        var stemCk = new List<DarkCheck>(); Panel stemBox = null;
        if (side)
        {
            var hd = new Label { Text = "Tracks to export on their own", Left = 580, Top = 16, Width = 256, Height = 22, Font = new Font("Segoe UI Semibold", 10.5f) }; f.Controls.Add(hd);
            int bh = Math.Min(340, trackNames.Count * 28 + 10);
            stemBox = new Panel { Left = 580, Top = 44, Width = 256, Height = bh, BackColor = TM.C(255, 22, 11, 44), BorderStyle = BorderStyle.FixedSingle, AutoScroll = true }; f.Controls.Add(stemBox);
            for (int i = 0; i < trackNames.Count; i++) { var c = new DarkCheck { Text = (i + 1) + "   " + trackNames[i], Left = 6, Top = 5 + i * 28, Width = 226, Height = 26, Checked = true }; stemCk.Add(c); stemBox.Controls.Add(c); }
            var all = B("All", 580, 44 + bh + 8, 70, 28, TM.C(255, 70, 40, 120)); var none = B("None", 658, 44 + bh + 8, 70, 28, TM.C(255, 70, 40, 120));
            all.Click += (s0, e0) => { foreach (var c in stemCk) c.Checked = true; }; none.Click += (s0, e0) => { foreach (var c in stemCk) c.Checked = false; }; f.Controls.Add(all); f.Controls.Add(none);
            f.Controls.Add(new Label { Text = "Tick INDIVIDUAL STEMS, then pick the tracks you want as separate files. Each one is saved on its own, with everything that is on it.", Left = 580, Top = 44 + bh + 42, Width = 256, Height = 70, ForeColor = TM.C(255, 170, 150, 190) });
            Action live = () => { stemBox.Enabled = cks[4].Checked; all.Enabled = none.Enabled = cks[4].Checked; hd.ForeColor = cks[4].Checked ? TM.C(255, 240, 232, 255) : TM.C(255, 140, 125, 165); }; cks[4].CheckedChanged += (s0, e0) => live(); live();
        }
        y += 4; f.Controls.Add(new Label { Text = "Trim", Left = 16, Top = y + 4, Width = 40, Height = 22 });
        f.Controls.Add(new Label { Text = "start", Left = 58, Top = y + 4, Width = 52, Height = 22 }); var ta = new TextBox { Left = 112, Top = y, Width = 80, Text = "0:00", BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(ta); f.Controls.Add(ta);
        f.Controls.Add(new Label { Text = "end", Left = 206, Top = y + 4, Width = 36, Height = 22 }); var tb = new TextBox { Left = 244, Top = y, Width = 80, Text = TimeText(songLen), BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(tb); f.Controls.Add(tb);
        f.Controls.Add(new Label { Text = "to export only a part", Left = 336, Top = y + 4, Width = 206, Height = 22, ForeColor = TM.C(255, 170, 150, 190) }); y += 40;
        y += 2; f.Controls.Add(new Label { Text = "Format", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var fm = new DarkCombo { Left = 90, Top = y, Width = 190 }; fm.Items.AddRange(Exporter.Formats); fm.Items[2] = Exporter.Formats[2] + "  (best)"; fm.Items[4] = Exporter.Formats[4] + "  (smallest)"; fm.SelectedIndex = 0; f.Controls.Add(fm);
        f.Controls.Add(new Label { Text = "Rate", Left = 292, Top = y + 4, Width = 92, Height = 22 }); var rt = new DarkCombo { Left = 388, Top = y, Width = 146 }; rt.Items.AddRange(Exporter.RateNames); for (int ri = 0; ri < rt.Items.Count && ri < Exporter.Rates.Length; ri++) if (Exporter.Rates[ri] > Edition.MaxRate) rt.Items[ri] = Exporter.RateNames[ri] + "  (Fantasy)"; rt.SelectedIndex = 0; int rtLast = 0; rt.SelectedIndexChanged += (ra, rb) => { int ri = rt.SelectedIndex; if (ri < 0) return; if (ri < Exporter.Rates.Length && Exporter.Rates[ri] > Edition.MaxRate) { Edition.Upsell(Tier.Fantasy, "Exporting at " + Exporter.RateNames[ri], f); rt.SelectedIndex = rtLast; } else rtLast = ri; }; f.Controls.Add(rt); y += 40;
        var rtNote = new Label { Left = 90, Top = y - 6, Width = 440, Height = 20, ForeColor = TM.C(255, 170, 150, 190), Text = "" }; f.Controls.Add(rtNote); fm.SelectedIndexChanged += (s0, e0) => { rtNote.Text = fm.SelectedIndex == 4 && rt.SelectedIndex > 1 ? "MP3 stops at 48 kHz, so it is saved at 48 kHz." : ""; }; rt.SelectedIndexChanged += (s0, e0) => { rtNote.Text = fm.SelectedIndex == 4 && rt.SelectedIndex > 1 ? "MP3 stops at 48 kHz, so it is saved at 48 kHz." : (rt.SelectedIndex > 0 ? "Made at 44.1 kHz and converted with a high quality filter: bigger files, the same sound." : ""); }; y += 18;
        // what each format and rate means, in plain words (for people who are new to this)
        var fmDesc = new Label { Left = 90, Top = y + 2, Width = side ? 470 : 446, Height = 78, ForeColor = TM.C(255, 205, 190, 230), Text = "" }; f.Controls.Add(fmDesc);
        Action descUpd = () => { int a0 = Math.Max(0, fm.SelectedIndex), b0 = Math.Max(0, rt.SelectedIndex); fmDesc.Text = FormatHint(a0) + "\r\n" + RateHint(b0, a0); };
        fm.SelectedIndexChanged += (s0, e0) => descUpd(); rt.SelectedIndexChanged += (s0, e0) => descUpd(); descUpd(); y += 84;
        f.Controls.Add(new Label { Text = "Name", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var nm = new TextBox { Left = 90, Top = y, Width = 440, Text = defName, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(nm); f.Controls.Add(nm); y += 38;
        f.Controls.Add(new Label { Text = "Folder", Left = 16, Top = y + 4, Width = 70, Height = 22 });
        var dir = new TextBox { Left = 90, Top = y, Width = 350, Text = LastExportDir, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(dir);   /* the folder you used last time (only this is remembered, the other choices start fresh) */ f.Controls.Add(dir);
        var br = B("Browse...", 448, y - 2, 84, 28, TM.C(255, 70, 40, 120)); br.Click += (s, e) => { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) { dir.Text = d.SelectedPath; LastExportDir = d.SelectedPath; } }; f.Controls.Add(br);   /* the folder is kept the moment you pick it */ y += 42;
        f.Controls.Add(new Label { Text = "All files start at 0:00 with the same length, so stems line up in any DAW. Existing files are never replaced without asking.", Left = 16, Top = y, Width = 520, Height = 40, ForeColor = TM.C(255, 170, 150, 190) });
        var ok = B("Export", 330, 588, 100, 34, TM.C(255, 118, 52, 212)); var cancel = B("Cancel", 440, 588, 100, 34, TM.C(255, 70, 40, 120));
        var tErr = new Label { Left = 16, Top = 592, Width = 300, Height = 26, ForeColor = TM.C(255, 255, 110, 130), Text = "" }; f.Controls.Add(tErr);
        ok.Click += (s, e) =>
        {
            if (nm.Text.Trim().Length == 0 || dir.Text.Trim().Length == 0) return;
            double sa, sb; if (!ParseTime(ta.Text, out sa) || !ParseTime(tb.Text, out sb) || sb <= sa) { tErr.Text = "Trim: the end must be after the start."; return; }
            if (songLen > 0 && sa >= songLen) { tErr.Text = "Trim: the start is after the end of the song."; return; }
            List<int> picked = null; if (side && cks[4].Checked) { picked = new List<int>(); for (int i = 0; i < stemCk.Count; i++) if (stemCk[i].Checked) picked.Add(i); if (picked.Count == 0) { tErr.Text = "Pick at least one track for the stems."; return; } if (picked.Count == stemCk.Count) picked = null; }
            bool whole = sa <= 0.0005 && (songLen <= 0 || sb >= songLen - 0.05);   // not trimmed = the whole song
            result = new ExportSettings { TrimStart = whole ? 0 : sa, TrimEnd = whole ? 0 : sb, MixMastered = cks[0].Checked, MixDry = cks[1].Checked, ProcVocal = cks[2].Checked, ProcBeat = cks[3].Checked, Stems = cks[4].Checked, StemTracks = picked, Fmt = fm.SelectedIndex, RateIdx = rt.SelectedIndex, Dir = dir.Text.Trim(), Prefix = nm.Text.Trim() }; LastExportDir = dir.Text.Trim(); f.Close();
        };
        cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, cancel });
        f.ShowDialog(owner); return result;
    }
}
