using System;
using System.Collections.Generic;
using System.Text;

// Vocal tools, creative effects and bass / EDM effects
static partial class PluginTables
{
    const string VT = "Vocal Tools", CR = "Creative", BS = "Bass & EDM";
    const string P16 = "nsteps=16;s1=100;s2=0;s3=100;s4=100;s5=0;s6=100;s7=0;s8=100;s9=100;s10=0;s11=100;s12=100;s13=0;s14=100;s15=100;s16=0";
    const string PSMOOTH = "nsteps=16;s1=100;s2=86;s3=62;s4=34;s5=12;s6=0;s7=4;s8=24;s9=52;s10=78;s11=96;s12=100;s13=70;s14=40;s15=16;s16=4";
    static string ModHide(string keep)   // hides every modulator setting that is not in the list
    {
        var all = new List<string> { "shape", "sync", "rate", "phase", "nsteps", "ftype", "cut", "res", "range", "eatt", "erel", "esens", "pump", "drive" }; for (int i = 1; i <= 16; i++) all.Add("s" + i);
        var k = new HashSet<string>(keep.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); var sb = new StringBuilder(); foreach (var a in all) if (!k.Contains(a)) { if (sb.Length > 0) sb.Append(','); sb.Append(a); }
        return sb.ToString();
    }
    static string Steps16(string keep) { var sb = new StringBuilder(keep); for (int i = 1; i <= 16; i++) sb.Append(",s").Append(i); return sb.ToString(); }
    static readonly string LFOK = "shape,sync,rate", FILTK = "ftype,cut,res,range", ENVK = "eatt,erel,esens";

