using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

// The Mastering tab as a little program of its own: its own top bars (only the buttons that belong to mastering), a separate PLAY and PREVIEW, AUTO MIX that listens to the whole mix and
// builds the EQ by itself and gives three versions to hear, master presets you can try before you keep them (Bass Boost Master, Vocal Enhance Master and more), automatic loudness
// (and manual for the picky), and an EQ picture that shows the sound before and after.

sealed class MasterPreset { public string Name, Blurb; public float Target; public string[] Slots; public string Group; }
static class MasterPresets
{
    // slots: Parametric EQ, Compressor, Multiband, Exciter, Saturation, Stereo imaging, Limiter (null = that module is off)
    static MasterPreset[] all;
    public static MasterPreset[] All { get { if (all == null) { var l = new List<MasterPreset>(Base); l.AddRange(MasterPresetsMore.All); all = l.ToArray(); } return all; } }
    public static int IndexOf(string namePrefix) { var a = All; for (int i = 0; i < a.Length; i++) if (a[i].Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)) return i; return 0; }
    public static readonly string[] Groups = { "Everyday", "Loud", "Genre", "Vocals", "Low end", "Colour", "Width", "Fix" };
    static readonly MasterPreset[] Base = {
        new MasterPreset { Name = "Loud / Club (-9 LUFS)", Target = -9, Group = "Loud", Blurb = "Big and dense for clubs and playlists that like it loud: low end lifted, multiband control, harmonics and a hard limiter.", Slots = new[]{ "hp_on=1;hp_f=28;b0_f=70;b0_g=1.5;b5_f=11000;b5_g=2", "thr=-16;ratio=3;att=20;rel=140;knee=6;makeup=1", "t0=-22;r0=2.5;t1=-24;r1=2.5;t2=-26;r2=2.5", "drive=6;harm=35;mix=20", "drive=4;mix=50", "width=112;bass=130", "ceil=-0.8;gain=8;rel=50" } },
        new MasterPreset { Name = "Gentle (-16 LUFS)", Target = -16, Group = "Everyday", Blurb = "Very light touch for dynamic music: nearly no compression, a little shine on top and plenty of headroom.", Slots = new[]{ "b5_g=0.5;b5_f=10000", "thr=-12;ratio=1.6;att=40;rel=250;knee=10;makeup=0", null, null, null, "width=100;bass=100", "ceil=-1.5;gain=0;rel=120" } },
        new MasterPreset { Name = "Warm & wide (-12 LUFS)", Target = -12, Group = "Colour", Blurb = "A rounder low end, softer top, tape warmth and a wider stereo picture.", Slots = new[]{ "b0_f=100;b0_g=2;b5_f=9000;b5_g=-1", "thr=-15;ratio=2.2;att=25;rel=180;knee=8;makeup=0.5", null, null, "drive=5;mix=60", "width=125;bass=140", "ceil=-1;gain=5;rel=70" } },
        new MasterPreset { Name = "Bright (-13 LUFS)", Target = -13, Group = "Colour", Blurb = "More presence and sparkle: a lift around 4 kHz and the top end, with exciter harmonics.", Slots = new[]{ "b3_f=4000;b3_g=1.5;b5_f=11000;b5_g=3", "thr=-14;ratio=2;att=25;rel=160;knee=8;makeup=0.5", null, "drive=6;harm=45;mix=25", null, "width=112;bass=120", "ceil=-1;gain=4;rel=80" } },
        new MasterPreset { Name = "Bass Boost Master (-10 LUFS)", Target = -10, Group = "Low end", Blurb = "A deep, tight low end that still sounds clear: a low shelf with the mud cleaned out, the bass band compressed so it stays even, tape harmonics so it shows up on small speakers, bass kept in the centre.", Slots = new[]{ "hp_on=1;hp_f=24;b0_t=1;b0_f=80;b0_g=5;b0_q=0.7;b1_f=220;b1_g=-1.4;b1_q=1.1;b5_f=11000;b5_g=0.8", "thr=-17;ratio=2.4;att=32;rel=170;knee=8;makeup=0.5", "xo1=140;t0=-18;r0=2.4;a0=30;l0=200;m0=2;t1=-24;r1=2;t2=-26;r2=2", null, "mode=0;drive=5;tone=9000;mix=38", "width=100;bass=150", "ceil=-0.8;gain=5;rel=90" } },
        new MasterPreset { Name = "Vocal Enhance Master (-13 LUFS)", Target = -13, Group = "Vocals", Blurb = "Puts the voice up front: mud cleared at 300 Hz, presence lifted at 3 kHz, sibilance tamed at 7 kHz, air on top, mids levelled and a little warmth.", Slots = new[]{ "hp_on=1;hp_f=32;b1_f=300;b1_g=-1.6;b1_q=1;b3_f=3200;b3_g=2.2;b3_q=0.9;b4_f=6800;b4_g=-0.9;b4_q=1.4;b5_f=11500;b5_g=2.2", "thr=-18;ratio=2.2;att=18;rel=140;knee=9;makeup=0.5", "t0=-24;r0=1.8;t1=-27;r1=2.3;a1=10;t2=-28;r2=2", "drive=5;harm=42;flo=3500;mix=22", "mode=1;drive=3;tone=11000;mix=28", "width=104;bass=110", "ceil=-1;gain=3.5;rel=100" } },
        new MasterPreset { Name = "Warm Tape (-14 LUFS)", Target = -14, Group = "Colour", Blurb = "Analogue tape feel: a soft low-end bump, a slightly rounded top, slow gentle compression and tape saturation that glues everything.", Slots = new[]{ "b0_f=90;b0_g=1.4;b2_f=1200;b2_g=0.6;b5_f=9500;b5_g=-1.6", "thr=-15;ratio=1.8;att=40;rel=260;knee=10;makeup=0.5", null, null, "mode=0;drive=8;tone=7500;mix=70", "width=100;bass=120", "ceil=-1;gain=2.5;rel=110" } },
        new MasterPreset { Name = "Punch & Impact (-10 LUFS)", Target = -10, Group = "Loud", Blurb = "Drums that hit: a slow compressor attack lets the transients through, the low and mid bands are controlled separately and a little presence is added.", Slots = new[]{ "hp_on=1;hp_f=30;b0_f=65;b0_g=1.8;b3_f=3800;b3_g=1.6;b3_q=1", "thr=-17;ratio=3;att=45;rel=90;knee=6;makeup=1", "t0=-19;r0=3;a0=35;l0=120;t1=-22;r1=2.8;a1=18;t2=-25;r2=2.2;a2=8", "drive=4;harm=35;mix=18", "mode=2;drive=3;mix=45", "width=108;bass=130", "ceil=-0.8;gain=6;rel=60" } },
        new MasterPreset { Name = "Wide & Huge (-12 LUFS)", Target = -12, Group = "Colour", Blurb = "A big stereo picture with the bass kept solid in the middle, plus air on the top.", Slots = new[]{ "b1_f=280;b1_g=-1;b4_f=7500;b4_g=0.8;b5_f=11000;b5_g=2", "thr=-15;ratio=2;att=28;rel=170;knee=8;makeup=0.5", null, "drive=5;harm=38;mix=20", null, "width=145;bass=160", "ceil=-1;gain=4.5;rel=80" } },
        new MasterPreset { Name = "Clean & Transparent (-14 LUFS)", Target = -14, Group = "Everyday", Blurb = "Almost invisible: only a low cut, very light compression and a limiter. Nothing is coloured.", Slots = new[]{ "hp_on=1;hp_f=24", "thr=-12;ratio=1.5;att=35;rel=220;knee=12;makeup=0", null, null, null, null, "ceil=-1;gain=2;rel=120" } },
        new MasterPreset { Name = "Radio Ready (-11 LUFS)", Target = -11, Group = "Loud", Blurb = "Polished for radio and playlists: a controlled low end, forward mids, three-band control, light colour and a firm limiter.", Slots = new[]{ "hp_on=1;hp_f=35;b0_f=90;b0_g=1;b1_f=300;b1_g=-1.2;b3_f=3500;b3_g=1.6;b5_f=11000;b5_g=1.8", "thr=-18;ratio=3;att=22;rel=130;knee=6;makeup=1", "t0=-22;r0=3;t1=-24;r1=3;t2=-26;r2=2.8", "drive=5;harm=38;mix=18", "mode=1;drive=4;mix=40", "width=110;bass=130", "ceil=-0.6;gain=7;rel=55" } },
    };
}

