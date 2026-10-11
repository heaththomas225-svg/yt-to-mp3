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
        Toast("Empty audio track added (raw, no effects). Press MIC to record onto it, or drop an audio file on it.");
    }

    List<NtItem> NtItems()
    {
        Func<int, Color> C = v => Color.FromArgb(v >> 16 & 255, v >> 8 & 255, v & 255); var l = new List<NtItem>();
        l.Add(new NtItem { Title = "Voice / Audio", Sub = "Record or add audio. Raw, no effects", Col = C(0xF04438), Icon = 0, Do = () => { CloseNewTrackSheet(); AddEmptyAudioTrack(); } });
        l.Add(new NtItem { Title = "Virtual Instruments", Sub = "Bass Lab, Vital and other VSTs", Tag = "MIDI", Col = C(0x16C784), Icon = 2, Do = () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); AddInstrumentClick(true); } });
        l.Add(new NtItem { Title = "MIDI Track", Sub = "Play your own keyboard or instrument", Tag = "MIDI", Col = C(0x3B8CF5), Icon = 3, Do = () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); recOn = true; AddInstrument("synth"); } });
        l.Add(new NtItem { Title = "Bus", Sub = "Group tracks and give them shared plugins", Col = C(0xFFB020), Icon = 6, Do = () => { CloseNewTrackSheet(); AddBus(); } });
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
                case 3: for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) g.DrawEllipse(pen, cx - 12 * s + a * 9 * s, cy - 12 * s + b * 9 * s, 5 * s, 5 * s); break;   // pads
                default: for (int k = -1; k <= 1; k++) g.DrawLine(pen, cx - 14 * s, cy + k * 8 * s, cx, cy); g.DrawLine(pen, cx, cy, cx + 14 * s, cy); g.DrawLine(pen, cx + 8 * s, cy - 5 * s, cx + 14 * s, cy); g.DrawLine(pen, cx + 8 * s, cy + 5 * s, cx + 14 * s, cy); break;   // lines merging into one
            }
        }
    }

    void PaintNewTrackSheet(Graphics g)
    {
        if (!ntOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseNewTrackSheet());
        var items = NtItems(); const float rowH = 70; bool pick = eng.Tracks.Count > 0;
        float sw = Math.Min(560, W - 24), sh = Math.Min(H - 40, 74 + (pick ? 42 : 0) + items.Count * rowH + 96); var sheet = Rc((W - sw) / 2, H - sh - 12, sw, sh);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 24); Glow(g, NeonViolet, sheet, 24, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 24, 1.5f); Reg(sheet, () => { });
        using (var b = new SolidBrush(Color.FromArgb(110, 150, 140, 190))) g.FillPath(b, RR(Rc(sheet.X + sw / 2 - 26, sheet.Y + 9, 52, 5), 2.5f));
        Txt(g, "New Track", 24, Ink, Rc(sheet.X + 24, sheet.Y + 24, 240, 40), StringAlignment.Near, true);
        var close = Rc(sheet.Right - 54, sheet.Y + 26, 34, 34); FillRR(g, Hot(close) ? Color.FromArgb(160, 70, 38, 130) : Color.FromArgb(110, 40, 24, 80), close, 17); Txt(g, "x", 16, Ink, close, StringAlignment.Center, true); Reg(close, () => CloseNewTrackSheet());
        float y = sheet.Y + 74;
        if (pick)
        {
            string[] w = { "Goes below the selected track", "Goes above the selected track", "Goes at the end of the list" }; var chip = Rc(sheet.X + 24, y, sheet.Width - 48, 32);
            FillRR(g, Hot(chip) ? Color.FromArgb(150, 60, 34, 110) : Color.FromArgb(100, 40, 24, 80), chip, 16); Txt(g, w[ntWhere] + "   (click to change)", 13, DimC, chip, StringAlignment.Center, false); Reg(chip, () => ntWhere = (ntWhere + 1) % 3);
            y += 42;
        }
        float bottomH = 84, top = y, areaH = sheet.Bottom - bottomH - top - 6; var area = Rc(sheet.X, top, sheet.Width, areaH);
        float total = items.Count * rowH; ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll - d * 60)) });
        g.SetClip(area);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; var row = Rc(sheet.X + 12, top + i * rowH - ntScroll, sheet.Width - 24, rowH - 6); if (row.Bottom < top || row.Y > area.Bottom) continue; bool hot = Hot(row) && area.Contains(MousePos);
            if (hot) FillRR(g, Color.FromArgb(120, 70, 40, 130), row, 14);
            var ic = Rc(row.X + 10, row.Y + 7, 52, 52); using (var b = new SolidBrush(it.Col)) g.FillEllipse(b, ic); NtIcon(g, ic, it.Icon);
            Txt(g, it.Title, 18, Ink, Rc(row.X + 78, row.Y + 8, 260, 26), StringAlignment.Near, false);
            Txt(g, it.Sub, 14, DimC, Rc(row.X + 78, row.Y + 36, row.Width - 90, 22), StringAlignment.Near, false);
            if (it.Tag.Length > 0) { SizeF ts; using (var f = new Font("Segoe UI", 13.5f)) ts = g.MeasureString(it.Title, f); var tg = Rc(row.X + 78 + ts.Width + 4, row.Y + 11, 44, 20); FillRR(g, Color.FromArgb(235, 235, 235), tg, 5); Txt(g, it.Tag, 11, Color.FromArgb(20, 14, 34), tg, StringAlignment.Center, true); }
            var act = it.Do; Reg(row, () => act());
        }
        g.ResetClip();
        // the buttons along the bottom: Import, Sounds
        float bw = (sheet.Width - 24 - 12) / 2, by = sheet.Bottom - bottomH + 4;
        string[] bn = { "Import a file", "Sounds" }; Action[] ba = { () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); AddMusicClick(true); }, () => { CloseNewTrackSheet(); OpenSounds(); } };
        for (int i = 0; i < 2; i++)
        {
            var b = Rc(sheet.X + 12 + i * (bw + 12), by, bw, 62); var act = ba[i]; FillRR(g, Hot(b) ? Color.FromArgb(235, 52, 34, 100) : Color.FromArgb(235, 34, 24, 66), b, 14); StrokeRR(g, Color.FromArgb(120, NeonViolet), b, 14, 1);
            Txt(g, bn[i], 16, Ink, b, StringAlignment.Center, true); Reg(b, () => act());
        }
    }
}
