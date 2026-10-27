using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  PRODUCER TAG: its own window over the studio (drag it, resize it, galaxy look). Type your tag, pick a voice on this PC or record your
//  own (MY VOICE), a character, the effects you want (each says what it does) and a vocal preset if you like; PREVIEW says what is in
//  the box with all of it. SAVE keeps it in Music / yt2mp3 / Producer Tags, ADD TO SONG puts it at the needle, or drag the circle onto
//  any track. Supporter Edition and up. The sound is made in TagMaker.cs.
// =====================================================================================================
partial class StudioForm
{
    const int TagKind = 18;
    readonly TagSettings tagSet = new TagSettings { Text = "Produced by Purphex" };
    float[] tagDry; string tagDrySig = ""; float[][] tagOut; string tagOutSig = "", tagPath = "", tagMsg = ""; volatile bool tagBusy; int tagFxShown = -1;
    AudioRecorder tagRec; DateTime tagRecAt; float[] tagMine;

    internal void OpenProducerTag() { if (!Paid("The producer tag generator")) return; OpenToolWin(TagKind); }
    string TagWinSig() { return tagSet.Sig() + "|" + tagBusy + "|" + tagMsg + "|" + (tagRec != null) + "|" + Auditioner.IsPlaying + "|" + tagFxShown + "|" + (tagMine != null) + "|" + tagOutSig.Length; }