sealed class AmVersion { public string Name, Blurb, Eq; public bool Recommended; public int Base; public string[] Snap; public double Lufs; public LoudnessResult Result; }

partial class StudioForm
{
    // ---------- the chain as text (so a preview can be tried and undone) ----------
    string[] SnapChain() { var l = new List<string>(); lock (eng.Sync) foreach (var f in eng.MasterFx) l.Add((f.Bypass ? "1" : "0") + f.Serialize()); return l.ToArray(); }
    void RestoreChain(string[] s)
    {
        if (s == null) return;
        lock (eng.Sync) for (int i = 0; i < eng.MasterFx.Count && i < s.Length; i++) { var f = eng.MasterFx[i]; f.ResetDefaults(); f.Bypass = s[i].Length > 0 && s[i][0] == '1'; f.Deserialize(s[i].Substring(Math.Min(1, s[i].Length))); f.Reset(); }
    }
    Fx MasterFind(string type) { foreach (var f in eng.MasterFx) if (f.Type == type) return f; return null; }

    // ---------- the 20 seconds that are previewed: the loudest part of the song ----------
    bool mpActive; long mpStart, mpEnd; string[] mpBackup; int mpPreviewIdx = -1;
    void LoudestRegion(out long start, out long end)
    {
        long sr = StudioEngine.SR, total = eng.EndFrame; start = 0; end = total; if (total <= sr * 24) return;
        int secs = (int)(total / sr) + 1; var e = new double[secs];
        foreach (var t in eng.Tracks.ToArray())
        {
            var c = t.Clip; if (c == null || t.IsBus || t.Mute || t.Off) continue; long used = t.UsedLength; float[] L = c.L; if (L == null) continue;
            for (long i = 0; i < used; i += 32) { long pos = t.Offset + i; if (pos < 0 || pos >= total) continue; long si = t.ClipStart + i; if (si >= L.Length) break; double v = L[si]; e[(int)(pos / sr)] += v * v; }
        }
        int win = 20; double best = -1; int bs = 0; double run = 0; for (int i = 0; i < Math.Min(win, secs); i++) run += e[i]; best = run;
        for (int i = 1; i + win <= secs; i++) { run += e[i + win - 1] - e[i - 1]; if (run > best) { best = run; bs = i; } }
        start = bs * sr; end = Math.Min(total, start + win * sr);
    }
    void MasterPreviewStart()
    {
        if (eng.Tracks.Count == 0) { Toast("Add music first."); return; }
        LoudestRegion(out mpStart, out mpEnd); eng.MasterBypass = false; eng.Seek(mpStart); mpActive = true; try { eng.Play(); } catch (Exception ex) { mpActive = false; Toast(ex.Message); }
    }
    void MasterPreviewStop() { mpActive = false; if (eng.Playing) eng.Pause(); }
    void MasterPreviewToggle() { if (mpActive) MasterPreviewStop(); else MasterPreviewStart(); }
    void MasterTick()
    {
        if (tab != 1 && tab != 5 && (mpBackup != null || amOpen)) { if (amOpen) AmCancel(); else if (mpBackup != null) PresetPutBack(); }
        if (!mpActive) return; if ((tab != 1 && tab != 5) || !eng.Playing) { if (tab != 1 && tab != 5 && eng.Playing) eng.Pause(); mpActive = false; return; }
        if (eng.Pos >= mpEnd || eng.Pos < mpStart - 4096) eng.Seek(mpStart);
    }

    // ---------- trying a master preset ----------
    void PresetUse(int i)
    {
        var before = mpBackup ?? SnapChain(); float tBefore = targetLufs; mpBackup = null; mpPreviewIdx = -1; Presets.ApplyMaster(eng, i); targetLufs = MasterPresets.All[i].Target; abOrig = abMast = null;
        Toast(MasterPresets.All[i].Name + " applied. Press PREVIEW to hear it, or the loudness buttons to set the level."); OfferRevert(MasterPresets.All[i].Name.Split('(')[0].Trim() + " is on your master", () => { RestoreChain(before); targetLufs = tBefore; abOrig = abMast = null; });
    }
    void PresetTry(int i)
    {
        if (mpBackup == null) mpBackup = SnapChain(); Presets.ApplyMaster(eng, i); targetLufs = MasterPresets.All[i].Target; mpPreviewIdx = i; abOrig = abMast = null;
        if (!mpActive) MasterPreviewStart();
    }
    void PresetKeep() { var before = mpBackup; string nm = mpPreviewIdx >= 0 ? MasterPresets.All[mpPreviewIdx].Name.Split('(')[0].Trim() : "The preset"; mpBackup = null; mpPreviewIdx = -1; Toast("Kept. Press PREVIEW to hear it, or the loudness buttons to set the level."); if (before != null) OfferRevert(nm + " is on your master", () => { RestoreChain(before); abOrig = abMast = null; }); }
    void PresetPutBack() { if (mpBackup != null) { RestoreChain(mpBackup); mpBackup = null; mpPreviewIdx = -1; Toast("Put back what you had."); } }

