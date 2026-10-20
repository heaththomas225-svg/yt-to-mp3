# Purphex on macOS and Linux

Purphex is being ported to macOS and Linux in phases, from **one codebase**: the same `.cs` files build the Windows app (`_env.sh`, .NET Framework 4) and the cross-platform projects here (.NET 8).

| Phase | What | Status |
|---|---|---|
| 1 | **Core**: audio engine, all plugins and Purphex VSTs, Bass Lab and instruments, sound library, mastering, presets | Done: builds and passes its smoke test on Windows, macOS and Linux (GitHub Actions) |
| 2 | **Sound output** on macOS (CoreAudio) and Linux (PulseAudio / PipeWire / ALSA), through SDL2 (`Purphex.Platform`) | Done: real-time playback passes the smoke test |
| 3 | **The studio window**: the drawing layer (System.Drawing → SkiaSharp) and the window host (Avalonia) | Next |
| 4 | Plugin windows, VST3 hosting per platform, Music Hub / editors (system web view), separation engine per platform | Planned |
| 5 | Packaging: macOS .dmg (signed and notarised, needs the Apple developer account), Linux AppImage | Planned |

## How the sharing works
- `Purphex.Core` links the shared files from the repo root. Windows-only parts inside them are wrapped in `#if !PURPHEX_CORE` (the Windows build never defines it, so it compiles exactly as before).
- `Purphex.Core/Platform/` holds the small cross-platform stand-ins (file helpers, edition limits, a Windows interface name).
- Sound goes through `AudioOut` (`Audio.Out.cs`): Windows uses waveOut (`Audio.WaveOut.cs`); other platforms set `AudioOut.Factory`.

## Tools (`cross/tools`)
- `node cross/tools/coreproj.js "<extra files, comma separated>" "<files to leave out>"`: rewrites the core project from the Windows file list (`core-files.txt` is the current list of files forced in).
- `node cross/tools/autoport.js File.cs ...`: wraps studio-window and plugin-window code in a shared file for Windows only.
- `node cross/tools/guard.js File.cs class|member|line|using <name>`: wraps one piece.
- `node cross/tools/movetypes.js New.cs "<comment>" File.cs:Class ...`: moves data classes out of a UI file into a shared file.

## Run the smoke test
`dotnet run --project cross/Purphex.Smoke -c Release` (any OS with the .NET 8 SDK).
