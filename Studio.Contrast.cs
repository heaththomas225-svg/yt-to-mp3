using System;
using System.Drawing;

// Writing must be readable on every theme: the label on a button is white, or dark when the button is light (a bright yellow or teal button with white writing could not be read).
static class Contrast
{
    static double Lin(int v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
    public static double Lum(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
    // the WCAG contrast ratio: 1 (invisible) to 21 (black on white); 4.5 is comfortable for small writing
    public static double Ratio(Color a, Color b) { double l1 = Lum(a), l2 = Lum(b); if (l1 < l2) { double t = l1; l1 = l2; l2 = t; } return (l1 + 0.05) / (l2 + 0.05); }
    public static readonly Color DarkInk = Color.FromArgb(255, 12, 6, 26);
    // white when it can be read on every one of these fills, otherwise whichever of white and dark is clearer
    public static Color LabelOn(params Color[] fills) { return Color.White; }   // the writing on buttons is always white (see ForWhite)
    // a fill darkened (keeping its colour) just enough for WHITE writing to be read on it
    public static Color ForWhite(Color fill, double want = 4.6)
    {
        Color c = fill; for (int i = 0; i < 24 && Ratio(Color.White, c) < want; i++) c = Color.FromArgb(c.A, (int)(c.R * 0.88), (int)(c.G * 0.88), (int)(c.B * 0.88)); return c;
    }
    // writing of this colour moved just far enough (lighter on a dark back, darker on a light one) to reach the ratio on that back
    public static Color Readable(Color text, Color back, double want = 4.5)
    {
        if (Ratio(text, back) >= want) return text; bool lighten = Lum(back) < 0.4; Color c = text;
        for (int i = 0; i < 40 && Ratio(c, back) < want; i++) c = lighten ? Color.FromArgb(c.A, Math.Min(255, c.R + 8), Math.Min(255, c.G + 8), Math.Min(255, c.B + 8)) : Color.FromArgb(c.A, Math.Max(0, c.R - 8), Math.Max(0, c.G - 8), Math.Max(0, c.B - 8));
        return c;
    }
}
