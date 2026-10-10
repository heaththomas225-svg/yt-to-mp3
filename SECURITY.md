# Purphex: what it does on your PC (and why antivirus programs sometimes complain)

Purphex is a normal Windows program written in C# and built with the compiler that comes with Windows. The full source code is in this repository. It is **not code-signed yet**, and few people have downloaded it, so some antivirus programs (Malwarebytes among them) treat a new, unsigned program that downloads tools and updates itself with suspicion. That is a **false positive**: nothing in the program is hidden or malicious. Nobody can promise that no antivirus will ever complain about an unsigned program, but this page says exactly what Purphex does so you (and the antivirus companies) can check.

## What Purphex does
- Runs as a normal user (it asks for no administrator rights, `requestedExecutionLevel = asInvoker`).
- Writes only to its own data folder `%LocalAppData%\YtToMp3` and to the folders you pick (your music, your projects, your exports).
- Starts nothing at boot. It adds no startup entries, no scheduled tasks and no registry keys.
- Does not read the keyboard when it is not the active window, hook other programs, inject into other processes or read other programs' data. (Keyboard piano keys only work while a Purphex window is in front.)
- Downloads only what you ask for: the audio or video of links you paste.

## Downloads the program makes (all over HTTPS from the project's own sources)
| What | From | Checked how |
| --- | --- | --- |
| yt-dlp (does the downloading) | github.com/yt-dlp/yt-dlp releases | SHA-256 against the checksum list the yt-dlp project publishes; a mismatch is deleted and never run |
| ffmpeg (converts audio and video) | github.com/yt-dlp/FFmpeg-Builds releases | SHA-256 against the checksum list published next to it |
| Deno (a helper YouTube needs) | github.com/denoland/deno releases | SHA-256 against the checksum published next to it |
| Vocal remover (optional) | python.org embeddable Python, pip (bootstrap.pypa.io), PyTorch (download.pytorch.org and pypi.org), the Demucs model | only if you press the vocal remover and agree to its set-up |
| Updates to Purphex | github.com/heaththomas225-svg/yt-to-mp3 releases | only when you press Update now; the file must match the SHA-256 published in `version.txt`, and the address must be this project's releases page |

## Things that can look odd to a scanner (and are normal here)
- **It updates itself**: after you press Update now it downloads the new program, checks its SHA-256, renames the old one and starts the new one.
- **Phone mode** (optional) starts a small web server on your own network. Windows asks for permission once (a firewall rule); you can say no.
- **VST plugins**: like any music program, it can load plugin files (.dll / .vst3) that **you** choose to enable.
- It starts helper programs (yt-dlp, ffmpeg) and ends them with `taskkill` when you press Cancel.

## Verify a download
Each release lists its SHA-256. In PowerShell: `Get-FileHash "Purphex Music.exe" -Algorithm SHA256` and compare.

## If your antivirus blocks or deletes it
1. Report it as a false positive. For Malwarebytes: open Malwarebytes, Detection History, choose the item, then *Report false positive*; or use the form at malwarebytes.com/support. Include the SHA-256 of the file and this page's address. Microsoft Defender and others have similar forms.
2. Add the folder where you keep Purphex (and `%LocalAppData%\YtToMp3`) to the antivirus's exclusions/allow list.
3. Restore the file from quarantine.

## The real fix: code signing
Reputation systems trust programs signed with a code-signing certificate from a recognised authority. Getting one (for example Microsoft's Trusted Signing, or an OV/EV certificate) costs money and needs the publisher's identity to be verified, so it has to be done by the project owner. Once releases are signed, these warnings mostly go away. Until then, expect the occasional false positive on a fresh download.
