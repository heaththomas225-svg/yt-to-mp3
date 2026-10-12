using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The MASTER VISUALISER. It is the heart of the Mastering page (the stage above the EQ and the presets) and it also has a full page of its own (tab 5) for watching.
// A ring of light bars around the logo dances to the spectrum, shells of light ripple out behind it, the sound before mastering is a thin ring inside, a circular scope
// draws the real waveform, every beat sends shockwaves and sparks, stars rush past faster the harder the bass hits, and a mirrored spectrum with peak caps runs along the floor.
// Everything is drawn with cheap shapes and stored pictures (a big soft gradient fill alone used to take 16 ms a frame).
partial class StudioForm
{
    const int VisN = 96;
    readonly float[] visB = new float[VisN], visPre = new float[VisN], visPk = new float[VisN]; readonly float[] visWave = new float[200];
    readonly float[][] visEcho = new float[5][]; double visEchoAt; int visEchoHead;
    double[] visRe = new double[2048], visIm = new double[2048], visHann; float visBeat, visBass, visBassAvg, visMid, visHigh, visFlash, visRot, visHoldL, visHoldR, visShake; double visT; int visFrame; double visBeatAt; bool visStageOn;
    readonly List<Spark> visSp = new List<Spark>(); readonly List<float[]> visRings = new List<float[]>(); readonly Random visRnd = new Random(77);
    readonly float[] stA = new float[150], stD = new float[150], stS = new float[150]; bool starsInit;

    static Color MixC(Color a, Color b, float t) { t = Math.Max(0, Math.Min(1, t)); return Color.FromArgb(a.A + (int)((b.A - a.A) * t), a.R + (int)((b.R - a.R) * t), a.G + (int)((b.G - a.G) * t), a.B + (int)((b.B - a.B) * t)); }
    // the colour of one place along the ring: theme cyan, then violet, then pink
    static Color VisCol(float t) { return t < 0.5f ? MixC(NeonCyan, NeonViolet, t * 2) : MixC(NeonViolet, NeonPink, (t - 0.5f) * 2); }

