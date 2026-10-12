using System;
using System.IO;

// Tries Generate Lyrics against the REAL lyrics database and YouTube (no pretend servers), with names like real downloads.   csc /main:RL ... RealLyr.cs     run: RL.exe
static class RL
{
    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_reallyr_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        string d = Path.Combine(StudioEnv.DataDir, "songs"); Directory.CreateDirectory(d);
        string[] files = { "Queen - Bohemian Rhapsody (Official Video Remastered).mp4", "Rick Astley - Never Gonna Give You Up [MP3 320k].mp3", "Bohemian Rhapsody.mp3", "Ed Sheeran - Shape of You.wav", "Adele - Hello (Official Music Video).mp3", "Imagine Dragons - Believer.flac", "The Weeknd - Blinding Lights (Official Audio).m4a", "Eminem - Lose Yourself [HD].mp3", "Queen Official - Queen - Bohemian Rhapsody (Official Video Remastered) [MP3 320k].mp3", "Taylor Swift - Anti-Hero (Official Music Video).mp4", "Coldplay - Yellow (Official Video).mp3", "Lose Yourself - Eminem.mp3", "Billie Eilish - bad guy [MP3 320k].mp3" };
        foreach (string f in files)
        {
            string p = Path.Combine(d, f); File.WriteAllText(p, "x"); var r = LyricsGen.Generate(p, false, null, () => false);
            Console.WriteLine((r.Found ? "OK    " : "FAIL  ") + f + "   -> " + r.Artist + " | " + r.Title + " | " + (r.Found ? r.Lyrics.Split('\n').Length + " lines" : r.Error));
        }
        foreach (string u in new[] { "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://youtu.be/fJ9rUzIMcZQ", "https://www.youtube.com/watch?v=kJQP7kiw5Fk" })
        {
            var r = LyricsGen.Generate(u, false, null, () => false); Console.WriteLine((r.Found ? "OK    " : "FAIL  ") + u + "   -> " + r.Artist + " | " + r.Title + " | " + (r.Found ? r.Lyrics.Split('\n').Length + " lines" : r.Error));
        }
    }
}
