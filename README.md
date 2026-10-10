# YouTube to MP3

Windows app that converts YouTube links to MP3 (uses yt-dlp + ffmpeg, downloaded automatically on first run).

Download `YtToMp3.exe` from the [latest release](https://github.com/heaththomas225-svg/yt-to-mp3/releases/latest). The app checks `version.txt` on launch and offers to update itself.

## Releasing an update
1. Raise `Version` in `YtToMp3.cs`.
2. Build with `csc` (see below), then publish a new GitHub release with `YtToMp3.exe` attached.
3. Set the version in `version.txt` to match and push.

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:YtToMp3.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.FileSystem.dll /r:System.IO.Compression.dll YtToMp3.cs
```

For personal use; only download content you have the right to.
