using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Preset browser: categories on the left, presets on the right, and buttons to save, rename, duplicate, delete and initialise.
partial class SynthCanvas
{
    internal void TourTab(int t) { tab = t; Invalidate(); }
    // where a part of the screen (in the 1240 x 716 layout) really is, in this control
    internal Rectangle ToClient(RectangleF r) { return new Rectangle((int)(ox + r.X * scale), (int)(oy + r.Y * scale), (int)(r.Width * scale), (int)(r.Height * scale)); }
    bool browseOpen; string browseCat = ""; float browseScroll; string browseMsg = ""; int soundTab; float menuT; bool draggingOut;   // soundTab: 0 built-in, 1 my sounds, 2 community
    List<CommPreset> commList; bool commLoading; string commErr = ""; int commGen;
    // the sounds other people shared (fetched in the background; shown under the Community category)
    void CommFetch()
    {
        int gen = ++commGen; commLoading = true; commErr = "";
        System.Threading.ThreadPool.QueueUserWorkItem(o =>
        {
            List<CommPreset> l = null; string err = ""; try { l = CommunityPresets.Fetch("synth", "popular"); } catch { err = "Could not reach the Purphex community. Check your internet connection."; }
            try { BeginInvoke(new Action(() => { if (gen != commGen) return; commLoading = false; commList = l ?? new List<CommPreset>(); commErr = err; Poke(); })); } catch { }
        });
    }
    void PresetMenu(PointF pt)
    {
        browseOpen = true; browseScroll = 0; browseMsg = ""; browseCat = ""; soundTab = SynthPresets.CategoryOf(fx.PresetName) == "My presets" ? 1 : 0; Poke();
    }
    void CloseSounds() { browseOpen = false; Poke(); }

    string AskName(string prompt, string def)
    {
        Typing = true; try { return StudioDialogs.Ask(FindForm(), prompt, def); } finally { Typing = false; Poke(); }
    }

    class SoundRow { public string Name, Head; public CommPreset Comm; }
    List<SoundRow> SoundRows()
    {
        var l = new List<SoundRow>();
        if (soundTab == 0) { foreach (var c in SynthPresets.Categories) { if (browseCat.Length > 0 && c != browseCat) continue; var n = SynthPresets.InCategory(c); if (n.Count == 0) continue; if (browseCat.Length == 0) l.Add(new SoundRow { Head = c.ToUpperInvariant() }); foreach (var s in n) l.Add(new SoundRow { Name = s }); } }
        else if (soundTab == 1) { foreach (var s in SynthPresets.UserNames()) l.Add(new SoundRow { Name = s }); }
        else if (commList != null) { foreach (var cp in commList) l.Add(new SoundRow { Name = cp.Name, Comm = cp }); }
        return l;
    }
    string SoundData(SoundRow r) { return r.Comm != null ? r.Comm.Data : (soundTab == 1 ? SynthPresets.UserData(r.Name) : SynthPresets.DataOf(r.Name)); }

