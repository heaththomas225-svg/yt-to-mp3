// text helpers on SkiaSharp 2.88: measuring and outlining go through an SKPaint set up like the font
using SkiaSharp;
namespace System.Drawing
{
    static class SkFontExt
    {
        [ThreadStatic] static SKPaint tp;
        static SKPaint P(SKFont f) { var p = tp ?? (tp = new SKPaint { IsAntialias = true }); p.Typeface = f.Typeface; p.TextSize = f.Size; p.FakeBoldText = f.Embolden; p.SubpixelText = true; return p; }
        public static float W(this SKFont f, string s) { return string.IsNullOrEmpty(s) ? 0f : P(f).MeasureText(s); }
        public static SKPath TextPath(this SKFont f, string s, SKPoint at) { return P(f).GetTextPath(s ?? "", at.X, at.Y); }
    }
}
