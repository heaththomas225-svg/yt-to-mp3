using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Purphex Music: the "New Track" sheet. + ADD TRACK / + TRACK open it: an empty audio track (raw: no recording starts and no effects are added),
// a virtual instrument, a MIDI track for playing your own keyboard, or a bus.
partial class StudioForm
{
    bool ntOpen; int ntWhere; float ntScroll;   // ntWhere: 0 = below the selected track, 1 = above it, 2 = at the end
    class NtItem { public string Title, Sub, Tag = ""; public Color Col; public int Icon; public Action Do; }

    void OpenNewTrackSheet(int where) { ntOpen = true; ntWhere = where; ntScroll = 0; }
    void CloseNewTrackSheet() { ntOpen = false; }
    int NtInsertAt()
    {
        int n = eng.Tracks.Count; if (n == 0) return 0; int sel = Math.Max(0, Math.Min(selTrack, n - 1));
        return ntWhere == 0 ? sel + 1 : (ntWhere == 1 ? sel : n);
    }

    // an empty audio track: nothing is recorded and no effects are added. ARM it and press MIC to record onto it, or drop a file on it.
    void AddEmptyAudioTrack()
    {
        int k = 1; foreach (var x in eng.Tracks) if (!x.IsBus && !x.Instrument) k++;
        var t = StudioEngine.NewTrack("Audio " + k, "audio", null); t.Armed = true; nextInsert = NtInsertAt(); AddTrack(t);
        selTrack = eng.Tracks.IndexOf(t); Toast("Empty audio track added (raw, no effects). Press MIC to record onto it, or drop an audio file on it.");
    }

    // an empty track meant for your beat / instrumental: it keeps the beat on its own row, apart from the vocals (drop the file on it or use ADD MUSIC)
    void AddBeatTrack()
    {
        int k = 1; foreach (var x in eng.Tracks) if (!x.IsBus && !x.Instrument && x.Role == "beat") k++;
        var t = StudioEngine.NewTrack(k == 1 ? "Beat" : "Beat " + k, "beat", null); nextInsert = NtInsertAt(); AddTrack(t);
        selTrack = eng.Tracks.IndexOf(t); Toast("Beat / Instrumental track added. Drop your beat on it, or use ADD MUSIC, so the beat stays on its own track.");
    }

