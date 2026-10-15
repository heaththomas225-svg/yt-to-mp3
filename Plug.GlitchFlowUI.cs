using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// ======================================================================
//  The GlitchFlow window: painted by hand in the Purphex style. Every control changes a real setting of the plugin (the same
//  settings the plugin panel, automation, presets and projects use), so automation moves the knobs and the knobs move the sound.
//  Knobs: drag up/down (Shift = fine), mouse wheel, double-click = default. Choice buttons open a list. Ctrl+Z / Ctrl+Y undo and redo.
// ======================================================================
class GlitchFlowWindow : Form
{
    static readonly Dictionary<GlitchFlowFx, GlitchFlowWindow> open = new Dictionary<GlitchFlowFx, GlitchFlowWindow>();
    public static GlitchFlowWindow Show(GlitchFlowFx fx, IWin32Window owner) { GlitchFlowWindow w; if (!open.TryGetValue(fx, out w) || w.IsDisposed) { w = new GlitchFlowWindow(fx); open[fx] = w; if (owner != null) w.Show(owner); else w.Show(); } else { if (w.WindowState == FormWindowState.Minimized) w.WindowState = FormWindowState.Normal; w.Activate(); } return w; }

    static readonly Color Bg = Color.FromArgb(14, 8, 28), Panel = Color.FromArgb(26, 15, 48), Edge = Color.FromArgb(70, 50, 120), Ink = Color.FromArgb(240, 236, 255), Dim = Color.FromArgb(160, 150, 195),
        Violet = Color.FromArgb(168, 92, 255), Cyan = Color.FromArgb(60, 225, 255), Pink = Color.FromArgb(255, 80, 196), Gold = Color.FromArgb(255, 205, 80);
    readonly GlitchFlowFx fx; readonly Timer tick;
    class Hit { public RectangleF R; public Action Click, Double; public Action<float> Drag; public Action<int> Wheel; public string Tip = ""; public Param P; }
    readonly List<Hit> hits = new List<Hit>(); Hit dragHit; float dragY0, dragV0; Point mouse; string tip = "";
    readonly List<string> undoS = new List<string>(), redoS = new List<string>();
    public string PresetName = "Init (clean start)";
    // AI GEN panel
    internal bool aiOpen, aiSetup; internal string aiStatus = "", aiPrompt = ""; internal List<string> aiNotes = new List<string>(); string aiBefore; bool aiBusy; TextBox aiBox, keyBox;

