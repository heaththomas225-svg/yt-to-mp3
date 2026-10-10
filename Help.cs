using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  Help: searchable guides + "Show me on screen" guided tours (spotlight overlay on the real controls).
// ======================================================================
class HelpTopic
{
    public string Id, Category, Title, Body; public bool HasTour;
    public HelpTopic(string id, string cat, string title, bool tour, string body) { Id = id; Category = cat; Title = title; HasTour = tour; Body = body; }
}

static class HelpContent
{
    public static readonly HelpTopic[] All = {
        new HelpTopic("start", "Basics", "Convert a link to MP3", true, @"
## What you need
A YouTube, BandLab or other video / music link (or just words to search for).
## Steps
1. Paste the link into the big box at the top. You can paste several links, one per line or separated by commas.
2. Pick a Format. MP3 320 kbps is the best all-round choice.
3. Press Convert. The first time, the app downloads its tools once (a few minutes). After that it is quick.
4. When it finishes your folder opens. Files are saved in the 'Save to' folder.
TIP: No link? Type words such as an artist and song name. The app searches YouTube and converts the top result.
TIP: Everything you change is remembered next time."),

        new HelpTopic("formats", "Basics", "Formats and quality: which should I pick?", false, @"
## The choices
- MP3 320 kbps: plays everywhere, very good quality. Best default.

- M4A (AAC 256): great on iPhone and Apple Music.
- FLAC / WAV 24-bit: no extra compression loss, but the audio on YouTube is already compressed, so they sound the same as MP3 320 and are much bigger. Use them if you are going to edit or master the file.
- Opus: smallest files for the quality.
## Sample rate
Leave it on Original unless your project needs 44.1 kHz or 48 kHz.
## File names
Every file is labelled with its format, for example 'Song [MP3 320k]', so you can keep track.
TIP: A 'mastered' tag in the name means the Purphex Mastering processed it."),

        new HelpTopic("bandlab", "Basics", "Using BandLab links", false, @"
## What works
Track, post, revision, album and collection links from BandLab (for example bandlab.com/post/...). Albums and collections convert every track automatically.
## How to get the link
1. Open the song on BandLab.
2. Use Share and Copy link.
3. Paste it into the app.
## If it fails
- Profile pages (bandlab.com/username) and some other page types are not supported by the downloader. Open the track itself and copy its link.
- Private tracks need the share link, and may not download at all.
TIP: You can mix YouTube and BandLab links in the same box."),

        new HelpTopic("batch", "Basics", "Several songs, playlists and search words", false, @"
## Many at once
Paste several links (one per line, or separated by commas or spaces). They are converted one after another and the progress bar shows which one is running.
## Playlists
Tick 'Whole playlists' to convert every video in a playlist link. Leave it off to convert only the one video in the link.
WARN: A link ending in list=LL is your Liked videos playlist. With 'Whole playlists' on, that converts all of them.
## Cancel
Press Cancel at any time. Files already finished are kept."),

        new HelpTopic("trim", "Basics", "Cut just a section (trim)", false, @"
## Steps
1. Type a Trim start and/or Trim end such as 1:30 or 0:01:30.5.
2. Convert. Only that part is downloaded and converted.
TIP: Leave the end empty to go to the end of the song, or the start empty to begin at 0:00."),

        new HelpTopic("normalize", "Basics", "Normalize loudness and the Hear preview", true, @"
## What it does
Brings every download to the same standard loudness (-14 LUFS, the level streaming services use). Quiet songs get louder, loud songs are controlled.
## Listen before you convert
1. Press the Hear button next to Normalize loudness.
2. Pick a file (or say yes to use the first link in the box; it downloads a 30 second sample).
3. A player opens. Click Original or Normalized to switch instantly while it plays and hear exactly what the setting does.
4. Close the player, tick Normalize loudness if you like it, then Convert.
TIP: Raise your volume the same for both versions. The numbers under each button show the real loudness."),

        new HelpTopic("vocal", "Basics", "Remove vocals from a song", true, @"
## Steps
1. Press Vocal remover, then 'Remove vocals from a song'.
2. The first time, say yes to the one-time download (about 0.5 GB). It stays on your PC.
3. Choose the song. You get two files: an instrumental (no vocals) and a vocals-only file, in your chosen format.
## Hear it first
Choose 'Preview: hear a 30 second sample first'. You can switch between Original, Instrumental and Vocals on a short sample before committing to the whole song.
## Speed
- The AI model loads while you pick the file, and stays ready between songs.
- Expect roughly half a song's length on a normal CPU (a 4 minute song is about 2 minutes). The progress bar shows the time left.
- Long songs are processed in pieces so memory stays low.
TIP: Cancel stops it safely."),

        new HelpTopic("phone", "Basics", "Use it from your phone", true, @"
## Steps
1. Press Phone mode on the PC. A window shows an address like http://192.168.1.64:8765/?c=123456 (also copied to the clipboard).
2. On your phone, connected to the SAME Wi-Fi as the PC, open that address in the browser.
3. Paste a link, choose a format, press Convert, then Download. On iPhone use the share button, Save to Files.
## If the page won't open
- Press 'Allow in Windows Firewall' in the Phone mode window (Windows asks for permission once).
- Check the phone is on the same Wi-Fi, not mobile data or a guest network.
- If the PC lists several addresses, use the one that starts like your phone's (usually 192.168...). VPN addresses don't work.
WARN: Phone mode is protected by the code at the end of the address. Turn it off when you're done.
TIP: Keep the app open on the PC while you use it. The PC does the converting."),

        new HelpTopic("masterassistant", "Basics", "Purphex Mastering (while converting)", true, @"
## What it is
A window with a live EQ curve, Auto EQ, Character, Loudness, Excite and Width controls that processes your downloads.
## Steps
1. Menu, then Mastering for downloads, opens it. Press the power button.
2. Pick a preset from the top, or drag the EQ dots and turn the knobs.
3. Convert as normal. Files come out processed and are labelled 'mastered'.
TIP: Reanalyze shows a spectrum of a file so Auto EQ can suggest corrections.
TIP: The same engine, plus much more, is in Purphex Music Mastering tab."),

        new HelpTopic("studio-start", "Purphex Music", "Add music and split vocals and beat", true, @"
## Open it
Press Purphex Music (or ADD MUSIC) on the main window.
## Steps
1. Press ADD MUSIC and choose a file: MP3, WAV, FLAC, AIFF and more.
2. Say what is IN the file: Full song with music, Vocals only, or Beat only. Only Full song gets split by the AI (see Full song, Vocals only, Beat only).

3. For a full song press SPLIT VOCALS + BEAT. The AI makes two real files and shows them as two tracks, perfectly in sync. The first time it asks to download the AI (about 0.5 GB).
4. Press PLAY (or Space). Use M and S to mute or solo either track.
TIP: You can also add your own vocal and your own beat as separate tracks.
TIP: Drag files onto the studio window to add them."),

        new HelpTopic("studio-mix", "Purphex Music", "Mixing: faders, mute / solo, pan, timing", false, @"
## Each track has
- Vol slider: the track level. Pan: left to right.
- M (mute) and S (solo). Solo lets you hear only that track.
- Offset arrows (< >) to nudge the track earlier or later (hold Shift for fine steps) if two recordings don't line up.
- A level meter. A red mark means it clipped; click it to clear.
## Timeline
Click the ruler or a track to move the playhead. Scroll with the wheel, Ctrl + wheel to zoom, Shift + wheel to move sideways.
TIP: Double-click a slider to reset it. Click a value to type an exact number."),

        new HelpTopic("studio-autotune", "Purphex Music", "Auto-Tune (pitch correction)", false, @"
## Steps
1. Select your vocal track, press Add Effect and choose Pitch Correction.
2. Press Detect key (or choose the key and scale yourself).
3. Press Hard Auto-Tune for the classic effect, or Natural for a subtle polish.
## Controls
- Retune speed: 0 is instant robotic tuning, higher is more natural.
- Strength: how far to pull toward the note.
- Natural: keeps vibrato (high) or flattens it (low).
- The note buttons let you allow or forbid single notes.
TIP: It only affects the vocal track. The beat is never tuned."),

        new HelpTopic("studio-eq", "Purphex Music", "EQ: shape the sound", false, @"
## Steps
1. Add Effect, then Parametric EQ, on the vocal or the beat. They are independent.
2. Drag a dot to change frequency (left to right) and gain (up and down). Mouse wheel on a dot changes Q (how wide).
3. Right-click a dot to turn the band off.
4. Use High-pass to remove rumble and Low-pass to soften the top.
5. Presets: Vocal Clarity, Warm, Bright, Bass Control, Clean Mix.
TIP: The purple area behind the curve is a live spectrum so you can see what you're adjusting."),

        new HelpTopic("studio-fx", "Purphex Music", "Effects, presets and the rap vocal chains", false, @"
## Add an effect
Press Add Effect on a track, type to search, press Add. There are 18 including EQ, compressor, multiband, exciter, saturation, reverb, delay, chorus, de-esser, stereo imager, limiter, gate, noise reduction, pitch correction and 8D.
## Edit it
Click it in the list. Drag sliders or click a value to type. Bypass turns it off, Presets lists built-in and your own.
## Whole-chain presets
Chain presets lets you load Melodic, Emotional, Technical, Aggressive, Clean Studio or Wide Atmospheric vocal chains, or the instrumental chain. Every plugin stays editable. Save your own chain with 'Save this track's chain'.
TIP: These are original sound-design starting points, not copies of any artist's settings."),

        new HelpTopic("studio-8d", "Purphex Music", "8D audio tuner", false, @"
## What it does
Makes the sound seem to circle around your head. Use headphones.
## Steps
1. Add Effect, then 8D Audio Tuner, on any track (or the master).
2. Choose a preset such as Classic 8D (10 s), or set Rotation time yourself.
3. Click or drag on the ring to park the sound at an angle and tune the position by ear.
4. Press the Hold button to stay put, or Rotating to move again.
## Controls
Width, Depth (behind you gets darker and quieter), Ear delay, Keep bass centred, Room and Mix."),

        new HelpTopic("studio-automix", "Purphex Music", "Auto Mix", true, @"
## Steps
1. Make sure you have a vocal and a beat (separate a song or add your own).
2. Choose a style next to the button: Natural, Clean Studio, Melodic Rap, Aggressive Rap or Technical Rap. The + means vocal enhancement is on.
3. Press AUTO MIX. It analyses loudness, clipping and tone, builds a chain for each track, balances the volumes and shows exactly what it changed.
4. Edit anything afterwards, or press Undo Auto Mix.
TIP: It never changes your original files."),

        new HelpTopic("studio-master", "Purphex Music", "Mastering tab: loudness and A/B", false, @"
## Steps
1. Open the MASTERING tab. The chain (EQ, compressor, multiband, exciter, saturation, imager, limiter) works on the whole mix.
2. Pick a mastering preset on the left or tweak each module.
3. Press Analyze whole mix to see loudness before and after, then compare with A ORIGINAL and B MASTERED. Loudness match keeps them equally loud so you judge tone, not volume.
4. Set a Target loudness and press 'Set loudness to target' to adjust the limiter automatically.
## Meters
Momentary, Short-term and Integrated LUFS, true peak, peak, gain reduction and clip detection."),

        new HelpTopic("studio-export", "Purphex Music", "Export and save projects", true, @"
## Export
1. Press EXPORT.
2. Tick what you want: the finished mix, vocals only, instrumental only, dry versions, or every track as a stem.
3. Choose WAV, FLAC or MP3 and a folder, then Export. Files are checked so nothing is replaced without asking.
All files start at 0:00 with the same length, so they line up in any other music software.
## Save and reopen
Save stores everything: your tracks, plugin settings and alignment. Open brings it back.
TIP: Even hour-long songs export with very little memory and you can cancel at any time."),

        new HelpTopic("typing", "Purphex Music", "Typing exact values", false, @"
Click any number (volume, pan, offset, a plugin parameter, EQ frequency / gain / Q, target loudness, 8D angle) and a small box opens. Type the value and press Enter. Esc cancels.
For pan type a number from -100 (left) to 100 (right), or L30 / R30 / C."),

        new HelpTopic("settings", "Basics", "Settings: background, animation, audio", false, @"
## Open it
Press Settings (top right of the main window) or Menu, then Settings. Everything you change applies at once and is remembered next time.
## Background
Choose any picture or video. It is cropped to fill the window automatically. Darken makes it easier to read. 'Keep the leaves' puts the floating leaves on top. Remove puts the purple leaves back.
## Animation
Frame rate limit: 60 is smoothest, 30 is lightest on a slow computer. You can show the frame rate in the title bar.
## Audio
Volume and output device only change what you hear in Purphex Music. Playback buffer: Low latency reacts fastest but can crackle on slow computers; Safe never crackles. Exported files are never affected.
TIP: Videos need ffmpeg, which is installed the first time you convert something."),

        new HelpTopic("fullscreen", "Basics", "Full screen, moving windows and shortcuts", false, @"
## Full screen
Press F11 in the main window, Purphex Music, Settings or Help. Press Esc (or F11 again) to leave. In Purphex Music there is also a Full button.
## Windows
Settings, Help and Plugins stay open without locking the other windows, so you can keep working and move any window.
## Shortcuts
F1 = Help.  F11 = full screen.  Space = play / pause in the studio.  Ctrl+S = save.  Ctrl+Shift+S = save as.  Ctrl+O = open project.  F2 = rename the selected track.  Esc = close a list or cancel a job."),

        new HelpTopic("studio-roles", "Purphex Music", "Full song, Vocals only, Beat only: what do they mean?", false, @"
When you add a file the studio asks what is IN the file. This is a label, it does not change the audio.
## Full song with music
A normal finished song (vocals and music together). Choose this, then press SPLIT VOCALS + BEAT and the AI makes a real vocals track and a real instrumental track.
## Vocals only
A file that already contains only a voice (an acapella, a rap or singing recording). Nothing is removed. If you pick this for a full song you will still hear the whole song.
## Beat only
A file that already contains only the music (an instrumental or beat).
TIP: Not sure? Pick Full song and separate it."),

        new HelpTopic("studio-rename", "Purphex Music", "Rename tracks and projects, Save as", false, @"
## Rename a track
Double-click its name, right-click the name, or select it and press F2.
## Rename the project
Click the project name at the top right (next to Save). If the project was already saved the file is renamed too.
## Save and Save as
Save writes the project. Save as (or Ctrl+Shift+S) saves a copy under a new name so you can keep versions. Open reloads a project with all tracks and effect settings.
TIP: You can add as many tracks as you like. Each loaded track uses computer memory (about 64 MB for a 3 minute song)."),

        new HelpTopic("studio-browser", "Purphex Music", "The Add Effect list", false, @"
Press + Add Effect on a track (or on the Mastering tab for the master chain). Type to search, scroll with the mouse wheel, then press Add or Enter. Esc closes it.
Effects are grouped: EQ, dynamics (compressor, limiter, gate), tone (exciter, saturation, distortion), space (reverb, delay, chorus), pitch (Auto-Tune, 8D) and your own plugins.
In a track, effects run top to bottom. Use the ^ and v buttons to change the order, the green dot to bypass, and x to remove."),

        new HelpTopic("sample-plugin", "Advanced", "What is the sample plugin?", false, @"
## What it is
'Create sample plugin' is for people who want to write their own effect. It is NOT an effect you can use straight away. It writes a small program template (a volume-wobble effect called Sample Tremolo) plus a build.bat that turns it into a plugin file.
## If you want to try it
Press Create sample plugin, then answer Yes to build it now. Press Reload, tick Sample Tremolo to approve it, then in Purphex Music use Add Effect, Plugins, Sample Tremolo.
## If you do not write code
You can ignore it. The built-in effects already cover EQ, compression, reverb, Auto-Tune and more."),

        new HelpTopic("vst", "Advanced", "Use VST plugins and instruments (like Vital)", false, @"
## What works
64-bit VST2 plugins (.dll) and, new and experimental, VST3 plugins (.vst3): instruments (synths such as Vital) and effects. 32-bit plugins are not supported. VST3 support was tested with a stand-in plugin only, so if a particular .vst3 will not load, use its VST2 version if it has one.
## Set it up
1. Menu, then Plugins and extensions. Plugins installed in the usual VST2 and VST3 folders are listed automatically (or copy the .dll into the plugins folder).
2. Tick the plugin. It loads at once (a big one can take a few seconds the first time).
## Use an instrument
1. In Purphex Music press + INSTRUMENT and choose it. A track and the plugin's own window appear.
2. Play notes with your computer keyboard (A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave) or click the piano at the bottom of its window. You hear it right away, even when the song is stopped.
3. To record: press REC, press PLAY, then play. The notes appear as small bars on the track and play back every time. Press C on the track to clear them.
4. Use the instrument's own window to change sounds. The sound you chose is saved with the project.
## Use an effect
Open + Add Effect on a track and pick it under PLUGINS (it is listed first). Press 'Open window' in the plugin panel to see its own controls.
## Good to know
- Recorded notes play in the mix and in every export.
- Only tick plugins you trust: a VST plugin is a program that runs on your computer.
- If a plugin will not load, it is usually 32-bit, or a VST3 that this app does not understand yet."),

        new HelpTopic("daw", "Music Studio", "Edit like a DAW: split, trim, move, duplicate, undo, tempo grid", false, @"
## Cut a track in two (split)
1. Put the needle where you want the cut by clicking the ruler (the numbers above the tracks) or the empty area below the tracks. Clicking a track itself never moves the needle.
2. Press SPLIT (or the key B). The track becomes two pieces. Each piece is its own track, so you can move it, mute it or put different effects on it.
## Shorten a track (trim)
- Drag the left or right END of a clip to shorten it. The audio is never deleted, you can drag it back out.
- Or put the needle inside the clip and press TRIM < (cuts the start off here) or TRIM > (cuts the end off here).
## Move a clip
Drag the clip anywhere left or right (the cursor turns into a move cross). A plain click just selects the clip: the needle stays where it is.
## Move a track up or down
Drag the track name on the left up or down, or use the Up and Dn buttons on the selected track.
## More
- Click a track to show a row of buttons at its top right (they hide again when you click the empty area): Up, Dn (change the row), << and >> (move the clip one grid step), Split, <Trim, Trim>, Copy and Del. Point at any button, clip end or clip to see what it does.
- Right-click a track for split, trim, duplicate, rename and delete.
- DUP copies the selected track, DEL deletes it (key Delete). UNDO / REDO (Ctrl+Z / Ctrl+Y) bring edits back.
## Tempo and grid
- BPM: click the number and type the tempo, or press DETECT and the studio listens to your song and sets it.
- SNAP makes moves, trims and cuts land exactly on the beat. The grid button picks the size (bar, 1/2, beat, 1/8, 1/16).
- CLICK plays a metronome while the song plays.
- The tempo also drives the synth's tempo-synced wobbles and delays.
## Zoom
Mouse wheel + Ctrl zooms, Alt + wheel makes tracks taller, or use - + FIT. After FIT the song fills the track area by itself, even in full screen. See the neon look page."),

        new HelpTopic("basslab", "Music Studio", "Bass Lab: make bass and wobbles (the built-in synth)", false, @"
## Open it
In Purphex Music press + INSTRUMENT and choose Bass Lab (synthesizer). A track and the Bass Lab window open. Play with your keyboard (A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave) or click the piano.
## Pick a sound
Click the preset name at the top (or the < > arrows) to open the preset browser: 54 presets in 13 styles, plus your own. See the Presets help page.
## Make a wobble in 3 clicks
1. Open the bottom panel tab LFO 1.
2. Press 'Filter 1 cutoff' in WOBBLE IT (or 'Osc A warp' for a growl, 'Volume' for a tremolo).
3. Turn the AMOUNT knob. Change the rate with the sync box: 1/8 = a wobble every eighth note of the song's tempo, 1/16 = twice as fast, 1 bar = slow and heavy. 'Hz' makes it free-running.
Your tempo comes from the BPM in the studio, so the wobble stays in time with the beat.
## The pages
- OSC: three wavetable oscillators. SOURCE picks Wavetable or a fixed shape (sine, triangle, saw, square, pulses, half and rectified sine, organ). With Wavetable, the table menu picks Basic Shapes, Saw to Square, Vowels, Growl, Organ Drawbars, Digital Bell, Harmonic Sweep, PWM, Metallic or your own Custom 1-3, and WAVE scans between the frames of the table. WARP bends the wave (Sync, Bend, Fold, Mirror, Asym, Crush). FM and RING let the next oscillator (A is shaped by B, B by C, C by A) modulate this one, even when its level is 0. DRAW WAVE opens an editor to draw your own wave and up to 8 frames. UNISON, DETUNE and BLEND make it fat. Then a sub oscillator, noise (white, pink, brown, blue, crackle) and two filters.
- MIX: level, pan, MUTE, SOLO, level meter and a Goes-to menu for each source (Filter 1, Filter 2, Both, or Direct to the output), plus the master volume and output meters.
- FX: a chain of 11 effects you can switch on and reorder (see the effect chain help page), including BASS BOOST.
- MATRIX: 16 connections from a source (LFO, envelope, velocity, key, macro) to any of 62 targets, each with an amount, polarity, on and off. The quick way is to drag the DRAG chip onto a knob (see the make anything move page).
- GLOBAL: volume, glide, voices (Poly / Mono / Legato), bend range.
## Filters
Two filters with types Low-pass, High-pass, Band-pass, Notch, Peak, Low shelf, High shelf, Comb + and -, and Formant (vowels). Slope 12, 24, 36 or 48 dB per octave. The curve shows exactly what the filter does; drag it left and right for cutoff, up and down for resonance. Serial runs Filter 1 into Filter 2; Parallel runs them side by side. KEY makes the cutoff follow the note.
## Bass boost (fine-tune it)
Turn it on with the BASS BOOST switch at the top. On the FX page drag the dot on the graph: left / right = frequency, up / down = amount. Double-click a knob to type an exact number (for example 55 Hz and 8 dB). HARMONICS adds overtones so you can hear the bass on small speakers. TIGHTEN removes the rumble you cannot hear. There is also a stand-alone Bass Boost effect under + Add Effect.
## Tips
- Every knob: drag up/down, mouse wheel for small steps (Shift = finer), double-click to type, right-click to reset.
- Hold REC in the studio and play to record your notes onto the track."),

        new HelpTopic("basslab-lfo", "Music Studio", "Bass Lab: draw your own LFO shape", false, @"
## What an LFO does
An LFO is a slow wave that moves a knob for you. Aim it at the filter and you get a wobble. Bass Lab has four, LFO 1 to LFO 4, in the tabs at the bottom.
## Draw the shape
- Click on empty space in the picture to add a point.
- Drag a point to move it. Right-click a point to delete it.
- Double-click a point to make it round (smooth) or sharp again.
- Drag the small diamond between two points to bend that stretch of the line.
- If a ready-made shape is showing (sine, saw...), your first click turns it into a drawn shape you can edit.
- SHAPE PRESETS has sine, triangle, saws, square, pulse, stepped, random steps, smooth random and three wobbles. SNAP puts points on the grid.
## Speed and options
- Rate: pick a note length (1/8, 1 bar...) to stay in time with the song, or Hz for a free speed.
- Start: Retrigger restarts the shape on every note, Free keeps it running.
- Range: Bipolar swings both ways around the knob, Unipolar only goes up.
- Loop: One-shot plays the shape once and holds its end, Loop repeats.
- PHASE shifts where it starts, SMOOTH rounds off the corners, FADE IN brings it in slowly.
The picture shows exactly what moves the sound (including smooth and range). While you play, a white marker follows the real LFO.
Each LFO keeps its own drawn shape and it is saved in the patch."),

        new HelpTopic("basslab-mod", "Music Studio", "Bass Lab: make anything move (drag and drop, matrix)", false, @"
## Drag and drop (quickest)
1. Find the DRAG chip on an LFO, an envelope (ENV 2 to ENV 4) or a MACRO.
2. Press on it and drag it onto any knob, then let go.
3. A coloured ring appears around the knob: that is how far the source moves it.
4. Hold Ctrl and drag the knob up or down to make the ring bigger or smaller (or negative).
If the knob cannot be moved by a source, a message tells you. There are 62 targets: oscillator wave, warp, level, fine tune, pan, blend, FM and ring amounts, sub and noise, both filters, volume, pan and the effects (distortion, saturation, chorus, phaser, flanger, delay, reverb, EQ, stereo width, bass boost).
## The matrix page
MATRIX shows all 32 connections (16 slots, two pages: SLOTS 1-8 and SLOTS 9-16). Each row: source, target, amount, polarity, an ON switch and an X to remove it.
- Bipolar: the source swings both ways. Positive: it only pushes up. Negative: it only pushes down.
- ON / OFF switches a connection without losing it.
- X clears the row.
## Effects follow the newest note
Targets in the effects (for example Reverb mix) follow whichever note you played last."),

        new HelpTopic("basslab-env", "Music Studio", "Bass Lab: envelopes (shape every note)", false, @"
## What they are
An envelope shapes a sound over time: how fast it starts (ATTACK), how fast it falls (DECAY), where it rests while you hold the key (SUSTAIN) and how it fades after you let go (RELEASE).
- ENV 1 always shapes the volume. A pluck has sustain 0 and a short decay. A pad has a slow attack and a long release.
- ENV 2, 3 and 4 are free: drag their DRAG chip onto any knob.
## Edit the picture
Drag the three dots: the first sets ATTACK, the second sets DECAY (left and right) and SUSTAIN (up and down), the third sets RELEASE. The knobs on the right do the same and let you type exact values (double-click)."),

        new HelpTopic("basslab-fx", "Music Studio", "Bass Lab: the effect chain", false, @"
## The chain
On the FX page you see 11 boxes in a row. Sound goes through them from left to right: Distortion, Saturation, Compressor, EQ, Phaser, Flanger, Chorus, Delay, Reverb, Stereo width and Bass boost.
- Click a box to edit it below.
- Click the light in the box to switch it on or off.
- The < and > buttons in a box move it earlier or later. Order matters: distortion before reverb sounds different from reverb before distortion.
## Wet / dry
Every effect has a WET/DRY knob: 0 is the untouched sound, 100 is all effect.
## Good habits
- Saturation and Compressor first keep the bass solid. Reverb and Delay last keep it clean.
- The Compressor has a MAKEUP knob to win back the volume it takes away.
- STEREO WIDTH keeps bass below MONO BELOW in the middle so it stays strong on speakers.
Your order and every setting are saved with the patch and with your presets."),

        new HelpTopic("basslab-presets", "Music Studio", "Bass Lab: presets, saving and starting fresh", false, @"
## Open the browser
Click the preset name at the top of Bass Lab (or use the < > arrows to step through them).
- Left: 13 styles (Wobble, Reese, Growl, Dubstep, DnB, Neuro, Riddim, Sub, Lead, Pluck, Pad, Keys, SFX), plus My presets.
- Right: the presets in that style. Click one to load it.
## Your own presets
- SAVE AS... keeps the current sound under a name you choose. It stores everything: oscillators and drawn waves, filters, envelopes, LFO shapes, modulation, the effect chain and voice settings.
- DUPLICATE makes a copy you can change.
- RENAME and DELETE work on your own presets (the built-in ones are protected).
- INITIALISE PATCH resets everything to a plain, clean sound to start from scratch.
## Remembered between sessions
Voice limit, master tuning, bend range and velocity are yours, not the sound's: they stay the same when you change preset and when you restart the app. Presets live in your data folder (Menu > Open data folder > synthpresets)."),

        new HelpTopic("download-video", "Basics", "Download videos (MP4) from YouTube and other sites", false, @"
## How
1. Paste a link. It can be YouTube, BandLab, or almost any video or music page (1000+ sites). A direct link to a video file works too.
2. In Format choose a VIDEO entry: MP4 best quality (up to 4K and beyond), 1440p, 1080p, 720p, 480p, 360p, or MKV best quality.
3. Press Convert. The finished video is saved in your Save to folder.
## Which quality?
- Best quality: the biggest picture the site offers.
- 1080p / 720p: smaller files, plays everywhere.
- MKV: keeps the original picture and sound without re-encoding. Some players need VLC.
If a site only has a lower quality than you picked, you get the best it has.
## Good to know
- Trim start and end work for video too.
- Sample rate and Normalize loudness are for audio, so they switch off when you pick a video format.
- Playlists: tick Whole playlists to take every video.
- A few sites need you to be signed in or block downloads. The log shows the reason.
- Only download what you have the right to keep."),

        new HelpTopic("studio-flex", "Purphex Music", "Flex Pitch: edit the notes of a vocal", false, @"
## What it does
Flex Pitch listens to a recording of one voice (or one instrument) and shows every note as a block on a piano-roll grid. Drag a block up or down to change that note's pitch. The track plays your edits straight away, and the length and timing stay exactly as they were.
## Opening it
Select the track, then press FLEX in the tool row (or Tools > Flex Pitch). The recording is analysed (a few seconds for a song-length take) and the timeline zooms to it. Flex Pitch works best on a single voice or instrument, not a full mix: use Split vocals + beat first.
## Editing notes
- Drag a note up or down: it snaps to whole semitones. Hold Alt to move freely.
- Click a note to select it; Shift-click adds more; drag a box around notes to select several; Ctrl+A selects all.
- Drag the left or right edge of a note to change where it starts or ends.
- Double-click a note to reset it. Right-click for more: split, join, snap to the nearest note, steady the pitch, take out the vibrato.
- The white line inside each note is the real pitch of the voice (drift and vibrato), after your edits.
## The note controls (left side)
Pitch (semitones) and Fine (cents) move the note. Drift removes slow wandering of the pitch. Vibrato makes the fast wobble smaller (0%) or bigger (200%). Gain changes the volume of the note. Formant makes the voice darker or brighter without changing the pitch. Double-click a slider to reset it.
## Correct pitch
Choose a key and scale (the button next to Correct pitch), set Amount, then press Correct pitch: the selected notes (or all notes) snap to the nearest note in that scale. 100% is hard and exact; lower values keep some of the natural feel.
## Finishing
DONE keeps your edits. The edited audio is saved as a new WAV file in the Purphex data folder and your project points to it. Original puts the recording back as it was. Undo also brings the original back.
## Good to know
- Notes are shown where the recording is on the timeline, so you can see the lanes above while you work.
- Very fast singing, whispers, and noisy recordings may give fewer notes. Use the Split tool, or drag note edges, to fix a note that was cut in the wrong place."),

        new HelpTopic("studio-plugins", "Purphex Music", "The built-in plugin library (pitch, voice, echo, reverb, EQ...)", false, @"
## Adding effects
Select a track and press + Add Effect (or use the master chain). The list now holds over a hundred named effects. Use the Category button to pick a group, or just type: telephone, demon, plate, tape echo, notch...
## What is in it
- Pitch & Voice: pitch shifters (up to four voices), formant shifter, octave up and down, doublers, harmonizers, unison, detune, pitch glide, hard tune and robot pitch.
- Telephone & Distortion: telephone, radio, walkie-talkie, megaphone, police radio, lo-fi, saturation (tube, tape, soft), overdrive, clippers, fuzz, bitcrusher, ring modulator, frequency shifter, comb filter, vocoder, talkbox, robot, demon and monster voices.
- Echo & Reverb: slapback, tape, analog, ping-pong, multi-tap, reverse, ducking and tempo-synced delays; room, hall, plate, chamber, spring, shimmer, gated, reverse and ambient reverbs; early reflections; convolution reverb (with your own impulse-response files).
- EQ & Clarity: graphic, channel, vintage and analog EQ, dynamic EQ, linear-phase EQ, tilt, shelves, notch, formant (vowel) filter, mid-side EQ, vocal presence, air, mud remover, harshness reducer, resonance suppressor, spectral shaper and automatic EQ.
## Good to know
- Every effect is real signal processing that runs on your computer. Several named effects can share one engine with different starting settings, and some chain two or three engines together.
- Parameters you do not need are hidden. Double-click a slider to go back to the starting value of that effect.
- Pitch and delay-based effects can add a small delay; Purphex lines the tracks up for you.
- The fixed-interval harmonizers add the notes you choose (they do not follow your key)."),

        new HelpTopic("studio-sounds", "Purphex Music", "Purphex Sounds: the sample browser and sound packs", false, @"
## Opening it
Press SOUNDS (next to MIC) or Tools > Purphex Sounds. A panel opens on the right of the Mix page. Press it again, or the x, to close it.
## Getting sounds
Sounds come in packs. Press Packs, pick a pack and press Install. The sounds are made on your computer by Purphex's own sound design in a few seconds: they are not recordings and not other people's samples. Installed packs show how many sounds and how much space they use. Remake rebuilds a pack, Remove deletes its files (you can install it again any time). Install all does a whole list.
## Finding a sound
- Type in the search box: a name, a pack, a style, a key or a tempo (for example: reese 174, F min, kick).
- Genre, Style, Type, Key and BPM open lists to narrow it down. BPM can also match your project's tempo, and can include half and double time.
- Toolkit gives ready-made sets (all kicks, all risers, vocal chops...).
- Favourites, Recent, One-shots, Loops, MIDI and Collections are quick filters. Clear resets everything.
## Hearing it
Click a sound and it plays straight away (turn Autoplay off if you prefer to press Play). The big waveform shows where you are: click or drag it to jump. Loop repeats the sound, vol sets the preview volume.
## Using it
- Drag a sound from the list onto the timeline: it becomes a new track at the place you drop it, lined up with the grid when Snap is on.
- Or double-click it, or press Add to timeline: it goes at the playhead.
- MIDI files go onto an instrument track (drag them onto its lane): the notes appear there.
- Bass Lab presets are added to Bass Lab's My presets.
- If a loop's tempo differs from your project you are told.
## Your own sounds
Import adds your own samples (wav, mp3, flac, midi...) to the My samples pack, or a sound pack .zip. Right-click a sound or press Collection to put it in a collection of your own. Each pack keeps its licence: press Licence."),

        new HelpTopic("studio-menu", "Purphex Music", "The side menu, My Projects with pictures, and Community", false, @"
## The side menu
Press the three-lines button at the top left of Purphex Music. Your picture and name are at the top (click the picture to change it, click the name to rename yourself). Click Project, Edit, Tools, View, Community, Settings, Help or Exit to open its list.
- Project: New Project, Recent Projects (the last ones you opened, with their pictures), My Projects, Open, Save, Save as, Set project picture, Export.
- Edit: undo, redo, split, trim, duplicate and delete.
- Tools: Auto Mix, split vocals and beat, detect BPM, metronome, mastering, add an instrument or track.
- View: switch between Mix, Mastering, Lyrics and My Projects, fit the whole song, thin / normal / tall tracks, full screen.
## My Projects
Every project you save or open is remembered here, newest first, as a card with a picture. Click a card to open it. The camera button adds or changes the picture (choose a JPG, PNG, GIF or BMP: it is cropped to a square and kept inside the project file, so it travels with it). Right-click a card for more (show in folder, remove the picture, remove it from the list). The x only removes a project from this list: your file stays on your PC.
Projects you saved in the usual folder before are found automatically.
## Open also finds your music
The Open button lists project files and audio (mp3, wav, flac, m4a, ogg...). Pick one or several songs and they are added as tracks.
## Community
A page that is reserved for the community side of Purphex. It is not built yet: it shows your profile (picture and name) and four boxes marked Coming soon (Feed, Following, Challenges, Collabs). Nothing you make is shared with anyone."),

        new HelpTopic("studio-lyrics","Purphex Music", "Lyrics page: write your own lyrics", false, @"
## Write
Press LYRICS at the top right of Purphex Music. Type your lyrics in the big box. The section buttons (Verse, Chorus, Pre-chorus, Bridge, Hook, Intro, Outro) put a label like [Verse] at the cursor.
Under the box you see the number of lines, words and syllables, and the syllables of the line you are on, so your bars stay the same length. The count is a good guess, not a dictionary, so a few words may be off by one.
## Saving
Your lyrics are saved inside your project (Save), and also as a draft on your PC as you type, so they are never lost.
## Export
Copy puts the lyrics on the clipboard. Save .txt writes a text file. Clear empties the page.
This page never writes or suggests lyrics for you: every word is yours."),

        new HelpTopic("studio-neon","Purphex Music", "The neon look, zooming and taller tracks", false, @"
## The look
Every track has its own glowing colour. The outline of its clip, the light strip beside it, the little LED and the level meter all use it, and the glow gets brighter with the volume.
## Move along the song and zoom (like BandLab)
- Mouse wheel: when all your tracks fit on screen the wheel moves along the song; when you have more tracks than fit, it scrolls up and down (and Shift + wheel moves along).
- Ctrl + mouse wheel zooms in and out around the mouse. The zoom slider in the toolbar does the same (drag right to zoom in; double-click it to see the whole song).
- The overview bar along the bottom shows the whole song with a lit window for what you see. Drag the window to move, drag its ends to zoom, click outside it to jump, double-click it to fit the whole song.
- Hold the middle mouse button and drag to move the view in any direction. On a touch pad, swipe sideways.
- Alt + mouse wheel makes the tracks taller or thinner.
- FIT in the toolbar shows the whole song again. After FIT the song fills the area by itself, including in full screen.
## Add a track exactly where you want it
Move the mouse to the line between two tracks (or above the first, or under the last) in the track list. A + ADD TRACK HERE button appears. Press it to add music or an instrument at that spot."),

        new HelpTopic("makesong", "Music Studio", "Record and make a song (instruments, microphone, piano roll)", false, @"
## Make music with an instrument
1. Press + INSTRUMENT and choose Bass Lab (or a VST such as Vital). Play it with your computer keyboard (A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave) or click the piano.
2. To record your playing: press REC (it turns red), then play a key. The song starts by itself and your notes are recorded onto the track. Press STOP when you are done.
3. Open the Notes button on the track (or double-click the track) for the piano roll: draw notes by dragging on the grid, drag a note to move it, drag its right end to resize it, right-click to delete it. Everything you draw plays right away. 'Snap notes to grid' tidies your timing.
4. Set the tempo with BPM (or DETECT). Turn on CLICK for a metronome and SNAP to make everything land on the beat.
## Record your voice or an instrument with a microphone
1. Plug in a microphone. Use headphones, so the beat is not picked up by the microphone.
2. Put the needle where you want to start and press MIC. The song plays while you record. Press MIC again, or STOP, to finish.
3. The take appears as a new vocal track, lined up with the beat, and is saved in Music / yt2mp3 / Recordings. Choose which input to use in Settings, Recording input.
4. Not happy? Press UNDO or DEL and record again.
## Put it together
Add beats with ADD MUSIC, layer instruments and recordings on their own tracks, move and split pieces, use AUTO MIX for a first balance, then EXPORT. NEW starts a fresh empty project, and Save keeps everything (notes, recordings and sounds).
"),

        new HelpTopic("plugins", "Advanced", "Plugins and custom ffmpeg filters", false, @"
## Add your own effects
1. Menu, then Plugins and extensions, then Open plugins folder.
2. Drop a plugin .dll in, press Reload and tick it to approve it.
3. It loads at once. In Purphex Music press + Add Effect on a track; your plugins are listed first under PLUGINS (or type the name in the search box), then press Add. It is then an effect on that track like any other.
WARN: Plugins are programs. Only approve ones you trust.
## Make one
Press 'Create sample plugin (SDK)'. It writes a working example and a build script. See PLUGINS.md for the full guide.
## Custom ffmpeg filter
Enter any ffmpeg audio filter chain (for example aecho=0.8:0.88:60:0.4) and it is applied to every conversion. It is tested before saving. LV2 plugins can be used through ffmpeg's lv2 filter.
64-bit VST2 plugins such as Vital are supported (see Use VST plugins and instruments). VST3 and 32-bit plugins are not. The sample plugin is for people who write their own effects."),

        new HelpTopic("long", "Advanced", "Long songs and DJ mixes", false, @"
- Loading streams the file, so even hour-long mixes load in seconds with a progress bar.
- Vocal separation processes long songs in overlapping pieces, so memory stays low. A time-left estimate is shown.
- Export streams to disk. WAV is limited to about 4 GB (roughly 3.5 hours at 24-bit); use FLAC or MP3 beyond that.
- After separating a long song its original track is unloaded from memory. Un-mute it and it loads back from disk.
TIP: Zoom out with Ctrl + wheel to see the whole mix."),

        new HelpTopic("updates", "Advanced", "Updates and what's new", false, @"
The app checks for new versions every 20 seconds while it is open. When one appears a card pops up with what is new: press Update now and it restarts by itself, or Later.
Menu, then What's new shows the full changelog. The title bar shows the current frame rate.
TIP: Your settings and projects are kept across updates."),

        new HelpTopic("trouble", "Advanced", "Troubleshooting", false, @"
## 'ffmpeg isn't installed yet'
Convert any link once. The first conversion installs the tools.
## A YouTube link gives a 403 error
Let the app finish its first-run setup (it installs a small JavaScript runtime). Try again; the downloader also updates itself.
## Phone can't connect
See 'Use it from your phone': firewall button, same Wi-Fi, correct address.
## No sound in the studio
Check Windows output device and that the track isn't muted or soloed elsewhere. Press Space to play.
## It feels slow
The animation adapts automatically, or untick 'Animated background'. For separation, close other heavy programs.
## Something else
Menu, then Open data folder shows settings, tools and the plugins folder.") };
}

// ----------------------------------------------------------------------
class HelpForm : Form
{
    readonly Func<string, bool> canTour; readonly Action<string> runTour; ListBox list; TextBox search; RichTextBox view; Button show; List<HelpTopic> shown = new List<HelpTopic>();
    public HelpForm(Func<string, bool> canTour, Action<string> runTour, string selectId)
    {
        this.canTour = canTour; this.runTour = runTour;
        Text = "Help"; FormBorderStyle = FormBorderStyle.Sizable; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(820, 520); ClientSize = new Size(960, 640);
        BackColor = Color.FromArgb(10, 4, 20); ForeColor = Color.FromArgb(240, 232, 255); Font = new Font("Segoe UI", 10f); Win.Dark(this); ShowInTaskbar = true;
        search = new TextBox { Left = 14, Top = 14, Width = 270, BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left };
        search.TextChanged += (s, e) => Fill(null); Controls.Add(search);
        Controls.Add(new Label { Text = "Search help (for example: phone, vocals, eq)", Left = 14, Top = 40, Width = 270, Height = 18, ForeColor = Color.FromArgb(150, 130, 190), Font = new Font("Segoe UI", 8.5f) });
        list = new ListBox { Left = 14, Top = 64, Width = 270, Height = 520, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 28, BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        list.DrawItem += DrawItem; list.SelectedIndexChanged += (s, e) => ShowTopic(); Controls.Add(list); Win.DarkControl(list);
        view = new RichTextBox { Left = 298, Top = 14, Width = 648, Height = 570, ReadOnly = true, BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, DetectUrls = false, Cursor = Cursors.Arrow };
        Controls.Add(view); Win.DarkControl(view);
        show = new Button { Text = "Show me on screen", Left = 298, Top = 596, Width = 220, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(190, 60, 200), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Visible = false };
        show.FlatAppearance.BorderColor = Color.FromArgb(220, 140, 255); show.Click += (s, e) => { var t = Current(); if (t != null) { Close(); runTour(t.Id); } }; Controls.Add(show);
        var close = new Button { Text = "Close", Left = 846, Top = 596, Width = 100, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        close.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); close.Click += (s, e) => Close(); Controls.Add(close);
        Fill(selectId);
    }

    HelpTopic Current() { return list.SelectedIndex >= 0 && list.SelectedIndex < shown.Count ? shown[list.SelectedIndex] : null; }

    void Fill(string selectId)
    {
        string q = search.Text.Trim().ToLowerInvariant(); shown.Clear(); list.Items.Clear(); int sel = 0;
        foreach (var t in HelpContent.All)
            if (q.Length == 0 || t.Title.ToLowerInvariant().Contains(q) || t.Body.ToLowerInvariant().Contains(q) || t.Category.ToLowerInvariant().Contains(q))
            { if (selectId != null && t.Id == selectId) sel = shown.Count; shown.Add(t); list.Items.Add(t.Title); }
        if (shown.Count > 0) list.SelectedIndex = sel; else { view.Clear(); view.Text = "Nothing matches that. Try a shorter word."; show.Visible = false; }
    }

    void DrawItem(object s, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= shown.Count) return; bool sel = (e.State & DrawItemState.Selected) != 0; var t = shown[e.Index];
        using (var b = new SolidBrush(sel ? Color.FromArgb(104, 48, 196) : Color.FromArgb(8, 4, 16))) e.Graphics.FillRectangle(b, e.Bounds);
        bool first = e.Index == 0 || shown[e.Index - 1].Category != t.Category;
        TextRenderer.DrawText(e.Graphics, t.Title, Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height), Color.FromArgb(240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (t.HasTour) TextRenderer.DrawText(e.Graphics, "▶", new Font("Segoe UI", 8f), new Rectangle(e.Bounds.Right - 20, e.Bounds.Y, 18, e.Bounds.Height), Color.FromArgb(236, 120, 230), TextFormatFlags.VerticalCenter);
        if (first) using (var pen = new Pen(Color.FromArgb(120, 190, 120, 255))) e.Graphics.DrawLine(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Right, e.Bounds.Y);
    }

    void ShowTopic()
    {
        var t = Current(); if (t == null) return; view.Clear();
        Add(t.Category.ToUpperInvariant(), 9, true, Color.FromArgb(236, 120, 230)); Add(t.Title, 20, true, Color.FromArgb(225, 190, 255)); Add("", 6, false, Color.White);
        int n = 0;
        foreach (var raw in t.Body.Replace("\r\n", "\n").Split('\n'))
        {
            string l = raw.TrimEnd(); if (l.Trim().Length == 0) continue;
            if (l.StartsWith("## ")) { n = 0; Add("", 4, false, Color.White); Add(l.Substring(3), 13, true, Color.FromArgb(190, 140, 255)); }
            else if (l.Length > 2 && char.IsDigit(l[0]) && l[1] == '.') { n++; Add("  " + l, 11, false, Color.FromArgb(240, 232, 255)); }
            else if (l.StartsWith("- ")) Add("  • " + l.Substring(2), 11, false, Color.FromArgb(240, 232, 255));
            else if (l.StartsWith("TIP:")) Add("  Tip: " + l.Substring(4).Trim(), 11, false, Color.FromArgb(120, 230, 160));
            else if (l.StartsWith("WARN:")) Add("  Careful: " + l.Substring(5).Trim(), 11, false, Color.FromArgb(255, 190, 90));
            else Add(l, 11, false, Color.FromArgb(215, 205, 235));
        }
        view.SelectionStart = 0; view.ScrollToCaret(); show.Visible = t.HasTour && canTour(t.Id);
    }

    void Add(string text, float size, bool bold, Color c)
    {
        view.SelectionStart = view.TextLength; view.SelectionLength = 0; view.SelectionFont = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); view.SelectionColor = c; view.AppendText(text + "\n");
    }
}

// ----------------------------------------------------------------------
class TourStep { public Func<Rectangle> Target; public string Title, Text; public TourStep(Func<Rectangle> t, string title, string text) { Target = t; Title = title; Text = text; } }

// Dims the owner window with a glowing hole around the control being explained (you can click through the hole), plus a Next / Back card.
class Tour
{
    readonly Form owner; readonly List<TourStep> steps; int idx; Form shade, card; System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer { Interval = 33 }; float t; Label title, text, count; Button back, next;
    Rectangle holeScreen;

    public static void Run(Form owner, List<TourStep> steps) { if (steps.Count == 0) return; new Tour(owner, steps).Start(); }
    Tour(Form owner, List<TourStep> steps) { this.owner = owner; this.steps = steps; }

    static GraphicsPath Round(Rectangle r, int rad) { var p = new GraphicsPath(); int d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }

    void Start()
    {
        shade = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, BackColor = Color.Black, Opacity = 0.66, Owner = owner };
        typeof(Form).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(shade, true, null);
        shade.Paint += (s, e) =>
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var h = new Rectangle(holeScreen.X - shade.Left, holeScreen.Y - shade.Top, holeScreen.Width, holeScreen.Height);
            float pulse = (float)(Math.Sin(t * 4) * 0.5 + 0.5);
            using (var p = Round(Rectangle.Inflate(h, 6, 6), 12))
            {
                using (var pen = new Pen(Color.FromArgb((int)(60 + 90 * pulse), 236, 92, 226), 14f)) g.DrawPath(pen, p);
                using (var pen = new Pen(Color.FromArgb(255, 245, 190, 255), 2.5f)) g.DrawPath(pen, p);
            }
        };
        card = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, BackColor = Color.FromArgb(18, 8, 38), Size = new Size(380, 190), Owner = owner, KeyPreview = true };
        card.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(220, 190, 110, 255), 2)) e.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3); };
        title = new Label { Left = 16, Top = 12, Width = 348, Height = 28, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(236, 150, 235), BackColor = card.BackColor };
        text = new Label { Left = 16, Top = 44, Width = 348, Height = 80, Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(240, 232, 255), BackColor = card.BackColor };
        count = new Label { Left = 16, Top = 160, Width = 80, Height = 20, ForeColor = Color.FromArgb(160, 140, 200), BackColor = card.BackColor };
        back = Mk("Back", 130, 148, 70, () => Go(-1)); next = Mk("Next", 206, 148, 90, () => Go(1)); var skip = Mk("Close", 302, 148, 62, () => End());
        card.Controls.AddRange(new Control[] { title, text, count });
        card.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) End(); else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) Go(1); else if (e.KeyCode == Keys.Left) Go(-1); };
        owner.LocationChanged += Follow; owner.SizeChanged += Follow; owner.FormClosed += (s, e) => End();
        tm.Tick += (s, e) => { t += 0.033f; shade.Invalidate(); }; tm.Start();
        Show();
    }

    Button Mk(string txt, int x, int y, int w, Action a)
    {
        var b = new Button { Text = txt, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); b.Click += (s, e) => a(); card.Controls.Add(b); return b;
    }

    void Follow(object s, EventArgs e) { Layout(); }
    void Go(int d) { idx += d; if (idx < 0) idx = 0; if (idx >= steps.Count) { End(); return; } Layout(); }

    void Show() { Layout(); shade.Show(owner); card.Show(owner); card.Focus(); }

    void Layout()
    {
        if (shade == null || shade.IsDisposed) return; var s = steps[idx];
        var cr = owner.RectangleToScreen(owner.ClientRectangle); shade.Bounds = cr;
        var tr = s.Target(); holeScreen = owner.RectangleToScreen(tr);
        var full = new Region(new Rectangle(0, 0, cr.Width, cr.Height)); using (var p = Round(new Rectangle(holeScreen.X - cr.X - 2, holeScreen.Y - cr.Y - 2, holeScreen.Width + 4, holeScreen.Height + 4), 10)) full.Exclude(p); shade.Region = full;
        title.Text = s.Title; text.Text = s.Text; count.Text = (idx + 1) + " of " + steps.Count; back.Enabled = idx > 0; next.Text = idx == steps.Count - 1 ? "Done" : "Next";
        // put the card below the highlighted control if it fits, otherwise above
        var scr = Screen.FromRectangle(cr).WorkingArea; int cx = Math.Max(scr.Left + 8, Math.Min(holeScreen.Left, scr.Right - card.Width - 8)), cy = holeScreen.Bottom + 16;
        if (cy + card.Height > scr.Bottom - 8) cy = holeScreen.Top - card.Height - 16; if (cy < scr.Top + 8) cy = Math.Max(scr.Top + 8, holeScreen.Bottom - card.Height - 8);
        card.Location = new Point(cx, cy); shade.Invalidate();
    }

    void End()
    {
        try { tm.Stop(); owner.LocationChanged -= Follow; owner.SizeChanged -= Follow; if (shade != null) shade.Close(); if (card != null) card.Close(); } catch { }
        shade = null; card = null;
    }
}
