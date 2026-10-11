using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Pop-up boxes (Gain, the track pickers, New Track, Buses, Vocal Styles, Automation, Community presets, Add effect) can be dragged anywhere by their top strip,
// so you can move them out of the way and keep them where you want them while you work.
partial class StudioForm
{
    readonly Dictionary<string, PointF> popOff = new Dictionary<string, PointF>(); readonly List<HitReg> popPending = new List<HitReg>(); readonly List<RectangleF> popRects = new List<RectangleF>();
    string popDragId = ""; PointF popDragMouse, popDragStart;

    // returns the box shifted to where you dragged it; its top strip (titleH tall, leaving closeW free on the right for the x button) becomes a handle
    RectangleF PopMove(string id, RectangleF box, float titleH = 56, float closeW = 64, bool dots = true)
    {
        PointF o; popOff.TryGetValue(id, out o); float W = ClientSize.Width, H = ClientSize.Height; var r = box; r.X += o.X; r.Y += o.Y;
        float nx = Math.Max(-(r.Width - 110), Math.Min(W - 110, r.X)), ny = Math.Max(0, Math.Min(H - 44, r.Y)); if (nx != r.X || ny != r.Y) { popOff[id] = new PointF(o.X + nx - r.X, o.Y + ny - r.Y); r.X = nx; r.Y = ny; }
        var handle = Rc(Math.Max(0, r.X), r.Y, Math.Max(10, Math.Min(W, r.Right) - Math.Max(0, r.X) - closeW), titleH); string pid = id; PointF cur = popOff.ContainsKey(id) ? popOff[id] : PointF.Empty;
        var hr = new HitReg { R = handle };
        hr.OnDown = () => { popDragId = pid; popDragMouse = MousePos; PointF oo; popOff.TryGetValue(pid, out oo); popDragStart = oo; };
        hr.OnDrag = pt => { if (popDragId == pid) popOff[pid] = new PointF(popDragStart.X + pt.X - popDragMouse.X, popDragStart.Y + pt.Y - popDragMouse.Y); };
        hr.OnUp = () => popDragId = "";
        popPending.Add(hr); popRects.Add(handle);
        if (Hot(handle) || popDragId == id) hintNow = "Drag here to move this box wherever you like.";
        // a small grip so you can see it moves
        if (dots) using (var b = new SolidBrush(TM.C(Hot(handle) ? 200 : 110, 190, 150, 255))) for (int i = 0; i < 3; i++) g0Grip(b, r.X + r.Width / 2 - 16 + i * 12, r.Y + 6);
        return r;
    }
    Graphics popG;
    void g0Grip(SolidBrush b, float x, float y) { if (popG != null) popG.FillEllipse(b, x, y, 5, 5); }
    void PopBegin(Graphics g) { popG = g; popPending.Clear(); popRects.Clear(); }
    void PopEnd() { foreach (var h in popPending) regs.Add(h); popG = null; }
}
