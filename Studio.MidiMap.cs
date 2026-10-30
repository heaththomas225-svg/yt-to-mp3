using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// The studio's side of MIDI mapping: what each target does, the right-click MIDI Learn on its controls, the banner while it learns,
// the light that shows MIDI coming in, and the MIDI MAPPING panel (every map, controller profiles).
partial class StudioForm
{
    bool mmOpen; float mmScroll;
    void MidiMapInit()
    {
        MidiMap.Load();
        MidiMap.Apply = MidiApply;
        MidiMap.Learned = (label, src) => { try { BeginInvoke(new Action(() => { Toast("MIDI mapped: " + MidiMap.SrcText(src) + " now drives " + label + "."); Invalidate(); })); } catch { } };
    }
    internal void LearnMenu(string target, string label) { MidiMap.Menu(this, new Point((int)MousePos.X, (int)MousePos.Y), target, label); }
    // a reg made for a control gets MIDI Learn on its right-click
    void LearnOn(HitReg r, string target, string label) { if (r != null) r.OnRight = () => LearnMenu(target, label); }
    void LearnOnNew(int fromReg, string target, string label, bool dragOnly) { for (int i = fromReg; i < regs.Count; i++) if (!dragOnly || regs[i].OnDrag != null) LearnOn(regs[i], target, label); }

