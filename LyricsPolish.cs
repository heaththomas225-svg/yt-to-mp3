using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// Makes lyrics read properly: capital letters, "I", the apostrophes in don't / I'm / can't, spaces around commas and full stops, a full stop or question mark at the end of each verse line group,
// a comma before "and / but / so" lines, and tidy blank lines between the verses. It works on English words, changes nothing else, and says what it did.
// It is careful rather than clever: a line is only changed when the rule is clear, and punctuation in the middle of a verse is left the way a lyric sheet has it.
static class LyricsPolish
{
    static readonly Dictionary<string, string> Contr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"im","I'm"},{"ive","I've"},{"dont","don't"},{"cant","can't"},{"wont","won't"},{"didnt","didn't"},{"doesnt","doesn't"},{"isnt","isn't"},{"arent","aren't"},{"wasnt","wasn't"},{"werent","weren't"},
        {"couldnt","couldn't"},{"wouldnt","wouldn't"},{"shouldnt","shouldn't"},{"havent","haven't"},{"hasnt","hasn't"},{"hadnt","hadn't"},{"thats","that's"},{"whats","what's"},{"theres","there's"},
        {"heres","here's"},{"youre","you're"},{"theyre","they're"},{"youve","you've"},{"youll","you'll"},{"theyll","they'll"},{"theyve","they've"},{"weve","we've"},{"wouldve","would've"},
        {"couldve","could've"},{"shouldve","should've"},{"aint","ain't"},{"whos","who's"},{"hows","how's"},{"wheres","where's"},{"shes","she's"},{"hes","he's"},{"cause","'cause"},{"ima","I'ma"}
    };
    static readonly HashSet<string> Aux = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "do", "does", "did", "is", "are", "was", "were", "can", "could", "would", "will", "should", "am", "have", "has" };
    static readonly HashSet<string> Pron = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "you", "i", "we", "they", "he", "she", "it", "ya", "u", "this", "that", "there", "anybody", "anyone", "everybody", "somebody", "someone" };
    static readonly HashSet<string> Wh = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "what", "why", "how", "who", "where", "which", "whose", "when" };
    static readonly HashSet<string> WhPron = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "you", "ya", "u" };
    static readonly Regex Noise = new Regex(@"^[\s\[\(\*♪♫#]*(music|applause|laughter|laughing|singing|instrumental|silence|blank_audio|no speech|inaudible|humming|chanting)?[\s\]\)\*♪♫#]*$|^\s*(thanks for watching|subtitles by|transcribed by).*$", RegexOptions.IgnoreCase);
    static readonly Regex Section = new Regex(@"^\[[^\]]+\]$");

    public static bool IsSection(string line) { return Section.IsMatch((line ?? "").Trim()); }

    // the words only: tidy and punctuate
    public static string Polish(string raw, bool groupIfFlat, out string note, string lang = "auto")
    {
        bool en = lang == "en" || ((lang ?? "auto") == "auto" && LyricsLang.LooksEnglish(raw));   // the apostrophe, "I" and punctuation rules are for English: other languages only get spacing and capital letters
        int caps = 0, apos = 0, ends = 0, spaces = 0, dropped = 0, commas = 0;
        string t = (raw ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("﻿", "").Replace("​", "").Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace("…", "...");
        var lines = new List<string>();
        foreach (string l0 in t.Split('\n'))
        {
            string l = l0.Replace('♪', ' ').Replace('♫', ' ').Trim();
            if (l.Length == 0) { lines.Add(""); continue; }
            if (Noise.IsMatch(l)) { dropped++; continue; }
            if (IsSection(l)) { lines.Add(l); continue; }
            string before = l;
            l = Regex.Replace(l, @"[ \t]+", " "); l = Regex.Replace(l, @"\s+([,.;:!?])(?!\.)", "$1"); l = Regex.Replace(l, @"([,;!?])(?=[A-Za-z])", "$1 "); l = Regex.Replace(l, @"(?<=[a-z]{2})\.(?=[A-Z])", ". "); l = Regex.Replace(l, @",{2,}", ","); l = Regex.Replace(l, @"\(\s+", "("); l = Regex.Replace(l, @"\s+\)", ")");
            if (l != before) spaces++;
            if (en) l = Regex.Replace(l, @"\b[A-Za-z]+\b", m =>
            {
                string w = m.Value; string rep;
                if (w == "i") { caps++; return "I"; }
                if (Contr.TryGetValue(w, out rep))
                {
                    if (w.Equals("cause", StringComparison.OrdinalIgnoreCase)) { string pre = l.Substring(0, m.Index).TrimEnd(); if (pre.EndsWith("'") || !(pre.Length == 0 || pre.EndsWith(","))) return w; }   // 'cause only where a clause starts: "the cause of it" stays
                    apos++; if (w.ToUpperInvariant() == w && w.Length > 1) return rep.ToUpperInvariant(); if (char.IsUpper(w[0])) return char.ToUpperInvariant(rep[0]) + rep.Substring(1); return rep;
                }
                return w;
            });
            l = Regex.Replace(l, @"(?<=[.!?]\s)([a-z])", m => { caps++; return m.Value.ToUpperInvariant(); });   // a new sentence in the middle of a line
            int fi = FirstLetter(l); if (fi >= 0 && char.IsLower(l[fi])) { l = l.Substring(0, fi) + char.ToUpperInvariant(l[fi]) + l.Substring(fi + 1); caps++; }
            lines.Add(l);
        }
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
        for (int i = lines.Count - 1; i > 0; i--) if (lines[i].Length == 0 && lines[i - 1].Length == 0) lines.RemoveAt(i);   // one blank line between verses, never more
        // a recording has no blank lines: make verses of four lines so the labeller has something to work with
        bool flat = true; int count = 0; foreach (string l in lines) { if (l.Length == 0) flat = false; else count++; }
        if (groupIfFlat && flat && count >= 8)
        {
            var grouped = new List<string>(); int inGroup = 0; for (int i = 0; i < lines.Count; i++)
            {
                int left = lines.Count - i; if (inGroup == 4 && left >= 2) { grouped.Add(""); inGroup = 0; }
                grouped.Add(lines[i]); inGroup++;
            }
            lines = grouped;
        }
        // end marks: inside a verse only a clear question or a line leading into "and / but / so" gets one; the last line of a verse always ends properly
        if (en) for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i]; if (l.Length == 0 || IsSection(l)) continue;
            string next = i + 1 < lines.Count ? lines[i + 1] : ""; bool last = next.Length == 0 || IsSection(next); char c = l[l.Length - 1];
            bool ended = c == '.' || c == '!' || c == '?';
            if (last)
            {
                if (ended) continue;
                if (c == ',' || c == ';' || c == ':' || c == '-') l = l.Substring(0, l.Length - 1).TrimEnd();
                else if (c == '"' || c == ')' || c == '\'') { char b = l.Length > 1 ? l[l.Length - 2] : ' '; if (b == '.' || b == '!' || b == '?' || b == ',') continue; }
                l += IsQuestion(l) ? "?" : "."; lines[i] = l; ends++;
            }
            else if (!ended && c != ',' && c != ';' && c != ':' && c != '"' && c != ')' && c != '-')
            {
                if (IsQuestion(l)) { lines[i] = l + "?"; ends++; }
                else if (Regex.IsMatch(next, @"^(and|but|or|so|'cause|because|though|yet|while)\b", RegexOptions.IgnoreCase)) { lines[i] = l + ","; commas++; }
            }
        }
        var sb = new StringBuilder(); for (int i = 0; i < lines.Count; i++) { if (i > 0) sb.Append('\n'); sb.Append(lines[i]); }
        var parts = new List<string>(); if (caps > 0) parts.Add(caps + (caps == 1 ? " capital letter" : " capital letters")); if (apos > 0) parts.Add(apos + (apos == 1 ? " apostrophe" : " apostrophes")); if (spaces > 0) parts.Add("spacing on " + spaces + (spaces == 1 ? " line" : " lines"));
        if (ends > 0) parts.Add(ends + (ends == 1 ? " end mark" : " end marks")); if (commas > 0) parts.Add(commas + (commas == 1 ? " comma" : " commas")); if (dropped > 0) parts.Add(dropped + (dropped == 1 ? " noise line removed" : " noise lines removed"));
        note = parts.Count == 0 ? "The grammar and punctuation already looked right." : "Grammar and punctuation: fixed " + string.Join(", ", parts.ToArray()) + ".";
        if (!en) note = "Not English (" + LyricsLang.NameOf(lang, raw) + "): only spacing and capital letters were tidied, the apostrophe and punctuation rules are for English." + (parts.Count > 0 ? " Fixed " + string.Join(", ", parts.ToArray()) + "." : "");
        return sb.ToString();
    }

    static int FirstLetter(string l) { for (int i = 0; i < l.Length; i++) { if (char.IsLetter(l[i])) return i; if (char.IsDigit(l[i])) return -1; } return -1; }

    static List<string> Words(string l) { var w = new List<string>(); foreach (Match m in Regex.Matches(l, @"[A-Za-z']+")) w.Add(m.Value); return w; }

    // "Do you love me", "Where do you go", "Why you running": a question; "What I want is you" is not
    public static bool IsQuestion(string line)
    {
        var w = Words(line); if (w.Count < 3) return false; string a = w[0], b = w[1];
        if (Aux.Contains(a) && Pron.Contains(b)) return true;
        if (Wh.Contains(a) && Aux.Contains(b)) return true;
        if ((a.Equals("why", StringComparison.OrdinalIgnoreCase) || a.Equals("where", StringComparison.OrdinalIgnoreCase) || a.Equals("how", StringComparison.OrdinalIgnoreCase) || a.Equals("what", StringComparison.OrdinalIgnoreCase)) && WhPron.Contains(b)) return true;
        return false;
    }

    // the whole job for the generator: polish, then label the verses and chorus
    public static string Finish(string raw, bool fromRecording, out string note, string lang = "auto")
    {
        string pn; string pol = Polish(raw, fromRecording, out pn, lang); if (pol.Trim().Length == 0) { note = pn; return ""; }
        int added; string sn; string lab = LyricSections.Add(pol, out added, out sn); note = pn + " " + sn.Replace(" Ctrl+Z undoes it.", ""); return lab;
    }
}

