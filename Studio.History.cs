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

    string histUntitled = Guid.NewGuid().ToString("N").Substring(0, 8);   // each untitled project (this session, or after NEW PROJECT) keeps its own history folder
    string HistDirNow() { return !string.IsNullOrEmpty(projPath) ? projPath + ".history" : Path.Combine(StudioEnv.DataDir, "history", "untitled-" + histUntitled); }
    // ---- a project put in the place of the open one (open, restore, recovery) ----
    List<Fx> TrackFxNow() { var l = new List<Fx>(); lock (eng.Sync) foreach (var t in eng.Tracks) l.AddRange(t.Fx); return l; }
    // the old tracks' plugins are retired (not disposed: an undo snapshot may still hold them) and their windows closed, like NEW PROJECT
    void RetireReplaced(List<Fx> oldFx) { var keep = new HashSet<Fx>(TrackFxNow()); foreach (var f in oldFx) { if (keep.Contains(f)) continue; if (!retired.Contains(f)) retired.Add(f); var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); } AutoMix.LastSnapshot = null; }
    // the loaded project's piano roll settings, its own tempo, and nothing selected from the old one (like opening a project)
    void AfterProjectLoaded() { PrApplyPrefs(ProjectIO.Roll); bpmUserSet = true; selTrack = 0; selFx = -1; }
    // a throwaway engine (preview, one-track restore, recovery test): its plugins are disposed once it has stopped
    static void DisposeEngineFx(StudioEngine pe)
    {
        if (pe == null) return; var l = new List<Fx>(); lock (pe.Sync) { foreach (var t in pe.Tracks) l.AddRange(t.Fx); l.AddRange(pe.MasterFx); }
        foreach (var f in l) { var d = f as IDisposable; if (d != null) { try { d.Dispose(); } catch { } } }
    }
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
        int n = ++histN; string file = n.ToString("00000") + ".ytproj"; ProjectIO.Roll = PrPrefsText(); ProjectIO.Save(eng, Path.Combine(histDir, file));
        var e = new HistEntry { N = n, At = DateTime.Now, Label = HistLabel(), Tracks = eng.Tracks.Count(t => t.Host == null && !t.IsBus), File = Path.Combine(histDir, file) }; hist.Add(e);
        while (hist.Count > 300) { try { File.Delete(hist[0].File); } catch { } hist.RemoveAt(0); }   // the oldest go after 300 states
        File.WriteAllLines(Path.Combine(histDir, "index.txt"), hist.Select(h => h.N + "|" + h.At.Ticks + "|" + h.Label.Replace("|", "/") + "|" + h.Tracks + "|" + Path.GetFileName(h.File)).ToArray());
    }
    // the whole project back as it was then (your project file keeps its name; the state you leave is in the history too)
    void HistRestoreAll(HistEntry e)
    {
        if (!AskYes("Put the whole project back as it was at " + e.At.ToString("ddd d MMM HH:mm") + "?\n\nThe state you have now stays in the history, so you can come back to it.", "Restore")) return;
        if (busy) { Toast("Wait for the current job to finish"); return; }
        HistWrite(); string keepPath = projPath, keepName = projName; string file = e.File; var oldFx = TrackFxNow();
        RunBusy("Restoring...", false, p => { ProjectIO.Load(eng, file, s => busyText = s); busyText = "Warming up the plugins..."; eng.Prime(); }, ok => { RetireReplaced(oldFx); if (!ok) return; AfterProjectLoaded(); projPath = keepPath; projName = keepName; undo.Clear(); redo.Clear(); editCount++; FitView(); Toast("The project is as it was at " + e.At.ToString("HH:mm") + ". Ctrl+Z will not undo this: the HISTORY page has the state before it."); });
    }
    // one track back as it was then, the rest stays as it is now
    void HistRestoreTrack(HistEntry e, string trackName)
    {
        if (busy) { Toast("Wait for the current job to finish"); return; }
        StudioEngine te = null; string file = e.File;   /* the state is read in the background (big audio files no longer freeze the window) */
        RunBusy("Reading " + trackName + " from then...", false, p => { te = new StudioEngine { ListenOnly = true }; StudioEngine.Current = eng; var tm = new ProjectIO.ProjMeta(); ProjectIO.Load(te, file, s => busyText = s, tm); if (!double.IsNaN(tm.Bpm) && tm.Bpm > 0) te.TempoOverride = tm.Bpm; }, ok =>
        {
            if (!ok || te == null) { if (te != null) { try { te.Shutdown(); } catch { } DisposeEngineFx(te); } return; }
            var src = te.Tracks.FirstOrDefault(t => t.Name == trackName && t.Host == null); if (src == null) { try { te.Shutdown(); } catch { } DisposeEngineFx(te); Toast("That track is not in that state."); return; }
            var srcPieces = te.Tracks.Where(t => t.Host == src && !t.IsBus).ToList();   // the other pieces that sat on that row then (SPLIT, layers): they come back with it
            PushUndo(); var dst = eng.Tracks.FirstOrDefault(t => t.Name == trackName && t.Host == null); List<Fx> oldFx = null;
            // sends go to the buses of this project with the same name (a send to a bus this project does not have is dropped)
            Func<Send[], Send[]> mapSends = ss => { var sends = new List<Send>(); foreach (var sd in ss) { var bn = sd.Bus != null ? sd.Bus.Name : null; var bus = bn == null ? null : eng.Tracks.FirstOrDefault(b => b.IsBus && b.Name == bn); if (bus != null && !sends.Exists(x => x.Bus == bus)) { sd.Bus = bus; sends.Add(sd); } } return sends.ToArray(); };
            lock (eng.Sync)
            {
                te.Tracks.Remove(src); foreach (var pc in srcPieces) te.Tracks.Remove(pc); src.Host = null;
                if (dst == null)
                {   // its output and sends go to the buses of this project with the same name, or to the master
                    var outName = src.Out != null ? src.Out.Name : null; src.Out = outName == null ? null : eng.Tracks.FirstOrDefault(b => b.IsBus && b.Name == outName);
                    src.Sends = mapSends(src.Sends);
                    eng.Tracks.Insert(Math.Min(LaneCount, eng.Tracks.Count), src); foreach (var f in src.Fx) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = src; }
                }
                else
                {
                    oldFx = new List<Fx>(dst.Fx); dst.Clip = src.Clip; dst.Source = src.Source; dst.Offset = src.Offset; dst.ClipStart = src.ClipStart; dst.ClipFrames = src.ClipFrames; dst.VolDb = src.VolDb; dst.Pan = src.Pan; dst.Mute = src.Mute; dst.Instrument = src.Instrument; dst.Fx = src.Fx; foreach (var f in dst.Fx) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = dst; } lock (dst.Midi) { dst.Midi.Clear(); dst.Midi.AddRange(src.Midi); } dst.Auto.Clear(); dst.Auto.AddRange(src.Auto);
                    dst.MClips = src.MClips; dst.FadeIn = src.FadeIn; dst.FadeOut = src.FadeOut; dst.FadeInDelay = src.FadeInDelay; dst.FadeOutTail = src.FadeOutTail; dst.FadeInBend = src.FadeInBend; dst.FadeOutBend = src.FadeOutBend; dst.ClipGainDb = src.ClipGainDb; dst.TrimDb = src.TrimDb; dst.Invert = src.Invert; dst.Mono = src.Mono; dst.Locked = src.Locked; dst.PieceLocked = src.PieceLocked; dst.PieceMuted = src.PieceMuted; dst.SectionLabel = src.SectionLabel;
                    foreach (var old in eng.Tracks.Where(t => t.Host == dst).ToList()) { oldFx.AddRange(old.Fx); eng.Tracks.Remove(old); }   // the pieces on this row now make way for the ones it had then (their plugins are retired below)
                }
                var row = dst ?? src;
                foreach (var pc in srcPieces)
                {   /* each piece goes back on the row (pieces are kept after the rows, before the buses) */
                    pc.Host = row; pc.Out = row.Out; pc.Solo = false; pc.Sends = mapSends(pc.Sends);
                    eng.Tracks.Insert(FirstBusIndex(), pc); foreach (var f in pc.Fx) { var inf = f as InstrumentFx; if (inf != null) inf.Owner = pc; }
                }
                if (selTrack >= eng.Tracks.Count) selTrack = Math.Max(0, eng.Tracks.IndexOf(row));
            }
            if (oldFx != null) foreach (var f in oldFx) { if (dst.Fx.Contains(f)) continue; if (!retired.Contains(f)) retired.Add(f); var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }   // the replaced plugins stay alive for Ctrl+Z
            try { te.Shutdown(); } catch { } DisposeEngineFx(te);   /* the rest of that state goes (the restored track was taken out of it first) */
            Toast(trackName + " is as it was at " + e.At.ToString("HH:mm") + "; every other track stays as it is. Ctrl+Z undoes it.");
        });
    }
    // FIRST SAVE OF AN UNTITLED PROJECT: its history (history/untitled-<id>) moves next to the saved project ("<project>.history"), so the
    // states made before the first save stay on its HISTORY page. Called after the save succeeded. A destination that already has a history
    // (a project saved over) gets these states added after its own, renumbered. Audio written into the history folder moves too, and every
    // path to it (the history states, the project just saved, the clips in memory and in Ctrl+Z) is rewritten. Nothing that failed to move is
    // deleted: a file that could not move stays where it was, and the paths to it are left pointing there.
    internal void HistAdoptUntitled()
    {
        if (string.IsNullOrEmpty(projPath)) return;
        string from = Path.Combine(StudioEnv.DataDir, "history", "untitled-" + histUntitled), to = projPath + ".history";
        try
        {
            if (!Directory.Exists(from) || string.Equals(Path.GetFullPath(from).TrimEnd('\\'), Path.GetFullPath(to).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(to);
            // 1. the audio files (names carry a time stamp, so a collision is rare: a file whose name is taken stays where it is)
            var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); string fa = Path.Combine(from, "audio"), ta = Path.Combine(to, "audio");
            if (Directory.Exists(fa)) foreach (var f in Directory.GetFiles(fa)) { string d = Path.Combine(ta, Path.GetFileName(f)); if (File.Exists(d)) continue; try { Directory.CreateDirectory(ta); File.Move(f, d); moved[f] = d; } catch { } }
            Func<string, string> fix = s => { foreach (var kv in moved) s = s.Replace(EncPath(kv.Key), EncPath(kv.Value)); return s; };   // (paths are written escaped in project files)
            // 2. the states, renumbered after the ones the destination already has; the index of both is merged
            var lines = new List<string>(); int top = 0; string tIdx = Path.Combine(to, "index.txt"), fIdx = Path.Combine(from, "index.txt");
            if (File.Exists(tIdx)) foreach (var l in File.ReadAllLines(tIdx)) { var p = l.Split('|'); int n; if (p.Length < 5 || !int.TryParse(p[0], out n)) continue; lines.Add(l); top = Math.Max(top, n); }
            var leftLines = new List<string>();
            if (File.Exists(fIdx)) foreach (var l in File.ReadAllLines(fIdx))
                {
                    var p = l.Split('|'); int n; if (p.Length < 5 || !int.TryParse(p[0], out n)) continue; string sf = Path.Combine(from, p[4]); if (!File.Exists(sf)) continue;
                    int nn = top + 1; string name = nn.ToString("00000") + ".ytproj", df = Path.Combine(to, name); while (File.Exists(df)) { nn++; name = nn.ToString("00000") + ".ytproj"; df = Path.Combine(to, name); }
                    try { File.WriteAllText(df, fix(File.ReadAllText(sf, System.Text.Encoding.UTF8)), System.Text.Encoding.UTF8); File.Delete(sf); top = nn; p[0] = nn.ToString(); p[4] = name; lines.Add(string.Join("|", p)); }
                    catch { try { if (File.Exists(sf) && File.Exists(df)) File.Delete(df); } catch { } leftLines.Add(l); }   /* it stays in the old folder, listed there */
                }
            if (lines.Count > 0) File.WriteAllLines(tIdx, lines.ToArray());
            if (File.Exists(fIdx)) { if (leftLines.Count > 0) File.WriteAllLines(fIdx, leftLines.ToArray()); else File.Delete(fIdx); }
            // 3. the project just saved, and the clips in memory (the track list, the undo steps, the audio the history wrote) point at the new place
            if (moved.Count > 0)
            {
                try { string txt = File.ReadAllText(projPath, System.Text.Encoding.UTF8), nt = fix(txt); if (nt != txt) Win.WriteAtomic(projPath, nt, System.Text.Encoding.UTF8); }
                catch { foreach (var kv in moved) { try { if (!File.Exists(kv.Key)) { Directory.CreateDirectory(fa); File.Copy(kv.Value, kv.Key); } } catch { } } }   /* the project could not be rewritten: a copy goes back where the saved project looks for it */
                var clips = new HashSet<AudioClip>(); foreach (var t in eng.Tracks.ToArray()) if (t.Clip != null) clips.Add(t.Clip);
                foreach (var s in undo.Concat(redo)) foreach (var ts in s.St.Values) if (ts.Clip != null) clips.Add(ts.Clip);
                foreach (var c in histAudio.Keys) clips.Add(c);
                string np; foreach (var c in clips) if (c.Path != null && moved.TryGetValue(c.Path, out np)) c.Path = np;
                foreach (var c in histAudio.Keys.ToList()) if (moved.TryGetValue(histAudio[c], out np)) histAudio[c] = np;
            }
            // 4. the old folder goes only when nothing is left in it
            try { if (Directory.Exists(fa) && !Directory.EnumerateFileSystemEntries(fa).Any()) Directory.Delete(fa); if (!Directory.EnumerateFileSystemEntries(from).Any()) Directory.Delete(from); } catch { }
        }
        catch { }
        histDir = "";   /* the HISTORY page reads the project's folder again */
    }
    static string EncPath(string s) { return s.Replace("%", "%25").Replace("|", "%7C"); }   // the same escaping ProjectIO uses for a path in a project file
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
    void Autosave() { if (StudioEnv.UnderTest && !autoTest) return; if (!HasUnsaved || eng.Tracks.Count == 0 || busy) return; Directory.CreateDirectory(AutoDir); string tmp = AutoFile + ".tmp"; ProjectIO.Roll = PrPrefsText(); ProjectIO.Save(eng, tmp); if (File.Exists(AutoFile)) File.Delete(AutoFile); File.Move(tmp, AutoFile); File.WriteAllText(Path.Combine(AutoDir, "autosave.txt"), projPath + "\n" + projName + "\n" + DateTime.Now.Ticks + "\n" + histUntitled); }
    internal bool autoTest;   // a test switches autosave on
    // the project was saved: the autosave is older than the save now, so it goes (a crash later never offers stale work back)
    void AutosaveClear() { if (StudioEnv.UnderTest && !autoTest) return; try { if (File.Exists(AutoFile)) File.Delete(AutoFile); string inf = Path.Combine(AutoDir, "autosave.txt"); if (File.Exists(inf)) File.Delete(inf); } catch { } }
    // at start: a session that did not close properly leaves its marker; offer the autosave back
    internal void SessionStart()
    {
        try
        {
            Directory.CreateDirectory(AutoDir); bool crashed = File.Exists(LockFile) && File.Exists(AutoFile); File.WriteAllText(LockFile, DateTime.Now.Ticks.ToString());
            if (!crashed || StudioEnv.UnderTest) { if (!StudioEnv.UnderTest) HistPruneUntitled(); return; } var info = File.Exists(Path.Combine(AutoDir, "autosave.txt")) ? File.ReadAllLines(Path.Combine(AutoDir, "autosave.txt")) : new string[0]; string name = info.Length > 1 ? info[1] : "your project";
            string real = info.Length > 0 ? info[0] : ""; bool hasReal = real.Length > 0 && File.Exists(real);
            /* (a save deletes the autosave, so one still here is newer than the last save: the file's own date is not trusted, a backup or sync tool can touch it) */
            string q = "Purphex closed unexpectedly last time (it crashed or the PC turned off) at " + File.GetLastWriteTime(AutoFile).ToString("ddd HH:mm") + ".\n\nYour work on \"" + name + "\" is still here, including what you had not saved.\n\n"
                + "YES: bring it back and carry on from where you were. Nothing is saved until you press SAVE.\n"
                + "NO: go to your Projects, to open a project or start fresh. This work is kept there too, as a recovered project, so nothing is lost.\n\nYour last save is never changed by this.";
            var a = (recoverAsk ?? (t => System.Windows.Forms.MessageBox.Show(this, t, "Pick up where you left off?", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question)))(q);
            var oldFx = TrackFxNow();
            if (a == System.Windows.Forms.DialogResult.Yes && info.Length > 3 && System.Text.RegularExpressions.Regex.IsMatch(info[3], "^[0-9a-f]{8}$")) histUntitled = info[3];   // the recovered untitled project keeps its own history
            if (a == System.Windows.Forms.DialogResult.Yes) { string file = AutoFile; RunBusy("Bringing your work back...", false, p => { ProjectIO.Load(eng, file, s => busyText = s); busyText = "Warming up the plugins..."; eng.Prime(); }, ok => { RetireReplaced(oldFx); if (!ok) return; AfterProjectLoaded(); undo.Clear(); redo.Clear(); projPath = real; projName = name; editCount++; FitView(); Toast("You're back where you were. Press SAVE to keep it (your last save is untouched until then)."); }); }
            else { string kept = KeepRecovered(name, real); if (kept != null) AutosaveClear(); GoTab(3); Toast(kept != null ? "Your unsaved work is kept on your Projects as \"" + Path.GetFileNameWithoutExtension(kept) + "\". Open a project or start fresh." : "Your unsaved work could not be copied to your Projects, so it is still kept and you will be asked again next time. Open a project or start fresh."); }   /* NO (or closing the question): the Projects page, and the crashed work is never thrown away */
        }
        catch { }
    }
    // the crashed work, put on your Projects as its own project (with Purphex's own copy of its audio); its path, or null
    string KeepRecovered(string name, string real)
    {
        try
        {
            if (!File.Exists(AutoFile)) return null; string dir = ProjectLibrary.DefaultFolder; Directory.CreateDirectory(dir);
            string stem = string.Join("_", ((name ?? "").Trim().Length > 0 && name != "your project" ? name.Trim() : "Untitled").Split(Path.GetInvalidFileNameChars())) + " (recovered " + File.GetLastWriteTime(AutoFile).ToString("d MMM HH-mm") + ")";
            string target = Path.Combine(dir, stem + ".ytmproj"); int n = 2; while (File.Exists(target)) target = Path.Combine(dir, stem + " " + n++ + ".ytmproj");
            File.Copy(AutoFile, target, false); int tracks = 0; double bpm = 0;
            foreach (var line in File.ReadAllLines(target)) { if (line.StartsWith("track|")) tracks++; else if (line.StartsWith("bpm|")) double.TryParse(line.Substring(4), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bpm); }
            ProjectLibrary.LoadOnce(); ProjectLibrary.Touch(target, Path.GetFileNameWithoutExtension(target), tracks, bpm, 0, true, null); ProjectVault.KeepAsync(target, Path.GetFileNameWithoutExtension(target));
            return target;
        }
        catch { return null; }
    }
    // untitled histories nobody can reach any more (their session is long gone) are cleared after 30 days
    void HistPruneUntitled() { try { string root = Path.Combine(StudioEnv.DataDir, "history"); if (!Directory.Exists(root)) return; foreach (var d in Directory.GetDirectories(root, "untitled-*")) { if (Path.GetFileName(d) == "untitled-" + histUntitled) continue; if ((DateTime.Now - Directory.GetLastWriteTime(d)).TotalDays > 30) { try { Directory.Delete(d, true); } catch { } } } } catch { } }
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
            var te = new StudioEngine { ListenOnly = true }; StudioEngine.Current = eng; var tm2 = new ProjectIO.ProjMeta(); var warn = ProjectIO.Load(te, auto, s => { }, tm2); if (!double.IsNaN(tm2.Bpm) && tm2.Bpm > 0) te.TempoOverride = tm2.Bpm; p(0.7f);   // 4. the recovery, into a sandbox engine
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
            try { te.Shutdown(); } catch { } DisposeEngineFx(te); rep.Ok = diffs == 0; p(1f);
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
