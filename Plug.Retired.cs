using System;
using System.Collections.Generic;

// ======================================================================
//  ONE OF EACH TYPE: + ADD EFFECT lists one plugin per kind of effect (Room, Hall, Plate, Spring, Shimmer, Gated, Reverse reverb...;
//  Stereo, Ping-pong, Tape, Analog, Multi-tap, Reverse delay...). Versions that were only the same plugin with other settings
//  (a size, a tone, a note value, a key, a mix) are "retired": they are not listed, but a project that already uses one still
//  opens with it (its plugin is still made when asked for). Every setting they had can be set on the listed plugin.
// ======================================================================
static partial class PluginTables
{
    static readonly HashSet<string> RetiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        // pitch and voice
        "Pitch Shifter ±12 Semitones", "Pitch Shifter ±24 Semitones", "Pitch + Formant Shifter", "Octave Down", "Octave Up", "4-Voice Harmonizer", "Vocal Transposer", "Micro Pitch Shift", "Stereo Detune",
        "Vocal Thickener", "Vocal Character Transformer", "Vocal Chorus Pitch", "Vocal Unison", "Vocal Stacking", "Harmonies Generator", "Spectral Pitch Shifter", "Granular Pitch Shifter", "Pitch Glide",
        "Auto-Tune-Style Pitch Corrector", "Hard Tune", "Robot Pitch", "Automatic Vocal Tuner",
        "Melody Tune: Natural Polish", "Melody Tune: Hard Tune", "Melody Tune: Trap Melody", "Melody Tune: Soft Harmony", "Melody Tune: Choir Stack", "Melody Tune: Octave Dream", "Melody Tune: Power Lead",
        "Demon Voice", "Monster Voice", "Satan Voice", "Lucifer Layer", "Possessed Whisper", "Hell Lord", "Underworld Growl", "Beast Mode Voice", "Exorcist Voice", "Venom Voice", "Hellhound Snarl", "Abyss Choir", "Dark Lord Rap", "Monster Under the Bed",
        // telephone, radio, distortion
        "Telephone EQ", "Vintage Telephone", "Vintage Speaker", "Narrow-Band Filter", "Walkie-Talkie", "Police Radio",
        "Vocal Saturator", "Soft Saturation", "Waveshaper", "Bass Saturator", "Sample-Rate Reducer", "Amplitude Modulator", "Vocal Exciter", "Harmonic Exciter", "Cabinet Simulator", "Speaker Distortion",
        // vocal chains that were settings of other plugins
        "Dark Vocal", "Whisper Enhancer", "Air Enhancer", "Glitch Vocal", "Vocal Clarity Enhancer", "Vocal Spectral Repair", "Vocal Formant Designer", "Vocal Texture Generator", "Whisper-to-Texture Effect", "Low-End Enhancer", "Bass Enhancer",
        // resonators, robots
        "Comb Resonator", "Physical Modelling Resonator", "Dynamic Resonator", "Robot Voice",
        // reverbs and spaces
        "Stereo Reverb", "Dark Reverb", "Vocal Space", "Reverb Ducking", "Convolution Effect", "Impulse Response Loader",
        // delays and echoes
        "Vocal Echo", "Mono Delay", "Tempo-Synced Delay", "Digital Delay", "Filtered Delay", "Telephone Echo", "Dotted-Eighth Delay", "Quarter-Note Delay", "Eighth-Note Delay", "Ping-Pong Vocal Throw",
        "Feedback Delay", "Duck Delay", "Modulated Delay", "Chorus Delay", "Vocal Reverse", "Stadium Echo", "Rap Ad-Lib Echo", "Dark Echo", "Airy Echo", "Telephone Throw", "Infinite Space",
        // EQ and repair
        "Channel EQ", "Vintage EQ", "Analog EQ", "Low Shelf EQ", "High Shelf EQ", "Vocal Presence", "Mud Remover", "Harshness Reducer", "Vocal Resonance Reducer", "Stereo EQ",
        "Vocal De-Esser", "Vocal De-Noise", "Vocal Gate", "Vocal Transient Shaper", "Vocal Width Control", "Bass Mono Maker", "Breath Remover", "Vocal Extractor", "Instrumental Maker",
        // chops, repeats, stops, generators
        "Vocal Chop Generator", "Stutter Vocal", "Buffer Repeat", "Stutter Edit", "Vinyl Stop", "Noise Burst Generator", "Texture Generator", "Subharmonic Synthesizer",
        // modulation
        "Resonance Filter", "Rhythmic Gate", "Rhythmic Filter", "Envelope Follower", "LFO Filter", "Resonant Low-Pass Filter", "Stereo Modulator", "Sidechain Ducking", "Step Modulator", "LFO Modulator",
        "Envelope Modulator", "Beat-Synced Modulator", "Random Modulator", "Stereo Auto-Pan", "Granular Synth Effect", "Frequency Modulator", "Frequency Modulation Effect",
        // censor: one Censor plugin, the sound is its own setting
        "Censor (Bleep)", "Censor (Mute)", "Censor (Reverse)", "Censor (Muffle)", "Censor (Scramble)", "Censor (Radio Static)", "Censor (Duck)", "Clean Version Bleep",
    };
    // the types that were missing as plugins of their own
    static void FillTypes()
    {
        A(ER, "Cathedral Reverb", "reverb2", "mode=5;size=85;decay=5.5;pre=35;damp=6000;hc=10000;mix=26", "A huge stone space with a very long, smooth tail.");
        A(CR, "Vibrato", "mod", "src=0;tgt=4;shape=0;sync=0;rate=5.5;depth=20", "The pitch wobbles up and down like a singer's vibrato (speed and depth).");
        A(VT, "Censor", "censor", "mode=0", "Hides a word: Bleep, Mute, Reverse, Muffle, Scramble, Static or Duck. Right-click a section: Censor this section.");
    }
    public static bool Retired(PluginLibrary.Variant v) { return v.Gen || RetiredNames.Contains(v.Name); }
}
