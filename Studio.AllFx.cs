using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ALL PLUGINS: every track's plugins side by side in one place (like the mixer of a big DAW): add, paste, switch off, copy, remove and open them for any track
// without picking the track first. And COPY PRESET / PASTE PRESET: a whole preset box (all its plugins with their settings) copied onto another track.
partial class StudioForm
{
    internal bool allFxOpen; float allFxScroll;
    internal readonly List<string[]> presetCopy = new List<string[]>(); internal string presetCopyName = "";   // the copied preset: { type, settings, bypass } for each plugin

    internal void OpenAllFx() { allFxOpen = true; allFxScroll = 0; ntOpen = false; busOpen = false; stylesOpen = false; }
    static string PresetKey(Fx f) { return f.PresetId.Length > 0 ? f.PresetId : f.Preset; }

    // COPY PRESET: every plugin of that preset box, with the settings it has now
    internal void CopyPreset(Track tr, string key)
    {
        presetCopy.Clear(); string name = "";
        lock (eng.Sync) foreach (var f in tr.Fx) if (!(f is InstrumentFx) && f.Preset.Length > 0 && PresetKey(f) == key) { presetCopy.Add(new[] { f.Type, f.Serialize(), f.Bypass ? "1" : "0", f.AutoKeyPending ? "1" : "0" }); name = f.Preset; }
        presetCopyName = name; if (presetCopy.Count == 0) { Toast("That preset has no plugins to copy."); return; }
        Toast("Copied the preset " + name + " (" + presetCopy.Count + " plugins with their settings). PASTE PRESET puts it on any track.");
    }
    // COPY ALL PLUGINS / PASTE ALL PLUGINS: every plugin of a track (with its settings and its preset boxes) onto another track; the plugins already there stay
    internal readonly List<string[]> allCopy = new List<string[]>(); internal string allCopyFrom = "";   // { type, settings, bypass, preset name, preset box }
    internal void CopyAllFx(Track tr)
    {
        allCopy.Clear(); int skipped = 0; lock (eng.Sync) foreach (var f in tr.Fx) { if (f is InstrumentFx) { skipped++; continue; } allCopy.Add(new[] { f.Type, f.Serialize(), f.Bypass ? "1" : "0", f.Preset, f.Preset.Length > 0 ? PresetKey(f) : "", f.AutoKeyPending ? "1" : "0" }); }
        allCopyFrom = tr.Name; if (allCopy.Count == 0) { Toast(tr.Name + " has no plugins to copy."); return; }
        Toast("Copied all " + allCopy.Count + " plugins of " + tr.Name + (skipped > 0 ? " (the instrument stays on its track)" : "") + ". PASTE ALL PLUGINS puts them on any track.");
    }
    internal void PasteAllFx(Track tr)
    {
        if (allCopy.Count == 0) { Toast("Nothing is copied yet: press COPY ALL PLUGINS on a track first."); return; }
        var made = new List<KeyValuePair<Fx, string[]>>(); foreach (var c in allCopy) { var nf = Presets.Make(c[0], c[1]); if (nf == null) continue; nf.Bypass = c[2] == "1"; nf.AutoKeyPending = c.Length > 5 && c[5] == "1"; made.Add(new KeyValuePair<Fx, string[]>(nf, c)); }
        if (made.Count == 0) { Toast("Those plugins cannot be pasted here."); return; }
        ChainApply(tr, "All plugins pasted", () =>
        {
            lock (eng.Sync)
            {
                var inst = new List<Fx>(); var pres = new List<Fx>(); var own = new List<Fx>(); foreach (var f in tr.Fx) { if (f is InstrumentFx) inst.Add(f); else if (f.Preset.Length > 0) pres.Add(f); else own.Add(f); }
                var ids = new Dictionary<string, string>(); foreach (var kv in made) { var c = kv.Value; if (c[3].Length > 0) { string nid; if (!ids.TryGetValue(c[4], out nid)) { nid = Guid.NewGuid().ToString("N").Substring(0, 8); ids[c[4]] = nid; } kv.Key.Preset = c[3]; kv.Key.PresetId = nid; pres.Add(kv.Key); } else own.Add(kv.Key); }   // each preset keeps a box of its own
                tr.Fx.Clear(); tr.Fx.AddRange(inst); tr.Fx.AddRange(pres); tr.Fx.AddRange(own);
            }
        });
        selFx = -1; Toast("Pasted " + made.Count + " plugins from " + allCopyFrom + " onto " + tr.Name + ". The plugins it had stay.");
    }
    // COPY PRESET for a whole track: its one preset, or a list to choose from when it has more than one
    internal void CopyTrackPreset(Track tr, RectangleF at)
    {
        var keys = new List<string>(); var names = new List<string>(); lock (eng.Sync) foreach (var f in tr.Fx) if (!(f is InstrumentFx) && f.Preset.Length > 0 && !keys.Contains(PresetKey(f))) { keys.Add(PresetKey(f)); names.Add(f.Preset); }
        if (keys.Count == 0) { Toast(tr.Name + " has no preset on it yet (PRESETS... adds one). COPY on a plugin copies one plugin."); return; }
        if (keys.Count == 1) { CopyPreset(tr, keys[0]); return; }
        var m = Win.NewMenu(); for (int i = 0; i < keys.Count; i++) { string k = keys[i]; m.Items.Add("Copy the preset " + names[i]).Click += (a, b) => CopyPreset(tr, k); } m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    // PASTE PRESET: the copied preset becomes a box of its own on this track (your own plugins and the other presets stay)
    internal void PastePreset(Track tr)
    {
        if (presetCopy.Count == 0) { Toast("No preset is copied yet: press COPY on a preset box first."); return; }
        var chain = new List<Fx>(); foreach (var c in presetCopy) { var nf = Presets.Make(c[0], c[1]); if (nf == null) continue; nf.Bypass = c[2] == "1"; nf.AutoKeyPending = c.Length > 3 && c[3] == "1"; chain.Add(nf); }
        if (chain.Count == 0) { Toast("That preset cannot be pasted here."); return; }
        string nm = presetCopyName; ChainApply(tr, "Preset " + nm + " pasted", () => PutPreset(tr, nm, chain)); selFx = -1;
        Toast("Pasted the preset " + nm + " on " + tr.Name + " (" + chain.Count + " plugins).");
    }

    void PaintAllFxSheet(Graphics g)
    {
        if (!allFxOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => allFxOpen = false); using (var dim = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillRectangle(dim, all);
        var sheet = Rc(16, 24, W - 32, H - 48); FillRR(g, TM.C(252, 14, 8, 30), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "ALL PLUGINS", 22, Ink, Rc(sheet.X + 22, sheet.Y + 12, 300, 34), StringAlignment.Near, true);
        Txt(g, "Every track and its plugins in one place. + ADD, COPY PRESET, PASTE PRESET and COPY / PASTE ALL PLUGINS work on that column's track; every plugin has COPY and PASTE; press a plugin to open it.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 46, sheet.Width - 200, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 120, sheet.Y + 14, 100, 34); Btn(g, close, "DONE", 2, () => allFxOpen = false, 13); Hint2(close, "Close this panel (Esc does the same)");
        Track[] rows; lock (eng.Sync) rows = eng.Tracks.FindAll(t => t.Host == null).ToArray();
        var area = Rc(sheet.X + 14, sheet.Y + 76, sheet.Width - 28, sheet.Height - 90);
        if (rows.Length == 0) { Txt(g, "NO TRACKS YET: ADD MUSIC OR A NEW TRACK FIRST.", 14, DimC, area, StringAlignment.Center, true); return; }
        float colW = 244, gap = 12, total = rows.Length * (colW + gap) - gap; if (total > area.Width) area = Rc(area.X, area.Y, area.Width, area.Height - 44);   // room for the logo scroll bar under the columns
        float maxS = Math.Max(0, total - area.Width); allFxScroll = Math.Max(0, Math.Min(maxS, allFxScroll));
        { float downX = 0, downS = 0; var pan = Reg(area, () => { downX = MousePos.X; downS = allFxScroll; }); pan.OnDrag = pt => allFxScroll = Math.Max(0, Math.Min(maxS, downS - (pt.X - downX))); }   // grab any empty part and drag it sideways, like a phone
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int ci = 0; ci < rows.Length; ci++)
        {
            var tr = rows[ci]; float cx = area.X + ci * (colW + gap) - allFxScroll; if (cx + colW < area.X || cx > area.Right) continue;
            int ti = eng.Tracks.IndexOf(tr); var col = Rc(cx, area.Y, colW, area.Height); var nc = Neon(tr.Color); bool picked = ti == selTrack;
            FillRR(g, TM.C(picked ? 210 : 170, 30, 16, 58), col, 14); StrokeRR(g, Color.FromArgb(picked ? 230 : 120, nc), col, 14, picked ? 1.8f : 1.1f);
            using (var b = new SolidBrush(nc)) g.FillRectangle(b, col.X + 14, col.Y + 8, colW - 28, 4);
            var nameR = Rc(col.X + 12, col.Y + 16, colW - 24, 24); Txt(g, (tr.IsBus ? "BUS: " : "") + tr.Name, 14, Ink, nameR, StringAlignment.Near, true);
            { int tix = ti; Reg(nameR, () => { selTrack = tix; selFx = -1; }); Hint2(nameR, "Pick this track"); }
            float by = col.Y + 46, bx = col.X + 10;
            var addB = Rc(bx, by, 54, 26); var cpB0 = Rc(addB.Right + 4, by, 78, 26); var ppB = Rc(cpB0.Right + 4, by, col.Right - 10 - cpB0.Right - 4, 26);
            { int tix = ti; Btn(g, addB, "+ ADD", 1, () => { selTrack = tix; OpenBrowser(false, tix); }, 11); Hint2(addB, "Add an effect to " + tr.Name); }
            { var tk = tr; var at0 = cpB0; Btn(g, cpB0, "COPY PRESET", 1, () => CopyTrackPreset(tk, at0), 10); Hint2(cpB0, "Copy a preset of " + tr.Name + " (every plugin in it, with its settings)"); }
            { var tk = tr; int tix = ti; Btn(g, ppB, "PASTE PRESET", presetCopy.Count > 0 ? 2 : 0, () => { selTrack = tix; PastePreset(tk); }, 10); Hint2(ppB, presetCopy.Count > 0 ? "Put the copied preset " + presetCopyName + " on " + tr.Name : "Copy a preset first (COPY on a preset)"); }
            Fx[] fxs; lock (eng.Sync) fxs = tr.Fx.ToArray();
            { var ca = Rc(bx, by + 32, (colW - 24) / 2, 26); var pa2 = Rc(ca.Right + 4, by + 32, col.Right - 10 - ca.Right - 4, 26); var tk = tr; int tix = ti;
              Btn(g, ca, "COPY ALL PLUGINS", 1, () => CopyAllFx(tk), 9); Hint2(ca, "Copy every plugin of " + tr.Name + " (with its settings and its preset boxes)");
              Btn(g, pa2, "PASTE ALL PLUGINS", allCopy.Count > 0 ? 2 : 0, () => { selTrack = tix; PasteAllFx(tk); }, 9); Hint2(pa2, allCopy.Count > 0 ? "Add all " + allCopy.Count + " copied plugins (from " + allCopyFrom + ") to " + tr.Name : "Copy all the plugins of a track first (COPY ALL PLUGINS)"); }
            by += 32;
            float ry = by + 36, bottom = col.Bottom - 10; string lastKey = null; int shown = 0;
            if (fxs.Length == 0) Txt(g, "NO PLUGINS YET", 11, DimC, Rc(col.X + 14, ry, colW - 28, 22), StringAlignment.Near, false);
            for (int i = 0; i < fxs.Length; i++)
            {
                var f = fxs[i]; int fi = i; string key = (!(f is InstrumentFx) && f.Preset.Length > 0) ? PresetKey(f) : "";
                if (key != (lastKey ?? "") || lastKey == null)
                {
                    if (key.Length > 0)
                    {
                        if (ry + 22 > bottom) break; var hr = Rc(col.X + 10, ry, colW - 20, 20); Txt(g, "PRESET: " + f.Preset.ToUpperInvariant(), 10, NeonViolet, Rc(hr.X + 2, hr.Y, hr.Width - 60, 20), StringAlignment.Near, true);
                        ry += 24;   /* COPY PRESET at the top of the column copies it */
                    }
                    else if (lastKey != null && lastKey.Length > 0) { if (ry + 20 > bottom) break; Txt(g, "YOUR PLUGINS", 10, NeonPink, Rc(col.X + 12, ry, colW - 24, 18), StringAlignment.Near, true); ry += 20; }
                    lastKey = key;
                }
                if (ry + 26 > bottom) break;
                var row = Rc(col.X + 10, ry, colW - 20, 26); bool hot = Hot(row); if (ti == selTrack && fi == selFx) PaintPickedFx(g, row, 7); else FillRR(g, hot ? TM.C(200, 70, 36, 130) : TM.C(150, 44, 24, 80), row, 7);   // the plugin open in the plugin panel stands out here too
                { var tk = tr; int tix = ti; Reg(row, () => { allFxOpen = false; tab = 0; selTrack = tix; selFx = fi; }); Hint2(row, "Open " + f.Title + " of " + tr.Name); }
                var dot = Rc(row.X + 7, row.Y + 7, 12, 12); using (var b2 = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b2, dot); { var ff = f; Reg(Rc(row.X, row.Y, 26, 26), () => ff.Bypass = !ff.Bypass); }
                float cpl = CopyPasteBtns(g, f, tr, row.Right - 28, row.Y + 3, 20);   // COPY and PASTE on every plugin
                RowTitle(g, f.Title, 12, f.Bypass ? DimC : Ink, Rc(row.X + 26, row.Y, cpl - row.X - 28, 26), false);
                { var xb = Rc(row.Right - 26, row.Y + 3, 22, 20); var tk = tr; var ff = f; Btn(g, xb, "x", 3, () => { int at = tk.Fx.IndexOf(ff); if (at >= 0) { PushUndo(); lock (eng.Sync) { tk.Fx.RemoveAt(at); tk.Auto.RemoveAll(al => al.Fx == ff); retired.Add(ff); } var ic = ff as InstrumentFx; if (ic != null) ic.CloseWindow(); } selFx = -1; }, 11); Hint2(xb, "Remove " + f.Title + " from " + tr.Name); }   // undoable: retired, not disposed, since undo snapshots still hold it
                ry += 30; shown++;
            }
            if (shown < fxs.Length) Txt(g, "+" + (fxs.Length - shown) + " MORE: PRESS ONE TO OPEN THE TRACK", 10, DimC, Rc(col.X + 12, col.Bottom - 24, colW - 24, 18), StringAlignment.Near, false);
        }
        g.Restore(gs);
        regs.Add(new HitReg { R = area, OnWheel = d => allFxScroll = Math.Max(0, Math.Min(maxS, allFxScroll - d * 90)) });
        if (maxS > 0) HScroll(g, Rc(area.X, area.Bottom + 8, area.Width, 34), total, area.Width, () => allFxScroll, v => allFxScroll = v, colW + gap);   // the app's own logo scroll bar: drag the logo, click the bar, or hold an arrow
    }
}
