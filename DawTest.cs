using System;
using System.Collections.Generic;

// Tests the DAW editing model: trim / split playback, project save of trims, tempo detection, Auto Mix on an unsplit song.   csc /main:D ... DawTest.cs
static class D
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static AudioClip BeatClip(double bpm, int seconds)
    {
        int sr = 44100, n = sr * seconds; var l = new float[n]; var rnd = new Random(5); double beat = sr * 60.0 / bpm;
        for (int b = 0; b * beat < n; b++) { int s = (int)(b * beat); for (int i = 0; i < 4000 && s + i < n; i++) l[s + i] += (float)(Math.Sin(2 * Math.PI * (60 + 80 * Math.Exp(-i / 500.0)) * i / sr) * Math.Exp(-i / 1200.0) * 0.9); int h = (int)(s + beat / 2); for (int i = 0; i < 900 && h + i < n; i++) l[h + i] += (float)((rnd.NextDouble() * 2 - 1) * Math.Exp(-i / 200.0) * 0.3); }
        return AudioClip.FromArrays(l, (float[])l.Clone(), "beat");
    }
    static string FfBin(string n) { foreach (var f in System.IO.Directory.GetFiles(System.IO.Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"), n, System.IO.SearchOption.AllDirectories)) return f; return null; }
    [STAThread] static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        string ffm = FfBin("ffmpeg.exe"), ffp = FfBin("ffprobe.exe"); StudioEnv.Ffmpeg = () => ffm; StudioEnv.Ffprobe = () => ffp;
        foreach (double bpm in new[] { 90.0, 128.0, 140.0 })
        {
            var c = BeatClip(bpm, 70); double d = Tempo.Detect(c, 0, 60);
            Check("tempo detection finds " + bpm + " BPM", Math.Abs(d - bpm) <= 1.5, "heard " + d.ToString("0.0") + " BPM");
        }
        // trimmed / moved clip plays only its part, at the right place
        int sr = 44100; var l = new float[sr * 4]; for (int i = 0; i < l.Length; i++) l[i] = (float)(i < sr * 2 ? 0.5 : -0.5);   // first 2 s are +0.5, last 2 s are -0.5
        var clip = AudioClip.FromArrays(l, (float[])l.Clone(), "t"); var eng = new StudioEngine(); foreach (var m in eng.MasterFx) m.Bypass = true;
        var t = StudioEngine.NewTrack("T", "other", clip); t.ClipStart = sr * 2; t.ClipFrames = sr; t.Offset = sr * 3; eng.Tracks.Add(t);   // use only seconds 2..3 of the file (the -0.5 part), placed at 3 s
        var o = new float[sr * 6]; long w = 0; eng.RenderStream(0, sr * 6, new RenderOpts { Master = false, Fader = false }, null, (bl, br, off, c2) => { Array.Copy(bl, off, o, w, c2); w += c2; }, null);
        Check("trimmed clip: silent before its start", Math.Abs(o[sr * 3 - 100]) < 1e-6, o[sr * 3 - 100].ToString());
        Check("trimmed clip: plays the chosen part (the -0.5 section)", Math.Abs(o[sr * 3 + 1000] + 0.5) < 1e-4 && Math.Abs(o[sr * 4 - 1000] + 0.5) < 1e-4, o[sr * 3 + 1000].ToString("0.00"));
        Check("trimmed clip: silent after its end", Math.Abs(o[sr * 4 + 1000]) < 1e-6, o[sr * 4 + 1000].ToString());
        Check("song length follows the trim", Math.Abs(t.EndFrame - sr * 4) < 2, t.EndFrame.ToString());
        // save and reopen keeps the trim
        string pf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dawtest.ytmproj"); System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dawtest.wav"), new byte[0]);
        t.Source = "x"; ProjectIO.Save(eng, pf); var e2 = new StudioEngine(); ProjectIO.Load(e2, pf, s => { });
        Check("project keeps the trim and position", e2.Tracks.Count == 1 && e2.Tracks[0].ClipStart == sr * 2 && e2.Tracks[0].ClipFrames == sr && e2.Tracks[0].Offset == sr * 3, e2.Tracks.Count == 1 ? e2.Tracks[0].ClipStart + "/" + e2.Tracks[0].ClipFrames + "/" + e2.Tracks[0].Offset : "none");
        // the vocal's plugins stay on the vocal and the beat's on the beat when a piece sat among the rows as the project was saved (they used to swap onto the wrong track on reopening)
        {
            var e5 = new StudioEngine(); var voc = StudioEngine.NewTrack("Vocal", "vocals", null); var pc = StudioEngine.NewTrack("Vocal piece", "vocals", null); pc.Host = voc; var beat = StudioEngine.NewTrack("Beat", "other", null);
            voc.Fx.Add(FxRegistry.Create("eq")); beat.Fx.Add(FxRegistry.Create("comp")); beat.VolDb = -6; e5.Tracks.Add(voc); e5.Tracks.Add(pc); e5.Tracks.Add(beat);
            Check("(the list is out of row order, as merging two rows used to leave it)", !StudioForm.InRowOrder(e5.Tracks), "");
            string pf5 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dawtest-order.ytmproj"); ProjectIO.Save(e5, pf5); var e6 = new StudioEngine(); ProjectIO.Load(e6, pf5, s => { });
            Track v6 = e6.Tracks.Find(x => x.Name == "Vocal"), b6 = e6.Tracks.Find(x => x.Name == "Beat"), p6 = e6.Tracks.Find(x => x.Name == "Vocal piece");
            Check("reopened: the vocal keeps only its own plugin", v6 != null && v6.Fx.Count == 1 && v6.Fx[0].Type == "eq", v6 == null ? "none" : string.Join(",", v6.Fx.ConvertAll(f => f.Type).ToArray()));
            Check("reopened: the beat keeps only its own plugin and volume", b6 != null && b6.Fx.Count == 1 && b6.Fx[0].Type == "comp" && Math.Abs(b6.VolDb + 6) < 0.01, b6 == null ? "none" : string.Join(",", b6.Fx.ConvertAll(f => f.Type).ToArray()) + " " + b6.VolDb);
            Check("reopened: the piece is still on the vocal row and the list is in row order", p6 != null && p6.Host == v6 && p6.Fx.Count == 0 && StudioForm.InRowOrder(e6.Tracks), "");
            var ro = StudioForm.RowOrder(e5.Tracks); Check("putting the list in order: rows, then pieces", ro.Count == 3 && ro[0] == voc && ro[1] == beat && ro[2] == pc, "");
            try { System.IO.File.Delete(pf5); } catch { }
        }
        // PAUSE stops exactly where you are listening (it used to skip ahead by the audio still waiting in the sound card)
        {
            var ep = new StudioEngine(); foreach (var mf in ep.MasterFx) mf.Bypass = true; ep.Tracks.Add(StudioEngine.NewTrack("Long", "beat", AudioClip.FromArrays(new float[44100 * 20], new float[44100 * 20], "l")));
            string perr = null; try { ep.Play(); } catch (Exception ex) { perr = ex.Message; }
            if (perr != null) Console.WriteLine("SKIP  pause test: no sound card here (" + perr + ")");
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); long firstHeard = -1; while (sw.ElapsedMilliseconds < 4000) { if (firstHeard < 0 && ep.HeardPos > 0) { firstHeard = 1; sw.Restart(); } System.Threading.Thread.Sleep(5); if (firstHeard > 0 && sw.ElapsedMilliseconds >= 2000) break; }
                double heardS = sw.Elapsed.TotalSeconds; long made = ep.Pos; ep.Pause(); double posS = ep.Pos / 44100.0, aheadMs = (posS - heardS) * 1000;
                Check("PAUSE stops where you are listening, not ahead of it (the audio still in the sound card is not skipped)", Math.Abs(aheadMs) < 60 && ep.Pos < made, "stopped at " + posS.ToString("0.000") + " s after hearing " + heardS.ToString("0.000") + " s (" + aheadMs.ToString("+0;-0") + " ms), it had made up to " + (made / 44100.0).ToString("0.000") + " s");
                long at = ep.Pos; ep.Play(); System.Threading.Thread.Sleep(30); long back = ep.HeardPos; ep.Pause(); Check("PLAY after PAUSE carries on from that very spot", back >= at - 2000 && back <= at + 44100 / 10, (back - at) + " frames after the pause point"); ep.Shutdown();
            }
        }
        // LOOP A SECTION: playback goes round from the end of the section to its start, exact to the sample, and never stops
        {
            int len = 44100 * 4; var ramp = new float[len]; for (int i = 0; i < len; i++) ramp[i] = 0.1f + i / (float)len * 0.5f;
            var e7 = new StudioEngine(); foreach (var mf in e7.MasterFx) mf.Bypass = true; var lt = StudioEngine.NewTrack("Ramp", "other", AudioClip.FromArrays(ramp, (float[])ramp.Clone(), "r")); e7.Tracks.Add(lt);
            long A = 44100, B = 44100 * 2; e7.Loop = true; e7.LoopA = A; e7.LoopB = B; Check("(a section loop is on)", e7.LoopSection, "");
            int n7 = 4096; var oL = new float[n7]; var oR = new float[n7]; e7.Pos = B - 1000; e7.Playing = true; e7.RenderRealtime(oL, oR, n7);
            double gain = oL[10] / ramp[B - 1000 + 10]; double want = gain * ramp[A + 5], got = oL[1000 + 5];
            Check("at the end of the section it carries straight on from its start (to the sample)", Math.Abs(got - want) < 1e-3 && Math.Abs(oL[999] - gain * ramp[B - 1]) < 1e-3, "got " + got.ToString("0.0000") + " want " + want.ToString("0.0000"));
            Check("the needle is back inside the section", e7.Pos == A + (n7 - 1000), (e7.Pos - A) + " frames in");
            for (int k = 0; k < 200; k++) e7.RenderRealtime(oL, oR, n7); Check("it keeps going round (it never stops or leaves the section)", e7.Playing && e7.Pos >= A && e7.Pos < B, e7.Pos.ToString());
            e7.Pos = 44100 * 3; e7.RenderRealtime(oL, oR, n7); Check("started after the section, it jumps into the section", e7.Pos >= A && e7.Pos < B, e7.Pos.ToString());
            e7.LoopA = -1; e7.LoopB = -1; e7.Pos = len + 44100 * 2 + 10; e7.RenderRealtime(oL, oR, n7); Check("loop the whole song still goes back to the start at the end", e7.Playing && e7.Pos < 44100, e7.Pos.ToString());
        }
        // every built-in Bass Boost preset raises the low end against the rest, leaves the highs close to where they were, and keeps the level under control
        {
            Func<float[], double, int, double> mag = (x, hz, srr) => { double re = 0, im = 0; int st = srr / 2; for (int i = st; i < x.Length; i++) { re += x[i] * Math.Cos(2 * Math.PI * hz * i / srr); im += x[i] * Math.Sin(2 * Math.PI * hz * i / srr); } return Math.Sqrt(re * re + im * im) / (x.Length - st) * 2; };
            var list = Presets.For("bassboost"); Check("Bass Boost has ready-made presets", list.Count >= 8, list.Count + " presets");
            foreach (var kv in list)
            {
                var pm = new Dictionary<string, double>(); foreach (var part in kv.Value.Split(';')) { var q = part.Split('='); pm[q[0]] = double.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture); }
                double lowHz = pm["freq"] * 0.6; int n = sr * 2; var src = new float[n]; for (int i = 0; i < n; i++) src[i] = (float)(0.35 * Math.Sin(2 * Math.PI * lowHz * i / sr) + 0.12 * Math.Sin(2 * Math.PI * 300.0 * i / sr) + 0.1 * Math.Sin(2 * Math.PI * 2000.0 * i / sr));
                double inLow = mag(src, lowHz, sr), inHigh = mag(src, 2000, sr), inPeak = 0; foreach (var v in src) inPeak = Math.Max(inPeak, Math.Abs(v));
                var fx = Presets.Make("bassboost", kv.Value); var ol = (float[])src.Clone(); var orr = (float[])src.Clone(); fx.Process(ol, orr, n);
                double gLow = 20 * Math.Log10(mag(ol, lowHz, sr) / inLow), gHigh = 20 * Math.Log10(mag(ol, 2000, sr) / inHigh), pk = 0; bool finite = true; foreach (var v in ol) { if (float.IsNaN(v) || float.IsInfinity(v)) finite = false; pk = Math.Max(pk, Math.Abs(v)); }
                double m = pm["mix"] / 100.0, want = 0.5 * 20 * Math.Log10(1 - m + m * Math.Pow(10, pm["gain"] / 20)), balance = gLow - gHigh, pkDb = 20 * Math.Log10(pk / inPeak);
                Check("Bass Boost \"" + kv.Key + "\": low end up " + balance.ToString("0.0") + " dB against the rest, highs " + gHigh.ToString("+0.0;-0.0") + " dB, peak " + pkDb.ToString("+0.0;-0.0") + " dB", finite && balance >= want && gHigh > -6.5 && gHigh < 0.6 && pkDb < 3.0 && pk < 0.99, "needs at least " + want.ToString("0.0") + " dB of lift");
            }
        }
        // Auto Mix works on a full song that has not been split
        var e3 = new StudioEngine(); var song = StudioEngine.NewTrack("Song", "song", BeatClip(100, 20)); e3.Tracks.Add(song);
        var rep = AutoMix.Run(e3, "Natural", false, s => { });
        bool ok = rep.Count > 0 && !rep[0].StartsWith("Nothing to mix") && song.Fx.Count > 0; string txt = rep.Count > 0 ? rep[0].Substring(0, Math.Min(60, rep[0].Length)) : "";
        Check("Auto Mix works before the song is split into vocals and beat", ok, "effects added: " + song.Fx.Count + ", fader " + song.VolDb.ToString("0.0") + " dB; " + txt);
        // mastering analysis moves the EQ the right way
        { int n = 44100 * 12; var bl = new float[n]; var rn = new Random(3); for (int i = 0; i < n; i++) bl[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 120 * i / 44100.0) + 0.05 * (rn.NextDouble() - 0.5)); var sb = AutoMix.Analyze(AudioClip.FromArrays(bl, (float[])bl.Clone(), "bassy")); double[] dv; var gn = StudioForm.MasterToneGains(sb, out dv);
          Check("a bass-heavy mix gets its bass turned down by the master EQ", gn[0] < -1.0, "bass band gain " + gn[0].ToString("0.0") + " dB (bass is " + dv[0].ToString("0.0") + " dB over a balanced mix)");
          var bright = new float[n]; for (int i = 0; i < n; i++) bright[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 6000 * i / 44100.0) + 0.05 * (rn.NextDouble() - 0.5)); var sbr = AutoMix.Analyze(AudioClip.FromArrays(bright, (float[])bright.Clone(), "bright")); var gb = StudioForm.MasterToneGains(sbr, out dv);
          Check("a harsh bright mix gets its highs turned down", gb[3] < -1.0 || gb[4] < -1.0 || gb[2] < -1.0, "upper bands " + gb[2].ToString("0.0") + " / " + gb[3].ToString("0.0") + " / " + gb[4].ToString("0.0") + " dB"); }
        // ---- recording: samples in, a saved track out ----
        {
            var rec = new AudioRecorder(); var pcm = new short[2205]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100.0) * 16000); for (int k = 0; k < 40; k++) rec.Feed(pcm, pcm.Length);
            int fr; var mono = rec.Take(out fr); Check("recorder keeps every sample it is given", fr == 2205 * 40 && rec.Level > 0.4f, fr + " frames, level " + rec.Level.ToString("0.00"));
            var app = (App)Activator.CreateInstance(typeof(App), true); var sf = new StudioForm(app); var tk = sf.AddRecording(mono, fr, 44100L * 10);
            var track = app.StudioEng.Tracks[app.StudioEng.Tracks.Count - 1];
            Check("a recording becomes a vocal track", track.Role == "vocal" && track.Clip != null && track.Clip.Length == fr, "role " + track.Role + ", " + track.Clip.Length + " frames");
            Check("the recording is moved earlier to cancel the sound-card delay", track.Offset < 44100L * 10 && track.Offset > 44100L * 9, "starts at " + (track.Offset / 44100.0).ToString("0.000") + " s (needle was at 10.000 s)");
            Check("the recording is saved as a file", System.IO.File.Exists(track.Source) && new System.IO.FileInfo(track.Source).Length > fr * 3, track.Source);
            string pj = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rectest.ytmproj"); ProjectIO.Save(app.StudioEng, pj); var e4 = new StudioEngine(); ProjectIO.Load(e4, pj, s => { });
            Check("a project with a recording reopens with the recording", e4.Tracks.Count == 1 && e4.Tracks[0].Clip != null && Math.Abs(e4.Tracks[0].Clip.Length - fr) < 4000, e4.Tracks.Count == 1 && e4.Tracks[0].Clip != null ? e4.Tracks[0].Clip.Length + " frames" : "missing");
        }
        // ---- clicking a track selects it (and shows its buttons) but never moves the needle; clicking off the tracks does ----
        {
            var bf2 = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            var app2 = (App)Activator.CreateInstance(typeof(App), true); var l5 = new float[44100 * 30]; for (int i = 0; i < l5.Length; i++) l5[i] = (float)(0.3 * Math.Sin(i * 0.02)); var trk = StudioEngine.NewTrack("Song", "song", AudioClip.FromArrays(l5, (float[])l5.Clone(), "x")); app2.StudioEng.Tracks.Add(trk);
            var sf2 = new StudioForm(app2); sf2.ClientSize = new System.Drawing.Size(1500, 700); sf2.Show(); sf2.Invalidate(); sf2.Update(); System.Windows.Forms.Application.DoEvents();
            Action<string, int, int> mouse2 = (name, x, y) => typeof(StudioForm).GetMethod(name, bf2).Invoke(sf2, new object[] { new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, x, y, 0) });
            Func<object> tools = () => typeof(StudioForm).GetField("toolsTrack", bf2).GetValue(sf2); app2.StudioEng.Seek(44100 * 3);
            Check("before any click no track shows its buttons", tools() == null, "");
            mouse2("OnMouseMove", 600, 250); mouse2("OnMouseDown", 600, 250); mouse2("OnMouseUp", 600, 250); sf2.Invalidate(); sf2.Update();
            Check("clicking a clip does not move the needle", app2.StudioEng.Pos == 44100 * 3, "needle at " + (app2.StudioEng.Pos / 44100.0).ToString("0.00") + " s (was 3.00 s)");
            Check("clicking a clip shows that track's buttons", tools() == trk, tools() == null ? "none" : "the clicked track");
            mouse2("OnMouseMove", 700, 500); mouse2("OnMouseDown", 700, 500); mouse2("OnMouseUp", 700, 500);
            Check("clicking the empty area below the tracks does NOT move the needle (it only moves from the ruler)", app2.StudioEng.Pos == 44100L * 3, "needle at " + (app2.StudioEng.Pos / 44100.0).ToString("0.00") + " s");
            Check("and hides the button row", tools() == null, "");
            mouse2("OnMouseMove", 700, 194); mouse2("OnMouseDown", 700, 194); mouse2("OnMouseUp", 700, 194);
            Check("clicking the ruler at the top moves the needle there", Math.Abs(app2.StudioEng.Pos - 44100L * 3) > 44100, "needle at " + (app2.StudioEng.Pos / 44100.0).ToString("0.00") + " s");
            sf2.Dispose();
        }
        // ---- piano roll: draw, resize, move and delete notes with the mouse ----
        {
            var eg = new StudioEngine(); var tr = StudioEngine.NewTrack("Keys", "inst", null); tr.Instrument = true; var sy = new SynthFx(); sy.Prepare(44100); sy.Owner = tr; tr.Fx.Add(sy); eg.Tracks.Add(tr);
            var form = new PianoRollForm(tr, sy, eg, () => { }); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            var cv = (System.Windows.Forms.Control)typeof(PianoRollForm).GetField("canvas", bf).GetValue(form); cv.Size = new System.Drawing.Size(1000, 560);
            Func<string, object> fld = nm => typeof(RollCanvas).GetField(nm, bf).GetValue(cv); int scy = (int)fld("scrollY"); double ppb = (double)fld("ppb");
            Action<string, int, int, System.Windows.Forms.MouseButtons> mouse = (name, x, y, b) => typeof(RollCanvas).GetMethod(name, bf).Invoke(cv, new object[] { new System.Windows.Forms.MouseEventArgs(b, 1, x, y, 0) });
            int y60 = 24 + (108 - 60) * 16 - scy + 5; int x0 = 70 + (int)(2 * ppb) + 2;   // beat 2, note 60
            mouse("OnMouseDown", x0, y60, System.Windows.Forms.MouseButtons.Left); mouse("OnMouseMove", x0 + (int)(2 * ppb), y60, System.Windows.Forms.MouseButtons.Left); mouse("OnMouseUp", x0 + (int)(2 * ppb), y60, System.Windows.Forms.MouseButtons.Left);
            double bfr = 44100 * 60.0 / Fx.ProjectBpm; var ev = tr.Midi;
            Check("drawing on the piano roll creates a note (note-on + note-off)", ev.Count == 2 && ev[0].D1 == 60 && (ev[0].S & 0xF0) == 0x90 && (ev[1].S & 0xF0) == 0x80, ev.Count + " events" + (ev.Count > 0 ? ", first note " + ev[0].D1 : ""));
            Check("the note starts on the grid and is about two beats long", ev.Count == 2 && Math.Abs(ev[0].Frame - 2 * bfr) < 2 && Math.Abs((ev[1].Frame - ev[0].Frame) - 2 * bfr) < bfr * 0.3, ev.Count == 2 ? "start " + (ev[0].Frame / bfr).ToString("0.00") + " beats, length " + ((ev[1].Frame - ev[0].Frame) / bfr).ToString("0.00") + " beats" : "");
            int y64 = 24 + (108 - 64) * 16 - scy + 5; mouse("OnMouseDown", x0 + 20, y60, System.Windows.Forms.MouseButtons.Left); mouse("OnMouseMove", x0 + 20 + (int)ppb, y64, System.Windows.Forms.MouseButtons.Left); mouse("OnMouseUp", x0 + 20 + (int)ppb, y64, System.Windows.Forms.MouseButtons.Left);
            Check("dragging a note moves it up in pitch and later in time", ev.Count == 2 && ev[0].D1 == 64 && ev[0].Frame > 2.5 * bfr, ev.Count == 2 ? "note " + ev[0].D1 + " at beat " + (ev[0].Frame / bfr).ToString("0.00") : "");
            mouse("OnMouseDown", x0 + (int)ppb + 20, y64, System.Windows.Forms.MouseButtons.Right); mouse("OnMouseUp", x0 + (int)ppb + 20, y64, System.Windows.Forms.MouseButtons.Right);
            Check("right-click deletes a note", ev.Count == 0, ev.Count + " events left");
            form.Dispose();
        }
        Console.WriteLine(fails == 0 ? "=== DAW checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
