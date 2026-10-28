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

    internal void OpenAllFx() { allFxOpen = true; allBusMode = false; allFxScroll = 0; ntOpen = false; busOpen = false; stylesOpen = false; }
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
        Txt(g, allBusMode ? "ALL BUSES" : "ALL PLUGINS", 22, Ink, Rc(sheet.X + 22, sheet.Y + 12, 300, 34), StringAlignment.Near, true);
        Txt(g, allBusMode ? "Every bus in one place: its level, mute, the tracks that go into it and its plugins. + ADD BUS makes a new one." : "Every track and every bus with their plugins in one place. + ADD, COPY / PASTE PRESET and COPY / PASTE ALL PLUGINS work on that column; every plugin has COPY and PASTE; press a plugin to open it.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 46, sheet.Width - 200, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 120, sheet.Y + 14, 100, 34); Btn(g, close, "DONE", 2, () => allFxOpen = false, 13); Hint2(close, "Close this panel (Esc does the same)");
        { var ab = Rc(close.X - 12 - 150, close.Y, 150, 34); Btn(g, ab, "+ ADD BUS", 1, () => { var nb = AddBus(); busEdit = nb; selFx = -1; allFxScroll = float.MaxValue; Toast(nb.Name + " made. Press TRACKS INTO THIS BUS to send tracks into it, and + ADD for its plugins."); }, 13); Hint2(ab, "Make a new bus (a group: send tracks into it and its plugins work on all of them together)");
          var sw = Rc(ab.X - 12 - 150, close.Y, 150, 34); Btn(g, sw, allBusMode ? "ALL PLUGINS" : "ALL BUSES", 1, () => { allBusMode = !allBusMode; allFxScroll = 0; }, 13); Hint2(sw, allBusMode ? "Show every track and bus with their plugins" : "Show only your buses, with their levels");
          var ra = Rc(sw.X - 12 - 200, close.Y, 200, 34); if (ra.X > sheet.X + 330) { Btn(g, ra, "REMOVE ALL PLUGINS", 3, () => RemoveEveryPlugin(), 12); Hint2(ra, allBusMode ? "Take every plugin off every bus (Ctrl+Z puts them back)" : "Take every plugin off every track and bus (instruments stay; Ctrl+Z puts them back)"); }
          if (allBusMode)
          {   // the AI BUS GENERATOR and BUS PRESETS work on the picked bus (the SELECTED column), or the only bus there is
              Action pickBus = () => { if (busEdit == null || !eng.Tracks.Contains(busEdit)) { var bs0 = eng.Tracks.FindAll(t => t.IsBus); if (bs0.Count > 0) busEdit = bs0[0]; } };
              var aig = Rc(ra.X - 12 - 210, close.Y, 210, 34); if (aig.X > sheet.X + 330) { Btn(g, aig, "AI BUS GENERATOR", 2, () => { pickBus(); allFxOpen = false; OpenAiBus(); }, 12); Hint2(aig, "Describe the bus in your own words (drill drum bus, punchy and dark...) and put it on the selected bus"); }
              var bpb = Rc(aig.X - 12 - 150, close.Y, 150, 34); if (bpb.X > sheet.X + 330) { Btn(g, bpb, "BUS PRESETS", 1, () => { pickBus(); allFxOpen = false; OpenBusPresets(); }, 12); Hint2(bpb, BusPresets.All.Count + " presets for buses, for the selected bus"); }
          } }
        Track[] rows; lock (eng.Sync) rows = eng.Tracks.FindAll(t => t.Host == null && (!allBusMode || t.IsBus)).ToArray();
        var area = Rc(sheet.X + 14, sheet.Y + 76, sheet.Width - 28, sheet.Height - 90);
        if (rows.Length == 0) { Txt(g, allBusMode ? "NO BUSES YET: PRESS + ADD BUS." : "NO TRACKS YET: ADD MUSIC OR A NEW TRACK FIRST.", 14, DimC, area, StringAlignment.Center, true); return; }
        float colW = 244, gap = 12, total = rows.Length * (colW + gap) - gap; if (total > area.Width) area = Rc(area.X, area.Y, area.Width, area.Height - 44);   // room for the logo scroll bar under the columns
        float maxS = Math.Max(0, total - area.Width); allFxScroll = Math.Max(0, Math.Min(maxS, allFxScroll));
        { float downX = 0, downS = 0; var pan = Reg(area, () => { downX = MousePos.X; downS = allFxScroll; }); pan.OnDrag = pt => allFxScroll = Math.Max(0, Math.Min(maxS, downS - (pt.X - downX))); }   // grab any empty part and drag it sideways, like a phone
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int ci = 0; ci < rows.Length; ci++)
        {
            var tr = rows[ci]; float cx = area.X + ci * (colW + gap) - allFxScroll; if (cx + colW < area.X || cx > area.Right) continue;
            int ti = eng.Tracks.IndexOf(tr); var col = Rc(cx, area.Y, colW, area.Height); var nc = Neon(tr.Color); bool picked = tr.IsBus ? busEdit == tr : (busEdit == null && ti == selTrack);   // a bus is picked when the plugin panel edits it
            if (picked) { Glow(g, nc, col, 14, 1.3f, 6); FillRR(g, TM.C(240, 52, 26, 96), col, 14); using (var b = new SolidBrush(Color.FromArgb(40, nc))) using (var p = RR(col, 14)) g.FillPath(b, p); StrokeRR(g, nc, col, 14, 3.2f); }   // the selected track: it glows in its colour, a thick lit edge and a SELECTED tag
            else { FillRR(g, TM.C(150, 30, 16, 58), col, 14); StrokeRR(g, Color.FromArgb(90, nc), col, 14, 1.1f); }
            { int tixc = ti; float dX = 0, dS = 0; var tkc = tr; var crg = Reg(col, () => { PickAllFxColumn(tkc, tixc); dX = MousePos.X; dS = allFxScroll; }); crg.OnDrag = pt => allFxScroll = Math.Max(0, Math.Min(maxS, dS - (pt.X - dX))); }   // a click anywhere on a column picks that track; a drag slides the columns
            using (var b = new SolidBrush(nc)) g.FillRectangle(b, col.X + 14, col.Y + 8, colW - 28, picked ? 6 : 4);
            if (picked) { var sp = Rc(col.Right - 12 - 78, col.Y + 18, 78, 20); FillRR(g, nc, sp, 10); Txt(g, "SELECTED", 10, Color.Black, sp, StringAlignment.Center, true); }
            var nameR = Rc(col.X + 12, col.Y + 16, colW - 24 - (picked ? 84 : 0), 24); Txt(g, (tr.IsBus ? "BUS: " : "") + tr.Name, 14, Ink, nameR, StringAlignment.Near, true);
            { int tix = ti; var tkn = tr; Reg(nameR, () => PickAllFxColumn(tkn, tix)); Hint2(nameR, tr.IsBus ? "Edit this bus in the plugin panel" : "Pick this track"); }
            if (tr.IsBus) { var srcs = eng.Tracks.FindAll(x => x.Out == tr && x.Host == null && !x.IsBus); Txt(g, srcs.Count == 0 ? "NO TRACKS GO INTO IT YET" : "FROM: " + string.Join(", ", srcs.ConvertAll(x => x.Name).ToArray()), 9.5f, srcs.Count == 0 ? DimC : Color.FromArgb(255, 255, 214, 110), Rc(col.X + 12, col.Y + 38, colW - 24, 14), StringAlignment.Near, false); }   // which tracks feed this bus
            float by = col.Y + (tr.IsBus ? 56 : 46), bx = col.X + 10;
            var addB = Rc(bx, by, 54, 26); var cpB0 = Rc(addB.Right + 4, by, 78, 26); var ppB = Rc(cpB0.Right + 4, by, col.Right - 10 - cpB0.Right - 4, 26);
            { int tix = ti; var tka = tr; Btn(g, addB, "+ ADD", 1, () => { PickAllFxColumn(tka, tix); OpenBrowser(false, tix); }, 11); Hint2(addB, "Add an effect to " + tr.Name); }
            { var tk = tr; var at0 = cpB0; Btn(g, cpB0, "COPY PRESET", 1, () => CopyTrackPreset(tk, at0), 10); Hint2(cpB0, "Copy a preset of " + tr.Name + " (every plugin in it, with its settings)"); }
            { var tk = tr; int tix = ti; Btn(g, ppB, "PASTE PRESET", presetCopy.Count > 0 ? 2 : 0, () => { selTrack = tix; PastePreset(tk); }, 10); Hint2(ppB, presetCopy.Count > 0 ? "Put the copied preset " + presetCopyName + " on " + tr.Name : "Copy a preset first (COPY on a preset)"); }
            Fx[] fxs; lock (eng.Sync) fxs = tr.Fx.ToArray();
            { var ca = Rc(bx, by + 32, (colW - 24) / 2, 26); var pa2 = Rc(ca.Right + 4, by + 32, col.Right - 10 - ca.Right - 4, 26); var tk = tr; int tix = ti;
              Btn(g, ca, "COPY ALL PLUGINS", 1, () => CopyAllFx(tk), 9); Hint2(ca, "Copy every plugin of " + tr.Name + " (with its settings and its preset boxes)");
              Btn(g, pa2, "PASTE ALL PLUGINS", allCopy.Count > 0 ? 2 : 0, () => { selTrack = tix; PasteAllFx(tk); }, 9); Hint2(pa2, allCopy.Count > 0 ? "Add all " + allCopy.Count + " copied plugins (from " + allCopyFrom + ") to " + tr.Name : "Copy all the plugins of a track first (COPY ALL PLUGINS)"); }
              { var tk = tr; int tix = ti; var pp1 = Rc(bx, by + 64, colW - 20, 26); Btn(g, pp1, fxCopyType.Length > 0 ? "PASTE PLUGIN: " + fxCopyTitle.ToUpper() : "PASTE PLUGIN", fxCopyType.Length > 0 ? 2 : 0, () => { selTrack = tix; PasteFxAsNew(tk); }, 9); Hint2(pp1, fxCopyType.Length > 0 ? "Add the copied " + fxCopyTitle + " to " + tr.Name + " as a new plugin, with all its settings" : "Copy one plugin first: press COPY on any plugin (its settings come with it)"); }
            by += 64;   // (the PASTE PLUGIN row)
            if (tr.IsBus) { var rb = Rc(bx, by + 32, colW - 20, 26); var tkb = tr; int nIn = eng.Tracks.FindAll(x => x.Out == tr && x.Host == null && !x.IsBus).Count; Btn(g, rb, "TRACKS INTO THIS BUS (" + nIn + ")...", 1, () => BusRouteMenu(tkb, rb), 9); Hint2(rb, "Tick the tracks that go through " + tr.Name); by += 32; }
            if (tr.IsBus) { var bt = tr; var mb = Rc(bx, by + 32, 30, 22); Btn(g, mb, "M", bt.Mute ? 4 : 0, () => bt.Mute = !bt.Mute, 11); Hint2(mb, bt.Mute ? "Unmute the bus" : "Mute the bus (every track in it goes quiet)");   // the level of the bus: mute, a fader and its dB, and delete
              float frac = Clamp01((bt.VolDb + 48f) / 54f); var fs = Slider(g, Rc(mb.Right + 8, by + 34, colW - 20 - 38 - 92, 18), frac, fv => bt.VolDb = fv < 0.003f ? -48f : (float)Math.Round(fv * 54f - 48f, 1), Accent2); fs.OnDouble = () => bt.VolDb = 0;
              ValueBox(g, Rc(col.Right - 10 - 88, by + 32, 54, 22), (bt.VolDb <= -47.9f ? "-inf" : bt.VolDb.ToString("+0.0;-0.0;0.0")) + " dB", bt.VolDb <= -47.9f ? "-inf" : Num(bt.VolDb), s0 => { float v; if (ParseNum(s0, out v)) bt.VolDb = v <= -47.9f ? -48f : Math.Min(6f, v); }, 10);   // click and type the level of the bus
              var xb2 = Rc(col.Right - 10 - 30, by + 32, 30, 22); Btn(g, xb2, "x", 3, () => { if (AskYes("Delete \"" + bt.Name + "\"? Tracks sent to it go straight to the master.", "Delete bus")) { if (busEdit == bt) busEdit = null; DeleteTrackQuiet(bt); } }, 11); Hint2(xb2, "Delete this bus");
              by += 32; }
            float ry = by + 36, bottom = col.Bottom - 10; string lastKey = null; int shown = 0;
            if (fxs.Length == 0) Txt(g, "NO PLUGINS YET", 11, DimC, Rc(col.X + 14, ry, colW - 28, 22), StringAlignment.Near, false);
            for (int i = 0; i < fxs.Length; i++)
            {
                var f = fxs[i]; int fi = i; string key = (!(f is InstrumentFx) && f.Preset.Length > 0) ? PresetKey(f) : "";
                if (key != (lastKey ?? "") || lastKey == null)
                {
                    if (key.Length > 0)
                    {
                        if (ry + 28 > bottom) break; var hr = Rc(col.X + 10, ry, colW - 20, 24); FillRR(g, TM.C(235, 34, 16, 66), hr, 8); StrokeRR(g, Color.FromArgb(220, NeonViolet), hr, 8, 1.3f);   // the preset the plugins below came from, in its own box
                        Txt(g, "PRESET: " + f.Preset.ToUpperInvariant(), 10, NeonViolet, Rc(hr.X + 9, hr.Y, hr.Width - 16, hr.Height), StringAlignment.Near, true);
                        ry += 30;   /* COPY PRESET at the top of the column copies it */
                    }
                    else if (lastKey != null && lastKey.Length > 0) { if (ry + 20 > bottom) break; Txt(g, "YOUR PLUGINS", 10, NeonPink, Rc(col.X + 12, ry, colW - 24, 18), StringAlignment.Near, true); ry += 20; }
                    lastKey = key;
                }
                if (ry + 26 > bottom) break;
                var row = Rc(col.X + 10, ry, colW - 20, 26); bool hot = Hot(row); if (ti == selTrack && fi == selFx) PaintPickedFx(g, row, 7); else FillRR(g, hot ? TM.C(200, 70, 36, 130) : TM.C(150, 44, 24, 80), row, 7);   // the plugin open in the plugin panel stands out here too
                { var tk = tr; int tix = ti; Reg(row, () => { allFxOpen = false; tab = 0; PickAllFxColumn(tk, tix); selFx = fi; }); Hint2(row, "Open " + f.Title + " of " + tr.Name + ". " + FxHelp.About(f)); }
                var dot = Rc(row.X + 5, row.Y + 4, 18, 18);   /* the on/off circle, 50% bigger */ using (var b2 = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b2, dot); { var ff = f; Reg(Rc(row.X, row.Y, 28, 26), () => ff.Bypass = !ff.Bypass); }
                float cpl = CopyPasteBtns(g, f, tr, row.Right - 28, row.Y + 3, 20);   // COPY and PASTE on every plugin
                RowTitle(g, f.Title, 12, f.Bypass ? DimC : Ink, Rc(row.X + 30, row.Y, cpl - row.X - 32, 26), false);
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

partial class StudioForm
{
    // ALL PLUGINS: pick a column; a bus is edited in the plugin panel (like EDIT in the Buses sheet), a track is selected as usual
    // REMOVE ALL PLUGINS: every plugin off every track and bus on the page (instruments stay, so instrument tracks still play); asks first, and one Ctrl+Z puts them all back
    internal Func<string, bool> removeAllAskHook;
    internal int RemoveEveryPlugin()
    {
        Track[] rows; lock (eng.Sync) rows = eng.Tracks.FindAll(t => !allBusMode || t.IsBus).ToArray(); int n = 0; foreach (var t in rows) foreach (var f in t.Fx) if (!(f is InstrumentFx)) n++;
        if (n == 0) { Toast("There are no plugins to remove."); return 0; }
        string q = "Remove all " + n + " plugin" + (n == 1 ? "" : "s") + " from every " + (allBusMode ? "bus" : "track and bus") + "?" + (allBusMode ? "" : " Instruments (Bass Lab, VST instruments) stay.") + "\n\nCtrl+Z puts them all back.";
        if (!(removeAllAskHook != null ? removeAllAskHook(q) : AskYes(q, "Remove all plugins"))) return 0;
        PushUndo();
        lock (eng.Sync) foreach (var t in rows) { var gone = t.Fx.FindAll(f => !(f is InstrumentFx)); foreach (var f in gone) { t.Fx.Remove(f); t.Auto.RemoveAll(al => al.Fx == f); retired.Add(f); } }   // undoable: retired, not disposed, since the undo snapshot still holds them
        selFx = -1; Toast(n + " plugin" + (n == 1 ? " was" : "s were") + " removed. Ctrl+Z puts them back."); return n;
    }
    internal bool allBusMode;   // ALL BUSES: the same sheet showing only the buses, with their levels
    internal void OpenAllBuses() { allFxOpen = true; allBusMode = true; allFxScroll = 0; busOpen = false; tab = 0; }
    internal void PickAllFxColumn(Track t, int index)
    {
        if (t == null) return; selFx = -1;
        if (t.IsBus) busEdit = t; else { busEdit = null; selTrack = Math.Max(0, index); }
    }
    // TRACKS INTO THIS BUS: tick the tracks that go through the bus (each track's pieces go with it)
    internal void BusRouteMenu(Track bus, RectangleF at)
    {
        var m = Win.NewMenu(); var tracks = eng.Tracks.FindAll(x => x.Host == null && !x.IsBus);
        if (tracks.Count == 0) { Toast("Add a track first."); return; }
        foreach (var t0 in tracks) { var t = t0; var it = new ToolStripMenuItem(t.Name + (t.Out != null && t.Out != bus ? "   (now in " + t.Out.Name + ")" : "")) { Checked = t.Out == bus }; it.Click += (a, b) => SetTrackBus(t, t.Out == bus ? null : bus); m.Items.Add(it); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    internal void SetTrackBus(Track t, Track bus)
    {
        PushUndo(); lock (eng.Sync) foreach (var x in eng.Tracks) if (x == t || x.Host == t) x.Out = bus;
        Toast(bus == null ? t.Name + " goes straight to the master again." : t.Name + " now goes through " + bus.Name + ".");
    }
}