    List<NtItem> NtItems()
    {
        Func<int, Color> C = v => Color.FromArgb(v >> 16 & 255, v >> 8 & 255, v & 255); var l = new List<NtItem>();
        l.Add(new NtItem { Title = "Voice / Audio", Sub = "Record or add audio. Raw, no effects", Col = C(0xF04438), Icon = 0, Do = () => { CloseNewTrackSheet(); AddEmptyAudioTrack(); } });
        l.Add(new NtItem { Title = "Beat / Instrumental", Sub = "Keep your beat on its own track", Col = C(0x8B5CF6), Icon = 5, Do = () => { CloseNewTrackSheet(); AddBeatTrack(); } });
        l.Add(new NtItem { Title = "Virtual Instruments", Sub = "Bass Lab and VST plugins", Tag = "MIDI", Col = C(0x16C784), Icon = 2, Do = () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); AddInstrumentClick(true); } });
        l.Add(new NtItem { Title = "MIDI Track", Sub = "Play your own keyboard or instrument", Tag = "MIDI", Col = C(0x3B8CF5), Icon = 3, Do = () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); recOn = true; AddInstrument("synth"); } });
        return l;
    }

    void NtIcon(Graphics g, RectangleF r, int kind)
    {
        using (var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = r.Width / 42f;
            switch (kind)
            {
                case 0: g.DrawPath(pen, RR(Rc(cx - 5 * s, cy - 13 * s, 10 * s, 17 * s), 5 * s)); g.DrawArc(pen, cx - 10 * s, cy - 8 * s, 20 * s, 18 * s, 10, 160); g.DrawLine(pen, cx, cy + 10 * s, cx, cy + 14 * s); break;   // microphone
                case 2: g.DrawPath(pen, RR(Rc(cx - 14 * s, cy - 8 * s, 28 * s, 18 * s), 3 * s)); for (int k = -1; k <= 1; k++) g.DrawLine(pen, cx + k * 7 * s, cy - 8 * s, cx + k * 7 * s, cy + 2 * s); break;   // keys
                case 5: { float[] hs = { 8, 17, 12, 21, 10 }; for (int b2 = 0; b2 < hs.Length; b2++) g.DrawLine(pen, cx - 12 * s + b2 * 6 * s, cy + hs[b2] * s / 2, cx - 12 * s + b2 * 6 * s, cy - hs[b2] * s / 2); break; }   // beat bars
                case 3: for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) g.DrawEllipse(pen, cx - 12 * s + a * 9 * s, cy - 12 * s + b * 9 * s, 5 * s, 5 * s); break;   // pads
                default: for (int k = -1; k <= 1; k++) g.DrawLine(pen, cx - 14 * s, cy + k * 8 * s, cx, cy); g.DrawLine(pen, cx, cy, cx + 14 * s, cy); g.DrawLine(pen, cx + 8 * s, cy - 5 * s, cx + 14 * s, cy); g.DrawLine(pen, cx + 8 * s, cy + 5 * s, cx + 14 * s, cy); break;   // lines merging into one
            }
        }
    }

    void PaintNewTrackSheet(Graphics g)
    {
        if (!ntOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => CloseNewTrackSheet());
        var items = NtItems(); const float rowH = 70; bool pick = eng.Tracks.Count > 0;
        float sw = Math.Min(560, W - 24), sh = Math.Min(H - 40, 74 + (pick ? 42 : 0) + items.Count * rowH + 96); var sheet = PopMove("newtrack", Rc((W - sw) / 2, H - sh - 12, sw, sh), 66, 70, false);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 24); Glow(g, NeonViolet, sheet, 24, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 24, 1.5f); Reg(sheet, () => { });
        using (var b = new SolidBrush(TM.C(110, 150, 140, 190))) g.FillPath(b, RR(Rc(sheet.X + sw / 2 - 26, sheet.Y + 9, 52, 5), 2.5f));
        Txt(g, "NEW TRACK", 24, Ink, Rc(sheet.X + 24, sheet.Y + 24, 240, 40), StringAlignment.Near, true);
        var close = Rc(sheet.Right - 54, sheet.Y + 26, 34, 34); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 17); Txt(g, "X", 16, Ink, close, StringAlignment.Center, true); Reg(close, () => CloseNewTrackSheet());
        float y = sheet.Y + 74;
        if (pick)
        {
            string[] w = { "Goes below the selected track", "Goes above the selected track", "Goes at the end of the list" }; var chip = Rc(sheet.X + 24, y, sheet.Width - 48, 32);
            FillRR(g, Hot(chip) ? TM.C(150, 60, 34, 110) : TM.C(100, 40, 24, 80), chip, 16); Txt(g, w[ntWhere] + "   (click to change)", 13, DimC, chip, StringAlignment.Center, false); Reg(chip, () => ntWhere = (ntWhere + 1) % 3);
            y += 42;
        }
        float bottomH = 84, top = y, areaH = sheet.Bottom - bottomH - top - 6; var area = Rc(sheet.X, top, sheet.Width, areaH);
        float total = items.Count * rowH; ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll)); float vsw = total > areaH ? 54 : 0;
        regs.Add(new HitReg { R = area, OnWheel = d => ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll - d * 60)) });
        g.SetClip(area);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; var row = Rc(sheet.X + 12, top + i * rowH - ntScroll, sheet.Width - 24 - vsw, rowH - 6); if (row.Bottom < top || row.Y > area.Bottom) continue; bool hot = Hot(row) && area.Contains(MousePos);
            if (hot) FillRR(g, TM.C(120, 70, 40, 130), row, 14);
            var ic = Rc(row.X + 10, row.Y + 7, 52, 52); using (var b = new SolidBrush(it.Col)) g.FillEllipse(b, ic); NtIcon(g, ic, it.Icon);
            Txt(g, it.Title, 18, Ink, Rc(row.X + 78, row.Y + 8, 260, 26), StringAlignment.Near, false);
            Txt(g, it.Sub, 14, DimC, Rc(row.X + 78, row.Y + 36, row.Width - 90, 22), StringAlignment.Near, false);
            if (it.Tag.Length > 0) { SizeF ts; using (var f = new Font("Segoe UI", 13.5f)) ts = g.MeasureString(it.Title, f); var tg = Rc(row.X + 78 + ts.Width + 4, row.Y + 11, 44, 20); FillRR(g, Color.FromArgb(235, 235, 235), tg, 5); Txt(g, it.Tag, 11, TM.C(255, 20, 14, 34), tg, StringAlignment.Center, true); }
            var act = it.Do; Reg(row, () => act());
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 54, top, 42, areaH), total, areaH, () => ntScroll, v => ntScroll = v);
        // the buttons along the bottom: Import, Sounds
        float bw = (sheet.Width - 24 - 12) / 2, by = sheet.Bottom - bottomH + 4;
        string[] bn = { "Import a file", "Sounds" }; Action[] ba = { () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); AddMusicClick(true); }, () => { CloseNewTrackSheet(); OpenSounds(); } };
        for (int i = 0; i < 2; i++)
        {
            var b = Rc(sheet.X + 12 + i * (bw + 12), by, bw, 62); var act = ba[i]; FillRR(g, Hot(b) ? TM.C(235, 52, 34, 100) : TM.C(235, 34, 24, 66), b, 14); StrokeRR(g, Color.FromArgb(120, NeonViolet), b, 14, 1);
            Txt(g, bn[i], 16, Ink, b, StringAlignment.Center, true); Reg(b, () => act());
        }
    }
}

