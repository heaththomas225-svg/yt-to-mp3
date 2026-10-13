using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

sealed class PlaceHit { public string Label = "", Value = ""; }

// "Where you are": search any country, county, town or area (Poole, Dorset). The countries are in the program (they work offline); everything else is searched through the website.
static class PlaceApi
{
    public static readonly string[] Countries = ("Afghanistan|Albania|Algeria|Andorra|Angola|Antigua and Barbuda|Argentina|Armenia|Australia|Austria|Azerbaijan|Bahamas|Bahrain|Bangladesh|Barbados|Belarus|Belgium|Belize|Benin|Bhutan|Bolivia|Bosnia and Herzegovina|Botswana|Brazil|Brunei|Bulgaria|Burkina Faso|Burundi|Cambodia|Cameroon|Canada|Cape Verde|Central African Republic|Chad|Chile|China|Colombia|Comoros|Congo|Costa Rica|Croatia|Cuba|Cyprus|Czechia|Denmark|Djibouti|Dominica|Dominican Republic|DR Congo|Ecuador|Egypt|El Salvador|England|Equatorial Guinea|Eritrea|Estonia|Eswatini|Ethiopia|Fiji|Finland|France|Gabon|Gambia|Georgia|Germany|Ghana|Greece|Grenada|Guatemala|Guinea|Guinea-Bissau|Guyana|Haiti|Honduras|Hong Kong|Hungary|Iceland|India|Indonesia|Iran|Iraq|Ireland|Israel|Italy|Ivory Coast|Jamaica|Japan|Jordan|Kazakhstan|Kenya|Kiribati|Kosovo|Kuwait|Kyrgyzstan|Laos|Latvia|Lebanon|Lesotho|Liberia|Libya|Liechtenstein|Lithuania|Luxembourg|Macao|Madagascar|Malawi|Malaysia|Maldives|Mali|Malta|Marshall Islands|Mauritania|Mauritius|Mexico|Micronesia|Moldova|Monaco|Mongolia|Montenegro|Morocco|Mozambique|Myanmar|Namibia|Nauru|Nepal|Netherlands|New Zealand|Nicaragua|Niger|Nigeria|North Korea|North Macedonia|Northern Ireland|Norway|Oman|Pakistan|Palau|Palestine|Panama|Papua New Guinea|Paraguay|Peru|Philippines|Poland|Portugal|Puerto Rico|Qatar|Romania|Russia|Rwanda|Saint Kitts and Nevis|Saint Lucia|Saint Vincent and the Grenadines|Samoa|San Marino|Sao Tome and Principe|Saudi Arabia|Scotland|Senegal|Serbia|Seychelles|Sierra Leone|Singapore|Slovakia|Slovenia|Solomon Islands|Somalia|South Africa|South Korea|South Sudan|Spain|Sri Lanka|Sudan|Suriname|Sweden|Switzerland|Syria|Taiwan|Tajikistan|Tanzania|Thailand|Timor-Leste|Togo|Tonga|Trinidad and Tobago|Tunisia|Turkey|Turkmenistan|Tuvalu|Uganda|Ukraine|United Arab Emirates|UK|USA|Uruguay|Uzbekistan|Vanuatu|Vatican City|Venezuela|Vietnam|Wales|Yemen|Zambia|Zimbabwe").Split('|');
    // the search through the website; a test can replace it
    public static Func<string, List<PlaceHit>> Fetch = q => ProfApi.Places(q);
    public static List<PlaceHit> LocalCountries(string q)
    {
        q = (q ?? "").Trim().ToLowerInvariant(); var l = new List<PlaceHit>(); if (q.Length < 1) return l;
        foreach (var c in Countries) if (c.ToLowerInvariant().StartsWith(q)) l.Add(new PlaceHit { Label = c, Value = c });
        foreach (var c in Countries) if (!c.ToLowerInvariant().StartsWith(q) && c.ToLowerInvariant().Contains(q)) l.Add(new PlaceHit { Label = c, Value = c });
        return l.Take(8).ToList();
    }
}
static partial class ProfApi
{
    public static List<PlaceHit> Places(string q)
    {
        var l = new List<PlaceHit>();
        try
        {
            var o = MiniJson.Parse(Http("GET", "/api/places?q=" + Uri.EscapeDataString(q), null)) as Dictionary<string, object>; object ps;
            if (o != null && o.TryGetValue("places", out ps) && ps is List<object>) foreach (var x in (List<object>)ps) { var d = x as Dictionary<string, object>; if (d == null) continue; string lab = S(d, "label"), val = S(d, "value"); if (val.Length > 0 && l.Count < 12) l.Add(new PlaceHit { Label = (lab.Length > 0 ? lab : val).Replace('<', ' ').Replace('>', ' '), Value = val.Replace('<', ' ').Replace('>', ' ') }); }
        }
        catch { return null; }
        return l;
    }
}

