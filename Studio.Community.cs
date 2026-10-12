using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Purphex Music: the Community page (left menu, feed of project cards with round neon art, right column with your profile, tags and activity),
// and the slim top bar used on the Community and My Projects pages so none of the music controls show there.
// Nothing here is faked: the cards are your own saved projects and the activity is your own saves until online sharing exists.
partial class StudioForm
{
    int commFilter; float commScroll; volatile string commInvite = ""; bool commInviteAsked;
    // the Discord invite lives in the website's settings: ask the website once (in the background) so the app never needs updating when it changes
    void FetchInvite()
    {
        if (commInviteAsked) return; commInviteAsked = true;
        ThreadPool.QueueUserWorkItem(o => { try { using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "PurphexMusic"; string j = wc.DownloadString("https://purphexmusic.com/api/community"); var m = Regex.Match(j, "\"discord\"\\s*:\\s*\"([^\"]*)\""); if (m.Success) { string u = m.Groups[1].Value.Replace("\\/", "/"); if (u.StartsWith("https://discord.gg/") || u.StartsWith("https://discord.com/invite/")) commInvite = u; } } } catch { } });
    }
    static readonly string[] CommTabs = { "All", "Recent" };

    static Color HueCol(double h, int a)
    {
        h = (h % 360 + 360) % 360; double c = 0.75, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = 0.2; double r = 0, gg = 0, b = 0;
        if (h < 60) { r = c; gg = x; } else if (h < 120) { r = x; gg = c; } else if (h < 180) { gg = c; b = x; } else if (h < 240) { gg = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb(a, (int)((r + m) * 255), (int)((gg + m) * 255), (int)((b + m) * 255));
    }

    // the slim bar on the Community and My Projects pages: menu button, page name, the page tabs, Help and Full (no transport, no track buttons)
    void PaintSlimBar(Graphics g)
    {
        float W = ClientSize.Width, y = 12, h = 46;
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH); NeonRule(g, 0, ToolH, W);
        Btn(g, Rc(12, y, 48, h), "≡", 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 24);
        string title = tab == 3 ? "MY PROJECTS" : (tab == 6 ? "PROFILE" : (tab == 7 ? "ARTISTS" : (tab == 8 ? "FONTS" : "COMMUNITY"))); Txt(g, title, 18, Ink, Rc(76, y, 190, h), StringAlignment.Near, true); if (tab != 3) Btn(g, Rc(270, y + 4, 100, h - 8), "HOME", 2, () => GoHome(), 14); if (ShowBack) Btn(g, Rc(tab == 3 ? 270 : 376, y + 4, 100, h - 8), "BACK", 0, () => GoBack(), 14);
        float rx = W - 14;
        Btn(g, Rc(rx - 58, y + 4, 58, h - 8), "Help", 2, () => app.ShowHelp(null), 14); rx -= 64;
        Btn(g, Rc(rx - 108, y + 4, 108, h - 8), Win.FullLabel(this), 0, () => Win.ToggleFullscreen(this), 12); rx -= 120;
        if (tab == 3) { string[] tn = { "MIX", "MASTERING", "LYRICS" }; int[] ti = { 0, 1, 2 }; float[] tw = { 90, 124, 98 }; for (int i = 2; i >= 0; i--) { int t = ti[i]; Btn(g, Rc(rx - tw[i], y, tw[i], h), tn[i], 0, () => tab = t, 13); rx -= tw[i] + 6; } }
        else if (tab != 8)
        {   // the community pages only link to each other
            string[] tn = { "COMMUNITY", "ARTISTS", "PROFILE" }; int[] ti = { 4, 7, 6 }; float[] tw = { 134, 112, 104 };
            for (int i = 2; i >= 0; i--) { int t = ti[i]; if (t == tab) continue; Btn(g, Rc(rx - tw[i], y + 4, tw[i], h - 8), tn[i], t == 6 ? 2 : 1, () => { if (t == 6) profScroll = 0; if (t == 7) OpenArtists(); else tab = t; }, 13); rx -= tw[i] + 6; }
        }
    }

    void PaintCommunity(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, Hb = H - (previewPath.Length > 0 ? 62 : 0); if (!projectsLoaded)   /* Hb: the bottom of the page when the preview bar is showing under it */ { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); }
        bool rail = W >= 1000; float sw = 0, rw = rail ? 290 : 0; PaintLogoBackdrop(g, Rc(sw, top, W - sw - rw, H - top));
        float cx0 = sw + 18, cw = W - sw - rw - 36 - (rail ? 18 : 0), cx1 = cx0 + cw;
        // ---- header banner ----
        var ban = Rc(cx0, top + 16, cw, 92); FillRR(g, TM.C(210, 20, 10, 44), ban, 16); Glow(g, NeonViolet, ban, 16, 0.55f, 3);
        using (var pen = new Pen(Color.FromArgb(70, NeonCyan), 1.2f)) for (int i = 0; i < 9; i++) g.DrawBezier(pen, ban.Right - 330, ban.Y + 70, ban.Right - 240, ban.Y + 10 + i * 4, ban.Right - 150, ban.Bottom - 6 - i * 3, ban.Right - 20, ban.Y + 14 + i * 6);
        Txt(g, "COMMUNITY", 24, Ink, Rc(ban.X + 22, ban.Y + 12, 400, 36), StringAlignment.Near, true); Txt(g, "DISCOVER. SHARE. CREATE. TOGETHER.", 14, DimC, Rc(ban.X + 22, ban.Y + 50, 420, 24), StringAlignment.Near, false);
        FetchInvite(); var np = Rc(ban.Right - 190, ban.Y + 26, 168, 40); Btn(g, np, "+  New project", 2, () => { tab = 0; NewProject(); }, 14); if (ban.Width > 700) { var sa0 = Rc(ban.Right - 372, ban.Y + 26, 172, 40); Btn(g, sa0, "MY PROJECTS", 1, () => GoTab(3), 13); Hint2(sa0, "Open the projects you saved on this PC"); if (ban.Width > 880) { var ar0 = Rc(sa0.X - 138, ban.Y + 26, 130, 40); Btn(g, ar0, "ARTISTS", 1, () => OpenArtists(), 13); Hint2(ar0, "Find artists and follow them"); } }
        if (commInvite.Length > 0 && ban.Width > 1080) { string inv = commInvite; Btn(g, Rc(ban.Right - 554, ban.Y + 26, 172, 40), "Join the Discord", 1, () => { try { System.Diagnostics.Process.Start(inv); } catch { Toast("Could not open the link: " + inv); } }, 14); }
        // ---- filter tabs ----
        float ty = ban.Bottom + 14; float tx = cx0 + 4;
        for (int i = 0; i < CommTabs.Length; i++)
        {
            int k = i; var r = Rc(tx, ty, 118, 34); bool on = commFilter == i; if (on) { FillRR(g, TM.C(120, 70, 40, 160), r, 10); } else if (Hot(r)) FillRR(g, TM.C(70, 70, 40, 130), r, 10);
            Txt(g, CommTabs[i], 13, on ? Ink : DimC, r, StringAlignment.Center, on); if (on) using (var pen = new Pen(NeonCyan, 2f)) g.DrawLine(pen, r.X + 12, r.Bottom - 1, r.Right - 12, r.Bottom - 1); Reg(r, () => { commFilter = k; commScroll = 0; }); tx += 124;
        }
        // ---- the feed: round neon art cards (your saved projects) ----
        var all = ProjectLibrary.AllByEdited(); var list = new List<ProjectEntry>();
        foreach (var e in all) { if (commFilter == 1 && (DateTime.Now - e.Opened).TotalDays > 7) continue; list.Add(e); }
        float fy = ty + 46, fh = Hb - fy; var area = Rc(cx0, fy, cw, fh); float cardW = 206, cardH = 390, gap = 14; int cols = Math.Max(1, (int)((cw - 56 + gap) / (cardW + gap))); float used = cols * cardW + (cols - 1) * gap, ox = cx0 + (cw - 56 - used) / 2;
        int rows = (list.Count + cols - 1) / cols; float maxS = Math.Max(0, rows * (cardH + gap) - fh + 10); commScroll = Math.Max(0, Math.Min(maxS, commScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => commScroll = Math.Max(0, Math.Min(maxS, commScroll - d * 70)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        bool overlayOk = !menuOpen && !ntOpen && !busOpen && !busy;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i]; float cx = ox + (i % cols) * (cardW + gap), cy = fy + (i / cols) * (cardH + gap) - commScroll; var card = Rc(cx, cy, cardW, cardH); if (card.Bottom < fy || card.Y > Hb) continue;
            bool hot = overlayOk && area.Contains(MousePos) && card.Contains(MousePos); FillRR(g, TM.C(hot ? 215 : 175, 18, 10, 38), card, 16); Win.DrawNeonBox(g, card, hot);
            float S = cardW - 28; var art = Rc(card.X + 14, card.Y + 14, S, S); DrawSquareArt(g, art, e);
            string path = e.Path; bool miss = e.Missing; var open = Reg(card, () => OpenProjectAsk(e)); open.OnRight = () => ProjectCardMenu(path);
            Txt(g, e.Name, 15, Ink, Rc(card.X + 14, card.Y + S + 22, cardW - 28, 22), StringAlignment.Near, true);
            var sa = Rc(card.X + 14, card.Y + S + 48, 18, 18); DrawAvatar(g, sa); Txt(g, ProjectLibrary.ProfileName, 12, DimC, Rc(card.X + 38, card.Y + S + 46, cardW - 52, 20), StringAlignment.Near, false);
            string meta = e.Tracks + (e.Tracks == 1 ? " track" : " tracks") + (e.Bpm > 0 ? "  -  " + e.Bpm.ToString("0") + " BPM" : "") + (e.Seconds > 1 ? "  -  " + ((int)(e.Seconds / 60)) + ":" + ((int)(e.Seconds % 60)).ToString("00") : ""); Txt(g, meta, 11, NeonCyan, Rc(card.X + 14, card.Y + S + 70, cardW - 28, 16), StringAlignment.Near, false);
            ProjectLibrary.MeasureAsync(e); Txt(g, "SIZE " + ProjectLibrary.Size(e.Bytes) + "  -  edited " + ProjectLibrary.Ago(e.Updated), 11, DimC, Rc(card.X + 14, card.Y + S + 88, cardW - 28, 16), StringAlignment.Near, false);
            if (e.Tags.Length > 0) { float tx2 = card.X + 14; foreach (var tg in e.Tags.Split(' ')) { string lb = "#" + tg; float tw = 14 + lb.Length * 6.6f; if (tx2 + tw > card.Right - 12) break; var tr2 = Rc(tx2, card.Y + S + 108, tw, 18); FillRR(g, TM.C(110, 60, 36, 120), tr2, 9); Txt(g, lb, 10, Ink, tr2, StringAlignment.Center, false); tx2 += tw + 5; } }
            if (e.Desc.Length > 0) Txt(g, e.Desc, 11, DimC, Rc(card.X + 14, card.Y + S + 130, cardW - 28, 32), StringAlignment.Near, false);
            else if (hot) Txt(g, "Hover tools: rename, picture, description, tags", 10, TM.C(120, 150, 130, 190), Rc(card.X + 14, card.Y + S + 130, cardW - 28, 16), StringAlignment.Near, false);
            if (!miss) PlayBox(g, Rc(card.X + 14, card.Bottom - 12 - 34, cardW - 28, 34), e);   // a solid play button along the bottom of the card, never covered by the hover tools
            if (hot)
            {
                // the hover tools sit over the picture
                using (var b = new SolidBrush(TM.C(205, 6, 3, 18))) using (var p = RR(art, 12)) g.FillPath(b, p);
                // the hover tools sit over the picture: Open on its own line, then the rest in pairs (Play is the button at the bottom of the card)
                string[] bn = { "Open", "Rename", "Picture", "No picture", "Describe", "Tags", "Folder" }; float bw = (S - 30) / 2f, bh = 32;
                Action[] ba = { () => OpenProjectAsk(e), () => RenameEntry(e), () => PickCover(path), () => { if (e.Cover.Length > 0) { ProjectLibrary.ClearCover(path); Toast("Picture removed: the Purphex logo is shown again."); } else Toast("This project has no picture of its own."); }, () => DescribeEntry(e), () => TagEntry(e), () => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { } } };
                for (int k = 0; k < 7; k++) { RectangleF br = k == 0 ? Rc(art.X + 10, art.Y + 10, S - 20, bh) : Rc(art.X + 10 + ((k - 1) % 2) * (bw + 10), art.Y + 10 + (1 + (k - 1) / 2) * (bh + 8), bw, bh); Action act = ba[k]; Btn(g, br, bn[k], k == 0 ? 2 : 1, () => act(), 12); }
            }
        }
        g.Restore(gs);
        VScroll(g, Rc(cx0 + cw - 52, fy, 42, fh - 8), fh - 8 + maxS, fh - 8, () => commScroll, v => commScroll = v);
        if (list.Count == 0) { Txt(g, commFilter == 0 ? "Nothing here yet. Make something, press Save, and it shows up as a card." : "No projects match this filter.", 14, DimC, Rc(cx0 + 10, fy + 40, cw - 20, 40), StringAlignment.Near, false); }
        // ---- right column ----
        if (rail)
        {
            float rx = cx1 + 18, ry = top + 16;
            var p2 = Rc(rx, ry, rw, 156); FillRR(g, TM.C(185, 18, 10, 38), p2, 14); StrokeRR(g, Color.FromArgb(100, NeonViolet), p2, 14, 1);
            Txt(g, "BROWSE SOUNDS BY TAG", 12, Ink, Rc(p2.X + 16, p2.Y + 12, 240, 20), StringAlignment.Near, true);
            string[] tags = { "dnb", "bassline", "garage", "trap", "rap", "bass", "drums", "synth", "vocal" }; float gx = p2.X + 14, gy = p2.Y + 42;
            foreach (var tg in tags)
            {
                string label = "# " + tg; float tw2 = 18 + label.Length * 7.5f; if (gx + tw2 > p2.Right - 10) { gx = p2.X + 14; gy += 36; } var r = Rc(gx, gy, tw2, 28); string t2 = tg;
                FillRR(g, Hot(r) ? TM.C(150, 80, 46, 170) : TM.C(90, 50, 30, 100), r, 14); Txt(g, label, 12, Ink, r, StringAlignment.Center, false); Reg(r, () => { OpenSounds(); sndFilter.Text = t2; sndFilter.Group = ""; sndFilter.Kind = ""; sndFilter.Type = ""; if (soundBox != null) soundBox.Text = t2; sndDirty = true; sndScroll = 0; }); gx += tw2 + 8;
            }
            ry = p2.Bottom + 14; var p3 = Rc(rx, ry, rw, Math.Max(150, Hb - ry - 16)); FillRR(g, TM.C(185, 18, 10, 38), p3, 14); StrokeRR(g, Color.FromArgb(100, NeonViolet), p3, 14, 1);
            Txt(g, "LATEST ACTIVITY", 12, Ink, Rc(p3.X + 16, p3.Y + 12, 240, 20), StringAlignment.Near, true);
            float ay = p3.Y + 44; int shown = 0;
            foreach (var e in all) { if (ay + 44 > p3.Bottom - 6 || shown >= 5) break; var sa = Rc(p3.X + 14, ay, 30, 30); DrawAvatar(g, sa); Txt(g, "YOU SAVED " + e.Name, 12, Ink, Rc(p3.X + 54, ay - 2, rw - 70, 18), StringAlignment.Near, false); Txt(g, ProjectLibrary.Ago(e.Updated), 11, DimC, Rc(p3.X + 54, ay + 16, rw - 70, 16), StringAlignment.Near, false); ay += 44; shown++; }
            if (shown == 0) Txt(g, "Your saves and uploads will show here.", 12, DimC, Rc(p3.X + 16, ay, rw - 32, 20), StringAlignment.Near, false);
        }
    }

    // project picture: the WHOLE picture in a square, wrapped in a neon glow (a generated burst when there is no picture)
    void DrawSquareArt(Graphics g, RectangleF r, ProjectEntry e)
    {
        int seed = 17; foreach (char ch in e.Name ?? "") seed = seed * 31 + ch; var rnd = new Random(seed); double hue = rnd.Next(240, 330); Color c1 = HueCol(hue, 255), c2 = HueCol(hue + 40, 255);
        double pulse = 0.5 + 0.5 * Math.Sin(Environment.TickCount / 600.0);
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((16 + 14 * pulse) / i * 2.4f) + 6, c1), i * 4f)) using (var p = RR(RectangleF.Inflate(r, i * 1.2f, i * 1.2f), 14 + i)) g.DrawPath(pen, p);
        var gs = g.Save(); using (var p = RR(r, 14))
        {
            g.SetClip(p, CombineMode.Intersect); using (var br = new LinearGradientBrush(r, TM.C(255, 30, 12, 66), TM.C(255, 6, 2, 16), 90f)) g.FillRectangle(br, r);
            bool pic = !string.IsNullOrEmpty(e.Cover) && ProjectLibrary.Scaled(e.Cover, (int)r.Width, (int)r.Height, InterpolationMode.HighQualityBilinear) != null;
            if (pic) DrawCover(g, r, e); else DrawLogoArt(g, r);   // no picture: the Purphex logo
        }
        g.Restore(gs);
        using (var pen = new Pen(Color.FromArgb(240, c2), 2.2f)) using (var p = RR(r, 14)) g.DrawPath(pen, p);
    }

    void RenameEntry(ProjectEntry e)
    {
        string nm = Ask("Project name:", e.Name); if (nm == null) return; nm = nm.Trim(); if (nm.Length == 0) return;
        e.Name = nm; e.Custom = true; if (string.Equals(e.Path, projPath, StringComparison.OrdinalIgnoreCase)) projName = nm; ProjectLibrary.Save(); Toast("Renamed to " + nm);
    }
    void DescribeEntry(ProjectEntry e)
    {
        string d = Ask("Description (one line, up to 140 characters):", e.Desc); if (d == null) return; d = d.Trim(); if (d.Length > 140) d = d.Substring(0, 140);
        e.Desc = d; ProjectLibrary.Save(); Toast(d.Length == 0 ? "Description removed" : "Description saved");
    }
    void TagEntry(ProjectEntry e)
    {
        string t = StudioDialogs.Tags(this, e.Tags); if (t == null) return;
        e.Tags = ProjectLibrary.CleanTags(t); ProjectLibrary.Save(); Toast(e.Tags.Length == 0 ? "Tags removed" : "Tags saved: " + e.Tags);
    }
}
