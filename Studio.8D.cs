using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

// 8D Audio Tuner editor: a top-down view of the listener with the orbiting source. Click or drag the ring to park the sound.
partial class StudioForm
{
    float Paint8dEditor(Graphics g, Spatial8D sp, RectangleF a)
    {
        float cx = a.X + 86, cy = a.Y + 90, R = 66; bool hold = sp.Get("hold") > 0.5f;
        FillRR(g, Color.FromArgb(120, 6, 3, 16), Rc(a.X, a.Y, a.Width, 184), 12);
        using (var pen = new Pen(Color.FromArgb(120, 150, 90, 230), 1.4f)) { g.DrawEllipse(pen, cx - R, cy - R, R * 2, R * 2); g.DrawEllipse(pen, cx - R * 0.55f, cy - R * 0.55f, R * 1.1f, R * 1.1f); }
        using (var pen = new Pen(Color.FromArgb(50, 190, 140, 255))) { g.DrawLine(pen, cx - R, cy, cx + R, cy); g.DrawLine(pen, cx, cy - R, cx, cy + R); }
        // listener: head, nose (front = up), ears
        using (var b = new SolidBrush(Color.FromArgb(200, 70, 40, 130))) g.FillEllipse(b, cx - 20, cy - 22, 40, 44);
        using (var b = new SolidBrush(Color.FromArgb(230, 190, 140, 255))) { g.FillPolygon(b, new[] { new PointF(cx - 6, cy - 21), new PointF(cx + 6, cy - 21), new PointF(cx, cy - 31) }); g.FillEllipse(b, cx - 25, cy - 6, 7, 12); g.FillEllipse(b, cx + 18, cy - 6, 7, 12); }
        Txt(g, "FRONT", 9, DimC, Rc(cx - 24, cy - R - 14, 48, 12), StringAlignment.Center, false); Txt(g, "BACK", 9, DimC, Rc(cx - 24, cy + R + 2, 48, 12), StringAlignment.Center, false);
        Txt(g, "L", 10, DimC, Rc(cx - R - 14, cy - 7, 12, 14), StringAlignment.Center, false); Txt(g, "R", 10, DimC, Rc(cx + R + 2, cy - 7, 12, 14), StringAlignment.Center, false);
        // orbiting source with a fading trail
        float ang = hold ? sp.Get("angle") : sp.AngleDeg;
        trail8.Add(ang); while (trail8.Count > 16) trail8.RemoveAt(0);
        for (int i = 0; i < trail8.Count; i++)
        {
            double r = trail8[i] * Math.PI / 180.0; float x = cx + (float)Math.Sin(r) * R, y = cy - (float)Math.Cos(r) * R; float f = (i + 1) / (float)trail8.Count;
            using (var b = new SolidBrush(Color.FromArgb((int)(20 + 120 * f * f), 236, 92, 226))) g.FillEllipse(b, x - 4 * f - 1, y - 4 * f - 1, 8 * f + 2, 8 * f + 2);
        }
        { double r = ang * Math.PI / 180.0; float x = cx + (float)Math.Sin(r) * R, y = cy - (float)Math.Cos(r) * R;
          using (var b = new SolidBrush(Color.FromArgb(70, 236, 92, 226))) g.FillEllipse(b, x - 14, y - 14, 28, 28);
          using (var b = new SolidBrush(Color.FromArgb(255, 245, 190, 255))) g.FillEllipse(b, x - 7, y - 7, 14, 14); }
        var ring = Rc(cx - R - 14, cy - R - 14, (R + 14) * 2, (R + 14) * 2); var rg = Reg(ring, () => Park8d(sp, cx, cy)); rg.OnDrag = pt => Park8d(sp, cx, cy, pt);
        // controls on the right
        float x0 = a.X + 186, w = a.Width - 196;
        Btn(g, Rc(x0, a.Y + 10, w, 28), hold ? "Holding: tap to rotate" : "Rotating: tap to hold", hold ? 4 : 1, () => sp.Set("hold", hold ? 0 : 1), 12);
        string[] nm = { "Front", "Right", "Behind", "Left" }; float[] an = { 0, 90, 180, 270 };
        for (int i = 0; i < 4; i++) { int k = i; Btn(g, Rc(x0 + (i % 2) * (w / 2 + 2), a.Y + 46 + (i / 2) * 32, w / 2 - 2, 28), nm[i], 0, () => { sp.Set("angle", an[k]); sp.Set("hold", 1); }, 12); }
        Txt(g, "Angle", 12, DimC, Rc(x0, a.Y + 114, 50, 22), StringAlignment.Near, false);
        Txt(g, ((int)ang) + " deg", 14, Accent, Rc(x0 + 50, a.Y + 114, 80, 22), StringAlignment.Near, true);
        ValueEdit(Rc(x0 + 50, a.Y + 114, 80, 22), Num(ang), s => { float v; if (ParseNum(s, out v)) { sp.Set("angle", ((v % 360) + 360) % 360); sp.Set("hold", 1); } });
        Txt(g, "Best with headphones. Click or drag the ring to place the sound.", 10, DimC, Rc(x0, a.Y + 142, w, 34), StringAlignment.Near, false);
        return a.Y + 192;
    }

    void Park8d(Spatial8D sp, float cx, float cy) { Park8d(sp, cx, cy, new PointF(MousePos.X, MousePos.Y)); }
    void Park8d(Spatial8D sp, float cx, float cy, PointF pt)
    {
        double ang = Math.Atan2(pt.X - cx, -(pt.Y - cy)) * 180.0 / Math.PI; if (ang < 0) ang += 360;
        sp.Set("angle", (float)Math.Round(ang)); sp.Set("hold", 1);
    }
}