using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  FLOATING PLUGIN WINDOWS: every effect can be opened in its own small window that floats over the Mix page. Windows stay open while you
//  click the timeline, the tracks, the transport or anything else; several can be open at once; each is bound to its own plugin instance
//  (the object itself, never its name), so selecting another track never redirects an open window. Drag a window by its title bar, resize
//  it from the corner, bring it forward with a click. Closing a window only closes the editor: the plugin keeps processing. The controls
//  are the plugin's real editor (the same knobs that change the sound), so every move is heard straight away, during playback too.
//  Plugins that have a window of their own (Bass Lab, VST instruments, the Purphex VSTs) open that window instead.
// =====================================================================================================
partial class StudioForm
{
    internal class FxWin { public Fx Fx; public Track Track; public RectangleF R; public long Seq; }
    internal readonly List<FxWin> fxWins = new List<FxWin>(); long fxWinSeq; FxWin fxWinDrag; PointF fxWinGrab; bool fxWinResize;
    const float FwTitle = 50, FwTool = 30, FwViz = 69;

    // open (or bring forward) the window of this plugin instance
    internal void OpenFxWindow(Track tr, Fx f)
    {
        if (f == null) return;
        var ins = f as InstrumentFx; if (ins != null) { if (tr != null) ins.Owner = tr; ins.ShowWindow(this); return; }   // instruments have their own window
        if (PxPlugins.HasWindow(f)) { PxPlugins.Open(f, this); return; }
        var w = fxWins.Find(x => ReferenceEquals(x.Fx, f));
        if (w == null)
        {
            float ww = Math.Max(340, Math.Min(430, ClientSize.Width * 0.25f)), hh = Math.Min(FwTitle + FwTool + FwViz + EditorHeight(f) - 34 + 18, Math.Max(260, ClientSize.Height - ToolH - TransH - 40));
            if (KnobEditor(f)) { ww = Math.Max(420, Math.Min(620, ClientSize.Width * 0.36f)); int nv = 0; var hid = f.Hidden.Length > 0 ? new HashSet<string>(f.Hidden.Split(',')) : new HashSet<string>(); foreach (var pq in f.P) if (!hid.Contains(pq.Id)) nv++; hh = Math.Min(FwTitle + FwTool + 8 + EditorHeight(f) - 34 - nv * 30 + KnobGridHeight(f, ww - 24) + 22, Math.Max(260, ClientSize.Height - ToolH - TransH - 40)); }   // room for the display in the middle of the knobs   // narrow, as tall as its controls need (it floats over the mixer, like a desk plugin window)
            int k = fxWins.Count; float x = Math.Max(HeadW + 20, (ClientSize.Width - PanelW - ww) / 2 + k * 28), y = ToolH + TransH + 40 + k * 28;
            w = new FxWin { Fx = f, Track = tr, R = new RectangleF(x, y, ww, hh) }; fxWins.Add(w);
        }
        w.Seq = ++fxWinSeq; tab = 0; Invalidate();
    }
    internal void CloseFxWindow(Fx f) { fxWins.RemoveAll(x => ReferenceEquals(x.Fx, f)); }
    internal FxWin FxWindowOf(Fx f) { return fxWins.Find(x => ReferenceEquals(x.Fx, f)); }
    // the plugin is still where the window thinks it is (a removed plugin or track closes its window; undo can bring the plugin back, then it reopens)
    bool FxWinAlive(FxWin w) { if (w.Track == null) return eng.MasterFx.Contains(w.Fx); if (!eng.Tracks.Contains(w.Track)) return false; lock (eng.Sync) return w.Track.Fx.Contains(w.Fx); }
    // a click inside a window brings it forward before anything else sees the click
    internal bool FxWinMouseDown(Point p)
    {
        if (tab != 0) return false; FxWin top = null; foreach (var w in fxWins) if (w.R.Contains(p) && (top == null || w.Seq > top.Seq)) top = w; if (top == null) return false;
        if (top.Seq != fxWinSeq) { top.Seq = ++fxWinSeq; } return true;
    }
    void PaintFxWindows(Graphics g)
    {
        if (tab != 0) return; fxWins.RemoveAll(w => !FxWinAlive(w)); if (fxWins.Count == 0) return;
        var look = BeginBrand(); try { foreach (var w in fxWins.OrderBy(x => x.Seq)) PaintFxWindow(g, w); } finally { EndBrand(look); }   // every plugin window in the Purphex look, whatever the theme
    }
    // keep a window reachable: its title bar always stays inside the studio
    void KeepFxWinOnScreen(FxWin w)
    {
        float minW = 340, minH = FwTitle + FwTool + 120; var r = w.R; r.Width = Math.Max(minW, Math.Min(ClientSize.Width - 20, r.Width)); r.Height = Math.Max(minH, Math.Min(ClientSize.Height - ToolH - 20, r.Height));
        r.X = Math.Max(-r.Width + 120, Math.Min(ClientSize.Width - 120, r.X)); r.Y = Math.Max(ToolH, Math.Min(ClientSize.Height - FwTitle - 4, r.Y));
        if (r.Bottom > ClientSize.Height - 6) r.Height = Math.Max(Math.Min(minH, ClientSize.Height - 6 - r.Y), ClientSize.Height - 6 - r.Y);   // a window never runs off the bottom (its editor scrolls instead)
        w.R = r;
    }
    void PaintFxWindow(Graphics g, FxWin w)
    {
        KeepFxWinOnScreen(w); var r = w.R; var f = w.Fx; bool front = w.Seq == fxWinSeq; Color acc = front ? UiTokens.Sel(NeonCyan) : NeonViolet;
        // the window: a rounded neon box with a soft shadow
        using (var sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillRectangle(sh, r.X + 6, r.Y + 8, r.Width, r.Height);
        FillRR(g, TM.C(250, 20, 10, 40), r, 12); Glow(g, acc, r, 12, front ? 0.9f : 0.45f, 3); StrokeRR(g, Color.FromArgb(front ? 230 : 150, acc), r, 12, 1.4f);
        Reg(r, () => { }).Overlay = true;   // the window takes its own clicks: nothing behind it reacts (not even a button under it)
        var wr = new HitReg { R = r, OnWheel = d => { } }; regs.Add(wr);
        // title bar: plugin, its kind, its track; drag it to move the window
        var title = Rc(r.X, r.Y, r.Width, FwTitle); var tbr = Rc(r.X + 1, r.Y + 1, r.Width - 2, FwTitle);
        // the header: a deep galaxy band with the PURPHEX logo and the plugin's name under it, and what it is / which track on the right
        using (var p = RR(tbr, 11)) { using (var tb = new LinearGradientBrush(tbr, Color.FromArgb(255, 14, 6, 34), Color.FromArgb(255, 34, 12, 70), 0f)) g.FillPath(tb, p); var gsH = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect); PaintGalaxy(g, tbr, f.Type.GetHashCode(), front ? 1.2f : 0.8f); g.Restore(gsH); }
        using (var pen = new Pen(Color.FromArgb(front ? 200 : 120, acc), 1.2f)) g.DrawLine(pen, r.X + 10, r.Y + FwTitle, r.Right - 10, r.Y + FwTitle);
        if (front && UiTokens.Shiny) PaintMaterial(g, Rc(r.X + 1, r.Y + 1, r.Width - 2, FwTitle), 11, acc, UiTokens.Finish, true);   // the accent's finish on the front window's header (its sheen moves unless motion is reduced)
        string trackName = w.Track == null ? "MASTER" : w.Track.Name; string cat = FxCategory(f);
        PaintPurphexMark(g, r.X + 12, r.Y + 1, 34, f.Title, acc);
        Txt(g, (cat + "  ·  " + trackName).ToUpperInvariant(), 9, Color.FromArgb(255, 200, 185, 240), Rc(r.Right - 250, r.Y + 32, 238, 14), StringAlignment.Far, true);
        var tr2 = Reg(title, () => { fxWinDrag = w; fxWinResize = false; fxWinGrab = new PointF(MousePos.X - w.R.X, MousePos.Y - w.R.Y); }); tr2.OnDrag = pt => { if (fxWinDrag == w && !fxWinResize) w.R = new RectangleF(pt.X - fxWinGrab.X, pt.Y - fxWinGrab.Y, w.R.Width, w.R.Height); }; tr2.OnUp = () => fxWinDrag = null;
        var cl = Rc(r.Right - 36, r.Y + 5, 26, 24); Btn(g, cl, "x", 3, () => CloseFxWindow(f), 12); Hint2(cl, "Close this window (the plugin keeps working)");
        var bp = Rc(cl.X - 72, r.Y + 5, 66, 24); Btn(g, bp, f.Bypass ? "BYPASSED" : "ON", f.Bypass ? 0 : 4, () => f.Bypass = !f.Bypass, 10); Hint2(bp, f.Bypass ? "Bypassed: the sound goes past this plugin. Click to switch it back on." : "On: click to bypass this plugin (it stays in the chain)");
        // toolbar: presets (previous / next / pick), reset
        float ty = r.Y + FwTitle + 2; var presets = FxPresetList(f); int cur = presets.FindIndex(kv => kv.Key == f.CurPreset);
        if (presets.Count > 0)
        {
            var pv = Rc(r.X + 10, ty, 28, 24); Btn(g, pv, "<", 0, () => StepFxPreset(f, -1), 11); Hint2(pv, "Previous preset");
            var pk = Rc(pv.Right + 4, ty, r.Width - 200, 24); Btn(g, pk, f.CurPreset.Length > 0 ? f.CurPreset : "PRESETS (" + presets.Count + ")", f.CurPreset.Length > 0 ? 4 : 0, () => OpenPresetSheet(f), 10); Hint2(pk, "Pick a preset (" + presets.Count + " for " + f.Title + ")");
            var nx = Rc(pk.Right + 4, ty, 28, 24); Btn(g, nx, ">", 0, () => StepFxPreset(f, 1), 11); Hint2(nx, "Next preset");
        }
        var rs = Rc(r.Right - 84, ty, 74, 24); Btn(g, rs, "RESET", 0, () => { if (AskYes("Reset " + f.Title + " to its default settings?", "Reset plugin")) { lock (eng.Sync) { f.ResetDefaults(); f.Reset(); f.CurPreset = ""; f.CurPresetData = ""; } } }, 10); Hint2(rs, "Back to this plugin's default settings");
        if (f.Error != null && f.Error.Length > 0) Txt(g, "ERROR: " + f.Error, 10, Warn, Rc(r.X + 12, ty + 26, r.Width - 24, 14), StringAlignment.Near, true);
        // the plugin's own editor (its real controls), scrolling if the window is shorter than the editor
        // the live picture of what this plugin is doing, in its own neon box
        bool knobWin = KnobEditor(f); float fwViz = knobWin ? 8 : FwViz;   // a knob window has its display in the middle of the knobs
        if (!knobWin)
        { var vb = Rc(r.X + 1.5f, r.Bottom - FwViz, r.Width - 3, FwViz - 1.5f);   // the strip along the bottom edge of the window, part of the window itself
          using (var p = new System.Drawing.Drawing2D.GraphicsPath()) { float rd = 11; p.AddLine(vb.X, vb.Y, vb.Right, vb.Y); p.AddLine(vb.Right, vb.Y, vb.Right, vb.Bottom - rd); p.AddArc(vb.Right - 2 * rd, vb.Bottom - 2 * rd, 2 * rd, 2 * rd, 0, 90); p.AddArc(vb.X, vb.Bottom - 2 * rd, 2 * rd, 2 * rd, 90, 90); p.CloseFigure();
            using (var b = new LinearGradientBrush(vb, Color.FromArgb(255, 14, 6, 32), Color.FromArgb(255, 30, 8, 52), 90f)) g.FillPath(b, p); var gs2 = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect); PaintFxViz(g, Rc(vb.X + 12, vb.Y + 4, vb.Width - 24, vb.Height - 8), f, true); g.Restore(gs2); }
          using (var pen = new Pen(FvMix(PxViolet, PxMagenta, 0.5f), 1.6f)) g.DrawLine(pen, vb.X + 8, vb.Y, vb.Right - 8, vb.Y);   // the neon line that separates it from the controls
          if (!eng.Playing) Txt(g, "PRESS PLAY TO SEE WHAT " + f.Title.ToUpperInvariant() + " IS DOING", 9, DimC, vb, StringAlignment.Center, true); }
        var body = Rc(r.X + 6, r.Y + FwTitle + FwTool + 6, r.Width - 12, r.Height - FwTitle - FwTool - fwViz - 14); float edH = EditorHeight(f) - 34;   // (without the editor's own title row)
        if (KnobEditor(f)) { int nv = 0; var hid = f.Hidden.Length > 0 ? new HashSet<string>(f.Hidden.Split(',')) : new HashSet<string>(); foreach (var pq in f.P) if (!hid.Contains(pq.Id)) nv++; edH = edH - nv * 30 + KnobGridHeight(f, body.Width - 12); }   // the settings are knobs here
        { var gsB = g.Save(); g.SetClip(body, System.Drawing.Drawing2D.CombineMode.Intersect); PaintGalaxy(g, Rc(r.X, r.Y + FwTitle, r.Width, r.Height - FwTitle - fwViz), f.Type.GetHashCode() ^ 0x5a5a, 1.1f); g.Restore(gsB); }
        var gs = g.Save(); g.SetClip(body, System.Drawing.Drawing2D.CombineMode.Intersect); float scrollY; fxWinScroll.TryGetValue(f, out scrollY); scrollY = Math.Max(0, Math.Min(scrollY, Math.Max(0, edH - body.Height)));
        int reg0 = regs.Count; editorBare = true; try { PaintEditor(g, f, Rc(body.X, body.Y - scrollY, body.Width, edH), w.Track); } finally { editorBare = false; } g.Restore(gs);
        for (int i = regs.Count - 1; i >= reg0; i--) { var rr = regs[i]; if (!rr.R.IntersectsWith(body)) regs.RemoveAt(i); else rr.R = RectangleF.Intersect(rr.R, body); }   // controls scrolled out of the window can not be clicked through it
        if (edH > body.Height + 1) { float sMax = edH - body.Height; wr.OnWheel = d => { float s0; fxWinScroll.TryGetValue(f, out s0); fxWinScroll[f] = Math.Max(0, Math.Min(sMax, s0 - d * 40)); }; }
        // resize from the bottom-right corner
        var grip = Rc(r.Right - 18, r.Bottom - 18, 16, 16); using (var pen = new Pen(Color.FromArgb(180, acc), 1.5f)) { g.DrawLine(pen, grip.Right - 3, grip.Y + 3, grip.X + 3, grip.Bottom - 3); g.DrawLine(pen, grip.Right - 3, grip.Y + 9, grip.X + 9, grip.Bottom - 3); }
        var gr = Reg(grip, () => { fxWinDrag = w; fxWinResize = true; }); gr.OnDrag = pt => { if (fxWinDrag == w && fxWinResize) w.R = new RectangleF(w.R.X, w.R.Y, pt.X - w.R.X + 4, pt.Y - w.R.Y + 4); }; gr.OnUp = () => { fxWinDrag = null; fxWinResize = false; }; Hint2(grip, "Drag to resize the window");
    }
    readonly Dictionary<Fx, float> fxWinScroll = new Dictionary<Fx, float>();
    // the plugins whose settings are drawn by the general editor (so a window shows them as knobs)
    static bool KnobEditor(Fx f) { return !(f is InstrumentFx) && !PxPlugins.HasWindow(f) && !(f is ParametricEq); }
    internal static List<KeyValuePair<string, string>> FxPresetList(Fx f) { var l = new List<KeyValuePair<string, string>>(); try { l.AddRange(Presets.User(f.Type)); l.AddRange(Presets.For(f.Type)); } catch { } return l; }
    internal void StepFxPreset(Fx f, int d)
    {
        var l = FxPresetList(f); if (l.Count == 0) return; int cur = l.FindIndex(kv => kv.Key == f.CurPreset); int nxt = cur < 0 ? (d > 0 ? 0 : l.Count - 1) : ((cur + d) % l.Count + l.Count) % l.Count;
        lock (eng.Sync) { Presets.Apply(f, l[nxt].Value); f.CurPreset = l[nxt].Key; f.CurPresetData = f.Serialize(); }
    }
    // the family of a plugin, for its window title and the effect browser
    internal static string FxCategory(Fx f) { var info = FxRegistry.All.FirstOrDefault(i => i.Type == f.Type); return info != null && !string.IsNullOrEmpty(info.Category) ? info.Category : "Effect"; }
    // tidy the windows: a neat cascade from the top-left of the timeline
    internal void ArrangeFxWindows() { int k = 0; foreach (var w in fxWins.OrderBy(x => x.Seq)) { w.R = new RectangleF(HeadW + 20 + k * 30, ToolH + TransH + 30 + k * 30, w.R.Width, w.R.Height); k++; } }
}
