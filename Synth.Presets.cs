using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Factory presets (grouped by style) and the user's own presets (one text file each, in the app's data folder).
static class SynthPresets
{
    public static readonly string[] Categories = { "Wobble", "Reese", "Growl", "Dubstep", "DnB", "Neuro", "Riddim", "Sub", "Lead", "Pluck", "Pad", "Keys", "SFX", "Init" };
    class E { public string Cat, Name, Data; }
    static readonly List<E> All = new List<E>();
    public static string[] Names;
    static void A(string cat, string name, string data) { All.Add(new E { Cat = cat, Name = name, Data = data }); }

    // shared pieces
    const string Bass = "aon=1;awt=50;aoct=-1;alvl=85;subon=1;suboct=-1;sublvl=60;f1on=1;f1type=0;f1slope=1;f1cut=500;f1res=30;f1drive=20;bbon=1;bbfreq=55;bbgain=6;bbharm=20;";
    const string W2 = "lc1=0:-1:0.6:0,0.3:1:-0.5:0,0.5:-0.2:0.5:0,0.7:1:-0.5:0,1:-1:0:0;l1shape=7;";
    const string W3 = "lc1=0:1:-0.7:0,0.12:-1:0:0,0.25:0.6:-0.6:0,0.4:-1:0:0,0.55:1:-0.7:0,0.7:-1:0:0,1:-1:0:0;l1shape=7;";
    const string Chug = "lc1=0:1:0:0,0.125:1:0:0,0.125:-1:0:0,0.25:-1:0:0,0.25:1:0:0,0.375:1:0:0,0.375:-1:0:0,0.5:-1:0:0,0.5:1:0:0,0.5625:1:0:0,0.5625:-1:0:0,0.625:-1:0:0,0.625:1:0:0,0.75:1:0:0,0.75:-1:0:0,1:-1:0:0;l1shape=7;";

