using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

// EPs and albums: several of your projects put together as one release. They go on your profile with the projects, and the Community tab can filter by ALBUMS and EPS.
sealed class Release { public string Id = "", Kind = "ep", Title = ""; public List<string> Pids = new List<string>(); }

static class ReleaseStore
{
    static readonly object lk = new object(); public static readonly List<Release> All = new List<Release>(); static bool loaded;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "releases.txt"); } }
    static string Clean(string s) { return (s ?? "").Replace("|", " ").Replace(",", " ").Replace("\r", " ").Replace("\n", " ").Trim(); }
    public static void Load()
    {
        lock (lk)
        {
            if (loaded) return; loaded = true; All.Clear();
            try { if (File.Exists(FileP)) foreach (var line in File.ReadAllLines(FileP)) { var f = line.Split('|'); if (f.Length < 4 || f[0].Length == 0) continue; var r = new Release { Id = f[0], Kind = f[1] == "album" ? "album" : "ep", Title = f[2] }; foreach (var p in f[3].Split(',')) if (p.Length > 0) r.Pids.Add(p); if (r.Title.Length > 0) All.Add(r); } } catch { }
        }
    }
    public static void Save()
    {
        Load();   // (reads once) never write before reading
        lock (lk) { try { Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); foreach (var r in All) sb.Append(r.Id).Append('|').Append(r.Kind).Append('|').Append(Clean(r.Title)).Append('|').Append(string.Join(",", r.Pids.ToArray())).Append('\n'); string tmp = FileP + ".tmp"; File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8); if (File.Exists(FileP)) File.Delete(FileP); File.Move(tmp, FileP); } catch { } }
    }
    public static Release Add(string kind, string title, List<string> pids)
    {
        Load(); var r = new Release { Id = "r" + Guid.NewGuid().ToString("N").Substring(0, 10), Kind = kind == "album" ? "album" : "ep", Title = Clean(title), Pids = new List<string>(pids) };
        lock (lk) All.Add(r); Save(); return r;
    }
    public static void Remove(string id) { Load(); lock (lk) All.RemoveAll(r => r.Id == id); Save(); }
    public static void ResetForTest() { lock (lk) { All.Clear(); loaded = false; try { File.Delete(FileP); } catch { } } }
    // "collections":[...] for the profile: only the projects that are published count, and a release needs two of them
    public static string Json(IEnumerable<ProjectEntry> published)
    {
        Load(); var have = new HashSet<string>(); foreach (var e in published) if (e.OnProfile && !e.Missing) have.Add(CommunitySongs.PidOf(e.Path)); var sb = new StringBuilder("["); int n = 0;
        lock (lk) foreach (var r in All)
        {
            var ps = r.Pids.FindAll(p => have.Contains(p)); if (ps.Count < 2 || n >= 40) continue; if (n++ > 0) sb.Append(',');
            sb.Append("{\"id\":" + ProfApi.J(r.Id) + ",\"t\":" + ProfApi.J(r.Title) + ",\"k\":" + ProfApi.J(r.Kind) + ",\"p\":[" + string.Join(",", ps.ConvertAll(p => ProfApi.J(p)).ToArray()) + "]}");
        }
        return sb.Append(']').ToString();
    }
    // a sentence when the choice is not allowed, null when it is fine
    public static string Check(string kind, string title, int n)
    {
        if ((title ?? "").Trim().Length < 2) return "Give it a name with at least 2 letters.";
        if (n < 2) return "Pick at least 2 projects: an " + (kind == "album" ? "album" : "EP") + " is several tracks together.";
        if (n > 20) return "An EP or an album holds up to 20 songs: pick fewer.";
        return null;
    }
}

