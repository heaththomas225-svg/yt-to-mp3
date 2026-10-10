using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

// =====================================================================================================
//  Bass Lab presets: a preset is just "id=value;id=value" laid over the default patch.
// =====================================================================================================
static class SynthPresets
{
    public static readonly string[] Names = { "Wobble Bass", "Reese Bass", "Growl Bass", "Deep Sub", "Pluck Bass", "Acid Squelch", "Supersaw Lead", "Soft Pad", "Init" };
    static readonly Dictionary<string, string> Data = new Dictionary<string, string>
    {
        { "Wobble Bass", "aon=1;awt=45;aoct=-1;auni=3;adet=14;alvl=85;bon=1;bwt=75;boct=-1;blvl=55;bfin=7;subon=1;suboct=-1;sublvl=65;f1on=1;f1type=1;f1cut=500;f1res=42;f1drive=25;e2a=0;e2d=420;e2s=0;" +
            "m1s=5;m1d=17;m1a=25;m2s=1;m2d=17;m2a=58;m3s=2;m3d=9;m3a=22;m4s=10;m4d=17;m4a=60;m5s=11;m5d=18;m5a=35;l1sync=8;l1shape=0;l2sync=5;l2shape=6;bbon=1;bbfreq=55;bbgain=7;bbharm=25;dston=1;dstmode=2;dstdrive=9;dstmix=30;mac1=0;mac2=0" },
        { "Reese Bass", "aon=1;awt=50;aoct=-1;auni=5;adet=26;alvl=80;bon=1;bwt=50;boct=-1;bfin=-14;buni=5;bdet=22;blvl=75;subon=1;sublvl=55;f1on=1;f1type=1;f1cut=650;f1res=18;f1drive=18;" +
            "m1s=1;m1d=17;m1a=22;l1sync=3;l1shape=0;m2s=2;m2d=22;m2a=35;l2sync=2;l2shape=0;chon=1;chmix=35;bbon=1;bbfreq=52;bbgain=7;bbharm=18" },
        { "Growl Bass", "aon=1;awt=60;aoct=-1;auni=3;adet=10;alvl=85;awarp=3;wamt=0;awamt=0;bon=1;bwt=40;boct=-1;blvl=60;subon=1;sublvl=60;f1on=1;f1type=1;f1cut=900;f1res=50;f1drive=40;" +
            "m1s=1;m1d=9;m1a=65;l1sync=13;l1shape=0;m2s=2;m2d=17;m2a=55;l2sync=11;l2shape=6;m3s=5;m3d=10;m3a=30;e2d=300;e2s=0;dston=1;dstmode=1;dstdrive=14;dstmix=55;bbon=1;bbfreq=58;bbgain=8;bbharm=30" },
        { "Deep Sub", "aon=1;awt=0;aoct=-1;auni=1;alvl=90;subon=1;subwave=0;suboct=-1;sublvl=60;f1on=1;f1type=1;f1cut=400;f1res=5;f1drive=0;e1a=4;e1d=300;e1s=90;e1r=200;bbon=1;bbfreq=45;bbgain=9;bbharm=22;bbq=0.9" },
        { "Pluck Bass", "aon=1;awt=70;aoct=-1;auni=2;adet=8;alvl=85;bon=1;bwt=25;boct=0;blvl=35;subon=1;sublvl=55;f1on=1;f1type=1;f1cut=300;f1res=30;e1a=0;e1d=160;e1s=0;e1r=90;e2a=0;e2d=170;e2s=0;m1s=5;m1d=17;m1a=60;bbon=1;bbfreq=60;bbgain=6" },
        { "Acid Squelch", "mode=2;glide=90;aon=1;awt=66;aoct=-1;auni=1;alvl=90;bon=0;subon=1;sublvl=45;f1on=1;f1type=1;f1cut=350;f1res=72;f1drive=35;e1d=300;e1s=70;e2a=0;e2d=260;e2s=10;m1s=5;m1d=17;m1a=55;m2s=1;m2d=17;m2a=20;l1sync=11;dston=1;dstmode=0;dstdrive=8;dstmix=25;bbon=1;bbfreq=62;bbgain=5" },
        { "Supersaw Lead", "aon=1;awt=50;aoct=0;auni=7;adet=34;alvl=80;bon=1;bwt=50;boct=0;bfin=6;buni=5;bdet=26;blvl=60;subon=0;f1on=1;f1type=0;f1cut=6500;f1res=10;e1a=8;e1d=400;e1s=75;e1r=300;m1s=1;m1d=2;m1a=0;chon=1;chmix=30;dlon=1;dlsync=3;dlmix=22;rvon=1;rvmix=22;bbon=0" },
        { "Soft Pad", "aon=1;awt=35;aoct=0;auni=5;adet=22;alvl=75;bon=1;bwt=60;boct=0;bfin=5;buni=3;bdet=18;blvl=50;subon=0;f1on=1;f1type=0;f1cut=2400;f1res=12;e1a=700;e1d=900;e1s=80;e1r=1100;m1s=1;m1d=17;m1a=18;l1sync=3;chon=1;chmix=40;rvon=1;rvsize=70;rvmix=30;bbon=0" },
        { "Init", "aon=1;awt=0;aoct=0;auni=1;alvl=80;subon=0;f1on=0;f1cut=20000;bbon=0;e1a=2;e1d=300;e1s=100;e1r=120;vol=-10" }
    };
    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "synthpresets"); } }
    public static void Apply(SynthFx fx, string name)
    {
        fx.ResetDefaults();
        string d; if (Data.TryGetValue(name, out d)) fx.Deserialize(d);
        else { try { string f = Path.Combine(Dir, name + ".txt"); if (File.Exists(f)) fx.Deserialize(File.ReadAllText(f)); } catch { } }
        fx.PresetName = name; fx.MarkDirty(); fx.Reset();
    }
    public static List<string> UserNames()
    {
        var l = new List<string>(); try { if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir, "*.txt")) l.Add(Path.GetFileNameWithoutExtension(f)); } catch { }
        l.Sort(); return l;
    }
    public static void SaveUser(SynthFx fx, string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, name + ".txt"), fx.Serialize()); fx.PresetName = name;
    }
}

