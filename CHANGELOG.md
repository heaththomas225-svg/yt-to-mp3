# Changelog

What changed in each version of **Purphex**. The same notes are shown in the app (Menu > What's new) and in the update card.

## 3.72.0  The piano roll with ghost notes, the Purphex Sampler, GENERATE PATTERN, and a faster studio

- NEW: The piano roll (PIANO ROLL at the top, or a track's Plugins / PIANO button): a tab of the bottom panel for every track that plays notes. Pick a track with < and > or the track button and edit its own notes without closing it. Click the grid to add a note (it follows the mouse until you let go), drag a note to move it, drag its right edge to resize it, right-click or double-click it to delete it, Ctrl+drag for a box selection, Shift+drag to copy. Keys: Delete, Ctrl+A, Ctrl+C, Ctrl+V (at the song's position), Ctrl+D (duplicate after itself), Ctrl+Q (quantise), the arrows (by step or semitone, Shift for an octave), Ctrl+Z and Ctrl+Y. SNAP from 1 bar to 1/32 with triplets, LENGTH for new notes, a velocity lane under the grid (drag a bar: the note does not move), SCALE to light up a key and scale with LOCK to keep notes on it, CHORDS to add a whole chord with one click, LABELS, PREVIEW (hear notes as you add them, on the track's own sound), zoom (Ctrl+wheel for time, Alt+wheel for rows), the playhead follows the song, and the ruler moves the song. It edits the track's real notes, so the studio's engine plays them with the track's instrument, plugins, bus, mute and solo.

- NEW: Ghost notes: while you edit one track, every other note track shows behind it as faint, see-through notes (drum tracks as timing columns), so you can write the snare around the kick and the hats around both. Ghost notes can never be clicked, moved or deleted, and GHOSTS turns them off or picks which tracks show.

- NEW: The Purphex Sampler: a track that plays real samples from notes. One sample (a kick, a snare, an 808...) or a whole kit with a sample on each key (the keys show the samples' names). A drum sample keeps its pitch on any key; an 808, bass or melodic sample follows the notes from its root note (an octave up plays it an octave up). + ADD TRACK > Drum Track makes one; in SOUNDS right-click a sound > Load into a new sampler track; drag a sound onto the piano roll to put it on a key; SAMPLES loads files, sets fixed pitch or follow, the root note and one-shot. An audio track stays real audio, and LOAD IT INTO A SAMPLER makes a sampler track from its sound. Samples are saved with the project.

- NEW: GENERATE PATTERN (in the piano roll): a starting beat in a style (Trap, Hip-Hop, Lo-Fi, UK Drill, Drum & Bass, House, Afrobeats, Reggaeton, UK Garage, Phonk), 4, 8 or 16 bars, written as real notes onto your tracks by their job: kick, snare or clap, hi-hats (with rolls and loud and soft hits), open hat, percussion and 808 (it follows the kick in the piano roll's key). Each track's job is worked out from its sound or name, or set in JOBS. It works with what you have (only a kick and an 808 still get their parts, and it says what would make it fuller), asks before changing the tempo, and is one undo step.

- FIXED: The studio is quicker: the sound library (45,000 sounds) now loads in the background a moment after the studio opens, so opening SOUNDS no longer freezes it for 3 seconds; the Rainbow theme no longer rebuilds the whole background every time its colours move (54 ms a frame with 150 ms hitches, now 29 ms like before); the packs page finds each pack's sounds through an index instead of searching 140,000 every time.

- CHANGED: The scroll bar of the arrangement (the long bar with the logo) now sits above the timeline ruler, at the top of the tracks: its permanent place. The tracks get the space it used at the bottom.

- CHANGED: The top row (ADD MUSIC, SPLIT VOCALS + BEAT, AUTO MIX, DETECT TRACK KEY, UNDO MIX, THIN TRACKS) sits in the middle of the bar; the side menu button stays on the left.

- CHANGED: Two notes on the same key never overlap: a note ends where the next note on its key starts (MIDI cannot tell overlapping notes on one key apart). The old piano roll window is replaced by the new piano roll.

- FIXED: When the tracks run off the bottom of the screen, the pinned + ADD TRACK button keeps its own space under the rows and never covers a track's buttons.

## 3.71.0  The real sound library: installed packs are real, checked audio files, SOUND PACKS first, buses on the mixer

- NEW: Installing a sound pack now makes its sounds as real audio files on this PC and checks every one before it is listed: long enough, not silent, not clipped, not a broken file, exactly its bars long at its tempo (loops), no click at the start, end or loop point, and not a copy of a sound already in the library (an exact copy, or one that is 99.3% alike, measured from the sound itself). A sound that fails is left out, with the reason, in the pack's failed list. Files are 16-bit WAV at 44.1 kHz, mono when both sides are the same. Each sound keeps its tempo, bars, key (only tonal sounds have one), length, channels, peak and loudness, licence, source, how it was made, when, and a fingerprint. An install that is stopped carries on from where it was next time; installing a pack again repairs missing files.

- NEW: Each pack has more kinds of sound, kept apart: drum loops, kick loops, snare loops, hi-hat loops, percussion loops and fills; bass loops and 808 loops (with sliding 808 phrases for drill and trap); 808 one-shots with twelve different characters (clean sub, long, short punchy, distorted, saturated, gritty, slide down, slide up, kick 808, pluck, tonal, harmonic), all tuned to the pack's key instead of one 808 at twelve pitches; long and short bass one-shots; drum hits whose names say what they are (Punch, Tight, Deep, Hard, Thump, Clean; Crack, Snap, Fat, Rim Crack...), and a second kit tuned differently; instrument notes and chord hits as different chords; FX, vocal chops, MIDI and Bass Lab presets.

- NEW: The library report (Check library writes sounds\library-report.txt): how many checked sounds there are by section, genre, kind, type and pack, the size of the library, every sound that was left out and why, and that nothing is in the library twice.

- CHANGED: SOUND PACKS opens on the packs (install, preview, browse); SOUNDS is the tab next to it for searching. The SOUNDS list shows your installed sounds (real files) by default; Show: made on play (or all) adds the sounds that are made the first time you play them. Every filter only offers what is there, with real counts that take the other filters into account (genre, style, instrument, type, key, tempo, pack), and More adds length, bars, time signature, mono or stereo, sample rate, family, character, source and licence, and recently added. A key filter never hides drums and FX (they have no key) unless you ask. Empty results say why and how to widen them. Each pack says what it really holds.

- CHANGED: Buses and effects are in one place, the mixer (ALL PLUGINS): each track strip has + BUS (put it in a bus, make a new one, or take it out), each bus has its own strip with BUS OPTIONS (effects, presets, rename, colour, delete), and + NEW BUS sits at the end. The separate BUSES tab and the MASTER button are gone. Effects are added in the mixer only: the track header's button is now Plugins (it opens the mixer on that track) and + ADD EFFECT is gone from the right panel. REMOVE ALL PLUGINS is at the top of the mixer (it asks first; Ctrl+Z brings them back), and ALL PLUGINS OF EVERY TRACK and Remove all plugins (go raw) are gone from the presets menu.

- CHANGED: Only plugin windows and VST windows always wear the Purphex design; everything else follows your theme (the plugin slots on the mixer too). The live picture at the bottom of a plugin window is 50% bigger.

- FIXED: Changing the theme now changes the whole background as well (it kept the old theme's colours until the window was resized). Menus always open inside the Purphex window, never on another monitor. Drum2MIDI hears a kick that starts on the ringing tail of the one before. A sound preview never plays out of the speakers during the automatic tests.

## 3.70.0  The mixer panel, floating plugin windows, sends, and your profile colour across the studio

- NEW: The mixer panel at the bottom of the Mix page (MIXER on the left, or ALL PLUGINS): CHANNEL is the picked track's channel strip (fader, level meter with clip light, pan, input trim, phase, mono or stereo, input and output, sends, automation and its plugins in order), PLUGINS is its chain from left to right (open, bypass, move, copy, remove, + ADD EFFECT, presets), BUSES makes, renames, deletes and routes buses (each with its own level, pan, mute, solo, meter and plugins, and which tracks go to it), and CLIP or MIDI edits what you picked: an audio clip (name, file, start, length, clip gain, fades, mute, lock, reverse, Flex Pitch) or a MIDI part (transpose, velocity, quantise, the instrument, the piano roll). Drag its top edge to resize it, HIDE gives the arrangement its space back, and the arrangement stays editable above it. It remembers its height and whether it is open.

- NEW: ALL PLUGINS is now a mixing desk at the bottom: every track and bus has its own strip side by side, with its plugin slots (click one to open that plugin in its own small window, the dot turns it on or off, right-click to move, copy or remove it, + ADD adds one), its sends, its output, pan, fader with a level meter, and mute and solo. While it is open the right panel shows no plugin list, so every plugin is in one place. (The master stays on the Mastering page.)

- NEW: Live plugin visualisers in the Purphex look: every plugin slot on the mixer and every plugin window shows a little moving picture of how much that plugin is doing to the sound while the song plays, measured from the real audio: ripples for reverbs, fading repeats for delays, a pumping gain-reduction meter (with the dB) for compressors and limiters, a swelling curve for EQs and filters, a crunchy stepped wave for distortion and bit crushers, a bending wave for pitch and voice plugins, and level bars for the rest. Bypassed plugins sit still. The slots are violet-to-magenta glass bars whose neon edge lights up with the plugin's work, and the plugin windows have a Purphex title bar with the logo and the live picture in a strip along the bottom of the window.

- CHANGED: Plugin windows are Purphex all over: the whole window (background, frame, buttons, sliders and writing) is drawn in the Purphex violet, magenta and cyan whatever theme you use, and so are the Purphex VST windows; other makers' VST windows get a Purphex bar on top (their own interface stays as they made it). The rest of the studio keeps your theme.

- CHANGED: Bass Lab is at the very top of + ADD EFFECT (the first of the VSTs: it makes all your sounds) and the separate BASS LAB button is gone. A new MIDI track no longer opens Bass Lab: it is an empty MIDI track and MIDI REC is on, so you can play your own MIDI keyboard (a piano, a guitar with MIDI...): it is heard straight away and recorded onto the track, and the notes play back with the new built-in keys (a warm piano voice with velocity and the sustain pedal). Bass Lab and the VSTs are extras: add one from + ADD EFFECT and it plays the notes instead.

- NEW: Floating plugin windows: every effect opens in its own small window over the studio (from the mixer panel, or double-click a plugin on the right). Windows stay open while you click the timeline, the tracks, the transport or the mixer, so you can play the song and turn a knob at the same time and hear it straight away. Several can be open at once, each tied to its own plugin (picking another track never changes an open window). Drag a window by its title bar, resize it from the corner, a click brings it to the front, ARRANGE WINDOWS tidies them. Each has ON/BYPASSED, previous and next preset, the preset list, RESET and close; closing a window keeps the plugin working, and removing a plugin closes its window.

- NEW: Sends: any track can send a copy of its sound to one or more buses at its own level, before the fader (PRE) or after it (POST), as well as its own output, for example to share one reverb. Buses never send, so there can be no feedback loops. Input TRIM (the level into the plugins) and PHASE (flip the polarity) are new on every track. All of it is saved with the project and undoable; projects from older versions open exactly as before.

- NEW: Your profile colour is now the accent of the whole studio: selected buttons and tabs, chips, the menus' chosen item, focus edges, sliders, the picked plugin, the selection outline and the front plugin window all take it, with its finish: metallic and chrome draw real bands of light, holographic and pearl a colour-shifting sheen (it only moves on window title bars, and never in Performance mode or with Windows animations off), chameleon blends into its second colour, candy, glitter and matte too, and translucent makes the selected parts see-through. A theme change keeps your accent; switching the profile back to the theme colour puts the theme's accent back. Text stays readable on every colour.

- CHANGED: Plugins have one home: opening the mixer panel tucks the right-hand plugin panel away. The right panel's tools are one per row (ALL PLUGINS, + ADD EFFECT, PRESETS, BASS LAB, SOUND PACKS) and ALL BUSES is gone from it (buses are in the mixer panel; Edit buses on a track's bus menu opens them there). Deleting a bus also removes the sends to it.

- CHANGED: Menus are 25% smaller (10 pt, compact rows) so they take less room, with each option in its own neon box. The editing row (BPM to PERFORMANCE and the zoom) is centred under the transport.

- FIXED: Undo now also brings back MIDI notes (it used to skip them).

## 3.69.0  The complete sound library: 125,000+ sounds in every category, real MIDI, new instruments, any key and tempo

- NEW: Every pack now has its whole range in the browser straight away, without installing it: drum, top, bass, chord, melody, arp and vocal loops; kicks, snares, claps, hats, percussion, cymbals and 808s; bass, instrument-note and chord-hit one-shots; FX; vocal shouts; and real MIDI files (drum patterns, basslines, chord progressions and melodies). That is 125,416 sounds: 77,992 loops, 26,736 one-shots and 20,688 MIDI files, every one its own recipe (none is a copy of another). Every genre has every category. A sound is made on your PC the first time you play or drag it (a fraction of a second) and kept, so it is there at once after that.

- NEW: New instruments, all synthesised by Purphex (nothing is anyone else's recording): Grand Piano, Electric Piano, Strings, Brass, Acoustic and Nylon Guitar (with strummed chords), Marimba, Kalimba, Bells, Log Drum (for amapiano) and Flute, next to the Bass Lab keys, leads, plucks and pads. Each pack plays the instruments that suit its genre (trap: piano, bells, strings, flute; amapiano: log drum, keys, marimba; lo-fi: electric piano, nylon guitar...). They are in tune, clean and end at silence.

- NEW: Make in: pick a key and a tempo (or your project's tempo) and every instant loop, bass, note, chord and MIDI file in the library is made in that key and tempo, with exact bar lengths. Drum hits and FX stay as they are. Pick F# minor at 128 BPM and there are 6,912 chord loops ready in it.

- NEW: An Instrument filter (every instrument under its family, with how many sounds it has). The filters are in two rows: Genre, Style, Instrument, Type, then Key, BPM, Make in and Toolkit.

- NEW: MIDI is heard in the preview, played through the pack's kit, bass or keys. Dropped where there is no instrument track, a bassline, chords or a melody get a new Bass Lab track with a sound that suits them, and drum MIDI becomes a drum track played with the pack's kit. Dropped below the last track it always makes a new track.

- NEW: Check library (in SOUND PACKS) opens every file the library lists and says what is missing or damaged; Repair makes those packs again. Damaged instant sounds are removed so they are simply made again. It also writes soundslibrary-manifest.json: the whole taxonomy with real counts.

- NEW: Import a whole folder of your samples (and its sub-folders): tempo and key are read from the file names, and each sound gets an instrument label.

- NEW: SOUND PACKS opens inside the sounds panel on the right (no window over the studio): choose, install, browse and check packs there, with < Sounds to go back. A Fullscreen button spreads the sounds browser or the packs over the whole Mix page; Exit full puts it back.

- FIXED: The Clav chord loops in installed packs asked for a Bass Lab preset that did not exist and played the plain default sound; that slot is now a proper FM bell sound.

- IMPROVED: Searching the library is fast even with 125,000+ sounds, and it uses far less memory.

- CHANGED: Installed packs offer an update (sound version 6).

## 3.68.1  Sounds play clean in your song: no more squashed, distorted master

- FIXED: The real reason sounds came out crackly and distorted once they were in a song: the default master squashed everything. A new song had the master compressor on at -14 dB and the limiter pushing +3 dB, so every sample and sound-pack loop was limited and distorted the moment it played (measured: a drum loop came out with its peaks held down and changed by up to 1 part in 9). A new song now plays clean: the compressor is off and the limiter is only a safety net at -0.3 dB with no gain push. A pack loop on the timeline now plays exactly as its file. Songs saved with the old untouched default open clean too; a master you set yourself, or a mastering preset, is left as it is. Mastering and AUTO MASTERING still make a song loud when you ask them to.

- FIXED: The sound browser preview always uses the biggest audio buffer, so a busy screen can no longer make the sound you are previewing stutter or crackle (with the low-latency setting it used a tiny buffer).

- IMPROVED: Clearer drums and basses: the noise in snares, claps, rims, congas, kick and 808 clicks and impacts no longer fizzes right up to the top of the spectrum, hats lose their harshest top end, and no pack bass goes through a distortion or saturation unit any more (growls come from the filter movement alone).

- CHANGED: Installed packs offer an update (sound version 5) so their sounds are remade with these changes. Instant loops you already played are remade too.

## 3.68.0  Clean sounds, 50,000+ instant drum and bass loops, every genre labelled properly

- FIXED: The crackle in the sound packs is gone. The real cause was found by measuring every sound: drum hits (kicks, snares, toms, cymbals, percussion) and 808s were cut off while they were still ringing, which clicked on every beat of every drum loop. Every hit now rings out to silence. Drum loops are exactly their length, with the last hits wrapping round to the start, so they repeat with no click and no gap.

- FIXED: No distortion: snares, claps, impacts and voices are no longer driven into saturation, loop mixes are no longer squashed by a saturating glue, the limiter that levels loops is gentler and slower (no gain wobble), the clean basses (sub, reese, organ, pluck, stab, rubber, wobble) have no distortion at all, and the kick and 808 clicks are softer. Plucked guitar notes start softer and end at silence.

- NEW: The instant library: every genre and style now has hundreds of drum loops and bass loops straight away, without installing anything (50,000+ in all: 120 drum loops and 120 bass loops for every pack, and twice that for a genre with one pack). Filter Trap drum loops and you get 1,800 of them; House bass loops in F minor, hundreds. Each loop is made the first time you play or drag it (a fraction of a second) and kept, so it is there at once next time. Every loop is its own groove: the kicks, hats, rolls, ghost notes, percussion and fills change, and bar two answers bar one.

- IMPROVED: Every pack is labelled with its real genre and style: Tech House, Bass House and Electro House are House; Dubstep, Riddim and Tearout are Dubstep; Trap, UK Drill, Grime, Boom Bap and R&B are their own genres instead of Rap and Vocals; Hyperpop sits in EDM. The sections are Drum & Bass, UK Bassline, UK Garage, Bass Music, Hip-Hop & Rap, House, Techno & Trance, Afro & Latin, Pop & R&B, Lo-Fi, EDM and Phonk. Installed packs show the new labels too.

- IMPROVED: The filters: Genre lists every genre under its section with how many sounds it has, Style lists the styles of the chosen genre (or every genre's styles), and every BPM range is labelled with the genres that use it. The filter buttons show what is picked.

- CHANGED: Installed packs offer an update (sound version 4) so their sounds are remade clean.

## 3.67.0  The sound library: 218 packs, 56,000+ clean sounds, Ctrl+X, pack search

- IMPROVED: No distorted or crackly sounds: every pack sound was measured for clipping, clicks and crackle. The growl, dirty, metallic, neuro and riddim basses no longer crackle (their filters are not overdriven any more and their LFO movements are smoothed), kicks and 808s are clean instead of squared-off (808 Dirty and 808 Distorted are now 808 Warm and 808 Punchy), stab chords and basses start without a click, the vinyl crackle texture is gone, and loud loops are levelled by a clean limiter instead of being clipped. A check of every sound runs with the tests so it stays that way.

- NEW: Ctrl+X cuts the audio you highlighted (or the piece you picked): it is copied, then taken off the timeline. Ctrl+V pastes it at the needle, Ctrl+Z puts it back. Pasting when there is no audio track left makes one.

- NEW: SOUND PACKS has a search box: type a genre, style, mood or tempo (drill, amapiano, dark trap, guitar, 140 bpm) and only the matching packs are listed.

- CHANGED: Sounds go onto the timeline by dragging them. A double-click only plays the sound again, so nothing lands on a track by accident.

- NEW: 121 new sound packs in 8 new sections: Hip-Hop (trap, rage, plugg, UK and NY drill, boom bap, jazz hop, grime), House (deep, tech, piano, bass, afro, melodic, disco, French, lo-fi house), Techno and Trance (dark, melodic, hard, acid, psytrance), Afro and Latin (afrobeats, amapiano, reggaeton, latin trap, moombahton, dancehall, baile funk), Pop and R&B (dance pop, synth pop, 90s R&B, neo soul, Jersey club), Lo-Fi (study beats, chillhop, vaporwave), EDM (future bass, big room, breaks) and Phonk (drift phonk, hyperpop). Each genre has its own drum groove, drum sound, bass rhythms, tempos and keys.

- NEW: Every pack is now much bigger: 260+ sounds each. 32 drum loops (full, without the kick, and kick and snare only), percussion loops, 24 bass loops and 808s, chord loops on every keys and pad sound, melody and arp loops on every lead, guitar and harp loops, 18 vocal chop loops and vocal notes, chord hits and synth one-shots, FX, MIDI and songstarter stems.

- IMPROVED: Every sound is still made on your PC by Purphex (no recordings, no one else's samples), so they are yours to use in your music. A pack takes about a minute to make; installing many at once tells you the space and time it needs first.

- IMPROVED: Loops of one kind play at an even loudness while you browse (sparse loops are lifted with a gentle limiter), and the section buttons in SOUND PACKS wrap onto two rows.

- IMPROVED: Packs you already have offer an update to the bigger version.

## 3.66.0  Type titles straight onto your video, and the Creative Suite

- FIXED: Video Studio: you can type your titles now. + ADD TEXT (or any title style) puts a title on the video with its words picked, so you just start typing on the video, like iMovie. Double-click any title on the video or on the timeline to change its words; Esc or DONE finishes; one Ctrl+Z takes the typing back.

- NEW: A title bar over the video for the picked title: font (your PC fonts and the font library), smaller / bigger and the exact size, bold, italic, capitals, left / centre / right, the text colour, an outline and its colour, and a box behind the words with its colour.

- NEW: The Text panel has a YOUR TEXT box with the picked title, and ADD TEXT at the top.

- NEW: Purphex Creative Suite: the SUITE button at the top left of the Creative Studio. Your songs, designs and videos in one list (make a video from a song, open a design or a video), shared media that moves pictures between the editors (a photo from your video into a new design, the frame under the playhead as a design, a photo upload onto your video), your edition and trial, help and settings.

## 3.65.0  Video Studio PRO

- NEW: 16 more transitions: push, slide up and down, wipe up and left, barn doors, iris, diamond, heart, clock wipe, spin, zoom through, flash, whip pan, glitch and pixelate (24 in all).

- NEW: Colour grading in COLOUR: lift, gamma and gain for red, green and blue (shadows, midtones, highlights), saturation, temperature, a curve, and 3D .cube LUTs from your camera or a colour pack, with a strength slider. COPY GRADE TO ALL CLIPS grades the whole video in one go.

- NEW: Video effects in EFFECTS: RGB split, glitch, VHS, film grain, pixelate, posterize, scan lines, letterbox (cinema bars), mirror (left to right, top to bottom, four ways) and kaleidoscope.

- NEW: Keyframe easing: smooth, linear, ease in, ease out, hold, overshoot, bounce and elastic. Play backwards: a clip plays in reverse, picture and sound.

- NEW: More text animations: zoom, slide from the right, drop and bounce, spin, flicker, glitch, wipe reveal, stretch, rise and blur. The templates that used a zoom title now really zoom.

- NEW: Editing: INSERT mode (I) pushes the rest of the track along and OVERWRITE mode (O) covers what is there; Alt + Left / Right nudges clips one frame (Shift: ten); G goes to a time you type; ? shows every shortcut.

- NEW: The media bin can be searched, sorted by name, kind or length, and filtered to what is not used yet. STICKERS has the 342 Purphex stickers too: click one and it goes on your video at the playhead.

- IMPROVED: Every effect, grade and transition is drawn the same way in the preview and in the exported video.

## 3.64.0  Photo Studio PRO

- NEW: Photo Studio is now Photo Studio PRO (side panel > Photo Studio PRO). It opens on quick starts: Single Cover, Album Cover (with a tracklist page), Artist Banner, YouTube Thumbnail, Promotional Post and Story, or start from scratch at any size with a see-through background.

- NEW: Pages: one design can hold several pages (the bar at the bottom left of the picture). Add, duplicate, rename and delete them; they are saved with the design, and EXPORT can write every page at once.

- NEW: Pro colour tools in Adjust, for one photo or the whole picture: levels, curves (all colours or red, green and blue alone), vibrance, temperature and tint, colour balance for shadows, midtones and highlights, selective colour for eight colour ranges, noise reduction and straighten.

- NEW: Layer effects on any layer: two outlines, glow, drop shadow, inner shadow, colour overlay, gradient overlay and a worn distress look. Clip to the layer below puts a photo inside text.

- NEW: Selections and masks: rectangle, ellipse and lasso selection. Keep the picked layer only inside the selection or cut it out (a mask you can feather, invert or remove later), fill it, or copy it to a new layer.

- NEW: New tools on the app bar: fill bucket (with tolerance), eyedropper, line and arrow (move, turn and resize them; plain, arrow or double arrow, dashed, curved), the fill colour, and rulers with guides that layers snap to. Keys: V, M, E, L, G, I, U, A.

- NEW: Sticker library: 342 stickers drawn by Purphex in 25 categories (music and studio, graffiti, sparkles, hearts and faces, flames and lightning, arrows, shapes, nature, weather, Y2K, cyberpunk, spooky, cute, gaming, lettering, doodles, tape and paper, light leaks, comic, equalizers, badges, abstract, seasonal, social media, patterns). Sharp at any size, in two colours you pick, with search, favourites and recently used.

- NEW: Background library: 30 drawn backgrounds (bokeh, starfield, nebula, clouds, sunset, concrete, fabric, denim, paper, marble, graffiti, brick, synthwave grid, studio spotlight, light rays, aurora, smoke, low poly, holographic, halftone, glitch, cinematic and more) in your colours, with SHUFFLE for a new arrangement.

- NEW: Font library: 143 free open-licence fonts by style (display, sans serif, serif, script, graffiti, retro and tech, gothic), each shown in its own look and downloaded the first time you use it. Your PC fonts and imported fonts are still there.

- NEW: Pro text: chrome, gold, glossy, metallic, embossed, distressed, neon, cinematic and minimal styles, warps (wave, rise, bulge, flag, a circle of text) and a second outline.

- NEW: Brush hardness and smoothing, a pencil and a soft airbrush. More sizes: YouTube banner, profile picture, X and Facebook headers, SoundCloud banner, logo, poster, 4K and phone wallpapers.

- IMPROVED: Export warns you when a format cannot be see-through (JPEG) and says which layer is left out for a see-through export.

## 3.63.5  Ctrl+C and Ctrl+V copy and paste clips

- NEW: Ctrl+C copies the clip you picked, or every highlighted section at once. Ctrl+V pastes at the needle on the picked track: several sections keep their spacing in time and between tracks (new tracks are added below if needed). One Ctrl+Z takes the whole paste away. The right-click Copy and Paste here work the same way.

## 3.63.4  See every clip you drag, and Purphex scroll bars in the Music Hub

- FIXED: Dragging several picked clips to other tracks now shows every one of them lifted, with its waveform, over the row it will land on (only the one you grabbed showed before, and without its audio).

- IMPROVED: The Music Hub pages use the Purphex scroll bar: a black rail with a violet edge, cyan arrows up, pink arrows down and the logo as the handle.

## 3.63.3  Type an exact gain

- NEW: The GAIN box has a box for the value: click it, type the gain you want (for example -8.5) and press Enter. It stays between -30 and +12 dB, and Ctrl+Z undoes it.

## 3.63.2  Ctrl + Shift no longer resizes the tracks

- FIXED: Holding Ctrl and Shift while scrolling no longer makes the tracks bigger and smaller. Alt + mouse wheel still changes the track height on purpose.

## 3.63.1  Better sounding sound packs, and help links that go to the right place

- IMPROVED: Hi-hats, cymbals, cowbells, risers, sirens and the vocal sounds no longer have harsh folded-back tones (band-limited oscillators: about 10 dB cleaner). Cymbals now shimmer like metal and are wide in stereo.

- FIXED: Plucked guitar and harp loops were out of tune (up to 14 cents flat on high notes): they are now within 1 cent.

- IMPROVED: Loops of the same kind now play at an even loudness while you browse (drum loops within about 4 dB instead of 10, melodic loops matched), and sirens no longer jump out.

- FIXED: Help: the SHOW ME links and Show me on screen no longer jump to the wrong button. Words that are part of another control (SAVE AS THEME, MY SOUNDS, STEREO WIDTH) and the controls of plugins, Bass Lab and the Video Studio no longer link to studio buttons with the same name, and every link left is checked to reach a real button.

- NEW: Packs made before this show "Older sound" in SOUND PACKS: press UPDATE on a pack, or Update all, to remake them with the better sound.

## 3.63.0  Split into 4 stems, and move notes in time in Flex Pitch

- NEW: Split into 4 stems (side menu, Tools): the AI splits a song into VOCALS, DRUMS, BASS and OTHER (everything else) on four tracks, perfectly in sync, and the four add up to the song exactly. Long songs are done in pieces like the 2-stem split, and the stems are saved next to it.

- NEW: Flex Pitch: drag a note sideways to move it in time, earlier or later. It keeps its sound, pitch and length, and the audio between it and the notes around it stretches to fit (it cannot pass them). Up and down still changes the pitch.

## 3.62.0  Purphex Drum2MIDI: drums into notes

- NEW: Purphex Drum2MIDI: pick a drum track (a beat, a loop, a recorded kit) and press TURN THE DRUMS INTO NOTES. It listens for the KICKS, SNARES and HATS and writes them onto its own track as notes you can edit (kick 36, snare 38, hat 42), from where that track starts, with how hard each was hit. QUANTIZE snaps them to 1/8, 1/16 or 1/32; a sensitivity for each lets you catch quiet hits or only the big ones. Its own kit plays them (kick, snare, clap, closed and open hat, each tunable), or move the notes to any drum instrument.

## 3.61.1  Closed accounts close their PCs

- IMPROVED: When the Purphex team closes an account, the app stops on the PCs it was linked to and says why (it used to only fall back to Free, saying the link belonged to another PC). It stays closed offline, and runs again as soon as the account is opened.

## 3.61.0  Purphex instruments: Current, Inferno, ChordPad

- NEW: Purphex Current: a wavetable synth. Two oscillators, each with eight wavetables (basic, pulse width, vowels, harmonic sweep, digital, metallic, organ drawbars, hollow) and a POSITION that morphs through the table, OCTAVE, SEMI, UNISON up to 7 voices with DETUNE and SPREAD; a SUB and NOISE; a filter (low-pass, high-pass, band-pass, notch) with its own envelope, drive and key tracking; amp and mod envelopes, an LFO and three MOD slots (route the mod envelope, LFO, velocity or note to position, cutoff, pitch, level, detune or resonance); GLIDE and MONO. High notes stay clean (no aliasing). A keyboard in the window to play it.

- NEW: Purphex Inferno: a growl and wobble bass. Saw, square, supersaw or FM with a second oscillator for a reese, a GROWL filter (low-pass, band-pass or vowel) wobbled in time with the song (1/1 to 1/16 and triplets), and HEAT distortion (tube, hard, fold, crush) with its mix. Glide, width, and a keyboard in the window.

- NEW: Purphex ChordPad: pick a KEY and SCALE (major, minor, dorian, mixolydian, harmonic minor, phrygian) and eight pads give you the chords of that key, named. One finger plays a whole chord: the white keys from middle C are the steps of the scale. CHORDS (triads, 7ths, 9ths, sus2, power), VOICING (close, open, spread, drop 2), INVERSION, OCTAVE, STRUM and a BASS NOTE; a built-in SOUND (warm pad, keys, pluck). Pick a PROGRESSION and WRITE 8 BARS TO THE TRACK puts it on the track as real notes you can edit.

- NEW: Each instrument comes with 12 factory presets.

## 3.60.0  Purphex VSTs: Reference, Pathfinder, Modular, Dreamscape, Morph

- NEW: Purphex Reference: load a finished song you like and flip between it and your mix at the same place in the song. LEVEL MATCH plays them equally loud (so louder never just sounds better); the spectrums of both side by side; loudness in LUFS and width of each; MONO checks both in mono; FOCUS listens to the lows, mids or highs.

- NEW: Purphex Pathfinder: a reverse delay in time with the song (1/16 to a bar). Echoes come back backwards, forwards, or backwards bouncing left and right, with PITCH (an octave up or down), FEEDBACK, high and low cut, DIFFUSION, WIDTH and DUCK (out of the way while you sing).

- NEW: Purphex Modular: four slots in a chain, each one of eleven modules (drive, fold, crush, rectify, low-pass, high-pass, band-pass, comb, ring, tremolo, downsample) with its own AMOUNT, moved by the LFO or by an envelope that follows the loudness.

- NEW: Purphex Dreamscape: a huge ambient space. SIZE, DECAY up to 30 seconds, DAMP, SHIMMER (an octave, a fifth, an octave and a fifth up, or an octave down fed back in), BLOOM (it swells in at each phrase), DRIFT, FREEZE (holds the sound forever), low and high cut, WIDTH.

- NEW: Purphex Morph: blends this track with any other track of the project by their spectrums: MORPH crossfades what they sound like, VOCODER puts the shape of a voice on a synth or pad, GATE lets through only what is also in the other track. SOURCE picks the track (the vocal track to start); FORMANT, SMOOTHING, ATTACK and RELEASE.

- NEW: Plugins that listen to another track can now use any track of the project as their sidechain, not only the vocal track.

## 3.59.1  ALL BUSES button

- NEW: An ALL BUSES button next to ALL PLUGINS in the plugin panel: one click opens every bus with its level, mute, the tracks that go into it and its plugins.

## 3.59.0  Text the PicsArt way, keyframes, and audio tools in the video editor

- NEW: Photo Studio: text works like PicsArt. Tap TEXT and a typing box opens at once; press the tick and your words go in the middle of the picture, picked. Then simple tabs: FONT (every font shown with your own words, plus bold, italic, capitals and alignment), COLOUR, STROKE, SHADOW, HIGHLIGHT, BEND, SPACING, OPACITY, EFFECTS (neon, 3D, glitch) and STYLES, each a row of choices and one slider, seen live.

- NEW: Photo Studio: every picked layer has a red X on the picture to delete it, a pencil to edit the words, and one corner handle that resizes and turns it in one drag (it clicks to straight angles; Alt turns freely).

- NEW: Video Studio: KEYFRAMES for position, size, rotation, opacity and volume: put the playhead where you want, set the value and press the diamond; it changes smoothly in between, in the preview and in the export. Diamonds show on the clips in the timeline.

- NEW: Video Studio: AUDIO TOOLS for every clip with sound: an EQ (bass, mid, treble), a compressor, and ducking (mark a voice-over and the music goes down by the amount you choose while it plays). Heard live, and the export sounds the same.

- NEW: Video Studio: RIPPLE DELETE (Shift + Delete) deletes a clip and moves the clips after it up; CLOSE GAPS removes the empty space on every track.

- CHANGED: The space around the video preview and the photo canvas is in your theme's colours (a soft glow with an accent edge), not flat black.

## 3.58.0  Pro layouts for the photo and video editors

- NEW: Video Studio has a professional layout: a bar of categories across the top (Media, Audio, Text, Captions, Stickers, Effects, Filters, Transitions, Speed, Colour, Animation, Overlays, Templates, Settings, Edit clip), the picked one docked on the left (the media bin to start), the big preview beside it and the timeline under. Audio, Speed, Colour, Animation, Effects and Transitions show just the controls for the selected clip.

- NEW: Photo Studio has a professional layout: an app bar on top (name, save state, undo, redo, zoom, size, EXPORT), a tool strip on the left whose options open beside it, the canvas, and a Properties / Layers panel with a live histogram on the right (the arrow tucks it away). A charcoal look like professional editors, in your theme's accent colour.

- NEW: Simple starts: the video editor opens with IMPORT VIDEO OR PHOTOS (or your song, your projects, a blank project, and your recent projects); the photo editor with IMPORT A PHOTO, START BLANK, BROWSE TEMPLATES or OPEN A DESIGN.

- NEW: Photo Studio: pick several layers (Shift + click, or drag a box), line them up, space them evenly, group them (Ctrl+G), copy and paste (Ctrl+C / Ctrl+V), pick everything (Ctrl+A); layers snap to each other and to a grid with guide lines; DESIGN has 16 sizes, your own size, see-through background, grid and safe margins.

- NEW: Photo Studio: UPLOADS keeps every picture you add (several at once, or drop them on the editor); add your own font files in FONTS; save text styles; a BRAND KIT for your colours, fonts and logos; hold to see the picture before its adjustments; rounded rectangles, lines, pentagons and octagons.

- NEW: Photo Studio: EXPORT as PNG, JPEG or WebP at half, full or double size, with quality, a see-through background and your own file name. AI TOOLS has what really runs here (remove background, auto enhance) and says honestly what would need an outside AI service.

- NEW: Photo Studio: over 2,000 templates now (logos and wallpapers, and Grunge, Cinematic, Logo emblem, Abstract and Gaming neon styles), with favourites and recently used.

- NEW: Video Studio: stickers (the sticker pack and emoji) go on your video as clips; ten templates build real, editable timelines (photo slideshow cut to the beat, promo advert, intro and outro cards, gaming montage, vertical social clip, music video, lyric video, visualiser, artist promotion); speed buttons from 0.25x to 4x; frame rate and your own size in SETTINGS.

## 3.57.0  Easier Photo Studio, and CapCut-style video tools

- NEW: Photo Studio: click anything on the picture and its tools appear right on it: EDIT TEXT, FONT, COLOUR, REPLACE, CROP, CUTOUT, EFFECTS, DUPLICATE, FRONT, BACK, LOCK and DELETE. Every word, sticker, shape and photo of a template can be removed or changed in one click (UNDO brings it back). Backgrounds, frames and textures picked in LAYERS get the same toolbar.

- NEW: Photo Studio: double-click text to type straight on the picture (double-click a photo to replace it), right-click anything for a menu, an outline shows what you are pointing at, and the tools are big tiles with pictures.

- NEW: Video Studio: FIND BEATS hears your song, works out its tempo and marks every beat on the timeline (clips snap to them). AUTO CUT TO BEAT lays your clips and photos out so every cut lands on a beat (1, 2, 4 or 8 beats each, and can repeat them to fill the song).

- NEW: Video Studio: MOTION for clips, text and visualisers: zoom in or out, Ken Burns, pop in, spin in, slide in, bounce, shake, swing, float, and pulse, shake, flash or zoom punch on every beat.

- NEW: Video Studio: GREEN SCREEN takes out any colour (tolerance, soft edge, and the green spill on the edges), BLUR FILL puts a blurred copy behind a clip that does not fill the frame, and FREEZE makes a still of the frame under the playhead.

## 3.56.0  PicsArt-style Photo Studio, and visualisers that move to the beat

- NEW: Video editor: MY PROJECTS (next to MY SONG, in the song wizard and on the welcome screen) lists your Purphex projects with their pictures; pick one and its song (mixed and mastered, like EXPORT) and its picture come in. Your project is not changed, and IMPORT still takes any file.

- NEW: Photo Studio, CUTOUT tab: REMOVE BACKGROUND (best on fairly plain backgrounds; tolerance and soft edge), a MAGIC ERASER (click a colour and that patch goes) and ERASE / RESTORE brushes. All of it stays editable and can be undone.

- NEW: Photo Studio, EFFECTS tab: glitch, RGB split, pixel art, halftone, pencil sketch, cartoon, posterize, emboss, film noise, VHS, comic, crisp HD, retro game and dreamy in one click, each with its own slider, on one photo or the whole picture.

- NEW: Photo Studio, ADJUST: exposure, highlights, shadows, tint, clarity and sharpen, as well as brightness, contrast, saturation, hue, warmth, fade, grain, vignette and blur.

- NEW: Photo Studio, COLLAGE tab: 14 layouts; click a box to put a photo in it, change the gap and the corners, and zoom or move each photo inside its box.

- NEW: Photo Studio, a sticker pack of 35 drawn stickers with their own colours and words: labels, OUT NOW, banners, LIVE, explicit tag, starburst, speech and thought bubbles, tape, sparkles, scribbles, hearts, vinyl, cassette, headphones, mic, waveform, EQ bars, a music player, play button, barcode, ticket, price tag, pin, verified tick, notification, likes, lightning and more.

- NEW: Photo Studio, canvas tools: CROP with Free, 1:1, 4:5, 9:16, 16:9 and 3:2; line layers up (left, centre, right, top, middle, bottom); a layer snaps to the middle with a guide line as you drag (Alt moves freely); zoom with - / + / FIT or Ctrl + wheel; drag layers in LAYERS to change their order.

- NEW: MOTION for the visualiser (the full page and the light show on the Mastering page): CALM, SMOOTH, LIVELY or WILD, saved. SMOOTH is the new default; WILD is how it moved before. CALM has soft, slow movement, no shaking and a gentle pulse on the beat.

- FIXED: The visualiser no longer goes mental: the bars glide up and ease down instead of jumping every frame, and the BANG (the flash, the rings, the logo jump) comes once per beat: on the song's own beat when Purphex knows its tempo, otherwise on the kick drum, never on hi-hats or snares in between. The shaking, the four blasts, the sparks, the spin and the star speed follow MOTION too, and so do the waves in the scroll bars.

- NEW: Video editor: visualiser clips have a SMOOTH setting (0.6 to start): the bars and shapes flow with the music instead of flickering (measured: about a quarter of the frame-to-frame jitter), and the waveform styles are smoothed. The export looks exactly like the preview.

## 3.55.0  Welcome screens, 1,344 photo templates, Distribution in the Music Hub

- NEW: WELCOME TO PURPHEX MUSIC PHOTO EDITOR: the first time Photo Studio opens it shows 1,344 templates to start from (album covers, stories and TikToks, Instagram posts, portrait posts, YouTube thumbnails, flyers and banners, in 16 styles such as neon ring, retro sun, glitch, halftone, film grain, polaroid and sticker party, each in its own colours). Search them, filter by size and style, or START BLANK, OPEN PHOTO or open MY DESIGNS. Every layer of a template stays editable, and the text always fits. TEMPLATES > BROWSE ALL opens it again.

- NEW: WELCOME TO PURPHEX MUSIC VIDEO EDITOR: the first time the video editor opens it offers MAKE A VIDEO FROM MY SONG, lyric video, visualiser video, cover art video, promo teaser, vertical video, music video and blank project, with your saved projects, every size and how it works.

- NEW: Music Hub: a DISTRIBUTION section under Socials: DistroKid, TuneCore, CD Baby, UnitedMasters, Amuse, Ditto Music, RouteNote, LANDR, Symphonic, AWAL, ONErpm, iMusician, Too Lost, Soundrop and FreshTunes, plus Spotify for Artists, Apple Music for Artists, SoundCloud for Artists and YouTube Studio, each on its own official site inside Purphex, so you can release your exports without leaving Purphex. The DISTRIBUTION button on the left jumps straight there.

- FIXED: The arrow that hides the track list sat on top of the tracks (over their X button and meters). It is now a HIDE TRACKS button in the empty strip above the track list (a small >> when the list is tucked away), so it never covers a track.

- FIXED: Music Hub: the YOUR LIBRARIES text no longer runs out of the bottom of the left column.

## 3.54.0  Photo Studio, Socials in the Music Hub, a folding track list

- NEW: Photo Studio replaces AI Cover Studio (the AI images needed an API key, so they are gone). A photo editor for covers, posts, stories, thumbnails and flyers: TEMPLATES with the right sizes, BACKGROUNDS (colours, 20 gradients, patterns, textures, or a photo), TEXT (14 styles: neon, outline, 3D, glitch, gradient, curved, retro, boxed, sticker and more, plus every font on your PC shown in its own style), STICKERS (over 170 emoji and 15 shapes), PHOTOS with shape masks, crop, border and shadow, FILTERS (20 looks for the whole picture or one photo), ADJUST (brightness, contrast, saturation, hue, warmth, fade, grain, vignette, blur), DRAW (a brush with neon and an eraser) and FRAMES. Move, resize, rotate and flip on the canvas, 15 blend modes, layers you can hide, lock and reorder, undo and redo. Designs from the old cover studio still open.

- NEW: The track list on the left folds away like the plugin panel: press the arrow on its edge for more room to edit. A thin strip keeps each track's colour, number and level (click it to pick the track), the arrow brings the list back, and it is remembered.

- NEW: THIN TRACKS is a saved setting: switch it on once and the tracks open thin every time.

- CHANGED: The menu button at the top left is four glowing tiles (they spread out when you point at it) and turns into an X while the menu is open, instead of three lines.

- NEW: Photo Studio: OPEN PHOTO starts a design from any picture of yours (the canvas takes the photo's own size), and PURPHEX VIDEO EDITOR jumps to the video editor.

- NEW: Music Hub: a SOCIALS section under the music services: Instagram, TikTok, Facebook, Snapchat, X, Threads, Reddit, Discord, Twitch, Pinterest, LinkedIn, WhatsApp, Messenger, Telegram, Bluesky and Tumblr, each on its own official site inside Purphex (sign in on the site's own page; Purphex never sees a password).

- FIXED: Music Hub: SIGNED IN is remembered and checked as soon as the hub opens (before, every card said OPEN TO SIGN IN until you opened a service), and Tidal (and the other services that keep the sign-in in the page) now show SIGNED IN too.

- FIXED: Music Hub: sign-in pages with no background of their own (such as Apple's for Tidal) had dark text on the dark hub: that text is white now.

- CHANGED: The built-in Purphex plugins have a permanent purple neon border in the effect list (the same purple in every theme), so you can see at a glance they are built in. The last row of the list no longer runs into the help line under it.

- CHANGED: The side panel item Cover Studio is now Photo Studio.

## 3.53.0  Purphex VSTs: EchoGrid and Modal Filter

- NEW: Purphex EchoGrid: eight echoes on a grid of sixteen steps in time with the song. Click a cell to set when each echo comes back; every echo has its own level, pan, tone, drive, crush and pitch (an octave up or down). Grid (1/8, 1/16, 1/32, triplets), feedback, mix and output.

- CHANGED: Music Hub: the player buttons are proper round buttons with real play, pause and skip icons (in the hub and in the small corner player).

- CHANGED: Music Hub: inside a service the ALL MUSIC button now says BACK TO MUSIC HUB (on the front page it says MUSIC HUB).

- CHANGED: Music Hub: the small player in the corner only shows while music is playing, and its arrow hides it (a slim tab at the edge brings it back).

- FIXED: The overview bar under the tracks draws every track in one even colour with neat lanes, instead of patchy track colours.

- NEW: Purphex Modal Filter: two filters (low-pass, high-pass, band-pass, notch, peak, comb, formant), each with cutoff, resonance and drive, morphed, in series or side by side. An envelope follower and an LFO (sine, triangle, square, random) move the cutoffs or the morph. A live picture of both filters and the result.

## 3.52.0  Purphex Creative Studio: Video Studio and AI Cover Studio

- NEW: PURPHEX CREATIVE STUDIO in the side panel (VIDEO STUDIO and COVER STUDIO, under Music Hub): a video editor and a cover art studio inside Purphex, with their own projects kept apart from your songs.

- NEW: Video Studio: import video, audio, photos and transparent PNGs (button or drag and drop) with thumbnails, waveforms and file details; a multitrack timeline (video, audio and text tracks you can add, rename, mute, solo, hide, lock and resize) where clips move, trim, split, duplicate and snap to the playhead, clip edges and markers; select several clips; zoom and scroll; undo and redo.

- NEW: Preview monitor that plays the real composition (layers, positions, opacity, scale, rotation, crop, fit / fill, effects, transitions, text and the audio mix) with play, pause, stop, frame stepping, scrubbing and timecode; drag, scale and rotate a clip right on the monitor, with safe-margin guides.

- NEW: Transitions (cross dissolve, fade through black or white, wipe, slide, zoom, blur, plus fade in and out at a clip's start and end), colour and effects (exposure, brightness, contrast, saturation, temperature, tint, highlights, shadows, blur, sharpen, vignette, fade, sepia, black and white) with looks (cinematic, dark, vibrant, monochrome, music video neon and more), and titles with fonts, size, weight, colour, alignment, spacing, outline, shadow, background shapes and in / out animations, with presets for song titles, artist names, lower thirds, lyric captions, karaoke, credits and promos.

- NEW: Lyric videos: paste the lyrics, play the song and tap SPACE as each line starts; karaoke fill; import .srt and .lrc, export .srt (no automatic speech recognition: the timing is yours).

- NEW: Audio: volume, fades, normalise, mute and solo per track, waveforms; MY SONG brings in a mixed copy of your current Purphex song (your DAW project is never changed). Visualisers (spectrum bars, circular spectrum, waveforms, bass pulse, animated gradient, geometric shapes) read the real audio of the timeline.

- NEW: Make a video from a song: pick the song, a background picture or video, a visualiser, the artist and title and colours, and it is all put on the timeline. Project presets for 16:9, 9:16, 1:1 and 4:5 videos, lyric videos, cover videos, visualiser videos, teasers and 4K, or your own size.

- NEW: Export: MP4 (H.264 or H.265 / HEVC with AAC), WebM (VP9 with Opus) or audio only (M4A or WAV), at the project size, 720p, 1080p or 4K, 24 to 60 fps, four quality levels and audio bitrates, for the whole project or a range, with real progress, cancel and a check of the finished file. What you export is drawn by the same code as the preview.

- NEW: AI Cover Studio: a layered artwork editor (pictures, text, shapes, gradients, textures, vignettes) with brightness, contrast, saturation, hue, filters, crop, borders, shadows and blend modes; text always stays editable; square 3000 / 1600, 16:9, 9:16 and 4:5 sizes; PNG and JPEG export; use it as your song's cover or send it to Video Studio; an artwork library (save, rename, duplicate, delete). AI image generation with style, colour, mood, lighting and composition settings and a history with favourites, through OpenAI with your own API key (AI SETTINGS): nothing is generated without a key.

- NEW: Projects save safely (a broken save never replaces a good one), autosave in the background and are offered back after a crash; missing media files are marked and can be found again.

## 3.51.0  Music Hub: listen to your streaming services inside Purphex

- NEW: MUSIC HUB in the side panel (right under Community): YouTube Music, YouTube, SoundCloud, Spotify, Apple Music, Deezer, Tidal, Amazon Music, Bandcamp, BeatStars, Audiomack and Mixcloud play in their own official players inside Purphex, in a clean frameless view with Purphex buttons: back, forward, reload, straight to your library pages (liked songs, playlists, albums, history), OPEN IN OWN WINDOW, SIGN OUT and BACK TO DAW. You sign in on each service's own page; Purphex never sees your password. Protected music plays through Windows' own Widevine and PlayReady support (nothing is bypassed or downloaded).

- NEW: Now playing with the cover, title and artist from the service, play / pause, next, previous, seek, volume and a queue; a small player stays in the corner of every other page. The hub pauses by itself when your project starts playing, so the two never play over each other.

- NEW: CONNECT ACCOUNTS: connect Spotify, YouTube and SoundCloud through their official APIs (with a free developer app of your own) to see your playlists, liked songs, albums, followed artists, recently played and more as cards inside Purphex. Sign-in goes through each service's own page in your browser (OAuth with PKCE); the tokens are kept encrypted for your Windows user and DISCONNECT deletes them.

- NEW: Search across everything: songs from your connected accounts plus Deezer's and Apple's public catalogues (no account needed); click one to play it on its service. Inside a service, the search box searches that service.

- FIXED: The + ADD TRACK button at the bottom of a long track list was partly cut off.

## 3.50.1  Buttons, scroll bars and the audio bar always on top; type the bus level

- FIXED: Clicking the audio bar along the bottom (or the name above it) on My Projects, the community, profiles and artists clicked the project or card under it. The whole bar is on top now, and so is every scroll bar, slider, value box and button: whatever sits under them never takes their click.

- FIXED: Cards and rows scrolled out of sight under a bar or a header could still be clicked there. A click now only reaches what you can see.

- CHANGED: ALL PLUGINS: the level of a bus is a box you can click and type into (like the tracks), and the preset its plugins came from sits in its own box.

## 3.50.0  Purphex VSTs: Tri-Drive, Shift, Maximizer and Spectral Gate; Rainbow speed

- NEW: Purphex Tri-Drive: three-band distortion. Drag the two split lines, then give LOW, MID and HIGH each their own type (tube, tape, fuzz, rectify, crush), drive, level and mix. Steep crossovers keep each band to its own notes (drive the highs and the bass stays clean). After the bands: a filter, a one-knob compressor, feedback for ringing metal, mix and output.

- NEW: Purphex Shift: two frequency shifters (every frequency moves by the same number of Hz: phaser sweeps when small, metal and bells when big), each with up / down / both, an LFO and mix, in serial or parallel, with feedback for endless rising or falling spirals and stereo spread.

- NEW: Purphex Maximizer: one big LOUDNESS knob into a look-ahead limiter that never goes over the ceiling, with release, character (clean, warm, punch), low and high tone, mono bass, width, a LUFS meter and TARGET (streaming -14, loud -11, club -9, max -7) that sets the loudness for you.

- NEW: Purphex Spectral Gate: gates every frequency on its own, so spill, hiss, hum and room are turned down where they sit while the wanted sound passes. LEARN SPILL and LEARN WANTED listen and remember (kept with the project); with nothing learned there is one threshold line you drag. Reduction, attack, release, smooth, range, listen to what is removed, mix and output.

- FIXED: Clicking a button on a card or row sometimes clicked the card or project under it instead (PLAY NOW opened the EP, PLAY EP could open a song). Buttons now always take the click from whatever they sit on, everywhere in the app; an open pop-up still blocks what is behind it.

- NEW: RAINBOW SPEED: with the Rainbow theme picked, a slider under the themes sets how fast it moves. It now fades smoothly through the colours in small steps instead of jumping (the jumps flashed), and it starts slow.

## 3.49.0  No more crackling, EPs play in order, see where a sound will land

- FIXED: Crackling when Purphex Music is not the window in front: Windows 11 slowed the app down in the background ("efficiency mode"), which starved the sound. The app and its sound thread now opt out, so playback stays smooth while you use another window or click off an EP.

- FIXED: Clicks and crackles in the sound packs: starting, stopping, switching to another sound, jumping to another place and the loop point all fade over a few milliseconds instead of jumping, and every sound ends at silence.

- IMPROVED: Sounds that are not 44.1 kHz (sound packs, dropped files, impulse responses) are converted with a high-quality filter instead of a straight line between samples, so they no longer get a gritty top end.

- NEW: PLAY NOW under OPEN EP / OPEN ALBUM, on My Projects and on the community page: every song of the release plays in order, one after another (press it again to stop). The EP / album window on My Projects has PLAY EP too.

- NEW: Dragging a sound from the sound packs shows the clip it will make before you let go: its real length and waveform, on the row and at the time it will land (snapped to the grid), with the track name or NEW TRACK.

- FIXED: My Projects said "NO PROJECT MATCHES" over your EP card when every project was inside an EP or album.

## 3.48.1  Plugin windows follow your theme, text fits its boxes, AI GEN removed

- CHANGED: Every Purphex VST window (GlitchFlow too) now uses the colours of your theme: backgrounds, panels, edges, knobs, buttons and the main colours change with it. ON stays gold.

- FIXED: Text in the Purphex VST windows no longer runs out of its box or sits on a box edge (GlitchFlow input / output and refresh window, Glow saturation, Radio RM / bitcrush). Long labels shrink a little to fit, and a test now checks every window.

- REMOVED: AI GEN on GlitchFlow. The preset bar takes its place.

- FIXED: The box behind the preset name at the top of every Purphex VST window was missing.

- FIXED: With two or more EPs or albums, the list sent to your profile was broken, so they could fail to show there.

- FIXED: The frame rate you picked in Settings was not applied when the studio opened.

- FIXED: Number boxes kept a white border on the dark theme.

## 3.48.0  Purphex VSTs: Clipper, Smack, Vinyl Stop, Radio, Doubler, Octave Choir, Vowel and Step Shaper

- NEW: Purphex Clipper: soft, hard, tape, tube and fold clipping with threshold, softness, pre and post gain (LINK keeps the loudness the same), a curve that shows the shape and a scope that shows what was cut.

- NEW: Purphex Smack: a transient shaper. ATTACK lifts or softens the hit, SUSTAIN lifts or cuts the tail, each with its own sensitivity, short / medium / long shape and duration, plus mix, output and a guard (clip or limit) so it never goes over 0 dB.

- NEW: Purphex Vinyl Stop: press PLAY / STOP for a record or tape stop in time with the song (length in bars or beats), with curve, pitch, step, digital, wash and SPIN-UP for how fast it comes back.

- NEW: Purphex Radio: a broken radio. TUNE drifts in and out of the station, with static, raw signal, AM wobble, ring mod, bitcrush, feedback, skips, bandwidth, drive, noise and squelch.

- NEW: Purphex Doubler: four extra voices around the original, each with on / off, octave, gain, pan, delay, feedback, detune and wobble, plus direct gain and pan, a low and high EQ and output.

- NEW: Purphex Octave Choir: eight harmony voices with level, pan, delay, feedback, interval and cents. HARMONY can make the 3rds, 6ths and 7ths all major (bright) or all minor (dark), RANDOMIZE loosens the timing, plus input, output and mix.

- NEW: Purphex Vowel: a talking filter. Drag the dot between the vowels (or click a vowel button) to make any sound say "ah", "ee", "oo" and the rest, with drive, shift, Q, mix, snap, movement (left-right, up-down or circle, free or in time) and loudness match.

- NEW: Purphex Step Shaper: sixteen steps of volume (and a low-pass filter) in time with the song. Draw the bars, pick the grid and rate, add smooth and swing, and FOLLOW makes the filter move with the steps.

## 3.47.0  Purphex VSTs: Frost, Tape Dirt and Constellation

- NEW: Purphex Frost: spectral freeze. The snowflake holds the sound as an endless pad (fade, width, low / mid / high, clear), with a RING MOD and a FEEDBACK delay you switch on, an LFO that moves the ring, the delay or a filter, and a master with serial or parallel, dry, wet and soft clip.

- NEW: Purphex Tape Dirt: a worn tape machine with two VU meters. DRIVE for warm saturation, DIRT from clean to bad (duller highs, then flutter, then drop-outs), normal or de-coupled stereo, low cut and output volume.

- NEW: Purphex Constellation: a granular cloud on five lanes, each with on / off, gain and pitch, plus number of grains, grain length, max delay, feedback, forwards / backwards / both, width, reverb, dry and wet. The grains glow as they play.

- CHANGED: the top of every Purphex VST window fits smaller windows (the preset bar gets narrower instead of the buttons running off the edge).

## 3.46.0  Purphex VSTs: BreathOff, Phantom Gate, Stereo Lab, Vortex, MultiDyn and Prism

- NEW: Purphex BreathOff: finds the breaths in a vocal by itself and turns them down, smoothly. Breath and energy graphs with lines you drag (sensitivity and gate), reduction, fade out / in, monitor (voice or breaths only) and room tone. It looks 10 ms ahead so a breath is caught from its start.

- NEW: Purphex Phantom Gate: a gate for drums and vocals with separate THRESH and RETURN lines (no chatter), floor, spike (lets the crack of a hit through), attack, hold, release in ms or note values, look-ahead, side-chain filters with listen, curve, ratio (expander) and de-bleed, with a scrolling picture of the gate at work.

- NEW: Purphex Stereo Lab: a stereo scope and phase meter with input gain, phase flip and pan for each side, width, pan, mid / side balance and bass mono, plus A and B settings with an A-B slider you can automate.

- NEW: Purphex Vortex: a 3D auto-panner. Rotate (free or synced to the song), shape, space and echo, with gain, filter, width, bend and slice sections, and a swirl that turns with the sound.

- NEW: Purphex MultiDyn: a five-band compressor (or upward with a ratio under 1:1) with draggable crossovers, solo and on / off per band, LEARN, knee, clean or vintage, a limiter, and gain reduction shown per band over the spectrum.

- NEW: Purphex Prism: a spectral resonator. Any sound rings at a note and up to three intervals (a chord), with unison, detune, harmonics (all / even / odd), decay, damping, blur, freeze and kill, a rainbow spectrum and a keyboard to pick the note.

## 3.45.0  Purphex VSTs: Glow, Curve EQ, Shape and SubForge

- NEW: Purphex Glow (PURPHEX VST): a four-band enhancer. LOW, MID, HIGH and AIR, each with two characters and its own ON / OFF, a big WET INJECT knob, FX ONLY, LEVEL MATCH, stereo or M/S, high-pass / low-pass filters and saturation (drive, distort, crush), with live band meters. Every band at 0 is perfectly transparent.

- NEW: Purphex Curve EQ (PURPHEX VST): an 8-band EQ drawn over a live spectrum (before and after). Double-click to add a band, drag nodes, wheel for width, right-click for type (bell, shelves, 12 / 24 dB cuts, notch). Any band can be DYNAMIC (it only moves when that part of the sound is loud). AUTO GAIN, scale 6 / 12 / 18 / 30 dB, mix and output.

- NEW: Purphex Shape (PURPHEX VST): a compressor and transient shaper. Ratio, knee, threshold, attack and release (fast and auto modes), intensity, auto gain, feed-forward or feedback, mix and output, plus TRANSIENT, SIDE-CHAIN (with LISTEN), EQ (pre or post) and CLIP sections. A scrolling level and gain-reduction display with in, GR and out meters.

- NEW: Purphex SubForge (instrument): an 808 and bass synth. Sine, triangle, saw or square with octave, tune and fine, a SUB layer one or two octaves down, PUNCH (the 808 pitch drop), an envelope you can see, a filter with resonance, envelope and key tracking, GLIDE in mono or play poly, DISTORTION (tube, hard, fold, fuzz) and a COMPRESSOR, with a master spectrum and a keyboard to play.

- NEW: every Purphex VST has factory presets (around 14 each) and your own presets (save, rename, delete), undo / redo in its window, automation for every setting and is saved with the project.

- CHANGED: BYPASS buttons are now ON / OFF everywhere (the plugin panel, the Purphex VST windows, GlitchFlow and mastering): lit ON means the effect is working, OFF means the sound passes untouched.

## 3.44.0  Purphex GlitchFlow: our own stutter and beat-repeat plugin

- NEW: Purphex GlitchFlow, our own stutter, beat-repeat and glitch plugin with its own window, at the top of + ADD EFFECT in a new PURPHEX VST box. REPEAT RATE sets how fast the captured slice repeats and REFRESH RATE how often a fresh slice is captured, both locked to the song (1/1 to 1/64, dotted and triplet, 2 and 4 bars) or in free milliseconds. It follows tempo changes, loops, jumps and stopping without clicks or half-recorded audio.

- NEW: GlitchFlow FREEZE holds the captured slice and repeats it for as long as you like; release and it is live again at once, with a fresh slice at the next refresh.

- NEW: GlitchFlow patterns (constant, every other, last-beat fills, last half bar, random, build-up, slow-down, triplet skip, first half, 3-3-2) with intensity, length, decay, fade and four fade shapes, plus reverse, pitch and pitch step for each repeat.

- NEW: GlitchFlow modules, each with its own ON switch: low-pass with resonance, high-pass, telephone, rhythm gate, drive (soft, hard clip, bitcrush), width, modulation (pitch wobble, filter sweep, auto-pan), delay and reverb. Input, output, dry/wet and a fully transparent bypass. A live picture of the captured slice, the repeat steps and in/out meters.

- NEW: 55 original GlitchFlow factory presets (subtle, vocal, drum fills, build-ups, transitions, EDM, drum and bass, UK garage, trap, experimental, aggressive) and your own presets: save, rename and delete, with undo and redo in the window. Every setting can be automated and is saved with the project.

- NEW: GlitchFlow AI GEN describes-to-settings with a real AI (Anthropic Claude) once you add your own API key in its SETUP (kept encrypted on this PC). Without a key it says so and changes nothing; every AI answer is checked against the real settings and their ranges; UNDO and REGENERATE. QUICK MATCH works offline from keywords and is labelled as not AI.

- CHANGED: the PRESETS menu no longer has the old Bus presets (echo, ad-lib, reverb) list; BUS PRESETS (1,440) and the AI BUS GENERATOR replace it.

- CHANGED: MIDI REC and MIC (with its mode) sit just left of the time display, next to the transport.

- CHANGED: SOUNDS is now SOUND PACKS and has moved from the transport row to the plugin panel, next to + ADD EFFECT, PRESETS and BASS LAB (sound packs belong with the effects).

## 3.43.1  One of each type of effect, REGENERATE keeps the sound, AI BUS GENERATOR on ALL BUSES

- CHANGED: + ADD EFFECT lists one plugin of each type: Room, Hall, Plate, Chamber, Spring, Cathedral, Ambient, Shimmer, Gated and Reverse reverb, early reflections and real spaces; stereo, ping-pong, slapback, tape, analog, multi-tap and reverse delay; and so on for every kind of effect (125 in all). The thousands of versions that were only the same plugin with other settings (a size, a tone, a note value, a key, a mix) are gone from the list, because every one of those settings is on the plugin itself. A project that already uses one of them still opens with it.

- NEW: Cathedral Reverb, Vibrato and one Censor plugin (its sound, Bleep, Mute, Reverse, Muffle, Scramble, Static or Duck, is a setting on it).

- FIXED: REGENERATE on the AI generators gives another take on the same sound every time, for every effect: the same plugins with their amounts nudged a little (it did nothing for some effects, and sometimes jumped to another genre or reverb type).

- NEW: ALL BUSES has an AI BUS GENERATOR button (and BUS PRESETS when the window is wide enough); they work on the selected bus.

- CHANGED: the button that hides the plugin panel is a big glowing handle with an arrow, half way down the panel's left edge (like the side panel on the website); when the panel is tucked away the same handle, arrow pointing back, brings it out.

- FIXED: if the effect list was set to a category that no longer exists it showed nothing; it now shows every effect.

## 3.43.0  EFFECT LIBRARY: 10,339 effects, BUS PRESETS, AI BUS + EFFECT GENERATORS, Censor, demon voices

- NEW: the EFFECT LIBRARY. + ADD EFFECT now has 10,339 effects (it had 275): every reverb type (room, hall, plate, chamber, spring, cathedral, ambient) in 5 sizes, 5 tones and 6 characters (clean, ducked, gated, reverse, shimmer, modulated) at 3 amounts; every delay mode on every note value with 3 feedbacks, 5 tones and 3 characters, plus slapbacks and throw echoes; Auto-Tune in 192 kinds that sound different (how fast it snaps, how much of your slides it keeps, how hard it pulls, which voice), Auto-Tune with doubles, octave layers, formant, chorus, telephone, distortion, width or lo-fi on top, and Melody Tune with every harmony (the key is set in the plugin or found from your track); pitch and formant shifts, harmonizers and doublers; EQ curves; compressors, parallel compression, OTT, gates, de-essers, limiters and transient shapers; saturation, exciters, amps with cabinets and bitcrushers; chorus, phaser, flanger, tremolo, auto-pan, auto-filter, vibrato and width; telephone and radio filters; ring mods, vocoders and frequency shifters; stutters, glitch chops and tape stops; real spaces, resonators, talkbox vowels and granular clouds. They are in their own categories ending in Library, after the hand-made plugins; Category shows how many each has, and search finds them (try: hall dark, 1/8 dotted, auto-tune instant, telephone).

- NEW: An Auto-Tune the AI EFFECT GENERATOR makes in a key you name (hard autotune in F# minor) stays in that key instead of guessing the key of the track.

- NEW: demon voices like the pros make them: Croaky Demon Voice, Satan Voice, Lucifer Layer, Hell Lord, Possessed Whisper, Underworld Growl, Beast Mode Voice, Exorcist Voice, Venom Voice, Hellhound Snarl, Abyss Choir, Dark Lord Rap and Monster Under the Bed, plus 144 more in Demon & Monster Voices. They drop the pitch and the formants, then the new Growl engine adds a croaking vocal fry, a sub-octave and rasp, with drive and a dark reverb. Growl is also there on its own (Vocal Fry / Croak).

- NEW: CENSOR for clean versions: Bleep, Mute, Reverse, Muffle, Scramble, Radio Static or Duck. Right-click a section and press Censor this section (split around the word first to hide just that word): it adds one Censor plugin to the track and switches it on only over that section. Uncensor takes it off. Clean Version Bleep only beeps while the word sounds.

- NEW: BUS PRESETS: 1,440 presets for buses, 12 kinds (vocal, ad-lib, backing vocals, drums, 808 and bass, beat, reverb, echo, FX, crush, glue, mix) in 15 genres and 8 flavours. Use puts it on the bus you are editing, on the bus the selected track goes into, or makes a new bus of that kind and routes the track into it. Open it from PRESETS on a bus, the BUS PRESETS button in VOCAL PRESETS, or Tools > Bus Presets.

- NEW: the AI BUS GENERATOR: describe the bus (drill drum bus, punchy and dark; Juice WRLD vocal bus with more glue and a 1/4 echo) and it builds the chain, showing what it understood.

- NEW: the AI EFFECT GENERATOR (top of + ADD EFFECT, or Tools > AI Effect Generator): describe the effect you want (huge dark cathedral reverb with a shimmer; 1/8 dotted ping pong delay, tape, ducked; hard autotune in F# minor; telephone with a bit of distortion; croaky demon voice 9 semitones down) and it makes it with those settings, shows what it understood and adds it to YOUR PLUGINS. Words it does not know as an effect are looked up among the library names.

- NEW: REGENERATE and RANDOMISE on every AI generator (presets, buses and effects): REGENERATE gives another take on the same words (another starting point and slightly different amounts), RANDOMISE makes a surprise from random words.

- CHANGED: the PRESETS button shows how many presets there are: PRESETS (1482) on a track; on a bus it says BUS PRESETS.

- NEW: the plugin panel on the right slides away: press >> at its top and it tucks into a thin PLUGINS tab on the right edge, so the timeline gets about 350 pixels wider. Click the tab to bring it back. It remembers how you left it, and adding an effect to a track brings it out.

- CHANGED: the tips for whatever the mouse is on show in a slim strip at the top of the studio, just above the ruler, instead of a box at the bottom. The long timeline tip fits on one line.

- CHANGED: the PRESETS menu is tidier: Rap vocal presets (the old 6) and Remove all the presets are gone (VOCAL PRESETS has all of that), and Community chains / My saved chains are called Community presets / My saved presets.

- CHANGED: the time display sits just left of |< (go back to the start); PLAY stays in the middle.

## 3.42.1  Community pictures load, centred transport, preset counter

- FIXED: on the Community tab some songs showed the purple logo instead of their picture. The app only allowed two connections to the website at a time, so the pictures queued behind fonts, avatars and uploads, timed out and waited a minute before trying again. It now uses many connections at once and tries a missing picture again after 5 seconds.

- CHANGED: the transport row is laid out around PLAY: PLAY sits in the middle with |< and STOP before it and PAUSE, LOOP and the time after it; the recording tools (MIDI REC, MIC, SOUNDS) are on the left and the page tabs on the right. On a narrower window the middle slides so nothing overlaps.

- CHANGED: the plugin panel title shows the name of the track you are on, the same as its box on the left (it showed the song name from the audio file, which looked the same on every track).

- NEW: a preset counter beside VOCAL PRESETS (1482 presets, or 111 of 1482 while you search or filter), and the PRESETS... button says how many there are.

- CHANGED: the PREVIEW / LOADING name above the preview strip sits in a small black box with a neon edge, so it is easy to read on top of buttons and pictures.

## 3.42.0  VOCAL PRESETS: 1,482 presets and the AI PRESET GENERATOR

- NEW: Rapper Vocal Styles is now VOCAL PRESETS, with 1,482 presets (it had 36). Over a hundred are inspired by the sound of an artist, named in brackets: Juice WRLD (emo singing, freestyle, hooks), Eminem (rapid fire, angry, Slim Shady), Drake (smooth OVO, melodic, late night), Mazza (Scouse road rap, northern melodic), Travis Scott, Playboi Carti, Kendrick Lamar, J. Cole, Lil Baby, Future, Young Thug, Lil Uzi Vert, Gunna, Lil Durk, Polo G, Rod Wave, NBA YoungBoy, The Weeknd, SZA, Bryson Tiller, Central Cee, Dave, Stormzy, Skepta, Pop Smoke, Burna Boy and many more: 91 artists. They are sound designs inspired by how those vocals sound, built from this app's plugins, not anyone's real studio settings.

- NEW: ULTIMATE all-in-one chains (Rap Lead, R&B Vocal, Trap Hook, Drill Lead, Melodic Sing, Boom Bap, Rage, Loud & Clear, Podcast Clean), ULTRA R&B (Glossy and Dark & Moody), and new groups: R&B, Singing (pure singing chains), UK Rap, Afro & Dancehall, Emo & Rock Rap, and Ad-libs & FX (phone, radio, megaphone, demon, chipmunk, robot, stadium throws, chops, glitches, tape stop...).

- NEW: every sound comes in ten versions, one for each job: Lead, Ad-Libs, Doubles, Hook, Dry Verse, Spacey, Dark, Bright, Radio and Stutter, with a PART row to show only one kind, and an ARTISTS button for every artist preset.

- NEW: presets marked +BUS bring a vocal bus (glue compression and space): it is made once, the track goes into it, and the ad-libs and doubles of the same sound go into the same bus, so your vocals sit together like a real vocal chain.

- NEW: AI PRESET GENERATOR (top of VOCAL PRESETS, or right-click PRESETS...): describe the sound in your own words, like Juice WRLD with a stutter delay and lots of reverb, and it builds the plugins. It understands the artists, genres and effect words (stutter, delay 1/4 / 1/8 / dotted / triplet / ping pong, slapback, reverb, hall, plate, dry, dark, bright, autotune or natural, harmony, octave, distortion, telephone, radio, deep or baby voice, wide, doubled, loud, soft, 8D, robot and more, with a bit / lots of for how strong), shows what it understood, and USE puts it on the track; SAVE AS MY PRESET keeps it and shares it in Community presets. It works inside the app, no internet needed.

## 3.41.7  Bigger on/off circles on plugins

- CHANGED: the green circle that turns a plugin on or off is 50% bigger, so it is easier to see and to hit: in the panel on the right, on the ALL PLUGINS page and in the mastering chain.

## 3.41.6  Smooth lyrics scrolling

- CHANGED: the lyrics scroll smoothly now, pixel by pixel, instead of jumping a whole line at a time. The mouse wheel glides 3 lines a notch, a click on the scroll bar glides there at a reading speed, and dragging the logo follows your hand.

- FIXED: the mouse wheel with the pointer on the lyrics themselves did not scroll them. It does now, the same as over the scroll bar or beside the box.

## 3.41.5  Lyrics: size, fill screen, the whole song

- FIXED: with the lyrics scroll bar at the bottom, the last lines of long lyrics did not show (the text box spaces its lines a little wider than the bar thought). The bottom of the bar is now exactly the end of your lyrics, at any text size.

- NEW: A- and A+ on the LYRICS page make the lyrics smaller or bigger (kept for next time), and FILL SCREEN gives the lyrics the whole window under the play controls, with the song title at the top (EXIT FILL SCREEN goes back).

- CHANGED: clicking the lyrics scroll bar, its arrows or the mouse wheel over it now glides the lyrics there slowly, one line at a time, so you can keep reading while they move (it used to jump). Dragging the logo still follows your hand straight away.

## 3.41.4  SAVE PRESET under your effects, lyrics scroll fixed

- NEW: SAVE PRESET right under your effects in the panel on the right: every plugin of the track, with all its settings, saved as your own preset under the name you choose. It is in PRESETS... on any track, in every project.

- FIXED: the scroll bar on the LYRICS page did not answer the mouse wheel, and its logo did not follow when you scrolled or typed in the lyrics. The wheel now scrolls the lyrics over the bar and anywhere beside the box, and the logo always shows where you are. The wheel works on every logo scroll bar in the studio now.

## 3.41.3  Gain on every section, your preset under MASTERED

- NEW: every section of a track has its own gain right on it. Hover a section to see its gain tag (it stays when the gain is not 0 dB): drag the tag up or down to change that section only, half a dB at a time, and double-click it for 0 dB. Right-click a section for Section gain: +6 / +3 / +1 / -1 / -3 / -6 dB, 0 dB, -12 dB for quiet backing, or set it exactly; with several sections picked they all change together.

- NEW: under MASTERED in the Mastering page visualiser (and in the full visualiser) you can always see which preset is on your master, with EDITED once you change it; click it to open the presets.

- FIXED: a version from AUTO MASTERING (like Vocal Forward), or a preset you tried and then kept, did not show as the preset in use: the top bar said None. Both show now, and they are kept in the project.

## 3.41.2  Name your sections, remove all plugins

- NEW: right-click a section of a track and press Rename this section to give just that piece its own name (Yeahs, Adlibs, Hook, Verse 2...). The name shows on the piece in gold, the track keeps its own name, and it is saved in the project. Pick several sections to name them all at once; leave it empty to show the track name again. Ctrl+Z undoes it.

- NEW: REMOVE ALL PLUGINS on the ALL PLUGINS page takes every plugin off every track and bus in one go (on ALL BUSES, off every bus). It asks first, instruments (Bass Lab, VST instruments) stay, and one Ctrl+Z puts them all back.

## 3.41.1  Pause and play never jump you around, safer recording

- NEW: recording onto a track you cannot hear (muted, switched off, or another track is soloed) asks first: unmute it and record, record anyway, or cancel.

- NEW: when your microphone sends no sound at all (muted on the headset, the mic boom flipped up, switched off), a warning says so within a second of recording, with what to check; a silent take is no longer added as an empty track.

- FIXED: pressing PLAY threw the view to wherever the needle was (forwards or backwards), so after scrolling away while paused you lost your place. PLAY now never moves the view. And PAUSE stops exactly where the needle is drawn: the needle no longer hops forward a little when you pause, and PLAY carries on from that very spot.

## 3.41.0  Mute a section of a track

- NEW: right-click a section of a track and press Mute this section only: that piece makes no sound (in playback and in every export) while the rest of the track still plays. A muted section is greyed out with a dashed edge and a MUTED tag, it still moves, trims and splits like any other, Ctrl+Z puts it back, and it is saved in the project. With several sections picked, Mute these sections does them all at once; right-click again to unmute.

## 3.40.9  Saving a published track updates it live

- NEW: saving a project that is published now updates its song on your profile and the community page straight away: it is exported and the new version replaces the old one, keeping its plays, likes, comments and music video. It used to keep the old version until you published again. Saving with nothing changed sends nothing.

## 3.40.8  Previews play smoothly straight away

- FIXED: playing a project on My Projects (or a song on Community) could stutter and stay that way until you played something on the mixing page. The fix-it that notices the sound breaking up only watched the mix. It now watches whatever you are listening to, and previews always use the biggest playback buffer (nothing is played live on top of a preview, so the extra delay costs nothing).

## 3.40.7  Sub-Bass Generator without the crackle

- FIXED: the Sub-Bass Generator crackled and distorted. On loud bass it added a sub at a fixed loudness (so the sound went far past full scale and clipped), and on quiet bass it switched on and off with a click on every note. Now the sub follows the loudness of your bass, fades in and out smoothly, tracks the note without noise flipping it, glides between notes, and a soft ceiling stops it ever pushing the sound past full scale. Your presets keep their settings.

## 3.40.6  Your preset on the Mastering page, trial countdown

- NEW: the Mastering page shows the mastering preset that is on your master, right in the top bar (gold), with EDITED once you change the chain after choosing it, or None: your own settings. Click it to open the presets. It is saved with the project, so it is still there when you open it again: handy for showing how presets work.

- NEW: 3 days of Fantasy free for new members. Your first Purphex account gets the Fantasy Edition free for 3 days (one trial per person, not per account). Settings > Account shows your trial and when it ends.

- NEW: a live countdown of an edition that ends (the free trial, a monthly plan, a tester pass): under your name in the menu, and in Settings > Account, ticking every second. purphexmusic.com shows it too.

## 3.40.5  EPs stay in place, bass leaves room

- CHANGED: on My Projects an EP or album is no longer always put first. It sits in the list like any project: where its most recently saved song would be (with the sort you chose), so it moves up when you work on one of its songs and otherwise stays where it was.

- CHANGED: Bass Boost now starts with its Output level at -12 dB, so you get big bass that leaves room for the vocals (and no clipping). Your saved projects and presets keep their own setting.

- FIXED: a Spotify, Apple Music or Deezer link could say the song was not on YouTube when the artist writes their name differently there (for example BUSYGETTINLIZZY15 for Busygettinlizzy). The artist is now also matched the way YouTube writes it, and another artist's song with the same name is still never taken.

- NEW on the website: the Converter is now on purphexmusic.com (in the menu), free with every account and made for phones: paste a link or pick a video (screen recordings, saved videos, any size) and get an MP3 or an MP4 back.

## 3.40.4  Streaming links get the right song

- NEW: Apple Music and Deezer links work in the converter too (songs, albums, Deezer playlists, artists' top songs), with the same strict matching on YouTube. YouTube, YouTube Music, SoundCloud, Bandcamp and the other 1000+ supported sites convert the exact song you linked, as before.

- FIXED: a Spotify link could convert a random song: the converter took YouTube's first search result. It now looks at the first 10 results and only converts the one that really is the song: the same title, the artist in the title or channel, and the same length as on Spotify (to within a few seconds). Covers, remixes, slowed or sped-up versions, loops and other artists' songs with the same name are passed over, and a song YouTube does not have is skipped with a message instead. Short spotify.link share links work too.

## 3.40.3  Lock many sections at once

- NEW: pick several clips (drag a box round them) and right-click one: Lock these sections locks every one of them (Unlock these sections when they are all locked). One Ctrl+Z puts them all back.

## 3.40.2  Pause right on the spot

- FIXED: PAUSE (and STOP) stop exactly where you are listening. They used to skip ahead by the audio still waiting in the sound card (up to about a fifth of a second), so PLAY carried on a little further on. The sound card now says exactly what it has played, and the needle uses the same, so the needle, pause and play all agree to within a few milliseconds.

## 3.40.1  Cleaner vocal and beat split, split anywhere

- NEW: SPLIT VOCALS + BEAT is much cleaner: it uses the best separation model (htdemucs_ft, four fine-tuned AI models instead of one) with smoother joins between the parts it works on, and the beat is now the song itself minus the vocal, so nothing of the beat is lost or muffled and vocals + beat add up to the song exactly. It takes a few minutes (about four times longer), runs on your graphics card, and downloads the model once (about 300 MB).

- CHANGED: splitting is as free as moving: a split goes exactly where the needle (or your click) is, it no longer jumps onto the nearest grid line, and you can split anywhere inside a clip, right up to its edges.

## 3.40.0  Spotify links in the converter

- NEW: Spotify links in the converter. Paste a Spotify song, album, playlist or artist link: Spotify's own audio is locked, so Purphex reads the songs (artist and title) from the link and finds each one on YouTube, then converts it like any other link. An album or playlist converts every song; an artist link converts their top 10.

## 3.39.8  No more nudge buttons

- REMOVED: the small < and > nudge buttons on each track. (Drag a clip to move it.)

## 3.39.7  See the preset you are using

- NEW: the preset you are using stands out: in the presets of any effect and in the mastering presets, the one you pressed USE on has a glowing outline, an IN USE tag and its button says IN USE (like the selected track in ALL PLUGINS).

## 3.39.6  NEW goes to the welcome page

- CHANGED: NEW (in the studio toolbar) takes you to the welcome page after it clears the project, so a new song starts from the start.

## 3.39.5  The whole picture, never cut off

- FIXED: a tall or wide MP4 zoomed right into your picture and cut most of it off. The whole picture is always in the video now, never cropped: it fills SQUARE videos edge to edge (the right size for a square picture), and wide or tall sizes show the whole picture with black only where the shapes differ. The size list says which ones fill.

## 3.39.4  Your picture fills the video

- FIXED: the picture in an MP4 video now fills the whole video, edge to edge, in every size. A black border inside the picture (from making it smaller with Resize) is trimmed off, and wide or tall videos crop a little off the picture's edges instead of adding black bars.

## 3.39.3  One picture in your videos

- FIXED: MP4 videos in wide and tall sizes showed your picture twice (a blurred copy filled the sides). Now the picture is shown once, in the middle, on solid black, in every size.

## 3.39.2  Songs play right, ALL BUSES, EP order, MP4 sizes

- FIXED: songs played from the Community tab and artist pages sounded broken: they went through a second mastering chain (a compressor and limiter) that squashed every published song. They now play exactly as they were published.
- FIXED: + ADD in ALL PLUGINS did nothing you could see: the list of effects opened behind the sheet. It now opens on top.
- NEW: ALL BUSES: EDIT BUSES (in the Bus drop-down) opens a page with every bus: its level, mute, delete, the tracks that go into it and its plugins. ALL PLUGINS and ALL BUSES have a + ADD BUS button at the top (the big NEW BUS column is gone) and a button to switch between the two.
- NEW: change the order of an EP or album: open it on My Projects and move songs up or down; each song shows which track it is (TRACK 1, TRACK 2...), and your profile gets the new order. When you make one, each picked song shows TRACK 1, TRACK 2... too.
- CHANGED: the EXPORT window no longer locks the program: the tabs and everything else stay clickable while it is open.
- NEW: song pictures next to the names on an artist's page. Emoji in menus (your links on a profile) are in colour.

- NEW: MP4 export, your choice of size and sound: 12 video sizes (square 720 to 4K, wide 480p to 4K for YouTube, tall 1080 x 1920 and 4K for TikTok, Reels and Shorts) and AAC 128, 192, 256 or 320 kbps sound. Wide and tall videos keep your picture square in the middle on a blur of itself. The video part is made as small as it can be (the picture never changes), so a 3 minute song is about 4 MB at 128 kbps, 5 MB at 192, 8 MB at 320.

## 3.39.1  Buses in ALL PLUGINS

- NEW: ALL PLUGINS has your buses too, so everything is in one place like Logic Pro: each bus is a gold column with its plugins (+ ADD, copy and paste them the same way), it shows which tracks go into it, TRACKS INTO THIS BUS lets you tick the tracks that go through it, and + NEW BUS at the end makes a bus. Picking a bus there edits it in the plugin panel.

## 3.39.0  Export your song as an MP4 video

- NEW: EXPORT as an MP4 VIDEO. Pick MP4 video in the Format box: your song comes out as a video with the picture of your project (the one on its card in My Projects, or the Purphex logo when it has none), for exactly as long as the song. 1080 x 1080 H.264 with AAC 320 kbps sound, ready for YouTube, Instagram and TikTok.

## 3.38.9  ALL PLUGINS: the selected track stands out

- NEW: ALL PLUGINS makes the selected track plain to see: its column glows in the track colour with a thick lit edge and a SELECTED tag (click anywhere on a column to pick that track). Every column has a PASTE PLUGIN button: COPY any one plugin, then PASTE PLUGIN puts it on another track with all its settings.

## 3.38.8  EPs you can open and play

- NEW: EPs and albums on the Community tab: under ALBUMS and EPS each release is one card. Click it (or the EP name on any of its songs) to see its songs in order, play any one, or press PLAY EP / PLAY ALBUM to hear them all one after another.
- NEW: EPs and albums on My Projects: the projects of an EP are one EP card (not separate cards). OPEN EP lists its songs: PLAY hears one without opening it, clicking a song asks to open its project. While you search, the songs show one by one.
- CHANGED: an EP needs at least 4 songs (a short release) and an album at least 5.
- NEW: renaming a published project renames the song on the community straight away.
- CHANGED: every option in every menu has its own neon box, with a little space between options.

## 3.38.7  Power buttons, bigger menus

- NEW: the light at the top left of a track is now a real POWER button: bigger, round and glossy with a power sign, lit in the track colour when on and dark red when off, and pressing it sends pulse rings out of it.
- CHANGED: every menu (right-click on a clip, plugin menus, pickers) is 40% bigger, with bigger text and taller lines, so you do not click the wrong thing.

## 3.38.6  Lock one section

- NEW: right-click a piece of audio and press Lock this section: only that piece is locked (the rest of the track can still be moved, cut and edited). A locked section has its own look: gold stripes, a glowing gold edge and a LOCKED SECTION tag. Try to move or change it and it tells you: this section is locked, right-click it to unlock. It stays locked when the project is saved.

## 3.38.5  Lock the right track

- FIXED: LOCK (and the DUPLICATE and DELETE track pickers) listed every piece sharing a row as a track of its own, so 7 tracks showed as 9 with the wrong numbers. It lists exactly the tracks on the left now, TRACK 1 is track 1, and locking a track locks that track (with the pieces on its row). The padlock no longer covers the name.

## 3.38.4  Loop a section, AUTO DETECT key, copy and paste plugins

- NEW: LOOP asks what to loop: the whole song (it keeps playing round) or a section. For a section, drag across the timeline over the part you want: it plays it again and again, straight away, exact to the beat. The section shows as a band on the ruler; drag its ends to change it.
- CHANGED: SPLIT VOCALS + BEAT always puts the beat (instrumental) as the first track, with the vocals right under it.
- CHANGED: the right-click menu on projects is gone: every option is a button on the card (open, preview, rename, picture, favourite, publish, remove).
- FIXED: ALL PLUGINS scrolls like the rest of the app: the logo scroll bar under the tracks (drag the logo, click the bar, hold an arrow), or grab an empty part and drag it sideways. It only worked with the mouse wheel.
- NEW: AUTO DETECT on every plugin with a key (Pitch Correction, Melody Tune and every tuner made from them): it listens to the track and sets the key and scale. It also works on a row made of pieces.
- NEW: the plugin you are editing stands out: a bright neon outline, a glow and a bar on its left, in the plugin list and in ALL PLUGINS.
- NEW: hold the left button on ANY empty space of the timeline and drag a box to select several clips: the empty part of a track, an empty track, or below the tracks.
- CHANGED: STOP stops right where you are, like PAUSE: the needle and the view stay put (press |< to go back to the start).
- CHANGED: while a song plays, the view no longer jumps after the needle when it runs off the screen (zoomed in, you stay where you are editing). Pressing PLAY shows the needle once if it is off screen; |<, STOP, Home and clicking the ruler still take you to it.
- FIXED: COPY and PASTE on plugins. A pasted Auto-Tune, pitch corrector or Melody Tune kept waiting to detect the key and then threw away the key you copied; it now keeps the copied key (also with PASTE PRESET and PASTE ALL PLUGINS). VST plugins can be copied and pasted now too (with their own patch), and a pasted Bass Lab plays the notes of the track it was pasted on.

## 3.38.3  Rename projects

- NEW: a RENAME button on every project in My Projects (under UPLOAD MUSIC VIDEO). A published project gets its new name on your profile and the community page too.

## 3.38.2  Plugins stay on their own track

- FIXED: plugins put on one track (your vocal) could also play on another track (the beat). Sharing a row (dragging one track onto another) left the track list out of order, so the plugin panel, the mixer and saved projects pointed at the wrong track. The list is kept in order now and puts itself right if it was already wrong, and reopening a project gives every track back its own plugins, volume and automation.

## 3.38.1  A safer close

- FIXED: closing a window at the moment it was being drawn could crash the program (the glow around text boxes was drawn on a surface that was already gone). The button test now also presses every entry of the side menu, its lists and the New Track box.

## 3.38.0  The release check: 23 fixes in the app

- MASTERING: pressing Try or Use on a master preset while the song plays could switch the master Limiter off for good (playback and exports then clipped). The master is changed safely now, also by AUTO MASTERING.
- EXPORTS land exactly on time: with plugins that delay the sound (Pitch Correction) or with buses, an export could cut the first moments of the song or come out a little late. The delay is now worked out the same way the mix uses it.
- STEMS and Processed vocals / instrumental exports of a split track hold the whole track (every piece), with one stem per track.
- The vocal ducker follows every piece of a split vocal, not only the first one. A soloed track that was merged onto another row no longer silences the whole mix. Playing an instrument while stopped follows its track volume, pan, mute and bus.
- UNDO: removing a plugin with its X can be undone (and a VST taken off is never brought back broken). Clear all plugins on a bus asks first and can be undone. After deleting or undoing a track the picked track stays right, and instrument windows of removed tracks close.
- Deleting a track's first piece passes the right fades to the next piece.
- YOUR PROJECT IS SAFE: a project that fails to open, a project preview, or a song being made for the Community no longer change the open project's lyrics, cover or BPM. Removing a project's picture stays removed. Writing a cover into a project can no longer lose the project file.
- The converter: a typed search with quote marks can no longer become extra commands for the downloader. When mastering fails the WAV is kept and the log says where.
- COMMUNITY covers up to 3 MB show in the app, and a cover that failed tries again a minute later. Pictures of artists are downloaded once, not again for every window size. The unread messages badge no longer comes back after you read them.
- A PC clock that was once set ahead no longer locks a paid PC to Free: linking again (or the next licence) fixes it.
- The Rainbow theme uses far less memory and power (it changed every picture of the program 7 times a second).

## 3.37.9  The converter keeps every colour of your theme

- FIXED: after the update the converter window could come up half in your theme and half in purple (the borders in your colours, the buttons purple). At start, while a linked PC was still renewing its licence, the theme flashed to the original and back, and the window took its colours from the wrong one. Now the theme is not touched until your edition is really known, so every window opens fully in your colours.

## 3.37.8  Copy all the plugins of a track to another track

- COPY ALL PLUGINS and PASTE ALL PLUGINS: in ALL PLUGINS every track has them (and PRESETS... too). COPY ALL PLUGINS takes every plugin of a track with its settings and its preset boxes; PASTE ALL PLUGINS puts them all on another track, and the plugins it already had stay. The instrument (Bass Lab or a VST) stays on its own track.

## 3.37.7  Drag several clips to other tracks at once

- DRAG A GROUP TO OTHER TRACKS: clips you picked together (with the selection box) can be dragged up or down to other tracks: they all move the same number of tracks, keeping their times. Onto a track that has audio they join it as pieces, below the last track new tracks are made. Before, a group could only slide along the timeline. Ctrl+Z undoes it.
- A clip moved to another track keeps its fades and its gain (they were lost when it joined a track that already had audio).

## 3.37.6  Edit a bus, REMOVE ALL moves to PRESETS

- EDIT A BUS: every bus in the Buses sheet has an EDIT button: its effects open in the plugin panel (EDITING BUS), where you can open and change them, switch them off, move, copy, paste and remove them. BACK TO TRACKS (or pressing the box of a track) goes back. Before, a bus could only get effects added.
- REMOVE ALL is no longer in the YOUR PLUGINS box: it is in PRESETS... as Remove all plugins (go RAW), and it asks Are you sure first (REVERT and Ctrl+Z still bring everything back).

## 3.37.5  Your theme stays (and colours the website), the plugin panel says whose plugins they are

- YOUR THEME STAYS: the theme you choose is never lost any more. While your edition is not known yet (a licence being renewed, or no internet) the original theme is shown for a moment, and your own theme comes back by itself as soon as your edition is known. It used to be reset to the original for good, so a Fantasy or Supporter theme did not carry over.
- YOUR THEME ON THE WEBSITE: on a PC linked to your account, the theme you choose here colours purphexmusic.com for you too (the menu, buttons, links, tabs, panels and background), for the themes of the edition you are on.
- WHOSE PLUGINS: the top of the plugin panel says which track they are on: TRACK 2  BEAT  Instrumental, in the colour of that track. Two tracks of one song start with the same long name, so the panel could look like the vocal while it showed the beat (the plugins of one track never change another track: a test checks it).

## 3.37.4  SPLIT VOCALS + BEAT removes the original

- SPLIT VOCALS + BEAT keeps your project tidy: the vocals and the instrumental take the place of the song, and the original track is removed (it used to stay, muted and marked original). Ctrl+Z brings it back. A track that has other pieces on it keeps those pieces; only the audio that was split goes.

## 3.37.3  COPY and PASTE on every plugin, COPY PRESET, the logo scroll bar in the welcome box

- FIXED: a track whose own clip was moved away while pieces were still on it showed EMPTY TRACK over those pieces. A track now only says EMPTY TRACK when nothing at all is on it (a test checks it every time).
- The order buttons of a plugin are drawn arrows (up and down) instead of the letters ^ and V, and say what they do when you point at them.
- The preset boxes no longer have their own COPY button: PRESETS... (Copy preset) and COPY PRESET in ALL PLUGINS copy a preset.
- COPY AND PASTE ON EVERY PLUGIN: every plugin row (on your tracks, on the master and in ALL PLUGINS) has a COPY and a PASTE button. PASTE on a plugin of the same kind puts the copied settings on it; on another kind it adds the copied plugin as a new one. (A VST plugin keeps its settings inside itself, so COPY tells you to use its own preset menu.)
- ALL PLUGINS: each track has + ADD, COPY PRESET and PASTE PRESET. COPY PRESET copies the preset on that track (a list lets you pick when it has more than one); PRESETS... has Copy preset too. The plain PASTE button there is gone (every plugin has its own PASTE now).
- A long plugin name next to COPY and PASTE on a narrow window goes onto two lines instead of being cut off.
- NUDGE: the two small arrow buttons next to the pan move the whole track 10 ms earlier or later (1 ms with Shift), to line up its timing by ear. They now say so when you point at them, move every piece on the track (not only the first), never go before 0:00, and Ctrl+Z undoes them.
- The right-click menu no longer has Delete this piece: Cut this piece out does the same now (it deletes the piece and nothing moves).
- THE WELCOME BOX scroll bar is now the same as the one under your tracks: a black bar with the arrows at both ends and the logo you hold and drag (waves flow out of it while music plays). Click the arrows to step one project, hold them to keep going, click the bar to jump, or use the mouse wheel.

## 3.37.2  An explosive visualiser, AUTO buttons in seconds, HOME on My Projects

- THE VISUALISER HITS: every kick is a BANG (the logo jumps, the stage shakes, rings and sparks fly), once per kick, also on loud dense songs where it used to barely move. Between the hits the bars fall right down, and each band follows the loudest and quietest part of your song, so a loud master still moves all the way. The full visualiser mode gets all of this too.
- AUTO MASTERING, AUTO LOUDNESS, SET LOUDNESS and AUTO EQ take seconds instead of minutes: the mix is listened to in slices spread over the whole song (a short song whole) and kept until a track changes, the loudness is set by running only the master chain again, and the ten mastered versions are worked out side by side on every processor core. On a test song AUTO MASTERING went from about 52 seconds to about 12 (and about 7 the next time), AUTO LOUDNESS from about 55 to about 7.
- MY PROJECTS has a HOME button, like the other pages.

## 3.37.1  Move clips freely, duplicate a piece, empty tracks stay

- A CLEAN DRAG: sliding a clip along its track moves just the clip itself, with a thin line and the exact time where it starts. The see-through copy that sat on top of it (and looked doubled and glitchy) only shows when you drag a clip to another track.
- DUPLICATE A PIECE: right-click a piece and choose Duplicate this piece: exactly that piece is copied onto a new track right under its track, at the same time, with that track's plugins. Duplicate the whole track is still there too.
- EMPTY TRACKS STAY: a track is never deleted by itself any more. An empty track stays where it is (so you can organise your tracks); only its X or DELETE removes it.
- ADD MUSIC asks first: pick music from your computer, or add an empty vocal track or an empty audio track (no music).
- AN EMPTY TRACK has no box to click any more (it used to open the file window): it says EMPTY TRACK. Drag a clip onto it, drop files on it, record with MIC, or right-click it and choose Add music to this track.
- MOVING A CLIP IS FREE: it goes exactly as far as you drag it, even the smallest amount, anywhere between the grid lines (it used to jump to the nearest grid line, so it would not sit in the gap you wanted). Trimming its start or end is free too.
- A drag starts after the mouse moves 2 pixels (it was 4), so tiny nudges work.

## 3.37.0  Effects always belong to the track, ALL PLUGINS, copy presets, auto key, reply to messages

- EFFECTS BELONG TO THE TRACK, ALWAYS: every piece of audio on a track plays through that track's plugins and presets, also after the track's own clip was moved to another track and only split pieces are left on it. Before, those pieces lost the effects of the track they sit on.
- ALL PLUGINS: a new panel (the ALL PLUGINS button above the plugins, or PRESETS...) shows every track with its plugins side by side, like the mixer of a big DAW. Add, paste, switch off, copy, remove and open plugins on any track from one place.
- COPY PRESET and PASTE PRESET: every preset box has COPY, which copies the whole preset (all its plugins with their settings). PASTE PRESET (in ALL PLUGINS and in PRESETS...) puts it on any other track in a box of its own.
- AUTO KEY: Pitch Correction and Melody Tune find the key of their track by themselves when they are added (from + ADD EFFECT, a preset or a style) and tune to it straight away. A key you pick yourself, or one saved in a project, is never changed. DETECT KEY still does it again by hand.
- MESSAGES: there is a message box under the conversation. Press it (or pick a conversation) and type: Enter sends, Esc stops typing, Ctrl+V pastes, the smiley adds an emoji. Letters you type go into the message, not to the shortcuts of the studio.
- REPLY: press any message in the conversation to reply to it. The box shows REPLYING TO and the message you answer, and the other person sees what you replied to. The little chat window works the same way.
- MESSAGES WITHOUT PUBLISHING: a PC linked to your purphexmusic.com account (Settings, Account, LINK THIS PC) can send messages, add friends and start collabs before you publish a profile, the same as on the website. It is the same conversation in the app and on the website.
- COPY AND PASTE ON EVERY EFFECT: every plugin row in YOUR PLUGINS and on the master has a C button that copies it, and PASTE next to the heading (and next to Add Effect to master) adds the copied plugin with its settings.
- WEBSITE: purphexmusic.com has a Messages page in the menu (with the number of unread messages) with the same three parts as the app: Messages, Friends and Collabs. It works on phones too.
- FRIEND REQUESTS: ACCEPT and DECLINE work again in Messages and Friends (the scroll area of the list was lying on top of them and took every click; it took the clicks on the conversations too). The request leaves the list the moment you answer.
- CLOSING THE DAW PAUSES THE MUSIC: however the DAW window is closed, the mix, a project preview, a song you were listening to and the sound browser all stop. Before, the mix could keep playing after the window was gone.
- DRAGGING THE NEEDLE scrolls the timeline with it: hold it at the left or right edge and the mix moves along (faster the further past the edge you hold it), so you always see the audio under the needle. It never goes behind the track boxes any more.
- THE NEEDLE STOPS EXACTLY AT THE END: when the song finishes the needle parks on the last bit of audio (the reverb and delay tail is still heard), and it runs down to the bottom of the last track only, not into the empty space or the bar under it.
- THE PICKED TRACK OUTLINE ends exactly where the audio of the track ends, also for a track made of pieces (after SPLIT or paste). When the audio ends before the part you see, the outline goes round the track box only. It used to run past the audio, past the end of the timeline and under the plugin panel.
- PICKING A TRACK: only the box of a track on the left (with its name, M, S, + ADD EFFECT and the volume) picks the track. Clicking the audio moves, trims and edits it, but leaves the picked track alone.
- CUT NEVER MOVES YOUR AUDIO: cutting a piece or a section out leaves a gap where it was. The audio after it, the notes and the automation all stay exactly where they were (before, everything after the cut slid back to fill the gap).
- THE WELCOME BOX shows all your projects, not only the newest four: they sit in a row with the scroll bar of the website under it (drag it, click it or use the mouse wheel). The cards no longer say how many tracks a project has.
- ADD EFFECT: the Plugins... button in its title strip works again (the strip you drag the box by was lying on top of it). The same goes for every button in the title strip of a box you can drag.
- On a narrow window the COPY, PASTE and REMOVE buttons of a master plugin with a long name (Multiband Compressor) sit on their own row instead of over the name.

## 3.36.9  The side menu opens on hover, a smoother DAW, nicer profile links

- SIDE MENU: point at a menu (for example COMMUNITY) and its tabs open at once, from where the pointer is. No click needed.
- SPACE BAR plays and pauses on the Mix page only. On any other page it leaves the mix alone.
- PROJECTS and PROFILE: the word tracks is gone from the project cards and from your profile page, and the artist pages say PROJECTS.
- SHARE PROFILE: the link carries your artist name (purphexmusic.com/artist/aheath), not a row of letters.
- The PLUGINS AND EXTENSIONS window opens on top of the window you pressed it in (one at a time), instead of hiding behind it.
- SMOOTHER: the program no longer asks for more frames than your PC can paint (it always keeps time for the mouse, so dragging windows and tabs stays smooth), and it paints less while a window is being moved. PERFORMANCE MODE keeps asking for the most your screen can show, up to 240.

## 3.36.8  One track, one set of effects; copy and paste; project pictures fit; SPLIT works zoomed in

- EFFECTS, MUTE, VOLUME AND PAN NOW BELONG TO THE WHOLE TRACK: every piece on a track (made by SPLIT, pasted, or added to the same row) plays through the same plugins, and MUTE, SOLO, the volume and the pan reach all of them. Before, only the first piece was changed and MUTE left the other pieces playing.
- COPY AND PASTE: right-click a piece, Copy this piece; right-click any audio track, Paste here (it lands where you clicked, on that track, through its plugins). On every plugin there are now COPY and PASTE buttons: PASTE puts the copied settings on a plugin of the same kind, or adds the copied plugin as a new one.
- PRESETS: every plugin shows which preset is on it (PRESET: NONE, or its name, or (CHANGED) when you moved something by hand) and REMOVE takes the preset off (back to the default settings, REVERT puts it back).
- The whole ruler, down to its bottom border, moves the needle (a track scrolled up under it used to take the click). Narrow clips get thin grips instead of grips covering the whole clip.
- SPLIT: zoomed in, a split now cuts exactly where the needle is. It used to jump to the nearest grid line, which when zoomed in is far away from the needle (and could be outside the clip). It only sits on the grid when a grid line is within a few pixels of the needle.
- PROJECT PICTURES: nothing is stretched any more. A picture that is not square sits whole on a plain background (the blurred, stretched copy that filled the square is gone). Every project picture you already had was gone through: the picture itself is found in the middle and kept on its own, fitted to the frame.
- EXPORT: the folder you picked last time is the folder the Export box opens with (only the folder is remembered; the other choices start fresh).
- MIX: the neon outline round the track you picked used to run on across the whole empty lane after the audio had ended (it looked like an extra box). It now stops where the audio stops.
- MIX: clicking on the empty part of a track (past its audio, or between its pieces) no longer switches to that track, and the "Click: select this track" label is gone there.
- SPLIT: the pieces are called Vox (2), Vox (3)... instead of Vox (2) (2) (2), and the plugins run once over the whole track (no cut-off tails at the split points).

## 3.36.7  Pictures that fit themselves, a smaller REVERT box, the DAW resets when you leave

- PROJECT PICTURES: a picture with black (or white) bars round it is cut to the picture itself and fitted to the frame by itself. Press the camera on a project: a sheet opens with the same ZOOM, LEFT / RIGHT, UP / DOWN, FIT and FILL controls as your profile picture, plus CHOOSE ANOTHER and REMOVE.
- The REVERT box is smaller (one line, no wider than the column it sits in) so it does not reach into the next box. The BEFORE / AFTER loudness lines on the Mastering page no longer sit under AUTO EQ, and the box corners are round all the way.
- LEAVING THE DAW resets it: the next time it opens it is empty and the welcome box with your projects is back, like the first time.
- A track box picks its track wherever you press it, not only on the name.

## 3.36.6  Colour mixer tidy-up, no nickname if you like

- COLOUR MIXER: the second bar under the circles was a copy of the HORIZONTAL GRADIENT MIX bar and is gone (that one moves the mix). The words in the middle circle fit inside it again (SOLO and A ONLY when one colour is on).
- NICKNAME: leave the box empty and press OK to have no nickname.

## 3.36.5  SHARE PROFILE gives your own page, performance mode at 240 fps, one export choice at a time

- SHARE PROFILE copies the link to YOUR OWN PAGE on the website (name with your nickname, picture, banner, bio, links, genres, tracks and songs) and opens it. Anybody you send it to can see it, with no sign-in. Publish your profile first (and not private) to have one.
- PERFORMANCE MODE no longer limits the frame rate: it takes the effects away and runs at the highest rate your screen can show, up to 240 fps.
- EXPORT: only ONE choice can be ticked at a time (complete mix, without mastering, vocals, instrumental, individual stems), and one always stays ticked.

## 3.36.4  Your profile no longer resets, new links window, the colour mixer fades one colour, asks before leaving or deleting

- YOUR PROFILE NO LONGER RESETS after an update: the bio, place, links, banner, nickname, the list of your projects with your name and picture, and your saved colour mixes were being written over with empty ones when something saved before they were read. Everything is read first now (and a test makes sure).
- NICKNAME: it shows in brackets after your name on your own profile too (as it does on the ARTISTS tabs and now on the website).
- NAMES: a name another artist already uses is no longer refused. You can have it: yours shows with a number after it (for example aheath #598774) and the first artist keeps the plain name. No more "name taken" window.
- The artist cards have the name and the track count in a neon box of their own, like the bio.
- LINKS window: a box for the TITLE and a box for the ADDRESS on every row (up to 10). Only streaming services and social media are allowed (YouTube, TikTok, Instagram, Snapchat, Spotify, Apple Music, SoundCloud, Bandcamp, X, Facebook, Twitch...); every row shows OK or what is wrong. Links shine in the colour of their service.
- Every link out of the program asks first: "You are leaving this DAW and being redirected to YOUTUBE" (press YES to go).
- SHARE PROFILE button on your profile: it opens the Community on the website, where your songs are (it asks before leaving).
- The buttons that delete something ask first: MAKE ALL PRIVATE, MAKE IT PRIVATE, REMOVE BANNER, REMOVE PICTURE, DELETE THIS PRESET (colours), REMOVE ALL tags, and removing an automation. Tracks and plugins do not ask: they have the REVERT bar.
- COLOUR MIXER: USE A ONLY and USE B ONLY are switches (press again and both colours are back). With one colour on, the gradient bar still moves: all the way left the colour stays exactly as it is (100%) and the further right the darker it fades. The Purphex logo decorates the colour wheel box.
- LOCATION: one click on a place picks it and closes the box.
- EMOJIS: the emoji panel opens beside the box you are typing in (never on top of it), so you can see what you type.

## 3.36.3  purphexmusic.com on the converter page, a BASS LAB button, a back-to-start button on Mastering

- A box of its own under the log on the converter page: PURPHEXMUSIC.COM. Press it to open the website (downloads, guides, the community and your account). The log box is a little shorter to make room.
- BASS LAB has its own button next to PRESETS in the plugin panel: press it and the Bass Lab synthesizer is put on the selected track and its window opens, ready to play. The ADD EFFECT box no longer lists Bass Lab: its instrument box is now VST PLUGINS only.
- MASTERING: the |< button (jump back to the start) sits left of PLAY, the same as on MIX. The LYRICS page already has it, in the same place.
- COMMUNITY: the second ARTISTS button on the Community page is gone (the ARTISTS tab in the top bar stays).

## 3.36.2  Generate Lyrics is gone, the Fantasy DAW buttons, a lock list that stays open, a GAIN box like the track list

- GENERATE LYRICS is removed (the button, the menu item, its window, the recent list and its help page, and the speech recogniser it downloaded): it did not write the words of songs well enough. The lyrics .txt that is saved next to each converted song, and the Lyrics page of the DAW, stay.
- FANTASY EDITION: every button of the DAW has the pale rim of the converter buttons, and the rim is exactly the same thickness all the way round, corners too, on every button. The Supporter edition keeps its own buttons and does not get this look.
- LOCK: the list of tracks stays open, so you can lock or unlock several tracks one after another and press DONE. A locked track is gold with a padlock in the list, and its piece on the timeline is gold all round with a big gold padlock.
- GAIN: the same neon box as the track list (TRACK 1, TRACK 2...): press a track to pick it, then set its volume under the list.
- The faded dark backdrop behind the movable tabs and sheets (Styles, Themes, Presets, New Track, Key, Auto, Mastering and the Add Effect browser) is gone, so moving them no longer looks glitchy and the page behind stays clear.
- MASTERING: a PAUSE button next to PLAY.
- AUTO EQ: after it runs, a block with A ORIGINAL and B EQ'D lets you flip between the EQ as it was and the EQ it made (like ORIGINAL VS MASTERED). The small AUTO EQ button on the master EQ header is removed (the big one stays).
- The mouse wheel never changes a value any more: plugin sliders, EQ bands, Bass Lab knobs, drop-down lists, number boxes and slider bars only change by click and drag. The wheel only scrolls pages and lists.
- The message box (Toast) never covers the REVERT bar: it moves above it, so REVERT is always pressable.

## 3.36.1  Unlimited tracks on your profile, EPs and albums of up to 20 songs, a welcome box that comes back, clickable empty tracks

- PROFILE: your profile can list any number of tracks (there was a limit before), on your profile page and on the artist page other people see.
- EP / ALBUM: an EP or an album holds up to 20 songs (the box says so, and the website keeps the first 20).
- WELCOME: if you press OPEN A PROJECT FILE and then cancel the window, you are back at the welcome box.
- EMPTY TRACKS: the add-music box inside an empty track is clickable (it was hidden behind the track). The RECORD HERE tag is gone: the picked track is simply lit.

## 3.36.0  A welcome box, the buttons of the converter on the DAW, AUTO EQ, nicknames, EPs and albums without limits, a Friends page, translucent colours, clearer fonts and a lot of tidying

- WELCOME: Purphex Music now opens on the mix with a neon box on top (your latest projects, START A NEW PROJECT, OPEN A PROJECT FILE and ALL MY PROJECTS) instead of opening on My Projects. Any choice, or the X, takes you to the mix.
- BUTTONS: every button of the DAW now looks like the buttons of the converter window: a soft shade, a thin lit edge, a light line on the top edge and a glow under the mouse. The one and two mark buttons (the menu, X, +, -, the arrows) have their mark exactly in the middle. Buttons that touched each other (the icon buttons on the project cards, RECYCLING BIN above PUBLISH ALL) now sit side by side, and the screen audit checks for it on every screen.
- ARTIST NAME: the window that says the artist name is taken only opens when you change your name on the Profile tab, never for a name that is already yours. A name held by another profile of your own linked account is yours: publishing it again from a new install moves your old profile (friends, followers, messages, collabs and songs) to it.
- NICKNAME: a new row on the Profile tab. A nickname can be anything you like (no swear words, up to 24 characters) and shows in brackets after your name on the ARTISTS tabs.
- EP / ALBUM is now a neon box inside the DAW with as many songs as you like (up to 300): tick them, put them in order with UP and DOWN, name it, pick EP or ALBUM. On My Projects, TYPE has EP / ALBUM to show only the songs that are in a release, and SORT is now Newest edited, Recently opened, Name A to Z, Name Z to A and First / oldest track you have (Most tracks and Longest are gone).
- AUTO EQ: a button on every Parametric EQ (the master and every track). It listens (the whole mix, or that track before its effects), sets bass, mud, presence, upper-mids and air for you, asks first, and REVERT or Ctrl+Z puts the EQ back.
- EXPORT: INDIVIDUAL STEMS shows a tab with every track of the project so you pick the ones to save on their own. Every FORMAT and RATE now has a plain-words description (which one is the highest quality, how big, what it is for). The (m:ss) words are gone.
- LEAVING THE DAW: lyrics that were never saved ask first: YOU HAVE UNSAVED LYRICS, save them as a .txt file?
- TRACKS: the Edit menu has no CUT any more; LOCK, DUPLICATE and DELETE TRACK ask which track (TRACK 1, 2, 3...). The LOCK, FLEX and DEL buttons ask which track when there is more than one, and GAIN has the track numbers inside its box. The track that is picked is lit along its whole row and says RECORD HERE when a recording would go onto it. A track with no audio has its own box inside its border: click it to pick music for that track, or drop files on it.
- PLUGIN PANEL: every preset is a box of its own (adding a preset never takes away another preset or your plugins, and REMOVE takes only that one), YOUR PLUGINS is always there, and REMOVE ALL takes every plugin and preset off at once to go raw (the instrument stays, REVERT brings them back). In ADD EFFECT, Bass Lab and the VST instruments sit together in one box that fits them exactly.
- FLEX PITCH no longer saves by itself (and never saves the project): DONE keeps your edits, REVERT ALL goes back to the original recording, and the new UNDO NOTE goes back one change at a time. The white pitch lines are short and stay on their note, and the notes are bigger so you do not have to zoom all the way in.
- MENU: COMMUNITY opens a list in the side panel with two choices, the Community tab and the Artists page (no numbers). The Community page has no New project button (MY PROJECTS is there). A PERFORMANCE button sits in the DAW as well as in VIEW, and a track that is off in performance mode shows its waves very faintly with a line that says why it cannot be edited. A small box next to the microphone arrow names the microphone mode.
- FRIENDS: a Friends page in the same layout as Messages (friend requests, your friends, and for each one MESSAGE, INVITE TO COLLAB, OPEN PROFILE and REMOVE FRIEND), and a FRIENDS button in Messages, Collabs and the artists list.
- ARTISTS: on the artist cards the bio is in a box of its own with a small arrow to read the rest. On an artist page COPY NAME and COPY BIO copy them, and TRANSLATE opens Google Translate with their name and bio: it finds the language by itself and translates it to yours. When FADE MY NAME A TO B is off, your name and your bio are in the normal white.
- TIME MADE: your own artist page shows your time as it is now, and the time is sent to your public profile every ten minutes.
- COLOUR MIXER: colour A and colour B each have an ON / OFF switch (so a profile can have one colour), USE A ONLY and USE B ONLY, the mix is a strip like the scroll bars with your logo as the handle in a box of its own, the logo rides over the gradient bar, the stepped blend reads 100% A, 75% A, 50 / 50, 75% B, 100% B, there is a new TRANSLUCENT see-through finish, and USE AS THEME makes your mix the theme of the whole program in one press.
- UNPUBLISH: making a song private again offers REVERT. The song is only hidden: its plays, likes and comments are kept and come back with it (same link) when it is public again.
- COMMUNITY PRESETS: every preset you save is shared (this switch stays on: community presets are for everybody). They show as your name's preset for RAP, BEATS or the effect, with the list of plugins used, not the name you gave them.
- BACKGROUND: picture and video backgrounds (in Settings and Themes) are gone. Settings has USE LEAVES and DON'T USE LEAVES for the floating leaves.
- FONTS: a font with thin or hollow letters (outline, shadow, hairline) now writes the big titles only, so the small writing stays in the clear original font and you can read everything.
- GENERATE LYRICS now listens to YOUR song first (the lyrics database is only used when listening is not set up or nothing could be heard), so the words are the ones in your version. It keeps quiet words and low-confidence words, brings quiet parts up before listening, and a hook said up to ten times is kept.
- EMOJI: the colour pictures are kept in a folder, so every emoji is in colour the moment it appears, the colour stays while the text is highlighted, the EMOJIS button opens and closes the panel, and CLEAR RECENT empties the recent list (only when you press it).

## 3.35.0  A real community: songs, artists, friends, messages, collabs, EPs, albums and music videos, export trim, presets that keep your plugins, Bass Lab YOUR SOUNDS, performance mode

- PUBLISHING A SONG now puts the song itself on the community page of the website: it is exported as an MP3 (mixed and mastered, like EXPORT), uploaded, and members listen to it, like it and comment. Publishing asks first (and says that your profile is made public with it, so people can find the song in ARTISTS). Making a project private again, or deleting it, takes the song off the page. Projects that were published before are not uploaded unless you publish them again.
- The COMMUNITY tab is now the songs of everyone, published from Purphex Music: filter by GENRE, NEW RELEASES, ALBUMS, EPS and SAVED. Every song has a round play button, a SAVE SONG button and a share link that opens it on the website. The Published filter and the Open button are gone from it: you open your own work in My Projects only. The cards have a running light around their border (here and on My Projects), like the picture on a profile.
- EP / ALBUM on My Projects: put several of your projects together as one release (a name, EP of 2 to 6 tracks or an album of up to 30, tick the projects, put them in order). It goes on your profile, and the Community tab shows it under EPS or ALBUMS. SHARE on My Projects opens the community page of the website.
- MUSIC VIDEOS: UPLOAD MUSIC VIDEO on every project (it goes with the song on the community page). Any size and any shape works (wide, square, tall, 4K): a file that the web cannot play is turned into one (the shape stays), and it is never more than 100 MB. Playing a song that has a video asks if you want to watch it too; the video then plays in a small player above the strip at the bottom, in time with the song, with a FULL SCREEN button that fits the picture to any screen without stretching it. Every track shows MUSIC VIDEO or AUDIO ONLY. On the website the song page plays the video where the picture was (it has full screen too).
- ARTISTS is where you talk to artists. On an artist page: ADD FRIEND (they accept or decline), MESSAGE, MESSAGE ABOUT IT on every track (a reply to a song), BLOCK, REPORT (you must say why: the report goes to the staff on Discord, in the member reports), their TIME MADE, FRIENDS and followers, and every track has a round play button, its own progress and the music video button. The MESSAGES page has your conversations, friend requests and an unread number on the ARTISTS button. The CHAT window (Menu, Pages) is a small chat that you can drag anywhere over the DAW, so you can talk and make music at the same time. FRIENDS filters the artists.
- COLLABS: invite a friend to make one track with you. When they accept, each of you sends your own version (one of your projects, mixed and mastered) and gets the other one to put in your own project (GET THEIR VERSION saves it in Documents > Purphex Music > Collabs). Each upload tells the other artist in a message.
- The DAW asks before it lets go: the X says YOU ARE LEAVING THE DAW and, when the project is not saved, asks if you want to save it (you give it a name and it is in My Projects). SAVE says SAVING WILL OVERWRITE THE CURRENT PROJECT, and opening a project or file says OPENING WILL OVERWRITE THE PROJECT THAT IS OPEN NOW. Purphex Music opens on My Projects with a banner: START A NEW PROJECT or OPEN A PROJECT FILE. The Community button of the menu gives two choices: the community tab or the artists page.
- Export: complete mix with mastering and complete mix without mastering cannot both be ticked any more (ticking one un-ticks the other). The dry vocals and dry instrumental choices are gone. New TRIM start and end boxes export only a part of the song (m:ss).
- Presets in Mix no longer take your plugins away: a preset is added, your own plugins and Bass Lab stay, and a box above them says which preset was used (REMOVE takes away only that box, a different preset replaces only the first one). You can still add plugins of your own under it, and the preset name is saved with the project.
- Bass Lab and VSTs have a box of their own (INSTRUMENT) in the plugin panel, and a box of their own in ADD EFFECT (INSTRUMENTS AND VSTs: they make the sound, they are not effects). In the plugin panel Bass Lab is only the OPEN BASS LAB button, because you edit it inside Bass Lab.
- Bass Lab has a menu button (three bars) in the top left corner instead of the preset name box. It opens YOUR SOUNDS (drag n drop): BUILT-IN sounds, MY SOUNDS (saved until you delete each one with its x), COMMUNITY sounds, SAVE CURRENT SOUND, and PUBLISH SOUNDS TO COMMUNITY (the sound you are playing, one of yours, or all of yours, after asking). Drag a sound out to save it as a .bsound file, drop a .bsound file on Bass Lab to add it.
- PERFORMANCE MODE (Settings > Animation, and the VIEW menu of the DAW) for slower PCs: 30 fps, no hover sparks or glide, thin glows, still backgrounds, no moving logo, no animated profile banner, and a track that is off is simply darkened.

## 3.34.0  Lyrics from a link, a worldwide place search, colour emoji, publish prompts, a preview that keeps playing, zapping glitch effect, neon menus

- Generate Lyrics now works from a LINK even when the song is not in the lyrics database: the sound of the link is fetched, only to listen to it, written out by listening (with the quality and voice choices of the page), and deleted afterwards. RECHECK VIDEO + WORDS works from a link too. Stray quote marks in a video title are cleaned before the search.
- WHERE YOU ARE: type one letter and every country and place that starts with it shows up, anywhere in the world (towns, counties, areas, islands), searched as you type; the countries are in the program and work without the internet.
- The emoji panel shows every emoji in COLOUR (it is drawn by the browser engine of Windows). Every box where you type a name, a bio or a description has an EMOJIS button. Flags are not in the panel because Windows draws them as letters.
- The small visualiser on the Mastering page now looks exactly like the full visualiser, only smaller: its lines and dots follow the size of the ring, so the bars no longer run together.
- Publishing asks first. Every project on My Projects has a PUBLISH TO PROFILE + COMMUNITY button (it asks, and says people see the name, tempo, length and tags, never the audio), there is a PUBLISH ALL button on that page (it asks: this will publish every project that is not published yet, are you sure?), and PUBLISH ALL is gone from the Profile page. The Community page has a Published filter.
- AUTO LOUDNESS and SET LOUDNESS (MANUAL) ask Are you sure first, like AUTO MASTERING.
- A preview that is playing keeps playing when you go to another page, with the strip and its pause button on the projects, community, profile, artists and fonts pages. It stops by itself the moment something plays in Mix, Mastering or Lyrics.
- UPLOAD PIC sits right above DELETE PIC on the project cards.
- The look of a track that is switched off is better: the audio turns into a ghost whose colours split apart like a broken screen, strips jerk sideways, scan lines crawl and blocks of colour flicker, lit by the colour of the track, with bolts of lightning that strike across it, branch, flash the light round them and spark where they land.
- The toolbar is tidier: SPLIT, CUT and DUP are gone from it (and Split at the needle from the side menu) (the B key splits, and right-clicking a clip has Split, Cut and Duplicate), the buttons are only lit while they are really on (GRID SIZE and FLEX were lit all the time), the BACK button is gone from the Mix, Mastering and Lyrics pages, the side menu item Track height is now Thin tracks and thins the tracks exactly like the THIN TRACKS button, the frame rate is always shown in the title bar (the switch for it is gone from the View menu and Settings), pressing the X of the main window asks Are you sure (it closes everything) and then whether to save your project, the picture and the banner of a profile are animated (lights run round the picture, a band of light and drifting sparks cross the banner), the Fantasy look of the buttons and the gold Fantasy theme are for the Fantasy Edition only (the theme is put on the DAW again when you update), the lists that ask which track (Detect key, Split vocals + beat...) show one button for each track of the timeline, not one for every piece of a split track, your bio can be as long as you like (the box grows with every line you write, and line breaks are kept), the clock of the transport has a small permanent neon glow, artist names are unique (the same name with other capitals, dots or dashes is taken: a window says so and suggests the same name written in other ways), every link on a profile has a title and shows its address, FADE MY NAME A TO B: OFF puts your name in the normal colour (white) whether the profile colour is on or off, SWAP opens each side on the style of the colour that is on it (and so does loading a preset), the marker on the colour gradient is the logo of the scroll bars, the Bass Lab piano makes sound again after a project preview (the preview had taken over the instrument sound), there is a PAUSE button next to PLAY (PLAY only plays now), and the Mastering page has no LOUDNESS MATCH button: original and mastered are always compared at the same loudness.
- Menus have neon borders and glowing separators, and in every menu the option that is chosen right now (the grid size in use, the microphone mode in use...) keeps a lit frame, a bar on its edge and a tick, so it is never mixed up with the option you are pointing at.

## 3.33.0  Profile themes, whole pictures, links and tags rules, music videos, a place search, emoji for lyrics, saved songs, smoother sound

- COLOUR MIXER: the white lines in the colour wheel are gone (it still turns). The merge is brighter and truer (orange and blue now pass through green and yellow instead of turning grey-brown). The gradient and the five steps are clickable: click or drag along the gradient, or click a step, to set the mix. Metals, chrome, brushed, pearlescent, holographic, chameleon and glitter now shine: a lit bevel and a sheen that slides across them in a wave, and glitter that twinkles, on the swatches, the three big circles, the gradient and the steps. Whatever you point at gets an outline. The COLOUR SWATCH VARIATIONS box is removed.
- Every colour code in the mixer can be copied with a click, and pasted with a right-click (a code on the clipboard becomes colour A or B).
- USE AS A THEME is now SAVE AS THEME: it adds the theme to a department of its own, THEMES then PROFILE THEMES, and nothing is switched on, so there is nothing to revert. That tab has a DELETE THEME button under your profile themes.
- The pictures on your profile (banner and picture) now show the WHOLE picture, and the empty sides are filled by stretching the edges of the picture, so it never looks plain and nothing is cut off. Zoom in yourself if you want it cropped. What is published to the community is made the same way.
- The text of a profile takes the colour of the profile (the colour you chose in the mixer), for you and for everyone who looks at it: the name fades from colour A to colour B on every profile, and the text, tags and buttons carry the colour.
- Links on profiles can only be streaming services and social media (Spotify, Apple Music, YouTube, SoundCloud, Bandcamp, Tidal, Deezer, Instagram, TikTok, X, Facebook, Twitch, Discord, Reddit and more): no other websites. They are clickable, as buttons, and a LINKS button on a profile lists them all with the name of each service. Links written in a bio are made clickable too, and other web addresses in a bio are removed. The website checks the same rules.
- Genre tags: as many as you like, in a box that scrolls with the neon bar of the DAW, and no swearing (a tag with a swear word is not kept). REMOVE TAGS on the profile removes all of them, only the ones you typed yourself, or one by one; the tag window has REMOVE MY OWN.
- Your published projects are in a neon box of their own on your profile, and every published project can have a MUSIC VIDEO: a link (YouTube, Vimeo, TikTok...) that shows as a MUSIC VIDEO button on your profile.
- WHERE YOU ARE is a search: type any country, county, town or area (Poole finds Poole, Dorset) and pick it from the list; you can change the words before you use them.
- The Lyrics tab of the DAW is tidier: the GENERATE LYRICS and RECENT GENERATED buttons are gone from it (Generate Lyrics is on the first screen), and the colour wheel is just the wheel, with no A, B or M markers. The lyrics page has an EMOJIS button: a panel with hundreds of emojis in groups and a search by word (fire, heart, guitar, sad...); click one to put it in your lyrics at the cursor.
- Community: SAVED SONGS and SHARE buttons. On the community page of the website you can now SAVE other producers songs (the bookmark) and open them under Saved songs (for listening: a song cannot be downloaded or saved to your PC). In the program, SAVED SONGS and SHARE open the community on the website, where you sign in.
- Project cards have UPLOAD PIC and DELETE PIC buttons. Buttons in the DAW now have the lit rim and glassy top of the buttons of the main window. The revert bar is a neat neon box on the left.
- Scroll bars: the text boxes of the windows (the log, the links box, the lyrics, the changelog, the tags) have the neon bar of the DAW instead of the grey Windows one.
- Sound: the first play after opening the DAW no longer crackles. The sound card now waits until its buffers are full before it starts, the audio thread runs as a pro audio thread, the code that makes the sound is prepared in the background when the studio opens, memory is cleaned while nothing is playing, and loading a file never takes the processor from the sound.
- Generate Lyrics finds the RIGHT song: a file named only with its title is matched by its length (a tribute or a live version is not used), a failed lookup is tried again, and songs that have to be heard now have a QUALITY choice (Fast, Better, Best) and HEAR ONLY THE VOICE FIRST (the singing is taken out of the music first, with the Vocal remover). TIME MADE counts while the DAW is open, even when it is not in front. TIMES USED sits clear of the logo, the Pages menu boxes fit, and the revert bar counts 30 seconds.

## 3.32.0  Colour mixer with two palettes, presets, every finish of colour, a language box for lyrics

- The colour mixer on the Profile page is rebuilt with two sides, like the reference: the LEFT palette is colour A and the RIGHT palette is colour B, and A merges into B. Each side has its own STYLE drop bar, so you pick both colours from any style. Between them are the merged circle, a MIX slider and SWAP.
- Whatever you change in the mixer IS your profile colour: no buttons to choose where it goes. The banner, rings and accents take the merged colour at once and your name fades from A to B. Two switches make it toggleable: PROFILE COLOUR = MY MIX (ON/OFF, OFF goes back to the colour of your theme) and FADE MY NAME A TO B (ON/OFF). The old ACCENT COLOUR square and the separate colour buttons are gone.
- SAVE COLOUR PRESET keeps the two colours, their finishes and the mix; PREV and NEXT go through your saved presets (each one is loaded and becomes your profile colour), you can click any saved preset, and DELETE THIS PRESET removes one. The mixer also remembers where you left it.
- USE AS A THEME (a theme for the whole program from your mix, REVERT puts the old one back) and COPY THE HEX CODE stay. Every section of the mixer is in a neat neon-outlined box. The colour wheel turns slowly and no longer has a title.
- More than a thousand colours in every finish, like a car paint sheet, each drawn in its own finish: Classic gloss, Metallic, Metals, Brushed metals, Chrome and mirror, Chameleon (40 colour-shifting pairs), Candy, Pearlescent, Holographic and iridescent, Glitter and sparkle, Carbon and tech, Satin, Matte, Frosted glass, Worn and weathered, Neon glow, Basic, Pastels, Jewel tones, Earth and nature, Flowers, Wood and stone, Vintage and retro, Military and utility, Skin and hair, Sweets and drinks, Seasons, Sky and sea, Fire and sunset, Greys and blacks, and Every shade (360 shades and a grey row).
- Generate Lyrics has a LANGUAGE drop bar (Auto-detect and 68 languages, remembered). The speech recogniser listens in the language you pick, which fixes songs it mis-detects. Lyrics that are not English only get spacing and capital letters tidied (the apostrophe and punctuation rules are for English), and Auto-detect works out which one a lyric is.
- The lyrics lookup was checked against the real lyrics database and YouTube with songs in English, Spanish, French, German, Italian, Korean and Hindi, with names written as downloads really are (uploader first, brackets, Official Video).

## 3.31.0  Colour mixer on your profile, Recheck video + words, a fuller lyrics lookup, a calmer visualiser

- COLOUR MIXER on the Profile page, in a box of its own (not a tab). Pick colour A and colour B and see the colour they make: a merged circle, a gradient from A to B, five steps (100% A, 75/25, 50/50, 25/75, 100% B), a colour wheel with arrows to A, B and the result, and swatch pairs like Orange + Blue. A MIX slider moves the merge point and SWAP flips A and B.
- A STYLE drop bar chooses which colours you pick from: Every shade (360 shades and a grey row), Basic, Neon and glow, Pastels, Metals (gold, silver, bronze, copper, rose gold, platinum, chrome, titanium, steel and more, drawn shiny), Pearlescent (pearl, opal, abalone, iridescent, drawn with a soft sheen), Matte (matte black, sage, terracotta and more, drawn flat), Jewel tones, Earth and nature, Sky and sea, Fire and sunset, Greys and blacks. Over 580 colours. Right-click puts a colour in the other slot.
- USE AS MY PROFILE COLOUR sets your profile colour to the merged colour, USE AS A THEME makes a theme for the whole program from your mix (REVERT puts the old one back), and COPY THE CODE copies the hex code.
- Generate Lyrics finds songs far more reliably. It reads a name every sensible way (Artist - Title, Uploader - Artist - Title, Title - Artist), cleans the artist and title written in the file tags (the channel name, Topic, VEVO, brackets), and a guess that fails on the network no longer stops the others. A match must be the right artist and the right title, so a cover or a reply song with the words in its name is never used.
- The database can hold several copies of a song and some are cut short: every real copy is looked at and the most complete one is used, so verses are no longer missing. Songs that only have time-stamped lyrics are used too (without the time stamps).
- RECHECK VIDEO + WORDS: Purphex listens to the song or video file again and lines the words up with the lyrics in the box. It lists lines that are not sung, finds passages that are sung but missing (a missing verse), and ADD THE MISSING PASSAGES puts them in the right place, marked to check. Nothing changes until you press a button, Undo takes it back, and a speech recogniser can still mishear a word, so it is a guide and not a guarantee.
- When a lyric is longer than the box, the page now says how many lines are below the edge (scroll to see them), so a verse out of sight no longer looks like a missing verse. The listening settings were changed so the recogniser hears each sentence on its own and no longer gets stuck repeating a line and skipping verses.
- REMOVE LYRICS on the Generate Lyrics page takes the lyrics, title and notes off the page (Undo brings them back; what you saved to the DAW stays in RECENT).
- Visualiser: the bars are back to following the music (quiet music gives short bars, loud music tall ones) instead of sitting near the top all the time, and each blast is as big as the hit that caused it, so soft hits give small blasts.

## 3.30.2  TIMES USED for every file type, trim on Generate Lyrics

- TIMES USED now has a card for every file type: MP3, MP4, OPUS, WAV, M4A, FLAC and MKV (the quality does not matter, only the type of file), plus the total for all of them and what you made on this PC. Every finished conversion counts as its own type, and the website keeps one counter for each, with no upper limit. A number too long for its card is shown short (1.2e29) with the whole number in the tooltip.
- The TIMES USED button moved over next to Changelog (it no longer sits near the Purphex logo).
- Generate Lyrics has TRIM START and TRIM END boxes (like 0:30 or 1:02:03). When the words are written by listening, only that part of the song is heard, so a verse or a chorus can be done on its own. A wrong time or an end before the start is refused with the reason.

## 3.30.1  Photo themes that really recolour the program, instant crackle recovery, a clear theme hover

- Photo themes now work properly: PHOTO in the Themes box turns a picture into a theme of its own. The purple parts of the program (buttons, panels, outlines) take the real colours of the picture (up to four of them, so a sunset gives orange, pink and blue), and the picture fills the window, cropped to fit and never stretched. The theme you had is left alone, and REVERT puts it back.
- The theme under the mouse in the Themes box now has a bright outline with a soft ring round it, so it is clear which one a click will pick (the old hover never lit up).
- Crackle recovery: the moment the sound breaks up (two starved buffers within two seconds, or the sound card stopping for a third of a second) the audio output is refreshed. It takes a split second, then the sound is clean again. Each refresh also makes the playback buffer a step bigger for the session (your choice in Settings is kept), and at the biggest buffer it is a plain refresh, at most once every second and a half.

## 3.30.0  Generate lyrics from songs, videos and links, TIMES USED, and a tidier studio

- Generate Lyrics: a new GENERATE LYRICS button on the first screen (and in the Menu) opens a page of its own, free in every edition. Drop or choose any song or video (mp3, wav, flac, m4a, ogg, opus, aac, mp4, mkv, webm, mov and more) and press GENERATE LYRICS. On the Fantasy edition the page wears the Fantasy gold and follows your Fantasy theme.
- Where the words come from: the song's tags and file name find it in the LRCLIB lyrics database. A song that is not there can be written by listening to it with the Whisper speech recogniser, which runs on your PC. Purphex asks before downloading it (about 160 MB, once) and your song is never uploaded. A recogniser can mishear a word, so check the words against the song.
- Correct grammar and punctuation: capital letters, I, the apostrophes in don't and I'm, spacing round commas, a full stop or question mark at the end of each verse, a comma before and / but / so lines, clean verses, and [Verse] and [Chorus] labels. FIX GRAMMAR does the same to words you typed, and UNDO brings your words back.
- Resize lyrics: A- and A+ change the writing size, and FIT TO BOX makes it as big as it can be while the whole lyric fits in the box (a very long song stops at a readable size and scrolls).
- Keep it: COPY, SAVE .TXT, or SAVE TO THE DAW under a TITLE so you know which song it was (it starts as Artist - Title). RECENT on the page shows the last ones, newest first. In Purphex Music, the Lyrics page has a RECENT GENERATED button that puts one back into the writer, and a GENERATE LYRICS button.
- Visualiser: the effects were too large, so the ring bars, shells, kick ring, hexagon and shockwave are smaller and calmer, and the all-round rays are gone. Instead there are four separate blasts, each with its own bang (a wave, streaks and sparks): the bass bursts out of the bottom, the low mids out of the left, the mids out of the right and the highs out of the top.
- Generate Lyrics also takes links: paste a YouTube link (other sites need the link reader that the converter installs) and the lyrics are found from the video's name, with nothing downloaded and no MP3 needed. To write the words by listening, add the song or video file instead.
- TIMES USED: a new button on the first screen shows how many MP3s and MP4s have been made with Purphex Music all over the world, in full (1,234,567) and short (1.2M). It has no upper limit and goes into the millions and far beyond. Every finished conversion adds one; only that number is sent, never who you are, the link or the song, and Settings, General turns it off.
- The side menu of Purphex Music: Projects and Community are buttons of their own (Projects opens your saved projects and nothing else), Pages keeps Mix, Mastering and Lyrics, and the Community header has ARTISTS.
- AUTO MASTERING now asks Are you sure? before it starts, so a stray click cannot touch your work. Nothing changes until you press USE THIS, you can hear YOUR MIX (BEFORE) and any version (after), and a REVERT button stays next to AUTO MASTERING to put the master back exactly as it was.
- Project cards on the Community page have one solid PLAY button along the bottom (the duplicate Play in the hover tools is gone: Open is on its own line and the other tools sit in pairs).
- TIME MADE on your profile is now the time you spend in the DAW, counted live in seconds while you use it (it starts from the length of the projects you already made).
- Fantasy edition: the gold Fantasy theme (Royal Gold) goes on every button and outline of the program the first time it is on your PC. Choose any other theme afterwards in Themes.
- Help has a new topic, Generate lyrics from a song or video.

## 3.29.1  An even visualiser, a FULL VISUALISER button on the light show

- The visualiser is even. The ring of bars no longer turns (the loud low bars used to wander round it, so one side always looked smaller), neighbouring bars share their level, and every bar carries the same overall bounce as the logo, so the whole ring and the logo move together. The spectrum along the bottom is lifted towards the highs, so it is no longer tall on the left and flat on the right.
- A FULL VISUALISER button sits at the top right of the light show on the Mastering page and opens the visualiser on a page of its own (Esc comes back). The Master Visualiser entry is gone from the Pages menu, so there is one way to it, right where the light show is.
- The song position bar of the visualiser visualises the sound too, like the scroll bars: waves flow out of its logo along the bar, cyan to the left and pink to the right.

## 3.29.0  Lyrics saved with every song, in every edition

- Lyrics getter. After a song is converted, its lyrics are looked up (LRCLIB, a free community lyrics database) and saved as a .txt with the same name next to the song. The parts are labelled [Verse 1], [Chorus], [Pre-chorus], [Bridge] by the same labeller as the Lyrics page. It works in every edition, Free included, for MP3 and the other formats and for mastered files.
- Nothing wrong is ever saved: a song only gets lyrics when the title (and artist) match well, an instrumental or an unknown song gets none and the log says so, and an existing .txt is never overwritten. The file says the lyrics belong to their writers and are for your own use.
- Settings > General has a switch for it (on by default). Help has a new topic, Lyrics saved with each song (.txt).

## 3.28.3  Settings text is never cropped or written over

- Settings measures its explanations in the font they really get on your PC and gives each one the height it needs, moving everything under it down. Before, a long message on the Account page (for example about linking) could run into the buttons below or be cut off.

## 3.28.2  Linking a PC cannot get lost, Settings always fits the screen

- Linking a PC no longer gets lost. If the website made the link but could not give a licence yet, the PC now stays linked and the Account page says so (before, the website showed 1 of 3 PCs while the app said it was not linked). CHECK AGAIN asks the website again, and if the answer to the link never reached the app it can be collected again.
- Settings is never wider than the writing: the window cannot be made narrower than its pages (so text no longer runs off to the right behind a scroll bar), it always fits on your screen, and the explanations are brighter and easier to read.

## 3.28.1  Softer buttons in the studio, bigger writing in Settings, every label fits

- Softer studio buttons. Every button in the studio has rounder corners, a soft shade from top to bottom and a thin light on its top edge, like the buttons of the other windows. The glow, the hover and the colours of your theme are unchanged.
- Bigger writing in Settings: the explanations under each setting are about a tenth bigger, and the frame-rate list is wide enough to read in full.
- Every button label fits: a label that is too long for its button is written a little smaller instead of wrapping or being cut off (the CHANGELOG and BROWSE buttons of the main window showed this), and the two tick boxes under the trim boxes are wide enough for their words.

## 3.28.0  Rounded buttons in every window, edition codes, Help for editions

- Rounded buttons everywhere. Every ordinary window (Settings, Help, the converter, What's new, the update card, the preview windows) has the new button: rounded corners, a soft shade, a thin lit edge, a glow under the mouse and a press that sinks. The writing is white like on the other buttons and turns dark only on a light colour.
- Edition codes. Every paid edition has a code (PX-XXXX-XXXX-XXXX) that is linked to one place at a time: your website account or your Discord. Settings > Account has a new button, EDITION CODES: UNLINK OR REDEEM, that opens the website page where you see your codes, unlink one so it can be used on another account, or redeem a code. Editions given by the Purphex team cannot be moved.
- Help has a new topic, Editions, edition codes and linking this PC, that every edition can read: what each edition has, how to unlock a PC (and that one account can have 3), and how codes work.
- Cleaner inside: an old unused preset menu was removed, and the Settings mouse-wheel check no longer fails when another program has a window over it.

## 3.27.0  Fonts page with 190+ fonts, microphone modes, track off light, profile picture and banner controls, easier to read in every theme

- Fonts. A new FONTS button in the side menu opens a page with 190+ fonts for all the writing in the studio: drippy and horror, gothic, graffiti, neon, retro, script, handwritten, bold, serif and clean, plus the fonts that came with Windows. Every card is written in its own font; click one and the whole studio changes. A font is fetched from purphexmusic.com the first time and kept on this PC. Every font is resized by itself so its lines are as wide and as tall as the original (nothing is cut off or runs off a page: all of them were tested on every page at two window sizes), and fonts too wide to read at a fitting size are left out.
- Microphone modes. The MIC button has a neon box with a down arrow and a short menu: AUTOMATIC (rumble out, quiet room noise eased down), STANDARD (exactly as it is), VOICE ISOLATION (only your voice: spectral noise reduction and a gate that opens before each word) and WIDE SPECTRUM (the whole range and room, a little air on top). They only change what you record, the take stays exactly in time, and your choice is remembered.
- The neon light on a track header is a power switch. Press it and the track is OFF: it is silent (and ignored by solo, exports, mastering and the vocal sidechain), its header dims, and on the timeline its audio turns very translucent and shakes apart in an animated earthquake of broken strips, gaps and cracks. Press it again to turn it back on. It is saved with the project.
- Your profile: any picture or banner of any size or shape is resized to fit its frame by itself, with ZOOM, LEFT / RIGHT and UP / DOWN buttons and sliders, FIT TO FRAME and FILL THE FRAME. The picture is a rounded square now and shows only on your Profile page and your public profile: every other circle in the studio is the Purphex logo and cannot be changed. The accent colour is a real colour palette (the nine swatches are gone). Up to 10 links and 10 genres wrap onto more lines instead of being cut off, emoji work in names and bios, YOUR PUBLISHED PROJECTS lists only what you published, and TIME MADE reads like 2h33m or 1d2h33m.
- Easier to read in every theme. Every piece of writing in the studio was checked against what is really behind it in all 24 themes: white writing on bright buttons (yellow, green and teal themes had it at 1.4 to 2.5 to 1) now turns dark when the button is light, bright theme colours can no longer become too bright for white writing, and the ruler, the Fantasy badge, the Mastering chain and the visualiser text were fixed. About 1,100 hard to read pieces are down to one that is right at the limit.
- Tidier: ADD EFFECT and PRESETS stay at the top of the plugin panel and the VOCAL STYLES button there is gone (it is still in the side menu under Tools); the VISUALISER button on the Mastering page is gone (the light show is on the page; the full page is Pages > Master Visualiser). The separation script is no longer left on disk as readable text.
- Bigger writing. All the small writing in the studio is up to a fifth bigger (the smallest the most) and is fitted to its box by itself: it only becomes smaller when a name would not fit, never smaller than it was before, and nothing is cut off or runs into the next thing. Checked on 347 screens in five window sizes and every theme.
- The Fonts page is only for choosing a font. The sample buttons and the music words are gone: your font is shown as a sentence, the alphabet, the numbers and the punctuation, with the search, the groups and the font cards under it.
- Presets in neon boxes. The master presets (Mastering page), the presets list of every plugin and the community presets list use the same neon box as the font cards: a glowing outline in the colours of your theme, a small tag (the group, BUILT-IN or MY PRESET), short text that never runs into the buttons, and the buttons at the side.
- The little arrow next to MIC now shows its hint when you point at it, like every other button.

## 3.26.1  Crash fixes and fewer duplicate buttons

- Fixed the grey Microsoft .NET Framework box that said DragDrop registration did not succeed. The studio window asks Windows to accept dropped files each time it is built; when Windows refused (after full screen, a theme change or another program holding it) the program crashed. Now it just carries on, and dropping files works again the next time the window is built.
- Any other problem inside Purphex no longer shows the grey .NET box either. It is written to crash.log (in %LocalAppData%\YtToMp3, next to settings.txt) with the time, the version and the details, the program keeps running, and one plain message says so (the same problem is not shown again for a minute). If something keeps happening, send that file on the Discord and it can be fixed.
- Fewer duplicate buttons. The Community page lost its left menu (Home, Artists and the others were already buttons at the top), the SEARCH ARTISTS button (ARTISTS is at the top) and the YOUR PROFILE card (PROFILE is at the top). MY PROJECTS, which was only reachable from that menu, is now a button in the Community header.
- HOME is only shown where there is no MIX button on the screen: it is gone from the Lyrics, Mastering and My Projects pages (MIX does the same). Mastering has one VISUALISER button (the top bar one; the copy on the picture is gone) and one original or mastered switch (the A and B buttons on the left; the picture only shows which one you hear).

## 3.26.0  Editions: Free, Supporter and Fantasy, padlocks, licence protection, privacy for your projects and profile, themes for each edition

- Purphex Music now has three editions. FREE is the MP3 and MP4 converter (links, search words, playlists, trim) and the vocal remover, for everyone, for ever. SUPPORTER (9.99 pounds a month, or 99.99 pounds a year which is 15 percent off) opens the whole studio: mixer, Auto Mix, mastering and the visualiser, plugins, sound packs, Bass Lab, Flex Pitch, themes, photo backgrounds, your profile and artists, and exports up to 192 kHz. FANTASY (449.99 pounds, once, for life) is everything in Supporter for ever, plus exports up to 384 kHz, themes and a border of its own and early builds. Everything is bought on purphexmusic.com: the app never takes a card.
- Padlocks. In the Free edition the PURPHEX MUSIC button, Hear, Normalize loudness, the formats other than MP3 and the MP4 videos, sample rates above 48 kHz, the Mastering and Plugins items of the menu and the Background and Theme pages of Settings are greyed out with a padlock. Clicking one explains what it needs and takes you to the editions on the website. Format and sample rate lists name the edition each entry needs.
- LINK THIS PC: Settings has a new ACCOUNT page. Press LINK THIS PC, sign in on the website, press Yes, link it, and this PC unlocks by itself. The token is kept on this PC encrypted for your Windows user, the app asks the website about your edition now and then, and it keeps working for a while without internet. UNLINK THIS PC forgets it.
- Themes for each edition: the Free edition has the original Purphex theme. Supporter has all the standard themes, the Rainbow and three of its own (Neon Tokyo, Aurora and Bubblegum). Fantasy adds five that only it has: Royal Gold, Nebula, Obsidian, Splatter and Dragon Fire. In the theme list each of these wears its edition name and the ones above your edition show a padlock.
- The Fantasy Edition has a badge and a border on its profile: a line running from pink to cyan with splatters and sparks around it, the same colours as the pointer over a song. It shows on your own profile and on the Artists page and every artist profile. The website decides who has it from the linked PC, so nobody can switch it on by themselves.
- Help only lists what your edition has: the Free edition keeps the converter, the vocal remover and the basics, and the studio guides appear with Supporter and Fantasy.
- Licence protection. Your edition is a signed licence that the website makes for your PC: it cannot be edited, invented or copied to another PC, it runs out after a few days unless the app renews it (so a cancelled plan or an unlinked PC stops working soon), turning the Windows clock back switches the paid features off, and a copy of the program that was changed is kept locked. An account can have up to 3 PCs linked at a time: unlink one on the website or in Settings to use another. If the licence ends while the studio is open, the studio warns you and closes.
- Privacy and your projects. Every project is private until you PUBLISH TO PROFILE: use the new person button on its card in My Projects (or its right-click menu), or the PUBLISH TO PROFILE buttons under YOUR PROJECTS on the Profile page (with PUBLISH ALL and MAKE ALL PRIVATE). Only the projects you publish appear on your profile, and only their name, tempo, length and tags, never the audio. The Profile page has a PRIVACY row: PRIVATE ACCOUNT (hidden from the Artists page and search), HIDE LOCATION and HIDE FOLLOWERS. A profile can now have up to 10 links and 10 genre tags.
- The side menu is tidier: File, Edit, Tools, Pages and View open lists, and Profile, Themes, Settings, Help and Exit are single buttons (Profile is a page of its own, not inside Community). The Community page keeps only its own links.
- Mastering: the visualiser is on the Mastering page in every window size now (it used to need full screen), and the song position bar under it is the same strip as the scroll bars, with the logo as its handle. The Community page no longer lets the preview bar cover your profile card.
- Themes: browsing themes quickly no longer leaves old colours on buttons. Every colour is rebuilt from its original colour each time, Mono can no longer leave a button grey for good, and the last theme you click always lands. Settings has one SAVE button (the one at the bottom). Eight themes are new: 3 only Supporter has and 5 only Fantasy has (so no two themes look alike, Obsidian, Dragon Fire and Bubblegum got their own colours).
- Downloads and tidying: converted files are saved in the Downloads folder of your PC by default (the old Music folder choice moves to Downloads, a folder you picked yourself stays), and exports start in Downloads, in a Purphex Music folder. Import a theme file is gone (themes come from the list, the community or a photo). The Community page has one GO TO YOUR PROFILE button and one copy of your picture, and the Artists page lost its second MY PROFILE button. The words in the Add your music to start box stay inside the dotted line at any window size.

## 3.25.0  Profiles and artists, a Mastering page that is the visualiser, themes that change everything, a much smoother studio, help that takes you there and sample rates up to 384 kHz

- The Mastering page is the visualiser now. The light show sits on top of the page (a ring of bars around the logo, shells of light behind it, a circular scope that draws the real waveform, beads turning against each other, rays on the highs, hexagon pulses, sparks and rushing stars on every beat) and the EQ, effects and presets sit right under it, so you watch the music move while you master. FULL VISUALISER opens it as a page of its own. A window too short for both keeps the controls and gives you a VISUALISER button. The visualiser is also three times lighter to draw than before.
- Everything is smoother. Buttons and glows are drawn once and then copied, big panels are solid, the grid is copied into every track, pop-up boxes move by sliding a stored picture instead of redrawing the whole studio, the Rainbow theme no longer forces the window to redraw all the time, and the studio waits for itself less: it now draws as often as the screen can show.
- Themes really change everything. Every colour in the studio, the converter window, Settings, Help, the tour cards, the leaves and Bass Lab follows the theme: purples follow its violet, cyans its cyan and pinks its pink, so the MIX button, every other button, every box, every glow, the grid and the waveforms change together. A window that is already open recolours when you pick a theme, and even the title bar follows.
- PROFILE is a page of its own with a big editing system: banner (any picture, move it up or down), round picture, accent colour, name, bio, where you are, your genres (tick or add your own), and LINKS (any web address works: your website, YouTube, SoundCloud, Spotify, Instagram, a shop, even app links). You see how it looks while you edit it, with your numbers (projects, tracks, time made, followers) and your tracks with play buttons. PUBLISH MY PROFILE puts it in the community under a secret key made on this PC (there is no sign-in). Profile pictures are never buttons anywhere any more, and everything about editing a profile lives only on the PROFILE page. The Community page has GO TO YOUR PROFILE and SEARCH ARTISTS.
- ARTISTS: search the other producers by name or genre, sort them, open their profile (banner, picture, bio, links, tracks, followers), FOLLOW and UNFOLLOW them, and see who you follow on your profile. Three reports hide a profile.
- The Community page: a play button on every project card and the audio player bar like in My Projects, and CREATIVE STUDIO, MASTERING and LYRICS are gone from it.
- Help takes you there. SHOW ME now goes to the right page and opens the right box first (the AUTO MASTERING guide goes to the Mastering page, AUTO MIX to Mix, sound packs to the Sounds browser...), and every guide with buttons in it has a prompt for each one. A pop-up box, a menu or a sheet that is open now blocks the page behind it completely: nothing behind it can be clicked, scrolled or typed into.
- Settings has a sidebar of sections (Background, Theme, Animation, Audio, Updates, Community, General) and a SAVE button at the top and at the bottom (it used to say Done). The playback buffer you choose is now saved and kept: the studio only makes it bigger for the rest of the session if the sound breaks up, and never changes your saved choice.
- The highest sample rate possible: exports can be 44.1, 48, 88.2, 96, 176.4, 192, 352.8 or 384 kHz (WAV through a high quality Kaiser filter, FLAC through ffmpeg; MP3 stops at 48 kHz because that is as high as the format goes), and the converter window has the same rates, held to what each format can really hold.
- Smaller fixes: the Lyrics page always has its logo scroll bar. The Edit section of the side menu only shows on the Mix page. Everything in the Tools menu (Auto Mix, Split vocals, Sound packs...) takes you to the Mix page. The hint label is see-through, smaller, lower and waits a moment before it shows. Every PREVIEW has a STOP PREVIEW beside it. The Genre tags box fits its words and has your own tags. The count-in numbers are drawn as clean neon strokes. The REVERT bar keeps its words inside its box, counts down 60 seconds and presses the X for you. Flex Pitch has its own zoom, separate from the mixer. The duplicate Streaming master preset is gone.

## 3.24.0  The Master Visualiser, your own photo as a theme, neon borders in the menu, unique theme colours and a Space bar that pauses in My Projects

- The Master Visualiser: a page of its own for the music you are mastering. Open it with the VISUALISER button on the Mastering bar, the strip along the bottom of Mastering, or Menu > View > Master Visualiser. A ring of 96 light bars dances around the Purphex logo, which beats with your bass. A thin ring inside shows the sound before mastering. Every beat sends out a ring and a burst of sparks. A mirrored spectrum with peak dots runs along the bottom, left and right level meters stand at the sides, and cards show MOMENTARY and INTEGRATED loudness, TRUE PEAK and GAIN REDUCTION. At the top you can press PLAY, PREVIEW (the loudest 20 seconds on a loop with the mastering on) and switch HEAR: ORIGINAL / MASTERED while you watch. The song bar at the bottom jumps to any place when you click or drag it. Esc, BACK or MASTERING returns.
- A visualiser strip along the bottom of Mastering, the same idea as the preview bar in My Projects: a black strip with the logo and moving waves, a play and pause button, the time, and a click-to-jump song bar. It appears when the window is tall enough (740 pixels or more) and makes room for itself, so nothing is covered.
- Themes take any photo. IMPORT now opens a theme file or any picture of any size (a huge photo, a tiny one, a tall one): the picture becomes a theme and fills the whole studio window, cropped in the middle and never stretched. It keeps filling it when you minimise, come back or go fullscreen. PHOTO puts a picture behind any theme, NO PHOTO takes it off, SAVE keeps the picture next to the theme file so you can share both, and every theme card shows a small picture of its photo.
- The themes are really different now. Toxic, Crimson, Red, Orange, Yellow, Green, Teal, Blue, Indigo, Violet and Pink each have their own colours instead of being nearly the same (Toxic is a hot acid yellow-green and Green is a deep forest green, for example).
- Neon borders around everything in the side menu: the lists, the recent projects and the columns all glow in the colours of your theme.
- BACK only shows when it has somewhere useful to take you. If going back would only take you home, the button stays hidden (HOME is its own button).
- My Projects: Space now pauses and continues the preview (it used to jump into the search box). The search box only takes typing after you click it, and clicking anywhere else lets go of it. The preview bar has a pause button too.
- Settings: scrolling the mouse wheel over the Settings window now scrolls it instead of changing the setting under the pointer.

## 3.23.0  A Mastering tab of its own, AUTO MASTERING, 67 master presets, themes you can import and share, REVERT, HOME and BACK, scroll bars everywhere and a Help that shows you

- The Mastering tab is a little program of its own. The top bars show only what belongs to mastering: PLAY (its own play button), the time, PREVIEW MASTER, the master meter, and MIX / MASTERING / LYRICS, SAVE AS, SAVE, OPEN, FULLSCREEN, HELP and EXPORT. The track buttons are gone from it, so nothing can be pressed by accident.
- AUTO MASTERING (it used to be Analyze whole mix) listens to the whole mix, builds the EQ for you with no input, and opens ten versions to hear: Clean and Balanced, Warm and Wide, Loud and Punchy, Bass Boost, Vocal Forward, Bright and Airy, Radio Ready, Streaming Safe, Glue and Cohesion and Lo-Fi Warm. One of them is marked BEST FOR YOUR MIX. PREVIEW plays the loudest 20 seconds of your song on a loop with that version on (switch between them as often as you like), USE THIS keeps one, and CANCEL puts everything back.
- Master presets, 67 of them, in groups (Everyday, Loud, Genre, Vocals, Low end, Colour, Width, Fix): Bass Boost Master, Bass Boost Extra, Sub Bass Focus, Bass Tighten, Vocal Improver, Vocal Enhance Master, Vocal Warmth, Vocal Air, Rap Vocal Forward, Podcast, Audiobook, Trap, Drill, Hip-Hop, Pop, Rock, Metal, EDM, House, Dubstep, DnB, Phonk, R and B, K-Pop, Reggaeton, Afrobeat, Lo-Fi, Cinematic, Radio and many more. Press PREVIEW on any of them to hear it on your song straight away, then KEEP IT or PUT BACK. USE applies it and always offers REVERT.
- AUTO LOUDNESS measures the whole mix, picks the best level for that track (dynamic music keeps its dynamics, dense music goes louder, everything else gets the -14 LUFS streaming level, and a mix that is already louder is never turned down) and sets it for you. For the picky there is still the manual way: the slider and SET LOUDNESS (MANUAL).
- The EQ picture shows your sound moving: a bright live spectrum after the EQ, a faint one before it (in Mastering), and what the EQ changed lit up: green where it added sound, pink where it took sound away.
- REVERT: whenever you press USE on a preset, a style, a plugin chain, a master preset, a community preset, AUTO MASTERING, AUTO LOUDNESS or a theme, a REVERT bar appears at the bottom and puts everything back exactly as it was. The plugin Presets sheet also has its own REVERT that goes back to how the plugin was when you opened it.
- The plugin Presets button opens a proper list instead of a small menu: search it, scroll it with the logo scroll bar, press USE on any preset, delete your own, save yours or open Community presets. Every plugin has many more presets now (EQ alone has about 40: kick, snare, bass, vocals, telephone, lo-fi and more).
- Themes: 16 themes, one for every colour of the rainbow (Red, Orange, Yellow, Green, Teal, Blue, Indigo, Violet, Pink) plus a Rainbow theme that slowly turns through every colour. The Theme page lets you IMPORT A FILE (a theme is a short text file of colours, .purphextheme), SAVE THIS THEME to share it, and open COMMUNITY THEMES to download themes other people made or share your own. Every theme goes through the same clean-up: backgrounds stay dark and accents stay bright and readable, so a custom theme can never look tacky or unreadable.
- HOME and BACK: HOME goes to the main studio (Mix) and forgets where you have been. BACK steps back through the pages you went through, one at a time.
- Every page has the logo scroll bar when it has more to scroll: My Projects, the Recycling Bin, Community, the Mix tracks, the plugin panel and its parameters, Sounds, the packs list, the lyrics box, the presets lists, the AUTO MASTERING versions, the themes and Bass Lab lists. The logo rides along the bar, is the part you hold and drag, and the lit box is gone. Flex Pitch and the bars under the tracks work the same way and the preview bar moves its logo with the song.
- The Count In is always four clicks, one a second, whatever the tempo is, so it stays a proper countdown.
- Pointing at a button throws fewer, softer sparks that stay under the buttons around it, and there are new 144, 165 and 240 fps modes in Settings for fast screens. The animated logo behind Community and My Projects was made lighter so it turns smoothly.
- The zoom - and + buttons are gone (the ZOOM slider does it all).
- Lyrics: opening a text file puts its file name at the top as the title, and clearing all the words clears the title too.
- Help: the topic list no longer puts the pink arrows on top of the writing, the steps are laid out clearly, and every step that names a button has a SHOW ME: it finds the page the button is on, goes there and points at the real button with its hint. The guided tours now point exactly at the real buttons and their cards are lined up. Bass Lab has a round ? HELP button that walks you through the screen (presets, oscillators, filter, envelopes, LFOs, effects, keyboard) and a Bass Lab basics guide.

## 3.22.0  Search, sort and filter your projects, a Recycling Bin, BACK on every page, STEREO and MONO buttons and cleaner scroll bars

- My Projects has a search box at the top: just start typing and the cards narrow down as you type. It finds a project by name, tag or type (song, beat, vocals) and every word has to match. Esc clears it, Enter lets go of the box (so Space plays again).
- SORT puts the projects in the order you like: newest edited, recently opened, name A to Z or Z to A, most tracks, longest or oldest. TYPE shows only songs (vocals with a beat), beats (beats and instruments) or vocals. The star on every card makes a favourite and the FAVOURITES button shows only those. CLEAR resets everything, and the page tells you how many projects match (2 OF 9 PROJECTS).
- The RECYCLING BIN: when you remove a project and tick the box to delete its file, the project now goes into the RECYCLING BIN (a button on the projects page) instead of being deleted. It waits there for 30 days, with the days left shown, and is then deleted for good. RESTORE puts it back in My Projects (if its name is taken by another file it comes back under a new name, nothing is ever overwritten). DELETE FOREVER and EMPTY THE BIN delete at once and always ask first. Only the project file waits in the bin: your audio files are never touched.
- In the side menu My Projects (pictures) is now just My Projects.
- BACK is on every page now. Mastering, Lyrics, Community and My Projects all have a BACK button that takes you to the page you came from (the Mix page shows it too when you arrived from Community or My Projects), so you never have to go through the menu.
- The vertical scroll bars (Rapper Vocal Styles, Community presets, Add effect and the other lists) were redone: the glowing box is gone, the logo sits at the top of the bar and you hold the logo and drag it to scroll. The arrows step, click the bar to jump.
- Every track has a STEREO button with a separate MONO button underneath it (the one that is on is lit).
- Thin tracks are fixed: the buttons no longer pile up or fall out of the track. There is a new THIN TRACKS button next to UNDO MIX that switches between thin and normal tracks.
- AUTO MIX now asks Run Auto Mix? before it does anything (so a slip of the mouse does nothing), and you can cancel while it is working: everything goes back as it was.
- MIC asks Would you like to record now? (Yes arms it, then press PLAY to record). MIDI REC asks Would you like to record an instrument? before it switches on.
- The DETECT button is now DETECT BPM.
- Pointing at any button throws a few small neon sparks off it.
- Community and My Projects have the animated Purphex logo behind them, like the one on the website: a turning ring of light and a soft breathing glow. The useless With picture tab in Community is gone.
- Playback sound: if a project is too loud, the sound is now turned down smoothly just before the sound card instead of its peaks being chopped off flat. Chopped peaks are what you hear as fuzz and crackle. Normal projects are not touched at all.

## 3.21.0  Capitals and a bold font, STEREO / MONO, movable boxes, logo scroll strips, a reworked CUT and fades, a new projects page and colour themes

- All the writing is now in CAPITAL LETTERS in a bold, sharp font (Bahnschrift, which every Windows 10 and 11 PC has), in the studio, the main window, Settings, menus and dialogs. Long sentences keep normal letters so they stay easy to read. The main window was re-spaced so no writing touches a box or another label, and GRID SIZE is in capitals too.
- CUT now really cuts: click the piece you want gone, press CUT, and that piece is DELETED and the pieces after it move up to close the gap (SPLIT is the one that makes two pieces). It works on every piece lit by a selection box too, skips locked tracks, and Ctrl+Z puts it all back.
- Fades were reworked. The FADE IN and FADE OUT buttons are gone: click a clip and drag the round handles. The top handle sets how long the fade is; the new bottom handle moves where the fade starts (or where it ends), so you can leave silence before a fade in or after a fade out. Double-click a handle to remove or reset it. Fades follow a smooth S curve (no click at the start, no plunge at the end) and the picture on the clip is drawn from the very same curve, so what you see is what you hear.
- Every track has a STEREO / MONO button. MONO makes left and right the same sound; it is saved with the project.
- Pop-up boxes (Gain, the track pickers, New Track, Buses, Rapper Vocal Styles, Automation, Community presets, Add effect) can be dragged by their top strip to anywhere on the screen and stay where you put them.
- Lists now have a vertical scroll strip made like the one under the tracks: solid black, arrows at both ends, a lit window showing where you are and the logo in the middle to hold and drag. It is on Rapper Vocal Styles, Community presets, Add effect, New Track, Buses and the track pickers.
- The scroll strips are solid black so nothing behind them can flicker through. The strip under the tracks keeps its logo in the middle on every screen size, and the view no longer jumps back to the needle while music plays: scroll where you like, and it stays (click the ruler, press FIT or press PLAY again to follow the needle).
- Flex Pitch has the same logo scroll strip along its top (the waves come out of the logo) with the zoom slider from the editing row centred under it. The PITCH ZOOM buttons were removed.
- The transport: PLAY always says PLAY (it lights up while playing), MIC keeps its name and pulses red when it is ready (PLAY then starts the recording), and REC is now MIDI REC for recording from a MIDI keyboard.
- The Count In is now a glowing hexagon the size of the logo (the big circle is gone) with a neon glow that pulses on every beat and neon sparks that burst off its edges, like the sparks on the needle.
- A track that once held audio, has been emptied (its clip moved or deleted) and still has plugins switched on is removed by itself, with Ctrl+Z to bring it back. Tracks you made empty on purpose are never touched.
- Lyrics: Open .txt now puts [Verse 1], [Chorus], [Bridge] and so on in for you, and a new ADD SECTIONS button does the same for lyrics you are writing (parts you already labelled are left alone).
- A preset you save (a plugin preset, a plugin chain or a Bass Lab sound) is also shared to the Community presets automatically, so everyone can use it. A switch in Settings turns this off.
- The neon text boxes line up with their writing on every PC and window size, and typing boxes are fixed-size so the frame can never drift away from the text.
- The pointer (the needle) can only be moved from the ruler strip at the top, which now has a neon border so you can see where to grab it. The needle itself is a glowing neon beam and throws tiny neon sparks while it plays, like fat spitting in a pan. Clicking empty space in the tracks no longer moves it.
- Hold the left mouse button on empty space in the tracks and drag to draw a selection box: every piece it touches lights up, and CUT, DEL, LOCK and the other buttons work on all of them.
- Value boxes (the numbers on plugins and tracks) are neon boxes now, kept well apart from their names so you cannot click one by accident. Click anywhere else to confirm what you typed, press Enter to confirm, or press Esc to cancel.
- Every track works out its own tempo (BPM) by itself, whenever it gets sound, and shows it under the track name.
- Hint boxes and messages grow to fit all of their words instead of ending in dots, and every button has a hint now.
- The count in number is exactly in the middle of the hexagon (it is measured from the real shape of the digit, so it is centred on every PC).
- My Projects was reworked. Every project is a neon card with a square picture; if you have not chosen a picture the card shows the Purphex logo, and any picture you choose is cropped to fill the whole square (never stretched, never with empty bars). The same goes for your profile picture: it is the Purphex logo until you pick your own, and an X on it removes yours again.
- There is a BACK button on the projects pages (and on Community) and in the right-click menu of a project, so you never have to guess that MIX takes you back. The right-click menu has all the options: open, preview, rename, change or remove the picture, show in folder, remove from the list and BACK.
- Clicking a project now asks you to confirm before it opens, so you are never sent into a project by accident. The projects in the side menu Recent list ask too.
- REMOVE PICTURE (a button on each project and in its right-click menu) takes your picture off and brings the logo back.
- Removing a project asks what you want: take it off the list only, or also send the file to the Recycle Bin (so you can still get it back). Nothing is deleted for good.
- PREVIEW: press the play button on a project card to hear it without opening it. The bar along the bottom turns into a visualiser (logo in the middle, waves coming out of it) with the time, a STOP button and click to jump to a place in the song. Opening another page, pressing PLAY in the studio or pressing STOP ends it.
- THEMES: a new Theme tab in the side menu (and a Theme section in Settings) with six colour themes: Purphex, Ocean, Sunset, Toxic, Crimson and Mono. One choice recolours the glows, borders, buttons, the needle and the sparks in Purphex Music, Mastering and Bass Lab, and it is remembered next time.
- Side menu texts were shortened so none of them is ever cut off.

## 3.20.0  Clip tools, community presets and a recorder that waits for you

- Split a track as many times as you like: every piece stays on the same track. New CUT button (press it at the start and again at the end of a section to take that section out of every track and close the gap), FADE IN and FADE OUT with a round handle you drag to set the length, GAIN to set the volume of one piece, and LOCK to freeze a track so it cannot be moved, trimmed, split, cut or deleted until you unlock it.
- The two Trim to the needle buttons and the floating row of little buttons on a clip are gone. DEL (and the Delete key) now removes just the selected piece.
- Community presets: every Presets button now opens Community presets, a list of presets other producers shared (POPULAR, NEWEST, search). Press USE to try one, or SHARE MY CURRENT SETTINGS to give yours. It works for every plugin, for whole plugin chains and for Bass Lab sounds. Presets are checked before they are used, and a preset that several people report is hidden.
- Bass Boost now has ten balanced presets (Clean Low End, Subtle Lift, Warm and Round, Punchy Kick, Bass Guitar Body, Club Thump, Deep 808 Sub, Heavy Trap, Boom and Small Speakers). Each one is tested to raise the low end without clipping.
- Recording: MIC now arms the microphone (MIC READY) and nothing is recorded until you press PLAY, which says RECORD while armed. Count In comes first if it is on. The take goes onto the track you selected as another piece on that same track, instead of making a new track every time (a locked track is never recorded over).
- Count In played five clicks for four numbers, because the last audio block ran one click past the end of the bar. It now plays exactly four clicks at any tempo, and a big Purphex logo sits behind the number in the middle of the count-in circle.
- Clicking a button a second time now closes the list it opened (Grid Size, Presets and the other menus) instead of opening it again, and the GAIN button opens and closes its panel.
- Flex Pitch has a scroll bar along the top, and your edits are saved automatically a moment after every change, so nothing is lost if you click away, press FLEX again or close the app.
- A new slim bar along the bottom fits the grid: hold the left mouse button on the hexagon logo to scroll left and right, use the arrows at the ends, and while music plays the bar becomes a live visualiser with waves flowing out of the logo.
- Grid Size (renamed), METRONOME in capitals, a close x on the Split vocals + beat and Detect Track key pickers, and the grid lines no longer jump out huge and bright over the tracks when you change the grid size. The hint bar now sits above the bottom bar.
- New Track now has Beat / Instrumental, so a beat can live on its own track.
- FULLSCREEN / MINIMISE: the button says what it will do, and MINIMISE always returns to a normal window with its border, even after you maximised with the title bar button.
- Lyrics: new Open .txt button next to Save .txt to load lyrics from a text file (replace, or add to the end).
- Settings: new Scan for updates button that checks right now and always tells you the result, even for an update you pressed Later on.
- Select several clips: hold the left mouse button on empty space and drag a box. Every clip it touches lights up. Drag one to move them all together, press Delete (or DEL) to remove them all with one Ctrl+Z to bring them back, Esc to let go. The needle now moves only from the ruler at the top: clicking empty space no longer moves it.
- Hover hints and messages: every button now shows a hint when you point at it (a lot of them had none), and long hints and messages grow to show the whole sentence instead of being cut off with three dots. Messages stay on screen long enough to read them.
- The needle: the strip at the top that moves it now has its own neon border, and the needle is a glowing beam with a bright head. While music plays (or while you drag it) tiny neon sparks spit off the beam like fat in a hot pan.
- Typing a number into a box: click anywhere else to confirm it (Enter still works and Esc cancels and closes it). A box you open by accident and click off leaves the value exactly as it was. Values are now neon boxes of their own, spaced well away from the sliders and the words so you cannot click them by accident, and every text and number box in the app has the same neon look. Bass Lab knobs use a small neon box that also confirms when you click away.
- The tempo is found automatically for every track you add (nothing to press): each track header shows its BPM, and the first beat or song also sets the project tempo unless you set one yourself.
- Phone mode was removed (the website covers it now).

## 3.19.2  Rapper Vocal Styles: one simple Use button

- Rapper Vocal Styles now has a single Use button on each style (the second Add button is gone, it was confusing). Use puts the style on the selected track and replaces its plugins, and Ctrl+Z puts them back.

## 3.19.1  Text fits its boxes everywhere

- The progress box while splitting vocals now grows to fit its message, and shrinks the text if the window is too small, so nothing runs off the edge.
- Button labels that are too long for their button (for example SPLIT VOCALS + BEAT or AUTO MIX in a smaller window) now get a smaller size instead of being cut off.
- Plugin settings keep their full names: the A (automate) button moved to the right end of each row, and the long names in Worldwide Echo and Melody Tune were shortened (Reverb, Duck on voice, Send pattern, Tune speed, Humanise).
- Rapper Vocal Styles shows each style description in full (it wraps onto two lines), and the automation editor makes room for the names of a list of choices and uses tidy values on its side scale.

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

