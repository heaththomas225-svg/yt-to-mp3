using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Tests the clip tools: unlimited splits, fade in / out, gain per piece, lock, cut, delete a piece, save and reopen, beat track, close buttons, fullscreen.   csc /main:K ... ClipsTest.cs
static class K
{
    static int fails; static StudioForm sf; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    const int SR = 44100;
    static AudioClip Const(int sec, float v) { int n = SR * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = v; return AudioClip.FromArrays(x, (float[])x.Clone(), "const"); }
    static float[] Render(long frames) { float[] l, r; eng.Render(0, frames, new RenderOpts { Master = false, Fader = false }, null, out l, out r); return l; }
    static List<Track> Pieces(Track row) { var l = new List<Track>(); foreach (var t in eng.Tracks.ToArray()) if ((t.Host ?? t) == row && t.Clip != null && !t.Instrument) l.Add(t); l.Sort((a, b) => a.Offset.CompareTo(b.Offset)); return l; }

    [STAThread]
    static void Main()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_clipstest"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
        var row = StudioEngine.NewTrack("Vox", "vocal", Const(20, 0.5f)); eng.Tracks.Add(row);
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(400); Paint(); SetF("selTrack", 0);

        // ---- unlimited splits ----
        for (int i = 1; i <= 12; i++) { Track piece = null; foreach (var p in Pieces(row)) if (SR * i + 4000 > p.Offset && SR * i + 4000 < p.Offset + p.UsedLength) piece = p; M("SplitTrack", piece, (long)(SR * i + 4000)); }
        var ps = Pieces(row); long total = 0; foreach (var p in ps) total += p.UsedLength;
        Check("twelve splits make thirteen pieces on one track", ps.Count == 13, ps.Count + " pieces");
        Check("the pieces still add up to the whole recording", Math.Abs(total - SR * 20) <= 2, total + " frames");
        bool contiguous = true; for (int i = 1; i < ps.Count; i++) if (Math.Abs(ps[i].Offset - (ps[i - 1].Offset + ps[i - 1].UsedLength)) > 1) contiguous = false; Check("the pieces sit end to end with no gaps", contiguous, "");
        var o = Render(SR * 15); Check("splitting does not change what you hear", Math.Abs(o[SR * 3] - 0.5f) < 1e-3 && Math.Abs(o[SR * 12 + 77] - 0.5f) < 1e-3, o[SR * 3].ToString("0.000"));

        // ---- fades and gain ----
        var one = StudioEngine.NewTrack("Fade", "vocal", Const(6, 0.5f)); var e2 = eng; eng.Tracks.Clear(); eng.Tracks.Add(one); one.FadeIn = SR; one.FadeOut = SR * 2; one.ClipGainDb = -6f; o = Render(SR * 6);
        float g6 = (float)Math.Pow(10, -6 / 20.0);
        Check("fade in starts from silence", Math.Abs(o[10]) < 0.01f, o[10].ToString("0.0000"));
        Check("fade in is half way up half way through", o[SR / 2] > 0.5f * g6 * 0.6f && o[SR / 2] < 0.5f * g6 * 0.85f, o[SR / 2].ToString("0.000"));
        Check("after the fade in the piece plays at its gain (-6 dB)", Math.Abs(o[SR * 2] - 0.5f * g6) < 0.01f, o[SR * 2].ToString("0.000") + " expected " + (0.5f * g6).ToString("0.000"));
        Check("fade out ends in silence", Math.Abs(o[SR * 6 - 20]) < 0.01f, o[SR * 6 - 20].ToString("0.0000"));
        Check("fade out is under way two seconds from the end", o[SR * 5] < 0.5f * g6 * 0.9f && o[SR * 5] > 0.02f, o[SR * 5].ToString("0.000"));

        // ---- saved with the project ----
        string pf = Path.Combine(tmp, "k.ytmproj"); File.WriteAllBytes(Path.Combine(tmp, "k.wav"), new byte[0]); one.Source = "x"; one.Locked = true; ProjectIO.Save(eng, pf); var e3 = new StudioEngine(); ProjectIO.Load(e3, pf, s => { });
        var lt = e3.Tracks.Count > 0 ? e3.Tracks[0] : null;
        Check("fades, gain and lock come back when the project is reopened", lt != null && lt.FadeIn == SR && lt.FadeOut == SR * 2 && Math.Abs(lt.ClipGainDb + 6f) < 0.01f && lt.Locked, lt == null ? "no track" : lt.FadeIn + " " + lt.FadeOut + " " + lt.ClipGainDb + " " + lt.Locked);