    public GlitchFlowWindow(GlitchFlowFx f)
    {
        fx = f; Text = "Purphex GlitchFlow"; ClientSize = new Size(1060, 744); MinimumSize = new Size(1076, 783); BackColor = Bg; KeyPreview = true; StartPosition = FormStartPosition.CenterParent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        tick = new Timer { Interval = 33 }; tick.Tick += (a, b) => Invalidate(); tick.Start();
        aiBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 6, 22), ForeColor = Ink, Font = new Font("Segoe UI", 11f) }; Controls.Add(aiBox);
        aiBox.KeyDown += (a, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AiRun(false); } };
        keyBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 6, 22), ForeColor = Ink, Font = new Font("Segoe UI", 10f), UseSystemPasswordChar = true }; Controls.Add(keyBox);
        FormClosed += (a, b) => { tick.Stop(); open.Remove(fx); };
        if (!string.IsNullOrEmpty(fx.CurPreset)) PresetName = fx.CurPreset;
    }

    // ---- undo (this window's own history of the plugin's settings) ----
    void Mark() { undoS.Add(fx.Serialize()); if (undoS.Count > 200) undoS.RemoveAt(0); redoS.Clear(); }
    void Restore(string s) { var e = StudioEngine.Current; object gate = e != null ? e.Sync : new object(); lock (gate) { fx.ResetDefaults(); fx.Deserialize(s); } }
    internal void Undo() { if (undoS.Count == 0) return; redoS.Add(fx.Serialize()); Restore(undoS[undoS.Count - 1]); undoS.RemoveAt(undoS.Count - 1); }
    internal void Redo() { if (redoS.Count == 0) return; undoS.Add(fx.Serialize()); Restore(redoS[redoS.Count - 1]); redoS.RemoveAt(redoS.Count - 1); }
    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (aiBox.Focused || keyBox.Focused) return base.ProcessCmdKey(ref msg, k);
        if (k == (Keys.Control | Keys.Z)) { Undo(); return true; } if (k == (Keys.Control | Keys.Y)) { Redo(); return true; } if (k == Keys.Escape && aiOpen) { CloseAi(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---- drawing helpers ----
    static GraphicsPath RR(RectangleF r, float rad) { var p = new GraphicsPath(); float d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    static void Fill(Graphics g, Color c, RectangleF r, float rad) { using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    static void Stroke(Graphics g, Color c, RectangleF r, float rad, float w) { using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }
    static void Txt(Graphics g, string s, float size, Color c, RectangleF r, StringAlignment al, bool bold)
    {
        using (var f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)) using (var b = new SolidBrush(c)) using (var sf = new StringFormat { Alignment = al, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(s, f, b, r, sf);
    }
    bool Hot(RectangleF r) { return r.Contains(mouse); }
    Hit Reg(RectangleF r, Action click, string tipText = "") { var h = new Hit { R = r, Click = click, Tip = tipText }; hits.Add(h); return h; }
    void Button(Graphics g, RectangleF r, string label, bool lit, Color col, Action click, string tipText = "", float size = 12)
    {
        bool hot = Hot(r); Fill(g, lit ? Color.FromArgb(hot ? 255 : 230, col) : (hot ? Color.FromArgb(60, 38, 100) : Color.FromArgb(38, 24, 70)), r, 8);
        Stroke(g, Color.FromArgb(lit ? 255 : 170, lit ? Color.White : col), r, 8, lit ? 1.6f : 1.2f); Txt(g, label, size, lit ? Color.FromArgb(20, 10, 30) : Ink, r, StringAlignment.Center, true); Reg(r, click, tipText);
    }
    // a choice: shows its value; click lists every choice
    void Choice(Graphics g, RectangleF r, string id, Color col, float size = 11.5f)
    {
        var p = fx.Find(id); bool hot = Hot(r); Fill(g, hot ? Color.FromArgb(56, 34, 96) : Color.FromArgb(34, 22, 62), r, 7); Stroke(g, Color.FromArgb(200, col), r, 7, 1.2f);
        Txt(g, p.Text + "  ▾", size, Ink, r, StringAlignment.Center, true);
        Reg(r, () => { var m = new ContextMenuStrip(); for (int k = 0; k < p.Choices.Length; k++) { int kk = k; var it = new ToolStripMenuItem(p.Choices[k]) { Checked = (int)p.Val == k }; it.Click += (a, b) => { Mark(); fx.Set(id, kk); }; m.Items.Add(it); } m.Show(this, new Point((int)r.X, (int)r.Bottom)); }, p.Name);
    }
    // a knob: arc from 7 to 5 o'clock, the value below
    void Knob(Graphics g, float cx, float cy, float rad, string id, string label, Color col, bool big = false)
    {
        var p = fx.Find(id); float frac = Frac(p); var r = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2); bool hot = Hot(r) || (dragHit != null && dragHit.P == p);
        using (var b = new SolidBrush(Color.FromArgb(hot ? 50 : 36, 24, hot ? 96 : 72))) g.FillEllipse(b, r);
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), big ? 6 : 4)) g.DrawArc(pen, r.X + 5, r.Y + 5, r.Width - 10, r.Height - 10, 135, 270);
        using (var pen = new Pen(col, big ? 6 : 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) if (frac > 0.002f) g.DrawArc(pen, r.X + 5, r.Y + 5, r.Width - 10, r.Height - 10, 135, 270 * frac);
        double ang = (135 + 270 * frac) * Math.PI / 180; using (var pen = new Pen(Ink, big ? 3 : 2) { EndCap = LineCap.Round }) g.DrawLine(pen, cx + (float)Math.Cos(ang) * rad * 0.25f, cy + (float)Math.Sin(ang) * rad * 0.25f, cx + (float)Math.Cos(ang) * (rad - 10), cy + (float)Math.Sin(ang) * (rad - 10));
        Txt(g, label, big ? 13 : 10.5f, Dim, new RectangleF(cx - 70, cy + rad + 2, 140, 16), StringAlignment.Center, true);
        Txt(g, Val(p), big ? 13 : 11, Ink, new RectangleF(cx - 70, cy + rad + (big ? 18 : 16), 140, 16), StringAlignment.Center, false);
        var h = Reg(new RectangleF(r.X, r.Y, r.Width, r.Height + (big ? 34 : 30)), null, p.Name + ": drag, wheel, double-click resets"); h.P = p;
        h.Drag = dy => { float nf = Math.Max(0, Math.Min(1, dragV0 + dy)); fx.Set(id, FromFrac(p, nf)); };
        h.Double = () => { Mark(); fx.Set(id, p.Def); }; h.Wheel = d => { Mark(); fx.Set(id, FromFrac(p, Math.Max(0, Math.Min(1, Frac(p) + d * 0.02f)))); };
    }
    static bool LogP(Param p) { return p.Unit == "Hz" || p.Unit == "ms"; }
    static float Frac(Param p) { if (LogP(p) && p.Min > 0) return (float)(Math.Log(p.Val / p.Min) / Math.Log(p.Max / p.Min)); return (p.Val - p.Min) / (p.Max - p.Min); }
    static float FromFrac(Param p, float f) { if (LogP(p) && p.Min > 0) return (float)(p.Min * Math.Pow(p.Max / p.Min, f)); return p.Min + f * (p.Max - p.Min); }
    static string Val(Param p) { if (p.IsChoice) return p.Text; string u = p.Unit; float v = p.Val; if (u == "Hz" && v >= 1000) return (v / 1000).ToString("0.0#", CultureInfo.InvariantCulture) + " kHz"; return v.ToString(Math.Abs(v) < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + (u.Length > 0 ? " " + u : ""); }
    void Toggle(Graphics g, RectangleF r, string id, Color col) { bool on = fx.Get(id) >= 0.5f; Button(g, r, on ? "ON" : "OFF", on, col, () => { Mark(); fx.Set(id, on ? 0 : 1); }, fx.Find(id).Name + ": switch on or off", 10.5f); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; hits.Clear();
        float W = ClientSize.Width, H = ClientSize.Height;
        using (var br = new LinearGradientBrush(new RectangleF(0, 0, W, H), Color.FromArgb(20, 10, 40), Color.FromArgb(8, 5, 18), 90f)) g.FillRectangle(br, 0, 0, W, H);
        // ---- header ----
        Txt(g, "PURPHEX", 13, Violet, new RectangleF(18, 10, 120, 18), StringAlignment.Near, true); Txt(g, "GLITCHFLOW", 26, Ink, new RectangleF(16, 24, 260, 32), StringAlignment.Near, true);
        using (var pen = new Pen(Color.FromArgb(150, Cyan), 2)) g.DrawLine(pen, 18, 60, 210, 60);
        var pb = new RectangleF(300, 16, 360, 40); Fill(g, Color.FromArgb(30, 18, 56), pb, 10); Stroke(g, Edge, pb, 10, 1.2f);
        Button(g, new RectangleF(pb.X + 6, pb.Y + 6, 28, 28), "‹", false, Violet, () => StepPreset(-1), "Previous preset", 16);
        Button(g, new RectangleF(pb.Right - 34, pb.Y + 6, 28, 28), "›", false, Violet, () => StepPreset(1), "Next preset", 16);
        Txt(g, PresetName, 13, Ink, new RectangleF(pb.X + 40, pb.Y, pb.Width - 80, pb.Height), StringAlignment.Center, true);
        Reg(new RectangleF(pb.X + 40, pb.Y, pb.Width - 80, pb.Height), () => PresetMenu(new Point((int)pb.X, (int)pb.Bottom)), "Presets: factory presets, your own, save, rename, delete");
        Button(g, new RectangleF(670, 16, 96, 40), "PRESETS", false, Violet, () => PresetMenu(new Point(670, 56)), "Factory presets, your own presets, save, rename and delete");
        Button(g, new RectangleF(774, 16, 96, 40), "AI GEN", aiOpen, Cyan, () => { if (aiOpen) CloseAi(); else OpenAi(); }, "Describe the effect in words: an AI sets GlitchFlow for you (needs an AI provider, see SETUP)");
        Button(g, new RectangleF(878, 16, 76, 40), fx.Bypass ? "BYPASSED" : "BYPASS", fx.Bypass, Gold, () => fx.Bypass = !fx.Bypass, "Bypass: the sound passes untouched", 11);
        Button(g, new RectangleF(962, 16, 40, 40), "↶", false, Violet, Undo, "Undo (Ctrl+Z)", 16); Button(g, new RectangleF(1008, 16, 40, 40), "↷", false, Violet, Redo, "Redo (Ctrl+Y)", 16);

        // ---- CORE: repeat rate, refresh rate, freeze ----
        var core = new RectangleF(16, 72, 470, 290); Fill(g, Panel, core, 16); Stroke(g, Color.FromArgb(160, Violet), core, 16, 1.4f);
        Txt(g, "CORE", 11, Violet, new RectangleF(core.X + 16, core.Y + 8, 100, 16), StringAlignment.Near, true);
        bool rFree = fx.Get("rsync") < 0.5f, fFree = fx.Get("fsync") < 0.5f;
        Knob(g, core.X + 100, core.Y + 100, 58, rFree ? "rate" : "rsync", "REPEAT RATE", Cyan, true);
        Knob(g, core.X + 260, core.Y + 100, 58, fFree ? "refresh" : "fsync", "REFRESH RATE", Pink, true);
        Choice(g, new RectangleF(core.X + 40, core.Y + 200, 120, 26), "rsync", Cyan); Choice(g, new RectangleF(core.X + 200, core.Y + 200, 120, 26), "fsync", Pink);
        Txt(g, rFree ? "free time" : "synced to the song", 10, Dim, new RectangleF(core.X + 40, core.Y + 228, 120, 14), StringAlignment.Center, false);
        Txt(g, fFree ? "free time" : "synced to the song", 10, Dim, new RectangleF(core.X + 200, core.Y + 228, 120, 14), StringAlignment.Center, false);
        bool frz = fx.Get("freeze") >= 0.5f; var fzr = new RectangleF(core.X + 350, core.Y + 40, 104, 104); bool fh = Hot(fzr);
        using (var b = new SolidBrush(frz ? Color.FromArgb(255, 70, 210, 255) : (fh ? Color.FromArgb(70, 40, 120) : Color.FromArgb(44, 28, 84)))) g.FillEllipse(b, fzr);
        using (var pen = new Pen(frz ? Color.White : Cyan, frz ? 3 : 2)) g.DrawEllipse(pen, fzr);
        Txt(g, "FREEZE", 15, frz ? Color.FromArgb(10, 10, 30) : Ink, fzr, StringAlignment.Center, true);
        Reg(fzr, () => { Mark(); fx.Set("freeze", frz ? 0 : 1); }, "Freeze: hold the captured slice and keep repeating it; press again to release");
        Knob(g, core.X + 402, core.Y + 200, 30, "mix", "DRY / WET", Violet);

        // ---- visualiser and levels ----
        var vis = new RectangleF(500, 72, 544, 290); Fill(g, Panel, vis, 16); Stroke(g, Color.FromArgb(160, Cyan), vis, 16, 1.4f);
        Txt(g, fx.ViewFrozen ? "FROZEN SLICE" : "CAPTURED SLICE", 11, fx.ViewFrozen ? Cyan : Violet, new RectangleF(vis.X + 16, vis.Y + 8, 200, 16), StringAlignment.Near, true);
        var wv = new RectangleF(vis.X + 16, vis.Y + 30, vis.Width - 120, 130); Fill(g, Color.FromArgb(12, 7, 26), wv, 10);
        float midY = wv.Y + wv.Height / 2; using (var pen = new Pen(fx.ViewActive ? Cyan : Color.FromArgb(150, Violet), 2))
            for (int k = 0; k < 256; k++) { float x = wv.X + 6 + k * (wv.Width - 12) / 255f, a = Math.Min(1, fx.View[k] * 1.6f) * (wv.Height / 2 - 6); g.DrawLine(pen, x, midY - a, x, midY + a); }
        if (fx.ViewActive) { float px = wv.X + 6 + fx.ViewPh * (wv.Width - 12); using (var pen = new Pen(Gold, 2)) g.DrawLine(pen, px, wv.Y + 4, px, wv.Bottom - 4); }
        Txt(g, fx.ViewActive ? (fx.ViewFrozen ? "holding" : "repeating") : "live", 11, fx.ViewActive ? Gold : Dim, new RectangleF(wv.Right - 90, wv.Y + 4, 84, 16), StringAlignment.Far, true);
        // the window: one step per repeat, lit when it repeats; the bar moves through it
        var steps = new RectangleF(vis.X + 16, vis.Y + 172, vis.Width - 120, 34); int nCyc = Math.Max(1, fx.ViewCycles); float sw = (steps.Width - 4) / nCyc;
        for (int s = 0; s < nCyc; s++)
        {
            var sr = new RectangleF(steps.X + 2 + s * sw, steps.Y, Math.Max(2, sw - 3), steps.Height); bool cur = s == Math.Min(nCyc - 1, (int)(fx.ViewWinPh * nCyc)); bool on = s > 0 && fx.ViewSteps[Math.Min(31, s)];
            Fill(g, s == 0 ? Color.FromArgb(70, 60, 100) : on ? Color.FromArgb(cur ? 255 : 170, Pink) : Color.FromArgb(40, 30, 70), sr, 4); if (cur) Stroke(g, Color.White, sr, 4, 1.5f);
        }
        Txt(g, "REFRESH WINDOW: first step records (live), lit steps repeat", 10, Dim, new RectangleF(steps.X, steps.Bottom + 2, steps.Width, 14), StringAlignment.Near, false);
        // meters
        float mx = vis.Right - 92; Meter(g, new RectangleF(mx, vis.Y + 30, 16, 176), fx.InPk, "IN"); Meter(g, new RectangleF(mx + 26, vis.Y + 30, 16, 176), fx.OutPk, "OUT");
        Knob(g, vis.X + 70, vis.Y + 250, 24, "in", "INPUT", Violet); Knob(g, vis.X + 170, vis.Y + 250, 24, "out", "OUTPUT", Violet);
        Txt(g, "BPM " + Fx.Bpm.ToString("0.#", CultureInfo.InvariantCulture), 12, Dim, new RectangleF(vis.X + 230, vis.Y + 238, 120, 18), StringAlignment.Near, true);

        // ---- PATTERN ----
        var pat = new RectangleF(16, 372, 1028, 112); Fill(g, Panel, pat, 16); Stroke(g, Color.FromArgb(140, Pink), pat, 16, 1.4f);
        Txt(g, "PATTERN & SHAPE", 11, Pink, new RectangleF(pat.X + 16, pat.Y + 8, 200, 16), StringAlignment.Near, true);
        Choice(g, new RectangleF(pat.X + 16, pat.Y + 34, 170, 28), "pattern", Pink); Txt(g, "which repeats sound", 10, Dim, new RectangleF(pat.X + 16, pat.Y + 64, 170, 14), StringAlignment.Center, false);
        Choice(g, new RectangleF(pat.X + 16, pat.Y + 78, 170, 26), "shape", Pink);
        float kx = pat.X + 250; string[] pk = { "intensity", "length", "decay", "fade", "pitch", "pstep", "seed" }; string[] pl = { "INTENSITY", "LENGTH", "DECAY", "FADE", "PITCH", "PITCH STEP", "SEED" };
        for (int k = 0; k < pk.Length; k++) Knob(g, kx + k * 96, pat.Y + 46, 22, pk[k], pl[k], Pink);
        Txt(g, "REVERSE", 10.5f, Dim, new RectangleF(pat.Right - 110, pat.Y + 24, 90, 16), StringAlignment.Center, true); Toggle(g, new RectangleF(pat.Right - 100, pat.Y + 44, 70, 28), "reverse", Pink);

        // ---- MODULES ----
        var mods = new RectangleF(16, 494, 1028, 214); Txt(g, "MODULES  (each one on or off; they work on the glitched sound)", 11, Cyan, new RectangleF(mods.X + 4, mods.Y - 2, 600, 16), StringAlignment.Near, true);
        var cards = new[] {
            new { T = "LOW-PASS", On = "lp_on", K = new[] { "lp_cut", "lp_res" }, L = new[] { "CUTOFF", "RESO" }, C = (string)null },
            new { T = "HIGH-PASS", On = "hp_on", K = new[] { "hp_cut" }, L = new[] { "CUTOFF" }, C = (string)null },
            new { T = "TELEPHONE", On = "tel_on", K = new[] { "tel_amt" }, L = new[] { "DRIVE" }, C = (string)null },
            new { T = "RHYTHM GATE", On = "gate_on", K = new[] { "gate_duty", "gate_depth" }, L = new[] { "LENGTH", "DEPTH" }, C = "gate_sync" },
            new { T = "DRIVE", On = "drv_on", K = new[] { "drv_amt" }, L = new[] { "AMOUNT" }, C = "drv_mode" },
            new { T = "WIDTH", On = "wid_on", K = new[] { "width" }, L = new[] { "WIDTH" }, C = (string)null },
            new { T = "MODULATION", On = "mod_on", K = new[] { "mod_rate", "mod_depth" }, L = new[] { "RATE", "DEPTH" }, C = "mod_tgt" },
            new { T = "DELAY", On = "dly_on", K = new[] { "dly_fb", "dly_mix" }, L = new[] { "FEEDBACK", "MIX" }, C = "dly_sync" },
            new { T = "REVERB", On = "rev_on", K = new[] { "rev_size", "rev_mix" }, L = new[] { "SIZE", "MIX" }, C = (string)null } };
        float cw = (mods.Width - 8 * 8) / 5f, ch = 98;
        for (int k = 0; k < cards.Length; k++)
        {
            var cd = cards[k]; int row = k / 5, col = k % 5; var cr = new RectangleF(mods.X + col * (cw + 8), mods.Y + 18 + row * (ch + 8), cw, ch); bool on = fx.Get(cd.On) >= 0.5f;
            Fill(g, on ? Color.FromArgb(40, 22, 74) : Color.FromArgb(24, 15, 44), cr, 12); Stroke(g, Color.FromArgb(on ? 230 : 90, Cyan), cr, 12, on ? 1.6f : 1);
            Txt(g, cd.T, 11, on ? Ink : Dim, new RectangleF(cr.X + 10, cr.Y + 6, cr.Width - 70, 18), StringAlignment.Near, true); Toggle(g, new RectangleF(cr.Right - 58, cr.Y + 6, 50, 22), cd.On, Cyan);
            float kx2 = cr.X + 34; for (int q = 0; q < cd.K.Length; q++) Knob(g, kx2 + q * 62, cr.Y + 50, 17, cd.K[q], cd.L[q], on ? Cyan : Color.FromArgb(120, Cyan));
            if (cd.C != null) Choice(g, new RectangleF(cr.Right - 80, cr.Y + 36, 72, 24), cd.C, Cyan, 10);
        }
        if (aiOpen) PaintAi(g, W, H); else { aiBox.Visible = false; keyBox.Visible = false; }
        // tip line
        foreach (var h in hits) if (h.R.Contains(mouse) && h.Tip.Length > 0) tip = h.Tip;
        if (tip.Length > 0) { var tr = new RectangleF(16, H - 20, W - 32, 18); Txt(g, tip, 11, Dim, tr, StringAlignment.Near, false); }
    }

    void Meter(Graphics g, RectangleF r, float pk, string label)
    {
        Fill(g, Color.FromArgb(12, 7, 26), r, 4); double db = 20 * Math.Log10(Math.Max(1e-5, pk)); float f = (float)Math.Max(0, Math.Min(1, (db + 48) / 54)); var lit = new RectangleF(r.X + 2, r.Bottom - 2 - (r.Height - 4) * f, r.Width - 4, (r.Height - 4) * f);
        using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height), db > -1 ? Color.Red : Gold, Cyan, 90f)) g.FillRectangle(b, lit);
        Txt(g, label, 9.5f, Dim, new RectangleF(r.X - 8, r.Bottom + 2, r.Width + 16, 14), StringAlignment.Center, true);
    }

    // ---- presets: factory (by category), your own, save / rename / delete ----
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
        m.Items.Add("Save as my preset...").Click += (a, b) => { string nm = AskName("Name for your preset:", PresetName.Contains(": ") ? PresetName.Substring(PresetName.IndexOf(": ") + 2) + " (mine)" : PresetName + " (mine)"); if (nm != null) { Presets.SaveUser(fx.Type, nm, fx); PresetName = nm; fx.CurPreset = nm; } };
        bool isMine = Presets.User(fx.Type).Any(kv => kv.Key == PresetName);
        var ren = new ToolStripMenuItem("Rename this preset...") { Enabled = isMine }; ren.Click += (a, b) => { string nm = AskName("New name:", PresetName); if (nm != null && nm != PresetName) { Presets.SaveUser(fx.Type, nm, fx); Presets.DeleteUser(fx.Type, PresetName); PresetName = nm; fx.CurPreset = nm; } }; m.Items.Add(ren);
        var del = new ToolStripMenuItem("Delete this preset...") { Enabled = isMine }; del.Click += (a, b) => { if (MessageBox.Show(this, "Delete your preset \"" + PresetName + "\"?", "Purphex GlitchFlow", MessageBoxButtons.YesNo) == DialogResult.Yes) { Presets.DeleteUser(fx.Type, PresetName); PresetName = "(unsaved)"; } }; m.Items.Add(del);
        m.Show(this, at);
    }
    internal static Func<string, string, string> AskHook;   // tests
    string AskName(string q, string def)
    {
        if (AskHook != null) return AskHook(q, def);
        using (var f = new Form { Text = "Purphex GlitchFlow", ClientSize = new Size(420, 120), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, BackColor = Panel, ForeColor = Ink })
        {
            var lb = new Label { Text = q, Left = 14, Top = 12, Width = 390 }; var tb = new TextBox { Text = def, Left = 14, Top = 38, Width = 390 }; var ok = new Button { Text = "OK", Left = 230, Top = 76, Width = 80, DialogResult = DialogResult.OK }; var no = new Button { Text = "Cancel", Left = 320, Top = 76, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { lb, tb, ok, no }); f.AcceptButton = ok; f.CancelButton = no; if (f.ShowDialog(this) != DialogResult.OK) return null;
            string nm = tb.Text.Trim(); foreach (char c in System.IO.Path.GetInvalidFileNameChars()) nm = nm.Replace(c.ToString(), ""); return nm.Length == 0 ? null : nm;
        }
    }

    // ---- AI GEN ----
    internal void OpenAi() { aiOpen = true; aiSetup = false; aiStatus = GlitchAi.Configured ? "Describe the effect and press GENERATE." : "AI GEN is not set up: no AI provider is configured. Press SETUP and paste your own Anthropic API key (from console.anthropic.com). Everything else in GlitchFlow works without it; QUICK MATCH works offline (keywords, not AI)."; }
    internal void CloseAi() { aiOpen = false; aiBox.Visible = false; keyBox.Visible = false; }
    internal void AiRun(bool variation)
    {
        aiPrompt = aiBox.Text.Trim(); if (aiPrompt.Length == 0) { aiStatus = "Type what you want first, for example: dark 1/16 vocal stutter that builds up with a reverse tail."; return; }
        if (!GlitchAi.Configured) { aiStatus = "Not generated: no AI provider is configured. Press SETUP to add your Anthropic API key, or use QUICK MATCH (offline keywords, not AI)."; return; }
        if (aiBusy) return; aiBusy = true; aiStatus = "Asking the AI (" + GlitchAi.Model + ")..."; aiNotes.Clear();
        GlitchAi.Generate(fx, aiPrompt, variation, (reply, err) => { try { BeginInvoke(new Action(() => AiDone(reply, err))); } catch { aiBusy = false; } });
    }
    internal void AiDone(string reply, string err)
    {
        aiBusy = false;
        if (err != null) { aiStatus = err == "no key" ? "Not generated: no API key is set. Press SETUP." : "Not generated: " + err + ". Nothing was changed."; return; }
        var c = GlitchAi.Validate(fx, reply); aiNotes = c.Notes;
        if (c.Values.Count == 0) { aiStatus = "Not generated: the AI's answer had no usable settings. Nothing was changed."; return; }
        aiBefore = fx.Serialize(); Mark(); var e = StudioEngine.Current; object gate = e != null ? e.Sync : new object();
        lock (gate) { fx.ResetDefaults(); foreach (var kv in c.Values) fx.Set(kv.Key, kv.Value); }
        PresetName = "AI: " + (aiPrompt.Length > 34 ? aiPrompt.Substring(0, 34) + "..." : aiPrompt); fx.CurPreset = PresetName;
        aiStatus = "Generated by the AI and playing now (" + c.Values.Count + " settings checked against their ranges). " + c.Summary;
    }
    internal void AiQuick()
    {
        aiPrompt = aiBox.Text.Trim(); var c = GlitchAi.QuickMatch(fx, aiPrompt); aiNotes = new List<string>();
        if (c.Values.Count == 0) { aiStatus = c.Summary; return; }
        aiBefore = fx.Serialize(); Mark(); foreach (var kv in c.Values) fx.Set(kv.Key, kv.Value); PresetName = "Quick match"; aiStatus = c.Summary;
    }
    internal void AiUndo() { if (aiBefore == null) { aiStatus = "Nothing to undo."; return; } Mark(); Restore(aiBefore); aiBefore = null; aiStatus = "Put back the settings from before."; }
    void PaintAi(Graphics g, float W, float H)
    {
        using (var dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(dim, 0, 0, W, H); Reg(new RectangleF(0, 0, W, H), () => { });
        var r = new RectangleF(W / 2 - 380, 90, 760, 470); Fill(g, Color.FromArgb(250, 22, 12, 44), r, 18); Stroke(g, Cyan, r, 18, 1.8f); Reg(r, () => { });
        Txt(g, "AI GEN", 22, Ink, new RectangleF(r.X + 22, r.Y + 14, 200, 30), StringAlignment.Near, true);
        Txt(g, GlitchAi.Configured ? "AI provider: Anthropic (" + GlitchAi.Model + "), your API key" : "AI provider: not set up", 11, GlitchAi.Configured ? Cyan : Gold, new RectangleF(r.X + 130, r.Y + 20, 420, 20), StringAlignment.Near, true);
        Button(g, new RectangleF(r.Right - 196, r.Y + 14, 90, 30), "SETUP", aiSetup, Gold, () => { aiSetup = !aiSetup; keyBox.Text = ""; }, "Add, change or remove your AI provider key", 11);
        Button(g, new RectangleF(r.Right - 98, r.Y + 14, 80, 30), "CLOSE", false, Violet, CloseAi, "", 11);
        float y = r.Y + 58;
        if (aiSetup)
        {
            Txt(g, "Anthropic API key (it stays on this PC, encrypted for your Windows user, and is only sent to api.anthropic.com):", 11, Dim, new RectangleF(r.X + 22, y, r.Width - 44, 18), StringAlignment.Near, false); y += 22;
            keyBox.Visible = true; keyBox.Bounds = new Rectangle((int)r.X + 22, (int)y, (int)r.Width - 260, 26);
            Button(g, new RectangleF(r.Right - 230, y, 100, 28), "SAVE KEY", false, Cyan, () => { GlitchAi.SaveKey(keyBox.Text); keyBox.Text = ""; aiSetup = false; aiStatus = GlitchAi.Configured ? "Key saved. Describe the effect and press GENERATE." : "No key saved."; }, "", 11);
            Button(g, new RectangleF(r.Right - 122, y, 100, 28), "REMOVE", false, Pink, () => { GlitchAi.SaveKey(""); aiStatus = "Key removed: AI GEN is not set up."; }, "", 11); y += 40;
        }
        else keyBox.Visible = false;
        Txt(g, "Describe the effect:", 12, Dim, new RectangleF(r.X + 22, y, 300, 18), StringAlignment.Near, true); y += 22;
        aiBox.Visible = true; aiBox.Bounds = new Rectangle((int)r.X + 22, (int)y, (int)r.Width - 44, 30); y += 42;
        Button(g, new RectangleF(r.X + 22, y, 130, 34), aiBusy ? "WORKING..." : "GENERATE", false, Cyan, () => AiRun(false), "Ask the AI for settings (needs SETUP)");
        Button(g, new RectangleF(r.X + 160, y, 130, 34), "REGENERATE", false, Cyan, () => AiRun(true), "Ask again for a different take on the same idea");
        Button(g, new RectangleF(r.X + 298, y, 100, 34), "UNDO", false, Pink, AiUndo, "Put back the settings from before the last generate");
        Button(g, new RectangleF(r.X + 406, y, 100, 34), "KEEP", false, Violet, CloseAi, "Keep these settings");
        Button(g, new RectangleF(r.Right - 222, y, 200, 34), "QUICK MATCH (not AI)", false, Gold, AiQuick, "Offline: a fixed list of keywords picks settings. It is not AI.", 11); y += 48;
        var st = new RectangleF(r.X + 22, y, r.Width - 44, r.Bottom - y - 18); Fill(g, Color.FromArgb(14, 8, 30), st, 10);
        using (var f = new Font("Segoe UI", 12f, GraphicsUnit.Pixel)) using (var b = new SolidBrush(Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
        {
            string all = aiStatus + (aiNotes.Count > 0 ? "\n\n" + string.Join("\n", aiNotes.Take(8).ToArray()) : "");
            g.DrawString(all, f, b, new RectangleF(st.X + 12, st.Y + 10, st.Width - 24, st.Height - 20), sf);
        }
    }

    // ---- mouse ----
    protected override void OnMouseMove(MouseEventArgs e)
    {
        mouse = e.Location; tip = "";
        if (dragHit != null && dragHit.Drag != null) { float sens = (ModifierKeys & Keys.Shift) != 0 ? 1500f : 220f; dragHit.Drag((dragY0 - e.Y) / sens); }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        mouse = e.Location; Hit h = null; for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(e.Location)) { h = hits[i]; break; }
        if (h == null) return;
        if (h.Drag != null) { Mark(); dragHit = h; dragY0 = e.Y; dragV0 = Frac(h.P); Capture = true; return; }
        if (h.Click != null) h.Click();
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragHit = null; Capture = false; }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(e.Location)) { if (hits[i].Double != null) hits[i].Double(); return; } }
    protected override void OnMouseWheel(MouseEventArgs e) { for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(e.Location)) { if (hits[i].Wheel != null) hits[i].Wheel(Math.Sign(e.Delta)); return; } }
    // tests: draw into a picture
    internal Bitmap Snapshot() { var bmp = new Bitmap(ClientSize.Width, ClientSize.Height); using (var g = Graphics.FromImage(bmp)) OnPaint(new PaintEventArgs(g, new Rectangle(Point.Empty, ClientSize))); return bmp; }
    internal int HitCount { get { return hits.Count; } }
    internal void SetPrompt(string s) { aiBox.Text = s; }
    internal void TestDrag(string id, float frac) { var p = fx.Find(id); Mark(); fx.Set(id, FromFrac(p, frac)); }
}
