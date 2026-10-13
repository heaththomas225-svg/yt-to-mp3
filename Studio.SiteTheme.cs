using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

// THE THEME ON THE WEBSITE: the theme you choose here goes to purphexmusic.com too (on a PC linked to your account), so the website is in your
// colours when you are signed in. The website keeps it only for the edition you are on. Sent once per change, in the background.
static class SiteTheme
{
    static string lastSent = "";
    static string Hex(Color c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }
    internal static string Body(ThemeDef t)
    {
        string req = t.Req == Tier.Fantasy ? "fantasy" : (t.Req == Tier.Free ? "free" : "supporter");
        return "{\"name\":\"" + (t.Name ?? "").Replace("\\", "").Replace("\"", "'") + "\",\"req\":\"" + req + "\",\"violet\":\"" + Hex(t.Violet) + "\",\"cyan\":\"" + Hex(t.Cyan) + "\",\"pink\":\"" + Hex(t.Pink) + "\",\"accent\":\"" + Hex(t.Accent) + "\",\"bg0\":\"" + Hex(t.Bg0) + "\",\"bg1\":\"" + Hex(t.Bg1) + "\"}";
    }
    public static void Send(ThemeDef t)
    {
        if (t == null) return; string token = Edition.Token; if (token.Length == 0) return;   // not linked (or a test): nothing to send
        string body = Body(t); if (body == lastSent) return; lastSent = body;
        ThreadPool.QueueUserWorkItem(o =>
        {
            try
            {
                var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/account/theme"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 10000; rq.ContentType = "application/json"; rq.Headers["Authorization"] = "Bearer " + token;
                var b = Encoding.UTF8.GetBytes(body); rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length);
                using (var r = (HttpWebResponse)rq.GetResponse()) { }
            }
            catch { lastSent = ""; }   // offline or refused: sent again next time
        });
    }
}
