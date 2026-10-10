using System;
using System.Drawing;
using System.Windows.Forms;

// The FX page: a chain of 11 effects. Click a box to edit it, the light turns it on or off, the arrows move it earlier or later in the chain.
partial class SynthCanvas
{
    int fxSel = 0;
    static readonly string[][] FxKnobs = {
        new[] { "dstdrive:DRIVE", "dsttone:TONE", "dstmix:WET/DRY" },
        new[] { "satdrive:DRIVE", "sattone:TONE", "satmix:WET/DRY" },
        new[] { "cpthr:THRESHOLD", "cpratio:RATIO", "cpatt:ATTACK", "cprel:RELEASE", "cpmake:MAKEUP", "cpmix:WET/DRY" },
        new[] { "eqlf:LOW FREQ", "eqlg:LOW GAIN", "eqmf:MID FREQ", "eqmg:MID GAIN", "eqmq:MID Q", "eqhf:HIGH FREQ", "eqhg:HIGH GAIN", "eqmix:WET/DRY" },
        new[] { "phrate:RATE", "phdepth:DEPTH", "phfb:FEEDBACK", "phmix:WET/DRY" },
        new[] { "flrate:RATE", "fldepth:DEPTH", "fldelay:DELAY", "flfb:FEEDBACK", "flmix:WET/DRY" },
        new[] { "chrate:RATE", "chdepth:DEPTH", "chmix:WET/DRY" },
        new[] { "dlfb:FEEDBACK", "dlmix:WET/DRY" },
        new[] { "rvsize:SIZE", "rvdamp:DAMPING", "rvmix:WET/DRY" },
        new[] { "wdwidth:WIDTH", "wdbass:MONO BELOW", "wdmix:WET/DRY" },
        new[] { "bbfreq:FREQUENCY", "bbgain:BOOST", "bbq:WIDTH", "bbharm:HARMONICS", "bbtight:TIGHTEN" } };
    static readonly string[] FxChoice = { "dstmode", "satmode", null, null, "phstages", null, null, "dlsync", null, null, null };
    static readonly string[] FxHelp = {
        "Crunch and grit. Hard clip, foldback or fuzz.", "Warm tape or tube saturation: adds weight and glue.", "Evens out the level so the bass stays solid. Raise MAKEUP to compensate.", "Three-band tone control: low shelf, mid bell, high shelf.",
        "Sweeping notches for a swirling, jet-like movement.", "Short delay with feedback: metallic sweeps.", "Thickens the sound with slowly moving copies.", "Echoes locked to the song tempo.", "Space around the sound.", "Makes the sound wider or narrower. Bass below the MONO BELOW frequency stays centred.", "Fine-tunable low-end boost with a graph you can drag." };

    int ChainAt(int pos) { return (int)Math.Round(P("fo" + pos).Val); }
    void ChainSwap(int a, int b)
    {
        if (b < 0 || b > 10) return; var pa = P("fo" + a); var pb = P("fo" + b); float va = pa.Val, vb = pb.Val; SetVal(pa, vb); SetVal(pb, va);
    }