    // 96 bands from 30 Hz to 18 kHz (low to high), quick to rise and smooth to fall
    void VisBands(float[] tap, int pos, float[] outB, bool playing)
    {
        if (!playing || tap == null) { for (int i = 0; i < outB.Length; i++) outB[i] *= 0.9f; return; }
        const int N = 2048; if (visHann == null) { visHann = new double[N]; for (int i = 0; i < N; i++) visHann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N); }
        for (int i = 0; i < N; i++) { visRe[i] = tap[(pos - N + i + 8192 * 2) & 8191] * visHann[i]; visIm[i] = 0; }
        Fft.Transform(visRe, visIm, false);
        for (int b = 0; b < outB.Length; b++)
        {
            double f0 = 30 * Math.Pow(600.0, b / (double)outB.Length), f1 = 30 * Math.Pow(600.0, (b + 1) / (double)outB.Length);
            int k0 = Math.Max(1, (int)(f0 * N / StudioEngine.SR)), k1 = Math.Max(k0 + 1, (int)(f1 * N / StudioEngine.SR)); double mx = 0, sum = 0; int c = 0;
            for (int k = k0; k < k1 && k < N / 2; k++) { double m = Math.Sqrt(visRe[k] * visRe[k] + visIm[k] * visIm[k]); if (m > mx) mx = m; sum += m; c++; }
            double v = c == 0 ? 0 : mx * 0.6 + (sum / c) * 0.4; double db = 20 * Math.Log10(v / (N / 4.0) + 1e-9); float nv = (float)Math.Max(0, Math.Min(1, (db + 78) / 60 + b / (double)outB.Length * 0.30));
            outB[b] = nv > outB[b] ? outB[b] + (nv - outB[b]) * 0.75f : outB[b] * 0.9f + nv * 0.1f;
        }
    }

    // a soft round glow, made once (per size and colour) and copied; it replaces a gradient fill that cost 16 ms a frame
    Bitmap AuraSprite(int dia, Color c, int peak)
    {
        long key = Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.Seed, 91), dia), c.ToArgb()), peak); var sp = Sprites.Get(key); if (sp != null) return sp;
        sp = Sprites.Make(dia, dia); var px = new int[dia * dia]; byte[] mask = AuraMask(dia); int cr = c.R, cg = c.G, cb = c.B;
        for (int i = 0; i < px.Length; i++) { int m = mask[i]; if (m == 0) continue; int al = m * peak / 255; px[i] = (al << 24) | ((cr * al / 255) << 16) | ((cg * al / 255) << 8) | (cb * al / 255); }
        var d = sp.LockBits(new Rectangle(0, 0, dia, dia), System.Drawing.Imaging.ImageLockMode.WriteOnly, sp.PixelFormat); System.Runtime.InteropServices.Marshal.Copy(px, 0, d.Scan0, px.Length); sp.UnlockBits(d); Sprites.Put(key, sp); return sp;
    }
    readonly Dictionary<int, byte[]> auraMasks = new Dictionary<int, byte[]>();
    byte[] AuraMask(int dia)
    {   // the shape of the glow (how strong it is at each place), worked out once per size
        byte[] m; if (auraMasks.TryGetValue(dia, out m)) return m; if (auraMasks.Count > 12) auraMasks.Clear(); m = new byte[dia * dia]; float R = dia / 2f;
        for (int y = 0; y < dia; y++) for (int x = 0; x < dia; x++) { float dx = x + 0.5f - R, dy = y + 0.5f - R, r = (float)Math.Sqrt(dx * dx + dy * dy) / R; if (r >= 1) continue; float a = (1 - r); a = a * a * (0.55f + 0.45f * (float)Math.Cos(r * 6.0)); m[y * dia + x] = (byte)Math.Max(0, Math.Min(255, (int)(a * 255))); }
        auraMasks[dia] = m; return m;
    }
    static Color Coarse(Color c) { return Color.FromArgb(255, Math.Min(255, (c.R + 16) / 32 * 32), Math.Min(255, (c.G + 16) / 32 * 32), Math.Min(255, (c.B + 16) / 32 * 32)); }
    Bitmap SparkSprite(Color c)
    {
        long key = Sprites.H(Sprites.H(Sprites.Seed, 92), c.ToArgb()); var sp = Sprites.Get(key); if (sp != null) return sp;
        sp = Sprites.Make(14, 14); using (var sg = Graphics.FromImage(sp)) { sg.SmoothingMode = SmoothingMode.AntiAlias; using (var b = new SolidBrush(Color.FromArgb(70, c))) sg.FillEllipse(b, 0, 0, 14, 14); using (var b = new SolidBrush(Color.FromArgb(150, c))) sg.FillEllipse(b, 3, 3, 8, 8); using (var b = new SolidBrush(Color.FromArgb(255, Math.Min(255, c.R + 90), Math.Min(255, c.G + 90), Math.Min(255, c.B + 90)))) sg.FillEllipse(b, 5, 5, 4, 4); }
        Sprites.Put(key, sp); return sp;
    }

    Bitmap visBg; long visBgKey;
    void VisBackground(Graphics g, RectangleF a, float cx, float cy, float R0)
    {
        int w = (int)Math.Ceiling(a.Width), h = (int)Math.Ceiling(a.Height); long key = Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.Seed, w), h), (long)(cx - a.X)), (long)(cy - a.Y)), (long)R0 / 4), Coarse(NeonViolet).ToArgb());
        if (visBg == null || visBgKey != key)
        {
            if (visBg == null || visBg.Width != w || visBg.Height != h) { if (visBg != null) visBg.Dispose(); visBg = Sprites.Make(w, h); }
            using (var bg = Graphics.FromImage(visBg))
            {
                bg.CompositingMode = CompositingMode.SourceCopy; using (var b = new SolidBrush(TM.C(255, 5, 2, 12))) bg.FillRectangle(b, 0, 0, w, h); bg.CompositingMode = CompositingMode.SourceOver;
                int dia = (int)(R0 * 5.6f) / 16 * 16; if (dia >= 64) { var au = AuraSprite(dia, Coarse(NeonViolet), 215); bg.DrawImageUnscaled(au, (int)(cx - a.X - dia / 2f), (int)(cy - a.Y - dia / 2f)); int d3 = (int)(R0 * 3.2f) / 16 * 16; if (d3 >= 48) { var au3 = AuraSprite(d3, Coarse(NeonPink), 120); bg.DrawImageUnscaled(au3, (int)(cx - a.X - d3 / 2f), (int)(cy - a.Y - d3 / 2f)); } }
            }
            visBgKey = key; Prof.A("vis.bgbuild", 0);
        }
        long bq = Prof.T(); g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(visBg, (int)Math.Round(a.X), (int)Math.Round(a.Y)); g.CompositingMode = CompositingMode.SourceOver; Prof.A("vis.bgblit", bq);
    }

    // ---------- the stage: the light show itself (inside rectangle a) ----------
    // waves flowing out of the logo along a bar (cyan to the left, pink to the right): the same as the scroll bars
    void VizWaves(Graphics g, RectangleF inner, RectangleF ob, float logoCx, float lhw)
    {
        bool playing = eng.Playing; VizUpdate(playing); if (!playing) return; float cy = ob.Y + ob.Height / 2;
        var st = g.Save(); g.SetClip(inner, CombineMode.Intersect); g.SmoothingMode = SmoothingMode.AntiAlias; float maxH = ob.Height / 2 - 3; int hl = vizHist.Length;
        for (int c = 0; c < 190; c++)
        {
            float v = vizHist[(vizHead - 1 - c + hl * 4) % hl]; float amp = Math.Max(1f, v * maxH * (1f - c / 260f) * (0.75f + 0.25f * vizBand[(c * 3) % vizBand.Length])); float fade = 1f - c / 200f; int al = (int)(235 * fade); if (al <= 4) break;
            float xl = logoCx - lhw - 6 - c * 2.2f, xr = logoCx + lhw + 6 + c * 2.2f;
            using (var pl = new Pen(TM.C(al, 0, 229, 255), 1.5f)) g.DrawLine(pl, xl, cy - amp, xl, cy + amp);
            using (var pr = new Pen(TM.C(al, 255, 43, 214), 1.5f)) g.DrawLine(pr, xr, cy - amp, xr, cy + amp);
        }
        g.Restore(st);
    }
    void PaintVisStage(Graphics g, RectangleF a, bool full)
    {
        float dt = Math.Min(0.05f, hDt); bool playing = eng.Playing; visT += dt; visFrame++;
        var gs = g.Save(); g.SetClip(a, CombineMode.Intersect); long v0 = Prof.T();
        // ---- listening: the sound after mastering, and (every other frame) the sound before it ----
        VisBands(eng.MasterTap, eng.MasterTapPos, visB, playing); if ((visFrame & 1) == 0) VisBands(eng.MasterTapPre, eng.MasterTapPos, visPre, playing);
        float bass = 0, mid = 0, hi = 0; for (int i = 0; i < 14; i++) bass += visB[i]; for (int i = 28; i < 56; i++) mid += visB[i]; for (int i = 70; i < 96; i++) hi += visB[i]; bass /= 14f; mid /= 28f; hi /= 26f;
        visBass += (bass - visBass) * 0.55f; visBassAvg += (bass - visBassAvg) * 0.08f; visMid += (mid - visMid) * 0.3f; visHigh += (hi - visHigh) * 0.3f;
        bool beat = playing && bass > visBassAvg * 1.2f + 0.03f && bass > 0.26f && visT - visBeatAt > 0.17;
        if (beat) { visBeat = 1f; visFlash = 1f; visBeatAt = visT; visShake = 1f; }
        visBeat = Math.Max(0, visBeat - dt * 3.2f); visFlash = Math.Max(0, visFlash - dt * 5f); visShake = Math.Max(0, visShake - dt * 6f);
        for (int i = 0; i < VisN; i++) visPk[i] = Math.Max(visB[i], visPk[i] - dt * 0.5f);
        visRot += dt * (0.10f + visMid * 0.9f + visBeat * 0.9f);
        if (visT - visEchoAt > 0.07) { visEchoAt = visT; visEchoHead = (visEchoHead + 1) % visEcho.Length; if (visEcho[visEchoHead] == null) visEcho[visEchoHead] = new float[VisN]; Array.Copy(visB, visEcho[visEchoHead], VisN); }
        float floorUp = full ? Math.Min(150f, a.Height * 0.2f) : Math.Min(64f, a.Height * 0.2f), seekH = full ? 48f : 42f;
        float topPad = full ? 64f : 4f; float cx = a.X + a.Width / 2 + (float)Math.Sin(visT * 60) * 2.5f * visShake, cy = a.Y + topPad + (a.Height - topPad - seekH - floorUp * 0.35f) / 2f + (float)Math.Cos(visT * 53) * 2f * visShake;
        float R0 = Math.Max(30f, Math.Min((a.Width - (full ? 560f : 40f)) / 2f / 2.3f, (a.Height - topPad - seekH - floorUp * 0.5f - 24f) / 2f / 2.25f));
        Prof.A("vis.fft+setup", v0); v0 = Prof.T();
        // ---- the background (dark, with the soft glow behind the ring) is one stored picture, copied ----
        VisBackground(g, a, a.X + a.Width / 2, cy - (float)Math.Cos(visT * 53) * 2f * visShake, R0); Prof.A("vis.bg", v0); v0 = Prof.T();
        // ---- stars rushing out from the middle ----
        if (!starsInit) { starsInit = true; for (int i = 0; i < stA.Length; i++) { stA[i] = (float)(visRnd.NextDouble() * Math.PI * 2); stD[i] = (float)visRnd.NextDouble(); stS[i] = 0.05f + (float)visRnd.NextDouble() * 0.2f; } }
        { float maxR = (float)Math.Sqrt(a.Width * a.Width + a.Height * a.Height) / 2f; float spd = playing ? 1f + visBass * 5f + visBeat * 3f : 0.25f; var sm0 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
          using (var sb = new SolidBrush(Color.White)) using (var pen = new Pen(Color.White, 1f))
            for (int i = 0; i < stA.Length; i++)
            {
                stD[i] += stS[i] * spd * dt; if (stD[i] > 1f) { stD[i] = 0.02f; stA[i] = (float)(visRnd.NextDouble() * Math.PI * 2); }
                float d = stD[i], ca = (float)Math.Cos(stA[i]), sa = (float)Math.Sin(stA[i]); float x = cx + ca * d * maxR, y = cy + sa * d * maxR; if (!a.Contains(x, y)) continue; int al = (int)(Math.Min(1f, d * 2.4f) * (playing ? 150 : 70));
                Color sc = i % 3 == 0 ? NeonCyan : (i % 3 == 1 ? NeonPink : Color.White);
                if (playing && spd > 3.2f && d > 0.12f) { pen.Color = Color.FromArgb(al / 2, sc); g.DrawLine(pen, x, y, x - ca * spd * 5f * d, y - sa * spd * 5f * d); }
                sb.Color = Color.FromArgb(al, sc); float sz = d > 0.55f ? 2.2f : 1.4f; g.FillRectangle(sb, x, y, sz, sz);
            }
          g.SmoothingMode = sm0; }
        Prof.A("vis.stars", v0); v0 = Prof.T();
        // ---- the aura behind the ring, flashing with the beat ----
        { if (visFlash > 0.05f) { int d2 = (int)(R0 * 2.8f) / 16 * 16; if (d2 >= 48) { var au2 = AuraSprite(d2, Coarse(NeonCyan), (int)(60 + 110 * visFlash) / 16 * 16); g.DrawImageUnscaled(au2, (int)(cx - d2 / 2f), (int)(cy - d2 / 2f)); } } }
        Prof.A("vis.aura", v0); v0 = Prof.T();
        // ---- shells of light that ripple out behind the bars (the last few moments of the spectrum) ----
        int nb = VisN; float r1 = R0 * 1.16f; var cosT = new float[nb]; var sinT = new float[nb]; for (int i = 0; i < nb; i++) { double an = (-Math.PI / 2) + (i + 0.5) * 2 * Math.PI / nb; /* fixed: the spectrum is the same on the left and the right of the logo */ cosT[i] = (float)Math.Cos(an); sinT[i] = (float)Math.Sin(an); }
        Func<int, int> bandOf = i => Math.Min(95, (i < 48 ? i : 95 - i) * 2 + 1);
        float ov = playing ? Math.Max(0.06f, visBass * 0.45f + visMid * 0.35f + visHigh * 0.2f) : 0f;   // the overall level: what the logo beats to
        Func<float, float> Even = x => Math.Min(1f, x * 0.5f + ov * 0.75f);   // each bar: its own band and the overall bounce, so mids and highs on the sides are never smaller than the bass
        var ringV = new float[nb]; { var rawV = new float[nb]; for (int i = 0; i < nb; i++) rawV[i] = visB[bandOf(i)]; float[] kern = { 0.08f, 0.17f, 0.50f, 0.17f, 0.08f }; for (int i = 0; i < nb; i++) { float acc = 0; for (int k = -2; k <= 2; k++) acc += rawV[((i + k) % nb + nb) % nb] * kern[k + 2]; ringV[i] = acc; } }
        for (int k = visEcho.Length - 1; k >= 1; k--)
        {
            var snap = visEcho[(visEchoHead - k + visEcho.Length * 2) % visEcho.Length]; if (snap == null) continue; var pts = new PointF[nb]; float grow = 1f + 0.09f * k;
            for (int i = 0; i < nb; i++) { float rr = (r1 + R0 * 0.06f + snap[bandOf(i)] * R0 * 1.15f * 0.9f) * grow; pts[i] = new PointF(cx + cosT[i] * rr, cy + sinT[i] * rr); }
            using (var pen = new Pen(Color.FromArgb((int)((playing ? 74 : 26) / (float)k), VisCol(0.55f + 0.1f * k)), 1.5f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pts);
        }
        Prof.A("vis.shells", v0); v0 = Prof.T();
        // ---- the kick: a soft thick ring that swells on every beat, and hexagons that grow out of the logo ----
        if (playing && visBeat > 0.02f) { using (var pen = new Pen(Color.FromArgb((int)(55 * visBeat), NeonPink), R0 * 0.35f * visBeat + 2)) g.DrawEllipse(pen, cx - r1 * 0.98f, cy - r1 * 0.98f, r1 * 1.96f, r1 * 1.96f); }
        // ---- two rings of beads turning against each other; they speed up and swell with the music ----
        { var sm3 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None; using (var br = new SolidBrush(Color.White))
            for (int ring = 0; ring < 2; ring++) { int cnt = ring == 0 ? 72 : 48; float rr = R0 * (ring == 0 ? 2.62f : 2.78f); double sp0 = visRot * (ring == 0 ? 0.7 : -1.1);
              for (int i = 0; i < cnt; i++) { double an = sp0 + i * 2 * Math.PI / cnt; float bx = cx + (float)Math.Cos(an) * rr, by = cy + (float)Math.Sin(an) * rr; if (!a.Contains(bx, by)) continue; float sz = 1.6f + (playing ? visHigh * 3.2f + visBeat * 1.6f : 0f); br.Color = Color.FromArgb(playing ? 190 : 80, ring == 0 ? NeonCyan : NeonPink); g.FillRectangle(br, bx - sz / 2, by - sz / 2, sz, sz); } }
          g.SmoothingMode = sm3; }
        // ---- rays that shoot out when the highs sparkle ----
        if (playing && visHigh > 0.12f) { var sm4 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None; using (var pen = new Pen(Color.White, 1f)) for (int i = 0; i < 36; i++) { double an = -visRot * 1.7 + i * 2 * Math.PI / 36; float len = R0 * (0.15f + visHigh * 1.7f) * (0.4f + 0.6f * (float)Math.Abs(Math.Sin(i * 12.9898 + visT * 9))); float x0 = cx + (float)Math.Cos(an) * R0 * 2.3f, y0 = cy + (float)Math.Sin(an) * R0 * 2.3f; pen.Color = Color.FromArgb((int)(120 * Math.Min(1f, visHigh * 2.2f)), VisCol(i / 36f)); g.DrawLine(pen, x0, y0, x0 + (float)Math.Cos(an) * len, y0 + (float)Math.Sin(an) * len); } g.SmoothingMode = sm4; }
        // ---- shockwaves ----
        if (beat) { visRings.Add(new[] { R0 * 1.2f, 1f }); for (int k = 0; k < 22 && visSp.Count < 220; k++) { double an = visRnd.NextDouble() * Math.PI * 2; float sp = 120 + (float)visRnd.NextDouble() * 300, rr0 = R0 * 1.3f, life = 0.7f + (float)visRnd.NextDouble() * 0.7f; visSp.Add(new Spark { X = cx + (float)Math.Cos(an) * rr0, Y = cy + (float)Math.Sin(an) * rr0, VX = (float)Math.Cos(an) * sp, VY = (float)Math.Sin(an) * sp, Life = life, Max = life, R = 1f, C = VisCol((float)visRnd.NextDouble()) }); } }
        for (int i = visRings.Count - 1; i >= 0; i--) { var rg = visRings[i]; rg[0] += dt * R0 * 2.1f; rg[1] -= dt * 1.15f; if (rg[1] <= 0) { visRings.RemoveAt(i); continue; } using (var pen = new Pen(Color.FromArgb((int)(150 * rg[1]), i % 2 == 0 ? NeonCyan : NeonPink), 2.2f + 3f * rg[1])) g.DrawEllipse(pen, cx - rg[0], cy - rg[0], rg[0] * 2, rg[0] * 2); }
        Prof.A("vis.shock", v0); v0 = Prof.T();
        // ---- the bars: a dim wide one under a bright thin one, each coloured by where it sits on the ring ----
        { var core = new PointF[4]; var smW = g.SmoothingMode;
          for (int pass = 0; pass < 2; pass++)
          {
              g.SmoothingMode = pass == 0 ? SmoothingMode.None : SmoothingMode.AntiAlias; float wdt = pass == 0 ? 11f : 3.4f;
              for (int i = 0; i < nb; i++)
              {
                  int bi = bandOf(i); float v = Even(ringV[i]); if (!playing) v = Math.Max(v, 0.03f + 0.025f * (float)Math.Sin(visT * 1.7 + i * 0.35)); float len = R0 * 0.07f + v * R0 * 1.15f * (1f + 0.22f * visBeat); float ca = cosT[i], sa = sinT[i], nx = -sa * wdt / 2, ny = ca * wdt / 2; float x0 = cx + ca * r1, y0 = cy + sa * r1, x1 = cx + ca * (r1 + len), y1 = cy + sa * (r1 + len);
                  core[0] = new PointF(x0 + nx, y0 + ny); core[1] = new PointF(x1 + nx, y1 + ny); core[2] = new PointF(x1 - nx, y1 - ny); core[3] = new PointF(x0 - nx, y0 - ny);
                  Color col = VisCol((i < 48 ? i : 95 - i) / 47f); if (pass == 0) col = Color.FromArgb((int)(34 + 40 * v), col); else col = MixC(col, Color.White, Math.Min(0.55f, v * 0.5f));
                  using (var br = new SolidBrush(col)) g.FillPolygon(br, core);
              }
          }
          g.SmoothingMode = smW; }
        Prof.A("vis.bars", v0); v0 = Prof.T();
        // peak dots
        { var sm1 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None; using (var br = new SolidBrush(Color.White)) for (int i = 0; i < nb; i += 2) { float pv = visPk[bandOf(i)]; if (pv < 0.07f) continue; float rr = r1 + R0 * 0.07f + pv * R0 * 1.15f + 8; br.Color = Color.FromArgb(200, 255, 255, 255); g.FillRectangle(br, cx + cosT[i] * rr - 1.5f, cy + sinT[i] * rr - 1.5f, 3f, 3f); } g.SmoothingMode = sm1; }
        // ---- the sound before mastering (a thin ring inside the bars) and the real waveform (a circular scope) ----
        { var pts = new PointF[nb]; for (int i = 0; i < nb; i++) { float rr = R0 * 1.06f + visPre[bandOf(i)] * R0 * 0.2f; pts[i] = new PointF(cx + cosT[i] * rr, cy + sinT[i] * rr); } using (var pen = new Pen(TM.C(120, 215, 215, 240), 1.3f)) g.DrawPolygon(pen, pts); }
        if (playing && eng.MasterTap != null)
        {
            int n = visWave.Length; var pts = new PointF[n]; var tap = eng.MasterTap; int pos = eng.MasterTapPos; for (int i = 0; i < n; i++) { float s = tap[(pos - (n - i) * 6 + 8192 * 4) & 8191]; double an = visRot * 0.5 + i * 2 * Math.PI / n; float rr = R0 * 0.66f + s * R0 * 0.34f; pts[i] = new PointF(cx + (float)Math.Cos(an) * rr, cy + (float)Math.Sin(an) * rr); }
            using (var pen = new Pen(Color.FromArgb(70, NeonCyan), 4.5f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pts); using (var pen = new Pen(Color.FromArgb(220, NeonCyan), 1.4f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pts);
        }
        Prof.A("vis.rings+scope", v0); v0 = Prof.T();
        // ---- the logo, beating ----
        { float hh0 = R0 * (0.80f + 0.1f * visBeat + 0.05f * visBass); if (visBeat > 0.05f) { float grow = (1 - visBeat) * R0 * 0.9f; using (var hp0 = HexPath(cx, cy, (hh0 + grow) * 1.16f, hh0 + grow)) using (var pen = new Pen(Color.FromArgb((int)(150 * visBeat), NeonCyan), 2f + 3f * visBeat) { LineJoin = LineJoin.Round }) g.DrawPath(pen, hp0); } DrawLogo(g, cx, cy, hh0, playing, false); }
        Prof.A("vis.logo", v0); v0 = Prof.T();
        // ---- sparks ----
        for (int i = visSp.Count - 1; i >= 0; i--) { var s = visSp[i]; s.VX *= 0.985f; s.VY = s.VY * 0.985f + 60 * dt; s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt; if (s.Life <= 0 || !a.Contains(s.X, s.Y)) { visSp.RemoveAt(i); continue; } var sp = SparkSprite(Coarse(s.C)); g.DrawImageUnscaled(sp, (int)(s.X - 7), (int)(s.Y - 7)); }
        Prof.A("vis.sparks", v0); v0 = Prof.T();
        // ---- the floor: a mirrored spectrum with peak caps ----
        { float baseY = a.Bottom - seekH - 14, x0 = a.X + (full ? 70 : 16), x1 = a.Right - (full ? 70 : 16), bw = (x1 - x0) / VisN; var sm2 = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
          using (var pen = new Pen(Color.FromArgb(110, NeonViolet), 1f)) g.DrawLine(pen, x0, baseY, x1, baseY);
          using (var br = new SolidBrush(Color.White))
            for (int i = 0; i < VisN; i++)
            {
                float v = Math.Min(1f, visB[i] * 0.75f + ov * 0.5f); if (!playing) v = Math.Max(v, 0.02f + 0.015f * (float)Math.Sin(visT * 2 + i * 0.3)); float hh = 2 + v * floorUp; Color c = VisCol(i / 95f); float bx = x0 + i * bw + 1, w2 = Math.Max(2f, bw - 2.4f);
                br.Color = Color.FromArgb(70, c); g.FillRectangle(br, bx - 1, baseY - hh - 1, w2 + 2, hh + 1); br.Color = Color.FromArgb(235, c); g.FillRectangle(br, bx, baseY - hh, w2, hh);
                br.Color = Color.FromArgb(46, c); g.FillRectangle(br, bx, baseY + 2, w2, Math.Min(18f, hh * 0.4f));
                float pk = 2 + visPk[i] * floorUp; br.Color = Color.FromArgb(220, 255, 255, 255); g.FillRectangle(br, bx, baseY - pk - 3, w2, 2);
            }
          g.SmoothingMode = sm2; }
        Prof.A("vis.floor", v0); v0 = Prof.T();
        // ---- the song position (click or drag to jump): the same strip as the scroll bars, the logo rides along and is the handle ----
        {
            float bh = full ? 36f : 32f; var ob = Rc(a.X + (full ? 70 : 16), a.Bottom - bh - 4, a.Width - (full ? 140 : 32), bh); bool pl2 = eng.Playing;
            FillRR(g, Color.Black, ob, 12); using (var pen = new Pen(Color.FromArgb(pl2 ? 230 : 150, NeonViolet), 1.3f)) { using (var p = RR(ob, 12)) g.DrawPath(pen, p); }
            const float tw0 = 74; var inner = Rc(ob.X + tw0, ob.Y + 4, ob.Width - 2 * tw0, ob.Height - 8); double tot = Math.Max(1, eng.EndFrame); float f = (float)Math.Max(0, Math.Min(1, dispPos / tot)); float px = inner.X + f * inner.Width;
            if (f > 0.002f) using (var br = new SolidBrush(TM.C(60, 0, 110, 130))) g.FillRectangle(br, inner.X, inner.Y, Math.Max(0, px - inner.X), inner.Height);
            using (var pen = new Pen(Color.FromArgb(230, NeonCyan), 1.6f)) g.DrawLine(pen, px, inner.Y, px, inner.Bottom);
            float lh = ob.Height / 2f - 2f, lhw = lh * 1.16f, lcx = Math.Max(inner.X + lhw + 2, Math.Min(inner.Right - lhw - 2, px)), lcy = ob.Y + ob.Height / 2f; VizWaves(g, inner, ob, lcx, lhw); DrawLogo(g, lcx, lcy, lh, pl2, Hot(Rc(lcx - lhw, lcy - lh, lhw * 2, lh * 2)));
            Txt(g, Fmt(dispPos / (double)StudioEngine.SR), 11, Ink, Rc(ob.X + 10, ob.Y, tw0 - 12, ob.Height), StringAlignment.Near, true); Txt(g, Fmt(tot / StudioEngine.SR), 11, DimC, Rc(ob.Right - tw0 + 2, ob.Y, tw0 - 12, ob.Height), StringAlignment.Far, false);
            Action<float> seek = mx => { double ff = Math.Max(0, Math.Min(1, (mx - inner.X) / inner.Width)); eng.Seek((long)(ff * tot)); }; var rg = Reg(inner, () => seek(MousePos.X)); rg.OnDrag = pt => seek(pt.X);
            if (Hot(inner)) { bubble = "Click or drag to jump to a place in the song"; bubblePt = MousePos; }
        }
        Prof.A("vis.seek", v0); v0 = Prof.T();
        // title and what you are hearing
        { string st = eng.MasterBypass ? "ORIGINAL" : "MASTERED"; float ty = a.Y + 8, tx0 = full ? a.X + a.Width / 2 - 62 : a.X + 14; Txt(g, projName.ToUpper(), full ? 20 : 14, Ink, Rc(full ? a.X + a.Width / 2 - 300 : tx0, ty, full ? 600 : Math.Min(320, a.Width - 200), full ? 30 : 22), full ? StringAlignment.Center : StringAlignment.Near, true); var pill = Rc(tx0, ty + (full ? 32 : 24), 124, full ? 24 : 20);
          FillRR(g, eng.MasterBypass ? TM.C(255, 60, 52, 98) : Color.FromArgb(255, 28, 112, 86), pill, 9); Txt(g, st, full ? 12 : 11, Color.White, pill, StringAlignment.Center, true); if (full) { var hp = Rc(pill.Right + 8, pill.Y, 122, pill.Height); Btn(g, hp, eng.MasterBypass ? "HEAR MASTER" : "HEAR ORIGINAL", 0, () => { eng.MasterBypass = !eng.MasterBypass; ApplyMatch(); }, 10); Hint2(hp, "Switch between your original mix and the mastered sound while you watch"); } }
        if (!full) { var fvb = Rc(a.Right - 156, a.Y + 6, 144, 26); Btn(g, fvb, "FULL VISUALISER", 1, () => GoTab(5), 11); Hint2(fvb, "Open the visualiser on a page of its own (Esc comes back)"); }
        if (!playing) Txt(g, "PRESS PLAY OR PREVIEW MASTER AND WATCH YOUR MUSIC MOVE", 12, DimC, Rc(a.X, Math.Min(cy + R0 * 2.35f, a.Bottom - (full ? 48f : 42f) - 8 - (full ? Math.Min(150f, a.Height * 0.2f) : Math.Min(64f, a.Height * 0.2f)) * 0.35f - 26), a.Width, 20), StringAlignment.Center, true);
        g.Restore(gs);
    }

    // ---------- the full page (tab 5): the stage over everything, with level meters and the loudness cards ----------
    void PaintVisualiser(Graphics g)
    {
        float W = ClientSize.Width, H = ClientSize.Height, top = ToolH; lastInput = DateTime.Now; var a = Rc(0, top, W, H - top);
        PaintVisStage(g, a, true);
        float mh = Math.Max(160f, H - top - 330f), my = top + (H - top - 120) / 2f - mh / 2; var ml = Rc(24, my, 24, mh); var mr = Rc(W - 48, my, 24, mh); float pl = Math.Max(1e-6f, eng.MasterPeakL), pr = Math.Max(1e-6f, eng.MasterPeakR);
        float fl = Clamp01(((float)(20 * Math.Log10(pl)) + 54f) / 54f), fr = Clamp01(((float)(20 * Math.Log10(pr)) + 54f) / 54f); visHoldL = Math.Max(fl, visHoldL - hDt * 0.25f); visHoldR = Math.Max(fr, visHoldR - hDt * 0.25f);
        LedMeter(g, ml, fl); LedMeter(g, mr, fr); using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 2f)) { float yl = ml.Bottom - visHoldL * ml.Height, yr = mr.Bottom - visHoldR * mr.Height; g.DrawLine(pen, ml.X - 3, yl, ml.Right + 3, yl); g.DrawLine(pen, mr.X - 3, yr, mr.Right + 3, yr); }
        Txt(g, "L", 12, DimC, Rc(ml.X - 4, ml.Bottom + 4, 32, 16), StringAlignment.Center, true); Txt(g, "R", 12, DimC, Rc(mr.X - 4, mr.Bottom + 4, 32, 16), StringAlignment.Center, true);
        var lu = eng.Lufs; lu.UpdateIntegrated(); VisCard(g, Rc(70, top + 18, 230, 84), "MOMENTARY", Db(lu.Momentary), "LUFS", Ink); VisCard(g, Rc(70, top + 110, 230, 84), "INTEGRATED", Db(lu.Integrated), "LUFS", NeonCyan);
        VisCard(g, Rc(W - 300, top + 18, 230, 84), "TRUE PEAK", Db(lu.TruePeakDb), "dBTP", lu.TruePeakDb > -0.9 ? Bad : Good); VisCard(g, Rc(W - 300, top + 110, 230, 84), "GAIN REDUCTION", eng.MasterGr.ToString("0.0"), "dB", Warn);
    }
    void VisCard(Graphics g, RectangleF r, string label, string value, string unit, Color vc)
    {
        FillRR(g, TM.C(255, 9, 5, 22), r, 12); Win.DrawNeonBox(g, r, false);
        Txt(g, label, 10, DimC, Rc(r.X, r.Y + 6, r.Width, 14), StringAlignment.Center, true); Txt(g, value, 30, vc, Rc(r.X, r.Y + 22, r.Width, 40), StringAlignment.Center, true); Txt(g, unit, 11, DimC, Rc(r.X, r.Y + 62, r.Width, 16), StringAlignment.Center, false);
    }

    // ---------- the top bar of the full page ----------
    void PaintVisBar(Graphics g)
    {
        float W = ClientSize.Width, y = 12, h = 46;
        using (var br = new SolidBrush(TM.C(255, 18, 10, 34))) g.FillRectangle(br, 0, 0, W, ToolH); NeonRule(g, 0, ToolH, W);
        Btn(g, Rc(12, y, 48, h), "≡", 0, () => { if (menuOpen) CloseMenu(); else OpenMenu(); }, 24);
        Txt(g, "VISUALISER", 18, Ink, Rc(76, y, 190, h), StringAlignment.Near, true); Btn(g, Rc(270, y + 4, 100, h - 8), "HOME", 2, () => GoHome(), 14); if (ShowBack) Btn(g, Rc(376, y + 4, 100, h - 8), "BACK", 0, () => GoBack(), 14);
        float rx = W - 14;
        Btn(g, Rc(rx - 108, y + 4, 108, h - 8), Win.FullLabel(this), 0, () => Win.ToggleFullscreen(this), 12); rx -= 114;
        var mb = Rc(rx - 130, y + 4, 130, h - 8); Btn(g, mb, "MASTERING", 1, () => tab = 1, 13); Hint2(mb, "Back to the Mastering page (the EQ, effects and presets)"); rx -= 136;
        var pl = Rc(rx - 96, y + 4, 96, h - 8); Btn(g, pl, "PLAY", eng.Playing && !mpActive ? 4 : 1, () => { if (mpActive) MasterPreviewStop(); PlayToggle(); }, 14); rx -= 102;
        var pv = Rc(rx - 168, y + 4, 168, h - 8); Btn(g, pv, "PREVIEW MASTER", mpActive ? 4 : 2, () => MasterPreviewStart(), 13); Hint2(pv, "Loop the loudest 20 seconds of your song with the mastering on"); rx -= 174;
        var sp = Rc(rx - 128, y + 4, 128, h - 8); Btn(g, sp, "STOP PREVIEW", 3, () => MasterPreviewStop(), 12); Hint2(sp, "Stop the preview loop"); rx -= 134;
    }
}