    static SynthPresets()
    {
        // ---- Wobble ----
        A("Wobble", "Wobble Bass", "aon=1;awt=45;aoct=-1;auni=3;adet=14;alvl=85;bon=1;bwt=75;boct=-1;blvl=55;bfin=7;subon=1;suboct=-1;sublvl=65;f1on=1;f1type=1;f1cut=500;f1res=42;f1drive=25;e2a=0;e2d=420;e2s=0;" +
            "m1s=5;m1d=17;m1a=25;m2s=1;m2d=17;m2a=58;m3s=2;m3d=9;m3a=22;m4s=10;m4d=17;m4a=60;m5s=11;m5d=18;m5a=35;l1sync=8;l1shape=0;l2sync=5;l2shape=6;bbon=1;bbfreq=55;bbgain=7;bbharm=25;dston=1;dstmode=2;dstdrive=9;dstmix=30;mac1=0;mac2=0");
        A("Wobble", "Slow Wub", Bass + "awt=48;auni=3;adet=12;f1cut=380;f1res=45;m1s=1;m1d=17;m1a=70;l1sync=5;l1shape=0;dston=1;dstdrive=8;dstmix=25;");
        A("Wobble", "Triplet Wobble", Bass + "awt=55;auni=2;adet=10;f1cut=420;f1res=50;m1s=1;m1d=17;m1a=68;l1sync=7;l1shape=1;m2s=1;m2d=9;m2a=25;");
        A("Wobble", "Half-Time Wob", Bass + W2 + "awt=60;auni=3;adet=14;f1cut=450;f1res=48;m1s=1;m1d=17;m1a=75;l1sync=4;dston=1;dstmode=1;dstdrive=10;dstmix=35;");
        A("Wobble", "Wide Wobble", Bass + "awt=50;auni=5;adet=22;bon=1;bwt=50;boct=-1;buni=5;bdet=18;blvl=55;f1cut=520;m1s=1;m1d=17;m1a=60;l1sync=8;m2s=1;m2d=22;m2a=25;chon=1;chmix=30;wdon=1;wdwidth=150;");
        // ---- Reese ----
        A("Reese", "Reese Bass", "aon=1;awt=50;aoct=-1;auni=5;adet=26;alvl=80;bon=1;bwt=50;boct=-1;bfin=-14;buni=5;bdet=22;blvl=75;subon=1;sublvl=55;f1on=1;f1type=1;f1cut=650;f1res=18;f1drive=18;" +
            "m1s=1;m1d=17;m1a=22;l1sync=3;l1shape=0;m2s=2;m2d=22;m2a=35;l2sync=2;l2shape=0;chon=1;chmix=35;bbon=1;bbfreq=52;bbgain=7;bbharm=18");
        A("Reese", "Dark Reese", Bass + "auni=5;adet=30;bon=1;bwt=50;boct=-1;bfin=-18;buni=5;bdet=26;blvl=75;f1cut=380;f1res=20;f1slope=2;m1s=1;m1d=17;m1a=30;l1sync=2;phon=1;phmix=30;phrate=0.2;");
        A("Reese", "Reese Sweep", Bass + "auni=5;adet=24;bon=1;bwt=50;boct=-1;bfin=12;buni=5;bdet=20;blvl=70;f1cut=300;f1res=25;m1s=1;m1d=17;m1a=60;l1sync=1;l1shape=1;m2s=2;m2d=22;m2a=30;l2sync=3;chon=1;chmix=30;");
        A("Reese", "Reese Stab", Bass + "auni=5;adet=28;bon=1;bwt=50;boct=-1;bfin=-10;buni=5;bdet=24;blvl=70;f1cut=900;f1res=20;e1d=220;e1s=20;e1r=140;e2d=250;e2s=0;m1s=5;m1d=17;m1a=45;saton=1;satdrive=8;satmix=60;");
        // ---- Growl ----
        A("Growl", "Growl Bass", "aon=1;awt=60;aoct=-1;auni=3;adet=10;alvl=85;awarp=3;wamt=0;awamt=0;bon=1;bwt=40;boct=-1;blvl=60;subon=1;sublvl=60;f1on=1;f1type=1;f1cut=900;f1res=50;f1drive=40;" +
            "m1s=1;m1d=9;m1a=65;l1sync=13;l1shape=0;m2s=2;m2d=17;m2a=55;l2sync=11;l2shape=6;m3s=5;m3d=10;m3a=30;e2d=300;e2s=0;dston=1;dstmode=1;dstdrive=14;dstmix=55;bbon=1;bbfreq=58;bbgain=8;bbharm=30");
        A("Growl", "Formant Growl", Bass + "awt=55;auni=3;adet=12;f1type=9;f1cut=900;f1res=60;f1drive=35;m1s=1;m1d=17;m1a=70;l1sync=5;l1shape=1;dston=1;dstdrive=12;dstmix=40;");
        A("Growl", "Screech Growl", Bass + "awt=55;awarp=3;awamt=40;auni=3;adet=14;f1cut=1500;f1res=45;f1drive=40;m1s=1;m1d=9;m1a=70;l1sync=11;l1shape=6;m2s=2;m2d=17;m2a=40;l2sync=8;dston=1;dstmode=1;dstdrive=16;dstmix=55;");
        A("Growl", "Acid Squelch", "mode=2;glide=90;aon=1;awt=66;aoct=-1;auni=1;alvl=90;bon=0;subon=1;sublvl=45;f1on=1;f1type=1;f1cut=350;f1res=72;f1drive=35;e1d=300;e1s=70;e2a=0;e2d=260;e2s=10;m1s=5;m1d=17;m1a=55;m2s=1;m2d=17;m2a=20;l1sync=11;dston=1;dstmode=0;dstdrive=8;dstmix=25;bbon=1;bbfreq=62;bbgain=5");
        // ---- Dubstep ----
        A("Dubstep", "Dub Wub", Bass + W2 + "awt=58;auni=3;adet=14;f1cut=420;f1res=55;f1drive=35;m1s=1;m1d=17;m1a=80;l1sync=4;m2s=1;m2d=9;m2a=20;dston=1;dstmode=2;dstdrive=14;dstmix=40;");
        A("Dubstep", "Brostep Mid", Bass + "awt=60;aoct=0;auni=3;adet=14;alvl=75;awarp=3;awamt=35;f1cut=1800;f1res=45;f1drive=40;suboct=-2;sublvl=50;m1s=1;m1d=17;m1a=65;l1sync=8;m2s=2;m2d=9;m2a=45;l2sync=11;dston=1;dstmode=1;dstdrive=16;dstmix=55;");
        A("Dubstep", "Robot Voice", Bass + "aoct=0;asrc=1;alvl=70;bon=1;bsrc=1;boct=1;blvl=0;afm=45;f1cut=2500;f1res=40;m1s=1;m1d=9;m1a=40;l1sync=11;l1shape=1;dston=1;dstdrive=14;dstmix=40;sublvl=40;");
        A("Dubstep", "Heavy Drop", Bass + W3 + "awt=62;auni=5;adet=18;bon=1;bwt=40;boct=-2;blvl=70;f1cut=350;f1res=55;f1drive=45;m1s=1;m1d=17;m1a=85;l1sync=3;dston=1;dstmode=1;dstdrive=18;dstmix=60;cpon=1;cpthr=-24;cpratio=5;cpmake=4;");
        // ---- DnB ----
        A("DnB", "DnB Roller", Bass + "awt=52;auni=2;adet=8;f1cut=700;f1res=25;e1d=350;e1s=85;m1s=5;m1d=17;m1a=25;e2d=250;e2s=30;cpon=1;cpthr=-20;cpratio=4;cpmake=3;");
        A("DnB", "Jump-Up Wob", Bass + "awt=58;auni=3;adet=12;f1cut=400;f1res=50;f1drive=30;m1s=1;m1d=17;m1a=75;l1sync=8;l1shape=0;m2s=1;m2d=9;m2a=15;dston=1;dstdrive=11;dstmix=35;");
        A("DnB", "Liquid Sub", "aon=1;awt=0;aoct=-1;auni=1;alvl=80;subon=1;sublvl=55;f1on=1;f1type=0;f1slope=0;f1cut=500;f1res=10;e1a=6;e1d=400;e1s=80;e1r=250;bbon=1;bbfreq=50;bbgain=6;bbharm=15;rvon=1;rvsize=30;rvmix=8;");
        A("DnB", "Amen Stab", Bass + "awt=65;auni=3;adet=16;f1cut=1200;f1res=35;e1d=180;e1s=0;e1r=120;e2d=200;e2s=0;m1s=5;m1d=17;m1a=55;saton=1;satdrive=10;satmix=70;");
        // ---- Neuro ----
        A("Neuro", "Neuro Bite", Bass + "awt=58;awarp=3;awamt=30;auni=3;adet=14;bon=1;bwt=50;boct=-1;blvl=55;bfin=9;f1cut=700;f1res=50;f1drive=40;m1s=1;m1d=17;m1a=60;l1sync=10;l1shape=6;m2s=2;m2d=9;m2a=35;l2sync=11;phon=1;phmix=35;phrate=0.3;dston=1;dstdrive=14;dstmix=45;");
        A("Neuro", "Neuro Chop", Bass + "awt=55;auni=3;adet=16;f1cut=600;f1res=55;f1drive=35;m1s=1;m1d=17;m1a=90;l1sync=11;l1shape=4;m2s=2;m2d=9;m2a=40;l2sync=8;l2shape=0;dston=1;dstmode=1;dstdrive=15;dstmix=50;");
        A("Neuro", "Metallic Neuro", Bass + "awt=50;auni=1;bon=1;bwt=50;boct=0;blvl=0;arm=50;f1cut=1500;f1res=45;m1s=1;m1d=17;m1a=60;l1sync=8;m2s=2;m2d=9;m2a=50;l2sync=10;flon=1;flmix=45;flfb=60;dston=1;dstdrive=12;dstmix=40;");
        A("Neuro", "Neuro Phaser", Bass + "awt=60;auni=5;adet=20;f1cut=650;f1res=40;m1s=1;m1d=17;m1a=55;l1sync=5;phon=1;phmix=60;phrate=0.5;phfb=65;phstages=2;");
        // ---- Riddim ----
        A("Riddim", "Riddim Chug", Bass + Chug + "awt=60;auni=3;adet=12;f1cut=420;f1res=50;f1drive=30;m1s=1;m1d=17;m1a=85;l1sync=3;dston=1;dstmode=1;dstdrive=14;dstmix=45;");
        A("Riddim", "Riddim Triplet", Bass + "awt=55;auni=2;adet=10;f1cut=400;f1res=55;m1s=1;m1d=17;m1a=80;l1sync=7;l1shape=4;m2s=1;m2d=9;m2a=15;dston=1;dstdrive=12;dstmix=40;");
        A("Riddim", "Minimal Riddim", Bass + "awt=50;auni=1;f1cut=350;f1res=40;m1s=1;m1d=17;m1a=70;l1sync=5;l1shape=4;sublvl=70;");
        A("Riddim", "Skank Bass", Bass + "awt=62;auni=3;adet=10;f1cut=800;f1res=35;e1d=160;e1s=0;e1r=90;e2d=140;e2s=0;m1s=5;m1d=17;m1a=60;saton=1;satdrive=9;satmix=70;");
        // ---- Sub ----
        A("Sub", "Deep Sub", "aon=1;awt=0;aoct=-1;auni=1;alvl=90;subon=1;subwave=0;suboct=-1;sublvl=60;f1on=1;f1type=1;f1cut=400;f1res=5;f1drive=0;e1a=4;e1d=300;e1s=90;e1r=200;bbon=1;bbfreq=45;bbgain=9;bbharm=22;bbq=0.9");
        A("Sub", "808 Sub", "mode=1;glide=60;aon=1;asrc=1;aoct=-1;auni=1;alvl=90;subon=0;f1on=1;f1type=0;f1slope=0;f1cut=300;f1res=0;e1a=0;e1d=1200;e1s=0;e1r=300;e2d=60;e2s=0;m1s=5;m1d=5;m1a=6;bbon=1;bbfreq=45;bbgain=8;bbharm=25;saton=1;satdrive=6;");
        A("Sub", "Sub Square", "aon=1;asrc=4;aoct=-1;auni=1;alvl=70;subon=1;sublvl=60;f1on=1;f1type=0;f1slope=2;f1cut=250;f1res=5;e1a=3;e1d=300;e1s=90;e1r=150;bbon=1;bbfreq=50;bbgain=6;");
        A("Sub", "Clean Sub Boost", "aon=1;asrc=1;aoct=-1;auni=1;alvl=85;subon=1;sublvl=40;f1on=0;e1a=3;e1d=300;e1s=95;e1r=180;bbon=1;bbfreq=40;bbgain=14;bbharm=35;bbtight=25;");
        // ---- Lead ----
        A("Lead", "Supersaw Lead", "aon=1;awt=50;aoct=0;auni=7;adet=34;alvl=80;bon=1;bwt=50;boct=0;bfin=6;buni=5;bdet=26;blvl=60;subon=0;f1on=1;f1type=0;f1cut=6500;f1res=10;e1a=8;e1d=400;e1s=75;e1r=300;m1s=1;m1d=2;m1a=0;chon=1;chmix=30;dlon=1;dlsync=3;dlmix=22;rvon=1;rvmix=22;bbon=0");
        A("Lead", "Square Lead", "aon=1;asrc=4;aoct=0;auni=3;adet=10;alvl=75;subon=0;f1on=1;f1type=0;f1cut=4500;f1res=15;e1a=4;e1d=300;e1s=80;e1r=200;m1s=1;m1d=5;m1a=2;l1sync=0;l1rate=5.5;dlon=1;dlmix=18;bbon=0;");
        A("Lead", "Mono Saw Lead", "mode=2;glide=70;aon=1;asrc=3;aoct=0;auni=3;adet=14;alvl=80;subon=0;f1on=1;f1type=0;f1cut=3500;f1res=22;e1a=6;e1d=300;e1s=85;e1r=220;m1s=5;m1d=17;m1a=20;e2d=350;e2s=30;dlon=1;dlmix=15;bbon=0;");
        A("Lead", "FM Bell Lead", "aon=1;asrc=1;aoct=0;auni=1;alvl=70;bon=1;bsrc=1;boct=2;blvl=0;afm=45;f1on=1;f1type=0;f1cut=9000;e1a=2;e1d=900;e1s=20;e1r=500;rvon=1;rvsize=55;rvmix=25;bbon=0;");
        // ---- Pluck ----
        A("Pluck", "Pluck Bass", "aon=1;awt=70;aoct=-1;auni=2;adet=8;alvl=85;bon=1;bwt=25;boct=0;blvl=35;subon=1;sublvl=55;f1on=1;f1type=1;f1cut=300;f1res=30;e1a=0;e1d=160;e1s=0;e1r=90;e2a=0;e2d=170;e2s=0;m1s=5;m1d=17;m1a=60;bbon=1;bbfreq=60;bbgain=6");
        A("Pluck", "Short Pluck", "aon=1;asrc=3;aoct=0;auni=2;adet=10;alvl=80;f1on=1;f1type=0;f1cut=700;f1res=25;e1a=0;e1d=110;e1s=0;e1r=60;e2d=130;e2s=0;m1s=5;m1d=17;m1a=70;dlon=1;dlsync=3;dlmix=15;bbon=0;");
        A("Pluck", "Soft Pluck", "aon=1;asrc=2;aoct=0;auni=2;adet=8;alvl=80;f1on=1;f1type=0;f1cut=1500;f1res=10;e1a=2;e1d=300;e1s=0;e1r=180;e2d=250;e2s=0;m1s=5;m1d=17;m1a=45;rvon=1;rvmix=20;bbon=0;");
        A("Pluck", "Bell Pluck", "aon=1;asrc=1;aoct=1;auni=1;alvl=70;bon=1;bsrc=1;boct=3;blvl=0;afm=38;f1on=0;e1a=0;e1d=700;e1s=0;e1r=500;e2d=500;e2s=0;rvon=1;rvsize=60;rvmix=28;bbon=0;");
        // ---- Pad ----
        A("Pad", "Soft Pad", "aon=1;awt=35;aoct=0;auni=5;adet=22;alvl=75;bon=1;bwt=60;boct=0;bfin=5;buni=3;bdet=18;blvl=50;subon=0;f1on=1;f1type=0;f1cut=2400;f1res=12;e1a=700;e1d=900;e1s=80;e1r=1100;m1s=1;m1d=17;m1a=18;l1sync=3;chon=1;chmix=40;rvon=1;rvsize=70;rvmix=30;bbon=0");
        A("Pad", "Dark Pad", "aon=1;awt=48;aoct=-1;auni=5;adet=24;alvl=75;bon=1;bwt=30;boct=0;blvl=40;subon=0;f1on=1;f1type=0;f1cut=900;f1res=15;e1a=900;e1d=1000;e1s=85;e1r=1400;m1s=1;m1d=17;m1a=25;l1sync=2;phon=1;phmix=25;rvon=1;rvsize=80;rvmix=35;bbon=0;");
        A("Pad", "Shimmer Pad", "aon=1;awt=40;aoct=1;auni=7;adet=30;alvl=65;bon=1;bwt=50;boct=0;blvl=45;f1on=1;f1type=0;f1cut=7000;f1res=8;e1a=800;e1d=1000;e1s=85;e1r=1600;m1s=1;m1d=22;m1a=20;l1sync=1;chon=1;chmix=45;flon=1;flmix=25;rvon=1;rvsize=85;rvmix=40;wdon=1;wdwidth=160;bbon=0;");
        A("Pad", "Choir Pad", "aon=1;atbl=2;awt=30;aoct=0;auni=3;adet=12;alvl=80;f1on=1;f1type=0;f1cut=5000;f1res=5;e1a=500;e1d=800;e1s=85;e1r=1100;m1s=1;m1d=6;m1a=22;l1sync=1;l1shape=0;rvon=1;rvsize=75;rvmix=35;bbon=0;");
        // ---- Keys ----
        A("Keys", "Electric Keys", "aon=1;asrc=2;aoct=0;auni=2;adet=6;alvl=80;bon=1;bsrc=1;boct=2;blvl=15;f1on=1;f1type=0;f1cut=3500;f1res=8;e1a=2;e1d=700;e1s=30;e1r=250;chon=1;chmix=25;rvon=1;rvmix=15;bbon=0;");
        A("Keys", "Organ", "aon=1;asrc=9;aoct=0;auni=1;alvl=80;f1on=0;e1a=4;e1d=200;e1s=100;e1r=90;m1s=1;m1d=5;m1a=1;l1sync=0;l1rate=6;chon=1;chmix=30;rvon=1;rvmix=12;bbon=0;");
        A("Keys", "Bell Keys", "aon=1;asrc=1;aoct=0;auni=1;alvl=70;bon=1;bsrc=1;boct=2;blvl=0;afm=30;f1on=0;e1a=1;e1d=1100;e1s=0;e1r=600;rvon=1;rvsize=60;rvmix=25;bbon=0;");
        A("Keys", "Clav Keys", "aon=1;asrc=5;aoct=0;auni=1;alvl=80;f1on=1;f1type=0;f1cut=2500;f1res=30;e1a=0;e1d=260;e1s=10;e1r=120;e2d=200;e2s=0;m1s=5;m1d=17;m1a=45;bbon=0;");
        // ---- SFX ----
        A("SFX", "Riser", "aon=1;asrc=3;aoct=0;auni=5;adet=40;alvl=70;bon=1;bsrc=3;boct=1;blvl=40;nzon=1;nzlvl=25;f1on=1;f1type=1;f1slope=1;f1cut=400;f1res=40;e1a=3000;e1d=2000;e1s=100;e1r=600;e2a=3500;e2d=1000;e2s=100;m1s=5;m1d=17;m1a=90;m2s=5;m2d=5;m2a=25;rvon=1;rvmix=30;bbon=0;");
        A("SFX", "Laser", "aon=1;asrc=1;aoct=1;auni=1;alvl=80;f1on=0;e1a=0;e1d=350;e1s=0;e1r=150;e2a=0;e2d=250;e2s=0;m1s=5;m1d=5;m1a=-60;dlon=1;dlmix=25;bbon=0;");
        A("SFX", "Impact", "aon=1;asrc=1;aoct=-2;auni=1;alvl=90;subon=1;sublvl=70;nzon=1;nzlvl=35;nztype=2;f1on=1;f1type=0;f1cut=500;e1a=0;e1d=1800;e1s=0;e1r=800;e2a=0;e2d=600;e2s=0;m1s=5;m1d=17;m1a=55;m2s=5;m2d=5;m2a=-25;rvon=1;rvsize=90;rvmix=40;saton=1;satdrive=12;satmix=70;bbon=1;bbgain=9;");
        A("SFX", "Alien Noise", "aon=1;asrc=4;aoct=0;auni=3;adet=60;alvl=60;bon=1;bsrc=2;boct=1;blvl=0;nzon=1;nzlvl=12;nztype=4;f1on=1;f1type=2;f1slope=1;f1cut=1800;f1res=70;e1a=200;e1d=600;e1s=70;e1r=600;m1s=1;m1d=17;m1a=60;l1sync=0;l1rate=7;l1shape=5;m2s=2;m2d=5;m2a=10;l2sync=0;l2rate=0.4;flon=1;flmix=40;dlon=1;dlmix=30;rvon=1;rvmix=35;bbon=0;");
        A("Init", "Init", "aon=1;awt=0;aoct=0;auni=1;alvl=80;subon=0;f1on=0;f1cut=20000;bbon=0;e1a=2;e1d=300;e1s=100;e1r=120;vol=-10");
        Names = new string[All.Count]; for (int i = 0; i < All.Count; i++) Names[i] = All[i].Name;
    }

