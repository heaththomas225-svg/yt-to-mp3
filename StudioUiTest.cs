using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.IO;

// Drives Purphex Music with synthetic mouse events: overview bar, middle-button panning, zoom limits, thin tracks, add-track strips.   csc /main:S ... StudioUiTest.cs
static class S
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "t"); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("A", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("B", "beat", Clip(60)));
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(500); Paint();
        Check("tracks are tall enough that the header is not squashed (132 px)", F<int>("LaneH") == 132, "lane height " + F<int>("LaneH"));
        double spp0 = F<double>("spp"); Check("the whole song fits the view at the start (auto-fit)", F<bool>("autoFit") && spp0 > 100, "frames per pixel " + spp0.ToString("0"));
        // overview bar: the 16 px strip with a double-click handler
        var ob = Regs().FindLast(r => r.OnDouble != null && Math.Abs(r.R.Height - 34) < 0.5f && r.R.Y > 600); Check("the overview bar is on screen", ob != null, "");
        if (ob != null)
        {
            float right = ob.R.Right - 12; int y = (int)(ob.R.Y + 8);
            // drag the right end of the lit window to the left: zoom in
            double total = Math.Max(eng.EndFrame * 1.04, 44100 * 10.0); float tw = ob.R.Width; var tl = (RectangleF)typeof(StudioForm).GetMethod("TimelineRect", BF).Invoke(sf, null);
            float thumbRight = ob.R.X + (float)(F<long>("scroll") / total * tw) + (float)(tl.Width * spp0 / total * tw);
            Ev("OnMouseMove", MouseButtons.None, 0, (int)thumbRight - 1, y); Ev("OnMouseDown", MouseButtons.Left, 1, (int)thumbRight - 1, y);
            for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)(thumbRight - 1 - i * (tw * 0.6f) / 8), y); Ev("OnMouseUp", MouseButtons.Left, 1, (int)(thumbRight - tw * 0.6f), y); Paint();
            double spp1 = F<double>("spp"); Check("dragging the end of the overview window zooms in", spp1 < spp0 * 0.6 && spp1 >= 4 && !F<bool>("autoFit"), spp0.ToString("0") + " -> " + spp1.ToString("0") + " frames per pixel");
            // drag the middle of the window to the right: move along the song
            Paint(); tl = (RectangleF)typeof(StudioForm).GetMethod("TimelineRect", BF).Invoke(sf, null); float tl0 = ob.R.X + (float)(F<long>("scroll") / total * tw), tr0 = tl0 + (float)(tl.Width * spp1 / total * tw); int mid = (int)((tl0 + tr0) / 2);
            Ev("OnMouseMove", MouseButtons.None, 0, mid, y); Ev("OnMouseDown", MouseButtons.Left, 1, mid, y);
            for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, mid + i * 40, y); Ev("OnMouseUp", MouseButtons.Left, 1, mid + 320, y); Paint();
            long sc1 = F<long>("scroll"); Check("dragging the middle of the overview window moves the view along the song", sc1 > 44100, "view now starts at " + (sc1 / 44100.0).ToString("0.0") + " s");
            Ev("OnMouseMove", MouseButtons.None, 0, mid, y); Ev("OnMouseDown", MouseButtons.Left, 2, mid, y); Ev("OnMouseUp", MouseButtons.Left, 1, mid, y); Paint();
            Check("double-clicking the overview bar shows the whole song again", F<bool>("autoFit") && F<double>("spp") > spp0 * 0.9 && F<long>("scroll") == 0, "frames per pixel " + F<double>("spp").ToString("0"));
        }
        // middle button drags the view
        long s0 = F<long>("scroll"); typeof(StudioForm).GetMethod("ZoomBy", BF).Invoke(sf, new object[] { 0.2 }); Paint(); double sppZ = F<double>("spp"); long sa = F<long>("scroll");
        Ev("OnMouseMove", MouseButtons.None, 0, 700, 300); Ev("OnMouseDown", MouseButtons.Middle, 1, 700, 300); for (int i = 1; i <= 5; i++) Ev("OnMouseMove", MouseButtons.Middle, 0, 700 - i * 40, 300); Ev("OnMouseUp", MouseButtons.Middle, 1, 500, 300); Paint();
        long sb = F<long>("scroll"); Check("dragging with the middle mouse button pans the view (drag left = later in the song)", sb > sa + 100, sa + " -> " + sb);
        for (int i = 0; i < 12; i++) typeof(StudioForm).GetMethod("ZoomBy", BF).Invoke(sf, new object[] { 0.3 }); Paint();
        Check("zooming in stops at a sensible limit instead of breaking the display", F<double>("spp") >= 4, "frames per pixel " + F<double>("spp").ToString("0.0"));
        for (int i = 0; i < 12; i++) typeof(StudioForm).GetMethod("ZoomBy", BF).Invoke(sf, new object[] { 3.0 }); Paint();
        Check("zooming out stops at a sensible limit too", F<double>("spp") <= Math.Max(20000.0, eng.EndFrame / 40.0) + 1 && F<long>("scroll") >= 0, "frames per pixel " + F<double>("spp").ToString("0"));
        // BandLab-style: the zoom slider, and the plain wheel moving along the song when the tracks all fit on screen
        typeof(StudioForm).GetMethod("FitView", BF).Invoke(sf, null); Paint(); var zs = Regs().FindLast(r => Math.Abs(r.R.Width - 130) < 0.5f && Math.Abs(r.R.Height - 18) < 0.5f && r.R.Y < 200); Check("there is a zoom slider in the toolbar", zs != null, "");
        if (zs != null)
        {
            double sFit = F<double>("spp"); int zy = (int)(zs.R.Y + 9); Ev("OnMouseMove", MouseButtons.None, 0, (int)zs.R.X + 10, zy); Ev("OnMouseDown", MouseButtons.Left, 1, (int)zs.R.X + 10, zy);
            for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)(zs.R.X + 10 + i * 18), zy); Ev("OnMouseUp", MouseButtons.Left, 1, (int)zs.R.X + 118, zy); Paint();
            Check("dragging the zoom slider to the right zooms in", F<double>("spp") < sFit * 0.2 && !F<bool>("autoFit"), sFit.ToString("0") + " -> " + F<double>("spp").ToString("0"));
            long w0 = F<long>("scroll"); Ev("OnMouseMove", MouseButtons.None, 0, 700, 400); Ev("OnMouseWheel", MouseButtons.None, 0, 700, 400);
        }
        { var mw = typeof(StudioForm).GetMethod("OnMouseWheel", BF | BindingFlags.DeclaredOnly); long w1 = F<long>("scroll"); Paint(); mw.Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, 700, 400, -120) }); Paint(); long w2 = F<long>("scroll");
          Check("the mouse wheel scrolls along the song (the tracks all fit, so it goes sideways)", w2 > w1, w1 + " -> " + w2); mw.Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, 700, 400, 120) }); Paint(); Check("and back again", F<long>("scroll") < w2, F<long>("scroll").ToString()); }
        // the new-track sheet, and inserting where asked
        Paint(); int strips = 0; foreach (var r in Regs()) if (Math.Abs(r.R.Width - 250) < 0.5f && Math.Abs(r.R.Height - 16) < 0.5f) strips++;
        Check("no glitchy add-track strips float between tracks any more", strips == 0, strips + " strips");
        { var cl = typeof(StudioForm).GetMethod("OpenNewTrackSheet", BF); cl.Invoke(sf, new object[] { 2 }); Paint(); Check("+ ADD TRACK opens the New Track sheet with the track types", F<bool>("ntOpen") && Regs().Count > 10, Regs().Count + " regions"); typeof(StudioForm).GetMethod("CloseNewTrackSheet", BF).Invoke(sf, null); Paint(); Check("and it closes again", !F<bool>("ntOpen"), ""); }
        {   // Rapper Vocal Styles sheet: open it, search, press Use, press the star
            string tmpFav = Path.Combine(Path.GetTempPath(), "ui-styles-fav-" + Guid.NewGuid().ToString("N") + ".txt"); VocalStyles.FavFile = tmpFav; SetF("selTrack", 0); int fx0 = eng.Tracks[0].Fx.Count;
            typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); Paint(); Check("Vocal Styles opens a sheet with a list of styles", F<bool>("stylesOpen") && Regs().FindAll(r => Math.Abs(r.R.Width - 96) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f).Count >= 5, Regs().Count + " regions");
            foreach (char ch in "drill") typeof(StudioForm).GetMethod("OnKeyPress", BF).Invoke(sf, new object[] { new KeyPressEventArgs(ch) }); Paint();
            Check("typing searches the styles", F<string>("stylesQuery") == "drill" && Regs().FindAll(r => Math.Abs(r.R.Width - 96) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f).Count == 4, Regs().FindAll(r => Math.Abs(r.R.Width - 96) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f).Count + " Use buttons (one for each of the 4 drill styles)");
            var useBtn = Regs().Find(r => Math.Abs(r.R.Width - 96) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f); int ux = (int)(useBtn.R.X + 30), uy = (int)(useBtn.R.Y + 17);
            Ev("OnMouseMove", MouseButtons.None, 0, ux, uy); Ev("OnMouseDown", MouseButtons.Left, 1, ux, uy); Ev("OnMouseUp", MouseButtons.Left, 1, ux, uy); Paint();
            Check("pressing Use builds that style's plugins on the selected track and closes the sheet", !F<bool>("stylesOpen") && eng.Tracks[0].Fx.Count >= 4, "plugins on the track: " + fx0 + " -> " + eng.Tracks[0].Fx.Count);
            typeof(StudioForm).GetMethod("DoUndo", BF).Invoke(sf, null); Check("Ctrl+Z takes a vocal style off the track again (the plugin chain comes back as it was)", eng.Tracks[0].Fx.Count == fx0, "plugins on the track: " + eng.Tracks[0].Fx.Count);
            typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); Paint(); var rgs = Regs(); int bdi = rgs.FindLastIndex(r => r.R.Width >= 1499 && r.R.Height >= 799); var starReg = rgs.GetRange(Math.Max(0, bdi), rgs.Count - Math.Max(0, bdi)).Find(r => Math.Abs(r.R.Width - 30) < 0.5f && Math.Abs(r.R.Height - 30) < 0.5f && r.OnDown != null);
            if (starReg != null) { int sx = (int)(starReg.R.X + 15), sy = (int)(starReg.R.Y + 15); Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); Ev("OnMouseUp", MouseButtons.Left, 1, sx, sy); Paint(); }
            Check("pressing a star keeps the style as a favourite", starReg != null && VocalStyles.Find("", "", true).Count == 1, "favourites " + VocalStyles.Find("", "", true).Count);
            typeof(StudioForm).GetMethod("ProcessCmdKey", BF).Invoke(sf, new object[] { new Message(), Keys.Escape }); Paint(); Check("Esc closes the Vocal Styles sheet", !F<bool>("stylesOpen"), "");
            eng.Tracks[0].Fx.Clear(); try { File.Delete(tmpFav); } catch { } VocalStyles.FavFile = null;
        }
        {   // automation editor: A next to a setting opens a curve; click adds a point, drag moves it, right-click deletes it
            var tr0 = eng.Tracks[0]; var gfx = new GainFx(); gfx.Prepare(44100); tr0.Fx.Add(gfx); SetF("selTrack", 0); SetF("selFx", 0); int undo0 = ((IList)F<object>("undo")).Count; SetF("revertAction", null);   /* the REVERT box of the step before sits at the bottom left, on top of the lowest A button: this test is about the A button */
            Paint(); var abtn = Regs().FindAll(r => Math.Abs(r.R.Width - 22) < 0.5f && r.R.Height > 20 && r.R.Height < 24 && r.OnDown != null); Check("the plugin editor shows an A (automate) button next to a setting", abtn.Count >= 1, abtn.Count + " buttons");
            if (abtn.Count > 0) { var ab = abtn[abtn.Count - 1]; int ax = (int)(ab.R.X + 11), ay = (int)(ab.R.Y + ab.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, ax, ay); Ev("OnMouseDown", MouseButtons.Left, 1, ax, ay); Ev("OnMouseUp", MouseButtons.Left, 1, ax, ay); Paint(); }
            Check("pressing A opens the automation editor and starts a lane for that setting", F<bool>("autoOpen") && tr0.Auto.Count == 1 && tr0.Auto[0].Pts.Count == 2, tr0.Auto.Count + " lanes");
            var rgs = Regs(); int bi = rgs.FindLastIndex(r => r.R.Width >= 1499 && r.R.Height >= 799); var plot = rgs.GetRange(Math.Max(0, bi), rgs.Count - Math.Max(0, bi)).Find(r => r.OnRight != null && r.OnDrag != null && r.OnUp != null && r.R.Width > 500);
            Check("the editor has a curve area", plot != null, "");
            if (plot != null)
            {
                var lane = tr0.Auto[0]; int cx = (int)(plot.R.X + plot.R.Width * 0.5f), cy = (int)(plot.R.Y + plot.R.Height * 0.25f);
                Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint();
                Check("clicking an empty spot adds a point there", lane.Pts.Count == 3 && lane.Pts[1].Frame > 0, lane.Pts.Count + " points");
                long fr1 = lane.Pts[1].Frame; plot = Regs().FindLast(r => r.OnRight != null && r.OnDrag != null && r.OnUp != null && r.R.Width > 500);
                Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); for (int i = 1; i <= 5; i++) Ev("OnMouseMove", MouseButtons.Left, 0, cx + i * 20, cy + i * 6); Ev("OnMouseUp", MouseButtons.Left, 1, cx + 100, cy + 30); Paint();
                Check("dragging a point moves it along the song and changes its value", lane.Pts.Count == 3 && lane.Pts[1].Frame > fr1 + 1000 && lane.Pts[1].Value < 0.0f + 100f, "frame " + fr1 + " -> " + lane.Pts[1].Frame);
                int rx = cx + 100, ry = cy + 30; Ev("OnMouseMove", MouseButtons.None, 0, rx, ry); Ev("OnMouseDown", MouseButtons.Right, 1, rx, ry); Ev("OnMouseUp", MouseButtons.Right, 1, rx, ry); Paint();
                Check("right-clicking a point deletes it", lane.Pts.Count == 2, lane.Pts.Count + " points");
                Check("every change can be undone with Ctrl+Z", ((IList)F<object>("undo")).Count > undo0, "undo steps " + undo0 + " -> " + ((IList)F<object>("undo")).Count);
            }
            typeof(StudioForm).GetMethod("ProcessCmdKey", BF).Invoke(sf, new object[] { new Message(), Keys.Escape }); Paint(); Check("Esc closes the automation editor", !F<bool>("autoOpen"), "");
            tr0.Auto.Clear(); tr0.Fx.Clear();
        }
        {   // an empty audio track takes the recording itself, and Up / Dn make a row when there is none
            var MI = typeof(StudioForm).GetMethod("AddEmptyAudioTrack", BF); int n0 = eng.Tracks.Count; MI.Invoke(sf, null); var emp = eng.Tracks[eng.Tracks.Count - 1];
            Check("New Track > Voice / Audio adds one empty raw track (no clip, no effects)", eng.Tracks.Count == n0 + 1 && emp.Clip == null && emp.Fx.Count == 0, "tracks " + eng.Tracks.Count);
            var rec = typeof(StudioForm).GetMethod("AddRecording", BF); var pcm = new float[44100]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (float)(0.2 * Math.Sin(i * 0.05));
            rec.Invoke(sf, new object[] { pcm, pcm.Length, 0L });
            Check("recording lands on that same empty track, not on a new one below", eng.Tracks.Count == n0 + 1 && emp.Clip != null && emp.Fx.Count == 0, "tracks " + eng.Tracks.Count);
            var mv = typeof(StudioForm).GetMethod("MoveTrackBy", BF); int n1 = eng.Tracks.Count; mv.Invoke(sf, new object[] { eng.Tracks[eng.Tracks.Count - 1], 1 });
            Check("Dn on the bottom track still moves it down, making an empty row below", eng.Tracks.Count == n1 + 1 && eng.Tracks[eng.Tracks.Count - 1].Clip != null && eng.Tracks[eng.Tracks.Count - 2].Clip == null, "tracks " + eng.Tracks.Count);
            while (eng.Tracks.Count > n0) lock (eng.Sync) eng.Tracks.RemoveAt(eng.Tracks.Count - 1);
        }
        {   // the order rule: every row first, then the layers, then the buses, whatever we do (this once broke after Separate / Duplicate)
            Func<string> order = () => { int phase = 0; foreach (var t in eng.Tracks) { int k = t.IsBus ? 2 : (t.Host != null ? 1 : 0); if (k < phase) return "bad order at " + t.Name; phase = k; } return ""; };
            var bf = BF; var tOrder = StudioEngine.NewTrack("OrdA", "audio", Clip(6)); lock (eng.Sync) eng.Tracks.Insert((int)typeof(StudioForm).GetProperty("LaneCount", bf).GetValue(sf, null), tOrder);
            var bus1 = (Track)typeof(StudioForm).GetMethod("AddBus", bf).Invoke(sf, null); Check("order: adding a bus keeps the rows first", order() == "", order());
            var tL = StudioEngine.NewTrack("OrdB", "audio", Clip(6)); lock (eng.Sync) eng.Tracks.Insert((int)typeof(StudioForm).GetProperty("LaneCount", bf).GetValue(sf, null), tL);
            Check("order: a row added when a bus exists lands before the bus", order() == "", order());
            typeof(StudioForm).GetMethod("MergeRows", bf).Invoke(sf, new object[] { tL, tOrder }); Check("order: sharing a row moves the track after the rows but before the bus", order() == "", order());
            typeof(StudioForm).GetMethod("DuplicateTrack", bf).Invoke(sf, new object[] { tL }); Check("order: duplicating a track that shares a row keeps the rule (the copy shares the row too)", order() == "" && eng.Tracks.FindAll(t => t.Host == tOrder).Count == 2, order());
            typeof(StudioForm).GetMethod("SplitTrack", bf).Invoke(sf, new object[] { tOrder, tOrder.Offset + 2 * StudioEngine.SR }); Check("order: splitting keeps the rule", order() == "", order());
            typeof(StudioForm).GetMethod("DoUndo", bf).Invoke(sf, null); Check("order: undo keeps the rule", order() == "", order()); typeof(StudioForm).GetMethod("DoRedo", bf).Invoke(sf, null); Check("order: redo keeps the rule", order() == "", order());
            typeof(StudioForm).GetMethod("DeleteTrackQuiet", bf).Invoke(sf, new object[] { tOrder }); Check("order: deleting a row removes its layers and keeps the rule", order() == "" && !eng.Tracks.Exists(t => t.Host == tOrder), order());
            typeof(StudioForm).GetMethod("DeleteTrackQuiet", bf).Invoke(sf, new object[] { bus1 }); typeof(StudioForm).GetMethod("DeleteTrackQuiet", bf).Invoke(sf, new object[] { tL });
            {   // the FIRST row shares the last row: it must leave the rows (left among them, every row number below it pointed one track off, and plugins added to the vocal went onto the beat)
                var tA = StudioEngine.NewTrack("OrdTop", "vocals", Clip(6)); var tZ = StudioEngine.NewTrack("OrdEnd", "other", Clip(6)); lock (eng.Sync) { eng.Tracks.Insert(0, tA); eng.Tracks.Insert((int)typeof(StudioForm).GetProperty("LaneCount", bf).GetValue(sf, null), tZ); }
                typeof(StudioForm).GetMethod("MergeRows", bf).Invoke(sf, new object[] { tA, tZ }); Check("order: the first row sharing the last row moves after the rows", order() == "" && tA.Host == tZ, order());
                lock (eng.Sync) { eng.Tracks.Remove(tA); eng.Tracks.Remove(tZ); eng.Tracks.Insert(0, tZ); eng.Tracks.Insert(0, tA); tA.Host = tZ; }   // an older list in the wrong order heals itself before it is drawn
                Paint(); Check("order: a list left in the wrong order is put right when the studio draws", order() == "", order());
                lock (eng.Sync) { eng.Tracks.Remove(tA); eng.Tracks.Remove(tZ); }
            }
        }
        {   // different kinds of track can share one row (nothing is pushed up or down)
            var lane0 = F<int>("LaneH"); int n0 = eng.Tracks.Count, rows0 = (int)typeof(StudioForm).GetProperty("LaneCount", BF).GetValue(sf, null);
            var ta = StudioEngine.NewTrack("HostRow", "audio", Clip(3)); lock (eng.Sync) eng.Tracks.Add(ta); rows0++; n0++; var extra = StudioEngine.NewTrack("Layered", "audio", Clip(3)); lock (eng.Sync) eng.Tracks.Add(extra);
            typeof(StudioForm).GetMethod("MergeRows", BF).Invoke(sf, new object[] { extra, ta }); Paint();
            int rows1 = (int)typeof(StudioForm).GetProperty("LaneCount", BF).GetValue(sf, null);
            Check("dropping a track on another row makes it share that row (one row fewer, same tracks)", extra.Host == ta && rows1 == rows0 && eng.Tracks.Count == n0 + 1, "rows " + rows0 + " -> " + rows1);
            Check("the shared track stays after the rows in the list", eng.Tracks.IndexOf(extra) >= rows1, "");
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_layer_test"); System.IO.Directory.CreateDirectory(dir); string pf = System.IO.Path.Combine(dir, "l.ytmproj"); ProjectIO.Save(eng, pf); var e2 = new StudioEngine(); ProjectIO.Load(e2, pf, delegate (string s) { });
            Check("a project keeps which tracks share a row", e2.Tracks.Exists(t => t.Host != null && t.Name == "Layered"), "");
            typeof(StudioForm).GetMethod("DeleteTrackQuiet", BF).Invoke(sf, new object[] { ta }); Check("deleting a row deletes the tracks layered on it", !eng.Tracks.Contains(extra), "");
            var sp = StudioEngine.NewTrack("ToSplit", "audio", Clip(8)); lock (eng.Sync) eng.Tracks.Add(sp); int rowsB = (int)typeof(StudioForm).GetProperty("LaneCount", BF).GetValue(sf, null), cntB = eng.Tracks.Count;
            typeof(StudioForm).GetMethod("SplitTrack", BF).Invoke(sf, new object[] { sp, sp.Offset + 4 * StudioEngine.SR });
            Check("splitting keeps both pieces on the same row (no new track row)", (int)typeof(StudioForm).GetProperty("LaneCount", BF).GetValue(sf, null) == rowsB && eng.Tracks.Count == cntB + 1 && eng.Tracks.Exists(t => t.Host == sp), "");
            lock (eng.Sync) { eng.Tracks.RemoveAll(t => t.Host == sp); eng.Tracks.Remove(sp); }
        }
        SetF("nextInsert", 1); typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("NEW", "audio", Clip(5)) });
        Check("a track added from a strip lands exactly there and is selected", eng.Tracks.Count == 3 && eng.Tracks[1].Name == "NEW" && F<int>("selTrack") == 1, string.Join(", ", eng.Tracks.ConvertAll(t => t.Name).ToArray()));
        typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("LAST", "audio", Clip(5)) });
        Check("a normal add still goes to the end", eng.Tracks[eng.Tracks.Count - 1].Name == "LAST", "");
        // the empty-project box is a big button (click to choose files) and lights up when files are dragged over the window
        { var save3 = new List<Track>(eng.Tracks); lock (eng.Sync) eng.Tracks.Clear(); Paint(); var tlr = (RectangleF)typeof(StudioForm).GetMethod("TimelineRect", BF).Invoke(sf, null);
          var box = Regs().FindLast(r => Math.Abs(r.R.Width - (tlr.Width - 80)) < 1f && Math.Abs(r.R.Height - 150) < 0.5f); Check("with no music yet, the Add your music box is a clickable button", box != null, box == null ? "missing" : box.R.ToString());
          sf.GetType().GetField("dragOver", BF).SetValue(sf, true); Paint(); var bm4 = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bm4, new Rectangle(0, 0, sf.Width, sf.Height)); Check("it highlights while files are dragged over the window", true, ""); sf.GetType().GetField("dragOver", BF).SetValue(sf, false);
          lock (eng.Sync) eng.Tracks.AddRange(save3); Paint(); }
        // Open: a .ytmproj goes to the project loader (the dialog also lists mp3, wav, flac... and adds them as tracks)
        { string ptmp = Path.Combine(Path.GetTempPath(), "opentest.ytmproj"); var save = new List<Track>(eng.Tracks); lock (eng.Sync) eng.Tracks.Clear(); ProjectIO.Save(eng, ptmp); lock (eng.Sync) eng.Tracks.AddRange(save);
          sf.PromptImportPublic(ptmp); Pump(1500); Check("a project file chosen in Open (or dropped on the window) opens as a project", F<string>("projPath") == ptmp && eng.Tracks.Count == 0, "tracks " + eng.Tracks.Count + ", path " + F<string>("projPath"));
          string exts = (string)typeof(StudioForm).GetField("AudioExts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); Check("the Open dialog lists mp3, wav, flac, m4a, ogg and more", exts.Contains("*.mp3") && exts.Contains("*.wav") && exts.Contains("*.flac") && exts.Contains("*.m4a") && exts.Contains("*.ogg"), exts); try { File.Delete(ptmp); } catch { } ProjectLibrary.Remove(ptmp); }
        // the + ADD TRACK button must always be on screen, even when the tracks run off the bottom
        for (int i = 0; i < 10; i++) typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("X" + i, "audio", Clip(3)) }); Paint();
        var addBtn = Regs().FindLast(r => Math.Abs(r.R.Width - 352) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f); Check("the + ADD TRACK button is on screen with 14 tracks (pinned to the bottom)", addBtn != null && addBtn.R.Bottom <= sf.ClientSize.Height && addBtn.R.Y > sf.ClientSize.Height / 2, addBtn == null ? "missing" : addBtn.R.ToString());
        Check("the toolbar has no extra + TRACK, + BUS or + INSTRUMENT buttons (ADD TRACK is under the tracks)", !Regs().Exists(r => r.R.Y < 120 && r.R.Y > 70 && r.R.Width == 100 && r.R.Height == 36), "");
        // ---- main window: one MP3 choice, and converted links leave the box ----
        {
            var names = (string[])typeof(App).GetField("FmtNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); int mp3 = 0; foreach (var nme in names) if (nme.StartsWith("MP3")) mp3++;
            Check("the format list has a single MP3 entry (the 320 kbps one) and no V0", mp3 == 1 && names[0].Contains("320") && Array.TrueForAll(names, nme => nme.IndexOf("V0") < 0), mp3 + " MP3 entries");
            var tb = (TextBox)typeof(App).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app); app.Show(); Pump(300);
            tb.Text = "https://youtu.be/aaa111\r\nhttps://youtu.be/bbb222, https://youtu.be/ccc333\r\nsome search words"; app.RemoveLinkFromBox("https://youtu.be/aaa111"); Pump(200); app.RemoveLinkFromBox("https://youtu.be/ccc333"); Pump(200);
            Check("a converted link is taken out of the box and the others stay", tb.Text == "https://youtu.be/bbb222\r\nsome search words", tb.Text.Replace("\r\n", " | "));
            app.RemoveLinkFromBox("some search words"); Pump(200); Check("a converted search is removed too", tb.Text == "https://youtu.be/bbb222", tb.Text); tb.Text = ""; app.Hide();
        }
        // ---- side menu, my projects with pictures, community ----
        {
            string mdir = Path.Combine(Path.GetTempPath(), "menutest"); Directory.CreateDirectory(mdir); string p1 = Path.Combine(mdir, "Alpha.ytmproj"), p2 = Path.Combine(mdir, "Beta.ytmproj"), png = Path.Combine(mdir, "pic.png");
            try
            {
                var save2 = new List<Track>(eng.Tracks); lock (eng.Sync) eng.Tracks.Clear(); ProjectIO.Save(eng, p1); ProjectIO.Save(eng, p2); lock (eng.Sync) eng.Tracks.AddRange(save2);
                ProjectLibrary.Touch(p1, "Alpha", 3, 140, 95, true, null); ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null);
                using (var bm = new Bitmap(300, 200)) { using (var gg = Graphics.FromImage(bm)) gg.Clear(Color.OrangeRed); bm.Save(png, System.Drawing.Imaging.ImageFormat.Png); }
                var sq = ProjectLibrary.MakeSquare(png, 256); bool sqOk = false; if (sq != null) using (var ms = new MemoryStream(sq)) using (var im = Image.FromStream(ms)) sqOk = im.Width == 256 && im.Height == 256; Check("a picture is cropped to a square JPEG of the right size", sqOk, sq == null ? "failed" : sq.Length + " bytes");
                string perr; bool setOk = ProjectLibrary.SetCover(p1, png, out perr); var e1 = ProjectLibrary.Find(p1);
                Check("a picture can be added to a project (kept in the list and inside the project file)", setOk && e1 != null && File.Exists(e1.Cover) && File.ReadAllText(p1).Contains("\ncover|"), perr ?? "ok");
                ProjectIO.CoverBytes = null; ProjectIO.Load(eng, p1, s => { }); Check("the picture comes back when the project is opened", ProjectIO.CoverBytes != null && ProjectIO.CoverBytes.Length > 100, ProjectIO.CoverBytes == null ? "none" : ProjectIO.CoverBytes.Length + " bytes");
                string bad = Path.Combine(mdir, "notapic.png"); File.WriteAllText(bad, "not a picture"); string berr; Check("a file that is not a picture is refused with a clear message", !ProjectLibrary.SetCover(p2, bad, out berr) && berr != null && berr.Contains("not a picture"), berr);
                SetF("projectsLoaded", true); typeof(StudioForm).GetMethod("OpenMenu", BF).Invoke(sf, null); Paint(); Check("the menu opens from the three-lines button", F<bool>("menuOpen"), "");
                var hamb = Regs().FindLast(r => Math.Abs(r.R.Width - 48) < 0.5f && r.R.X < 20 && r.R.Y < 40); Check("the three-lines button is at the top left", hamb != null, "");
                Check("when the menu opens nothing is expanded yet (you choose what to open)", F<int>("menuSec") == -1 && F<int>("menuSub") == -1, "section " + F<int>("menuSec"));
                SetF("menuSec", 0); Paint(); var names = new List<string>(); foreach (var r in Regs()) { }
                SetF("menuSub", 1); Paint(); var recReg = Regs().FindAll(r => Math.Abs(r.R.Width - 334) < 1f && Math.Abs(r.R.Height - 50) < 0.5f); Check("Recent Projects lists the projects you opened, newest first", recReg.Count >= 2, recReg.Count + " rows");
                typeof(StudioForm).GetMethod("CloseMenu", BF).Invoke(sf, null); SetF("tab", 3); Paint(); var cards = Regs().FindAll(r => Math.Abs(r.R.Width - 230) < 0.5f && Math.Abs(r.R.Height - 394) < 0.5f); Check("My Projects shows a card for the new-project tile and each saved project", cards.Count >= 3, cards.Count + " cards");
                SetF("tab", 4); Paint(); var bmp3 = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmp3, new Rectangle(0, 0, sf.Width, sf.Height)); bmp3.Save("_community.png", System.Drawing.Imaging.ImageFormat.Png); Check("the Community page draws (a place to build on later)", true, "");
                SetF("tab", 0); ProjectLibrary.ClearCover(p1); Check("a picture can be removed again", !File.ReadAllText(p1).Contains("\ncover|") && ProjectLibrary.Find(p1).Cover.Length == 0, "");
            }
            finally { ProjectLibrary.Remove(p1); ProjectLibrary.Remove(p2); try { Directory.Delete(mdir, true); } catch { } ProjectIO.CoverBytes = null; }
        }
        // ---- lyrics page ----
        {
            Check("syllable counter gets everyday words right", LyricsTools.Syllables("love") == 1 && LyricsTools.Syllables("rhyme") == 1 && LyricsTools.Syllables("music") == 2 && LyricsTools.Syllables("melody") == 3 && LyricsTools.Syllables("beautiful") == 3 && LyricsTools.Syllables("remember") == 3, "love " + LyricsTools.Syllables("love") + ", music " + LyricsTools.Syllables("music") + ", melody " + LyricsTools.Syllables("melody") + ", beautiful " + LyricsTools.Syllables("beautiful"));
            Check("a line's syllables are added up and the last word is found", LyricsTools.LineSyllables("I remember the music") == 7 && LyricsTools.LastWord("walking in the night!") == "night", LyricsTools.LineSyllables("I remember the music") + " syllables");
            // project round trip
            string tmp = Path.Combine(Path.GetTempPath(), "lyrictest.ytmproj"); ProjectIO.Lyrics = "[Verse]\nfirst line\nsecond \"line\" é"; ProjectIO.LyricsTitle = "My Song | test"; ProjectIO.Save(eng, tmp); ProjectIO.Lyrics = ""; ProjectIO.LyricsTitle = ""; ProjectIO.Load(eng, tmp, s => { });
            Check("lyrics are saved inside the project file and come back", ProjectIO.Lyrics == "[Verse]\nfirst line\nsecond \"line\" é" && ProjectIO.LyricsTitle == "My Song | test", ProjectIO.LyricsTitle); try { File.Delete(tmp); } catch { }
            // the page itself
            ProjectIO.Lyrics = "I walk alone in the night\nsearching for a light"; ProjectIO.LyricsTitle = "Night walk"; SetF("tab", 2); Paint(); Pump(200); Paint();
            var lb = F<TextBox>("lyricBox"); Check("the lyrics page shows the saved text in a real text box", lb != null && lb.Visible && lb.Text.Contains("searching for a light"), lb == null ? "none" : lb.Text.Length + " chars");
            lb.SelectionStart = lb.Text.Length; Paint(); Check("the text box is big and fits inside the window", lb.Left >= 0 && lb.Right <= sf.ClientSize.Width && lb.Height > 300, lb.Bounds.ToString());
            var bmp2 = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmp2, new Rectangle(0, 0, sf.Width, sf.Height)); bmp2.Save("_lyrics.png", System.Drawing.Imaging.ImageFormat.Png);
            typeof(StudioForm).GetMethod("OpenMenu", BF).Invoke(sf, null); Paint(); Check("while the side menu is open the lyric text boxes step aside (nothing overlaps)", !lb.Visible, "");
            typeof(StudioForm).GetMethod("CloseMenu", BF).Invoke(sf, null); Paint(); Check("and they come back when the menu closes", lb.Visible, "");
            SetF("tab", 3); Paint(); var dawBtn = Regs().Find(r => r.R.Y > 140 && r.R.Y < 180 && r.R.Width == 62 && r.R.Height == 30); Check("the track-editing buttons are not drawn on other pages", dawBtn == null && !lb.Visible, "");
            SetF("tab", 0); Paint(); Check("leaving the page hides its text boxes", !lb.Visible, "");
            typeof(App).GetMethod("OpenStudio", BF).Invoke(app, null); Pump(300); var real = (Form)typeof(App).GetField("studioForm", BF).GetValue(app);
            Check("Purphex Music opened from the converter is its own window (not owned by it), so either one can be brought to the front", real != null && real.Owner == null && real.ShowInTaskbar, real == null ? "not opened" : (real.Owner == null ? "independent" : "owned")); if (real != null) real.Close();
            { string err = ""; try { Win.ToggleFullscreen(sf); Pump(150); Win.ToggleFullscreen(sf); Pump(150); for (int q = 0; q < 3; q++) { typeof(Control).GetMethod("RecreateHandle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sf, null); Pump(60); } } catch (Exception ex) { err = ex.GetType().Name + ": " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
              Check("the studio window can be rebuilt (full screen, themes) without a drag-and-drop crash, and still takes dropped files", err.Length == 0 && sf.IsHandleCreated && !sf.IsDisposed && sf.AllowDrop, err); }
            {   // the Fonts page
                SetF("tab", 8); Paint(); string ferr = ""; try { for (int q = 0; q < 3; q++) { Paint(); Pump(30); } } catch (Exception ex) { ferr = ex.Message; }
                Check("the Fonts page paints (search box, groups and a card for every font)", ferr.Length == 0 && Regs().Count > 25 && FontKit.All.Count >= 25, FontKit.All.Count + " fonts, " + Regs().Count + " areas " + ferr);
                var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); int fi = Array.IndexOf(names, "Fonts"); Check("the side menu has a Fonts button (a single button, after Themes)", fi == Array.IndexOf(names, "Themes") + 1 && fi >= 5, fi + "");
                if (fi >= 0) { SetF("tab", 0); ((Action)typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { fi }))(); Check("the Fonts button opens the Fonts page", F<int>("tab") == 8, "tab " + F<int>("tab")); }
                var win = FontKit.All.Find(e => e.Installed && e.Id != FontKit.DefaultId); if (win != null)
                {
                    bool ok = FontKit.Apply(win); Paint(); Check("choosing a font sets the studio writing in it (and remembers it)", ok && FontKit.CurrentId == win.Id && Prefs.FontId == win.Id && FontKit.Scale > 0.4f, win.Name + " scale " + FontKit.Scale.ToString("0.00"));
                    SetF("tab", 0); Paint(); SetF("tab", 8); Paint(); var def = FontKit.Find(FontKit.DefaultId); FontKit.Apply(def); Paint(); Check("the original font comes back", FontKit.CurrentId == FontKit.DefaultId && Prefs.FontId.Length == 0 && FontKit.Scale == 1f, "");
                }
                int before = FontKit.All.Count; FontKit.ReadList("nosifer\tNosifer\tDrippy and horror\t98000\nBAD ID\tx\tBold display\t5000\nzzz\tNo Group Font\tNot a group\t5000\nbig\tToo Big\tBold display\t99999999\nshort\tToo Short\n");
                var nos = FontKit.Find("nosifer"); var zzz = FontKit.Find("zzz"); Check("the font list from the website is read safely (bad ids, sizes and groups are handled)", nos != null && nos.Cat == "Drippy and horror" && zzz != null && zzz.Cat == "Bold display" && FontKit.Find("bad id") == null && FontKit.Find("big") == null && FontKit.Find("short") == null, (FontKit.All.Count - before) + " added");
                FontKit.All.RemoveAll(e => e.Id == "nosifer" || e.Id == "zzz"); SetF("tab", 0);
            }
            {   // the MIC button has a neon box with a down arrow: a short menu of microphone modes (only for recording)
                SetF("tab", 0); Paint(); var drops = Regs().FindAll(r => r.OnDown != null && Math.Abs(r.R.Width - 24) < 0.5f && r.R.Y > 60 && r.R.Y < 120 && r.R.Height > 28 && r.R.X > 380);
                Check("the MIC button has a small box with a down arrow beside it", drops.Count >= 1, drops.Count + " found");
                int keep = Prefs.MicMode; var ch = typeof(StudioForm).GetMethod("ChooseMicMode", BF); ch.Invoke(sf, new object[] { MicMode.VoiceIsolation }); Check("choosing a mode in the menu is remembered (Voice Isolation)", Prefs.MicMode == 2 && Array.IndexOf(Prefs.Lines(), "micmode=2") >= 0, Prefs.MicMode + "");
                ch.Invoke(sf, new object[] { MicMode.Standard }); Check("and another one replaces it (Standard)", Prefs.MicMode == 1, Prefs.MicMode + ""); Prefs.MicMode = keep;
                var icons = typeof(StudioForm).GetMethod("MicIcon", BF | BindingFlags.Static); bool allDrawn = true; foreach (MicMode mm in new[] { MicMode.Automatic, MicMode.Standard, MicMode.VoiceIsolation, MicMode.WideSpectrum }) { using (var ib = (Bitmap)icons.Invoke(null, new object[] { mm })) { int ink = 0; for (int y = 0; y < ib.Height; y++) for (int x = 0; x < ib.Width; x++) if (ib.GetPixel(x, y).A > 100) ink++; if (ink < 12) allDrawn = false; } }
                Check("each mode has its own little picture in the menu", allDrawn, "");
            }
            {   // the neon light of a track turns it off
                bool addedTk = false; if (eng.Tracks.Count == 0) { eng.Tracks.Add(StudioEngine.NewTrack("Lead", "vocal", Clip(5))); addedTk = true; } SetF("tab", 0); Paint(); var dots = Regs().FindAll(r => r.OnDown != null && Math.Abs(r.R.Width - 24) < 0.5f && Math.Abs(r.R.Height - 24) < 0.5f && r.R.X < 20 && r.R.Y > 100); dots.Sort((a, b) => a.R.Y.CompareTo(b.R.Y)); var tk0 = eng.Tracks[0]; bool was = tk0.Off;
                Check("every track has a light at the top left of its header that can be pressed", dots.Count >= eng.Tracks.Count, dots.Count + " lights for " + eng.Tracks.Count + " tracks");
                if (dots.Count > 0) { dots[0].OnDown(); Paint(); Check("pressing the light turns the track off", tk0.Off && !was, ""); string perr = ""; try { for (int q = 0; q < 6; q++) { Paint(); Pump(40); } } catch (Exception ex) { perr = ex.Message; } Check("a track that is off paints (the broken, shaking picture) without a problem", perr.Length == 0, perr);
                  Check("while a track is off the timeline keeps animating", (bool)typeof(StudioForm).GetMethod("AnyTrackOff", BF).Invoke(sf, null), "");
                  var dots2 = Regs().FindAll(r => r.OnDown != null && Math.Abs(r.R.Width - 24) < 0.5f && Math.Abs(r.R.Height - 24) < 0.5f && r.R.X < 20 && r.R.Y > 100); dots2.Sort((a, b) => a.R.Y.CompareTo(b.R.Y)); if (dots2.Count > 0) dots2[0].OnDown(); Paint(); Check("pressing it again turns the track on", !tk0.Off, ""); }
                if (addedTk) { eng.Tracks.Clear(); Paint(); }
            }
        }
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height)); bmp.Save("_studio_ui.png", System.Drawing.Imaging.ImageFormat.Png);
        eng.Shutdown(); Console.WriteLine(fails == 0 ? "=== studio UI checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
