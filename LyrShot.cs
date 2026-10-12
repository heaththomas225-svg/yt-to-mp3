using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Paints the Generate Lyrics window (empty, with lyrics, Free and Fantasy, two sizes) into pictures to look at.   csc /main:LS ... LyrShot.cs     run: LS.exe <output folder>
static class LS
{
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static T Fld<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(o); }
    const string LYR = "I walk alone in the night\nsearching for a light\nnobody by my side\nbut I'm alright\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down\n\nThe city never sleeps tonight\nthe stars are burning bright\nwe dance until the dawn\nand then we carry on\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down";
    static void Shot(Form f, string path) { Pump(500); var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); var full = new Bitmap(f.Width, f.Height); f.DrawToBitmap(full, new Rectangle(0, 0, f.Width, f.Height)); full.Save(path, ImageFormat.Png); full.Dispose(); bmp.Dispose(); Console.WriteLine("saved " + path); }
    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_lyrshot_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        if (args.Length > 1) { var app = (App)Activator.CreateInstance(typeof(App), true); Edition.Override = Tier.Free; app.Show(); Pump(1500); Shot(app, Path.Combine(outDir, "main_free.png")); app.Close(); return; }
        string song = Path.Combine(StudioEnv.DataDir, "Neon Fox - Night Owl [MP3 320k].mp3"); File.WriteAllText(song, "x");
        foreach (var tier in new[] { Tier.Free, Tier.Fantasy })
            foreach (var sz in new[] { new Size(1000, 900), new Size(860, 700) })
            {
                Edition.Override = tier; var f = new LyricsGenForm(null); f.ClientSize = sz; f.Show(); Pump(400); string tag = tier + "_" + sz.Width;
                Shot(f, Path.Combine(outDir, "lyrgen_" + tag + "_empty.png"));
                f.LoadFile(song); string n; string pol = LyricsPolish.Finish(LYR, false, out n); Fld<TextBox>(f, "lyricsBox").Text = pol.Replace("\n", "\r\n"); f.TitleText = "Neon Fox - Night Owl"; Pump(600);
                typeof(LyricsGenForm).GetField("noteLbl", BindingFlags.NonPublic | BindingFlags.Instance).FieldType.GetProperty("Text").SetValue(Fld<object>(f, "noteLbl"), "LRCLIB lyrics database. Found in the lyrics database. " + n, null);
                Shot(f, Path.Combine(outDir, "lyrgen_" + tag + "_lyrics.png")); f.Close(); Pump(100);
            }
    }
}
