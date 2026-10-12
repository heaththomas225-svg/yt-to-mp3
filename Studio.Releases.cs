using System;
using System.Collections.Generic;
using System.Drawing;
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
        lock (lk) { try { Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); foreach (var r in All) sb.Append(r.Id).Append('|').Append(r.Kind).Append('|').Append(Clean(r.Title)).Append('|').Append(string.Join(",", r.Pids.ToArray())).Append('\n'); string tmp = FileP + ".tmp"; File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8); if (File.Exists(FileP)) File.Delete(FileP); File.Move(tmp, FileP); } catch { } }
    }
    public static Release Add(string kind, string title, List<string> pids)
    {
        Load(); var r = new Release { Id = "r" + Guid.NewGuid().ToString("N").Substring(0, 10), Kind = kind == "album" ? "album" : "ep", Title = Clean(title), Pids = new List<string>(pids) };
        lock (lk) All.Add(r); Save(); return r;
    }
    public static void Remove(string id) { Load(); lock (lk) All.RemoveAll(r => r.Id == id); Save(); }
    public static void ResetForTest() { lock (lk) { All.Clear(); loaded = false; } }
    // "collections":[...] for the profile: only the projects that are published count, and a release needs two of them
    public static string Json(IEnumerable<ProjectEntry> published)
    {
        Load(); var have = new HashSet<string>(); foreach (var e in published) if (e.OnProfile && !e.Missing) have.Add(CommunitySongs.PidOf(e.Path)); var sb = new StringBuilder("["); int n = 0;
        lock (lk) foreach (var r in All)
        {
            var ps = r.Pids.FindAll(p => have.Contains(p)); if (ps.Count < 2 || n >= 20) continue; if (n++ > 0) sb.Append(',');
            sb.Append("{\"id\":" + ProfApi.J(r.Id) + ",\"t\":" + ProfApi.J(r.Title) + ",\"k\":" + ProfApi.J(r.Kind) + ",\"p\":[" + string.Join(",", ps.ConvertAll(p => ProfApi.J(p)).ToArray()) + "]}");
        }
        return sb.Append(']').ToString();
    }
    // a sentence when the choice is not allowed, null when it is fine
    public static string Check(string kind, string title, int n)
    {
        if ((title ?? "").Trim().Length < 2) return "Give it a name with at least 2 letters.";
        if (n < 2) return "Pick at least 2 projects: an " + (kind == "album" ? "album" : "EP") + " is several tracks together.";
        if (kind == "ep" && n > 6) return "An EP has 6 tracks at most. Pick fewer, or make it an ALBUM.";
        if (n > 30) return "An album can hold 30 tracks at most.";
        return null;
    }
}

