using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The Presets sheet for a plugin: a long list with search and the logo scroll bar, USE on any preset, and REVERT at all times (the sheet remembers how the plugin was when you opened it).
partial class StudioForm
{
    bool psOpen; Fx psFx; float psScroll; string psQuery = "", psBefore = ""; bool psBeforeBypass; Func<string, bool> psConfirmHook;

    internal void OpenPresetSheet(Fx f)
    {
        psOpen = true; psFx = f; psScroll = 0; psQuery = ""; psBefore = f.Serialize(); psBeforeBypass = f.Bypass; stylesOpen = false; ntOpen = false; busOpen = false; browserOpen = false; cpOpen = false; themeOpen = false; amOpen = false;
    }
    void PsKey(char c) { if (c == '\b') { if (psQuery.Length > 0) psQuery = psQuery.Substring(0, psQuery.Length - 1); } else if (!char.IsControl(c) && psQuery.Length < 30) psQuery += c; psScroll = 0; }
    bool PsChanged { get { return psFx != null && (psFx.Serialize() != psBefore || psFx.Bypass != psBeforeBypass); } }
    void PsRestore() { var f = psFx; lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(psBefore); f.Bypass = psBeforeBypass; f.Reset(); } }
    // USE: applies the preset, and the REVERT bar (and the REVERT button on the sheet) can put the plugin back exactly as it was
    internal void PsUse(string name, string data)
    {
        var f = psFx; string before = f.Serialize(); bool bp = f.Bypass; Presets.Apply(f, data);
        OfferRevert(name + " on " + f.Title, () => { lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(before); f.Bypass = bp; f.Reset(); } });
        Toast(name + " is on. REVERT puts the plugin back as it was.");
    }

    void PaintPresetSheet(Graphics g)
    {
        if (!psOpen || psFx == null) return; var f = psFx; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(TM.C(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => psOpen = false);
        float sw = Math.Min(680, W - 24), sh = Math.Min(H - 30, 700); var sheet = PopMove("presets", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "PRESETS", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 220, 34), StringAlignment.Near, true); Txt(g, f.Title.ToUpper(), 13, NeonCyan, Rc(sheet.X + 190, sheet.Y + 20, sheet.Width - 300, 24), StringAlignment.Near, true);
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => psOpen = false);
        TxtWrap(g, "Press USE to try one. REVERT puts the plugin back exactly as it was when you opened this list. Start typing to search.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 50, sheet.Width - 44, 34));
        float y = sheet.Y + 88; var sb = Rc(sheet.X + 22, y, sheet.Width - 44, 34); FillRR(g, TM.C(235, 14, 7, 32), sb, 8); StrokeRR(g, Accent, sb, 8, 1.4f);
        if (psQuery.Length > 0) { var xb = Rc(sb.Right - 30, sb.Y + 5, 24, 24); Btn(g, xb, "x", 3, () => { psQuery = ""; psScroll = 0; }, 12); Hint2(xb, "Clear the search"); }
        double tm = Environment.TickCount / 1000.0; bool blink = ((int)(tm * 2) % 2) == 0; Txt(g, psQuery.Length == 0 ? (blink ? "|" : " ") + "  SEARCH THE PRESETS" : psQuery + (blink ? "|" : ""), 13, psQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 12, sb.Y, sb.Width - 50, sb.Height), StringAlignment.Near, false);
        y += 42; float bw = (sheet.Width - 44 - 16) / 3f;
        var c1 = Rc(sheet.X + 22, y, bw, 32); var c2 = Rc(c1.Right + 8, y, bw, 32); var c3 = Rc(c2.Right + 8, y, bw, 32);
        Btn(g, c1, "COMMUNITY PRESETS", 1, () => { psOpen = false; OpenCommunityPresets("fx:" + f.Type, f.Title, cp => { lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(cp.Data); f.Reset(); } }, () => f.Serialize(), f.Title + " custom"); }, 11); Hint2(c1, "Download presets other producers shared (and share yours)");
        Btn(g, c2, "SAVE MY SETTINGS", 1, () => { string nm = Ask(Prefs.ShareSaved ? "Preset name (it is also shared with the community):" : "Preset name:", f.Title + " custom"); if (nm != null) { Presets.SaveUser(f.Type, nm, f); Toast("Preset saved"); CommunityPresets.AutoShare("fx:" + f.Type, nm, f.Serialize(), ProjectLibrary.ProfileName, msg => { if (msg != null) BeginInvoke(new Action(() => Toast(msg))); }); } }, 11); Hint2(c2, "Save the settings you have now as a preset of your own");
        Btn(g, c3, "REVERT", PsChanged ? 3 : 0, () => { if (PsChanged) { PsRestore(); Toast("Back to how it was when you opened the list."); } else Toast("Nothing has changed yet."); }, 12); Hint2(c3, "Put the plugin back exactly as it was when you opened this list");
        float top = y + 42, areaBottom = sheet.Bottom - 56, areaH = areaBottom - top; var area = Rc(sheet.X + 10, top, sheet.Width - 20, areaH);
        var rows = new List<KeyValuePair<string, string>>(); var mine = new HashSet<string>(); string q = psQuery.Trim();
        foreach (var kv in Presets.User(f.Type)) { if (q.Length == 0 || kv.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { rows.Add(kv); mine.Add(kv.Key); } }
        foreach (var kv in Presets.For(f.Type)) if (q.Length == 0 || kv.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) rows.Add(kv);
        float rowH = 50, gap = 6, total = rows.Count * (rowH + gap), vsw = 56; psScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), psScroll)); regs.Add(new HitReg { R = area, OnWheel = d => psScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), psScroll - d * 60)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        if (rows.Count == 0) Txt(g, "NO PRESET MATCHES THAT.", 13, DimC, Rc(area.X + 14, area.Y + 10, area.Width - 28, 24), StringAlignment.Near, false);
        for (int i = 0; i < rows.Count; i++)
        {
            var kv = rows[i]; var row = Rc(sheet.X + 22, top + i * (rowH + gap) - psScroll, sheet.Width - 44 - vsw, rowH); if (row.Bottom < top - 2 || row.Y > areaBottom) continue; bool isMine = mine.Contains(kv.Key); bool hot = Hot(RectangleF.Intersect(row, area)) && area.Contains(MousePos);
            FillRR(g, hot ? TM.C(200, 60, 34, 112) : TM.C(150, 26, 15, 52), row, 10); StrokeRR(g, Color.FromArgb(hot ? 160 : 80, NeonViolet), row, 10, 1);
            Txt(g, kv.Key, 14, Ink, Rc(row.X + 14, row.Y + 4, row.Width - 230, 24), StringAlignment.Near, true); Txt(g, isMine ? "MY PRESET" : "BUILT-IN", 10, isMine ? NeonLime : DimC, Rc(row.X + 14, row.Y + 28, 160, 16), StringAlignment.Near, true);
            string data = kv.Value, nm = kv.Key; var us = Rc(row.Right - 96, row.Y + 8, 84, 34); var ua = RectangleF.Intersect(us, area); if (!ua.IsEmpty) { Btn(g, us, "USE", 2, () => PsUse(nm, data), 13); Hint2(us, "Try this preset (REVERT puts it back)"); }
            if (isMine) { var dl = Rc(us.X - 34, row.Y + 11, 28, 28); var da = RectangleF.Intersect(dl, area); if (!da.IsEmpty) { Btn(g, dl, "x", 3, () => { bool ok = psConfirmHook != null ? psConfirmHook(nm) : MessageBox.Show(this, "Delete your preset \"" + nm + "\"?", "Delete preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes; if (ok) { Presets.DeleteUser(f.Type, nm); Toast("Preset deleted."); } }, 12); Hint2(dl, "Delete this preset of yours"); } }
        }
        g.Restore(gs); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => psScroll, v => psScroll = v);
        var done = Rc(sheet.Right - 22 - 120, sheet.Bottom - 48, 120, 34); Btn(g, done, "DONE", 2, () => psOpen = false, 13); Hint2(done, "Close the list and keep what you have now");
        Txt(g, rows.Count + (rows.Count == 1 ? " PRESET" : " PRESETS"), 11, DimC, Rc(sheet.X + 22, sheet.Bottom - 44, 200, 26), StringAlignment.Near, false);
    }
}