// =====================================================================================================
//  The Bass Lab window: a custom-drawn synth panel (same layout idea as the big wavetable synths: oscillators and
//  filters on top, macros / envelopes / LFOs below, keyboard at the bottom) in this app's own style.
// =====================================================================================================
class SynthWindow : Form, IKeySink
{
    readonly SynthFx fx; readonly Track track; readonly SynthCanvas canvas; readonly PianoPanel piano; int octave = 3; readonly HashSet<int> down = new HashSet<int>();
    static readonly Keys[] KeyMap = { Keys.A, Keys.W, Keys.S, Keys.E, Keys.D, Keys.F, Keys.T, Keys.G, Keys.Y, Keys.H, Keys.U, Keys.J, Keys.K, Keys.O, Keys.L, Keys.P, Keys.Oemtilde };
    Timer tick;

    public SynthWindow(SynthFx fx, Track t)
    {
        this.fx = fx; track = t; Text = "Purphex Bass Lab"; BackColor = Color.FromArgb(10, 4, 20); ForeColor = Color.FromArgb(240, 232, 255); KeyPreview = true; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimumSize = new Size(900, 700); Win.Dark(this);
        canvas = new SynthCanvas(fx, this) { Dock = DockStyle.Fill };
        piano = new PianoPanel(this, 49) { Dock = DockStyle.Bottom, Height = 124 };
        Controls.Add(canvas); Controls.Add(piano); ClientSize = new Size(1240, 716 + 124);
        Activated += (s, e) => { MidiInput.Target = fx; };
        Shown += (s, e) => { MidiInput.Target = fx; if (Prefs.MidiDev.Length > 0 && MidiInput.OpenName != Prefs.MidiDev) MidiInput.Open(Prefs.MidiDev); var se = StudioEngine.Current; if (se != null) se.EnsureAudio(); };
        tick = new Timer { Interval = 33 }; tick.Tick += (s, e) => { if (!Win.AnyMoving) canvas.Step(); }; tick.Start();
        FormClosed += (s, e) => { if (MidiInput.Target == fx) MidiInput.Target = null; };
        FormClosing += (s, e) => { tick.Stop(); ReleaseAll(); };
        Deactivate += (s, e) => ReleaseAll();
    }

    public void PanicAll() { down.Clear(); fx.Panic(); piano.Invalidate(); }
    public void TestSound() { fx.NoteOn(48, 100); var t = new Timer { Interval = 700 }; t.Tick += (s, e) => { t.Stop(); t.Dispose(); fx.NoteOff(48); }; t.Start(); }
    public void KeyOn(int note, int vel) { if (!down.Add(note)) return; fx.NoteOn(note, vel); var f = InstrumentFx.Recorder; if (f != null) f(track, true, note, vel); piano.Invalidate(); }
    public void KeyOff(int note) { if (!down.Remove(note)) return; fx.NoteOff(note); var f = InstrumentFx.Recorder; if (f != null) f(track, false, note, 0); piano.Invalidate(); }
    void ReleaseAll() { foreach (var n in new List<int>(down)) KeyOff(n); }
    public int BaseNote { get { return 12 * (octave + 1); } }
    public bool IsDown(int note) { return down.Contains(note); }
    public int Octave { get { return octave; } set { octave = Math.Max(0, Math.Min(8, value)); } }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if ((k == Keys.Z || k == Keys.X) && msg.Msg == 0x100 && !canvas.Typing) { ReleaseAll(); octave = Math.Max(0, Math.Min(8, octave + (k == Keys.Z ? -1 : 1))); piano.Invalidate(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); int i = Array.IndexOf(KeyMap, e.KeyCode);
        if (i >= 0 && !e.Control && !e.Alt) { KeyOn(12 * (octave + 1) + i, 100); e.Handled = true; }
    }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); int i = Array.IndexOf(KeyMap, e.KeyCode); if (i >= 0) KeyOff(12 * (octave + 1) + i); }
}

partial class SynthCanvas : Control
{
    class Reg { public RectangleF R; public Action<PointF> Down; public Action<float, float> Drag; public Action<int> Wheel; public Action Double; public Action Right; public string Tip = ""; public Param P; public bool Log; public float Step; }
    readonly SynthFx fx; readonly SynthWindow win; readonly List<Reg> regs = new List<Reg>(); Reg hover, active; PointF mouse, downPt; float downFrac; DateTime lastInput = DateTime.Now; bool modified;
    int tab, modTab = 4; const float W = 1240, H = 716; float scale = 1, ox, oy;
    public bool Typing;
    static readonly Color Bg0 = Color.FromArgb(13, 7, 26), Bg1 = Color.FromArgb(26, 13, 48), Panel = Color.FromArgb(30, 17, 56), PanelHi = Color.FromArgb(44, 26, 80), Edge = Color.FromArgb(96, 62, 160), Ink = Color.FromArgb(240, 232, 255), Dim = Color.FromArgb(150, 132, 190),
        Acc = Color.FromArgb(188, 120, 255), Acc2 = Color.FromArgb(236, 92, 226), Good = Color.FromArgb(120, 232, 150), Disp = Color.FromArgb(14, 8, 28);
    static readonly Dictionary<string, string> Tips = new Dictionary<string, string> {
        { "f1cut", "Cutoff: how bright the sound is. Wobble comes from an LFO moving this." }, { "f1res", "Resonance: the 'wah' peak at the cutoff." }, { "f1drive", "Drive: pushes the filter input for a dirtier bass." },
        { "bbgain", "Bass boost: how much the low end is raised." }, { "bbfreq", "Bass boost frequency: where the boost is centred (type an exact Hz by double-clicking)." },
        { "bbq", "Bass boost width: low = wide and gentle, high = narrow." }, { "bbharm", "Adds harmonics of the bass so it is audible on small speakers." }, { "bbtight", "Cuts the inaudible rumble below this frequency." },
        { "awt", "Wave: morphs Sine > Triangle > Saw > Square > Pulse." }, { "awamt", "Warp amount: how much the Warp type bends the wave (growl!)." }, { "auni", "Unison: stacked voices for a fat sound." },
        { "mac1", "Macro 1: a knob you can aim at anything in the Matrix." }, { "vol", "Master volume of the synth." }
    };

