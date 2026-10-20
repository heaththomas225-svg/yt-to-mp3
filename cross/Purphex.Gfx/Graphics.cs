// Purphex.Gfx: the parts of System.Drawing that the studio draws with, implemented on SkiaSharp so the same drawing code
// runs on macOS and Linux (System.Drawing's own Graphics is Windows-only on .NET 8). Only what Purphex uses is here.
// Colours, points, rectangles and sizes are .NET's own (System.Drawing.Primitives, cross-platform).
using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace System.Drawing
{
    public enum GraphicsUnit { World, Display, Pixel, Point, Inch, Document, Millimeter }
    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum StringAlignment { Near, Center, Far }
    [Flags] public enum StringFormatFlags { DirectionRightToLeft = 1, DirectionVertical = 2, FitBlackBox = 4, DisplayFormatControl = 32, NoFontFallback = 1024, MeasureTrailingSpaces = 2048, NoWrap = 4096, LineLimit = 8192, NoClip = 16384 }
    public enum StringTrimming { None, Character, Word, EllipsisCharacter, EllipsisWord, EllipsisPath }
    public enum ContentAlignment { TopLeft = 1, TopCenter = 2, TopRight = 4, MiddleLeft = 16, MiddleCenter = 32, MiddleRight = 64, BottomLeft = 256, BottomCenter = 512, BottomRight = 1024 }

    static class Sk
    {
        public static SKColor C(Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
        public static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
        public static SKRect R(Rectangle r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
        public static SKPoint P(PointF p) { return new SKPoint(p.X, p.Y); }
    }

    // ---------------- brushes ----------------
    public abstract class Brush : IDisposable, ICloneable
    {
        internal abstract void Apply(SKPaint p);
        public virtual void Dispose() { }
        public virtual object Clone() { return MemberwiseClone(); }
    }
    public sealed class SolidBrush : Brush
    {
        public Color Color { get; set; }
        public SolidBrush(Color c) { Color = c; }
        internal override void Apply(SKPaint p) { p.Shader = null; p.Color = Sk.C(Color); }
    }
    public sealed class TextureBrush : Brush
    {
        readonly Image img; public WrapMode WrapMode { get; set; } = WrapMode.Tile; public Matrix Transform { get; set; } = new Matrix();
        public TextureBrush(Image image) { img = image; } public TextureBrush(Image image, WrapMode wrap) { img = image; WrapMode = wrap; }
        public void TranslateTransform(float dx, float dy) { Transform.Translate(dx, dy); }
        internal override void Apply(SKPaint p) { p.Color = SKColors.White; p.Shader = SKShader.CreateBitmap(img.Bmp, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, Transform.M); }
    }

    // ---------------- pens ----------------
    public sealed class Pen : IDisposable, ICloneable
    {
        public Color Color { get; set; } public float Width { get; set; } = 1f; public Brush Brush { get; set; }
        public DashStyle DashStyle { get; set; } public float[] DashPattern { get; set; } public float DashOffset { get; set; }
        public LineJoin LineJoin { get; set; } public LineCap StartCap { get; set; } public LineCap EndCap { get; set; } public DashCap DashCap { get; set; }
        public PenAlignment Alignment { get; set; } public float MiterLimit { get; set; } = 10f;
        public Pen(Color c) { Color = c; } public Pen(Color c, float w) { Color = c; Width = w; } public Pen(Brush b) { Brush = b; } public Pen(Brush b, float w) { Brush = b; Width = w; }
        public void SetLineCap(LineCap start, LineCap end, DashCap dash) { StartCap = start; EndCap = end; DashCap = dash; }
        internal void Apply(SKPaint p)
        {
            p.Style = SKPaintStyle.Stroke; p.StrokeWidth = Width; p.StrokeMiter = MiterLimit;
            if (Brush != null) Brush.Apply(p); else { p.Shader = null; p.Color = Sk.C(Color); }
            p.StrokeJoin = LineJoin == LineJoin.Round ? SKStrokeJoin.Round : (LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter);
            var cap = StartCap == LineCap.Round || EndCap == LineCap.Round ? SKStrokeCap.Round : (StartCap == LineCap.Square || EndCap == LineCap.Square ? SKStrokeCap.Square : SKStrokeCap.Butt); p.StrokeCap = cap;
            float w = Math.Max(1f, Width); float[] iv = null;
            if (DashStyle == DashStyle.Dash) iv = new[] { 3 * w, w }; else if (DashStyle == DashStyle.Dot) iv = new[] { w, w }; else if (DashStyle == DashStyle.DashDot) iv = new[] { 3 * w, w, w, w }; else if (DashStyle == DashStyle.DashDotDot) iv = new[] { 3 * w, w, w, w, w, w };
            else if (DashStyle == DashStyle.Custom && DashPattern != null && DashPattern.Length >= 2) { iv = new float[DashPattern.Length % 2 == 0 ? DashPattern.Length : DashPattern.Length * 2]; for (int i = 0; i < iv.Length; i++) iv[i] = DashPattern[i % DashPattern.Length] * w; }
            p.PathEffect = iv != null ? SKPathEffect.CreateDash(iv, DashOffset * w) : null;
        }
        public void Dispose() { } public object Clone() { return MemberwiseClone(); }
    }

    // ---------------- fonts ----------------
    public sealed class FontFamily : IDisposable
    {
        public string Name { get; } internal readonly SKTypeface Face;
        public FontFamily(string name) { Name = name; Face = SKTypeface.FromFamilyName(name) ?? SKTypeface.Default; }
        internal FontFamily(string name, SKTypeface face) { Name = name; Face = face ?? SKTypeface.Default; }
        public static FontFamily GenericSansSerif { get { return new FontFamily("Arial"); } }
        public static FontFamily GenericMonospace { get { return new FontFamily("Courier New"); } }
        public static FontFamily[] Families { get { var l = new List<FontFamily>(); foreach (var n in SKFontManager.Default.FontFamilies) l.Add(new FontFamily(n)); return l.ToArray(); } }
        public bool IsStyleAvailable(FontStyle s) { return true; }
        public int GetEmHeight(FontStyle s) { return 2048; } public int GetCellAscent(FontStyle s) { return (int)(2048 * 0.8); } public int GetCellDescent(FontStyle s) { return (int)(2048 * 0.2); } public int GetLineSpacing(FontStyle s) { return (int)(2048 * 1.15); }
        public void Dispose() { }
    }
    public sealed class Font : IDisposable, ICloneable
    {
        public FontFamily FontFamily { get; } public float Size { get; } public FontStyle Style { get; } public GraphicsUnit Unit { get; }
        public string Name { get { return FontFamily.Name; } } public bool Bold { get { return (Style & FontStyle.Bold) != 0; } } public bool Italic { get { return (Style & FontStyle.Italic) != 0; } }
        internal readonly SKFont Sk_;
        public Font(string family, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point) : this(new FontFamily(family), size, style, unit) { }
        public Font(FontFamily family, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
        {
            FontFamily = family; Size = size; Style = style; Unit = unit;
            var face = family.Face; if ((style & (FontStyle.Bold | FontStyle.Italic)) != 0) { var w = (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal; var sl = (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright; var f2 = SKFontManager.Default.MatchFamily(family.Name, new SKFontStyle(w, SKFontStyleWidth.Normal, sl)); if (f2 != null && string.Equals(f2.FamilyName, face.FamilyName, StringComparison.OrdinalIgnoreCase)) face = f2; }
            Sk_ = new SKFont(face, SizeInPoints * 96f / 72f) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
            if ((style & FontStyle.Bold) != 0 && !face.IsBold) Sk_.Embolden = true;
        }
        public Font(string family, float size, GraphicsUnit unit) : this(new FontFamily(family), size, FontStyle.Regular, unit) { }
        public Font(FontFamily family, float size, GraphicsUnit unit) : this(family, size, FontStyle.Regular, unit) { }
        public Font(Font proto, FontStyle style) : this(proto.FontFamily, proto.Size, style, proto.Unit) { }
        public float SizeInPoints { get { return Unit == GraphicsUnit.Pixel ? Size * 72f / 96f : (Unit == GraphicsUnit.Inch ? Size * 72f : (Unit == GraphicsUnit.Millimeter ? Size * 72f / 25.4f : Size)); } }
        public int Height { get { return (int)Math.Ceiling(GetHeight()); } }
        public float GetHeight() { return Sk_.Spacing; } public float GetHeight(Graphics g) { return Sk_.Spacing; } public float GetHeight(float dpi) { return Sk_.Spacing * dpi / 96f; }
        public void Dispose() { } public object Clone() { return new Font(FontFamily, Size, Style, Unit); }
    }
    public sealed class StringFormat : IDisposable, ICloneable
    {
        public StringAlignment Alignment { get; set; } public StringAlignment LineAlignment { get; set; } public StringFormatFlags FormatFlags { get; set; } public StringTrimming Trimming { get; set; } = StringTrimming.Character;
        public System.Drawing.Text.HotkeyPrefix HotkeyPrefix { get; set; }
        public StringFormat() { } public StringFormat(StringFormatFlags f) { FormatFlags = f; } public StringFormat(StringFormat s) { Alignment = s.Alignment; LineAlignment = s.LineAlignment; FormatFlags = s.FormatFlags; Trimming = s.Trimming; }
        public static StringFormat GenericDefault { get { return new StringFormat(); } }
        public static StringFormat GenericTypographic { get { return new StringFormat { FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip, Trimming = StringTrimming.None }; } }
        public void SetMeasurableCharacterRanges(CharacterRange[] r) { }
        public void Dispose() { } public object Clone() { return new StringFormat(this); }
    }
    public struct CharacterRange { public int First, Length; public CharacterRange(int f, int l) { First = f; Length = l; } }

    // ---------------- images ----------------
    public abstract class Image : IDisposable, ICloneable
    {
        internal SKBitmap Bmp;
        public int Width { get { return Bmp.Width; } } public int Height { get { return Bmp.Height; } } public Size Size { get { return new Size(Width, Height); } }
        public float HorizontalResolution { get { return 96f; } } public float VerticalResolution { get { return 96f; } }
        public PixelFormat PixelFormat { get; protected set; } = PixelFormat.Format32bppArgb;
        public object Tag { get; set; }
        public static Image FromFile(string path) { using (var s = System.IO.File.OpenRead(path)) return FromStream(s); }
        public static Image FromStream(System.IO.Stream s) { return FromStream(s, false, false); }
        public static Image FromStream(System.IO.Stream s, bool useEmbeddedColorManagement, bool validateImageData) { var b = SKBitmap.Decode(s); if (b == null) throw new ArgumentException("the picture could not be read"); return new Bitmap(b); }
        public void Save(string path) { Save(path, path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void Save(string path, ImageFormat f) { using (var s = System.IO.File.Create(path)) Save(s, f); }
        public void Save(System.IO.Stream s, ImageFormat f) { using (var img = SKImage.FromBitmap(Bmp)) using (var data = img.Encode(f == ImageFormat.Jpeg ? SKEncodedImageFormat.Jpeg : (f == ImageFormat.Bmp ? SKEncodedImageFormat.Bmp : SKEncodedImageFormat.Png), 92)) data.SaveTo(s); }
        public void Save(System.IO.Stream s, ImageCodecInfo enc, EncoderParameters p) { Save(s, enc != null && enc.MimeType == "image/jpeg" ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void Save(string path, ImageCodecInfo enc, EncoderParameters p) { Save(path, enc != null && enc.MimeType == "image/jpeg" ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void RotateFlip(RotateFlipType t) { if (t == RotateFlipType.RotateNoneFlipNone) return; var n = new SKBitmap(t == RotateFlipType.Rotate90FlipNone || t == RotateFlipType.Rotate270FlipNone ? Height : Width, t == RotateFlipType.Rotate90FlipNone || t == RotateFlipType.Rotate270FlipNone ? Width : Height); using (var c = new SKCanvas(n)) { if (t == RotateFlipType.RotateNoneFlipX) { c.Scale(-1, 1); c.Translate(-Width, 0); } else if (t == RotateFlipType.RotateNoneFlipY) { c.Scale(1, -1); c.Translate(0, -Height); } else if (t == RotateFlipType.Rotate90FlipNone) { c.Translate(Height, 0); c.RotateDegrees(90); } else if (t == RotateFlipType.Rotate180FlipNone) { c.Translate(Width, Height); c.RotateDegrees(180); } else if (t == RotateFlipType.Rotate270FlipNone) { c.Translate(0, Width); c.RotateDegrees(270); } c.DrawBitmap(Bmp, 0, 0); } Bmp.Dispose(); Bmp = n; }
        public Image GetThumbnailImage(int w, int h, Func<bool> abort, IntPtr data) { return new Bitmap(this, w, h); }
        public virtual void Dispose() { if (Bmp != null) { Bmp.Dispose(); Bmp = null; } }
        public object Clone() { return new Bitmap(Bmp.Copy()); }
    }
    public sealed class Bitmap : Image
    {
        public Bitmap(int w, int h) : this(w, h, PixelFormat.Format32bppArgb) { }
        public Bitmap(int w, int h, PixelFormat f) { PixelFormat = f; Bmp = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Bgra8888, f == PixelFormat.Format32bppPArgb ? SKAlphaType.Premul : (f == PixelFormat.Format24bppRgb ? SKAlphaType.Opaque : SKAlphaType.Unpremul))); Bmp.Erase(SKColors.Transparent); }
        public Bitmap(Image src) : this(src, src.Width, src.Height) { }
        public Bitmap(Image src, int w, int h) { Bmp = new SKBitmap(Math.Max(1, w), Math.Max(1, h)); using (var c = new SKCanvas(Bmp)) using (var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true }) c.DrawBitmap(src.Bmp, new SKRect(0, 0, w, h), p); }
        public Bitmap(Image src, Size s) : this(src, s.Width, s.Height) { }
        public Bitmap(string path) { using (var s = System.IO.File.OpenRead(path)) Bmp = SKBitmap.Decode(s); }
        public Bitmap(System.IO.Stream s) { Bmp = SKBitmap.Decode(s); }
        internal Bitmap(SKBitmap b) { Bmp = b; }
        public Color GetPixel(int x, int y) { var c = Bmp.GetPixel(x, y); return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue); }
        public void SetPixel(int x, int y, Color c) { Bmp.SetPixel(x, y, Sk.C(c)); }
        public void MakeTransparent(Color c) { var k = Sk.C(c); for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) if (Bmp.GetPixel(x, y) == k) Bmp.SetPixel(x, y, SKColors.Transparent); }
        public void SetResolution(float x, float y) { }
        public Bitmap Clone(Rectangle r, PixelFormat f) { var n = new SKBitmap(r.Width, r.Height); Bmp.ExtractSubset(n, Sk.R(r).ToSKRectI()); return new Bitmap(n.Copy()); }
        public Bitmap Clone(RectangleF r, PixelFormat f) { return Clone(Rectangle.Round(r), f); }
        public BitmapData LockBits(Rectangle r, ImageLockMode m, PixelFormat f) { return new BitmapData { Width = r.Width, Height = r.Height, Stride = Bmp.RowBytes, PixelFormat = f, Scan0 = Bmp.GetPixels() + r.Y * Bmp.RowBytes + r.X * 4 }; }
        public void UnlockBits(BitmapData d) { Bmp.NotifyPixelsChanged(); }
        public IntPtr GetHicon() { return IntPtr.Zero; }
    }
    static class SkExt { public static SKRectI ToSKRectI(this SKRect r) { return new SKRectI((int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom); } }
    public enum RotateFlipType { RotateNoneFlipNone = 0, Rotate90FlipNone = 1, Rotate180FlipNone = 2, Rotate270FlipNone = 3, RotateNoneFlipX = 4, RotateNoneFlipY = 6 }
    public sealed class Icon : IDisposable { public Icon(System.IO.Stream s) { } public Icon(string p) { } public static Icon FromHandle(IntPtr h) { return new Icon((string)null); } public static Icon ExtractAssociatedIcon(string path) { return null; } public Bitmap ToBitmap() { return new Bitmap(32, 32); } public void Dispose() { } }

    // ---------------- regions ----------------
    public sealed class Region : IDisposable
    {
        internal SKPath Path;
        public Region() { Path = new SKPath(); Path.AddRect(new SKRect(-1e6f, -1e6f, 1e6f, 1e6f)); }
        public Region(RectangleF r) { Path = new SKPath(); Path.AddRect(Sk.R(r)); } public Region(Rectangle r) : this((RectangleF)r) { }
        public Region(GraphicsPath p) { Path = new SKPath(p.P); }
        public bool IsVisible(PointF p) { return Path.Contains(p.X, p.Y); } public bool IsVisible(float x, float y) { return Path.Contains(x, y); }
        public RectangleF GetBounds(Graphics g) { var b = Path.Bounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); }
        public void Union(RectangleF r) { using (var o = new SKPath()) { o.AddRect(Sk.R(r)); Path = Path.Op(o, SKPathOp.Union) ?? Path; } }
        public void Exclude(GraphicsPath gp) { Path = Path.Op(gp.P, SKPathOp.Difference) ?? Path; } public void Union(GraphicsPath gp) { Path = Path.Op(gp.P, SKPathOp.Union) ?? Path; } public void Intersect(GraphicsPath gp) { Path = Path.Op(gp.P, SKPathOp.Intersect) ?? Path; } public void Exclude(Region r) { Path = Path.Op(r.Path, SKPathOp.Difference) ?? Path; } public bool IsEmpty(Graphics g) { return Path.IsEmpty; } public void MakeEmpty() { Path = new SKPath(); } public void MakeInfinite() { Path = new SKPath(); Path.AddRect(new SKRect(-1e6f, -1e6f, 1e6f, 1e6f)); } public Region Clone() { return new Region { Path = new SKPath(Path) }; }
        public void Exclude(RectangleF r) { using (var o = new SKPath()) { o.AddRect(Sk.R(r)); Path = Path.Op(o, SKPathOp.Difference) ?? Path; } }
        public void Intersect(RectangleF r) { using (var o = new SKPath()) { o.AddRect(Sk.R(r)); Path = Path.Op(o, SKPathOp.Intersect) ?? Path; } }
        public void Dispose() { }
    }
    public sealed class GraphicsState { internal int Count; internal GraphicsState(int c) { Count = c; } }

    // ---------------- Graphics ----------------
    public interface IDeviceContext { }
    public sealed class Graphics : IDisposable, IDeviceContext
    {
        readonly SKCanvas c; readonly bool own; readonly int baseSave; readonly Stack<object> extra = new Stack<object>();
        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.None; public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Bilinear;
        public PixelOffsetMode PixelOffsetMode { get; set; } public CompositingMode CompositingMode { get; set; } public CompositingQuality CompositingQuality { get; set; } public TextRenderingHint TextRenderingHint { get; set; }
        public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Pixel; public float PageScale { get; set; } = 1f; public float DpiX { get { return 96f; } } public float DpiY { get { return 96f; } }
        public Graphics(SKCanvas canvas, bool owns = false) { c = canvas; own = owns; baseSave = c.Save(); }
        public static Graphics FromImage(Image img) { return new Graphics(new SKCanvas(img.Bmp), true); }
        public static Graphics FromHwnd(IntPtr h) { return new Graphics(new SKCanvas(new SKBitmap(1, 1)), true); }
        public SKCanvas Canvas { get { return c; } }
        SKPaint Paint(bool aaDefault = false) { return new SKPaint { IsAntialias = SmoothingMode == SmoothingMode.AntiAlias || SmoothingMode == SmoothingMode.HighQuality || aaDefault, BlendMode = CompositingMode == CompositingMode.SourceCopy ? SKBlendMode.Src : SKBlendMode.SrcOver, FilterQuality = InterpolationMode == InterpolationMode.NearestNeighbor ? SKFilterQuality.None : (InterpolationMode == InterpolationMode.HighQualityBicubic || InterpolationMode == InterpolationMode.HighQualityBilinear || InterpolationMode == InterpolationMode.High ? SKFilterQuality.High : SKFilterQuality.Medium) }; }

        // state and clipping
        public GraphicsState Save() { return new GraphicsState(c.Save()); }
        public void Restore(GraphicsState s) { if (s != null) c.RestoreToCount(s.Count); }
        public void ResetClip() { var m = c.TotalMatrix; c.RestoreToCount(baseSave); c.Save(); c.SetMatrix(m); }
        public void SetClip(RectangleF r) { SetClip(r, CombineMode.Replace); } public void SetClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Replace); }
        public void SetClip(RectangleF r, CombineMode m) { if (m == CombineMode.Replace) ResetClip(); if (m == CombineMode.Exclude) c.ClipRect(Sk.R(r), SKClipOperation.Difference, true); else c.ClipRect(Sk.R(r), SKClipOperation.Intersect, true); }
        public void SetClip(Rectangle r, CombineMode m) { SetClip((RectangleF)r, m); }
        public void SetClip(GraphicsPath p) { SetClip(p, CombineMode.Replace); }
        public void SetClip(GraphicsPath p, CombineMode m) { if (m == CombineMode.Replace) ResetClip(); c.ClipPath(p.P, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, true); }
        public void SetClip(Region r, CombineMode m) { if (m == CombineMode.Replace) ResetClip(); c.ClipPath(r.Path, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, true); }
        public void SetClip(Graphics g) { }
        public void IntersectClip(RectangleF r) { c.ClipRect(Sk.R(r), SKClipOperation.Intersect, true); } public void ExcludeClip(Rectangle r) { c.ClipRect(Sk.R(r), SKClipOperation.Difference, true); }
        public Region Clip { get { var b = c.LocalClipBounds; return new Region(RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom)); } set { ResetClip(); if (value != null) c.ClipPath(value.Path, SKClipOperation.Intersect, true); } }
        public RectangleF ClipBounds { get { var b = c.LocalClipBounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); } }
        public bool IsClipEmpty { get { return c.LocalClipBounds.IsEmpty; } } public bool IsVisibleClipEmpty { get { return IsClipEmpty; } }
        public RectangleF VisibleClipBounds { get { return ClipBounds; } }
        public bool IsVisible(RectangleF r) { return !c.QuickReject(Sk.R(r)); } public bool IsVisible(PointF p) { return ClipBounds.Contains(p); }

        // transforms
        public Matrix Transform { get { return new Matrix(c.TotalMatrix); } set { c.SetMatrix(value != null ? value.M : SKMatrix.Identity); } }
        public void ResetTransform() { c.ResetMatrix(); }
        public void TranslateTransform(float dx, float dy) { c.Translate(dx, dy); } public void TranslateTransform(float dx, float dy, MatrixOrder o) { if (o == MatrixOrder.Append) c.SetMatrix(c.TotalMatrix.PostConcat(SKMatrix.CreateTranslation(dx, dy))); else c.Translate(dx, dy); }
        public void ScaleTransform(float sx, float sy) { c.Scale(sx, sy); } public void ScaleTransform(float sx, float sy, MatrixOrder o) { if (o == MatrixOrder.Append) c.SetMatrix(c.TotalMatrix.PostConcat(SKMatrix.CreateScale(sx, sy))); else c.Scale(sx, sy); }
        public void RotateTransform(float a) { c.RotateDegrees(a); } public void RotateTransform(float a, MatrixOrder o) { if (o == MatrixOrder.Append) c.SetMatrix(c.TotalMatrix.PostConcat(SKMatrix.CreateRotationDegrees(a))); else c.RotateDegrees(a); }
        public void MultiplyTransform(Matrix m) { var mm = m.M; c.Concat(ref mm); }

        // filling
        public void Clear(Color col) { c.Clear(Sk.C(col)); }
        public void FillRectangle(Brush b, RectangleF r) { using (var p = Paint()) { p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawRect(Sk.R(r), p); } }
        public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, (RectangleF)r); } public void FillRectangle(Brush b, float x, float y, float w, float h) { FillRectangle(b, new RectangleF(x, y, w, h)); } public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangles(Brush b, RectangleF[] rs) { foreach (var r in rs) FillRectangle(b, r); } public void FillRectangles(Brush b, Rectangle[] rs) { foreach (var r in rs) FillRectangle(b, r); }
        public void FillEllipse(Brush b, RectangleF r) { using (var p = Paint()) { p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawOval(Sk.R(r), p); } }
        public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, (RectangleF)r); } public void FillEllipse(Brush b, float x, float y, float w, float h) { FillEllipse(b, new RectangleF(x, y, w, h)); } public void FillEllipse(Brush b, int x, int y, int w, int h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void FillPath(Brush b, GraphicsPath path) { using (var p = Paint()) { p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawPath(path.P, p); } }
        public void FillRegion(Brush b, Region r) { using (var p = Paint()) { p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawPath(r.Path, p); } }
        public void FillPolygon(Brush b, PointF[] pts) { FillPolygon(b, pts, FillMode.Alternate); }
        public void FillPolygon(Brush b, PointF[] pts, FillMode m) { if (pts == null || pts.Length < 3) return; using (var path = new SKPath { FillType = m == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd }) using (var p = Paint()) { path.AddPoly(Array.ConvertAll(pts, Sk.P), true); p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawPath(path, p); } }
        public void FillPolygon(Brush b, Point[] pts) { FillPolygon(b, Array.ConvertAll(pts, q => (PointF)q)); }
        public void FillPie(Brush b, RectangleF r, float start, float sweep) { using (var path = new SKPath()) using (var p = Paint()) { path.MoveTo(r.X + r.Width / 2, r.Y + r.Height / 2); path.ArcTo(Sk.R(r), start, sweep, false); path.Close(); p.Style = SKPaintStyle.Fill; b.Apply(p); c.DrawPath(path, p); } }
        public void FillPie(Brush b, float x, float y, float w, float h, float start, float sweep) { FillPie(b, new RectangleF(x, y, w, h), start, sweep); }
        public void FillPie(Brush b, Rectangle r, float start, float sweep) { FillPie(b, (RectangleF)r, start, sweep); }
        public void FillClosedCurve(Brush b, PointF[] pts) { FillPolygon(b, pts); }

        // lines and outlines
        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) { using (var p = Paint()) { pen.Apply(p); c.DrawLine(x1, y1, x2, y2, p); } }
        public void DrawLine(Pen pen, PointF a, PointF b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); } public void DrawLine(Pen pen, Point a, Point b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); } public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) { DrawLine(pen, (float)x1, y1, x2, y2); }
        public void DrawLines(Pen pen, PointF[] pts) { if (pts == null || pts.Length < 2) return; using (var path = new SKPath()) using (var p = Paint()) { path.AddPoly(Array.ConvertAll(pts, Sk.P), false); pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawLines(Pen pen, Point[] pts) { DrawLines(pen, Array.ConvertAll(pts, q => (PointF)q)); }
        public void DrawPolygon(Pen pen, PointF[] pts) { if (pts == null || pts.Length < 2) return; using (var path = new SKPath()) using (var p = Paint()) { path.AddPoly(Array.ConvertAll(pts, Sk.P), true); pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawPolygon(Pen pen, Point[] pts) { DrawPolygon(pen, Array.ConvertAll(pts, q => (PointF)q)); }
        public void DrawRectangle(Pen pen, RectangleF r) { using (var p = Paint()) { pen.Apply(p); c.DrawRect(Sk.R(r), p); } }
        public void DrawRectangle(Pen pen, Rectangle r) { DrawRectangle(pen, (RectangleF)r); } public void DrawRectangle(Pen pen, float x, float y, float w, float h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); } public void DrawRectangle(Pen pen, int x, int y, int w, int h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void DrawRectangles(Pen pen, RectangleF[] rs) { foreach (var r in rs) DrawRectangle(pen, r); }
        public void DrawEllipse(Pen pen, RectangleF r) { using (var p = Paint()) { pen.Apply(p); c.DrawOval(Sk.R(r), p); } }
        public void DrawEllipse(Pen pen, Rectangle r) { DrawEllipse(pen, (RectangleF)r); } public void DrawEllipse(Pen pen, float x, float y, float w, float h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); } public void DrawEllipse(Pen pen, int x, int y, int w, int h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void DrawArc(Pen pen, RectangleF r, float start, float sweep) { using (var path = new SKPath()) using (var p = Paint()) { path.AddArc(Sk.R(r), start, sweep); pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawArc(Pen pen, Rectangle r, float start, float sweep) { DrawArc(pen, (RectangleF)r, start, sweep); } public void DrawArc(Pen pen, float x, float y, float w, float h, float start, float sweep) { DrawArc(pen, new RectangleF(x, y, w, h), start, sweep); }
        public void DrawPie(Pen pen, RectangleF r, float start, float sweep) { using (var path = new SKPath()) using (var p = Paint()) { path.MoveTo(r.X + r.Width / 2, r.Y + r.Height / 2); path.ArcTo(Sk.R(r), start, sweep, false); path.Close(); pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawPath(Pen pen, GraphicsPath path) { using (var p = Paint()) { pen.Apply(p); c.DrawPath(path.P, p); } }
        public void DrawBezier(Pen pen, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) { using (var path = new SKPath()) using (var p = Paint()) { path.MoveTo(x1, y1); path.CubicTo(x2, y2, x3, y3, x4, y4); pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawBezier(Pen pen, PointF a, PointF b, PointF cc, PointF d) { DrawBezier(pen, a.X, a.Y, b.X, b.Y, cc.X, cc.Y, d.X, d.Y); }
        public void DrawCurve(Pen pen, PointF[] pts) { DrawCurve(pen, pts, 0.5f); }
        public void DrawCurve(Pen pen, PointF[] pts, float tension) { if (pts == null || pts.Length < 2) return; using (var path = GraphicsPath.Cardinal(pts, tension, false)) using (var p = Paint()) { pen.Apply(p); c.DrawPath(path, p); } }
        public void DrawClosedCurve(Pen pen, PointF[] pts) { if (pts == null || pts.Length < 3) return; using (var path = GraphicsPath.Cardinal(pts, 0.5f, true)) using (var p = Paint()) { pen.Apply(p); c.DrawPath(path, p); } }

        // pictures
        void Img(Image img, SKRect dst, SKRect? src, ImageAttributes ia) { if (img == null || img.Bmp == null) return; using (var p = Paint(true)) { if (ia != null) ia.Apply(p); if (src.HasValue) c.DrawBitmap(img.Bmp, src.Value, dst, p); else c.DrawBitmap(img.Bmp, dst, p); } }
        public void DrawImage(Image img, float x, float y) { Img(img, new SKRect(x, y, x + img.Width, y + img.Height), null, null); }
        public void DrawImage(Image img, int x, int y) { DrawImage(img, (float)x, y); } public void DrawImage(Image img, PointF p) { DrawImage(img, p.X, p.Y); } public void DrawImage(Image img, Point p) { DrawImage(img, (float)p.X, p.Y); }
        public void DrawImage(Image img, RectangleF r) { Img(img, Sk.R(r), null, null); } public void DrawImage(Image img, Rectangle r) { Img(img, Sk.R(r), null, null); }
        public void DrawImage(Image img, float x, float y, float w, float h) { Img(img, new SKRect(x, y, x + w, y + h), null, null); } public void DrawImage(Image img, int x, int y, int w, int h) { DrawImage(img, (float)x, y, w, h); }
        public void DrawImage(Image img, RectangleF dst, RectangleF src, GraphicsUnit u) { Img(img, Sk.R(dst), Sk.R(src), null); }
        public void DrawImage(Image img, Rectangle dst, Rectangle src, GraphicsUnit u) { Img(img, Sk.R(dst), Sk.R(src), null); }
        public void DrawImage(Image img, Rectangle dst, int sx, int sy, int sw, int sh, GraphicsUnit u) { Img(img, Sk.R(dst), new SKRect(sx, sy, sx + sw, sy + sh), null); }
        public void DrawImage(Image img, Rectangle dst, int sx, int sy, int sw, int sh, GraphicsUnit u, ImageAttributes ia) { Img(img, Sk.R(dst), new SKRect(sx, sy, sx + sw, sy + sh), ia); }
        public void DrawImage(Image img, Rectangle dst, float sx, float sy, float sw, float sh, GraphicsUnit u, ImageAttributes ia) { Img(img, Sk.R(dst), new SKRect(sx, sy, sx + sw, sy + sh), ia); }
        public void DrawImage(Image img, Rectangle dst, float sx, float sy, float sw, float sh, GraphicsUnit u) { Img(img, Sk.R(dst), new SKRect(sx, sy, sx + sw, sy + sh), null); }
        public void DrawImage(Image img, PointF[] dst, RectangleF src, GraphicsUnit u, ImageAttributes ia) { if (dst.Length < 3) return; Img(img, new SKRect(dst[0].X, dst[0].Y, dst[1].X, dst[2].Y), Sk.R(src), ia); }
        public void DrawImage(Image img, PointF[] dst, RectangleF src, GraphicsUnit u) { DrawImage(img, dst, src, u, null); }
        public void DrawImage(Image img, PointF[] dstParallelogram) { if (dstParallelogram.Length < 3) return; var a = dstParallelogram[0]; var b = dstParallelogram[1]; var d = dstParallelogram[2]; Img(img, new SKRect(a.X, a.Y, b.X, d.Y), null, null); }
        public void DrawImageUnscaled(Image img, int x, int y) { DrawImage(img, (float)x, y); } public void DrawImageUnscaled(Image img, Point p) { DrawImage(img, (float)p.X, p.Y); }
        public void DrawImageUnscaledAndClipped(Image img, Rectangle r) { var s = c.Save(); c.ClipRect(Sk.R(r)); DrawImage(img, (float)r.X, r.Y); c.RestoreToCount(s); }
        public void DrawIcon(Icon i, Rectangle r) { }

        // text
        static readonly char[] Spaces = { ' ' };
        internal List<string> Lines(string s, Font f, float maxW, StringFormat fmt)
        {
            var res = new List<string>(); if (s == null) return res; bool wrap = maxW > 0 && (fmt == null || (fmt.FormatFlags & StringFormatFlags.NoWrap) == 0);
            foreach (var para in s.Replace("\r\n", "\n").Split('\n'))
            {
                if (!wrap || f.Sk_.W(para) <= maxW) { res.Add(para); continue; }
                string line = ""; foreach (var word in para.Split(Spaces))
                {
                    string tryL = line.Length == 0 ? word : line + " " + word;
                    if (f.Sk_.W(tryL) <= maxW || line.Length == 0) { if (f.Sk_.W(tryL) > maxW && line.Length == 0) { string part = ""; foreach (char ch in word) { if (f.Sk_.W(part + ch) > maxW && part.Length > 0) { res.Add(part); part = ""; } part += ch; } line = part; } else line = tryL; }
                    else { res.Add(line); line = word; }
                }
                res.Add(line);
            }
            return res;
        }
        string Trim(string s, Font f, float maxW, StringFormat fmt)
        {
            if (maxW <= 0 || fmt == null || fmt.Trimming == StringTrimming.None || f.Sk_.W(s) <= maxW) return s;
            bool ell = fmt.Trimming == StringTrimming.EllipsisCharacter || fmt.Trimming == StringTrimming.EllipsisWord || fmt.Trimming == StringTrimming.EllipsisPath; string tail = ell ? "…" : "";
            int lo = 0, hi = s.Length; while (lo < hi) { int mid = (lo + hi + 1) / 2; if (f.Sk_.W(s.Substring(0, mid) + tail) <= maxW) lo = mid; else hi = mid - 1; } return s.Substring(0, lo) + tail;
        }
        public void DrawString(string s, Font f, Brush b, float x, float y) { DrawString(s, f, b, new RectangleF(x, y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, PointF p) { DrawString(s, f, b, new RectangleF(p.X, p.Y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat fmt) { DrawString(s, f, b, new RectangleF(x, y, 0, 0), fmt); }
        public void DrawString(string s, Font f, Brush b, PointF p, StringFormat fmt) { DrawString(s, f, b, new RectangleF(p.X, p.Y, 0, 0), fmt); }
        public void DrawString(string s, Font f, Brush b, RectangleF r) { DrawString(s, f, b, r, null); }
        public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat fmt)
        {
            if (string.IsNullOrEmpty(s)) return; var lines = Lines(s, f, r.Width, fmt); float lh = f.Sk_.Spacing; var m = f.Sk_.Metrics; float total = lh * lines.Count;
            bool clip = r.Width > 0 && r.Height > 0 && (fmt == null || (fmt.FormatFlags & StringFormatFlags.NoClip) == 0); int sv = c.Save(); if (clip) c.ClipRect(Sk.R(r), SKClipOperation.Intersect, true);
            float y0 = r.Y; if (r.Height > 0 && fmt != null) { if (fmt.LineAlignment == StringAlignment.Center) y0 = r.Y + (r.Height - total) / 2f; else if (fmt.LineAlignment == StringAlignment.Far) y0 = r.Bottom - total; }
            using (var p = new SKPaint { IsAntialias = true }) { p.Style = SKPaintStyle.Fill; b.Apply(p);
                for (int i = 0; i < lines.Count; i++)
                {
                    string ln = Trim(lines[i], f, r.Width, fmt); float w = f.Sk_.W(ln); float x = r.X;
                    if (r.Width > 0 && fmt != null) { if (fmt.Alignment == StringAlignment.Center) x = r.X + (r.Width - w) / 2f; else if (fmt.Alignment == StringAlignment.Far) x = r.Right - w; }
                    else if (fmt != null && r.Width == 0) { if (fmt.Alignment == StringAlignment.Center) x = r.X - w / 2f; else if (fmt.Alignment == StringAlignment.Far) x = r.X - w; }
                    c.DrawText(ln, x, y0 + i * lh - m.Ascent, f.Sk_, p);
                    if (f.Style.HasFlag(FontStyle.Underline)) c.DrawLine(x, y0 + i * lh - m.Ascent + 2, x + w, y0 + i * lh - m.Ascent + 2, p);
                } }
            c.RestoreToCount(sv);
        }
        public SizeF MeasureString(string s, Font f) { return MeasureString(s, f, 0, null); }
        public SizeF MeasureString(string s, Font f, int width) { return MeasureString(s, f, width, null); }
        public SizeF MeasureString(string s, Font f, SizeF area) { return MeasureString(s, f, (int)area.Width, null); }
        public SizeF MeasureString(string s, Font f, SizeF area, StringFormat fmt) { return MeasureString(s, f, (int)area.Width, fmt); }
        public SizeF MeasureString(string s, Font f, PointF p, StringFormat fmt) { return MeasureString(s, f, 0, fmt); }
        public SizeF MeasureString(string s, Font f, int width, StringFormat fmt)
        {
            if (string.IsNullOrEmpty(s)) return new SizeF(0, f.Sk_.Spacing); var lines = Lines(s, f, width, fmt); float w = 0; foreach (var l in lines) w = Math.Max(w, f.Sk_.W(l));
            bool typo = fmt != null && fmt.Trimming == StringTrimming.None && (fmt.FormatFlags & StringFormatFlags.FitBlackBox) != 0; return new SizeF(w + (typo ? 0 : f.Sk_.Size / 6f), f.Sk_.Spacing * lines.Count);
        }
        public Region[] MeasureCharacterRanges(string s, Font f, RectangleF r, StringFormat fmt) { return new[] { new Region(new RectangleF(r.X, r.Y, MeasureString(s, f).Width, f.Sk_.Spacing)) }; }

        public IntPtr GetHdc() { return IntPtr.Zero; } public void ReleaseHdc(IntPtr h) { } public void Flush() { c.Flush(); }
        public void Dispose() { c.RestoreToCount(baseSave); if (own) c.Dispose(); }
    }

    public static class Brushes { public static Brush White { get { return new SolidBrush(Color.White); } } public static Brush Black { get { return new SolidBrush(Color.Black); } } public static Brush Transparent { get { return new SolidBrush(Color.Transparent); } } public static Brush Gray { get { return new SolidBrush(Color.Gray); } } public static Brush Red { get { return new SolidBrush(Color.Red); } } }
    public static class Pens { public static Pen White { get { return new Pen(Color.White); } } public static Pen Black { get { return new Pen(Color.Black); } } public static Pen Gray { get { return new Pen(Color.Gray); } } }
    public static class SystemFonts { public static Font DefaultFont { get { return new Font("Segoe UI", 9f); } } public static Font MessageBoxFont { get { return new Font("Segoe UI", 9f); } } }
}
