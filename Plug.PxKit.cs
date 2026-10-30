using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX VST KIT: what every Purphex plugin window shares: the painted Purphex look, knobs (drag, Shift = fine, wheel, double-click =
//  default), choice buttons, switches, meters, the preset bar (factory and your own: save, rename, delete), bypass, undo / redo and a tip line.
//  Every control works on a real setting of the plugin (the same settings automation, presets and projects use).
//  A plugin window derives from PxWindow and paints its own body in PaintBody.
// ======================================================================
abstract class PxWindow : Form
{
    static readonly Dictionary<Fx, PxWindow> open = new Dictionary<Fx, PxWindow>();
    public static PxWindow Show(Fx fx, IWin32Window owner, Func<PxWindow> make)
    {
        PxWindow w; if (!open.TryGetValue(fx, out w) || w.IsDisposed) { w = make(); open[fx] = w; if (owner != null) w.Show(owner); else w.Show(); }
        else { if (w.WindowState == FormWindowState.Minimized) w.WindowState = FormWindowState.Normal; w.Activate(); }
        return w;
    }

    protected static Color Bg0 = Color.FromArgb(20, 10, 40), Bg1 = Color.FromArgb(8, 5, 18), PanelC = Color.FromArgb(26, 15, 48), Edge = Color.FromArgb(70, 50, 120),
        Ink = Color.FromArgb(240, 236, 255), Dim = Color.FromArgb(160, 150, 195), Violet = Color.FromArgb(168, 92, 255), Cyan = Color.FromArgb(60, 225, 255),
        Pink = Color.FromArgb(255, 80, 196), Gold = Color.FromArgb(255, 205, 80), Lime = Color.FromArgb(120, 240, 140), Orange = Color.FromArgb(255, 150, 70);
    protected static Color Ctl = Color.FromArgb(38, 24, 70), CtlHot = Color.FromArgb(60, 38, 100), Track = Color.FromArgb(40, 28, 70), Well = Color.FromArgb(12, 7, 26), PBar = Color.FromArgb(30, 18, 56);
    internal static Color Mix(Color a, Color b, double t) { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
    // the app theme: backgrounds, panels, edges, controls and the four main colours of every Purphex window (called when the theme changes)
    internal static void ApplyTheme(ThemeDef t)
    {
        Bg0 = t.Bg1; Bg1 = t.Bg0; PanelC = t.Panel; Edge = Mix(t.Panel, t.EdgeC, 0.45); Violet = t.Violet; Cyan = t.Cyan; Pink = t.Pink; Lime = t.Lime;
        Ctl = Mix(t.Panel, Edge, 0.3); CtlHot = Mix(t.Panel, Edge, 0.6); Track = Mix(t.Panel, Edge, 0.35); Well = Mix(t.Bg0, Color.Black, 0.3); PBar = Mix(t.Panel, Edge, 0.12);
        GlitchFlowWindow.ApplyTheme(t);
    }
    protected readonly Fx fx; protected Color Accent = Cyan; readonly Timer tick; readonly string brand;
    protected class Hit { public RectangleF R; public Action Click, Double, Right; public Action<float> Drag; public Action<PointF> DragAt; public Action Up; public Action<int> Wheel; public string Tip = ""; public Param P; }
    protected readonly List<Hit> hits = new List<Hit>(); Hit dragHit; float dragY0, dragV0; protected Point mouse; string tip = "";
    readonly List<string> undoS = new List<string>(), redoS = new List<string>();
    public string PresetName = "Init";

    protected PxWindow(Fx f, string name, Size size)
    {
        fx = f; brand = name; Text = "Purphex " + name; ClientSize = size; MinimumSize = new Size(size.Width + 16, size.Height + 39); BackColor = Bg1; KeyPreview = true; StartPosition = FormStartPosition.CenterParent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        tick = new Timer { Interval = 33 }; tick.Tick += (a, b) => { if (!Win.AnyMoving) Invalidate(); }; tick.Start();
        FormClosed += (a, b) => { tick.Stop(); open.Remove(fx); };
        if (!string.IsNullOrEmpty(fx.CurPreset)) PresetName = fx.CurPreset;
    }

    // ---- undo: this window's history of the plugin's settings ----
    protected void Mark() { undoS.Add(fx.Serialize()); if (undoS.Count > 200) undoS.RemoveAt(0); redoS.Clear(); }
    protected void Restore(string s) { var e = StudioEngine.Current; object gate = e != null ? e.Sync : new object(); lock (gate) { fx.ResetDefaults(); fx.Deserialize(s); } }
    internal void Undo() { if (undoS.Count == 0) return; redoS.Add(fx.Serialize()); Restore(undoS[undoS.Count - 1]); undoS.RemoveAt(undoS.Count - 1); }
    internal void Redo() { if (redoS.Count == 0) return; undoS.Add(fx.Serialize()); Restore(redoS[redoS.Count - 1]); redoS.RemoveAt(redoS.Count - 1); }
    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (ActiveControl is TextBox) return base.ProcessCmdKey(ref msg, k);
        if (k == (Keys.Control | Keys.Z)) { Undo(); return true; } if (k == (Keys.Control | Keys.Y)) { Redo(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---- drawing helpers ----
    protected static GraphicsPath RR(RectangleF r, float rad) { var p = new GraphicsPath(); float d = Math.Max(1, rad * 2); p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    protected static void Fill(Graphics g, Color c, RectangleF r, float rad) { using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    protected static void Stroke(Graphics g, Color c, RectangleF r, float rad, float w) { if (rad >= 12) UiAudit.Box(r); using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }
    protected static void Txt(Graphics g, string s, float size, Color c, RectangleF r, StringAlignment al, bool bold)
    {
        size = UiAudit.Fit(g, s, size, bold, r);   // text that is too long for its box gets a little smaller instead of running out
        using (var f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)) using (var b = new SolidBrush(c)) using (var sf = new StringFormat { Alignment = al, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        { UiAudit.Text(g, s, f, r, al); g.DrawString(s, f, b, r, sf); }
    }
    protected void Panel(Graphics g, RectangleF r, string title, Color col)
    {
        Fill(g, Color.FromArgb(185, PanelC), r, 14); Stroke(g, Color.FromArgb(150, col), r, 14, 1.3f);
        if (title.Length > 0) Txt(g, title, 11, col, new RectangleF(r.X + 14, r.Y + 6, r.Width - 28, 16), StringAlignment.Near, true);
    }
    protected bool Hot(RectangleF r) { return r.Contains(mouse); }
    protected Hit Reg(RectangleF r, Action click, string tipText = "") { var h = new Hit { R = r, Click = click, Tip = tipText }; hits.Add(h); return h; }
    protected void Button(Graphics g, RectangleF r, string label, bool lit, Color col, Action click, string tipText = "", float size = 12)
    {
        bool hot = Hot(r); GalaxyBg.Face(g, r, 8, lit ? col : (hot ? CtlHot : Ctl), lit ? (hot ? 170 : 150) : 95);   // a galaxy face
        Stroke(g, Color.FromArgb(lit ? 255 : 170, lit ? Color.White : col), r, 8, lit ? 1.6f : 1.2f); Txt(g, label, size, lit ? Color.FromArgb(20, 10, 30) : Ink, r, StringAlignment.Center, true); Reg(r, click, tipText);
    }
    protected void Choice(Graphics g, RectangleF r, string id, Color col, float size = 11.5f)
    {
        var p = fx.Find(id); bool hot = Hot(r); GalaxyBg.Face(g, r, 7, hot ? CtlHot : Ctl, 95); Stroke(g, Color.FromArgb(200, col), r, 7, 1.2f);
        Txt(g, p.Text + "  ▾", size, Ink, r, StringAlignment.Center, true);
        Reg(r, () => { var m = new ContextMenuStrip(); for (int k = 0; k < p.Choices.Length; k++) { int kk = k; var it = new ToolStripMenuItem(p.Choices[k]) { Checked = (int)p.Val == k }; it.Click += (a, b) => { Mark(); fx.Set(id, kk); }; m.Items.Add(it); } m.Show(this, new Point((int)r.X, (int)r.Bottom)); }, p.Name);
    }
    // a row of buttons, one per choice (the lit one is the value)
    protected void Segments(Graphics g, RectangleF r, string id, Color col, float size = 10.5f)
    {
        var p = fx.Find(id); int n = p.Choices.Length; float w = r.Width / n;
        for (int k = 0; k < n; k++) { int kk = k; Button(g, new RectangleF(r.X + k * w + 1, r.Y, w - 2, r.Height), p.Choices[k], (int)Math.Round(p.Val) == k, col, () => { Mark(); fx.Set(id, kk); }, p.Name + ": " + p.Choices[k], size); }
    }
    protected void Knob(Graphics g, float cx, float cy, float rad, string id, string label, Color col, bool big = false)
    {
        var p = fx.Find(id); float frac = Frac(p); var r = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2); bool hot = Hot(r) || (dragHit != null && dragHit.P == p);
        float pw = big ? 6 : rad > 20 ? 4 : 3; GalaxyBg.Sphere(g, cx, cy, rad * 2 - pw * 3.2f, hot);   // the galaxy knob, inside its ring
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), pw)) g.DrawArc(pen, r.X + pw, r.Y + pw, r.Width - pw * 2, r.Height - pw * 2, 135, 270);
        if (p.Min < 0 && p.Max > 0)
        {   // a value that goes both ways is drawn from the middle
            float z = (0 - p.Min) / (p.Max - p.Min), a0 = 135 + 270 * z, a1 = 135 + 270 * frac;
            using (var pen = new Pen(col, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round }) if (Math.Abs(a1 - a0) > 0.5f) g.DrawArc(pen, r.X + pw, r.Y + pw, r.Width - pw * 2, r.Height - pw * 2, Math.Min(a0, a1), Math.Abs(a1 - a0));
        }
        else using (var pen = new Pen(col, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round }) if (frac > 0.002f) g.DrawArc(pen, r.X + pw, r.Y + pw, r.Width - pw * 2, r.Height - pw * 2, 135, 270 * frac);
        double ang = (135 + 270 * frac) * Math.PI / 180; using (var pen = new Pen(Ink, big ? 3 : 2) { EndCap = LineCap.Round }) g.DrawLine(pen, cx + (float)Math.Cos(ang) * rad * 0.25f, cy + (float)Math.Sin(ang) * rad * 0.25f, cx + (float)Math.Cos(ang) * (rad - pw * 2), cy + (float)Math.Sin(ang) * (rad - pw * 2));
        if (label.Length > 0) Txt(g, label, big ? 13 : 10.5f, Dim, new RectangleF(cx - 60, cy + rad + 2, 120, 15), StringAlignment.Center, true);
        Txt(g, Val(p), big ? 13 : 10.5f, Ink, new RectangleF(cx - 60, cy + rad + (label.Length > 0 ? (big ? 18 : 15) : 2), 120, 15), StringAlignment.Center, false);
        var h = Reg(new RectangleF(r.X, r.Y, r.Width, r.Height + (big ? 34 : 30)), null, p.Name + ": drag up or down, wheel, double-click resets"); h.P = p;
        h.Drag = dy => { float nf = Math.Max(0, Math.Min(1, dragV0 + dy)); float v = FromFrac(p, nf); fx.Set(id, p.IsChoice ? (float)Math.Round(v) : v); };
        h.Double = () => { Mark(); fx.Set(id, p.Def); }; h.Wheel = d => { Mark(); float v = FromFrac(p, Math.Max(0, Math.Min(1, Frac(p) + d * (p.IsChoice ? 1f / Math.Max(1, p.Max - p.Min) : 0.02f)))); fx.Set(id, p.IsChoice ? (float)Math.Round(v) : v); };
    }
    // a horizontal slider (for levels)
    protected void HSlider(Graphics g, RectangleF r, string id, string label, Color col)
    {
        var p = fx.Find(id); float f = Frac(p); Txt(g, label, 10, Dim, new RectangleF(r.X, r.Y - 15, r.Width, 14), StringAlignment.Near, true); Txt(g, Val(p), 10, Ink, new RectangleF(r.X, r.Y - 15, r.Width, 14), StringAlignment.Far, false);
        Fill(g, Track, r, r.Height / 2); Fill(g, col, new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * f), r.Height), r.Height / 2);
        var h = Reg(new RectangleF(r.X, r.Y - 4, r.Width, r.Height + 8), null, p.Name); h.P = p; h.DragAt = pt => fx.Set(id, FromFrac(p, Math.Max(0, Math.Min(1, (pt.X - r.X) / r.Width)))); h.Double = () => { Mark(); fx.Set(id, p.Def); };
        h.Wheel = d => { Mark(); fx.Set(id, FromFrac(p, Math.Max(0, Math.Min(1, Frac(p) + d * 0.02f)))); };
    }
    protected static bool LogP(Param p) { return (p.Unit == "Hz" || p.Unit == "ms") && p.Min > 0; }
    protected static float Frac(Param p) { if (LogP(p)) return (float)(Math.Log(p.Val / p.Min) / Math.Log(p.Max / p.Min)); return (p.Val - p.Min) / Math.Max(1e-6f, p.Max - p.Min); }
    protected static float FromFrac(Param p, float f) { if (LogP(p)) return (float)(p.Min * Math.Pow(p.Max / p.Min, f)); return p.Min + f * (p.Max - p.Min); }
    protected static string Val(Param p)
    {
        if (p.IsChoice) return p.Text; string u = p.Unit; float v = p.Val;
        if (u == "Hz" && v >= 1000) return (v / 1000).ToString(v >= 10000 ? "0.#" : "0.0#", CultureInfo.InvariantCulture) + " kHz";
        if (u == "ms" && v >= 1000) return (v / 1000).ToString("0.0#", CultureInfo.InvariantCulture) + " s";
        if (u == ":1") return v.ToString("0.0#", CultureInfo.InvariantCulture) + ":1";
        return v.ToString(Math.Abs(v) < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + (u.Length > 0 ? " " + u : "");
    }
    protected void Toggle(Graphics g, RectangleF r, string id, Color col, string on = "ON", string off = "OFF") { bool o = fx.Get(id) >= 0.5f; Button(g, r, o ? on : off, o, col, () => { Mark(); fx.Set(id, o ? 0 : 1); }, fx.Find(id).Name + ": switch on or off", 10.5f); }
    protected void Meter(Graphics g, RectangleF r, float lin, string label, bool gr = false)
    {
        Fill(g, Well, r, 4); double db = gr ? -lin : 20 * Math.Log10(Math.Max(1e-5, lin));
        if (gr) { float f = (float)Math.Max(0, Math.Min(1, lin / 24)); var lit = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, (r.Height - 4) * f); using (var b = new SolidBrush(Orange)) g.FillRectangle(b, lit); }
        else { float f = (float)Math.Max(0, Math.Min(1, (db + 48) / 54)); var lit = new RectangleF(r.X + 2, r.Bottom - 2 - (r.Height - 4) * f, r.Width - 4, Math.Max(0.1f, (r.Height - 4) * f)); using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), db > -1 ? Color.Red : Gold, Cyan, 90f)) g.FillRectangle(b, lit); }
        if (label.Length > 0) Txt(g, label, 9.5f, Dim, new RectangleF(r.X - 10, r.Bottom + 2, r.Width + 20, 13), StringAlignment.Center, true);
    }

    // ---- the header: brand, presets, bypass, undo / redo ----
    protected virtual string SubTitle { get { return ""; } }
    void PaintHeader(Graphics g, float W)
    {
        Txt(g, "PURPHEX", 12, Violet, new RectangleF(16, 8, 140, 16), StringAlignment.Near, true); Txt(g, brand.ToUpperInvariant(), 24, Ink, new RectangleF(14, 22, 300, 30), StringAlignment.Near, true);
        if (SubTitle.Length > 0) Txt(g, SubTitle, 10, Dim, new RectangleF(16, 50, 300, 14), StringAlignment.Near, false);
        using (var pen = new Pen(Color.FromArgb(150, Accent), 2)) g.DrawLine(pen, 16, 66, 200, 66);
        float pbX = Math.Max(230, W - 16 - 370 - 330 - 8), pbW = Math.Min(330, W - 16 - 370 - pbX - 8); var pb = new RectangleF(pbX, 16, pbW, 38);   /* the preset bar gets narrower in a small window */ Fill(g, PBar, pb, 10); Stroke(g, Edge, pb, 10, 1.2f);
        Button(g, new RectangleF(pb.X + 5, pb.Y + 5, 28, 28), "‹", false, Violet, () => StepPreset(-1), "Previous preset", 16);
        Button(g, new RectangleF(pb.Right - 33, pb.Y + 5, 28, 28), "›", false, Violet, () => StepPreset(1), "Next preset", 16);
        Txt(g, PresetName, 13, Ink, new RectangleF(pb.X + 38, pb.Y, pb.Width - 76, pb.Height), StringAlignment.Center, true);
        Reg(new RectangleF(pb.X + 38, pb.Y, pb.Width - 76, pb.Height), () => PresetMenu(new Point((int)pb.X, (int)pb.Bottom)), "Presets: factory and your own; save, rename, delete");
        float x = pb.Right + 8; Button(g, new RectangleF(x, 16, 92, 38), "PRESETS", false, Violet, () => PresetMenu(new Point((int)x, 54)), "Factory presets, your own presets, save, rename and delete"); x += 100;
        Button(g, new RectangleF(x, 16, 72, 38), "SAVE", false, Lime, SaveMine, "Save these settings as a preset of your own, with a name (as many as you like)", 12); x += 80;
        Button(g, new RectangleF(x, 16, 92, 38), fx.Bypass ? "OFF" : "ON", !fx.Bypass, Gold, () => fx.Bypass = !fx.Bypass, fx.Bypass ? "Off: the sound passes untouched. Press to turn it on" : "On: press to turn it off", 13); x += 100;
        Button(g, new RectangleF(x, 16, 38, 38), "↶", false, Violet, Undo, "Undo (Ctrl+Z)", 16); Button(g, new RectangleF(x + 44, 16, 38, 38), "↷", false, Violet, Redo, "Redo (Ctrl+Y)", 16);
    }
    protected abstract void PaintBody(Graphics g, RectangleF area);
    readonly ResizeSnap snap = new ResizeSnap();
    protected override void WndProc(ref Message m) { Win.TrackMove(this, ref m); base.WndProc(ref m); }
    protected override void OnPaint(PaintEventArgs e) { snap.Paint(this, e.Graphics, PaintAll); }
    void PaintAll(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; hits.Clear();
        float W = ClientSize.Width, H = ClientSize.Height;
        using (var br = new LinearGradientBrush(new RectangleF(0, 0, W, H), Bg0, Bg1, 90f)) g.FillRectangle(br, 0, 0, W, H);
        GalaxyBg.Paint(g, new Rectangle(0, 0, (int)W, (int)H), brand.GetHashCode(), 0.85f, Bg1, 80);   // the galaxy, like every Purphex window
        PaintHeader(g, W); PaintBody(g, new RectangleF(16, 76, W - 32, H - 76 - 24));
        tip = ""; foreach (var h in hits) if (h.R.Contains(mouse) && h.Tip.Length > 0) tip = h.Tip;
        if (tip.Length > 0) Txt(g, tip, 11, Dim, new RectangleF(16, H - 20, W - 32, 18), StringAlignment.Near, false);
    }

    // ---- presets ----
    List<KeyValuePair<string, string>> AllPresets() { var l = new List<KeyValuePair<string, string>>(Presets.For(fx.Type)); l.AddRange(Presets.User(fx.Type)); return l; }
    internal void ApplyPreset(string name, string data) { Mark(); Presets.Apply(fx, data); fx.CurPreset = name; fx.CurPresetData = fx.Serialize(); PresetName = name; }
    void StepPreset(int d) { var l = AllPresets(); if (l.Count == 0) return; int i = l.FindIndex(kv => kv.Key == PresetName); i = (i + d + l.Count) % l.Count; ApplyPreset(l[i].Key, l[i].Value); }
    void PresetMenu(Point at)
    {
        var m = new ContextMenuStrip(); var cats = new Dictionary<string, ToolStripMenuItem>();
        foreach (var kv in Presets.For(fx.Type))
        {
            int c = kv.Key.IndexOf(": "); string cat = c > 0 ? kv.Key.Substring(0, c) : "General"; ToolStripMenuItem sub; if (!cats.TryGetValue(cat, out sub)) { sub = new ToolStripMenuItem(cat); cats[cat] = sub; m.Items.Add(sub); }
            var k2 = kv; var it = new ToolStripMenuItem(c > 0 ? kv.Key.Substring(c + 2) : kv.Key) { Checked = kv.Key == PresetName }; it.Click += (a, b) => ApplyPreset(k2.Key, k2.Value); sub.DropDownItems.Add(it);
        }
        m.Items.Add(new ToolStripSeparator()); var mine = new ToolStripMenuItem("My presets"); m.Items.Add(mine);
        foreach (var kv in Presets.User(fx.Type)) { var k2 = kv; var it = new ToolStripMenuItem(kv.Key) { Checked = kv.Key == PresetName }; it.Click += (a, b) => ApplyPreset(k2.Key, k2.Value); mine.DropDownItems.Add(it); }
        if (mine.DropDownItems.Count == 0) mine.DropDownItems.Add(new ToolStripMenuItem("(none yet)") { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Save as my preset...").Click += (a, b) => { string nm = AskName("Name for your preset:", (PresetName.Contains(": ") ? PresetName.Substring(PresetName.IndexOf(": ") + 2) : PresetName) + " (mine)"); if (nm != null) { Presets.SaveUser(fx.Type, nm, fx); PresetName = nm; fx.CurPreset = nm; } };
        bool isMine = Presets.User(fx.Type).Any(kv => kv.Key == PresetName);
        var ren = new ToolStripMenuItem("Rename this preset...") { Enabled = isMine }; ren.Click += (a, b) => { string nm = AskName("New name:", PresetName); if (nm != null && nm != PresetName) { Presets.SaveUser(fx.Type, nm, fx); Presets.DeleteUser(fx.Type, PresetName); PresetName = nm; fx.CurPreset = nm; } }; m.Items.Add(ren);
        var del = new ToolStripMenuItem("Delete this preset...") { Enabled = isMine }; del.Click += (a, b) => { if (MessageBox.Show(this, "Delete your preset \"" + PresetName + "\"?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes) { Presets.DeleteUser(fx.Type, PresetName); PresetName = "(unsaved)"; } }; m.Items.Add(del);
        m.Show(this, at);
    }
    // SAVE: these settings as a preset of yours under a name you give (a free name is offered; an existing name asks first)
    protected void SaveMine()
    {
        string def = Presets.FreeName(fx.Type, PresetName == "Init" || PresetName.Length == 0 ? fx.Title.Replace("Purphex ", "") + " 1" : (PresetName.Contains(": ") ? PresetName.Substring(PresetName.IndexOf(": ") + 2) : PresetName));
        string nm = AskName("Name for your preset (you can save as many as you like):", def); if (nm == null) return;
        if (Presets.UserExists(fx.Type, nm) && MessageBox.Show(this, "You already have a preset called \"" + nm + "\". Replace it?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Presets.SaveUser(fx.Type, nm, fx); PresetName = nm; fx.CurPreset = nm; fx.CurPresetData = fx.Serialize();
    }
    protected string AskName(string q, string def)
    {
        using (var f = new Form { Text = Text, ClientSize = new Size(420, 120), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, BackColor = PanelC, ForeColor = Ink })
        {
            var lb = new Label { Text = q, Left = 14, Top = 12, Width = 390 }; var tb = new TextBox { Text = def, Left = 14, Top = 38, Width = 390 }; var ok = new Button { Text = "OK", Left = 230, Top = 76, Width = 80, DialogResult = DialogResult.OK }; var no = new Button { Text = "Cancel", Left = 320, Top = 76, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { lb, tb, ok, no }); f.AcceptButton = ok; f.CancelButton = no; if (f.ShowDialog(this) != DialogResult.OK) return null;
            string nm = tb.Text.Trim(); foreach (char c in System.IO.Path.GetInvalidFileNameChars()) nm = nm.Replace(c.ToString(), ""); return nm.Length == 0 ? null : nm;
        }
    }

    // ---- mouse ----
    protected override void OnMouseMove(MouseEventArgs e)
    {
        mouse = e.Location;
        if (dragHit != null) { if (dragHit.DragAt != null) dragHit.DragAt(e.Location); else if (dragHit.Drag != null) { float sens = (ModifierKeys & Keys.Shift) != 0 ? 1500f : 220f; dragHit.Drag((dragY0 - e.Y) / sens); } }
    }
    protected Hit HitAt(Point p) { for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(p)) return hits[i]; return null; }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        mouse = e.Location; var h = HitAt(e.Location); if (h == null) return;
        if (e.Button == MouseButtons.Right) { if (h.Right != null) h.Right(); return; }
        if (h.Drag != null || h.DragAt != null) { Mark(); dragHit = h; dragY0 = e.Y; dragV0 = h.P != null ? Frac(h.P) : 0; Capture = true; if (h.DragAt != null) h.DragAt(e.Location); if (h.Click != null) h.Click(); return; }
        if (h.Click != null) h.Click();
    }
    protected override void OnMouseUp(MouseEventArgs e) { if (dragHit != null && dragHit.Up != null) dragHit.Up(); dragHit = null; Capture = false; }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { var h = HitAt(e.Location); if (h != null && h.Double != null) h.Double(); }
    protected override void OnMouseWheel(MouseEventArgs e) { var h = HitAt(e.Location); if (h != null && h.Wheel != null) h.Wheel(Math.Sign(e.Delta)); }

    // ---- tests ----
    internal Bitmap Snapshot() { var bmp = new Bitmap(ClientSize.Width, ClientSize.Height); using (var g = Graphics.FromImage(bmp)) OnPaint(new PaintEventArgs(g, new Rectangle(Point.Empty, ClientSize))); return bmp; }
    internal int HitCount { get { return hits.Count; } }
    internal void TestDrag(string id, float frac) { var p = fx.Find(id); Mark(); fx.Set(id, FromFrac(p, frac)); }
}

// a ring of the latest audio (the window reads it to draw spectra and waveforms; a torn read only draws one odd frame)
// Text checks for the plugin windows: Fit shrinks a label (to 75 % at most) so it fits its box; with Begin() on (tests only) every label
// that still does not fit, or that sits across the edge of a panel, is written to Log.
static class UiAudit
{
    [ThreadStatic] public static List<string> Log; [ThreadStatic] static List<RectangleF> boxes; [ThreadStatic] static List<KeyValuePair<string, RectangleF>> texts;
    public static void Begin() { Log = new List<string>(); boxes = new List<RectangleF>(); texts = new List<KeyValuePair<string, RectangleF>>(); }
    public static List<string> End()
    {
        var res = Log; if (res == null) return new List<string>();
        foreach (var t in texts) foreach (var bx in boxes) { var tb = t.Value; if (!tb.IntersectsWith(bx)) continue; var inner = RectangleF.Inflate(bx, -2, -2); if (inner.Contains(tb)) continue; if (tb.Contains(bx)) continue;
            var ov = RectangleF.Intersect(tb, bx); if (ov.Width < 2 || ov.Height < 2) continue; res.Add("'" + t.Key + "' sits across the edge of a box at " + (int)bx.X + "," + (int)bx.Y + " " + (int)bx.Width + "x" + (int)bx.Height); break; }
        Log = null; boxes = null; texts = null; return res;
    }
    public static void Box(RectangleF r) { if (boxes != null) boxes.Add(r); }
    public static float Fit(Graphics g, string s, float size, bool bold, RectangleF r)
    {
        if (string.IsNullOrEmpty(s) || r.Width <= 0) return size;
        using (var f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)) { float w = g.MeasureString(s, f).Width; if (w <= r.Width - 2) return size; return Math.Max(size * 0.75f, size * (r.Width - 2) / w); }
    }
    public static void Text(Graphics g, string s, Font f, RectangleF r, StringAlignment al)
    {
        if (Log == null || string.IsNullOrEmpty(s)) return; float w = g.MeasureString(s, f).Width, h = f.Size * 0.8f;
        if (w > r.Width + 1) Log.Add("'" + s + "' is cut off (needs " + (int)w + " px, has " + (int)r.Width + ")");
        float x = al == StringAlignment.Near ? r.X : al == StringAlignment.Center ? r.X + (r.Width - w) / 2 : r.Right - w; texts.Add(new KeyValuePair<string, RectangleF>(s, new RectangleF(x, r.Y + (r.Height - h) / 2, Math.Min(w, r.Width), h)));
    }
}

