using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

// =====================================================================================================
//  AUDIO TIME MACHINE (a tool window; Tools > Audio time machine): scrub back through the real history of a track's sound.
//  Every time a track's audio is replaced (reversed, re-tuned with Flex, or any edit that makes new audio), the version from before is
//  kept. The stages run from the oldest audio, through each edit, to the audio now, and then through its plugins one by one (dry,
//  after the 1st plugin, after the 2nd... to what you hear). Drag the slider (or press HEAR IT on a stage) to hear 8 seconds of that
//  stage from the needle; RESTORE puts an older audio back (the one you had is kept too).
// =====================================================================================================
partial class StudioForm
{
    sealed class ClipVer { public string Label = ""; public AudioClip Clip; public long ClipStart, ClipFrames; public DateTime At; }
    readonly Dictionary<Track, List<ClipVer>> clipHist = new Dictionary<Track, List<ClipVer>>(); readonly Dictionary<Track, AudioClip> clipSeen = new Dictionary<Track, AudioClip>(); readonly Dictionary<Track, long[]> clipSeenTrim = new Dictionary<Track, long[]>();
    int tmStage = -1; string tmPlaying = "";
    // called every frame: a track whose audio object changed keeps the old one in its history
    void TmWatch()
    {
        foreach (var t in eng.Tracks.ToArray())
        {
            AudioClip was; clipSeen.TryGetValue(t, out was); var now = t.Clip;
            if (was != null && now != null && !ReferenceEquals(was, now))
            {
                string why = (now.Name ?? "").EndsWith("(reversed)") ? "Before reverse" : (flexOpen ? "Before Flex" : "Before an edit"); long[] tr; clipSeenTrim.TryGetValue(t, out tr);
                List<ClipVer> l; if (!clipHist.TryGetValue(t, out l)) clipHist[t] = l = new List<ClipVer>(); if (!l.Any(v => ReferenceEquals(v.Clip, was))) { l.Add(new ClipVer { Label = why, Clip = was, ClipStart = tr != null ? tr[0] : 0, ClipFrames = tr != null ? tr[1] : -1, At = DateTime.Now }); if (l.Count > 30) l.RemoveAt(0); }
            }
            clipSeen[t] = now; clipSeenTrim[t] = new[] { t.ClipStart, t.ClipFrames };
        }
    }
    sealed class TmStage { public string Label = ""; public ClipVer Ver; public int Plugins = -1; }   // Ver null = the audio now; Plugins: -1 = no plugin step (an edit), 0 = dry, k = after the k-th plugin
    List<TmStage> TmStages(Track t)
    {
        var l = new List<TmStage>(); List<ClipVer> vs; if (t.Clip != null && clipHist.TryGetValue(t, out vs)) for (int i = 0; i < vs.Count; i++) l.Add(new TmStage { Label = (i == 0 ? "Original" : vs[i].Label) + "  (" + vs[i].At.ToString("HH:mm") + ")", Ver = vs[i] });
        l.Add(new TmStage { Label = "Now, dry (no plugins)", Plugins = 0 });
        var fx = t.Fx.ToList(); for (int k = 0; k < fx.Count; k++) l.Add(new TmStage { Label = "After " + fx[k].Title + (fx[k].Bypass ? " (bypassed)" : ""), Plugins = k + 1 });
        return l;
    }
    // 8 seconds of a stage from the needle (or the start of the clip), played in the preview player
    void TmHear(Track t, TmStage st)
    {
        var tmp = StudioEngine.NewTrack(t.Name, "audio", st.Ver != null ? st.Ver.Clip : t.Clip); tmp.Instrument = t.Instrument && st.Ver == null; tmp.Offset = t.Offset; tmp.ClipStart = st.Ver != null ? st.Ver.ClipStart : t.ClipStart; tmp.ClipFrames = st.Ver != null ? st.Ver.ClipFrames : t.ClipFrames;
        if (tmp.Instrument) lock (t.Midi) tmp.Midi.AddRange(t.Midi);
        int n = st.Plugins <= 0 ? 0 : st.Plugins; foreach (var f in t.Fx.Take(n)) { try { var c = FxRegistry.Create(f.Type); if (c == null) continue; c.Prepare(StudioEngine.SR); c.Deserialize(f.Serialize()); c.Bypass = f.Bypass; var inf = c as InstrumentFx; if (inf != null) inf.Owner = tmp; tmp.Fx.Add(c); } catch { } }
        if (st.Plugins > 0 && t.Instrument && st.Ver == null && !tmp.Fx.OfType<InstrumentFx>().Any()) { }   // (a MIDI track with no instrument plays its built-in keys)
        long from = Math.Max(t.Offset, eng.Pos); if (t.Clip != null && from > t.Offset + t.UsedLength - StudioEngine.SR) from = t.Offset; float[] l, r;
        eng.Render(from, StudioEngine.SR * 8, new RenderOpts { Master = false, Fader = false, MuteSolo = false, Only = new List<Track> { tmp } }, null, out l, out r);
        string path = Path.Combine(Path.GetTempPath(), "purphex-timemachine.wav"); try { WavIO.Write(path, l, r, StudioEngine.SR, 24); } catch { path = Path.Combine(Path.GetTempPath(), "purphex-timemachine-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".wav"); WavIO.Write(path, l, r, StudioEngine.SR, 24); }
        if (eng.Playing) eng.Pause(); Auditioner.Stop(); Auditioner.Play(new SoundItem { Name = st.Label, Kind = "one-shot", Path = path }, null); tmPlaying = st.Label;
    }
    void TmRestore(Track t, ClipVer v)
    {
        PushUndo(); List<ClipVer> l; if (clipHist.TryGetValue(t, out l)) l.Add(new ClipVer { Label = "Before restoring", Clip = t.Clip, ClipStart = t.ClipStart, ClipFrames = t.ClipFrames, At = DateTime.Now });
        lock (eng.Sync) { t.Clip = v.Clip; t.ClipStart = v.ClipStart; t.ClipFrames = v.ClipFrames; } clipSeen[t] = v.Clip; Toast(t.Name + " has its " + v.Label.ToLowerInvariant().Replace("before ", "audio from before the ") + " back. The audio you had is kept in the time machine too.");
    }
    void PaintTimeMachine(Graphics g, RectangleF a)
    {
        var t = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null;
        Txt(g, "AUDIO TIME MACHINE" + (t != null ? "  ·  " + t.Name.ToUpperInvariant() : ""), 15, Ink, Rc(a.X + 6, a.Y + 4, a.Width - 12, 24), StringAlignment.Near, true);
        if (t == null || t.IsBus) { Txt(g, "Click a track on the timeline to see its history.", 12, DimC, Rc(a.X + 6, a.Y + 40, a.Width - 12, 18), StringAlignment.Near, false); return; }
        var st = TmStages(t); if (tmStage < 0 || tmStage >= st.Count) tmStage = st.Count - 1;
        Txt(g, "Drag through the stages and press HEAR IT (8 seconds from the needle). Older audio comes from every edit that made new audio; then the plugins one by one.", 10.5f, DimC, Rc(a.X + 6, a.Y + 30, a.Width - 12, 18), StringAlignment.Near, false);
        // the scrub: one dot per stage on a line, the picked one lit
        var line = Rc(a.X + 30, a.Y + 74, a.Width - 60, 26); float step = st.Count > 1 ? line.Width / (st.Count - 1) : 0;
        using (var pen = new Pen(Color.FromArgb(150, NeonViolet), 2f)) g.DrawLine(pen, line.X, line.Y + 13, line.Right, line.Y + 13);
        for (int i = 0; i < st.Count; i++) { float x = line.X + i * step; bool on = i == tmStage, edit = st[i].Plugins < 0; Color c = edit ? NeonPink : (st[i].Plugins == 0 ? NeonCyan : NeonViolet); using (var b = new SolidBrush(on ? Color.White : c)) g.FillEllipse(b, x - (on ? 9 : 6), line.Y + 13 - (on ? 9 : 6), on ? 18 : 12, on ? 18 : 12); int ii = i; Reg(Rc(x - 12, line.Y, 24, 26), () => tmStage = ii); }
        var sc = Reg(line, () => { }); sc.OnDrag = pt => { if (st.Count > 1) tmStage = Math.Max(0, Math.Min(st.Count - 1, (int)Math.Round((pt.X - line.X) / step))); }; sc.OnUp = () => { if (tmStage >= 0 && tmStage < st.Count) TmHear(t, st[tmStage]); };
        Txt(g, st[tmStage].Label, 14, Ink, Rc(a.X + 6, line.Bottom + 8, a.Width - 160, 22), StringAlignment.Near, true);
        var hb = Rc(a.Right - 150, line.Bottom + 4, 144, 30); Btn(g, hb, "HEAR IT", 2, () => TmHear(t, st[tmStage]), 12); Hint2(hb, "Hear 8 seconds of this stage from the needle");
        // every stage as a row
        float y = line.Bottom + 48; Txt(g, "STAGES", 11, DimC, Rc(a.X + 6, y, 200, 16), StringAlignment.Near, true); y += 20;
        for (int i = st.Count - 1; i >= 0 && y < a.Bottom - 30; i--)
        {
            var s = st[i]; var row = Rc(a.X + 6, y, a.Width - 12, 34); y += 40; bool on = i == tmStage; FillRR(g, on ? TM.C(230, 50, 22, 96) : TM.C(200, 20, 10, 44), row, 9);
            Txt(g, (i + 1) + ".  " + s.Label, 11.5f, Ink, Rc(row.X + 12, row.Y, row.Width - 300, row.Height), StringAlignment.Near, true);
            var ss = s; int ii = i; var h2 = Rc(row.Right - 280, row.Y + 4, 130, row.Height - 8); Btn(g, h2, tmPlaying == s.Label && Auditioner.IsPlaying ? "PLAYING" : "HEAR IT", 1, () => { tmStage = ii; TmHear(t, ss); }, 10);
            if (s.Ver != null) { var rb = Rc(row.Right - 140, row.Y + 4, 132, row.Height - 8); Btn(g, rb, "RESTORE", 2, () => TmRestore(t, ss.Ver), 10); Hint2(rb, "Put this audio back on the track (the audio you have now is kept here too)"); }
            Reg(Rc(row.X, row.Y, row.Width - 290, row.Height), () => tmStage = ii);
        }
    }
}
