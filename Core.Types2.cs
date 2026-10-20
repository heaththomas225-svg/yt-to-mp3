using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// More shared engine types (automation points)
// ======================================================================
//  Parameter automation: a setting of a plugin follows a curve while the song plays (and in every export).
//  Each automated setting has a "lane": points of (song position, value) joined by straight lines (steps for a choice list).
// ======================================================================
class AutoPoint { public long Frame; public float Value; }
