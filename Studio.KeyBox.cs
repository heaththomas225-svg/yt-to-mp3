using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Purphex Music: Detect Track key. It listens to the selected track (then the others, longest first) until it finds a clear key,
// and tells you in a neon box in the middle of the screen (click it, press Esc, or wait: it goes away by itself).
partial class StudioForm
{
    bool keyOn, keyFound; string keyTitle = "", keySub = "", keyNote = ""; int keyAt;

    void ShowKeyBox(bool found, string title, string sub, string note) { keyOn = true; keyFound = found; keyTitle = title; keySub = sub; keyNote = note; keyAt = Environment.TickCount; lastInput = DateTime.Now; }

    void DetectTrackKey()
    {
        var cands = new List<Track>(); Track sel = Selected; if (sel != null && sel.Clip != null && !sel.Instrument && !sel.IsBus) cands.Add(sel);
        var rest = new List<Track>(); foreach (var x in eng.Tracks) if (x.Clip != null && !x.Instrument && !x.IsBus && !cands.Contains(x)) rest.Add(x);
        rest.Sort((a, b) => b.UsedLength.CompareTo(a.UsedLength)); cands.AddRange(rest);
        if (cands.Count == 0) { ShowKeyBox(false, "No audio yet", "Add a track with music or a voice, then press Detect Track key again.", ""); return; }
        int key = 0, scale = 1; double conf = 0; bool found = false; Track used = null;
        RunBusy("Finding the key (listening to " + cands[0].Name + ")...", false, p =>
        {
            foreach (var t in cands) { int k, s; double c; bool ok; try { ok = KeyDetect.Detect(t.Clip, out k, out s, out c); } catch { ok = false; k = 0; s = 1; c = 0; } if (ok) { found = true; key = k; scale = s; conf = c; used = t; break; } }
        }, ok =>
        {
            if (!ok) return;
            if (!found) { ShowKeyBox(false, "No clear key", "I could not hear a clear key. It works best on notes with a clear pitch: a voice, a melody, a bass line.", "Try the track with the vocal or the melody."); return; }
            string name = PitchCorrector.Keys[key] + (scale == 2 ? " minor" : " major"), rel = scale == 2 ? PitchCorrector.Keys[(key + 3) % 12] + " major" : PitchCorrector.Keys[(key + 9) % 12] + " minor";
            ShowKeyBox(true, name, "Relative " + (scale == 2 ? "major" : "minor") + ": " + rel + "     Confidence " + (int)Math.Round(Math.Max(0, Math.Min(1, conf)) * 100) + "%", "Listened to: " + used.Name);
        });
    }

    void PaintKeyBox(Graphics g)
    {
        if (!keyOn) return; int age = unchecked(Environment.TickCount - keyAt); if (age > 12000) { keyOn = false; return; } lastInput = DateTime.Now;
        float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(130, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => keyOn = false);
        float k = Math.Min(1f, age / 260f), ease = 1 - (1 - k) * (1 - k); float bw = Math.Min(560, W - 40), bh = 230, grow = 0.92f + 0.08f * ease;
        var box = Rc(W / 2 - bw * grow / 2, H / 2 - bh * grow / 2 - 20, bw * grow, bh * grow); double pulse = 0.5 + 0.5 * Math.Sin(age / 260.0); Color c1 = keyFound ? NeonCyan : Color.FromArgb(255, 150, 110), c2 = keyFound ? NeonPink : Color.FromArgb(255, 90, 130);
        for (int i = 6; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((int)((20 + 22 * pulse) / i * 2.2f) + 6, c1), i * 5f)) using (var p = RR(RectangleF.Inflate(box, i * 1.4f, i * 1.4f), 24 + i)) g.DrawPath(pen, p);
        using (var p = RR(box, 24)) { using (var br = new LinearGradientBrush(box, Color.FromArgb(250, 24, 10, 56), Color.FromArgb(250, 8, 3, 22), 90f)) g.FillPath(br, p); using (var pen = new Pen(c1, 2.4f)) g.DrawPath(pen, p); }
        using (var pen = new Pen(Color.FromArgb(120, c2), 1.2f)) using (var p = RR(RectangleF.Inflate(box, -10, -10), 18)) g.DrawPath(pen, p);
        Reg(box, () => keyOn = false);
        Txt(g, keyFound ? "THE KEY OF YOUR PROJECT" : "KEY DETECTOR", 12, DimC, Rc(box.X, box.Y + 22, box.Width, 20), StringAlignment.Center, true);
        Txt(g, keyTitle, keyFound ? 54 : 34, c1, Rc(box.X + 14, box.Y + 48, box.Width - 28, keyFound ? 78 : 50), StringAlignment.Center, true);
        Txt(g, keySub, 14, Ink, Rc(box.X + 24, box.Y + (keyFound ? 130 : 100), box.Width - 48, keyFound ? 24 : 60), StringAlignment.Center, false);
        if (keyNote.Length > 0) Txt(g, keyNote, 12, DimC, Rc(box.X + 24, box.Y + (keyFound ? 158 : 150), box.Width - 48, 20), StringAlignment.Center, false);
        Txt(g, "click anywhere to close", 11, Color.FromArgb(120, 150, 140, 190), Rc(box.X, box.Bottom - 30, box.Width, 18), StringAlignment.Center, false);
    }
}
