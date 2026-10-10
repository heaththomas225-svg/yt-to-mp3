# Writing plugins for BandLab + YouTube to MP3

The Music Studio can load extra effects from plugin `.dll` files. A plugin is a small C# class that derives from `Fx`.

## Install and approve
1. Menu > **Plugins and extensions...** > **Open plugins folder** (`%LocalAppData%\YtToMp3\plugins`).
2. Drop a plugin `.dll` in the folder, press **Reload**.
3. Tick the plugin to **approve** it. Plugins are programs, so only approve ones you trust. If the file changes later it is un-approved automatically.
4. In the studio use **Add Effect > Plugins**.

A plugin that crashes is switched off (bypassed, with the error recorded); the app keeps running.

## Write your own
Press **Create sample plugin (SDK)** in the same window. It writes `plugins\sdk\SamplePlugin.cs` (a working tremolo) and `build.bat`.

```csharp
using System;
public class SampleTremolo : Fx
{
    double phase, rate, depth;
    public SampleTremolo()
    {
        Type = "sample_tremolo"; Title = "Sample Tremolo"; Category = "Plugins"; Blurb = "Example plugin";
        AddP("rate", "Rate", 0.1f, 20f, 5f, "Hz");     // id, name, min, max, default, unit
        AddP("depth", "Depth", 0f, 100f, 60f, "%");
        // AddC("mode", "Mode", new[] { "A", "B" }, 0);  // drop-down style choice
    }
    protected override void Update() { rate = Get("rate"); depth = Get("depth") / 100.0; }   // when a parameter changes
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++) { /* process l[i], r[i] in place */ }
    }
}
```

Useful members: `SR` (sample rate), `Get("id")`, `Set("id", value)`, `Latency` (override if you delay the signal; the studio compensates), `Reset()` (clear state), `GR` (gain reduction in dB for the meter), `ScL`/`ScR` (sidechain, if used). Helper classes `Biquad` and `DspMath` are public.

Parameters, presets and bypass work automatically in the studio, and the plugin's settings are saved in projects by its `Type` id.

## Custom ffmpeg filter (conversions)
In the same window you can enter any ffmpeg audio filter chain that is applied to each download, for example `aecho=0.8:0.88:60:0.4`, `flanger` or `acrusher=bits=8`. ffmpeg builds used by the app can also host **LV2** plugins (`lv2=p=<plugin URI>`). The filter is tested before it is saved.

## Not supported
VST2 / VST3 / AU hosting is not included. (The format needs a native plugin host; it has not been built or tested here.)
