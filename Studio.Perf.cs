using System;
using System.Collections.Generic;
using System.Diagnostics;

// A tiny stopwatch for finding what makes a frame slow (off unless PerfTest turns it on).
static class Prof
{
    public static bool On; static readonly Stopwatch sw = Stopwatch.StartNew(); static double last; public static readonly Dictionary<string, double> Totals = new Dictionary<string, double>();
    public static void Start() { if (On) last = sw.Elapsed.TotalMilliseconds; }
    public static readonly Dictionary<string, double> Acc = new Dictionary<string, double>(); public static readonly Dictionary<string, int> Cnt = new Dictionary<string, int>();
    public static long T() { return On ? Stopwatch.GetTimestamp() : 0; }
    public static void A(string name, long t0) { if (!On) return; double ms = t0 == 0 ? 0.5 : (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; double v; Acc.TryGetValue(name, out v); Acc[name] = v + ms; int c; Cnt.TryGetValue(name, out c); Cnt[name] = c + 1; }
    public static readonly List<string> Notes = new List<string>(); public static void Note(string s) { if (Notes.Count < 80) Notes.Add(s); }
    public static void Mark(string name) { if (!On) return; double n = sw.Elapsed.TotalMilliseconds; double v; Totals.TryGetValue(name, out v); Totals[name] = v + (n - last); last = n; }
}
