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
    static readonly string[] CommTabs = { "ALL", "NEW RELEASES", "ALBUMS", "EPS", "SAVED" };
    // the community itself lives on the website (members sign in there)
    void OpenCommunitySite(string path) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CommunityPresets.BaseUrl + path) { UseShellExecute = true }); Toast("Opening the community on the website: sign in there."); } catch (Exception ex) { Toast("Could not open the website: " + ex.Message); } }

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
            FetchInbox(false); string[] tn = { "COMMUNITY", "ARTISTS" + (msgUnread > 0 ? " (" + msgUnread + ")" : ""), "PROFILE" }; int[] ti = { 4, 7, 6 }; float[] tw = { 134, 150, 104 };
            for (int i = 2; i >= 0; i--) { int t = ti[i]; if (t == tab) continue; Btn(g, Rc(rx - tw[i], y + 4, tw[i], h - 8), tn[i], t == 6 ? 2 : 1, () => { if (t == 6) profScroll = 0; if (t == 7) OpenArtists(); else tab = t; }, 13); rx -= tw[i] + 6; }
        }
    }

    void PaintCommunity(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH, Hb = H - (previewPath.Length > 0 ? 62 : 0); if (!projectsLoaded)   /* Hb: the bottom of the page when the preview bar is showing under it */ { projectsLoaded = true; ProjectLibrary.Load(); ProjectLibrary.Discover(); }
        bool rail = false; float sw = 0, rw = rail ? 290 : 0; PaintLogoBackdrop(g, Rc(sw, top, W - sw - rw, H - top));
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
            int k = i; var r = Rc(tx, ty, 134, 34); bool on = commFilter == i; if (on) { FillRR(g, TM.C(120, 70, 40, 160), r, 10); } else if (Hot(r)) FillRR(g, TM.C(70, 70, 40, 130), r, 10);
            Txt(g, CommTabs[i], 13, on ? Ink : DimC, r, StringAlignment.Center, on); if (on) using (var pen = new Pen(NeonCyan, 2f)) g.DrawLine(pen, r.X + 12, r.Bottom - 1, r.Right - 12, r.Bottom - 1); Reg(r, () => { commFilter = k; commScroll = 0; FeedRefetch(); }); tx += 140;
        }
        // ---- SAVED SONGS and SHARE: both are on the community page of the website (you sign in there); a song saved there is for listening, never a file on this PC ----
        { var shB = Rc(cx1 - 4 - 150, ty, 150, 34); var svB = Rc(shB.X - 8 - 178, ty, 178, 34); if (svB.X > tx + 20)
          { Btn(g, svB, "SAVED SONGS", 1, () => { commFilter = 4; FeedRefetch(); }, 13); Hint2(svB, "The songs you saved with SAVE SONG");
            Btn(g, shB, "SHARE", 2, () => OpenCommunitySite("/community"), 13); Hint2(shB, "Share a song with the community. Opens the community page of the website, where you sign in and upload it"); } }
        PaintFeed(g, W, Hb, ty, cx0, cw);
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
    // the down arrow on a project card: every tag of the project in a list (EDIT TAGS opens the tag window)
    void ShowAllTags(RectangleF at, ProjectEntry e)
    {
        var m = Win.NewMenu(); m.Items.Add(e.Name + ": " + e.Tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length + " tags").Enabled = false; m.Items.Add(new ToolStripSeparator());
        foreach (var tg in e.Tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) m.Items.Add("#" + tg).Enabled = false;
        m.Items.Add(new ToolStripSeparator()); var ee = e; m.Items.Add("Edit the tags...").Click += (a, b) => TagEntry(ee); m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    void TagEntry(ProjectEntry e)
    {
        string t = StudioDialogs.Tags(this, e.Tags); if (t == null) return;
        e.Tags = ProjectLibrary.CleanTags(t); ProjectLibrary.Save(); Toast(e.Tags.Length == 0 ? "Tags removed" : "Tags saved: " + e.Tags);
    }
}
