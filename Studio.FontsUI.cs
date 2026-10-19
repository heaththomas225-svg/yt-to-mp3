using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The Fonts page (page 8): the font in use shown big, a search box, the groups, and a card for every font written in itself. Click a card and the whole studio is set in that font.
partial class StudioForm
{
    float fontScroll, fontContentH; string fontQuery = "", fontCat = ""; bool fontSearchOn, fontAsked; RectangleF fontSearchR; FontKit.Entry fontPending;

    bool FontSearchActive { get { return (tab == 8 || fontsWin) && fontSearchOn && !OverlayOpen && (editBox == null || !editBox.Visible); } }
    bool FontSearchKey(char c)
    {
        if (!FontSearchActive) return false; if (c == '\b') { if (fontQuery.Length > 0) fontQuery = fontQuery.Substring(0, fontQuery.Length - 1); } else if (!char.IsControl(c) && fontQuery.Length < 30) fontQuery += c; else return true;
        fontScroll = 0; return true;
    }
    bool FontSearchCmd(Keys k)
    {
        if (!FontSearchActive) return false; if (k == Keys.Escape) { if (fontQuery.Length > 0) { fontQuery = ""; fontScroll = 0; } else fontSearchOn = false; return true; } if (k == Keys.Enter || k == Keys.Return) { fontSearchOn = false; return true; } return false;
    }
    bool FontSearchTakesKey(Keys k) { return FontSearchActive && (k & Keys.Control) == 0 && (k & Keys.Alt) == 0 && !((k & Keys.KeyCode) >= Keys.F1 && (k & Keys.KeyCode) <= Keys.F24); }
    internal void OpenFonts() { tab = 8; fontScroll = 0; foreach (var e in FontKit.All) if (e.State == 2) e.State = 0; }

