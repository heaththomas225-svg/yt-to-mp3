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
    // the buttons a guide talks about, in the order it talks about them: {button, the words about it}
    public static List<string[]> Guide(HelpTopic t)
    {
        var l = new List<string[]>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in t.Body.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("## ")) continue; string lb = HelpForm.FindLabelPublic(line); if (lb == null || !seen.Add(lb)) continue;
            string text = line; if (text.Length > 2 && char.IsDigit(text[0]) && text[1] == '.') text = text.Substring(2).Trim(); else if (text.StartsWith("- ")) text = text.Substring(2); else if (text.StartsWith("TIP:")) text = "Tip: " + text.Substring(4).Trim(); else if (text.StartsWith("WARN:")) text = "Careful: " + text.Substring(5).Trim();
            if (text.Length > 230) { int cut = text.IndexOf(". ", 80); text = cut > 0 && cut < 230 ? text.Substring(0, cut + 1) : text.Substring(0, 227) + "..."; }
            l.Add(new[] { lb, text }); if (l.Count >= 9) break;
        }
        return l;
    }
    // a guide can be shown on screen when it points at the converter window by hand (those ids) or names buttons of the studio
    static readonly HashSet<string> ConverterGuides = new HashSet<string> { "start", "formats", "batch", "trim", "normalize", "vocal", "masterassistant", "download-video", "settings", "updates", "fullscreen" };
    public static bool HasGuide(HelpTopic t) { return t.HasTour || ConverterGuides.Contains(t.Id) || Guide(t).Count > 0; }
    public static readonly HelpTopic[] All = {
        new HelpTopic("start", "Basics", "Convert a link to MP3", true, @"
## What you need
A YouTube or other video / music link (or just words to search for).
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
Leave it on Original unless your project needs another rate. The list goes up to 384 kHz (MP3 and Opus stop at 48 kHz and M4A at 96 kHz, because that is as high as those formats go).
## File names
Every file is labelled with its format, for example 'Song [MP3 320k]', so you can keep track.
TIP: A 'mastered' tag in the name means the Purphex Mastering processed it."),


        new HelpTopic("batch", "Basics", "Several songs, playlists and search words", false, @"
## Many at once
Paste several links (one per line, or separated by commas or spaces). They are converted one after another and the progress bar shows which one is running.
## Playlists
Tick 'Whole playlists' to convert every video in a playlist link. Leave it off to convert only the one video in the link.
WARN: A link ending in list=LL is your Liked videos playlist. With 'Whole playlists' on, that converts all of them.
## Cancel
Press Cancel at any time. Files already finished are kept."),

        new HelpTopic("editions", "Basics", "Editions, edition codes and linking this PC", false, @"
## The editions
- Free: the MP3 and MP4 converter and the vocal remover, for everyone, for ever.
- Supporter: the whole studio while you pay each month or year.
- Fantasy: everything, for life, with every future update.
Nothing is paid inside the app: you choose an edition on the website, and the app unlocks.
## Unlock this PC
1. Press Settings, then Account, then LINK THIS PC.
2. The website opens: sign in, check the name of the PC and press Yes, link it.
3. The app unlocks by itself in a few seconds. One account can have 3 PCs linked: UNLINK THIS PC frees one.
## Your edition code
Every paid edition has a code like PX-ABCD-EFGH-JKLM. It is linked to ONE place at a time: your website account (or your Discord, with the /redeem command in the Purphex server). A code that is linked cannot be linked again.
- To see your codes, or to move one to another account: Settings, Account, EDITION CODES: UNLINK OR REDEEM. Press Unlink on the code, then sign in to the other account and redeem it there.
- Editions given by the Purphex team (admins and testers) cannot be moved.
TIP: Press Check again on the Account page if you just paid and the app still says Free."),

        new HelpTopic("lyrics-generator", "Basics", "Generate lyrics from a song or video", false, @"
## Where it is
On the first screen, press GENERATE LYRICS (next to Vocal remover and Purphex Music), or Menu, Generate lyrics. It is a page of its own, and it is free in every edition. On the Fantasy edition it wears the Fantasy gold and follows your Fantasy theme.
## Get the lyrics
1. Drop a song or a video on it, or press CHOOSE FILE. Any of mp3, wav, flac, m4a, ogg, opus, aac, mp4, mkv, webm, mov and more.
2. Or paste a link (YouTube; other sites need the link reader the converter installs) in OR PASTE A LINK: the lyrics are found from the video's name, with nothing downloaded and no MP3 needed.
3. Pick the LANGUAGE the song is sung in (Auto-detect lets the recogniser decide; pick it when it gets the language wrong, and it is remembered). Then press GENERATE LYRICS. The song's tags and file name (or the link) say which song it is, and the lyrics database (LRCLIB) is asked for it.
4. A song that is not in the database can be written by listening to it (this needs the file, not a link). TRIM START and TRIM END (like 0:30) pick the part of the song that is heard. That uses the Whisper speech recogniser, which runs on your PC: Purphex asks before it downloads it (about 160 MB, once, and your song is never uploaded). A recogniser can mishear a word, so check the words against the song.
## Grammar and punctuation
Every result is tidied: capital letters, I, the apostrophes in don't and I'm, spaces round commas, a full stop or question mark at the end of each verse, a comma before and / but / so lines, clean blank lines between verses, and [Verse] and [Chorus] labels. FIX GRAMMAR does the same to words you typed or changed, and UNDO brings your words back.
## Resize lyrics
A- and A+ change the size of the writing. FIT TO BOX makes it as big as it can be while the whole lyric still fits in the box (a very long song stops at a readable size and scrolls instead).
## Recheck
RECHECK VIDEO + WORDS listens to the song or video file again and compares what is sung with the lyrics in the box: it lists lines that are not heard, finds a verse that is sung but missing, and ADD THE MISSING PASSAGES puts it in the right place (marked to check). It needs the file, not a link, and the speech recogniser (the same one-time download). A recogniser can mishear a word, so it is a guide: listen to anything it flags.
## Remove
REMOVE LYRICS takes the lyrics, the title and the notes off the page. Undo brings them back, and what you saved to the DAW stays in RECENT.
## Keep it
COPY, SAVE .TXT, or SAVE TO THE DAW: give it a TITLE (it starts as Artist - Title) so you know which song it was. In Purphex Music, open Lyrics and press RECENT GENERATED to bring one back into the writer. RECENT on the Generate Lyrics page shows the last ones you saved, newest first (the last 30 are kept).
NOTE: Lyrics belong to their writers. The text is for your own use."),

        new HelpTopic("colour-mixer", "Basics", "Colour mixer (on your Profile page)", false, @"
## Where it is
On the Profile page, in a box of its own under Edit your profile (it is not a tab). Every section is in a neon-outlined box.
## Two sides
The LEFT palette is colour A and the RIGHT palette is colour B: A merges into B. Each side has its own STYLE drop bar, and clicking a colour sets that side (right-click puts it on the other side). The middle shows colour A, the merged colour and colour B, the MIX slider moves the merge point, and SWAP flips A and B. Below are the gradient from A to B, five steps (100% A, 75/25, 50/50, 25/75, 100% B), a colour wheel that turns slowly with arrows to A, B and the result, and pairs like Orange + Blue (click a pair to try it).
## Your profile colour
Whatever you change here IS your profile colour: the banner, the rings and the accents take the merged colour at once, and your name fades from A to B. PROFILE COLOUR = MY MIX turns that ON or OFF (OFF goes back to the colour of your theme) and FADE MY NAME A TO B turns the fade ON or OFF (OFF writes the name in the merged colour).
## Every style of colour
More than a thousand colours, each drawn in its own finish: Every shade (360 shades and greys), Classic gloss, Metallic, Metals, Brushed metals, Chrome and mirror, Chameleon (colour shifting: each one changes from one colour to another), Candy, Pearlescent, Holographic and iridescent, Glitter and sparkle, Carbon and tech, Satin, Matte, Frosted glass, Worn and weathered, Neon and glow, Basic, Pastels, Jewel tones, Earth and nature, Flowers, Wood and stone, Vintage and retro, Military and utility, Skin and hair, Sweets and drinks, Seasons, Sky and sea, Fire and sunset, Greys and blacks.
## Presets
SAVE COLOUR PRESET keeps the two colours, their finishes and the mix. PREV and NEXT go through the saved presets (each one is loaded and becomes your profile colour), you can click any saved one, and DELETE THIS PRESET removes the picked one. The mixer remembers where you left it.
## Theme and code
USE AS A THEME makes a theme for the whole program from your mix (the merged colour, A and B): REVERT puts your old theme back. COPY THE HEX CODE copies the code of the merged colour."),

        new HelpTopic("times-used", "Basics", "TIMES USED: worldwide MP3 and MP4 numbers", false, @"
## What it is
The TIMES USED button (next to Changelog on the first screen) shows how many conversions have been made with Purphex Music all over the world: a card for each file type (MP3, MP4, OPUS, WAV, M4A, FLAC and MKV: the quality does not matter), the total, and how many you made on this PC.
## No limit
The numbers have no upper limit. They are shown in full (1,234,567) and short (1.2M, 3.4B...) and keep going past billions and trillions.
## What is sent
Each finished conversion adds one to the worldwide number. Only that number is sent: never who you are, the link or the song. If you are offline the count waits and is sent later. Settings, General, Add my conversions to the worldwide TIMES USED number, turns it off (it is still counted on this PC)."),

        new HelpTopic("lyrics-getter", "Basics", "Lyrics saved with each song (.txt)", false, @"
## What it does
After a song is converted, Purphex looks up its lyrics and saves them as a text file with the same name next to the song, for example 'Artist - Title [MP3 320k].txt'. It is in every edition, Free included.
## What is in the file
The title, the artist and the lyrics with the parts labelled: [Verse 1], [Chorus], [Pre-chorus], [Bridge]... The labels are a good guess made from the shape of the lyrics (a part that comes back is the chorus), so change any that are wrong.
## Where the lyrics come from
LRCLIB (lrclib.net), a free community lyrics database, looked up by the artist and title in the file name. Use the File name list so the name is 'Artist - Title': that finds the right song most often. Nothing is saved when no lyrics match well enough (an instrumental, a brand new song, a different spelling): the log says so, and a wrong song's lyrics are never saved.
## Turn it off
Settings, General, 'Save the lyrics (.txt) next to each song I convert'. An existing .txt is never overwritten.
NOTE: Lyrics belong to their writers. The file is for your own use. The Lyrics page of the studio is for writing your own and never writes lyrics for you."),

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
- The neon light at the top left of the header turns the whole track OFF: it goes quiet, its header dims, and on the timeline its audio turns very translucent and shakes apart like an earthquake. Press the light again to turn it back on. A track that is off is left out of exports and mastering too.
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
Click it in the list. Drag sliders, or click the neon box with the number to type an exact value: click anywhere else (or press Enter) to confirm it, press Esc to cancel. Bypass turns it off, Presets lists built-in and your own.
## Presets
Presets lets you load Melodic, Emotional, Technical, Aggressive, Clean Studio or Wide Atmospheric vocal chains, the instrumental chain, or a bus preset (Echo bus, AdLib bus, Reverb bus). The Buses sheet has a Presets button for each bus. Every plugin stays editable. Save your own chain with 'Save this track's chain'.
## Community presets
Every Presets button (on a plugin, and the track Presets button for whole chains) starts with Community presets. A list opens with presets that other producers shared: POPULAR shows the most used first, NEWEST the latest, and you can type to search. Press USE to try one, press the x or Esc to close. Bass Lab has a Community style in its preset browser too.
Press SHARE MY CURRENT SETTINGS to give your own sound to everyone (you choose the name; your producer name is shown as the author). Share only sounds you made. If something does not belong there, press Report: a preset that several people report is hidden.
## Bass Boost presets
Bass Boost comes with ten ready-made presets that are already balanced so the low end grows without clipping: Clean Low End, Subtle Lift, Warm and Round, Punchy Kick, Bass Guitar Body, Club Thump, Deep 808 Sub, Heavy Trap, Boom, and Small Speakers (adds harmonics so the bass can be heard on laptop speakers).
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
3. Press AUTO MIX and say Yes when it asks (a slip of the mouse does nothing; you can also cancel while it works). It analyses loudness, clipping and tone, builds a chain for each track, balances the volumes and shows exactly what it changed.
4. Edit anything afterwards, or press Undo Auto Mix.
TIP: It never changes your original files."),

        new HelpTopic("studio-master", "Purphex Music", "Mastering tab: AUTO MASTERING, presets and loudness", false, @"
## The quick way
1. Open the MASTERING tab and press AUTO MASTERING. It listens to the whole mix, makes the EQ for you and shows ten versions.
2. Press PREVIEW on a version to hear it on the loudest part of your song (it loops). Switch between versions as often as you like.
3. Press USE THIS on the one you like. REVERT (the bar at the bottom) or CANCEL puts everything back.
4. Press AUTO LOUDNESS: it picks the best level for this track and sets it.
## The picky way
- Open PRESETS in the middle: over 60 master presets in groups (Everyday, Loud, Genre, Vocals, Low end, Colour, Width, Fix), including Bass Boost Master and Vocal Improver. Press PREVIEW to hear one on your song, then KEEP IT or PUT BACK.
- EQ & EFFECTS shows the selected effect of the chain. The EQ picture shows your sound before and after, and what the EQ changed.
- Set your own loudness with the slider and SET LOUDNESS (MANUAL). A ORIGINAL and B MASTERED compare with loudness match, so you judge tone, not volume.
- PLAY and PREVIEW MASTER are at the top. Only the buttons that belong to mastering are there.
## Watch your music
The light show is on the Mastering page itself: light bars dance around the logo, rings and sparks fly on every beat. To see it as a page of its own, press FULL VISUALISER at the top right of the light show; there you can switch between ORIGINAL and MASTERED while you watch (on the Mastering page use A ORIGINAL and B MASTERED on the left). Esc goes back.
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
Press Settings (top right of the main window) or Menu, then Settings. The sections are listed down the left side (Background, Theme, Animation, Audio, Updates, Community, General). Everything you change applies at once. SAVE (at the top and at the bottom) writes it down and closes the window.
## Background
Choose any picture or video. It is cropped to fill the window automatically. Darken makes it easier to read. 'Keep the leaves' puts the floating leaves on top. Remove puts the purple leaves back.
## Animation
Frame rate limit: 60 is smoothest, 30 is lightest on a slow computer. You can show the frame rate in the title bar.
## Updates
Scan for updates checks for a new version right now and always tells you what it found (you are up to date, or an update is ready and its window opens). It even shows an update you pressed Later on before.
## Audio
Volume and output device only change what you hear in Purphex Music. Playback buffer: Low latency reacts fastest but can crackle on slow computers; Safe never crackles. Your choice is saved and kept (if the sound breaks up the studio makes its own buffer bigger for that session only). Exported files are never affected.
TIP: Videos need ffmpeg, which is installed the first time you convert something."),

        new HelpTopic("fullscreen", "Basics", "Full screen, moving windows and shortcuts", false, @"
## Full screen
Press F11 in the main window, Purphex Music, Settings or Help. Press Esc (or F11 again) to leave. In Purphex Music there is also a FULLSCREEN button that turns into MINIMISE: it always takes you back to a normal window with its border, even after you maximised with the title bar button.
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

        new HelpTopic("vst", "Advanced", "Use VST plugins and instruments", false, @"
## What works
64-bit VST2 plugins (.dll) and, new and experimental, VST3 plugins (.vst3): instruments (synths) and effects. 32-bit plugins are not supported. VST3 support was tested with a stand-in plugin only, so if a particular .vst3 will not load, use its VST2 version if it has one.
## Set it up
1. Menu, then Plugins and extensions. Plugins installed in the usual VST2 and VST3 folders are listed automatically (or copy the .dll into the plugins folder).
2. Tick the plugin. It loads at once (a big one can take a few seconds the first time).
## Use an instrument
1. In Purphex Music press + ADD TRACK, then Virtual Instruments, and choose it. A track and the plugin's own window appear. (Bass Lab, the built-in synthesizer, is listed too and needs no setup.)
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
1. Put the needle where you want the cut by clicking or dragging on the ruler (the numbers at the top of the tracks): the needle only moves from there. Clicking a track never moves it.
2. Press SPLIT (or the key B). The track becomes two pieces on the SAME track. Split as many times as you like: there is no limit. Drag a piece to move it, or select it to give it its own fade, gain or plugins.
## Shorten a track (trim)
- Drag the left or right END of a clip to shorten it. The audio is never deleted, you can drag it back out.
## Take a section out (CUT)
1. SPLIT the track where the part you do not want starts and where it ends, so it is a piece of its own.
2. Click that piece and press CUT. The piece is deleted and the pieces after it move up to close the gap. (SPLIT makes two pieces; CUT removes one.) It also cuts every clip lit by a selection box, leaves locked tracks alone, and Ctrl+Z puts it back.
## Fade in and fade out
Click a clip: round handles appear on it. Drag the top handle at the top left to fade in (the farther you drag it, the longer the fade) and the top handle at the top right to fade out. The small bottom handle on a fade moves where the fade starts (or ends), which leaves silence before a fade in or after a fade out. Double-click a handle to remove or reset it. The curve drawn on the clip is exactly the curve you hear.
## Gain (volume of one piece)
Click a piece and press GAIN. A small panel opens with a slider from -30 to +12 dB that only changes that piece, on top of the track volume. Press GAIN again (or the x) to close it. Reset to 0 puts it back.
## Lock a track in place
Press LOCK to lock the selected track: it cannot be moved, trimmed, split, cut or deleted until you press UNLOCK, so you cannot nudge it by accident. A locked track is also left alone by CUT.
## Select several clips
Hold the left mouse button on empty space (between or below the tracks) and drag a box. Every clip the box touches lights up. Drag any lit clip to move them all together, press Delete (or DEL) to remove them, and press Esc or click empty space to let go. Locked tracks do not move or delete.
## Moving the boxes
Pop-up boxes (Gain, track pickers, New Track, Buses, Rapper Vocal Styles, Automation, Community presets, Add effect) have a strip along their top: drag it to put the box anywhere you like.
## Long lists
Lists have a vertical scroll strip like the one under the tracks: hold the logo and drag, click the arrows at the ends, or click along the strip.
## The needle
The needle (the glowing line) moves only from the strip at the top of the tracks, which has a neon border so you can see where to grab it: click or drag there. While music plays, small neon sparks spit off the beam.
## Move a clip
Drag the clip anywhere left or right (the cursor turns into a move cross). A plain click just selects the clip: the needle stays where it is.
## Stereo or mono
Every track has a STEREO button with a MONO button under it, next to its Bus button. STEREO keeps left and right apart; MONO (the button lights up) makes both sides the same sound, which is handy for a voice or a bass, or to check how your mix sounds on a single speaker. It is saved with the project.
## Move a track up or down
Drag the track name on the left up or down, or use the Up and Dn buttons on the selected track.
## More
- Point at any button, clip end or clip to see what it does.
- Right-click a track for split, cut, fades, gain, lock, duplicate, rename and delete.
- DUP copies the selected track. DEL (or the Delete key) deletes the selected piece; if the track has more pieces it keeps them. UNDO / REDO (Ctrl+Z / Ctrl+Y) bring edits back.
- Clicking a button again closes the list or panel it opened.
## Tempo and grid
- BPM: the tempo is found by itself for every track you add (each track shows its BPM under its name, and the first beat or song sets the project tempo). To use your own, click the number and type it (a tempo you typed is never changed automatically), or press DETECT to listen to the selected track again.
- Moves, trims and cuts snap to the beat. The Grid Size button picks how fine the snapping is (bar, 1/2 bar, beat, 1/8 note, 1/16 note); the grid lines brighten a little when you change it and while you drag. Snap can be switched off in the Menu.
- Detect Track key listens to the selected track and shows its musical key in a box on the screen.
- COUNT IN gives exactly four clicks (1 2 3 4, shown big on screen over the Purphex logo) before the song plays or records. METRONOME keeps a click going on every beat while it plays.
- The tempo also drives the synth's tempo-synced wobbles and delays.
## Zoom
Mouse wheel + Ctrl zooms, Alt + wheel makes tracks taller, or use - + FIT. After FIT the song fills the track area by itself, even in full screen. See the neon look page."),

        new HelpTopic("basslab-basics", "Music Studio", "Bass Lab: the basics in two minutes", false, @"
## The quickest way
Press the round ? button (HELP) at the top right of Bass Lab. A short tour points at each part of the screen and tells you what it does.
## Make your first sound
1. Click the preset name at the top (or press the < > buttons) and pick any sound. Starting from a sound you like is much easier than starting from nothing.
2. Play it: click the piano keys at the bottom, or use A S D F G H J on your computer keyboard (Z and X change the octave). A MIDI keyboard works too (choose it on the GLOBAL page).
3. Make it darker or brighter: turn CUTOFF on FILTER 1 down or up.
4. Make it fatter: turn UNISON up on OSC A (more voices stacked on top of each other).
5. Make it wobble: open the LFO 1 tab at the bottom, press Filter 1 cutoff under WOBBLE IT and turn AMOUNT up.
6. Add weight: switch on BASS BOOST at the top and turn BOOST up.
7. Keep it: open the preset list and press SAVE AS.
TIP: Every knob: drag up and down, mouse wheel for small steps, double-click to type a number, right-click to put it back.
TIP: PANIC (at the top) stops every note if something keeps ringing.
TIP: The other Bass Lab pages explain each part in more detail."),

        new HelpTopic("basslab", "Music Studio", "Bass Lab: make bass and wobbles (the built-in synth)", false, @"
## Open it
In Purphex Music press + ADD TRACK, then MIDI Track. A track and the Bass Lab window open. Play with your keyboard (A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave) or click the piano.
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
1. Paste a link. It can be YouTube or almost any video or music page (1000+ sites). A direct link to a video file works too.
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

        new HelpTopic("studio-automation", "Purphex Music", "Automation: make a plugin setting move with the song", false, @"
## What it is
Automation lets any setting of a plugin follow a curve while the song plays: an echo that opens up for the chorus, a filter that sweeps, a vocal that fades in. It is also used in every export.
## Starting a lane
Select the track, click the plugin in the panel on the right, and press the small A button next to the setting you want to move. The automation editor opens with a flat line at the setting's current value.
## Editing the curve
- Click an empty spot to add a point. Drag a point to move it along the song and up or down.
- Right-click a point to delete it. Press Flatten to set every point to the plugin's current value.
- The white line is the playhead, and the box at the top right shows what the setting is right now.
- For settings that are a list of choices (for example Off / On) the curve changes in steps.
## Turning it off
Press Automation on to switch the lane off, and the plugin goes back to the value you set by hand. Remove deletes the lane. The A button lights up on settings that have a lane, and the chips along the top of the editor switch between all the lanes on the track.
## Good to know
- Lanes are saved in the project. Ctrl+Z undoes changes to them.
- While a lane is on, the setting follows it: moving the slider by hand will be overridden. Switch the lane off to play with the slider.
- Before the first point and after the last point the setting holds that point's value.
"),
        new HelpTopic("studio-styles", "Purphex Music", "Rapper Vocal Styles: ready-made rap vocal chains", false, @"
## What it is
Rapper Vocal Styles is a searchable list of 36 original plugin chains for rap vocals, grouped by subgenre: Trap, Melodic Trap, Drill, Boom Bap, Old School, Grime and UK, Cloud and Lo-Fi, Rage and Hyper, Pop Rap, Phonk and Storytelling. Each one is made from this app's own plugins (tuning, EQ, compression, de-essing, saturation, echo, reverb) with settings chosen for that sound. No artist's settings are copied.
## Opening it
Select the vocal track, then press Presets... at the top of the plugin panel (right side) and choose Rapper Vocal Styles, or use the side menu: Tools > Rapper Vocal Styles.
## Using it
- Type to search by name, subgenre or description (for example drill, dark, warm, echo).
- Press a subgenre button to show only that kind, or Favourites to see the ones you starred.
- Press Use to put the style on the selected track. It replaces the plugins that are on the track, and Ctrl+Z puts them back.
- Press the star to keep a style as a favourite.
- Every plugin stays fully editable afterwards, and Ctrl+Z undoes the change.
## Tips
- Use Split vocals + beat first so the style is applied to a clean vocal.
- Start with the style closest to the song, then adjust the tuning key and the echo level.
"),
        new HelpTopic("studio-worldecho", "Purphex Music", "Worldwide Echo: throw your voice around the world", false, @"
## What it is
Worldwide Echo is a wide echo that locks to your project tempo. Its repeats bounce left and right, can melt into reverb, get darker or brighter each time round, and duck out of the way while you are singing.
## The Send pattern
Send pattern decides WHEN the echo hears you. All the time is a normal echo. The other choices open the Send only at certain moments of the song, for example the last beat of every bar (the classic end-of-line throw), the second half of each bar (ad-libs echo, the verse stays clean) or the off-beats. The repeats keep ringing after the Send closes. Send level is how much goes in, and Send smoothing is how quickly it opens and closes.
## The versions
- Worldwide Echo: wide ping-pong with a little reverb.
- Stadium Echo: huge slow repeats in a big wash of reverb.
- Rap Ad-Lib Echo: bouncy dotted-eighth repeats on the second half of each bar.
- Dark Echo: dim, warm repeats.
- Airy Echo: bright, floating repeats.
- Telephone Throw: only the last beat of each bar goes into a thin phone-line echo.
- Infinite Space: near-endless repeats that melt into reverb.
## Tips
- Keep Original sound at 100 and use Echo level to set how loud the echo is.
- Turn Duck on voice up so the echo only fills the gaps between words.
- Time follows the project tempo: set the tempo first.
"),
        new HelpTopic("studio-melody", "Purphex Music", "Melody Tune: pitch correction and harmonies", false, @"
## What it does
Melody Tune moves a sung or played note to the nearest note of the key and scale you choose, then can add harmony voices that follow the tuned note.
## Tuning
- Key and Scale: the notes the voice is allowed to land on.
- Tune speed: 0 snaps from note to note instantly (the hard-tuned sound). Higher values glide more slowly and sound more natural.
- Strength: how far the note is pulled. 100 puts it exactly on the note, lower values leave some of the original.
- Humanise: keeps the singer's natural vibrato and slides.
- Voice range: pick the range that matches the singer so the pitch is found reliably.
## Melodic layer
Choose Third above, Fifth above, Octave above or below, Power, Choir or Thick double. Layer level sets how loud the extra voices are and Layer width spreads them left and right. The intervals follow the character of your scale (major or minor), they do not change note by note.
## The versions
Melody Tune, Natural Polish, Hard Tune, Trap Melody, Soft Harmony, Choir Stack, Octave Dream and Power Lead are ready-made starting points.
## Tips
- Works best on one clean voice: use Split vocals + beat first on a full song.
- The plugin adds a small delay (about 0.1 s). The studio lines the other tracks up for you.
"),
        new HelpTopic("studio-flex", "Purphex Music", "Flex Pitch: edit the notes of a vocal", false, @"
## What it does
Flex Pitch listens to a recording of one voice (or one instrument) and shows every note as a block on a piano-roll grid. Drag a block up or down to change that note's pitch. The track plays your edits straight away, and the length and timing stay exactly as they were.
## Opening it
Select the track, then press FLEX in the tool row (or Tools > Flex Pitch). The recording is analysed (a few seconds for a song-length take) and the timeline zooms to it. Flex Pitch works best on a single voice or instrument, not a full mix: use Split vocals + beat first.
## Scroll bar and saving
A scroll bar runs along the top of Flex Pitch: drag the lit part to move along the recording, or click to jump. Your edits are saved by themselves a moment after every change (the top line says when), so nothing is lost if you click away, press FLEX again or close the app.
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
Select a track and press + Add Effect (or use the master chain). The list now holds over two hundred named effects. Use the Category button to pick a group, or just type: telephone, demon, plate, tape echo, notch...
## What is in it
- Pitch & Voice: pitch shifters (up to four voices), formant shifter, octave up and down, doublers, harmonizers, unison, detune, pitch glide, hard tune and robot pitch.
- Telephone & Distortion: telephone, radio, walkie-talkie, megaphone, police radio, lo-fi, saturation (tube, tape, soft), overdrive, clippers, fuzz, bitcrusher, ring modulator, frequency shifter, comb filter, vocoder, talkbox, robot, demon and monster voices.
- Echo & Reverb: slapback, tape, analog, ping-pong, multi-tap, reverse, ducking and tempo-synced delays; room, hall, plate, chamber, spring, shimmer, gated, reverse and ambient reverbs; early reflections; convolution reverb (with your own impulse-response files).
- Vocal Tools: vocal rider, alignment, breath control, plosive reducer, vocal isolation, transient shaper, chopper, stutter and glitch vocal, freeze, reverse, stacking, harmonies, clarity enhancer and spectral repair.
- Creative: granular synth, delay and reverb, spectral freeze / blur / delay, wavefolder, FM, resonator banks, impulse-response loader, tape stop and vinyl stop, scratch, beat repeat and stutter edit, rhythmic gate and filter, auto-pan, tremolo, sidechain pump, envelope follower, and risers, impacts, sub drops, whooshes and textures that lock to your bars.
- Bass & EDM: wobble, LFO, acid, envelope and step filters, sub-bass generators, bass enhancer, multiband distortion, OTT-style dynamics, phaser, flangers (including barber-pole), amp and cabinet simulators, volume shaper, gate sequencer and the modulators.
- EQ & Clarity: graphic, channel, vintage and analog EQ, dynamic EQ, linear-phase EQ, tilt, shelves, notch, formant (vowel) filter, mid-side EQ, vocal presence, air, mud remover, harshness reducer, resonance suppressor, spectral shaper and automatic EQ.
## Good to know
- Every effect is real signal processing that runs on your computer. Several named effects can share one engine with different starting settings, and some chain two or three engines together.
- Parameters you do not need are hidden. Double-click a slider to go back to the starting value of that effect.
- Pitch and delay-based effects can add a small delay; Purphex lines the tracks up for you.
- The fixed-interval harmonizers add the notes you choose (they do not follow your key)."),

        new HelpTopic("studio-sounds", "Purphex Music", "Purphex Sounds: the sample browser and sound packs", false, @"
## Opening it
Press SOUNDS (next to MIC) or use the side menu: Tools > Sounds and sound packs. A panel opens on the right of the Mix page. Press it again, or the x, to close it.
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
Press the three-lines button at the top left of Purphex Music. Your picture and name are at the top. Five of the buttons open a list (File, Edit, Tools, Pages, View) and the others just take you there (Projects, Community, Profile, Themes, Fonts, Settings, Help, Exit).
- File: New Project, Recent Projects (the last ones you opened, with their pictures), Open, Save, Save as, Set project picture, Export.
- Edit (only on the Mix page): undo, redo, split, cut, lock, duplicate and delete.
- Tools: Auto Mix, split vocals and beat, detect BPM, Flex Pitch, Sounds and sound packs, Rapper Vocal Styles, add a track, metronome and snap to grid. They all take you to the Mix page first.
- Pages: Mix, Mastering and Lyrics. Projects and Community are buttons of their own (Projects opens your saved projects and nothing else; Community has ARTISTS in its header). Every page also has a BACK button that returns to the page you came from.
- View: fit the whole song, thin / normal / tall tracks, full screen, the FPS counter and the converter window.
- Profile: your own profile page (banner, picture, bio, links, privacy and your tracks).
- Fonts: the Fonts page, with 190+ fonts (drippy, graffiti, neon, gothic, retro, script, handwritten and more) for all the writing in the studio.
- Themes: pick a colour theme, or download one other people made (the Themes box has MY THEMES, COMMUNITY THEMES, PHOTO and SAVE; the theme under the mouse is outlined). PHOTO turns a picture into a theme: the colours of the program come from the picture and it fills the window. The whole program changes colour at once and remembers your choice. Settings has the same choice.
## My Projects
Every project you save or open is remembered here, newest first, as a card with a picture. Click a card to open it: Purphex asks you to confirm first. The BACK button (top of the page, and in the right-click menu) takes you back to the tracks. The play button on a card PREVIEWS the project: you hear it without opening it and the bar along the bottom becomes a visualiser (press its square STOP button, or click the bar to jump). A project with no picture shows the Purphex logo; any picture is cropped to fill the square. The camera button adds or changes the picture (choose a JPG, PNG, GIF or BMP: it is cropped to a square and kept inside the project file, so it travels with it). The crossed camera button (or Remove picture in the right-click menu) takes your picture off again. Right-click a card for everything: open, preview, rename, change or remove the picture, show in folder, remove from the list and BACK. The x asks whether to take the project off this list only (your file stays on your PC) or to delete the file too: it then goes into the RECYCLING BIN.
## Search, sort and filter
Type in the search box at the top of My Projects to find a project by name, tag or type (every word has to match; Esc clears, Enter lets go of the box). SORT chooses the order (newest edited, recently opened, name, most tracks, longest, oldest). TYPE shows only songs, beats or vocals. The star on a card makes it a favourite and FAVOURITES shows only those. CLEAR resets the search and every filter.
## The Recycling Bin
The RECYCLING BIN button (top right of My Projects) lists the projects you deleted. A project stays there for 30 days, with the days left shown, and is then deleted for good. RESTORE puts it back in My Projects (under a new name if another file has taken its name). DELETE FOREVER and EMPTY THE BIN delete at once and ask first. Only the project file is kept in the bin: the audio files it uses are never touched.
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
ADD SECTIONS puts labels like [Verse 1], [Chorus], [Pre-chorus] and [Bridge] on the parts of your lyrics that have none yet (a part that comes back is the chorus; parts you labelled yourself are left alone; Ctrl+Z takes it back). Open .txt loads lyrics from a text file (replace what you have or add it to the end) and adds those labels for you. Copy puts the lyrics on the clipboard. Save .txt writes a text file. Clear empties the page.
This page never writes or suggests lyrics for you: every word is yours."),

        new HelpTopic("studio-profile","Purphex Music", "Your profile, artists and following", false, @"
## Your profile
1. Press PROFILE at the top of the studio (or Profile in the side menu).
2. Press CHOOSE BANNER or CHOOSE PICTURE and pick any picture of any size or shape: it is resized to fit its frame by itself (the banner is wide, the picture is a rounded square). Then place it: ZOOM with the - and + buttons or the slider, LEFT / RIGHT and UP / DOWN with the arrow buttons or the sliders, FIT TO FRAME to see the whole picture, FILL THE FRAME to cover the frame completely.
3. The picture only shows on this page and on your public profile. Everywhere else in the studio the circle is the Purphex logo and cannot be changed.
4. The COLOUR MIXER (the box under the editing system) sets your profile colour: mix two colours and press USE AS MY PROFILE COLOUR; PROFILE COLOUR: THEME'S AGAIN goes back to the colour of your theme.
5. Press EDIT beside your name, bio, where you are, LINKS (up to 10, one on every line) and genres (up to 10). Any web address works as a link: your website, YouTube, SoundCloud, Spotify, Instagram, a shop. Emoji work in the name, bio and where you are.
6. YOUR PUBLISHED PROJECTS lists only the projects you published (use the person button on a project in My Projects, or PUBLISH ALL), with a play button each. TIME MADE reads like 45s, 2m33s, 2h33m, or 1d2h33m.
## Share it
1. Press PUBLISH MY PROFILE to put it in the community. It is shared under a secret key made on this PC (there is no sign-in). Press it again after you change something.
2. MAKE IT PRIVATE takes it out of the community again.
## Find other producers
1. Press ARTISTS at the top of the studio (or Artists in the side menu). Type a name or a genre, and use SORT or FOLLOWING to change the list.
2. Click an artist to open their profile: banner, bio, links, tracks and followers.
3. Press FOLLOW to follow them. The artists you follow show on your profile.
TIP: Profile pictures are never buttons. Everything about editing your profile is on the PROFILE page."),

        new HelpTopic("studio-fonts","Purphex Music", "Fonts: 190+ fonts for the studio", false, @"
## Pick a font
1. Open the side menu and press Fonts. The page shows the font in use with a sentence, the alphabet, the numbers and the punctuation written in it.
2. Search by name, or press a group: Drippy and horror, Gothic and blackletter, Graffiti and street, Neon and futuristic, Retro pixel and arcade, Script and calligraphy, Handwritten, Bold display, Elegant serif, Clean sans and mono, and On this PC (the fonts that came with Windows).
3. Every card is written in its own font. Click one and the whole studio is set in it. The font is fetched from purphexmusic.com the first time (a few seconds, once) and kept on this PC.
4. USE THE ORIGINAL FONT goes back to Bahnschrift.
## It always fits
Fonts are very different in width and height, so every font is resized by itself until its lines are as wide and as tall as the original, and any line that is still too long for its box shrinks to fit. Fonts that could not be made to fit neatly (too wide to read at that size) are not in the list.
TIP: The font changes the writing of the studio. The converter window and the pop-up windows keep their normal font. All the fonts are free, open-licence fonts (the notices are at purphexmusic.com/fonts/LICENSES.txt)."),

        new HelpTopic("studio-neon","Purphex Music", "The neon look, zooming and taller tracks", false, @"
## The look
Every track has its own glowing colour. The outline of its clip, the light strip beside it, the little LED and the level meter all use it, and the glow gets brighter with the volume.
## Move along the song and zoom
- Mouse wheel: when all your tracks fit on screen the wheel moves along the song; when you have more tracks than fit, it scrolls up and down (and Shift + wheel moves along).
- Ctrl + mouse wheel zooms in and out around the mouse. The zoom slider in the toolbar does the same (drag right to zoom in; double-click it to see the whole song).
- The slim bar along the bottom shows the whole song with a lit window for what you see. Drag the window to move, drag its ends to zoom, click outside it to jump, double-click it to fit the whole song. Hold the left mouse button on the hexagon logo in the middle and move left or right to scroll, or press and hold the arrows at either end. While music plays the bar turns into a live visualiser: the sound ripples out of the logo.
- Hold the middle mouse button and drag to move the view in any direction. On a touch pad, swipe sideways.
- Alt + mouse wheel makes the tracks taller or thinner.
- FIT in the toolbar shows the whole song again. After FIT the song fills the area by itself, including in full screen.
## Add a track exactly where you want it
Move the mouse to the line between two tracks (or above the first, or under the last) in the track list. A + ADD TRACK HERE button appears. Press it to add music or an instrument at that spot."),

        new HelpTopic("makesong", "Music Studio", "Record and make a song (instruments, microphone, piano roll)", false, @"
## Make music with an instrument
1. Press + ADD TRACK and choose MIDI Track (Bass Lab) or Virtual Instruments (any VST plugin). Play it with your computer keyboard (A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave) or click the piano.
2. To record your playing: press REC (it turns red), then play a key. The song starts by itself and your notes are recorded onto the track. Press STOP when you are done.
3. Open the Notes button on the track (or double-click the track) for the piano roll: draw notes by dragging on the grid, drag a note to move it, drag its right end to resize it, right-click to delete it. Everything you draw plays right away. 'Snap notes to grid' tidies your timing.
4. Set the tempo with BPM (or DETECT). Turn on COUNT IN for a 1 2 3 4 countdown (one a second, whatever the tempo) before it plays and METRONOME for a click on every beat. Everything lands on the beat by itself (snap).
## Record your voice or an instrument with a microphone
1. Plug in a microphone. Use headphones, so the beat is not picked up by the microphone. The little box with a down arrow beside MIC chooses the microphone mode (it only changes what you record): AUTOMATIC (a gentle clean-up for most rooms), STANDARD (the microphone exactly as it is), VOICE ISOLATION (only your voice: hiss and room noise taken down hard) and WIDE SPECTRUM (the full range and the whole room with a little air on top). The mode is remembered.
2. Click the track you want to record on, put the needle where you want to start and press MIC. The button turns red and says MIC READY: nothing is recorded yet.
3. Press PLAY (the button now says RECORD) or the space bar when you are ready. With COUNT IN on you get four clicks first, then the song plays while you record. Press MIC again, or STOP, to finish. Press MIC a second time before you start to cancel.
4. The take goes onto the track you selected, as another piece on that same track (an empty track takes it directly), lined up with the beat, and is saved in Music / yt2mp3 / Recordings. It never makes a new track by itself unless that track is locked. Choose which input to use in Settings, Recording input.
5. Not happy? Press UNDO, or select the piece and press DEL, and record again.
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
64-bit VST2 plugins are supported (see Use VST plugins and instruments). VST3 and 32-bit plugins are not. The sample plugin is for people who write their own effects."),

        new HelpTopic("long", "Advanced", "Long songs and DJ mixes", false, @"
- Loading streams the file, so even hour-long mixes load in seconds with a progress bar.
- Vocal separation processes long songs in overlapping pieces, so memory stays low. A time-left estimate is shown.
- Export streams to disk. WAV is limited to about 4 GB (roughly 3.5 hours at 24-bit); use FLAC or MP3 beyond that.
- After separating a long song its original track is unloaded from memory. Un-mute it and it loads back from disk.
TIP: Zoom out with Ctrl + wheel to see the whole mix."),

        new HelpTopic("updates", "Advanced", "Updates and what's new", false, @"
The app checks for new versions every few seconds while it is open. You can also press Scan for updates in Settings at any time. When one appears a card pops up with what is new: press Update now and it restarts by itself, or Later.
Menu, then What's new shows the full changelog. The title bar shows the current frame rate.
TIP: Your settings and projects are kept across updates."),

        new HelpTopic("trouble", "Advanced", "Troubleshooting", false, @"
## 'ffmpeg isn't installed yet'
Convert any link once. The first conversion installs the tools.
## A YouTube link gives a 403 error
Let the app finish its first-run setup (it installs a small JavaScript runtime). Try again; the downloader also updates itself.
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
        BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); Font = new Font("Segoe UI", 10f); Win.Dark(this); ShowInTaskbar = true;
        search = new TextBox { Left = 14, Top = 14, Width = 310, BackColor = TM.C(255, 8, 4, 16), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left }; Win.NeonBox(search);
        search.TextChanged += (s, e) => Fill(null); Controls.Add(search);
        Controls.Add(new Label { Text = "Search help (for example: phone, vocals, eq)", Left = 14, Top = 40, Width = 310, Height = 18, ForeColor = TM.C(255, 150, 130, 190), Font = new Font("Segoe UI", 8.5f) });
        list = new ListBox { Left = 14, Top = 64, Width = 310, Height = 520, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 28, BackColor = TM.C(255, 8, 4, 16), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        list.DrawItem += DrawItem; list.SelectedIndexChanged += (s, e) => ShowTopic(); Controls.Add(list); Win.DarkControl(list);
        view = new RichTextBox { Left = 338, Top = 14, Width = 608, Height = 570, ReadOnly = true, BackColor = TM.C(255, 8, 4, 16), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, DetectUrls = false, Cursor = Cursors.Arrow }; Win.NeonBox(view);
        Controls.Add(view); Win.DarkControl(view);
        view.MouseMove += (s2, e2) => { int k = LinkAt(e2.Location); view.Cursor = k >= 0 ? Cursors.Hand : Cursors.IBeam; };
        view.MouseClick += (s2, e2) => { int k = LinkAt(e2.Location); if (k >= 0) { string target = linkTargets[k]; Close(); runTour("target:" + target); } };
        show = new PxButton { Text = "Show me on screen", Left = 338, Top = 596, Width = 220, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 190, 60, 200), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Visible = false };
        show.FlatAppearance.BorderColor = TM.C(255, 220, 140, 255); show.Click += (s, e) => { var t = Current(); if (t != null) { Close(); runTour(t.Id); } }; Controls.Add(show);
        var close = new PxButton { Text = "Close", Left = 846, Top = 596, Width = 100, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        close.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); close.Click += (s, e) => Close(); Controls.Add(close);
        Fill(selectId);
    }

    HelpTopic Current() { return list.SelectedIndex >= 0 && list.SelectedIndex < shown.Count ? shown[list.SelectedIndex] : null; }

    void Fill(string selectId)
    {
        string q = search.Text.Trim().ToLowerInvariant(); shown.Clear(); list.Items.Clear(); int sel = 0;
        foreach (var t in HelpContent.All)
            if (Edition.HelpVisible(t.Id) && (q.Length == 0 || t.Title.ToLowerInvariant().Contains(q) || t.Body.ToLowerInvariant().Contains(q) || t.Category.ToLowerInvariant().Contains(q)))
            { if (selectId != null && t.Id == selectId) sel = shown.Count; shown.Add(t); list.Items.Add(t.Title); }
        if (shown.Count > 0) list.SelectedIndex = sel; else { view.Clear(); view.Text = "Nothing matches that. Try a shorter word."; show.Visible = false; }
    }

    void DrawItem(object s, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= shown.Count) return; bool sel = (e.State & DrawItemState.Selected) != 0; var t = shown[e.Index];
        using (var b = new SolidBrush(sel ? TM.C(255, 104, 48, 196) : TM.C(255, 8, 4, 16))) e.Graphics.FillRectangle(b, e.Bounds);
        bool first = e.Index == 0 || shown[e.Index - 1].Category != t.Category;
        TextRenderer.DrawText(e.Graphics, t.Title, Font, new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 12 - (HelpContent.HasGuide(t) ? 46 : 16), e.Bounds.Height), TM.C(255, 240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (HelpContent.HasGuide(t)) { var pill = new Rectangle(e.Bounds.Right - 38, e.Bounds.Y + 6, 30, e.Bounds.Height - 12); using (var br = new SolidBrush(TM.C(sel ? 120 : 70, 236, 92, 226))) e.Graphics.FillRectangle(br, pill); using (var pen = new Pen(TM.C(200, 236, 120, 230))) e.Graphics.DrawRectangle(pen, pill); TextRenderer.DrawText(e.Graphics, "▶", new Font("Segoe UI", 7.5f), pill, TM.C(255, 255, 200, 250), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter); }
        if (first) using (var pen = new Pen(TM.C(120, 190, 120, 255))) e.Graphics.DrawLine(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Right, e.Bounds.Y);
    }

    // the buttons the guides talk about: the words in the text, and the button they point at (SHOW ME finds it on screen, whatever page it is on)
    static readonly string[][] UiLabels = {
        new[] { "GO TO YOUR PROFILE", "PROFILE" }, new[] { "CHOOSE BANNER", "CHOOSE BANNER" }, new[] { "CHOOSE PICTURE", "CHOOSE PICTURE" }, new[] { "PUBLISH MY PROFILE", "PUBLISH MY PROFILE" }, new[] { "SEARCH ARTISTS", "ARTISTS" },
        new[] { "SPLIT VOCALS + BEAT", "SPLIT VOCALS + BEAT" }, new[] { "ADD MUSIC", "ADD MUSIC" }, new[] { "Add music", "ADD MUSIC" }, new[] { "AUTO MASTERING", "AUTO MASTERING" }, new[] { "AUTO LOUDNESS", "AUTO LOUDNESS" }, new[] { "PREVIEW MASTER", "PREVIEW MASTER" }, new[] { "AUTO MIX", "AUTO MIX" }, new[] { "UNDO MIX", "Undo mix" }, new[] { "Undo mix", "Undo mix" }, new[] { "Undo Auto Mix", "Undo mix" },
        new[] { "Detect Track key", "Detect Track key" }, new[] { "Detect key", "Detect Track key" }, new[] { "THIN TRACKS", "THIN TRACKS" }, new[] { "ADD EFFECT", "+ Add Effect" }, new[] { "Add Effect", "+ Add Effect" }, new[] { "Add effect", "+ Add Effect" },
        new[] { "RECYCLING BIN", "RECYCLING BIN" }, new[] { "ADD SECTIONS", "ADD SECTIONS" }, new[] { "COUNT IN", "COUNT IN" }, new[] { "METRONOME", "METRONOME" }, new[] { "GRID SIZE", "GRID SIZE" }, new[] { "DETECT BPM", "DETECT BPM" }, new[] { "MIDI REC", "MIDI REC" }, new[] { "EXPORT", "EXPORT" }, new[] { "SAVE AS", "Save as" }, new[] { "FULLSCREEN", "FULLSCREEN" },
        new[] { "PLAY", "PLAY" }, new[] { "STOP", "STOP" }, new[] { "LOOP", "LOOP" }, new[] { "MIC", "MIC" }, new[] { "SOUNDS", "SOUNDS" }, new[] { "LYRICS", "LYRICS" }, new[] { "MASTERING", "MASTERING" }, new[] { "FLEX", "FLEX" }, new[] { "SPLIT", "SPLIT" }, new[] { "CUT", "CUT" }, new[] { "GAIN", "GAIN" }, new[] { "LOCK", "LOCK" }, new[] { "DUP", "DUP" }, new[] { "FIT", "FIT" }, new[] { "STEREO", "STEREO" }, new[] { "MONO", "MONO" }, new[] { "UNDO", "UNDO" }, new[] { "REDO", "REDO" },
    };
    // the first button named in a line of the guide (null if none); a single capital word has to be written in capitals so ordinary words do not count
    internal static string FindLabelPublic(string line) { return FindLabel(line); }
    internal static string FindLabel(string line)
    {
        string best = null; int bestAt = int.MaxValue;
        foreach (var p in UiLabels)
        {
            bool multi = p[0].Contains(" ") || p[0].Contains("+"); int at = multi ? line.IndexOf(p[0], StringComparison.OrdinalIgnoreCase) : line.IndexOf(p[0], StringComparison.Ordinal); if (at < 0) continue;
            if (!multi) { bool lb = at == 0 || !char.IsLetterOrDigit(line[at - 1]), rb = at + p[0].Length >= line.Length || !char.IsLetterOrDigit(line[at + p[0].Length]); if (!lb || !rb) continue; }
            if (at < bestAt) { bestAt = at; best = p[1]; }
        }
        return best;
    }
    readonly List<int[]> links = new List<int[]>(); readonly List<string> linkTargets = new List<string>();
    void ShowTopic()
    {
        var t = Current(); if (t == null) return; view.Clear(); links.Clear(); linkTargets.Clear();
        Add(t.Category.ToUpperInvariant(), 9, true, TM.C(255, 236, 120, 230)); Add(t.Title, 20, true, TM.C(255, 225, 190, 255)); Add("", 6, false, Color.White);
        foreach (var raw in t.Body.Replace("\r\n", "\n").Split('\n'))
        {
            string l = raw.TrimEnd(); if (l.Trim().Length == 0) continue; string label = null;
            if (l.StartsWith("## ")) { Add("", 6, false, Color.White); Indent(0, 0); Add(l.Substring(3), 13, true, TM.C(255, 190, 140, 255)); Add("", 3, false, Color.White); continue; }
            Color col = TM.C(255, 240, 232, 255); string text = l;
            if (l.Length > 2 && char.IsDigit(l[0]) && l[1] == '.') { Indent(8, 26); label = FindLabel(l); AddStep(l.Substring(0, 2), l.Substring(2).TrimStart(), col, label); Add("", 4, false, Color.White); continue; }
            else if (l.StartsWith("- ")) { Indent(8, 18); label = FindLabel(l); AddStep("•", l.Substring(2), col, label); Add("", 3, false, Color.White); continue; }
            else if (l.StartsWith("TIP:")) { Indent(8, 0); text = "Tip: " + l.Substring(4).Trim(); col = Color.FromArgb(120, 230, 160); }
            else if (l.StartsWith("WARN:")) { Indent(8, 0); text = "Careful: " + l.Substring(5).Trim(); col = Color.FromArgb(255, 190, 90); }
            else { Indent(0, 0); col = TM.C(255, 215, 205, 235); label = FindLabel(l); }
            AddStep("", text, col, label); Add("", 3, false, Color.White);
        }
        Indent(0, 0); view.SelectionStart = 0; view.ScrollToCaret(); show.Visible = HelpContent.HasGuide(t) && canTour(t.Id);
    }
    void Indent(int first, int hang) { view.SelectionIndent = first; view.SelectionHangingIndent = hang; view.SelectionRightIndent = 10; }
    // one step: a pink number or bullet, the words, and (if the words name a button) a SHOW ME on its own at the end, never on top of the writing
    void AddStep(string num, string body, Color col, string label)
    {
        view.SelectionStart = view.TextLength; view.SelectionLength = 0;
        if (num.Length > 0) { view.SelectionFont = new Font("Segoe UI", 11f, FontStyle.Bold); view.SelectionColor = TM.C(255, 236, 120, 230); view.AppendText(num + "  "); }
        view.SelectionStart = view.TextLength; view.SelectionFont = new Font("Segoe UI", 11f, FontStyle.Regular); view.SelectionColor = col; view.AppendText(body);
        if (label != null)
        {
            view.SelectionStart = view.TextLength; view.SelectionFont = new Font("Segoe UI", 9f, FontStyle.Bold); view.SelectionColor = TM.C(255, 120, 230, 255); int at = view.TextLength; string tag = "    [ SHOW ME ]"; view.AppendText(tag); links.Add(new[] { at + 4, tag.Length - 4 }); linkTargets.Add(label);
        }
        view.SelectionStart = view.TextLength; view.SelectionFont = new Font("Segoe UI", 11f, FontStyle.Regular); view.SelectionColor = Color.White; view.AppendText("\n");
    }
    int LinkAt(Point p) { int i = view.GetCharIndexFromPosition(p); for (int k = 0; k < links.Count; k++) if (i >= links[k][0] && i < links[k][0] + links[k][1]) return k; return -1; }

    void Add(string text, float size, bool bold, Color c)
    {
        view.SelectionStart = view.TextLength; view.SelectionLength = 0; view.SelectionFont = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); view.SelectionColor = c; view.AppendText(text + "\n");
    }
}

// ----------------------------------------------------------------------
class TourStep { public Func<Rectangle> Target; public string Title, Text; public Action Enter; public TourStep(Func<Rectangle> t, string title, string text) { Target = t; Title = title; Text = text; } }

// Dims the owner window with a glowing hole around the control being explained (you can click through the hole), plus a Next / Back card.
class Tour
{
    readonly Form owner; readonly List<TourStep> steps; int idx; Form shade, card; System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer { Interval = 33 }; float t; Label title, text, count; Button back, next, skip;
    Rectangle holeScreen; bool holeOn = true; int entered = -1;

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
            if (holeOn) using (var p = Round(Rectangle.Inflate(h, 6, 6), 12))
            {
                using (var pen = new Pen(TM.C((int)(60 + 90 * pulse), 236, 92, 226), 14f)) g.DrawPath(pen, p);
                using (var pen = new Pen(TM.C(255, 245, 190, 255), 2.5f)) g.DrawPath(pen, p);
            }
        };
        card = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, BackColor = TM.C(255, 18, 8, 38), Size = new Size(430, 214), Owner = owner, KeyPreview = true };
        card.Paint += (s, e) => { using (var pen = new Pen(TM.C(220, 190, 110, 255), 2)) e.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3); };
        title = new Label { Left = 20, Top = 14, Width = 390, Height = 28, Font = new Font("Segoe UI Semibold", 13f), ForeColor = TM.C(255, 236, 150, 235), BackColor = card.BackColor };
        text = new Label { Left = 20, Top = 48, Width = 390, Height = 90, Font = new Font("Segoe UI", 10f), ForeColor = TM.C(255, 240, 232, 255), BackColor = card.BackColor };
        count = new Label { Left = 20, Top = 160, Width = 90, Height = 22, ForeColor = TM.C(255, 160, 140, 200), BackColor = card.BackColor };
        back = Mk("Back", 118, 154, 88, () => Go(-1)); next = Mk("Next", 214, 154, 96, () => Go(1)); skip = Mk("Close", 318, 154, 92, () => End());
        card.Controls.AddRange(new Control[] { title, text, count });
        card.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) End(); else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) Go(1); else if (e.KeyCode == Keys.Left) Go(-1); };
        owner.LocationChanged += Follow; owner.SizeChanged += Follow; owner.FormClosed += (s, e) => End();
        tm.Tick += (s, e) => { t += 0.033f; shade.Invalidate(); }; tm.Start();
        Show();
    }

    Button Mk(string txt, int x, int y, int w, Action a)
    {
        var b = new PxButton { Text = txt, Left = x, Top = y, Width = w, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); b.Click += (s, e) => a(); card.Controls.Add(b); return b;
    }

    void Follow(object s, EventArgs e) { Layout(); }
    void Go(int d) { idx += d; if (idx < 0) idx = 0; if (idx >= steps.Count) { End(); return; } Layout(); }

    void Show() { Layout(); shade.Show(owner); card.Show(owner); card.Focus(); }

    void Layout()
    {
        if (shade == null || shade.IsDisposed) return; var s = steps[idx];
        if (s.Enter != null && entered != idx) { entered = idx; try { s.Enter(); owner.Refresh(); } catch { } }   // a step can first take you to the right page or open the right box
        var cr = owner.RectangleToScreen(owner.ClientRectangle); shade.Bounds = cr;
        var tr = s.Target(); bool noHole = tr.IsEmpty; if (noHole) tr = new Rectangle(cr.Width / 2 - 2, cr.Height / 3, 4, 4); holeScreen = owner.RectangleToScreen(tr);
        var full = new Region(new Rectangle(0, 0, cr.Width, cr.Height)); if (!noHole) using (var p = Round(new Rectangle(holeScreen.X - cr.X - 2, holeScreen.Y - cr.Y - 2, holeScreen.Width + 4, holeScreen.Height + 4), 10)) full.Exclude(p); shade.Region = full; holeOn = !noHole;
        title.Text = s.Title; text.Text = s.Text; bool one = steps.Count == 1; count.Text = one ? "" : (idx + 1) + " of " + steps.Count; back.Visible = !one; next.Visible = !one; back.Enabled = idx > 0; next.Text = idx == steps.Count - 1 ? "Done" : "Next"; skip.Text = one ? "Got it" : "Close"; if (one) { skip.Left = 318; }
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
