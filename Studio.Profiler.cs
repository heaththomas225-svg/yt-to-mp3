using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

// =====================================================================================================
//  LIVE PROJECT PROFILER (tab 13; Tools > Live profiler): while the song plays the engine times every plugin (Fx.CpuTicks), and this page
//  shows how much of the real-time budget each one, and each track's chain, takes, which add delay (latency), and the sound card's dropouts.
//  Heavy chains are marked with FREEZE: the track is played once into audio (its plugins then rest), UNFREEZE brings the live chain back.
// =====================================================================================================
partial class StudioForm
{
    sealed class ProfRow { public Track T; public Fx F; public double Load, Peak; public int Lat; }
    readonly Dictionary<Fx, double> profLoad = new Dictionary<Fx, double>(), profPeak = new Dictionary<Fx, double>(); readonly Dictionary<Fx, long> profLast = new Dictionary<Fx, long>();
    long profAt; int profUnder0 = -1; float cpuScroll;
    // every half second: how much of the time each plugin took since the last look (an average, plus the highest)
    void ProfSample()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp(); if (profAt == 0) { profAt = now; return; } double span = now - profAt; if (span < System.Diagnostics.Stopwatch.Frequency / 2) return; profAt = now;
        foreach (var t in eng.Tracks.ToArray()) foreach (var f in t.Fx.ToArray())
        {
            long cur = f.CpuTicks, last; profLast.TryGetValue(f, out last); profLast[f] = cur; double load = Math.Max(0, (cur - last) / span * 100);
            double old; profLoad.TryGetValue(f, out old); double sm = old * 0.6 + load * 0.4; profLoad[f] = sm; double pk; profPeak.TryGetValue(f, out pk); profPeak[f] = Math.Max(pk * 0.97, load);
        }
    }
    void PaintProfiler(Graphics g) { PaintProfiler(g, Rc(14, ToolH + 12, ClientSize.Width - 28, ClientSize.Height - ToolH - 26)); }
    void PaintProfiler(Graphics g, RectangleF a)
    {
        if (eng.Playing) { ProfSample(); lastInput = DateTime.Now; }
        FillRR(g, TM.C(225, 12, 6, 28), a, 14); StrokeRR(g, Color.FromArgb(180, NeonViolet), a, 14, 1.3f);
        var rows = new List<ProfRow>();
        foreach (var t in eng.Tracks.ToArray()) foreach (var f in t.Fx.ToArray()) { double l, p; profLoad.TryGetValue(f, out l); profPeak.TryGetValue(f, out p); rows.Add(new ProfRow { T = t, F = f, Load = l, Peak = p, Lat = f.Latency }); }
        double total = rows.Sum(r => r.Load); int under = AudioOut.Underruns; if (profUnder0 < 0) profUnder0 = under;
        // the summary cards
        float cw = (a.Width - 28 - 30) / 4f, cy = a.Y + 14; string[] heads = { "PLUGINS USE", "DROPOUTS", "MOST DELAY", "PLUGINS" };
        int maxLat = rows.Count == 0 ? 0 : rows.Max(r => r.Lat);
        string[] vals = { total.ToString("0.0") + " %", (under - profUnder0).ToString(), (maxLat * 1000.0 / StudioEngine.SR).ToString("0.0") + " ms", rows.Count.ToString() };
        string[] subs = { "of the real-time budget", "since this page opened", "the most latency on one track", "on " + eng.Tracks.Count(t => !t.IsBus && t.Host == null) + " tracks" };
        for (int i = 0; i < 4; i++) { var c = Rc(a.X + 14 + i * (cw + 10), cy, cw, 74); FillRR(g, TM.C(230, 22, 10, 48), c, 12); StrokeRR(g, Color.FromArgb(150, i == 1 && under > profUnder0 ? Warn : NeonCyan), c, 12, 1.2f); Txt(g, heads[i], 10, DimC, Rc(c.X + 12, c.Y + 8, c.Width - 24, 14), StringAlignment.Near, true); Txt(g, vals[i], 20, Ink, Rc(c.X + 12, c.Y + 24, c.Width - 24, 28), StringAlignment.Near, true); Txt(g, subs[i], 9.5f, DimC, Rc(c.X + 12, c.Y + 52, c.Width - 24, 14), StringAlignment.Near, false); }
        if (!eng.Playing) Txt(g, "PRESS PLAY: the plugins are timed while the song plays", 12, NeonCyan, Rc(a.X + 14, cy + 82, a.Width - 28, 18), StringAlignment.Near, true);
        // per track: the chain's total, its heaviest plugin, and FREEZE when it is heavy
        float y = cy + 108; Txt(g, "TRACKS (HEAVIEST FIRST)", 11, DimC, Rc(a.X + 14, y, 300, 16), StringAlignment.Near, true); y += 22;
        var byTrack = rows.GroupBy(r => r.T).Select(gr => new { T = gr.Key, Load = gr.Sum(r => r.Load), Top = gr.OrderByDescending(r => r.Load).First(), Lat = gr.Sum(r => r.Lat), N = gr.Count() }).OrderByDescending(x => x.Load).ToList();
        foreach (var tr in eng.Tracks.ToArray()) if (frozen.ContainsKey(tr) && !byTrack.Any(x => x.T == tr)) byTrack.Add(new { T = tr, Load = 0.0, Top = (ProfRow)null, Lat = 0, N = 0 });
        float listH = a.Bottom - y - 16, rowH = 44; cpuScroll = Math.Max(0, Math.Min(Math.Max(0, byTrack.Count * rowH - listH), cpuScroll)); var listR = Rc(a.X + 10, y, a.Width - 20, listH);
        regs.Add(new HitReg { R = listR, OnWheel = d => cpuScroll -= d * 40 }); var gs = g.Save(); g.SetClip(listR);
        for (int i = 0; i < byTrack.Count; i++)
        {
            var x = byTrack[i]; var row = Rc(listR.X + 4, y + i * rowH - cpuScroll, listR.Width - 8, rowH - 6); if (row.Bottom < listR.Y || row.Y > listR.Bottom) continue;
            bool heavy = x.Load >= 12 || (x.Top != null && x.Top.Load >= 8); bool isFrozen = frozen.ContainsKey(x.T);
            FillRR(g, heavy ? TM.C(220, 60, 18, 60) : TM.C(200, 24, 12, 50), row, 10); if (heavy) StrokeRR(g, Color.FromArgb(200, Warn), row, 10, 1.2f);
            Txt(g, x.T.Name, 12, Ink, Rc(row.X + 12, row.Y, 220, row.Height), StringAlignment.Near, true);
            var bar = Rc(row.X + 240, row.Y + 14, Math.Max(60, row.Width - 760), 10); FillRR(g, TM.C(200, 10, 4, 22), bar, 5); FillRR(g, heavy ? Warn : NeonCyan, Rc(bar.X, bar.Y, (float)(bar.Width * Math.Min(1, x.Load / 30)), bar.Height), 5);
            Txt(g, x.Load.ToString("0.0") + " %", 12, Ink, Rc(bar.Right + 10, row.Y, 70, row.Height), StringAlignment.Near, true);
            string what = isFrozen ? "FROZEN: playing its audio, its plugins rest" : x.Top == null ? "no plugins" : "heaviest: " + x.Top.F.Title + " " + x.Top.Load.ToString("0.0") + " % (peak " + x.Top.Peak.ToString("0.0") + " %)" + (x.Lat > 0 ? "  ·  delay " + (x.Lat * 1000.0 / StudioEngine.SR).ToString("0.0") + " ms" : "");
            Txt(g, what, 10.5f, heavy ? TM.C(255, 255, 200, 150) : DimC, Rc(bar.Right + 84, row.Y, 340, row.Height), StringAlignment.Near, false);
            var fb = Rc(row.Right - 112, row.Y + 7, 104, row.Height - 14); var tt = x.T;
            if (isFrozen) { Btn(g, fb, "UNFREEZE", 4, () => Unfreeze(tt), 10); Hint2(fb, "Bring back the live plugins of " + tt.Name); }
            else if (x.N > 0) { Btn(g, fb, heavy ? "FREEZE" : "freeze", heavy ? 2 : 0, () => Freeze(tt), 10); Hint2(fb, heavy ? "This chain is heavy: play it once into audio so its plugins rest (UNFREEZE brings them back)" : "Play this track once into audio so its plugins rest"); }
        }
        g.Restore(gs); if (byTrack.Count == 0) Txt(g, "No plugins yet.", 12, DimC, Rc(listR.X + 10, listR.Y + 6, 300, 18), StringAlignment.Near, false);
    }

    // FREEZE: the track (its clip through its plugins) is played once into audio on a new track; the original is switched off (kept, so UNFREEZE brings it back)
    readonly Dictionary<Track, Track> frozen = new Dictionary<Track, Track>();
    void Freeze(Track t)
    {
        if (frozen.ContainsKey(t)) return; long end = 0; foreach (var p in Exporter.RowSet(eng, t)) end = Math.Max(end, p.Offset + p.UsedLength); if (t.Instrument) lock (t.Midi) foreach (var m in t.Midi) end = Math.Max(end, m.Frame);
        if (end <= 0) { Toast(t.Name + " has nothing to freeze yet."); return; }
        float[] l, r; var only = Exporter.RowSet(eng, t); eng.Render(0, end + StudioEngine.SR * 2, new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = only }, null, out l, out r);
        string dir = PatternAudioDir(); string path = System.IO.Path.Combine(dir, "Frozen " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".wav"); WavIO.Write(path, l, r, StudioEngine.SR, 24);
        PushUndo(); var clip = AudioClip.FromArrays(l, r, t.Name + " (frozen)"); clip.Path = path; var fz = StudioEngine.NewTrack(t.Name + " (frozen)", "audio", clip); fz.Color = t.Color; fz.VolDb = t.VolDb; fz.Pan = t.Pan; fz.Out = t.Out;
        AddTrack(fz); lock (eng.Sync) t.Off = true; frozen[t] = fz; Toast(t.Name + " is frozen: it plays as audio now and its plugins rest. UNFREEZE brings them back.");
    }
    void Unfreeze(Track t)
    {
        Track fz; if (!frozen.TryGetValue(t, out fz)) return; PushUndo(); lock (eng.Sync) { eng.Tracks.Remove(fz); t.Off = false; } frozen.Remove(t); Toast(t.Name + " plays live again.");
    }
}