    void PaintFx(Graphics g)
    {
        float y = 66, x0 = 8, cw = (W - 16) / 11f;
        Txt(g, "EFFECT CHAIN: sound flows left to right. Click a box to edit it, click its light to switch it on or off, use the arrows to change the order.", 11, Dim, Rc(8, y - 2, W - 16, 16), StringAlignment.Near, false);
        for (int pos = 0; pos < 11; pos++)
        {
            int e = ChainAt(pos); int p0 = pos; bool on = P(SynthFx.FxOnIds[e]).Val > 0.5f, sel = e == fxSel; var r = Rc(x0 + pos * cw + 1, y + 16, cw - 4, 50);
            Fill(g, sel ? PanelHi : Panel, r, 7); Stroke(g, sel ? Acc : (on ? Color.FromArgb(160, Good) : Edge), r, 7, sel ? 2 : 1);
            var rg = Add(r); rg.Tip = SynthFx.FxNames[e] + ": click to edit"; int ee = e; rg.Down = pt => { fxSel = ee; };
            Txt(g, SynthFx.FxNames[e], 11, on ? Ink : Dim, Rc(r.X + 20, r.Y + 3, r.Width - 22, 18), StringAlignment.Near, sel);
            var led = Rc(r.X + 3, r.Y + 3, 18, 18); var pw = P(SynthFx.FxOnIds[e]); bool onNow = on;
            using (var b = new SolidBrush(on ? Good : Color.FromArgb(70, 60, 90))) g.FillEllipse(b, led.X + 4, led.Y + 4, 10, 10); if (on) using (var pen = new Pen(Color.FromArgb(90, Good), 3f)) g.DrawEllipse(pen, led.X + 2, led.Y + 2, 14, 14);
            var lr = Add(led); lr.Tip = "Switch " + SynthFx.FxNames[e] + (on ? " off" : " on"); lr.Down = pt => { SetVal(pw, onNow ? 0 : 1); };
            var lb = Rc(r.X + 6, r.Y + 28, 34, 16); var rb = Rc(r.Right - 40, r.Y + 28, 34, 16); Txt(g, (pos + 1).ToString(), 10, Dim, Rc(r.X + 44, r.Y + 28, r.Width - 88, 16), StringAlignment.Center, false);
            if (pos > 0) { Fill(g, Hot(lb) ? PanelHi : Color.FromArgb(18, 10, 34), lb, 4); Stroke(g, Edge, lb, 4, 1); Txt(g, "<", 11, Ink, lb, StringAlignment.Center, true); var a = Add(lb); a.Tip = "Move earlier in the chain"; a.Down = pt => { ChainSwap(p0, p0 - 1); fxSel = ee; }; }
            if (pos < 10) { Fill(g, Hot(rb) ? PanelHi : Color.FromArgb(18, 10, 34), rb, 4); Stroke(g, Edge, rb, 4, 1); Txt(g, ">", 11, Ink, rb, StringAlignment.Center, true); var a = Add(rb); a.Tip = "Move later in the chain"; a.Down = pt => { ChainSwap(p0, p0 + 1); fxSel = ee; }; }
        }
        var pn = Rc(8, y + 72, W - 16, 252 - 72 + 4); ModuleFrame(g, pn, SynthFx.FxNames[fxSel].ToUpper(), SynthFx.FxOnIds[fxSel]);
        Txt(g, FxHelp[fxSel], 11, Dim, Rc(pn.X + 200, pn.Y + 4, pn.Width - 210, 18), StringAlignment.Near, false);
        string ch = FxChoice[fxSel]; if (ch != null) Choose(g, ch, Rc(pn.X + 12, pn.Y + 32, 150, 22), Acc2);
        if (fxSel == 10)
        {
            var gr = Rc(pn.X + 8, pn.Y + 30, pn.Width - 16, 74); DrawBoostGraph(g, gr);
            var ks = FxKnobs[10]; float dx = (pn.Width - 60) / ks.Length; for (int i = 0; i < ks.Length; i++) { var kv = ks[i].Split(':'); Knob(g, kv[0], pn.X + 40 + dx * i, pn.Y + 150, kv[1], i < 2 ? Acc2 : Acc, 18); }
        }
        else
        {
            var ks = FxKnobs[fxSel]; float dx = Math.Min(130, (pn.Width - 60) / ks.Length); float sx = pn.X + 60 + (ch != null ? 40 : 0);
            for (int i = 0; i < ks.Length; i++) { var kv = ks[i].Split(':'); Knob(g, kv[0], sx + dx * i, pn.Y + 110, kv[1], kv[1] == "WET/DRY" ? Acc2 : Acc, 22); }
        }
    }
}
