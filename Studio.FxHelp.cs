using System;
using System.Collections.Generic;

// =====================================================================================================
//  WHAT EACH PLUGIN DOES, IN PLAIN WORDS: for every effect and VST in the list, what it does to the sound and what to use it for.
//  The plugin list shows both under the name; hovering a plugin on a track shows them too. Keyed by the plugin's name.
// =====================================================================================================
static class FxHelp
{
    // a plugin on a track: what it does and what to use it for (the effect library's own notes are already plain, so they stay as they are)
    public static string About(Fx f) { if (f == null) return ""; string what = PluginLibrary.IsLibrary(f.Type) || !Has(f.Title) ? (f.Blurb ?? "") : What(f.Title, f.Blurb), u = Use(f.Title); return (what + (u.Length > 0 ? "  Use it for: " + u : "")).Trim(); }
    // name -> { what it does, use it for }
    static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    static void A(string name, string what, string use) { map[name] = new[] { what, use }; }
    public static string What(string name, string fallback) { string[] v; return map.TryGetValue(name ?? "", out v) ? v[0] : (fallback ?? ""); }
    public static string Use(string name) { string[] v; return map.TryGetValue(name ?? "", out v) ? v[1] : ""; }
    public static bool Has(string name) { return map.ContainsKey(name ?? ""); }
    // the whole explanation on one line (for hover hints)
    public static string Full(string name, string fallback) { string u = Use(name); return What(name, fallback) + (u.Length > 0 ? "  Use it for: " + u : ""); }