    // ---------- AUTO MIX for the master ----------
    bool amOpen; AmVersion[] amVersions; string[] amOrig; int amApplied = -2; string[] amLines = new string[0]; float amScroll; LoudnessResult amOrigResult;
    Func<string, bool> amAskHook;
    // the versions AUTO MASTERING builds: each is a master preset with the EQ made for your mix on top
    static readonly string[][] AmTable = {
        new[] { "CLEAN & BALANCED", "Clean & Transparent", "Natural and clear: your mix with its tone balanced, light glue and a safe level for streaming." },
        new[] { "WARM & WIDE", "Warm & wide", "Rounder and bigger: warm low end, softer top, a wider picture and a little tape colour." },
        new[] { "LOUD & PUNCHY", "Punch & Impact", "Dense and exciting: tight control of the lows, forward mids and more level, for playlists and clubs." },
        new[] { "BASS BOOST", "Bass Boost Master", "A deep, tight low end that still sounds clear, with the mud cleaned out." },
        new[] { "VOCAL FORWARD", "Vocal Improver", "The voice sits up front: mud cleared, presence lifted, harshness tamed and air added." },
        new[] { "BRIGHT & AIRY", "Bright (-13", "More presence and sparkle on the top with exciter harmonics." },
        new[] { "RADIO READY", "Radio Ready", "Polished and firm for radio and playlists: controlled lows, forward mids, a firm limiter." },
        new[] { "STREAMING SAFE", "Streaming Safe", "A lower ceiling so nothing clips when a streaming service turns it into MP3 or AAC." },
        new[] { "GLUE & COHESION", "Glue & Cohesion", "Makes everything sound like one piece with broad gentle compression and a touch of warmth." },
        new[] { "LO-FI WARM", "Lo-Fi Chill", "Dusty and cosy: softer highs, tape saturation and a warm low end." },
    };

    // the button: it asks first, so a stray click never starts it (nothing is changed before USE THIS, and REVERT undoes it)
    string[] amRevert;   // the master chain from before the version that is on your master now (kept until you revert or start again)
    void AutoMasterClick()
    {
        if (!AskYes("Are you sure you want to run AUTO MASTERING?\n\nIt listens to the whole mix and builds ten mastered versions for you to preview. Nothing changes on your master until you press USE THIS on one, you can hear your mix before and after, and REVERT puts everything back afterwards.", "Auto Mastering")) return;
        AutoMixMaster();
    }
    void RevertAutoMaster()
    {
        if (amRevert == null) return; if (!AskYes("Put your master back as it was before AUTO MASTERING?\n\nAnything you changed on the master since then goes too (Ctrl+Z brings it back).", "Revert Auto Mastering")) return;
        PushUndo(); RestoreChain(amRevert); abOrig = abMast = null; amRevert = null; revertAction = null; eng.MasterBypass = false; Toast("Reverted: your master is back as it was before AUTO MASTERING.");
    }
    void AutoMixMaster()
    {
        if (eng.Tracks.Count == 0) { Toast("Add music first."); return; }
        Fx lim = MasterFind("limiter"), eq = MasterFind("eq"); if (lim == null || eq == null) { Toast("AUTO MASTERING needs the Parametric EQ and the Limiter in the master chain."); return; }
        MasterPreviewStop(); long full = Math.Min(eng.EndFrame, StudioEngine.SR * 240), rs, re; LoudestRegion(out rs, out re); long rf = Math.Max(StudioEngine.SR * 3, Math.Min(re - rs, StudioEngine.SR * 14)); string[] orig = SnapChain();
        LoudnessResult a = null; var vers = new List<AmVersion>(); string[] lines = null;
        RunBusy("AUTO MASTERING: listening to the whole mix...", false, prog =>
        {
            float[] l, r; eng.Render(0, full, new RenderOpts { Master = false }, null, out l, out r); a = Loudness.Measure(l, r, StudioEngine.SR, 0, l.Length); var tone = AutoMix.Analyze(AudioClip.FromArrays(l, r, "mix")); prog(0.1f);
            double[] dv; double[] gains = MasterToneGains(tone, out dv); int[] band = { 0, 1, 3, 4, 5 }; string[] nm = { "bass", "low-mids (mud)", "presence", "upper-mids", "air" };
            var ll = new List<string>(); ll.Add("Your mix: " + a.I.ToString("0.0") + " LUFS, true peak " + a.TruePeakDb.ToString("0.0") + " dBTP, loudness range " + a.LRA.ToString("0.0") + " LU.");
            var moves = new List<string>(); for (int i = 0; i < 5; i++) if (Math.Abs(gains[i]) > 0.05) moves.Add(nm[i] + " " + gains[i].ToString("+0.0;-0.0") + " dB"); ll.Add(moves.Count > 0 ? "The EQ was made for you: " + string.Join(", ", moves.ToArray()) + "." : "The tone is already well balanced, so the EQ was left almost flat."); lines = ll.ToArray();
            string eqText = moves.Count > 0 ? string.Join(", ", moves.ToArray()) : "no correction needed"; int rec = RecommendVersion(a, dv);
            for (int v = 0; v < AmTable.Length; v++)
            {
                int bi = MasterPresets.IndexOf(AmTable[v][1]); RestoreChain(orig); Presets.ApplyMaster(eng, bi);
                for (int i = 0; i < 5; i++) { float cur = eq.Get("b" + band[i] + "_g"); eq.Set("b" + band[i] + "_on", 1); eq.Set("b" + band[i] + "_g", (float)DspMath.Clamp(cur + gains[i], -6, 6)); }
                eq.Bypass = false; var res = FitLoudness(lim, MasterPresets.All[bi].Target, rs, rf);
                vers.Add(new AmVersion { Name = AmTable[v][0], Blurb = AmTable[v][2], Base = bi, Snap = SnapChain(), Lufs = res.I, Result = res, Eq = eqText, Recommended = v == rec }); prog(0.1f + 0.9f * (v + 1) / AmTable.Length);
            }
            RestoreChain(orig);
        }, ok =>
        {
            if (!ok || vers.Count == 0) { RestoreChain(orig); return; }
            amOrig = orig; amVersions = vers.ToArray(); amLines = lines; amOrigResult = a; amApplied = -1; amScroll = 0; amOpen = true; tab = 1; mpBackup = null; mpPreviewIdx = -1;
        });
    }
    // which version fits this mix best: bass-light mixes get the bass boost, dull ones the bright version, a hidden voice the vocal version, dense ones the punchy one
    internal static int RecommendVersion(LoudnessResult a, double[] dev)
    {
        if (dev != null && dev.Length >= 5) { if (dev[0] < -1.8) return 3; if (dev[2] < -1.8) return 4; if (dev[4] < -2.2) return 5; }
        if (a != null && a.LRA < 5) return 2; return 0;
    }
    LoudnessResult FitLoudness(Fx lim, float target, long start, long frames)
    {
        LoudnessResult m = new LoudnessResult(); bool was = eng.MasterBypass; eng.MasterBypass = false;
        for (int it = 0; it < 3; it++)
        {
            float[] l, r; eng.Render(start, frames, new RenderOpts { Master = true }, null, out l, out r); m = Loudness.Measure(l, r, StudioEngine.SR, 0, l.Length);
            if (double.IsInfinity(m.I) || Math.Abs(target - m.I) < 0.4) break; lim.Set("gain", (float)DspMath.Clamp(lim.Get("gain") + (target - m.I), 0, 24));
        }
        eng.MasterBypass = was; return m;
    }
    void AmPreview(int v)
    {
        if (v < 0) RestoreChain(amOrig); else RestoreChain(amVersions[v].Snap); amApplied = v; eng.MasterBypass = false; if (!mpActive) MasterPreviewStart();
    }
    void AmUse(int v)
    {
        RestoreChain(amVersions[v].Snap); targetLufs = MasterPresets.All[amVersions[v].Base].Target; abOrig = amOrigResult; abMast = amVersions[v].Result; ApplyMatch(); amOpen = false; MasterPreviewStop(); eng.MasterBypass = false;
        Toast(amVersions[v].Name + " is on your master. Edit anything in the chain, or press PREVIEW to hear it again.");
        var origKeep = amOrig; amRevert = origKeep; OfferRevert(amVersions[v].Name + " is on your master", () => { RestoreChain(origKeep); abOrig = abMast = null; amRevert = null; });
    }
    void AmCancel() { if (amOrig != null) RestoreChain(amOrig); amOpen = false; MasterPreviewStop(); amApplied = -2; }

