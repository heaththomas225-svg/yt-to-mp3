using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// The Theme sheet: pick a colour theme, import one from a file, export yours, or download one from Community themes.
partial class StudioForm
{
    bool themeOpen; float themeScroll;
    Func<string, bool> themeConfirmHook;   // a test answers for itself

    internal void OpenThemeSheet(bool community = false)
    {
        if (community) { OpenCommunityThemes(); return; }
        themeOpen = true; themeScroll = 0; stylesOpen = false; ntOpen = false; busOpen = false; browserOpen = false; cpOpen = false; menuOpen = false;
    }
    void OpenCommunityThemes()
    {
        themeOpen = false;
        OpenCommunityPresets("theme", "Community themes", cp =>
        {
            string text = "name=" + cp.Name + "\nauthor=" + cp.Author + "\n" + cp.Data; ThemeDef t; string err = Themes.Import(text, cp.Name, out t);
            if (err != null) { Toast(err); return; } Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed(); Toast("Theme added and in use: " + t.Name + ". It is in your Themes list now.");
        }, () => Themes.Current.ToText(), Themes.Current.Name + " custom");
    }
    void ImportThemeFile()
    {
        using (var d = new OpenFileDialog { Title = "Import a theme", Filter = "Purphex themes|*.purphextheme;*.txt|All files|*.*" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return; string text;
            try { var fi = new FileInfo(d.FileName); if (fi.Length > 16 * 1024) { Toast("That file is too big to be a theme."); return; } text = File.ReadAllText(d.FileName); }
            catch (Exception ex) { Toast("Could not open that file: " + ex.Message); return; }
            ThemeDef t; string err = Themes.Import(text, Path.GetFileNameWithoutExtension(d.FileName), out t); if (err != null) { Toast(err); return; }
            int prevT = Themes.Index; Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed(); Toast("Imported and in use: " + t.Name + "."); OfferRevert("Theme " + t.Name + " is on", () => { Themes.Apply(prevT); Prefs.Changed(); });
        }
    }
    void ExportThemeFile()
    {
        var t = Themes.Current;
        using (var d = new SaveFileDialog { Title = "Save this theme", Filter = "Purphex theme|*.purphextheme", FileName = t.Name + ".purphextheme" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(d.FileName, Themes.ToFile(t)); Toast("Saved " + Path.GetFileName(d.FileName) + ". Share it with anyone: they press IMPORT."); } catch (Exception ex) { Toast("Could not save: " + ex.Message); }
        }
    }
    void DeleteTheme(ThemeDef t)
    {
        bool ok = themeConfirmHook != null ? themeConfirmHook(t.Name) : MessageBox.Show(this, "Delete the theme \"" + t.Name + "\" from this PC?", "Delete theme", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        if (ok && Themes.Delete(t)) { Prefs.Changed(); Toast("Theme deleted."); }
    }

    void PaintThemeSheet(Graphics g)
    {
        if (!themeOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => themeOpen = false);
        float sw = Math.Min(800, W - 24), sh = Math.Min(H - 30, 660); var sheet = PopMove("themes", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "THEMES", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 300, 34), StringAlignment.Near, true);
        TxtWrap(g, "Pick the colours of the whole program. Make your own, import one from a file, or download one that somebody else made.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 90, 34));
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? Color.FromArgb(160, 70, 38, 130) : Color.FromArgb(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => themeOpen = false);
        float y = sheet.Y + 90, x = sheet.X + 22;
        var tabA = Rc(x, y, 150, 34); var tabB = Rc(tabA.Right + 8, y, 210, 34);
        Btn(g, tabA, "MY THEMES", 4, () => { }, 13); Hint2(tabA, "The themes on this PC (built in and imported)");
        Btn(g, tabB, "COMMUNITY THEMES", 0, () => OpenCommunityThemes(), 13); Hint2(tabB, "Download themes other people made, or share yours");
        float rx = sheet.Right - 22; var exp = Rc(rx - 170, y, 170, 34); var imp = Rc(exp.X - 8 - 170, y, 170, 34);
        Btn(g, imp, "IMPORT A FILE", 1, () => ImportThemeFile(), 12); Hint2(imp, "Import a theme file (.purphextheme) you were given or made");
        Btn(g, exp, "SAVE THIS THEME", 1, () => ExportThemeFile(), 12); Hint2(exp, "Save the theme in use as a file you can share");
        float top = y + 50, bottom = sheet.Bottom - 20; float areaH = bottom - top; var area = Rc(sheet.X + 10, top, sheet.Width - 20, areaH);
        var list = Themes.All; int cols = sheet.Width >= 700 ? 3 : 2; float vsw = 56, gap = 12, cw = (sheet.Width - 44 - vsw - gap * (cols - 1)) / cols, ch = 96; int rows = (list.Count + cols - 1) / cols; float total = rows * (ch + gap);
        themeScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), themeScroll)); regs.Add(new HitReg { R = area, OnWheel = d => themeScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), themeScroll - d * 60)) });
        g.SetClip(area);
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i]; int ti = i; var card = Rc(sheet.X + 22 + (i % cols) * (cw + gap), top + (i / cols) * (ch + gap) - themeScroll, cw, ch); if (card.Bottom < top - 2 || card.Y > bottom) continue;
            bool on = i == Themes.Index, hot = Hot(RectangleF.Intersect(card, area)) && area.Contains(MousePos);
            using (var br = new LinearGradientBrush(card, Color.FromArgb(hot ? 235 : 215, t.Bg1), Color.FromArgb(hot ? 235 : 215, t.Bg0), 90f)) { using (var p = RR(card, 14)) g.FillPath(br, p); }
            using (var pen = new Pen(on ? t.Violet : Color.FromArgb(hot ? 220 : 130, t.EdgeC), on ? 2.2f : 1.2f)) { using (var p = RR(card, 14)) g.DrawPath(pen, p); }
            if (on || hot) Glow(g, t.Violet, card, 14, on ? 0.7f : 0.4f, 3);
            Color[] sw6 = { t.Violet, t.Cyan, t.Pink, t.Lime, t.Accent, t.Btn }; for (int k = 0; k < 6; k++) { var dot = Rc(card.X + 14 + k * 26, card.Y + 14, 20, 20); using (var br = new SolidBrush(sw6[k])) g.FillEllipse(br, dot); using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawEllipse(pen, dot); }
            Txt(g, t.Name.ToUpper(), 15, Ink, Rc(card.X + 14, card.Y + 42, card.Width - 28, 24), StringAlignment.Near, true);
            TxtWrap(g, t.Blurb, 11, Color.FromArgb(190, 200, 190, 230), Rc(card.X + 14, card.Y + 66, card.Width - 28, 28));
            if (on) { var tag = Rc(card.Right - 76, card.Y + 43, 64, 22); FillRR(g, Color.FromArgb(230, t.Violet), tag, 8); Txt(g, "IN USE", 10, Color.Black, tag, StringAlignment.Center, true); }
            Reg(card, () => { int prev = Themes.Index; if (prev == ti) return; Themes.Apply(ti); Prefs.Changed(); OfferRevert("Theme " + list[ti].Name + " is on", () => { Themes.Apply(prev); Prefs.Changed(); }); });
            if (t.Custom) { var del = Rc(card.Right - (on ? 100 : 36), card.Y + 10, 24, 24); Btn(g, del, "x", 3, () => DeleteTheme(t), 12); Hint2(del, "Delete this theme from this PC"); }
            if (t.Rainbow && !on) { /* the rainbow card is lit by its own colours */ }
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => themeScroll, v => themeScroll = v);
        if (list.Count > 0 && total <= areaH) { }
    }
}
