using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// ======================================================================
//  MUSIC HUB page (side panel > Music Hub). Left: every service, with whether you are signed in on it. Middle: the service's own web
//  player in a frameless view with Purphex's buttons (back, forward, reload, your library pages, open in its own window, sign out), or
//  your libraries from the connected APIs, or search results. Bottom: now playing, with play / pause, next, previous, seek, volume and
//  the queue. On every other page a small player stays in the corner. When your project plays, the hub pauses so the two never clash.
// ======================================================================
partial class StudioForm
{
    object hubBObj; HubBrowser HubB { get { return (HubBrowser)hubBObj; } }   // created the first time a service is opened (nothing loads before that)
    internal int hubView;              // 0 home (services, your accounts, search), 1 a service's web player, 2 a library from an API
    internal string hubSvc = "", hubApiId = "", hubSection = "Playlists", hubErr = "", hubQuery = ""; internal bool hubBusy, hubConnOpen;
    internal List<HubItem> hubItems = new List<HubItem>(), hubResults; internal readonly Dictionary<string, int> hubSigned = new Dictionary<string, int>();   // 1 signed in, 2 not
    internal List<HubItem> hubQueue; internal int hubQAt; HubItem hubCur; int hubAutoTries; long hubAutoAt;
    internal string npTitle = "", npArtist = "", npArt = ""; internal bool npPlaying, npHas, npEnded; internal double npPos, npDur; internal float hubVol = 1f; float hubVolSent = -1;
    long hubPollAt, hubSignAt; bool hubPolling, hubEngWas; float hubScroll; TextBox hubBox; RectangleF hubWebR;

    internal static bool HubMuted { get { return StudioEnv.UnderTest || WaveOutPlayer.MuteAll; } }
    HubService CurSvc { get { return HubServices.Get(hubSvc); } }
    void OpenHub() { GoTab(9); if (hubView == 1 && hubSvc.Length == 0) hubView = 0; hubSignAt = 0; }

    // ---- opening a service or a page of it ----
    internal bool EnsureHubBrowser()
    {
        if (hubBObj != null) return true;
        if (HubBrowser.RuntimeVersion() == null) { hubErr = "The music browser needs Microsoft Edge WebView2 Runtime (part of Windows 11). Install it from Microsoft, then open the hub again."; return false; }
        var b = new HubBrowser(this, System.IO.Path.Combine(StudioEnv.DataDir, "hubweb"), HubMuted); b.Changed = () => { lastInput = DateTime.Now; try { BeginInvoke(new Action(HubAfterNav)); } catch { } }; hubBObj = b; return true;
    }
    internal void HubOpenService(string id, string url = null)
    {
        var s = HubServices.Get(id); if (s == null) return; if (!EnsureHubBrowser()) { Toast(hubErr); return; }
        hubSvc = id; hubView = 1; hubErr = ""; HubB.Navigate(url ?? s.Home);
    }
    void HubPlayItem(List<HubItem> list, int i)
    {
        if (list == null || i < 0 || i >= list.Count) return; var it = list[i]; if (!it.Open.StartsWith("https://", StringComparison.Ordinal)) { Toast("That one has no page to open."); return; }
        if (it.Kind == "track") { hubQueue = list.Where(x => x.Kind == "track").ToList(); hubQAt = Math.Max(0, hubQueue.IndexOf(it)); } else { hubQueue = null; }
        hubCur = it; bool stay = hubView != 1; var s = HubServices.Get(it.Service); if (s == null || !EnsureHubBrowser()) { Toast(hubErr.Length > 0 ? hubErr : "Unknown service."); return; }
        hubSvc = s.Id; HubB.Navigate(it.Open); hubAutoTries = it.Kind == "track" ? 6 : 0; hubAutoAt = Environment.TickCount + 2500;
        if (it.Kind != "track" || !stay) hubView = 1;   // a playlist, album or artist opens on its service; a song plays while you keep browsing
        Toast(it.Kind == "track" ? "Playing " + it.Title + " on " + s.Name + "." : "Opening " + it.Title + " on " + s.Name + ".");
    }
    void HubAfterNav() { if (hubBObj == null) return; if (!HubB.Loading) hubSignAt = Math.Min(hubSignAt, Environment.TickCount + 1500); }

