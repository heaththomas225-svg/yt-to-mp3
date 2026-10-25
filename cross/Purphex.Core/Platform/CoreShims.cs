// The few app-level helpers the shared engine code calls, for the cross-platform core.
// On Windows these come from Theme.cs, Themes.cs and Studio.Edition.cs; here they are plain .NET (no UI).
using System;
using System.IO;
using System.Text;

static class Win
{
    // write a file safely: to a temporary file first, then swap it in (a crash never leaves half a file)
    public static void WriteAtomic(string path, string text, Encoding enc)
    {
        string tmp = path + ".tmp"; File.WriteAllText(tmp, text, enc);
        if (File.Exists(path)) { try { File.Replace(tmp, path, null); } catch { File.Copy(tmp, path, true); try { File.Delete(tmp); } catch { } } } else File.Move(tmp, path);
    }
}

static class Edition
{
    // the highest export sample rate the account's edition allows (Free 48 kHz, Supporter 192 kHz, Fantasy 384 kHz); the host sets it from the signed-in account
    public static Func<int> MaxRateOf = () => 48000;
    public static int MaxRate { get { return MaxRateOf(); } }
    public static int StudioMaxRate { get { return MaxRateOf(); } }
}

class ThemeDef
{
    // the colour names a theme file may use (the same lists as Themes.cs)
    public static readonly string[] Keys = { "violet", "cyan", "pink", "lime", "accent", "accent2", "btn", "btnc", "edge", "panel", "lane", "lane2", "bg0", "bg1" };
    public static readonly string[] ShortKeys = { "primary", "secondary", "tertiary", "background" };
}
