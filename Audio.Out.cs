using System;
using System.Threading;

// Where the studio's sound goes. The engine asks AudioOut.Create for a player and hands it a render callback;
// Windows uses waveOut (Audio.WaveOut.cs), macOS and Linux use the backend the cross-platform host sets in AudioOut.Factory.
abstract class AudioOut : IDisposable
{
    public Func<long> PosNow; public Func<bool> PlayingNow;   // the engine: where the song is, and whether it plays
    public abstract void Start();
    public abstract long PlayingSongPos();   // the place in the song being heard right now (-1 when it cannot tell)
    public abstract int Queued();            // frames handed to the device that it has not played yet
    public abstract void Dispose();
    public static volatile int QueuedFrames = 4096;   // audio waiting in the buffers: how far the sound lags behind the render position
    public static volatile int Underruns;       // times the device ran out of audio (heard as stutter)
    public static volatile int Stalls;          // times the device stopped taking audio for a moment
    public static volatile float Gain = 1f;     // playback volume from Settings (exports are not affected)
    public static readonly bool MuteAll = Environment.GetEnvironmentVariable("PURPHEX_MUTE") == "1";   // tests never play out loud
    public static Func<Action<float[], float[], int>, int, AudioOut> Factory;   // set by a host to use its own backend
    public static AudioOut Create(Action<float[], float[], int> render, int level)
    {
        if (Factory != null) return Factory(render, level);
#if PURPHEX_CORE
        return new NullAudioOut(render);
#else
        return new WaveOutPlayer(render, level);
#endif
    }
}
// no sound device (a server, a test, or a host that has not set a backend yet): the song still moves in real time
sealed class NullAudioOut : AudioOut
{
    readonly Action<float[], float[], int> render; Thread th; volatile bool run; long sent; readonly float[] l = new float[1024], r = new float[1024];
    public NullAudioOut(Action<float[], float[], int> render) { this.render = render; }
    public override void Start() { run = true; th = new Thread(() => { var sw = System.Diagnostics.Stopwatch.StartNew(); while (run) { long due = (long)(sw.Elapsed.TotalSeconds * StudioEngine.SR); while (sent < due) { render(l, r, 1024); sent += 1024; } Thread.Sleep(5); } }) { IsBackground = true, Name = "Purphex null audio" }; th.Start(); }
    public override long PlayingSongPos() { return -1; }
    public override int Queued() { return 0; }
    public override void Dispose() { run = false; }
}
