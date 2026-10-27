using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Pictures of the preset windows (vocal presets, bus presets, the mastering presets) in two themes, to check that they keep the Purphex look.
//   csc /main:PresetShots ... PresetShots.cs     run: PresetShots.exe <output folder>
static class PresetShots
{
    static StudioForm sf; static string outDir; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Call(string n, params object[] a) { typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string name)
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents();
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        string p = Path.Combine(outDir, name + ".png"); bmp.Save(p, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + p);
    }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "clip"); }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "presetshots"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_pshot_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Win.GalaxyInTests = true; Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(30)));
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 860); sf.Show(); Pump(500); SetF("selTrack", 0);
        string only = args.Length > 1 ? args[1] : "";
        for (int pass = 0; pass < 2; pass++)
        {
            string tag = pass == 0 ? "default" : "theme";
            if (pass == 1) { int pick = -1; for (int i = 0; i < Themes.All.Count; i++) if (i != Themes.Index && Themes.All[i].Name.IndexOf("Purphex", StringComparison.OrdinalIgnoreCase) < 0) { pick = i; break; } if (pick >= 0) { Themes.Apply(pick); Console.WriteLine("theme " + Themes.All[pick].Name); } Pump(200); }
            SetF("tab", 0); Pump(100); Shot(tag + "-mix");
            Call("OpenStyles"); Pump(100); Shot(tag + "-vocal-presets"); SetF("stylesOpen", false);
            Call("OpenBusPresets"); Pump(100); Shot(tag + "-bus-presets"); SetF("stylesOpen", false);
            SetF("tab", 1); SetF("mpTab", 1); Pump(100); Shot(tag + "-mastering"); SetF("mpTab", 0); SetF("tab", 0);
            { var comp = FxRegistry.Create("comp"); comp.Prepare(44100); eng.Tracks[0].Fx.Add(comp); Call("OpenPresetSheet", comp); Pump(100); Shot(tag + "-plugin-presets"); SetF("psOpen", false); eng.Tracks[0].Fx.Remove(comp); }
        }
        { Themes.Apply(0); Pump(100); var t0 = eng.Tracks[0]; var c1 = FxRegistry.Create("comp"); c1.Prepare(44100); var r1 = FxRegistry.Create("reverb"); r1.Prepare(44100); t0.Fx.Add(c1); t0.Fx.Add(r1);
          SetF("tab", 0); Call("OpenFxWindow", t0, c1); Pump(200); Shot("plugin-window-comp"); Call("CloseFxWindow", c1); Call("OpenFxWindow", t0, r1); Pump(200); Shot("plugin-window-reverb"); Call("CloseFxWindow", r1);
          var eq = FxRegistry.Create("eq"); eq.Prepare(44100); t0.Fx.Add(eq); Call("OpenFxWindow", t0, eq); Pump(200); Shot("plugin-window-eq");
          Call("OpenFxWindow", t0, c1); Call("OpenFxWindow", t0, r1); Pump(200); Shot("plugin-windows-many"); { var wl = (System.Collections.IList)typeof(StudioForm).GetField("fxWins", BF).GetValue(sf); var fw = wl[wl.Count - 1]; var rf = fw.GetType().GetField("R"); var r0 = (RectangleF)rf.GetValue(fw); rf.SetValue(fw, new RectangleF(r0.X, 845, r0.Width, r0.Height)); Pump(200); Shot("plugin-window-low"); var r1b = (RectangleF)rf.GetValue(fw); rf.SetValue(fw, new RectangleF(r0.X, 300, r1b.Width, r1b.Height)); Pump(200); var r2 = (RectangleF)rf.GetValue(fw); Console.WriteLine("drag low: height " + r0.Height + " -> " + r1b.Height + " at y " + r1b.Y + "; back up: height " + r2.Height); } Call("CloseFxWindow", c1); Call("CloseFxWindow", r1); Call("CloseFxWindow", eq);
          { Call("GoTab", 0); Pump(200); typeof(App).GetMethod("ShowUpdateCard", BF).Invoke(app, new object[] { "9.9.9", "https://example.invalid/x.exe", new System.Collections.Generic.List<string> { "NEW: something" } }); Pump(200); Console.WriteLine("box before studio: " + (typeof(StudioForm).GetField("updYes", BF).GetValue(sf) != null)); typeof(App).GetField("studioForm", BF).SetValue(app, sf); typeof(App).GetMethod("UpdateToStudio", BF).Invoke(app, null); Console.WriteLine("box after studio opens: " + (typeof(StudioForm).GetField("updYes", BF).GetValue(sf) != null)); Pump(600); sf.Invalidate(); Pump(300); var b3 = new Bitmap(sf.Width, sf.Height); sf.DrawToBitmap(b3, new Rectangle(0, 0, sf.Width, sf.Height)); b3.Save(Path.Combine(outDir, "update-in-studio.png"), ImageFormat.Png); Console.WriteLine("saved update-in-studio"); { var keep2 = new System.Collections.Generic.List<Track>(eng.Tracks); eng.Tracks.Clear(); Call("ShowWelcome"); Pump(700); Shot("update-welcome"); SetF("welcomeOn", false); eng.Tracks.AddRange(keep2); } typeof(App).GetMethod("HideCard", BF).Invoke(app, null); SetF("updYes", null); SetF("updNo", null); SetF("bannerYes", null); Call("GoTab", 0); Pump(300); typeof(App).GetField("studioForm", BF).SetValue(app, null); }
          { Call("OpenVsts"); Pump(300); SetF("browserScroll", 99999f); Pump(200); Shot("vsts-list"); SetF("browserOpen", false); Pump(100); }
          { var sfx = new SynthFx(); sfx.Prepare(44100); sfx.LfoCurves[0].Load("Wobble 2"); sfx.Set("l1shape", 7); var swin = new SynthWindow(sfx, null); swin.Show(); Pump(600); var bb2 = new Bitmap(swin.Width, swin.Height); swin.DrawToBitmap(bb2, new Rectangle(0, 0, swin.Width, swin.Height)); bb2.Save(Path.Combine(outDir, "basslab.png"), ImageFormat.Png); Console.WriteLine("saved basslab"); { var cvm = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(swin); sfx.Set("f1on", 1); sfx.Set("m1s", 1); sfx.Set("m1d", 17); sfx.Set("m1e", 1); sfx.Set("m1a", 45); sfx.Set("m2s", 2); sfx.Set("m2d", 6); sfx.Set("m2e", 1); sfx.Set("m2a", 30); cvm.Invalidate(); Pump(300); var bbm = new Bitmap(swin.Width, swin.Height); swin.DrawToBitmap(bbm, new Rectangle(0, 0, swin.Width, swin.Height)); bbm.Save(Path.Combine(outDir, "basslab-mod.png"), ImageFormat.Png); Console.WriteLine("saved basslab-mod"); } { var cv = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(swin); var ct = cv.GetType(); ct.GetField("dragSrc", BF).SetValue(cv, 1); ct.GetField("dragName", BF).SetValue(cv, "LFO 1"); cv.Invalidate(); Pump(300); var bb3 = new Bitmap(swin.Width, swin.Height); swin.DrawToBitmap(bb3, new Rectangle(0, 0, swin.Width, swin.Height)); bb3.Save(Path.Combine(outDir, "basslab-drag.png"), ImageFormat.Png); Console.WriteLine("saved basslab-drag"); ct.GetField("dragSrc", BF).SetValue(cv, 0); } { var cv2 = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(swin); var wavp = (string)cv2.GetType().GetMethod("RenderNote", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { sfx, 48, Path.Combine(outDir, "bl") }); var wl = new float[0]; float pk = 0; var bytes = File.ReadAllBytes(wavp); for (int i = 44; i + 2 < bytes.Length; i += 3) { int v = (bytes[i] | (bytes[i + 1] << 8) | (bytes[i + 2] << 16)); if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000); pk = Math.Max(pk, Math.Abs(v / 8388608f)); } Console.WriteLine("rendered note peak " + pk.ToString("0.000") + " bytes " + bytes.Length); cv2.GetType().GetField("browseOpen", BF).SetValue(cv2, true); cv2.Invalidate(); Pump(400); var bb4 = new Bitmap(swin.Width, swin.Height); swin.DrawToBitmap(bb4, new Rectangle(0, 0, swin.Width, swin.Height)); bb4.Save(Path.Combine(outDir, "basslab-sounds.png"), ImageFormat.Png); Console.WriteLine("saved basslab-sounds"); } swin.Close(); }
          foreach (var pid in new[] { "glow", "glitchflow" }) { var px = FxRegistry.Create(pid); if (px == null || !PxPlugins.HasWindow(px)) { Console.WriteLine("no px " + pid); continue; } px.Prepare(44100); PxPlugins.Open(px, sf); Pump(400); foreach (Form fm in Application.OpenForms) if (fm.GetType().Name.Contains("Window") && fm != sf && fm.Visible) { var bb = new Bitmap(fm.Width, fm.Height); fm.DrawToBitmap(bb, new Rectangle(0, 0, fm.Width, fm.Height)); bb.Save(Path.Combine(outDir, "px-" + pid + ".png"), ImageFormat.Png); Console.WriteLine("saved px-" + pid); fm.Close(); break; } } }
        { Themes.Apply(0); Call("GoTab", 11); Pump(600); sf.Invalidate(); Pump(300); var b2 = new Bitmap(sf.Width, sf.Height); sf.DrawToBitmap(b2, new Rectangle(0, 0, sf.Width, sf.Height)); b2.Save(Path.Combine(outDir, "converter.png"), ImageFormat.Png); Console.WriteLine("saved converter"); Call("GoTab", 0); Pump(400);
          for (int pi = 0; pi < 6; pi++) { string pp = Path.Combine(StudioEnv.DataDir, "wel" + pi + ".ytmproj"); File.WriteAllText(pp, "YTMPROJ\nname|Song " + pi + "\n"); ProjectLibrary.Touch(pp, "Song " + pi, 3, 120, 100, true, null); } var keep = new System.Collections.Generic.List<Track>(eng.Tracks); eng.Tracks.Clear(); Call("ShowWelcome"); Pump(600); Shot("welcome"); SetF("welcomeOn", false); eng.Tracks.AddRange(keep); }
        { Themes.Apply(0); Call("PlaceOnStage", eng.Tracks[0]); Call("PlaceOnStage", eng.Tracks[1]); Call("OpenToolWin", 12); Pump(300); Shot("stage3d");
          Call("OpenToolWin", 13); Pump(300); Shot("profiler");
          var mres = typeof(StudioForm).GetMethod("MeasureArrangement", BF).Invoke(sf, new object[] { (long)(44100 * 30), (Action<float>)(x => { }) }); SetF("arr", mres); Call("OpenToolWin", 14); Pump(300); Shot("arrangement"); SetF("selTrack", 0); Pump(100); { var t0 = eng.Tracks[0]; var c = t0.Clip; var rl = (float[])c.L.Clone(); var rr = (float[])(c.R ?? c.L).Clone(); Array.Reverse(rl); Array.Reverse(rr); t0.Clip = AudioClip.FromArrays(rl, rr, c.Name + " (reversed)"); Pump(200); t0.Clip = AudioClip.FromArrays((float[])c.L.Clone(), (float[])(c.R ?? c.L).Clone(), c.Name); Pump(200); }
          Call("OpenToolWin", 16); Pump(300); Shot("timemachine"); Call("OpenToolWin", 17); for (int i = 0; i < 80; i++) { Pump(250); sf.Invalidate(); } Shot("autopsy"); typeof(StudioForm).GetField("toolWins", BF).FieldType.GetMethod("Clear").Invoke(typeof(StudioForm).GetField("toolWins", BF).GetValue(sf), null); }
        { Themes.Apply(0); sf.OpenInspector(1); Pump(300); Shot("mixer"); Call("CloseInspector"); Pump(100); }
        { Themes.Apply(0); Call("OpenToolWin", 18); string terr; var dry = TagMaker.Speak("Produced by Purphex", 0, 0, out terr); var ts = (TagSettings)typeof(StudioForm).GetField("tagSet", BF).GetValue(sf); ts.Fx[0] = true; ts.Fx[1] = true; ts.Character = 1;
          if (dry != null) { typeof(StudioForm).GetField("tagOut", BF).SetValue(sf, TagMaker.Process(dry, ts)); typeof(StudioForm).GetField("tagOutSig", BF).SetValue(sf, ts.Sig()); }
          Pump(300); Shot("producer-tag"); var tw = (System.Collections.IList)typeof(StudioForm).GetField("toolWins", BF).GetValue(sf); tw.Clear(); Pump(100); }
        // the Free Edition studio
        Edition.Override = Tier.Free; Themes.Apply(0); Pump(200);
        SetF("tab", 1); SetF("mpTab", 0); Pump(100); Shot("free-mastering"); SetF("mpTab", 1); Pump(100); Shot("free-mastering-presets"); SetF("mpTab", 0); SetF("tab", 0);
        SetF("menuOpen", true); Pump(100); Shot("free-menu"); SetF("menuOpen", false);
        { Call("OpenVsts"); Pump(300); Shot("free-vsts"); SetF("browserOpen", false); Pump(100); }
        StudioDialogs.ExportOptions(sf, "My song", 60, new[] { "Lead vocal", "Beat" }, r => { }, fm => { Pump(300); var b = new Bitmap(fm.Width, fm.Height); fm.DrawToBitmap(b, new Rectangle(0, 0, fm.Width, fm.Height)); b.Save(Path.Combine(outDir, "free-export.png"), ImageFormat.Png); Console.WriteLine("saved free-export"); fm.Close(); });
        Pump(500);
        Environment.Exit(0);
    }
}
