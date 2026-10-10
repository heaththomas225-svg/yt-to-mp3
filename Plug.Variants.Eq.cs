using System;

// EQ and clarity plugins
static partial class PluginTables
{
    const string EQC = "EQ & Clarity";
    const string DYN2 = "b2_on,b2_m,b2_f,b2_q,b2_t,b2_r,b3_on,b3_m,b3_f,b3_q,b3_t,b3_r";

    static void FillEq()
    {
        A(EQC, "Graphic EQ", "geq", "g1=1.5;g2=1;g8=2;g9=3", "Ten sliders, one per octave, for quick broad tone shaping.");
        A(EQC, "Channel EQ", "eq", "hp_on=1;hp_f=70;b0_f=100;b0_g=2;b1_f=250;b1_g=-2;b1_q=1.2;b2_f=1500;b2_g=1.5;b3_f=3500;b3_g=2.5;b4_on=0;b5_f=10000;b5_g=2", "A console-style channel strip EQ: high-pass, low shelf, two mid bells and a high shelf.");
        A(EQC, "Vintage EQ", "eq", "b0_f=80;b0_g=3;b0_q=0.5;b1_f=250;b1_q=0.5;b1_g=-1.5;b2_f=1000;b2_q=0.5;b3_f=3500;b3_g=2.5;b3_q=0.5;b4_f=7000;b4_q=0.5;b5_f=12000;b5_g=3;lp_on=1;lp_f=17000", "Wide, gentle curves and a softly rolled-off top, like classic studio hardware.");
        A(EQC, "Analog EQ", "eq", "b0_f=60;b0_g=2.5;b0_q=0.6;b1_f=200;b1_g=1.5;b1_q=0.6;b2_f=1200;b2_g=-1;b2_q=0.6;b3_f=4000;b3_g=1.5;b3_q=0.6;b4_on=0;b5_f=9000;b5_g=2", "Smooth, musical broad boosts: hard to make sound bad.");
        A(EQC, "Dynamic EQ", "dyneq", "", "An EQ band that only works when its frequencies get loud: cut a harsh note only when it jumps out, or boost on loud parts.");
        A(EQC, "Linear-Phase EQ", "lineq", "b0_g=2;b3_f=3500;b3_g=3", "The six-band EQ with zero phase shift, for mastering and parallel work. Adds a little delay (about 70 ms), which Purphex compensates for.");
        A(EQC, "Low-Pass Filter", "eq", "lp_on=1;lp_f=3500;lp_s=1", "Removes everything above a chosen frequency. 12 or 24 dB per octave.");
        A(EQC, "High-Pass Filter", "eq", "hp_on=1;hp_f=150;hp_s=1", "Removes everything below a chosen frequency (rumble, plosives, muddy lows).");
        A(EQC, "Notch Filter", "eq", "b2_t=3;b2_f=1000;b2_q=6;b2_g=0", "Cuts a very narrow frequency to remove a whistle, hum or ring. Set Freq with the band 3 handle.");
        A(EQC, "Formant Filter", "vowel", "vowel=0;q=9;shape=1;rate=0.8;depth=2", "A talking filter that moves between the vowel sounds A E I O U: a voice-like wah for synths, bass and drums.");
        A(EQC, "Tilt EQ", "tilt", "tilt=2;pivot=650", "One knob that makes a sound brighter or darker by tilting the whole spectrum around a pivot.");
        A(EQC, "Low Shelf EQ", "eq", "b0_f=120;b0_g=4;b0_q=0.7;b5_g=0", "Raises or lowers everything below a frequency.");
        A(EQC, "High Shelf EQ", "eq", "b5_f=9000;b5_g=4;b0_g=0", "Raises or lowers everything above a frequency: add air or take off harshness.");
        A(EQC, "Multiband De-Esser", "deess{freq=5200;thr=-30;range=8} > deess{freq=8800;thr=-34;range=6}", "", "Two de-essers at once, to tame both the harsh S around 5 kHz and the hiss up at 9 kHz.");
        A(EQC, "Vocal Presence", "eq", "hp_on=1;hp_f=90;b3_f=3500;b3_g=3.5;b3_q=0.9;b4_f=5500;b4_g=1.5;b4_q=1", "Brings a vocal forward in the mix with a lift around 3-6 kHz and a low cut.");
        A(EQC, "Air Enhancer", "eq{b5_f=10500;b5_g=3.5;b0_on=0} > exciter{drive=6;harm=50;flo=8000;fhi=20000;mix=28}", "", "Adds silky top-end air with a gentle shelf plus new high harmonics.");
        A(EQC, "Spectral Shaper", "specshape", "high=2;peaks=-20", "Reshapes the whole spectrum: lows, mids, highs and tilt, and flattens or exaggerates its peaks.");
        A(EQC, "Resonance Suppressor", "ressup", "", "Automatically finds and presses down harsh ringing peaks in the spectrum, only while they are there.");
        A(EQC, "Mud Remover", "dyneq", "b1_on=1;b1_f=260;b1_q=1.0;b1_t=-34;b1_r=7", "Clears boxy, muddy lows (around 260 Hz) but only when they build up.", DYN2);
        A(EQC, "Harshness Reducer", "dyneq", "b1_on=1;b1_f=3400;b1_q=1.0;b1_t=-36;b1_r=6;b2_on=1;b2_f=6200;b2_q=1.2;b2_t=-38;b2_r=5", "Softens harsh upper-mid and treble spikes (3-7 kHz) only when they poke out.", "b3_on,b3_m,b3_f,b3_q,b3_t,b3_r");
        A(EQC, "Stereo EQ", "mseq", "mode=1;a_lg=0;b_lg=0;a_mf=2500;a_mg=1;b_mf=2500;b_mg=-1", "Separate EQ for the left and right sides: fix a lopsided recording or make a deliberately uneven stereo tone.");
        A(EQC, "Mid-Side EQ", "mseq", "mode=0;a_lf=100;a_lg=1.5;b_hf=9000;b_hg=3;b_lf=200;b_lg=-4", "EQ the centre (mid) and the sides separately: tighten the sides' lows and open up their highs for width.");
        A(EQC, "Automatic EQ", "autoeq", "", "Listens to your sound and gently steers its overall tone toward a target curve (natural, bright, warm or vocal).");
    }
}
