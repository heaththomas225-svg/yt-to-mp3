**Purphex Music for macOS and Linux: beta.** This is the same Purphex as on Windows, built for your system. It is new, so some parts may not work yet. Please report anything that breaks in the Purphex Discord and say which system you use.

**macOS** (11 Big Sur or newer)
1. Download `Purphex-macOS-AppleSilicon.zip` (M1, M2, M3, M4 Macs) or `Purphex-macOS-Intel.zip` (older Macs), and unzip it.
2. Drag **Purphex** into Applications.
3. The first time, right-click Purphex and choose **Open**, then **Open** again. (The beta is not notarised by Apple yet, so a double-click is blocked the first time.) If macOS says the app is damaged, open Terminal and run: `xattr -cr /Applications/Purphex.app`

**Linux** (64-bit, X11 or Wayland with XWayland)
1. Download `Purphex-Linux-x64.tar.gz` and unpack it: `tar -xzf Purphex-Linux-x64.tar.gz`
2. Run `./Purphex/Purphex`, or run `./Purphex/install.sh` to add it to your app menu.
3. No sound? Install SDL2 from your package manager, for example `sudo apt install libsdl2-2.0-0` (Ubuntu, Debian, Mint) or `sudo dnf install SDL2` (Fedora).

**Not in the beta yet:** the Music Hub and the Video / Photo editors (they need a web view, coming next), VST plugins from other makers, and Split Vocals + Beat (coming soon). The converter needs ffmpeg: Purphex uses the one already on your computer (Homebrew, apt) or downloads it once.

Your edition works here too: open Settings and press LINK THIS PC.