static class PlacePicker
{
    // returns the place to show on the profile, "" to clear it, or null when cancelled
    public static string Show(IWin32Window owner, string current)
    {
        string result = null; var f = StudioDialogs.Make("Where you are", 580, 520); var Fld = TM.C(255, 34, 16, 62); var Ink = TM.C(255, 240, 232, 255);
        f.Controls.Add(new Label { Text = "Type a letter and every country and place that starts with it shows up, anywhere in the world: towns, counties, areas (try Poole, Dorset). Click one in the list and it is yours.", Left = 16, Top = 12, Width = 548, Height = 40, ForeColor = TM.C(255, 200, 170, 255) });
        var q = new TextBox { Left = 16, Top = 56, Width = 548, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12f), MaxLength = 60 }; Win.NeonBox(q); f.Controls.Add(q);
        var list = new ListBox { Left = 16, Top = 94, Width = 548, Height = 270, BackColor = TM.C(255, 14, 7, 30), ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 27, Font = new Font("Segoe UI", 10.5f), IntegralHeight = false }; Win.NeonBox(list); Win.DarkControl(list); f.Controls.Add(list);
        var status = new Label { Left = 16, Top = 368, Width = 548, Height = 22, ForeColor = TM.C(255, 160, 140, 200), Text = "Type a letter: the countries and places that start with it show at once." }; f.Controls.Add(status);
        f.Controls.Add(new Label { Text = "SHOWN ON YOUR PROFILE (you can change the words)", Left = 16, Top = 396, Width = 548, Height = 18, ForeColor = TM.C(255, 200, 170, 255), Font = new Font("Segoe UI Semibold", 9.5f) });
        var val = new TextBox { Left = 16, Top = 418, Width = 548, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12f), MaxLength = 30, Text = current ?? "" }; Win.NeonBox(val); f.Controls.Add(val);
        var ok = StudioDialogs.B("USE THIS", 300, 468, 120, 36, TM.C(255, 118, 52, 212)); var clear = StudioDialogs.B("CLEAR", 430, 468, 64, 36, TM.C(255, 150, 40, 80)); var cancel = StudioDialogs.B("Cancel", 502, 468, 62, 36, TM.C(255, 70, 40, 120)); f.Controls.AddRange(new Control[] { ok, clear, cancel });
        var hits = new List<PlaceHit>(); int token = 0;
        list.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= hits.Count) return; bool on = (e.State & DrawItemState.Selected) != 0; using (var br = new SolidBrush(on ? TM.C(255, 118, 52, 212) : TM.C(255, 14, 7, 30))) e.Graphics.FillRectangle(br, e.Bounds);
            TextRenderer.DrawText(e.Graphics, hits[e.Index].Label, list.Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), on ? Color.White : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        Action show = () => { list.BeginUpdate(); list.Items.Clear(); foreach (var h in hits) list.Items.Add(h.Label); list.EndUpdate(); };
        list.SelectedIndexChanged += (s, e) => { if (list.SelectedIndex >= 0 && list.SelectedIndex < hits.Count) val.Text = hits[list.SelectedIndex].Value; };
        var wait = new System.Windows.Forms.Timer { Interval = 450 };
        wait.Tick += (s, e) =>
        {
            wait.Stop(); string text = q.Text.Trim(); int my = ++token; if (text.Length < 1) return; status.Text = "Searching...";
            ThreadPool.QueueUserWorkItem(o => { var found = PlaceApi.Fetch(text); try { f.BeginInvoke(new Action(() => { if (my != token || f.IsDisposed) return; if (found == null) { status.Text = "Could not reach the search. Countries still work; or type your place below."; return; } var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); hits.Clear(); foreach (var h in found.Concat(PlaceApi.LocalCountries(text))) if (seen.Add(h.Value)) hits.Add(h); show(); if (hits.Count > 0 && !val.Focused) list.SelectedIndex = 0; status.Text = hits.Count == 0 ? "Nothing found. Try another spelling, or type your place below." : hits.Count + " found: pick one."; })); } catch { } });
        };
        q.TextChanged += (s, e) => { token++; string text = q.Text.Trim(); if (text.Length < 1) { hits.Clear(); show(); status.Text = "Type a letter: the countries and places that start with it show at once."; return; } hits.Clear(); foreach (var h in PlaceApi.LocalCountries(text)) hits.Add(h); show(); wait.Stop(); wait.Start(); };
        list.MouseClick += (s, e) => { int ix = list.IndexFromPoint(e.Location); if (ix >= 0 && ix < hits.Count && hits[ix].Value.Trim().Length > 0) { result = hits[ix].Value.Trim(); f.Close(); } };   // one click on a place picks it and closes the box
        list.DoubleClick += (s, e) => { if (list.SelectedIndex >= 0 && val.Text.Trim().Length > 0) { result = val.Text.Trim(); f.Close(); } };
        ok.Click += (s, e) => { result = val.Text.Trim(); f.Close(); }; clear.Click += (s, e) => { result = ""; f.Close(); }; cancel.Click += (s, e) => f.Close(); f.CancelButton = cancel; f.AcceptButton = ok;
        f.FormClosed += (s, e) => wait.Stop(); f.Shown += (s, e) => q.Focus(); f.ShowDialog(owner); return result;
    }
}

partial class StudioForm
{
    void PickLocation()
    {
        string r = PlacePicker.Show(this, ProfileStore.Location); if (r == null) return; r = EmojiText.Cut(r, 30);
        ProfileStore.Location = r; ProfileStore.Save(); ProjectLibrary.Save(); QueueRepublish(); Toast(r.Length == 0 ? "Your place was cleared." : "You are in " + r + ".");
    }
}