    // YOUR SOUNDS: a menu that slides in from the left. Tabs: the sounds built into the DAW, the sounds you saved (kept until you delete them), and the community's.
    void PaintPresetBrowser(Graphics g)
    {
        float goal = browseOpen ? 1 : 0; if (Prefs.PerfMode) menuT = goal; else if (Math.Abs(menuT - goal) > 0.004f) { menuT += (goal - menuT) * 0.38f; if (Math.Abs(menuT - goal) < 0.01f) menuT = goal; lastInput = DateTime.Now; }
        if (menuT <= 0.004f && !browseOpen) return;
        float pw = 440, top = 62, px0 = -pw - 14 + (pw + 14) * menuT; var pan = Rc(px0, top, pw, H - top - 8);
        var all = Rc(0, 0, W, H); using (var b = new SolidBrush(Color.FromArgb((int)(120 * menuT), 5, 2, 12))) g.FillRectangle(b, all);
        if (browseOpen) { var blocker = Add(all); blocker.Down = pt => { if (!pan.Contains(pt)) CloseSounds(); }; }
        Fill(g, TM.C(250, 22, 12, 42), pan, 14); for (int k = 3; k >= 1; k--) Stroke(g, Color.FromArgb(14 * k, Acc), Rc(pan.X - k, pan.Y - k, pan.Width + 2 * k, pan.Height + 2 * k), 14 + k, 2f); Stroke(g, Acc, pan, 14, 1.5f);
        Add(pan);
        float x0 = pan.X + 18, iw = pan.Width - 36;
        Txt(g, "YOUR SOUNDS", 20, Ink, Rc(x0, pan.Y + 10, 260, 28), StringAlignment.Near, true);
        Txt(g, "(drag n drop)   playing: " + (modified ? "* " : "") + fx.PresetName, 11, Dim, Rc(x0, pan.Y + 38, iw - 50, 18), StringAlignment.Near, false);
        var close = Rc(pan.Right - 62, pan.Y + 12, 44, 28); Fill(g, Hot(close) ? PanelHi : Panel, close, 6); Stroke(g, Edge, close, 6, 1); Txt(g, "X", 13, Ink, close, StringAlignment.Center, true); Add(close).Down = pt => CloseSounds();
        // the three tabs
        string[] tn = { "BUILT-IN", "MY SOUNDS", "COMMUNITY" }; int[] tc = { SynthPresets.Names == null ? 0 : SynthPresets.Names.Length, SynthPresets.UserNames().Count, commList == null ? 0 : commList.Count };
        float tw = (iw - 12) / 3f, ty = pan.Y + 64;
        for (int i = 0; i < 3; i++)
        {
            int k = i; var r = Rc(x0 + i * (tw + 6), ty, tw, 32); bool sel = soundTab == i; Fill(g, sel ? PanelHi : Panel, r, 7); Stroke(g, sel ? Acc : Edge, r, 7, sel ? 2 : 1);
            Txt(g, tn[i] + (i == 2 && commList == null ? "" : "  " + tc[i]), 11, sel ? Ink : Dim, r, StringAlignment.Center, sel);
            var rg = Add(r); rg.Tip = i == 0 ? "The sounds that come with the DAW" : (i == 1 ? "The sounds you saved: they stay until you delete them" : "Sounds other people published"); rg.Down = pt => { soundTab = k; browseScroll = 0; browseMsg = ""; if (k == 2 && commList == null && !commLoading) CommFetch(); else if (k == 2) CommFetch(); };
        }
        float cy = ty + 42;
        if (soundTab == 0)
        {   // the categories (they wrap onto more lines)
            string[] cats = new string[SynthPresets.Categories.Length + 1]; cats[0] = ""; Array.Copy(SynthPresets.Categories, 0, cats, 1, SynthPresets.Categories.Length); float cx = x0;
            foreach (var c0 in cats)
            {
                string c = c0; string lab = c.Length == 0 ? "All" : c; float cw = 20 + lab.Length * 7f; if (cx + cw > x0 + iw) { cx = x0; cy += 28; }
                var r = Rc(cx, cy, cw, 24); bool sel = browseCat == c; Fill(g, sel ? PanelHi : (Hot(r) ? TM.C(255, 36, 20, 66) : Panel), r, 12); Stroke(g, sel ? Acc : Edge, r, 12, sel ? 1.8f : 1); Txt(g, lab, 11, sel ? Ink : Dim, r, StringAlignment.Center, sel);
                var rg = Add(r); rg.Down = pt => { browseCat = c; browseScroll = 0; }; cx += cw + 5;
            }
            cy += 32;
        }
        float footH = 158; var list = Rc(x0, cy, iw, pan.Bottom - footH - cy - 6); Fill(g, TM.C(235, 14, 7, 32), list, 8); Stroke(g, Edge, list, 8, 1);
        var rows = SoundRows(); float total = 8; foreach (var rw in rows) total += rw.Head != null ? 26 : 36;
        float maxScroll = Math.Max(0, total - list.Height); browseScroll = Math.Max(0, Math.Min(maxScroll, browseScroll));
        var lw = Add(list); lw.Wheel = d => { browseScroll = Math.Max(0, Math.Min(maxScroll, browseScroll - d * 36)); };
        bool lbar = maxScroll > 0 && list.Height > 130; float rowW = list.Width - (lbar ? 62 : 16); var inner = Rc(list.X + 2, list.Y + 2, list.Width - 4, list.Height - 4);
        var gs = g.Save(); g.SetClip(inner, System.Drawing.Drawing2D.CombineMode.Intersect);
        if (rows.Count == 0) Txt(g, soundTab == 2 ? (commLoading || commList == null ? "Loading the community..." : (commErr.Length > 0 ? commErr : "Nobody has published a Bass Lab sound yet. Be the first: press PUBLISH SOUNDS TO COMMUNITY.")) : (soundTab == 1 ? "You have not saved a sound yet. Make one, then press SAVE CURRENT SOUND. You can also drop a .bsound file anywhere on Bass Lab." : "Nothing here."), 12, Dim, Rc(list.X + 14, list.Y + 12, list.Width - 28, 80), StringAlignment.Near, false);
        float yy = list.Y + 6 - browseScroll;
        foreach (var row in rows)
        {
            if (row.Head != null) { if (yy + 24 > list.Y && yy < list.Bottom) Txt(g, row.Head, 10, Acc, Rc(list.X + 12, yy + 6, 300, 16), StringAlignment.Near, true); yy += 26; continue; }
            var r = Rc(list.X + 8, yy, rowW, 32); yy += 36; if (r.Bottom < list.Y || r.Y > list.Bottom) continue; var vis = RectangleF.Intersect(r, inner); if (vis.Width < 4 || vis.Height < 4) continue;
            string n = row.Name; var rowC = row; bool cur = n == fx.PresetName && row.Comm == null, hot = Hot(vis);
            Fill(g, cur ? TM.C(150, 90, 50, 170) : (hot ? TM.C(150, 60, 34, 110) : TM.C(120, 44, 24, 80)), r, 7); if (cur) Stroke(g, Acc2, r, 7, 1.5f);
            if (row.Comm != null) { Txt(g, n, 13, Ink, Rc(r.X + 12, r.Y, r.Width * 0.5f, r.Height), StringAlignment.Near, false); Txt(g, "BY " + row.Comm.Author + "   " + row.Comm.Uses + (row.Comm.Uses == 1 ? " use" : " uses"), 10, Dim, Rc(r.X + r.Width * 0.5f, r.Y, r.Width * 0.5f - 10, r.Height), StringAlignment.Far, false); }
            else Txt(g, n, 13, Ink, Rc(r.X + 12, r.Y, r.Width - 24 - (soundTab == 1 ? 30 : 0), r.Height), StringAlignment.Near, cur);
            var rg = Add(vis); rg.Tip = row.Comm != null ? "Click to use " + n : "Click to load " + n;
            rg.Down = pt =>
            {
                if (rowC.Comm != null)
                {
                    if (!CommunityPresets.Safe("synth", rowC.Comm.Data)) { browseMsg = "That sound is not valid."; return; }
                    try { SynthPresets.ApplyData(fx, rowC.Comm.Name, rowC.Comm.Data); modified = false; browseMsg = "Loaded " + rowC.Comm.Name + " by " + rowC.Comm.Author + ". Press SAVE CURRENT SOUND to keep your own copy."; CommunityPresets.Tell("use", rowC.Comm.Id); } catch (Exception ex) { browseMsg = "Could not load it: " + ex.Message; }
                }
                else { SynthPresets.Apply(fx, n); modified = false; browseMsg = "Loaded " + n; }
            };
            rg.Drag = (dx, dy) => { if (Math.Abs(dx) > 22 || Math.Abs(dy) > 22) DragOut(n, SoundData(rowC)); };   // drag the sound out of the window: it is saved as a file where it is dropped
            if (soundTab == 1)
            {
                var del = Rc(r.Right - 30, r.Y + 5, 24, 22); if (RectangleF.Intersect(del, inner) == del) { Fill(g, Hot(del) ? TM.C(255, 190, 50, 80) : TM.C(255, 120, 30, 62), del, 5); Txt(g, "x", 12, Ink, del, StringAlignment.Center, true); var dg = Add(del); dg.Tip = "Delete " + n + " (only this sound)"; dg.Down = pt => DeleteSound(n); }
            }
        }
        g.Restore(gs); if (lbar) LogoBar(g, Rc(list.Right - 50, list.Y + 4, 42, list.Height - 8), total, list.Height - 8, () => browseScroll, v => browseScroll = v);
        // the buttons
        float by = list.Bottom + 10, bx = x0; bool isUser = SynthPresets.UserNames().Contains(fx.PresetName);
        bx = Action(g, "SAVE CURRENT SOUND...", bx, by, 206, true, "Keep the sound you are playing under a name. It stays in MY SOUNDS until you delete it.", () => SaveCurrentSound());
        bx = Action(g, "DUPLICATE", bx, by, 96, true, "Make a copy you can change.", () => { try { string n2 = SynthPresets.DuplicateUser(fx.PresetName); SynthPresets.Apply(fx, n2); modified = false; soundTab = 1; browseMsg = "Made " + n2; } catch (Exception ex) { browseMsg = ex.Message; } });
        bx = Action(g, "RENAME", bx, by, 90, isUser, "Rename the sound you are playing (one of yours).", () => { string s = AskName("New name:", fx.PresetName); if (s == null) return; try { string old = fx.PresetName; SynthPresets.RenameUser(old, s); fx.PresetName = s.Trim(); browseMsg = "Renamed to " + s.Trim(); } catch (Exception ex) { browseMsg = ex.Message; } });
        by += 36; bx = x0;
        bx = Action(g, "START FROM SCRATCH", bx, by, 160, true, "Reset everything to a blank, plain sound to build your own from.", () => { SynthPresets.Initialise(fx); modified = false; browseMsg = "A blank sound: build yours from here."; });
        bx = Action(g, "EXPORT...", bx, by, 96, true, "Save the sound you are playing as a .bsound file (to share it or keep a copy).", () => ExportSound());
        bx = Action(g, "IMPORT...", bx, by, 96, true, "Add .bsound files to MY SOUNDS.", () => ImportSounds());
        {   // the sound itself onto the song: a glowing circle you drag onto the studio
            var dr = Rc(bx, by - 2, Math.Max(120, x0 + iw - bx), 34); bool dh = Hot(dr); DragOrb(g, dr.X + 17, dr.Y + 17, 30, Acc2, dh); Txt(g, "DRAG TO SONG", 11, dh ? Ink : Dim, Rc(dr.X + 38, dr.Y, dr.Width - 38, dr.Height), StringAlignment.Near, true);
            var drg = Add(dr); drg.Tip = "Drag this onto the studio: the sound you are playing goes on the song as an audio clip (one note, 3 seconds)"; drg.Down = pt => DragSoundToSong();
        }
        var pub = Rc(x0, by + 36, iw, 34); bool phot = Hot(pub); Fill(g, phot ? TM.C(255, 170, 60, 200) : TM.C(255, 130, 44, 170), pub, 8); Stroke(g, Acc2, pub, 8, 1.6f); Txt(g, "PUBLISH SOUNDS TO COMMUNITY", 13, Ink, pub, StringAlignment.Center, true);
        { var pr = Add(pub); pr.Tip = "Publish a sound for everyone to use (you pick which)."; pr.Down = pt => PublishMenu(); }
        Txt(g, browseMsg, 11, Good, Rc(x0, by + 76, iw, 16), StringAlignment.Near, true);
        Txt(g, "Drop a .bsound file on Bass Lab to add it to MY SOUNDS.", 10, Dim, Rc(x0, by + 94, iw, 14), StringAlignment.Near, false); Txt(g, "Drag a sound out of the list to keep it as a file.", 10, Dim, Rc(x0, by + 108, iw, 14), StringAlignment.Near, false);
    }

