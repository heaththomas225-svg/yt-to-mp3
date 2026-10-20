# Purphex on macOS and Linux

Purphex is being ported to macOS and Linux in phases, from **one codebase**: the same `.cs` files build the Windows app (`_env.sh`, .NET Framework 4) and the cross-platform projects here (.NET 8).

| Phase | What | Status |
|---|---|---|
| 1 | **Core**: audio engine, all plugins and Purphex VSTs, Bass Lab and instruments, sound library, mastering, presets | Done: builds and passes its smoke test on Windows, macOS and Linux (GitHub Actions) |
| 2 | **Sound output** on macOS (CoreAudio) and Linux (PulseAudio / PipeWire / ALSA), through SDL2 (`Purphex.Platform`) | Done: real-time playback passes the smoke test |
| 3 | **The whole app**: drawing (System.Drawing → SkiaSharp, `Purphex.Gfx`) and windows (Windows Forms → Avalonia, `Purphex.Desktop/Forms`) | Done: the full app builds, starts, and opens the converter and the studio. CI starts it on each OS and keeps a picture |
| 4 | Music Hub / editors (system web view), VST hosting per platform, separation engine per platform, microphone input | Next |
| 5 | Packaging: CI makes `Purphex.app` (zip, ad-hoc signed) and a Linux tar.gz; a signed and notarised .dmg needs the Apple developer account | Beta packages done; notarising planned |

## How the sharing works
- `Purphex.Core` links the shared engine files. Windows-only parts inside them are wrapped in `#if !PURPHEX_CORE` (the Windows build never defines it, so it compiles exactly as before). The smoke test uses it.
- `Purphex.Desktop` compiles **every** file of the Windows build as one program (the studio window is a partial class across many files, so it cannot be split across libraries), defines `PURPHEX_DESKTOP`, and adds:
  - `Forms/`: the Windows Forms API on Avalonia (controls, layout, menus, dialogs, timers, drag and drop) plus small stand-ins (web view, DPAPI key storage, machine id, the converter's tools).
  - `Purphex.Gfx`: the System.Drawing API on SkiaSharp.
  - `Purphex.Platform/SdlAudioOut.cs`: sound through SDL2.
- Sound goes through `AudioOut` (`Audio.Out.cs`): Windows uses waveOut (`Audio.WaveOut.cs`); other platforms set `AudioOut.Factory`.
- After adding a file to the Windows `SRC` list, run `node cross/tools/desktopproj.js` to update the desktop project.

## Tools (`cross/tools`)
- `node cross/tools/desktopproj.js`: rewrites the desktop project from the Windows file list (`SRC` and the `-resource:` files in `_env.sh`).
- `node cross/tools/coreproj.js "<extra files, comma separated>" "<files to leave out>"`: rewrites the core project from the Windows file list (`core-files.txt` is the current list of files forced in).
- `node cross/tools/autoport.js File.cs ...`: wraps studio-window and plugin-window code in a shared file for Windows only.
- `node cross/tools/guard.js File.cs class|member|line|using <name>`: wraps one piece.
- `node cross/tools/movetypes.js New.cs "<comment>" File.cs:Class ...`: moves data classes out of a UI file into a shared file.

## Run it
- The app: `dotnet run --project cross/Purphex.Desktop -c Release` (any OS with the .NET 8 SDK; on Windows it runs the Avalonia version, handy for checking the port).
- A picture of the window, then quit: set `PURPHEX_SNAP="shot.png;10"` (seconds to wait). `PURPHEX_SNAP_CLICK="PURPHEX MUSIC"` presses that button first (opens the studio).
- The smoke test: `dotnet run --project cross/Purphex.Smoke -c Release`.

## Releasing the beta
Push a tag named `desktop-v<version>` (for example `desktop-v3.75.1-beta1`). The `Cross-platform` workflow builds, starts and packages the app on each OS and publishes the packages as a GitHub pre-release, with `cross/packaging/BETA.md` as its notes.