// Purphex Music: the Buses sheet. Buses are not tracks on the timeline: every track has a small Bus drop-down, and the levels and plugins of the buses are here.
partial class StudioForm
{
    bool busOpen; float busScroll;
    int LaneCount { get { int c = 0; foreach (var t in eng.Tracks) if (!t.IsBus && t.Host == null) c++; return c; } }   // rows on the timeline; layers and buses are kept after them in the list
    int FirstBusIndex() { for (int i = 0; i < eng.Tracks.Count; i++) if (eng.Tracks[i].IsBus) return i; return eng.Tracks.Count; }
    void OpenBusSheet() { busOpen = true; busScroll = 0; ntOpen = false; }

    void PaintBusSheet(Graphics g)
    {
        if (!busOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => busOpen = false);
        var buses = new List<Track>(); foreach (var t in eng.Tracks) if (t.IsBus) buses.Add(t);
        const float rowH = 84; float sw = Math.Min(620, W - 24), sh = Math.Min(H - 40, 150 + Math.Max(1, buses.Count) * rowH); var sheet = PopMove("bus", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "BUSES", 22, Ink, Rc(sheet.X + 22, sheet.Y + 16, 200, 34), StringAlignment.Near, true);
        TxtWrap(g, "A bus mixes the tracks sent to it and puts shared plugins on them. Choose it from the Bus drop-down on a track.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 44, 34));
        var close = Rc(sheet.Right - 52, sheet.Y + 16, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => busOpen = false);
        float top = sheet.Y + 86, areaH = sheet.Bottom - 66 - top; var area = Rc(sheet.X, top, sheet.Width, areaH); float total = buses.Count * rowH; busScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), busScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => busScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), busScroll - d * 60)) });
        g.SetClip(area);
        if (buses.Count == 0) Txt(g, "NO BUSES YET. PRESS NEW BUS.", 14, DimC, Rc(sheet.X + 22, top + 10, sheet.Width - 44, 24), StringAlignment.Near, false);
        for (int i = 0; i < buses.Count; i++)
        {
            var bt = buses[i]; int bi = eng.Tracks.IndexOf(bt); var row = Rc(sheet.X + 14, top + i * rowH - busScroll, sheet.Width - 28 - (total > areaH ? 56 : 0), rowH - 8); if (row.Bottom < top || row.Y > area.Bottom) continue;
            FillRR(g, TM.C(150, 26, 15, 52), row, 12); StrokeRR(g, Color.FromArgb(90, NeonViolet), row, 12, 1);
            int cnt = 0; foreach (var x in eng.Tracks) if (x.Out == bt) cnt++;
            var nm = Rc(row.X + 14, row.Y + 8, 190, 24); Txt(g, bt.Name, 15, Ink, nm, StringAlignment.Near, true); Reg(nm, () => { }).OnDouble = () => RenameTrack(bt);
            Txt(g, cnt + (cnt == 1 ? " track" : " tracks"), 11, DimC, Rc(row.X + 14, row.Y + 32, 150, 18), StringAlignment.Near, false);
            Btn(g, Rc(row.X + 210, row.Y + 8, 34, 28), "M", bt.Mute ? 4 : 0, () => bt.Mute = !bt.Mute, 13);
            Btn(g, Rc(row.X + 376, row.Y + 8, 90, 28), "Presets", 1, () => { var pm = Win.NewMenu(); for (int pi = 0; pi < Presets.BusNames.Length; pi++) { string pn = Presets.BusNames[pi]; var pit = new ToolStripMenuItem(pn) { ToolTipText = Presets.BusBlurbs[pi] }; pit.Click += (a, b) => { PutPreset(bt, pn, Presets.BuildBus(pn)); Toast(pn + " applied to " + bt.Name + ": " + Presets.BusBlurbs[Array.IndexOf(Presets.BusNames, pn)]); }; pm.Items.Add(pit); } pm.Items.Add(new ToolStripSeparator()); pm.Items.Add("Clear all plugins").Click += (a, b) => { if (bt.Fx.Count == 0 || !Sure("remove every plugin from " + bt.Name)) return; ChainApply(bt, "All plugins removed", () => { lock (eng.Sync) { var gone = new List<Fx>(bt.Fx); bt.Fx.Clear(); bt.Auto.RemoveAll(al => gone.Contains(al.Fx)); } }); selFx = -1; };   /* undoable, asks first, and its automation goes too; the plugins are not disposed because Undo still holds them */ pm.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 12);
            Btn(g, Rc(row.X + 250, row.Y + 8, 62, 28), "+ ADD", 1, () => { busOpen = false; OpenBrowser(false, bi); }, 11);
            { var eb = Rc(row.X + 316, row.Y + 8, 56, 28); Btn(g, eb, "EDIT" + (bt.Fx.Count > 0 ? " (" + bt.Fx.Count + ")" : ""), 2, () => EditBus(bt), 10); Hint2(eb, "Edit the effects of " + bt.Name + ": open, change, switch off, move or remove them"); }
            Btn(g, Rc(row.Right - 44, row.Y + 8, 30, 28), "x", 3, () => { if (MessageBox.Show(this, "Delete \"" + bt.Name + "\"? Tracks sent to it go straight to the master.", "Delete bus", MessageBoxButtons.YesNo) == DialogResult.Yes) DeleteTrackQuiet(bt); }, 13);
            float frac = Clamp01((bt.VolDb + 48f) / 54f); Txt(g, "VOL", 11, DimC, Rc(row.X + 14, row.Y + 52, 28, 18), StringAlignment.Near, false);
            var fs = Slider(g, Rc(row.X + 44, row.Y + 52, 230, 18), frac, f => bt.VolDb = f < 0.003f ? -48f : (float)Math.Round(f * 54f - 48f, 1), Accent2); fs.OnDouble = () => bt.VolDb = 0;
            Txt(g, (bt.VolDb <= -47.9f ? "-inf" : bt.VolDb.ToString("+0.0;-0.0;0.0")) + " dB", 12, Ink, Rc(row.X + 282, row.Y + 52, 70, 18), StringAlignment.Near, false);
            HBar(g, Rc(row.Right - 150, row.Y + 54, 120, 6), bt.PeakL); HBar(g, Rc(row.Right - 150, row.Y + 63, 120, 6), bt.PeakR);
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => busScroll, v => busScroll = v);
        var nb = Rc(sheet.X + 14, sheet.Bottom - 54, sheet.Width - 28, 40); Btn(g, nb, "+ New bus", 2, () => AddBus(), 14);
    }
}
