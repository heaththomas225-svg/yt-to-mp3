using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// What's new: shown from the "What's new" button and automatically once after an update.
// Keep CHANGELOG.md in the repo in sync with this text.
static class Changelog
{
    public const string Latest = "3.0.8";

    public const string Text =
@"3.0.8  Rename and Save as
 + NEW rename tracks: double-click a track name, right-click it, or press F2. Click the project name to rename the project.
 + NEW Save as button (Ctrl+Shift+S) next to Save.
 + SMOOTHER: separating vocals now leaves two CPU cores free, so the app and playback stay smooth while it works.
 + Tracks are unlimited; the only limit is your computer memory.

3.0.7  Update notes
 + FIX: the update window now wraps and scrolls its notes instead of cutting them off.
 + CLEARER: when adding music, only Full song removes the music. Vocals only and Beat only just label the track.

3.0.6  Full screen and windows
 + NEW full screen: press F11 in the main window, Music Studio, Settings, Help and more (Esc leaves it). The Studio also has a Full button.
 + CHANGE: Settings, Help, Plugins and Phone mode no longer lock the other windows, so you can use and move any window while they are open.
 + FIX: Settings scrolls when the screen is small.
 + FIX: Studio crash when the background darkness was set high.
 + FIX: background painting glitch (parts of the window showing the desktop) after changing the picture.

3.0.5  Mastering and effects panel
 + CHANGE: the Mastering button is now Master a song. It opens the Music Studio Mastering tab, where you add your own song, master it and export it. The old Mastering Assistant for downloads is still in Menu.
 + FIX: the effects panel in the Studio only scrolls as far as its content (no more endless scrolling).

3.0.4  Speed
 + FASTER: the animated leaves are drawn about 10 times faster (about 6 ms a frame, steady 60 fps). This was the main cause of the lag.
 + FIX: custom picture and video backgrounds no longer glitch while loading, and are prepared in the background so they cost almost nothing.

3.0.3  Stability
 + FIX Music Studio no longer crashes with an index-out-of-range error after Auto Mix or effect changes.
 + Dragging the Studio or Mastering window is smooth: all animation pauses while any window moves.
 + Closing the Mastering window now turns mastering off.

3.0.2  Settings, backgrounds and Studio speed
 + NEW Settings button: use your own picture or video as the background. It is cropped to fill the window automatically and remembered after you close the app.
 + NEW audio controls: playback volume, output device and buffer size. Exports are never affected.
 + NEW frame rate limit and optional fps display.
 + FIX Music Studio: Cancel (or Esc) now works while a job runs, so the window can no longer get stuck.
 + Music Studio is much lighter: text drawing cached, background cached, only redraws when something changes, smooth playhead needle, faster scrolling.

3.0.1  Smoothness + effects fix
 + FIX Music Studio: the Add buttons in the effect browser did nothing; fixed.
 + Dragging or resizing a window is now smooth: animation pauses while you move it.
 + Animation capped at 60 fps so the interface stays responsive.

3.0.0  Music Studio
 + NEW Music Studio window (button: Music Studio / ADD MUSIC): add a song, vocal or beat and work on it like a mini Logic Pro.
 + Separate a full song into real VOCAL and INSTRUMENTAL tracks with AI (Demucs), kept perfectly in sync. Or import your own vocal / beat.
 + Timeline with waveforms, mute / solo, volume faders, pan, level meters, nudge and zoom. Play everything together, solo either track.
 + Auto-Tune style Pitch Correction on the vocal: key + scale, note picker, key detection, retune speed, strength, natural vs hard.
 + Parametric EQ with a draggable curve, live spectrum, 6 bands, shelves, high-pass / low-pass and presets.
 + Searchable effect browser: EQ, compressor, multiband, exciter, saturation, distortion, reverb, delay, chorus, de-esser, imager, limiter, gate, noise reduction.
 + Original rap vocal presets: Melodic, Emotional, Technical, Aggressive, Clean Studio, Wide Atmospheric (all editable, saveable).
 + Separate instrumental chain with vocal-ducking sidechain. AUTO MIX balances and explains every change (Natural, Clean Studio, Melodic, Aggressive, Technical).
 + MASTERING tab: master EQ, compressor, multiband, exciter, saturation, imager, limiter, LUFS + true-peak meters, A/B with loudness match.
 + Export mix / vocals / instrumental / dry stems as WAV, FLAC or MP3. Save and reopen projects with all plugin settings.
 + NEW look: black-purple theme, dark title bar and controls, neon animated leaves, glowing cards. Full screen fills at once.
 + What's new button and this changelog; update notes now show in the update card.
 + Phone mode fixes: picks your real Wi-Fi address (skips VPN adapters), one-click Windows Firewall button, guess limiter on the access code.
 + NEW Help button (and F1): searchable guides for every feature, plus Show me on screen tours that spotlight the real buttons.
 + NEW 8D Audio Tuner effect: orbits the sound around your head (click the ring to park it at an angle), with headphone-ready presets.
 + NEW plugin system: drop plugin .dll files in the plugins folder (Menu > Plugins), approve them, and they appear in the effect browser. SDK sample included. Plus a custom ffmpeg filter box for conversions.
 + NEW hear-before-you-convert: the Hear button previews loudness normalization, and the vocal remover can preview a 30 second sample, switching between versions instantly.
 + NEW type exact numbers: click any value (volume, pan, offset, plugin parameters, EQ frequency / gain / Q, target loudness, 8D angle) and type it.
 + Long songs and DJ mixes: streaming load, AI separation in crossfaded chunks with a time-left estimate, streaming export with flat memory, cancel any time, and long originals unload from RAM after separation.
 + Faster vocal separation: the AI model loads while you pick the file and stays warm between songs, with tuned settings (about 20% faster plus no startup wait after the first use).
 + Phone page redesigned: neon look, big touch controls, GPU-smooth animation that runs at your phone's full refresh rate, recent downloads, format chips.
 + Smoother app: high-precision frame loop, pre-rendered sprites and background layers, quality that adapts to keep the frame rate high; the fps shows in the title bar.

2.5.0  Vocal remover and phone mode
 + Vocal remover: pick a song, get an instrumental and a vocals-only file (AI, one-time ~0.5 GB download).
 + Phone mode: open a web page on your phone (same Wi-Fi), paste a link, download the file.
 + Mastering Assistant window and format labels in file names (e.g. Song [MP3 320k mastered -14LUFS]).

2.3.0 / 2.2.x  Quality of life
 + All settings auto-save; window size and position are remembered.
 + Several links can be pasted on one line (commas or spaces).
 + BandLab support (tracks, posts, albums, collections). Update checks every 20 seconds.

2.0.0 / 2.1.x  Producer features
 + MP3 320 / V0, WAV 24-bit, FLAC, M4A, Opus; sample rate; trim; loudness normalise; batch queue; cancel; file-name styles.
 + Live update checks while the app is open; in-app update card.

1.x  First releases
 + YouTube to MP3 converter with auto-update. Fixes for YouTube 403 errors (bundled JavaScript runtime), any yt-dlp site, text search, animated purple theme.";

    public static void Show(IWin32Window owner)
    {
        var f = new Form { Text = "What's new", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Color.FromArgb(10, 4, 20), ForeColor = Color.FromArgb(240, 232, 255), ClientSize = new Size(720, 560), Font = new Font("Segoe UI", 10f) };
        Win.Dark(f);
        var tb = new TextBox { Left = 14, Top = 14, Width = 692, Height = 484, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), Text = Text.Replace("\r\n", "\n").Replace("\n", "\r\n") };
        Win.DarkControl(tb); f.Controls.Add(tb);
        var ok = new Button { Text = "Close", Left = 596, Top = 510, Width = 110, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        ok.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); ok.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.Add(ok);
        f.Shown += (s, e) => { tb.SelectionStart = 0; tb.SelectionLength = 0; };
        f.ShowDialog(owner);
    }
}