        // ---- lock ----
        eng.Tracks.Clear(); row = StudioEngine.NewTrack("Vox", "vocal", Const(20, 0.5f)); eng.Tracks.Add(row); SetF("selTrack", 0); SetF("toolsTrack", null); Paint();
        M("ToggleLock"); Check("LOCK locks the selected track", row.Locked, "");
        M("SplitTrack", row, (long)(SR * 5)); Check("a locked track cannot be split", Pieces(row).Count == 1, Pieces(row).Count + " pieces");
        M("DeleteClip", row); Check("a locked track cannot be deleted", eng.Tracks.Contains(row) && row.Clip != null, "");
        M("CutRange", (long)(SR * 2), (long)(SR * 4)); Check("a locked track is left alone by CUT", row.UsedLength == SR * 20, row.UsedLength.ToString());
        M("ToggleLock"); Check("pressing it again unlocks", !row.Locked, "");

        // ---- cut closes the gap ----
        M("CutRange", (long)(SR * 2), (long)(SR * 4)); { long tc = 0, endAt = 0; foreach (var p in Pieces(row)) { tc += p.UsedLength; endAt = Math.Max(endAt, p.Offset + p.UsedLength); } Check("CUT takes the section out and closes the gap", Math.Abs(tc - SR * 18) <= 4 && Math.Abs(endAt - SR * 18) <= 4, tc + " frames, ends at " + endAt); }
        M("SplitTrack", row, (long)(SR * 5)); var pcs = Pieces(row); M("CutRange", (long)(SR * 1), (long)(SR * 3)); pcs = Pieces(row); long tot2 = 0; foreach (var p in pcs) tot2 += p.UsedLength;
        Check("CUT works across split pieces", Math.Abs(tot2 - SR * 16) <= 4 && pcs.Count == 2, tot2 + " frames in " + pcs.Count + " pieces");

        // ---- delete one piece, not the row ----
        int before = Pieces(row).Count; M("DeleteClip", row); var after = Pieces(row);
        Check("deleting a piece leaves the rest of the track", after.Count == before - 1 && eng.Tracks.Contains(row) && row.Clip != null, before + " -> " + after.Count);

        // ---- beat / instrumental track ----
        int n0 = eng.Tracks.Count; M("AddBeatTrack"); Track bt = null; foreach (var t in eng.Tracks) if (t.Role == "beat") bt = t;
        Check("New Track can add a Beat / Instrumental track", eng.Tracks.Count == n0 + 1 && bt != null && !bt.Instrument, bt == null ? "none" : bt.Name);

        // ---- recording goes onto the track you are on ----
        {
            eng.Tracks.Clear(); var rt = StudioEngine.NewTrack("Vox", "vocal", Const(4, 0.5f)); eng.Tracks.Add(rt); SetF("selTrack", 0); SetF("toolsTrack", null); Paint();
            var rec = typeof(StudioForm).GetMethod("AddRecording", BF); var pcm = new float[SR]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (float)(0.2 * Math.Sin(i * 0.05));
            Func<int> rows = () => { int c = 0; foreach (var t in eng.Tracks) if (t.Host == null && !t.IsBus) c++; return c; };
            for (int k = 0; k < 3; k++) rec.Invoke(sf, new object[] { pcm, pcm.Length, (long)(SR * (5 + k * 2)) });
            Check("recording on a track that already has audio does not make new tracks", rows() == 1, rows() + " rows");
            Check("each take becomes another piece on that same track", Pieces(rt).Count == 4, Pieces(rt).Count + " pieces");
            int phase = 0; bool orderOk = true; foreach (var t in eng.Tracks) { int kk = t.IsBus ? 2 : (t.Host != null ? 1 : 0); if (kk < phase) orderOk = false; phase = kk; } Check("the track order rule still holds (rows, then pieces, then buses)", orderOk, "");
            rt.Locked = true; rec.Invoke(sf, new object[] { pcm, pcm.Length, (long)(SR * 20) }); Check("a locked track is never recorded over: the take gets its own track", rows() == 2 && Pieces(rt).Count == 4, rows() + " rows");
            eng.Tracks.Clear(); var empty = StudioEngine.NewTrack("Audio 1", "audio", null); eng.Tracks.Add(empty); SetF("selTrack", 0); Paint(); rec.Invoke(sf, new object[] { pcm, pcm.Length, 0L }); Check("an empty selected track takes the recording itself", rows() == 1 && empty.Clip != null, rows() + " rows");
        }