    // a card or a sample: the text in its own font, made smaller (never cut) when the box is too narrow
    void TxtIn(Graphics g, string s, FontKit.Entry e, float px, bool bold, Color c, RectangleF r, StringAlignment ha)
    {
        if (e == null || e.Fam == null) { Txt(g, s, px, c, r, ha, bold); return; }   // not here yet: written in the font in use for now
        float use = px; Font f = FontKit.Preview(e, use, bold); float w = g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4;
        while (w > r.Width && use > px * 0.55f) { use -= 0.5f; f = FontKit.Preview(e, use, bold); w = g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4; }
        using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, GfxCache.Fmt(ha, false));
    }

    List<FontKit.Entry> FontsShown()
    {
        var l = new List<FontKit.Entry>(); string q = fontQuery.Trim().ToLowerInvariant();
        foreach (var e in FontKit.All)
        {
            if (fontCat.Length > 0 && e.Cat != fontCat) continue; if (q.Length > 0 && e.Name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && e.Cat.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue; l.Add(e);
        }
        return l;
    }
    void FontPick(FontKit.Entry e)
    {
        if (FontKit.Ready(e)) { fontPending = null; if (FontKit.Apply(e)) Toast("Font: " + e.Name + ". Every box and button was fitted to it."); else Toast(e.Name + " could not be loaded: it was not used."); return; }
        fontPending = e; Toast("Getting " + e.Name + "...");
        FontKit.Want(e, this, () => { if (FontKit.Ready(e)) { if (fontPending == e) FontPick(e); } else if (fontPending == e) { fontPending = null; Toast("Could not get " + e.Name + ". Check your internet connection and try again."); } Invalidate(); });
    }

    // the whole Fonts page, or the mini window over the studio
    internal bool fontsWin; RectangleF fontArea; internal PointF fontsWinAt = new PointF(-1, -1); PointF fontsGrab; bool fontsDrag; internal RectangleF fontsWinR;
    void PaintFonts(Graphics g)
    {
        float H0 = ClientSize.Height; fontArea = Rc(0, ToolH, ClientSize.Width, H0 - (previewPath.Length > 0 ? 62 : 0) - ToolH); PaintLogoBackdrop(g, fontArea);
        var sm = Rc(ClientSize.Width - 230, ToolH + 8, 160, 30); PaintFontsIn(g); Btn(g, sm, "SMALL WINDOW", 0, () => { fontsWin = true; GoTab(0); }, 10); Hint2(sm, "Fonts in a small window over the studio, so you see your buttons change while you pick");
    }
    void PaintFontsWindow(Graphics g)
    {
        if (!fontsWin || tab == 8) return; float W0 = ClientSize.Width, H0 = ClientSize.Height; float ww = Math.Min(900, W0 - 80), wh = Math.Min(660, H0 - 150); float wx = fontsWinAt.X >= 0 ? fontsWinAt.X : W0 - ww - 40, wy = fontsWinAt.Y >= 0 ? fontsWinAt.Y : 110; wx = Math.Max(0, Math.Min(W0 - ww, wx)); wy = Math.Max(ToolH * 0.5f, Math.Min(H0 - 60, wy)); var win = Rc(wx, wy, ww, wh); fontsWinR = win;
        Reg(win, () => { });
        { var ttl = Rc(win.X, win.Y, win.Width - 190, 40); var tr = Reg(ttl, () => { fontsDrag = true; fontsGrab = new PointF(MousePos.X - win.X, MousePos.Y - win.Y); });   // drag the title bar to move the window
          tr.OnDrag = pt => { if (!fontsDrag) return; fontsWinAt = new PointF(pt.X - fontsGrab.X, pt.Y - fontsGrab.Y); Invalidate(); }; tr.OnUp = () => fontsDrag = false; Hint2(ttl, "Drag here to move the fonts window"); } FillRR(g, TM.C(250, 14, 7, 30), win, 14); Win.DrawNeonBox(g, win, true);
        Txt(g, "FONTS", 15, Ink, Rc(win.X + 18, win.Y + 8, 200, 26), StringAlignment.Near, true); Txt(g, "Pick one and the whole studio changes at once (drag this bar to move the window).", 10, DimC, Rc(win.X + 110, win.Y + 12, win.Width - 340, 20), StringAlignment.Near, false);
        var fsb = Rc(win.Right - 176, win.Y + 8, 120, 28); Btn(g, fsb, "FULLSCREEN", 1, () => { fontsWin = false; GoTab(8); fontScroll = 0; }, 10); Hint2(fsb, "The Fonts page on the whole screen");
        var xb = Rc(win.Right - 48, win.Y + 8, 34, 28); Btn(g, xb, "x", 0, () => fontsWin = false, 12); Hint2(xb, "Close the fonts window");
        fontArea = Rc(win.X + 4, win.Y + 42, win.Width - 8, win.Height - 46); var gs0 = g.Save(); g.SetClip(fontArea, CombineMode.Intersect); PaintFontsIn(g); g.Restore(gs0);
    }
    void PaintFontsIn(Graphics g)
    {
        var A = fontArea; float X0 = A.X, W = A.Width, top = A.Y, bottom = A.Bottom, viewH = bottom - top; FontKit.Build();
        if (!fontAsked) { fontAsked = true; FontKit.Refresh(this, () => Invalidate()); }
        float cw = Math.Min(1500, W - 110), cx = X0 + (W - 54 - cw) / 2f; if (cx < X0 + 20) { cx = X0 + 20; cw = W - 94; }
        fontScroll = Math.Max(0, Math.Min(Math.Max(0, fontContentH - viewH + 20), fontScroll)); regs.Add(new HitReg { R = Rc(X0, top, W, viewH), OnWheel = d => fontScroll = Math.Max(0, Math.Min(Math.Max(0, fontContentH - viewH + 20), fontScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(Rc(X0, top, W - 56, viewH), CombineMode.Intersect); float y = top + 14 - fontScroll, y00 = y;
        // ---- the font in use ----
        var card = Rc(cx, y, cw, FontKit.HardCurrent ? 182 : 150); FillRR(g, TM.C(235, 16, 9, 34), card, 16); Win.DrawNeonBox(g, card, false);
        Txt(g, "YOUR FONT", 12, DimC, Rc(card.X + 20, card.Y + 12, 200, 18), StringAlignment.Near, true); Txt(g, FontKit.CurrentName, 24, Ink, Rc(card.X + 20, card.Y + 30, card.Width - 300, 36), StringAlignment.Near, true);
        { var cur0 = FontKit.Find(FontKit.CurrentId); bool hardNow = FontKit.HardCurrent && cur0 != null && cur0.Fam != null;   // the samples are written in the font itself (the rest of the page stays clear when the font is thin or hollow)
          Action<string, float, Color, float> sample = (t, px, col, yy) => { if (cur0 != null && cur0.Fam != null && hardNow) { using (var f0 = FontKit.Preview(cur0, px, false)) using (var b0 = new SolidBrush(col)) g.DrawString(t, f0, b0, new RectangleF(card.X + 20, yy, card.Width - 300, px * 1.6f), GfxCache.Fmt(StringAlignment.Near, true)); } else Txt(g, t, px, col, Rc(card.X + 20, yy, card.Width - 300, px * 1.5f), StringAlignment.Near, false); };
          sample("The quick brown fox jumps over the lazy dog", 24, Ink, card.Y + 66); sample("ABCDEFGHIJKLMNOPQRSTUVWXYZ   0123456789", 17, DimC, card.Y + 100); sample("abcdefghijklmnopqrstuvwxyz   . , ! ? & @ #", 17, DimC, card.Y + 124);
          if (hardNow) Txt(g, "THIN OR HOLLOW FONT: it writes the big titles, and the small writing stays in the clear original font so you can read everything.", 11, Color.FromArgb(255, 210, 120), Rc(card.X + 20, card.Bottom - 22, card.Width - 40, 18), StringAlignment.Near, true); }
        bool isDef = FontKit.CurrentId == FontKit.DefaultId; var rb = Rc(card.Right - 250, card.Y + 14, 232, 36); Btn(g, rb, "USE THE ORIGINAL FONT", isDef ? 0 : 1, () => { var d = FontKit.Find(FontKit.DefaultId); if (d != null && FontKit.Apply(d)) Toast("The original font is back."); }, 12); Hint2(rb, "Go back to Bahnschrift, the font Purphex Music came with");
        y = card.Bottom + 16;
        // ---- search and groups ----
        var sb = Rc(cx, y, Math.Max(220, Math.Min(380, cw * 0.32f)), 38); FillRR(g, TM.C(255, 14, 7, 32), sb, 9); Win.DrawNeonBox(g, sb, fontSearchOn); bool blink = ((int)(Environment.TickCount / 500) % 2) == 0 && fontSearchOn;
        Txt(g, fontQuery.Length == 0 ? (blink ? "|  " : "   ") + "SEARCH 190+ FONTS BY NAME" : fontQuery + (blink ? "|" : ""), 13, fontQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 14, sb.Y, sb.Width - 54, sb.Height), StringAlignment.Near, fontQuery.Length > 0);
        fontSearchR = sb; Reg(sb, () => fontSearchOn = true); Hint2(sb, "Type part of a font name, or a group such as neon or script. Click here first.");
        if (fontQuery.Length > 0) { var x = Rc(sb.Right - 34, sb.Y + 7, 24, 24); Btn(g, x, "x", 3, () => { fontQuery = ""; fontScroll = 0; }, 12); Hint2(x, "Clear the search"); }
        var shown = FontsShown(); int ready = 0; foreach (var e in FontKit.All) if (FontKit.Ready(e)) ready++;
        Txt(g, shown.Count + " OF " + FontKit.All.Count + " FONTS   -   " + ready + " ON THIS PC", 12, DimC, Rc(sb.Right + 16, sb.Y, 360, sb.Height), StringAlignment.Near, true);
        y = sb.Bottom + 12; float chipX = cx; var names = new List<string>(); names.Add(""); names.AddRange(FontKit.Cats);
        foreach (var cn in names)
        {
            string label = cn.Length == 0 ? "ALL" : cn.ToUpper(); float w = Math.Max(60, 26 + label.Length * 7.4f); if (chipX + w > cx + cw) { chipX = cx; y += 38; } var r = Rc(chipX, y, w, 30); string cc = cn; int count = 0; foreach (var e in FontKit.All) if (cc.Length == 0 || e.Cat == cc) count++;
            if (cn.Length == 0 || count > 0) { Btn(g, r, label, fontCat == cc ? 2 : 1, () => { fontCat = cc; fontScroll = 0; }, 11); Hint2(r, cn.Length == 0 ? "Every font" : count + " fonts: " + cn); chipX += w + 8; }
        }
        y += 44;
        // ---- the cards ----
        int cols = cw >= 1180 ? 4 : (cw >= 800 ? 3 : (cw >= 520 ? 2 : 1)); float gap = 12, cardW = (cw - gap * (cols - 1)) / cols, cardH = 118;
        if (shown.Count == 0) { Txt(g, "No font matches that. Try another word, or press ALL.", 14, DimC, Rc(cx + 6, y, cw - 12, 24), StringAlignment.Near, false); y += 40; }
        for (int i = 0; i < shown.Count; i++)
        {
            var e = shown[i]; int col = i % cols, row = i / cols; var r = Rc(cx + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH); if (r.Bottom < top - 4 || r.Y > bottom + 4) continue;
            if (!FontKit.Ready(e) && e.State == 0) FontKit.Want(e, this, () => Invalidate()); FontKit.Load(e); bool inUse = FontKit.CurrentId == e.Id, hot = Hot(r) && !OverlayOpen, here = e.Fam != null;
            FillRR(g, inUse ? TM.C(235, 62, 34, 120) : (hot ? TM.C(235, 34, 20, 66) : TM.C(235, 18, 10, 38)), r, 14); StrokeRR(g, inUse ? NeonPink : Color.FromArgb(hot ? 200 : 110, NeonViolet), r, 14, inUse ? 2f : 1f);
            TxtIn(g, e.Name, here ? e : null, 24, false, Ink, Rc(r.X + 14, r.Y + 8, r.Width - 28, 40), StringAlignment.Near); TxtIn(g, "Purphex Music 0123", here ? e : null, 15, false, DimC, Rc(r.X + 14, r.Y + 52, r.Width - 28, 28), StringAlignment.Near);
            Txt(g, e.Cat.ToUpper(), 10, DimC, Rc(r.X + 14, r.Bottom - 28, r.Width - 130, 20), StringAlignment.Near, false);
            string st = inUse ? "IN USE" : (here || FontKit.Ready(e) ? "READY" : (e.State == 2 ? "OFFLINE" : "GETTING...")); var pill = Rc(r.Right - 100, r.Bottom - 30, 88, 22); FillRR(g, inUse ? NeonPink : TM.C(150, 50, 30, 100), pill, 11); Txt(g, st, 10, inUse ? Color.White : Ink, pill, StringAlignment.Center, true);
            var ee = e; Reg(r, () => FontPick(ee)); if (hot) hintNow = inUse ? e.Name + " is the font in use." : "Click to set the whole studio in " + e.Name + (FontKit.Ready(e) ? "." : " (it is fetched first).");
        }
        y += ((shown.Count + cols - 1) / cols) * (cardH + gap) + 8;
        fontContentH = y - y00 + 30; g.Restore(gs);
        VScroll(g, Rc(X0 + W - 52, top + 8, 42, viewH - 16), fontContentH + 20, viewH, () => fontScroll, v => fontScroll = v);
    }
}