    static void FillVocal()
    {
        A(VT, "Vocal Chopper", "chop", "sync=7;pmute=25;prep=30;prev=0;ppit=0;rep=4;seed=1", "Slices the vocal on the beat and drops or stutters slices at random (the same every time).", "prev,ppit");
        A(VT, "Vocal Chop Generator", "chop", "sync=7;pmute=30;prep=20;prev=15;ppit=35;seed=11", "Turns a vocal line into a hook of chops: mutes, stutters, reverses and pitch jumps on the beat.");
        A(VT, "Stutter Vocal", "chop", "sync=10;pmute=0;prep=70;prev=0;ppit=0;rep=4;seed=3", "Stutters the vocal in fast repeats on the 1/16 grid.", "pmute,prev,ppit");
        A(VT, "Glitch Vocal", "chop{sync=10;pmute=15;prep=30;prev=20;ppit=25;rep=3;seed=7;fade=0.8} > crush{bits=12;rate=24000;mix=35}", "", "A glitchy, broken-up vocal: chops, reversals and pitch jumps with a touch of digital crush.");
        A(VT, "Vocal Rider", "rider", "", "Keeps a vocal at an even level by moving the volume, slowly and musically, like riding a fader.");
        A(VT, "Vocal Alignment", "align", "", "Lines up a double with the lead vocal: delay, polarity, or find the delay automatically from the vocal track.", "found");
        A(VT, "Vocal Unison", "pshift", "p1=0;c1=-10;l1=70;pan1=-60;d1=12;p2=0;c2=10;l2=70;pan2=60;d2=20;mix=60", "Two quietly detuned copies that make one voice sound like a small group singing together.", HV2);
        A(VT, "Breath Control", "breath", "reduce=10;sens=55", "Turns down the breaths between phrases so they stay natural but quieter.");
        A(VT, "Breath Remover", "breath", "reduce=45;sens=75", "Takes the breaths away almost completely.");
        A(VT, "Plosive Reducer", "plosive", "", "Calms the pops of P and B sounds by lifting a high-pass filter for just an instant.");
        A(VT, "Vocal De-Esser", "deess", "freq=6500;thr=-30;range=10", "Tames harsh S sounds in a vocal.");
        A(VT, "Vocal De-Noise", "denoise", "amount=14;sens=1.8", "Takes hiss and room noise out of a vocal.");
        A(VT, "Vocal Isolation", "vociso", "mode=0;strength=70;hard=50", "A quick way to keep the vocal: it keeps whatever sits in the middle of the stereo picture. The Split vocals + beat button is much better; this is for fast tricks and live use.");
        A(VT, "Vocal Extractor", "vociso", "mode=0;strength=90;hard=70;smooth=40;wide=0", "A harder version of Vocal Isolation that takes the centre out more strictly.");
        A(VT, "Instrumental Maker", "vociso", "mode=1;strength=70;hard=50", "The opposite: removes the centre of the stereo picture to make a rough instrumental.");
        A(VT, "Vocal Gate", "gate", "thr=-46;range=40;att=1;hold=70;rel=180", "Closes between words to remove noise and bleed.");
        A(VT, "Vocal Transient Shaper", "transient", "attack=3;sustain=-2", "Adds or softens the attack of consonants and the body of vowels.");
        A(VT, "Vocal Resonance Reducer", "ressup", "depth=55;sens=60;cut=8;lo=300;hi=7000;rel=100", "Presses down harsh resonances in a voice only while they are there.");
        A(VT, "Vocal Clarity Enhancer", "eq{hp_on=1;hp_f=90;b1_f=300;b1_g=-2;b1_q=1;b3_f=3200;b3_g=2.5;b5_f=11000;b5_g=2} > ressup{depth=35;sens=55} > exciter{drive=6;harm=40;mix=22}", "", "Clears mud, lifts presence, calms harsh peaks and adds a little air in one plugin.");
        A(VT, "Vocal Spectral Repair", "declick > denoise{amount=10} > exciter{drive=5;harm=45;flo=5000;fhi=16000;mix=25}", "", "Repairs clicks, reduces hiss, then refills the top end with gentle harmonics.");
        A(VT, "Vocal Formant Designer", "formant{shift=0} > eq{b0_on=0;b1_f=520;b1_g=3;b1_q=3;b2_f=1500;b2_g=2;b2_q=3;b3_f=2600;b3_g=2;b3_q=3;b4_on=0;b5_on=0}", "", "Shape the vowel colour: a formant shifter followed by three narrow boosts at the vowel formants.");
        A(VT, "Vocal Texture Generator", "vocoder{carrier=3;note=45;chord=3;bands=3;sib=50;dry=10} > grain{size=90;dens=30;pos=200;posj=300;fb=25;dry=40;mix=60} > reverb2{mode=6;decay=3.5;mix=25}", "", "Turns a vocal into a shimmering synthetic texture: vocoder, grains and a big reverb.");
        A(VT, "Vocal Freeze", "freeze", "", "Switch Freeze on and the last moment of the vocal loops as a sustained pad until you switch it off.");
        A(VT, "Vocal Reverse", "echo", "mode=4;sync=7;dry=0;fb=0;lc=20;hc=20000;mix=100", "Plays the vocal backwards in slices that follow your tempo.", "offset,decay,mod,modr,wow,diff,duck,duckr,rev,revsize,fb");
        A(VT, "Vocal Stacking", "pshift", "p1=0;c1=-12;l1=60;pan1=-70;d1=10;p2=0;c2=12;l2=60;pan2=70;d2=17;p3=12;c3=0;l3=25;pan3=-20;d3=5;p4=-12;c4=0;l4=25;pan4=20;d4=8;mix=60", "Four layers around the vocal: two detuned doubles and an octave above and below.", HV4);
        A(VT, "Harmonies Generator", "pshift", "p1=4;l1=50;pan1=-60;d1=10;p2=7;l2=50;pan2=60;d2=14;p3=-3;l3=40;pan3=-20;d3=18;p4=12;l4=35;pan4=20;d4=22;keep=100;mix=55", "Four harmony voices at the intervals you choose (third, fifth, below and octave by default). Change each pitch to suit your key.", HV4);
        A(VT, "Whisper-to-Texture Effect", "vocoder{carrier=2;bands=3;sib=70;dry=0} > reverb2{mode=6;size=70;decay=3;mix=40}", "", "Turns a whisper into an airy, breathing texture.");
        A(VT, "Vocal Width Control", "widener", "width=130;bass=120;mid=0;side=0", "Widens a vocal's stereo image while keeping its low end in the centre.");
        A("EQ & Clarity", "Resonance Filter", "mod", "src=6;tgt=2;depth=0;ftype=1;cut=900;res=10;mix=100", "A resonant band-pass filter that rings at the chosen frequency.", ModHide("ftype,cut,res"));
        A("EQ & Clarity", "Low-End Enhancer", "shaper{split=180;mode=1;drive=9;os=1;mix=70} > sub{mode=1;level=-8;mix=60}", "", "Makes bass bigger and clearer: warm harmonics on the lows plus a quiet sub an octave down.");
    }