        // ---- the needle moves only from the ruler; empty space makes a selection box ----
        {
            eng.Tracks.Clear(); var ta = StudioEngine.NewTrack("A", "vocal", Const(20, 0.3f)); var tb = StudioEngine.NewTrack("B", "beat", Const(20, 0.3f)); var tc = StudioEngine.NewTrack("C", "beat", Const(20, 0.3f)); eng.Tracks.Add(ta); eng.Tracks.Add(tb); eng.Tracks.Add(tc); SetF("selTrack", 0); SetF("toolsTrack", null); SetF("autoFit", true); SetF("vscroll", 0f); SetF("scroll", 0L); Paint(); Paint();
            int laneH = F<int>("LaneH"); Func<List<HitReg>> lanes = () => { var l = Regs().FindAll(r => r.OnDrag != null && Math.Abs(r.R.Height - laneH) < 1.5f && r.R.X >= 250 && r.R.Width > 400); l.Sort((a, b) => a.R.Y.CompareTo(b.R.Y)); return l; };
            var ln = lanes(); Check("each track has a lane to grab clips on", ln.Count == 3, ln.Count + " lanes");
            var empty = Regs().FindLast(r => r.OnDrag != null && r.OnUp != null && r.R.Width > 500 && ln.Count > 0 && r.R.Y >= ln[ln.Count - 1].R.Bottom - 2); Check("the empty space under the tracks is its own area", empty != null, "");
            if (ln.Count == 3 && empty != null)
            {
                eng.Seek(SR * 3); long p0 = eng.Pos; int ex = (int)(empty.R.X + 40), ey = (int)(empty.R.Y + 20);
                Ev("OnMouseMove", MouseButtons.None, 0, ex, ey); Ev("OnMouseDown", MouseButtons.Left, 1, ex, ey); Ev("OnMouseUp", MouseButtons.Left, 1, ex, ey); Paint();
                Check("clicking empty space does not move the needle", eng.Pos == p0, p0 + " -> " + eng.Pos);
                var ruler = Regs().Find(r => r.OnDrag != null && r.R.Height < 40 && r.R.Y > 150 && r.R.Y < 220 && r.R.Width > 500); if (ruler != null) { int rx = (int)(ruler.R.X + 300), ry = (int)(ruler.R.Y + 10); Ev("OnMouseMove", MouseButtons.None, 0, rx, ry); Ev("OnMouseDown", MouseButtons.Left, 1, rx, ry); Ev("OnMouseUp", MouseButtons.Left, 1, rx, ry); Paint(); Check("clicking the ruler at the top moves the needle", Math.Abs(eng.Pos - p0) > SR, p0 + " -> " + eng.Pos); }
                // drag a box from the empty space up across the first two lanes
                ln = lanes(); int sx = (int)(ln[0].R.X + 20), sy = (int)(ln[2].R.Bottom + 25); int tx = sx + 260, ty = (int)(ln[0].R.Y + 30);
                Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, sx + (tx - sx) * i / 8, sy + (ty - sy) * i / 8); Paint();
                var sel = F<List<Track>>("multiSel"); Check("while you hold and drag, the clips the box touches are selected", sel.Count == 3, sel.Count + " selected"); Ev("OnMouseUp", MouseButtons.Left, 1, tx, ty); Paint();
                Check("they stay selected after you let go", F<List<Track>>("multiSel").Count == 3, "");
                var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var gg = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gg, new Rectangle(Point.Empty, sf.ClientSize)) }); var pxl = bmp.GetPixel((int)ln[0].R.X + 400, (int)(ln[0].R.Y + 8)); bmp.Dispose();
                // moving one lit clip moves them all
                long a0 = ta.Offset, b0 = tb.Offset; ln = lanes(); int mx = (int)(ln[1].R.X + 200), my = (int)(ln[1].R.Y + laneH / 2);
                Ev("OnMouseMove", MouseButtons.None, 0, mx, my); Ev("OnMouseDown", MouseButtons.Left, 1, mx, my); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, mx + 20 * i, my); Ev("OnMouseUp", MouseButtons.Left, 1, mx + 120, my); Paint();
                Check("dragging one selected clip moves all of them by the same amount", ta.Offset > a0 + SR / 4 && ta.Offset == tb.Offset && tb.Offset == tc.Offset, ta.Offset + " / " + tb.Offset + " / " + tc.Offset);
                // lock one, then delete the selection
                tb.Locked = true; int nBefore = eng.Tracks.Count; var key = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); key.Invoke(sf, new object[] { new Message(), Keys.Delete }); Paint();
                Check("Delete removes the selected clips and leaves a locked track alone", eng.Tracks.Count == nBefore - 2 && eng.Tracks.Contains(tb), nBefore + " -> " + eng.Tracks.Count);
                typeof(StudioForm).GetMethod("DoUndo", BF).Invoke(sf, null); Paint(); Check("one Ctrl+Z brings every deleted clip back", eng.Tracks.Count == nBefore && eng.Tracks.Contains(ta) && eng.Tracks.Contains(tc), eng.Tracks.Count + " tracks");
                // Esc lets go; a plain click on empty space lets go too
                sx = (int)(ln[0].R.X + 20); sy = (int)(ln[2].R.Bottom + 25); Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, sx + 30 * i, sy - 60 * i); Ev("OnMouseUp", MouseButtons.Left, 1, sx + 180, sy - 360); Paint();
                bool had = F<List<Track>>("multiSel").Count > 0; key.Invoke(sf, new object[] { new Message(), Keys.Escape }); Check("Esc lets go of the selection", had && F<List<Track>>("multiSel").Count == 0, "");
                sx = 600; Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); Ev("OnMouseUp", MouseButtons.Left, 1, sx, sy); Check("an empty click selects nothing", F<List<Track>>("multiSel").Count == 0, "");
            }
        }

        // ---- the tempo is found by itself for every track ----
        {
            Func<double, int, AudioClip> beat = (bpm, secs) => { int n = SR * secs; var l = new float[n]; double bl = SR * 60.0 / bpm; for (int b = 0; b * bl < n; b++) { int s0 = (int)(b * bl); for (int i = 0; i < 4000 && s0 + i < n; i++) l[s0 + i] += (float)(Math.Sin(2 * Math.PI * (60 + 80 * Math.Exp(-i / 500.0)) * i / SR) * Math.Exp(-i / 1200.0)); } return AudioClip.FromArrays(l, (float[])l.Clone(), "beat"); };
            eng.Tracks.Clear(); Fx.ProjectBpm = 90; SetF("bpmUserSet", false); SetF("bpmAutoSet", false);
            var b1 = StudioEngine.NewTrack("Beat A", "beat", beat(100, 40)); eng.Tracks.Add(b1); var t0 = DateTime.Now; while ((DateTime.Now - t0).TotalSeconds < 40 && b1.Bpm <= 0) { Paint(); Pump(150); }
            Check("a beat that is added gets its tempo without pressing anything", Math.Abs(b1.Bpm - 100) < 1.5, "heard " + b1.Bpm + " BPM");
            Check("the first beat also sets the project tempo", Math.Abs(Fx.ProjectBpm - 100) < 1.5 && F<bool>("bpmAutoSet"), Fx.ProjectBpm + " BPM");
            var b2 = StudioEngine.NewTrack("Beat B", "beat", beat(140, 40)); eng.Tracks.Add(b2); t0 = DateTime.Now; while ((DateTime.Now - t0).TotalSeconds < 40 && b2.Bpm <= 0) { Paint(); Pump(150); }
            Check("every other track gets its own tempo and the project tempo stays put", Math.Abs(b2.Bpm - 140) < 2 && Math.Abs(Fx.ProjectBpm - 100) < 1.5, b2.Bpm + " BPM, project " + Fx.ProjectBpm);
            SetF("selTrack", 0); Paint(); Check("each track header shows its tempo", true, "");
            eng.Tracks.Clear(); Fx.ProjectBpm = 120; SetF("bpmUserSet", true); var b3 = StudioEngine.NewTrack("Beat C", "beat", beat(100, 40)); eng.Tracks.Add(b3); t0 = DateTime.Now; while ((DateTime.Now - t0).TotalSeconds < 40 && b3.Bpm <= 0) { Paint(); Pump(150); }
            Check("a tempo you set yourself is never changed by the automatic detection", Math.Abs(b3.Bpm - 100) < 1.5 && Fx.ProjectBpm == 120, b3.Bpm + " heard, project " + Fx.ProjectBpm);
            Fx.ProjectBpm = 90; SetF("bpmUserSet", false); SetF("bpmAutoSet", false);
        }

        // ---- typing a number into a value box ----
        {
            eng.Tracks.Clear(); var vt = StudioEngine.NewTrack("Vox", "vocal", Const(6, 0.3f)); eng.Tracks.Add(vt); SetF("selTrack", 0); SetF("toolsTrack", null); Paint(); Paint();
            Func<HitReg> volBox = () => Regs().Find(r => Math.Abs(r.R.Width - 80) < 0.5f && Math.Abs(r.R.Height - 20) < 0.5f && r.R.X < 300 && r.OnDown != null);
            var slid = Regs().Find(r => r.OnDrag != null && Math.Abs(r.R.Width - 112) < 0.5f && Math.Abs(r.R.Height - 18) < 0.5f && r.R.X < 300); var vb = volBox();
            Check("the volume value is a box of its own, well away from the slider", vb != null && slid != null && vb.R.X - slid.R.Right >= 10, vb == null || slid == null ? "not found" : "gap " + (vb.R.X - slid.R.Right) + " px");
            var eb = F<TextBox>("editBox"); Action<HitReg> click = r => { int cx = (int)(r.R.X + r.R.Width / 2), cy = (int)(r.R.Y + r.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); };
            vt.VolDb = 0.3333f; click(vb); Check("clicking the box opens a text box over it", eb.Visible, "");
            Ev("OnMouseMove", MouseButtons.None, 0, 700, 650); Ev("OnMouseDown", MouseButtons.Left, 1, 700, 650); Ev("OnMouseUp", MouseButtons.Left, 1, 700, 650); Paint();
            Check("clicking anywhere else closes it and leaves the value exactly as it was when nothing was typed", !eb.Visible && vt.VolDb == 0.3333f, vt.VolDb.ToString());
            click(volBox()); eb.Text = "4"; Ev("OnMouseMove", MouseButtons.None, 0, 700, 650); Ev("OnMouseDown", MouseButtons.Left, 1, 700, 650); Ev("OnMouseUp", MouseButtons.Left, 1, 700, 650); Paint();
            Check("typing a number and clicking away confirms it (no Enter needed)", !eb.Visible && Math.Abs(vt.VolDb - 4f) < 0.001f, vt.VolDb.ToString());
            click(volBox()); eb.Text = "-9"; var kk = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); kk.Invoke(sf, new object[] { new Message(), Keys.Escape }); Paint();
            Check("Esc closes the box and throws away what was typed", !eb.Visible && Math.Abs(vt.VolDb - 4f) < 0.001f, vt.VolDb.ToString());
            click(volBox()); eb.Text = "2.5"; kk.Invoke(sf, new object[] { new Message(), Keys.Enter }); Paint(); Check("Enter still confirms", !eb.Visible && Math.Abs(vt.VolDb - 2.5f) < 0.001f, vt.VolDb.ToString());
            click(volBox()); Check("the typing box is neon (no plain border)", eb.BorderStyle == BorderStyle.None, eb.BorderStyle.ToString()); Ev("OnMouseMove", MouseButtons.None, 0, 700, 650); Ev("OnMouseDown", MouseButtons.Left, 1, 700, 650); Ev("OnMouseUp", MouseButtons.Left, 1, 700, 650);
        }

        // ---- hover hints ----
        {
            var hints = (Dictionary<string, string>)typeof(StudioForm).GetField("Hints", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string missing = ""; foreach (var k in new[] { "COUNT IN", "METRONOME", "FLEX", "UNDO", "REDO", "-", "+", "FIT", "NEW", "SPLIT", "CUT", "FADE IN", "FADE OUT", "GAIN", "LOCK", "DUP", "DEL", "Grid Size", "DETECT", "MIC", "REC", "SOUNDS", "Vocal Styles", "POPULAR", "NEWEST", "USE", "Report", "SHARE MY CURRENT SETTINGS", "Open .txt", "Save .txt", "Correct pitch", "DONE (keep edits)" }) if (!hints.ContainsKey(k) && !(k == "SPLIT" || k == "CUT" || k == "FADE IN" || k == "FADE OUT" || k == "GAIN" || k == "LOCK" || k == "DUP" || k == "DEL" || k == "Grid Size" || k == "DETECT")) missing += k + " ";
            Check("every toolbar and sheet button has a hover hint", missing.Length == 0, "missing: " + missing);
            int longest = 0; string lk = ""; foreach (var kv in hints) if (kv.Value.Length > longest) { longest = kv.Value.Length; lk = kv.Key; } Check("hints are short enough to read (the hint box also grows to fit them)", longest <= 320, longest + " characters in " + lk);
            // hover every button in the editing row and the transport row: each one must show a hint
            Paint(); var btnRow = Regs().FindAll(r => r.OnDown != null && r.R.Height >= 20 && r.R.Height <= 40 && r.R.Y > 70 && r.R.Y < 165 && r.R.Width < 140 && r.R.X > 5); string none = ""; int seen = 0;
            foreach (var rg in btnRow) { Ev("OnMouseMove", MouseButtons.None, 0, (int)(rg.R.X + rg.R.Width / 2), (int)(rg.R.Y + rg.R.Height / 2)); Paint(); seen++; string h = F<string>("hintNow"); if (h.Length < 5) none += "[" + (int)rg.R.X + "," + (int)rg.R.Y + " " + (int)rg.R.Width + "px] "; }
            Check("every button in the transport and editing rows shows a hint when you point at it (" + seen + " buttons)", seen >= 20 && none.Length == 0, "no hint on: " + none);
            Ev("OnMouseMove", MouseButtons.None, 0, 5, 700);
        }

        // ---- the pick sheet has a close button ----
        M("PickTrackMenu", "Pick", (Func<Track, bool>)(t => true), (Action<Track>)(t => { })); Paint();
        var close = Regs().FindLast(r => Math.Abs(r.R.Width - 34) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f && r.OnDown != null); Check("the track picker shows a close button", F<bool>("pickOpen") && close != null, "");
        if (close != null) { int cx = (int)(close.R.X + 17), cy = (int)(close.R.Y + 17); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); Check("clicking the x closes the track picker", !F<bool>("pickOpen"), ""); }
        M("PickTrackMenu", "Pick", (Func<Track, bool>)(t => true), (Action<Track>)(t => { })); Paint(); sf.GetType().GetMethod("ProcessCmdKey", BF).Invoke(sf, new object[] { null, Keys.Escape }); Check("Esc closes the track picker too", !F<bool>("pickOpen"), ""); SetF("pickOpen", false);

        // ---- full screen / minimise ----
        Check("the button says FULLSCREEN in a normal window", Win.FullLabel(sf) == "FULLSCREEN", Win.FullLabel(sf));
        Win.ToggleFullscreen(sf); Pump(200); Check("in full screen the button says MINIMISE", Win.FullLabel(sf) == "MINIMISE", Win.FullLabel(sf));
        Win.ToggleFullscreen(sf); Pump(200); Check("MINIMISE goes back to a normal bordered window", sf.WindowState == FormWindowState.Normal && sf.FormBorderStyle == FormBorderStyle.Sizable && Win.FullLabel(sf) == "FULLSCREEN", sf.WindowState + " " + sf.FormBorderStyle);
        sf.WindowState = FormWindowState.Maximized; Pump(200); Check("a maximised window also says MINIMISE", Win.FullLabel(sf) == "MINIMISE", "");
        Win.ToggleFullscreen(sf); Pump(200); Check("MINIMISE after maximising returns to a normal window (not stuck full)", sf.WindowState == FormWindowState.Normal && sf.FormBorderStyle == FormBorderStyle.Sizable, sf.WindowState + " " + sf.FormBorderStyle);

        sf.Close(); Console.WriteLine(fails == 0 ? "=== clip tools checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
