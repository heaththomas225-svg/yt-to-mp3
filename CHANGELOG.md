# Changelog

What changed in each version of **Purphex**. The same notes are shown in the app (Menu > What's new) and in the update card.

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