    void SaveCurrentSound()
    {
        string s = AskName("Name your sound:", SynthPresets.IsFactory(fx.PresetName) ? "My " + fx.PresetName : fx.PresetName); if (s == null) return;
        try
        {
            if (SynthPresets.UserNames().Contains(s.Trim()) && MessageBox.Show(this, "\"" + s.Trim() + "\" exists. Replace it?", "Save sound", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            SynthPresets.SaveUser(fx, s); modified = false; soundTab = 1; browseMsg = "Saved " + s.Trim() + ". It stays in MY SOUNDS until you delete it.";
            CommunityPresets.AutoShare("synth", s.Trim(), fx.Serialize(), ProjectLibrary.ProfileName, msg => { if (msg != null) { try { BeginInvoke(new Action(() => { browseMsg = msg; Poke(); })); } catch { } } });
        }
        catch (Exception ex) { browseMsg = ex.Message; }
    }
    void DeleteSound(string n)
    {
        if (MessageBox.Show(this, "Delete the sound \"" + n + "\"? (Only this one.)", "Delete sound", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { SynthPresets.DeleteUser(n); browseMsg = "Deleted " + n; if (fx.PresetName == n) fx.PresetName = "Init"; } catch (Exception ex) { browseMsg = ex.Message; }
        Poke();
    }

    // PUBLISH SOUNDS TO COMMUNITY: the sound you are playing, one of yours, or all of yours
    void PublishMenu()
    {
        var m = Win.NewMenu(); var mine = SynthPresets.UserNames();
        m.Items.Add("The sound I am playing now: " + fx.PresetName).Click += (a, b) => PublishSounds(new[] { fx.PresetName }, new[] { fx.Serialize() });
        if (mine.Count > 0)
        {
            m.Items.Add(new ToolStripSeparator());
            foreach (var n0 in mine) { string n = n0; m.Items.Add(n).Click += (a, b) => PublishSounds(new[] { n }, new[] { SynthPresets.UserData(n) }); }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("All my sounds (" + mine.Count + ")").Click += (a, b) => { var d = new string[mine.Count]; for (int i = 0; i < d.Length; i++) d[i] = SynthPresets.UserData(mine[i]); PublishSounds(mine.ToArray(), d); };
        }
        Typing = true; m.Closed += (a, b) => { Typing = false; Poke(); }; m.Show(this, PointToClient(Cursor.Position));
    }
    void PublishSounds(string[] names, string[] data)
    {
        if (MessageBox.Show(this, (names.Length == 1 ? "Publish \"" + names[0] + "\"" : "Publish " + names.Length + " sounds") + " to the community?\n\nEveryone can see and use " + (names.Length == 1 ? "it" : "them") + ", with your artist name on " + (names.Length == 1 ? "it" : "them") + ".", "Publish sounds", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string who = ProjectLibrary.ProfileName; if (string.IsNullOrWhiteSpace(who)) who = "Producer"; browseMsg = "Publishing..."; Poke();
        System.Threading.ThreadPool.QueueUserWorkItem(o =>
        {
            int ok = 0; string err = null;
            for (int i = 0; i < names.Length; i++) { if (data[i] == null || !CommunityPresets.Safe("synth", data[i])) { err = "\"" + names[i] + "\" is too big or has something that cannot be shared."; continue; } string e2 = CommunityPresets.Share("synth", names[i], who, data[i]); if (e2 == null) ok++; else err = e2; }
            try { BeginInvoke(new Action(() => { browseMsg = ok > 0 ? "Published " + (ok == 1 ? "1 sound" : ok + " sounds") + ". Find " + (ok == 1 ? "it" : "them") + " under COMMUNITY." + (err != null ? " (" + err + ")" : "") : (err ?? "Nothing was published."); if (ok > 0) { soundTab = 2; CommFetch(); } Poke(); })); } catch { }
        });
    }

    // EXPORT / IMPORT: .bsound files
    void ExportSound()
    {
        using (var d = new SaveFileDialog { Title = "Export the sound", Filter = "Bass Lab sound|*" + SynthPresets.FileExt, FileName = (fx.PresetName == "Init" ? "My sound" : fx.PresetName) + SynthPresets.FileExt })
        {
            Typing = true; try { if (d.ShowDialog(FindForm()) != DialogResult.OK) return; System.IO.File.Copy(SynthPresets.ExportFile(fx.PresetName, fx.Serialize()), d.FileName, true); browseMsg = "Exported " + System.IO.Path.GetFileName(d.FileName); }
            catch (Exception ex) { browseMsg = ex.Message; } finally { Typing = false; Poke(); }
        }
    }
    void ImportSounds()
    {
        using (var d = new OpenFileDialog { Title = "Import sounds", Filter = "Bass Lab sounds|*" + SynthPresets.FileExt + ";*.txt", Multiselect = true })
        { Typing = true; try { if (d.ShowDialog(FindForm()) != DialogResult.OK) return; browseMsg = DropSoundFiles(d.FileNames); soundTab = 1; browseScroll = 0; } finally { Typing = false; Poke(); } }
    }
    // the sound you are playing as audio: one note (held 2 s, then let go) rendered to a WAV and dragged out (drop it on the studio)
    internal static string RenderNote(SynthFx src, int note, string dir)
    {
        var f2 = new SynthFx(); f2.Prepare(44100); SynthPresets.ApplyData(f2, src.PresetName, src.Serialize());
        int frames = 44100 * 3, off = 44100 * 2, pos = 0; var l = new float[frames]; var r = new float[frames]; f2.NoteOn(note, 110);
        while (pos < frames) { int c = Math.Min(1024, frames - pos); var bl = new float[c]; var br = new float[c]; if (pos <= off && off < pos + c) f2.NoteOff(note); f2.BeginBlock(null, pos, c, false); f2.Process(bl, br, c); Array.Copy(bl, 0, l, pos, c); Array.Copy(br, 0, r, pos, c); pos += c; }
        System.IO.Directory.CreateDirectory(dir); string name = (src.PresetName == "Init" ? "Bass Lab" : src.PresetName); foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(ch, '-');
        string p = System.IO.Path.Combine(dir, name + " " + DateTime.Now.ToString("HH-mm-ss") + ".wav"); WavIO.Write(p, l, r, 44100, 24); return p;
    }
    void DragSoundToSong()
    {
        if (draggingOut) return; draggingOut = true; active = null;
        try
        {
            string dir = StudioEnv.UnderTest ? System.IO.Path.Combine(StudioEnv.DataDir, "basslab") : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Bass Lab");
            string p = RenderNote(fx, 48, dir); DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { p }), DragDropEffects.Copy); browseMsg = "Your sound is " + System.IO.Path.GetFileName(p) + " (in Music / yt2mp3 / Bass Lab)";
        }
        catch (Exception ex) { browseMsg = ex.Message; }
        finally { draggingOut = false; Poke(); }
    }
    // ---- drag and drop ----
    void DragOut(string name, string data)
    {
        if (draggingOut || data == null) return; draggingOut = true; active = null;
        try { string p = SynthPresets.ExportFile(name, data); DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { p }), DragDropEffects.Copy); browseMsg = "Dragged out as " + System.IO.Path.GetFileName(p); }
        catch (Exception ex) { browseMsg = ex.Message; }
        finally { draggingOut = false; Poke(); }
    }
    void SoundDragEnter(object s, DragEventArgs e) { e.Effect = (!draggingOut && e.Data.GetDataPresent(DataFormats.FileDrop)) ? DragDropEffects.Copy : DragDropEffects.None; }
    void SoundDragDrop(object s, DragEventArgs e)
    {
        if (draggingOut) return; var files = e.Data.GetData(DataFormats.FileDrop) as string[]; if (files == null) return;
        string msg = DropSoundFiles(files); browseOpen = true; soundTab = 1; browseScroll = 0; browseMsg = msg; Poke();
    }
    // files dropped on Bass Lab: sound files are added to MY SOUNDS (returns the sentence to show)
    internal string DropSoundFiles(string[] files)
    {
        int ok = 0; string last = "", bad = ""; bool audio = false;
        foreach (var f in files)
        {
            string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
            if (ext == SynthPresets.FileExt || ext == ".txt") { try { last = SynthPresets.Import(System.IO.Path.GetFileNameWithoutExtension(f), System.IO.File.ReadAllText(f)); ok++; } catch (Exception ex) { bad = ex.Message; } }
            else if (ext == ".wav" || ext == ".mp3" || ext == ".flac" || ext == ".ogg" || ext == ".m4a" || ext == ".aiff" || ext == ".aif") audio = true;
            else bad = "That is not a Bass Lab sound file.";
        }
        return ok > 0 ? "Added " + (ok == 1 ? "\"" + last + "\"" : ok + " sounds") + " to MY SOUNDS." : (audio ? "Bass Lab makes its own sound, so audio files cannot be used here. Drop a .bsound file (a sound saved from Bass Lab)." : (bad.Length > 0 ? bad : "Nothing was added."));
    }

    float Action(Graphics g, string label, float x, float y, float w, bool enabled, string tip, Action a)
    {
        var r = Rc(x, y, w, 30); Fill(g, enabled ? (Hot(r) ? PanelHi : Panel) : TM.C(255, 14, 8, 26), r, 6); Stroke(g, enabled ? Acc : TM.C(255, 60, 60, 90), r, 6, 1);
        Txt(g, label, 11, enabled ? Ink : TM.C(255, 90, 80, 120), r, StringAlignment.Center, true);
        if (enabled) { var rg = Add(r); rg.Tip = tip; rg.Down = pt => a(); }
        return x + w + 8;
    }

    // the same scroll bar as in Purphex Music: black, arrows at both ends, and the Purphex logo that rides along the bar and is held and dragged to scroll
    void LogoBar(Graphics g, RectangleF bar, float contentH, float viewH, Func<float> get, Action<float> set)
    {
        if (contentH <= viewH + 1 || bar.Height < 120) return; float max = contentH - viewH; Fill(g, Color.Black, bar, 12); Stroke(g, TM.C(160, 140, 85, 210), bar, 12, 1.3f);
        const float cap = 30; float cx = bar.X + bar.Width / 2, lh = Math.Min(13f, bar.Width / 2 - 3), lhw = lh * 1.16f, pad = lh + 3; var up = Rc(bar.X, bar.Y, bar.Width, cap); var dn = Rc(bar.X, bar.Bottom - cap, bar.Width, cap); var inner = Rc(bar.X + 3, bar.Y + cap, bar.Width - 6, bar.Height - 2 * cap); float trav = Math.Max(1f, inner.Height - 2 * pad);
        using (var pen = new Pen(TM.C(220, 0, 229, 255), 2.1f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round, StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(cx - 6, up.Y + 17 + k * 6), new PointF(cx, up.Y + 11 + k * 6), new PointF(cx + 6, up.Y + 17 + k * 6) });
        using (var pen = new Pen(TM.C(220, 255, 43, 214), 2.1f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round, StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round }) for (int k = 0; k < 2; k++) g.DrawLines(pen, new[] { new PointF(cx - 6, dn.Bottom - 17 - k * 6), new PointF(cx, dn.Bottom - 11 - k * 6), new PointF(cx + 6, dn.Bottom - 17 - k * 6) });
        float frac = max <= 0 ? 0 : Math.Max(0, Math.Min(1, get() / max)), lcy = inner.Y + pad + frac * trav; using (var pen = new Pen(TM.C(60, 140, 85, 210), 2f)) g.DrawLine(pen, cx, inner.Y + 2, cx, inner.Bottom - 2);
        var hex = new[] { new PointF(cx - lhw, lcy), new PointF(cx - lhw / 2, lcy - lh), new PointF(cx + lhw / 2, lcy - lh), new PointF(cx + lhw, lcy), new PointF(cx + lhw / 2, lcy + lh), new PointF(cx - lhw / 2, lcy + lh) };
        using (var br = new SolidBrush(TM.C(245, 8, 4, 22))) g.FillPolygon(br, hex); using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(cx - lhw, lcy - lh, lhw * 2, lh * 2), TM.C(255, 0, 229, 255), TM.C(255, 255, 43, 214), 45f)) using (var pen = new Pen(br, 2f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) g.DrawPolygon(pen, hex);
        using (var pen = new Pen(TM.C(255, 0, 229, 255), 1.4f)) g.DrawLines(pen, new[] { new PointF(cx - lhw * 0.6f, lcy), new PointF(cx - lhw * 0.3f, lcy - lh * 0.3f), new PointF(cx, lcy + lh * 0.45f), new PointF(cx + lhw * 0.3f, lcy - lh * 0.2f), new PointF(cx + lhw * 0.6f, lcy) });
        Action<float> jump = my => set(Math.Max(0, Math.Min(max, (my - inner.Y - pad) / trav * max)));
        var rg = Add(inner); rg.Down = pt => jump(pt.Y); rg.Drag = (dx, dy) => jump(mouse.Y);
        var ru = Add(up); ru.Down = pt => set(Math.Max(0, get() - 60)); var rd = Add(dn); rd.Down = pt => set(Math.Min(max, get() + 60));
        float downY = 0, downS = 0; var lg = Add(Rc(cx - lhw - 3, lcy - lh - 3, lhw * 2 + 6, lh * 2 + 6)); lg.Tip = "Hold the logo and drag it to scroll the list"; lg.Down = pt => { downY = mouse.Y; downS = get(); }; lg.Drag = (dx, dy) => set(Math.Max(0, Math.Min(max, downS + (mouse.Y - downY) / trav * max)));
    }
}
