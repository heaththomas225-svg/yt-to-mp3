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
    void PaintTinted(int packed, Action paint)
    {
        if (packed < 0) { paint(); return; }
        var oi = Ink; var od = DimC; Ink = ProfInk(packed); DimC = ProfDim(packed); try { paint(); } finally { Ink = oi; DimC = od; }
    }
    void PaintProfile(Graphics g) { PaintTinted(ProfileStore.Accent, () => PaintProfile0(g)); }
    void PaintArtistView(Graphics g, float W, float top, float bottom) { var a = artSel; PaintTinted(a == null ? -1 : (a.NameRgb >= 0 ? a.NameRgb : a.Accent), () => PaintArtistView0(g, W, top, bottom)); }

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
        Action<List<string>, string> set = (keep, msg) => { ProfileStore.Tags = string.Join(" ", keep.ToArray()); ProfileStore.Save(); QueueRepublish(); tagScrollOwn = 0; Toast(msg); };
        m.Items.Add("Remove ALL my tags (" + tags.Count + ")").Click += (a, b) => set(new List<string>(), "All your tags were removed.");
        var custom = tags.FindAll(t => Array.IndexOf(StudioDialogs.SuggestedTagList, t) < 0);
        if (custom.Count > 0) m.Items.Add("Remove only the tags I typed myself (" + custom.Count + ")").Click += (a, b) => set(tags.FindAll(t => Array.IndexOf(StudioDialogs.SuggestedTagList, t) >= 0), custom.Count + " of your own tags were removed.");
        m.Items.Add(new ToolStripSeparator());
        int shown = 0; for (int i = tags.Count - 1; i >= 0 && shown < 24; i--, shown++) { string t = tags[i]; m.Items.Add("Remove #" + t).Click += (a, b) => { var keep = new List<string>(tags); keep.Remove(t); set(keep, "#" + t + " was removed."); }; }
        if (tags.Count > shown) m.Items.Add("(" + (tags.Count - shown) + " older ones: use the first two choices, or EDIT)").Enabled = false;
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }

    // ---- LINKS: the streaming and social links of a profile in one list ----
    void LinksMenu(RectangleF at, List<string> links, bool mine)
    {
        var m = Win.NewMenu(); if (links.Count == 0) m.Items.Add(mine ? "You have not added a link yet." : "No links on this profile.").Enabled = false;
        foreach (var l in links) { string u = l; string name = ProfileStore.LinkLabel(u); string shortUrl = u.Replace("https://", ""); if (shortUrl.Length > 44) shortUrl = shortUrl.Substring(0, 43) + "..."; m.Items.Add(name + "      " + shortUrl).Click += (a, b) => OpenLink(u); }
        if (links.Count > 0) { m.Items.Add(new ToolStripSeparator()); m.Items.Add("Copy all the links").Click += (a, b) => { try { Clipboard.SetText(string.Join("\r\n", links.ToArray())); Toast("Links copied."); } catch { Toast("Could not copy (another program is using the clipboard)."); } }; }
        if (mine) { m.Items.Add(new ToolStripSeparator()); m.Items.Add("Edit my links...").Click += (a, b) => EditLinks(); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    void LinksButton(Graphics g, RectangleF r, List<string> links, bool mine)
    {
        Btn(g, r, "LINKS" + (links.Count > 0 ? "  (" + links.Count + ")" : ""), links.Count > 0 ? 2 : 1, () => LinksMenu(r, links, mine), 12);
        Hint2(r, links.Count > 0 ? "See " + (mine ? "your" : "their") + " streaming and social links, and open one" : (mine ? "Add your links (streaming services and social media only)" : "This artist has not added any links"));
    }

    // ---- a music video on a published project: a link to it (YouTube, Vimeo, TikTok... streaming and social links only) ----
    void VideoMenu(RectangleF at, ProjectEntry e)
    {
        if ((e.Video ?? "").Length == 0) { SetVideo(e); return; }
        var m = Win.NewMenu(); m.Items.Add("Watch the music video").Click += (a, b) => OpenLink(e.Video); m.Items.Add("Change the link...").Click += (a, b) => SetVideo(e); m.Items.Add("Remove the music video").Click += (a, b) => { e.Video = ""; ProjectLibrary.Save(); QueueRepublish(); Toast("Music video removed."); };
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
