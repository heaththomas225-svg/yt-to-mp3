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
    {
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("A", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("B", "beat", Clip(60)));
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(500); Paint();
        Check("tracks are thinner than before (104 px instead of 128)", F<int>("LaneH") == 104, "lane height " + F<int>("LaneH"));
        double spp0 = F<double>("spp"); Check("the whole song fits the view at the start (auto-fit)", F<bool>("autoFit") && spp0 > 100, "frames per pixel " + spp0.ToString("0"));
        // overview bar: the 16 px strip with a double-click handler
        var ob = Regs().FindLast(r => r.OnDouble != null && Math.Abs(r.R.Height - 16) < 0.5f && r.R.Y > 600); Check("the overview bar is on screen", ob != null, "");
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
        // add-track strips: one above each track (and one below the last), inserting where asked
        Paint(); int strips = 0; foreach (var r in Regs()) if (Math.Abs(r.R.Width - 250) < 0.5f && Math.Abs(r.R.Height - 16) < 0.5f) strips++;
        Check("there is an add-track strip above every track and one below the last", strips == 3, strips + " strips for 2 tracks");
        SetF("nextInsert", 1); typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("NEW", "audio", Clip(5)) });
        Check("a track added from a strip lands exactly there and is selected", eng.Tracks.Count == 3 && eng.Tracks[1].Name == "NEW" && F<int>("selTrack") == 1, string.Join(", ", eng.Tracks.ConvertAll(t => t.Name).ToArray()));
        typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("LAST", "audio", Clip(5)) });
        Check("a normal add still goes to the end", eng.Tracks[eng.Tracks.Count - 1].Name == "LAST", "");
        // Open: a .ytmproj goes to the project loader (the dialog also lists mp3, wav, flac... and adds them as tracks)
        { string ptmp = Path.Combine(Path.GetTempPath(), "opentest.ytmproj"); var save = new List<Track>(eng.Tracks); lock (eng.Sync) eng.Tracks.Clear(); ProjectIO.Save(eng, ptmp); lock (eng.Sync) eng.Tracks.AddRange(save);
          sf.PromptImportPublic(ptmp); Pump(1500); Check("a project file chosen in Open (or dropped on the window) opens as a project", F<string>("projPath") == ptmp && eng.Tracks.Count == 0, "tracks " + eng.Tracks.Count + ", path " + F<string>("projPath"));
          string exts = (string)typeof(StudioForm).GetField("AudioExts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); Check("the Open dialog lists mp3, wav, flac, m4a, ogg and more", exts.Contains("*.mp3") && exts.Contains("*.wav") && exts.Contains("*.flac") && exts.Contains("*.m4a") && exts.Contains("*.ogg"), exts); try { File.Delete(ptmp); } catch { } }
        // the + ADD TRACK button must always be on screen, even when the tracks run off the bottom
        for (int i = 0; i < 10; i++) typeof(StudioForm).GetMethod("AddTrack", BF).Invoke(sf, new object[] { StudioEngine.NewTrack("X" + i, "audio", Clip(3)) }); Paint();
        var addBtn = Regs().FindLast(r => Math.Abs(r.R.Width - 230) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f); Check("the + ADD TRACK button is on screen with 14 tracks (pinned to the bottom)", addBtn != null && addBtn.R.Bottom <= sf.ClientSize.Height && addBtn.R.Y > sf.ClientSize.Height / 2, addBtn == null ? "missing" : addBtn.R.ToString());
        var tbtn = Regs().FindLast(r => r.R.Y < 120 && r.R.Y > 70 && r.R.Width == 100 && r.R.Height == 36); Check("there is a + TRACK button in the toolbar", tbtn != null, "");
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
            SetF("tab", 0); Paint(); Check("leaving the page hides its text boxes", !lb.Visible, "");
        }
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height)); bmp.Save("_studio_ui.png", System.Drawing.Imaging.ImageFormat.Png);
        eng.Shutdown(); Console.WriteLine(fails == 0 ? "=== studio UI checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
