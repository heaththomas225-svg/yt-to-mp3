# Changelog

What changed in each version of **Purphex**. The same notes are shown in the app (Menu > What's new) and in the update card.

## 3.19.0  Parameter automation

- New: automation. Press the small A button next to any plugin setting to draw a curve that moves it while the song plays and in every export. Click to add points, drag them, right-click to delete, switch the lane on and off, and flip between the lanes on a track. Lanes are saved with the project.
- Settings glide smoothly along the curve (updated every 256 frames), and a list of choices such as Off / On switches in steps.
- Ctrl+Z now also brings back the plugins on a track after you applied a Rapper Vocal Style (it said it would, but only the volume and clip changes were undone before).

## 3.18.2  NVIDIA turbo engine for vocal splitting

- PCs with an NVIDIA graphics card can now run the vocal splitting on it with the CUDA version of PyTorch, which is several times faster than before. The app asks once before splitting: it needs a one-time download of about 2.5 GB (about 5 GB on disk). Say No and it never asks again; the normal engines keep working.
- The turbo engine tests itself the first time: it is only used if it is clearly faster than the processor and gives the same result. If it does not start, or CUDA is not available (for example an old graphics driver), the app quietly goes back to the faster DirectML or processor engine.
- The separation log (separation.log in the Purphex data folder) now also records which engine and graphics card each split used.

## 3.18.1  Rapper Vocal Styles, a truthful time-left, faster start for splitting

- New: Rapper Vocal Styles. 36 original plugin chains for rap vocals in 11 subgenres (Trap, Melodic Trap, Drill, Boom Bap, Old School, Grime and UK, Cloud and Lo-Fi, Rage and Hyper, Pop Rap, Phonk, Storytelling). Search, filter by subgenre, star your favourites, then Use (replace the plugins) or Add (put them after yours). Find it as Vocal Styles in the plugin panel or in Menu > Tools.
- The time left while splitting vocals + beat is now accurate. It is worked out from how fast the splitting is going right now, not from the whole time so far (which included the start-up and was often several times too high).
- Splitting starts loading the AI model as soon as you press Split vocals + beat, while you choose the track, so it starts working sooner.
- The progress message now says what to expect, and every split writes a short line to separation.log in your Purphex data folder (which engine and device it used and how long each step took), so a slow PC can be understood.

## 3.18.0  Much faster vocal splitting, Worldwide Echo and Melody Tune

- Split vocals + beat is much faster: the AI engine now uses your graphics card when that is quicker (a 60 second song went from 28 seconds to 10 on the test PC), and a newer, faster processor path when it is not. The result is the same as before.
- The first split after updating downloads the faster engine once (about 250 MB, 1.4 GB on disk). If it cannot be installed or does not work on your PC, the old engine keeps working.
- New plugin: Worldwide Echo (7 versions). A wide tempo-synced echo with reverb on the repeats, ducking, and a Send pattern that only echoes chosen moments such as the last beat of each bar.
- New plugin: Melody Tune (8 versions). Pitch correction to a key and scale with tune speed, strength and humanise, plus harmony layers (thirds, fifths, octaves, choir).
- Fixed a rare crash in echo-type plugins when a delay time landed exactly on a whole sample.

## 3.17.10  Lighter on the CPU, faster website

- The main window animated background no longer hogs the processor in a big or full-screen window: it rests longer after each repaint when the window is large.
- The website loads faster: smaller logo pictures, compressed pages and long-lived caching.
- Community songs can have a photo (add it when you share, or change it later from the song).

## 3.17.9  Audit fixes: safer saves, layers and rows, smoother buttons

- Project, lyrics, settings and library files are now written safely (a crash or power cut mid-save can no longer leave a half-written file).
- Duplicating or splitting a layer keeps it on the same row, and Split vocals + beat adds its tracks in the right place, so rows never get out of order.
- Buttons ease in and out when you hover them, and the window has rounded corners on Windows 11.
- The website is faster and works properly on phones (menu drawer fixed, no zoom when typing, pages are compressed).

## 3.17.8  Smoother dragging, Count In + Metronome, Grid size, track pickers, presets

- Much smoother dragging (clip pictures cached while you hold them, no zoom re-fit, faster grid).
- COUNT IN with the big on-screen count, and a working METRONOME button.
- Grid size button with a description, visible grid lines and a snap guide. Detect Track key and Split vocals + beat ask which track.
- Presets (was Chain presets) with Echo, AdLib and Reverb bus presets.
- Main window repaints its buttons after resizing (no grey blocks in full screen).

## 3.17.7  Key box, vocal remover files, SNAP button replaced

- Detect Track key shows the key in a neon box on screen.
- Vocal remover: vocals, beat and original as separate files; the original is never duplicated.
- The main window scales with its size (text and controls). Fixed pieces of another window showing through it when it was sized up.
- Removed the BandLab wording from the main window and help.
- SNAP button replaced by 1 2 3 (Count In); snap stays on (Menu can turn it off).

## 3.17.6  Count In, key finder, shared rows, simpler toolbar, project cards

- 1 2 3 is the Count In (one bar of clicks before playing or recording). Detect Track key replaces the Auto Mix style box.
- Toolbar: no more + TRACK, + BUS, + INSTRUMENT. Bass Lab is first in the effect list.
- Split keeps both pieces on the same track; drop a clip or track onto any row to share it (overlapping). Vertical scroll bar and thicker scroll bars.

- Project pictures show the whole picture in a square (no cropping) inside a neon glow.
- Hover a project on Community for Open, Rename, Picture, Describe, Tags and Folder. Tags and description show on the card.
- Cards show the size of the project and its audio (KB / MB / GB).

## 3.17.5  Dragging tracks, no import question

- Dragging a track or clip shows a see-through glowing copy that follows the mouse smoothly; rows slide into place and the drop row lights up.
- Inside Purphex Music, ADD MUSIC puts the file straight on the chart (no "What is this file?" window).
- Main window: ADD MUSIC asks what you want (edit in Purphex Music, vocal remover, or trim start and end and save as MP3 / WAV). The Master a song button is gone.

## 3.17.4  Discord button, one frame-rate readout

- Community page: Join the Discord button (invite comes from the website).
- Removed the corner frame-rate badge from the converter window; the title bar shows it.

## 3.17.3  Community page, simpler buses, recording and dragging fixes

- Community and My Projects have no music controls (slim bar); Community redesigned: left menu, round neon project cards, profile, tags, activity.
- Buses are a small Bus drop-down on each track; levels and plugins are in + BUS.
- Recording goes onto the empty audio track you made. Dragging a clip to another row (or below the last) moves it, making a track if needed; Up / Dn make a row when there is none.
- One frame-rate readout (title bar).

## 3.17.2  Buses, empty audio tracks, roomier tracks

- Buses: + BUS adds as many as you like. Press Out on a track to send it to a bus; each bus has a fader, pan, meters and its own plugins. Saved with the project.
- New Track sheet is simple: Voice / Audio (empty and raw, nothing records or gets effects), Virtual Instruments, MIDI Track and Bus. ARM + MIC records onto an empty audio track.
- Wider, taller track headers.

## 3.17.1  New Track sheet

- + ADD TRACK opens a New Track sheet: record voice with AutoPitch, rap vocal, speech, radio voice, guitar or bass (effects already on the track), add an instrument, browse drum beats and loops, or import a file.
- Removed the glitchy "add track here" strips between tracks.

## 3.17.0  Flex Pitch, the whole plugin library, faster and safer

- Flex Pitch: edit the notes of a recording on a piano-roll (pitch, fine, drift, vibrato, gain, formant, correct to scale, split/join); live re-tuning; kept with the project.
- All 218 listed plugins are built in (Vocal Tools, Creative, Bass & EDM added).
- FPS counter badge on every screen; 120 fps option.
- Performance and stability fixes from a full review of the app and the website.

## 3.16.0  Built-in plugin library

- About 115 named effects: Pitch & Voice, Telephone & Distortion, Echo & Reverb, EQ & Clarity (pitch shifters, formant, vocoder, tape / ping-pong / reverse echoes, plate / hall / spring / shimmer / gated reverbs, convolution reverb, dynamic and linear-phase EQ, resonance suppressor and more).
- Effect browser: filter by category; effects hide settings they do not need.

## 3.15.0  Purphex Sounds: sample browser and sound packs

- New SOUNDS button: search and filter sounds (genre, style, type, key, BPM with half/double time), favourites, recents, collections.
- Instant preview with waveform seek, loop, autoplay and volume. Drag onto the timeline or double-click to add; MIDI fills instrument tracks; presets go to Bass Lab.
- Pack manager installs ~100 procedurally synthesised packs on your PC (no recordings, licence per pack). Import your own samples or a pack zip. 22 Toolkit sets.

## 3.2.0  Purphex: Bass Lab and DAW tools

- Renamed Purphex (studio = Purphex Music).
- Bass Lab synthesizer + Bass Boost; split/trim/move/duplicate/undo, BPM + grid, metronome.
- Auto Mix works before splitting; layout fixes; updates within seconds.

## 3.1.0  VST plugins and instruments

- VST2 instruments (Vital) and effects supported; + INSTRUMENT tracks with keyboard, REC and export.
- Not yet: VST3 and 32-bit plugins.

## 3.0.13  Search box

- Add Effect search box shows it is active and has a clear button.

## 3.0.12  Smooth playback

- Studio playback robust on slow computers; buffer grows automatically if sound breaks up.
- VST / native plugins shown as unsupported instead of an error.

## 3.0.11  Plugins are easier

- Ticking a plugin loads it at once; plugins are listed first in Add Effect.

## 3.0.10  More help

- Hover hints on Studio buttons; 6 new Help guides; the sample plugin explains itself and can build itself.

## 3.0.9  Effect list fix

- Add effect list stops at its last row.

## 3.0.8  Rename and Save as

- Rename tracks (double-click, right-click or F2) and the project.
- Save as button (Ctrl+Shift+S).
- Separating vocals leaves CPU free so the app stays smooth.

## 3.0.7  Update notes

- Update notes scroll and wrap.
- Clearer Add Music choices.

## 3.0.6  Full screen and windows

- F11 full screen in every resizable window (Esc leaves).
- Settings, Help, Plugins and Phone mode no longer block other windows.
- Settings scrolls; Studio crash with high darkness fixed; background paint glitch fixed.

## 3.0.5  Mastering and effects panel

- Master a song opens the Studio Mastering tab (add, master, export). The old Mastering Assistant for downloads is in Menu.
- Effects panel no longer scrolls endlessly.

## 3.0.4  Speed

- Animated leaves drawn about 10x faster (about 6 ms per frame, steady 60 fps). This was the main cause of the lag.
- Custom picture and video backgrounds no longer glitch while loading and cost almost nothing.

## 3.0.3  Stability

- FIX Music Studio crash (index out of range) after Auto Mix or effect changes.
- Smooth dragging for Studio and Mastering windows.
- Closing the Mastering window turns mastering off.

## 3.0.2  Settings, backgrounds and Studio speed

- NEW Settings button: custom picture or video background (auto-cropped, remembered), darken slider, leaves on or off.
- NEW audio controls: playback volume, output device, buffer size. Exports are not affected.
- NEW frame rate limit and optional fps display.
- FIX Music Studio: Cancel (or Esc) works while a job runs.
- Music Studio is much lighter and the playhead is smooth.

## 3.0.1  Smoothness + effects fix

- FIX Music Studio: the Add buttons in the effect browser did nothing; fixed.
- Dragging or resizing a window is now smooth: animation pauses while you move it.
- Animation capped at 60 fps so the interface stays responsive.

## 3.0.0  Music Studio

- NEW Music Studio window (button: Music Studio / ADD MUSIC): add a song, vocal or beat and work on it like a mini Logic Pro.
- Separate a full song into real VOCAL and INSTRUMENTAL tracks with AI (Demucs), kept perfectly in sync. Or import your own vocal / beat.
- Timeline with waveforms, mute / solo, volume faders, pan, level meters, nudge and zoom. Play everything together, solo either track.
- Auto-Tune style Pitch Correction on the vocal: key + scale, note picker, key detection, retune speed, strength, natural vs hard.
- Parametric EQ with a draggable curve, live spectrum, 6 bands, shelves, high-pass / low-pass and presets.
- Searchable effect browser: EQ, compressor, multiband, exciter, saturation, distortion, reverb, delay, chorus, de-esser, imager, limiter, gate, noise reduction.
- Original rap vocal presets: Melodic, Emotional, Technical, Aggressive, Clean Studio, Wide Atmospheric (all editable, saveable).
- Separate instrumental chain with vocal-ducking sidechain. AUTO MIX balances and explains every change (Natural, Clean Studio, Melodic, Aggressive, Technical).
- MASTERING tab: master EQ, compressor, multiband, exciter, saturation, imager, limiter, LUFS + true-peak meters, A/B with loudness match.
- Export mix / vocals / instrumental / dry stems as WAV, FLAC or MP3. Save and reopen projects with all plugin settings.
- NEW look: black-purple theme, dark title bar and controls, neon animated leaves, glowing cards. Full screen fills at once.
- What's new button and this changelog; update notes now show in the update card.
- Phone mode fixes: picks your real Wi-Fi address (skips VPN adapters), one-click Windows Firewall button, guess limiter on the access code.
- NEW Help button (and F1): searchable guides for every feature, plus Show me on screen tours that spotlight the real buttons.
- NEW 8D Audio Tuner effect: orbits the sound around your head (click the ring to park it at an angle), with headphone-ready presets.
- NEW plugin system: drop plugin .dll files in the plugins folder (Menu > Plugins), approve them, and they appear in the effect browser. SDK sample included. Plus a custom ffmpeg filter box for conversions.
- NEW hear-before-you-convert: the Hear button previews loudness normalization, and the vocal remover can preview a 30 second sample, switching between versions instantly.
- NEW type exact numbers: click any value (volume, pan, offset, plugin parameters, EQ frequency / gain / Q, target loudness, 8D angle) and type it.
- Long songs and DJ mixes: streaming load, AI separation in crossfaded chunks with a time-left estimate, streaming export with flat memory, cancel any time, and long originals unload from RAM after separation.
- Faster vocal separation: the AI model loads while you pick the file and stays warm between songs, with tuned settings (about 20% faster plus no startup wait after the first use).
- Phone page redesigned: neon look, big touch controls, GPU-smooth animation that runs at your phone's full refresh rate, recent downloads, format chips.
- Smoother app: high-precision frame loop, pre-rendered sprites and background layers, quality that adapts to keep the frame rate high; the fps shows in the title bar.

## 2.5.0  Vocal remover and phone mode

- Vocal remover: pick a song, get an instrumental and a vocals-only file (AI, one-time ~0.5 GB download).
- Phone mode: open a web page on your phone (same Wi-Fi), paste a link, download the file.
- Mastering Assistant window and format labels in file names (e.g. Song [MP3 320k mastered -14LUFS]).

## 2.3.0 / 2.2.x  Quality of life

- All settings auto-save; window size and position are remembered.
- Several links can be pasted on one line (commas or spaces).
- BandLab support (tracks, posts, albums, collections). Update checks every 20 seconds.

## 2.0.0 / 2.1.x  Producer features

- MP3 320 / V0, WAV 24-bit, FLAC, M4A, Opus; sample rate; trim; loudness normalise; batch queue; cancel; file-name styles.
- Live update checks while the app is open; in-app update card.

## 1.x  First releases

- YouTube to MP3 converter with auto-update. Fixes for YouTube 403 errors (bundled JavaScript runtime), any yt-dlp site, text search, animated purple theme.

