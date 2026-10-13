using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// The profile pages (yours and the ones you look at): text in the colour of the profile, a box for the genre tags with the DAW scroll bar, REMOVE TAGS, LINKS, music videos, a box for the published projects.
partial class StudioForm
{
    float tagScrollOwn, tagScrollArt, profPubH, artPubH;

    // ---- the text of a profile takes the colour of the profile (what the owner chose in the colour mixer), for everyone who looks at it ----
    Color ProfInk(int packed) { return MixColours.Blend(MixLegible(ProfileStore.ColorOf(packed)), Color.White, 0.12f); }
    Color ProfDim(int packed) { return MixColours.Blend(MixLegible(ProfileStore.ColorOf(packed)), Color.FromArgb(255, 125, 112, 150), 0.28f); }
    Color tintBaseInk; bool tinting;   // the normal writing colour while a profile is drawn in its own colour (the name is always in it unless the name fades)
    Color PlainInk { get { return tinting ? tintBaseInk : Ink; } }
    void PaintTinted(int packed, Action paint)
    {
        if (packed < 0) { paint(); return; }
        var oi = Ink; var od = DimC; tintBaseInk = oi; tinting = true; Ink = ProfInk(packed); DimC = ProfDim(packed); try { paint(); } finally { Ink = oi; DimC = od; tinting = false; }
    }
    void PaintProfile(Graphics g) { PaintTinted(ProfileStore.Accent, () => PaintProfile0(g)); }
    void PaintArtistView(Graphics g, float W, float top, float bottom) { var a = artSel; PaintTinted(a == null ? -1 : (a.NameRgb >= 0 ? a.NameRgb : a.Accent), () => PaintArtistView0(g, W, top, bottom)); }

