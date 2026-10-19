using System; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
static class TallShot
{
    static StudioForm sf; const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { var f = typeof(StudioForm).GetField(n, BF); if (f != null) f.SetValue(sf, v); else typeof(StudioForm).GetProperty(n, BF).SetValue(sf, v, null); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static AudioClip Clip(int sec) { var l = new float[44100 * sec]; for (int i = 0; i < l.Length; i++) l[i] = (float)(0.3 * Math.Sin(i * 0.03) * Math.Sin(i * 0.00002)); return AudioClip.FromArrays(l, (float[])l.Clone(), "x"); }
    [STAThread] static void Main()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_tallshot"); try { Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp; Application.EnableVisualStyles();
        var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Keys", "audio", null));
        sf = new StudioForm(app);
        foreach (var sz in new[] { new Size(1240, 680), new Size(1366, 768) })
        {
            sf.ClientSize = sz; sf.Show(); Pump(400); var lead = eng.Tracks[0]; SetF("selTrack", 0); SetF("tab", 0);
            if (eng.Tracks.Count == 3) typeof(StudioForm).GetMethod("SplitTrack", BF).Invoke(sf, new object[] { lead, 44100L * 20 }); SetF("toolsTrack", lead); SetF("LaneH", 168); Pump(200); sf.Invalidate(); sf.Update();
            var bm = new Bitmap(sz.Width, sz.Height); sf.DrawToBitmap(bm, new Rectangle(0, 0, sf.Width, sf.Height)); bm.Save("C:/Users/thoma/nfout/tall-" + sz.Width + ".png");
        }
        sf.Close();
    }
}
