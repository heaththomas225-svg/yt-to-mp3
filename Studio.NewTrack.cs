using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Purphex Music: the "New Track" sheet. + ADD TRACK / + TRACK open it: pick what to record or add (voice with AutoPitch, rap, podcast, guitar, bass,
// radio voice, instruments, drums, loops, sounds, or a file from the PC) and it goes straight onto a new track with the right effects.
partial class StudioForm
{
    bool ntOpen; int ntWhere; float ntScroll; string[] micChain; string micName, micRole;   // ntWhere: 0 = below the selected track, 1 = above it, 2 = at the end
    class NtItem { public string Title, Sub, Tag = ""; public Color Col; public int Icon; public Action Do; }

    void OpenNewTrackSheet(int where) { ntOpen = true; ntWhere = where; ntScroll = 0; }
    void CloseNewTrackSheet() { ntOpen = false; }
    int NtInsertAt()
    {
        int n = eng.Tracks.Count; if (n == 0) return 0; int sel = Math.Max(0, Math.Min(selTrack, n - 1));
        return ntWhere == 0 ? sel + 1 : (ntWhere == 1 ? sel : n);
    }

    // find an effect by its name (built-in or from the plugin library); null if it is not there
    Fx NtFx(string name)
    {
        foreach (var i in FxRegistry.All) if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) { try { return FxRegistry.Create(i.Type); } catch { return null; } }
        return null;
    }
    // record from the microphone / input onto a brand new track that already has the chosen effects on it
    void NtRecord(string name, string role, params string[] chain)
    {
        if (mic != null) { Toast("Already recording. Press MIC (or STOP) to finish that take first."); return; }
        nextInsert = NtInsertAt(); micChain = chain; micName = name; micRole = role; CloseNewTrackSheet(); StartMic();
        if (mic == null) { micChain = null; micName = null; micRole = null; nextInsert = -1; }
    }
    void NtBrowse(string group, string kind)
    {
        CloseNewTrackSheet(); OpenSounds(); sndFilter.Group = group; sndFilter.Kind = kind; sndFilter.Type = ""; sndFilter.Text = ""; if (soundBox != null) soundBox.Text = ""; sndDirty = true; sndScroll = 0;
    }

    List<NtItem> NtItems()
    {
        Func<int, Color> C = v => Color.FromArgb(v >> 16 & 255, v >> 8 & 255, v & 255); var l = new List<NtItem>();
        l.Add(new NtItem { Title = "Voice / Audio", Sub = "Record with AutoPitch + Fx", Col = C(0xF04438), Icon = 0, Do = () => NtRecord("Voice", "vocal", "Pitch Correction", "Vocal De-Esser") });
        l.Add(new NtItem { Title = "Rap Vocal", Sub = "Hard tune, presence and echo", Col = C(0xFF5CC8), Icon = 0, Do = () => NtRecord("Rap vocal", "vocal", "Hard Tune", "Vocal Presence", "Vocal Echo") });
        l.Add(new NtItem { Title = "Podcast / Speech", Sub = "A clean, even voice", Col = C(0xB06BFF), Icon = 0, Do = () => NtRecord("Speech", "vocal", "Vocal De-Noise", "Vocal De-Esser", "Vocal Presence") });
        l.Add(new NtItem { Title = "Radio / Telephone Voice", Sub = "Record with a lo-fi voice effect", Col = C(0x6C7BFF), Icon = 4, Do = () => NtRecord("Radio voice", "vocal", "Radio Voice") });
        l.Add(new NtItem { Title = "Guitar", Sub = "Jam with Amps + Fx", Col = C(0x1FC8D8), Icon = 1, Do = () => NtRecord("Guitar", "audio", "Amp Overdrive") });
        l.Add(new NtItem { Title = "Bass", Sub = "Find your signature tone", Col = C(0x3B8CF5), Icon = 1, Do = () => NtRecord("Bass", "audio", "Bass Amp Simulator", "Bass Enhancer") });
        l.Add(new NtItem { Title = "Virtual Instruments", Sub = "Bass Lab, Vital and other VSTs", Tag = "MIDI", Col = C(0x16C784), Icon = 2, Do = () => { nextInsert = NtInsertAt(); CloseNewTrackSheet(); AddInstrumentClick(true); } });
        l.Add(new NtItem { Title = "Drum Beats", Sub = "Pick drum loops and hits", Col = C(0xFF7A1A), Icon = 3, Do = () => NtBrowse("Drums", "") });
        l.Add(new NtItem { Title = "Loops", Sub = "Easily make complete tracks", Col = C(0xFFB020), Icon = 5, Do = () => NtBrowse("", "loop") });
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
                case 1: g.DrawLine(pen, cx + 12 * s, cy - 12 * s, cx - 2 * s, cy + 2 * s); g.DrawEllipse(pen, cx - 12 * s, cy - 2 * s, 12 * s, 12 * s); g.DrawLine(pen, cx + 8 * s, cy - 14 * s, cx + 14 * s, cy - 8 * s); break;   // neck + body
                case 2: g.DrawPath(pen, RR(Rc(cx - 14 * s, cy - 8 * s, 28 * s, 18 * s), 3 * s)); for (int k = -1; k <= 1; k++) g.DrawLine(pen, cx + k * 7 * s, cy - 8 * s, cx + k * 7 * s, cy + 2 * s); break;   // keys
                case 3: for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) g.DrawEllipse(pen, cx - 12 * s + a * 9 * s, cy - 12 * s + b * 9 * s, 5 * s, 5 * s); break;   // pads
                case 4: g.DrawPath(pen, RR(Rc(cx - 13 * s, cy - 6 * s, 26 * s, 16 * s), 4 * s)); g.DrawLine(pen, cx - 4 * s, cy - 6 * s, cx + 8 * s, cy - 14 * s); g.DrawEllipse(pen, cx - 8 * s, cy - 1 * s, 6 * s, 6 * s); break;   // radio
                default: for (int k = -3; k <= 3; k++) { float h = (k % 2 == 0 ? 14 : 8) * s - Math.Abs(k) * s; g.DrawLine(pen, cx + k * 4.5f * s, cy - h, cx + k * 4.5f * s, cy + h); } break;   // waveform
            }
        }
    }

    void PaintNewTrackSheet(Graphics g)
    {
        if (!ntOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(150, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => CloseNewTrackSheet());
        float sw = Math.Min(560, W - 24), sh = Math.Min(H - 40, 760); var sheet = Rc((W - sw) / 2, H - sh - 12, sw, sh);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 24); Glow(g, NeonViolet, sheet, 24, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 24, 1.5f); Reg(sheet, () => { });
        using (var b = new SolidBrush(Color.FromArgb(110, 150, 140, 190))) g.FillPath(b, RR(Rc(sheet.X + sw / 2 - 26, sheet.Y + 9, 52, 5), 2.5f));
        Txt(g, "New Track", 24, Ink, Rc(sheet.X + 24, sheet.Y + 24, 240, 40), StringAlignment.Near, true);
        var close = Rc(sheet.Right - 54, sheet.Y + 26, 34, 34); FillRR(g, Hot(close) ? Color.FromArgb(160, 70, 38, 130) : Color.FromArgb(110, 40, 24, 80), close, 17); Txt(g, "x", 16, Ink, close, StringAlignment.Center, true); Reg(close, () => CloseNewTrackSheet());
        float y = sheet.Y + 74;
        if (eng.Tracks.Count > 0)
        {
            string[] w = { "Goes below the selected track", "Goes above the selected track", "Goes at the end of the list" }; var chip = Rc(sheet.X + 24, y, sheet.Width - 48, 32);
            FillRR(g, Hot(chip) ? Color.FromArgb(150, 60, 34, 110) : Color.FromArgb(100, 40, 24, 80), chip, 16); Txt(g, w[ntWhere] + "   (click to change)", 13, DimC, chip, StringAlignment.Center, false); Reg(chip, () => ntWhere = (ntWhere + 1) % 3);
            y += 42;
        }
        float bottomH = 84, top = y, areaH = sheet.Bottom - bottomH - top - 6; var area = Rc(sheet.X, top, sheet.Width, areaH);
        var items = NtItems(); const float rowH = 66; float total = items.Count * rowH; ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => ntScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), ntScroll - d * 60)) });
        g.SetClip(area);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; var row = Rc(sheet.X + 12, top + i * rowH - ntScroll, sheet.Width - 24, rowH - 4); if (row.Bottom < top || row.Y > area.Bottom) continue; bool hot = Hot(row) && area.Contains(MousePos);
            if (hot) FillRR(g, Color.FromArgb(120, 70, 40, 130), row, 14);
            var ic = Rc(row.X + 10, row.Y + 5, 52, 52); using (var b = new SolidBrush(it.Col)) g.FillEllipse(b, ic); NtIcon(g, ic, it.Icon);
            Txt(g, it.Title, 18, Ink, Rc(row.X + 76, row.Y + 7, row.Width - 160, 26), StringAlignment.Near, false);
            Txt(g, it.Sub, 14, DimC, Rc(row.X + 76, row.Y + 33, row.Width - 90, 22), StringAlignment.Near, false);
            if (it.Tag.Length > 0) { using (var f = new Font("Segoe UI", 9f, FontStyle.Bold)) { var ts = g.MeasureString(it.Title, new Font("Segoe UI", 13.5f)); var tg = Rc(row.X + 76 + ts.Width + 6, row.Y + 10, 44, 20); FillRR(g, Color.FromArgb(235, 235, 235), tg, 5); Txt(g, it.Tag, 11, Color.FromArgb(20, 14, 34), tg, StringAlignment.Center, true); } }
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
