// Sound output for macOS and Linux (and Windows when the cross-platform build runs there), through SDL2.
// The engine's render callback fills blocks; a thread keeps the device's queue topped up a little ahead of what is heard,
// so the song position (what you hear) is always known: the frames sent minus the frames still queued.
using System;
using System.Threading;
using Thread = System.Threading.Thread;
using Silk.NET.SDL;

static class PlatformAudio
{
    static Sdl sdl; static bool ready; public static string Status = "not started";
    // call once at start: every AudioOut the engine asks for from then on plays through SDL
    public static bool Install()
    {
        if (ready) return true;
        try
        {
            sdl = Sdl.GetApi();
            if (sdl.Init(Sdl.InitAudio) < 0) { Status = "no sound device: " + sdl.GetErrorS(); return false; }
            ready = true; Status = "SDL audio (" + sdl.GetCurrentAudioDriverS() + ")";
            AudioOut.Factory = (render, level) => new SdlAudioOut(sdl, render, level);
            return true;
        }
        catch (Exception e) { Status = "SDL could not start: " + e.Message; return false; }
    }
}

sealed unsafe class SdlAudioOut : AudioOut
{
    readonly Sdl sdl; readonly Action<float[], float[], int> render; readonly int block, ahead; uint dev; Thread th; volatile bool run; long sent; float[] l, r, inter;
    readonly long[] marks = new long[256]; readonly long[] songAt = new long[256]; int head;   // which song position each block started at
    public SdlAudioOut(Sdl sdl, Action<float[], float[], int> render, int level)
    {
        this.sdl = sdl; this.render = render; int lv = level < 0 ? 1 : Math.Min(2, level);
        block = lv == 0 ? 512 : (lv == 1 ? 1024 : 2048); ahead = block * (lv == 0 ? 3 : 4);   // low latency, normal, safest
        l = new float[block]; r = new float[block]; inter = new float[block * 2];
    }
    public override void Start()
    {
        var want = new AudioSpec { Freq = StudioEngine.SR, Format = Sdl.AudioF32Sys, Channels = 2, Samples = (ushort)block };
        AudioSpec have; dev = sdl.OpenAudioDevice((byte*)null, 0, &want, &have, 0);
        if (dev == 0) throw new InvalidOperationException("the sound device could not be opened: " + sdl.GetErrorS());
        sdl.PauseAudioDevice(dev, 0); run = true;
        th = new Thread(Loop) { IsBackground = true, Name = "Purphex sound", Priority = System.Threading.ThreadPriority.Highest }; th.Start();
    }
    void Loop()
    {
        while (run)
        {
            int queued = (int)(sdl.GetQueuedAudioSize(dev) / 8);   // bytes / (2 channels * 4 bytes)
            if (queued >= ahead) { Thread.Sleep(2); continue; }
            if (queued == 0 && sent > 0 && PlayingNow != null && PlayingNow()) Underruns++;
            long song = PosNow != null ? PosNow() : 0;
            try { render(l, r, block); } catch { Array.Clear(l, 0, block); Array.Clear(r, 0, block); }
            float g = MuteAll ? 0f : Gain;
            for (int i = 0; i < block; i++) { inter[2 * i] = l[i] * g; inter[2 * i + 1] = r[i] * g; }
            fixed (float* p = inter) sdl.QueueAudio(dev, p, (uint)(block * 8));
            lock (marks) { marks[head] = sent; songAt[head] = song; head = (head + 1) % marks.Length; } sent += block;
        }
    }
    // frames handed to the device that it has not played yet
    public override int Queued() { return dev == 0 ? 0 : (int)(sdl.GetQueuedAudioSize(dev) / 8); }
    // the song position being heard: find the block that holds the frame playing now
    public override long PlayingSongPos()
    {
        if (dev == 0) return -1; long playing = sent - Queued();
        lock (marks) for (int k = 1; k <= marks.Length; k++) { int i = (head - k + marks.Length) % marks.Length; if (marks[i] <= playing && playing < marks[i] + block && (k == 1 || marks[i] > 0 || i == 0)) return songAt[i] + (playing - marks[i]); }
        return -1;
    }
    public override void Dispose() { run = false; try { th?.Join(200); } catch { } if (dev != 0) { sdl.CloseAudioDevice(dev); dev = 0; } }
}