partial class StudioForm
{
    internal Func<List<ProjectEntry>, Release> releaseHook;   // a test answers for itself
    // the RELEASES button on My Projects: make an EP or album, or delete one you made
    void ReleasesMenu(RectangleF at)
    {
        ReleaseStore.Load(); var m = Win.NewMenu();
        m.Items.Add("Make an EP or album from my projects...").Click += (a, b) => NewReleaseClick();
        if (ReleaseStore.All.Count > 0) { m.Items.Add(new ToolStripSeparator()); foreach (var r in ReleaseStore.All.ToArray()) { var rr = r; int n = rr.Pids.Count; m.Items.Add((rr.Kind == "album" ? "ALBUM  " : "EP  ") + rr.Title + "  (" + n + " tracks)  -  click to delete").Click += (a, b) => { if (AskYes("Delete the " + (rr.Kind == "album" ? "album" : "EP") + " \"" + rr.Title + "\"? (Your projects stay as they are.)", "Delete")) { ReleaseStore.Remove(rr.Id); QueueRepublish(); Toast("Deleted."); } }; } }
        m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
    }
    internal void NewReleaseClick()
    {
        var all = ProjectLibrary.AllByEdited().FindAll(x => !x.Missing); if (all.Count < 2) { Toast("Make and save at least 2 projects first."); return; }
        if (releaseHook == null) { OpenReleaseBuilder(all); return; }
        var r = releaseHook(all); if (r == null) return; FinishRelease(r, all);
    }
    // ---- the neon box to make an EP or an album: a name, EP or ALBUM, then as many of your projects as you like (tick them, move them into the order you want) ----
    bool relOpen, relAlbum; TextBox relNameBox; List<ProjectEntry> relOrder = new List<ProjectEntry>(), relAll = new List<ProjectEntry>(); readonly HashSet<string> relPick = new HashSet<string>(StringComparer.OrdinalIgnoreCase); float relScroll; string relErr = ""; int relAt; Rectangle relBoxAt;
    void OpenReleaseBuilder(List<ProjectEntry> all)
    {
        relAll = all; relOrder = new List<ProjectEntry>(all); relPick.Clear(); relAlbum = false; relErr = ""; relScroll = 0; relAt = Environment.TickCount; menuOpen = false;
        if (relNameBox == null)
        {
            relNameBox = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 13f), MaxLength = 50, Visible = false }; Win.NeonBox(relNameBox); Controls.Add(relNameBox); Win.DarkControl(relNameBox);
            relNameBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ReleaseCreate(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; relOpen = false; Invalidate(); } };
        }
        relNameBox.Text = ""; relOpen = true; Invalidate();
    }
    void ReleaseCreate()
    {
        var pids = new List<string>(); foreach (var e in relOrder) if (relPick.Contains(e.Path)) pids.Add(CommunitySongs.PidOf(e.Path));
        string kind = relAlbum ? "album" : "ep"; string bad = ReleaseStore.Check(kind, relNameBox.Text, pids.Count); if (bad != null) { relErr = bad; return; }
        relOpen = false; FinishRelease(new Release { Kind = kind, Title = relNameBox.Text.Trim(), Pids = pids }, relAll);
    }
    void PaintReleaseBuilder(Graphics g)
    {
        if (!relOpen) { if (relNameBox != null && relNameBox.Visible) relNameBox.Visible = false; return; }
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); lastInput = DateTime.Now;
        using (var b = new SolidBrush(TM.C(165, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => { });
        int age = unchecked(Environment.TickCount - relAt); float k = Math.Min(1f, age / 260f), ease = 1 - (1 - k) * (1 - k), grow = 0.94f + 0.06f * ease; double pulse = 0.5 + 0.5 * Math.Sin(age / 320.0);
        float bw = Math.Min(800, W - 40), bh = Math.Min(H - 40, 680); var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2, bw * grow, bh * grow);
        for (int i = 5; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((16 + 22 * pulse) / i * 2.2f) + 5, NeonPink), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 22 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 22)) { using (var br = new LinearGradientBrush(box, TM.C(250, 26, 12, 58), TM.C(250, 9, 4, 24), 90f)) g.FillPath(br, p); using (var pen = new Pen(NeonCyan, 2.2f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(110, NeonPink), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -9, -9), 16)) g.DrawPath(pen, p); AnimRing(g, box, NeonCyan); Reg(box, () => { });
        Txt(g, "MAKE AN EP OR ALBUM", 24, Ink, Rc(box.X + 28, box.Y + 16, box.Width - 110, 34), StringAlignment.Near, true);
        Txt(g, "Pick up to 20 songs: tick them, then move them into the order you want.", 13, DimC, Rc(box.X + 28, box.Y + 52, box.Width - 56, 22), StringAlignment.Near, false);
        var cl = Rc(box.Right - 46, box.Y + 14, 30, 28); Btn(g, cl, "x", 3, () => { relOpen = false; }, 12); Hint2(cl, "Close without making anything");
        float y = box.Y + 86; Txt(g, "NAME", 12, DimC, Rc(box.X + 28, y, 100, 18), StringAlignment.Near, true);
        var nb = new Rectangle((int)(box.X + 28), (int)(y + 20), (int)(box.Width - 56), 32); if (relBoxAt != nb) { relBoxAt = nb; relNameBox.Bounds = nb; } if (!relNameBox.Visible) { relNameBox.Visible = true; relNameBox.BringToFront(); relNameBox.Focus(); }
        y += 64;
        var ec = Rc(box.X + 28, y, 110, 34); var ac = Rc(ec.Right + 10, y, 130, 34); Chip(g, ec, "EP", !relAlbum, () => relAlbum = false, 1); Hint2(ec, "An EP: a short release"); Chip(g, ac, "ALBUM", relAlbum, () => relAlbum = true, 1); Hint2(ac, "An album: a full release (any number of songs)");
        Txt(g, relPick.Count + (relPick.Count == 1 ? " SONG PICKED" : " SONGS PICKED"), 13, relPick.Count >= 2 ? NeonCyan : DimC, Rc(ac.Right + 20, y, box.Right - ac.Right - 48, 34), StringAlignment.Far, true);
        y += 46; Txt(g, "YOUR PROJECTS", 12, DimC, Rc(box.X + 28, y, 300, 18), StringAlignment.Near, true); y += 22;
        var lr = Rc(box.X + 28, y, box.Width - 56, box.Bottom - y - 96); float rowH = 54, total = relOrder.Count * rowH, maxS = Math.Max(0, total - lr.Height); relScroll = Math.Max(0, Math.Min(maxS, relScroll));
        FillRR(g, TM.C(120, 6, 3, 16), lr, 12); StrokeRR(g, Color.FromArgb(90, NeonViolet), lr, 12, 1);
        regs.Add(new HitReg { R = lr, OnWheel = d => relScroll = Math.Max(0, Math.Min(maxS, relScroll - d * 40)) });
        var num = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); { int n = 0; foreach (var e in relOrder) if (relPick.Contains(e.Path)) num[e.Path] = ++n; }
        { var gs = g.Save(); g.SetClip(lr, CombineMode.Intersect);
          for (int i = 0; i < relOrder.Count; i++)
          {
              var e = relOrder[i]; var row = Rc(lr.X + 6, lr.Y + 4 + i * rowH - relScroll, lr.Width - 12 - (maxS > 0 ? 22 : 0), rowH - 6); if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue; bool on = relPick.Contains(e.Path), hot = Hot(row); int idx = i; string path = e.Path;
              FillRR(g, on ? TM.C(215, 62, 34, 130) : TM.C(170, 28, 15, 56), row, 10); StrokeRR(g, on ? NeonCyan : (hot ? Color.FromArgb(200, NeonViolet) : Color.FromArgb(80, NeonViolet)), row, 10, on ? 1.4f : 1f);
              Reg(row, () => { if (!relPick.Add(path)) relPick.Remove(path); relErr = ""; });
              var tick = Rc(row.X + 10, row.Y + row.Height / 2 - 11, 22, 22); FillRR(g, TM.C(255, 8, 4, 20), tick, 6); StrokeRR(g, on ? NeonCyan : Color.FromArgb(150, NeonViolet), tick, 6, 1.4f);
              if (on) { var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(NeonCyan, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(pen, new[] { new PointF(tick.X + 5, tick.Y + 11), new PointF(tick.X + 9.5f, tick.Y + 16), new PointF(tick.X + 17, tick.Y + 6) }); g.SmoothingMode = sm; }
              int nn; Txt(g, num.TryGetValue(e.Path, out nn) ? nn.ToString() : "", 17, NeonCyan, Rc(tick.Right + 6, row.Y, 34, row.Height), StringAlignment.Center, true);
              var cov = Rc(tick.Right + 44, row.Y + 5, row.Height - 10, row.Height - 10); DrawCover(g, cov, e);
              float tx = cov.Right + 10, bw2 = 118; Txt(g, e.Name, 14, Ink, Rc(tx, row.Y + 5, row.Right - tx - bw2 - 10, 22), StringAlignment.Near, true);
              Txt(g, (e.OnProfile ? "PUBLISHED" : "PRIVATE") + "   " + e.Tracks + " tracks", 10, e.OnProfile ? NeonCyan : DimC, Rc(tx, row.Y + 27, row.Right - tx - bw2 - 10, 16), StringAlignment.Near, false);
              var up = Rc(row.Right - bw2 - 4, row.Y + 10, 54, 26); var dn = Rc(up.Right + 6, row.Y + 10, 54, 26);
              Btn(g, up, "UP", 1, () => { if (idx > 0) { var t = relOrder[idx]; relOrder[idx] = relOrder[idx - 1]; relOrder[idx - 1] = t; } }, 11); Btn(g, dn, "DOWN", 1, () => { if (idx < relOrder.Count - 1) { var t = relOrder[idx]; relOrder[idx] = relOrder[idx + 1]; relOrder[idx + 1] = t; } }, 11);
          }
          g.Restore(gs); }
        if (maxS > 0) VScroll(g, Rc(lr.Right - 22, lr.Y + 4, 18, lr.Height - 8), total, lr.Height, () => relScroll, v => relScroll = v);
        if (relErr.Length > 0) Txt(g, relErr, 13, Color.FromArgb(255, 255, 120, 140), Rc(box.X + 28, box.Bottom - 84, box.Width - 56, 22), StringAlignment.Near, true);
        var cb = Rc(box.Right - 28 - 190, box.Bottom - 56, 190, 38); Btn(g, cb, "CREATE", 2, () => ReleaseCreate(), 15); Hint2(cb, "Make the " + (relAlbum ? "album" : "EP") + " from the songs you ticked, in this order");
        var xb = Rc(cb.X - 12 - 130, cb.Y, 130, 38); Btn(g, xb, "CANCEL", 1, () => { relOpen = false; }, 14);
    }
    void FinishRelease(Release r, List<ProjectEntry> all)
    {
        var made = ReleaseStore.Add(r.Kind, r.Title, r.Pids);
        var priv = new List<ProjectEntry>(); foreach (var e in all) if (!e.OnProfile && made.Pids.Contains(CommunitySongs.PidOf(e.Path))) priv.Add(e);
        Toast("Made the " + (made.Kind == "album" ? "album" : "EP") + " \"" + made.Title + "\" with " + made.Pids.Count + " tracks.");
        if (priv.Count > 0 && AskYes((made.Kind == "album" ? "The album" : "The EP") + " \"" + made.Title + "\" has " + priv.Count + (priv.Count == 1 ? " project that is" : " projects that are") + " private. Publish " + (priv.Count == 1 ? "it" : "them") + " now, so the release shows on your profile and the Community tab?" + ProfNote() + "\n\nThe songs go on the community page of the website. Only publish music you made or have the right to share.", "Publish the release"))
        { EnsureSongMarks(); foreach (var e in priv) ProjectLibrary.SetOnProfile(e, true); if (!PublishProfileToo()) QueueRepublish(); }
        else QueueRepublish();
    }
}