    // the banner is alive: a band of light sweeps across it every few seconds and little sparks drift up and twinkle (in the colour of the profile)
    void AnimBanner(Graphics g, RectangleF r, Color accent)
    {
        if (Prefs.PerfMode) return;   // PERFORMANCE MODE: the banner stands still
        double t = Environment.TickCount / 1000.0; lastInput = DateTime.Now; var gs = g.Save(); using (var clip = RR(r, 18)) g.SetClip(clip, CombineMode.Intersect); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = Math.Max(60f, r.Width * 0.2f), x0 = r.X - w + (r.Width + 2 * w) * (float)((t / 4.8) % 1.0); var band = new RectangleF(x0, r.Y, w, r.Height);
        using (var br = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, 0f)) { var cb = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0, accent), Color.FromArgb(58, MixC(accent, Color.White, 0.55f)), Color.FromArgb(0, accent) }, Positions = new[] { 0f, 0.5f, 1f } }; br.InterpolationColors = cb; g.FillRectangle(br, band); }
        for (int i = 0; i < 22; i++)
        {
            double fx = (i * 0.61803 + t * (0.012 + 0.006 * (i % 3))) % 1.0, fy = 1.0 - ((i * 0.37 + t * (0.05 + 0.025 * (i % 4))) % 1.0); float px = r.X + (float)fx * r.Width, py = r.Y + (float)fy * r.Height; double tw = Math.Abs(Math.Sin(t * (1.4 + (i % 5) * 0.3) + i)); float sz = 1.6f + (i % 3) * 0.9f;
            Color sc = i % 3 == 0 ? accent : (i % 3 == 1 ? Color.White : Color.FromArgb(255, 0, 229, 255)); using (var br = new SolidBrush(Color.FromArgb((int)(34 + 190 * tw), sc))) g.FillEllipse(br, px - sz / 2, py - sz / 2, sz, sz);
            if (tw > 0.85) using (var pen = new Pen(Color.FromArgb((int)(120 * tw), Color.White), 1f)) { g.DrawLine(pen, px - 4, py, px + 4, py); g.DrawLine(pen, px, py - 4, px, py + 4); }
        }
        g.SmoothingMode = sm; g.Restore(gs);
    }
    // lights that run round the edge of the picture, with a short bright tail
    void AnimRing(Graphics g, RectangleF r, Color c)
    {
        if (Prefs.PerfMode) return;   // PERFORMANCE MODE: the picture ring stands still
        double t = Environment.TickCount / 1000.0; lastInput = DateTime.Now; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; float w = r.Width, h = r.Height, P = 2 * (w + h);
        Func<float, PointF> at = d => { d = ((d % P) + P) % P; if (d < w) return new PointF(r.X + d, r.Y); d -= w; if (d < h) return new PointF(r.Right, r.Y + d); d -= h; if (d < w) return new PointF(r.Right - d, r.Bottom); d -= w; return new PointF(r.X, r.Bottom - d); };
        for (int k = 0; k < 3; k++)
        {
            float head = (float)((t / 3.2 + k / 3.0) % 1.0) * P; Color col = k == 1 ? Color.FromArgb(255, 0, 229, 255) : c;
            for (int s = 0; s < 14; s++) { var p = at(head - s * 3.5f); float a = 1f - s / 14f; float sz = 2f + 5f * a; using (var br = new SolidBrush(Color.FromArgb((int)(210 * a * a), s < 2 ? Color.White : col))) g.FillEllipse(br, p.X - sz / 2, p.Y - sz / 2, sz, sz); }
        }
        g.SmoothingMode = sm;
    }

    // a bio of any length: the block is as tall as the text needs (returns the height used)
    // the bio of an artist in a box of its own, with COPY NAME, COPY BIO and TRANSLATE (their name or bio can be in a language you do not read: copy it, or let Google Translate detect the language and translate it to yours)
    internal Action<string> openUrlHook;   // a test answers for itself
    float ArtistBioBox(Graphics g, ArtistInfo a, float x, float y, float w)
    {
        string bio = a.Bio ?? ""; var f = GfxCache.Exact(GfxCache.Grow(14), false); float th = bio.Length == 0 ? 22 : Math.Max(26f, g.MeasureString(bio, f, (int)(w - 32)).Height + (EmojiText.Has(bio) ? f.GetHeight(g) + 2 : 3f));
        float h = 46 + th + 14; var box = Rc(x, y, w, h); FillRR(g, TM.C(190, 8, 4, 20), box, 12); StrokeRR(g, Color.FromArgb(140, NeonViolet), box, 12, 1f);
        Txt(g, "BIO", 11, DimC, Rc(box.X + 16, box.Y + 12, 60, 18), StringAlignment.Near, true);
        float bx = box.Right - 12; var tr = Rc(bx - 120, box.Y + 8, 120, 26); bx = tr.X - 8; var cb = Rc(bx - 96, box.Y + 8, 96, 26); bx = cb.X - 8; var cn = Rc(bx - 110, box.Y + 8, 110, 26);
        if (w > 520) { string nm = a.Name, bi = bio; var aa = a;
            Btn(g, cn, "COPY NAME", 0, () => CopyArtistText(nm, "the name"), 10); Hint2(cn, "Copy their name (to add them, or to search for them)");
            Btn(g, cb, "COPY BIO", 0, () => CopyArtistText(bi, "the bio"), 10); Hint2(cb, "Copy their bio");
            Btn(g, tr, "TRANSLATE", 1, () => TranslateArtist(aa), 10); Hint2(tr, "Open Google Translate with their name and bio: it finds out which language it is by itself and translates it to yours"); }
        if (bio.Length > 0) TxtWrap(g, bio, 14, a.NameRgb >= 0 ? ProfileStore.ColorOf(a.NameRgb) : PlainInk, Rc(box.X + 16, box.Y + 40, w - 32, th)); else Txt(g, "No bio yet.", 13, DimC, Rc(box.X + 16, box.Y + 40, w - 32, 22), StringAlignment.Near, false);
        return h + 8;
    }
    void CopyArtistText(string text, string what) { if (string.IsNullOrEmpty(text)) { Toast("There is nothing to copy."); return; } try { Clipboard.SetText(text); Toast("Copied " + what + "."); } catch { Toast("Could not copy (another program is using the clipboard)."); } }
    internal static string TranslateUrl(string name, string bio, string lang)
    {
        string text = (name ?? "") + ((bio ?? "").Length > 0 ? "\n\n" + bio : ""); if (text.Length > 3500) text = text.Substring(0, 3500);
        if (string.IsNullOrEmpty(lang) || lang.Length > 8) lang = "en"; return "https://translate.google.com/?sl=auto&tl=" + Uri.EscapeDataString(lang) + "&text=" + Uri.EscapeDataString(text) + "&op=translate";
    }
    void TranslateArtist(ArtistInfo a)
    {
        string lang = "en"; try { lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; } catch { }
        string url = TranslateUrl(a.Name, a.Bio, lang); if (openUrlHook != null) { openUrlHook(url); return; }
        if (!ConfirmLeave(url)) return; try { System.Diagnostics.Process.Start(url); Toast("Opening Google Translate: it detects the language and translates it to yours."); } catch { Toast("Could not open the browser."); }
    }
    float BioBlock(Graphics g, string s, float px, Color c, float x, float y, float w)
    {
        var f = GfxCache.Exact(GfxCache.Grow(px), false); float h = g.MeasureString(s, f, (int)w).Height; h = Math.Max(44f, h + (EmojiText.Has(s) ? f.GetHeight(g) + 2 : 3f));
        TxtWrap(g, s, px, c, Rc(x, y, w, h)); return h + 4;
    }

    // ---- the genre tags: any number of them, in a box that scrolls with the bar of the DAW ----
    float TagBox(Graphics g, float x0, float y, float w, string[] tags, Color c, bool own)
    {
        const float rowH = 28, viewMax = 168;
        Func<float, List<RectangleF>> layout = mx => { var l = new List<RectangleF>(); float x = x0, yy = 0; foreach (var tg in tags) { float cw = 16 + (tg.Length + 1) * 6.8f; if (x + cw > mx && x > x0) { x = x0; yy += rowH; } l.Add(new RectangleF(x, yy, cw, 22)); x += cw + 6; } return l; };
        var rects = layout(x0 + w); float content = (rects.Count == 0 ? 0 : rects[rects.Count - 1].Y + rowH) + 4; bool scrolls = content > viewMax + 1;
        if (scrolls) { rects = layout(x0 + w - 52); content = rects[rects.Count - 1].Y + rowH + 4; }
        float viewH = scrolls ? viewMax : content; float sc = own ? tagScrollOwn : tagScrollArt; sc = Math.Max(0, Math.Min(Math.Max(0, content - viewH), sc)); Action<float> set = v => { v = Math.Max(0, Math.Min(Math.Max(0, content - viewH), v)); if (own) tagScrollOwn = v; else tagScrollArt = v; };
        var area = Rc(x0, y, scrolls ? w - 48 : w, viewH); var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i]; r.Y += y - sc; if (r.Bottom < y - 2 || r.Y > y + viewH + 2) continue; var rr = new RectangleF(r.X, r.Y, r.Width, r.Height);
            FillRR(g, Color.FromArgb(70, c), rr, 11); StrokeRR(g, Color.FromArgb(150, c), rr, 11, 1); Txt(g, "#" + tags[i], 11, Ink, rr, StringAlignment.Center, true);
        }
        g.Restore(gs);
        if (scrolls)
        {
            float cur = sc; regs.Add(new HitReg { R = area, OnWheel = d => set(cur - d * 40) });
            VScroll(g, Rc(x0 + w - 44, y, 42, viewH), content, viewH, () => own ? tagScrollOwn : tagScrollArt, set);
        }
        return viewH + 4;
    }

    // REMOVE TAGS: all of them, only the ones typed by hand, or one by one
    void RemoveTagsMenu(RectangleF at)
    {
        var tags = ProfileStore.Tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(); var m = Win.NewMenu();
        if (tags.Count == 0) { m.Items.Add("You have no tags.").Enabled = false; m.Show(this, new Point((int)at.X, (int)at.Bottom)); return; }
        Action<List<string>, string> set = (keep, msg) => { if (keep.Count == 0 && !Sure("remove all your tags")) return; ProfileStore.Tags = string.Join(" ", keep.ToArray()); ProfileStore.Save(); QueueRepublish(); tagScrollOwn = 0; Toast(msg); };
        m.Items.Add("Remove ALL my tags (" + tags.Count + ")").Click += (a, b) => set(new List<string>(), "All your tags were removed.");
        var custom = tags.FindAll(t => Array.IndexOf(StudioDialogs.SuggestedTagList, t) < 0);
        if (custom.Count > 0) m.Items.Add("Remove only the tags I typed myself (" + custom.Count + ")").Click += (a, b) => set(tags.FindAll(t => Array.IndexOf(StudioDialogs.SuggestedTagList, t) >= 0), custom.Count + " of your own tags were removed.");
        m.Items.Add(new ToolStripSeparator());
        int shown = 0; for (int i = tags.Count - 1; i >= 0 && shown < 24; i--, shown++) { string t = tags[i]; m.Items.Add("Remove #" + t).Click += (a, b) => { var keep = new List<string>(tags); keep.Remove(t); set(keep, "#" + t + " was removed."); }; }
        if (tags.Count > shown) m.Items.Add("(" + (tags.Count - shown) + " older ones: use the first two choices, or EDIT)").Enabled = false;
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }

    // ---- LINKS: the streaming and social links of a profile in one list ----
    void LinksMenu(RectangleF at, List<string> links, bool mine, Dictionary<string, string> titles = null)
    {
        var m = Win.NewMenu(); if (links.Count == 0) m.Items.Add(mine ? "You have not added a link yet." : "No links on this profile.").Enabled = false;
        foreach (var l in links) { string u = l; string name = ProfileStore.TitleOf(titles, u); string shortUrl = u.Replace("https://", ""); if (shortUrl.Length > 48) shortUrl = shortUrl.Substring(0, 47) + "..."; m.Items.Add(name + "      " + shortUrl).Click += (a, b) => OpenLink(u); }
        if (links.Count > 0) { m.Items.Add(new ToolStripSeparator()); m.Items.Add("Copy all the links").Click += (a, b) => { try { Clipboard.SetText(string.Join("\r\n", links.ToArray())); Toast("Links copied."); } catch { Toast("Could not copy (another program is using the clipboard)."); } }; }
        if (mine) { m.Items.Add(new ToolStripSeparator()); m.Items.Add("Edit my links...").Click += (a, b) => EditLinks(); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    void LinksButton(Graphics g, RectangleF r, List<string> links, bool mine, Dictionary<string, string> titles = null)
    {
        Btn(g, r, "LINKS" + (links.Count > 0 ? "  (" + links.Count + ")" : ""), links.Count > 0 ? 2 : 1, () => LinksMenu(r, links, mine, titles), 12); if (links.Count > 0) LinkShine(g, r, NeonCyan, 0);
        Hint2(r, links.Count > 0 ? "See " + (mine ? "your" : "their") + " streaming and social links, and open one" : (mine ? "Add your links (streaming services and social media only)" : "This artist has not added any links"));
    }

    // ---- a music video on a published project: a link to it (YouTube, Vimeo, TikTok... streaming and social links only) ----
    void VideoMenu(RectangleF at, ProjectEntry e)
    {
        if ((e.Video ?? "").Length == 0) { SetVideo(e); return; }
        var m = Win.NewMenu(); m.Items.Add("Upload a video file instead...").Click += (a, b) => UploadMusicVideo(e); m.Items.Add("Watch the music video").Click += (a, b) => OpenLink(e.Video); m.Items.Add("Change the link...").Click += (a, b) => SetVideo(e); m.Items.Add("Remove the music video").Click += (a, b) => { e.Video = ""; ProjectLibrary.Save(); QueueRepublish(); Toast("Music video removed."); };
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    void SetVideo(ProjectEntry e)
    {
        string t = Ask("Link to the music video for \"" + e.Name + "\" (YouTube, Vimeo, TikTok, Instagram... a streaming or social link):", e.Video ?? ""); if (t == null) return; t = t.Trim();
        if (t.Length == 0) { e.Video = ""; ProjectLibrary.Save(); QueueRepublish(); Toast("Music video removed."); return; }
        string c = ProfileStore.CleanLink(t); if (c.Length == 0) { Toast("Only links to streaming services and social media can be used (YouTube, Vimeo, TikTok, Instagram, SoundCloud...)."); return; }
        e.Video = c; ProjectLibrary.Save(); QueueRepublish(); Toast("Music video added: it shows on your profile as MUSIC VIDEO.");
    }

    // a neon box round a section; the height is the one it had last time (the box is drawn first, so what is inside can be drawn over it)
    RectangleF SectionBox(Graphics g, float x, float y, float w, float lastH) { var b = Rc(x, y, w, Math.Max(110, lastH)); FillRR(g, TM.C(255, 12, 6, 28), b, 16); Win.DrawNeonBox(g, b, false); return b; }

    // the name in one colour or fading from one colour to another (the colours are the ones the owner published)
    void NameTextC(Graphics g, string s, float px, RectangleF r, Color normal, bool bold, int rgb1, int rgb2)
    {
        if (rgb1 < 0) { Txt(g, s, px, normal, r, StringAlignment.Near, bold); return; }
        Color c1 = ProfileStore.ColorOf(rgb1); if (rgb2 < 0 || NoTextDraw || EmojiText.Has(s)) { Txt(g, s, px, c1, r, StringAlignment.Near, bold); return; }
        float real = GfxCache.Grow(px); var f = GfxCache.Exact(real, bold); float w = Math.Max(20f, Math.Min(r.Width, g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4));
        using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y, w, Math.Max(2f, r.Height)), c1, ProfileStore.ColorOf(rgb2), 0f)) g.DrawString(s, f, br, r, GfxCache.Fmt(StringAlignment.Near, true));
    }
}