    void PaintProducerTag(Graphics g, RectangleF b)
    {
        var voices = TagMaker.Voices(); float x = b.X + 8, w = b.Width - 16, y = b.Y + 4;
        if (tagRec != null) { Invalidate(); if ((DateTime.Now - tagRecAt).TotalSeconds > 10) TagStopRecording(); }
        // the tag and PREVIEW
        Txt(g, "YOUR TAG", 11, NeonCyan, Rc(x, y, 200, 18), StringAlignment.Near, true); y += 20;
        var tb = Rc(x, y, w - 150, 40); ValueBox(g, tb, tagSet.Text.Length > 0 ? tagSet.Text : "(click to type your tag)", tagSet.Text, s => { tagSet.Text = s.Trim(); }, 16, true); Hint2(tb, "Click and type what the tag says, for example your producer name");
        var pv = Rc(tb.Right + 8, y, 142, 40); Btn(g, pv, tagBusy ? "MAKING..." : (Auditioner.IsPlaying && Auditioner.Current != null && Auditioner.Current.Name == "Producer tag" ? "STOP" : "PREVIEW"), 4, () => TagPreview(), 15); Hint2(pv, "Hear what is in the box with the voice, character, effects and vocal preset you picked");
        y += 50;
        // the voice
        Txt(g, "VOICE", 11, NeonCyan, Rc(x, y, 200, 18), StringAlignment.Near, true);
        Txt(g, voices.Count + " voices on this PC (more free ones: this PC's Settings > Time & language > Speech), or your own", 9, DimC, Rc(x + 60, y + 2, w - 60, 16), StringAlignment.Near, false); y += 20;
        { float cx = x; float ch = 28;
          for (int i = 0; i < voices.Count; i++) { int k = i; float cw = Math.Max(90, voices[i].Length * 7.4f + 22); if (cx + cw > x + w - 230) { cx = x; y += ch + 4; } var r = Rc(cx, y, cw, ch); Btn(g, r, voices[i], tagSet.Voice == i ? 4 : 0, () => tagSet.Voice = k, 10); Hint2(r, "Speak the tag with " + voices[i]); cx += cw + 6; }
          if (cx + 224 > x + w) { cx = x; y += ch + 4; }
          var mv = Rc(cx, y, 104, ch); Btn(g, mv, "MY VOICE", tagSet.Voice < 0 ? 4 : 1, () => { if (tagMine == null && tagRec == null) TagStartRecording(); else tagSet.Voice = -1; }, 10); Hint2(mv, tagMine == null ? "Record yourself saying your tag" : "Use your own recording");
          var rb = Rc(mv.Right + 6, y, 114, ch); Btn(g, rb, tagRec != null ? "STOP  " + (DateTime.Now - tagRecAt).TotalSeconds.ToString("0.0") + "s" : (tagMine == null ? "RECORD" : "RECORD AGAIN"), tagRec != null ? 3 : 0, () => { if (tagRec != null) TagStopRecording(); else TagStartRecording(); }, 10); Hint2(rb, "Record your voice saying the tag (up to 10 seconds; the silence before and after is cut off)");
          y += ch + 10; }
        // the character
        Txt(g, "CHARACTER", 11, NeonCyan, Rc(x, y, 200, 18), StringAlignment.Near, true); y += 20;
        { float cx = x; for (int i = 0; i < TagMaker.Characters.Length; i++) { int k = i; var c = TagMaker.Characters[i]; float cw = Math.Max(78, c.Name.Length * 8f + 20); if (cx + cw > x + w) { cx = x; y += 32; } var r = Rc(cx, y, cw, 28); Btn(g, r, c.Name.ToUpperInvariant(), tagSet.Character == i ? 4 : 0, () => tagSet.Character = k, 10); Hint2(r, c.Name + ": " + c.Says); cx += cw + 6; } y += 34; }
        Txt(g, TagMaker.Characters[Math.Max(0, Math.Min(TagMaker.Characters.Length - 1, tagSet.Character))].Says, 10, Ink, Rc(x, y, w, 16), StringAlignment.Near, false); y += 20;
        // speed and pitch
        { float hw = (w - 20) / 2;
          Txt(g, "SPEED  " + (tagSet.Speed > 0 ? "+" : "") + tagSet.Speed, 10, DimC, Rc(x, y, hw, 16), StringAlignment.Near, true); Txt(g, "PITCH  " + (tagSet.Pitch > 0 ? "+" : "") + tagSet.Pitch.ToString("0") + " st", 10, DimC, Rc(x + hw + 20, y, hw, 16), StringAlignment.Near, true); y += 18;
          var sp = Slider(g, Rc(x, y, hw, 14), (tagSet.Speed + 5) / 10f, v => tagSet.Speed = (int)Math.Round(v * 10 - 5), NeonCyan); sp.OnDouble = () => tagSet.Speed = 0; Hint2(Rc(x, y, hw, 14), "How fast the voice speaks (double-click: normal)");
          var pp = Slider(g, Rc(x + hw + 20, y, hw, 14), (tagSet.Pitch + 12) / 24f, v => tagSet.Pitch = (float)Math.Round(v * 24 - 12), NeonPink); pp.OnDouble = () => tagSet.Pitch = 0; Hint2(Rc(x + hw + 20, y, hw, 14), "Higher or lower, on top of the character (double-click: none)");
          y += 26; }
        // the effects
        Txt(g, "EFFECTS", 11, NeonCyan, Rc(x, y, 200, 18), StringAlignment.Near, true); Txt(g, "pick any: each one says what it does", 9, DimC, Rc(x + 74, y + 2, w - 74, 16), StringAlignment.Near, false); y += 20;
        { float cx = x; int hovered = -1;
          for (int i = 0; i < TagMaker.Effects.Length; i++) { int k = i; string nm = TagMaker.Effects[i][0]; float cw = Math.Max(76, nm.Length * 8f + 20); if (cx + cw > x + w) { cx = x; y += 32; } var r = Rc(cx, y, cw, 28); if (Hot(r)) hovered = i; Btn(g, r, nm, tagSet.Fx[i] ? 4 : 0, () => { tagSet.Fx[k] = !tagSet.Fx[k]; tagFxShown = k; }, 10); Hint2(r, nm + ": " + TagMaker.Effects[i][1]); cx += cw + 6; }
          y += 32; int show = hovered >= 0 ? hovered : tagFxShown;
          Txt(g, show >= 0 ? TagMaker.Effects[show][0] + ": " + TagMaker.Effects[show][1] : "Point at an effect to see what it does.", 10, Ink, Rc(x, y, w, 16), StringAlignment.Near, false); y += 22; }
        // a vocal preset
        { Txt(g, "VOCAL PRESET", 11, NeonCyan, Rc(x, y + 5, 130, 18), StringAlignment.Near, true);
          var vp = Rc(x + 130, y, Math.Min(360, w - 130), 28); Btn(g, vp, tagSet.VocalPreset.Length > 0 ? tagSet.VocalPreset : "NONE (PICK ONE)", tagSet.VocalPreset.Length > 0 ? 4 : 0, () => TagPresetMenu(), 10); Hint2(vp, "Put one of the studio's vocal presets on the tag (the same chains you use on your vocals)");
          if (tagSet.VocalPreset.Length > 0) { var cl = Rc(vp.Right + 6, y, 28, 28); Btn(g, cl, "x", 0, () => tagSet.VocalPreset = "", 11); Hint2(cl, "No vocal preset"); }
          y += 38; }
        // the tag as it sounds, and what to do with it
        var wave = Rc(x, y, w, Math.Max(40, Math.Min(90, b.Bottom - y - 50))); FillRR(g, TM.C(210, 8, 3, 20), wave, 10); StrokeRR(g, Color.FromArgb(150, NeonViolet), wave, 10, 1.1f);
        if (tagOut != null && tagOut[0].Length > 1)
        {
            var L = tagOut[0]; int bars = (int)Math.Min(wave.Width / 3, 400); float mid = wave.Y + wave.Height / 2, amp = wave.Height / 2 - 6;
            using (var pen = new Pen(Color.FromArgb(220, NeonCyan), 1.6f)) for (int k = 0; k < bars; k++) { int a = k * L.Length / bars, e = Math.Min(L.Length, (k + 1) * L.Length / bars); float m = 0; for (int i = a; i < e; i++) m = Math.Max(m, Math.Abs(L[i])); float bx = wave.X + 6 + k * (wave.Width - 12) / bars; g.DrawLine(pen, bx, mid - m * amp, bx, mid + m * amp); }
            Txt(g, (L.Length / (double)TagMaker.SR).ToString("0.0") + " s", 9, DimC, Rc(wave.Right - 60, wave.Y + 4, 54, 14), StringAlignment.Far, false);
        }
        else Txt(g, tagMsg.Length > 0 ? tagMsg : "Press PREVIEW to make your tag", 11, DimC, wave, StringAlignment.Center, false);
        if (tagMsg.Length > 0 && tagOut != null) Txt(g, tagMsg, 9, Warn, Rc(wave.X + 8, wave.Y + 4, wave.Width - 80, 14), StringAlignment.Near, false);
        y = wave.Bottom + 8; float bw = (w - 60 - 18) / 3;
        var sv = Rc(x, y, bw, 32); Btn(g, sv, "SAVE TAG", 2, () => TagMakeThen(p => { string f = TagSave(p); if (f != null) Toast("Saved: Music / yt2mp3 / Producer Tags / " + Path.GetFileName(f)); }), 11); Hint2(sv, "Keep the tag as a WAV file in Music / yt2mp3 / Producer Tags");
        var ad = Rc(sv.Right + 6, y, bw, 32); Btn(g, ad, "ADD TO SONG", 2, () => TagMakeThen(p => { string f = TagSave(p); if (f == null) return; string m = AddSample(TagItem(f), Snap(DispPos), eng.Tracks.Count); if (m != null) Toast(m); }), 11); Hint2(ad, "Put the tag on a new track at the needle");
        var op = Rc(ad.Right + 6, y, bw, 32); Btn(g, op, "OPEN FOLDER", 0, () => { try { Directory.CreateDirectory(TagDir()); System.Diagnostics.Process.Start(TagDir()); } catch { } }, 11); Hint2(op, "Open the folder with your saved tags");
        { float dcx = op.Right + 30, dcy = y + 16; var dr = Rc(dcx - 22, dcy - 22, 44, 44); var dsm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; GalaxyBg.Sphere(g, dcx, dcy, 34, Hot(dr)); using (var pen = new Pen(Color.White, 2f)) { g.DrawLine(pen, dcx - 8, dcy, dcx + 8, dcy); g.DrawLine(pen, dcx, dcy - 8, dcx, dcy + 8); } g.SmoothingMode = dsm;
          var drg = Reg(dr, () => { if (tagPath.Length == 0 || tagOutSig != tagSet.Sig()) { Toast("Press PREVIEW first, then drag your tag onto a track."); return; } string f = TagSave(tagPath); if (f == null) return; sndDrag = TagItem(f); sndDragFrom = MousePos; sndDragging = false; });
          drg.OnDrag = pt => { if (sndDrag != null && !sndDragging && (Math.Abs(pt.X - sndDragFrom.X) + Math.Abs(pt.Y - sndDragFrom.Y) > 6)) sndDragging = true; }; Hint2(dr, "Drag your tag onto any track (it goes where you drop it)"); }
    }