    public SynthCanvas(SynthFx fx, SynthWindow w)
    {
        this.fx = fx; win = w; DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); BackColor = Bg0;
    }

    public void Step() { if ((DateTime.Now - lastInput).TotalMilliseconds < 800 || fx.ActiveVoices > 0) Invalidate(); }
    void Poke() { lastInput = DateTime.Now; Invalidate(); }

    // ---------- drawing helpers ----------
    static void Txt(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    { using (var b = new SolidBrush(c)) g.DrawString(s, GfxCache.Font(px, bold), b, r, GfxCache.Fmt(ha, true)); }
    static GraphicsPath RR(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void Fill(Graphics g, Color c, RectangleF r, float rad) { using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    static void Stroke(Graphics g, Color c, RectangleF r, float rad, float w) { using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }
    static RectangleF Rc(float x, float y, float w, float h) { return new RectangleF(x, y, w, h); }
    Reg Add(RectangleF r) { var g = new Reg { R = r }; regs.Add(g); return g; }
    bool Hot(RectangleF r) { return hover != null && hover.R == r; }

    float FracOf(Param p, bool log) { if (log) return (float)(Math.Log(p.Val / p.Min) / Math.Log(p.Max / p.Min)); return (p.Val - p.Min) / (p.Max - p.Min); }
    float FromFrac(Param p, bool log, float f) { f = Math.Max(0, Math.Min(1, f)); return log ? (float)(p.Min * Math.Pow(p.Max / p.Min, f)) : p.Min + f * (p.Max - p.Min); }
    static bool IsLog(Param p) { return !p.IsChoice && p.Min > 0 && p.Max / p.Min >= 8; }
    Param P(string id) { return fx.Find(id); }
    void SetVal(Param p, float v) { fx.Set(p.Id, v); modified = true; }

    // ---------- widgets ----------
    void Knob(Graphics g, string id, float cx, float cy, string label, Color col, float rad = 17)
    {
        var p = P(id); if (p == null) return; bool log = IsLog(p); float f = Math.Max(0, Math.Min(1, FracOf(p, log)));
        var area = Rc(cx - 28, cy - rad - 4, 56, rad * 2 + 24); var reg = Add(area); reg.P = p; reg.Log = log; reg.Tip = Tips.ContainsKey(id) ? Tips[id] : p.Name;
        bool hot = Hot(area) || active == reg;
        var kr = Rc(cx - rad, cy - rad, rad * 2, rad * 2);
        using (var br = new LinearGradientBrush(kr, Color.FromArgb(62, 44, 100), Color.FromArgb(20, 12, 40), 90f)) g.FillEllipse(br, kr);
        using (var pen = new Pen(Color.FromArgb(hot ? 220 : 120, Acc), 1.2f)) g.DrawEllipse(pen, kr);
        var ar = Rc(cx - rad - 3, cy - rad - 3, rad * 2 + 6, rad * 2 + 6);
        using (var pen = new Pen(Color.FromArgb(70, 120, 90, 170), 3f)) g.DrawArc(pen, ar, 135, 270);
        bool bipolar = p.Min < 0 && p.Max > 0 && !p.IsChoice; float zero = bipolar ? (0 - p.Min) / (p.Max - p.Min) : 0;
        float a0 = bipolar ? zero : 0, sweep = (f - a0) * 270f;
        using (var pen = new Pen(col, 3f)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; if (Math.Abs(sweep) > 0.5f) g.DrawArc(pen, ar, 135 + a0 * 270f, sweep); }
        double ang = (135 + f * 270) * Math.PI / 180; using (var pen = new Pen(Ink, 2f)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, cx + (float)Math.Cos(ang) * rad * 0.35f, cy + (float)Math.Sin(ang) * rad * 0.35f, cx + (float)Math.Cos(ang) * rad * 0.85f, cy + (float)Math.Sin(ang) * rad * 0.85f); }
        Txt(g, hot ? p.Text : label, hot ? 11 : 10, hot ? Ink : Dim, Rc(cx - 30, cy + rad + 3, 60, 14), StringAlignment.Center, hot);
        reg.Down = pt => { downPt = pt; downFrac = FracOf(p, log); };
        reg.Drag = (dx, dy) => { float fine = (ModifierKeys & Keys.Shift) != 0 ? 0.25f : 1f; SetVal(p, FromFrac(p, log, downFrac - dy / 160f * fine)); };
        reg.Wheel = d => { float step = p.IsChoice ? 1 : (p.Max - p.Min) / 50f * ((ModifierKeys & Keys.Shift) != 0 ? 0.2f : 1f); if (p.Id == "auni" || p.Id == "buni" || p.Id == "cuni") step = 1; SetVal(p, (float)Math.Round(p.Val + d * step, p.IsChoice || p.Max - p.Min < 20 ? 0 : 1)); };
        reg.Double = () => TypeValue(p); reg.Right = () => SetVal(p, p.Def);
    }

    void TypeValue(Param p)
    {
        Typing = true;
        try
        {
            string s = StudioDialogs.Ask(FindForm(), p.Name + (p.Unit.Length > 0 ? " (" + p.Unit + ")" : "") + " - type a value:", p.Val.ToString("0.##", CultureInfo.InvariantCulture)); if (s == null) return;
            float v; if (float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) SetVal(p, v);
        }
        finally { Typing = false; Poke(); }
    }

    void Power(Graphics g, string id, RectangleF r)
    {
        var p = P(id); if (p == null) return; bool on = p.Val > 0.5f; var reg = Add(r); reg.Tip = p.Name + (on ? ": on (click to switch off)" : ": off (click to switch on)");
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        using (var b = new SolidBrush(on ? Good : Color.FromArgb(70, 60, 90))) g.FillEllipse(b, cx - 5, cy - 5, 10, 10);
        if (on) using (var pen = new Pen(Color.FromArgb(90, Good), 3f)) g.DrawEllipse(pen, cx - 7, cy - 7, 14, 14);
        reg.Down = pt => { SetVal(p, on ? 0 : 1); };
    }

    void Choose(Graphics g, string id, RectangleF r, Color col, string prefix = "")
    {
        var p = P(id); if (p == null || !p.IsChoice) return; var reg = Add(r); reg.P = p; reg.Tip = p.Name + ": click to choose, mouse wheel to step";
        Fill(g, Hot(r) ? PanelHi : Color.FromArgb(20, 11, 38), r, 5); Stroke(g, Color.FromArgb(Hot(r) ? 230 : 130, col), r, 5, 1);
        Txt(g, prefix + p.Text, 11, Ink, Rc(r.X + 5, r.Y, r.Width - 16, r.Height), StringAlignment.Near, false); Txt(g, "v", 9, Dim, Rc(r.Right - 14, r.Y, 12, r.Height), StringAlignment.Center, false);
        reg.Down = pt => { var m = Win.NewMenu(); for (int i = 0; i < p.Choices.Length; i++) { int k = i; var it = new ToolStripMenuItem(p.Choices[i]) { Checked = (int)Math.Round(p.Val) == i }; it.Click += (a, b) => { SetVal(p, k); Poke(); }; m.Items.Add(it); } m.Show(this, new Point((int)(pt.X * scale + ox), (int)((r.Bottom) * scale + oy))); };
        reg.Wheel = d => SetVal(p, (p.Val + (d > 0 ? 1 : -1) + p.Choices.Length) % p.Choices.Length);
    }

    void NumBox(Graphics g, string id, RectangleF r, string label)
    {
        var p = P(id); if (p == null) return; var reg = Add(r); reg.P = p; reg.Tip = p.Name + ": drag up/down, or double-click to type";
        Fill(g, Hot(r) ? PanelHi : Color.FromArgb(18, 10, 34), r, 4); Txt(g, label, 9, Dim, Rc(r.X + 3, r.Y, 30, r.Height), StringAlignment.Near, true);
        Txt(g, p.Val.ToString(p.Max - p.Min >= 100 ? "0" : "+0;-0;0", CultureInfo.InvariantCulture), 11, Ink, Rc(r.X + 22, r.Y, r.Width - 24, r.Height), StringAlignment.Far, false);
        float start = 0; reg.Down = pt => { downPt = pt; start = p.Val; downFrac = p.Val; };
        reg.Drag = (dx, dy) => SetVal(p, (float)Math.Round(downFrac - dy / (p.Max - p.Min > 100 ? 1.0 : 10.0), 0));
        reg.Wheel = d => SetVal(p, p.Val + d); reg.Double = () => TypeValue(p); reg.Right = () => SetVal(p, p.Def);
    }

    // ---------- top-level paint ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        try { Paint2(e.Graphics); } catch (ArgumentException) { } catch (InvalidOperationException) { } catch (NullReferenceException) { }
    }
    void Paint2(Graphics g)
    {
        regs.Clear(); scale = Math.Min(ClientSize.Width / W, ClientSize.Height / H); if (scale <= 0) return; ox = (ClientSize.Width - W * scale) / 2; oy = (ClientSize.Height - H * scale) / 2;
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var br = new LinearGradientBrush(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Bg0, Bg1, 80f)) g.FillRectangle(br, 0, 0, ClientSize.Width, ClientSize.Height);
        g.TranslateTransform(ox, oy); g.ScaleTransform(scale, scale);
        PaintTop(g);
        switch (tab) { case 0: PaintOsc(g); break; case 1: PaintMix(g); break; case 2: PaintFx(g); break; case 3: PaintMatrix(g); break; default: PaintGlobal(g); break; }
        PaintMods(g);
        string tip = hover != null ? hover.Tip : ""; if (tip.Length > 0) { var tr = Rc(8, H - 22, W - 16, 18); Txt(g, tip, 11, Ink, tr, StringAlignment.Near, false); }
        g.ResetTransform();
    }

    void PaintTop(Graphics g)
    {
        var logo = Rc(8, 6, 182, 50); Fill(g, Panel, logo, 8); Stroke(g, Edge, logo, 8, 1);
        using (var br = new LinearGradientBrush(logo, Acc, Acc2, 0f)) using (var f = new Font("Segoe UI Black", 19f, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("BASS LAB", f, br, logo.X + 14, logo.Y + 6);
        Txt(g, "synth for bass + leads", 10, Dim, Rc(logo.X + 14, logo.Y + 30, 160, 14), StringAlignment.Near, false);
        string[] tabs = { "OSC", "MIX", "FX", "MATRIX", "GLOBAL" }; float x = 200;
        for (int i = 0; i < tabs.Length; i++)
        {
            int k = i; var r = Rc(x, 6, i == 3 ? 86 : (i == 4 ? 86 : 70), 50); bool sel = tab == i; Fill(g, sel ? PanelHi : Panel, r, 6); Stroke(g, sel ? Acc : Edge, r, 6, sel ? 2 : 1);
            using (var b = new SolidBrush(sel ? Good : Color.FromArgb(70, 60, 90))) g.FillEllipse(b, r.X + r.Width / 2 - 3, r.Y + 8, 6, 6);
            Txt(g, tabs[i], 13, sel ? Ink : Dim, Rc(r.X, r.Y + 20, r.Width, 24), StringAlignment.Center, true); Add(r).Down = pt => { tab = k; }; regs[regs.Count - 1].Tip = "Show the " + tabs[i] + " page";
            x += r.Width + 4;
        }
        // preset box
        var pb = Rc(620, 6, 270, 50); Fill(g, Panel, pb, 8); Stroke(g, Edge, pb, 8, 1);
        Txt(g, (modified ? "* " : "") + fx.PresetName, 15, Acc, Rc(pb.X + 12, pb.Y + 4, pb.Width - 90, 24), StringAlignment.Near, true);
        { var se = StudioEngine.Current; string st = se != null ? se.AudioStatus : "Audio: no engine"; bool bad = st.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0; Txt(g, fx.ActiveVoices + " voice" + (fx.ActiveVoices == 1 ? "" : "s") + "  |  " + st, 10, bad ? Color.FromArgb(255, 140, 150) : Dim, Rc(pb.X + 12, pb.Y + 28, pb.Width - 24, 16), StringAlignment.Near, false); }
        var nameR = Rc(pb.X, pb.Y, pb.Width - 84, pb.Height); var nr = Add(nameR); nr.Tip = "Choose a preset, or save your own"; nr.Down = pt => PresetMenu(pt);
        var prev = Rc(pb.Right - 78, pb.Y + 10, 30, 30); var next = Rc(pb.Right - 44, pb.Y + 10, 30, 30);
        Fill(g, Hot(prev) ? PanelHi : Color.FromArgb(20, 11, 38), prev, 5); Txt(g, "<", 15, Ink, prev, StringAlignment.Center, true); Add(prev).Down = pt => StepPreset(-1);
        Fill(g, Hot(next) ? PanelHi : Color.FromArgb(20, 11, 38), next, 5); Txt(g, ">", 15, Ink, next, StringAlignment.Center, true); Add(next).Down = pt => StepPreset(1);
        var pnc = Rc(896, 6, 88, 50); Fill(g, Hot(pnc) ? Color.FromArgb(160, 40, 80) : Color.FromArgb(120, 30, 62), pnc, 8); Stroke(g, Acc2, pnc, 8, 1.5f); Txt(g, "PANIC", 14, Ink, Rc(pnc.X, pnc.Y + 6, pnc.Width, 24), StringAlignment.Center, true); Txt(g, "all notes off", 9, Ink, Rc(pnc.X, pnc.Y + 30, pnc.Width, 14), StringAlignment.Center, false);
        var pnr = Add(pnc); pnr.Tip = "Panic: stops every note and every ringing tail right now."; pnr.Down = pt => win.PanicAll();
        // quick bass boost
        var bb = Rc(990, 6, 150, 50); Fill(g, Panel, bb, 8); Stroke(g, Edge, bb, 8, 1);
        Power(g, "bbon", Rc(bb.X + 4, bb.Y + 4, 24, 24)); Txt(g, "BASS BOOST", 10, Ink, Rc(bb.X + 4, bb.Y + 30, 80, 16), StringAlignment.Near, true);
        Knob(g, "bbgain", bb.X + 100, bb.Y + 20, "BOOST", Acc2, 14);
        // main volume
        Knob(g, "vol", 1190, 28, "MAIN", Acc, 16);
        var tb = Rc(1146, 40, 0, 0);
    }

    void StepPreset(int d)
    {
        var all = new List<string>(SynthPresets.Names); all.AddRange(SynthPresets.UserNames()); int i = all.IndexOf(fx.PresetName); i = (i + d + all.Count) % all.Count;
        SynthPresets.Apply(fx, all[i]); modified = false;
    }
    void PresetMenu(PointF pt)
    {
        var m = Win.NewMenu();
        foreach (var n in SynthPresets.Names) { string nn = n; var it = new ToolStripMenuItem(n) { Checked = fx.PresetName == n }; it.Click += (a, b) => { SynthPresets.Apply(fx, nn); modified = false; Poke(); }; m.Items.Add(it); }
        var us = SynthPresets.UserNames();
        if (us.Count > 0) { m.Items.Add(new ToolStripSeparator()); foreach (var n in us) { string nn = n; var it = new ToolStripMenuItem("My: " + n) { Checked = fx.PresetName == n }; it.Click += (a, b) => { SynthPresets.Apply(fx, nn); modified = false; Poke(); }; m.Items.Add(it); } }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Save current sound as a preset...").Click += (a, b) =>
        {
            Typing = true; string s = StudioDialogs.Ask(FindForm(), "Preset name:", fx.PresetName == "Init" ? "My bass" : fx.PresetName + " 2"); Typing = false; if (s == null) return;
            try { SynthPresets.SaveUser(fx, s); modified = false; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save preset"); }
            Poke();
        };
        m.Show(this, new Point((int)(620 * scale + ox), (int)(56 * scale + oy)));
    }

    // ---------- OSC page ----------
    void ModuleFrame(Graphics g, RectangleF r, string title, string powerId)
    {
        Fill(g, Panel, r, 8); Stroke(g, Edge, r, 8, 1); var hd = Rc(r.X, r.Y, r.Width, 24); Fill(g, PanelHi, hd, 8);
        if (powerId != null) Power(g, powerId, Rc(r.X + 4, r.Y, 22, 24));
        Txt(g, title, 12, Ink, Rc(r.X + (powerId != null ? 26 : 10), r.Y, r.Width - 30, 24), StringAlignment.Near, true);
    }

    // ---------- FX page ----------
    void PaintFx(Graphics g)
    {
        float y = 66, h = 252, x = 8;
        var d = Rc(x, y, 214, h); ModuleFrame(g, d, "DISTORTION", "dston"); Choose(g, "dstmode", Rc(d.X + 8, d.Y + 34, d.Width - 16, 20), Acc2);
        Knob(g, "dstdrive", d.X + 56, d.Y + 110, "DRIVE", Acc2, 20); Knob(g, "dsttone", d.X + 156, d.Y + 110, "TONE", Acc, 20); Knob(g, "dstmix", d.X + 106, d.Y + 200, "MIX", Acc, 20); x += 217;
        var c = Rc(x, y, 190, h); ModuleFrame(g, c, "CHORUS", "chon"); Knob(g, "chrate", c.X + 50, c.Y + 80, "RATE", Acc, 18); Knob(g, "chdepth", c.X + 140, c.Y + 80, "DEPTH", Acc, 18); Knob(g, "chmix", c.X + 95, c.Y + 170, "MIX", Acc2, 18); x += 193;
        var dl = Rc(x, y, 190, h); ModuleFrame(g, dl, "DELAY", "dlon"); Choose(g, "dlsync", Rc(dl.X + 8, dl.Y + 34, dl.Width - 16, 20), Acc); Knob(g, "dlfb", dl.X + 50, dl.Y + 110, "FEEDBACK", Acc, 18); Knob(g, "dlmix", dl.X + 140, dl.Y + 110, "MIX", Acc2, 18); x += 193;
        var rv = Rc(x, y, 190, h); ModuleFrame(g, rv, "REVERB", "rvon"); Knob(g, "rvsize", rv.X + 50, rv.Y + 80, "SIZE", Acc, 18); Knob(g, "rvdamp", rv.X + 140, rv.Y + 80, "DAMP", Acc, 18); Knob(g, "rvmix", rv.X + 95, rv.Y + 170, "MIX", Acc2, 18); x += 193;
        // bass boost with a response graph you can drag
        var bb = Rc(x, y, W - 8 - x, h); ModuleFrame(g, bb, "BASS BOOST", "bbon");
        var gr = Rc(bb.X + 8, bb.Y + 30, bb.Width - 16, 100); DrawBoostGraph(g, gr);
        float kx = bb.X + 34, ky = bb.Y + 190; float dx = (bb.Width - 50) / 5f;
        Knob(g, "bbfreq", kx, ky, "FREQUENCY", Acc2, 17); Knob(g, "bbgain", kx + dx, ky, "BOOST", Acc2, 17); Knob(g, "bbq", kx + dx * 2, ky, "WIDTH", Acc, 17); Knob(g, "bbharm", kx + dx * 3, ky, "HARMONICS", Acc, 17); Knob(g, "bbtight", kx + dx * 4, ky, "TIGHTEN", Acc, 17);
    }
    void DrawBoostGraph(Graphics g, RectangleF r)
    {
        Fill(g, Disp, r, 5); var f = P("bbfreq"); var gn = P("bbgain"); var q = P("bbq"); bool on = P("bbon").Val > 0.5f;
        var co = Biquad.Coeffs(Biquad.LowShelf, 44100, f.Val, Math.Max(0.3, q.Val), gn.Val); var pts = new List<PointF>(); int N = 120;
        using (var pen = new Pen(Color.FromArgb(40, 150, 130, 200))) for (int hz = 50; hz <= 500; hz *= 2) { float gx = r.X + 6 + (r.Width - 12) * (float)(Math.Log(hz / 20.0) / Math.Log(25)); g.DrawLine(pen, gx, r.Y + 4, gx, r.Bottom - 4); Txt(g, hz + " Hz", 9, Dim, Rc(gx + 2, r.Bottom - 14, 50, 12), StringAlignment.Near, false); }
        for (int i = 0; i <= N; i++) { double hz = 20 * Math.Pow(25, i / (double)N); double db = Biquad.MagDb(co, hz, 44100); pts.Add(new PointF(r.X + 6 + (r.Width - 12) * i / (float)N, r.Bottom - 14 - (float)((db + 2) / 28.0) * (r.Height - 26))); }
        var c = on ? Color.FromArgb(236, 92, 226) : Color.FromArgb(90, 90, 110); using (var pen = new Pen(c, 2.2f)) g.DrawLines(pen, pts.ToArray());
        float px = r.X + 6 + (r.Width - 12) * (float)(Math.Log(f.Val / 20.0) / Math.Log(25)), py = r.Bottom - 14 - (float)((gn.Val + 2) / 28.0) * (r.Height - 26);
        using (var b = new SolidBrush(Ink)) g.FillEllipse(b, px - 5, py - 5, 10, 10); using (var pen = new Pen(c, 2f)) g.DrawEllipse(pen, px - 8, py - 8, 16, 16);
        Txt(g, "Drag the dot: left / right = frequency, up / down = boost.   " + f.Val.ToString("0") + " Hz,  +" + gn.Val.ToString("0.0") + " dB", 10, Dim, Rc(r.X + 8, r.Y + 3, r.Width - 16, 14), StringAlignment.Near, false);
        var reg = Add(r); reg.Tip = "Drag the dot to fine-tune the bass boost. Double-click a knob below to type an exact value."; reg.Down = pt => BoostDrag(r, pt); reg.Drag = (dx, dy) => BoostDrag(r, new PointF(mouse.X, mouse.Y));
    }
    void BoostDrag(RectangleF r, PointF pt)
    {
        var f = P("bbfreq"); var gn = P("bbgain"); float fx0 = Math.Max(0, Math.Min(1, (pt.X - r.X - 6) / (r.Width - 12))); double hz = 20 * Math.Pow(25, fx0);
        SetVal(f, (float)Math.Max(f.Min, Math.Min(f.Max, hz))); float gy = 1 - Math.Max(0, Math.Min(1, (pt.Y - r.Y - 12) / (r.Height - 26))); SetVal(gn, (float)Math.Max(0, Math.Min(24, gy * 28 - 2)));
    }

    // ---------- MATRIX page ----------
    void PaintMatrix(Graphics g)
    {
        var r = Rc(8, 66, W - 16, 252); Fill(g, Panel, r, 8); Stroke(g, Edge, r, 8, 1);
        Txt(g, "MOD MATRIX: pick a source (an LFO, envelope, velocity, macro...), pick what it moves, and how much.  This is how you build wobbles.", 12, Ink, Rc(r.X + 12, r.Y + 4, r.Width - 24, 20), StringAlignment.Near, true);
        for (int i = 1; i <= 8; i++)
        {
            float y = r.Y + 28 + (i - 1) * 27; Txt(g, i.ToString(), 12, Dim, Rc(r.X + 12, y, 20, 24), StringAlignment.Center, true);
            Choose(g, "m" + i + "s", Rc(r.X + 40, y + 2, 190, 22), Acc); Txt(g, "->", 13, Dim, Rc(r.X + 232, y, 30, 24), StringAlignment.Center, true); Choose(g, "m" + i + "d", Rc(r.X + 264, y + 2, 220, 22), Acc2);
            var p = P("m" + i + "a"); var sr = Rc(r.X + 510, y + 2, 520, 22); var reg = Add(sr); reg.P = p; reg.Tip = "Amount: drag, wheel, or double-click to type (negative flips it)";
            Fill(g, Color.FromArgb(18, 10, 34), sr, 5); float f = (p.Val - p.Min) / (p.Max - p.Min); float mid = sr.X + sr.Width / 2; float vx = sr.X + sr.Width * f;
            using (var b = new SolidBrush(Color.FromArgb(200, Acc2))) g.FillRectangle(b, Math.Min(mid, vx), sr.Y + 6, Math.Abs(vx - mid), sr.Height - 12); using (var pen = new Pen(Dim)) g.DrawLine(pen, mid, sr.Y + 3, mid, sr.Bottom - 3);
            Txt(g, p.Val.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " %", 12, Ink, Rc(sr.Right + 10, y, 80, 24), StringAlignment.Near, true);
            reg.Down = pt => { SetVal(p, FromFrac(p, false, (pt.X - sr.X) / sr.Width)); }; reg.Drag = (dx, dy) => SetVal(p, (float)Math.Round(FromFrac(p, false, (mouse.X - sr.X) / sr.Width))); reg.Wheel = d => SetVal(p, p.Val + d * 2); reg.Double = () => TypeValue(p); reg.Right = () => SetVal(p, 0);
        }
    }

    // ---------- GLOBAL page ----------
    void PaintGlobal(Graphics g)
    {
        var r = Rc(8, 66, W - 16, 252); Fill(g, Panel, r, 8); Stroke(g, Edge, r, 8, 1);
        Txt(g, "GLOBAL: voices, playing, tuning, MIDI and audio", 12, Ink, Rc(r.X + 12, r.Y + 4, 600, 20), StringAlignment.Near, true);
        float x = r.X + 60, y = r.Y + 70;
        Knob(g, "vol", x, y, "VOLUME", Acc, 22); Knob(g, "voices", x + 105, y, "VOICE LIMIT", Acc2, 22); Knob(g, "glide", x + 210, y, "GLIDE", Acc2, 22); Knob(g, "bend", x + 315, y, "BEND RANGE", Acc, 22); Knob(g, "tune", x + 420, y, "MASTER TUNE", Acc, 22); Knob(g, "vsens", x + 525, y, "VELOCITY", Acc, 22);
        Choose(g, "mode", Rc(r.X + 20, r.Y + 130, 190, 26), Acc, "Voices: ");
        Txt(g, "Poly = chords.  Mono = one note at a time (best for bass).  Legato = mono, notes glide into each other without retriggering. Glide sets how slowly the pitch slides.", 11, Dim, Rc(r.X + 220, r.Y + 126, 520, 34), StringAlignment.Near, false);
        // ---- audio + MIDI + panic ----
        var ap = Rc(r.X + 760, r.Y + 28, r.Width - 780, 100); Fill(g, Color.FromArgb(24, 13, 46), ap, 8); Stroke(g, Edge, ap, 8, 1);
        var eng = StudioEngine.Current; string audio = eng != null ? eng.AudioStatus : "Audio: no engine"; bool bad = audio.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0;
        Txt(g, audio, 12, bad ? Color.FromArgb(255, 140, 150) : Good, Rc(ap.X + 10, ap.Y + 6, ap.Width - 20, 18), StringAlignment.Near, true);
        Txt(g, fx.ActiveVoices + " of " + (int)P("voices").Val + " voices in use", 12, Ink, Rc(ap.X + 10, ap.Y + 26, ap.Width - 20, 18), StringAlignment.Near, false);
        Txt(g, MidiInput.Status + (MidiInput.OpenName.Length > 0 ? "  (" + MidiInput.Received + " messages)" : ""), 11, MidiInput.Status.StartsWith("MIDI: listening") ? Good : Dim, Rc(ap.X + 10, ap.Y + 46, ap.Width - 20, 32), StringAlignment.Near, false);
        var test = Rc(ap.X + 10, ap.Y + 72, 120, 24); Fill(g, Hot(test) ? PanelHi : Color.FromArgb(18, 10, 34), test, 5); Stroke(g, Acc, test, 5, 1); Txt(g, "Test sound", 12, Ink, test, StringAlignment.Center, true);
        var tr = Add(test); tr.Tip = "Plays a short note so you can check that you hear Bass Lab."; tr.Down = pt => win.TestSound();
        var pan = Rc(ap.X + 138, ap.Y + 72, 110, 24); Fill(g, Hot(pan) ? Color.FromArgb(150, 40, 80) : Color.FromArgb(110, 30, 60), pan, 5); Stroke(g, Acc2, pan, 5, 1); Txt(g, "PANIC", 12, Ink, pan, StringAlignment.Center, true);
        var pr = Add(pan); pr.Tip = "Stops every note and every ringing tail right now."; pr.Down = pt => win.PanicAll();
        var md = Rc(r.X + 20, r.Y + 176, 330, 26); Fill(g, Hot(md) ? PanelHi : Color.FromArgb(20, 11, 38), md, 5); Stroke(g, Acc, md, 5, 1);
        Txt(g, "MIDI keyboard: " + (Prefs.MidiDev.Length == 0 ? "Off" : Prefs.MidiDev), 12, Ink, Rc(md.X + 8, md.Y, md.Width - 24, md.Height), StringAlignment.Near, false); Txt(g, "v", 9, Dim, Rc(md.Right - 16, md.Y, 12, md.Height), StringAlignment.Center, false);
        var mr = Add(md); mr.Tip = "Choose a plugged-in MIDI keyboard or controller. Notes, pitch bend and sustain pedal work."; mr.Down = pt => MidiMenu(md);
        Txt(g, "Computer keyboard: A S D F G H J = white keys, W E T Y U = black keys, Z / X = octave.  Mouse: click and hold a piano key (lower on the key = louder).", 11, Dim, Rc(r.X + 20, r.Y + 212, 1100, 30), StringAlignment.Near, false);
    }
    void MidiMenu(RectangleF at)
    {
        var m = Win.NewMenu(); var devs = MidiInput.Devices();
        var off = new ToolStripMenuItem("Off") { Checked = Prefs.MidiDev.Length == 0 }; off.Click += (a, b) => { Prefs.MidiDev = ""; MidiInput.Open(""); Prefs.Changed(); Poke(); }; m.Items.Add(off);
        foreach (var d in devs) { string dn = d; var it = new ToolStripMenuItem(d) { Checked = Prefs.MidiDev == d }; it.Click += (a, b) => { Prefs.MidiDev = dn; MidiInput.Target = fx; MidiInput.Open(dn); Prefs.Changed(); Poke(); }; m.Items.Add(it); }
        if (devs.Count == 0) m.Items.Add("(no MIDI device found: plug one in and open this menu again)").Enabled = false;
        m.Show(this, new Point((int)(at.X * scale + ox), (int)(at.Bottom * scale + oy)));
    }

    // ---------- modulation section ----------
    void PaintMods(Graphics g)
    {
        float y = 404;
        var mac = Rc(8, y, 130, 306); Fill(g, Panel, mac, 8); Stroke(g, Edge, mac, 8, 1); Txt(g, "MACROS", 12, Ink, Rc(mac.X, mac.Y + 2, mac.Width, 20), StringAlignment.Center, true);
        for (int i = 0; i < 4; i++) Knob(g, "mac" + (i + 1), mac.X + 33 + (i % 2) * 64, mac.Y + 62 + (i / 2) * 100, "MACRO " + (i + 1), Acc2, 20);
        string[] tn = { "ENV 1", "ENV 2", "ENV 3", "ENV 4", "LFO 1", "LFO 2", "LFO 3", "LFO 4" }; float x = 144, tw = 144;
        for (int i = 0; i < 8; i++)
        {
            int k = i; var r = Rc(x + i * (tw + 2), y, tw, 28); bool sel = modTab == i; Fill(g, sel ? PanelHi : Panel, r, 6); Stroke(g, sel ? Acc : Edge, r, 6, sel ? 2 : 1);
            Txt(g, tn[i] + (i == 0 ? "  (volume)" : ""), 12, sel ? Ink : Dim, r, StringAlignment.Center, true); var rg = Add(r); rg.Tip = i == 0 ? "Envelope 1 shapes the volume of every note" : "Open " + tn[i]; rg.Down = pt => { modTab = k; };
        }
        var pn = Rc(144, y + 32, W - 152, 274); Fill(g, Panel, pn, 8); Stroke(g, Edge, pn, 8, 1);
        if (modTab < 4) PaintEnv(g, pn, modTab + 1); else PaintLfo(g, pn, modTab - 3);
    }

    void PaintEnv(Graphics g, RectangleF pn, int k)
    {
        var disp = Rc(pn.X + 12, pn.Y + 12, 560, 250); Fill(g, Disp, disp, 6);
        double a = P("e" + k + "a").Val, d = P("e" + k + "d").Val, s = P("e" + k + "s").Val / 100.0, rr = P("e" + k + "r").Val;
        double wa = Math.Sqrt(a + 20), wd = Math.Sqrt(d + 20), wh = Math.Sqrt(300), wr = Math.Sqrt(rr + 20), tot = wa + wd + wh + wr; float iw = disp.Width - 24, ih = disp.Height - 40; float x0 = disp.X + 12, yb = disp.Bottom - 14;
        PointF p0 = new PointF(x0, yb), p1 = new PointF(x0 + (float)(wa / tot) * iw, yb - ih), p2 = new PointF(p1.X + (float)(wd / tot) * iw, yb - (float)s * ih), p3 = new PointF(p2.X + (float)(wh / tot) * iw, p2.Y), p4 = new PointF(p3.X + (float)(wr / tot) * iw, yb);
        var poly = new[] { p0, p1, p2, p3, p4 }; using (var b = new SolidBrush(Color.FromArgb(45, Acc2))) g.FillPolygon(b, poly); using (var pen = new Pen(Acc2, 2.5f)) g.DrawLines(pen, poly);
        Txt(g, k == 1 ? "AMPLITUDE ENVELOPE: how every note's volume rises, falls and fades out." : "ENVELOPE " + k + ": a shape that starts every note. Aim it at something in the MATRIX page (for example Filter 1 cutoff).", 11, Dim, Rc(disp.X + 10, disp.Y + 4, disp.Width - 20, 16), StringAlignment.Near, false);
        float kx = pn.X + 640; Knob(g, "e" + k + "a", kx, pn.Y + 80, "ATTACK", Acc2, 24); Knob(g, "e" + k + "d", kx + 120, pn.Y + 80, "DECAY", Acc2, 24); Knob(g, "e" + k + "s", kx + 240, pn.Y + 80, "SUSTAIN", Acc, 24); Knob(g, "e" + k + "r", kx + 360, pn.Y + 80, "RELEASE", Acc, 24);
        if (k > 1) QuickAssign(g, Rc(kx - 30, pn.Y + 150, 480, 100), 5 + (k - 2), "Envelope " + k);
    }

    // "Send to" shortcut: finds (or makes) the matrix slot that connects this source to a destination.
    void QuickAssign(Graphics g, RectangleF r, int srcIndex, string name)
    {
        Fill(g, Color.FromArgb(24, 13, 46), r, 8); Stroke(g, Edge, r, 8, 1);
        int slot = -1; for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "s").Val == srcIndex && (int)P("m" + i + "d").Val != 0) { slot = i; break; }
        Txt(g, "WOBBLE IT: send " + name + " to", 12, Ink, Rc(r.X + 10, r.Y + 6, 240, 20), StringAlignment.Near, true);
        string[] quick = { "Filter 1 cutoff", "Osc A warp", "Volume", "All pitch", "Osc A wave", "Unison detune" }; int[] qd = { 17, 9, 1, 5, 6, 22 };
        for (int i = 0; i < quick.Length; i++)
        {
            int ii = i; var b = Rc(r.X + 10 + (i % 3) * 160, r.Y + 30 + (i / 3) * 28, 154, 24); bool cur = slot > 0 && (int)P("m" + slot + "d").Val == qd[i];
            Fill(g, cur ? Color.FromArgb(120, Acc2) : (Hot(b) ? PanelHi : Color.FromArgb(18, 10, 34)), b, 5); Stroke(g, cur ? Acc2 : Edge, b, 5, 1); Txt(g, quick[i], 11, Ink, b, StringAlignment.Center, cur);
            var rg = Add(b); rg.Tip = "Aim " + name + " at: " + quick[i] + ". Then set how much with the amount knob."; rg.Down = pt => { int s = SlotFor(srcIndex); SetVal(P("m" + s + "s"), srcIndex); SetVal(P("m" + s + "d"), qd[ii]); if (Math.Abs(P("m" + s + "a").Val) < 1) SetVal(P("m" + s + "a"), 50); };
        }
        if (slot > 0) { Knob(g, "m" + slot + "a", r.X + r.Width - 60, r.Y + 62, "AMOUNT", Acc2, 20); } else Txt(g, "(pick a target)", 11, Dim, Rc(r.X + r.Width - 130, r.Y + 52, 120, 20), StringAlignment.Center, false);
    }
    int SlotFor(int src)
    {
        for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "s").Val == src && (int)P("m" + i + "d").Val != 0) return i;
        for (int i = 1; i <= 8; i++) if ((int)P("m" + i + "d").Val == 0 || (int)P("m" + i + "s").Val == 0) return i;
        return 8;
    }

    // ---------- mouse ----------
    PointF ToLogical(Point p) { return new PointF((p.X - ox) / scale, (p.Y - oy) / scale); }
    Reg HitTest(PointF pt) { for (int i = regs.Count - 1; i >= 0; i--) if (regs[i].R.Contains(pt)) return regs[i]; return null; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); mouse = ToLogical(e.Location); lastInput = DateTime.Now;
        if (active != null && active.Drag != null) { active.Drag(mouse.X - downPt.X, mouse.Y - downPt.Y); Invalidate(); }
        else { var h = HitTest(mouse); if (h != hover) { hover = h; Invalidate(); } }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); mouse = ToLogical(e.Location); var r = HitTest(mouse); lastInput = DateTime.Now; if (r == null) return;
        if (e.Button == MouseButtons.Right) { if (r.Right != null) r.Right(); Poke(); return; }
        if (e.Clicks == 2 && r.Double != null) { r.Double(); return; }
        downPt = mouse; if (r.Down != null) r.Down(mouse); if (r.Drag != null) active = r; Poke();
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); active = null; Poke(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); var r = HitTest(ToLogical(e.Location)); if (r != null && r.Wheel != null) { r.Wheel(e.Delta > 0 ? 1 : -1); Poke(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }
}
