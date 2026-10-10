using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  FLEX PITCH in Purphex Music: select a track, press FLEX, and every note of the recording appears as a block you can drag.
//  The audio is re-tuned live (the track plays your edits), and the result is saved with the project.
// ======================================================================
partial class StudioForm
{
    bool flexOpen, flexAnalysing, flexEdited, flexSplitTool; volatile float flexPct; Track flexTrack; float[] flexSrcL, flexSrcR; int flexLen; FlexAnalysis flexAna; float[] flexRatio;
    readonly List<FlexNote> flexSel = new List<FlexNote>(); float flexLow = 48; int flexKey = 0, flexScale = 0; float flexAmount = 100; readonly object flexLk = new object();
    AudioClip flexClip0; long flexStart0, flexFrames0; string flexName = "";
    // render worker
    Thread flexThread; volatile int flexReq, flexDone; volatile bool flexSwap; float[] flexOutL, flexOutR;
    // mouse drags
    int flexDragMode; double flexDragMidi0; List<FlexNote> flexDragNotes = new List<FlexNote>(); List<double> flexDragT0 = new List<double>(); PointF flexDown, flexNow; bool flexMoved; FlexNote flexEdgeNote;

    // ---- open / close ----
    void FlexOpen()
    {
        var t = Selected; if (t == null || t.Clip == null || !t.Clip.Loaded || t.Instrument) { Toast("Select a track that has a recording first. Flex Pitch works best on a single voice or instrument."); return; }
        if (flexOpen) FlexClose(true); PushUndo(); flexTrack = t; flexClip0 = t.Clip; flexStart0 = t.ClipStart; flexFrames0 = t.ClipFrames; flexName = t.Name;
        long len = t.UsedLength; if (len < 4096) { Toast("That recording is too short to edit."); return; }
        var l = new float[len]; var r = new float[len]; Array.Copy(t.Clip.L, t.ClipStart, l, 0, len); Array.Copy(t.Clip.R ?? t.Clip.L, t.ClipStart, r, 0, len);
        flexSrcL = l; flexSrcR = r; flexLen = (int)len; flexAna = null; flexRatio = null; flexSel.Clear(); flexEdited = false; flexSplitTool = false; flexAnalysing = true; flexPct = 0; flexOpen = true; flexReq = 0; flexDone = 0; flexSwap = false; menuOpen = false;
        var th = new Thread(() =>
        {
            FlexAnalysis a = null; try { a = FlexPitch.Analyse(l, r, (int)len, StudioEngine.SR, p => flexPct = p, () => !flexOpen || flexTrack != t); } catch { a = null; }
            BeginInvoke(new Action(() => { if (!flexOpen || flexTrack != t) return; flexAnalysing = false; if (a == null) { Toast("Could not analyse that recording."); flexOpen = false; return; } lock (flexLk) flexAna = a; FlexFitRange(); FlexRatioUpdate(); { double w = Math.Max(300, TimelineRect().Width * 0.94); spp = Math.Max(MinSpp, Math.Min(MaxSpp, flexLen / w)); scroll = Math.Max(0, t.Offset - (long)(spp * 6)); autoFit = false; } if (a.Notes.Count == 0) Toast("No clear notes were found. Flex Pitch needs a single voice or instrument, not a full mix."); Invalidate(); }));
        }) { IsBackground = true, Name = "flex-analysis" }; th.Start();
        flexThread = new Thread(FlexWorker) { IsBackground = true, Name = "flex-render" }; flexThread.Start();
    }

    void FlexFitRange()
    {
        var a = flexAna; if (a == null || a.Notes.Count == 0) { flexLow = 48; return; } double lo = 1e9, hi = -1e9; foreach (var n in a.Notes) { lo = Math.Min(lo, n.Target); hi = Math.Max(hi, n.Target); }
        flexRows = (float)Math.Max(12, Math.Min(48, Math.Ceiling(hi - lo) + 7)); flexLow = (float)Math.Floor((lo + hi) / 2 - flexRows / 2 + 0.5);
    }