    // ---- every frame: where the browser sits, what is playing, the DAW and the hub never playing over each other ----
    void HubTick()
    {
        bool show = tab == 9 && hubView == 1 && hubBObj != null && !OverlayOpen && !hubConnOpen && !busy && hubWebR.Width > 50;
        if (hubBObj != null) HubB.SetBounds(Rectangle.Round(hubWebR), show);
        if (hubBox != null) { bool sb = tab == 9 && !OverlayOpen && !hubConnOpen; if (hubBox.Visible != sb) hubBox.Visible = sb; }
        if (hubBObj == null || !HubB.Ready) return; long now = Environment.TickCount;
        if (eng.Playing && !hubEngWas && npPlaying) { HubCmd(HubPlayerJs.Pause(CurSvc ?? HubServices.All[0])); npPlaying = false; Toast("Music Hub paused while your project plays."); }
        hubEngWas = eng.Playing;
        if (!hubPolling && now - hubPollAt > 700)
        {
            hubPollAt = now; hubPolling = true; var b = HubB;
            b.Js(HubPlayerJs.Poll).ContinueWith(t => { try { HubPolled(t.Result); } catch { } hubPolling = false; }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        if (hubVolSent != hubVol && npHas) { hubVolSent = hubVol; HubCmd(HubPlayerJs.Volume(hubVol)); }
        if (hubAutoTries > 0 && now > hubAutoAt && !HubB.Loading && CurSvc != null) { hubAutoTries--; hubAutoAt = now + 1500; if (npPlaying) hubAutoTries = 0; else HubCmd(HubPlayerJs.PlayPage(CurSvc)); }
        if (tab == 9 && now > hubSignAt) { hubSignAt = now + 8000; HubCheckSignedIn(); }
    }
    void HubPolled(string res)
    {
        if (string.IsNullOrEmpty(res) || res == "null") return; var inner = MiniJson.Parse(res) as string; if (inner == null) return; var d = MiniJson.Parse(inner) as Dictionary<string, object>; if (d == null) return;
        bool was = npPlaying, wasEnd = npEnded; npHas = d.ContainsKey("has") && d["has"] is bool && (bool)d["has"]; npPlaying = d.ContainsKey("p") && d["p"] is bool && (bool)d["p"]; npEnded = d.ContainsKey("end") && d["end"] is bool && (bool)d["end"];
        string t = HubApi.S(d, "t"), a = HubApi.S(d, "a"); npTitle = t.Length > 0 ? t : (npHas ? HubB.Title : ""); npArtist = a; npArt = HubApi.S(d, "art");
        double v; npPos = double.TryParse(HubApi.S(d, "pos"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0; npDur = double.TryParse(HubApi.S(d, "dur"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0;
        if (npEnded && !wasEnd && hubQueue != null && hubCur != null && hubQAt + 1 < hubQueue.Count) HubSkip(1);   // the queue goes on when a song ends
        if (was != npPlaying) lastInput = DateTime.Now;
    }
    void HubCmd(string js) { if (hubBObj != null && HubB.Ready) HubB.Js(js); }
    void HubToggle() { var s = CurSvc; if (s == null) return; HubCmd(HubPlayerJs.Toggle(s)); npPlaying = !npPlaying; }
    void HubSkip(int d)
    {
        var s = CurSvc; if (s == null) return;
        if (hubQueue != null && hubCur != null && hubQueue.Contains(hubCur)) { int j = hubQAt + d; if (j >= 0 && j < hubQueue.Count) { hubQAt = j; hubCur = hubQueue[j]; HubB.Navigate(hubCur.Open); hubAutoTries = 6; hubAutoAt = Environment.TickCount + 2500; return; } if (d > 0) { Toast("That was the last song of the queue."); return; } }
        HubB.Js(d > 0 ? HubPlayerJs.Next(s) : HubPlayerJs.Prev(s)).ContinueWith(t => { if (t.Result != null && t.Result.Contains("none")) BeginInvoke(new Action(() => Toast(s.Name + " has no " + (d > 0 ? "next" : "previous") + " button Purphex can press here: use the one in its own player."))); });
    }
    async void HubCheckSignedIn()
    {
        if (hubBObj == null) return;
        foreach (var s in HubServices.All) { if (s.LoginCookie == null) continue; var names = await HubB.CookieNames(s.CookieUrl); hubSigned[s.Id] = names.Contains(s.LoginCookie) ? 1 : 2; }
        lastInput = DateTime.Now;
    }
    async void HubSignOut(HubService s)
    {
        if (!AskYes("Sign out of " + s.Name + " inside Purphex? Its sign-in and saved page data in the hub are deleted (your " + s.Name + " account itself is not touched).", "Sign out")) return;
        if (hubBObj == null) return; await HubB.ClearSites(s.Origins); hubSigned[s.Id] = 2; if (hubSvc == s.Id) HubB.Navigate(s.Home); Toast("Signed out of " + s.Name + " in the hub.");
    }
    static void HubOwnWindow(string url)
    {   // the service's own page in an Edge app window (no tabs, no address bar), or your normal browser
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("msedge.exe", "--app=\"" + url + "\"") { UseShellExecute = true }); }
        catch { try { System.Diagnostics.Process.Start(url); } catch { } }
    }

    // ---- searching ----
    void HubSearch(string q)
    {
        q = (q ?? "").Trim(); if (q.Length == 0) return; hubQuery = q;
        if (hubView == 1 && CurSvc != null) { HubB.Navigate(CurSvc.Search(q)); return; }   // inside a service: its own search
        hubView = 0; hubResults = new List<HubItem>(); hubBusy = true; hubErr = ""; hubScroll = 0;
        ThreadPool.QueueUserWorkItem(o =>
        {
            var all = new List<HubItem>(); string err = "";
            foreach (var api in HubApi.All) if (api.Connected) { try { all.AddRange(api.Search(q)); } catch (Exception ex) { err = ex.Message; } }
            all.AddRange(HubCatalog.Search(q));
            try { BeginInvoke(new Action(() => { if (hubQuery != q) return; hubResults = all; hubBusy = false; hubErr = all.Count == 0 ? (err.Length > 0 ? err : "Nothing found for \"" + q + "\". Try a service's own search on the left.") : err; lastInput = DateTime.Now; })); } catch { }
        });
    }
    void HubLoadLibrary(string apiId, string section)
    {
        var api = HubApi.Get(apiId); if (api == null) return; hubApiId = apiId; hubSection = section; hubView = 2; hubItems = new List<HubItem>(); hubBusy = true; hubErr = ""; hubScroll = 0;
        ThreadPool.QueueUserWorkItem(o =>
        {
            List<HubItem> l = null; string err = ""; try { l = api.Load(section); } catch (Exception ex) { err = ex.Message; }
            try { BeginInvoke(new Action(() => { if (hubApiId != apiId || hubSection != section) return; hubItems = l ?? new List<HubItem>(); hubBusy = false; hubErr = err.Length > 0 ? err : (hubItems.Count == 0 ? "Nothing here yet in your " + api.Name + " " + section.ToLower() + "." : ""); lastInput = DateTime.Now; })); } catch { }
        });
    }
    void HubConnect(HubApi api)
    {
        if (!api.Configured) { Toast(api.Name + " needs your developer app first: press SET CLIENT ID" + (api.NeedsSecret ? " and SET SECRET." : ".")); return; }
        Toast("Your browser opens " + api.Name + "'s own sign-in page. Allow Purphex there, then come back.");
        api.Connect(err => { try { BeginInvoke(new Action(() => { if (err != null) Toast(err); else { Toast(api.Name + " is connected."); HubLoadLibrary(api.Id, "Playlists"); hubConnOpen = false; } })); } catch { } });
    }

    // ---- the page ----
    void PaintHub(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + 10;
        if (hubBox == null)
        {
            hubBox = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 12f), Visible = false }; Win.NeonBox(hubBox); Controls.Add(hubBox); Win.DarkControl(hubBox);
            hubBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; HubSearch(hubBox.Text); } };
        }
        // the top row: search, home, connect accounts, back to the DAW
        var sr = Rc(24, top, Math.Min(460, W - 760), 34); FillRR(g, TM.C(255, 10, 5, 22), sr, 8); Win.DrawNeonBox(g, sr, false);
        var tb = Rectangle.Round(Rc(sr.X + 10, sr.Y + 6, sr.Width - 110, 22)); if (hubBox.Bounds != tb) hubBox.Bounds = tb;
        if (hubBox.Text.Length == 0 && !hubBox.Focused) Txt(g, hubView == 1 && CurSvc != null ? "Search " + CurSvc.Name + "..." : "Search songs, artists, albums...", 12, DimC, Rc(sr.X + 14, sr.Y, sr.Width - 120, sr.Height), StringAlignment.Near, false);
        Btn(g, Rc(sr.Right - 92, sr.Y + 4, 86, 26), "SEARCH", 2, () => HubSearch(hubBox.Text), 11);
        float bx = sr.Right + 12; var hb = Rc(bx, top, 196, 34); Btn(g, hb, hubView == 0 ? "MUSIC HUB" : "BACK TO MUSIC HUB", hubView == 0 ? 4 : 2, () => { hubView = 0; hubResults = null; }, 12); Hint2(hb, hubView == 0 ? "The Music Hub front page: your services and accounts, and search across them" : "Back to the Music Hub front page (the music keeps playing)");
        var cb = Rc(hb.Right + 8, top, 196, 34); Btn(g, cb, "CONNECT ACCOUNTS", hubConnOpen ? 4 : 1, () => hubConnOpen = true, 12); Hint2(cb, "Connect Spotify, YouTube and SoundCloud to see your playlists, liked songs and more here");
        var back = Rc(W - 24 - 160, top, 160, 34); Btn(g, back, "BACK TO DAW", 2, () => { tab = 0; }, 12); Hint2(back, "Back to your project: the music keeps playing (the small player stays in the corner)");
        // the services on the left
        float ly = top + 48, lw = 236, playerH = 72, bottom = H - 16 - playerH - 8; var left = Rc(24, ly, lw, bottom - ly); FillRR(g, TM.C(200, 16, 9, 34), left, 12); Win.DrawNeonBox(g, left, false);
        Txt(g, "SERVICES", 11, DimC, Rc(left.X + 14, left.Y + 8, 200, 18), StringAlignment.Near, true); float yy = left.Y + 30;
        float rowH = Math.Min(34f, (left.Height - 150) / HubServices.All.Length);
        foreach (var s in HubServices.All)
        {
            var r = Rc(left.X + 8, yy, lw - 16, rowH - 4); bool on = hubView == 1 && hubSvc == s.Id; if (on || Hot(r)) FillRR(g, on ? TM.C(160, 70, 38, 130) : TM.C(90, 60, 34, 110), r, 8);
            using (var b = new SolidBrush(s.Col)) g.FillEllipse(b, r.X + 8, r.Y + r.Height / 2 - 6, 12, 12); Txt(g, s.Name, 12, Ink, Rc(r.X + 28, r.Y, r.Width - 90, r.Height), StringAlignment.Near, on);
            int st; hubSigned.TryGetValue(s.Id, out st); if (st == 1) Txt(g, "SIGNED IN", 9, NeonLime, Rc(r.Right - 74, r.Y, 70, r.Height), StringAlignment.Far, true);
            var ss = s; Reg(r, () => HubOpenService(ss.Id)); Hint2(r, s.Note); yy += rowH;
        }
        yy += 6; Txt(g, "YOUR LIBRARIES", 11, DimC, Rc(left.X + 14, yy, 200, 18), StringAlignment.Near, true); yy += 22;
        int nc = 0; foreach (var api in HubApi.All) if (api.Connected) { var r = Rc(left.X + 8, yy, lw - 16, 28); bool on = hubView == 2 && hubApiId == api.Id; Btn(g, r, api.Name.ToUpper() + " LIBRARY", on ? 4 : 1, () => HubLoadLibrary(api.Id, "Playlists"), 11); yy += 32; nc++; }
        if (nc == 0) TxtWrap(g, "Connect Spotify, YouTube or SoundCloud (CONNECT ACCOUNTS) to see your playlists and liked songs here.", 10.5f, DimC, Rc(left.X + 14, yy, lw - 28, 60));
        // the middle
        var main = Rc(left.Right + 14, ly, W - 24 - left.Right - 14, bottom - ly);
        if (hubView == 1 && CurSvc != null) PaintHubWeb(g, main); else { hubWebR = RectangleF.Empty; if (hubView == 2) PaintHubLibrary(g, main); else PaintHubHome(g, main); }
        PaintHubPlayer(g, Rc(24, H - 16 - playerH, W - 48, playerH));
        if (hubConnOpen) PaintHubConnect(g);
    }
    void PaintHubWeb(Graphics g, RectangleF a)
    {
        var s = CurSvc; var b = hubBObj == null ? null : HubB; float x = a.X;
        var bk = Rc(x, a.Y, 40, 32); Btn(g, bk, "<", b != null && b.CanBack ? 1 : 0, () => { if (b != null) b.Back(); }, 15); Hint2(bk, "Back"); x += 44;
        var fw = Rc(x, a.Y, 40, 32); Btn(g, fw, ">", b != null && b.CanForward ? 1 : 0, () => { if (b != null) b.Forward(); }, 15); Hint2(fw, "Forward"); x += 44;
        var rl = Rc(x, a.Y, 84, 32); Btn(g, rl, "RELOAD", 1, () => { if (b != null) b.Reload(); }, 11); x += 90;
        foreach (var lk in s.Links) { float w = 18 + lk[0].Length * 7.4f; if (x + w > a.Right - 330) break; string u = lk[1]; Btn(g, Rc(x, a.Y, w, 32), lk[0].ToUpper(), b != null && b.Url.StartsWith(u, StringComparison.OrdinalIgnoreCase) ? 4 : 0, () => HubB.Navigate(u), 10); x += w + 4; }
        var ow = Rc(a.Right - 318, a.Y, 196, 32); Btn(g, ow, "OPEN IN OWN WINDOW", 1, () => HubOwnWindow(b != null && b.Url.StartsWith("https://") ? b.Url : s.Home), 10); Hint2(ow, "Open " + s.Name + " in its own window (no tabs or address bar), for anything that does not work inside Purphex");
        var so = Rc(a.Right - 116, a.Y, 116, 32); Btn(g, so, "SIGN OUT", 3, () => HubSignOut(s), 10); Hint2(so, "Sign out of " + s.Name + " inside Purphex (deletes its sign-in from the hub)");
        string status = b == null ? "" : b.Error.Length > 0 ? b.Error : b.Loading ? "LOADING " + s.Name.ToUpper() + "..." : s.Note;
        Txt(g, status, 10.5f, b != null && b.Error.Length > 0 ? NeonPink : (b != null && b.Loading ? NeonCyan : DimC), Rc(a.X + 2, a.Y + 36, a.Width - 4, 18), StringAlignment.Near, b != null && b.Loading);
        var web = Rc(a.X, a.Y + 58, a.Width, a.Height - 58); FillRR(g, TM.C(255, 10, 5, 24), web, 10); Win.DrawNeonBox(g, web, false);
        if (b == null || !b.Ready) { Txt(g, b != null && b.Error.Length > 0 ? b.Error : "STARTING THE MUSIC BROWSER...", 14, b != null && b.Error.Length > 0 ? NeonPink : DimC, web, StringAlignment.Center, true); if (b != null && b.NoRuntime) { var ib = Rc(web.X + web.Width / 2 - 140, web.Y + web.Height / 2 + 30, 280, 34); Btn(g, ib, "GET WEBVIEW2 FROM MICROSOFT", 2, () => { try { System.Diagnostics.Process.Start("https://developer.microsoft.com/microsoft-edge/webview2/"); } catch { } }, 11); } }
        hubWebR = Rc(web.X + 3, web.Y + 3, web.Width - 6, web.Height - 6);
        Reg(web, () => { });
    }
    void PaintHubHome(Graphics g, RectangleF a)
    {
        var gs = g.Save(); g.SetClip(a, CombineMode.Intersect); float y = a.Y - hubScroll;
        if (hubResults != null)
        {
            Txt(g, hubBusy ? "SEARCHING FOR \"" + hubQuery.ToUpper() + "\"..." : "RESULTS FOR \"" + hubQuery.ToUpper() + "\"  (" + hubResults.Count + ")", 15, Ink, Rc(a.X, y, a.Width, 26), StringAlignment.Near, true);
            Txt(g, "Songs from your connected accounts, Deezer's and Apple's catalogues. Click one to play it on its service inside Purphex.", 10.5f, DimC, Rc(a.X, y + 26, a.Width, 18), StringAlignment.Near, false);
            if (hubErr.Length > 0 && !hubBusy) Txt(g, hubErr, 11, NeonPink, Rc(a.X, y + 46, a.Width, 18), StringAlignment.Near, false);
            y = PaintHubGrid(g, a, hubResults, y + 70);
        }
        else
        {
            Txt(g, "MUSIC HUB", 22, Ink, Rc(a.X, y, a.Width, 32), StringAlignment.Near, true);
            Txt(g, "Listen while you make music. Every service plays in its own official player right here: sign in on its own page, Purphex never sees your password.", 11, DimC, Rc(a.X, y + 32, a.Width, 18), StringAlignment.Near, false);
            y += 64; float cw = 196, ch = 104, gap = 12; int cols = Math.Max(1, (int)((a.Width + gap) / (cw + gap)));
            for (int i = 0; i < HubServices.All.Length; i++)
            {
                var s = HubServices.All[i]; var r = Rc(a.X + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch); bool hot = Hot(r) && a.Contains(MousePos);
                FillRR(g, hot ? TM.C(230, 40, 20, 70) : TM.C(210, 24, 12, 46), r, 12); using (var br = new SolidBrush(s.Col)) g.FillRectangle(br, r.X + 10, r.Y + 12, 6, ch - 24); Win.DrawNeonBox(g, r, hot);
                Txt(g, s.Name, 15, Ink, Rc(r.X + 26, r.Y + 10, cw - 34, 24), StringAlignment.Near, true);
                int st; hubSigned.TryGetValue(s.Id, out st); Txt(g, st == 1 ? "SIGNED IN" : s.LoginCookie == null || st == 0 ? "OPEN TO SIGN IN" : "NOT SIGNED IN", 10, st == 1 ? NeonLime : DimC, Rc(r.X + 26, r.Y + 36, cw - 34, 16), StringAlignment.Near, true);
                Txt(g, s.Api != null ? "LIBRARY IN PURPHEX TOO" : "WEB PLAYER", 9, NeonCyan, Rc(r.X + 26, r.Y + 56, cw - 34, 14), StringAlignment.Near, true);
                var ob = Rc(r.X + 26, r.Bottom - 30, 80, 22); var ss = s; Btn(g, ob, "OPEN", 2, () => HubOpenService(ss.Id), 10); Reg(r, () => HubOpenService(ss.Id)); Hint2(r, s.Note);
            }
            y += ((HubServices.All.Length + cols - 1) / cols) * (ch + gap) + 10;
        }
        g.Restore(gs); float total = y + hubScroll - a.Y; float maxS = Math.Max(0, total - a.Height); hubScroll = Math.Max(0, Math.Min(maxS, hubScroll));
        regs.Add(new HitReg { R = a, OnWheel = d => hubScroll = Math.Max(0, Math.Min(maxS, hubScroll - d * 70)) });
        if (maxS > 0) VScroll(g, Rc(a.Right - 44, a.Y, 42, a.Height), total, a.Height, () => hubScroll, v => hubScroll = v);
    }
    void PaintHubLibrary(Graphics g, RectangleF a)
    {
        var api = HubApi.Get(hubApiId); if (api == null) { hubView = 0; return; } float x = a.X;
        Txt(g, api.Name.ToUpper() + " LIBRARY", 16, Ink, Rc(a.X, a.Y, 260, 32), StringAlignment.Near, true); x += 260;
        foreach (var sec in HubApi.Sections) { if (!api.Has(sec)) continue; float w = 22 + sec.Length * 8; string s2 = sec; Btn(g, Rc(x, a.Y, w, 30), sec.ToUpper(), hubSection == sec ? 4 : 0, () => HubLoadLibrary(api.Id, s2), 10); x += w + 6; }
        var area = Rc(a.X, a.Y + 44, a.Width, a.Height - 44); var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        float y = area.Y - hubScroll;
        if (hubBusy) Txt(g, "LOADING YOUR " + hubSection.ToUpper() + "...", 13, NeonCyan, Rc(area.X, y, area.Width, 24), StringAlignment.Near, true);
        else if (hubErr.Length > 0) TxtWrap(g, hubErr, 12, hubItems.Count == 0 && hubErr.StartsWith("Nothing") ? DimC : NeonPink, Rc(area.X, y, area.Width, 44));
        y = PaintHubGrid(g, area, hubItems, y + (hubBusy || hubErr.Length > 0 ? 40 : 0));
        g.Restore(gs); float total = y + hubScroll - area.Y; float maxS = Math.Max(0, total - area.Height); hubScroll = Math.Max(0, Math.Min(maxS, hubScroll));
        regs.Add(new HitReg { R = area, OnWheel = d => hubScroll = Math.Max(0, Math.Min(maxS, hubScroll - d * 70)) });
        if (maxS > 0) VScroll(g, Rc(area.Right - 44, area.Y, 42, area.Height), total, area.Height, () => hubScroll, v => hubScroll = v);
    }
    // the cards: cover, title, artist and which service it is on; click to play it (or open a playlist / album / artist)
    float PaintHubGrid(Graphics g, RectangleF a, List<HubItem> items, float y)
    {
        float cw = 160, ch = 222, gap = 12; int cols = Math.Max(1, (int)((a.Width - 50 + gap) / (cw + gap)));
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; var r = Rc(a.X + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch); if (r.Bottom < a.Y || r.Y > a.Bottom) continue; bool hot = Hot(r) && a.Contains(MousePos);
            FillRR(g, hot ? TM.C(230, 40, 20, 70) : TM.C(200, 22, 12, 44), r, 10); Win.DrawNeonBox(g, r, hot || it == hubCur);
            var pic = Rc(r.X + 8, r.Y + 8, cw - 16, cw - 16); var bm = HubArt.Get(it.Art); var s = HubServices.Get(it.Service);
            if (bm != null) g.DrawImage(bm, pic); else { FillRR(g, TM.C(255, 30, 16, 60), pic, 8); Txt(g, it.Kind == "artist" ? "ARTIST" : it.Kind == "playlist" ? "PLAYLIST" : it.Kind == "album" ? "ALBUM" : "SONG", 11, DimC, pic, StringAlignment.Center, true); }
            if (s != null) { var tag = Rc(pic.X + 6, pic.Y + 6, Math.Min(pic.Width - 12, 14 + s.Name.Length * 6.4f), 18); FillRR(g, Color.FromArgb(230, 12, 6, 24), tag, 6); using (var br = new SolidBrush(s.Col)) g.FillEllipse(br, tag.X + 5, tag.Y + 6, 6, 6); Txt(g, s.Name, 9, Ink, Rc(tag.X + 13, tag.Y, tag.Width - 15, tag.Height), StringAlignment.Near, true); }
            Txt(g, it.Title, 12, Ink, Rc(r.X + 8, pic.Bottom + 4, cw - 16, 20), StringAlignment.Near, true); Txt(g, it.Sub.Length > 0 ? it.Sub : it.Kind.ToUpper(), 10.5f, DimC, Rc(r.X + 8, pic.Bottom + 24, cw - 16, 18), StringAlignment.Near, false);
            var list = items; int k = i; Reg(r, () => HubPlayItem(list, k)); Hint2(r, (it.Kind == "track" ? "Play " : "Open ") + it.Title + (s != null ? " on " + s.Name : ""));
        }
        return y + ((items.Count + cols - 1) / cols) * (ch + gap);
    }
    // now playing (in the hub)
    void PaintHubPlayer(Graphics g, RectangleF r)
    {
        FillRR(g, Color.Black, r, 14); using (var pen = new Pen(Color.FromArgb(npPlaying ? 230 : 150, NeonViolet), 1.3f)) using (var p = RR(r, 14)) g.DrawPath(pen, p); Reg(r, () => { }).Overlay = true;
        var s = CurSvc; var pic = Rc(r.X + 10, r.Y + 8, r.Height - 16, r.Height - 16); var bm = HubArt.Get(npArt); if (bm != null) g.DrawImage(bm, pic); else { FillRR(g, TM.C(255, 30, 16, 60), pic, 8); DrawLogo(g, pic.X + pic.Width / 2, pic.Y + pic.Height / 2, pic.Height / 3, npPlaying, false); }
        string title = npHas || npTitle.Length > 0 ? npTitle : "NOTHING PLAYING"; Txt(g, title, 14, Ink, Rc(pic.Right + 12, r.Y + 10, 330, 22), StringAlignment.Near, true);
        Txt(g, (npArtist.Length > 0 ? npArtist + "  -  " : "") + (s != null ? s.Name.ToUpper() : "PICK A SERVICE ON THE LEFT"), 11, s != null ? s.Col : DimC, Rc(pic.Right + 12, r.Y + 34, 330, 18), StringAlignment.Near, true);
        float cx = r.X + r.Width / 2 - 70; bool live = s != null && hubBObj != null;
        var pv = Rc(cx - 50, r.Y + r.Height / 2 - 20, 40, 40); MediaBtn(g, pv, "prev", false, live, () => HubSkip(-1), "Previous");
        var pp = Rc(cx + 2, r.Y + r.Height / 2 - 27, 54, 54); MediaBtn(g, pp, npPlaying ? "pause" : "play", true, live, () => HubToggle(), npPlaying ? "Pause" : "Play");
        var nx = Rc(cx + 68, r.Y + r.Height / 2 - 20, 40, 40); MediaBtn(g, nx, "next", false, live, () => HubSkip(1), "Next" + (hubQueue != null ? " (queue: " + (hubQAt + 1) + " of " + hubQueue.Count + ")" : ""));
        var seek = Rc(cx + 130, r.Y + r.Height / 2 - 4, Math.Max(80, r.Right - 330 - (cx + 130)), 8);
        if (npDur > 1) { var sg = Slider(g, seek, (float)(npPos / npDur), f => { npPos = f * npDur; HubCmd(HubPlayerJs.Seek(npPos)); }, NeonCyan); Txt(g, Tm(npPos) + " / " + Tm(npDur), 10, DimC, Rc(seek.X, seek.Bottom + 4, seek.Width, 14), StringAlignment.Center, false); }
        else Txt(g, npHas ? "LIVE" : "", 10, DimC, Rc(seek.X, r.Y, seek.Width, r.Height), StringAlignment.Center, true);
        Txt(g, "VOL", 10, DimC, Rc(r.Right - 300, r.Y, 34, r.Height), StringAlignment.Near, true); Slider(g, Rc(r.Right - 262, r.Y + r.Height / 2 - 4, 120, 8), hubVol, f => hubVol = f, NeonViolet);
        var qb = Rc(r.Right - 128, r.Y + r.Height / 2 - 15, 118, 30); Btn(g, qb, hubQueue != null ? "QUEUE " + (hubQAt + 1) + "/" + hubQueue.Count : "QUEUE: NONE", hubQueue != null ? 1 : 0, () => { if (hubQueue != null && AskYes("Clear the queue (" + hubQueue.Count + " songs)?", "Queue")) { hubQueue = null; } }, 10);
        Hint2(qb, hubQueue != null ? "Songs from the list you played from go one after another. Press to clear the queue." : "Play a song from a library or search results: the songs after it become the queue");
    }
    // a round player button with a drawn icon: "prev", "next", "play" or "pause"
    void MediaBtn(Graphics g, RectangleF r, string icon, bool main, bool enabled, Action click, string tip)
    {
        bool hot = enabled && Hot(r); var c1 = main ? NeonViolet : TM.C(255, 40, 26, 74); var c2 = main ? NeonPink : TM.C(255, 26, 16, 52);
        if (hot) Glow(g, main ? NeonPink : NeonViolet, r, r.Height / 2, main ? 0.9f : 0.6f, 3);
        using (var br = new LinearGradientBrush(r, hot ? Lighter(c1) : c1, hot ? Lighter(c2) : c2, 90f)) g.FillEllipse(br, r);
        using (var pen = new Pen(Color.FromArgb(enabled ? (hot ? 255 : 200) : 90, main ? Color.White : NeonViolet), main ? 1.8f : 1.4f)) g.DrawEllipse(pen, r);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = r.Height * (main ? 0.22f : 0.2f); var ic = Color.FromArgb(enabled ? 255 : 110, 255, 255, 255);
        using (var b = new SolidBrush(ic))
        {
            if (icon == "play") g.FillPolygon(b, new[] { new PointF(cx - s * 0.7f, cy - s * 1.1f), new PointF(cx - s * 0.7f, cy + s * 1.1f), new PointF(cx + s * 1.15f, cy) });
            else if (icon == "pause") { float bw = s * 0.62f, gap = s * 0.42f; FillRR(g, ic, Rc(cx - gap - bw, cy - s * 1.05f, bw, s * 2.1f), bw * 0.3f); FillRR(g, ic, Rc(cx + gap, cy - s * 1.05f, bw, s * 2.1f), bw * 0.3f); }
            else
            {
                float d = icon == "next" ? 1 : -1, bw = s * 0.34f;   // a triangle pointing the way, against a bar
                g.FillPolygon(b, new[] { new PointF(cx - d * s * 0.8f, cy - s), new PointF(cx - d * s * 0.8f, cy + s), new PointF(cx + d * s * 0.55f, cy) });
                var bar = d > 0 ? Rc(cx + s * 0.55f, cy - s, bw, s * 2) : Rc(cx - s * 0.55f - bw, cy - s, bw, s * 2); FillRR(g, ic, bar, bw * 0.4f);
            }
        }
        if (enabled) { Reg(r, () => click()).Button = true; Hint2(r, tip); }
    }
    static Color Lighter(Color c) { return Color.FromArgb(c.A, Math.Min(255, c.R + 40), Math.Min(255, c.G + 40), Math.Min(255, c.B + 40)); }
    static string Tm(double s) { if (s < 0 || double.IsNaN(s)) s = 0; return ((int)(s / 60)) + ":" + ((int)(s % 60)).ToString("00"); }
    // the small player on every other page
    internal bool miniHidden;
    void PaintHubMini(Graphics g)
    {
        if (tab == 9 || hubBObj == null || !npPlaying || OverlayOpen) return; float W = ClientSize.Width, H = ClientSize.Height;
        if (miniHidden)
        {   // hidden: a slim tab at the right edge shows something is playing; click it to bring the player back
            var tb = Rc(W - 26, H - 118, 26, 56); FillRR(g, TM.C(245, 6, 3, 14), tb, 8); StrokeRR(g, Color.FromArgb(220, NeonViolet), tb, 8, 1.3f);
            using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, new[] { new PointF(tb.X + 16, tb.Y + 20), new PointF(tb.X + 16, tb.Y + 36), new PointF(tb.X + 8, tb.Y + 28) });
            var rg = Reg(tb, () => miniHidden = false); rg.Button = true; Hint2(tb, "Show the Music Hub player"); return;
        }
        var r = Rc(W - 24 - 380, H - 120, 380, 60); FillRR(g, TM.C(245, 6, 3, 14), r, 14); Glow(g, NeonViolet, r, 14, 0.5f, 3); StrokeRR(g, Color.FromArgb(220, NeonViolet), r, 14, 1.3f); Reg(r, () => { }).Overlay = true;
        var s = CurSvc; var pic = Rc(r.X + 8, r.Y + 8, 44, 44); var bm = HubArt.Get(npArt); if (bm != null) g.DrawImage(bm, pic); else FillRR(g, TM.C(255, 30, 16, 60), pic, 6);
        Txt(g, npTitle.Length > 0 ? npTitle : "MUSIC HUB", 12, Ink, Rc(pic.Right + 8, r.Y + 8, 170, 20), StringAlignment.Near, true); Txt(g, (npArtist.Length > 0 ? npArtist + " - " : "") + (s != null ? s.Name : ""), 10, s != null ? s.Col : DimC, Rc(pic.Right + 8, r.Y + 30, 170, 18), StringAlignment.Near, false);
        float bx = r.Right - 158; MediaBtn(g, Rc(bx, r.Y + 14, 32, 32), "prev", false, true, () => HubSkip(-1), "Previous"); MediaBtn(g, Rc(bx + 37, r.Y + 9, 42, 42), npPlaying ? "pause" : "play", true, true, () => HubToggle(), npPlaying ? "Pause" : "Play"); MediaBtn(g, Rc(bx + 84, r.Y + 14, 32, 32), "next", false, true, () => HubSkip(1), "Next");
        var ob = Rc(r.Right - 38, r.Y + 6, 30, 22); Btn(g, ob, "HUB", 0, () => OpenHub(), 9); Hint2(ob, "Open the Music Hub");
        var hd = Rc(r.Right - 38, r.Y + 32, 30, 22); Btn(g, hd, " ", 0, () => miniHidden = true, 9); using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(pen, hd.X + 12, hd.Y + 6, hd.X + 18, hd.Y + 11); g.DrawLine(pen, hd.X + 18, hd.Y + 11, hd.X + 12, hd.Y + 16); }   // a chevron: slide it away to the side Hint2(hd, "Hide the player (a tab at the edge brings it back)");
    }
    // CONNECT ACCOUNTS: the official APIs with your own developer apps
    void PaintHubConnect(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); Reg(all, () => hubConnOpen = false); using (var dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(dim, all);
        float bw = Math.Min(860, W - 40), bh = Math.Min(H - 40, 600); var box = Rc(W / 2 - bw / 2, H / 2 - bh / 2, bw, bh); FillRR(g, TM.C(252, 14, 8, 30), box, 20); Glow(g, NeonViolet, box, 20, 0.9f, 4); StrokeRR(g, NeonViolet, box, 20, 1.6f); Reg(box, () => { });
        Txt(g, "CONNECT ACCOUNTS", 22, Ink, Rc(box.X + 24, box.Y + 16, 400, 32), StringAlignment.Near, true); Btn(g, Rc(box.Right - 52, box.Y + 16, 32, 30), "x", 3, () => hubConnOpen = false, 13);
        TxtWrap(g, "Every service already plays in the hub once you sign in on its page. Connecting its official API as well lists your library inside Purphex (playlists, liked songs, albums, artists, recently played). Each needs a free developer app of your own (its Client ID); you then sign in on the service's own page in your browser. Purphex keeps the sign-in encrypted for your Windows user. Redirect URI to add in each app:  " + HubApi.Redirect, 11, DimC, Rc(box.X + 24, box.Y + 50, box.Width - 48, 64));
        float y = box.Y + 124;
        foreach (var api in HubApi.All)
        {
            var r = Rc(box.X + 20, y, box.Width - 40, 128); FillRR(g, TM.C(220, 24, 12, 46), r, 12); Win.DrawNeonBox(g, r, false); var a2 = api;
            Txt(g, api.Name.ToUpper(), 16, Ink, Rc(r.X + 16, r.Y + 8, 200, 24), StringAlignment.Near, true);
            Txt(g, api.Connected ? "CONNECTED" : api.Configured ? "READY TO CONNECT" : "NEEDS YOUR DEVELOPER APP", 11, api.Connected ? NeonLime : api.Configured ? NeonCyan : Color.FromArgb(255, 255, 200, 90), Rc(r.X + 200, r.Y + 10, 300, 20), StringAlignment.Near, true);
            TxtWrap(g, api.Setup, 10.5f, DimC, Rc(r.X + 16, r.Y + 34, r.Width - 32, 34));
            float bx = r.X + 16, by = r.Bottom - 44;
            Btn(g, Rc(bx, by, 150, 32), "SETUP PAGE", 0, () => { try { System.Diagnostics.Process.Start(a2.SetupUrl); } catch { } }, 10); bx += 156;
            Btn(g, Rc(bx, by, 150, 32), "COPY REDIRECT", 0, () => { try { Clipboard.SetText(HubApi.Redirect); Toast("Copied " + HubApi.Redirect); } catch { } }, 10); bx += 156;
            Btn(g, Rc(bx, by, 150, 32), a2.ClientId.Length > 0 ? "CLIENT ID: SET" : "SET CLIENT ID", a2.ClientId.Length > 0 ? 4 : 1, () => { string v = Ask(a2.Name + " Client ID (from your developer app):", a2.ClientId); if (v != null) { HubVault.Set(a2.Id + ".id", v.Trim()); a2.Disconnect(); } }, 10); bx += 156;
            if (api.NeedsSecret) { Btn(g, Rc(bx, by, 140, 32), a2.Secret.Length > 0 ? "SECRET: SET" : "SET SECRET", a2.Secret.Length > 0 ? 4 : 1, () => { string v = Ask(a2.Name + " Client secret (from your developer app; kept encrypted):", ""); if (v != null && v.Trim().Length > 0) HubVault.Set(a2.Id + ".secret", v.Trim()); }, 10); bx += 146; }
            var cr = Rc(r.Right - 16 - 150, by, 150, 32);
            if (api.Connected) Btn(g, cr, "DISCONNECT", 3, () => { if (AskYes("Disconnect " + a2.Name + "? Purphex deletes its sign-in (you can also remove Purphex's access on " + a2.Name + "'s site).", "Disconnect")) { a2.Disconnect(); if (hubApiId == a2.Id) hubView = 0; Toast(a2.Name + " disconnected."); } }, 11);
            else Btn(g, cr, "CONNECT", api.Configured ? 2 : 0, () => HubConnect(a2), 11);
            y += 138;
        }
    }
}
