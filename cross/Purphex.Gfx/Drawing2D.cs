// Purphex.Gfx: System.Drawing.Drawing2D / Imaging / Text on SkiaSharp (paths, gradient brushes, matrices, picture settings)
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing.Drawing2D
{
    public enum SmoothingMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, AntiAlias = 4 }
    public enum InterpolationMode { Invalid = -1, Default = 0, Low = 1, High = 2, Bilinear = 3, Bicubic = 4, NearestNeighbor = 5, HighQualityBilinear = 6, HighQualityBicubic = 7 }
    public enum PixelOffsetMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, Half = 4 }
    public enum CompositingMode { SourceOver, SourceCopy }
    public enum CompositingQuality { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, GammaCorrected = 3, AssumeLinear = 4 }
    public enum DashStyle { Solid, Dash, Dot, DashDot, DashDotDot, Custom }
    public enum DashCap { Flat = 0, Round = 2, Triangle = 3 }
    public enum LineJoin { Miter, Bevel, Round, MiterClipped }
    public enum LineCap { Flat = 0, Square = 1, Round = 2, Triangle = 3, NoAnchor = 16, SquareAnchor = 17, RoundAnchor = 18, DiamondAnchor = 19, ArrowAnchor = 20, Custom = 255, AnchorMask = 240 }
    public enum PenAlignment { Center, Inset, Outset, Left, Right }
    public enum WrapMode { Tile, TileFlipX, TileFlipY, TileFlipXY, Clamp }
    public enum FillMode { Alternate, Winding }
    public enum CombineMode { Replace, Intersect, Union, Xor, Exclude, Complement }
    public enum MatrixOrder { Prepend, Append }
    public enum LinearGradientMode { Horizontal, Vertical, ForwardDiagonal, BackwardDiagonal }
    public enum HatchStyle { Horizontal = 0, Vertical = 1, ForwardDiagonal = 2, BackwardDiagonal = 3, Cross = 4, DiagonalCross = 5, Percent05 = 6, Percent10 = 7, Percent20 = 8, Percent25 = 9, Percent50 = 18, LightUpwardDiagonal = 21, DarkUpwardDiagonal = 23, WideUpwardDiagonal = 25, SmallCheckerBoard = 49, LargeCheckerBoard = 50 }

    public sealed class Matrix : IDisposable
    {
        internal SKMatrix M;
        public Matrix() { M = SKMatrix.Identity; } internal Matrix(SKMatrix m) { M = m; }
        public Matrix(float m11, float m12, float m21, float m22, float dx, float dy) { M = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1); }
        public float[] Elements { get { return new[] { M.ScaleX, M.SkewY, M.SkewX, M.ScaleY, M.TransX, M.TransY }; } }
        public float OffsetX { get { return M.TransX; } } public float OffsetY { get { return M.TransY; } } public bool IsIdentity { get { return M.IsIdentity; } }
        public void Translate(float dx, float dy) { Translate(dx, dy, MatrixOrder.Prepend); } public void Translate(float dx, float dy, MatrixOrder o) { var t = SKMatrix.CreateTranslation(dx, dy); M = o == MatrixOrder.Append ? M.PostConcat(t) : M.PreConcat(t); }
        public void Scale(float sx, float sy) { Scale(sx, sy, MatrixOrder.Prepend); } public void Scale(float sx, float sy, MatrixOrder o) { var t = SKMatrix.CreateScale(sx, sy); M = o == MatrixOrder.Append ? M.PostConcat(t) : M.PreConcat(t); }
        public void Rotate(float a) { Rotate(a, MatrixOrder.Prepend); } public void Rotate(float a, MatrixOrder o) { var t = SKMatrix.CreateRotationDegrees(a); M = o == MatrixOrder.Append ? M.PostConcat(t) : M.PreConcat(t); }
        public void RotateAt(float a, PointF p) { RotateAt(a, p, MatrixOrder.Prepend); } public void RotateAt(float a, PointF p, MatrixOrder o) { var t = SKMatrix.CreateRotationDegrees(a, p.X, p.Y); M = o == MatrixOrder.Append ? M.PostConcat(t) : M.PreConcat(t); }
        public void Shear(float sx, float sy) { M = M.PreConcat(SKMatrix.CreateSkew(sx, sy)); }
        public void Multiply(Matrix m) { Multiply(m, MatrixOrder.Prepend); } public void Multiply(Matrix m, MatrixOrder o) { M = o == MatrixOrder.Append ? M.PostConcat(m.M) : M.PreConcat(m.M); }
        public void Invert() { SKMatrix inv; if (M.TryInvert(out inv)) M = inv; } public void Reset() { M = SKMatrix.Identity; }
        public void TransformPoints(PointF[] pts) { for (int i = 0; i < pts.Length; i++) { var p = M.MapPoint(pts[i].X, pts[i].Y); pts[i] = new PointF(p.X, p.Y); } }
        public void TransformPoints(Point[] pts) { for (int i = 0; i < pts.Length; i++) { var p = M.MapPoint(pts[i].X, pts[i].Y); pts[i] = new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)); } }
        public Matrix Clone() { return new Matrix(M); } public void Dispose() { }
    }

    public sealed class GraphicsPath : IDisposable, ICloneable
    {
        internal SKPath P = new SKPath(); bool newFigure = true;
        public GraphicsPath() { } public GraphicsPath(FillMode m) { FillMode = m; }
        public FillMode FillMode { get { return P.FillType == SKPathFillType.Winding ? FillMode.Winding : FillMode.Alternate; } set { P.FillType = value == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd; } }
        public int PointCount { get { return P.PointCount; } } public PointF[] PathPoints { get { return Array.ConvertAll(P.Points, p => new PointF(p.X, p.Y)); } }
        void Start(float x, float y) { if (newFigure || P.PointCount == 0) { P.MoveTo(x, y); newFigure = false; } else P.LineTo(x, y); }
        public void AddLine(float x1, float y1, float x2, float y2) { Start(x1, y1); P.LineTo(x2, y2); }
        public void AddLine(PointF a, PointF b) { AddLine(a.X, a.Y, b.X, b.Y); } public void AddLine(int x1, int y1, int x2, int y2) { AddLine((float)x1, y1, x2, y2); }
        public void AddLines(PointF[] pts) { if (pts.Length == 0) return; Start(pts[0].X, pts[0].Y); for (int i = 1; i < pts.Length; i++) P.LineTo(pts[i].X, pts[i].Y); }
        public void AddArc(float x, float y, float w, float h, float start, float sweep)
        {
            var oval = new SKRect(x, y, x + w, y + h); if (w <= 0 || h <= 0) return;
            if (newFigure || P.PointCount == 0) { P.ArcTo(oval, start, sweep, true); newFigure = false; } else P.ArcTo(oval, start, sweep, false);
        }
        public void AddArc(RectangleF r, float start, float sweep) { AddArc(r.X, r.Y, r.Width, r.Height, start, sweep); } public void AddArc(Rectangle r, float start, float sweep) { AddArc(r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) { Start(x1, y1); P.CubicTo(x2, y2, x3, y3, x4, y4); }
        public void AddBezier(PointF a, PointF b, PointF c, PointF d) { AddBezier(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y); }
        public void AddBeziers(PointF[] pts) { if (pts.Length < 4) return; Start(pts[0].X, pts[0].Y); for (int i = 1; i + 2 < pts.Length; i += 3) P.CubicTo(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, pts[i + 2].X, pts[i + 2].Y); }
        public void AddEllipse(float x, float y, float w, float h) { P.AddOval(new SKRect(x, y, x + w, y + h)); newFigure = true; }
        public void AddEllipse(RectangleF r) { AddEllipse(r.X, r.Y, r.Width, r.Height); } public void AddEllipse(Rectangle r) { AddEllipse(r.X, r.Y, r.Width, r.Height); }
        public void AddRectangle(RectangleF r) { P.AddRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom)); newFigure = true; } public void AddRectangle(Rectangle r) { AddRectangle((RectangleF)r); }
        public void AddPolygon(PointF[] pts) { P.AddPoly(Array.ConvertAll(pts, p => new SKPoint(p.X, p.Y)), true); newFigure = true; }
        public void AddPolygon(Point[] pts) { AddPolygon(Array.ConvertAll(pts, p => (PointF)p)); }
        public void AddPie(float x, float y, float w, float h, float start, float sweep) { P.MoveTo(x + w / 2, y + h / 2); P.ArcTo(new SKRect(x, y, x + w, y + h), start, sweep, false); P.Close(); newFigure = true; }
        public void AddPath(GraphicsPath o, bool connect) { P.AddPath(o.P, connect ? SKPathAddMode.Extend : SKPathAddMode.Append); }
        public void AddCurve(PointF[] pts) { using (var c = Cardinal(pts, 0.5f, false)) P.AddPath(c); newFigure = true; }
        public void AddClosedCurve(PointF[] pts) { using (var c = Cardinal(pts, 0.5f, true)) P.AddPath(c); newFigure = true; }
        public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat fmt)
        {
            using (var f = new Font(family, emSize, (FontStyle)style, GraphicsUnit.Pixel)) { var m = f.Sk_.Metrics; using (var tp = f.Sk_.TextPath(s, new SKPoint(origin.X, origin.Y - m.Ascent))) P.AddPath(tp); } newFigure = true;
        }
        public void AddString(string s, FontFamily family, int style, float emSize, RectangleF r, StringFormat fmt)
        {
            using (var f = new Font(family, emSize, (FontStyle)style, GraphicsUnit.Pixel)) { var m = f.Sk_.Metrics; float w = f.Sk_.W(s), h = f.Sk_.Spacing; float x = r.X, y = r.Y;
                if (fmt != null) { if (fmt.Alignment == StringAlignment.Center) x = r.X + (r.Width - w) / 2f; else if (fmt.Alignment == StringAlignment.Far) x = r.Right - w; if (fmt.LineAlignment == StringAlignment.Center) y = r.Y + (r.Height - h) / 2f; else if (fmt.LineAlignment == StringAlignment.Far) y = r.Bottom - h; }
                using (var tp = f.Sk_.TextPath(s, new SKPoint(x, y - m.Ascent))) P.AddPath(tp); } newFigure = true;
        }
        public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat fmt) { AddString(s, family, style, emSize, (PointF)origin, fmt); }
        public void CloseFigure() { P.Close(); newFigure = true; } public void CloseAllFigures() { P.Close(); newFigure = true; } public void StartFigure() { newFigure = true; }
        public void Reset() { P.Reset(); newFigure = true; } public void Reverse() { }
        public RectangleF GetBounds() { var b = P.Bounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); }
        public RectangleF GetBounds(Matrix m) { using (var t = new SKPath(P)) { if (m != null) t.Transform(m.M); var b = t.Bounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); } }
        public RectangleF GetBounds(Matrix m, Pen pen) { var b = GetBounds(m); float w = pen == null ? 0 : pen.Width / 2f; return RectangleF.Inflate(b, w, w); }
        public void Transform(Matrix m) { P.Transform(m.M); }
        public void Widen(Pen pen) { using (var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width }) { var o = new SKPath(); p.GetFillPath(P, o); P = o; } }
        public void Flatten() { } public void Flatten(Matrix m, float f) { if (m != null) P.Transform(m.M); }
        public bool IsVisible(PointF p) { return P.Contains(p.X, p.Y); } public bool IsVisible(float x, float y) { return P.Contains(x, y); } public bool IsVisible(Point p) { return P.Contains(p.X, p.Y); }
        public bool IsOutlineVisible(PointF p, Pen pen) { using (var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, pen.Width) }) using (var o = new SKPath()) { paint.GetFillPath(P, o); return o.Contains(p.X, p.Y); } }
        public object Clone() { var g = new GraphicsPath(); g.P = new SKPath(P); return g; }
        public void Dispose() { P.Dispose(); }
        internal static SKPath Cardinal(PointF[] pts, float tension, bool closed)
        {
            var path = new SKPath(); int n = pts.Length; if (n < 2) return path; path.MoveTo(pts[0].X, pts[0].Y); float t = tension / 3f;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                PointF p0 = pts[closed ? (i - 1 + n) % n : Math.Max(0, i - 1)], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[closed ? (i + 2) % n : Math.Min(n - 1, i + 2)];
                path.CubicTo(p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t, p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t, p2.X, p2.Y);
            }
            if (closed) path.Close(); return path;
        }
    }

    public sealed class ColorBlend { public Color[] Colors; public float[] Positions; public ColorBlend() { } public ColorBlend(int n) { Colors = new Color[n]; Positions = new float[n]; } }
    public sealed class Blend { public float[] Factors; public float[] Positions; public Blend() { } public Blend(int n) { Factors = new float[n]; Positions = new float[n]; } }

    public sealed class LinearGradientBrush : Brush
    {
        PointF a, b; Color c1, c2;
        public ColorBlend InterpolationColors { get; set; } public Blend Blend { get; set; } public WrapMode WrapMode { get; set; } = WrapMode.Tile; public bool GammaCorrection { get; set; }
        public Color[] LinearColors { get { return new[] { c1, c2 }; } set { c1 = value[0]; c2 = value[1]; } }
        public RectangleF Rectangle { get; private set; } public Matrix Transform { get; set; } = new Matrix();
        public LinearGradientBrush(PointF p1, PointF p2, Color col1, Color col2) { a = p1; b = p2; c1 = col1; c2 = col2; Rectangle = RectangleF.FromLTRB(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)); }
        public LinearGradientBrush(Point p1, Point p2, Color col1, Color col2) : this((PointF)p1, (PointF)p2, col1, col2) { }
        public LinearGradientBrush(RectangleF r, Color col1, Color col2, float angle) : this(r, col1, col2, angle, false) { }
        public LinearGradientBrush(RectangleF r, Color col1, Color col2, float angle, bool scale)
        {
            c1 = col1; c2 = col2; Rectangle = r; double rad = angle * Math.PI / 180.0; float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            // like GDI+: the gradient runs along the angle and spans the rectangle exactly (corner to corner along that direction)
            double dx = Math.Cos(rad), dy = Math.Sin(rad); double half = Math.Abs(r.Width / 2 * dx) + Math.Abs(r.Height / 2 * dy);
            a = new PointF((float)(cx - dx * half), (float)(cy - dy * half)); b = new PointF((float)(cx + dx * half), (float)(cy + dy * half));
        }
        public LinearGradientBrush(Rectangle r, Color col1, Color col2, float angle) : this((RectangleF)r, col1, col2, angle) { }
        public LinearGradientBrush(RectangleF r, Color col1, Color col2, LinearGradientMode m) : this(r, col1, col2, m == LinearGradientMode.Horizontal ? 0f : (m == LinearGradientMode.Vertical ? 90f : (m == LinearGradientMode.ForwardDiagonal ? 45f : 135f))) { }
        public LinearGradientBrush(Rectangle r, Color col1, Color col2, LinearGradientMode m) : this((RectangleF)r, col1, col2, m) { }
        public void SetSigmaBellShape(float focus) { SetSigmaBellShape(focus, 1f); }
        public void SetSigmaBellShape(float focus, float scale) { var bl = new Blend(5); bl.Positions = new[] { 0f, focus * 0.5f, focus, focus + (1 - focus) * 0.5f, 1f }; bl.Factors = new[] { 0f, scale * 0.5f, scale, scale * 0.5f, 0f }; if (focus <= 0) { bl.Positions = new[] { 0f, 0.5f, 1f }; bl.Factors = new[] { scale, scale * 0.5f, 0f }; } else if (focus >= 1) { bl.Positions = new[] { 0f, 0.5f, 1f }; bl.Factors = new[] { 0f, scale * 0.5f, scale }; } Blend = bl; }
        public void SetBlendTriangularShape(float focus) { SetBlendTriangularShape(focus, 1f); }
        public void SetBlendTriangularShape(float focus, float scale) { var bl = new Blend(3); bl.Positions = new[] { 0f, Math.Max(0.0001f, Math.Min(0.9999f, focus)), 1f }; bl.Factors = new[] { 0f, scale, 0f }; if (focus <= 0) { bl.Positions = new[] { 0f, 1f }; bl.Factors = new[] { scale, 0f }; } else if (focus >= 1) { bl.Positions = new[] { 0f, 1f }; bl.Factors = new[] { 0f, scale }; } Blend = bl; }
        public void TranslateTransform(float dx, float dy) { Transform.Translate(dx, dy); } public void ScaleTransform(float sx, float sy) { Transform.Scale(sx, sy); } public void RotateTransform(float a) { Transform.Rotate(a); } public void ResetTransform() { Transform = new Matrix(); }
        static SKColor Mix(Color x, Color y, float t) { return new SKColor((byte)(x.R + (y.R - x.R) * t), (byte)(x.G + (y.G - x.G) * t), (byte)(x.B + (y.B - x.B) * t), (byte)(x.A + (y.A - x.A) * t)); }
        internal override void Apply(SKPaint p)
        {
            SKColor[] cols; float[] pos;
            if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length >= 2) { cols = Array.ConvertAll(InterpolationColors.Colors, x => new SKColor(x.R, x.G, x.B, x.A)); pos = InterpolationColors.Positions; }
            else if (Blend != null && Blend.Factors != null && Blend.Factors.Length >= 2) { cols = new SKColor[Blend.Factors.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = Mix(c1, c2, Blend.Factors[i]); pos = Blend.Positions; }
            else { cols = new[] { new SKColor(c1.R, c1.G, c1.B, c1.A), new SKColor(c2.R, c2.G, c2.B, c2.A) }; pos = null; }
            var mode = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : (WrapMode == WrapMode.Tile ? SKShaderTileMode.Clamp : SKShaderTileMode.Mirror);
            p.Color = SKColors.White; p.Shader = SKShader.CreateLinearGradient(new SKPoint(a.X, a.Y), new SKPoint(b.X, b.Y), cols, pos, mode, Transform.M);
        }
    }

    public sealed class PathGradientBrush : Brush
    {
        readonly RectangleF bounds; public Color CenterColor { get; set; } = Color.White; public Color[] SurroundColors { get; set; } = { Color.White }; public PointF CenterPoint { get; set; }
        public ColorBlend InterpolationColors { get; set; } public Blend Blend { get; set; } public PointF FocusScales { get; set; } public WrapMode WrapMode { get; set; } = WrapMode.Clamp;
        public PathGradientBrush(GraphicsPath path) { bounds = path.GetBounds(); CenterPoint = new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2); }
        public PathGradientBrush(PointF[] pts) { using (var g = new GraphicsPath()) { g.AddPolygon(pts); bounds = g.GetBounds(); } CenterPoint = new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2); }
        public void SetSigmaBellShape(float focus) { } public void SetSigmaBellShape(float focus, float scale) { }
        internal override void Apply(SKPaint p)
        {
            // an ellipse that fills its bounds: a radial gradient from the centre out to the edge, stretched to the bounds
            float rx = Math.Max(0.5f, bounds.Width / 2), ry = Math.Max(0.5f, bounds.Height / 2); var edge = SurroundColors != null && SurroundColors.Length > 0 ? SurroundColors[0] : Color.Transparent;
            SKColor[] cols; float[] pos;
            if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length >= 2) { var ic = InterpolationColors.Colors; cols = new SKColor[ic.Length]; pos = new float[ic.Length]; for (int i = 0; i < ic.Length; i++) { var x = ic[ic.Length - 1 - i]; cols[i] = new SKColor(x.R, x.G, x.B, x.A); pos[i] = 1f - InterpolationColors.Positions[ic.Length - 1 - i]; } }
            else { cols = new[] { new SKColor(CenterColor.R, CenterColor.G, CenterColor.B, CenterColor.A), new SKColor(edge.R, edge.G, edge.B, edge.A) }; pos = null; }
            var m = SKMatrix.CreateScale(1f, ry / rx, CenterPoint.X, CenterPoint.Y);
            p.Color = SKColors.White; p.Shader = SKShader.CreateRadialGradient(new SKPoint(CenterPoint.X, CenterPoint.Y), rx, cols, pos, SKShaderTileMode.Clamp, m);
        }
    }
    public sealed class HatchBrush : Brush
    {
        public Color ForegroundColor { get; } public Color BackgroundColor { get; } public HatchStyle HatchStyle { get; }
        public HatchBrush(HatchStyle s, Color fore) : this(s, fore, Color.Transparent) { } public HatchBrush(HatchStyle s, Color fore, Color back) { HatchStyle = s; ForegroundColor = fore; BackgroundColor = back; }
        internal override void Apply(SKPaint p)
        {
            using (var bmp = new SKBitmap(8, 8)) { bmp.Erase(new SKColor(BackgroundColor.R, BackgroundColor.G, BackgroundColor.B, BackgroundColor.A)); var f = new SKColor(ForegroundColor.R, ForegroundColor.G, ForegroundColor.B, ForegroundColor.A);
                for (int i = 0; i < 8; i++) { if (HatchStyle == HatchStyle.Horizontal) bmp.SetPixel(i, 0, f); else if (HatchStyle == HatchStyle.Vertical) bmp.SetPixel(0, i, f); else if (HatchStyle == HatchStyle.SmallCheckerBoard || HatchStyle == HatchStyle.LargeCheckerBoard) { for (int j = 0; j < 8; j++) if (((i / 4) + (j / 4)) % 2 == 0) bmp.SetPixel(i, j, f); } else bmp.SetPixel(i, 7 - i, f); }
                p.Color = SKColors.White; p.Shader = SKShader.CreateBitmap(bmp.Copy(), SKShaderTileMode.Repeat, SKShaderTileMode.Repeat); }
        }
    }
}