    Track TrackNamed(string name) { lock (eng.Sync) return eng.Tracks.FirstOrDefault(t => t.Name == name); }
    // from the MIDI thread: pads and Beat Lab rows play at once; everything else is done on the studio's thread
    bool MidiApply(string target, float v, bool cc)
    {
        var p = target.Split('|'); string k = p[0];
        if (k == "pad" && p.Length >= 3)
        {   // a pad of Purphex Pads on that track: hit with the key's strength (a knob or CC button hits it when it goes over half way)
            int pad; int.TryParse(p[2], out pad); var t = TrackNamed(p[1]); var pf = t == null ? null : t.Fx.OfType<PadsFx>().FirstOrDefault(); if (pf == null) return false;
            int note = pf.PadNote(pad); if (cc) { if (MidiMap.Pressed(target, v, true)) pf.NoteOn(note, 110); } else if (v > 0) pf.NoteOn(note, Math.Max(1, (int)(v * 127))); else pf.NoteOff(note);
            return true;
        }
        if (k == "blpad" && p.Length >= 2)
        {
            int row; int.TryParse(p[1], out row); if (MidiMap.Pressed(target, v, cc)) BlHit(row, cc ? 110 : Math.Max(1, (int)(v * 127))); return true;
        }
        try { BeginInvoke(new Action(() => { try { MidiApplyUi(p, target, v, cc); Invalidate(); } catch { } })); } catch { }
        return true;
    }
    void MidiApplyUi(string[] p, string target, float v, bool cc)
    {
        string k = p[0];
        Track sel = eng.Tracks.Count > 0 ? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))] : null;
        Track t = k == "sel" ? sel : (p.Length > 1 && k != "transport" && k != "selfx" && k != "fx" ? TrackNamed(p[1]) : null);
        switch (k == "sel" ? p[1] : k)
        {
            case "vol": if (t != null) t.VolDb = v < 0.003f ? -48f : (float)Math.Round(v * 54f - 48f, 1); break;
            case "pan": if (t != null) t.Pan = (float)Math.Round(v * 2 - 1, 2); break;
            case "trim": if (t != null && !t.IsBus) t.TrimDb = (float)Math.Round(v * 48 - 24, 1); break;
            case "mute": if (t != null && MidiMap.Pressed(target, v, cc)) ToggleMute(t); break;
            case "solo": if (t != null && MidiMap.Pressed(target, v, cc)) ToggleSolo(t); break;
            case "transport":
                if (!MidiMap.Pressed(target, v, cc)) break;
                if (p[1] == "play") { if (eng.Playing) PauseHere(); else PlayToggle(); } else if (p[1] == "stop") PauseHere(); else if (p[1] == "rec") { recOn = !recOn; Toast(recOn ? "MIDI REC on (from your controller)." : "MIDI REC off."); }
                break;
            case "fx":
                {   // fx|track|place|type|param: that plugin's knob, wherever it sits now (by its place in the chain, else the first of its kind)
                    var list = p[1].Length == 0 ? eng.MasterFx : (TrackNamed(p[1]) ?? new Track()).Fx; int i; int.TryParse(p[2], out i); Fx f;
                    lock (eng.Sync) { f = i >= 0 && i < list.Count && list[i].Type == p[3] ? list[i] : list.FirstOrDefault(x => x.Type == p[3]); }
                    var pr = f == null ? null : f.Find(p[4]); if (pr != null) f.Set(pr.Id, pr.IsChoice ? (float)Math.Round(PFromFrac(pr, v)) : PFromFrac(pr, v));
                    break;
                }
            case "selfx":
                {   // the Nth knob of the plugin in front (its window), else of the picked track's first effect
                    int n; int.TryParse(p[1], out n); var f = FrontFx(sel); if (f == null) break; var hid = new HashSet<string>((f.Hidden ?? "").Split(',')); var nums = f.P.Where(q => !q.IsChoice && !hid.Contains(q.Id)).ToList();
                    if (n >= 1 && n <= nums.Count) f.Set(nums[n - 1].Id, PFromFrac(nums[n - 1], v));
                    break;
                }
        }
    }
    Fx FrontFx(Track sel)
    {
        var pw = Form.ActiveForm as PxWindow; if (pw != null && pw.Plugin != null) return pw.Plugin;
        var w = fxWins.OrderByDescending(x => x.Seq).FirstOrDefault(); if (w != null) return w.Fx;
        if (sel == null) return null; lock (eng.Sync) return sel.Fx.FirstOrDefault(f => !(f is InstrumentFx)) ?? sel.Fx.FirstOrDefault();
    }

    // while it learns: a banner across the top (click it to stop)
    void PaintLearnBanner(Graphics g)
    {
        if (MidiMap.Learning == null) return; var ma = MidiMap.ForTarget(MidiMap.Learning);
        var b = Rc(ClientSize.Width / 2 - 360, 6, 720, 40); FillRR(g, TM.C(245, 40, 12, 60), b, 12); StrokeRR(g, NeonPink, b, 12, 2f); Glow(g, NeonPink, b, 12, 0.6f, 3);
        Txt(g, "MIDI LEARN: move a knob or slider, or press a key or pad on your controller", 12, Ink, Rc(b.X + 16, b.Y + 2, b.Width - 140, 20), StringAlignment.Near, true);
        Txt(g, "for " + MidiMap.LearningLabel, 10.5f, DimC, Rc(b.X + 16, b.Y + 20, b.Width - 140, 16), StringAlignment.Near, false);
        Btn(g, Rc(b.Right - 110, b.Y + 7, 98, 26), "CANCEL", 3, () => MidiMap.CancelLearn(), 10);
    }
    // the light next to MIDI REC: it flashes on every message from your controller
    void PaintMidiLed(Graphics g, RectangleF recBtn)
    {
        bool lit = unchecked(Environment.TickCount - MidiMap.ActivityAt) < 140 && MidiMap.ActivityAt != 0; var d = Rc(recBtn.Right - 13, recBtn.Y + 4, 8, 8);
        using (var b = new SolidBrush(lit ? NeonCyan : (MidiInput.OpenName.Length > 0 ? TM.C(160, 40, 90, 110) : TM.C(160, 90, 50, 60)))) g.FillEllipse(b, d);
        if (lit) using (var b = new SolidBrush(Color.FromArgb(70, NeonCyan))) g.FillEllipse(b, RectangleF.Inflate(d, 4, 4));
        if (lit) lastInput = DateTime.Now;
    }
    string MidiInHint { get { return MidiInput.OpenName.Length == 0 ? "No MIDI keyboard connected (right-click MIDI REC to pick one)." : "MIDI in from " + MidiInput.OpenName + (MidiMap.LastIn.Length > 0 ? ": " + MidiMap.LastIn : ": nothing yet") + ". Right-click for the keyboard and MIDI MAPPING."; } }

    // ---- the MIDI MAPPING panel ----
    void OpenMidiMap() { mmOpen = true; mmScroll = 0; }
    void PaintMidiMap(Graphics g)
    {
        if (!mmOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var bd = new SolidBrush(TM.C(160, 3, 1, 10))) g.FillRectangle(bd, all); Reg(all, () => mmOpen = false);
        var box = Rc(W / 2 - 430, Math.Max(20, H / 2 - 300), 860, Math.Min(600, H - 40)); FillRR(g, TM.C(250, 16, 8, 34), box, 18); StrokeRR(g, NeonCyan, box, 18, 2f); Glow(g, NeonCyan, box, 18, 0.5f, 3); Reg(box, () => { });
        Txt(g, "MIDI MAPPING", 22, Ink, Rc(box.X + 24, box.Y + 16, 400, 30), StringAlignment.Near, true);
        Txt(g, "Right-click any knob, slider or button in the studio or a plugin window and pick MIDI Learn, then move a knob or press a key or pad on your controller.", 10.5f, DimC, Rc(box.X + 24, box.Y + 48, box.Width - 48, 18), StringAlignment.Near, false);
        Btn(g, Rc(box.Right - 50, box.Y + 14, 32, 28), "x", 3, () => mmOpen = false, 12);
        // the profile and what comes in
        var pr = Rc(box.X + 24, box.Y + 76, 330, 30); Btn(g, pr, "PROFILE: " + MidiMap.Profile + "  ▾", 2, () => ProfileMenu(), 11); Hint2(pr, "Your controller profiles: switch, make a new one, copy, rename or delete");
        bool lit = unchecked(Environment.TickCount - MidiMap.ActivityAt) < 160 && MidiMap.ActivityAt != 0; var led = Rc(pr.Right + 16, pr.Y + 9, 12, 12); using (var b = new SolidBrush(lit ? NeonCyan : TM.C(150, 60, 60, 90))) g.FillEllipse(b, led); if (lit) lastInput = DateTime.Now;
        Txt(g, MidiInput.OpenName.Length == 0 ? "NO MIDI KEYBOARD CONNECTED" : (MidiMap.LastIn.Length > 0 ? "IN: " + MidiMap.LastIn : "IN: " + MidiInput.OpenName + " (move something on it)"), 10.5f, lit ? NeonCyan : DimC, Rc(led.Right + 8, pr.Y, box.Right - led.Right - 32, 30), StringAlignment.Near, true);
        // the check: every MIDI input Windows reports and what it is doing, and the sound card
        var rep = MidiInput.Report(); float cy = pr.Bottom + 10; var chk = Rc(box.X + 24, cy, box.Width - 48, 22 + rep.Count * 18 + 18); FillRR(g, TM.C(200, 10, 6, 24), chk, 10);
        Txt(g, "CHECK: YOUR MIDI INPUTS", 10, NeonCyan, Rc(chk.X + 12, chk.Y + 4, chk.Width - 24, 16), StringAlignment.Near, true);
        for (int i = 0; i < rep.Count; i++) Txt(g, rep[i], 10, rep[i].Contains("LISTENING") ? Good : Warn, Rc(chk.X + 12, chk.Y + 22 + i * 18, chk.Width - 24, 16), StringAlignment.Near, false);
        int lv = Prefs.BufEff; string buf = lv == 0 ? "4 x 512 (46 ms)" : lv == 2 ? "8 x 2048 (372 ms)" : "8 x 1024 (186 ms)";
        Txt(g, "SOUND CARD: buffer " + buf + ",  dropouts since the studio opened: " + WaveOutPlayer.Underruns + " (each one is a crackle)", 10, WaveOutPlayer.Underruns > 0 ? Warn : DimC, Rc(chk.X + 12, chk.Bottom - 20, chk.Width - 24, 16), StringAlignment.Near, false);
        if (unchecked(Environment.TickCount - MidiMap.ActivityAt) < 400) lastInput = DateTime.Now;
        // the maps
        var maps = MidiMap.All; var list = Rc(box.X + 24, chk.Bottom + 10, box.Width - 48, box.Bottom - chk.Bottom - 70); FillRR(g, TM.C(200, 8, 4, 18), list, 10);
        if (maps.Count == 0) Txt(g, "No MIDI mappings in this profile yet.", 12, DimC, list, StringAlignment.Center, false);
        float rh = 38, y0 = list.Y + 6 - mmScroll; var gs = g.Save(); g.SetClip(list);
        foreach (var m0 in maps)
        {
            var m = m0; var row = Rc(list.X + 6, y0, list.Width - 12, rh - 4); y0 += rh; if (row.Bottom < list.Y || row.Y > list.Bottom) continue;
            FillRR(g, TM.C(140, 40, 22, 74), row, 8);
            Txt(g, MidiMap.SrcText(m.Src), 10.5f, NeonCyan, Rc(row.X + 12, row.Y, 300, row.Height), StringAlignment.Near, true);
            Txt(g, "→  " + m.Label, 11, Ink, Rc(row.X + 318, row.Y, row.Width - 318 - 220, row.Height), StringAlignment.Near, true);
            var re = Rc(row.Right - 206, row.Y + 5, 100, row.Height - 10); Btn(g, re, "RE-LEARN", 1, () => { MidiMap.StartLearn(m.Target, m.Label); mmOpen = false; }, 9.5f); Hint2(re, "Assign another knob, key or pad to " + m.Label);
            var rm = Rc(row.Right - 100, row.Y + 5, 92, row.Height - 10); Btn(g, rm, "REMOVE", 3, () => MidiMap.Remove(m.Target), 9.5f); Hint2(rm, "Take this mapping off");
        }
        g.Restore(gs); float maxS = Math.Max(0, maps.Count * rh + 12 - list.Height); mmScroll = Math.Min(mmScroll, maxS);
        var wh = new HitReg { R = list, OnWheel = d => mmScroll = Math.Max(0, Math.Min(maxS, mmScroll - d * 40)) }; regs.Add(wh);
        var ca = Rc(box.X + 24, box.Bottom - 50, 170, 34); Btn(g, ca, "CLEAR ALL", 3, () => { if (maps.Count > 0 && AskYes("Take every mapping off the profile " + MidiMap.Profile + "?", "MIDI mapping")) MidiMap.Clear(); }, 10.5f);
        var dn = Rc(box.Right - 24 - 140, box.Bottom - 50, 140, 34); Btn(g, dn, "DONE", 2, () => mmOpen = false, 12);
        Txt(g, maps.Count + " mapping" + (maps.Count == 1 ? "" : "s") + "  ·  saved as you go", 10, DimC, Rc(ca.Right + 16, ca.Y, dn.X - ca.Right - 32, 34), StringAlignment.Center, false);
    }
    void ProfileMenu()
    {
        var m = Win.NewMenu(); m.Items.Add(new ToolStripMenuItem("CONTROLLER PROFILES") { Enabled = false });
        foreach (var p0 in MidiMap.Profiles()) { var p = p0; var it = new ToolStripMenuItem(p) { Checked = p == MidiMap.Profile }; it.Click += (a, b) => { MidiMap.Save(); MidiMap.Load(p); Toast("MIDI profile: " + p); }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("New empty profile...").Click += (a, b) => { string n = Ask("Name for the new controller profile:", Presets.FreeName("_midi_", "My controller 2")); if (!string.IsNullOrWhiteSpace(n)) MidiMap.NewProfile(n.Trim()); };
        m.Items.Add("Save a copy of this profile as...").Click += (a, b) => { string n = Ask("Name for the copy:", MidiMap.Profile + " copy"); if (!string.IsNullOrWhiteSpace(n)) MidiMap.CopyProfile(n.Trim()); };
        m.Items.Add("Rename this profile...").Click += (a, b) => { string n = Ask("New name:", MidiMap.Profile); if (!string.IsNullOrWhiteSpace(n)) MidiMap.RenameProfile(n.Trim()); };
        m.Items.Add("Make a profile for the Akai MPK Mini (its 8 knobs turn the plugin in front)").Click += (a, b) => { MidiMap.MakeMpkMini(); Toast("Akai MPK Mini profile: knobs 1 to 8 turn the first 8 knobs of the plugin in front; its pads play Purphex Pads and the Beat Lab."); };
        var del = new ToolStripMenuItem("Delete this profile") { Enabled = MidiMap.Profiles().Count > 1 }; del.Click += (a, b) => { if (AskYes("Delete the controller profile " + MidiMap.Profile + "?", "MIDI mapping")) MidiMap.DeleteProfile(); }; m.Items.Add(del);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
}
