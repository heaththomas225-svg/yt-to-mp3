using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Listen before you convert: a 30-second sample (from a file or the first link in the box) is processed
//  with the real settings, then played back with one-click switching between versions.
// ======================================================================
class PreviewForm : Form
{
    readonly StudioEngine prevCur = StudioEngine.Current; readonly StudioEngine eng = new StudioEngine(); readonly Font waveFont = new Font("Segoe UI Semibold", 10f); long lastPos = -1; readonly List<string> names = new List<string>(); readonly List<string> info = new List<string>();
    int sel; Panel wave; System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer { Interval = 33 }; Button playBtn; Label timeLbl; List<Button> srcBtns = new List<Button>();
    readonly float[] wmn = new float[2048], wmx = new float[2048]; bool loop = true;

    public static string Describe(AudioClip c)
    {
        var r = Loudness.Measure(c.L, c.R, StudioEngine.SR, 0, c.Length);
        return double.IsInfinity(r.I) ? "silent" : r.I.ToString("0.0", CultureInfo.InvariantCulture) + " LUFS, peak " + r.TruePeakDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
    }

    public PreviewForm(string title, string subtitle, List<KeyValuePair<string, AudioClip>> sources, List<string> infos)
    {
        Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
        BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); ClientSize = new Size(620, 400); Font = new Font("Segoe UI", 10f); DoubleBuffered = true;
        Win.Dark(this);
        foreach (var f in eng.MasterFx) f.Bypass = true;   // plain playback: no mastering chain in the way
        foreach (var kv in sources)
        {
            var tr = StudioEngine.NewTrack(kv.Key, "other", kv.Value); tr.Mute = true; eng.Tracks.Add(tr); names.Add(kv.Key);
            info.Add(infos[names.Count - 1]);
        }
        Controls.Add(new Label { Text = title, Left = 18, Top = 14, Width = 580, Height = 28, Font = new Font("Segoe UI Semibold", 14f), ForeColor = TM.C(255, 225, 190, 255) });
        Controls.Add(new Label { Text = subtitle, Left = 18, Top = 44, Width = 584, Height = 36, ForeColor = TM.C(255, 170, 150, 200) });
        int bw = (584 - 8 * (sources.Count - 1)) / sources.Count;
        for (int i = 0; i < sources.Count; i++)
        {
            int k = i; var b = new PxButton { Left = 18 + i * (bw + 8), Top = 86, Width = bw, Height = 62, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10.5f) };
            b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); b.Text = names[i] + "\r\n" + info[i]; b.Click += (s, e) => Select(k); srcBtns.Add(b); Controls.Add(b);
        }
        wave = new Panel { Left = 18, Top = 162, Width = 584, Height = 150, BackColor = TM.C(255, 6, 3, 12) };
        typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(wave, true, null);
        wave.Paint += PaintWave; wave.MouseDown += (s, e) => Seek(e.X); wave.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) Seek(e.X); }; Controls.Add(wave);
        playBtn = Mk("Play", 18, 326, 100, () => Toggle()); Mk("Stop", 126, 326, 80, () => { eng.Pause(); eng.Seek(0); UpdateUi(); });
        var lp = new DarkCheck { Text = "Loop", Left = 216, Top = 328, Width = 80, Height = 30, Checked = true }; lp.CheckedChanged += (s, e) => { loop = lp.Checked; eng.Loop = loop; }; Controls.Add(lp); eng.Loop = true;
        timeLbl = new Label { Left = 306, Top = 331, Width = 160, Height = 26, ForeColor = TM.C(255, 190, 120, 255), Font = new Font("Segoe UI Semibold", 11f) }; Controls.Add(timeLbl);
        Mk("Close", 502, 326, 100, () => Close());
        Controls.Add(new Label { Text = "Space = play / pause.  Click a version to switch instantly while it plays. Nothing is saved from this preview.", Left = 18, Top = 366, Width = 584, Height = 24, ForeColor = TM.C(255, 150, 130, 180), Font = new Font("Segoe UI", 8.5f) });
        Select(sources.Count > 1 ? 1 : 0);
        tm.Tick += (s, e) => { UpdateUi(); if (eng.Playing || eng.Pos != lastPos) { lastPos = eng.Pos; wave.Invalidate(); } }; tm.Start();
        FormClosed += (s, e) => { tm.Stop(); eng.Pause(); eng.Shutdown(); if (StudioEngine.Current == eng) StudioEngine.Current = prevCur; waveFont.Dispose(); };
        KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Space) { Toggle(); e.Handled = true; e.SuppressKeyPress = true; } };
        Shown += (s, e) => { try { eng.Play(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
    }

    Button Mk(string text, int x, int y, int w, Action click)
    {
        var b = new PxButton { Text = text, Left = x, Top = y, Width = w, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); b.Click += (s, e) => click(); Controls.Add(b); return b;
    }

    void Select(int i)
    {
        sel = i; for (int k = 0; k < eng.Tracks.Count; k++) eng.Tracks[k].Mute = k != i;
        for (int k = 0; k < srcBtns.Count; k++) srcBtns[k].BackColor = k == i ? TM.C(255, 150, 60, 230) : TM.C(255, 44, 22, 84);
        wave.Invalidate();
    }

    void Toggle() { if (eng.Playing) eng.Pause(); else eng.Play(); UpdateUi(); }
    void Seek(int x) { long len = eng.Tracks.Count > 0 ? eng.Tracks[0].Clip.Length : 0; eng.Seek((long)(Math.Max(0, Math.Min(1, x / (double)wave.Width)) * len)); }
    void UpdateUi()
    {
        playBtn.Text = eng.Playing ? "Pause" : "Play";
        long len = eng.Tracks.Count > 0 ? eng.Tracks[0].Clip.Length : 0; timeLbl.Text = (eng.Pos / (double)StudioEngine.SR).ToString("0.0", CultureInfo.InvariantCulture) + " / " + (len / (double)StudioEngine.SR).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }

    void PaintWave(object sender, PaintEventArgs e)
    {
        var g = e.Graphics; if (eng.Tracks.Count == 0) return; var clip = eng.Tracks[sel].Clip; int w = wave.Width; double spp = Math.Max(1, clip.Length / (double)w);
        clip.GetPeaks(0, spp, w, wmn, wmx); float cy = wave.Height / 2f, amp = wave.Height * 0.44f;
        using (var pen = new Pen(TM.C(220, 190, 120, 255))) for (int i = 0; i < w; i++) { float a = cy - Math.Min(1f, wmx[i]) * amp, b = cy - Math.Max(-1f, wmn[i]) * amp; if (b - a < 1) b = a + 1; g.DrawLine(pen, i, a, i, b); }
        float px = (float)(eng.Pos / (double)Math.Max(1, clip.Length) * w); using (var pen = new Pen(TM.C(255, 236, 92, 226), 2)) g.DrawLine(pen, px, 0, px, wave.Height);
        TextRenderer.DrawText(g, names[sel], waveFont, new Point(8, 6), TM.C(255, 230, 200, 255));
    }
}

// ----------------------------------------------------------------------
partial class App
{
    // Returns a local audio path, a URL, or null (cancelled).
    string PickPreviewSource()
    {
        var items = ParseItems(links.Text); string first = items.Count > 0 ? NormalizeTarget(items[0]) : null;
        if (first != null && first.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var r = MessageBox.Show(this, "Preview using the first link in the box?\n\nYes: downloads a 30-second sample of it.\nNo: pick an audio file from your PC instead.", "Preview", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return null; if (r == DialogResult.Yes) return first;
        }
        using (var d = new OpenFileDialog { Title = "Choose a song to preview", Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.aiff;*.aif;*.wma|All files|*.*" })
        {
            try { if (LastFile != null && File.Exists(LastFile)) d.InitialDirectory = Path.GetDirectoryName(LastFile); } catch { }
            return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }
    }

    // 30 seconds (from 0:30 if the song is long enough) as a WAV in a temp folder.
    string MakeExcerpt(string src, string tmp)
    {
        string outWav = Path.Combine(tmp, "sample.wav");
        if (src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(YtDlp)) throw new Exception("Convert something once first so the downloader is installed.");
            string ff = FfmpegExe(); string deno = FindFile(DenoDir, "deno.exe"); status = "Downloading a 30 s sample..."; targetPct = 0; pctScale = 1f; itemIdx = 0; itemTotal = 1;
            string args = "-x --audio-format wav --newline --windows-filenames --ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" " + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "")
                + "--download-sections \"*0:30-1:00\" --force-keyframes-at-cuts --no-playlist -o \"" + Path.Combine(tmp, "dl.%(ext)s") + "\" -- \"" + src + "\"";
            if (RunProc(YtDlp, args, true) != 0) throw new Exception("Couldn't download a sample from that link.");
            string dl = Path.Combine(tmp, "dl.wav"); if (!File.Exists(dl)) throw new Exception("The sample download produced no audio.");
            File.Copy(dl, outWav, true); return outWav;
        }
        status = "Reading the sample..."; var clip = AudioClip.FromFile(src); int sr = StudioEngine.SR, n = clip.Length, start = n > sr * 45 ? sr * 30 : 0, len = Math.Min(sr * 30, n - start);
        var l = new float[len]; var r = new float[len]; Array.Copy(clip.L, start, l, 0, len); Array.Copy(clip.R, start, r, 0, len); WavIO.Write(outWav, l, r, sr, 32); return outWav;
    }

    void PreviewNormalize()
    {
        if (busy) return; if (FfmpegExe() == null) { MessageBox.Show(this, "Convert something once first. That installs ffmpeg, which the preview needs."); return; }
        string src = PickPreviewSource(); if (src == null) return;
        cancelReq = false; SetBusy(true); AudioClip orig = null, norm = null;
        new Thread(() =>
        {
            string tmp = Path.Combine(Path.GetTempPath(), "yt2mp3_prev_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(tmp);
            try
            {
                string ex = MakeExcerpt(src, tmp); status = "Applying loudness normalization..."; targetPct = 60; int code;
                string nw = Path.Combine(tmp, "norm.wav");
                MasterDsp.RunCapture(FfmpegExe(), "-hide_banner -y -i \"" + ex + "\" -af loudnorm=I=-14:TP=-1:LRA=11 -ar 44100 -ac 2 \"" + nw + "\"", p => { cur = p; }, out code);
                if (code != 0 || !File.Exists(nw)) throw new Exception("Normalization failed.");
                orig = AudioClip.FromFile(ex); norm = AudioClip.FromFile(nw); var inf = new List<string> { PreviewForm.Describe(orig), PreviewForm.Describe(norm) }; targetPct = 100; status = "Ready";
                BeginInvoke(new Action(() =>
                {
                    using (var f = new PreviewForm("Preview: loudness normalization", "Switch between the original and the version normalized to -14 LUFS (this is what Normalize loudness does to your download). Raise your volume the same for both.",
                        new List<KeyValuePair<string, AudioClip>> { new KeyValuePair<string, AudioClip>("Original", orig), new KeyValuePair<string, AudioClip>("Normalized -14 LUFS", norm) }, inf))
                    f.ShowDialog(this);
                }));
            }
            catch (Exception ex) { Log("Preview: " + ex.Message); BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Preview"))); }
            finally { try { Directory.Delete(tmp, true); } catch { } SetBusy(false); }
        }) { IsBackground = true }.Start();
    }

    void PreviewVocals()
    {
        if (busy) return; if (FfmpegExe() == null) { MessageBox.Show(this, "Convert something once first. That installs ffmpeg, which the preview needs."); return; }
        if (!Separator.Installed) { MessageBox.Show(this, "Set up the vocal remover first (use \"Remove vocals from a song\"). It's a one-time download of about 0.5 GB.", "Vocal remover"); return; }
        Separator.Warmup();
        string src = PickPreviewSource(); if (src == null) return;
        cancelReq = false; SetBusy(true);
        new Thread(() =>
        {
            string tmp = Path.Combine(Path.GetTempPath(), "yt2mp3_prev_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(tmp);
            try
            {
                string ex = MakeExcerpt(src, tmp); status = "Separating the 30 s sample with AI..."; targetPct = 0; string v, i;
                if (!Separator.Separate(ex, tmp, "preview", "htdemucs", Log, p => { targetPct = p * 90f; }, () => cancelReq, out v, out i)) throw new Exception(cancelReq ? "Cancelled." : "Separation failed.");
                var o = AudioClip.FromFile(ex); var ic = AudioClip.FromFile(i); var vc = AudioClip.FromFile(v); var inf = new List<string> { PreviewForm.Describe(o), PreviewForm.Describe(ic), PreviewForm.Describe(vc) }; targetPct = 100; status = "Ready";
                BeginInvoke(new Action(() =>
                {
                    using (var f = new PreviewForm("Preview: vocal remover", "Listen to the 30-second sample as the original, the instrumental (vocals removed) and the vocals alone. This is the same AI that runs on the full song.",
                        new List<KeyValuePair<string, AudioClip>> { new KeyValuePair<string, AudioClip>("Original", o), new KeyValuePair<string, AudioClip>("Instrumental", ic), new KeyValuePair<string, AudioClip>("Vocals only", vc) }, inf))
                    f.ShowDialog(this);
                }));
            }
            catch (Exception ex) { Log("Preview: " + ex.Message); BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Preview"))); }
            finally { try { Directory.Delete(tmp, true); } catch { } SetBusy(false); }
        }) { IsBackground = true }.Start();
    }
}
