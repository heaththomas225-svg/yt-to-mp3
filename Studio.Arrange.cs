using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

// =====================================================================================================
//  ARRANGEMENT INTELLIGENCE (tab 14; Tools > Arrangement intelligence): the song is played once through the master (streamed: only small
//  measurements are kept, never the whole song in memory) and measured bar by bar: energy (level), density (how many hits and changes),
//  loudness, and the balance of bass, mids and highs. Where those change a lot, a new section starts. Each section gets a likely name
//  (intro, verse, chorus, break, outro) and notes when it may feel repetitive (very like the one before it, or the same thing for a
//  long time) or empty (very little going on). It only points things out: nothing in the song is changed.
// =====================================================================================================
partial class StudioForm
{
    sealed class ArrSection { public int Bar0, Bar1; public string Name = ""; public double Energy, Density, Low, Mid, High, Lufs; public List<string> Notes = new List<string>(); public bool Repetitive, Empty; }
    sealed class ArrResult { public double Bpm, Seconds; public int Bars; public double[] BarEnergy, BarDensity, BarLow, BarMid, BarHigh; public float[] PeakMin, PeakMax; public List<ArrSection> Sections = new List<ArrSection>(); }
    ArrResult arr; bool arrRunning;

    void AnalyseArrangement()
    {
        if (arrRunning || eng.Tracks.Count == 0) { if (eng.Tracks.Count == 0) Toast("Add music first."); return; }
        long end = eng.EndFrame; if (end < StudioEngine.SR * 8) { Toast("The song is too short to look at its arrangement (8 seconds or more)."); return; }
        end = Math.Min(end, (long)StudioEngine.SR * 900); arrRunning = true; ArrResult res = null;
        RunBusy("Listening to the arrangement...", true, prog => { res = MeasureArrangement(end, prog); }, ok => { arrRunning = false; if (ok && res != null) { arr = res; Toast("Done: " + res.Sections.Count + " sections. The notes only point things out; nothing was changed."); } Invalidate(); });
    }

