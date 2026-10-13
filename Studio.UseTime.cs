using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

// TIME MADE on the profile is the time you spend in the DAW, for life, counted in seconds for as long as the DAW is open (even in the background or minimised).
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
    public static void Add(double s) { lock (lk) { Load(); secs += s; if ((DateTime.Now - lastSave).TotalSeconds >= 10) Save(); } }
    // the first time only: start from the length of the projects already made
    public static void Seed(double fromProjects) { lock (lk) { Load(); if (seeded) return; seeded = true; if (fromProjects > secs) { secs = fromProjects; Save(); } else Save(); } }
    public static void Save() { lock (lk) { lastSave = DateTime.Now; try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(FileP, secs.ToString("0.###", CultureInfo.InvariantCulture), new System.Text.UTF8Encoding(false)); } catch { } } }
    public static void ResetForTest() { lock (lk) { secs = -1; seeded = false; } }
}

partial class StudioForm
{
    System.Windows.Forms.Timer useTimer; readonly System.Diagnostics.Stopwatch useClock = System.Diagnostics.Stopwatch.StartNew(); double useLast;
    // TIME MADE is for life and keeps counting for as long as the DAW is open: in the background, behind other windows, minimised, with nothing playing. Open means in use.
    // It counts the real time that passed (a busy or sleeping computer never loses or invents seconds), saves every few seconds and when the DAW closes, and keeps going up on the Profile page while you watch.
    DateTime lastTimeSent = DateTime.Now.AddMinutes(-9.5);
    void EnsureUseTimer()
    {
        if (useTimer != null) return; useTimer = new System.Windows.Forms.Timer { Interval = 1000 }; useLast = useClock.Elapsed.TotalSeconds;
        useTimer.Tick += (s, e) => { double now = useClock.Elapsed.TotalSeconds, dt = now - useLast; useLast = now; if (UsingNow() && dt > 0) { UseTime.Add(Math.Min(dt, 5.0)); if (tab == 6 && WindowState != FormWindowState.Minimized) Invalidate(); if (ProfileStore.Published && (DateTime.Now - lastTimeSent).TotalMinutes >= 10) { lastTimeSent = DateTime.Now; QueueRepublish(); } } };   // the time made reaches your public profile every ten minutes (and with every publish)   // on the Profile page the number is redrawn every second
        useTimer.Start(); FormClosed += (s, e) => { try { useTimer.Stop(); UseTime.Save(); } catch { } };
    }
    internal bool UsingNow() { return !IsDisposed && IsHandleCreated; }
}
