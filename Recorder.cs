using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// Records from a microphone or line input (Windows winmm, 44.1 kHz mono). Used by the MIC button in Purphex Music.
class AudioRecorder : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
    [StructLayout(LayoutKind.Sequential)] struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct WAVEINCAPS { public ushort mid, pid; public uint ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public uint formats; public ushort ch, res; }
    [DllImport("winmm.dll")] static extern int waveInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveInGetDevCaps(IntPtr id, ref WAVEINCAPS caps, int size);
    [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveInStart(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveInStop(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveInReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveInClose(IntPtr h);
    const int CALLBACK_EVENT = 0x50000, WHDR_DONE = 1, Buffers = 6, Frames = 1024;
    public const int InputBufferFrames = Frames * 2;   // rough delay between a sound and it being recorded

    IntPtr hwi; readonly IntPtr[] hdrs = new IntPtr[Buffers], data = new IntPtr[Buffers]; readonly ManualResetEvent ev = new ManualResetEvent(false); Thread th; volatile bool run, started;
    readonly List<float[]> chunks = new List<float[]>(); long total; public volatile float Level; public string Error = "";
    MicProcessor proc; long skipLeft;   // the microphone mode: what is recorded is cleaned while it comes in (Standard = nothing is changed)
    public void SetMode(MicMode m) { lock (chunks) { proc = m == MicMode.Standard ? null : new MicProcessor(m, 44100); skipLeft = proc == null ? 0 : proc.LatencyFrames; } }
    public int FramesRecorded { get { return (int)Math.Min(int.MaxValue, total); } }

    public static List<string> Devices()
    {
        var l = new List<string>();
        try { int n = waveInGetNumDevs(); for (int i = 0; i < n; i++) { var c = new WAVEINCAPS(); if (waveInGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WAVEINCAPS))) == 0) l.Add(c.name); else l.Add("Input " + i); } } catch { }
        return l;
    }

    public bool Start(int device)   // -1 = the Windows default input
    {
        try
        {
            var f = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = 44100, wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = 88200 };
            int r = waveInOpen(out hwi, device, ref f, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
            if (r != 0) { Error = "Couldn't open the microphone (error " + r + "). If Windows is blocking it: Settings > Privacy > Microphone > allow desktop apps."; return false; }
            int hs = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < Buffers; i++)
            {
                data[i] = Marshal.AllocHGlobal(Frames * 2); hdrs[i] = Marshal.AllocHGlobal(hs);
                var h = new WAVEHDR { lpData = data[i], dwBufferLength = (uint)(Frames * 2) }; Marshal.StructureToPtr(h, hdrs[i], false);
                waveInPrepareHeader(hwi, hdrs[i], hs); waveInAddBuffer(hwi, hdrs[i], hs);
            }
            run = true; started = true; th = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "record" }; th.Start(); waveInStart(hwi); return true;
        }
        catch (Exception ex) { Error = "Recording is unavailable: " + ex.Message; return false; }
    }

    void Loop()
    {
        int hs = Marshal.SizeOf(typeof(WAVEHDR)); var tmp = new short[Frames];
        while (run)
        {
            ev.WaitOne(30); ev.Reset();
            for (int i = 0; i < Buffers && run; i++)
            {
                var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                if ((h.dwFlags & WHDR_DONE) == 0) continue;
                int n = (int)(h.dwBytesRecorded / 2); if (n > 0) { Marshal.Copy(data[i], tmp, 0, n); Feed(tmp, n); }
                h.dwFlags &= ~(uint)WHDR_DONE; h.dwBytesRecorded = 0; Marshal.StructureToPtr(h, hdrs[i], false); waveInAddBuffer(hwi, hdrs[i], hs);
            }
        }
    }

    // converts 16-bit samples to float and keeps them (also used by tests)
    // what came in from the microphone itself (before any clean-up): a live mic always has some hiss; pure zeros mean it is muted on the headset or switched off
    float rawPeak; long rawFrames;
    public bool AllSilent { get { return rawFrames > 44100 && rawPeak < 0.0002f; } }
    internal void Feed(short[] pcm, int n)
    {
        for (int i = 0; i < n; i++) { float a = Math.Abs(pcm[i] / 32768f); if (a > rawPeak) rawPeak = a; } rawFrames += n;
        var c = new float[n]; float pk = 0; for (int i = 0; i < n; i++) { float v = pcm[i] / 32768f; c[i] = v; float a = Math.Abs(v); if (a > pk) pk = a; }
        lock (chunks)
        {
            if (proc != null)
            {
                proc.Process(c, n); if (skipLeft > 0) { int k = (int)Math.Min(skipLeft, n); skipLeft -= k; if (k > 0) { var rest = new float[n - k]; Array.Copy(c, k, rest, 0, n - k); c = rest; } }   // the first LatencyFrames of output are only the processor warming up
                pk = 0; foreach (float v in c) { float a = Math.Abs(v); if (a > pk) pk = a; }
            }
            if (c.Length > 0) { chunks.Add(c); total += c.Length; }
        }
        Level = pk;
    }

    // stops and returns everything recorded (mono)
    public float[] Stop(out int frames)
    {
        run = false; ev.Set(); if (th != null) th.Join(800);
        if (started && hwi != IntPtr.Zero)
        {
            try
            {
                waveInStop(hwi); waveInReset(hwi); int hs = Marshal.SizeOf(typeof(WAVEHDR)); var tmp = new short[Frames];
                for (int i = 0; i < Buffers; i++) { var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR)); int n = (int)(h.dwBytesRecorded / 2); if ((h.dwFlags & WHDR_DONE) != 0 && n > 0) { Marshal.Copy(data[i], tmp, 0, n); Feed(tmp, n); } waveInUnprepareHeader(hwi, hdrs[i], hs); Marshal.FreeHGlobal(data[i]); Marshal.FreeHGlobal(hdrs[i]); }
                waveInClose(hwi);
            }
            catch { }
            hwi = IntPtr.Zero; started = false;
        }
        return Take(out frames);
    }
    internal float[] Take(out int frames)
    {
        lock (chunks) { if (proc != null) { var tail = proc.Finish(); if (skipLeft > 0) { int k = (int)Math.Min(skipLeft, tail.Length); skipLeft -= k; var rest = new float[tail.Length - k]; Array.Copy(tail, k, rest, 0, rest.Length); tail = rest; } if (tail.Length > 0) { chunks.Add(tail); total += tail.Length; } proc = null; }   /* the sound still inside the processor comes out at the end, so the take is exactly as long as what was heard */
            var all = new float[(int)Math.Min(int.MaxValue - 64, total)]; int p = 0; foreach (var c in chunks) { Array.Copy(c, 0, all, p, c.Length); p += c.Length; } frames = all.Length; chunks.Clear(); total = 0; return all; }
    }
    public void Dispose() { int f; try { if (started) Stop(out f); } catch { } }
}
