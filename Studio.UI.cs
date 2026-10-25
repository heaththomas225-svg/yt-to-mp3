using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Purphex Music window (part 1): infrastructure, toolbar, transport, mix view,
//  plugin panel, effect browser, generic / EQ / pitch editors.
//  Custom-drawn in the app's purple theme; every control is hit-tested through "regions".
// ======================================================================
class HitReg
{
    public RectangleF R; public Action OnDown, OnRight, OnDouble, OnUp; public Action<PointF> OnDrag; public Action<int> OnWheel; public bool WhileBusy; public bool ScrollBar; public bool Button; public int Layer; public bool Overlay;   // Overlay: a bar drawn over the page (the audio bar, a scroll bar): nothing under it takes its clicks   // Button: made by Btn (a button always takes the click from the card or row it sits on); Layer: which pop-up it belongs to   // ScrollBar: a part of a logo scroll bar (the wheel there scrolls the list, it never changes a value)
}

partial class StudioForm : Form
{
    internal App app; internal StudioEngine eng;
    readonly List<HitReg> regs = new List<HitReg>(); HitReg hover, dragReg;
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); FrameLoop studioLoop; readonly FpsCounter fpsc = new FpsCounter(); double lastTickMs, lastStepMs; readonly System.Diagnostics.Stopwatch studioClock = System.Diagnostics.Stopwatch.StartNew(); bool lastIdle;
    internal void ApplyFps() { if (studioLoop != null) studioLoop.TargetFps = Math.Max(30, Math.Min(Prefs.FpsEff, FrameLoop.DisplayHz())); }
    double FrameGapMs { get { return 1000.0 / Math.Max(30, Math.Min(Prefs.FpsEff, FrameLoop.DisplayHz())) - 1.5; } }
    // one step of the studio loop: housekeeping about 60 times a second, drawing as fast as the chosen frame rate allows
    void FrameStep()
    {
        if (moving) return; double now = studioClock.Elapsed.TotalMilliseconds; if (now - lastTickMs >= 15) { lastTickMs = now; Tick(); }
        if (NeedsFrame()) { double dt = (now - lastStepMs) / 1000.0; lastStepMs = now; if (dt > 0.1) dt = 0.033; if (dt < 0.001) dt = 0.001; t += (float)dt; Invalidate(); }
    }
    float t;

    // theme (the app's deep purple, darker and calmer for a work surface)
    static Color Bg0 = TM.C(255, 7, 4, 16), Bg1 = TM.C(255, 18, 8, 36), Panel = TM.C(235, 18, 10, 34), Lane = TM.C(255, 11, 6, 22), Lane2 = TM.C(255, 15, 8, 29);
    static Color Edge = TM.C(120, 140, 85, 210), Ink = TM.C(255, 240, 232, 255), Accent = TM.C(255, 178, 108, 255), Accent2 = TM.C(255, 236, 92, 226);
    static Color Violet = TM.C(255, 118, 52, 212), BtnC = TM.C(255, 70, 40, 120), Good = Color.FromArgb(120, 230, 160), Warn = Color.FromArgb(255, 190, 90), Bad = TM.C(255, 255, 100, 140);
    static Color DimC = TM.C(255, 165, 148, 190);

    int tab;                         // 0 Mix, 1 Mastering
    int selTrack, selFx = -1, selMaster = 0; long scroll; double spp = 600; float vscroll;
    Dictionary<Track, float[]> hold = new Dictionary<Track, float[]>();
    double[] spec = new double[2049]; double[] specIn = new double[4096], specRe = new double[4096], specIm = new double[4096];
    float genScroll, panelScroll; int eqBand = 2;
    // Auto-mix choice
    string amStyle = "Natural"; bool amEnhance = true;
    // browser overlay
    bool browserOpen; string browserQuery = ""; bool browserMaster; int browserTrack; float browserScroll; internal bool browserVst;   // browserVst: the VSTS list (Bass Lab, VST instruments, Purphex VSTs) instead of the effects
    static bool IsVstCat(string c) { return c == "Instruments" || c == "Purphex VST"; }
    // busy overlay
    DateTime busyStart; volatile bool busy; string busyText = ""; volatile float busyPct = -1; volatile bool cancelFlag; bool busyCancelable;
    string projPath = "", projName = "Untitled project";
    bool recOn;   // notes played on an instrument are recorded onto its track while the song plays
    RectangleF toastRect = RectangleF.Empty; string toast = ""; DateTime toastAt = DateTime.MinValue;
    double ToastSecs { get { return Math.Max(3.5, Math.Min(11, 2.2 + toast.Length * 0.05)); } }   // a long message stays long enough to read
    float lufsPeakHold = -120;
    TextBox editBox; Action<string> editApply; string editOrig = "";
    List<float> trail8 = new List<float>();

    void InitEdit()
    {
        editBox = new TextBox { Visible = false, BorderStyle = BorderStyle.None, BackColor = TM.C(255, 10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 10.5f), TextAlign = HorizontalAlignment.Center };
        Controls.Add(editBox); Win.NeonBox(editBox);
        editBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitEdit(true); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CommitEdit(false); }
        };
        editBox.Leave += (s, e) => CommitEdit(true);
    }

    // Click a value to type an exact number. Click anywhere else (or press Enter) to confirm it, Esc to cancel. Nothing changes if you did not type anything.
    void BeginEdit(RectangleF r, string text, Action<string> apply)
    {
        if (editBox.Visible) CommitEdit(true);
        editApply = apply; editOrig = text ?? ""; editBox.Text = editOrig; int bh = editBox.PreferredHeight; editBox.SetBounds((int)r.X + 3, (int)(r.Y + (r.Height - bh) / 2), (int)Math.Max(40, r.Width) - 6, bh);
        editBox.Visible = true; editBox.BringToFront(); editBox.Focus(); editBox.SelectAll();
    }

    void CommitEdit(bool apply)
    {
        if (editBox == null || !editBox.Visible) return;
        var a = editApply; string txt = editBox.Text; bool changed = txt != editOrig; editBox.Visible = false; editApply = null; Focus(); Invalidate();
        if (apply && changed && a != null) { try { a(txt); } catch { } }
    }

    HitReg ValueEdit(RectangleF r, string text, Action<string> apply) { return Reg(r, () => BeginEdit(r, text, apply)); }
    // a value you can type over: a neon box with the number inside it (the click area is exactly the box, kept well away from the slider and the words)
    HitReg ValueBox(Graphics g, RectangleF r, string shown, string editText, Action<string> apply, float px = 12, bool bold = false)
    {
        bool hot = Hot(r) && !(editBox != null && editBox.Visible); FillRR(g, hot ? TM.C(225, 22, 12, 50) : TM.C(205, 10, 5, 24), r, 7); Win.DrawNeonBox(g, r, hot);
        Txt(g, shown, px, Ink, r, StringAlignment.Center, bold); if (hot) hintNow = "Click the box to type an exact value. Click anywhere else (or press Enter) to confirm, Esc to cancel.";
        var vr = Reg(r, () => BeginEdit(r, editText, apply)); vr.Button = true; return vr;   // a value box is a control: it is always on top of what is under it
    }

    static bool ParseNum(string s, out float v)
    {
        v = 0; if (s == null) return false; s = s.Trim().Replace(',', '.');
        if (s.ToLowerInvariant().Contains("inf")) { v = -999; return true; }
        var m = Regex.Match(s, @"-?\d+(\.\d+)?"); return m.Success && float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
    static string Num(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    public StudioForm(App owner)
    {
        Wv2Loader.Ensure(); app = owner; eng = app.StudioEng; Win.Dark(this); InitEdit();
        Text = "Purphex Music"; StartPosition = FormStartPosition.CenterParent; BackColor = Bg0;
        ClientSize = new Size(1360, 800); MinimumSize = new Size(1240, 680);
        DoubleBuffered = true; KeyPreview = true; FontKit.Restore(this);   /* AllowDrop is switched on in OnHandleCreated (Studio.Crash.cs) so a refused registration cannot crash */
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        studioLoop = new FrameLoop(this, FrameStep) { Slow = () => WindowState == FormWindowState.Minimized || moving || Win.AnyMoving };   /* while a window is being dragged around nothing needs painting often */ ApplyFps();
        InstrumentFx.Recorder = OnMidiNote; MidiInput.BareTrack = MidiToBareTrack;
        MinimumSize = new Size(1240, 640);
        FormClosed += (s, e) => { try { if (flexOpen) FlexClose(true); } catch { } flexOpen = false; DisposeRetired(); Auditioner.Shutdown(); };
        FormClosed += (s, e) => { if (backdrop != null) { backdrop.Dispose(); backdrop = null; } foreach (var pc0 in peakCache.Values) if (pc0.Img != null) pc0.Img.Dispose(); peakCache.Clear(); };
        FormClosed += (s, e) => { timer.Stop(); if (studioLoop != null) studioLoop.Dispose(); eng.Pause(); };
        DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; dragOver = true; Invalidate(); } };
        DragOver += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; if (!dragOver) { dragOver = true; Invalidate(); } } };
        DragLeave += (s, e) => { dragOver = false; Invalidate(); };
        DragDrop += (s, e) => { dragOver = false; Invalidate(); var fl = (string[])e.Data.GetData(DataFormats.FileDrop); var dropOn = EmptyTrackAt(PointToClient(new Point(e.X, e.Y))); bool firstFile = true; foreach (var f in fl) { if (firstFile && dropOn != null && !f.EndsWith(".ytmproj", StringComparison.OrdinalIgnoreCase)) fillTrack = dropOn; firstFile = false; try { PromptImport(f); } finally { fillTrack = null; } } }; StudioEngine.WarmUp();
        FitView();
    }

    volatile bool moving; PluginManagerForm pluginWin;
    // Redraw only when something is changing; an idle studio repaints once a second (for loading and background state). Input times are stamped so hover and drags stay live.
    DateTime lastInput = DateTime.Now, lastFrame = DateTime.MinValue, lastPlayAt = DateTime.MinValue; int lastW, lastH;
    bool NeedsFrame()
    {
        EnsureUseTimer(); var now = DateTime.Now;
        if (ClientSize.Width != lastW || ClientSize.Height != lastH) { lastW = ClientSize.Width; lastH = ClientSize.Height; lastFrame = now; return true; }
        if (eng.Playing) lastPlayAt = now;
        bool vizPage = tab == 5 || (tab == 1 && visStageOn); bool anim = vizPage || eng.Playing || (tab == 0 && AnyTrackOff()) || busy || browserOpen || PanelAnimating || stylesOpen || aiOpen || autoOpen || dragReg != null || (now - lastInput).TotalMilliseconds < 600 || animOff.Count > 0 || ghostOn || btnAnim || (now - toastAt).TotalSeconds < ToastSecs + 0.2 || (now - lastPlayAt).TotalMilliseconds < 1500 || installing.Length > 0 || mic != null || Auditioner.IsPlaying;   // the 1.5 s after playback lets the meters settle
        double gap = (now - lastFrame).TotalMilliseconds;
        // a slow computer redraws less often (paint time x3) so the window never starves the audio
        lastIdle = !anim; if (!(anim ? gap >= Math.Max(vizPage && !eng.Playing ? 45 : FrameGapMs, Math.Min(250, paintMs * 1.5)) : gap >= 1000)) return false;
        lastFrame = now; return true;
    }
    // Smooth playhead: the audio position moves in 23 ms blocks and runs ahead of what you hear, so the needle is a clock that follows it.
    double dispPos, lastClockS; readonly System.Diagnostics.Stopwatch playClock = System.Diagnostics.Stopwatch.StartNew();
    long DispPos { get { return (long)dispPos; } }
    void UpdatePlayhead()
    {
        double nowS = playClock.Elapsed.TotalSeconds, dt = Math.Min(0.1, nowS - lastClockS); lastClockS = nowS;
        if (eng.Playing)
        {
            double target = eng.HeardPos;   // exactly what you are hearing (the sound card says how much it has played), so the needle and PAUSE agree
            if (Math.Abs(dispPos - target) > 9000) dispPos = target;
            else { dispPos += dt * StudioEngine.SR; dispPos += (target - dispPos) * Math.Min(1.0, dt * 8); }
            { long endF = eng.EndFrame; if (endF > 0 && mic == null && dispPos > endF) dispPos = endF; }   // the reverb / delay tail after the last clip plays on, but the needle stops exactly at the end of the audio (never past the track)
        }
        else dispPos = eng.Pos;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0112 && (((int)m.WParam) & 0xFFF0) == 0xF060) NoteUserX();   // WM_SYSCOMMAND SC_CLOSE: the X was pressed
        Win.TrackMove(ref m); if (m.Msg == 0x231) moving = true; else if (m.Msg == 0x232) moving = false;
        if (m.Msg == 0x020E && tab == 0) { int dlt = (short)(((long)m.WParam >> 16) & 0xFFFF); ScrollView(dlt / 120.0 * 0.15); Invalidate(); }   // touch pad: swipe sideways
        base.WndProc(ref m);
    }

    bool followPaused, wasPlaying;   // followPaused: you scrolled while it plays, so the view no longer chases the needle
    void UserScrolled() { followPaused = true; }
    bool autoFit = true; int lastFitW; long lastFitLen;   // while true, the song keeps filling the track area (full screen, resizing, new recordings)
    void FitView()
    {
        long len = Math.Max(eng.EndFrame, StudioEngine.SR * 10); float w = Math.Max(400, TimelineRect().Width - 24);
        spp = Math.Max(1, len / (double)w); scroll = 0; autoFit = true; followPaused = false;
    }

    void Tick()
    {
        UpdatePlayhead(); Auditioner.Tick(); FlexTick(); PreviewHousekeeping();
        if (mic != null && !eng.Playing) StopMic();
        if (mic != null && !micSilentWarned && mic.AllSilent) { micSilentWarned = true; Toast("Your microphone is sending NO sound (muted or switched off). On a headset: flip the mic down or press its mute button. Press MIC to stop."); }
        { string tt = "Purphex Music  -  " + projName + (Prefs.ShowFps ? "    " + (lastIdle ? "idle" : fpsc.Fps + " fps") : ""); if (Text != tt) Text = tt; }
        if (eng.Playing || (previewEng != null && previewEng.Playing))
        {
            AudioGlitchCheck(DateTime.Now);
        }
        if (eng.Playing && !wasPlaying)
        {   // PLAY never moves the view: it stays exactly where you left it (it used to jump to the needle, forwards or backwards); the view never moves by itself while it plays either
            followPaused = false;
        }
        wasPlaying = eng.Playing;
        if (NeedsFrame0() && Environment.TickCount - eqPaintTick < 600) UpdateSpectrum();
    }

    bool NeedsFrame0() { return eng.Playing || (DateTime.Now - lastInput).TotalMilliseconds < 1500; }

    // ---------------- geometry ----------------
    const int ToolH = 70, HeadFullW = 372, RulerH = 28, HintStripH = 26;
    internal const int HeadTabW = 46;   // the track list tucked away: a thin strip (colour and number of each track)
    int HeadW { get { return (int)Math.Round(HeadTabW + (HeadFullW - HeadTabW) * HeadOpenK()); } }
    DateTime headAt = DateTime.MinValue;
    float HeadOpenK() { float target = Prefs.HeadHidden ? 0f : 1f; double t = (DateTime.Now - headAt).TotalMilliseconds / 180.0; if (t >= 1 || t < 0) return target; float e = (float)(1 - Math.Pow(1 - t, 3)); return (1 - target) + (target - (1 - target)) * e; }
    internal void ToggleHead() { Prefs.HeadHidden = !Prefs.HeadHidden; Prefs.Changed(); headAt = DateTime.Now; Invalidate(); }
    // the arrow on the edge of the track list: hide it for more room to edit, and bring it back
    void HeadHandle(Graphics g, float top)
    {   // it sits in the empty strip above the track list (level with the ruler), so it never covers a track's buttons
        bool open = !Prefs.HeadHidden; var hr = open ? Rc(HeadW - 128, top + 3, 122, RulerH - 6) : Rc(4, top + 3, HeadW - 8, RulerH - 6); bool hot = Hot(hr);
        var gs = g.Save(); g.ResetClip();
        FillRR(g, hot ? TM.C(250, 70, 34, 130) : TM.C(245, 30, 16, 60), hr, 7); if (hot) Glow(g, NeonCyan, hr, 7, 0.8f, 3); StrokeRR(g, Color.FromArgb(hot ? 255 : 200, NeonCyan), hr, 7, 1.4f);
        float d = open ? -1 : 1, cx = open ? hr.X + 16 : hr.X + hr.Width / 2, cy = hr.Y + hr.Height / 2;   // open: the arrows point left (the list goes away); tucked away: right (it comes back)
        using (var pen = new Pen(hot ? Color.White : NeonCyan, 2.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
            foreach (float ox in new[] { -3f, 3f }) g.DrawLines(pen, new[] { new PointF(cx + ox - 3 * d, cy - 6), new PointF(cx + ox + 3 * d, cy), new PointF(cx + ox - 3 * d, cy + 6) });
        if (open) Txt(g, "HIDE TRACKS", 10, hot ? Color.White : Ink, Rc(hr.X + 28, hr.Y, hr.Width - 32, hr.Height), StringAlignment.Near, true);
        var rg = Reg(hr, () => ToggleHead()); rg.Button = true; Hint2(hr, open ? "Hide the track list for more room to edit (the button comes back here)" : "Show the track list: names, volume, mute, solo and the rest");
        if (open)
        {   // ALL PLUGINS and the inspector: always on the left, whatever the panels show
            var ap = Rc(6, top + 3, 118, RulerH - 6); bool apOn = InsShown && insTab == 1; Btn(g, ap, "MIXER", apOn ? 4 : 1, () => { if (apOn) CloseInspector(); else OpenInspector(1); }, 10); Hint2(ap, "The mixer: every track and bus as a strip with its plugins, sends, output, pan and fader (open, add, bypass, move and remove plugins)");
            var ib = Rc(ap.Right + 4, top + 3, Math.Max(60, hr.X - ap.Right - 8), RulerH - 6); Btn(g, ib, InsShown ? "HIDE ▼" : "OPEN ▲", InsShown ? 4 : 0, () => { if (InsShown) CloseInspector(); else OpenInspector(insTab); }, 10); Hint2(ib, InsShown ? "Hide the panel at the bottom" : "Show the panel at the bottom again: channel, mixer, presets, clip and MIDI, piano roll");
        }
        g.Restore(gs);
    }
    // a track while the list is tucked away: its colour, its number, and a level light; click it to pick the track
    void PaintHeaderMini(Graphics g, Track tr, int i, RectangleF r, bool sel)
    {
        var c = Neon(tr.Color); using (var br = new SolidBrush(sel ? TM.C(255, 40, 22, 74) : TM.C(255, 26, 15, 46))) g.FillRectangle(br, r);
        using (var br = new SolidBrush(Color.FromArgb(tr.Off ? 70 : 230, c))) g.FillRectangle(br, r.X + 3, r.Y + 4, 4, r.Height - 8);
        Txt(g, (i + 1).ToString(), 15, sel ? Color.White : Ink, Rc(r.X + 8, r.Y + 8, r.Width - 10, 22), StringAlignment.Center, true);
        float live = Math.Min(1f, Math.Max(tr.PeakL, tr.PeakR)); var m = Rc(r.X + r.Width / 2 - 2, r.Y + 34, 6, Math.Max(10, r.Height - 44)); FillRR(g, TM.C(160, 10, 6, 20), m, 3);
        if (live > 0.001f) { float mh = m.Height * live; FillRR(g, Color.FromArgb(230, c), Rc(m.X, m.Bottom - mh, m.Width, mh), 3); }
        var trH = tr; Reg(r, () => { busEdit = null; selTrack = Math.Max(0, eng.Tracks.IndexOf(trH)); selFx = -1; toolsTrack = trH; }); Hint2(r, tr.Name + "  (the track list is tucked away: the arrow brings it back)");
    }
    int TransH { get { return tab == 0 ? 86 + HintStripH : 86; } }   // on the studio page the transport area has the hint strip along its bottom edge
    int PanelW { get { if (soundsOpen && tab == 0) return sndFull ? Math.Max(640, ClientSize.Width) : 640; return (int)Math.Round(PanelTabW + (380 - PanelTabW) * PanelOpenK()); } }
    // the plugin panel can be tucked away for a big timeline: a thin tab stays on the right edge; it slides out and back in 0.18 s
    internal const int PanelTabW = 30; internal DateTime panelAt = DateTime.MinValue;
    float PanelOpenK() { float target = Prefs.PanelHidden ? 0f : 1f; double t = (DateTime.Now - panelAt).TotalMilliseconds / 180.0; if (t >= 1 || t < 0) return target; float e = (float)(1 - Math.Pow(1 - t, 3)); return (1 - target) + (target - (1 - target)) * e; }
    bool PanelAnimating { get { double ms = (DateTime.Now - panelAt).TotalMilliseconds; return ms >= 0 && ms < 260; } }
    internal void TogglePanel() { Prefs.PanelHidden = !Prefs.PanelHidden; Prefs.Changed(); panelAt = DateTime.Now; Invalidate(); }
    void ShowPanel() { if (Prefs.PanelHidden && !InsShown) TogglePanel(); }   // (while the inspector is open it is the place for plugins: the right panel stays tucked away)
    // the handle: a tall glowing tab half way down the panel's left edge, sticking out over the timeline, with a big arrow
    void PanelHandle(Graphics g, float px, float py, float ph, bool open)
    {
        float hw = 26, hh = 96; var hr = Rc(px - hw + 6, py + ph / 2 - hh / 2, hw, hh); bool hot = Hot(hr);
        FillRR(g, hot ? TM.C(250, 70, 34, 130) : TM.C(245, 34, 18, 66), hr, 10); Glow(g, NeonCyan, hr, 10, hot ? 1f : 0.7f, 4); StrokeRR(g, Color.FromArgb(hot ? 255 : 220, NeonCyan), hr, 10, 1.8f);
        float cx = hr.X + hr.Width / 2, cy = hr.Y + hr.Height / 2, d = open ? 1 : -1;   // open: the arrow points right (the panel goes away); tucked away: it points left (it comes back)
        using (var pen = new Pen(Color.FromArgb(255, hot ? Color.White : NeonCyan), 3.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
        {
            foreach (float ox in new[] { -4f, 4f }) g.DrawLines(pen, new[] { new PointF(cx + ox - 4 * d, cy - 12), new PointF(cx + ox + 4 * d, cy), new PointF(cx + ox - 4 * d, cy + 12) });   // a big double arrow
        }
        Reg(hr, () => TogglePanel()); Hint2(hr, open ? "Hide the plugin panel for a bigger timeline (this handle brings it back)" : "Show the plugin panel: add, edit and save plugins");
    }   // the right-hand panel is wider while the sound browser is open
    int LaneH = InitLaneH();   // track height: Alt + mouse wheel makes the tracks taller so the waveform shows more detail
    // the scroll bar of the arrangement has its own band above the timeline ruler; the arrangement (ruler, rows, headers) starts under it
    internal int OverH { get { return tab == 0 ? 46 : 0; } }
    internal float ArrTop { get { return ToolH + TransH + OverH; } }
    // the gap under the rows: when the tracks run off the bottom, room for the pinned + ADD TRACK button (it never covers a track's buttons)
    float ArrGap { get { return LaneCount * LaneH + 50 > MixH - ArrTop - RulerH - 6 ? 46 : 6; } }
    RectangleF TimelineRect() { return new RectangleF(HeadW, ArrTop + RulerH, Math.Max(100, ClientSize.Width - HeadW - PanelW), Math.Max(100, MixH - ArrTop - RulerH - 6)); }

    // ---------------- drawing helpers ----------------
    static GraphicsPath RR(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void FillRR(Graphics g, Color c, RectangleF r, float rad)
    {
        long t0 = Prof.T();
        if (r.Width * r.Height > 40000 && rad >= 1 && r.Width > rad * 4 && r.Height > rad * 4)
        {   // a big panel: the straight middle part is filled as plain rectangles (a smoothed path over a million pixels is slow); only the rounded corners are smoothed. Panels are nearly solid, so they are made fully solid
            if (c.A >= 224) c = Color.FromArgb(255, c); else if (Prof.On) Prof.Note("ALPHA " + new System.Diagnostics.StackFrame(1, true).GetMethod().Name + " " + (int)r.Width + "x" + (int)r.Height + " a" + c.A);
            float d = rad, x = r.X, y = r.Y, w = r.Width, h = r.Height; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None; var po = g.PixelOffsetMode;
            using (var b = new SolidBrush(c))
            {
                float x0 = (float)Math.Round(x), y0 = (float)Math.Round(y), x1 = (float)Math.Round(x + w), y1 = (float)Math.Round(y + h);
                g.FillRectangle(b, x0 + d, y0, x1 - x0 - 2 * d, d); g.FillRectangle(b, x0, y0 + d, x1 - x0, y1 - y0 - 2 * d); g.FillRectangle(b, x0 + d, y1 - d, x1 - x0 - 2 * d, d);
                g.SmoothingMode = SmoothingMode.AntiAlias; g.FillPie(b, x0, y0, d * 2, d * 2, 180, 90); g.FillPie(b, x1 - d * 2, y0, d * 2, d * 2, 270, 90); g.FillPie(b, x1 - d * 2, y1 - d * 2, d * 2, d * 2, 0, 90); g.FillPie(b, x0, y1 - d * 2, d * 2, d * 2, 90, 90);
            }
            g.SmoothingMode = sm; if (Prof.On && (Stopwatch0() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > 0.3) Prof.Note("FAST " + new System.Diagnostics.StackFrame(1, true).GetMethod().Name + ":" + new System.Diagnostics.StackFrame(1, true).GetFileLineNumber() + " " + (int)r.Width + "x" + (int)r.Height + " a" + c.A + " " + ((Stopwatch0() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.00") + "ms"); Prof.A("FillRR", t0); return;
        }
        using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); if (Prof.On && r.Width * r.Height > 20000) Prof.Note("FillRR " + (int)r.Width + "x" + (int)r.Height + " a" + c.A + " " + ((Stopwatch0() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.00") + "ms"); Prof.A("FillRR", t0);
    }
    static long Stopwatch0() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
    static void StrokeRR(Graphics g, Color c, RectangleF r, float rad, float w) { long t0 = Prof.T(); using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); Prof.A("StrokeRR", t0); }
    // Test hook: when this list exists, every string drawn is recorded with where its ink really lands, so a test can find text that runs out of its box or lands on other text.
    internal sealed class AuditText { public string S; public RectangleF Box, Ink; public bool Cut; public Color C; public float Px; public bool Bold; }
    internal static List<AuditText> TxtAudit;
    internal static bool NoTextDraw;   // test hook: the writing is measured and recorded but not drawn (to see what is behind it)
    static void Txt(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    {
        if (EmojiText.Has(s)) { EmojiText.Draw(g, s, px, c, r, ha, bold); return; }   /* emoji need their own font */
        float real = GfxCache.Grow(px); var f = GfxCache.Exact(real, bold); long t2 = Prof.T();   // as big as the box allows: the grown size, a little smaller when it does not fit
        if (s != null && s.Length > 3 && r.Width > 30 && s.Length * real * 0.5f > r.Width * 0.7f)
        {   // a name that does not fit its box is written a little smaller instead of being cut off
            float w0 = g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4;
            if (w0 > r.Width - 4) { float mn = Math.Max(8f, px * 0.72f); while (real > mn && w0 > r.Width - 4) { real -= 0.5f; w0 = g.MeasureString(s, GfxCache.Exact(real, bold), 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4; } f = GfxCache.Exact(real, bold); }
        }
        px = real;
        Prof.A("Txt.measure", t2); if (TxtAudit != null && !string.IsNullOrWhiteSpace(s) && r.Width > 1 && r.Height > 1)
        {
            var sz = g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)); float w = Math.Min(sz.Width - 4, r.Width), ih = Math.Min(sz.Height * 0.72f, r.Height); float x = ha == StringAlignment.Near ? r.X : (ha == StringAlignment.Center ? r.X + (r.Width - w) / 2 : r.Right - w);
            var ink = new RectangleF(x + 1, r.Y + (r.Height - ih) / 2, Math.Max(1, w - 2), Math.Max(1, ih)); var seen = RectangleF.Intersect(ink, g.ClipBounds);   // only what can really be seen: a list row scrolled out of view is not on screen
            if (!seen.IsEmpty) TxtAudit.Add(new AuditText { S = s, Box = r, Ink = seen, Cut = sz.Width - 4 > r.Width + 2 && seen.Width >= ink.Width - 1, C = c, Px = px, Bold = bold });
        }
        long t1 = Prof.T(); if (!NoTextDraw) using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, GfxCache.Fmt(ha, true)); Prof.A("DrawString", t1);
    }
    // writing that wraps onto as many lines as its box allows (long explanations): nothing is cut off with ... unless the box really is too small
    static void TxtWrap(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha = StringAlignment.Near)
    {
        if (EmojiText.Has(s)) { EmojiText.DrawWrap(g, s, px, c, r, ha); return; }
        float real = GfxCache.Grow(px); var f = GfxCache.Exact(real, false);
        if (s != null && r.Width > 1 && r.Height > 1 && s.Length * real * 0.45f > r.Width * 0.7f)   // more than a line or two: made smaller (never below the old size) until all of it fits its box
        { var sz0 = g.MeasureString(s, f, (int)r.Width); while (sz0.Height > r.Height + 1 && real > px) { real -= 0.5f; if (real < px) real = px; f = GfxCache.Exact(real, false); sz0 = g.MeasureString(s, f, (int)r.Width); } }
        px = real;
        using (var fmt = new StringFormat { Alignment = ha, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord })
        {
            if (TxtAudit != null && !string.IsNullOrWhiteSpace(s) && r.Width > 1)
            {
                var sz = g.MeasureString(s, f, (int)r.Width); var ink = new RectangleF(r.X + 1, r.Y + 1, Math.Max(1, Math.Min(sz.Width - 4, r.Width)), Math.Max(1, Math.Min(sz.Height * 0.8f, r.Height))); var seen = RectangleF.Intersect(ink, g.ClipBounds);
                if (!seen.IsEmpty) TxtAudit.Add(new AuditText { S = s, Box = r, Ink = seen, Cut = sz.Height > r.Height + 2, C = c, Px = px });
            }
            if (!NoTextDraw) using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, fmt);
        }
    }
    static RectangleF Rc(float x, float y, float w, float h) { return new RectangleF(x, y, w, h); }
    static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

    int regLayer;   // goes up by one for every region that covers the whole window (a pop-up sheet starting): nothing under a pop-up can take its clicks
    Graphics regG;   // the picture being painted: its clip is where a click area can reach
    HitReg Reg(RectangleF r, Action down)
    {
        if (regG != null) { try { if (!regG.IsClipEmpty) { var cb = regG.ClipBounds; if (cb.Width < 100000) r = cb.IntersectsWith(r) ? RectangleF.Intersect(r, cb) : new RectangleF(r.X, r.Y, 0, 0); } } catch { } }
        return Reg0(r, down);
    }
    HitReg Reg0(RectangleF r, Action down) { if (r.Width >= ClientSize.Width - 2 && r.Height >= ClientSize.Height - 2) regLayer++; var g = new HitReg { R = r, OnDown = down, Layer = regLayer }; regs.Add(g); return g; }
    bool Hot(RectangleF r) { return hover != null && hover.R == r; }

    // Plain-English hints shown at the bottom while the mouse is over a button.
    string hintNow = "", stripHint = "";   // stripHint: the hint of the last frame, shown in the studio's hint strip
    static readonly Dictionary<string, string> Hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { "ADD MUSIC", "Add a song, a vocal recording or a beat. You can also drop files onto this window." },
        { "SPLIT VOCALS + BEAT", "Splits the selected FULL SONG into a vocals track and an instrumental track (AI)." },
        { "AUTO MIX", "Balances the levels and builds an effect chain on every track, then lists what it changed." },
        { "Undo mix", "Puts everything back the way it was before Auto Mix." },
        { "EXPORT", "Save the finished mix, the vocals, the instrumental or all stems as WAV, FLAC, MP3, or an MP4 video with your project picture." },
        { "Help", "Open the guides. Each one can show you on screen where to click." },
        { "Open", "Open a saved project (Ctrl+O)." }, { "Save", "Save this project (Ctrl+S)." },
        { "Save as", "Save a copy under a new name (Ctrl+Shift+S)." },
        { "Full", "Full screen (F11). Esc leaves it." }, { "Window", "Leave full screen (Esc)." },
        { "PLAY", "Play from the needle (Space)." }, { "PAUSE", "Pause (Space)." }, { "STOP", "Stop where you are (the needle stays put)." }, { "|<", "Jump back to the start." },
        { "LOOP", "Loop the whole song, or drag a section on the timeline to hear it again and again." },
        { "MIX", "The tracks, faders and effects." }, { "≡", "Menu: new, open, recent projects, my projects with pictures, edit, tools, community, settings and help." }, { "+ TRACK", "Add a track: audio from your PC or an instrument, above or below the selected track or at the end." }, { "+ ADD TRACK", "Add a track at the end of the list." }, { "LYRICS", "A page for writing your own lyrics, with line and syllable counts." }, { "MASTERING", "Final loudness and polish on the whole mix, with LUFS meters and A/B compare." },
        { "+ Add Effect", "Pick an effect (EQ, compressor, reverb, Auto-Tune...) to add to this track." },
        { "+ Add Effect to master", "Add an effect to the final mix instead of one track." },
        { "VSTS", "Bass Lab, your VST instruments and the Purphex VSTs: choose the track it goes on (FOR) and add one" }, { "+ ADD YOUR OWN", "Add your own VST plugins: pick the folders they are in, and they show up in this list" }, { "MIXER", "Every track and its plugins in one place: add, paste, copy and open plugins on any track, and copy a whole preset to another track" }, { "ALL BUSES", "Every bus in one place: its level, what is sent to it and its plugins" },
        { "Bass Lab", "Add the Bass Lab synthesizer to this track: wobble bass and leads with oscillators, warp, filters, LFOs and a matrix. Its window opens so you can play it and shape the sound." }, { "Presets...", "Load a ready-made effect chain (a rap vocal chain, the instrumental chain or a bus preset like echo, ad-lib or reverb), or save your own." },
        { "Bypass", "Turn this effect off without removing it." }, { "ON", "This effect is on: press to turn it off (it stays on the track)." }, { "OFF", "This effect is off: the sound passes untouched. Press to turn it on." }, { "Presets", "Load or save settings for this effect." }, { "FX presets", "Load or save settings for this one effect. (Whole mastering chains are in the PRESETS tab above.)" },
        { "^", "Move this effect up (it runs earlier)." }, { "v", "Move this effect down (it runs later)." },
        { "x", "Remove it." }, { "M", "Mute this track." }, { "S", "Solo: hear only this track." },

        { "MIDI REC", "Record MIDI: switch it on (it turns red), press PLAY, then play your MIDI keyboard or the piano window. The notes are recorded onto the selected MIDI track. Press it again to switch it off." },
        { "Piano", "Open the instrument window: its own controls plus a piano you can play." }, { "C", "Clear the recorded notes on this instrument track." },
        { "+ Effect", "Add an effect to this instrument track (EQ, reverb...)." },
        { "FLEX", "Flex Pitch: see every note of the selected recording and drag notes up or down to re-tune them, steady the pitch, take out vibrato, change volume and formants." },
        { "SPLIT", "Split the clip under the needle in two (key B). Split as many times as you like: each piece can be moved, trimmed, faded and given its own gain." },
        { "DUP", "Make a copy of the selected track." }, { "DEL", "Delete the selected track (Undo brings it back)." },
        { "UNDO", "Undo the last edit (Ctrl+Z)." }, { "REDO", "Redo (Ctrl+Y)." }, { "DETECT BPM", "Listen to the song and set the BPM for you." }, { "Detect Track key", "Find the musical key of the selected track and show it." },
        
        { "COUNT IN", "Count In: when it is on, PLAY and recording first count 1 2 3 4, one a second (the tempo does not change it), so you can get ready. The number shows on screen." }, { "METRONOME", "Metronome: a click on every beat while the song plays. Click to switch it on or off (it is lit while it is on)." },
        { "-", "Zoom out." }, { "+", "Zoom in." }, { "FIT", "Zoom to fit the whole song." },
        { "MIC", "Microphone: press it to get ready (it pulses red), then press PLAY to record onto the selected track (Count In gives you four clicks first). Press it again to cancel, or while recording to finish. Use headphones." }, 
        { "NEW", "Start a new empty project." },
        { "Add", "Add this effect." }, { "Cancel (Esc)", "Stop the current job." },
        { "STEREO", "Keep this track in STEREO: left and right are kept apart (lit when it is stereo)." }, { "MONO", "Make this track MONO: left and right play the same sound (lit when it is mono)." }, { "THIN TRACKS", "Squeeze every track into a thin lane so more tracks fit on the screen. Press it again to go back to normal size." }, { "BACK", "Go back one page, to the page you were on before." }, { "HOME", "Go home: the main studio (Mix). It also forgets the pages you have been through." },
        { "ADD SECTIONS", "Put [Verse 1], [Chorus], [Pre-chorus], [Bridge]... on the parts of your lyrics that have no label yet. Parts you already labelled stay as they are. Ctrl+Z takes it back." },
        { "POPULAR", "Show the presets people use the most first." },
        { "NEWEST", "Show the most recently shared presets first." },
        { "TRY AGAIN", "Ask the Purphex community for the list again." },
        { "USE", "Use this community preset now." },
        { "Report", "Report this preset as spam or something that should not be shared. Presets reported by several people are hidden." },
        { "SHARE THE THEME I AM USING", "Share the colours you are using now with everyone. You choose the name. Only share themes you made." }, { "SHARE MY CURRENT SETTINGS", "Share the sound you have set up right now with everyone. You choose the name. Only share sounds you made." },
        { "Edit profile", "Change your producer name and picture." },
        { "+  New project", "Start a new empty project." },
        { "Join the Discord", "Open the Purphex Discord server in your browser." },
        { "Change picture", "Choose a picture for your profile." },
        { "Correct pitch", "Snap the selected notes (or all notes) to the nearest note of the chosen scale." },
        { "Select all", "Select every note in the recording (Ctrl+A)." },
        { "Reset note", "Put the selected notes back exactly as they were recorded." },
        { "Original", "Go back to the original recording and clear every Flex Pitch edit." },
        { "DONE (keep edits)", "Close Flex Pitch and keep your edits. They are also saved automatically as you work." },
        { "Open .txt", "Load lyrics from a text file. You can replace what you have or add the file to the end." },
        { "Save .txt", "Save these lyrics as a text file." },
        { "Clear", "Empty the lyrics page (it asks first)." },
        { "Copy", "Copy the lyrics to the clipboard." },
        { "Back to the tracks", "Close this and go back to your tracks." },
        { "+ New bus", "Make a new bus: a shared channel that several tracks can send to and share plugins on." },
        { "Import", "Add your own sound files to the Sounds collection." },
        { "Reset to 0", "Put this piece back to its normal volume (0 dB)." },
        { "Flatten", "Remove the curve and keep the setting where it is now." },
        { "Remove", "Remove this automation lane." },
        { "SOUNDS", "Open or close the Purphex Sounds panel: drums, samples and the Bass Lab sounds." },
        { "SOUND PACKS", "Open or close the sound packs: drums, samples, loops and the Bass Lab sounds." },
        { "Vocal Styles", "Rapper Vocal Styles: pick a ready-made plugin chain for rap vocals and put it on this track." },
        { "Plugins...", "Manage extra plugins (VST) that Purphex can load." },
        { "Load your own impulse response...", "Load a recording of a real room or speaker to use as the reverb." },
        { "Detect key", "Listen to this track and show its musical key." },
        { "Natural", "Gentle, natural-sounding pitch correction." },
        { "Hard Auto-Tune", "The classic hard Auto-Tune effect: notes snap instantly." },
        { "Reset meters", "Clear the loudness meters and start measuring again." },
        { "A  ORIGINAL", "Listen to the mix as it was before mastering." },
        { "B  MASTERED", "Listen to the mix with the mastering chain on." },
        { "Analyze whole mix", "Measure the loudness and tone of the whole mix and suggest EQ moves." },
        { "Set loudness to target (adjusts the limiter)", "Raises or lowers the limiter on the master so the mix lands on the target loudness. The slider only picks the number: this button does the work." },
        { "Install", "Download and install this pack." },
        { "Browse", "Look through the sounds in this pack." },
        { "Packs", "Show the sound packs you can install." },
        { "Folder", "Open the folder that holds these sounds." },
        { "Licence", "Read the licence for these sounds." },
        { "Collection...", "Open your collection of favourite sounds." },
        { "Remake", "Make this sound again with fresh random settings." },
        { "Import...", "Add your own sound files." },
    };

    // style: 0 normal, 1 accent, 2 hot gradient, 3 danger, 4 toggle-on
    // buttons glide into their hover look instead of snapping (a little hover amount per button, 0 to 1)
    readonly Dictionary<long, float> hoverT = new Dictionary<long, float>(); bool btnAnim;
    float HoverAmount(RectangleF r, string text)
    {
        long key = ((long)(int)r.X << 42) ^ ((long)(int)r.Y << 24) ^ ((long)(int)r.Width << 8) ^ (long)(uint)text.GetHashCode(); float t; hoverT.TryGetValue(key, out t); float nt = Prefs.PerfMode ? (Hot(r) ? 1f : 0f) : (Hot(r) ? t + (1 - t) * 0.4f : t * 0.7f);   // PERFORMANCE MODE: no gliding
        if (nt < 0.015f) nt = 0; else if (nt > 0.985f) nt = 1; if (nt == 0) hoverT.Remove(key); else { if (hoverT.Count > 400) hoverT.Clear(); hoverT[key] = nt; if (nt < 1) btnAnim = true; } return nt;
    }
    HitReg Btn(Graphics g, RectangleF r, string text, int style, Action down, float px = 15)
    {
        long b0 = Prof.T(); var res = Btn0(g, r, text, style, down, px); Prof.A("Btn", b0); return res;
    }
    HitReg Btn0(Graphics g, RectangleF r, string text, int style, Action down, float px)
    {
        long k0 = Prof.T(); btnRects.Add(r); if (!btnMap.ContainsKey(text)) btnMap[text] = r; if (Hot(r)) { string hh; if (Hints.TryGetValue(text, out hh)) hintNow = hh; } Prof.A("btn.a", k0); k0 = Prof.T();
        float ht = HoverAmount(r, text); if (ht > 0.5f && Hot(r)) HoverSparks(r); Prof.A("btn.hover", k0); k0 = Prof.T();
        string shown0 = Win.Up(text); Prof.A("btn.up", k0);
        if (TxtAudit == null) DrawBtnHalo(g, r, style, ht);   // the galaxy flare behind it
        if (TxtAudit == null && r.Width > 4 && r.Height > 4 && r.Width * r.Height < 90000) BtnDrawSprite(g, r, shown0, style, ht, px); else BtnLook(g, r, shown0, style, ht, px);
        k0 = Prof.T(); var rr0 = Reg(r, down); rr0.Button = true; Prof.A("btn.reg", k0); return rr0;
    }
    // the Fantasy edition has the button of the converter window: a pale rim all round, and inside it a soft shaded button (hot buttons fade from violet to pink); a glow under the mouse.
    // Every other edition keeps its own look (BtnLookClassic) and never gets this one.
    void BtnLook(Graphics g, RectangleF r, string shown, int style, float ht, float px)
    {
        if (!Edition.Has(Tier.Fantasy)) { BtnLookClassic(g, r, shown, style, ht, px); return; }
        Color c = style == 1 ? Violet : (style == 3 ? TM.C(255, 150, 40, 80) : (style == 4 ? (UiTokens.Custom ? UiTokens.Accent : TM.C(255, 150, 70, 230)) : BtnC));
        if (ht > 0) c = Color.FromArgb(Math.Min(255, c.R + (int)(28 * ht)), Math.Min(255, c.G + (int)(22 * ht)), Math.Min(255, c.B + (int)(30 * ht)));
        c = Contrast.ForWhite(c); if (style == 4 && UiTokens.Glass) c = Color.FromArgb(150, c);   // a translucent accent: the selected button is see-through
        Color vA = Contrast.ForWhite(Color.FromArgb(Math.Min(255, Violet.R + 32), Math.Min(255, Violet.G + 8), Math.Min(255, Violet.B + 28))), vB = Contrast.ForWhite(Accent2);
        float rad = Math.Min(10f, Math.Max(6f, r.Height * 0.3f)); const float rim = 2.6f; var inner = RectangleF.Inflate(r, -rim, -rim); float irad = Math.Max(1f, rad - rim);   // the inner corner is exactly one rim smaller than the outer one, so the rim is the same thickness all the way round (corners too) on every button
        Color rimC = Color.FromArgb(style == 4 && UiTokens.Glass ? 70 : 240, (int)(c.R + (255 - c.R) * 0.82f), (int)(c.G + (255 - c.G) * 0.82f), (int)(c.B + (255 - c.B) * 0.84f));
        using (var p = RR(r, rad)) using (var br = new SolidBrush(rimC)) g.FillPath(br, p);   // the pale rim
        if (style == 2) { using (var p = RR(inner, irad)) using (var br = new LinearGradientBrush(inner, vA, vB, 0f)) g.FillPath(br, p); }
        else using (var p = RR(inner, irad)) using (var br = new LinearGradientBrush(inner, Color.FromArgb(c.A, Math.Min(255, (int)(c.R * 1.10f) + 4), Math.Min(255, (int)(c.G * 1.10f) + 4), Math.Min(255, (int)(c.B * 1.10f) + 4)), Color.FromArgb(c.A, (int)(c.R * 0.88f), (int)(c.G * 0.88f), (int)(c.B * 0.88f)), 90f)) g.FillPath(br, p);
        if (style == 4 && UiTokens.Shiny) PaintMaterial(g, inner, irad, c, UiTokens.Finish, false);   // the accent's finish on a selected button
        if (inner.Width > irad * 3) using (var hl = new Pen(Color.FromArgb(style == 2 ? 80 : 52, 255, 255, 255), 1f)) g.DrawLine(hl, inner.X + irad, inner.Y + 1.2f, inner.Right - irad, inner.Y + 1.2f);
        if (style == 4) Glow(g, UiTokens.Sel(Accent2), r, rad, 0.8f, 3); else if (ht > 0.03f) Glow(g, NeonCyan, r, rad, 0.7f * ht, 3);
        bool bdt = true; float fpx = px, avail = r.Width - 12;   // a label that is too wide for its button gets a smaller size instead of being cut off
        if (shown.Length * fpx * 0.55f > avail * 0.7f) { while (fpx > 7f && g.MeasureString(shown, GfxCache.Exact(GfxCache.Grow(fpx), bdt)).Width > avail) fpx -= 0.5f; }
        Color lab = Color.White;   // always white
        if (shown.Length <= 2 && shown.Trim().Length > 0 && !EmojiText.Has(shown) && !HasLetterOrDigit(shown)) GlyphCentred(g, shown, fpx, lab, r, bdt); else Txt(g, shown, fpx, lab, r, StringAlignment.Center, bdt);
    }
    // the button of the Supporter edition: as it always was (the new look below is for the Fantasy edition only)
    void BtnLookClassic(Graphics g, RectangleF r, string shown, int style, float ht, float px)
    {
        Color c = style == 1 ? Violet : (style == 3 ? TM.C(255, 150, 40, 80) : (style == 4 ? (UiTokens.Custom ? UiTokens.Accent : TM.C(255, 150, 70, 230)) : BtnC));
        if (ht > 0) c = Color.FromArgb(Math.Min(255, c.R + (int)(28 * ht)), Math.Min(255, c.G + (int)(22 * ht)), Math.Min(255, c.B + (int)(30 * ht)));
        c = Contrast.ForWhite(c); if (style == 4 && UiTokens.Glass) c = Color.FromArgb(150, c);   // white writing: a theme that makes a button too light gets a darker button, not black writing
        Color vA = Contrast.ForWhite(Color.FromArgb(Math.Min(255, Violet.R + 32), Math.Min(255, Violet.G + 8), Math.Min(255, Violet.B + 28))), vB = Contrast.ForWhite(Accent2);
        float rad = Math.Min(10f, Math.Max(6f, r.Height * 0.3f));   // softer corners on every button
        if (style == 2)
        {
            using (var p = RR(r, rad)) using (var br = new LinearGradientBrush(r, vA, vB, 0f)) g.FillPath(br, p);
            if (ht > 0) FillRR(g, Color.FromArgb((int)(40 * ht), 255, 255, 255), r, rad);
        }
        else using (var p = RR(r, rad)) using (var br = new LinearGradientBrush(r, Color.FromArgb(c.A, Math.Min(255, (int)(c.R * 1.07f) + 2), Math.Min(255, (int)(c.G * 1.07f) + 2), Math.Min(255, (int)(c.B * 1.07f) + 2)), Color.FromArgb(c.A, (int)(c.R * 0.93f), (int)(c.G * 0.93f), (int)(c.B * 0.93f)), 90f)) g.FillPath(br, p);   // a soft shade from top to bottom
        if (style == 4 && UiTokens.Shiny) PaintMaterial(g, r, rad, c, UiTokens.Finish, false);   // the accent's finish on a selected button
        if (Edition.Has(Tier.Fantasy))
        {   // the Fantasy look: a lit rim in a lighter shade of the button's own colour, a glassy top and a bright line where the light catches the edge
            { Color rimBase = style == 2 ? Violet : c; Color rim = Color.FromArgb(Math.Min(255, (int)(rimBase.R + (255 - rimBase.R) * (0.42f + 0.30f * ht))), Math.Min(255, (int)(rimBase.G + (255 - rimBase.G) * (0.42f + 0.30f * ht))), Math.Min(255, (int)(rimBase.B + (255 - rimBase.B) * (0.42f + 0.30f * ht)))); StrokeRR(g, Color.FromArgb(215, rim), r, rad, 1.4f); }
            if (r.Height >= 18) { var top = Rc(r.X + 1.5f, r.Y + 1.5f, r.Width - 3, (r.Height - 3) * 0.46f); using (var p = RR(top, Math.Max(3f, rad - 1.5f))) using (var br = new LinearGradientBrush(top, Color.FromArgb(style == 2 ? 58 : 40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p); }
            if (r.Width > rad * 3) using (var hl = new Pen(Color.FromArgb(style == 2 ? 120 : 80, 255, 255, 255), 1f)) g.DrawLine(hl, r.X + rad, r.Y + 1.5f, r.Right - rad, r.Y + 1.5f);
        }
        else
        {   // the other editions keep the simpler look
            StrokeRR(g, TM.C(style == 2 ? 200 : 110, 200, 150, 255), r, rad, 1);
            if (r.Width > rad * 3) using (var hl = new Pen(Color.FromArgb(style == 2 ? 70 : 40, 255, 255, 255), 1f)) g.DrawLine(hl, r.X + rad, r.Y + 1.5f, r.Right - rad, r.Y + 1.5f);   // the light catching the top edge
        }
        if (style == 4) Glow(g, UiTokens.Sel(Accent2), r, rad, 0.8f, 3); else if (ht > 0.03f) Glow(g, NeonCyan, r, rad, 0.7f * ht, 3); else if (style == 2) Glow(g, NeonViolet, r, rad, 0.5f, 3);
        bool bdt = true; float fpx = px, avail = r.Width - 10;   // a label that is too wide for its button gets a smaller size instead of being cut off
        if (shown.Length * fpx * 0.55f > avail * 0.7f) { while (fpx > 7f && g.MeasureString(shown, GfxCache.Exact(GfxCache.Grow(fpx), bdt)).Width > avail) fpx -= 0.5f; }
        Color lab = Color.White;   // always white
        if (shown.Length <= 2 && shown.Trim().Length > 0 && !EmojiText.Has(shown) && !HasLetterOrDigit(shown)) GlyphCentred(g, shown, fpx, lab, r, bdt); else Txt(g, shown, fpx, lab, r, StringAlignment.Center, bdt);
    }

    // a one or two character button (the menu, X, +, -, <, >, |<, >>...): a font puts the glyph where its line box says, which is often above or beside the middle of the picture.
    // So the glyph is drawn by its own ink: the three lines of the menu exactly, any other mark moved until its ink is in the middle of the button
    static bool HasLetterOrDigit(string s) { foreach (char c in s) if (char.IsLetterOrDigit(c)) return true; return false; }   // (letters keep their normal writing; only marks such as the menu, +, - and the arrows are centred by their ink)
    void GlyphCentred(Graphics g, string s, float px, Color c, RectangleF r, bool bold)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (s == "\u2261")
        {
            float lw = Math.Min(r.Width * 0.46f, px * 0.9f), th = Math.Max(2f, px * 0.1f), gap = Math.Max(4f, px * 0.25f), cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (var b = new SolidBrush(c)) for (int i = -1; i <= 1; i++) { var ln = new RectangleF(cx - lw / 2f, cy + i * (gap + th) - th / 2f, lw, th); using (var p = RR(ln, th / 2f)) g.FillPath(b, p); }
            g.SmoothingMode = sm; return;
        }
        var f = GfxCache.Exact(GfxCache.Grow(px), bold); float em = f.Unit == GraphicsUnit.Pixel ? f.Size : f.Size * g.DpiY / 72f;
        using (var path = new GraphicsPath())
        {
            path.AddString(s, f.FontFamily, (int)f.Style, em, PointF.Empty, StringFormat.GenericTypographic); var bb = path.GetBounds();
            if (bb.Width < 0.5f || bb.Height < 0.5f) { g.SmoothingMode = sm; Txt(g, s, px, c, r, StringAlignment.Center, bold); return; }
            using (var m = new System.Drawing.Drawing2D.Matrix()) { m.Translate(r.X + (r.Width - bb.Width) / 2f - bb.X, r.Y + (r.Height - bb.Height) / 2f - bb.Y); path.Transform(m); }
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }
        g.SmoothingMode = sm;
    }

    // horizontal slider; frac 0..1; returns region so callers can add OnDouble (the wheel never moves a slider: click and drag only)
    HitReg Slider(Graphics g, RectangleF r, float frac, Action<float> set, Color? fill = null)
    {
        float ty = r.Y + r.Height / 2 - 3; var tr = Rc(r.X, ty, r.Width, 6);
        FillRR(g, TM.C(180, 10, 4, 22), tr, 3);
        float fx = r.X + r.Width * Clamp01(frac); if (fx - r.X > 1) FillRR(g, fill ?? Accent, Rc(r.X, ty, fx - r.X, 6), 3);
        using (var b = new SolidBrush(TM.C(255, 255, 236, 255))) g.FillEllipse(b, fx - 6, r.Y + r.Height / 2 - 6, 12, 12);
        var reg = new HitReg { R = r };
        reg.OnDown = () => set(Clamp01((MousePos.X - r.X) / r.Width));
        reg.OnDrag = pt => set(Clamp01((pt.X - r.X) / r.Width));
        reg.Layer = regLayer; reg.Button = true; regs.Add(reg); return reg;   // a slider is a control: on top of what is under it, in the pop-up it is drawn in
    }

    PointF MousePos;

    static string Db(double v) { return double.IsNaN(v) || v < -99 ? "-inf" : v.ToString("0.0", CultureInfo.InvariantCulture); }

    // ---------------- main paint ----------------
    // CRACKLE: the moment the sound breaks up (two starved buffers within two seconds, or the sound card stopping for a third of a second) the output is refreshed: a split second, then clean sound.
    // Each time the buffer is also made a step bigger (for this session only; your choice in Settings is kept), until the biggest; after that a plain refresh. At most one refresh every 1.5 seconds.
    int lastStalls; DateTime lastRefresh = DateTime.MinValue;
    internal bool AudioGlitchCheck(DateTime now)
    {
        var ge = eng.Playing ? eng : (previewEng != null && previewEng.Playing ? previewEng : null);   // the engine you are hearing: the mix, or a preview (My Projects, Community)
        if (ge == null) { lastUnder = WaveOutPlayer.Underruns; lastStalls = WaveOutPlayer.Stalls; underSince = now; return false; }
        int u = WaveOutPlayer.Underruns, st = WaveOutPlayer.Stalls; bool stalled = st != lastStalls, breaking = u - lastUnder >= 2 && (now - underSince).TotalSeconds <= 2;
        if ((now - underSince).TotalSeconds > 2) { underSince = now; lastUnder = u; }
        if (!stalled && !breaking) return false;
        lastUnder = u; lastStalls = st; underSince = now; if ((now - lastRefresh).TotalMilliseconds < 1500) return false; lastRefresh = now;
        bool bigger = Prefs.BufEff < 2; if (bigger) Prefs.BufBoost++; ge.ReopenOutput();
        Toast(bigger ? "The sound was breaking up, so I refreshed the audio and made the playback buffer bigger for now (your choice in Settings is kept)." : "The sound was breaking up, so I refreshed the audio.");
        return true;
    }
    int paintErrors; double paintMs = 8; int lastUnder; DateTime underSince = DateTime.Now;
    internal static bool NoWindowPaint;   // (a recording of the program paints the picture itself: the window does not paint as well)
    protected override void OnPaint(PaintEventArgs e)
    {
        if (NoWindowPaint) return;
        try { FixTrackOrder(); } catch { }
        // Background jobs (Auto Mix, separation, presets) change the track and effect lists while we draw; a stale index must never reach the user as a crash.
        var pw = System.Diagnostics.Stopwatch.StartNew();
        try { PaintAll(e); }
        catch (ArgumentException) { paintErrors++; }
        catch (InvalidOperationException) { paintErrors++; }
        catch (NullReferenceException) { paintErrors++; }
        paintMs = paintMs * 0.8 + pw.Elapsed.TotalMilliseconds * 0.2;
    }

    void PaintAll(PaintEventArgs e)
    {
        Prof.Start(); EditionWatch(); hintNow = ""; bubble = ""; btnAnim = false; NavTrack(); MasterTick(); HoverTick(); if (Themes.Tick()) { lastInput = DateTime.Now; btnAnim = true; } AutoBpmTick(); TidyEmptyTracks();
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; Sharp(g);
        if (popDragId.Length > 0 && PaintPopDragFast(g)) { fpsc.Frame(); return; }   // dragging a box: the page under it is a stored picture and only the box is moved
        if (popDragId.Length == 0 && popBase != null) PopCacheClear();
        regs.Clear(); regLayer = 0; regG = g; btnRects.Clear(); btnMap.Clear(); PopBegin(g); Prof.Mark("setup");
        PaintBase(g); PaintHubMini(g);
        if (TxtAudit != null && (ntOpen || busOpen || stylesOpen || allFxOpen || themeOpen || caOpen || psOpen || amOpen || autoOpen || gainOpen || pickOpen || cpOpen || relOpen || tpOpen || WelcomeShowing)) TxtAudit.Clear();   // an open sheet covers the screen: only its own text counts
        PaintSheets(g); PopEnd(); Prof.Mark("sheets"); Win.PaintNeonChildren(this, g); Prof.Mark("neon children");
        if (TxtAudit != null && (menuOpen || eng.CountBeat > 0)) TxtAudit.Clear();
        PaintMenu(g); Prof.Mark("menu"); HubTick(); CreativeTick();
        PaintChatWindow(g); PaintVideoOverlay(g); PaintWelcome(g); PaintReleaseBuilder(g); PaintTrackPicker(g); PaintHoverSparks(g); Prof.Mark("sparks"); PaintRevertBar(g); PaintBubble(g); PaintKeyBox(g); PaintCountIn(g); Prof.Mark("overlays");
        fpsc.Frame();
        float hintStackY = ClientSize.Height - (tab == 0 ? 56 : (previewPath.Length > 0 ? 56 : 10));
        if (hintNow != hintLast) { hintLast = hintNow; hintSince = DateTime.Now; }
        bool inStrip = tab == 0 && !OverlayOpen; string stripWas = stripHint; stripHint = inStrip ? hintNow : ""; if (stripHint != stripWas) lastInput = DateTime.Now;   // the strip repaints when the hint changes
        if (hintNow.Length > 0 && !busy && (DateTime.Now - hintSince).TotalMilliseconds < 380) { lastInput = DateTime.Now; }   // a hint waits a moment before it shows, so it does not flash while you move about
        if (hintNow.Length > 0 && !busy && !inStrip && (DateTime.Now - hintSince).TotalMilliseconds >= 380)
        {   // the hint is a small see-through label at the bottom edge, as wide as its sentence needs (two lines when long), out of the way of the controls
            float maxw = Math.Min(ClientSize.Width - 24, 760); var hf = GfxCache.Font(12, false); var hsz = g.MeasureString(hintNow, hf, (int)maxw - 28); float hw = Math.Max(150, Math.Min(maxw, hsz.Width + 30)), inner = hw - 24; float hh = Math.Max(24, hsz.Height + 8), bottom = ClientSize.Height - (tab == 0 ? 46 : 8);
            var hr = Rc(ClientSize.Width / 2 - hw / 2, bottom - hh, hw, hh); FillRR(g, TM.C(168, 14, 8, 30), hr, 8); StrokeRR(g, TM.C(110, 140, 85, 210), hr, 8, 1);
            using (var sf2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (var br2 = new SolidBrush(Color.FromArgb(235, Ink))) g.DrawString(hintNow, hf, br2, new RectangleF(hr.X + 12, hr.Y, inner, hr.Height), sf2);
            hintStackY = hr.Y - 8;
        }
        if (prBoxDrag) PaintPatternBox(g, RectangleF.Empty);
        if (prDraftDrag) PaintDraftBox(g, RectangleF.Empty);   // a generated beat being dragged onto the song
        if (busy) PaintBusy(g);
        toastRect = RectangleF.Empty;
        if ((DateTime.Now - toastAt).TotalSeconds < ToastSecs && toast.Length > 0)
        {   // the message box grows to hold the whole message (it used to cut long ones off with ...), and sits above the hint box
            float tw = Math.Min(ClientSize.Width - 24, 780), inner = tw - 36; var tf = GfxCache.Font(15, false); var tsz = g.MeasureString(toast, tf, (int)inner); float th = Math.Max(38, tsz.Height + 16);
            var tr = Rc(ClientSize.Width / 2 - tw / 2, hintStackY - th, tw, th); if (!revertRect.IsEmpty && tr.IntersectsWith(revertRect)) tr = Rc(tr.X, revertRect.Y - th - 6, tw, th);   // never on top of the REVERT bar
            toastRect = tr; FillRR(g, TM.C(244, 40, 20, 80), tr, 10); StrokeRR(g, Accent, tr, 10, 1.2f);
            using (var sf3 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) using (var br3 = new SolidBrush(Ink)) g.DrawString(toast, tf, br3, new RectangleF(tr.X + 18, tr.Y, inner, tr.Height), sf3);
        }
    }

    // everything under the pop-up boxes: the backdrop, the page, the bars and the browsers
    void PaintBase(Graphics g)
    {
        if (lookApplied && lookEdition != Edition.Version) { lookEdition = Edition.Version; ApplyProfileLook(); }   // a new edition: the profile theme is Fantasy only
        if (!lookApplied) { lookApplied = true; lookEdition = Edition.Version; ApplyProfileLook(); if (!StudioEnv.UnderTest) { var lt = new System.Windows.Forms.Timer { Interval = 1500 }; lt.Tick += (s0, e0) => { lt.Stop(); lt.Dispose(); if (!soundsLoaded) { soundsLoaded = true; LibLoader.Done = () => { try { BeginInvoke(new Action(() => { sndDirty = true; Invalidate(); })); } catch { } }; LibLoader.Start(); } }; lt.Start(); } }   // your profile colour and its finish become the accent
        FreePageGuard(); DrawBackdrop(g); Prof.Mark("backdrop");
        SyncConverter(); if (tab == 11) PaintConverterPage(g); else if (tab == 0) PaintMix(g); else if (tab == 2) PaintLyrics(g); else if (tab == 3) PaintProjects(g); else if (tab == 4) PaintCommunity(g); else if (tab == 5) PaintVisualiser(g); else if (tab == 6) PaintProfile(g); else if (tab == 7) PaintArtists(g); else if (tab == 8) PaintFonts(g); else if (tab == 9) PaintHub(g); else if (tab == 10) PaintCreative(g); else PaintMastering(g);
        Prof.Mark("page " + tab);
        if (tab != 2) SyncLyricUi(false);
        if (tab == 3 || tab == 4 || tab == 6 || tab == 7 || tab == 8 || tab == 9 || tab == 10 || tab == 11) { PaintSlimBar(g); if (tab != 9 && tab != 10 && tab != 11) PaintPreviewBar(g); } else if (tab == 5) PaintVisBar(g); else if (tab == 1) PaintMasterBars(g); else { PaintToolbar(g); Prof.Mark("toolbar"); PaintTransport(g); }   // the Community and My Projects pages have no music controls
        Prof.Mark("bars");
        PaintFxWindows(g);   // the floating plugin windows: over the Mix page, under the pop-ups
        if (browserOpen && !allFxOpen) { if (TxtAudit != null) TxtAudit.Clear(); PaintBrowser(g); }   // (over ALL PLUGINS the list of effects is drawn on top of the sheet, in PaintSheets)
        PaintSoundDrag(g); PaintPackManager(g);
        if (soundBox != null && soundBox.Visible && !(soundsOpen && tab == 0)) soundBox.Visible = false;
        if (packBox != null && packBox.Visible && !packsOpen) packBox.Visible = false;
    }
    void PaintSheets(Graphics g) { PaintFontsWindow(g); PaintCutMark(g); PaintNewTrackSheet(g); PaintBusSheet(g); PaintStylesSheet(g); PaintAiSheet(g); PaintThemeSheet(g); PaintCoverSheet(g); PaintPresetSheet(g); PaintAutoMixSheet(g); PaintAutoSheet(g); PaintGainPanel(g); PaintPickSheet(g); PaintCommPresets(g); PaintAllFxSheet(g); if (browserOpen && allFxOpen) PaintBrowser(g); PaintEpSheet(g); PaintProjEpSheet(g); }

    // ---- dragging a pop-up box: the page under it and the box itself are each drawn once into a picture, then the box is just slid across (redrawing the whole studio for every mouse move was the lag) ----
    Bitmap popBase, popLayer; string popCapId = ""; PointF popLayerOff; List<HitReg> popKeepRegs;
    void PopCacheClear() { if (popBase != null) { popBase.Dispose(); popBase = null; } if (popLayer != null) { popLayer.Dispose(); popLayer = null; } popCapId = ""; popKeepRegs = null; }
    bool PaintPopDragFast(Graphics g)
    {
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height); PointF now; if (!popOff.TryGetValue(popDragId, out now)) now = PointF.Empty;
        if (popBase == null || popBase.Width != w || popBase.Height != h || popCapId != popDragId)
        {
            PopCacheClear(); popBase = Sprites.Make(w, h); popLayer = Sprites.Make(w, h); var keepHint = hintNow; var savedRegs = new List<HitReg>(regs);
            using (var bg = Graphics.FromImage(popBase)) { bg.SmoothingMode = SmoothingMode.AntiAlias; bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; Sharp(bg); regs.Clear(); btnRects.Clear(); btnMap.Clear(); PopBegin(bg); PaintBase(bg); popG = null; popPending.Clear(); }
            using (var lg = Graphics.FromImage(popLayer)) { lg.SmoothingMode = SmoothingMode.AntiAlias; lg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; Sharp(lg); lg.Clear(Color.Transparent); regs.Clear(); PopBegin(lg); PaintSheets(lg); PopEnd(); }
            popKeepRegs = new List<HitReg>(regs); regs.Clear(); regs.AddRange(savedRegs); popCapId = popDragId; popLayerOff = popOff.TryGetValue(popDragId, out now) ? now : PointF.Empty; hintNow = keepHint;
        }
        PointF cur; if (!popOff.TryGetValue(popDragId, out cur)) cur = PointF.Empty;
        g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(popBase, 0, 0); g.CompositingMode = CompositingMode.SourceOver; g.DrawImageUnscaled(popLayer, (int)Math.Round(cur.X - popLayerOff.X), (int)Math.Round(cur.Y - popLayerOff.Y));
        lastInput = DateTime.Now; return true;
    }

    string hintLast = ""; DateTime hintSince = DateTime.Now;
    Bitmap backdrop; int backVer = -1;
    string backTheme = "", backName = ""; int backAt, backEpoch;   // the theme the background picture was made in (a theme change makes it again)
    void DrawBackdrop(Graphics g)
    {
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        if (moving && backdrop != null) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(backdrop, 0, 0, w, h); g.CompositingMode = CompositingMode.SourceOver; return; }   // while a border is dragged, stretch the old picture; one real rebuild follows when the drag ends
        string themeNow = Themes.Current.Name + "|" + TM.Epoch; bool themeMoved = backName != Themes.Current.Name || (backEpoch != TM.Epoch && !Themes.Current.Rainbow && unchecked(Environment.TickCount - backAt) > 300);   /* (also when you change the colour mix; only the Rainbow theme's constant drift is ignored) */   /* (made again when you pick another theme; the Rainbow theme's constant small colour change does not rebuild it: that cost 150 ms each time) */
        if (backdrop == null || backdrop.Width != w || backdrop.Height != h || backVer != UserBackground.Version || themeMoved)
        {
            if (backdrop != null) backdrop.Dispose(); backdrop = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(backdrop))
            {
                bg.SmoothingMode = SmoothingMode.AntiAlias; var full = new RectangleF(0, 0, w, h);
                backVer = UserBackground.Version; backTheme = themeNow; backName = Themes.Current.Name; backEpoch = TM.Epoch; backAt = Environment.TickCount; var still = Prefs.BgStudio ? UserBackground.Fresh(w, h) : null; bool fromTheme = false;
                { var tp = Themes.PhotoFit(w, h); if (tp != null) { if (still != null) still.Dispose(); still = tp; fromTheme = true; } }   // the theme's own photo covers the whole window
                if (still != null) { bg.DrawImageUnscaled(still, 0, 0); still.Dispose(); using (var dm = new SolidBrush(TM.C(Math.Min(255, Math.Max(fromTheme ? 40 : 150, Prefs.BgDim * 255 / 100 + (fromTheme ? 0 : 70))), 6, 2, 16))) bg.FillRectangle(dm, full); }
                else
                {
                    using (var br = new LinearGradientBrush(full, Bg0, Bg1, 80f)) bg.FillRectangle(br, full);
                    // the studio's sky: a galaxy in the theme's own colours (nebula light, a dusty band of stars, spiral galaxies, stars), kept soft so everything on it reads
                    using (var gal = MakeGalaxy(w, h, 0x50E, 0.55f, new[] { NeonViolet, Accent, NeonCyan, ControlPaint.Light(NeonViolet, 0.3f), Accent })) bg.DrawImageUnscaled(gal, 0, 0);
                }
            }
            Win.ForceOpaque(backdrop);   // no pixel may be see-through
        }
        g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(backdrop, 0, 0); g.CompositingMode = CompositingMode.SourceOver;
    }

    // guided tours for the studio (rectangles match the toolbar / panel layout)
    internal void RunTour(string id)
    {
        var s = new List<TourStep>(); float W = ClientSize.Width;
        Func<string, Func<Rectangle>> T = l => (() => TargetOf(l));
        if (id == "studio-start") { s.Add(new TourStep(T("ADD MUSIC"), "1. Add music", "Press ADD MUSIC and choose a song, vocal or beat. You can also drop files on this window.")); s.Add(new TourStep(T("SPLIT VOCALS + BEAT"), "2. Split a full song", "Select the song track, then press this. The AI makes real VOCAL and INSTRUMENTAL tracks, perfectly in sync.")); s.Add(new TourStep(T("PLAY"), "3. Play", "Press PLAY (or Space). Use M and S on each track to mute or solo.")); }
        else if (id == "studio-automix") { s.Add(new TourStep(T("AUTO MIX"), "Press AUTO MIX", "It asks first, then balances levels, builds a chain per track and lists every change. You can cancel while it works.")); s.Add(new TourStep(T("Undo mix"), "Changed your mind?", "UNDO MIX puts everything back as it was.")); }
        else if (id == "studio-export") { s.Add(new TourStep(T("EXPORT"), "Export", "Choose the finished mix, vocals only, instrumental, dry versions or stems, in WAV, FLAC or MP3.")); s.Add(new TourStep(T("Save"), "Save your project", "Save keeps tracks, plugin settings and alignment so you can reopen it later.")); }
        if (s.Count > 0) Tour.Run(this, s);
    }

    internal void ShowMastering() { tab = 1; lastInput = DateTime.Now; if (eng.Tracks.Count == 0) Toast("Add your song with ADD MUSIC, set the mastering chain here, then EXPORT."); }

    internal void Toast(string s) { toast = s; toastAt = DateTime.Now; }

    // ---------------- toolbar ----------------
    void PaintToolbar(Graphics g)
    {
        float W = ClientSize.Width; long z0 = Prof.T();
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH); Prof.A("tb.fill", z0); z0 = Prof.T();
        NeonRule(g, 0, ToolH, W); Prof.A("tb.rule", z0);
        float y = 12, h = 46;
        // right group first (fixed sizes from the right edge), then the left group is squeezed to fit what is left
        float rx = W - 14; string[] rn = { "EXPORT", "Help", "Full", "Open", "Save", "Save as" }; float[] rw = { 106, 58, 108, 72, 72, 84 };
        float rightStart = W - 14; for (int i = 0; i < rw.Length; i++) rightStart -= rw[i] + 6; rightStart += 6;
        float avail = rightStart - 70 - 10, leftTotal = 156 + 232 + 126 + 140 + 84 + 36 + 124; float f = Math.Max(0.5f, Math.Min(1f, avail / leftTotal));
        float groupW = (164 + 240 + 132 + 148 + 90 + 118) * f; float x = 70 + Math.Max(0, (avail - groupW) / 2); float fp = Math.Max(11, 15 * f);   // ADD MUSIC to THIN TRACKS sit in the middle of the room between the side menu and the right-hand buttons (the side menu stays at the left)
        MenuBtn(g, Rc(12, y, 48, h));
        { var amR = Rc(x, y, 156 * f, h); Btn(g, amR, "ADD MUSIC", 2, () => AddMusicMenu(amR), Math.Max(13, 16 * f)); } x += 164 * f;
        Btn(g, Rc(x, y, 232 * f, h), "SPLIT VOCALS + BEAT", 1, () => SeparateClick(), Math.Max(12, 14 * f)); x += 240 * f;
        Btn(g, Rc(x, y, 126 * f, h), "AUTO MIX", 2, () => AutoMixClick(), Math.Max(13, 16 * f)); x += 132 * f;
        Btn(g, Rc(x, y + 4, 140 * f, h - 8), "Detect Track key", 0, () => DetectTrackKey(), Math.Max(11, 12 * f)); x += 148 * f;
        Btn(g, Rc(x, y + 4, 84 * f, h - 8), "Undo mix", 0, () => { if (AutoMix.LastSnapshot != null) { AutoMix.Undo(eng); Toast("Auto Mix undone"); } }, Math.Max(12, 13 * f)); x += 90 * f;
        Btn(g, Rc(x, y + 4, 118 * f, h - 8), "THIN TRACKS", LaneH < 96 ? 4 : 0, () => ToggleThinTracks(), Math.Max(11, 12 * f)); x += 118 * f;
        float leftEnd = x;
        Btn(g, Rc(rx - 106, y, 106, h), "EXPORT", 1, () => ExportClick(), 16); rx -= 112;
        Btn(g, Rc(rx - 58, y + 4, 58, h - 8), "Help", 2, () => app.ShowHelp(null), 14); rx -= 64;
        Btn(g, Rc(rx - 108, y + 4, 108, h - 8), Win.FullLabel(this), 0, () => Win.ToggleFullscreen(this), 12); rx -= 114;
        Btn(g, Rc(rx - 72, y + 4, 72, h - 8), "Open", 0, () => OpenClick(), 14); rx -= 78;
        Btn(g, Rc(rx - 72, y + 4, 72, h - 8), "Save", 0, () => SaveClick(false), 14); rx -= 78;
        Btn(g, Rc(rx - 84, y + 4, 84, h - 8), "Save as", 0, () => SaveClick(true), 14); rx -= 90;
        // project name only when there is clear room (it is always in the window title too)
        float room = rx - leftEnd - 12;
        if (room > 90) { var pn = Rc(leftEnd + 12, y, room, h); Txt(g, projName, 13, Hot(pn) ? Ink : DimC, pn, StringAlignment.Far, false); Reg(pn, () => { }).OnDouble = RenameProject; regs[regs.Count - 1].OnRight = RenameProject; }
    }

    void AutoMixMenu()
    {
        var m = Win.NewMenu();
        foreach (var s in AutoMix.Styles) { string k = s; var mi = new ToolStripMenuItem(s) { Checked = amStyle == s }; mi.Click += (a, b) => amStyle = k; m.Items.Add(mi); }
        m.Items.Add(new ToolStripSeparator());
        var enh = new ToolStripMenuItem("Vocal enhancement (key detect + tuning)") { Checked = amEnhance }; enh.Click += (a, b) => amEnhance = !amEnhance; m.Items.Add(enh);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- transport ----------------
    void PaintTransport(Graphics g)
    {
        float y0 = ToolH, W = ClientSize.Width;
        using (var br = new SolidBrush(TM.C(255, 22, 12, 40))) g.FillRectangle(br, 0, y0, W, TransH);
        NeonRule(g, 0, y0 + TransH, W);
        if (tab == 0)
        {   // the hint strip, just above the ruler: what the thing under the mouse does (a quiet tip when there is nothing to say)
            var hs = Rc(10, y0 + 86, W - 20, HintStripH - 4); FillRR(g, TM.C(150, 10, 5, 24), hs, 7); StrokeRR(g, Color.FromArgb(stripHint.Length > 0 ? 120 : 55, NeonViolet), hs, 7, 1);
            bool has = stripHint.Length > 0; Txt(g, has ? "i" : "", 11, NeonCyan, Rc(hs.X + 6, hs.Y, 16, hs.Height), StringAlignment.Center, true);
            using (var sfH = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.NoWrap }) using (var brH = new SolidBrush(has ? Color.FromArgb(235, Ink) : DimC))
                g.DrawString(has ? stripHint : "Point at anything to see what it does here.", GfxCache.Font(11.5f, false), brH, new RectangleF(hs.X + 26, hs.Y, hs.Width - 34, hs.Height), sfH);
        }
        float x = 14, y = y0 + 8, h = 36;
        // the row is laid out around PLAY: the recording tools on the left, the transport in the middle with PLAY at its centre (|< and STOP before it,
        // PAUSE and LOOP after it, then the time), the master meter and the page tabs on the right. On a narrow window the middle slides so nothing overlaps.

        float recW = 44 + 6 + 44 + 6 + 92 + 6 + (mic != null ? 128 : 60) + 6 + 28 + (W >= 1320 ? 69 : 0), leftEnd = x + recW + 12, tabsX = W - TabsW;   // MIDI REC and MIC (with its mode) go just left of the time
        const float wHome = 50, wStop = 70, wPlay = 96, wPause = 76, wLoop = 68, gap = 6, leftHalf = wHome + gap + wStop + gap, rightHalf = gap + wPause + gap + wLoop;
        float timeW = 210, cx = W / 2;   // PLAY in the middle of the window when it fits; the time sits just before |<
        cx = Math.Min(cx, tabsX - 12 - (wPlay / 2 + rightHalf)); cx = Math.Max(cx, leftEnd + 14 + 130 + 12 + leftHalf + wPlay / 2);
        timeW = Math.Max(130, Math.Min(210, cx - wPlay / 2 - leftHalf - 12 - leftEnd - 14));
        float px = cx - wPlay / 2, tx0 = px - leftHalf;
        x = tx0 - 12 - timeW - 12 - recW;
        { var pk = Rc(x, y, 44, h); bool prOn = InsShown && insTab == 4; Btn(g, pk, "", prOn ? 4 : 0, () => { if (prOn) CloseInspector(); else OpenPianoRoll(null); }, 10); PianoGlyph(g, pk); btnMap["PIANO ROLL"] = pk; if (Hot(pk)) hintNow = "Piano roll: click a track, choose its sound and write notes. Press again to close it."; Hint2(pk, "Piano roll: click a track, choose its sound, write notes (click again to close)"); x += 50; }
        { var mk = Rc(x, y, 44, h); bool mxOn = InsShown && insTab == 1; Btn(g, mk, "", mxOn ? 4 : 0, () => { if (mxOn) CloseInspector(); else OpenInspector(1); }, 10); MixerGlyph(g, mk); btnMap["MIXER TOP"] = mk; if (Hot(mk)) hintNow = "Mixer: every track and bus as a strip with its plugins, sends, pan and fader. Press again to close it."; Hint2(mk, "Mixer: every track and bus as a strip with its plugins, sends, pan and fader (click again to close)"); x += 50; }
        Btn(g, Rc(x, y, 92, h), "MIDI REC", recOn ? 3 : 0, () => { if (!recOn && !AskYes("Would you like to record an instrument?\n\nYour MIDI keyboard (or the piano window) is recorded onto the selected MIDI track while the song plays. Press PLAY after you say Yes. If not, press No.", "MIDI recording")) return; recOn = !recOn; Toast(recOn ? "MIDI recording is on (the button is red): press PLAY, then play your MIDI keyboard or the piano window and the notes are recorded onto the selected MIDI track." : "MIDI recording is off."); }, 13); x += 98;
        { bool rec = mic != null; double el = rec ? (eng.CountBeat > 0 ? 0 : Math.Max(0, (DateTime.Now - micT0).TotalSeconds - micLead / (double)StudioEngine.SR)) : 0; bool armed = micArmed && !rec; var mr = Rc(x, y, rec ? 128 : 60, h); Btn(g, mr, (rec ? "* MIC  " + ((int)(el / 60)).ToString("00") + ":" + ((int)(el % 60)).ToString("00") : "MIC"), rec || armed ? 3 : 0, () => MicClick(), 13); if (armed) { double pl = 0.5 + 0.5 * Math.Sin(Environment.TickCount / 220.0); Glow(g, Color.FromArgb(255, 60, 90), mr, 8, 0.7f + 0.5f * (float)pl, 4); lastInput = DateTime.Now; } if (rec) HBar(g, Rc(mr.X + 6, mr.Bottom - 7, mr.Width - 12, 5), mic.Level); PaintMicDrop(g, Rc(mr.Right + 3, y, 24, h), rec); bool modeRoom = W >= 1320; if (modeRoom) PaintMicModeBox(g, Rc(mr.Right + 3 + 24 + 3, y + 5, 66, h - 10), rec); x += mr.Width + 6 + 28 + (modeRoom ? 69 : 0); }   /* the box that names the microphone mode is there when there is room for it */
        x = 0;
        { var hb = Rc(tx0, y, wHome, h); TransportBtn(g, hb, "|<", 0, false, () => { eng.Seek(0); scroll = 0; followPaused = false; }); Hint2(hb, "Jump back to the start"); }
        { var sb = Rc(tx0 + wHome + gap, y, wStop, h); TransportBtn(g, sb, "STOP", 1, false, () => { if (mpActive) MasterPreviewStop(); PauseHere(); }); Hint2(sb, "Stop where you are: the needle and the view stay put (|< takes you back to the start)"); }
        { var plb = Rc(px, y, wPlay, h); TransportBtn(g, plb, "PLAY", 2, eng.Playing, () => { if (!eng.Playing) PlayToggle(); }); Hint2(plb, "Play from where the needle is (Space does the same)"); }   // PLAY only plays (it is lit while playing); PAUSE is its own button
        float rx0 = px + wPlay + gap;
        { bool paused = !eng.Playing && eng.Pos > 0; var pb = Rc(rx0, y, wPause, h); TransportBtn(g, pb, "PAUSE", 3, paused, () => PauseHere()); Hint2(pb, "Pause where you are (Space does the same); PLAY carries on from there"); }
        { var lb = Rc(rx0 + wPause + gap, y, wLoop, h); TransportBtn(g, lb, "LOOP", 4, eng.Loop || loopPick, () => LoopMenu(lb)); Hint2(lb, eng.LoopSection ? "Looping " + LoopTime(eng.LoopA) + " to " + LoopTime(eng.LoopB) + ": press to change it or turn it off" : (eng.Loop ? "The whole song loops: press to loop a section or turn it off" : "Loop the whole song, or a section you drag on the timeline")); }
        double sec = dispPos / StudioEngine.SR, tot = eng.EndFrame / (double)StudioEngine.SR;
        var tr = Rc(tx0 - 12 - timeW, y, timeW, h); FillRR(g, TM.C(200, 8, 4, 18), tr, 6); StrokeRR(g, Edge, tr, 6, 1); Glow(g, Accent, tr, 6, eng.Playing ? 0.85f : 0.5f, 3); using (var pen = new Pen(Color.FromArgb(eng.Playing ? 230 : 150, Accent), 1.2f)) using (var pp = RR(tr, 6)) g.DrawPath(pen, pp);   // a small neon glow that is always there (brighter while it plays)
        Txt(g, Fmt(sec) + "  /  " + Fmt(tot), timeW >= 190 ? 19 : 15, Accent, tr, StringAlignment.Center, true); x = rx0 + wPause + gap + wLoop + 12;   // the master meter goes after LOOP

        // tabs on the right; master meters in whatever room is left between
        float tx = W - TabsW;
        float mx = Math.Max(x + 16, W - PanelW - 300); bool showBack = false; float room = tx - 12 - mx - (showBack ? 106 : 0);   /* no HOME here: the MIX tab is beside it */ float bx0 = tx - 6;
        if (showBack) { bx0 -= 100; Btn(g, Rc(bx0, y, 100, h), "BACK", 0, () => GoBack(), 14); bx0 -= 6; }
        if (room >= 70)
        {
            Txt(g, "MASTER", 11, DimC, Rc(mx, y - 4, 60, 14), StringAlignment.Near, false);
            float bw = Math.Min(120, room); HBar(g, Rc(mx, y + 12, bw, 8), eng.MasterPeakL); HBar(g, Rc(mx, y + 22, bw, 8), eng.MasterPeakR);
            if (room >= 230) { float lm = eng.Lufs.Momentary; Txt(g, "M " + Db(lm) + " LUFS", 13, Ink, Rc(mx + 128, y, Math.Min(120, room - 128), h), StringAlignment.Near, false); }
        }
        Btn(g, Rc(tx, y, 106, h), "MIX", tab == 0 ? 4 : 0, () => tab = 0, 14);
        Btn(g, Rc(tx + 112, y, 106, h), "MASTERING", tab == 1 ? 4 : 0, () => tab = 1, 13);
        Btn(g, Rc(tx + 224, y, 90, h), "LYRICS", tab == 2 ? 4 : 0, () => tab = 2, 13);
        { var cvb = Rc(tx + 320, y, 112, h); Btn(g, cvb, "CONVERTER", tab == 11 ? 4 : 0, () => GoTab(11), 12); Hint2(cvb, "The MP3 / MP4 converter, here in the studio"); }
        if (tab == 0) PaintDawBar(g, y0 + 52, W);   // the editing tools only belong to the track view
    }

    static string Fmt(double s) { int m = (int)(s / 60); double r = s - m * 60; return m.ToString("00") + ":" + r.ToString("00.00", CultureInfo.InvariantCulture); }

    void HBar(Graphics g, RectangleF r, float lin)
    {
        FillRR(g, TM.C(180, 8, 4, 18), r, r.Height / 2);
        float db = lin > 0 ? (float)(20 * Math.Log10(lin)) : -90; float f = Clamp01((db + 60) / 60f); if (f < 0.01f) return;
        Color c = db > -1 ? Bad : (db > -6 ? Warn : Good);
        using (var br = new LinearGradientBrush(r, Violet, c, 0f)) using (var p = RR(Rc(r.X, r.Y, r.Width * f, r.Height), r.Height / 2)) g.FillPath(br, p);
    }

    internal bool keysInSounds;
    void PlayGuard()
    {
        Auditioner.Stop();   // the song starting stops a sound you were hearing in the sound packs
        try { eng.Play(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Playback"); }
    }

    // dragging the needle: it never goes behind the track boxes or off the right side. At an edge the timeline scrolls along (faster the further past the edge you hold it), so you always see the audio under the needle
    void NeedleDragTo(float x, RectangleF tl) { float cx = Math.Max(HeadW, Math.Min(tl.Right - 1, x)); eng.Seek(Math.Max(0, scroll + (long)((cx - HeadW) * spp))); }
    void NeedleEdgeScroll(RectangleF tl)
    {
        float edgeL = HeadW + 28, edgeR = tl.Right - 28, x = MousePos.X; float over = x < edgeL ? x - edgeL : (x > edgeR ? x - edgeR : 0); if (over == 0) return;
        if (over < 0 && scroll <= 0) { NeedleDragTo(x, tl); return; }
        long maxScroll = Math.Max(0, eng.EndFrame + StudioEngine.SR * 30 - (long)(tl.Width * spp)); long step = (long)(Math.Sign(over) * Math.Min(240, Math.Abs(over) + 8) * spp * 0.25);
        scroll = Math.Max(0, Math.Min(Math.Max(scroll, maxScroll), scroll + step)); autoFit = false; NeedleDragTo(x, tl); lastInput = DateTime.Now;
    }
    // ---------------- MIX view ----------------
    void PaintMix(Graphics g)
    {
        float top = ArrTop; var tl = TimelineRect();
        PruneTrackCaches(); StepAnim(GhostDt()); AutoKeyTick();
        int n = LaneCount; float viewH = MixH - top - RulerH - ArrGap, contentH = n * LaneH + 50; vscroll = Math.Max(0, Math.Min(vscroll, Math.Max(0, contentH - viewH)));   // never stuck scrolled past the tracks   // buses are not lanes: they live in the Buses sheet and in each track's Bus drop-down
        if (autoFit && !ghostOn && (lastFitW != (int)tl.Width || lastFitLen != eng.EndFrame) && tl.Width > 100) { FitView(); lastFitW = (int)tl.Width; lastFitLen = eng.EndFrame; }
        // clamp selection
        if (selTrack >= n) selTrack = Math.Max(0, n - 1);
        // ruler
        var ruler = Rc(HeadW, top, tl.Width, RulerH); using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, ruler);
        long q0 = Prof.T(); PaintRuler(g, ruler); PaintRulerNeon(g, ruler); Prof.A("mix.ruler", q0); if (Hot(ruler)) hintNow = "Drag here to move the needle. This strip at the top is the only place that moves it.";
        var rr = Reg(ruler, () => { needleDrag = true; followPaused = false; eng.Seek(scroll + (long)((MousePos.X - HeadW) * spp)); }); rr.OnDrag = pt => NeedleDragTo(pt.X, tl); rr.OnUp = () => needleDrag = false;

        if (needleDrag && dragReg != null) NeedleEdgeScroll(tl);   // the needle dragged to an edge: the timeline scrolls with it
        regs.Add(new HitReg { R = Rc(0, top + RulerH, HeadW, Math.Max(0, MixH - top - RulerH)), OnWheel = d => { vscroll = Math.Max(0, vscroll - d * LaneH * 0.5f); Invalidate(); } });   // the wheel over the track headers scrolls the tracks up and down
        float y = top + RulerH - vscroll;
        g.SetClip(Rc(0, top + RulerH, ClientSize.Width - PanelW, MixH - top - RulerH - ArrGap));
        int regs0 = regs.Count;
        for (int i = 0; i < n; i++, y += LaneH)
        {
            if (y + LaneH < top + RulerH || y > MixH) continue;
            var tr = eng.Tracks[i]; bool sel = i == selTrack; float ys = y + AnimOff(tr);   // ys: where this row is drawn right now (it slides when rows swap)
            var lane = Rc(HeadW, ys, tl.Width, LaneH); q0 = Prof.T(); PaintGrid(g, tl, ys, ys + LaneH, 1f, i % 2 == 0 ? Lane : Lane2); Prof.A("mix.grid", q0);   // the grid sits UNDER the clips, inside each row
            q0 = Prof.T(); if (tr.IsBus) PaintBusLane(g, tr, lane); else if (tr.Instrument) PaintMidi(g, tr, lane); else if (tr.Clip == null) { if (!RowHasPieces(tr)) PaintEmptyLane(g, tr, lane); } else PaintWave(g, tr, lane, sel);   /* a row only shows EMPTY TRACK when nothing at all is on it (its own clip may have moved away while pieces are still on it) */
            foreach (var lay in eng.Tracks) if (lay.Host == tr) { if (lay.Instrument) PaintMidi(g, lay, lane); else if (lay.Clip != null) PaintWave(g, lay, lane, sel); }   // tracks sharing this row
            Prof.A("mix.wave", q0);
            var lr = LaneReg(lane, tr); int idx = i; RegGainTags();   /* (the gain tags of this row sit above it) */
            /* an empty track is just an empty lane: no box to click (drag audio onto it, drop files, record with MIC, or right-click it for Add music) */   // the box of an empty track: click it to pick music for THIS track
            q0 = Prof.T(); if (HeadW <= HeadTabW + 1) PaintHeaderMini(g, tr, i, Rc(0, ys, HeadW, LaneH), sel); else { var gsH = g.Save(); g.SetClip(Rc(0, ys, HeadW, LaneH), System.Drawing.Drawing2D.CombineMode.Intersect); PaintHeader(g, tr, i, Rc(HeadW - HeadFullW, ys, HeadFullW, LaneH), sel); g.Restore(gsH); } Prof.A("mix.header", q0);   // the list slides away to the left
            q0 = Prof.T(); NeonLaneChrome(g, tr, Rc(0, ys, HeadW, LaneH), lane, sel); Prof.A("mix.neonchrome", q0);
            if (sel && n > 1) { float rEnd = tl.Right - 6; if (!tr.Instrument && !tr.IsBus) { float mx = -1e9f, v; if (tr.Clip != null && clipRightX.TryGetValue(tr, out v)) mx = v; foreach (var lay2 in eng.Tracks) if (lay2.Host == tr && lay2.Clip != null && clipRightX.TryGetValue(lay2, out v)) mx = Math.Max(mx, v); if (mx > -1e8f) rEnd = mx <= HeadW + 2 ? HeadW : Math.Min(tl.Right - 6, mx + 3); } PaintPickedRow(g, tr, Rc(1, ys + 1, rEnd - 1, LaneH - 2)); }   /* audio that ends before the part you see: the outline goes round the track box on the left only (no stub into the timeline) */   /* the outline ends where the audio of the row ends: also a row made only of pieces (SPLIT, paste), and never past the timeline into the plugin panel */   // the outline ends where the audio ends
            using (var pen = new Pen(TM.C(70, 140, 85, 210))) g.DrawLine(pen, 0, ys + LaneH, tl.Right, ys + LaneH);
            PaintGhostSlot(g, tr, Rc(0, ys, tl.Right, LaneH));
        }
        { float visBot = MixH - ArrGap; for (int k = regs.Count - 1; k >= regs0; k--) if (regs[k].R.Y >= visBot) regs.RemoveAt(k); }   // a row half under the bottom panel: its hidden buttons are not clickable things
        q0 = Prof.T(); PaintGhost(g, tl, top); Prof.A("mix.ghost", q0);
        AddTrackRow(g, Math.Max(y, top + RulerH), n);
        HeadHandle(g, top);
        { float ey = Math.Max(y, top + RulerH); if (ey < MixH) { var em = Reg(Rc(HeadW, ey, tl.Width, MixH - ey), () => { toolsTrack = null; MarqueeBegin(); }); em.OnDrag = pt => MarqueeTo(pt); em.OnUp = () => MarqueeEnd(); } }   // empty space: hold and drag a selection box (the needle moves from the ruler only)
        PaintMarquee(g);
        { var rr2 = Reg(ruler, () => { needleDrag = true; followPaused = false; eng.Seek(scroll + (long)((MousePos.X - HeadW) * spp)); }); rr2.OnDrag = pt => NeedleDragTo(pt.X, tl); rr2.OnUp = () => needleDrag = false; }   // the whole ruler, to its bottom border, moves the needle: registered after the rows so a row scrolled up under it can not take the click
        PaintLoop(g, tl, top);   // the loop section (and, while you pick one, the drag that sets it)
        if (n == 0)
        {
            var r0 = Rc(HeadW + 40, top + RulerH + 60, tl.Width - 80, 150);
            bool over = Hot(r0), drop = dragOver; FillRR(g, TM.C(drop ? 170 : (over ? 140 : 110), 40, 20, 80), r0, 14); Glow(g, drop ? NeonCyan : NeonViolet, r0, 14, drop ? 1.3f : (over ? 0.9f : 0.45f), 4);
            using (var pen = new Pen(Color.FromArgb(drop ? 255 : 150, drop ? NeonCyan : NeonViolet), 2f) { DashStyle = DashStyle.Dash }) { var inner = RectangleF.Inflate(r0, -10, -10); using (var p = RR(inner, 10)) g.DrawPath(pen, p); }
            // the words stay inside the dotted line: each line shrinks to fit the box and wraps if it still cannot
            float tw0 = r0.Width - 64; Action<string, float, Color, float, bool> fitLine = (t, sz0, col, yy, bold) =>
            {
                float sz = sz0; while (sz > 11f && g.MeasureString(t, GfxCache.Font(sz, bold)).Width > tw0) sz -= 0.5f;
                if (g.MeasureString(t, GfxCache.Font(sz, bold)).Width > tw0) TxtWrap(g, t, sz, col, Rc(r0.X + 32, yy, tw0, 38)); else Txt(g, t, sz, col, Rc(r0.X + 32, yy, tw0, 26), StringAlignment.Center, bold);
            };
            fitLine(drop ? "Drop your music here" : "Add your music to start", 26, Ink, r0.Y + 18, true);
            fitLine("Click this box to pick songs from your computer, or drag and drop them onto it.", 15, drop ? Ink : DimC, r0.Y + 64, false);
            fitLine("MP3, WAV, FLAC, M4A, OGG and more. For a full song, split it afterwards.", 13, DimC, r0.Y + 104, false);
            Reg(r0, () => AddMusicClick()); if (over) { bubble = "Click to choose music from your computer"; bubblePt = MousePos; }
        }
        g.ResetClip();
        { q0 = Prof.T(); float gy = Math.Max(y, top + RulerH); PaintGrid(g, tl, gy, MixH - ArrGap, 0.5f); Prof.A("mix.grid2", q0); }   // the empty space under the last row
        // playhead
        float px = HeadW + (float)((dispPos - scroll) / spp);
        if (px >= HeadW && px <= tl.Right) { q0 = Prof.T(); float nb = MixH - ArrGap; if (n > 0) nb = Math.Min(nb, Math.Max(top + RulerH + 8, y)); PaintNeedle(g, px, top, nb, RulerH);   /* the needle runs down to the bottom of the last track, never into the empty space or the bar under it */ Prof.A("mix.needle", q0); } else if (sparks.Count > 0) sparks.Clear();
        if (contentH > viewH + 1) VScroll(g, Rc(tl.Right - 50, top + RulerH + 4, 42, viewH - 8), contentH, viewH, () => vscroll, v => vscroll = v);   // the logo scroll bar: hold the logo and drag, or use the arrows
        q0 = Prof.T(); PaintOverview(g, tl); Prof.A("mix.overview", q0);
        // wheel: scroll / zoom
        var wr = new HitReg { R = Rc(HeadW, top, tl.Width, MixH - top) };
        wr.OnWheel = d =>
        {
            if ((ModifierKeys & Keys.Control) != 0 && (ModifierKeys & Keys.Alt) == 0 && (ModifierKeys & Keys.Shift) == 0) autoFit = false;
            if ((ModifierKeys & Keys.Control) != 0 && (ModifierKeys & Keys.Shift) != 0) { }   // Ctrl + Shift + wheel does nothing (it used to resize the tracks by accident)
            else if ((ModifierKeys & Keys.Alt) != 0) { LaneH = Math.Max(64, Math.Min(420, LaneH + (d > 0 ? 16 : -16))); vscroll = Math.Max(0, Math.Min(vscroll, Math.Max(0, n * LaneH - (MixH - top - RulerH - 66)))); }
            else if ((ModifierKeys & Keys.Control) != 0) { double f = d > 0 ? 0.7 : 1.43; double cur = scroll + (MousePos.X - HeadW) * spp; spp = Math.Max(MinSpp, Math.Min(MaxSpp, spp * f)); scroll = Math.Max(0, (long)(cur - (MousePos.X - HeadW) * spp)); }
            else if ((ModifierKeys & Keys.Shift) != 0) ScrollView(-d * 0.25);
            else if (n * LaneH + 50 <= MixH - top - RulerH - 46 || MousePos.Y < top + RulerH) ScrollView(-d * 0.25);   // everything fits vertically: the wheel moves along the song (like BandLab)
            else vscroll = Math.Max(0, Math.Min(Math.Max(0, n * LaneH + 50 - (MixH - top - RulerH - 46)), vscroll - d * 40));
        };
        regs.Insert(0, wr);
        q0 = Prof.T(); PaintInspector(g); Prof.A("mix.inspector", q0);   // the bottom inspector, under the arrangement
        q0 = Prof.T(); if (soundsOpen) PaintSoundsPanel(g); else PaintPluginPanel(g); Prof.A("mix.panel", q0);
        if (flexOpen) PaintFlex(g);
    }

    void PaintRuler(Graphics g, RectangleF r)
    {
        double[] steps = { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 }; double secPerPx = spp / StudioEngine.SR, want = 90 * secPerPx, major = 3600;
        foreach (var s in steps) if (s >= want) { major = s; break; }
        double t0 = scroll / (double)StudioEngine.SR, first = Math.Floor(t0 / major) * major;
        using (var pen = new Pen(TM.C(120, 190, 140, 255)))
            for (double s = first; ; s += major)
            {
                float x = r.X + (float)((s - t0) / secPerPx); if (x > r.Right) break; if (x < r.X) continue;
                g.DrawLine(pen, x, r.Bottom - 10, x, r.Bottom); Txt(g, major >= 1 ? ((int)(s / 60)) + ":" + ((int)(s % 60)).ToString("00") : Fmt(s).Substring(0, 8), 11, Ink, Rc(x + 3, r.Y + 2, 60, 16), StringAlignment.Near, false);
            }
    }

    // forget the pictures and meter holds of tracks that were deleted or replaced by opening another project (they would keep the decoded audio alive)
    int pruneTick;
    void PruneTrackCaches()
    {
        int now = Environment.TickCount; if (now - pruneTick < 2000 && pruneTick != 0) return; pruneTick = now;
        Track[] cur; lock (eng.Sync) cur = eng.Tracks.ToArray();
        List<Track> gone = null;
        foreach (var k in peakCache.Keys) if (Array.IndexOf(cur, k) < 0) { if (gone == null) gone = new List<Track>(); gone.Add(k); }
        if (gone != null) foreach (var k in gone) { var pc = peakCache[k]; if (pc.Img != null) pc.Img.Dispose(); peakCache.Remove(k); }
        gone = null;
        foreach (var k in hold.Keys) if (Array.IndexOf(cur, k) < 0) { if (gone == null) gone = new List<Track>(); gone.Add(k); }
        if (gone != null) foreach (var k in gone) hold.Remove(k);
    }
    // the clip picture (fill + heartbeat line) for one lane, drawn in song-area coordinates; PaintWave caches it in a bitmap
    // a clip's waveform: glowing bars on a little galaxy (a dark sky, soft nebula light and stars) in a framed box with cut corners,
    // in the colours of your theme (its cyan and violet, with the track colour in the middle). It is drawn once into the clip's picture.
    // the transport buttons: icons on dark glass with a neon edge, and PLAY a big glowing round button in the middle (theme colours)
    // kind: 0 to the start, 1 stop, 2 play, 3 pause, 4 loop
    void TransportBtn(Graphics g, RectangleF r, string key, int kind, bool lit, Action act)
    {
        bool hot = Hot(r); var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; Color ca = Accent, cv = NeonViolet, ink = Color.White;
        if (kind == 2)
        {
            float d = Math.Min(r.Height + 6, r.Width); var c = new RectangleF(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
            for (int k = 3; k >= 1; k--) using (var gb = new SolidBrush(Color.FromArgb((lit ? 34 : 22) + (hot ? 10 : 0), ca))) g.FillEllipse(gb, c.X - k * 2.5f, c.Y - k * 2.5f, c.Width + k * 5, c.Height + k * 5);   // the glow
            using (var br = new LinearGradientBrush(c, ControlPaint.Light(ca, 0.25f), cv, 90f)) g.FillEllipse(br, c);
            using (var pen = new Pen(Color.FromArgb(lit || hot ? 255 : 190, Color.White), lit ? 2f : 1.4f)) g.DrawEllipse(pen, c);
            float tcx = c.X + c.Width / 2 + d * 0.05f, tcy = c.Y + c.Height / 2, ts = d * 0.22f;
            using (var tb = new SolidBrush(ink)) g.FillPolygon(tb, new[] { new PointF(tcx - ts * 0.8f, tcy - ts), new PointF(tcx + ts, tcy), new PointF(tcx - ts * 0.8f, tcy + ts) });
        }
        else
        {
            FillRR(g, lit ? TM.C(230, 50, 24, 100) : (hot ? TM.C(230, 34, 18, 72) : TM.C(220, 16, 8, 34)), r, r.Height / 2);
            StrokeRR(g, Color.FromArgb(lit ? 255 : (hot ? 220 : 140), lit ? ca : cv), r, r.Height / 2, lit ? 1.8f : 1.2f); if (lit) Glow(g, ca, r, r.Height / 2, 0.8f, 3);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s0 = Math.Min(r.Height, r.Width) * 0.2f; Color gc = lit ? ink : TM.C(255, 220, 205, 255);
            using (var b = new SolidBrush(gc)) using (var pen = new Pen(gc, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (kind == 0) { g.FillRectangle(b, cx - s0 - 2, cy - s0, 2.4f, s0 * 2); g.FillPolygon(b, new[] { new PointF(cx + s0, cy - s0), new PointF(cx - s0 + 1, cy), new PointF(cx + s0, cy + s0) }); }
                else if (kind == 1) { var q = new RectangleF(cx - s0 * 0.85f, cy - s0 * 0.85f, s0 * 1.7f, s0 * 1.7f); using (var rp = RR(q, 2)) g.FillPath(b, rp); }
                else if (kind == 3) { g.FillRectangle(b, cx - s0 * 0.75f, cy - s0, s0 * 0.5f, s0 * 2); g.FillRectangle(b, cx + s0 * 0.25f, cy - s0, s0 * 0.5f, s0 * 2); }
                else if (kind == 4) { float rr = s0 * 1.05f; g.DrawArc(pen, cx - rr, cy - rr, rr * 2, rr * 2, 200, 250); g.FillPolygon(b, new[] { new PointF(cx - rr - 3.5f, cy - 2), new PointF(cx - rr + 3.5f, cy - 2), new PointF(cx - rr, cy + 3.5f) }); }
            }
        }
        g.SmoothingMode = oldSm; btnMap[key] = r; Reg(r, act);
    }
    void PaintWaveBody(Graphics g, RectangleF lane, RectangleF cr, Color c, bool audible, bool sel, float cy, float amp, int px, float[] wmn, float[] wmx)
    {
        if (cr.Width <= 0) return;
        Color ca = NeonCyan, cb = NeonViolet; int al = audible ? 255 : 110; float cut = Math.Min(10f, Math.Min(cr.Width, cr.Height) / 4f);
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var frame = new GraphicsPath())
        {
            frame.AddPolygon(new[] { new PointF(cr.X + cut, cr.Y), new PointF(cr.Right - cut, cr.Y), new PointF(cr.Right, cr.Y + cut), new PointF(cr.Right, cr.Bottom - cut), new PointF(cr.Right - cut, cr.Bottom), new PointF(cr.X + cut, cr.Bottom), new PointF(cr.X, cr.Bottom - cut), new PointF(cr.X, cr.Y + cut) });
            // the sky
            using (var sky = new LinearGradientBrush(cr, TM.C(sel ? 250 : 238, 6, 4, 18), TM.C(sel ? 250 : 238, 10, 6, 28), 90f)) g.FillPath(sky, frame);
            var gs = g.Save(); g.SetClip(frame, CombineMode.Intersect);
            if (cr.Width > 30 && cr.Height > 24)
            {
                // nebula light in two corners (and one in the middle of long clips), from the clip's own place so it stays still while you scroll
                int seed = (int)((long)(cr.X - lane.X) * 31 + (int)cr.Width * 7) ^ c.ToArgb(); var rnd = new Random(seed);
                Action<float, float, float, Color> neb = (nx, ny, nr, nc) => { using (var pth = new GraphicsPath()) { pth.AddEllipse(nx - nr, ny - nr * 0.6f, nr * 2, nr * 1.2f); using (var pb = new PathGradientBrush(pth) { CenterColor = Color.FromArgb(audible ? 70 : 30, nc), SurroundColors = new[] { Color.FromArgb(0, nc) } }) g.FillPath(pb, pth); } };
                float nr0 = Math.Min(cr.Height * 0.9f, 90f);
                neb(cr.X + nr0 * 0.5f, cr.Y + cr.Height * 0.3f, nr0, ca); neb(cr.Right - nr0 * 0.5f, cr.Bottom - cr.Height * 0.3f, nr0, cb);
                if (cr.Width > 400) neb(cr.X + cr.Width * (0.35f + (float)rnd.NextDouble() * 0.3f), cr.Y + cr.Height * 0.5f, nr0 * 0.8f, c);
                // stars
                int stars = Math.Min(120, (int)(cr.Width * cr.Height / 900)); using (var sb = new SolidBrush(Color.FromArgb(audible ? 150 : 60, 235, 230, 255))) for (int i = 0; i < stars; i++) { float sx = cr.X + (float)rnd.NextDouble() * cr.Width, sy = cr.Y + (float)rnd.NextDouble() * cr.Height, sz = rnd.Next(10) == 0 ? 1.8f : 1f; g.FillEllipse(sb, sx, sy, sz, sz); }
            }
            // the dotted centre line
            using (var pen = new Pen(Color.FromArgb(audible ? 120 : 40, ca), 1f) { DashPattern = new[] { 1f, 3f } }) g.DrawLine(pen, cr.X + 4, cy, cr.Right - 4, cy);
            // the bars: one every 3 pixels, mirrored round the centre, a soft glow under a bright core; big peaks get a white tip
            if (cr.Width > 4)
            {
                int i0 = Math.Max(0, (int)(cr.X - lane.X)), i1 = Math.Min(px, (int)Math.Ceiling(cr.Right - lane.X)); const int step = 3;
                var bounds = new RectangleF(cr.X - 1, cr.Y, Math.Max(2, cr.Width + 2), cr.Height);
                using (var core = new LinearGradientBrush(bounds, ca, cb, 0f)) using (var glow = new LinearGradientBrush(bounds, ca, cb, 0f))
                {
                    core.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(al, ca), Color.FromArgb(al, c), Color.FromArgb(al, cb) }, Positions = new[] { 0f, 0.5f, 1f } };
                    glow.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(audible ? 60 : 20, ca), Color.FromArgb(audible ? 60 : 20, c), Color.FromArgb(audible ? 60 : 20, cb) }, Positions = new[] { 0f, 0.5f, 1f } };
                    using (var pg = new Pen(glow, 3.6f)) using (var pc2 = new Pen(core, 1.4f)) using (var tip = new Pen(Color.FromArgb(audible ? 230 : 90, 255, 255, 255), 1f))
                        for (int i = i0; i < i1; i += step)
                        {
                            float v = 0; for (int j = i; j < Math.Min(i1, i + step); j++) v = Math.Max(v, Math.Max(wmx[j], -wmn[j])); if (v <= 0.002f) continue;
                            float h = (float)Math.Pow(Math.Min(1f, v), 0.8) * amp, x = lane.X + i + 1;
                            g.DrawLine(pg, x, cy - h, x, cy + h); g.DrawLine(pc2, x, cy - h, x, cy + h);
                            if (v > 0.7f) g.DrawLine(tip, x, cy - h * 0.55f, x, cy + h * 0.55f);
                        }
                }
            }
            g.Restore(gs);
            // the frame: a glowing outline with cut corners, little ticks near the corners and a star at the top and bottom middle
            using (var pg = new Pen(Color.FromArgb(audible ? (sel ? 110 : 60) : 25, ca), 4f)) g.DrawPath(pg, frame);
            using (var fb = new LinearGradientBrush(new RectangleF(cr.X - 1, cr.Y, cr.Width + 2, cr.Height), Color.FromArgb(al, ca), Color.FromArgb(al, cb), 0f)) using (var pf = new Pen(fb, sel ? 1.8f : 1.2f)) g.DrawPath(pf, frame);
            if (cr.Width > 120 && cr.Height > 30)
            {
                using (var pt = new Pen(Color.FromArgb(audible ? 200 : 70, ca), 1.4f)) { float ty = cr.Y + 4; for (int k = 0; k < 4; k++) { g.DrawLine(pt, cr.X + cut + 8 + k * 6, ty, cr.X + cut + 11 + k * 6, ty); g.DrawLine(pt, cr.Right - cut - 11 - k * 6, cr.Bottom - 4, cr.Right - cut - 8 - k * 6, cr.Bottom - 4); } }
                Action<float, float, float> star = (sx, sy, r) => { using (var sp = new GraphicsPath()) { sp.AddPolygon(new[] { new PointF(sx, sy - r), new PointF(sx + r * 0.22f, sy - r * 0.22f), new PointF(sx + r, sy), new PointF(sx + r * 0.22f, sy + r * 0.22f), new PointF(sx, sy + r), new PointF(sx - r * 0.22f, sy + r * 0.22f), new PointF(sx - r, sy), new PointF(sx - r * 0.22f, sy - r * 0.22f) }); using (var sb = new SolidBrush(Color.FromArgb(al, 235, 240, 255))) g.FillPath(sb, sp); } };
                float mx = cr.X + cr.Width / 2; star(mx, cr.Y + 1, 5f); star(mx, cr.Bottom - 1, 5f);
            }
        }
        g.SmoothingMode = oldSm;
    }
    class PeakCache { public AudioClip Clip; public long Start, Len; public double Spp; public int Px; public float[] Mn, Mx; public Bitmap Img; public int Ver, ImgVer, ImgCol; public long ImgTheme; public RectangleF ImgLane, ImgCr, ImgRel; public float ImgX0; public bool ImgAud, ImgSel; }
    readonly Dictionary<Track, PeakCache> peakCache = new Dictionary<Track, PeakCache>();
    readonly Dictionary<Track, float> clipRightX = new Dictionary<Track, float>();   // where each clip ends on screen: the outline of the picked row stops there (not at the far end of the lane)
    void PaintWave(Graphics g, Track tr, RectangleF lane, bool sel)
    {
        if (tr.Clip == null) return;
        int px = (int)lane.Width;
        PeakCache pc; if (!peakCache.TryGetValue(tr, out pc)) { pc = new PeakCache(); peakCache[tr] = pc; }
        bool dragWave = ghostOn && ghostKind == 0 && ghostTrack == tr && pc.Img != null;   // while this clip is being dragged its finished picture is kept and just slid along (redrawing it every frame was the lag)
        if (!dragWave && (pc.Clip != tr.Clip || pc.Start != scroll - tr.Offset + tr.ClipStart || pc.Spp != spp || pc.Px != px || pc.Len != tr.UsedLength)) { if (pc.Mn == null || pc.Mn.Length < px) { pc.Mn = new float[px + 64]; pc.Mx = new float[px + 64]; } tr.Clip.GetPeaks(scroll - tr.Offset + tr.ClipStart, spp, px, pc.Mn, pc.Mx); pc.Clip = tr.Clip; pc.Start = scroll - tr.Offset + tr.ClipStart; pc.Spp = spp; pc.Px = px; pc.Len = tr.UsedLength; pc.Ver++; }
        var wmn = pc.Mn; var wmx = pc.Mx;
        Color c = Neon(tr.Color); bool audible = !tr.Mute && !tr.Off; float cy = lane.Y + lane.Height / 2, amp = lane.Height * 0.42f;
        long clipStart = tr.Offset, clipEnd = tr.Offset + tr.UsedLength;
        float x0 = lane.X + (float)((clipStart - scroll) / spp), x1 = lane.X + (float)((clipEnd - scroll) / spp);
        var cr = RectangleF.Intersect(Rc(x0, lane.Y + 5, x1 - x0, lane.Height - 10), lane);
        if (cr.Width > 0)
        {
            int ix = (int)Math.Floor(lane.X), iy = (int)Math.Floor(lane.Y), iw = Math.Max(1, (int)Math.Ceiling(lane.Right) - ix), ih = Math.Max(1, (int)Math.Ceiling(lane.Bottom) - iy);
            var rel = new RectangleF(cr.X - lane.X, cr.Y - lane.Y, cr.Width, cr.Height);   // the picture only depends on where the clip is INSIDE the lane, so a lane that slides up or down (rows swapping) does not redraw it
            if (!dragWave && (pc.Img == null || pc.Img.Width != iw || pc.Img.Height != ih || pc.ImgVer != pc.Ver || pc.ImgRel != rel || pc.ImgAud != audible || pc.ImgSel != sel || pc.ImgCol != c.ToArgb() || pc.ImgTheme != ThemeKeyNow()))
            {
                if (pc.Img == null || pc.Img.Width != iw || pc.Img.Height != ih) { if (pc.Img != null) pc.Img.Dispose(); pc.Img = new Bitmap(iw, ih, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); }
                using (var ig = Graphics.FromImage(pc.Img)) { ig.Clear(Color.Transparent); ig.SmoothingMode = SmoothingMode.AntiAlias; ig.TranslateTransform(-ix, -iy); PaintWaveBody(ig, lane, cr, c, audible, sel, cy, amp, px, wmn, wmx); }
                pc.ImgVer = pc.Ver; pc.ImgRel = rel; pc.ImgX0 = x0; pc.ImgLane = lane; pc.ImgCr = cr; pc.ImgAud = audible; pc.ImgSel = sel; pc.ImgCol = c.ToArgb(); pc.ImgTheme = ThemeKeyNow();
            }
            var gi = g.Save(); g.SetClip(lane, CombineMode.Intersect); if (tr.Off || IsOffRow(tr)) PaintQuake(g, pc.Img, ix + (dragWave ? (int)Math.Round(x0 - pc.ImgX0) : 0), iy, tr, lane, cr); else g.DrawImageUnscaled(pc.Img, ix + (dragWave ? (int)Math.Round(x0 - pc.ImgX0) : 0), iy); g.Restore(gi);
        }
        if (cr.Width > 0 && !tr.Off) { var gst = g.Save(); g.SetClip(lane, CombineMode.Intersect); float lv = Math.Max(tr.PeakL, tr.PeakR); Glow(g, c, cr, 6, (sel ? 1.2f : 0.8f) + Math.Min(0.6f, lv * 0.9f), 5); g.Restore(gst); }
        { float nx = Math.Max(lane.X, x0) + 8, nw = Math.Min(300, Math.Min(x1, lane.Right) - nx - 6); bool lab = tr.SectionLabel.Length > 0; if (nw > (lab ? 30 : 70)) Txt(g, lab ? tr.SectionLabel : tr.Name, 12, lab ? Color.FromArgb(255, 255, 214, 110) : Color.FromArgb(200, 255, 255, 255), Rc(nx, lane.Y + 6, nw, 16), StringAlignment.Near, true); }   // the name (a named section shows its own name, in gold) stays inside its own clip
        clipRightX[tr] = x1; PaintClipExtras(g, tr, lane, x0, x1, cr);
        PaintClipChrome(g, tr, lane, cr, sel);
    }

    float[] Hold(Track t) { float[] h; if (!hold.TryGetValue(t, out h)) { h = new float[4]; hold[t] = h; } return h; }

    void PaintHeader(Graphics g, Track tr, int i, RectangleF r, bool sel)
    {
        bool thin = r.Height < 96, compact = r.Height < 112, bus = tr.IsBus; float by = thin ? 34 : (compact ? 36 : 44), vy = thin ? 60 : (compact ? 64 : 84), py = compact ? 86 : 110;
        using (var br = new SolidBrush(sel ? TM.C(255, 40, 22, 74) : TM.C(255, 26, 15, 46))) g.FillRectangle(br, r);
        using (var pen = new Pen(sel ? Color.FromArgb(200, Neon(tr.Color)) : Color.FromArgb(70, Neon(tr.Color)), sel ? 1.6f : 1f)) g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        { var trH = tr; Reg(r, () => { busEdit = null; selTrack = Math.Max(0, eng.Tracks.IndexOf(trH)); selFx = -1; toolsTrack = trH; }); }   // any empty part of the box picks this track (the buttons and sliders on top of it keep their own clicks)
        PaintPowerButton(g, tr, r.X + 20, r.Y + 17, Hot(Rc(r.X + 7, r.Y + 4, 26, 26)));   /* the power button of the track (press it: the track is off) */
        Txt(g, tr.Name, 15, Ink, Rc(r.X + 36, r.Y + 6, 142, 22), StringAlignment.Near, true);
        if (tr.Bpm > 0 && !bus) { var bt = thin ? Rc(r.X + 298, r.Y + 9, 40, 14) : Rc(r.X + 36, r.Y + 26, 120, 14); Txt(g, tr.Bpm.ToString("0.#") + (thin ? "" : " BPM"), 10, Math.Abs(tr.Bpm - Fx.ProjectBpm) < 0.6 ? Color.FromArgb(150, 255, 190) : DimC, bt, StringAlignment.Near, false); if (Hot(bt)) hintNow = "The tempo heard in this track" + (Math.Abs(tr.Bpm - Fx.ProjectBpm) < 0.6 ? ", the same as the project." : ". The project is at " + Fx.ProjectBpm.ToString("0.#") + " BPM: press DETECT to use this track's tempo."); }
        string role = bus ? "BUS" : (tr.Instrument ? "SYNTH" : (tr.Role == "vocal" ? "VOCAL" : (tr.Role == "beat" ? "BEAT" : (tr.Role == "song" ? "SONG" : "AUDIO"))));
        int layers = 0; foreach (var lx in eng.Tracks) if (lx.Host == tr) layers++;
        if (layers > 0) { var chip = thin ? Rc(r.X + 242, r.Y + 8, 52, 18) : Rc(r.X + 186, r.Y + 27, 52, 15); FillRR(g, Hot(chip) ? TM.C(200, 90, 50, 170) : TM.C(150, 50, 30, 100), chip, 7); Txt(g, "+" + layers + " layer" + (layers == 1 ? "" : "s"), 9, Ink, chip, StringAlignment.Center, true); var hostTr = tr; Reg(chip, () => LayerMenu(hostTr)); if (Hot(chip)) { bubble = "Tracks sharing this row. Click to mute, separate or delete them."; bubblePt = MousePos; } }
        var pill = Rc(r.X + 186, r.Y + 8, 52, 18); FillRR(g, Color.FromArgb(90, Neon(tr.Color)), pill, 9); StrokeRR(g, Color.FromArgb(200, Neon(tr.Color)), pill, 9, 1); Txt(g, role, 10, Color.White, pill, StringAlignment.Center, true);
        int ti = i; var tk = tr;
        Btn(g, Rc(r.Right - 34, r.Y + 7, 22, 22), "x", 3, () => RemoveTrack(tk), 13);
        var nameReg = Reg(Rc(r.X + 4, r.Y + 4, 176, 26), () => HeaderDown(tk)); nameReg.OnDrag = pt => HeaderDrag(tk, pt); nameReg.OnUp = () => { HeaderDrop(tk); GhostEnd(); }; nameReg.OnDouble = () => RenameTrack(tk); nameReg.OnRight = () => RenameTrack(tk);
        if (Hot(Rc(r.X + 4, r.Y + 4, 176, 26))) bubble = "Drag up or down to reorder.  Double-click to rename."; bubblePt = MousePos;
        { var dotR = Rc(r.X + 7, r.Y + 4, 26, 26); Reg(dotR, () => ToggleOff(tk)); if (Hot(dotR)) { bubble = tk.Off ? "This track is OFF. Press the power button to turn it back on." : "POWER: turn this track completely off. It goes quiet, dims, and shakes apart on the timeline."; bubblePt = MousePos; } }
        // M / S, + Add Effect, Out
        float bh = thin ? 24 : 30;
        Btn(g, Rc(r.X + 12, r.Y + by, 36, bh), "M", tk.Mute ? 4 : 0, () => ToggleMute(tk), 14);
        if (!bus) Btn(g, Rc(r.X + 54, r.Y + by, 36, bh), "S", tk.Solo ? 4 : 0, () => ToggleSolo(tk), 14);
        float ex = bus ? 54 : 96, ew = bus ? 154 : (tk.Instrument ? 74 : 112);
        { var eb = Rc(r.X + ex, r.Y + by, ew, bh); Btn(g, eb, "Plugins" + (tk.Fx.Count > 0 ? " (" + tk.Fx.Count + ")" : ""), 1, () => { if (bus) insBus = tk; else { selTrack = ti; insBus = null; } busEdit = null; OpenInspector(1); }, 12); Hint2(eb, "Open the mixer at the bottom on " + tk.Name + ": add, open and edit its effects there"); }
        if (!bus)
        {
            string outTxt = tk.Out == null ? "Bus" : tk.Out.Name;
            var ob = Rc(r.X + 212, r.Y + by, 48, bh); Btn(g, ob, outTxt, tk.Out == null ? 0 : 4, () => OutMenu(tk), 11);
            using (var tb = new SolidBrush(TM.C(200, 215, 200, 250))) g.FillPolygon(tb, new[] { new PointF(ob.Right - 11, ob.Bottom - 10), new PointF(ob.Right - 3, ob.Bottom - 10), new PointF(ob.Right - 7, ob.Bottom - 5) });
            if (Hot(ob)) { bubble = tk.Out == null ? "No bus. Click to pick a bus for this track (or make a new one)." : "Output: " + tk.Out.Name + ". Click to change."; bubblePt = MousePos; }
        }
        { float sx = r.X + (bus ? 218 : 272), sh = thin ? 22 : (compact ? 24 : 26), gp = thin ? 2 : 4; Btn(g, Rc(sx, r.Y + by, 66, sh), "STEREO", tk.Mono ? 0 : 4, () => SetMono(tk, false), 10); Btn(g, Rc(sx, r.Y + by + sh + gp, 66, sh), "MONO", tk.Mono ? 4 : 0, () => SetMono(tk, true), 11); }
        if (tk.Instrument) Btn(g, Rc(r.X + 176, r.Y + by, 32, bh), "C", 0, () => { if (MessageBox.Show(this, "Delete every recorded note on \"" + tk.Name + "\"?", "Clear notes", MessageBoxButtons.YesNo) == DialogResult.Yes) lock (tk.Midi) tk.Midi.Clear(); }, 12);
        // fader
        float frac = Clamp01((tk.VolDb + 48f) / 54f);
        Txt(g, "VOL", 11, DimC, Rc(r.X + 12, r.Y + vy, 30, 18), StringAlignment.Near, false);
        var fs = Slider(g, Rc(r.X + (thin ? 40 : 44), r.Y + vy, thin ? 84 : 112, 18), frac, f => tk.VolDb = f < 0.003f ? -48f : (float)Math.Round(f * 54f - 48f, 1), Accent2);
        fs.OnDouble = () => tk.VolDb = 0;
        ValueBox(g, Rc(r.X + (thin ? 130 : 172), r.Y + vy - 1, thin ? 64 : 80, 20), (tk.VolDb <= -47.9f ? "-inf" : tk.VolDb.ToString("+0.0;-0.0;0.0")) + " dB", tk.VolDb <= -47.9f ? "-inf" : Num(tk.VolDb), s => { float v; if (ParseNum(s, out v)) tk.VolDb = v <= -47.9f ? -48f : Math.Min(12f, v); });
        // pan
        if (thin) Txt(g, "PAN", 11, DimC, Rc(r.X + 200, r.Y + vy, 30, 18), StringAlignment.Near, false); else Txt(g, "PAN", 11, DimC, Rc(r.X + 12, r.Y + py, 30, 18), StringAlignment.Near, false);
        if (!thin) { var ps = Slider(g, Rc(r.X + 44, r.Y + py, 112, 18), (tk.Pan + 1) / 2, f => tk.Pan = (float)Math.Round(f * 2 - 1, 2)); ps.OnDouble = () => tk.Pan = 0; }
        if (!(thin && tk.Instrument)) ValueBox(g, thin ? Rc(r.X + 228, r.Y + vy - 1, 40, 20) : Rc(r.X + 172, r.Y + py - 1, 40, 20), tk.Pan == 0 ? "C" : (tk.Pan < 0 ? "L" + (int)(-tk.Pan * 100) : "R" + (int)(tk.Pan * 100)), Num(tk.Pan * 100), s => { string u = s.Trim().ToUpperInvariant(); float v; if (u.StartsWith("C")) tk.Pan = 0; else if (ParseNum(u, out v)) { if (u.StartsWith("L")) v = -Math.Abs(v); else if (u.StartsWith("R")) v = Math.Abs(v); tk.Pan = Math.Max(-1f, Math.Min(1f, v / 100f)); } });
        // right of the pan row: piano (instrument), arm (empty audio track), nudge (audio), nothing (bus)
        if (tk.Instrument)
            Btn(g, thin ? Rc(r.X + 200, r.Y + vy - 2, 66, 22) : Rc(r.X + 220, r.Y + py - 3, 40, 24), "Piano", 1, () => { var vf = tk.Fx.Count > 0 ? tk.Fx[0] as InstrumentFx : null; if (vf != null) { vf.Owner = tk; vf.ShowWindow(this); } }, 11);
        // meters (peak with hold)
        var h = Hold(tr); float[] pk = { tr.PeakL, tr.PeakR };
        for (int c = 0; c < 2; c++)
        {
            float v = pk[c]; h[c] = Math.Max(v, h[c] * 0.93f); float db = h[c] > 0 ? (float)(20 * Math.Log10(h[c])) : -90; float f = Clamp01((db + 54) / 54f);
            var mr = Rc(r.Right - 28 + c * 11, r.Y + by, 8, Math.Min(compact ? 76f : 96f, r.Height - by - 8)); LedMeter(g, mr, f);
        }
        if (!compact && tr.Clipped) { FillRR(g, Bad, Rc(r.Right - 30, r.Y + r.Height - 12, 24, 8), 3); Reg(Rc(r.Right - 30, r.Y + r.Height - 14, 24, 12), () => tr.Clipped = false); }
        if (r.Height >= 150 && !bus)
        {
            long offMs = tr.Offset * 1000 / StudioEngine.SR; Txt(g, "OFFSET MS", 10, DimC, Rc(r.X + 150, r.Y + r.Height - 22, 62, 16), StringAlignment.Far, false);
            ValueBox(g, Rc(r.X + 218, r.Y + r.Height - 25, 42, 19), offMs.ToString(), offMs.ToString(), s => { float v; if (ParseNum(s, out v)) tk.Offset = (long)(v * StudioEngine.SR / 1000.0); });
        }
        if (tr.Off) PaintHeaderOff(g, r, tr);
    }

    // choose where a track's sound goes: straight to the master, or to one of the buses (or a new bus)
    void OutMenu(Track tk)
    {
        var m = Win.NewMenu(); var master = new ToolStripMenuItem("No bus (straight to the master)") { Checked = tk.Out == null }; master.Click += (a, b) => { PushUndo(); lock (eng.Sync) tk.Out = null; }; m.Items.Add(master);
        foreach (var b0 in eng.Tracks.ToArray()) if (b0.IsBus) { var bus = b0; var it = new ToolStripMenuItem(bus.Name) { Checked = tk.Out == bus }; it.Click += (a, b) => { PushUndo(); lock (eng.Sync) tk.Out = bus; }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("New bus").Click += (a, b) => { int keep = selTrack; var nb = AddBus(); selTrack = keep; if (nb != null) lock (eng.Sync) tk.Out = nb; };
        m.Items.Add("Edit buses and their effects (the mixer)...").Click += (a, b) => OpenInspector(1);   // buses are strips on the mixer desk
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    Track AddBus()
    {
        int n = 1; bool used = true; while (used) { used = false; foreach (var x in eng.Tracks) if (x.IsBus && x.Name == "Bus " + n) { used = true; n++; break; } }
        var t = StudioEngine.NewTrack("Bus " + n, "bus", null); t.IsBus = true; t.Color = unchecked((int)0xFFFFD04D); int keepSel = selTrack; nextInsert = -1; AddTrack(t); selTrack = Math.Max(0, Math.Min(keepSel, LaneCount - 1));
        Toast(t.Name + " added: it has its own strip on the MIXER. Put tracks into it with + BUS on their strips, and add its effects there.");
        return t;
    }
    void PaintBusLane(Graphics g, Track tr, RectangleF lane)
    {
        var src = new List<string>(); foreach (var x in eng.Tracks) if (x.Out == tr) src.Add(x.Name);
        Txt(g, "BUS  -  " + (src.Count == 0 ? "nothing is sent here yet. Press Out on a track to send it to this bus." : "receives: " + string.Join(", ", src.ToArray())), 13, DimC, Rc(lane.X + 16, lane.Y + lane.Height / 2 - 12, lane.Width - 32, 24), StringAlignment.Near, false);
    }
    // a track with no audio has its own box inside its border: click it to pick music for THIS track, or drop files on it (the same box as an empty project, made to fit the track)
    readonly Dictionary<Track, RectangleF> emptyLanes = new Dictionary<Track, RectangleF>(); readonly Dictionary<Track, int> emptyLaneAt = new Dictionary<Track, int>(); Track fillTrack;
    // the track that is picked (click its name or its lane): a bright outline round the whole row (the microphone puts a recording on the picked audio track)
    // the plugin you are editing: bright fill, a neon outline with a glow and a bar down its left side, so you always see which one is open
    void PaintPickedFx(Graphics g, RectangleF row, float rad)
    {
        Color sc = UiTokens.Sel(NeonCyan); Glow(g, sc, row, rad, 0.9f, 4); FillRR(g, TM.C(235, 120, 50, 200), row, rad); FillRR(g, Color.FromArgb(95, sc), row, rad); if (UiTokens.Shiny) PaintMaterial(g, row, rad, sc, UiTokens.Finish, false); StrokeRR(g, sc, row, rad, 2.2f);
        using (var b = new SolidBrush(sc)) g.FillRectangle(b, row.X + 2, row.Y + 5, 3, row.Height - 10);
    }
    void PaintPickedRow(Graphics g, Track tr, RectangleF row)
    {
        Color c = micArmed || mic != null ? Color.FromArgb(255, 255, 90, 120) : NeonCyan; using (var p = RR(row, 6)) { using (var pen = new Pen(Color.FromArgb(70, c), 5f)) g.DrawPath(pen, p); using (var pen = new Pen(Color.FromArgb(230, c), 1.6f)) g.DrawPath(pen, p); }
    }
    void PaintEmptyLane(Graphics g, Track tr, RectangleF lane)
    {
        var r0 = Rc(lane.X + 6, lane.Y + 4, lane.Width - 12, lane.Height - 8); if (r0.Width < 60 || r0.Height < 24) return; emptyLanes[tr] = r0; emptyLaneAt[tr] = Environment.TickCount;
        bool over = Hot(r0), drop = dragOver && r0.Contains(PointToClient(Cursor.Position)); FillRR(g, TM.C(drop ? 170 : (over ? 140 : 100), 40, 20, 80), r0, 12); if (over || drop) Glow(g, drop ? NeonCyan : NeonViolet, r0, 12, drop ? 1.1f : 0.7f, 3);
        using (var pen = new Pen(Color.FromArgb(drop ? 255 : 140, drop ? NeonCyan : (tr.Armed ? Color.FromArgb(255, 120, 140) : NeonViolet)), 1.6f) { DashStyle = DashStyle.Dash }) { var inner = RectangleF.Inflate(r0, -5, -4); using (var p = RR(inner, 9)) g.DrawPath(pen, p); }
        float tw = r0.Width - 40; bool tall = r0.Height >= 84, mid = r0.Height >= 54;
        Action<string, float, Color, float, bool> line = (t, sz0, col, yy, bold) => { float sz = sz0; while (sz > 9f && g.MeasureString(t, GfxCache.Font(sz, bold)).Width > tw) sz -= 0.5f; if (g.MeasureString(t, GfxCache.Font(sz, bold)).Width > tw) { int cut = Math.Min(t.Length, Math.Max(8, (int)(t.Length * tw / Math.Max(1f, g.MeasureString(t, GfxCache.Font(sz, bold)).Width)) - 3)); t = t.Substring(0, cut).TrimEnd() + "..."; } Txt(g, t, sz, col, Rc(r0.X + 20, yy, tw, sz * 1.7f), StringAlignment.Center, bold); };
        string t2 = tr.Armed ? "Ready to record: press MIC. Or drag audio or drop files onto it." : "Drag a clip onto it, drop files on it, record with MIC, or right-click it to add music.";
        if (tall) { line(drop ? "Drop your music here" : "EMPTY TRACK", 18, Ink, r0.Y + r0.Height * 0.14f, true); line(t2, 12, drop ? Ink : DimC, r0.Y + r0.Height * 0.46f, false); line("MP3, WAV, FLAC, M4A, OGG and more.", 10, DimC, r0.Y + r0.Height * 0.72f, false); }
        else if (mid) { line(drop ? "Drop your music here" : "EMPTY TRACK", 14, Ink, r0.Y + r0.Height * 0.14f, true); line(t2, 10.5f, drop ? Ink : DimC, r0.Y + r0.Height * 0.55f, false); }
        else line(drop ? "Drop your music here" : "EMPTY TRACK: drag or drop audio here, or right-click it", 12, Ink, r0.Y + (r0.Height - 20) / 2f, true);
        if (over) { bubble = "An empty track: drag audio onto it, or right-click it to add music"; bubblePt = MousePos; }
    }
    // the empty track under a point (a drop of files lands on it), null when there is none
    Track EmptyTrackAt(Point p) { foreach (var kv in emptyLanes) { int at; if (kv.Key.Clip == null && eng.Tracks.Contains(kv.Key) && emptyLaneAt.TryGetValue(kv.Key, out at) && unchecked(Environment.TickCount - at) < 400 && kv.Value.Contains(p.X, p.Y)) return kv.Key; } return null; }

    void RenameTrack(Track tr)
    {
        string nm = Ask("Track name:", tr.Name); if (nm == null) return;
        tr.Name = nm.Trim(); Toast("Renamed to " + tr.Name);
    }

    void RenameProject()
    {
        string nm = Ask("Project name:", projName == "Untitled project" ? "My song" : projName); if (nm == null) return;
        foreach (char c in Path.GetInvalidFileNameChars()) nm = nm.Replace(c, '_'); nm = nm.Trim(); if (nm.Length == 0) return;
        if (!string.IsNullOrEmpty(projPath) && File.Exists(projPath))
        {
            try { string np = Path.Combine(Path.GetDirectoryName(projPath), nm + ".ytmproj"); if (!File.Exists(np)) { File.Move(projPath, np); projPath = np; } else { Toast("A project with that name already exists here."); return; } }
            catch (Exception ex) { Toast("Couldn't rename the file: " + ex.Message); return; }
        }
        projName = nm; Toast("Project renamed to " + nm);
    }

    void RemoveTrack(Track tr)
    {
        if (MessageBox.Show(this, "Remove \"" + tr.Name + "\" from the project? (Your audio file on disk is not touched.)", "Remove track", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        DeleteTrackQuiet(tr);
    }

    // ---------------- plugin panel (right side) ----------------
    void PaintPluginPanel(Graphics g)
    {
        float x = ClientSize.Width - PanelW, y = ToolH + TransH, w = soundsOpen && tab == 0 ? PanelW : 380, h = ClientSize.Height - y;   // while it slides the panel keeps its full width (the part past the edge is off screen)
        if (!(soundsOpen && tab == 0) && PanelW <= PanelTabW + 1)
        {   // tucked away: a thin tab on the right edge (click it, or the >> button brought you here)
            var strip = Rc(x, y, PanelW, h); bool hotS = Hot(strip); int nfx = 0; if (eng.Tracks.Count > 0) { var tS = busEdit ?? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))]; lock (eng.Sync) nfx = tS.Fx.Count; }
            using (var br = new SolidBrush(hotS ? TM.C(255, 42, 22, 78) : TM.C(255, 20, 11, 38))) g.FillRectangle(br, strip);
            using (var pen = new Pen(Color.FromArgb(hotS ? 240 : 160, NeonViolet), 1.6f)) g.DrawLine(pen, x, y, x, y + h);
            PanelHandle(g, x, y, h, false);
            var gs = g.Save(); g.TranslateTransform(x + PanelW / 2f + 8, y + 44); g.RotateTransform(90); Txt(g, "PLUGINS" + (nfx > 0 ? "  (" + nfx + ")" : ""), 12, hotS ? Ink : DimC, new RectangleF(0, 0, 240, 16), StringAlignment.Near, true); g.Restore(gs);
            Reg(strip, () => TogglePanel()); Hint2(strip, "Show the plugin panel: add, edit and save plugins");
            return;
        }
        var pr = Rc(x, y, w, h); using (var br = new SolidBrush(TM.C(255, 20, 11, 38))) g.FillRectangle(br, pr);
        using (var pen = new Pen(Edge)) g.DrawLine(pen, x, y, x, y + h);
        Reg(pr, () => { });
        if (!(soundsOpen && tab == 0)) PanelHandle(g, x, y, h, true);
        if (eng.Tracks.Count == 0) { Txt(g, "SELECT A TRACK TO EDIT ITS PLUGINS", 14, DimC, pr, StringAlignment.Center, false); return; }
        if (busEdit != null && !eng.Tracks.Contains(busEdit)) busEdit = null; var tr = busEdit ?? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))];   // EDIT on a bus shows that bus here
        PaintPluginsTitle(g, tr, Rc(x + 14, y + 6, w - 28 - (busEdit != null ? 126 : 0), 30));   // which track these plugins are on: its number, its kind and the end of its name, in its colour (two tracks of one song start with the same long name)
        if (busEdit != null) { var bk = Rc(x + w - 132, y + 8, 120, 24); Btn(g, bk, "BACK TO TRACKS", 2, () => { busEdit = null; selFx = -1; }, 10); Hint2(bk, "Stop editing the bus: the plugins of the picked track show again"); }
        else { }
        Fx[] fxs; lock (eng.Sync) fxs = tr.Fx.ToArray(); float origW = w; { int n0 = fxs.Length; float est = (tr.IsBus ? 188 : 218) + n0 * 32 + GroupExtra(fxs) + 8 + ((selFx >= 0 && selFx < n0) ? EditorHeight(fxs[selFx]) : 60); if (est - h + 6 > 1 && h > 200) w -= 52; }
        int ti = Array.IndexOf(eng.Tracks.ToArray(), tr);
        // the tools, one per row (full width, the same height)
        float hb; { float bx = x + 10, bw2 = w - 20, by = y + 38; int bi = 0; Func<RectangleF> nextRow = () => Rc(bx, by + (bi++) * 30, bw2, 26);
          var r1 = nextRow(); Btn(g, r1, "MIXER", InsShown && insTab == 1 ? 4 : 2, () => OpenInspector(1), 11); Hint2(r1, "This track's plugins in the panel at the bottom: open each in its own window, add, bypass, move and remove them (buses are there too)");
          { var vb = nextRow(); Btn(g, vb, "VSTS", browserOpen && browserVst ? 4 : 2, () => OpenVsts(), 11); Hint2(vb, "Bass Lab, your VST instruments and the Purphex VSTs: choose the track (FOR) and add one"); }
          var snb = nextRow(); Btn(g, snb, "SOUND PACKS", soundsOpen ? 4 : 0, () => { if (soundsOpen) CloseSounds(); else OpenPacks(); }, 11);
          hb = by + bi * 30 + 4; }
        if (InsShown)
        {   // the mixer panel is open: every plugin is down there (one place for plugins); a click on a plugin there opens its own window
            var nb = Rc(x + 6, hb + 4, w - 12, 118); FillRR(g, TM.C(70, 12, 6, 28), nb, 10); Glow(g, NeonPink, nb, 10, 0.35f, 3); StrokeRR(g, Color.FromArgb(190, NeonPink), nb, 10, 1.2f);
            Txt(g, "YOUR PLUGINS ARE IN THE MIXER", 11, NeonPink, Rc(nb.X + 12, nb.Y + 6, nb.Width - 24, 20), StringAlignment.Near, true);
            Txt(g, "Every track's plugins are in the panel below.", 10, DimC, Rc(nb.X + 12, nb.Y + 30, nb.Width - 24, 18), StringAlignment.Near, false); Txt(g, "Click a plugin there to open its window.", 10, DimC, Rc(nb.X + 12, nb.Y + 50, nb.Width - 24, 18), StringAlignment.Near, false);
            var hm = Rc(nb.X + 12, nb.Bottom - 34, nb.Width - 24, 26); Btn(g, hm, "HIDE THE MIXER", 0, () => CloseInspector(), 10); Hint2(hm, "Close the mixer panel: the plugins show here again");
            return;
        }
        float ly = hb - panelScroll; int n = fxs.Length; g.SetClip(Rc(x, hb - 4, w, h - (hb - 4 - y)));
        if (selFx >= n) selFx = -1;
        // the boxes: the instrument (Bass Lab or a VST), every preset that was added (each one its own box, they never replace each other) and YOUR PLUGINS (always there)
        var ig = new PGroup { Title = "INSTRUMENT", Col = NeonCyan, Kind = 0 }; var og = new PGroup { Title = "YOUR PLUGINS", Col = NeonPink, Kind = 2 }; var pgs = new List<PGroup>(); bool anyFx = false;
        for (int i = 0; i < n; i++)
        {
            var f0 = fxs[i]; if (f0 is InstrumentFx) ig.Items.Add(i);
            else if (f0.Preset.Length > 0) { anyFx = true; string key = f0.PresetId.Length > 0 ? f0.PresetId : f0.Preset; var g1 = pgs.Find(q => q.Id == key); if (g1 == null) { g1 = new PGroup { Title = "PRESET: " + f0.Preset.ToUpperInvariant(), Col = NeonViolet, Kind = 1, Id = key }; pgs.Add(g1); } g1.Items.Add(i); }
            else { anyFx = true; og.Items.Add(i); }
        }
        var groups = new List<PGroup>(); if (ig.Items.Count > 0) groups.Add(ig); groups.AddRange(pgs); groups.Add(og);
        foreach (var gp in groups)
        {
            float head = 28, bh = head + Math.Max(1, gp.Items.Count) * 32 + 2;
            var box = Rc(x + 6, ly, w - 12, bh); FillRR(g, TM.C(70, 12, 6, 28), box, 10); Glow(g, gp.Col, box, 10, 0.35f, 3); StrokeRR(g, Color.FromArgb(190, gp.Col), box, 10, 1.2f);
            Txt(g, gp.Title, 11, gp.Col, Rc(box.X + 12, box.Y + 4, box.Width - (gp.Kind == 2 ? 92 : 110), 20), StringAlignment.Near, true);
            if (gp.Kind == 1) { string pid = gp.Id; var rb = Rc(box.Right - 86, box.Y + 4, 78, 20); Btn(g, rb, "REMOVE", 3, () => { ChainApply(tr, "Preset removed", () => RemovePreset(tr, pid)); selFx = -1; }, 10); Hint2(rb, "Take away this preset only (every other preset and your own plugins stay)"); }
            if (gp.Kind == 2) { var pst = Rc(box.Right - 74, box.Y + 4, 60, 20); Btn(g, pst, "PASTE", fxCopyType.Length > 0 ? 2 : 0, () => PasteFxAsNew(tr), 10); Hint2(pst, fxCopyType.Length > 0 ? "Add the copied " + fxCopyTitle + " with its settings as a new effect on this track" : "Copy an effect first: press COPY on a plugin"); }
            ly += head;
            if (gp.Items.Count == 0) { Txt(g, "NOTHING YET: ADD EFFECTS IN THE MIXER", 11, DimC, Rc(box.X + 14, ly, box.Width - 28, 28), StringAlignment.Near, false); ly += 32; }
            foreach (int i in gp.Items)
            {
                var f = fxs[i]; int fi = i; bool sel = i == selFx;
                var row = Rc(x + 14, ly, w - 36, 28); if (sel) PaintPickedFx(g, row, 6); else FillRR(g, TM.C(150, 40, 22, 74), row, 6);   // the plugin you are editing stands out
                Reg(row, () => selFx = fi); { var rw2 = Reg(row, () => selFx = fi); rw2.OnDouble = () => OpenFxWindow(tr, f); }   // double-click: its own window
                var dotr = Rc(row.X + 6, row.Y + 3.5f, 21, 21);   /* the on/off circle, 50% bigger */ using (var b2 = new SolidBrush(f.Bypass ? TM.C(90, 90, 70, 110) : Good)) g.FillEllipse(b2, dotr);
                Reg(dotr, () => { f.Bypass = !f.Bypass; });
                float cpl = CopyPasteBtns(g, f, tr, row.Right - 80, row.Y + 3, 22);   // COPY and PASTE on every plugin
                bool grRoom = cpl - (row.X + 34) > 150; RowTitle(g, f.Title, 14, f.Bypass ? DimC : Ink, Rc(row.X + 34, row.Y, cpl - row.X - 38 - (grRoom && f.GR > 0.2f ? 44 : 0), 28), sel);
                if (grRoom && f.GR > 0.2f) Txt(g, "-" + f.GR.ToString("0.0"), 11, Warn, Rc(cpl - 46, row.Y, 40, 28), StringAlignment.Far, false);
                { var ub = Rc(row.Right - 76, row.Y + 3, 22, 22); Btn(g, ub, " ", 0, () => MoveFx(tr, fi, -1), 11); ArrowIcon(g, ub, true); Hint2(ub, "Move " + f.Title + " up the chain (it works on the sound earlier)"); }
                { var db = Rc(row.Right - 52, row.Y + 3, 22, 22); Btn(g, db, "  ", 0, () => MoveFx(tr, fi, 1), 11); ArrowIcon(g, db, false); Hint2(db, "Move " + f.Title + " down the chain (it works on the sound later)"); }
                Btn(g, Rc(row.Right - 28, row.Y + 3, 22, 22), "x", 3, () => { if (fi < tr.Fx.Count) { PushUndo(); Fx rf; lock (eng.Sync) { rf = tr.Fx[fi]; tr.Fx.RemoveAt(fi); tr.Auto.RemoveAll(al => al.Fx == rf); retired.Add(rf); } var ic = rf as InstrumentFx; if (ic != null) ic.CloseWindow(); } selFx = -1; }, 12);   // undoable: retired, not disposed, since undo snapshots still hold it (a disposed VST brought back by Ctrl+Z crashed the app)
                ly += 32;
            }
            ly += 10;
        }
        { int nOwn = 0; foreach (var ff in tr.Fx) if (!(ff is InstrumentFx)) nOwn++; var spb = Rc(x + 6, ly, w - 12, 34); Btn(g, spb, "SAVE PRESET", nOwn > 0 ? 2 : 0, () => SaveTrackPreset(tr), 13);   // under the effects: save this chain (every plugin with its settings) as your own preset
          Hint2(spb, nOwn > 0 ? "Save the " + nOwn + " plugin" + (nOwn == 1 ? "" : "s") + " of " + tr.Name + " (with every setting) as your own preset: it is in PRESETS... on any track" : "Add effects first, then save them as a preset"); ly += 42; }
        float ey = ly + 8;
        float edH = (selFx >= 0 && selFx < n) ? EditorHeight(fxs[selFx]) : 60;
        if (selFx >= 0 && selFx < n) PaintEditor(g, fxs[selFx], Rc(x + 6, ey, w - 12, edH), tr);
        else Txt(g, "CLICK A PLUGIN TO EDIT IT", 13, DimC, Rc(x, ey, w, 40), StringAlignment.Center, false);
        g.ResetClip();
        float contentBottom = ly + panelScroll + 8 + ((selFx >= 0 && selFx < n) ? edH : 60), maxScroll = Math.Max(0, contentBottom - (y + h) + 6); panelScroll = Math.Min(panelScroll, maxScroll);
        regs.Insert(0, new HitReg { R = Rc(x, hb - 4, origW, h - (hb - 4 - y)), OnWheel = d => panelScroll = Math.Max(0, Math.Min(maxScroll, panelScroll - d * 50)) });
        if (origW != w) VScroll(g, Rc(x + origW - 50, hb, 42, h - (hb - y) - 8), h - (hb - y) - 8 + maxScroll, h - (hb - y) - 8, () => panelScroll, v => panelScroll = v);
    }

    // Exact height of an effect editor, so the panel scrolls only as far as its content (no endless blank space).
    // ---- the row under the plugin's name: the preset that is on it, COPY / PASTE of the whole plugin (its settings), and REMOVE PRESET (back to the plugin's own defaults) ----
    string fxCopyType = "", fxCopyData = "", fxCopyTitle = "";
    internal static string PresetShown(Fx f) { if (f.CurPreset.Length == 0) return "PRESET: NONE"; return "PRESET: " + f.CurPreset.ToUpper() + (f.Serialize() == f.CurPresetData ? "" : "  (CHANGED)"); }
    float PaintPresetRow(Graphics g, Fx f, Track tr, RectangleF a, float y)
    {
        float x = a.X + 10, right = a.Right - 10; var rm = Rc(right - 64, y - 6, 64, 22); var pa = Rc(rm.X - 54, y - 6, 50, 22); var co = Rc(pa.X - 50, y - 6, 46, 22);
        float tw = co.X - x - 6; Txt(g, PresetShown(f), 10, f.CurPreset.Length == 0 ? DimC : NeonCyan, Rc(x, y - 6, Math.Max(20, tw), 22), StringAlignment.Near, f.CurPreset.Length > 0);
        Btn(g, co, "COPY", 0, () => CopyFx(f), 10); Hint2(co, "Copy this plugin with all its settings");
        Btn(g, pa, "PASTE", fxCopyType.Length > 0 ? 2 : 0, () => PasteFx(f, tr), 10); Hint2(pa, fxCopyType.Length > 0 ? "Paste the copied " + fxCopyTitle + ": onto this plugin if it is the same kind, otherwise as a new plugin on this track" : "Copy a plugin first (COPY)");
        Btn(g, rm, "REMOVE", f.CurPreset.Length > 0 ? 3 : 0, () => RemoveFxPreset(f), 10); Hint2(rm, "Take the preset off: the plugin goes back to its own default settings (REVERT puts it back)");
        return y + 28;
    }
    void PaintPresetInline(Graphics g, Fx f, Track tr, RectangleF a, float ty)
    {
        float x = a.X + 10, right = a.Right - 176 - 8; var rm = Rc(right - 58, ty + 1, 58, 22); var pa = Rc(rm.X - 48, ty + 1, 44, 22); var co = Rc(pa.X - 46, ty + 1, 42, 22);
        float tx = x + Math.Min(260f, g.MeasureString(f.Title, GfxCache.Font(16, true)).Width + 14); float tw = co.X - tx - 6; if (tw > 46) Txt(g, PresetShown(f), 10, f.CurPreset.Length == 0 ? DimC : NeonCyan, Rc(tx, ty + 1, tw, 22), StringAlignment.Near, f.CurPreset.Length > 0);   // COPY, PASTE and REMOVE are always there, the preset name gets the room that is left
        Btn(g, co, "COPY", 0, () => CopyFx(f), 10); Hint2(co, "Copy this plugin with all its settings");
        Btn(g, pa, "PASTE", fxCopyType.Length > 0 ? 2 : 0, () => PasteFx(f, tr), 10); Hint2(pa, fxCopyType.Length > 0 ? "Paste the copied " + fxCopyTitle + ": onto this plugin if it is the same kind, otherwise as a new plugin" : "Copy a plugin first (COPY)");
        Btn(g, rm, "REMOVE", f.CurPreset.Length > 0 ? 3 : 0, () => RemoveFxPreset(f), 10); Hint2(rm, "Take the preset off: the plugin goes back to its own default settings (REVERT puts it back)");
    }
    internal void RemoveFxPreset(Fx f)
    {
        if (f.CurPreset.Length == 0) { Toast("No preset is on this plugin."); return; }
        string before = f.Serialize(), pn = f.CurPreset, pd = f.CurPresetData; bool bp = f.Bypass;
        lock (eng.Sync) { f.ResetDefaults(); f.Reset(); } f.CurPreset = ""; f.CurPresetData = "";
        OfferRevert(pn + " removed from " + f.Title, () => { lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(before); f.Bypass = bp; f.Reset(); f.CurPreset = pn; f.CurPresetData = pd; } });
        Toast("Preset removed: " + f.Title + " is back to its default settings.");
    }
    // the COPY and PASTE buttons of one plugin, ending at right; returns where they start. PASTE on a plugin of the same kind takes the copied settings, on another kind it adds the copied plugin as a new one
    internal float CopyPasteBtns(Graphics g, Fx f, Track tr, float right, float y, float h)
    {
        var pa = Rc(right - 46, y, 46, h); var co = Rc(pa.X - 44, y, 40, h); var ff = f; var tk = tr;
        Btn(g, co, "COPY", 0, () => CopyFx(ff), 9); Hint2(co, "Copy " + f.Title + " with all its settings");
        Btn(g, pa, "PASTE", fxCopyType.Length > 0 ? 2 : 0, () => PasteFx(ff, tk), 9); Hint2(pa, fxCopyType.Length > 0 ? (fxCopyType == f.Type ? "Paste the copied settings onto this " + f.Title : "Add the copied " + fxCopyTitle + " as a new plugin") : "Copy a plugin first (COPY)");
        return co.X;
    }
    // a plugin's name in its row: one line when it fits, else two smaller lines (a long name beside COPY and PASTE on a narrow window)
    void RowTitle(Graphics g, string s, float px, Color c, RectangleF r, bool bold)
    {
        if (s.IndexOf(' ') < 0 || g.MeasureString(s, GfxCache.Font(10.5f, bold)).Width <= r.Width) { Txt(g, s, px, c, r, StringAlignment.Near, bold); return; }
        TxtWrap(g, s, 10f, c, Rc(r.X, r.Y + 1, r.Width, r.Height - 1));
    }
    // a small drawn arrow (up or down) for the buttons that move a plugin in the chain
    static void ArrowIcon(Graphics g, RectangleF r, bool up) { float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, d = up ? -1 : 1; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, new[] { new PointF(cx - 5, cy - 2.5f * d), new PointF(cx + 5, cy - 2.5f * d), new PointF(cx, cy + 3.5f * d) }); g.SmoothingMode = sm; }
    // the title of the plugin panel: TRACK 3  BEAT  ...Instrumental, in the colour of the track, so you always see whose plugins you are changing
    internal static string TailName(string n) { n = n ?? ""; int i = n.LastIndexOf(" - "); string tail = i > 0 && i < n.Length - 3 ? n.Substring(i + 3) : n; return tail.Length > 0 ? tail : n; }
    void PaintPluginsTitle(Graphics g, Track tr, RectangleF r)
    {
        int row = Math.Max(0, eng.Tracks.IndexOf(tr.Host ?? tr)) + 1; Color c = Neon(tr.Color); string kind = tr.IsBus ? "BUS" : (tr.Instrument ? "INSTRUMENT" : (tr.Role ?? "audio").ToUpperInvariant());
        using (var b = new SolidBrush(c)) g.FillRectangle(b, r.X - 6, r.Y + 4, 4, r.Height - 8);
        string head = tr.IsBus ? "EDITING BUS" : "TRACK " + row + "  " + kind; float hw = Math.Min(r.Width * 0.55f, g.MeasureString(head, GfxCache.Font(12, true)).Width + 10);
        Txt(g, head, 12, c, Rc(r.X, r.Y, hw, r.Height), StringAlignment.Near, true);
        var rowT = tr.Host ?? tr; string shownName = rowT.Name.Trim().Length > 0 ? rowT.Name : "Track " + row; Txt(g, shownName, 15, Ink, Rc(r.X + hw, r.Y, r.Width - hw, r.Height), StringAlignment.Near, true);   // the name of the track, as on its box on the left
        if (Hot(r)) { bubble = "These plugins are on " + tr.Name + ". Press the box of another track on the left to change its plugins."; bubblePt = MousePos; }
    }
    internal Track busEdit;   // a bus being edited in the plugin panel (EDIT in the Buses sheet)
    internal void EditBus(Track bt) { busEdit = bt; busOpen = false; tab = 0; selFx = bt.Fx.Count > 0 ? 0 : -1; panelScroll = 0; Toast("Editing " + bt.Name + ": its effects are in the plugin panel. Open, change, switch off, move or remove them. BACK TO TRACKS when you are done."); }
    internal bool RowHasPieces(Track row) { foreach (var p in eng.Tracks) if (p.Host == row && (p.Clip != null || p.Instrument)) return true; return false; }
    bool fxCopyKeyPending;   // the copied plugin was still waiting to hear the key: a pasted copy keeps the key that was copied, it does not detect a new one over it
    void Adopt(Fx nf, Track tr) { nf.AutoKeyPending = fxCopyKeyPending; var ic = nf as InstrumentFx; if (ic != null && tr != null) ic.Owner = tr; }
    internal void CopyFx(Fx f) { fxCopyType = f.Type; fxCopyData = f.Serialize(); fxCopyTitle = f.Title; fxCopyKeyPending = f.AutoKeyPending; Toast("Copied " + f.Title + " with its settings. PASTE adds it to any track or to the master."); }
    // PASTE next to the list: the copied effect becomes a NEW effect at the end of the chain (a track, or the master when tr is null)
    internal void PasteFxAsNew(Track tr)
    {
        if (fxCopyType.Length == 0) { Toast("Nothing is copied yet: press COPY on a plugin."); return; }
        var nf = Presets.Make(fxCopyType, fxCopyData); if (nf == null) { Toast("That effect cannot be pasted here."); return; } Adopt(nf, tr);
        List<Fx> chain = tr == null ? eng.MasterFx : tr.Fx; int at; lock (eng.Sync) { chain.Add(nf); at = chain.Count - 1; }
        if (tr == null) selMaster = at; else selFx = at;
        OfferRevert(fxCopyTitle + " pasted as a new effect", () => { lock (eng.Sync) { chain.Remove(nf); } if (tr == null) selMaster = Math.Min(selMaster, Math.Max(0, chain.Count - 1)); else selFx = -1; }); Toast(fxCopyTitle + " added with the copied settings.");
    }
    internal void PasteFx(Fx f, Track tr)
    {
        if (fxCopyType.Length == 0) { Toast("Nothing is copied yet: press COPY on a plugin."); return; }
        if (f.Type == fxCopyType)
        {
            string before = f.Serialize(), pn = f.CurPreset, pd = f.CurPresetData; bool bp = f.Bypass, kp = f.AutoKeyPending; lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(fxCopyData); f.Reset(); } f.CurPreset = ""; f.CurPresetData = ""; f.AutoKeyPending = fxCopyKeyPending;
            OfferRevert("Pasted settings on " + f.Title, () => { lock (eng.Sync) { f.ResetDefaults(); f.Deserialize(before); f.Bypass = bp; f.Reset(); f.CurPreset = pn; f.CurPresetData = pd; f.AutoKeyPending = kp; } }); Toast("Pasted the copied settings on " + f.Title + ".");
            return;
        }
        var nf = Presets.Make(fxCopyType, fxCopyData); if (nf == null) { Toast("That plugin cannot be pasted here (a VST plugin has to be installed on this PC)."); return; } Adopt(nf, tr);
        List<Fx> chain = tr == null ? eng.MasterFx : tr.Fx; int at; lock (eng.Sync) { chain.Add(nf); at = chain.Count - 1; }
        if (tr == null) selMaster = at; else selFx = at;
        OfferRevert(fxCopyTitle + " pasted as a new plugin", () => { lock (eng.Sync) { chain.Remove(nf); } if (tr == null) selMaster = Math.Min(selMaster, Math.Max(0, chain.Count - 1)); else selFx = -1; }); Toast(fxCopyTitle + " added as a new plugin with the copied settings.");
    }
    static float EditorHeight(Fx f)
    {
        int n = 0; { var hid = f.Hidden.Length > 0 ? new List<string>(f.Hidden.Split(',')) : new List<string>(); foreach (var p in f.P) if (!hid.Contains(p.Id)) n++; }
        if (f is InstrumentFx) return 34 + 38 + 16;
        if (PxPlugins.HasWindow(f)) return 34 + 28 + 42 + 420;   // its settings scroll inside (the window is where it is played)
        if (f is ParametricEq) return 668 + 28;
        if (f is Spatial8D) return 34 + 28 + 190 + Math.Max(0, n - 2) * 30 + 24;
        if (f is ConvFx) return 34 + 28 + 52 + n * 30 + 24;
        if (f is PitchCorrector) return 34 + 28 + 200 + Math.Max(0, n - 3) * 30 + 24;
        return 34 + 28 + (HasKeyParam(f) ? 34 : 0) + n * 30 + 24;   // (a plugin with a key has the AUTO DETECT KEY row)
    }

    sealed class PGroup { public string Title = "", Id = ""; public Color Col; public int Kind; public List<int> Items = new List<int>(); }   // one box of the plugin panel (Kind 0 instrument, 1 a preset, 2 your plugins)
    // which box a plugin lives in: the instrument, one use of a preset, or your own plugins
    static string FxKey(Fx f) { return f is InstrumentFx ? "I" : (f.Preset.Length > 0 ? "P" + (f.PresetId.Length > 0 ? f.PresetId : f.Preset) : "O"); }
    static float GroupExtra(Fx[] fxs)
    {
        bool inst = false, own = false; var keys = new HashSet<string>(); foreach (var f in fxs) { if (f is InstrumentFx) inst = true; else if (f.Preset.Length > 0) keys.Add(FxKey(f)); else own = true; }
        return ((inst ? 1 : 0) + keys.Count + 1) * 38 + (own ? 0 : 32);   // (YOUR PLUGINS is always there, with a line of its own when it is empty)
    }
    // a preset goes on the track WITHOUT taking anything away: the presets that are on it, your own plugins and the instrument all stay, and each preset is a box of its own
    void PutPreset(Track tr, string name, List<Fx> chain)
    {
        lock (eng.Sync)
        {
            var inst = new List<Fx>(); var pres = new List<Fx>(); var own = new List<Fx>();
            foreach (var f in tr.Fx) { if (f is InstrumentFx) inst.Add(f); else if (f.Preset.Length > 0) pres.Add(f); else own.Add(f); }
            string id = Guid.NewGuid().ToString("N").Substring(0, 8); foreach (var f in chain) if (!(f is InstrumentFx)) { f.Preset = name; f.PresetId = id; }
            tr.Fx.Clear(); tr.Fx.AddRange(inst); tr.Fx.AddRange(pres); tr.Fx.AddRange(chain); tr.Fx.AddRange(own);
        }
    }
    // id = one preset (its box); null = every preset. Your own plugins and the instrument stay.
    void RemovePreset(Track tr, string id = null)
    {
        lock (eng.Sync)
        {
            var gone = new List<Fx>(); foreach (var f in tr.Fx) if (!(f is InstrumentFx) && f.Preset.Length > 0 && (id == null || FxKey(f) == "P" + id)) gone.Add(f);
            foreach (var f in gone) tr.Fx.Remove(f); tr.Auto.RemoveAll(al => gone.Contains(al.Fx));
        }
    }
    // every plugin and preset off the track (RAW): the instrument stays, it is the sound
    // SAVE PRESET (the button under the effects, and the right-click menu): every plugin of the track with its settings, under a name you choose; it shows in PRESETS... on every track
    internal Func<string, string> savePresetAskHook;
    internal bool SaveTrackPreset(Track tr)
    {
        if (tr == null) return false; var own = tr.Fx.FindAll(f => !(f is InstrumentFx)); if (own.Count == 0) { Toast("Add some effects first (+ ADD EFFECT), then press SAVE PRESET."); return false; }
        string q = Prefs.ShareSaved ? "Name for this preset (it is also shared with the community):" : "Name for this preset:"; string nm = savePresetAskHook != null ? savePresetAskHook(q) : Ask(q, tr.Name + " chain");
        if (nm == null) return false; nm = nm.Replace("\r", " ").Replace("\n", " ").Trim(); if (nm.Length == 0) { Toast("A preset needs a name."); return false; }
        Presets.SaveChain(nm, own); Toast("Saved your preset \"" + nm + "\" (" + own.Count + " plugin" + (own.Count == 1 ? "" : "s") + "). Find it in PRESETS... on any track.");
        if (savePresetAskHook == null) CommunityPresets.AutoShare("chain", nm, Presets.ChainText(own), ProjectLibrary.ProfileName, msg => { if (msg != null) BeginInvoke(new Action(() => Toast(msg))); });
        return true;
    }
    void RemoveAllPlugins(Track tr)
    {
        lock (eng.Sync) { var gone = new List<Fx>(); foreach (var f in tr.Fx) if (!(f is InstrumentFx)) gone.Add(f); foreach (var f in gone) tr.Fx.Remove(f); tr.Auto.RemoveAll(al => gone.Contains(al.Fx)); }
    }

    void MoveFx(Track tr, int i, int d)
    {
        if (i < 0 || i >= tr.Fx.Count) return; string g0 = FxKey(tr.Fx[i]); int j = i + d; while (j >= 0 && j < tr.Fx.Count && FxKey(tr.Fx[j]) != g0) j += d;   // it moves inside its own box (instrument, preset or your plugins)
        if (j < 0 || j >= tr.Fx.Count) return; lock (eng.Sync) { if (i >= tr.Fx.Count) return; var f = tr.Fx[i]; tr.Fx.RemoveAt(i); tr.Fx.Insert(j, f); } selFx = j;
    }

    void ChainMenu(Track tr) { BuildChainMenu(tr).Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }
    ContextMenuStrip BuildChainMenu(Track tr)
    {
        var m = Win.NewMenu(); bool vocal = tr.Role == "vocal";
        if (tr.IsBus) { m.Items.Add("Bus Presets (searchable)...").Click += (a, b) => OpenBusPresets(); m.Items.Add("AI Bus Generator (describe the bus)...").Click += (a, b) => OpenAiBus(); m.Items.Add(new ToolStripSeparator()); }
        m.Items.Add("Vocal Presets (" + VocalStyles.All.Count + ", searchable)...").Click += (a, b) => OpenStyles();
        m.Items.Add("AI Preset Generator (describe the sound)...").Click += (a, b) => OpenAiPreset();
        if (!tr.IsBus) { m.Items.Add("Bus Presets (for the bus this track goes into)...").Click += (a, b) => OpenBusPresets(); m.Items.Add("AI Bus Generator (describe the bus)...").Click += (a, b) => OpenAiBus(); }
        { var pp = new ToolStripMenuItem(presetCopy.Count > 0 ? "Paste preset: " + presetCopyName + " (" + presetCopy.Count + " plugins)" : "Paste preset (copy one first: COPY on a preset box)") { Enabled = presetCopy.Count > 0 }; pp.Click += (a, b) => PastePreset(tr); m.Items.Add(pp); }
        { var keys = new List<string>(); foreach (var f in tr.Fx) if (!(f is InstrumentFx) && f.Preset.Length > 0 && !keys.Contains(f.PresetId.Length > 0 ? f.PresetId : f.Preset)) { string k = f.PresetId.Length > 0 ? f.PresetId : f.Preset; keys.Add(k); string nm = f.Preset; m.Items.Add("Copy preset: " + nm).Click += (a, b) => CopyPreset(tr, k); } }
        m.Items.Add("Copy all plugins of this track").Click += (a, b) => CopyAllFx(tr);
        { var pa = new ToolStripMenuItem(allCopy.Count > 0 ? "Paste all plugins (" + allCopy.Count + " from " + allCopyFrom + ")" : "Paste all plugins (copy them from a track first)") { Enabled = allCopy.Count > 0 }; pa.Click += (a, b) => PasteAllFx(tr); m.Items.Add(pa); }
        var beat = new ToolStripMenuItem("Instrumental chain (EQ, comp, multiband, sat, exciter, imager, ducker, limiter)"); beat.Click += (a, b) => { ChainApply(tr, "Instrumental chain", () => PutPreset(tr, "Instrumental chain", Presets.BeatChain())); selFx = -1; Toast("Beat chain added as a preset (REVERT puts it back)"); }; m.Items.Add(beat);
        m.Items.Add("Community presets (download and use)...").Click += (a, b) => OpenCommunityPresets("chain", "Plugin chains", cp => { var c = Presets.ChainFromText(cp.Data); if (c.Count == 0) throw new InvalidOperationException("it holds no plugins you have"); PutPreset(tr, cp.Name, c); selFx = -1; }, () => tr.Fx.Count == 0 ? null : Presets.ChainText(tr.Fx), tr.Name + " chain");
        var mine = new ToolStripMenuItem("My saved presets"); m.Items.Add(mine);
        foreach (var nm0 in Presets.UserChains()) { string nm = nm0; var it = new ToolStripMenuItem(nm); it.Click += (a, b) => { var c = Presets.LoadChain(nm); ChainApply(tr, nm, () => PutPreset(tr, nm, c)); selFx = -1; }; mine.DropDownItems.Add(it); }
        if (mine.DropDownItems.Count == 0) mine.DropDownItems.Add(new ToolStripMenuItem("(none yet)") { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Save this track's plugins as a preset...").Click += (a, b) => SaveTrackPreset(tr);
        return m;
    }

    // ---------------- effect browser (searchable) ----------------
    internal void OpenBrowser(bool master, int track) { browserOpen = true; browserQuery = ""; browserMaster = master; browserTrack = track; browserScroll = 0; blCache = null; browserVst = false; }
    // the VSTS button: the VSTs, put on the track you choose at the top (FOR:), the picked track to start with
    internal void OpenVsts()
    {
        if (!Paid("VSTS (Purphex VSTs and your own)")) return;
        int t = selTrack; if (t < 0 || t >= eng.Tracks.Count || eng.Tracks[t].IsBus || eng.Tracks[t].Host != null) t = eng.Tracks.FindIndex(x => !x.IsBus && x.Host == null);
        OpenBrowser(false, t); browserVst = true; browserCat = ""; blCache = null;
    }
    void VstTrackMenu()
    {
        var m = Win.NewMenu(); for (int i = 0; i < eng.Tracks.Count; i++) { var tq = eng.Tracks[i]; if (tq.IsBus || tq.Host != null) continue; int k = i; var it = new ToolStripMenuItem("Track " + (i + 1) + ": " + tq.Name) { Checked = i == browserTrack, ForeColor = Neon(tq.Color) }; it.Click += (a, b) => { browserTrack = k; selTrack = k; }; m.Items.Add(it); }
        if (m.Items.Count > 0) m.Items.Add(new ToolStripSeparator());
        m.Items.Add("A new track").Click += (a, b) => { var nt = StudioEngine.NewTrack("Track " + (eng.Tracks.Count + 1), "audio", null); AddTrack(nt); browserTrack = eng.Tracks.IndexOf(nt); selTrack = browserTrack; };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    void PaintBrowser(Graphics g)
    {
        Reg(Rc(0, 0, ClientSize.Width, ClientSize.Height), () => { if (!browserBox.Contains(MousePos)) browserOpen = false; });
        float w = 620, h = Math.Min(560, ClientSize.Height - 80), x = (ClientSize.Width - w) / 2, y = 60; browserBox = PopMove("browser", Rc(x, y, w, h), 52, 70); x = browserBox.X; y = browserBox.Y;
        FillRR(g, TM.C(252, 26, 14, 48), browserBox, 14); StrokeRR(g, Accent, browserBox, 14, 1.5f); var wr = new HitReg { R = browserBox, OnWheel = d => browserScroll = Math.Max(0, Math.Min(browserMaxF, browserScroll - d * 40)) }; regs.Add(wr); Reg(browserBox, () => { });
        Txt(g, browserVst ? "VSTS" : "ADD EFFECT", 20, Ink, Rc(x + 20, y + 12, 300, 30), StringAlignment.Near, true);
        Btn(g, Rc(x + w - 60, y + 14, 40, 28), "X", 3, () => browserOpen = false, 14);
        if (!browserVst) { var aie = Rc(x + w - 196 - 12 - 196, y + 14, 196, 28); Btn(g, aie, "AI EFFECT GENERATOR", 2, () => OpenAiEffect(true), 11); Hint2(aie, "Describe the effect you want in your own words (huge dark reverb, hard autotune in F# minor, telephone with distortion...) and get it"); }
        if (browserVst) Btn(g, Rc(x + w - 60 - 12 - 190, y + 14, 190, 28), "+ ADD YOUR OWN", 2, () => { browserOpen = false; if (pluginWin == null || pluginWin.IsDisposed) { pluginWin = new PluginManagerForm(() => app.ExtraFilter, s => { app.ExtraFilter = s; app.SaveSettings(); }); pluginWin.Show(this); } Win.Bring(pluginWin); }, 12);
        var sb = Rc(x + 20, y + 52, w - 40, 36); FillRR(g, TM.C(235, 14, 7, 32), sb, 8); StrokeRR(g, Color.FromArgb(60, Accent), Rc(sb.X - 2, sb.Y - 2, sb.Width + 4, sb.Height + 4), 10, 4f); StrokeRR(g, Accent, sb, 8, 2f);   // always active: it is ready to type the moment the list opens
        Reg(sb, () => { });
        if (browserQuery.Length > 0) Btn(g, Rc(sb.Right - 32, sb.Y + 6, 24, 24), "x", 3, () => { browserQuery = ""; browserScroll = 0; }, 12);
        Txt(g, browserQuery.Length == 0 ? (((int)(t * 2) % 2 == 0 ? "|" : " ") + "  Search here - just start typing: EQ, reverb, pitch...") : browserQuery + ((int)(t * 2) % 2 == 0 ? "|" : ""), 15, browserQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 12, sb.Y, sb.Width - 50, sb.Height), StringAlignment.Near, false);
        var list = BrowserList();
        if (browserVst) { var fb = Rc(x + 20, y + 94, 330, 26); var bt = browserTrack >= 0 && browserTrack < eng.Tracks.Count ? eng.Tracks[browserTrack] : null; Btn(g, fb, "FOR: " + (bt != null ? (browserTrack + 1) + ". " + bt.Name : "CHOOSE A TRACK") + "  v", bt != null ? 1 : 3, () => VstTrackMenu(), 12); Hint2(fb, "The track the VST goes on: pick one of your tracks or a new track"); Txt(g, list.Count + (list.Count == 1 ? " VST" : " VSTs"), 12, DimC, Rc(fb.Right + 12, y + 94, 160, 26), StringAlignment.Near, false); }
        else { var cb = Rc(x + 20, y + 94, 250, 26); Btn(g, cb, "Category: " + (browserCat.Length == 0 ? "All" : browserCat), browserCat.Length == 0 ? 0 : 4, () => CategoryMenu(), 12); Txt(g, list.Count + (list.Count == 1 ? " effect" : " effects"), 12, DimC, Rc(x + 280, y + 94, 200, 26), StringAlignment.Near, false); }
        float listTop = y + 128;
        float listH = 0; { string lc = ""; foreach (var i2 in list) { if (i2.Category != lc) { lc = i2.Category; listH += 20; } listH += 48; } }
        float browserMax = Math.Max(0, listH - (h - (listTop - y) - 10) + 8); browserMaxF = browserMax; browserScroll = Math.Max(0, Math.Min(browserMax, browserScroll));
        float ry = listTop - browserScroll; var clipR = Rc(x + 10, listTop - 4, w - 20, h - (listTop - y) - 28); g.SetClip(clipR);
        string lastCat = "";
        for (int li = 0; li < list.Count; li++)
        {
            var info = list[li];
            if (info.Category != lastCat)
            {
                lastCat = info.Category;
                if (info.Category == "Purphex VST")   // our own plugins with their own window: first, in a box of their own
                {
                    int pc = 0; for (int q = li; q < list.Count && list[q].Category == "Purphex VST"; q++) pc++; var pbox = Rc(x + 12, ry - 3, w - 24 - (browserMax > 0 ? 56 : 0), 20 + pc * 48 + 2);
                    FillRR(g, Color.FromArgb(130, 40, 16, 70), pbox, 12); Glow(g, PxPurple, pbox, 12, 0.7f, 3); StrokeRR(g, PxPurple, pbox, 12, 1.6f);
                    if (ry + 18 > clipR.Y && ry < clipR.Bottom) Txt(g, "PURPHEX VST  -  built in: our own plugins, each with its own window", 11, PxPurple, Rc(x + 26, ry, w - 100, 18), StringAlignment.Near, true);
                }
                else if (info.Category == "Instruments")   // Bass Lab and the VSTs are not effects: they sit apart, in a box of their own
                {
                    int ic = 0; for (int q = li; q < list.Count && list[q].Category == "Instruments"; q++) ic++; var ibox = Rc(x + 12, ry - 3, w - 24 - (browserMax > 0 ? 56 : 0), 20 + ic * 48 + 2);
                    FillRR(g, TM.C(120, 14, 24, 52), ibox, 12); Glow(g, NeonCyan, ibox, 12, 0.45f, 3); StrokeRR(g, Color.FromArgb(200, NeonCyan), ibox, 12, 1.3f);
                    if (ry + 18 > clipR.Y && ry < clipR.Bottom) Txt(g, "VST PLUGINS  -  they make the sound, they are not effects", 11, NeonCyan, Rc(x + 26, ry, w - 100, 18), StringAlignment.Near, true);
                }
                else if (ry + 18 > clipR.Y && ry < clipR.Bottom) Txt(g, info.Category.ToUpperInvariant(), 11, Accent, Rc(x + 24, ry, 300, 18), StringAlignment.Near, true);
                ry += 20;
            }
            if (ry + 44 < clipR.Y || ry > clipR.Bottom) { ry += 48; continue; }   // off screen: neither drawn nor given a click region (it would lie on top of the header)
            var row = Rc(x + 18, ry, w - 36 - (browserMax > 0 ? 56 : 0), 44); var vis = RectangleF.Intersect(row, clipR); bool hot = Hot(vis); FillRR(g, hot ? TM.C(220, 90, 50, 170) : TM.C(160, 44, 24, 80), row, 8);
            if (info.Category == "Purphex VST") { var gsB = g.Save(); g.SetClip(clipR, System.Drawing.Drawing2D.CombineMode.Intersect); Glow(g, PxPurple, row, 8, hot ? 1f : 0.6f, 3); StrokeRR(g, Color.FromArgb(hot ? 255 : 230, PxPurple), row, 8, hot ? 2f : 1.5f); g.Restore(gsB); }   // built in: a purple neon border that never changes with the theme
            Txt(g, info.Name, 15, Ink, Rc(row.X + 14, row.Y + 3, row.Width - 120, 20), StringAlignment.Near, true);
            Txt(g, info.Blurb, 12, DimC, Rc(row.X + 14, row.Y + 22, row.Width - 120, 18), StringAlignment.Near, false);
            string type = info.Type; if (vis.Width > 0 && vis.Height > 0) Reg(vis, () => AddFx(type));   /* the whole row adds it; the Add button sits on top of the row */
            if (row.Y + 8 >= clipR.Y && row.Y + 36 <= clipR.Bottom) Btn(g, Rc(row.Right - 86, row.Y + 8, 72, 28), "Add", 1, () => AddFx(type), 13); ry += 48;
        }
        if (list.Count == 0) Txt(g, "No plugin matches \"" + browserQuery + "\"", 14, DimC, Rc(x, y + 140, w, 30), StringAlignment.Center, false);
        g.ResetClip(); VScroll(g, Rc(x + w - 56, listTop, 42, h - (listTop - y) - 30), listH, h - (listTop - y) - 30, () => browserScroll, v => browserScroll = v);
        Txt(g, browserVst ? "Pick the track with FOR, then press Add  -  Enter adds the first match  -  Esc closes" : "Enter adds the first match  -  Esc closes  -  the VSTs are under VSTS in the right panel", 11, DimC, Rc(x + 20, y + h - 22, w - 40, 18), StringAlignment.Near, false);
    }
    RectangleF browserBox; float browserMaxF; string browserCat = "";
    static readonly Color PxPurple = Color.FromArgb(255, 178, 92, 255);   // the built-in plugins' own purple (the same in every theme)

    // the effects that match the search and category, grouped by category (your own plugins first)
    List<FxRegistry.Info> blCache; string blKey = "";
    List<FxRegistry.Info> BrowserList()
    {
        if (browserCat.Length > 0 && !Array.Exists(FxRegistry.All, i => i.Category == browserCat)) browserCat = "";   // a category that is not in the list any more shows everything
        string key = browserQuery + "\n" + browserCat + "\n" + browserVst; if (blCache != null && key == blKey) return blCache;
        var all = FxRegistry.All; var list = new List<FxRegistry.Info>(); string q = browserQuery.ToLowerInvariant().Trim();
        foreach (var i in all) { if (i.Type == "gain") continue; if (IsVstCat(i.Category) != browserVst) continue;   /* the VSTs are under VSTS, the effects under ADD EFFECT */ if (browserCat.Length > 0 && i.Category != browserCat) continue; if (q.Length == 0 || i.Name.ToLowerInvariant().Contains(q) || i.Category.ToLowerInvariant().Contains(q) || i.Blurb.ToLowerInvariant().Contains(q) || i.Type.Contains(q)) list.Add(i); }
        var order = new List<string>(); order.Add("Plugins"); foreach (var i in all) if (!order.Contains(i.Category)) order.Add(i.Category);
        var res = new List<FxRegistry.Info>(); foreach (var c in order) foreach (var i in list) if (i.Category == c) res.Add(i); var pv = res.FindAll(x => x.Category == "Purphex VST"); { res.RemoveAll(x => x.Category == "Purphex VST"); var inst = res.FindAll(x => x.Category == "Instruments"); if (inst.Count > 0) { res.RemoveAll(x => x.Category == "Instruments"); var bl = inst.Find(x => x.Type == "synth"); if (bl != null) { inst.Remove(bl); inst.Insert(0, bl); } res.InsertRange(0, inst); } }   // the instruments (Bass Lab first, then the VSTs) are together at the top, in one box that fits them exactly
        res.InsertRange(0, pv);
        { var ins2 = res.FindAll(x => x.Category == "Instruments"); res.RemoveAll(x => x.Category == "Instruments"); res.InsertRange(0, ins2); }   // the VSTs that make the sound on top, Bass Lab first of all (it makes all your sounds), then the Purphex VST effects
        blCache = res; blKey = key; return res;
    }
    void CategoryMenu()
    {
        var m = Win.NewMenu(); var it0 = new ToolStripMenuItem("All effects") { Checked = browserCat.Length == 0 }; it0.Click += (a, b) => { browserCat = ""; browserScroll = 0; }; m.Items.Add(it0); m.Items.Add(new ToolStripSeparator());
        var cats = new List<string>(); var counts = new Dictionary<string, int>(); foreach (var i in FxRegistry.All) { if (i.Type == "gain" || IsVstCat(i.Category)) continue; if (!counts.ContainsKey(i.Category)) { counts[i.Category] = 0; cats.Add(i.Category); } counts[i.Category]++; }
        foreach (var c in cats) { string cc = c; var it = new ToolStripMenuItem(c + "  (" + counts[c] + ")") { Checked = browserCat == c }; it.Click += (a, b) => { browserCat = cc; browserScroll = 0; }; m.Items.Add(it); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    void AddFx(string type)
    {
        var f = FxRegistry.Create(type); if (f == null) return; f.Prepare(StudioEngine.SR);
        if (type == "duck") { var v = eng.VocalTrack(); if (v == null) Toast("The ducker listens to a vocal track; add or separate a vocal first."); }
        if (browserMaster) { lock (eng.Sync) eng.MasterFx.Add(f); selMaster = eng.MasterFx.Count - 1; }
        else if (browserVst && (browserTrack < 0 || browserTrack >= eng.Tracks.Count)) { Toast("Choose the track first: FOR at the top of the VSTS box (or a new track)."); return; }
        else if (browserTrack >= 0 && browserTrack < eng.Tracks.Count) { var tr = eng.Tracks[browserTrack]; var vfx = f as InstrumentFx; if (vfx != null) vfx.Owner = tr; lock (eng.Sync) tr.Fx.Add(f); selTrack = browserTrack; selFx = tr.Fx.Count - 1; ShowPanel(); }
        browserOpen = false; Toast(f.Title + " added"); { var addedInst = f as InstrumentFx; if (addedInst != null && !browserMaster) addedInst.ShowWindow(this); }
        { if (PxPlugins.HasWindow(f)) PxPlugins.Open(f, this); else if (!browserMaster && insAddFor != null && browserTrack >= 0 && browserTrack < eng.Tracks.Count && eng.Tracks[browserTrack] == insAddFor && !(f is InstrumentFx)) OpenFxWindow(insAddFor, f); } insAddFor = null;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e); lastInput = DateTime.Now;
        if (psOpen) { PsKey(e.KeyChar); e.Handled = true; return; }
        if (MsgKey(e.KeyChar)) { e.Handled = true; return; }
        if (ProjSearchKey(e.KeyChar)) { e.Handled = true; return; }
        if (ArtSearchKey(e.KeyChar)) { e.Handled = true; return; }
        if (FontSearchKey(e.KeyChar)) { e.Handled = true; return; }
        if (cpOpen) { CpKey(e.KeyChar); e.Handled = true; return; }
        if (AiKey(e.KeyChar)) { e.Handled = true; return; }
        if (stylesOpen) { if (e.KeyChar == '\b') { if (stylesQuery.Length > 0) { stylesQuery = stylesQuery.Substring(0, stylesQuery.Length - 1); stylesScroll = 0; } } else if (!char.IsControl(e.KeyChar)) { stylesQuery += e.KeyChar; stylesScroll = 0; } e.Handled = true; return; }
        if (browserOpen) { if (e.KeyChar == '\b') { if (browserQuery.Length > 0) browserQuery = browserQuery.Substring(0, browserQuery.Length - 1); } else if (!char.IsControl(e.KeyChar)) browserQuery += e.KeyChar; e.Handled = true; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        lastInput = DateTime.Now;
        if (editBox != null && editBox.Visible)
        {   // Esc always closes a value box (even if it lost the keyboard), Enter confirms it
            if (k == Keys.Escape) { CommitEdit(false); return true; }
            if (k == Keys.Enter || k == Keys.Return) { CommitEdit(true); return true; }
            return base.ProcessCmdKey(ref msg, k);
        }
        if (ActiveControl is TextBoxBase && ((Control)ActiveControl).Visible) return base.ProcessCmdKey(ref msg, k);   // typing in the lyrics page: every key belongs to the text box
        if (loopPick && k == Keys.Escape) { loopPick = false; loopPicking = false; Toast("Loop section cancelled."); return true; }
        if (keyOn && (k == Keys.Escape || k == Keys.Enter)) { keyOn = false; return true; }
        if (busOpen && k == Keys.Escape) { busOpen = false; return true; }
        if (ntOpen && k == Keys.Escape) { CloseNewTrackSheet(); return true; }
        if (menuOpen && k == Keys.Escape) { CloseMenu(); return true; }
        if ((menuOpen || busOpen || ntOpen || keyOn) && !flexOpen) return base.ProcessCmdKey(ref msg, k);   // while a menu or sheet is open the keys do not play, split or delete things behind it
        if (flexOpen && !menuOpen && !browserOpen && FlexKey(k)) return true;
        if (!menuOpen && !browserOpen && PrKey(k)) return true;   // the piano roll's keys (when you last clicked in it)
        if (autoOpen) { if (k == Keys.Escape) { CloseAuto(); return true; } return base.ProcessCmdKey(ref msg, k); }
        if (cpOpen) { if (k == Keys.Escape) { cpOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (themeOpen) { if (k == Keys.Escape) { themeOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (caOpen) { if (k == Keys.Escape) { caOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (psOpen) { if (k == Keys.Escape) { psOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (amOpen) { if (k == Keys.Escape) { AmCancel(); return true; } if (k == Keys.Space) { if (mpActive) MasterPreviewStop(); else MasterPreviewStart(); return true; } return base.ProcessCmdKey(ref msg, k); }
        if (pickOpen) { if (k == Keys.Escape) { pickOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (gainOpen) { if (k == Keys.Escape) { gainOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (k == Keys.Escape && cutMark >= 0) { CutCancel(); return true; }
        if (k == Keys.Escape && multiSel.Count > 0) { multiSel.Clear(); return true; }
        if (aiOpen) { if (k == Keys.Escape) { aiOpen = false; return true; } if (k == (Keys.Control | Keys.V)) { try { if (Clipboard.ContainsText()) { aiPrompt = aiPrompt + Clipboard.GetText().Replace("\r", " ").Replace("\n", " "); if (aiPrompt.Length > 200) aiPrompt = aiPrompt.Substring(0, 200); } } catch { } return true; } return base.ProcessCmdKey(ref msg, k); }
        if (stylesOpen) { if (k == Keys.Escape) { stylesOpen = false; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (allFxOpen && !browserOpen && k == Keys.Escape) { allFxOpen = false; return true; }
        if (epOpen && k == Keys.Escape) { epOpen = false; return true; }
        if (projEpOpen && k == Keys.Escape) { projEpOpen = false; return true; }
        if (browserOpen)
        {
            if (k == Keys.Escape) { browserOpen = false; return true; }
            if (k == Keys.Enter)
            {
                var first = BrowserList(); if (first.Count > 0) AddFx(first[0].Type);
                return true;
            }
            return base.ProcessCmdKey(ref msg, k);
        }
        if (busy) { if (k == Keys.Escape && busyCancelable) { cancelFlag = true; busyText = "Cancelling..."; return true; } return base.ProcessCmdKey(ref msg, k); }
        if (k == Keys.Escape && tab == 5 && !menuOpen) { tab = 1; return true; }   // Esc leaves the visualiser
        if (k == Keys.Escape && vidFull) { ExitVideoFull(); return true; }
        if (k == Keys.Space && tab == 9 && hubBObj != null && (hubBox == null || !hubBox.Focused)) { HubToggle(); return true; }   // on the Music Hub page Space plays and pauses the hub
        if (k == Keys.Space && (tab == 3 || tab == 4 || tab == 6 || tab == 7 || tab == 8) && !ProjSearchActive && !ArtSearchActive && previewPath.Length > 0 && previewEng != null) { PreviewPauseToggle(); return true; }   // Space pauses the preview (the search box only takes keys after you click it)
        if (ProjSearchCmd(k)) return true; if (ProjSearchTakesKey(k)) return base.ProcessCmdKey(ref msg, k);
        if (MsgCmd(k)) return true; if (MsgTakesKey(k)) return base.ProcessCmdKey(ref msg, k);
        if (ArtSearchCmd(k)) return true; if (ArtSearchTakesKey(k)) return base.ProcessCmdKey(ref msg, k); if (FontSearchCmd(k)) return true; if (FontSearchTakesKey(k)) return base.ProcessCmdKey(ref msg, k);
        if (k == Keys.F1) { app.ShowHelp(null); return true; }
        if (k == Keys.Space && tab == 0 && soundsOpen && keysInSounds) { Auditioner.Toggle(); Invalidate(); return true; }   // in the sound packs: Space pauses and plays the sound you are hearing
        if (k == Keys.Space) { if (tab != 0) return base.ProcessCmdKey(ref msg, k); PlayToggle(); return true; }   // the space bar plays and pauses on the Mix page only: on any other page it leaves the mix alone
        if (k == Keys.Home) { eng.Seek(0); scroll = 0; followPaused = false; return true; }
        if (k == (Keys.Control | Keys.Shift | Keys.S)) { SaveClick(true); return true; }
        if (k == Keys.F2 && tab == 0 && selTrack < eng.Tracks.Count) { RenameTrack(eng.Tracks[selTrack]); return true; }
        if (k == (Keys.Control | Keys.Z)) { DoUndo(); return true; }
        if (tab == 0 && k == (Keys.Control | Keys.C)) { CopyKey(); return true; }   // copy the picked piece, or every highlighted section
        if (tab == 0 && k == (Keys.Control | Keys.V)) { PasteKey(); return true; }
        if (tab == 0 && k == (Keys.Control | Keys.X)) { CutKey(); return true; }   // cut the highlighted audio   // paste at the needle on the picked track
        if (k == (Keys.Control | Keys.Y) || k == (Keys.Control | Keys.Shift | Keys.Z)) { DoRedo(); return true; }
        if (k == Keys.B && tab == 0) { SplitSelected(); return true; }
        if (k == Keys.Delete && tab == 0 && multiSel.Count > 0) { DeleteSelectedClips(); return true; }
        if (k == Keys.Delete && tab == 0 && Selected != null && Selected.Clip != null) { DeleteClip(CurClip() ?? Selected); return true; }
        if (k == (Keys.Control | Keys.S)) { SaveClick(false); return true; }
        if (k == (Keys.Control | Keys.O)) { OpenClick(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---------------- mouse ----------------
    // the area under the pointer that takes a click: an area that only scrolls (a list's wheel area, added after its rows) never takes the click from the buttons in it (it ate ACCEPT / DECLINE and the conversations in Messages)
    static bool WheelOnly(HitReg r) { return r.OnWheel != null && r.OnDown == null && r.OnRight == null && r.OnDouble == null && r.OnDrag == null && r.OnUp == null; }
    // a button inside the card or row under the pointer takes the click from it (cards and rows were often added after their own buttons, so a click on PLAY opened the project), but never a button from under a pop-up
    static bool OnTop(HitReg r) { return r.Button || r.ScrollBar || r.Overlay; }
    HitReg TopRegion(PointF p)
    {
        int ti = -1; for (int i = regs.Count - 1; i >= 0; i--) if (regs[i].R.Contains(p) && !WheelOnly(regs[i])) { ti = i; break; }
        if (ti < 0) return null; var top = regs[ti]; if (OnTop(top)) return top;
        int start = 0; for (int i = ti; i >= 0; i--) if (regs[i].R.Width >= ClientSize.Width - 2 && regs[i].R.Height >= ClientSize.Height - 2) { start = i; break; }   // where the pop-up it is in begins (nothing before that is reachable)
        for (int i = ti - 1; i >= start; i--) { var b = regs[i]; if (WheelOnly(b) || !b.R.Contains(p)) continue; if (b.Overlay) return top; if (b.Button || b.ScrollBar) return b; }   // reaching the bar the top one is drawn on: the top one is part of that bar
        return top;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); MousePos = e.Location; lastInput = DateTime.Now;
        if (panning) { MiddleMove(e.Location); return; }
        { var cur = CursorAt(MousePos); if (Cursor != cur) Cursor = cur; }
        if (dragReg != null && dragReg.OnDrag != null) dragReg.OnDrag(e.Location);
        else hover = TopRegion(e.Location);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); lastInput = DateTime.Now; if (ActiveControl is TextBoxBase && ActiveControl != editBox) ActiveControl = null; Focus(); MousePos = e.Location;   // clicking the studio takes the keys back from a search box (so Space plays again)
        keysInSounds = soundsOpen && tab == 0 && e.X >= ClientSize.Width - PanelW && e.Y >= ToolH + TransH;   // the last click was in the sound packs: Space plays and pauses the sound there
        if (e.Button == MouseButtons.Middle && tab == 0 && TimelineRect().Contains(e.Location)) { MiddleDown(e.Location); return; }
        if (editBox != null && editBox.Visible) CommitEdit(true);   // clicking off a value box confirms what you typed
        if (tab == 3 && projSearchOn && !projSearchR.Contains(e.Location)) projSearchOn = false;
        if ((tab == 8 || fontsWin) && fontSearchOn && !fontSearchR.Contains(e.Location)) fontSearchOn = false;
        if (tab == 7 && artSearchOn && !artSearchR.Contains(e.Location)) artSearchOn = false;   // clicking anywhere else lets go of the search box
        FxWinMouseDown(e.Location);   // a click in a plugin window brings that window to the front
        prFocus = PrShown && (prGrid.Contains(e.Location) || prVel.Contains(e.Location) || prKeys.Contains(e.Location) || prRuler.Contains(e.Location));
        var r = TopRegion(e.Location); if (r == null) return;
        if (Win.ClickClosedMenu(this, r.R)) return;   // the click that just closed a menu is not also a click on the button that opened it
        if (busy && !r.WhileBusy) return;   // while a job runs only the Cancel button works
        if (e.Button == MouseButtons.Right) { if (r.OnRight != null) r.OnRight(); return; }
        if (e.Clicks == 2 && r.OnDouble != null) { r.OnDouble(); return; }
        if (r.OnDown != null) r.OnDown();
        if (r.OnDrag != null) dragReg = r;
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); lastInput = DateTime.Now; if (dragReg != null && dragReg.OnUp != null) { try { dragReg.OnUp(); } catch { } } dragReg = null; zoomAnchor = 0; if (sndDrag != null) { if (sndDragging) DropSampleAt(e.Location); sndDrag = null; sndDragging = false; } if (panning) { panning = false; Cursor = Cursors.Default; } }
    // true while any pop-up sheet, menu or panel covers the studio: nothing behind it can be clicked, scrolled or typed into
    bool OverlayOpen { get { return menuOpen || hubConnOpen || browserOpen || busy || ntOpen || busOpen || stylesOpen || aiOpen || themeOpen || caOpen || psOpen || amOpen || autoOpen || gainOpen || pickOpen || cpOpen || keyOn || packsOpen || allFxOpen || epOpen || projEpOpen; } }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e); MousePos = e.Location; lastInput = DateTime.Now;
        for (int i = regs.Count - 1; i >= 0; i--)
        {
            var rg = regs[i]; if (!rg.R.Contains(e.Location)) continue;
            if (rg.OnWheel != null) { rg.OnWheel(e.Delta > 0 ? 1 : -1); return; }
            if (rg.OnDown != null && rg.R.Width >= ClientSize.Width - 2 && rg.R.Height >= ClientSize.Height - 2) return;   // a pop-up sheet covers the window: the wheel does not reach what is behind it
        }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; }

    // ---------------- editors ----------------
    static string ParamName(Fx f, Param p) { return p.Name; }
    static bool LogParam(Param p) { return p.Unit == "Hz" && p.Min > 0 && p.Max / p.Min >= 8; }
    static float PFrac(Param p) { if (LogParam(p)) return (float)(Math.Log(p.Val / p.Min) / Math.Log(p.Max / p.Min)); return (p.Val - p.Min) / (p.Max - p.Min); }
    static float PFromFrac(Param p, float f) { if (LogParam(p)) return (float)(p.Min * Math.Pow(p.Max / p.Min, f)); return p.Min + f * (p.Max - p.Min); }

    // a see-through colour worked out against the panel it sits on, as one solid colour (blending a big see-through box over a panel on every frame is slow)
    static Color OverPanel(Color c) { float k = c.A / 255f; return Color.FromArgb(255, (int)(Panel.R + (c.R - Panel.R) * k), (int)(Panel.G + (c.G - Panel.G) * k), (int)(Panel.B + (c.B - Panel.B) * k)); }
    bool editorBare;   // the editor without its own title row (inside a floating plugin window)
    void PaintEditor(Graphics g, Fx f, RectangleF a, Track tr)
    {
        if (!editorBare) FillRR(g, OverPanel(TM.C(120, 34, 18, 62)), a, 10);   // (in a floating window the galaxy shows through)
        float x = a.X + 10, y = a.Y + 8, w = a.Width - 20;
        bool wide = tr == null;   // the Mastering page has room for the longer label; the narrow track panel keeps the short one
        bool isEq = f is ParametricEq; float aeW = wide ? 84 : 74, aeGap = wide ? 8 : 6; bool eqOwnRow = isEq && !wide;
        if (!editorBare)   // (in a floating window the window's own title bar has the name, bypass and presets)
        {
            Txt(g, f.Title, 16, Ink, Rc(x, y, w - (wide ? 190 : 150), 24), StringAlignment.Near, true);
            Btn(g, Rc(a.Right - (wide ? 176 : 150), y, wide ? 70 : 66, 24), f.Bypass ? "OFF" : "ON", f.Bypass ? 0 : 4, () => f.Bypass = !f.Bypass, 11);
            Btn(g, Rc(a.Right - (wide ? 100 : 80), y, wide ? 90 : 70, 24), wide ? "FX presets" : "Presets", 1, () => OpenPresetSheet(f), 12);
            y += 34;
        }
        if (!(f is InstrumentFx)) { bool fitsInline = wide && a.Width - 10 - 184 - 150 - 6 >= Math.Min(260f, g.MeasureString(f.Title, GfxCache.Font(16, true)).Width + 14); if (fitsInline) PaintPresetInline(g, f, tr, a, y - 34); else y = PaintPresetRow(g, f, tr, a, y); }   /* a long name (Multiband Compressor) on a narrow window: the buttons go on their own row instead of over the name */   // which preset is on, COPY / PASTE of the plugin, and REMOVE PRESET (on the wide Mastering page they sit in the title row: no room is taken from the editor)
        if (eqOwnRow) { var ae2 = Rc(a.X + 10, y - 4, a.Width - 20, 26); Btn(g, ae2, "AUTO EQ", 2, () => AutoEqClick((ParametricEq)f, tr), 12); Hint2(ae2, "The EQ sets itself: it listens to this track and moves bass, mud, presence, upper-mids and air to balance it (it asks first, and you can undo it)"); y += 28; }
        var vstf = f as InstrumentFx; if (vstf != null) { Btn(g, Rc(a.X + 10, y - 2, a.Width - 20, 32), vstf.WindowLabel, 2, () => { vstf.Owner = tr; vstf.ShowWindow(this); }, 14); y += 38; return; }   // an instrument is edited in its own window, like a real plugin: nothing else is listed here
        { if (PxPlugins.HasWindow(f)) { var pf = f; var ob = Rc(a.X + 10, y - 2, a.Width - 20, 34); Btn(g, ob, "OPEN " + f.Title.ToUpperInvariant(), 2, () => PxPlugins.Open(pf, this), 13); Hint2(ob, "Open the " + f.Title + " window (every setting is also below, with its A button for automation)"); y += 42; } }
        var skip = new List<string>(); if (f.Hidden.Length > 0) skip.AddRange(f.Hidden.Split(','));
        if (f is ParametricEq) { PaintEqEditor(g, (ParametricEq)f, Rc(a.X + 6, y, a.Width - 12, a.Bottom - y - 4), tr == null ? null : tr.TapPost, tr == null ? 0 : tr.TapPos); return; }
        var cvx = f as ConvFx; if (cvx != null) { var lb = Rc(a.X + 10, y - 2, a.Width - 20, 30); Btn(g, lb, "Load your own impulse response...", 2, () => { using (var d = new OpenFileDialog { Title = "Choose an impulse response (a recording of a space or speaker)", Filter = "Audio|*.wav;*.mp3;*.flac;*.ogg;*.aiff|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) cvx.UserPath = d.FileName; }, 12); y += 34; Txt(g, cvx.IrNote.Length > 0 ? "Using: " + cvx.IrNote : "", 11, DimC, Rc(a.X + 14, y - 4, a.Width - 28, 16), StringAlignment.Near, false); y += 18; }
        if (f is Spatial8D) { y = Paint8dEditor(g, (Spatial8D)f, Rc(a.X + 6, y, a.Width - 12, 190)); skip.AddRange(new[] { "hold", "angle" }); }
        if (f is PitchCorrector) { y = PaintPitchEditor(g, (PitchCorrector)f, tr, Rc(a.X + 6, y, a.Width - 12, 200)); skip.AddRange(new[] { "key", "scale", "mask" }); }
        else if (HasKeyParam(f)) { var ad = Rc(a.X + 10, y - 2, a.Width - 20, 28); var tk = tr; Btn(g, ad, "AUTO DETECT KEY", 2, () => AutoDetectKey(f, tk), 12); Hint2(ad, "Listen to this track and set the key and scale of " + f.Title + " by themselves"); y += 34; }
        paramTrack = tr; PaintParams(g, f, Rc(a.X + 6, y, a.Width - 12, a.Bottom - y - 4), skip);
    }

    void PaintParams(Graphics g, Fx f, RectangleF a, List<string> skip)
    {
        if (editorBare) { PaintParamKnobs(g, f, a, skip); return; }   // in a floating plugin window the settings are knobs
        var ps = new List<Param>(); foreach (var p in f.P) if (!skip.Contains(p.Id)) ps.Add(p);
        float rowH = 30, total = ps.Count * rowH; float maxScroll = Math.Max(0, total - a.Height); var aFull = a; RectangleF? barR = null;
        if (maxScroll > 0 && a.Height >= 160) { barR = Rc(a.Right - 46, a.Y, 42, a.Height); a = Rc(a.X, a.Y, a.Width - 52, a.Height); }
        genScroll = Math.Max(0, Math.Min(maxScroll, genScroll));
        g.SetClip(a); float y = a.Y - genScroll;
        foreach (var p in ps)
        {
            var row = Rc(a.X, y, a.Width, rowH); y += rowH; if (row.Bottom < a.Y || row.Y > a.Bottom) continue;
            Txt(g, Win.Up(p.Name), 12, DimC, Rc(row.X + 6, row.Y, row.Width * 0.38f, rowH), StringAlignment.Near, false);
            var pp = p; float aw = paramTrack != null ? 26 : 0;   // the A (automate) button sits at the right end of the row so the names keep their room
            if (paramTrack != null) { var lane0 = FindLane(paramTrack, f, p.Id); var atr = paramTrack; Btn(g, Rc(row.Right - 22, row.Y + 4, 22, rowH - 8), "A", lane0 != null && lane0.On ? 4 : 0, () => OpenAuto(atr, f, pp), 10); }
            if (p.IsChoice)
            {
                var br = Rc(row.X + row.Width * 0.40f, row.Y + 3, row.Width * 0.60f - 6 - aw, rowH - 6);
                Btn(g, br, p.Text, 0, () => { pp.Val = (pp.Val + 1) > pp.Max ? pp.Min : pp.Val + 1; f.MarkDirty(); }, 12);
                var rg = regs[regs.Count - 1]; rg.OnRight = () => { pp.Val = (pp.Val - 1) < pp.Min ? pp.Max : pp.Val - 1; f.MarkDirty(); };
            }
            else
            {
                var sr = Rc(row.X + row.Width * 0.40f, row.Y + 4, row.Width * 0.60f - 88 - aw, rowH - 8);
                var rg = Slider(g, sr, PFrac(p), fr => { f.Set(pp.Id, PFromFrac(pp, fr)); }); rg.OnDouble = () => f.Set(pp.Id, pp.Def);
                ValueBox(g, Rc(sr.Right + 14, row.Y + 3, 72, rowH - 6), p.Text, Num(p.Val), s => { float v; if (ParseNum(s, out v)) f.Set(pp.Id, v); });
            }
        }
        g.ResetClip(); if (barR.HasValue) VScroll(g, barR.Value, total, barR.Value.Height, () => genScroll, v => genScroll = v);
        if (maxScroll > 0) { var wr = new HitReg { R = aFull, OnWheel = d => genScroll = Math.Max(0, Math.Min(maxScroll, genScroll - d * 30)) }; regs.Insert(0, wr); }
    }

    // ---------------- spectrum ----------------
    static double[] hann; int eqPaintTick = -100000;
    void UpdateSpectrum()
    {
        if (!eng.Playing) { for (int k = 0; k <= 2048; k++) { spec[k] = spec[k] * 0.9 - 12; specPre[k] = specPre[k] * 0.9 - 12; } return; }   // nothing to analyse: just let the picture fall away
        float[] tap = null; int pos = 0;
        if (tab == 0 && eng.Tracks.Count > 0 && selTrack < eng.Tracks.Count) { var tr = eng.Tracks[selTrack]; tap = tr.TapPost; pos = tr.TapPos; }
        else if (tab == 1) { tap = eng.MasterTap; pos = eng.MasterTapPos; }
        if (tap == null) return;
        int N = 4096; double avg = 0;
        if (hann == null) { hann = new double[N]; for (int i = 0; i < N; i++) hann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N); }
        for (int i = 0; i < N; i++) { specRe[i] = tap[(pos - N + i + 8192 * 2) & 8191] * hann[i]; specIm[i] = 0; }
        Fft.Transform(specRe, specIm, false);
        for (int k = 0; k <= N / 2; k++)
        {
            double mag = Math.Sqrt(specRe[k] * specRe[k] + specIm[k] * specIm[k]) / (N / 4.0), db = 20 * Math.Log10(mag + 1e-9);

            spec[k] = db > spec[k] ? spec[k] * 0.4 + db * 0.6 : spec[k] * 0.9 + db * 0.1; avg += 0;
        }
        if (tab == 1 && eng.MasterTapPre != null)
        {   // the same picture of the sound going INTO the mastering chain, to compare
            var pre = eng.MasterTapPre; for (int i = 0; i < N; i++) { specRe[i] = pre[(pos - N + i + 8192 * 2) & 8191] * hann[i]; specIm[i] = 0; }
            Fft.Transform(specRe, specIm, false);
            for (int k = 0; k <= N / 2; k++) { double mag = Math.Sqrt(specRe[k] * specRe[k] + specIm[k] * specIm[k]) / (N / 4.0), db = 20 * Math.Log10(mag + 1e-9); specPre[k] = db > specPre[k] ? specPre[k] * 0.4 + db * 0.6 : specPre[k] * 0.9 + db * 0.1; }
        }
    }

    // ---------------- EQ editor ----------------
    static readonly Color[] BandCol = { Color.FromArgb(255, 120, 120), Color.FromArgb(255, 180, 90), Color.FromArgb(240, 230, 100), Color.FromArgb(120, 230, 150), TM.C(255, 100, 200, 255), TM.C(255, 200, 130, 255) };

    void PaintEqEditor(Graphics g, ParametricEq eq, RectangleF a, float[] tap, int tapPos)
    {
        eqPaintTick = Environment.TickCount;
        var plot = Rc(a.X + 28, a.Y + 4, a.Width - 36, Math.Min(230, a.Height * 0.52f));
        FillEqBackdrop(g, plot);
        Func<double, float> FX = f => plot.X + plot.Width * (float)(Math.Log10(f / 20.0) / 3.0);
        Func<double, float> GY = db => plot.Y + plot.Height * (float)((18 - db) / 36.0);
        // grid
        using (var pen = new Pen(TM.C(40, 190, 140, 255)))
        {
            foreach (double f in new double[] { 50, 100, 200, 500, 1000, 2000, 5000, 10000 }) g.DrawLine(pen, FX(f), plot.Y, FX(f), plot.Bottom);
            foreach (int d in new[] { -12, -6, 0, 6, 12 }) g.DrawLine(pen, plot.X, GY(d), plot.Right, GY(d));
        }
        foreach (var kv in new[] { new KeyValuePair<double, string>(100, "100"), new KeyValuePair<double, string>(1000, "1k"), new KeyValuePair<double, string>(10000, "10k") }) Txt(g, kv.Value, 10, DimC, Rc(FX(kv.Key) - 14, plot.Bottom - 14, 30, 12), StringAlignment.Center, false);
        foreach (int d in new[] { -12, 0, 12 }) Txt(g, d.ToString(), 10, DimC, Rc(a.X, GY(d) - 7, 26, 14), StringAlignment.Far, false);
        // live spectrum: your sound after the EQ, and (in Mastering) before it, with what changed lit up
        var eqClip = g.Save(); g.SetClip(plot, System.Drawing.Drawing2D.CombineMode.Intersect); DrawEqSpectrum(g, plot, tab == 1);
        // curve
        // same maths as ParametricEq.ResponseDb, but each filter is designed once per frame instead of once per pixel
        var secC = new List<double[]>(); var secN = new List<int>();
        if (eq.Get("hp_on") > 0.5f) { secC.Add(Biquad.Coeffs(Biquad.HighPass, eq.SR, eq.Get("hp_f"), 0.7071, 0)); secN.Add(eq.Get("hp_s") > 0.5f ? 2 : 1); }
        if (eq.Get("lp_on") > 0.5f) { secC.Add(Biquad.Coeffs(Biquad.LowPass, eq.SR, eq.Get("lp_f"), 0.7071, 0)); secN.Add(eq.Get("lp_s") > 0.5f ? 2 : 1); }
        for (int b = 0; b < ParametricEq.Bands; b++)
        {
            if (eq.Get("b" + b + "_on") < 0.5f) continue; int bt = (int)eq.Get("b" + b + "_t");
            secC.Add(Biquad.Coeffs(bt == 1 ? Biquad.LowShelf : (bt == 2 ? Biquad.HighShelf : (bt == 3 ? Biquad.Notch : Biquad.Bell)), eq.SR, eq.Get("b" + b + "_f"), eq.Get("b" + b + "_q"), eq.Get("b" + b + "_g"))); secN.Add(1);
        }
        var cp = new List<PointF>(); for (int px = 0; px <= (int)plot.Width; px += 2)
        {
            double f = 20 * Math.Pow(1000, px / plot.Width), w0 = DspMath.TwoPi * f / eq.SR, c1 = Math.Cos(w0), s1 = Math.Sin(w0), c2 = Math.Cos(2 * w0), s2 = Math.Sin(2 * w0), prod = 1;
            for (int k = 0; k < secC.Count; k++)
            {
                var c = secC[k]; double nr = c[0] + c[1] * c1 + c[2] * c2, ni = -(c[1] * s1 + c[2] * s2), dr = 1 + c[3] * c1 + c[4] * c2, di = -(c[3] * s1 + c[4] * s2);
                double rr = (nr * nr + ni * ni) / (dr * dr + di * di); if (secN[k] == 2) rr *= rr; prod *= rr;
            }
            cp.Add(new PointF(plot.X + px, GY(DspMath.Clamp(10 * Math.Log10(prod + 1e-30), -18, 18))));
        }
        using (var pen = new Pen(Color.FromArgb(70, Accent2), 8)) g.DrawLines(pen, cp.ToArray());
        using (var pen = new Pen(Accent2, 2.4f)) g.DrawLines(pen, cp.ToArray());
        g.Restore(eqClip);   // (back to the clip it had: a plugin window clips the editor to itself)
        // draggable nodes
        for (int b = 0; b < ParametricEq.Bands; b++)
        {
            if (eq.Get("b" + b + "_on") < 0.5f) continue; int bi = b;
            int type = (int)eq.Get("b" + b + "_t"); float fq = eq.Get("b" + b + "_f"), gn = type == 3 ? 0 : eq.Get("b" + b + "_g");
            float cx = FX(fq), cy = GY(gn); bool sel = b == eqBand;
            using (var bb = new SolidBrush(Color.FromArgb(sel ? 90 : 40, BandCol[b]))) g.FillEllipse(bb, cx - 16, cy - 16, 32, 32);
            using (var bb = new SolidBrush(BandCol[b])) g.FillEllipse(bb, cx - 8, cy - 8, 16, 16);
            using (var pen = new Pen(Color.White, sel ? 2f : 1f)) g.DrawEllipse(pen, cx - 8, cy - 8, 16, 16);
            Txt(g, (b + 1).ToString(), 10, Color.Black, Rc(cx - 8, cy - 8, 16, 16), StringAlignment.Center, true);
            var nr = Rc(cx - 14, cy - 14, 28, 28); var rg = Reg(nr, () => eqBand = bi);
            rg.OnDrag = pt =>
            {
                double nf = 20 * Math.Pow(1000, DspMath.Clamp((pt.X - plot.X) / plot.Width, 0, 1)); eq.Set("b" + bi + "_f", (float)nf);
                if ((int)eq.Get("b" + bi + "_t") != 3) eq.Set("b" + bi + "_g", (float)Math.Round(DspMath.Clamp(18 - (pt.Y - plot.Y) / plot.Height * 36, -18, 18), 1));
            };
            rg.OnRight = () => eq.Set("b" + bi + "_on", 0); rg.OnDouble = () => eq.Set("b" + bi + "_g", 0);
        }
        Txt(g, "Drag a dot.  Right-click: remove.", 10, DimC, Rc(plot.X, plot.Bottom + 1, plot.Width, 14), StringAlignment.Near, false);
        // band chips
        float cy2 = plot.Bottom + 20; float cw = (a.Width - 8) / 6f;
        for (int b = 0; b < ParametricEq.Bands; b++)
        {
            int bi = b; bool on = eq.Get("b" + b + "_on") > 0.5f; var chip = Rc(a.X + 4 + b * cw, cy2, cw - 4, 26);
            FillRR(g, on ? Color.FromArgb(200, BandCol[b].R / 2, BandCol[b].G / 2, BandCol[b].B / 2) : TM.C(120, 40, 24, 70), chip, 6); if (b == eqBand) StrokeRR(g, BandCol[b], chip, 6, 1.6f);
            Txt(g, "BAND " + (b + 1), 12, on ? Color.White : DimC, chip, StringAlignment.Center, b == eqBand);
            Reg(chip, () => { if (eqBand == bi) eq.Set("b" + bi + "_on", on ? 0 : 1); else eqBand = bi; if (!on) eq.Set("b" + bi + "_on", 1); });
        }
        // selected band controls
        float y = cy2 + 34; int s = eqBand; string pfx = "b" + s + "_";
        var tb = Rc(a.X + 4, y, 118, 26); Btn(g, tb, eq.Find(pfx + "t").Text, 0, () => eq.Set(pfx + "t", (eq.Get(pfx + "t") + 1) % 4), 12); y += 32;
        foreach (var id in new[] { "f", "g", "q" })
        {
            var p = eq.Find(pfx + id); var pp = p; Txt(g, id == "f" ? "Freq" : (id == "g" ? "Gain" : "Q"), 12, DimC, Rc(a.X + 6, y, 40, 24), StringAlignment.Near, false);
            var sr = Rc(a.X + 48, y + 2, a.Width - 170, 20); var rg = Slider(g, sr, PFrac(p), fr => eq.Set(pp.Id, PFromFrac(pp, fr)), BandCol[s]); rg.OnDouble = () => eq.Set(pp.Id, pp.Def);
            ValueBox(g, Rc(sr.Right + 14, y, 96, 24), p.Text, Num(p.Val), str => { float v; if (ParseNum(str, out v)) eq.Set(pp.Id, v); }); y += 28;
        }
        // HP / LP
        foreach (var kind in new[] { "hp", "lp" })
        {
            string k = kind; bool on = eq.Get(k + "_on") > 0.5f;
            Btn(g, Rc(a.X + 4, y, 70, 24), kind == "hp" ? "High-pass" : "Low-pass", on ? 4 : 0, () => eq.Set(k + "_on", on ? 0 : 1), 12);
            var p = eq.Find(k + "_f"); var sr = Rc(a.X + 80, y + 2, a.Width - 218, 20); var rg = Slider(g, sr, PFrac(p), fr => eq.Set(k + "_f", PFromFrac(p, fr)), Accent); rg.OnDouble = () => eq.Set(k + "_f", p.Def);
            ValueBox(g, Rc(sr.Right + 14, y, 70, 24), p.Text, Num(p.Val), str => { float v; if (ParseNum(str, out v)) eq.Set(k + "_f", v); });
            Btn(g, Rc(a.Right - 52, y, 48, 24), eq.Find(k + "_s").Text, 0, () => eq.Set(k + "_s", eq.Get(k + "_s") > 0.5f ? 0 : 1), 11); y += 28;
        }
    }

    // ---------------- pitch correction editor ----------------
    float PaintPitchEditor(Graphics g, PitchCorrector pc, Track tr, RectangleF a)
    {
        float x = a.X + 4, y = a.Y, w = a.Width - 8;
        int key = (int)pc.Get("key"), scale = (int)pc.Get("scale"); int mask = (int)pc.Get("mask") & 4095;
        Btn(g, Rc(x, y, 64, 28), "Key " + PitchCorrector.Keys[key], 0, () => { var m = Win.NewMenu(); for (int k = 0; k < 12; k++) { int kk = k; m.Items.Add(PitchCorrector.Keys[k]).Click += (s, e) => { pc.AutoKeyPending = false; pc.Set("key", kk); pc.ApplyScale(); }; } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 12);
        Btn(g, Rc(x + 70, y, w - 70 - 110, 28), PitchCorrector.Scales[scale], 0, () => { var m = Win.NewMenu(); for (int k = 0; k < PitchCorrector.Scales.Length; k++) { int kk = k; m.Items.Add(PitchCorrector.Scales[k]).Click += (s, e) => { pc.AutoKeyPending = false; pc.Set("scale", kk); pc.ApplyScale(); }; } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 12);
        { var ad = Rc(x + w - 104, y, 104, 28); Btn(g, ad, "AUTO DETECT", 2, () => AutoDetectKey(pc, tr), 11); Hint2(ad, "Listen to this track and set the key and scale by themselves"); }
        y += 34; float cw = w / 12f;
        for (int n = 0; n < 12; n++)
        {
            int nn = n; bool on = (mask & (1 << n)) != 0; bool black = n == 1 || n == 3 || n == 6 || n == 8 || n == 10; var c = Rc(x + n * cw, y, cw - 3, 30);
            FillRR(g, on ? (black ? TM.C(220, 150, 70, 230) : TM.C(230, 190, 120, 255)) : TM.C(120, 40, 24, 70), c, 5);
            Txt(g, PitchCorrector.Keys[n], 11, on ? Color.Black : DimC, c, StringAlignment.Center, true);
            Reg(c, () => { pc.Set("mask", mask ^ (1 << nn)); });
        }
        y += 36;
        Btn(g, Rc(x, y, w / 2 - 3, 26), "Natural", 0, () => { pc.Set("speed", 80); pc.Set("strength", 70); pc.Set("natural", 70); }, 12);
        Btn(g, Rc(x + w / 2 + 3, y, w / 2 - 3, 26), "Hard Auto-Tune", 2, () => { pc.Set("speed", 0); pc.Set("strength", 100); pc.Set("natural", 0); }, 12);
        y += 32;
        // live readout
        var lr = Rc(x, y, w, 46); FillRR(g, TM.C(150, 8, 4, 20), lr, 8);
        string det = pc.Voiced ? PitchName(pc.DetHz) + "  " + pc.DetHz.ToString("0.0") + " Hz" : "(no pitch)";
        Txt(g, "Detected: " + det, 12, Ink, Rc(lr.X + 8, lr.Y + 2, lr.Width / 2, 20), StringAlignment.Near, false);
        Txt(g, pc.Voiced ? "Target: " + PitchName(pc.TargetHz) : "", 12, Accent, Rc(lr.X + lr.Width / 2, lr.Y + 2, lr.Width / 2 - 8, 20), StringAlignment.Far, false);
        float mid = lr.X + lr.Width / 2, sh = DspMath.Clamp(pc.ShiftCents, -100, 100) / 100f;
        using (var pen = new Pen(DimC)) g.DrawLine(pen, mid, lr.Y + 26, mid, lr.Y + 42);
        FillRR(g, Math.Abs(sh) > 0.5f ? Warn : Good, Rc(Math.Min(mid, mid + sh * lr.Width * 0.45f), lr.Y + 30, Math.Max(3, Math.Abs(sh) * lr.Width * 0.45f), 8), 3);
        Txt(g, pc.ShiftCents.ToString("+0;-0;0") + " cents", 10, DimC, Rc(lr.X + 6, lr.Y + 26, 70, 16), StringAlignment.Near, false);
        return y + 54;
    }

    static string PitchName(double hz) { if (hz <= 0) return ""; double m = 69 + 12 * Math.Log(hz / 440.0) / Math.Log(2); int n = (int)Math.Round(m); return PitchCorrector.Keys[((n % 12) + 12) % 12] + (n / 12 - 1); }
}

