using System;
using System.IO;
using System.Windows.Forms;

// A problem inside Purphex no longer shows the grey "Microsoft .NET Framework" box: it is written to crash.log (next to settings.txt),
// the program keeps running, and one plain message says so (the same problem is not shown again for a minute, so a repeating one cannot bury you in boxes).
static class CrashGuard
{
    static string lastKey = ""; static int lastAt = Environment.TickCount - 600000;
    public static string LogPath { get { return Path.Combine(StudioEnv.DataDir, "crash.log"); } }

    public static void Install()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Handle(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Handle(e.ExceptionObject as Exception, true);
        }
        catch { }
    }

    public static void Handle(Exception ex, bool fatal)
    {
        if (ex == null) return;
        string key = ex.GetType().Name + ex.Message; bool repeat = key == lastKey && unchecked(Environment.TickCount - lastAt) < 60000; lastKey = key; lastAt = Environment.TickCount;
        try
        {
            Directory.CreateDirectory(StudioEnv.DataDir);
            string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  v" + Application.ProductVersion + "  " + (fatal ? "FATAL" : "caught") + "\r\n" + ex + "\r\n\r\n";
            var fi = new FileInfo(LogPath); if (fi.Exists && fi.Length > 400000) File.Delete(LogPath);   // the log never grows without end
            File.AppendAllText(LogPath, text);
        }
        catch { }
        // a window that is closing leaves a few last paints behind: nothing to tell anyone about
        if (repeat || ex is ObjectDisposedException) return;
        try { MessageBox.Show("Purphex hit a problem, but it is still running.\n\n" + ex.Message + "\n\nThe details were saved to:\n" + LogPath + "\n\nIf it keeps happening, send that file to the Purphex Discord.", "Purphex Music", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
    }
}

// Dropping files onto the studio needs Windows to register the window for drag and drop. When that is refused (the window is being rebuilt for full screen or a theme,
// or another program holds the registration) WinForms used to throw and show the grey .NET box. Now the window just opens without it and tries again the next time.
partial class StudioForm
{
    protected override void OnHandleCreated(EventArgs e)
    {
        try { if (AllowDrop) AllowDrop = false; } catch { }
        base.OnHandleCreated(e);
        try { AllowDrop = true; } catch { }
        EnsureUseTimer();   // TIME MADE starts counting the moment the DAW is open, whether or not anyone is looking at it
    }
}
