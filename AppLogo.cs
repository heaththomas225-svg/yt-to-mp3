using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// The Purphex emblem: the program's icon, the default profile picture, and the logo in the main window. It is built into the exe.
static class AppLogo
{
    static Bitmap logo; static Icon ico; static bool tried;
    public static Bitmap Logo { get { Load(); return logo; } }
    public static Icon Icon { get { Load(); return ico; } }
    static void Load()
    {
        if (tried) return; tried = true;
        try { using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Purphex.logo.png")) if (s != null) using (var im = Image.FromStream(s)) logo = new Bitmap(im); } catch { }
        try { ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    }
}
