using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// What's new: shown from the "What's new" button and automatically once after an update.
// Keep CHANGELOG.md in the repo in sync with this text.
static class Changelog
{
    public const string Latest = "3.33.0";

    public const string Text =
@"3.33.0  Profile themes, whole pictures, links and tags rules, music videos, a place search, emoji for lyrics, saved songs, smoother sound
 + COLOUR MIXER: the white lines in the colour wheel are gone (it still turns). The merge is brighter and truer (orange and blue now pass through green and yellow instead of turning grey-brown). The gradient and the five steps are clickable: click or drag along the gradient, or click a step, to set the mix. Metals, chrome, brushed, pearlescent, holographic, chameleon and glitter now shine: a lit bevel and a sheen that slides across them in a wave, and glitter that twinkles, on the swatches, the three big circles, the gradient and the steps. Whatever you point at gets an outline. The COLOUR SWATCH VARIATIONS box is removed.
 + Every colour code in the mixer can be copied with a click, and pasted with a right-click (a code on the clipboard becomes colour A or B).
 + USE AS A THEME is now SAVE AS THEME: it adds the theme to a department of its own, THEMES then PROFILE THEMES, and nothing is switched on, so there is nothing to revert. That tab has a DELETE THEME button under your profile themes.
 + The pictures on your profile (banner and picture) now show the WHOLE picture, and the empty sides are filled by stretching the edges of the picture, so it never looks plain and nothing is cut off. Zoom in yourself if you want it cropped. What is published to the community is made the same way.
 + The text of a profile takes the colour of the profile (the colour you chose in the mixer), for you and for everyone who looks at it: the name fades from colour A to colour B on every profile, and the text, tags and buttons carry the colour.
 + Links on profiles can only be streaming services and social media (Spotify, Apple Music, YouTube, SoundCloud, Bandcamp, Tidal, Deezer, Instagram, TikTok, X, Facebook, Twitch, Discord, Reddit and more): no other websites. They are clickable, as buttons, and a LINKS button on a profile lists them all with the name of each service. Links written in a bio are made clickable too, and other web addresses in a bio are removed. The website checks the same rules.
 + Genre tags: as many as you like, in a box that scrolls with the neon bar of the DAW, and no swearing (a tag with a swear word is not kept). REMOVE TAGS on the profile removes all of them, only the ones you typed yourself, or one by one; the tag window has REMOVE MY OWN.
 + Your published projects are in a neon box of their own on your profile, and every published project can have a MUSIC VIDEO: a link (YouTube, Vimeo, TikTok...) that shows as a MUSIC VIDEO button on your profile.
 + WHERE YOU ARE is a search: type any country, county, town or area (Poole finds Poole, Dorset) and pick it from the list; you can change the words before you use them.
 + The Lyrics tab of the DAW is tidier: the GENERATE LYRICS and RECENT GENERATED buttons are gone from it (Generate Lyrics is on the first screen), and the colour wheel is just the wheel, with no A, B or M markers. The lyrics page has an EMOJIS button: a panel with hundreds of emojis in groups and a search by word (fire, heart, guitar, sad...); click one to put it in your lyrics at the cursor.
 + Community: SAVED SONGS and SHARE buttons. On the community page of the website you can now SAVE other producers songs (the bookmark) and open them under Saved songs (for listening: a song cannot be downloaded or saved to your PC). In the program, SAVED SONGS and SHARE open the community on the website, where you sign in.
 + Project cards have UPLOAD PIC and DELETE PIC buttons. Buttons in the DAW now have the lit rim and glassy top of the buttons of the main window. The revert bar is a neat neon box on the left.
 + Scroll bars: the text boxes of the windows (the log, the links box, the lyrics, the changelog, the tags) have the neon bar of the DAW instead of the grey Windows one.
 + Sound: the first play after opening the DAW no longer crackles. The sound card now waits until its buffers are full before it starts, the audio thread runs as a pro audio thread, the code that makes the sound is prepared in the background when the studio opens, memory is cleaned while nothing is playing, and loading a file never takes the processor from the sound.
 + Generate Lyrics finds the RIGHT song: a file named only with its title is matched by its length (a tribute or a live version is not used), a failed lookup is tried again, and songs that have to be heard now have a QUALITY choice (Fast, Better, Best) and HEAR ONLY THE VOICE FIRST (the singing is taken out of the music first, with the Vocal remover). TIME MADE counts while the DAW is open, even when it is not in front. TIMES USED sits clear of the logo, the Pages menu boxes fit, and the revert bar counts 30 seconds.

3.32.0  Colour mixer with two palettes, presets, every finish of colour, a language box for lyrics
 + The colour mixer on the Profile page is rebuilt with two sides, like the reference: the LEFT palette is colour A and the RIGHT palette is colour B, and A merges into B. Each side has its own STYLE drop bar, so you pick both colours from any style. Between them are the merged circle, a MIX slider and SWAP.
 + Whatever you change in the mixer IS your profile colour: no buttons to choose where it goes. The banner, rings and accents take the merged colour at once and your name fades from A to B. Two switches make it toggleable: PROFILE COLOUR = MY MIX (ON/OFF, OFF goes back to the colour of your theme) and FADE MY NAME A TO B (ON/OFF). The old ACCENT COLOUR square and the separate colour buttons are gone.
 + SAVE COLOUR PRESET keeps the two colours, their finishes and the mix; PREV and NEXT go through your saved presets (each one is loaded and becomes your profile colour), you can click any saved preset, and DELETE THIS PRESET removes one. The mixer also remembers where you left it.
 + USE AS A THEME (a theme for the whole program from your mix, REVERT puts the old one back) and COPY THE HEX CODE stay. Every section of the mixer is in a neat neon-outlined box. The colour wheel turns slowly and no longer has a title.
 + More than a thousand colours in every finish, like a car paint sheet, each drawn in its own finish: Classic gloss, Metallic, Metals, Brushed metals, Chrome and mirror, Chameleon (40 colour-shifting pairs), Candy, Pearlescent, Holographic and iridescent, Glitter and sparkle, Carbon and tech, Satin, Matte, Frosted glass, Worn and weathered, Neon glow, Basic, Pastels, Jewel tones, Earth and nature, Flowers, Wood and stone, Vintage and retro, Military and utility, Skin and hair, Sweets and drinks, Seasons, Sky and sea, Fire and sunset, Greys and blacks, and Every shade (360 shades and a grey row).
 + Generate Lyrics has a LANGUAGE drop bar (Auto-detect and 68 languages, remembered). The speech recogniser listens in the language you pick, which fixes songs it mis-detects. Lyrics that are not English only get spacing and capital letters tidied (the apostrophe and punctuation rules are for English), and Auto-detect works out which one a lyric is.
 + The lyrics lookup was checked against the real lyrics database and YouTube with songs in English, Spanish, French, German, Italian, Korean and Hindi, with names written as downloads really are (uploader first, brackets, Official Video).

3.31.0  Colour mixer on your profile, Recheck video + words, a fuller lyrics lookup, a calmer visualiser
 + COLOUR MIXER on the Profile page, in a box of its own (not a tab). Pick colour A and colour B and see the colour they make: a merged circle, a gradient from A to B, five steps (100% A, 75/25, 50/50, 25/75, 100% B), a colour wheel with arrows to A, B and the result, and swatch pairs like Orange + Blue. A MIX slider moves the merge point and SWAP flips A and B.
 + A STYLE drop bar chooses which colours you pick from: Every shade (360 shades and a grey row), Basic, Neon and glow, Pastels, Metals (gold, silver, bronze, copper, rose gold, platinum, chrome, titanium, steel and more, drawn shiny), Pearlescent (pearl, opal, abalone, iridescent, drawn with a soft sheen), Matte (matte black, sage, terracotta and more, drawn flat), Jewel tones, Earth and nature, Sky and sea, Fire and sunset, Greys and blacks. Over 580 colours. Right-click puts a colour in the other slot.
 + USE AS MY PROFILE COLOUR sets your profile colour to the merged colour, USE AS A THEME makes a theme for the whole program from your mix (REVERT puts the old one back), and COPY THE CODE copies the hex code.
 + Generate Lyrics finds songs far more reliably. It reads a name every sensible way (Artist - Title, Uploader - Artist - Title, Title - Artist), cleans the artist and title written in the file tags (the channel name, Topic, VEVO, brackets), and a guess that fails on the network no longer stops the others. A match must be the right artist and the right title, so a cover or a reply song with the words in its name is never used.
 + The database can hold several copies of a song and some are cut short: every real copy is looked at and the most complete one is used, so verses are no longer missing. Songs that only have time-stamped lyrics are used too (without the time stamps).
 + RECHECK VIDEO + WORDS: Purphex listens to the song or video file again and lines the words up with the lyrics in the box. It lists lines that are not sung, finds passages that are sung but missing (a missing verse), and ADD THE MISSING PASSAGES puts them in the right place, marked to check. Nothing changes until you press a button, Undo takes it back, and a speech recogniser can still mishear a word, so it is a guide and not a guarantee.
 + When a lyric is longer than the box, the page now says how many lines are below the edge (scroll to see them), so a verse out of sight no longer looks like a missing verse. The listening settings were changed so the recogniser hears each sentence on its own and no longer gets stuck repeating a line and skipping verses.
 + REMOVE LYRICS on the Generate Lyrics page takes the lyrics, title and notes off the page (Undo brings them back; what you saved to the DAW stays in RECENT).
 + Visualiser: the bars are back to following the music (quiet music gives short bars, loud music tall ones) instead of sitting near the top all the time, and each blast is as big as the hit that caused it, so soft hits give small blasts.

3.30.2  TIMES USED for every file type, trim on Generate Lyrics
 + TIMES USED now has a card for every file type: MP3, MP4, OPUS, WAV, M4A, FLAC and MKV (the quality does not matter, only the type of file), plus the total for all of them and what you made on this PC. Every finished conversion counts as its own type, and the website keeps one counter for each, with no upper limit. A number too long for its card is shown short (1.2e29) with the whole number in the tooltip.
 + The TIMES USED button moved over next to Changelog (it no longer sits near the Purphex logo).
 + Generate Lyrics has TRIM START and TRIM END boxes (like 0:30 or 1:02:03). When the words are written by listening, only that part of the song is heard, so a verse or a chorus can be done on its own. A wrong time or an end before the start is refused with the reason.

3.30.1  Photo themes that really recolour the program, instant crackle recovery, a clear theme hover
 + Photo themes now work properly: PHOTO in the Themes box turns a picture into a theme of its own. The purple parts of the program (buttons, panels, outlines) take the real colours of the picture (up to four of them, so a sunset gives orange, pink and blue), and the picture fills the window, cropped to fit and never stretched. The theme you had is left alone, and REVERT puts it back.
 + The theme under the mouse in the Themes box now has a bright outline with a soft ring round it, so it is clear which one a click will pick (the old hover never lit up).
 + Crackle recovery: the moment the sound breaks up (two starved buffers within two seconds, or the sound card stopping for a third of a second) the audio output is refreshed. It takes a split second, then the sound is clean again. Each refresh also makes the playback buffer a step bigger for the session (your choice in Settings is kept), and at the biggest buffer it is a plain refresh, at most once every second and a half.

3.30.0  Generate lyrics from songs, videos and links, TIMES USED, and a tidier studio
 + Generate Lyrics: a new GENERATE LYRICS button on the first screen (and in the Menu) opens a page of its own, free in every edition. Drop or choose any song or video (mp3, wav, flac, m4a, ogg, opus, aac, mp4, mkv, webm, mov and more) and press GENERATE LYRICS. On the Fantasy edition the page wears the Fantasy gold and follows your Fantasy theme.
 + Where the words come from: the song's tags and file name find it in the LRCLIB lyrics database. A song that is not there can be written by listening to it with the Whisper speech recogniser, which runs on your PC. Purphex asks before downloading it (about 160 MB, once) and your song is never uploaded. A recogniser can mishear a word, so check the words against the song.
 + Correct grammar and punctuation: capital letters, I, the apostrophes in don't and I'm, spacing round commas, a full stop or question mark at the end of each verse, a comma before and / but / so lines, clean verses, and [Verse] and [Chorus] labels. FIX GRAMMAR does the same to words you typed, and UNDO brings your words back.
 + Resize lyrics: A- and A+ change the writing size, and FIT TO BOX makes it as big as it can be while the whole lyric fits in the box (a very long song stops at a readable size and scrolls).
 + Keep it: COPY, SAVE .TXT, or SAVE TO THE DAW under a TITLE so you know which song it was (it starts as Artist - Title). RECENT on the page shows the last ones, newest first. In Purphex Music, the Lyrics page has a RECENT GENERATED button that puts one back into the writer, and a GENERATE LYRICS button.
 + Visualiser: the effects were too large, so the ring bars, shells, kick ring, hexagon and shockwave are smaller and calmer, and the all-round rays are gone. Instead there are four separate blasts, each with its own bang (a wave, streaks and sparks): the bass bursts out of the bottom, the low mids out of the left, the mids out of the right and the highs out of the top.
 + Generate Lyrics also takes links: paste a YouTube link (other sites need the link reader that the converter installs) and the lyrics are found from the video's name, with nothing downloaded and no MP3 needed. To write the words by listening, add the song or video file instead.
 + TIMES USED: a new button on the first screen shows how many MP3s and MP4s have been made with Purphex Music all over the world, in full (1,234,567) and short (1.2M). It has no upper limit and goes into the millions and far beyond. Every finished conversion adds one; only that number is sent, never who you are, the link or the song, and Settings, General turns it off.
 + The side menu of Purphex Music: Projects and Community are buttons of their own (Projects opens your saved projects and nothing else), Pages keeps Mix, Mastering and Lyrics, and the Community header has ARTISTS.
 + AUTO MASTERING now asks Are you sure? before it starts, so a stray click cannot touch your work. Nothing changes until you press USE THIS, you can hear YOUR MIX (BEFORE) and any version (after), and a REVERT button stays next to AUTO MASTERING to put the master back exactly as it was.
 + Project cards on the Community page have one solid PLAY button along the bottom (the duplicate Play in the hover tools is gone: Open is on its own line and the other tools sit in pairs).
 + TIME MADE on your profile is now the time you spend in the DAW, counted live in seconds while you use it (it starts from the length of the projects you already made).
 + Fantasy edition: the gold Fantasy theme (Royal Gold) goes on every button and outline of the program the first time it is on your PC. Choose any other theme afterwards in Themes.
 + Help has a new topic, Generate lyrics from a song or video.

3.29.1  An even visualiser, a FULL VISUALISER button on the light show
 + The visualiser is even. The ring of bars no longer turns (the loud low bars used to wander round it, so one side always looked smaller), neighbouring bars share their level, and every bar carries the same overall bounce as the logo, so the whole ring and the logo move together. The spectrum along the bottom is lifted towards the highs, so it is no longer tall on the left and flat on the right.
 + A FULL VISUALISER button sits at the top right of the light show on the Mastering page and opens the visualiser on a page of its own (Esc comes back). The Master Visualiser entry is gone from the Pages menu, so there is one way to it, right where the light show is.
 + The song position bar of the visualiser visualises the sound too, like the scroll bars: waves flow out of its logo along the bar, cyan to the left and pink to the right.

3.29.0  Lyrics saved with every song, in every edition
 + Lyrics getter. After a song is converted, its lyrics are looked up (LRCLIB, a free community lyrics database) and saved as a .txt with the same name next to the song. The parts are labelled [Verse 1], [Chorus], [Pre-chorus], [Bridge] by the same labeller as the Lyrics page. It works in every edition, Free included, for MP3 and the other formats and for mastered files.
 + Nothing wrong is ever saved: a song only gets lyrics when the title (and artist) match well, an instrumental or an unknown song gets none and the log says so, and an existing .txt is never overwritten. The file says the lyrics belong to their writers and are for your own use.
 + Settings > General has a switch for it (on by default). Help has a new topic, Lyrics saved with each song (.txt).

3.28.3  Settings text is never cropped or written over
 + Settings measures its explanations in the font they really get on your PC and gives each one the height it needs, moving everything under it down. Before, a long message on the Account page (for example about linking) could run into the buttons below or be cut off.

3.28.2  Linking a PC cannot get lost, Settings always fits the screen
 + Linking a PC no longer gets lost. If the website made the link but could not give a licence yet, the PC now stays linked and the Account page says so (before, the website showed 1 of 3 PCs while the app said it was not linked). CHECK AGAIN asks the website again, and if the answer to the link never reached the app it can be collected again.
 + Settings is never wider than the writing: the window cannot be made narrower than its pages (so text no longer runs off to the right behind a scroll bar), it always fits on your screen, and the explanations are brighter and easier to read.

3.28.1  Softer buttons in the studio, bigger writing in Settings, every label fits
 + Softer studio buttons. Every button in the studio has rounder corners, a soft shade from top to bottom and a thin light on its top edge, like the buttons of the other windows. The glow, the hover and the colours of your theme are unchanged.
 + Bigger writing in Settings: the explanations under each setting are about a tenth bigger, and the frame-rate list is wide enough to read in full.
 + Every button label fits: a label that is too long for its button is written a little smaller instead of wrapping or being cut off (the CHANGELOG and BROWSE buttons of the main window showed this), and the two tick boxes under the trim boxes are wide enough for their words.

3.28.0  Rounded buttons in every window, edition codes, Help for editions
 + Rounded buttons everywhere. Every ordinary window (Settings, Help, the converter, What's new, the update card, the preview windows) has the new button: rounded corners, a soft shade, a thin lit edge, a glow under the mouse and a press that sinks. The writing is white like on the other buttons and turns dark only on a light colour.
 + Edition codes. Every paid edition has a code (PX-XXXX-XXXX-XXXX) that is linked to one place at a time: your website account or your Discord. Settings > Account has a new button, EDITION CODES: UNLINK OR REDEEM, that opens the website page where you see your codes, unlink one so it can be used on another account, or redeem a code. Editions given by the Purphex team cannot be moved.
 + Help has a new topic, Editions, edition codes and linking this PC, that every edition can read: what each edition has, how to unlock a PC (and that one account can have 3), and how codes work.
 + Cleaner inside: an old unused preset menu was removed, and the Settings mouse-wheel check no longer fails when another program has a window over it.

3.27.0  Fonts page with 190+ fonts, microphone modes, track off light, profile picture and banner controls, easier to read in every theme
 + Fonts. A new FONTS button in the side menu opens a page with 190+ fonts for all the writing in the studio: drippy and horror, gothic, graffiti, neon, retro, script, handwritten, bold, serif and clean, plus the fonts that came with Windows. Every card is written in its own font; click one and the whole studio changes. A font is fetched from purphexmusic.com the first time and kept on this PC. Every font is resized by itself so its lines are as wide and as tall as the original (nothing is cut off or runs off a page: all of them were tested on every page at two window sizes), and fonts too wide to read at a fitting size are left out.
 + Microphone modes. The MIC button has a neon box with a down arrow and a short menu: AUTOMATIC (rumble out, quiet room noise eased down), STANDARD (exactly as it is), VOICE ISOLATION (only your voice: spectral noise reduction and a gate that opens before each word) and WIDE SPECTRUM (the whole range and room, a little air on top). They only change what you record, the take stays exactly in time, and your choice is remembered.
 + The neon light on a track header is a power switch. Press it and the track is OFF: it is silent (and ignored by solo, exports, mastering and the vocal sidechain), its header dims, and on the timeline its audio turns very translucent and shakes apart in an animated earthquake of broken strips, gaps and cracks. Press it again to turn it back on. It is saved with the project.
 + Your profile: any picture or banner of any size or shape is resized to fit its frame by itself, with ZOOM, LEFT / RIGHT and UP / DOWN buttons and sliders, FIT TO FRAME and FILL THE FRAME. The picture is a rounded square now and shows only on your Profile page and your public profile: every other circle in the studio is the Purphex logo and cannot be changed. The accent colour is a real colour palette (the nine swatches are gone). Up to 10 links and 10 genres wrap onto more lines instead of being cut off, emoji work in names and bios, YOUR PUBLISHED PROJECTS lists only what you published, and TIME MADE reads like 2h33m or 1d2h33m.
 + Easier to read in every theme. Every piece of writing in the studio was checked against what is really behind it in all 24 themes: white writing on bright buttons (yellow, green and teal themes had it at 1.4 to 2.5 to 1) now turns dark when the button is light, bright theme colours can no longer become too bright for white writing, and the ruler, the Fantasy badge, the Mastering chain and the visualiser text were fixed. About 1,100 hard to read pieces are down to one that is right at the limit.
 + Tidier: ADD EFFECT and PRESETS stay at the top of the plugin panel and the VOCAL STYLES button there is gone (it is still in the side menu under Tools); the VISUALISER button on the Mastering page is gone (the light show is on the page; the full page is Pages > Master Visualiser). The separation script is no longer left on disk as readable text.
 + Bigger writing. All the small writing in the studio is up to a fifth bigger (the smallest the most) and is fitted to its box by itself: it only becomes smaller when a name would not fit, never smaller than it was before, and nothing is cut off or runs into the next thing. Checked on 347 screens in five window sizes and every theme.
 + The Fonts page is only for choosing a font. The sample buttons and the music words are gone: your font is shown as a sentence, the alphabet, the numbers and the punctuation, with the search, the groups and the font cards under it.
 + Presets in neon boxes. The master presets (Mastering page), the presets list of every plugin and the community presets list use the same neon box as the font cards: a glowing outline in the colours of your theme, a small tag (the group, BUILT-IN or MY PRESET), short text that never runs into the buttons, and the buttons at the side.
 + The little arrow next to MIC now shows its hint when you point at it, like every other button.

3.26.1  Crash fixes and fewer duplicate buttons
 + Fixed the grey Microsoft .NET Framework box that said DragDrop registration did not succeed. The studio window asks Windows to accept dropped files each time it is built; when Windows refused (after full screen, a theme change or another program holding it) the program crashed. Now it just carries on, and dropping files works again the next time the window is built.
 + Any other problem inside Purphex no longer shows the grey .NET box either. It is written to crash.log (in %LocalAppData%\YtToMp3, next to settings.txt) with the time, the version and the details, the program keeps running, and one plain message says so (the same problem is not shown again for a minute). If something keeps happening, send that file on the Discord and it can be fixed.
 + Fewer duplicate buttons. The Community page lost its left menu (Home, Artists and the others were already buttons at the top), the SEARCH ARTISTS button (ARTISTS is at the top) and the YOUR PROFILE card (PROFILE is at the top). MY PROJECTS, which was only reachable from that menu, is now a button in the Community header.
 + HOME is only shown where there is no MIX button on the screen: it is gone from the Lyrics, Mastering and My Projects pages (MIX does the same). Mastering has one VISUALISER button (the top bar one; the copy on the picture is gone) and one original or mastered switch (the A and B buttons on the left; the picture only shows which one you hear).

3.26.0  Editions: Free, Supporter and Fantasy, padlocks, licence protection, privacy for your projects and profile, themes for each edition
 + Purphex Music now has three editions. FREE is the MP3 and MP4 converter (links, search words, playlists, trim) and the vocal remover, for everyone, for ever. SUPPORTER (9.99 pounds a month, or 99.99 pounds a year which is 15 percent off) opens the whole studio: mixer, Auto Mix, mastering and the visualiser, plugins, sound packs, Bass Lab, Flex Pitch, themes, photo backgrounds, your profile and artists, and exports up to 192 kHz. FANTASY (449.99 pounds, once, for life) is everything in Supporter for ever, plus exports up to 384 kHz, themes and a border of its own and early builds. Everything is bought on purphexmusic.com: the app never takes a card.
 + Padlocks. In the Free edition the PURPHEX MUSIC button, Hear, Normalize loudness, the formats other than MP3 and the MP4 videos, sample rates above 48 kHz, the Mastering and Plugins items of the menu and the Background and Theme pages of Settings are greyed out with a padlock. Clicking one explains what it needs and takes you to the editions on the website. Format and sample rate lists name the edition each entry needs.
 + LINK THIS PC: Settings has a new ACCOUNT page. Press LINK THIS PC, sign in on the website, press Yes, link it, and this PC unlocks by itself. The token is kept on this PC encrypted for your Windows user, the app asks the website about your edition now and then, and it keeps working for a while without internet. UNLINK THIS PC forgets it.
 + Themes for each edition: the Free edition has the original Purphex theme. Supporter has all the standard themes, the Rainbow and three of its own (Neon Tokyo, Aurora and Bubblegum). Fantasy adds five that only it has: Royal Gold, Nebula, Obsidian, Splatter and Dragon Fire. In the theme list each of these wears its edition name and the ones above your edition show a padlock.
 + The Fantasy Edition has a badge and a border on its profile: a line running from pink to cyan with splatters and sparks around it, the same colours as the pointer over a song. It shows on your own profile and on the Artists page and every artist profile. The website decides who has it from the linked PC, so nobody can switch it on by themselves.
 + Help only lists what your edition has: the Free edition keeps the converter, the vocal remover and the basics, and the studio guides appear with Supporter and Fantasy.
 + Licence protection. Your edition is a signed licence that the website makes for your PC: it cannot be edited, invented or copied to another PC, it runs out after a few days unless the app renews it (so a cancelled plan or an unlinked PC stops working soon), turning the Windows clock back switches the paid features off, and a copy of the program that was changed is kept locked. An account can have up to 3 PCs linked at a time: unlink one on the website or in Settings to use another. If the licence ends while the studio is open, the studio warns you and closes.
 + Privacy and your projects. Every project is private until you PUBLISH TO PROFILE: use the new person button on its card in My Projects (or its right-click menu), or the PUBLISH TO PROFILE buttons under YOUR PROJECTS on the Profile page (with PUBLISH ALL and MAKE ALL PRIVATE). Only the projects you publish appear on your profile, and only their name, tempo, length and tags, never the audio. The Profile page has a PRIVACY row: PRIVATE ACCOUNT (hidden from the Artists page and search), HIDE LOCATION and HIDE FOLLOWERS. A profile can now have up to 10 links and 10 genre tags.
 + The side menu is tidier: File, Edit, Tools, Pages and View open lists, and Profile, Themes, Settings, Help and Exit are single buttons (Profile is a page of its own, not inside Community). The Community page keeps only its own links.
 + Mastering: the visualiser is on the Mastering page in every window size now (it used to need full screen), and the song position bar under it is the same strip as the scroll bars, with the logo as its handle. The Community page no longer lets the preview bar cover your profile card.
 + Themes: browsing themes quickly no longer leaves old colours on buttons. Every colour is rebuilt from its original colour each time, Mono can no longer leave a button grey for good, and the last theme you click always lands. Settings has one SAVE button (the one at the bottom). Eight themes are new: 3 only Supporter has and 5 only Fantasy has (so no two themes look alike, Obsidian, Dragon Fire and Bubblegum got their own colours).
 + Downloads and tidying: converted files are saved in the Downloads folder of your PC by default (the old Music folder choice moves to Downloads, a folder you picked yourself stays), and exports start in Downloads, in a Purphex Music folder. Import a theme file is gone (themes come from the list, the community or a photo). The Community page has one GO TO YOUR PROFILE button and one copy of your picture, and the Artists page lost its second MY PROFILE button. The words in the Add your music to start box stay inside the dotted line at any window size.

3.25.0  Profiles and artists, a Mastering page that is the visualiser, themes that change everything, a much smoother studio, help that takes you there and sample rates up to 384 kHz
 + The Mastering page is the visualiser now. The light show sits on top of the page (a ring of bars around the logo, shells of light behind it, a circular scope that draws the real waveform, beads turning against each other, rays on the highs, hexagon pulses, sparks and rushing stars on every beat) and the EQ, effects and presets sit right under it, so you watch the music move while you master. FULL VISUALISER opens it as a page of its own. A window too short for both keeps the controls and gives you a VISUALISER button. The visualiser is also three times lighter to draw than before.
 + Everything is smoother. Buttons and glows are drawn once and then copied, big panels are solid, the grid is copied into every track, pop-up boxes move by sliding a stored picture instead of redrawing the whole studio, the Rainbow theme no longer forces the window to redraw all the time, and the studio waits for itself less: it now draws as often as the screen can show.
 + Themes really change everything. Every colour in the studio, the converter window, Settings, Help, the tour cards, the leaves and Bass Lab follows the theme: purples follow its violet, cyans its cyan and pinks its pink, so the MIX button, every other button, every box, every glow, the grid and the waveforms change together. A window that is already open recolours when you pick a theme, and even the title bar follows.
 + PROFILE is a page of its own with a big editing system: banner (any picture, move it up or down), round picture, accent colour, name, bio, where you are, your genres (tick or add your own), and LINKS (any web address works: your website, YouTube, SoundCloud, Spotify, Instagram, a shop, even app links). You see how it looks while you edit it, with your numbers (projects, tracks, time made, followers) and your tracks with play buttons. PUBLISH MY PROFILE puts it in the community under a secret key made on this PC (there is no sign-in). Profile pictures are never buttons anywhere any more, and everything about editing a profile lives only on the PROFILE page. The Community page has GO TO YOUR PROFILE and SEARCH ARTISTS.
 + ARTISTS: search the other producers by name or genre, sort them, open their profile (banner, picture, bio, links, tracks, followers), FOLLOW and UNFOLLOW them, and see who you follow on your profile. Three reports hide a profile.
 + The Community page: a play button on every project card and the audio player bar like in My Projects, and CREATIVE STUDIO, MASTERING and LYRICS are gone from it.
 + Help takes you there. SHOW ME now goes to the right page and opens the right box first (the AUTO MASTERING guide goes to the Mastering page, AUTO MIX to Mix, sound packs to the Sounds browser...), and every guide with buttons in it has a prompt for each one. A pop-up box, a menu or a sheet that is open now blocks the page behind it completely: nothing behind it can be clicked, scrolled or typed into.
 + Settings has a sidebar of sections (Background, Theme, Animation, Audio, Updates, Community, General) and a SAVE button at the top and at the bottom (it used to say Done). The playback buffer you choose is now saved and kept: the studio only makes it bigger for the rest of the session if the sound breaks up, and never changes your saved choice.
 + The highest sample rate possible: exports can be 44.1, 48, 88.2, 96, 176.4, 192, 352.8 or 384 kHz (WAV through a high quality Kaiser filter, FLAC through ffmpeg; MP3 stops at 48 kHz because that is as high as the format goes), and the converter window has the same rates, held to what each format can really hold.
 + Smaller fixes: the Lyrics page always has its logo scroll bar. The Edit section of the side menu only shows on the Mix page. Everything in the Tools menu (Auto Mix, Split vocals, Sound packs...) takes you to the Mix page. The hint label is see-through, smaller, lower and waits a moment before it shows. Every PREVIEW has a STOP PREVIEW beside it. The Genre tags box fits its words and has your own tags. The count-in numbers are drawn as clean neon strokes. The REVERT bar keeps its words inside its box, counts down 60 seconds and presses the X for you. Flex Pitch has its own zoom, separate from the mixer. The duplicate Streaming master preset is gone.

3.24.0  The Master Visualiser, your own photo as a theme, neon borders in the menu, unique theme colours and a Space bar that pauses in My Projects
 + The Master Visualiser: a page of its own for the music you are mastering. Open it with the VISUALISER button on the Mastering bar, the strip along the bottom of Mastering, or Menu > View > Master Visualiser. A ring of 96 light bars dances around the Purphex logo, which beats with your bass. A thin ring inside shows the sound before mastering. Every beat sends out a ring and a burst of sparks. A mirrored spectrum with peak dots runs along the bottom, left and right level meters stand at the sides, and cards show MOMENTARY and INTEGRATED loudness, TRUE PEAK and GAIN REDUCTION. At the top you can press PLAY, PREVIEW (the loudest 20 seconds on a loop with the mastering on) and switch HEAR: ORIGINAL / MASTERED while you watch. The song bar at the bottom jumps to any place when you click or drag it. Esc, BACK or MASTERING returns.
 + A visualiser strip along the bottom of Mastering, the same idea as the preview bar in My Projects: a black strip with the logo and moving waves, a play and pause button, the time, and a click-to-jump song bar. It appears when the window is tall enough (740 pixels or more) and makes room for itself, so nothing is covered.
 + Themes take any photo. IMPORT now opens a theme file or any picture of any size (a huge photo, a tiny one, a tall one): the picture becomes a theme and fills the whole studio window, cropped in the middle and never stretched. It keeps filling it when you minimise, come back or go fullscreen. PHOTO puts a picture behind any theme, NO PHOTO takes it off, SAVE keeps the picture next to the theme file so you can share both, and every theme card shows a small picture of its photo.
 + The themes are really different now. Toxic, Crimson, Red, Orange, Yellow, Green, Teal, Blue, Indigo, Violet and Pink each have their own colours instead of being nearly the same (Toxic is a hot acid yellow-green and Green is a deep forest green, for example).
 + Neon borders around everything in the side menu: the lists, the recent projects and the columns all glow in the colours of your theme.
 + BACK only shows when it has somewhere useful to take you. If going back would only take you home, the button stays hidden (HOME is its own button).
 + My Projects: Space now pauses and continues the preview (it used to jump into the search box). The search box only takes typing after you click it, and clicking anywhere else lets go of it. The preview bar has a pause button too.
 + Settings: scrolling the mouse wheel over the Settings window now scrolls it instead of changing the setting under the pointer.

3.23.0  A Mastering tab of its own, AUTO MASTERING, 67 master presets, themes you can import and share, REVERT, HOME and BACK, scroll bars everywhere and a Help that shows you
 + The Mastering tab is a little program of its own. The top bars show only what belongs to mastering: PLAY (its own play button), the time, PREVIEW MASTER, the master meter, and MIX / MASTERING / LYRICS, SAVE AS, SAVE, OPEN, FULLSCREEN, HELP and EXPORT. The track buttons are gone from it, so nothing can be pressed by accident.
 + AUTO MASTERING (it used to be Analyze whole mix) listens to the whole mix, builds the EQ for you with no input, and opens ten versions to hear: Clean and Balanced, Warm and Wide, Loud and Punchy, Bass Boost, Vocal Forward, Bright and Airy, Radio Ready, Streaming Safe, Glue and Cohesion and Lo-Fi Warm. One of them is marked BEST FOR YOUR MIX. PREVIEW plays the loudest 20 seconds of your song on a loop with that version on (switch between them as often as you like), USE THIS keeps one, and CANCEL puts everything back.
 + Master presets, 67 of them, in groups (Everyday, Loud, Genre, Vocals, Low end, Colour, Width, Fix): Bass Boost Master, Bass Boost Extra, Sub Bass Focus, Bass Tighten, Vocal Improver, Vocal Enhance Master, Vocal Warmth, Vocal Air, Rap Vocal Forward, Podcast, Audiobook, Trap, Drill, Hip-Hop, Pop, Rock, Metal, EDM, House, Dubstep, DnB, Phonk, R and B, K-Pop, Reggaeton, Afrobeat, Lo-Fi, Cinematic, Radio and many more. Press PREVIEW on any of them to hear it on your song straight away, then KEEP IT or PUT BACK. USE applies it and always offers REVERT.
 + AUTO LOUDNESS measures the whole mix, picks the best level for that track (dynamic music keeps its dynamics, dense music goes louder, everything else gets the -14 LUFS streaming level, and a mix that is already louder is never turned down) and sets it for you. For the picky there is still the manual way: the slider and SET LOUDNESS (MANUAL).
 + The EQ picture shows your sound moving: a bright live spectrum after the EQ, a faint one before it (in Mastering), and what the EQ changed lit up: green where it added sound, pink where it took sound away.
 + REVERT: whenever you press USE on a preset, a style, a plugin chain, a master preset, a community preset, AUTO MASTERING, AUTO LOUDNESS or a theme, a REVERT bar appears at the bottom and puts everything back exactly as it was. The plugin Presets sheet also has its own REVERT that goes back to how the plugin was when you opened it.
 + The plugin Presets button opens a proper list instead of a small menu: search it, scroll it with the logo scroll bar, press USE on any preset, delete your own, save yours or open Community presets. Every plugin has many more presets now (EQ alone has about 40: kick, snare, bass, vocals, telephone, lo-fi and more).
 + Themes: 16 themes, one for every colour of the rainbow (Red, Orange, Yellow, Green, Teal, Blue, Indigo, Violet, Pink) plus a Rainbow theme that slowly turns through every colour. The Theme page lets you IMPORT A FILE (a theme is a short text file of colours, .purphextheme), SAVE THIS THEME to share it, and open COMMUNITY THEMES to download themes other people made or share your own. Every theme goes through the same clean-up: backgrounds stay dark and accents stay bright and readable, so a custom theme can never look tacky or unreadable.
 + HOME and BACK: HOME goes to the main studio (Mix) and forgets where you have been. BACK steps back through the pages you went through, one at a time.
 + Every page has the logo scroll bar when it has more to scroll: My Projects, the Recycling Bin, Community, the Mix tracks, the plugin panel and its parameters, Sounds, the packs list, the lyrics box, the presets lists, the AUTO MASTERING versions, the themes and Bass Lab lists. The logo rides along the bar, is the part you hold and drag, and the lit box is gone. Flex Pitch and the bars under the tracks work the same way and the preview bar moves its logo with the song.
 + The Count In is always four clicks, one a second, whatever the tempo is, so it stays a proper countdown.
 + Pointing at a button throws fewer, softer sparks that stay under the buttons around it, and there are new 144, 165 and 240 fps modes in Settings for fast screens. The animated logo behind Community and My Projects was made lighter so it turns smoothly.
 + The zoom - and + buttons are gone (the ZOOM slider does it all).
 + Lyrics: opening a text file puts its file name at the top as the title, and clearing all the words clears the title too.
 + Help: the topic list no longer puts the pink arrows on top of the writing, the steps are laid out clearly, and every step that names a button has a SHOW ME: it finds the page the button is on, goes there and points at the real button with its hint. The guided tours now point exactly at the real buttons and their cards are lined up. Bass Lab has a round ? HELP button that walks you through the screen (presets, oscillators, filter, envelopes, LFOs, effects, keyboard) and a Bass Lab basics guide.

3.22.0  Search, sort and filter your projects, a Recycling Bin, BACK on every page, STEREO and MONO buttons and cleaner scroll bars
 + My Projects has a search box at the top: just start typing and the cards narrow down as you type. It finds a project by name, tag or type (song, beat, vocals) and every word has to match. Esc clears it, Enter lets go of the box (so Space plays again).
 + SORT puts the projects in the order you like: newest edited, recently opened, name A to Z or Z to A, most tracks, longest or oldest. TYPE shows only songs (vocals with a beat), beats (beats and instruments) or vocals. The star on every card makes a favourite and the FAVOURITES button shows only those. CLEAR resets everything, and the page tells you how many projects match (2 OF 9 PROJECTS).
 + The RECYCLING BIN: when you remove a project and tick the box to delete its file, the project now goes into the RECYCLING BIN (a button on the projects page) instead of being deleted. It waits there for 30 days, with the days left shown, and is then deleted for good. RESTORE puts it back in My Projects (if its name is taken by another file it comes back under a new name, nothing is ever overwritten). DELETE FOREVER and EMPTY THE BIN delete at once and always ask first. Only the project file waits in the bin: your audio files are never touched.
 + In the side menu My Projects (pictures) is now just My Projects.
 + BACK is on every page now. Mastering, Lyrics, Community and My Projects all have a BACK button that takes you to the page you came from (the Mix page shows it too when you arrived from Community or My Projects), so you never have to go through the menu.
 + The vertical scroll bars (Rapper Vocal Styles, Community presets, Add effect and the other lists) were redone: the glowing box is gone, the logo sits at the top of the bar and you hold the logo and drag it to scroll. The arrows step, click the bar to jump.
 + Every track has a STEREO button with a separate MONO button underneath it (the one that is on is lit).
 + Thin tracks are fixed: the buttons no longer pile up or fall out of the track. There is a new THIN TRACKS button next to UNDO MIX that switches between thin and normal tracks.
 + AUTO MIX now asks Run Auto Mix? before it does anything (so a slip of the mouse does nothing), and you can cancel while it is working: everything goes back as it was.
 + MIC asks Would you like to record now? (Yes arms it, then press PLAY to record). MIDI REC asks Would you like to record an instrument? before it switches on.
 + The DETECT button is now DETECT BPM.
 + Pointing at any button throws a few small neon sparks off it.
 + Community and My Projects have the animated Purphex logo behind them, like the one on the website: a turning ring of light and a soft breathing glow. The useless With picture tab in Community is gone.
 + Playback sound: if a project is too loud, the sound is now turned down smoothly just before the sound card instead of its peaks being chopped off flat. Chopped peaks are what you hear as fuzz and crackle. Normal projects are not touched at all.

3.21.0  Capitals and a bold font, STEREO / MONO, movable boxes, logo scroll strips, a reworked CUT and fades, a new projects page and colour themes
 + All the writing is now in CAPITAL LETTERS in a bold, sharp font (Bahnschrift, which every Windows 10 and 11 PC has), in the studio, the main window, Settings, menus and dialogs. Long sentences keep normal letters so they stay easy to read. The main window was re-spaced so no writing touches a box or another label, and GRID SIZE is in capitals too.
 + CUT now really cuts: click the piece you want gone, press CUT, and that piece is DELETED and the pieces after it move up to close the gap (SPLIT is the one that makes two pieces). It works on every piece lit by a selection box too, skips locked tracks, and Ctrl+Z puts it all back.
 + Fades were reworked. The FADE IN and FADE OUT buttons are gone: click a clip and drag the round handles. The top handle sets how long the fade is; the new bottom handle moves where the fade starts (or where it ends), so you can leave silence before a fade in or after a fade out. Double-click a handle to remove or reset it. Fades follow a smooth S curve (no click at the start, no plunge at the end) and the picture on the clip is drawn from the very same curve, so what you see is what you hear.
 + Every track has a STEREO / MONO button. MONO makes left and right the same sound; it is saved with the project.
 + Pop-up boxes (Gain, the track pickers, New Track, Buses, Rapper Vocal Styles, Automation, Community presets, Add effect) can be dragged by their top strip to anywhere on the screen and stay where you put them.
 + Lists now have a vertical scroll strip made like the one under the tracks: solid black, arrows at both ends, a lit window showing where you are and the logo in the middle to hold and drag. It is on Rapper Vocal Styles, Community presets, Add effect, New Track, Buses and the track pickers.
 + The scroll strips are solid black so nothing behind them can flicker through. The strip under the tracks keeps its logo in the middle on every screen size, and the view no longer jumps back to the needle while music plays: scroll where you like, and it stays (click the ruler, press FIT or press PLAY again to follow the needle).
 + Flex Pitch has the same logo scroll strip along its top (the waves come out of the logo) with the zoom slider from the editing row centred under it. The PITCH ZOOM buttons were removed.
 + The transport: PLAY always says PLAY (it lights up while playing), MIC keeps its name and pulses red when it is ready (PLAY then starts the recording), and REC is now MIDI REC for recording from a MIDI keyboard.
 + The Count In is now a glowing hexagon the size of the logo (the big circle is gone) with a neon glow that pulses on every beat and neon sparks that burst off its edges, like the sparks on the needle.
 + A track that once held audio, has been emptied (its clip moved or deleted) and still has plugins switched on is removed by itself, with Ctrl+Z to bring it back. Tracks you made empty on purpose are never touched.
 + Lyrics: Open .txt now puts [Verse 1], [Chorus], [Bridge] and so on in for you, and a new ADD SECTIONS button does the same for lyrics you are writing (parts you already labelled are left alone).
 + A preset you save (a plugin preset, a plugin chain or a Bass Lab sound) is also shared to the Community presets automatically, so everyone can use it. A switch in Settings turns this off.
 + The neon text boxes line up with their writing on every PC and window size, and typing boxes are fixed-size so the frame can never drift away from the text.
 + The pointer (the needle) can only be moved from the ruler strip at the top, which now has a neon border so you can see where to grab it. The needle itself is a glowing neon beam and throws tiny neon sparks while it plays, like fat spitting in a pan. Clicking empty space in the tracks no longer moves it.
 + Hold the left mouse button on empty space in the tracks and drag to draw a selection box: every piece it touches lights up, and CUT, DEL, LOCK and the other buttons work on all of them.
 + Value boxes (the numbers on plugins and tracks) are neon boxes now, kept well apart from their names so you cannot click one by accident. Click anywhere else to confirm what you typed, press Enter to confirm, or press Esc to cancel.
 + Every track works out its own tempo (BPM) by itself, whenever it gets sound, and shows it under the track name.
 + Hint boxes and messages grow to fit all of their words instead of ending in dots, and every button has a hint now.
 + The count in number is exactly in the middle of the hexagon (it is measured from the real shape of the digit, so it is centred on every PC).
 + My Projects was reworked. Every project is a neon card with a square picture; if you have not chosen a picture the card shows the Purphex logo, and any picture you choose is cropped to fill the whole square (never stretched, never with empty bars). The same goes for your profile picture: it is the Purphex logo until you pick your own, and an X on it removes yours again.
 + There is a BACK button on the projects pages (and on Community) and in the right-click menu of a project, so you never have to guess that MIX takes you back. The right-click menu has all the options: open, preview, rename, change or remove the picture, show in folder, remove from the list and BACK.
 + Clicking a project now asks you to confirm before it opens, so you are never sent into a project by accident. The projects in the side menu Recent list ask too.
 + REMOVE PICTURE (a button on each project and in its right-click menu) takes your picture off and brings the logo back.
 + Removing a project asks what you want: take it off the list only, or also send the file to the Recycle Bin (so you can still get it back). Nothing is deleted for good.
 + PREVIEW: press the play button on a project card to hear it without opening it. The bar along the bottom turns into a visualiser (logo in the middle, waves coming out of it) with the time, a STOP button and click to jump to a place in the song. Opening another page, pressing PLAY in the studio or pressing STOP ends it.
 + THEMES: a new Theme tab in the side menu (and a Theme section in Settings) with six colour themes: Purphex, Ocean, Sunset, Toxic, Crimson and Mono. One choice recolours the glows, borders, buttons, the needle and the sparks in Purphex Music, Mastering and Bass Lab, and it is remembered next time.
 + Side menu texts were shortened so none of them is ever cut off.

3.20.0  Clip tools, community presets and a recorder that waits for you
 + Split a track as many times as you like: every piece stays on the same track. New CUT button (press it at the start and again at the end of a section to take that section out of every track and close the gap), FADE IN and FADE OUT with a round handle you drag to set the length, GAIN to set the volume of one piece, and LOCK to freeze a track so it cannot be moved, trimmed, split, cut or deleted until you unlock it.
 + The two Trim to the needle buttons and the floating row of little buttons on a clip are gone. DEL (and the Delete key) now removes just the selected piece.
 + Community presets: every Presets button now opens Community presets, a list of presets other producers shared (POPULAR, NEWEST, search). Press USE to try one, or SHARE MY CURRENT SETTINGS to give yours. It works for every plugin, for whole plugin chains and for Bass Lab sounds. Presets are checked before they are used, and a preset that several people report is hidden.
 + Bass Boost now has ten balanced presets (Clean Low End, Subtle Lift, Warm and Round, Punchy Kick, Bass Guitar Body, Club Thump, Deep 808 Sub, Heavy Trap, Boom and Small Speakers). Each one is tested to raise the low end without clipping.
 + Recording: MIC now arms the microphone (MIC READY) and nothing is recorded until you press PLAY, which says RECORD while armed. Count In comes first if it is on. The take goes onto the track you selected as another piece on that same track, instead of making a new track every time (a locked track is never recorded over).
 + Count In played five clicks for four numbers, because the last audio block ran one click past the end of the bar. It now plays exactly four clicks at any tempo, and a big Purphex logo sits behind the number in the middle of the count-in circle.
 + Clicking a button a second time now closes the list it opened (Grid Size, Presets and the other menus) instead of opening it again, and the GAIN button opens and closes its panel.
 + Flex Pitch has a scroll bar along the top, and your edits are saved automatically a moment after every change, so nothing is lost if you click away, press FLEX again or close the app.
 + A new slim bar along the bottom fits the grid: hold the left mouse button on the hexagon logo to scroll left and right, use the arrows at the ends, and while music plays the bar becomes a live visualiser with waves flowing out of the logo.
 + Grid Size (renamed), METRONOME in capitals, a close x on the Split vocals + beat and Detect Track key pickers, and the grid lines no longer jump out huge and bright over the tracks when you change the grid size. The hint bar now sits above the bottom bar.
 + New Track now has Beat / Instrumental, so a beat can live on its own track.
 + FULLSCREEN / MINIMISE: the button says what it will do, and MINIMISE always returns to a normal window with its border, even after you maximised with the title bar button.
 + Lyrics: new Open .txt button next to Save .txt to load lyrics from a text file (replace, or add to the end).
 + Settings: new Scan for updates button that checks right now and always tells you the result, even for an update you pressed Later on.
 + Select several clips: hold the left mouse button on empty space and drag a box. Every clip it touches lights up. Drag one to move them all together, press Delete (or DEL) to remove them all with one Ctrl+Z to bring them back, Esc to let go. The needle now moves only from the ruler at the top: clicking empty space no longer moves it.
 + Hover hints and messages: every button now shows a hint when you point at it (a lot of them had none), and long hints and messages grow to show the whole sentence instead of being cut off with three dots. Messages stay on screen long enough to read them.
 + The needle: the strip at the top that moves it now has its own neon border, and the needle is a glowing beam with a bright head. While music plays (or while you drag it) tiny neon sparks spit off the beam like fat in a hot pan.
 + Typing a number into a box: click anywhere else to confirm it (Enter still works and Esc cancels and closes it). A box you open by accident and click off leaves the value exactly as it was. Values are now neon boxes of their own, spaced well away from the sliders and the words so you cannot click them by accident, and every text and number box in the app has the same neon look. Bass Lab knobs use a small neon box that also confirms when you click away.
 + The tempo is found automatically for every track you add (nothing to press): each track header shows its BPM, and the first beat or song also sets the project tempo unless you set one yourself.
 + Phone mode was removed (the website covers it now).

3.19.2  Rapper Vocal Styles: one simple Use button
 + Rapper Vocal Styles now has a single Use button on each style (the second Add button is gone, it was confusing). Use puts the style on the selected track and replaces its plugins, and Ctrl+Z puts them back.

3.19.1  Text fits its boxes everywhere
 + The progress box while splitting vocals now grows to fit its message, and shrinks the text if the window is too small, so nothing runs off the edge.
 + Button labels that are too long for their button (for example SPLIT VOCALS + BEAT or AUTO MIX in a smaller window) now get a smaller size instead of being cut off.
 + Plugin settings keep their full names: the A (automate) button moved to the right end of each row, and the long names in Worldwide Echo and Melody Tune were shortened (Reverb, Duck on voice, Send pattern, Tune speed, Humanise).
 + Rapper Vocal Styles shows each style description in full (it wraps onto two lines), and the automation editor makes room for the names of a list of choices and uses tidy values on its side scale.

3.19.0  Parameter automation
 + New: automation. Press the small A button next to any plugin setting to draw a curve that moves it while the song plays and in every export. Click to add points, drag them, right-click to delete, switch the lane on and off, and flip between the lanes on a track. Lanes are saved with the project.
 + Settings glide smoothly along the curve (updated every 256 frames), and a list of choices such as Off / On switches in steps.
 + Ctrl+Z now also brings back the plugins on a track after you applied a Rapper Vocal Style (it said it would, but only the volume and clip changes were undone before).

3.18.2  NVIDIA turbo engine for vocal splitting
 + PCs with an NVIDIA graphics card can now run the vocal splitting on it with the CUDA version of PyTorch, which is several times faster than before. The app asks once before splitting: it needs a one-time download of about 2.5 GB (about 5 GB on disk). Say No and it never asks again; the normal engines keep working.
 + The turbo engine tests itself the first time: it is only used if it is clearly faster than the processor and gives the same result. If it does not start, or CUDA is not available (for example an old graphics driver), the app quietly goes back to the faster DirectML or processor engine.
 + The separation log (separation.log in the Purphex data folder) now also records which engine and graphics card each split used.

3.18.1  Rapper Vocal Styles, a truthful time-left, faster start for splitting
 + New: Rapper Vocal Styles. 36 original plugin chains for rap vocals in 11 subgenres (Trap, Melodic Trap, Drill, Boom Bap, Old School, Grime and UK, Cloud and Lo-Fi, Rage and Hyper, Pop Rap, Phonk, Storytelling). Search, filter by subgenre, star your favourites, then Use (replace the plugins) or Add (put them after yours). Find it as Vocal Styles in the plugin panel or in Menu > Tools.
 + The time left while splitting vocals + beat is now accurate. It is worked out from how fast the splitting is going right now, not from the whole time so far (which included the start-up and was often several times too high).
 + Splitting starts loading the AI model as soon as you press Split vocals + beat, while you choose the track, so it starts working sooner.
 + The progress message now says what to expect, and every split writes a short line to separation.log in your Purphex data folder (which engine and device it used and how long each step took), so a slow PC can be understood.

3.18.0  Much faster vocal splitting, Worldwide Echo and Melody Tune
 + Split vocals + beat is much faster: the AI engine now uses your graphics card when that is quicker (a 60 second song went from 28 seconds to 10 on the test PC), and a newer, faster processor path when it is not. The result is the same as before.
 + The first split after updating downloads the faster engine once (about 250 MB, 1.4 GB on disk). If it cannot be installed or does not work on your PC, the old engine keeps working.
 + New plugin: Worldwide Echo (7 versions). A wide tempo-synced echo with reverb on the repeats, ducking, and a Send pattern that only echoes chosen moments such as the last beat of each bar.
 + New plugin: Melody Tune (8 versions). Pitch correction to a key and scale with tune speed, strength and humanise, plus harmony layers (thirds, fifths, octaves, choir).
 + Fixed a rare crash in echo-type plugins when a delay time landed exactly on a whole sample.

3.17.10  Lighter on the CPU, faster website
 + The main window animated background no longer hogs the processor in a big or full-screen window: it rests longer after each repaint when the window is large.
 + The website loads faster: smaller logo pictures, compressed pages and long-lived caching.
 + Community songs can have a photo (add it when you share, or change it later from the song).

3.17.9  Audit fixes: safer saves, layers and rows, smoother buttons
 + Project, lyrics, settings and library files are now written safely (a crash or power cut mid-save can no longer leave a half-written file).
 + Duplicating or splitting a layer keeps it on the same row, and Split vocals + beat adds its tracks in the right place, so rows never get out of order.
 + Buttons ease in and out when you hover them, and the window has rounded corners on Windows 11.
 + The website is faster and works properly on phones (menu drawer fixed, no zoom when typing, pages are compressed).

3.17.8  Smoother dragging, Count In + Metronome, Grid size, track pickers, presets
 + Dragging a clip or track is much smoother: the clip picture is no longer redrawn every frame, the zoom stops re-fitting while you hold something, and the grid draws faster.
 + COUNT IN (with the big 1 2 3 4 on screen) and a METRONOME button that really turns the click on and off.
 + Grid size button: says what it is, explains itself when hovered, lights up the grid lines when you change it, and shows a snap guide while you drag.
 + Detect Track key and Split vocals + beat ask which track you want.
 + Presets (was Chain presets) now has bus presets: Echo bus, AdLib bus and Reverb bus. The Buses sheet has a Presets button for each bus.
 + The main window repaints all its buttons after resizing, so going full screen no longer leaves grey blocks.

3.17.7  Key box, vocal remover files, SNAP button replaced
 + Detect Track key now tells you the key in a neon box in the middle of the screen (it tries the selected track, then the others, until it hears a clear key).
 + Vocal remover (main window) explains that you get the vocals, the beat and the original as separate files, and the original is never copied twice.
 + The main window scales with its size: text, buttons and boxes grow and shrink together, so a big window is no longer tiny controls in a big space.
 + Fixed the main window sometimes showing pieces of another window when it was sized up (big back buffers, fully opaque backgrounds).
 + The word BandLab is gone from the main window and the help (links from those sites still work).
 + The SNAP button is gone: the 1 2 3 Count In button took its place. Snap stays on; the Menu can turn it off.

3.17.6  Count In, key finder, shared rows, simpler toolbar, project cards
 + 1 2 3 is the Count In: one bar of clicks before it plays or records (recordings line up with the song).
 + Detect Track key replaces the Auto Mix style box: it finds the key of the selected track.
 + The toolbar lost + TRACK, + BUS and + INSTRUMENT (use + ADD TRACK under the tracks; buses are in each track Bus drop-down). Bass Lab is first in the effect list.
 + Split keeps both pieces on the same track. Drop a clip or a track onto any row and it joins that row, overlapping what is there. Drag a track by its header: the middle of a row shares it, a boundary moves it. The + layers chip on a row separates them.
 + A vertical scroll bar for the tracks and thicker scroll bars everywhere in the studio. Tracks can no longer get stuck above the view.
 + Sounds dropped from the browser land on the row you drop them on.
 + Project pictures show the whole picture in a square (nothing cropped) inside a neon glow. Pick a picture again to refit an old one.
 + Hover a project on the Community page for tools: Open, Rename, Picture, Describe, Tags, Folder. Tags and the one-line description show on the card.
 + Each card shows the size of the project and its audio in KB, MB or GB.

3.17.5  Dragging tracks, no import question
 + Drag a track or clip and you see it: a see-through glowing copy follows the mouse smoothly, the slot it left stays marked, the row it will land on lights up (or shows + new track), and the other rows slide into place.
 + Inside Purphex Music, ADD MUSIC puts the file straight on the chart. The what-is-this-file question is gone.
 + Main window: ADD MUSIC now asks what you want: edit in Purphex Music, vocal remover, or trim the start and end of a file and save it as MP3 or WAV. The Master a song button is gone.

3.17.4  Discord button, one frame-rate readout
 + Community page: Join the Discord button (the invite comes from the website, so it can change without an app update).
 + The extra frame-rate badge in the corner of the converter window is gone; the title bar shows it.

3.17.3  Community page, simpler buses, recording and dragging fixes
 + Community and My Projects pages: no music controls any more (a slim bar instead). Community has a new look: left menu, round neon project cards, profile, tags and activity.
 + Buses are no longer rows on the timeline: every track has a small Bus drop-down, and bus levels and plugins are in + BUS.
 + Recording lands on the empty audio track you made. Drag a clip to another row (or below the last) and it moves there, making a track if needed; Up / Dn make a row when there is none.
 + Only one frame-rate readout now (the title bar).

3.17.2  Buses, empty audio tracks, roomier tracks
 + BUS: press + BUS (or New Track > Bus) for as many buses as you like. On any track press Out to send it to a bus; a bus has its own fader, pan,
   meters and + Effect plugins, like a bus in Logic. Buses are saved with the project.
 + New Track is simple now: Voice / Audio adds an empty raw track (no recording starts, no effects), Virtual Instruments, MIDI Track (play your own
   keyboard) and Bus. Press ARM then MIC to record onto an empty audio track.
 + Track headers are wider and taller, so nothing is squashed.

3.17.1  New Track sheet
 + + ADD TRACK now opens a New Track sheet: record a voice with AutoPitch, a rap vocal, speech, radio voice, guitar or bass
   (each with its own effects already on the track), add an instrument, browse drum beats and loops, or import a file.
 + The glitchy add-track-here strips and + dots between tracks are gone.

3.17.0  Flex Pitch, the whole plugin library, faster and safer
 + NEW FLEX PITCH: select a recording and press FLEX. Every note appears as a block on a piano-roll grid: drag a note up or down to re-tune it, steady its pitch (Drift), shrink or exaggerate the vibrato, change its volume or formant, correct all notes to a key and scale, split and join notes. The track plays your edits live, and DONE saves the edited audio with the project. Undo and Original bring the recording back.
 + 100 more built-in plugins, so all 218 you listed are now in: Vocal Tools, Creative and Bass & EDM (granular, spectral freeze, tape stop, beat repeat, step modulators, risers and impacts, OTT-style dynamics, phaser and flangers, amp and cabinet simulators and more).
 + FPS counter: switch it on in Settings (or View menu in Purphex Music) and the frame rate shows in the title bar.
 + New 120 fps option in Settings (it needs a 120 Hz or faster screen).
 + A full code review found and fixed many problems: effects that could build up or stall, audio glitches while moving a control, extra drawing work in the studio and the converter window, saving settings too often while dragging the window, and more.

3.16.0  Built-in plugin library
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
        var tb = new TextBox { Left = 14, Top = 14, Width = 692, Height = 484, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), Text = Text.Replace("\r\n", "\n").Replace("\n", "\r\n") }; Win.NeonBox(tb); NeonScroll.Attach(tb);
        Win.DarkControl(tb); f.Controls.Add(tb);
        var ok = new PxButton { Text = "Close", Left = 596, Top = 510, Width = 110, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        ok.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); ok.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.Add(ok);
        f.Shown += (s, e) => { tb.SelectionStart = 0; tb.SelectionLength = 0; };
        f.ShowDialog(owner);
    }
}
