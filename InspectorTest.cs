using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// The bottom inspector (channel strip, plugins, buses, clip, MIDI), floating plugin windows, sends / trim / phase in the real engine, saving and
// reopening, the right-hand panel one button per row, and the smaller menus.   csc /main:IN ... InspectorTest.cs
static class IN
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
    static void Click(float x, float y) { Ev("OnMouseMove", MouseButtons.None, 0, (int)x, (int)y); Ev("OnMouseDown", MouseButtons.Left, 1, (int)x, (int)y); Ev("OnMouseUp", MouseButtons.Left, 1, (int)x, (int)y); Paint(); }
    static void Shot(string name) { Paint(); var bm = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bm, new Rectangle(0, 0, sf.Width, sf.Height)); bm.Save("C:/Users/thoma/nfout/" + name + ".png", System.Drawing.Imaging.ImageFormat.Png); }
    const int SR = 44100;
    static AudioClip Sine(int sec, double hz, float amp) { int n = SR * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / SR)); return AudioClip.FromArrays(x, (float[])x.Clone(), "sine"); }
    static float[] Render(long frames, List<Track> only = null) { float[] l, r; eng.Render(0, frames, new RenderOpts { Only = only }, null, out l, out r); return l; }
    static double Rms(float[] x, int a = 4410) { double e = 0; int n = 0; for (int i = a; i < x.Length; i++) { e += x[i] * x[i]; n++; } return Math.Sqrt(e / Math.Max(1, n)); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy;
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_inspector"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
        var vox = StudioEngine.NewTrack("Vocal", "vocal", Sine(4, 220, 0.3f)); eng.Tracks.Add(vox);
        var beat = StudioEngine.NewTrack("Beat", "beat", Sine(4, 110, 0.3f)); eng.Tracks.Add(beat);
        var keys = StudioEngine.NewTrack("Keys", "inst", null); keys.Instrument = true; var bl = FxRegistry.Create("synth"); bl.Prepare(SR); ((InstrumentFx)bl).Owner = keys; keys.Fx.Add(bl);
        for (int i = 0; i < 8; i++) { keys.Midi.Add(new MidiEv { Frame = i * 11025 + 300, S = 0x90, D1 = (byte)(60 + i), D2 = 90 }); keys.Midi.Add(new MidiEv { Frame = i * 11025 + 9000, S = 0x80, D1 = (byte)(60 + i), D2 = 0 }); }
        eng.Tracks.Add(keys); var bus = StudioEngine.NewTrack("Reverb Bus", "bus", null); bus.IsBus = true; eng.Tracks.Add(bus);
        sf = new StudioForm(app); sf.ClientSize = new Size(1600, 900); sf.Show(); Pump(400); Paint(); SetF("selTrack", 0);

        // ---- the inspector: opens under the arrangement, which gets the rest of the height; resizes; hides ----
        sf.OpenInspector(0); Paint(); float mixH = sf.MixH; var tl = (RectangleF)M("TimelineRect");
        Check("the inspector opens under the arrangement and the arrangement ends where it starts", sf.InsShown && mixH < sf.ClientSize.Height - 160 && tl.Bottom <= mixH + 1, "arrangement to " + tl.Bottom + ", inspector from " + mixH);
        Check("opening it tucks the right-hand plugin panel away (one place for plugins)", Prefs.PanelHidden, "");
        Check("the inspector shows the picked track", sf.InsTarget == vox, sf.InsTarget == null ? "none" : sf.InsTarget.Name);
        Shot("ins-channel");
        { float y0 = sf.MixH; Ev("OnMouseMove", MouseButtons.None, 0, 400, (int)y0); Ev("OnMouseDown", MouseButtons.Left, 1, 400, (int)y0); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, 400, (int)y0 - i * 20); Ev("OnMouseUp", MouseButtons.Left, 1, 400, (int)y0 - 120); Paint();
          Check("dragging its top edge makes it taller (and the arrangement shorter), within limits", sf.MixH < y0 - 60 && sf.InsH <= sf.ClientSize.Height * 0.6f + 1, y0 + " -> " + sf.MixH); }
        SetF("selTrack", 1); Paint(); Check("picking another track updates the inspector", sf.InsTarget == beat, sf.InsTarget.Name);
        SetF("selTrack", 0); Paint();

        // ---- the channel strip changes the real track (one source of truth) ----
        vox.VolDb = 0; var rendered0 = Render(SR * 2, new List<Track> { vox }); vox.TrimDb = 6f; var rendered6 = Render(SR * 2, new List<Track> { vox }); vox.TrimDb = 0;
        Check("TRIM changes the level going into the plugins (+6 dB doubles it)", Math.Abs(Rms(rendered6) / Rms(rendered0) - 2.0) < 0.05, (Rms(rendered6) / Rms(rendered0)).ToString("0.00") + "x");
        var copy = StudioEngine.NewTrack("Vocal copy", "vocal", vox.Clip); eng.Tracks.Add(copy); copy.Invert = true; var both = Render(SR * 2, new List<Track> { vox, copy }); copy.Invert = false; var both2 = Render(SR * 2, new List<Track> { vox, copy });
        Check("PHASE flips the polarity: a track and its inverted copy cancel out", Rms(both) < 1e-4 && Rms(both2) > 0.2, Rms(both).ToString("0.00000") + " vs " + Rms(both2).ToString("0.000"));
        eng.Tracks.Remove(copy);

        // ---- sends: real audio to the bus, pre and post fader ----
        { vox.Out = null; vox.Sends = new[] { new Send { Bus = bus, LevelDb = 0f, Pre = false } }; var gainOnBus = FxRegistry.Create("gain"); var only = new List<Track> { vox, bus };
          bus.Mute = true; var dry = Render(SR * 2, only); bus.Mute = false; var wet = Render(SR * 2, only); double addRatio = Rms(wet) / Rms(dry);
          Check("a send carries the track to the bus: with the bus on, the track is heard twice as loud (dry + send at 0 dB)", Math.Abs(addRatio - 2.0) < 0.05, addRatio.ToString("0.00") + "x");
          vox.VolDb = -20; var post = Render(SR * 2, only); vox.Sends[0].Pre = true; var pre = Render(SR * 2, only); vox.VolDb = 0;
          double fader = Math.Pow(10, -20 / 20.0); Check("POST follows the track's fader, PRE does not", Math.Abs(Rms(post) / Rms(dry) - 2 * fader) < 0.02 && Math.Abs(Rms(pre) / Rms(dry) - (fader + 1)) < 0.03, "post " + (Rms(post) / Rms(dry)).ToString("0.000") + ", pre " + (Rms(pre) / Rms(dry)).ToString("0.000"));
          vox.Sends[0].Pre = false; vox.Sends[0].LevelDb = -6;
          M("AddSend", bus, bus); Check("a bus can never send (no feedback loops)", bus.Sends.Length == 0, ""); M("AddSend", vox, bus); Check("one send per bus per track", vox.Sends.Length == 1, vox.Sends.Length + " sends"); }

        // ---- plugins: add from the inspector opens a floating window; remove, bypass, reorder, copy ----
        sf.OpenInspector(1); Paint(); Shot("ins-plugins-empty");
        SetF("insAddFor", vox); SetF("browserTrack", 0); SetF("browserMaster", false); M("AddFx", "reverb"); var rev = vox.Fx.Last(); Paint();
        Check("+ ADD EFFECT inserts the real effect into the track's chain and opens its own window", rev.Type == "reverb" && sf.FxWindowOf(rev) != null && sf.fxWins.Count == 1, vox.Fx.Count + " plugins, " + sf.fxWins.Count + " windows");
        SetF("insAddFor", vox); SetF("browserTrack", 0); M("AddFx", "eq"); var eq = vox.Fx.Last(); SetF("insAddFor", beat); SetF("browserTrack", 1); M("AddFx", "comp"); var comp = beat.Fx.Last(); Paint();
        Check("several plugin windows stay open at once (EQ, reverb, compressor on two tracks)", sf.fxWins.Count == 3 && sf.FxWindowOf(eq) != null && sf.FxWindowOf(comp) != null, sf.fxWins.Count + " windows");
        Shot("fx-windows");
        // live processing: the reverb's mix changes the sound; bypass passes the dry sound
        { var only = new List<Track> { vox }; vox.Sends = new Send[0]; eq.Bypass = true; rev.Set("mix", 0); var r0 = Render(SR * 3, only); rev.Set("mix", 80); var r80 = Render(SR * 3, only); rev.Bypass = true; var rb = Render(SR * 3, only); rev.Bypass = false;
          double diffMix = 0, diffByp = 0; for (int i = 0; i < r0.Length; i++) { diffMix += Math.Abs(r80[i] - r0[i]); diffByp += Math.Abs(rb[i] - r0[i]); }
          Check("turning the reverb's mix up changes the sound; bypass gives back the dry sound (real processing)", diffMix / r0.Length > 0.01 && diffByp / r0.Length < 1e-4, (diffMix / r0.Length).ToString("0.0000") + " / " + (diffByp / r0.Length).ToString("0.000000")); eq.Bypass = false; }
        // ---- the plugin visualisers: the engine measures what each plugin does while it plays; the pictures follow it ----
        { float[] vbL = new float[1024], vbR = new float[1024]; rev.Set("mix", 80); eq.Bypass = true; eng.Pos = SR / 2; eng.Playing = true; for (int i = 0; i < 40; i++) eng.RenderRealtime(vbL, vbR, 1024); float actOn = rev.Act;
          rev.Set("mix", 0); for (int i = 0; i < 60; i++) eng.RenderRealtime(vbL, vbR, 1024); float actOff = rev.Act;
          Check("while it plays, the engine measures how much a plugin changes the sound (reverb at 80% mix: a lot; at 0%: nothing)", actOn > 0.1f && actOff < 0.03f, "80%: " + actOn.ToString("0.000") + ", 0%: " + actOff.ToString("0.000"));
          Check("each plugin gets the picture that fits it: ripples for the reverb, a pumping meter for the compressor, a curve for the EQ", StudioForm.VizName(rev) == "Ripples" && StudioForm.VizName(comp) == "Pump" && StudioForm.VizName(eq) == "Curve", StudioForm.VizName(rev) + ", " + StudioForm.VizName(comp) + ", " + StudioForm.VizName(eq));
          rev.Set("mix", 80); for (int i = 0; i < 40; i++) eng.RenderRealtime(vbL, vbR, 1024);
          Func<Fx, int> lit = fx => { var bm = new Bitmap(220, 60); using (var g0 = Graphics.FromImage(bm)) { g0.Clear(Color.Black); for (int k = 0; k < 12; k++) sf.PaintFxViz(g0, new RectangleF(0, 0, 220, 60), fx, true); } int n = 0; for (int yy = 0; yy < 60; yy += 2) for (int xx = 0; xx < 220; xx += 2) if (bm.GetPixel(xx, yy).GetBrightness() > 0.25f) n++; bm.Dispose(); return n; };
          int busy = lit(rev); rev.Bypass = true; for (int k = 0; k < 80; k++) sf.VizLevel(rev); int still = lit(rev); rev.Bypass = false;
          sf.OpenInspector(1); Paint(); Shot("fx-viz"); eng.Playing = false; eng.Pos = 0; eq.Bypass = false; rev.Set("mix", 33);
          Check("the picture grows with the plugin's work and settles when it is bypassed", busy > still * 1.5 && busy > 40, "working " + busy + " lit, bypassed " + still); }
        // the windows stay open while the studio is clicked; each stays bound to its own plugin
        var revWin = sf.FxWindowOf(rev); var tl2 = (RectangleF)M("TimelineRect"); Click(tl2.X + tl2.Width - 60, tl2.Y + 20); SetF("selTrack", 1); Paint(); Click(60, sf.MixH + 18);   /* the inspector (the mixer): its CHANNEL tab */
        Check("clicking the arrangement, another track and the mixer does not close the windows", F<int>("insTab") == 0 && sf.fxWins.Count == 3 && sf.FxWindowOf(rev) == revWin, sf.fxWins.Count + " windows");
        float m0 = comp.Get("thr"); revWin.Fx.Set("mix", 33); Check("with Track B picked, Track A's reverb window still edits Track A's reverb (B is untouched)", Math.Abs(rev.Get("mix") - 33) < 0.01 && Math.Abs(comp.Get("thr") - m0) < 0.001 && revWin.Track == vox, "");
        // drag a window by its title bar; clicking inside brings it forward
        { var w = sf.FxWindowOf(eq); sf.FxWinMouseDown(new Point((int)w.R.X + 10, (int)w.R.Y + 10)); Paint(); bool front = sf.fxWins.Max(q => q.Seq) == w.Seq; float x0 = w.R.X, y0 = w.R.Y;
          int gx = (int)(w.R.X + 60), gy = (int)(w.R.Y + 12); Ev("OnMouseMove", MouseButtons.None, 0, gx, gy); Ev("OnMouseDown", MouseButtons.Left, 1, gx, gy); for (int i = 1; i <= 5; i++) Ev("OnMouseMove", MouseButtons.Left, 0, gx + i * 30, gy + i * 12); Ev("OnMouseUp", MouseButtons.Left, 1, gx + 150, gy + 60); Paint();
          Check("a click inside a window brings it to the front, and its title bar drags it", front && Math.Abs(w.R.X - (x0 + 150)) < 3 && Math.Abs(w.R.Y - (y0 + 60)) < 3, "moved " + (w.R.X - x0) + ", " + (w.R.Y - y0));
          var x1 = w.R.X; var knob = Regs().LastOrDefault(r => w.R.Contains(r.R.Location) && r.R.Y > w.R.Y + 80 && r.OnDrag != null && r.R.Width >= 30 && r.R.Width < w.R.Width - 30 && r.R.Bottom < sf.ClientSize.Height && r.R.Bottom < w.R.Bottom - 14 && r.R.Height >= 20); if (knob != null) { int kx = (int)(knob.R.X + knob.R.Width / 2), ky = (int)(knob.R.Y + knob.R.Height / 2); string before = eq.Serialize(); Ev("OnMouseMove", MouseButtons.None, 0, kx, ky); Ev("OnMouseDown", MouseButtons.Left, 1, kx, ky); for (int i = 1; i <= 4; i++) Ev("OnMouseMove", MouseButtons.Left, 0, kx + i * 10, ky - i * 8); Ev("OnMouseUp", MouseButtons.Left, 1, kx + 40, ky - 32); Paint();
            Check("dragging a control in the window changes the plugin and does not move the window", Math.Abs(w.R.X - x1) < 0.5 && eq.Serialize() != before, "window moved " + (w.R.X - x1) + ", control " + knob.R + ", changed " + (eq.Serialize() != before)); } else Check("dragging a control in the window changes the plugin and does not move the window", false, "no control found"); }
        // closing a window keeps the plugin; removing the plugin closes its window
        sf.CloseFxWindow(eq); Paint(); Check("closing a window keeps the plugin in the chain (and its settings)", vox.Fx.Contains(eq) && sf.FxWindowOf(eq) == null && Math.Abs(rev.Get("mix") - 33) < 0.01, "");
        sf.OpenFxWindow(vox, eq); Check("reopening it shows the same plugin instance", sf.FxWindowOf(eq).Fx == eq, "");
        int ri = vox.Fx.IndexOf(eq); sf.RemoveFxAt(vox, ri); Paint(); Check("removing a plugin closes its window", !vox.Fx.Contains(eq) && sf.FxWindowOf(eq) == null, "");
        // chain edits
        sf.DuplicateFx(vox, vox.Fx.IndexOf(rev)); var dup = vox.Fx[vox.Fx.IndexOf(rev) + 1]; Check("COPY adds a second reverb with the same settings right after it", dup.Type == "reverb" && dup != rev && dup.Serialize() == rev.Serialize(), "");
        int i0 = vox.Fx.IndexOf(dup); M("MoveFx", vox, i0, -1); Check("moving a plugin changes the processing order", vox.Fx.IndexOf(dup) == i0 - 1, "");
        sf.OpenInspector(1); Paint(); Shot("ins-plugins");
        // ---- ALL PLUGINS is a desk: a strip per track and bus (no master: it has its own page); a click on a plugin slot opens its small window; the right panel has no plugin list while it is open ----
        { SetF("insScrollX", 0f); Paint(); float ny = sf.MixH + 36 + 8; var names = Regs().Where(r => r.Button && Math.Abs(r.R.Y - ny) < 0.6f && Math.Abs(r.R.Width - 106) < 0.6f && Math.Abs(r.R.Height - 20) < 0.6f).Select(r => r.R.X).Distinct().ToList();
          Check("ALL PLUGINS shows one strip for every track and bus, side by side, and no master strip", names.Count == eng.Tracks.Count && !File.ReadAllText("Studio.Inspector.cs").Contains("PaintMasterStrip"), names.Count + " strips, " + eng.Tracks.Count + " tracks");
          sf.fxWins.Clear(); Paint(); int vi = eng.Tracks.Where(q => !q.IsBus).ToList().IndexOf(vox), ri2 = vox.Fx.IndexOf(rev); float sx = names.OrderBy(v => v).ElementAt(vi);
          Click(sx + 50, ny + 26 + ri2 * 27 + 12); Paint(); var rw = sf.FxWindowOf(rev);
          Check("clicking a plugin slot on a strip opens that plugin in its own small window", rw != null && rw.Fx == rev && rw.R.Width <= 431, rw == null ? "no window" : rw.R.Width + " wide");
          Click(sx + 8, ny + 26 + ri2 * 27 + 12); Paint(); bool byp = rev.Bypass; Click(sx + 8, ny + 26 + ri2 * 27 + 12); Paint();
          Check("the dot on a slot bypasses the plugin and turns it back on", byp && !rev.Bypass, "");
          Prefs.PanelHidden = false; SetF("panelAt", DateTime.MinValue); SetF("soundsOpen", false); Paint(); Paint(); float px2 = sf.ClientSize.Width - 380;
          Check("with the mixer open the right panel lists no plugins (they are all in the mixer)", !Regs().Any(r => r.R.X >= px2 && r.OnDouble != null && r.R.Height == 28), "");
          Shot("ins-console-panel"); Prefs.PanelHidden = true; SetF("panelAt", DateTime.MinValue); Paint(); Paint(); Shot("ins-console"); }

        // ---- buses tab: make, route, delete safely ----
        sf.OpenInspector(2); SetF("insBus", bus); Paint(); Check("the BUSES tab shows the bus and its channel", sf.InsTarget == bus, sf.InsTarget.Name); Shot("ins-buses");
        beat.Out = bus; vox.Sends = new[] { new Send { Bus = bus, LevelDb = -6 } }; sf.DeleteBusSafely(bus); Paint();
        Check("deleting a bus sends its tracks back to the master and removes the sends to it", !eng.Tracks.Contains(bus) && beat.Out == null && vox.Sends.Length == 0, "");
        M("DoUndo"); Check("undo brings the bus, its routing and the send back", eng.Tracks.Contains(bus) && beat.Out == bus && vox.Sends.Length == 1, eng.Tracks.Contains(bus) + ", out " + (beat.Out == bus) + ", sends " + vox.Sends.Length);

        // ---- clip tab ----
        SetF("toolsTrack", beat); SetF("selTrack", 1); sf.OpenInspector(3); Paint(); Check("CLIP shows the picked audio clip", sf.InsClip == beat, ""); Shot("ins-clip");
        float s0 = beat.Clip.L[100], sEnd = beat.Clip.L[beat.Clip.Length - 101]; var oldClip = beat.Clip; sf.ReverseClip(beat); Check("REVERSE plays the clip backwards", Math.Abs(beat.Clip.L[beat.Clip.Length - 101] - s0) < 1e-6 && Math.Abs(beat.Clip.L[100] - sEnd) < 1e-6, ""); M("DoUndo"); Check("and undo puts the original back", beat.Clip == oldClip, "");

        // ---- MIDI tab ----
        SetF("selTrack", eng.Tracks.IndexOf(keys)); SetF("toolsTrack", null); sf.OpenInspector(3); Paint(); Shot("ins-midi");
        int n0 = keys.Midi[0].D1; sf.MidiTranspose(keys, 12); Check("TRANSPOSE moves every note", keys.Midi[0].D1 == n0 + 12, keys.Midi[0].D1 + ""); M("DoUndo"); Check("and undo puts the notes back (MIDI is in the undo history now)", keys.Midi[0].D1 == n0, keys.Midi[0].D1 + "");
        sf.MidiQuantise(keys, 0.25); double grid = 0.25 * 60.0 / Fx.ProjectBpm * SR; bool onGrid = keys.Midi.Where(m => (m.S & 0xF0) == 0x90).All(m => Math.Abs(m.Frame / grid - Math.Round(m.Frame / grid)) < 1e-3); Check("QUANTISE puts every note start on the grid", onGrid, "");

        // ---- a MIDI track is only a MIDI track: no Bass Lab is opened; a MIDI keyboard records straight onto it ----
        { int nTracks = eng.Tracks.Count; var mt = sf.AddMidiTrack(); Paint();
          Check("a new MIDI track has no instrument on it (Bass Lab is optional, nothing opens)", mt != null && mt.Instrument && mt.Fx.Count == 0 && eng.Tracks.Count == nTracks + 1 && !Application.OpenForms.Cast<Form>().Any(fm => fm.Visible && fm.Text.Contains("Bass Lab")), mt == null ? "none" : mt.Fx.Count + " plugins");
          var keep = MidiInput.Target; MidiInput.Target = null; eng.Playing = true; MidiInput.Handle(0x90 | (60 << 8) | (100 << 16)); MidiInput.Handle(0x80 | (60 << 8)); eng.Playing = false; MidiInput.Target = keep;
          Check("notes from a MIDI keyboard are recorded onto the picked MIDI track with no instrument", mt.Midi.Count == 2 && mt.Midi[0].D1 == 60 && mt.Midi[0].D2 == 100, mt.Midi.Count + " events");
          Check("playing the keyboard on it is heard straight away (the built-in keys play it)", mt.DefaultKeys != null && !mt.HasInstrumentFx, mt.DefaultKeys == null ? "no sound" : "built-in keys");
          lock (mt.Midi) { mt.Midi.Clear(); mt.Midi.Add(new MidiEv { Frame = SR / 10, S = 0x90, D1 = 69, D2 = 100 }); mt.Midi.Add(new MidiEv { Frame = SR, S = 0x80, D1 = 69, D2 = 0 }); }
          var kr = Render(SR * 2, new List<Track> { mt }); double kp = 0; foreach (var v in kr) kp = Math.Max(kp, Math.Abs(v)); int zc = 0; for (int i = SR / 5 + 1; i < SR / 5 + SR / 2; i++) if (kr[i - 1] < 0 && kr[i] >= 0) zc++; double hz = zc * 2.0;
          Check("the recorded notes play back on the MIDI track without Bass Lab: a clean note at the right pitch (A = 440 Hz), no clipping", Rms(kr) > 0.01 && kp < 0.99 && Math.Abs(hz - 440) < 25, "rms " + Rms(kr).ToString("0.000") + ", peak " + kp.ToString("0.00") + ", " + hz.ToString("0") + " Hz");
          var bl2 = FxRegistry.Create("synth") as InstrumentFx; bl2.Prepare(SR); bl2.Owner = mt; lock (eng.Sync) mt.Fx.Add(bl2); Check("with Bass Lab added, Bass Lab plays the notes instead of the built-in keys", mt.HasInstrumentFx, ""); lock (eng.Sync) mt.Fx.Remove(bl2); bl2.CloseWindow();
          SetF("recOn", false); M("DeleteTrackQuiet", mt); Paint(); }
        // ---- saving and reopening keeps the sends, trim, phase and plugin settings; old projects open with defaults ----
        { vox.TrimDb = 3.5f; vox.Invert = true; vox.Sends = new[] { new Send { Bus = bus, LevelDb = -9.5f, Pre = true } }; rev.Set("mix", 41); string path = Path.Combine(tmp, "t.ytmproj"); ProjectIO.Save(eng, path);
          var e2 = new StudioEngine(); ProjectIO.Load(e2, path, s => { }); var v2 = e2.Tracks.First(t => t.Name == "Vocal"); var b2 = e2.Tracks.First(t => t.IsBus); var rv2 = v2.Fx[vox.Fx.IndexOf(rev)];
          Check("save and reopen: trim, phase, the send (bus, level, pre) and the plugin settings come back", Math.Abs(v2.TrimDb - 3.5f) < 0.01 && v2.Invert && v2.Sends.Length == 1 && v2.Sends[0].Bus == b2 && Math.Abs(v2.Sends[0].LevelDb + 9.5f) < 0.01 && v2.Sends[0].Pre && Math.Abs(rv2.Get("mix") - 41) < 0.01, "trim " + v2.TrimDb + " inv " + v2.Invert + " sends " + v2.Sends.Length + (v2.Sends.Length > 0 ? " bus " + (v2.Sends[0].Bus == b2) + " lvl " + v2.Sends[0].LevelDb + " pre " + v2.Sends[0].Pre : "") + " mix " + rv2.Get("mix"));
          string old = File.ReadAllText(path); old = string.Join("\n", old.Split('\n').Where(l => !l.StartsWith("strip|") && !l.StartsWith("send|"))); string p2 = Path.Combine(tmp, "old.ytmproj"); File.WriteAllText(p2, old); var e3 = new StudioEngine(); var warn = ProjectIO.Load(e3, p2, s => { });
          var v3 = e3.Tracks.First(t => t.Name == "Vocal"); Check("a project from before this version opens with no trim, normal phase and no sends", v3.TrimDb == 0 && !v3.Invert && v3.Sends.Length == 0 && e3.Tracks.Count == eng.Tracks.Count, warn.Count + " warnings"); }

        // ---- plugin windows and VST windows are always in the Purphex look, whatever the theme; the rest of the studio keeps the theme ----
        { Themes.ApplyByName("Ocean"); sf.OpenFxWindow(vox, rev); Paint(); Paint(); var w = sf.FxWindowOf(rev); var bm = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bm, new Rectangle(0, 0, sf.Width, sf.Height));
          int purple = 0, n = 0; for (int yy = (int)w.R.Bottom - 60; yy < (int)w.R.Bottom - 20; yy += 3) for (int xx = (int)w.R.X + 14; xx < (int)w.R.X + 60; xx += 3) { var c = bm.GetPixel(xx, yy + 30); n++; if (c.GetSaturation() > 0.15f && c.GetHue() > 245 && c.GetHue() < 320) purple++; }
          var themed = TM.C(255, 150, 70, 230); bool rawP = themed.R == 150 && themed.G == 70 && themed.B == 230;
          var pxViolet = (Color)typeof(PxWindow).GetField("Violet", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
          Check("with another theme on, the plugin window is still drawn in the Purphex colours (its background is Purphex purple) and the studio around it keeps the theme", purple > n / 2 && !rawP && !TM.Brand, purple + "/" + n + " purple, studio colour " + themed);
          Check("the Purphex VST windows keep the brand colours too", pxViolet.ToArgb() == Themes.All[0].Violet.ToArgb(), pxViolet.ToString());
          bm.Save("C:/Users/thoma/nfout/brand-ocean.png"); bm.Dispose(); Themes.ApplyByName("Purphex"); Paint(); }
        // ---- the right panel: one button per row; the menus are smaller ----
        sf.CloseInspector(); Prefs.PanelHidden = false; SetF("panelAt", DateTime.MinValue); SetF("soundsOpen", false); SetF("selTrack", 0); Paint(); Paint();
        float px = sf.ClientSize.Width - 380; var col = Regs().Where(r => Math.Abs(r.R.X - (px + 10)) < 1.5f && Math.Abs(r.R.Height - 26) < 0.5f && r.R.Width > 300).Select(r => r.R).Distinct().OrderBy(r => r.Y).ToList();
        bool oneEach = col.Count >= 2;   /* (PRESETS moved to the mixer strips) */ for (int i = 1; i < col.Count; i++) if (Math.Abs(col[i].Y - col[i - 1].Y - 30) > 0.5f || Math.Abs(col[i].Width - col[0].Width) > 0.5f) oneEach = false;
        Check("the right panel's tools are one per row, all the same width and height", oneEach, col.Count + " rows");
        Check("there is no ALL BUSES button on the right panel any more (buses are in the inspector)", !File.ReadAllText("Studio.UI.cs").Contains("\"ALL BUSES\", 2, () => OpenAllBuses()"), "");
        Shot("right-panel");
        { var mm = Win.NewMenu(); mm.Items.Add("One"); Check("menus are 25% smaller (10 pt) and each option has its own neon box", Math.Abs(mm.Font.SizeInPoints - 10f) < 0.3f, mm.Font.SizeInPoints + " pt"); mm.Dispose(); }
        // ---- the look: your profile colour and its finish reach the studio (buttons, menus, selections, plugin windows), survive a theme change, and go back cleanly ----
        { Func<int, Bitmap> btnShot = st => { var bm = new Bitmap(160, 40); using (var g0 = Graphics.FromImage(bm)) { g0.Clear(Color.FromArgb(255, 30, 30, 30)); for (int yy = 0; yy < 40; yy += 8) for (int xx = 0; xx < 160; xx += 8) if (((xx + yy) / 8) % 2 == 0) g0.FillRectangle(Brushes.White, xx, yy, 8, 8); typeof(StudioForm).GetMethod("BtnLook", BF).Invoke(sf, new object[] { g0, new RectangleF(4, 4, 152, 32), "ON", st, 0f, 12f }); } return bm; };
          Func<Bitmap, double> hueMid = bm => { double hs = 0; int n = 0; for (int xx = 30; xx < 130; xx += 3) { var c = bm.GetPixel(xx, 24); if (c.GetSaturation() > 0.2) { hs += c.GetHue(); n++; } } return n > 0 ? hs / n : -1; };
          Func<Bitmap, double> bands = bm => { double s = 0, ss = 0; int n = 0; for (int yy = 8; yy < 32; yy++) { var c = bm.GetPixel(20, yy); double l = c.GetBrightness(); s += l; ss += l * l; n++; } double m = s / n; return Math.Sqrt(Math.Max(0, ss / n - m * m)); };
          var green = Color.FromArgb(255, 30, 200, 120); StudioForm.ApplyProfileTokens(true, green, MixColours.Plain, Color.Empty); var plainBtn = btnShot(4); double h1 = hueMid(plainBtn);
          Check("your profile colour becomes the accent: a selected button, the menus' chosen item and the focus edge use it", UiTokens.Custom && Win.NeonHot == green && Math.Abs(h1 - green.GetHue()) < 25, "button hue " + h1.ToString("0") + " vs " + green.GetHue().ToString("0"));
          StudioForm.ApplyProfileTokens(true, green, MixColours.Metal, Color.Empty); var metalBtn = btnShot(4); StudioForm.ApplyProfileTokens(true, green, MixColours.Chrome, Color.Empty); var chromeBtn = btnShot(4);
          Check("METALLIC and CHROME finishes are drawn on selected buttons (bands of light and dark, not a flat colour)", bands(metalBtn) > bands(plainBtn) * 1.3 && bands(chromeBtn) > bands(plainBtn) * 1.3, "plain " + bands(plainBtn).ToString("0.000") + ", metal " + bands(metalBtn).ToString("0.000") + ", chrome " + bands(chromeBtn).ToString("0.000"));
          StudioForm.ApplyProfileTokens(true, green, MixColours.Holo, Color.Empty); var holoBtn = btnShot(4); double hueSpread = 0; { double mn = 360, mx = 0; for (int xx = 20; xx < 140; xx += 4) { var c = holoBtn.GetPixel(xx, 18); if (c.GetSaturation() > 0.15) { mn = Math.Min(mn, c.GetHue()); mx = Math.Max(mx, c.GetHue()); } } hueSpread = mx - mn; }
          Check("HOLOGRAPHIC draws a colour-shifting sheen across the button (several hues), still readable", hueSpread > 25, hueSpread.ToString("0") + " degrees of hue");
          StudioForm.ApplyProfileTokens(true, green, MixColours.Translucent, Color.Empty); var glassBtn = btnShot(4); int seeThrough = 0; for (int xx = 30; xx < 130; xx++) { var a1 = glassBtn.GetPixel(xx, 30); var a2 = glassBtn.GetPixel(xx, 22); if (Math.Abs(a1.GetBrightness() - a2.GetBrightness()) > 0.12) seeThrough++; }
          Check("TRANSLUCENT makes the selected button see-through (the pattern behind shows)", seeThrough > 10 && UiTokens.Glass, seeThrough + " columns show the pattern");
          StudioForm.ApplyProfileTokens(true, green, MixColours.Metal, Color.Empty); int v0 = UiTokens.Version; StudioForm.ApplyTheme(Themes.Current); Check("changing the theme keeps your accent and its finish", UiTokens.Custom && UiTokens.Finish == MixColours.Metal && Win.NeonHot == green, "");
          var pink = Color.FromArgb(255, 240, 60, 170); StudioForm.ApplyProfileTokens(true, pink, MixColours.Metal, Color.Empty); var pinkBtn = btnShot(4); Check("picking a new profile colour keeps the finish and repaints with the new colour (no stale cached buttons)", Math.Abs(hueMid(pinkBtn) - pink.GetHue()) < 25 && UiTokens.Version > v0, "hue " + hueMid(pinkBtn).ToString("0"));
          sf.OpenInspector(1); sf.OpenFxWindow(vox, rev); Paint(); Shot("theme-metal");
          StudioForm.ApplyProfileTokens(true, Color.FromArgb(255, 80, 160, 255), MixColours.Holo, Color.Empty); Paint(); Shot("theme-holo");
          // the colour mixer drives it (the real path): set a colour with a finish and switch the profile to follow it
          M("MixSet", 0, Color.FromArgb(255, 200, 160, 40), MixColours.Chrome, "Gold Chrome", Color.Empty); M("MixSetOn", 1, false); M("MixSetFollow", true);
          Check("the colour mixer drives it: a chrome gold profile colour makes the accent gold with a chrome finish", UiTokens.Custom && UiTokens.Finish == MixColours.Chrome && Math.Abs(UiTokens.Accent.GetHue() - 43) < 20, "finish " + UiTokens.Finish + ", hue " + UiTokens.Accent.GetHue().ToString("0"));
          M("MixSetFollow", false); Check("switching the profile back to the theme colour puts the theme's own accent back", !UiTokens.Custom && Win.NeonHot == Themes.Current.Cyan, ""); }
        // the transport and the editing row are centred
        sf.OpenInspector(0); Paint(); Shot("ins-final");
        eng.Shutdown(); try { Directory.Delete(tmp, true); } catch { } Console.WriteLine(fails == 0 ? "=== inspector checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
