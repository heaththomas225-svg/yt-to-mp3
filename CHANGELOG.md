# Changelog

What changed in each version of **Purphex**. The same notes are shown in the app (Menu > What's new) and in the update card.

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