    // ---------- loudness: automatic (picks the best level for this track) or manual ----------
    void AutoLoudnessAuto()
    {
        Fx lim = MasterFind("limiter"); if (lim == null) { Toast("Add a Limiter to the master chain first."); return; } if (eng.Tracks.Count == 0) { Toast("Add music first."); return; }
        long full = Math.Min(eng.EndFrame, StudioEngine.SR * 240); LoudnessResult a = null; var chainB = SnapChain();
        RunBusy("AUTO LOUDNESS: listening to the whole mix...", false, prog => { float[] l, r; eng.Render(0, full, new RenderOpts { Master = false }, null, out l, out r); a = Loudness.Measure(l, r, StudioEngine.SR, 0, l.Length); },
            ok =>
            {
                if (!ok || a == null || double.IsInfinity(a.I)) { if (ok) Toast("There is no sound to measure yet."); return; }
                float t = AutoTarget(a, out amReason); var comp = MasterFind("comp"); if (comp != null && comp.Bypass) comp.Bypass = false; targetLufs = t; Toast("AUTO LOUDNESS: " + amReason); AutoLoudness(chainB);
            });
    }
    string amReason = "";
    // the best target for this material: dynamic music keeps its dynamics, dense music is taken louder, everything else sits at the streaming standard; never turned down (the limiter only adds level)
    internal static float AutoTarget(LoudnessResult pre, out string why)
    {
        float t; if (pre.LRA > 12) { t = -16; why = "this track is very dynamic (" + pre.LRA.ToString("0") + " LU), so I chose -16 LUFS to keep it natural."; }
        else if (pre.LRA < 6) { t = -11; why = "this track is dense and even (" + pre.LRA.ToString("0.0") + " LU), so I chose -11 LUFS: loud and punchy."; }
        else { t = -14; why = "I chose -14 LUFS, the level streaming services play at, so it sounds the same everywhere."; }
        if (pre.I > t) { t = (float)Math.Round(pre.I, 1); why = "it is already " + pre.I.ToString("0.0") + " LUFS, so I kept that level and only evened it out."; }
        return t;
    }

