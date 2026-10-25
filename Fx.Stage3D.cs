using System;

// =====================================================================================================
//  3D SOUND STAGE (the plugin behind the 3D STAGE page): puts a track at a place round your head and plays it there in binaural stereo
//  (for headphones): which way it is (azimuth), how high (elevation) and how far (distance). Every setting can be automated, so a sound
//  can fly round the room as the song plays.
//  How it is heard: equal-power panning from the angle, the far ear a little later (time difference) and duller (head shadow), sounds
//  behind slightly darker, sounds above slightly brighter, and distance makes it quieter, softer (air) and wetter (more room).
// =====================================================================================================
class Stage3DFx : Fx
{
    double az, el, dist, width, roomAmt, air; float[] dL = new float[512], dR = new float[512]; int dw; double lpL, lpR, shL, shR, hsState, airL, airR;
    Reverb rv = new Reverb(); float[] wl = new float[4096], wr = new float[4096];
    public Stage3DFx()
    {
        Type = "stage3d"; Title = "3D Sound Stage"; Category = "Space"; Blurb = "Place the track anywhere round your head (headphones)";
        AddP("az", "Direction", -180, 180, 0, "deg"); AddP("el", "Height", -60, 60, 0, "deg"); AddP("dist", "Distance", 0.5f, 12, 1.5f, "m");
        AddP("width", "Source width", 0, 100, 20, "%"); AddP("room", "Room", 0, 60, 18, "%"); AddP("air", "Air (far = softer)", 0, 100, 60, "%");
        rv.Prepare(StudioEngine.SR);
    }
    protected override void Update()
    {
        az = Get("az") * Math.PI / 180; el = Get("el") * Math.PI / 180; dist = Math.Max(0.5, Get("dist")); width = Get("width") / 100.0; air = Get("air") / 100.0;
        roomAmt = Math.Min(60, Get("room") * (0.6 + 0.4 * Math.Min(1, dist / 6)));   // further away = more of the room
        rv.Set("mix", (float)roomAmt); rv.Set("size", 55); rv.Set("damp", 55); rv.Set("pre", (float)Math.Min(40, 4 + dist * 3)); rv.Set("lc", 150); rv.Set("hc", 8000);
    }
    public override void Reset() { base.Reset(); Array.Clear(dL, 0, dL.Length); Array.Clear(dR, 0, dR.Length); lpL = lpR = shL = shR = hsState = airL = airR = 0; rv.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); if (n > wl.Length) { wl = new float[n]; wr = new float[n]; }
        double sr = SR, pan = Math.Sin(az) * Math.Cos(el), behind = (1 - Math.Cos(az)) * 0.5, up = Math.Sin(el);
        double gain = Math.Min(1.6, 1.5 / dist) * Math.Pow(10, -2.5 * behind / 20.0);   // the inverse distance law (gentle near the head)
        double farFc = 18000 - 13000 * Math.Abs(pan), farA = 1 - Math.Exp(-2 * Math.PI * Math.Max(1500, farFc) / sr);
        double backFc = 20000 - 12000 * behind, backA = 1 - Math.Exp(-2 * Math.PI * Math.Max(3000, backFc) / sr);
        double airFc = 20000 / (1 + air * (dist - 0.5) * 0.35), airA = 1 - Math.Exp(-2 * Math.PI * Math.Max(2500, airFc) / sr);
        double hsA = 1 - Math.Exp(-2 * Math.PI * 4000 / sr), hsGain = up * 0.5;   // above = a little brighter, below = a little duller
        double pl = Math.Cos((pan + 1) * Math.PI / 4), pr = Math.Sin((pan + 1) * Math.PI / 4), delay = Math.Abs(pan) * 0.00065 * sr;
        for (int i = 0; i < n; i++)
        {
            double inL = l[i], inR = r[i], m = 0.5 * (inL + inR), side = 0.5 * (inL - inR) * width;
            hsState += hsA * (m - hsState); m += (m - hsState) * hsGain;   // the height tilt
            int wi = dw & 511; dL[wi] = (float)m; dR[wi] = (float)m;
            double xl = Tap(dL, pan > 0 ? delay : 0), xr = Tap(dR, pan < 0 ? delay : 0); dw++;
            if (pan > 0) { shL += farA * (xl - shL); xl = shL; } else { shR += farA * (xr - shR); xr = shR; }   // head shadow on the far ear
            lpL += backA * (xl - lpL); lpR += backA * (xr - lpR); airL += airA * (lpL - airL); airR += airA * (lpR - airR);
            wl[i] = (float)((airL * pl * 1.414 + side) * gain); wr[i] = (float)((airR * pr * 1.414 - side) * gain);
        }
        if (roomAmt > 0.5) rv.Process(wl, wr, n);
        Array.Copy(wl, l, n); Array.Copy(wr, r, n);
    }
    float Tap(float[] b, double delay) { double rp = dw - delay; int i0 = (int)Math.Floor(rp); double f = rp - i0; float a = b[i0 & 511], c = b[(i0 + 1) & 511]; return (float)(a * (1 - f) + c * f); }
}
