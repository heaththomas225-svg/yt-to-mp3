using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// Drives the Bass Lab window with synthetic mouse events (the same code path a real mouse takes) and checks what changed.   csc /main:U ... UiTest.cs
static class U
{
    static int fails; static SynthWindow w; static SynthFx fx; static Control cv; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string name) { return (T)typeof(SynthCanvas).GetField(name, BF).GetValue(cv); }
    static void SetF(string name, object v) { typeof(SynthCanvas).GetField(name, BF).SetValue(cv, v); }
    static void Paint() { cv.Refresh(); Application.DoEvents(); }

    class R { public RectangleF Rect; public Param P; public string Tip; }
    static List<R> Regs() { var l = new List<R>(); foreach (object o in (IEnumerable)typeof(SynthCanvas).GetField("regs", BF).GetValue(cv)) { var t = o.GetType(); l.Add(new R { Rect = (RectangleF)t.GetField("R").GetValue(o), P = t.GetField("P").GetValue(o) as Param, Tip = (string)t.GetField("Tip").GetValue(o) }); } return l; }
    static R Find(Func<R, bool> pred) { var l = Regs(); for (int i = l.Count - 1; i >= 0; i--) if (pred(l[i])) return l[i]; return null; }
    static Point ToClient(PointF p) { float s = F<float>("scale"), ox = F<float>("ox"), oy = F<float>("oy"); return new Point((int)(p.X * s + ox), (int)(p.Y * s + oy)); }
    static PointF Center(RectangleF r) { return new PointF(r.X + r.Width / 2, r.Y + r.Height / 2); }
    static void Ev(string name, MouseButtons b, int clicks, PointF logical) { var pt = ToClient(logical); typeof(SynthCanvas).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(cv, new object[] { new MouseEventArgs(b, clicks, pt.X, pt.Y, 0) }); }
    static void Down(PointF p, MouseButtons b = MouseButtons.Left, int clicks = 1) { Ev("OnMouseMove", MouseButtons.None, 0, p); Ev("OnMouseDown", b, clicks, p); }
    static void Move(PointF p) { Ev("OnMouseMove", MouseButtons.Left, 0, p); }
    static void Up(PointF p) { Ev("OnMouseUp", MouseButtons.Left, 1, p); Paint(); }
    static void Click(PointF p) { Down(p); Up(p); }
    static void Drag(PointF a, PointF b) { Down(a); for (int i = 1; i <= 6; i++) Move(new PointF(a.X + (b.X - a.X) * i / 6, a.Y + (b.Y - a.Y) * i / 6)); Up(b); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        Application.EnableVisualStyles(); Fx.ProjectBpm = 120; fx = new SynthFx(); fx.Prepare(44100); SynthPresets.Initialise(fx);
        w = new SynthWindow(fx, null); w.Show(); Application.DoEvents(); cv = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(w); Paint();

        // ---- LFO editor: click adds a point (and turns the shape into a drawn one), drag moves it, right-click deletes it ----
        SetF("modTab", 4); Paint(); fx.Set("l1shape", 0);
        var disp = new RectangleF(156, 448, 440, 250); var ir = new RectangleF(disp.X + 12, disp.Y + 24, disp.Width - 24, disp.Height - 38);
        var cur = fx.LfoCurves[0]; int before = cur.Pts.Count; var p1 = new PointF(ir.X + ir.Width * 0.25f, ir.Y + 4); Click(p1);
        Check("clicking the LFO picture adds a point and switches to the drawn shape", fx.Get("l1shape") == 7 && fx.LfoCurves[0].Pts.Count == before + 1, "shape " + fx.Get("l1shape") + ", points " + before + " -> " + fx.LfoCurves[0].Pts.Count);
        var pt = fx.LfoCurves[0].Pts.Find(q => Math.Abs(q.X - 0.25f) < 0.03f && q.Y > 0.9f); Check("the new point is where it was clicked", pt != null, pt == null ? "not found" : "x " + pt.X.ToString("0.00") + " y " + pt.Y.ToString("0.00"));
        if (pt != null) { Drag(p1, new PointF(ir.X + ir.Width * 0.30f, ir.Bottom - 6)); Check("dragging the point moves it", fx.LfoCurves[0].Pts.Exists(q => q.X > 0.27f && q.X < 0.33f && q.Y < -0.85f), "points: " + fx.LfoCurves[0].Serialize()); }
        int n1 = fx.LfoCurves[0].Pts.Count; Down(new PointF(ir.X + ir.Width * 0.30f, ir.Bottom - 6), MouseButtons.Right); Up(new PointF(ir.X + ir.Width * 0.30f, ir.Bottom - 6));
        Check("right-clicking a point deletes it", fx.LfoCurves[0].Pts.Count == n1 - 1, n1 + " -> " + fx.LfoCurves[0].Pts.Count);

        // ---- drag and drop: LFO 1 chip onto the Filter 1 cutoff knob ----
        SetF("tab", 0); Paint(); var chip = Find(r => r.Tip != null && r.Tip.StartsWith("Drag this onto any knob to make LFO 1")); var knob = Find(r => r.P != null && r.P.Id == "f1cut");
        Check("the LFO chip and the cutoff knob are on screen", chip != null && knob != null, "");
        if (chip != null && knob != null)
        {
            Drag(Center(chip.Rect), new PointF(knob.Rect.X + knob.Rect.Width / 2, knob.Rect.Y + 20));
            bool linked = false; for (int i = 1; i <= 16; i++) if (fx.Get("m" + i + "s") == 1 && fx.Get("m" + i + "d") == 17 && fx.Get("m" + i + "e") > 0.5f && Math.Abs(fx.Get("m" + i + "a")) > 1) linked = true;
            Check("dropping the LFO on the cutoff knob makes a matrix connection", linked, "");
        }
        // ---- the preset browser ----
        var nameReg = Find(r => r.Tip == "YOUR SOUNDS (drag n drop)"); Check("the menu button is on screen", nameReg != null, ""); if (nameReg != null) { Click(Center(nameReg.Rect)); Paint(); }
        Check("clicking it opens YOUR SOUNDS", F<bool>("browseOpen"), "");
        { SetF("menuT", 1f); SetF("browseCat", ""); Paint(); var row = Find(r => r.Tip != null && r.Tip.StartsWith("Click to load")); float s0 = F<float>("browseScroll");
          if (row != null) { var wpt = ToClient(Center(row.Rect)); typeof(SynthCanvas).GetMethod("OnMouseWheel", BF | BindingFlags.DeclaredOnly).Invoke(cv, new object[] { new MouseEventArgs(MouseButtons.None, 0, wpt.X, wpt.Y, -120) }); }
          Check("the mouse wheel over a sound in YOUR SOUNDS scrolls the list", row != null && F<float>("browseScroll") > s0, s0 + " -> " + F<float>("browseScroll")); SetF("browseScroll", 0f); }
        SetF("menuT", 1f); var dnb = Find(r => r.Tip == "Click to load DnB Roller"); SetF("browseCat", "DnB"); Paint(); dnb = Find(r => r.Tip == "Click to load DnB Roller");
        if (dnb != null) { Click(Center(dnb.Rect)); Check("clicking a preset loads it", fx.PresetName == "DnB Roller", fx.PresetName); }
        var init = Find(r => r.Tip == "Reset everything to a clean, plain sound."); if (init != null) { Click(Center(init.Rect)); Check("INITIALISE PATCH resets the sound", fx.PresetName == "Init", fx.PresetName); }
        var close = Find(r => r.Tip == null || r.Tip == ""); SetF("browseOpen", false); Paint(); for (int sk = 0; sk < 8; sk++) { System.Threading.Thread.Sleep(120); Application.DoEvents(); Paint(); }   // (the YOUR SOUNDS panel slides shut: wait for it before looking for the chain buttons)

        // ---- the effect chain: move the first box later ----
        SetF("tab", 2); Paint(); float a0 = fx.Get("fo0"), a1 = fx.Get("fo1"); var later = Regs().Find(r => r.Tip == "Move later in the chain"); Check("the chain buttons are on screen", later != null, "");
        if (later != null) { Click(Center(later.Rect)); Check("the > button moves an effect later in the chain", fx.Get("fo0") == a1 && fx.Get("fo1") == a0, "slot 1 " + a0 + " -> " + fx.Get("fo0")); }
        var led = Find(r => r.Tip != null && r.Tip.StartsWith("Switch Distortion")); if (led != null) { float was = fx.Get("dston"); Click(Center(led.Rect)); Check("the light on an effect box switches it on or off", fx.Get("dston") != was, was + " -> " + fx.Get("dston")); }

        // ---- the matrix page: second page of slots ----
        SetF("tab", 3); Paint(); var pg2 = Find(r => r.Tip == "Show matrix slots 9 to 16"); if (pg2 != null) { Click(Center(pg2.Rect)); Check("SLOTS 9-16 shows the second page", F<int>("matPage") == 1, ""); }
        var rm = Find(r => r.Tip == "Remove this connection."); Check("each matrix row has a remove button", rm != null, "");

        // ---- envelope graph: drag the attack dot ----
        SetF("tab", 0); SetF("modTab", 1); Paint(); float atk0 = fx.Get("e2a"); var h = Find(r => r.Tip == "ATTACK: drag left / right");
        if (h != null) { var c = Center(h.Rect); Drag(c, new PointF(c.X + 90, c.Y)); Check("dragging the attack dot changes the attack time", fx.Get("e2a") > atk0 + 5, atk0 + " ms -> " + fx.Get("e2a") + " ms"); }
        var hd = Find(r => r.Tip == "DECAY (left / right) and SUSTAIN (up / down)"); if (hd != null) { float s0 = fx.Get("e2s"); var c = Center(hd.Rect); Drag(c, new PointF(c.X, c.Y - 40)); Check("dragging the decay dot up raises the sustain", fx.Get("e2s") > s0 + 5, s0 + "% -> " + fx.Get("e2s") + "%"); }
        Console.WriteLine(fails == 0 ? "=== UI checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
