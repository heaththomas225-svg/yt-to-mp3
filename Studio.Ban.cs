using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// A PC whose account the Purphex team closed does not run the app: it asks the website at start and every few hours (linked or not),
// remembers the answer (so it stays closed offline), and runs again as soon as the website says the account was opened again.
static class PcBan
{
    static string FlagPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "closed.txt"); } }
    static int shown;
    // the website's answer: true (closed, with the reason), false (fine), null (no answer)
    public static bool? Parse(string json, out string reason)
    {
        reason = ""; if (string.IsNullOrEmpty(json) || json.IndexOf("\"ok\":true", StringComparison.Ordinal) < 0) return null;
        var m = Regex.Match(json, "\"reason\":\"((?:[^\"\\\\]|\\\\.)*)\""); if (m.Success) { try { reason = Regex.Unescape(m.Groups[1].Value); } catch { reason = m.Groups[1].Value; } }
        if (reason.Length > 200) reason = reason.Substring(0, 200);
        if (json.IndexOf("\"banned\":true", StringComparison.Ordinal) >= 0) return true; if (json.IndexOf("\"banned\":false", StringComparison.Ordinal) >= 0) return false; return null;
    }
    public static void Check()
    {
        if (StudioEnv.UnderTest) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            bool? closed = null; string reason = "";
            try
            {
                var rq = (HttpWebRequest)WebRequest.Create(Edition.Site + "/api/pc-status"); rq.UserAgent = "PurphexMusic"; rq.Timeout = 12000; rq.ReadWriteTimeout = 12000; rq.Headers["X-Device"] = Edition.Fingerprint;
                using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream())) closed = Parse(sr.ReadToEnd(), out reason);
            }
            catch { }
            if (closed == true) Closed(reason);
            else if (closed == false) { try { if (File.Exists(FlagPath)) File.Delete(FlagPath); } catch { } }
            else { string saved = null; try { if (File.Exists(FlagPath)) saved = File.ReadAllText(FlagPath); } catch { } if (saved != null) Closed(saved); }   // no answer: a PC that was closed stays closed
        });
    }
    // closed: remember it, say so, and stop the app
    public static void Closed(string reason)
    {
        if (StudioEnv.UnderTest) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(FlagPath)); File.WriteAllText(FlagPath, reason ?? ""); } catch { }
        if (Interlocked.Exchange(ref shown, 1) == 1) return;
        string msg = "The Purphex account this PC was linked to has been closed by the Purphex team." + (string.IsNullOrEmpty(reason) ? "" : "\n\nReason: " + reason) + "\n\nPurphex Music cannot be used on this PC. If you think this is a mistake, open a ticket in the Purphex Discord.";
        Action show = () => { try { MessageBox.Show(msg, "Purphex Music", MessageBoxButtons.OK, MessageBoxIcon.Stop); } catch { } Environment.Exit(0); };
        Form f = null; try { if (Application.OpenForms.Count > 0) f = Application.OpenForms[0]; } catch { }
        if (f != null && f.IsHandleCreated) { try { f.BeginInvoke(show); return; } catch { } } show();
    }
}