    public static List<string> InCategory(string cat) { var l = new List<string>(); foreach (var e in All) if (e.Cat == cat) l.Add(e.Name); return l; }
    public static string CategoryOf(string name) { foreach (var e in All) if (e.Name == name) return e.Cat; return UserNames().Contains(name) ? "My presets" : ""; }
    static string Find(string name) { foreach (var e in All) if (e.Name == name) return e.Data; return null; }
    public static string DataOf(string name) { return Find(name); }

    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "synthpresets"); } }
    static string FileFor(string name) { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return Path.Combine(Dir, name.Trim() + ".txt"); }

    // a clean slate first (knobs, LFO shapes, drawn waves), then the preset on top
    public static void Apply(SynthFx fx, string name, bool resetWaves = true)
    {
        fx.ResetAll(resetWaves);
        string d = Find(name);
        if (d != null) fx.Deserialize(d);
        else { try { string f = FileFor(name); if (File.Exists(f)) fx.Deserialize(File.ReadAllText(f)); } catch { } }
        fx.ApplyGlobals(); fx.PresetName = name; fx.MarkDirty(); fx.Reset();
    }
    public static void Initialise(SynthFx fx) { Apply(fx, "Init"); }
    public static bool IsFactory(string name) { return Find(name) != null; }
    public static bool Exists(string name) { return IsFactory(name) || File.Exists(FileFor(name)); }
    public static List<string> UserNames()
    {
        var l = new List<string>(); try { if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir, "*.txt")) l.Add(Path.GetFileNameWithoutExtension(f)); } catch { }
        l.Sort(StringComparer.OrdinalIgnoreCase); return l;
    }
    // written to a temp file first and then moved, so a crash can never leave half a preset behind
    public static void SaveUser(SynthFx fx, string name)
    {
        name = name.Trim(); if (name.Length == 0) throw new Exception("Give the preset a name."); if (IsFactory(name)) throw new Exception("\"" + name + "\" is a built-in preset name. Pick another name.");
        Directory.CreateDirectory(Dir); string f = FileFor(name), tmp = f + ".tmp"; File.WriteAllText(tmp, fx.Serialize(), Encoding.UTF8);
        if (File.Exists(f)) File.Delete(f); File.Move(tmp, f); fx.PresetName = name;
    }
    public static void DeleteUser(string name) { string f = FileFor(name); if (File.Exists(f)) File.Delete(f); }
    public static void RenameUser(string from, string to)
    {
        to = to.Trim(); if (to.Length == 0 || IsFactory(to)) throw new Exception("Pick another name."); string a = FileFor(from), b = FileFor(to);
        if (!File.Exists(a)) throw new Exception("That preset no longer exists."); if (File.Exists(b) && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) throw new Exception("A preset with that name already exists.");
        File.Move(a, b);
    }
    public static string DuplicateUser(string from)
    {
        string n = from + " copy"; int i = 2; while (Exists(n)) n = from + " copy " + (i++);
        Directory.CreateDirectory(Dir);
        if (IsFactory(from)) File.WriteAllText(FileFor(n), Find(from), Encoding.UTF8); else File.Copy(FileFor(from), FileFor(n));
        return n;
    }
}