    static void FillCreative()
    {
        A(CR, "Granular Synth Effect", "grain", "", "Chops the sound into tiny grains and plays them back scattered, for clouds and textures.");
        A(CR, "Granular Delay", "grain", "size=250;dens=8;pos=600;posj=200;fb=45;dry=70;mix=70;spread=80;lp=6000", "A delay made of grains: each echo is a smeared, scattered fragment.");
        A(CR, "Granular Reverb", "grain", "size=350;dens=45;pos=1200;posj=1500;fb=60;lp=5000;dry=60;mix=70;spread=100", "A reverb made of long overlapping grains, with endless feedback.");
        A(CR, "Reverse Grain Effect", "grain", "rev=100;size=180;dens=16;pos=500;posj=250;dry=40;mix=80", "Every grain plays backwards: a swirl of reversed fragments.");
        A(CR, "Spectral Freeze", "specfx", "mode=0;mix=100", "Freezes one moment of the sound's spectrum into an endless drone. Switch Freeze on to hold it.", "blur,depth,curve,fb");
        A(CR, "Spectral Blur", "specfx", "mode=1;blur=70;mix=100", "Smears the sound in time and frequency into a soft wash.", "freeze,depth,curve,fb");
        A(CR, "Spectral Delay", "specfx", "mode=2;depth=55;fb=35;mix=100", "Delays high frequencies by more than low ones, so every sound sweeps and trails.", "freeze,blur");
        A(CR, "Spectral Pitch Shifter", "pshift", "quality=2;p1=7;keep=60;mix=100", "A smooth phase-vocoder pitch shifter (the 4096 quality setting).", HV1 + ",d1,pan1");
        A(CR, "Wavefolder", "shaper", "mode=7;drive=14;auto=1;tone=14000;os=1", "Folds the waveform back on itself for rich, metallic harmonics.");
        A(CR, "Waveshaper", "shaper", "mode=8;drive=10;auto=1;os=1", "Bends the waveform through a sine curve for smooth, growing harmonics.");
        A(CR, "FM Effect", "fm", "mode=0;freq=220;depth=40;mix=100", "Your sound drives a sine wave's pitch, creating bell and metal tones (FM synthesis).", "fine");
        A(CR, "Frequency Modulator", "fm", "mode=1;freq=180;depth=55;mix=100", "An oscillator wobbles the sound's timing at audio speed for clangorous, vocal-like warps.", "fine");
        A(CR, "Amplitude Modulator", "ring", "mode=1;wave=0;freq=24;depth=100;mix=100", "Modulates the volume at high speed for a buzzing, tremolo-to-ring effect.");
        A(CR, "Resonator Bank", "comb", "mode=0;chord=7;freq=196;fb=88;damp=6000;mix=45", "A bank of tuned resonators playing a chord: sounds ring in tune.", "dyn");
        A(CR, "Comb Resonator", "comb", "mode=0;chord=0;freq=330;fb=90;mix=50", "A single tuned resonance that makes drums and voices sing at a pitch.", "chord,dyn,spread");
        A(CR, "Physical Modelling Resonator", "comb", "mode=0;chord=0;freq=146.8;fb=96;damp=3200;dyn=70;mix=65;snap=1", "A plucked-string model (Karplus-Strong): the sound excites a string that rings and decays.", "chord");
        A(CR, "Convolution Effect", "conv", "ir=7;mix=70;dry=40", "Passes the sound through a metal tank, spring, plate or other space as a creative effect.");
        A(CR, "Impulse Response Loader", "conv", "ir=12;mix=60;dry=60", "Load any impulse response file (a recording of a space or a speaker) and run your sound through it.");
        A(CR, "Tape Stop", "tapestop", "", "The tape slows to a halt then spins back up. Press Stop it now, or let it happen on the beat.");
        A(CR, "Vinyl Stop", "tapestop", "stop=380;start=180;curve=0.7;lp=60", "A quicker, harder stop like pressing a record's brake.");
        A(CR, "Scratch Effect", "scratch", "", "A DJ scratch on the beat: the last slice is dragged back and forth like a record.");
        A(CR, "Buffer Repeat", "repeat", "sync=7;mode=2;grid=1;decay=0;ramp=0;dark=0", "Captures the last moment and repeats it as the bar builds to a drop.", "chance,seed");
        A(CR, "Beat Repeat", "repeat", "sync=10;mode=1;chance=45;grid=1;decay=2;seed=9", "Randomly repeats short slices of the sound, bar by bar.");
        A(CR, "Stutter Edit", "repeat", "sync=10;mode=1;chance=60;grid=1;decay=1.5;ramp=0.5;dark=40;seed=4", "Stutter edit style: repeats rise in pitch and darken as they go.");
        A(CR, "Rhythmic Gate", "mod", "src=1;tgt=0;sync=10;depth=100;smooth=3;" + P16, "Chops the sound into a rhythm of 16 steps: click the steps to draw your pattern.", ModHide(Steps16("sync,nsteps")));
        A(CR, "Rhythmic Filter", "mod", "src=1;tgt=2;sync=10;depth=80;cut=500;res=6;range=3;smooth=6;" + P16, "A filter whose cutoff jumps on a 16-step pattern.", ModHide(Steps16("sync,nsteps,ftype,cut,res,range")));
        A(CR, "Auto-Pan", "mod", "src=0;tgt=1;shape=0;sync=0;rate=0.8;depth=85;mix=100", "Moves the sound from left to right and back.", ModHide("shape,sync,rate,phase"));
        A(CR, "Auto-Tremolo", "mod", "src=0;tgt=0;shape=0;sync=0;rate=6;depth=70;smooth=2;mix=100", "Raises and lowers the volume in a steady pulse.", ModHide("shape,sync,rate,phase"));
        A(CR, "Sidechain Pump", "mod", "src=5;tgt=0;sync=4;depth=75;pump=55;smooth=2;mix=100", "The classic EDM pump: the volume dips on every beat and swells back (follows your tempo, no kick needed).", ModHide("sync,pump"));
        A(CR, "Envelope Follower", "mod", "src=2;tgt=2;depth=100;cut=300;res=5;range=4;esens=70;mix=100", "A filter that opens and closes with the loudness of the sound itself: auto-wah.", ModHide(ENVK + "," + FILTK));
        A(CR, "Transient Designer", "transient", "attack=6;sustain=4;speed=100", "Shape the punch (attack) and the tail (sustain) of drums and instruments separately.");
        A(CR, "Noise Burst Generator", "gen", "kind=5;grid=4;level=-14", "Adds a short noise burst on every beat division: hats, snaps and transitions.", "every,len,from,to,res,tone,width");
        A(CR, "Riser Generator", "gen", "kind=0;every=8;len=4;level=-12", "A rising noise sweep that builds into the next section, locked to your bars.", "from,to,grid");
        A(CR, "Downlifter Generator", "gen", "kind=1;every=8;len=4;level=-12", "A falling noise sweep after a hit: the drop-away after a drop.", "from,to,grid");
        A(CR, "Impact Generator", "gen", "kind=2;every=8;len=2;level=-6", "A cinematic impact with a sub boom on the first beat of every cycle.", "from,to,grid,res");
        A(CR, "Sub Drop Generator", "gen", "kind=3;every=8;len=2;level=-6", "A sine that dives from a high note to a rumble: the classic sub drop.", "grid,res,tone,width");
        A(CR, "Whoosh Generator", "gen", "kind=4;every=8;len=2;level=-14", "A band of noise that sweeps up and back down: a whoosh or swell.", "from,to,grid");
        A(CR, "Texture Generator", "gen", "kind=6;level=-20", "A slowly changing bed of filtered noise with soft pings, for atmosphere.", "every,len,from,to,grid");
    }

