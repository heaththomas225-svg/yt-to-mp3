using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Purphex Key Map: any sound on any key of your MIDI keyboard. A sound sits on one key (it keeps its pitch, like a drum hit),
// or plays across a range of keys, pitched from the key it was put on (an octave each side, or the whole keyboard).
// It is the sampler underneath, so it plays from the piano roll and saves with the project like it.
class KeyMapFx : SamplerFx
{
    public readonly int[] HitAt = new int[128]; public volatile int LastNote = -1;   // the window lights the keys and can follow your playing
    public KeyMapFx() { Type = "pxkeymap"; Title = "Purphex Key Map"; Category = "Instruments"; Blurb = "Any sound on any key of your MIDI keyboard, on one key or played across a range of keys"; }
    // the sound a key plays (a range covering it counts), and the zone that gives it
    public Zone ZoneOf(int key) { return ZoneFor(key); }
    public string KeyName(int key) { var z = ZoneFor(key); return z == null ? "" : z.Name; }
    // a sound on this key only
    public string SetKey(int key, string path, string name) { CutRange(key, key); return AddToKit(path, name, key); }
    // a sound across lo..hi, pitched from root (the keys inside lose what they had)
    public string SetRange(int lo, int hi, int root, string path, string name)
    {
        lo = Math.Max(0, lo); hi = Math.Min(127, hi); var z = new Zone { Path = path, Name = name, Lo = lo, Hi = hi, Root = root, Follow = true, OneShot = false };
        string err = LoadInto(z); if (err != null) return err; CutRange(lo, hi); lock (zl) Zones.Add(z); return null;
    }
    // what a key plays spread over lo..hi (or brought back to its key alone), the sound kept
    public string Spread(int key, int lo, int hi) { var z = ZoneFor(key); if (z == null) return "That key has no sound yet."; int root = z.Lo == z.Hi ? z.Lo : z.Root; return lo == hi ? SetKey(lo, z.Path, z.Name) : SetRange(lo, hi, root, z.Path, z.Name); }
    public void ClearKey(int key) { var z = ZoneFor(key); if (z != null) lock (zl) Zones.Remove(z); }
    public void ClearAll() { lock (zl) Zones.Clear(); }
    public void Nudge(int key, float db) { var z = ZoneFor(key); if (z != null) z.GainDb = Math.Max(-24, Math.Min(12, z.GainDb + db)); }
    // a new sound cuts what was under it: one-key sounds there go, a range is trimmed back to the part outside (a range in the middle keeps its lower part)
    void CutRange(int lo, int hi)
    {
        lock (zl)
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                var z = Zones[i]; if (z.Hi < lo || z.Lo > hi) continue;
                if (z.Lo >= lo && z.Hi <= hi) { Zones.RemoveAt(i); continue; }
                if (z.Lo < lo) z.Hi = lo - 1; else z.Lo = hi + 1;
            }
    }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev)
    {
        if (ev != null) foreach (var kv in ev) { var m = kv.Value; if ((m.S & 0xF0) == 0x90 && m.D2 > 0) { HitAt[m.D1 & 127] = Environment.TickCount; LastNote = m.D1; } }
        base.OnEvents(ev);
    }
    public override bool HasWindow { get { return true; } }
    public override string WindowLabel { get { return "Open Purphex Key Map"; } }
    public static string NoteLabel(int n) { return PadsFx.NoteName(n).Split(' ')[0]; }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new KeyMapWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
}

