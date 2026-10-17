using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX CREATIVE STUDIO page (side panel > Video Studio / Photo Studio, tab 10). The editor itself is a page of Purphex's own
//  (creative/*.html, inside the exe) running in WebView2; this side opens files, keeps projects, reads media with ffmpeg, encodes the export
//  from the frames the editor renders, mixes your song from the DAW and asks the AI image service.
// ======================================================================
partial class StudioForm
{
    object crBObj; HubBrowser CrB { get { return (HubBrowser)crBObj; } } internal string crTool = "video"; RectangleF crR; bool crPlaying;
    internal static Func<string, string> CrSaveHook;   // tests: where a save dialog would have saved (null = ask the user)
    string CrAskSave(SaveFileDialog d) { if (CrSaveHook != null) return CrSaveHook(d.FileName); return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null; }
    void OpenCreative(string tool)
    {
        crTool = tool; GoTab(10); if (eng.Playing) eng.Pause();
        if (crBObj == null) { if (!EnsureCreative()) { Toast(hubErr); tab = 0; } }
        else CrSend("{\"t\":\"tool\",\"tool\":\"" + tool + "\"}");
    }
    internal bool EnsureCreative()
    {
        if (crBObj != null) return true;
        if (HubBrowser.RuntimeVersion() == null) { hubErr = "Creative Studio needs Microsoft Edge WebView2 Runtime (part of Windows 11). Install it from Microsoft, then open it again."; return false; }
        string app = CreativeStore.Dir("app"); var asm = typeof(StudioForm).Assembly;
        foreach (var f in new[] { "index.html", "app.css", "engine.js", "ui.js", "cover.js", "templates.js", "photofx.js", "easy.js", "capcut.js", "toolbar.js", "vtoolbar.js" }) using (var st = asm.GetManifestResourceStream("Purphex.creative." + f)) { if (st == null) continue; var path = Path.Combine(app, f); var ms = new MemoryStream(); st.CopyTo(ms); var bytes = ms.ToArray(); if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes); }
        var b = new HubBrowser(this, Path.Combine(StudioEnv.DataDir, "creativeweb"), HubMuted);
        b.MapHost("app.creative", app); b.MapHost("cache.creative", CreativeStore.Dir("cache")); b.MapHost("art.creative", CreativeStore.Dir("art"));
        b.Message = m => { try { BeginInvoke(new Action(() => CrMessage(m, null))); } catch { } };
        b.MessageFiles = (m, files) => { try { BeginInvoke(new Action(() => CrMessage(m, files))); } catch { } };
        b.Upload = CrUpload; b.Serve = p => { var parts = p.Trim('/').Split('/'); string fp; return parts.Length >= 1 && crPaths.TryGetValue(Uri.UnescapeDataString(parts[0]), out fp) ? fp : null; }; b.Changed = () => { lastInput = DateTime.Now; };
        crBObj = b; b.Navigate("https://app.creative/index.html"); return true;
    }
    void CrSend(string json) { if (crBObj != null) CrB.Post(json); }
    void CrSend(Dictionary<string, object> d) { CrSend(JsonOut.Write(d)); }
    static Dictionary<string, object> D(params object[] kv) { var d = new Dictionary<string, object>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1]; return d; }
    string Hex(Color c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }

    // ---- what the editor asks for ----
    void CrMessage(string json, List<string> files)
    {
        if (json == "artdrop") { CrArtFiles(files ?? new List<string>()); return; }
        if (json == "drop" || (files != null && files.Count > 0)) { CrImportPaths(files ?? new List<string>()); return; }
        Dictionary<string, object> m; try { m = MiniJson.Parse(json) as Dictionary<string, object>; } catch { return; } if (m == null) return; string t = HubApi.S(m, "t");
        try
        {
            switch (t)
            {
                case "ready": CrInit(); break;
                case "import": { string what = HubApi.S(m, "what"); using (var d = new OpenFileDialog { Title = "Import media", Multiselect = true, Filter = what == "audio" ? "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aif;*.aiff|All files|*.*" : what == "visual" ? "Pictures and video|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif;*.mp4;*.mov;*.m4v;*.webm;*.mkv;*.avi|All files|*.*" : CreativeStore.Filter }) if (d.ShowDialog(this) == DialogResult.OK) CrImportPaths(d.FileNames.ToList()); break; }
                case "relink": { string id = HubApi.S(m, "id"); using (var d = new OpenFileDialog { Title = "Find the missing file", Filter = CreativeStore.Filter }) if (d.ShowDialog(this) == DialogResult.OK) { var it = CrMediaItem(d.FileName, id); if (it != null) { CrSend(D("t", "relinked", "id", id, "item", it)); CrAnalyse(d.FileName, id); } } break; }
                case "proxy": CrProxy(HubApi.S(m, "id")); break;
                case "song": CrSong(); break;
                case "myProjects": CrMyProjects(); break;
                case "fromProject": CrFromProject(HubApi.S(m, "path")); break;
                case "save": case "autosave": CrSave("video", HubApi.Obj(m, "project"), t == "autosave"); break;
                case "list": CrSend(D("t", "projects", "list", CreativeStore.List("video").Cast<object>().ToList())); break;
                case "open": CrOpen(HubApi.S(m, "id")); break;
                case "rename": CreativeStore.Rename("video", HubApi.S(m, "id"), HubApi.S(m, "name")); CrSend(D("t", "renamed", "id", HubApi.S(m, "id"), "name", CreativeStore.Clip(HubApi.S(m, "name"), 80))); break;
                case "duplicate": CreativeStore.Duplicate("video", HubApi.S(m, "id")); CrSend(D("t", "projects", "list", CreativeStore.List("video").Cast<object>().ToList())); break;
                case "delete": CreativeStore.Delete("video", HubApi.S(m, "id")); CrSend(D("t", "projects", "list", CreativeStore.List("video").Cast<object>().ToList())); break;
                case "exportStart": CrExportStart(m); break;
                case "exportEnd": CrExportEnd(); break;
                case "exportCancel": CrExportCancel(true); break;
                case "subOpen": using (var d = new OpenFileDialog { Title = "Import lyrics with timing", Filter = "Subtitles and lyrics|*.srt;*.lrc|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) { if (new FileInfo(d.FileName).Length > 2 * 1024 * 1024) throw new Exception("That file is too big for lyrics."); CrSend(D("t", "subText", "name", Path.GetFileName(d.FileName), "text", File.ReadAllText(d.FileName))); } break;
                case "subSave": using (var d = new SaveFileDialog { Title = "Save the lyrics as subtitles", Filter = "SubRip subtitles|*.srt", FileName = Safe(HubApi.S(m, "name")) + ".srt" }) if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(d.FileName, HubApi.S(m, "text"), new UTF8Encoding(false)); CrSend(D("t", "artDone", "msg", "Saved " + Path.GetFileName(d.FileName))); } break;
                case "toDaw": tab = 0; break;
                case "playing": crPlaying = m.ContainsKey("on") && m["on"] is bool && (bool)m["on"]; if (crPlaying) { if (eng.Playing) eng.Pause(); if (npPlaying) { HubCmd(HubPlayerJs.Pause(CurSvc ?? HubServices.All[0])); npPlaying = false; } } break;
                case "tool": crTool = HubApi.S(m, "tool"); break;
                // Photo Studio
                case "artImport": using (var d = new OpenFileDialog { Title = "Add pictures", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif", Multiselect = m.ContainsKey("multi") }) if (d.ShowDialog(this) == DialogResult.OK) CrArtFiles(d.FileNames.ToList()); break;
                case "artUploads": CrSend(D("t", "artUploads", "list", CrUploadList())); break;
                case "artUploadDelete": { string fn = Path.GetFileName(HubApi.S(m, "file")); string p = Path.Combine(CreativeStore.Dir("art/uploads"), fn); if (fn.Length > 0 && File.Exists(p)) { File.Delete(p); CrUploadName(fn, null); } CrSend(D("t", "artUploads", "list", CrUploadList())); break; }
                case "fontImport": using (var d = new OpenFileDialog { Title = "Add a font", Filter = "Fonts|*.ttf;*.otf;*.woff;*.woff2", Multiselect = true }) if (d.ShowDialog(this) == DialogResult.OK) { foreach (var f in d.FileNames) { if (new FileInfo(f).Length > 25L * 1024 * 1024) continue; string fn = Safe(Path.GetFileNameWithoutExtension(f)).Replace(" ", "_") + Path.GetExtension(f).ToLowerInvariant(); File.Copy(f, Path.Combine(CreativeStore.Dir("art/fonts"), fn), true); } CrSend(D("t", "fontList", "list", CrFontList(), "added", true)); } break;
                case "fontList": CrSend(D("t", "fontList", "list", CrFontList())); break;
                case "kvSave": { string k = HubApi.S(m, "key"); if (!System.Text.RegularExpressions.Regex.IsMatch(k, "^[a-z]{2,20}$")) break; string v = HubApi.S(m, "value"); if (v.Length > 2 * 1024 * 1024) break; string kp = Path.Combine(CreativeStore.Dir("art/settings"), k + ".json"); File.WriteAllText(kp + ".tmp", v); if (File.Exists(kp)) File.Replace(kp + ".tmp", kp, null); else File.Move(kp + ".tmp", kp); break; }
                case "kvLoad": { string k = HubApi.S(m, "key"); if (!System.Text.RegularExpressions.Regex.IsMatch(k, "^[a-z]{2,20}$")) break; string kp = Path.Combine(CreativeStore.Dir("art/settings"), k + ".json"); CrSend(D("t", "kv", "key", k, "value", File.Exists(kp) ? File.ReadAllText(kp) : "")); break; }
                case "artSave": case "artAutosave": CrSave("art", HubApi.Obj(m, "art"), t == "artAutosave"); break;
                case "artList": { var l = CreativeStore.List("art"); var d = D("t", "artList", "list", l.Cast<object>().ToList(), "silent", m.ContainsKey("silent")); if (m.ContainsKey("silent")) { string last = CreativeStore.LastId("art"); try { bool rec; if (last.Length > 0) { var a = CreativeStore.Load("art", last, out rec); if (rec) d["recover"] = a; } } catch { } } CrSend(d); break; }
                case "artOpen": { bool rec; CrSend(D("t", "artOpened", "art", CreativeStore.Load("art", HubApi.S(m, "id"), out rec))); break; }
                case "artRename": CreativeStore.Rename("art", HubApi.S(m, "id"), HubApi.S(m, "name")); CrSend(D("t", "artList", "list", CreativeStore.List("art").Cast<object>().ToList())); break;
                case "artDuplicate": CreativeStore.Duplicate("art", HubApi.S(m, "id")); CrSend(D("t", "artList", "list", CreativeStore.List("art").Cast<object>().ToList())); break;
                case "artDelete": CreativeStore.Delete("art", HubApi.S(m, "id")); CrSend(D("t", "artList", "list", CreativeStore.List("art").Cast<object>().ToList())); break;
            }
        }
        catch (Exception ex) { CrSend(D("t", t.StartsWith("save") || t == "autosave" ? "saveError" : "mediaError", "msg", ex.Message)); }
    }
    static string Safe(string s) { s = string.Concat((s ?? "").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim(); return s.Length == 0 ? "video" : s; }
    void CrInit()
    {
        var th = Themes.Current; var fonts = new List<object>(); try { using (var ifc = new System.Drawing.Text.InstalledFontCollection()) foreach (var f in ifc.Families.Take(400)) fonts.Add(f.Name); } catch { }
        var init = D("t", "init", "theme", D("bg0", Hex(th.Bg0), "bg1", Hex(th.Bg1), "panel", Hex(th.Panel), "accent", Hex(th.Violet), "accent2", Hex(th.Cyan), "pink", Hex(th.Pink), "lime", Hex(th.Lime)),
            "fonts", fonts, "artist", ProjectLibrary.ProfileName ?? "", "title", projName ?? "", "ffmpeg", StudioEnv.Ffmpeg() != null, "tool", crTool);
        string last = CreativeStore.LastId("video"); if (last.Length > 0) { try { bool rec; var p = CreativeStore.Load("video", last, out rec); CrResolve(p); init["project"] = p; init["recovered"] = rec; } catch { } }
        CrSend(init);
    }
    // media paths become the page's own https addresses again (and missing files are marked)
    void CrResolve(Dictionary<string, object> p)
    {
        foreach (var o in HubApi.Arr(p, "media"))
        {
            var m = o as Dictionary<string, object>; if (m == null) continue; string path = HubApi.S(m, "path"); bool ok = path.Length > 0 && File.Exists(path); m["missing"] = !ok; m.Remove("proxyUrl"); m.Remove("audioUrl");
            if (ok) { string id = HubApi.S(m, "id"); crPaths[id] = path; m["url"] = CreativeStore.UrlFor(path, id); string p2 = path; ThreadPool.QueueUserWorkItem(_ => CrAnalyse(p2, id)); }
        }
    }
    void CrOpen(string id) { bool rec; var p = CreativeStore.Load("video", id, out rec); CrResolve(p); File.WriteAllText(Path.Combine(CreativeStore.Dir(""), "video-last.txt"), id); CrSend(D("t", "project", "project", p, "recovered", rec)); }
    void CrSave(string kind, Dictionary<string, object> p, bool auto)
    {
        if (p == null) throw new Exception("Nothing to save."); CreativeStore.Save(kind, p, auto);
        if (!auto) CrSend(D("t", kind == "art" ? "artSaved" : "saved", "id", HubApi.S(p, "id"), "name", HubApi.S(p, "name")));
    }
    Dictionary<string, object> CrMediaItem(string path, string id)
    {
        string kind = CreativeStore.KindOf(path); if (kind == null) { CrSend(D("t", "mediaError", "msg", "\"" + Path.GetFileName(path) + "\" is not a video, audio or picture file Purphex can use.")); return null; }
        if (!File.Exists(path)) { CrSend(D("t", "mediaError", "msg", "\"" + Path.GetFileName(path) + "\" is not there.")); return null; }
        id = id ?? ("m" + Guid.NewGuid().ToString("N").Substring(0, 12)); crPaths[id] = path;
        var it = D("id", id, "path", path, "url", CreativeStore.UrlFor(path, id), "name", Path.GetFileName(path), "kind", kind, "size", (double)new FileInfo(path).Length, "missing", false);
        if (kind == "image" && Path.GetExtension(path).ToLowerInvariant() == ".png") it["alpha"] = CreativeStore.PngHasAlpha(path);
        return it;
    }
    void CrImportPaths(List<string> paths)
    {
        var items = new List<object>(); var bad = new List<string>();
        foreach (var p in paths) { if (Directory.Exists(p)) { bad.Add(Path.GetFileName(p) + " (a folder)"); continue; } if (CreativeStore.KindOf(p) == null) { bad.Add(Path.GetFileName(p)); continue; } var it = CrMediaItem(p, null); if (it != null) items.Add(it); }
        if (bad.Count > 0) CrSend(D("t", "mediaError", "msg", "Not imported (not a supported video, audio or picture file): " + string.Join(", ", bad.Take(6))));
        if (items.Count == 0) return; CrSend(D("t", "media", "items", items));
        foreach (Dictionary<string, object> it in items) { string p = HubApi.S(it, "path"), id = HubApi.S(it, "id"); ThreadPool.QueueUserWorkItem(_ => CrAnalyse(p, id)); }
    }
    // in the background: what is in the file, and its sound for the waveform, the mix and the visualisers
    void CrAnalyse(string path, string id)
    {
        string ff = StudioEnv.Ffmpeg(), fp = StudioEnv.Ffprobe(); string kind = CreativeStore.KindOf(path); if (kind == "image") return;
        if (ff == null || fp == null) { CrPost(D("t", "mediaError", "msg", "Purphex needs ffmpeg to read the sound of media (for waveforms, the mix and the export). Convert something once in the converter: that installs it.")); return; }
        try
        {
            string err; var info = CreativeStore.Probe(fp, path, out err); if (info == null) { CrPost(D("t", "mediaError", "msg", err)); return; }
            CrPost(D("t", "probe", "id", id, "info", info));
            if ((bool)info["hasAudio"]) { string key = CreativeStore.CacheKey(path); string a = CreativeStore.ExtractAudio(ff, path, key, out err); if (a == null) CrPost(D("t", "mediaError", "msg", err)); else CrPost(D("t", "audioReady", "id", id, "url", "https://cache.creative/" + Path.GetFileName(a))); }
            else CrPost(D("t", "noAudio", "id", id));
        }
        catch (Exception ex) { CrPost(D("t", "mediaError", "msg", "Could not read \"" + Path.GetFileName(path) + "\": " + ex.Message)); }
    }
    void CrPost(Dictionary<string, object> d) { try { BeginInvoke(new Action(() => CrSend(d))); } catch { } }
    void CrProxy(string id)
    {
        // only a file Purphex itself registered (never a path the page makes up)
        string path; if (!crPaths.TryGetValue(id ?? "", out path) || !File.Exists(path)) { CrSend(D("t", "proxyError", "id", id, "msg", "the file is not there")); return; }
        string ff = StudioEnv.Ffmpeg(); if (ff == null) { CrSend(D("t", "proxyError", "id", id, "msg", "ffmpeg is not installed yet (convert something once in the converter)")); return; }
        ThreadPool.QueueUserWorkItem(_ => { string err; string p = CreativeStore.Proxy(ff, path, CreativeStore.CacheKey(path), out err); CrPost(p == null ? D("t", "proxyError", "id", id, "msg", err) : D("t", "proxyDone", "id", id, "url", "https://cache.creative/" + Path.GetFileName(p))); });
    }
    readonly Dictionary<string, string> crPaths = new Dictionary<string, string>();
    // MY SONG: a copy of the current Purphex project's mix (your DAW project and its files are never changed)
    void CrSong()
    {
        if (eng.Tracks.Count == 0 || eng.EndFrame < StudioEngine.SR / 2) { CrSend(D("t", "mediaError", "msg", "Your Purphex project is empty: make or open a song in the DAW first.")); return; }
        string name = Safe(string.IsNullOrEmpty(projName) ? "My song" : projName), path = Path.Combine(CreativeStore.Dir("media"), name + " (mix " + DateTime.Now.ToString("yyyy-MM-dd HHmm") + ").wav"); long frames = eng.EndFrame;
        CrSend(D("t", "artDone", "msg", "Mixing your song for the video..."));
        new Thread(() =>
        {
            string err = null; try { using (var ws = new WavStream(path, StudioEngine.SR, 16)) eng.RenderStream(0, frames, new RenderOpts(), null, (l, r, o, n) => ws.Write(l, r, o, n), null); } catch (Exception ex) { err = ex.Message; }
            try { BeginInvoke(new Action(() => { if (err != null) { CrSend(D("t", "mediaError", "msg", "Could not mix the song: " + err)); return; } CrSend(D("t", "song", "artist", ProjectLibrary.ProfileName ?? "", "title", projName ?? "")); CrImportPaths(new List<string> { path }); })); } catch { }
        }) { IsBackground = true, Name = "creative-song" }.Start();
    }

    // ---- your Purphex projects: pick one and its song (mixed and mastered, like EXPORT) and its picture come into the video ----
    void CrMyProjects()
    {
        ProjectLibrary.LoadOnce(); var list = new List<object>();
        foreach (var e in ProjectLibrary.AllByEdited())
        {
            if (e.Missing) continue; string cover = "";
            try { if (!string.IsNullOrEmpty(e.Cover) && File.Exists(e.Cover) && new FileInfo(e.Cover).Length < 400 * 1024) cover = "data:image/" + (e.Cover.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpeg") + ";base64," + Convert.ToBase64String(File.ReadAllBytes(e.Cover)); } catch { }
            list.Add(D("name", e.Name, "path", e.Path, "secs", e.Seconds, "bpm", e.Bpm, "tracks", e.Tracks, "edited", e.Updated.ToString("yyyy-MM-dd HH:mm"), "cover", cover, "fav", e.Fav, "finished", e.Finished));
            if (list.Count >= 300) break;
        }
        CrSend(D("t", "myProjects", "list", list));
    }
    void CrFromProject(string path)
    {
        ProjectLibrary.LoadOnce(); ProjectEntry e = null; foreach (var x in ProjectLibrary.AllByEdited()) if (string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)) e = x;
        if (e == null || e.Missing) { CrSend(D("t", "mediaError", "msg", "That project could not be found (moved or deleted).")); return; }
        CrSend(D("t", "artDone", "msg", "Mixing \"" + e.Name + "\" for the video (your project is not changed)..."));
        new Thread(() =>
        {
            string err = null, outp = null;
            try { string mp3 = RenderSong(e); outp = Path.Combine(CreativeStore.Dir("media"), Safe(e.Name) + " (" + DateTime.Now.ToString("yyyy-MM-dd HHmm") + ")" + Path.GetExtension(mp3)); File.Copy(mp3, outp, true); try { File.Delete(mp3); } catch { } }
            catch (Exception ex) { err = ex.Message; }
            try { BeginInvoke(new Action(() =>
            {
                if (err != null) { CrSend(D("t", "mediaError", "msg", "Could not bring in \"" + e.Name + "\": " + err)); return; }
                var paths = new List<string> { outp }; try { if (!string.IsNullOrEmpty(e.Cover) && File.Exists(e.Cover)) { string cp = Path.Combine(CreativeStore.Dir("media"), Safe(e.Name) + " cover" + Path.GetExtension(e.Cover)); File.Copy(e.Cover, cp, true); paths.Add(cp); } } catch { }
                CrSend(D("t", "song", "artist", ProjectLibrary.ProfileName ?? "", "title", e.Name)); CrImportPaths(paths);
            })); } catch { }
        }) { IsBackground = true, Name = "creative-project" }.Start();
    }

    // ---- export: the editor renders each frame; ffmpeg encodes them with the mixed sound ----
    sealed class CrExport { public string Out, Tmp, Wav, Fmt, Quality; public int Abr, W, H; public double Fps, Dur; public Process P; public BlockingCollection<byte[]> Q; public Thread Writer; public string Err; public volatile bool Cancelled; public long Frames; }
    CrExport crEx;
    void CrExportStart(Dictionary<string, object> m)
    {
        if (crEx != null) throw new Exception("An export is already running.");
        string ff = StudioEnv.Ffmpeg(); if (ff == null) { CrSend(D("t", "exportError", "msg", "Exporting needs ffmpeg: convert something once in the converter (that installs it), then export again.")); return; }
        string fmt = HubApi.S(m, "fmt"); double dur = Num(m, "dur"), fps = Num(m, "fps"); int w = (int)Num(m, "w"), h = (int)Num(m, "h");
        if (dur <= 0 || fps < 1 || fps > 120 || w < 16 || h < 16 || w > 7680 || h > 4320) throw new Exception("Those export settings are not valid.");
        string ext = CreativeStore.Ext(fmt);
        using (var d = new SaveFileDialog { Title = "Export the video", FileName = Safe(HubApi.S(m, "name")) + ext, Filter = ext.TrimStart('.').ToUpperInvariant() + " file|*" + ext, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) })
        {
            string outName = CrAskSave(d); if (outName == null) { CrSend(D("t", "exportCancelled")); return; } d.FileName = outName;
            try { var drive = new DriveInfo(Path.GetPathRoot(d.FileName)); double need = fmt == "wav" ? dur * 48000 * 4 * 1.1 : dur * (w * h / 2073600.0) * 2.5e6; if (drive.AvailableFreeSpace < need + 200e6) throw new Exception("There is not enough free space on " + drive.Name + " for this export (about " + ((need + 200e6) / 1e9).ToString("0.0") + " GB needed)."); } catch (ArgumentException) { }
            string tmp = Path.Combine(Path.GetTempPath(), "purphex_export_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp);
            crEx = new CrExport { Out = d.FileName, Tmp = tmp, Wav = Path.Combine(tmp, "audio.wav"), Fmt = fmt, Quality = HubApi.S(m, "quality"), Abr = (int)Num(m, "abr"), W = w, H = h, Fps = fps, Dur = dur };
        }
        CrSend(D("t", "exportReady"));
    }
    static double Num(Dictionary<string, object> m, string k) { object v; return m.TryGetValue(k, out v) && v is double ? (double)v : 0; }
    // the frames and the sound arrive here (POST https://export.purphex/...)
    string CrUpload(string uri, byte[] body)
    {
        var u = new Uri(uri); string what = u.AbsolutePath.Trim('/');
        if (what == "art") return CrArtUpload(u, body);
        var ex = crEx; if (ex == null) return "No export is running.";
        if (ex.Err != null) return ex.Err; if (ex.Cancelled) return "cancelled";
        if (what == "audio") { File.WriteAllBytes(ex.Wav, body); if (ex.Fmt != "m4a" && ex.Fmt != "wav") CrStartEncoder(ex); return null; }
        if (what == "frame") { if (ex.Q == null) return "The encoder did not start."; if (body.Length < 100 || body[0] != 0xFF || body[1] != 0xD8) return "A frame was not a picture."; if (!ex.Q.TryAdd(body, 30000)) return ex.Err ?? "The encoder stopped taking frames."; ex.Frames++; return null; }
        return "Unknown upload.";
    }
    void CrStartEncoder(CrExport ex)
    {
        var psi = new ProcessStartInfo(StudioEnv.Ffmpeg(), CreativeStore.ExportArgs(ex.Fmt, ex.Quality, ex.Abr, ex.Fps, ex.Wav, ex.Out)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        ex.P = Process.Start(psi); var errText = new StringBuilder(); ex.P.ErrorDataReceived += (s, e) => { if (e.Data != null && errText.Length < 4000) errText.AppendLine(e.Data); }; ex.P.BeginErrorReadLine();
        ex.Q = new BlockingCollection<byte[]>(24);
        ex.Writer = new Thread(() =>
        {
            try { var st = ex.P.StandardInput.BaseStream; foreach (var f in ex.Q.GetConsumingEnumerable()) { if (ex.Cancelled) break; st.Write(f, 0, f.Length); } st.Flush(); st.Close(); }
            catch (Exception e2) { ex.Err = "The encoder stopped: " + (errText.Length > 0 ? errText.ToString().Trim().Split('\n').Last() : e2.Message); }
        }) { IsBackground = true, Name = "creative-export" };
        ex.Writer.Start();
    }
    void CrExportEnd()
    {
        var ex = crEx; if (ex == null) return;
        new Thread(() =>
        {
            string err = ex.Err, info = null;
            try
            {
                if (ex.Fmt == "m4a" || ex.Fmt == "wav") { int code; string o = CreativeStore.Run(StudioEnv.Ffmpeg(), CreativeStore.ExportArgs(ex.Fmt, ex.Quality, ex.Abr, ex.Fps, ex.Wav, ex.Out), 3600000, out code); if (code != 0) err = "ffmpeg could not write the file: " + o.Trim(); }
                else { ex.Q.CompleteAdding(); ex.Writer.Join(); if (!ex.P.WaitForExit(600000)) { try { ex.P.Kill(); } catch { } err = "The encoder took too long."; } else if (ex.P.ExitCode != 0 && err == null) err = "The encoder failed (code " + ex.P.ExitCode + ")."; }
                if (err == null) { bool ok; info = CreativeStore.Validate(StudioEnv.Ffprobe(), ex.Out, ex.Dur, ex.Fmt != "m4a" && ex.Fmt != "wav", out ok); if (!ok) { err = "The exported file did not check out: " + info; } }
            }
            catch (Exception e2) { err = e2.Message; }
            if (err != null) { try { File.Delete(ex.Out); } catch { } }
            try { Directory.Delete(ex.Tmp, true); } catch { }
            crEx = null; CrPost(err != null ? D("t", "exportError", "msg", err) : D("t", "exportDone", "path", ex.Out, "info", info));
        }) { IsBackground = true, Name = "creative-finish" }.Start();
    }
    void CrExportCancel(bool tell)
    {
        var ex = crEx; if (ex == null) return; ex.Cancelled = true; crEx = null;
        try { if (ex.Q != null) ex.Q.CompleteAdding(); } catch { } try { if (ex.P != null && !ex.P.HasExited) ex.P.Kill(); } catch { }
        ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(500); try { File.Delete(ex.Out); } catch { } try { Directory.Delete(ex.Tmp, true); } catch { } });
        if (tell) CrSend(D("t", "exportCancelled"));
    }

    // pictures for the photo editor: copied into its uploads folder (the originals are not touched) and listed in UPLOADS
    static readonly string[] ArtExts = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };
    void CrArtFiles(List<string> files)
    {
        int bad = 0;
        foreach (var f in files)
        {
            string ext = Path.GetExtension(f).ToLowerInvariant(); if (!File.Exists(f) || Array.IndexOf(ArtExts, ext) < 0 || new FileInfo(f).Length > 60L * 1024 * 1024) { bad++; continue; }
            string dst = Path.Combine(CreativeStore.Dir("art/uploads"), Guid.NewGuid().ToString("N").Substring(0, 12) + ext); File.Copy(f, dst); CrUploadName(Path.GetFileName(dst), Path.GetFileNameWithoutExtension(f));
            CrSend(D("t", "artImported", "url", "https://art.creative/uploads/" + Path.GetFileName(dst), "name", Path.GetFileNameWithoutExtension(f), "file", Path.GetFileName(dst)));
        }
        if (bad > 0) CrSend(D("t", "artDone", "msg", bad + " file(s) were not added: only PNG, JPEG, WebP, BMP and GIF pictures up to 60 MB.", "err", true));
    }
    static string UploadNames { get { return Path.Combine(CreativeStore.Dir("art/uploads"), "names.txt"); } }
    void CrUploadName(string file, string name)
    {
        var lines = File.Exists(UploadNames) ? File.ReadAllLines(UploadNames).Where(l => !l.StartsWith(file + "|")).ToList() : new List<string>(); if (name != null) lines.Add(file + "|" + name.Replace("|", "/").Replace("\n", " ")); File.WriteAllLines(UploadNames, lines);
    }
    List<object> CrUploadList()
    {
        var names = new Dictionary<string, string>(); if (File.Exists(UploadNames)) foreach (var l in File.ReadAllLines(UploadNames)) { int i = l.IndexOf('|'); if (i > 0) names[l.Substring(0, i)] = l.Substring(i + 1); }
        var list = new List<object>(); foreach (var f in new DirectoryInfo(CreativeStore.Dir("art/uploads")).GetFiles().Where(x => Array.IndexOf(ArtExts, x.Extension.ToLowerInvariant()) >= 0).OrderByDescending(x => x.LastWriteTimeUtc).Take(500)) { string n; list.Add(D("file", f.Name, "url", "https://art.creative/uploads/" + f.Name, "name", names.TryGetValue(f.Name, out n) ? n : Path.GetFileNameWithoutExtension(f.Name), "kb", (int)(f.Length / 1024))); }
        return list;
    }
    List<object> CrFontList() { var list = new List<object>(); foreach (var f in new DirectoryInfo(CreativeStore.Dir("art/fonts")).GetFiles()) { string e = f.Extension.ToLowerInvariant(); if (e == ".ttf" || e == ".otf" || e == ".woff" || e == ".woff2") list.Add(D("name", Path.GetFileNameWithoutExtension(f.Name).Replace("_", " "), "url", "https://art.creative/fonts/" + f.Name)); } return list; }
    string CrArtUpload(Uri u, byte[] body)
    {
        var q = HubApi.Query(u.Query.Length > 0 ? "?" + u.Query.TrimStart('?') : ""); string what = q.ContainsKey("what") ? q["what"] : "png", name = Safe(q.ContainsKey("name") ? q["name"] : "cover");
        if (body.Length < 100) return "The picture was empty."; bool png = body[0] == 0x89 && body[1] == 0x50, jpg = body[0] == 0xFF && body[1] == 0xD8, webp = body.Length > 12 && body[0] == 0x52 && body[1] == 0x49 && body[8] == 0x57 && body[9] == 0x45; if (!png && !jpg && !webp) return "That was not a picture.";
        BeginInvoke(new Action(() =>
        {
            try
            {
                if (what == "png" || what == "jpg" || what == "webp")
                {
                    using (var d = new SaveFileDialog { Title = "Export the artwork", FileName = name + (what == "jpg" ? ".jpg" : what == "webp" ? ".webp" : ".png"), Filter = what == "jpg" ? "JPEG picture|*.jpg" : what == "webp" ? "WebP picture|*.webp" : "PNG picture|*.png", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) })
                    { string op = CrAskSave(d); if (op != null) { File.WriteAllBytes(op, body); CrSend(D("t", "artDone", "msg", "Saved " + Path.GetFileName(op))); } }
                }
                else if (what == "daw")
                {
                    if (string.IsNullOrEmpty(projPath) || !File.Exists(projPath)) { CrSend(D("t", "artDone", "msg", "Save your song project in the DAW first: then the artwork can be its cover.", "err", true)); return; }
                    string tmp = Path.Combine(Path.GetTempPath(), "purphex_cover_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png"); File.WriteAllBytes(tmp, body); string err;
                    if (!ProjectLibrary.SetCover(projPath, tmp, out err)) CrSend(D("t", "artDone", "msg", "Could not set the cover: " + err, "err", true)); else { ProjectIO.CoverBytes = ProjectLibrary.MakeSquare(ProjectLibrary.Find(projPath).Cover, 256); CrSend(D("t", "artDone", "msg", "It is now the cover of \"" + projName + "\".")); }
                    try { File.Delete(tmp); } catch { }
                }
                else if (what == "video") { string p = Path.Combine(CreativeStore.Dir("media"), name + " " + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"); File.WriteAllBytes(p, body); CrImportPaths(new List<string> { p }); CrSend(D("t", "artDone", "msg", "Added to Video Studio's MEDIA.")); }
            }
            catch (Exception ex) { CrSend(D("t", "artDone", "msg", ex.Message, "err", true)); }
        }));
        return null;
    }

    // ---- the page in the studio ----
    void PaintCreative(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + 4; crR = Rc(6, top, W - 12, H - top - 6);
        FillRR(g, TM.C(255, 10, 5, 24), crR, 10); Win.DrawNeonBox(g, crR, false); Reg(crR, () => { });
        if (crBObj == null || !CrB.Ready) Txt(g, crBObj != null && CrB.Error.Length > 0 ? CrB.Error : "STARTING CREATIVE STUDIO...", 14, crBObj != null && CrB.Error.Length > 0 ? NeonPink : DimC, crR, StringAlignment.Center, true);
    }
    void CreativeTick()
    {
        if (crBObj == null) return; bool show = tab == 10 && !OverlayOpen && !busy && crR.Width > 100; CrB.SetBounds(Rectangle.Round(Rc(crR.X + 2, crR.Y + 2, crR.Width - 4, crR.Height - 4)), show);
        if (eng.Playing && crPlaying) { CrSend("{\"t\":\"dawPlaying\"}"); crPlaying = false; }
    }
}