    // ---------- the bars at the top: only what belongs to mastering ----------
    void PaintMasterBars(Graphics g)
    {
        float W = ClientSize.Width, y = 12, h = 46;
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH); NeonRule(g, 0, ToolH, W);
        Btn(g, Rc(12, y, 48, h), "≡", 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 24);
        Txt(g, "MASTERING", 18, Ink, Rc(76, y, 190, h), StringAlignment.Near, true); if (ShowBack) Btn(g, Rc(270, y + 4, 100, h - 8), "BACK", 0, () => GoBack(), 14);   /* no HOME: the MIX tab is on this page */
        float rx = W - 14;
        Btn(g, Rc(rx - 106, y, 106, h), "EXPORT", 1, () => ExportClick(), 16); rx -= 112;
        Btn(g, Rc(rx - 58, y + 4, 58, h - 8), "Help", 2, () => app.ShowHelp(null), 14); rx -= 64;
        Btn(g, Rc(rx - 108, y + 4, 108, h - 8), Win.FullLabel(this), 0, () => Win.ToggleFullscreen(this), 12); rx -= 114;
        Btn(g, Rc(rx - 72, y + 4, 72, h - 8), "Open", 0, () => OpenClick(), 14); rx -= 78;
        Btn(g, Rc(rx - 72, y + 4, 72, h - 8), "Save", 0, () => SaveClick(false), 14); rx -= 78;
        Btn(g, Rc(rx - 84, y + 4, 84, h - 8), "Save as", 0, () => SaveClick(true), 14);
        // the second bar: PLAY, the time, PREVIEW, the master meter and the page tabs
        float y0 = ToolH; using (var br = new SolidBrush(TM.C(255, 22, 12, 40))) g.FillRectangle(br, 0, y0, W, TransH); NeonRule(g, 0, y0 + TransH, W);
        float x = 14, yy = y0 + 8, hh = 36;
        Btn(g, Rc(x, yy, 96, hh), "PLAY", eng.Playing && !mpActive ? 4 : 1, () => { if (mpActive) MasterPreviewStop(); PlayToggle(); }, 15); x += 104;
        double sec = dispPos / StudioEngine.SR, tot = eng.EndFrame / (double)StudioEngine.SR; var tr = Rc(x, yy, 210, hh); FillRR(g, TM.C(200, 8, 4, 18), tr, 6); StrokeRR(g, Edge, tr, 6, 1);
        Txt(g, Fmt(sec) + "  /  " + Fmt(tot), 19, Accent, tr, StringAlignment.Center, true); x += 222;
        Btn(g, Rc(x, yy, 168, hh), "PREVIEW MASTER", mpActive ? 4 : 2, () => MasterPreviewStart(), 13); Hint2(Rc(x, yy, 168, hh), "Hear your master: a loop of the loudest 20 seconds with the mastering chain on."); x += 174;
        { var spb = Rc(x, yy, 112, hh); Btn(g, spb, "STOP PREVIEW", mpActive ? 3 : 0, () => MasterPreviewStop(), 11); Hint2(spb, "Stop the preview loop"); x += 118; }
        float tx = W - 328; float mx = x + 10; float room = tx - 12 - mx;
        if (room >= 70) { Txt(g, "MASTER", 11, DimC, Rc(mx, yy - 4, 60, 14), StringAlignment.Near, false); float bw = Math.Min(120, room); HBar(g, Rc(mx, yy + 12, bw, 8), eng.MasterPeakL); HBar(g, Rc(mx, yy + 22, bw, 8), eng.MasterPeakR); if (room >= 230) { float lm = eng.Lufs.Momentary; Txt(g, "M " + Db(lm) + " LUFS", 13, Ink, Rc(mx + 128, yy, Math.Min(120, room - 128), hh), StringAlignment.Near, false); } }
        Btn(g, Rc(tx, yy, 106, hh), "MIX", 0, () => tab = 0, 14); Btn(g, Rc(tx + 112, yy, 106, hh), "MASTERING", 4, () => tab = 1, 13); Btn(g, Rc(tx + 224, yy, 90, hh), "LYRICS", 0, () => tab = 2, 13);
    }

    // ---------- the page ----------
    int mpTab; float mpScroll; string mpGroup = "";
    void PaintMastering(Graphics g)
    {
        float top = ToolH + TransH + 10, W = ClientSize.Width, H = ClientSize.Height;
        // ---- left: the chain and the A/B ----
        var lp = Rc(14, top, 270, H - top - 12); FillRR(g, Panel, lp, 12); StrokeRR(g, Edge, lp, 12, 1);
        Txt(g, "MASTERING CHAIN", 17, Ink, Rc(lp.X + 14, lp.Y + 8, 240, 26), StringAlignment.Near, true);
        float y = lp.Y + 40; int n = eng.MasterFx.Count; if (selMaster >= n) selMaster = Math.Max(0, n - 1); float rowH = lp.Height > 560 ? 40 : 34, rowG = rowH + 4;
        for (int i = 0; i < n; i++, y += rowG)
        {
            var f = eng.MasterFx[i]; int fi = i; bool sel = i == selMaster; var row = Rc(lp.X + 10, y, lp.Width - 20, rowH);
            FillRR(g, sel ? TM.C(210, 80, 40, 150) : TM.C(150, 40, 22, 74), row, 8); if (sel) StrokeRR(g, Accent, row, 8, 1.4f);
            Reg(row, () => { selMaster = fi; mpTab = 0; });
            var dot = Rc(row.X + 10, row.Y + (rowH - 18) / 2, 18, 18); using (var b = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b, dot); Reg(dot, () => f.Bypass = !f.Bypass);
            Txt(g, i < ModNames.Length && f.Type != "gain" ? (i < 7 ? ModNames[i] : f.Title) : f.Title, 14, f.Bypass && !sel ? DimC : Ink, Rc(row.X + 38, row.Y, row.Width - 100, rowH), StringAlignment.Near, sel);
            if (f.GR > 0.2f) Txt(g, "-" + f.GR.ToString("0.0"), 11, Warn, Rc(row.Right - 78, row.Y, 40, rowH), StringAlignment.Far, false);
            if (i >= 7) Btn(g, Rc(row.Right - 30, row.Y + (rowH - 22) / 2, 22, 22), "x", 3, () => { lock (eng.Sync) eng.MasterFx.RemoveAt(fi); }, 12);
        }
        Btn(g, Rc(lp.X + 10, y + 2, lp.Width - 20, 28), "+ Add Effect to master", 1, () => OpenBrowser(true, -1), 12); y += 40;
        Txt(g, "ORIGINAL VS MASTERED", 13, Ink, Rc(lp.X + 14, y, 250, 20), StringAlignment.Near, true); y += 24;
        Btn(g, Rc(lp.X + 10, y, 120, 34), "A  ORIGINAL", !eng.MasterBypass ? 0 : 4, () => { eng.MasterBypass = true; ApplyMatch(); }, 13);
        Btn(g, Rc(lp.X + 138, y, 122, 34), "B  MASTERED", eng.MasterBypass ? 0 : 4, () => { eng.MasterBypass = false; }, 13); y += 40;
        Btn(g, Rc(lp.X + 10, y, lp.Width - 20, 28), matchOn ? "Loudness match: ON" : "Loudness match: OFF", matchOn ? 4 : 0, () => { matchOn = !matchOn; ApplyMatch(); }, 12); y += 34;
        if (abOrig != null && abMast != null && y + 30 < lp.Bottom)
        {
            Txt(g, "BEFORE " + Db(abOrig.I) + "   AFTER " + Db(abMast.I) + " LUFS", 11, Ink, Rc(lp.X + 12, y, lp.Width - 24, 16), StringAlignment.Near, true);
            Txt(g, "MASTERING ADDS " + (abMast.I - abOrig.I).ToString("+0.0;-0.0") + " LU", 11, DimC, Rc(lp.X + 12, y + 16, lp.Width - 24, 16), StringAlignment.Near, false);
        }

        // ---- centre: the editor and the presets ----
        float ex = lp.Right + 14, ew = W - ex - 430;
        var ep0 = Rc(ex, top, ew, H - top - 12);
        // the visualiser is the top of this page: the light show over the EQ, the effects and the presets (only a very short window keeps just the controls; the full page is the menu item Pages > Master Visualiser)
        const float minDrawer = 560f; visStageOn = ep0.Height >= 360f; var ep = ep0;   // (only a very short window leaves it out)
        if (visStageOn)
        {
            float stageH = ep0.Height - minDrawer >= 226f ? Math.Min(ep0.Height * 0.5f, ep0.Height - minDrawer) : Math.Max(190f, Math.Min(ep0.Height * 0.36f, ep0.Height - 300f)); var sa = Rc(ep0.X, ep0.Y, ep0.Width, stageH); FillRR(g, TM.C(255, 5, 2, 12), sa, 12); PaintVisStage(g, RectangleF.Inflate(sa, -3, -3), false); Win.DrawNeonBox(g, sa, false);
            ep = Rc(ep0.X, sa.Bottom + 8, ep0.Width, ep0.Bottom - sa.Bottom - 8);
        }
        FillRR(g, Panel, ep, 12); StrokeRR(g, Edge, ep, 12, 1);
        var t1 = Rc(ep.X + 10, ep.Y + 8, 170, 32); var t2 = Rc(t1.Right + 8, ep.Y + 8, 150, 32);
        Btn(g, t1, "EQ & EFFECTS", mpTab == 0 ? 4 : 0, () => mpTab = 0, 12); Hint2(t1, "Edit the selected effect of the chain (the EQ shows your sound before and after)");
        Btn(g, t2, "PRESETS", mpTab == 1 ? 4 : 0, () => mpTab = 1, 12); Hint2(t2, "Master presets you can hear before you keep them: Bass Boost, Vocal Enhance, Warm Tape and more");
        var inner = Rc(ep.X + 4, ep.Y + 46, ep.Width - 8, ep.Height - 52);
        if (mpTab == 0) { if (selMaster >= 0 && selMaster < eng.MasterFx.Count) PaintEditor(g, eng.MasterFx[selMaster], inner, null); }
        else PaintMasterPresets(g, inner);

        // ---- right: AUTO MIX, the analyser and the loudness ----
        float rx = W - 416; var ap = Rc(rx, top, 402, H - top - 12); FillRR(g, Panel, ap, 12); StrokeRR(g, Edge, ap, 12, 1);
        var am = Rc(ap.X + 12, ap.Y + 12, amRevert != null ? 262 : 378, 50); Btn(g, am, "AUTO MASTERING", 2, () => AutoMasterClick(), amRevert != null ? 16 : 19); Hint2(am, "Asks first, then listens to the whole mix, builds the EQ for you and gives you ten versions to hear (and tells you which one fits best): nothing changes until you pick one");
        if (amRevert != null) { var rv0 = Rc(am.Right + 8, am.Y, 108, 50); Btn(g, rv0, "REVERT", 3, () => RevertAutoMaster(), 15); Hint2(rv0, "Put the master chain back exactly as it was before AUTO MASTERING (hear your original again)"); }
        TxtWrap(g, "Listens to the whole mix, makes the EQ by itself and gives you 10 versions to preview.", 11, DimC, Rc(ap.X + 14, am.Bottom + 4, ap.Width - 28, 30));
        float yy = am.Bottom + 38; Txt(g, "LOUDNESS ANALYSER", 14, Ink, Rc(ap.X + 14, yy, 250, 22), StringAlignment.Near, true);
        Btn(g, Rc(ap.Right - 112, yy - 2, 98, 26), "Reset meters", 0, () => { eng.Lufs.Reset(); }, 12); yy += 28; var lu = eng.Lufs; lu.UpdateIntegrated();
        string[] lb = { "MOMENTARY", "SHORT-TERM", "INTEGRATED" }; float[] vv = { lu.Momentary, lu.ShortTerm, lu.Integrated };
        for (int i = 0; i < 3; i++)
        {
            var c = Rc(ap.X + 12 + i * 128, yy, 122, 66); FillRR(g, TM.C(150, 8, 4, 20), c, 10);
            Txt(g, lb[i], 10, DimC, Rc(c.X, c.Y + 3, c.Width, 14), StringAlignment.Center, true); Txt(g, Db(vv[i]), 25, i == 2 ? Accent : Ink, Rc(c.X, c.Y + 16, c.Width, 32), StringAlignment.Center, true); Txt(g, "LUFS", 11, DimC, Rc(c.X, c.Y + 48, c.Width, 16), StringAlignment.Center, false);
        }
        yy += 74; double tpDb = lu.TruePeakDb; bool tpBad = tpDb > -0.9;
        var c1 = Rc(ap.X + 12, yy, 190, 54); FillRR(g, TM.C(150, 8, 4, 20), c1, 10); Txt(g, "TRUE PEAK (MAX)", 10, DimC, Rc(c1.X, c1.Y + 3, c1.Width, 14), StringAlignment.Center, true); Txt(g, Db(tpDb) + " dBTP", 20, tpBad ? Bad : Good, Rc(c1.X, c1.Y + 17, c1.Width, 32), StringAlignment.Center, true);
        var c2 = Rc(ap.X + 210, yy, 180, 54); bool clip = lu.Clips > 0; FillRR(g, TM.C(150, 8, 4, 20), c2, 10); Txt(g, "CLIPPING", 10, DimC, Rc(c2.X, c2.Y + 3, c2.Width, 14), StringAlignment.Center, true); Txt(g, clip ? lu.Clips + " samples" : "none detected", 17, clip ? Bad : Good, Rc(c2.X, c2.Y + 17, c2.Width, 32), StringAlignment.Center, true);
        yy += 62; Txt(g, "GAIN REDUCTION", 11, DimC, Rc(ap.X + 14, yy, 110, 16), StringAlignment.Near, false); float grf = Clamp01(eng.MasterGr / 18f);
        FillRR(g, TM.C(180, 8, 4, 18), Rc(ap.X + 130, yy + 3, 220, 9), 4); if (grf > 0.01f) FillRR(g, Warn, Rc(ap.X + 130, yy + 3, 220 * grf, 9), 4); Txt(g, eng.MasterGr.ToString("0.0") + " dB", 11, Ink, Rc(ap.X + 356, yy, 40, 16), StringAlignment.Near, false); yy += 30;
        // loudness: automatic first, manual for the picky
        Txt(g, "LOUDNESS", 14, Ink, Rc(ap.X + 14, yy, 200, 22), StringAlignment.Near, true); yy += 26;
        var al = Rc(ap.X + 12, yy, 378, 40); Btn(g, al, "AUTO LOUDNESS", 2, () => AutoLoudnessAuto(), 15); Hint2(al, "Measures the whole mix, picks the best level for this track (and evens it out), and sets it. You do not have to do anything."); yy += 44;
        TxtWrap(g, "Picks the best level for this track by itself.", 11, DimC, Rc(ap.X + 14, yy, ap.Width - 28, 16)); yy += 22;
        Txt(g, "MANUAL", 12, DimC, Rc(ap.X + 14, yy, 90, 20), StringAlignment.Near, true); ValueBox(g, Rc(ap.Right - 128, yy - 3, 114, 26), targetLufs.ToString("0.0") + " LUFS", Num(targetLufs), s => { float v; if (ParseNum(s, out v)) targetLufs = Math.Max(-20, Math.Min(-6, v)); }); yy += 28;
        var ts = Slider(g, Rc(ap.X + 14, yy, 372, 22), (targetLufs + 20f) / 14f, f => targetLufs = (float)Math.Round(f * 14f - 20f, 1), Accent2); ts.OnDouble = () => targetLufs = -14; yy += 30;
        var ml = Rc(ap.X + 12, yy, 378, 32); Btn(g, ml, "SET LOUDNESS (MANUAL)", 0, () => AutoLoudness(), 13); Hint2(ml, "Sets the loudness to the number you chose with the slider (it adjusts the limiter).");
    }

    // ---------- the presets page ----------
    void PaintMasterPresets(Graphics g, RectangleF a)
    {
        float y = a.Y + 2;
        if (mpBackup != null)
        {
            var bn = Rc(a.X + 4, y, a.Width - 8, 44); FillRR(g, TM.C(220, 30, 60, 90), bn, 10); StrokeRR(g, NeonCyan, bn, 10, 1.4f);
            Txt(g, "TRYING: " + (mpPreviewIdx >= 0 ? MasterPresets.All[mpPreviewIdx].Name.Split('(')[0].Trim().ToUpper() : ""), 13, Ink, Rc(bn.X + 12, bn.Y, bn.Width - 250, bn.Height), StringAlignment.Near, true);
            var keep = Rc(bn.Right - 232, bn.Y + 6, 108, 32); var back = Rc(bn.Right - 118, bn.Y + 6, 108, 32);
            Btn(g, keep, "KEEP IT", 2, () => PresetKeep(), 13); Hint2(keep, "Keep this preset on your master"); Btn(g, back, "PUT BACK", 3, () => PresetPutBack(), 12); Hint2(back, "Go back to the chain you had before trying");
            y += 52;
        }
        else { TxtWrap(g, "Press PREVIEW to hear a preset on your song straight away (the loudest 20 seconds, looping), then KEEP the one you like.", 12, DimC, Rc(a.X + 8, y, a.Width - 16, 40)); y += 44; }
        // the groups (every preset belongs to one): All, Everyday, Loud, Genre, Vocals, Low end, Colour, Width, Fix
        { float cx = a.X + 6, cy = y; const float chH = 26; Action<string> chip = lbl => { float cwid = 20 + lbl.Length * 7.2f; if (cx + cwid > a.Right - 8) { cx = a.X + 6; cy += chH + 4; } string lb2 = lbl; Chip(g, Rc(cx, cy, cwid, chH), lbl, (lbl == "ALL" ? mpGroup.Length == 0 : string.Equals(mpGroup, lbl, StringComparison.OrdinalIgnoreCase)), () => { mpGroup = lb2 == "ALL" ? "" : lb2; mpScroll = 0; }); cx += cwid + 4; };
          chip("ALL"); foreach (var gn in MasterPresets.Groups) chip(gn.ToUpper()); y = cy + chH + 8; }
        var all0 = MasterPresets.All; var show = new List<int>(); for (int q = 0; q < all0.Length; q++) if (mpGroup.Length == 0 || string.Equals(all0[q].Group, mpGroup, StringComparison.OrdinalIgnoreCase)) show.Add(q);
        int cols = a.Width >= 640 ? 2 : 1; float vsw = 56, gap = 10, cw = (a.Width - 8 - vsw - gap * (cols - 1)) / cols, ch = 142; int rows = (show.Count + cols - 1) / cols; float areaH = a.Bottom - y - 2, total = rows * (ch + gap);
        mpScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), mpScroll)); var area = Rc(a.X, y, a.Width, areaH); regs.Add(new HitReg { R = area, OnWheel = d => mpScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), mpScroll - d * 60)) });
        var gs = g.Save(); g.SetClip(area);
        for (int i = 0; i < show.Count; i++)
        {
            int pi = show[i]; var p = all0[pi]; var card = Rc(a.X + 4 + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap) - mpScroll, cw, ch); if (card.Bottom < y - 2 || card.Y > a.Bottom) continue;
            bool trying = mpBackup != null && mpPreviewIdx == pi, hot = Hot(RectangleF.Intersect(card, area)) && area.Contains(MousePos);
            NeonCard(g, card, hot, trying);
            Txt(g, p.Name.ToUpper(), 14, Ink, Rc(card.X + 12, card.Y + 8, card.Width - 24, 22), StringAlignment.Near, true);
            NeonTag(g, card.X + 12, card.Y + 32, p.Group.ToUpper(), NeonViolet);
            TxtWrap(g, p.Blurb, 11, TM.C(205, 200, 190, 230), Rc(card.X + 12, card.Y + 54, card.Width - 24, 50));
            var pv = Rc(card.X + 12, card.Bottom - 38, 104, 30); var sp0 = Rc(pv.Right + 6, card.Bottom - 38, 62, 30); var us = Rc(sp0.Right + 6, card.Bottom - 38, 70, 30);
            var pva = RectangleF.Intersect(pv, area); if (!pva.IsEmpty) { Btn(g, pv, trying && mpActive ? "PREVIEWING" : "PREVIEW", trying ? 4 : 1, () => PresetTry(pi), 12); Hint2(pv, "Hear this preset on your song now (the loudest 20 seconds, looping)"); var spa = RectangleF.Intersect(sp0, area); if (!spa.IsEmpty) { Btn(g, sp0, "STOP", trying && mpActive ? 3 : 0, () => MasterPreviewStop(), 11); Hint2(sp0, "Stop the preview"); } }
            var usa = RectangleF.Intersect(us, area); if (!usa.IsEmpty) { Btn(g, us, "USE", 2, () => PresetUse(pi), 12); Hint2(us, "Put this preset on your master and keep it"); }
        }
        g.Restore(gs); VScroll(g, Rc(a.Right - 56, y, 42, areaH), total, areaH, () => mpScroll, v => mpScroll = v);
    }

    // ---------- the AUTO MIX sheet: three versions to hear ----------
    void PaintAutoMixSheet(Graphics g)
    {
        if (!amOpen || amVersions == null) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(TM.C(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => { });
        float sw = Math.Min(900, W - 24), sh = Math.Min(H - 30, 700); var sheet = PopMove("amaster", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "AUTO MASTERING", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 400, 34), StringAlignment.Near, true);
        float y = sheet.Y + 52; foreach (var ln in amLines) { TxtWrap(g, ln, 12, DimC, Rc(sheet.X + 22, y, sheet.Width - 44, 20)); y += 22; }
        TxtWrap(g, "Press PREVIEW to hear each version on the loudest part of your song (it loops), then USE THIS on the one you like. You can always REVERT afterwards, and CANCEL puts everything back.", 12, Ink, Rc(sheet.X + 22, y + 4, sheet.Width - 44, 36)); y += 44;
        float ch = 96, gap = 8, areaTop = y, areaBottom = sheet.Bottom - 64, areaH = areaBottom - areaTop; int count = amVersions.Length + 1; float total = count * (ch + gap); float vsw = 56;
        amScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), amScroll)); var area = Rc(sheet.X + 10, areaTop, sheet.Width - 20, areaH); regs.Add(new HitReg { R = area, OnWheel = d => amScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), amScroll - d * 60)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = -1; i < amVersions.Length; i++)
        {
            int vi = i; var card = Rc(sheet.X + 22, areaTop + (i + 1) * (ch + gap) - amScroll, sheet.Width - 44 - vsw, ch); if (card.Bottom < areaTop - 2 || card.Y > areaBottom) continue; bool on = amApplied == i;
            FillRR(g, on ? TM.C(225, 34, 56, 100) : TM.C(170, 28, 16, 56), card, 12); StrokeRR(g, on ? NeonCyan : Color.FromArgb(110, NeonViolet), card, 12, on ? 1.8f : 1.1f); if (on) Glow(g, NeonCyan, card, 12, 0.5f, 3);
            string nm = i < 0 ? "YOUR MIX (BEFORE)" : amVersions[i].Name; string bl = i < 0 ? "Exactly what you have now, with the chain you had. Use it to compare." : amVersions[i].Blurb; float tw = card.Width - 400;
            Txt(g, nm, 16, Ink, Rc(card.X + 16, card.Y + 8, Math.Min(tw, 260), 24), StringAlignment.Near, true);
            if (i >= 0 && amVersions[i].Recommended) { var rt = Rc(card.X + 16 + Math.Min(tw, 260) + 6, card.Y + 11, 150, 18); FillRR(g, Color.FromArgb(230, 40, 150, 100), rt, 6); Txt(g, "BEST FOR YOUR MIX", 10, Color.White, rt, StringAlignment.Center, true); }
            TxtWrap(g, bl, 11, TM.C(210, 200, 190, 230), Rc(card.X + 16, card.Y + 32, tw, 30));
            if (i >= 0) Txt(g, "EQ: " + amVersions[i].Eq + "   -   " + amVersions[i].Lufs.ToString("0.0") + " LUFS", 11, Color.FromArgb(150, 235, 200), Rc(card.X + 16, card.Y + 66, tw, 24), StringAlignment.Near, false);
            else Txt(g, amOrigResult != null ? amOrigResult.I.ToString("0.0") + " LUFS (before mastering)" : "", 11, DimC, Rc(card.X + 16, card.Y + 66, tw, 20), StringAlignment.Near, false);
            var pv = Rc(card.Right - 366, card.Y + 30, 150, 36); var sp0 = Rc(pv.Right + 6, card.Y + 30, 70, 36); var us = Rc(card.Right - 130, card.Y + 30, 114, 36);
            var pva = RectangleF.Intersect(pv, area); if (!pva.IsEmpty) { Btn(g, pv, on && mpActive ? "PREVIEWING" : "PREVIEW", on ? 4 : 1, () => AmPreview(vi), 13); Hint2(pv, "Hear this version on your song now"); var spa = RectangleF.Intersect(sp0, area); if (!spa.IsEmpty) { Btn(g, sp0, "STOP", on && mpActive ? 3 : 0, () => MasterPreviewStop(), 12); Hint2(sp0, "Stop the preview"); } }
            if (i >= 0) { var usa = RectangleF.Intersect(us, area); if (!usa.IsEmpty) { Btn(g, us, "USE THIS", 2, () => AmUse(vi), 13); Hint2(us, "Keep this version on your master (you can revert it afterwards)"); } }
        }
        g.Restore(gs); VScroll(g, Rc(sheet.Right - 56, areaTop, 42, areaH), total, areaH, () => amScroll, v => amScroll = v);
        var cancel = Rc(sheet.Right - 22 - 130, sheet.Bottom - 54, 130, 38); Btn(g, cancel, "CANCEL", 3, () => AmCancel(), 13); Hint2(cancel, "Close and put your master back as it was");
        Txt(g, "Esc closes", 11, DimC, Rc(sheet.X + 22, sheet.Bottom - 50, 200, 30), StringAlignment.Near, false);
    }

    // ---------- the EQ picture: your sound before and after, moving with the music ----------
    double[] specPre = new double[2049];
    void FillEqBackdrop(Graphics g, RectangleF plot)
    {
        using (var p = RR(plot, 8)) using (var br = new LinearGradientBrush(plot, TM.C(235, 14, 8, 34), TM.C(235, 4, 2, 12), 90f)) g.FillPath(br, p);
        StrokeRR(g, Edge, plot, 8, 1);
    }
    void DrawEqSpectrum(Graphics g, RectangleF plot, bool master)
    {
        int w = (int)plot.Width, cnt = w / 2 + 1; var after = new PointF[cnt]; var before = new PointF[cnt]; float hh = plot.Height * 0.9f;
        for (int i = 0, px = 0; px <= w; px += 2, i++)
        {
            double f = 20 * Math.Pow(1000, px / plot.Width); int k = Math.Min(2048, Math.Max(1, (int)(f * 4096 / StudioEngine.SR)));
            after[i] = new PointF(plot.X + px, plot.Bottom - (float)DspMath.Clamp((spec[k] + 90) / 80.0, 0, 1) * hh); before[i] = new PointF(plot.X + px, plot.Bottom - (float)DspMath.Clamp((specPre[k] + 90) / 80.0, 0, 1) * hh);
        }
        var poly = new List<PointF>(); poly.Add(new PointF(plot.X, plot.Bottom));
        if (master)
        {   // what the chain changed: lime where it added sound, pink where it took sound away
            for (int i = 0; i < cnt; i++) { float diff = (before[i].Y - after[i].Y) / hh * 80f; if (Math.Abs(diff) < 1.2f) continue; using (var pen = new Pen(Color.FromArgb((int)Math.Min(120, 40 + Math.Abs(diff) * 9), diff > 0 ? NeonLime : NeonPink), 2f)) g.DrawLine(pen, after[i].X, after[i].Y, before[i].X, before[i].Y); }
            poly.AddRange(before); poly.Add(new PointF(plot.Right, plot.Bottom)); using (var br = new SolidBrush(TM.C(26, 210, 210, 235))) g.FillPolygon(br, poly.ToArray());
            using (var pen = new Pen(TM.C(150, 205, 205, 235), 1.2f)) g.DrawLines(pen, before);
        }
        poly.Clear(); poly.Add(new PointF(plot.X, plot.Bottom)); poly.AddRange(after); poly.Add(new PointF(plot.Right, plot.Bottom));
        using (var br = new LinearGradientBrush(plot, Color.FromArgb(140, NeonCyan), Color.FromArgb(14, NeonViolet), 90f)) g.FillPolygon(br, poly.ToArray());
        using (var pen = new Pen(Color.FromArgb(60, NeonCyan), 5f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, after); using (var pen = new Pen(NeonCyan, 1.7f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, after);
        if (master) { Txt(g, "BEFORE", 10, TM.C(200, 205, 205, 235), Rc(plot.X + 8, plot.Y + 4, 60, 14), StringAlignment.Near, true); Txt(g, "AFTER", 10, NeonCyan, Rc(plot.X + 64, plot.Y + 4, 60, 14), StringAlignment.Near, true); Txt(g, "ADDED", 10, NeonLime, Rc(plot.X + 118, plot.Y + 4, 60, 14), StringAlignment.Near, true); Txt(g, "TAKEN AWAY", 10, NeonPink, Rc(plot.X + 172, plot.Y + 4, 90, 14), StringAlignment.Near, true); }
        else Txt(g, "LIVE SOUND", 10, NeonCyan, Rc(plot.X + 8, plot.Y + 4, 100, 14), StringAlignment.Near, true);
    }
}
