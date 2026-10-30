using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// Windows sound output (waveOut): the AudioOut backend of the Windows app.
class WaveOutPlayer : AudioOut
{
    [StructLayout(LayoutKind.Sequential)] struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
    [StructLayout(LayoutKind.Sequential)] struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }
    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutPause(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct MMTIME { public uint wType; public uint u; public uint pad; }
    [DllImport("winmm.dll")] static extern int waveOutGetPosition(IntPtr h, ref MMTIME t, int size);
    long sent;   // frames handed to the sound card since it was opened
    readonly long[] bSent = new long[64], bFrom = new long[64], bTo = new long[64]; readonly bool[] bPlay = new bool[64]; int bHead;   // the last blocks sent: where each starts in the sound card, which part of the song it holds
    // the place in the song the sound card is playing right now (-1 when it cannot tell)
    public override long PlayingSongPos()
    {
        try
        {
            if (hwo == IntPtr.Zero) return -1; var t = new MMTIME { wType = 2 }; if (waveOutGetPosition(hwo, ref t, Marshal.SizeOf(typeof(MMTIME))) != 0 || t.wType != 2) return -1; long played = t.u;
            lock (bSent) for (int k = 0; k < 64; k++) { int i = (bHead - 1 - k + 64 * 2) % 64; if (bSent[i] <= played && played < bSent[i] + Frames) { long off = played - bSent[i]; if (!bPlay[i]) return bFrom[i]; if (bTo[i] >= bFrom[i]) return Math.Min(bTo[i], bFrom[i] + off); return bFrom[i] + off; } }
        }
        catch { }
        return -1;
    }
    // the frames sent to the sound card that it has not played yet: exactly how far what you hear is behind what has been made
    public override int Queued()
    {
        try { if (hwo == IntPtr.Zero) return QueuedFrames; var t = new MMTIME { wType = 2 }; if (waveOutGetPosition(hwo, ref t, Marshal.SizeOf(typeof(MMTIME))) == 0 && t.wType == 2) { long q = Interlocked.Read(ref sent) - t.u; if (q >= 0 && q <= (long)Buffers * Frames) return (int)q; } } catch { }
        return QueuedFrames;
    }
    [DllImport("winmm.dll")] static extern int waveOutRestart(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct POWER_THROTTLING { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll")] static extern bool SetProcessInformation(IntPtr h, int cls, ref POWER_THROTTLING s, int size);
    [DllImport("kernel32.dll")] static extern bool SetThreadInformation(IntPtr h, int cls, ref POWER_THROTTLING s, int size);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
    static bool unthrottled;
    // the whole app opts out of power throttling (execution speed and timer resolution): in the background it keeps full speed, so the sound never breaks up
    public static void NoThrottle()
    {
        if (unthrottled) return; unthrottled = true;
        try { var p = new POWER_THROTTLING { Version = 1, ControlMask = 0x1 | 0x4, StateMask = 0 }; SetProcessInformation((IntPtr)(-1), 4, ref p, Marshal.SizeOf(typeof(POWER_THROTTLING))); } catch { }
    }
    [DllImport("avrt.dll", CharSet = CharSet.Unicode)] static extern IntPtr AvSetMmThreadCharacteristicsW(string task, ref uint index);
    const int CALLBACK_EVENT = 0x50000, WHDR_DONE = 1;
    long lastDone = Environment.TickCount;
    readonly int Buffers;   // more, smaller buffers: the same 23 ms steps but much more slack when the computer is busy
    public static int Device { get { return Prefs.OutDev; } }
    readonly int Frames;
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveOutGetDevCaps(IntPtr id, ref WAVEOUTCAPS caps, int size);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct WAVEOUTCAPS { public ushort mid, pid; public uint ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public uint formats; public ushort ch, res; public uint support; }
    public static System.Collections.Generic.List<string> DeviceNames()
    {
        var l = new System.Collections.Generic.List<string>(); int n = waveOutGetNumDevs();
        for (int i = 0; i < n; i++) { var c = new WAVEOUTCAPS(); if (waveOutGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WAVEOUTCAPS))) == 0) l.Add(c.name); else l.Add("Device " + i); }
        return l;
    }

    readonly OutSafety safety = new OutSafety(); Action<float[], float[], int> render; IntPtr hwo; IntPtr[] hdrs, data;
    ManualResetEvent ev = new ManualResetEvent(false); Thread th; volatile bool run; float[] fl, fr; short[] tmp;

    // level: the buffer size (0 small, 1 medium, 2 biggest); -1 = the one from Settings (made bigger for the session when the sound broke up)
    public WaveOutPlayer(Action<float[], float[], int> render, int level = -1) { int lv = level < 0 ? Prefs.BufEff : Math.Min(2, level); Buffers = lv == 0 ? 3 : (lv == 2 ? 6 : 4); Frames = lv == 2 ? 1024 : 512;   /* low latency 35 ms, normal 46 ms, safe 139 ms of sound queued (normal was 186 ms: every pad hit came out that late) */ this.render = render; hdrs = new IntPtr[Buffers]; data = new IntPtr[Buffers]; QueuedFrames = Buffers * Frames; fl = new float[Frames]; fr = new float[Frames]; tmp = new short[Frames * 2]; }

    public override void Start()
    {
        NoThrottle(); var f = new WAVEFORMATEX { wFormatTag = 1, nChannels = 2, nSamplesPerSec = 44100, wBitsPerSample = 16, nBlockAlign = 4, nAvgBytesPerSec = 44100 * 4 };
        int r = waveOutOpen(out hwo, Device, ref f, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
        if (r != 0) throw new Exception("Couldn't open the audio output device (winmm error " + r + ").");
        waveOutPause(hwo);   // the card does not start until every buffer is full: filling them takes a moment, and a card that starts early runs dry (a crackle at the very start)
        for (int i = 0; i < Buffers; i++)
        {
            data[i] = Marshal.AllocHGlobal(Frames * 4); hdrs[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
            var h = new WAVEHDR { lpData = data[i], dwBufferLength = (uint)(Frames * 4), dwFlags = 0 };
            Marshal.StructureToPtr(h, hdrs[i], false); waveOutPrepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
            Fill(i);
        }
        waveOutRestart(hwo); run = true; th = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "audio" }; th.Start();
    }

    void Fill(int i)
    {
        long from = PosNow != null ? PosNow() : 0; bool pl = PlayingNow != null && PlayingNow();
        try { render(fl, fr, Frames); } catch { Array.Clear(fl, 0, Frames); Array.Clear(fr, 0, Frames); }
        float gn = StudioEnv.UnderTest || MuteAll ? 0f : Math.Min(Gain, Prefs.OutVol / 100f); if (gn != 1f) for (int k = 0; k < Frames; k++) { fl[k] *= gn; fr[k] *= gn; }
        safety.Process(fl, fr, Frames);   // a project that is too loud is brought down smoothly instead of being chopped off (chopped peaks are the crackle and fuzz you hear)
        for (int k = 0; k < Frames; k++)
        {
            float a = fl[k], b = fr[k]; if (a > 1) a = 1; else if (a < -1) a = -1; if (b > 1) b = 1; else if (b < -1) b = -1;
            tmp[k * 2] = (short)(a * 32767f); tmp[k * 2 + 1] = (short)(b * 32767f);
        }
        Marshal.Copy(tmp, 0, data[i], Frames * 2);
        lock (bSent) { int b = bHead % 64; bSent[b] = Interlocked.Read(ref sent); bFrom[b] = from; bTo[b] = PosNow != null ? PosNow() : from; bPlay[b] = pl && bTo[b] != from; bHead = (bHead + 1) % 64; }
        Interlocked.Add(ref sent, Frames);
        var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR)); h.dwFlags &= ~(uint)WHDR_DONE; Marshal.StructureToPtr(h, hdrs[i], false);
        waveOutWrite(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
    }

    void Loop()
    {
        try { uint ix = 0; AvSetMmThreadCharacteristicsW("Pro Audio", ref ix); } catch { }
        try { var t = new POWER_THROTTLING { Version = 1, ControlMask = 1, StateMask = 0 }; SetThreadInformation(GetCurrentThread(), 3, ref t, Marshal.SizeOf(typeof(POWER_THROTTLING))); } catch { }   // and this thread never runs on the slow cores   // Windows treats this thread as pro audio: it gets the processor on time even when the computer is busy
        while (run)
        {
            ev.WaitOne(30); ev.Reset();
            int ready = 0; for (int i = 0; i < Buffers; i++) { var hh = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR)); if ((hh.dwFlags & WHDR_DONE) != 0) ready++; }
            if (ready >= Buffers) Underruns++;   // every buffer had already been played: the sound card was starved
            if (ready > 0) lastDone = Environment.TickCount; else if (Environment.TickCount - lastDone > 350) { Stalls++; lastDone = Environment.TickCount; }   // nothing has finished for a third of a second: the device has stopped
            for (int i = 0; i < Buffers && run; i++)
            {
                var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                if ((h.dwFlags & WHDR_DONE) != 0) Fill(i);
            }
        }
    }

    public override void Dispose()
    {
        run = false; ev.Set(); if (th != null) th.Join(500);
        if (hwo != IntPtr.Zero)
        {
            waveOutReset(hwo);
            for (int i = 0; i < Buffers; i++) { waveOutUnprepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR))); Marshal.FreeHGlobal(data[i]); Marshal.FreeHGlobal(hdrs[i]); }
            waveOutClose(hwo); hwo = IntPtr.Zero;
        }
    }
}
