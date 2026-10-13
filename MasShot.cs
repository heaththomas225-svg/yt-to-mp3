using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The Mastering tab with the BEFORE / AFTER loudness lines and the REVERT bar.   csc /main:MaS ... MasShot.cs     run: MasShot.exe <folder>
static class MaS
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    [STAThread]
    static void Main(string[] args)
    {
        Edition.Override = Tier.Fantasy; string outDir = args[0]; Directory.CreateDirectory(outDir);
        string work = Path.Combine(Path.GetTempPath(), "mas_data_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = work;
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(10)));
            sf = new StudioForm(app); int[][] sizes = { new[] { 1366, 768 }, new[] { 1240, 680 }, new[] { 1700, 1000 } };
            foreach (var sz in sizes)
            {
                sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(40, 30); sf.ClientSize = new Size(sz[0], sz[1]); sf.Show(); Pump(400);
                SetF("tab", 1); SetF("mpTab", 0); SetF("selMaster", 0);
                SetF("abOrig", new LoudnessResult { I = -16.9, LRA = 6 }); SetF("abMast", new LoudnessResult { I = -13.2, LRA = 5 });
                typeof(StudioForm).GetMethod("OfferRevert", BF).Invoke(sf, new object[] { "Vocal Forward is on your master", (Action)(() => { }) });
                var bmp = new Bitmap(sz[0], sz[1]); var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, sz[0], sz[1])); for (int i = 0; i < 2; i++) pm.Invoke(sf, new object[] { pe }); }
                bmp.Save(Path.Combine(outDir, "mas-" + sz[0] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                using (var crop = bmp.Clone(new Rectangle(0, Math.Max(0, sz[1] - 330), 420, 330), bmp.PixelFormat)) crop.Save(Path.Combine(outDir, "mas-corner-" + sz[0] + ".png"), System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose();
            }
            { // the picture sheet: a wide picture with black bars is cut and fitted by itself
                string pp = Path.Combine(work, "demo.ytmp"); File.WriteAllText(pp, "PURPHEX\nname|Demo\n"); ProjectLibrary.Touch(pp, "Demo", 3, 120, 100, true, null);
                string img = Path.Combine(work, "wide.png"); using (var b0 = new Bitmap(1200, 600)) { using (var g0 = Graphics.FromImage(b0)) { g0.Clear(Color.Black); using (var br = new SolidBrush(Color.FromArgb(230, 255, 40, 60))) g0.FillEllipse(br, 480, 220, 240, 160); } b0.Save(img, System.Drawing.Imaging.ImageFormat.Png); }
                string err; bool ok = ProjectLibrary.SetCover(pp, img, out err); int z, x, y; ProjectLibrary.LoadAdj(pp, out z, out x, out y); Console.WriteLine("setcover " + ok + " " + err + " adj " + z + "/" + x + "/" + y);
                using (var im = Image.FromFile(ProjectLibrary.CoverSrc(pp))) Console.WriteLine("kept picture " + im.Width + "x" + im.Height);
                sf.ClientSize = new Size(1366, 768); Pump(300); SetF("tab", 0); typeof(StudioForm).GetMethod("OpenCoverAdjust", BF).Invoke(sf, new object[] { pp });
                var bmp2 = new Bitmap(1366, 768); var pm2 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp2)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm2.Invoke(sf, new object[] { pe }); }
                bmp2.Save(Path.Combine(outDir, "cover-sheet.png"), System.Drawing.Imaging.ImageFormat.Png); bmp2.Dispose();
            }
            { // the picked row's outline stops where the audio ends
                eng.Tracks.Add(StudioEngine.NewTrack("Short", "vocal", Clip(3))); eng.Tracks.Add(StudioEngine.NewTrack("Third", "beat", Clip(8))); sf.ClientSize = new Size(1366, 768); Pump(300); SetF("caOpen", false); SetF("revertAction", null); SetF("tab", 0); SetF("selTrack", 1);
                var bmp3 = new Bitmap(1366, 768); var pm3 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp3)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm3.Invoke(sf, new object[] { pe }); }
                bmp3.Save(Path.Combine(outDir, "row-outline.png"), System.Drawing.Imaging.ImageFormat.Png); bmp3.Dispose();
            }
            { // the plugin editor: which preset is on, COPY / PASTE / REMOVE
                eng.Tracks.Clear(); var tk = StudioEngine.NewTrack("Vox", "vocal", Clip(10)); var dfx = FxRegistry.Create("delay"); dfx.Prepare(44100); dfx.CurPreset = "Slapback Echo"; dfx.CurPresetData = dfx.Serialize(); tk.Fx.Add(dfx); eng.Tracks.Add(tk);
                var cfx = FxRegistry.Create("compressor"); if (cfx != null) { cfx.Prepare(44100); tk.Fx.Add(cfx); }
                sf.ClientSize = new Size(1366, 768); Pump(300); SetF("caOpen", false); SetF("revertAction", null); SetF("tab", 0); SetF("selTrack", 0); SetF("selFx", 0);
                var bmp4 = new Bitmap(1366, 768); var pm4 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp4)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm4.Invoke(sf, new object[] { pe }); }
                bmp4.Save(Path.Combine(outDir, "fx-row.png"), System.Drawing.Imaging.ImageFormat.Png); bmp4.Dispose();
            }
            { // a track split into several pieces
                eng.Tracks.Clear(); var tk2 = StudioEngine.NewTrack("Vox", "vocal", Clip(10)); eng.Tracks.Add(tk2); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(10)));
                sf.ClientSize = new Size(1366, 768); Pump(300); SetF("tab", 0); SetF("selTrack", 0); SetF("revertAction", null);
                var sp = typeof(StudioForm).GetMethod("SplitTrack", BF); foreach (long at in new long[] { 44100 * 2, 44100 * 3 + 100, 44100 * 5, 44100 * 7 }) { Track piece = null; foreach (var t in eng.Tracks.ToArray()) if ((t.Host ?? t) == tk2 && t.Clip != null && at > t.Offset && at < t.Offset + t.UsedLength) piece = t; sp.Invoke(sf, new object[] { piece, at }); }
                var bmp5 = new Bitmap(1366, 768); var pm5 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp5)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm5.Invoke(sf, new object[] { pe }); }
                bmp5.Save(Path.Combine(outDir, "split-pieces.png"), System.Drawing.Imaging.ImageFormat.Png); bmp5.Dispose();
            }
            { // the Messages page with the reply box
                ProfileStore.Published = true; var th = new MsgThread { With = new ArtistInfo { Id = "a0123456789ab", Name = "Neo" } }; th.Msgs.Add(new MsgItem { Mine = false, At = DateTime.UtcNow.Ticks, Text = "Want to make a track together?" }); th.Msgs.Add(new MsgItem { Mine = true, At = DateTime.UtcNow.Ticks, Text = "Yes! Send me the beat.", About = "Reply: Want to make a track together?" });
                var thr = (System.Collections.Generic.List<MsgThread>)typeof(StudioForm).GetField("msgThreads", BF).GetValue(sf); thr.Clear(); thr.Add(th); SetF("msgWithId", "a0123456789ab"); SetF("tab", 7); SetF("artView", 1); SetF("artSel", null); SetF("caOpen", false); SetF("revertAction", null); SetF("msgReplyTo", "Want to make a track together?"); SetF("msgDraft", "Sounds good, I'll send it tonight"); SetF("msgTyping", true);
                sf.ClientSize = new Size(1366, 768); Pump(300); var bmp6 = new Bitmap(1366, 768); var pm6 = typeof(StudioForm).GetMethod("PaintAll", BF); using (var g = Graphics.FromImage(bmp6)) { var pe = new PaintEventArgs(g, new Rectangle(0, 0, 1366, 768)); for (int i = 0; i < 3; i++) pm6.Invoke(sf, new object[] { pe }); }
                bmp6.Save(Path.Combine(outDir, "messages.png"), System.Drawing.Imaging.ImageFormat.Png); bmp6.Dispose();
            }
            Console.WriteLine("saved");
        }
        finally { try { Directory.Delete(work, true); } catch { } }
        Environment.Exit(0);
    }
}