// the languages the speech recogniser knows, for the language box of the Generate Lyrics page ("auto" lets it listen and decide)
static class LyricsLang
{
    public static readonly string[] Codes = { "auto", "en", "es", "fr", "de", "it", "pt", "nl", "ru", "uk", "pl", "tr", "ar", "he", "hi", "bn", "ur", "fa", "id", "ms", "vi", "th", "zh", "ja", "ko", "sv", "no", "da", "fi", "el", "cs", "ro", "hu", "bg", "sr", "hr", "sk", "sl", "lt", "lv", "et", "ca", "cy", "ga", "is", "sq", "hy", "az", "ka", "kk", "sw", "ta", "te", "mr", "gu", "pa", "kn", "ml", "ne", "si", "tl", "af", "la", "eu", "gl", "mt", "so", "yo", "zu" };
    public static readonly string[] Names = { "Auto-detect", "English", "Spanish", "French", "German", "Italian", "Portuguese", "Dutch", "Russian", "Ukrainian", "Polish", "Turkish", "Arabic", "Hebrew", "Hindi", "Bengali", "Urdu", "Persian", "Indonesian", "Malay", "Vietnamese", "Thai", "Chinese", "Japanese", "Korean", "Swedish", "Norwegian", "Danish", "Finnish", "Greek", "Czech", "Romanian", "Hungarian", "Bulgarian", "Serbian", "Croatian", "Slovak", "Slovenian", "Lithuanian", "Latvian", "Estonian", "Catalan", "Welsh", "Irish", "Icelandic", "Albanian", "Armenian", "Azerbaijani", "Georgian", "Kazakh", "Swahili", "Tamil", "Telugu", "Marathi", "Gujarati", "Punjabi", "Kannada", "Malayalam", "Nepali", "Sinhala", "Filipino", "Afrikaans", "Latin", "Basque", "Galician", "Maltese", "Somali", "Yoruba", "Zulu" };
    public static bool Valid(string code) { return Array.IndexOf(Codes, code ?? "") >= 0; }
    public static string Clean(string code) { code = (code ?? "auto").Trim().ToLowerInvariant(); return Valid(code) ? code : "auto"; }
    public static string NameOf(string code, string textForAuto = null)
    {
        code = Clean(code); if (code == "auto") return textForAuto != null && LooksEnglish(textForAuto) ? "English" : "another language"; int i = Array.IndexOf(Codes, code); return Names[i];
    }
    static readonly System.Collections.Generic.HashSet<string> Stop = new System.Collections.Generic.HashSet<string> { "the", "and", "you", "i", "to", "it", "my", "in", "of", "is", "on", "that", "this", "for", "with", "be", "not", "but", "we", "your", "all", "just", "like", "ill", "im", "dont", "cant", "know", "got", "now", "when", "can", "what", "they", "up", "if", "are", "was", "will", "have", "he", "she", "her", "his", "out", "get", "one", "youre", "thats", "want", "need", "never", "time", "feel", "night", "life" };
    // is this text English? Counts how many of its words are common English words and how many letters are in other alphabets
    public static bool LooksEnglish(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true; int words = 0, hits = 0, letters = 0, ascii = 0;
        foreach (char c in text) if (char.IsLetter(c)) { letters++; if (c < 128) ascii++; }
        if (letters > 0 && ascii < letters * 0.85) return false;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[a-z']+")) { words++; if (Stop.Contains(m.Value.Replace("'", ""))) hits++; }
        return words < 12 || hits >= words * 0.18;
    }
}
