# Purphex

Windows app that turns BandLab / YouTube (and other yt-dlp supported) links into audio files, with producer tools:

- **Formats:** MP3 320 / V0, WAV 24-bit, FLAC, M4A, Opus. Sample rate, trim, batch, file-name labels with the format used.
- **Mastering Assistant:** custom EQ, auto EQ, character, loudness, excite, stereo width (ffmpeg based).
- **Vocal remover:** AI source separation (Demucs) gives an instrumental and a vocals-only file.
- **Phone mode:** turn it on, open the shown address on your phone (same Wi-Fi), paste a link, download the file. A random code protects it.
- **Auto-update:** open copies check GitHub every 20 s and offer the new version.
- **Music Studio:** add a song, vocal or beat; AI split into vocals + instrumental; Auto-Tune style pitch correction, parametric EQ with spectrum, 18 effects incl. an 8D Audio Tuner, rap vocal presets, Auto Mix, mastering with LUFS / true-peak meters, export and projects. Handles hour-long songs.
- **Plugins:** drop plugin .dll files into the plugins folder (see [PLUGINS.md](PLUGINS.md)); custom ffmpeg filter for conversions.
- **Hear before you convert:** preview loudness normalization and the vocal remover on a 30 s sample.

Download `YtToMp3.exe` from the [latest release](https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest).

## Downloaded on first use (not bundled)
| Component | Used for | Licence | Size |
|---|---|---|---|
| yt-dlp | downloading | Unlicense | 17 MB |
| ffmpeg (yt-dlp builds) | converting | GPL | ~160 MB |
| Deno | YouTube JS runtime | MIT | ~90 MB |
| Python (embedded), PyTorch (CPU), Demucs + model | vocal remover (asks first) | PSF / BSD / MIT | ~0.5 GB |

Everything lives in `%LocalAppData%\YtToMp3`; nothing is installed system-wide.

## Build
```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:YtToMp3.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.FileSystem.dll /r:System.IO.Compression.dll YtToMp3.cs Mastering.cs Phone.cs Theme.cs Background.cs Changelog.cs Plugins.cs Preview.cs Studio.Dsp.cs Studio.Pitch.cs Studio.Engine.cs Studio.Sep.cs Studio.Mix.cs Studio.UI.cs Studio.UI2.cs Studio.8D.cs
```
Tests (in the repo): StudioTest.cs (DSP, engine, mixing, plugins; 61 checks), BigSongTest.cs (hour-long songs), ChunkSepTest.cs (chunked AI separation). Build with csc /main:T, /main:B or /main:S plus the source files.

## Releasing
Raise `Version` in `YtToMp3.cs`, rebuild, `gh release create vX.Y.Z YtToMp3.exe`, then set `version.txt` to the new version and push.

For personal use; only download or process content you have the right to.
