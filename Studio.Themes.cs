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
    // a photo for the theme in use: any picture, any size; it fills the whole window (cropped in the middle, never stretched)
    void SetThemePhoto()
    {
        using (var d = new OpenFileDialog { Title = "Choose a photo for this theme", Filter = "Pictures|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" })
        { if (d.ShowDialog(this) != DialogResult.OK) return; string err = SetThemePhotoPath(d.FileName); if (err != null) Toast(err); }
    }
    internal string SetThemePhotoPath(string file) { var t = Themes.Current; string err = Themes.SetPhoto(t, file); if (err == null) Toast("Photo added to " + t.Name + ": it now fills the whole window."); return err; }
    void ClearThemePhoto() { var t = Themes.Current; Themes.ClearPhoto(t); Toast("Photo removed from " + t.Name + "."); }
    void ExportThemeFile()
    {
        var t = Themes.Current;
        using (var d = new SaveFileDialog { Title = "Save this theme", Filter = "Purphex theme|*.purphextheme", FileName = t.Name + ".purphextheme" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(d.FileName, Themes.ToFile(t)); if (t.Photo.Length > 0 && File.Exists(t.Photo)) File.Copy(t.Photo, Path.Combine(Path.GetDirectoryName(d.FileName), Path.GetFileName(t.Photo)), true); Toast("Saved " + Path.GetFileName(d.FileName) + ". Share it with anyone: they press IMPORT."); } catch (Exception ex) { Toast("Could not save: " + ex.Message); }
        }
    }
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
        using (var b = new SolidBrush(TM.C(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => themeOpen = false);
        float sw = Math.Min(800, W - 24), sh = Math.Min(H - 30, 660); var sheet = PopMove("themes", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "THEMES", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 300, 34), StringAlignment.Near, true);
        TxtWrap(g, "Pick the colours of the whole program. Make your own, import one from a file, or download one that somebody else made.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 90, 34));
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => themeOpen = false);
        float y = sheet.Y + 90, x = sheet.X + 22;
        var tabA = Rc(x, y, 128, 34); var tabB = Rc(tabA.Right + 8, y, 186, 34);
        Btn(g, tabA, "MY THEMES", 4, () => { }, 13); Hint2(tabA, "The themes on this PC (built in and imported)");
        Btn(g, tabB, "COMMUNITY THEMES", 0, () => OpenCommunityThemes(), 13); Hint2(tabB, "Download themes other people made, or share yours");
        float rx = sheet.Right - 22; var exp = Rc(rx - 90, y, 90, 34); var pho = Rc(exp.X - 8 - 118, y, 118, 34); bool hasPhoto = Themes.Current.Photo.Length > 0;
        Btn(g, pho, hasPhoto ? "NO PHOTO" : "PHOTO", hasPhoto ? 3 : 1, () => { if (hasPhoto) ClearThemePhoto(); else SetThemePhoto(); }, 12); Hint2(pho, hasPhoto ? "Take the photo off this theme" : "Put a photo behind the whole studio for the theme in use (any picture, any size)");
        Btn(g, exp, "SAVE", 1, () => ExportThemeFile(), 12); Hint2(exp, "Save the theme in use as a file you can share (its photo is saved beside it)");
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
            bool ph = t.Photo.Length > 0; if (ph) { var th = ThemeThumb(t.Photo); if (th != null) { var tr = Rc(card.Right - 120, card.Y + 8, 112, card.Height - 16); var st0 = g.Save(); using (var pth = RR(tr, 8)) g.SetClip(pth, CombineMode.Intersect); g.DrawImage(th, tr); g.Restore(st0); StrokeRR(g, Color.FromArgb(150, t.EdgeC), tr, 8, 1); } }
            Txt(g, t.Name.ToUpper(), 15, Ink, Rc(card.X + 14, card.Y + 42, card.Width - 28 - (ph ? 112 : 0), 24), StringAlignment.Near, true);
            TxtWrap(g, t.Blurb, 11, TM.C(190, 200, 190, 230), Rc(card.X + 14, card.Y + 66, card.Width - 28 - (ph ? 112 : 0), 28));
            if (on) { var tag = Rc(card.Right - (ph ? 196 : 76), card.Y + 43, 64, 22); FillRR(g, Color.FromArgb(230, t.Violet), tag, 8); Txt(g, "IN USE", 10, Color.Black, tag, StringAlignment.Center, true); }
            { string tg = Themes.Tag(t); bool allowed = Themes.Allowed(t);   // the themes made for one edition wear its name; one above this edition has a padlock
              if (tg.Length > 0 && !ph && !on) { Color tc = t.Req == Tier.Fantasy ? TM.C(255, 255, 110, 230) : TM.C(255, 255, 200, 90); var pill = Rc(card.Right - 90, card.Y + 43, 76, 20); FillRR(g, Color.FromArgb(60, tc), pill, 8); StrokeRR(g, Color.FromArgb(200, tc), pill, 8, 1f); Txt(g, tg, 9, tc, pill, StringAlignment.Center, true); }
              if (!allowed) { using (var br = new SolidBrush(Color.FromArgb(120, 4, 2, 10))) { using (var p = RR(card, 14)) g.FillPath(br, p); } Edition.Padlock(g, Rc(card.Right - 34, card.Y + 8, 20, 26), t.Req == Tier.Fantasy ? Color.FromArgb(255, 255, 110, 230) : Color.FromArgb(255, 255, 200, 90)); } }
            Reg(card, () => { if (!Themes.Allowed(t)) { Edition.Upsell(t.Req, "The " + t.Name + " theme", this); return; } int prev = Themes.Index; if (prev == ti) return; Themes.Apply(ti); Prefs.Changed(); OfferRevert("Theme " + list[ti].Name + " is on", () => { Themes.Apply(prev); Prefs.Changed(); }); });
            if (t.Custom) { var del = Rc(card.Right - (on ? 100 : 36), card.Y + 10, 24, 24); Btn(g, del, "x", 3, () => DeleteTheme(t), 12); Hint2(del, "Delete this theme from this PC"); }
            if (t.Rainbow && !on) { /* the rainbow card is lit by its own colours */ }
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => themeScroll, v => themeScroll = v);
        if (list.Count > 0 && total <= areaH) { }
    }
}
