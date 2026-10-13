using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The neon box with a down arrow beside the MIC button: a very short menu of the microphone modes (Automatic, Standard, Voice Isolation, Wide Spectrum). They only change what is recorded.
partial class StudioForm
{
    static readonly MicMode[] MicModes = { MicMode.Automatic, MicMode.Standard, MicMode.VoiceIsolation, MicMode.WideSpectrum };

    void PaintMicDrop(Graphics g, RectangleF r, bool recording)
    {
        bool hot = Hot(r) && !recording; FillRR(g, TM.C(255, 10, 5, 24), r, 8); Glow(g, NeonCyan, r, 8, hot ? 0.95f : 0.5f, 3); StrokeRR(g, Color.FromArgb(hot ? 255 : 190, NeonCyan), r, 8, 1.4f);
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f + 1; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Color.FromArgb(recording ? 90 : 245, hot ? Color.White : NeonCyan))) g.FillPolygon(b, new[] { new PointF(cx - 5, cy - 2.5f), new PointF(cx + 5, cy - 2.5f), new PointF(cx, cy + 3.5f) });
        g.SmoothingMode = sm;
        Reg(r, () => MicMenu(r)); string mh = recording ? "The microphone mode cannot change while recording." : "Microphone mode: " + MicProcessor.Name((MicMode)Prefs.MicMode) + ". Click to choose (only changes what you record)."; Hint2(r, mh); if (Hot(r)) hintNow = mh;
    }

    // the mode that is picked, in small writing in a box of its own next to the arrow (press it to choose another)
    void PaintMicModeBox(Graphics g, RectangleF r, bool recording)
    {
        var mode = (MicMode)Prefs.MicMode; string nm = mode == MicMode.Automatic ? "AUTOMATIC" : (mode == MicMode.Standard ? "STANDARD" : (mode == MicMode.VoiceIsolation ? "VOICE ISO." : "WIDE SPECTRUM")); if (mode == MicMode.WideSpectrum) nm = "WIDE";
        bool hot = Hot(r) && !recording; FillRR(g, TM.C(255, 10, 5, 24), r, 6); StrokeRR(g, Color.FromArgb(hot ? 230 : 120, NeonCyan), r, 6, 1f);
        Txt(g, nm, 9, recording ? DimC : (hot ? Color.White : Ink), r, StringAlignment.Center, true);
        Reg(r, () => MicMenu(r)); string mh = recording ? "The microphone mode cannot change while recording." : "Microphone mode: " + MicProcessor.Name(mode) + ". Click to choose another (only changes what you record)."; Hint2(r, mh); if (Hot(r)) hintNow = mh;
    }

    // the choice is remembered; it is used for the next recording
    void ChooseMicMode(MicMode m)
    {
        Prefs.MicMode = (int)m; Prefs.Changed(); lastInput = DateTime.Now;
        Toast("Microphone mode: " + MicProcessor.Name(m) + ". " + MicProcessor.Blurb(m) + " (It only changes what you record.)");
    }

    void MicMenu(RectangleF anchor)
    {
        var m = Win.NewMenu(); m.ShowImageMargin = true; m.ShowItemToolTips = true;
        var head = new ToolStripMenuItem("Microphone mode (recording only)") { Enabled = false }; m.Items.Add(head); m.Items.Add(new ToolStripSeparator());
        foreach (var mode in MicModes)
        {
            var md = mode; var it = new ToolStripMenuItem(MicProcessor.Name(md)) { Checked = Prefs.MicMode == (int)md, Image = MicIcon(md), ToolTipText = MicProcessor.Blurb(md), Enabled = mic == null };
            it.Click += (a, b) => ChooseMicMode(md); m.Items.Add(it);
        }
        if (mic != null) { m.Items.Add(new ToolStripSeparator()); m.Items.Add(new ToolStripMenuItem("Stop recording to change the mode") { Enabled = false }); }
        m.Show(this, new Point((int)anchor.X, (int)anchor.Bottom + 2));
    }

    // a little neon picture for each mode (a waveform, a microphone, a voice with sound waves, a wide band of bars)
    internal static Bitmap MicIcon(MicMode mode)
    {
        var bmp = new Bitmap(20, 20); using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; var c = Color.FromArgb(255, 70, 225, 255); using (var pen = new Pen(c, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) using (var br = new SolidBrush(c))
            {
                switch (mode)
                {
                    case MicMode.Automatic: { float[] h = { 4, 9, 14, 8, 12, 5 }; for (int i = 0; i < h.Length; i++) { float x = 3 + i * 2.8f; g.DrawLine(pen, x, 10 - h[i] / 2, x, 10 + h[i] / 2); } } break;
                    case MicMode.Standard: g.DrawArc(pen, 5, 3, 10, 14, 0, 180); g.DrawLine(pen, 5, 8, 5, 10); g.DrawLine(pen, 15, 8, 15, 10); using (var p = new GraphicsPath()) { p.AddArc(7, 2, 6, 8, 180, 180); p.AddLine(13, 6, 13, 8); p.AddArc(7, 5, 6, 6, 0, 180); p.CloseFigure(); g.FillPath(br, p); } g.DrawLine(pen, 10, 15, 10, 18); g.DrawLine(pen, 6.5f, 18, 13.5f, 18); break;
                    case MicMode.VoiceIsolation: g.FillEllipse(br, 3.5f, 4, 6, 6); using (var p = new GraphicsPath()) { p.AddArc(1.5f, 12, 10, 10, 180, 180); p.CloseFigure(); g.FillPath(br, p); } g.DrawArc(pen, 11, 3, 6, 8, -60, 120); g.DrawArc(pen, 13, 1.5f, 6, 11, -60, 120); break;
                    default: { float[] h = { 3, 7, 5, 11, 15, 11, 5, 8, 3 }; for (int i = 0; i < h.Length; i++) { float x = 1.5f + i * 2.1f; g.DrawLine(pen, x, 10 - h[i] / 2, x, 10 + h[i] / 2); } } break;
                }
            }
        }
        return bmp;
    }
}
