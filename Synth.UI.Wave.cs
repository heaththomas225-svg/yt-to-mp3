using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Draw your own oscillator waveform: freehand with the mouse, up to 8 frames that the WAVE knob scans between. Every change is heard at once.
class WaveEditorForm : Form
{
    readonly SynthFx fx; readonly int slot; readonly string osc; readonly List<short[]> frames = new List<short[]>(); int cur; readonly DrawPad pad; Label info; Timer previewOff;

    public WaveEditorForm(SynthFx fx, int slot, string osc)
    {
        this.fx = fx; this.slot = slot; this.osc = osc; Text = "Draw a waveform: Custom " + (slot + 1); BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255);
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 500); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Win.Dark(this);
        var cur0 = Wavetables.GetCustom(slot); if (cur0 != null && cur0.Length > 0) foreach (var f in cur0) frames.Add((short[])f.Clone()); else frames.Add(Wavetables.DefaultFrame());
        pad = new DrawPad(this) { Left = 14, Top = 14, Width = 732, Height = 260 }; Controls.Add(pad);
        info = new Label { Left = 14, Top = 280, Width = 732, Height = 22, ForeColor = TM.C(255, 200, 170, 255) }; Controls.Add(info);
        bx = 14; by = 308;
        Controls.Add(new Label { Text = "Start from:", Left = bx, Top = by + 5, Width = 70, ForeColor = TM.C(255, 160, 140, 200) }); bx += 72;
        string[] shapes = { "Sine", "Triangle", "Saw", "Square", "Pulse", "Noise" };
        foreach (var sh in shapes) { string s2 = sh; B(sh, 66, (s, e) => { Fill(s2); }); }
        bx = 14; by = 342;
        B("Smooth", 70, (s, e) => { Smooth(); }); B("Normalize", 80, (s, e) => { Normalize(); }); B("Flip", 50, (s, e) => { Flip(); }); B("Clear", 56, (s, e) => { Fill("Flat"); });
        bx += 16; Controls.Add(new Label { Text = "Frame:", Left = bx, Top = by + 5, Width = 44, ForeColor = TM.C(255, 160, 140, 200) }); bx += 46;
        B("<", 32, (s, e) => { cur = Math.Max(0, cur - 1); pad.Invalidate(); Info(); }); B(">", 32, (s, e) => { cur = Math.Min(frames.Count - 1, cur + 1); pad.Invalidate(); Info(); });
        B("Add frame (copy)", 120, (s, e) => { if (frames.Count < 8) { frames.Insert(cur + 1, (short[])frames[cur].Clone()); cur++; Apply(); } });
        B("Delete frame", 100, (s, e) => { if (frames.Count > 1) { frames.RemoveAt(cur); cur = Math.Max(0, cur - 1); Apply(); } });
        bx = 14; by = 380;
        B("Hear it (C2)", 110, (s, e) => Hear()); B("Done", 80, (s, e) => Close());
        Controls.Add(new Label { Left = 14, Top = 420, Width = 732, Height = 44, ForeColor = TM.C(255, 150, 130, 190), Font = new Font("Segoe UI", 9f), Text = "Hold the mouse button and draw across the picture. Everything you draw is used by Oscillator " + osc.ToUpper() + " straight away (it is set to Custom " + (slot + 1) + "). With several frames, the WAVE knob (and any LFO or envelope on it) scans smoothly from one frame to the next. Your drawings are saved with the patch." });
        FormClosed += (s, e) => { if (previewOff != null) previewOff.Stop(); fx.NoteOff(36); };
        Apply();
    }

    int bx, by;
    Button B(string t, int w, EventHandler h) { var b = new PxButton { Text = t, Left = bx, Top = by, Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 70, 40, 120), ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = TM.C(255, 200, 150, 255); b.Click += h; Controls.Add(b); bx += w + 6; return b; }
    public short[] Current { get { return frames[cur]; } }
    void Info() { info.Text = "Frame " + (cur + 1) + " of " + frames.Count + "   (up to 8 frames; each frame is one cycle of the wave)"; }
    public void Apply() { Wavetables.SetCustom(slot, frames.ToArray()); Info(); pad.Invalidate(); }
    void Fill(string shape)
    {
        var f = frames[cur]; var rnd = new Random(11);
        for (int i = 0; i < 256; i++) { double p = i / 256.0, y; switch (shape) { case "Sine": y = Math.Sin(2 * Math.PI * p); break; case "Triangle": y = 4 * Math.Abs(p - 0.5) - 1; break; case "Saw": y = 2 * p - 1; break; case "Square": y = p < 0.5 ? 1 : -1; break; case "Pulse": y = p < 0.2 ? 1 : -1; break; case "Noise": y = rnd.NextDouble() * 2 - 1; break; default: y = 0; break; } f[i] = (short)(y * 30000); }
        Apply();
    }
    void Smooth() { var f = frames[cur]; var t = new short[256]; for (int i = 0; i < 256; i++) t[i] = (short)((f[(i + 255) % 256] + 2 * f[i] + f[(i + 1) % 256]) / 4); Array.Copy(t, f, 256); Apply(); }
    void Flip() { var f = frames[cur]; for (int i = 0; i < 256; i++) f[i] = (short)Math.Max(-32767, Math.Min(32767, -f[i])); Apply(); }
    void Normalize() { var f = frames[cur]; int pk = 1; for (int i = 0; i < 256; i++) pk = Math.Max(pk, Math.Abs((int)f[i])); double g = 30000.0 / pk; for (int i = 0; i < 256; i++) f[i] = (short)(f[i] * g); Apply(); }
    void Hear() { fx.NoteOn(36, 100); if (previewOff == null) { previewOff = new Timer { Interval = 900 }; previewOff.Tick += (s, e) => { previewOff.Stop(); fx.NoteOff(36); }; } previewOff.Stop(); previewOff.Start(); }

    class DrawPad : Control
    {
        readonly WaveEditorForm w; int lastX = -1, lastY;
        public DrawPad(WaveEditorForm w) { this.w = w; DoubleBuffered = true; BackColor = TM.C(255, 12, 6, 24); Cursor = Cursors.Cross; }
        int XToI(int x) { return Math.Max(0, Math.Min(255, (int)(x * 256.0 / Width))); }
        short YToV(int y) { return (short)(Math.Max(-1.0, Math.Min(1.0, 1 - 2.0 * y / Height)) * 32000); }
        void Paint1(int x, int y)
        {
            var f = w.Current; int i1 = XToI(x); short v1 = YToV(y);
            if (lastX < 0) f[i1] = v1; else { int i0 = XToI(lastX); short v0 = YToV(lastY); int a = Math.Min(i0, i1), b = Math.Max(i0, i1); for (int i = a; i <= b; i++) { double t = b == a ? 1 : (i - a) / (double)(b - a); f[i] = (short)(i0 <= i1 ? v0 + (v1 - v0) * t : v1 + (v0 - v1) * t); } }
            lastX = x; lastY = y; w.Apply();
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); lastX = -1; Paint1(e.X, e.Y); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (e.Button == MouseButtons.Left) Paint1(e.X, e.Y); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); lastX = -1; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(TM.C(40, 150, 130, 200))) { for (int i = 1; i < 8; i++) g.DrawLine(pen, i * Width / 8, 0, i * Width / 8, Height); g.DrawLine(pen, 0, Height / 2, Width, Height / 2); g.DrawLine(pen, 0, Height / 4, Width, Height / 4); g.DrawLine(pen, 0, 3 * Height / 4, Width, 3 * Height / 4); }
            // neighbouring frames, faint, so you can see what you are morphing between
            for (int d = -1; d <= 1; d += 2) { int fi = Array.IndexOf(w.frames.ToArray(), w.Current) + d; if (fi < 0 || fi >= w.frames.Count) continue; var o = w.frames[fi]; var pts = new PointF[256]; for (int i = 0; i < 256; i++) pts[i] = new PointF(i * Width / 255f, Height / 2f - o[i] / 32768f * (Height / 2f - 6)); using (var pen = new Pen(TM.C(70, 188, 120, 255), 1.5f)) g.DrawLines(pen, pts); }
            var f = w.Current; var p2 = new PointF[256]; for (int i = 0; i < 256; i++) p2[i] = new PointF(i * Width / 255f, Height / 2f - f[i] / 32768f * (Height / 2f - 6));
            using (var pen = new Pen(Color.FromArgb(120, 232, 150), 2.4f)) g.DrawLines(pen, p2);
        }
    }
}
