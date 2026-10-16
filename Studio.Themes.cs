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
        using (var d = new OpenFileDialog { Title = "Import a theme or a photo", Filter = "Themes and photos|*.purphextheme;*.txt;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" })
        { if (d.ShowDialog(this) != DialogResult.OK) return; string err = ImportThemePath(d.FileName); if (err != null) Toast(err); }
    }
    // a theme file (with the picture named in it, if it sits beside the file) or any photo (it becomes a theme with the photo as the background); null = it worked
    internal string ImportThemePath(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant(); bool isTheme = ext == ".purphextheme" || ext == ".txt"; ThemeDef t; string err; int prevT = Themes.Index;
        if (isTheme)
        {
            string text; try { if (new FileInfo(file).Length > 16 * 1024) return "That file is too big to be a theme."; text = File.ReadAllText(file); } catch (Exception ex) { return "Could not open that file: " + ex.Message; }
            err = Themes.Import(text, Path.GetFileNameWithoutExtension(file), out t, Path.GetDirectoryName(file));
        }
        else err = Themes.ImportPhoto(file, out t);
        if (err != null) return err; Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed(); Toast("Imported and in use: " + t.Name + (t.Photo.Length > 0 ? " (its photo fills the whole window)." : "."));
        OfferRevert("Theme " + t.Name + " is on", () => { Themes.Apply(prevT); Prefs.Changed(); }); return null;
    }
    void ExportThemeFile()
    {
        var t = Themes.Current;
        using (var d = new SaveFileDialog { Title = "Save this theme", Filter = "Purphex theme|*.purphextheme", FileName = t.Name + ".purphextheme" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(d.FileName, Themes.ToFile(t)); if (t.Photo.Length > 0 && File.Exists(t.Photo)) File.Copy(t.Photo, Path.Combine(Path.GetDirectoryName(d.FileName), Path.GetFileName(t.Photo)), true); Toast("Saved " + Path.GetFileName(d.FileName) + ". Share it with anyone: they press IMPORT."); } catch (Exception ex) { Toast("Could not save: " + ex.Message); }
        }
    }
    int themeTab; string themeSelName = "";
    void DeleteTheme(ThemeDef t)
    {
        bool ok = themeConfirmHook != null ? themeConfirmHook(t.Name) : MessageBox.Show(this, "Delete the theme \"" + t.Name + "\" from this PC?", "Delete theme", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        if (ok && Themes.Delete(t)) { Prefs.Changed(); Toast("Theme deleted."); }
    }

    readonly Dictionary<string, Bitmap> themeThumbs = new Dictionary<string, Bitmap>();
    Bitmap ThemeThumb(string path)
    {   // a small picture of the theme's photo for its card (made once)
        Bitmap b; if (themeThumbs.TryGetValue(path, out b)) return b; b = null;
        try { using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false)) { b = new Bitmap(112, 64); double k = Math.Max(112.0 / im.Width, 64.0 / im.Height); float dw = (float)(im.Width * k), dh = (float)(im.Height * k); using (var gg = Graphics.FromImage(b)) { gg.InterpolationMode = InterpolationMode.HighQualityBicubic; gg.DrawImage(im, (112 - dw) / 2f, (64 - dh) / 2f, dw, dh); } } } catch { b = null; }
        if (themeThumbs.Count > 60) themeThumbs.Clear(); themeThumbs[path] = b; return b;
    }
    void PaintThemeSheet(Graphics g)
    {
        if (!themeOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        Reg(all, () => themeOpen = false);
        float sw = Math.Min(800, W - 24), sh = Math.Min(H - 30, 660); var sheet = PopMove("themes", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "THEMES", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 300, 34), StringAlignment.Near, true);
        TxtWrap(g, "Pick the colours of the whole program. Make your own, import one from a file, or download one that somebody else made.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 90, 34));
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => themeOpen = false);
        float y = sheet.Y + 90, x = sheet.X + 22;
        var tabA = Rc(x, y, 128, 34); var tabP = Rc(tabA.Right + 8, y, 168, 34); var tabB = Rc(tabP.Right + 8, y, 186, 34);
        Btn(g, tabA, "MY THEMES", themeTab == 0 ? 4 : 0, () => { themeTab = 0; themeScroll = 0; }, 13); Hint2(tabA, "The themes on this PC (built in and imported)");
        Btn(g, tabP, "PROFILE THEMES", themeTab == 1 ? 4 : 0, () => { themeTab = 1; themeScroll = 0; }, 13); Hint2(tabP, "The themes you saved from the colour mixer on your profile (SAVE AS THEME)");
        Btn(g, tabB, "COMMUNITY THEMES", 0, () => OpenCommunityThemes(), 13); Hint2(tabB, "Download themes other people made, or share yours");
        float rx = sheet.Right - 22; var exp = Rc(rx - 90, y, 90, 34);
        Btn(g, exp, "SAVE", 1, () => ExportThemeFile(), 12); Hint2(exp, "Save the theme in use as a file you can share");
        bool rbBar = themeTab == 0 && Themes.Current.Rainbow; float top = y + 50, bottom = sheet.Bottom - (themeTab == 1 || rbBar ? 78 : 20); float areaH = bottom - top; var area = Rc(sheet.X + 10, top, sheet.Width - 20, areaH);
        var list = Themes.All; var idx = new List<int>(); for (int q0 = 0; q0 < list.Count; q0++) if (Themes.IsProfile(list[q0]) == (themeTab == 1)) idx.Add(q0);
        int cols = sheet.Width >= 700 ? 3 : 2; float vsw = 56, gap = 12, cw = (sheet.Width - 44 - vsw - gap * (cols - 1)) / cols, ch = 96; int rows = (idx.Count + cols - 1) / cols; float total = rows * (ch + gap);
        themeScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), themeScroll)); regs.Add(new HitReg { R = area, OnWheel = d => themeScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), themeScroll - d * 60)) });
        g.SetClip(area);
        for (int k0 = 0; k0 < idx.Count; k0++)
        {
            int i = idx[k0]; var t = list[i]; int ti = i; var card = Rc(sheet.X + 22 + (k0 % cols) * (cw + gap), top + (k0 / cols) * (ch + gap) - themeScroll, cw, ch); if (card.Bottom < top - 2 || card.Y > bottom) continue;
            bool on = i == Themes.Index, hot = Hot(card) && area.Contains(MousePos);   /* the card itself is the region (an Intersect would change its size by a hair and never match) */
            using (var br = new LinearGradientBrush(card, Color.FromArgb(hot ? 235 : 215, t.Bg1), Color.FromArgb(hot ? 235 : 215, t.Bg0), 90f)) { using (var p = RR(card, 14)) g.FillPath(br, p); }
            // the theme under the mouse gets a bright outline (in its own colours) with a soft ring round it, so it is plain which one a click will pick
            using (var pen = new Pen(hot ? t.Cyan : (on ? t.Violet : Color.FromArgb(130, t.EdgeC)), hot ? 2.8f : (on ? 2.2f : 1.2f))) { using (var p = RR(card, 14)) g.DrawPath(pen, p); }
            if (hot) { using (var pen = new Pen(Color.FromArgb(110, t.Cyan), 1.4f)) { using (var p = RR(RectangleF.Inflate(card, 4, 4), 17)) g.DrawPath(pen, p); } }
            if (on || hot) Glow(g, hot ? t.Cyan : t.Violet, card, 14, hot ? 0.85f : 0.7f, 3);
            Color[] sw6 = { t.Violet, t.Cyan, t.Pink, t.Lime, t.Accent, t.Btn }; for (int k = 0; k < 6; k++) { var dot = Rc(card.X + 14 + k * 26, card.Y + 14, 20, 20); using (var br = new SolidBrush(sw6[k])) g.FillEllipse(br, dot); using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawEllipse(pen, dot); }
            bool ph = t.Photo.Length > 0; if (ph) { var th = ThemeThumb(t.Photo); if (th != null) { var tr = Rc(card.Right - 120, card.Y + 8, 112, card.Height - 16); var st0 = g.Save(); using (var pth = RR(tr, 8)) g.SetClip(pth, CombineMode.Intersect); g.DrawImage(th, tr); g.Restore(st0); StrokeRR(g, Color.FromArgb(150, t.EdgeC), tr, 8, 1); } }
            Txt(g, t.Name.ToUpper(), 15, Ink, Rc(card.X + 14, card.Y + 42, card.Width - 28 - (ph ? 112 : 0), 24), StringAlignment.Near, true);
            TxtWrap(g, t.Blurb, 11, TM.C(190, 200, 190, 230), Rc(card.X + 14, card.Y + 66, card.Width - 28 - (ph ? 112 : 0), 28));
            if (on) { var tag = Rc(card.Right - (ph ? 196 : 76), card.Y + 43, 64, 22); FillRR(g, Color.FromArgb(230, t.Violet), tag, 8); Txt(g, "IN USE", 10, Color.Black, tag, StringAlignment.Center, true); }
            { string tg = Themes.Tag(t); bool allowed = Themes.Allowed(t);   // the themes made for one edition wear its name; one above this edition has a padlock
              if (tg.Length > 0 && !ph && !on) { Color tc = t.Req == Tier.Fantasy ? TM.C(255, 255, 110, 230) : TM.C(255, 255, 200, 90); var pill = Rc(card.Right - 90, card.Y + 43, 76, 20); FillRR(g, Color.FromArgb(60, tc), pill, 8); StrokeRR(g, Color.FromArgb(200, tc), pill, 8, 1f); Txt(g, tg, 9, tc, pill, StringAlignment.Center, true); }
              if (!allowed) { using (var br = new SolidBrush(Color.FromArgb(120, 4, 2, 10))) { using (var p = RR(card, 14)) g.FillPath(br, p); } Edition.Padlock(g, Rc(card.Right - 34, card.Y + 8, 20, 26), t.Req == Tier.Fantasy ? Color.FromArgb(255, 255, 110, 230) : Color.FromArgb(255, 255, 200, 90)); } }
            Reg(card, () => { themeSelName = t.Name; if (!Themes.Allowed(t)) { Edition.Upsell(t.Req, "The " + t.Name + " theme", this); return; } int prev = Themes.Index; if (prev == ti) return; Themes.Apply(ti); Prefs.Changed(); if (mixFollow) MixSetFollow(false);   /* one theme at a time: the colour mix of the profile steps aside when a theme is chosen */ OfferRevert("Theme " + list[ti].Name + " is on", () => { Themes.Apply(prev); Prefs.Changed(); }); });
            if (t.Custom) { var del = Rc(card.Right - (on ? 100 : 36), card.Y + 10, 24, 24); Btn(g, del, "x", 3, () => DeleteTheme(t), 12); Hint2(del, "Delete this theme from this PC"); }
            if (t.Rainbow && !on) { /* the rainbow card is lit by its own colours */ }
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => themeScroll, v => themeScroll = v);
        if (rbBar)
        {   // RAINBOW SPEED: how fast it fades through the colours (slow is gentle on the eyes)
            var bar = Rc(sheet.X + 22, sheet.Bottom - 68, sheet.Width - 44 - 56, 52); FillRR(g, TM.C(255, 10, 5, 24), bar, 14); Win.DrawNeonBox(g, bar, false);
            Txt(g, "RAINBOW SPEED", 12, Ink, Rc(bar.X + 14, bar.Y, 150, bar.Height), StringAlignment.Near, true);
            var tr = Rc(bar.X + 170, bar.Y + bar.Height / 2 - 4, bar.Width - 170 - 150, 8); float f = Prefs.RainbowSpeed / 100f;
            FillRR(g, TM.C(255, 40, 28, 70), tr, 4); FillRR(g, NeonCyan, Rc(tr.X, tr.Y, Math.Max(8, tr.Width * f), tr.Height), 4);
            var kn = Rc(tr.X + tr.Width * f - 9, tr.Y - 7, 18, 22); FillRR(g, Color.White, kn, 6);
            Txt(g, Prefs.RainbowSpeed <= 10 ? "VERY SLOW" : Prefs.RainbowSpeed <= 35 ? "SLOW" : Prefs.RainbowSpeed <= 65 ? "MEDIUM" : "FAST", 11, NeonCyan, Rc(tr.Right + 14, bar.Y, 120, bar.Height), StringAlignment.Near, true);
            var hit = Rc(tr.X - 10, bar.Y, tr.Width + 20, bar.Height); Action<PointF> setAt = pt => { Prefs.RainbowSpeed = (int)Math.Round(Math.Max(0, Math.Min(1, (pt.X - tr.X) / tr.Width)) * 100); Prefs.Changed(); };
            var rg = Reg(hit, () => setAt(MousePos)); rg.OnDrag = setAt; Hint2(hit, "How fast the Rainbow fades through the colours: left is slow and gentle, right is fast");
        }
        if (themeTab == 1)
        {   // the profile themes: what they are, and a DELETE THEME button under them
            if (idx.Count == 0) TxtWrap(g, "No profile themes yet. On your Profile page, mix two colours and press SAVE AS THEME: it is added here, and nothing changes until you pick it.", 13, DimC, Rc(sheet.X + 30, top + 10, sheet.Width - 120, 60));
            ThemeDef target = null; if (Themes.IsProfile(Themes.Current)) target = Themes.Current; else foreach (var q1 in list) if (Themes.IsProfile(q1) && q1.Name == themeSelName) target = q1;
            var bar = Rc(sheet.X + 22, sheet.Bottom - 68, sheet.Width - 44 - 56, 52); FillRR(g, TM.C(255, 10, 5, 24), bar, 14); Win.DrawNeonBox(g, bar, false);
            var del = Rc(bar.X + 10, bar.Y + 9, 190, 34); var tgt = target; Btn(g, del, "DELETE THEME", tgt != null ? 3 : 0, () => { if (tgt != null) DeleteTheme(tgt); }, 12); Hint2(del, tgt != null ? "Delete the theme " + tgt.Name + " from this PC" : "Pick a profile theme first, then delete it here");
            Txt(g, idx.Count + (idx.Count == 1 ? " profile theme saved" : " profile themes saved") + (tgt != null ? "   -   picked: " + tgt.Name : ""), 12, DimC, Rc(del.Right + 14, bar.Y, bar.Width - del.Width - 34, bar.Height), StringAlignment.Near, false);
        }
    }
}
