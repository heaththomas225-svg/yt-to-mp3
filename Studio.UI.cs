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
    public RectangleF R; public Action OnDown, OnRight, OnDouble, OnUp; public Action<PointF> OnDrag; public Action<int> OnWheel; public bool WhileBusy;
}

partial class StudioForm : Form
{
    internal App app; internal StudioEngine eng;
    readonly List<HitReg> regs = new List<HitReg>(); HitReg hover, dragReg;
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); FrameLoop studioLoop; readonly FpsCounter fpsc = new FpsCounter(); double lastTickMs, lastStepMs; readonly System.Diagnostics.Stopwatch studioClock = System.Diagnostics.Stopwatch.StartNew(); bool lastIdle;
    internal void ApplyFps() { if (studioLoop != null) studioLoop.TargetFps = Math.Max(30, Math.Min(Prefs.FpsCap, FrameLoop.DisplayHz())); }
    double FrameGapMs { get { return 1000.0 / Math.Max(30, Math.Min(Prefs.FpsCap, FrameLoop.DisplayHz())) - 1.5; } }
    // one step of the studio loop: housekeeping about 60 times a second, drawing as fast as the chosen frame rate allows
    void FrameStep()
    {
        if (moving) return; double now = studioClock.Elapsed.TotalMilliseconds; if (now - lastTickMs >= 15) { lastTickMs = now; Tick(); }
        if (NeedsFrame()) { double dt = (now - lastStepMs) / 1000.0; lastStepMs = now; if (dt > 0.1) dt = 0.033; if (dt < 0.001) dt = 0.001; t += (float)dt; Invalidate(); }
    }
    float t;

    // theme (the app's deep purple, darker and calmer for a work surface)
    static readonly Color Bg0 = Color.FromArgb(7, 4, 16), Bg1 = Color.FromArgb(18, 8, 36), Panel = Color.FromArgb(235, 18, 10, 34), Lane = Color.FromArgb(255, 11, 6, 22), Lane2 = Color.FromArgb(255, 15, 8, 29);
    static readonly Color Edge = Color.FromArgb(120, 140, 85, 210), Ink = Color.FromArgb(240, 232, 255), Accent = Color.FromArgb(178, 108, 255), Accent2 = Color.FromArgb(236, 92, 226);
    static readonly Color Violet = Color.FromArgb(118, 52, 212), BtnC = Color.FromArgb(70, 40, 120), Good = Color.FromArgb(120, 230, 160), Warn = Color.FromArgb(255, 190, 90), Bad = Color.FromArgb(255, 100, 140);
    static readonly Color DimC = Color.FromArgb(165, 148, 190);

    int tab;                         // 0 Mix, 1 Mastering
    int selTrack, selFx = -1, selMaster = 0; long scroll; double spp = 600; float vscroll;
    Dictionary<Track, float[]> hold = new Dictionary<Track, float[]>();
    double[] spec = new double[2049]; double[] specIn = new double[4096], specRe = new double[4096], specIm = new double[4096];
    float genScroll, panelScroll; int eqBand = 2;
    // Auto-mix choice
    string amStyle = "Natural"; bool amEnhance = true;
    // browser overlay
    bool browserOpen; string browserQuery = ""; bool browserMaster; int browserTrack; float browserScroll;
    // busy overlay
    DateTime busyStart; volatile bool busy; string busyText = ""; volatile float busyPct = -1; volatile bool cancelFlag; bool busyCancelable;
    string projPath = "", projName = "Untitled project";
    bool recOn;   // notes played on an instrument are recorded onto its track while the song plays
    string toast = ""; DateTime toastAt = DateTime.MinValue;
    float lufsPeakHold = -120;
    TextBox editBox; Action<string> editApply;
    List<float> trail8 = new List<float>();

    void InitEdit()
    {
        editBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(10, 5, 22), ForeColor = Ink, Font = new Font("Segoe UI", 10f), TextAlign = HorizontalAlignment.Center };
        Controls.Add(editBox);
        editBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitEdit(true); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CommitEdit(false); }
        };
        editBox.Leave += (s, e) => CommitEdit(true);
    }

    // Click a value to type an exact number (Enter applies, Esc cancels).
    void BeginEdit(RectangleF r, string text, Action<string> apply)
    {
        editApply = apply; editBox.SetBounds((int)r.X - 4, (int)r.Y - 3, (int)Math.Max(78, r.Width) + 8, (int)Math.Max(24, r.Height) + 6);
        editBox.Text = text; editBox.Visible = true; editBox.BringToFront(); editBox.Focus(); editBox.SelectAll();
    }

    void CommitEdit(bool apply)
    {
        if (editBox == null || !editBox.Visible) return;
        var a = editApply; string txt = editBox.Text; editBox.Visible = false; editApply = null; Focus();
        if (apply && a != null) { try { a(txt); } catch { } }
    }

    HitReg ValueEdit(RectangleF r, string text, Action<string> apply) { return Reg(r, () => BeginEdit(r, text, apply)); }

    static bool ParseNum(string s, out float v)
    {
        v = 0; if (s == null) return false; s = s.Trim().Replace(',', '.');
        if (s.ToLowerInvariant().Contains("inf")) { v = -999; return true; }
        var m = Regex.Match(s, @"-?\d+(\.\d+)?"); return m.Success && float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
    static string Num(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    public StudioForm(App owner)
    {
        app = owner; eng = app.StudioEng; Win.Dark(this); InitEdit();
        Text = "Purphex Music"; StartPosition = FormStartPosition.CenterParent; BackColor = Bg0;
        ClientSize = new Size(1360, 800); MinimumSize = new Size(1240, 680);
        DoubleBuffered = true; KeyPreview = true; AllowDrop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        studioLoop = new FrameLoop(this, FrameStep) { Slow = () => WindowState == FormWindowState.Minimized }; ApplyFps();
        InstrumentFx.Recorder = OnMidiNote;
        MinimumSize = new Size(1240, 640);
        FormClosed += (s, e) => { flexOpen = false; DisposeRetired(); Auditioner.Shutdown(); };
        FormClosed += (s, e) => { if (backdrop != null) { backdrop.Dispose(); backdrop = null; } foreach (var pc0 in peakCache.Values) if (pc0.Img != null) pc0.Img.Dispose(); peakCache.Clear(); };
        FormClosed += (s, e) => { timer.Stop(); if (studioLoop != null) studioLoop.Dispose(); eng.Pause(); };
        DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; dragOver = true; Invalidate(); } };
        DragOver += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; if (!dragOver) { dragOver = true; Invalidate(); } } };
        DragLeave += (s, e) => { dragOver = false; Invalidate(); };
        DragDrop += (s, e) => { dragOver = false; Invalidate(); var fl = (string[])e.Data.GetData(DataFormats.FileDrop); foreach (var f in fl) PromptImport(f); };
        FitView();
    }

    volatile bool moving;
    // Redraw only when something is changing; an idle studio repaints once a second (for loading and background state). Input times are stamped so hover and drags stay live.
    DateTime lastInput = DateTime.Now, lastFrame = DateTime.MinValue, lastPlayAt = DateTime.MinValue; int lastW, lastH;
    bool NeedsFrame()
    {
        var now = DateTime.Now;
        if (ClientSize.Width != lastW || ClientSize.Height != lastH) { lastW = ClientSize.Width; lastH = ClientSize.Height; lastFrame = now; return true; }
        if (eng.Playing) lastPlayAt = now;
        bool anim = eng.Playing || busy || browserOpen || dragReg != null || (now - lastInput).TotalMilliseconds < 600 || (now - toastAt).TotalSeconds < 3.7 || (now - lastPlayAt).TotalMilliseconds < 1500 || installing.Length > 0 || mic != null || Auditioner.IsPlaying;   // the 1.5 s after playback lets the meters settle
        double gap = (now - lastFrame).TotalMilliseconds;
        // a slow computer redraws less often (paint time x3) so the window never starves the audio
        lastIdle = !anim; if (!(anim ? gap >= Math.Max(FrameGapMs, Math.Min(250, paintMs * 3)) : gap >= 1000)) return false;
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
            double target = Math.Max(0, eng.Pos - WaveOutPlayer.QueuedFrames + 512);
            if (Math.Abs(dispPos - target) > 9000) dispPos = target;
            else { dispPos += dt * StudioEngine.SR; dispPos += (target - dispPos) * Math.Min(1.0, dt * 8); }
        }
        else dispPos = eng.Pos;
    }
    protected override void WndProc(ref Message m)
    {
        Win.TrackMove(ref m); if (m.Msg == 0x231) moving = true; else if (m.Msg == 0x232) moving = false;
        if (m.Msg == 0x020E && tab == 0) { int dlt = (short)(((long)m.WParam >> 16) & 0xFFFF); ScrollView(dlt / 120.0 * 0.15); Invalidate(); }   // touch pad: swipe sideways
        base.WndProc(ref m);
    }

    bool autoFit = true; int lastFitW; long lastFitLen;   // while true, the song keeps filling the track area (full screen, resizing, new recordings)
    void FitView()
    {
        long len = Math.Max(eng.EndFrame, StudioEngine.SR * 10); float w = Math.Max(400, TimelineRect().Width - 24);
        spp = Math.Max(1, len / (double)w); scroll = 0; autoFit = true;
    }

    void Tick()
    {
        UpdatePlayhead(); Auditioner.Tick(); FlexTick();
        if (mic != null && !eng.Playing) StopMic();
        { string tt = "Purphex Music  -  " + projName + (Prefs.ShowFps ? "    " + (lastIdle ? "idle" : fpsc.Fps + " fps") : ""); if (Text != tt) Text = tt; }
        if (eng.Playing)
        {
            int u = WaveOutPlayer.Underruns;
            if ((DateTime.Now - underSince).TotalSeconds > 6) { underSince = DateTime.Now; lastUnder = u; }
            else if (u - lastUnder >= 3 && Prefs.Buf < 2)
            {
                Prefs.Buf++; lastUnder = u; underSince = DateTime.Now; eng.ReopenOutput(); Prefs.Changed();
                Toast("The sound was breaking up, so I made the playback buffer bigger (Settings > Audio).");
            }
        }
        if (eng.Playing)
        {
            float w = TimelineRect().Width; long x = (long)((DispPos - scroll) / spp);
            if (x > w * 0.92f || x < 0) scroll = Math.Max(0, DispPos - (long)(w * 0.08 * spp));
        }
        if (NeedsFrame0() && Environment.TickCount - eqPaintTick < 600) UpdateSpectrum();
    }

    bool NeedsFrame0() { return eng.Playing || (DateTime.Now - lastInput).TotalMilliseconds < 1500; }

    // ---------------- geometry ----------------
    const int ToolH = 70, TransH = 86, HeadW = 290, RulerH = 28;
    int PanelW { get { return soundsOpen && tab == 0 ? 640 : 380; } }   // the right-hand panel is wider while the sound browser is open
    int LaneH = 132;   // track height: Alt + mouse wheel makes the tracks taller so the waveform shows more detail
    RectangleF TimelineRect() { return new RectangleF(HeadW, ToolH + TransH + RulerH, Math.Max(100, ClientSize.Width - HeadW - PanelW), Math.Max(100, ClientSize.Height - ToolH - TransH - RulerH - 6)); }

    // ---------------- drawing helpers ----------------
    static GraphicsPath RR(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void FillRR(Graphics g, Color c, RectangleF r, float rad) { using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    static void StrokeRR(Graphics g, Color c, RectangleF r, float rad, float w) { using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }
    static void Txt(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    {
        using (var b = new SolidBrush(c)) g.DrawString(s, GfxCache.Font(px, bold), b, r, GfxCache.Fmt(ha, true));
    }
    static RectangleF Rc(float x, float y, float w, float h) { return new RectangleF(x, y, w, h); }
    static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

    HitReg Reg(RectangleF r, Action down) { var g = new HitReg { R = r, OnDown = down }; regs.Add(g); return g; }
    bool Hot(RectangleF r) { return hover != null && hover.R == r; }

    // Plain-English hints shown at the bottom while the mouse is over a button.
    string hintNow = "";
    static readonly Dictionary<string, string> Hints = new Dictionary<string, string> {
        { "ADD MUSIC", "Add a song, a vocal recording or a beat. You can also drop files onto this window." },
        { "SPLIT VOCALS + BEAT", "Splits the selected FULL SONG into a vocals track and an instrumental track (AI)." },
        { "AUTO MIX", "Balances the levels and builds an effect chain on every track, then lists what it changed." },
        { "Undo mix", "Puts everything back the way it was before Auto Mix." },
        { "EXPORT", "Save the finished mix, the vocals, the instrumental or all stems as WAV, FLAC or MP3." },
        { "Help", "Open the guides. Each one can show you on screen where to click." },
        { "Open", "Open a saved project (Ctrl+O)." }, { "Save", "Save this project (Ctrl+S)." },
        { "Save as", "Save a copy under a new name (Ctrl+Shift+S)." },
        { "Full", "Full screen (F11). Esc leaves it." }, { "Window", "Leave full screen (Esc)." },
        { "PLAY", "Play from the needle (Space)." }, { "PAUSE", "Pause (Space)." }, { "STOP", "Stop and go back to the start." }, { "|<", "Jump back to the start." },
        { "LOOP", "Repeat the song when it reaches the end." },
        { "MIX", "The tracks, faders and effects." }, { "≡", "Menu: new, open, recent projects, my projects with pictures, edit, tools, community, settings and help." }, { "+ TRACK", "Add a track: audio from your PC or an instrument, above or below the selected track or at the end." }, { "+ ADD TRACK", "Add a track at the end of the list." }, { "LYRICS", "A page for writing your own lyrics, with line and syllable counts." }, { "MASTERING", "Final loudness and polish on the whole mix, with LUFS meters and A/B compare." },
        { "+ Add Effect", "Pick an effect (EQ, compressor, reverb, Auto-Tune...) to add to this track." },
        { "+ Add Effect to master", "Add an effect to the final mix instead of one track." },
        { "Chain presets...", "Load a ready-made effect chain, for example a rap vocal chain." },
        { "Bypass", "Turn this effect off without removing it." }, { "Presets", "Load or save settings for this effect." },
        { "^", "Move this effect up (it runs earlier)." }, { "v", "Move this effect down (it runs later)." },
        { "x", "Remove it." }, { "M", "Mute this track." }, { "S", "Solo: hear only this track." },
        { "+ INSTRUMENT", "Add a VST instrument (like Vital) as a track you can play with your keyboard. Set up plugins in Menu > Plugins." },
        { "REC", "Arm recording: press PLAY, then play notes on an instrument window and they are recorded onto its track." }, { "* REC", "Recording is armed. Press again to turn it off." },
        { "Piano", "Open the instrument window: its own controls plus a piano you can play." }, { "C", "Clear the recorded notes on this instrument track." },
        { "+ Effect", "Add an effect to this instrument track (EQ, reverb...)." },
        { "FLEX", "Flex Pitch: see every note of the selected recording and drag notes up or down to re-tune them, steady the pitch, take out vibrato, change volume and formants." },
        { "SPLIT", "Cut the selected track in two at the needle (key B). Each piece can be moved and trimmed." },
        { "TRIM <", "Cut the start of the selected clip off at the needle." }, { "TRIM >", "Cut the end of the selected clip off at the needle." },
        { "DUP", "Make a copy of the selected track." }, { "DEL", "Delete the selected track (Undo brings it back)." },
        { "UNDO", "Undo the last edit (Ctrl+Z)." }, { "REDO", "Redo (Ctrl+Y)." }, { "DETECT", "Listen to the song and set the BPM for you." },
        { "SNAP", "Moves, trims and cuts snap to the beat grid. Click to turn off." }, { "snap", "Snap to the beat grid is off. Click to turn it on." },
        { "CLICK", "Metronome on. Click to turn off." }, { "click", "Play a metronome click while the song plays." },
        { "-", "Zoom out." }, { "+", "Zoom in." }, { "FIT", "Zoom to fit the whole song." },
        { "MIC", "Record from your microphone onto a new track (use headphones). Press again, or STOP, to finish." },
        { "NEW", "Start a new empty project." },
        { "Add", "Add this effect." }, { "Cancel (Esc)", "Stop the current job." }
    };

    // style: 0 normal, 1 accent, 2 hot gradient, 3 danger, 4 toggle-on
    HitReg Btn(Graphics g, RectangleF r, string text, int style, Action down, float px = 15)
    {
        if (Hot(r)) { string hh; if (Hints.TryGetValue(text, out hh)) hintNow = hh; }
        Color c = style == 1 ? Violet : (style == 3 ? Color.FromArgb(150, 40, 80) : (style == 4 ? Color.FromArgb(150, 70, 230) : BtnC));
        if (Hot(r)) c = Color.FromArgb(Math.Min(255, c.R + 28), Math.Min(255, c.G + 22), Math.Min(255, c.B + 30));
        if (style == 2)
        {
            using (var p = RR(r, 8)) using (var br = new LinearGradientBrush(r, Color.FromArgb(150, 60, 240), Color.FromArgb(236, 92, 226), 0f)) g.FillPath(br, p);
            if (Hot(r)) FillRR(g, Color.FromArgb(40, 255, 255, 255), r, 8);
        }
        else FillRR(g, c, r, 6);
        StrokeRR(g, Color.FromArgb(style == 2 ? 200 : 110, 200, 150, 255), r, style == 2 ? 8 : 6, 1);
        if (style == 4) Glow(g, Accent2, r, 8, 0.8f, 3); else if (Hot(r)) Glow(g, NeonCyan, r, 8, 0.7f, 3); else if (style == 2) Glow(g, NeonViolet, r, 8, 0.5f, 3);
        Txt(g, text, px, Color.White, r, StringAlignment.Center, style == 2 || style == 1);
        return Reg(r, down);
    }

    // horizontal slider; frac 0..1; returns region so callers can add OnDouble / OnWheel
    HitReg Slider(Graphics g, RectangleF r, float frac, Action<float> set, Color? fill = null)
    {
        float ty = r.Y + r.Height / 2 - 3; var tr = Rc(r.X, ty, r.Width, 6);
        FillRR(g, Color.FromArgb(180, 10, 4, 22), tr, 3);
        float fx = r.X + r.Width * Clamp01(frac); if (fx - r.X > 1) FillRR(g, fill ?? Accent, Rc(r.X, ty, fx - r.X, 6), 3);
        using (var b = new SolidBrush(Color.FromArgb(244, 236, 255))) g.FillEllipse(b, fx - 6, r.Y + r.Height / 2 - 6, 12, 12);
        var reg = new HitReg { R = r };
        reg.OnDown = () => set(Clamp01((MousePos.X - r.X) / r.Width));
        reg.OnDrag = pt => set(Clamp01((pt.X - r.X) / r.Width));
        regs.Add(reg); return reg;
    }

    PointF MousePos;

    static string Db(double v) { return double.IsNaN(v) || v < -99 ? "-inf" : v.ToString("0.0", CultureInfo.InvariantCulture); }

    // ---------------- main paint ----------------
    int paintErrors; double paintMs = 8; int lastUnder; DateTime underSince = DateTime.Now;
    protected override void OnPaint(PaintEventArgs e)
    {
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
        hintNow = ""; bubble = "";
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        regs.Clear();
        var full = new RectangleF(0, 0, ClientSize.Width, ClientSize.Height);
        DrawBackdrop(g);
        if (tab == 0) PaintMix(g); else if (tab == 2) PaintLyrics(g); else if (tab == 3) PaintProjects(g); else if (tab == 4) PaintCommunity(g); else PaintMastering(g);
        if (tab != 2) SyncLyricUi(false);
        if (tab == 3 || tab == 4) PaintSlimBar(g); else { PaintToolbar(g); PaintTransport(g); }   // the Community and My Projects pages have no music controls
        if (browserOpen) PaintBrowser(g);
        PaintSoundDrag(g); PaintPackManager(g);
        if (soundBox != null && soundBox.Visible && !(soundsOpen && tab == 0)) soundBox.Visible = false;
        PaintNewTrackSheet(g); PaintBusSheet(g);
        PaintMenu(g);
        PaintBubble(g);
        fpsc.Frame();
        if (hintNow.Length > 0 && !busy) { var hr = Rc(ClientSize.Width / 2 - 340, ClientSize.Height - 36, 680, 26); FillRR(g, Color.FromArgb(235, 30, 16, 58), hr, 8); StrokeRR(g, Edge, hr, 8, 1); Txt(g, hintNow, 13, Ink, hr, StringAlignment.Center, false); }
        if (busy) PaintBusy(g);
        if ((DateTime.Now - toastAt).TotalSeconds < 3.5 && toast.Length > 0)
        {
            var tr = Rc(ClientSize.Width / 2 - 260, ClientSize.Height - 70, 520, 38); FillRR(g, Color.FromArgb(240, 40, 20, 80), tr, 10); StrokeRR(g, Accent, tr, 10, 1.2f);
            Txt(g, toast, 15, Ink, tr, StringAlignment.Center, false);
        }
    }

    Bitmap backdrop; int backVer = -1;
    void DrawBackdrop(Graphics g)
    {
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        if (moving && backdrop != null) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(backdrop, 0, 0, w, h); g.CompositingMode = CompositingMode.SourceOver; return; }   // while a border is dragged, stretch the old picture; one real rebuild follows when the drag ends
        if (backdrop == null || backdrop.Width != w || backdrop.Height != h || backVer != UserBackground.Version)
        {
            if (backdrop != null) backdrop.Dispose(); backdrop = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(backdrop))
            {
                bg.SmoothingMode = SmoothingMode.AntiAlias; var full = new RectangleF(0, 0, w, h);
                backVer = UserBackground.Version; var still = Prefs.BgStudio ? UserBackground.Fresh(w, h) : null;
                if (still != null) { bg.DrawImageUnscaled(still, 0, 0); still.Dispose(); using (var dm = new SolidBrush(Color.FromArgb(Math.Min(255, Math.Max(150, Prefs.BgDim * 255 / 100 + 70)), 6, 2, 16))) bg.FillRectangle(dm, full); }
                else
                {
                    using (var br = new LinearGradientBrush(full, Bg0, Bg1, 80f)) bg.FillRectangle(br, full);
                    using (var gp = new GraphicsPath())
                    {
                    float rad = 420; gp.AddEllipse(w * 0.7f - rad, 80 - rad, rad * 2, rad * 2);
                    using (var pg = new PathGradientBrush(gp)) { pg.CenterColor = Color.FromArgb(46, 150, 60, 240); pg.SurroundColors = new[] { Color.FromArgb(0, 150, 60, 240) }; bg.FillPath(pg, gp); }
                    }
                }
            }
        }
        g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(backdrop, 0, 0); g.CompositingMode = CompositingMode.SourceOver;
    }

    // guided tours for the studio (rectangles match the toolbar / panel layout)
    internal void RunTour(string id)
    {
        var s = new List<TourStep>(); float W = ClientSize.Width;
        if (id == "studio-start") { s.Add(new TourStep(() => new Rectangle(14, 12, 168, 46), "1. Add music", "Press ADD MUSIC and choose a song, vocal or beat. You can also drop files on this window.")); s.Add(new TourStep(() => new Rectangle(192, 12, 262, 46), "2. Split a full song", "Select the song track, then press this. The AI makes real VOCAL and INSTRUMENTAL tracks, perfectly in sync.")); s.Add(new TourStep(() => new Rectangle(14, 78, 330, 36), "3. Play", "Press PLAY (or Space). Use M and S on each track to mute or solo.")); }
        else if (id == "studio-automix") { s.Add(new TourStep(() => new Rectangle(594, 16, 154, 38), "Pick a style", "Natural, Clean Studio, Melodic, Aggressive or Technical. The + means vocal enhancement is on.")); s.Add(new TourStep(() => new Rectangle(464, 12, 130, 46), "Press AUTO MIX", "It balances levels, builds a chain per track and lists every change. Undo mix reverts it.")); }
        else if (id == "studio-export") { s.Add(new TourStep(() => new Rectangle((int)W - 126, 12, 112, 46), "Export", "Choose the finished mix, vocals only, instrumental, dry versions or stems, in WAV, FLAC or MP3.")); s.Add(new TourStep(() => new Rectangle((int)W - 392, 16, 80, 38), "Save your project", "Save keeps tracks, plugin settings and alignment so you can reopen it later.")); }
        if (s.Count > 0) Tour.Run(this, s);
    }

    internal void ShowMastering() { tab = 1; lastInput = DateTime.Now; if (eng.Tracks.Count == 0) Toast("Add your song with ADD MUSIC, set the mastering chain here, then EXPORT."); }

    internal void Toast(string s) { toast = s; toastAt = DateTime.Now; }

    // ---------------- toolbar ----------------
    void PaintToolbar(Graphics g)
    {
        float W = ClientSize.Width;
        using (var br = new SolidBrush(Color.FromArgb(245, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH);
        NeonRule(g, 0, ToolH, W);
        float y = 12, h = 46;
        // right group first (fixed sizes from the right edge), then the left group is squeezed to fit what is left
        float rx = W - 14; string[] rn = { "EXPORT", "Help", "Full", "Open", "Save", "Save as" }; float[] rw = { 106, 58, 58, 72, 72, 84 };
        float rightStart = W - 14; for (int i = 0; i < rw.Length; i++) rightStart -= rw[i] + 6; rightStart += 6;
        float avail = rightStart - 70 - 10, leftTotal = 156 + 232 + 126 + 140 + 84 + 36; float f = Math.Max(0.5f, Math.Min(1f, avail / leftTotal));
        float x = 70; float fp = Math.Max(11, 15 * f);
        Btn(g, Rc(12, y, 48, h), "≡", 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 24);
        Btn(g, Rc(x, y, 156 * f, h), "ADD MUSIC", 2, () => AddMusicClick(), Math.Max(13, 16 * f)); x += 164 * f;
        Btn(g, Rc(x, y, 232 * f, h), "SPLIT VOCALS + BEAT", 1, () => SeparateClick(), Math.Max(12, 14 * f)); x += 240 * f;
        Btn(g, Rc(x, y, 126 * f, h), "AUTO MIX", 2, () => AutoMixClick(), Math.Max(13, 16 * f)); x += 132 * f;
        Btn(g, Rc(x, y + 4, 140 * f, h - 8), amStyle + (amEnhance ? " +" : ""), 0, () => AutoMixMenu(), Math.Max(12, 13 * f));
        Txt(g, "style", 11, DimC, Rc(x + 4, y - 4, 60, 12), StringAlignment.Near, false); x += 148 * f;
        Btn(g, Rc(x, y + 4, 84 * f, h - 8), "Undo mix", 0, () => { if (AutoMix.LastSnapshot != null) { AutoMix.Undo(eng); Toast("Auto Mix undone"); } }, Math.Max(12, 13 * f)); x += 84 * f;
        float leftEnd = x;
        Btn(g, Rc(rx - 106, y, 106, h), "EXPORT", 1, () => ExportClick(), 16); rx -= 112;
        Btn(g, Rc(rx - 58, y + 4, 58, h - 8), "Help", 2, () => app.ShowHelp(null), 14); rx -= 64;
        Btn(g, Rc(rx - 58, y + 4, 58, h - 8), Win.IsFullscreen(this) ? "Window" : "Full", 0, () => Win.ToggleFullscreen(this), 13); rx -= 64;
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
        using (var br = new SolidBrush(Color.FromArgb(240, 22, 12, 40))) g.FillRectangle(br, 0, y0, W, TransH);
        NeonRule(g, 0, y0 + TransH, W);
        float x = 14, y = y0 + 8, h = 36;
        Btn(g, Rc(x, y, 44, h), "|<", 0, () => { eng.Seek(0); scroll = 0; }, 15); x += 50;
        Btn(g, Rc(x, y, 70, h), eng.Playing ? "PAUSE" : "PLAY", eng.Playing ? 4 : 1, () => { if (eng.Playing) eng.Pause(); else PlayGuard(); }, 14); x += 76;
        Btn(g, Rc(x, y, 56, h), "STOP", 0, () => { eng.Stop(); scroll = 0; }, 14); x += 62;
        Btn(g, Rc(x, y, 62, h), "LOOP", eng.Loop ? 4 : 0, () => eng.Loop = !eng.Loop, 14); x += 72;
        double sec = dispPos / StudioEngine.SR, tot = eng.EndFrame / (double)StudioEngine.SR;
        var tr = Rc(x, y, 210, h); FillRR(g, Color.FromArgb(200, 8, 4, 18), tr, 6); StrokeRR(g, Edge, tr, 6, 1);
        Txt(g, Fmt(sec) + "  /  " + Fmt(tot), 19, Accent, tr, StringAlignment.Center, true); x += 222;
        Btn(g, Rc(x, y, 100, h), "+ TRACK", 2, () => ShowAddTrackMenuMain(), 13); x += 106;
        Btn(g, Rc(x, y, 70, h), "+ BUS", 2, () => OpenBusSheet(), 13); x += 76;
        Btn(g, Rc(x, y, 124, h), "+ INSTRUMENT", 1, () => AddInstrumentClick(), 12); x += 130;
        Btn(g, Rc(x, y, 70, h), recOn ? "* REC" : "REC", recOn ? 3 : 0, () => { recOn = !recOn; Toast(recOn ? "Recording is armed: press PLAY, then play notes on an instrument window." : "Recording off."); }, 13); x += 76;
        { bool rec = mic != null; double el = rec ? (DateTime.Now - micT0).TotalSeconds : 0; var mr = Rc(x, y, rec ? 128 : 60, h); Btn(g, mr, rec ? "* MIC  " + ((int)(el / 60)).ToString("00") + ":" + ((int)(el % 60)).ToString("00") : "MIC", rec ? 3 : 0, () => MicClick(), 13); if (rec) HBar(g, Rc(mr.X + 6, mr.Bottom - 7, mr.Width - 12, 5), mic.Level); x += mr.Width + 6; }
        Btn(g, Rc(x, y, 92, h), "SOUNDS", soundsOpen ? 4 : 0, () => { if (soundsOpen) CloseSounds(); else OpenSounds(); }, 12); x += 98;

        // tabs on the right; master meters in whatever room is left between
        float tx = W - 328;
        float mx = Math.Max(x + 16, W - PanelW - 300); float room = tx - 12 - mx;
        if (room >= 70)
        {
            Txt(g, "MASTER", 11, DimC, Rc(mx, y - 4, 60, 14), StringAlignment.Near, false);
            float bw = Math.Min(120, room); HBar(g, Rc(mx, y + 12, bw, 8), eng.MasterPeakL); HBar(g, Rc(mx, y + 22, bw, 8), eng.MasterPeakR);
            if (room >= 230) { float lm = eng.Lufs.Momentary; Txt(g, "M " + Db(lm) + " LUFS", 13, Ink, Rc(mx + 128, y, Math.Min(120, room - 128), h), StringAlignment.Near, false); }
        }
        Btn(g, Rc(tx, y, 106, h), "MIX", tab == 0 ? 4 : 0, () => tab = 0, 14);
        Btn(g, Rc(tx + 112, y, 106, h), "MASTERING", tab == 1 ? 4 : 0, () => tab = 1, 13);
        Btn(g, Rc(tx + 224, y, 90, h), "LYRICS", tab == 2 ? 4 : 0, () => tab = 2, 13);
        if (tab == 0) PaintDawBar(g, y0 + 52, W);   // the editing tools only belong to the track view
    }

    static string Fmt(double s) { int m = (int)(s / 60); double r = s - m * 60; return m.ToString("00") + ":" + r.ToString("00.00", CultureInfo.InvariantCulture); }

    void HBar(Graphics g, RectangleF r, float lin)
    {
        FillRR(g, Color.FromArgb(180, 8, 4, 18), r, r.Height / 2);
        float db = lin > 0 ? (float)(20 * Math.Log10(lin)) : -90; float f = Clamp01((db + 60) / 60f); if (f < 0.01f) return;
        Color c = db > -1 ? Bad : (db > -6 ? Warn : Good);
        using (var br = new LinearGradientBrush(r, Violet, c, 0f)) using (var p = RR(Rc(r.X, r.Y, r.Width * f, r.Height), r.Height / 2)) g.FillPath(br, p);
    }

    void PlayGuard()
    {
        try { eng.Play(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Playback"); }
    }

    // ---------------- MIX view ----------------
    void PaintMix(Graphics g)
    {
        float top = ToolH + TransH; var tl = TimelineRect();
        PruneTrackCaches();
        int n = LaneCount;   // buses are not lanes: they live in the Buses sheet and in each track's Bus drop-down
        if (autoFit && (lastFitW != (int)tl.Width || lastFitLen != eng.EndFrame) && tl.Width > 100) { FitView(); lastFitW = (int)tl.Width; lastFitLen = eng.EndFrame; }
        // clamp selection
        if (selTrack >= n) selTrack = Math.Max(0, n - 1);
        // ruler
        var ruler = Rc(HeadW, top, tl.Width, RulerH); using (var br = new SolidBrush(Color.FromArgb(240, 18, 10, 34))) g.FillRectangle(br, ruler);
        PaintRuler(g, ruler);
        var rr = Reg(ruler, () => { eng.Seek(scroll + (long)((MousePos.X - HeadW) * spp)); }); rr.OnDrag = pt => eng.Seek(Math.Max(0, scroll + (long)((pt.X - HeadW) * spp)));

        float y = top + RulerH - vscroll;
        g.SetClip(Rc(0, top + RulerH, ClientSize.Width - PanelW, ClientSize.Height - top - RulerH - 46));
        for (int i = 0; i < n; i++, y += LaneH)
        {
            if (y + LaneH < top + RulerH || y > ClientSize.Height) continue;
            var tr = eng.Tracks[i]; bool sel = i == selTrack;
            var lane = Rc(HeadW, y, tl.Width, LaneH); using (var br = new SolidBrush(i % 2 == 0 ? Lane : Lane2)) g.FillRectangle(br, lane);
            if (tr.IsBus) PaintBusLane(g, tr, lane); else if (tr.Instrument) PaintMidi(g, tr, lane); else if (tr.Clip == null) PaintEmptyLane(g, tr, lane); else PaintWave(g, tr, lane, sel);
            var lr = LaneReg(lane, tr); int idx = i;
            PaintLaneTools(g, tr, lane);
            PaintHeader(g, tr, i, Rc(0, y, HeadW, LaneH), sel);
            NeonLaneChrome(g, tr, Rc(0, y, HeadW, LaneH), lane, sel);
            using (var pen = new Pen(Color.FromArgb(70, 140, 85, 210))) g.DrawLine(pen, 0, y + LaneH, tl.Right, y + LaneH);
        }
        AddTrackRow(g, Math.Max(y, top + RulerH), n);
        { float ey = Math.Max(y, top + RulerH); if (ey < ClientSize.Height) { var em = Reg(Rc(HeadW, ey, tl.Width, ClientSize.Height - ey), () => { toolsTrack = null; eng.Seek(Math.Max(0, Snap(scroll + (long)((MousePos.X - HeadW) * spp)))); }); em.OnDrag = pt => eng.Seek(Math.Max(0, Snap(scroll + (long)((pt.X - HeadW) * spp)))); } }
        if (n == 0)
        {
            var r0 = Rc(HeadW + 40, top + RulerH + 60, tl.Width - 80, 150);
            bool over = Hot(r0), drop = dragOver; FillRR(g, Color.FromArgb(drop ? 170 : (over ? 140 : 110), 40, 20, 80), r0, 14); Glow(g, drop ? NeonCyan : NeonViolet, r0, 14, drop ? 1.3f : (over ? 0.9f : 0.45f), 4);
            using (var pen = new Pen(Color.FromArgb(drop ? 255 : 150, drop ? NeonCyan : NeonViolet), 2f) { DashStyle = DashStyle.Dash }) { var inner = RectangleF.Inflate(r0, -10, -10); using (var p = RR(inner, 10)) g.DrawPath(pen, p); }
            Txt(g, drop ? "Drop your music here" : "Add your music to start", 26, Ink, Rc(r0.X, r0.Y + 14, r0.Width, 40), StringAlignment.Center, true);
            Txt(g, "Click this box to pick songs from your computer, or drag and drop them onto it.", 15, drop ? Ink : DimC, Rc(r0.X, r0.Y + 62, r0.Width, 26), StringAlignment.Center, false);
            Txt(g, "MP3, WAV, FLAC, M4A, OGG and more. For a full song, split it afterwards.", 13, DimC, Rc(r0.X, r0.Y + 92, r0.Width, 24), StringAlignment.Center, false);
            Reg(r0, () => AddMusicClick()); if (over) { bubble = "Click to choose music from your computer"; bubblePt = MousePos; }
        }
        g.ResetClip();
        PaintGrid(g, tl, top + RulerH, ClientSize.Height);
        // playhead
        float px = HeadW + (float)((dispPos - scroll) / spp);
        if (px >= HeadW && px <= tl.Right) { GlowLine(g, NeonPink, px, top, px, ClientSize.Height, 1f); using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, new[] { new PointF(px - 7, top), new PointF(px + 7, top), new PointF(px, top + 12) }); }
        PaintOverview(g, tl);
        // wheel: scroll / zoom
        var wr = new HitReg { R = Rc(HeadW, top, tl.Width, ClientSize.Height - top) };
        wr.OnWheel = d =>
        {
            if ((ModifierKeys & Keys.Control) != 0 && (ModifierKeys & Keys.Alt) == 0 && (ModifierKeys & Keys.Shift) == 0) autoFit = false;
            if ((ModifierKeys & Keys.Alt) != 0 || ((ModifierKeys & Keys.Control) != 0 && (ModifierKeys & Keys.Shift) != 0)) { LaneH = Math.Max(64, Math.Min(420, LaneH + (d > 0 ? 16 : -16))); vscroll = Math.Max(0, Math.Min(vscroll, Math.Max(0, n * LaneH - (ClientSize.Height - top - RulerH - 66)))); }
            else if ((ModifierKeys & Keys.Control) != 0) { double f = d > 0 ? 0.7 : 1.43; double cur = scroll + (MousePos.X - HeadW) * spp; spp = Math.Max(MinSpp, Math.Min(MaxSpp, spp * f)); scroll = Math.Max(0, (long)(cur - (MousePos.X - HeadW) * spp)); }
            else if ((ModifierKeys & Keys.Shift) != 0) ScrollView(-d * 0.25);
            else if (n * LaneH <= ClientSize.Height - top - RulerH - 40 || MousePos.Y < top + RulerH) ScrollView(-d * 0.25);   // everything fits vertically: the wheel moves along the song (like BandLab)
            else vscroll = Math.Max(0, Math.Min(Math.Max(0, n * LaneH - (ClientSize.Height - top - RulerH - 66)), vscroll - d * 40));
        };
        regs.Insert(0, wr);
        if (soundsOpen) PaintSoundsPanel(g); else PaintPluginPanel(g);
        if (flexOpen) PaintFlex(g);
    }

    void PaintRuler(Graphics g, RectangleF r)
    {
        double[] steps = { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 }; double secPerPx = spp / StudioEngine.SR, want = 90 * secPerPx, major = 3600;
        foreach (var s in steps) if (s >= want) { major = s; break; }
        double t0 = scroll / (double)StudioEngine.SR, first = Math.Floor(t0 / major) * major;
        using (var pen = new Pen(Color.FromArgb(120, 190, 140, 255)))
            for (double s = first; ; s += major)
            {
                float x = r.X + (float)((s - t0) / secPerPx); if (x > r.Right) break; if (x < r.X) continue;
                g.DrawLine(pen, x, r.Bottom - 10, x, r.Bottom); Txt(g, major >= 1 ? ((int)(s / 60)) + ":" + ((int)(s % 60)).ToString("00") : Fmt(s).Substring(0, 8), 11, DimC, Rc(x + 3, r.Y + 2, 60, 16), StringAlignment.Near, false);
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
    void PaintWaveBody(Graphics g, RectangleF lane, RectangleF cr, Color c, bool audible, bool sel, float cy, float amp, int px, float[] wmn, float[] wmx)
    {
        if (cr.Width > 0) { using (var p = RR(cr, 6)) using (var gb = new LinearGradientBrush(cr, Color.FromArgb(sel ? 70 : 44, c), Color.FromArgb(sel ? 14 : 8, c), 90f)) g.FillPath(gb, p); }
        if (cr.Width > 4)
        {
            // heartbeat-style line: one clean zig-zag through the peaks (a point every few pixels), blue > track colour > purple, with a soft glow
            var gst0 = g.Save(); g.SetClip(cr, CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias;
            int i0 = Math.Max(0, (int)(cr.X - lane.X)), i1 = Math.Min(px, (int)Math.Ceiling(cr.Right - lane.X)); int step = Math.Max(8, Math.Min(20, px / 110));
            var pts = new List<PointF>(); pts.Add(new PointF(cr.X, cy));
            for (int i = i0; i < i1; i += step)
            {
                float mx = 0, mn = 0; bool any = false; for (int j = i; j < Math.Min(i1, i + step); j++) { if (wmx[j] == 0 && wmn[j] == 0) continue; any = true; if (wmx[j] > mx) mx = wmx[j]; if (wmn[j] < mn) mn = wmn[j]; }
                float x = lane.X + i + step / 2f; if (!any) { pts.Add(new PointF(x, cy)); continue; }
                Func<float, float> sh = v => (float)(Math.Sign(v) * Math.Pow(Math.Min(1f, Math.Abs(v)), 0.85));
                pts.Add(new PointF(x - step * 0.22f, cy - sh(mx) * amp)); pts.Add(new PointF(x + step * 0.22f, cy - sh(mn) * amp));
            }
            pts.Add(new PointF(cr.Right, cy));
            using (var pen = new Pen(Color.FromArgb(audible ? 60 : 20, 120, 150, 255), 1f)) g.DrawLine(pen, cr.X, cy, cr.Right, cy);
            var bounds = new RectangleF(cr.X - 1, cr.Y, Math.Max(2, cr.Width + 2), cr.Height); int al = audible ? 255 : 100;
            using (var gb = new LinearGradientBrush(bounds, Color.FromArgb(al, 70, 150, 255), Color.FromArgb(al, 190, 90, 255), 0f))
            {
                gb.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(al, 70, 150, 255), Color.FromArgb(al, c), Color.FromArgb(al, 190, 90, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
                using (var gl = new LinearGradientBrush(bounds, Color.Blue, Color.Purple, 0f)) { gl.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(audible ? 46 : 14, 70, 150, 255), Color.FromArgb(audible ? 46 : 14, c), Color.FromArgb(audible ? 46 : 14, 190, 90, 255) }, Positions = new[] { 0f, 0.5f, 1f } }; using (var pen = new Pen(gl, 5.5f)) { pen.LineJoin = LineJoin.Round; if (pts.Count > 1) g.DrawLines(pen, pts.ToArray()); } }
                using (var pen = new Pen(gb, 1.8f)) { pen.LineJoin = LineJoin.Round; if (pts.Count > 1) g.DrawLines(pen, pts.ToArray()); }
            }
            g.Restore(gst0);
        }
    }
    class PeakCache { public AudioClip Clip; public long Start, Len; public double Spp; public int Px; public float[] Mn, Mx; public Bitmap Img; public int Ver, ImgVer, ImgCol; public RectangleF ImgLane, ImgCr; public bool ImgAud, ImgSel; }
    readonly Dictionary<Track, PeakCache> peakCache = new Dictionary<Track, PeakCache>();
    void PaintWave(Graphics g, Track tr, RectangleF lane, bool sel)
    {
        if (tr.Clip == null) return;
        int px = (int)lane.Width;
        PeakCache pc; if (!peakCache.TryGetValue(tr, out pc)) { pc = new PeakCache(); peakCache[tr] = pc; }
        if (pc.Clip != tr.Clip || pc.Start != scroll - tr.Offset + tr.ClipStart || pc.Spp != spp || pc.Px != px || pc.Len != tr.UsedLength) { if (pc.Mn == null || pc.Mn.Length < px) { pc.Mn = new float[px + 64]; pc.Mx = new float[px + 64]; } tr.Clip.GetPeaks(scroll - tr.Offset + tr.ClipStart, spp, px, pc.Mn, pc.Mx); pc.Clip = tr.Clip; pc.Start = scroll - tr.Offset + tr.ClipStart; pc.Spp = spp; pc.Px = px; pc.Len = tr.UsedLength; pc.Ver++; }
        var wmn = pc.Mn; var wmx = pc.Mx;
        Color c = Neon(tr.Color); bool audible = !tr.Mute; float cy = lane.Y + lane.Height / 2, amp = lane.Height * 0.42f;
        long clipStart = tr.Offset, clipEnd = tr.Offset + tr.UsedLength;
        float x0 = lane.X + (float)((clipStart - scroll) / spp), x1 = lane.X + (float)((clipEnd - scroll) / spp);
        var cr = RectangleF.Intersect(Rc(x0, lane.Y + 5, x1 - x0, lane.Height - 10), lane);
        if (cr.Width > 0)
        {
            int ix = (int)Math.Floor(lane.X), iy = (int)Math.Floor(lane.Y), iw = Math.Max(1, (int)Math.Ceiling(lane.Right) - ix), ih = Math.Max(1, (int)Math.Ceiling(lane.Bottom) - iy);
            if (pc.Img == null || pc.Img.Width != iw || pc.Img.Height != ih || pc.ImgVer != pc.Ver || pc.ImgLane != lane || pc.ImgCr != cr || pc.ImgAud != audible || pc.ImgSel != sel || pc.ImgCol != c.ToArgb())
            {
                if (pc.Img == null || pc.Img.Width != iw || pc.Img.Height != ih) { if (pc.Img != null) pc.Img.Dispose(); pc.Img = new Bitmap(iw, ih, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); }
                using (var ig = Graphics.FromImage(pc.Img)) { ig.Clear(Color.Transparent); ig.SmoothingMode = SmoothingMode.AntiAlias; ig.TranslateTransform(-ix, -iy); PaintWaveBody(ig, lane, cr, c, audible, sel, cy, amp, px, wmn, wmx); }
                pc.ImgVer = pc.Ver; pc.ImgLane = lane; pc.ImgCr = cr; pc.ImgAud = audible; pc.ImgSel = sel; pc.ImgCol = c.ToArgb();
            }
            var gi = g.Save(); g.SetClip(lane, CombineMode.Intersect); g.DrawImageUnscaled(pc.Img, ix, iy); g.Restore(gi);
        }
        if (cr.Width > 0) { var gst = g.Save(); g.SetClip(lane, CombineMode.Intersect); float lv = Math.Max(tr.PeakL, tr.PeakR); Glow(g, c, cr, 6, (sel ? 1.2f : 0.8f) + Math.Min(0.6f, lv * 0.9f), 5); g.Restore(gst); }
        Txt(g, tr.Name, 12, Color.FromArgb(200, 255, 255, 255), Rc(Math.Max(lane.X, x0) + 8, lane.Y + 6, 300, 16), StringAlignment.Near, true);
        PaintClipChrome(g, tr, lane, cr, sel);
    }

    float[] Hold(Track t) { float[] h; if (!hold.TryGetValue(t, out h)) { h = new float[4]; hold[t] = h; } return h; }

    void PaintHeader(Graphics g, Track tr, int i, RectangleF r, bool sel)
    {
        bool compact = r.Height < 112, bus = tr.IsBus; float by = compact ? 36 : 44, vy = compact ? 64 : 84, py = compact ? 86 : 110;
        using (var br = new SolidBrush(sel ? Color.FromArgb(255, 40, 22, 74) : Color.FromArgb(255, 26, 15, 46))) g.FillRectangle(br, r);
        using (var pen = new Pen(sel ? Color.FromArgb(200, Neon(tr.Color)) : Color.FromArgb(70, Neon(tr.Color)), sel ? 1.6f : 1f)) g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        Led(g, Neon(tr.Color), r.X + 20, r.Y + 17, 5, !tr.Mute);
        Txt(g, tr.Name, 15, Ink, Rc(r.X + 28, r.Y + 6, 150, 22), StringAlignment.Near, true);
        string role = bus ? "BUS" : (tr.Instrument ? "SYNTH" : (tr.Role == "vocal" ? "VOCAL" : (tr.Role == "beat" ? "BEAT" : (tr.Role == "song" ? "SONG" : "AUDIO"))));
        var pill = Rc(r.X + 186, r.Y + 8, 52, 18); FillRR(g, Color.FromArgb(90, Neon(tr.Color)), pill, 9); StrokeRR(g, Color.FromArgb(200, Neon(tr.Color)), pill, 9, 1); Txt(g, role, 10, Color.White, pill, StringAlignment.Center, true);
        int ti = i; var tk = tr;
        Btn(g, Rc(r.Right - 34, r.Y + 7, 22, 22), "x", 3, () => RemoveTrack(tk), 13);
        var nameReg = Reg(Rc(r.X + 4, r.Y + 4, 176, 26), () => HeaderDown(tk)); nameReg.OnDrag = pt => HeaderDrag(tk, pt); nameReg.OnDouble = () => RenameTrack(tk); nameReg.OnRight = () => RenameTrack(tk);
        if (Hot(Rc(r.X + 4, r.Y + 4, 176, 26))) bubble = "Drag up or down to reorder.  Double-click to rename."; bubblePt = MousePos;
        // M / S, + Add Effect, Out
        float bh = 30;
        Btn(g, Rc(r.X + 12, r.Y + by, 36, bh), "M", tk.Mute ? 4 : 0, () => ToggleMute(tk), 14);
        if (!bus) Btn(g, Rc(r.X + 54, r.Y + by, 36, bh), "S", tk.Solo ? 4 : 0, () => ToggleSolo(tk), 14);
        float ex = bus ? 54 : 96, ew = bus ? 154 : (tk.Instrument ? 74 : 112);
        Btn(g, Rc(r.X + ex, r.Y + by, ew, bh), (tk.Instrument || bus ? "+ Effect" : "+ Add Effect") + (tk.Fx.Count > 0 && !tk.Instrument ? " (" + tk.Fx.Count + ")" : ""), 1, () => OpenBrowser(false, ti), 12);
        if (!bus)
        {
            string outTxt = tk.Out == null ? "Bus" : tk.Out.Name;
            var ob = Rc(r.X + 212, r.Y + by, 48, bh); Btn(g, ob, outTxt, tk.Out == null ? 0 : 4, () => OutMenu(tk), 11);
            using (var tb = new SolidBrush(Color.FromArgb(200, 215, 200, 250))) g.FillPolygon(tb, new[] { new PointF(ob.Right - 11, ob.Bottom - 10), new PointF(ob.Right - 3, ob.Bottom - 10), new PointF(ob.Right - 7, ob.Bottom - 5) });
            if (Hot(ob)) { bubble = tk.Out == null ? "No bus. Click to pick a bus for this track (or make a new one)." : "Output: " + tk.Out.Name + ". Click to change."; bubblePt = MousePos; }
        }
        if (tk.Instrument) Btn(g, Rc(r.X + 176, r.Y + by, 32, bh), "C", 0, () => { if (MessageBox.Show(this, "Delete every recorded note on \"" + tk.Name + "\"?", "Clear notes", MessageBoxButtons.YesNo) == DialogResult.Yes) lock (tk.Midi) tk.Midi.Clear(); }, 12);
        // fader
        float frac = Clamp01((tk.VolDb + 48f) / 54f);
        Txt(g, "Vol", 11, DimC, Rc(r.X + 12, r.Y + vy, 30, 18), StringAlignment.Near, false);
        var fs = Slider(g, Rc(r.X + 44, r.Y + vy, 130, 18), frac, f => tk.VolDb = f < 0.003f ? -48f : (float)Math.Round(f * 54f - 48f, 1), Accent2);
        fs.OnDouble = () => tk.VolDb = 0;
        Txt(g, (tk.VolDb <= -47.9f ? "-inf" : tk.VolDb.ToString("+0.0;-0.0;0.0")) + " dB", 12, Ink, Rc(r.X + 182, r.Y + vy, 70, 18), StringAlignment.Near, false);
        ValueEdit(Rc(r.X + 182, r.Y + vy, 70, 18), tk.VolDb <= -47.9f ? "-inf" : Num(tk.VolDb), s => { float v; if (ParseNum(s, out v)) tk.VolDb = v <= -47.9f ? -48f : Math.Min(12f, v); });
        // pan
        Txt(g, "Pan", 11, DimC, Rc(r.X + 12, r.Y + py, 30, 18), StringAlignment.Near, false);
        var ps = Slider(g, Rc(r.X + 44, r.Y + py, 130, 18), (tk.Pan + 1) / 2, f => tk.Pan = (float)Math.Round(f * 2 - 1, 2));
        ps.OnDouble = () => tk.Pan = 0;
        Txt(g, tk.Pan == 0 ? "C" : (tk.Pan < 0 ? "L" + (int)(-tk.Pan * 100) : "R" + (int)(tk.Pan * 100)), 12, Ink, Rc(r.X + 182, r.Y + py, 34, 18), StringAlignment.Near, false);
        ValueEdit(Rc(r.X + 182, r.Y + py, 34, 18), Num(tk.Pan * 100), s => { string u = s.Trim().ToUpperInvariant(); float v; if (u.StartsWith("C")) tk.Pan = 0; else if (ParseNum(u, out v)) { if (u.StartsWith("L")) v = -Math.Abs(v); else if (u.StartsWith("R")) v = Math.Abs(v); tk.Pan = Math.Max(-1f, Math.Min(1f, v / 100f)); } });
        // right of the pan row: piano (instrument), arm (empty audio track), nudge (audio), nothing (bus)
        if (tk.Instrument)
            Btn(g, Rc(r.X + 214, r.Y + py - 3, 44, 24), "Piano", 1, () => { var vf = tk.Fx.Count > 0 ? tk.Fx[0] as InstrumentFx : null; if (vf != null) { vf.Owner = tk; vf.ShowWindow(this); } }, 11);
        else if (!bus && tk.Clip != null)
        {
            Btn(g, Rc(r.X + 216, r.Y + py - 1, 20, 22), "<", 0, () => tk.Offset -= (ModifierKeys & Keys.Shift) != 0 ? 44 : 441, 12);
            Btn(g, Rc(r.X + 238, r.Y + py - 1, 20, 22), ">", 0, () => tk.Offset += (ModifierKeys & Keys.Shift) != 0 ? 44 : 441, 12);
        }
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
            long offMs = tr.Offset * 1000 / StudioEngine.SR; Txt(g, "offset " + offMs + " ms", 10, DimC, Rc(r.X + 182, r.Y + r.Height - 20, 76, 14), StringAlignment.Near, false);
            ValueEdit(Rc(r.X + 182, r.Y + r.Height - 22, 76, 16), offMs.ToString(), s => { float v; if (ParseNum(s, out v)) tk.Offset = (long)(v * StudioEngine.SR / 1000.0); });
        }
    }

    // choose where a track's sound goes: straight to the master, or to one of the buses (or a new bus)
    void OutMenu(Track tk)
    {
        var m = Win.NewMenu(); var master = new ToolStripMenuItem("No bus (straight to the master)") { Checked = tk.Out == null }; master.Click += (a, b) => { PushUndo(); lock (eng.Sync) tk.Out = null; }; m.Items.Add(master);
        foreach (var b0 in eng.Tracks.ToArray()) if (b0.IsBus) { var bus = b0; var it = new ToolStripMenuItem(bus.Name) { Checked = tk.Out == bus }; it.Click += (a, b) => { PushUndo(); lock (eng.Sync) tk.Out = bus; }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("New bus").Click += (a, b) => { int keep = selTrack; var nb = AddBus(); selTrack = keep; if (nb != null) lock (eng.Sync) tk.Out = nb; };
        m.Items.Add("Edit buses (levels, plugins)...").Click += (a, b) => OpenBusSheet();
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    Track AddBus()
    {
        int n = 1; bool used = true; while (used) { used = false; foreach (var x in eng.Tracks) if (x.IsBus && x.Name == "Bus " + n) { used = true; n++; break; } }
        var t = StudioEngine.NewTrack("Bus " + n, "bus", null); t.IsBus = true; t.Color = unchecked((int)0xFFFFD04D); int keepSel = selTrack; nextInsert = -1; AddTrack(t); selTrack = Math.Max(0, Math.Min(keepSel, LaneCount - 1));
        Toast(t.Name + " added. Pick it from the Bus drop-down on any track. Levels and plugins: + BUS.");
        return t;
    }
    void PaintBusLane(Graphics g, Track tr, RectangleF lane)
    {
        var src = new List<string>(); foreach (var x in eng.Tracks) if (x.Out == tr) src.Add(x.Name);
        Txt(g, "BUS  -  " + (src.Count == 0 ? "nothing is sent here yet. Press Out on a track to send it to this bus." : "receives: " + string.Join(", ", src.ToArray())), 13, DimC, Rc(lane.X + 16, lane.Y + lane.Height / 2 - 12, lane.Width - 32, 24), StringAlignment.Near, false);
    }
    void PaintEmptyLane(Graphics g, Track tr, RectangleF lane)
    {
        Txt(g, "Empty audio track: press MIC to record onto it, or drop an audio file here.", 13, tr.Armed ? Color.FromArgb(255, 150, 160) : DimC, Rc(lane.X + 16, lane.Y + lane.Height / 2 - 12, lane.Width - 32, 24), StringAlignment.Near, false);
    }

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
        float x = ClientSize.Width - PanelW, y = ToolH + TransH, w = PanelW, h = ClientSize.Height - y;
        var pr = Rc(x, y, w, h); using (var br = new SolidBrush(Color.FromArgb(248, 20, 11, 38))) g.FillRectangle(br, pr);
        using (var pen = new Pen(Edge)) g.DrawLine(pen, x, y, x, y + h);
        Reg(pr, () => { });
        if (eng.Tracks.Count == 0) { Txt(g, "Select a track to edit its plugins", 14, DimC, pr, StringAlignment.Center, false); return; }
        var tr = eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))];
        Txt(g, "Plugins: " + tr.Name, 16, Ink, Rc(x + 14, y + 8, w - 28, 26), StringAlignment.Near, true);
        Fx[] fxs; lock (eng.Sync) fxs = tr.Fx.ToArray();
        float ly = y + 40 - panelScroll; int n = fxs.Length; g.SetClip(Rc(x, y + 36, w, h - 36));
        if (selFx >= n) selFx = -1;
        for (int i = 0; i < n; i++, ly += 32)
        {
            var f = fxs[i]; int fi = i; bool sel = i == selFx;
            var row = Rc(x + 10, ly, w - 20, 28); FillRR(g, sel ? Color.FromArgb(200, 80, 40, 150) : Color.FromArgb(150, 40, 22, 74), row, 6); if (sel) StrokeRR(g, Accent, row, 6, 1.2f);
            Reg(row, () => selFx = fi);
            var dotr = Rc(row.X + 8, row.Y + 7, 14, 14); using (var b = new SolidBrush(f.Bypass ? Color.FromArgb(90, 90, 70, 110) : Good)) g.FillEllipse(b, dotr);
            Reg(dotr, () => { f.Bypass = !f.Bypass; });
            Txt(g, f.Title, 14, f.Bypass ? DimC : Ink, Rc(row.X + 30, row.Y, row.Width - 150, 28), StringAlignment.Near, sel);
            if (f.GR > 0.2f) Txt(g, "-" + f.GR.ToString("0.0"), 11, Warn, Rc(row.Right - 120, row.Y, 40, 28), StringAlignment.Far, false);
            Btn(g, Rc(row.Right - 76, row.Y + 3, 22, 22), "^", 0, () => MoveFx(tr, fi, -1), 11);
            Btn(g, Rc(row.Right - 52, row.Y + 3, 22, 22), "v", 0, () => MoveFx(tr, fi, 1), 11);
            Btn(g, Rc(row.Right - 28, row.Y + 3, 22, 22), "x", 3, () => { lock (eng.Sync) { if (fi < tr.Fx.Count) { var rf = tr.Fx[fi]; tr.Fx.RemoveAt(fi); if (rf is IDisposable) { try { ((IDisposable)rf).Dispose(); } catch { } } } } selFx = -1; }, 12);
        }
        int ti = Array.IndexOf(eng.Tracks.ToArray(), tr);
        Btn(g, Rc(x + 10, ly + 2, 150, 30), "+ Add Effect", 1, () => OpenBrowser(false, ti), 14);
        Btn(g, Rc(x + 168, ly + 2, 200, 30), "Chain presets...", 0, () => ChainMenu(tr), 14);
        float ey = ly + 42;
        float edH = (selFx >= 0 && selFx < n) ? EditorHeight(fxs[selFx]) : 60;
        if (selFx >= 0 && selFx < n) PaintEditor(g, fxs[selFx], Rc(x + 6, ey, w - 12, edH), tr);
        else Txt(g, "Click a plugin to edit it", 13, DimC, Rc(x, ey, w, 40), StringAlignment.Center, false);
        g.ResetClip();
        float contentBottom = ly + panelScroll + 42 + ((selFx >= 0 && selFx < n) ? edH : 60), maxScroll = Math.Max(0, contentBottom - (y + h) + 6); panelScroll = Math.Min(panelScroll, maxScroll);
        regs.Insert(0, new HitReg { R = Rc(x, y + 36, w, h - 36), OnWheel = d => panelScroll = Math.Max(0, Math.Min(maxScroll, panelScroll - d * 50)) });
    }

    // Exact height of an effect editor, so the panel scrolls only as far as its content (no endless blank space).
    static float EditorHeight(Fx f)
    {
        int n = 0; { var hid = f.Hidden.Length > 0 ? new List<string>(f.Hidden.Split(',')) : new List<string>(); foreach (var p in f.P) if (!hid.Contains(p.Id)) n++; }
        if (f is ParametricEq) return 640;
        if (f is Spatial8D) return 34 + 190 + Math.Max(0, n - 2) * 30 + 24;
        if (f is ConvFx) return 34 + 52 + n * 30 + 24;
        if (f is PitchCorrector) return 34 + 200 + Math.Max(0, n - 3) * 30 + 24;
        return 34 + n * 30 + 24;
    }

    void MoveFx(Track tr, int i, int d)
    {
        int j = i + d; if (j < 0 || j >= tr.Fx.Count) return; lock (eng.Sync) { if (i >= tr.Fx.Count) return; var f = tr.Fx[i]; tr.Fx.RemoveAt(i); tr.Fx.Insert(j, f); } selFx = j;
    }

    void ChainMenu(Track tr)
    {
        var m = Win.NewMenu(); bool vocal = tr.Role == "vocal";
        var rap = new ToolStripMenuItem("Rap vocal presets (replace chain)"); m.Items.Add(rap);
        for (int i = 0; i < Presets.ChainNames.Length; i++) { string nm = Presets.ChainNames[i]; var it = new ToolStripMenuItem(nm) { ToolTipText = Presets.ChainBlurbs[i] }; it.Click += (a, b) => { lock (eng.Sync) { tr.Fx.Clear(); tr.Fx.AddRange(Presets.BuildChain(nm)); } selFx = -1; Toast(nm + " applied: every plugin is editable"); }; rap.DropDownItems.Add(it); }
        var beat = new ToolStripMenuItem("Instrumental chain (EQ, comp, multiband, sat, exciter, imager, ducker, limiter)"); beat.Click += (a, b) => { lock (eng.Sync) { tr.Fx.Clear(); tr.Fx.AddRange(Presets.BeatChain()); } selFx = -1; Toast("Beat chain applied"); }; m.Items.Add(beat);
        var mine = new ToolStripMenuItem("My saved chains"); m.Items.Add(mine);
        foreach (var nm0 in Presets.UserChains()) { string nm = nm0; var it = new ToolStripMenuItem(nm); it.Click += (a, b) => { var c = Presets.LoadChain(nm); lock (eng.Sync) { tr.Fx.Clear(); tr.Fx.AddRange(c); } selFx = -1; }; mine.DropDownItems.Add(it); }
        if (mine.DropDownItems.Count == 0) mine.DropDownItems.Add(new ToolStripMenuItem("(none yet)") { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Save this track's chain as a preset...").Click += (a, b) => { string nm = Ask("Name for this chain preset:", tr.Name + " chain"); if (nm != null) { Presets.SaveChain(nm, tr.Fx); Toast("Saved chain \"" + nm + "\""); } };
        m.Items.Add("Clear all plugins").Click += (a, b) => { lock (eng.Sync) tr.Fx.Clear(); selFx = -1; };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- effect browser (searchable) ----------------
    internal void OpenBrowser(bool master, int track) { browserOpen = true; browserQuery = ""; browserMaster = master; browserTrack = track; browserScroll = 0; blCache = null; }

    void PaintBrowser(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(170, 6, 3, 14))) g.FillRectangle(br, 0, 0, ClientSize.Width, ClientSize.Height);
        Reg(Rc(0, 0, ClientSize.Width, ClientSize.Height), () => { if (!browserBox.Contains(MousePos)) browserOpen = false; });
        float w = 620, h = Math.Min(560, ClientSize.Height - 80), x = (ClientSize.Width - w) / 2, y = 60; browserBox = Rc(x, y, w, h);
        FillRR(g, Color.FromArgb(252, 26, 14, 48), browserBox, 14); StrokeRR(g, Accent, browserBox, 14, 1.5f); var wr = new HitReg { R = browserBox, OnWheel = d => browserScroll = Math.Max(0, Math.Min(browserMaxF, browserScroll - d * 40)) }; regs.Add(wr); Reg(browserBox, () => { });
        Txt(g, "Add effect", 20, Ink, Rc(x + 20, y + 12, 300, 30), StringAlignment.Near, true);
        Btn(g, Rc(x + w - 60, y + 14, 40, 28), "X", 3, () => browserOpen = false, 14);
        Btn(g, Rc(x + w - 196, y + 14, 130, 28), "Plugins...", 1, () => { browserOpen = false; new PluginManagerForm(() => app.ExtraFilter, s => { app.ExtraFilter = s; app.SaveSettings(); }).Show(this); }, 12);
        var sb = Rc(x + 20, y + 52, w - 40, 36); FillRR(g, Color.FromArgb(235, 14, 7, 32), sb, 8); StrokeRR(g, Color.FromArgb(60, Accent), Rc(sb.X - 2, sb.Y - 2, sb.Width + 4, sb.Height + 4), 10, 4f); StrokeRR(g, Accent, sb, 8, 2f);   // always active: it is ready to type the moment the list opens
        Reg(sb, () => { });
        if (browserQuery.Length > 0) Btn(g, Rc(sb.Right - 32, sb.Y + 6, 24, 24), "x", 3, () => { browserQuery = ""; browserScroll = 0; }, 12);
        Txt(g, browserQuery.Length == 0 ? (((int)(t * 2) % 2 == 0 ? "|" : " ") + "  Search here - just start typing: EQ, reverb, pitch...") : browserQuery + ((int)(t * 2) % 2 == 0 ? "|" : ""), 15, browserQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 12, sb.Y, sb.Width - 50, sb.Height), StringAlignment.Near, false);
        var list = BrowserList();
        { var cb = Rc(x + 20, y + 94, 250, 26); Btn(g, cb, "Category: " + (browserCat.Length == 0 ? "All" : browserCat), browserCat.Length == 0 ? 0 : 4, () => CategoryMenu(), 12); Txt(g, list.Count + (list.Count == 1 ? " effect" : " effects"), 12, DimC, Rc(x + 280, y + 94, 200, 26), StringAlignment.Near, false); }
        float listTop = y + 128;
        float listH = 0; { string lc = ""; foreach (var i2 in list) { if (i2.Category != lc) { lc = i2.Category; listH += 20; } listH += 48; } }
        float browserMax = Math.Max(0, listH - (h - (listTop - y) - 10) + 8); browserMaxF = browserMax; browserScroll = Math.Max(0, Math.Min(browserMax, browserScroll));
        float ry = listTop - browserScroll; var clipR = Rc(x + 10, listTop - 4, w - 20, h - (listTop - y) - 10); g.SetClip(clipR);
        string lastCat = "";
        foreach (var info in list)
        {
            if (info.Category != lastCat) { lastCat = info.Category; if (ry + 18 > clipR.Y && ry < clipR.Bottom) Txt(g, info.Category.ToUpperInvariant(), 11, Accent, Rc(x + 24, ry, 300, 18), StringAlignment.Near, true); ry += 20; }
            if (ry + 44 < clipR.Y || ry > clipR.Bottom) { ry += 48; continue; }   // off screen: neither drawn nor given a click region (it would lie on top of the header)
            var row = Rc(x + 18, ry, w - 36, 44); var vis = RectangleF.Intersect(row, clipR); bool hot = Hot(vis); FillRR(g, hot ? Color.FromArgb(220, 90, 50, 170) : Color.FromArgb(160, 44, 24, 80), row, 8);
            Txt(g, info.Name, 15, Ink, Rc(row.X + 14, row.Y + 3, row.Width - 120, 20), StringAlignment.Near, true);
            Txt(g, info.Blurb, 12, DimC, Rc(row.X + 14, row.Y + 22, row.Width - 120, 18), StringAlignment.Near, false);
            string type = info.Type; if (row.Y + 8 >= clipR.Y && row.Y + 36 <= clipR.Bottom) Btn(g, Rc(row.Right - 86, row.Y + 8, 72, 28), "Add", 1, () => AddFx(type), 13);
            if (vis.Width > 0 && vis.Height > 0) Reg(vis, () => AddFx(type)); ry += 48;
        }
        if (list.Count == 0) Txt(g, "No plugin matches \"" + browserQuery + "\"", 14, DimC, Rc(x, y + 140, w, 30), StringAlignment.Center, false);
        g.ResetClip();
        Txt(g, "Enter adds the first match  -  Esc closes  -  your plugins are listed first", 11, DimC, Rc(x + 20, y + h - 22, w - 40, 18), StringAlignment.Near, false);
    }
    RectangleF browserBox; float browserMaxF; string browserCat = "";

    // the effects that match the search and category, grouped by category (your own plugins first)
    List<FxRegistry.Info> blCache; string blKey = "";
    List<FxRegistry.Info> BrowserList()
    {
        string key = browserQuery + "\n" + browserCat; if (blCache != null && key == blKey) return blCache;
        var all = FxRegistry.All; var list = new List<FxRegistry.Info>(); string q = browserQuery.ToLowerInvariant().Trim();
        foreach (var i in all) { if (i.Type == "gain") continue; if (browserCat.Length > 0 && i.Category != browserCat) continue; if (q.Length == 0 || i.Name.ToLowerInvariant().Contains(q) || i.Category.ToLowerInvariant().Contains(q) || i.Blurb.ToLowerInvariant().Contains(q) || i.Type.Contains(q)) list.Add(i); }
        var order = new List<string>(); order.Add("Plugins"); foreach (var i in all) if (!order.Contains(i.Category)) order.Add(i.Category);
        var res = new List<FxRegistry.Info>(); foreach (var c in order) foreach (var i in list) if (i.Category == c) res.Add(i); blCache = res; blKey = key; return res;
    }
    void CategoryMenu()
    {
        var m = Win.NewMenu(); var it0 = new ToolStripMenuItem("All effects") { Checked = browserCat.Length == 0 }; it0.Click += (a, b) => { browserCat = ""; browserScroll = 0; }; m.Items.Add(it0); m.Items.Add(new ToolStripSeparator());
        var cats = new List<string>(); var counts = new Dictionary<string, int>(); foreach (var i in FxRegistry.All) { if (i.Type == "gain") continue; if (!counts.ContainsKey(i.Category)) { counts[i.Category] = 0; cats.Add(i.Category); } counts[i.Category]++; }
        foreach (var c in cats) { string cc = c; var it = new ToolStripMenuItem(c + "  (" + counts[c] + ")") { Checked = browserCat == c }; it.Click += (a, b) => { browserCat = cc; browserScroll = 0; }; m.Items.Add(it); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    void AddFx(string type)
    {
        var f = FxRegistry.Create(type); if (f == null) return; f.Prepare(StudioEngine.SR);
        if (type == "duck") { var v = eng.VocalTrack(); if (v == null) Toast("The ducker listens to a vocal track; add or separate a vocal first."); }
        if (browserMaster) { lock (eng.Sync) eng.MasterFx.Add(f); selMaster = eng.MasterFx.Count - 1; }
        else if (browserTrack >= 0 && browserTrack < eng.Tracks.Count) { var tr = eng.Tracks[browserTrack]; var vfx = f as InstrumentFx; if (vfx != null) vfx.Owner = tr; lock (eng.Sync) tr.Fx.Add(f); selTrack = browserTrack; selFx = tr.Fx.Count - 1; }
        browserOpen = false; Toast(f.Title + " added");
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e); lastInput = DateTime.Now;
        if (browserOpen) { if (e.KeyChar == '\b') { if (browserQuery.Length > 0) browserQuery = browserQuery.Substring(0, browserQuery.Length - 1); } else if (!char.IsControl(e.KeyChar)) browserQuery += e.KeyChar; e.Handled = true; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        lastInput = DateTime.Now;
        if (editBox != null && editBox.Visible) return base.ProcessCmdKey(ref msg, k);
        if (ActiveControl is TextBoxBase && ((Control)ActiveControl).Visible) return base.ProcessCmdKey(ref msg, k);   // typing in the lyrics page: every key belongs to the text box
        if (busOpen && k == Keys.Escape) { busOpen = false; return true; }
        if (ntOpen && k == Keys.Escape) { CloseNewTrackSheet(); return true; }
        if (menuOpen && k == Keys.Escape) { CloseMenu(); return true; }
        if (flexOpen && !menuOpen && !browserOpen && FlexKey(k)) return true;
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
        if (k == Keys.F1) { app.ShowHelp(null); return true; }
        if (k == Keys.Space) { if (eng.Playing) eng.Pause(); else PlayGuard(); return true; }
        if (k == Keys.Home) { eng.Seek(0); scroll = 0; return true; }
        if (k == (Keys.Control | Keys.Shift | Keys.S)) { SaveClick(true); return true; }
        if (k == Keys.F2 && selTrack < eng.Tracks.Count) { RenameTrack(eng.Tracks[selTrack]); return true; }
        if (k == (Keys.Control | Keys.Z)) { DoUndo(); return true; }
        if (k == (Keys.Control | Keys.Y) || k == (Keys.Control | Keys.Shift | Keys.Z)) { DoRedo(); return true; }
        if (k == Keys.B) { SplitSelected(); return true; }
        if (k == Keys.Delete && Selected != null && Selected.Clip != null) { DeleteTrackQuiet(Selected); return true; }
        if (k == (Keys.Control | Keys.S)) { SaveClick(false); return true; }
        if (k == (Keys.Control | Keys.O)) { OpenClick(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---------------- mouse ----------------
    HitReg TopRegion(PointF p) { for (int i = regs.Count - 1; i >= 0; i--) if (regs[i].R.Contains(p)) return regs[i]; return null; }

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
        base.OnMouseDown(e); lastInput = DateTime.Now; Focus(); MousePos = e.Location; if (e.Button == MouseButtons.Middle && tab == 0 && TimelineRect().Contains(e.Location)) { MiddleDown(e.Location); return; }
        var r = TopRegion(e.Location); if (r == null) return;
        if (busy && !r.WhileBusy) return;   // while a job runs only the Cancel button works
        if (e.Button == MouseButtons.Right) { if (r.OnRight != null) r.OnRight(); return; }
        if (e.Clicks == 2 && r.OnDouble != null) { r.OnDouble(); return; }
        if (r.OnDown != null) r.OnDown();
        if (r.OnDrag != null) dragReg = r;
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); lastInput = DateTime.Now; if (dragReg != null && dragReg.OnUp != null) { try { dragReg.OnUp(); } catch { } } dragReg = null; zoomAnchor = 0; if (sndDrag != null) { if (sndDragging) DropSampleAt(e.Location); sndDrag = null; sndDragging = false; } if (panning) { panning = false; Cursor = Cursors.Default; } }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e); MousePos = e.Location; lastInput = DateTime.Now; for (int i = regs.Count - 1; i >= 0; i--) if (regs[i].R.Contains(e.Location) && regs[i].OnWheel != null) { regs[i].OnWheel(e.Delta > 0 ? 1 : -1); return; }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; }

    // ---------------- editors ----------------
    static string ParamName(Fx f, Param p) { return p.Name; }
    static bool LogParam(Param p) { return p.Unit == "Hz" && p.Min > 0 && p.Max / p.Min >= 8; }
    static float PFrac(Param p) { if (LogParam(p)) return (float)(Math.Log(p.Val / p.Min) / Math.Log(p.Max / p.Min)); return (p.Val - p.Min) / (p.Max - p.Min); }
    static float PFromFrac(Param p, float f) { if (LogParam(p)) return (float)(p.Min * Math.Pow(p.Max / p.Min, f)); return p.Min + f * (p.Max - p.Min); }

    void PaintEditor(Graphics g, Fx f, RectangleF a, Track tr)
    {
        FillRR(g, Color.FromArgb(120, 34, 18, 62), a, 10);
        float x = a.X + 10, y = a.Y + 8, w = a.Width - 20;
        Txt(g, f.Title, 16, Ink, Rc(x, y, w - 150, 24), StringAlignment.Near, true);
        Btn(g, Rc(a.Right - 150, y, 66, 24), f.Bypass ? "Bypassed" : "Bypass", f.Bypass ? 4 : 0, () => f.Bypass = !f.Bypass, 11);
        Btn(g, Rc(a.Right - 80, y, 70, 24), "Presets", 1, () => PresetMenu(f), 12);
        y += 34;
        var vstf = f as InstrumentFx; if (vstf != null) { Btn(g, Rc(a.X + 10, y - 2, a.Width - 20, 32), vstf.WindowLabel, 2, () => { vstf.Owner = tr; vstf.ShowWindow(this); }, 14); y += 38; }
        var skip = new List<string>(); if (f.Hidden.Length > 0) skip.AddRange(f.Hidden.Split(','));
        if (f is ParametricEq) { PaintEqEditor(g, (ParametricEq)f, Rc(a.X + 6, y, a.Width - 12, a.Bottom - y - 4), tr == null ? null : tr.TapPost, tr == null ? 0 : tr.TapPos); return; }
        var cvx = f as ConvFx; if (cvx != null) { var lb = Rc(a.X + 10, y - 2, a.Width - 20, 30); Btn(g, lb, "Load your own impulse response...", 2, () => { using (var d = new OpenFileDialog { Title = "Choose an impulse response (a recording of a space or speaker)", Filter = "Audio|*.wav;*.mp3;*.flac;*.ogg;*.aiff|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) cvx.UserPath = d.FileName; }, 12); y += 34; Txt(g, cvx.IrNote.Length > 0 ? "Using: " + cvx.IrNote : "", 11, DimC, Rc(a.X + 14, y - 4, a.Width - 28, 16), StringAlignment.Near, false); y += 18; }
        if (f is Spatial8D) { y = Paint8dEditor(g, (Spatial8D)f, Rc(a.X + 6, y, a.Width - 12, 190)); skip.AddRange(new[] { "hold", "angle" }); }
        if (f is PitchCorrector) { y = PaintPitchEditor(g, (PitchCorrector)f, tr, Rc(a.X + 6, y, a.Width - 12, 200)); skip.AddRange(new[] { "key", "scale", "mask" }); }
        PaintParams(g, f, Rc(a.X + 6, y, a.Width - 12, a.Bottom - y - 4), skip);
    }

    void PaintParams(Graphics g, Fx f, RectangleF a, List<string> skip)
    {
        var ps = new List<Param>(); foreach (var p in f.P) if (!skip.Contains(p.Id)) ps.Add(p);
        float rowH = 30, total = ps.Count * rowH; float maxScroll = Math.Max(0, total - a.Height);
        genScroll = Math.Max(0, Math.Min(maxScroll, genScroll));
        g.SetClip(a); float y = a.Y - genScroll;
        foreach (var p in ps)
        {
            var row = Rc(a.X, y, a.Width, rowH); y += rowH; if (row.Bottom < a.Y || row.Y > a.Bottom) continue;
            Txt(g, p.Name, 13, DimC, Rc(row.X + 6, row.Y, row.Width * 0.38f, rowH), StringAlignment.Near, false);
            var pp = p;
            if (p.IsChoice)
            {
                var br = Rc(row.X + row.Width * 0.40f, row.Y + 3, row.Width * 0.60f - 6, rowH - 6);
                Btn(g, br, p.Text, 0, () => { pp.Val = (pp.Val + 1) > pp.Max ? pp.Min : pp.Val + 1; f.MarkDirty(); }, 12);
                var rg = regs[regs.Count - 1]; rg.OnRight = () => { pp.Val = (pp.Val - 1) < pp.Min ? pp.Max : pp.Val - 1; f.MarkDirty(); };
            }
            else
            {
                var sr = Rc(row.X + row.Width * 0.40f, row.Y + 4, row.Width * 0.60f - 70, rowH - 8);
                var rg = Slider(g, sr, PFrac(p), fr => { f.Set(pp.Id, PFromFrac(pp, fr)); }); rg.OnDouble = () => f.Set(pp.Id, pp.Def);
                rg.OnWheel = d => f.Set(pp.Id, pp.Val + d * (pp.Max - pp.Min) / 100f);
                Txt(g, p.Text, 12, Ink, Rc(sr.Right + 6, row.Y, 66, rowH), StringAlignment.Near, false);
                ValueEdit(Rc(sr.Right + 6, row.Y, 66, rowH), Num(p.Val), s => { float v; if (ParseNum(s, out v)) f.Set(pp.Id, v); });
            }
        }
        g.ResetClip();
        if (maxScroll > 0) { var wr = new HitReg { R = a, OnWheel = d => genScroll = Math.Max(0, Math.Min(maxScroll, genScroll - d * 30)) }; regs.Insert(0, wr); }
    }

    void PresetMenu(Fx f)
    {
        var m = Win.NewMenu();
        foreach (var kv in Presets.For(f.Type)) { string data = kv.Value; m.Items.Add(kv.Key).Click += (a, b) => Presets.Apply(f, data); }
        var user = Presets.User(f.Type);
        if (user.Count > 0) { m.Items.Add(new ToolStripSeparator()); foreach (var kv in user) { string data = kv.Value; string nm = kv.Key; var it = new ToolStripMenuItem("My: " + nm); it.Click += (a, b) => Presets.Apply(f, data); it.DropDownItems.Add("Delete").Click += (a, b) => Presets.DeleteUser(f.Type, nm); m.Items.Add(it); } }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Save current settings as preset...").Click += (a, b) => { string nm = Ask("Preset name:", f.Title + " custom"); if (nm != null) { Presets.SaveUser(f.Type, nm, f); Toast("Preset saved"); } };
        m.Items.Add("Reset to defaults").Click += (a, b) => { lock (eng.Sync) { f.ResetDefaults(); f.Reset(); } };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }

    // ---------------- spectrum ----------------
    float[] specSrc; int specSrcPos; static double[] hann; int eqPaintTick = -100000;
    void UpdateSpectrum()
    {
        if (!eng.Playing) { for (int k = 0; k <= 2048; k++) spec[k] = spec[k] * 0.9 - 12; return; }   // nothing to analyse: just let the picture fall away
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
    }

    // ---------------- EQ editor ----------------
    static readonly Color[] BandCol = { Color.FromArgb(255, 120, 120), Color.FromArgb(255, 180, 90), Color.FromArgb(240, 230, 100), Color.FromArgb(120, 230, 150), Color.FromArgb(100, 200, 255), Color.FromArgb(200, 130, 255) };

    void PaintEqEditor(Graphics g, ParametricEq eq, RectangleF a, float[] tap, int tapPos)
    {
        eqPaintTick = Environment.TickCount;
        var plot = Rc(a.X + 28, a.Y + 4, a.Width - 36, Math.Min(230, a.Height * 0.52f));
        FillRR(g, Color.FromArgb(220, 8, 4, 20), plot, 8); StrokeRR(g, Edge, plot, 8, 1);
        Func<double, float> FX = f => plot.X + plot.Width * (float)(Math.Log10(f / 20.0) / 3.0);
        Func<double, float> GY = db => plot.Y + plot.Height * (float)((18 - db) / 36.0);
        // grid
        using (var pen = new Pen(Color.FromArgb(40, 190, 140, 255)))
        {
            foreach (double f in new double[] { 50, 100, 200, 500, 1000, 2000, 5000, 10000 }) g.DrawLine(pen, FX(f), plot.Y, FX(f), plot.Bottom);
            foreach (int d in new[] { -12, -6, 0, 6, 12 }) g.DrawLine(pen, plot.X, GY(d), plot.Right, GY(d));
        }
        foreach (var kv in new[] { new KeyValuePair<double, string>(100, "100"), new KeyValuePair<double, string>(1000, "1k"), new KeyValuePair<double, string>(10000, "10k") }) Txt(g, kv.Value, 10, DimC, Rc(FX(kv.Key) - 14, plot.Bottom - 14, 30, 12), StringAlignment.Center, false);
        foreach (int d in new[] { -12, 0, 12 }) Txt(g, d.ToString(), 10, DimC, Rc(a.X, GY(d) - 7, 26, 14), StringAlignment.Far, false);
        // live spectrum (post-EQ of this track)
        var pts = new List<PointF>(); pts.Add(new PointF(plot.X, plot.Bottom));
        for (int px = 0; px <= (int)plot.Width; px += 2)
        {
            double f = 20 * Math.Pow(1000, px / plot.Width); int k = Math.Min(2048, Math.Max(1, (int)(f * 4096 / StudioEngine.SR))); double db = spec[k];
            pts.Add(new PointF(plot.X + px, plot.Bottom - (float)DspMath.Clamp((db + 90) / 80.0, 0, 1) * plot.Height * 0.9f));
        }
        pts.Add(new PointF(plot.Right, plot.Bottom));
        g.SetClip(plot);
        using (var br = new SolidBrush(Color.FromArgb(70, 150, 90, 255))) g.FillPolygon(br, pts.ToArray());
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
        g.ResetClip();
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
            rg.OnWheel = d => eq.Set("b" + bi + "_q", eq.Get("b" + bi + "_q") * (d > 0 ? 1.12f : 0.89f));
            rg.OnRight = () => eq.Set("b" + bi + "_on", 0); rg.OnDouble = () => eq.Set("b" + bi + "_g", 0);
        }
        Txt(g, "Drag a dot: frequency + gain.  Wheel: Q.  Right-click: remove.", 10, DimC, Rc(plot.X, plot.Bottom + 1, plot.Width, 14), StringAlignment.Near, false);
        // band chips
        float cy2 = plot.Bottom + 20; float cw = (a.Width - 8) / 6f;
        for (int b = 0; b < ParametricEq.Bands; b++)
        {
            int bi = b; bool on = eq.Get("b" + b + "_on") > 0.5f; var chip = Rc(a.X + 4 + b * cw, cy2, cw - 4, 26);
            FillRR(g, on ? Color.FromArgb(200, BandCol[b].R / 2, BandCol[b].G / 2, BandCol[b].B / 2) : Color.FromArgb(120, 40, 24, 70), chip, 6); if (b == eqBand) StrokeRR(g, BandCol[b], chip, 6, 1.6f);
            Txt(g, "Band " + (b + 1), 12, on ? Color.White : DimC, chip, StringAlignment.Center, b == eqBand);
            Reg(chip, () => { if (eqBand == bi) eq.Set("b" + bi + "_on", on ? 0 : 1); else eqBand = bi; if (!on) eq.Set("b" + bi + "_on", 1); });
        }
        // selected band controls
        float y = cy2 + 34; int s = eqBand; string pfx = "b" + s + "_";
        var tb = Rc(a.X + 4, y, 118, 26); Btn(g, tb, eq.Find(pfx + "t").Text, 0, () => eq.Set(pfx + "t", (eq.Get(pfx + "t") + 1) % 4), 12); y += 32;
        foreach (var id in new[] { "f", "g", "q" })
        {
            var p = eq.Find(pfx + id); var pp = p; Txt(g, id == "f" ? "Freq" : (id == "g" ? "Gain" : "Q"), 12, DimC, Rc(a.X + 6, y, 40, 24), StringAlignment.Near, false);
            var sr = Rc(a.X + 48, y + 2, a.Width - 150, 20); var rg = Slider(g, sr, PFrac(p), fr => eq.Set(pp.Id, PFromFrac(pp, fr)), BandCol[s]); rg.OnDouble = () => eq.Set(pp.Id, pp.Def);
            Txt(g, p.Text, 12, Ink, Rc(sr.Right + 8, y, 90, 24), StringAlignment.Near, false);
            ValueEdit(Rc(sr.Right + 8, y, 90, 24), Num(p.Val), str => { float v; if (ParseNum(str, out v)) eq.Set(pp.Id, v); }); y += 28;
        }
        // HP / LP
        foreach (var kind in new[] { "hp", "lp" })
        {
            string k = kind; bool on = eq.Get(k + "_on") > 0.5f;
            Btn(g, Rc(a.X + 4, y, 70, 24), kind == "hp" ? "High-pass" : "Low-pass", on ? 4 : 0, () => eq.Set(k + "_on", on ? 0 : 1), 12);
            var p = eq.Find(k + "_f"); var sr = Rc(a.X + 80, y + 2, a.Width - 200, 20); var rg = Slider(g, sr, PFrac(p), fr => eq.Set(k + "_f", PFromFrac(p, fr)), Accent); rg.OnDouble = () => eq.Set(k + "_f", p.Def);
            Txt(g, p.Text, 12, Ink, Rc(sr.Right + 6, y, 60, 24), StringAlignment.Near, false);
            ValueEdit(Rc(sr.Right + 6, y, 60, 24), Num(p.Val), str => { float v; if (ParseNum(str, out v)) eq.Set(k + "_f", v); });
            Btn(g, Rc(a.Right - 52, y, 48, 24), eq.Find(k + "_s").Text, 0, () => eq.Set(k + "_s", eq.Get(k + "_s") > 0.5f ? 0 : 1), 11); y += 28;
        }
    }

    // ---------------- pitch correction editor ----------------
    float PaintPitchEditor(Graphics g, PitchCorrector pc, Track tr, RectangleF a)
    {
        float x = a.X + 4, y = a.Y, w = a.Width - 8;
        int key = (int)pc.Get("key"), scale = (int)pc.Get("scale"); int mask = (int)pc.Get("mask") & 4095;
        Btn(g, Rc(x, y, 64, 28), "Key " + PitchCorrector.Keys[key], 0, () => { var m = Win.NewMenu(); for (int k = 0; k < 12; k++) { int kk = k; m.Items.Add(PitchCorrector.Keys[k]).Click += (s, e) => { pc.Set("key", kk); pc.ApplyScale(); }; } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 12);
        Btn(g, Rc(x + 70, y, w - 70 - 100, 28), PitchCorrector.Scales[scale], 0, () => { var m = Win.NewMenu(); for (int k = 0; k < PitchCorrector.Scales.Length; k++) { int kk = k; m.Items.Add(PitchCorrector.Scales[k]).Click += (s, e) => { pc.Set("scale", kk); pc.ApplyScale(); }; } m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y)); }, 12);
        Btn(g, Rc(x + w - 94, y, 94, 28), "Detect key", 1, () => DetectKey(pc, tr), 12);
        y += 34; float cw = w / 12f;
        for (int n = 0; n < 12; n++)
        {
            int nn = n; bool on = (mask & (1 << n)) != 0; bool black = n == 1 || n == 3 || n == 6 || n == 8 || n == 10; var c = Rc(x + n * cw, y, cw - 3, 30);
            FillRR(g, on ? (black ? Color.FromArgb(220, 150, 70, 230) : Color.FromArgb(230, 190, 120, 255)) : Color.FromArgb(120, 40, 24, 70), c, 5);
            Txt(g, PitchCorrector.Keys[n], 11, on ? Color.Black : DimC, c, StringAlignment.Center, true);
            Reg(c, () => { pc.Set("mask", mask ^ (1 << nn)); });
        }
        y += 36;
        Btn(g, Rc(x, y, w / 2 - 3, 26), "Natural", 0, () => { pc.Set("speed", 80); pc.Set("strength", 70); pc.Set("natural", 70); }, 12);
        Btn(g, Rc(x + w / 2 + 3, y, w / 2 - 3, 26), "Hard Auto-Tune", 2, () => { pc.Set("speed", 0); pc.Set("strength", 100); pc.Set("natural", 0); }, 12);
        y += 32;
        // live readout
        var lr = Rc(x, y, w, 46); FillRR(g, Color.FromArgb(150, 8, 4, 20), lr, 8);
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

