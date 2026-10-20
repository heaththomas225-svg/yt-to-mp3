using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;


partial class StudioForm
{
    internal bool aiOpen; internal string aiPrompt = ""; internal PresetPrompt.Result aiResult;
    static readonly string[] AiExamples = { "Juice WRLD with a stutter delay and lots of reverb", "Drake, dark and smooth, slapback", "Eminem rapid fire, dry and loud", "UK drill, dry, bit of distortion", "R&B singing with harmonies and a big hall", "Travis Scott hard tune, huge echo, wide", "Playboi Carti baby voice, distorted", "Afro singing with a 1/4 ping pong delay" };
    internal bool aiBus;   // the same sheet is the AI BUS GENERATOR
    static readonly string[] AiBusExamples = { "Drill drum bus, punchy and dark", "Wide R&B reverb bus", "Juice WRLD vocal bus with more glue and a 1/4 echo", "Heavy 808 bus for rage", "Vintage beat bus, warm tape", "Ad-lib bus with a triplet echo", "Clean mix bus for pop, loud", "Telephone FX bus for UK rap" };
    internal bool aiFx, aiFxFromBrowser; internal int aiSeed; readonly Random aiRnd = new Random();   // aiFx: the AI EFFECT GENERATOR (opened from ADD EFFECT it adds to that track or the master)
    static readonly string[] AiFxExamples = { "Huge dark cathedral reverb with a shimmer", "1/8 dotted ping pong delay, tape, ducked", "Hard autotune in F# minor", "Telephone with a bit of distortion", "Croaky demon voice 9 semitones down", "Slow tremolo on 1/4 notes", "Cut the mud and add air", "Bleep censor" };
    int aiKindLast = -1;   // the words typed stay while you reopen the same generator; another generator starts empty
    void AiOpenCore(int kind) { aiOpen = true; aiBus = kind == 1; aiFx = kind == 2; aiFxFromBrowser = false; aiSeed = 0; stylesOpen = false; aiResult = null; browserOpen = false; if (kind != aiKindLast) aiPrompt = ""; aiKindLast = kind; }
    internal void OpenAiPreset() { AiOpenCore(0); }
    internal void OpenAiBus() { AiOpenCore(1); }
    internal void OpenAiEffect(bool fromBrowser) { bool m = browserMaster; int bt = browserTrack; AiOpenCore(2); aiFxFromBrowser = fromBrowser; browserMaster = fromBrowser && m; browserTrack = bt; }
    string[] AiExs { get { return aiFx ? AiFxExamples : aiBus ? AiBusExamples : AiExamples; } }
    PresetPrompt.Result AiMake(int seed) { return aiFx ? EffectPrompt.Make(aiPrompt, seed) : aiBus ? BusPrompt.Make(aiPrompt, seed) : PresetPrompt.Make(aiPrompt, seed); }
    internal void AiGenerate() { if (aiPrompt.Trim().Length == 0) { Toast("Type what you want first, for example: " + AiExs[0]); return; } aiSeed = 0; aiResult = AiMake(0); }
    // REGENERATE: another take on the same words (another starting version, other amounts); RANDOMISE: a surprise description and its result
    internal void AiRegenerate(int seed = 0) { if (aiPrompt.Trim().Length == 0) { Toast("Type what you want first (or press RANDOMISE for a surprise)."); return; } aiSeed = seed != 0 ? seed : aiRnd.Next(1, int.MaxValue); aiResult = AiMake(aiSeed); }
    internal void AiRandomise(int seed = 0) { var r = seed != 0 ? new Random(seed) : aiRnd; aiPrompt = aiFx ? AiVary.RandomEffect(r) : aiBus ? AiVary.RandomBus(r) : AiVary.RandomVocal(r); aiSeed = r.Next(1, int.MaxValue); aiResult = AiMake(aiSeed); }
    VocalStyles.Style AiStyle() { return aiResult == null ? null : new VocalStyles.Style { Name = aiResult.Name, Genre = "AI", Blurb = "", Chain = aiResult.Chain, BusName = aiResult.BusName, BusChain = aiResult.BusChain }; }
    internal void AiUse() { var s = AiStyle(); if (s == null) return; aiOpen = false; if (aiFx) { AiAddEffects(); return; } if (aiBus) { s.Genre = aiResult.BusName; s.BusName = null; ApplyBusPreset(s); } else ApplyStyle(s, false); }
    // the AI effect goes into YOUR PLUGINS of the track (or the master, from the mastering ADD EFFECT)
    void AiAddEffects()
    {
        var fx = VocalStyles.BuildChain(aiResult.Chain); if (fx.Count == 0) { Toast("That effect could not be built."); return; }
        bool master = aiFxFromBrowser && browserMaster; int ti = aiFxFromBrowser && browserTrack >= 0 ? browserTrack : selTrack;
        if (!master && (eng.Tracks.Count == 0 || ti < 0 || ti >= eng.Tracks.Count)) { Toast("Add or select a track first."); return; }
        PushUndo();
        if (master) { lock (eng.Sync) eng.MasterFx.AddRange(fx); selMaster = eng.MasterFx.Count - 1; Toast(aiResult.Name + " added to the master (" + fx.Count + " plugin" + (fx.Count == 1 ? "" : "s") + "). Ctrl+Z undoes it."); return; }
        var tr = eng.Tracks[ti]; lock (eng.Sync) tr.Fx.AddRange(fx); selTrack = ti; selFx = tr.Fx.Count - 1;
        Toast(aiResult.Name + " added to " + tr.Name + " (" + string.Join(", ", fx.Select(f => f.Title).ToArray()) + "). Every setting is editable; Ctrl+Z undoes it.");
    }
    internal Func<string, string> aiSaveAskHook;
    internal bool AiSave()
    {
        if (aiResult == null) return false; var fx = VocalStyles.BuildChain(aiResult.Chain); if (fx.Count == 0) return false;
        string nm = aiSaveAskHook != null ? aiSaveAskHook(aiResult.Name) : Ask("Name for this preset (it is also shared with the community):", aiResult.Name.Replace("AI: ", "")); if (nm == null || nm.Trim().Length == 0) return false; nm = nm.Trim();
        Presets.SaveChain(nm, fx); Toast("Saved your preset \"" + nm + "\" (" + fx.Count + " plugins). It is in PRESETS... on any track.");
        if (aiSaveAskHook == null) CommunityPresets.AutoShare("chain", nm, Presets.ChainText(fx), ProjectLibrary.ProfileName, msg => { if (msg != null) BeginInvoke(new Action(() => Toast(msg))); });
        return true;
    }
    internal bool AiKey(char c)
    {
        if (!aiOpen) return false;
        if (c == '\r') AiGenerate(); else if (c == '\b') { if (aiPrompt.Length > 0) aiPrompt = aiPrompt.Substring(0, aiPrompt.Length - 1); } else if (!char.IsControl(c) && aiPrompt.Length < 200) aiPrompt += c;
        return true;
    }
    void PaintAiSheet(Graphics g) { var look = BeginBrand(); try { PaintAiSheet0(g); } finally { EndBrand(look); } }
    void PaintAiSheet0(Graphics g)
    {
        if (!aiOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); Reg(all, () => aiOpen = false);
        float sw = Math.Min(820, W - 24), sh = Math.Min(H - 30, 640); var sheet = PopMove("aipreset", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonCyan, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(200, NeonCyan), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, aiFx ? "AI EFFECT GENERATOR" : aiBus ? "AI BUS GENERATOR" : "AI PRESET GENERATOR", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 400, 34), StringAlignment.Near, true); var exs = AiExs;
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => aiOpen = false);
        { var back = Rc(close.X - 12 - 170, sheet.Y + 14, 170, 32); if (aiFx) { Btn(g, back, "ADD EFFECT", 0, () => { aiOpen = false; OpenBrowser(browserMaster && aiFxFromBrowser, aiFxFromBrowser ? browserTrack : selTrack); }, 12); Hint2(back, "Back to the list of " + FxRegistry.All.Length + " effects"); } else if (aiBus) { Btn(g, back, "BUS PRESETS", 0, () => { aiOpen = false; OpenBusPresets(); }, 12); Hint2(back, "Back to the list of " + BusPresets.All.Count + " bus presets"); } else { Btn(g, back, "VOCAL PRESETS", 0, () => { aiOpen = false; OpenStyles(); }, 12); Hint2(back, "Back to the list of " + VocalStyles.All.Count + " presets"); } }
        Txt(g, aiFx ? "Describe the effect in your own words: what it is (reverb, delay, autotune, distortion...) and how it should sound. It sets it up for you." : aiBus ? "Describe the bus in your own words: what goes into it (vocals, drums, 808...), the genre, and how it should sound. It builds the plugins." : "Describe the sound in your own words: an artist or a genre, and the effects you want. It builds the plugins for you.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 50, sheet.Width - 44, 20), StringAlignment.Near, false);
        // the prompt box (type, Enter generates, Ctrl+V pastes)
        var pb = Rc(sheet.X + 22, sheet.Y + 80, sheet.Width - 44 - 150, 44); FillRR(g, TM.C(235, 10, 5, 26), pb, 10); StrokeRR(g, NeonCyan, pb, 10, 1.8f); Reg(pb, () => { });
        bool blink = (int)(t * 2) % 2 == 0; Txt(g, aiPrompt.Length == 0 ? (blink ? "|" : " ") + "  e.g. " + exs[0] : aiPrompt + (blink ? "|" : ""), 14, aiPrompt.Length == 0 ? DimC : Ink, Rc(pb.X + 14, pb.Y, pb.Width - 50, pb.Height), StringAlignment.Near, false);
        if (aiPrompt.Length > 0) Btn(g, Rc(pb.Right - 34, pb.Y + 10, 24, 24), "x", 3, () => { aiPrompt = ""; aiResult = null; }, 12);
        var gb = Rc(pb.Right + 10, pb.Y, 140, 44); Btn(g, gb, "GENERATE", 2, () => AiGenerate(), 14); Hint2(gb, "Build the plugins from what you typed (Enter does the same)");
        // examples to start from
        float ex = sheet.X + 22, ey = pb.Bottom + 12; Txt(g, "TRY", 10, DimC, Rc(ex, ey, 36, 26), StringAlignment.Near, true); ex += 38;
        foreach (var e in exs) { float cw = 18 + e.Length * 6.3f; if (ex + cw > sheet.Right - 22) { ex = sheet.X + 60; ey += 32; } if (ey > sheet.Y + 230) break; string ee = e; Chip(g, Rc(ex, ey, cw, 26), e, aiPrompt == e, () => { aiPrompt = ee; AiGenerate(); }); ex += cw + 6; }
        {   // REGENERATE (another take on the same words) and RANDOMISE (a surprise) on every AI generator
            float by = sheet.Bottom - 62; var rg = Rc(sheet.Right - 22 - 170 - 12 - 170, by, 170, 42); var rz = Rc(sheet.Right - 22 - 170, by, 170, 42);
            Btn(g, rg, "REGENERATE", 0, () => AiRegenerate(), 13); Hint2(rg, "Another take on the same sound: the same plugins with their amounts nudged a little (press it again for more)");
            Btn(g, rz, "RANDOMISE", 0, () => AiRandomise(), 13); Hint2(rz, "A surprise: random words and what they make (press USE if you like it)");
        }
        float ry = ey + 42; var res = Rc(sheet.X + 22, ry, sheet.Width - 44, sheet.Bottom - 20 - ry - 52); FillRR(g, TM.C(150, 22, 12, 46), res, 14); StrokeRR(g, Color.FromArgb(90, NeonViolet), res, 14, 1);
        if (aiResult == null && aiFx) { using (var hf = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit }) using (var hb = new SolidBrush(DimC)) g.DrawString("Type what you want and press GENERATE, pick an example above, or press RANDOMISE for a surprise.\n\nIt understands reverbs (room, hall, plate, chamber, spring, cathedral, ambient; tiny to huge; dark or bright; gated, reverse, shimmer, ducked), delays (1/4, 1/8, dotted, triplet, 1/16, ping pong, slapback, tape, long tail, ducked), autotune and harmonies in any key (F# minor, hard, natural), pitch and formant (up 7, octave down, deeper voice, chipmunk), telephone, radio, megaphone, robot, vocoder, distortion (tube, tape, fuzz, clipper), bitcrush, EQ (bass boost, mud, presence, air), compressors, gates, de-essers, chorus, phaser, flanger, tremolo, auto-pan, filter sweeps, stutter, glitch, tape stop, 8D, wide, demon voices and censor bleeps.", GfxCache.Font(13, false), hb, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), hf); return; }
        if (aiResult == null && aiBus) { Txt(g, "Type what you want and press GENERATE, or pick one of the examples above.\nIt understands the kind of bus (vocal, ad-lib, backing vocals, drums, 808 or bass, beat, reverb, echo, FX, crush, glue, mix), genres and artists (it uses their genre), flavours (clean, warm, punchy, wide, dark, bright, vintage, heavy) and changes: more glue, saturation, distortion, dry, lots of reverb, echo (1/4, 1/8, dotted, triplet), telephone, radio, mono, very wide, de-ess, loud.", 12, DimC, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), StringAlignment.Near, false); return; }
        if (aiResult == null) { Txt(g, "Type what you want and press GENERATE, or pick one of the examples above.\nIt understands artists (Drake, Juice WRLD, Eminem, Central Cee, Mazza...), genres, and effects: stutter, delay (1/4, 1/8, dotted, triplet, ping pong), slapback, reverb, hall, plate, dry, dark, bright, autotune, natural, harmony, octave, distortion, telephone, radio, deep or baby voice, wide, doubled, loud, soft, 8D, robot... Add \"a bit\" or \"lots of\" to set how strong.", 12, DimC, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), StringAlignment.Near, false); return; }
        Txt(g, aiResult.Name, 17, Color.FromArgb(255, 255, 214, 110), Rc(res.X + 16, res.Y + 10, res.Width - 32, 28), StringAlignment.Near, true);
        float ly = res.Y + 42; Txt(g, "WHAT IT UNDERSTOOD", 10, NeonCyan, Rc(res.X + 16, ly, 300, 16), StringAlignment.Near, true); ly += 18;
        foreach (var u in aiResult.Understood) { if (ly > res.Bottom - 74) break; Txt(g, "•  " + u, 12, Ink, Rc(res.X + 24, ly, res.Width - 40, 19), StringAlignment.Near, false); ly += 19; }
        ly += 6; Txt(g, "PLUGINS" + (aiFx ? "   (added to YOUR PLUGINS)" : aiBus ? "   (for a " + aiResult.BusName + ")" : aiResult.BusName != null ? "   (+ " + aiResult.BusName + ")" : ""), 10, NeonCyan, Rc(res.X + 16, ly, res.Width - 32, 16), StringAlignment.Near, true); ly += 18;
        var titles = VocalStyles.BuildChain(aiResult.Chain).Select(f => f.Title).ToList(); using (var pf = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit }) using (var pbr = new SolidBrush(Ink)) g.DrawString(string.Join("  >  ", titles), GfxCache.Font(12, false), pbr, Rc(res.X + 24, ly, res.Width - 40, Math.Max(20, res.Bottom - ly - 8)), pf);   // the plugins in order
        var ub = Rc(sheet.X + 22, sheet.Bottom - 62, 200, 42); Btn(g, ub, aiFx ? (aiFxFromBrowser && browserMaster ? "ADD TO MASTER" : "ADD TO TRACK") : aiBus ? "USE ON BUS" : "USE ON TRACK", 2, () => AiUse(), 14); Hint2(ub, aiFx ? "Add these plugins to YOUR PLUGINS (Ctrl+Z undoes it)" : aiBus ? "Put these plugins on the picked bus, the bus the picked track goes into, or a new bus (Ctrl+Z undoes it)" : "Put these plugins on the selected track (Ctrl+Z undoes it)");
        var sb = Rc(ub.Right + 12, ub.Y, 200, 42); Btn(g, sb, "SAVE AS MY PRESET", 1, () => AiSave(), 13); Hint2(sb, "Keep it as your own preset (in PRESETS... on any track), shared in Community presets");
    }
}