#if !PURPHEX_CORE
class KeyMapWindow : PxWindow, ISoundDrop
{
    readonly KeyMapFx e; int sel = 60, held = -1, low = 36, lastSeen = -1; bool follow = true;   // low: the first key shown (5 octaves from there)
    static readonly bool[] Black = { false, true, false, true, false, false, true, false, true, false, true, false };
    readonly List<KeyValuePair<int, RectangleF>> keyRects = new List<KeyValuePair<int, RectangleF>>();
    public KeyMapWindow(KeyMapFx f) : base(f, "Key Map", new Size(1080, 600))
    {
        e = f; Accent = Cyan; AllowDrop = true; MidiWindow.Hook(this, f);
        DragEnter += (s, a) => { a.Effect = SoundSources.AudioFiles(a.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; };
        DragOver += (s, a) => { mouse = PointToClient(new Point(a.X, a.Y)); a.Effect = SoundSources.AudioFiles(a.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; };
        DragDrop += (s, a) =>
        {   // one file onto the key under the mouse; several go on the next white keys from there
            var files = SoundSources.AudioFiles(a.Data); int k = KeyAt(PointToClient(new Point(a.X, a.Y))); if (k < 0) k = sel;
            foreach (var f2 in files) { if (k > 127) break; PutKey(k, f2, Path.GetFileNameWithoutExtension(f2)); k++; while (k <= 127 && Black[k % 12]) k++; }
        };
    }
    protected override string SubTitle { get { return "any sound on any key"; } }
    public bool DropSound(Point screen, string path, string name) { int k = KeyAt(PointToClient(screen)); if (k < 0) k = sel; PutKey(k, path, name); Activate(); return true; }
    int DragKey { get { if (SoundDrop.Hover.IsEmpty || SoundDrop.At(SoundDrop.Hover) != this) return -1; return KeyAt(PointToClient(SoundDrop.Hover)); } }
    internal RectangleF KeyRect(int key) { foreach (var kv in keyRects) if (kv.Key == key) return kv.Value; return RectangleF.Empty; }
    int KeyAt(Point p) { for (int i = keyRects.Count - 1; i >= 0; i--) if (keyRects[i].Value.Contains(p)) return keyRects[i].Key; return -1; }   // (black keys are drawn last, so they win)
    void PutKey(int key, string path, string name) { Mark(); string err = e.SetKey(key, path, name); sel = key; if (err != null) MessageBox.Show(this, err, Text); else Audition(key); }
    void Audition(int key) { e.NoteOn(key, 105); var t = new Timer { Interval = 450 }; t.Tick += (a, b) => { t.Stop(); t.Dispose(); e.NoteOff(key); }; t.Start(); }
    static Color ColourOf(string name) { int h = 0; foreach (char c in name) h = h * 31 + c; Color[] cs = { Pink, Cyan, Gold, Lime, Violet, Orange }; return cs[((h % cs.Length) + cs.Length) % cs.Length]; }

    protected override void PaintBody(Graphics g, RectangleF a)
    {
        int ln = e.LastNote; if (follow && ln >= 0 && ln != lastSeen) { lastSeen = ln; sel = ln; if (ln < low) low = Math.Max(0, ln / 12 * 12); else if (ln >= low + 60) low = Math.Min(67, ln / 12 * 12 - 48); }
        // the top: which keys are shown, follow your playing, clear
        var top = new RectangleF(a.X, a.Y, a.Width, 56); Panel(g, top, "", Cyan);
        Button(g, new RectangleF(top.X + 12, top.Y + 10, 44, 36), "◀", false, Cyan, () => low = Math.Max(0, low - 12), "Show lower keys", 14);
        Txt(g, KeyMapFx.NoteLabel(low) + " to " + KeyMapFx.NoteLabel(low + 59), 12, Ink, new RectangleF(top.X + 60, top.Y, 150, top.Height), StringAlignment.Center, true);
        Button(g, new RectangleF(top.X + 214, top.Y + 10, 44, 36), "▶", false, Cyan, () => low = Math.Min(67, low + 12), "Show higher keys", 14);
        Button(g, new RectangleF(top.X + 272, top.Y + 10, 210, 36), follow ? "PICK BY PLAYING: ON" : "PICK BY PLAYING: OFF", follow, Gold, () => follow = !follow, "On: the key you play on your MIDI keyboard is picked, ready for a sound", 10.5f);
        Button(g, new RectangleF(top.X + 492, top.Y + 10, 110, 36), "CLEAR ALL", false, Violet, () => { if (MessageBox.Show(this, "Take the sound off every key?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return; Mark(); e.ClearAll(); }, "Take every sound off the keyboard", 11);
        Button(g, new RectangleF((top.X + 612), top.Y + 10, 110, 36), "SAVE MAP", false, Lime, SaveMine, "Save these sounds as a key map of yours, with a name: its sounds are copied in, so it always works", 11);
        Button(g, new RectangleF((top.X + 612) + 118, top.Y + 10, 120, 36), "MY MAPS  ▾", false, Lime, () => KitsMenu(new Point((int)((top.X + 612) + 118), (int)top.Y + 46)), "Your saved key maps: pick one to load it (the arrows at the top step through them)", 11);
        Txt(g, "Drop sounds on a key, or right-click a key", 10.5f, Dim, new RectangleF(top.X + 862, top.Y, top.Width - 874, top.Height), StringAlignment.Near, false);
        // the keyboard: 5 octaves, the sounds' names above the keys they play
        var kb = new RectangleF(a.X, top.Bottom + 12, a.Width, 250); Panel(g, kb, "", Cyan); keyRects.Clear();
        float lane = 44, kx = kb.X + 12, ky = kb.Y + 12 + lane, kw = (kb.Width - 24) / 35f, kh = kb.Height - 24 - lane; int now = Environment.TickCount;
        var whites = new List<int>(); for (int n = low; n < low + 60 && n <= 127; n++) if (!Black[n % 12]) whites.Add(n);
        Func<int, float> xOf = n => { int wi = whites.IndexOf(Black[n % 12] ? n - 1 : n); return kx + Math.Max(0, wi) * kw + (Black[n % 12] ? kw * 0.68f : 0); };
        // the lane: one bar per sound over the keys it plays
        var seen = new HashSet<SamplerFx.Zone>();
        for (int n = low; n < low + 60 && n <= 127; n++)
        {
            var z = e.ZoneOf(n); if (z == null || !seen.Add(z)) continue; int lo = Math.Max(low, z.Lo), hi = Math.Min(low + 59, z.Hi);
            float x0 = xOf(lo) + (Black[lo % 12] ? 0 : 1), x1 = xOf(hi) + (Black[hi % 12] ? kw * 0.64f : kw) - 1; Color c = ColourOf(z.Name); var bar = new RectangleF(x0, kb.Y + 12, Math.Max(10, x1 - x0), lane - 6);
            Fill(g, Color.FromArgb(150, c), bar, 6); Txt(g, z.Name + (z.Lo != z.Hi ? "  (pitched)" : ""), 10, Color.Black, new RectangleF(bar.X + 4, bar.Y, bar.Width - 8, bar.Height), StringAlignment.Center, true);
            var zz = z; Reg(bar, () => { sel = zz.Lo == zz.Hi ? zz.Lo : zz.Root; Audition(sel); }, zz.Name + (zz.Lo == zz.Hi ? " on " + KeyMapFx.NoteLabel(zz.Lo) : " from " + KeyMapFx.NoteLabel(zz.Lo) + " to " + KeyMapFx.NoteLabel(zz.Hi) + ", pitched from " + KeyMapFx.NoteLabel(zz.Root))).Right = () => { sel = zz.Lo == zz.Hi ? zz.Lo : zz.Root; KeyMenu(sel); };
        }
        foreach (bool blackPass in new[] { false, true })
            for (int n = low; n < low + 60 && n <= 127; n++)
            {
                if (Black[n % 12] != blackPass) continue; float x = xOf(n); var r = blackPass ? new RectangleF(x, ky, kw * 0.64f, kh * 0.62f) : new RectangleF(x + 1, ky, kw - 2, kh);
                var z = e.ZoneOf(n); bool lit = held == n || unchecked(now - e.HitAt[n]) < 150, me = sel == n; Color c = z != null ? ColourOf(z.Name) : Edge;
                Color body = lit ? Color.FromArgb(255, c) : (z != null ? Mix(blackPass ? Color.FromArgb(30, 20, 50) : Color.FromArgb(225, 220, 240), c, blackPass ? 0.55 : 0.45) : (blackPass ? Color.FromArgb(24, 16, 40) : Color.FromArgb(225, 220, 240)));
                Fill(g, body, r, 4); Stroke(g, me ? Color.White : Color.FromArgb(blackPass ? 180 : 120, me ? Color.White : Edge), r, 4, me ? 2.4f : 1f);
                if (me) Stroke(g, Cyan, RectangleF.Inflate(r, 2, 2), 5, 1.6f);
                if (DragKey == n) { Fill(g, Color.FromArgb(150, Cyan), r, 4); Stroke(g, Cyan, RectangleF.Inflate(r, 3, 3), 6, 2.6f); }   // a sound dragged from the studio lands here
                if (!blackPass && n % 12 == 0) Txt(g, KeyMapFx.NoteLabel(n), 9, Color.FromArgb(90, 70, 130), new RectangleF(r.X, r.Bottom - 18, r.Width, 14), StringAlignment.Center, true);
                int k = n; keyRects.Add(new KeyValuePair<int, RectangleF>(n, r)); var h = Reg(r, () => { sel = k; held = k; e.NoteOn(k, 110); }, KeyMapFx.NoteLabel(n) + (z != null ? ": " + z.Name : ": no sound") + ". Click to play, right-click to choose its sound");
                h.DragAt = p => { }; h.Up = () => { if (held >= 0) e.NoteOff(held); held = -1; }; h.Right = () => { sel = k; KeyMenu(k); };
            }
        // the picked key
        var pk = new RectangleF(a.X, kb.Bottom + 12, a.Width, a.Bottom - kb.Bottom - 12); Panel(g, pk, "KEY " + KeyMapFx.NoteLabel(sel), Cyan);
        var zs = e.ZoneOf(sel); string sn = zs == null ? "No sound on this key yet" : zs.Name + (zs.Lo != zs.Hi ? "   (pitched, " + KeyMapFx.NoteLabel(zs.Lo) + " to " + KeyMapFx.NoteLabel(zs.Hi) + ")" : "");
        Txt(g, sn, 14, zs == null ? Dim : Ink, new RectangleF(pk.X + 14, pk.Y + 26, pk.Width * 0.45f, 24), StringAlignment.Near, true);
        int s0 = sel; float bx = pk.X + 14, by = pk.Y + 58;
        Button(g, new RectangleF(bx, by, 196, 34), "CHOOSE ITS SOUND  ▾", true, Cyan, () => KeyMenu(s0), "A sound from the library, a file on your PC, or one of your song's tracks", 11.5f);
        Button(g, new RectangleF(bx + 204, by, 186, 34), "A SOUND FILE ON MY PC...", false, Pink, () => { string f = SoundSources.PickFile(this, "A sound for " + KeyMapFx.NoteLabel(s0)); if (f != null) PutKey(s0, f, Path.GetFileNameWithoutExtension(f)); }, "Choose a WAV, MP3, FLAC, OGG, AIFF or M4A file", 10.5f);
        Button(g, new RectangleF(bx + 398, by, 140, 34), "CLEAR THIS KEY", false, Violet, () => { Mark(); e.ClearKey(s0); }, zs != null && zs.Lo != zs.Hi ? "Take this sound off all the keys it plays" : "Take the sound off this key", 10.5f);
        // play it across: this key only, an octave each side, the whole keyboard (pitched)
        float sx = Math.Max(pk.X + pk.Width * 0.5f + 20, bx + 556);   /* (right of the three buttons, never over them) */ Txt(g, "PLAY IT ACROSS", 10, Dim, new RectangleF(sx, pk.Y + 26, 300, 14), StringAlignment.Near, true);
        string[] spans = { "THIS KEY ONLY", "AN OCTAVE EACH SIDE", "THE WHOLE KEYBOARD" }; float sw = (a.Right - sx - 14 - 12) / 3;
        int span = zs == null ? -1 : (zs.Lo == zs.Hi ? 0 : (zs.Lo <= 0 && zs.Hi >= 127 ? 2 : 1));
        for (int i = 0; i < 3; i++)
        {
            int ii = i; Button(g, new RectangleF(sx + i * (sw + 6), pk.Y + 44, sw, 34), spans[i], span == i, Gold, () =>
            {
                if (e.ZoneOf(s0) == null) { MessageBox.Show(this, "Give this key a sound first.", Text); return; } Mark(); var z0 = e.ZoneOf(s0); int root = z0.Lo == z0.Hi ? z0.Lo : z0.Root;
                string err = ii == 0 ? e.Spread(s0, root, root) : (ii == 1 ? e.Spread(s0, root - 12, root + 12) : e.Spread(s0, 0, 127)); if (err != null) MessageBox.Show(this, err, Text); else { sel = root; Audition(root); }
            }, ii == 0 ? "The sound on its key alone, at its own pitch" : "The sound pitched up and down the keys, from this key (a melody from one sound)", 9.5f);
        }
        if (zs != null)
        {
            Txt(g, "LEVEL  " + (zs.GainDb > 0 ? "+" : "") + zs.GainDb.ToString("0.#") + " dB", 11, Ink, new RectangleF(sx, pk.Y + 90, 160, 30), StringAlignment.Near, true);
            Button(g, new RectangleF(sx + 170, pk.Y + 90, 44, 30), "−", false, Cyan, () => { Mark(); e.Nudge(s0, -1.5f); }, "Quieter", 14);
            Button(g, new RectangleF(sx + 220, pk.Y + 90, 44, 30), "+", false, Cyan, () => { Mark(); e.Nudge(s0, 1.5f); }, "Louder", 14);
        }
    }
    void KitsMenu(Point at)
    {
        var m = Win.NewMenu(); var mine = Presets.User(fx.Type);
        foreach (var kv in mine) { var k2 = kv; var it = new ToolStripMenuItem(kv.Key) { Checked = kv.Key == PresetName }; it.Click += (a, b) => ApplyPreset(k2.Key, k2.Value); m.Items.Add(it); }
        if (mine.Count == 0) m.Items.Add(new ToolStripMenuItem("No key maps saved yet: put sounds on, then press SAVE MAP") { Enabled = false });
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Save these sounds as a new key map...").Click += (a, b) => SaveMine();
        m.Show(this, at);
    }
    void KeyMenu(int key)
    {
        var m = Win.NewMenu(); string nm = e.KeyName(key); m.Items.Add(new ToolStripMenuItem("KEY " + KeyMapFx.NoteLabel(key) + (nm.Length > 0 ? ": " + nm : "")) { Enabled = false });
        SoundSources.Add(m.Items, this, (path, n2) => PutKey(key, path, n2));
        m.Items.Add(new ToolStripSeparator());
        var cl = new ToolStripMenuItem("Clear this key") { Enabled = nm.Length > 0 }; cl.Click += (a, b) => { Mark(); e.ClearKey(key); }; m.Items.Add(cl);
        m.Show(this, PointToClient(Cursor.Position));
    }
}
#endif
