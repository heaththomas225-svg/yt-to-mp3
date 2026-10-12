using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

// TIME MADE on the profile is the time you spend in the DAW, counted in seconds while you use it (the window is the one you are in, and you are playing or have touched it in the last 3 minutes).
// It is kept in the data folder (usetime.txt). The first time, it starts from the length of the projects you already made, so the number does not start again from nothing.
static class UseTime
{
    static double secs = -1; static readonly object lk = new object(); static DateTime lastSave = DateTime.Now;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "usetime.txt"); } }
    static void Load()
    {
        if (secs >= 0) return; secs = 0; bool had = false;
        try { if (File.Exists(FileP)) { double v; if (double.TryParse(File.ReadAllText(FileP).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0) { secs = v; had = true; } } } catch { }
        seeded = had;
    }
    static bool seeded;
    public static double Seconds { get { lock (lk) { Load(); return secs; } } }
    public static void Add(double s) { lock (lk) { Load(); secs += s; if ((DateTime.Now - lastSave).TotalSeconds >= 15) Save(); } }
    // the first time only: start from the length of the projects already made
    public static void Seed(double fromProjects) { lock (lk) { Load(); if (seeded) return; seeded = true; if (fromProjects > secs) { secs = fromProjects; Save(); } else Save(); } }
    public static void Save() { lock (lk) { lastSave = DateTime.Now; try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(FileP, secs.ToString("0.###", CultureInfo.InvariantCulture), new System.Text.UTF8Encoding(false)); } catch { } } }
    public static void ResetForTest() { lock (lk) { secs = -1; seeded = false; } }
}

partial class StudioForm
{
    System.Windows.Forms.Timer useTimer;
    // a one second clock: it counts a second when this is the window you are in and you are using it
    void EnsureUseTimer()
    {
        if (useTimer != null) return; useTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        useTimer.Tick += (s, e) => { if (UsingNow()) UseTime.Add(1); };
        useTimer.Start(); FormClosed += (s, e) => { try { useTimer.Stop(); UseTime.Save(); } catch { } };
    }
    internal bool UsingNow()
    {
        if (!Visible || WindowState == FormWindowState.Minimized || IsDisposed) return false; if (Form.ActiveForm != this && !StudioEnv.UnderTest) return false;
        return eng.Playing || (DateTime.Now - lastInput).TotalSeconds < 180;
    }
}
