using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  THE BOTTOM INSPECTOR: one panel docked under the arrangement for the track (or bus) you picked. Tabs: CHANNEL (the channel strip:
//  fader, meter, pan, trim, phase, mono, output, sends, inserts), PLUGINS (the chain in processing order: open, bypass, move, copy, remove,
//  add), BUSES (make, rename, delete and route buses, with their levels and plugins) and CLIP / MIDI (the picked audio clip or MIDI part).
//  Everything here changes the same track settings the rest of the studio uses, so the track headers, the mixer and this panel always
//  agree. Drag its top edge to resize it; HIDE gives the arrangement its space back. The arrangement stays editable above it.
// =====================================================================================================
partial class StudioForm
{
    internal bool insOpen; internal int insTab; internal Track insBus; float insScrollX; bool insDragging;
    internal float InsH { get { float mx = Math.Max(180, ClientSize.Height * (insTab == 4 ? 0.74f : 0.6f)); return Math.Max(170, Math.Min(mx, Prefs.InsH)); } }
    internal bool InsShown { get { return insOpen && tab == 0 && !flexOpen; } }
    // the bottom of the arrangement (the inspector takes the space under it)
    internal float MixH { get { return ClientSize.Height - (InsShown ? InsH : 0); } }
    internal void OpenInspector(int tabIndex) { insOpen = true; insTab = Math.Max(0, Math.Min(4, tabIndex)); tab = 0;   /* (2 = PRESETS) */ if (insTab == 1) EnsureConsoleHeight(); Prefs.InsOpen = true; if (!Prefs.PanelHidden && !soundsOpen) { Prefs.PanelHidden = true; panelAt = DateTime.Now; } Prefs.Changed(); Invalidate(); }   // one place for plugins: the right-hand plugin panel is tucked away (its tab brings it back)
    internal void CloseInspector() { insOpen = false; Prefs.InsOpen = false; Prefs.Changed(); Invalidate(); }

    // which track the panel shows: the bus picked in BUSES, the bus being edited, or the picked track
    internal Track InsTarget
    {
        get
        {
            if (insBus != null && !eng.Tracks.Contains(insBus)) insBus = null;
            if (insBus != null) return insBus; if (busEdit != null && eng.Tracks.Contains(busEdit)) return busEdit;
            if (eng.Tracks.Count == 0) return null; return eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))];
        }
    }
    // the clip the CLIP tab works on: the piece you clicked, or the picked track's own clip
    internal Track InsClip { get { if (toolsTrack != null && eng.Tracks.Contains(toolsTrack) && toolsTrack.Clip != null) return toolsTrack; var t = InsTarget; return t != null && t.Clip != null && !t.IsBus ? t : null; } }

    void PaintInspector(Graphics g)
    {
        if (!InsShown) return; float x = 0, y = MixH, w = ClientSize.Width - PanelW, h = ClientSize.Height - y; var P = Rc(x, y, w, h);
        using (var b = new SolidBrush(TM.C(255, 14, 7, 30))) g.FillRectangle(b, P); using (var pen = new Pen(Color.FromArgb(200, Accent), 1.5f)) g.DrawLine(pen, x, y, x + w, y);
        Reg(P, () => { }).Overlay = true; var wheel = new HitReg { R = P, OnWheel = d => insScrollX = Math.Max(0, insScrollX - d * 60) }; regs.Add(wheel);
        // the divider: drag to resize (the arrangement gets the rest)
        var div = Rc(x, y - 3, w, 8); var dr = Reg(div, () => insDragging = true); dr.OnDrag = pt => { Prefs.InsH = (int)Math.Max(170, Math.Min(ClientSize.Height * (insTab == 4 ? 0.74f : 0.6f), ClientSize.Height - pt.Y)); }; dr.OnUp = () => { insDragging = false; Prefs.Changed(); };
        if (Hot(div) || insDragging) { using (var pen = new Pen(NeonCyan, 3f)) g.DrawLine(pen, x, y, x + w, y); Cursor = Cursors.SizeNS; } Hint2(div, "Drag to make the inspector taller or shorter");
        // header: tabs, what is shown, hide
        var t = InsTarget; var clipT = InsClip; bool midi = t != null && t.Instrument; string fourth = midi ? "MIDI" : "CLIP";
        string[] tabs = { "CHANNEL", "MIXER", "PRESETS", fourth, "PIANO ROLL", "BEAT LAB" }; float tx = x + 10;   // BEAT LAB: the drum machine, in the piano roll's place
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].Length == 0) continue; int k = i; float cw = i == 1 ? 118 : 100; var r = Rc(tx, y + 6, cw, 24); bool on = i == 5 ? insTab == 4 && blOn : (i == 4 ? insTab == 4 && !blOn : insTab == i);
            Chip(g, r, tabs[i], on, () => { if (k == 5) { OpenBeatLab(); return; } if (blOn) { blOn = false; BlPlay(false); } if (k == 1) EnsureConsoleHeight(); insTab = k; insScrollX = 0; }); tx += cw + 4;
            if (i == 5) Hint2(r, "The Beat Lab: rows of steps for your drums (tap a row's name to hear it, TRACK to use one of your own tracks)");
        }
        if (t != null) { var nm = Rc(tx + 10, y + 6, Math.Max(60, w - tx - 200), 24); using (var cb = new SolidBrush(Neon(t.Color))) g.FillEllipse(cb, nm.X, nm.Y + 6, 12, 12); Txt(g, (t.IsBus ? "BUS: " : (t.Instrument ? "MIDI: " : "TRACK: ")) + t.Name, 12, Ink, Rc(nm.X + 18, nm.Y, nm.Width - 18, 24), StringAlignment.Near, true); }
        var hide = Rc(x + w - 92, y + 6, 82, 24); Btn(g, hide, "HIDE", 0, () => CloseInspector(), 10); Hint2(hide, "Hide the inspector (the arrangement gets its space back)");
        float hx = hide.X; if (fxWins.Count > 1) { var ar = Rc(hide.X - 118, y + 6, 112, 24); Btn(g, ar, "ARRANGE WINDOWS", 0, () => ArrangeFxWindows(), 9); hx = ar.X; }
        if (t != null && (insTab == 1 || insTab == 0)) { bool any = t.Fx.Exists(f => !(f is InstrumentFx)); var rmb = Rc(hx - 156, y + 6, 150, 24); Btn(g, rmb, "REMOVE ALL PLUGINS", any ? 3 : 0, () => { if (!any) { Toast(t.Name + " has no plugins to remove."); return; } if (!AskYes("Are you sure? Every plugin on " + t.Name + " will be removed (Ctrl+Z brings them back).", "Remove all plugins")) return; ChainApply(t, "All plugins removed", () => RemoveAllPlugins(t)); selFx = -1; }, 9); Hint2(rmb, "Take every plugin off " + t.Name + " (it asks first; Ctrl+Z undoes it)"); }
        bool bar = insTab != 4 && insMaxX > 1; var C = Rc(x + 8, y + 36, w - 16, h - 42 - (bar ? 18 : 0)); if (t == null && insTab != 2 && insTab != 4) { Txt(g, "ADD A TRACK, THEN PICK IT TO SEE ITS CHANNEL, PLUGINS AND CLIPS HERE", 12, DimC, C, StringAlignment.Center, false); return; }
        var gs = g.Save(); g.SetClip(C, System.Drawing.Drawing2D.CombineMode.Intersect);
        if (insTab == 4) PaintPianoRoll(g, C); else if (insTab == 0) PaintInsChannel(g, C, t); else if (insTab == 1) PaintInsPlugins(g, C, t); else if (insTab == 2) { var look = BeginBrand(); try { PaintInsPresets(g, C, t); } finally { EndBrand(look); } } else if (midi) PaintInsMidi(g, C, t); else PaintInsClip(g, C, clipT);
        g.Restore(gs);
        if (bar && insMaxX > 1) PaintInsScrollBar(g, Rc(C.X, C.Bottom + 4, C.Width, 12));
    }
    // the scroll bar along the bottom: drag the thumb, or click the bar to jump there
    float insBarGrab;
    void PaintInsScrollBar(Graphics g, RectangleF r)
    {
        float total = r.Width + insMaxX, tw = Math.Max(40, r.Width * r.Width / total), tx = r.X + (r.Width - tw) * Math.Min(1, insScrollX / insMaxX); var thumb = Rc(tx, r.Y, tw, r.Height);
        FillRR(g, TM.C(200, 8, 4, 18), r, 6); FillRR(g, Color.FromArgb(Hot(thumb) ? 255 : 200, NeonCyan), thumb, 6);
        Action<float> setAt = px => { insScrollX = Math.Max(0, Math.Min(insMaxX, (px - insBarGrab - r.X) / Math.Max(1, r.Width - tw) * insMaxX)); Invalidate(); };
        var tr = Reg(r, () => { insBarGrab = tw / 2; setAt(MousePos.X); }); tr.OnDrag = pt => setAt(pt.X); tr.ScrollBar = true;
        var th = Reg(thumb, () => { insBarGrab = MousePos.X - tx; }); th.OnDrag = pt => setAt(pt.X); th.ScrollBar = true; Hint2(r, "Scroll along the strips (or use the mouse wheel)");
    }

    // a neon box with a title: every section of the inspector sits in one
    RectangleF InsBox(Graphics g, RectangleF r, string title, Color col)
    {
        FillRR(g, TM.C(120, 26, 14, 50), r, 10); Glow(g, col, r, 10, 0.35f, 2); StrokeRR(g, Color.FromArgb(200, col), r, 10, 1.2f);
        Txt(g, title, 10, col, Rc(r.X + 10, r.Y + 4, r.Width - 20, 16), StringAlignment.Near, true); return Rc(r.X + 10, r.Y + 22, r.Width - 20, r.Height - 28);
    }
    static float DbToFrac(float db) { return (float)((Math.Max(-60, Math.Min(6, db)) + 60) / 66.0); }
    static float FracToDb(float f) { return (float)(Math.Max(0, Math.Min(1, f)) * 66 - 60); }
    static float MeterFrac(float lin) { if (lin <= 1e-5f) return 0; double db = 20 * Math.Log10(lin); return (float)Math.Max(0, Math.Min(1, (db + 60) / 66)); }

    // a vertical fader (dB) with its readout; double-click puts it back to 0 dB
    void VFader(Graphics g, RectangleF r, float db, Action<float> set, Color col)
    {
        float f = DbToFrac(db), cx = r.X + r.Width / 2; var groove = Rc(cx - 3.5f, r.Y, 7, r.Height);
        FillRR(g, TM.C(235, 6, 2, 16), groove, 3.5f); StrokeRR(g, Color.FromArgb(110, NeonViolet), groove, 3.5f, 1f);
        for (int k = 0; k <= 6; k++) { float ty = r.Y + r.Height * k / 6f; using (var pen = new Pen(Color.FromArgb(70, NeonCyan), 1f)) { g.DrawLine(pen, r.X, ty, cx - 6, ty); g.DrawLine(pen, cx + 6, ty, r.Right, ty); } }   // the scale marks
        float zy = r.Bottom - r.Height * DbToFrac(0); using (var pen = new Pen(Color.FromArgb(150, Ink))) g.DrawLine(pen, r.X, zy, r.Right, zy);
        float ky = r.Bottom - r.Height * f; var fill = Rc(cx - 3.5f, ky, 7, r.Bottom - ky);
        if (fill.Height > 1) { using (var gl = new SolidBrush(Color.FromArgb(50, col))) g.FillRectangle(gl, fill.X - 3, fill.Y, fill.Width + 6, fill.Height); using (var p = RR(fill, 3.5f)) using (var br = new LinearGradientBrush(new RectangleF(fill.X, r.Y, fill.Width, r.Height), NeonPink, col, 90f)) g.FillPath(br, p); }
        var knob = Rc(r.X - 2, ky - 9, r.Width + 4, 18); using (var gl = new SolidBrush(Color.FromArgb(80, col))) g.FillEllipse(gl, knob.X - 6, knob.Y - 6, knob.Width + 12, knob.Height + 12);   // the glowing cap
        using (var p = RR(knob, 4)) using (var br = new LinearGradientBrush(knob, Color.White, TM.C(255, 175, 150, 250), 90f)) g.FillPath(br, p); StrokeRR(g, col, knob, 4, 1.6f);
        using (var pen = new Pen(Color.FromArgb(220, col), 1.6f)) g.DrawLine(pen, knob.X + 3, knob.Y + knob.Height / 2, knob.Right - 3, knob.Y + knob.Height / 2);
        var rg = Reg(Rc(r.X - 4, r.Y - 8, r.Width + 8, r.Height + 16), () => set(FracToDb((r.Bottom - MousePos.Y) / r.Height))); rg.OnDrag = pt => set(FracToDb((r.Bottom - pt.Y) / r.Height)); rg.OnDouble = () => set(0);
    }
    // a level meter (peak of each side) with the clip light (click it to clear)
    void Meter(Graphics g, RectangleF r, Track t)
    {
        float l = MeterFrac(t.PeakL), rr = MeterFrac(t.PeakR); float bw = (r.Width - 3) / 2;
        for (int s2 = 0; s2 < 2; s2++)
        {
            var bar = Rc(r.X + s2 * (bw + 3), r.Y + 12, bw, r.Height - 12); FillRR(g, TM.C(235, 4, 2, 12), bar, 2); float v = s2 == 0 ? l : rr; float hh = bar.Height * v;
            if (hh > 1) { var lit = Rc(bar.X, bar.Bottom - hh, bar.Width, hh); using (var br = new LinearGradientBrush(bar, Color.Red, Color.Lime, 90f)) { br.InterpolationColors = new ColorBlend(5) { Colors = new[] { Color.FromArgb(255, 255, 40, 70), Color.FromArgb(255, 255, 200, 40), Color.FromArgb(255, 80, 255, 120), Color.FromArgb(255, 60, 200, 255), Color.FromArgb(255, 140, 80, 255) }, Positions = new[] { 0f, 0.15f, 0.4f, 0.7f, 1f } }; g.FillRectangle(br, lit); }
              for (float yy = bar.Bottom - 3; yy > lit.Y; yy -= 3) using (var pen = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawLine(pen, bar.X, yy, bar.Right, yy); }   // LED segments
        }
        var led = Rc(r.X, r.Y, r.Width, 9); FillRR(g, t.Clipped ? TM.C(255, 255, 60, 80) : TM.C(160, 50, 30, 60), led, 3); Reg(led, () => t.Clipped = false); Hint2(led, t.Clipped ? "It went over 0 dB (clipping). Click to clear." : "Lights up red if the track goes over 0 dB");
    }

    // ---------------- CHANNEL ----------------
    void PaintInsChannel(Graphics g, RectangleF C, Track t)
    {
        float x = C.X - insScrollX, y = C.Y, h = C.Height; Color col = Neon(t.Color);
        // TRACK: name, colour, mute / solo / on, mono, phase
        var a = InsBox(g, Rc(x, y, 230, h), t.IsBus ? "BUS" : (t.Instrument ? "INSTRUMENT TRACK" : "AUDIO TRACK"), col);
        Btn(g, Rc(a.X, a.Y, a.Width - 34, 24), t.Name, 0, () => RenameTrack(t), 10); var sw = Rc(a.Right - 28, a.Y, 28, 24); using (var cb = new SolidBrush(col)) using (var p = RR(sw, 5)) g.FillPath(cb, p); Reg(sw, () => TrackColourMenu(t)); Hint2(sw, "Change the track colour");
        float by = a.Y + 30; Btn(g, Rc(a.X, by, 46, 24), "M", t.Mute ? 3 : 0, () => { t.Mute = !t.Mute; }, 11); Btn(g, Rc(a.X + 50, by, 46, 24), "S", t.Solo ? 2 : 0, () => { t.Solo = !t.Solo; }, 11); Btn(g, Rc(a.X + 100, by, a.Width - 100, 24), t.Off ? "OFF" : "ON", t.Off ? 0 : 4, () => { t.Off = !t.Off; }, 10);
        by += 30; if (!t.IsBus) { Btn(g, Rc(a.X, by, (a.Width - 4) / 2, 24), t.Mono ? "MONO" : "STEREO", t.Mono ? 4 : 0, () => { PushUndo(); t.Mono = !t.Mono; }, 10); Btn(g, Rc(a.X + (a.Width + 4) / 2, by, (a.Width - 4) / 2, 24), "PHASE Ø", t.Invert ? 4 : 0, () => { PushUndo(); t.Invert = !t.Invert; }, 10); Hint2(Rc(a.X + (a.Width + 4) / 2, by, (a.Width - 4) / 2, 24), "Flip the phase (polarity) of this track"); by += 30; }
        int lanes = t.Auto.Count; Txt(g, lanes == 0 ? "AUTOMATION: NONE" : "AUTOMATION: " + lanes + " LANE" + (lanes == 1 ? "" : "S"), 9.5f, lanes > 0 ? NeonCyan : DimC, Rc(a.X, by, a.Width, 16), StringAlignment.Near, lanes > 0);
        x += 238;
        // FADER and METER
        var fb = InsBox(g, Rc(x, y, 130, h), "VOLUME", col); float fh = fb.Height - 22;
        VFader(g, Rc(fb.X + 6, fb.Y + 4, 30, fh), t.VolDb, v => t.VolDb = (float)Math.Round(v, 1), col); Meter(g, Rc(fb.X + 50, fb.Y + 4, 26, fh), t);
        Txt(g, (t.VolDb > 0 ? "+" : "") + t.VolDb.ToString("0.0") + " dB", 10, Ink, Rc(fb.X, fb.Bottom - 16, fb.Width, 16), StringAlignment.Center, true);
        float pk = Math.Max(t.PeakL, t.PeakR); Txt(g, pk > 1e-5f ? (20 * Math.Log10(pk)).ToString("0.0") : "-inf", 9, pk > 1 ? Warn : DimC, Rc(fb.X + 80, fb.Y + 4, fb.Width - 80, 16), StringAlignment.Near, false);
        x += 138;
        // PAN and TRIM
        var pb = InsBox(g, Rc(x, y, 210, h), t.IsBus ? "PAN" : "PAN · TRIM", col);
        Txt(g, "PAN  " + (Math.Abs(t.Pan) < 0.01f ? "CENTRE" : (t.Pan < 0 ? "L " : "R ") + Math.Abs(t.Pan * 100).ToString("0")), 10, Ink, Rc(pb.X, pb.Y, pb.Width, 16), StringAlignment.Near, true);
        var ps = Slider(g, Rc(pb.X, pb.Y + 18, pb.Width, 18), (t.Pan + 1) / 2, v => t.Pan = (float)Math.Round(v * 2 - 1, 2), col); ps.OnDouble = () => t.Pan = 0;
        if (!t.IsBus) { Txt(g, "TRIM  " + (t.TrimDb > 0 ? "+" : "") + t.TrimDb.ToString("0.0") + " dB", 10, Ink, Rc(pb.X, pb.Y + 46, pb.Width, 16), StringAlignment.Near, true); var tsl = Slider(g, Rc(pb.X, pb.Y + 64, pb.Width, 18), (t.TrimDb + 24) / 48, v => t.TrimDb = (float)Math.Round(v * 48 - 24, 1), col); tsl.OnDouble = () => t.TrimDb = 0; Hint2(Rc(pb.X, pb.Y + 46, pb.Width, 36), "Input trim: the level going into the plugins (double-click for 0 dB)"); }
        x += 218;
        // OUTPUT
        var ob = InsBox(g, Rc(x, y, 200, h), "INPUT · OUTPUT", col);
        string input = t.IsBus ? "Tracks sent here" : (t.Instrument ? "Instrument: " + InstrumentName(t) : (t.Clip != null ? "Audio: " + System.IO.Path.GetFileName(t.Clip.Path.Length > 0 ? t.Clip.Path : t.Clip.Name) : "Empty (record or drop audio)"));
        Txt(g, "IN", 9, DimC, Rc(ob.X, ob.Y, 30, 16), StringAlignment.Near, true); Txt(g, input, 9.5f, Ink, Rc(ob.X + 28, ob.Y, ob.Width - 28, 16), StringAlignment.Near, false);
        Txt(g, "OUT", 9, DimC, Rc(ob.X, ob.Y + 24, 30, 16), StringAlignment.Near, true);
        bool badOut = t.Out != null && (!eng.Tracks.Contains(t.Out) || !t.Out.IsBus);
        if (t.IsBus) Txt(g, "Master (buses go to the master)", 9.5f, Ink, Rc(ob.X + 28, ob.Y + 24, ob.Width - 28, 16), StringAlignment.Near, false);
        else Btn(g, Rc(ob.X + 30, ob.Y + 20, ob.Width - 30, 24), badOut ? "MISSING BUS: FIX" : (t.Out != null ? t.Out.Name : "Master"), badOut ? 3 : 0, () => OutputMenu(t), 10);
        Txt(g, t.Mono ? "MONO" : "STEREO", 9, DimC, Rc(ob.X, ob.Y + 52, ob.Width, 16), StringAlignment.Near, true);
        x += 208;
        // SENDS (tracks) or WHAT FEEDS IT (buses)
        float sw2 = Math.Max(260, C.Right - x - 238 + insScrollX); var sbx = InsBox(g, Rc(x, y, sw2, h), t.IsBus ? "FEEDING THIS BUS" : "SENDS", NeonPink);
        if (t.IsBus) PaintBusFeeds(g, sbx, t); else PaintSends(g, sbx, t);
        x += sw2 + 8;
        // INSERTS
        var ib = InsBox(g, Rc(x, y, 230, h), "PLUGINS (IN ORDER)", NeonViolet); Fx[] fxs; lock (eng.Sync) fxs = t.Fx.ToArray(); float iy = ib.Y;
        for (int i = 0; i < fxs.Length && iy + 22 <= ib.Bottom - 26; i++) { var f = fxs[i]; var row = Rc(ib.X, iy, ib.Width, 22); bool open = FxWindowOf(f) != null; FillRR(g, open ? TM.C(160, 60, 30, 110) : TM.C(110, 40, 22, 74), row, 5); var dot = Rc(row.X + 4, row.Y + 4, 14, 14); using (var b2 = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b2, dot); Reg(dot, () => f.Bypass = !f.Bypass); Txt(g, f.Title, 10, f.Bypass ? DimC : Ink, Rc(row.X + 22, row.Y, row.Width - 26, 22), StringAlignment.Near, false); var rr = Reg(Rc(row.X + 20, row.Y, row.Width - 20, 22), () => OpenFxWindow(t, f)); Hint2(row, "Open " + f.Title + " in its own window. " + FxHelp.About(f)); iy += 25; }
        if (fxs.Length == 0) Txt(g, "NO PLUGINS YET", 10, DimC, Rc(ib.X, iy, ib.Width, 20), StringAlignment.Near, false);
        Btn(g, Rc(ib.X, ib.Bottom - 24, ib.Width, 24), "+ ADD EFFECT", 1, () => InsAddEffect(t), 10);
        insMaxX = Math.Max(0, (x + 238 + insScrollX) - C.Right); insScrollX = Math.Min(insScrollX, insMaxX);
    }
    float insMaxX;
    string InstrumentName(Track t) { foreach (var f in t.Fx) if (f is InstrumentFx) return f.Title; return "none"; }
    void InsAddEffect(Track t) { int ti = eng.Tracks.IndexOf(t); if (ti < 0) return; insAddFor = t; OpenBrowser(false, ti); }
    internal Track insAddFor;   // the track an effect from the browser was asked for in the inspector: its window opens when it is added

    void PaintSends(Graphics g, RectangleF r, Track t)
    {
        var sends = t.Sends; float y = r.Y; var buses = eng.Tracks.Where(b => b.IsBus).ToList();
        for (int i = 0; i < sends.Length && y + 26 <= r.Bottom - 28; i++)
        {
            var sd = sends[i]; bool bad = sd.Bus == null || !eng.Tracks.Contains(sd.Bus) || !sd.Bus.IsBus; int si = i;
            Btn(g, Rc(r.X, y, 110, 24), bad ? "MISSING BUS" : sd.Bus.Name, bad ? 3 : 0, () => SendBusMenu(t, si), 9.5f);
            var sl = Slider(g, Rc(r.X + 116, y + 3, Math.Max(40, r.Width - 116 - 150), 18), DbToFrac(sd.LevelDb), v => { sd.LevelDb = (float)Math.Round(FracToDb(v), 1); }, NeonPink); sl.OnDouble = () => sd.LevelDb = 0;
            Txt(g, (sd.LevelDb > 0 ? "+" : "") + sd.LevelDb.ToString("0.0") + " dB", 9.5f, Ink, Rc(r.Right - 146, y, 58, 24), StringAlignment.Far, false);
            Btn(g, Rc(r.Right - 84, y, 50, 24), sd.Pre ? "PRE" : "POST", sd.Pre ? 4 : 0, () => { PushUndo(); sd.Pre = !sd.Pre; }, 9.5f); Hint2(Rc(r.Right - 84, y, 50, 24), sd.Pre ? "Pre-fader: the send ignores the track's fader" : "Post-fader: the send follows the track's fader");
            Btn(g, Rc(r.Right - 30, y, 28, 24), "x", 3, () => { PushUndo(); var l = new List<Send>(t.Sends); l.RemoveAt(si); t.Sends = l.ToArray(); }, 10);
            y += 28;
        }
        if (sends.Length == 0) Txt(g, "NO SENDS. A SEND COPIES THIS TRACK TO A BUS (FOR EXAMPLE A SHARED REVERB) AT ITS OWN LEVEL.", 9.5f, DimC, Rc(r.X, y, r.Width, 32), StringAlignment.Near, false);
        Btn(g, Rc(r.X, r.Bottom - 24, 120, 24), "+ ADD SEND", 1, () => AddSendMenu(t), 10);
    }
    void PaintBusFeeds(Graphics g, RectangleF r, Track bus)
    {
        float y = r.Y; int n = 0;
        foreach (var t in eng.Tracks)
        {
            if (t.IsBus) continue; bool outHere = t.Out == bus; Send sd = Array.Find(t.Sends, s => s.Bus == bus); if (!outHere && sd == null) continue; n++; if (y + 22 > r.Bottom) break;
            Txt(g, t.Name, 10, Ink, Rc(r.X, y, r.Width * 0.45f, 20), StringAlignment.Near, false); Txt(g, outHere ? "OUTPUT" : "SEND " + (sd.Pre ? "PRE " : "") + sd.LevelDb.ToString("0.0") + " dB", 9.5f, outHere ? NeonCyan : NeonPink, Rc(r.X + r.Width * 0.45f, y, r.Width * 0.55f, 20), StringAlignment.Near, true); y += 22;
        }
        if (n == 0) Txt(g, "NOTHING IS SENT HERE YET: PICK THIS BUS AS A TRACK'S OUTPUT, OR ADD A SEND ON A TRACK (BUSES TAB).", 9.5f, DimC, Rc(r.X, y, r.Width, 32), StringAlignment.Near, false);
    }
    void OutputMenu(Track t)
    {
        var m = Win.NewMenu(); var ms = new ToolStripMenuItem("Master") { Checked = t.Out == null }; ms.Click += (a, b) => { PushUndo(); t.Out = null; }; m.Items.Add(ms);
        foreach (var b0 in eng.Tracks.Where(q => q.IsBus)) { var bb = b0; var it = new ToolStripMenuItem(bb.Name) { Checked = t.Out == bb }; it.Click += (a, b) => { PushUndo(); t.Out = bb; }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("New bus and send this track to it").Click += (a, b) => { var nb = AddBus(); PushUndo(); t.Out = nb; };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void AddSendMenu(Track t)
    {
        var m = Win.NewMenu(); int n = 0;
        foreach (var b0 in eng.Tracks.Where(q => q.IsBus)) { var bb = b0; if (Array.Exists(t.Sends, s => s.Bus == bb)) continue; n++; m.Items.Add("Send to " + bb.Name).Click += (a, b) => AddSend(t, bb); }
        if (n > 0) m.Items.Add(new ToolStripSeparator()); m.Items.Add("Send to a new bus").Click += (a, b) => { var nb = AddBus(); AddSend(t, nb); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    internal void AddSend(Track t, Track bus)
    {
        if (t == null || bus == null || !bus.IsBus || t.IsBus || t == bus || Array.Exists(t.Sends, s => s.Bus == bus)) return;   // a bus never sends (no feedback loops), and one send per bus
        PushUndo(); var l = new List<Send>(t.Sends); l.Add(new Send { Bus = bus, LevelDb = -6f }); t.Sends = l.ToArray();
    }
    void SendBusMenu(Track t, int si)
    {
        var m = Win.NewMenu(); foreach (var b0 in eng.Tracks.Where(q => q.IsBus)) { var bb = b0; if (Array.Exists(t.Sends, s => s.Bus == bb && s != t.Sends[si])) continue; m.Items.Add(bb.Name).Click += (a, b) => { PushUndo(); var l = Array.ConvertAll(t.Sends, s => s.Copy()); l[si].Bus = bb; t.Sends = l; }; }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void TrackColourMenu(Track t)
    {
        var m = Win.NewMenu(); int[] cols = { unchecked((int)0xFF7C5CFF), unchecked((int)0xFFE05CC8), unchecked((int)0xFF5CC8E0), unchecked((int)0xFF16C784), unchecked((int)0xFFFFB84D), unchecked((int)0xFFFFD04D), unchecked((int)0xFFFF5C5C), unchecked((int)0xFF4D8BFF), unchecked((int)0xFFB0FF4D), unchecked((int)0xFFFFFFFF) };
        string[] names = { "Violet", "Pink", "Cyan", "Green", "Orange", "Gold", "Red", "Blue", "Lime", "White" };
        for (int i = 0; i < cols.Length; i++) { int c = cols[i]; var it = new ToolStripMenuItem(names[i]) { Checked = t.Color == c, ForeColor = Color.FromArgb(c) }; it.Click += (a, b) => { PushUndo(); t.Color = c; }; m.Items.Add(it); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- ALL PLUGINS: the mixing console ----------------
    // Every track, then every bus, side by side like a desk: each strip has its name, its plugin slots (a click opens that
    // plugin in its own small window, the dot bypasses it, a right-click moves, copies or removes it, the empty slot adds one), its sends,
    // its output, pan, the fader with its meter, and mute / solo. The rows line up across the strips. Scroll sideways with the wheel.
    const float StripW = 118, SlotH = 24, SlotGap = 3;
    void EnsureConsoleHeight() { if (Prefs.InsH < 360) { Prefs.InsH = 360; Prefs.Changed(); } }   // (the desk needs room for its slots and faders; InsH still caps it at 60% of the window)
    void PaintInsPlugins(Graphics g, RectangleF C, Track picked)
    {
        var strips = new List<Track>(); foreach (var q in eng.Tracks) if (!q.IsBus) strips.Add(q); foreach (var q in eng.Tracks) if (q.IsBus) strips.Add(q);
        int maxFx = 0, maxSends = 0; foreach (var q in strips) { lock (eng.Sync) maxFx = Math.Max(maxFx, q.Fx.Count); if (!q.IsBus) maxSends = Math.Max(maxSends, q.Sends.Length); }
        // the shared rows: name, slots, sends, output, pan, fader, mute / solo (the slots and the fader share what is left)
        int sendRows = Math.Min(3, maxSends + 1); float fixedH = 26 + 6 + sendRows * (SlotH + SlotGap) + 6 + 26 + 26 + 20 + 36, faderMin = 70;   /* (a row for PRESETS under the output) */
        int fit = Math.Max(1, (int)((C.Height - fixedH - faderMin) / (SlotH + SlotGap))); int slots = Math.Max(1, Math.Min(fit, Math.Max(3, maxFx + 1)));
        float faderH = Math.Max(24, C.Height - fixedH - slots * (SlotH + SlotGap));
        float x = C.X - insScrollX;
        foreach (var t in strips) { if (x + StripW >= C.X - 4 && x <= C.Right + 4) PaintStrip(g, Rc(x, C.Y, StripW, C.Height), t, t == picked, slots, sendRows, faderH); x += StripW + 6; }
        // + NEW BUS: a bus is made here and gets its own strip (with its effects) next to the tracks
        if (x + 84 >= C.X - 4 && x <= C.Right + 4) { var nb = Rc(x, C.Y, 84, C.Height); using (var pen = new Pen(Color.FromArgb(120, NeonPink), 1.4f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) using (var p = RR(nb, 9)) g.DrawPath(pen, p);
          var nbb = Rc(nb.X + 6, nb.Y + 8, nb.Width - 12, 40); Btn(g, nbb, "+ NEW BUS", 2, () => { var b2 = AddBus(); if (b2 != null) { insBus = b2; } }, 9); Hint2(nbb, "Make a bus: send tracks into it with + BUS on their strips, and give it its own effects"); }
        x += 90;
        if (strips.Count == 0) Txt(g, "ADD A TRACK: EVERY TRACK GETS ITS OWN STRIP HERE", 11, DimC, Rc(C.X + StripW + 20, C.Y, C.Width - StripW - 30, 40), StringAlignment.Near, false);
        insMaxX = Math.Max(0, (x + insScrollX) - C.Right); insScrollX = Math.Min(insScrollX, insMaxX);
    }
    // the PRESETS tab: every preset option of the track as a big button (the same as the old PRESETS menu); options with a list open it
    ContextMenuStrip insPresetMenu; string insPresetKey = "";
    void PaintInsPresets(Graphics g, RectangleF C, Track t)
    {
        if (t == null) return; string key = eng.Tracks.IndexOf(t) + "|" + t.Name + "|" + t.Fx.Count + "|" + presetCopy.Count + "|" + allCopy.Count + "|" + Presets.UserChains().Count();
        if (insPresetMenu == null || key != insPresetKey) { if (insPresetMenu != null) insPresetMenu.Dispose(); insPresetMenu = BuildChainMenu(t); insPresetKey = key; }
        using (var b = new SolidBrush(TM.C(255, 18, 8, 38))) g.FillRectangle(b, C);
        Txt(g, (t.IsBus ? "BUS PRESETS FOR " : "PRESETS FOR ") + t.Name.ToUpperInvariant(), 13, Ink, Rc(C.X + 6, C.Y + 2, C.Width - 12, 22), StringAlignment.Near, true);
        var items = insPresetMenu.Items.OfType<ToolStripMenuItem>().ToList(); float bw = 270, bh = 44, gap = 8; int cols = Math.Max(1, (int)((C.Width + gap) / (bw + gap))); float bx0 = C.X + 6, by0 = C.Y + 30;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; var r = Rc(bx0 + (i % cols) * (bw + gap), by0 + (i / cols) * (bh + gap), bw, bh); if (r.Bottom > C.Bottom) break; bool has = it.DropDownItems.Count > 0;
            string full = it.Text.Replace("...", ""); int pi = full.IndexOf(" ("); if (pi < 0) pi = full.IndexOf(": "); string title = (pi > 0 ? full.Substring(0, pi) : full).ToUpperInvariant() + (has ? "  >" : ""), sub = pi > 0 ? full.Substring(pi).Trim(' ', '(', ')', ':') : ""; var ii = it;
            Btn(g, r, "", !it.Enabled ? 0 : (i < 2 ? 2 : 1), () => { if (!ii.Enabled) return; if (ii.DropDownItems.Count > 0) { var m = Win.NewMenu(); foreach (ToolStripItem d in ii.DropDownItems.OfType<ToolStripItem>().ToList()) m.Items.Add(d); m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); insPresetKey = ""; } else { ii.PerformClick(); insPresetKey = ""; } }, 9.5f);
            Txt(g, title, 10.5f, Ink, Rc(r.X + 10, r.Y + (sub.Length > 0 ? 4 : 0), r.Width - 20, sub.Length > 0 ? 18 : r.Height), StringAlignment.Near, true); if (sub.Length > 0) Txt(g, sub, 8.5f, DimC, Rc(r.X + 10, r.Y + 22, r.Width - 20, 16), StringAlignment.Near, false);
            if (!it.Enabled) using (var dim = new SolidBrush(Color.FromArgb(120, 10, 4, 20))) g.FillRectangle(dim, r);
        }
    }
    // one strip in its own neon box (the picked one glows in your accent)
    void PaintStrip(Graphics g, RectangleF r, Track t, bool sel, int slots, int sendRows, float faderH)
    {
        Color col = Neon(t.Color); Color edge = sel ? UiTokens.Sel(NeonCyan) : col;
        FillRR(g, sel ? TM.C(170, 40, 22, 74) : TM.C(120, 26, 14, 50), r, 9); if (sel) Glow(g, edge, r, 9, 0.6f, 3); StrokeRR(g, Color.FromArgb(sel ? 240 : 150, edge), r, 9, sel ? 1.8f : 1.1f);
        using (var b = new SolidBrush(col)) using (var p = RR(Rc(r.X + 6, r.Y + 3, r.Width - 12, 3), 1.5f)) g.FillPath(b, p);
        float x = r.X + 6, w = r.Width - 12, y = r.Y + 8;
        // name: a click picks the track (or the bus)
        Track tt = t; var nm = Rc(x, y, w, 20); Btn(g, nm, t.Name, sel ? 4 : 0, () => PickStrip(tt), 9); Hint2(nm, (t.IsBus ? "Bus " : "Track ") + t.Name + ": click to pick it (right-click to rename)"); regs[regs.Count - 1].OnRight = () => RenameTrack(tt);
        y += 26;
        // the plugin slots
        Fx[] fxs; lock (eng.Sync) fxs = t.Fx.ToArray(); PaintSlots(g, x, y, w, slots, fxs, t); y += slots * (SlotH + SlotGap) + 6;
        // sends (tracks) or what a bus is
        if (!t.IsBus)
        {
            var sends = t.Sends; int shown = Math.Min(sends.Length, sendRows - (sends.Length < sendRows ? 1 : 0));
            for (int i = 0; i < sendRows; i++)
            {
                var row = Rc(x, y + i * (SlotH + SlotGap), w, SlotH);
                if (i < shown)
                {
                    var sd = sends[i]; int si = i; bool bad = sd.Bus == null || !eng.Tracks.Contains(sd.Bus) || !sd.Bus.IsBus;
                    FillRR(g, TM.C(140, 60, 20, 70), row, 5); var nb = Rc(row.X, row.Y, 44, SlotH); Txt(g, bad ? "?" : sd.Bus.Name, 8.5f, bad ? Warn : NeonPink, Rc(nb.X + 4, nb.Y, nb.Width - 4, SlotH), StringAlignment.Near, true);
                    var nr = Reg(nb, () => SendBusMenu(tt, si)); nr.OnRight = () => { PushUndo(); var l = new List<Send>(tt.Sends); if (si < l.Count) l.RemoveAt(si); tt.Sends = l.ToArray(); };
                    var sl = Slider(g, Rc(row.X + 46, row.Y + 3, row.Width - 48, SlotH - 6), DbToFrac(sd.LevelDb), v => sd.LevelDb = (float)Math.Round(FracToDb(v), 1), NeonPink); sl.OnDouble = () => sd.LevelDb = 0;
                    Hint2(row, "Send to " + (bad ? "a missing bus" : sd.Bus.Name) + ": " + sd.LevelDb.ToString("0.0") + " dB " + (sd.Pre ? "(pre-fader)" : "(post-fader)") + ". Click the name to change the bus, right-click it to remove the send.");
                }
                else if (i == shown) { Btn(g, row, "+ SEND", 0, () => AddSendMenu(tt), 8.5f); Hint2(row, "Send a copy of " + t.Name + " to a bus (for example a shared reverb)"); }
            }
        }
        else Txt(g, "BUS", 9, col, Rc(x, y, w, SlotH), StringAlignment.Center, true);
        y += sendRows * (SlotH + SlotGap) + 6;
        // output
        var ob = Rc(x, y, w, 22);
        if (t.IsBus) { Btn(g, ob, "BUS OPTIONS", 0, () => BusStripMenu(tt), 9); Hint2(ob, "Rename, recolour or delete this bus, or add an effect to it"); }
        else { bool badOut = t.Out != null && (!eng.Tracks.Contains(t.Out) || !t.Out.IsBus); Btn(g, ob, badOut ? "FIX BUS" : (t.Out != null ? t.Out.Name : "+ BUS"), badOut ? 3 : (t.Out != null ? 4 : 1), () => StripBusMenu(tt), 9); Hint2(ob, t.Out != null ? t.Name + " goes into the bus " + t.Out.Name + ". Click to change it." : "Put " + t.Name + " into a bus (a new one, or one you have): its effects then work on every track in it"); }
        y += 26;
        { var prb = Rc(x, y, w, 22); Btn(g, prb, "PRESETS", 0, () => { int ti = eng.Tracks.IndexOf(tt); if (tt.IsBus) insBus = tt; else { insBus = null; if (ti >= 0) selTrack = ti; } OpenInspector(2); }, 9); Hint2(prb, tt.IsBus ? "Bus presets and the AI Bus Generator for this bus" : "Vocal presets (" + VocalStyles.All.Count + "), bus presets, the AI generators and your own and community presets for " + t.Name); }
        y += 26;
        // pan
        var pr = Rc(x, y, w, 14); var ps = Slider(g, pr, (t.Pan + 1) / 2, v => tt.Pan = (float)Math.Round(v * 2 - 1, 2), col); ps.OnDouble = () => tt.Pan = 0;
        Hint2(pr, "Pan " + (Math.Abs(t.Pan) < 0.01f ? "centre" : (t.Pan < 0 ? "left " : "right ") + Math.Abs(t.Pan * 100).ToString("0")) + " (double-click: centre)");
        y += 20;
        // mute / solo: right under the pan, big enough to read and hit (at the bottom they were cut off when the mixer was short)
        { float hw = (w - 4) / 2; Btn(g, Rc(x, y, hw, 30), "M", t.Mute ? 3 : 0, () => tt.Mute = !tt.Mute, 15); Btn(g, Rc(x + hw + 4, y, hw, 30), "S", t.Solo ? 2 : 0, () => tt.Solo = !tt.Solo, 15); }
        y += 36;
        // fader and meter
        VFader(g, Rc(x + 6, y + 6, 26, faderH - 12), t.VolDb, v => tt.VolDb = (float)Math.Round(v, 1), col); Meter(g, Rc(x + 40, y, 18, faderH), t);
        Txt(g, (t.VolDb > 0 ? "+" : "") + t.VolDb.ToString("0.0"), 9, Ink, Rc(x + 60, y, w - 60, 14), StringAlignment.Far, true);
        float pk = Math.Max(t.PeakL, t.PeakR); Txt(g, pk > 1e-5f ? (20 * Math.Log10(pk)).ToString("0.0") : "-inf", 8.5f, pk > 1 ? Warn : DimC, Rc(x + 60, y + 16, w - 60, 14), StringAlignment.Far, false);
    }
    // the + BUS button of a track strip: pick a bus, make a new one, or take the track out of its bus
    void StripBusMenu(Track t)
    {
        var m = Win.NewMenu(); int n = 0;
        foreach (var b0 in eng.Tracks.ToArray()) if (b0.IsBus) { var bus = b0; n++; var it = new ToolStripMenuItem("Into " + bus.Name) { Checked = t.Out == bus }; it.Click += (a, b) => { PushUndo(); lock (eng.Sync) t.Out = bus; }; m.Items.Add(it); }
        if (n > 0) m.Items.Add(new ToolStripSeparator());
        m.Items.Add("New bus with " + t.Name + " in it").Click += (a, b) => { int keep = selTrack; var nb = AddBus(); selTrack = keep; if (nb != null) { PushUndo(); lock (eng.Sync) t.Out = nb; insBus = null; } };
        if (t.Out != null) m.Items.Add("Take it out of the bus (straight to the master)").Click += (a, b) => { PushUndo(); lock (eng.Sync) t.Out = null; };
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Send a copy to a bus (a send)...").Click += (a, b) => AddSendMenu(t);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    // the BUS OPTIONS button of a bus strip
    void BusStripMenu(Track bus)
    {
        var m = Win.NewMenu(); m.Items.Add("Add an effect to " + bus.Name).Click += (a, b) => InsAddEffect(bus); m.Items.Add("Bus presets...").Click += (a, b) => ChainMenu(bus);
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Rename...").Click += (a, b) => RenameTrack(bus); m.Items.Add("Colour...").Click += (a, b) => TrackColourMenu(bus);
        int feeding = 0; foreach (var q in eng.Tracks) if (!q.IsBus && (q.Out == bus || Array.Exists(q.Sends, s => s.Bus == bus))) feeding++;
        m.Items.Add(new ToolStripMenuItem(feeding == 0 ? "No track goes into this bus yet (use + BUS on a track)" : feeding + " track" + (feeding == 1 ? "" : "s") + " go into this bus") { Enabled = false });
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Delete " + bus.Name).Click += (a, b) => { if (AskYes("Delete " + bus.Name + "? Tracks in it go straight to the master again.", "Delete bus")) DeleteBusSafely(bus); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void PickStrip(Track t)
    {
        if (t.IsBus) { insBus = t; busEdit = null; selFx = -1; return; }
        int i = eng.Tracks.IndexOf(t); if (i >= 0) { selTrack = i; busEdit = null; insBus = null; selFx = -1; }
    }
    // the slots: each plugin in the order the sound goes through them, then an empty slot to add one; when they don't all fit the last slot lists the rest
    void PaintSlots(Graphics g, float x, float y, float w, int slots, Fx[] fxs, Track t)
    {
        bool master = t == null; int room = fxs.Length + 1 <= slots ? fxs.Length : slots - 1;
        for (int i = 0; i < slots; i++)
        {
            var row = Rc(x, y + i * (SlotH + SlotGap), w, SlotH);
            if (i < room)
            {
                var f = fxs[i]; int fi = i; bool open = FxWindowOf(f) != null; Color c = f.Bypass ? DimC : (open ? UiTokens.Sel(NeonCyan) : NeonViolet);
                PaintPxSlot(g, row, f, open); PaintFxViz(g, Rc(row.X + 3, row.Y + 2, row.Width - 6, row.Height - 4), f, false);   // the Purphex slot with its live picture of what the plugin is doing
                var dot = Rc(row.X + 6, row.Y + 7, 10, 10); using (var b = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b, dot);
                Txt(g, f.Title, 8.5f, Color.FromArgb(120, 0, 0, 0), Rc(row.X + 20, row.Y + 1, row.Width - 22, SlotH), StringAlignment.Near, true); Txt(g, f.Title, 8.5f, f.Bypass ? DimC : Ink, Rc(row.X + 19, row.Y, row.Width - 22, SlotH), StringAlignment.Near, true);   // (a soft shadow keeps the name readable over the picture)
                var rg = Reg(Rc(row.X + 16, row.Y, row.Width - 16, SlotH), () => { OpenFxWindow(t, f); if (!master) PickStrip(t); }); rg.OnRight = () => SlotMenu(t, f);
                Reg(Rc(row.X, row.Y, 16, SlotH), () => f.Bypass = !f.Bypass);
                Hint2(row, f.Title + (f.Bypass ? " (bypassed)" : "") + ": " + FxHelp.About(f) + "  Click to open its window, the dot turns it on or off, right-click for more");
            }
            else if (i == room && room < fxs.Length)
            {   // more plugins than slots: this one lists them all
                Btn(g, row, "+" + (fxs.Length - room) + " MORE", 2, () => AllSlotsMenu(t, fxs), 8.5f); Hint2(row, "The rest of the plugins on this strip");
            }
            else if (i == room)
            {
                Btn(g, row, "+ ADD", 0, () => { if (master) { insAddFor = null; OpenBrowser(true, -1); } else InsAddEffect(t); }, 10); Hint2(row, "Add an effect" + (master ? " to the master" : " to " + t.Name) + ": its window opens");
            }
            else { using (var pen = new Pen(Color.FromArgb(50, Ink)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot }) using (var p = RR(row, 5)) g.DrawPath(pen, p); }
        }
    }
    void SlotMenu(Track t, Fx f)
    {
        var m = Win.NewMenu(); var list = t == null ? eng.MasterFx : t.Fx; int fi; lock (eng.Sync) fi = list.IndexOf(f); if (fi < 0) return;
        m.Items.Add("Open " + f.Title).Click += (a, b) => OpenFxWindow(t, f);
        m.Items.Add(f.Bypass ? "Turn on" : "Bypass").Click += (a, b) => f.Bypass = !f.Bypass;
        if (t != null)
        {
            m.Items.Add(new ToolStripSeparator());
            if (fi > 0) m.Items.Add("Move up (earlier in the chain)").Click += (a, b) => MoveFx(t, fi, -1);
            if (fi < list.Count - 1) m.Items.Add("Move down (later in the chain)").Click += (a, b) => MoveFx(t, fi, 1);
            if (!(f is InstrumentFx)) m.Items.Add("Copy (same settings, right after it)").Click += (a, b) => DuplicateFx(t, fi);
            m.Items.Add(new ToolStripSeparator()); m.Items.Add("Remove " + f.Title).Click += (a, b) => RemoveFxAt(t, fi);
        }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void AllSlotsMenu(Track t, Fx[] fxs)
    {
        var m = Win.NewMenu(); foreach (var f0 in fxs) { var f = f0; var it = new ToolStripMenuItem((f.Bypass ? "(off) " : "") + f.Title); it.Click += (a, b) => OpenFxWindow(t, f); m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("+ Add effect").Click += (a, b) => { if (t == null) { insAddFor = null; OpenBrowser(true, -1); } else InsAddEffect(t); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    internal void RemoveFxAt(Track t, int fi)
    {
        if (fi < 0 || fi >= t.Fx.Count) return; PushUndo(); Fx rf; lock (eng.Sync) { rf = t.Fx[fi]; t.Fx.RemoveAt(fi); t.Auto.RemoveAll(al => al.Fx == rf); retired.Add(rf); }
        var ic = rf as InstrumentFx; if (ic != null) ic.CloseWindow(); CloseFxWindow(rf); selFx = -1;
    }
    internal void DuplicateFx(Track t, int fi)
    {
        if (fi < 0 || fi >= t.Fx.Count) return; var src = t.Fx[fi]; if (src is InstrumentFx) { Toast("An instrument can't be copied this way."); return; }
        var f = FxRegistry.Create(src.Type); if (f == null) return; f.Prepare(StudioEngine.SR); f.Deserialize(src.Serialize()); f.Bypass = src.Bypass; f.CurPreset = src.CurPreset; f.CurPresetData = src.CurPresetData;
        PushUndo(); lock (eng.Sync) t.Fx.Insert(fi + 1, f); Toast(f.Title + " copied");
    }

    // ---------------- BUSES ----------------
    void PaintInsBuses(Graphics g, RectangleF C)
    {
        var buses = eng.Tracks.Where(b => b.IsBus).ToList(); if (insBus == null && buses.Count > 0) insBus = buses[0];
        float x = C.X, y = C.Y, h = C.Height;
        var lb = InsBox(g, Rc(x, y, 260, h), "BUSES (" + buses.Count + ")", Accent); float ly = lb.Y;
        foreach (var b in buses)
        {
            if (ly + 26 > lb.Bottom - 30) break; var bb = b; bool sel = b == insBus; var row = Rc(lb.X, ly, lb.Width, 24);
            FillRR(g, sel ? TM.C(170, 70, 40, 120) : TM.C(110, 40, 22, 74), row, 5); if (sel) StrokeRR(g, Accent, row, 5, 1.2f);
            using (var cb = new SolidBrush(Neon(b.Color))) g.FillEllipse(cb, row.X + 6, row.Y + 6, 12, 12); Txt(g, b.Name, 10, Ink, Rc(row.X + 24, row.Y, row.Width - 110, 24), StringAlignment.Near, sel);
            float mtr = MeterFrac(Math.Max(b.PeakL, b.PeakR)); FillRR(g, TM.C(200, 6, 3, 14), Rc(row.Right - 84, row.Y + 9, 30, 6), 2); if (mtr > 0) FillRR(g, Good, Rc(row.Right - 84, row.Y + 9, 30 * mtr, 6), 2);
            Reg(row, () => { insBus = bb; }); Btn(g, Rc(row.Right - 50, row.Y + 2, 22, 20), "M", b.Mute ? 3 : 0, () => bb.Mute = !bb.Mute, 9); Btn(g, Rc(row.Right - 26, row.Y + 2, 22, 20), "S", b.Solo ? 2 : 0, () => bb.Solo = !bb.Solo, 9);
            ly += 27;
        }
        if (buses.Count == 0) Txt(g, "NO BUSES YET. A BUS MIXES SEVERAL TRACKS AND RUNS SHARED PLUGINS ON THEM.", 9.5f, DimC, Rc(lb.X, ly, lb.Width, 40), StringAlignment.Near, false);
        float bw3 = (lb.Width - 8) / 3; Btn(g, Rc(lb.X, lb.Bottom - 24, bw3, 24), "+ NEW", 1, () => { insBus = AddBus(); }, 10);
        Btn(g, Rc(lb.X + bw3 + 4, lb.Bottom - 24, bw3, 24), "RENAME", insBus != null ? 0 : 0, () => { if (insBus != null) RenameTrack(insBus); }, 9.5f);
        Btn(g, Rc(lb.X + 2 * (bw3 + 4), lb.Bottom - 24, bw3, 24), "DELETE", insBus != null ? 3 : 0, () => { if (insBus != null && AskYes("Delete " + insBus.Name + "? Tracks sent to it go back to the master.", "Delete bus")) DeleteBusSafely(insBus); }, 9.5f);
        x += 268; if (insBus == null) return; var bus = insBus;
        // the bus's own channel: level, pan, meter, output
        var cbx = InsBox(g, Rc(x, y, 150, h), "LEVEL", Neon(bus.Color)); VFader(g, Rc(cbx.X + 6, cbx.Y + 4, 30, cbx.Height - 44), bus.VolDb, v => bus.VolDb = (float)Math.Round(v, 1), Neon(bus.Color)); Meter(g, Rc(cbx.X + 48, cbx.Y + 4, 24, cbx.Height - 44), bus);
        Txt(g, bus.VolDb.ToString("0.0") + " dB", 10, Ink, Rc(cbx.X + 76, cbx.Y + 4, cbx.Width - 76, 16), StringAlignment.Near, true); var ps = Slider(g, Rc(cbx.X, cbx.Bottom - 34, cbx.Width, 16), (bus.Pan + 1) / 2, v => bus.Pan = (float)Math.Round(v * 2 - 1, 2), Neon(bus.Color)); ps.OnDouble = () => bus.Pan = 0;
        Txt(g, "PAN " + (Math.Abs(bus.Pan) < 0.01f ? "C" : (bus.Pan < 0 ? "L" : "R") + Math.Abs(bus.Pan * 100).ToString("0")) + " · OUT: MASTER", 9, DimC, Rc(cbx.X, cbx.Bottom - 16, cbx.Width, 16), StringAlignment.Near, false);
        x += 158;
        // routing: every track, with its output and its send to this bus
        float rw = Math.Max(300, C.Right - x - 250); var rb = InsBox(g, Rc(x, y, rw, h), "ROUTING TO " + bus.Name.ToUpperInvariant(), NeonPink); float ry = rb.Y;
        var tracks = eng.Tracks.Where(q => !q.IsBus && q.Host == null).ToList(); var badList = eng.Tracks.Where(q => q.Out != null && (!eng.Tracks.Contains(q.Out) || !q.Out.IsBus)).ToList();
        if (badList.Count > 0) { Btn(g, Rc(rb.X, ry, rb.Width, 22), badList.Count + " TRACK(S) SENT TO A MISSING BUS: SEND THEM TO THE MASTER", 3, () => { PushUndo(); foreach (var q in badList) q.Out = null; }, 9); ry += 26; }
        foreach (var q in tracks)
        {
            if (ry + 24 > rb.Bottom) break; var qq = q; bool outHere = q.Out == bus; Send sd = Array.Find(q.Sends, s => s.Bus == bus);
            Txt(g, q.Name, 10, Ink, Rc(rb.X, ry, rb.Width * 0.32f, 22), StringAlignment.Near, false);
            Btn(g, Rc(rb.X + rb.Width * 0.33f, ry, 80, 22), outHere ? "OUTPUT ✓" : "OUTPUT", outHere ? 4 : 0, () => { PushUndo(); qq.Out = qq.Out == bus ? null : bus; }, 9);
            if (sd == null) Btn(g, Rc(rb.X + rb.Width * 0.33f + 84, ry, 70, 22), "+ SEND", 0, () => AddSend(qq, bus), 9);
            else { var sl = Slider(g, Rc(rb.X + rb.Width * 0.33f + 84, ry + 3, Math.Max(40, rb.Width * 0.67f - 84 - 90), 16), DbToFrac(sd.LevelDb), v => sd.LevelDb = (float)Math.Round(FracToDb(v), 1), NeonPink); Txt(g, sd.LevelDb.ToString("0.0") + " dB", 9, Ink, Rc(rb.Right - 86, ry, 52, 22), StringAlignment.Far, false); Btn(g, Rc(rb.Right - 30, ry, 28, 22), "x", 3, () => { PushUndo(); qq.Sends = Array.FindAll(qq.Sends, s => s.Bus != bus); }, 9); }
            ry += 25;
        }
        x += rw + 8;
        // the bus's plugins
        var pbx = InsBox(g, Rc(x, y, 236, h), "BUS PLUGINS", NeonViolet); Fx[] fxs; lock (eng.Sync) fxs = bus.Fx.ToArray(); float py = pbx.Y;
        for (int i = 0; i < fxs.Length && py + 22 <= pbx.Bottom - 26; i++) { var f = fxs[i]; int fi = i; var row = Rc(pbx.X, py, pbx.Width, 22); FillRR(g, TM.C(110, 40, 22, 74), row, 5); var dot = Rc(row.X + 4, row.Y + 4, 14, 14); using (var b2 = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b2, dot); Reg(dot, () => f.Bypass = !f.Bypass); Txt(g, f.Title, 10, Ink, Rc(row.X + 22, row.Y, row.Width - 80, 22), StringAlignment.Near, false); Reg(Rc(row.X + 20, row.Y, row.Width - 76, 22), () => OpenFxWindow(bus, f)); Btn(g, Rc(row.Right - 50, row.Y + 1, 22, 20), "<", 0, () => MoveFx(bus, fi, -1), 9); Btn(g, Rc(row.Right - 26, row.Y + 1, 22, 20), "x", 3, () => RemoveFxAt(bus, fi), 9); py += 25; }
        Btn(g, Rc(pbx.X, pbx.Bottom - 24, pbx.Width, 24), "+ ADD EFFECT TO BUS", 1, () => InsAddEffect(bus), 10);
    }
    // a bus is deleted without leaving anything pointing at it: tracks that went to it go to the master, sends to it are removed
    internal void DeleteBusSafely(Track bus)
    {
        if (bus == null || !bus.IsBus) return;
        foreach (var f in bus.Fx) CloseFxWindow(f); DeleteTrackQuiet(bus); if (insBus == bus) insBus = null; if (busEdit == bus) busEdit = null;   // (DeleteTrackQuiet takes the undo snapshot first, then sends the bus's tracks to the master and removes the sends to it)
    }

    // ---------------- CLIP ----------------
    void PaintInsClip(Graphics g, RectangleF C, Track c)
    {
        if (c == null || c.Clip == null) { Txt(g, "PICK AN AUDIO CLIP ON THE TIMELINE TO EDIT IT HERE", 12, DimC, C, StringAlignment.Center, false); return; }
        float x = C.X - insScrollX, y = C.Y, h = C.Height; Color col = Neon(c.Color); double sr = StudioEngine.SR;
        var a = InsBox(g, Rc(x, y, 300, h), "CLIP", col);
        Btn(g, Rc(a.X, a.Y, a.Width, 24), c.SectionLabel.Length > 0 ? c.SectionLabel : c.Name, 0, () => { string nm = Ask("Name for this clip:", c.SectionLabel.Length > 0 ? c.SectionLabel : c.Name); if (nm != null) { PushUndo(); c.SectionLabel = nm.Trim(); } }, 10);
        Txt(g, "FILE: " + System.IO.Path.GetFileName(c.Clip.Path.Length > 0 ? c.Clip.Path : c.Clip.Name), 9, DimC, Rc(a.X, a.Y + 28, a.Width, 16), StringAlignment.Near, false);
        Txt(g, "STARTS " + Clock(c.Offset / sr) + "   LENGTH " + Clock(c.UsedLength / sr) + "   ENDS " + Clock((c.Offset + c.UsedLength) / sr), 9.5f, Ink, Rc(a.X, a.Y + 46, a.Width, 16), StringAlignment.Near, true);
        Txt(g, "TRIM: STARTS " + (c.ClipStart / sr).ToString("0.00") + " s INTO THE FILE", 9, DimC, Rc(a.X, a.Y + 64, a.Width, 16), StringAlignment.Near, false);
        // the picture of the part that plays
        var wv = Rc(a.X, a.Y + 84, a.Width, Math.Max(20, a.Bottom - a.Y - 84 - 30)); FillRR(g, TM.C(200, 8, 4, 18), wv, 5); DrawMiniWave(g, c, wv, col);
        float bw = (a.Width - 8) / 3, by = a.Bottom - 24;
        Btn(g, Rc(a.X, by, bw, 24), c.PieceMuted ? "MUTED" : "MUTE", c.PieceMuted ? 3 : 0, () => { PushUndo(); c.PieceMuted = !c.PieceMuted; }, 9.5f);
        Btn(g, Rc(a.X + bw + 4, by, bw, 24), c.PieceLocked ? "LOCKED" : "LOCK", c.PieceLocked ? 4 : 0, () => { PushUndo(); c.PieceLocked = !c.PieceLocked; }, 9.5f);
        Btn(g, Rc(a.X + 2 * (bw + 4), by, bw, 24), "REVERSE", 0, () => ReverseClip(c), 9.5f);
        x += 308;
        var gb = InsBox(g, Rc(x, y, 260, h), "GAIN · FADES", col);
        Txt(g, "CLIP GAIN  " + (c.ClipGainDb > 0 ? "+" : "") + c.ClipGainDb.ToString("0.0") + " dB", 10, Ink, Rc(gb.X, gb.Y, gb.Width, 16), StringAlignment.Near, true);
        var gs = Slider(g, Rc(gb.X, gb.Y + 18, gb.Width, 18), (c.ClipGainDb + 24) / 36, v => c.ClipGainDb = (float)Math.Round(v * 36 - 24, 1), col); gs.OnDouble = () => c.ClipGainDb = 0;
        double maxFade = Math.Max(0.05, Math.Min(10, c.UsedLength / sr / 2));
        Txt(g, "FADE IN  " + (c.FadeIn / sr).ToString("0.00") + " s", 10, Ink, Rc(gb.X, gb.Y + 44, gb.Width, 16), StringAlignment.Near, true); Slider(g, Rc(gb.X, gb.Y + 62, gb.Width, 18), (float)(c.FadeIn / sr / maxFade), v => c.FadeIn = (long)(v * maxFade * sr), col);
        Txt(g, "FADE OUT  " + (c.FadeOut / sr).ToString("0.00") + " s", 10, Ink, Rc(gb.X, gb.Y + 88, gb.Width, 16), StringAlignment.Near, true); Slider(g, Rc(gb.X, gb.Y + 106, gb.Width, 18), (float)(c.FadeOut / sr / maxFade), v => c.FadeOut = (long)(v * maxFade * sr), col);
        x += 268;
        var pb = InsBox(g, Rc(x, y, 230, h), "PITCH · TIME", col);
        Txt(g, "Fix or change the pitch of this clip note by note, or tune it to a key, in Flex Pitch.", 9.5f, DimC, Rc(pb.X, pb.Y, pb.Width, 34), StringAlignment.Near, false);
        Btn(g, Rc(pb.X, pb.Y + 40, pb.Width, 26), "OPEN FLEX PITCH", 1, () => { toolsTrack = c; selTrack = Math.Max(0, eng.Tracks.IndexOf(RowHost(c))); FlexOpen(); }, 10);
        Txt(g, c.Bpm > 0 ? "TEMPO HEARD: " + c.Bpm.ToString("0.#") + " BPM" : "TEMPO: NOT DETECTED YET", 9.5f, Ink, Rc(pb.X, pb.Y + 74, pb.Width, 16), StringAlignment.Near, true);
        insMaxX = Math.Max(0, (x + 238 + insScrollX) - C.Right); insScrollX = Math.Min(insScrollX, insMaxX);
    }
    static string Clock(double s) { if (s < 0) s = 0; int m = (int)(s / 60); return m + ":" + (s - m * 60).ToString("00.00"); }
    void DrawMiniWave(Graphics g, Track c, RectangleF r, Color col)
    {
        var L = c.Clip.L; if (L == null) return; long st = c.ClipStart, n = c.UsedLength; if (n <= 0) return; float mid = r.Y + r.Height / 2;
        using (var pen = new Pen(Color.FromArgb(220, col), 1f)) for (int px = 0; px < (int)r.Width; px++) { long a = st + n * px / (long)r.Width, b = Math.Min(L.Length, st + n * (px + 1) / (long)r.Width); float mx = 0; for (long i = a; i < b; i += Math.Max(1, (b - a) / 64)) mx = Math.Max(mx, Math.Abs(L[i])); float hh = mx * r.Height / 2 * 0.95f; g.DrawLine(pen, r.X + px, mid - hh, r.X + px, mid + hh); }
    }
    // reverse the part of the clip that plays (a reversed copy: undo brings the original back)
    internal void ReverseClip(Track c)
    {
        if (c == null || c.Clip == null || c.Clip.L == null) return; long st = c.ClipStart, n = c.UsedLength; if (n <= 0) return; PushUndo();
        var l = new float[n]; var r = new float[n]; var R = c.Clip.R ?? c.Clip.L; for (long i = 0; i < n; i++) { l[i] = c.Clip.L[st + n - 1 - i]; r[i] = R[st + n - 1 - i]; }
        var nc = AudioClip.FromArrays(l, r, c.Clip.Name + " (reversed)"); lock (eng.Sync) { c.Clip = nc; c.ClipStart = 0; c.ClipFrames = -1; } Toast("Clip reversed (Ctrl+Z puts it back)");
    }

    // ---------------- MIDI ----------------
    void PaintInsMidi(Graphics g, RectangleF C, Track t)
    {
        float x = C.X - insScrollX, y = C.Y, h = C.Height; Color col = Neon(t.Color); var on = new List<MidiEv>(); lock (t.Midi) foreach (var m in t.Midi) if ((m.S & 0xF0) == 0x90 && m.D2 > 0) on.Add(m); double sr = StudioEngine.SR;
        var a = InsBox(g, Rc(x, y, 300, h), "MIDI PART", col);
        Btn(g, Rc(a.X, a.Y, a.Width, 24), t.Name, 0, () => RenameTrack(t), 10);
        if (on.Count == 0) Txt(g, "NO NOTES YET: PLAY OR DRAW SOME IN THE PIANO ROLL", 10, DimC, Rc(a.X, a.Y + 30, a.Width, 30), StringAlignment.Near, false);
        else
        {
            long first = on.Min(m => m.Frame), last; lock (t.Midi) last = t.Midi.Count > 0 ? t.Midi.Max(m => m.Frame) : first; int lo = on.Min(m => m.D1), hi = on.Max(m => m.D1); double bars = (last - first) / sr / (240.0 / Fx.ProjectBpm);
            Txt(g, on.Count + " NOTES  ·  " + Music.NoteName(lo) + " TO " + Music.NoteName(hi), 10, Ink, Rc(a.X, a.Y + 30, a.Width, 16), StringAlignment.Near, true);
            Txt(g, "STARTS " + Clock(first / sr) + "   LENGTH " + Clock((last - first) / sr) + " (" + bars.ToString("0.#") + " BARS)", 9.5f, DimC, Rc(a.X, a.Y + 48, a.Width, 16), StringAlignment.Near, false);
            Txt(g, "VELOCITY " + on.Min(m => m.D2) + " TO " + on.Max(m => m.D2), 9.5f, DimC, Rc(a.X, a.Y + 66, a.Width, 16), StringAlignment.Near, false);
        }
        Btn(g, Rc(a.X, a.Bottom - 24, a.Width, 24), "OPEN PIANO ROLL", 1, () => OpenPianoRoll(t), 10);
        x += 308;
        var tb = InsBox(g, Rc(x, y, 250, h), "TRANSPOSE · VELOCITY", col); float bw = (tb.Width - 12) / 4;
        Txt(g, "TRANSPOSE", 9.5f, DimC, Rc(tb.X, tb.Y, tb.Width, 16), StringAlignment.Near, true);
        string[] tl = { "-12", "-1", "+1", "+12" }; int[] tv = { -12, -1, 1, 12 }; for (int i = 0; i < 4; i++) { int d = tv[i]; Btn(g, Rc(tb.X + i * (bw + 4), tb.Y + 18, bw, 24), tl[i], 0, () => MidiTranspose(t, d), 10); }
        Txt(g, "VELOCITY", 9.5f, DimC, Rc(tb.X, tb.Y + 50, tb.Width, 16), StringAlignment.Near, true);
        string[] vl = { "-10%", "+10%", "ALL 100", "ALL 127" }; for (int i = 0; i < 4; i++) { int k = i; Btn(g, Rc(tb.X + i * (bw + 4), tb.Y + 68, bw, 24), vl[i], 0, () => MidiVelocity(t, k), 9); }
        x += 258;
        var qb = InsBox(g, Rc(x, y, 230, h), "QUANTISE", col); string[] ql = { "1/4", "1/8", "1/16", "1/32" }; double[] qv = { 1, 0.5, 0.25, 0.125 }; float qw = (qb.Width - 12) / 4;
        Txt(g, "Snap note starts to the grid at " + Fx.ProjectBpm.ToString("0.#") + " BPM (lengths are kept).", 9.5f, DimC, Rc(qb.X, qb.Y, qb.Width, 30), StringAlignment.Near, false);
        for (int i = 0; i < 4; i++) { double q = qv[i]; Btn(g, Rc(qb.X + i * (qw + 4), qb.Y + 34, qw, 26), ql[i], 0, () => MidiQuantise(t, q), 10); }
        x += 238;
        var ib = InsBox(g, Rc(x, y, 230, h), "INSTRUMENT", col); InstrumentFx ins = null; foreach (var f in t.Fx) { var i2 = f as InstrumentFx; if (i2 != null) { ins = i2; break; } }
        Txt(g, ins != null ? ins.Title : "NO INSTRUMENT", 11, ins != null ? Ink : Warn, Rc(ib.X, ib.Y, ib.Width, 20), StringAlignment.Near, true);
        if (ins != null) Btn(g, Rc(ib.X, ib.Y + 26, ib.Width, 26), ins.WindowLabel, 1, () => { ins.Owner = t; ins.ShowWindow(this); }, 10);
        insMaxX = Math.Max(0, (x + 238 + insScrollX) - C.Right); insScrollX = Math.Min(insScrollX, insMaxX);
    }
    internal void MidiTranspose(Track t, int semis) { PushUndo(); lock (t.Midi) foreach (var m in t.Midi) { int s = m.S & 0xF0; if (s == 0x90 || s == 0x80) m.D1 = (byte)Math.Max(0, Math.Min(127, m.D1 + semis)); } Toast("Transposed " + (semis > 0 ? "+" : "") + semis); }
    internal void MidiVelocity(Track t, int mode)
    {
        PushUndo(); lock (t.Midi) foreach (var m in t.Midi) { if ((m.S & 0xF0) != 0x90 || m.D2 == 0) continue; int v = m.D2; v = mode == 0 ? (int)Math.Round(v * 0.9) : mode == 1 ? (int)Math.Round(v * 1.1) : mode == 2 ? 100 : 127; m.D2 = (byte)Math.Max(1, Math.Min(127, v)); }
    }
    // note starts snap to the grid; each note's end moves with its start, so lengths stay the same
    internal void MidiQuantise(Track t, double beats)
    {
        double grid = beats * 60.0 / Fx.ProjectBpm * StudioEngine.SR; if (grid < 1) return; PushUndo();
        lock (t.Midi)
        {
            var open = new Dictionary<int, long>(); var shift = new Dictionary<MidiEv, long>(); var evs = t.Midi.OrderBy(m => m.Frame).ToList();
            foreach (var m in evs) { int s = m.S & 0xF0; if (s == 0x90 && m.D2 > 0) { long q = (long)(Math.Round(m.Frame / grid) * grid); open[m.D1] = q - m.Frame; m.Frame = Math.Max(0, q); } else if ((s == 0x80 || (s == 0x90 && m.D2 == 0)) && open.ContainsKey(m.D1)) { m.Frame = Math.Max(0, m.Frame + open[m.D1]); open.Remove(m.D1); } }
            t.Midi.Sort((a2, b2) => a2.Frame != b2.Frame ? a2.Frame.CompareTo(b2.Frame) : ((a2.S & 0xF0) == 0x80 ? -1 : 1).CompareTo((b2.S & 0xF0) == 0x80 ? -1 : 1));
        }
        Toast("Quantised to " + (beats >= 1 ? "1/4" : beats >= 0.5 ? "1/8" : beats >= 0.25 ? "1/16" : "1/32"));
    }
}
