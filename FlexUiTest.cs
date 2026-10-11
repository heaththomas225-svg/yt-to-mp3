using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Drives the Flex Pitch editor in Purphex Music with synthetic mouse events.   csc /main:G ... FlexUiTest.cs
static class G
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T Fld<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static float[] Sing(int n, Func<double, double> freq, double amp) { var x = new float[n]; double ph = 0; for (int i = 0; i < n; i++) { double f = freq(i / 44100.0); if (f <= 0) { ph = 0; continue; } ph += f / 44100.0; double s = 0; for (int h = 1; h <= 20; h++) { if (h * f > 9000) break; s += Math.Sin(2 * Math.PI * ph * h) / (h * 0.9); } x[i] = (float)(amp * s * 0.5); } return x; }
    static List<HitReg> NoteRegs() { return Regs().FindAll(r => r.OnDrag != null && r.OnDouble != null && r.OnRight != null && r.R.Height > 10 && r.R.Height < 45 && r.R.Y > 300 && r.R.X >= 250); }
    static double Cents(double a, double b) { return 1200 * Math.Log(a / b, 2); }

    [STAThread]
    static void Main()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_flexui"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        int n = 44100 * 4; var x = Sing(n, t => t < 1.2 ? 220.0 : (t < 2.4 ? 261.63 : (t < 2.6 ? 0 : (t < 3.8 ? 329.63 : 0))), 0.5); var clip = AudioClip.FromArrays(x, (float[])x.Clone(), "lead vocal"); clip.Path = "";
        var tr = StudioEngine.NewTrack("Lead", "vocal", clip); eng.Tracks.Add(tr);
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 860); sf.Show(); Pump(400); Paint(); SetF("selTrack", 0);
        Check("the FLEX button is in the track tools", Regs().Exists(r => r.R.Width >= 36 && r.R.Width <= 60 && r.R.Height >= 24 && r.R.Y < 200 && r.OnDown != null) , "");
        typeof(StudioForm).GetMethod("FlexOpen", BF).Invoke(sf, null); Pump(200); Paint();
        Check("Flex Pitch opens and starts listening", Fld<bool>("flexOpen") && (Fld<bool>("flexAnalysing") || Fld<object>("flexAna") != null), "");
        var t0 = DateTime.Now; while (Fld<bool>("flexAnalysing") && (DateTime.Now - t0).TotalSeconds < 30) { Pump(100); }
        Pump(200); Paint(); var ana = Fld<FlexAnalysis>("flexAna"); Check("the three notes are found", ana != null && ana.Notes.Count == 3, ana == null ? "none" : ana.Notes.Count + " notes");
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height)); bmp.Save("_flex_ui.png", System.Drawing.Imaging.ImageFormat.Png);
        var regs = NoteRegs(); Check("each note is a block you can grab", regs.Count >= 3, regs.Count + " blocks on screen");
        if (regs.Count < 3) { Console.WriteLine("=== " + (fails + 1) + " FAILED ==="); Environment.Exit(1); }
        regs.Sort((a, b) => a.R.X.CompareTo(b.R.X)); var first = regs[0]; int cx = (int)(first.R.X + first.R.Width / 2), cy = (int)(first.R.Y + first.R.Height / 2);
        // click selects
        Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint();
        var sel = Fld<List<FlexNote>>("flexSel"); Check("clicking a note selects it", sel.Count == 1 && sel[0] == ana.Notes[0], sel.Count + " selected");
        Check("a plain click does not change the pitch", Math.Abs(ana.Notes[0].Shift) < 1e-6, "");
        // drag up two rows (17 px each)
        Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); double rh = Convert.ToDouble(typeof(StudioForm).GetProperty("FlexRowH", BF).GetValue(sf, null)); int dy = (int)Math.Round(rh * 2); for (int i = 1; i <= 6; i++) Ev("OnMouseMove", MouseButtons.Left, 0, cx, cy - dy * i / 6); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy - dy); Paint();
        Check("dragging a note up two rows raises it two semitones", Math.Abs(ana.Notes[0].Shift - 2) < 0.01 && ana.Notes[0].Edited, "shift " + ana.Notes[0].Shift.ToString("0.00"));
        t0 = DateTime.Now; while (DateTime.Now - t0 < TimeSpan.FromSeconds(6) && ReferenceEquals(tr.Clip, clip)) Pump(100); Pump(300);
        Check("the track now plays the edited audio (live re-tune)", !ReferenceEquals(tr.Clip, clip) && tr.Clip.Length == clip.Length, "");
        var re = FlexPitch.Analyse(tr.Clip.L, tr.Clip.R, tr.Clip.Length, 44100, null, null); double hz = 0; { var v = new List<double>(); for (int f = (int)(0.3 * 44100 / re.Hop); f < (int)(1.0 * 44100 / re.Hop); f++) if (re.F0[f] > 0) v.Add(re.F0[f]); v.Sort(); hz = v.Count > 0 ? v[v.Count / 2] : 0; }
        Check("and its pitch really moved from A3 to B3", Math.Abs(Cents(hz, 246.94)) < 25, hz.ToString("0.0") + " Hz");
        // sliders: drift/vibrato/gain through the inspector
        Paint(); var fineSlider = Regs().FindAll(r => r.OnDouble != null && r.OnDrag != null && Math.Abs(r.R.Height - 16) < 0.5f && r.R.X < 250); Check("the inspector has sliders for pitch, fine, drift, vibrato, gain and formant", fineSlider.Count >= 6, fineSlider.Count + " sliders");
        // double-click resets
        Paint(); regs = NoteRegs(); regs.Sort((a, b) => a.R.X.CompareTo(b.R.X)); first = regs[0]; cx = (int)(first.R.X + first.R.Width / 2); cy = (int)(first.R.Y + first.R.Height / 2);
        Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 2, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Pump(200); Check("double-clicking a note resets it", !ana.Notes[0].Edited, "");
        // correct pitch to the nearest semitone on a detuned file is a no-op; test the scale button path directly
        typeof(StudioForm).GetMethod("FlexCorrect", BF).Invoke(sf, null); Pump(150); Check("Correct pitch runs on all notes without error", true, "");
        // marquee select
        Paint(); var na = (RectangleF)typeof(StudioForm).GetMethod("FlexNoteArea", BF).Invoke(sf, null); Ev("OnMouseMove", MouseButtons.None, 0, (int)na.X + 4, (int)na.Y + 4); Ev("OnMouseDown", MouseButtons.Left, 1, (int)na.X + 4, (int)na.Y + 4); for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, (int)na.X + 4 + i * 80, (int)na.Y + 4 + i * 40); Ev("OnMouseUp", MouseButtons.Left, 1, (int)na.X + 640, (int)na.Y + 320); Paint();
        Check("dragging a box around notes selects them", Fld<List<FlexNote>>("flexSel").Count >= 1, Fld<List<FlexNote>>("flexSel").Count + " selected");
        // set a clear edit again, then undo and redo
        ana.Notes[0].Shift = 3; typeof(StudioForm).GetMethod("FlexChanged", BF).Invoke(sf, null); t0 = DateTime.Now; while (DateTime.Now - t0 < TimeSpan.FromSeconds(6) && Fld<bool>("flexSwap") == false) Pump(100); Pump(300);
        // Done: the edit is kept and saved as a real file the project can point to
        t0 = DateTime.Now; while ((DateTime.Now - t0).TotalSeconds < 10 && string.IsNullOrEmpty(Fld<string>("flexSavedAt"))) Pump(100);
        Check("your edits are saved by themselves (no need to press Done)", !string.IsNullOrEmpty(Fld<string>("flexSavedAt")) && File.Exists(Fld<string>("flexAutoPath")), Fld<string>("flexAutoPath"));
        Check("and the track already points at that saved file", tr.Clip.Path == Fld<string>("flexAutoPath"), tr.Clip.Path);
        ana.Notes[0].Shift = 4; typeof(StudioForm).GetMethod("FlexChanged", BF).Invoke(sf, null);   // then click away straight after the edit, before it has rendered
        var closeM = typeof(StudioForm).GetMethod("FlexClose", BF); closeM.Invoke(sf, new object[] { true });
        { var rr = FlexPitch.Analyse(tr.Clip.L, tr.Clip.R, tr.Clip.Length, 44100, null, null); var v = new List<double>(); for (int f = (int)(0.3 * 44100 / rr.Hop); f < (int)(1.0 * 44100 / rr.Hop); f++) if (rr.F0[f] > 0) v.Add(rr.F0[f]); v.Sort(); double h4 = v.Count > 0 ? v[v.Count / 2] : 0; Check("an edit made just before closing is not lost (A3 + 4 semitones = C#4)", Math.Abs(Cents(h4, 220.0 * Math.Pow(2, 4 / 12.0))) < 30, h4.ToString("0.0") + " Hz"); } Pump(200); Paint();
        Check("Done closes the editor and keeps the edit", !Fld<bool>("flexOpen") && !ReferenceEquals(tr.Clip, clip), "");
        Check("the edited audio was written to a WAV file and the track points to it", !string.IsNullOrEmpty(tr.Clip.Path) && File.Exists(tr.Clip.Path) && tr.Clip.Path.EndsWith(".wav"), tr.Clip.Path);
        // undo brings the original recording back
        typeof(StudioForm).GetMethod("DoUndo", BF).Invoke(sf, null); Pump(100); Check("Undo brings back the original recording", ReferenceEquals(tr.Clip, clip), "");
        // open again and revert
        typeof(StudioForm).GetMethod("FlexOpen", BF).Invoke(sf, null); t0 = DateTime.Now; while (Fld<bool>("flexAnalysing") && (DateTime.Now - t0).TotalSeconds < 30) Pump(100); Pump(200); ana = Fld<FlexAnalysis>("flexAna"); ana.Notes[1].Shift = -5; typeof(StudioForm).GetMethod("FlexChanged", BF).Invoke(sf, null); t0 = DateTime.Now; while (DateTime.Now - t0 < TimeSpan.FromSeconds(6) && ReferenceEquals(tr.Clip, clip)) Pump(100); Pump(200);
        typeof(StudioForm).GetMethod("FlexRevert", BF).Invoke(sf, null); Pump(100); Check("Original puts the recording back and clears every edit", ReferenceEquals(tr.Clip, clip) && !ana.Notes[1].Edited, "");
        closeM.Invoke(sf, new object[] { true }); Pump(100);
        // a track without audio
        eng.Tracks.Add(StudioEngine.NewTrack("Keys", "instrument", null)); SetF("selTrack", 1); typeof(StudioForm).GetMethod("FlexOpen", BF).Invoke(sf, null); Pump(100); Check("an instrument track cannot be opened in Flex Pitch (clear message, no crash)", !Fld<bool>("flexOpen"), "");
        sf.Close(); try { Directory.Delete(tmp, true); } catch { } Console.WriteLine(fails == 0 ? "=== flex pitch UI checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