    ArrResult MeasureArrangement(long end, Action<float> prog)
    {
        const int hop = 1024; int sr = StudioEngine.SR; var rms = new List<float>(); var lo = new List<float>(); var mi = new List<float>(); var hi = new List<float>(); var flux = new List<float>(); var pmin = new List<float>(); var pmax = new List<float>();
        double aLo = 1 - Math.Exp(-2 * Math.PI * 200.0 / sr), aHi = 1 - Math.Exp(-2 * Math.PI * 2500.0 / sr); double sLo = 0, sHi = 0; double accE = 0, accL = 0, accM = 0, accH = 0; int cnt = 0; float mn = 0, mx = 0; double pL = 0, pM = 0, pH = 0;
        eng.RenderStream(0, end, new RenderOpts { Master = true }, prog, (bl, br, off, n) =>
        {
            for (int i = 0; i < n; i++)
            {
                double m = 0.5 * (bl[off + i] + br[off + i]); sLo += aLo * (m - sLo); sHi += aHi * (m - sHi); double low = sLo, high = m - sHi, mid = m - low - high;
                accE += m * m; accL += low * low; accM += mid * mid; accH += high * high; if (m < mn) mn = (float)m; if (m > mx) mx = (float)m;
                if (++cnt == hop)
                {
                    double eL = accL / hop, eM = accM / hop, eH = accH / hop; rms.Add((float)Math.Sqrt(accE / hop)); lo.Add((float)eL); mi.Add((float)eM); hi.Add((float)eH);
                    flux.Add((float)(Math.Max(0, Math.Sqrt(eL) - Math.Sqrt(pL)) + Math.Max(0, Math.Sqrt(eM) - Math.Sqrt(pM)) * 1.5 + Math.Max(0, Math.Sqrt(eH) - Math.Sqrt(pH)) * 2)); pL = eL; pM = eM; pH = eH;
                    pmin.Add(mn); pmax.Add(mx); accE = accL = accM = accH = 0; cnt = 0; mn = mx = 0;
                }
            }
        }, () => cancelFlag);
        if (cancelFlag) return null;
        var r = new ArrResult { Bpm = Fx.ProjectBpm, Seconds = end / (double)sr, PeakMin = pmin.ToArray(), PeakMax = pmax.ToArray() };
        double barFrames = sr * 60.0 / Math.Max(40, Fx.ProjectBpm) * 4; int hopsPerBar = Math.Max(1, (int)Math.Round(barFrames / hop)); int bars = Math.Max(1, rms.Count / hopsPerBar); r.Bars = bars;
        r.BarEnergy = new double[bars]; r.BarDensity = new double[bars]; r.BarLow = new double[bars]; r.BarMid = new double[bars]; r.BarHigh = new double[bars];
        double fluxMean = flux.Count > 0 ? flux.Average() : 0;
        for (int b = 0; b < bars; b++)
        {
            int h0 = b * hopsPerBar, h1 = Math.Min(rms.Count, h0 + hopsPerBar); double e = 0, l = 0, m = 0, h = 0; int hits = 0;
            for (int k = h0; k < h1; k++) { e += rms[k] * rms[k]; l += lo[k]; m += mi[k]; h += hi[k]; if (k > 0 && flux[k] > fluxMean * 1.8 && flux[k] > flux[k - 1]) hits++; }
            int cn = Math.Max(1, h1 - h0); r.BarEnergy[b] = 10 * Math.Log10(e / cn + 1e-10); double tot = l + m + h + 1e-12; r.BarLow[b] = l / tot; r.BarMid[b] = m / tot; r.BarHigh[b] = h / tot; r.BarDensity[b] = hits;
        }
        // where a new section starts: the change between the 4 bars before and the 4 bars after (energy, density, balance), at its peaks, at least 4 bars apart
        var nov = new double[bars]; double eMax = r.BarEnergy.Max(), eMin = r.BarEnergy.Min(); double eRange = Math.Max(3, eMax - eMin), dMax = Math.Max(1, r.BarDensity.Max());
        Func<int, int, double[]> prof = (b0, b1) => { b0 = Math.Max(0, b0); b1 = Math.Min(bars, b1); int c = Math.Max(1, b1 - b0); double e = 0, d = 0, l = 0, m = 0, h = 0; for (int b = b0; b < b1; b++) { e += r.BarEnergy[b]; d += r.BarDensity[b]; l += r.BarLow[b]; m += r.BarMid[b]; h += r.BarHigh[b]; } return new[] { (e / c - eMin) / eRange, d / c / dMax, l / c, m / c, h / c }; };
        for (int b = 4; b < bars - 2; b++) { var p0 = prof(b - 4, b); var p1 = prof(b, b + 4); double d = 0; for (int k = 0; k < 5; k++) d += (p1[k] - p0[k]) * (p1[k] - p0[k]) * (k == 0 ? 2.5 : 1); nov[b] = Math.Sqrt(d); }
        double nMean = nov.Average(), nStd = Math.Sqrt(nov.Select(v => (v - nMean) * (v - nMean)).Average()); var cuts = new List<int> { 0 };
        for (int b = 4; b < bars - 2; b++) if (nov[b] > nMean + nStd * 0.9 && nov[b] >= nov[b - 1] && nov[b] >= nov[b + 1] && b - cuts[cuts.Count - 1] >= 4) { int snap = (int)(Math.Round(b / 4.0) * 4); if (snap - cuts[cuts.Count - 1] >= 4 && snap < bars) cuts.Add(snap); }
        cuts.Add(bars);
        for (int i = 0; i + 1 < cuts.Count; i++) { if (cuts[i + 1] <= cuts[i]) continue; var p = prof(cuts[i], cuts[i + 1]); var s = new ArrSection { Bar0 = cuts[i], Bar1 = cuts[i + 1], Energy = p[0], Density = p[1], Low = p[2], Mid = p[3], High = p[4], Lufs = Enumerable.Range(cuts[i], cuts[i + 1] - cuts[i]).Average(b => r.BarEnergy[b]) }; r.Sections.Add(s); }
        // likely names: by how much is going on, compared with the rest of the song
        var ranked = r.Sections.OrderBy(s => s.Energy + s.Density * 0.5).ToList();
        for (int i = 0; i < r.Sections.Count; i++)
        {
            var s = r.Sections[i]; double rank = r.Sections.Count < 2 ? 0.5 : ranked.IndexOf(s) / (double)(r.Sections.Count - 1);
            s.Name = i == 0 && rank < 0.5 ? "Intro" : (i == r.Sections.Count - 1 && rank < 0.5 ? "Outro" : (rank >= 0.7 ? "Chorus" : (rank <= 0.25 ? "Break" : "Verse")));
        }
        // notes: repetitive (very like the section before, or one thing for a long time), empty (very little going on)
        for (int i = 0; i < r.Sections.Count; i++)
        {
            var s = r.Sections[i]; int len = s.Bar1 - s.Bar0;
            if (s.Energy < 0.18 && s.Density < 0.2 && len >= 4 && i > 0 && i < r.Sections.Count - 1) { s.Empty = true; s.Notes.Add("Very little is happening here (low level and few hits). If that is not a planned break, a pad, a riser or a drum fill would keep it moving."); }
            if (i > 0)
            {
                var q = r.Sections[i - 1]; double d = Math.Abs(s.Energy - q.Energy) * 2.5 + Math.Abs(s.Density - q.Density) + Math.Abs(s.Low - q.Low) + Math.Abs(s.Mid - q.Mid) + Math.Abs(s.High - q.High);
                if (d < 0.12 && len >= 8) { s.Repetitive = true; s.Notes.Add("Sounds a lot like the " + q.Name.ToLowerInvariant() + " before it (same level, busyness and tone). A change here (a new layer, a filter sweep, a drop-out or a different drum pattern) would make it feel new."); }
            }
            if (len >= 24) { s.Repetitive = true; s.Notes.Add("One section for " + len + " bars without much change. Splitting it, or adding a small change every 8 bars, keeps the listener with you."); }
            if (s.High < 0.04 && s.Energy > 0.5) s.Notes.Add("Little top end here for how loud it is: it may feel dull next to the other sections.");
        }
        return r;
    }

