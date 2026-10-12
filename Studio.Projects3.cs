using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// My Projects: search, sort and filter (favourites, type) and the Recycling Bin (a removed project waits 30 days, then goes for good).
partial class StudioForm
{
    string projSearch = "", projType = ""; int projSort; bool projFav, projBin, projSearchOn; float binScroll; RectangleF projSearchR;   // the search box only takes typing after you click it
    Func<string, bool> binConfirmHook;   // a test answers for itself; the real program asks with a window before anything is deleted for good
    static readonly string[] SortNames = { "Newest edited", "Recently opened", "Name A to Z", "Name Z to A", "Most tracks", "Longest", "Oldest edited" };
    static readonly string[] TypeNames = { "Song", "Beat", "Vocals" };
    bool FiltersOn { get { return projSearch.Trim().Length > 0 || projFav || projType.Length > 0; } }

    // the projects that pass the search and the filters, in the chosen order (missing files always sink to the end)
    List<ProjectEntry> VisibleProjects()
    {
        var all = ProjectLibrary.AllByEdited(); var l = new List<ProjectEntry>(); var words = projSearch.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var e in all)
        {
            if (projFav && !e.Fav) continue;
            if (projType.Length > 0 || words.Length > 0) ProjectLibrary.EnsureKind(e);
            if (projType.Length > 0 && !string.Equals(e.Kind, projType, StringComparison.OrdinalIgnoreCase)) continue;
            if (words.Length > 0)
            {
                string hay = e.Name + " " + e.Tags + " " + e.Desc + " " + e.Kind + (e.Fav ? " favourite" : ""); bool ok = true;
                foreach (var w in words) if (hay.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0) { ok = false; break; }
                if (!ok) continue;
            }
            l.Add(e);
        }
        l.Sort((a, b) =>
        {
            int m = a.Missing.CompareTo(b.Missing); if (m != 0) return m; int c = 0;
            switch (projSort)
            {
                case 1: c = b.Opened.CompareTo(a.Opened); break;
                case 2: c = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); break;
                case 3: c = string.Compare(b.Name, a.Name, StringComparison.CurrentCultureIgnoreCase); break;
                case 4: c = b.Tracks.CompareTo(a.Tracks); break;
                case 5: c = b.Seconds.CompareTo(a.Seconds); break;
                case 6: c = a.Updated.CompareTo(b.Updated); break;
                default: c = b.Updated.CompareTo(a.Updated); break;
            }
            return c != 0 ? c : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        });
        return l;
    }
    void ClearProjectFilters() { projSearch = ""; projFav = false; projType = ""; projScroll = 0; }

    // typing goes straight into the search box while the projects page is open (Enter or Esc stops it, so Space can play again)
    bool ProjSearchActive { get { return tab == 3 && !projBin && projSearchOn && !menuOpen && !busy && !cpOpen && !stylesOpen && !browserOpen && !pickOpen && !autoOpen && !gainOpen && (editBox == null || !editBox.Visible); } }
    bool ProjSearchKey(char c)
    {
        if (!ProjSearchActive) return false;
        if (c == '\b') { if (projSearch.Length > 0) projSearch = projSearch.Substring(0, projSearch.Length - 1); }
        else if (!char.IsControl(c) && projSearch.Length < 40) projSearch += c; else return true;
        projScroll = 0; return true;
    }
    bool ProjSearchCmd(Keys k)
    {   // true = handled here; false = let the key through to the normal handling
        if (!ProjSearchActive) return false;
        if (k == Keys.Escape) { if (projSearch.Length > 0) projSearch = ""; else projSearchOn = false; projScroll = 0; return true; }
        if (k == Keys.Enter || k == Keys.Return) { projSearchOn = false; return true; }
        return false;
    }
    bool ProjSearchTakesKey(Keys k) { return ProjSearchActive && (k & Keys.Control) == 0 && (k & Keys.Alt) == 0 && !((k & Keys.KeyCode) >= Keys.F1 && (k & Keys.KeyCode) <= Keys.F24); }

    void Hint2(RectangleF r, string text) { if (Hot(r)) { bubble = text; bubblePt = MousePos; } }
    HitReg Chip(Graphics g, RectangleF r, string text, bool on, Action a, string hint, float px = 12) { var h = Btn(g, r, text, on ? 4 : 0, a, px); Hint2(r, hint); return h; }

    // the bar above the cards: search, bin button, sort, type and the favourites chip. Returns the y where the cards start.
    float PaintProjectsBar(Graphics g, float W, float top, int total, int shown)
    {
        float y = top + 10, h = 34; var bin = Rc(W - 24 - 190, y, 190, h); int bn = ProjectLibrary.Bin.Count;
        var sb = Rc(24, y, Math.Max(180, Math.Min(420, bin.X - 24 - 20)), h); FillRR(g, TM.C(235, 14, 7, 32), sb, 9); Win.DrawNeonBox(g, sb, projSearchOn);
        double tm = Environment.TickCount / 1000.0; bool blink = ((int)(tm * 2) % 2) == 0 && projSearchOn;
        Txt(g, projSearch.Length == 0 ? (blink ? "|  " : "   ") + "SEARCH PROJECTS" : projSearch + (blink ? "|" : ""), 13, projSearch.Length == 0 ? DimC : Ink, Rc(sb.X + 14, sb.Y, sb.Width - 54, sb.Height), StringAlignment.Near, projSearch.Length > 0);
        projSearchR = sb; Reg(sb, () => projSearchOn = true); Hint2(sb, "Search your projects by name, tag or type (song, beat, vocals). Just start typing.");
        if (projSearch.Length > 0) { var x = Rc(sb.Right - 34, sb.Y + 5, 24, 24); Btn(g, x, "x", 3, () => { projSearch = ""; projScroll = 0; }, 12); Hint2(x, "Clear the search"); }
        float cx = sb.Right + 16, cw = bin.X - 10 - cx; if (cw > 110) Txt(g, (FiltersOn ? shown + " OF " + total : total.ToString()) + (total == 1 ? " PROJECT" : " PROJECTS"), 12, DimC, Rc(cx, y, cw, h), StringAlignment.Near, true);
        Btn(g, bin, "RECYCLING BIN" + (bn > 0 ? "  (" + bn + ")" : ""), bn > 0 ? 1 : 0, () => { ProjectLibrary.PurgeBin(DateTime.Now); projBin = true; binScroll = 0; }, 13); Hint2(bin, "Removed projects wait here for 30 days. Restore one, or delete it for good.");
        y += h + 8; float x0 = 24;
        var sortB = Rc(x0, y, 232, 32); Btn(g, sortB, "SORT: " + SortNames[projSort].ToUpper(), 0, () => SortMenu(sortB), 12); Hint2(sortB, "Choose how the projects are ordered"); x0 = sortB.Right + 10;
        var typeB = Rc(x0, y, 160, 32); Btn(g, typeB, "TYPE: " + (projType.Length == 0 ? "ALL" : projType.ToUpper()), projType.Length > 0 ? 4 : 0, () => TypeMenu(typeB), 12); Hint2(typeB, "Show only songs, beats or vocal projects"); x0 = typeB.Right + 10;
        var favB = Rc(x0, y, 140, 32); Chip(g, favB, "FAVOURITES", projFav, () => { projFav = !projFav; projScroll = 0; }, "Show only the projects you starred"); x0 = favB.Right + 10;
        if (FiltersOn) { var clr = Rc(x0, y, 100, 32); Btn(g, clr, "CLEAR", 3, () => ClearProjectFilters(), 12); Hint2(clr, "Clear the search and every filter"); }
        { var pubAll = Rc(W - 24 - 200, y, 200, 32); if (pubAll.X > x0 + 10) { Btn(g, pubAll, "PUBLISH ALL", 2, () => PublishAllClick(), 12); Hint2(pubAll, "Publish every project that is not published yet to your profile and the community (it asks first)"); } }
        return y + 32 + 18;
    }
    void SortMenu(RectangleF r)
    {
        var m = Win.NewMenu(); for (int i = 0; i < SortNames.Length; i++) { int k = i; var it = new ToolStripMenuItem(SortNames[i]) { Checked = i == projSort }; it.Click += (a, b) => { projSort = k; projScroll = 0; }; m.Items.Add(it); }
        m.Show(this, new Point((int)r.X, (int)r.Bottom + 2));
    }
    void TypeMenu(RectangleF r)
    {
        var m = Win.NewMenu(); var all = new ToolStripMenuItem("All projects") { Checked = projType.Length == 0 }; all.Click += (a, b) => { projType = ""; projScroll = 0; }; m.Items.Add(all);
        foreach (var t in TypeNames) { string tt = t; var it = new ToolStripMenuItem(t + (t == "Song" ? " (vocals with a beat)" : (t == "Beat" ? " (beats and instruments)" : " (vocals only)"))) { Checked = string.Equals(projType, t, StringComparison.OrdinalIgnoreCase) }; it.Click += (a, b) => { projType = tt; projScroll = 0; }; m.Items.Add(it); }
        m.Show(this, new Point((int)r.X, (int)r.Bottom + 2));
    }
    // a five-pointed star: filled when it is a favourite
    void DrawStar(Graphics g, RectangleF r, bool on, bool hot)
    {
        var pts = new PointF[10]; float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 + 0.5f, R = Math.Min(r.Width, r.Height) * 0.40f, rr = R * 0.45f;
        for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float rad = i % 2 == 0 ? R : rr; pts[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad); }
        var gold = Color.FromArgb(255, 255, 205, 60);
        if (on) { using (var b = new SolidBrush(gold)) g.FillPolygon(b, pts); }
        using (var pen = new Pen(on ? gold : (hot ? Ink : TM.C(200, 190, 170, 235)), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pts);
    }
    void ToggleFavourite(ProjectEntry e) { ProjectLibrary.SetFlags(e.Path, !e.Fav, null); Toast(e.Fav ? "Added to your favourites." : "Removed from your favourites."); }

    // ---------- the Recycling Bin page ----------
    bool BinAsk(string text)
    {
        if (binConfirmHook != null) return binConfirmHook(text);
        return MessageBox.Show(this, text, "Recycling Bin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }
    void BinRestore(ProjectLibrary.BinItem b)
    {
        string err; string p = ProjectLibrary.Restore(b, out err);
        Toast(p == null ? "Could not restore it: " + err : "Restored \"" + b.Name + "\" to My Projects.");
    }
    void BinDelete(ProjectLibrary.BinItem b)
    {
        if (!BinAsk("Delete \"" + b.Name + "\" for good?\n\nThis cannot be undone.")) return; ProjectLibrary.DeleteForever(b); Toast("Deleted for good.");
    }
    void BinEmpty()
    {
        int n = ProjectLibrary.Bin.Count; if (n == 0) return; if (!BinAsk("Delete everything in the Recycling Bin for good (" + n + " project" + (n == 1 ? "" : "s") + ")?\n\nThis cannot be undone.")) return;
        ProjectLibrary.EmptyBin(); Toast("The Recycling Bin is empty.");
    }
    void PaintBin(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH; var items = new List<ProjectLibrary.BinItem>(ProjectLibrary.Bin); items.Sort((a, b) => b.Deleted.CompareTo(a.Deleted)); DateTime now = DateTime.Now;
        float y = top + 10; var back = Rc(24, y, 150, 34); Btn(g, back, "PROJECTS", 2, () => { projBin = false; projScroll = 0; }, 14); Hint2(back, "Back to your projects");
        Txt(g, "RECYCLING BIN", 20, Ink, Rc(190, y, 300, 34), StringAlignment.Near, true);
        var emp = Rc(W - 24 - 190, y, 190, 34); if (items.Count > 0) { Btn(g, emp, "EMPTY THE BIN", 3, () => BinEmpty(), 13); Hint2(emp, "Delete everything in the bin for good (it asks first)"); }
        TxtWrap(g, "Removed projects stay here for " + ProjectLibrary.BinDays + " days, then they are deleted for good. RESTORE puts one back in My Projects; DELETE FOREVER removes it now. Only the project file waits here: your audio files are never touched.", 12, DimC, Rc(24, y + 44, W - 48, 36));
        float y0 = y + 92, rh = 88, gap = 10; var area = Rc(0, y0 - 6, W, H - y0 + 6); float room = previewPath.Length > 0 ? 70 : 20; float maxScroll = Math.Max(0, y0 + items.Count * (rh + gap) - H + room);
        binScroll = Math.Max(0, Math.Min(maxScroll, binScroll)); Reg(area, () => { }).OnWheel = d => { binScroll = Math.Max(0, Math.Min(maxScroll, binScroll - d * 70)); };
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        for (int i = 0; i < items.Count; i++)
        {
            var b = items[i]; var row = Rc(24, y0 + i * (rh + gap) - binScroll, W - 48 - 56, rh); if (row.Bottom < area.Y || row.Y > H) continue;
            FillRR(g, TM.C(205, 22, 12, 42), row, 14); Win.DrawNeonBox(g, row, Hot(row));
            var pic = Rc(row.X + 10, row.Y + 10, rh - 20, rh - 20); DrawCover(g, pic, new ProjectEntry { Cover = b.Cover ?? "", Name = b.Name }); Win.DrawNeonBox(g, pic, false);
            float bx = row.Right - 12, rw = 110, dw = 156; var del = Rc(bx - dw, row.Y + (rh - 34) / 2, dw, 34); var res = Rc(del.X - 10 - rw, del.Y, rw, 34); float tw = res.X - (pic.Right + 14) - 12;
            Txt(g, b.Name, 15, Ink, Rc(pic.Right + 14, row.Y + 12, tw, 24), StringAlignment.Near, true);
            string meta = (b.Kind != null && b.Kind.Length > 0 ? b.Kind.ToUpper() + "   " : "") + b.Tracks + " tracks" + (b.Bpm > 0 ? "   " + b.Bpm.ToString("0") + " BPM" : "") + (b.Seconds > 1 ? "   " + ((int)(b.Seconds / 60)) + ":" + ((int)(b.Seconds % 60)).ToString("00") : "");
            Txt(g, meta, 11, DimC, Rc(pic.Right + 14, row.Y + 38, tw, 18), StringAlignment.Near, false);
            int left = ProjectLibrary.DaysLeft(b, now); int ago = (int)Math.Floor((now - b.Deleted).TotalDays);
            Txt(g, "REMOVED " + (ago <= 0 ? "TODAY" : ago + (ago == 1 ? " DAY AGO" : " DAYS AGO")) + "  -  " + (left <= 0 ? "DELETED FOR GOOD TOMORROW" : left + (left == 1 ? " DAY LEFT" : " DAYS LEFT")), 11, left <= 3 ? Color.FromArgb(255, 150, 150) : Color.FromArgb(150, 235, 200), Rc(pic.Right + 14, row.Y + 58, tw, 18), StringAlignment.Near, true);
            var bi = b; Btn(g, res, "RESTORE", 2, () => BinRestore(bi), 13); Hint2(res, "Put this project back in My Projects"); Btn(g, del, "DELETE FOREVER", 3, () => BinDelete(bi), 12); Hint2(del, "Delete this project for good (it asks first)");
        }
        g.Restore(gs);
        { float barH = H - (y0 - 6) - (previewPath.Length > 0 ? 56 : 8); VScroll(g, Rc(W - 54, y0 - 6, 42, barH), barH + maxScroll, barH, () => binScroll, v => binScroll = v); }
        if (items.Count == 0) { Txt(g, "THE RECYCLING BIN IS EMPTY", 17, Ink, Rc(0, y0 + 60, W, 30), StringAlignment.Center, true); Txt(g, "Projects you remove with the X (and tick delete the file) will wait here.", 12, DimC, Rc(0, y0 + 96, W, 22), StringAlignment.Center, false); }
    }
}
