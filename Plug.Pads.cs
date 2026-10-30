using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Purphex Pads: 16 pads, each playing a sound you choose (from the sound library, a file on your PC, or one of your song's tracks),
// played from the pads of a MIDI keyboard (pads 1-16 start on notes 36-51, the usual drum-pad notes; LEARN takes any other).
// It is the sampler underneath (one sound per key), so it plays from the piano roll and saves with the project like it.
class PadsFx : SamplerFx
{
    public const int N = 16;
    public readonly int[] HitAt = new int[128];   // when each note was last hit (the window lights the pad)
    public volatile int LearnPad = -1, Learned = -1;   // LEARN: the pad waiting for a key, and the key that came
    public PadsFx()
    {
        Type = "pxpads"; Title = "Purphex Pads"; Category = "Instruments"; Blurb = "16 pads, any sound on each (library, your files or your tracks), played from your MIDI keyboard's pads";
        var hid = new List<string>();
        for (int i = 0; i < N; i++) { AddP("n" + i, "Pad " + (i + 1) + " note", 0, 127, 36 + i, ""); AddP("g" + i, "Pad " + (i + 1) + " level", -24, 12, 0, "dB"); hid.Add("n" + i); hid.Add("g" + i); }
        Hidden = string.Join(",", hid);
    }
    public int PadNote(int pad) { return (int)Math.Round(Get("n" + pad)); }
    public int PadOf(int note) { for (int i = 0; i < N; i++) if (PadNote(i) == note) return i; return -1; }
    public Zone PadZone(int pad) { return ZoneAt(PadNote(pad)); }
    public string PadName(int pad) { var z = PadZone(pad); return z == null ? "" : z.Name; }
    // a sound onto a pad (it keeps its pitch, like a drum hit)
    public string SetPad(int pad, string path, string name) { return AddToKit(path, name, PadNote(pad)); }
    public void ClearPad(int pad) { RemoveKey(PadNote(pad)); }
    // a pad takes another key; a pad that had that key gets this pad's old one, so no two pads share a key
    public void MovePad(int pad, int note)
    {
        note = Math.Max(0, Math.Min(127, note)); int old = PadNote(pad); if (old == note) return; int other = PadOf(note);
        SwapKeys(old, note); Set("n" + pad, note); if (other >= 0) Set("n" + other, old);
    }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev)
    {
        if (ev != null) foreach (var kv in ev) { var m = kv.Value; if ((m.S & 0xF0) == 0x90 && m.D2 > 0) { HitAt[m.D1 & 127] = Environment.TickCount; if (LearnPad >= 0) Learned = m.D1; } }
        base.OnEvents(ev);
    }
    public override void Process(float[] l, float[] r, int n)
    {
        for (int i = 0; i < N; i++) { var z = PadZone(i); if (z != null) z.GainDb = Get("g" + i); }   // each pad's level
        base.Process(l, r, n);
    }
    public override bool HasWindow { get { return true; } }
    public override string WindowLabel { get { return "Open Purphex Pads"; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new PadsWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    public static string NoteName(int n) { return Names[((n % 12) + 12) % 12] + (n / 12 - 2) + " (" + n + ")"; }
}

#if !PURPHEX_CORE
class PadsWindow : PxWindow, ISoundDrop
{
    readonly PadsFx e; int sel, held = -1;
    public PadsWindow(PadsFx f) : base(f, "Pads", new Size(1000, 660))
    {
        e = f; Accent = Pink; AllowDrop = true;
        DragEnter += (s, a) => { a.Effect = SoundSources.AudioFiles(a.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; };
        DragOver += (s, a) => { mouse = PointToClient(new Point(a.X, a.Y)); a.Effect = SoundSources.AudioFiles(a.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; };
        DragDrop += (s, a) =>
        {   // one file onto the pad under the mouse; several fill the pads from there on
            var files = SoundSources.AudioFiles(a.Data); int pad = PadAt(PointToClient(new Point(a.X, a.Y))); if (pad < 0) pad = sel;
            for (int i = 0; i < files.Count && pad + i < PadsFx.N; i++) Put(pad + i, files[i], Path.GetFileNameWithoutExtension(files[i]));
        };
    }
    protected override string SubTitle { get { return "any sound on any pad"; } }
    public bool DropSound(Point screen, string path, string name) { int pad = PadAt(PointToClient(screen)); if (pad < 0) pad = sel; Put(pad, path, name); Activate(); return true; }
    int DragPad { get { if (SoundDrop.Hover.IsEmpty || SoundDrop.At(SoundDrop.Hover) != this) return -1; return PadAt(PointToClient(SoundDrop.Hover)); } }
    readonly RectangleF[] padRects = new RectangleF[PadsFx.N];
    internal RectangleF PadRect(int i) { return padRects[i]; }
    int PadAt(Point p) { for (int i = 0; i < PadsFx.N; i++) if (padRects[i].Contains(p)) return i; return -1; }
    void Put(int pad, string path, string name)
    {
        Mark(); string err = e.SetPad(pad, path, name); sel = pad;
        if (err != null) MessageBox.Show(this, err, Text); else { e.NoteOn(e.PadNote(pad), 105); var t = new Timer { Interval = 400 }; int n = e.PadNote(pad); t.Tick += (a, b) => { t.Stop(); t.Dispose(); e.NoteOff(n); }; t.Start(); }
    }

    protected override void PaintBody(Graphics g, RectangleF a)
    {
        if (e.LearnPad >= 0 && e.Learned >= 0) { Mark(); e.MovePad(e.LearnPad, e.Learned); sel = e.LearnPad; e.LearnPad = -1; e.Learned = -1; }
        // the top: a whole kit at once, LEARN, clear
        var top = new RectangleF(a.X, a.Y, a.Width, 56); Panel(g, top, "", Pink);
        Button(g, new RectangleF(top.X + 12, top.Y + 10, 230, 36), "KIT FROM A GENRE  ▾", false, Pink, () => KitMenu(new Point((int)top.X + 12, (int)top.Y + 46)), "Fill all 16 pads with a genre's drum kit from the sound library", 12);
        Button(g, new RectangleF(top.X + 252, top.Y + 10, 180, 36), e.LearnPad >= 0 ? "HIT A PAD ON YOUR KEYBOARD..." : "LEARN", e.LearnPad >= 0, Gold, () => e.LearnPad = e.LearnPad >= 0 ? -1 : sel, "LEARN: then hit a pad (or key) on your MIDI keyboard, and the picked pad plays from it", 11);
        Button(g, new RectangleF(top.X + 442, top.Y + 10, 120, 36), "CLEAR ALL", false, Violet, () => { if (MessageBox.Show(this, "Take the sound off every pad?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return; Mark(); for (int i = 0; i < PadsFx.N; i++) e.ClearPad(i); }, "Take the sounds off all the pads", 11);
        Button(g, new RectangleF((top.X + 572), top.Y + 10, 110, 36), "SAVE KIT", false, Lime, SaveMine, "Save these sounds as a kit of yours, with a name: its sounds are copied in, so it always works", 11);
        Button(g, new RectangleF((top.X + 572) + 118, top.Y + 10, 120, 36), "MY KITS  ▾", false, Lime, () => KitsMenu(new Point((int)((top.X + 572) + 118), (int)top.Y + 46)), "Your saved kits: pick one to load it (the arrows at the top step through them)", 11);
        Txt(g, "Drop or right-click a pad", 10.5f, Dim, new RectangleF(top.X + 822, top.Y, top.Width - 834, top.Height), StringAlignment.Near, false);
        // the 16 pads: pad 1 at the bottom left, like the pads on a MIDI keyboard
        float gw = a.Width * 0.66f, gh = a.Bottom - top.Bottom - 12, gap = 10, pw = (gw - gap * 3) / 4, ph = (gh - gap * 3) / 4; int now = Environment.TickCount;
        for (int i = 0; i < PadsFx.N; i++)
        {
            int col = i % 4, row = 3 - i / 4; var r = new RectangleF(a.X + col * (pw + gap), top.Bottom + 12 + row * (ph + gap), pw, ph); padRects[i] = r;
            int note = e.PadNote(i); string nm = e.PadName(i); bool has = nm.Length > 0, lit = held == i || unchecked(now - e.HitAt[note]) < 140, me = sel == i;
            Color c = has ? Pink : Edge; Fill(g, lit ? Color.FromArgb(240, c) : (has ? Color.FromArgb(120, Ctl) : Color.FromArgb(70, Ctl)), r, 12);
            Stroke(g, me ? Color.White : Color.FromArgb(lit ? 255 : 150, c), r, 12, me ? 2.2f : 1.3f); if (Hot(r) && !lit) Stroke(g, Color.FromArgb(120, Cyan), RectangleF.Inflate(r, 2, 2), 13, 1.2f);
            if (DragPad == i) { Fill(g, Color.FromArgb(90, Cyan), r, 12); Stroke(g, Cyan, RectangleF.Inflate(r, 3, 3), 14, 2.6f); }   // a sound dragged from the studio lands here
            Txt(g, "PAD " + (i + 1), 10, lit ? Color.Black : Dim, new RectangleF(r.X + 10, r.Y + 6, r.Width - 20, 14), StringAlignment.Near, true);
            Txt(g, has ? nm : "EMPTY", has ? 14 : 11, lit ? Color.Black : (has ? Ink : Dim), new RectangleF(r.X + 8, r.Y + r.Height / 2 - 16, r.Width - 16, 32), StringAlignment.Center, true);
            Txt(g, PadsFx.NoteName(note), 9.5f, lit ? Color.Black : Dim, new RectangleF(r.X + 10, r.Bottom - 20, r.Width - 20, 14), StringAlignment.Near, false);
            int k = i; var h = Reg(r, () => { sel = k; held = k; e.NoteOn(e.PadNote(k), 110); }, (has ? nm + ": click to play it" : "Empty pad") + ", right-click to choose its sound"); h.DragAt = p => { };
            h.Up = () => { if (held >= 0) e.NoteOff(e.PadNote(held)); held = -1; }; h.Right = () => { sel = k; SoundMenu(k); };
        }
        // the picked pad
        var side = new RectangleF(a.X + gw + 14, top.Bottom + 12, a.Right - a.X - gw - 14, gh); Panel(g, side, "PAD " + (sel + 1), Pink);
        string sn = e.PadName(sel); Txt(g, sn.Length > 0 ? sn : "No sound yet", 15, sn.Length > 0 ? Ink : Dim, new RectangleF(side.X + 14, side.Y + 28, side.Width - 28, 26), StringAlignment.Near, true);
        Button(g, new RectangleF(side.X + 14, side.Y + 62, side.Width - 28, 34), "CHOOSE ITS SOUND  ▾", true, Pink, () => SoundMenu(sel), "A sound from the library, a file on your PC, or one of your song's tracks", 11.5f);
        Button(g, new RectangleF(side.X + 14, side.Y + 104, side.Width - 28, 30), "A SOUND FILE ON MY PC...", false, Cyan, () => PickFile(sel), "Choose a WAV, MP3, FLAC, OGG, AIFF or M4A file", 10.5f);
        Knob(g, side.X + side.Width / 2, side.Y + 186, 26, "g" + sel, "LEVEL", Pink);
        Txt(g, "PLAYS FROM", 10, Dim, new RectangleF(side.X + 14, side.Y + 256, side.Width - 28, 14), StringAlignment.Near, true);
        Txt(g, PadsFx.NoteName(e.PadNote(sel)), 14, Ink, new RectangleF(side.X + 14, side.Y + 272, side.Width - 28, 22), StringAlignment.Near, true);
        float bw = (side.Width - 28 - 12) / 3; int s0 = sel;
        Button(g, new RectangleF(side.X + 14, side.Y + 300, bw, 30), "−", false, Cyan, () => { Mark(); e.MovePad(s0, e.PadNote(s0) - 1); }, "One key lower", 14);
        Button(g, new RectangleF(side.X + 14 + bw + 6, side.Y + 300, bw, 30), "+", false, Cyan, () => { Mark(); e.MovePad(s0, e.PadNote(s0) + 1); }, "One key higher", 14);
        Button(g, new RectangleF(side.X + 14 + (bw + 6) * 2, side.Y + 300, bw, 30), "LEARN", e.LearnPad == sel, Gold, () => e.LearnPad = e.LearnPad == sel ? -1 : sel, "Then hit the pad or key on your MIDI keyboard that should play this pad", 10);
        Button(g, new RectangleF(side.X + 14, side.Bottom - 44, side.Width - 28, 30), "CLEAR THIS PAD", false, Violet, () => { Mark(); e.ClearPad(s0); }, "Take the sound off this pad", 10.5f);
    }

    void PickFile(int pad) { string f = SoundSources.PickFile(this, "A sound for pad " + (pad + 1)); if (f != null) Put(pad, f, Path.GetFileNameWithoutExtension(f)); }
    void SoundMenu(int pad)
    {
        var m = Win.NewMenu(); m.Items.Add(new ToolStripMenuItem("PAD " + (pad + 1) + (e.PadName(pad).Length > 0 ? ": " + e.PadName(pad) : "")) { Enabled = false });
        SoundSources.Add(m.Items, this, (path, nm) => Put(pad, path, nm));
        m.Items.Add(new ToolStripSeparator());
        var cl = new ToolStripMenuItem("Clear this pad") { Enabled = e.PadName(pad).Length > 0 }; cl.Click += (a, b) => { Mark(); e.ClearPad(pad); }; m.Items.Add(cl);
        m.Show(this, PointToClient(Cursor.Position));
    }
    void KitsMenu(Point at)
    {
        var m = Win.NewMenu(); var mine = Presets.User(fx.Type);
        foreach (var kv in mine) { var k2 = kv; var it = new ToolStripMenuItem(kv.Key) { Checked = kv.Key == PresetName }; it.Click += (a, b) => ApplyPreset(k2.Key, k2.Value); m.Items.Add(it); }
        if (mine.Count == 0) m.Items.Add(new ToolStripMenuItem("No kits saved yet: put sounds on, then press SAVE KIT") { Enabled = false });
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Save these sounds as a new kit...").Click += (a, b) => SaveMine();
        m.Show(this, at);
    }
    void KitMenu(Point at)
    {
        var m = Win.NewMenu(); var fams = new Dictionary<string, ToolStripMenuItem>();
        for (int i = 0; i < StudioForm.PatStyles.Length; i++)
        {
            int k = i; var st = StudioForm.PatStyles[i]; ToolStripMenuItem f; if (!fams.TryGetValue(st.Family, out f)) { f = new ToolStripMenuItem(st.Family); fams[st.Family] = f; m.Items.Add(f); }
            f.DropDownItems.Add(st.Name).Click += (a, b) => LoadKit(k);
        }
        m.Show(this, at);
    }
    // a genre's kit on the 16 pads: kicks, snares, claps, hats, percussion, an 808, a cymbal, FX
    void LoadKit(int style)
    {
        var rec = SoundCatalog.Find(StudioForm.PackOfStyle(style)); if (rec == null) { MessageBox.Show(this, "That genre has no kit in the library.", Text); return; }
        var items = InstantLib.ItemsOf(rec.Id); string[] plan = { "ok|0", "os|0", "oc|0", "oh|0", "oo|0", "op|0", "o8|0", "ok|1", "os|1", "oc|1", "oh|1", "op|1", "op|2", "oy|0", "of|0", "ov|0" };
        Mark(); int put = 0; Cursor = Cursors.WaitCursor;
        try
        {
            for (int i = 0; i < PadsFx.N; i++)
            {
                string code = plan[i].Substring(0, 3); int nth = plan[i][3] - '0'; var it = items.Where(x => x.Spec.StartsWith(code)).Skip(nth).FirstOrDefault();
                if (it == null) { e.ClearPad(i); continue; } if (InstantLib.Ensure(it) != null) continue;
                if (e.SetPad(i, it.Path, it.Name.Replace(it.PackName + " - ", "")) == null) put++;
            }
        }
        finally { Cursor = Cursors.Default; }
        if (put == 0) MessageBox.Show(this, "Nothing could be loaded from that kit.", Text); else { PresetName = StudioForm.PatStyles[style].Name + " kit"; fx.CurPreset = ""; }   // (SAVE KIT offers this name, numbered if you have one already)
    }
}
// A sound dragged from the studio (SOUNDS, MY SOUNDS) and let go over Purphex Pads or Key Map: the studio keeps the mouse while it drags,
// so it finds the window under the mouse and hands the sound over. While it drags, Hover says where the mouse is, and the window lights what is under it.
interface ISoundDrop { bool DropSound(Point screen, string path, string name); }
static class SoundDrop
{
    public static Point Hover = Point.Empty;   // the mouse on the screen while a sound is dragged from the studio (Empty: no drag)
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    public static ISoundDrop At(Point screen)
    {
        try { var c = Control.FromChildHandle(WindowFromPoint(screen)); var f = c == null ? null : (c as Form ?? c.FindForm()); return f as ISoundDrop; } catch { return null; }
    }
}

// Where a sound for a pad or a key can come from (Purphex Pads and Purphex Key Map): a file on this PC, the sound library
// (kind > pack > sound, filled in when opened: the library has thousands), or a track of the song (one hit of its instrument, or its part as audio)
static class SoundSources
{
    static readonly string[][] Kinds = { new[] { "ok", "Kicks" }, new[] { "os", "Snares" }, new[] { "oc", "Claps" }, new[] { "oh", "Hi-hats" }, new[] { "oo", "Open hats" }, new[] { "op", "Percussion" }, new[] { "oy", "Cymbals" }, new[] { "o8", "808s" }, new[] { "ob", "Bass hits" }, new[] { "on", "Instrument hits" }, new[] { "och", "Chord hits" }, new[] { "of", "FX" }, new[] { "ov", "Vocal shouts" } };
    public static bool IsAudio(string f) { string x = Path.GetExtension(f).ToLowerInvariant(); return x == ".wav" || x == ".mp3" || x == ".flac" || x == ".ogg" || x == ".aif" || x == ".aiff" || x == ".m4a"; }
    public static List<string> AudioFiles(IDataObject d) { var l = new List<string>(); var fs = d.GetData(DataFormats.FileDrop) as string[]; if (fs != null) foreach (var f in fs) if (IsAudio(f)) l.Add(f); return l; }
    public static string PickFile(IWin32Window owner, string title)
    {
        using (var d = new OpenFileDialog { Title = title, Filter = "Sounds|*.wav;*.mp3;*.flac;*.ogg;*.aif;*.aiff;*.m4a|All files|*.*" }) return d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
    }
    public static void Add(ToolStripItemCollection items, Form w, Action<string, string> put)
    {
        var file = new ToolStripMenuItem("A sound file on my PC..."); file.Click += (a, b) => { string f = PickFile(w, "Choose a sound"); if (f != null) put(f, Path.GetFileNameWithoutExtension(f)); }; items.Add(file);
        var lib = new ToolStripMenuItem("From the sound library"); items.Add(lib);
        foreach (var kd in Kinds)
        {
            string code = kd[0] + "|"; var ki = new ToolStripMenuItem(kd[1]); ki.DropDownItems.Add(new ToolStripMenuItem("...") { Enabled = false }); lib.DropDownItems.Add(ki);
            ki.DropDownOpening += (s, a2) =>
            {
                if (ki.Tag != null) return; ki.Tag = 1; ki.DropDownItems.Clear();
                foreach (var grp in InstantLib.Items.Where(x => x.Spec.StartsWith(code)).GroupBy(x => x.PackName).OrderBy(gq => gq.Key))
                {
                    var pi = new ToolStripMenuItem(grp.Key.Length > 0 ? grp.Key : "Sounds"); ki.DropDownItems.Add(pi);
                    foreach (var it0 in grp) { var it = it0; string nm = it.Name.Replace(it.PackName + " - ", ""); pi.DropDownItems.Add(nm).Click += (a3, b3) => { string err = InstantLib.Ensure(it); if (err != null) { MessageBox.Show(w, err, w.Text); return; } put(it.Path, nm); }; }
                }
                if (ki.DropDownItems.Count == 0) ki.DropDownItems.Add(new ToolStripMenuItem("(none in the library)") { Enabled = false });
            };
        }
        var sf = w.Owner as StudioForm; var tr = new ToolStripMenuItem("One of your song's tracks"); items.Add(tr);
        Action<Track, bool> from = (t, oneHit) => { string path, nm; string err = sf.TrackToSound(t, oneHit, out path, out nm); if (err != null) { MessageBox.Show(w, err, w.Text); return; } put(path, nm); };
        if (sf != null) foreach (var t0 in sf.eng.Tracks)
        {
            var t = t0; if (t.Host != null || t.IsBus) continue;
            if (t.Instrument) { var ti = new ToolStripMenuItem(t.Name); ti.DropDownItems.Add("One hit of its instrument").Click += (a, b) => from(t, true); ti.DropDownItems.Add("Its whole part as audio").Click += (a, b) => from(t, false); tr.DropDownItems.Add(ti); }
            else if (t.Clip != null) tr.DropDownItems.Add(t.Name).Click += (a, b) => from(t, false);
        }
        if (tr.DropDownItems.Count == 0) tr.DropDownItems.Add(new ToolStripMenuItem("(no tracks with sound yet)") { Enabled = false });
    }
}
#endif