    static FxHelp()
    {
        // ---- the basics ----
        A("Parametric EQ", "Turns chosen frequencies up or down: more bass, less mud, more sparkle. Drag the dots on the curve.", "fixing the tone of anything: cut mud around 300 Hz, add air on top, clean rumble with the low cut.");
        A("Compressor", "Turns the loud parts down so the volume stays even and the sound feels tighter and closer.", "vocals that jump in volume, punchier drums, a bass that sits steady.");
        A("Multiband Compressor", "A compressor split into lows, mids and highs, so each part is evened out on its own.", "controlling a boomy low end or harsh top without squashing the whole sound; mastering.");
        A("Limiter", "A ceiling the sound can never go over: makes it louder without ever clipping.", "the very end of the master to make the song loud and safe.");
        A("Noise Gate", "Silences the sound whenever it gets quiet, cutting out hiss and background noise between parts.", "removing room noise between vocal lines, tightening drums.");
        A("Vocal Ducker", "Turns this track down a little whenever the vocal plays, then back up when it stops.", "a beat or pad so the vocal always sits on top.");
        A("De-esser", "Turns down sharp S and T sounds only when they happen.", "vocals with harsh, hissy S sounds.");
        A("Exciter", "Adds new high harmonics so a sound gets brighter, clearer and more present.", "dull vocals, sleepy hats, making a sound cut through.");
        A("Saturation", "Gently overdrives the sound for warmth and thickness, like old tape or tube gear.", "making digital sounds warmer and fuller: vocals, bass, drums, the whole mix.");
        A("Distortion", "Drives the sound hard until it breaks up: crunchy, gritty, aggressive.", "dirty 808s, gritty synths, rough vocal effects.");
        A("Reverb", "Puts the sound in a room or hall, adding space and a tail after it.", "giving vocals and instruments space and depth.");
        A("Delay", "Repeats the sound as echoes, in time with the song if you want.", "echo throws on vocals, rhythmic repeats on synths and guitars.");
        A("Chorus", "Adds slightly moving copies of the sound, making it wider and shimmering.", "wide pads, thicker guitars and keys, dreamy vocals.");
        A("Stereo Imager", "Makes the sound wider or narrower, and can keep the bass in the middle.", "widening pads and backing vocals; keeping the low end tight and centred.");
        A("Noise Reduction", "Learns the background hiss or hum and removes it.", "noisy recordings: phone mics, room hum, fan noise.");
        A("Pitch Correction", "Pulls each sung note to the nearest note of the key: subtle tuning or the hard robotic effect.", "tuning vocals, or the hard snapped vocal sound when set fast.");
        A("8D Audio Tuner", "Moves the sound in circles around your head. Best on headphones.", "8D-style edits and moments where a sound should spin around the listener.");
        A("3D Sound Stage", "Places the sound anywhere around you: left, right, in front, behind, close or far. Headphones.", "putting ad-libs and effects in their own spot around the listener.");
        A("Bass Boost", "Raises the low end at the frequency you pick, and can add harmonics so the bass shows on small speakers.", "making kicks and 808s hit harder, especially on phones and laptops.");
        A("Bass Lab (synthesizer)", "A full synth that makes its own sound from notes: basses, leads, pads, wobbles.", "making 808s, bass lines, leads and pads from scratch in the piano roll.");
        A("Purphex Wobble", "A bass synth built for wobbles: a filter that moves in time with the song, vowels, grit and sub.", "dubstep and riddim wobble basses, growls and talking basses.");
        A("Purphex Pads", "16 pads, each playing any sound you pick: from the sound library, a file on your PC or one of your tracks.", "playing your own drum kit from the pads of your MIDI keyboard.");
        A("Purphex Sampler", "Plays your own sounds from notes: one sample at any pitch, or a whole drum kit on the keys.", "playing kicks, snares and 808s in the piano roll; turning any sound into an instrument.");
        A("Gain", "Turns the volume up or down before the next plugin.", "fixing a sound that is too quiet or too loud going into an effect.");
        // ---- the Purphex VSTs ----
        A("Purphex GlitchFlow", "Chops, repeats, stutters and freezes the sound in time with the song.", "glitchy fills, stutter edits on vocals, beat-repeat transitions.");
        A("Purphex Glow", "Brightens and enriches the lows, mids, highs and air separately, with a bit of warmth.", "making a vocal or the whole mix sound more expensive and polished.");
        A("Purphex Curve EQ", "An 8-band EQ on a live picture of the sound; bands can react only when a frequency gets loud.", "precise tone fixes while you see what you are cutting or boosting.");
        A("Purphex Shape", "A compressor and punch shaper in one, with its own EQ and a clipper.", "drums and vocals that need control and punch in one plugin.");
        A("Purphex SubForge", "An 808 and bass instrument: punch, sub, glide between notes, filter and distortion.", "trap and drill 808s with slides, from notes in the piano roll.");
        A("Purphex BreathOff", "Finds the breaths in a vocal and turns them down smoothly.", "cleaning up vocals without cutting breaths by hand.");
        A("Purphex Phantom Gate", "A gate that silences the gaps and lets the hits through, with look-ahead so nothing is cut short.", "cleaning drum recordings and vocals; tight, punchy drums.");
        A("Purphex Stereo Lab", "Shows how wide your sound is and lets you change width, pan and the mid / side balance.", "checking and fixing stereo width; keeping bass mono.");
        A("Purphex Vortex", "Moves the sound around in 3D: spinning, swaying, with space and echo.", "making ad-libs, synths and effects move around the listener.");
        A("Purphex MultiDyn", "A five-band compressor that can learn your sound, with a limiter at the end.", "mastering and evening out a full mix or a busy instrument.");
        A("Purphex Prism", "Makes any sound ring at the notes or chord you pick, like it is played through tuned strings.", "turning drums or noise into musical, tuned textures.");
        A("Purphex Frost", "Freezes the sound into a held texture, with metallic ring, echoes and movement.", "ambient pads from any sound, frozen transitions.");
        A("Purphex Tape Dirt", "Makes the sound like it is playing from an old worn tape: warm, wobbly, with drop-outs.", "lo-fi beats, vintage vibes, dusty loops.");
        A("Purphex Constellation", "Breaks the sound into tiny grains and scatters them into a pitched cloud with reverb.", "ambient textures, dreamy pads, strange sound design.");
        A("Purphex Clipper", "Cuts the tops off the loudest peaks so the sound gets louder and punchier.", "loud drums and 808s, the master before the limiter.");
        A("Purphex Smack", "Turns up or down the hit (attack) and the tail (sustain) of every sound.", "punchier kicks and snares, or softer, rounder drums.");
        A("Purphex Vinyl Stop", "Slows the sound down to a stop like a record or tape being stopped.", "dramatic stops before a drop or at the end of a section.");
        A("Purphex Radio", "Makes the sound like a broken radio: static, tuning, a thin band, crackle.", "intros, phone-like vocals, lo-fi breaks.");
        A("Purphex Doubler", "Makes up to four copies of the sound, each slightly different, so it sounds like several takes.", "thick, wide vocals and guitars without recording doubles.");
        A("Purphex Octave Choir", "Adds up to eight harmony voices that follow the key.", "instant backing harmonies and choirs on a vocal or lead.");
        A("Purphex Vowel", "A filter that makes sounds say vowels like A, E, I, O, U.", "talking synths and basses, wah effects.");
        A("Purphex Step Shaper", "Sixteen steps of volume and filter moves in time with the song.", "choppy, rhythmic pads and chords; trance gates.");
        A("Purphex Tri-Drive", "Distortion on the lows, mids and highs separately, with a filter and compressor.", "heavy basses and synths that stay clear in the low end.");
        A("Purphex Shift", "Moves every frequency up or down by the same amount: strange, metallic, phasing sounds.", "sci-fi effects, swirling textures, sound design.");
        A("Purphex Maximizer", "One knob to make the mix loud, with tone, mono bass and a loudness target.", "the last plugin on the master for a release-ready loudness.");
        A("Purphex Spectral Gate", "Removes quiet background frequency by frequency, keeping only the sound you want.", "removing spill and noise from vocals and drums.");
        A("Purphex EchoGrid", "Eight echoes placed on a grid, each with its own pitch, tone and grit.", "rhythmic echo patterns that become part of the groove.");
        A("Purphex Modal Filter", "Two filters that morph between types and move by themselves.", "evolving synths and pads, filter sweeps.");
        A("Purphex Reference", "Compares your mix with a song you like at the same volume: tone, loudness, width.", "checking your mix against a professional song.");
        A("Purphex Pathfinder", "A reverse echo: the repeats swell in backwards before each sound, in time with the song.", "dreamy vocal and guitar effects, transitions.");
        A("Purphex Modular", "Four effect slots (drive, fold, crush, filters and more), each moved automatically.", "experimental sound design, evolving bass and synth textures.");
        A("Purphex Dreamscape", "A huge ambient space that can shimmer, freeze and bloom.", "ambient intros, dreamy pads and vocals.");
        A("Purphex Morph", "Blends or vocodes this track with another track you pick.", "talking synths, blending two sounds into a new one.");
        A("Purphex Current", "A synth with two morphing waveforms, thick unison and movement.", "modern leads, pads and basses.");
        A("Purphex Inferno", "A growl and wobble bass synth with heavy distortion.", "dubstep, riddim and heavy EDM basses.");
        A("Purphex ChordPad", "Plays full chords in any key from one key or pad, with strum and voicings.", "making chord progressions without knowing music theory.");
        A("Purphex Drum2MIDI", "Listens to a drum track, turns it into notes and plays them with its own kit.", "replacing or layering the drums of a loop or recording.");
        // ---- pitch and voice ----
        A("Pitch Shifter", "Moves the pitch up or down while keeping the voice natural.", "changing the key of a vocal or sample.");
        A("Formant Shifter", "Makes a voice sound deeper or smaller without changing the note.", "character vocals, gender or size changes.");
        A("Octaver", "Adds a note an octave below (and a quieter one above).", "fatter bass lines and leads.");
        A("Vocal Doubler", "Two detuned, delayed copies left and right.", "a wide, double-tracked vocal from one take.");
        A("Vocal Harmonizer", "Adds two harmony notes above the vocal.", "quick backing harmonies.");
        A("Unison Voice", "Four detuned copies spread wide, like several singers.", "big hooks and chants.");
        A("Real-Time Pitch Shifter", "Shifts the pitch with almost no delay.", "playing or singing live through an effect.");
        A("Melody Tune", "Tunes the vocal to the key and can add harmonies that follow it.", "natural vocal tuning with harmonies.");
        A("Croaky Demon Voice", "Deep, croaking, distorted demon voice in a dark space.", "horror, villain or dark ad-libs.");
        A("Vocal Fry / Croak", "Adds a creaky, croaky texture to a voice.", "gritty, raspy vocal character.");
        // ---- telephone and distortion ----
        A("Telephone Vocal", "Makes the sound thin and crunchy like a phone call.", "intros, bridges and vocal breakdowns.");
        A("Radio Voice", "Sounds like an old AM radio.", "intros, skits and retro vocal effects.");
        A("Megaphone", "Boxy and distorted, like a loud-hailer.", "shouted hooks and aggressive ad-libs.");
        A("Lo-Fi Vocal", "Dusty, crushed and warm, like a cassette sample.", "lo-fi hip-hop vocals and samples.");
        A("Band-Pass Filter", "Keeps only one band of frequencies.", "thin, filtered intros and breakdowns.");
        A("Tube Saturation", "Warm tube drive.", "warming vocals, bass and keys.");
        A("Tape Saturation", "Smooth tape warmth and soft highs.", "gluing drums or a whole mix.");
        A("Overdrive", "Guitar-pedal crunch.", "gritty guitars, keys and basses.");
        A("Hard Clipper", "Chops off the peaks: loud and aggressive.", "loud, in-your-face drums and 808s.");
        A("Soft Clipper", "Rounded clipping for loudness without harshness.", "drums, 808s and the master before the limiter.");
        A("Fuzz", "A thick, buzzy fuzz.", "dirty guitars, synths and basses.");
        A("Bitcrusher", "Lowers the sound quality for a retro, gritty, video-game feel.", "lo-fi drums, chiptune vibes, gritty effects.");
        A("Ring Modulator", "Bell-like, metallic and robotic tones.", "robot voices and sci-fi effects.");
        A("Frequency Shifter", "Shifts every frequency the same amount: strange and inharmonic.", "weird sound design and rising spirals.");
        A("Comb Filter", "Metallic, tube-like resonance.", "metallic textures and flanging effects.");
        A("Vocoder", "Your voice makes a synth talk.", "robot vocals and talking synths.");
        A("Talkbox-Style Effect", "A talking-synth sound made with the vocoder.", "funky, talking lead lines.");
        // ---- echo and reverb ----
        A("Room Reverb", "A small natural room.", "making dry recordings sound real and close.");
        A("Hall Reverb", "A big, smooth concert hall.", "ballads, strings, big vocals.");
        A("Plate Reverb", "A bright, dense vocal reverb.", "lead vocals and snares.");
        A("Chamber Reverb", "A warm echo chamber.", "vocals and instruments that need warm space.");
        A("Spring Reverb", "The drippy sound of a guitar-amp spring.", "guitars, dub and surf vibes.");
        A("Shimmer Reverb", "A huge reverb with a glowing octave above.", "dreamy pads, ambient intros.");
        A("Gated Reverb", "A big reverb cut off short.", "80s snares and punchy drums.");
        A("Reverse Reverb", "The reverb swells in backwards.", "intros and vocal lead-ins.");
        A("Ambient Reverb", "A slow drifting wash.", "pads and atmospheres.");
        A("Early Reflections", "Just a sense of room, no tail.", "making sounds feel in a space without washing them out.");
        A("Convolution Reverb", "The sound of real recorded spaces.", "realistic halls, churches and rooms.");
        A("Cathedral Reverb", "A huge stone space with a very long tail.", "epic, haunting vocals and pads.");
        A("Stereo Delay", "Separate left and right echoes.", "wide echo on vocals and synths.");
        A("Ping-Pong Delay", "Echoes bounce left and right.", "vocal throws and bouncy synths.");
        A("Slapback Echo", "One short repeat that thickens.", "rap and rock vocals.");
        A("Tape Echo", "Warm, wobbly, dark repeats.", "vintage vocal and guitar echoes.");
        A("Analog Echo", "Murky repeats that darken.", "warm dub-style echoes.");
        A("Multi-Tap Delay", "Four fading echoes left and right.", "rhythmic echo patterns.");
        A("Reverse Delay", "Backwards swells into each phrase.", "dreamy vocal effects.");
        A("Worldwide Echo", "A wide tempo echo with reverb that ducks while you sing.", "clean vocal echoes that never get in the way.");
        // ---- EQ and clarity ----
        A("Graphic EQ", "Ten sliders for broad tone shaping.", "quick, simple tone changes.");
        A("Dynamic EQ", "An EQ band that only works when that frequency gets loud.", "taming harsh notes only when they jump out.");
        A("Linear-Phase EQ", "A clean EQ that does not smear the sound.", "mastering and careful EQ.");
        A("Low-Pass Filter", "Removes the highs above a frequency.", "muffled intros, filter sweeps, darker sounds.");
        A("High-Pass Filter", "Removes the lows below a frequency.", "cleaning rumble from vocals and everything that is not bass.");
        A("Notch Filter", "Cuts one very narrow frequency.", "removing a whistle, hum or ring.");
        A("Formant Filter", "A talking vowel filter.", "voice-like wah on synths, bass and drums.");
        A("Tilt EQ", "One knob: brighter or darker.", "fast tone balance on anything.");
        A("Multiband De-Esser", "Tames harsh S sounds and hiss together.", "very sibilant vocals.");
        A("Spectral Shaper", "Reshapes the whole tone and smooths or exaggerates peaks.", "balancing a sound's overall tone.");
        A("Resonance Suppressor", "Finds harsh ringing frequencies and presses them down automatically.", "harsh vocals, boxy recordings, ringing snares.");
        A("Mid-Side EQ", "EQ the centre and the sides separately.", "wider mixes and a cleaner low end.");
        A("Automatic EQ", "Listens and steers the tone toward a natural, bright, warm or vocal curve.", "quick tone fixes when you are not sure what to change.");
        // ---- vocal tools ----
        A("Vocal Chopper", "Slices the vocal on the beat and stutters or drops slices.", "chopped vocal hooks and glitchy edits.");
        A("Vocal Rider", "Rides the vocal volume to keep it even.", "vocals that get loud and quiet.");
        A("Vocal Alignment", "Lines up a double with the lead vocal.", "tight double tracks.");
        A("Breath Control", "Turns down breaths between phrases.", "cleaner vocals that still sound natural.");
        A("Plosive Reducer", "Calms P and B pops.", "vocals recorded too close to the mic.");
        A("Vocal Isolation", "Keeps what sits in the middle of the stereo picture.", "quick vocal tricks and live use.");
        A("Vocal Freeze", "Holds the last moment of the vocal as a pad.", "vocal pads and ambient transitions.");
        A("Censor", "Hides a word with a bleep, mute, reverse or other effect.", "clean versions of a song.");
        // ---- creative ----
        A("Granular Delay", "Echoes made of smeared, scattered grains.", "textured, experimental echoes.");
        A("Granular Reverb", "A reverb of long overlapping grains.", "huge ambient textures.");
        A("Reverse Grain Effect", "A swirl of reversed fragments.", "strange transitions and textures.");
        A("Spectral Freeze", "Freezes one moment into an endless drone.", "drones and pads from any sound.");
        A("Spectral Blur", "Smears the sound into a soft wash.", "dreamy backgrounds.");
        A("Spectral Delay", "Highs echo later than lows: every sound sweeps.", "sweeping, trailing effects.");
        A("Wavefolder", "Rich, metallic harmonics.", "aggressive synths and basses.");
        A("FM Effect", "Bell and metal tones from your sound.", "metallic sound design.");
        A("Resonator Bank", "Makes sounds ring in tune with a chord.", "tuned drums and textures.");
        A("Tape Stop", "The sound slows to a halt and spins back up.", "dramatic stops before a drop.");
        A("Scratch Effect", "A DJ scratch on the beat.", "hip-hop fills and transitions.");
        A("Beat Repeat", "Repeats short slices at random.", "stutter fills and glitchy variation.");
        A("Auto-Pan", "Moves the sound left and right.", "moving hats, pads and keys.");
        A("Auto-Tremolo", "A steady pulsing volume.", "vintage keys and guitars.");
        A("Sidechain Pump", "The volume dips on every beat and swells back.", "EDM pumping pads and basses.");
        A("Transient Designer", "Shapes the punch and tail of a sound.", "punchier or softer drums.");
        A("Riser Generator", "A rising noise sweep into the next section.", "build-ups before a drop.");
        A("Downlifter Generator", "A falling sweep after a hit.", "after drops and section changes.");
        A("Impact Generator", "A cinematic boom on the first beat.", "big drops and section starts.");
        A("Sub Drop Generator", "A sine that dives into a rumble.", "drops and transitions.");
        A("Whoosh Generator", "A noise swell up and down.", "transitions and fills.");
        A("Vibrato", "The pitch wobbles like a singer's vibrato.", "expressive leads and vocals.");
        // ---- bass and EDM ----
        A("Wobble Bass Filter", "A filter swept in time with the song.", "dubstep and DnB wobbles.");
        A("Auto Filter", "A filter that opens when you play harder.", "expressive synths and basses.");
        A("Envelope Filter", "A funky quack on every note.", "funk basses and guitars.");
        A("Acid Filter", "A squelchy screaming resonant filter.", "acid basslines and leads.");
        A("Sub-Bass Generator", "Adds a pure sub an octave below.", "deeper basses and kicks.");
        A("Multiband Distortion", "Different distortion on lows, mids and highs.", "heavy basses that keep a clean low end.");
        A("OTT-Style Multiband Dynamics", "Squashes loud parts and lifts quiet ones in three bands: big, bright and glossy.", "EDM synths, basses and drums.");
        A("Phaser", "A swirling, sweeping sound.", "synths, keys and guitars.");
        A("Flanger", "A jet-like sweep.", "synths, drums and transitions.");
        A("Barberpole Flanger", "A sweep that seems to rise forever.", "endless risers and tension.");
        A("Bass Amp Simulator", "A tube bass amp and cabinet.", "warm, punchy bass guitars.");
        A("Amp Overdrive", "A cranked guitar amp.", "rock guitars and gritty synths.");
        A("Volume Shaper", "Draw a repeating volume curve.", "pumping and choppy rhythms.");
        A("Gate Sequencer", "Choose which of 16 steps let sound through.", "trance gates and choppy pads.");
        A("Step Filter", "A filter that jumps between 16 positions.", "rhythmic synth patterns.");
    }
}
