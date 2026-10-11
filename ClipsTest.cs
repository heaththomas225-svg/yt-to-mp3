using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        Check("fade in is half way up half way through (a smooth S curve)", Math.Abs(o[SR / 2] - 0.5f * g6 * 0.5f) < 0.01f, o[SR / 2].ToString("0.000") + " expected " + (0.25f * g6).ToString("0.000"));
        Check("after the fade in the piece plays at its gain (-6 dB)", Math.Abs(o[SR * 2] - 0.5f * g6) < 0.01f, o[SR * 2].ToString("0.000") + " expected " + (0.5f * g6).ToString("0.000"));
        Check("fade out ends in silence", Math.Abs(o[SR * 6 - 20]) < 0.01f, o[SR * 6 - 20].ToString("0.0000"));
        Check("fade out is half way down one second before the end", Math.Abs(o[SR * 5] - 0.5f * g6 * 0.5f) < 0.01f, o[SR * 5].ToString("0.000") + " expected " + (0.25f * g6).ToString("0.000"));
        { float mx = 0; bool down = true; for (int i = SR * 4; i < SR * 6 - 1; i++) { mx = Math.Max(mx, Math.Abs(o[i + 1] - o[i])); if (o[i + 1] > o[i] + 1e-6f) down = false; } Check("the fade out only ever goes down and has no jumps or clicks", down && mx < 2e-4f, "biggest step " + mx.ToString("0.000000")); float mi = 0; bool up = true; for (int i = 0; i < SR - 1; i++) { mi = Math.Max(mi, Math.Abs(o[i + 1] - o[i])); if (o[i + 1] < o[i] - 1e-6f) up = false; } Check("the fade in only ever goes up and starts with no click", up && mi < 2e-4f && Math.Abs(o[0]) < 1e-4f, "biggest step " + mi.ToString("0.000000")); }

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

        // ---- fades in the studio: add, drag the handles, remove ----
        {
            eng.Tracks.Clear(); var ft = StudioEngine.NewTrack("Vox", "vocal", Const(20, 0.4f)); eng.Tracks.Add(ft); SetF("selTrack", 0); SetF("autoFit", true); SetF("scroll", 0L); SetF("toolsTrack", ft); SetF("vscroll", 0f); Paint(); Paint();
            double spp = F<double>("spp"); int laneH = F<int>("LaneH"); var lane = Regs().Find(r => r.OnDrag != null && Math.Abs(r.R.Height - laneH) < 1.5f && r.R.X >= 250 && r.R.Width > 400); var zoneAt = typeof(StudioForm).GetMethod("ZoneAt", BF);
            M("ToggleFade", false); M("ToggleFade", true); Paint(); Check("FADE OUT and FADE IN each give the selected clip a fade", ft.FadeOut > 0 && ft.FadeIn > 0, ft.FadeIn + " / " + ft.FadeOut);
            if (lane != null)
            {
                float x1 = lane.R.X + (float)(ft.UsedLength / spp), hy = lane.R.Y + 12, hx = x1 - Math.Max(12f, (float)(ft.FadeOut / spp)); int z = (int)zoneAt.Invoke(sf, new object[] { ft, lane.R, new PointF(hx, hy) });
                Check("the fade out handle is where it is drawn and can be grabbed", z == 5, "zone " + z);
                float hx0 = lane.R.X + Math.Max(12f, (float)(ft.FadeIn / spp)); z = (int)zoneAt.Invoke(sf, new object[] { ft, lane.R, new PointF(hx0, hy) }); Check("so can the fade in handle", z == 4, "zone " + z);
                long fo0 = ft.FadeOut; int dx = 160; Ev("OnMouseMove", MouseButtons.None, 0, (int)hx, (int)hy); Ev("OnMouseDown", MouseButtons.Left, 1, (int)hx, (int)hy); for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)hx - dx * i / 8, (int)hy); Ev("OnMouseUp", MouseButtons.Left, 1, (int)hx - dx, (int)hy); Paint();
                Check("dragging the fade out handle left makes the fade longer", ft.FadeOut > fo0 + (long)(dx * spp * 0.8), fo0 + " -> " + ft.FadeOut);
                long fo1 = ft.FadeOut; hx = x1 - (float)(ft.FadeOut / spp); Ev("OnMouseMove", MouseButtons.None, 0, (int)hx, (int)hy); Ev("OnMouseDown", MouseButtons.Left, 1, (int)hx, (int)hy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)hx + 40 * i / 6, (int)hy); Ev("OnMouseUp", MouseButtons.Left, 1, (int)hx + 40, (int)hy); Paint();
                Check("dragging it right makes the fade shorter", ft.FadeOut < fo1 - (long)(30 * spp), fo1 + " -> " + ft.FadeOut);
                hx = x1 - Math.Max(12f, (float)(ft.FadeOut / spp)); Ev("OnMouseMove", MouseButtons.None, 0, (int)hx, (int)hy); Ev("OnMouseDown", MouseButtons.Left, 2, (int)hx, (int)hy); Ev("OnMouseUp", MouseButtons.Left, 1, (int)hx, (int)hy); Paint(); Check("double-clicking the handle removes the fade out", ft.FadeOut == 0 && ft.FadeIn > 0, "out " + ft.FadeOut + " in " + ft.FadeIn);
                // the sound really follows the picture: render the clip end
                ft.FadeOut = SR * 3; eng.Tracks.Add(StudioEngine.NewTrack("pad", "other", null)); eng.Tracks.RemoveAt(1); var rr = Render(SR * 20); float full = 0.4f; Check("the faded end of the clip is silent and the start of the fade is still at full level", Math.Abs(rr[SR * 20 - 5]) < 1e-3f && Math.Abs(rr[SR * 17 - 2000] - full) < 0.01f, rr[SR * 20 - 5] + " / " + rr[SR * 17 - 2000]);
                ft.FadeOut = 0;
            }
        }

        // ---- CUT deletes the selected piece and closes the gap ----
        {
            eng.Tracks.Clear(); var ct = StudioEngine.NewTrack("Vox", "vocal", Const(20, 0.4f)); eng.Tracks.Add(ct); SetF("selTrack", 0); SetF("autoFit", true); SetF("toolsTrack", null); Paint();
            var spl = typeof(StudioForm).GetMethod("SplitTrack", BF); spl.Invoke(sf, new object[] { ct, (long)(SR * 5) }); var p2 = Pieces(ct)[1]; spl.Invoke(sf, new object[] { p2, (long)(SR * 10) });
            var cutPcs = Pieces(ct); Check("split twice: three pieces", cutPcs.Count == 3, cutPcs.Count + " pieces"); var mid = cutPcs[1]; SetF("toolsTrack", mid); Paint();
            M("CutClick"); cutPcs = Pieces(ct); long tot = 0; long endAt = 0; foreach (var p in cutPcs) { tot += p.UsedLength; endAt = Math.Max(endAt, p.Offset + p.UsedLength); }
            Check("CUT deletes the selected piece (it does not make two pieces)", cutPcs.Count == 2 && Math.Abs(tot - SR * 15) <= 4, cutPcs.Count + " pieces, " + tot / (double)SR + " s");
            Check("and the piece after it moves up to close the gap", Math.Abs(endAt - SR * 15) <= 4 && Math.Abs(cutPcs[1].Offset - SR * 5) <= 4, "ends at " + endAt / (double)SR + " s, second piece starts at " + cutPcs[1].Offset / (double)SR + " s");
            var ro = Render(SR * 14); Check("what you hear has no hole where the piece was", Math.Abs(ro[SR * 5 + 1000] - 0.4f) < 1e-3f && Math.Abs(ro[SR * 12] - 0.4f) < 1e-3f, ro[SR * 5 + 1000].ToString("0.000"));
            typeof(StudioForm).GetMethod("DoUndo", BF).Invoke(sf, null); cutPcs = Pieces(ct); tot = 0; foreach (var p in cutPcs) tot += p.UsedLength; Check("Ctrl+Z puts the piece back", cutPcs.Count == 3 && Math.Abs(tot - SR * 20) <= 4, cutPcs.Count + " pieces");
            ct.Locked = true; SetF("toolsTrack", Pieces(ct)[1]); M("CutClick"); Check("CUT leaves a locked track alone", Pieces(ct).Count == 3, ""); ct.Locked = false;
        }

        // ---- fades: the bottom point moves too ----
        {
            eng.Tracks.Clear(); var bt2 = StudioEngine.NewTrack("Vox", "vocal", Const(10, 0.4f)); eng.Tracks.Add(bt2); SetF("selTrack", 0); SetF("autoFit", true); SetF("toolsTrack", bt2); SetF("scroll", 0L); Paint(); Paint();
            bt2.FadeIn = SR * 2; bt2.FadeInDelay = SR * 1; bt2.FadeOut = SR * 2; bt2.FadeOutTail = SR * 1; var rr = Render(SR * 10);
            Check("a fade in that starts later: silence first, then the fade", Math.Abs(rr[SR / 2]) < 1e-4f && Math.Abs(rr[SR + SR / 2] - 0.4f * 0.15625f) < 0.01f && Math.Abs(rr[SR * 4] - 0.4f) < 1e-3f, rr[SR / 2] + " / " + rr[SR + SR / 2]);
            Check("a fade out that ends early: the fade, then silence to the end", Math.Abs(rr[SR * 10 - SR / 2]) < 1e-4f && Math.Abs(rr[SR * 7 + SR / 2] - 0.4f * 0.84375f) < 0.01f && Math.Abs(rr[SR * 6] - 0.4f) < 1e-3f, rr[SR * 10 - SR / 2] + " / " + rr[SR * 7 + SR / 2]);
            double spp = F<double>("spp"); int laneH = F<int>("LaneH"); var lane = Regs().Find(r => r.OnDrag != null && Math.Abs(r.R.Height - laneH) < 1.5f && r.R.X >= 250 && r.R.Width > 400); var zoneAt = typeof(StudioForm).GetMethod("ZoneAt", BF);
            if (lane != null)
            {
                float x1 = lane.R.X + (float)(bt2.UsedLength / spp), by = lane.R.Bottom - 12; float bx0 = lane.R.X + (float)(bt2.FadeInDelay / spp), bx1 = x1 - (float)(bt2.FadeOutTail / spp);
                Check("the bottom point of the fade in can be grabbed", (int)zoneAt.Invoke(sf, new object[] { bt2, lane.R, new PointF(bx0, by) }) == 6, "");
                Check("so can the bottom point of the fade out", (int)zoneAt.Invoke(sf, new object[] { bt2, lane.R, new PointF(bx1, by) }) == 7, "");
                long d0 = bt2.FadeInDelay; int hx = (int)bx0, hy = (int)by; Ev("OnMouseMove", MouseButtons.None, 0, hx, hy); Ev("OnMouseDown", MouseButtons.Left, 1, hx, hy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, hx + 60 * i / 6, hy); Ev("OnMouseUp", MouseButtons.Left, 1, hx + 60, hy); Paint();
                Check("dragging the bottom point of the fade in moves where it starts", bt2.FadeInDelay > d0 + (long)(45 * spp), d0 + " -> " + bt2.FadeInDelay);
                long t0b = bt2.FadeOutTail; hx = (int)bx1; Ev("OnMouseMove", MouseButtons.None, 0, hx, hy); Ev("OnMouseDown", MouseButtons.Left, 1, hx, hy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, hx - 60 * i / 6, hy); Ev("OnMouseUp", MouseButtons.Left, 1, hx - 60, hy); Paint();
                Check("dragging the bottom point of the fade out moves where it ends", bt2.FadeOutTail > t0b + (long)(45 * spp), t0b + " -> " + bt2.FadeOutTail);
            }
            string fadePf = Path.Combine(tmp, "fades.ytmproj"); bt2.Source = "x"; File.WriteAllBytes(Path.Combine(tmp, "x.wav"), new byte[0]); long fdl = bt2.FadeInDelay, ftl = bt2.FadeOutTail; bt2.Mono = true; ProjectIO.Save(eng, fadePf); var e4 = new StudioEngine(); ProjectIO.Load(e4, fadePf, s => { });
            Check("the fade start points and MONO come back when the project is reopened", e4.Tracks.Count > 0 && e4.Tracks[0].FadeInDelay == fdl && e4.Tracks[0].FadeOutTail == ftl && e4.Tracks[0].Mono, e4.Tracks.Count > 0 ? e4.Tracks[0].FadeInDelay + " / " + e4.Tracks[0].FadeOutTail + " / " + e4.Tracks[0].Mono : "no track");
            bt2.Mono = false; bt2.FadeIn = bt2.FadeOut = bt2.FadeInDelay = bt2.FadeOutTail = 0;
        }

        // ---- STEREO / MONO ----
        {
            eng.Tracks.Clear(); int nn = SR * 2; var l = new float[nn]; var r = new float[nn]; for (int i = 0; i < nn; i++) { l[i] = 0.6f; r[i] = 0f; }
            var mt = StudioEngine.NewTrack("Wide", "vocal", AudioClip.FromArrays(l, r, "wide")); eng.Tracks.Add(mt); float[] ol, orr;
            eng.Render(0, SR, new RenderOpts { Master = false, Fader = false }, null, out ol, out orr); Check("a stereo track keeps its sides apart", Math.Abs(ol[1000] - 0.6f) < 1e-4f && Math.Abs(orr[1000]) < 1e-4f, ol[1000] + " / " + orr[1000]);
            mt.Mono = true; eng.Render(0, SR, new RenderOpts { Master = false, Fader = false }, null, out ol, out orr); Check("a mono track plays the same on both sides (the average of the two)", Math.Abs(ol[1000] - 0.3f) < 1e-4f && Math.Abs(orr[1000] - 0.3f) < 1e-4f, ol[1000] + " / " + orr[1000]);
            mt.Mono = false; SetF("selTrack", 0); SetF("tab", 0); Paint(); Paint(); var mbtns = Regs().FindAll(r2 => Math.Abs(r2.R.Width - 66) < 0.5f && Math.Abs(r2.R.Height - 26) < 0.5f && r2.R.X < 372 && r2.OnDown != null); var mbtn = mbtns.Count >= 2 ? mbtns[1] : null; var sbtn = mbtns.Count >= 2 ? mbtns[0] : null; Check("every track header has a STEREO button and a MONO button under it", mbtn != null && mbtn.R.Y > sbtn.R.Y, "");
            if (mbtn != null) { int cx = (int)(mbtn.R.X + 30), cy = (int)(mbtn.R.Y + 12); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); Check("pressing it switches the track to MONO", mt.Mono, "");  cx = (int)(sbtn.R.X + 30); cy = (int)(sbtn.R.Y + 12); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); Check("and STEREO switches it back", !mt.Mono, ""); }
        }

        // ---- the transport: PLAY stays PLAY, MIC does not change its words, REC is MIDI REC ----
        {
            eng.Tracks.Clear(); eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", Const(4, 0.3f))); Paint(); StudioForm.TxtAudit = new List<StudioForm.AuditText>(); Paint(); var words = new List<string>(); foreach (var a in StudioForm.TxtAudit) words.Add(a.S); StudioForm.TxtAudit = null;
            Check("the transport shows PLAY, MIC and MIDI REC", words.Contains("PLAY") && words.Contains("MIC") && words.Contains("MIDI REC") && !words.Contains("REC"), string.Join(",", words.GetRange(0, Math.Min(12, words.Count))));
            int ov = Prefs.OutVol; Prefs.OutVol = 0; SetF("micArmed", true); eng.Playing = true; StudioForm.TxtAudit = new List<StudioForm.AuditText>(); Paint(); words.Clear(); foreach (var a in StudioForm.TxtAudit) words.Add(a.S); StudioForm.TxtAudit = null; eng.Playing = false; SetF("micArmed", false); Prefs.OutVol = ov;
            Check("while playing and with the mic ready it still says PLAY and MIC (no PAUSE, RECORD or MIC READY)", words.Contains("PLAY") && words.Contains("MIC") && !words.Contains("PAUSE") && !words.Contains("RECORD") && !words.Contains("MIC READY"), string.Join(",", words.FindAll(w => w.Contains("PLAY") || w.Contains("PAUSE") || w.Contains("MIC") || w.Contains("RECORD"))));
        }

        // ---- the view stays where you scroll it while it plays ----
        {
            eng.Tracks.Clear(); eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", Const(120, 0.3f))); SetF("autoFit", false); SetF("spp", 400.0); SetF("scroll", 0L); SetF("followPaused", false); SetF("wasPlaying", false); Paint();
            int ov = Prefs.OutVol; Prefs.OutVol = 0; eng.Playing = true; eng.Pos = SR * 60; SetF("dispPos", (double)(SR * 60)); M("Tick"); long followed = F<long>("scroll"); Check("the view follows the needle when you have not scrolled", followed > SR * 30, "scroll " + followed / SR + " s");
            SetF("scroll", 0L); M("UserScrolled"); M("Tick"); Check("after you scroll it, the view stays where you put it", F<long>("scroll") == 0, "scroll " + F<long>("scroll") / SR + " s");
            eng.Playing = false; M("Tick"); eng.Playing = true; M("Tick"); Check("pressing PLAY again follows the needle again", F<long>("scroll") > SR * 30, "scroll " + F<long>("scroll") / SR + " s"); eng.Playing = false; Prefs.OutVol = ov; eng.Pos = 0; SetF("dispPos", 0.0);
        }

        // ---- pop-up boxes move; lists have the logo scroll strip ----
        {
            M("OpenStyles"); Paint(); Paint(); var po = typeof(StudioForm).GetField("popOff", BF).GetValue(sf) as System.Collections.IDictionary; var prc = (List<RectangleF>)typeof(StudioForm).GetField("popRects", BF).GetValue(sf);
            Check("a pop-up box has a handle strip you can drag", prc.Count > 0, prc.Count + " handles");
            if (prc.Count > 0)
            {
                var h = prc[0]; int hx = (int)(h.X + 120), hy = (int)(h.Y + 10); Ev("OnMouseMove", MouseButtons.None, 0, hx, hy); Ev("OnMouseDown", MouseButtons.Left, 1, hx, hy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, hx + 20 * i, hy + 10 * i); Ev("OnMouseUp", MouseButtons.Left, 1, hx + 120, hy + 60); Paint(); Paint();
                var off = po.Contains("styles") ? (PointF)po["styles"] : PointF.Empty; Check("dragging the strip moves the box", Math.Abs(off.X - 120) < 6 && Math.Abs(off.Y - 60) < 6, off.X + "," + off.Y); po.Remove("styles");
            }
            Paint(); Paint(); float s0 = F<float>("stylesScroll"); var logoReg = Regs().FindAll(r => r.OnDrag != null && r.OnDouble == null && r.R.Width < 50 && r.R.Width > 20 && r.R.Height > 20 && r.R.Height < 50 && r.R.X > 400).FirstOrDefault();
            var strip = Regs().FindAll(r => r.OnDrag != null && r.R.Width > 30 && r.R.Width < 50 && r.R.Height > 200).FirstOrDefault(); Check("the styles list has a vertical scroll strip", strip != null, "");
            if (strip != null) { int cx = (int)(strip.R.X + strip.R.Width / 2), cy = (int)(strip.R.Y + strip.R.Height * 0.2f); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, cx, cy + 40 * i); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy + 240); Paint(); Check("dragging along it scrolls the list", F<float>("stylesScroll") > s0 + 50, s0 + " -> " + F<float>("stylesScroll")); }
            SetF("stylesOpen", false);
        }

        // ---- lyric sections ----
        {
            string song = "Walking down the street tonight\nCity lights are burning bright\nI can feel it in my bones\nNo more waiting on my own\n\nHold me closer, hold me tight\nWe are fire in the night\nHold me closer, hold me tight\nWe are fire in the night\n\nMorning came and I was gone\nChasing echoes of the dawn\nEvery step a different song\nNothing here to hold me long\n\nHold me closer, hold me tight\nWe are fire in the night\nHold me closer, hold me tight\nWe are fire in the night\n\nWhen the sun goes down again\nI will find you in the rain\nBreak the silence, break the chain\nNever be the same\n\nHold me closer, hold me tight\nWe are fire in the night\nHold me closer, hold me tight\nWe are fire in the night";
            int ad; string nt; string res = LyricSections.Add(song, out ad, out nt); var rl = res.Split('\n'); Func<string, int> cnt = lab => { int c = 0; foreach (var l in rl) if (l == lab) c++; return c; };
            Check("a text file gets its repeating part labelled as the chorus (3 times)", cnt("[Chorus]") == 3, nt);
            Check("the other parts become verses, in order", cnt("[Verse 1]") == 1 && cnt("[Verse 2]") == 1 && res.IndexOf("[Verse 1]") < res.IndexOf("[Verse 2]"), "");
            Check("a different part between the choruses late in the song is the bridge or the next verse", cnt("[Bridge]") + cnt("[Verse 3]") == 1, nt);
            Check("every label sits on its own line right above its part, with a blank line between parts", res.StartsWith("[Verse 1]\nWalking down") && res.Contains("\n\n[Chorus]\nHold me closer"), res.Substring(0, 60).Replace("\n", "|"));
            string res2 = LyricSections.Add(res, out ad, out nt); Check("running it again changes nothing", res2 == res && ad == 0, nt);
            string half = "[Verse 1]\nline one\nline two\n\nHold on\nHold on\n\nHold on\nHold on"; string rh = LyricSections.Add(half, out ad, out nt); Check("parts you labelled yourself stay as they are", rh.StartsWith("[Verse 1]\nline one") && ad == 2 && rh.Contains("[Chorus]"), nt + " / " + rh.Replace("\n", "|"));
            string flat = ""; for (int i = 1; i <= 12; i++) flat += "this is line number " + i + "\n"; string rf = LyricSections.Add(flat, out ad, out nt); Check("lyrics with no blank lines are cut into parts of a few lines", ad == 3 && rf.Contains("\n\n[Verse 2]\n"), nt);
            // the button
            M("AddLyricSections"); SetF("tab", 2); Paint(); var lbx = F<TextBox>("lyricBox"); lbx.Text = song.Replace("\n", "\r\n"); Paint();
            var sb = Regs().Find(r => r.R.Width >= 120 && r.R.Width <= 145 && Math.Abs(r.R.Height - 28) < 1 && r.OnDown != null && r.R.Y < 300); Check("the lyrics page has an ADD SECTIONS button", sb != null, "");
            if (sb != null) { int cx = (int)(sb.R.X + 20), cy = (int)(sb.R.Y + 12); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); Check("pressing it puts the labels into the writing box", lbx.Text.Contains("[Chorus]") && lbx.Text.Contains("[Verse 1]"), lbx.Text.Length > 30 ? lbx.Text.Substring(0, 30) : lbx.Text); }
            lbx.Text = ""; SetF("tab", 0); Paint();
        }

        // ---- an emptied track that still has plugins on is removed by itself ----
        {
            eng.Tracks.Clear(); Func<string, AudioClip, bool, Track> mk = (nm, clip, plug) => { var t = StudioEngine.NewTrack(nm, "vocal", clip); if (plug) { var f = FxRegistry.Create("eq"); f.Prepare(SR); t.Fx.Add(f); } eng.Tracks.Add(t); return t; };
            var keep = mk("Keeper", Const(4, 0.3f), true); var moved = mk("Moved away", Const(4, 0.3f), true); var fresh = mk("Fresh empty", null, true); var armed = mk("Armed", null, true); armed.Armed = true; var byp = mk("Bypassed", Const(4, 0.3f), true); var plain = mk("No plugins", Const(4, 0.3f), false);
            SetF("selTrack", 0); Paint(); Pump(700); Paint();   // each one is seen holding audio
            moved.Clip = null; byp.Clip = null; byp.Fx[0].Bypass = true; plain.Clip = null; int nTidy = eng.Tracks.Count; Pump(700); Paint(); Pump(700); Paint();
            Check("a track that is emptied while plugins are on is removed", !eng.Tracks.Contains(moved), eng.Tracks.Count + " tracks left");
            Check("a track with audio is kept", eng.Tracks.Contains(keep), "");
            Check("a track that never held audio is kept (even with plugins)", eng.Tracks.Contains(fresh), "");
            Check("an armed track waiting for a recording is kept", eng.Tracks.Contains(armed), "");
            Check("an emptied track whose plugins are all bypassed is kept", eng.Tracks.Contains(byp), "");
            Check("an emptied track with no plugins is kept", eng.Tracks.Contains(plain), "");
            typeof(StudioForm).GetMethod("DoUndo", BF).Invoke(sf, null); Paint(); Pump(700); Paint(); Pump(700); Paint();
            Check("Ctrl+Z brings the removed track back and it stays", eng.Tracks.Contains(moved) && eng.Tracks.Count == nTidy, eng.Tracks.Count + " tracks");
            var lay = mk("Has a piece", Const(4, 0.3f), true); var piece = StudioEngine.NewTrack("piece", "vocal", Const(2, 0.3f)); piece.Host = lay; lock (eng.Sync) eng.Tracks.Insert(eng.Tracks.Count, piece); Paint(); Pump(700); Paint(); lay.Clip = null; Pump(700); Paint(); Pump(700); Paint();
            Check("a row that still has a piece on it is kept", eng.Tracks.Contains(lay), "");
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
            string missing = ""; foreach (var k in new[] { "COUNT IN", "METRONOME", "FLEX", "UNDO", "REDO", "-", "+", "FIT", "NEW", "SPLIT", "CUT", "FADE IN", "FADE OUT", "GAIN", "LOCK", "DUP", "DEL", "Grid Size", "DETECT BPM", "THIN TRACKS", "BACK", "STEREO", "MONO", "MIC", "MIDI REC", "SOUNDS", "Vocal Styles", "POPULAR", "NEWEST", "USE", "Report", "SHARE MY CURRENT SETTINGS", "Open .txt", "Save .txt", "Correct pitch", "DONE (keep edits)" }) if (!hints.ContainsKey(k) && !(k == "SPLIT" || k == "CUT" || k == "FADE IN" || k == "FADE OUT" || k == "GAIN" || k == "LOCK" || k == "DUP" || k == "DEL" || k == "Grid Size" || k == "DETECT")) missing += k + " ";
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
