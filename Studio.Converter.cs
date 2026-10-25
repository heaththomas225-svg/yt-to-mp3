using System;
using System.Drawing;
using System.Windows.Forms;

// =====================================================================================================
//  THE CONVERTER IN THE STUDIO (tab 11, the CONVERTER button at the top): the MP3 / MP4 converter's own panel is moved onto this page
//  while it is open (everything in it works as before) and goes back to its own window when you leave the page.
// =====================================================================================================
partial class StudioForm
{
    const float TabsW = 444;   // MIX, MASTERING, LYRICS and CONVERTER at the top right
    Rectangle ConverterRect { get { int w = 640, h = Math.Max(300, Math.Min(712, ClientSize.Height - ToolH - 24)); return new Rectangle(Math.Max(10, (ClientSize.Width - w) / 2), ToolH + 12, w, h); } }
    bool convSyncQueued;
    // the panel is moved after painting (moving controls while painting is not allowed)
    void SyncConverter()
    {
        if (app == null || convSyncQueued) return; bool want = tab == 11, has = app.ConverterInStudio;
        if (want == has && (!want || app.ConverterBounds == ConverterRect)) return;   // already where it belongs
        convSyncQueued = true; BeginInvokeSafe(() => { convSyncQueued = false; if (tab == 11) app.EmbedConverter(this, ConverterRect); else app.ReleaseConverter(); Invalidate(); });
    }
    void BeginInvokeSafe(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }
    void PaintConverterPage(Graphics g)
    {
        var r = ConverterRect; var fr = RectangleF.Inflate(new RectangleF(r.X, r.Y, r.Width, r.Height), 8, 8);
        Glow(g, NeonCyan, fr, 14, 0.8f, 4); StrokeRR(g, NeonCyan, fr, 14, 1.6f);
        Txt(g, "Convert links to MP3 or MP4 here. Your files go to the folder in SAVE TO; drag one onto the MIX page to use it in a song.", 11, DimC, Rc(10, fr.Bottom + 6, ClientSize.Width - 20, 18), StringAlignment.Center, false);
    }
}