    // done = keep the edits (saved as a new audio file with the project); revert = go back to the recording as it was
    void FlexClose(bool keep)
    {
        if (!flexOpen) return; var t = flexTrack; flexOpen = false; Thread.Sleep(10);
        if (keep && flexEdited && t != null && t.Clip != null && flexAna != null)
        {
            try
            {
                string dir = Path.Combine(StudioEnv.DataDir, "flex"); Directory.CreateDirectory(dir); string safe = flexName; foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_'); string file = Path.Combine(dir, safe + "_flex_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav");
                var c0 = t.Clip; SoundIO.WriteWav(file, c0.L, c0.R, StudioEngine.SR, 24); c0.Path = file; Toast("Flex Pitch edits kept (saved with the project).");
            }
            catch (Exception ex) { Toast("Could not save the edited audio: " + ex.Message); }
        }
        else if (!flexEdited && t != null) { lock (eng.Sync) { t.Clip = flexClip0; t.ClipStart = flexStart0; t.ClipFrames = flexFrames0; } }
        flexAna = null; flexSel.Clear(); flexSrcL = flexSrcR = null; flexTrack = null;
    }
    void FlexRevert()
    {
        var t = flexTrack; if (!flexOpen || t == null) return; lock (flexLk) { if (flexAna != null) foreach (var n in flexAna.Notes) { n.ResetEdits(); n.Deleted = false; } }
        lock (eng.Sync) { t.Clip = flexClip0; t.ClipStart = flexStart0; t.ClipFrames = flexFrames0; } flexEdited = false; FlexRatioUpdate(); Toast("Back to the original recording.");
    }
    void FlexCommitIfOpen() { if (flexOpen && flexEdited) { /* keep the editor open but make sure the project points at a real file */ var t = flexTrack; if (t != null && t.Clip != null) { try { string dir = Path.Combine(StudioEnv.DataDir, "flex"); Directory.CreateDirectory(dir); string safe = flexName; foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_'); string file = Path.Combine(dir, safe + "_flex_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav"); SoundIO.WriteWav(file, t.Clip.L, t.Clip.R, StudioEngine.SR, 24); t.Clip.Path = file; } catch { } } } }

    // ---- live re-tuning ----
    void FlexChanged() { flexEdited = true; FlexRatioUpdate(); flexReq++; }
    void FlexRatioUpdate() { var a = flexAna; if (a == null) return; lock (flexLk) flexRatio = FlexPitch.RatioCurve(a, null, null); }
    void FlexWorker()
    {
        while (flexOpen)
        {
            if (flexDone == flexReq) { Thread.Sleep(15); continue; }
            int req = flexReq; Thread.Sleep(70); if (req != flexReq) continue;
            FlexAnalysis snap; lock (flexLk) { var a = flexAna; if (a == null) { flexDone = req; continue; } snap = new FlexAnalysis { Hop = a.Hop, SR = a.SR, Frames = a.Frames, F0 = a.F0, Midi = a.Midi, Level = a.Level }; foreach (var n in a.Notes) snap.Notes.Add(n.Clone()); }
            float[] ol, orr; try { FlexPitch.Render(flexSrcL, flexSrcR, flexLen, snap, out ol, out orr); } catch { flexDone = req; continue; }
            if (!flexOpen) return; flexOutL = ol; flexOutR = orr; flexSwap = true; flexDone = req;
        }
    }
    // called every tick on the UI thread: puts the newest render into the track
    void FlexTick()
    {
        if (!flexSwap || !flexOpen || flexTrack == null) return; flexSwap = false; var l = flexOutL; var r = flexOutR; if (l == null) return; var t = flexTrack;
        var c = AudioClip.FromArrays(l, r, flexClip0 != null ? flexClip0.Name : t.Name); c.Path = flexClip0 != null ? flexClip0.Path : ""; lock (eng.Sync) { t.Clip = c; t.ClipStart = 0; t.ClipFrames = -1; }
    }

    // ---- geometry ----
    int FlexPanelH() { return Math.Max(280, Math.Min(440, (int)((ClientSize.Height - ToolH - TransH) * 0.64))); }
    RectangleF FlexPanel() { float h = FlexPanelH(); return Rc(0, ClientSize.Height - h, ClientSize.Width - PanelW, h); }
    RectangleF FlexNoteArea() { var p = FlexPanel(); return Rc(HeadW, p.Y + 34, p.Width - HeadW - 4, p.Height - 34 - 52); }
    float flexRows = 24;   // how many semitones fit in the note area (zoom)
    float FlexRowH { get { var na = FlexNoteArea(); return Math.Max(13f, Math.Min(44f, na.Height / Math.Max(8f, flexRows))); } }
    float FlexX(double clipSample) { return HeadW + (float)((flexTrack.Offset + clipSample - scroll) / spp); }
    double FlexSampleAt(float x) { return scroll + (x - HeadW) * spp - flexTrack.Offset; }
    float FlexY(double midi, RectangleF na) { return na.Bottom - (float)(midi - flexLow + 0.5) * FlexRowH; }
    double FlexMidiAt(float y, RectangleF na) { return flexLow + (na.Bottom - y) / FlexRowH - 0.5; }
    static bool FlexBlack(int midi) { int p = ((midi % 12) + 12) % 12; return p == 1 || p == 3 || p == 6 || p == 8 || p == 10; }

    // ---- painting ----
    void PaintFlex(Graphics g)
    {
        FlexTick(); var P = FlexPanel(); var na = FlexNoteArea(); var t = flexTrack; if (t == null) return; var gs0 = g.Save();
        using (var br = new SolidBrush(Color.FromArgb(246, 10, 5, 24))) g.FillRectangle(br, P);
        using (var pen = new Pen(Color.FromArgb(220, NeonViolet), 1.6f)) g.DrawLine(pen, P.X, P.Y, P.Right, P.Y); using (var glow = new Pen(Color.FromArgb(50, NeonCyan), 5f)) g.DrawLine(glow, P.X, P.Y + 1, P.Right, P.Y + 1);
        Reg(P, () => { }).OnWheel = d => { if (ModifierKeys == Keys.Control) { ZoomBy(d > 0 ? 0.8 : 1.25); autoFit = false; } else flexLow = Math.Max(0, Math.Min(110, flexLow + (d > 0 ? 3 : -3))); };
        PaintFlexInspector(g, P);
        // header
        Txt(g, "FLEX PITCH  -  " + t.Name, 13, Ink, Rc(na.X, P.Y + 6, 420, 22), StringAlignment.Near, true);
        if (!flexAnalysing) { Btn(g, Rc(na.Right - 62, P.Y + 5, 26, 24), "-", 0, () => { double mid = flexLow + flexRows / 2; flexRows = Math.Min(60, flexRows * 1.25f); flexLow = (float)(mid - flexRows / 2); }, 13); Btn(g, Rc(na.Right - 32, P.Y + 5, 26, 24), "+", 0, () => { double mid = flexLow + flexRows / 2; flexRows = Math.Max(10, flexRows / 1.25f); flexLow = (float)(mid - flexRows / 2); }, 13); Txt(g, "pitch zoom", 10, DimC, Rc(na.Right - 140, P.Y + 8, 74, 18), StringAlignment.Far, false); }
        Txt(g, flexAnalysing ? "Listening to the recording... " + (int)(flexPct * 100) + "%" : "Drag notes up or down (Alt = free)   Double-click = reset   Right-click = more", 11, DimC, Rc(na.X + 330, P.Y + 8, na.Width - 480, 20), StringAlignment.Far, false);
        // pitch rows
        var gc = g.Save(); g.SetClip(na); using (var bg = new SolidBrush(Color.FromArgb(235, 6, 3, 16))) g.FillRectangle(bg, na);
        int rows = (int)Math.Ceiling(na.Height / FlexRowH) + 1;
        for (int i = 0; i < rows; i++)
        {
            int midi = (int)flexLow + i; float y = FlexY(midi, na) - FlexRowH / 2; var rr = Rc(na.X, y, na.Width, FlexRowH); using (var b = new SolidBrush(FlexBlack(midi) ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(22, 120, 80, 220))) g.FillRectangle(b, rr);
            using (var pen = new Pen(((midi % 12) == 0) ? Color.FromArgb(95, NeonViolet) : Color.FromArgb(34, 160, 120, 255), 1f)) g.DrawLine(pen, na.X, y + FlexRowH, na.Right, y + FlexRowH);
            if (((midi % 12) == 0) || FlexRowH >= 15 && ((midi % 12) == 4 || (midi % 12) == 7)) Txt(g, FlexNote.Name(midi), 10, ((midi % 12) == 0) ? Color.FromArgb(190, 190, 160, 255) : Color.FromArgb(110, 160, 140, 220), Rc(na.X + 3, y, 40, FlexRowH), StringAlignment.Near, (midi % 12) == 0);
        }
        // time grid (beats) follows the studio grid
        double bpm = Math.Max(20, Fx.ProjectBpm), spb = 60.0 / bpm * StudioEngine.SR; double firstBeat = Math.Floor(scroll / spb); for (double b = firstBeat; ; b += 1) { double fr = b * spb; float x = HeadW + (float)((fr - scroll) / spp); if (x > na.Right) break; if (x < na.X) continue; using (var pen = new Pen(Color.FromArgb(((long)b % 4 == 0) ? 60 : 24, NeonCyan), 1f)) g.DrawLine(pen, x, na.Y, x, na.Bottom); }
        g.Restore(gc);
        if (flexAnalysing || flexAna == null)
        {
            var pb = Rc(na.X + na.Width / 2 - 180, na.Y + na.Height / 2 - 14, 360, 18); FillRR(g, Color.FromArgb(220, 8, 4, 18), pb, 9); FillRR(g, NeonCyan, Rc(pb.X, pb.Y, Math.Max(10, pb.Width * flexPct), pb.Height), 9);
            Txt(g, "Finding the notes in " + t.Name + "...", 13, Ink, Rc(na.X, pb.Y - 30, na.Width, 24), StringAlignment.Center, true); FlexWave(g, P, na); g.Restore(gs0); return;
        }
        // background: click = deselect / split tool, drag = marquee
        var bgReg = Reg(na, () => FlexBackgroundDown()); bgReg.OnDrag = pt => { flexNow = pt; flexMoved = true; }; bgReg.OnUp = () => FlexBackgroundUp(); bgReg.OnDouble = () => { };
        var ncl = g.Save(); g.SetClip(na); List<FlexNote> list; lock (flexLk) list = new List<FlexNote>(flexAna.Notes); int hop = flexAna.Hop; var ratio = flexRatio;
        foreach (var n in list)
        {
            float x0 = FlexX((double)n.Start * hop), x1 = FlexX((double)n.End * hop); if (x1 < na.X - 4 || x0 > na.Right + 4) continue; if (x1 - x0 < 3) x1 = x0 + 3; float yc = FlexY(n.Target, na); var blk = Rc(x0, yc - FlexRowH * 0.42f, x1 - x0, FlexRowH * 0.84f); bool sel = flexSel.Contains(n);
            if (n.Deleted) { using (var pen = new Pen(Color.FromArgb(90, 160, 140, 200), 1f) { DashStyle = DashStyle.Dot }) g.DrawRectangle(pen, blk.X, blk.Y, blk.Width, blk.Height); continue; }
            if (n.Edited && Math.Abs(n.Target - n.OrigMidi) > 0.05) { float yo = FlexY(n.OrigMidi, na); using (var pen = new Pen(Color.FromArgb(110, 200, 190, 255), 1f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, x0, yo - FlexRowH * 0.42f, x1 - x0, FlexRowH * 0.84f); }
            Color baseC = n.Edited ? NeonCyan : NeonViolet; if (sel) Glow(g, baseC, blk, 4, 0.8f, 3);
            using (var lg = new LinearGradientBrush(blk, Color.FromArgb(sel ? 245 : 210, Math.Min(255, baseC.R + 30), Math.Min(255, baseC.G + 30), Math.Min(255, baseC.B + 30)), Color.FromArgb(sel ? 235 : 190, baseC.R / 2, baseC.G / 2, baseC.B / 2 + 40), 90f)) using (var path = RR(blk, 5)) g.FillPath(lg, path);
            StrokeRR(g, sel ? Color.White : Color.FromArgb(160, baseC), blk, 5, sel ? 2f : 1f);
            // the pitch line inside the note: what the voice actually does (drift, vibrato), after your edits
            if (blk.Width > 8)
            {
                int step = Math.Max(1, (int)(2 * spp / hop)); var pts = new List<PointF>(); for (int f = n.Start; f < n.End; f += step) { double m = flexAna.Midi[f]; if (m <= 0) continue; double me = ratio != null && f < ratio.Length ? m + 12 * Math.Log(ratio[f], 2) : m; pts.Add(new PointF(FlexX((double)f * hop), FlexY(me, na))); }
                if (pts.Count > 1) using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 1.4f)) { try { g.DrawLines(pen, pts.ToArray()); } catch { } }
            }
            if (blk.Width > 30) { double dev = (n.Target - Math.Round(n.Target)) * 100; string lb = FlexNote.Name(n.Target) + (blk.Width > 78 ? "  " + (dev >= 0 ? "+" : "") + ((int)Math.Round(dev)) + "c" : ""); Txt(g, lb, 10, Color.FromArgb(240, 255, 255, 255), Rc(blk.X + 3, blk.Y - 13, Math.Max(60, blk.Width), 13), StringAlignment.Near, true); }
            var nn = n; var body = Reg(blk, () => FlexNoteDown(nn)); body.OnDrag = pt => FlexNoteDrag(pt); body.OnUp = () => FlexNoteUp(); body.OnDouble = () => { lock (flexLk) nn.ResetEdits(); FlexChanged(); }; body.OnRight = () => FlexNoteMenu(nn);
            if (blk.Width > 14) { var le = Rc(blk.X - 3, blk.Y, 7, blk.Height); var re = Rc(blk.Right - 4, blk.Y, 7, blk.Height); var lr = Reg(le, () => FlexEdgeDown(nn, 2)); lr.OnDrag = pt => FlexEdgeDrag(pt); lr.OnUp = () => FlexEdgeUp(); var rr2 = Reg(re, () => FlexEdgeDown(nn, 3)); rr2.OnDrag = pt => FlexEdgeDrag(pt); rr2.OnUp = () => FlexEdgeUp(); }
        }
        // marquee
        if (flexDragMode == 4 && flexMoved) { var m = Rc(Math.Min(flexDown.X, flexNow.X), Math.Min(flexDown.Y, flexNow.Y), Math.Abs(flexNow.X - flexDown.X), Math.Abs(flexNow.Y - flexDown.Y)); using (var b = new SolidBrush(Color.FromArgb(40, NeonCyan))) g.FillRectangle(b, m); using (var pen = new Pen(Color.FromArgb(200, NeonCyan), 1f)) g.DrawRectangle(pen, m.X, m.Y, m.Width, m.Height); }
        // needle
        float px = HeadW + (float)((DispPos - scroll) / spp); if (px >= na.X && px <= na.Right) using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 1.4f)) g.DrawLine(pen, px, na.Y, px, na.Bottom);
        g.Restore(ncl); FlexWave(g, P, na); g.Restore(gs0);
    }

    void FlexWave(Graphics g, RectangleF P, RectangleF na)
    {
        var wr = Rc(na.X, na.Bottom + 6, na.Width, 40); FillRR(g, Color.FromArgb(200, 6, 3, 16), wr, 6); var src = flexSrcL; if (src == null) return; var gs = g.Save(); g.SetClip(wr);
        using (var pen = new Pen(Color.FromArgb(150, NeonCyan), 1f)) for (int x = 0; x < (int)wr.Width; x += 1)
            {
                double s0 = FlexSampleAt(wr.X + x), s1 = FlexSampleAt(wr.X + x + 1); if (s1 < 0 || s0 >= flexLen) continue; int a = (int)Math.Max(0, s0), b = (int)Math.Min(flexLen - 1, Math.Max(a + 1, s1)); float mn = 0, mx = 0; int stp = Math.Max(1, (b - a) / 48);
                for (int i = a; i < b; i += stp) { float v = src[i]; if (v < mn) mn = v; if (v > mx) mx = v; }
                float cy = wr.Y + wr.Height / 2; g.DrawLine(pen, wr.X + x, cy - mx * wr.Height * 0.48f, wr.X + x, cy - mn * wr.Height * 0.48f - 0.5f);
            }
        g.Restore(gs);
    }

    void PaintFlexInspector(Graphics g, RectangleF P)
    {
        float x = 12, y = P.Y + 8, w = HeadW - 24; Txt(g, "NOTE", 12, NeonCyan, Rc(x, y, 80, 18), StringAlignment.Near, true);
        string who = flexSel.Count == 0 ? "no note selected" : (flexSel.Count == 1 ? FlexNote.Name(flexSel[0].Target) + (flexSel[0].Edited ? "  (edited)" : "") : flexSel.Count + " notes"); Txt(g, who, 12, Ink, Rc(x + 52, y, w - 52, 18), StringAlignment.Near, true); y += 24;
        Action<string, double, double, Func<FlexNote, double>, Action<FlexNote, double>, string, double> slider = (name, min, max, get, set, fmt, def) =>
        {
            bool on = flexSel.Count > 0 && flexAna != null; double val = on ? get(flexSel[0]) : def; Txt(g, name, 11, on ? DimC : Color.FromArgb(90, 120, 110, 150), Rc(x, y, 52, 22), StringAlignment.Near, false);
            var sr = Rc(x + 54, y + 3, w - 54 - 52, 16); float frac = (float)((val - min) / (max - min)); var rg = Slider(g, sr, Math.Max(0, Math.Min(1, frac)), f => { if (!on) return; double v = min + f * (max - min); v = Math.Round(v * 100) / 100; lock (flexLk) foreach (var n in flexSel) set(n, v); FlexChanged(); }, NeonCyan); rg.OnDouble = () => { if (!on) return; lock (flexLk) foreach (var n in flexSel) set(n, def); FlexChanged(); };
            Txt(g, on ? string.Format(CultureInfo.InvariantCulture, fmt, val) : "-", 11, on ? Ink : Color.FromArgb(90, 120, 110, 150), Rc(x + w - 50, y, 50, 22), StringAlignment.Far, false); y += 25;
        };
        slider("Pitch", -12, 12, n => n.Shift, (n, v) => n.Shift = v, "{0:+0.00;-0.00;0} st", 0);
        slider("Fine", -50, 50, n => n.Fine, (n, v) => n.Fine = v, "{0:+0;-0;0} ct", 0);
        slider("Drift", 0, 100, n => n.Drift, (n, v) => n.Drift = v, "{0:0} %", 0);
        slider("Vibrato", 0, 200, n => n.Vib, (n, v) => n.Vib = v, "{0:0} %", 100);
        slider("Gain", -24, 12, n => n.GainDb, (n, v) => n.GainDb = v, "{0:+0.0;-0.0;0} dB", 0);
        slider("Formant", -12, 12, n => n.Formant, (n, v) => n.Formant = v, "{0:+0.0;-0.0;0} st", 0);
        y += 4; float bw = (w - 6) / 2;
        Btn(g, Rc(x, y, bw, 26), "Correct pitch", 1, () => FlexCorrect(), 11); Btn(g, Rc(x + bw + 6, y, bw, 26), PitchCorrector.Keys[flexKey] + " " + (flexScale == 0 ? "chromatic" : PitchCorrector.Scales[flexScale].ToLowerInvariant()), 0, () => FlexScaleMenu(), 10); y += 30;
        Txt(g, "Amount", 11, DimC, Rc(x, y, 52, 20), StringAlignment.Near, false); var ar = Rc(x + 54, y + 3, w - 54 - 52, 16); Slider(g, ar, flexAmount / 100f, f => flexAmount = (float)Math.Round(f * 100), NeonViolet).OnDouble = () => flexAmount = 100; Txt(g, ((int)flexAmount) + " %", 11, Ink, Rc(x + w - 50, y, 50, 20), StringAlignment.Far, false); y += 26;
        Btn(g, Rc(x, y, bw, 26), "Select all", 0, () => { lock (flexLk) { flexSel.Clear(); if (flexAna != null) flexSel.AddRange(flexAna.Notes.FindAll(n => !n.Deleted)); } }, 11); Btn(g, Rc(x + bw + 6, y, bw, 26), flexSplitTool ? "Split: ON" : "Split tool", flexSplitTool ? 4 : 0, () => flexSplitTool = !flexSplitTool, 11); y += 30;
        Btn(g, Rc(x, y, bw, 26), "Reset note", 0, () => { lock (flexLk) foreach (var n in flexSel) n.ResetEdits(); FlexChanged(); }, 11); Btn(g, Rc(x + bw + 6, y, bw, 26), "Original", 3, () => FlexRevert(), 11); y += 30;
        Btn(g, Rc(x, y, w, 30), "DONE (keep edits)", 2, () => FlexClose(true), 12);
    }

    // ---- mouse ----
    void FlexSelect(FlexNote n, bool add) { if (add) { if (flexSel.Contains(n)) flexSel.Remove(n); else flexSel.Add(n); } else if (!flexSel.Contains(n)) { flexSel.Clear(); flexSel.Add(n); } }
    void FlexNoteDown(FlexNote n)
    {
        if (flexSplitTool) { var na0 = FlexNoteArea(); int fr = (int)(FlexSampleAt(MousePos.X) / flexAna.Hop); lock (flexLk) { var s = FlexPitch.Split(flexAna, n, fr); if (s == null) { Toast("Click further inside the note to split it."); } else { flexSel.Clear(); flexSel.Add(n); flexSel.Add(s); FlexChanged(); } } return; }
        FlexSelect(n, (ModifierKeys & Keys.Shift) != 0); flexDragMode = 1; flexDown = MousePos; flexMoved = false; var na = FlexNoteArea(); flexDragMidi0 = FlexMidiAt(MousePos.Y, na); flexDragNotes = new List<FlexNote>(flexSel); flexDragT0.Clear(); foreach (var q in flexDragNotes) flexDragT0.Add(q.Target);
    }
    void FlexNoteDrag(PointF pt)
    {
        if (flexDragMode != 1) return; if (!flexMoved && Math.Abs(pt.Y - flexDown.Y) < 4) return; flexMoved = true; var na = FlexNoteArea(); double delta = FlexMidiAt(pt.Y, na) - flexDragMidi0; bool free = (ModifierKeys & Keys.Alt) != 0;
        lock (flexLk) for (int i = 0; i < flexDragNotes.Count; i++) { var n = flexDragNotes[i]; double tgt = flexDragT0[i] + delta; if (!free) tgt = Math.Round(tgt); n.Fine = 0; n.Shift = Math.Max(-24, Math.Min(24, tgt - n.OrigMidi)); }
        FlexChanged();
    }
    void FlexNoteUp() { flexDragMode = 0; flexMoved = false; }
    void FlexEdgeDown(FlexNote n, int mode) { flexDragMode = mode; flexEdgeNote = n; flexSel.Clear(); flexSel.Add(n); flexMoved = false; }
    void FlexEdgeDrag(PointF pt)
    {
        var n = flexEdgeNote; if (n == null || flexAna == null) return; int hop = flexAna.Hop; int f = (int)Math.Round(FlexSampleAt(pt.X) / hop); var notes = flexAna.Notes; int i = notes.IndexOf(n); if (i < 0) return; flexMoved = true;
        lock (flexLk)
        {
            if (flexDragMode == 2) { int lo = i > 0 ? notes[i - 1].End : 0; n.Start = Math.Max(lo, Math.Min(n.End - 6, f)); }
            else { int hi = i + 1 < notes.Count ? notes[i + 1].Start : flexAna.Frames; n.End = Math.Min(hi, Math.Max(n.Start + 6, f)); }
            var v = new List<double>(); for (int k = n.Start; k < n.End; k++) if (flexAna.Midi[k] > 0) v.Add(flexAna.Midi[k]); if (v.Count > 3) { v.Sort(); n.OrigMidi = v[v.Count / 2]; }
        }
        FlexChanged();
    }
    void FlexEdgeUp() { flexDragMode = 0; flexEdgeNote = null; flexMoved = false; }
    void FlexBackgroundDown()
    {
        var na = FlexNoteArea(); flexDown = MousePos; flexNow = MousePos; flexMoved = false; flexDragMode = 4; if ((ModifierKeys & Keys.Shift) == 0) flexSel.Clear();
        if (!flexSplitTool) { long fr = (long)(scroll + (MousePos.X - HeadW) * spp); eng.Seek(Math.Max(0, fr)); }
    }
    void FlexBackgroundUp()
    {
        if (flexDragMode == 4 && flexMoved && flexAna != null)
        {
            var na = FlexNoteArea(); var m = Rc(Math.Min(flexDown.X, flexNow.X), Math.Min(flexDown.Y, flexNow.Y), Math.Abs(flexNow.X - flexDown.X), Math.Abs(flexNow.Y - flexDown.Y)); int hop = flexAna.Hop;
            lock (flexLk) foreach (var n in flexAna.Notes) { if (n.Deleted) continue; float x0 = FlexX((double)n.Start * hop), x1 = FlexX((double)n.End * hop), yc = FlexY(n.Target, na); if (m.IntersectsWith(Rc(x0, yc - 8, Math.Max(3, x1 - x0), 16)) && !flexSel.Contains(n)) flexSel.Add(n); }
        }
        flexDragMode = 0; flexMoved = false;
    }

    // ---- actions ----
    void FlexCorrect()
    {
        if (flexAna == null) return; int mask = PitchCorrector.MaskFor(flexKey, flexScale); var targets = flexSel.Count > 0 ? new List<FlexNote>(flexSel) : new List<FlexNote>(flexAna.Notes.FindAll(n => !n.Deleted)); double amt = flexAmount / 100.0;
        lock (flexLk) foreach (var n in targets) { double sn = FlexPitch.Snap(n.OrigMidi, mask); n.Shift = (sn - n.OrigMidi) * amt; n.Fine = 0; }
        FlexChanged(); Toast("Corrected " + targets.Count + " note" + (targets.Count == 1 ? "" : "s") + (flexScale == 0 ? " to the nearest semitone." : " to " + PitchCorrector.Keys[flexKey] + " " + PitchCorrector.Scales[flexScale].ToLowerInvariant() + "."));
    }
    void FlexScaleMenu()
    {
        var m = Win.NewMenu(); var keyM = new ToolStripMenuItem("Key"); for (int i = 0; i < 12; i++) { int k = i; var it = new ToolStripMenuItem(PitchCorrector.Keys[i]) { Checked = flexKey == i }; it.Click += (a, b) => flexKey = k; keyM.DropDownItems.Add(it); } m.Items.Add(keyM);
        for (int i = 0; i < PitchCorrector.Scales.Length; i++) { int sc = i; var it = new ToolStripMenuItem(PitchCorrector.Scales[i]) { Checked = flexScale == i }; it.Click += (a, b) => flexScale = sc; m.Items.Add(it); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void FlexNoteMenu(FlexNote n)
    {
        if (!flexSel.Contains(n)) { flexSel.Clear(); flexSel.Add(n); } var m = Win.NewMenu(); int hop = flexAna.Hop; int fr = (int)(FlexSampleAt(MousePos.X) / hop);
        m.Items.Add("Split here").Click += (a, b) => { lock (flexLk) { var s = FlexPitch.Split(flexAna, n, fr); if (s == null) Toast("Click further inside the note to split it."); else { flexSel.Clear(); flexSel.Add(n); flexSel.Add(s); } } FlexChanged(); };
        m.Items.Add("Join with the next note").Click += (a, b) => { lock (flexLk) { if (!FlexPitch.Join(flexAna, n)) Toast("There is no note right after this one to join."); } FlexChanged(); };
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Snap to the nearest note").Click += (a, b) => { lock (flexLk) foreach (var q in flexSel) { q.Shift = Math.Round(q.OrigMidi) - q.OrigMidi; q.Fine = 0; } FlexChanged(); };
        m.Items.Add("Steady the pitch (drift 100%)").Click += (a, b) => { lock (flexLk) foreach (var q in flexSel) q.Drift = 100; FlexChanged(); };
        m.Items.Add("Take out the vibrato").Click += (a, b) => { lock (flexLk) foreach (var q in flexSel) q.Vib = 0; FlexChanged(); };
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Reset this note").Click += (a, b) => { lock (flexLk) foreach (var q in flexSel) q.ResetEdits(); FlexChanged(); };
        m.Items.Add(n.Deleted ? "Use this note again" : "Leave this note as recorded").Click += (a, b) => { lock (flexLk) foreach (var q in flexSel) q.Deleted = !n.Deleted; FlexChanged(); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    bool FlexKey(Keys k)
    {
        if (!flexOpen || flexAna == null) return false;
        if (k == (Keys.Control | Keys.A)) { lock (flexLk) { flexSel.Clear(); flexSel.AddRange(flexAna.Notes.FindAll(n => !n.Deleted)); } return true; }
        if (k == Keys.Delete || k == Keys.Back) { if (flexSel.Count == 0) return false; lock (flexLk) foreach (var q in flexSel) q.Deleted = true; FlexChanged(); return true; }
        if (k == Keys.Escape) { FlexClose(true); return true; }
        return false;
    }
}
