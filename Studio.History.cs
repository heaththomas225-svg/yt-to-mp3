using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

// =====================================================================================================
//  HISTORY THAT OUTLIVES THE SESSION (tab 15; Edit > History, or HISTORY in the menu): every change you make is also written next to the
//  project (in "<project>.history", or the data folder for a project with no file yet) as a small project file. Close Purphex, come back
//  tomorrow, and the HISTORY page still lists every state: hear any of them (PREVIEW), put the whole project back as it was (RESTORE ALL),
//  or bring back just one track from then (RESTORE THIS TRACK) without touching the rest. Audio that has no file of its own (a reversed
//  clip, a recording in memory) is written once into the history folder so every state can really be opened again.
//
//  AUTOSAVE AND CRASH RECOVERY: every two minutes an unsaved project is written to the autosave; a marker says a session is open.
//  If Purphex did not close properly, the next start offers the autosave back.
//  SESSION RECOVERY TEST (Tools): proves recovery works without touching your project: a copy goes into a sandbox, a crash is simulated
//  there, the recovery runs on the copy, and the result is compared with your project, track by track.
// =====================================================================================================
partial class StudioForm
{
    sealed class HistEntry { public int N; public DateTime At; public string Label = "", File = ""; public int Tracks; }
    readonly List<HistEntry> hist = new List<HistEntry>(); string histDir = ""; DateTime histDue = DateTime.MinValue; int histN; float histScroll; HistEntry histOpen;
    readonly Dictionary<AudioClip, string> histAudio = new Dictionary<AudioClip, string>();
    static readonly CultureInfo HCI = CultureInfo.InvariantCulture;