    // ---- making the tag ----
    void TagPreview()
    {
        if (Auditioner.IsPlaying && Auditioner.Current != null && Auditioner.Current.Name == "Producer tag") { Auditioner.Stop(); return; }
        TagMakeThen(p => Auditioner.Play(TagItem(p, "Producer tag"), e => Toast(e)));
    }
    // make the tag (on a thread: the voice takes a moment), then do something with the file
    void TagMakeThen(Action<string> then)
    {
        if (tagBusy) return; var s = new TagSettings { Text = tagSet.Text, Voice = tagSet.Voice, Character = tagSet.Character, Speed = tagSet.Speed, Pitch = tagSet.Pitch, Fx = (bool[])tagSet.Fx.Clone(), VocalPreset = tagSet.VocalPreset };
        string sig = s.Sig(); if (tagOut != null && sig == tagOutSig && File.Exists(tagPath)) { then(tagPath); return; }
        if (s.Voice < 0 && tagMine == null) { tagMsg = "Record your voice first (RECORD), or pick a voice."; Toast(tagMsg); return; }
        tagBusy = true; tagMsg = ""; var mine = tagMine;
        new System.Threading.Thread(() =>
        {
            string err = null, path = null; float[][] o = null;
            try
            {
                string dsig = s.Voice < 0 ? "mine" : s.Text + "|" + s.Voice + "|" + s.Speed; float[] dry = s.Voice < 0 ? mine : (dsig == tagDrySig && tagDry != null ? tagDry : TagMaker.Speak(s.Text, s.Voice, s.Speed, out err));
                if (dry != null) { if (s.Voice >= 0) { tagDry = dry; tagDrySig = dsig; } o = TagMaker.Process(dry, s); }
                if (o != null) { path = Path.Combine(Path.GetTempPath(), "purphex_tag_preview_" + Math.Abs(sig.GetHashCode()) + ".wav"); SoundIO.WriteWav(path, o[0], o[1], TagMaker.SR, 24); }
                else if (err == null) err = "The tag came out silent.";
            }
            catch (Exception ex) { err = ex.Message; }
            BeginInvoke((Action)(() => { tagBusy = false; if (o != null) { tagOut = o; tagOutSig = sig; tagPath = path; tagMsg = ""; then(path); } else { tagMsg = err; Toast(err); } Invalidate(); }));
        }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal }.Start();
    }
    static string TagDir() { return StudioEnv.UnderTest ? Path.Combine(StudioEnv.DataDir, "tags") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Producer Tags"); }
    // the tag kept as a file named after what it says
    string TagSave(string tmp)
    {
        try
        {
            string nm = new string(tagSet.Text.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').ToArray()).Trim(); if (nm.Length == 0) nm = "My tag"; if (nm.Length > 40) nm = nm.Substring(0, 40);
            Directory.CreateDirectory(TagDir()); string f = Path.Combine(TagDir(), "Tag - " + nm + " - " + TagMaker.Characters[tagSet.Character].Name + ".wav"); File.Copy(tmp, f, true); return f;
        }
        catch (Exception ex) { Toast("Couldn't save the tag: " + ex.Message); return null; }
    }
    SoundItem TagItem(string path, string name = null)
    {
        var s = new SoundItem { Name = name ?? Path.GetFileNameWithoutExtension(path), Path = path, Kind = "one-shot", Group = "Vocal", Rel = Path.GetFileName(path) };
        if (tagOut != null) { var L = tagOut[0]; s.Dur = L.Length / (double)TagMaker.SR; var pk = new byte[200]; for (int k = 0; k < 200; k++) { int a = k * L.Length / 200, e = Math.Min(L.Length, (k + 1) * L.Length / 200); float m = 0; for (int i = a; i < e; i++) m = Math.Max(m, Math.Abs(L[i])); pk[k] = (byte)Math.Min(255, m * 255); } s.Peaks = pk; }
        return s;
    }
    void TagPresetMenu()
    {
        var m = Win.NewMenu(); var none = new ToolStripMenuItem("None") { Checked = tagSet.VocalPreset.Length == 0 }; none.Click += (a, b) => tagSet.VocalPreset = ""; m.Items.Add(none); m.Items.Add(new ToolStripSeparator());
        foreach (var gname in VocalStyles.Genres)
        {
            var it = new ToolStripMenuItem(gname); foreach (var st in VocalStyles.All.Where(x => x.Genre == gname)) { var nm = st.Name; var si = new ToolStripMenuItem(nm) { Checked = nm == tagSet.VocalPreset, ToolTipText = st.Blurb }; si.Click += (a, b) => { tagSet.VocalPreset = nm; Invalidate(); }; it.DropDownItems.Add(si); }
            if (it.DropDownItems.Count > 0) m.Items.Add(it);
        }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    // ---- your own voice ----
    void TagStartRecording()
    {
        try
        {
            Auditioner.Stop(); var devs = AudioRecorder.Devices(); int idx = Prefs.InDev.Length > 0 ? devs.IndexOf(Prefs.InDev) : -1; var r = new AudioRecorder();
            if (!r.Start(idx)) { Toast(r.Error); r.Dispose(); return; }
            tagRec = r; tagRecAt = DateTime.Now; tagMsg = "Recording: say your tag, then press STOP.";
        }
        catch (Exception ex) { Toast("Couldn't start the microphone: " + ex.Message); }
    }
    void TagStopRecording()
    {
        var r = tagRec; tagRec = null; if (r == null) return; int frames; float[] x; try { x = r.Stop(out frames); } finally { r.Dispose(); }
        var t = TagMaker.Trim(x); if (t == null || t.Length < TagMaker.SR / 5) { tagMsg = "Nothing was heard. Check your microphone and try again."; Toast(tagMsg); return; }
        tagMine = t; tagSet.Voice = -1; tagMsg = ""; tagOutSig = ""; Toast("Your voice is ready: press PREVIEW.");
    }
}
