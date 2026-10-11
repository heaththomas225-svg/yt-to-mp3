using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// PURPHEX SOUNDS in the studio: a browser panel (search, filters, preview, drag onto the timeline) and the pack manager.
partial class StudioForm
{
    bool soundsOpen, packsOpen, soundsLoaded; TextBox soundBox; readonly SoundLibrary.Filter sndFilter = new SoundLibrary.Filter(); List<SoundItem> sndResults = new List<SoundItem>(); SoundItem sndSel; float sndScroll; bool sndDirty = true; string sndText = "";
    SoundItem sndDrag; PointF sndDragFrom; bool sndDragging; Size soundLayout;
    // pack manager and installing
    readonly Queue<string> installQ = new Queue<string>(); string installing = ""; volatile float installPct; volatile string installNote = ""; volatile bool installCancel; string packSection = "All"; float packScroll; string packMsg = ""; readonly Dictionary<string, long> packBytes = new Dictionary<string, long>();

    void OpenSounds()
    {
        if (!soundsLoaded) { soundsLoaded = true; SoundLibrary.Load(); }
        tab = 0; soundsOpen = true; sndDirty = true; EnsureSoundBox();
    }
    void CloseSounds() { soundsOpen = false; Auditioner.Stop(); }
    void EnsureSoundBox()
    {
        if (soundBox != null) return;
        soundBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 11f) }; Controls.Add(soundBox); Win.DarkControl(soundBox);
        soundBox.TextChanged += (s, e) => { sndFilter.Text = soundBox.Text.Trim(); sndDirty = true; sndScroll = 0; Invalidate(); };
    }
    void RefreshSounds()
    {
        sndResults = SoundLibrary.Search(sndFilter); sndDirty = false; if (sndSel != null && !sndResults.Contains(sndSel)) { /* keep showing the selected sound even if the filter hides it */ }
    }

    // ---- adding a sound to the song ----
    static int GroupColour(string g) { switch (g) { case "Drums": return unchecked((int)0xFFFF9A3D); case "Bass": return unchecked((int)0xFF9B6BFF); case "Synth": return unchecked((int)0xFF3DD9FF); case "FX": return unchecked((int)0xFFFF5CC8); case "Vocal": return unchecked((int)0xFFFF6BA0); case "Loops": return unchecked((int)0xFFFFD23D); default: return unchecked((int)0xFF6BFF9A); } }

    // returns null if it worked, or a message
    string AddSample(SoundItem s, long frame, int insertAt)
    {
        if (s == null) return "Pick a sound first."; if (!s.Exists) return "This file is missing (" + s.Rel + "). Reinstall the pack in the pack manager."; if (busy) return "Wait for the current job to finish.";
        if (s.Kind == "preset") return InstallPreset(s);
        if (s.Kind == "midi") return DropMidi(s, frame, insertAt);
        float[] l, r; int sr; string fmt; if (!SoundIO.Read(s.Path, out l, out r, out sr, out fmt)) return "Couldn't read " + s.Rel + ".";
        if (sr != StudioEngine.SR && sr > 0) { l = Resample(l, sr); r = Resample(r, sr); }
        var clip = AudioClip.FromArrays(l, r, s.Name); clip.Path = s.Path; var t = StudioEngine.NewTrack(s.Name, "audio", clip); t.Color = GroupColour(s.Group); t.Offset = Math.Max(0, frame);
        bool first = eng.Tracks.Count == 0; if (first && s.Bpm > 0) Fx.ProjectBpm = s.Bpm; nextInsert = insertAt; PlaceDropped(t); SoundLibrary.MarkUsed(s);
        if (!first && s.Bpm > 0 && Math.Abs(s.Bpm - Fx.ProjectBpm) > 1.5) Toast("Added. This loop is " + s.Bpm.ToString("0") + " BPM and your project is " + Fx.ProjectBpm.ToString("0") + " BPM."); else Toast("Added " + s.Name);
        return null;
    }
    static float[] Resample(float[] x, int from) { int n = (int)((long)x.Length * StudioEngine.SR / from); var y = new float[n]; double st = from / (double)StudioEngine.SR; for (int i = 0; i < n; i++) { double p = i * st; int a = (int)p; float f = (float)(p - a); float v0 = a < x.Length ? x[a] : 0, v1 = a + 1 < x.Length ? x[a + 1] : v0; y[i] = v0 + (v1 - v0) * f; } return y; }

    // a MIDI file dropped on an instrument track puts its notes there
    string DropMidi(SoundItem s, long frame, int laneIndex)
    {
        Track t = laneIndex >= 0 && laneIndex < eng.Tracks.Count ? eng.Tracks[laneIndex] : Selected; if (t == null || !t.Instrument) return "MIDI needs an instrument track: add one with + ADD TRACK > MIDI Track (for example Bass Lab) and drop the file on its lane.";
        double bpm; var notes = SoundIO.ReadMidi(s.Path, out bpm); if (notes.Count == 0) return "That MIDI file has no notes."; double spb = 60.0 / Fx.ProjectBpm * StudioEngine.SR; PushUndo();
        lock (t.Midi) { foreach (var n in notes) { long on = frame + (long)(n[0] * spb), off = frame + (long)((n[0] + n[1]) * spb); t.Midi.Add(new MidiEv { Frame = on, S = 0x90, D1 = (byte)n[2], D2 = (byte)Math.Max(1, Math.Min(127, n[3])) }); t.Midi.Add(new MidiEv { Frame = Math.Max(on + 1, off), S = 0x80, D1 = (byte)n[2], D2 = 0 }); } t.Midi.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
        selTrack = eng.Tracks.IndexOf(t); SoundLibrary.MarkUsed(s); Toast("Added " + notes.Count + " notes to " + t.Name); return null;
    }
    string InstallPreset(SoundItem s)
    {
        try { Directory.CreateDirectory(SynthPresets.Dir); string name = s.Name; string dest = Path.Combine(SynthPresets.Dir, PackWriter.Clean(name) + ".txt"); File.Copy(s.Path, dest, true); SoundLibrary.MarkUsed(s); Toast("Added to Bass Lab: open its preset browser > My presets > " + name); return null; }
        catch (Exception ex) { return "Couldn't install the preset: " + ex.Message; }
    }

    Track dropHost;
    // a sound dropped on a row goes onto that row (into an empty track, or overlapping what is already there); below the last row it makes a new track
    void PlaceDropped(Track t)
    {
        var row = dropHost; dropHost = null;
        if (row != null && row.Host == null && !row.IsBus && t.Clip != null)
        {
            if (row.Clip == null && !row.Instrument) { PushUndo(); lock (eng.Sync) { row.Clip = t.Clip; row.Source = t.Source; row.ClipStart = 0; row.ClipFrames = -1; row.Offset = t.Offset; row.Color = t.Color; if (row.Name.StartsWith("Audio ")) row.Name = t.Name; } selTrack = Math.Max(0, eng.Tracks.IndexOf(row)); return; }
            t.Host = row; t.Out = row.Out; AddTrack(t); selTrack = Math.Max(0, eng.Tracks.IndexOf(row)); return;
        }
        AddTrack(t);
    }

    // dropped from the browser onto the timeline
    void DropSampleAt(Point p)
    {
        var s = sndDrag; sndDrag = null; sndDragging = false; if (s == null) return; var tl = TimelineRect(); float top = ToolH + TransH + RulerH;
        if (p.X < HeadW || p.X > tl.Right || p.Y < top) { Toast("Drop it on the timeline (the area to the right of the track names)."); return; }
        long frame = Snap(scroll + (long)((p.X - HeadW) * spp)); int lane = (int)((p.Y - top + vscroll) / LaneH); int at = lane >= LaneCount ? LaneCount : lane + 1;
        dropHost = lane >= 0 && lane < LaneCount ? eng.Tracks[lane] : null;   // dropped on a row: it goes on that row (overlapping whatever is there)
        string msg = AddSample(s, frame, s.Kind == "midi" ? lane : at); dropHost = null; if (msg != null) Toast(msg);
    }
    void AddSampleAtNeedle(SoundItem s) { string msg = AddSample(s, Snap(DispPos), eng.Tracks.Count); if (msg != null) Toast(msg); }

    // ---- the browser panel ----
    void PaintSoundsPanel(Graphics g)
    {
        if (!soundsLoaded) { soundsLoaded = true; SoundLibrary.Load(); } EnsureSoundBox(); if (sndDirty) RefreshSounds(); Auditioner.Tick();
        float x = ClientSize.Width - PanelW, y = ToolH + TransH, w = PanelW, h = ClientSize.Height - y; var pr = Rc(x, y, w, h);
        using (var br = new SolidBrush(Color.FromArgb(250, 18, 10, 34))) g.FillRectangle(br, pr); using (var pen = new Pen(Color.FromArgb(150, NeonViolet))) g.DrawLine(pen, x, y, x, y + h); Reg(pr, () => { });
        Txt(g, "PURPHEX SOUNDS", 16, Ink, Rc(x + 14, y + 8, 200, 26), StringAlignment.Near, true);
        var cl = Rc(x + w - 38, y + 8, 28, 26); Btn(g, cl, "x", 0, () => CloseSounds(), 13);
        Btn(g, Rc(x + w - 130, y + 8, 84, 26), "Import", 0, () => ImportMenu(), 11); Btn(g, Rc(x + w - 218, y + 8, 82, 26), "Packs", 1, () => { packsOpen = true; packScroll = 0; }, 11);
        int installedPacks = 0; lock (SoundLibrary.Lock) foreach (var p in SoundLibrary.Packs) if (p.Items.Count > 0) installedPacks++;
        Txt(g, "Search by name, pack, style, key or tempo (for example: reese 174, F min, kick):", 10, DimC, Rc(x + 14, y + 38, w - 28, 14), StringAlignment.Near, false);
        var sb = Rc(x + 14, y + 54, w - 28, 28); if (soundBox.Visible != !(packsOpen || menuOpen || browserOpen || busy)) soundBox.Visible = !(packsOpen || menuOpen || browserOpen || busy);
        var nb = new Rectangle((int)sb.X, (int)sb.Y, (int)sb.Width, (int)sb.Height); if (soundBox.Bounds != nb) soundBox.Bounds = nb;
        // filters
        float fy = y + 90, fw = (w - 28 - 5 * 4) / 6f; var F = sndFilter;
        Func<string, string, string> lab = (n, v) => v.Length > 0 ? n + ": " + v : n;
        string bpmLabel = F.BpmMax > 0 ? F.BpmMin.ToString("0") + "-" + F.BpmMax.ToString("0") : "";
        Chip(g, Rc(x + 14, fy, fw, 26), lab("Genre", F.Genre), F.Genre.Length > 0, GenreMenu); Chip(g, Rc(x + 14 + (fw + 4), fy, fw, 26), lab("Style", F.Sub), F.Sub.Length > 0, StyleMenu); Chip(g, Rc(x + 14 + 2 * (fw + 4), fy, fw, 26), lab("Type", F.Type.Length > 0 ? F.Type : (F.Group.Length > 0 ? F.Group : "")), F.Type.Length > 0 || F.Group.Length > 0 || F.AnyType != null, TypeMenu);
        Chip(g, Rc(x + 14 + 3 * (fw + 4), fy, fw, 26), lab("Key", F.Key), F.Key.Length > 0, KeyMenu); Chip(g, Rc(x + 14 + 4 * (fw + 4), fy, fw, 26), lab("BPM", bpmLabel), F.BpmMax > 0, BpmMenu); Chip(g, Rc(x + 14 + 5 * (fw + 4), fy, fw, 26), "Toolkit", false, ToolkitMenu);
        float ry = y + 120, rx = x + 14; float[] rw = { 92, 66, 100, 76, 84, 74, 62 }; string[] rn = { "Favourites", "Recent", "Collections", "One-shots", "Loops", "MIDI", "Clear" };
        for (int i = 0; i < rn.Length; i++)
        {
            var r = Rc(rx, ry, rw[i], 24); int k = i; bool on = (i == 0 && F.OnlyFav) || (i == 1 && F.OnlyRecent) || (i == 2 && F.Collection.Length > 0) || (i == 3 && F.Kind == "one-shot") || (i == 4 && F.Kind == "loop") || (i == 5 && F.Kind == "midi");
            Chip(g, r, i == 2 && F.Collection.Length > 0 ? F.Collection : rn[i], on, () => { SoundChipClick(k); }, i == 6 ? 0 : -1); rx += rw[i] + 4;
        }
        Txt(g, sndResults.Count + (sndResults.Count == 1 ? " sound" : " sounds") + "   (" + installedPacks + " pack" + (installedPacks == 1 ? "" : "s") + " installed)", 11, DimC, Rc(x + 14, y + 148, w - 28, 16), StringAlignment.Near, false);
        // list
        float py = 170, ph = 214; var list = Rc(x + 8, y + py, w - 16, h - py - ph - 6); var gs = g.Save(); g.SetClip(list, CombineMode.Intersect); float rowH = 52;
        float maxScroll = Math.Max(0, sndResults.Count * rowH - list.Height); sndScroll = Math.Max(0, Math.Min(maxScroll, sndScroll)); Reg(list, () => { }).OnWheel = d => { sndScroll = Math.Max(0, Math.Min(maxScroll, sndScroll - d * 52)); };
        if (installedPacks == 0 && sndResults.Count == 0)
        {
            Txt(g, "No sounds yet", 18, Ink, Rc(list.X, list.Y + 30, list.Width, 28), StringAlignment.Center, true); Txt(g, "Open Packs and press Install. Each pack is made on your computer in a few seconds.", 12, DimC, Rc(list.X + 20, list.Y + 64, list.Width - 40, 40), StringAlignment.Center, false);
            Btn(g, Rc(list.X + list.Width / 2 - 90, list.Y + 112, 180, 34), "Open the packs", 1, () => { packsOpen = true; packScroll = 0; }, 13);
        }
        else if (sndResults.Count == 0) Txt(g, "Nothing matches. Clear a filter or change the search.", 13, DimC, Rc(list.X + 10, list.Y + 20, list.Width - 20, 30), StringAlignment.Near, false);
        int first = (int)(sndScroll / rowH), last = Math.Min(sndResults.Count - 1, first + (int)(list.Height / rowH) + 1);
        for (int i = Math.Max(0, first); i <= last; i++) PaintSoundRow(g, sndResults[i], Rc(list.X, list.Y + i * rowH - sndScroll, list.Width, rowH - 2));
        g.Restore(gs);
        PaintSoundPreview(g, Rc(x + 8, y + h - ph, w - 16, ph - 6));
    }

    void Chip(Graphics g, RectangleF r, string text, bool on, Action a, int style = -1) { Btn(g, r, text, on ? 4 : (style < 0 ? 0 : style), a, 11); }
    void SoundChipClick(int k)
    {
        var F = sndFilter; sndDirty = true; sndScroll = 0;
        if (k == 0) F.OnlyFav = !F.OnlyFav; else if (k == 1) F.OnlyRecent = !F.OnlyRecent; else if (k == 2) CollectionsMenu(); else if (k == 3) F.Kind = F.Kind == "one-shot" ? "" : "one-shot"; else if (k == 4) F.Kind = F.Kind == "loop" ? "" : "loop"; else if (k == 5) F.Kind = F.Kind == "midi" ? "" : "midi";
        else { var t = F.Text; var n = new SoundLibrary.Filter(); F.Genre = n.Genre; F.Sub = ""; F.Type = ""; F.Group = ""; F.Key = ""; F.Kind = ""; F.Pack = ""; F.Mood = ""; F.BpmMin = 0; F.BpmMax = 0; F.HalfDouble = false; F.OnlyFav = false; F.OnlyRecent = false; F.Collection = ""; F.AnyType = null; if (soundBox != null) soundBox.Text = ""; }
    }

    void PaintSoundRow(Graphics g, SoundItem s, RectangleF r)
    {
        bool sel = s == sndSel, hot = Hot(r), sex = s.Exists; FillRR(g, sel ? Color.FromArgb(150, 80, 44, 150) : (hot ? Color.FromArgb(130, 60, 34, 110) : Color.FromArgb(90, 34, 20, 64)), r, 8); if (sel) StrokeRR(g, NeonCyan, r, 8, 1.4f);
        var tile = Rc(r.X + 6, r.Y + 6, 38, 38); DrawPackTile(g, tile, SoundLibrary.Pack(s.PackId), s.PackName);
        Txt(g, s.Name.Replace(s.PackName + " - ", ""), 12, sex ? Ink : Color.FromArgb(255, 140, 140), Rc(r.X + 50, r.Y + 5, 250, 20), StringAlignment.Near, sel);
        Txt(g, s.PackName + "  ·  " + s.Type + (sex ? "" : "  (file missing)"), 10, DimC, Rc(r.X + 50, r.Y + 26, 250, 18), StringAlignment.Near, false);
        // little wave
        var wr = Rc(r.X + 304, r.Y + 10, 90, 30); if (s.Peaks != null && s.Peaks.Length > 0) { using (var pen = new Pen(Color.FromArgb(sel ? 230 : 150, Neon(GroupColour(s.Group))), 1f)) for (int i = 0; i < wr.Width; i += 2) { float v = s.Peaks[Math.Min(s.Peaks.Length - 1, (int)(i / wr.Width * s.Peaks.Length))] / 255f * wr.Height / 2; g.DrawLine(pen, wr.X + i, wr.Y + wr.Height / 2 - v, wr.X + i, wr.Y + wr.Height / 2 + v); } }
        Txt(g, s.Bpm > 0 ? s.Bpm.ToString("0") : "-", 11, s.Bpm > 0 ? Ink : DimC, Rc(r.X + 400, r.Y, 40, r.Height), StringAlignment.Center, false); Txt(g, s.Key.Length > 0 ? s.Key : "-", 11, s.Key.Length > 0 ? Ink : DimC, Rc(r.X + 440, r.Y, 62, r.Height), StringAlignment.Center, false); Txt(g, s.Dur > 0 ? s.Dur.ToString("0.0") + "s" : "", 10, DimC, Rc(r.X + 502, r.Y, 46, r.Height), StringAlignment.Center, false);
        var star = Rc(r.Right - 30, r.Y + 12, 22, 24); bool fav = SoundLibrary.Favourites.Contains(s.Id); Txt(g, fav ? "★" : "☆", 16, fav ? Color.FromArgb(255, 210, 80) : DimC, star, StringAlignment.Center, false);
        var rg = Reg(r, () => { SelectSound(s, true); sndDrag = s; sndDragFrom = MousePos; sndDragging = false; }); rg.OnDrag = pt => { if (sndDrag != null && !sndDragging && (Math.Abs(pt.X - sndDragFrom.X) + Math.Abs(pt.Y - sndDragFrom.Y)) > 8) sndDragging = true; }; rg.OnDouble = () => AddSampleAtNeedle(s); rg.OnRight = () => SoundMenu(s);
        var sr = Reg(star, () => { SoundLibrary.ToggleFav(s); sndDirty = sndFilter.OnlyFav; sndDrag = null; }); if (Hot(star)) { bubble = fav ? "Remove from favourites" : "Add to favourites"; bubblePt = MousePos; }
    }

    static Bitmap logoSmall;
    void DrawPackTile(Graphics g, RectangleF r, SoundPack p, string name)
    {
        int hue = p != null ? p.Hue : 280; var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); ProjectLibrary.DrawArt(g, r, name, hue); g.Restore(gs); using (var pen = new Pen(Color.FromArgb(160, NeonViolet), 1.2f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        if (logoSmall == null) { var lg = AppLogo.Logo; if (lg != null) { try { logoSmall = new Bitmap(28, 28, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); using (var lgg = Graphics.FromImage(logoSmall)) { lgg.InterpolationMode = InterpolationMode.HighQualityBicubic; lgg.DrawImage(lg, new Rectangle(0, 0, 28, 28)); } } catch { logoSmall = null; } } } if (logoSmall != null) { var q = Rc(r.Right - 15, r.Bottom - 15, 14, 14); g.DrawImage(logoSmall, q); }
    }

    void SelectSound(SoundItem s, bool play) { sndSel = s; if (play && SoundLibrary.AutoPlay && s.IsAudio) Auditioner.Play(s, m => Toast(m)); else if (play && s.Kind == "midi") Toast("MIDI: drag it onto an instrument track to load its notes."); }

    void PaintSoundPreview(Graphics g, RectangleF r)
    {
        FillRR(g, Color.FromArgb(150, 24, 13, 46), r, 10); StrokeRR(g, Color.FromArgb(140, NeonViolet), r, 10, 1); var s = sndSel;
        if (s == null) { Txt(g, "Click a sound to hear it. Double-click or drag it onto the timeline to use it.", 12, DimC, Rc(r.X + 14, r.Y + 20, r.Width - 28, 40), StringAlignment.Near, false); return; }
        Txt(g, s.Name.Replace(s.PackName + " - ", ""), 13, Ink, Rc(r.X + 12, r.Y + 6, r.Width - 24, 20), StringAlignment.Near, true);
        var wf = Rc(r.X + 12, r.Y + 30, r.Width - 24, 56); FillRR(g, Color.FromArgb(200, 8, 4, 20), wf, 6);
        bool cur = Auditioner.Current == s && Auditioner.LeftSamples != null; var col = Neon(GroupColour(s.Group));
        if (Auditioner.Loading && Auditioner.Current == s) Txt(g, "Loading...", 12, DimC, wf, StringAlignment.Center, false);
        else if (s.Peaks != null && s.Peaks.Length > 0) using (var pen = new Pen(Color.FromArgb(220, col), 1.6f)) for (int i = 0; i < (int)wf.Width; i += 2) { float v = s.Peaks[Math.Min(s.Peaks.Length - 1, (int)(i / wf.Width * s.Peaks.Length))] / 255f * (wf.Height / 2 - 4); g.DrawLine(pen, wf.X + i, wf.Y + wf.Height / 2 - v, wf.X + i, wf.Y + wf.Height / 2 + v); }
        if (cur && Auditioner.Length > 0) { float px = wf.X + (float)(Auditioner.Seconds / Auditioner.Length) * wf.Width; using (var pen = new Pen(Color.White, 1.5f)) g.DrawLine(pen, px, wf.Y + 2, px, wf.Bottom - 2); }
        var wr = Reg(wf, () => { if (cur) Auditioner.Seek((MousePos.X - wf.X) / wf.Width); }); wr.OnDrag = pt => { if (cur) Auditioner.Seek((pt.X - wf.X) / wf.Width); };
        float cy = r.Y + 92; bool playing = cur && Auditioner.IsPlaying;
        Btn(g, Rc(r.X + 12, cy, 66, 28), playing ? "Pause" : "Play", 1, () => { if (cur) Auditioner.Toggle(); else if (s.IsAudio) Auditioner.Play(s, m => Toast(m)); }, 12); Btn(g, Rc(r.X + 82, cy, 50, 28), "Stop", 0, () => Auditioner.Stop(), 11);
        Chip(g, Rc(r.X + 136, cy, 56, 28), "Loop", SoundLibrary.LoopPreview, () => { SoundLibrary.LoopPreview = !SoundLibrary.LoopPreview; }); Chip(g, Rc(r.X + 196, cy, 78, 28), "Autoplay", SoundLibrary.AutoPlay, () => { SoundLibrary.AutoPlay = !SoundLibrary.AutoPlay; SoundLibrary.SaveUser(); });
        Txt(g, "vol", 10, DimC, Rc(r.X + 282, cy + 6, 24, 16), StringAlignment.Near, false); var vs = Slider(g, Rc(r.X + 306, cy + 5, r.Width - 306 - 16 + r.X - r.X, 18), SoundLibrary.AuditionVolume, f => { SoundLibrary.AuditionVolume = f; }, NeonCyan); vs.OnDouble = () => SoundLibrary.AuditionVolume = 0.8f;
        float by = cy + 34; bool preset = s.Kind == "preset", midi = s.Kind == "midi";
        Btn(g, Rc(r.X + 12, by, 168, 30), preset ? "Add to Bass Lab" : (midi ? "Add to selected track" : "Add to timeline"), 2, () => { string m = midi ? DropMidi(s, Snap(DispPos), -1) : AddSample(s, Snap(DispPos), eng.Tracks.Count); if (m != null) Toast(m); }, 12);
        bool fav = SoundLibrary.Favourites.Contains(s.Id); Chip(g, Rc(r.X + 184, by, 96, 30), fav ? "★ Favourite" : "☆ Favourite", fav, () => { SoundLibrary.ToggleFav(s); sndDirty = sndFilter.OnlyFav; }); Btn(g, Rc(r.X + 284, by, 104, 30), "Collection...", 0, () => SoundMenu(s), 11);
        Btn(g, Rc(r.X + 392, by, 70, 30), "Folder", 0, () => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + s.Path + "\""); } catch { } }, 11); Btn(g, Rc(r.X + 466, by, 80, 30), "Licence", 0, () => { var p = SoundLibrary.Pack(s.PackId); MessageBox.Show(this, (p != null ? p.Name + "\n\n" + p.License : "No licence information."), "Licence"); }, 11);
        string info = (s.Format.Length > 0 ? s.Format : "") + (s.Rate > 0 ? "  ·  " + (s.Rate / 1000.0).ToString("0.#") + " kHz" : "") + (s.Dur > 0 ? "  ·  " + s.Dur.ToString("0.00") + " s" : "") + (s.Bpm > 0 ? "  ·  " + s.Bpm.ToString("0") + " BPM" : "") + (s.Key.Length > 0 ? "  ·  " + s.Key : "") + "  ·  " + s.PackName;
        Txt(g, info, 10, DimC, Rc(r.X + 12, by + 34, r.Width - 24, 16), StringAlignment.Near, false);
    }

    // ---- menus ----
    void ShowFilterMenu(ContextMenuStrip m) { m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    void GenreMenu() { var m = Win.NewMenu(); AddAny(m, "Any genre", () => sndFilter.Genre = ""); foreach (var v in SoundLibrary.Distinct(x => x.Genre)) { string vv = v; AddItem(m, v, sndFilter.Genre == v, () => { sndFilter.Genre = vv; sndFilter.Sub = ""; }); } ShowFilterMenu(m); }
    void StyleMenu() { var m = Win.NewMenu(); AddAny(m, "Any style", () => sndFilter.Sub = ""); foreach (var v in SoundLibrary.Distinct(x => (sndFilter.Genre.Length == 0 || x.Genre == sndFilter.Genre) ? x.Sub : "")) { string vv = v; AddItem(m, v, sndFilter.Sub == v, () => sndFilter.Sub = vv); } ShowFilterMenu(m); }
    void TypeMenu()
    {
        var m = Win.NewMenu(); AddAny(m, "Any type", () => { sndFilter.Type = ""; sndFilter.Group = ""; sndFilter.AnyType = null; });
        foreach (var grp in SoundLibrary.Distinct(x => x.Group)) { string gg = grp; var top = new ToolStripMenuItem(grp); var all = new ToolStripMenuItem("All " + grp.ToLowerInvariant()); all.Click += (a, b) => { sndFilter.Group = gg; sndFilter.Type = ""; sndFilter.AnyType = null; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(all); top.DropDownItems.Add(new ToolStripSeparator());
            foreach (var t in SoundLibrary.Distinct(x => x.Group == gg ? x.Type : "")) { string tt = t; var it = new ToolStripMenuItem(t) { Checked = sndFilter.Type == t }; it.Click += (a, b) => { sndFilter.Type = tt; sndFilter.Group = ""; sndFilter.AnyType = null; sndDirty = true; sndScroll = 0; }; top.DropDownItems.Add(it); } m.Items.Add(top); }
        ShowFilterMenu(m);
    }
    void KeyMenu() { var m = Win.NewMenu(); AddAny(m, "Any key", () => sndFilter.Key = ""); for (int mi = 0; mi < 2; mi++) for (int i = 0; i < 12; i++) { string k = SoundLibrary.NoteNames[i] + (mi == 0 ? " min" : " maj"); string kk = k; AddItem(m, k, sndFilter.Key == k, () => sndFilter.Key = kk); } ShowFilterMenu(m); }
    void BpmMenu()
    {
        var m = Win.NewMenu(); AddAny(m, "Any tempo", () => { sndFilter.BpmMin = 0; sndFilter.BpmMax = 0; });
        object[][] opts = { new object[] { "85 - 100  (boom bap, half-time)", 85.0, 100.0 }, new object[] { "120 - 130  (house, tech house)", 120.0, 130.0 }, new object[] { "130 - 138  (garage, bassline)", 130.0, 138.0 }, new object[] { "138 - 150  (trap, drill, dubstep)", 138.0, 150.0 }, new object[] { "160 - 170  (jungle)", 160.0, 170.0 }, new object[] { "170 - 176  (drum and bass)", 170.0, 176.0 } };
        foreach (var o in opts) { double a = (double)o[1], b = (double)o[2]; AddItem(m, (string)o[0], sndFilter.BpmMin == a && sndFilter.BpmMax == b, () => { sndFilter.BpmMin = a; sndFilter.BpmMax = b; }); }
        m.Items.Add("Exact tempo...").Click += (a, b) => { string v = Ask("Tempo in BPM (the search allows 3 BPM either way):", Fx.ProjectBpm.ToString("0")); double bpm; if (v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bpm)) { sndFilter.BpmMin = bpm - 3; sndFilter.BpmMax = bpm + 3; sndDirty = true; sndScroll = 0; } };
        m.Items.Add("Match my project's tempo").Click += (a, b) => { sndFilter.BpmMin = Fx.ProjectBpm - 3; sndFilter.BpmMax = Fx.ProjectBpm + 3; sndDirty = true; sndScroll = 0; };
        var hd = new ToolStripMenuItem("Also show half and double time") { Checked = sndFilter.HalfDouble }; hd.Click += (a, b) => { sndFilter.HalfDouble = !sndFilter.HalfDouble; sndDirty = true; }; m.Items.Add(hd); ShowFilterMenu(m);
    }
    void ToolkitMenu() { var m = Win.NewMenu(); foreach (var set in SoundToolkit.Sets) { var st = set; AddItem(m, set.Name, false, () => { var keep = sndFilter.Text; var f = st.Filter.Copy(); sndFilter.Type = f.Type; sndFilter.Group = f.Group; sndFilter.AnyType = f.AnyType; sndFilter.Kind = f.Kind; sndFilter.Pack = f.Pack; sndFilter.Genre = ""; sndFilter.Sub = ""; sndFilter.Key = ""; sndFilter.BpmMin = 0; sndFilter.BpmMax = 0; sndFilter.OnlyFav = false; sndFilter.OnlyRecent = false; sndFilter.Collection = ""; }); } ShowFilterMenu(m); }
    void CollectionsMenu()
    {
        var m = Win.NewMenu(); AddAny(m, "All sounds", () => sndFilter.Collection = ""); lock (SoundLibrary.Lock) foreach (var kv in SoundLibrary.Collections) { string n = kv.Key; AddItem(m, n + "  (" + kv.Value.Count + ")", sndFilter.Collection == n, () => sndFilter.Collection = n); }
        if (SoundLibrary.Collections.Count == 0) m.Items.Add("(none yet: use Collection... on a sound)").Enabled = false; ShowFilterMenu(m);
    }
    void SoundMenu(SoundItem s)
    {
        var m = Win.NewMenu(); m.Items.Add("Add to timeline").Click += (a, b) => AddSampleAtNeedle(s); m.Items.Add(SoundLibrary.Favourites.Contains(s.Id) ? "Remove from favourites" : "Add to favourites").Click += (a, b) => { SoundLibrary.ToggleFav(s); sndDirty = true; }; m.Items.Add(new ToolStripSeparator());
        lock (SoundLibrary.Lock) foreach (var kv in SoundLibrary.Collections) { string n = kv.Key; bool inC = kv.Value.Contains(s.Id); m.Items.Add((inC ? "Remove from collection: " : "Add to collection: ") + n).Click += (a, b) => { if (inC) SoundLibrary.RemoveFromCollection(n, s); else SoundLibrary.AddToCollection(n, s); sndDirty = true; }; }
        m.Items.Add("New collection with this sound...").Click += (a, b) => { string n = Ask("Collection name:", "My collection"); if (n != null) { SoundLibrary.AddToCollection(n.Trim().Replace("|", "/"), s); Toast("Added to " + n); } };
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Show in folder").Click += (a, b) => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + s.Path + "\""); } catch { } }; ShowFilterMenu(m);
    }
    void AddAny(ContextMenuStrip m, string text, Action a) { var it = new ToolStripMenuItem(text); it.Click += (x, y) => { a(); sndDirty = true; sndScroll = 0; }; m.Items.Add(it); m.Items.Add(new ToolStripSeparator()); }
    void AddItem(ContextMenuStrip m, string text, bool on, Action a) { var it = new ToolStripMenuItem(text) { Checked = on }; it.Click += (x, y) => { a(); sndDirty = true; sndScroll = 0; }; m.Items.Add(it); }
    void ImportMenu()
    {
        var m = Win.NewMenu();
        m.Items.Add("Import my own samples (wav, mp3, flac, midi...)").Click += (a, b) =>
        {
            using (var d = new OpenFileDialog { Title = "Choose samples you own", Multiselect = true, Filter = "Audio and MIDI|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.m4a;*.mid|All files|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) { int n = SoundLibrary.ImportFiles(d.FileNames, null); sndDirty = true; Toast("Imported " + n + " sound" + (n == 1 ? "" : "s") + " into My samples"); }
        };
        m.Items.Add("Import a sound pack (.zip)").Click += (a, b) =>
        {
            using (var d = new OpenFileDialog { Title = "Choose a Purphex sound pack", Filter = "Sound pack|*.zip" })
                if (d.ShowDialog(this) == DialogResult.OK) { string err = SoundLibrary.ImportPackZip(d.FileName); sndDirty = true; Toast(err ?? "Pack added"); }
        };
        m.Items.Add("Open the sounds folder").Click += (a, b) => { try { Directory.CreateDirectory(SoundLibrary.Root); System.Diagnostics.Process.Start("explorer.exe", "\"" + SoundLibrary.Root + "\""); } catch { } }; ShowFilterMenu(m);
    }

    // ---- drag ghost ----
    void PaintSoundDrag(Graphics g)
    {
        if (!sndDragging || sndDrag == null) return; var tl = TimelineRect(); float top = ToolH + TransH + RulerH; bool over = MousePos.X >= HeadW && MousePos.X <= tl.Right && MousePos.Y >= top;
        if (over) { using (var pen = new Pen(Color.FromArgb(220, NeonCyan), 2f)) { g.DrawLine(pen, MousePos.X, top, MousePos.X, ClientSize.Height - 20); } }
        var r = Rc(MousePos.X + 12, MousePos.Y + 10, 230, 24); FillRR(g, Color.FromArgb(235, 30, 14, 64), r, 8); Glow(g, over ? NeonCyan : NeonPink, r, 8, 0.8f, 2); Txt(g, (over ? "Drop to add: " : "Drag to the timeline: ") + sndDrag.Name.Replace(sndDrag.PackName + " - ", ""), 11, Ink, r, StringAlignment.Center, true);
    }

    // ---- the pack manager ----
    string PackSizeText(SoundPack p) { long b; if (!packBytes.TryGetValue(p.Id, out b)) { b = p.Bytes; packBytes[p.Id] = b; } return b > 1048576 ? (b / 1048576.0).ToString("0") + " MB" : (b / 1024) + " KB"; }
    void StartInstall(Recipe r)
    {
        lock (installQ) { if (installQ.Contains(r.Id) || installing == r.Id) return; installQ.Enqueue(r.Id); }
        if (installing.Length > 0) return; installCancel = false; new Thread(InstallLoop) { IsBackground = true }.Start();
    }
    void InstallLoop()
    {
        while (true)
        {
            string id; lock (installQ) { if (installQ.Count == 0) { installing = ""; return; } id = installQ.Dequeue(); installing = id; } var rec = SoundCatalog.Find(id); installPct = 0; installNote = "Starting...";
            try
            {
                var pack = PackBuilder.Build(rec, (note, p) => { installNote = note; installPct = p; }, () => installCancel);
                if (pack != null) { SoundLibrary.AddOrReplace(pack); packBytes.Remove(id); packMsg = "Installed " + rec.Name + " (" + pack.Items.Count + " sounds)"; } else packMsg = "Cancelled " + rec.Name;
            }
            catch (Exception ex) { packMsg = "Couldn't make " + rec.Name + ": " + ex.Message; }
            sndDirty = true; if (installCancel) { lock (installQ) installQ.Clear(); }
            try { BeginInvoke(new Action(Invalidate)); } catch { }
        }
    }

    void PaintPackManager(Graphics g)
    {
        if (!packsOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); using (var b = new SolidBrush(Color.FromArgb(190, 4, 2, 12))) g.FillRectangle(b, all);
        var box = Rc(50, 40, W - 100, H - 80); Reg(all, () => { if (!box.Contains(MousePos)) packsOpen = false; }); FillRR(g, Color.FromArgb(252, 24, 13, 46), box, 14); Glow(g, NeonViolet, box, 14, 0.7f, 3); Reg(box, () => { });
        Txt(g, "PURPHEX SOUND PACKS", 20, Ink, Rc(box.X + 22, box.Y + 14, 400, 30), StringAlignment.Near, true); Btn(g, Rc(box.Right - 100, box.Y + 14, 82, 30), "Close", 0, () => packsOpen = false, 12);
        Txt(g, "Every sound is made on your computer by Purphex's own sound design: no recordings, no one else's samples. Installing a pack takes a few seconds.", 12, DimC, Rc(box.X + 22, box.Y + 46, box.Width - 44, 18), StringAlignment.Near, false);
        // sections
        var secs = new List<string> { "All" }; foreach (var r in SoundCatalog.Recipes) if (!secs.Contains(r.Section)) secs.Add(r.Section); secs.Add("Installed"); float sx = box.X + 22;
        foreach (var sc in secs) { string s2 = sc; float bw = Math.Max(62, sc.Length * 8 + 26); Chip(g, Rc(sx, box.Y + 74, bw, 28), sc, packSection == sc, () => { packSection = s2; packScroll = 0; }); sx += bw + 6; }
        // list
        var list = Rc(box.X + 16, box.Y + 112, box.Width - 32, box.Height - 112 - 70); var rows = new List<Recipe>(); foreach (var r in SoundCatalog.Recipes) { var ip = SoundLibrary.Pack(r.Id); bool inst = ip != null && ip.Items.Count > 0; if (packSection == "All" || (packSection == "Installed" ? inst : r.Section == packSection)) rows.Add(r); }
        float rowH = 66; float maxS = Math.Max(0, rows.Count * rowH - list.Height); packScroll = Math.Max(0, Math.Min(maxS, packScroll)); Reg(list, () => { }).OnWheel = d => { packScroll = Math.Max(0, Math.Min(maxS, packScroll - d * 66)); };
        var gs = g.Save(); g.SetClip(list, CombineMode.Intersect); int first = Math.Max(0, (int)(packScroll / rowH)), last = Math.Min(rows.Count - 1, first + (int)(list.Height / rowH) + 1);
        if (rows.Count == 0) Txt(g, "Nothing here yet.", 14, DimC, Rc(list.X + 10, list.Y + 20, 400, 24), StringAlignment.Near, false);
        for (int i = first; i <= last; i++)
        {
            var rec = rows[i]; var rr = Rc(list.X, list.Y + i * rowH - packScroll, list.Width, rowH - 4); var ip = SoundLibrary.Pack(rec.Id); bool inst = ip != null && ip.Items.Count > 0; bool busyNow = installing == rec.Id; bool queued; lock (installQ) queued = installQ.Contains(rec.Id);
            FillRR(g, Color.FromArgb(120, 34, 20, 64), rr, 8); var tile = Rc(rr.X + 6, rr.Y + 6, 50, 50); var fake = new SoundPack { Hue = rec.Hue }; DrawPackTile(g, tile, fake, rec.Name);
            Txt(g, rec.Name, 14, Ink, Rc(rr.X + 66, rr.Y + 6, rr.Width - 330, 22), StringAlignment.Near, true); Txt(g, rec.Genre + "  ·  " + rec.Sub + "  ·  " + rec.Mood, 10, DimC, Rc(rr.X + 66, rr.Y + 28, rr.Width - 330, 16), StringAlignment.Near, false); Txt(g, rec.Desc, 10, DimC, Rc(rr.X + 66, rr.Y + 44, rr.Width - 330, 16), StringAlignment.Near, false);
            float bx = rr.Right - 250;
            if (busyNow) { var pb = Rc(bx, rr.Y + 12, 170, 10); FillRR(g, Color.FromArgb(200, 8, 4, 18), pb, 5); FillRR(g, NeonCyan, Rc(pb.X, pb.Y, Math.Max(6, pb.Width * installPct), pb.Height), 5); Txt(g, ((int)(installPct * 100)) + "%  " + installNote, 10, DimC, Rc(bx, rr.Y + 26, 240, 16), StringAlignment.Near, false); Btn(g, Rc(bx + 176, rr.Y + 8, 66, 22), "Stop", 3, () => installCancel = true, 10); }
            else if (queued) Txt(g, "Waiting...", 12, DimC, Rc(bx, rr.Y + 18, 240, 24), StringAlignment.Near, false);
            else if (inst) { Txt(g, "Installed: " + ip.Items.Count + " sounds, " + PackSizeText(ip), 11, Color.FromArgb(120, 232, 150), Rc(bx, rr.Y + 8, 240, 18), StringAlignment.Near, false); Btn(g, Rc(bx, rr.Y + 30, 76, 26), "Browse", 1, () => { packsOpen = false; sndFilter.Pack = rec.Id; sndDirty = true; sndScroll = 0; }, 11); Btn(g, Rc(bx + 80, rr.Y + 30, 76, 26), "Remake", 0, () => StartInstall(rec), 11); Btn(g, Rc(bx + 160, rr.Y + 30, 76, 26), "Remove", 3, () => { if (MessageBox.Show(this, "Remove \"" + rec.Name + "\" and its files from this PC? You can install it again any time.", "Remove pack", MessageBoxButtons.YesNo) == DialogResult.Yes) { if (sndFilter.Pack == rec.Id) sndFilter.Pack = ""; if (sndSel != null && sndSel.PackId == rec.Id) { Auditioner.Stop(); sndSel = null; } SoundLibrary.RemovePack(rec.Id); sndDirty = true; packMsg = "Removed " + rec.Name; } }, 11); }
            else Btn(g, Rc(bx + 76, rr.Y + 16, 160, 32), "Install", 2, () => StartInstall(rec), 13);
        }
        g.Restore(gs);
        // footer
        float fy = box.Bottom - 56; int notInst = 0; foreach (var r in rows) { var ip = SoundLibrary.Pack(r.Id); if (ip == null || ip.Items.Count == 0) notInst++; }
        Btn(g, Rc(box.X + 22, fy, 230, 32), "Install all " + notInst + " in this list", notInst > 0 ? 1 : 0, () => { foreach (var r in rows) { var ip = SoundLibrary.Pack(r.Id); if (ip == null || ip.Items.Count == 0) StartInstall(r); } }, 12);
        Btn(g, Rc(box.X + 260, fy, 120, 32), "Import...", 0, () => ImportMenu(), 12);
        Txt(g, installing.Length > 0 ? "Making " + (SoundCatalog.Find(installing) != null ? SoundCatalog.Find(installing).Name : "") + "..." : packMsg, 12, installing.Length > 0 ? NeonCyan : Color.FromArgb(120, 232, 150), Rc(box.X + 392, fy + 6, box.Width - 420, 20), StringAlignment.Near, true);
        Txt(g, "Licence: " + SoundLibrary.LicenceText, 9, DimC, Rc(box.X + 22, fy + 30, box.Width - 44, 14), StringAlignment.Near, false);
    }
}