    string HistDirNow() { return !string.IsNullOrEmpty(projPath) ? projPath + ".history" : Path.Combine(StudioEnv.DataDir, "history", "untitled"); }
    // a change was made: the state is written a moment later (so a drag is one step, not hundreds)
    void HistNote() { histDue = DateTime.Now.AddSeconds(1.2); }
    void HistTick()
    {
        if (StudioEnv.UnderTest && !autoTest) return;   // (a test switches it on when it checks it)
        if (histDue != DateTime.MinValue && DateTime.Now >= histDue && !busy) { histDue = DateTime.MinValue; try { HistWrite(); } catch { } }
        if (DateTime.Now >= autoDue) { autoDue = DateTime.Now.AddSeconds(30); try { Autosave(); } catch { } }
    }
    void HistEnsureLoaded() { string d = HistDirNow(); if (d == histDir) return; histDir = d; hist.Clear(); histN = 0; HistRead(); }
    void HistRead()
    {
        string idx = Path.Combine(histDir, "index.txt"); if (!File.Exists(idx)) return;
        foreach (var line in File.ReadAllLines(idx)) { var p = line.Split('|'); if (p.Length < 5) continue; int n; long t; int tr; if (!int.TryParse(p[0], out n) || !long.TryParse(p[1], out t) || !int.TryParse(p[3], out tr)) continue; var e = new HistEntry { N = n, At = new DateTime(t), Label = p[2], Tracks = tr, File = Path.Combine(histDir, p[4]) }; if (File.Exists(e.File)) hist.Add(e); histN = Math.Max(histN, n); }
    }
    // what changed, in a few words (tracks added or taken away, or the tracks that changed)
    string HistLabel()
    {
        var names = eng.Tracks.Where(t => t.Host == null && !t.IsBus).Select(t => t.Name).ToList(); var last = hist.Count > 0 ? hist[hist.Count - 1] : null;
        if (last == null) return "Start: " + names.Count + " tracks"; if (names.Count > last.Tracks) return "Added a track (" + names.Count + " tracks)"; if (names.Count < last.Tracks) return "Removed a track (" + names.Count + " tracks)";
        var sel = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack].Name : ""; return sel.Length > 0 ? "Edited " + sel : "Edit";
    }
    void HistWrite()
    {
        if (eng.Tracks.Count == 0) return; HistEnsureLoaded(); Directory.CreateDirectory(histDir); string adir = Path.Combine(histDir, "audio");
        // audio that only lives in memory gets a file once, so this state can be opened again later
        foreach (var t in eng.Tracks.ToArray()) { var c = t.Clip; if (c == null || !string.IsNullOrEmpty(c.Path) && File.Exists(c.Path)) continue; string p; if (!histAudio.TryGetValue(c, out p)) { Directory.CreateDirectory(adir); p = Path.Combine(adir, "clip " + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".wav"); if (c.L != null) WavIO.Write(p, c.L, c.R ?? c.L, StudioEngine.SR, 24); histAudio[c] = p; } c.Path = p; }
        int n = ++histN; string file = n.ToString("00000") + ".ytproj"; ProjectIO.Save(eng, Path.Combine(histDir, file));
        var e = new HistEntry { N = n, At = DateTime.Now, Label = HistLabel(), Tracks = eng.Tracks.Count(t => t.Host == null && !t.IsBus), File = Path.Combine(histDir, file) }; hist.Add(e);
        while (hist.Count > 300) { try { File.Delete(hist[0].File); } catch { } hist.RemoveAt(0); }   // the oldest go after 300 states
        File.WriteAllLines(Path.Combine(histDir, "index.txt"), hist.Select(h => h.N + "|" + h.At.Ticks + "|" + h.Label.Replace("|", "/") + "|" + h.Tracks + "|" + Path.GetFileName(h.File)).ToArray());
    }
    // the whole project back as it was then (your project file keeps its name; the state you leave is in the history too)
    void HistRestoreAll(HistEntry e)
    {
        if (!AskYes("Put the whole project back as it was at " + e.At.ToString("ddd d MMM HH:mm") + "?\n\nThe state you have now stays in the history, so you can come back to it.", "Restore")) return;
        HistWrite(); string keepPath = projPath, keepName = projName; string file = e.File;
        RunBusy("Restoring...", false, p => ProjectIO.Load(eng, file, s => busyText = s), ok => { projPath = keepPath; projName = keepName; undo.Clear(); redo.Clear(); editCount++; FitView(); Toast("The project is as it was at " + e.At.ToString("HH:mm") + ". Ctrl+Z will not undo this: the HISTORY page has the state before it."); });
    }
    // one track back as it was then, the rest stays as it is now
    void HistRestoreTrack(HistEntry e, string trackName)
    {
        StudioEngine te = null; try { te = new StudioEngine { ListenOnly = true }; StudioEngine.Current = eng; ProjectIO.Load(te, e.File, s => { }, new ProjectIO.ProjMeta()); } catch (Exception ex) { Toast("That state could not be opened: " + ex.Message); return; }
        var src = te.Tracks.FirstOrDefault(t => t.Name == trackName && t.Host == null); if (src == null) { Toast("That track is not in that state."); return; }
        PushUndo(); var dst = eng.Tracks.FirstOrDefault(t => t.Name == trackName && t.Host == null);
        lock (eng.Sync)
        {
            te.Tracks.Remove(src);
            if (dst == null) { eng.Tracks.Insert(Math.Min(LaneCount, eng.Tracks.Count), src); foreach (var f in src.Fx) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = src; } }
            else { dst.Clip = src.Clip; dst.Offset = src.Offset; dst.ClipStart = src.ClipStart; dst.ClipFrames = src.ClipFrames; dst.VolDb = src.VolDb; dst.Pan = src.Pan; dst.Mute = src.Mute; dst.Instrument = src.Instrument; dst.Fx = src.Fx; foreach (var f in dst.Fx) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = dst; } lock (dst.Midi) { dst.Midi.Clear(); dst.Midi.AddRange(src.Midi); } dst.Auto.Clear(); dst.Auto.AddRange(src.Auto); }
        }
        try { te.Shutdown(); } catch { } Toast(trackName + " is as it was at " + e.At.ToString("HH:mm") + "; every other track stays as it is. Ctrl+Z undoes it.");
    }
    void HistPreview(HistEntry e) { PreviewToggle(new ProjectEntry { Path = e.File, Name = "History " + e.At.ToString("HH:mm:ss") }); }

    void PaintHistory(Graphics g) { PaintHistory(g, Rc(14, ToolH + 12, ClientSize.Width - 28, ClientSize.Height - ToolH - 26)); }
    void PaintHistory(Graphics g, RectangleF a)
    {
        HistEnsureLoaded();
        FillRR(g, TM.C(225, 12, 6, 28), a, 14); StrokeRR(g, Color.FromArgb(180, NeonViolet), a, 14, 1.3f);
        Txt(g, "HISTORY", 16, Ink, Rc(a.X + 16, a.Y + 12, 300, 24), StringAlignment.Near, true);
        Txt(g, "Every state of this project, kept after you close Purphex. Hear one, put the whole project back, or bring back just one track.", 11, DimC, Rc(a.X + 16, a.Y + 38, a.Width - 32, 18), StringAlignment.Near, false);
        var list = hist.AsEnumerable().Reverse().ToList(); float y = a.Y + 66, rowH = 52; var area = Rc(a.X + 10, y, a.Width - 20, a.Bottom - y - 10);
        if (list.Count == 0) { Txt(g, "No history yet: make a change and it is kept here.", 12, DimC, Rc(area.X + 8, area.Y + 6, 500, 18), StringAlignment.Near, false); return; }
        float total = list.Sum(e => e == histOpen ? rowH + 34 * Math.Max(1, e.Tracks) : rowH); histScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), histScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => histScroll -= d * 50 }); var gs = g.Save(); g.SetClip(area); float yy = area.Y - histScroll;
        // a timeline down the left side: one dot per state
        using (var pen = new Pen(Color.FromArgb(120, NeonCyan), 2f)) g.DrawLine(pen, area.X + 18, area.Y, area.X + 18, area.Bottom);
        foreach (var e in list)
        {
            bool open = e == histOpen; float h = open ? rowH + 34 * Math.Max(1, e.Tracks) : rowH; var row = Rc(area.X + 34, yy, area.Width - 44, h - 6); yy += h; if (row.Bottom < area.Y || row.Y > area.Bottom) continue;
            using (var db = new SolidBrush(open ? NeonPink : NeonCyan)) g.FillEllipse(db, area.X + 12, row.Y + 14, 12, 12);
            FillRR(g, open ? TM.C(230, 40, 18, 80) : TM.C(200, 22, 10, 46), row, 10);
            string when = (DateTime.Now - e.At).TotalHours < 20 ? e.At.ToString("HH:mm:ss") : e.At.ToString("ddd d MMM HH:mm");
            Txt(g, when, 12, Ink, Rc(row.X + 12, row.Y + 4, 170, 20), StringAlignment.Near, true); Txt(g, e.Label + "  ·  " + e.Tracks + " tracks", 11, DimC, Rc(row.X + 12, row.Y + 24, row.Width - 420, 18), StringAlignment.Near, false);
            var ee = e; var pv = Rc(row.Right - 396, row.Y + 8, 110, 30); Btn(g, pv, previewPath == e.File ? "STOP" : "PREVIEW", previewPath == e.File ? 4 : 1, () => { if (previewPath == ee.File) StopPreview(); else HistPreview(ee); }, 11); Hint2(pv, "Hear the project as it was then (your project is not touched)");
            var tb = Rc(row.Right - 278, row.Y + 8, 140, 30); Btn(g, tb, open ? "HIDE TRACKS" : "ONE TRACK...", 0, () => histOpen = open ? null : ee, 11); Hint2(tb, "Bring back just one track from then");
            var ra = Rc(row.Right - 130, row.Y + 8, 120, 30); Btn(g, ra, "RESTORE ALL", 2, () => HistRestoreAll(ee), 11); Hint2(ra, "Put the whole project back as it was then");
            if (open)
            {
                var names = HistTrackNames(e); float ty = row.Y + 46;
                foreach (var nm in names) { var tr = Rc(row.X + 24, ty, row.Width - 48, 28); ty += 34; FillRR(g, TM.C(200, 16, 8, 36), tr, 8); Txt(g, nm, 11, Ink, Rc(tr.X + 10, tr.Y, tr.Width - 200, tr.Height), StringAlignment.Near, true); var nn = nm; var rb = Rc(tr.Right - 180, tr.Y + 2, 176, tr.Height - 4); Btn(g, rb, "RESTORE THIS TRACK", 1, () => HistRestoreTrack(ee, nn), 10); Hint2(rb, "Bring back " + nm + " as it was then; every other track stays as it is"); }
            }
        }
        g.Restore(gs);
    }
    readonly Dictionary<string, List<string>> histNames = new Dictionary<string, List<string>>();
    List<string> HistTrackNames(HistEntry e)
    {
        List<string> l; if (histNames.TryGetValue(e.File, out l)) return l; l = new List<string>();
        try { var hosts = new HashSet<int>(); var lines = File.ReadAllLines(e.File); foreach (var line in lines) if (line.StartsWith("host|")) { var p = line.Split('|'); int i; if (p.Length > 1 && int.TryParse(p[1], out i)) hosts.Add(i); } int k = 0; foreach (var line in lines) if (line.StartsWith("track|")) { var p = line.Split('|'); if (p.Length > 2 && !hosts.Contains(k) && p[1] != "bus") l.Add(p[2].Replace("%7C", "|").Replace("%25", "%")); k++; } } catch { }
        histNames[e.File] = l; return l;
    }

    // ---------------- autosave and crash recovery ----------------
    // (this is never a save of your project: your project file only changes when you press SAVE. It is a hidden copy, kept only so a crash or a
    //  power cut can offer your unsaved work back; it is written every 30 seconds while there are changes you have not saved)
    DateTime autoDue = DateTime.Now.AddSeconds(30);
    internal Func<string, System.Windows.Forms.DialogResult> recoverAsk;   // a test answers for itself
    static string AutoDir { get { return Path.Combine(StudioEnv.DataDir, "autosave"); } }
    static string AutoFile { get { return Path.Combine(AutoDir, "autosave.ytproj"); } }
    static string LockFile { get { return Path.Combine(AutoDir, "session.open"); } }
    void Autosave() { if (StudioEnv.UnderTest && !autoTest) return; if (!HasUnsaved || eng.Tracks.Count == 0 || busy) return; Directory.CreateDirectory(AutoDir); string tmp = AutoFile + ".tmp"; ProjectIO.Save(eng, tmp); if (File.Exists(AutoFile)) File.Delete(AutoFile); File.Move(tmp, AutoFile); File.WriteAllText(Path.Combine(AutoDir, "autosave.txt"), projPath + "\n" + projName + "\n" + DateTime.Now.Ticks); }
    internal bool autoTest;   // a test switches autosave on
    // at start: a session that did not close properly leaves its marker; offer the autosave back
    internal void SessionStart()
    {
        try
        {
            Directory.CreateDirectory(AutoDir); bool crashed = File.Exists(LockFile) && File.Exists(AutoFile); File.WriteAllText(LockFile, DateTime.Now.Ticks.ToString());
            if (!crashed || StudioEnv.UnderTest) return; var info = File.Exists(Path.Combine(AutoDir, "autosave.txt")) ? File.ReadAllLines(Path.Combine(AutoDir, "autosave.txt")) : new string[0]; string name = info.Length > 1 ? info[1] : "your project";
            string real = info.Length > 0 ? info[0] : ""; bool hasReal = real.Length > 0 && File.Exists(real);
            string q = "Purphex closed unexpectedly last time (it crashed or the PC turned off) at " + File.GetLastWriteTime(AutoFile).ToString("ddd HH:mm") + ".\n\nYour work on \"" + name + "\" is still here, including what you had not saved.\n\n"
                + "YES: carry on from where you were (like you never closed it). Nothing is saved until you press SAVE.\n"
                + (hasReal ? "NO: open your last save instead (" + File.GetLastWriteTime(real).ToString("ddd HH:mm") + ").\n" : "NO: start without it.\n")
                + "CANCEL: start fresh.\n\nYour last save is never changed by this, so you can still open it later.";
            var a = (recoverAsk ?? (t => System.Windows.Forms.MessageBox.Show(this, t, "Pick up where you left off?", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question)))(q);
            if (a == System.Windows.Forms.DialogResult.Yes) { string file = AutoFile; RunBusy("Bringing your work back...", false, p => ProjectIO.Load(eng, file, s => busyText = s), ok => { projPath = real; projName = name; editCount++; FitView(); Toast("You're back where you were. Press SAVE to keep it (your last save is untouched until then)."); }); }
            else if (a == System.Windows.Forms.DialogResult.No && hasReal) { RunBusy("Opening your last save...", false, p => ProjectIO.Load(eng, real, s => busyText = s), ok => { projPath = real; projName = name; FitView(); Toast("Opened your last save of " + name + "."); }); }
        }
        catch { }
    }
    internal void SessionEnd() { try { if (File.Exists(LockFile)) File.Delete(LockFile); } catch { } }

    // ---------------- the session recovery test ----------------
    sealed class RecoveryReport { public bool Ok; public List<string> Lines = new List<string>(); public string Sandbox = ""; }
    RecoveryReport recReport;
    internal void RecoveryTest()
    {
        if (eng.Tracks.Count == 0) { Toast("Open or make a project first: the test runs on a copy of it."); return; }
        string sandbox = Path.Combine(Path.GetTempPath(), "purphex-recovery-test-" + Guid.NewGuid().ToString("N").Substring(0, 8)); RecoveryReport rep = null;
        RunBusy("Testing crash recovery on a copy of your project...", false, p =>
        {
            Directory.CreateDirectory(sandbox); rep = new RecoveryReport { Sandbox = sandbox }; string auto = Path.Combine(sandbox, "autosave.ytproj"), lockf = Path.Combine(sandbox, "session.open");
            ProjectIO.Save(eng, auto); File.WriteAllText(lockf, "open"); p(0.3f);   // 1. the autosave, written the same way as the real one; 2. the session marker: then the "crash" (nothing else is saved)
            bool sawCrash = File.Exists(lockf) && File.Exists(auto);   // 3. what the next start would see
            var te = new StudioEngine { ListenOnly = true }; StudioEngine.Current = eng; var warn = ProjectIO.Load(te, auto, s => { }, new ProjectIO.ProjMeta()); p(0.7f);   // 4. the recovery, into a sandbox engine
            var a = eng.Tracks.Where(t => !t.IsBus).ToList(); var b = te.Tracks.Where(t => !t.IsBus).ToList(); int diffs = 0;
            rep.Lines.Add(sawCrash ? "The crash was noticed (the session marker was still there)." : "The crash was NOT noticed."); if (!sawCrash) diffs++;
            if (a.Count != b.Count) { rep.Lines.Add("Tracks: " + a.Count + " now, " + b.Count + " recovered."); diffs++; } else rep.Lines.Add("All " + a.Count + " tracks came back.");
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                var x = a[i]; var y = b[i]; var bad = new List<string>();
                if (x.Name != y.Name) bad.Add("name"); if (x.Offset != y.Offset) bad.Add("position"); if ((x.Clip == null) != (y.Clip == null) || x.Clip != null && y.Clip != null && Math.Abs(x.Clip.Length - y.Clip.Length) > 2) bad.Add("audio");
                if (x.Fx.Count != y.Fx.Count) bad.Add("plugins (" + x.Fx.Count + " / " + y.Fx.Count + ")"); else for (int k = 0; k < x.Fx.Count; k++) if (x.Fx[k].Serialize() != y.Fx[k].Serialize()) { bad.Add("settings of " + x.Fx[k].Title); break; }
                if (Math.Abs(x.VolDb - y.VolDb) > 0.01 || Math.Abs(x.Pan - y.Pan) > 0.01) bad.Add("fader / pan"); if (x.Instrument) { int mx, my; lock (x.Midi) mx = x.Midi.Count; lock (y.Midi) my = y.Midi.Count; if (mx != my) bad.Add("notes"); }
                if (bad.Count > 0) { rep.Lines.Add(x.Name + ": differs in " + string.Join(", ", bad.ToArray())); diffs++; }
            }
            if (warn != null) foreach (var w in warn) { rep.Lines.Add("Missing: " + w); diffs++; }
            int unsaved = a.Count(t => t.Clip != null && string.IsNullOrEmpty(t.Clip.Path)); if (unsaved > 0) rep.Lines.Add(unsaved + " clip(s) only exist in memory (not saved to a file yet): the real autosave keeps them through the HISTORY folder.");
            try { te.Shutdown(); } catch { } rep.Ok = diffs == 0; p(1f);
        }, ok => { recReport = rep; try { Directory.Delete(sandbox, true); } catch { } Invalidate(); });
    }
    void PaintRecoveryReport(Graphics g)
    {
        var rep = recReport; if (rep == null) return; float W = ClientSize.Width, H = ClientSize.Height; var box = Rc(W / 2 - 330, H / 2 - 200, 660, 400);
        using (var b = new SolidBrush(TM.C(170, 2, 1, 8))) g.FillRectangle(b, 0, 0, W, H); Reg(Rc(0, 0, W, H), () => { });
        FillRR(g, TM.C(250, 16, 8, 36), box, 18); StrokeRR(g, rep.Ok ? NeonCyan : Warn, box, 18, 2f);
        Txt(g, rep.Ok ? "RECOVERY WORKS" : "RECOVERY FOUND DIFFERENCES", 20, rep.Ok ? NeonCyan : Warn, Rc(box.X + 24, box.Y + 18, box.Width - 48, 30), StringAlignment.Near, true);
        Txt(g, "A copy of your project was autosaved in a sandbox, a crash was simulated there and the copy was recovered. Your project was not touched.", 11, DimC, Rc(box.X + 24, box.Y + 54, box.Width - 48, 34), StringAlignment.Near, false);
        float y = box.Y + 96; foreach (var l in rep.Lines.Take(12)) { Txt(g, "•  " + l, 11.5f, Ink, Rc(box.X + 24, y, box.Width - 48, 20), StringAlignment.Near, false); y += 22; }
        var ok = Rc(box.Right - 140, box.Bottom - 50, 116, 34); Btn(g, ok, "OK", 2, () => recReport = null, 12);
    }
}