namespace System.Drawing.Imaging
{
    public enum PixelFormat { Undefined = 0, Format24bppRgb = 137224, Format32bppRgb = 139273, Format32bppArgb = 2498570, Format32bppPArgb = 925707, Format8bppIndexed = 198659 }
    public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }
    public sealed class BitmapData { public int Width, Height, Stride; public PixelFormat PixelFormat; public IntPtr Scan0; }
    public sealed class ImageFormat { readonly string n; ImageFormat(string n) { this.n = n; } public static readonly ImageFormat Png = new ImageFormat("png"), Jpeg = new ImageFormat("jpeg"), Bmp = new ImageFormat("bmp"), Gif = new ImageFormat("gif"), Icon = new ImageFormat("icon"); public override string ToString() { return n; } }
    public sealed class ImageCodecInfo { public string MimeType; public string FormatDescription; public static ImageCodecInfo[] GetImageEncoders() { return new[] { new ImageCodecInfo { MimeType = "image/jpeg", FormatDescription = "JPEG" }, new ImageCodecInfo { MimeType = "image/png", FormatDescription = "PNG" } }; } }
    public sealed class Encoder { public static readonly Encoder Quality = new Encoder(); }
    public sealed class EncoderParameter : IDisposable { public EncoderParameter(Encoder e, long v) { } public void Dispose() { } }
    public sealed class EncoderParameters : IDisposable { public EncoderParameter[] Param; public EncoderParameters(int n) { Param = new EncoderParameter[n]; } public void Dispose() { } }
    public enum ColorMatrixFlag { Default, SkipGrays, AltGrays }
    public enum ColorAdjustType { Default, Bitmap, Brush, Pen, Text, Count, Any }
    public sealed class ColorMatrix
    {
        public float[][] M = { new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0, 0, 0, 0, 1 } };
        public ColorMatrix() { } public ColorMatrix(float[][] m) { M = m; }
        public float this[int r, int c] { get { return M[r][c]; } set { M[r][c] = value; } }
        public float Matrix00 { get { return M[0][0]; } set { M[0][0] = value; } } public float Matrix11 { get { return M[1][1]; } set { M[1][1] = value; } } public float Matrix22 { get { return M[2][2]; } set { M[2][2] = value; } } public float Matrix33 { get { return M[3][3]; } set { M[3][3] = value; } } public float Matrix44 { get { return M[4][4]; } set { M[4][4] = value; } }
        public float Matrix40 { get { return M[4][0]; } set { M[4][0] = value; } } public float Matrix41 { get { return M[4][1]; } set { M[4][1] = value; } } public float Matrix42 { get { return M[4][2]; } set { M[4][2] = value; } } public float Matrix43 { get { return M[4][3]; } set { M[4][3] = value; } }
    }
    public sealed class ImageAttributes : IDisposable
    {
        ColorMatrix cm; public void SetColorMatrix(ColorMatrix m) { cm = m; } public void SetColorMatrix(ColorMatrix m, ColorMatrixFlag f, ColorAdjustType t) { cm = m; }
        public void SetWrapMode(System.Drawing.Drawing2D.WrapMode m) { } public void SetGamma(float g) { } public void ClearColorMatrix() { cm = null; }
        internal void Apply(SKPaint p)
        {
            if (cm == null) return; var m = cm.M;   // GDI+ is row-vector (rows = input channels), Skia is column (rows = output channels), and its offsets are 0-255
            p.ColorFilter = SKColorFilter.CreateColorMatrix(new[] { m[0][0], m[1][0], m[2][0], m[3][0], m[4][0] * 255, m[0][1], m[1][1], m[2][1], m[3][1], m[4][1] * 255, m[0][2], m[1][2], m[2][2], m[3][2], m[4][2] * 255, m[0][3], m[1][3], m[2][3], m[3][3], m[4][3] * 255 });
        }
        public void Dispose() { }
    }
}

namespace System.Drawing.Text
{
    public enum TextRenderingHint { SystemDefault, SingleBitPerPixelGridFit, SingleBitPerPixel, AntiAliasGridFit, AntiAlias, ClearTypeGridFit }
    public enum HotkeyPrefix { None, Show, Hide }
    public abstract class FontCollection : IDisposable { protected readonly List<FontFamily> fams = new List<FontFamily>(); public FontFamily[] Families { get { return fams.ToArray(); } } public void Dispose() { } }
    public sealed class PrivateFontCollection : FontCollection
    {
        public void AddFontFile(string path) { var tf = SKTypeface.FromFile(path); if (tf != null) fams.Add(new FontFamily(tf.FamilyName, tf)); }
        public void AddMemoryFont(IntPtr p, int len) { var data = new byte[len]; System.Runtime.InteropServices.Marshal.Copy(p, data, 0, len); var tf = SKTypeface.FromData(SKData.CreateCopy(data)); if (tf != null) fams.Add(new FontFamily(tf.FamilyName, tf)); }
    }
    public sealed class InstalledFontCollection : FontCollection { public InstalledFontCollection() { foreach (var n in SKFontManager.Default.FontFamilies) fams.Add(new FontFamily(n)); } }
}