static class ReleaseDialog
{
    // the window to make an EP or an album: a name, EP or ALBUM, and your projects (tick the ones that belong, move them up or down for the order)
    public static Release Show(IWin32Window owner, List<ProjectEntry> projects)
    {
        Release result = null; var f = StudioDialogs.Make("New EP or album", 560, 600); int y = 16;
        f.Controls.Add(new Label { Text = "Name", Left = 16, Top = y + 4, Width = 70, Height = 22 }); var nm = new TextBox { Left = 90, Top = y, Width = 440, MaxLength = 50, BackColor = TM.C(255, 34, 16, 62), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(nm); f.Controls.Add(nm); y += 40;
        var ep = new DarkCheck { Text = "EP  (2 to 6 tracks)", Left = 90, Top = y, Width = 220, Height = 26, Checked = true }; var al = new DarkCheck { Text = "ALBUM", Left = 320, Top = y, Width = 200, Height = 26 };
        ep.CheckedChanged += (s, e) => { if (ep.Checked) al.Checked = false; else if (!al.Checked) ep.Checked = true; }; al.CheckedChanged += (s, e) => { if (al.Checked) ep.Checked = false; else if (!ep.Checked) al.Checked = true; }; f.Controls.Add(ep); f.Controls.Add(al); y += 36;
        f.Controls.Add(new Label { Text = "Tick the projects that belong to it (published ones show on the Community tab):", Left = 16, Top = y, Width = 520, Height = 22 }); y += 26;
        var list = new CheckedListBox { Left = 16, Top = y, Width = 514, Height = 270, CheckOnClick = true, BackColor = TM.C(255, 34, 16, 62), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false }; var pidOf = new List<string>(); foreach (var e in projects) { list.Items.Add(e.Name + (e.OnProfile ? "" : "   (private)")); pidOf.Add(CommunitySongs.PidOf(e.Path)); }
        f.Controls.Add(list); y += 278;
        Action<int> move = d => { int i = list.SelectedIndex, j = i + d; if (i < 0 || j < 0 || j >= list.Items.Count) return; object a = list.Items[i], b = list.Items[j]; bool ca = list.GetItemChecked(i), cb = list.GetItemChecked(j); string pa = pidOf[i]; pidOf[i] = pidOf[j]; pidOf[j] = pa; list.Items[i] = b; list.Items[j] = a; list.SetItemChecked(i, cb); list.SetItemChecked(j, ca); list.SelectedIndex = j; };
        var up = StudioDialogs.B("Move up", 16, y, 110, 30, Color.FromArgb(70, 40, 120)); var dn = StudioDialogs.B("Move down", 134, y, 110, 30, Color.FromArgb(70, 40, 120)); up.Click += (s, e) => move(-1); dn.Click += (s, e) => move(1); f.Controls.Add(up); f.Controls.Add(dn);
        var err = new Label { Left = 16, Top = y + 38, Width = 514, Height = 22, ForeColor = Color.FromArgb(255, 255, 110, 130), Text = "" }; f.Controls.Add(err);
        var ok = StudioDialogs.B("Create", 330, y + 34, 100, 34, Color.FromArgb(118, 52, 212)); var cancel = StudioDialogs.B("Cancel", 440, y + 34, 100, 34, Color.FromArgb(70, 40, 120));
        ok.Click += (s, e) =>
        {
            var pids = new List<string>(); for (int i = 0; i < list.Items.Count; i++) if (list.GetItemChecked(i)) pids.Add(pidOf[i]); string kind = al.Checked ? "album" : "ep"; string bad = ReleaseStore.Check(kind, nm.Text, pids.Count); if (bad != null) { err.Text = bad; return; }
            result = new Release { Kind = kind, Title = nm.Text.Trim(), Pids = pids }; f.Close();
        };
        cancel.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { ok, cancel }); f.ClientSize = new Size(560, y + 34 + 34 + 16);
        f.ShowDialog(owner); return result;
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
        var r = releaseHook != null ? releaseHook(all) : ReleaseDialog.Show(this, all); if (r == null) return; var made = ReleaseStore.Add(r.Kind, r.Title, r.Pids);
        var priv = new List<ProjectEntry>(); foreach (var e in all) if (!e.OnProfile && made.Pids.Contains(CommunitySongs.PidOf(e.Path))) priv.Add(e);
        Toast("Made the " + (made.Kind == "album" ? "album" : "EP") + " \"" + made.Title + "\" with " + made.Pids.Count + " tracks.");
        if (priv.Count > 0 && AskYes((made.Kind == "album" ? "The album" : "The EP") + " \"" + made.Title + "\" has " + priv.Count + (priv.Count == 1 ? " project that is" : " projects that are") + " private. Publish " + (priv.Count == 1 ? "it" : "them") + " now, so the release shows on your profile and the Community tab?" + ProfNote() + "\n\nThe songs go on the community page of the website. Only publish music you made or have the right to share.", "Publish the release"))
        { EnsureSongMarks(); foreach (var e in priv) ProjectLibrary.SetOnProfile(e, true); if (!PublishProfileToo()) QueueRepublish(); }
        else QueueRepublish();
    }
}
