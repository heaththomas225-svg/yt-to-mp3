using System;
using System.Drawing;
using System.Drawing.Drawing2D;

// One look for every preset card (master presets, plugin presets, community presets): a dark panel in a neon box in the colours of the theme, a small tag, and short text that never runs into the buttons.
partial class StudioForm
{
    void NeonCard(Graphics g, RectangleF r, bool hot, bool picked)
    {
        FillRR(g, picked ? TM.C(235, 26, 38, 80) : TM.C(hot ? 240 : 225, hot ? 24 : 18, hot ? 14 : 9, hot ? 52 : 40), r, 12); Glow(g, picked ? NeonCyan : NeonViolet, r, 12, picked ? 0.55f : (hot ? 0.6f : 0.28f), 3); Win.DrawNeonBox(g, r, hot || picked); StrokeRR(g, Color.FromArgb(hot || picked ? 255 : 150, picked ? NeonCyan : NeonViolet), r, 12, hot || picked ? 1.8f : 1.2f);
    }
    // a small tag in a neon outline; returns its width so the next thing can sit beside it
    float NeonTag(Graphics g, float x, float y, string s, Color c)
    {
        float w = 16 + s.Length * 7f; var r = Rc(x, y, w, 20); FillRR(g, Color.FromArgb(46, c), r, 6); StrokeRR(g, Color.FromArgb(170, c), r, 6, 1f); Txt(g, s, 9, Ink, r, StringAlignment.Center, true); return w;
    }
}
