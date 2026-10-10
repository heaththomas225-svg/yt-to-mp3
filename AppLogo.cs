using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// The Purphex emblem: the program's icon, the default profile picture, and the logo in the main window. It is built into the exe.
static class AppLogo
{
    static Bitmap logo, small; static Icon ico; static bool tried;
    public static Bitmap Logo { get { Load(); return logo; } }
    // 42x42 premultiplied copy, resampled once, so the title bar logo is a plain 1:1 blit every frame
    public static Bitmap Small { get { Load(); if (small == null && logo != null) { var b = new Bitmap(42, 42, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); using (var g = Graphics.FromImage(b)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality; g.DrawImage(logo, new Rectangle(0, 0, 42, 42)); } small = b; } return small; } }
    public static Icon Icon { get { Load(); return ico; } }
    static void Load()
    {
        if (tried) return; tried = true;
        try { using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Purphex.logo.png")) if (s != null) using (var im = Image.FromStream(s)) logo = new Bitmap(im); } catch { }
        try { ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    }
}
