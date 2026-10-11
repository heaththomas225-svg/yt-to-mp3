using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Piano roll: draw, move, resize and delete the notes of an instrument track. Changes play back at once.
class PianoRollForm : Form
{
    public class N { public long Start, Len; public int Note, Vel = 100; }
    readonly Track track; readonly InstrumentFx inst; readonly StudioEngine eng; readonly Action togglePlay; readonly RollCanvas canvas;
    public PianoRollForm(Track t, InstrumentFx inst, StudioEngine eng, Action togglePlay)
    {
        track = t; this.inst = inst; this.eng = eng; this.togglePlay = togglePlay; Text = "Piano roll: " + t.Name; BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255);
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1100, 620); MinimumSize = new Size(700, 420); KeyPreview = true; Win.Dark(this);
        canvas = new RollCanvas(t, inst, eng) { Dock = DockStyle.Fill }; Controls.Add(canvas);
        var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = TM.C(255, 18, 9, 34) }; Controls.Add(bar);
        int x = 10;
        MakeBtn(bar, ref x, "Play / Stop", 90, (s, e) => togglePlay(), TM.C(255, 130, 45, 220));
        var snap = new ComboBox { Left = x, Top = 8, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = TM.C(255, 24, 12, 44), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; snap.Items.AddRange(RollCanvas.GridNames); snap.SelectedIndex = canvas.GridIdx; snap.SelectedIndexChanged += (s, e) => { canvas.GridIdx = snap.SelectedIndex; canvas.Invalidate(); }; bar.Controls.Add(snap); x += 128;
        MakeBtn(bar, ref x, "Snap notes to grid", 130, (s, e) => canvas.Quantize(), TM.C(255, 70, 40, 120));
        MakeBtn(bar, ref x, "Clear all notes", 120, (s, e) => { if (MessageBox.Show(this, "Delete every note on this track?", "Piano roll", MessageBoxButtons.YesNo) == DialogResult.Yes) canvas.ClearAll(); }, TM.C(255, 150, 40, 80));
        MakeBtn(bar, ref x, "Close", 70, (s, e) => Close(), TM.C(255, 70, 40, 120));
        bar.Controls.Add(new Label { Text = "Drag on the grid to draw a note. Drag a note to move it, drag its right end to resize it, right-click to delete. Click the piano keys to hear them. Mouse wheel scrolls, Shift+wheel moves sideways, Ctrl+wheel zooms.", Left = x + 10, Top = 4, Width = 900, Height = 34, ForeColor = TM.C(255, 160, 140, 200), Font = new Font("Segoe UI", 8.5f) });
        var tm = new Timer { Interval = 33 }; tm.Tick += (s, e) => { if (eng.Playing || canvas.Busy) canvas.Invalidate(); }; tm.Start(); FormClosed += (s, e) => { tm.Stop(); canvas.Silence(); };
    }
    static Button MakeBtn(Panel bar, ref int x, string text, int w, EventHandler h, Color c)
    {
        var b = new Button { Text = text, Left = x, Top = 6, Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = TM.C(255, 200, 150, 255); b.Click += h; bar.Controls.Add(b); x += w + 6; return b;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (k == Keys.Space) { togglePlay(); return true; } if (k == Keys.Delete) { canvas.DeleteSelected(); return true; } if (k == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }
}

class RollCanvas : Control
{
    public static readonly string[] GridNames = { "Snap: 1 bar", "Snap: 1 beat", "Snap: 1/8 beat", "Snap: 1/16 beat", "Snap: 1/32 beat" }; static readonly double[] GridBeats = { 4, 1, 0.5, 0.25, 0.125 };
    readonly Track track; readonly InstrumentFx inst; readonly StudioEngine eng; readonly List<PianoRollForm.N> notes = new List<PianoRollForm.N>();
    public int GridIdx = 1; double ppb = 56; int rowH = 16, scrollX, scrollY; const int KeyW = 70, RulerH = 24, LowNote = 24, HighNote = 108;
    PianoRollForm.N sel; int mode; long grab; int origNote; int previewNote = -1; public bool Busy;   // mode: 0 none, 1 create/resize, 2 move
    static Color Bg { get { return TM.C(255, 14, 8, 28); } } static Color Acc { get { return TM.C(255, 188, 120, 255); } } static Color Acc2 { get { return TM.C(255, 236, 92, 226); } } static Color Ink { get { return TM.C(255, 240, 232, 255); } } static Color Dim { get { return TM.C(255, 150, 132, 190); } }

    public RollCanvas(Track t, InstrumentFx inst, StudioEngine eng)
    {
        track = t; this.inst = inst; this.eng = eng; DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Load(); int lo = 127, hi = 0; foreach (var n in notes) { lo = Math.Min(lo, n.Note); hi = Math.Max(hi, n.Note); }
        int mid = notes.Count > 0 ? (lo + hi) / 2 : 60; scrollY = (HighNote - mid) * rowH - 200; if (scrollY < 0) scrollY = 0;
    }
    double BeatFrames { get { return StudioEngine.SR * 60.0 / Math.Max(20, Fx.ProjectBpm); } }
    double GridFrames { get { return BeatFrames * GridBeats[GridIdx]; } }
    long Snap(long f) { double g = GridFrames; return (long)(Math.Round(f / g) * g); }
    float XOf(long frame) { return KeyW + (float)(frame / BeatFrames * ppb) - scrollX; }
    long FrameAt(float x) { return Math.Max(0, (long)((x - KeyW + scrollX) / ppb * BeatFrames)); }
    float YOf(int note) { return RulerH + (HighNote - note) * rowH - scrollY; }
    int NoteAt(float y) { return HighNote - (int)Math.Floor((y - RulerH + scrollY) / (double)rowH); }
    static bool Black(int n) { int s = n % 12; return s == 1 || s == 3 || s == 6 || s == 8 || s == 10; }

    // ---- notes <-> the track's MIDI events ----
    void Load()
    {
        notes.Clear(); var open = new Dictionary<int, PianoRollForm.N>();
        lock (track.Midi)
            foreach (var m in track.Midi)
            {
                int k = m.S & 0xF0;
                if (k == 0x90 && m.D2 > 0) { var n = new PianoRollForm.N { Start = m.Frame, Note = m.D1, Vel = m.D2, Len = StudioEngine.SR / 4 }; notes.Add(n); open[m.D1] = n; }
                else if (k == 0x80 || k == 0x90) { PianoRollForm.N n; if (open.TryGetValue(m.D1, out n)) { n.Len = Math.Max(200, m.Frame - n.Start); open.Remove(m.D1); } }
            }
    }
    void Commit()
    {
        var ev = new List<MidiEv>();
        foreach (var n in notes) { ev.Add(new MidiEv { Frame = n.Start, S = 0x90, D1 = (byte)n.Note, D2 = (byte)n.Vel }); ev.Add(new MidiEv { Frame = n.Start + n.Len, S = 0x80, D1 = (byte)n.Note, D2 = 0 }); }
        ev.Sort((a, b) => { int c = a.Frame.CompareTo(b.Frame); return c != 0 ? c : a.S.CompareTo(b.S); });   // at the same moment a note ends before the next one starts
        lock (track.Midi) { track.Midi.Clear(); track.Midi.AddRange(ev); }
    }
    public void Quantize() { foreach (var n in notes) { n.Start = Snap(n.Start); n.Len = Math.Max((long)GridFrames, Snap(n.Len)); } Commit(); Invalidate(); }
    public void ClearAll() { notes.Clear(); sel = null; Commit(); Invalidate(); }
    public void DeleteSelected() { if (sel != null) { notes.Remove(sel); sel = null; Commit(); Invalidate(); } }
    public void Silence() { if (previewNote >= 0) { inst.NoteOff(previewNote); previewNote = -1; } }
    void Preview(int note) { if (note == previewNote) return; Silence(); if (note >= LowNote && note <= HighNote) { previewNote = note; inst.NoteOn(note, 100); } }

    // ---- drawing ----
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.None; g.Clear(Bg); int W = ClientSize.Width, H = ClientSize.Height;
        int rows = HighNote - LowNote + 1; scrollY = Math.Max(0, Math.Min(Math.Max(0, rows * rowH - (H - RulerH)), scrollY)); scrollX = Math.Max(0, scrollX);
        g.SetClip(new Rectangle(KeyW, RulerH, W - KeyW, H - RulerH));
        for (int n = HighNote; n >= LowNote; n--) { float y = YOf(n); if (y + rowH < RulerH || y > H) continue; using (var b = new SolidBrush(Black(n) ? TM.C(255, 18, 10, 34) : TM.C(255, 24, 14, 44))) g.FillRectangle(b, KeyW, y, W - KeyW, rowH); if (n % 12 == 0) using (var p = new Pen(TM.C(70, 140, 100, 200))) g.DrawLine(p, KeyW, y + rowH, W, y + rowH); }
        double bf = BeatFrames, step = Math.Max(0.125, GridBeats[GridIdx]); while (step * ppb < 10) step *= 2;
        using (var thin = new Pen(TM.C(34, 190, 150, 255))) using (var thick = new Pen(TM.C(90, 210, 170, 255)))
            for (double beat = Math.Floor(scrollX / ppb / step) * step; ; beat += step)
            {
                float x = KeyW + (float)(beat * ppb) - scrollX; if (x > W) break; if (x < KeyW) continue; bool bar = Math.Abs(beat / 4 - Math.Round(beat / 4)) < 1e-6; g.DrawLine(bar ? thick : thin, x, RulerH, x, H);
            }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var n in notes)
        {
            float x0 = XOf(n.Start), x1 = XOf(n.Start + n.Len), y = YOf(n.Note); if (x1 < KeyW || x0 > W) continue; var r = new RectangleF(x0, y + 1, Math.Max(4, x1 - x0), rowH - 2);
            int a = 150 + n.Vel / 3; using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, a), n == sel ? Acc2 : Acc))) g.FillRectangle(b, r); using (var p = new Pen(n == sel ? Color.White : TM.C(220, 255, 220, 255), 1f)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        }
        long pos = eng.Pos; float px = XOf(pos); if (px >= KeyW && px <= W) using (var p = new Pen(TM.C(240, 236, 92, 226), 2f)) g.DrawLine(p, px, RulerH, px, H);
        g.ResetClip();
        // ruler
        using (var b = new SolidBrush(TM.C(240, 22, 12, 40))) g.FillRectangle(b, KeyW, 0, W - KeyW, RulerH);
        using (var f = new Font("Segoe UI", 9f)) using (var tb = new SolidBrush(Dim))
            for (double beat = Math.Floor(scrollX / ppb / 4) * 4; ; beat += 4) { float x = KeyW + (float)(beat * ppb) - scrollX; if (x > W) break; if (x < KeyW - 40) continue; g.DrawString(((int)(beat / 4 + 1)).ToString(), f, tb, x + 3, 4); }
        // piano keys
        using (var f = new Font("Segoe UI", 8f))
            for (int n = HighNote; n >= LowNote; n--)
            {
                float y = YOf(n); if (y + rowH < RulerH || y > H) continue; bool bk = Black(n); bool on = n == previewNote;
                using (var b = new SolidBrush(on ? Acc2 : (bk ? TM.C(255, 20, 12, 36) : TM.C(255, 232, 226, 248)))) g.FillRectangle(b, 0, y, bk ? KeyW * 0.62f : KeyW, rowH);
                if (!bk) using (var p = new Pen(TM.C(120, 90, 60, 140))) g.DrawLine(p, 0, y + rowH, KeyW, y + rowH);
                if (n % 12 == 0) g.DrawString("C" + (n / 12 - 1).ToString(), f, Brushes.Black, KeyW - 26, y + 1);
            }
        using (var p = new Pen(TM.C(120, 140, 100, 200))) { g.DrawLine(p, KeyW, 0, KeyW, H); g.DrawLine(p, 0, RulerH, W, RulerH); }
    }

    // ---- mouse ----
    PianoRollForm.N Hit(float x, float y, out bool rightEdge)
    {
        rightEdge = false; for (int i = notes.Count - 1; i >= 0; i--) { var n = notes[i]; float x0 = XOf(n.Start), x1 = XOf(n.Start + n.Len), ny = YOf(n.Note); if (y >= ny && y < ny + rowH && x >= x0 && x <= x1) { rightEdge = x1 - x0 > 14 && x > x1 - 7; return n; } }
        return null;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (e.X < KeyW) { if (e.Y > RulerH) { Preview(NoteAt(e.Y)); Busy = true; Invalidate(); } return; }
        if (e.Y < RulerH) { eng.Seek(Math.Max(0, FrameAt(e.X))); Invalidate(); return; }
        bool re; var hit = Hit(e.X, e.Y, out re);
        if (e.Button == MouseButtons.Right) { if (hit != null) { notes.Remove(hit); if (sel == hit) sel = null; Commit(); Invalidate(); } return; }
        if (hit != null) { sel = hit; if (re) mode = 1; else { mode = 2; grab = FrameAt(e.X) - hit.Start; origNote = hit.Note; } Preview(hit.Note); }
        else { long s = Snap(FrameAt(e.X)); int nn = NoteAt(e.Y); if (nn < LowNote || nn > HighNote) return; var n = new PianoRollForm.N { Start = s, Len = (long)GridFrames, Note = nn, Vel = 100 }; notes.Add(n); sel = n; mode = 1; Preview(nn); }
        Busy = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (previewNote >= 0 && mode == 0 && e.X < KeyW && e.Button == MouseButtons.Left) { Preview(NoteAt(e.Y)); Invalidate(); }
        if (mode == 0 || sel == null) { bool re; var h = e.X >= KeyW && e.Y >= RulerH ? Hit(e.X, e.Y, out re) : null; bool r2 = false; if (h != null) Hit(e.X, e.Y, out r2); Cursor = h == null ? Cursors.Cross : (r2 ? Cursors.SizeWE : Cursors.SizeAll); return; }
        if (mode == 1) { long end = Snap(FrameAt(e.X)); sel.Len = Math.Max((long)GridFrames, end - sel.Start); }
        else if (mode == 2) { sel.Start = Math.Max(0, Snap(FrameAt(e.X) - grab)); int nn = Math.Max(LowNote, Math.Min(HighNote, NoteAt(e.Y))); if (nn != sel.Note) { sel.Note = nn; Preview(nn); } }
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); if (mode != 0) Commit(); mode = 0; Busy = false; Silence(); Invalidate();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Control) != 0) { double old = ppb; ppb = Math.Max(8, Math.Min(400, ppb * (e.Delta > 0 ? 1.2 : 1 / 1.2))); scrollX = (int)Math.Max(0, (scrollX + e.X - KeyW) * (ppb / old) - (e.X - KeyW)); }
        else if ((ModifierKeys & Keys.Shift) != 0) scrollX = Math.Max(0, scrollX - e.Delta);
        else scrollY -= e.Delta / 2;
        Invalidate();
    }
}