    static void FillBass()
    {
        A(BS, "Wobble Bass Filter", "mod", "src=0;tgt=2;shape=0;sync=7;depth=90;ftype=0;cut=220;res=8;range=3.2;smooth=2;mix=100", "The classic dubstep and DnB wobble: a resonant filter swept by a tempo-synced LFO.", ModHide(LFOK + "," + FILTK));
        A(BS, "LFO Filter", "mod", "src=0;tgt=2;shape=1;sync=0;rate=1.2;depth=75;cut=900;res=3;range=2.5;mix=100", "A filter that is swept slowly by an LFO.", ModHide(LFOK + "," + FILTK));
        A(BS, "Auto Filter", "mod", "src=2;tgt=2;depth=80;cut=250;res=4;range=4;esens=60;eatt=3;erel=160;mix=100", "A filter that opens when you play harder.", ModHide(ENVK + "," + FILTK));
        A(BS, "Envelope Filter", "mod", "src=2;tgt=2;depth=100;cut=180;res=7;range=4.5;esens=75;eatt=2;erel=90;mix=100", "A funky envelope filter that quacks with every note.", ModHide(ENVK + "," + FILTK));
        A(BS, "Resonant Low-Pass Filter", "mod", "src=6;tgt=2;depth=0;ftype=0;cut=1200;res=8;mix=100", "A low-pass filter with adjustable resonance.", ModHide("ftype,cut,res"));
        A(BS, "Acid Filter", "mod{src=2;tgt=2;depth=100;cut=250;res=14;range=3.2;eatt=1;erel=140;esens=70} > shaper{mode=3;drive=12;tone=9000;mix=60}", "", "A squelchy, screaming 303-style resonant filter with a little drive.");
        A(BS, "Bass Enhancer", "shaper{split=220;mode=1;drive=8;os=1;mix=70} > sub{mode=1;level=-9;mix=50}", "", "Adds warmth to the low end and a quiet sub below it so bass is heard on small speakers.");
        A(BS, "Subharmonic Synthesizer", "sub", "mode=0;level=-4", "Makes a new bass note an octave below the sound (a classic divider circuit).", "");
        A(BS, "Sub-Bass Generator", "sub", "mode=1;level=-3", "A pure sine that follows the bass note an octave below.", "");
        A(BS, "Bass Mono Maker", "widener", "width=100;bass=170;mid=0;side=0", "Keeps everything below 170 Hz in the centre so the bass is solid on every system.");
        A(BS, "Multiband Distortion", "mbdist", "", "Three bands (low, mid, high) with their own distortion type, drive and level.");
        A(BS, "Bass Saturator", "shaper", "mode=1;drive=12;split=240;os=1;tone=9000;mix=85;auto=1", "Saturates only the lows (below 240 Hz) so bass grows without harshness up top.");
        A(BS, "OTT-Style Multiband Dynamics", "ott", "", "Squashes loud parts and lifts quiet ones in three bands: huge, bright and glossy.");
        A(BS, "Dynamic Resonator", "comb", "mode=0;chord=2;freq=110;fb=82;damp=5000;dyn=100;mix=40;snap=1", "Tuned resonances that bloom when the sound is loud and fade when it is quiet.", "");
        A(BS, "Phaser", "phaser", "", "A swirling phaser with up to 24 stages, tempo sync and stereo offset.");
        A(BS, "Flanger", "flanger", "barber=0", "A classic flanger: short sweeping delay with feedback.", "barber,bshift");
        A(BS, "Barberpole Flanger", "flanger", "barber=1;fb=70;bshift=0.4;mix=60", "A flanger that seems to rise (or fall) forever, using a frequency shifter in the feedback.");
        A(BS, "Frequency Modulation Effect", "fm", "mode=1;freq=90;depth=40;mix=100", "Wobbles the timing of the sound at sub-audio to audio speed for growls and metallic movement.", "fine");
        A(BS, "Stereo Modulator", "mod", "src=0;tgt=5;shape=0;sync=0;rate=0.4;depth=80;phase=90;mix=100", "Swirls the stereo width with a slow LFO.", ModHide("shape,sync,rate,phase"));
        A(BS, "Harmonic Exciter", "exciter", "drive=10;harm=60;flo=2500;fhi=18000;mix=40", "Adds new harmonics for bite, air and presence.");
        A(BS, "Bass Amp Simulator", "amp", "pre=5;gain=16;cab=4;bass=3;mid=-2;level=-3;tight=40", "A tube bass amp and an 8x10 cabinet: warm, round and punchy.");
        A(BS, "Cabinet Simulator", "amp", "pre=0;gain=0;cab=3;level=0", "Puts any sound through a speaker cabinet (1x12, 2x12, 4x12, bass cabs, radio and more).", "gain,tight,bass,mid,midf,treble,pre");
        A(BS, "Speaker Distortion", "amp", "pre=4;gain=26;cab=6;level=-5", "Fuzz pushed through a small broken speaker.");
        A(BS, "Amp Overdrive", "amp", "pre=2;gain=22;cab=1", "A cranked guitar amp and combo cabinet.");
        A(BS, "Sidechain Ducking", "mod", "src=5;tgt=0;sync=4;depth=85;pump=70;smooth=1;mix=100", "Ducks the sound on every beat to make room for the kick (follows your tempo).", ModHide("sync,pump"));
        A(BS, "Volume Shaper", "mod", "src=1;tgt=0;sync=10;depth=100;smooth=12;" + PSMOOTH, "Draw a repeating volume curve with 16 steps, locked to the beat.", ModHide(Steps16("sync,nsteps")));
        A(BS, "Gate Sequencer", "mod", "src=1;tgt=0;sync=10;depth=100;smooth=2;" + P16, "A step gate: choose which of 16 steps let sound through.", ModHide(Steps16("sync,nsteps")));
        A(BS, "Step Filter", "mod", "src=1;tgt=2;sync=10;depth=85;cut=400;res=7;range=3.5;smooth=8;" + P16, "A resonant filter that jumps between 16 cutoff values.", ModHide(Steps16("sync,nsteps,ftype,cut,res,range")));
        A(BS, "Step Modulator", "mod", "src=1;tgt=1;sync=8;depth=90;smooth=10;" + P16, "A 16-step pattern that moves the sound left and right.", ModHide(Steps16("sync,nsteps")));
        A(BS, "LFO Modulator", "mod", "src=0;tgt=2;shape=1;sync=4;depth=60;cut=700;res=2.5;range=2;mix=100", "A tempo-synced LFO that moves a filter (or choose another target).", ModHide(LFOK + ",phase," + FILTK));
        A(BS, "Envelope Modulator", "mod", "src=2;tgt=0;depth=70;esens=60;eatt=4;erel=120;mix=100", "The sound's own level controls its volume, gain or filter (choose the target).", ModHide(ENVK + "," + FILTK));
        A(BS, "Beat-Synced Modulator", "mod", "src=4;tgt=2;sync=7;depth=80;cut=500;res=4;range=3;mix=100", "A rising ramp that restarts on every beat division and sweeps a filter.", ModHide("sync," + FILTK));
        A(BS, "Random Modulator", "mod", "src=3;tgt=2;sync=8;depth=70;cut=600;res=4;range=3;smooth=15;mix=100", "Random values (sample and hold) that move a filter on the beat.", ModHide("sync," + FILTK));
        A(BS, "Stereo Auto-Pan", "mod", "src=0;tgt=1;shape=1;sync=3;depth=100;phase=0;mix=100", "A tempo-synced triangle pan that sweeps between the speakers.", ModHide("shape,sync,rate,phase"));
    }
}
