using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// What's new: shown from the "What's new" button and automatically once after an update.
// Keep CHANGELOG.md in the repo in sync with this text.
static class Changelog
{
    public const string Latest = "3.16.0";

    public const string Text =
@"3.16.0  Built-in plugin library
 + About 115 named effects are now built in, grouped as Pitch & Voice, Telephone & Distortion, Echo & Reverb and EQ & Clarity. Add them from + Add Effect and pick a Category.
 + New engines behind them: a phase-vocoder pitch shifter with up to four voices and formant control, a classic low-delay shifter, formant shifter, frequency shifter, vocoder, comb resonators, a waveshaper with oversampling, a bit crusher, an echo with tape, ping-pong, multi-tap, reverse and ducking, a feedback-network reverb (room, hall, plate, chamber, spring, shimmer, gated, reverse), convolution reverb with your own impulse responses, dynamic EQ, linear-phase EQ, a vowel filter, a resonance suppressor, a spectral shaper and an automatic EQ.
 + The effect list can be filtered by category, and effects hide the settings they do not need.

3.15.0  Purphex Sounds: sample browser and sound packs
 + NEW SOUNDS button in Purphex Music opens a sound browser: search by name, pack, style, key or tempo, filter by genre, style, type, key and BPM (with half and double time), plus favourites, recents and your own collections.
 + Click any sound to hear it instantly, with a waveform you can click to jump, play / pause / stop, loop, autoplay and a preview volume.
 + Drag a sound onto the timeline to make a track right where you drop it, or double-click to add it at the playhead. MIDI files fill an instrument track and synth presets go into Bass Lab.
 + The pack manager installs, remakes and removes packs (about 100 packs: drum and bass, UK bassline, UK garage, electronic bass, rap and vocal, cinematic FX and Purphex Signature). Every sound is made on your computer by Purphex itself, with MIDI, stems, presets and a licence in each pack.
 + Import your own samples or a pack .zip. New Toolkit sets (22) gather the same sounds by job: kicks, risers, vocal chops and more.

3.14.2  Safer for antivirus programs
 + The program now carries proper version information (name, publisher, version) and a manifest that says it needs no administrator rights.
 + Updates are checked against a SHA-256 published with each release, and only accepted from this project's releases page.
 + yt-dlp, ffmpeg and Deno are checked against the checksums their own projects publish before they are used.
 + The optional Phone mode firewall step now shows its window instead of hiding it.
 + New SECURITY.md explains everything the program does and how to report a false positive to Malwarebytes.
 + FIXED windows getting stuck behind each other: Purphex Music, Settings, Help and Mastering are now their own windows, so you can click any of them (or its taskbar button) to bring it to the front. Closing the converter closes the others. The side menu has a Converter window entry in View.
 + FIXED overlapping: the lyric text boxes now step aside when the side menu or a popup is open, and the track-editing buttons only show on the track view.

3.14.1  The program is called Purphex Music
 + The program file is now Purphex Music.exe. When an older copy updates itself, it also leaves a Purphex Music.exe next to it (your shortcut keeps working).
 + Behind the scenes: the sound library engine (used for the coming Purphex Sounds) and a steadier filter for sound design.

3.14.0  Purphex
 + NEW the Purphex emblem is the program icon, the default profile picture and the logo in the main window.
 + The app is simply called Purphex now.
 + One MP3 choice: the duplicate MP3 V0 entry is gone, MP3 320 kbps stays (your saved format choice is carried over).
 + A link that has been converted is removed from the box (links that failed stay so you can retry).
 + Purphex Music: the Add your music box is a button that opens your files, and it lights up when you drag songs over the window to drop them.

3.13.1  Side menu opens closed
 + FIXED: the side menu used to open with Project already expanded. It now opens with every section closed; click a section to open its list, click it again to close it.

3.13.0  Side menu, My Projects and Community
 + NEW side menu (the three-lines button at the top left of Purphex Music) like BandLab: Project (New, Recent Projects, My Projects, Open, Save, Save as, Set project picture, Export), Edit, Tools, View, Community, Settings, Help and Exit.
 + NEW My Projects page: every project you save or open as a card with a picture (add your own with the camera button; it is kept inside the project file), newest first, with a menu to open, change the picture, show in folder or remove from the list.
 + NEW profile picture and name.
 + NEW Community page (a place to build on later: it shows your profile and four Coming soon sections).

3.12.3  Open finds your music
 + FIXED: the Open button in Purphex Music only listed project files. It now lists mp3, wav, flac, m4a, ogg and other audio too: pick one or several and they are added as tracks (several at once now load one after the other instead of only the first). Project files dropped on the window open as projects.

3.12.2  Add Track is always visible
 + FIXED: adding a track is no longer hidden. There is a + TRACK button in the toolbar (add music or an instrument below or above the selected track, or at the end), a + ADD TRACK button under the last track that stays pinned to the bottom of the list however many tracks you have, and a + circle on every line between tracks.

3.12.1  Lyrics page is just for your own words
 + REMOVED the rhyme finder and the AI writing helper (and the AI key) from the Lyrics page. It is now a plain page for writing your own lyrics.

3.12.0  Lyrics page, cleaner text
 + NEW LYRICS page in Purphex Music: write your own lyrics with section labels and line, word and syllable counts. Lyrics are saved in your project.
 + FIXED squeezed text: the top toolbar buttons are wider with bigger labels (SEPARATE VOCALS is now SPLIT VOCALS + BEAT), the window cannot be made too small, labels in Bass Lab no longer get cut off, and the zoom slider no longer sits on the NEW button.

3.11.1  Steadier vocal removal on long songs
 + FIXED: songs split into pieces for vocal removal are now levelled by the whole song instead of each piece alone, so the stems match at the joins.

3.11.0  Move around like BandLab, thinner tracks, add track, VST3
 + NEW Purphex Music: the mouse wheel moves along the song when your tracks fit on screen, Ctrl + wheel zooms, and there is a zoom slider in the toolbar.
 + NEW an overview bar along the bottom shows the whole song: drag its lit window to move, drag its ends to zoom, double-click to fit. The middle mouse button drags the view, touch pads can swipe sideways.
 + NEW tracks are thinner (Alt + wheel changes the height) and zooming now stops at sensible limits instead of breaking the display.
 + NEW + ADD TRACK HERE: hover the line between tracks (or above the first, or below the last) to add music or an instrument at that spot.
 + NEW (experimental) VST3 plugin support next to VST2. Tested with a stand-in plugin only.
 + NEW the Bass Lab and Purphex Music screens are now checked by automated mouse tests.

3.10.0  Heartbeat waveforms, help pages, more modulation
 + NEW Purphex Music: audio tracks now draw as one clean glowing line (blue, your track colour, purple) with a point every few pixels, so the shape is easy to read.
 + NEW the song fills the track area by itself after FIT, including in full screen and when you resize the window. Zooming or scrolling yourself turns that off until you press FIT.
 + NEW Bass Lab: 16 matrix slots (two pages) and 62 targets. FM and ring amounts, sub and noise pan, saturation, phaser, flanger, stereo width and EQ can be modulated.
 + NEW Help: pages for video downloads, the neon studio and zooming, the LFO drawing, modulation, envelopes, the effect chain and presets.

3.9.0  YouTube MP3 Converter By Phurex
 + NEW video downloads: pick an MP4 format (best quality up to 4K and beyond, 1440p, 1080p, 720p, 480p, 360p) or MKV best quality. Works with links from YouTube and 1000+ other sites, not just music.
 + NEW the app is now called YouTube MP3 Converter By Phurex, with a cleaner, easier to read main window.
 + NEW Purphex Music: neon look with glowing track outlines, LED level meters and light strips in each track colour.
 + NEW Purphex Music: Alt + mouse wheel makes the tracks taller, Ctrl + mouse wheel zooms deeper into the waveform.
 + NEW Bass Lab: draggable envelope graphs, an 11-slot effect chain (distortion, saturation, compressor, EQ, phaser, flanger, chorus, delay, reverb, stereo width, bass boost) you can reorder, and a preset browser with 54 presets in 13 styles.
 + NEW Bass Lab presets: save, rename, duplicate, delete, and Initialise Patch. Voice limit and tuning are remembered between sessions.

3.8.0  Bass Lab: modulate almost any knob
 + NEW drag and drop: grab DRAG on an LFO, envelope or macro and drop it on a knob. A coloured ring shows how far it moves the knob. Ctrl + drag the knob to change the amount.
 + NEW 44 targets: oscillator fine, pan and blend, filter drive, and the effects (distortion, chorus, delay, reverb, bass boost).
 + NEW matrix slots have Bipolar / Positive / Negative, an on/off switch and a remove button.
 + FIXED the AMOUNT knob covering a button, and drop-down lists that stayed open (they now close when you pick, click away, or leave them).

3.7.0  Bass Lab: draw your own LFO
 + NEW wavetable list now has Sine, Sawtooth, Square, Pulse 25% and Triangle. The Sub oscillator offers sine, triangle, square and saw.
 + NEW LFO editor: click the picture to add points, drag them, right-click to delete, double-click to make a point round, drag the diamond between points to bend the line.
 + NEW shape presets (sine, saw, stepped, random, three wobbles) you can reshape. Each of the 4 LFOs keeps its own drawn shape, saved with the patch.
 + NEW one-shot mode (play the shape once and hold), snap-to-grid, and a marker that follows the real LFO while notes play.
 + The picture shows exactly what moves the sound, including smoothing and unipolar range.

3.6.0  Bass Lab: wavetables, mixer and filters
 + NEW wavetable oscillators: pick a source (sine, triangle, saw, square, pulses, organ...) or a wavetable (9 factory tables) and scan its frames with WAVE. Band-limited, so high notes stay clean.
 + NEW DRAW WAVE: draw your own waveform, or up to 8 frames to scan between. Saved with the patch.
 + NEW warp types (Sync, Bend, Fold, Mirror, Asym, Crush), FM and ring modulation between oscillators.
 + NEW MIX page: level, pan, mute, solo, meters and routing for every source, with master output meters.
 + NEW filters: 10 types and 12 to 48 dB slopes, with a curve that matches the sound. Serial or parallel routing.
 + NEW noise types: brown, blue and crackle.

3.5.1  Calmer editing
 + Clicking a track now only selects it. The needle moves only when you click the ruler or the empty area under the tracks.
 + The button row (Up, Dn, move, Split, trim, Copy, Del) and the clip grips appear only on the track you clicked.
 + NEW Changelog button on the main window. Updates no longer pop up the list by themselves; a star on the button shows when there is something new.

3.5.0  Record and make songs
 + NEW MIC button: record a microphone onto a new vocal track while the song plays. Takes are lined up with the beat and saved as files. Pick the input in Settings.
 + NEW piano roll (the Notes button on an instrument track, or double-click it): draw, move, resize and delete notes, snap to the grid.
 + CHANGE: REC now starts the song by itself when you play your first note.
 + NEW button: start a fresh empty project.

3.4.0  Bass Lab piano fix
 + FIX: the Bass Lab piano (and any instrument) now makes sound straight away. Before, the sound device was only opened when you pressed PLAY, so keys were silent until then. This is tested on the real sound device.
 + NEW: PANIC button (top bar and GLOBAL page) stops every note and tail at once.
 + NEW: voice limit, master tune, test sound button and live audio and voice status.
 + NEW: MIDI keyboard input (pick your device on the GLOBAL page). Notes, pitch bend and sustain pedal work. A clear message appears when no device is found.
 + NEW: piano velocity: click lower on a key for a louder note.

3.3.0  Easier editing
 + Drag a clip anywhere to move it. A click still puts the needle.
 + Hover cues: the cursor changes, clips light up with grips on their ends, and a label next to the mouse says what a drag will do.
 + The selected track has a button row: Up, Dn, move a grid step, Split, trim start, trim end, Copy, Del.
 + Drag a track name up or down to change the order of the tracks.

3.2.1  Purphex (complete build)
 + This update completes 3.2.0: Analyze whole mix now moves the Mastering EQ, and split tracks are named after the song.

3.2.0  Purphex: Bass Lab and DAW tools
 + NEW NAME: the app is now Purphex. The studio is Purphex Music.
 + NEW Bass Lab: a built-in synthesizer for bass and leads. Three warping oscillators, sub, noise, two filters, four envelopes, four tempo-synced LFOs (wobbles), a mod matrix, macros, effects and its own piano. Add it with + INSTRUMENT.
 + NEW Bass Boost: a fine-tunable low-end boost (frequency, amount, width, harmonics, tighten). In Bass Lab, and as a stand-alone effect.
 + NEW DAW tools: SPLIT a track at the needle, TRIM it, drag clips to move them, DUP, DEL, UNDO and REDO, BPM with DETECT, beat-grid SNAP and a metronome.
 + CHANGE: Analyze whole mix now also listens to the tone of your mix and moves the Mastering Parametric EQ to balance it (bass, mud, presence, air). The curve changes and a list tells you what moved, with Undo.
 + CHANGE: after splitting vocals and beat, the tracks are named after the song, for example My Song - Vocals and My Song - Instrumental.
 + FIX: Auto Mix now works on a full song before it is split into vocals and beat.
 + FIX: toolbar and transport text no longer overlap at any window width.
 + FASTER updates: the app checks for a new version every few seconds and when you come back to it.

3.1.0  VST plugins and instruments
 + NEW: VST plugin support. 64-bit VST2 instruments (like Vital) and effects now work. Menu > Plugins and extensions lists the VST2 plugins installed on your PC; tick one and it loads.
 + NEW: + INSTRUMENT in the Music Studio adds a track for a VST instrument, opens its own window with a piano, and lets you play it with your keyboard.
 + NEW: REC records the notes you play while the song plays. They play back in the mix and in every export, and are saved with the project.
 + VST effects appear under + Add Effect > PLUGINS with an Open window button.
 + Not supported yet: VST3 (.vst3) and 32-bit plugins.

3.0.13  Search box
 + FIX: the Add Effect search box clearly shows it is ready to type (glowing border, blinking cursor) and has a clear button.

3.0.12  Smooth playback
 + FIX: Music Studio playback stays smooth on slower computers: bigger audio safety buffer, a top-priority audio thread, and the window redraws less often when your computer is busy.
 + NEW: if the sound ever starts breaking up, the app makes the playback buffer bigger by itself and tells you (Settings > Audio).
 + CLEARER: VST and other native plugins (for example Vital) are listed as not supported instead of showing an error.

3.0.11  Plugins are easier
 + CHANGE: ticking a plugin loads it straight away (no Reload needed) and tells you where to find it.
 + CHANGE: your own plugins are listed first in the Music Studio Add Effect list, under PLUGINS.

3.0.10  More help
 + NEW hover hints: point at any button in the Music Studio and a plain-English explanation appears at the bottom.
 + NEW Help guides: Settings, full screen and shortcuts, Full song vs Vocals only vs Beat only, rename and Save as, the Add Effect list, and the sample plugin.
 + CLEARER: Create sample plugin now explains it is a template for developers and offers to build it so you can try it.

3.0.9  Effect list fix
 + FIX: the Add effect list now stops at its last row instead of scrolling forever.

3.0.8  Rename and Save as
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