    static readonly Color[] ArrCols = { Color.FromArgb(255, 90, 140, 255), Color.FromArgb(255, 61, 217, 255), Color.FromArgb(255, 255, 120, 60), Color.FromArgb(255, 176, 92, 255), Color.FromArgb(255, 255, 92, 200) };
    static Color ArrColour(string name) { switch (name) { case "Intro": return ArrCols[0]; case "Verse": return ArrCols[1]; case "Chorus": return ArrCols[2]; case "Break": return ArrCols[3]; default: return ArrCols[4]; } }

    void PaintArrangement(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH + 12; var a = Rc(14, top, W - 28, H - top - 14);
        FillRR(g, TM.C(225, 12, 6, 28), a, 14); StrokeRR(g, Color.FromArgb(180, NeonViolet), a, 14, 1.3f);
        var an = Rc(a.Right - 220, a.Y + 12, 206, 34); Btn(g, an, arr == null ? "ANALYSE THE SONG" : "ANALYSE AGAIN", 2, () => AnalyseArrangement(), 12); Hint2(an, "Play the song once through the master and measure its arrangement (nothing is changed)");
        Txt(g, "ARRANGEMENT INTELLIGENCE", 16, Ink, Rc(a.X + 16, a.Y + 14, 500, 26), StringAlignment.Near, true);
        if (arr == null) { TxtWrap(g, "Press ANALYSE THE SONG. Purphex listens to the whole song and measures its energy, how busy it is, its loudness and its balance of bass, mids and highs, bar by bar. It marks the sections and points out any that may feel repetitive or empty. It never changes your music.", 13, DimC, Rc(a.X + 16, a.Y + 60, Math.Min(760, a.Width - 32), 90)); return; }
        var r = arr; float x0 = a.X + 16, x1 = a.Right - 16, wv = x1 - x0;
        // the cards
        string[] heads = { "BPM", "LENGTH", "SECTIONS", "NOTES" }; string[] vals = { r.Bpm.ToString("0.#"), ((int)(r.Seconds / 60)) + ":" + ((int)(r.Seconds % 60)).ToString("00"), r.Sections.Count.ToString(), r.Sections.Sum(s => s.Notes.Count).ToString() };
        float cw = (wv - 30) / 4f; for (int i = 0; i < 4; i++) { var c = Rc(x0 + i * (cw + 10), a.Y + 54, cw, 56); FillRR(g, TM.C(230, 22, 10, 48), c, 12); StrokeRR(g, Color.FromArgb(140, NeonCyan), c, 12, 1.1f); Txt(g, heads[i], 9.5f, DimC, Rc(c.X + 12, c.Y + 6, c.Width - 24, 14), StringAlignment.Near, true); Txt(g, vals[i], 18, Ink, Rc(c.X + 12, c.Y + 22, c.Width - 24, 28), StringAlignment.Near, true); }
        // the sections strip and the waveform coloured by section
        float sy = a.Y + 124, wy = sy + 30, wh = 90; float pxPerBar = wv / r.Bars;
        foreach (var s in r.Sections)
        {
            var sr2 = Rc(x0 + s.Bar0 * pxPerBar, sy, (s.Bar1 - s.Bar0) * pxPerBar - 2, 24); Color c = ArrColour(s.Name);
            FillRR(g, Color.FromArgb(s.Empty || s.Repetitive ? 230 : 170, c), sr2, 6); if (s.Empty || s.Repetitive) StrokeRR(g, Color.White, sr2, 6, 1.6f);
            Txt(g, s.Name.ToUpperInvariant() + (s.Notes.Count > 0 ? "  !" : ""), 9.5f, Color.White, sr2, StringAlignment.Center, true);
            Hint2(sr2, s.Name + ": bars " + (s.Bar0 + 1) + " to " + s.Bar1 + (s.Notes.Count > 0 ? ". " + s.Notes[0] : ""));
        }
        { int n = r.PeakMax.Length; float cyw = wy + wh / 2; for (int px = 0; px < (int)wv; px++) { int k0 = (int)((long)px * n / (int)wv), k1 = Math.Max(k0 + 1, (int)((long)(px + 1) * n / (int)wv)); float mn = 0, mx = 0; for (int k = k0; k < Math.Min(n, k1); k++) { mn = Math.Min(mn, r.PeakMin[k]); mx = Math.Max(mx, r.PeakMax[k]); } int bar = Math.Min(r.Bars - 1, (int)(px / pxPerBar)); var sec = r.Sections.FirstOrDefault(s => bar >= s.Bar0 && bar < s.Bar1); using (var pen = new Pen(Color.FromArgb(220, sec == null ? NeonCyan : ArrColour(sec.Name)), 1f)) g.DrawLine(pen, x0 + px, cyw - mx * wh / 2, x0 + px, cyw - mn * wh / 2); } }
        // energy and density along the song
        float ey = wy + wh + 14, eh = 70; FillRR(g, TM.C(200, 10, 4, 24), Rc(x0, ey, wv, eh), 8);
        double eMax = r.BarEnergy.Max(), eMin = r.BarEnergy.Min(), dMax = Math.Max(1, r.BarDensity.Max());
        var pe = new List<PointF>(); var pd = new List<PointF>(); for (int b = 0; b < r.Bars; b++) { float xx = x0 + (b + 0.5f) * pxPerBar; pe.Add(new PointF(xx, ey + eh - 6 - (float)((r.BarEnergy[b] - eMin) / Math.Max(1, eMax - eMin)) * (eh - 12))); pd.Add(new PointF(xx, ey + eh - 6 - (float)(r.BarDensity[b] / dMax) * (eh - 12))); }
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; if (pe.Count > 1) { using (var pen = new Pen(NeonPink, 2f)) g.DrawLines(pen, pe.ToArray()); using (var pen = new Pen(Color.FromArgb(200, NeonCyan), 1.4f) { DashPattern = new[] { 3f, 2f } }) g.DrawLines(pen, pd.ToArray()); } g.SmoothingMode = oldSm;
        Txt(g, "ENERGY", 9, NeonPink, Rc(x0 + 8, ey + 4, 80, 12), StringAlignment.Near, true); Txt(g, "DENSITY (HITS AND CHANGES)", 9, NeonCyan, Rc(x0 + 80, ey + 4, 220, 12), StringAlignment.Near, true);
        // the notes, section by section (with its bass / mid / high balance)
        float ny = ey + eh + 14; Txt(g, "WHAT TO LOOK AT", 11, DimC, Rc(x0, ny, 300, 16), StringAlignment.Near, true); ny += 20;
        foreach (var s in r.Sections)
        {
            if (ny > a.Bottom - 30) break; Color c = ArrColour(s.Name); var row = Rc(x0, ny, wv, s.Notes.Count == 0 ? 26 : 22 + s.Notes.Count * 18);
            using (var b = new SolidBrush(c)) g.FillRectangle(b, row.X, row.Y + 4, 4, row.Height - 8);
            Txt(g, s.Name + "  ·  bars " + (s.Bar0 + 1) + "-" + s.Bar1, 11, Ink, Rc(row.X + 12, row.Y + 2, 260, 18), StringAlignment.Near, true);
            var bal = Rc(row.X + 280, row.Y + 7, 120, 8); float bx = bal.X; foreach (var part in new[] { new { V = s.Low, C = ArrCols[0] }, new { V = s.Mid, C = ArrCols[3] }, new { V = s.High, C = ArrCols[1] } }) { float bw = (float)(part.V * bal.Width); using (var b = new SolidBrush(part.C)) g.FillRectangle(b, bx, bal.Y, bw, bal.Height); bx += bw; }
            Hint2(bal, "Balance: bass " + Math.Round(s.Low * 100) + "%, mids " + Math.Round(s.Mid * 100) + "%, highs " + Math.Round(s.High * 100) + "%");
            if (s.Notes.Count == 0) Txt(g, "Nothing to point out.", 10, DimC, Rc(row.X + 412, row.Y + 2, wv - 420, 18), StringAlignment.Near, false);
            for (int k = 0; k < s.Notes.Count; k++) Txt(g, s.Notes[k], 10, TM.C(255, 255, 215, 170), Rc(row.X + 12, row.Y + 20 + k * 18, wv - 24, 16), StringAlignment.Near, false);
            ny += row.Height + 6;
        }
    }
}
